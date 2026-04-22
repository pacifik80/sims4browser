# Projection, Reveal, And Generated-Light Boundary

This companion keeps the umbrella `Projection / Reveal / Lightmap` packet from flattening three neighboring branches into one generic "special shader" family.

Related docs:

- [Material Pipeline Deep Dives](README.md)
- [Shader Family Registry](shader-family-registry.md)
- [Edge-Family Matrix](edge-family-matrix.md)
- [Projection, Reveal, And Lightmap Families](family-sheets/projection-reveal-lightmap.md)
- [ShaderDayNight Evidence Ledger](shader-daynight-evidence-ledger.md)
- [Generated-Light Evidence Ledger](generated-light-evidence-ledger.md)
- [RefractionMap Live Proof](live-proof-packets/refractionmap-live-proof.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
Projection / Reveal / Generated-Light Boundary
├─ Parallel branch separation ~ 91%
├─ Refraction versus reveal boundary ~ 88%
├─ Reveal versus generated-light boundary ~ 90%
└─ Exact per-branch slot closure ~ 24%
```

## Externally confirmed anchors

The current source pack supports three different semantic homes:

- refraction/projective lineage for `RefractionMap`
- reveal/light-aware layered lineage for `ShaderDayNightParameters` and `RevealMap`
- generated-light/lightmap lineage for `GenerateSpotLightmap` and `NextFloorLightMapXform`

Safe externally backed reading:

- these names live under one broader projective/reveal/light umbrella
- they do not currently justify one flattened semantic branch

## Local corroboration

The current workspace keeps all three branches alive separately:

- `RefractionMap` already has one named `Build/Buy` bridge root and a bounded object/material seam
- `ShaderDayNightParameters` already has isolated visible `Build/Buy` roots plus repeated helper vocabulary
- generated-light helpers already have concentrated carry-through packets plus adjacent projective/light-space comparison roots

Safe local reading:

- the local packet strengthens separation between these branches
- it does not prove that their exact runtime math is solved

## Bounded synthesis

What this repo can now say safely:

- `RefractionMap` should stay in the refraction/projective row
- `ShaderDayNightParameters` should stay in the layered reveal/light-aware row
- `GenerateSpotLightmap` and `NextFloorLightMapXform` should stay in the generated-light row
- future fixtures may show interaction between those rows, but interaction is not the same thing as semantic collapse

Unsafe reading:

- do not let the broad umbrella doc turn these names into one generic runtime-dependent fallback family
- do not borrow generated-light semantics to explain refraction
- do not borrow refraction or projective semantics to explain reveal/day-night helpers

## Still open

- exact slot contracts for each branch
- exact visible-pass math where branches interact in the same asset
- exact matrix semantics for generated-light helpers
