# Tooling And Scratch Space

This file defines which verification/debug tools are considered durable and which locations are intentionally temporary.

## Durable Tools We Actively Use

- `run.ps1`
  - launches the packaged app for real UI verification
- `profile-live-indexing.ps1`
  - launches through `run.ps1`, attaches `dotnet-trace`, and writes trace output under `tmp/`
- `tools/ProbeAsset/`
  - command-line probe tool for package/resource inspection, coverage surveys, batch probes, and focused diagnostics
- `tests/Sims4ResourceExplorer.Tests`
  - regression and integration verification layer

## Expected Verification Order

1. targeted tests
2. full test suite when the change is broad
3. `tools/ProbeAsset` or focused package/resource queries
4. live SQLite cache inspection
5. UI launch through `run.ps1` only when a visual check is actually needed

## App Build Rule

- Do not build the app for routine non-visual verification if tests/probes are enough.
- Only produce a verification app build when the user wants to inspect the UI or a visual/runnable check is genuinely necessary.
- Only then should `BuildNumber` be incremented in `src/Sims4ResourceExplorer.App/Sims4ResourceExplorer.App.csproj`.

## Assembly Research Protocol

When implementing a game-assembly path such as `Sim`, `CAS`, material routing, rigging, or morph/deformer behavior:

1. read the repo-local documentation and references first
2. if that is insufficient, search the web and prefer the most authoritative sources available
3. if no clean authoritative solution is found, do not silently add a fallback/workaround path
4. ask the user explicitly before introducing such a fallback/workaround because it changes project cleanliness and future architecture

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
