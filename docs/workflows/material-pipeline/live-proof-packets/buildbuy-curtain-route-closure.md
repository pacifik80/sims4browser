# Build/Buy Curtain Route Closure

This packet checks whether the surviving `EP10` curtain pair closes through explicit `AlphaBlended` or only through a weaker threshold/cutout route after the window-side verdict has already been fixed.

Question:

- do the surviving curtain anchors close through explicit `AlphaBlended`, or is the safest current curtain-side verdict only a weaker threshold/cutout route?

Related docs:

- [Build/Buy Window Structural-Cutout Verdict Floor](buildbuy-window-structural-cutout-verdict-floor.md)
- [Build/Buy Window-Curtain Strongest-Pair Material Divergence](buildbuy-window-curtain-strongest-pair-material-divergence.md)
- [Object Glass And Transparency](../family-sheets/object-glass-and-transparency.md)
- [Object Transparency Evidence Ledger](../object-transparency-evidence-ledger.md)
- [Edge-Family Matrix](../edge-family-matrix.md)

## Scope status (`v0.1`)

```text
Build/Buy Curtain Route Closure
├─ External curtain branch order ~ 91%
├─ Noren direct-material route ~ 92%
├─ Straw negative curtain control ~ 90%
└─ Curtain-side verdict floor ~ 89%
```

## External rule that stays safe

What remains externally strong enough:

- curtains should only be carried as `AlphaBlended` when that route is explicit
- ordinary object transparency tutorials still distinguish hole/cutout transparency from gauze-like or glass-like transparency
- object glass remains separate from both

External anchors:

- [Object Material Settings Cheat Sheet](https://staberindesims.wordpress.com/2021/06/05/object-material-settings-cheat-sheet/)
- [Tutorial: how to make CC Cutout compatible with last update](https://s4cc.syboulette.fr/tutorial-how-to-make-cc-windows-doors-and-archways/)

Safe reading:

- if a curtain fixture does not locally close as explicit `AlphaBlended`, the safer current verdict is the weaker threshold/cutout route
- that verdict can stay narrower than exact engine precedence

## Local curtain snapshot

Snapshot:

- [buildbuy_curtain_route_snapshot_2026-04-21.json](../../../tmp/buildbuy_curtain_route_snapshot_2026-04-21.json)
- [curtain_sameinstance_companion_listing_2026-04-21.txt](../../../tmp/curtain_sameinstance_companion_listing_2026-04-21.txt)

## Strongest curtain

Fixture:

- `curtain2x1_EP10GENnorenShortTileable_set1`
- object root: `C0DB5AE7:00000000:000000000003D69B`
- promoted model root: `01661233:00000000:9870E13047BE1D75`

Local proof:

- [probe_curtain_noren_set1_probe_seq.txt](../../../tmp/probe_curtain_noren_set1_probe_seq.txt) closes the strongest current curtain material packet as:
  - `transparent=True`
  - `alpha=alpha-test-or-blend`
  - `textures=2`
  - `shaderProfile=colorMap7`
  - `AlphaCutoutMaterialDecodeStrategy=1`
- [probe_curtain_noren_set1_probejson_seq.txt](../../../tmp/probe_curtain_noren_set1_probejson_seq.txt) keeps the same narrow summary:
  - `SceneStatus=Partial`
  - `MaterialFamilies.colorMap7=1`
  - `MaterialStrategies.AlphaCutoutMaterialDecodeStrategy=1`
- [curtain_sameinstance_companion_listing_2026-04-21.txt](../../../tmp/curtain_sameinstance_companion_listing_2026-04-21.txt) shows only:
  - `CutoutInfoTable`
  - `Footprint`
  - no same-instance `ModelCutout`
- [inspect_cutoutinfo_noren_2026-04-21.txt](../../../tmp/inspect_cutoutinfo_noren_2026-04-21.txt) shows a weak `CutoutInfoTable` packet:
  - `baseFileNameHash = 0`
  - `widthAndMappingFlags = 0x00`
  - `minimumWallHeight = 0`
  - `numberOfLevels = 0`
  - `flags = 0x00000000`

Safe reading:

- the strongest curtain does survive as a transparent material packet
- but the surviving route is still named by cutout-side local signals rather than by explicit `AlphaBlended`

## Weaker curtain control

Fixture:

- `curtain1x1_EP10GENstrawTileable2Tile_set1`
- object root: `C0DB5AE7:00000000:000000000003D568`
- promoted model root: `01661233:00000000:8FBB0B34229B82BD`

Local proof:

- [probe_curtain_straw_set1_probe_seq.txt](../../../tmp/probe_curtain_straw_set1_probe_seq.txt) does not close a transparent packet:
  - `transparent=False`
  - `alpha=opaque`
  - `textures=1`
  - selected portable profile = `SeasonalFoliage`
- [probe_curtain_straw_set1_probejson_seq.txt](../../../tmp/probe_curtain_straw_set1_probejson_seq.txt) keeps that weaker summary:
  - `SceneStatus=Partial`
  - `MaterialFamilies.SeasonalFoliage=1`
  - `MaterialStrategies.SeasonalFoliageMaterialDecodeStrategy=1`
- [curtain_sameinstance_companion_listing_2026-04-21.txt](../../../tmp/curtain_sameinstance_companion_listing_2026-04-21.txt) again shows:
  - `CutoutInfoTable`
  - `Footprint`
  - no same-instance `ModelCutout`
- [inspect_cutoutinfo_straw_2026-04-21.txt](../../../tmp/inspect_cutoutinfo_straw_2026-04-21.txt) keeps only a minimal `CutoutInfoTable`:
  - `baseFileNameHash = 0`
  - `widthAndMappingFlags = 0x00`
  - `minimumWallHeight = 0`
  - `numberOfLevels = 0`
  - `flags = 0x00000001`
  - decoded flag floor: `USES_INSTANCED_SHADER`

Safe reading:

- the weaker curtain does not promote the branch into explicit `AlphaBlended`
- it acts as a control against overreading the stronger `noren` material packet

## Exact claim this packet proves

- the surviving curtain pair does not currently close through explicit `AlphaBlended`
- the safest current curtain-side verdict is the weaker threshold/cutout route

## Safe boundary after this packet

What is safe now:

- do treat the curtain side as weaker threshold/cutout first
- do not promote explicit `AlphaBlended` from absence-based wishful reading
- do not borrow the window/opening structural verdict:
  - the curtains do not carry the same-instance `ModelCutout + CutoutInfoTable` pair that now closes the windows
- do not borrow object glass:
  - no local fixture here has promoted `GlassForObjectsTranslucent`

Implementation mistake this packet blocks:

- forcing the curtain side into explicit `AlphaBlended` when the surviving local packet still closes only as cutout-leaning transparency and the weaker control does not even stay transparent

## Best next step

1. Keep the curtain side fixed as weaker threshold/cutout, not explicit `AlphaBlended`.
2. Combine that result with the closed window-side verdict.
3. Freeze the quartet family split without reopening the same window or curtain question.

## Honest limit

What this packet proves:

- `norenShortTileable` survives through a transparent alpha-test-or-blend packet with `AlphaCutoutMaterialDecodeStrategy`
- `strawTileable2Tile` does not survive through an explicit transparent packet
- neither surviving curtain closes as explicit `AlphaBlended`

What remains open:

- exact runtime precedence between the weak curtain `CutoutInfoTable` packet and the surviving material packet
- whether a stronger future curtain fixture elsewhere in the corpus will promote explicit `AlphaBlended`
