using FluentAssertions;
using SysAnalyzer.Capture;

namespace SysAnalyzer.Tests.Unit;

public class TimeWindowTests
{
    public TimeWindowTests()
    {
        QpcTimestamp.SetCaptureEpoch(0, DateTime.UtcNow);
    }

    [Fact]
    public void IsWithin_ExactlyAtCenter_ReturnsTrue()
    {
        var center = QpcTimestamp.FromMilliseconds(1000);
        var halfWidth = TimeSpan.FromMilliseconds(50);
        TimeWindow.IsWithin(center, center, halfWidth).Should().BeTrue();
    }

    [Fact]
    public void IsWithin_ExactlyAtLeftEdge_ReturnsTrue()
    {
        var center = QpcTimestamp.FromMilliseconds(1000);
        var eventTime = QpcTimestamp.FromMilliseconds(950);
        var halfWidth = TimeSpan.FromMilliseconds(50);
        TimeWindow.IsWithin(eventTime, center, halfWidth).Should().BeTrue();
    }

    [Fact]
    public void IsWithin_ExactlyAtRightEdge_ReturnsTrue()
    {
        var center = QpcTimestamp.FromMilliseconds(1000);
        var eventTime = QpcTimestamp.FromMilliseconds(1050);
        var halfWidth = TimeSpan.FromMilliseconds(50);
        TimeWindow.IsWithin(eventTime, center, halfWidth).Should().BeTrue();
    }

    [Fact]
    public void IsWithin_JustOutsideLeft_ReturnsFalse()
    {
        var center = QpcTimestamp.FromMilliseconds(1000);
        var eventTime = QpcTimestamp.FromMilliseconds(949);
        var halfWidth = TimeSpan.FromMilliseconds(50);
        TimeWindow.IsWithin(eventTime, center, halfWidth).Should().BeFalse();
    }

    [Fact]
    public void IsWithin_JustOutsideRight_ReturnsFalse()
    {
        var center = QpcTimestamp.FromMilliseconds(1000);
        var eventTime = QpcTimestamp.FromMilliseconds(1051);
        var halfWidth = TimeSpan.FromMilliseconds(50);
        TimeWindow.IsWithin(eventTime, center, halfWidth).Should().BeFalse();
    }

    [Fact]
    public void IsWithin_EmptyWindow_OnlyExactMatch()
    {
        var center = QpcTimestamp.FromMilliseconds(1000);
        var halfWidth = TimeSpan.Zero;
        TimeWindow.IsWithin(center, center, halfWidth).Should().BeTrue();
        TimeWindow.IsWithin(QpcTimestamp.FromMilliseconds(1001), center, halfWidth).Should().BeFalse();
    }

    [Fact]
    public void NearestSample_EmptyList_ReturnsInconclusive()
    {
        var items = Array.Empty<(QpcTimestamp ts, int val)>();
        var result = NearestSample.Find(items, x => x.ts, QpcTimestamp.FromMilliseconds(100), TimeSpan.FromMilliseconds(50));
        result.Found.Should().BeFalse();
    }

    [Fact]
    public void NearestSample_ExactMatch_ReturnsIt()
    {
        var items = new[]
        {
            (ts: QpcTimestamp.FromMilliseconds(100), val: 1),
            (ts: QpcTimestamp.FromMilliseconds(200), val: 2),
            (ts: QpcTimestamp.FromMilliseconds(300), val: 3),
        };
        var result = NearestSample.Find(items, x => x.ts, QpcTimestamp.FromMilliseconds(200), TimeSpan.FromMilliseconds(50));
        result.Found.Should().BeTrue();
        result.Value.val.Should().Be(2);
    }

    [Fact]
    public void NearestSample_ClosestWithinWindow_ReturnsNearest()
    {
        var items = new[]
        {
            (ts: QpcTimestamp.FromMilliseconds(100), val: 1),
            (ts: QpcTimestamp.FromMilliseconds(200), val: 2),
            (ts: QpcTimestamp.FromMilliseconds(400), val: 4),
        };
        // Target at 190ms, window ±50ms — should find 200ms
        var result = NearestSample.Find(items, x => x.ts, QpcTimestamp.FromMilliseconds(190), TimeSpan.FromMilliseconds(50));
        result.Found.Should().BeTrue();
        result.Value.val.Should().Be(2);
    }

    [Fact]
    public void NearestSample_GapNoSampleInWindow_ReturnsInconclusive()
    {
        var items = new[]
        {
            (ts: QpcTimestamp.FromMilliseconds(100), val: 1),
            (ts: QpcTimestamp.FromMilliseconds(500), val: 5),
        };
        // Target at 300ms, window ±50ms — nothing in [250, 350]
        var result = NearestSample.Find(items, x => x.ts, QpcTimestamp.FromMilliseconds(300), TimeSpan.FromMilliseconds(50));
        result.Found.Should().BeFalse();
    }

    [Fact]
    public void NearestSample_TwoNeighbors_PicksCloser()
    {
        var items = new[]
        {
            (ts: QpcTimestamp.FromMilliseconds(90), val: 1),
            (ts: QpcTimestamp.FromMilliseconds(120), val: 2),
        };
        // Target at 100ms, window ±50ms — both within window, 90 is 10ms away, 120 is 20ms away
        var result = NearestSample.Find(items, x => x.ts, QpcTimestamp.FromMilliseconds(100), TimeSpan.FromMilliseconds(50));
        result.Found.Should().BeTrue();
        result.Value.val.Should().Be(1);
    }

    [Fact]
    public void CorrelationWindows_HaveCorrectValues()
    {
        CorrelationWindows.FrameSpikeNarrow.Should().Be(TimeSpan.FromMilliseconds(50));
        CorrelationWindows.FrameSpikeWide.Should().Be(TimeSpan.FromMilliseconds(500));
        CorrelationWindows.MetricCorrelation.Should().Be(TimeSpan.FromSeconds(2));
        CorrelationWindows.TrendWindow.Should().Be(TimeSpan.FromSeconds(60));
    }
}
