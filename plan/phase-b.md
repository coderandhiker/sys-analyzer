# Phase B: Thin End-to-End Vertical Slice

**Goal**: Wire the first real providers into the capture lifecycle and produce a valid JSON output file. This proves the entire pipeline works — from sensor init through capture loop to file emission — before adding the complex providers (PresentMon, ETW).

**Key risk addressed**: If the capture lifecycle, poll loop, or JSON emitter have design flaws, finding them now (with simple providers) is far cheaper than finding them after PresentMon/ETW are integrated.

---

## Entry Gates

| # | Gate | Verification |
|---|------|-------------|
| 1 | Phase A exit gates all satisfied | All A exit gates checked and documented |
| 2 | All domain types compile and pass tests | `dotnet test` — all Phase A tests green |
| 3 | Provider interfaces defined | `IProvider`, `ISnapshotProvider`, `IPolledProvider`, `IEventStreamProvider` exist with correct signatures |
| 4 | JSON schema (`AnalysisSummary`) defined | Records compile, fixture round-trip test passes |
| 5 | Config loader operational | Default `config.yaml` loads without errors |

---

## Deliverables

### B.1 — Capture Session State Machine (§13)

1. Implement `CaptureSession` class with the full lifecycle:
   ```
   Created → Probing → [PartialProbe] → Capturing → Stopping → Analyzing → Emitting → Complete
   ```
2. State transitions:
   - `Init()`: Created → Probing. Calls `InitAsync()` on each registered provider. Populates `SensorHealthMatrix`.
   - `Start()`: Probing/PartialProbe → Capturing. Starts the poll loop.
   - `Stop()`: Capturing → Stopping. Halts poll loop, calls `StopAsync()` on event stream providers.
   - Internal: Stopping → Analyzing → Emitting → Complete (automatic after Stop).
