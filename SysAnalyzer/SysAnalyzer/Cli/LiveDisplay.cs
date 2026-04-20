using SysAnalyzer.Capture;
using SysAnalyzer.Capture.Providers;

namespace SysAnalyzer.Cli;

public sealed class LiveDisplay : IDisposable
{
    private readonly CaptureSession _session;
    private readonly CancellationToken _ct;
    private readonly PresentMonProvider? _presentMon;
    private readonly List<FrameTimeSample>? _frameSamples;
    private Task? _displayTask;
    private readonly DateTime _captureStart;

    public LiveDisplay(CaptureSession session, CancellationToken ct,
        PresentMonProvider? presentMon = null, List<FrameTimeSample>? frameSamples = null)
    {
        _session = session;
        _ct = ct;
        _presentMon = presentMon;
        _frameSamples = frameSamples;
        _captureStart = DateTime.UtcNow;
    }

    public void Start()
    {
        Console.CursorVisible = false;
        _displayTask = Task.Run(RunDisplayLoop);
    }

    private async Task RunDisplayLoop()
    {
        try
        {
            while (!_ct.IsCancellationRequested)
            {
                try { Render(); }
                catch { /* display errors must never crash capture */ }

                await Task.Delay(1000, _ct);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            Console.CursorVisible = true;
        }
    }

    private void Render()
    {
        var snapshots = _session.Snapshots;
        var latest = snapshots.Count > 0 ? snapshots[^1] : null;
        var elapsed = DateTime.UtcNow - _captureStart;

        int totalLines = 12; // header(2) + blank + bars(4) + tier2(2) + fps(1) + blank + footer
        Console.SetCursorPosition(0, Console.CursorTop >= totalLines ? Console.CursorTop - totalLines : 0);

        // Header
        WriteLineFixed($"  SysAnalyzer | Elapsed: {elapsed:hh\\:mm\\:ss} | Samples: {_session.SampleCount}");
        WriteLineFixed($"  Tier: {_session.HealthMatrix.OverallTier} | Providers: {ActiveProviderCount()}/{_session.HealthMatrix.Providers.Count}");
        WriteLineFixed("");

        if (latest != null)
        {
            // Tier 1 bars (always shown)
            string cpuSuffix = FormatTier2Suffix(latest.CpuClockMhz, "GHz", 1000, latest.CpuTempC, "\u00B0C");
            string gpuSuffix = FormatTier2Suffix(latest.GpuClockMhz, "MHz", null, latest.GpuTempC, "\u00B0C");

            WriteBar("  CPU", latest.TotalCpuPercent, 100, suffix: cpuSuffix);
            WriteBar("  RAM", latest.MemoryUtilizationPercent, 100);
            WriteBar("  GPU", latest.GpuUtilizationPercent ?? 0, 100, latest.GpuUtilizationPercent.HasValue, suffix: gpuSuffix);
            WriteBar("  DSK", latest.DiskActiveTimePercent, 100);

            // Tier 2 power line
            if (latest.CpuPowerW is not null || latest.GpuPowerW is not null)
            {
                string powerLine = "  Power:";
                if (latest.CpuPowerW is not null)
                    powerLine += $" CPU {latest.CpuPowerW.Value:F0}W";
                if (latest.GpuPowerW is not null)
                    powerLine += $" + GPU {latest.GpuPowerW.Value:F0}W";
                if (latest.CpuPowerW is not null && latest.GpuPowerW is not null)
                    powerLine += $" = {latest.CpuPowerW.Value + latest.GpuPowerW.Value:F0}W";
                WriteLineFixed(powerLine);
            }
            else
            {
                WriteLineFixed("");
            }
        }
        else
        {
            WriteLineFixed("  Waiting for first sample...");
            WriteLineFixed("");
            WriteLineFixed("");
            WriteLineFixed("");
            WriteLineFixed("");
        }

        // FPS line
        WriteLineFixed(GetFpsLine());

        WriteLineFixed("");
        WriteLineFixed("  Press Q or Esc to stop capture");
    }

    private static string FormatTier2Suffix(double? clock, string clockUnit, double? divisor, double? temp, string tempUnit)
    {
        if (clock is null && temp is null) return "";

        var parts = new List<string>();
        if (clock is not null)
        {
            double displayClock = divisor.HasValue ? clock.Value / divisor.Value : clock.Value;
            string format = divisor.HasValue ? "F1" : "F0";
            parts.Add($"{displayClock.ToString(format)}{clockUnit}");
        }
        if (temp is not null)
            parts.Add($"{temp.Value:F0}{tempUnit}");

        return " " + string.Join(" / ", parts);
    }

    private string GetFpsLine()
    {
        if (_presentMon == null)
            return "";

        if (_presentMon.Health.Status == ProviderStatus.Unavailable)
            return "  FPS: N/A (PresentMon unavailable)";

        var appName = _presentMon.TrackedApplication;

        // Compute live FPS from recent samples (last 1 second)
        double avgFps = 0;
        double p1Fps = 0;
        int stutterCount = 0;

        if (_frameSamples != null)
        {
            FrameTimeSample[] recentSamples;
            lock (_frameSamples)
            {
                if (_frameSamples.Count == 0)
                    return appName != null
                        ? $"  App: {appName} | FPS: collecting..."
                        : "  FPS: waiting for frames...";

                // Last ~1 second of samples
                int lookback = Math.Min(_frameSamples.Count, 120);
                recentSamples = _frameSamples.Skip(_frameSamples.Count - lookback).ToArray();
                stutterCount = _frameSamples.Count(s => s.FrameTimeMs > 33.3); // rough stutter count
            }

            if (recentSamples.Length > 0)
            {
                double meanMs = recentSamples.Average(s => s.FrameTimeMs);
                avgFps = meanMs > 0 ? 1000.0 / meanMs : 0;

                var sorted = recentSamples.Select(s => s.FrameTimeMs).OrderBy(x => x).ToArray();
                double p99Ms = sorted[(int)(sorted.Length * 0.99)];
                p1Fps = p99Ms > 0 ? 1000.0 / p99Ms : 0;
            }
        }

        var appPart = appName != null ? $"App: {appName} | " : "";
        return $"  {appPart}FPS: {avgFps:F0} avg / {p1Fps:F0} P1 | Stutters: {stutterCount}";
    }

    private void WriteBar(string label, double value, double max, bool available = true, string suffix = "")
    {
        const int barWidth = 40;
        var pct = Math.Clamp(value / max, 0, 1);
        int filled = (int)(pct * barWidth);

        var bar = new string('#', filled) + new string('-', barWidth - filled);

        if (!available)
        {
            WriteLineFixed($"  {label} [{"N/A",40}]  ---");
        }
        else
        {
            WriteLineFixed($"  {label} [{bar}] {value,5:F1}%{suffix}");
        }
    }

    private static void WriteLineFixed(string text)
    {
        var width = Math.Max(Console.WindowWidth, 80);
        Console.Write(text.PadRight(width)[..width]);
        Console.WriteLine();
    }

    private int ActiveProviderCount()
    {
        return _session.HealthMatrix.Providers.Values
            .Count(h => h.Status != ProviderStatus.Failed);
    }

    public void Dispose()
    {
        Console.CursorVisible = true;
    }
}
