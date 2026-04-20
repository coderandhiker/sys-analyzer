using SysAnalyzer.Analysis.Models;

namespace SysAnalyzer.Analysis;

/// <summary>
/// Compares current analysis against a prior baseline (§5.1 Phase 4).
/// Computes per-metric deltas with Better/Worse/Same verdicts.
/// </summary>
public class BaselineComparator
{
    private const double SameThresholdPct = 5.0;

    public BaselineComparisonResult? Compare(AnalysisSummary current, AnalysisSummary? baseline)
    {
        if (baseline is null)
            return null;

        var fingerprintMatch = current.Fingerprint.ComputeHash() == baseline.Fingerprint.ComputeHash();
        var hardwareChanges = new List<string>();

        if (!fingerprintMatch)
            hardwareChanges.AddRange(current.Fingerprint.Diff(baseline.Fingerprint));

        var scoreDeltas = ComputeScoreDeltas(current.Scores, baseline.Scores);
        var metricDeltas = ComputeMetricDeltas(current, baseline);
        var recommendationDeltas = ComputeRecommendationDeltas(current.Recommendations, baseline.Recommendations);

        return new BaselineComparisonResult(
            BaselineId: baseline.Metadata.CaptureId,
            BaselineTimestamp: baseline.Metadata.Timestamp,
            FingerprintMatch: fingerprintMatch,
            HardwareChanges: hardwareChanges,
            ScoreDeltas: scoreDeltas,
            MetricDeltas: metricDeltas,
            NewRecommendations: recommendationDeltas.New,
            ResolvedRecommendations: recommendationDeltas.Resolved
        );
    }

    private static List<DeltaScore> ComputeScoreDeltas(ScoresSummary current, ScoresSummary baseline)
    {
        var deltas = new List<DeltaScore>();

        AddScoreDelta(deltas, "CPU", current.Cpu, baseline.Cpu);
        AddScoreDelta(deltas, "Memory", current.Memory, baseline.Memory);
        if (current.Gpu is not null && baseline.Gpu is not null)
            AddScoreDelta(deltas, "GPU", current.Gpu, baseline.Gpu);
        AddScoreDelta(deltas, "Disk", current.Disk, baseline.Disk);
        AddScoreDelta(deltas, "Network", current.Network, baseline.Network);

        return deltas;
    }

    private static void AddScoreDelta(List<DeltaScore> deltas, string name, CategoryScore current, CategoryScore baseline)
    {
        double change = current.Score - baseline.Score;
        double pctChange = baseline.Score != 0 ? change / baseline.Score * 100 : (change != 0 ? 100 : 0);
        string verdict = GetScoreVerdict(current.Score, baseline.Score, pctChange);

        deltas.Add(new DeltaScore(name, baseline.Score, current.Score, change, pctChange, verdict));
    }

    private static string GetScoreVerdict(int current, int baseline, double pctChange)
    {
        if (Math.Abs(pctChange) <= SameThresholdPct && Math.Abs(current - baseline) <= 3)
            return "Same";

        // For scores, lower is better
        if (current < baseline)
        {
            return (baseline - current) >= 50 ? "Fixed" : "Better";
        }
        else
        {
            return (current - baseline) >= 50 ? "Regressed" : "Worse";
        }
    }

    private static List<DeltaMetric> ComputeMetricDeltas(AnalysisSummary current, AnalysisSummary baseline)
    {
        var deltas = new List<DeltaMetric>();

        if (current.FrameTime is not null && baseline.FrameTime is not null)
        {
            AddMetricDelta(deltas, "avg_fps", baseline.FrameTime.AvgFps, current.FrameTime.AvgFps, higherIsBetter: true);
            AddMetricDelta(deltas, "p99_frame_time_ms", baseline.FrameTime.P99FrameTimeMs, current.FrameTime.P99FrameTimeMs, higherIsBetter: false);
            AddMetricDelta(deltas, "stutter_count", baseline.FrameTime.StutterCount, current.FrameTime.StutterCount, higherIsBetter: false);
        }

        return deltas;
    }

    private static void AddMetricDelta(List<DeltaMetric> deltas, string name, double baselineVal, double currentVal, bool higherIsBetter)
    {
        double change = currentVal - baselineVal;
        double pctChange = baselineVal != 0 ? change / baselineVal * 100 : (change != 0 ? 100 : 0);

        string verdict;
        if (Math.Abs(pctChange) <= SameThresholdPct)
            verdict = "Same";
        else if ((higherIsBetter && change > 0) || (!higherIsBetter && change < 0))
            verdict = "Better";
        else
            verdict = "Worse";

        deltas.Add(new DeltaMetric(name, baselineVal, currentVal, change, pctChange, verdict));
    }

    private static (List<string> New, List<string> Resolved) ComputeRecommendationDeltas(
        IReadOnlyList<RecommendationEntry> current, IReadOnlyList<RecommendationEntry> baseline)
    {
        var currentIds = current.Select(r => r.Id).ToHashSet();
        var baselineIds = baseline.Select(r => r.Id).ToHashSet();

        var newRecs = currentIds.Except(baselineIds).ToList();
        var resolved = baselineIds.Except(currentIds).ToList();

        return (newRecs, resolved);
    }
}

public record BaselineComparisonResult(
    string BaselineId,
    DateTime BaselineTimestamp,
    bool FingerprintMatch,
    IReadOnlyList<string> HardwareChanges,
    IReadOnlyList<DeltaScore> ScoreDeltas,
    IReadOnlyList<DeltaMetric> MetricDeltas,
    IReadOnlyList<string> NewRecommendations,
    IReadOnlyList<string> ResolvedRecommendations
);

public record DeltaScore(string Subsystem, int BaselineScore, int CurrentScore, double Change, double PercentChange, string Verdict);
public record DeltaMetric(string Metric, double BaselineValue, double CurrentValue, double Change, double PercentChange, string Verdict);
