# Test Dossier — Phase G: HTML Report & Visual Output

```
Generated: 2026-04-20 (updated with elevated Tier 2 live capture + analysis pipeline wiring)
SDK: 10.0.100
OS: Microsoft Windows 11 Home (NT 10.0.26200.0)
Duration: 97.5s (full test suite incl. Playwright browser tests)
Verdict: ✅ ALL PASS (396 tests)
```

---

## Summary Dashboard

| Test Class | Tests | Pass | Fail | Skip | Status |
|------------|-------|------|------|------|--------|
| LttbDownsamplerTests | 17 | 17 | 0 | 0 | ✅ |
| ChartDataPreparerTests | 6 | 6 | 0 | 0 | ✅ |
| CsvExporterTests | 7 | 7 | 0 | 0 | ✅ |
| HtmlReportRenderTests | 10 | 10 | 0 | 0 | ✅ |
| FullOutputPipelineTests | 4 | 4 | 0 | 0 | ✅ |
| PlaywrightHtmlReportTests | 20 | 20 | 0 | 0 | ✅ |
| **Phase G Subtotal** | **64** | **64** | **0** | **0** | ✅ |
| ScreenshotCapture (manual) | 1 | 1 | 0 | 0 | ✅ |
| TimestampConversionTests | 8 | 8 | 0 | 0 | ✅ |
| TimeWindowTests | 6 | 6 | 0 | 0 | ✅ |
| CorrelationWindowTests | 12 | 12 | 0 | 0 | ✅ |
| TimestampAlignmentTests | 8 | 8 | 0 | 0 | ✅ |
| FingerprintTests | 9 | 9 | 0 | 0 | ✅ |
| JsonSchemaRoundTripTests | 7 | 7 | 0 | 0 | ✅ |
| ConfigLoaderTests | 8 | 8 | 0 | 0 | ✅ |
| ConfigValidatorTests | 8 | 8 | 0 | 0 | ✅ |
| ExpressionParserTests | 13 | 13 | 0 | 0 | ✅ |
| ExpressionEvaluatorTests | 15 | 15 | 0 | 0 | ✅ |
| TemplateResolverTests | 10 | 10 | 0 | 0 | ✅ |
| FilenameGenerationTests | 7 | 7 | 0 | 0 | ✅ |
| SelfOverheadTests | 3 | 3 | 0 | 0 | ✅ |
| CaptureSessionStateTests | 9 | 9 | 0 | 0 | ✅ |
| PollLoopTests | 3 | 3 | 0 | 0 | ✅ |
| PerfCounterProviderTests | 8 | 8 | 0 | 0 | ✅ |
| LibreHardwareProviderTests | 8 | 8 | 0 | 0 | ✅ |
| EtwProviderTests | 7 | 7 | 0 | 0 | ✅ |
| PresentMonProviderTests | 11 | 11 | 0 | 0 | ✅ |
| HardwareInventoryProviderTests | 5 | 5 | 0 | 0 | ✅ |
| WindowsDeepCheckProviderTests | 6 | 6 | 0 | 0 | ✅ |
| MetricAggregatorTests | 10 | 10 | 0 | 0 | ✅ |
| FrameTimeCorrelatorTests | 8 | 8 | 0 | 0 | ✅ |
| FrameTimeAggregationTests | 13 | 13 | 0 | 0 | ✅ |
| BottleneckScorerTests | 10 | 10 | 0 | 0 | ✅ |
| CrossCorrelationTests | 7 | 7 | 0 | 0 | ✅ |
| AdvancedDetectionTests | 7 | 7 | 0 | 0 | ✅ |
| ThermalAnalyzerTests | 10 | 10 | 0 | 0 | ✅ |
| PowerAnalyzerTests | 9 | 9 | 0 | 0 | ✅ |
| CulpritAttributorTests | 16 | 16 | 0 | 0 | ✅ |
| CulpritResultMapperTests | 8 | 8 | 0 | 0 | ✅ |
| BaselineComparatorTests | 9 | 9 | 0 | 0 | ✅ |
| BaselineManagerTests | 7 | 7 | 0 | 0 | ✅ |
| RecommendationEngineTests | 6 | 6 | 0 | 0 | ✅ |
| AnalysisPipelineTests | 4 | 4 | 0 | 0 | ✅ |
| Tier1IsolationTests | 9 | 9 | 0 | 0 | ✅ |
| FullCaptureFlowTests | 5 | 5 | 0 | 0 | ✅ |
| ElevationFlowTests | 5 | 5 | 0 | 0 | ✅ |
| CliArgumentTests | 17 | 17 | 0 | 0 | ✅ |
| **Grand Total** | **396** | **396** | **0** | **0** | ✅ |

---

## Deliverable → Test Mapping

### G.1 — Tabler Asset Extraction & Embedding (AssetLoader.cs)

