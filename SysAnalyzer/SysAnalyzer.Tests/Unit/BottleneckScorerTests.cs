using SysAnalyzer.Analysis;
using SysAnalyzer.Capture;
using SysAnalyzer.Config;
using Xunit;

namespace SysAnalyzer.Tests.Unit;

public class BottleneckScorerTests
{
    private static ProfileConfig GamingProfile => CreateGamingProfile();
    private static ProfileConfig CompilingProfile => CreateCompilingProfile();
    private static ThresholdsConfig Thresholds => CreateThresholds();

    [Fact]
    public void GamingProfile_CpuBound_HighCpuScore()
    {
        var metrics = CreateCpuBoundMetrics();
        var result = BottleneckScorer.Score(metrics, GamingProfile, Thresholds);

        Assert.NotNull(result.Cpu.Score);
        Assert.True(result.Cpu.Score >= 50, $"Expected CPU score >= 50 for CPU-bound, got {result.Cpu.Score}");
    }

    [Fact]
    public void GamingProfile_GpuBound_HighGpuScore()
    {
        var metrics = CreateGpuBoundMetrics();
        var result = BottleneckScorer.Score(metrics, GamingProfile, Thresholds);

        Assert.NotNull(result.Gpu);
        Assert.NotNull(result.Gpu.Score);
        Assert.True(result.Gpu.Score > 50, $"Expected GPU score > 50 for GPU-bound, got {result.Gpu.Score}");
    }

    [Fact]
    public void DifferentProfiles_DifferentScores()
    {
        var metrics = CreateCpuBoundMetrics();
        var gaming = BottleneckScorer.Score(metrics, GamingProfile, Thresholds);
        var compiling = BottleneckScorer.Score(metrics, CompilingProfile, Thresholds);

        // They should differ because weights differ
        Assert.NotEqual(gaming.Cpu.Score, compiling.Cpu.Score);
    }

    [Fact]
    public void MissingGpu_NullGpuScore()
    {
        var metrics = CreateMetricsNoGpu();
        var result = BottleneckScorer.Score(metrics, GamingProfile, Thresholds);
        Assert.Null(result.Gpu);
    }

    [Fact]
    public void AllMetricsMissing_NullScore()
    {
        var metrics = AggregatedMetrics.Empty;
        var result = BottleneckScorer.Score(metrics, GamingProfile, Thresholds);
        Assert.Null(result.Gpu);
    }

    [Fact]
    public void MissingTier2Metrics_ScoreRenormalized()
    {
        var metrics = CreateMetricsNoTier2();
        var result = BottleneckScorer.Score(metrics, GamingProfile, Thresholds);

        Assert.NotNull(result.Cpu.Score);
        Assert.True(result.Cpu.Missing.Count > 0, "Should report missing metrics");
        Assert.True(result.Cpu.AvailableMetrics < result.Cpu.TotalMetrics);
    }

    [Fact]
    public void Classification_Boundaries()
    {
        Assert.Equal("Healthy", BottleneckScorer.Classify(0));
        Assert.Equal("Healthy", BottleneckScorer.Classify(25));
        Assert.Equal("Moderate", BottleneckScorer.Classify(26));
        Assert.Equal("Moderate", BottleneckScorer.Classify(50));
        Assert.Equal("Stressed", BottleneckScorer.Classify(51));
        Assert.Equal("Stressed", BottleneckScorer.Classify(75));
        Assert.Equal("Bottleneck", BottleneckScorer.Classify(76));
        Assert.Equal("Bottleneck", BottleneckScorer.Classify(100));
    }

    [Fact]
    public void Normalize_AtWarning_Returns0()
    {
        var thresholds = new Dictionary<string, double> { ["test_warning"] = 70, ["test_critical"] = 95 };
        double result = BottleneckScorer.Normalize(70, "test", thresholds);
        Assert.Equal(0, result, 0.1);
    }

    [Fact]
    public void Normalize_AtCritical_Returns100()
    {
        var thresholds = new Dictionary<string, double> { ["test_warning"] = 70, ["test_critical"] = 95 };
        double result = BottleneckScorer.Normalize(95, "test", thresholds);
        Assert.Equal(100, result, 0.1);
    }

