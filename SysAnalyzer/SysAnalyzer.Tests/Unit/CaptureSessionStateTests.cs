using SysAnalyzer.Capture;
using SysAnalyzer.Capture.Providers;

namespace SysAnalyzer.Tests.Unit;

public class CaptureSessionStateTests
{
    private class FakeProvider : IPolledProvider
    {
        public string Name { get; }
        public ProviderTier RequiredTier => ProviderTier.Tier1;
        public ProviderHealth Health { get; private set; } = new(ProviderStatus.Active, null, 5, 5, 0);
        public bool ShouldFail { get; set; }
        public bool InitCalled { get; private set; }

        public FakeProvider(string name, bool shouldFail = false)
        {
            Name = name;
            ShouldFail = shouldFail;
        }

        public Task<ProviderHealth> InitAsync()
        {
            InitCalled = true;
            if (ShouldFail)
            {
                Health = new ProviderHealth(ProviderStatus.Failed, "Simulated failure", 0, 5, 0);
                return Task.FromResult(Health);
            }
            return Task.FromResult(Health);
        }

        public MetricBatch Poll(long qpcTimestamp) => MetricBatch.Create();
        public void Dispose() { }
    }

    private class FakeEventStreamProvider : IEventStreamProvider
    {
        public string Name => "FakeEventStream";
        public ProviderTier RequiredTier => ProviderTier.Tier1;
        public ProviderHealth Health { get; private set; } = new(ProviderStatus.Active, null, 3, 3, 0);
        public bool StopCalled { get; private set; }

        public Task<ProviderHealth> InitAsync() => Task.FromResult(Health);
        public Task StartAsync(long captureStartQpc) => Task.CompletedTask;

        public Task StopAsync()
        {
            StopCalled = true;
            return Task.CompletedTask;
        }

        public IAsyncEnumerable<TimestampedEvent> Events => throw new NotImplementedException();
        public void Dispose() { }
    }

