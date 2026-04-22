# Build/Buy Window-Curtain Strongest-Pair Material Divergence

This packet inspects the strongest direct material-entry pair inside the widened `EP10` window/curtain quartet.

Question:

- after direct material-entry inspection of one strongest window and one strongest curtain, do they already diverge enough to block a single quartet-wide family verdict?

Related docs:

- [Build/Buy Window-Curtain Family Verdict Boundary](buildbuy-window-curtain-family-verdict-boundary.md)
- [Build/Buy Window-Curtain Widening Route](buildbuy-window-curtain-widening-route.md)
- [Object Glass And Transparency](../family-sheets/object-glass-and-transparency.md)
- [Build/Buy Material Authority Matrix](../buildbuy-material-authority-matrix.md)
- [Object Transparency Evidence Ledger](../object-transparency-evidence-ledger.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)

## Scope status (`v0.1`)

```text
Build/Buy Window-Curtain Strongest-Pair Material Divergence
├─ External branch order recheck ~ 94%
├─ Strongest window direct packet ~ 90%
├─ Strongest curtain direct packet ~ 92%
└─ Shared-family collapse blocked ~ 88%
```

## External order that stays safe

What remains externally strong enough:

- windows, doors, and archways keep structural opening resources explicit through `Model Cutout` and `Cut Info Table`
- threshold/cutout transparency remains a separate object-side route through `AlphaMap` plus `AlphaMaskThreshold` / `AlphaThresholdMask`
- semi-transparent curtain workflows treat `AlphaBlended` as a separate object-side route
- `GlassForObjectsTranslucent` is real, but it is not the default answer for curtains and should not be borrowed just because an object looks transparent

External anchors:

