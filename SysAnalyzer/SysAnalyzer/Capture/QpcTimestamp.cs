using System.Diagnostics;

namespace SysAnalyzer.Capture;

/// <summary>
/// Canonical QPC-based timestamp. All providers normalize to this clock.
/// Stores ticks relative to capture start epoch.
/// </summary>
public readonly struct QpcTimestamp : IEquatable<QpcTimestamp>, IComparable<QpcTimestamp>
{
    public long RawTicks { get; }

    public QpcTimestamp(long rawTicks) => RawTicks = rawTicks;

    /// <summary>QPC frequency (ticks per second). Set once at process start.</summary>
    public static long Frequency { get; } = Stopwatch.Frequency;

    /// <summary>
    /// The QPC value captured at the start of a capture session.
    /// All relative timestamps are computed from this epoch.
    /// </summary>
    public static long CaptureEpoch { get; private set; }

    /// <summary>
    /// Wall-clock anchor captured at the same instant as CaptureEpoch.
    /// Used only for display / file naming.
    /// </summary>
    public static DateTime WallClockAnchor { get; private set; }

    /// <summary>
    /// Initialize the capture epoch. Must be called exactly once per session.
    /// </summary>
    public static void SetCaptureEpoch(long qpcEpoch, DateTime wallClock)
    {
        CaptureEpoch = qpcEpoch;
        WallClockAnchor = wallClock;
    }

    /// <summary>Convert relative ticks to milliseconds.</summary>
    public double ToMilliseconds() => (double)RawTicks / Frequency * 1000.0;

    /// <summary>Convert relative ticks to seconds.</summary>
    public double ToSeconds() => (double)RawTicks / Frequency;

    /// <summary>
    /// Create from an ETW raw QPC timestamp by subtracting the capture epoch.
    /// ETW timestamps share the same QPC clock, so direct subtraction works.
    /// </summary>
    public static QpcTimestamp FromEtwQpc(long rawQpc) => new(rawQpc - CaptureEpoch);

    /// <summary>
    /// Create from PresentMon's TimeInSeconds column.
    /// qpcOffset = our QPC at PM launch - PM's first TimeInSeconds converted to ticks.
    /// </summary>
    public static QpcTimestamp FromPresentMonSeconds(double timeInSeconds, long qpcOffset)
    {
        long ticks = (long)(timeInSeconds * Frequency) + qpcOffset - CaptureEpoch;
        return new QpcTimestamp(ticks);
    }

    /// <summary>Convert to a wall-clock DateTime for display purposes.</summary>
    public DateTime ToWallClock() => WallClockAnchor.AddTicks((long)((double)RawTicks / Frequency * TimeSpan.TicksPerSecond));

    /// <summary>Create from milliseconds (useful for tests).</summary>
    public static QpcTimestamp FromMilliseconds(double ms) => new((long)(ms / 1000.0 * Frequency));

    public static QpcTimestamp operator -(QpcTimestamp a, QpcTimestamp b) => new(a.RawTicks - b.RawTicks);
    public static QpcTimestamp operator +(QpcTimestamp a, QpcTimestamp b) => new(a.RawTicks + b.RawTicks);
    public static bool operator <(QpcTimestamp a, QpcTimestamp b) => a.RawTicks < b.RawTicks;
    public static bool operator >(QpcTimestamp a, QpcTimestamp b) => a.RawTicks > b.RawTicks;
    public static bool operator <=(QpcTimestamp a, QpcTimestamp b) => a.RawTicks <= b.RawTicks;
    public static bool operator >=(QpcTimestamp a, QpcTimestamp b) => a.RawTicks >= b.RawTicks;
    public static bool operator ==(QpcTimestamp a, QpcTimestamp b) => a.RawTicks == b.RawTicks;
    public static bool operator !=(QpcTimestamp a, QpcTimestamp b) => a.RawTicks != b.RawTicks;

    public bool Equals(QpcTimestamp other) => RawTicks == other.RawTicks;
    public override bool Equals(object? obj) => obj is QpcTimestamp other && Equals(other);
    public override int GetHashCode() => RawTicks.GetHashCode();
    public int CompareTo(QpcTimestamp other) => RawTicks.CompareTo(other.RawTicks);
    public override string ToString() => $"{ToMilliseconds():F3}ms";
}
