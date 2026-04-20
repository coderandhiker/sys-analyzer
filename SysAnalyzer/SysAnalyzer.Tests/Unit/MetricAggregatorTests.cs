using SysAnalyzer.Analysis;
using SysAnalyzer.Capture;
using Xunit;

namespace SysAnalyzer.Tests.Unit;

public class MetricAggregatorTests
{
    [Fact]
    public void KnownValues_CorrectMeanAndPercentiles()
    {
        // 1..10
        var values = Enumerable.Range(1, 10).Select(i => (double)i).ToArray();
        var stats = MetricAggregator.AggregateArray(values);

        Assert.Equal(5.5, stats.Mean, 0.001);
        Assert.Equal(1.0, stats.Min);
        Assert.Equal(10.0, stats.Max);
        Assert.Equal(5.5, stats.P50, 0.1);
        Assert.InRange(stats.P95, 9.0, 10.0);
        Assert.InRange(stats.P99, 9.5, 10.0);
    }

    [Fact]
    public void StdDev_KnownValues()
    {
        var values = new double[] { 2, 4, 4, 4, 5, 5, 7, 9 };
        double mean = values.Average();
        double stdDev = MetricAggregator.ComputeStdDev(values, mean);
        Assert.InRange(stdDev, 2.0, 2.2);
    }

    [Fact]
    public void TrendSlope_MonotonicallyIncreasing_PositiveSlopeHighR2()
    {
        var values = Enumerable.Range(0, 100).Select(i => (double)i).ToArray();
        var (slope, r2) = MetricAggregator.ComputeTrendSlope(values);
        Assert.True(slope > 0.9);
        Assert.True(r2 > 0.99);
    }

    [Fact]
    public void TrendSlope_FlatArray_SlopeNearZero()
    {
        var values = Enumerable.Repeat(42.0, 100).ToArray();
        var (slope, r2) = MetricAggregator.ComputeTrendSlope(values);
        Assert.InRange(slope, -0.001, 0.001);
    }

    [Fact]
    public void Percentile_SingleValue()
    {
        var values = new double[] { 7.0 };
        var stats = MetricAggregator.AggregateArray(values);
        Assert.Equal(7.0, stats.P50);
        Assert.Equal(7.0, stats.P95);
        Assert.Equal(7.0, stats.P99);
        Assert.Equal(0.0, stats.StdDev);
    }

    [Fact]
    public void Percentile_TwoValues()
    {
        var sorted = new double[] { 1.0, 10.0 };
        double p50 = MetricAggregator.InterpolatedPercentile(sorted, 0.50);
        Assert.Equal(5.5, p50, 0.001);
    }

    [Fact]
    public void TimeAboveThreshold_Correct()
    {
        var values = new double[] { 10, 20, 30, 80, 90, 95 };
        double pct = MetricAggregator.ComputeTimeAboveThreshold(values, 70);
        Assert.Equal(50.0, pct, 0.1); // 3 of 6 above 70
    }

    [Fact]
    public void Aggregate_EmptySnapshots_ReturnsEmpty()
    {
        var result = MetricAggregator.Aggregate([]);
        Assert.Equal(0, result.SampleCount);
        Assert.True(result.ShortCapture);
    }

    [Fact]
    public void Aggregate_MissingGpu_ReturnsNullGpu()
    {
        var snapshots = CreateSnapshots(5, gpuNull: true);
        var result = MetricAggregator.Aggregate(snapshots);
        Assert.Null(result.Gpu);
    }

    [Fact]
    public void Aggregate_WithGpu_ReturnsGpuMetrics()
    {
        var snapshots = CreateSnapshots(10, gpuNull: false);
        var result = MetricAggregator.Aggregate(snapshots);
        Assert.NotNull(result.Gpu);
        Assert.True(result.Gpu.Load.Mean > 0);
    }

    private static List<SensorSnapshot> CreateSnapshots(int count, bool gpuNull = false)
    {
        var list = new List<SensorSnapshot>();
        for (int i = 0; i < count; i++)
        {
            list.Add(new SensorSnapshot(
                Timestamp: QpcTimestamp.FromMilliseconds(i * 1000),
                TotalCpuPercent: 50 + i,
                PerCoreCpuPercent: [50 + i, 40 + i],
                ContextSwitchesPerSec: 10000 + i * 100,
                DpcTimePercent: 1.0 + i * 0.1,
                InterruptsPerSec: 5000,
                MemoryUtilizationPercent: 60 + i,
                AvailableMemoryMb: 4000 - i * 100,
                PageFaultsPerSec: 100 + i * 10,
                HardFaultsPerSec: 5 + i,
                CommittedBytes: 8_000_000_000.0 + i * 100_000_000,
                CommittedBytesInUsePercent: 50 + i * 2,
                GpuUtilizationPercent: gpuNull ? null : 70 + i,
                GpuMemoryUtilizationPercent: gpuNull ? null : 60 + i,
                GpuMemoryUsedMb: gpuNull ? null : 4000 + i * 50,
                DiskActiveTimePercent: 30 + i * 2,
                DiskQueueLength: 1.0 + i * 0.1,
                DiskBytesPerSec: 50_000_000 + i * 1_000_000,
                DiskReadLatencyMs: 5 + i * 0.5,
                DiskWriteLatencyMs: 3 + i * 0.3,
                NetworkBytesPerSec: 1_000_000 + i * 10000,
                NetworkUtilizationPercent: 10 + i,
                TcpRetransmitsPerSec: 0.1 + i * 0.01,
                CpuTempC: 65 + i,
                CpuClockMhz: 4500 - i * 10,
                CpuPowerW: 95,
                GpuTempC: gpuNull ? null : 70 + i,
                GpuClockMhz: gpuNull ? null : 1800 - i * 5,
                GpuPowerW: gpuNull ? null : 200,
                GpuFanRpm: gpuNull ? null : 1500
            ));
        }
        return list;
    }
}
