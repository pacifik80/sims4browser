# ShaderDayNightParameters Visible-Pass Proof

This packet turns `ShaderDayNightParameters` into a concrete live-proof document.

Question:

- can the current workspace already support a bounded visible-pass reading for `ShaderDayNightParameters` without collapsing reveal and light-lookup helpers into ordinary surface slots?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Edge-Family Matrix](../edge-family-matrix.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [ShaderDayNightParameters](../family-sheets/shader-daynight-parameters.md)
- [Projection, Reveal, And Lightmap Families](../family-sheets/projection-reveal-lightmap.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
ShaderDayNightParameters Visible-Pass Proof
├─ Externally proved family identity ~ 68%
├─ Reveal-helper packet ~ 74%
├─ Candidate live-root isolation ~ 79%
├─ Exact visible-pass contract ~ 36%
└─ Implementation-diagnostic value ~ 67%
```

## Externally proved family identity

What is already strong enough:

- the family name itself strongly signals layered day/night or lighting-aware behavior rather than an ordinary static surface family
- [Sims_3:Shaders\\Params](https://modthesims.info/wiki.php?title=Sims_3%3AShaders%5CParams) records `RevealMap` as a dedicated helper texture param in the same engine lineage
- [Sims_3:Shaders](https://modthesims.info/wiki.php?title=Sims_3%3AShaders) shows `RevealMap` inside `Painting`, which supports the safer reading that reveal textures are family-local helpers rather than diffuse aliases

Safe reading:

- `ShaderDayNightParameters` should stay in the layered day/night or reveal-aware branch
- `samplerRevealMap` should stay helper provenance
- `LightsAnimLookupMap` should stay narrow lighting-helper provenance

## Local candidate live-root packet

Strongest current local roots:

- `tmp/probe_sample_ep06_ep10_coverage.txt`: `ClientFullBuild0.package | 01661233:00000000:0737711577697F1C`
- `tmp/probe_sample_next24_coverage_after_nonvisual_fix.txt`: `ClientFullBuild0.package | 01661233:00000000:00B6ABED04A8F593`

Why these roots matter:

- both are already isolated as Build/Buy model roots with explicit `ShaderDayNightParameters=1`
- both stay in the `textured` payload path rather than a purely non-visual bucket
- both coexist with a narrow adjacent packet dominated by `WriteDepthMask`, which makes them useful comparison targets instead of diffuse-noise dumps

Representative local evidence:

- `tmp/probe_sample_ep06_ep10_coverage.txt`: `Material Families: painting=1, ShaderDayNightParameters=1, WriteDepthMask=2`
- `tmp/probe_sample_ep06_ep10_coverage.txt`: `payload=textured=4`
- `tmp/probe_sample_next24_coverage_after_nonvisual_fix.txt`: `Material Families: ShaderDayNightParameters=1, WriteDepthMask=1`
- `tmp/probe_sample_next24_coverage_after_nonvisual_fix.txt`: `payload=textured=2`

Safe reading:

- the family is already anchored to real visible Build/Buy model roots in the current local survey
- this is stronger than profile-name archaeology alone
- it still does not close the exact visible-pass math

## Helper packet that survives the current workspace

Useful local corroboration:

- `tmp/precomp_sblk_inventory.json`: `name_guess = "ShaderDayNightParameters"` with repeated `LightsAnimLookupMap` and `samplerRevealMap` presence
- the same inventory keeps `samplerRevealMap` concentrated enough to treat it as recurring helper vocabulary, not a one-off typo
- `tmp/precomp_shader_profiles.json` repeats both `samplerRevealMap` and `LightsAnimLookupMap` across profile packets
- [Shared TS4 Material, Texture, And UV Pipeline](../../../shared-ts4-material-texture-pipeline.md) already preserves these names under the `lighting/reveal/runtime helper` bucket instead of flattening them into ordinary slots

What this does mean:

- the current workspace now has a bounded helper packet around `ShaderDayNightParameters`
- `RevealMap` lineage plus repeated local helper presence is enough to preserve layered-light provenance in docs

What this does not mean:

- it does not prove exact day/night transition math
- it does not prove that `samplerRevealMap` is equivalent to `diffuse`, `overlay`, or `emissive`

## What this packet is trying to prove

Exact target claim:

- at least one real Build/Buy root in the current local survey already supports the safer reading that `ShaderDayNightParameters` is a visible layered family whose helper names should be preserved instead of coerced into plain slots

Not being proved yet:

- exact visible-pass blend math
- exact ranking between `ShaderDayNightParameters`, `WriteDepthMask`, and any adjacent projective controls in every asset
- exact semantics of `LightsAnimLookupMap`

## Current implementation boundary

Current repo behavior is useful only as a diagnostic boundary:

- the current preview still approximates this family through broad textured/material normalization
- that approximation is not evidence that the family really is just `diffuse + overlay + emissive`
- the current survey is useful because it proves the family reaches visible Build/Buy roots, not because the current renderer already interprets it faithfully

Diagnostic value of this packet:

- it blocks slot-flattening in future docs
- it gives two concrete roots for future side-by-side visual/manual inspection

## Best next inspection step

1. Keep the Sims-lineage `RevealMap` packet as the external helper baseline.
2. Use the two current `ClientFullBuild0.package` roots as the first concrete visible-pass comparison packet.
3. Compare those roots against the adjacent `WriteDepthMask` rows before making any stronger slot claims.

## Honest limit

This packet does not yet prove the exact TS4 visible-pass contract.

What it does prove:

- `ShaderDayNightParameters` is already too well anchored to keep under a generic unresolved-parameter bucket
- the current local workspace already supports a bounded visible-family reading with preserved reveal/light helper provenance
