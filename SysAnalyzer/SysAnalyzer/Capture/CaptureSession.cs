using SysAnalyzer.Capture.Providers;

namespace SysAnalyzer.Capture;

public enum CaptureState
{
    Created,
    Probing,
    PartialProbe,
    Capturing,
    Stopping,
    Analyzing,
    Emitting,
    Complete
}

public class CaptureSession : IDisposable
{
    private readonly List<IProvider> _providers;
    private readonly SensorHealthMatrix _healthMatrix = new();
    private CaptureState _state = CaptureState.Created;
    private readonly object _stateLock = new();
    private CancellationTokenSource? _captureCts;
    private readonly List<SensorSnapshot> _snapshots = new();
    private Action<CaptureState>? _onStateChanged;

    public CaptureState State
    {
        get { lock (_stateLock) return _state; }
    }

    public SensorHealthMatrix HealthMatrix => _healthMatrix;
    public IReadOnlyList<SensorSnapshot> Snapshots => _snapshots;
    public int SampleCount => _snapshots.Count;

    public event Action<CaptureState>? StateChanged
    {
        add => _onStateChanged += value;
        remove => _onStateChanged -= value;
    }

    // Injected dependencies for poll loop, analysis, and emission
    private readonly Func<CaptureSession, CancellationToken, Task>? _pollLoopFunc;
    private readonly Func<CaptureSession, Task>? _analyzeFunc;
    private readonly Func<CaptureSession, Task>? _emitFunc;

    public CaptureSession(
        List<IProvider> providers,
        Func<CaptureSession, CancellationToken, Task>? pollLoopFunc = null,
        Func<CaptureSession, Task>? analyzeFunc = null,
        Func<CaptureSession, Task>? emitFunc = null)
    {
        _providers = providers;
        _pollLoopFunc = pollLoopFunc;
        _analyzeFunc = analyzeFunc;
        _emitFunc = emitFunc;
    }

    public void AddSnapshot(SensorSnapshot snapshot) => _snapshots.Add(snapshot);

    public async Task InitAsync()
    {
        TransitionTo(CaptureState.Probing);

        bool anyFailed = false;
        bool allFailed = true;

        foreach (var provider in _providers)
        {
            try
            {
                var health = await provider.InitAsync();
                _healthMatrix.Register(provider.Name, health);

                if (health.Status == ProviderStatus.Failed)
                    anyFailed = true;
                else
                    allFailed = false;
            }
            catch (Exception ex)
            {
                _healthMatrix.Register(provider.Name, new ProviderHealth(
                    ProviderStatus.Failed, ex.Message, 0, 0, 0));
                anyFailed = true;
            }
        }

        if (_providers.Count == 0)
            allFailed = true;

        if (allFailed)
        {
            // All providers failed — still allow transition to Capturing for minimal report
            TransitionTo(CaptureState.PartialProbe);
        }
        else if (anyFailed)
        {
            TransitionTo(CaptureState.PartialProbe);
        }
        // else stays in Probing — Start() will transition to Capturing
    }

    public async Task StartAsync(CancellationToken externalCt = default)
    {
        var currentState = State;
        if (currentState != CaptureState.Probing && currentState != CaptureState.PartialProbe)
            throw new InvalidOperationException($"Cannot start capture from state {currentState}");

        _captureCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        TransitionTo(CaptureState.Capturing);

        if (_pollLoopFunc != null)
        {
            await _pollLoopFunc(this, _captureCts.Token);
        }
    }

    public void RequestStop()
    {
        lock (_stateLock)
        {
            if (_state == CaptureState.Capturing)
            {
                _captureCts?.Cancel();
            }
            // During Analyzing/Emitting, ignore stop requests — let them finish
        }
    }

    public async Task StopAndFinishAsync()
    {
        RequestStop();
        await FinishAsync();
    }

    public async Task FinishAsync()
    {
        TransitionTo(CaptureState.Stopping);

        // Stop event stream providers
        foreach (var provider in _providers.OfType<IEventStreamProvider>())
        {
            try { await provider.StopAsync(); }
            catch { /* log but don't fail */ }
        }

        TransitionTo(CaptureState.Analyzing);
        if (_analyzeFunc != null)
            await _analyzeFunc(this);

        TransitionTo(CaptureState.Emitting);
        if (_emitFunc != null)
            await _emitFunc(this);

        TransitionTo(CaptureState.Complete);
    }

    private void TransitionTo(CaptureState newState)
    {
        lock (_stateLock)
        {
            ValidateTransition(_state, newState);
            _state = newState;
        }
        _onStateChanged?.Invoke(newState);
    }

    private static void ValidateTransition(CaptureState from, CaptureState to)
    {
        bool valid = (from, to) switch
        {
            (CaptureState.Created, CaptureState.Probing) => true,
            (CaptureState.Probing, CaptureState.PartialProbe) => true,
            (CaptureState.Probing, CaptureState.Capturing) => true,
            (CaptureState.PartialProbe, CaptureState.Capturing) => true,
            (CaptureState.Capturing, CaptureState.Stopping) => true,
            (CaptureState.Stopping, CaptureState.Analyzing) => true,
            (CaptureState.Analyzing, CaptureState.Emitting) => true,
            (CaptureState.Emitting, CaptureState.Complete) => true,
            _ => false
        };

        if (!valid)
            throw new InvalidOperationException($"Invalid state transition: {from} → {to}");
    }

    public IReadOnlyList<IPolledProvider> PolledProviders =>
        _providers.OfType<IPolledProvider>()
                  .Where(p => p.Health.Status != ProviderStatus.Failed)
                  .ToList();

    public IReadOnlyList<ISnapshotProvider> SnapshotProviders =>
        _providers.OfType<ISnapshotProvider>()
                  .Where(p => p.Health.Status != ProviderStatus.Failed)
                  .ToList();

    public void Dispose()
    {
        _captureCts?.Dispose();
        foreach (var provider in _providers)
        {
            try { provider.Dispose(); }
            catch { }
        }
    }
}
