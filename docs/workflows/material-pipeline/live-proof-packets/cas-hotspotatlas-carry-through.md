# CASHotSpotAtlas Carry-Through

This packet turns the hotspot-atlas branch into a concrete `P1` inspection document.

Question:

- when `CASHotSpotAtlas` survives into broader profile or render-adjacent packets, is that still safer to read as morph-helper provenance than as an ordinary sampled surface slot?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Edge-Family Matrix](../edge-family-matrix.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [CASHotSpotAtlas](../family-sheets/cas-hotspot-atlas.md)
- [CAS/Sim Material Authority Matrix](../cas-sim-material-authority-matrix.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
CASHotSpotAtlas Carry-Through
├─ Externally proved identity ~ 89%
├─ Local external snapshot packet ~ 71%
├─ Carry-through concentration packet ~ 74%
├─ Exact runtime render proof ~ 28%
└─ Implementation-diagnostic value ~ 63%
```

## Externally proved identity

What is already strong enough:

- [Making a CAS slider with TS4MorphMaker using a Deformer Map](https://modthesims.info/t/613057) identifies `CASHotSpotAtlas` as an EA atlas mapped to `UV1`
- the same tutorial ties atlas colors to `HotSpotControl`, then to `SimModifier`, then to morph resources such as `DMap`
- [Pointed Ears as CAS Sliders](https://db.modthesims.info/showthread.php?t=596028) reinforces the same routing packet in creator-facing terms

Safe reading:

- `CASHotSpotAtlas` is a real EA hotspot atlas
- it belongs first to the CAS editing and morph branch
- `UV1` plus hotspot-to-modifier routing are part of its identity, not incidental metadata

## Local external snapshot packet

Strongest local packet in this repo:

- [TS4SimRipper Enums.cs](../../../references/external/TS4SimRipper/src/Enums.cs): explicit `HotSpotControl` and `SimModifier` resource types
- [TS4SimRipper SIMInfo.cs](../../../references/external/TS4SimRipper/src/SIMInfo.cs): body and face modifier arrays preserved as real `SimModifierData`
- [TS4SimRipper GameFileHandler.cs](../../../references/external/TS4SimRipper/src/GameFileHandler.cs): explicit fetch path for `SimModifier` resources
- [TS4SimRipper Form1.cs](../../../references/external/TS4SimRipper/src/Form1.cs): explicit `SimModifier` fetch/use paths in the external tool snapshot

Why this packet matters:

- it gives local external confirmation that the atlas tutorial is not describing imaginary resource families
- the modifier side of the chain exists as a concrete TS4 resource packet in checked-in external tooling
- this strengthens the reading that hotspot-atlas carry-through should stay inside morph/helper provenance unless a stronger runtime packet appears

## Current candidate live targets

These are queue targets, not proof.

### Candidate group A: concentrated carry-through packet

Useful local clues:

- `tmp/precomp_sblk_inventory.json`: strongest current carry-through packet is `diffuse = 121`
- `tmp/precomp_sblk_inventory.json`: secondary packet is `VertexLightColors = 47`
- `tmp/precomp_sblk_inventory.json`: narrower carry-through packets include `staticTerrainCompositor = 18`
- `tmp/precomp_sblk_inventory.json`: narrower carry-through packets also include `StairRailings = 16`
- `tmp/precomp_shader_profiles.json`: repeated named `CASHotSpotAtlas` rows across profile packets

Safe reading:

- hotspot-atlas vocabulary is not a one-off noise event
- the name clearly carries through a broader local corpus
- the carry-through is strongest in broad render/profile archaeology, not in a direct surface-fixture packet
- that carry-through still does not prove ordinary surface sampling

### Candidate group B: morph-domain bridge packet

Useful local clue:

- `tmp/probe_3d_survey.json` currently records `SimModifier = 472` and `SimHotspotControl = 212`

Safe reading:

- the wider local survey already sees substantial modifier-side resource presence
- this supports the morph-domain branch as a real live search space
- it still does not prove that the hotspot atlas itself is directly sampled in runtime shading

### Candidate group C: best next fixture

Best current next target:

- one real CAS part or morph chain where `CASHotSpotAtlas`, `HotSpotControl`, and `SimModifier` can be inspected together

Why it is next:

- identity is already strong
- concentration is already strong enough for queueing
- the missing proof is a concrete fixture, not another generic vocabulary count

## What this packet is trying to prove

Exact target claim:

- if `CASHotSpotAtlas` appears in broader render/profile packets, the safest current reading is still helper provenance carried forward from the CAS edit/morph branch unless a concrete runtime-sampled fixture proves otherwise

Not being proved yet:

- exact runtime render semantics
- exact shader-family ranking against ordinary material textures
- proof that every hotspot-atlas carry-through packet is visible in the final render path

## Current implementation boundary

Current repo behavior is useful only as a diagnostic boundary:

- if current preview sees `CASHotSpotAtlas` near render profiles, that is evidence to preserve the name
- it is not evidence to treat the atlas like `diffuse`, `overlay`, or another ordinary sampled material slot

Diagnostic value of this packet:

- it blocks a common flattening mistake before it reaches the docs or implementation
- it keeps the render-adjacent archaeology connected to the stronger morph/edit packet

## Best next inspection step

1. Keep the tutorial-backed `CASHotSpotAtlas -> HotSpotControl -> SimModifier` route as the external baseline.
2. Use the local `TS4SimRipper` `HotSpotControl` and `SimModifier` packet as the external-local bridge.
3. Then isolate one real CAS part or modifier fixture where hotspot-atlas provenance survives into adjacent metadata without being coerced into an ordinary surface slot.

## Honest limit

This packet does not yet prove direct runtime render sampling.

What it does prove:

- `CASHotSpotAtlas` is already too well grounded to leave inside generic unresolved shader vocabulary
- current carry-through evidence is strong enough to preserve the name as morph/helper provenance while stricter live proof is still being gathered
