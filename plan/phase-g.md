# Phase G: HTML Report & Visual Output

**Goal**: Generate a self-contained, single-file HTML report using Tabler + ApexCharts. The HTML is a visual presentation layer over the stable JSON schema — no new analysis, just rendering. Also finalize CSV/ETL optional exports and polish the CLI output.

**Key risk addressed**: The HTML must be truly self-contained (no external URLs, no CDN), render correctly across browsers (Edge, Chrome, Firefox), and handle large datasets (multi-hour captures) without browser performance issues. Tabler asset extraction and embedding must work at build time.

---

## Entry Gates

| # | Gate | Verification |
|---|------|-------------|
| 1 | Phase E + F exit gates all satisfied | Full pipeline produces scored, recommendations-rich JSON with all tiers |
| 2 | JSON schema stable and complete | `AnalysisSummary` contains all sections: scores, frame timing, culprits, recommendations, baseline comparison, hardware inventory, system config, self-overhead |
| 3 | Tabler built | `cd tabler && pnpm install && pnpm build` succeeds. `tabler.min.css`, `tabler.min.js`, `tabler-theme.min.js` exist in build output. |
| 4 | ApexCharts available | `apexcharts.min.js` exists in `node_modules/apexcharts/dist/` (installed via Tabler's dependencies) |
| 5 | LTTB downsampling algorithm tested | Phase A or E unit tests cover the downsampling logic |
| 6 | Real capture data exists | At least 3 real captures: a gaming session (with PresentMon + ETW), a Tier 1 only capture, and a short (<5 min) capture |

---

## Deliverables

### G.1 — Tabler Asset Extraction & Embedding (§7.7)

1. Build Tabler assets:
   - Run `pnpm build` in `c:\dev\sys-analyzer\tabler\` to produce compiled CSS/JS
   - Locate output files: `tabler.min.css`, `tabler.min.js`, `tabler-theme.min.js`
   - Locate `apexcharts.min.js` from node_modules
2. Embed as .NET resources:
   - Add the 4 files as embedded resources in `SysAnalyzer.csproj`:
     ```xml
     <ItemGroup>
       <EmbeddedResource Include="Report\Assets\tabler.min.css" />
       <EmbeddedResource Include="Report\Assets\tabler.min.js" />
       <EmbeddedResource Include="Report\Assets\tabler-theme.min.js" />
       <EmbeddedResource Include="Report\Assets\apexcharts.min.js" />
     </ItemGroup>
     ```
   - Implement `AssetLoader` that reads embedded resources at report generation time
3. Verify: single-file publish still works. Assets are inside the `.exe`.

### G.2 — HTML Report Template (§7.4)

1. Create `report-template.html` — a complete Tabler page with placeholder markers:
   - `<style>` blocks for Tabler CSS + custom overrides
   - `<script>` blocks for Tabler JS, ApexCharts JS, theme JS
   - `<script>` block for `window.reportData = {JSON_DATA_PLACEHOLDER};` — the full analysis data injected as a JavaScript object
   - HTML structure using Tabler components matching the layout in §7.4:
     - **Header**: Report title, metadata (timestamp, duration, machine, profile, label, tier)
     - **Sensor Health Matrix**: Status badges per provider (Tabler status indicators)
     - **Score Cards**: 4-5 cards with progress bars and color coding (Tabler card component)
     - **Frame Timing Summary**: Metrics panel (if PresentMon data available)
     - **Top Recommendations**: Sorted by priority/confidence, grouped by confidence level (Tabler alert components with severity colors)
     - **Culprit Attribution**: Process/driver table (if ETW data available)
     - **Time-Series Charts**: Placeholder `<div>` elements for ApexCharts rendering
     - **Baseline Comparison**: Delta table (if baseline exists)
     - **Hardware Inventory**: Component list (Tabler list group)
     - **System Configuration Audit**: Checklist with pass/fail icons
     - **Raw Statistics Table**: Expandable/collapsible detailed metrics (Tabler accordion)
     - **Footer**: Version, capture ID, profile, tier, duration
2. Template uses minimal custom CSS — leverage Tabler's existing component classes
3. Conditional sections: hide blocks when data is null (e.g., no PresentMon → hide frame timing section)

### G.3 — Chart Generation (§7.5)

1. Implement chart data preparation for each chart type:

   **Frame Time Over Time** (line chart):
   - Series: frame time in ms, P95 threshold line
   - Annotations: stutter spike markers (vertical lines with cause labels)
   - Custom tooltip: show concurrent CPU/GPU/VRAM at that timestamp
   - X-axis: datetime, format `HH:mm:ss.fff`

   **Frame Time Distribution** (histogram):
   - Buckets: 0-8ms, 8-16ms, 16-33ms, 33-50ms, 50-100ms, 100ms+
   - Annotations: P50, P95, P99 markers
   - Y-axis: logarithmic (frame counts vary hugely across buckets)

   **FPS Over Time** (line chart):
   - Series: instantaneous FPS (1000/frameTime), smoothed average
   - Annotations: P1 FPS line (worst 1%)

   **CPU Utilization** (area chart):
   - Series: per-core utilization + total
   - Threshold annotation at 95%
   - Colors: Tabler palette

   **Memory Utilization** (line chart):
   - Series: memory utilization %, page faults/sec (dual axis)
   - Threshold annotations

   **GPU Load + VRAM** (line chart):
   - Series: GPU utilization %, VRAM utilization %, temperature (if Tier 2)
   - Dual axis: % on left, °C on right

   **Disk Activity** (line chart):
   - Series: disk active %, queue length, read/write latency
   - Threshold annotations

   **Network Throughput** (area chart):
   - Series: bytes/sec (receive + send stacked)
   - Utilization % overlay

2. All charts use:
   - `animations: { enabled: false }` — no animation in a static report
   - `toolbar: { show: false }` — no export buttons
   - Tabler color palette
   - Responsive sizing

### G.4 — Data Downsampling (§7.6)

1. Implement LTTB (Largest-Triangle-Three-Buckets) downsampling:
   - Input: raw time-series array, target point count
   - Output: downsampled array that preserves peaks and valleys
2. Apply downsampling rules:
   - Capture < 30 min: raw data (1s granularity) — no downsampling
   - 30 min – 2 hours: 5-second intervals (~360-1440 points)
   - 2–8 hours: 15-second intervals (~480-1920 points)
3. Frame-time data (from PresentMon at display refresh rate):
   - Always downsample for HTML charts (could be 60-144+ samples/second)
   - Target: ~2000 points per chart maximum
   - Preserve stutter spikes: any frame > 2× median is kept regardless of downsampling
4. Inject downsampled data into the `window.reportData` JavaScript object
5. JSON sidecar contains summary statistics only (not raw time-series)
6. CSV export (`--csv`) contains full raw data at original granularity

**Unit tests**:
- LTTB preserves the maximum and minimum values in the input
- LTTB output length matches target (within ±1)
- Stutter spike preservation: input with 3 spikes → all 3 present in output
- Edge case: input shorter than target → no downsampling applied

### G.5 — HTML Report Generator

1. Implement `HtmlReportGenerator`:
   - Load `report-template.html` from embedded resources
   - Load CSS/JS assets from embedded resources
   - Serialize `AnalysisSummary` + downsampled chart data to JSON
   - Inject into template:
     - Replace CSS/JS placeholders with actual content in `<style>`/`<script>` tags
     - Replace data placeholder with `window.reportData = { ... };`
   - Conditional section visibility:
     - If `FrameTimeSummary == null` → hide frame timing section, hide frame charts
     - If `CulpritAttribution == null` → hide culprit section
     - If `BaselineComparison == null` → hide baseline section
     - If `tier == 1` → hide Tier 2 metric rows in statistics table
   - Write the complete HTML to `{output_dir}/{filename}.html`
2. Verify: open the HTML file in a browser — all content renders, no console errors

### G.6 — Score Card Rendering

1. Map score ranges to Tabler visual elements:
   - 0-25 (Healthy): green progress bar, green badge
   - 26-50 (Moderate): yellow progress bar, yellow badge
   - 51-75 (Stressed): orange progress bar, orange badge
   - 76-100 (Bottleneck): red progress bar, red badge
2. Show delta arrows when baseline comparison exists:
   - Improved: green down arrow with delta value
   - Worsened: red up arrow with delta value
   - Same: gray dash
3. Confidence indicator: small badge on each score card showing data confidence (high/medium/low)

### G.7 — Recommendation Rendering

1. Group recommendations by confidence:
   - **High confidence**: Featured prominently with full evidence text
   - **Medium confidence**: Standard list items
   - **Low confidence**: Collapsible "configuration suggestions" section
2. Color by severity: critical = red alert, warning = yellow alert, info = blue alert
3. Each recommendation shows:
   - Title, severity badge, confidence badge
   - Full body text with resolved placeholders
   - Category badge (cpu/memory/gpu/disk/network/software/config/frametime)
4. Culprit-specific recommendations show process/driver names prominently

### G.8 — CSV Export (§7.1, `--csv`)

1. Implement `CsvExporter`:
   - Export raw time-series `SensorSnapshot` data at 1-second granularity
   - Columns: timestamp, all CPU/Memory/GPU/Disk/Network metrics, Tier 2 metrics (if available)
   - One row per second of capture
   - Filename: `{base}-timeseries.csv`
2. Export PresentMon raw data:
   - Columns: timestamp, app, frameTimeMs, cpuBusyMs, gpuBusyMs, dropped, presentMode
   - One row per frame
   - Filename: `{base}-presentmon.csv`
3. Both files only created when `--csv` flag is passed

### G.9 — ETL Export (`--etl`)

1. When `--etl` flag is passed, preserve the raw ETW trace file
2. The `TraceEventSession` can write to an `.etl` file simultaneously
3. This allows users to open the trace in Windows Performance Analyzer (WPA) for deeper investigation
4. Filename: `{base}.etl`

### G.10 — CLI Output Polish

1. Finalize the stop & report display (§8.2):
   - Show sample count, frame count, duration
   - Show analysis progress (correlation, scoring, recommendations)
   - Show output file paths
   - Show quick summary: top bottleneck, recommendation count, top culprit
2. Finalize the quick summary box:
   - #1 bottleneck subsystem with score and confidence
   - Frame timing headline (avg FPS, P99, stutter count)
   - Top culprit process (if ETW data exists)
3. Exit cleanly with code 0

---

## Exit Gates

| # | Gate | Verification |
|---|------|-------------|
| 1 | `dotnet build` succeeds | 0 errors, 0 warnings |
| 2 | All tests pass (all phases) | `dotnet test` — all green |
| 3 | Single-file publish works | `dotnet publish` produces a single `SysAnalyzer.exe`. Running it with `--duration 10` produces both JSON and HTML output. |
| 4 | HTML is truly self-contained | Open `*.html` in a browser with no network connection (airplane mode). All content renders. No external resource requests in browser DevTools Network tab. |
| 5 | HTML renders in Edge | Open in Microsoft Edge. All charts render. No JavaScript console errors. Scores match JSON. |
| 6 | HTML renders in Chrome | Same verification in Chrome. |
| 7 | HTML renders in Firefox | Same verification in Firefox. |
| 8 | Score cards correct | HTML score card colors match severity thresholds. Values match JSON scores. |
| 9 | Charts render correctly | All chart types render with correct data. Frame-time chart shows stutter spike annotations. CPU chart shows per-core lines. |
| 10 | Recommendations display correctly | All triggered recommendations appear in HTML with correct severity colors, confidence badges, and resolved placeholder text. No raw `{placeholder}` text visible. |
| 11 | Conditional sections work | Tier 1 report (no LHM): no temperature/clock rows. No PresentMon: frame timing section hidden. No ETW: culprit section hidden. No baseline: comparison section hidden. |
| 12 | Downsampling works | Multi-hour capture HTML file is < 5MB. Charts are responsive (no browser lag when scrolling). |
| 13 | CSV export works | `--csv` produces `*-timeseries.csv` and `*-presentmon.csv`. CSV opens in Excel correctly. Row count matches capture duration (for time-series) and frame count (for PresentMon). |
| 14 | Baseline comparison renders | Run with `--compare <prior.json>`. HTML shows delta table with arrows and verdicts. JSON has `delta_from_baseline` populated. |
| 15 | Final EXE size reasonable | Published single-file EXE is < 30MB (runtime + all assets + LHM driver). |
| 16 | End-to-end gaming test | Run a real game for 5+ minutes with `--elevate --profile gaming`. Open HTML report. Verify: sensor health, scores, frame timing, charts, recommendations, hardware inventory all look correct and match the JSON. |
| 17 | End-to-end Tier 1 test | Same game, without `--elevate`. Report correctly shows Tier 1 mode, no temp/clock data, recommendations adjusted. |

---

## Files Created / Modified

```
SysAnalyzer/
├── Report/
│   ├── HtmlReportGenerator.cs           (NEW — template injection, asset embedding)
│   ├── CsvExporter.cs                   (NEW — time-series + PresentMon CSV)
│   ├── AssetLoader.cs                   (NEW — embedded resource reader)
│   ├── ChartDataPreparer.cs             (NEW — downsampling + chart JSON)
│   ├── LttbDownsampler.cs              (NEW — Largest-Triangle-Three-Buckets)
│   ├── Templates/
│   │   └── report-template.html         (NEW — full Tabler HTML template)
│   └── Assets/
│       ├── tabler.min.css               (COPIED from Tabler build)
│       ├── tabler.min.js                (COPIED from Tabler build)
│       ├── tabler-theme.min.js          (COPIED from Tabler build)
│       ├── apexcharts.min.js            (COPIED from node_modules)
│       └── report.css                   (NEW — minimal custom overrides)
├── Capture/
│   └── Providers/
│       └── EtwProvider.cs               (MODIFIED — add ETL file output option)
├── Cli/
│   └── LiveDisplay.cs                   (MODIFIED — final polish)
└── Program.cs                           (MODIFIED — wire HTML + CSV + ETL output)

SysAnalyzer.Tests/
├── Unit/
│   ├── LttbDownsamplerTests.cs          (NEW)
│   ├── ChartDataPreparerTests.cs        (NEW)
│   └── CsvExporterTests.cs              (NEW)
├── Integration/
│   ├── HtmlReportRenderTests.cs         (NEW — generate from fixture, verify no errors)
│   └── FullOutputPipelineTests.cs       (NEW — JSON + HTML + CSV from fixture)
└── Fixtures/
    └── (all existing fixtures reused for HTML generation tests)
```
