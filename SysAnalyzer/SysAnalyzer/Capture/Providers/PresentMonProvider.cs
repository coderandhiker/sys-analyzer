using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace SysAnalyzer.Capture.Providers;

/// <summary>
/// Launches PresentMon as a subprocess and streams frame-time data via CSV stdout.
/// Implements auto-detection, timestamp alignment, and graceful degradation.
/// </summary>
public sealed class PresentMonProvider : IEventStreamProvider
{
    public string Name => "PresentMon";
    public ProviderTier RequiredTier => ProviderTier.Tier1;
    public ProviderHealth Health { get; private set; } = new(ProviderStatus.Active, null, 0, 0, 0);

    private readonly string? _processFilter;
    private readonly IPresentMonProcessLauncher _launcher;
    private readonly Channel<FrameTimeSample> _channel;
    private Process? _process;
    private Task? _readerTask;
    private CancellationTokenSource? _cts;
    private long _qpcAtLaunch;
    private long _captureStartQpc;

    // Auto-detection state
    private readonly Dictionary<string, AppTrackingInfo> _appCounts = new();
    private string? _trackedApp;
    private bool _autoDetectionComplete;
    private readonly object _trackingLock = new();
    private TaskCompletionSource<bool>? _firstRowTcs;

    // Statistics
    private int _totalFramesParsed;
    private int _parseErrors;
    private bool _crashed;
    private string? _crashNote;
    private string? _borderlessNote;

    // CSV column indices
    private int _colApplication = -1;
    private int _colTimeInSeconds = -1;
    private int _colMsBetweenPresents = -1;
    private int _colMsCpuBusy = -1;
    private int _colMsGpuBusy = -1;
    private int _colDropped = -1;
    private int _colPresentMode = -1;
    private int _colAllowsTearing = -1;

    // Configuration
    private static readonly TimeSpan AutoDetectTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan AutoDetectWindow = TimeSpan.FromSeconds(5);
    private const int ChannelCapacity = 4096;

