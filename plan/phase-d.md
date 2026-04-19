# Phase D: ETW & Culprit Attribution

**Goal**: Add a real-time ETW trace session to capture process-level events and build the culprit attribution system. This turns generic "your CPU is busy" into "`MsMpEng.exe` caused 68% of context switches during your frame-time spikes."

**Key risk addressed**: ETW is powerful but brittle — session creation can fail, events can be lost under load, and attribution requires precise time-alignment with PresentMon frame spikes. Get the plumbing right now; Phase E will consume this data for full analysis.

---

## Entry Gates

| # | Gate | Verification |
|---|------|-------------|
| 1 | Phase C exit gates all satisfied | All C exit gates checked and documented |
| 2 | PresentMon frame-time capture working | JSON includes frame-time percentiles; stutter spike timestamps are available for correlation |
| 3 | `QpcTimestamp` alignment verified | PresentMon timestamps align correctly with `SensorSnapshot` timestamps (within 1 sample/second) |
| 4 | `EtwEvent` record hierarchy defined | `ContextSwitchEvent`, `DiskIoEvent`, `DpcEvent`, `ProcessLifetimeEvent` exist (Phase A deliverable A.4) |
| 5 | `IEventStreamProvider` interface finalized | Contract from Phase A includes `StartAsync`, `StopAsync`, `IAsyncEnumerable<TimestampedEvent>` |
| 6 | TraceEvent NuGet package added | `Microsoft.Diagnostics.Tracing.TraceEvent` in project references |

---

## Deliverables

### D.1 — ETW Provider (§4.2)

1. Implement `EtwProvider : IEventStreamProvider`
2. `InitAsync()`:
   - Create a real-time `TraceEventSession` with a unique session name (include PID to avoid collision: `SysAnalyzer-{pid}`)
   - Enable ETW providers:
     - `Microsoft-Windows-Kernel-Process` — context switches, process start/stop
     - `Microsoft-Windows-Kernel-Processor-Power` — thread scheduling
     - `Microsoft-Windows-Kernel-DPC` — DPC/ISR attribution
     - `Microsoft-Windows-Kernel-Disk` — disk I/O per process
   - If session creation fails (policy restriction, permission, another consumer conflict):
     - Log the specific error reason
     - Retry once with alternate session name (append random suffix)
     - If retry fails: set `Health = Unavailable`, return. All culprit attribution disabled.
3. `StartAsync()`:
   - Start processing events on a background thread via `TraceEventSource.Process()`
   - For each received event, normalize the timestamp:
     ```
     canonicalQpc = rawEtwQpc - captureEpoch
     ```
     (ETW timestamps are raw QPC ticks — direct subtraction per §3.2)
   - Convert to the appropriate `EtwEvent` subtype and yield via the async enumerable
   - Use a bounded ring buffer: if the consumer can't keep up, increment `EventsLost` and drop oldest events
4. `StopAsync()`:
   - Stop the trace session
   - Drain remaining events from the buffer
   - Record `EventsLost` count
5. Buffer overflow handling: if `TraceEventSession.EventsLost > 0`, mark attribution confidence as degraded

### D.2 — Event Parsing & Filtering

1. Parse context switch events:
   - Extract `OldProcessId`, `NewProcessId`, `NewProcessName` (the process gaining the CPU)
   - Filter: only keep context switches involving the tracked game process (from PresentMon auto-detection or `--process`)
   - Store: process that preempted the game thread, timestamp, core number
2. Parse DPC events:
   - Extract `DriverModule` (e.g., `ndis.sys`, `storport.sys`), `DurationUs`
   - Filter: keep all DPC events (they affect system-wide latency)
3. Parse disk I/O events:
   - Extract `ProcessId`, `ProcessName`, `BytesTransferred`
   - Filter: keep events above a minimum size threshold (ignore tiny metadata I/O)
4. Parse process lifetime events:
   - Extract `ProcessId`, `ProcessName`, `IsStart` (true for start, false for exit)
   - Keep all: used to detect "process launched during capture" interference

