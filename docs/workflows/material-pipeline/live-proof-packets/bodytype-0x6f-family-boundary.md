# BodyType 0x6F Family Boundary

This packet tightens the next mixed special-content high-byte `BodyType` family and freezes the strongest current creator-facing bridge for its head-decoration and tail-base-adjacent branches.

Question:

- does the current external-first evidence stack justify reading `0x6Fxxxxxx` as an auxiliary-area or secondary texture-space family aligned with `TailBase` vocabulary, rather than as one direct ordinary-slot enum?

Related docs:

- [Live-Proof Packets](README.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [BodyType Translation Boundary](../bodytype-translation-boundary.md)
- [CompositionMethod And SortLayer Boundary](../compositionmethod-sortlayer-boundary.md)
- [Overlay And Detail Family Authority Table](../overlay-detail-family-authority-table.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
BodyType 0x6F Family Boundary
├─ Externally proved split between BodyType and texture-space layer ~ 92%
├─ External family-specific corroboration for head-decoration / tail-base branches ~ 82%
├─ Local external snapshot packet ~ 90%
├─ Direct shard-backed family snapshot ~ 95%
└─ Exact 0x6F encoding rule ~ 47%
```

## Externally proved packet

What is already strong enough:

- [CASPartResource | Sims 4 Files Wiki](https://sims-4-files.fandom.com/wiki/CASPartResource) documents `AdditionalTextureSpace` separately from `BodyType`
- [CASP | Sims 4 Files Wiki](https://sims-4-files.fandom.com/wiki/CASP) preserves `UniqueTextureSpace` separately from `BodyType`
- [Dissecting the new CAS Categories -- "Head Decorations" is broken, "Body Paint" is "Tattoos", "Skin Effects" IS MINE! <CACKLES>](https://www.patreon.com/posts/are-new-cas-half-134140317) states that current creator-facing `Head Decorations` should be configured through `BodyType + AdditionalTextureSpce`, and specifically describes them as using the `Hat` texture area
- [Leafy Crown](https://www.patreon.com/posts/leafy-crown-134026188) gives a second creator-facing corroboration with `Head decoration section / Hat Texture area`
- [Butterflies Tail Accessory for Horses](https://www.thesimsresource.com/downloads/details/category/sims4/title/butterflies-tail-accessory-for-horses/id/1667488/) describes a real horse CAS item as `Tail Base Category`

Safe reading:

- modern creator-facing TS4 evidence now directly shows special CAS categories using secondary texture areas instead of behaving like ordinary clothing slots
- that makes a `TailBase`-aligned high-byte family plausible without flattening the whole family into literal horse-tail-only meaning
- the family can safely stay in the auxiliary-area track while keeping exact member semantics open

## Local external snapshot packet

Current strongest local external anchors:

- [TS4SimRipper CASP.cs](../../../references/external/TS4SimRipper/src/CASP.cs) preserves a separate `textureSpace` byte plus later `bodyType` field split
- [TS4SimRipper Enums.cs](../../../references/external/TS4SimRipper/src/Enums.cs) maps `0x6F` to `TailBase`
- [Binary Templates CASPart_0x034aeecb.bt](../../../references/external/Binary-Templates/CASPart_0x034aeecb.bt) separately preserves `mbUniqueTextureSpace`, `mBodyType`, and `mNormalUVBodyType`

Why this matters:

- `0x6F` has a real external-tool vocabulary anchor instead of only a local prevalence pattern
- the creator-facing head-decoration and tail-base evidence makes that anchor more usable than it was for the broader apparel-heavy families

## Direct shard-backed family snapshot

Current local snapshot:

- [bodytype_highbyte_family_snapshot_2026-04-21_6d_6f.json](../../../tmp/bodytype_highbyte_family_snapshot_2026-04-21_6d_6f.json)

What that snapshot currently shows for `0x6F`:

- family total = `3888` rows across `48` packages
- dominant members:
  - `0x6F000000 = 2175`
  - `0x6F000005 = 837`
  - `0x6F00000A = 504`
  - `0x6F000001 = 369`
- strongest family pairs:
  - `0 | 0 = 1917`
  - `0 | 196608 = 639`
  - `255 | 0 = 318`
  - `4 | 2100 = 240`
  - `191 | 8192 = 153`
- current token profile is clearly special-content mixed:
  - `MakeupLipstick = 942`
  - `MakeupEyeshadow = 897`
  - `Acc = 468`
  - `MakeupEyeliner = 396`
  - `FacialHair = 393`
  - `HorseTack = 360`
  - `MakeupBlush = 300`
  - `Hair = 279`
  - `HeadDeco = 159`
- current strongest subrows separate into visible branches:
  - `0x6F000000` is makeup-heavy
  - `0x6F000005` adds explicit `HeadDeco` content, including `yuHeadDeco_EP19CircletLeaf_*`
  - `0x6F00000A` leans strongly toward horse accessories and tack
  - `0x6F000001` is facial-hair-heavy

Safe reading:

- `0x6F` is not random noise and not a normal apparel row
- the family already spans head-decoration-like, tail-base-adjacent horse-accessory, makeup, and facial-hair-adjacent branches
- this is much safer as a special auxiliary-area family namespace than as one flat ordinary slot

## Current safest boundary reading

The current safest synthesis is:

```text
0x6F high byte
    -> externally named auxiliary-area vocabulary (`TailBase`)
    -> mixed special-content family in live CAS data
    -> includes creator-facing head-decoration and horse-tail-adjacent branches
    -> family still cannot be reduced to one ordinary slot rule
```

What this packet proves:

- `0x6F` is restart-safe as its own family packet instead of one anonymous mixed bucket
- the external stack now directly corroborates that the family belongs in the auxiliary-area track rather than the ordinary-slot subset
- the family is structured enough to keep separate from both plain apparel decoding and overlay/detail priority arguments

What this packet does not prove:

- the exact bit-level rule for all `0x6F` members
- that every `0x6Fxxxxxx` row is literally only `TailBase`
- that the family's makeup, head-decoration, and horse-tack branches already share one fully decoded runtime rule

## Current implementation boundary

Current repo behavior must stay diagnostic only:

- if the repo renames the whole family from low byte `0x00`, `0x01`, `0x05`, or `0x0A`, that is an implementation shortcut
- if the repo collapses head-decoration or horse-tail-adjacent rows into ordinary apparel slots, that also exceeds the current evidence

## Best next inspection step

1. Keep [BodyType 0x52 Family Boundary](bodytype-0x52-family-boundary.md) and [BodyType 0x80 Family Boundary](bodytype-0x80-family-boundary.md) as the closed continuation pair after `0x6D` and `0x6F`.
2. Revisit overlay/detail priority now that the current queued high-byte families are all separated more honestly.
