using SysAnalyzer.Capture;

namespace SysAnalyzer.Analysis;

/// <summary>
/// Power limit detection and PSU adequacy estimation (§15.7).
/// Requires Tier 2 sensor data (power draw, clock speed).
/// </summary>
public static class PowerAnalyzer
{
    /// <summary>
    /// Compute the percentage of time CPU is at TDP and clocks are dropping.
    /// Power at TDP = within 5% of rated TDP. Clock drop = below 95% of max observed clock.
    /// </summary>
    public static double ComputeCpuPowerLimitPct(
        IReadOnlyList<SensorSnapshot> snapshots,
        double cpuTdpWatts)
    {
        if (snapshots.Count == 0 || cpuTdpWatts <= 0) return 0;

        // Get max observed clock to detect drops
        var clocks = snapshots.Select(s => s.CpuClockMhz).Where(c => c.HasValue).Select(c => c!.Value).ToArray();
        if (clocks.Length == 0) return 0;
        double maxClock = clocks.Max();
        if (maxClock == 0) return 0;

        double tdpLowThreshold = cpuTdpWatts * 0.95;
        double clockDropThreshold = maxClock * 0.95;

        int limitedSamples = 0;
        int validSamples = 0;

        foreach (var s in snapshots)
        {
            if (s.CpuPowerW is not null && s.CpuClockMhz is not null)
            {
                validSamples++;
                if (s.CpuPowerW.Value >= tdpLowThreshold && s.CpuClockMhz.Value < clockDropThreshold)
                    limitedSamples++;
            }
        }

        return validSamples > 0 ? (double)limitedSamples / validSamples * 100.0 : 0;
    }

    /// <summary>
    /// Compute the percentage of time GPU is power-limited (at TDP + clock dropping).
    /// </summary>
    public static double ComputeGpuPowerLimitPct(
        IReadOnlyList<SensorSnapshot> snapshots,
        double gpuTdpWatts)
    {
        if (snapshots.Count == 0 || gpuTdpWatts <= 0) return 0;

        var clocks = snapshots.Select(s => s.GpuClockMhz).Where(c => c.HasValue).Select(c => c!.Value).ToArray();
        if (clocks.Length == 0) return 0;
        double maxClock = clocks.Max();
        if (maxClock == 0) return 0;

        double tdpLowThreshold = gpuTdpWatts * 0.95;
        double clockDropThreshold = maxClock * 0.95;

        int limitedSamples = 0;
        int validSamples = 0;

        foreach (var s in snapshots)
        {
            if (s.GpuPowerW is not null && s.GpuClockMhz is not null)
            {
                validSamples++;
                if (s.GpuPowerW.Value >= tdpLowThreshold && s.GpuClockMhz.Value < clockDropThreshold)
                    limitedSamples++;
            }
        }

        return validSamples > 0 ? (double)limitedSamples / validSamples * 100.0 : 0;
    }

    /// <summary>
    /// PSU adequacy estimation. Compares total CPU+GPU power draw against common PSU tiers.
    /// Returns a warning if power draw approaches or exceeds 80% of the estimated PSU tier.
    /// </summary>
    public static PsuAdequacyResult EstimatePsuAdequacy(
        IReadOnlyList<SensorSnapshot> snapshots,
        int? knownPsuWatts = null)
    {
        if (snapshots.Count == 0)
            return new PsuAdequacyResult(0, 0, 0, null, false);

        // Compute peak total power (CPU + GPU)
        double peakPower = 0;
        double avgPower = 0;
        int validSamples = 0;

        foreach (var s in snapshots)
        {
            double samplePower = 0;
            bool hasPower = false;

            if (s.CpuPowerW is not null)
            {
                samplePower += s.CpuPowerW.Value;
                hasPower = true;
            }
            if (s.GpuPowerW is not null)
            {
                samplePower += s.GpuPowerW.Value;
                hasPower = true;
            }

            if (hasPower)
            {
                validSamples++;
                avgPower += samplePower;
                peakPower = Math.Max(peakPower, samplePower);
            }
        }

        if (validSamples == 0)
            return new PsuAdequacyResult(0, 0, 0, null, false);

        avgPower /= validSamples;

        // Estimate total system power (components measured + ~100W for rest of system)
        double systemOverhead = 100; // mobo, RAM, fans, drives
        double estimatedTotalPeak = peakPower + systemOverhead;

        // Determine PSU tier to compare against
        int psuTier = knownPsuWatts ?? EstimatePsuTier(estimatedTotalPeak);

        // Warning if estimated total > 80% of PSU tier
        bool isWarning = estimatedTotalPeak > psuTier * 0.80;

        return new PsuAdequacyResult(peakPower, avgPower, estimatedTotalPeak, psuTier, isWarning);
    }

    /// <summary>
    /// Estimate which PSU tier the system likely has based on power draw.
    /// Common tiers: 550W, 650W, 750W, 850W, 1000W.
    /// </summary>
    private static int EstimatePsuTier(double estimatedTotalPeak)
    {
        // Assume PSU is one tier above peak — most common build practice
        int[] tiers = [550, 650, 750, 850, 1000, 1200];

        foreach (var tier in tiers)
        {
            if (estimatedTotalPeak <= tier * 0.85)
                return tier;
        }

        return tiers[^1]; // Assume largest if draw is very high
    }
}

public record PsuAdequacyResult(
    double PeakComponentPowerW,
    double AvgComponentPowerW,
    double EstimatedTotalPeakW,
    int? PsuTierW,
    bool IsWarning);
