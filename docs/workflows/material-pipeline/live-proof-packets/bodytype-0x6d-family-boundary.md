# BodyType 0x6D Family Boundary

This packet tightens the most cosmetic-heavy mixed high-byte `BodyType` family and freezes the strongest current counterexample to naive low-byte decoding.

Question:

- does the current external-first evidence stack justify reading `0x6Dxxxxxx` as an auxiliary-area or secondary texture-space family aligned with `HairFeathers` vocabulary, rather than as one direct ordinary-slot enum?

Related docs:

- [Live-Proof Packets](README.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [BodyType Translation Boundary](../bodytype-translation-boundary.md)
- [CompositionMethod And SortLayer Boundary](../compositionmethod-sortlayer-boundary.md)
- [Overlay And Detail Family Authority Table](../overlay-detail-family-authority-table.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
BodyType 0x6D Family Boundary
├─ Externally proved split between BodyType and texture-space layer ~ 92%
├─ External family-specific corroboration for special auxiliary areas ~ 68%
├─ Local external snapshot packet ~ 89%
├─ Direct shard-backed family snapshot ~ 95%
└─ Exact 0x6D encoding rule ~ 44%
```

## Externally proved packet

What is already strong enough:

- [CASPartResource | Sims 4 Files Wiki](https://sims-4-files.fandom.com/wiki/CASPartResource) documents `AdditionalTextureSpace` separately from `BodyType`
- [CASP | Sims 4 Files Wiki](https://sims-4-files.fandom.com/wiki/CASP) preserves `UniqueTextureSpace` separately from `BodyType` inside the same CAS part record
- [Texture space](https://modthesims.info/showthread.php?t=630984) records the practical creator reading that unique texture space means a part uses texture area normally used by another CAS part type
- [Dissecting the new CAS Categories -- "Head Decorations" is broken, "Body Paint" is "Tattoos", "Skin Effects" IS MINE! <CACKLES>](https://www.patreon.com/posts/are-new-cas-half-134140317) shows current creator-facing practice still reading outfit type as `BodyType + AdditionalTextureSpce`, not as one flat slot id

Safe reading:

- the external stack still supports one two-field interpretation space:
  - primary `BodyType`
  - secondary texture-space or auxiliary-area selector
- that is already strong enough to block any attempt to rename `0x6D` from the low byte alone
- it is not yet strong enough to prove that the whole family is literally only `HairFeathers`

## Local external snapshot packet

Current strongest local external anchors:

- [TS4SimRipper CASP.cs](../../../references/external/TS4SimRipper/src/CASP.cs) preserves a separate `textureSpace` byte before the later `bodyType` field
- [TS4SimRipper Enums.cs](../../../references/external/TS4SimRipper/src/Enums.cs) maps `0x6D` to `HairFeathers`
- [Binary Templates CASPart_0x034aeecb.bt](../../../references/external/Binary-Templates/CASPart_0x034aeecb.bt) separately preserves `mbUniqueTextureSpace`, `mBodyType`, and `mNormalUVBodyType`

Why this matters:

- `0x6D` already sits on top of real external-tool vocabulary instead of being only a local corpus bucket
- the checked-in tooling stack keeps reinforcing that high-byte family identity may ride on a separate texture-space-style field

## Direct shard-backed family snapshot

Current local snapshot:

- [bodytype_highbyte_family_snapshot_2026-04-21_6d_6f.json](../../../tmp/bodytype_highbyte_family_snapshot_2026-04-21_6d_6f.json)

What that snapshot currently shows for `0x6D`:

- family total = `3333` rows across `72` packages
- dominant members:
  - `0x6D00000C = 1938`
  - `0x6D000005 = 750`
  - `0x6D00000A = 498`
- strongest family pairs:
  - `255 | 0 = 1548`
  - `0 | 0 = 1473`
  - then much smaller branches such as `0 | 1838 = 54` and `0 | 1020 = 36`
- current token profile is strongly cosmetic, but still mixed:
  - `SkinDetail = 2043`
  - `MakeupLipstick = 747`
  - `Body = 246`
  - `Bottom = 90`
  - `Acc = 75`
  - `Shoes = 72`
  - `HorseTack = 30`
- current strongest subrows are structurally different from one another:
  - `0x6D00000C` is overwhelmingly `SkinDetail`, especially scars and face marks
  - `0x6D000005` is overwhelmingly `MakeupLipstick`
  - `0x6D00000A` reopens a mixed apparel and horse-tack branch
  - smaller rows like `0x6D000006` and `0x6D000000` also surface freckles and `BirthmarkOccult` names

Safe reading:

- `0x6D` is the cleanest current proof that the low byte does not safely decode these high-byte families by itself
- the same family can carry scar-heavy `SkinDetail`, lipstick-heavy makeup, and mixed apparel/horse rows
- this is much safer as an auxiliary-area family namespace than as one hidden ordinary slot

## Current safest boundary reading

The current safest synthesis is:

```text
0x6D high byte
    -> externally named auxiliary-area vocabulary (`HairFeathers`)
    -> strongly cosmetic mixed family in live CAS data
    -> low byte clearly fails as a standalone ordinary-slot decoder
    -> family must stay unresolved above member-level prevalence notes
```

What this packet proves:

- `0x6D` belongs in the same high-byte translation track as `0x44` and `0x41`
- this family is the strongest current restart-safe counterexample to naive low-byte decoding
- the external structure still favors a secondary texture-space or auxiliary-area reading over a plain ordinary-slot reading

What this packet does not prove:

- the exact bit-level rule for all `0x6D` members
- that `HairFeathers` is the literal runtime meaning of every `0x6Dxxxxxx` row
- that the family's current compositor branches are already semantically decoded

## Current implementation boundary

Current repo behavior must stay diagnostic only:

- if the repo tries to rename `0x6D00000C` from low byte `0x0C`, that is an implementation shortcut
- if the repo flattens the whole family into one cosmetic slot, that also overstates the evidence

## Best next inspection step

1. Keep [BodyType 0x6F Family Boundary](bodytype-0x6f-family-boundary.md), [BodyType 0x52 Family Boundary](bodytype-0x52-family-boundary.md), and [BodyType 0x80 Family Boundary](bodytype-0x80-family-boundary.md) as the now-closed continuation stack for the current queued high-byte families.
2. Revisit overlay/detail priority against that frozen packet stack instead of reopening the family queue from scratch.
