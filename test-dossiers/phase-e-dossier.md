# Test Dossier — Phase E: Analysis Engine & Rule System
Generated: 2025-04-19  
SDK: 10.0.100  
OS: Microsoft Windows NT 10.0.26200.0  
Duration: 2.2s (full test suite)  
Verdict: **✅ ALL PASS**

---

## Summary Dashboard

| Test Class | Tests | Pass | Fail | Skip | Status |
|------------|-------|------|------|------|--------|
| MetricAggregatorTests | 10 | 10 | 0 | 0 | ✅ |
| FrameTimeCorrelatorTests | 8 | 8 | 0 | 0 | ✅ |
| BottleneckScorerTests | 10 | 10 | 0 | 0 | ✅ |
| CrossCorrelationTests | 7 | 7 | 0 | 0 | ✅ |
| AdvancedDetectionTests | 7 | 7 | 0 | 0 | ✅ |
| BaselineComparatorTests | 9 | 9 | 0 | 0 | ✅ |
| RecommendationEngineTests | 6 | 6 | 0 | 0 | ✅ |
| AnalysisPipelineTests | 4 | 4 | 0 | 0 | ✅ |
| **Phase E Subtotal** | **61** | **61** | **0** | **0** | **✅** |
| *All other test classes* | *229* | *229* | *0* | *0* | *✅* |
| **Grand Total** | **290** | **290** | **0** | **0** | **✅** |

---

## Deliverable → Test Mapping

### E.1 — MetricAggregator (Statistical Aggregation)

**Test class:** `Unit/MetricAggregatorTests.cs`

| Test | Assertion Summary | Status |
|------|-------------------|--------|
| `KnownValues_CorrectMeanAndPercentiles` | Verifies mean, P50, P95, P99 against hand-computed values for a known data set | ✅ |
| `StdDev_KnownValues` | Standard deviation matches expected value for a known array | ✅ |
| `TrendSlope_MonotonicallyIncreasing_PositiveSlopeHighR2` | Monotonically increasing array produces positive slope with R² near 1.0 | ✅ |
| `TrendSlope_FlatArray_SlopeNearZero` | Flat array produces slope near zero | ✅ |
| `Percentile_SingleValue` | Single-element array returns that value for all percentiles | ✅ |
| `Percentile_TwoValues` | Two-element array produces correct interpolated percentiles | ✅ |
| `TimeAboveThreshold_Correct` | Percentage of samples exceeding threshold matches expected ratio | ✅ |
| `Aggregate_EmptySnapshots_ReturnsEmpty` | Empty input produces empty aggregated result without error | ✅ |
| `Aggregate_WithGpu_ReturnsGpuMetrics` | GPU metrics are included when GPU data is present in snapshots | ✅ |
| `Aggregate_MissingGpu_ReturnsNullGpu` | Missing GPU data produces null GPU section, not zeros | ✅ |

### E.2 — FrameTimeCorrelator (Spike ↔ Metric Correlation)

**Test class:** `Unit/FrameTimeCorrelatorTests.cs`

| Test | Assertion Summary | Status |
|------|-------------------|--------|
| `VramAt99_TaggedAsVramOverflow` | VRAM at 99%+ during a stutter spike is tagged as `vram_overflow` | ✅ |
| `CpuBoundFrame_Tagged` | Frame where CPU duration exceeds GPU duration tagged as `cpu_bound` | ✅ |
| `GpuBoundFrame_Tagged` | Frame where GPU duration exceeds CPU duration tagged as `gpu_bound` | ✅ |
| `MultipleCauses_AllTagsApplied` | A spike with multiple simultaneous conditions gets all relevant tags | ✅ |
| `NoPresentMon_ReturnsNull` | Null output when no PresentMon data exists; no crash | ✅ |
| `EmptySpikes_ReturnsNull` | Empty spike list produces null correlation result | ✅ |
| `PeriodicPattern_DetectedEvery60s` | Regular 60-second spike interval detected as periodic/scheduled | ✅ |
| `SpikeWithNoMetricCorrelation_TaggedAsUnknown` | Spike with no correlated system metric tagged as `unknown` | ✅ |

### E.3 — BottleneckScorer (Profile-Weighted Scoring)

**Test class:** `Unit/BottleneckScorerTests.cs`

