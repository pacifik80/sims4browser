# Hair, Accessory, And Shoes Authority Table

This document turns the current worn-slot evidence into one explicit sibling table for `Hair`, `Accessory`, and `Shoes` after shell selection and before broader compositor guesswork.

Related docs:

- [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [Body And Head Shell Authority Table](body-head-shell-authority-table.md)
- [Overlay And Detail Family Authority Table](overlay-detail-family-authority-table.md)
- [Skintone And Overlay Compositor](skintone-and-overlay-compositor.md)
- [CompositionMethod Census Baseline](compositionmethod-census-baseline.md)
- [SortLayer Census Baseline](sortlayer-census-baseline.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
Hair, Accessory, And Shoes Authority Table
├─ Hair identity row ~ 93%
├─ Accessory identity row ~ 91%
├─ Shoes overlay row ~ 92%
├─ Worn-slot-versus-shell boundary ~ 94%
└─ Exact runtime material tie-breaks ~ 58%
```

## What this table is for

- make the current worn-slot reading explicit enough for implementation work
- keep `Hair`/`Accessory` exact-slot authority separate from `Shoes` body-assembly overlay handling
- stop clothing-like compositor lanes from being overread as shell or skintone authority

## Current authority table

| Question | Hair | Shoes | Accessory | Current safe rule |
| --- | --- | --- | --- | --- |
| Main role | head-adjacent worn slot | footwear overlay-capable worn slot | head-adjacent worn slot | all three are selected after shell identity, not as shell replacements |
| Typical body type | `2` | `8` | `12` in the current repo slot mapping; external enum wording still uses `NECKLACE = 12` as the closest creator-facing anchor | body-type vocabulary is still slot vocabulary, not family-shader vocabulary |
| Best current selection signal | exact part-link from `SimInfo` first | selected footwear part, but still routed through body-assembly overlay logic | exact part-link from `SimInfo` first | exact worn-slot identity should outrank compatibility fallback |
| Current repo slot matching | exact normalized `hair` category | matched through body-assembly slot/category logic | exact normalized `accessory` category | `Hair` and `Accessory` are the cleanest exact-slot rows; `Shoes` stays special |
| Material-input chain after selection | selected `CASP -> linked GEOM -> MTNF / explicit material resources / parsed CASP fields` | same chain after footwear acceptance | same chain after selection | worn-slot families still feed the shared canonical material path |
| `RegionMap` role | post-selection geometry/coverage modifier when present | active coverage/layer signal; can raise footwear above lower-leg body regions | post-selection geometry/coverage modifier when present | `RegionMap` refines accepted parts; it does not choose the slot |
| Current direct pair floor | overwhelmingly `composition=0 | sort=12000` | split between `composition=0 | sort=10700` and `composition=32 | sort=65536` | split between `composition=0 | sort=17100` and `composition=32 | sort=65536` | `Hair` is the cleanest ordinary worn-slot lane; `Shoes` and `Accessory` preserve a real clothing-like `32|65536` branch without becoming shell authority |
| Skintone interaction | excluded from skintone-target routing | excluded from skintone-target routing | excluded from skintone-target routing | skintone remains shell-scoped rather than mutating these worn slots directly |
| If exact slot evidence is missing | compatibility fallback only | body-assembly fallback only | compatibility fallback only | absence of exact slot evidence should weaken confidence, not silently convert these rows into shell logic |

## Current safest order

The safest current order is:

```text
choose body/head shell
        ->
accept exact head-adjacent worn slots first
   (`Hair`, `Accessory`)
        ->
accept `Shoes` as footwear layer inside body assembly
        ->
resolve each accepted part through
`CASP -> GEOM -> material candidates`
        ->
run compositor-only and skintone-only passes on their own bounded targets
```

What this means:

- `Hair` and `Accessory` should be read as exact worn-slot identity first
- `Shoes` should be read as a real worn slot with extra body-region/layer behavior, not as a body shell
- all three still converge into the same shared shader/material contract once authoritative inputs are chosen

## Current evidence anchors

### Externally confirmed

- [Modifying Sim Appearances](https://thesims4moddersreference.org/tutorials/modifying-sim-appearances/) keeps `CAS Parts` and `Skin Tones` separate, describes body types as outfit slots, and records `HAIR = 2`, `SHOES = 8`, and `NECKLACE = 12`.
- [File Types](https://thesims4moddersreference.org/reference/file-types/) keeps `CAS Part`, `Geometry`, and `Region Map` as distinct CAS resources instead of one flattened blob.
- [Adding new GEOMs to a CAS part with s4pe and S4CASTools](https://modthesims.info/t/536671) explicitly edits `CASP` to pick up new `GEOM` rows and updates `RegionMap` to keep render order and LOD linkage aligned.

Safe reading:

- creator-facing sources still support `CASP` as the slot/body-type identity root
- `GEOM` and `RegionMap` remain linked follow-on resources for accepted parts
- none of those sources promote `Hair`, `Accessory`, or `Shoes` into shell or skintone authority

### Local snapshots of external tooling

- [TS4SimRipper CASP.cs](../../references/external/TS4SimRipper/src/CASP.cs) exposes `bodyType`, `sortLayer`, `compositionMethod`, `RegionMapIndex`, and per-`LOD` mesh links on the `CASP` resource.
- [TS4SimRipper PreviewControl.cs](../../references/external/TS4SimRipper/src/PreviewControl.cs) keeps `Shoes` separate enough to:
  - read the footwear `RegionMap`
  - compare calf/knee layers against body and bottom parts
  - raise a synthetic footwear `sortLayer` to `17000` when shoes end up on top
- the same file also sorts the main image stack by `CompositionMethod` first and `SortLayer` second, which keeps footwear ordering inside compositor logic rather than shell identity.
- [TS4SimRipper TONE.cs](../../references/external/TS4SimRipper/src/TONE.cs) and [SkinBlender.cs](../../references/external/TS4SimRipper/src/SkinBlender.cs) keep `SkinSets`, `OverlayInstance`, and overlay lists in a separate `TONE` branch, not in the `Hair`/`Accessory`/`Shoes` selection lane.

### Current repo boundary

- [AssetServices.cs](../../src/Sims4ResourceExplorer.Assets/AssetServices.cs) preserves `ExactPartLink` as the authoritative source kind for selected worn slots.
- The same file currently:
  - treats `Hair` and `Accessory` as head-adjacent exact-slot selections
  - keeps `Shoes` in indexed default-body and body-assembly slot categories
  - labels `Shoes` as `Footwear layer`
  - keeps a separate note when `Shoes` are active on top of the current body shell
- current human CAS slot ordering is `Hair`, `Full Body`, `Top`, `Bottom`, `Shoes`, `Accessory`, which is another signal that `Shoes` remain part of worn-slot assembly rather than shell identity.

### Local direct count floor

The stricter whole-install floor still lives in:

- [CompositionMethod Census Baseline](compositionmethod-census-baseline.md)
- [SortLayer Census Baseline](sortlayer-census-baseline.md)

The smaller current local cache snapshot is:

- [hair_accessory_shoes_authority_snapshot_2026-04-21.json](../../tmp/hair_accessory_shoes_authority_snapshot_2026-04-21.json)

Current snapshot reading from the local profile-index cache:

- `Hair`: `16871` rows, almost entirely `composition=0 | sort=12000 = 16808`
- `Shoes`: `5222` rows, split between `composition=0 | sort=10700 = 2964` and `composition=32 | sort=65536 = 2258`
- `Accessory`: `3429` rows, split between `composition=0 | sort=17100 = 2281` and `composition=32 | sort=65536 = 1148`

Safe reading:

- the smaller live cache agrees directionally with the stronger census docs
- `Hair` is the cleanest ordinary worn-slot lane on the current parsed layer
- `Shoes` and `Accessory` preserve the clothing-like `32|65536` branch without becoming shell or skintone roots

## Honest limit

This table does not yet prove:

- the exact in-game tie-break when multiple worn slots share identical downstream material characteristics
- the exact runtime ranking between embedded `MTNF`, explicit material resources, and parsed `CASP` routing for every worn-slot case
- the final transparency-family outcome for alpha-heavy hair or accessory content

## Recommended next work

1. Keep this table paired with [Body And Head Shell Authority Table](body-head-shell-authority-table.md) so shell and worn-slot identity do not collapse back together.
2. Return to [SimSkin Versus SimSkinMask](live-proof-packets/simskin-vs-simskinmask.md) as the next highest-priority character-side live-proof packet now that the sibling worn-slot table exists.
3. Reopen hair/accessory transparency only through the narrower `SimGlass` and `SimAlphaBlended` boundary docs, not by weakening this slot-authority table.