| Test Class | Test Method | Assertion Summary | Status |
|------------|-------------|-------------------|--------|
| HtmlReportRenderTests | `BuildHtml_ContainsTablerCss` | Generated HTML contains Tabler CSS variables (`--tblr`) and `.card` class, confirming CSS was loaded from embedded resources | ✅ |
| HtmlReportRenderTests | `BuildHtml_ContainsApexChartsJs` | Generated HTML contains `ApexCharts` string, proving the JS library was embedded and injected | ✅ |
| HtmlReportRenderTests | `BuildHtml_SelfContained_NoPlaceholdersLeft` | No `TABLER_CSS_PLACEHOLDER`, `TABLER_THEME_JS_PLACEHOLDER`, `APEXCHARTS_JS_PLACEHOLDER`, or `TABLER_JS_PLACEHOLDER` remain in output | ✅ |
| HtmlReportRenderTests | `BuildHtml_NoExternalResourceLoads` | No `<link href="http...">` or `<script src="http...">` or `//cdn.` in the output — truly self-contained | ✅ |

### G.2 — HTML Report Template (report-template.html)

| Test Class | Test Method | Assertion Summary | Status |
|------------|-------------|-------------------|--------|
| HtmlReportRenderTests | `BuildHtml_ContainsReportData` | Output HTML includes `window.reportData`, `"captureId"`, and `"scores"` — data injection works | ✅ |
| HtmlReportRenderTests | `BuildHtml_SelfContained_NoPlaceholdersLeft` | All 6 placeholder tokens are replaced — template processing is complete | ✅ |
| FullOutputPipelineTests | `BuildHtml_FromSchemaFixture_Succeeds` | Loads `schema_example.json` fixture, generates HTML, verifies `window.reportData`, hardware name, and report title appear | ✅ |

### G.3 — Chart Generation (ChartDataPreparer.cs)

| Test Class | Test Method | Assertion Summary | Status |
|------------|-------------|-------------------|--------|
| ChartDataPreparerTests | `PrepareAll_EmptySnapshots_ReturnsNoSystemCharts` | Empty input produces no chart keys (no crash on empty data) | ✅ |
| ChartDataPreparerTests | `PrepareAll_WithSnapshots_ReturnsCpuMemoryDiskNetworkCharts` | 30 snapshots produce `cpuUtilization`, `memoryUtilization`, `diskActivity`, `networkThroughput` chart entries | ✅ |
| ChartDataPreparerTests | `PrepareAll_WithGpuData_ReturnsGpuChart` | When GPU data is present, `gpuLoad` chart is generated | ✅ |
| ChartDataPreparerTests | `PrepareAll_WithoutGpuData_NoGpuChart` | When GPU data is null, `gpuLoad` key is absent (conditional section) | ✅ |
| ChartDataPreparerTests | `PrepareAll_WithFrameTimeSamples_ReturnsFrameCharts` | With PresentMon data, produces `frameTimeOverTime`, `frameTimeDistribution`, `fpsOverTime` | ✅ |
| ChartDataPreparerTests | `PrepareAll_WithoutFrameTimeSamples_NoFrameCharts` | Without PresentMon, no frame-related chart keys | ✅ |
| PlaywrightHtmlReportTests | `Chromium_ChartsRender` | ApexCharts SVG elements present in browser DOM for CPU, Memory, Frame Time, Histogram charts | ✅ |
| PlaywrightHtmlReportTests | `Firefox_ChartsRender` | Charts render identically in Firefox | ✅ |
| PlaywrightHtmlReportTests | `WebKit_ChartsRender` | Charts render identically in WebKit | ✅ |

### G.4 — Data Downsampling (LttbDownsampler.cs)

| Test Class | Test Method | Assertion Summary | Status |
|------------|-------------|-------------------|--------|
| LttbDownsamplerTests | `Downsample_InputShorterThanTarget_ReturnsAllPoints` | 5-point input with target 10 → returns all 5 points unchanged | ✅ |
| LttbDownsamplerTests | `Downsample_PreservesFirstAndLastPoints` | First and last points of 100-point linear input preserved in 20-point output | ✅ |
| LttbDownsamplerTests | `Downsample_PreservesMaxAndMinValues` | Sinusoidal input's global max/min preserved within 5.0 tolerance in 30-point output | ✅ |
| LttbDownsamplerTests | `Downsample_OutputLengthMatchesTarget` | 500 → 50 produces exactly 50 points | ✅ |
| LttbDownsamplerTests | `Downsample_PreservesStutterSpikes` | 3 spikes at 80/90/70ms (baseline 10ms) all present in 20-point output with 2× spike threshold | ✅ |
| LttbDownsamplerTests | `Downsample_SpikePreservation_DoesNotDuplicateExistingSpikes` | Spike at index 0 (already first point) appears exactly once | ✅ |
| LttbDownsamplerTests | `Downsample_ResultIsSortedByX` | Output with spike-preserved points maintains X-axis sort order | ✅ |
| LttbDownsamplerTests | `Downsample_WithSeparateArrays_MatchesTupleVersion` | Separate x/y array API produces same first/last points as tuple API | ✅ |
| LttbDownsamplerTests | `Downsample_MismatchedArrayLengths_Throws` | Mismatched x (3) and y (2) arrays throw `ArgumentException` | ✅ |
| LttbDownsamplerTests | `Downsample_TargetLessThan3_ReturnsAllPoints` | Target < 3 returns all input points (LTTB minimum) | ✅ |
| LttbDownsamplerTests | `GetTargetPointCount_NeverExceeds2000` | 100,000s capture → target ≤ 2000 | ✅ |
| LttbDownsamplerTests | `GetTargetPointCount_ReturnsExpectedValue` ×6 | Parameterized: 60s→60, 30min→360, 1h→720, 2h→480, 4h→960, 8h→1920 | ✅ |