| Test | Assertion Summary | Status |
|------|-------------------|--------|
| `GamingProfile_CpuBound_HighCpuScore` | Gaming profile on CPU-bound data produces high CPU subsystem score | ✅ |
| `GamingProfile_GpuBound_HighGpuScore` | Gaming profile on GPU-bound data produces high GPU subsystem score | ✅ |
| `DifferentProfiles_DifferentScores` | Same data with gaming vs. compiling profile yields different scores | ✅ |
| `MissingTier2Metrics_ScoreRenormalized` | Missing Tier 2 metrics trigger score renormalization with report note | ✅ |
| `AllMetricsMissing_NullScore` | All metrics missing for a subsystem → score is null, not 0 | ✅ |
| `Normalize_AtWarning_Returns0` | Metric at warning threshold normalizes to 0 | ✅ |
| `Normalize_AtCritical_Returns100` | Metric at critical threshold normalizes to 100 | ✅ |
| `Normalize_BetweenThresholds_ReturnsProportional` | Metric between thresholds returns proportional value | ✅ |
| `MissingGpu_NullGpuScore` | Missing GPU data produces null GPU score | ✅ |
| `Classification_Boundaries` | Score boundaries (0-25 Healthy, 26-50 Moderate, 51-75 Stressed, 76-100 Bottleneck) correct | ✅ |

### E.4 — CrossCorrelationDetector (Compound Bottleneck Patterns)

**Test class:** `Unit/CrossCorrelationTests.cs`

| Test | Assertion Summary | Status |
|------|-------------------|--------|
| `CpuAndMemoryBothHigh_CompoundDetected` | CPU + Memory both high triggers compound diagnosis | ✅ |
| `GpuBoundCpuHeadroom_Detected` | GPU 100% + CPU low → GPU-bound with CPU headroom pattern | ✅ |
| `PagefileThrash_Detected` | Disk high + Memory high → pagefile thrashing detected | ✅ |
| `SingleThreadBottleneck_Detected` | Single core maxed + others idle → single-threaded bottleneck | ✅ |
| `VramOverflow_Detected` | VRAM full + GPU spiking → VRAM overflow pattern | ✅ |
| `SingleResourceStress_NoFalseCompound` | Single-resource stress does not falsely trigger compound patterns | ✅ |
| `HealthySystem_NoPatterns` | Healthy system metrics produce no cross-correlation patterns | ✅ |

### E.5 — BaselineComparator (Delta Scoring)

**Test class:** `Component/BaselineComparatorTests.cs`

| Test | Assertion Summary | Status |
|------|-------------------|--------|
| `SameFingerprint_FingerprintMatchTrue` | Matching fingerprints set `fingerprintMatch = true` | ✅ |
| `DifferentFingerprint_WarningEmitted` | Mismatched fingerprints emit hardware change warning | ✅ |
| `NullBaseline_ReturnsNull` | No prior baseline → delta section is null | ✅ |
| `ScoreImproved_VerdictBetter` | Score improvement → verdict "Better" | ✅ |
| `ScoreWorsened_VerdictWorse` | Score worsened → verdict "Worse" | ✅ |
| `ScoreWithin5Pct_VerdictSame` | Score within ±5% → verdict "Same" | ✅ |
| `ScoreMajorImprovement_VerdictFixed` | Score 91→23 → verdict "Fixed" | ✅ |
| `ScoreMajorRegression_VerdictRegressed` | Score 23→88 → verdict "Regressed" | ✅ |
| `NewRecommendations_Tracked` | New recommendations not in baseline are tracked in delta | ✅ |

### E.6 — RecommendationEngine (Trigger Eval + Template Resolve)

**Test class:** `Component/RecommendationEngineTests.cs`

| Test | Assertion Summary | Status |
|------|-------------------|--------|
| `CpuBoundData_TriggersCpuBoundRec` | CPU-bound fixture data triggers CPU-bound recommendation | ✅ |
| `CleanHealthy_ZeroRecommendations` | Clean/healthy fixture produces zero recommendations | ✅ |
| `ConfidenceAutoEscalation_WithEvidenceBoost` | Trigger match + evidence_boost match → confidence escalated to `high` | ✅ |
| `MissingCulpritData_TemplateDegradesGracefully` | Missing culprit data → templates degrade to "[data unavailable]" | ✅ |
| `SortedByPriorityThenSeverity` | Results sorted by priority descending, then severity | ✅ |
| `Determinism_100Runs_IdenticalOutput` | 100 runs on same input produce identical recommendation lists | ✅ |

### E.7 — AdvancedDetections (Thermal Soak, Memory Leak, NUMA, etc.)

**Test class:** `Unit/AdvancedDetectionTests.cs`

