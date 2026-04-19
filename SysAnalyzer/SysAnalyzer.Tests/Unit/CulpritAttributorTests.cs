using System.Text.Json;
using SysAnalyzer.Analysis;
using SysAnalyzer.Capture;

namespace SysAnalyzer.Tests.Unit;

public class CulpritAttributorTests : IDisposable
{
    public CulpritAttributorTests()
    {
        // Set a known capture epoch for test reproducibility
        QpcTimestamp.SetCaptureEpoch(0, DateTime.UtcNow);
    }

    public void Dispose() { }

    [Fact]
    public void Attribute_NoEtw_ReturnsEmptyWithNoAttribution()
    {
        var attributor = new CulpritAttributor();
        var spikes = MakeSpikes(1000, 2000, 3000);

        var result = attributor.Attribute(spikes, [], hasEtw: false, hasDpc: false, eventsLost: 0);

        Assert.False(result.HasAttribution);
        Assert.False(result.HasDpcAttribution);
        Assert.Empty(result.TopContextSwitchProcesses);
        Assert.Empty(result.TopDpcDrivers);
        Assert.Empty(result.TopDiskIoProcesses);
        Assert.Equal(0f, result.InterferenceCorrelation);
    }

    [Fact]
    public void Attribute_EmptyEvents_ReturnsEmptyWithNoAttribution()
    {
        var attributor = new CulpritAttributor();
        var spikes = MakeSpikes(1000, 2000);

        var result = attributor.Attribute(spikes, [], hasEtw: true, hasDpc: true, eventsLost: 0);

        Assert.False(result.HasAttribution);
    }

    [Fact]
    public void CorrelateContextSwitches_SingleProcess_CorrectPercentage()
    {
        var spikes = MakeSpikes(1000, 2000);
        var events = new List<ContextSwitchEvent>
        {
            MakeContextSwitch(990, 100, 200, "MsMpEng.exe"),
            MakeContextSwitch(1010, 100, 200, "MsMpEng.exe"),
            MakeContextSwitch(2010, 100, 200, "MsMpEng.exe"),
        };

        var result = CulpritAttributor.CorrelateContextSwitches(spikes, events);

        Assert.Single(result);
        Assert.Equal("MsMpEng.exe", result[0].ProcessName);
        Assert.Equal(3, result[0].ContextSwitchCount);
        Assert.Equal(100f, result[0].PercentOfTotal);
        Assert.Equal(1.0f, result[0].CorrelationWithStutter); // Present in 2/2 spikes
    }

    [Fact]
    public void CorrelateContextSwitches_MultipleProcesses_RankedByCount()
    {
        var spikes = MakeSpikes(1000, 2000, 3000);
        var events = new List<ContextSwitchEvent>
        {
            MakeContextSwitch(1010, 100, 200, "MsMpEng.exe"),
            MakeContextSwitch(1020, 100, 200, "MsMpEng.exe"),
            MakeContextSwitch(1030, 100, 200, "MsMpEng.exe"),
            MakeContextSwitch(2010, 100, 300, "SearchIndexer.exe"),
            MakeContextSwitch(3010, 100, 200, "MsMpEng.exe"),
            MakeContextSwitch(3020, 100, 400, "dwm.exe"),
        };

        var result = CulpritAttributor.CorrelateContextSwitches(spikes, events);

        Assert.True(result.Count >= 2);
        Assert.Equal("MsMpEng.exe", result[0].ProcessName);
        Assert.Equal(4, result[0].ContextSwitchCount);
        Assert.True(result[0].PercentOfTotal > result[1].PercentOfTotal);
    }

    [Fact]
    public void CorrelateContextSwitches_OutsideWindow_Excluded()
    {
        var spikes = MakeSpikes(1000);
        var events = new List<ContextSwitchEvent>
        {
            // Outside ±50ms window
            MakeContextSwitch(900, 100, 200, "MsMpEng.exe"),
            MakeContextSwitch(1100, 100, 200, "MsMpEng.exe"),
            // Inside window
            MakeContextSwitch(1020, 100, 300, "dwm.exe"),
        };

        var result = CulpritAttributor.CorrelateContextSwitches(spikes, events);

        Assert.Single(result);
        Assert.Equal("dwm.exe", result[0].ProcessName);
    }

    [Fact]
    public void CorrelateDpcEvents_DriversRankedByTime()
    {
        var spikes = MakeSpikes(1000, 2000);
        var events = new List<DpcEvent>
        {
            MakeDpc(800, "ndis.sys", 500),
            MakeDpc(900, "ndis.sys", 800),
            MakeDpc(1100, "ndis.sys", 300),
            MakeDpc(1200, "storport.sys", 200),
            MakeDpc(1800, "ndis.sys", 400),
        };

        var result = CulpritAttributor.CorrelateDpcEvents(spikes, events);

        Assert.True(result.Count >= 1);
        Assert.Equal("ndis.sys", result[0].DriverModule);
        Assert.True(result[0].TotalDpcTimeMs > 0);
        Assert.True(result[0].PercentOfDpcTime > 50f);
    }

