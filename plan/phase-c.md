# Phase C: PresentMon Frame-Time Integration

**Goal**: Integrate PresentMon as an external subprocess to capture real frame-time telemetry. This is the single highest-value data source — it transforms SysAnalyzer from "smart system monitor" into "actual stutter diagnostician."

**Key risk addressed**: PresentMon is an external process writing CSV to stdout. Parsing must be real-time, timestamp alignment must be correct, and graceful degradation must work when PresentMon is unavailable or the tracked app exits.

---

## Entry Gates

| # | Gate | Verification |
|---|------|-------------|
| 1 | Phase B exit gates all satisfied | All B exit gates checked and documented |
| 2 | Capture session state machine operational | `--duration 10` produces valid JSON with perf counter data |
| 3 | `QpcTimestamp` model and windowing utilities implemented | Phase A tests pass for timestamp conversion, windowing, nearest-sample |
| 4 | `FrameTimeSample` record defined | Exists with all fields from §4.1 (Phase A deliverable A.4) |
| 5 | PresentMon binary obtained | `PresentMon.exe` (MIT-licensed) downloaded and placed alongside `SysAnalyzer.exe`. Version confirmed with `PresentMon.exe --version`. |
| 6 | PresentMon CSV output format understood | Developer has examined sample CSV output from `PresentMon.exe --output_stdout --no_top` |

---

## Deliverables

### C.1 — PresentMon Provider (§4.1)

1. Implement `PresentMonProvider : IEventStreamProvider`
2. `InitAsync()`:
   - Check `File.Exists("PresentMon.exe")` in the directory adjacent to `SysAnalyzer.exe`
   - If missing: set `Health = Unavailable`, return — all frame-time features disabled
   - Record QPC timestamp at the moment we're about to launch PresentMon (this is the clock offset anchor — see §3.2)
   - Launch `PresentMon.exe --output_stdout --no_top` as a child subprocess
   - If `--process` was specified: add `--process_name <name>` flag
   - Capture stdout pipe
   - Parse header line to map column indices
3. `Events` (`IAsyncEnumerable<TimestampedEvent>`):
   - Read stdout line-by-line on a dedicated background thread
   - Parse each CSV line into a `FrameTimeSample`:
     - `ApplicationName` from `Application` column
     - `FrameTimeMs` from `msBetweenPresents`
     - `CpuBusyMs` from `msCPUBusy` (or `msCPUWait` derived)
     - `GpuBusyMs` from `msGPUBusy` (or `msGPUWait` derived)
     - `Dropped` from `Dropped` column (boolean)
     - `PresentMode` from `PresentMode` column
     - `AllowsTearing` from `AllowsTearing` column
   - Convert PresentMon's time column to `QpcTimestamp` using the offset captured at launch (§3.2):
     ```
     canonicalQpc = rawPmTimestamp - qpcOffset + captureEpoch
     ```
   - Yield each `FrameTimeSample` into the async enumerable
   - Use a bounded channel as the buffer between the reader thread and the consumer
4. `StopAsync()`:
   - Send termination signal to PresentMon subprocess (kill the process)
   - Drain remaining lines from stdout
   - Record final statistics (total frames parsed, gaps, errors)

### C.2 — Foreground App Auto-Detection (§12.2)

1. When `--process` is not specified, PresentMon captures all presenting apps
2. Implement auto-detection logic:
   - Wait up to 10 seconds for CSV rows to appear
   - If no rows after 10 seconds: log warning, set `Health = Degraded`, continue capture without frame data
   - If rows appear from multiple apps: pick the app with the highest frame count in the first 5 seconds
   - Prefer fullscreen apps (`PresentMode = Hardware: Independent Flip`) over windowed
   - If still ambiguous: prefer highest frame rate
   - If the tracked app exits mid-capture: check for a new presenter (game restart scenario)
3. Log the selected app: "Tracking: Cyberpunk2077.exe (auto-detected, fullscreen)"
4. Update live display with tracked app name and live FPS

### C.3 — Timestamp Alignment Verification

1. At PresentMon launch, record:
   - `qpcAtLaunch`: `Stopwatch.GetTimestamp()` immediately before `Process.Start()`
   - `captureEpoch`: the session's epoch QPC (set during `CaptureSession.Init()`)
2. PresentMon's timestamp column is in QPC ticks or seconds-since-start (varies by version). Implement adaptive parsing:
   - If timestamps are in seconds (monotonically increasing from near-zero): multiply by QPC frequency, add `qpcAtLaunch - captureEpoch`
   - If timestamps are raw QPC: subtract `captureEpoch` directly
3. Validate alignment: the first PresentMon sample's canonical timestamp should be close to the elapsed time since capture start (within ±100ms). If it's way off, log a warning: "PresentMon timestamp alignment may be incorrect. Frame-time correlation accuracy reduced."

**Unit tests**:
- Offset calculation: known QPC values → correct canonical timestamp
- Adaptive parsing: seconds-based input → correct conversion
- Adaptive parsing: raw QPC input → correct conversion
- Alignment validation: within tolerance → no warning; outside → warning logged

### C.4 — PresentMon Failure Modes (§12.1)

Implement all failure modes from the degradation matrix:

