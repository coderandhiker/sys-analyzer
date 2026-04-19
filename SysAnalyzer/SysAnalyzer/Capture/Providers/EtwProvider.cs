using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;

namespace SysAnalyzer.Capture.Providers;

/// <summary>
/// Real-time ETW trace session that captures kernel events for culprit attribution.
/// Implements IEventStreamProvider with bounded ring buffer and graceful degradation.
/// </summary>
public sealed class EtwProvider : IEventStreamProvider
{
    public string Name => "ETW";
    public ProviderTier RequiredTier => ProviderTier.Tier2;
    public ProviderHealth Health { get; private set; } = new(ProviderStatus.Active, null, 0, 0, 0);

    private readonly Channel<EtwEvent> _channel;
    private TraceEventSession? _session;
    private ETWTraceEventSource? _source;
    private Task? _processingTask;
    private CancellationTokenSource? _cts;
    private long _captureStartQpc;
    private long _sessionStartQpc;
    private int _eventsLost;
    private int _channelDropped;
    private bool _disposed;
    private string? _sessionName;

    // Provider availability tracking
    private bool _hasContextSwitchEvents;
    private bool _hasDpcEvents;
    private bool _hasDiskEvents;
    private bool _hasProcessEvents;

    private const int ChannelCapacity = 16384;

    public int EventsLost => _eventsLost + _channelDropped;
    public bool HasDpcAttribution => _hasDpcEvents;
    public bool HasContextSwitchAttribution => _hasContextSwitchEvents;
    public bool HasDiskAttribution => _hasDiskEvents;
    public bool HasProcessAttribution => _hasProcessEvents;

