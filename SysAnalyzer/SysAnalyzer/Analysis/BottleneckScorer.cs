using SysAnalyzer.Config;

namespace SysAnalyzer.Analysis;

/// <summary>
/// Workload-aware bottleneck scoring (§5.1 Phase 3).
/// Profile-weighted scoring per subsystem with renormalization for missing metrics.
/// </summary>
public static class BottleneckScorer
{
    public static ScoringResult Score(AggregatedMetrics metrics, ProfileConfig profile, ThresholdsConfig thresholds)
    {
        var cpu = ScoreCpu(metrics.Cpu, metrics.Tier2, profile.Scoring.Cpu, thresholds.Cpu);
        var memory = ScoreMemory(metrics.Memory, profile.Scoring.Memory, thresholds.Memory);
        var gpu = metrics.Gpu is not null
            ? ScoreGpu(metrics.Gpu, metrics.Tier2, profile.Scoring.Gpu, thresholds.Gpu)
            : null;
        var disk = ScoreDisk(metrics.Disk, profile.Scoring.Disk, thresholds.Disk);
        var network = ScoreNetwork(metrics.Network, profile.Scoring.Network, thresholds.Network);

        return new ScoringResult(cpu, memory, gpu, disk, network);
    }

    private static SubsystemScore ScoreCpu(CpuMetrics cpu, Tier2Metrics tier2, Dictionary<string, double> weights, Dictionary<string, double> thresholds)
    {
        var components = new List<(string Name, double? Value, double Weight)>
        {
            ("avg_load", cpu.TotalLoad.Mean, weights.GetValueOrDefault("avg_load_weight")),
            ("p95_load", cpu.TotalLoad.P95, weights.GetValueOrDefault("p95_load_weight")),
            ("thermal_throttle", ComputeThermalThrottlePct(tier2), weights.GetValueOrDefault("thermal_throttle_weight")),
            ("single_core_saturation", cpu.SingleCoreSaturationPct, weights.GetValueOrDefault("single_core_saturation_weight")),
            ("dpc_time", cpu.DpcTime.Mean, weights.GetValueOrDefault("dpc_time_weight")),
            ("clock_drop", ComputeClockDropPct(tier2), weights.GetValueOrDefault("clock_drop_weight"))
        };

        return ComputeSubsystemScore(components, thresholds);
    }

    private static SubsystemScore ScoreMemory(MemoryMetrics memory, Dictionary<string, double> weights, Dictionary<string, double> thresholds)
    {
        var components = new List<(string Name, double? Value, double Weight)>
        {
            ("avg_utilization", memory.Utilization.Mean, weights.GetValueOrDefault("avg_utilization_weight")),
            ("page_fault_rate", memory.PageFaults.Mean, weights.GetValueOrDefault("page_fault_rate_weight")),
            ("hard_fault_rate", memory.HardFaults.Mean, weights.GetValueOrDefault("hard_fault_rate_weight")),
            ("commit_ratio", memory.CommitRatio.Mean, weights.GetValueOrDefault("commit_ratio_weight")),
            ("low_available", null, weights.GetValueOrDefault("low_available_weight")) // Computed differently
        };

        return ComputeSubsystemScore(components, thresholds);
    }

    private static SubsystemScore ScoreGpu(GpuMetrics gpu, Tier2Metrics tier2, Dictionary<string, double> weights, Dictionary<string, double> thresholds)
    {
        var components = new List<(string Name, double? Value, double Weight)>
        {
            ("avg_load", gpu.Load.Mean, weights.GetValueOrDefault("avg_load_weight")),
            ("vram_utilization", gpu.VramUtilization?.Mean, weights.GetValueOrDefault("vram_utilization_weight")),
            ("thermal_throttle", ComputeGpuThermalThrottlePct(tier2), weights.GetValueOrDefault("thermal_throttle_weight")),
            ("power_throttle", null, weights.GetValueOrDefault("power_throttle_weight")),
            ("clock_drop", ComputeGpuClockDropPct(tier2), weights.GetValueOrDefault("clock_drop_weight")),
            ("vram_overflow", gpu.VramUtilization is not null && gpu.VramUtilization.P99 >= 95 ? 100.0 : (gpu.VramUtilization?.P99 ?? (double?)null), weights.GetValueOrDefault("vram_overflow_weight"))
        };

        return ComputeSubsystemScore(components, thresholds);
    }

    private static SubsystemScore ScoreDisk(DiskMetrics disk, Dictionary<string, double> weights, Dictionary<string, double> thresholds)
    {
        var components = new List<(string Name, double? Value, double Weight)>
        {
            ("avg_queue_length", disk.QueueLength.Mean, weights.GetValueOrDefault("avg_queue_length_weight")),
            ("avg_latency", (disk.ReadLatency.Mean + disk.WriteLatency.Mean) / 2, weights.GetValueOrDefault("avg_latency_weight")),
            ("active_time", disk.ActiveTime.Mean, weights.GetValueOrDefault("active_time_weight")),
            ("is_hdd", null, weights.GetValueOrDefault("is_hdd_weight")) // requires hardware info
        };

        return ComputeSubsystemScore(components, thresholds);
    }

