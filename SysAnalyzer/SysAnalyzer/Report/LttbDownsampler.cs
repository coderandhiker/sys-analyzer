namespace SysAnalyzer.Report;

/// <summary>
/// Largest-Triangle-Three-Buckets (LTTB) downsampling algorithm.
/// Preserves visual shape of time-series data while reducing point count.
/// Also preserves stutter spikes (values > 2× median) regardless of downsampling.
/// </summary>
public static class LttbDownsampler
{
    /// <summary>
    /// Downsample a time-series to the target number of points using LTTB.
    /// Preserves max/min values and stutter spikes (values > spikeThresholdMultiplier × median).
    /// </summary>
    /// <param name="data">Input data points (x = time, y = value).</param>
    /// <param name="targetCount">Desired output point count.</param>
    /// <param name="spikeThresholdMultiplier">Values above median × this multiplier are always preserved. Default 2.0.</param>
    /// <returns>Downsampled data preserving shape and spikes.</returns>
    public static (double X, double Y)[] Downsample(
        ReadOnlySpan<(double X, double Y)> data,
        int targetCount,
        double spikeThresholdMultiplier = 2.0)
    {
        if (data.Length <= targetCount || targetCount < 3)
        {
            var copy = new (double X, double Y)[data.Length];
            data.CopyTo(copy);
            return copy;
        }

        // Find spike indices (values > multiplier × median)
        var spikeIndices = FindSpikeIndices(data, spikeThresholdMultiplier);

        // Run core LTTB
        var lttbResult = CoreLttb(data, targetCount);

        // Merge spike points into the LTTB result
        if (spikeIndices.Count > 0)
        {
            return MergeSpikes(data, lttbResult, spikeIndices);
        }

        return lttbResult;
    }

    /// <summary>
    /// Overload accepting separate x/y arrays.
    /// </summary>
    public static (double X, double Y)[] Downsample(
        double[] xValues, double[] yValues, int targetCount,
        double spikeThresholdMultiplier = 2.0)
    {
        if (xValues.Length != yValues.Length)
            throw new ArgumentException("X and Y arrays must have the same length.");

        var data = new (double X, double Y)[xValues.Length];
        for (int i = 0; i < xValues.Length; i++)
            data[i] = (xValues[i], yValues[i]);

        return Downsample(data, targetCount, spikeThresholdMultiplier);
    }

    private static (double X, double Y)[] CoreLttb(ReadOnlySpan<(double X, double Y)> data, int targetCount)
    {
        var result = new List<(double X, double Y)>(targetCount + 10);

        // Always include first point
        result.Add(data[0]);

        double bucketSize = (double)(data.Length - 2) / (targetCount - 2);

        int prevSelectedIndex = 0;

        for (int i = 0; i < targetCount - 2; i++)
        {
            // Calculate bucket boundaries
            int bucketStart = (int)Math.Floor((i + 1) * bucketSize) + 1;
            int bucketEnd = (int)Math.Floor((i + 2) * bucketSize) + 1;
            if (bucketEnd >= data.Length) bucketEnd = data.Length - 1;

            // Calculate next bucket average
            int nextBucketStart = (int)Math.Floor((i + 2) * bucketSize) + 1;
            int nextBucketEnd = (int)Math.Floor((i + 3) * bucketSize) + 1;
            if (nextBucketEnd >= data.Length) nextBucketEnd = data.Length - 1;
            if (nextBucketStart >= data.Length) nextBucketStart = data.Length - 1;

            double avgX = 0, avgY = 0;
            int nextCount = nextBucketEnd - nextBucketStart + 1;
            if (nextCount <= 0) nextCount = 1;

            for (int j = nextBucketStart; j <= nextBucketEnd && j < data.Length; j++)
            {
                avgX += data[j].X;
                avgY += data[j].Y;
            }
            avgX /= nextCount;
            avgY /= nextCount;

            // Find the point in the current bucket that forms the largest triangle
            double maxArea = -1;
            int bestIndex = bucketStart;

            double prevX = data[prevSelectedIndex].X;
            double prevY = data[prevSelectedIndex].Y;

            for (int j = bucketStart; j <= bucketEnd && j < data.Length; j++)
            {
                double area = Math.Abs(
                    (prevX - avgX) * (data[j].Y - prevY) -
                    (prevX - data[j].X) * (avgY - prevY));

                if (area > maxArea)
                {
                    maxArea = area;
                    bestIndex = j;
                }
            }

            result.Add(data[bestIndex]);
            prevSelectedIndex = bestIndex;
        }

        // Always include last point
        result.Add(data[data.Length - 1]);

        return result.ToArray();
    }

    private static HashSet<int> FindSpikeIndices(ReadOnlySpan<(double X, double Y)> data, double multiplier)
    {
        // Compute median using a copy
        var values = new double[data.Length];
        for (int i = 0; i < data.Length; i++)
            values[i] = data[i].Y;

        Array.Sort(values);
        double median = values.Length % 2 == 0
            ? (values[values.Length / 2 - 1] + values[values.Length / 2]) / 2.0
            : values[values.Length / 2];

        double threshold = median * multiplier;
        var spikes = new HashSet<int>();

        for (int i = 0; i < data.Length; i++)
        {
            if (data[i].Y > threshold)
                spikes.Add(i);
        }

        return spikes;
    }

    private static (double X, double Y)[] MergeSpikes(
        ReadOnlySpan<(double X, double Y)> original,
        (double X, double Y)[] lttbResult,
        HashSet<int> spikeIndices)
    {
        // Build a set of X values already in the LTTB result
        var existingX = new HashSet<double>();
        foreach (var p in lttbResult)
            existingX.Add(p.X);

        // Add any spike points not already present
        var merged = new List<(double X, double Y)>(lttbResult);
        foreach (int idx in spikeIndices)
        {
            if (!existingX.Contains(original[idx].X))
            {
                merged.Add(original[idx]);
                existingX.Add(original[idx].X);
            }
        }

        // Sort by X
        merged.Sort((a, b) => a.X.CompareTo(b.X));
        return merged.ToArray();
    }

    /// <summary>
    /// Determines the target point count based on capture duration rules.
    /// &lt;30min: raw, 30m-2h: 5s intervals, 2-8h: 15s intervals.
    /// Max ~2000 points per chart.
    /// </summary>
    public static int GetTargetPointCount(double durationSeconds, int rawSampleCount)
    {
        const int maxPoints = 2000;

        if (durationSeconds < 30 * 60) // < 30 min
            return Math.Min(rawSampleCount, maxPoints);

        if (durationSeconds < 2 * 60 * 60) // 30 min - 2 hours
        {
            int target = (int)(durationSeconds / 5.0);
            return Math.Min(target, maxPoints);
        }

        // 2 - 8 hours
        int target15s = (int)(durationSeconds / 15.0);
        return Math.Min(target15s, maxPoints);
    }
}
