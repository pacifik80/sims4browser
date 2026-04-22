# BodyType 0x52 Family Boundary

This packet tightens the next concentrated high-byte `BodyType` family and freezes the strongest current conflict between an externally named scar-area vocabulary anchor and a heavily bottom-dominated live corpus family.

Question:

- does the current external-first evidence stack justify reading `0x52xxxxxx` as an auxiliary-area or secondary texture-space family aligned with `BodyScarArmLeft` vocabulary, rather than as one direct ordinary-slot enum?

Related docs:

- [Live-Proof Packets](README.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [BodyType Translation Boundary](../bodytype-translation-boundary.md)
- [CompositionMethod And SortLayer Boundary](../compositionmethod-sortlayer-boundary.md)
- [Overlay And Detail Family Authority Table](../overlay-detail-family-authority-table.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
BodyType 0x52 Family Boundary
├─ Externally proved split between BodyType and texture-space layer ~ 92%
├─ External family-specific corroboration for body-scar semantics ~ 74%
├─ Local external snapshot packet ~ 90%
├─ Direct shard-backed family snapshot ~ 95%
└─ Exact 0x52 encoding rule ~ 46%
```

## Externally proved packet

What is already strong enough:

- [CASPartResource | Sims 4 Files Wiki](https://sims-4-files.fandom.com/wiki/CASPartResource) documents `AdditionalTextureSpace` separately from `BodyType`
- [CASP | Sims 4 Files Wiki](https://sims-4-files.fandom.com/wiki/CASP) preserves `UniqueTextureSpace` separately from `BodyType`
- [Texture space](https://modthesims.info/showthread.php?t=630984) supports the practical creator reading that unique texture space means a part is using texture area normally used by another CAS part type
- current creator-facing CAS usage also preserves explicit body-scar semantics:
  - [Scars N2](https://www.patreon.com/posts/scars-n2-125282171) lists a dedicated `body scar right arm` category variant
  - [Astarion's Infernal Scars](https://modthesims.info/d/679664/astarion-s-infernal-scars-bgc-skin-detail.html) uses the `Body Scars` category for torso scars
  - [Plastic Surgery Scar Details](https://www.patreon.com/posts/plastic-surgery-115622375) explicitly includes arm-lift scars in the `Body Scar` category

Safe reading:

- the external stack still supports a two-field interpretation:
  - primary `BodyType`
  - secondary texture-space or auxiliary-area selector
- creator-facing body-scar semantics are real enough that `BodyScarArmLeft` is a usable vocabulary anchor
- that still does not justify renaming the whole `0x52` family as literal left-arm-scar content

## Local external snapshot packet

Current strongest local external anchors:

- [TS4SimRipper CASP.cs](../../../references/external/TS4SimRipper/src/CASP.cs) preserves a separate `textureSpace` byte before the later `bodyType` field
- [TS4SimRipper Enums.cs](../../../references/external/TS4SimRipper/src/Enums.cs) maps `0x52` to `BodyScarArmLeft`
- [Binary Templates CASPart_0x034aeecb.bt](../../../references/external/Binary-Templates/CASPart_0x034aeecb.bt) separately preserves `mbUniqueTextureSpace`, `mBodyType`, and `mNormalUVBodyType`

Why this matters:

- `0x52` has a direct external-tool vocabulary anchor instead of being only a local bucket
- that anchor sharply conflicts with the live family shape, which is exactly why this packet matters

## Direct shard-backed family snapshot

Current local snapshot:

- [bodytype_highbyte_family_snapshot_2026-04-21_52_80.json](../../../tmp/bodytype_highbyte_family_snapshot_2026-04-21_52_80.json)

What that snapshot currently shows for `0x52`:

- family total = `3774` rows across `85` packages
- dominant members:
  - `0x52000000 = 2412`
  - `0x52000006 = 948`
  - `0x5200000B = 414`
- strongest family pairs:
  - `0 | 0 = 2320`
  - `255 | 0 = 405`
  - `0 | 1792 = 186`
  - `0 | 19200 = 123`
  - `32 | 65536 = 93`
- current token profile is overwhelmingly bottom-heavy:
  - `Bottom = 3711`
  - `Body = 63`
- member structure stays concentrated rather than random:
  - `0x52000000` is entirely `Bottom`
  - `0x52000006` is almost entirely `Bottom`
  - `0x5200000B` is still mostly `Bottom`, but also freezes one clothing-like `composition=32 | sort=65536` sub-lane

Safe reading:

- `0x52` is much more semantically concentrated than `0x44` or `0x41`
- it still cannot be literally renamed from the external high-byte name, because the live family is overwhelmingly bottom-dominated
- this is safer as a secondary-area or auxiliary-family namespace riding on bottom carriers than as one direct ordinary slot or one literal scar-only slot

## Current safest boundary reading

The current safest synthesis is:

```text
0x52 high byte
    -> externally named auxiliary-area vocabulary (`BodyScarArmLeft`)
    -> strongly bottom-heavy mixed family in live CAS data
    -> includes one real clothing-like compositor sub-lane
    -> cannot be collapsed into literal arm-scar or low-byte slot naming
```

What this packet proves:

- `0x52` belongs in the same high-byte translation track as `0x44`, `0x41`, `0x6D`, and `0x6F`
- the external vocabulary overlap is real, but not strong enough to override the corpus shape
- the family is concentrated enough to close as a restart-safe packet without pretending the exact encoding is solved

What this packet does not prove:

- the exact bit-level rule for all `0x52` members
- that `0x52000000`, `0x52000006`, or `0x5200000B` are ordinary bottom slots
- that `BodyScarArmLeft` is the literal runtime meaning of every `0x52xxxxxx` row

## Current implementation boundary

Current repo behavior must stay diagnostic only:

- if the repo renames `0x52` as plain `Bottom`, that is an implementation shortcut
- if the repo renames the whole family as literal arm-scar content from the high byte alone, that also exceeds the evidence

## Best next inspection step

1. Keep [BodyType 0x80 Family Boundary](bodytype-0x80-family-boundary.md) as the last queued family packet in the current restart-safe stack.
2. Revisit overlay/detail priority now that `0x80` is also packetized and the current queue is closed through that weaker family.
