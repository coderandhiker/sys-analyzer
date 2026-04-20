namespace SysAnalyzer.Analysis;

/// <summary>
/// Detects compound bottleneck patterns from cross-correlation of multiple subsystems (§5.2).
/// </summary>
public static class CrossCorrelationDetector
{
    public static CrossCorrelationResult Detect(AggregatedMetrics metrics, FrameTimeCorrelation? frameCorrelation)
    {
        var patterns = new List<CompoundPattern>();

        // CPU + Memory both high → compound diagnosis
        if (metrics.Cpu.TotalLoad.P95 > 85 && metrics.Memory.Utilization.P95 > 85)
            patterns.Add(new CompoundPattern("cpu_memory_compound", "CPU and Memory both stressed",
                "High CPU load combined with high memory utilization indicates system-wide resource exhaustion.", Severity.Critical));

        // GPU 100% + CPU low → GPU-bound with CPU headroom
        if (metrics.Gpu is not null && metrics.Gpu.Load.P95 > 95 && metrics.Cpu.TotalLoad.Mean < 60)
            patterns.Add(new CompoundPattern("gpu_bound_cpu_headroom", "GPU-bound with CPU headroom",
                "GPU is fully utilized while CPU has spare capacity. Consider lowering graphics settings or upgrading GPU.", Severity.Warning));

        // Disk high + Memory high → pagefile thrashing
        if (metrics.Disk.ActiveTime.P95 > 80 && metrics.Memory.HardFaults.P95 > 100)
            patterns.Add(new CompoundPattern("pagefile_thrash", "Pagefile thrashing detected",
                "High disk activity combined with frequent hard page faults indicates the system is actively swapping memory to disk.", Severity.Critical));

        // Single core maxed + others idle → single-threaded bottleneck
        if (metrics.Cpu.SingleCoreSaturationPct > 50 && metrics.Cpu.TotalLoad.Mean < 50)
            patterns.Add(new CompoundPattern("single_thread_bottleneck", "Single-threaded bottleneck",
                "One CPU core is frequently saturated while overall CPU utilization is low, indicating a single-threaded workload bottleneck.", Severity.Warning));

        // GPU VRAM full + GPU load spiking → VRAM overflow
        if (metrics.Gpu?.VramUtilization is not null && metrics.Gpu.VramUtilization.P95 > 95 && metrics.Gpu.Load.P95 > 90)
            patterns.Add(new CompoundPattern("vram_overflow", "VRAM overflow",
                "GPU VRAM is nearly full while GPU load is high. This causes texture thrashing and frame drops.", Severity.Critical));

        // CPU thermal throttle + high load → cooling inadequate
        if (metrics.Tier2.CpuTemp is not null && metrics.Tier2.CpuTemp.P95 > 90 && metrics.Cpu.TotalLoad.P95 > 80)
            patterns.Add(new CompoundPattern("thermal_throttle_cpu", "CPU thermal throttling under load",
                "CPU temperature exceeds 90°C under high load, indicating inadequate cooling.", Severity.Critical));

        // Triple: frame spikes + VRAM 99% + disk bursts → VRAM exhaustion (highest confidence)
        if (frameCorrelation is not null &&
            frameCorrelation.CauseBreakdown.Any(c => c.Cause == "vram_overflow") &&
            metrics.Disk.QueueLength.P95 > 3)
        {
            patterns.Add(new CompoundPattern("vram_exhaustion_triple", "VRAM exhaustion confirmed",
                "Frame-time spikes correlated with VRAM overflow and disk burst activity. This is a confirmed VRAM exhaustion pattern.", Severity.Critical,
                Confidence: "high"));
        }

        return new CrossCorrelationResult(patterns);
    }
}

public enum Severity { Info, Warning, Critical }

public record CompoundPattern(
    string Id,
    string Title,
    string Description,
    Severity Severity,
    string Confidence = "medium"
);

public record CrossCorrelationResult(IReadOnlyList<CompoundPattern> Patterns);
