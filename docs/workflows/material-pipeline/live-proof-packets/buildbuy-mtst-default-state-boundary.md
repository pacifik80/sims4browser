# Build/Buy MTST Default-State Boundary

This packet turns the current `MTST` / stateful-material seam into one concrete fixture-backed boundary case.

Question:

- does the current workspace already prove one real `Build/Buy` model root where `MTST` carries multiple material states strongly enough that the authority path cannot be simplified to one `MATD` plus texture fallback?

Related docs:

- [Build/Buy Material Authority Matrix](../buildbuy-material-authority-matrix.md)
- [Build/Buy Stateful Material-Set Seam](../buildbuy-stateful-material-set-seam.md)
- [Material Pipeline Deep Dives](../README.md)
- [Live-Proof Packets](README.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
Build/Buy MTST Default-State Boundary
├─ External material-set authority ~ 85%
├─ Fixture-root identity ~ 62%
├─ Multi-state MTST proof ~ 81%
├─ Default-state boundary wording ~ 89%
└─ Full swatch/state closure ~ 24%
```

## Externally proved authority baseline

What is already strong enough:

- [The Sims 4 Modders Reference: File Types](https://thesims4moddersreference.org/reference/file-types/) describes `Material Set` as sets of `Material Definitions`
- [Sims_4:0x01D10F34](https://modthesims.info/wiki.php?title=Sims_4%3A0x01D10F34) keeps `MLOD` as the mesh-group layer that points to `MATD` or `MTST`
- [Sims_4:RCOL](https://modthesims.info/wiki.php?title=Sims_4%3ARCOL) keeps `MODL`, `MLOD`, `MATD`, and `MTST` together in one object-side scenegraph packet
- [Info | COBJ/OBJD resources](https://modthesims.info/t/551120) and the [EA material-variant thread](https://forums.ea.com/discussions/the-sims-4-mods-and-custom-content-en/simgurumodsquad-looking-for-cross-reference-between-catalogobject-or-object-defi/1213694/replies/1213695) keep swatch and variant selection inside the object-side model/material chain rather than as loose texture discovery

Safe reading:

- `MTST` is a real object-side material authority seam
- a fixture only needs to prove that real stateful behavior survives at the model/material layer
- it does not need to close full swatch/object-state identity to be useful

## Current local fixture

Current fixture root:

- package: `EP01\ClientDeltaBuild0.package`
- display: `Build/Buy Model 002211BA8D2EE539`
- root: `01661233:00000000:002211BA8D2EE539`
- source artifact: `tmp/probe_002211_after.txt`

What the probe already says:

- exact-instance `ObjectCatalog/ObjectDefinition` metadata was not found for this model
- the asset remains usable through its model-rooted identity
- `ModelLOD candidates = 5`
- repeated `MTST` chunks exist on the same model-root packet:
  - `02019972:00B53A5F:002211BA8D2EE539`
  - `02019972:00129C76:002211BA8D2EE539`

Safe reading:

- this is a real model-rooted `Build/Buy` fixture, not a closed swatch-level object fixture
- the lack of exact `OBJD/COBJ` identity is an honest limit, not a reason to discard the packet

## What the fixture proves

The current probe repeats the same high-signal preview note across the sampled LODs:

- `MTST exposes multiple material states`
- preview `evaluated the available entries and kept the inferred default state`
- all current state hashes received equal scores:
  - `0x00000000`
  - `0xBA5CC973`
  - `0xD7B49960`
  - `0xDE5CF5D6`
  - `0xF4BD1CE9`
- the current difference packet is narrower than a full texture/state split:
  - the variants currently change only non-portable control properties
  - repeated example from the probe: `Prop_AD983A7C` varies by state while texture coverage remains `0`

This is enough to prove:

- the preview is not seeing one flat material packet
- `MTST` is carrying multiple selectable states at the object-side material layer
- the current workspace already has one fixture where the safest boundary is “choose an inferred default state and preserve that this was a stateful `MTST` decision”

## What this fixture does not prove

It does not yet prove:

- full `COBJ/OBJD` swatch closure for this model root
- exact semantic meaning of the five state hashes
- exact relation between these states and in-game burned/dirty/other runtime object states
- texture-bearing differences between states, because the current probe still reports `Texture candidates: 0`

Safe reading:

- this is a default-state boundary packet
- it is not a full state-machine packet

## Current implementation boundary

Current repo behavior is useful here as boundary evidence only:

- preview recognized that the material root was stateful and did not collapse it to a single implicit `MATD`
- preview still had to keep an inferred default state because current state variants only surfaced as non-portable control-property differences
- this is implementation boundary, not TS4 truth about what every stateful object does

## Exact target claim for this packet

- the current workspace already has one concrete model-rooted `Build/Buy` fixture where `MTST` clearly outranks the naive “single `MATD` plus texture fallback” reading

## Best next step after this packet

The next packet should not re-prove that `MTST` exists.

It should close one stronger follow-up:

1. a swatch-level fixture where `COBJ/OBJD` identity and `MaterialVariant` can be named directly, or
2. a runtime-state fixture where the `MTST` state hashes can be tied to meaningful object-state behavior, or
3. a texture-bearing stateful fixture where the state change affects real material/texture payload rather than only non-portable control properties

## Honest limit

What this packet proves:

- one real `Build/Buy` model root in the current workspace is materially `MTST`-stateful
- current preview already treats it as a default-state selection problem, not as one flat material packet
- the `MTST` seam now has one fixture-backed boundary case instead of only abstract authority wording

What remains open:

- exact swatch-level identity for this root
- exact meaning of the state hashes
- stronger stateful fixtures with named object identity and/or texture-bearing state changes
