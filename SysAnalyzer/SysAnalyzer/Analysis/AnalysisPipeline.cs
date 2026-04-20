using SysAnalyzer.Analysis.Models;
using SysAnalyzer.Capture;
using SysAnalyzer.Config;

namespace SysAnalyzer.Analysis;

/// <summary>
/// Orchestrates the full analysis pipeline (§5, §6): aggregate → correlate → score → compare → recommend.
/// </summary>
public class AnalysisPipeline
{
    private readonly AnalyzerConfig _config;
    private readonly BaselineComparator _baselineComparator = new();
    private readonly RecommendationEngine _recommendationEngine = new();

    public AnalysisPipeline(AnalyzerConfig config)
    {
        _config = config;
    }

    public AnalysisPipelineResult Run(
        IReadOnlyList<SensorSnapshot> snapshots,
        IReadOnlyList<FrameTimeSample>? frameTimeSamples,
        IReadOnlyList<EtwEvent>? etwEvents,
        HardwareInventory? hardware,
        SystemConfiguration? sysConfig,
        string profileName,
        AnalysisSummary? baseline = null)
    {
        // 1. Statistical aggregation
        var aggregatedMetrics = MetricAggregator.Aggregate(snapshots);

        // 2. Frame-time summary (from Phase C)
        FrameTimeSummary? frameTimeSummary = null;
        IReadOnlyList<FrameTimeSample>? stutterSpikes = null;
        if (frameTimeSamples is not null && frameTimeSamples.Count > 0)
        {
            string? trackedApp = frameTimeSamples[0].ApplicationName;
            frameTimeSummary = FrameTimeAggregator.Compute(frameTimeSamples, trackedApp);

            // Compute stutter spikes for correlation
            double median = frameTimeSummary?.P50FrameTimeMs ?? 0;
            double threshold = median * _config.Thresholds.FrameTime.GetValueOrDefault("stutter_spike_multiplier", 2.0);
            stutterSpikes = frameTimeSamples.Where(s => s.FrameTimeMs > threshold).ToList();
        }

        // 3. Culprit attribution (from Phase D)
        CulpritAttributionResult? culpritResult = null;
        if (etwEvents is not null && etwEvents.Count > 0 && stutterSpikes is not null)
        {
            var attributor = new CulpritAttributor();
            culpritResult = attributor.Attribute(
                stutterSpikes.Select(s => s.Timestamp).ToList(),
                etwEvents,
                hasEtw: true,
                hasDpc: etwEvents.Any(e => e is DpcEvent),
                eventsLost: 0);
        }

        // 4. Frame-time correlation
        var correlator = new FrameTimeCorrelator();
        var frameCorrelation = correlator.Correlate(stutterSpikes, snapshots, culpritResult);

        // 5. Bottleneck scoring
        var profile = _config.Profiles.GetValueOrDefault(profileName) ?? _config.Profiles.Values.FirstOrDefault() ?? new ProfileConfig();
        var scoringResult = BottleneckScorer.Score(aggregatedMetrics, profile, _config.Thresholds);

        // 6. Cross-correlation
        var crossCorrelation = CrossCorrelationDetector.Detect(aggregatedMetrics, frameCorrelation);

        // 7. Advanced detections
        var advancedDetections = AdvancedDetections.RunAll(aggregatedMetrics, snapshots, frameTimeSummary, hardware);

        // 8. Build field dictionary for recommendations
        var fields = RecommendationEngine.BuildFieldDictionary(
            aggregatedMetrics, frameTimeSummary, culpritResult,
            scoringResult, frameCorrelation, crossCorrelation,
            hardware, sysConfig);

        // 9. Recommendation engine
        var recommendations = _recommendationEngine.Evaluate(fields, _config);

        // 10. Baseline comparison
        BaselineComparisonResult? baselineComparison = null;
        // (Baseline comparison requires assembled AnalysisSummary; handled at the caller level)

        return new AnalysisPipelineResult(
            AggregatedMetrics: aggregatedMetrics,
            FrameTimeSummary: frameTimeSummary,
            CulpritResult: culpritResult,
            FrameCorrelation: frameCorrelation,
            ScoringResult: scoringResult,
            CrossCorrelation: crossCorrelation,
            AdvancedDetections: advancedDetections,
            Recommendations: recommendations,
            BaselineComparison: baselineComparison,
            Fields: fields
        );
    }
}

public record AnalysisPipelineResult(
    AggregatedMetrics AggregatedMetrics,
    FrameTimeSummary? FrameTimeSummary,
    CulpritAttributionResult? CulpritResult,
    FrameTimeCorrelation? FrameCorrelation,
    ScoringResult ScoringResult,
    CrossCorrelationResult CrossCorrelation,
    IReadOnlyList<AdvancedDetection> AdvancedDetections,
    IReadOnlyList<RecommendationEntry> Recommendations,
    BaselineComparisonResult? BaselineComparison,
    Dictionary<string, object?> Fields
);