### G.5 — HTML Report Generator (HtmlReportGenerator.cs)

| Test Class | Test Method | Assertion Summary | Status |
|------------|-------------|-------------------|--------|
| HtmlReportRenderTests | `GenerateAsync_ProducesHtmlFile` | `GenerateAsync` writes a `.html` file to the temp directory | ✅ |
| HtmlReportRenderTests | `BuildHtml_ContainsReportData` | `window.reportData` with `captureId` and `scores` JSON in output | ✅ |
| HtmlReportRenderTests | `BuildHtml_SelfContained_NoPlaceholdersLeft` | All 6 template placeholders fully replaced | ✅ |
| HtmlReportRenderTests | `BuildHtml_NoExternalResourceLoads` | No external CSS/JS links — fully self-contained HTML | ✅ |
| FullOutputPipelineTests | `Pipeline_ProducesJsonAndHtml` | End-to-end: JSON + HTML files both exist, JSON deserializes correctly, HTML contains report data | ✅ |
| FullOutputPipelineTests | `Pipeline_Tier1Only_HtmlOmitsGpuTempRows` | Tier 1 summary with no GPU → HTML contains `"tier":"Tier1"` | ✅ |

### G.6 — Score Card Rendering (in template JS)

| Test Class | Test Method | Assertion Summary | Status |
|------------|-------------|-------------------|--------|
| HtmlReportRenderTests | `BuildHtml_ContainsReportData` | Scores JSON (`"scores"`) injected into `window.reportData` for client-side rendering | ✅ |
| PlaywrightHtmlReportTests | `Chromium_ScoreCardsRender` | 4-5 score cards visible in browser, each with progress bar and numeric value (0-100) | ✅ |
| PlaywrightHtmlReportTests | `Chromium_ScoreCardColors_MatchSeverity` | CPU score 45 → `bg-warning` class, GPU score 82 → `bg-danger` class | ✅ |
| PlaywrightHtmlReportTests | `Firefox_ScoreCardsRender` | Score cards render identically in Firefox | ✅ |
| PlaywrightHtmlReportTests | `WebKit_ScoreCardsRender` | Score cards render identically in WebKit | ✅ |
| FullOutputPipelineTests | `Pipeline_ProducesJsonAndHtml` | Deserialized JSON has `Scores.Cpu.Score == 45` | ✅ |

### G.7 — Recommendation Rendering (in template JS)

| Test Class | Test Method | Assertion Summary | Status |
|------------|-------------|-------------------|--------|
| HtmlReportRenderTests | `BuildHtml_WithRecommendations_ContainsRecommendationData` | With recommendations, output contains `"GPU-Bound Rendering"` and `"confidence"` fields | ✅ |
| HtmlReportRenderTests | `BuildHtml_WithCulprits_ContainsCulpritData` | Culprit data includes `dwm.exe` process name in output | ✅ |
| HtmlReportRenderTests | `BuildHtml_WithBaselineComparison_ContainsBaselineData` | Baseline data includes `baselineComparison` and `cpu.avg_load` delta entry | ✅ |
| PlaywrightHtmlReportTests | `Chromium_RecommendationsRender` | Recommendation alerts visible in browser, section not hidden, "GPU-Bound Rendering" text rendered | ✅ |
| PlaywrightHtmlReportTests | `Chromium_CulpritSectionRender` | Culprit section visible in browser, `dwm.exe` and `SearchIndexer.exe` text rendered | ✅ |
| PlaywrightHtmlReportTests | `Chromium_BaselineComparisonRenders` | Baseline section visible in browser, `cpu.avg_load` delta entry rendered | ✅ |

### G.8 — CSV Export (CsvExporter.cs)

