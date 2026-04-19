# Test Dossier — Phase A: Core Contracts & Data Model

| Field | Value |
|-------|-------|
| **Generated** | 2026-04-19 |
| **SDK** | .NET 10.0.100 |
| **OS** | Windows 11 |
| **Build Time** | 4.2s (full non-incremental) |
| **Test Duration** | 1.1s |
| **Verdict** | ✅ **ALL 108 TESTS PASS** |

---

## Summary Dashboard

| Test Class | Tests | Pass | Fail | Skip | Status |
|------------|------:|-----:|-----:|-----:|--------|
| TimestampConversionTests | 8 | 8 | 0 | 0 | ✅ |
| TimeWindowTests | 12 | 12 | 0 | 0 | ✅ |
| FingerprintTests | 14 | 14 | 0 | 0 | ✅ |
| JsonSchemaRoundTripTests | 5 | 5 | 0 | 0 | ✅ |
| ConfigLoaderTests | 8 | 8 | 0 | 0 | ✅ |
| ConfigValidatorTests | 13 | 13 | 0 | 0 | ✅ |
| ExpressionParserTests | 15 | 15 | 0 | 0 | ✅ |
| ExpressionEvaluatorTests | 24 | 24 | 0 | 0 | ✅ |
| TemplateResolverTests | 9 | 9 | 0 | 0 | ✅ |
| **TOTAL** | **108** | **108** | **0** | **0** | ✅ |

---

## Deliverable → Test Mapping

### A.1 — Project Scaffold

| Artifact | Verified By |
|----------|-------------|
| `SysAnalyzer.sln` exists, targets `net10.0-windows` | `dotnet build` succeeds with 0 errors, 0 warnings |
| NuGet packages restore (LibreHardwareMonitorLib 0.9.6, Hardware.Info 101.1.1.1, TraceEvent 3.1.20, YamlDotNet 16.3.0) | `dotnet restore` succeeds |
| `app.manifest` with `asInvoker` | File present, build embeds it |
| `Program.cs` exits cleanly | `dotnet build` produces `SysAnalyzer.dll` |
| xUnit + FluentAssertions test project | All 108 tests discovered and executed |

> No dedicated unit tests — validated transitively by build + all other tests running.

---

### A.2 — Canonical Timestamp Model (§3)

**Covered by:** `TimestampConversionTests` (8 tests) + `TimeWindowTests` (12 tests) = **20 tests**

| Test | What It Proves | Duration | Status |
|------|---------------|----------|--------|
| `ToMilliseconds_RoundTrips` | QPC ticks → ms → QPC round-trips within 0.01ms tolerance | <1ms | ✅ |
| `ToSeconds_RoundTrips` | QPC ticks → seconds conversion accurate to 0.001s | <1ms | ✅ |
| `FromEtwQpc_SubtractsEpoch` | ETW raw QPC - epoch = correct relative ticks | <1ms | ✅ |
| `FromPresentMonSeconds_AppliesOffset` | PresentMon `TimeInSeconds` + QPC offset → correct canonical time | <1ms | ✅ |
| `ToWallClock_CorrectOffset` | Relative ticks → wall-clock DateTime within 1ms of expected | <1ms | ✅ |
| `Comparison_Operators_Work` | `<`, `>`, `==`, `!=`, `CompareTo` behave correctly | <1ms | ✅ |
| `Subtraction_Works` | Timestamp arithmetic produces correct deltas | <1ms | ✅ |
| `Zero_Ticks_Is_Zero_Ms` | Zero-value edge case handled | <1ms | ✅ |
| `IsWithin_ExactlyAtCenter_ReturnsTrue` | Event at window center is within | <1ms | ✅ |
| `IsWithin_ExactlyAtLeftEdge_ReturnsTrue` | Boundary condition: left edge inclusive | <1ms | ✅ |
| `IsWithin_ExactlyAtRightEdge_ReturnsTrue` | Boundary condition: right edge inclusive | <1ms | ✅ |
| `IsWithin_JustOutsideLeft_ReturnsFalse` | 1ms past left edge → excluded | <1ms | ✅ |
| `IsWithin_JustOutsideRight_ReturnsFalse` | 1ms past right edge → excluded | <1ms | ✅ |
| `IsWithin_EmptyWindow_OnlyExactMatch` | Zero-width window only matches exact time | <1ms | ✅ |
| `NearestSample_EmptyList_ReturnsInconclusive` | No samples → inconclusive (not crash) | <1ms | ✅ |
| `NearestSample_ExactMatch_ReturnsIt` | Binary search finds exact timestamp | <1ms | ✅ |
| `NearestSample_ClosestWithinWindow_ReturnsNearest` | Off-by-10ms target finds closest sample | <1ms | ✅ |
| `NearestSample_GapNoSampleInWindow_ReturnsInconclusive` | Gap in data → inconclusive, not false match | <1ms | ✅ |
| `NearestSample_TwoNeighbors_PicksCloser` | Two candidates, picks the nearer one | <1ms | ✅ |
| `CorrelationWindows_HaveCorrectValues` | Named constants: ±50ms, ±500ms, ±2s, 60s | <1ms | ✅ |

