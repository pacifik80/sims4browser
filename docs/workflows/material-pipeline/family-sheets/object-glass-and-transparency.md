# Object Glass And Transparency

This sheet isolates the narrow object-side transparency seam that is easy to misread as either character-side `SimGlass` or one generic alpha fallback.

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Build/Buy Transparent Object Classification Signals](../buildbuy-transparent-object-classification-signals.md)
- [Family Sheets](README.md)
- [Shader Family Registry](../shader-family-registry.md)
- [Build/Buy Material Authority Matrix](../buildbuy-material-authority-matrix.md)
- [Edge-Family Matrix](../edge-family-matrix.md)
- [Object Transparency Evidence Ledger](../object-transparency-evidence-ledger.md)
- [Open questions](../../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Scope status (`v0.1`)

```text
Object Glass And Transparency
├─ Object-side glass family identity packet ~ 84%
├─ Cutout and threshold transparency packet ~ 87%
├─ SimGlass separation boundary ~ 90%
└─ Exact TS4 live-family slot closure ~ 53%
```

## Evidence order

Use this seam in the following order:

1. creator-facing object tutorials that name the object-side shader or material fields directly
2. Sims-lineage shader vocabulary that preserves object-glass families and their parameter packet
3. general Sims 4 Studio object-material guidance
4. local game-package fixtures only as evidence for where to test the already-proved branch next
5. current repo behavior only as implementation boundary

Current local boundary update:

- the widened EP10 window/curtain route is now anchored by exact `ObjectDefinition` identity roots rather than only by transformed model-root timeout behavior
- the full widened quartet on that route now survives to `Partial` scene builds through same-package `swap32` `Model` promotion plus embedded `MLOD`
- current widened-route floor is stronger but still bounded:
  - windows: `StaticReady=4`, `Bloom=3`, `SkyDark=1`, `AlphaCutoutMaterialDecodeStrategy=1`
  - curtains: `StaticReady=1`, with one `SeasonalFoliage...` bucket and one `AlphaCutout...` bucket
- the strongest current direct material pair is now narrower than the quartet summary:
  - strongest window: `sliding2Tile` still reads mostly as opaque/default-state material handling with one cutout-side `SkyDark` pass
  - strongest curtain: `norenShortTileable` already reaches `transparent=True` through `colorMap7` plus `alpha-test-or-blend`
- the surviving window pair now also carries explicit same-instance `CutoutInfoTable` companions:
  - `sliding2Tile -> 81CA1A10:00000000:05879178560EABDF`
  - `windowBox2Tile -> 81CA1A10:00000000:970F358CFC9991D1`
  - both entries point back to the exact model root and carry `flags=0x321` with `IS_PORTAL` plus `USES_CUTOUT`
- the surviving curtain pair now also has bounded local closure:
  - neither curtain currently closes through explicit `AlphaBlended`
  - both curtains preserve same-instance `CutoutInfoTable`, but neither preserves same-instance `ModelCutout`
  - `norenShortTileable` survives only through cutout-leaning transparency
  - `strawTileable2Tile` stays opaque as the negative control
- the widened quartet is now frozen as:
  - windows -> structural cutout/opening first
  - curtains -> weaker threshold/cutout
  - object glass not selected
- this sheet still does not claim a winning live object-glass branch

## Externally proved packet

### `GlassForObjectsTranslucent`

Strongest evidence:

- [DaraSims glass-object tutorial](https://darasims.com/stati/tutorial/tutor_sims4/2980-urok-po-sozdaniyu-steklyannyh-obektov-pri-pomoschi-programmy-sims-4-studio.html) instructs creators to change object `Shader` entries to `GlassForObjectsTranslucent` in the `Model LOD` material entries
- [Sims_3:Shaders](https://modthesims.info/wiki.php?title=Sims_3%3AShaders) keeps `GlassForObjectsTranslucent` as a distinct object-side shader family with its own parameter packet including `AlphaMap`, `AlphaMaskThreshold`, `RefractionDistortionScale`, `Transparency`, and specular or normal helpers
- [Object Material Settings Cheat Sheet](https://staberindesims.wordpress.com/2021/06/05/object-material-settings-cheat-sheet/) reinforces the practical creator rule that object material behavior is set in Sims 4 Studio rather than in Blender and must be validated in-game rather than from Studio preview alone

Evidence labeling for this packet:

- externally confirmed versus local package evidence versus bounded synthesis now also lives in [Object Transparency Evidence Ledger](../object-transparency-evidence-ledger.md)

Safe reading:

- object-side glass is a real material-family branch for objects
- it should not be collapsed into character-side `SimGlass`
- it should also not be collapsed into one generic “alpha” story
- once the object-side authority chain chooses this family, it still flows into the same shared shader or material contract as the rest of the project

Unsafe reading:

- do not assume every transparent object uses `GlassForObjectsTranslucent`
- do not assume the Sims 3 lineage page alone closes the exact TS4 runtime slot contract

### Cutout and threshold transparency

Strongest evidence:

- [DaraSims transparency tutorial for objects without `AlphaBlended`](https://darasims.com/stati/tutorial/tutor_sims4/3196-dobavlenie-obektam-prozrachnosti-gde-net-parametra-alphablended-v-sims-4-studio.html) tells creators to add `AlphaMap` and `AlphaMaskThreshold`, and explicitly warns that a positive Studio preview can still fail in-game
- [DaraSims transparent-curtain tutorial](https://darasims.com/stati/tutorial/tutor_sims4/2984-sozdanie-prozrachnyh-shtor-v-sims-4.html) treats `AlphaBlended` as a separate object-side path for semi-transparent curtain content
- [Object Material Settings Cheat Sheet](https://staberindesims.wordpress.com/2021/06/05/object-material-settings-cheat-sheet/) explicitly distinguishes ordinary transparency used for holes or invisible texture sections from glass-like behavior and uses `AlphaThresholdMask`
- [Tutorial: how to make CC Cutout compatible with last update](https://s4cc.syboulette.fr/tutorial-how-to-make-cc-windows-doors-and-archways/) keeps `Model Cutout` and `Cut Info Table` explicit for windows, doors, and archways and also keeps diagonal-wall variants as separate object-side cutout work rather than plain texture alpha

Safe reading:

- cutout or threshold transparency is a separate object-side branch from glass shaders
- `AlphaMap`, `AlphaMaskThreshold`, and `AlphaThresholdMask` are not proof of `SimGlass`
- `AlphaBlended` is a further object-side transparency path and should not be silently merged into object-glass or character-glass semantics
- transparent architectural objects can also depend on object-side cutout resources such as `Model Cutout` and `Cut Info Table`, not only on alpha-bearing textures

Unsafe reading:

- do not treat every object that uses alpha-bearing textures as “glass”
- do not flatten `GlassForObjectsTranslucent`, `AlphaBlended`, and threshold or cutout transparency into one universal transparency family

## Separation table

| Branch | Strongest external signal | Current safe reading |
| --- | --- | --- |
| `SimGlass` | `TS4SimRipper` enum plus separate preview/export handling | narrow character-side GEOM family |
| `GlassForObjectsTranslucent` | creator object-glass workflow plus Sims-lineage shader page | object-side glass material family |
| `AlphaMap` + `AlphaMaskThreshold` / `AlphaThresholdMask` | creator cutout/transparency tutorials and object-material notes | object-side threshold or cutout transparency helper path |
| `AlphaBlended` | creator curtain and semi-transparent object workflows | object-side blended transparency path |

## Classification companion

For reopened `Build/Buy` fixtures, the current signal-level decision table now lives in:

- [Build/Buy Transparent Object Classification Signals](../buildbuy-transparent-object-classification-signals.md)

Safe reading:

- this sheet freezes the semantic split
- the companion doc freezes the current decision signals used after a stable reopen

## Current repo boundary

Current repo behavior is useful only as an implementation boundary:

- it may currently flatten some object-side transparency families too aggressively
- local Build/Buy survey data may help choose the next object fixture
- local probe tooling now proves that exact `ObjectDefinition` roots can promote into same-package `swap32` `Model` roots for the leading widened-window fixtures
- neither of those should be promoted into semantic truth

## Open questions

- which live `Build/Buy` fixtures most cleanly reopen the object-side glass family rather than only generic alpha-bearing objects
- exact TS4 slot and param closure for object-glass families after `Build/Buy` authority selection reaches the relevant `MATD` or `MTST`
- how often live object families use `GlassForObjectsTranslucent` versus threshold or blended transparency routes
- whether a later stronger object fixture elsewhere should reopen the frozen quartet split with explicit `AlphaBlended` or object-glass evidence

## Recommended next work

1. Keep `SimGlass` and object-side glass as separate family rows in the docs.
2. Keep the widened window/curtain quartet frozen as the current named fixture floor:
   - windows -> structural cutout/opening first
   - curtains -> weaker threshold/cutout
   - object glass remains unselected on this quartet
3. Treat the surviving windows as a real structural companion floor, not only as a material-hint floor:
   - explicit `CutoutInfoTable` proof now exists on both window models
   - explicit same-instance `ModelCutout` proof now exists on both window models too
4. Treat the surviving windows as a window/opening structural-cutout verdict floor:
   - structural cutout companions first
   - material cutout hints second
5. Treat the surviving curtains as a bounded weaker threshold/cutout floor unless a later fixture proves explicit `AlphaBlended`.
6. Reopen this row only when a later fixture can honestly challenge the frozen quartet with stronger object-glass or explicit `AlphaBlended` evidence.
7. Hand the next autonomous batch to the next unfinished Tier A lane instead of deepening this same quartet again.
8. Keep probe runs sequential while the sqlite probe-cache remains shared, and treat that as tooling boundary rather than family truth.
