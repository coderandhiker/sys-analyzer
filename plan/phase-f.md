# Phase F: Tier 2 Sensors (LibreHardwareMonitor)

**Goal**: Add LibreHardwareMonitor integration for temperature, clock speed, fan speed, and power sensors — available only when running elevated (admin). The Tier 1 pipeline must remain completely unaffected; Tier 2 is purely additive.

**Key risk addressed**: LHM loads a kernel driver. It can fail due to Defender/HVCI blocking, driver signing issues, or permission errors. The tool must handle all failure modes without corrupting Tier 1 data. Privilege escalation via `--elevate` must re-launch cleanly.

---

## Entry Gates

| # | Gate | Verification |
|---|------|-------------|
| 1 | Phase E exit gates all satisfied | Full analysis pipeline runs, produces scored recommendations from real data |
| 2 | Tier 1 fully operational | Run without `--elevate`: JSON contains scores, recommendations, frame-time, culprit data. Everything works without LHM. |
| 3 | `SensorSnapshot` has nullable Tier 2 fields | `CpuTempC?`, `CpuClockMhz?`, `CpuPowerW?`, `GpuTempC?`, `GpuClockMhz?`, `GpuPowerW?`, `GpuFanRpm?` all defined (Phase A) |
| 4 | Score renormalization tested | Missing Tier 2 metrics don't inflate/deflate scores (Phase E exit gate 8) |
| 5 | `--elevate` re-launch logic implemented | Exists from Phase B (B.8). May need refinement. |
| 6 | Thermal and power recommendation triggers exist in `config.yaml` | `cpu_thermal_throttle`, `gpu_thermal_throttle`, `cpu_power_limited` defined with Tier 2 conditions |

---

## Deliverables

### F.1 — LibreHardwareMonitor Provider (§4.3 Tier 2 columns)

1. Implement `LibreHardwareProvider : IPolledProvider`
2. `InitAsync()`:
   - Check `IsElevated` (current process is admin)
   - If not elevated: set `Health = Unavailable("Not elevated — Tier 2 sensors require admin. Use --elevate.")`, return immediately. Do NOT attempt to load LHM.
   - If elevated:
     - Create `Computer` object, enable CPU/GPU/Storage/Motherboard sensors
     - Call `computer.Open()` — this loads the kernel driver
     - If `Open()` throws or sensors collection is empty:
       - Set `Health = Failed("{reason}")`, return
       - Common failures: Defender blocks driver, HVCI enabled, driver signing issue
     - If successful: enumerate available sensors, set `Health = Active`
     - Log sensor count: "LibreHardwareMonitor: 47 sensors active"
3. `Poll(long qpcTimestamp)`:
   - Call `computer.Accept(updateVisitor)` to refresh sensor readings
   - Read each relevant sensor:
     - CPU package temperature (°C)
     - Per-core clock speeds (MHz)
     - CPU package power (W)
     - GPU temperature (°C)
     - GPU core clock (MHz)
     - GPU memory clock (MHz)
     - GPU power (W)
     - GPU fan speed (RPM and %)
     - NVMe/drive temperature (°C) if available
   - Populate the Tier 2 fields of `MetricBatch`
   - Must complete in <10ms (LHM sensor reads are typically <2ms)
4. `Dispose()`:
   - Call `computer.Close()` — unloads the kernel driver
   - Ensure no resource leaks

### F.2 — Tier Detection & Probe Display

1. At session start (Probing state), detect current tier:
   - If elevated + LHM loads → Tier 2
   - If elevated + LHM fails → Tier 1 (with note about LHM failure)
   - If not elevated → Tier 1 (with note about `--elevate`)
2. Update the console probe display:
   - Tier 2: `✅ LibreHardwareMonitor: 47 sensors (Tier 2 — admin)`
   - Tier 1: `⚠ LibreHardwareMonitor: Unavailable (not elevated) → Use --elevate for Tier 2`
   - Failed: `❌ LibreHardwareMonitor: Driver failed ({reason}) → Tier 2 sensors unavailable`
3. Set `sensor_health.tier` in JSON output: 1 or 2

### F.3 — Elevation Refinement (§10.2)