    [Fact]
    public void Normalize_BetweenThresholds_ReturnsProportional()
    {
        var thresholds = new Dictionary<string, double> { ["test_warning"] = 0, ["test_critical"] = 100 };
        double result = BottleneckScorer.Normalize(50, "test", thresholds);
        Assert.Equal(50, result, 0.1);
    }

    #region Fixture Helpers

    private static AggregatedMetrics CreateCpuBoundMetrics()
    {
        return new AggregatedMetrics(
            new CpuMetrics(
                new MetricStatistics(95, 95, 99, 100, 100, 80, 100, 5, 0.1, 0.5), // high CPU
                new MetricStatistics(2, 2, 3, 4, 4, 1, 5, 1, 0, 0),
                new MetricStatistics(50000, 50000, 60000, 70000, 70000, 30000, 70000, 5000, 0, 0),
                80, 90, 60),
            new MemoryMetrics(
                new MetricStatistics(60, 60, 65, 68, 70, 55, 70, 3, 0, 0),
                new MetricStatistics(100, 100, 200, 300, 300, 50, 300, 50, 0, 0),
                new MetricStatistics(5, 5, 10, 15, 15, 0, 15, 3, 0, 0),
                new MetricStatistics(8e9, 8e9, 9e9, 9.5e9, 9.5e9, 7e9, 10e9, 5e8, 0, 0),
                new MetricStatistics(50, 50, 55, 58, 60, 45, 60, 3, 0, 0)),
            new GpuMetrics(
                new MetricStatistics(40, 40, 50, 55, 55, 30, 60, 5, 0, 0), // low GPU
                new MetricStatistics(50, 50, 55, 58, 60, 40, 60, 3, 0, 0),
                new MetricStatistics(4000, 4000, 4200, 4300, 4300, 3800, 4400, 100, 0, 0)),
            new DiskMetrics(
                new MetricStatistics(20, 20, 30, 35, 35, 10, 40, 5, 0, 0),
                new MetricStatistics(1, 1, 2, 3, 3, 0, 3, 0.5, 0, 0),
                new MetricStatistics(5, 5, 8, 10, 10, 2, 12, 2, 0, 0),
                new MetricStatistics(3, 3, 5, 7, 7, 1, 8, 1, 0, 0),
                new MetricStatistics(50e6, 50e6, 80e6, 100e6, 100e6, 20e6, 120e6, 20e6, 0, 0)),
            new NetworkMetrics(
                new MetricStatistics(5, 5, 10, 15, 15, 1, 20, 3, 0, 0),
                new MetricStatistics(0.1, 0.1, 0.2, 0.3, 0.3, 0, 0.5, 0.1, 0, 0),
                new MetricStatistics(1e6, 1e6, 2e6, 3e6, 3e6, 500000, 4e6, 500000, 0, 0)),
            new Tier2Metrics(
                new MetricStatistics(85, 85, 90, 92, 92, 75, 95, 3, 0.1, 0.8),
                new MetricStatistics(4500, 4500, 4600, 4650, 4650, 4400, 4700, 50, 0, 0),
                new MetricStatistics(65, 65, 70, 72, 72, 60, 75, 3, 0, 0),
                new MetricStatistics(1800, 1800, 1850, 1870, 1870, 1750, 1900, 30, 0, 0)),
            60, 60, false);
    }

