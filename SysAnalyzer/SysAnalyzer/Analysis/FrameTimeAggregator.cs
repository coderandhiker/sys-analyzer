using SysAnalyzer.Analysis.Models;
using SysAnalyzer.Capture;

namespace SysAnalyzer.Analysis;

/// <summary>
/// Computes post-capture frame-time statistics from collected FrameTimeSamples.
/// </summary>
public static class FrameTimeAggregator
{
    /// <summary>
    /// Computes frame-time summary from a list of samples.
    /// </summary>
    /// <param name="samples">All frame-time samples from the capture.</param>
    /// <param name="trackedApp">The tracked application name.</param>
    /// <param name="stutterSpikeMultiplier">Multiplier over median to count as a stutter spike.</param>
    /// <param name="cpuBoundRatio">Ratio threshold: CpuBusyMs > GpuBusyMs * ratio → CPU-bound.</param>
    /// <param name="notes">Additional notes (borderless mode, crash info, etc.)</param>
    public static FrameTimeSummary? Compute(
        IReadOnlyList<FrameTimeSample> samples,
        string? trackedApp,
        double stutterSpikeMultiplier = 2.0,
        double cpuBoundRatio = 1.5,
        IReadOnlyList<string>? notes = null)
    {
        if (samples.Count == 0)
            return null;

        var frameTimes = samples.Select(s => s.FrameTimeMs).ToArray();
        Array.Sort(frameTimes);

        double mean = frameTimes.Average();
        double avgFps = mean > 0 ? 1000.0 / mean : 0;

        // Percentiles (interpolated)
        double p50 = InterpolatedPercentile(frameTimes, 0.50);
        double p95 = InterpolatedPercentile(frameTimes, 0.95);
        double p99 = InterpolatedPercentile(frameTimes, 0.99);
        double p999 = InterpolatedPercentile(frameTimes, 0.999);

        // P1 FPS = 1000 / P99 frame time (worst 1% of frames → P1 FPS)
        double p1Fps = p99 > 0 ? 1000.0 / p99 : 0;

        // Dropped frames
        int droppedCount = samples.Count(s => s.Dropped);
        double droppedPct = (double)droppedCount / samples.Count * 100.0;

        // CPU-bound vs GPU-bound
        int cpuBoundFrames = 0;
        int gpuBoundFrames = 0;
        foreach (var s in samples)
        {
            if (s.CpuBusyMs > s.GpuBusyMs * cpuBoundRatio)
                cpuBoundFrames++;
            else if (s.GpuBusyMs > s.CpuBusyMs * cpuBoundRatio)
                gpuBoundFrames++;
        }
        double cpuBoundPct = (double)cpuBoundFrames / samples.Count * 100.0;
        double gpuBoundPct = (double)gpuBoundFrames / samples.Count * 100.0;

        // Stutter spikes: FrameTimeMs > median * multiplier
        double median = p50;
        int stutterCount = frameTimes.Count(ft => ft > median * stutterSpikeMultiplier);

        // Present mode: most common
        string presentMode = samples
            .GroupBy(s => s.PresentMode)
            .OrderByDescending(g => g.Count())
            .First().Key;

        // AllowsTearing: any frame had tearing
        bool allowsTearing = samples.Any(s => s.AllowsTearing);

        return new FrameTimeSummary(
            Available: true,
            TrackedApplication: trackedApp,
            AvgFps: Math.Round(avgFps, 2),
            P1Fps: Math.Round(p1Fps, 2),
            P50FrameTimeMs: Math.Round(p50, 3),
            P95FrameTimeMs: Math.Round(p95, 3),
            P99FrameTimeMs: Math.Round(p99, 3),
            P999FrameTimeMs: Math.Round(p999, 3),
            DroppedFramePct: Math.Round(droppedPct, 2),
            CpuBoundPct: Math.Round(cpuBoundPct, 2),
            GpuBoundPct: Math.Round(gpuBoundPct, 2),
            PresentMode: presentMode,
            AllowsTearing: allowsTearing,
            StutterCount: stutterCount,
            Notes: notes);
    }

    /// <summary>
    /// Interpolated percentile using the C = 1 method (exclusive).
    /// </summary>
    public static double InterpolatedPercentile(double[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0) return 0;
        if (sortedValues.Length == 1) return sortedValues[0];

        double rank = percentile * (sortedValues.Length - 1);
        int lower = (int)Math.Floor(rank);
        int upper = (int)Math.Ceiling(rank);

        if (lower == upper || upper >= sortedValues.Length)
            return sortedValues[lower];

        double fraction = rank - lower;
        return sortedValues[lower] + fraction * (sortedValues[upper] - sortedValues[lower]);
    }
}
