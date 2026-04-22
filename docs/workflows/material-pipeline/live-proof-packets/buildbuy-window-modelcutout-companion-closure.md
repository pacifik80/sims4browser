# Build/Buy Window ModelCutout Companion Closure

This packet checks whether the surviving `EP10` windows close the remaining structural-companion gap by carrying explicit same-instance `ModelCutout` resources alongside the already-proved `CutoutInfoTable` companions.

Question:

- do the surviving window anchors also carry same-instance `ModelCutout` resources, so the structural opening branch is no longer missing half of its local companion stack?

Related docs:

- [Build/Buy Window CutoutInfoTable Companion Floor](buildbuy-window-cutoutinfotable-companion-floor.md)
- [Build/Buy Window-Curtain Strongest-Pair Material Divergence](buildbuy-window-curtain-strongest-pair-material-divergence.md)
- [Object Glass And Transparency](../family-sheets/object-glass-and-transparency.md)
- [Build/Buy Material Authority Matrix](../buildbuy-material-authority-matrix.md)
- [Object Transparency Evidence Ledger](../object-transparency-evidence-ledger.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)

## Scope status (`v0.1`)

```text
Build/Buy Window ModelCutout Companion Closure
├─ External structural-cutout branch ~ 95%
├─ Sliding window same-instance ModelCutout proof ~ 95%
├─ WindowBox same-instance ModelCutout proof ~ 95%
└─ Window-side full structural companion pair ~ 93%
```

## External rule that stays safe

What remains externally strong enough:

- windows, doors, and archways can depend on structural opening resources such as `Model Cutout` and `Cut Info Table`
- those structural resources stay separate from generic object-glass and from generic alpha-only reading
- material-level cutout hints can coexist with structural opening resources rather than replacing them

External anchor:

- [Tutorial: how to make CC Cutout compatible with last update](https://s4cc.syboulette.fr/tutorial-how-to-make-cc-windows-doors-and-archways/)

Safe reading:

- once both structural companions are explicit on the same surviving window pair, the structural opening branch is locally closed enough to stop talking as if only one helper half were proved
- this still does not force exact runtime order against material-entry decoding

## Local companion snapshot

Snapshot:

- [buildbuy_window_structural_cutout_snapshot_2026-04-21.json](../../../tmp/buildbuy_window_structural_cutout_snapshot_2026-04-21.json)
- [window_sameinstance_companion_listing_2026-04-21.txt](../../../tmp/window_sameinstance_companion_listing_2026-04-21.txt)

## Surviving window 1

Fixture:

- `window2X1_EP10GENsliding2Tile_set1`
- object root: `C0DB5AE7:00000000:000000000003D122`
- promoted model root: `01661233:00000000:05879178560EABDF`
- same-instance structural companions:
  - `07576A17:00000000:05879178560EABDF`
  - `81CA1A10:00000000:05879178560EABDF`

Local proof:

- [window_sameinstance_companion_listing_2026-04-21.txt](../../../tmp/window_sameinstance_companion_listing_2026-04-21.txt) shows that `ModelCutout` and `CutoutInfoTable` both sit on the exact same model-root instance
- direct `ProbeAsset --inspect-resource` on [inspect_modelcutout_sliding_2026-04-21.txt](../../../tmp/inspect_modelcutout_sliding_2026-04-21.txt) yields:
  - `Type: ModelCutout`
  - `PreviewKind: Hex`
  - `UncompressedSize: 204`
  - decoded-byte header `17 6A 57 07 01 00 00 00 08 00 00 00`, which safely reads as:
    - resource type `0x07576A17`
    - version `1`
    - point count `8`
- the earlier `CutoutInfoTable` proof on the same instance still carries `flags = 0x321` with `IS_PORTAL` plus `USES_CUTOUT`

Safe reading:

- the sliding window no longer has a one-sided structural-companion story
- the full local structural pair now survives on the exact promoted model root

## Surviving window 2

Fixture:

- `window2X1_EP10TRADwindowBox2Tile_set1`
- object root: `C0DB5AE7:00000000:000000000003D55A`
- promoted model root: `01661233:00000000:970F358CFC9991D1`
- same-instance structural companions:
  - `07576A17:00000000:970F358CFC9991D1`
  - `81CA1A10:00000000:970F358CFC9991D1`

Local proof:

- [window_sameinstance_companion_listing_2026-04-21.txt](../../../tmp/window_sameinstance_companion_listing_2026-04-21.txt) shows the same pair-level pattern on `windowBox2Tile`
- direct `ProbeAsset --inspect-resource` on [inspect_modelcutout_windowbox_2026-04-21.txt](../../../tmp/inspect_modelcutout_windowbox_2026-04-21.txt) yields the same core packet:
  - `Type: ModelCutout`
  - `PreviewKind: Hex`
  - `UncompressedSize: 204`
  - decoded-byte header `17 6A 57 07 01 00 00 00 08 00 00 00`
- the matching `CutoutInfoTable` on the same instance still holds `flags = 0x321`

Safe reading:

- the second surviving window repeats the same full structural-companion stack
- this is now a pair-level closure, not a one-fixture exception

## Exact claim this packet proves

- the surviving window pair now carries the full same-instance structural companion pair:
  - `ModelCutout`
  - `CutoutInfoTable`

## Safe boundary after this packet

What is safe now:

- do promote stronger window-side structural language:
  - both surviving windows now have the same-instance `ModelCutout + CutoutInfoTable` pair
  - that pair sits on the exact promoted model root rather than only on object metadata roots
- do keep material hints as separate evidence:
  - `AlphaCutoutMaterialDecodeStrategy` still survives in the direct material packet
  - the structural pair does not erase the material packet; it outruns the earlier “missing companion” gap
- do not yet claim exact runtime precedence:
  - this packet proves pair closure
  - it does not yet prove exact engine order between structural companions and material-entry routing

Implementation mistake this packet blocks:

- treating the surviving windows as if the structural opening branch were still incomplete after both same-instance structural companions are now locally explicit

## Best next step

1. Keep the surviving window pair as the closed structural-companion stack:
   - `sliding2Tile`
   - `windowBox2Tile`
2. Use that closure to decide whether the window-side family verdict can now be promoted above the remaining material-hint ambiguity.
3. Then move the next direct proof to the curtain side instead of reopening the same window-companion question.

## Honest limit

What this packet proves:

- both surviving windows now carry same-instance `ModelCutout`
- both surviving windows already carried same-instance `CutoutInfoTable`
- the window-side structural companion pair is now locally closed

What remains open:

- exact runtime authority order between the structural pair and `AlphaCutout` material decoding
- whether the curtain side closes as explicit `AlphaBlended` or only as a weaker threshold/cutout route
