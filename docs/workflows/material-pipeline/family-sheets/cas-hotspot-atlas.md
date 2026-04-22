# CASHotSpotAtlas

This sheet isolates `CASHotSpotAtlas` because it is no longer safe to leave it inside a generic unresolved shader-parameter bucket.

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Shader Family Registry](../shader-family-registry.md)
- [CAS/Sim Material Authority Matrix](../cas-sim-material-authority-matrix.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)
- [Open questions](../../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Scope status (`v0.1`)

```text
CASHotSpotAtlas
├─ Identity as EA hotspot atlas ~ 86%
├─ UV1 mapping packet ~ 82%
├─ Morph/edit routing packet ~ 79%
├─ Carry-through into render/profile archaeology ~ 34%
└─ Runtime render semantics ~ 19%
```

## Evidence order

Use this family packet in the following order:

1. creator documentation that explains what the atlas is and how it is mapped
2. creator tutorials that tie it to `HotSpotControl`, `SimModifier`, and actual morph behavior
3. local repo archaeology only as a clue that the atlas name survives into other packets

## Externally proved packet

Strongest evidence:

- [Making a CAS slider with TS4MorphMaker using a Deformer Map](https://modthesims.info/t/613057) identifies `CASHotSpotAtlas` as an EA image resource mapped to `UV1` of sim meshes
- the same tutorial ties hotspot colors to `HotSpotControl` and then to actual morph resources such as `DMap`
- the same tutorial also states that each atlas color value corresponds to a `HotSpotControl`, and that `HotSpotControl` chooses sim modifiers by slider direction and viewing angle
- [Pointed Ears as CAS Sliders](https://db.modthesims.info/showthread.php?t=596028) describes the same family in simpler creator terms: the atlas marks morphable regions and routes them into slider logic

Safe reading:

- `CASHotSpotAtlas` is a real EA hotspot atlas
- it belongs first to the CAS editing and morph pipeline
- `UV1` is part of its identity packet, not an incidental implementation detail

Unsafe reading:

- do not treat `CASHotSpotAtlas` as an ordinary surface texture slot
- do not remap it into `diffuse`, `overlay`, or `alpha` just because its name appears in broader render/profile archaeology

## Practical routing packet

The externally safest routing chain today is:

```text
CASHotSpotAtlas
      ->
HotSpotControl
      ->
SimModifier
      ->
DMap / BGEO / BOND style morph resources
      ->
GEOM deformation or slider behavior
```

What is safe to say:

- the atlas is part of edit or morph targeting, not ordinary surface shading
- atlas colors and `UV1` mapping are central to that targeting packet
- `HotSpotControl` is not just a name in the chain; the current external packet already ties it to color selection, slider direction, and viewing-angle routing
- any render-domain handling should preserve this provenance instead of flattening it

## Local external snapshot boundary

Current strongest checked-in external-tool snapshot:

- [TS4SimRipper Enums.cs](../../../references/external/TS4SimRipper/src/Enums.cs) defines `HotSpotControl`, `SimModifier`, and `DeformerMap` resource types
- [TS4SimRipper SIMInfo.cs](../../../references/external/TS4SimRipper/src/SIMInfo.cs) preserves face/body modifier arrays as weighted `SimModifier` references
- [TS4SimRipper SMOD.cs](../../../references/external/TS4SimRipper/src/SMOD.cs) exposes one downstream modifier packet with explicit links to `BGEO`, `deformerMapShape`, `deformerMapNormal`, and `BOND`
- [TS4SimRipper Form1.cs](../../../references/external/TS4SimRipper/src/Form1.cs) and [GameFileHandler.cs](../../../references/external/TS4SimRipper/src/GameFileHandler.cs) apply those linked morph resources through dedicated fetch paths

Safe reading:

- the local external snapshot strongly confirms the downstream modifier side of the hotspot/morph branch
- it makes `SimModifier -> SMOD -> BGEO/DMap/BOND` a real local tooling packet
- it does not currently provide a local parser or usage path for `CASHotSpotAtlas` itself
- it also does not currently provide a local parser or usage path for `HotSpotControl` beyond the resource-type enum

## Current repo boundary

Current repo archaeology still sees `CASHotSpotAtlas` carry-through in some profile or parameter packets.

That is useful only for one thing:

- it proves the name survives outside one narrow tutorial context and therefore should not be discarded as random noise

That is not enough to prove:

- direct runtime surface sampling
- ordinary material-slot semantics
- any exact shader-family contract

Safe wording:

- “current repo archaeology shows carry-through of hotspot-atlas provenance”

Unsafe wording:

- “the renderer should treat `CASHotSpotAtlas` like a normal sampled material map”

## Open questions

- why the atlas name survives in some non-obvious render/profile packets
- whether any runtime path samples it directly outside explicit editing workflows
- how often hotspot-atlas provenance travels together with visible overlay behavior versus pure morph metadata

## Recommended next work

1. Keep `CASHotSpotAtlas` under the CAS editing or morph branch in all docs.
2. Use the current local `TS4SimRipper` `SimModifier -> SMOD -> BGEO/DMap/BOND` packet as the restart-safe local external bridge, not as a substitute for `CASHotSpotAtlas` proof.
3. Build a separate carry-through note only if new live assets prove it has direct runtime render semantics.
4. When the name appears in render/profile dumps, record it as helper provenance first, not as a surface-slot claim.
