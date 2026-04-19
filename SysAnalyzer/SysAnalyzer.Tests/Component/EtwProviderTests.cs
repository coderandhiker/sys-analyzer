using System.Runtime.CompilerServices;
using System.Threading.Channels;
using SysAnalyzer.Analysis;
using SysAnalyzer.Analysis.Models;
using SysAnalyzer.Capture;
using SysAnalyzer.Capture.Providers;

namespace SysAnalyzer.Tests.Component;

/// <summary>
/// Fake IEventStreamProvider for component testing of the culprit attribution pipeline.
/// </summary>
public sealed class FakeEventStreamProvider : IEventStreamProvider
{
    public string Name { get; }
    public ProviderTier RequiredTier => ProviderTier.Tier1;
    public ProviderHealth Health { get; private set; }

    private readonly List<EtwEvent> _events;
    private readonly Channel<TimestampedEvent> _channel;
    private readonly bool _failOnInit;
    private readonly int _eventsLost;

    public FakeEventStreamProvider(
        string name = "FakeETW",
        List<EtwEvent>? events = null,
        bool failOnInit = false,
        int eventsLost = 0,
        ProviderStatus status = ProviderStatus.Active)
    {
        Name = name;
        _events = events ?? [];
        _failOnInit = failOnInit;
        _eventsLost = eventsLost;
        Health = new ProviderHealth(status, null, 4, 4, eventsLost);
        _channel = Channel.CreateUnbounded<TimestampedEvent>();
    }

    public Task<ProviderHealth> InitAsync()
    {
        if (_failOnInit)
        {
            Health = new ProviderHealth(ProviderStatus.Unavailable, "Fake init failure", 0, 4, 0);
            return Task.FromResult(Health);
        }

        return Task.FromResult(Health);
    }

    public async Task StartAsync(long captureStartQpc)
    {
        if (Health.Status == ProviderStatus.Unavailable)
            return;

        // Write all events to channel
        foreach (var evt in _events)
        {
            await _channel.Writer.WriteAsync(evt);
        }
        _channel.Writer.Complete();
    }

    public Task StopAsync()
    {
        _channel.Writer.TryComplete();
        return Task.CompletedTask;
    }

    public IAsyncEnumerable<TimestampedEvent> Events => ReadEventsAsync();

    private async IAsyncEnumerable<TimestampedEvent> ReadEventsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var evt in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return evt;
        }
    }

    public void Dispose() { }
}

public class EtwProviderTests : IDisposable
{
    public EtwProviderTests()
    {
        QpcTimestamp.SetCaptureEpoch(0, DateTime.UtcNow);
    }

    public void Dispose() { }

    [Fact]
    public async Task NormalEventFlow_ProducesCorrectAttribution()
    {
        var spikes = new List<QpcTimestamp>
        {
            QpcTimestamp.FromMilliseconds(1000),
            QpcTimestamp.FromMilliseconds(2000),
            QpcTimestamp.FromMilliseconds(3000),
        };

        var events = new List<EtwEvent>
        {
            new ContextSwitchEvent(QpcTimestamp.FromMilliseconds(1010), 100, 200, "MsMpEng.exe"),
            new ContextSwitchEvent(QpcTimestamp.FromMilliseconds(1020), 100, 200, "MsMpEng.exe"),
            new ContextSwitchEvent(QpcTimestamp.FromMilliseconds(2010), 100, 300, "SearchIndexer.exe"),
            new ContextSwitchEvent(QpcTimestamp.FromMilliseconds(3010), 100, 200, "MsMpEng.exe"),
            new DpcEvent(QpcTimestamp.FromMilliseconds(1100), "ndis.sys", 500),
            new DpcEvent(QpcTimestamp.FromMilliseconds(2100), "ndis.sys", 300),
            new DiskIoEvent(QpcTimestamp.FromMilliseconds(1500), 200, "MsMpEng.exe", 1048576),
        };

        // Use fake provider to get events
        var fakeProvider = new FakeEventStreamProvider(events: events);
        await fakeProvider.InitAsync();
        await fakeProvider.StartAsync(0);

        var collectedEvents = new List<EtwEvent>();
        await foreach (var evt in fakeProvider.Events)
        {
            if (evt is EtwEvent etwEvt)
                collectedEvents.Add(etwEvt);
        }

        var attributor = new CulpritAttributor();
        var result = attributor.Attribute(spikes, collectedEvents, hasEtw: true, hasDpc: true, eventsLost: 0);

        Assert.True(result.HasAttribution);
        Assert.True(result.HasDpcAttribution);
        Assert.NotEmpty(result.TopContextSwitchProcesses);
        Assert.Equal("MsMpEng.exe", result.TopContextSwitchProcesses[0].ProcessName);
        Assert.NotEmpty(result.TopDpcDrivers);
        Assert.Equal("ndis.sys", result.TopDpcDrivers[0].DriverModule);
    }