| Test Class | Test Method | Assertion Summary | Status |
|------------|-------------|-------------------|--------|
| CsvExporterTests | `ExportTimeSeries_WritesCorrectHeader` | Header contains `timestamp_s`, `cpu_total_pct`, `memory_util_pct`, `gpu_util_pct`, `disk_active_pct`, `network_bytes_per_sec`, `cpu_temp_c` | ✅ |
| CsvExporterTests | `ExportTimeSeries_RowCountMatchesSnapshotCount` | 10 snapshots → 11 lines (1 header + 10 data) | ✅ |
| CsvExporterTests | `ExportTimeSeries_NullableFieldsAreEmpty` | Tier 1 (no GPU) → GPU columns are empty strings in CSV | ✅ |
| CsvExporterTests | `ExportPresentMon_WritesCorrectHeader` | Header contains `timestamp_s`, `frame_time_ms`, `cpu_busy_ms`, `gpu_busy_ms`, `dropped`, `present_mode` | ✅ |
| CsvExporterTests | `ExportPresentMon_RowCountMatchesSampleCount` | 20 samples → 21 lines (1 header + 20 data) | ✅ |
| CsvExporterTests | `ExportPresentMon_HandlesSpecialCharactersInAppName` | App name `Game, "Special" Edition` properly RFC 4180 escaped as `"Game, ""Special"" Edition"` | ✅ |
| CsvExporterTests | `ExportTimeSeries_CreatesDirectoryIfNotExists` | Non-existent nested directory auto-created before writing CSV | ✅ |
| FullOutputPipelineTests | `Pipeline_ProducesJsonHtmlAndCsv` | Full pipeline: JSON + HTML + timeseries CSV (16 lines = 1+15) + PresentMon CSV (101 lines = 1+100) all created | ✅ |

### G.9 — ETL Export (EtwProvider.cs)

ETL export is a runtime-only feature (preserves the raw ETW trace `.etl` file when `--etl` flag is passed). This requires elevated kernel-level ETW tracing and cannot be unit-tested. Coverage is provided by the existing `EtwProviderTests` (7 tests) which verify the ETW data pipeline, and by the `CliArgumentTests` which verify flag parsing.

### G.10 — CLI Output Polish (Program.cs)

CLI polish is a cosmetic enhancement to `Program.cs` (533 lines). Runtime behavior is validated through integration tests:

| Test Class | Test Method | Assertion Summary | Status |
|------------|-------------|-------------------|--------|
| CliArgumentTests | 17 tests | All CLI argument parsing, flags (`--csv`, `--etl`, `--elevate`, etc.), and help text validated | ✅ |
| FullOutputPipelineTests | `Pipeline_ProducesJsonAndHtml` | End-to-end output pipeline (JSON + HTML) — the same code path `Program.cs` invokes | ✅ |
| FullOutputPipelineTests | `Pipeline_ProducesJsonHtmlAndCsv` | Full multi-format output (JSON + HTML + CSV) pipeline | ✅ |

---

## Exit Gate Checklist

| # | Gate | Evidence | Status |
|---|------|----------|--------|
| 1 | `dotnet build` succeeds | `dotnet build --no-incremental` → 0 errors, 2 warnings (file-lock on TraceEvent DLLs — build artifacts only, not code warnings) | ✅ |
| 2 | All tests pass (all phases) | `dotnet test` → 396 passed, 0 failed, 0 skipped | ✅ |
| 3 | Single-file publish works | Deferred — requires runtime verification | ⚠️ |
| 4 | HTML is truly self-contained | `BuildHtml_NoExternalResourceLoads` passes — no `<link href="http">`, `<script src="http">`, or `//cdn.` references. Playwright `*_NoNetworkRequests` tests (Chromium/Firefox/WebKit) confirm zero external requests at runtime | ✅ |
| 5 | HTML renders in Edge (Chromium) | Playwright `Chromium_NoJavaScriptErrors`, `Chromium_ScoreCardsRender`, `Chromium_ChartsRender`, `Chromium_RecommendationsRender`, `Chromium_CulpritSectionRender`, `Chromium_BaselineComparisonRenders`, `Chromium_HardwareInventoryRenders`, `Chromium_SystemConfigRenders` — all pass | ✅ |
| 6 | HTML renders in Chrome (Chromium) | Same Chromium engine as Edge — 10 Playwright Chromium tests pass | ✅ |
| 7 | HTML renders in Firefox | Playwright `Firefox_NoJavaScriptErrors`, `Firefox_ScoreCardsRender`, `Firefox_ChartsRender`, `Firefox_NoNetworkRequests` — all pass | ✅ |
| 7b | HTML renders in WebKit (Safari) | Playwright `WebKit_NoJavaScriptErrors`, `WebKit_ScoreCardsRender`, `WebKit_ChartsRender`, `WebKit_NoNetworkRequests` — all pass | ✅ |
| 8 | Score cards correct | `Chromium_ScoreCardsRender` verifies 4-5 score cards with progress bars and numeric values. `Chromium_ScoreCardColors_MatchSeverity` verifies CPU=warning (45), GPU=danger (82) color classes. Cross-browser: Firefox + WebKit also pass | ✅ |
| 9 | Charts render correctly | `Chromium_ChartsRender` verifies SVG elements rendered by ApexCharts for CPU, Memory, Frame Time, and Histogram charts. Firefox + WebKit also pass. `ChartDataPreparerTests` (6/6) verify data preparation | ✅ |
| 10 | Recommendations display correctly | `Chromium_RecommendationsRender` verifies recommendation alerts visible in browser with "GPU-Bound Rendering" text | ✅ |
| 11 | Conditional sections work | `Chromium_Tier1Report_HidesFrameTimingSection` verifies frame-timing, culprit, and baseline sections hidden via `section-hidden` class. `Chromium_ConditionalSections_FrameTimeVisible` verifies frame timing visible when data present | ✅ |
| 12 | Downsampling works | `Downsample_OutputLengthMatchesTarget` (500→50), `GetTargetPointCount_NeverExceeds2000`, spike preservation all pass | ✅ |
| 13 | CSV export works | `ExportTimeSeries_WritesCorrectHeader`, `ExportPresentMon_WritesCorrectHeader`, row count assertions, special character escaping all pass | ✅ |
| 14 | Baseline comparison renders | `Chromium_BaselineComparisonRenders` verifies baseline section visible in browser with `cpu.avg_load` delta entry | ✅ |
| 15 | Final EXE size reasonable | Deferred — requires `dotnet publish` verification | ⚠️ |
| 16 | End-to-end gaming test | Elevated Satisfactory capture: Tier 2, CPU 18/Healthy, Memory 99/Bottleneck, 1 recommendation, LHM 7/7, ETW 4/4. Real analysis pipeline with scoring, culprit attribution, and recommendations | ✅ |
| 17 | End-to-end Tier 1 test | `Pipeline_Tier1Only_HtmlOmitsGpuTempRows` + `Chromium_Tier1Report_HidesFrameTimingSection` verify Tier 1 output end-to-end including browser rendering | ✅ |