| Test | Assertion Summary | Status |
|------|-------------------|--------|
| `ThermalSoak_MonotonicTempCurve_Detected` | Monotonic temperature curve → thermal soak detected | ✅ |
| `ThermalSoak_FlatCurve_NotDetected` | Flat temperature curve → not detected | ✅ |
| `MemoryLeak_PositiveSlope_Detected` | Positive committed-bytes slope → memory leak detected | ✅ |
| `MemoryLeak_StableCurve_NotDetected` | Stable committed bytes → not detected | ✅ |
| `NumaImbalance_3SticksIn4Slots_Detected` | 3 DIMMs in 4 slots → NUMA/channel imbalance detected | ✅ |
| `NumaImbalance_2SticksIn4Slots_NotDetected` | 2 DIMMs in 4 slots (proper dual-channel) → not detected | ✅ |
| `SingleChannelMemory_Detected` | Single-channel memory configuration detected | ✅ |

### E.8 — AnalysisPipeline (Full Pipeline Orchestration)

**Test class:** `Component/AnalysisPipelineTests.cs`

| Test | Assertion Summary | Status |
|------|-------------------|--------|
| `FullPipeline_WithAllData_ProducesResults` | Full pipeline with all data sources produces complete AnalysisSummary with scores and recommendations | ✅ |
| `FullPipeline_NoPresentMon_FrameTimeNull` | Pipeline without PresentMon data → frame-time correlation is null, rest completes | ✅ |
| `DegradedMode_Tier1Only_StillProducesScores` | Tier 1 only mode → scores still produced (renormalized), no crash | ✅ |
| `Determinism_SameInput_SameOutput` | Same input twice → identical AnalysisSummary output | ✅ |

---

## Exit Gate Checklist

| # | Gate | Evidence | Status |
|---|------|----------|--------|
| 1 | `dotnet build` succeeds | `dotnet build --no-incremental` → 0 errors, 2 MSB3061 warnings (locked DLLs, non-functional) | ✅ |
| 2 | All unit + component + integration tests pass | `dotnet test --verbosity normal` → 290 passed, 0 failed, 0 skipped | ✅ |
| 3 | Full pipeline runs end-to-end | `AnalysisPipelineTests.FullPipeline_WithAllData_ProducesResults` — produces JSON with scores and recommendations | ✅ |
| 4 | Scoring varies by profile | `BottleneckScorerTests.DifferentProfiles_DifferentScores` — same data, different profiles → different scores | ✅ |
| 5 | Recommendations are deterministic | `RecommendationEngineTests.Determinism_100Runs_IdenticalOutput` — 100 runs → identical output | ✅ |
| 6 | Each canonical fixture produces expected output | `CpuBoundData_TriggersCpuBoundRec`, `CleanHealthy_ZeroRecommendations` — fixture-to-recommendation mapping verified | ✅ |
| 7 | Confidence escalation works | `ConfidenceAutoEscalation_WithEvidenceBoost` — auto confidence + evidence_boost → `high` | ✅ |
| 8 | Score renormalization works | `MissingTier2Metrics_ScoreRenormalized` — Tier 2 absent → renormalized score with missing-metric note | ✅ |
| 9 | Baseline comparison works | `BaselineComparatorTests` (9 tests) — deltas computed, fingerprint mismatch → warning, verdicts correct | ✅ |
| 10 | Auto-baseline matching | `BaselineManagerTests.LoadLatest_ReturnsNewestFile` — auto-detection of prior baseline by fingerprint | ✅ |
| 11 | Advanced detections work | `AdvancedDetectionTests` — memory leak, thermal soak, NUMA imbalance all detected/not-detected correctly | ✅ |
| 12 | Template variables resolved | `MissingCulpritData_TemplateDegradesGracefully` — all placeholders resolved; missing data degrades gracefully | ✅ |
| 13 | Missing data graceful | `FullPipeline_NoPresentMon_FrameTimeNull`, `DegradedMode_Tier1Only_StillProducesScores` — no crash, lower-confidence recs | ✅ |

---

## Test Detail Listing

### Unit/MetricAggregatorTests (10 tests)

| # | Test Method | Duration | Result |
|---|-------------|----------|--------|
| 1 | `KnownValues_CorrectMeanAndPercentiles` | < 1 ms | ✅ Passed |
| 2 | `StdDev_KnownValues` | < 1 ms | ✅ Passed |
| 3 | `TrendSlope_MonotonicallyIncreasing_PositiveSlopeHighR2` | < 1 ms | ✅ Passed |
| 4 | `TrendSlope_FlatArray_SlopeNearZero` | 1 ms | ✅ Passed |
| 5 | `Percentile_SingleValue` | < 1 ms | ✅ Passed |
| 6 | `Percentile_TwoValues` | < 1 ms | ✅ Passed |
| 7 | `TimeAboveThreshold_Correct` | < 1 ms | ✅ Passed |
| 8 | `Aggregate_EmptySnapshots_ReturnsEmpty` | < 1 ms | ✅ Passed |
| 9 | `Aggregate_WithGpu_ReturnsGpuMetrics` | < 1 ms | ✅ Passed |
| 10 | `Aggregate_MissingGpu_ReturnsNullGpu` | < 1 ms | ✅ Passed |

