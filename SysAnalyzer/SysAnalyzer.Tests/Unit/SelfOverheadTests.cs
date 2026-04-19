using SysAnalyzer.Capture;

namespace SysAnalyzer.Tests.Unit;

public class SelfOverheadTests
{
    [Fact]
    public void Finish_ReturnsValidOverhead()
    {
        var tracker = new SelfOverheadTracker();
        tracker.Start();
        tracker.Sample();
        tracker.Sample();
        var result = tracker.Finish();

        Assert.True(result.AvgCpuPercent >= 0);
        Assert.True(result.PeakWorkingSetBytes > 0);
        Assert.True(result.GcCollections >= 0);
        Assert.True(result.GcPauseTimeMs >= 0);
        Assert.Equal(0, result.EtwEventsLost);
    }

    [Fact]
    public void Finish_WithNoSamples_ZeroCpu()
    {
        var tracker = new SelfOverheadTracker();
        tracker.Start();
        var result = tracker.Finish();

        Assert.Equal(0, result.AvgCpuPercent);
    }

    [Fact]
    public void Sample_DoesNotThrow()
    {
        var tracker = new SelfOverheadTracker();
        tracker.Start();

        var ex = Record.Exception(() =>
        {
            for (int i = 0; i < 10; i++)
                tracker.Sample();
        });

        Assert.Null(ex);
    }
}
