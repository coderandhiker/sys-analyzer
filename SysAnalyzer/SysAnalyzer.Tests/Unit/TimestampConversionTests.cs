using SysAnalyzer.Capture;

namespace SysAnalyzer.Tests.Unit;

public class TimestampConversionTests
{
    [Fact]
    public void QpcTimestamp_ToMilliseconds_Correct()
    {
        // 1 second worth of ticks → 1000ms
        var ts = new QpcTimestamp(QpcTimestamp.Frequency);
        Assert.Equal(1000.0, ts.ToMilliseconds(), precision: 1);
    }

    [Fact]
    public void QpcTimestamp_ToSeconds_Correct()
    {
        var ts = new QpcTimestamp(QpcTimestamp.Frequency * 5);
        Assert.Equal(5.0, ts.ToSeconds(), precision: 3);
    }

    [Fact]
    public void QpcTimestamp_FromEtwQpc_SubtractsEpoch()
    {
        QpcTimestamp.SetCaptureEpoch(1000, DateTime.UtcNow);
        var ts = QpcTimestamp.FromEtwQpc(1500);
        Assert.Equal(500, ts.RawTicks);
    }

    [Fact]
    public void QpcTimestamp_FromPresentMonSeconds_Converts()
    {
        QpcTimestamp.SetCaptureEpoch(0, DateTime.UtcNow);
        var ts = QpcTimestamp.FromPresentMonSeconds(1.0, 0);
        Assert.Equal(QpcTimestamp.Frequency, ts.RawTicks);
    }

    [Fact]
    public void QpcTimestamp_Equality()
    {
        var ts1 = new QpcTimestamp(100);
        var ts2 = new QpcTimestamp(100);
        Assert.Equal(ts1, ts2);
        Assert.True(ts1.Equals(ts2));
    }

    [Fact]
    public void QpcTimestamp_Comparison()
    {
        var ts1 = new QpcTimestamp(100);
        var ts2 = new QpcTimestamp(200);
        Assert.True(ts1.CompareTo(ts2) < 0);
        Assert.True(ts2.CompareTo(ts1) > 0);
    }

    [Fact]
    public void QpcTimestamp_Frequency_IsPositive()
    {
        Assert.True(QpcTimestamp.Frequency > 0);
    }

    [Fact]
    public void QpcTimestamp_ZeroTicks_ZeroMs()
    {
        var ts = new QpcTimestamp(0);
        Assert.Equal(0.0, ts.ToMilliseconds());
    }
}
