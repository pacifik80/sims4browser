# Projection, Reveal, And Lightmap Families

This sheet isolates the narrow family packet around `RefractionMap`, `ShaderDayNightParameters`, `GenerateSpotLightmap`, `NextFloorLightMapXform`, and related reveal or generated-light helpers.

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Shader Family Registry](../shader-family-registry.md)
- [CAS/Sim Material Authority Matrix](../cas-sim-material-authority-matrix.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)
- [Open questions](../../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Scope status (`v0.1`)

```text
Projection / Reveal / Lightmap
├─ Refraction family identity ~ 58%
├─ ShaderDayNight layered-family packet ~ 54%
├─ Generated-light helper packet ~ 63%
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

## Current repo boundary

Current repo archaeology is still useful here only as a warning sign:

- these families survive into local profile and parameter dumps
- current implementation still tends to approximate them with broad buckets
- that approximation is evidence of an implementation gap, not evidence of family semantics

Safe wording:

- “current implementation still approximates this projection/light family”

Unsafe wording:

- “the family means X because our decoder currently maps it that way”

## Open questions

- exact TS4 visible-pass behavior for reveal helpers
- exact slot contract for `RefractionMap`
- exact matrix semantics of `NextFloorLightMapXform`
- exact boundary between generated-light helpers and any visible layered material pass

## Recommended next work

1. Keep these names under projection, reveal, or generated-light packets until stronger TS4-facing proof appears.
2. Build live comparison fixtures instead of widening generic slot guesses.
3. Only promote one of these helpers into ordinary slot semantics if a real TS4-facing source or live-asset comparison proves it.
