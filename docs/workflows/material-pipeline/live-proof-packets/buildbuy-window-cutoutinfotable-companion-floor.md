# Build/Buy Window CutoutInfoTable Companion Floor

This packet checks whether the surviving `EP10` windows already carry explicit structural cutout companions rather than only cutout-leaning material hints.

Question:

- do the surviving window anchors already carry explicit same-instance `CutoutInfoTable` companions strong enough to promote a window-side structural cutout floor?

Related docs:

- [Build/Buy Window-Curtain Strongest-Pair Material Divergence](buildbuy-window-curtain-strongest-pair-material-divergence.md)
- [Build/Buy Window-Curtain Family Verdict Boundary](buildbuy-window-curtain-family-verdict-boundary.md)
- [Object Glass And Transparency](../family-sheets/object-glass-and-transparency.md)
- [Build/Buy Material Authority Matrix](../buildbuy-material-authority-matrix.md)
- [Object Transparency Evidence Ledger](../object-transparency-evidence-ledger.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)

## Scope status (`v0.1`)

```text
Build/Buy Window CutoutInfoTable Companion Floor
├─ External structural-cutout branch ~ 95%
├─ Sliding window CutoutInfoTable proof ~ 94%
├─ WindowBox CutoutInfoTable proof ~ 94%
└─ Window-side structural companion floor ~ 90%
```

## External rule that stays safe

What remains externally strong enough:

- windows, doors, and archways can depend on structural opening resources such as `Model Cutout` and `Cut Info Table`
- those structural resources should stay separate from generic object-glass and from generic alpha-only reading
- material-level cutout hints can coexist with structural opening resources rather than replacing them

External anchor:

- [Tutorial: how to make CC Cutout compatible with last update](https://s4cc.syboulette.fr/tutorial-how-to-make-cc-windows-doors-and-archways/)

Safe reading:

- once explicit cutout companions are present on a surviving window pair, the window-side branch is no longer only a material-hint hypothesis
- this still does not force exact runtime order between structural cutout companions and material-entry decoding

## Local companion snapshot

Snapshot:

- [buildbuy_window_cutoutinfo_companion_snapshot_2026-04-21.json](../../../tmp/buildbuy_window_cutoutinfo_companion_snapshot_2026-04-21.json)

## Surviving window 1

Fixture:

- `window2X1_EP10GENsliding2Tile_set1`
- object root: `C0DB5AE7:00000000:000000000003D122`
- promoted model root: `01661233:00000000:05879178560EABDF`
- explicit cutout companion: `81CA1A10:00000000:05879178560EABDF`

Local proof:

- [probe_window_sliding_set1_objectdef.txt](../../../tmp/probe_window_sliding_set1_objectdef.txt) already fixes the model root at `05879178560EABDF`
- direct `ProbeAsset --find-resource` now finds `81CA1A10:00000000:05879178560EABDF` in `EP10\\ClientFullBuild0.package`
- direct `ProbeAsset --inspect-resource` on that `CutoutInfoTable` yields:
  - `tableSize = 1`
  - `modelID = 0x05879178560EABDF`
  - `baseFileNameHash = 0x05879178560EABDF`
  - `widthAndMappingFlags = 0x42`
  - `minimumWallHeight = 3`
  - `numberOfLevels = 1`
  - `flags = 0x321`

Safe reading:

- the same-instance cutout companion is explicit rather than inferred
- the `modelID` points back to the exact promoted model root
- `flags = 0x321` keeps the strongest current structural reading:
  - `USES_INSTANCED_SHADER`
  - `IS_PORTAL`
  - `USES_WALL_LIGHTMAP`
  - `USES_CUTOUT`

## Surviving window 2

Fixture:

- `window2X1_EP10TRADwindowBox2Tile_set1`
- object root: `C0DB5AE7:00000000:000000000003D55A`
- promoted model root: `01661233:00000000:970F358CFC9991D1`
- explicit cutout companion: `81CA1A10:00000000:970F358CFC9991D1`

Local proof:

- [probe_window_box_set1_objectdef.txt](../../../tmp/probe_window_box_set1_objectdef.txt) already fixes the model root at `970F358CFC9991D1`
- direct `ProbeAsset --find-resource` now finds `81CA1A10:00000000:970F358CFC9991D1` in `EP10\\ClientFullBuild0.package`
- direct `ProbeAsset --inspect-resource` on that `CutoutInfoTable` yields the same core packet:
  - `tableSize = 1`
  - `modelID = 0x970F358CFC9991D1`
  - `baseFileNameHash = 0x970F358CFC9991D1`
  - `widthAndMappingFlags = 0x42`
  - `minimumWallHeight = 3`
  - `numberOfLevels = 1`
  - `flags = 0x321`

Safe reading:

- the second surviving window repeats the same structural companion pattern
- this is now a pair-level floor rather than a one-off anomaly on `sliding2Tile`

## Exact claim this packet proves

- the surviving window pair already carries explicit same-instance `CutoutInfoTable` companions strong enough to promote a window-side structural cutout companion floor

## Safe boundary after this packet

What is safe now:

- do promote a stronger window-side structural reading:
  - the surviving windows are no longer only cutout-leaning at the material level
  - they now also carry explicit same-instance `CutoutInfoTable` companions with `USES_CUTOUT` and `IS_PORTAL`
- do not yet claim full `ModelCutout` closure:
  - this packet proves `CutoutInfoTable`
  - it does not yet prove matching `ModelCutout` resources
- do not yet claim exact authority order:
  - the windows still also carry `AlphaCutoutMaterialDecodeStrategy` in the scene/material packet
  - this packet does not yet rank structural cutout companions against material-entry decoding
- do not widen again:
  - the next question is narrower than route widening and narrower than quartet-wide family relabeling

Implementation mistake this packet blocks:

- treating the surviving windows as if they only had alpha-like material hints when explicit structural cutout companions already exist on the same model instances

## Best next step

1. Keep the surviving window pair as the structural cutout companion floor:
   - `sliding2Tile`
   - `windowBox2Tile`
2. Continue the stronger closure through [Build/Buy Window ModelCutout Companion Closure](buildbuy-window-modelcutout-companion-closure.md).
3. Then decide whether window-side verdict language should read:
   - structural cutout companion plus material cutout routing
   - or structural cutout companion alone with material hints as secondary evidence

## Honest limit

What this packet proves:

- both surviving windows now have explicit same-instance `CutoutInfoTable` companions
- both entries point back to the exact promoted model roots
- both entries carry `USES_CUTOUT` and `IS_PORTAL`

What remains open:

- whether matching `ModelCutout` resources also survive on the same instances
- exact runtime authority order between structural cutout companions and `AlphaCutout` material decoding
- whether the window-side family verdict should explicitly name both structural cutout companions and threshold/cutout material routing in one combined branch
