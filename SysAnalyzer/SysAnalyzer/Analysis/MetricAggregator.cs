using SysAnalyzer.Capture;

namespace SysAnalyzer.Analysis;

/// <summary>
/// Statistical aggregation of time-series sensor snapshots (§5.1 Phase 1).
/// Computes mean, percentiles, stddev, time-above-threshold, and trend slope for each metric.
/// </summary>
public static class MetricAggregator
{
    public static AggregatedMetrics Aggregate(IReadOnlyList<SensorSnapshot> snapshots, Dictionary<string, double>? thresholds = null)
    {
        if (snapshots.Count == 0)
            return AggregatedMetrics.Empty;

        double durationSeconds = snapshots.Count > 1
            ? (snapshots[^1].Timestamp - snapshots[0].Timestamp).ToSeconds()
            : 0;

        bool shortCapture = durationSeconds < 30;

        var cpu = AggregateCpu(snapshots, thresholds);
        var memory = AggregateMemory(snapshots, thresholds);
        var gpu = AggregateGpu(snapshots, thresholds);
        var disk = AggregateDisk(snapshots, thresholds);
        var network = AggregateNetwork(snapshots, thresholds);
        var tier2 = AggregateTier2(snapshots);

        return new AggregatedMetrics(cpu, memory, gpu, disk, network, tier2, snapshots.Count, durationSeconds, shortCapture);
    }

    private static CpuMetrics AggregateCpu(IReadOnlyList<SensorSnapshot> snapshots, Dictionary<string, double>? thresholds)
    {
        var totalLoad = snapshots.Select(s => s.TotalCpuPercent).ToArray();
        var dpcTime = snapshots.Select(s => s.DpcTimePercent).ToArray();
        var ctxSwitches = snapshots.Select(s => s.ContextSwitchesPerSec).ToArray();

        // Single-core saturation: percentage of samples where any core > 98%
        double singleCoreSatPct = 0;
        if (snapshots[0].PerCoreCpuPercent.Length > 0)
        {
            int saturatedSamples = snapshots.Count(s => s.PerCoreCpuPercent.Any(c => c >= 98));
            singleCoreSatPct = (double)saturatedSamples / snapshots.Count * 100.0;
        }

        return new CpuMetrics(
            AggregateArray(totalLoad),
            AggregateArray(dpcTime),
            AggregateArray(ctxSwitches),
            singleCoreSatPct,
            ComputeTimeAboveThreshold(totalLoad, thresholds?.GetValueOrDefault("cpu_load_moderate", 70) ?? 70),
            ComputeTimeAboveThreshold(totalLoad, thresholds?.GetValueOrDefault("cpu_load_bottleneck", 95) ?? 95)
        );
    }

    private static MemoryMetrics AggregateMemory(IReadOnlyList<SensorSnapshot> snapshots, Dictionary<string, double>? thresholds)
    {
        var utilization = snapshots.Select(s => s.MemoryUtilizationPercent).ToArray();
        var pageFaults = snapshots.Select(s => s.PageFaultsPerSec).ToArray();
        var hardFaults = snapshots.Select(s => s.HardFaultsPerSec).ToArray();
        var committed = snapshots.Select(s => s.CommittedBytes).ToArray();
        var commitRatio = snapshots.Select(s => s.CommittedBytesInUsePercent).ToArray();

        return new MemoryMetrics(
            AggregateArray(utilization),
            AggregateArray(pageFaults),
            AggregateArray(hardFaults),
            AggregateArray(committed),
            AggregateArray(commitRatio)
        );
    }

    private static GpuMetrics? AggregateGpu(IReadOnlyList<SensorSnapshot> snapshots, Dictionary<string, double>? thresholds)
    {
        var gpuLoad = snapshots.Select(s => s.GpuUtilizationPercent).Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        if (gpuLoad.Length == 0)
            return null;

        var vramUtil = snapshots.Select(s => s.GpuMemoryUtilizationPercent).Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        var vramUsed = snapshots.Select(s => s.GpuMemoryUsedMb).Where(v => v.HasValue).Select(v => v!.Value).ToArray();

        return new GpuMetrics(
            AggregateArray(gpuLoad),
            vramUtil.Length > 0 ? AggregateArray(vramUtil) : null,
            vramUsed.Length > 0 ? AggregateArray(vramUsed) : null
        );
    }

    private static DiskMetrics AggregateDisk(IReadOnlyList<SensorSnapshot> snapshots, Dictionary<string, double>? thresholds)
    {
        var activeTime = snapshots.Select(s => s.DiskActiveTimePercent).ToArray();
        var queueLength = snapshots.Select(s => s.DiskQueueLength).ToArray();
        var readLatency = snapshots.Select(s => s.DiskReadLatencyMs).ToArray();
        var writeLatency = snapshots.Select(s => s.DiskWriteLatencyMs).ToArray();
        var throughput = snapshots.Select(s => s.DiskBytesPerSec).ToArray();

        return new DiskMetrics(
            AggregateArray(activeTime),
            AggregateArray(queueLength),
            AggregateArray(readLatency),
            AggregateArray(writeLatency),
            AggregateArray(throughput)
        );
    }

    private static NetworkMetrics AggregateNetwork(IReadOnlyList<SensorSnapshot> snapshots, Dictionary<string, double>? thresholds)
    {
        var utilization = snapshots.Select(s => s.NetworkUtilizationPercent).ToArray();
        var retransmits = snapshots.Select(s => s.TcpRetransmitsPerSec).ToArray();
        var throughput = snapshots.Select(s => s.NetworkBytesPerSec).ToArray();

        return new NetworkMetrics(
            AggregateArray(utilization),
            AggregateArray(retransmits),
            AggregateArray(throughput)
        );
    }

