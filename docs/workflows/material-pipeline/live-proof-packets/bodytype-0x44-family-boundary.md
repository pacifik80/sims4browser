# BodyType 0x44 Family Boundary

This packet tightens the biggest unresolved high-byte `BodyType` family without pretending the mixed values are already decoded into ordinary CAS slots.

Question:

- does the current external-first evidence stack justify reading `0x44xxxxxx` as a secondary texture-space or auxiliary-area family aligned with `OccultLeftCheek` vocabulary, rather than as one direct primary-slot enum?

Related docs:

- [Live-Proof Packets](README.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [BodyType Translation Boundary](../bodytype-translation-boundary.md)
- [CAS/Sim Material Authority Matrix](../cas-sim-material-authority-matrix.md)
- [Overlay And Detail Family Authority Table](../overlay-detail-family-authority-table.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
BodyType 0x44 Family Boundary
├─ Externally proved split between BodyType and texture-space layer ~ 92%
├─ External vocabulary overlap for 0x44 ~ 83%
├─ Local external snapshot packet ~ 88%
├─ Direct shard-backed family snapshot ~ 94%
└─ Exact 0x44 encoding rule ~ 46%
```

## Externally proved packet

What is already strong enough:

- [CASPartResource | Sims 4 Files Wiki](https://sims-4-files.fandom.com/wiki/CASPartResource) documents `AdditionalTextureSpace` and `BodyType` as separate fields, and the `AdditionalTextureSpace` list includes `OccultLeftCheek`
- [CASP | Sims 4 Files Wiki](https://sims-4-files.fandom.com/wiki/CASP) shows `UniqueTextureSpace` and `BodyType` as separate stored values inside one CAS part record
- [Texture space](https://modthesims.info/showthread.php?t=630984) records CmarNYC's practical reading that unique texture space indicates a part is using texture area normally used by another CAS part type
- [Dissecting the new CAS Categories -- "Head Decorations" is broken, "Body Paint" is "Tattoos", "Skin Effects" IS MINE! <CACKLES>](https://www.patreon.com/posts/are-new-cas-half-134140317) explicitly uses the combined creator-facing reading `Outfit Type (BodyType + AdditionalTextureSpce)`

Safe reading:

- the high-byte layer is not a SQLite-only accident or one local decoder quirk
- TS4 creator-facing evidence already supports a two-field reading:
  - primary `BodyType`
  - secondary texture-space or auxiliary-area selector
- `0x44` can safely be treated as vocabulary that overlaps with `OccultLeftCheek`, not as proof that every `0x44xxxxxx` row is literally one cheek-only occult slot

Unsafe reading:

- do not rename `0x44000000` as plain `All`, `Top`, or `Bottom` from the low byte
- do not rename the whole family as literal `OccultLeftCheek` just because the high byte overlaps that external name

## Local external snapshot packet

Current strongest local external anchors:

- [TS4SimRipper CASP.cs](../../../references/external/TS4SimRipper/src/CASP.cs) reads one `textureSpace` byte before the later `bodyType` field
- [TS4SimRipper Enums.cs](../../../references/external/TS4SimRipper/src/Enums.cs) maps `0x44` to `OccultLeftCheek`
- [Binary Templates CASPart_0x034aeecb.bt](../../../references/external/Binary-Templates/CASPart_0x034aeecb.bt) names the same earlier byte `mbUniqueTextureSpace`, then stores `mBodyType` separately, and later preserves `mNormalUVBodyType` as another cross-space routing field

Why this matters:

- the checked-in external tooling agrees on the structural split between texture-space-style routing and `BodyType`
- the local external enum packet gives `0x44` a real vocabulary anchor without proving a full decode on its own

## Direct shard-backed family snapshot

Current local snapshot:

- [bodytype_highbyte_family_snapshot_2026-04-21.json](../../../tmp/bodytype_highbyte_family_snapshot_2026-04-21.json)

What that snapshot currently shows for `0x44`:

- family total = `116574` rows across `247` packages
- dominant members:
  - `0x44000000 = 113301`
  - `0x44000001 = 3273`
- strongest family pairs:
  - `0 | 0 = 67403`
  - `0 | 1536 = 13282`
  - `0 | 1792 = 8011`
  - `0 | 16000 = 6129`
  - `0 | 1280 = 5915`
- current token profile is still broadly apparel-heavy:
  - `Top = 36884`
  - `Bottom = 24155`
  - `Body = 22711`
  - `Shoes = 6962`
  - `Hat = 4430`
  - `Hair = 2079`
- current sample names still mix body, top, bottom, and hair-style rows instead of one clean occult-cheek-only packet

Safe reading:

- `0x44` is the main unresolved mixed family above the readable low-value slots
- the occult-face-area overlap is real vocabulary, but the local corpus packet is still much broader than one cheek-only slot
- this is much safer as a secondary-area family riding on top of many ordinary apparel carriers than as one hidden replacement for `Top` or `Bottom`

## Current safest boundary reading

The current safest synthesis is:

```text
0x44 high byte
    -> externally named auxiliary-area / texture-space vocabulary (`OccultLeftCheek`)
    -> mixed family namespace in live CAS data
    -> low byte may still preserve older slot relations in some members
    -> family must stay unresolved until more exact encoding proof exists
```

What this packet proves:

- `0x44` should stay above naive low-byte decoding
- the family is now strong enough to treat as its own restart-safe packet instead of one anonymous mixed bucket
- the current evidence favors a secondary texture-space or auxiliary-area reading over a plain hidden-slot reading

What this packet does not prove:

- the exact bit-level encoding rule for every `0x44` member
- that `0x44000000` is exclusively occult-face content
- that the compositor lanes inside the family are already semantically decoded

## Current implementation boundary

Current repo behavior must stay diagnostic only:

- if the repo treats `0x44000000` as if the low byte alone defines the slot, that is an implementation shortcut
- if the repo flattens this family into ordinary shell or overlay slot identity, that is a boundary to fix later, not TS4 truth

## Best next inspection step

1. Carry the same packet shape into `0x41`, because it is the next largest mixed apparel-heavy family.
2. After `0x41`, move to the more semantically concentrated `0x6D` and `0x6F` families before revisiting overlay/detail priority.