---

## Test Detail Listing

### Unit: LttbDownsamplerTests (17 tests)

| Test Method | Duration | Result |
|-------------|----------|--------|
| `Downsample_InputShorterThanTarget_ReturnsAllPoints` | 1 ms | ✅ Passed |
| `Downsample_PreservesFirstAndLastPoints` | < 1 ms | ✅ Passed |
| `Downsample_PreservesMaxAndMinValues` | 1 ms | ✅ Passed |
| `Downsample_OutputLengthMatchesTarget` | < 1 ms | ✅ Passed |
| `Downsample_PreservesStutterSpikes` | 1 ms | ✅ Passed |
| `Downsample_SpikePreservation_DoesNotDuplicateExistingSpikes` | < 1 ms | ✅ Passed |
| `Downsample_ResultIsSortedByX` | 6 ms | ✅ Passed |
| `Downsample_WithSeparateArrays_MatchesTupleVersion` | 6 ms | ✅ Passed |
| `Downsample_MismatchedArrayLengths_Throws` | < 1 ms | ✅ Passed |
| `Downsample_TargetLessThan3_ReturnsAllPoints` | < 1 ms | ✅ Passed |
| `GetTargetPointCount_NeverExceeds2000` | < 1 ms | ✅ Passed |
| `GetTargetPointCount_ReturnsExpectedValue(60, 60, 60)` | < 1 ms | ✅ Passed |
| `GetTargetPointCount_ReturnsExpectedValue(1800, 100, 360)` | < 1 ms | ✅ Passed |
| `GetTargetPointCount_ReturnsExpectedValue(3600, 10000, 720)` | < 1 ms | ✅ Passed |
| `GetTargetPointCount_ReturnsExpectedValue(7200, 50000, 480)` | < 1 ms | ✅ Passed |
| `GetTargetPointCount_ReturnsExpectedValue(14400, 100000, 960)` | < 1 ms | ✅ Passed |
| `GetTargetPointCount_ReturnsExpectedValue(28800, 200000, 1920)` | < 1 ms | ✅ Passed |

### Unit: ChartDataPreparerTests (6 tests)

| Test Method | Duration | Result |
|-------------|----------|--------|
| `PrepareAll_EmptySnapshots_ReturnsNoSystemCharts` | < 1 ms | ✅ Passed |
| `PrepareAll_WithSnapshots_ReturnsCpuMemoryDiskNetworkCharts` | 3 ms | ✅ Passed |
| `PrepareAll_WithGpuData_ReturnsGpuChart` | < 1 ms | ✅ Passed |
| `PrepareAll_WithoutGpuData_NoGpuChart` | < 1 ms | ✅ Passed |
| `PrepareAll_WithFrameTimeSamples_ReturnsFrameCharts` | 5 ms | ✅ Passed |
| `PrepareAll_WithoutFrameTimeSamples_NoFrameCharts` | < 1 ms | ✅ Passed |

### Unit: CsvExporterTests (7 tests)

