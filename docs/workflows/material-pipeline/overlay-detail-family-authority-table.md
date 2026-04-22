# Overlay And Detail Family Authority Table

This document turns the current compositor evidence into the first explicit table for `CAS` overlay/detail families after shell selection.

Related docs:

- [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [Body And Head Shell Authority Table](body-head-shell-authority-table.md)
- [CompositionMethod And SortLayer Boundary](compositionmethod-sortlayer-boundary.md)
- [CompositionMethod Census Baseline](compositionmethod-census-baseline.md)
- [Overlay-Detail Priority After High-Byte Stack](live-proof-packets/overlay-detail-priority-after-highbyte-stack.md)
- [Skintone And Overlay Compositor](skintone-and-overlay-compositor.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
Overlay And Detail Family Authority Table
├─ Overlay/detail identity root ~ 91%
├─ Separation from shell selection ~ 94%
├─ CompositionMethod practical table ~ 91%
├─ SortLayer ordering role ~ 88%
├─ Skintone-versus-overlay split ~ 87%
└─ Exact game-faithful blend math ~ 39%
```

## What this table is for

- make the current overlay/detail reading explicit enough for implementation work
- keep shell choice separate from layer ordering
- show where ordinary `CASPart` overlay/detail families stop and where `Skintone`-carried overlay logic begins

## Current authority table

| Layer family | Main identity source | Main ordering fields | Interaction with skintone | Current safe rule |
| --- | --- | --- | --- | --- |
| skin details | exact selected `CASP` | `CompositionMethod`, `SortLayer`, optional `part_shift` / `layer_id` carry-through | sits on top of already selected shell/skintone result | do not treat skin details as body/head shell choice |
| tattoos | exact selected `CASP` | `CompositionMethod`; `SortLayer`; custom color-shift carry-through is visible in tooling | usually post-skintone layer; keep separate from shell and from base skintone set | treat as compositor-driven layer, not as shell identity |
| makeup | exact selected `CASP` | `CompositionMethod`, `SortLayer` | reference code uses skintone makeup opacity for `CompositionMethod = 2` and a second makeup opacity path for `4` | makeup is a post-shell, post-skintone face/body layer |
| face paint and similar face overlays | exact selected `CASP` | `CompositionMethod`, `SortLayer`, optional shift/layer metadata | belongs with compositor-driven face/body layers | keep it in the same overlay/detail family branch until a narrower rule is proved |
| skintone-carried overlay images | selected `TONE` and active `SkinSet`, not ordinary overlay/detail `CASP` | `OverlayInstance`, `OverlayMultiplier`, `Opacity`, `SortOrder`, overlay lists in `TONE` | this is part of the skintone branch itself | keep this branch separate from ordinary `CASPart` makeup/tattoo/detail rows |

## Current safest order

The safest current order is:

```text
choose body/head shell
        ->
resolve shell-compatible materials
        ->
apply skintone base and skintone-carried overlay branch
        ->
apply ordinary CASPart overlay/detail layers
   by CompositionMethod and SortLayer
        ->
final compositor output
```

What this means:

- `CompositionMethod` and `SortLayer` matter after shell choice
- ordinary overlay/detail families do not replace the shell
- skintone-carried overlay images belong to the skintone branch, not to the ordinary `CASPart` overlay/detail branch

## Current mode table

This is the strongest safe reading from the local `TS4SimRipper` copy plus the current shared docs.

| `CompositionMethod` | Current reading | Current confidence |
| --- | --- | --- |
| `0` | straight overlay-like path | medium-high |
| `1` | often tattoo-oriented default in creator tooling | medium |
| `2` | makeup-like path using skintone makeup opacity | high |
| `3` | grayscale-shading-oriented overlay path | medium-high |
| `4` | second makeup-like path using a second makeup opacity input | high |

`SortLayer` rule that is already safe:

- higher layers should be treated as later-drawn than lower layers
- equal-layer collisions are still a practical instability case, not a closed rule

## Current direct prevalence floor

The new whole-install package census adds a direct counted floor:

- `RowsWithCompositionMethodZero = 243517`
- `RowsWithCompositionMethodNonZero = 55511`
- `DistinctCompositionMethods = 59`
- the dominant non-zero lane is `composition=32 = 44619`
- the strongest non-zero pair is `composition=32 | sort=65536 = 44598`

Readable slot patterns now look like this:

| Slot | Strongest current direct pattern |
| --- | --- |
| `Hair` | almost entirely `composition=0 | sort=12000` |
| `Full Body` | split between `composition=32 | sort=65536` and `composition=0 | sort=16000` |
| `Top` | split between `composition=0 | sort=16000` and `composition=32 | sort=65536` |
| `Bottom` | split between `composition=0 | sort=14000` and `composition=32 | sort=65536` |
| `Shoes` | split between `composition=0 | sort=10700` and `composition=32 | sort=65536` |
| `Accessory` | split between `composition=0 | sort=17100` and `composition=32 | sort=65536` |

Safe reading:

- the direct data now confirms that `CompositionMethod` is not just a narrow tooling hint
- `composition=32` is especially important for clothing-like and accessory-like overlay rows
- the rare tooling-discussed values `2`, `3`, and `4` should still be treated as real special cases, but not as the dominant whole-install story

## Direct low-slot versus high-byte split

The new packet [Overlay-Detail Priority After High-Byte Stack](live-proof-packets/overlay-detail-priority-after-highbyte-stack.md) now adds a sharper restart-safe boundary.

The current same-layer query floor is now frozen in:

- [composition_sortlayer_boundary_snapshot_2026-04-21.json](../../tmp/composition_sortlayer_boundary_snapshot_2026-04-21.json)

Current stronger direct reading:

- ordinary low-value overlay/detail rows still provide the cleanest precedence anchors where direct pair patterns are stable
- the mixed high-byte families can reuse the same `CompositionMethod` and `SortLayer` lanes, especially the cosmetic-heavy `0x6F` family
- that reuse is not enough to promote those high-byte families into the main overlay/detail vocabulary

Useful current examples from [overlay_detail_priority_snapshot_2026-04-21.json](../../tmp/overlay_detail_priority_snapshot_2026-04-21.json):

- `Lipstick` keeps a strong ordinary lane:
  - `composition=4 | sort=5500 = 1004 / 1131`
- `Eyeliner` keeps a strong ordinary lane:
  - `composition=0 | sort=5300 = 354 / 399`
- the stricter same-layer query now keeps the same shape on the current shard-backed floor:
  - `Lipstick` / `29 -> 4 | 5500 = 496`
  - `Eyeshadow` / `30 -> 4 | 5200 = 304`; `4 | 2100 = 136`
  - `Blush` / `32 -> 4 | 2100 = 78`
  - `Facepaint` / `33 -> 0 | 7500 = 118`
  - `SkinOverlay` / `58 -> 1 | 1100 = 5`
- `0x44000000` remains overwhelmingly mixed:
  - `composition=0 = 111159 / 113301`
- `0x6F000000` echoes ordinary cosmetic lanes without replacing them:
  - `composition=4 | sort=2100 = 240`
  - `composition=4 | sort=5200 = 123`
  - `composition=4 | sort=5500 = 99`
  - but also `composition=0 | sort=0 = 774`

Safe reading:

- low-value overlay/detail rows plus the separate `TONE` branch should stay above the mixed high-byte families in precedence reasoning
- the same-layer query now also shows that readable shell/worn-slot rows keep different dominant pairs:
  - `Head -> 0 | 1000 = 88`
  - `Hair -> 0 | 12000 = 5771`
  - `Full Body -> 32 | 65536 = 3437`; `0 | 16000 = 2824`
- high-byte family packets remain interpretation guards, not primary precedence anchors

## Current evidence anchors

### Local copy of external tooling

- [TS4SimRipper CASP.cs](../../references/external/TS4SimRipper/src/CASP.cs) exposes both `CompositionMethod` and `SortLayer` as first-class `CASP` fields.
- [TS4SimRipper PreviewControl.cs](../../references/external/TS4SimRipper/src/PreviewControl.cs) sorts the image stack by `CompositionMethod` first and `SortLayer` second, then applies separate handling for `CompositionMethod = 2`, `3`, and `4`.
- The same file also shows a footwear-specific override that can push shoes to a high synthetic layer, which is one more reason not to merge footwear logic into shell identity.
- [TS4SimRipper TONE.cs](../../references/external/TS4SimRipper/src/TONE.cs) exposes `SkinSets`, `OverlayList`, `OverlayInstance`, `OverlayMultiplier`, `Opacity`, and `SortOrder`.
- [TS4SimRipper SkinBlender.cs](../../references/external/TS4SimRipper/src/SkinBlender.cs) shows the skintone branch as multi-pass: skin-set texture, detail image, skin-set overlay or mask, overlay instance, and later mouth/age-like overlays.
- [TS4SimRipper Form1.cs](../../references/external/TS4SimRipper/src/Form1.cs) carries `part_shift` and `layer_id` per selected outfit part.
- [TS4SimRipper SIMInfo.cs](../../references/external/TS4SimRipper/src/SIMInfo.cs) carries `part_shifts` through selected outfit parts.

### Current repo boundary

- current repo code already parses `SortLayer` from `CASPart` in [AssetServices.cs](../../src/Sims4ResourceExplorer.Assets/AssetServices.cs)
- current repo code now also parses `CompositionMethod` from `CASPart` in [AssetServices.cs](../../src/Sims4ResourceExplorer.Assets/AssetServices.cs)
- current repo shell assembly keeps shell choice separate in [SimSceneComposer.cs](../../src/Sims4ResourceExplorer.Core/SimSceneComposer.cs)
- current docs already keep `CompositionMethod` and `SortLayer` on the compositor side in:
  - [CompositionMethod And SortLayer Boundary](compositionmethod-sortlayer-boundary.md)
  - [Skintone And Overlay Compositor](skintone-and-overlay-compositor.md)

## Honest limit

This table does not yet prove:

- the exact EA meaning of every `CompositionMethod` value across all categories
- the exact tie-break rule when `SortLayer` is equal
- the final visual order for every family across all patches and occult/body-part exceptions

## Recommended next work

1. Keep using [Overlay-Detail Priority After High-Byte Stack](live-proof-packets/overlay-detail-priority-after-highbyte-stack.md) so mixed high-byte families do not silently replace low-value overlay rows.
2. Use [Hair, Accessory, And Shoes Authority Table](hair-accessory-shoes-authority-table.md) as the parallel worn-slot boundary so clothing-like `32|65536` lanes do not bleed back into shell or skintone authority.
3. Reopen sparse rows such as `SkinOverlay`, `SkinDetailScar`, or `SkinDetailAcne` only when a stronger parsed or external packet appears.