---

### A.3 — Provider Interfaces (§2.1)

**Covered by:** Compilation (no dedicated tests per spec — "tested transitively in Phase B")

| Interface | Signature Verified |
|-----------|-------------------|
| `IProvider : IDisposable` | `Name`, `RequiredTier`, `InitAsync()`, `Health` |
| `ISnapshotProvider : IProvider` | `CaptureAsync() → SnapshotData` |
| `IPolledProvider : IProvider` | `Poll(long qpcTimestamp) → MetricBatch` |
| `IEventStreamProvider : IProvider` | `StartAsync`, `StopAsync`, `IAsyncEnumerable<TimestampedEvent> Events` |
| `ProviderTier` enum | `Tier1`, `Tier2` |
| `ProviderStatus` enum | `Active`, `Degraded`, `Unavailable`, `Failed` |
| `ProviderHealth` record | All 5 fields present |
| `SensorHealthMatrix` class | `Register`, `Providers`, `OverallTier`, `DegradedProviders`, `FailedProviders` |

> All compile cleanly. Build succeeds with 0 warnings under `TreatWarningsAsErrors`.

---

### A.4 — Immutable Capture Domain Objects

**Covered by:** Compilation + JSON round-trip tests (types used in `AnalysisSummary`)

| Type | Kind | Nullable GPU/Tier2 Fields |
|------|------|---------------------------|
| `SensorSnapshot` | `record` | ✅ GPU and Tier 2 fields nullable |
| `TimestampedEvent` | `abstract record` | N/A |
| `FrameTimeSample` | `record : TimestampedEvent` | N/A |
| `ContextSwitchEvent` | `record : EtwEvent` | N/A |
| `DiskIoEvent` | `record : EtwEvent` | N/A |
| `DpcEvent` | `record : EtwEvent` | N/A |
| `ProcessLifetimeEvent` | `record : EtwEvent` | N/A |
| `SnapshotData` | `record` | N/A |
| `HardwareInventory` | `record` | ✅ GPU fields nullable |
| `SystemConfiguration` | `record` | N/A |
| `MetricBatch` | `struct` | Uses `double.NaN` for absent values |

> `MetricBatch` is a `struct` as required (no heap allocation on hot path). All domain objects are `record` types ensuring immutability.

---

### A.5 — Machine Fingerprint (§9)

**Covered by:** `FingerprintTests` — **14 tests**

| Test | What It Proves | Duration | Status |
|------|---------------|----------|--------|
| `ComputeHash_SameInputs_SameHash` | Deterministic: identical inputs → identical hash | <1ms | ✅ |
| `ComputeHash_Is12HexChars` | Output format: exactly 12 lowercase hex chars | <1ms | ✅ |
| `ComputeHash_CpuChange_DifferentHash` | CPU model change → hash changes | <1ms | ✅ |
| `ComputeHash_GpuChange_DifferentHash` | GPU model change → hash changes | <1ms | ✅ |
| `ComputeHash_RamChange_DifferentHash` | Total RAM change → hash changes | <1ms | ✅ |
| `ComputeHash_RamConfigChange_DifferentHash` | RAM config (stick layout) change → hash changes | <1ms | ✅ |
| `ComputeHash_OsBuildChange_DifferentHash` | OS build change → hash changes | <1ms | ✅ |
| `ComputeHash_DisplayChange_DifferentHash` | Display resolution/refresh change → hash changes | <1ms | ✅ |
| `ComputeHash_StorageChange_DifferentHash` | Storage config change → hash changes | <1ms | ✅ |
| `ComputeHash_DriverChange_DifferentHash` | GPU driver version change → hash changes | <1ms | ✅ |
| `ComputeHash_MotherboardChange_DifferentHash` | Motherboard model change → hash changes | <1ms | ✅ |
| `Diff_IdenticalFingerprints_EmptyList` | No changes → empty diff list | 1ms | ✅ |
| `Diff_SingleComponentChanged_ReportsIt` | One change → exactly one diff entry with details | <1ms | ✅ |
| `Diff_MultipleChanges_ReportsAll` | Three changes → three diff entries | <1ms | ✅ |

