# Build/Buy Window Structural-Cutout Verdict Floor

This packet checks whether the surviving `EP10` windows are now strong enough to carry a window-side family verdict as structural cutout/opening content instead of staying parked in a generic material-hint ambiguity.

Question:

- after same-instance `ModelCutout + CutoutInfoTable` closure, is the window-side verdict now safely structural cutout/opening first, with material cutout hints only as secondary evidence?

Related docs:

- [Build/Buy Window ModelCutout Companion Closure](buildbuy-window-modelcutout-companion-closure.md)
- [Build/Buy Window CutoutInfoTable Companion Floor](buildbuy-window-cutoutinfotable-companion-floor.md)
- [Build/Buy Window-Curtain Family Verdict Boundary](buildbuy-window-curtain-family-verdict-boundary.md)
- [Build/Buy Window-Curtain Strongest-Pair Material Divergence](buildbuy-window-curtain-strongest-pair-material-divergence.md)
- [Object Glass And Transparency](../family-sheets/object-glass-and-transparency.md)
- [Edge-Family Matrix](../edge-family-matrix.md)

## Scope status (`v0.1`)

```text
Build/Buy Window Structural-Cutout Verdict Floor
├─ External window/opening branch order ~ 95%
├─ Local full structural pair on surviving windows ~ 94%
├─ Direct material-hint boundary on surviving windows ~ 90%
└─ Window-side family verdict floor ~ 92%
```

## External rule that stays safe

What remains externally strong enough:

- windows, doors, and archways can close through structural opening resources such as `Model Cutout` and `Cut Info Table`
- those structural resources are a separate object-side branch from object glass
- material cutout helpers can coexist with that branch instead of replacing it

External anchor:

- [Tutorial: how to make CC Cutout compatible with last update](https://s4cc.syboulette.fr/tutorial-how-to-make-cc-windows-doors-and-archways/)

Safe reading:

- once the surviving windows carry the full structural pair and do not independently prove object-glass or curtain-like blended routing, the safest family verdict is the structural window/opening branch
- exact runtime precedence can remain open while the family verdict still moves forward

## Local verdict snapshot

Snapshot:

- [buildbuy_window_structural_cutout_snapshot_2026-04-21.json](../../../tmp/buildbuy_window_structural_cutout_snapshot_2026-04-21.json)

## Window-side evidence that now wins

Structural side:

- both surviving windows now carry same-instance `ModelCutout`
- both surviving windows now carry same-instance `CutoutInfoTable`
- both `CutoutInfoTable` entries carry `flags = 0x321`, including `IS_PORTAL` plus `USES_CUTOUT`
- both structural companions point to the exact promoted model-root instances rather than to only object metadata roots

Direct material side:

- the strongest current window-side material packet still survives only as:
  - `Partial`
  - `StaticReady=4`
  - `Bloom=3`
  - `SkyDark=1`
  - `AlphaCutoutMaterialDecodeStrategy=1`
- the strongest direct material packet for `sliding2Tile` still remains mostly opaque rather than closing as explicit object-glass or curtain-like blended transparency

Safe reading:

- the structural side is now the stronger family-defining signal
- the surviving material hints remain useful, but as secondary confirmation that the windows still lean cutout rather than as the main branch identity

## Exact claim this packet proves

- the surviving window pair can now be carried in the docs as the structural cutout/opening branch, with remaining material cutout hints treated as secondary evidence rather than as the unresolved main verdict

## Safe boundary after this packet

What is safe now:

- do treat the window-side branch as structurally closed enough for family-verdict language:
  - window/opening structural cutout companions first
  - material cutout hints second
- do not borrow the object-glass label:
  - no direct local evidence has promoted these windows into `GlassForObjectsTranslucent`
- do not borrow the curtain-side route:
  - the strongest window material packet does not close as the same transparent path already seen on `norenShortTileable`

Implementation mistake this packet blocks:

- leaving the window-side family verdict artificially open after the full structural companion pair is already explicit and stronger than the surviving material-only hints

## Best next step

1. Keep the window-side verdict fixed as structural cutout/opening content with secondary material cutout hints.
2. Move the next live proof to the curtain side:
   - `norenShortTileable`
   - `strawTileable2Tile`
3. Check whether the curtain side closes through explicit `AlphaBlended` or only through weaker threshold/cutout routing.
4. Then finish the quartet-level family verdict without reopening the window-side branch.

## Honest limit

What this packet proves:

- the window-side family verdict no longer needs to stay open between “structural” and “material-hint only”
- structural cutout/opening is now the safest verdict floor on the surviving window pair

What remains open:

- exact runtime authority order between the structural pair and `AlphaCutout` material decoding
- which exact curtain-side route wins on the surviving curtain pair
