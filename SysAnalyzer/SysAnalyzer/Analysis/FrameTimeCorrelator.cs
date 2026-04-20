using SysAnalyzer.Analysis.Models;
using SysAnalyzer.Capture;

namespace SysAnalyzer.Analysis;

/// <summary>
/// Correlates frame-time stutter spikes with system metrics within ±2s windows (§5.1 Phase 2).
/// Tags each spike by probable cause and detects periodic patterns.
/// </summary>
public class FrameTimeCorrelator
{
    public FrameTimeCorrelation? Correlate(
        IReadOnlyList<FrameTimeSample>? stutterSpikes,
        IReadOnlyList<SensorSnapshot> snapshots,
        CulpritAttributionResult? culprits)
    {
        if (stutterSpikes is null || stutterSpikes.Count == 0)
            return null;

        var taggedSpikes = new List<TaggedSpike>();

        foreach (var spike in stutterSpikes)
        {
            var tags = new List<SpikeTag>();

            // Find nearest sensor snapshot within ±2s
            var nearestSnapshot = FindNearest(snapshots, spike.Timestamp, CorrelationWindows.MetricCorrelation);

            if (nearestSnapshot is not null)
            {
                // VRAM overflow
                if (nearestSnapshot.GpuMemoryUtilizationPercent.HasValue && nearestSnapshot.GpuMemoryUtilizationPercent >= 99)
                    tags.Add(new SpikeTag("vram_overflow", "GPU VRAM at " + nearestSnapshot.GpuMemoryUtilizationPercent.Value.ToString("F0") + "%"));

                // Disk stall
                if (nearestSnapshot.DiskQueueLength > 5)
                    tags.Add(new SpikeTag("disk_stall", "Disk queue length: " + nearestSnapshot.DiskQueueLength.ToString("F1")));

                // Memory pressure
                if (nearestSnapshot.CommittedBytesInUsePercent > 90)
                    tags.Add(new SpikeTag("memory_pressure", "Committed bytes at " + nearestSnapshot.CommittedBytesInUsePercent.ToString("F0") + "%"));

                // DPC storm
                if (nearestSnapshot.DpcTimePercent > 8)
                {
                    string driverInfo = GetTopDpcDriver(culprits);
                    tags.Add(new SpikeTag("dpc_storm", "DPC time: " + nearestSnapshot.DpcTimePercent.ToString("F1") + "%" + driverInfo));
                }
            }

            // CPU-bound vs GPU-bound from frame timing
            if (spike.CpuBusyMs > spike.GpuBusyMs * 1.5)
                tags.Add(new SpikeTag("cpu_bound", "CPU frame: " + spike.CpuBusyMs.ToString("F1") + "ms > GPU: " + spike.GpuBusyMs.ToString("F1") + "ms"));
            else if (spike.GpuBusyMs > spike.CpuBusyMs * 1.5)
                tags.Add(new SpikeTag("gpu_bound", "GPU frame: " + spike.GpuBusyMs.ToString("F1") + "ms > CPU: " + spike.CpuBusyMs.ToString("F1") + "ms"));

            // Interference from culprit data
            if (culprits is not null && culprits.HasAttribution)
            {
                var topProcess = culprits.TopContextSwitchProcesses.FirstOrDefault(p => p.CorrelationWithStutter > 0.3);
                if (topProcess is not null)
                    tags.Add(new SpikeTag("interference", topProcess.ProcessName + " (correlation: " + topProcess.CorrelationWithStutter.ToString("F2") + ")"));
            }

            if (tags.Count == 0)
                tags.Add(new SpikeTag("unknown", "No correlated system metric found"));

            taggedSpikes.Add(new TaggedSpike(spike.Timestamp.ToSeconds(), spike.FrameTimeMs, tags));
        }

        // Group by cause
        var causeCounts = new Dictionary<string, int>();
        foreach (var ts in taggedSpikes)
        {
            foreach (var tag in ts.Tags)
            {
                causeCounts.TryGetValue(tag.Cause, out int count);
                causeCounts[tag.Cause] = count + 1;
            }
        }

        var causeBreakdown = causeCounts
            .Select(kv => new CauseBreakdown(kv.Key, kv.Value, (double)kv.Value / taggedSpikes.Count * 100.0))
            .OrderByDescending(c => c.Count)
            .ToList();

        // Detect periodic patterns
        var periodicPattern = DetectPeriodicPattern(stutterSpikes);

        return new FrameTimeCorrelation(taggedSpikes, causeBreakdown, periodicPattern);
    }

    private static SensorSnapshot? FindNearest(IReadOnlyList<SensorSnapshot> snapshots, QpcTimestamp target, TimeSpan window)
    {
        SensorSnapshot? best = null;
        long bestDelta = long.MaxValue;

        foreach (var s in snapshots)
        {
            long delta = Math.Abs(s.Timestamp.RawTicks - target.RawTicks);
            long windowTicks = (long)(window.TotalSeconds * QpcTimestamp.Frequency);

            if (delta <= windowTicks && delta < bestDelta)
            {
                bestDelta = delta;
                best = s;
            }
        }
        return best;
    }

    private static string GetTopDpcDriver(CulpritAttributionResult? culprits)
    {
        if (culprits is null || culprits.TopDpcDrivers.Count == 0) return "";
        return " (driver: " + culprits.TopDpcDrivers[0].DriverModule + ")";
    }

    private static PeriodicPattern? DetectPeriodicPattern(IReadOnlyList<FrameTimeSample> spikes)
    {
        if (spikes.Count < 3) return null;

        var intervals = new List<double>();
        for (int i = 1; i < spikes.Count; i++)
        {
            double intervalSec = (spikes[i].Timestamp - spikes[i - 1].Timestamp).ToSeconds();
            intervals.Add(intervalSec);
        }

        double meanInterval = intervals.Average();
        double stdDev = MetricAggregator.ComputeStdDev(intervals.ToArray(), meanInterval);
        double cv = meanInterval > 0 ? stdDev / meanInterval : double.MaxValue;

        // CV < 0.3 → regular interval → periodic
        if (cv < 0.3 && meanInterval > 1.0)
            return new PeriodicPattern(meanInterval, cv, true);

        return null;
    }
}

public record FrameTimeCorrelation(
    IReadOnlyList<TaggedSpike> TaggedSpikes,
    IReadOnlyList<CauseBreakdown> CauseBreakdown,
    PeriodicPattern? PeriodicPattern
);

public record TaggedSpike(double TimestampSeconds, double FrameTimeMs, IReadOnlyList<SpikeTag> Tags);
public record SpikeTag(string Cause, string Detail);
public record CauseBreakdown(string Cause, int Count, double Percentage);
public record PeriodicPattern(double IntervalSeconds, double CoefficientOfVariation, bool IsScheduled);
