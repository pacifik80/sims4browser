# BodyType 0x41 Family Boundary

This packet tightens the second-largest mixed apparel-heavy high-byte `BodyType` family and freezes the first restart-safe reading for its clothing-like compositor sub-lane.

Question:

- does the current external-first evidence stack justify reading `0x41xxxxxx` as a secondary texture-space or auxiliary-area family aligned with `OccultEyeSocket` vocabulary, rather than as one direct ordinary-slot enum?

Related docs:

- [Live-Proof Packets](README.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [BodyType Translation Boundary](../bodytype-translation-boundary.md)
- [CompositionMethod And SortLayer Boundary](../compositionmethod-sortlayer-boundary.md)
- [Overlay And Detail Family Authority Table](../overlay-detail-family-authority-table.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
BodyType 0x41 Family Boundary
├─ Externally proved split between BodyType and texture-space layer ~ 92%
├─ External vocabulary overlap for 0x41 ~ 83%
├─ Local external snapshot packet ~ 88%
├─ Direct shard-backed family snapshot ~ 94%
└─ Exact 0x41 encoding rule ~ 49%
```

## Externally proved packet

What is already strong enough:

- [CASPartResource | Sims 4 Files Wiki](https://sims-4-files.fandom.com/wiki/CASPartResource) documents separate `AdditionalTextureSpace` and `BodyType` fields, and the `AdditionalTextureSpace` vocabulary includes `OccultEyeSocket`
- [CASP | Sims 4 Files Wiki](https://sims-4-files.fandom.com/wiki/CASP) shows `UniqueTextureSpace` and `BodyType` stored separately in one record
- [Texture space](https://modthesims.info/showthread.php?t=630984) explicitly describes unique texture space as using texture area normally used by another CAS part type
- [Dissecting the new CAS Categories -- "Head Decorations" is broken, "Body Paint" is "Tattoos", "Skin Effects" IS MINE! <CACKLES>](https://www.patreon.com/posts/are-new-cas-half-134140317) shows current creator-facing practice using `BodyType + AdditionalTextureSpce` together as one outfit-type reading

Safe reading:

- `0x41` is safely inside the same two-field interpretation space as `0x44`
- the high byte overlaps with `OccultEyeSocket` vocabulary, but that overlap is still a family anchor rather than a full literal rename of every member

## Local external snapshot packet

Current strongest local external anchors:

- [TS4SimRipper CASP.cs](../../../references/external/TS4SimRipper/src/CASP.cs) preserves the same `textureSpace` byte plus later `bodyType` field split
- [TS4SimRipper Enums.cs](../../../references/external/TS4SimRipper/src/Enums.cs) maps `0x41` to `OccultEyeSocket`
- [Binary Templates CASPart_0x034aeecb.bt](../../../references/external/Binary-Templates/CASPart_0x034aeecb.bt) separately preserves `mbUniqueTextureSpace`, `mBodyType`, and `mNormalUVBodyType`

Why this matters:

- `0x41` is not only a local corpus cluster; it already sits on top of real external vocabulary and tooling structure
- the shared structural evidence with `0x44` makes a family-by-family high-byte reading safer than a value-by-value low-byte decode

## Direct shard-backed family snapshot

Current local snapshot:

- [bodytype_highbyte_family_snapshot_2026-04-21.json](../../../tmp/bodytype_highbyte_family_snapshot_2026-04-21.json)

What that snapshot currently shows for `0x41`:

- family total = `19509` rows across `159` packages
- dominant members:
  - `0x41000000 = 12292`
  - `0x41000006 = 4752`
  - `0x4100000B = 2118`
  - `0x41000001 = 308`
- strongest family pairs:
  - `0 | 0 = 9016`
  - `255 | 0 = 2325`
  - `0 | 15616 = 822`
  - `32 | 65536 = 336`
- current token profile is still mixed:
  - `Body = 6940`
  - `Top = 6071`
  - `Shoes = 2226`
  - `Hat = 1714`
- sample names mix school-uniform bodies, snow-bunny hats, infant gel shoes, and a small whisker/facial-hair tail

Important narrower result:

- `0x4100000B` is the first family member that carries a stronger clothing-like compositor branch:
  - `32 | 65536 = 333`
- that is strong enough to freeze one safe rule:
  - a real overlay/detail-style compositor sub-lane exists inside `0x41`
  - it still does not collapse the whole family into one readable ordinary slot

## Current safest boundary reading

The current safest synthesis is:

```text
0x41 high byte
    -> externally named auxiliary-area / texture-space vocabulary (`OccultEyeSocket`)
    -> mixed apparel-heavy family in live CAS data
    -> one real clothing-like compositor sub-lane appears at `0x4100000B`
    -> family still cannot be renamed from low byte alone
```

What this packet proves:

- `0x41` belongs in the same high-byte translation track as `0x44`, not in the direct low-value enum subset
- the family is structurally mixed but not random
- `0x4100000B` gives the first clean restart-safe clothing-like compositor signal inside that broader family

What this packet does not prove:

- that `0x41` is exclusively occult-eye content
- that low bytes `00`, `06`, or `0B` can already be translated directly into ordinary slot names
- that `composition=32 | sort=65536` closes the exact runtime compositor semantics for the whole family

## Current implementation boundary

Current repo behavior must stay diagnostic only:

- if the repo promotes low-byte guesses like `0x0B` into direct slot truth for the whole `0x41` family, that is an implementation shortcut
- if the repo ignores the family-level compositor sub-lane entirely, that is also a useful boundary, not a semantic result

## Best next inspection step

1. Move to `0x6D`, then `0x6F`, because they look more semantically concentrated than the remaining broad apparel families.
2. Revisit overlay/detail priority only after those families are split out more honestly from the readable low-value slot subset.
