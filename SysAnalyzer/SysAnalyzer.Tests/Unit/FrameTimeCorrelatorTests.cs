using SysAnalyzer.Analysis;
using SysAnalyzer.Analysis.Models;
using SysAnalyzer.Capture;
using Xunit;

namespace SysAnalyzer.Tests.Unit;

public class FrameTimeCorrelatorTests
{
    private readonly FrameTimeCorrelator _correlator = new();

    [Fact]
    public void NoPresentMon_ReturnsNull()
    {
        var result = _correlator.Correlate(null, [], null);
        Assert.Null(result);
    }

    [Fact]
    public void EmptySpikes_ReturnsNull()
    {
        var result = _correlator.Correlate([], [], null);
        Assert.Null(result);
    }

    [Fact]
    public void VramAt99_TaggedAsVramOverflow()
    {
        var spike = MakeSpike(5000, cpuMs: 8, gpuMs: 12);
        var snapshot = MakeSnapshot(5000, gpuVramUtilPct: 99.5);

        var result = _correlator.Correlate([spike], [snapshot], null);

        Assert.NotNull(result);
        Assert.Contains(result.TaggedSpikes[0].Tags, t => t.Cause == "vram_overflow");
    }

    [Fact]
    public void MultipleCauses_AllTagsApplied()
    {
        var spike = MakeSpike(5000, cpuMs: 8, gpuMs: 12);
        var snapshot = MakeSnapshot(5000, gpuVramUtilPct: 99.5, diskQueue: 10, commitPct: 95);

        var result = _correlator.Correlate([spike], [snapshot], null);

        Assert.NotNull(result);
        var tags = result.TaggedSpikes[0].Tags;
        Assert.Contains(tags, t => t.Cause == "vram_overflow");
        Assert.Contains(tags, t => t.Cause == "disk_stall");
        Assert.Contains(tags, t => t.Cause == "memory_pressure");
    }

    [Fact]
    public void SpikeWithNoMetricCorrelation_TaggedAsUnknown()
    {
        var spike = MakeSpike(5000, cpuMs: 8, gpuMs: 8); // balanced, no bound
        // Snapshot far away (outside ±2s window)
        var snapshot = MakeSnapshot(100000);

        var result = _correlator.Correlate([spike], [snapshot], null);

        Assert.NotNull(result);
        Assert.Contains(result.TaggedSpikes[0].Tags, t => t.Cause == "unknown");
    }

    [Fact]
    public void CpuBoundFrame_Tagged()
    {
        var spike = MakeSpike(5000, cpuMs: 20, gpuMs: 8);
        var snapshot = MakeSnapshot(5000);

        var result = _correlator.Correlate([spike], [snapshot], null);

        Assert.NotNull(result);
        Assert.Contains(result.TaggedSpikes[0].Tags, t => t.Cause == "cpu_bound");
    }

    [Fact]
    public void GpuBoundFrame_Tagged()
    {
        var spike = MakeSpike(5000, cpuMs: 8, gpuMs: 20);
        var snapshot = MakeSnapshot(5000);

        var result = _correlator.Correlate([spike], [snapshot], null);

        Assert.NotNull(result);
        Assert.Contains(result.TaggedSpikes[0].Tags, t => t.Cause == "gpu_bound");
    }

    [Fact]
    public void PeriodicPattern_DetectedEvery60s()
    {
        // Spikes every 60 seconds
        var spikes = Enumerable.Range(0, 5)
            .Select(i => MakeSpike(i * 60000, cpuMs: 8, gpuMs: 8))
            .ToList();
        var snapshots = Enumerable.Range(0, 5)
            .Select(i => MakeSnapshot(i * 60000))
            .ToList();

        var result = _correlator.Correlate(spikes, snapshots, null);

        Assert.NotNull(result);
        Assert.NotNull(result.PeriodicPattern);
        Assert.True(result.PeriodicPattern.IsScheduled);
        Assert.InRange(result.PeriodicPattern.IntervalSeconds, 55, 65);
    }

    private static FrameTimeSample MakeSpike(double timestampMs, double cpuMs = 8, double gpuMs = 12)
    {
        return new FrameTimeSample(
            QpcTimestamp.FromMilliseconds(timestampMs),
            "TestApp", 33.3, cpuMs, gpuMs, false, "Hardware: Legacy Flip", false);
    }

    private static SensorSnapshot MakeSnapshot(double timestampMs,
        double gpuVramUtilPct = 50, double diskQueue = 1, double commitPct = 50,
        double dpcTimePct = 1)
    {
        return new SensorSnapshot(
            Timestamp: QpcTimestamp.FromMilliseconds(timestampMs),
            TotalCpuPercent: 50,
            PerCoreCpuPercent: [50, 50],
            ContextSwitchesPerSec: 10000,
            DpcTimePercent: dpcTimePct,
            InterruptsPerSec: 5000,
            MemoryUtilizationPercent: 60,
            AvailableMemoryMb: 4000,
            PageFaultsPerSec: 100,
            HardFaultsPerSec: 5,
            CommittedBytes: 8_000_000_000,
            CommittedBytesInUsePercent: commitPct,
            GpuUtilizationPercent: 70,
            GpuMemoryUtilizationPercent: gpuVramUtilPct,
            GpuMemoryUsedMb: 4000,
            DiskActiveTimePercent: 30,
            DiskQueueLength: diskQueue,
            DiskBytesPerSec: 50_000_000,
            DiskReadLatencyMs: 5,
            DiskWriteLatencyMs: 3,
            NetworkBytesPerSec: 1_000_000,
            NetworkUtilizationPercent: 10,
            TcpRetransmitsPerSec: 0.1,
            CpuTempC: 65,
            CpuClockMhz: 4500,
            CpuPowerW: 95,
            GpuTempC: 70,
            GpuClockMhz: 1800,
            GpuPowerW: 200,
            GpuFanRpm: 1500
        );
    }
}