### D.3 — Culprit Attributor (§5.1 Phase 5)

1. Implement `CulpritAttributor`:
   - Input: frame-time stutter spike timestamps (from Phase C), ETW events, `SensorSnapshot` time-series
   - For each stutter spike (frame time > threshold):
     a. Find all context switch events within `FRAME_SPIKE_NARROW` (±50ms) of the spike
     b. Aggregate: count context switches per preempting process
     c. Find all DPC events within `FRAME_SPIKE_WIDE` (±500ms)
     d. Aggregate: total DPC time per driver module
     e. Find all disk I/O events within `METRIC_CORRELATION` (±2s)
     f. Aggregate: total bytes per process
   - Across all spikes, compute:
     - **Top context-switch processes**: sorted by total count, with percentage of total switches
     - **Top DPC drivers**: sorted by total DPC time, with percentage
     - **Top disk I/O processes**: sorted by total bytes, with percentage
     - **Correlation score**: for each process, what fraction of stutter spikes had this process preempting? (0.0–1.0)
2. Output: `CulpritAttribution` record:
   ```csharp
   record CulpritAttribution(
       List<ProcessCulprit> TopContextSwitchProcesses,
       List<DriverCulprit> TopDpcDrivers,
       List<ProcessCulprit> TopDiskIoProcesses,
       bool HasAttribution,           // true if ETW data was available
       bool HasDpcAttribution,        // true if DPC events were captured
       float InterferenceCorrelation  // overall correlation strength
   );
   
   record ProcessCulprit(
       string ProcessName,
       int ProcessId,
       int ContextSwitchCount,
       float PercentOfTotal,
       float CorrelationWithStutter,  // fraction of spikes this process was active in
       string Description,            // auto-generated: "Windows Defender real-time scan"
       string Remediation             // auto-generated: "Exclude game folder from scans"
   );
   
   record DriverCulprit(
       string DriverModule,
       double TotalDpcTimeMs,
       float PercentOfDpcTime,
       string Description
   );
   ```
3. Known process descriptions: maintain a lookup table of common culprits:
   - `MsMpEng.exe` → "Windows Defender real-time scanner"
   - `SearchIndexer.exe` → "Windows Search file indexer"
   - `OneDrive.exe` → "OneDrive cloud sync"
   - `dwm.exe` → "Desktop Window Manager"
   - `audiodg.exe` → "Windows Audio Device Graph Isolation"
   - etc.

### D.4 — Process Lifetime Tracking

1. Track processes that start during capture:
   - Log with timestamp: "`WindowsUpdate.exe` launched at 00:47:12"
   - If the process launch coincides with a stutter cluster (multiple spikes within 10 seconds), note the correlation
2. Track processes that exit during capture (potential game crash detection)
3. Include in the JSON output as part of `CulpritAttribution`

### D.5 — ETW Failure Modes (§12.1)

Implement all ETW failure paths from the degradation matrix:

| Failure Mode | Detection | Implementation |
|-------------|-----------|----------------|
| Session creation fails | Constructor throws | Health = Unavailable. No culprit data. Recommendation confidence downgraded. |
| Buffer overflow | `EventsLost > 0` | Continue. Mark `attributionConfidence = degraded`. Note in report: "ETW lost {n} events." |
| Session name collision | Session creation fails with specific error | Retry with PID + random suffix. If second attempt fails, treat as session creation failure. |
| Provider not available | Specific provider enable fails | Continue with remaining providers. Note which attribution types are unavailable. |

**Component tests** (fake `IEventStreamProvider`):
- Normal event flow → correct `CulpritAttribution` output
- No ETW → attribution fields empty, `HasAttribution = false`
- Buffer overflow → degraded confidence noted
- Mixed: context switches available but DPC events missing → partial attribution

### D.6 — Populate Analysis Result Fields

