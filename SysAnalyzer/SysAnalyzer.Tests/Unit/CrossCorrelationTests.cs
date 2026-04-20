using SysAnalyzer.Analysis;
using Xunit;

namespace SysAnalyzer.Tests.Unit;

public class CrossCorrelationTests
{
    [Fact]
    public void CpuAndMemoryBothHigh_CompoundDetected()
    {
        var metrics = MakeMetrics(cpuP95: 90, memP95: 90);
        var result = CrossCorrelationDetector.Detect(metrics, null);
        Assert.Contains(result.Patterns, p => p.Id == "cpu_memory_compound");
    }

    [Fact]
    public void GpuBoundCpuHeadroom_Detected()
    {
        var metrics = MakeMetrics(cpuMean: 40, gpuP95: 98);
        var result = CrossCorrelationDetector.Detect(metrics, null);
        Assert.Contains(result.Patterns, p => p.Id == "gpu_bound_cpu_headroom");
    }

    [Fact]
    public void PagefileThrash_Detected()
    {
        var metrics = MakeMetrics(diskActiveP95: 85, hardFaultP95: 150);
        var result = CrossCorrelationDetector.Detect(metrics, null);
        Assert.Contains(result.Patterns, p => p.Id == "pagefile_thrash");
    }

    [Fact]
    public void SingleThreadBottleneck_Detected()
    {
        var metrics = MakeMetrics(cpuMean: 30, singleCoreSat: 60);
        var result = CrossCorrelationDetector.Detect(metrics, null);
        Assert.Contains(result.Patterns, p => p.Id == "single_thread_bottleneck");
    }

    [Fact]
    public void VramOverflow_Detected()
    {
        var metrics = MakeMetrics(gpuP95: 95, vramP95: 97);
        var result = CrossCorrelationDetector.Detect(metrics, null);
        Assert.Contains(result.Patterns, p => p.Id == "vram_overflow");
    }

    [Fact]
    public void SingleResourceStress_NoFalseCompound()
    {
        // Only CPU is high, memory is fine
        var metrics = MakeMetrics(cpuP95: 95, memP95: 50);
        var result = CrossCorrelationDetector.Detect(metrics, null);
        Assert.DoesNotContain(result.Patterns, p => p.Id == "cpu_memory_compound");
    }

    [Fact]
    public void HealthySystem_NoPatterns()
    {
        var metrics = MakeMetrics(cpuMean: 30, cpuP95: 40, memP95: 50, gpuP95: 40);
        var result = CrossCorrelationDetector.Detect(metrics, null);
        Assert.Empty(result.Patterns);
    }

    private static AggregatedMetrics MakeMetrics(
        double cpuMean = 50, double cpuP95 = 60,
        double memP95 = 60,
        double gpuP95 = 60, double vramP95 = 50,
        double diskActiveP95 = 30, double hardFaultP95 = 5,
        double singleCoreSat = 10,
        double cpuTempP95 = 70)
    {
        return new AggregatedMetrics(
            new CpuMetrics(
                new MetricStatistics(cpuMean, cpuMean, cpuP95, cpuP95 + 2, cpuP95 + 3, 10, 100, 5, 0, 0),
                new MetricStatistics(1, 1, 2, 3, 3, 0.5, 3, 0.5, 0, 0),
                new MetricStatistics(20000, 20000, 25000, 30000, 30000, 15000, 35000, 3000, 0, 0),
                singleCoreSat, 0, 0),
            new MemoryMetrics(
                new MetricStatistics(memP95 - 10, memP95 - 10, memP95, memP95 + 2, memP95 + 3, 40, memP95 + 5, 3, 0, 0),
                new MetricStatistics(100, 100, 200, 300, 300, 50, 300, 50, 0, 0),
                new MetricStatistics(hardFaultP95 / 2, hardFaultP95 / 2, hardFaultP95, hardFaultP95 + 10, hardFaultP95 + 10, 0, hardFaultP95 + 20, 3, 0, 0),
                new MetricStatistics(8e9, 8e9, 9e9, 9.5e9, 9.5e9, 7e9, 10e9, 5e8, 0, 0),
                new MetricStatistics(50, 50, 55, 58, 60, 45, 60, 3, 0, 0)),
            new GpuMetrics(
                new MetricStatistics(gpuP95 - 10, gpuP95 - 10, gpuP95, gpuP95 + 1, gpuP95 + 2, 30, 100, 5, 0, 0),
                new MetricStatistics(vramP95 - 10, vramP95 - 10, vramP95, vramP95 + 1, vramP95 + 2, 30, 100, 3, 0, 0),
                new MetricStatistics(4000, 4000, 4200, 4300, 4300, 3800, 4400, 100, 0, 0)),
            new DiskMetrics(
                new MetricStatistics(diskActiveP95 - 10, diskActiveP95 - 10, diskActiveP95, diskActiveP95 + 3, diskActiveP95 + 5, 5, 100, 5, 0, 0),
                new MetricStatistics(1, 1, 2, 3, 3, 0, 3, 0.5, 0, 0),
                new MetricStatistics(5, 5, 8, 10, 10, 2, 12, 2, 0, 0),
                new MetricStatistics(3, 3, 5, 7, 7, 1, 8, 1, 0, 0),
                new MetricStatistics(50e6, 50e6, 80e6, 100e6, 100e6, 20e6, 120e6, 20e6, 0, 0)),
            new NetworkMetrics(
                new MetricStatistics(5, 5, 10, 15, 15, 1, 20, 3, 0, 0),
                new MetricStatistics(0.1, 0.1, 0.2, 0.3, 0.3, 0, 0.5, 0.1, 0, 0),
                new MetricStatistics(1e6, 1e6, 2e6, 3e6, 3e6, 500000, 4e6, 500000, 0, 0)),
            new Tier2Metrics(
                new MetricStatistics(cpuTempP95 - 5, cpuTempP95 - 5, cpuTempP95, cpuTempP95 + 2, cpuTempP95 + 3, 60, cpuTempP95 + 5, 3, 0, 0),
                new MetricStatistics(4500, 4500, 4600, 4650, 4650, 4400, 4700, 50, 0, 0),
                new MetricStatistics(70, 70, 75, 78, 78, 60, 80, 3, 0, 0),
                new MetricStatistics(1800, 1800, 1850, 1870, 1870, 1750, 1900, 30, 0, 0)),
            60, 60, false);
    }
}
