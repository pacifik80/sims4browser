# GenerateSpotLightmap And NextFloorLightMapXform

This packet turns the generated-light row into a concrete live-proof document.

Question:

- does the current workspace already support a bounded generated-light reading for `GenerateSpotLightmap` and `NextFloorLightMapXform` without flattening them into ordinary UV or material slots?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Edge-Family Matrix](../edge-family-matrix.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [GenerateSpotLightmap And NextFloorLightMapXform](../family-sheets/generate-spotlightmap-nextfloorlightmapxform.md)
- [Projection, Reveal, And Lightmap Families](../family-sheets/projection-reveal-lightmap.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
GenerateSpotLightmap / NextFloorLightMapXform
├─ Externally proved family identity ~ 74%
├─ Local carry-through packet ~ 84%
├─ Candidate live-root isolation ~ 64%
├─ Exact matrix semantics ~ 18%
└─ Implementation-diagnostic value ~ 71%
```

## Externally proved family identity

What is already strong enough:

- [Sims 4 lighting in Sims 3?](https://modthesims.info/showthread.php?t=646135) groups `GenerateSpotLightmap`, `GenerateWindowLightmap`, `GenerateRectAreaLightmap`, `GenerateTubeLightmap`, and `NextFloorLightMapXform` inside one TS4-specific lightmap vocabulary
- the family names themselves are strongly generated-light and transform-oriented rather than ordinary surface-slot vocabulary

Safe reading:

- `GenerateSpotLightmap` belongs in generated-light vocabulary
- `NextFloorLightMapXform` is safer as transform/helper provenance than as an ordinary UV transform or sampled texture slot

## Local carry-through packet

Strongest current local packet:

- `tmp/precomp_sblk_inventory.json`: `name_guess = "GenerateSpotLightmap"` with `occurrences = 6`
- `tmp/precomp_sblk_inventory.json`: `NextFloorLightMapXform = 14`
- `tmp/precomp_sblk_inventory.json`: secondary `NextFloorLightMapXform = 3`
- `tmp/precomp_sblk_inventory.json`: the stronger generated-light packet also keeps `SeaLevel = 14`
- `tmp/precomp_shader_profiles.json`: repeated `NextFloorLightMapXform` rows
- the same local inventory still shows `SimGhostGlassCAS` as a weaker adjacent carry-through case rather than the semantic home of the helper

Safe reading:

- the generated-light vocabulary is alive in the current corpus
- `NextFloorLightMapXform` is concentrated enough to keep as a real helper name
- the packet is still archaeology unless tied to a stronger live root

## Current candidate live roots

Best current local roots:

- `tmp/precomp_sblk_inventory.json`: `GenerateSpotLightmap` packet as the main search queue
- `tmp/probe_sample_ep06_ep10_coverage.txt`: `ClientFullBuild0.package | 01661233:00000000:00F643B0FDD2F1F7` for adjacent `WorldToDepthMapSpaceMatrix`
- `tmp/probe_sample_next24_coverage_after_nonvisual_fix.txt`: `ClientFullBuild0.package | 01661233:00000000:0124E3B8AC7BEE62` for adjacent projective/light-space behavior

Why these roots matter:

- they do not yet prove `GenerateSpotLightmap` directly
- they do give the nearest currently isolated visible/projective comparison packet in the same local survey family
- they help keep the generated-light row tied to projection/light-space behavior instead of ordinary surface decoding
- they are still comparison controls, not the first direct generated-light fixture

## What this packet is trying to prove

Exact target claim:

- the safest current reading is that `GenerateSpotLightmap` and `NextFloorLightMapXform` stay inside generated-light/helper provenance, and any current broad slot or UV treatment is approximation only

Not being proved yet:

- exact matrix structure for `NextFloorLightMapXform`
- exact visible impact of the generated-light helper on final rendered assets
- a single definitive live root where the whole helper family can be inspected end to end

## Current implementation boundary

Current repo behavior is useful only as a diagnostic boundary:

- if current preview normalizes these names into broad UV or material-slot behavior, that is approximation
- it is not authority for generated-light semantics

Diagnostic value of this packet:

- it protects the docs against a false “normal UV transform” story
- it gives a stronger bridge from lightmap-family identity to future live-root inspection

## Best next inspection step

1. Keep the TS4 lightmap discussion as the external family baseline.
2. Use the stronger `GenerateSpotLightmap` and `NextFloorLightMapXform = 14` packet as the main local queue.
3. Compare that queue against the nearby isolated `WorldToDepthMapSpaceMatrix` roots before making any stronger transform claims.

## Honest limit

This packet does not yet prove exact lightmap-transform math.

What it does prove:

- `GenerateSpotLightmap` and `NextFloorLightMapXform` are already strong enough to preserve as generated-light/helper provenance
- the current workspace still does not justify collapsing them into ordinary surface-slot or plain UV semantics
