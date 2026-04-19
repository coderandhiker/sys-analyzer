using System.Diagnostics;
using SysAnalyzer.Capture;

namespace SysAnalyzer.Tests.Unit;

public class TimestampAlignmentTests
{
    public TimestampAlignmentTests()
    {
        // Set a known epoch for deterministic tests
        QpcTimestamp.SetCaptureEpoch(0, DateTime.UtcNow);
    }

    [Fact]
    public void FromPresentMonSeconds_KnownOffset_ConvertsCorrectly()
    {
        long qpcAtLaunch = 5 * Stopwatch.Frequency;
        long captureEpoch = 0;
        long qpcOffset = qpcAtLaunch - captureEpoch;

        var ts = QpcTimestamp.FromPresentMonSeconds(1.0, qpcOffset);

        Assert.InRange(ts.ToMilliseconds(), 5999.0, 6001.0);
    }

    [Fact]
    public void FromPresentMonSeconds_ZeroOffset_EqualsDirectConversion()
    {
        var ts = QpcTimestamp.FromPresentMonSeconds(2.5, 0);
        Assert.InRange(ts.ToMilliseconds(), 2499.0, 2501.0);
    }

    [Fact]
    public void FromPresentMonSeconds_LargeOffset_HandlesCorrectly()
    {
        long qpcOffset = 60 * Stopwatch.Frequency;
        var ts = QpcTimestamp.FromPresentMonSeconds(0.5, qpcOffset);

        Assert.InRange(ts.ToMilliseconds(), 60499.0, 60501.0);
    }

    [Fact]
    public void FromPresentMonSeconds_SecondsBasedInput_CorrectConversion()
    {
        long qpcOffset = 10 * Stopwatch.Frequency;
        double pmTime = 0.016;

        var ts = QpcTimestamp.FromPresentMonSeconds(pmTime, qpcOffset);

        Assert.InRange(ts.ToMilliseconds(), 10015.0, 10017.0);
    }

    [Fact]
    public void FromPresentMonSeconds_RawQpcInput_CanBeConverted()
    {
        long rawQpcFromPm = Stopwatch.Frequency * 3;
        double timeInSeconds = (double)rawQpcFromPm / Stopwatch.Frequency;

        long qpcOffset = 2 * Stopwatch.Frequency;
        var ts = QpcTimestamp.FromPresentMonSeconds(timeInSeconds, qpcOffset);

        Assert.InRange(ts.ToMilliseconds(), 4999.0, 5001.0);
    }

    [Fact]
    public void AlignmentValidation_WithinTolerance_NoIssue()
    {
        long qpcOffset = Stopwatch.Frequency;
        var ts = QpcTimestamp.FromPresentMonSeconds(0.001, qpcOffset);

        var sampleMs = ts.ToMilliseconds();
        Assert.InRange(sampleMs, 1000.0, 1002.0);
    }

    [Fact]
    public void AlignmentValidation_OutsideTolerance_DriftDetectable()
    {
        long badOffset = 10 * Stopwatch.Frequency;
        var ts = QpcTimestamp.FromPresentMonSeconds(0.001, badOffset);

        double actualElapsedMs = 1000.0;
        double sampleMs = ts.ToMilliseconds();
        double drift = Math.Abs(sampleMs - actualElapsedMs);

        Assert.True(drift > 100.0, $"Drift {drift}ms should exceed 100ms tolerance");
    }

    [Fact]
    public void TimestampConversion_RoundTrip_Preserves()
    {
        double inputSeconds = 1.23456;
        long qpcOffset = 0;
        var ts = QpcTimestamp.FromPresentMonSeconds(inputSeconds, qpcOffset);

        Assert.InRange(ts.ToSeconds(), inputSeconds - 0.001, inputSeconds + 0.001);
    }
}
