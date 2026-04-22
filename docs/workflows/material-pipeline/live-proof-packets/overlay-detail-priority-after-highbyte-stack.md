# Overlay-Detail Priority After High-Byte Stack

This packet fixes the current safest overlay/detail precedence reading now that the large mixed `BodyType` high-byte families have been frozen as restart-safe family packets.

Question:

- after closing the `0x44`, `0x41`, `0x6D`, `0x6F`, `0x52`, and `0x80` packets, is the safest compositor reading to keep ordinary low-value overlay/detail rows and skintone-carried overlays as the direct precedence anchors, while treating the high-byte families only as mixed namespace context?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Live-Proof Packets](README.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [SimSkin Body/Head Shell Authority](simskin-body-head-shell-authority.md)
- [BodyType Translation Boundary](../bodytype-translation-boundary.md)
- [Overlay And Detail Family Authority Table](../overlay-detail-family-authority-table.md)
- [CompositionMethod And SortLayer Boundary](../compositionmethod-sortlayer-boundary.md)
- [CompositionMethod Census Baseline](../compositionmethod-census-baseline.md)
- [SortLayer Census Baseline](../sortlayer-census-baseline.md)
- [Skintone And Overlay Compositor](../skintone-and-overlay-compositor.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)
- [Overlay/detail priority snapshot](../../../tmp/overlay_detail_priority_snapshot_2026-04-21.json)
- [Composition/sort same-layer snapshot](../../../tmp/composition_sortlayer_boundary_snapshot_2026-04-21.json)

## Scope status (`v0.1`)

```text
Overlay-Detail Priority After High-Byte Stack
├─ Externally proved overlay/detail identity ~ 89%
├─ Local external snapshot packet ~ 91%
├─ Direct low-slot versus high-byte split ~ 93%
├─ Skintone-versus-ordinary-overlay boundary ~ 87%
└─ Exact universal runtime order ~ 44%
```

## Externally proved overlay/detail identity

What is already strong enough:

- [CompositionMethod | Sims 4 Files Wiki](https://sims-4-files.fandom.com/wiki/CompositionMethod) keeps `CompositionMethod` in `CASPartResource` and describes it as the field that determines how skin-affecting textures such as overlays and makeup interact
- the same page preserves the current creator-facing rule of thumb:
  - `0` as a straight non-skin-blending path often used for makeup
  - `3` as the overlay-skin or skin-detail style path
- [SortLayer | Sims 4 Files Wiki](https://sims-4-files.fandom.com/wiki/SortLayer) keeps `SortLayer` in `CASPartResource` and describes it as the field that determines how textures are layered, with larger values layered higher
- [Modifying Sim Appearances](https://thesims4moddersreference.org/tutorials/modifying-sim-appearances/) keeps `CAS Parts` and `Skin Tones` as separate appearance categories and states that a Sim can only have one part from each `Body Type` in an outfit
- [TS4 Skininator](https://modthesims.info/d/568474) defines `TONE` as the file that links skin color images, overlays, and settings, and defines overlay as a texture layered on top of the skin details and skin color

Safe reading:

- ordinary overlay/detail precedence should stay anchored first in selected `CASP` rows plus `CompositionMethod` and `SortLayer`
- skintone-carried overlays remain a separate `TONE` branch, not just another ordinary overlay/detail slot
- high-byte `BodyType` families can echo overlay-like lanes without becoming the primary external vocabulary for overlay/detail precedence

Unsafe reading:

- do not let a mixed high-byte family replace direct overlay/detail slot vocabulary just because it reuses some of the same sort or composition lanes
- do not let skintone-carried overlays collapse back into ordinary `CASPart` overlay rows

## Local external snapshot packet

Current strongest local snapshot stack:

- [TS4SimRipper PreviewControl.cs](../../../references/external/TS4SimRipper/src/PreviewControl.cs) adds selected outfit parts to the image stack with both `SortLayer` and `CompositionMethod`
- the same file sorts the stack by `CompositionMethod` first and `SortLayer` second
- the same file then applies a narrow special-case split:
  - `CompositionMethod = 2` uses `currentTONE.SkinSets[0].MakeupOpacity`
  - `CompositionMethod = 4` uses `currentTONE.SkinSets[0].MakeupOpacity2`
  - `CompositionMethod = 3` is routed through a separate overlay handling path
- [TS4SimRipper TONE.cs](../../../references/external/TS4SimRipper/src/TONE.cs) preserves:
  - `SkinSets`
  - `OverlayInstance`
  - `OverlayMultiplier`
  - `OverlayList`
  - `Opacity`
  - `SortOrder`
- [TS4SimRipper SkinBlender.cs](../../../references/external/TS4SimRipper/src/SkinBlender.cs) explicitly composes:
  - body details
  - physique overlays
  - sculpt and outfit overlays
  - skin-set texture
  - optional skin-set overlay or mask
  - second-pass opacity
  - age/gender overlay instances
  - later mouth overlay

Why this packet matters:

- it keeps ordinary overlay/detail composition and skintone-carried overlay structure in one local external packet
- it shows that `CompositionMethod` special cases are already tied to `TONE` makeup opacity inputs, not just to free-floating slot labels
- it keeps high-byte `BodyType` interpretation below the compositor packet instead of letting those families overwrite it

## Direct low-slot versus high-byte split

Current direct snapshot:

- [overlay_detail_priority_snapshot_2026-04-21.json](../../../tmp/overlay_detail_priority_snapshot_2026-04-21.json)
- [composition_sortlayer_boundary_snapshot_2026-04-21.json](../../../tmp/composition_sortlayer_boundary_snapshot_2026-04-21.json)

Current strongest same-layer floor from `cas_part_facts`:

- whole-layer top pairs now stay directly visible together:
  - `composition=0 | sort=0 = 18668`
  - `composition=32 | sort=65536 = 12212`
  - `composition=0 | sort=16000 = 8970`
  - `composition=0 | sort=12000 = 5960`
  - `composition=0 | sort=14000 = 3750`
  - `composition=0 | sort=17100 = 3503`
- readable shell/worn-slot rows keep different dominant pairs than ordinary low-value overlay rows:
  - `Head -> 0 | 1000 = 88`; `3 | 1000 = 2`
  - `Hair -> 0 | 12000 = 5771`
  - `Full Body -> 32 | 65536 = 3437`; `0 | 16000 = 2824`
  - `Top -> 0 | 16000 = 4894`; `32 | 65536 = 3715`
  - `Bottom -> 0 | 14000 = 3032`; `32 | 65536 = 2943`
  - `Shoes -> 0 | 10700 = 1420`; `32 | 65536 = 918`
  - `Accessory -> 0 | 17100 = 2035`; `32 | 65536 = 188`

Current strongest ordinary low-value overlay/detail anchors:

| Row | Strongest direct pair | Current safe reading |
| --- | --- | --- |
| `29` `Lipstick` | `composition=4 | sort=5500 = 1004 / 1131` | strong ordinary overlay row |
| `31` `Eyeliner` | `composition=0 | sort=5300 = 354 / 399` | strong ordinary overlay row |
| `33` `Facepaint` | `composition=0 | sort=7500 = 187 / 235`; `0 | 9000 = 36` | still ordinary overlay row, but not one single universal sort value |
| `30` `Eyeshadow` | `composition=4 | sort=5200 = 657 / 1230`; `4 | 2100 = 272`; `32 | 65536 = 92` | real overlay row, but not a perfectly clean one-lane packet |
| `32` `Blush` | `composition=4 | sort=2100 = 156 / 200`; `2 | 5600 = 44` | real overlay row with method split |
| `58` `SkinOverlay` | `13` parsed rows total | too small on the current parsed layer to anchor the whole precedence model |

Current stricter same-layer confirmation:

- `Lipstick` / `29 -> 4 | 5500 = 496`
- `Eyeshadow` / `30 -> 4 | 5200 = 304`; `4 | 2100 = 136`
- `Eyeliner` / `31 -> 0 | 5300 = 158`
- `Blush` / `32 -> 4 | 2100 = 78`
- `Facepaint` / `33 -> 0 | 7500 = 118`
- `SkinOverlay` / `58 -> 1 | 1100 = 5`

Current strongest mixed high-byte comparison rows:

| Row | Strongest direct packet | Current safe reading |
| --- | --- | --- |
| `0x44000000` | `composition=0 = 111159 / 113301`; only `4 = 42`; `32 = 12` | overwhelmingly mixed default-family lane, not an overlay anchor |
| `0x41000000` | `composition=0 = 11011 / 12292`; `255 = 1206` | mixed apparel-family lane, not an overlay anchor |
| `0x41000006` | `composition=0 = 4101 / 4752`; `255 = 633` | same family; still mixed |
| `0x52000000` | `composition=0 = 2112 / 2412`; `255 = 294` | concentrated family, but not direct overlay precedence vocabulary |
| `0x6F000000` | `composition=0 = 1365 / 2175`; `4 = 462`; `255 = 318` | the first strong family that echoes makeup-like lanes without becoming a clean low-value slot replacement |
| `0x80000000` | `composition=0 = 2169 / 2175` | mixed sign-bit family, not overlay precedence vocabulary |

Current stricter same-layer warning floor:

- `0x41000000 -> 0 | 0 = 806`; `255 | 0 = 214`
- `0x80000000 -> 0 | 196608 = 15`; `0 | 0 = 6`
- the same query layer therefore still separates ordinary overlay anchors from high-byte comparison rows instead of smoothing them together

Current strongest subrow warning packet:

- `0x6D00000C` is still mostly `composition=255 | sort=0 = 1293`
- `0x6D000005` is still mostly `composition=0 | sort=0 = 435` plus `255 | 0 = 255`
- `0x6F000000` now visibly reuses ordinary overlay/detail lanes:
  - `4 | 2100 = 240`
  - `4 | 5200 = 123`
  - `4 | 5500 = 99`
  - but the same row also keeps:
    - `0 | 0 = 774`
    - `0 | 196608 = 369`
    - `255 | 0 = 318`
- `0x6F000005` also mixes:
  - `191 | 8192 = 153`
  - `2 | 5600 = 36`
  - `0 | 196608 = 270`
  - `0 | 0 = 300`

What this proves:

- low-value overlay/detail rows remain the cleaner precedence anchors when they have direct slot identity plus stable pair patterns
- the same-layer query floor now shows that this is not only a cross-doc inference; ordinary overlay rows and readable shell/worn-slot rows really do keep different dominant pair shapes in one query layer
- high-byte families can reuse the same `CompositionMethod` or `SortLayer` lanes, especially the cosmetic-heavy `0x6F` family
- that reuse is not enough to promote the high-byte families into the main overlay/detail vocabulary

## Current safest precedence reading

The current safest synthesis is:

```text
choose shell and linked family context
        ->
resolve shell-compatible material targets
        ->
apply skintone base and skintone-carried overlay branch
        ->
apply ordinary low-value overlay/detail rows
   using CompositionMethod and SortLayer
        ->
consult high-byte family packets only when a row lives inside that mixed namespace
        ->
final compositor output
```

What this means right now:

- the high-byte packet stack narrows interpretation; it does not replace ordinary overlay/detail precedence anchors
- low-value overlay/detail rows are still the safest direct place to talk about compositor order
- `TONE` overlays remain a separate branch above ordinary overlay/detail rows
- readable shell/worn-slot rows still keep their own dominant pair shapes, so they should not be reused as the main ordinary overlay/detail vocabulary either
- when a row sits inside `0x44`, `0x41`, `0x6D`, `0x6F`, `0x52`, or `0x80`, use the family packet first and only then borrow low-byte hints where the evidence supports them

## What this packet proves

- the closed high-byte packet stack does not justify renaming overlay/detail precedence around the mixed families
- ordinary low-value overlay/detail rows plus the separate `TONE` branch remain the safest Tier A compositor anchors
- `0x6D` and `0x6F` now act as explicit comparison packets showing how high-byte families can echo overlay semantics without replacing them

## What this packet does not prove

- the exact universal live-game tie-break when two ordinary overlay/detail rows collide
- the exact runtime order between every skintone-carried overlay and every ordinary `CASPart` overlay row
- the exact meaning of unusual method values like `191`, `255`, or the negative sort tails now visible in mixed families
- full closure for sparse rows such as `SkinOverlay`, `SkinDetailScar`, or `SkinDetailAcne` on the current parsed subset

## Current implementation boundary

Current repo behavior is diagnostic only:

- the repo parses `CompositionMethod` and `SortLayer`
- the repo now has direct whole-install counts for both fields
- the repo still does not implement exact game-faithful compositor math
- the repo should therefore use this packet as an authority-order boundary, not as proof of final blend behavior

Useful implementation anchors:

- [AssetServices.cs](../../../src/Sims4ResourceExplorer.Assets/AssetServices.cs)
- [SimSceneComposer.cs](../../../src/Sims4ResourceExplorer.Core/SimSceneComposer.cs)
- [ExplorerTests.cs](../../../tests/Sims4ResourceExplorer.Tests/ExplorerTests.cs)

## Best next inspection step

1. Keep this packet as the restart-safe Tier A bridge from the closed high-byte family stack into overlay/detail precedence.
2. Keep [CompositionMethod And SortLayer Boundary](../compositionmethod-sortlayer-boundary.md) paired with this packet so same-layer pair evidence and precedence wording stay aligned.
3. Reopen sparse overlay rows only when a stronger parsed or external packet appears.