    private static SubsystemScore ScoreNetwork(NetworkMetrics network, Dictionary<string, double> weights, Dictionary<string, double> thresholds)
    {
        var components = new List<(string Name, double? Value, double Weight)>
        {
            ("avg_utilization", network.Utilization.Mean, weights.GetValueOrDefault("avg_utilization_weight")),
            ("retransmit_rate", network.Retransmits.Mean, weights.GetValueOrDefault("retransmit_rate_weight")),
            ("bandwidth_ceiling", null, weights.GetValueOrDefault("bandwidth_ceiling_weight"))
        };

        return ComputeSubsystemScore(components, thresholds);
    }

    private static SubsystemScore ComputeSubsystemScore(
        List<(string Name, double? Value, double Weight)> components,
        Dictionary<string, double> thresholds)
    {
        double availableWeightedSum = 0;
        double availableWeightSum = 0;
        double totalWeightSum = 0;
        int available = 0;
        int total = components.Count;
        var missing = new List<string>();
        var details = new List<ScoreComponentDetail>();

        foreach (var (name, value, weight) in components)
        {
            totalWeightSum += weight;

            if (value is null || weight == 0)
            {
                if (weight > 0)
                    missing.Add(name);
                details.Add(new ScoreComponentDetail(name, null, 0, weight));
                continue;
            }

            available++;
            double normalized = Normalize(value.Value, name, thresholds);
            availableWeightedSum += normalized * weight;
            availableWeightSum += weight;
            details.Add(new ScoreComponentDetail(name, value.Value, normalized, weight));
        }

        if (availableWeightSum == 0)
            return new SubsystemScore(null, "Unknown", available, total, missing, details);

        // Renormalization: scale up proportionally for missing metrics
        double rawScore = availableWeightedSum / availableWeightSum;
        int score = (int)Math.Round(Math.Clamp(rawScore, 0, 100));

        string classification = score switch
        {
            <= 25 => "Healthy",
            <= 50 => "Moderate",
            <= 75 => "Stressed",
            _ => "Bottleneck"
        };

        return new SubsystemScore(score, classification, available, total, missing, details);
    }

    /// <summary>
    /// Normalize a raw metric value to 0-100 scale based on thresholds.
    /// 0 = at or below healthy, 100 = at or above bottleneck threshold.
    /// Uses generic threshold naming: {metric}_warning → 0-50 range, {metric}_critical → 50-100 range.
    /// Falls back to a simple percentage if no thresholds found.
    /// </summary>
    public static double Normalize(double value, string metricName, Dictionary<string, double> thresholds)
    {
        // Try to find relevant thresholds
        double? warningThreshold = FindThreshold(thresholds, metricName, "warning", "moderate");
        double? criticalThreshold = FindThreshold(thresholds, metricName, "critical", "bottleneck", "stressed");

        if (warningThreshold is null && criticalThreshold is null)
        {
            // For percentage metrics, use value directly
            return Math.Clamp(value, 0, 100);
        }

        double low = warningThreshold ?? 0;
        double high = criticalThreshold ?? 100;

        if (high <= low) return value >= high ? 100 : 0;

        if (value <= low) return 0;
        if (value >= high) return 100;

        return (value - low) / (high - low) * 100.0;
    }

    private static double? FindThreshold(Dictionary<string, double> thresholds, string metricName, params string[] suffixes)
    {
        foreach (var suffix in suffixes)
        {
            // Try direct name match
            if (thresholds.TryGetValue(metricName + "_" + suffix, out double val))
                return val;
        }

        // Try common patterns
        foreach (var (key, val) in thresholds)
        {
            foreach (var suffix in suffixes)
            {
                if (key.Contains(metricName, StringComparison.OrdinalIgnoreCase) && key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    return val;
            }
        }

        return null;
    }

    private static double? ComputeThermalThrottlePct(Tier2Metrics tier2)
    {
        if (tier2.CpuTemp is null) return null;
        // Percentage of time above warning temp (85°C)
        return MetricAggregator.ComputeTimeAboveThreshold(
            [tier2.CpuTemp.Mean], 85);
    }

    private static double? ComputeClockDropPct(Tier2Metrics tier2)
    {
        if (tier2.CpuClock is null) return null;
        // If max clock >> mean, there's clock dropping
        double maxClock = tier2.CpuClock.Max;
        if (maxClock == 0) return 0;
        return (1.0 - tier2.CpuClock.Mean / maxClock) * 100.0;
    }

    private static double? ComputeGpuThermalThrottlePct(Tier2Metrics tier2)
    {
        if (tier2.GpuTemp is null) return null;
        return MetricAggregator.ComputeTimeAboveThreshold(
            [tier2.GpuTemp.Mean], 80);
    }

    private static double? ComputeGpuClockDropPct(Tier2Metrics tier2)
    {
        if (tier2.GpuClock is null) return null;
        double maxClock = tier2.GpuClock.Max;
        if (maxClock == 0) return 0;
        return (1.0 - tier2.GpuClock.Mean / maxClock) * 100.0;
    }

    public static string Classify(int score) => score switch
    {
        <= 25 => "Healthy",
        <= 50 => "Moderate",
        <= 75 => "Stressed",
        _ => "Bottleneck"
    };
}

public record SubsystemScore(int? Score, string Classification, int AvailableMetrics, int TotalMetrics, IReadOnlyList<string> Missing, IReadOnlyList<ScoreComponentDetail>? ComponentDetails = null);

public record ScoreComponentDetail(string Name, double? RawValue, double NormalizedValue, double Weight);

public record ScoringResult(
    SubsystemScore Cpu,
    SubsystemScore Memory,
    SubsystemScore? Gpu,
    SubsystemScore Disk,
    SubsystemScore Network
);