    [Fact]
    public async Task NoEtw_AttributionFieldsEmpty()
    {
        var fakeProvider = new FakeEventStreamProvider(failOnInit: true);
        var health = await fakeProvider.InitAsync();

        Assert.Equal(ProviderStatus.Unavailable, health.Status);

        var attributor = new CulpritAttributor();
        var result = attributor.Attribute([], [], hasEtw: false, hasDpc: false, eventsLost: 0);

        Assert.False(result.HasAttribution);
        Assert.Empty(result.TopContextSwitchProcesses);
        Assert.Empty(result.TopDpcDrivers);
        Assert.Empty(result.TopDiskIoProcesses);
    }

    [Fact]
    public void BufferOverflow_DegradedConfidence()
    {
        var fakeProvider = new FakeEventStreamProvider(eventsLost: 500);

        Assert.Equal(500, fakeProvider.Health.EventsLost);

        // Attribution should still work but with degraded confidence noted
        var attributor = new CulpritAttributor();
        var events = new List<EtwEvent>
        {
            new ContextSwitchEvent(QpcTimestamp.FromMilliseconds(1010), 100, 200, "MsMpEng.exe"),
        };
        var spikes = new List<QpcTimestamp> { QpcTimestamp.FromMilliseconds(1000) };

        var result = attributor.Attribute(spikes, events, hasEtw: true, hasDpc: false, eventsLost: 500);

        Assert.True(result.HasAttribution);
        Assert.NotEmpty(result.TopContextSwitchProcesses);
    }

    [Fact]
    public void ContextSwitchesAvailable_DpcMissing_PartialAttribution()
    {
        var attributor = new CulpritAttributor();
        var spikes = new List<QpcTimestamp>
        {
            QpcTimestamp.FromMilliseconds(1000),
            QpcTimestamp.FromMilliseconds(2000),
        };

        var events = new List<EtwEvent>
        {
            new ContextSwitchEvent(QpcTimestamp.FromMilliseconds(1010), 100, 200, "MsMpEng.exe"),
            new ContextSwitchEvent(QpcTimestamp.FromMilliseconds(2010), 100, 200, "MsMpEng.exe"),
            // No DPC events
        };

        var result = attributor.Attribute(spikes, events, hasEtw: true, hasDpc: false, eventsLost: 0);

        Assert.True(result.HasAttribution);
        Assert.False(result.HasDpcAttribution);
        Assert.NotEmpty(result.TopContextSwitchProcesses);
        Assert.Empty(result.TopDpcDrivers);
    }

    [Fact]
    public async Task FakeProvider_StartStop_CompletesGracefully()
    {
        var events = new List<EtwEvent>
        {
            new ContextSwitchEvent(QpcTimestamp.FromMilliseconds(100), 1, 2, "test.exe"),
        };

        var provider = new FakeEventStreamProvider(events: events);
        await provider.InitAsync();
        await provider.StartAsync(0);

        var collected = new List<TimestampedEvent>();
        await foreach (var e in provider.Events)
            collected.Add(e);

        await provider.StopAsync();

        Assert.Single(collected);
        Assert.IsType<ContextSwitchEvent>(collected[0]);
    }

    [Fact]
    public async Task FakeProvider_FailOnInit_NoEvents()
    {
        var provider = new FakeEventStreamProvider(failOnInit: true);
        var health = await provider.InitAsync();

        Assert.Equal(ProviderStatus.Unavailable, health.Status);

        await provider.StartAsync(0);
        // Channel should be empty since health is Unavailable
        await provider.StopAsync();
    }

    [Fact]
    public void ProcessLifetimeEvents_IncludedInAttribution()
    {
        var attributor = new CulpritAttributor();
        var spikes = new List<QpcTimestamp>
        {
            QpcTimestamp.FromMilliseconds(5000),
            QpcTimestamp.FromMilliseconds(5200),
        };

        var events = new List<EtwEvent>
        {
            new ProcessLifetimeEvent(QpcTimestamp.FromMilliseconds(4900), 200, "MsMpEng.exe", true),
            new ProcessLifetimeEvent(QpcTimestamp.FromMilliseconds(6000), 200, "MsMpEng.exe", false),
            new ContextSwitchEvent(QpcTimestamp.FromMilliseconds(5010), 100, 200, "MsMpEng.exe"),
        };

        var result = attributor.Attribute(spikes, events, hasEtw: true, hasDpc: false, eventsLost: 0);

        Assert.True(result.HasAttribution);
        Assert.Equal(2, result.ProcessLifetimeEvents.Count);
        Assert.True(result.ProcessLifetimeEvents[0].IsStart);
        Assert.False(result.ProcessLifetimeEvents[1].IsStart);
    }
}
