# Runtime Shader Interface Baseline

This document records what the new standalone DX11 runtime-capture lane contributes to the main TS4 material research track.

Use it when the question is:

- what do we now know from live runtime shader interfaces,
- what does that change in the material research docs,
- and what still remains open even after the new runtime spec exists.

Related docs:

- [Shader Family Registry](shader-family-registry.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [Build/Buy Material Authority Matrix](buildbuy-material-authority-matrix.md)
- [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md)
- [P1 Live-Proof Queue](p1-live-proof-queue.md)
- [Documentation Status Catalog](documentation-status-catalog.md)
- [Package, Runtime, And Scene Bridge Boundary](package-runtime-scene-bridge-boundary.md)
- [External GPU Scene-Pass Baseline](external-gpu-scene-pass-baseline.md)
- [TS4 Material Shader Spec](../../../satellites/ts4-dx11-introspection/docs/ts4-material-shader-spec.md)
- [Raw Material Shader Interface Spec](../../../satellites/ts4-dx11-introspection/docs/raw/material-shader-interface-spec.md)

## Scope status (`v0.1`)

```text
Runtime Shader Interface Baseline
├─ Runtime shader inventory floor ~ 92%
├─ Per-shader interface contract floor ~ 88%
├─ Executable-linked subset floor ~ 81%
├─ Family-level interface clustering ~ 34%
├─ Package-to-shader binding ownership ~ 18%
└─ Draw-state / pass-order closure ~ 12%
```

## What changed

The new DX11 runtime spec is the first checked-in artifact in this repo that gives a broad live-game shader-interface floor rather than only:

- external creator-facing family identity,
- local package-side census,
- or narrow packet-by-packet fixture proof.

Current checked-in scope from the runtime spec:

- `1922` unique runtime shaders
- `1043` pixel shaders
- `879` vertex shaders
- `364` direct executable-linked shaders
- `364` executable-linked shaders with checked-in disassembly coverage

The runtime spec currently surfaces, per shader hash:

- stage
- bytecode size
- reflection signature
- input semantics
- output semantics
- bound resources
- constant buffer layouts and variable names
- executable linkage when found
- coarse disassembly signals when available:
  - sample count
  - `sample_l` usage
  - loop presence
  - discard presence
  - dynamic constant-buffer indexing

That moves the research track from "some families probably expect texture slot patterns" to "many concrete runtime shader hashes now expose real interface contracts".

The updated spec also adds a first explicit package-side carrier layer beside the runtime layer:

- `MaterialDefinition` (`MATD`) is now named as the first authored package-side material carrier
- `MaterialSet` (`MTST`) is now named as the next package-side state or variant layer
- `Geometry`, `Model`, and `ModelLOD` are explicitly kept in the critical path for matching runtime vertex expectations back to package-side carriers
- the current checked-in resource census also keeps the material-carrying surface concrete:
  - `MaterialDefinition = 28225`
  - `MaterialSet = 514`
  - `Geometry = 187832`
  - `ModelLOD = 105743`
  - `Model = 52122`

## Evidence boundary

This is a new evidence layer, but it is not a replacement for the existing external-first contract.

Safe reading:

- external sources and creator tooling still anchor family identity and gameplay semantics
- runtime capture now strongly anchors shader-interface reality
- package-side census still anchors prevalence and carrier discovery
- current repo implementation still remains only an implementation boundary

Unsafe reading:

- do not treat a runtime shader hash as a family label by itself
- do not assume a cbuffer variable name proves the exact package-side field owner
- do not infer final blend/pass behavior from reflection alone

## What the runtime spec now closes

### 1. Runtime shader inventory is no longer hypothetical

We now have a real checked-in runtime shader floor instead of only:

- profile names from package-side material records,
- helper-family names from creator tooling,
- or isolated packets.

Current safe answer:

- yes, at the hash/interface level
- not yet at the full family/gameplay-role level

### 2. Per-shader interface contracts are now real

For many runtime-used shaders we now have a usable interface contract:

- texture resources
- sampler resources
- constant buffers
- variable names and offsets
- required geometry semantics

This is the strongest current answer to:

- what does the renderer expect a material path to satisfy?

### 3. Geometry-channel requirements are much less speculative

The runtime spec makes it much easier to justify that some paths require more than a trivial mesh:

- extra `POSITIONn` channels
- `NORMAL0`
- tangent-like or color channels
- richer `TEXCOORD` sets

That is especially useful for keeping character-side and helper-family paths from being flattened into generic object-style materials.

### 4. Some shader blobs are now bridged back to the executable

The executable-linked subset is not full engine understanding, but it is enough to create a clean bridge for later binary recon and runtime hook targeting.

Current safe answer:

- yes, for a meaningful subset

### 5. Package-side bridge priorities are less fuzzy

The spec now states the package-side reading priorities more explicitly instead of leaving them as scattered implications:

- parse `MATD` first
- treat `MTST` as the smaller state-selection layer on top
- keep `Geometry` / `Model` / `ModelLOD` in the same bridge conversation as runtime semantics

That does not solve package ownership, but it does make the bridge order less debatable.

### 6. Disassembly signals now help rank runtime clusters

The executable-linked subset now also has a lightweight behavior layer:

- `245` shaders with texture sampling instructions
- `26` with explicit `sample_l`
- `26` with explicit loops
- `66` with dynamic constant-buffer indexing

Safe reading:

- these signals do not prove family identity
- they do make some runtime clusters easier to prioritize when choosing which ones deserve the next family-local bridge work

## What the runtime spec still does not close

### 1. It does not yet tell us family ownership

The spec is hash-first and interface-first.

It does not yet tell us:

- which hashes belong to `ShaderDayNightParameters`
- which belong to projection/reveal/lightmap families
- which are character shell/compositor helpers
- which are object-side transparency branches

That requires family clustering and context rebinding.

### 2. It does not yet tell us package ownership

The spec does not yet prove:

- which package resource type owns each texture binding
- which `MATD` or `MTST` field corresponds to each runtime resource name
- which values are authored versus engine-defaulted

This remains the main package-to-runtime bridge gap.

### 3. It does not yet tell us draw-state or pass-order truth

Reflection tells us what a shader can bind, not:

- which render states were active for a given draw
- which pass order was used
- which shader hash belonged to which screen context
- or whether a helper-family path was opaque, cutout, or blended in practice

That still needs draw-level capture.

The first checked-in external answer to that missing layer now lives in:

- [External GPU Scene-Pass Baseline](external-gpu-scene-pass-baseline.md)

Safe reading:

- the runtime interface corpus still does not close draw-state or pass-order truth by itself
- but that gap is no longer completely empty, because the RenderDoc handoff now provides a real external scene/pass baseline

### 4. It still does not close the full three-way bridge

The strongest remaining global blocker is now easiest to say as one missing join:

- package-side carrier ownership
- runtime shader identity
- scene or pass context

We now have much stronger package-side priorities and much stronger runtime interface evidence.

We still do not have the final proven join that says:

- which package carrier fed which runtime shader family
- in which draw or pass context
- under which render-state conditions

That blocker now also has its own boundary document:

- [Package, Runtime, And Scene Bridge Boundary](package-runtime-scene-bridge-boundary.md)

## Immediate impact on the research track

The runtime spec strengthens the track in three concrete ways.

### A. The main global blocker is smaller

The old blocker was:

- we lacked full family-local contracts

The new truthful reading is:

- we now have many per-shader interface contracts,
- we now also have a cleaner package-side carrier priority layer,
- but we still lack the final three-way join between package carrier, runtime shader family, and scene or pass context.

So the blocker did not disappear, but it narrowed.

### B. The next useful work is no longer "find more names"

The highest-value next step is now:

- cluster runtime shader hashes into stable interface families,
- then connect those families back to package-side material structures.

### C. Weak helper-family docs now have a better upgrade path

`ShaderDayNightParameters`, `Projection / Reveal / Lightmap`, and `GenerateSpotLightmap / NextFloorLightMapXform` are still weak.

But their next step is now clearer:

- not more narrative search first,
- but family-local clustering over the runtime interface corpus,
- then context-specific rebinding from captures and package candidates.

## Practical next steps

1. Generate a normalized `runtime shader interface table` from the checked-in spec.
2. Cluster hashes by stable interface shapes:
   - resource names
   - cbuffer names
   - variable layouts
   - geometry semantics
3. Add capture-context tags so clusters can be split by:
   - `CAS`
   - `Build/Buy`
   - object transparency
   - shell/compositor-heavy scenes
4. Create family-level candidate tables for the weakest rows first:
   - `ShaderDayNightParameters`
   - `Projection / Reveal / Lightmap`
   - `GenerateSpotLightmap / NextFloorLightMapXform`
5. Only after clustering, attempt package-side field ownership mapping.
6. Use the executable-linked disassembly signals to rank which runtime clusters deserve the next family-local bridge work first.

## Bottom line

The new runtime spec is a real research unlock.

It does not finish the material problem, but it closes the broad runtime shader-interface gap enough to change what the next best work looks like:

- less name hunting,
- less prose-only family speculation,
- more family clustering over live runtime interface evidence.