    [Fact]
    public void CorrelateDiskIo_ProcessesRankedByBytes()
    {
        var spikes = MakeSpikes(5000);
        var events = new List<DiskIoEvent>
        {
            MakeDiskIo(4000, 200, "MsMpEng.exe", 1048576),
            MakeDiskIo(5500, 200, "MsMpEng.exe", 524288),
            MakeDiskIo(6000, 300, "OneDrive.exe", 4194304),
        };

        var result = CulpritAttributor.CorrelateDiskIo(spikes, events);

        Assert.True(result.Count >= 1);
        // OneDrive has the most bytes
        Assert.Equal("OneDrive.exe", result[0].ProcessName);
    }

    [Fact]
    public void CorrelateDiskIo_OutsideWindow_Excluded()
    {
        var spikes = MakeSpikes(5000);
        var events = new List<DiskIoEvent>
        {
            // Outside ±2s window
            MakeDiskIo(2000, 200, "MsMpEng.exe", 1048576),
            MakeDiskIo(8000, 200, "MsMpEng.exe", 1048576),
            // Inside window
            MakeDiskIo(5500, 300, "dwm.exe", 512),
        };

        var result = CulpritAttributor.CorrelateDiskIo(spikes, events);

        Assert.Single(result);
        Assert.Equal("dwm.exe", result[0].ProcessName);
    }

    [Fact]
    public void KnownProcess_DefenderHasDescriptionAndRemediation()
    {
        var spikes = MakeSpikes(1000);
        var events = new List<ContextSwitchEvent>
        {
            MakeContextSwitch(1010, 100, 200, "MsMpEng.exe"),
        };

        var result = CulpritAttributor.CorrelateContextSwitches(spikes, events);

        Assert.Single(result);
        Assert.Equal("Windows Defender real-time scanner", result[0].Description);
        Assert.Equal("Exclude game folder from Defender scans", result[0].Remediation);
    }

    [Fact]
    public void ProcessLifetime_TracksStartAndStop()
    {
        var lifetimeEvents = new List<ProcessLifetimeEvent>
        {
            new(QpcTimestamp.FromMilliseconds(1000), 200, "MsMpEng.exe", true),
            new(QpcTimestamp.FromMilliseconds(5000), 200, "MsMpEng.exe", false),
        };

        var result = CulpritAttributor.TrackProcessLifetimes(lifetimeEvents, []);

        Assert.Equal(2, result.Count);
        Assert.True(result[0].IsStart);
        Assert.False(result[1].IsStart);
        Assert.Equal("MsMpEng.exe", result[0].ProcessName);
    }

    [Fact]
    public void ProcessLifetime_CorrelatesWithStutterCluster()
    {
        // Two spikes close together = cluster
        var spikes = MakeSpikes(5000, 5200);
        var lifetimeEvents = new List<ProcessLifetimeEvent>
        {
            new(QpcTimestamp.FromMilliseconds(4900), 200, "MsMpEng.exe", true),
        };

        var result = CulpritAttributor.TrackProcessLifetimes(lifetimeEvents, spikes);

        Assert.Single(result);
        Assert.True(result[0].CorrelatesWithStutterCluster);
    }

    [Fact]
    public void ProcessLifetime_NoCluster_DoesNotCorrelate()
    {
        // Single spike — no cluster
        var spikes = MakeSpikes(5000);
        var lifetimeEvents = new List<ProcessLifetimeEvent>
        {
            new(QpcTimestamp.FromMilliseconds(4900), 200, "MsMpEng.exe", true),
        };

        var result = CulpritAttributor.TrackProcessLifetimes(lifetimeEvents, spikes);

        Assert.Single(result);
        Assert.False(result[0].CorrelatesWithStutterCluster);
    }

    [Fact]
    public void Attribute_FullIntegration_DefenderFixture()
    {
        var fixture = LoadFixture("defender_interference.json");
        var attributor = new CulpritAttributor();

        var spikes = fixture.StutterSpikes
            .Select(s => QpcTimestamp.FromMilliseconds(s.TimestampMs))
            .ToList();

        var events = new List<EtwEvent>();
        events.AddRange(fixture.ContextSwitches.Select(cs =>
            new ContextSwitchEvent(QpcTimestamp.FromMilliseconds(cs.TimestampMs),
                cs.OldProcessId, cs.NewProcessId, cs.NewProcessName)));
        events.AddRange(fixture.DiskIo.Select(dio =>
            new DiskIoEvent(QpcTimestamp.FromMilliseconds(dio.TimestampMs),
                dio.ProcessId, dio.ProcessName, dio.BytesTransferred)));
        events.AddRange(fixture.ProcessLifetime.Select(pl =>
            new ProcessLifetimeEvent(QpcTimestamp.FromMilliseconds(pl.TimestampMs),
                pl.ProcessId, pl.ProcessName, pl.IsStart)));

        var result = attributor.Attribute(spikes, events, hasEtw: true, hasDpc: false, eventsLost: 0);

        Assert.True(result.HasAttribution);
        Assert.False(result.HasDpcAttribution);
        Assert.NotEmpty(result.TopContextSwitchProcesses);
        Assert.Equal("MsMpEng.exe", result.TopContextSwitchProcesses[0].ProcessName);
        Assert.True(result.TopContextSwitchProcesses[0].PercentOfTotal > 50f);
    }