### Unit/FrameTimeCorrelatorTests (8 tests)

| # | Test Method | Duration | Result |
|---|-------------|----------|--------|
| 1 | `VramAt99_TaggedAsVramOverflow` | 1 ms | ✅ Passed |
| 2 | `CpuBoundFrame_Tagged` | 1 ms | ✅ Passed |
| 3 | `GpuBoundFrame_Tagged` | < 1 ms | ✅ Passed |
| 4 | `MultipleCauses_AllTagsApplied` | < 1 ms | ✅ Passed |
| 5 | `NoPresentMon_ReturnsNull` | < 1 ms | ✅ Passed |
| 6 | `EmptySpikes_ReturnsNull` | < 1 ms | ✅ Passed |
| 7 | `PeriodicPattern_DetectedEvery60s` | < 1 ms | ✅ Passed |
| 8 | `SpikeWithNoMetricCorrelation_TaggedAsUnknown` | < 1 ms | ✅ Passed |

### Unit/BottleneckScorerTests (10 tests)

| # | Test Method | Duration | Result |
|---|-------------|----------|--------|
| 1 | `GamingProfile_CpuBound_HighCpuScore` | < 1 ms | ✅ Passed |
| 2 | `GamingProfile_GpuBound_HighGpuScore` | < 1 ms | ✅ Passed |
| 3 | `DifferentProfiles_DifferentScores` | 2 ms | ✅ Passed |
| 4 | `MissingTier2Metrics_ScoreRenormalized` | < 1 ms | ✅ Passed |
| 5 | `AllMetricsMissing_NullScore` | < 1 ms | ✅ Passed |
| 6 | `MissingGpu_NullGpuScore` | 5 ms | ✅ Passed |
| 7 | `Normalize_AtWarning_Returns0` | < 1 ms | ✅ Passed |
| 8 | `Normalize_AtCritical_Returns100` | < 1 ms | ✅ Passed |
| 9 | `Normalize_BetweenThresholds_ReturnsProportional` | < 1 ms | ✅ Passed |
| 10 | `Classification_Boundaries` | < 1 ms | ✅ Passed |

### Unit/CrossCorrelationTests (7 tests)

| # | Test Method | Duration | Result |
|---|-------------|----------|--------|
| 1 | `CpuAndMemoryBothHigh_CompoundDetected` | < 1 ms | ✅ Passed |
| 2 | `GpuBoundCpuHeadroom_Detected` | < 1 ms | ✅ Passed |
| 3 | `PagefileThrash_Detected` | < 1 ms | ✅ Passed |
| 4 | `SingleThreadBottleneck_Detected` | < 1 ms | ✅ Passed |
| 5 | `VramOverflow_Detected` | < 1 ms | ✅ Passed |
| 6 | `SingleResourceStress_NoFalseCompound` | < 1 ms | ✅ Passed |
| 7 | `HealthySystem_NoPatterns` | < 1 ms | ✅ Passed |

### Unit/AdvancedDetectionTests (7 tests)

| # | Test Method | Duration | Result |
|---|-------------|----------|--------|
| 1 | `ThermalSoak_MonotonicTempCurve_Detected` | 1 ms | ✅ Passed |
| 2 | `ThermalSoak_FlatCurve_NotDetected` | < 1 ms | ✅ Passed |
| 3 | `MemoryLeak_PositiveSlope_Detected` | < 1 ms | ✅ Passed |
| 4 | `MemoryLeak_StableCurve_NotDetected` | < 1 ms | ✅ Passed |
| 5 | `NumaImbalance_3SticksIn4Slots_Detected` | < 1 ms | ✅ Passed |
| 6 | `NumaImbalance_2SticksIn4Slots_NotDetected` | < 1 ms | ✅ Passed |
| 7 | `SingleChannelMemory_Detected` | < 1 ms | ✅ Passed |

### Component/BaselineComparatorTests (9 tests)

