# BodyType Translation Boundary

This document records the current safest reading for the largest `CASPart.body_type` values after `composition_method` became queryable in the shard cache.

Related docs:

- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [CompositionMethod Census Baseline](compositionmethod-census-baseline.md)
- [CompositionMethod And SortLayer Boundary](compositionmethod-sortlayer-boundary.md)
- [Overlay And Detail Family Authority Table](overlay-detail-family-authority-table.md)
- [P1 Live-Proof Queue](p1-live-proof-queue.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
BodyType Translation Boundary
├─ Low-value enum matches ~ 92%
├─ Large mixed-value bucket isolation ~ 88%
├─ High-byte family isolation ~ 91%
├─ CompositionMethod / SortLayer shape per bucket ~ 86%
└─ Exact semantic meaning of high-bit values ~ 46%
```

## What this document is for

- separate clearly named `BodyType` values from large mixed-value buckets
- stop over-reading the readable slot subset as if it already explained all `CASPart` compositor data
- fix the next priority after the `composition_method` cache backfill: large unresolved `BodyType` values, not more cache plumbing

## Current cache floor

The shard cache now has a populated `composition_method` column in all four databases:

- `tmp/profile-index-cache/cache/index.sqlite`
- `tmp/profile-index-cache/cache/index.shard01.sqlite`
- `tmp/profile-index-cache/cache/index.shard02.sqlite`
- `tmp/profile-index-cache/cache/index.shard03.sqlite`

Verification artifacts:

- [compositionmethod_cache_backfill.json](../../../tmp/compositionmethod_cache_backfill.json)
- [compositionmethod_cache_backfill.progress.json](../../../tmp/compositionmethod_cache_backfill.progress.json)
- [bodytype_highbyte_family_snapshot_2026-04-21.json](../../../tmp/bodytype_highbyte_family_snapshot_2026-04-21.json)

Verified current state:

- all four shard databases now contain `cas_part_facts.composition_method`
- all four shard databases now report `NULL = 0` for that column
- all four shard databases now report `seed_fact_content_version = 2026-04-21.seed-facts-v2`

Safe reading:

- later `CompositionMethod` and `SortLayer` analysis can now use ordinary SQLite queries
- the next real blocker is interpretation of the remaining biggest `BodyType` buckets, not cache population
- the first two family-specific continuation packets now exist:
  - [BodyType 0x44 Family Boundary](live-proof-packets/bodytype-0x44-family-boundary.md)
  - [BodyType 0x41 Family Boundary](live-proof-packets/bodytype-0x41-family-boundary.md)
- the next two semantically concentrated family packets now also exist:
  - [BodyType 0x6D Family Boundary](live-proof-packets/bodytype-0x6d-family-boundary.md)
  - [BodyType 0x6F Family Boundary](live-proof-packets/bodytype-0x6f-family-boundary.md)
- the current restart-safe stack now also closes the remaining queued families:
  - [BodyType 0x52 Family Boundary](live-proof-packets/bodytype-0x52-family-boundary.md)
  - [BodyType 0x80 Family Boundary](live-proof-packets/bodytype-0x80-family-boundary.md)

## New external lead: separate texture-space layer

The current external source stack is now strong enough for one cautious new hypothesis.

What the external sources say:

- local `TS4SimRipper` reads a separate `textureSpace` byte in `CASP`, then a separate `bodyType` field
- local `Binary-Templates` call the same byte `mbUniqueTextureSpace`
- the community `CASPartResource` page documents a separate `AdditionalTextureSpace` field with the same vocabulary as `BodyType`
- CmarNYC described `Unique Texture Space` as a way to indicate that a CAS part uses a texture area normally used by another CAS part type
- a recent creator write-up describes “Outfit Type” as `BodyType + AdditionalTextureSpce`

Current safe reading:

- the large packed-looking values may be tied to a second texture-space layer rather than to one flat primary slot enum
- this is now externally supported as a serious candidate explanation
- it is not yet proved as the exact encoding rule for every value in the current corpus
- the new `0x44` and `0x41` packets now make that hypothesis concrete enough to use as the restart-safe family reading for those two rows

Current external anchors:

- [TS4SimRipper CASP.cs](../../references/external/TS4SimRipper/src/CASP.cs)
- [Binary Templates CASPart_0x034aeecb.bt](../../references/external/Binary-Templates/CASPart_0x034aeecb.bt)
- [CASPartResource | Sims 4 Files Wiki](https://sims-4-files.fandom.com/wiki/CASPartResource)
- [Texture space thread on Mod The Sims](https://modthesims.info/showthread.php?t=630984)
- [Sejian on new CAS categories](https://www.patreon.com/posts/are-new-cas-half-134140317)

Implementation boundary to keep explicit:

- current repo parsing also reads one standalone byte before `bodyType`
- current repo indexing does not synthesize the large values by combining that byte with a later slot id
- so the packed-looking `bodyType` integers are not a late SQLite-only artifact created by this repo

## Probable packed structure

The biggest unresolved values are not random decimals. They already show a repeated packed shape:

| Decimal value | Hex value | High byte | Low byte |
| --- | --- | --- | --- |
| `1140850688` | `0x44000000` | `0x44` | `0x00` |
| `1090519040` | `0x41000000` | `0x41` | `0x00` |
| `1090519046` | `0x41000006` | `0x41` | `0x06` |
| `1140850689` | `0x44000001` | `0x44` | `0x01` |
| `1610612736` | `0x60000000` | `0x60` | `0x00` |
| `1375731712` | `0x52000000` | `0x52` | `0x00` |
| `1862270976` | `0x6F000000` | `0x6F` | `0x00` |
| `-2147483648` | `0x80000000` | `0x80` | `0x00` |

What this proves:

- the unresolved layer is not just “unknown large numbers”
- there are repeated high-byte families
- some rows also carry a meaningful low byte, for example:
  - `0x41000006`
  - `0x44000001`
- several high bytes now also line up with names from the external `BodyType` / `AdditionalTextureSpace` vocabulary

What this does not yet prove:

- that the low byte is always a clean ordinary `BodyType`
- that the high byte has one simple documented meaning
- that these packed values can already be flattened back into the public enum

## High-byte names from the external enum vocabulary

Several high bytes already align with names from the external `BodyType` vocabulary in `TS4SimRipper`:

| High byte | External name |
| --- | --- |
| `0x41` | `OccultEyeSocket` |
| `0x44` | `OccultLeftCheek` |
| `0x52` | `BodyScarArmLeft` |
| `0x60` | `MoleChestUpper` |
| `0x65` | `Saddle` |
| `0x66` | `Bridle` |
| `0x67` | `Reins` |
| `0x68` | `Blanket` |
| `0x69` | `SkinDetailHoofColor` |
| `0x6A` | `HairMane` |
| `0x6B` | `HairTail` |
| `0x6C` | `HairForelock` |
| `0x6D` | `HairFeathers` |
| `0x6E` | `Horn` |
| `0x6F` | `TailBase` |
| `0x70` | `BirthmarkOccult` |
| `0x71` | `TattooHead` |
| `0x72` | `Wings` |
| `0x73` | `HeadDeco` |

Safe reading:

- high bytes are no longer just anonymous family ids
- they already overlap strongly with the external secondary-body-area vocabulary
- this makes a texture-space or auxiliary-area interpretation more plausible than a plain hidden-slot interpretation

## Current high-byte families

Across all unresolved values above `255`, the strongest high-byte families are:

| High byte | Current total rows | Strongest current values |
| --- | ---: | --- |
| `0x44` | `116574` | `0x44000000`, `0x44000001` |
| `0x41` | `19509` | `0x41000000`, `0x41000006`, `0x4100000B` |
| `0x6F` | `3888` | `0x6F000000`, `0x6F000005`, `0x6F00000A`, `0x6F000001` |
| `0x52` | `3774` | `0x52000000`, `0x52000006`, `0x5200000B` |
| `0x6D` | `3333` | `0x6D00000C`, `0x6D000005`, `0x6D00000A` |
| `0x60` | `3102` | `0x60000000`, `0x6000000B`, `0x60000006` |
| `0x80` | `2181` | `0x80000000`, `0x8000000B` |

Safe reading:

- the next translation step should work family-by-family by high byte
- trying to solve each decimal value in isolation would hide the repeated structure

## Why the low byte is not enough

The low byte sometimes looks familiar:

- `0x41000006` ends in `0x06`
- `0x44000001` ends in `0x01`

But that shortcut breaks quickly.

Examples:

- `0x6D00000C` ends in `0x0C`, but current names are mostly `SkinDetail` and scar-like rows, not simple `Necklace`
- `0x6D000005` ends in `0x05`, but current names are mostly `Lipstick`
- `0x6F000001` ends in `0x01`, but current names are mostly `FacialHair`
- `0x8000000B` ends in `0x0B`, but the tiny sample currently mixes `Nude Top` and `Nude Shoes`

Safe reading:

- low byte may still preserve some older slot relation
- it is not strong enough to rename these rows by itself
- high-byte family context matters more than low-byte guesswork

## Externally named low-value `BodyType` rows

The local `TS4SimRipper` `BodyType` enum gives direct names for the low-value rows:

- `10 = Earrings`
- `28 = FacialHair`
- `29 = Lipstick`
- `30 = Eyeshadow`
- `31 = Eyeliner`
- `32 = Blush`
- `33 = Facepaint`
- `34 = Eyebrows`
- `35 = Eyecolor`
- `36 = Socks`
- `58 = SkinOverlay`
- `71 = SkinDetailScar`
- `72 = SkinDetailAcne`

Source anchor:

- [TS4SimRipper Enums.cs](../../references/external/TS4SimRipper/src/Enums.cs)

Current direct cache checks already align with two important rows:

| `BodyType` | External enum name | Direct cache shape | Safe reading |
| --- | --- | --- | --- |
| `10` | `Earrings` | sample names are `*Earrings*`; `0|0 = 2058`; `32|65536 = 572` | confidently named |
| `28` | `FacialHair` | sample names are `*FacialHair*`; `0|8300 = 1548`; `32|65536 = 624`; `2|8300 = 36` | confidently named |

Safe reading:

- low-value rows that match the external enum should be translated directly
- these rows are no longer part of the large unresolved compositor mass

## Largest unresolved buckets

These are still the biggest unresolved rows in `cas_part_facts`:

| `BodyType` | Count | Current reading |
| --- | ---: | --- |
| `1140850688` | `113301` | large mixed bucket, not a clean slot |
| `1090519040` | `12292` | large mixed bucket, not a clean slot |
| `1090519046` | `4752` | large mixed bucket, not a clean slot |
| `1140850689` | `3273` | likely related to the same high-bit family, still mixed |
| `1610612736` | `2955` | large mixed bucket, still mixed |
| `1862270976` | `2175` | partly makeup-like, but not cleanly isolated yet |
| `-2147483648` | `2175` | high-bit or sign-bit bucket, still mixed |

Why these are not safe to rename yet:

- sample internal names mix different apparent slots such as `Body`, `Top`, `Bottom`, `Hair`, `Hat`, `Shoes`, and `Accessory`
- their strongest values often sit on broad `composition=0 | sort=0` or other repeated high-bit lanes
- current external enum evidence covers the low-value named rows, not these large high-bit values

## Current shape of the biggest mixed buckets

### `1140850688`

Direct counts:

- `113301` rows
- `composition=0 = 111159`
- `composition=255 = 1743`
- strongest pairs:
  - `0 | 0 = 65444`
  - `0 | 1536 = 13192`
  - `0 | 1792 = 7861`
  - `0 | 16000 = 5958`
  - `0 | 1280 = 5747`

Current sample names mix:

- `yfBottom_GP01ShortsCargo_*`
- `cfBottom_SP20TrackSuit_*`
- `yfBody_GP06DressSafari_*`
- `ymAcc_NecklaceEP19Key_*`

Safe reading:

- this is not one readable slot
- it currently behaves more like a broad flagged bucket than a normal `BodyType` enum row

### `1090519040`

Direct counts:

- `12292` rows
- `composition=0 = 11011`
- `composition=255 = 1206`
- strongest pairs:
  - `0 | 0 = 5798`
  - `255 | 0 = 1206`
  - `0 | 15616 = 822`

Current sample names mix:

- `cmTop_*`
- `cuHat_*`

Safe reading:

- mixed bucket
- not safe to collapse into `Top` or `Hat`

### `1090519046`

Direct counts:

- `4752` rows
- `composition=0 = 4101`
- `composition=255 = 633`
- strongest pairs:
  - `0 | 0 = 2121`
  - `255 | 0 = 633`
  - `0 | 1023410176 = 336`

Current sample names mix:

- `puHat_*`
- `puBody_*`

Safe reading:

- mixed bucket
- still likely part of the same large unresolved family as `1090519040`

Related family signal:

- both values sit in the same `0x41` high-byte family
- the same family also includes `0x4100000B = 2118` rows
- that family mixes `Body`, `Hat`, and other apparel-like names

### `1140850689`

Direct counts:

- `3273` rows
- `composition=0 = 3147`
- strongest pairs:
  - `0 | 0 = 1959`
  - `0 | 14000 = 273`
  - `0 | 16000 = 171`

Current sample names lean toward:

- `yfHair_*`

Safe reading:

- this looks narrower than `1140850688`
- it still needs explicit translation work before it can be treated as ordinary `Hair`

Related family signal:

- both values sit in the same `0x44` high-byte family
- the whole family is heavily dominated by `0x44000000`
- the current top names of the full `0x44` family lean strongly toward necklaces and accessories, but the largest value still mixes in body and bottom rows

### `1610612736`

Direct counts:

- `2955` rows
- `composition=0 = 2841`
- `composition=255 = 114`
- strongest pairs:
  - `0 | 3072 = 840`
  - `0 | 3840 = 504`
  - `0 | 3584 = 336`

Current sample names mix:

- `cmTop_*`
- `cuTop_*`

Safe reading:

- mixed bucket
- not safe to rename yet

Related family signal:

- the wider `0x60` family includes small companion rows ending in `0x0B`, `0x06`, and `0x01`
- current names still mix `Top` and accessory-like content

### `1862270976`

Direct counts:

- `2175` rows
- `composition=0 = 1365`
- `composition=4 = 462`
- `composition=255 = 318`
- strongest pairs:
  - `0 | 0 = 774`
  - `0 | 196608 = 369`
  - `4 | 2100 = 240`
  - `4 | 5200 = 123`

Current sample names include:

- `yfMakeupLipstick_*`
- `ymHair_*`

Safe reading:

- there is a real makeup-like branch here
- but this value is not yet clean enough to translate to one simple slot name

Related family signal:

- the wider `0x6F` family contains:
  - `0x6F000005 = 837`, currently leaning toward head decoration
  - `0x6F00000A = 504`, currently leaning toward horse accessories
  - `0x6F000001 = 369`, currently leaning toward facial hair
- this family is clearly structured, but not by one simple low-byte rule

### `-2147483648`

Direct counts:

- `2175` rows
- `composition=0 = 2169`
- strongest pairs:
  - `0 | 0 = 2058`
  - `0 | 536870912 = 60`
  - `0 | 196608 = 51`

Current sample names mix:

- `yfBody_*`
- `ymShoes_*`
- `ahHat_*`

Safe reading:

- this is another mixed high-bit bucket
- current sign-bit appearance is a strong warning against treating it as a normal named enum row

Related family signal:

- the `0x80` family is small but still structured
- current samples mix `Body`, `Shoes`, `Hat`, and nude helper rows
- it should stay unresolved until a clearer family rule appears

## High-byte family notes

### `0x41`

Current known members:

- `0x41000000 = 12292`
- `0x41000006 = 4752`
- `0x4100000B = 2118`
- `0x41000001 = 308`

Current safe reading:

- apparel-heavy mixed family
- not cleanly reducible to one slot
- the high byte overlaps externally with `OccultEyeSocket`

Current name-token profile:

- `Body = 6940`
- `Top = 6059`
- `Shoes = 2226`
- `Acc = 2193`
- `Hat = 1714`

Current package coverage:

- `159` packages

Current stronger subrows:

- `0x41000000 = 12292` stays broadly mixed
- `0x41000006 = 4752` still mixes `Body`, `Top`, `Shoes`, `Hat`, and `Acc`
- `0x4100000B = 2118` introduces the first stronger `composition=32 | sort=65536 = 333` clothing-like branch inside this family

Current safe reading:

- this looks like a broad apparel-family namespace, not a normal one-slot enum row
- low-byte hints may matter inside the family, but they do not close it by themselves
- the external overlap makes a simple plain-slot reading less likely
- the first concrete continuation packet now lives in [BodyType 0x41 Family Boundary](live-proof-packets/bodytype-0x41-family-boundary.md)

### `0x44`

Current known members:

- `0x44000000 = 113301`
- `0x44000001 = 3273`

Current safe reading:

- the single biggest unresolved family
- mixes body-like and accessory-like rows
- must stay above all narrower translation work
- the high byte overlaps externally with `OccultLeftCheek`

Current name-token profile:

- `Top = 36625`
- `Bottom = 24155`
- `Body = 22474`
- `Acc = 14600`
- `Shoes = 6962`
- `Hat = 4274`

Current package coverage:

- `247` packages

Current stronger subrows:

- `0x44000000 = 113301` dominates the whole unresolved space
- `0x44000001 = 3273` is smaller and slightly narrower, but still mixed

Current safe reading:

- this is the main unresolved apparel-family blocker above the readable slots
- any next translation pass should start here before trying to polish smaller families
- the occult-face-area overlap now makes a borrowed texture-space reading plausible
- that first pass now lives in [BodyType 0x44 Family Boundary](live-proof-packets/bodytype-0x44-family-boundary.md)

### `0x52`

Current known members:

- `0x52000000 = 2412`
- `0x52000006 = 948`
- `0x5200000B = 414`

Current safe reading:

- mostly bottom-heavy family in current names
- still not clean enough to rename by low byte alone
- the high byte overlaps externally with `BodyScarArmLeft`
- the concrete packet now lives in [BodyType 0x52 Family Boundary](live-proof-packets/bodytype-0x52-family-boundary.md)

Current name-token profile:

- `Bottom = 3711`
- `Body = 63`

Current package coverage:

- `85` packages

Current safe reading:

- this is the first family that looks close to one readable direction: strongly bottom-heavy
- it still needs explicit proof before being renamed as a true bottom-only family
- the body-scar overlap is a warning that the family may still be texture-area driven rather than slot driven

### `0x6D`

Current known members:

- `0x6D00000C = 1938`
- `0x6D000005 = 750`
- `0x6D00000A = 498`

Current safe reading:

- makeup and skin-detail heavy family
- direct proof that low byte cannot be used as a simple ordinary-slot decoder
- the high byte overlaps externally with `HairFeathers`

Current name-token profile:

- `SkinDetail = 1953`
- `MakeupLipstick = 747`
- `Body = 252`
- `Bottom = 90`
- `Shoes = 87`
- `Acc = 75`

Current package coverage:

- `72` packages

Current stronger subrows:

- `0x6D00000C = 1938` is overwhelmingly `SkinDetail`
- `0x6D000005 = 750` is overwhelmingly `MakeupLipstick`
- `0x6D00000A = 498` mixes apparel-like rows again

Current safe reading:

- this family is heavily cosmetic, but not cleanly one-category
- it is the strongest current counterexample to any naive low-byte decoding rule
- the `HairFeathers` overlap is another reason to treat the high byte as auxiliary-area vocabulary
- the concrete packet now lives in [BodyType 0x6D Family Boundary](live-proof-packets/bodytype-0x6d-family-boundary.md)

### `0x6F`

Current known members:

- `0x6F000000 = 2175`
- `0x6F000005 = 837`
- `0x6F00000A = 504`
- `0x6F000001 = 369`

Current safe reading:

- mixed special-content family
- currently spans makeup, head decoration, horse accessories, and facial hair
- the high byte overlaps externally with `TailBase`

Current name-token profile:

- `MakeupLipstick = 942`
- `MakeupEyeshadow = 897`
- `Acc = 468`
- `FacialHair = 393`
- `MakeupEyeliner = 342`
- `MakeupBlush = 300`

Current package coverage:

- `48` packages

Current stronger subrows:

- `0x6F000000 = 2175` is makeup-heavy
- `0x6F000005 = 837` adds head-decoration content
- `0x6F00000A = 504` leans toward accessory and horse tack rows
- `0x6F000001 = 369` leans toward facial hair

Current safe reading:

- this family looks like a special cosmetic namespace, not normal clothing slots
- it is structured enough to keep as its own translation track
- the `TailBase` overlap again supports a secondary-area reading more than an ordinary slot reading
- the concrete packet now lives in [BodyType 0x6F Family Boundary](live-proof-packets/bodytype-0x6f-family-boundary.md)

### `0x80`

Current known members:

- `0x80000000 = 2175`
- `0x8000000B = 6`

Current safe reading:

- sign-bit family
- too mixed and too small for confident naming
- the concrete packet now lives in [BodyType 0x80 Family Boundary](live-proof-packets/bodytype-0x80-family-boundary.md)

Current name-token profile:

- `Body = 1518`
- `Acc = 432`
- `Hat = 210`

Current package coverage:

- `27` packages

Current safe reading:

- still unresolved
- current sign-bit shape makes it risky to flatten into ordinary slot names too early

## First family-level priority

Current safe reading:

- the restart-safe high-byte packet stack for the current queue is now closed:
  - `0x44`
  - `0x41`
  - `0x6D`
  - `0x6F`
  - `0x52`
  - `0x80`
- the next highest-priority step is no longer “open another large high-byte family first”
- the next highest-priority step is to revisit overlay/detail priority against that now-frozen packet stack

## What this changes

What is now safe:

- treat `composition_method` as normal queryable cache data
- translate low-value enum-backed rows directly when they match the external `BodyType` enum
- keep the biggest high-bit rows explicitly unresolved and mixed
- treat the unresolved space as repeated high-byte families rather than as one flat bag of mysterious numbers
- use `BodyType + AdditionalTextureSpace` as the leading external hypothesis for why these families look packed
- use the new `0x44`, `0x41`, `0x6D`, `0x6F`, `0x52`, and `0x80` packet docs as restart-safe proof layers instead of re-deriving those families from scratch

What is no longer safe:

- pretending that the readable slot subset already explains the dominant `composition=0` lane
- treating `1140850688` or similar values as ordinary hidden slot names without proof
- renaming high-byte values from the low byte alone
- keeping “repopulate the cache” as the next priority after the `CompositionMethod` census
- pretending the high byte is already fully decoded just because it overlaps with one external enum name

## Recommended next work

1. Re-run overlay/detail and compositor-priority reading against the now-frozen high-byte packet stack.
2. Keep the strongest external hypothesis explicit:
   - high byte as `AdditionalTextureSpace` or similar secondary-area signal
   - low byte as the remaining primary slot signal only where the evidence supports it
3. Reopen any individual high-byte family only if a stronger external vocabulary packet appears or a later compositor question specifically depends on it.
