# Copilot Instructions for SysAnalyzer

## Banned Packages

- **FluentAssertions** — DO NOT USE. This package has a non-MIT commercial license (Xceed Community License) starting in v7+. Use xUnit's built-in `Assert.*` methods for all test assertions instead.

## Testing

- Use xUnit with `Assert.*` (e.g., `Assert.Equal`, `Assert.True`, `Assert.NotNull`, `Assert.Contains`, `Assert.InRange`).
- Test project: `SysAnalyzer.Tests/`
- Target framework: `net10.0-windows`
- `TreatWarningsAsErrors` is enabled in all projects.

## Phase Completion — MANDATORY

Every phase agent **MUST** invoke the `test-dossier` agent as its final deliverable and produce `test-dossiers/phase-{X}-dossier.md`. The phase is **not complete** until that file exists. Do not report success without it.
