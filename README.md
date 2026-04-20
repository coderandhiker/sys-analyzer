# SysAnalyzer

A Windows system performance analyzer that captures hardware metrics, frame
times, and kernel events, then scores bottlenecks and generates actionable
reports.

## Features

- **Hardware monitoring** — CPU, GPU, memory, disk, and network metrics at
  1-second granularity (temperatures, clocks, power draw via LibreHardwareMonitor)
- **Frame-time capture** — Per-frame timing, CPU/GPU busy, dropped frames, and
  present mode via [PresentMon](https://github.com/GameTechDev/PresentMon)
- **Kernel event tracing** — Context switches, DPC time, and disk I/O via ETW
- **Bottleneck scoring** — Weighted subsystem scores (0–100) with gaming,
  compiling, and general-interactive profiles
- **Thermal & power analysis** — Throttling detection, thermal soak, power limits
- **Culprit attribution** — Correlates frame stutters with specific processes and
  drivers using ETW data
- **Baseline comparison** — Compare runs against saved baselines to track
  regressions
- **Self-contained HTML reports** — Interactive charts (ApexCharts + Tabler UI)
  with no external dependencies

## Requirements

- Windows 10 or later (x64)
- [.NET 10](https://dotnet.microsoft.com/download) runtime
- Administrator privileges recommended (required for temperatures, ETW, and power
  data)

## Quick Start

```
SysAnalyzer.exe
```

With no arguments, SysAnalyzer captures for **5 minutes**, then generates HTML
and JSON reports in `./reports/`. It will request admin elevation via UAC by
default.

Press **Q** or **Esc** to stop early.

## Usage

```
SysAnalyzer [options]

Options:
  --profile <name>     Scoring profile: gaming, compiling, general_interactive
                       (default: gaming)
  --label <text>       Label for this capture run
  --process <name>     Target process for frame-time tracking
  --config <path>      Path to custom config.yaml
  --output <dir>       Output directory (default: ./reports)
  --interval <ms>      Poll interval in milliseconds (default: 1000)
  --duration <sec>     Auto-stop after N seconds (default: 300)
  --compare <path>     Compare against a previous baseline JSON file

  --elevate            Force re-launch with admin privileges
  --no-elevate         Skip admin elevation (Tier 1 sensors only)
  --no-presentmon      Disable PresentMon frame-time capture
  --no-etw             Disable ETW kernel event tracing
  --no-live            Disable live console display

  --csv                Export 1-second time-series CSV
  --etl                Export raw ETL trace (for Windows Performance Analyzer)

  --version            Print version and exit
  --help, -h           Show help
```

## Examples

Capture a 2-minute gaming session for a specific game:

```
SysAnalyzer.exe --duration 120 --process MyGame.exe --profile gaming
```

Quick compile benchmark with CSV export:

```
SysAnalyzer.exe --duration 60 --profile compiling --csv
```

Compare against a previous run:

```
SysAnalyzer.exe --compare ./reports/sysanalyzer-2026-04-19_14-30-00.json
```

Run without admin (limited sensors):

```
SysAnalyzer.exe --no-elevate --no-etw
```

## Output

Every capture produces:

| Format | File | Description |
|--------|------|-------------|
| HTML | `*-report.html` | Interactive self-contained report with charts |
| JSON | `*.json` | Machine-readable scores, metrics, and recommendations |
| CSV | `*-metrics.csv`, `*-presentmon.csv` | Time-series data (with `--csv`) |
| ETL | `*.etl` | Raw kernel trace (with `--etl`) |

## Sensor Tiers

| Tier | Requires Admin | Providers |
|------|----------------|-----------|
| Tier 1 | No | Performance counters, hardware inventory, Windows checks, PresentMon |
| Tier 2 | Yes | LibreHardwareMonitor (temps, clocks, power), ETW (kernel events) |

If admin elevation is denied, SysAnalyzer continues with Tier 1 data only.

## Configuration

Settings are loaded from `config.yaml` (adjacent to the executable). Override
individual options via CLI flags. Key settings:

```yaml
capture:
  poll_interval_ms: 1000
  presentmon_enabled: true
  etw_enabled: true

output:
  directory: "./reports"

baselines:
  directory: "~/.sysanalyzer/baselines"
  auto_save: true
  max_stored: 50
```

## PresentMon

SysAnalyzer uses Intel's [PresentMon](https://github.com/GameTechDev/PresentMon)
(MIT licensed) for frame-time capture. If `PresentMon.exe` is not found next to
the SysAnalyzer executable, it is automatically downloaded from GitHub Releases
on first run. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for license
details.

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Configuration error |
| 2 | No providers available |
| 3 | Capture error |

## License

[MIT](LICENSE)
