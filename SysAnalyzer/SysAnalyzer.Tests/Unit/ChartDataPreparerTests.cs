using SysAnalyzer.Capture;
using SysAnalyzer.Analysis.Models;
using SysAnalyzer.Report;

namespace SysAnalyzer.Tests.Unit;

public class ChartDataPreparerTests
{
    [Fact]
    public void PrepareAll_EmptySnapshots_ReturnsNoSystemCharts()
    {
        var snapshots = Array.Empty<SensorSnapshot>();

        var result = ChartDataPreparer.PrepareAll(snapshots, null, null, 60);

        Assert.DoesNotContain("cpuUtilization", result.Keys);
        Assert.DoesNotContain("memoryUtilization", result.Keys);
    }

    [Fact]
    public void PrepareAll_WithSnapshots_ReturnsCpuMemoryDiskNetworkCharts()
    {
        var snapshots = CreateTestSnapshots(30);

        var result = ChartDataPreparer.PrepareAll(snapshots, null, null, 30);

        Assert.Contains("cpuUtilization", result.Keys);
        Assert.Contains("memoryUtilization", result.Keys);
        Assert.Contains("diskActivity", result.Keys);
        Assert.Contains("networkThroughput", result.Keys);
    }

    [Fact]
    public void PrepareAll_WithGpuData_ReturnsGpuChart()
    {
        var snapshots = CreateTestSnapshots(30, hasGpu: true);

        var result = ChartDataPreparer.PrepareAll(snapshots, null, null, 30);

        Assert.Contains("gpuLoad", result.Keys);
    }

    [Fact]
    public void PrepareAll_WithoutGpuData_NoGpuChart()
    {
        var snapshots = CreateTestSnapshots(30, hasGpu: false);

        var result = ChartDataPreparer.PrepareAll(snapshots, null, null, 30);

        Assert.DoesNotContain("gpuLoad", result.Keys);
    }

    [Fact]
    public void PrepareAll_WithFrameTimeSamples_ReturnsFrameCharts()
    {
        var snapshots = CreateTestSnapshots(30);
        var frameSamples = CreateTestFrameSamples(100);
        var frameTimeSummary = new FrameTimeSummary(
            true, "TestApp.exe", 60, 30, 16.6, 20.0, 33.0, 50.0,
            0.5, 20, 75, "Hardware: Independent Flip", true, 2, null);

        var result = ChartDataPreparer.PrepareAll(snapshots, frameTimeSummary, frameSamples, 30);

        Assert.Contains("frameTimeOverTime", result.Keys);
        Assert.Contains("frameTimeDistribution", result.Keys);
        Assert.Contains("fpsOverTime", result.Keys);
    }

    [Fact]
    public void PrepareAll_WithoutFrameTimeSamples_NoFrameCharts()
    {
        var snapshots = CreateTestSnapshots(30);

        var result = ChartDataPreparer.PrepareAll(snapshots, null, null, 30);

        Assert.DoesNotContain("frameTimeOverTime", result.Keys);
        Assert.DoesNotContain("frameTimeDistribution", result.Keys);
        Assert.DoesNotContain("fpsOverTime", result.Keys);
    }

    private static List<SensorSnapshot> CreateTestSnapshots(int count, bool hasGpu = false)
    {
        var snapshots = new List<SensorSnapshot>();
        for (int i = 0; i < count; i++)
        {
            snapshots.Add(new SensorSnapshot(
                Timestamp: new QpcTimestamp((long)(i * QpcTimestamp.Frequency)),
                TotalCpuPercent: 30 + i * 0.5,
                PerCoreCpuPercent: new[] { 25.0, 35.0, 20.0, 40.0 },
                ContextSwitchesPerSec: 1000 + i * 10,
                DpcTimePercent: 1.5,
                InterruptsPerSec: 500,
                MemoryUtilizationPercent: 45 + i * 0.3,
                AvailableMemoryMb: 16000 - i * 50,
                PageFaultsPerSec: 200 + i * 5,
                HardFaultsPerSec: 10,
                CommittedBytes: 8000000000,
                CommittedBytesInUsePercent: 50,
                GpuUtilizationPercent: hasGpu ? 70 + i * 0.5 : null,
                GpuMemoryUtilizationPercent: hasGpu ? 60 + i * 0.3 : null,
                GpuMemoryUsedMb: hasGpu ? 6000 : null,
                DiskActiveTimePercent: 20 + i * 0.2,
                DiskQueueLength: 1.5,
                DiskBytesPerSec: 50000000,
                DiskReadLatencyMs: 2.5,
                DiskWriteLatencyMs: 3.0,
                NetworkBytesPerSec: 1000000,
                NetworkUtilizationPercent: 5,
                TcpRetransmitsPerSec: 0.1,
                CpuTempC: null,
                CpuClockMhz: null,
                CpuPowerW: null,
                GpuTempC: null,
                GpuClockMhz: null,
                GpuPowerW: null,
                GpuFanRpm: null
            ));
        }
        return snapshots;
    }

    private static List<FrameTimeSample> CreateTestFrameSamples(int count)
    {
        var samples = new List<FrameTimeSample>();
        for (int i = 0; i < count; i++)
        {
            samples.Add(new FrameTimeSample(
                Timestamp: new QpcTimestamp((long)(i * (QpcTimestamp.Frequency / 60.0))),
                ApplicationName: "TestApp.exe",
                FrameTimeMs: 16.6 + (i % 10 == 0 ? 20.0 : 0),
                CpuBusyMs: 8.0,
                GpuBusyMs: 12.0,
                Dropped: false,
                PresentMode: "Hardware: Independent Flip",
                AllowsTearing: true
            ));
        }
        return samples;
    }
}
