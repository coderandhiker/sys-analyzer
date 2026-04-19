# Phase E: Analysis Engine & Rule System

**Goal**: Build the full bottleneck analysis pipeline: statistical aggregation, frame-time correlation, workload-aware scoring, baseline comparison, recommendation evaluation, and confidence classification. After this phase, SysAnalyzer produces actionable, evidence-backed recommendations from real capture data.

**Key risk addressed**: The analysis engine must be deterministic (same data → same output, always), must handle missing data gracefully (score renormalization), and must produce meaningfully different results across workload profiles. Getting this wrong produces misleading recommendations.

---

## Entry Gates

| # | Gate | Verification |
|---|------|-------------|
| 1 | Phases B-D exit gates all satisfied | All exit gates checked and documented |
| 2 | Real capture data available | At least 2 real JSON captures exist from Phase B/C/D testing — one with PresentMon + ETW, one degraded (Tier 1 only) |
| 3 | Config loader and expression engine working | Default `config.yaml` loads and validates. Expression parser/evaluator pass all Phase A tests. |
| 4 | Frame-time stutter spike list available | Phase C produces a list of stutter spike timestamps with frame-time details |
| 5 | Culprit attribution data available | Phase D produces `CulpritAttribution` records with process/driver names and correlation scores |
| 6 | All `AnalysisSummary` fields defined | Schema from Phase A includes scores, recommendations, baseline comparison, confidence |
| 7 | Canonical test fixtures committed | At least `cpu_bound_game.json`, `gpu_bound_game.json`, `clean_healthy.json`, `tier1_only.json` exist in `SysAnalyzer.Tests/Fixtures/` |

---

## Deliverables

### E.1 — Statistical Aggregation (§5.1 Phase 1)

1. Implement `MetricAggregator`:
   - Input: `List<SensorSnapshot>` (the time-series from capture)
   - For each metric, compute:
     - **Mean**
     - **P50, P95, P99, P999** — using interpolated percentile algorithm
     - **Max, Min**
     - **Time above threshold** — percentage of samples above each severity threshold (from config)
     - **Standard deviation** — consistency measure
     - **Trend slope** — linear regression (slope + R²) for detecting monotonic changes
   - Output: `AggregatedMetrics` record with all computed values
2. Handle edge cases:
   - Capture < 30 seconds: warn that statistical significance is low
   - Single sample: all percentiles = that sample, std dev = 0, no trend
   - Null/missing metric values (from degraded providers): skip in aggregation, note in output

**Unit tests** (deterministic numeric arrays):
- Known values → correct mean, percentiles, stddev
- Trend detection: monotonically increasing array → positive slope, R² near 1.0
- Trend detection: flat array → slope near 0
- Percentile edge cases: single value, two values, values at exact percentile boundaries
- Missing values excluded correctly

### E.2 — Frame-Time Correlation (§5.1 Phase 2)

1. Implement `FrameTimeCorrelator`:
   - Input: frame-time stutter spikes (timestamps + frame data), `SensorSnapshot` time-series, `CulpritAttribution`
   - For each stutter spike, find the `SensorSnapshot` within `METRIC_CORRELATION` (±2s) and check:
     - VRAM at 99%+ → tag as `vram_overflow`
     - Disk queue spike (> threshold) → tag as `disk_stall`
     - Context switch burst from ETW → tag as `interference` (with process name)
     - DPC time spike → tag as `dpc_storm` (with driver name)
     - CPU frame duration > GPU frame duration → tag as `cpu_bound`
     - GPU frame duration > CPU frame duration → tag as `gpu_bound`
     - Committed bytes jump → tag as `memory_pressure`
   - Group correlated spikes by cause and compute:
     - Count per cause
     - Percentage of total spikes per cause
     - Whether the pattern is periodic (regular interval between spikes → scheduled interference)
   - Produce the correlation table from §5.1 Phase 2
2. When PresentMon data is absent: skip this entirely. Set `FrameTimeCorrelation = null`.
3. When ETW data is absent: culprit-related tags are unavailable. Use utilization-only correlations.

**Unit tests** (deterministic fixtures):
- VRAM at 99% during spike → tagged correctly
- Multiple causes for one spike → all tags applied
- No PresentMon → null output, no crash
- Periodic spike pattern (every 60s) → detected as scheduled
- Spike with no correlated system metric → tagged as `unknown`

### E.3 — Workload-Aware Bottleneck Scoring (§5.1 Phase 3)