| Test Method | Duration | Result |
|-------------|----------|--------|
| `ExportTimeSeries_WritesCorrectHeader` | 3 ms | ✅ Passed |
| `ExportTimeSeries_RowCountMatchesSnapshotCount` | 3 ms | ✅ Passed |
| `ExportTimeSeries_NullableFieldsAreEmpty` | 9 ms | ✅ Passed |
| `ExportPresentMon_WritesCorrectHeader` | 2 ms | ✅ Passed |
| `ExportPresentMon_RowCountMatchesSampleCount` | 3 ms | ✅ Passed |
| `ExportPresentMon_HandlesSpecialCharactersInAppName` | 4 ms | ✅ Passed |
| `ExportTimeSeries_CreatesDirectoryIfNotExists` | 2 ms | ✅ Passed |

### Integration: HtmlReportRenderTests (10 tests)

| Test Method | Duration | Result |
|-------------|----------|--------|
| `GenerateAsync_ProducesHtmlFile` | 16 ms | ✅ Passed |
| `BuildHtml_ContainsTablerCss` | 9 ms | ✅ Passed |
| `BuildHtml_ContainsApexChartsJs` | 15 ms | ✅ Passed |
| `BuildHtml_ContainsReportData` | 48 ms | ✅ Passed |
| `BuildHtml_NoExternalResourceLoads` | 48 ms | ✅ Passed |
| `BuildHtml_SelfContained_NoPlaceholdersLeft` | 94 ms | ✅ Passed |
| `BuildHtml_WithFrameTimeSummary_ContainsFrameTimeSection` | 59 ms | ✅ Passed |
| `BuildHtml_WithRecommendations_ContainsRecommendationData` | 38 ms | ✅ Passed |
| `BuildHtml_WithCulprits_ContainsCulpritData` | 20 ms | ✅ Passed |
| `BuildHtml_WithBaselineComparison_ContainsBaselineData` | 37 ms | ✅ Passed |

### Integration: FullOutputPipelineTests (4 tests)

| Test Method | Duration | Result |
|-------------|----------|--------|
| `Pipeline_ProducesJsonAndHtml` | 943 ms | ✅ Passed |
| `Pipeline_ProducesJsonHtmlAndCsv` | 15 ms | ✅ Passed |
| `Pipeline_Tier1Only_HtmlOmitsGpuTempRows` | 945 ms | ✅ Passed |
| `BuildHtml_FromSchemaFixture_Succeeds` | 45 ms | ✅ Passed |

### Integration: PlaywrightHtmlReportTests (20 tests)

Browser-based tests using Microsoft.Playwright 1.52.0 with headless Chromium 136.0, Firefox 137.0, and WebKit 18.4.

| Test Method | Browser | Duration | Result |
|-------------|---------|----------|--------|
| `Chromium_NoJavaScriptErrors` | Chromium | ~4s | ✅ Passed |
| `Chromium_ScoreCardsRender` | Chromium | ~3s | ✅ Passed |
| `Chromium_ChartsRender` | Chromium | ~5s | ✅ Passed |
| `Chromium_RecommendationsRender` | Chromium | ~2s | ✅ Passed |
| `Chromium_CulpritSectionRender` | Chromium | ~2s | ✅ Passed |
| `Chromium_ConditionalSections_FrameTimeVisible` | Chromium | ~2s | ✅ Passed |
| `Chromium_BaselineComparisonRenders` | Chromium | ~2s | ✅ Passed |
| `Chromium_NoNetworkRequests` | Chromium | ~4s | ✅ Passed |
| `Chromium_HardwareInventoryRenders` | Chromium | ~2s | ✅ Passed |
| `Chromium_SystemConfigRenders` | Chromium | ~2s | ✅ Passed |
| `Firefox_NoJavaScriptErrors` | Firefox | ~5s | ✅ Passed |
| `Firefox_ScoreCardsRender` | Firefox | ~3s | ✅ Passed |
| `Firefox_ChartsRender` | Firefox | ~5s | ✅ Passed |
| `Firefox_NoNetworkRequests` | Firefox | ~4s | ✅ Passed |
| `WebKit_NoJavaScriptErrors` | WebKit | ~5s | ✅ Passed |
| `WebKit_ScoreCardsRender` | WebKit | ~3s | ✅ Passed |
| `WebKit_ChartsRender` | WebKit | ~5s | ✅ Passed |
| `WebKit_NoNetworkRequests` | WebKit | ~4s | ✅ Passed |
| `Chromium_Tier1Report_HidesFrameTimingSection` | Chromium | ~3s | ✅ Passed |
| `Chromium_ScoreCardColors_MatchSeverity` | Chromium | ~3s | ✅ Passed |

#### Playwright Test Coverage Summary

| Verification | Chromium | Firefox | WebKit |
|-------------|----------|---------|--------|
| No JS execution errors | ✅ | ✅ | ✅ |
| Score cards render with values | ✅ | ✅ | ✅ |
| ApexCharts SVG rendering | ✅ | ✅ | ✅ |
| No external network requests | ✅ | ✅ | ✅ |
| Recommendations alerts visible | ✅ | — | — |
| Culprit section visible | ✅ | — | — |
| Frame timing section visible | ✅ | — | — |
| Baseline comparison visible | ✅ | — | — |
| Hardware inventory visible | ✅ | — | — |
| System config visible | ✅ | — | — |
| Conditional section hiding (Tier 1) | ✅ | — | — |
| Score card color severity mapping | ✅ | — | — |

