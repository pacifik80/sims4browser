# Build/Buy Window-Curtain Widening Route

This packet freezes the next widened transparent-object route after the full `EP10` transparent-decor cluster stalled at the current inspection layer.

Question:

- after the exhausted transparent-decor route, does the current workspace already have one stronger widened window/curtain route than a vague return to the old window-heavy sweep?

Related docs:

- [Build/Buy Transparent Object Full-Route Stall](buildbuy-transparent-object-full-route-stall.md)
- [Build/Buy Window-Heavy Transparent Negative Control](buildbuy-window-heavy-transparent-negative-control.md)
- [Build/Buy Transparent Object Classification Boundary](buildbuy-transparent-object-classification-boundary.md)
- [Object Glass And Transparency](../family-sheets/object-glass-and-transparency.md)
- [Build/Buy Material Authority Matrix](../buildbuy-material-authority-matrix.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)

## Scope status (`v0.1`)

```text
Build/Buy Window-Curtain Widening Route
├─ External architectural-cutout anchor ~ 88%
├─ Widened route ranking evidence ~ 97%
├─ Exact ObjectDefinition-root mapping ~ 98%
└─ Stable live-fixture closure ~ 94%
```

## What is already externally proved

What is already strong enough:

- transparent `Build/Buy` fixtures must be classified through the object-side glass/transparency split before they are attached to any narrower family row
- TS4 creator-facing window, door, and archway workflows keep `Model Cutout` and `Cut Info Table` as explicit object-side resources rather than reducing the whole problem to texture alpha
- diagonal wall variants can require separate cutout/mesh handling, which keeps transparent architectural fixtures structurally narrower than a generic “glass-named object” sweep

External anchors:

