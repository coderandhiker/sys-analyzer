using SysAnalyzer.Capture;

namespace SysAnalyzer.Tests.Unit;

public class CorrelationWindowTests
{
    public CorrelationWindowTests()
    {
        QpcTimestamp.SetCaptureEpoch(0, DateTime.UtcNow);
    }

    [Fact]
    public void FrameSpikeNarrow_Is50ms()
    {
        Assert.Equal(50, CorrelationWindows.FrameSpikeNarrow.TotalMilliseconds);
    }

    [Fact]
    public void FrameSpikeWide_Is500ms()
    {
        Assert.Equal(500, CorrelationWindows.FrameSpikeWide.TotalMilliseconds);
    }

    [Fact]
    public void MetricCorrelation_Is2s()
    {
        Assert.Equal(2000, CorrelationWindows.MetricCorrelation.TotalMilliseconds);
    }

    [Fact]
    public void ContextSwitch_Within50ms_IsCorrelated()
    {
        var spikeTime = QpcTimestamp.FromMilliseconds(1000);
        var eventTime = QpcTimestamp.FromMilliseconds(1040); // +40ms

        Assert.True(TimeWindow.IsWithin(eventTime, spikeTime, CorrelationWindows.FrameSpikeNarrow));
    }

    [Fact]
    public void ContextSwitch_Beyond50ms_NotCorrelated()
    {
        var spikeTime = QpcTimestamp.FromMilliseconds(1000);
        var eventTime = QpcTimestamp.FromMilliseconds(1060); // +60ms

        Assert.False(TimeWindow.IsWithin(eventTime, spikeTime, CorrelationWindows.FrameSpikeNarrow));
    }

    [Fact]
    public void DpcEvent_Within500ms_IsCorrelated()
    {
        var spikeTime = QpcTimestamp.FromMilliseconds(1000);
        var eventTime = QpcTimestamp.FromMilliseconds(600); // -400ms

        Assert.True(TimeWindow.IsWithin(eventTime, spikeTime, CorrelationWindows.FrameSpikeWide));
    }

    [Fact]
    public void DpcEvent_Beyond500ms_NotCorrelated()
    {
        var spikeTime = QpcTimestamp.FromMilliseconds(1000);
        var eventTime = QpcTimestamp.FromMilliseconds(400); // -600ms

        Assert.False(TimeWindow.IsWithin(eventTime, spikeTime, CorrelationWindows.FrameSpikeWide));
    }

    [Fact]
    public void DiskIo_Within2s_IsCorrelated()
    {
        var spikeTime = QpcTimestamp.FromMilliseconds(5000);
        var eventTime = QpcTimestamp.FromMilliseconds(3500); // -1500ms

        Assert.True(TimeWindow.IsWithin(eventTime, spikeTime, CorrelationWindows.MetricCorrelation));
    }

    [Fact]
    public void DiskIo_Beyond2s_NotCorrelated()
    {
        var spikeTime = QpcTimestamp.FromMilliseconds(5000);
        var eventTime = QpcTimestamp.FromMilliseconds(2500); // -2500ms

        Assert.False(TimeWindow.IsWithin(eventTime, spikeTime, CorrelationWindows.MetricCorrelation));
    }

    [Fact]
    public void ExactBoundary_Narrow_IsIncluded()
    {
        var spikeTime = QpcTimestamp.FromMilliseconds(1000);
        var eventTime = QpcTimestamp.FromMilliseconds(1050); // exactly +50ms

        Assert.True(TimeWindow.IsWithin(eventTime, spikeTime, CorrelationWindows.FrameSpikeNarrow));
    }

    [Fact]
    public void NegativeBoundary_Narrow_IsIncluded()
    {
        var spikeTime = QpcTimestamp.FromMilliseconds(1000);
        var eventTime = QpcTimestamp.FromMilliseconds(950); // exactly -50ms

        Assert.True(TimeWindow.IsWithin(eventTime, spikeTime, CorrelationWindows.FrameSpikeNarrow));
    }

    [Fact]
    public void EtwTimestamp_AlignedWithPresentMonSpike()
    {
        // Simulate a frame spike at T=120.5s
        var spikeTs = QpcTimestamp.FromMilliseconds(120500);
        // ETW context switch at T=120.48s (within ±50ms)
        var etwTs = QpcTimestamp.FromMilliseconds(120480);

        Assert.True(TimeWindow.IsWithin(etwTs, spikeTs, CorrelationWindows.FrameSpikeNarrow));
    }
}