> All 9 identity components tested individually. SHA-256 hash uses sorted components ensuring order-independence.

---

### A.6 — JSON Summary Schema

**Covered by:** `JsonSchemaRoundTripTests` — **5 tests**

| Test | What It Proves | Duration | Status |
|------|---------------|----------|--------|
| `FullSummary_RoundTrips` | Fully populated `AnalysisSummary` → JSON → deserialize → equivalent to original | 36ms | ✅ |
| `SparseSummary_NullFieldsHandledCorrectly` | Nullable fields (no PresentMon, no ETW, no baseline) → omitted in JSON, null on deserialize | <1ms | ✅ |
| `Fixture_Deserializes_Correctly` | Hand-written `schema_example.json` fixture → deserializes with correct types and values | 43ms | ✅ |
| `Json_UsesCamelCase` | Property names are camelCase (`captureId`, not `CaptureId`) | <1ms | ✅ |
| `Json_IsIndented` | JSON output is indented (human-readable) | 16ms | ✅ |

> Full record tree: `AnalysisSummary` → `Metadata`, `Fingerprint`, `SensorHealth`, `HardwareInventory`, `SystemConfiguration`, `Scores`, `FrameTime?`, `CulpritAttribution?`, `Recommendations[]`, `BaselineComparison?`, `SelfOverhead`, `TimeSeriesMetadata`.

---

### A.7 — Config Loader & Validator (§6.1–6.4)

**Covered by:** `ConfigLoaderTests` (8 tests) + `ConfigValidatorTests` (13 tests) = **21 tests**

| Test | What It Proves | Duration | Status |
|------|---------------|----------|--------|
| `Load_ValidYaml_Succeeds` | Embedded default config.yaml loads and parses correctly | 4ms | ✅ |
| `Load_DefaultConfig_HasExpectedProfiles` | gaming, compiling, general_interactive profiles present | 1ms | ✅ |
| `Load_DefaultConfig_HasExpectedThresholds` | CPU/memory threshold sections populated | 2ms | ✅ |
| `Load_DefaultConfig_HasRecommendations` | All recommendations have non-empty IDs | 4ms | ✅ |
| `Load_ExplicitPath_FileNotFound_Throws` | Missing file → `FileNotFoundException` | <1ms | ✅ |
| `Load_ValidConfig_ParsesCapture` | poll_interval_ms, min/max duration, presentmon/etw flags parsed | 1ms | ✅ |
| `Load_ValidConfig_ParsesOutput` | Output directory and filename format parsed | 2ms | ✅ |
| `Load_ValidConfig_ParsesBaselines` | auto_save and max_stored parsed | 3ms | ✅ |
| `ValidConfig_LoadsWithoutErrors` | Default config passes full validation (0 errors) | 56ms | ✅ |
| `DuplicateRecommendationId_ReportsError` | Two recommendations with same ID → error | <1ms | ✅ |
| `BadSeverity_ReportsError` | `severity: "extreme"` → error with valid options listed | 2ms | ✅ |
| `BadCategory_ReportsError` | `category: "storage"` → error with valid options listed | <1ms | ✅ |
| `BadConfidence_ReportsError` | `confidence: "maybe"` → error | <1ms | ✅ |
| `BadExpressionSyntax_ReportsPositionalError` | `>>` operator → parse error at position | <1ms | ✅ |
| `EmptyTrigger_ReportsError` | Empty trigger → required error | <1ms | ✅ |
| `UnknownFieldReference_ReportsWarning` | `foo.bar` → warning (not error — may be future field) | <1ms | ✅ |
| `BadPlaceholder_ReportsErrorWithSuggestion` | `{cpx.model}` → error suggesting `{cpu.model}` | 10ms | ✅ |
| `MissingId_ReportsError` | Empty ID → required error | <1ms | ✅ |
| `MultipleErrors_AllReported` | 3+ errors in config → all reported at once (not stop-at-first) | 2ms | ✅ |
| `ValidEvidenceBoost_NoError` | Valid evidence_boost expression passes | <1ms | ✅ |
| `BadEvidenceBoost_ReportsError` | Invalid evidence_boost syntax → error | <1ms | ✅ |

