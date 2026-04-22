# BodyType 0x80 Family Boundary

This packet tightens the last large high-byte `BodyType` family in the current queue and keeps it honestly weaker than the named-family packets because no direct external vocabulary anchor has survived yet.

Question:

- does the current external-first evidence stack justify any stronger reading for `0x80xxxxxx` than an unresolved sign-bit high-byte family that still participates in the same auxiliary-area track?

Related docs:

- [Live-Proof Packets](README.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [BodyType Translation Boundary](../bodytype-translation-boundary.md)
- [CompositionMethod And SortLayer Boundary](../compositionmethod-sortlayer-boundary.md)
- [Overlay And Detail Family Authority Table](../overlay-detail-family-authority-table.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
BodyType 0x80 Family Boundary
├─ Externally proved split between BodyType and texture-space layer ~ 92%
├─ External family-specific vocabulary anchor for 0x80 ~ 24%
├─ Local external snapshot packet ~ 71%
├─ Direct shard-backed family snapshot ~ 94%
└─ Exact 0x80 encoding rule ~ 29%
```

## Externally proved packet

What is already strong enough:

- [CASPartResource | Sims 4 Files Wiki](https://sims-4-files.fandom.com/wiki/CASPartResource) documents `AdditionalTextureSpace` separately from `BodyType`
- [CASP | Sims 4 Files Wiki](https://sims-4-files.fandom.com/wiki/CASP) preserves `UniqueTextureSpace` separately from `BodyType`
- [Texture space](https://modthesims.info/showthread.php?t=630984) supports the practical creator reading that texture-space-like routing can sit on top of ordinary CAS categories

What is notably missing:

- no current external creator-facing page or checked-in external enum snapshot gives a direct name for high byte `0x80`
- no current external source justifies flattening this family into one readable slot

Safe reading:

- `0x80` still belongs under the broader high-byte auxiliary-area problem space because the same two-field structural evidence applies
- unlike `0x52`, `0x6D`, or `0x6F`, this family does not currently have a direct external vocabulary anchor
- that missing anchor must stay explicit in the packet

## Local external snapshot packet

Current strongest local external anchors:

- [TS4SimRipper CASP.cs](../../../references/external/TS4SimRipper/src/CASP.cs) preserves a separate `textureSpace` byte before the later `bodyType` field
- [Binary Templates CASPart_0x034aeecb.bt](../../../references/external/Binary-Templates/CASPart_0x034aeecb.bt) separately preserves `mbUniqueTextureSpace`, `mBodyType`, and `mNormalUVBodyType`
- [TS4SimRipper Enums.cs](../../../references/external/TS4SimRipper/src/Enums.cs) currently provides no direct named `0x80` entry in the checked-in body-type range

Why this matters:

- the structural split still holds
- the naming layer does not
- that makes `0x80` a weaker packet by design, not by accident

## Direct shard-backed family snapshot

Current local snapshot:

- [bodytype_highbyte_family_snapshot_2026-04-21_52_80.json](../../../tmp/bodytype_highbyte_family_snapshot_2026-04-21_52_80.json)

What that snapshot currently shows for `0x80`:

- family total = `2181` rows across `27` packages
- dominant members:
  - `0x80000000 = 2175`
  - `0x8000000B = 6`
- strongest family pairs:
  - `0 | 0 = 2064`
  - `0 | 536870912 = 60`
  - `0 | 196608 = 51`
- current token profile is mixed:
  - `Body = 1518`
  - `Acc = 432`
  - `Hat = 210`
  - `Top = 36`
  - `Shoes = 12`
- current sample names mix snowboard bodies, skates, horse flower accessories, and flower crowns rather than one clean category

Bounded inference from the sample names:

- some `0x80` content appears adjacent to creator-visible hat and tail-base-style accessory areas
- that is still only an inference from local samples, not an externally confirmed family name

Safe reading:

- `0x80` is a real repeated family, not a one-off bad decode
- it is too mixed to rename from the low byte
- it is also too weakly anchored externally to receive a literal high-byte name yet

## Current safest boundary reading

The current safest synthesis is:

```text
0x80 high byte
    -> unresolved sign-bit family inside the broader auxiliary-area track
    -> mixed body / accessory / hat carrier packet in live CAS data
    -> no direct external vocabulary anchor yet
    -> must remain weaker and more explicitly unresolved than 0x44 / 0x41 / 0x6D / 0x6F / 0x52
```

What this packet proves:

- `0x80` belongs in the same family-by-family high-byte translation problem, not in the readable low-value subset
- the family is repeated and restart-safe enough to document
- the honest limit is now explicit: this packet is weaker because the naming layer is still missing

What this packet does not prove:

- any direct external meaning for high byte `0x80`
- the exact bit-level rule for the family
- that the sample hats, accessories, and body rows share one already-decoded runtime rule

## Current implementation boundary

Current repo behavior must stay diagnostic only:

- if the repo renames `0x80` from low byte `0x00` or `0x0B`, that is an implementation shortcut
- if the repo fabricates a high-byte name for `0x80` from sample-name clustering alone, that also exceeds the evidence

## Best next inspection step

1. Revisit overlay/detail priority now that the current restart-safe high-byte packet stack covers `0x44`, `0x41`, `0x6D`, `0x6F`, `0x52`, and `0x80`.
2. Only reopen `0x80` specifically if a new external tool snapshot or creator-facing category packet surfaces a direct vocabulary anchor.
