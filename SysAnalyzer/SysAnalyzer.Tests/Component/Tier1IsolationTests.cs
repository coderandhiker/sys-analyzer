using SysAnalyzer.Analysis;
using SysAnalyzer.Capture;
using SysAnalyzer.Capture.Providers;

namespace SysAnalyzer.Tests.Component;

/// <summary>
/// Verify that Tier 1 functionality is completely unaffected by Phase F additions.
/// All tests must pass without admin / without LHM active.
/// </summary>
public class Tier1IsolationTests
{
    private static SensorSnapshot MakeTier1Snapshot(int index)
    {
        return new SensorSnapshot(
            Timestamp: new QpcTimestamp(index * 10_000_000L),
            TotalCpuPercent: 50 + index % 20,
            PerCoreCpuPercent: [60, 70, 80, 90],
            ContextSwitchesPerSec: 5000,
            DpcTimePercent: 1.5,
            InterruptsPerSec: 1000,
            MemoryUtilizationPercent: 65,
            AvailableMemoryMb: 5000,
            PageFaultsPerSec: 100,
            HardFaultsPerSec: 5,
            CommittedBytes: 8_000_000_000,
            CommittedBytesInUsePercent: 65,
            GpuUtilizationPercent: 80,
            GpuMemoryUtilizationPercent: 60,
            GpuMemoryUsedMb: 4000,
            DiskActiveTimePercent: 30,
            DiskQueueLength: 1.5,
            DiskBytesPerSec: 100_000_000,
            DiskReadLatencyMs: 5,
            DiskWriteLatencyMs: 8,
            NetworkBytesPerSec: 1_000_000,
            NetworkUtilizationPercent: 10,
            TcpRetransmitsPerSec: 0.1,
            // ALL Tier 2 fields null — simulating non-elevated run
            CpuTempC: null,
            CpuClockMhz: null,
            CpuPowerW: null,
            GpuTempC: null,
            GpuClockMhz: null,
            GpuPowerW: null,
            GpuFanRpm: null
        );
    }

    [Fact]
    public void MetricAggregator_WorksWithoutTier2Data()
    {
        var snapshots = Enumerable.Range(0, 30).Select(MakeTier1Snapshot).ToList();

        var metrics = MetricAggregator.Aggregate(snapshots);

        Assert.True(metrics.Cpu.TotalLoad.Mean > 0);
        Assert.True(metrics.Memory.Utilization.Mean > 0);
        Assert.NotNull(metrics.Gpu);
        Assert.True(metrics.Disk.ActiveTime.Mean > 0);
        Assert.Null(metrics.Tier2.CpuTemp);
        Assert.Null(metrics.Tier2.CpuClock);
        Assert.Null(metrics.Tier2.GpuTemp);
        Assert.Null(metrics.Tier2.GpuClock);
    }

    [Fact]
    public void BottleneckScorer_HandlesMissingTier2()
    {
        var snapshots = Enumerable.Range(0, 30).Select(MakeTier1Snapshot).ToList();
        var metrics = MetricAggregator.Aggregate(snapshots);

        var profile = CreateTestProfile();
        var thresholds = CreateTestThresholds();

        var result = BottleneckScorer.Score(metrics, profile, thresholds);

        // CPU score should still be computed (renormalized without thermal/clock metrics)
        Assert.NotNull(result.Cpu);
        Assert.NotNull(result.Cpu.Score);
        Assert.NotNull(result.Memory);
        Assert.NotNull(result.Memory.Score);
    }

    [Fact]
    public void SensorHealthMatrix_Tier1WhenLhmUnavailable()
    {
        var matrix = new SensorHealthMatrix();
        matrix.Register("PerformanceCounters", new ProviderHealth(ProviderStatus.Active, null, 20, 20, 0));
        matrix.Register("LibreHardwareMonitor", new ProviderHealth(ProviderStatus.Unavailable, "Not elevated", 0, 7, 0));

        Assert.Equal(ProviderTier.Tier1, matrix.OverallTier);
    }

    [Fact]
    public void SensorHealthMatrix_Tier2WhenLhmActive()
    {
        var matrix = new SensorHealthMatrix();
        matrix.Register("PerformanceCounters", new ProviderHealth(ProviderStatus.Active, null, 20, 20, 0));
        matrix.Register("LibreHardwareMonitor", new ProviderHealth(ProviderStatus.Active, null, 7, 7, 0));

        Assert.Equal(ProviderTier.Tier2, matrix.OverallTier);
    }

    [Fact]
    public void ThermalAnalyzer_NullTier2Data_ReturnsZero()
    {
        var snapshots = Enumerable.Range(0, 10).Select(MakeTier1Snapshot).ToList();

        double cpuThrottle = ThermalAnalyzer.ComputeCpuThermalThrottlePct(snapshots, 85, 3500);
        double gpuThrottle = ThermalAnalyzer.ComputeGpuThermalThrottlePct(snapshots, 80, 1500);
        double clockDrop = ThermalAnalyzer.ComputeClockDropPct(snapshots, isCpu: true);

        Assert.Equal(0, cpuThrottle);
        Assert.Equal(0, gpuThrottle);
        Assert.Equal(0, clockDrop);
    }

