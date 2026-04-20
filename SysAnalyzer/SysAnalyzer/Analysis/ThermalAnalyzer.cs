using SysAnalyzer.Capture;

namespace SysAnalyzer.Analysis;

/// <summary>
/// Thermal throttle detection and thermal soak analysis (§15.1, §15.7).
/// Requires Tier 2 sensor data (temperature, clock speed).
/// </summary>
public static class ThermalAnalyzer
{
    /// <summary>
    /// Compute the percentage of capture time where CPU was thermally throttled.
    /// Throttled = temp above warning threshold AND clock below base clock.
    /// </summary>
    public static double ComputeCpuThermalThrottlePct(
        IReadOnlyList<SensorSnapshot> snapshots,
        double tempWarning,
        double baseClockMhz)
    {
        if (snapshots.Count == 0 || baseClockMhz <= 0) return 0;

        int throttledSamples = 0;
        int validSamples = 0;

        foreach (var s in snapshots)
        {
            if (s.CpuTempC is not null && s.CpuClockMhz is not null)
            {
                validSamples++;
                if (s.CpuTempC.Value >= tempWarning && s.CpuClockMhz.Value < baseClockMhz)
                    throttledSamples++;
            }
        }

        return validSamples > 0 ? (double)throttledSamples / validSamples * 100.0 : 0;
    }

    /// <summary>
    /// Compute the percentage of capture time where GPU was thermally throttled.
    /// </summary>
    public static double ComputeGpuThermalThrottlePct(
        IReadOnlyList<SensorSnapshot> snapshots,
        double tempWarning,
        double baseClockMhz)
    {
        if (snapshots.Count == 0 || baseClockMhz <= 0) return 0;

        int throttledSamples = 0;
        int validSamples = 0;

        foreach (var s in snapshots)
        {
            if (s.GpuTempC is not null && s.GpuClockMhz is not null)
            {
                validSamples++;
                if (s.GpuTempC.Value >= tempWarning && s.GpuClockMhz.Value < baseClockMhz)
                    throttledSamples++;
            }
        }

        return validSamples > 0 ? (double)throttledSamples / validSamples * 100.0 : 0;
    }

    /// <summary>
    /// Compute clock drop percentage: % of time clocks are > 10% below max observed boost.
    /// </summary>
    public static double ComputeClockDropPct(IReadOnlyList<SensorSnapshot> snapshots, bool isCpu)
    {
        double[] clocks;
        if (isCpu)
            clocks = snapshots.Select(s => s.CpuClockMhz).Where(c => c.HasValue).Select(c => c!.Value).ToArray();
        else
            clocks = snapshots.Select(s => s.GpuClockMhz).Where(c => c.HasValue).Select(c => c!.Value).ToArray();

        if (clocks.Length == 0) return 0;

        double maxClock = clocks.Max();
        if (maxClock == 0) return 0;

        double threshold = maxClock * 0.9; // 10% below max
        int droppedSamples = clocks.Count(c => c < threshold);

        return (double)droppedSamples / clocks.Length * 100.0;
    }

    /// <summary>
    /// Detect thermal soak: temperature continuously rising after 15+ minutes (R² > 0.8).
    /// Returns (isSoaking, slope, rSquared).
    /// </summary>
    public static (bool IsSoaking, double Slope, double RSquared) DetectThermalSoak(
        IReadOnlyList<SensorSnapshot> snapshots,
        double minDurationMinutes = 15)
    {
        if (snapshots.Count < 2)
            return (false, 0, 0);

        double durationSeconds = snapshots.Count > 1
            ? (snapshots[^1].Timestamp - snapshots[0].Timestamp).ToSeconds()
            : 0;

        if (durationSeconds < minDurationMinutes * 60)
            return (false, 0, 0);

        var temps = snapshots.Select(s => s.CpuTempC).Where(t => t.HasValue).Select(t => t!.Value).ToArray();
        if (temps.Length < 10)
            return (false, 0, 0);

        var (slope, r2) = MetricAggregator.ComputeTrendSlope(temps);

        // Soaking if temperature is going up with strong linear fit
        bool isSoaking = slope > 0.01 && r2 > 0.8;
        return (isSoaking, slope, r2);
    }
}
