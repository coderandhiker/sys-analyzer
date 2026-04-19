# Phase A: Core Contracts & Data Model

**Goal**: Build the foundation that everything else depends on. No capture, no UI, no hardware access. Pure types, interfaces, config loading, and expression evaluation.

**Key risk addressed**: If domain types, timestamp model, or expression grammar are wrong, every subsequent phase inherits the bug. Fix it here where changes are cheap.

---

## Entry Gates

| # | Gate | Verification |
|---|------|-------------|
| 1 | .NET 10 SDK installed | `dotnet --version` shows 10.x |
| 2 | Workspace exists at `c:\dev\sys-analyzer\` | Directory present with `plan.md` and `tabler/` |
| 3 | NuGet source reachable | `dotnet restore` succeeds against nuget.org |
| 4 | plan.md reviewed and understood | Developer has read Sections 1-3, 6.1-6.4, 9, 12, 13, 19 |

---

## Deliverables

### A.1 — Project Scaffold

1. Create `SysAnalyzer/SysAnalyzer.sln` and `SysAnalyzer/SysAnalyzer.csproj` targeting `net10.0-windows`
2. Add NuGet packages with pinned versions (§1.2):
   - `LibreHardwareMonitorLib` 0.9.6
   - `Hardware.Info` 101.1.1.1
   - `Microsoft.Diagnostics.Tracing.TraceEvent` (latest stable)
   - `YamlDotNet` (latest stable)
3. Create `SysAnalyzer.Tests/SysAnalyzer.Tests.csproj` (xUnit + FluentAssertions or equivalent)
4. Add `app.manifest` with `requestedExecutionLevel level="asInvoker"` (§10.2)
5. Create empty `Program.cs` with top-level statements that exits cleanly
6. Verify: `dotnet build` succeeds, `dotnet test` runs (0 tests)

### A.2 — Canonical Timestamp Model (§3)

1. Create `QpcTimestamp` readonly struct:
   - Stores `long RawTicks` (QPC ticks since capture start)
   - Static `CaptureEpoch` holder (set once at session init)
   - Conversion methods: `ToMilliseconds()`, `ToSeconds()`, `FromEtwQpc(long rawQpc)`, `FromPresentMonSeconds(double timeInSeconds, long qpcOffset)`
   - `ToWallClock(DateTime anchor)` for display
2. Create named window constants (§3.3):
   - `FRAME_SPIKE_NARROW` = ±50ms
   - `FRAME_SPIKE_WIDE` = ±500ms
   - `METRIC_CORRELATION` = ±2s
   - `TREND_WINDOW` = 60s
3. Create `TimeWindow` utility: `IsWithin(QpcTimestamp eventTime, QpcTimestamp center, TimeSpan halfWidth) → bool`
4. Create `NearestSample<T>` utility: given a sorted list of timestamped items and a target timestamp, find the nearest item within a window (or return `null` / `inconclusive`)

**Unit tests**:
- QPC → ms conversion round-trips correctly
- ETW timestamp normalization (raw QPC - epoch = relative)
- PresentMon offset calculation (our QPC at launch - PM first time)
- `IsWithin` boundary conditions (exactly at edge, just outside, empty window)
- `NearestSample` with gaps (no sample in window → inconclusive)

### A.3 — Provider Interfaces (§2.1)

1. Create `IProvider` base interface:
   ```csharp
   interface IProvider : IDisposable
   {
       string Name { get; }
       ProviderTier RequiredTier { get; }
       Task<ProviderHealth> InitAsync();
       ProviderHealth Health { get; }
   }
   ```
2. Create `ISnapshotProvider : IProvider` with `Task<SnapshotData> CaptureAsync()`
3. Create `IPolledProvider : IProvider` with `MetricBatch Poll(long qpcTimestamp)`
4. Create `IEventStreamProvider : IProvider` with `StartAsync`, `StopAsync`, `IAsyncEnumerable<TimestampedEvent> Events`
5. Create `ProviderTier` enum: `Tier1, Tier2`
6. Create `ProviderStatus` enum: `Active, Degraded, Unavailable, Failed`
7. Create `ProviderHealth` record (§2.1)
8. Create `SensorHealthMatrix` class: holds `Dictionary<string, ProviderHealth>`, exposes `OverallTier`, `DegradedProviders`, `FailedProviders`

**No unit tests needed** — these are pure interface/type definitions. Tested transitively in Phase B.

### A.4 — Immutable Capture Domain Objects

1. Create `SensorSnapshot` record — one point-in-time for all polled metrics:
   - `QpcTimestamp Timestamp`
   - CPU metrics: `double TotalCpuPercent`, `double[] PerCoreCpuPercent`, `double ContextSwitchesPerSec`, `double DpcTimePercent`, `double InterruptsPerSec`
   - Memory metrics: `double MemoryUtilizationPercent`, `double AvailableMemoryMb`, `double PageFaultsPerSec`, `double HardFaultsPerSec`, `double CommittedBytes`, `double CommittedBytesInUsePercent`
   - GPU metrics (nullable — may be absent): `double? GpuUtilizationPercent`, `double? GpuMemoryUtilizationPercent`, `double? GpuMemoryUsedMb`
   - Disk metrics: `double DiskActiveTimePercent`, `double DiskQueueLength`, `double DiskBytesPerSec`, `double DiskReadLatencyMs`, `double DiskWriteLatencyMs`
   - Network metrics: `double NetworkBytesPerSec`, `double NetworkUtilizationPercent`, `double TcpRetransmitsPerSec`
   - Tier 2 (nullable): `double? CpuTempC`, `double? CpuClockMhz`, `double? CpuPowerW`, `double? GpuTempC`, `double? GpuClockMhz`, `double? GpuPowerW`, `double? GpuFanRpm`
2. Create `TimestampedEvent` base record: `QpcTimestamp Timestamp`
3. Create `FrameTimeSample : TimestampedEvent` — one PresentMon row:
   - `string ApplicationName`, `double FrameTimeMs`, `double CpuBusyMs`, `double GpuBusyMs`, `bool Dropped`, `string PresentMode`, `bool AllowsTearing`
4. Create `EtwEvent : TimestampedEvent` with subclasses:
   - `ContextSwitchEvent`: `int OldProcessId`, `int NewProcessId`, `string NewProcessName`
   - `DiskIoEvent`: `int ProcessId`, `string ProcessName`, `long BytesTransferred`
   - `DpcEvent`: `string DriverModule`, `double DurationUs`
   - `ProcessLifetimeEvent`: `int ProcessId`, `string ProcessName`, `bool IsStart`
5. Create `SnapshotData` — result from `ISnapshotProvider.CaptureAsync()`:
   - `HardwareInventory` record: CPU model/cores/threads/clocks/cache, RAM sticks (list of stick records), GPU model/VRAM/driver, disks, motherboard, BIOS, OS, network adapters, display resolution/refresh
   - `SystemConfiguration` record: power plan, game mode, HAGS, Game DVR, SysMain status, WSearch status, shader cache size, temp folder size, pagefile config, startup programs, AV product
6. Create `MetricBatch` struct returned by `IPolledProvider.Poll()` — lightweight, pre-allocated, no heap allocation on the hot path. Contains the same fields as the relevant subset of `SensorSnapshot` for that provider.

### A.5 — Machine Fingerprint (§9)

1. Create `MachineFingerprint` record with all 9 identity components:
   - CPU model, GPU model, total RAM GB, RAM config (stick count × capacity × speed), OS build, display resolution + refresh, storage config hash, GPU driver major version, motherboard model
2. Implement `ComputeHash() → string` (SHA-256, first 12 hex chars)
3. Implement `Diff(MachineFingerprint other) → List<string>` — lists which components changed

**Unit tests**:
- Same inputs → same hash (deterministic)
- Any single component change → different hash
- `Diff` lists changed components correctly
- Component ordering doesn't affect hash (sorted before hashing)

### A.6 — JSON Summary Schema

1. Define `AnalysisSummary` as C# records — this is the contract that JSON, HTML, and baseline comparison all build against:
   ```
   AnalysisSummary
   ├── Metadata (version, timestamp, duration, label, profile, tier, captureId)
   ├── MachineFingerprint
   ├── SensorHealth (SensorHealthMatrix serialized)
   ├── HardwareInventory
   ├── SystemConfiguration
   ├── Scores (cpu, memory, gpu, disk, network — each with score, classification, available metrics)
   ├── FrameTimeSummary? (avgFps, p50/p95/p99/p999 frameTime, droppedPct, cpuBoundPct, gpuBoundPct, presentMode, stutterCount)
   ├── CulpritAttribution? (topProcesses[], topDpcDrivers[], topDiskProcesses[])
   ├── Recommendations[] (id, title, body, severity, category, confidence, priority, evidence[])
   ├── BaselineComparison? (baselineId, fingerprint match, deltas[])
   ├── SelfOverhead (avgCpuPercent, peakWorkingSet, gcCollections, gcPauseTime, etwEventsLost)
   └── TimeSeriesMetadata (sampleCount, durationSec, downsampleFactor)
   ```
2. Implement `System.Text.Json` serialization with `JsonSerializerOptions` (camelCase, indented, null handling)
3. Write a sample JSON file by hand matching this schema — commit as `SysAnalyzer.Tests/Fixtures/schema_example.json`

**Unit tests**:
- Serialize a fully-populated `AnalysisSummary` → deserialize → round-trip equals
- Serialize with nullable fields (no PresentMon, no ETW, no Tier 2) → valid JSON, null fields omitted or explicit
- Deserialize the hand-written fixture → succeeds with correct types

### A.7 — Config Loader & Validator (§6.1-6.4)

1. Create `AnalyzerConfig` class hierarchy matching the `config.yaml` schema:
   - `CaptureConfig`, `OutputConfig`, `BaselinesConfig`, `ProfilesConfig` (with per-profile scoring weights), `ThresholdsConfig`, `FrameTimeThresholdsConfig`
   - `RecommendationConfig` — one per recommendation entry with `Id`, `Trigger`, `Severity`, `Category`, `Confidence`, `Priority`, `Title`, `Body`, `EvidenceBoost`
2. Implement `ConfigLoader`:
   - Load from adjacent file → CWD → embedded default (§10.3)
   - Parse with YamlDotNet
   - Apply defaults for missing optional fields
3. Implement `ConfigValidator` (§6.3 validation rules):
   - Parse every `trigger` and `evidence_boost` expression — fail with positional error messages
   - Validate enum fields (severity, category, confidence)
   - Check unique recommendation IDs
   - Warn on unknown field references in expressions
   - Validate `{placeholder}` variables in body templates
   - Report all errors at once (don't stop at first)
4. Create a default `config.yaml` matching the full schema from §6.1

**Unit tests**:
- Valid config loads without errors
- Missing required section → clear error message
- Bad expression syntax → positional parse error
- Duplicate recommendation ID → error
- Unknown field reference in trigger → warning
- Bad `{placeholder}` in body → error with suggestion
- Enum validation (bad severity, bad category)
- Default config (no file) loads embedded defaults

### A.8 — Trigger Expression Engine (§6.3)

1. Implement `ExpressionParser`:
   - Tokenizer: splits into `FieldRef`, `NumberLiteral`, `StringLiteral`, `BoolLiteral`, `Operator` (`AND`, `OR`, `NOT`, `>`, `<`, `>=`, `<=`, `==`, `!=`)
   - Parser: builds AST from PEG grammar (§6.3): `Expression → OrExpr → AndExpr → NotExpr → Comparison → Value`
   - Error reporting: token position, expected vs actual, snippet of surrounding expression
2. Implement `ExpressionEvaluator`:
   - Evaluates AST against `Dictionary<string, object>` (the `AnalysisResult` flat dict)
   - Short-circuit: `AND` stops at first `false`, `OR` stops at first `true`
   - Type-strict: string vs number comparison is an error, not silent false
   - Null handling: missing field → `null` → any comparison returns `false`
   - Bare field reference: truthy if `bool true`, `double > 0`, `string` non-empty
3. Implement `TemplateResolver`:
   - `Resolve(string template, Dictionary<string, object> fields) → string`
   - Doubles formatted to 1 decimal place
   - Bools → "Yes"/"No"
   - Missing fields → `[unknown]`

**Unit tests** (comprehensive — this is a mini language):
- Parse simple: `"cpu.load > 90"` → Comparison(FieldRef, GT, NumberLiteral)
- Parse compound: `"a > 1 AND b < 2 OR c == 'foo'"` → correct precedence (AND before OR)
- Parse NOT: `"NOT gpu.has_data"` → NotExpr
- Parse error: `"a >> 5"` → error at position with message
- Parse error: `"a > "` → unexpected end of expression
- Evaluate: `{cpu.load: 95.0}` against `"cpu.load > 90"` → true
- Evaluate: `{cpu.load: 80.0}` against `"cpu.load > 90"` → false
- Evaluate: missing field → false
- Evaluate: string equality `"system.power_plan == 'Balanced'"` with matching value → true
- Evaluate: type mismatch (string field compared with >) → error
- Evaluate: short-circuit AND (second operand has missing field, but first is false → no error)
- Evaluate: bare field truthy/falsy
- Template: `"Your {cpu.model} at {cpu.load}%"` → "Your AMD Ryzen 7 5800X at 95.1%"
- Template: missing field → `"Your [unknown] at [unknown]%"`

---

## Exit Gates

| # | Gate | Verification |
|---|------|-------------|
| 1 | Solution builds cleanly | `dotnet build` — 0 errors, 0 warnings (treat warnings as errors) |
| 2 | All unit tests pass | `dotnet test` — all green |
| 3 | Timestamp model is correct | Unit tests cover QPC conversion, ETW normalization, PresentMon offset, window boundaries, nearest-sample with gaps |
| 4 | Provider interfaces compile | `IProvider`, `ISnapshotProvider`, `IPolledProvider`, `IEventStreamProvider` all defined with correct signatures |
| 5 | Domain types are immutable | `SensorSnapshot`, `FrameTimeSample`, `EtwEvent` subtypes, `SnapshotData`, `MachineFingerprint` are all `record` types |
| 6 | JSON schema round-trips | Serialize → deserialize → equal for fully-populated and sparse (nullable) `AnalysisSummary` |
| 7 | Config loads and validates | Default `config.yaml` loads without errors. All validation error classes tested. |
| 8 | Expression engine is correct | Parser handles all grammar productions. Evaluator handles all comparison types, short-circuit, null, type-strict. Template resolver handles missing fields. |
| 9 | Machine fingerprint is deterministic | Same inputs → same hash. Any change → different hash. Diff works. |
| 10 | Zero runtime hardware dependencies | All tests run on any machine (including CI) without admin, without GPU, without PresentMon |

---

## Files Created

```
SysAnalyzer/
├── SysAnalyzer.sln
├── SysAnalyzer.csproj
├── app.manifest
├── config.yaml
├── Program.cs                           (stub)
├── Capture/
│   ├── SensorSnapshot.cs
│   ├── TimestampedEvent.cs
│   ├── QpcTimestamp.cs
│   ├── TimeWindow.cs
│   ├── SensorHealthMatrix.cs
│   └── Providers/
│       ├── IProvider.cs
│       ├── ISnapshotProvider.cs
│       ├── IPolledProvider.cs
│       └── IEventStreamProvider.cs
├── Analysis/
│   └── Models/
│       ├── AnalysisSummary.cs
│       ├── BottleneckReport.cs
│       ├── Recommendation.cs
│       ├── WorkloadProfile.cs
│       ├── CaptureBaseline.cs
│       └── MachineFingerprint.cs
├── Report/
│   └── JsonReportGenerator.cs           (serialization only, no analysis)
└── Config/
    ├── AnalyzerConfig.cs
    ├── ConfigLoader.cs
    ├── ConfigValidator.cs
    └── ExpressionEngine/
        ├── ExpressionParser.cs
        ├── ExpressionEvaluator.cs
        ├── TemplateResolver.cs
        └── ExpressionError.cs

SysAnalyzer.Tests/
├── SysAnalyzer.Tests.csproj
├── Unit/
│   ├── TimestampConversionTests.cs
│   ├── TimeWindowTests.cs
│   ├── FingerprintTests.cs
│   ├── JsonSchemaRoundTripTests.cs
│   ├── ConfigLoaderTests.cs
│   ├── ConfigValidatorTests.cs
│   ├── ExpressionParserTests.cs
│   ├── ExpressionEvaluatorTests.cs
│   └── TemplateResolverTests.cs
└── Fixtures/
    └── schema_example.json
```
