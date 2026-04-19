namespace SysAnalyzer.Capture;

/// <summary>
/// Named correlation windows per §3.3.
/// Different analysis tasks use different window half-widths.
/// </summary>
public static class CorrelationWindows
{
    /// <summary>±50ms — ETW context switch → frame spike attribution.</summary>
    public static readonly TimeSpan FrameSpikeNarrow = TimeSpan.FromMilliseconds(50);

    /// <summary>±500ms — System metric → frame spike correlation.</summary>
    public static readonly TimeSpan FrameSpikeWide = TimeSpan.FromMilliseconds(500);

    /// <summary>±2s — Cross-metric correlation (e.g., VRAM full → disk burst).</summary>
    public static readonly TimeSpan MetricCorrelation = TimeSpan.FromSeconds(2);

    /// <summary>60s sliding — Thermal soak, memory leak regression.</summary>
    public static readonly TimeSpan TrendWindow = TimeSpan.FromSeconds(60);
}

/// <summary>
/// Checks whether a timestamp falls within a window centered on another timestamp.
/// </summary>
public static class TimeWindow
{
    /// <summary>
    /// Returns true if eventTime is within [center - halfWidth, center + halfWidth].
    /// </summary>
    public static bool IsWithin(QpcTimestamp eventTime, QpcTimestamp center, TimeSpan halfWidth)
    {
        long halfWidthTicks = (long)(halfWidth.TotalSeconds * QpcTimestamp.Frequency);
        long delta = eventTime.RawTicks - center.RawTicks;
        return delta >= -halfWidthTicks && delta <= halfWidthTicks;
    }
}

/// <summary>
/// Result of a nearest-sample lookup.
/// </summary>
public readonly struct NearestSampleResult<T>
{
    public T? Value { get; }
    public bool Found { get; }

    private NearestSampleResult(T value)
    {
        Value = value;
        Found = true;
    }

    private NearestSampleResult(bool _)
    {
        Value = default;
        Found = false;
    }

    public static NearestSampleResult<T> Of(T value) => new(value);
    public static NearestSampleResult<T> Inconclusive => new(false);
}

/// <summary>
/// Finds the nearest timestamped sample to a target time within a window.
/// </summary>
public static class NearestSample
{
    /// <summary>
    /// Given a sorted list of timestamped items, find the nearest one within halfWidth of target.
    /// Returns Inconclusive if no sample exists in the window.
    /// </summary>
    public static NearestSampleResult<T> Find<T>(
        IReadOnlyList<T> sortedItems,
        Func<T, QpcTimestamp> timestampSelector,
        QpcTimestamp target,
        TimeSpan halfWidth)
    {
        if (sortedItems.Count == 0)
            return NearestSampleResult<T>.Inconclusive;

        long halfWidthTicks = (long)(halfWidth.TotalSeconds * QpcTimestamp.Frequency);
        long targetTicks = target.RawTicks;

        // Binary search for the insertion point
        int lo = 0, hi = sortedItems.Count - 1;
        while (lo <= hi)
        {
            int mid = lo + (hi - lo) / 2;
            long midTicks = timestampSelector(sortedItems[mid]).RawTicks;
            if (midTicks < targetTicks)
                lo = mid + 1;
            else if (midTicks > targetTicks)
                hi = mid - 1;
            else
            {
                // Exact match
                return NearestSampleResult<T>.Of(sortedItems[mid]);
            }
        }

        // Check neighbors: lo is the insertion point
        T? best = default;
        long bestDist = long.MaxValue;
        bool found = false;

        if (lo < sortedItems.Count)
        {
            long dist = Math.Abs(timestampSelector(sortedItems[lo]).RawTicks - targetTicks);
            if (dist <= halfWidthTicks)
            {
                best = sortedItems[lo];
                bestDist = dist;
                found = true;
            }
        }

        if (lo > 0)
        {
            long dist = Math.Abs(timestampSelector(sortedItems[lo - 1]).RawTicks - targetTicks);
            if (dist <= halfWidthTicks && dist < bestDist)
            {
                best = sortedItems[lo - 1];
                found = true;
            }
        }

        return found ? NearestSampleResult<T>.Of(best!) : NearestSampleResult<T>.Inconclusive;
    }
}
