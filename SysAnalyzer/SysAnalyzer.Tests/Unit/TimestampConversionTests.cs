using FluentAssertions;
using SysAnalyzer.Capture;

namespace SysAnalyzer.Tests.Unit;

public class TimestampConversionTests
{
    public TimestampConversionTests()
    {
        // Reset epoch for each test
        QpcTimestamp.SetCaptureEpoch(0, new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ToMilliseconds_RoundTrips()
    {
        var ts = QpcTimestamp.FromMilliseconds(1234.5);
        ts.ToMilliseconds().Should().BeApproximately(1234.5, 0.01);
    }

    [Fact]
    public void ToSeconds_RoundTrips()
    {
        var ts = QpcTimestamp.FromMilliseconds(5000.0);
        ts.ToSeconds().Should().BeApproximately(5.0, 0.001);
    }

    [Fact]
    public void FromEtwQpc_SubtractsEpoch()
    {
        long epoch = 1_000_000;
        QpcTimestamp.SetCaptureEpoch(epoch, DateTime.UtcNow);

        long rawQpc = 1_500_000;
        var ts = QpcTimestamp.FromEtwQpc(rawQpc);
        ts.RawTicks.Should().Be(500_000);
    }

    [Fact]
    public void FromPresentMonSeconds_AppliesOffset()
    {
        long epoch = 1_000_000;
        QpcTimestamp.SetCaptureEpoch(epoch, DateTime.UtcNow);

        // PresentMon says 2.0 seconds, offset accounts for launch delay
        long qpcOffset = epoch; // offset = our QPC at launch - PM first time in ticks
        var ts = QpcTimestamp.FromPresentMonSeconds(2.0, qpcOffset);

        // Expected: (2.0 * Frequency) + offset - epoch = 2.0 * Frequency
        ts.ToSeconds().Should().BeApproximately(2.0, 0.001);
    }

    [Fact]
    public void ToWallClock_CorrectOffset()
    {
        var anchor = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);
        QpcTimestamp.SetCaptureEpoch(0, anchor);

        var ts = QpcTimestamp.FromMilliseconds(5000.0);
        var wall = ts.ToWallClock();
        wall.Should().BeCloseTo(anchor.AddSeconds(5), TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void Comparison_Operators_Work()
    {
        var a = QpcTimestamp.FromMilliseconds(100);
        var b = QpcTimestamp.FromMilliseconds(200);

        (a < b).Should().BeTrue();
        (b > a).Should().BeTrue();
        a.Equals(a).Should().BeTrue();
        (a != b).Should().BeTrue();
        a.CompareTo(a).Should().Be(0);
        (b >= a).Should().BeTrue();
    }

    [Fact]
    public void Subtraction_Works()
    {
        var a = QpcTimestamp.FromMilliseconds(500);
        var b = QpcTimestamp.FromMilliseconds(200);
        var diff = a - b;
        diff.ToMilliseconds().Should().BeApproximately(300.0, 0.1);
    }

    [Fact]
    public void Zero_Ticks_Is_Zero_Ms()
    {
        var ts = new QpcTimestamp(0);
        ts.ToMilliseconds().Should().Be(0.0);
        ts.ToSeconds().Should().Be(0.0);
    }
}
