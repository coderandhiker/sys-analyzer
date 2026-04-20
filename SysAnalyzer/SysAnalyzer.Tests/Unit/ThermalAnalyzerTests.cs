using SysAnalyzer.Analysis;
using SysAnalyzer.Capture;

namespace SysAnalyzer.Tests.Unit;

public class ThermalAnalyzerTests
{
    private static SensorSnapshot MakeSnapshot(
        double? cpuTempC = null, double? cpuClockMhz = null,
        double? gpuTempC = null, double? gpuClockMhz = null,
        long ticks = 0)
    {
        return new SensorSnapshot(
            Timestamp: new QpcTimestamp(ticks),
            TotalCpuPercent: 50,
            PerCoreCpuPercent: [],
            ContextSwitchesPerSec: 0,
            DpcTimePercent: 0,
            InterruptsPerSec: 0,
            MemoryUtilizationPercent: 50,
            AvailableMemoryMb: 8000,
            PageFaultsPerSec: 0,
            HardFaultsPerSec: 0,
            CommittedBytes: 0,
            CommittedBytesInUsePercent: 0,
            GpuUtilizationPercent: 50,
            GpuMemoryUtilizationPercent: 50,
            GpuMemoryUsedMb: 2000,
            DiskActiveTimePercent: 10,
            DiskQueueLength: 0,
            DiskBytesPerSec: 0,
            DiskReadLatencyMs: 0,
            DiskWriteLatencyMs: 0,
            NetworkBytesPerSec: 0,
            NetworkUtilizationPercent: 0,
            TcpRetransmitsPerSec: 0,
            CpuTempC: cpuTempC,
            CpuClockMhz: cpuClockMhz,
            CpuPowerW: null,
            GpuTempC: gpuTempC,
            GpuClockMhz: gpuClockMhz,
            GpuPowerW: null,
            GpuFanRpm: null
        );
    }

    [Fact]
    public void CpuThermalThrottle_TempAboveWarningAndClockBelowBase_ReturnsPositive()
    {
        var snapshots = new List<SensorSnapshot>
        {
            MakeSnapshot(cpuTempC: 90, cpuClockMhz: 3200), // throttled
            MakeSnapshot(cpuTempC: 92, cpuClockMhz: 3100), // throttled
            MakeSnapshot(cpuTempC: 88, cpuClockMhz: 3800), // not throttled (clock ok)
            MakeSnapshot(cpuTempC: 70, cpuClockMhz: 3000), // not throttled (temp ok)
        };

        double pct = ThermalAnalyzer.ComputeCpuThermalThrottlePct(snapshots, tempWarning: 85, baseClockMhz: 3500);

        Assert.True(pct > 0);
        Assert.Equal(50.0, pct); // 2 out of 4
    }

    [Fact]
    public void CpuThermalThrottle_TempBelowWarning_ReturnsZero()
    {
        var snapshots = new List<SensorSnapshot>
        {
            MakeSnapshot(cpuTempC: 70, cpuClockMhz: 4000),
            MakeSnapshot(cpuTempC: 75, cpuClockMhz: 4200),
            MakeSnapshot(cpuTempC: 80, cpuClockMhz: 3900),
        };

        double pct = ThermalAnalyzer.ComputeCpuThermalThrottlePct(snapshots, tempWarning: 85, baseClockMhz: 3500);

        Assert.Equal(0, pct);
    }

    [Fact]
    public void GpuThermalThrottle_TempAboveWarningAndClockBelowBase_ReturnsPositive()
    {
        var snapshots = new List<SensorSnapshot>
        {
            MakeSnapshot(gpuTempC: 85, gpuClockMhz: 1400), // throttled
            MakeSnapshot(gpuTempC: 87, gpuClockMhz: 1300), // throttled
            MakeSnapshot(gpuTempC: 70, gpuClockMhz: 1900), // not throttled
        };

        double pct = ThermalAnalyzer.ComputeGpuThermalThrottlePct(snapshots, tempWarning: 80, baseClockMhz: 1500);

        Assert.True(pct > 0);
    }

