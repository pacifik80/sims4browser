# TS4 DX11 Introspection

This folder is intentionally isolated from the main browser application under `src/`.

Purpose:
- hold research, prototypes, and tooling for live DirectX 11 introspection of The Sims 4;
- avoid coupling early reverse-engineering work to the browser/runtime codebase;
- provide a clean handoff point for a separate implementation thread.

This workspace is not wired into `Sims4ResourceExplorer.sln` yet.

Current layout:
- `recon/`: lightweight static PE recon CLI for `Game\Bin`
- `native/`: `d3d11.dll` proxy MVP that hooks device creation, `Present`, shader creation, and bounded capture hooks
- `companion/`: named-pipe listener plus log summarizer
- `docs/`: canonical implementation-oriented spec plus raw supporting artifacts
- `schemas/`: event/log schema definitions
- `scripts/`: one-command helpers for recon, companion start, native build, and proxy install
- `captures/`: ignored local artifacts from live runs

Canonical document:
- [TS4 Material Shader Spec](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/docs/ts4-material-shader-spec.md)

Current capture-operations contract:
- [TS4 DX11 Context-Tagged Capture Contract](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/docs/context-tagged-capture-contract.md)
- [TS4 DX11 Context-Tagged Capture Recipes](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/docs/context-tagged-capture-recipes.md)
- [TS4 DX11 Context-Tagged Capture Analysis Workflow](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/docs/context-tagged-capture-analysis-workflow.md)

Supporting/legacy research artifacts:
- `docs/raw/`

## MVP Scope

Implemented in this satellite:
- static recon over TS4 `exe`/`dll` binaries with PE import inspection and `DXBC` scanning;
- native `d3d11.dll` proxy that forwards to system `d3d11.dll`;
- hook points for `Present`, shader creation, shader/state tracking, and continuous draw-centric logging;
- safer command-list telemetry for deferred rendering paths via `FinishCommandList` and `ExecuteCommandList`;
- stable SHA-256 shader hashes plus `D3DReflect` summaries;
- a companion process that receives JSON Lines over a named pipe and writes structured capture files.

## Build

Managed tools:

```powershell
dotnet build .\satellites\ts4-dx11-introspection\TS4.DX11.Introspection.sln -c Debug
```

Native proxy:

```powershell
powershell -ExecutionPolicy Bypass -File .\satellites\ts4-dx11-introspection\scripts\build-native.ps1
```

## Run

Preferred one-script workflow:

Double-click:

`C:\Users\stani\PROJECTS\Sims4Browser\satellites\ts4-dx11-introspection\Run Live Capture.cmd`

Or, if you prefer PowerShell directly:

```powershell
powershell -ExecutionPolicy Bypass -File .\satellites\ts4-dx11-introspection\scripts\run-live-capture.ps1
```

What it does:
- rebuilds the managed solution;
- rebuilds the native `d3d11.dll` proxy;
- creates a timestamped capture session under `captures/live/`;
- starts the companion with session-local logs;
- installs the proxy into `Game\Bin`;
- launches `TS4_x64.exe`;
- waits for the game to exit;
- writes a summary when session logs exist;
- removes the proxy automatically on shutdown.

For tagged helper-family sessions, the fastest path is now a helper preset plus scene-specific fields:

```powershell
powershell -ExecutionPolicy Bypass -File .\satellites\ts4-dx11-introspection\scripts\run-live-capture.ps1 `
  -HelperPreset GeneratedLight `
  -SceneLabel "generated-light indoor room" `
  -TargetAssetsOrEffects "spot lights","visible room lighting" `
  -Notes "camera held on indoor lit scene for the full bounded capture"
```

The lower-level explicit field path still works too:

```powershell
powershell -ExecutionPolicy Bypass -File .\satellites\ts4-dx11-introspection\scripts\run-live-capture.ps1 `
  -WorldMode LiveMode `
  -SceneLabel "generated-light indoor room" `
  -FamilyFocus generated-light `
  -SceneClass lighting-heavy,indoor-lit `
  -ExpectedCandidateClusters F03-maptex `
  -TargetAssetsOrEffects "spot lights","visible room lighting" `
  -Notes "camera held on indoor lit scene for the full bounded capture"