    [Fact]
    public async Task HappyPath_AllStatesTraversed()
    {
        var states = new List<CaptureState>();
        var provider = new FakeProvider("Test");
        var session = new CaptureSession(
            new List<IProvider> { provider },
            pollLoopFunc: (s, ct) => Task.CompletedTask,
            analyzeFunc: _ => Task.CompletedTask,
            emitFunc: _ => Task.CompletedTask);
        session.StateChanged += s => states.Add(s);

        await session.InitAsync();
        await session.StartAsync();
        await session.FinishAsync();

        Assert.Equal(CaptureState.Complete, session.State);
        var expected = new[]
        {
            CaptureState.Probing,
            CaptureState.Capturing,
            CaptureState.Stopping,
            CaptureState.Analyzing,
            CaptureState.Emitting,
            CaptureState.Complete
        };
        for (int i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], states[i]);
    }

    [Fact]
    public async Task ProviderFailsDuringProbe_PartialProbe_ContinuesInDegradedMode()
    {
        var good = new FakeProvider("Good");
        var bad = new FakeProvider("Bad", shouldFail: true);
        var session = new CaptureSession(
            new List<IProvider> { good, bad },
            pollLoopFunc: (s, ct) => Task.CompletedTask,
            analyzeFunc: _ => Task.CompletedTask,
            emitFunc: _ => Task.CompletedTask);

        await session.InitAsync();

        Assert.Equal(CaptureState.PartialProbe, session.State);
        Assert.Contains("Bad", session.HealthMatrix.FailedProviders);

        await session.StartAsync();
        await session.FinishAsync();

        Assert.Equal(CaptureState.Complete, session.State);
    }

    [Fact]
    public async Task CtrlC_DuringCapturing_StopsNormally()
    {
        var cts = new CancellationTokenSource();
        var captureStarted = new TaskCompletionSource();
        var session = new CaptureSession(
            new List<IProvider> { new FakeProvider("Test") },
            pollLoopFunc: async (s, ct) =>
            {
                captureStarted.SetResult();
                try { await Task.Delay(Timeout.Infinite, ct); }
                catch (OperationCanceledException) { }
            },
            analyzeFunc: _ => Task.CompletedTask,
            emitFunc: _ => Task.CompletedTask);

        await session.InitAsync();

        var captureTask = session.StartAsync(cts.Token);
        await captureStarted.Task;

        session.RequestStop();
        await captureTask;
        await session.FinishAsync();

        Assert.Equal(CaptureState.Complete, session.State);
    }

    [Fact]
    public async Task AllProvidersFail_MinimalReport()
    {
        bool emitCalled = false;
        var bad1 = new FakeProvider("Bad1", shouldFail: true);
        var bad2 = new FakeProvider("Bad2", shouldFail: true);
        var session = new CaptureSession(
            new List<IProvider> { bad1, bad2 },
            pollLoopFunc: (s, ct) => Task.CompletedTask,
            analyzeFunc: _ => Task.CompletedTask,
            emitFunc: _ => { emitCalled = true; return Task.CompletedTask; });

        await session.InitAsync();
        Assert.Equal(CaptureState.PartialProbe, session.State);
        Assert.Equal(2, session.HealthMatrix.FailedProviders.Count);

        await session.StartAsync();
        await session.FinishAsync();

        Assert.True(emitCalled);
        Assert.Equal(CaptureState.Complete, session.State);
    }

    [Fact]
    public async Task InvalidTransition_Start_FromCreated_Throws()
    {
        var session = new CaptureSession(new List<IProvider>());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartAsync());
        Assert.Contains("Cannot start capture from state Created", ex.Message);
    }

    [Fact]
    public async Task InvalidTransition_Finish_FromCreated_Throws()
    {
        var session = new CaptureSession(new List<IProvider>());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => session.FinishAsync());
        Assert.Contains("Invalid state transition", ex.Message);
    }

    [Fact]
    public async Task EventStreamProvider_StopCalled_DuringStop()
    {
        var esp = new FakeEventStreamProvider();
        var session = new CaptureSession(
            new List<IProvider> { esp },
            pollLoopFunc: (s, ct) => Task.CompletedTask,
            analyzeFunc: _ => Task.CompletedTask,
            emitFunc: _ => Task.CompletedTask);

        await session.InitAsync();
        await session.StartAsync();
        await session.FinishAsync();

        Assert.True(esp.StopCalled);
    }

    [Fact]
    public async Task NoProviders_TransitionsToPartialProbe()
    {
        var session = new CaptureSession(
            new List<IProvider>(),
            pollLoopFunc: (s, ct) => Task.CompletedTask,
            analyzeFunc: _ => Task.CompletedTask,
            emitFunc: _ => Task.CompletedTask);

        await session.InitAsync();
        Assert.Equal(CaptureState.PartialProbe, session.State);
    }

    [Fact]
    public async Task ProviderThrows_DuringInit_MarkedAsFailed()
    {
        var throwing = new ThrowingProvider();
        var session = new CaptureSession(
            new List<IProvider> { throwing },
            pollLoopFunc: (s, ct) => Task.CompletedTask,
            analyzeFunc: _ => Task.CompletedTask,
            emitFunc: _ => Task.CompletedTask);

        await session.InitAsync();

        Assert.Contains("ThrowingProvider", session.HealthMatrix.FailedProviders);
    }

    private class ThrowingProvider : IPolledProvider
    {
        public string Name => "ThrowingProvider";
        public ProviderTier RequiredTier => ProviderTier.Tier1;
        public ProviderHealth Health => new(ProviderStatus.Failed, "threw", 0, 0, 0);
        public Task<ProviderHealth> InitAsync() => throw new Exception("Init exploded");
        public MetricBatch Poll(long qpcTimestamp) => MetricBatch.Empty;
        public void Dispose() { }
    }
}