1. Review `--elevate` re-launch from Phase B. Ensure:
   - All original CLI arguments are preserved in the re-launched process
   - If UAC is declined, the tool falls back to Tier 1 (no crash, clear message)
   - If already elevated, `--elevate` is a no-op (don't re-launch again)
   - The re-launched process inherits the same working directory
   - Console output is preserved (the re-launched process writes to its own console)
2. Add elevation check at startup: if `--elevate` was passed AND already running elevated, skip re-launch

### F.4 — Thermal & Power Analysis Integration

1. Feed Tier 2 sensor data into the analysis pipeline:
   - **Thermal throttle detection**: If temperature exceeds `thresholds.cpu.temp_warning` AND clock speed drops below base clock → compute `thermal_throttle_pct` (percentage of capture time in throttled state)
   - **Power limit detection**: If CPU/GPU power is at TDP-rated value AND clocks are dropping → compute `power_at_tdp_pct`
   - **Clock drop calculation**: `clock_drop_pct` = percentage of time clocks are > 10% below max observed boost
   - **Thermal soak** (§15.1): temperature trending up after 15+ minutes → cooling inadequate
2. These derived metrics feed into:
   - Bottleneck scoring weights: `thermal_throttle_weight`, `clock_drop_weight`, `power_throttle_weight`
   - Recommendation triggers: `cpu_thermal_throttle`, `gpu_thermal_throttle`, `cpu_power_limited`
   - Frame-time correlation: temperature spikes coinciding with frame-time spikes
3. PSU adequacy estimation (§15.7): Sum CPU + GPU power draw. If approaching common PSU tiers (550W, 650W, 750W), warn about potential power-related instability.

**Unit tests**:
- Temp above warning + clock below base → `thermal_throttle_pct > 0`
- Temp below warning → `thermal_throttle_pct = 0`
- Power at TDP + clock drop → `power_at_tdp_pct > 0`
- PSU estimation: CPU 125W + GPU 300W = 425W → no warning at 750W tier; → warning at 550W tier

### F.5 — Update Live Display with Tier 2 Data

1. When Tier 2 is active, show additional info in live display:
   - `CPU: 78% @ 4.5GHz / 72°C`
   - `GPU: 58% @ 1905MHz / 68°C`
   - `Power: CPU 95W + GPU 220W = 315W`
2. When Tier 1 only: omit temp/clock/power lines (already handled in Phase B)

### F.6 — LHM Failure Modes (§12.1)

Implement all LHM failure paths from the degradation matrix:

| Failure Mode | Detection | Implementation |
|-------------|-----------|----------------|
| Not elevated | `!IsElevated` at startup | Don't load LHM at all. Health = Unavailable. Tier 1 mode. |
| Driver load fails (Defender) | `Computer.Open()` throws | Health = Failed. Continue Tier 1. Log: "LHM driver blocked by {reason}." |
| Driver load fails (HVCI) | `Computer.Open()` throws with HVCI-specific error | Same as above, with specific guidance: "HVCI (Memory Integrity) may block the sensor driver." |
| Sensors empty after Open | `computer.Hardware` collection empty | Health = Failed("No sensors found"). Continue Tier 1. |
| Sensor read timeout | Individual sensor read takes > 10ms | Skip that sensor for this tick. Log warning. Don't block other sensors. |
| Partial sensors | Some hardware types missing (e.g., no GPU sensors) | Read what's available. Note gaps in health matrix. |

**Component tests** (mock LHM):
- Elevated + all sensors → `MetricBatch` fully populated with Tier 2 data
- Not elevated → Health = Unavailable, no LHM calls attempted
- `Open()` throws → Health = Failed, Tier 1 unaffected
- Partial sensors (CPU only, no GPU) → CPU Tier 2 populated, GPU Tier 2 null

### F.7 — Verify Tier 1 Isolation

1. Run the full test suite WITHOUT admin / WITHOUT LHM:
   - All Tier 1 functionality works identically to Phase E
   - No code paths attempt LHM sensor reads
   - No recommendations that depend only on Tier 2 data are triggered
   - Score renormalization handles missing Tier 2 weights correctly
2. This is the critical safety check: Phase F must not regress Phase E.

---

## Exit Gates

| # | Gate | Verification |
|---|------|-------------|
| 1 | `dotnet build` succeeds | 0 errors, 0 warnings |
| 2 | All tests pass (including all prior phases) | `dotnet test` — all green |
| 3 | Tier 1 unchanged | Run `SysAnalyzer.exe --duration 15` without admin. Compare JSON to Phase E output — same fields present, same score calculation, same recommendations. No regression. |
| 4 | `--elevate` triggers UAC | Run `SysAnalyzer.exe --elevate --duration 15`. UAC prompt appears. After accepting, Tier 2 sensors active. |
| 5 | UAC decline handled | Run `--elevate`, decline UAC. Tool falls back to Tier 1 with message. No crash. |
| 6 | Tier 2 data in JSON | With admin, JSON `sensor_health.tier = 2`. `SensorSnapshot` entries contain non-null temperature, clock, power values. |
| 7 | Thermal throttle detection works | On a system under heavy load (or with fake data), verify `cpu_thermal_throttle` recommendation triggers when temp exceeds threshold and clocks drop. |
| 8 | Power limit detection works | Verify `cpu_power_limited` recommendation triggers when power at TDP + clock drop detected (may require synthetic fixture). |
| 9 | LHM driver failure handled | On a system with HVCI enabled (or by mocking), verify LHM fails gracefully → Tier 1 continues → report notes the failure. |
| 10 | Overhead budget: Tier 2 | With LHM active, total CPU still < 1.5%, working set still < 100MB. LHM poll adds < 0.2% CPU. |
| 11 | Live display shows Tier 2 | When elevated, console shows temperature and clock speed alongside utilization. |

---

## Files Created / Modified

```
SysAnalyzer/
├── Capture/
│   └── Providers/
│       └── LibreHardwareProvider.cs     (NEW — IPolledProvider)
├── Analysis/
│   ├── ThermalAnalyzer.cs              (NEW — thermal soak, throttle detection)
│   └── PowerAnalyzer.cs                (NEW — power limit, PSU estimation)
├── Cli/
│   ├── ElevationHelper.cs              (MODIFIED — refined re-launch logic)
│   └── LiveDisplay.cs                  (MODIFIED — Tier 2 metrics display)
└── Report/
    └── JsonReportGenerator.cs          (MODIFIED — Tier 2 fields in output)

SysAnalyzer.Tests/
├── Unit/
│   ├── ThermalAnalyzerTests.cs         (NEW)
│   └── PowerAnalyzerTests.cs           (NEW)
├── Component/
│   ├── LibreHardwareProviderTests.cs   (NEW — mock LHM)
│   └── Tier1IsolationTests.cs          (NEW — verify no Tier 1 regression)
└── Integration/
    └── ElevationFlowTests.cs           (NEW)
```