---

### A.8 — Trigger Expression Engine (§6.3)

**Covered by:** `ExpressionParserTests` (15 tests) + `ExpressionEvaluatorTests` (24 tests) + `TemplateResolverTests` (9 tests) = **48 tests**

#### Parser Tests

| Test | What It Proves | Duration | Status |
|------|---------------|----------|--------|
| `Parse_SimpleComparison` | `cpu.load > 90` → `Comparison(FieldRef, GT, Number)` | <1ms | ✅ |
| `Parse_CompoundAndOrPrecedence` | `a > 1 AND b < 2 OR c == 'foo'` → correct AND-before-OR precedence | <1ms | ✅ |
| `Parse_NotExpression` | `NOT gpu.has_data` → `NotExpr(FieldRef)` | <1ms | ✅ |
| `Parse_BareFieldRef` | `frametime.has_data` → bare field ref (truthy check) | <1ms | ✅ |
| `Parse_StringComparison` | String literal in single quotes parsed correctly | <1ms | ✅ |
| `Parse_BoolLiteral` | `true`/`false` keywords recognized | <1ms | ✅ |
| `Parse_MultipleAnd` | 3-way AND → `AndExpression` with 3 operands | <1ms | ✅ |
| `Parse_AllComparisonOperators` | All 6 operators: `>`, `<`, `>=`, `<=`, `==`, `!=` | <1ms | ✅ |
| `Parse_DecimalNumber` | `33.3` parsed as `NumberLiteral(33.3)` | <1ms | ✅ |
| `Parse_NotEqualString` | `!=` with string operand | <1ms | ✅ |
| `ParseError_DoubleGreaterThan` | `>>` → error at correct position | 2ms | ✅ |
| `ParseError_UnexpectedEndOfExpression` | `a > ` → "unexpected end" error | <1ms | ✅ |
| `ParseError_EmptyExpression` | `""` → error | <1ms | ✅ |
| `ParseError_UnterminatedString` | `'unterminated` → error | <1ms | ✅ |
| `GetFieldReferences_FindsAll` | Extracts all field paths from compound expression | 2ms | ✅ |

#### Evaluator Tests