    private static Tier2Metrics AggregateTier2(IReadOnlyList<SensorSnapshot> snapshots)
    {
        var cpuTemp = snapshots.Select(s => s.CpuTempC).Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        var cpuClock = snapshots.Select(s => s.CpuClockMhz).Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        var gpuTemp = snapshots.Select(s => s.GpuTempC).Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        var gpuClock = snapshots.Select(s => s.GpuClockMhz).Where(v => v.HasValue).Select(v => v!.Value).ToArray();

        return new Tier2Metrics(
            cpuTemp.Length > 0 ? AggregateArray(cpuTemp) : null,
            cpuClock.Length > 0 ? AggregateArray(cpuClock) : null,
            gpuTemp.Length > 0 ? AggregateArray(gpuTemp) : null,
            gpuClock.Length > 0 ? AggregateArray(gpuClock) : null
        );
    }

    public static MetricStatistics AggregateArray(double[] values)
    {
        if (values.Length == 0)
            return MetricStatistics.Empty;

        double mean = values.Average();
        double min = values.Min();
        double max = values.Max();

        var sorted = (double[])values.Clone();
        Array.Sort(sorted);

        double p50 = InterpolatedPercentile(sorted, 0.50);
        double p95 = InterpolatedPercentile(sorted, 0.95);
        double p99 = InterpolatedPercentile(sorted, 0.99);
        double p999 = InterpolatedPercentile(sorted, 0.999);

        double stdDev = ComputeStdDev(values, mean);
        var trend = ComputeTrendSlope(values);

        return new MetricStatistics(mean, p50, p95, p99, p999, min, max, stdDev, trend.Slope, trend.RSquared);
    }

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

    public static double ComputeStdDev(double[] values, double mean)
    {
        if (values.Length <= 1) return 0;
        double sumSqDiff = values.Sum(v => (v - mean) * (v - mean));
        return Math.Sqrt(sumSqDiff / (values.Length - 1));
    }

    public static (double Slope, double RSquared) ComputeTrendSlope(double[] values)
    {
        if (values.Length < 2)
            return (0, 0);

        int n = values.Length;
        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;

        for (int i = 0; i < n; i++)
        {
            sumX += i;
            sumY += values[i];
            sumXY += i * values[i];
            sumX2 += (double)i * i;
        }

        double denom = n * sumX2 - sumX * sumX;
        if (denom == 0)
            return (0, 0);

        double slope = (n * sumXY - sumX * sumY) / denom;
        double intercept = (sumY - slope * sumX) / n;

        // R² calculation
        double meanY = sumY / n;
        double ssTotal = 0, ssResidual = 0;
        for (int i = 0; i < n; i++)
        {
            double predicted = intercept + slope * i;
            ssTotal += (values[i] - meanY) * (values[i] - meanY);
            ssResidual += (values[i] - predicted) * (values[i] - predicted);
        }

        double rSquared = ssTotal > 0 ? 1.0 - ssResidual / ssTotal : 0;
        return (slope, Math.Max(0, rSquared));
    }

    public static double ComputeTimeAboveThreshold(double[] values, double threshold)
    {
        if (values.Length == 0) return 0;
        int count = values.Count(v => v > threshold);
        return (double)count / values.Length * 100.0;
    }
}

public record MetricStatistics(
    double Mean,
    double P50,
    double P95,
    double P99,
    double P999,
    double Min,
    double Max,
    double StdDev,
    double TrendSlope,
    double TrendRSquared
)
{
    public static MetricStatistics Empty => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}

public record CpuMetrics(
    MetricStatistics TotalLoad,
    MetricStatistics DpcTime,
    MetricStatistics ContextSwitches,
    double SingleCoreSaturationPct,
    double TimeAboveModerate,
    double TimeAboveBottleneck
);

public record MemoryMetrics(
    MetricStatistics Utilization,
    MetricStatistics PageFaults,
    MetricStatistics HardFaults,
    MetricStatistics CommittedBytes,
    MetricStatistics CommitRatio
);

public record GpuMetrics(
    MetricStatistics Load,
    MetricStatistics? VramUtilization,
    MetricStatistics? VramUsedMb
);

public record DiskMetrics(
    MetricStatistics ActiveTime,
    MetricStatistics QueueLength,
    MetricStatistics ReadLatency,
    MetricStatistics WriteLatency,
    MetricStatistics Throughput
);

public record NetworkMetrics(
    MetricStatistics Utilization,
    MetricStatistics Retransmits,
    MetricStatistics Throughput
);

public record Tier2Metrics(
    MetricStatistics? CpuTemp,
    MetricStatistics? CpuClock,
    MetricStatistics? GpuTemp,
    MetricStatistics? GpuClock
);

public record AggregatedMetrics(
    CpuMetrics Cpu,
    MemoryMetrics Memory,
    GpuMetrics? Gpu,
    DiskMetrics Disk,
    NetworkMetrics Network,
    Tier2Metrics Tier2,
    int SampleCount,
    double DurationSeconds,
    bool ShortCapture
)
{
    public static AggregatedMetrics Empty => new(
        new CpuMetrics(MetricStatistics.Empty, MetricStatistics.Empty, MetricStatistics.Empty, 0, 0, 0),
        new MemoryMetrics(MetricStatistics.Empty, MetricStatistics.Empty, MetricStatistics.Empty, MetricStatistics.Empty, MetricStatistics.Empty),
        null,
        new DiskMetrics(MetricStatistics.Empty, MetricStatistics.Empty, MetricStatistics.Empty, MetricStatistics.Empty, MetricStatistics.Empty),
        new NetworkMetrics(MetricStatistics.Empty, MetricStatistics.Empty, MetricStatistics.Empty),
        new Tier2Metrics(null, null, null, null),
        0, 0, true);
}