```

Offline analysis helpers:

```powershell
dotnet run --project .\satellites\ts4-dx11-introspection\companion\Ts4Dx11Companion.csproj -- summarize --input .\satellites\ts4-dx11-introspection\captures\live\<session>
dotnet run --project .\satellites\ts4-dx11-introspection\companion\Ts4Dx11Companion.csproj -- compare --left .\satellites\ts4-dx11-introspection\captures\live\<session-a> --right .\satellites\ts4-dx11-introspection\captures\live\<session-b>
dotnet run --project .\satellites\ts4-dx11-introspection\companion\Ts4Dx11Companion.csproj -- summarize --input .\satellites\ts4-dx11-introspection\captures\live\<session> --output .\satellites\ts4-dx11-introspection\captures\live\reports\summary-<session>.md
dotnet run --project .\satellites\ts4-dx11-introspection\companion\Ts4Dx11Companion.csproj -- compare --left .\satellites\ts4-dx11-introspection\captures\live\<session-a> --right .\satellites\ts4-dx11-introspection\captures\live\<session-b> --output .\satellites\ts4-dx11-introspection\captures\live\reports\compare-<a>-vs-<b>.md
dotnet run --project .\satellites\ts4-dx11-introspection\companion\Ts4Dx11Companion.csproj -- compare-groups --left .\satellites\ts4-dx11-introspection\captures\live\<session-a1> --left .\satellites\ts4-dx11-introspection\captures\live\<session-a2> --right .\satellites\ts4-dx11-introspection\captures\live\<session-b1> --right .\satellites\ts4-dx11-introspection\captures\live\<session-b2> --output .\satellites\ts4-dx11-introspection\captures\live\reports\group-compare.md
dotnet run --project .\satellites\ts4-dx11-introspection\companion\Ts4Dx11Companion.csproj -- catalog --input .\satellites\ts4-dx11-introspection\captures\live\<session-a> --input .\satellites\ts4-dx11-introspection\captures\live\<session-b> --output-md .\satellites\ts4-dx11-introspection\docs\raw\shader-catalog.md --output-json .\satellites\ts4-dx11-introspection\docs\raw\shader-catalog.json
```

`compare` is the safest current way to isolate scenario-specific shader families without reintroducing unstable deferred-context hooks.

For the most common workflow, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\satellites\ts4-dx11-introspection\scripts\compare-last-two.ps1
```

This compares the two newest timestamped sessions under `captures/live/` and writes a Markdown report under `captures/live/reports/`.
Crash runs are skipped automatically; the helper only compares sessions that ended with `launcher_exit_code: 0`.

For helper-family research captures, either:
- use the preset recipes in:
  - `docs/context-tagged-capture-recipes.md`
- pass context-tag parameters to `run-live-capture.ps1`; or
- add a `context-tags.json` sidecar manually using:
  - `docs/context-tagged-capture-contract.md`
  - `schemas/context-tag.schema.json`

For explicit multi-run A/B analysis, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\satellites\ts4-dx11-introspection\scripts\compare-capture-groups.ps1 -Left 20260421-212139,20260421-212533 -Right 20260421-210119,20260421-211853
```

This writes a Markdown report that highlights common hashes, left-only/right-only hashes, per-group support counts, stage breakdowns, and top unique reflection signatures across both groups.

For helper-family tagged sessions, the simplest post-capture compare path is:

```powershell
powershell -ExecutionPolicy Bypass -File .\satellites\ts4-dx11-introspection\scripts\compare-tagged-helper-captures.ps1 `
  -Target 20260422-210000 `
  -Control 20260422-211500 `
  -FamilyFocus generated-light
```

This verifies `context-tags.json` on both sides, expects the same helper-family focus on target and control, and then selects `compare` or `compare-groups` automatically.