| Test | What It Proves | Duration | Status |
|------|---------------|----------|--------|
| `Evaluate_NumberGreaterThan_True` | `{cpu.load: 95} → "cpu.load > 90"` → true | 13ms | ✅ |
| `Evaluate_NumberGreaterThan_False` | `{cpu.load: 80} → "cpu.load > 90"` → false | <1ms | ✅ |
| `Evaluate_MissingField_ReturnsFalse` | Missing field → null → false | <1ms | ✅ |
| `Evaluate_NullField_ReturnsFalse` | Explicit null → false | <1ms | ✅ |
| `Evaluate_StringEquality_True` | String `==` match | <1ms | ✅ |
| `Evaluate_StringEquality_False` | String `==` mismatch | <1ms | ✅ |
| `Evaluate_TypeMismatch_ThrowsError` | String vs number → `ExpressionEvaluationException` | 5ms | ✅ |
| `Evaluate_ShortCircuitAnd_FirstFalse_NoError` | AND stops at first false (second operand not evaluated) | <1ms | ✅ |
| `Evaluate_ShortCircuitOr_FirstTrue_NoError` | OR stops at first true | <1ms | ✅ |
| `Evaluate_BareFieldTruthy_BoolTrue` | Bare `true` field → truthy | <1ms | ✅ |
| `Evaluate_BareFieldTruthy_BoolFalse` | Bare `false` field → falsy | <1ms | ✅ |
| `Evaluate_BareFieldTruthy_PositiveDouble` | Bare `50.0` → truthy | <1ms | ✅ |
| `Evaluate_BareFieldTruthy_ZeroDouble` | Bare `0.0` → falsy | <1ms | ✅ |
| `Evaluate_BareFieldTruthy_NonEmptyString` | Bare non-empty string → truthy | <1ms | ✅ |
| `Evaluate_BareFieldTruthy_EmptyString` | Bare empty string → falsy | <1ms | ✅ |
| `Evaluate_BareFieldTruthy_Missing` | Missing field → falsy | <1ms | ✅ |
| `Evaluate_NotExpression` | `NOT false` → true | <1ms | ✅ |
| `Evaluate_CompoundAndOr` | Complex compound expression evaluates correctly | <1ms | ✅ |
| `Evaluate_IntFieldAsNumber` | `int` field auto-promoted to `double` for comparison | <1ms | ✅ |
| `Evaluate_BoolEquality` | `== true` with bool field | <1ms | ✅ |
| `Evaluate_LessThanOrEqual` | `<=` with equal value → true | <1ms | ✅ |
| `Evaluate_GreaterThanOrEqual` | `>=` with equal value → true | <1ms | ✅ |
| `Evaluate_StringNotEqual` | `!=` with different string → true | <1ms | ✅ |
| `Evaluate_StringGreaterThan_ThrowsError` | `>` on strings → error (only `==`/`!=` allowed) | 6ms | ✅ |

#### Template Resolver Tests

| Test | What It Proves | Duration | Status |
|------|---------------|----------|--------|
| `Resolve_BasicSubstitution` | `{cpu.model}` and `{cpu.load}` replaced with values | 2ms | ✅ |
| `Resolve_MissingField_ReturnsUnknown` | Missing → `[unknown]` (never crashes, never raw placeholder) | <1ms | ✅ |
| `Resolve_NullField_ReturnsUnknown` | Null → `[unknown]` | <1ms | ✅ |
| `Resolve_BoolTrue_ReturnsYes` | `true` → `"Yes"` | <1ms | ✅ |
| `Resolve_BoolFalse_ReturnsNo` | `false` → `"No"` | <1ms | ✅ |
| `Resolve_Double_OneDecimalPlace` | `82.678` → `"82.7"` | <1ms | ✅ |
| `Resolve_Integer_NoDecimalPlace` | `32` → `"32"` (not `"32.0"`) | <1ms | ✅ |
| `Resolve_NoPlaceholders_ReturnsOriginal` | Plain text passes through unchanged | <1ms | ✅ |
| `GetPlaceholders_FindsAll` | Extracts all `{field.path}` names from template | <1ms | ✅ |

---

## Exit Gate Checklist

| # | Gate | Evidence | Status |
|---|------|----------|--------|
| 1 | Solution builds cleanly | `dotnet build --no-incremental` → 0 errors, 0 warnings (4.2s) | ✅ |
| 2 | All unit tests pass | `dotnet test` → 108 passed, 0 failed, 0 skipped (1.1s) | ✅ |
| 3 | Timestamp model correct | 20 tests cover QPC conversion, ETW normalization, PresentMon offset, window boundaries, nearest-sample gaps | ✅ |
| 4 | Provider interfaces compile | `IProvider`, `ISnapshotProvider`, `IPolledProvider`, `IEventStreamProvider` defined with correct signatures | ✅ |
| 5 | Domain types are immutable | All domain types are `record`; `MetricBatch` is `struct`; `QpcTimestamp` is `readonly struct` | ✅ |
| 6 | JSON schema round-trips | 5 tests: full + sparse round-trip, fixture deser, camelCase, indented | ✅ |
| 7 | Config loads and validates | 21 tests: default loads, all error classes tested, multi-error reporting | ✅ |
| 8 | Expression engine correct | 48 tests: parser grammar, evaluator semantics, template resolution | ✅ |
| 9 | Fingerprint deterministic | 14 tests: same→same hash, each component change→different hash, diff works | ✅ |
| 10 | Zero runtime hardware deps | All tests run without admin, GPU, or PresentMon | ✅ |

