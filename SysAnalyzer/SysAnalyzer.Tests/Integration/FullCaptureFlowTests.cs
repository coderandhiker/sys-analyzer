using SysAnalyzer.Capture;
using SysAnalyzer.Capture.Providers;

namespace SysAnalyzer.Tests.Integration;

public class FullCaptureFlowTests
{
    private class FakePolledProvider : IPolledProvider
    {
        public string Name => "FakePolled";
        public ProviderTier RequiredTier => ProviderTier.Tier1;
        public ProviderHealth Health { get; private set; } = new(ProviderStatus.Active, null, 1, 1, 0);

        public Task<ProviderHealth> InitAsync()
        {
            Health = new ProviderHealth(ProviderStatus.Active, null, 1, 1, 0);
            return Task.FromResult(Health);
        }

        public MetricBatch Poll(long qpcTimestamp)
        {
            var batch = MetricBatch.Create();
            batch.TotalCpuPercent = 42.0;
            batch.MemoryUtilizationPercent = 65.0;
            batch.DiskActiveTimePercent = 20.0;
            return batch;
        }

        public void Dispose() { }
    }

    [Fact]
    public async Task FullFlow_CaptureWithPollLoop_ProducesSamples()
    {
        var provider = new FakePolledProvider();
        var analyzeRan = false;
        var emitRan = false;

        var session = new CaptureSession(
            new List<IProvider> { provider },
            pollLoopFunc: async (s, ct) =>
            {
                var pollLoop = new PollLoop(s, 50);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromMilliseconds(250));
                await pollLoop.RunAsync(cts.Token);
            },
            analyzeFunc: s => { analyzeRan = true; return Task.CompletedTask; },
            emitFunc: s => { emitRan = true; return Task.CompletedTask; });

        await session.InitAsync();
        Assert.Equal(CaptureState.Probing, session.State);

        await session.StartAsync();
        await session.FinishAsync();

        Assert.Equal(CaptureState.Complete, session.State);
        Assert.True(session.SampleCount > 0);
        Assert.True(analyzeRan);
        Assert.True(emitRan);
    }

    [Fact]
    public async Task FullFlow_NoProviders_CompleteWithMinimalReport()
    {
        var emitRan = false;
        var session = new CaptureSession(
            new List<IProvider>(),
            pollLoopFunc: (s, ct) => Task.CompletedTask,
            analyzeFunc: _ => Task.CompletedTask,
            emitFunc: _ => { emitRan = true; return Task.CompletedTask; });

        await session.InitAsync();
        Assert.Equal(CaptureState.PartialProbe, session.State);

        await session.StartAsync();
        await session.FinishAsync();

        Assert.Equal(CaptureState.Complete, session.State);
        Assert.True(emitRan);
    }

    [Fact]
    public async Task FullFlow_RequestStop_GracefulShutdown()
    {
        var provider = new FakePolledProvider();
        var captureStarted = new TaskCompletionSource();

        var session = new CaptureSession(
            new List<IProvider> { provider },
            pollLoopFunc: async (s, ct) =>
            {
                captureStarted.SetResult();
                var pollLoop = new PollLoop(s, 50);
                await pollLoop.RunAsync(ct);
            },
            analyzeFunc: _ => Task.CompletedTask,
            emitFunc: _ => Task.CompletedTask);

        await session.InitAsync();
        var captureTask = session.StartAsync();
        await captureStarted.Task;

        session.RequestStop();
        await captureTask;
        await session.FinishAsync();

        Assert.Equal(CaptureState.Complete, session.State);
    }

    [Fact]
    public async Task FullFlow_Snapshots_ContainMetricValues()
    {
        var provider = new FakePolledProvider();

        var session = new CaptureSession(
            new List<IProvider> { provider },
            pollLoopFunc: async (s, ct) =>
            {
                var pollLoop = new PollLoop(s, 50);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromMilliseconds(200));
                await pollLoop.RunAsync(cts.Token);
            },
            analyzeFunc: _ => Task.CompletedTask,
            emitFunc: _ => Task.CompletedTask);

        await session.InitAsync();
        await session.StartAsync();
        await session.FinishAsync();

        Assert.True(session.Snapshots.Count > 0);
        var first = session.Snapshots[0];
        Assert.Equal(42.0, first.TotalCpuPercent);
        Assert.Equal(65.0, first.MemoryUtilizationPercent);
    }

    [Fact]
    public async Task FullFlow_StateTransitions_InCorrectOrder()
    {
        var states = new List<CaptureState>();
        var provider = new FakePolledProvider();

        var session = new CaptureSession(
            new List<IProvider> { provider },
            pollLoopFunc: (s, ct) => Task.CompletedTask,
            analyzeFunc: _ => Task.CompletedTask,
            emitFunc: _ => Task.CompletedTask);
        session.StateChanged += s => states.Add(s);

        await session.InitAsync();
        await session.StartAsync();
        await session.FinishAsync();

        Assert.Equal(CaptureState.Probing, states[0]);
        Assert.Equal(CaptureState.Capturing, states[1]);
        Assert.Equal(CaptureState.Stopping, states[2]);
        Assert.Equal(CaptureState.Analyzing, states[3]);
        Assert.Equal(CaptureState.Emitting, states[4]);
        Assert.Equal(CaptureState.Complete, states[5]);
    }
}