1. After attribution completes, populate the `CulpritAttribution` section of `AnalysisSummary`
2. Populate the `culprit.*` namespace in the flat `AnalysisResult` dictionary for recommendation trigger evaluation:
   - `culprit.has_attribution` → bool
   - `culprit.has_dpc_attribution` → bool
   - `culprit.top_process_name` → string
   - `culprit.top_process_ctx_switch_pct` → double
   - `culprit.interference_correlation` → double
   - `culprit.top_dpc_driver` → string
   - `culprit.top_dpc_driver_pct` → double
   - `culprit.disk_io_top_process` → string
   - `culprit.process_summary` → string (human-readable summary)
   - `culprit.dpc_summary` → string
   - `culprit.disk_io_summary` → string
   - `culprit.top_process_description` → string
   - `culprit.top_process_remediation` → string

### D.7 — Update JSON Output

1. Update `JsonReportGenerator` to include `culprits` array in JSON
2. Include process names, types, impact percentages, correlation scores
3. When ETW was unavailable: `culprits = null`, note in `sensor_health`

---

## Exit Gates

| # | Gate | Verification |
|---|------|-------------|
| 1 | `dotnet build` succeeds | 0 errors, 0 warnings |
| 2 | All unit + component tests pass | `dotnet test` — all green |
| 3 | ETW session creates and captures events | Run `SysAnalyzer.exe --duration 15`. JSON has `sensor_health.etw.available = true`. |
| 4 | Context switch attribution works | Launch a CPU-heavy background process (e.g., `stress-ng` or a tight loop). Run SysAnalyzer with a game. JSON `culprits` array includes the background process name with a non-zero correlation score. |
| 5 | DPC attribution works | If a DPC storm is detectable on the test machine, verify driver module appears in `culprits`. Otherwise, verify component test with fake DPC events. |
| 6 | Disk I/O attribution works | Launch a process doing heavy disk I/O (e.g., `robocopy`). Run SysAnalyzer. JSON culprits include the disk-heavy process. |
| 7 | Process lifetime tracking works | Launch a new process mid-capture. JSON shows the process start event with correct timestamp. |
| 8 | Timestamp alignment correct | ETW event timestamps align with PresentMon frame spikes. A frame spike at T=120.5s should have correlated ETW context switches within ±50ms (manually verified by inspecting JSON). |
| 9 | Graceful degradation: no ETW | Run with `--no-etw`. JSON has `sensor_health.etw.available = false`, `culprits = null`. No crash. Recommendation confidence downgraded for attribution-dependent recommendations. |
| 10 | Graceful degradation: session conflict | If another ETW consumer is running (e.g., WPR), SysAnalyzer retries once and falls back gracefully. |
| 11 | Events-lost tracking | Component test: simulate buffer overflow → `EventsLost > 0` → confidence degraded in output. |
| 12 | Overhead budget maintained | ETW session adds < 0.3% CPU. Total SysAnalyzer (including PresentMon + ETW) still < 1.5% CPU, < 100MB RAM. |

---

## Files Created / Modified

```
SysAnalyzer/
├── Capture/
│   └── Providers/
│       └── EtwProvider.cs               (NEW — IEventStreamProvider)
├── Analysis/
│   ├── CulpritAttributor.cs             (NEW)
│   └── Models/
│       └── CulpritAttribution.cs        (NEW — record types)
├── Data/
│   └── KnownProcesses.cs               (NEW — lookup table of known culprits)
└── Report/
    └── JsonReportGenerator.cs           (MODIFIED — add culprits section)

SysAnalyzer.Tests/
├── Unit/
│   ├── CulpritAttributorTests.cs        (NEW)
│   └── CorrelationWindowTests.cs        (NEW — ETW ↔ frame spike alignment)
├── Component/
│   └── EtwProviderTests.cs              (NEW — fake event source)
└── Fixtures/
    ├── defender_interference.json       (NEW — canonical ETW attribution fixture)
    └── dpc_storm.json                   (NEW — DPC storm fixture)
```