1. Implement `BottleneckScorer`:
   - Input: `AggregatedMetrics`, `WorkloadProfile` (from config), `ThresholdsConfig`
   - For each subsystem (CPU, Memory, GPU, Disk, Network), compute score (0-100) using profile weights:
     ```
     cpuScore = profile.cpu.avg_load_weight * normalize(avgLoad) +
                profile.cpu.p95_load_weight * normalize(p95Load) +
                profile.cpu.thermal_throttle_weight * normalize(thermalThrottlePct) +
                profile.cpu.single_core_saturation_weight * normalize(singleCorePct) +
                profile.cpu.dpc_time_weight * normalize(dpcTimePct) +
                profile.cpu.clock_drop_weight * normalize(clockDropPct)
     ```
   - `normalize()`: maps raw metric to 0-100 based on config thresholds (0 = healthy threshold, 100 = bottleneck threshold)
   - Different profiles produce different scores for the same data (this is the point)
2. Score renormalization when metrics are unavailable (§12.3):
   ```
   If metricN is unavailable:
   adjustedScore = (sum of available weighted metrics) / (sum of available weights) * (sum of all weights)
   ```
   Report: "Score based on {n}/{total} available metrics. Missing: {list}."
3. Classification:
   - 0-25: Healthy (green)
   - 26-50: Moderate (yellow)
   - 51-75: Stressed (orange)
   - 76-100: Bottleneck (red)

**Unit tests**:
- Gaming profile: CPU-bound fixture → high CPU score, low GPU score
- Gaming profile: GPU-bound fixture → high GPU score, low CPU score
- Compiling profile vs gaming profile on same data → different scores (compiling weights all-core higher)
- All metrics available → sum of weighted components equals score
- Missing Tier 2 metrics → score renormalized, report notes missing metrics
- All metrics missing for one subsystem → score = null, not 0
- Threshold boundary: metric exactly at warning threshold → score > 0

### E.4 — Cross-Correlation Patterns (§5.2)

1. Implement compound bottleneck detection:
   - CPU + Memory both high → compound diagnosis
   - GPU 100% + CPU low → GPU-bound with CPU headroom
   - Disk high + Memory high → pagefile thrashing
   - Single core maxed + others idle → single-threaded bottleneck
   - GPU VRAM full + GPU load spiking → VRAM overflow
   - CPU thermal throttle + high load → cooling inadequate
   - Triple: frame spikes + VRAM 99% + disk bursts → VRAM exhaustion (highest confidence)
2. Cross-correlation results influence recommendation priority and confidence

**Unit tests**:
- Each compound pattern detected with matching fixture data
- Compound patterns not falsely triggered on single-resource stress

### E.5 — Baseline Comparison & Delta Scoring (§5.1 Phase 4)

1. Implement `BaselineComparator`:
   - Input: current `AnalysisSummary`, prior `AnalysisSummary` (from `--compare` or auto-matched baseline)
   - Compare machine fingerprints:
     - Match → proceed normally
     - Mismatch → warn: "Hardware changed: {list of changes}. Delta scores may not reflect the same system."
   - For each metric and score, compute delta:
     - Absolute delta (current - baseline)
     - Percentage delta
     - Verdict: Better / Worse / Same (threshold: ±5% relative change)
   - Produce `BaselineComparison` record:
     - `baselineId`, `baselineTimestamp`, `fingerprintMatch`, `hardwareChanges[]`
     - `deltaScores` (per subsystem)
     - `deltaMetrics` (per key metric: avg FPS, P99 frame time, memory util, etc.)
     - `deltaRecommendations` (new recommendations not in baseline, resolved recommendations no longer triggered)
2. Auto-baseline matching:
   - Look in `~/.sysanalyzer/baselines/{fingerprint}/` for the most recent prior JSON
   - If found, show delta automatically (no `--compare` needed)
   - If not found, skip delta section

**Unit tests** (fixture pairs):
- Same fingerprint, RAM upgraded → memory score delta negative (improved), correctly calculated
- Different fingerprint → warning emitted, deltas still computed
- No prior baseline → delta section null
- Score improved from 91 → 23 → verdict "Fixed"
- Score worsened from 23 → 88 → verdict "Regressed"
- Score within ±5% → verdict "Same"

### E.6 — Recommendation Engine (§6.2)

