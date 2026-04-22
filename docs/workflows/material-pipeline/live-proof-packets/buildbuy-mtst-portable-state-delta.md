# Build/Buy MTST Portable-State Delta

This packet promotes the `MTST` seam past the default-state-only floor and into a stronger texture-bearing stateful-material fixture.

Question:

- does the current workspace already prove one real `Build/Buy` model root where `MTST` state changes survive as portable shader-property deltas with actual texture-bearing material payload, not only as non-portable control-property variance?

Related docs:

- [Build/Buy Material Authority Matrix](../buildbuy-material-authority-matrix.md)
- [Build/Buy Stateful Material-Set Seam](../buildbuy-stateful-material-set-seam.md)
- [Build/Buy MTST Default-State Boundary](buildbuy-mtst-default-state-boundary.md)
- [Material Pipeline Deep Dives](../README.md)
- [Live-Proof Packets](README.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
Build/Buy MTST Portable-State Delta
├─ External material-set authority ~ 86%
├─ Fixture-root identity ~ 64%
├─ Texture-bearing MTST proof ~ 84%
├─ Portable-state delta wording ~ 88%
└─ Full swatch/state closure ~ 29%
```

## Externally proved authority baseline

What is already strong enough:

- [The Sims 4 Modders Reference: File Types](https://thesims4moddersreference.org/reference/file-types/) describes `Material Set` as sets of `Material Definitions`
- [Sims_4:0x01D10F34](https://modthesims.info/wiki.php?title=Sims_4%3A0x01D10F34) keeps `MLOD` as the mesh-group layer that points to `MATD` or `MTST`
- [Sims_4:RCOL](https://modthesims.info/wiki.php?title=Sims_4%3ARCOL) keeps `MODL`, `MLOD`, `MATD`, and `MTST` together in one object-side scenegraph packet
- [Info | COBJ/OBJD resources](https://modthesims.info/t/551120) and the [EA material-variant thread](https://forums.ea.com/discussions/the-sims-4-mods-and-custom-content-en/simgurumodsquad-looking-for-cross-reference-between-catalogobject-or-object-defi/1213694/replies/1213695) keep swatch and variant selection inside the object-side model/material chain rather than as loose texture discovery

Safe reading:

- `MTST` is a real object-side material authority seam
- a stronger fixture only needs to prove that real stateful behavior survives as portable material deltas at the model/material layer
- it still does not need to close full swatch/object-state identity to be useful

## Current local fixture

Current fixture root:

- package: `EP10\ClientFullBuild0.package`
- display: `Build/Buy Model 05773EECEE557829`
- root: `01661233:00000000:05773EECEE557829`
- source artifact: `tmp/probe_0577_after_heuristic_filter.txt`

What the probe already says:

- exact-instance `ObjectCatalog/ObjectDefinition` metadata was not found for this model
- the asset remains usable through its model-rooted identity
- `ModelLOD candidates = 3`
- `Texture candidates = 1`
- repeated `MTST` chunks exist on the same model-root packet:
  - `Chunk 10: ... tag=MTST`
  - `Chunk 16: ... tag=MTST`
  - `Chunk 9: ... tag=MTST`
  - `Chunk 15: ... tag=MTST`

Safe reading:

- this is a real model-rooted `Build/Buy` fixture, not a closed swatch-level object fixture
- the lack of exact `OBJD/COBJ` identity is still an honest limit, not a reason to discard the packet

## What the fixture proves

The current probe exposes a stronger stateful-material result than the earlier default-state boundary:

- one material remains a weaker control case:
  - `Material_C33144C8`
  - `source=FallbackCandidate`
  - `textures=1`
  - `MTST` state variants only change non-portable control properties
- one material becomes the stronger seam fixture:
  - `Material_B45072B0`
  - `shader=Shader_123F3EF3`
  - `source=MaterialSet`
  - `alpha=alpha-test-or-blend`
  - `textures=2`
  - `MTST exposes multiple material states`
  - preview kept the inferred default state after evaluating the available entries
  - state scores remain tied:
    - `0x00000000 = 210`
    - `0xF4BD1CE9 = 210`

The important improvement is the state delta itself:

- the state variants change portable shader properties, not only non-portable control properties
- current explicitly surfaced deltas:
  - `AmbientDomeBottom`
    - `0x00000000 -> word[3]=0xEE557829`
    - `0xF4BD1CE9 -> word[3]=0xD6060725`
  - `CloudColorWRTHorizonLight1`
    - `0x00000000 -> vector=[0, 0, 0, 0]`
    - `0xF4BD1CE9 -> vector=[-0, 0, 0, 0]`

This is enough to prove:

- the workspace already has a texture-bearing `Build/Buy` `MTST` fixture, not only a default-state floor with zero texture coverage
- `MTST` state choice is surviving as part of portable material semantics for at least one `MaterialSet`-sourced material
- the safest boundary is now stronger than “stateful but only on non-portable control properties”

## What this fixture does not prove

It does not yet prove:

- full `COBJ/OBJD` swatch closure for this model root
- exact semantic meaning of `0x00000000` versus `0xF4BD1CE9`
- exact relation between these states and in-game burned/dirty/other runtime object states
- that the currently surfaced portable deltas are the primary visible in-game difference rather than only one slice of a larger state packet

Safe reading:

- this is a texture-bearing portable-state boundary packet
- it is still not a full swatch/state-machine packet

## Current implementation boundary

Current repo behavior is useful here as boundary evidence only:

- preview recognized that one `MaterialSet`-sourced material is stateful and texture-bearing
- preview surfaced state deltas on portable shader properties rather than only on non-portable control properties
- preview still kept an inferred default state because exact object-state authority and state meaning are not yet closed
- this is implementation boundary, not TS4 truth about what every stateful object does

## Exact target claim for this packet

- the current workspace already has one concrete model-rooted `Build/Buy` fixture where `MTST` outranks the naive “single `MATD` plus texture fallback” reading with a texture-bearing `MaterialSet` material and portable state deltas

## Best next step after this packet

The next packet should not re-prove that `MTST` can change portable properties.

It should close one stronger follow-up:

1. a swatch-level fixture where `COBJ/OBJD` identity and `MaterialVariant` can be named directly, or
2. a runtime-state fixture where the state hashes can be tied to meaningful object-state behavior, or
3. a family where state deltas change clearly visible portable payload with stronger object identity than this model-rooted packet

## Honest limit

What this packet proves:

- one real `Build/Buy` model root in the current workspace is `MTST`-stateful beyond the earlier default-state floor
- that stronger statefulness survives on a `MaterialSet`-sourced material with `textures=2`
- the current seam now has both a default-state floor and a texture-bearing portable-state boundary

What remains open:

- exact swatch-level identity for this root
- exact meaning of the state hashes
- stronger stateful fixtures with named object identity and/or clearly interpreted runtime-state semantics