3. Cancellation:
   - `Ctrl+C` or `Q`/`Esc` during Capturing = equivalent to `Stop()`
   - `Ctrl+C` during Analyzing/Emitting = wait for current phase to finish (don't leave half-written outputs)
4. Provider failure during Probing: mark provider as `Failed` in `SensorHealthMatrix`, continue with remaining providers. If all providers fail, emit a minimal report noting total failure.
5. Provider failure during Capturing: record failure timestamp and reason, continue with remaining providers.

**Unit tests** (all providers faked):
- Happy path: Created → Probing → Capturing → Stopping → Analyzing → Emitting → Complete
- Provider fails during Probing → PartialProbe → Capturing continues in degraded mode
- Ctrl+C during Capturing → Stopping → normal completion
- All providers fail → minimal report emitted
- Invalid state transitions throw (e.g., Start() from Created)

### B.2 — Performance Counter Provider (§4.3)

1. Implement `PerformanceCounterProvider : IPolledProvider`
2. `InitAsync()`: Create all `PerformanceCounter` instances from the Tier 1 tables in §4.3:
   - CPU: `Processor(_Total)\% Processor Time`, per-core, `System\Context Switches/sec`, `Processor\% DPC Time`, `Processor\Interrupts/sec`
   - Memory: `Memory\% Committed Bytes In Use`, `Memory\Available MBytes`, `Memory\Page Faults/sec`, `Memory\Pages/sec`, `Memory\Committed Bytes`, `Memory\Pool Nonpaged Bytes`, `Memory\Cache Bytes`
   - GPU: `GPU Engine\Utilization Percentage`, `GPU Process Memory\Dedicated Usage` (Win10 1709+)
   - Disk: `PhysicalDisk\% Disk Time`, `PhysicalDisk\Avg. Disk Queue Length`, `PhysicalDisk\Disk Bytes/sec`, `PhysicalDisk\Avg. Disk sec/Read`, `PhysicalDisk\Avg. Disk sec/Write`
   - Network: `Network Interface\Bytes Total/sec`, `TCPv4\Segments Retransmitted/sec`
   - System: `System\Processes`, `System\Threads`, `Process(_Total)\Handle Count`, `System\System Up Time`
3. `Poll(long qpcTimestamp)`: Read all counters, populate `MetricBatch`, return. Must complete in <10ms.
4. Handle missing counters gracefully: if a counter doesn't exist on the machine, mark it as unavailable in health and return `MetricBatch` with that field empty/null. Do not throw.
5. Pre-allocate the `MetricBatch` struct — no heap allocations on the hot path.

**Component tests** (mock the actual PerformanceCounter system call):
- All counters available → `MetricBatch` fully populated
- Specific counter missing → rest still populated, health notes the gap
- Counter throws during read → handled, metric is null for that tick
- `Poll()` timing: verify no allocations (use `GC.GetAllocatedBytesForCurrentThread()` before/after)

### B.3 — Hardware Inventory Provider (§4.4)

1. Implement `HardwareInventoryProvider : ISnapshotProvider` using `Hardware.Info`
2. `InitAsync()`: Call `RefreshCPUList()`, `RefreshMemoryList()`, `RefreshVideoControllerList()`, etc.
3. `CaptureAsync()`: Return a `SnapshotData` containing `HardwareInventory` record with all fields from §4.4
4. Handle WMI timeout: Hardware.Info can take up to 21 seconds on cold first call. Set a 30-second timeout. If partial data comes back, use what we got and note the gap.

**Component tests** (mock Hardware.Info calls):
- Full inventory succeeds → all fields populated
- WMI timeout → partial data + health note
- No discrete GPU (iGPU-only) → GPU fields from iGPU
- Multiple drives → all listed

### B.4 — Windows Deep Checks Provider (§4.5)

1. Implement `WindowsDeepCheckProvider : ISnapshotProvider`
2. Reads registry keys and WMI for: pagefile config, visual effects, Game Mode, HAGS, Game DVR, SysMain status, WSearch status, shader cache size, temp folder size, AV product, standby list
3. Returns `SystemConfiguration` record as part of `SnapshotData`
4. Each check is independent — failure of one does not block others

**Component tests** (mock registry/WMI reads):
- All checks succeed → full `SystemConfiguration`
- Registry key missing → graceful default + note
- WMI query fails → that check returns null

### B.5 — Poll Loop & Data Storage

1. Implement the capture loop in `CaptureSession`:
   - Timer fires every `config.poll_interval_ms` (default 1000ms)
   - Calls `Poll()` on each `IPolledProvider`
   - Assembles a `SensorSnapshot` from the `MetricBatch` results
   - Appends to an in-memory `List<SensorSnapshot>` (bounded by `max_capture_duration_sec`)
2. Track `QpcTimestamp` for each sample using `Stopwatch.GetTimestamp()`
3. Track sample count and capture duration
4. Implement self-overhead measurement (§11.2):
   - At session start: record `GC.GetTotalAllocatedBytes()`, `GC.CollectionCount()` for each generation, `GC.GetTotalPauseDuration()`
   - During capture: periodically sample process CPU usage for SysAnalyzer.exe itself
   - At session end: compute deltas for `SelfOverhead` record

**Unit tests**:
- Poll loop produces expected number of samples for a given duration
- Self-overhead record is populated with reasonable values
- Timestamps are monotonically increasing

### B.6 — JSON Output Emitter

1. Implement `JsonReportGenerator`:
   - Takes the complete `AnalysisSummary` (from Phase A types)
   - Serializes using `System.Text.Json` with configured options (camelCase, indented)
   - Writes to `{output_dir}/{filename_format}.json`
2. For Phase B, the analysis fields will be stubs (scores = 0, recommendations = empty, frame timing = null, culprits = null). The JSON still validates against the schema.
3. Implement the filename generation logic:
   - Timestamp format from config
   - Label sanitization (strip invalid filename chars, replace spaces with hyphens)
   - Output directory creation if needed

**Unit tests**:
- Generated JSON deserializes back to `AnalysisSummary`
- Filename sanitization handles special characters
- Output directory is created when missing

### B.7 — Baseline Auto-Save (§9)

1. Implement `BaselineManager`:
   - On each run, compute `MachineFingerprint` from hardware inventory
   - Copy JSON summary to `~/.sysanalyzer/baselines/{fingerprint-hash}/{timestamp}.json`
   - Prune oldest baselines when count exceeds `config.baselines.max_stored`
2. Implement `--compare <path>` CLI flag (Phase B only loads the prior JSON for comparison; actual delta scoring is Phase E)
3. Fingerprint mismatch warning when `--compare` targets a different machine

**Unit tests** (filesystem mocked):
- Auto-save creates the correct directory structure
- Pruning removes oldest files when limit exceeded
- Fingerprint hash is used as directory name

### B.8 — CLI Entrypoint & Argument Parsing (§8.3)

1. Implement `Program.cs` with `System.CommandLine` or manual arg parsing:
   - `--profile`, `--label`, `--process`, `--config`, `--output`, `--interval`
   - `--elevate`, `--no-presentmon`, `--no-etw`, `--no-live`
   - `--compare`, `--csv`, `--etl`
   - `--duration`, `--version`, `--help`
2. Wire up: parse args → load config → create providers → create `CaptureSession` → run → emit outputs
3. Implement `--duration` for unattended capture (auto-stop after N seconds)
4. Implement `--version` and `--help`
5. Implement `--elevate` re-launch via `Process.Start` with `runas` verb (§10.2)
6. Exit codes: 0 = success, 1 = config error, 2 = no providers available, 3 = capture error

**Integration tests** (all providers faked):
- `--duration 5` → captures for ~5 seconds → emits JSON → exits 0
- `--version` → prints version → exits 0
- Bad config → exits 1 with clear message
- `--output ./test-reports` → creates directory and writes there

### B.9 — Live Console Display (§8.1)

1. Implement basic live display (updated once per second):
   - Show sensor health matrix (probe results)
   - Show elapsed time, sample count
   - Show CPU/RAM/GPU/Disk utilization bars
   - Use `Console.SetCursorPosition()` for in-place updates
2. Implement `--no-live` flag to suppress display (for piped/scripted usage)
3. Implement `Q` / `Esc` keypress detection to stop capture

**No automated tests** — visual output is verified manually.

---

## Exit Gates

| # | Gate | Verification |
|---|------|-------------|
| 1 | `dotnet build` succeeds | 0 errors, 0 warnings |
| 2 | All unit + component tests pass | `dotnet test` — all green |
| 3 | Capture session state machine works end-to-end | Integration test: fake providers → all states traversed → JSON emitted |
| 4 | `--duration 10` produces valid JSON | Run `SysAnalyzer.exe --duration 10 --no-presentmon --no-etw`. Output JSON exists. Deserializes to `AnalysisSummary`. Contains real CPU/Memory/Disk/Network metrics from perf counters. |
| 5 | Hardware inventory populated | JSON contains CPU model, RAM sticks, GPU model, disk model, OS version |
| 6 | System config audit populated | JSON contains power plan, Game Mode, HAGS, Game DVR, SysMain, WSearch status |
| 7 | Sensor health matrix accurate | JSON `sensor_health` shows correct tier, lists available/unavailable providers |
| 8 | Self-overhead recorded | JSON `self_overhead` has non-zero CPU%, working set, GC counts |
| 9 | Baseline auto-save works | After a run, `~/.sysanalyzer/baselines/` contains the JSON copy |
| 10 | Graceful degradation | Run without admin (Tier 1): LHM fields are null, no crash. Report notes Tier 1 mode. |
| 11 | `--elevate` works | Triggers UAC prompt; if accepted, Tier 2 fields become available (manual test) |
| 12 | Poll loop stays under overhead budget | During a 60-second capture, SysAnalyzer CPU < 1.5%, working set < 100MB |

---

## Files Created / Modified

```
SysAnalyzer/
├── Program.cs                           (full CLI entrypoint)
├── Capture/
│   ├── CaptureSession.cs                (lifecycle state machine)
│   ├── PollLoop.cs                      (timer-driven poll orchestrator)
│   ├── SelfOverheadTracker.cs           (§11.2 measurement)
│   └── Providers/
│       ├── PerformanceCounterProvider.cs
│       ├── HardwareInventoryProvider.cs
│       └── WindowsDeepCheckProvider.cs
├── Report/
│   ├── JsonReportGenerator.cs           (concrete implementation)
│   └── FilenameGenerator.cs
├── Baselines/
│   └── BaselineManager.cs
└── Cli/
    ├── CliParser.cs
    └── LiveDisplay.cs

SysAnalyzer.Tests/
├── Unit/
│   ├── CaptureSessionStateTests.cs
│   ├── PollLoopTests.cs
│   ├── SelfOverheadTests.cs
│   └── FilenameGenerationTests.cs
├── Component/
│   ├── PerfCounterProviderTests.cs
│   ├── HardwareInventoryProviderTests.cs
│   ├── WindowsDeepCheckProviderTests.cs
│   └── BaselineManagerTests.cs
└── Integration/
    ├── FullCaptureFlowTests.cs
    └── CliArgumentTests.cs
```