    public PresentMonProvider(string? processFilter = null, IPresentMonProcessLauncher? launcher = null)
    {
        _processFilter = processFilter;
        _launcher = launcher ?? new SystemProcessLauncher();
        _channel = Channel.CreateBounded<FrameTimeSample>(new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        });
    }

    public string? TrackedApplication
    {
        get { lock (_trackingLock) return _trackedApp; }
    }

    public bool Crashed => _crashed;
    public string? CrashNote => _crashNote;
    public string? BorderlessNote => _borderlessNote;
    public int TotalFramesParsed => _totalFramesParsed;
    public int ParseErrors => _parseErrors;

    public Task<ProviderHealth> InitAsync()
    {
        var binaryPath = _launcher.GetBinaryPath();
        if (!_launcher.BinaryExists(binaryPath))
        {
            Health = new ProviderHealth(ProviderStatus.Unavailable,
                "PresentMon.exe not found", 0, 0, 0);
            return Task.FromResult(Health);
        }

        Health = new ProviderHealth(ProviderStatus.Active, null, 1, 1, 0);
        return Task.FromResult(Health);
    }

    public Task StartAsync(long captureStartQpc)
    {
        if (Health.Status == ProviderStatus.Unavailable)
            return Task.CompletedTask;

        _captureStartQpc = captureStartQpc;
        _cts = new CancellationTokenSource();
        _firstRowTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var binaryPath = _launcher.GetBinaryPath();
        var arguments = BuildArguments();

        _qpcAtLaunch = Stopwatch.GetTimestamp();
        _process = _launcher.Start(binaryPath, arguments);

        if (_process == null || _process.HasExited)
        {
            Health = new ProviderHealth(ProviderStatus.Unavailable,
                "PresentMon process failed to start", 0, 0, 0);
            return Task.CompletedTask;
        }

        _readerTask = Task.Run(() => ReadLoop(_cts.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_process == null) return;

        _cts?.Cancel();

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        catch { /* best effort */ }

        if (_readerTask != null)
        {
            try { await _readerTask.WaitAsync(TimeSpan.FromSeconds(2)); }
            catch { /* timeout or cancellation — ok */ }
        }

        _channel.Writer.TryComplete();
    }

    public IAsyncEnumerable<TimestampedEvent> Events => ReadEventsAsync();

    private async IAsyncEnumerable<TimestampedEvent> ReadEventsAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var sample in _channel.Reader.ReadAllAsync(ct))
        {
            yield return sample;
        }
    }

    private async Task ReadLoop(CancellationToken ct)
    {
        try
        {
            var stdout = _process!.StandardOutput;
            var headerLine = await stdout.ReadLineAsync(ct);

            if (headerLine == null)
            {
                HandleNoData();
                return;
            }

            ParseHeader(headerLine);

            var autoDetectStart = Stopwatch.GetTimestamp();
            var autoDetectDeadline = autoDetectStart + Stopwatch.Frequency * (long)AutoDetectTimeout.TotalSeconds;
            var autoDetectWindowEnd = autoDetectStart + Stopwatch.Frequency * (long)AutoDetectWindow.TotalSeconds;
            bool firstRowSeen = false;

            while (!ct.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await stdout.ReadLineAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (line == null)
                {
                    // Process ended
                    if (_process.HasExited && _process.ExitCode != 0)
                    {
                        _crashed = true;
                        _crashNote = $"PresentMon crashed at {DateTime.UtcNow:HH:mm:ss} (exit code {_process.ExitCode})";
                        Health = new ProviderHealth(ProviderStatus.Degraded, _crashNote,
                            _totalFramesParsed > 0 ? 1 : 0, 1, 0);
                    }
                    break;
                }

                var sample = ParseLine(line);
                if (sample == null)
                {
                    _parseErrors++;
                    continue;
                }

                if (!firstRowSeen)
                {
                    firstRowSeen = true;
                    _firstRowTcs?.TrySetResult(true);
                    ValidateTimestampAlignment(sample);
                }

                Interlocked.Increment(ref _totalFramesParsed);

                // Auto-detection logic
                if (!_autoDetectionComplete && _processFilter == null)
                {
                    lock (_trackingLock)
                    {
                        if (!_appCounts.TryGetValue(sample.ApplicationName, out var info))
                        {
                            info = new AppTrackingInfo();
                            _appCounts[sample.ApplicationName] = info;
                        }
                        info.FrameCount++;
                        if (sample.PresentMode.Contains("Hardware: Independent Flip", StringComparison.OrdinalIgnoreCase))
                            info.FullscreenFrames++;
                    }

                    var now = Stopwatch.GetTimestamp();
                    if (now >= autoDetectWindowEnd)
                    {
                        FinalizeAutoDetection();
                    }
                }

                // Check for borderless windowed
                if (sample.PresentMode.StartsWith("Composed:", StringComparison.OrdinalIgnoreCase)
                    && _borderlessNote == null)
                {
                    _borderlessNote = "App running in composed mode. Frame times include DWM composition latency.";
                }

                // Filter: only emit samples for tracked app (or all if --process specified)
                bool emit = _processFilter != null
                    ? sample.ApplicationName.Equals(_processFilter, StringComparison.OrdinalIgnoreCase)
                    : _trackedApp == null || sample.ApplicationName.Equals(_trackedApp, StringComparison.OrdinalIgnoreCase);

                if (emit)
                {
                    _channel.Writer.TryWrite(sample);
                }
            }

            // Handle auto-detect timeout: no rows after 10s
            if (!firstRowSeen)
            {
                HandleNoData();
            }
            else if (!_autoDetectionComplete && _processFilter == null)
            {
                FinalizeAutoDetection();
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception)
        {
            _crashed = true;
            _crashNote = "PresentMon reader encountered an unexpected error";
            Health = new ProviderHealth(ProviderStatus.Degraded, _crashNote, 0, 1, 0);
        }
        finally
        {
            _channel.Writer.TryComplete();
            _firstRowTcs?.TrySetResult(false);
        }
    }

    private void HandleNoData()
    {
        Health = new ProviderHealth(ProviderStatus.Degraded,
            "No presenting application detected within timeout", 0, 1, 0);
        _firstRowTcs?.TrySetResult(false);
    }

    private void FinalizeAutoDetection()
    {
        lock (_trackingLock)
        {
            if (_autoDetectionComplete) return;
            _autoDetectionComplete = true;

            if (_appCounts.Count == 0)
            {
                _trackedApp = null;
                return;
            }

            // Prefer fullscreen apps
            var fullscreenApps = _appCounts
                .Where(kv => kv.Value.FullscreenFrames > kv.Value.FrameCount / 2)
                .ToList();

            if (fullscreenApps.Count > 0)
            {
                _trackedApp = fullscreenApps
                    .OrderByDescending(kv => kv.Value.FrameCount)
                    .First().Key;
            }
            else
            {
                _trackedApp = _appCounts
                    .OrderByDescending(kv => kv.Value.FrameCount)
                    .First().Key;
            }
        }
    }

    private void ParseHeader(string headerLine)
    {
        var columns = headerLine.Split(',');
        for (int i = 0; i < columns.Length; i++)
        {
            var col = columns[i].Trim();
            switch (col)
            {
                case "Application": _colApplication = i; break;
                case "TimeInSeconds": _colTimeInSeconds = i; break;
                case "msBetweenPresents": _colMsBetweenPresents = i; break;
                case "msCPUBusy": _colMsCpuBusy = i; break;
                case "msGPUBusy": _colMsGpuBusy = i; break;
                case "Dropped": _colDropped = i; break;
                case "PresentMode": _colPresentMode = i; break;
                case "AllowsTearing": _colAllowsTearing = i; break;
            }
        }
    }

    private FrameTimeSample? ParseLine(string line)
    {
        var parts = line.Split(',');

        try
        {
            string appName = _colApplication >= 0 && _colApplication < parts.Length
                ? parts[_colApplication].Trim() : "";

            double timeInSeconds = _colTimeInSeconds >= 0 && _colTimeInSeconds < parts.Length
                ? double.Parse(parts[_colTimeInSeconds].Trim()) : 0;

            double frameTimeMs = _colMsBetweenPresents >= 0 && _colMsBetweenPresents < parts.Length
                ? double.Parse(parts[_colMsBetweenPresents].Trim()) : 0;

            double cpuBusyMs = _colMsCpuBusy >= 0 && _colMsCpuBusy < parts.Length
                ? double.Parse(parts[_colMsCpuBusy].Trim()) : 0;

            double gpuBusyMs = _colMsGpuBusy >= 0 && _colMsGpuBusy < parts.Length
                ? double.Parse(parts[_colMsGpuBusy].Trim()) : 0;

            bool dropped = _colDropped >= 0 && _colDropped < parts.Length
                && parts[_colDropped].Trim() == "1";

            string presentMode = _colPresentMode >= 0 && _colPresentMode < parts.Length
                ? parts[_colPresentMode].Trim() : "";

            bool allowsTearing = _colAllowsTearing >= 0 && _colAllowsTearing < parts.Length
                && parts[_colAllowsTearing].Trim() == "1";

            // Convert timestamp: PresentMon TimeInSeconds → canonical QpcTimestamp
            var timestamp = QpcTimestamp.FromPresentMonSeconds(timeInSeconds, _qpcAtLaunch - _captureStartQpc);

            return new FrameTimeSample(
                timestamp, appName, frameTimeMs, cpuBusyMs, gpuBusyMs,
                dropped, presentMode, allowsTearing);
        }
        catch
        {
            return null;
        }
    }

    private void ValidateTimestampAlignment(FrameTimeSample firstSample)
    {
        // First sample's canonical timestamp should be close to elapsed time since capture start
        var elapsedSinceCaptureStart = Stopwatch.GetTimestamp() - _captureStartQpc;
        var elapsedMs = (double)elapsedSinceCaptureStart / Stopwatch.Frequency * 1000.0;
        var sampleMs = firstSample.Timestamp.ToMilliseconds();
        var drift = Math.Abs(elapsedMs - sampleMs);

        if (drift > 100.0) // ±100ms tolerance
        {
            var note = $"PresentMon timestamp alignment drift: {drift:F1}ms (tolerance ±100ms)";
            Health = new ProviderHealth(Health.Status, note,
                Health.MetricsAvailable, Health.MetricsExpected, Health.EventsLost);
        }
    }

    private string BuildArguments()
    {
        var args = "--output_stdout --no_top";
        if (!string.IsNullOrEmpty(_processFilter))
            args += $" --process_name {_processFilter}";
        return args;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _process?.Dispose();
    }

    private sealed class AppTrackingInfo
    {
        public int FrameCount;
        public int FullscreenFrames;
    }
}

/// <summary>
/// Abstraction for PresentMon process launch — enables testing with fake stdout.
/// </summary>
public interface IPresentMonProcessLauncher
{
    string GetBinaryPath();
    bool BinaryExists(string path);
    Process? Start(string path, string arguments);
}

/// <summary>
/// Real system implementation that launches PresentMon.exe.
/// </summary>
public sealed class SystemProcessLauncher : IPresentMonProcessLauncher
{
    public string GetBinaryPath()
    {
        var dir = Path.GetDirectoryName(Environment.ProcessPath)
            ?? AppContext.BaseDirectory;
        return Path.Combine(dir, "PresentMon.exe");
    }

    public bool BinaryExists(string path) => File.Exists(path);

    public Process? Start(string path, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = path,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        return Process.Start(psi);
    }
}
