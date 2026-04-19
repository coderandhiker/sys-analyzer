using SysAnalyzer.Capture;

namespace SysAnalyzer.Tests.Unit;

public class TimeWindowTests
{
    [Fact]
    public void IsWithin_ExactCenter_True()
    {
        var center = new QpcTimestamp(1000);
        var point = new QpcTimestamp(1000);
        Assert.True(TimeWindow.IsWithin(point, center, TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public void IsWithin_InsideWindow_True()
    {
        var center = new QpcTimestamp(1000);
        var halfWidth = TimeSpan.FromSeconds(1);
        long halfWidthTicks = (long)(halfWidth.TotalSeconds * QpcTimestamp.Frequency);
        var point = new QpcTimestamp(1000 + halfWidthTicks / 2);
        Assert.True(TimeWindow.IsWithin(point, center, halfWidth));
    }

    [Fact]
    public void IsWithin_OutsideWindow_False()
    {
        var center = new QpcTimestamp(1000);
        var halfWidth = TimeSpan.FromSeconds(1);
        long halfWidthTicks = (long)(halfWidth.TotalSeconds * QpcTimestamp.Frequency);
        var point = new QpcTimestamp(1000 + halfWidthTicks * 2);
        Assert.False(TimeWindow.IsWithin(point, center, halfWidth));
    }

    [Fact]
    public void IsWithin_AtBoundary_True()
    {
        var center = new QpcTimestamp(1000);
        var halfWidth = TimeSpan.FromSeconds(1);
        long halfWidthTicks = (long)(halfWidth.TotalSeconds * QpcTimestamp.Frequency);
        var point = new QpcTimestamp(1000 + halfWidthTicks);
        Assert.True(TimeWindow.IsWithin(point, center, halfWidth));
    }

    [Fact]
    public void IsWithin_NegativeOffset_InsideWindow_True()
    {
        var center = new QpcTimestamp(10000);
        var halfWidth = TimeSpan.FromSeconds(1);
        long halfWidthTicks = (long)(halfWidth.TotalSeconds * QpcTimestamp.Frequency);
        var point = new QpcTimestamp(10000 - halfWidthTicks / 2);
        Assert.True(TimeWindow.IsWithin(point, center, halfWidth));
    }

    [Fact]
    public void CorrelationWindows_CorrectValues()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(50), CorrelationWindows.FrameSpikeNarrow);
        Assert.Equal(TimeSpan.FromMilliseconds(500), CorrelationWindows.FrameSpikeWide);
        Assert.Equal(TimeSpan.FromSeconds(2), CorrelationWindows.MetricCorrelation);
        Assert.Equal(TimeSpan.FromSeconds(60), CorrelationWindows.TrendWindow);
    }
}
