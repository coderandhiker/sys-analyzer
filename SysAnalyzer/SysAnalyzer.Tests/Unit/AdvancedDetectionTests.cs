using SysAnalyzer.Analysis;
using SysAnalyzer.Capture;
using Xunit;

namespace SysAnalyzer.Tests.Unit;

public class AdvancedDetectionTests
{
    [Fact]
    public void ThermalSoak_MonotonicTempCurve_Detected()
    {
        var snapshots = Enumerable.Range(0, 60).Select(i => MakeSnapshot(i * 1000, cpuTempC: 60 + i * 0.5)).ToList();
        var metrics = MetricAggregator.Aggregate(snapshots);
        // Override duration to be > 5 minutes
        metrics = metrics with { DurationSeconds = 600 };

        var detections = new List<AdvancedDetection>();
        AdvancedDetections.DetectThermalSoak(metrics, snapshots, detections);

        Assert.Contains(detections, d => d.Id == "thermal_soak_cpu");
    }

    [Fact]
    public void ThermalSoak_FlatCurve_NotDetected()
    {
        var snapshots = Enumerable.Range(0, 60).Select(i => MakeSnapshot(i * 1000, cpuTempC: 70)).ToList();
        var metrics = MetricAggregator.Aggregate(snapshots);
        metrics = metrics with { DurationSeconds = 600 };

        var detections = new List<AdvancedDetection>();
        AdvancedDetections.DetectThermalSoak(metrics, snapshots, detections);

        Assert.DoesNotContain(detections, d => d.Id == "thermal_soak_cpu");
    }

    [Fact]
    public void MemoryLeak_PositiveSlope_Detected()
    {
        var snapshots = Enumerable.Range(0, 60).Select(i =>
            MakeSnapshot(i * 1000, committedBytes: 8_000_000_000.0 + i * 10_000_000)).ToList();
        var metrics = MetricAggregator.Aggregate(snapshots);

        var detections = new List<AdvancedDetection>();
        AdvancedDetections.DetectMemoryLeak(metrics, snapshots, null, detections);

        Assert.Contains(detections, d => d.Id == "memory_leak");
    }

    [Fact]
    public void MemoryLeak_StableCurve_NotDetected()
    {
        var snapshots = Enumerable.Range(0, 60).Select(i =>
            MakeSnapshot(i * 1000, committedBytes: 8_000_000_000.0)).ToList();
        var metrics = MetricAggregator.Aggregate(snapshots);

        var detections = new List<AdvancedDetection>();
        AdvancedDetections.DetectMemoryLeak(metrics, snapshots, null, detections);

        Assert.DoesNotContain(detections, d => d.Id == "memory_leak");
    }

    [Fact]
    public void NumaImbalance_3SticksIn4Slots_Detected()
    {
        var hardware = MakeHardware(stickCount: 3, totalSlots: 4);
        var detections = new List<AdvancedDetection>();
        AdvancedDetections.DetectNumaImbalance(hardware, detections);

        Assert.Contains(detections, d => d.Id == "numa_imbalance");
    }

    [Fact]
    public void NumaImbalance_2SticksIn4Slots_NotDetected()
    {
        var hardware = MakeHardware(stickCount: 2, totalSlots: 4);
        var detections = new List<AdvancedDetection>();
        AdvancedDetections.DetectNumaImbalance(hardware, detections);

        Assert.DoesNotContain(detections, d => d.Id == "numa_imbalance");
    }

    [Fact]
    public void SingleChannelMemory_Detected()
    {
        var hardware = MakeHardware(stickCount: 1, totalSlots: 2);
        var detections = new List<AdvancedDetection>();
        AdvancedDetections.DetectNumaImbalance(hardware, detections);

        Assert.Contains(detections, d => d.Id == "single_channel_memory");
    }

    private static SensorSnapshot MakeSnapshot(double timestampMs,
        double cpuTempC = 70, double committedBytes = 8_000_000_000.0)
    {
        return new SensorSnapshot(
            Timestamp: QpcTimestamp.FromMilliseconds(timestampMs),
            TotalCpuPercent: 50, PerCoreCpuPercent: [50, 50],
            ContextSwitchesPerSec: 10000, DpcTimePercent: 1, InterruptsPerSec: 5000,
            MemoryUtilizationPercent: 60, AvailableMemoryMb: 4000,
            PageFaultsPerSec: 100, HardFaultsPerSec: 5,
            CommittedBytes: committedBytes, CommittedBytesInUsePercent: 50,
            GpuUtilizationPercent: 50, GpuMemoryUtilizationPercent: 50, GpuMemoryUsedMb: 4000,
            DiskActiveTimePercent: 20, DiskQueueLength: 1, DiskBytesPerSec: 50_000_000,
            DiskReadLatencyMs: 5, DiskWriteLatencyMs: 3,
            NetworkBytesPerSec: 1_000_000, NetworkUtilizationPercent: 10, TcpRetransmitsPerSec: 0.1,
            CpuTempC: cpuTempC, CpuClockMhz: 4500, CpuPowerW: 95,
            GpuTempC: 70, GpuClockMhz: 1800, GpuPowerW: 200, GpuFanRpm: 1500);
    }

    private static HardwareInventory MakeHardware(int stickCount, int totalSlots)
    {
        var sticks = Enumerable.Range(0, stickCount)
            .Select(i => new RamStick($"BANK{i}", 8L * 1024 * 1024 * 1024, 6000, "DDR5"))
            .ToList();

        return new HardwareInventory(
            "TestCPU", 8, 16, 3600, 5200, 32_000_000,
            sticks, stickCount * 8, totalSlots, totalSlots - stickCount,
            "TestGPU", 8192, "537.58",
            [new DiskDrive("NVMe SSD", "NVMe", 1_000_000_000_000)],
            "TestMobo", "1.0", "2024-01-01", "26100.1", "Windows 11",
            [new NetworkAdapter("Ethernet", 1_000_000_000)],
            "1920x1080", 144);
    }
}