---

## Screenshots

> 📸 No UI artifacts in this phase — screenshots not applicable.
> Phase A produces only types, interfaces, and logic. Visual output begins in Phase E (HTML report).

---

## Build Artifacts

### Source Files (27 files, 1,916 lines)

| File | Lines | Purpose |
|------|------:|---------|
| `Program.cs` | 2 | CLI entry point stub |
| `app.manifest` | 17 | UAC `asInvoker` manifest |
| `config.yaml` | 227 | Default embedded configuration |
| `Capture/QpcTimestamp.cs` | 81 | Canonical QPC timestamp model |
| `Capture/TimeWindow.cs` | 128 | Correlation windows + nearest-sample search |
| `Capture/SensorSnapshot.cs` | 50 | Point-in-time polled metric record |
| `Capture/TimestampedEvent.cs` | 56 | Event type hierarchy (ETW + PresentMon) |
| `Capture/SnapshotData.cs` | 90 | Hardware inventory + system config records |
| `Capture/MetricBatch.cs` | 71 | Zero-alloc polled metric struct |
| `Capture/SensorHealthMatrix.cs` | 36 | Provider health aggregation |
| `Capture/Providers/IProvider.cs` | 12 | Base provider interface |
| `Capture/Providers/ISnapshotProvider.cs` | 12 | One-shot capture interface |
| `Capture/Providers/IPolledProvider.cs` | 13 | Synchronous poll interface |
| `Capture/Providers/IEventStreamProvider.cs` | 15 | Async event stream interface |
| `Capture/Providers/ProviderHealth.cs` | 26 | Health record + enums |
| `Analysis/Models/MachineFingerprint.cs` | 78 | Machine identity with SHA-256 hash |
| `Analysis/Models/AnalysisSummary.cs` | 159 | Full JSON schema record tree |
| `Report/JsonReportGenerator.cs` | 24 | System.Text.Json serialization |
| `Config/AnalyzerConfig.cs` | 92 | YAML config model hierarchy |
| `Config/ConfigLoader.cs` | 60 | YAML loader with fallback chain |
| `Config/ConfigValidator.cs` | 186 | Multi-error validation + Levenshtein suggestions |
| `Config/ExpressionEngine/Tokenizer.cs` | 151 | Expression tokenizer |
| `Config/ExpressionEngine/ExpressionAst.cs` | 16 | AST node types |
| `Config/ExpressionEngine/ExpressionParser.cs` | 180 | PEG recursive descent parser |
| `Config/ExpressionEngine/ExpressionEvaluator.cs` | 140 | Short-circuit type-strict evaluator |
| `Config/ExpressionEngine/TemplateResolver.cs` | 47 | `{placeholder}` template resolution |
| `Config/ExpressionEngine/ExpressionError.cs` | 25 | Positional error types |

### Test Files (10 files, 1,422 lines)

| File | Lines | Tests | Purpose |
|------|------:|------:|---------|
| `Unit/TimestampConversionTests.cs` | 94 | 8 | QPC conversion, ETW, PresentMon alignment |
| `Unit/TimeWindowTests.cs` | 138 | 12 | Window boundaries, nearest-sample search |
| `Unit/FingerprintTests.cs` | 134 | 14 | Hash determinism, component sensitivity, diff |
| `Unit/JsonSchemaRoundTripTests.cs` | 185 | 5 | JSON serialization contract |
| `Unit/ConfigLoaderTests.cs` | 76 | 8 | YAML loading, fallback chain |
| `Unit/ConfigValidatorTests.cs` | 201 | 13 | Validation error classes, multi-error |
| `Unit/ExpressionParserTests.cs` | 140 | 15 | PEG grammar coverage |
| `Unit/ExpressionEvaluatorTests.cs` | 193 | 24 | Evaluation semantics |
| `Unit/TemplateResolverTests.cs` | 82 | 9 | Template substitution |
| `Fixtures/schema_example.json` | 176 | — | Hand-written reference fixture |

---

## Reproduction Command

```powershell
cd C:\dev\sys-analyzer\SysAnalyzer
dotnet build --no-incremental    # Expect: 0 errors, 0 warnings
dotnet test --verbosity normal   # Expect: 108 passed, 0 failed
```
