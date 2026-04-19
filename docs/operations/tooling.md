# Tooling And Scratch Space

Repo-level working rules live in `AGENT.md`. This file only defines durable tools, verification order, and temporary locations.

## Durable Tools We Actively Use

- `run.ps1`
  - launches the packaged app for real UI verification
  - `.\run.ps1` = rebuilds and launches fresh binaries from the standard app output folder
  - `.\run.ps1 -NoBuild` = launches the already-built binaries without rebuilding
  - use `-NoBuild` only when intentionally rerunning the same build; otherwise prefer plain `.\run.ps1`
- `profile-live-indexing.ps1`
  - launches through `run.ps1`, attaches `dotnet-trace`, and writes trace output under `tmp/`
- `tools/ProbeAsset/`
  - command-line probe tool for package/resource inspection, coverage surveys, batch probes, focused diagnostics, and live `Sim Archetype` body-shell audits
- `tests/Sims4ResourceExplorer.Tests`
  - regression and integration verification layer

## Expected Verification Order

1. targeted tests
2. full test suite when the change is broad
3. `tools/ProbeAsset` or focused package/resource queries
4. live SQLite cache inspection
5. UI launch through `run.ps1` only when a visual check is actually needed

In multi-agent mode, this verification ladder belongs to the verifier role, not implicitly to the worker.

## Verification Build Rule

- Do not build the app for routine non-visual verification if tests/probes are enough.
- Only produce a verification app build when the user wants to inspect the UI or a visual/runnable check is genuinely necessary.
- Only then should `BuildNumber` be incremented in `src/Sims4ResourceExplorer.App/Sims4ResourceExplorer.App.csproj`.
- The reported build id must match the window title and diagnostics `Build:` line.
- For every user-facing manual test packet, provide one exact launch command and say whether it rebuilds or reuses the existing binaries.
- Prefer the standard repo-root launch contract:
  1. `.\run.ps1` when the packet depends on freshly landed code
  2. `.\run.ps1 -NoBuild` only when rerunning the same already-built app
- If a nonstandard executable path is required, explain that deviation explicitly in the handoff.

## Scratch Locations

- `tmp/`
  - active scratch workspace for probe outputs, traces, local scripts, and ad hoc research artifacts
  - do not treat files here as durable project knowledge
- `tools/ProbeAsset/bin/` and `tools/ProbeAsset/obj/`
  - disposable build outputs

## Junk Holding Area

- `junk/local-artifacts/`
  - quarantine for loose files that used to clutter root or no longer deserve a first-class location

## Promotion Rule

If something from `tmp/` keeps being reused, do one of these:

- move it into `tools/` if it is executable tooling
- distill it into `docs/` if it is knowledge
- add or update a test if it represents a regression case
