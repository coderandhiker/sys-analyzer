using SysAnalyzer.Capture;
using SysAnalyzer.Capture.Providers;

namespace SysAnalyzer.Cli;

public sealed class LiveDisplay : IDisposable
{
    private readonly CaptureSession _session;
    private readonly CancellationToken _ct;
    private Task? _displayTask;
    private readonly DateTime _captureStart;

    public LiveDisplay(CaptureSession session, CancellationToken ct)
    {
        _session = session;
        _ct = ct;
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

        Console.SetCursorPosition(0, Console.CursorTop >= 8 ? Console.CursorTop - 8 : 0);

        // Header
        WriteLineFixed($"  SysAnalyzer | Elapsed: {elapsed:hh\\:mm\\:ss} | Samples: {_session.SampleCount}");
        WriteLineFixed($"  Tier: {_session.HealthMatrix.OverallTier} | Providers: {ActiveProviderCount()}/{_session.HealthMatrix.Providers.Count}");
        WriteLineFixed("");

        if (latest != null)
        {
            WriteBar("  CPU", latest.TotalCpuPercent, 100);
            WriteBar("  RAM", latest.MemoryUtilizationPercent, 100);
            WriteBar("  GPU", latest.GpuUtilizationPercent ?? 0, 100, latest.GpuUtilizationPercent.HasValue);
            WriteBar("  DSK", latest.DiskActiveTimePercent, 100);
        }
        else
        {
            WriteLineFixed("  Waiting for first sample...");
            WriteLineFixed("");
            WriteLineFixed("");
            WriteLineFixed("");
        }

        WriteLineFixed("");
        WriteLineFixed("  Press Q or Esc to stop capture");
    }

    private void WriteBar(string label, double value, double max, bool available = true)
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
            WriteLineFixed($"  {label} [{bar}] {value,5:F1}%");
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
