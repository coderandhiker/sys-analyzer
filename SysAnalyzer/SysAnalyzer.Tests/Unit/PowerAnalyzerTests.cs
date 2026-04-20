using SysAnalyzer.Analysis;
using SysAnalyzer.Capture;

namespace SysAnalyzer.Tests.Unit;

public class PowerAnalyzerTests
{
    private static SensorSnapshot MakeSnapshot(
        double? cpuPowerW = null, double? cpuClockMhz = null,
        double? gpuPowerW = null, double? gpuClockMhz = null,
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
            CpuTempC: null,
            CpuClockMhz: cpuClockMhz,
            CpuPowerW: cpuPowerW,
            GpuTempC: null,
            GpuClockMhz: gpuClockMhz,
            GpuPowerW: gpuPowerW,
            GpuFanRpm: null
        );
    }

    [Fact]
    public void CpuPowerLimit_PowerAtTdpAndClockDrop_ReturnsPositive()
    {
        var snapshots = new List<SensorSnapshot>
        {
            MakeSnapshot(cpuPowerW: 125, cpuClockMhz: 4500), // max clock sample (establishes max)
            MakeSnapshot(cpuPowerW: 123, cpuClockMhz: 3800), // at TDP (>=95% of 125=118.75) + clock drop (<95% of 4500=4275)
            MakeSnapshot(cpuPowerW: 120, cpuClockMhz: 3600), // at TDP + clock drop
            MakeSnapshot(cpuPowerW: 80, cpuClockMhz: 4400),  // not at TDP
        };

        double pct = PowerAnalyzer.ComputeCpuPowerLimitPct(snapshots, cpuTdpWatts: 125);

        Assert.True(pct > 0);
    }

    [Fact]
    public void CpuPowerLimit_PowerBelowTdp_ReturnsZero()
    {
        var snapshots = new List<SensorSnapshot>
        {
            MakeSnapshot(cpuPowerW: 80, cpuClockMhz: 4500),
            MakeSnapshot(cpuPowerW: 90, cpuClockMhz: 4400),
            MakeSnapshot(cpuPowerW: 75, cpuClockMhz: 4300),
        };

        double pct = PowerAnalyzer.ComputeCpuPowerLimitPct(snapshots, cpuTdpWatts: 125);

        Assert.Equal(0, pct);
    }

    [Fact]
    public void GpuPowerLimit_PowerAtTdpAndClockDrop_ReturnsPositive()
    {
        var snapshots = new List<SensorSnapshot>
        {
            MakeSnapshot(gpuPowerW: 300, gpuClockMhz: 1900), // max clock
            MakeSnapshot(gpuPowerW: 295, gpuClockMhz: 1500), // at TDP + clock drop
            MakeSnapshot(gpuPowerW: 290, gpuClockMhz: 1400), // at TDP + clock drop
            MakeSnapshot(gpuPowerW: 200, gpuClockMhz: 1850), // not at TDP
        };

        double pct = PowerAnalyzer.ComputeGpuPowerLimitPct(snapshots, gpuTdpWatts: 300);

        Assert.True(pct > 0);
    }

    [Fact]
    public void PsuAdequacy_LowPowerDraw_NoWarning()
    {
        // CPU 125W + GPU 300W = 425W, estimated total = 525W — fits in 750W tier
        var snapshots = new List<SensorSnapshot>
        {
            MakeSnapshot(cpuPowerW: 125, gpuPowerW: 300),
            MakeSnapshot(cpuPowerW: 120, gpuPowerW: 290),
            MakeSnapshot(cpuPowerW: 115, gpuPowerW: 280),
        };

        var result = PowerAnalyzer.EstimatePsuAdequacy(snapshots, knownPsuWatts: 750);

        Assert.False(result.IsWarning);
    }

    [Fact]
    public void PsuAdequacy_HighPowerDraw_Warning()
    {
        // CPU 125W + GPU 300W = 425W peak, estimated total = 525W — exceeds 80% of 550W tier
        var snapshots = new List<SensorSnapshot>
        {
            MakeSnapshot(cpuPowerW: 125, gpuPowerW: 300),
            MakeSnapshot(cpuPowerW: 120, gpuPowerW: 290),
            MakeSnapshot(cpuPowerW: 130, gpuPowerW: 310), // peak = 440W
        };

        var result = PowerAnalyzer.EstimatePsuAdequacy(snapshots, knownPsuWatts: 550);

        Assert.True(result.IsWarning); // 440 + 100 = 540 > 550 * 0.80 = 440
    }

    [Fact]
    public void PsuAdequacy_NoPowerData_NoWarning()
    {
        var snapshots = new List<SensorSnapshot>
        {
            MakeSnapshot(cpuPowerW: null, gpuPowerW: null),
        };

        var result = PowerAnalyzer.EstimatePsuAdequacy(snapshots);

        Assert.False(result.IsWarning);
        Assert.Equal(0, result.PeakComponentPowerW);
    }

    [Fact]
    public void CpuPowerLimit_EmptySnapshots_ReturnsZero()
    {
        double pct = PowerAnalyzer.ComputeCpuPowerLimitPct([], cpuTdpWatts: 125);
        Assert.Equal(0, pct);
    }

    [Fact]
    public void GpuPowerLimit_EmptySnapshots_ReturnsZero()
    {
        double pct = PowerAnalyzer.ComputeGpuPowerLimitPct([], gpuTdpWatts: 300);
        Assert.Equal(0, pct);
    }

    [Fact]
    public void PsuAdequacy_EmptySnapshots_NoWarning()
    {
        var result = PowerAnalyzer.EstimatePsuAdequacy([]);
        Assert.False(result.IsWarning);
    }
}
