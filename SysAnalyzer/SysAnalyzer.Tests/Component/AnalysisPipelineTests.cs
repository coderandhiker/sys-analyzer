using SysAnalyzer.Analysis;
using SysAnalyzer.Analysis.Models;
using SysAnalyzer.Capture;
using SysAnalyzer.Config;
using Xunit;

namespace SysAnalyzer.Tests.Component;

public class AnalysisPipelineTests
{
    [Fact]
    public void FullPipeline_WithAllData_ProducesResults()
    {
        var config = ConfigLoader.LoadDefault();
        var pipeline = new AnalysisPipeline(config);

        var snapshots = CreateSnapshots(60);
        var frameSamples = CreateFrameSamples(120);
        var hardware = CreateHardware();
        var sysConfig = CreateSysConfig();

        var result = pipeline.Run(snapshots, frameSamples, null, hardware, sysConfig, "gaming");

        Assert.NotNull(result.AggregatedMetrics);
        Assert.True(result.AggregatedMetrics.SampleCount > 0);
        Assert.NotNull(result.FrameTimeSummary);
        Assert.NotNull(result.ScoringResult);
        Assert.NotNull(result.ScoringResult.Cpu);
    }

    [Fact]
    public void FullPipeline_NoPresentMon_FrameTimeNull()
    {
        var config = ConfigLoader.LoadDefault();
        var pipeline = new AnalysisPipeline(config);

        var snapshots = CreateSnapshots(60);

        var result = pipeline.Run(snapshots, null, null, null, null, "gaming");

        Assert.Null(result.FrameTimeSummary);
        Assert.Null(result.CulpritResult);
        Assert.Null(result.FrameCorrelation);
    }

    [Fact]
    public void Determinism_SameInput_SameOutput()
    {
        var config = ConfigLoader.LoadDefault();
        var snapshots = CreateSnapshots(30);
        var frameSamples = CreateFrameSamples(60);

        var pipeline1 = new AnalysisPipeline(config);
        var result1 = pipeline1.Run(snapshots, frameSamples, null, null, null, "gaming");

        var pipeline2 = new AnalysisPipeline(config);
        var result2 = pipeline2.Run(snapshots, frameSamples, null, null, null, "gaming");

        Assert.Equal(result1.Recommendations.Count, result2.Recommendations.Count);
        for (int i = 0; i < result1.Recommendations.Count; i++)
        {
            Assert.Equal(result1.Recommendations[i].Id, result2.Recommendations[i].Id);
            Assert.Equal(result1.Recommendations[i].Body, result2.Recommendations[i].Body);
        }

        Assert.Equal(result1.ScoringResult.Cpu.Score, result2.ScoringResult.Cpu.Score);
        Assert.Equal(result1.ScoringResult.Memory.Score, result2.ScoringResult.Memory.Score);
    }

    [Fact]
    public void DegradedMode_Tier1Only_StillProducesScores()
    {
        var config = ConfigLoader.LoadDefault();
        var pipeline = new AnalysisPipeline(config);

        // No GPU, no Tier 2 data
        var snapshots = CreateSnapshots(30, gpuNull: true, tier2Null: true);

        var result = pipeline.Run(snapshots, null, null, null, null, "gaming");

        Assert.NotNull(result.ScoringResult.Cpu);
        Assert.NotNull(result.ScoringResult.Cpu.Score);
        Assert.Null(result.ScoringResult.Gpu);
    }

    private static List<SensorSnapshot> CreateSnapshots(int count, bool gpuNull = false, bool tier2Null = false)
    {
        return Enumerable.Range(0, count).Select(i => new SensorSnapshot(
            Timestamp: QpcTimestamp.FromMilliseconds(i * 1000),
            TotalCpuPercent: 50 + i % 20,
            PerCoreCpuPercent: [50 + i % 20, 40 + i % 15],
            ContextSwitchesPerSec: 10000 + i * 100,
            DpcTimePercent: 1.0 + i * 0.05,
            InterruptsPerSec: 5000,
            MemoryUtilizationPercent: 60 + i % 10,
            AvailableMemoryMb: 4000 - i * 50,
            PageFaultsPerSec: 100 + i * 5,
            HardFaultsPerSec: 5 + i % 3,
            CommittedBytes: 8_000_000_000.0 + i * 50_000_000,
            CommittedBytesInUsePercent: 50 + i % 15,
            GpuUtilizationPercent: gpuNull ? null : 70 + i % 15,
            GpuMemoryUtilizationPercent: gpuNull ? null : 60 + i % 10,
            GpuMemoryUsedMb: gpuNull ? null : 4000 + i * 30,
            DiskActiveTimePercent: 30 + i % 20,
            DiskQueueLength: 1.0 + i % 5 * 0.2,
            DiskBytesPerSec: 50_000_000 + i * 500_000,
            DiskReadLatencyMs: 5 + i % 10 * 0.5,
            DiskWriteLatencyMs: 3 + i % 8 * 0.3,
            NetworkBytesPerSec: 1_000_000 + i * 10000,
            NetworkUtilizationPercent: 10 + i % 5,
            TcpRetransmitsPerSec: 0.1 + i % 3 * 0.01,
            CpuTempC: tier2Null ? null : 65 + i % 10,
            CpuClockMhz: tier2Null ? null : 4500 - i % 5 * 10,
            CpuPowerW: tier2Null ? null : 95,
            GpuTempC: tier2Null || gpuNull ? null : 70 + i % 8,
            GpuClockMhz: tier2Null || gpuNull ? null : 1800 - i % 5 * 5,
            GpuPowerW: tier2Null || gpuNull ? null : 200,
            GpuFanRpm: tier2Null || gpuNull ? null : 1500
        )).ToList();
    }

    private static List<FrameTimeSample> CreateFrameSamples(int count)
    {
        return Enumerable.Range(0, count).Select(i => new FrameTimeSample(
            QpcTimestamp.FromMilliseconds(i * 16.67),
            "TestApp.exe",
            16.67 + (i % 10 == 0 ? 20 : 0), // occasional spike
            8.0 + i % 3,
            10.0 + i % 4,
            false,
            "Hardware: Legacy Flip",
            false
        )).ToList();
    }

    private static HardwareInventory CreateHardware() => new(
        "Intel Core i7-13700K", 16, 24, 3400, 5400, 30_000_000,
        [new RamStick("BANK0", 16L * 1024 * 1024 * 1024, 6000, "DDR5"),
         new RamStick("BANK1", 16L * 1024 * 1024 * 1024, 6000, "DDR5")],
        32, 4, 2, "NVIDIA GeForce RTX 4070", 12288, "537.58",
        [new DiskDrive("Samsung 990 Pro", "NVMe", 1_000_000_000_000)],
        "ASUS ROG STRIX Z790-E", "1.0", "2024-01-01", "26100.1", "Windows 11",
        [new NetworkAdapter("Intel Ethernet", 2_500_000_000)],
        "2560x1440", 165);

    private static SystemConfiguration CreateSysConfig() => new(
        "High performance", true, true, false, false, false, 1024, 512, "System managed", 10, "Windows Defender");
}
