using SysAnalyzer.Analysis;
using SysAnalyzer.Capture;

namespace SysAnalyzer.Tests.Unit;

public class FrameTimeAggregationTests
{
    private static FrameTimeSample MakeSample(double frameTimeMs, double cpuMs = 8.0, double gpuMs = 12.0,
        bool dropped = false, string presentMode = "Hardware: Independent Flip", bool tearing = false)
    {
        return new FrameTimeSample(
            QpcTimestamp.FromMilliseconds(0),
            "TestApp.exe",
            frameTimeMs, cpuMs, gpuMs, dropped, presentMode, tearing);
    }

    [Fact]
    public void Steady60Fps_CorrectAverageAndPercentiles()
    {
        var samples = Enumerable.Range(0, 100)
            .Select(_ => MakeSample(16.67))
            .ToList();

        var result = FrameTimeAggregator.Compute(samples, "TestApp.exe");

        Assert.NotNull(result);
        Assert.InRange(result.AvgFps, 59.5, 60.5);
        Assert.InRange(result.P50FrameTimeMs, 16.66, 16.68);
        Assert.InRange(result.P99FrameTimeMs, 16.66, 16.68);
        Assert.Equal(0, result.StutterCount);
        Assert.Equal("TestApp.exe", result.TrackedApplication);
        Assert.True(result.Available);
    }

    [Fact]
    public void SteadyWithSpikes_CorrectP99AndStutterCount()
    {
        var samples = Enumerable.Range(0, 95)
            .Select(_ => MakeSample(16.67))
            .Concat(Enumerable.Range(0, 5).Select(_ => MakeSample(100.0)))
            .ToList();

        var result = FrameTimeAggregator.Compute(samples, "TestApp.exe", stutterSpikeMultiplier: 2.0);

        Assert.NotNull(result);
        Assert.True(result.P99FrameTimeMs > 50.0, $"P99 was {result.P99FrameTimeMs}, expected >50");
        Assert.Equal(5, result.StutterCount);
    }

    [Fact]
    public void MixedCpuAndGpuBound_CorrectPercentages()
    {
        var cpuBound = Enumerable.Range(0, 50)
            .Select(_ => MakeSample(16.67, cpuMs: 20.0, gpuMs: 8.0));
        var gpuBound = Enumerable.Range(0, 50)
            .Select(_ => MakeSample(16.67, cpuMs: 5.0, gpuMs: 15.0));

        var samples = cpuBound.Concat(gpuBound).ToList();

        var result = FrameTimeAggregator.Compute(samples, "TestApp.exe", cpuBoundRatio: 1.5);

        Assert.NotNull(result);
        Assert.InRange(result.CpuBoundPct, 49.0, 51.0);
        Assert.InRange(result.GpuBoundPct, 49.0, 51.0);
    }

    [Fact]
    public void AllFramesDropped_100PercentDroppedRate()
    {
        var samples = Enumerable.Range(0, 20)
            .Select(_ => MakeSample(16.67, dropped: true))
            .ToList();

        var result = FrameTimeAggregator.Compute(samples, "TestApp.exe");

        Assert.NotNull(result);
        Assert.InRange(result.DroppedFramePct, 99.99, 100.01);
    }

    [Fact]
    public void NoFramesDropped_ZeroDroppedRate()
    {
        var samples = Enumerable.Range(0, 50)
            .Select(_ => MakeSample(16.67, dropped: false))
            .ToList();

        var result = FrameTimeAggregator.Compute(samples, "TestApp.exe");

        Assert.NotNull(result);
        Assert.Equal(0.0, result.DroppedFramePct);
    }

    [Fact]
    public void MultiplePresentModes_MostCommonSelected()
    {
        var hwFlip = Enumerable.Range(0, 60)
            .Select(_ => MakeSample(16.67, presentMode: "Hardware: Independent Flip"));
        var composed = Enumerable.Range(0, 40)
            .Select(_ => MakeSample(16.67, presentMode: "Composed: Flip"));

        var samples = hwFlip.Concat(composed).ToList();
        var result = FrameTimeAggregator.Compute(samples, "TestApp.exe");

        Assert.NotNull(result);
        Assert.Equal("Hardware: Independent Flip", result.PresentMode);
    }

    [Fact]
    public void TearingDetected_AllowsTearingTrue()
    {
        var samples = Enumerable.Range(0, 10)
            .Select(i => MakeSample(16.67, tearing: i == 5))
            .ToList();

        var result = FrameTimeAggregator.Compute(samples, "TestApp.exe");

        Assert.NotNull(result);
        Assert.True(result.AllowsTearing);
    }

    [Fact]
    public void NoTearing_AllowsTearingFalse()
    {
        var samples = Enumerable.Range(0, 10)
            .Select(_ => MakeSample(16.67, tearing: false))
            .ToList();

        var result = FrameTimeAggregator.Compute(samples, "TestApp.exe");

        Assert.NotNull(result);
        Assert.False(result.AllowsTearing);
    }

    [Fact]
    public void EmptySamples_ReturnsNull()
    {
        var result = FrameTimeAggregator.Compute([], "TestApp.exe");
        Assert.Null(result);
    }

    [Fact]
    public void P1Fps_CalculatedFromP99FrameTime()
    {
        var samples = Enumerable.Range(0, 100)
            .Select(_ => MakeSample(16.67))
            .ToList();

        var result = FrameTimeAggregator.Compute(samples, "TestApp.exe");

        Assert.NotNull(result);
        Assert.InRange(result.P1Fps, 59.5, 60.5);
    }

    [Fact]
    public void HighFrameRate_120Fps_CorrectStats()
    {
        var samples = Enumerable.Range(0, 200)
            .Select(_ => MakeSample(8.33))
            .ToList();

        var result = FrameTimeAggregator.Compute(samples, "TestApp.exe");

        Assert.NotNull(result);
        Assert.InRange(result.AvgFps, 119.0, 121.0);
    }

    [Fact]
    public void InterpolatedPercentile_CorrectValues()
    {
        double[] sorted = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

        var p50 = FrameTimeAggregator.InterpolatedPercentile(sorted, 0.50);
        var p90 = FrameTimeAggregator.InterpolatedPercentile(sorted, 0.90);
        var p0 = FrameTimeAggregator.InterpolatedPercentile(sorted, 0.0);
        var p100 = FrameTimeAggregator.InterpolatedPercentile(sorted, 1.0);

        Assert.InRange(p50, 5.49, 5.51);
        Assert.InRange(p90, 9.09, 9.11);
        Assert.Equal(1.0, p0);
        Assert.Equal(10.0, p100);
    }

    [Fact]
    public void NotesPassedThrough()
    {
        var samples = Enumerable.Range(0, 10)
            .Select(_ => MakeSample(16.67))
            .ToList();
        var notes = new List<string> { "PresentMon crashed at 12:00:00" };

        var result = FrameTimeAggregator.Compute(samples, "TestApp.exe", notes: notes);

        Assert.NotNull(result);
        Assert.NotNull(result.Notes);
        Assert.Contains("PresentMon crashed at 12:00:00", result.Notes);
    }
}
