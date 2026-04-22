# Build/Buy Window-Curtain Family Verdict Boundary

This packet freezes the next transparent-object question after the widened `EP10` window/curtain quartet already survived to real `Partial` scenes.

Question:

- after the widened quartet reached a real live floor, is the next honest packet family-verdict closure rather than more widening?

Related docs:

- [Build/Buy Window-Curtain Widening Route](buildbuy-window-curtain-widening-route.md)
- [Build/Buy Transparent Object Classification Boundary](buildbuy-transparent-object-classification-boundary.md)
- [Build/Buy Transparent Object Classification Signals](../buildbuy-transparent-object-classification-signals.md)
- [Object Glass And Transparency](../family-sheets/object-glass-and-transparency.md)
- [Build/Buy Material Authority Matrix](../buildbuy-material-authority-matrix.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)

## Scope status (`v0.1`)

```text
Build/Buy Window-Curtain Family Verdict Boundary
├─ External object-side branch split ~ 93%
├─ Widened quartet live floor ~ 95%
├─ Safe verdict boundary ~ 89%
└─ Winning family closure ~ 41%
```

## What is already externally proved

What is already strong enough:

- `GlassForObjectsTranslucent` is a real object-side glass path, but the current creator-facing glass workflow does not treat it as the default answer for curtains
- threshold/cutout transparency is a separate object-side branch through `AlphaMap` plus `AlphaMaskThreshold`
- `AlphaBlended` is a separate creator-facing route for semi-transparent curtains
- windows, doors, and archways can also depend on structural cutout resources such as `Model Cutout` and `Cut Info Table`, which keeps openings narrower than one generic alpha-bearing family

External anchors:

- [Tutorial: how to make CC Cutout compatible with last update](https://s4cc.syboulette.fr/tutorial-how-to-make-cc-windows-doors-and-archways/)
- [Урок по созданию стеклянных объектов при помощи программы Sims 4 Studio](https://darasims.com/stati/tutorial/tutor_sims4/2980-urok-po-sozdaniyu-steklyannyh-obektov-pri-pomoschi-programmy-sims-4-studio.html)
- [Добавление объектам прозрачности, где нет параметра AlphaBlended, в Sims 4 Studio](https://darasims.com/stati/tutorial/tutor_sims4/3196-dobavlenie-obektam-prozrachnosti-gde-net-parametra-alphablended-v-sims-4-studio.html)
- [Подробный урок по созданию прозрачных штор в Симс 4](https://darasims.com/stati/tutorial/tutor_sims4/2984-sozdanie-prozrachnyh-shtor-v-sims-4.html)
- [Object Material Settings Cheat Sheet](https://staberindesims.wordpress.com/2021/06/05/object-material-settings-cheat-sheet/)

Safe reading:

- the safest external split for this quartet is four-way:
  - object glass
  - threshold/cutout transparency
  - blended curtain-like transparency
  - structural cutout/opening resources
- windows/openings and curtains should not be forced through the same object-side branch just because they all look transparent in game

## Current widened quartet

Current bounded snapshot:

- [buildbuy_window_curtain_widening_snapshot_2026-04-21.json](../../../tmp/buildbuy_window_curtain_widening_snapshot_2026-04-21.json)

Current live floor:

1. `window2X1_EP10GENsliding2Tile_set1`
   - identity root: `C0DB5AE7:00000000:000000000003D122`
   - promoted model root: `01661233:00000000:05879178560EABDF`
   - scene status: `Partial`
   - current local packet: `StaticReady=4`, `Bloom=3`, `SkyDark=1`, `AlphaCutoutMaterialDecodeStrategy=1`
2. `window2X1_EP10TRADwindowBox2Tile_set1`
   - identity root: `C0DB5AE7:00000000:000000000003D55A`
   - promoted model root: `01661233:00000000:970F358CFC9991D1`
   - scene status: `Partial`
   - current local packet: `StaticReady=4`, `Bloom=3`, `SkyDark=1`, `AlphaCutoutMaterialDecodeStrategy=1`
3. `curtain1x1_EP10GENstrawTileable2Tile_set1`
   - identity root: `C0DB5AE7:00000000:000000000003D568`
   - promoted model root: `01661233:00000000:8FBB0B34229B82BD`
   - scene status: `Partial`
   - current local packet: `StaticReady=1`, `SeasonalFoliage=1`, `SeasonalFoliageMaterialDecodeStrategy=1`
4. `curtain2x1_EP10GENnorenShortTileable_set1`
   - identity root: `C0DB5AE7:00000000:000000000003D69B`
   - promoted model root: `01661233:00000000:9870E13047BE1D75`
   - scene status: `Partial`
   - current local packet: `StaticReady=1`, `colorMap7=1`, `AlphaCutoutMaterialDecodeStrategy=1`

Safe reading:

- the widened route no longer needs more widening just to prove that it reaches real live fixtures
- the windows and curtains now form one bounded comparison floor, but not one closed family verdict
- the windows carry the strongest cutout/opening-side pressure because they already sit next to the external `Model Cutout` / `Cut Info Table` packet and the current local `AlphaCutout` decode hint
- the curtains carry the strongest blended-curtain pressure from external creator guidance, but the current local curtain pair is still mixed rather than verdict-grade

## Exact target claim for this packet

- the next honest transparent-object packet after the widened quartet is now family-verdict closure across the quartet, not more widening

## Safe verdict boundary

What is safe to say now:

- do not promote object-glass from the widened quartet yet:
  - external curtain guidance points away from the glass path
  - no current quartet fixture shows direct object-glass closure
- do not promote threshold/cutout from the whole quartet yet:
  - the window pair makes that branch materially stronger
  - the curtain pair does not currently converge on the same local packet
- do not promote `AlphaBlended` from the whole quartet yet:
  - external curtain guidance makes it the leading curtain-side hypothesis
  - the current local curtain packet is still split between `SeasonalFoliage...` and `AlphaCutout...`
- do not borrow `SimGlass`:
  - the external object-side split still points away from it as the default Build/Buy label

Implementation mistake this packet blocks:

- treating one surviving decode bucket or one transparent-looking object as enough to collapse windows, curtains, glass, cutouts, and blended transparency into one branch

## Best next step after this packet

1. Keep the widened quartet as the current verdict floor:
   - `sliding2Tile`
   - `windowBox2Tile`
   - `strawTileable2Tile`
   - `norenShortTileable`
2. Compare the quartet against the external branch order instead of widening again:
   - windows/openings: structural cutout/opening resources first, then material transparency path
   - curtains: `AlphaBlended` first, then threshold/cutout only if the object materially fails the blended lane
   - object glass only when a fixture actually shows that branch rather than merely looking transparent
3. Inspect actual material-entry evidence next for one strongest window and one strongest curtain before widening the route again.

## Honest limit

What this packet proves:

- the widened transparent-object route is now strong enough to stop widening by inertia
- the next restart-safe question is family-verdict closure across the quartet
- current external evidence keeps windows/openings and curtains under different leading hypotheses even inside the same widened route

What remains open:

- which branch actually wins for each widened fixture
- whether the window pair closes as structural cutout, threshold/cutout, or object glass after direct material-entry inspection
- whether the curtain pair closes as `AlphaBlended` after direct material-entry inspection
- exact TS4 authority order between cutout resources and material-entry decoding inside the surviving quartet
