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
- any render-domain handling should preserve this provenance instead of flattening it

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
2. Build a separate carry-through note only if new live assets prove it has direct runtime render semantics.
3. When the name appears in render/profile dumps, record it as helper provenance first, not as a surface-slot claim.