    public EtwProvider()
    {
        _channel = Channel.CreateBounded<EtwEvent>(new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        });
    }

    public Task<ProviderHealth> InitAsync()
    {
        // Check if ETW sessions can be created (requires admin)
        if (!TraceEventSession.IsElevated() ?? true)
        {
            Health = new ProviderHealth(ProviderStatus.Unavailable,
                "ETW requires elevated (admin) privileges", 0, 4, 0);
            return Task.FromResult(Health);
        }

        // Try to create the session
        _sessionName = $"SysAnalyzer-{Environment.ProcessId}";
        try
        {
            _session = CreateSession(_sessionName);
        }
        catch
        {
            // Retry with random suffix on name collision
            try
            {
                _sessionName = $"SysAnalyzer-{Environment.ProcessId}-{Random.Shared.Next(1000, 9999)}";
                _session = CreateSession(_sessionName);
            }
            catch (Exception retryEx)
            {
                Health = new ProviderHealth(ProviderStatus.Unavailable,
                    $"ETW session creation failed: {retryEx.Message}", 0, 4, 0);
                return Task.FromResult(Health);
            }
        }

        // Enable kernel providers
        int providersEnabled = 0;
        try
        {
            _session.EnableKernelProvider(
                KernelTraceEventParser.Keywords.ContextSwitch |
                KernelTraceEventParser.Keywords.Process |
                KernelTraceEventParser.Keywords.DiskIO |
                KernelTraceEventParser.Keywords.DeferedProcedureCalls);

            _hasContextSwitchEvents = true;
            _hasDpcEvents = true;
            _hasDiskEvents = true;
            _hasProcessEvents = true;
            providersEnabled = 4;
        }
        catch
        {
            // Try enabling providers individually for partial availability
            providersEnabled = EnableProvidersIndividually();
        }

        if (providersEnabled == 0)
        {
            _session.Dispose();
            _session = null;
            Health = new ProviderHealth(ProviderStatus.Unavailable,
                "No ETW kernel providers could be enabled", 0, 4, 0);
            return Task.FromResult(Health);
        }

        _source = _session.Source;
        Health = new ProviderHealth(
            providersEnabled < 4 ? ProviderStatus.Degraded : ProviderStatus.Active,
            providersEnabled < 4 ? $"Only {providersEnabled}/4 ETW providers available" : null,
            providersEnabled, 4, 0);

        return Task.FromResult(Health);
    }

    public Task StartAsync(long captureStartQpc)
    {
        if (Health.Status == ProviderStatus.Unavailable || _session == null || _source == null)
            return Task.CompletedTask;

        _captureStartQpc = captureStartQpc;
        _sessionStartQpc = System.Diagnostics.Stopwatch.GetTimestamp();
        _cts = new CancellationTokenSource();

        // Wire up event callbacks
        RegisterEventCallbacks();

        // Start processing on a background thread
        _processingTask = Task.Run(() =>
        {
            try
            {
                _source.Process();
            }
            catch (Exception) when (_cts?.IsCancellationRequested == true)
            {
                // Expected during shutdown
            }
        });

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_session == null) return;

        _cts?.Cancel();

        try
        {
            _session.Stop();
        }
        catch
        {
            // Session may already be stopped
        }

        if (_processingTask != null)
        {
            try
            {
                await _processingTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                // Processing thread didn't stop in time — continue
            }
        }

        _channel.Writer.TryComplete();

        // Update health with final events-lost count
        int sessionLost = (int)_session.EventsLost;
        _eventsLost = sessionLost;

        var finalStatus = Health.Status;
        string? reason = Health.DegradationReason;
        if (EventsLost > 0 && finalStatus == ProviderStatus.Active)
        {
            finalStatus = ProviderStatus.Degraded;
            reason = $"ETW lost {EventsLost} events (session: {sessionLost}, buffer: {_channelDropped})";
        }

        Health = new ProviderHealth(finalStatus, reason, Health.MetricsAvailable, Health.MetricsExpected, EventsLost);
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

    private void RegisterEventCallbacks()
    {
        if (_source == null) return;

        var kernel = _source.Kernel;
        if (kernel == null) return;

        if (_hasContextSwitchEvents)
        {
            kernel.ThreadCSwitch += data =>
            {
                var ts = TimestampFromRelativeMSec(data.TimeStampRelativeMSec);
                var evt = new ContextSwitchEvent(ts, data.OldProcessID, data.NewProcessID, data.NewProcessName);
                TryWrite(evt);
            };
        }

        if (_hasDpcEvents)
        {
            kernel.PerfInfoDPC += data =>
            {
                var ts = TimestampFromRelativeMSec(data.TimeStampRelativeMSec);
                // DPC duration in microseconds — ETW provides elapsed time
                var evt = new DpcEvent(ts, data.ProviderName ?? "unknown", data.ElapsedTimeMSec * 1000.0);
                TryWrite(evt);
            };
        }

        if (_hasDiskEvents)
        {
            kernel.DiskIORead += data =>
            {
                var ts = TimestampFromRelativeMSec(data.TimeStampRelativeMSec);
                var evt = new DiskIoEvent(ts, data.ProcessID, data.ProcessName, (long)data.TransferSize);
                TryWrite(evt);
            };
            kernel.DiskIOWrite += data =>
            {
                var ts = TimestampFromRelativeMSec(data.TimeStampRelativeMSec);
                var evt = new DiskIoEvent(ts, data.ProcessID, data.ProcessName, (long)data.TransferSize);
                TryWrite(evt);
            };
        }

        if (_hasProcessEvents)
        {
            kernel.ProcessStart += data =>
            {
                var ts = TimestampFromRelativeMSec(data.TimeStampRelativeMSec);
                var evt = new ProcessLifetimeEvent(ts, data.ProcessID, data.ProcessName, true);
                TryWrite(evt);
            };
            kernel.ProcessStop += data =>
            {
                var ts = TimestampFromRelativeMSec(data.TimeStampRelativeMSec);
                var evt = new ProcessLifetimeEvent(ts, data.ProcessID, data.ProcessName, false);
                TryWrite(evt);
            };
        }
    }

    /// <summary>
    /// Convert ETW's TimeStampRelativeMSec to our canonical QpcTimestamp.
    /// TimeStampRelativeMSec is relative to session start — convert to QPC ticks relative to capture epoch.
    /// </summary>
    private QpcTimestamp TimestampFromRelativeMSec(double relativeMSec)
    {
        long ticks = (long)(relativeMSec / 1000.0 * QpcTimestamp.Frequency) + _sessionStartQpc - _captureStartQpc;
        return new QpcTimestamp(ticks);
    }

    private void TryWrite(EtwEvent evt)
    {
        if (!_channel.Writer.TryWrite(evt))
        {
            Interlocked.Increment(ref _channelDropped);
        }
    }

    private static TraceEventSession CreateSession(string sessionName)
    {
        return new TraceEventSession(sessionName)
        {
            BufferSizeMB = 64,
            CpuSampleIntervalMSec = 0 // disable CPU sampling
        };
    }

    private int EnableProvidersIndividually()
    {
        int count = 0;
        if (_session == null) return count;

        try
        {
            _session.EnableKernelProvider(KernelTraceEventParser.Keywords.ContextSwitch | KernelTraceEventParser.Keywords.Process);
            _hasContextSwitchEvents = true;
            _hasProcessEvents = true;
            count += 2;
        }
        catch { /* Provider not available */ }

        // DPC and Disk require separate sessions in some configurations
        // Note: kernel providers must be enabled in a single call for NT Kernel Logger
        // If the combined call above succeeded, we get all; if individual fails, we skip
        try
        {
            // DPC and DiskIO are part of the same kernel session
            // Re-enable with additional flags if possible
            if (_hasContextSwitchEvents)
            {
                _session.EnableKernelProvider(
                    KernelTraceEventParser.Keywords.ContextSwitch |
                    KernelTraceEventParser.Keywords.Process |
                    KernelTraceEventParser.Keywords.DeferedProcedureCalls);
                _hasDpcEvents = true;
                count++;
            }
        }
        catch { /* DPC not available */ }

        try
        {
            if (_hasContextSwitchEvents)
            {
                _session.EnableKernelProvider(
                    KernelTraceEventParser.Keywords.ContextSwitch |
                    KernelTraceEventParser.Keywords.Process |
                    (_hasDpcEvents ? KernelTraceEventParser.Keywords.DeferedProcedureCalls : 0) |
                    KernelTraceEventParser.Keywords.DiskIO);
                _hasDiskEvents = true;
                count++;
            }
        }
        catch { /* DiskIO not available */ }

        return count;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _channel.Writer.TryComplete();

        try { _session?.Dispose(); }
        catch { /* best-effort cleanup */ }

        _cts?.Dispose();
    }
}
