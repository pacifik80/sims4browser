# External GPU Scene-Pass Baseline

This document records what the checked-in RenderDoc handoff contributes to the main TS4 material research track.

Use it when the question is not:

- "what runtime shader interfaces exist at all?"

but:

- "what do external GPU frame captures now prove about scene ownership, pass families, and draw-level material versus compositor separation?"

Related docs:

- [Package, Runtime, And Scene Bridge Boundary](package-runtime-scene-bridge-boundary.md)
- [Runtime Shader Interface Baseline](runtime-shader-interface-baseline.md)
- [Shader Family Registry](shader-family-registry.md)
- [P1 Live-Proof Queue](p1-live-proof-queue.md)
- [TS4 RenderDoc Handoff](../../../satellites/ts4-dx11-introspection/docs/ts4-renderdoc-handoff.md)
- [TS4 DX11 Context-Tagged Capture Contract](../../../satellites/ts4-dx11-introspection/docs/context-tagged-capture-contract.md)

## Scope status (`v0.1`)

```text
External GPU Scene-Pass Baseline
├─ Scene-domain split floor ~ 86%
├─ Pass-family classification floor ~ 82%
├─ Material-like versus compositor-like separation ~ 78%
├─ Depth-only helper-pass recognition ~ 81%
├─ Package-to-pass signature matching route ~ 49%
└─ RenderDoc-to-live hash normalization ~ 6%
```

## What changed

The checked-in RenderDoc handoff is the first external GPU-capture layer in this repo that is already reduced to main-project use.

It does not just add more shader blobs.

It adds:

- scene-domain truth
- pass-family truth
- representative draw-level material/compositor evidence
- a practical ingest contract for using that truth before exact shader-hash closure exists

Current checked-in capture surface:

- `yafem_amale_table`
  - `CAS`
  - `89` draw calls
  - `123` actions
- `room_top_view`
  - `WorldRoom`
  - `749` draw calls
  - `802` actions

## High-confidence external GPU facts

What is now safe from external GPU capture:

1. TS4 frames are partitioned into multiple pass families.
2. At least some world/room frames use explicit `Depth-only` passes before major color/material passes.
3. `CAS` frames and `WorldRoom` frames do not share one flat pass layout.
4. There is a repeatable split between:
   - `MaterialLike`
   - `CompositorOrUi`
   - `DepthOnly`
5. Repeated `tex[0]` / `tex[1]` plus `COLOR` / `SV_Position` is strong exclusion evidence for normal authored material ownership.
6. Repeated `texture0..N` plus `cbuffer0/cbuffer2/cbuffer7` in depth-adjacent geometry passes is strong positive evidence for visible material-style rendering.

## Current strongest pass-family evidence

### `CAS`

Strongest current material-like pass:

- `Colour Pass #3 (1 Targets + Depth)`
- representative events:
  - `522`
  - `532`
- repeated PS resources:
  - `texture0`
  - `texture1`
  - `texture2`
  - `texture3`
  - `texture4`
- repeated constant buffers:
  - `cbuffer0`
  - `cbuffer7`

Safe reading:

- this is the strongest current external GPU evidence for visible CAS material work
- it is much stronger than compositor-like passes as a candidate source for authored character-material hypotheses

Strongest current compositor-like pass:

- `Colour Pass #5 (1 Targets)`
- representative event:
  - `996`
- repeated resources:
  - `tex[0]`
  - `tex[1]`
- repeated inputs:
  - `COLOR`
  - `SV_Position`

Safe reading:

- this is strong negative-control evidence
- it should not be treated as normal authored character-material ownership without contrary proof

### `WorldRoom`

Strongest current material-like pass:

- `Colour Pass #6 (1 Targets + Depth)`
- representative events:
  - `5759`
  - `5822`
  - `5733`
  - `5744`
- repeated PS resources:
  - `texture0`
  - `texture1`
  - `texture2`
  - `texture3`
  - `texture4`
- repeated constant buffers:
  - `cbuffer0`
  - `cbuffer2`
- representative draw sizes:
  - `22155`
  - `20346`
  - `18924`
  - `12864`

Safe reading:

- this is the strongest current external GPU evidence for visible world/object material work
- it is strong enough to separate authored material-style passes from later compositor-style passes

Strongest current compositor-like pass:

- `Colour Pass #10 (1 Targets)`
- repeated resources:
  - `tex[0]`
  - `tex[1]`
- repeated VS/PS pair:
  - `fa885f...`
  - `c953833...`

Safe reading:

- this cross-capture pattern is strong reusable exclusion evidence for normal authored material ownership

Strongest current depth-only evidence:

- `Depth-only Pass #1`
- `Depth-only Pass #2`
- `Depth-only Pass #3`

Safe reading:

- these should be modeled as helper/depth-prepass families
- they show geometry participation, not final visible material ownership

## Why this matters to the main project

The main project's current global blocker is the missing bridge:

- package-side carrier
- runtime shader family or cluster
- scene or pass context

This new baseline strengthens one whole side of that bridge:

- scene/pass context is no longer abstract
- pass-family ownership is no longer inferred only from local runtime clustering

That means the main project can now:

- reject many false `MATD/MTST -> visible material pass` hypotheses earlier
- separate candidate visible material passes from screen/compositor helpers
- separate helper/depth-prepass participation from final shading ownership

## Immediate application rules

Current safe rules for the main project:

1. import scene domain as a hard filter:
   - `CAS`
   - `WorldRoom`
2. import pass class as a hard filter:
   - `MaterialLike`
   - `CompositorOrUi`
   - `DepthOnly`
   - `Unknown`
3. treat `texture0..N` plus geometry-style large draws and depth-adjacent color passes as positive visible-material evidence
4. treat `tex[0]/tex[1]` plus screen-style inputs as compositor exclusion evidence
5. treat `DepthOnly` passes as helper evidence, not visible ownership closure

## Matching strategy uplift

The strongest immediate use is not direct hash closure.

The strongest immediate use is signature matching.

Current safe package-to-pass signature inputs:

- package-side:
  - `MATD.shader`
  - texture slot names
  - texture slot count
  - parameter names
  - `MTST` state context
  - asset domain
- pass-side:
  - scene domain
  - pass class
  - resource vocabulary
  - constant-buffer vocabulary
  - vertex-input vocabulary
  - draw scale

Safe reading:

- this is already useful before exact RenderDoc-to-live hash closure exists
- it should be used to rank and filter hypotheses, not to overclaim exact authored ownership

## Honest limit

This baseline does not close:

- exact `MATD` / `MTST` -> shader-hash identity
- exact authored-material ownership per pass
- exact RenderDoc-to-live shader normalization

Current hard blocker remains:

- RenderDoc replay shader hashes do not yet live in the same identity space as the checked-in live DX11 catalog

That means the current safe gain is:

- scene/pass truth
- pass-family truth
- stronger positive and negative evidence for package-material matching

not:

- full three-way bridge closure