    private static AggregatedMetrics CreateGpuBoundMetrics()
    {
        return new AggregatedMetrics(
            new CpuMetrics(
                new MetricStatistics(40, 40, 50, 55, 55, 30, 60, 5, 0, 0), // low CPU
                new MetricStatistics(1, 1, 2, 3, 3, 0.5, 3, 0.5, 0, 0),
                new MetricStatistics(20000, 20000, 25000, 30000, 30000, 15000, 35000, 3000, 0, 0),
                10, 20, 5),
            new MemoryMetrics(
                new MetricStatistics(55, 55, 60, 62, 63, 50, 65, 3, 0, 0),
                new MetricStatistics(80, 80, 150, 200, 200, 40, 250, 40, 0, 0),
                new MetricStatistics(3, 3, 6, 8, 8, 0, 10, 2, 0, 0),
                new MetricStatistics(7e9, 7e9, 8e9, 8.5e9, 8.5e9, 6e9, 9e9, 4e8, 0, 0),
                new MetricStatistics(45, 45, 50, 53, 55, 40, 55, 3, 0, 0)),
            new GpuMetrics(
                new MetricStatistics(98, 98, 100, 100, 100, 90, 100, 2, 0, 0), // very high GPU
                new MetricStatistics(92, 92, 96, 98, 99, 85, 99, 3, 0.1, 0.9),
                new MetricStatistics(7500, 7500, 7800, 7900, 7950, 7000, 8000, 200, 0, 0)),
            new DiskMetrics(
                new MetricStatistics(15, 15, 20, 25, 25, 5, 30, 4, 0, 0),
                new MetricStatistics(0.5, 0.5, 1, 1.5, 1.5, 0, 2, 0.3, 0, 0),
                new MetricStatistics(3, 3, 5, 7, 7, 1, 8, 1, 0, 0),
                new MetricStatistics(2, 2, 4, 5, 5, 1, 6, 1, 0, 0),
                new MetricStatistics(30e6, 30e6, 50e6, 70e6, 70e6, 10e6, 80e6, 15e6, 0, 0)),
            new NetworkMetrics(
                new MetricStatistics(3, 3, 5, 8, 8, 1, 10, 2, 0, 0),
                new MetricStatistics(0.05, 0.05, 0.1, 0.15, 0.15, 0, 0.2, 0.03, 0, 0),
                new MetricStatistics(500000, 500000, 1e6, 1.5e6, 1.5e6, 200000, 2e6, 300000, 0, 0)),
            new Tier2Metrics(
                new MetricStatistics(70, 70, 75, 78, 78, 65, 80, 3, 0, 0),
                new MetricStatistics(4600, 4600, 4650, 4680, 4680, 4550, 4700, 30, 0, 0),
                new MetricStatistics(82, 82, 87, 89, 89, 75, 90, 3, 0.05, 0.7),
                new MetricStatistics(1750, 1750, 1800, 1820, 1820, 1700, 1850, 25, -0.5, 0.6)),
            60, 60, false);
    }

    private static AggregatedMetrics CreateMetricsNoGpu()
    {
        var cpuBound = CreateCpuBoundMetrics();
        return cpuBound with { Gpu = null };
    }

    private static AggregatedMetrics CreateMetricsNoTier2()
    {
        var cpuBound = CreateCpuBoundMetrics();
        return cpuBound with { Tier2 = new Tier2Metrics(null, null, null, null) };
    }

    private static ProfileConfig CreateGamingProfile()
    {
        return new ProfileConfig
        {
            Description = "Gaming",
            Scoring = new ScoringConfig
            {
                Cpu = new Dictionary<string, double>
                {
                    ["avg_load_weight"] = 0.15, ["p95_load_weight"] = 0.20,
                    ["thermal_throttle_weight"] = 0.15, ["single_core_saturation_weight"] = 0.20,
                    ["dpc_time_weight"] = 0.15, ["clock_drop_weight"] = 0.15
                },
                Memory = new Dictionary<string, double>
                {
                    ["avg_utilization_weight"] = 0.20, ["page_fault_rate_weight"] = 0.20,
                    ["hard_fault_rate_weight"] = 0.25, ["commit_ratio_weight"] = 0.15,
                    ["low_available_weight"] = 0.20
                },
                Gpu = new Dictionary<string, double>
                {
                    ["avg_load_weight"] = 0.20, ["vram_utilization_weight"] = 0.25,
                    ["thermal_throttle_weight"] = 0.15, ["power_throttle_weight"] = 0.15,
                    ["clock_drop_weight"] = 0.10, ["vram_overflow_weight"] = 0.15
                },
                Disk = new Dictionary<string, double>
                {
                    ["avg_queue_length_weight"] = 0.25, ["avg_latency_weight"] = 0.30,
                    ["active_time_weight"] = 0.25, ["is_hdd_weight"] = 0.20
                },
                Network = new Dictionary<string, double>
                {
                    ["avg_utilization_weight"] = 0.35, ["retransmit_rate_weight"] = 0.40,
                    ["bandwidth_ceiling_weight"] = 0.25
                }
            }
        };
    }