    [Fact]
    public void GpuThermalThrottle_TempBelowWarning_ReturnsZero()
    {
        var snapshots = new List<SensorSnapshot>
        {
            MakeSnapshot(gpuTempC: 65, gpuClockMhz: 1900),
            MakeSnapshot(gpuTempC: 70, gpuClockMhz: 1850),
        };

        double pct = ThermalAnalyzer.ComputeGpuThermalThrottlePct(snapshots, tempWarning: 80, baseClockMhz: 1500);

        Assert.Equal(0, pct);
    }

    [Fact]
    public void ClockDropPct_ClocksBelow90PercentOfMax_ReturnsPositive()
    {
        var snapshots = new List<SensorSnapshot>
        {
            MakeSnapshot(cpuClockMhz: 4500), // max
            MakeSnapshot(cpuClockMhz: 4400), // ok (> 90% of 4500 = 4050)
            MakeSnapshot(cpuClockMhz: 3500), // dropped
            MakeSnapshot(cpuClockMhz: 3000), // dropped
        };

        double pct = ThermalAnalyzer.ComputeClockDropPct(snapshots, isCpu: true);

        Assert.Equal(50.0, pct); // 2 out of 4
    }

    [Fact]
    public void ClockDropPct_AllClocksNearMax_ReturnsZero()
    {
        var snapshots = new List<SensorSnapshot>
        {
            MakeSnapshot(cpuClockMhz: 4500),
            MakeSnapshot(cpuClockMhz: 4400),
            MakeSnapshot(cpuClockMhz: 4300),
            MakeSnapshot(cpuClockMhz: 4200), // 4200/4500 = 93.3% > 90%
        };

        double pct = ThermalAnalyzer.ComputeClockDropPct(snapshots, isCpu: true);

        Assert.Equal(0, pct);
    }

    [Fact]
    public void ClockDropPct_NoClockData_ReturnsZero()
    {
        var snapshots = new List<SensorSnapshot>
        {
            MakeSnapshot(cpuClockMhz: null),
            MakeSnapshot(cpuClockMhz: null),
        };

        double pct = ThermalAnalyzer.ComputeClockDropPct(snapshots, isCpu: true);

        Assert.Equal(0, pct);
    }

    [Fact]
    public void ThermalSoak_ShortCapture_NotDetected()
    {
        // Less than 15 minutes worth of data
        var snapshots = Enumerable.Range(0, 100)
            .Select(i => MakeSnapshot(cpuTempC: 60 + i * 0.3, ticks: i * 10_000_000L))
            .ToList();

        var result = ThermalAnalyzer.DetectThermalSoak(snapshots, minDurationMinutes: 15);

        Assert.False(result.IsSoaking);
    }

    [Fact]
    public void ThermalSoak_LongCaptureRisingTemp_Detected()
    {
        // Simulate 20 minutes (1200 seconds) with steadily rising temp
        long ticksPerSecond = System.Diagnostics.Stopwatch.Frequency;
        var snapshots = Enumerable.Range(0, 1200)
            .Select(i => MakeSnapshot(cpuTempC: 60 + i * 0.02, ticks: i * ticksPerSecond))
            .ToList();

        QpcTimestamp.SetCaptureEpoch(0, DateTime.UtcNow);

        var result = ThermalAnalyzer.DetectThermalSoak(snapshots, minDurationMinutes: 15);

        Assert.True(result.IsSoaking);
        Assert.True(result.Slope > 0);
        Assert.True(result.RSquared > 0.8);
    }

    [Fact]
    public void EmptySnapshots_ReturnsZero()
    {
        double pct = ThermalAnalyzer.ComputeCpuThermalThrottlePct([], tempWarning: 85, baseClockMhz: 3500);
        Assert.Equal(0, pct);
    }
}
