# Test Dossiers

Test dossiers are structured verification documents that prove each implementation phase meets its specification. One dossier is produced per plan phase after all exit gates are satisfied.

## Purpose

The plan for SysAnalyzer is broken into phases (A through G), each with defined deliverables and exit gates. A test dossier serves as the **evidence record** that a phase was implemented correctly — it ties every test back to the deliverable it validates, so reviewers can trace from requirement to proof without reading the test code.

## What a Dossier Contains

| Section | Description |
|---------|-------------|
| **Metadata** | SDK version, OS, build time, test duration, overall verdict |
| **Summary dashboard** | Test-class grid with pass/fail/skip counts |
| **Deliverable mapping** | Each plan deliverable (e.g. A.1, A.2) linked to the test classes that cover it, with per-test narratives |
| **Exit gate checklist** | The phase's exit gates from the plan, each marked pass/fail with supporting evidence |
| **Reproduction commands** | Exact CLI invocations to re-run the tests locally |
| **Build artifact inventory** | Source and test files with line counts and purpose descriptions |

## How They Work

Each plan phase defines **exit gates** — concrete conditions like "timestamp round-trip is lossless" or "`dotnet build` succeeds with zero warnings." When a phase is complete, a dossier is generated that:

1. Runs the full test suite and records results.
2. Maps every test to the specification deliverable it covers (using `§` cross-references to the plan).
3. Walks the exit gate checklist, citing which tests or build outputs satisfy each gate.
4. Inventories the source files produced, with line counts and roles.

This makes it possible to verify a phase by reading one document rather than navigating across plan files, source code, and test output.

## Naming Convention

```
phase-{letter}-dossier.md
```

Each file corresponds to the plan file of the same letter in `/plan/` (e.g. `phase-a-dossier.md` validates `plan/phase-a.md`).

## Current Status

| Phase | Dossier | Tests |
|-------|---------|-------|
| A — Core Contracts & Data Model | `phase-a-dossier.md` | 108 |
| B — Capture Engine | — | — |
| C — PresentMon Integration | — | — |
| D — ETW Provider | — | — |
| E — Analysis Engine | — | — |
| F — LibreHardwareMonitor Tier 2 | — | — |
| G — HTML Report Generation | — | — |
