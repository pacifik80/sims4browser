# Runtime Helper-Family Clustering Floor

This document turns the new DX11 runtime shader-interface corpus into a first clustering floor for the weakest helper-family rows.

Use it when the question is not:

- "what does one shader hash expose?"

but:

- "what repeatable interface shapes can we already group together for helper-family work?"

Related docs:

- [Runtime Shader Interface Baseline](runtime-shader-interface-baseline.md)
- [Shader Family Registry](shader-family-registry.md)
- [ShaderDayNightParameters](family-sheets/shader-daynight-parameters.md)
- [Projection, Reveal, And Lightmap Families](family-sheets/projection-reveal-lightmap.md)
- [GenerateSpotLightmap And NextFloorLightMapXform](family-sheets/generate-spotlightmap-nextfloorlightmapxform.md)
- [TS4 DX11 Context-Tagged Capture Contract](../../../satellites/ts4-dx11-introspection/docs/context-tagged-capture-contract.md)
- [TS4 DX11 Context-Tagged Capture Recipes](../../../satellites/ts4-dx11-introspection/docs/context-tagged-capture-recipes.md)
- [TS4 DX11 Context-Tagged Capture Analysis Workflow](../../../satellites/ts4-dx11-introspection/docs/context-tagged-capture-analysis-workflow.md)
- [P1 Live-Proof Queue](p1-live-proof-queue.md)
- [TS4 DX11 Raw Shader Family Registry](../../../satellites/ts4-dx11-introspection/docs/raw/shader-family-registry.md)
- [Runtime helper-family clustering snapshot](../../../tmp/runtime_helper_family_clustering_snapshot_2026-04-21.json)

## Scope status (`v0.1`)

```text
Runtime Helper-Family Clustering Floor
├─ Generic runtime interface-shape floor ~ 86%
├─ Helper-like resource/variable-name floor ~ 61%
├─ Seeded helper-family candidate clusters ~ 80%
├─ Family-local narrowing on weak rows ~ 72%
├─ Exact family ownership per cluster ~ 28%
└─ Exact draw/pass mapping ~ 12%
```

## What this floor is for

The runtime shader spec helped with per-shader interfaces, but the weakest family sheets still need a bridge from:

- many individual hashes

to:

- a few reusable cluster candidates that can later be tied to helper-family semantics.

This document provides that bridge.

## Evidence order

Use this floor in the following order:

1. externally anchored family identity from the main family sheets
2. runtime interface-shape clustering from the live DX11 corpus
3. context-specific capture rebinding once stable draw/state tagging exists
4. package-side ownership mapping only after the cluster shapes are stable

## Current runtime clustering floor

Current deduplicated broad-capture floor from the three checked-in wide sessions:

- `1922` unique hashes
- `1043` pixel shaders
- `879` vertex shaders

Current strongest generic reflection-signature shapes from the snapshot:

- `ps br=0 cb=0 in=8 out=1 = 154`
- `ps br=0 cb=0 in=9 out=1 = 112`
- `ps br=0 cb=0 in=7 out=1 = 97`
- `ps br=0 cb=0 in=3 out=1 = 78`
- `ps br=0 cb=0 in=2 out=1 = 70`
- `ps br=0 cb=0 in=4 out=1 = 65`

Safe reading:

- the broad runtime surface is dominated by interface-shape repetition, not by descriptive helper-family names
- clustering has to start from repeated shapes, not from literal strings like `RevealMap` or `NextFloorLightMapXform`
- the next tagged-capture step is no longer only a contract:
  - the standard runner now has helper presets
  - the three minimum helper-family sessions now have exact runnable recipes
  - the post-capture comparison step is now standardized too
  - the clean compare shape is now explicit too:
    - target and control should stay inside the same helper-family focus
    - the scene emphasis should differ, not the tagging discipline

## Resource-name floor

Current top runtime resource names are mostly generic:

- `Constants = 156`
- `sampler_tex[0] = 153`
- `tex[0] = 153`
- `tex[1] = 131`
- `sampler_tex[1] = 131`
- `tex[2] = 84`
- `sampler_tex[2] = 84`
- `tex = 78`
- `sampler_tex = 78`

Current helper-like resource names are much rarer:

- `srctex = 25`
- `sampler_srctex = 25`
- `dsttex = 5`
- `sampler_dsttex = 5`
- `maptex = 3`
- `sampler_maptex = 3`
- `alphatex = 1`
- `sampler_alphatex = 1`

Safe reading:

- the runtime corpus does not currently preserve helper-family names in a clean literal way
- but it does preserve a small helper-like resource vocabulary for source, destination, map, and alpha-style intermediate work

Unsafe reading:

- do not claim that `srctex` or `dsttex` already proves one exact gameplay family

## Variable-name floor

Current helper-like variable names are stronger than the helper-like resource names:

- `texscale = 26`
- `offset = 22`
- `scolor = 22`
- `mipLevels = 21`
- `srctexscale = 18`
- `texgen = 16`
- `scolor2 = 12`
- `mapScale = 3`
- `scale = 3`

Safe reading:

- the strongest runtime helper clue is not family naming
- it is repeated parameterized interface shape:
  - scale
  - offset
  - color
  - mip
  - source-texture transform

That is exactly the kind of evidence that can support family clustering before exact package ownership is solved.

## Seeded cluster candidates from the raw runtime registry

The raw runtime registry in the satellite track already seeds practical cluster candidates.

