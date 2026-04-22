# Task Brief: TS4 DX11 Shader And Material Introspection

## Why This Exists

The current research track has reached the point where public modding sites and creator documentation no longer provide enough truth about actual runtime shader behavior.

What is still missing is not more mentions of shader families, but direct evidence of:
- which compiled shaders are actually used at runtime;
- which resources and constant buffers are bound to them;
- which blend/depth/raster states are active for specific draws;
- how object, CAS, shell, overlay, transparency, and helper-family passes differ in practice.

The goal of this task is to build our own source of truth from the live DX11 render path of The Sims 4.

## Primary Goal

Build a standalone TS4 runtime introspection toolchain that can log enough DirectX 11 state to support later shader-family and material-authority analysis.

The preferred shape is:
- a native DX11 interception component;
- an external companion/logger process;
- structured output suitable for offline clustering and documentation work.

## Non-Goals

This task is not trying to:
- replace or modify the game's rendering;
- build a visual overlay or UI-heavy debugging tool;
- fully decompile the engine;
- recover original HLSL source text;
- integrate into the main browser application yet.

## Preferred Architecture

### 1. Static Recon Pass

Do a lightweight pass over the game `exe` and major `dll` files first, only to locate:
- DX11 initialization points;
- imported graphics APIs;
- likely shader creation/binding call sites;
- embedded `DXBC` blocks, if any;
- useful strings and render-related symbols.

This pass is preparation, not the primary evidence source.

### 2. Native Runtime Component

Build a native component that can observe the DX11 pipeline during gameplay.

Prefer one of these approaches:
- proxy `dxgi.dll`/`d3d11.dll` style loading if practical;
- injected helper DLL if a safer/cleaner launcher path exists;
- another lightweight DX11 hook approach, provided it remains easy to run and remove.

The component should prioritize logging over invasive behavior.

### 3. External Companion Process

Build a separate process that:
- receives events from the runtime component;
- deduplicates repeated data;
- stores compact structured logs;
- can emit per-frame or per-window snapshots on demand.

Communication can be via:
- named pipe;
- shared memory plus control messages;
- append-only structured file output if that is simpler for MVP.

## Minimum Viable Logging Scope

The MVP should not try to log every API call. It should focus on the calls that produce the highest-value evidence.

### Must capture

- frame boundaries:
  - `Present`
- shader creation:
  - `CreateVertexShader`
  - `CreatePixelShader`
  - optionally `CreateGeometryShader`
  - optionally `CreateHullShader`
  - optionally `CreateDomainShader`
- shader binding:
  - `VSSetShader`
  - `PSSetShader`
- resource binding:
  - `VSSetShaderResources`
  - `PSSetShaderResources`
  - `VSSetConstantBuffers`
  - `PSSetConstantBuffers`
  - `PSSetSamplers`
- output/render state:
  - `OMSetBlendState`
  - `OMSetDepthStencilState`
  - `RSSetState`
- draw calls:
  - `Draw`
  - `DrawIndexed`
  - `DrawInstanced`
  - `DrawIndexedInstanced`

### Strongly preferred on shader creation

For every newly seen shader blob:
- compute a stable hash of the bytecode;
- record shader stage;
- record bytecode length;
- run shader reflection;
- extract constant-buffer layout;
- extract bound resource declarations;
- extract input/output signatures where available.

## User Workflow Requirement

The final tool should support a simple human workflow:

1. User starts the game with the logger enabled.
2. User opens specific contexts:
   - CAS
   - skin/overlay-heavy Sim setup
   - Build/Buy windows
   - curtains
   - glass-heavy objects
   - lighting/day-night sensitive scenes
3. User triggers a snapshot hotkey or bounded capture mode.
4. Tool writes compact structured logs for the relevant frames.
5. Offline scripts can later group the data by shader hash, stage, bindings, and state.

## Data Model Expectations

The output should be good enough to answer questions like:
- Which unique pixel shaders appear in CAS versus Build/Buy?
- Which shaders repeatedly bind alpha-related textures or cutout-like constant buffers?
- Which draws run with blending enabled versus opaque-like state?
- Which helper-family passes appear only in certain contexts?
- Which shader hashes recur across many assets and which are rare?

At minimum, design for these entities:

- `ShaderRecord`
  - hash
  - stage
  - bytecode_size
  - reflection summary
- `FrameRecord`
  - frame index
  - timestamp
  - optional capture label
- `DrawRecord`
  - frame index
  - draw index
  - bound shader hashes
  - bound SRV slots
  - bound CB slots
  - blend/depth/raster state summary
- `SnapshotRecord`
  - user label
  - frame window
  - coarse context note

JSON Lines is acceptable for MVP. SQLite is also acceptable if easier for incremental querying.

## Priorities

### Priority 1

Prove the runtime component can:
- load with the game;
- survive normal gameplay;
- log new shader creation events and hashes;
- emit a bounded capture around selected frames.

### Priority 2

Add reflection and binding summaries so the logs become analytically useful.

### Priority 3

Add context tagging and better deduplication so captures stay compact enough for repeated runs.

## Suggested MVP Deliverables

- isolated project layout under this folder;
- short setup instructions;
- native runtime logger that records at least shader creation plus frame boundaries;
- external companion that writes structured logs;
- one sample capture from a simple scene;
- one analysis script that prints:
  - unique shader hashes by stage;
  - top repeated shader hashes;
  - first-pass reflection summary for each shader.

## Success Criteria

Treat the MVP as successful if all of the following are true:

- the game launches with the logger enabled;
- at least one CAS session and one Build/Buy session can be captured without crashing;
- the logs show stable repeated shader hashes across repeated runs;
- at least one vertex shader and one pixel shader produce usable reflection output;
- the output is compact enough to diff and query offline.

## Risks And Constraints

- The Sims 4 is now on a DX11 path for supported Windows setups, but local configuration can still matter.
- Release binaries likely have no useful debug symbols.
- Full source-level shader debugging should not be assumed.
- Over-logging will create unusable noise and may hurt performance.
- Injection/proxy approach should be chosen to minimize breakage and ease removal.

## First Implementation Cut

If starting from zero, do this in order:

1. static binary recon for DX11 entry points;
2. create native hook skeleton;
3. log `Present`;
4. log shader creation with hash and stage;
5. add reflection on shader creation;
6. add draw-call and bind-state snapshots behind an opt-in hotkey/capture window;
7. add offline summarizer.

## Nice-To-Have Later

- per-context labels such as `CAS`, `BuildBuy`, `LiveMode`
- pixel-history-style target selection
- capture presets for specific research lanes
- bridge output into existing repository docs/status pages

## Handoff Prompt Seed

Use this if you want to continue the work in a separate task window:

> Build a standalone TS4 DX11 runtime introspection tool under `satellites/ts4-dx11-introspection/`. Do not integrate it into the browser app yet. Start with a lightweight static recon pass over the game binaries, then implement an MVP native DX11 logger plus companion process. The MVP must log frame boundaries, shader creation, stable shader hashes, and reflection summaries, and should be designed to grow into bounded per-frame capture of shader binds, resources, constant buffers, render states, and draw calls. Optimize for trustworthy structured logs and low operational friction rather than UI polish.

