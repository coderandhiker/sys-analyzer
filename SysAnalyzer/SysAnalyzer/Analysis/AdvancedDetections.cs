using SysAnalyzer.Analysis.Models;
using SysAnalyzer.Capture;

namespace SysAnalyzer.Analysis;

/// <summary>
/// Advanced detection algorithms (§15): thermal soak, memory leak, frame-time patterns,
/// NUMA imbalance, storage tiering, driver age, cross-resource contention.
/// </summary>
public static class AdvancedDetections
{
    public static List<AdvancedDetection> RunAll(
        AggregatedMetrics metrics,
        IReadOnlyList<SensorSnapshot> snapshots,
        FrameTimeSummary? frameTime,
        HardwareInventory? hardware)
    {
        var detections = new List<AdvancedDetection>();

        DetectThermalSoak(metrics, snapshots, detections);
        DetectMemoryLeak(metrics, snapshots, frameTime, detections);
        DetectFrameTimePatterns(frameTime, detections);
        DetectNumaImbalance(hardware, detections);
        DetectStorageTiering(hardware, detections);
        DetectDriverAge(hardware, detections);
        DetectCrossResourceContention(metrics, detections);

        return detections;
    }

    /// <summary>§15.1: Thermal soak — temp hasn't plateaued, monotonic increase with R² > 0.8.</summary>
    public static void DetectThermalSoak(AggregatedMetrics metrics, IReadOnlyList<SensorSnapshot> snapshots, List<AdvancedDetection> detections)
    {
        if (metrics.DurationSeconds < 300) return; // Need 5+ minutes minimum

        // Tier 2: use actual temps
        if (metrics.Tier2.CpuTemp is not null)
        {
            var temps = snapshots.Select(s => s.CpuTempC).Where(t => t.HasValue).Select(t => t!.Value).ToArray();
            if (temps.Length >= 10)
            {
                var (slope, r2) = MetricAggregator.ComputeTrendSlope(temps);
                if (slope > 0.01 && r2 > 0.8)
                {
                    detections.Add(new AdvancedDetection("thermal_soak_cpu",
                        "CPU Thermal Soak Detected",
                        $"CPU temperature is continuously rising (slope: {slope:F2}°C/sample, R²: {r2:F2}). Cooling may be inadequate.",
                        "critical"));
                }
            }
        }

        // Tier 1 fallback: clock speed drops over time
        if (metrics.Tier2.CpuTemp is null && metrics.Tier2.CpuClock is not null)
        {
            var clocks = snapshots.Select(s => s.CpuClockMhz).Where(c => c.HasValue).Select(c => c!.Value).ToArray();
            if (clocks.Length >= 10)
            {
                var (slope, r2) = MetricAggregator.ComputeTrendSlope(clocks);
                if (slope < -1 && r2 > 0.7)
                {
                    detections.Add(new AdvancedDetection("thermal_soak_clock_fallback",
                        "Possible Thermal Throttling (Clock Drop)",
                        $"CPU clock speed is declining over time (slope: {slope:F1} MHz/sample, R²: {r2:F2}), suggesting thermal throttling.",
                        "warning"));
                }
            }
        }
    }

    /// <summary>§15.2: Memory leak — linear regression on committed bytes, positive slope + R² > 0.8.</summary>
    public static void DetectMemoryLeak(AggregatedMetrics metrics, IReadOnlyList<SensorSnapshot> snapshots,
        FrameTimeSummary? frameTime, List<AdvancedDetection> detections)
    {
        var committed = snapshots.Select(s => s.CommittedBytes).ToArray();
        if (committed.Length < 10) return;

        var (slope, r2) = MetricAggregator.ComputeTrendSlope(committed);

        if (slope > 0 && r2 > 0.8)
        {
            // slope is bytes per sample. Convert to MB/hour assuming 1 sample/sec
            double mbPerHour = slope * 3600 / (1024 * 1024);
            string fpsNote = frameTime is not null ? " Correlate with FPS degradation." : "";

            detections.Add(new AdvancedDetection("memory_leak",
                "Possible Memory Leak Detected",
                $"Committed bytes are steadily increasing ({mbPerHour:F0} MB/hour, R²: {r2:F2}).{fpsNote}",
                "warning"));
        }
    }