    [Fact]
    public void Attribute_FullIntegration_DpcStormFixture()
    {
        var fixture = LoadDpcFixture("dpc_storm.json");
        var attributor = new CulpritAttributor();

        var spikes = fixture.StutterSpikes
            .Select(s => QpcTimestamp.FromMilliseconds(s.TimestampMs))
            .ToList();

        var events = new List<EtwEvent>();
        events.AddRange(fixture.DpcEvents.Select(d =>
            new DpcEvent(QpcTimestamp.FromMilliseconds(d.TimestampMs), d.DriverModule, d.DurationUs)));
        events.AddRange(fixture.ContextSwitches.Select(cs =>
            new ContextSwitchEvent(QpcTimestamp.FromMilliseconds(cs.TimestampMs),
                cs.OldProcessId, cs.NewProcessId, cs.NewProcessName)));

        var result = attributor.Attribute(spikes, events, hasEtw: true, hasDpc: true, eventsLost: 0);

        Assert.True(result.HasAttribution);
        Assert.True(result.HasDpcAttribution);
        Assert.NotEmpty(result.TopDpcDrivers);
        Assert.Equal("ndis.sys", result.TopDpcDrivers[0].DriverModule);
        Assert.True(result.TopDpcDrivers[0].PercentOfDpcTime > 80f);
    }

    [Fact]
    public void CorrelateContextSwitches_NoSpikes_ReturnsEmpty()
    {
        List<QpcTimestamp> spikes = [];
        var events = new List<ContextSwitchEvent>
        {
            MakeContextSwitch(1000, 100, 200, "MsMpEng.exe"),
        };

        var result = CulpritAttributor.CorrelateContextSwitches(spikes, events);
        Assert.Empty(result);
    }

    [Fact]
    public void CorrelateContextSwitches_NoEvents_ReturnsEmpty()
    {
        var spikes = MakeSpikes(1000);
        List<ContextSwitchEvent> events = [];

        var result = CulpritAttributor.CorrelateContextSwitches(spikes, events);
        Assert.Empty(result);
    }

    // --- Helpers ---

    private static List<QpcTimestamp> MakeSpikes(params double[] timestampsMs)
        => timestampsMs.Select(QpcTimestamp.FromMilliseconds).ToList();

    private static ContextSwitchEvent MakeContextSwitch(double ms, int oldPid, int newPid, string name)
        => new(QpcTimestamp.FromMilliseconds(ms), oldPid, newPid, name);

    private static DpcEvent MakeDpc(double ms, string driver, double durationUs)
        => new(QpcTimestamp.FromMilliseconds(ms), driver, durationUs);

    private static DiskIoEvent MakeDiskIo(double ms, int pid, string name, long bytes)
        => new(QpcTimestamp.FromMilliseconds(ms), pid, name, bytes);

    // --- Fixture deserialization ---

    private record SpikeEntry(double TimestampMs);
    private record ContextSwitchEntry(double TimestampMs, int OldProcessId, int NewProcessId, string NewProcessName);
    private record DiskIoEntry(double TimestampMs, int ProcessId, string ProcessName, long BytesTransferred);
    private record ProcessLifetimeFixtureEntry(double TimestampMs, int ProcessId, string ProcessName, bool IsStart);
    private record DpcEntry(double TimestampMs, string DriverModule, double DurationUs);

    private record DefenderFixture(
        List<SpikeEntry> StutterSpikes,
        List<ContextSwitchEntry> ContextSwitches,
        List<DiskIoEntry> DiskIo,
        List<ProcessLifetimeFixtureEntry> ProcessLifetime);

    private record DpcStormFixture(
        List<SpikeEntry> StutterSpikes,
        List<DpcEntry> DpcEvents,
        List<ContextSwitchEntry> ContextSwitches);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static DefenderFixture LoadFixture(string name)
    {
        var path = Path.Combine("Fixtures", name);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<DefenderFixture>(json, JsonOptions)!;
    }

    private static DpcStormFixture LoadDpcFixture(string name)
    {
        var path = Path.Combine("Fixtures", name);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<DpcStormFixture>(json, JsonOptions)!;
    }
}