1. Implement `RecommendationEngine`:
   - Input: `AggregatedMetrics`, `FrameTimeCorrelation?`, `CulpritAttribution?`, `AnalyzerConfig`
   - Build the flat `AnalysisResult` dictionary from all data sources:
     - `cpu.*`, `memory.*`, `gpu.*`, `disk.*`, `network.*` from metrics
     - `frametime.*` from frame-time aggregation (null if unavailable)
     - `culprit.*` from ETW attribution (null if unavailable)
     - `system.*` from static checks
     - `thresholds.*` from config
   - Iterate all recommendations from `config.yaml`
   - For each recommendation:
     a. Evaluate `trigger` expression against the dictionary (using Phase A expression engine)
     b. If trigger matches:
        - Resolve confidence:
          - Explicit confidence → use as baseline
          - `auto` → start at `low`; if trigger references runtime data → `medium`; if `evidence_boost` also matches → `high`
        - Resolve `body` template using `TemplateResolver`
        - Add to results list
   - Sort by `priority` (descending), then `severity` (critical > warning > info)
   - Group: high-confidence first, then medium, then low
2. Ensure determinism: same `AnalysisResult` + same config → identical recommendation list (no randomness, no timing dependency)
3. Handle missing `culprit.*` fields: templates degrade to `[data unavailable — run with ETW enabled]`

**Unit tests** (using canonical fixtures):
- `cpu_bound_game.json` fixture → triggers `ft_cpu_bound`, `cpu_single_thread_bottleneck`
- `gpu_bound_game.json` fixture → triggers `ft_gpu_bound`, `gpu_bottleneck`
- `vram_exhaustion.json` fixture → triggers `ft_vram_stutter`, `gpu_vram_overflow`
- `defender_interference.json` fixture → triggers `ft_background_interference` with `MsMpEng.exe`
- `clean_healthy.json` fixture → zero recommendations triggered
- `tier1_only.json` fixture → Tier 2 recommendations suppressed, confidence correct
- Confidence auto-escalation: trigger matches + evidence_boost matches → `high`
- Template resolution: all placeholders replaced with correct values
- Missing culprit data: templates degrade gracefully
- Determinism: run engine 100 times on same input → identical output every time

### E.7 — Advanced Detections (§15)

1. **Thermal soak** (§15.1): If Tier 2 temp data exists and temperature hasn't plateaued after 15 minutes (monotonic increase, R² > 0.8), flag cooling inadequacy. Tier 1 fallback: detect clock speed drops over time.
2. **Memory leak** (§15.2): Linear regression on committed bytes. Positive slope + R² > 0.8 → leak. Report leak rate in MB/hour. If PresentMon active, correlate with FPS degradation trend.
3. **Frame-time pattern analysis** (§15.4): Coefficient of variation (std_dev/mean > 0.4) → inconsistent pacing. Bimodal distribution → thermal or power oscillation.
4. **NUMA/channel imbalance** (§15.5): 3 sticks in 4-slot board → broken dual-channel.
5. **Storage tiering** (§15.6): OS on NVMe but game on HDD → recommend moving game.
6. **Driver age** (§15.8): GPU driver > 6 months old → suggest update.
7. **Cross-resource contention** (§15.9): CPU-GPU imbalance, storage-memory cascade, VRAM-RAM spillover patterns.

**Unit tests** (per detection):
- Thermal soak: monotonic temp curve → detected; flat curve → not detected
- Memory leak: positive slope → detected; stable curve → not detected
- NUMA imbalance: 3 sticks / 4 slots → detected; 2 sticks / 4 slots → not detected

### E.8 — Wire Full Pipeline Together

1. After capture stops, execute the analysis in sequence:
   1. `MetricAggregator.Aggregate()` → `AggregatedMetrics`
   2. `FrameTimeAggregator.Summarize()` → `FrameTimeSummary` (from Phase C)
   3. `CulpritAttributor.Attribute()` → `CulpritAttribution` (from Phase D)
   4. `FrameTimeCorrelator.Correlate()` → `FrameTimeCorrelation`
   5. `BottleneckScorer.Score()` → per-subsystem scores
   6. `BaselineComparator.Compare()` → `BaselineComparison?`
   7. `RecommendationEngine.Evaluate()` → `Recommendation[]`
   8. Assemble `AnalysisSummary` with all results
2. Emit JSON with all fields populated
3. Update live display with quick summary (top bottleneck, recommendation count)

---

## Exit Gates