- [Tutorial: how to make CC Cutout compatible with last update](https://s4cc.syboulette.fr/tutorial-how-to-make-cc-windows-doors-and-archways/)
- [Подробный урок по созданию прозрачных штор в Симс 4](https://darasims.com/stati/tutorial/tutor_sims4/2984-sozdanie-prozrachnyh-shtor-v-sims-4.html)
- [Добавление объектам прозрачности, где нет параметра AlphaBlended, в Sims 4 Studio](https://darasims.com/stati/tutorial/tutor_sims4/3196-dobavlenie-obektam-prozrachnosti-gde-net-parametra-alphablended-v-sims-4-studio.html)
- [Создание стеклянных объектов при помощи программы Sims 4 Studio](https://darasims.com/stati/tutorial/tutor_sims4/2980-urok-po-sozdaniyu-steklyannyh-obektov-pri-pomoschi-programmy-sims-4-studio.html)
- [Object Material Settings Cheat Sheet](https://staberindesims.wordpress.com/2021/06/05/object-material-settings-cheat-sheet/)

Safe reading:

- window/opening fixtures still lead with structural cutout pressure before a generic transparent-family verdict
- curtain fixtures still lead with blended-curtain pressure when `AlphaBlended` is explicit, but can still survive through threshold/cutout-style material packets
- object glass remains a separate branch from both of those

## Strongest inspected window

Fixture:

- `window2X1_EP10GENsliding2Tile_set1`
- identity root: `C0DB5AE7:00000000:000000000003D122`
- promoted model root: `01661233:00000000:05879178560EABDF`
- local artifacts:
  - [buildbuy_window_curtain_strongest_pair_snapshot_2026-04-21.json](../../../tmp/buildbuy_window_curtain_strongest_pair_snapshot_2026-04-21.json)
  - [probe_window_sliding_set1_objectdef.txt](../../../tmp/probe_window_sliding_set1_objectdef.txt)
  - [probe_window_sliding_set1_probejson_seq_unblocked.txt](../../../tmp/probe_window_sliding_set1_probejson_seq_unblocked.txt)
  - [probe_window_sliding_set1_probe_seq_unblocked.txt](../../../tmp/probe_window_sliding_set1_probe_seq_unblocked.txt)

What the local packet now says:

- `SceneStatus = Partial`
- `MaterialCoverage = StaticReady=4`
- `MaterialFamilies = Bloom=3, SkyDark=1`
- `MaterialStrategies = AlphaCutoutMaterialDecodeStrategy=1, DefaultMaterialDecodeStrategy=3`
- two `MTST` chunks survive on the promoted model root
- the resolved `MTST -> MATD` states are dominated by `HiliteTerrainHighWithBlendedPaint`
- the selected preview materials stay `alpha=opaque transparent=False` even when the packet retains one cutout-side strategy hit

Safe reading:

- this is the strongest current window-side direct packet because it stays fully textured at the scene summary layer and keeps the cleanest exact root
- it does not currently close as object glass
- it also does not yet behave like the strongest curtain-side direct transparency packet
- the surviving local pressure is still window-opening/cutout pressure:
  - one cutout-side `SkyDark` pass survives
  - the broader direct material packet still reads mostly as opaque/default-state material-set handling

## Strongest inspected curtain

Fixture:

- `curtain2x1_EP10GENnorenShortTileable_set1`
- identity root: `C0DB5AE7:00000000:000000000003D69B`
- promoted model root: `01661233:00000000:9870E13047BE1D75`
- local artifacts:
  - [buildbuy_window_curtain_strongest_pair_snapshot_2026-04-21.json](../../../tmp/buildbuy_window_curtain_strongest_pair_snapshot_2026-04-21.json)
  - [probe_curtain_noren_set1_objectdef.txt](../../../tmp/probe_curtain_noren_set1_objectdef.txt)
  - [probe_curtain_noren_set1_probejson_seq.txt](../../../tmp/probe_curtain_noren_set1_probejson_seq.txt)
  - [probe_curtain_noren_set1_probe_seq.txt](../../../tmp/probe_curtain_noren_set1_probe_seq.txt)

What the local packet now says:

- `SceneStatus = Partial`
- `MaterialCoverage = StaticReady=1`
- `MaterialFamilies = colorMap7=1`
- `MaterialStrategies = AlphaCutoutMaterialDecodeStrategy=1`
- one `MTST` chunk survives on the promoted model root
- every resolved `MTST -> MATD` state in the current packet stays on `colorMap7`
- the surviving resolved material is `alpha=alpha-test-or-blend transparent=True`
- the selected material keeps `textures=2`, and the `MTST` state variants change portable shader properties rather than only inert preview metadata

Safe reading:

- this is the strongest current curtain-side direct packet inside the quartet
- it is materially stronger than the straw curtain for direct transparency because it reaches `transparent=True` rather than only a family decode label
- it still does not prove explicit `AlphaBlended` field closure
- it does prove that the strongest curtain-side route is already much closer to a direct transparency material packet than the strongest window-side route

## Exact claim this packet proves

- the strongest direct material-entry window and the strongest direct material-entry curtain already diverge enough to block a single quartet-wide family label

## Safe boundary after this packet

What is safe now:

- do not collapse the widened quartet into one family:
  - the strongest window and strongest curtain no longer look alike at the direct material-entry layer
- do not promote object glass from the strongest pair:
  - neither side closes there
- do not promote quartet-wide `AlphaBlended`:
  - the strongest curtain still closes locally through `colorMap7` plus `alpha-test-or-blend` / `AlphaCutout` strategy rather than explicit `AlphaBlended`
- do promote one stronger local distinction:
  - window-side strongest pair remains opening/cutout pressured
  - curtain-side strongest pair now has the strongest direct material transparency packet in the widened route

Implementation mistake this packet blocks:

- using the widened quartet as if one winning family already applied equally to windows and curtains after only scene-summary survival

## Best next step

1. Keep `sliding2Tile` as the strongest window-side anchor.
2. Keep `norenShortTileable` as the strongest curtain-side anchor.
3. Continue from [Build/Buy Window CutoutInfoTable Companion Floor](buildbuy-window-cutoutinfotable-companion-floor.md), because the surviving window pair now already proves explicit `CutoutInfoTable` companions.
4. Only reopen explicit `AlphaBlended` curtain closure if the direct curtain-side packet later fails to stay stable.

## Honest limit

What this packet proves:

- the widened quartet should now be read as one live comparison floor with internally diverging branches
- the strongest current curtain-side packet is materially more transparent than the strongest current window-side packet
- the strongest current window-side packet still does not outrun the external opening/cutout hypothesis

What remains open:

- matching `ModelCutout` closure on the strongest windows
- whether a later curtain fixture or deeper field-level inspection closes explicit `AlphaBlended`
- exact TS4 authority order between architectural cutout helpers and material-entry decoding on the window side
