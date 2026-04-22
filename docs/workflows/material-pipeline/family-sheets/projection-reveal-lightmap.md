# Projection, Reveal, And Lightmap Families

This sheet isolates the narrow family packet around `RefractionMap`, `ShaderDayNightParameters`, `GenerateSpotLightmap`, `NextFloorLightMapXform`, and related reveal or generated-light helpers.

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Shader Family Registry](../shader-family-registry.md)
- [Package, Runtime, And Scene Bridge Boundary](../package-runtime-scene-bridge-boundary.md)
- [Helper-Family Package Carrier Boundary](../helper-family-package-carrier-boundary.md)
- [Helper-Family Carrier Plausibility Matrix](../helper-family-carrier-plausibility-matrix.md)
- [Runtime Helper-Family Clustering Floor](../runtime-helper-family-clustering-floor.md)
- [CAS/Sim Material Authority Matrix](../cas-sim-material-authority-matrix.md)
- [Projection, Reveal, And Generated-Light Boundary](../projection-reveal-generated-light-boundary.md)
- [Refraction Evidence Ledger](../refraction-evidence-ledger.md)
- [Refraction Bridge Fixture Boundary](../refraction-bridge-fixture-boundary.md)
- [ShaderDayNight Evidence Ledger](../shader-daynight-evidence-ledger.md)
- [Generated-Light Evidence Ledger](../generated-light-evidence-ledger.md)
- [Generated-Light Runtime Cluster Candidate Floor](../live-proof-packets/generated-light-runtime-cluster-candidate-floor.md)
- [Projection-Reveal Runtime Cluster Candidate Floor](../live-proof-packets/projection-reveal-runtime-cluster-candidate-floor.md)
- [Projection-Reveal Runtime Context Gap](../live-proof-packets/projection-reveal-runtime-context-gap.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)
- [Open questions](../../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Scope status (`v0.1`)

```text
Projection / Reveal / Lightmap
├─ Refraction family identity ~ 66%
├─ ShaderDayNight layered-family packet ~ 61%
├─ Generated-light helper packet ~ 71%
├─ Runtime helper-family narrowing ~ 45%
├─ Exact TS4 slot contracts ~ 23%
└─ Exact visible-pass math ~ 14%
```

## Evidence order

Use this packet in the following order:

1. TS-family naming and creator-facing lightmap discussions
2. engine-lineage corroboration for reveal or refraction vocabulary
3. local repo archaeology only as a failure-boundary clue

## Externally safest readings

### `RefractionMap`

Strongest evidence:

- [Sims_3:Shaders](https://modthesims.info/wiki.php?title=Sims_3%3AShaders) already treats refraction-oriented families and `simglass` as their own material families rather than as synonyms for diffuse sampling
- [Sims_3:0xEA5118B0](https://modthesims.info/wiki.php?title=Sims_3%3A0xEA5118B0) exposes `Refraction Distortion Scale` in lineage-era material blocks, which is strong support for refraction-specific helper semantics
- family names in that lineage support dedicated refraction behavior and helper parameters instead of ordinary one-pass surface slots

Safe reading:

- `RefractionMap` belongs under projection or refraction families
- family-local names near it, including `tex1`, should stay unresolved inside that branch until stronger TS4-facing proof appears
- [Refraction Evidence Ledger](../refraction-evidence-ledger.md) now keeps external corroboration, local survey/bridge evidence, and bounded synthesis separate for this row

Unsafe reading:

- do not coerce `RefractionMap` into ordinary diffuse-slot semantics

### `ShaderDayNightParameters`

Strongest evidence:

- the family name itself clearly signals layered day/night or lighting-aware behavior
- [Sims_3:Shaders\\Params](https://modthesims.info/wiki.php?title=Sims_3%3AShaders%5CParams) supports the broader lineage point that shader params like reveal or alpha-style maps are family-local helpers, not automatically ordinary base-color maps
- reveal-map lineage from older Sims shader docs supports the idea that reveal textures are helper or mask inputs, not ordinary base-color maps

Safe reading:

- `ShaderDayNightParameters` should stay a layered family
- `samplerRevealMap` should stay helper provenance
- `LightsAnimLookupMap` should stay a narrow light-lookup helper

Unsafe reading:

- do not normalize these names into ordinary `diffuse`, `overlay`, or `emissive` truth claims unless stronger TS4-facing proof appears

### `GenerateSpotLightmap` and `NextFloorLightMapXform`

Strongest evidence:

- [Sims 4 lighting in Sims 3?](https://modthesims.info/showthread.php?t=646135) groups `NextFloorLightMapXform` with `GenerateSpotLightmap` and related generated-light names
- the shared name family itself is generated-light vocabulary rather than ordinary material-slot vocabulary

Safe reading:

- this is a generated-light or lightmap-helper branch
- `NextFloorLightMapXform` is safer as transform/helper provenance than as a directly sampled material slot

Unsafe reading:

- do not reinterpret generated-light helper names as ordinary surface-slot vocabulary

## Current branch boundary

This umbrella sheet now has an explicit boundary companion:

- [Projection, Reveal, And Generated-Light Boundary](../projection-reveal-generated-light-boundary.md)

Safe reading:

- `RefractionMap`, `ShaderDayNightParameters`, and `GenerateSpotLightmap` / `NextFloorLightMapXform` currently live under one broader umbrella
- they do not currently collapse into one semantic branch

## Current repo boundary

Current repo archaeology is still useful here only as a warning sign:

- these families survive into local profile and parameter dumps
- current implementation still tends to approximate them with broad buckets
- that approximation is evidence of an implementation gap, not evidence of family semantics

Safe wording:

- “current implementation still approximates this projection/light family”

Unsafe wording:

- “the family means X because our decoder currently maps it that way”

## Runtime clustering floor

The new runtime shader-interface corpus now gives this umbrella sheet a better next step than narrative-only follow-up.

Current strongest runtime floor:

- generic names dominate:
  - `Constants = 156`
  - `tex[0] = 153`
  - `tex[1] = 131`
- helper-like names survive only in a smaller subset:
  - `srctex = 25`
  - `dsttex = 5`
  - `maptex = 3`
  - `alphatex = 1`
- helper-like variables are stronger:
  - `texscale = 26`
  - `offset = 22`
  - `scolor = 22`
  - `mipLevels = 21`
  - `srctexscale = 18`
  - `texgen = 16`

Current safe reading:

- the runtime bridge for this umbrella is shape-first, not label-first
- `F03`, `F04`, and `F05` from the raw runtime family seed are now the best current clustering anchors for projection, reveal, and generated-light follow-up
- [Generated-Light Runtime Cluster Candidate Floor](../live-proof-packets/generated-light-runtime-cluster-candidate-floor.md) now sharpens one branch inside that umbrella:
  - generated-light should start from `F03`
  - the stable `maptex` packet is currently stronger than a broad `F04/F05` first pass for that row
- [Projection-Reveal Runtime Cluster Candidate Floor](../live-proof-packets/projection-reveal-runtime-cluster-candidate-floor.md) now sharpens the remaining middle branch:
  - projection/reveal should start from the stable `F04` `srctex + tex` packet
  - this branch no longer needs to start from a broad `F03/F04/F05` bucket either
- [Projection-Reveal Runtime Context Gap](../live-proof-packets/projection-reveal-runtime-context-gap.md) now closes the current broad-capture ceiling for that same middle branch:
  - the stable `srctex + tex` packet already survives across all checked broad captures
  - the checked-in manifests still do not carry scene labels strong enough for family-context closure
  - the next honest move is one context-tagged capture, not more re-reading of the same broad sessions

## Package carrier boundary

This umbrella now also has a stronger offline carrier boundary.

Current safe carrier order:

1. `MATD`
2. `MTST`
3. `Geometry` / `Model` / `ModelLOD`

Current safe reading:

- [Helper-Family Package Carrier Boundary](../helper-family-package-carrier-boundary.md) now makes the offline ownership limit explicit for all three sub-branches:
  - `ShaderDayNightParameters` is not yet safe as a direct `MATD` family claim
  - generated-light is not yet safe as a direct `MATD` or default `MTST` owner claim
  - projection/reveal is not yet safe as a direct authored carrier claim from current counts alone
- [Package, Runtime, And Scene Bridge Boundary](../package-runtime-scene-bridge-boundary.md) keeps the missing join explicit:
  - package carrier order is now good enough to test plausibility
  - runtime cluster narrowing is now good enough to test the candidate side
  - scene/pass context is still what blocks final closure
- [Helper-Family Carrier Plausibility Matrix](../helper-family-carrier-plausibility-matrix.md) now also keeps the three-row offline comparison compact:
  - `ShaderDayNight` stays carrier-constrained but not carrier-closed
  - generated-light stays carrier-constrained but not carrier-closed
  - projection/reveal stays carrier-constrained but not carrier-closed

Safe wording:

- "package-side carriers now constrain ownership claims across the umbrella"
- "the umbrella is still open at the final package-to-runtime-to-scene join"

Unsafe wording:

- "this umbrella now has a proved authored owner"
- "projection/reveal and generated-light now collapse into one package family"

## Open questions

- exact TS4 visible-pass behavior for reveal helpers
- exact slot contract for `RefractionMap`
- exact matrix semantics of `NextFloorLightMapXform`
- exact boundary between generated-light helpers and any visible layered material pass

## Recommended next work

1. Keep these names under projection, reveal, or generated-light packets until stronger TS4-facing proof appears.
2. Start generated-light runtime narrowing from [Generated-Light Runtime Cluster Candidate Floor](../live-proof-packets/generated-light-runtime-cluster-candidate-floor.md).
3. Start the remaining projection/reveal runtime narrowing from [Projection-Reveal Runtime Cluster Candidate Floor](../live-proof-packets/projection-reveal-runtime-cluster-candidate-floor.md).
4. Use [Projection-Reveal Runtime Context Gap](../live-proof-packets/projection-reveal-runtime-context-gap.md) to avoid overreading the current broad capture corpus.
5. Use the runtime helper-family clustering floor before widening more generic slot guesses.
6. Use [Helper-Family Package Carrier Boundary](../helper-family-package-carrier-boundary.md) before promoting any umbrella row into authored carrier ownership.
7. Use [Helper-Family Carrier Plausibility Matrix](../helper-family-carrier-plausibility-matrix.md) when the question is which umbrella row is plausible versus actually promotable.
8. Build live comparison fixtures and context-tagged captures instead of widening generic slot guesses.
9. Only promote one of these helpers into ordinary slot semantics if a real TS4-facing source or live-asset comparison proves it.