| # | Gate | Verification |
|---|------|-------------|
| 1 | `dotnet build` succeeds | 0 errors, 0 warnings |
| 2 | All unit + component + integration tests pass | `dotnet test` — all green |
| 3 | Full pipeline runs end-to-end | `SysAnalyzer.exe --duration 30 --profile gaming` produces JSON with non-zero scores, recommendations, and (if applicable) frame-time + culprit data |
| 4 | Scoring varies by profile | Same capture analyzed with `--profile gaming` vs `--profile compiling` produces different scores (verify manually) |
| 5 | Recommendations are deterministic | Run the tool twice on the same fixture data → identical recommendations (automated test) |
| 6 | Each canonical fixture produces expected output | `cpu_bound_game.json` → CPU recommendations. `clean_healthy.json` → no recommendations. `vram_exhaustion.json` → VRAM + frame-time recommendations. (Automated test per fixture.) |
| 7 | Confidence escalation works | A recommendation with `confidence: auto` and `evidence_boost` that matches frame-time data → boosted to `high`. Verified in output JSON. |
| 8 | Score renormalization works | `tier1_only.json` fixture → scores computed without Tier 2 metrics, report notes missing metrics. Score is not inflated or deflated by absence. |
| 9 | Baseline comparison works | `--compare <prior-run.json>` → JSON includes `delta_from_baseline` with per-metric deltas and verdicts. Fingerprint mismatch → warning. |
| 10 | Auto-baseline matching | After two runs on same machine, second run auto-detects prior baseline and shows deltas without `--compare` flag. |
| 11 | Advanced detections work | Memory leak fixture (committed bytes trending up) → memory leak recommendation triggered. Thermal soak fixture → thermal recommendation. |
| 12 | Template variables resolved | All `{placeholder}` values in recommendation bodies are replaced with actual values. No raw `{placeholder}` text in output. |
| 13 | Missing data graceful | Run without PresentMon and ETW → recommendations still produced (utilization-based, lower confidence). No crash. No raw `{culprit.*}` placeholders. |

---

## Files Created / Modified

```
SysAnalyzer/
├── Analysis/
│   ├── MetricAggregator.cs              (NEW — statistical aggregation)
│   ├── FrameTimeCorrelator.cs           (NEW — spike ↔ metric correlation)
│   ├── BottleneckScorer.cs              (NEW — profile-weighted scoring)
│   ├── BaselineComparator.cs            (NEW — delta calculation)
│   ├── RecommendationEngine.cs          (NEW — trigger eval + template resolve)
│   ├── AdvancedDetections.cs            (NEW — thermal soak, memory leak, etc.)
│   └── AnalysisPipeline.cs             (NEW — orchestrates all phases in sequence)
├── Capture/
│   └── CaptureSession.cs               (MODIFIED — wire analysis after Stop)
└── Report/
    └── JsonReportGenerator.cs           (MODIFIED — full AnalysisSummary output)

SysAnalyzer.Tests/
├── Unit/
│   ├── MetricAggregatorTests.cs         (NEW)
│   ├── FrameTimeCorrelatorTests.cs      (NEW)
│   ├── BottleneckScorerTests.cs         (NEW)
│   ├── ScoreRenormalizationTests.cs     (NEW)
│   ├── CrossCorrelationTests.cs         (NEW)
│   ├── AdvancedDetectionTests.cs        (NEW — thermal soak, memory leak, NUMA)
│   └── ConfidenceEscalationTests.cs     (MODIFIED — add evidence_boost cases)
├── Component/
│   ├── RecommendationEngineTests.cs     (NEW — per-fixture recommendation verification)
│   ├── BaselineComparatorTests.cs       (NEW)
│   └── AnalysisPipelineTests.cs         (NEW — end-to-end with fakes)
├── Integration/
│   ├── FullAnalysisFlowTests.cs         (NEW)
│   ├── DeterminismTests.cs              (NEW — same input → same output)
│   └── DegradedModeAnalysisTests.cs     (NEW — missing PresentMon/ETW/Tier2)
└── Fixtures/
    ├── cpu_bound_game.json              (NEW or verified)
    ├── gpu_bound_game.json              (NEW or verified)
    ├── vram_exhaustion.json             (NEW)
    ├── pagefile_thrash.json             (NEW)
    ├── thermal_soak.json                (NEW)
    ├── memory_leak.json                 (NEW)
    ├── clean_healthy.json               (NEW)
    ├── multi_bottleneck.json            (NEW)
    └── tier1_only.json                  (NEW or verified)
```