### `F03` Single-Texcoord Pixel

Current seeded shape:

- family size: `42`
- dominant shape: `TEXCOORD0 -> SV_Target0`
- richer members include:
  - `ps br=2 cb=0 in=1 out=1`
  - `ps br=5 cb=1 in=1 out=1`
  - `ps br=7 cb=1 in=1 out=1`

Useful concrete signal:

- this cluster includes the `maptex + tex + Constants` example path with `compx`, `compy`, `mapScale`, and `scale`

Safe reading:

- this is a strong candidate for utility/sample/mask-style subfamilies
- it is also the current best runtime home for the tiny `maptex` branch

### `F04` Three-Texcoord Pixel

Current seeded shape:

- family size: `59`
- dominant shape: `ps br=5 cb=1 in=3 out=1`
- raw seeded interpretation already treats it as a broad material/intermediate-combine family

Useful concrete signal:

- representative members expose:
  - `fsize`
  - `offset`
  - `scolor`
  - `texscale`

Safe reading:

- this is the strongest current runtime candidate for parameter-heavy helper or combine work
- if a weak helper-family row is later found to be a real visible/intermediate pass, `F04` is currently the best place to start looking

### `F05` Color Plus Four-Texcoord Pixel

Current seeded shape:

- family size: `48`
- dominant shape: `ps br=4 cb=0 in=5 out=1`
- input shape:
  - `COLOR0`
  - `TEXCOORD0`
  - `TEXCOORD1`
  - `TEXCOORD2`
  - `TEXCOORD3`

Safe reading:

- this is the strongest current color-aware compositor candidate in the runtime seed registry
- it is a better helper-family bridge than broad narrative claims about tint or overlay alone

## What this changes for the weak family sheets

### `ShaderDayNightParameters`

The next honest step is no longer:

- search for literal `RevealMap`-like names in runtime reflection

The next honest step is:

- start from [ShaderDayNight Runtime Cluster Candidate Floor](live-proof-packets/shader-daynight-runtime-cluster-candidate-floor.md)
- treat `F04` as the leading parameter-heavy helper/combine candidate
- keep `F05` as the nearest color-aware sibling comparator
- use [ShaderDayNight Runtime Context Gap](live-proof-packets/shader-daynight-runtime-context-gap.md) to keep the current broad runtime captures in the blocker layer:
  - they preserve both candidate clusters
  - they do not yet separate them by scene/context
- only widen back to broader helper clusters if `F04` fails under context-tagged capture

### Remaining projection/reveal branch

The remaining umbrella branch is now narrower too.

- [Projection-Reveal Runtime Cluster Candidate Floor](live-proof-packets/projection-reveal-runtime-cluster-candidate-floor.md) now sharpens the best current runtime move:
  - start from the stable `F04` `srctex + tex` packet
  - keep the `fsize`, `offset`, `scolor`, `srctexscale`, and `texscale` packet together
  - do not widen back to a broad `F03/F04/F05` middle bucket unless this route fails
- [Projection-Reveal Runtime Context Gap](live-proof-packets/projection-reveal-runtime-context-gap.md) now closes the current broad-capture ceiling for that same branch:
  - the stable `srctex + tex` packet already survives across all checked broad captures
  - current manifests still do not separate it by scene/context
  - the next honest move is one context-tagged capture, not more broad-session rereading

### `Projection / Reveal / Lightmap`

The broad umbrella now has a real runtime bridge:

- generic texture-heavy shapes are too broad
- helper-like `srctex`, `dsttex`, `maptex`, `alphatex`, `texscale`, `srctexscale`, and `texgen` are better bridge clues

That is still not exact ownership, but it is already better than narrative-only searching.

### `GenerateSpotLightmap / NextFloorLightMapXform`

The runtime corpus does not preserve these family names directly.

But the generated-light row is no longer one undifferentiated helper bucket either.

- [Generated-Light Runtime Cluster Candidate Floor](live-proof-packets/generated-light-runtime-cluster-candidate-floor.md) now narrows the first runtime move:
  - start from `F03`
  - use the stable `maptex + tex + Constants` packet with `compx`, `compy`, `mapScale`, and `scale`
  - keep `F04` as the broader comparator only after that
- [Generated-Light Runtime Context Gap](live-proof-packets/generated-light-runtime-context-gap.md) now closes the current broad-capture ceiling for that same branch:
  - the stable `maptex + tex` packet already survives across all checked broad captures
  - current manifests still do not separate it by scene/context
  - the next honest move is one lighting-heavy context-tagged capture, not more broad-session rereading

The next useful step is therefore:

- context-tagged lighting-heavy captures
- then `F03`-first narrowing before widening into broader helper/combine families

## Honest limit

This floor does not yet tell us:

- which cluster is definitively `ShaderDayNightParameters`
- which one is definitively `GenerateSpotLightmap`
- which package fields own the exposed runtime resources
- which draw states or pass order belong to each cluster

It is still a clustering floor, not final semantic closure.

## Bottom line

The runtime corpus now gives the helper-family rows something they did not have before:

- a real shape-first clustering path

The next operational step is explicit now too:

- use [TS4 DX11 Context-Tagged Capture Contract](../../../satellites/ts4-dx11-introspection/docs/context-tagged-capture-contract.md)
- use manual `context-tags.json` sidecars until native manifest tagging lands

That is enough to change the next research move from:

- more name hunting

to:

- cluster first
- label second
- map package ownership third