    private static ProfileConfig CreateCompilingProfile()
    {
        return new ProfileConfig
        {
            Description = "Compiling",
            Scoring = new ScoringConfig
            {
                Cpu = new Dictionary<string, double>
                {
                    ["avg_load_weight"] = 0.25, ["p95_load_weight"] = 0.30,
                    ["thermal_throttle_weight"] = 0.15, ["single_core_saturation_weight"] = 0.05,
                    ["dpc_time_weight"] = 0.10, ["clock_drop_weight"] = 0.15
                },
                Memory = new Dictionary<string, double>
                {
                    ["avg_utilization_weight"] = 0.25, ["page_fault_rate_weight"] = 0.25,
                    ["hard_fault_rate_weight"] = 0.25, ["commit_ratio_weight"] = 0.15,
                    ["low_available_weight"] = 0.10
                },
                Gpu = new Dictionary<string, double>
                {
                    ["avg_load_weight"] = 0.40, ["vram_utilization_weight"] = 0.10,
                    ["thermal_throttle_weight"] = 0.10, ["power_throttle_weight"] = 0.10,
                    ["clock_drop_weight"] = 0.10, ["vram_overflow_weight"] = 0.20
                },
                Disk = new Dictionary<string, double>
                {
                    ["avg_queue_length_weight"] = 0.30, ["avg_latency_weight"] = 0.25,
                    ["active_time_weight"] = 0.30, ["is_hdd_weight"] = 0.15
                },
                Network = new Dictionary<string, double>
                {
                    ["avg_utilization_weight"] = 0.50, ["retransmit_rate_weight"] = 0.25,
                    ["bandwidth_ceiling_weight"] = 0.25
                }
            }
        };
    }

    private static ThresholdsConfig CreateThresholds()
    {
        return new ThresholdsConfig
        {
            Cpu = new Dictionary<string, double>
            {
                ["load_moderate"] = 70, ["load_stressed"] = 85, ["load_bottleneck"] = 95,
                ["temp_warning"] = 85, ["temp_critical"] = 95,
                ["dpc_warning"] = 3, ["dpc_critical"] = 8,
                ["single_core_saturation"] = 98, ["clock_drop_pct_warning"] = 10
            },
            Memory = new Dictionary<string, double>
            {
                ["utilization_moderate"] = 70, ["utilization_stressed"] = 85, ["utilization_bottleneck"] = 95,
                ["available_warning_mb"] = 1024, ["available_critical_mb"] = 512,
                ["hard_faults_warning"] = 50, ["hard_faults_critical"] = 200,
                ["commit_ratio_warning"] = 0.85, ["commit_ratio_critical"] = 0.95
            },
            Gpu = new Dictionary<string, double>
            {
                ["load_moderate"] = 80, ["load_stressed"] = 90, ["load_bottleneck"] = 98,
                ["vram_utilization_warning"] = 80, ["vram_utilization_critical"] = 95,
                ["temp_warning"] = 80, ["temp_critical"] = 90
            },
            Disk = new Dictionary<string, double>
            {
                ["queue_length_warning"] = 2, ["queue_length_critical"] = 5,
                ["latency_warning_ms"] = 20, ["latency_critical_ms"] = 100,
                ["active_time_warning"] = 70, ["active_time_critical"] = 90
            },
            Network = new Dictionary<string, double>
            {
                ["utilization_warning"] = 60, ["utilization_critical"] = 80,
                ["retransmit_rate_warning"] = 0.5, ["retransmit_rate_critical"] = 2.0
            }
        };
    }

    #endregion
}