To regenerate the current broad-union shader catalog, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\satellites\ts4-dx11-introspection\scripts\build-shader-catalog.ps1
```

By default this builds a union catalog from the current wide stable reference captures:
- `20260421-212139`
- `20260421-212533`
- `20260421-220041`

Outputs:
- `docs/raw/shader-catalog.md` for human-readable review
- `docs/raw/shader-catalog.json` for machine-readable analysis

Each catalog entry includes:
- shader hash
- stage
- bytecode size
- reflection signature
- input/output semantic signatures
- capture-support count across the input set
- first/last seen capture and timestamps

To compare executable-embedded `DXBC` containers against the runtime catalog, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\satellites\ts4-dx11-introspection\scripts\extract-embedded-dxbc.ps1 -BinaryPath "C:\Games\The Sims 4\Game\Bin\TS4_x64.exe" -CatalogPath .\satellites\ts4-dx11-introspection\docs\raw\shader-catalog.json -OutputMarkdown .\satellites\ts4-dx11-introspection\docs\raw\reports\embedded-dxbc-TS4_x64.md -OutputJson .\satellites\ts4-dx11-introspection\docs\raw\reports\embedded-dxbc-TS4_x64.json
```

This produces:
- an offset-by-offset list of embedded `DXBC` containers in the target executable
- exact `SHA-256` matches against the live runtime shader catalog
- a direct binary-to-runtime linkage report for further family mapping

For non-invasive external GPU frame analysis from a saved RenderDoc capture, use:

```powershell
python .\satellites\ts4-dx11-introspection\scripts\analyze-renderdoc-capture.py --capture .\satellites\ts4-dx11-introspection\ender_doc\<capture>.rdc
```

This writes:
- `renderdoc-extract.raw.json` with the raw action-tree and per-draw replay data
- `renderdoc-draws.enriched.json` with pass-level and shader-level summaries
- `renderdoc-summary.json`
- `renderdoc-summary.md`
- an extracted thumbnail when `renderdoccmd.exe` is available

The current goal of this path is to establish scene/pass ownership and draw-level shader usage without reintroducing risky in-process deferred-context hooks.

The script accepts:
- `-Executable TS4_x64_fpb.exe` to target the FPB executable;
- `-CaptureFrames 300` to change the bounded capture window;
- `-NoLaunch` to stop after build/start/install for troubleshooting.

Manual steps remain available if needed.

1. Run static recon:

```powershell
powershell -ExecutionPolicy Bypass -File .\satellites\ts4-dx11-introspection\scripts\run-recon.ps1
```

2. Start the companion:

```powershell
powershell -ExecutionPolicy Bypass -File .\satellites\ts4-dx11-introspection\scripts\start-companion.ps1
```

3. Install the proxy into `Game\Bin`:

```powershell
powershell -ExecutionPolicy Bypass -File .\satellites\ts4-dx11-introspection\scripts\install-proxy.ps1
```

4. Launch `TS4_x64.exe` or `TS4_x64_fpb.exe`.
5. Logging runs continuously. `F10` is optional and only writes a bookmark event into the logs.

To roll back quickly:

```powershell
powershell -ExecutionPolicy Bypass -File .\satellites\ts4-dx11-introspection\scripts\remove-proxy.ps1
```

## Operational Notes

- The proxy prefers a named pipe sink at `\\.\pipe\ts4-dx11-introspection-v1`.
- If the companion is not running, the native component falls back to a local JSONL file in `%TEMP%`.
- Detailed logging is continuous, but it is draw-centric rather than setter-centric: the runtime tracks binds/states in memory and attaches compact summaries to each `draw_call`.
- When TS4 routes work through deferred contexts, the runtime also records command-list creation and submission so frame-level analysis still shows when deferred work was recorded and played back.
- In current stable builds, offline capture comparison is the primary safe workflow for isolating context-specific shader sets while deferred-context draw/state hooks remain too crash-prone for TS4.
- Supporting generated artifacts and legacy notes live under `docs/raw/`; the implementation-facing document to read first is `docs/ts4-material-shader-spec.md`.