| # | Test Method | Duration | Result |
|---|-------------|----------|--------|
| 1 | `SameFingerprint_FingerprintMatchTrue` | < 1 ms | ✅ Passed |
| 2 | `DifferentFingerprint_WarningEmitted` | < 1 ms | ✅ Passed |
| 3 | `NullBaseline_ReturnsNull` | < 1 ms | ✅ Passed |
| 4 | `ScoreImproved_VerdictBetter` | < 1 ms | ✅ Passed |
| 5 | `ScoreWorsened_VerdictWorse` | < 1 ms | ✅ Passed |
| 6 | `ScoreWithin5Pct_VerdictSame` | < 1 ms | ✅ Passed |
| 7 | `ScoreMajorImprovement_VerdictFixed` | 1 ms | ✅ Passed |
| 8 | `ScoreMajorRegression_VerdictRegressed` | < 1 ms | ✅ Passed |
| 9 | `NewRecommendations_Tracked` | < 1 ms | ✅ Passed |

### Component/RecommendationEngineTests (6 tests)

| # | Test Method | Duration | Result |
|---|-------------|----------|--------|
| 1 | `CpuBoundData_TriggersCpuBoundRec` | < 1 ms | ✅ Passed |
| 2 | `CleanHealthy_ZeroRecommendations` | < 1 ms | ✅ Passed |
| 3 | `ConfidenceAutoEscalation_WithEvidenceBoost` | < 1 ms | ✅ Passed |
| 4 | `MissingCulpritData_TemplateDegradesGracefully` | 5 ms | ✅ Passed |
| 5 | `SortedByPriorityThenSeverity` | < 1 ms | ✅ Passed |
| 6 | `Determinism_100Runs_IdenticalOutput` | < 1 ms | ✅ Passed |

### Component/AnalysisPipelineTests (4 tests)

| # | Test Method | Duration | Result |
|---|-------------|----------|--------|
| 1 | `FullPipeline_WithAllData_ProducesResults` | 9 ms | ✅ Passed |
| 2 | `FullPipeline_NoPresentMon_FrameTimeNull` | 2 ms | ✅ Passed |
| 3 | `DegradedMode_Tier1Only_StillProducesScores` | 18 ms | ✅ Passed |
| 4 | `Determinism_SameInput_SameOutput` | 3 ms | ✅ Passed |

---

## Screenshots

No UI artifacts in this phase — screenshots not applicable.

---

## Build Artifacts

### Phase E Source Files (Analysis Engine)

| File | Lines | Purpose |
|------|-------|---------|
| `Analysis/MetricAggregator.cs` | 305 | Statistical aggregation: mean, percentiles, stddev, trend slope |
| `Analysis/FrameTimeCorrelator.cs` | 150 | Spike ↔ metric correlation with cause tagging |
| `Analysis/BottleneckScorer.cs` | 239 | Profile-weighted subsystem scoring (0-100) with renormalization |
| `Analysis/CrossCorrelationDetector.cs` | 66 | Compound bottleneck pattern detection |
| `Analysis/BaselineComparator.cs` | 134 | Delta scoring between current and baseline runs |
| `Analysis/RecommendationEngine.cs` | 264 | Trigger evaluation + template resolution for actionable recommendations |
| `Analysis/AdvancedDetections.cs` | 203 | Thermal soak, memory leak, NUMA imbalance, etc. |
| `Analysis/AnalysisPipeline.cs` | 113 | Full pipeline orchestration (all phases in sequence) |

### Phase E Test Files

| File | Lines | Purpose |
|------|-------|---------|
| `Tests/Unit/MetricAggregatorTests.cs` | 142 | Statistical aggregation correctness |
| `Tests/Unit/FrameTimeCorrelatorTests.cs` | 153 | Spike ↔ metric correlation tagging |
| `Tests/Unit/BottleneckScorerTests.cs` | 313 | Profile scoring, normalization, renormalization |
| `Tests/Unit/CrossCorrelationTests.cs` | 106 | Compound pattern detection and false-positive guards |
| `Tests/Unit/AdvancedDetectionTests.cs` | 125 | Advanced detection positive and negative cases |
| `Tests/Component/BaselineComparatorTests.cs` | 155 | Delta scoring with fingerprint matching |
| `Tests/Component/RecommendationEngineTests.cs` | 199 | Recommendation trigger, confidence, determinism |
| `Tests/Component/AnalysisPipelineTests.cs` | 148 | End-to-end pipeline with fakes |

---

## Coverage Notes

No code coverage collection was performed for this dossier run. To collect coverage, run:

```
dotnet test --collect:"XPlat Code Coverage"
```

---

## Reproducibility

```
cd C:\dev\sys-analyzer\SysAnalyzer
dotnet test --verbosity normal --logger "console;verbosity=detailed"
```