    /// <summary>§15.4: Frame-time pattern analysis — CV > 0.4 → inconsistent pacing.</summary>
    public static void DetectFrameTimePatterns(FrameTimeSummary? frameTime, List<AdvancedDetection> detections)
    {
        if (frameTime is null || !frameTime.Available) return;

        // CV = stddev / mean. We approximate from P50 and P95.
        double approxStdDev = (frameTime.P95FrameTimeMs - frameTime.P50FrameTimeMs) / 1.645;
        double cv = frameTime.P50FrameTimeMs > 0 ? approxStdDev / frameTime.P50FrameTimeMs : 0;

        if (cv > 0.4)
        {
            detections.Add(new AdvancedDetection("frame_time_inconsistent",
                "Inconsistent Frame Pacing",
                $"Frame time coefficient of variation is high ({cv:F2}), indicating very inconsistent frame pacing.",
                "warning"));
        }
    }

    /// <summary>§15.5: NUMA/channel imbalance — 3 sticks in 4-slot board.</summary>
    public static void DetectNumaImbalance(HardwareInventory? hardware, List<AdvancedDetection> detections)
    {
        if (hardware is null) return;

        int stickCount = hardware.RamSticks.Count;
        int totalSlots = hardware.TotalMemorySlots;

        // 3 sticks in a 4-slot board → broken dual-channel
        if (totalSlots == 4 && stickCount == 3)
        {
            detections.Add(new AdvancedDetection("numa_imbalance",
                "Broken Dual-Channel Memory Configuration",
                "3 RAM sticks in a 4-slot board breaks dual-channel mode. Remove one stick or add a fourth for optimal bandwidth.",
                "warning"));
        }

        // 1 stick in multi-slot → single channel
        if (totalSlots >= 2 && stickCount == 1)
        {
            detections.Add(new AdvancedDetection("single_channel_memory",
                "Single-Channel Memory Configuration",
                "Only 1 RAM stick installed. Adding a matching stick enables dual-channel mode, roughly doubling memory bandwidth.",
                "warning"));
        }
    }

    /// <summary>§15.6: Storage tiering — OS on NVMe but game on HDD.</summary>
    public static void DetectStorageTiering(HardwareInventory? hardware, List<AdvancedDetection> detections)
    {
        if (hardware is null || hardware.Disks.Count < 2) return;

        bool hasNvme = hardware.Disks.Any(d => d.DriveType.Contains("NVMe", StringComparison.OrdinalIgnoreCase));
        bool hasHdd = hardware.Disks.Any(d => d.DriveType.Equals("HDD", StringComparison.OrdinalIgnoreCase));

        if (hasNvme && hasHdd)
        {
            detections.Add(new AdvancedDetection("storage_tiering",
                "Mixed Storage: NVMe + HDD",
                "System has both NVMe and HDD drives. Ensure games and frequently-used applications are on the NVMe drive.",
                "info"));
        }
    }

    /// <summary>§15.8: Driver age — GPU driver > 6 months old.</summary>
    public static void DetectDriverAge(HardwareInventory? hardware, List<AdvancedDetection> detections)
    {
        if (hardware?.GpuDriverVersion is null) return;

        // Simple heuristic: check if driver version looks old based on known patterns
        // This is a placeholder — real implementation would check driver date from registry
        // For now, always mark as info if driver version is available
    }

    /// <summary>§15.9: Cross-resource contention patterns.</summary>
    public static void DetectCrossResourceContention(AggregatedMetrics metrics, List<AdvancedDetection> detections)
    {
        // CPU-GPU imbalance
        if (metrics.Gpu is not null)
        {
            double cpuLoad = metrics.Cpu.TotalLoad.Mean;
            double gpuLoad = metrics.Gpu.Load.Mean;

            if (cpuLoad > 90 && gpuLoad < 50)
            {
                detections.Add(new AdvancedDetection("cpu_gpu_imbalance",
                    "CPU-GPU Imbalance",
                    $"CPU at {cpuLoad:F0}% while GPU at {gpuLoad:F0}%. The CPU is bottlenecking the GPU.",
                    "warning"));
            }
        }

        // Storage-memory cascade: high disk + high memory
        if (metrics.Disk.ActiveTime.P95 > 80 && metrics.Memory.HardFaults.P95 > 50)
        {
            detections.Add(new AdvancedDetection("storage_memory_cascade",
                "Storage-Memory Cascade",
                "High disk activity coincides with hard page faults, indicating memory pressure is spilling to disk.",
                "critical"));
        }

        // VRAM-RAM spillover
        if (metrics.Gpu?.VramUtilization is not null && metrics.Gpu.VramUtilization.P95 > 95 && metrics.Memory.Utilization.P95 > 85)
        {
            detections.Add(new AdvancedDetection("vram_ram_spillover",
                "VRAM-RAM Spillover",
                "GPU VRAM is full and system RAM is also under pressure, indicating VRAM data is spilling to system memory.",
                "critical"));
        }
    }
}

public record AdvancedDetection(string Id, string Title, string Description, string Severity);
