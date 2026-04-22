# Package, Runtime, And Scene Bridge Boundary

This document isolates the current global bridge problem for the TS4 material track.

Use it when the question is not:

- "what does one package carrier prove?"
- "what does one runtime shader hash prove?"

but:

- "what exactly is still missing between package-side material carriers, runtime shader families, and scene or pass context?"

Related docs:

- [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md)
- [Shader Family Registry](shader-family-registry.md)
- [Runtime Shader Interface Baseline](runtime-shader-interface-baseline.md)
- [MATD Shader Census Baseline](matd-shader-census-baseline.md)
- [Runtime Helper-Family Clustering Floor](runtime-helper-family-clustering-floor.md)
- [Helper-Family Package Carrier Boundary](helper-family-package-carrier-boundary.md)
- [External GPU Scene-Pass Baseline](external-gpu-scene-pass-baseline.md)
- [P1 Live-Proof Queue](p1-live-proof-queue.md)
- [TS4 Material Shader Spec](../../../satellites/ts4-dx11-introspection/docs/ts4-material-shader-spec.md)
- [TS4 DX11 Context-Tagged Capture Contract](../../../satellites/ts4-dx11-introspection/docs/context-tagged-capture-contract.md)
- [TS4 DX11 Context-Tagged Capture Recipes](../../../satellites/ts4-dx11-introspection/docs/context-tagged-capture-recipes.md)
- [TS4 DX11 Context-Tagged Capture Analysis Workflow](../../../satellites/ts4-dx11-introspection/docs/context-tagged-capture-analysis-workflow.md)

## Scope status (`v0.1`)

```text
Package / Runtime / Scene Bridge Boundary
├─ Package-side carrier priority layer ~ 79%
├─ Runtime shader-interface layer ~ 91%
├─ Family-local runtime cluster layer ~ 47%
├─ Package-to-runtime ownership closure ~ 18%
├─ Scene/pass rebinding layer ~ 22%
└─ Full three-way join ~ 12%
```

## Why this exists

The current research track is no longer blocked by "we know nothing".

It is blocked by one narrower but still global missing join:

1. package-side carriers
2. runtime shader identities or clusters
3. scene or pass context

All three layers now exist in partially useful form.

They are not yet closed into one authoritative bridge.

## Current strongest package-side layer

The updated shader spec now sharpens the package-side carrier order:

- `MaterialDefinition` (`MATD`) is the first authored package-side material carrier
- `MaterialSet` (`MTST`) is the next state or variant layer
- `Geometry`, `Model`, and `ModelLOD` stay in the critical path because runtime vertex expectations must line up with those carriers

Current counted carrier surface from checked-in sources:

- `MaterialDefinition = 28225`
- `MaterialSet = 514`
- `Geometry = 187832`
- `ModelLOD = 105743`
- `Model = 52122`

Safe reading:

- package-side priority is no longer fuzzy
- `MATD` and `MTST` do not solve the bridge by themselves
- package counts do not identify runtime family ownership on their own

## Current strongest runtime layer

The runtime shader-interface corpus is already strong at the interface level:

- `1922` unique runtime shaders
- `1043` pixel shaders
- `879` vertex shaders
- `364` executable-linked shaders

The spec now also carries a lightweight behavior layer for the executable-linked subset:

- `245` shaders with texture sampling instructions
- `26` with explicit `sample_l`
- `26` with explicit loops
- `66` with dynamic constant-buffer indexing

Safe reading:

- runtime interface reality is no longer the weak side of the research stack
- reflection plus coarse disassembly signals still do not prove family ownership
- runtime hashes are not yet the same thing as package-side material identities

## Current strongest middle layer

The helper-family side now has a real middle layer instead of prose-only speculation:

- `ShaderDayNightParameters` can start from `F04`, with `F05` as the nearest comparator
- generated-light can start from `F03 maptex`
- projection/reveal can start from `F04 srctex + tex`

That is enough to say:

- some runtime cluster candidates are now real and reusable

That is not enough to say:

- which package-side carrier owns each cluster
- or which draw/pass context promotes a cluster into one family row

## Current strongest scene/pass layer

The external GPU capture layer is no longer empty.

[External GPU Scene-Pass Baseline](external-gpu-scene-pass-baseline.md) now gives one checked-in external pass/context floor:

- `CAS` versus `WorldRoom` scene-domain split
- `MaterialLike` versus `CompositorOrUi` versus `DepthOnly` pass-family split
- repeated cross-capture compositor exclusion pattern:
  - `tex[0]`
  - `tex[1]`
  - `COLOR`
  - `SV_Position`
- repeated positive material-style pattern:
  - large indexed geometry draws
  - depth-adjacent color passes
  - `texture0..N`
  - `cbuffer0/cbuffer2/cbuffer7`

That is enough to say:

- scene/pass truth is now materially stronger than before
- many false `MATD/MTST -> visible material pass` hypotheses can now be filtered earlier

That is not enough to say:

- one captured pass equals one authored material
- RenderDoc replay shader identity already closes to the live DX11 catalog

## What is actually missing

The missing join is now easiest to state explicitly.

We still do not have a proved chain like:

`package carrier -> runtime shader cluster/family -> scene or pass context`

In practice that means these questions remain open:

- which `MATD` or `MTST` structures satisfy which runtime cluster
- whether one runtime cluster belongs to one family or several neighboring families
- which target scene or pass actually concentrates that cluster
- which render-state conditions make the same cluster behave as opaque, cutout, blended, projected, or helper-only

## Safe current reading

What is already safe:

- package-side carrier priority is much better defined than before
- runtime shader-interface contracts are much better defined than before
- helper-family candidate clustering is much better defined than before
- external scene/pass truth is now much better defined than before

What is not yet safe:

- "this package-side carrier owns this runtime family"
- "this runtime cluster is definitively the day/night family" without context-tagged capture proof
- "this family closes as one render-pass behavior" without draw/state evidence

## Practical meaning for the queue

This boundary changes the next-step logic:

- more blind name-hunting is low value
- more unlabeled broad captures are low value
- the best next work must strengthen one edge of the missing join

There are now two honest routes:

1. tagged runtime route
   - use target/control tagged captures
   - strengthen the `runtime cluster -> scene/pass context` side
2. package-bridge route
   - compare package-side carrier structures against the narrowed runtime clusters
   - strengthen the `package carrier -> runtime cluster` side
3. external pass-family route
   - use RenderDoc-derived `SceneDomain` plus `PassClass` as a hard filter
   - reject compositor/depth-only false positives before deeper authored-material matching

The current restart-safe ownership-boundary companion for that route is:

- [Helper-Family Package Carrier Boundary](helper-family-package-carrier-boundary.md)

## Best current next step

If the game can be run:

- do the tagged target/control capture pairs first

If the game cannot be run:

- do the external pass-family route plus package-bridge route first
- specifically:
  - import `SceneDomain` plus `PassClass` from the checked-in RenderDoc handoff
  - compare narrowed helper-family cluster candidates against `MATD`, `MTST`, `Geometry`, `Model`, and `ModelLOD` structure expectations
  - keep the result at ownership-boundary wording, not full closure wording

## Honest limit

This document does not solve the bridge.

It exists so the whole research track stops talking about the blocker too vaguely.

The blocker is no longer:

- "we need more research"

It is now:

- "we need one proved join between package-side carriers, runtime shader families, and scene/pass context"