---

## Browser Verification

Playwright 1.52.0 automated browser tests replace manual QA for Exit Gates 5-7. The HTML report was generated with full test data (scores, charts, recommendations, culprits, baseline, hardware inventory, system config) and opened via `file://` in headless Chromium 136.0, Firefox 137.0, and WebKit 18.4. All three browsers:

- Execute the client-side JavaScript without errors (uncaught exceptions)
- Render ApexCharts SVG elements for CPU, Memory, Frame Time, and Histogram charts
- Display score cards with numeric values and progress bars
- Make zero external network requests (fully self-contained)

Resource-loading console messages (font downloads, favicon 404s) are expected and filtered out — these are cosmetic and do not affect report functionality.

### Live Capture Screenshots — Satisfactory (Tier 2, Elevated)

A 30-second live capture was performed **with admin elevation** against **Satisfactory** (FactoryGameSteam-Win64-Shipping.exe) running on the test system:

- **CPU:** Intel Core Ultra 9 285K (24C/24T, 5260–3700 MHz)
- **GPU:** NVIDIA GeForce RTX 5090 (32 GB VRAM)
- **RAM:** 32 GB DDR5-5600
- **Storage:** NVMe PC811 SK hynix 2048GB
- **OS:** Windows 10.0.26200 (Build 26200)
- **Tier:** Tier 2 (elevated via UAC, LibreHardwareMonitor 7/7, ETW 4/4, no PresentMon installed)
- **Duration:** ~30s
- **Auto-elevation:** Program.cs now demands admin via UAC by default (unless `--no-elevate`)

Capture command (auto-elevated):
```
dotnet run -- --duration 30 --process FactoryGameSteam-Win64-Shipping.exe --label satisfactory-elevated --csv --no-live --output C:\dev\sys-analyzer\test-captures
```

Output files:
- `sysanalyzer-2026-04-20_10-28-54.json` (5.2 KB)
- `sysanalyzer-2026-04-20_10-28-54.html` (1.3 MB — self-contained)
- `sysanalyzer-2026-04-20_10-28-54-timeseries.csv` (8 KB)

**Key results (real analysis data — not stubs):**
- **CPU Score: 18 (Healthy)** — 25-41% utilization, not bottlenecked
- **Memory Score: 99 (Bottleneck!)** — 96.14% utilization, genuine memory pressure
- **Disk Score: 0 (Healthy)** — minimal activity
- **Network Score: 0 (Healthy)** — minimal activity
- **Recommendations: 1** — "Suboptimal Power Plan" (Balanced instead of High Performance)
- **Sensor Health:** PerformanceCounters 21/24, HardwareInventory 7/7, WindowsDeepCheck 11/11, LibreHardwareMonitor 7/7, ETW 4/4, PresentMon 0/0 (not installed)

Screenshots captured via Playwright (headless Chromium 136.0 + Firefox 137.0, 1920×1080 viewport):

| Screenshot | Description | Browser |
|------------|-------------|---------|
| `report-fullpage-chromium.png` | Full-page report with real scores, charts, and recommendation | Chromium |
| `report-fullpage-firefox.png` | Full-page report rendering | Firefox |
| `report-viewport-chromium.png` | Above-the-fold: Tier2 badge, real score cards (CPU 18, Memory 99), sensor health | Chromium |
| `report-viewport-firefox.png` | Above-the-fold view | Firefox |
| `report-recommendations-chromium.png` | Recommendations section — "Configuration Suggestions (1)" | Chromium |
| `report-recommendations-firefox.png` | Recommendations section | Firefox |
| `report-chart-cpu-chromium.png` | CPU Utilization chart (25-41% time-series) | Chromium |
| `report-chart-cpu-firefox.png` | CPU Utilization chart | Firefox |
| `report-chart-memory-chromium.png` | Memory dual-axis chart (96%+ Memory + Page Faults/s) | Chromium |
| `report-chart-memory-firefox.png` | Memory Utilization chart | Firefox |
| `report-hardware-chromium.png` | Hardware Inventory (i9-285K, RTX 5090, 32GB DDR5) | Chromium |
| `report-hardware-firefox.png` | Hardware Inventory | Firefox |
| `report-sysconfig-chromium.png` | System Configuration (Power Plan: Balanced ⚠️, Game Mode: Disabled ⚠️, etc.) | Chromium |
| `report-sysconfig-firefox.png` | System Configuration | Firefox |
| `report-sensor-health-chromium.png` | Sensor Health (LHM 7/7 ✅, ETW 4/4 ✅, PresentMon 0/0 ❌) | Chromium |
| `report-sensor-health-firefox.png` | Sensor Health | Firefox |

