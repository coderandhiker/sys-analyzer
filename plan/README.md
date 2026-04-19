# SysAnalyzer — Implementation Plan

This folder contains the phased implementation plan for SysAnalyzer. Each phase is a self-contained document with entry gates (what must be true before starting), deliverables, and exit gates (what must be true before moving on).

The master design document is [../plan.md](../plan.md). These phase files extract the implementation-relevant details and add the gating criteria. When a phase file references a section like "§3.2", it refers to that section number in plan.md.

## Phase Sequence

```
Phase A ──→ Phase B ──→ Phase C ──→ Phase D ──→ Phase E ──→ Phase F ──→ Phase G
Contracts    Vertical    PresentMon   ETW &       Analysis    Tier 2      HTML
& Data       Slice                    Culprit     Engine &    Sensors     Report
Model                                Attribution  Rules
```

| Phase | File | Summary | Key Risk Addressed |
|-------|------|---------|-------------------|
| **A** | [phase-a.md](phase-a.md) | Core contracts, data model, config/expression engine | Type safety, schema stability, rule engine correctness |
| **B** | [phase-b.md](phase-b.md) | Thin end-to-end vertical slice (PerfCounters → JSON) | Capture lifecycle, output shape, idempotency |
| **C** | [phase-c.md](phase-c.md) | PresentMon frame-time integration | Frame-time capture, timestamp alignment, degradation |
| **D** | [phase-d.md](phase-d.md) | ETW session & culprit attribution | Attribution correctness, event buffering, correlation |
| **E** | [phase-e.md](phase-e.md) | Full analysis engine & rule system | Scoring determinism, rule evaluation, baseline comparison |
| **F** | [phase-f.md](phase-f.md) | Tier 2 sensors (LibreHardwareMonitor) | Privilege handling, graceful degradation |
| **G** | [phase-g.md](phase-g.md) | HTML report & visual output | Report rendering, chart fidelity, asset embedding |

## Dependency Graph

```
A is required by: B, C, D, E, F, G  (everything depends on contracts)
B is required by: C, D, E            (need capture lifecycle before adding providers)
C is required by: D, E               (frame-time data needed for correlation/attribution)
D is required by: E                  (ETW data needed for full analysis)
E is required by: F, G               (analysis pipeline needed before adding sensors/UI)
F is required by: G                  (Tier 2 metrics must be in the data model before rendering)
G is terminal                        (HTML is a view over stable JSON)
```

## How to Use These Files

1. **Before starting a phase**: Review its Entry Gates. Every gate must be satisfied.
2. **During implementation**: Use the Deliverables as a checklist. Work through them in order.
3. **Before moving on**: Review its Exit Gates. Every gate must be demonstrably true. Run the specified verification commands.
4. **Cross-reference**: Each phase references sections in plan.md (e.g., "§3.2") for the full design detail. The phase file tells you *what to build and how to verify*; plan.md tells you *why and the exact specification*.
