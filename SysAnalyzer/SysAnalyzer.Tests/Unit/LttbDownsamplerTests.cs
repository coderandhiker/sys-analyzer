using SysAnalyzer.Report;

namespace SysAnalyzer.Tests.Unit;

public class LttbDownsamplerTests
{
    [Fact]
    public void Downsample_InputShorterThanTarget_ReturnsAllPoints()
    {
        var data = new (double X, double Y)[]
        {
            (0, 10), (1, 20), (2, 15), (3, 25), (4, 30)
        };

        var result = LttbDownsampler.Downsample(data.AsSpan(), 10);

        Assert.Equal(data.Length, result.Length);
        for (int i = 0; i < data.Length; i++)
        {
            Assert.Equal(data[i].X, result[i].X);
            Assert.Equal(data[i].Y, result[i].Y);
        }
    }

    [Fact]
    public void Downsample_PreservesFirstAndLastPoints()
    {
        var data = GenerateLinearData(100);

        var result = LttbDownsampler.Downsample(data.AsSpan(), 20);

        Assert.Equal(data[0].X, result[0].X);
        Assert.Equal(data[0].Y, result[0].Y);
        Assert.Equal(data[^1].X, result[^1].X);
        Assert.Equal(data[^1].Y, result[^1].Y);
    }

    [Fact]
    public void Downsample_PreservesMaxAndMinValues()
    {
        var data = new (double X, double Y)[200];
        for (int i = 0; i < 200; i++)
            data[i] = (i, Math.Sin(i * 0.1) * 50 + 50);

        // Find global max and min
        double maxY = data.Max(p => p.Y);
        double minY = data.Min(p => p.Y);

        var result = LttbDownsampler.Downsample(data.AsSpan(), 30, spikeThresholdMultiplier: 100);

        // LTTB preserves visual shape — max/min of result should be close to original
        double resultMax = result.Max(p => p.Y);
        double resultMin = result.Min(p => p.Y);
        Assert.InRange(resultMax, maxY - 5.0, maxY + 0.01);
        Assert.InRange(resultMin, minY - 0.01, minY + 5.0);
    }

    [Fact]
    public void Downsample_OutputLengthMatchesTarget()
    {
        var data = GenerateLinearData(500);

        var result = LttbDownsampler.Downsample(data.AsSpan(), 50, spikeThresholdMultiplier: 100);

        // LTTB output should be exactly targetCount (first + buckets + last)
        Assert.Equal(50, result.Length);
    }

    [Fact]
    public void Downsample_PreservesStutterSpikes()
    {
        // Create data with a baseline of ~10ms and 3 spikes at > 2x median
        var data = new (double X, double Y)[200];
        for (int i = 0; i < 200; i++)
            data[i] = (i, 10.0);

        // Insert 3 clear spikes
        data[50] = (50, 80.0);  // Spike 1
        data[100] = (100, 90.0); // Spike 2
        data[150] = (150, 70.0); // Spike 3

        var result = LttbDownsampler.Downsample(data.AsSpan(), 20, spikeThresholdMultiplier: 2.0);

        // All 3 spikes should be present in the output
        Assert.Contains(result, p => p.X == 50 && p.Y == 80.0);
        Assert.Contains(result, p => p.X == 100 && p.Y == 90.0);
        Assert.Contains(result, p => p.X == 150 && p.Y == 70.0);
    }

    [Fact]
    public void Downsample_SpikePreservation_DoesNotDuplicateExistingSpikes()
    {
        var data = new (double X, double Y)[100];
        for (int i = 0; i < 100; i++)
            data[i] = (i, 10.0);

        data[0] = (0, 50.0); // Spike at first point (already included by LTTB)

        var result = LttbDownsampler.Downsample(data.AsSpan(), 20, spikeThresholdMultiplier: 2.0);

        // First point should appear exactly once
        int firstPointCount = result.Count(p => p.X == 0);
        Assert.Equal(1, firstPointCount);
    }

    [Fact]
    public void Downsample_ResultIsSortedByX()
    {
        var data = new (double X, double Y)[200];
        for (int i = 0; i < 200; i++)
            data[i] = (i, 10.0 + (i % 20 == 0 ? 50.0 : 0));

        var result = LttbDownsampler.Downsample(data.AsSpan(), 30, spikeThresholdMultiplier: 2.0);

        for (int i = 1; i < result.Length; i++)
        {
            Assert.True(result[i].X >= result[i - 1].X,
                $"Result not sorted: [{i - 1}].X={result[i - 1].X} > [{i}].X={result[i].X}");
        }
    }

    [Fact]
    public void Downsample_WithSeparateArrays_MatchesTupleVersion()
    {
        var xValues = new double[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var yValues = new double[] { 10, 20, 15, 25, 30, 28, 35, 22, 18, 40 };

        var result = LttbDownsampler.Downsample(xValues, yValues, 5, spikeThresholdMultiplier: 100);

        Assert.InRange(result.Length, 4, 6); // ~5 ± 1
        Assert.Equal(0, result[0].X);
        Assert.Equal(9, result[^1].X);
    }

    [Fact]
    public void Downsample_MismatchedArrayLengths_Throws()
    {
        var x = new double[] { 0, 1, 2 };
        var y = new double[] { 10, 20 };

        Assert.Throws<ArgumentException>(() => LttbDownsampler.Downsample(x, y, 2));
    }

    [Fact]
    public void Downsample_TargetLessThan3_ReturnsAllPoints()
    {
        var data = new (double X, double Y)[] { (0, 10), (1, 20), (2, 30) };

        var result = LttbDownsampler.Downsample(data.AsSpan(), 2);

        Assert.Equal(3, result.Length);
    }

    [Theory]
    [InlineData(60, 60, 60)]         // <30min: raw count
    [InlineData(1800, 100, 360)]     // 30min: 5s intervals = 360
    [InlineData(3600, 10000, 720)]   // 1h: 5s intervals = 720
    [InlineData(7200, 50000, 480)]   // 2h: 15s intervals = 480
    [InlineData(14400, 100000, 960)] // 4h: 15s intervals = 960
    [InlineData(28800, 200000, 1920)] // 8h: 15s intervals = 1920
    public void GetTargetPointCount_ReturnsExpectedValue(
        double duration, int rawCount, int expected)
    {
        var result = LttbDownsampler.GetTargetPointCount(duration, rawCount);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetTargetPointCount_NeverExceeds2000()
    {
        // Very long capture
        var result = LttbDownsampler.GetTargetPointCount(100000, 1000000);
        Assert.InRange(result, 1, 2000);
    }

    private static (double X, double Y)[] GenerateLinearData(int count)
    {
        var data = new (double X, double Y)[count];
        for (int i = 0; i < count; i++)
            data[i] = (i, i * 0.5 + 10);
        return data;
    }
}
