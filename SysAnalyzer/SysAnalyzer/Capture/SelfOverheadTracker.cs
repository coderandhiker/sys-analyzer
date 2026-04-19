using System.Diagnostics;

namespace SysAnalyzer.Capture;

public sealed class SelfOverheadTracker
{
    private long _startAllocatedBytes;
    private int _startGen0;
    private int _startGen1;
    private int _startGen2;
    private TimeSpan _startGcPause;
    private long _startProcessTime;
    private long _startTimestamp;
    private readonly List<double> _cpuSamples = new();
    private TimeSpan _lastProcessorTime;
    private long _lastSampleTimestamp;

    public void Start()
    {
        _startAllocatedBytes = GC.GetTotalAllocatedBytes(precise: false);
        _startGen0 = GC.CollectionCount(0);
        _startGen1 = GC.CollectionCount(1);
        _startGen2 = GC.CollectionCount(2);
        _startGcPause = GC.GetTotalPauseDuration();
        _startTimestamp = Stopwatch.GetTimestamp();

        using var proc = Process.GetCurrentProcess();
        _startProcessTime = proc.TotalProcessorTime.Ticks;
        _lastProcessorTime = proc.TotalProcessorTime;
        _lastSampleTimestamp = _startTimestamp;
    }

    public void Sample()
    {
        try
        {
            var now = Stopwatch.GetTimestamp();
            using var proc = Process.GetCurrentProcess();
            var currentProcessorTime = proc.TotalProcessorTime;

            var elapsedSeconds = (double)(now - _lastSampleTimestamp) / Stopwatch.Frequency;
            if (elapsedSeconds > 0)
            {
                var cpuSeconds = (currentProcessorTime - _lastProcessorTime).TotalSeconds;
                var cpuPercent = (cpuSeconds / elapsedSeconds) * 100.0 / Environment.ProcessorCount;
                _cpuSamples.Add(cpuPercent);
            }

            _lastProcessorTime = currentProcessorTime;
            _lastSampleTimestamp = now;
        }
        catch { /* self-overhead tracking must never disrupt capture */ }
    }

    public Analysis.Models.SelfOverhead Finish()
    {
        var endTimestamp = Stopwatch.GetTimestamp();
        var gcCollections = (GC.CollectionCount(0) - _startGen0)
                          + (GC.CollectionCount(1) - _startGen1)
                          + (GC.CollectionCount(2) - _startGen2);
        var gcPauseMs = (GC.GetTotalPauseDuration() - _startGcPause).TotalMilliseconds;

        using var proc = Process.GetCurrentProcess();
        var peakWorkingSet = proc.PeakWorkingSet64;

        var avgCpu = _cpuSamples.Count > 0 ? _cpuSamples.Average() : 0;

        return new Analysis.Models.SelfOverhead(
            AvgCpuPercent: Math.Round(avgCpu, 2),
            PeakWorkingSetBytes: peakWorkingSet,
            GcCollections: gcCollections,
            GcPauseTimeMs: Math.Round(gcPauseMs, 2),
            EtwEventsLost: 0 // populated by ETW provider in Phase D
        );
    }
}