| Failure Mode | Detection | Implementation |
|-------------|-----------|----------------|
| Binary not found | `File.Exists` at `InitAsync` | Health = Unavailable. All `frametime.*` = null in JSON. |
| No presenting app | No CSV rows after 10s | Health = Degraded. Log warning. Continue capture. |
| Multiple presenting apps | Multiple `Application` values | Use highest-frame-count app (or `--process` override). Note in report. |
| Borderless windowed | `PresentMode = Composed:*` | Detect and note: "App running in composed mode. Frame times include DWM composition latency." |
| Subprocess crashes | Process exit code != 0 | Record crash timestamp. Continue capture without frame data from that point. Note in report: "PresentMon crashed at {time}." |
| Process exits mid-capture | Tracked app stops producing frames | Check for new presenter after 5s gap. If found, switch tracking. If not, continue without frame data. |

**Component tests** (fake subprocess stdout):
- Normal stream → `FrameTimeSample` events flow correctly
- Empty stream for 10s → auto-detection gives up, Health = Degraded
- Two apps in stream → highest frame count selected
- `PresentMode` = `Composed:Flip` → borderless-windowed flag set
- Subprocess exit code 1 → crash recorded, remaining data preserved
- Stream gap then new app → tracking switches

### C.5 — Frame-Time Aggregation

1. After capture stops, compute frame-time summary statistics:
   - Average FPS: `1000.0 / mean(frameTimeMs)`
   - P1 FPS: `1000.0 / P99(frameTimeMs)` (worst 1% of frame times → P1 FPS)
   - Frame time percentiles: P50, P95, P99, P999
   - Dropped frame count and percentage
   - CPU-bound frame percentage: frames where `CpuBusyMs > GpuBusyMs * threshold`
   - GPU-bound frame percentage: frames where `GpuBusyMs > CpuBusyMs * threshold`
   - Stutter spike count: frames where `FrameTimeMs > median * stutter_spike_multiplier` (from config)
   - Present mode: most common mode during capture
   - `AllowsTearing`: whether any tearing was detected
2. Populate the `FrameTimeSummary` section of `AnalysisSummary`
3. Mark frame-time stutter spike timestamps for later correlation (Phase E)

**Unit tests** (deterministic frame-time arrays):
- 60fps steady → avg 16.67ms, P99 near 16.67ms, stutter count = 0
- 60fps with occasional 100ms spikes → P99 high, stutter count correct
- Mixed CPU-bound and GPU-bound frames → percentages correct
- All frames dropped → 100% dropped rate
- Present mode classification (multiple modes → most common)

### C.6 — Update JSON Output

1. Update `JsonReportGenerator` to include `FrameTimeSummary` when PresentMon data exists
2. When PresentMon was unavailable: `frame_timing.available = false`, all sub-fields null
3. When PresentMon crashed mid-capture: `frame_timing.available = true`, data covers partial capture, note in `sensor_health`

### C.7 — Update Live Display

1. Add live FPS counter to console display: `FPS: 62 avg / 34 P1`
2. Show tracked app name: `App: Cyberpunk2077.exe`
3. Show running stutter spike count
4. If PresentMon unavailable: show `FPS: N/A (PresentMon unavailable)`

---

## Exit Gates

| # | Gate | Verification |
|---|------|-------------|
| 1 | `dotnet build` succeeds | 0 errors, 0 warnings |
| 2 | All unit + component tests pass | `dotnet test` — all green |
| 3 | PresentMon launches and captures frame data | Run `SysAnalyzer.exe --duration 15` while a windowed app is running. JSON contains `frame_timing.available = true` with non-null percentiles. |
| 4 | Frame-time percentiles are correct | Compare P99 frame time from SysAnalyzer JSON vs PresentMon's own output (manual check). Values should match within 5%. |
| 5 | Timestamp alignment is correct | Compare frame-time spike timestamps with CPU/memory metric timestamps. A stutter event's frame-time timestamp should fall within the same second's `SensorSnapshot` window. |
| 6 | Auto-detection works | Run without `--process` with a game running. JSON shows the correct process name. |
| 7 | `--process` override works | Run with `--process notepad.exe` (or any windowed app). JSON tracks specified app. |
| 8 | Graceful degradation: no PresentMon | Rename `PresentMon.exe` to `PresentMon.bak`. Run. JSON has `frame_timing.available = false`. No crash. Report notes PresentMon unavailable. |
| 9 | Graceful degradation: no presenting app | Run with no windowed/fullscreen graphical app. JSON has `frame_timing` null or unavailable. Warning in output. |
| 10 | Overhead budget maintained | During a 60-second capture with PresentMon active, total CPU < 1.5%, working set < 100MB. PresentMon subprocess overhead accounted separately. |
| 11 | Bounded channel backpressure | PresentMon at 144Hz (144 samples/sec) doesn't cause unbounded memory growth. Channel drops oldest if consumer falls behind. |

---

## Files Created / Modified

```
SysAnalyzer/
├── Capture/
│   └── Providers/
│       └── PresentMonProvider.cs        (NEW — IEventStreamProvider)
├── Analysis/
│   └── FrameTimeAggregator.cs           (NEW — post-capture stats)
├── Cli/
│   └── LiveDisplay.cs                   (MODIFIED — add FPS display)
└── Report/
    └── JsonReportGenerator.cs           (MODIFIED — add FrameTimeSummary)

SysAnalyzer.Tests/
├── Unit/
│   ├── FrameTimeAggregationTests.cs     (NEW)
│   └── TimestampAlignmentTests.cs       (NEW — PresentMon offset calc)
├── Component/
│   └── PresentMonProviderTests.cs       (NEW — fake subprocess)
└── Fixtures/
    └── presentmon_csv/
        ├── dx11_game.csv                (NEW — recorded CSV fixtures)
        ├── dx12_game.csv                (NEW)
        ├── vulkan_game.csv              (NEW)
        ├── borderless_windowed.csv      (NEW)
        └── multi_app.csv               (NEW)
```
