using SysAnalyzer.Capture;
using SysAnalyzer.Capture.Providers;

namespace SysAnalyzer.Tests.Unit;

public class PollLoopTests
{
    private class FakePolledProvider : IPolledProvider
    {
        public string Name => "FakePolled";
        public ProviderTier RequiredTier => ProviderTier.Tier1;
        public ProviderHealth Health { get; } = new(ProviderStatus.Active, null, 1, 1, 0);
        public int PollCount { get; private set; }

        public Task<ProviderHealth> InitAsync() => Task.FromResult(Health);

        public MetricBatch Poll(long qpcTimestamp)
        {
            PollCount++;
            var batch = MetricBatch.Create();
            batch.TotalCpuPercent = 50.0;
            return batch;
        }

        public void Dispose() { }
    }

    [Fact]
    public async Task RunAsync_PollsProviders()
    {
        var provider = new FakePolledProvider();
        var session = new CaptureSession(
            new List<IProvider> { provider },
            pollLoopFunc: null,
            analyzeFunc: _ => Task.CompletedTask,
            emitFunc: _ => Task.CompletedTask);

        await session.InitAsync();

        // Manually create and run a PollLoop with short interval
        var pollLoop = new PollLoop(session, 50);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await pollLoop.RunAsync(cts.Token);

        Assert.True(provider.PollCount > 0);
        Assert.True(session.SampleCount > 0);
    }

    [Fact]
    public async Task RunAsync_OverheadTrackerSamples()
    {
        var provider = new FakePolledProvider();
        var session = new CaptureSession(
            new List<IProvider> { provider },
            pollLoopFunc: null,
            analyzeFunc: _ => Task.CompletedTask,
            emitFunc: _ => Task.CompletedTask);

        await session.InitAsync();

        var pollLoop = new PollLoop(session, 50);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await pollLoop.RunAsync(cts.Token);

        var overhead = pollLoop.OverheadTracker.Finish();
        Assert.True(overhead.PeakWorkingSetBytes > 0);
    }

    [Fact]
    public async Task RunAsync_CancellationStopsLoop()
    {
        var provider = new FakePolledProvider();
        var session = new CaptureSession(
            new List<IProvider> { provider },
            pollLoopFunc: null,
            analyzeFunc: _ => Task.CompletedTask,
            emitFunc: _ => Task.CompletedTask);

        await session.InitAsync();

        var pollLoop = new PollLoop(session, 1000);
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // cancel immediately

        await pollLoop.RunAsync(cts.Token);

        // Should have exited quickly without polling
        Assert.True(provider.PollCount <= 1);
    }
}
