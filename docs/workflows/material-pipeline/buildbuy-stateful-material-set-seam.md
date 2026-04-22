# Build/Buy Stateful Material-Set Seam

This document narrows the `Build/Buy` authority seam around `MTST`, `MaterialVariant`, swatch selection, and object-side material-set linkage.

Use it when the question is not the base object/material chain itself, but the narrower problem:

- when should `MTST` stay in the authority path
- what does `MaterialVariant` safely mean in `OBJD`
- how should swatch/state selection be described without flattening everything into one `MATD`

Related docs:

- [Build/Buy Material Authority Matrix](buildbuy-material-authority-matrix.md)
- [Build/Buy MTST Default-State Boundary](live-proof-packets/buildbuy-mtst-default-state-boundary.md)
- [Build/Buy MTST Portable-State Delta](live-proof-packets/buildbuy-mtst-portable-state-delta.md)
- [Build/Buy MTST State-Selector Structure](live-proof-packets/buildbuy-mtst-state-selector-structure.md)
- [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md)
- [Material pipeline deep dives](README.md)
- [Edge-Family Matrix](edge-family-matrix.md)
- [Live-Proof Packets](live-proof-packets/README.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)
- [Open questions](../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Scope status (`v0.1`)

```text
Build/Buy Stateful Material-Set Seam
├─ External material-set identity ~ 86%
├─ Swatch/variant linkage reading ~ 84%
├─ MATD-versus-MTST boundary ~ 88%
├─ Fixture-backed family closure ~ 52%
└─ Restart-safe authority wording ~ 90%
```

## Question

- what is the safest current authority reading for `MTST`, `MaterialVariant`, and swatch/stateful Build/Buy objects?

## What is already externally strong enough

- [The Sims 4 Modders Reference: File Types](https://thesims4moddersreference.org/reference/file-types/) describes `Material Set` as sets of `Material Definitions`
- [Sims_4:0x01D10F34](https://modthesims.info/wiki.php?title=Sims_4%3A0x01D10F34) states that `MLOD` maps each mesh group to `VRTF`, `VBUF`, `IBUF`, `SKIN`, and `MATD` or `MTST`
- [Sims_4:RCOL](https://modthesims.info/wiki.php?title=Sims_4%3ARCOL) keeps `MODL`, `MLOD`, `MATD`, and `MTST` in one object-side scenegraph packet
- [Info | COBJ/OBJD resources](https://modthesims.info/t/551120) already narrows the swatch seam to same-instance `COBJ/OBJD`, with `COBJ` catalogue-facing and `OBJD` linkage-facing
- the [EA material-variant thread](https://forums.ea.com/discussions/the-sims-4-mods-and-custom-content-en/simgurumodsquad-looking-for-cross-reference-between-catalogobject-or-object-defi/1213694/replies/1213695) explicitly ties `MaterialVariant` strings in `OBJD` to hashed entries in the `MTST` block of the referenced model resource
- community swatch-override guidance in [How to add more color swatches to objects?](https://modthesims.info/t/545796) keeps the same reading:
  - swatch identity lives at the `COBJ/OBJD` seam
  - `MaterialVariant` points into Type300/material-entry selection
  - one new swatch may require one new material entry and one new `MATD` per relevant object state

## Current safest authority reading

```text
COBJ/OBJD swatch instance
          ->
OBJD model root + MaterialVariant selector
          ->
MODL/MLOD material-entry list
          ->
MTST when the object family is set/state/variant-driven
          ->
MATD as the per-material definition layer inside that set
          ->
shared shader/material contract
```

Safe rule:

- `MTST` is not an optional afterthought when the object family is swatch-heavy, stateful, or variant-driven
- `MaterialVariant` is best read as a selector into the object-side material-entry chain, not as a shortcut to loose package-local textures
- `MATD` remains the narrower material-definition unit, but `MTST` is the stronger authority carrier whenever the object family actually uses material sets or state variants

## What this seam does and does not prove

What it proves safely:

- `Build/Buy` swatch/state selection is already narrow enough to preserve as a separate authority seam
- the object-side material path should not be simplified to `OBJD -> one MATD -> textures`
- `MTST` belongs in the current Build/Buy authority model even before exact family-specific runtime state behavior is fully fixture-backed

What it does not prove yet:

- exact ranking between `MTST`, burned/dirty/other runtime state packets, and final in-game state resolution
- exact family-specific state behavior for every object ecosystem
- that every object with multiple swatches necessarily uses the same `MTST` pattern

## Why this matters architecturally

Without this seam, the docs drift into two weak readings:

1. `MATD` is always the only material authority that matters.
2. swatches/states are just texture swaps outside the object-side material model.

Both are weaker than the current external evidence.

Current safe architectural rule:

- preserve `MTST` and `MaterialVariant` as object-side authority vocabulary
- keep them on the discovery/authority side of the pipeline
- once a specific material entry is chosen, the resulting shader/material semantics still converge into the same shared decoder and canonical material contract used everywhere else

## Current fixture boundary

The docs do not yet have one strong live fixture that closes this seam end to end.

Current honest state:

- the named lily-pad `RefractionMap` fixture proves the broader `OBJD -> Model -> MLOD -> MATD/MTST` object/material seam, but not a closed `MTST`-driven stateful-object packet
- the named lily-pad seam now also has an explicit recording boundary:
  - [Refraction Companion MATD-vs-MTST Boundary](live-proof-packets/refraction-companion-matd-vs-mtst-boundary.md)
  - safe reading: `MTST` is an allowed inspection result there, not a default assumption
- current `Build/Buy` edge-family fixtures are useful as object/material anchors, not yet as explicit stateful-material-set fixtures
- the first narrower closure now exists:
  - [Build/Buy MTST Default-State Boundary](live-proof-packets/buildbuy-mtst-default-state-boundary.md) proves one model-rooted fixture where `MTST` clearly carries multiple states and preview keeps an inferred default state
- the stronger second closure now exists too:
  - [Build/Buy MTST Portable-State Delta](live-proof-packets/buildbuy-mtst-portable-state-delta.md) proves one model-rooted texture-bearing fixture where a `MaterialSet`-sourced material carries `textures=2` and `MTST` state deltas on portable shader properties
- the third closure now exists too:
  - [Build/Buy MTST State-Selector Structure](live-proof-packets/buildbuy-mtst-state-selector-structure.md) proves that repeated `stateHash -> MATD` mappings are structurally stable across the current fixtures and that `002211...` carries a repeated paired `unknown0=0x00000000` versus `0xC3867C32` split
- the remaining stronger closure should now come from a swatch-heavy or stateful object family where `MaterialVariant` and `MTST` can be tracked more directly through named object identity

## Current implementation boundary

Current repo behavior is useful here only as a boundary:

- it can show whether object-side linkage survives far enough to decode materials
- it can show whether current preview flattens state/variant-rich objects into one broad material bag
- it cannot define the true TS4 state/variant contract on its own

## Best next fixture target

The next packet that should grow from this seam is not another wording pass.

It should be one concrete fixture from a family such as:

- swatch-heavy decor or furniture with visible material-set changes
- burned/dirty/stateful object families where one swatch or state maps to multiple `MATD` packets
- object ecosystems where creator swatch work already implies `Type300` / material-entry manipulation

Safe next claim to prove:

- one real Build/Buy family where `MaterialVariant` and `MTST` clearly outrank the naive “single `MATD` plus texture fallback” reading

Current fixture-backed boundary:

- [Build/Buy MTST Default-State Boundary](live-proof-packets/buildbuy-mtst-default-state-boundary.md)
- [Build/Buy MTST Portable-State Delta](live-proof-packets/buildbuy-mtst-portable-state-delta.md)

## Honest limit

This seam is now narrow enough to guide future packets, but it is not a full state-machine spec.

What is now strong:

- `MTST` is a real object-side authority seam
- `MaterialVariant` is safely part of swatch/state selection, not generic texture discovery
- Build/Buy docs no longer need to restate this as an ad hoc side note
- the seam now has both a default-state floor and a stronger texture-bearing portable-state boundary
- the seam now also has a structural selector packet, so repeated `stateHash` behavior no longer needs to be rediscovered from raw probe output

What remains open:

- exact swatch-level or `MaterialVariant`-named closure for concrete stateful families
- exact semantic meaning of the repeated selector hashes and of `unknown0=0xC3867C32`
- exact interplay with runtime state packets
- wider family-specific coverage beyond the current external reading