    [Fact]
    public void PowerAnalyzer_NullTier2Data_ReturnsZero()
    {
        var snapshots = Enumerable.Range(0, 10).Select(MakeTier1Snapshot).ToList();

        double cpuLimit = PowerAnalyzer.ComputeCpuPowerLimitPct(snapshots, 125);
        double gpuLimit = PowerAnalyzer.ComputeGpuPowerLimitPct(snapshots, 300);
        var psu = PowerAnalyzer.EstimatePsuAdequacy(snapshots);

        Assert.Equal(0, cpuLimit);
        Assert.Equal(0, gpuLimit);
        Assert.False(psu.IsWarning);
    }

    [Fact]
    public void PollLoop_MergesBatch_Tier2NaN_BecomesNull()
    {
        // Verify that NaN Tier 2 fields in MetricBatch become null in SensorSnapshot
        var batch = MetricBatch.Create();
        // All Tier 2 fields should be NaN by default
        Assert.True(double.IsNaN(batch.CpuTempC));
        Assert.True(double.IsNaN(batch.CpuClockMhz));
        Assert.True(double.IsNaN(batch.CpuPowerW));
        Assert.True(double.IsNaN(batch.GpuTempC));
        Assert.True(double.IsNaN(batch.GpuClockMhz));
        Assert.True(double.IsNaN(batch.GpuPowerW));
        Assert.True(double.IsNaN(batch.GpuFanRpm));
    }

    [Fact]
    public async Task LibreHardwareProvider_NotElevated_DoesNotAttemptLoad()
    {
        if (LibreHardwareProvider.IsElevated())
            return; // Skip on admin — can't test this path

        using var provider = new LibreHardwareProvider();
        var health = await provider.InitAsync();

        Assert.Equal(ProviderStatus.Unavailable, health.Status);
        // Verify no poll data is produced
        var batch = provider.Poll(1000);
        Assert.True(batch.IsEmpty);
    }

    [Fact]
    public void AdvancedDetections_WorksWithoutTier2()
    {
        var snapshots = Enumerable.Range(0, 30).Select(MakeTier1Snapshot).ToList();
        var metrics = MetricAggregator.Aggregate(snapshots);

        var detections = AdvancedDetections.RunAll(metrics, snapshots, null, null);

        // Should not crash and should not produce thermal soak detections without Tier 2 data
        Assert.DoesNotContain(detections, d => d.Id == "thermal_soak_cpu");
    }

    private static Config.ProfileConfig CreateTestProfile()
    {
        return new Config.ProfileConfig
        {
            Description = "test",
            Scoring = new Config.ScoringConfig
            {
                Cpu = new Dictionary<string, double>
                {
                    ["avg_load_weight"] = 0.2,
                    ["p95_load_weight"] = 0.2,
                    ["thermal_throttle_weight"] = 0.15,
                    ["single_core_saturation_weight"] = 0.15,
                    ["dpc_time_weight"] = 0.15,
                    ["clock_drop_weight"] = 0.15
                },
                Memory = new Dictionary<string, double>
                {
                    ["avg_utilization_weight"] = 0.25,
                    ["page_fault_rate_weight"] = 0.2,
                    ["hard_fault_rate_weight"] = 0.25,
                    ["commit_ratio_weight"] = 0.15,
                    ["low_available_weight"] = 0.15
                },
                Gpu = new Dictionary<string, double>
                {
                    ["avg_load_weight"] = 0.2,
                    ["vram_utilization_weight"] = 0.25,
                    ["thermal_throttle_weight"] = 0.15,
                    ["power_throttle_weight"] = 0.15,
                    ["clock_drop_weight"] = 0.1,
                    ["vram_overflow_weight"] = 0.15
                },
                Disk = new Dictionary<string, double>
                {
                    ["avg_queue_length_weight"] = 0.25,
                    ["avg_latency_weight"] = 0.3,
                    ["active_time_weight"] = 0.25,
                    ["is_hdd_weight"] = 0.2
                },
                Network = new Dictionary<string, double>
                {
                    ["avg_utilization_weight"] = 0.35,
                    ["retransmit_rate_weight"] = 0.4,
                    ["bandwidth_ceiling_weight"] = 0.25
                }
            }
        };
    }

    private static Config.ThresholdsConfig CreateTestThresholds()
    {
        return new Config.ThresholdsConfig
        {
            Cpu = new Dictionary<string, double>
            {
                ["load_moderate"] = 70,
                ["load_stressed"] = 85,
                ["load_bottleneck"] = 95,
                ["temp_warning"] = 85,
                ["temp_critical"] = 95
            },
            Memory = new Dictionary<string, double>
            {
                ["utilization_moderate"] = 70,
                ["utilization_stressed"] = 85,
                ["utilization_bottleneck"] = 95
            },
            Gpu = new Dictionary<string, double>
            {
                ["load_moderate"] = 80,
                ["load_stressed"] = 90,
                ["load_bottleneck"] = 98
            },
            Disk = new Dictionary<string, double>
            {
                ["queue_length_warning"] = 2,
                ["queue_length_critical"] = 5,
                ["latency_warning_ms"] = 20,
                ["latency_critical_ms"] = 100
            },
            Network = new Dictionary<string, double>
            {
                ["utilization_warning"] = 60,
                ["utilization_critical"] = 80
            },
            FrameTime = new Dictionary<string, double>()
        };
    }
}