- [Object Glass And Transparency](../family-sheets/object-glass-and-transparency.md)
- [Tutorial: how to make CC Cutout compatible with last update](https://s4cc.syboulette.fr/tutorial-how-to-make-cc-windows-doors-and-archways/)
- [Alpha Blending (Direct3D 9)](https://learn.microsoft.com/en-us/windows/win32/direct3d9/alpha-blending)

Safe reading:

- windows/curtains are now a better widened lane because they carry object-side cutout structure, not just because their names say `glass`
- this still does not prove object-glass, threshold/cutout, or `AlphaBlended` for any one live fixture

## Current local widened route

Current bounded snapshot:

- [buildbuy_window_curtain_widening_snapshot_2026-04-21.json](../../../tmp/buildbuy_window_curtain_widening_snapshot_2026-04-21.json)

Current strongest widened anchors:

1. `window2X1_EP10GENsliding2Tile`
   - survey root: `01661233:00000000:560EABDF05879178`
   - transformed model root: `01661233:00000000:05879178560EABDF`
   - exact `set1` identity root: `C0DB5AE7:00000000:000000000003D122`
   - companion bundle:
     - `Model`
     - `Rig`
     - `Slot`
     - `Footprint`
2. `window2X1_EP10TRADwindowBox2Tile`
   - survey root: `01661233:00000000:FC9991D1970F358C`
   - transformed model root: `01661233:00000000:970F358CFC9991D1`
   - exact `set1` identity root: `C0DB5AE7:00000000:000000000003D55A`
   - companion bundle:
     - `Model`
     - `Rig`
     - `Slot`
     - `Footprint`
3. `curtain1x1_EP10GENstrawTileable2Tile`
   - survey root: `01661233:00000000:229B82BD8FBB0B34`
   - transformed model root: `01661233:00000000:8FBB0B34229B82BD`
   - exact `set1` identity root: `C0DB5AE7:00000000:000000000003D568`
   - current bounded bundle:
      - `Model`
      - `Footprint`
4. `curtain2x1_EP10GENnorenShortTileable`
   - survey root: `01661233:00000000:47BE1D759870E130`
   - transformed model root: `01661233:00000000:9870E13047BE1D75`
   - exact `set1` identity root: `C0DB5AE7:00000000:000000000003D69B`
   - current bounded bundle:
      - `Model`
      - `Footprint`

Current weaker-but-real naming packet:

- `window1X1_EP10TRADglassPanelTileable3Tile`
- `window1X1_EP10TRADglassShortTileable2Tile`
- `window1X1_EP10TRADglassTallTileable3Tile`
- `window1X1_EP10GENglassTileable3Tile`
- `window2X1_EP10GENglassWideTileable3Tile`

Safe reading:

- the widened route is now narrower than a generic window-heavy fallback
- the strongest widened pair is `sliding2Tile` then `windowBox2Tile`
- curtains remain part of the widened route, but below the two full-bundle window anchors
- repeated `glass` naming alone is still weaker than exact companion-bundle evidence

## Current exact-root outcome

Current exact `set1` `ObjectDefinition` roots:

- `C0DB5AE7:00000000:000000000003D122`
- `C0DB5AE7:00000000:000000000003D55A`
- `C0DB5AE7:00000000:000000000003D568`
- `C0DB5AE7:00000000:000000000003D69B`

Current observed result:

- all four roots are now positively probeable as `Build/Buy Object` identity assets
- current `Support` is `Metadata`
- current `Snapshot` stays `SceneRoot=True, ExactGeom=False, Textures=False`
- current `Graph Diagnostics` recover the `ObjectDefinition` internal names plus raw and `swap32` reference candidates
- `ProbeAsset` now also resolves same-package `swap32` `Model` and `Footprint` references directly from the exact identity roots:
  - `window2X1_EP10GENsliding2Tile_set1 -> 01661233:00000000:05879178560EABDF`
  - `window2X1_EP10TRADwindowBox2Tile_set1 -> 01661233:00000000:970F358CFC9991D1`
- the two window anchors now survive to real scene builds from the promoted `Model` root:
  - `sliding2Tile -> Success=True | Status=Partial | Scene=BuildBuy_05879178560EABDF`
  - `windowBox2Tile -> Success=True | Status=Partial | Scene=BuildBuy_970F358CFC9991D1`
- the two curtain anchors now also survive to real scene builds from the promoted `Model` root:
  - `strawTileable2Tile -> Success=True | Status=Partial | Scene=BuildBuy_8FBB0B34229B82BD`
  - `norenShortTileable -> Success=True | Status=Partial | Scene=BuildBuy_9870E13047BE1D75`
- both window anchors currently reopen through embedded `MLOD` rather than indexed exact-instance `ModelLOD`
- both window anchors currently land on the same bounded material floor:
  - `Material coverage: StaticReady=4`
  - `Material families: Bloom=3, SkyDark=1`
  - `Material decode strategies: AlphaCutoutMaterialDecodeStrategy=1, DefaultMaterialDecodeStrategy=3`
- `windowBox2Tile` is slightly richer on visual payloads than `sliding2Tile`:
  - `sliding2Tile -> textured=4`
  - `windowBox2Tile -> textured=3, material-color=1`
- the curtain pair survives too, but with a thinner and more split floor:
  - `strawTileable2Tile -> StaticReady=1 | SeasonalFoliage=1 | SeasonalFoliageMaterialDecodeStrategy=1 | material-color=1`
  - `norenShortTileable -> StaticReady=1 | colorMap7=1 | AlphaCutoutMaterialDecodeStrategy=1 | textured=1`

Safe reading:

- the widened route is now stronger than both the old negative-control packet and the metadata-only exact-root packet
- the current blocker is no longer `Build/Buy asset not found`
- the current blocker is no longer best described as transformed-model timeout
- the current blocker is no longer best described as `ObjectDefinition -> Model` bridge failure for the two leading window anchors
- the current strongest live floor is now the full four-fixture widened route, not only the leading window pair
- the next remaining gap is family verdict closure across the widened quartet, not basic scene-root recovery

Current resource-mapping control:

- raw `ModelRef` forms from the decoded `ObjectDefinition` payload do not inspect successfully:
  - `01661233:00000000:560EABDF05879178`
  - `01661233:00000000:229B82BD8FBB0B34`
- matching `swap32` model-resource forms do inspect successfully as real `Model` resources:
  - `01661233:00000000:05879178560EABDF`
  - `01661233:00000000:8FBB0B34229B82BD`

Unsafe reading:

- do not treat browseable metadata support as the whole result anymore; the leading windows are already beyond that floor
- do not treat `Partial` scene survival as proof that object-glass, threshold/cutout, or `AlphaBlended` has already won
- do not treat `Bloom` or `SkyDark` family names by themselves as final transparency classification
- do not treat the current local `AlphaCutoutMaterialDecodeStrategy = 1` signal as universal closure for all four widened anchors
- do not treat `SeasonalFoliage` or `colorMap7` decode buckets as direct transparent-family truth without the external object-side branch order

## Exact target claim for this packet

- after the stalled transparent-decor route, the current workspace now has enough structural, external, exact identity-root, and live-probe evidence to replace the vague “window-heavy” widening note with one bounded window/curtain route where all four anchors now survive to real `Partial` fixtures, with the window pair still stronger than the curtain pair

## Best next step after this packet

1. Preserve the widened route in this order:
   - `window2X1_EP10GENsliding2Tile`
   - `window2X1_EP10TRADwindowBox2Tile`
   - `curtain1x1_EP10GENstrawTileable2Tile`
   - `curtain2x1_EP10GENnorenShortTileable`
2. Treat the exact `ObjectDefinition` roots as the real identity-entry lane for this widened route, not the transformed model roots:
   - `C0DB5AE7:00000000:000000000003D122`
   - `C0DB5AE7:00000000:000000000003D55A`
   - `C0DB5AE7:00000000:000000000003D568`
   - `C0DB5AE7:00000000:000000000003D69B`
3. Treat the `ObjectDefinition -> swap32 Model` bridge as currently strong enough for the full widened quartet and stop reopening that same bridge question by inertia.
4. Use the current quartet as the new bounded floor:
   - `sliding2Tile -> Partial | StaticReady=4 | Bloom=3 | SkyDark=1 | AlphaCutoutMaterialDecodeStrategy=1`
   - `windowBox2Tile -> Partial | StaticReady=4 | Bloom=3 | SkyDark=1 | AlphaCutoutMaterialDecodeStrategy=1`
   - `strawTileable2Tile -> Partial | StaticReady=1 | SeasonalFoliage=1 | SeasonalFoliageMaterialDecodeStrategy=1`
   - `norenShortTileable -> Partial | StaticReady=1 | colorMap7=1 | AlphaCutoutMaterialDecodeStrategy=1`
5. Keep family classification unchanged until one real verdict survives beyond the current bounded floor:
   - object-glass first
   - threshold/cutout second
   - `AlphaBlended` third
   - `SimGlass` only last-choice
6. Next honest packet inside this route:
   - compare the widened quartet against the frozen external object-side branch order instead of widening again

## Honest limit

What this packet proves:

- the next widening phase is no longer one generic window-heavy sweep
- the widened route now has a restart-safe internal order
- the real `Build/Buy` identity roots for the strongest widened anchors are now known
- the current `ProbeAsset` layer can now promote exact `ObjectDefinition` roots into same-package `swap32` `Model` roots for the full widened quartet
- the route now has four real `Partial` live fixtures with bounded material evidence instead of only route-ranking evidence

What remains open:

- the winning transparent-family branch for the widened route
- whether the current `AlphaCutout` hint on the surviving windows is the real family verdict or only one bounded decode bucket
- how to interpret the curtain-side `SeasonalFoliage` and `colorMap7` buckets without overreading them as transparent-family truth
- exact `ProbeAsset` concurrency behavior while multiple widened fixtures share the same sqlite probe-cache