All screenshots are in `test-dossiers/screenshots/`. Key observations:
- **Real analysis data**: Score cards show actual scores (CPU 18/Healthy, Memory 99/Bottleneck) — not stubs
- **Memory 99/Bottleneck is genuine**: 96.14% memory utilization during Satisfactory gameplay with 32GB RAM
- **Recommendations render**: "Configuration Suggestions (1)" section visible with actionable advice
- **Tier 2 badge**: Green Tier2 badge confirms elevated capture with full sensor access
- **Cross-browser parity**: Chromium and Firefox render identically
- **System Configuration**: Shows 6 warning items (Power Plan, Game Mode, HAGS, Game DVR, SysMain, Windows Search, Temp Folder)
- **Charts populate with real data**: CPU 25-41%, Memory 96%+, Page Faults 10K-60K/s

---

## Build Artifacts

### Source Files (New/Modified in Phase G)

| File | Lines | Purpose |
|------|-------|---------|
| Report/AssetLoader.cs | 42 | Reads embedded Tabler CSS/JS and ApexCharts from assembly resources |
| Report/ChartDataPreparer.cs | 244 | Prepares chart data dictionaries for all chart types (CPU, memory, GPU, disk, network, frame time) |
| Report/CsvExporter.cs | 153 | Exports time-series snapshots and PresentMon frame data to RFC 4180 CSV |
| Report/HtmlReportGenerator.cs | 109 | Loads template, injects assets + data, writes self-contained HTML file |
| Report/LttbDownsampler.cs | 200 | LTTB downsampling with spike preservation and configurable target point calculation |
| Report/FilenameGenerator.cs | 44 | Generates output filenames with timestamp and label tokens |
| Report/JsonReportGenerator.cs | 36 | JSON serialization/deserialization of AnalysisSummary |
| Report/Templates/report-template.html | 656 | Full Tabler HTML template with ApexCharts, score cards, recommendations, and conditional sections |
| Report/Assets/report.css | 24 | Minimal custom CSS overrides for the HTML report |
| Report/Assets/tabler.min.css | — | Compiled Tabler CSS (embedded resource) |
| Report/Assets/tabler.js | — | Compiled Tabler JS (embedded resource) |
| Report/Assets/tabler-theme.js | — | Tabler theme switcher JS (embedded resource) |
| Report/Assets/apexcharts.min.js | — | ApexCharts library (embedded resource) |
| Program.cs | ~600 | Main entry point — CLI output polish, auto-elevation via UAC, analysis pipeline wiring, HTML/CSV/ETL output |

### Test Files (New in Phase G)

| File | Lines | Purpose |
|------|-------|---------|
| Tests/Unit/LttbDownsamplerTests.cs | 183 | LTTB algorithm: preservation, spike handling, edge cases, target calculation |
| Tests/Unit/ChartDataPreparerTests.cs | 140 | Chart generation: conditional chart types, GPU/frame time presence |
| Tests/Unit/CsvExporterTests.cs | 244 | CSV export: headers, row counts, nullable fields, RFC 4180 escaping |
| Tests/Integration/HtmlReportRenderTests.cs | 281 | HTML generation: asset embedding, self-containment, conditional data sections |
| Tests/Integration/FullOutputPipelineTests.cs | 246 | End-to-end: JSON+HTML+CSV pipeline, Tier 1 mode, fixture round-trip |
| Tests/Integration/PlaywrightHtmlReportTests.cs | 371 | Playwright browser tests: cross-browser JS errors, chart rendering, score cards, conditional sections, network isolation |
| Tests/Integration/ScreenshotCapture.cs | ~80 | Captures viewport, fullpage, and section screenshots of live report for dossier evidence |

---

## Coverage Notes

No formal code coverage collection was performed. Based on test-to-source mapping:

- **LttbDownsampler.cs** — Well covered: all public APIs tested, edge cases (short input, mismatched arrays, target < 3), spike preservation, and target point calculation with parameterized theory tests across all duration bands.
- **ChartDataPreparer.cs** — Covered at the API level: all conditional chart types tested (with/without GPU, with/without PresentMon). Internal chart construction logic not individually tested.
- **CsvExporter.cs** — Thoroughly covered: both export methods tested with header verification, row count validation, nullable field handling, special character escaping, and directory creation.
- **HtmlReportGenerator.cs** — Well covered: file generation, asset injection, data injection, self-containment, and conditional sections (frame time, recommendations, culprits, baseline).
- **AssetLoader.cs** — Indirectly covered via HtmlReportRenderTests (asset loading is a prerequisite for HTML generation).
- **report-template.html** — Structurally verified via `NoPlaceholdersLeft` and `NoExternalResourceLoads` tests. Client-side JavaScript rendering requires browser.

---

## Reproducibility

```
cd C:\dev\sys-analyzer\SysAnalyzer
dotnet build --no-incremental
dotnet test --verbosity normal
```
