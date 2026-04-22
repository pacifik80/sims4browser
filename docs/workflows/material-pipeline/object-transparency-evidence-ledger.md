# Object Transparency Evidence Ledger

This ledger separates four layers for the current object-side transparency packet:

1. externally confirmed
2. local game-package evidence
3. bounded Codex synthesis
4. still-open questions

Related docs:

- [Object Glass And Transparency](family-sheets/object-glass-and-transparency.md)
- [Build/Buy Transparent Object Classification Signals](buildbuy-transparent-object-classification-signals.md)
- [Build/Buy Transparent Object Authority Order](buildbuy-transparent-object-authority-order.md)
- [Edge-Family Matrix](edge-family-matrix.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
Object Transparency Evidence Ledger
├─ External confirmation separation ~ 95%
├─ Local package-evidence separation ~ 93%
├─ Codex synthesis boundary ~ 91%
└─ Remaining open questions ~ 87%
```

## Ledger

### 1. Externally confirmed

These are directly supported by creator-facing or lineage sources:

- `GlassForObjectsTranslucent` is a real object-side shader family used through object material entries.
  - Sources:
    - [DaraSims glass-object tutorial](https://darasims.com/stati/tutorial/tutor_sims4/2980-urok-po-sozdaniyu-steklyannyh-obektov-pri-pomoschi-programmy-sims-4-studio.html)
    - [Sims_3:Shaders](https://modthesims.info/wiki.php?title=Sims_3%3AShaders)
  - Evidence:
    - creators are instructed to set object shader entries to `GlassForObjectsTranslucent`
    - lineage shader docs preserve `GlassForObjectsTranslucent` as a separate object-side family with its own parameter packet

- threshold/cutout transparency is a separate object-side branch from object glass.
  - Sources:
    - [DaraSims object transparency without `AlphaBlended`](https://darasims.com/stati/tutorial/tutor_sims4/3196-dobavlenie-obektam-prozrachnosti-gde-net-parametra-alphablended-v-sims-4-studio.html)
    - [Object Material Settings Cheat Sheet](https://staberindesims.wordpress.com/2021/06/05/object-material-settings-cheat-sheet/)
    - [Tutorial: how to make CC Cutout compatible with last update](https://s4cc.syboulette.fr/tutorial-how-to-make-cc-windows-doors-and-archways/)
  - Evidence:
    - creators are told to use `AlphaMap` plus `AlphaMaskThreshold`
    - `AlphaThresholdMask` is treated as separate from object-glass behavior
    - windows/doors/archways can also require `Model Cutout` and `Cut Info Table`, which keeps architectural cutouts as explicit object-side structure rather than generic alpha prose

- `AlphaBlended` is a further object-side transparency path, not merely another name for threshold/cutout or glass.
  - Sources:
    - [DaraSims transparent-curtain tutorial](https://darasims.com/stati/tutorial/tutor_sims4/2984-sozdanie-prozrachnyh-shtor-v-sims-4.html)
    - [Object Material Settings Cheat Sheet](https://staberindesims.wordpress.com/2021/06/05/object-material-settings-cheat-sheet/)
  - Evidence:
    - semi-transparent object surfaces are described through an `AlphaBlended` path, distinct from cutout and glass workflows

### 2. Local game-package evidence

These are not semantic truth claims. They are local evidence for candidate selection and fixture ordering:

- the transparent-decor cluster in the current `EP10` survey is a good object-side candidate packet:
  - `displayShelf`
  - `shopDisplayTileable`
  - `mirror`
  - `lantern`
  - `fishBowl`

- those rows are useful because they preserve transformed roots and companion-bundle evidence in the current local survey and candidate-resolution files.

- this local packet does not prove which transparency family wins.
- after the stalled decor route, the current best widened packet is no longer one vague window-heavy sweep:
  - `window2X1_EP10GENsliding2Tile`
  - `window2X1_EP10TRADwindowBox2Tile`
  - `curtain1x1_EP10GENstrawTileable2Tile`
  - `curtain2x1_EP10GENnorenShortTileable`
- exact `set1` `ObjectDefinition` roots on that widened route are now positively probeable `Build/Buy Object` identity assets:
  - `C0DB5AE7:00000000:000000000003D122`
  - `C0DB5AE7:00000000:000000000003D55A`
  - `C0DB5AE7:00000000:000000000003D568`
  - `C0DB5AE7:00000000:000000000003D69B`
- those probes recover internal names and raw versus `swap32` reference candidates
- the two leading window anchors now also resolve same-package `swap32` `Model` roots and survive to real `Partial` scenes:
  - `sliding2Tile -> StaticReady=4 | Bloom=3 | SkyDark=1 | AlphaCutoutMaterialDecodeStrategy=1`
  - `windowBox2Tile -> StaticReady=4 | Bloom=3 | SkyDark=1 | AlphaCutoutMaterialDecodeStrategy=1`
- the curtain pair now also resolves same-package `swap32` `Model` roots and survives to real `Partial` scenes:
  - `strawTileable2Tile -> StaticReady=1 | SeasonalFoliage=1 | SeasonalFoliageMaterialDecodeStrategy=1`
  - `norenShortTileable -> StaticReady=1 | colorMap7=1 | AlphaCutoutMaterialDecodeStrategy=1`
- the strongest direct material-entry pair is now explicitly narrowed:
  - `sliding2Tile` keeps the strongest current window-side exact root and resolves through `MTST` into mostly opaque default-state material packets with one cutout-side `SkyDark` pass
  - `norenShortTileable` is the strongest current curtain-side direct packet and already reaches `transparent=True` with `alpha=alpha-test-or-blend`, `textures=2`, `colorMap7`, and `AlphaCutoutMaterialDecodeStrategy`
- the surviving window pair now also has explicit structural companions:
  - `sliding2Tile -> CutoutInfoTable 81CA1A10:00000000:05879178560EABDF`
  - `windowBox2Tile -> CutoutInfoTable 81CA1A10:00000000:970F358CFC9991D1`
  - both `CutoutInfoTable` entries carry `flags=0x321` with `IS_PORTAL` plus `USES_CUTOUT`
- the surviving window pair now has the full same-instance structural pair:
  - `sliding2Tile -> ModelCutout 07576A17:00000000:05879178560EABDF`
  - `windowBox2Tile -> ModelCutout 07576A17:00000000:970F358CFC9991D1`
  - both `ModelCutout` resources sit beside the matching `CutoutInfoTable` on the exact promoted model roots
- the surviving curtain pair now also has bounded same-instance companion evidence:
  - both preserve same-instance `CutoutInfoTable`
  - neither preserves same-instance `ModelCutout`
  - `norenShortTileable` `CutoutInfoTable` stays weak with `flags=0x00000000`
  - `strawTileable2Tile` `CutoutInfoTable` stays weak with `flags=0x00000001` and only `USES_INSTANCED_SHADER`
- the surviving curtain pair now also has bounded direct-material closure:
  - `norenShortTileable` survives only through `transparent=True`, `alpha=alpha-test-or-blend`, `textures=2`, `colorMap7`, and `AlphaCutoutMaterialDecodeStrategy`
  - `strawTileable2Tile` stays `transparent=False`, `alpha=opaque`, `textures=1`, under `SeasonalFoliage`
- asset summaries still surface as `Metadata` with `ExactGeom=False` and `Textures=False`, so the widened route now has a stronger live floor than the summary layer alone suggests
- raw `ModelRef` forms from the decoded `ObjectDefinition` payload do not inspect successfully, while the matching `swap32` model forms do

### 3. Bounded Codex synthesis

These are synthesis from the evidence above. They are not direct source claims:

- object-side transparent fixtures should currently be classified in this order:
  - object-glass
  - threshold/cutout
  - `AlphaBlended`
  - only then last-choice `SimGlass`

- local package data should only choose the next fixture and support the restart-safe route.
- after the exhausted decor route, the next restart-safe route is a bounded window/curtain widening packet rather than a generic return to any object whose name contains `glass`
- the current widened-route blocker is no longer best described as transformed-root timeout
- the current widened-route blocker is also no longer best described as an `ObjectDefinition -> geometry` bridge gap for the two leading windows
- the strongest current open gap is now narrower than the quartet itself:
  - the strongest pair already blocks one shared family verdict
  - the surviving windows now already close through explicit same-instance `ModelCutout + CutoutInfoTable`
  - the surviving curtains now close only as weaker threshold/cutout, not explicit `AlphaBlended`
  - the widened quartet can now stay frozen as windows -> structural cutout/opening and curtains -> weaker threshold/cutout

- object transparency should not be flattened into one universal alpha family and should not borrow character-side `SimGlass` by default.

Reason this synthesis is bounded:

- it combines creator-facing external sources with local survey evidence
- it does not claim exact TS4 slot closure for the winning object-side branch

### 4. Still open

These are not confirmed yet:

- exact TS4 slot and param closure for the winning object-side branch
- exact runtime authority order between the structural companion pair and `AlphaCutout` material decoding on the windows
- whether later live fixtures will force a refinement of the current decision order
- whether a later stronger object-glass or explicit `AlphaBlended` fixture elsewhere should reopen the frozen quartet split
- whether the sqlite-backed probe-cache should stay single-run only for bounded transparent-object packets, or whether that concurrency limit belongs in the tooling docs rather than the family docs
