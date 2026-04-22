# P1 Live-Proof Queue

This document is the working queue for the next high-value live-proof packets in the TS4 material research track.

If the task is being resumed in a new chat, read [Research Restart Guide](research-restart-guide.md) first so the queue is not mistaken for a truth layer.

Primary rule:

- candidate targets are not proof
- local coverage and corpus files are used here only to pick what to inspect next
- externally backed family identity still lives in the family sheets and matrix docs
- do not borrow the `SimGlass` label for object-side glass or generic transparent `Build/Buy` content; that semantic split now lives in [Object Glass And Transparency](family-sheets/object-glass-and-transparency.md)
- domain- or asset-heavy candidates in this queue are authority-discovery hints only; any proved result still has to converge into the shared shader/material contract
- the queue itself must be prioritized from whole-game family importance, prevalence, and external evidence strength, not from whichever single pack has the easiest current fixture

Related docs:

- [Research Restart Guide](research-restart-guide.md)
- [Material Pipeline Deep Dives](README.md)
- [Corpus-Wide Family Priority](corpus-wide-family-priority.md)
- [Corpus-Wide Family Census Baseline](corpus-wide-family-census-baseline.md)
- [CASPart Linkage Census Baseline](caspart-linkage-census-baseline.md)
- [Build/Buy Material Authority Matrix](buildbuy-material-authority-matrix.md)
- [Edge-Family Matrix](edge-family-matrix.md)
- [Live-Proof Packets](live-proof-packets/README.md)
- [Family Sheets](family-sheets/README.md)
- [Object Glass And Transparency](family-sheets/object-glass-and-transparency.md)
- [Shader Family Registry](shader-family-registry.md)
- [Runtime Shader Interface Baseline](runtime-shader-interface-baseline.md)
- [Package, Runtime, And Scene Bridge Boundary](package-runtime-scene-bridge-boundary.md)
- [External GPU Scene-Pass Baseline](external-gpu-scene-pass-baseline.md)
- [Helper-Family Package Carrier Boundary](helper-family-package-carrier-boundary.md)
- [Helper-Family Carrier Plausibility Matrix](helper-family-carrier-plausibility-matrix.md)
- [Runtime Helper-Family Clustering Floor](runtime-helper-family-clustering-floor.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [ShaderDayNight Evidence Ledger](shader-daynight-evidence-ledger.md)
- [Generated-Light Evidence Ledger](generated-light-evidence-ledger.md)
- [Refraction Evidence Ledger](refraction-evidence-ledger.md)
- [Refraction Bridge Fixture Boundary](refraction-bridge-fixture-boundary.md)
- [Open questions](../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Queue status (`v0.1`)

```text
P1 Live-Proof Queue
├─ Build/Buy transparent-object route ~ 100%
├─ SimSkin body/head shell authority ~ 94%
├─ SimGlass versus shell baseline ~ 96%
├─ SimSkin versus SimSkinMask ~ 83%
├─ CASHotSpotAtlas carry-through ~ 76%
├─ ShaderDayNightParameters visible-pass proof ~ 82%
├─ GenerateSpotLightmap / NextFloorLightMapXform ~ 80%
└─ RefractionMap live proof ~ 100%
```

## How to use this queue

For each row, the next packet should answer:

1. which externally backed family identity is being tested
2. which local candidate asset or corpus packet should be inspected next
3. what exact claim would be proved or falsified
4. what current implementation mistake would become easier to diagnose after that proof

Safe reading:

- a queued `BuildBuy`, `CAS`, or `Sim` fixture is not a proposal for a domain-specific shader
- it is only a concrete route for proving authoritative inputs or preserved family semantics before those inputs enter the shared material/shader path
- a queued `EP10`, `EP11`, or other pack-local route is not a statement that this pack now defines the global research priority
- use [Corpus-Wide Family Priority](corpus-wide-family-priority.md) before allowing any one narrow lane to dominate the queue
- use [Corpus-Wide Family Census Baseline](corpus-wide-family-census-baseline.md) before saying that one row is "more popular" or "more common" across the game
- use [CASPart Linkage Census Baseline](caspart-linkage-census-baseline.md) before underweighting the character-side shell/compositor rows just because their direct family names are still partially open
- use [Runtime Shader Interface Baseline](runtime-shader-interface-baseline.md) before widening weak helper-family rows into more narrative-only search; the runtime interface corpus now gives a better route for slot/param questions
- use [Package, Runtime, And Scene Bridge Boundary](package-runtime-scene-bridge-boundary.md) before claiming the main blocker is still "missing research" in the abstract; the missing join is now narrower and explicit
- use [External GPU Scene-Pass Baseline](external-gpu-scene-pass-baseline.md) before treating any captured shader/pass evidence as if compositor or depth-helper work were normal authored material ownership
- use [Helper-Family Package Carrier Boundary](helper-family-package-carrier-boundary.md) when the game is not being run; the next honest offline move is now an ownership-boundary pass, not another free-form helper-family guess
- after that ownership-boundary pass lands, push the result back into the weak family sheets before creating more helper-family boundary docs
- use [Helper-Family Carrier Plausibility Matrix](helper-family-carrier-plausibility-matrix.md) when the next offline question is row-by-row carrier plausibility rather than one more general helper-family boundary
- use [Runtime Helper-Family Clustering Floor](runtime-helper-family-clustering-floor.md) when the helper-family rows need a next action; the first honest move is now shape clustering, not more literal name search
- use [TS4 DX11 Context-Tagged Capture Contract](../../../satellites/ts4-dx11-introspection/docs/context-tagged-capture-contract.md) when a helper-family row reaches the current broad-capture ceiling; the next honest move is tagged capture, not another unlabeled session
- use [TS4 DX11 Context-Tagged Capture Recipes](../../../satellites/ts4-dx11-introspection/docs/context-tagged-capture-recipes.md) when the next helper-family batch is actually being run; the standard runner now has helper presets for the three minimum tagged sessions
- use [TS4 DX11 Context-Tagged Capture Analysis Workflow](../../../satellites/ts4-dx11-introspection/docs/context-tagged-capture-analysis-workflow.md) after the tagged sessions exist; the next honest helper-family uplift must compare target versus control instead of reading one tagged session in isolation
- when building that compare pair, keep the same helper-family focus on both target and control and vary the scene emphasis instead of dropping the control metadata
- the current `Build/Buy` `MTST` seam is now narrower than this queue:
  - [Build/Buy MTST Default-State Boundary](live-proof-packets/buildbuy-mtst-default-state-boundary.md)
  - [Build/Buy MTST Portable-State Delta](live-proof-packets/buildbuy-mtst-portable-state-delta.md)
  - [Build/Buy MTST State-Selector Structure](live-proof-packets/buildbuy-mtst-state-selector-structure.md)
- do not widen that seam back into a generic “find any `MTST` case” task before a swatch-level or clearer runtime-state target is named
- repeated `stateHash` structure and the `002211...` paired `unknown0` split no longer need to be re-proved from raw probe output unless a contradictory fixture appears

## P1 rows

### 1. `Build/Buy` transparent-object route

Externally backed identity:

- transparent `Build/Buy` content must now be classified against object-side glass/transparency before it is attached to any narrower family row

Current local candidate packet:

- [Object Glass And Transparency](family-sheets/object-glass-and-transparency.md)
- [Build/Buy Transparent Object Classification Boundary](live-proof-packets/buildbuy-transparent-object-classification-boundary.md)
- [Build/Buy Transparent Object Fallback Ladder](buildbuy-transparent-object-fallback-ladder.md)
- [Build/Buy Transparent Object Authority Order](buildbuy-transparent-object-authority-order.md)
- [Build/Buy Transparent Object Candidate State Ladder](live-proof-packets/buildbuy-transparent-object-candidate-state-ladder.md)
- [Build/Buy Transparent Object Classification Signals](buildbuy-transparent-object-classification-signals.md)
- [Build/Buy Transparent-Decor Route](live-proof-packets/buildbuy-transparent-decor-route.md)
- [Build/Buy Transparent Object DisplayShelf Anchor](live-proof-packets/buildbuy-transparent-object-displayshelf-anchor.md)
- [Build/Buy Transparent Object ShopDisplayTileable Anchor](live-proof-packets/buildbuy-transparent-object-shopdisplay-anchor.md)
- [Build/Buy Transparent Object Top-Anchor Negative Reopen](live-proof-packets/buildbuy-transparent-object-top-anchor-negative-reopen.md)
- [Build/Buy Transparent Object Lower-Anchor Negative Reopen](live-proof-packets/buildbuy-transparent-object-lower-anchor-negative-reopen.md)
- [Build/Buy Transparent Object Full-Route Stall](live-proof-packets/buildbuy-transparent-object-full-route-stall.md)
- [Build/Buy Transparent Object Post-Top-Anchor Handoff](live-proof-packets/buildbuy-transparent-object-post-top-anchor-handoff.md)
- [Build/Buy Transparent Object Top-Anchor Tiebreak](live-proof-packets/buildbuy-transparent-object-top-anchor-tiebreak.md)
- [Build/Buy Transparent Object Top-Anchor Exhaustion Boundary](live-proof-packets/buildbuy-transparent-object-top-anchor-exhaustion-boundary.md)
- [Build/Buy Transparent Object Survey-Versus-Reopen Boundary](live-proof-packets/buildbuy-transparent-object-survey-vs-reopen-boundary.md)
- [Build/Buy Transparent Object Fixture Promotion Boundary](live-proof-packets/buildbuy-transparent-object-fixture-promotion-boundary.md)
- [Build/Buy Transparent Object Mixed-Signal Resolution](live-proof-packets/buildbuy-transparent-object-mixed-signal-resolution.md)
- [Build/Buy Transparent Object Reopen Checklist](live-proof-packets/buildbuy-transparent-object-reopen-checklist.md)
- [Build/Buy Transparent Object Route Stall Boundary](live-proof-packets/buildbuy-transparent-object-route-stall-boundary.md)
- [Build/Buy Transparent Object Target Priority](live-proof-packets/buildbuy-transparent-object-target-priority.md)
- [Build/Buy Window-Heavy Transparent Negative Control](live-proof-packets/buildbuy-window-heavy-transparent-negative-control.md)
- [Build/Buy Window-Curtain Widening Route](live-proof-packets/buildbuy-window-curtain-widening-route.md)
- the current transparent-decor cluster remains:
  - `fishBowl_EP10GENmarimo`
  - `shelfFloor2x1_EP10TEAdisplayShelf`
  - `shelfFloor2x1_EP10TEAshopDisplayTileable`
  - `lightWall_EP10GENlantern`
  - `mirrorWall1x1_EP10BATHsunrise`

Concrete local search targets:

- `tmp/probe_ep10_buildbuy_identity_survey_full.json`: the five transparent-decor object rows
- `tmp/probe_ep10_buildbuy_candidate_resolution_full.json`: the same five transformed-root matches
- `tmp/probe_all_buildbuy.txt`: root-list confirmation for the transformed roots
- `tmp/buildbuy_window_curtain_widening_snapshot_2026-04-21.json`: widened window/curtain route after the stalled decor cluster

What the next live-proof packet should prove:

- whether any later transparent-object reopen can honestly overturn the now-frozen widened quartet with stronger object-glass or explicit `AlphaBlended` evidence

Best current next target:

- preserve the completed decor-route stack as the exhausted first route:
  - all five `EP10` decor roots have already been attempted as real reopen targets
  - all five stop at `Build/Buy asset not found`
  - the full transparent-decor route is now stalled at the present inspection layer
- use the next widened route instead of one vague return to “window-heavy” search:
  - `window2X1_EP10GENsliding2Tile`
  - `window2X1_EP10TRADwindowBox2Tile`
  - `curtain1x1_EP10GENstrawTileable2Tile`
  - `curtain2x1_EP10GENnorenShortTileable`
- preserve why that widened route is narrower than repeated naming:
  - `sliding2Tile` and `windowBox2Tile` each preserve `Model`, `Rig`, `Slot`, and `Footprint`
  - the two curtain anchors currently preserve weaker `Model` plus `Footprint` bundles
- preserve the stronger exact-root boundary too:
  - exact `set1` `ObjectDefinition` roots are now browseable `Build/Buy Object` identity assets:
    - `C0DB5AE7:00000000:000000000003D122`
    - `C0DB5AE7:00000000:000000000003D55A`
    - `C0DB5AE7:00000000:000000000003D568`
    - `C0DB5AE7:00000000:000000000003D69B`
  - asset summaries still stop at metadata support with `ExactGeom=False` and `Textures=False`
  - the leading window pair now survives past that summary floor:
    - `sliding2Tile -> Partial | StaticReady=4 | Bloom=3 | SkyDark=1 | AlphaCutoutMaterialDecodeStrategy=1`
    - `windowBox2Tile -> Partial | StaticReady=4 | Bloom=3 | SkyDark=1 | AlphaCutoutMaterialDecodeStrategy=1`
  - the curtain pair now also survives past that summary floor:
    - `strawTileable2Tile -> Partial | StaticReady=1 | SeasonalFoliage=1 | SeasonalFoliageMaterialDecodeStrategy=1`
    - `norenShortTileable -> Partial | StaticReady=1 | colorMap7=1 | AlphaCutoutMaterialDecodeStrategy=1`
- keep family classification unchanged until a real fixture survives:
  - object-glass signals first
  - threshold/cutout signals second
  - explicit `AlphaBlended` third
  - `SimGlass` only as the last-choice branch
- keep the old repeated-name packet behind the new widened route:
  - [Build/Buy Window-Heavy Transparent Negative Control](live-proof-packets/buildbuy-window-heavy-transparent-negative-control.md)
- preserve the widening packet itself:
  - [Build/Buy Window-Curtain Widening Route](live-proof-packets/buildbuy-window-curtain-widening-route.md)
- preserve the closed curtain-side route:
  - [Build/Buy Curtain Route Closure](live-proof-packets/buildbuy-curtain-route-closure.md)
- preserve the frozen quartet split:
  - [Build/Buy Window-Curtain Quartet Family Split](live-proof-packets/buildbuy-window-curtain-quartet-family-split.md)

Current packet:

- [Build/Buy Transparent Object Authority Order](buildbuy-transparent-object-authority-order.md)
- [Build/Buy Transparent Object Fallback Ladder](buildbuy-transparent-object-fallback-ladder.md)
- [Build/Buy Transparent Object Candidate State Ladder](live-proof-packets/buildbuy-transparent-object-candidate-state-ladder.md)
- [Build/Buy Transparent Object Classification Signals](buildbuy-transparent-object-classification-signals.md)
- [Build/Buy Transparent-Decor Route](live-proof-packets/buildbuy-transparent-decor-route.md)
- [Build/Buy Transparent Object Fixture Promotion Boundary](live-proof-packets/buildbuy-transparent-object-fixture-promotion-boundary.md)
- [Build/Buy Transparent Object Mixed-Signal Resolution](live-proof-packets/buildbuy-transparent-object-mixed-signal-resolution.md)
- [Build/Buy Transparent Object Reopen Checklist](live-proof-packets/buildbuy-transparent-object-reopen-checklist.md)
- [Build/Buy Transparent Object Route Stall Boundary](live-proof-packets/buildbuy-transparent-object-route-stall-boundary.md)
- [Build/Buy Transparent Object Target Priority](live-proof-packets/buildbuy-transparent-object-target-priority.md)
- [Build/Buy Transparent Object Classification Boundary](live-proof-packets/buildbuy-transparent-object-classification-boundary.md)
- [Build/Buy Window-Curtain Widening Route](live-proof-packets/buildbuy-window-curtain-widening-route.md)
- [Build/Buy Window-Curtain Family Verdict Boundary](live-proof-packets/buildbuy-window-curtain-family-verdict-boundary.md)
- [Build/Buy Window-Curtain Strongest-Pair Material Divergence](live-proof-packets/buildbuy-window-curtain-strongest-pair-material-divergence.md)
- [Build/Buy Window CutoutInfoTable Companion Floor](live-proof-packets/buildbuy-window-cutoutinfotable-companion-floor.md)
- [Build/Buy Window ModelCutout Companion Closure](live-proof-packets/buildbuy-window-modelcutout-companion-closure.md)
- [Build/Buy Window Structural-Cutout Verdict Floor](live-proof-packets/buildbuy-window-structural-cutout-verdict-floor.md)
- [Build/Buy Curtain Route Closure](live-proof-packets/buildbuy-curtain-route-closure.md)
- [Build/Buy Window-Curtain Quartet Family Split](live-proof-packets/buildbuy-window-curtain-quartet-family-split.md)

Current next step after the stalled `EP10` decor route:

- continue from [Build/Buy Window-Curtain Widening Route](live-proof-packets/buildbuy-window-curtain-widening-route.md)
- then freeze the next handoff through [Build/Buy Window-Curtain Family Verdict Boundary](live-proof-packets/buildbuy-window-curtain-family-verdict-boundary.md)
- then keep the strongest-pair direct-material packet separate through [Build/Buy Window-Curtain Strongest-Pair Material Divergence](live-proof-packets/buildbuy-window-curtain-strongest-pair-material-divergence.md)
- use the exact `ObjectDefinition` roots as the real entry lane, not the transformed model roots
- preserve the current live floor instead of reopening the same bridge question:
  - the full widened quartet now already proves same-package `swap32` model promotion plus embedded-`MLOD` `Partial` scene survival
- preserve the strongest inspected pair inside that floor:
  - `sliding2Tile` is the current strongest window-side direct packet
  - `norenShortTileable` is the current strongest curtain-side direct packet
- use that strongest-pair split to block one shared quartet verdict:
  - strongest window still reads as opening/cutout pressured
  - strongest curtain already reaches a direct `transparent=True` material packet through `colorMap7` plus `alpha-test-or-blend`
- the surviving window pair now already has explicit same-instance `CutoutInfoTable` companions:
  - `81CA1A10:00000000:05879178560EABDF`
  - `81CA1A10:00000000:970F358CFC9991D1`
  - both entries point back to the exact promoted model roots and carry `flags=0x321` with `IS_PORTAL` plus `USES_CUTOUT`
- the surviving window pair now also has same-instance `ModelCutout`:
  - `07576A17:00000000:05879178560EABDF`
  - `07576A17:00000000:970F358CFC9991D1`
  - both sit on the exact promoted model roots beside the matching `CutoutInfoTable`
- keep the window-side verdict fixed as structural cutout/opening first:
  - the remaining `AlphaCutoutMaterialDecodeStrategy` hints stay secondary
  - exact runtime precedence can remain open without blocking family-verdict wording
- keep the curtain-side verdict fixed too:
  - neither `norenShortTileable` nor `strawTileable2Tile` currently closes as explicit `AlphaBlended`
  - the safest surviving curtain-side verdict is weaker threshold/cutout routing
- keep the widened quartet frozen as a family split:
  - windows -> structural cutout/opening
  - curtains -> weaker threshold/cutout
  - object glass not selected
- do not spend the next batch reopening this same quartet unless a stronger challenger appears elsewhere:
  - stronger object-glass fixture
  - stronger explicit `AlphaBlended` fixture
- hand the next autonomous batch to the next unfinished Tier A lane instead:
  - [SimSkin Versus SimSkinMask](live-proof-packets/simskin-vs-simskinmask.md)
- keep `ProbeAsset` runs sequential while the shared sqlite probe-cache remains a tooling concurrency ceiling
- after the current Tier A authority lanes, prefer family-level runtime interface clustering over more name-hunting for the weakest helper-family rows:
  - `ShaderDayNightParameters`
  - `Projection / Reveal / Lightmap`
  - `GenerateSpotLightmap / NextFloorLightMapXform`
  - `GenerateSpotLightmap / NextFloorLightMapXform`
- the current helper-family clustering floor now already narrows what that means:
  - start from seeded runtime shapes `F03`, `F04`, and `F05`
  - use `srctex`, `dsttex`, `maptex`, `alphatex`, `texscale`, `offset`, `scolor`, `srctexscale`, and `texgen` as bridge clues
  - do not wait for literal runtime labels like `RevealMap` or `NextFloorLightMapXform` to appear in reflection before clustering starts
- the next helper-family data step is now explicit too:
  - use [TS4 DX11 Context-Tagged Capture Contract](../../../satellites/ts4-dx11-introspection/docs/context-tagged-capture-contract.md)
  - use [TS4 DX11 Context-Tagged Capture Recipes](../../../satellites/ts4-dx11-introspection/docs/context-tagged-capture-recipes.md)
  - use [TS4 DX11 Context-Tagged Capture Analysis Workflow](../../../satellites/ts4-dx11-introspection/docs/context-tagged-capture-analysis-workflow.md)
  - treat the compare pair as target-versus-nearby-tagged-control, not target-versus-untagged background
  - allow manual `context-tags.json` sidecars until manifest fields are extended
  - do not treat another unlabeled broad session as a meaningful helper-family uplift

### 2. `SimSkin` body/head shell authority

Externally backed identity:

- `SimSkin` is a real GEOM-side shell-family branch, and creator-facing shell guidance keeps body/head shell selection separate from later layered skintone and overlay/detail content

Current local candidate packet:

- [SimSkin Body/Head Shell Authority](live-proof-packets/simskin-body-head-shell-authority.md)
- [BodyType Translation Boundary](bodytype-translation-boundary.md)
- [SimSkin, SimGlass, And SimSkinMask](family-sheets/simskin-simglass-simskinmask.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [Skintone And Overlay Compositor](skintone-and-overlay-compositor.md)
- [CASPart GEOM Shader Census Baseline](caspart-geom-shader-census-baseline.md)
- [TS4SimRipper Enums.cs](../../references/external/TS4SimRipper/src/Enums.cs)
- [TS4SimRipper TONE.cs](../../references/external/TS4SimRipper/src/TONE.cs)
- [TS4SimRipper SkinBlender.cs](../../references/external/TS4SimRipper/src/SkinBlender.cs)
- the completed direct character-side family floor now records:
  - `RowsWithResolvedGeometryShader = 281271`
  - `SimSkin = 280983` across `401` packages by `CASPart` rows
  - `SimSkin = 86697` across `147` packages by unique linked `GEOM`
  - `GeometryResolvedFromExternalPackage = 12911`
- the current shell-side direct floor is now also frozen in:
  - [body_head_shell_authority_snapshot_2026-04-21.json](../../tmp/body_head_shell_authority_snapshot_2026-04-21.json)
  - `Head` currently stays narrow at `90` parsed rows
  - the body-driving shell lane stays broader through `Full Body = 6276`, `Top = 9287`, and `Bottom = 6191`
  - the graph-backed archetype audit still keeps `FullBodyShell = 23` and `SplitBodyLayers = 12`

What the next live-proof packet should prove:

- whether body shell and head shell can now be treated as the main `SimSkin`-anchored authority packet before skintone/compositor refinement
- whether the queue can safely move from family existence into per-shell authority ordering without pretending exact compositor math is already solved

Best current next target:

- keep the new packet as the current shell/compositor authority anchor
- keep the first body-shell versus head-shell authority table restart-safe with the new direct shell floor
- use the new post-high-byte compositor-order packet to keep ordinary low-value overlay/detail rows above the mixed high-byte families in precedence reasoning
- keep wider `SimSkinMask` search and narrower `SimGlass` carry-over work below this row unless a stronger new corpus-level reason appears

Current packet:

- [SimSkin Body/Head Shell Authority](live-proof-packets/simskin-body-head-shell-authority.md)
- [BodyType 0x44 Family Boundary](live-proof-packets/bodytype-0x44-family-boundary.md)
- [BodyType 0x41 Family Boundary](live-proof-packets/bodytype-0x41-family-boundary.md)
- [BodyType 0x6D Family Boundary](live-proof-packets/bodytype-0x6d-family-boundary.md)
- [BodyType 0x6F Family Boundary](live-proof-packets/bodytype-0x6f-family-boundary.md)
- [BodyType 0x52 Family Boundary](live-proof-packets/bodytype-0x52-family-boundary.md)
- [BodyType 0x80 Family Boundary](live-proof-packets/bodytype-0x80-family-boundary.md)
- [Body And Head Shell Authority Table](body-head-shell-authority-table.md)
- [Hair, Accessory, And Shoes Authority Table](hair-accessory-shoes-authority-table.md)
- [CompositionMethod And SortLayer Boundary](compositionmethod-sortlayer-boundary.md)
- [Overlay And Detail Family Authority Table](overlay-detail-family-authority-table.md)
- [Overlay-Detail Priority After High-Byte Stack](live-proof-packets/overlay-detail-priority-after-highbyte-stack.md)
- [SortLayer Census Baseline](sortlayer-census-baseline.md)
- [CompositionMethod Census Baseline](compositionmethod-census-baseline.md)

Current next step after this packet:

- keep `0x44`, `0x41`, `0x6D`, `0x6F`, `0x52`, and `0x80` frozen as concrete packet layers instead of reopening them from the broad boundary doc
- treat `AdditionalTextureSpace` as the leading external hypothesis for the high-byte layer while keeping the exact encoding open
- use that now-closed packet stack plus the new overlay/detail precedence packet to keep the new sibling authority table for `Hair`, `Accessory`, and `Shoes` restart-safe
- keep `SimSkin` versus `SimSkinMask` below this row unless a genuinely new sample appears
- hand the next character-side batch to compositor-order follow-up first:
  - [Overlay-Detail Priority After High-Byte Stack](live-proof-packets/overlay-detail-priority-after-highbyte-stack.md)
  - [CompositionMethod And SortLayer Boundary](compositionmethod-sortlayer-boundary.md)

### 3. `SimGlass` versus shell baseline

Externally backed identity:

- `SimGlass` is a real narrow glass-family branch in local external `TS4SimRipper` snapshots and Sims-lineage shader docs

Current local candidate packet:

- [TS4SimRipper Enums.cs](../../references/external/TS4SimRipper/src/Enums.cs)
- [TS4SimRipper ColladaDAE.cs](../../references/external/TS4SimRipper/src/ColladaDAE.cs)
- [TS4SimRipper PreviewControl.cs](../../references/external/TS4SimRipper/src/PreviewControl.cs)
- [CASPart GEOM Shader Census Baseline](caspart-geom-shader-census-baseline.md)
- `tmp/precomp_sblk_inventory.json` currently includes `name_guess = "SimGlass"` with a narrow occurrence packet
- `tmp/probe_all_buildbuy_summary_full.json` now records `"SimGlass": 5` across the resolved Build/Buy survey
- the completed character-side family floor now also records:
  - `SimGlass = 6048` across `189` packages by `CASPart` rows
  - `SimSkin = 280983` across `401` packages by `CASPart` rows
- that Build/Buy side is now capped by an explicit evidence-limit packet:
  - [SimGlass Build/Buy Evidence Limit](live-proof-packets/simglass-buildbuy-evidence-limit.md)
- the first valid Build/Buy promotion is now also capped by an explicit winning-branch packet:
  - [SimGlass Build/Buy Promotion Gate](live-proof-packets/simglass-buildbuy-promotion-gate.md)
- the branch-specific losing conditions and winning record are now explicit too:
  - [SimGlass Build/Buy Disqualifiers](live-proof-packets/simglass-buildbuy-disqualifiers.md)
  - [SimGlass Build/Buy Winning Signals](live-proof-packets/simglass-buildbuy-winning-signals.md)
  - [SimGlass Build/Buy Outcome Ladder](live-proof-packets/simglass-buildbuy-outcome-ladder.md)
  - [SimGlass Build/Buy Mixed-Signal Resolution](live-proof-packets/simglass-buildbuy-mixed-signal-resolution.md)
  - [SimGlass Build/Buy Provisional Candidate Checklist](live-proof-packets/simglass-buildbuy-provisional-candidate-checklist.md)
  - [SimGlass Build/Buy Winning Fixture Checklist](live-proof-packets/simglass-buildbuy-winning-fixture-checklist.md)
- creator-facing transparency guidance now also strengthens the family shape:
  - [Transparency in clothing tutorial](https://maxismatchccworld.tumblr.com/post/645249485712326656/transparency-in-clothing-tutorial)
  - [Semi-Square Eyeglasses](https://kijiko-catfood.com/semi-square-eyeglasses/)
  - [Lashes and hair cc clashing](https://forums.ea.com/discussions/the-sims-4-mods-and-custom-content-en/lashes-and-hair-cc-clashing-pics-included-please-help-/12047424)
- character-transparency ordering is now also narrower on the `CAS/Sim` side:
  - [SimGlass Character Transparency Boundary](simglass-character-transparency-boundary.md)
  - [SimGlass Character Transparency Order](simglass-character-transparency-order.md)
  - current safe order is `SimGlass` first, then `SimAlphaBlended`, then generic character alpha only as provisional fallback
- bundled shell-control `.simgeom` fixtures in `docs/references/external/TS4SimRipper/src/Resources/` now give a cleaner `SimSkin` baseline for glass-versus-shell comparison
- the first narrower `EP10` identity sweep against obvious glass/window object names is now a useful negative control: checked candidate model refs from `window2X1_EP10GENsliding2Tile`, `window2X1_EP10TRADwindowBox2Tile`, `mirrorWall1x1_EP10BATHsunrise`, `sculptFountainEmitterSingle1x1_EP10GARDstoneBowl`, and `sculptWall_EP10TRADwindowBars` did not resolve to Build/Buy asset roots through either exact lookup or `instance-swap32`
- a broader `EP10` survey-backed cluster now gives a better next-step route than the window-heavy packet:
  - `fishBowl_EP10GENmarimo -> 01661233:00000000:FAE0318F3711431D`
  - `shelfFloor2x1_EP10TEAdisplayShelf -> 01661233:00000000:E779C31F25406B73`
  - `shelfFloor2x1_EP10TEAshopDisplayTileable -> 01661233:00000000:93EE8A0CF97A3861`
  - `lightWall_EP10GENlantern -> 01661233:00000000:F4A27FC1857F08D4`
  - `mirrorWall1x1_EP10BATHsunrise -> 01661233:00000000:3CD0344C1824BDDD`
- those transformed roots already appear in `tmp/probe_all_buildbuy.txt`, but current direct reopen attempts still fail with `Build/Buy asset not found`, so they are candidate anchors rather than stable fixtures
- the route is now narrower than that baseline note alone:
  - [SimGlass EP10 Transparent-Decor Route](live-proof-packets/simglass-ep10-transparent-decor-route.md) records that the cluster preserves repeated transformed companion bundles instead of only promising names
  - current strongest structural targets are `displayShelf`, `shopDisplayTileable`, `mirror`, then `lantern`
  - `fishBowl` remains plausible but structurally weaker because the current candidate packet only preserves its transformed `Model` root

Concrete local search targets:

- `tmp/precomp_sblk_inventory.json`: `name_guess = "SimGlass"` with `occurrences = 1`
- `docs/references/external/TS4SimRipper/src/Enums.cs`: explicit `SimGlass = 0x5EDA9CDE`
- `docs/references/external/TS4SimRipper/src/PreviewControl.cs`: separate glass grouping branch
- `tmp/probe_ep10_buildbuy_identity_survey_full.json`: `fishBowl`, `displayShelf`, `shopDisplayTileable`, `lantern`, `mirror` object rows
- `tmp/probe_ep10_buildbuy_candidate_resolution_full.json`: `instance-swap32` matches for the same cluster
- `tmp/probe_all_buildbuy.txt`: root-list confirmation for `FAE0318F3711431D`, `E779C31F25406B73`, `93EE8A0CF97A3861`, `F4A27FC1857F08D4`, `3CD0344C1824BDDD`

What the next live-proof packet should prove:

- whether a real shell companion asset can preserve a GEOM-side glass-family identity without being flattened into generic skin or alpha fallback
- whether that preserved identity survives discovery without implying a separate asset-domain shader branch
- or whether a reopened object actually belongs to the separate object-side glass/transparency branch rather than to `SimGlass`

Best current next target:

- start from the external `TS4SimRipper` glass-family code path, then use the Build/Buy survey hit as the stronger TS4-facing hint layer before falling back to narrow precompiled packets
- keep this row below `SimSkin` and character compositor-authority work:
  - the completed character-side family floor now makes `SimGlass` directly real
  - it also makes `SimGlass` directly much narrower than `SimSkin`
- treat the current Build/Buy survey hit as aggregate-only until one family-annotated row or object-name linkage is extracted
- do not let summary-grade `Build/Buy` presence skip over the current evidence ceiling:
  - `SimGlass = 5` keeps the branch alive
  - it does not promote a transparent object into the `SimGlass` row by itself
- do not let the first successful reopen skip over the winning-branch burden either:
  - a reopened transparent object is still not enough
  - `SimGlass` has to beat object-glass, threshold/cutout, and `AlphaBlended` explicitly
- if one of those branches survives more strongly, treat that as an explicit `SimGlass` disqualifier rather than as an ambiguous near-win
- if `SimGlass` really wins, record that win through a branch-specific checklist rather than through the generic transparent-object checklist alone
- if `SimGlass` stays alive, require at least one branch-positive signal rather than treating it as a win by elimination
- after reopen, force the result into the explicit verdict ladder:
  - stronger object-side branch win
  - generic transparent provisional boundary
  - provisional `SimGlass` candidate
  - winning `SimGlass` fixture
- if the result is provisional `SimGlass`, record it through the branch-specific provisional checklist
- if signals stay mixed around `SimGlass`, apply the branch-specific mixed-signal tie-break instead of borrowing only the generic transparent-object rule
- keep the current `EP10` obvious-name window packet as a negative boundary
- keep the semantic split explicit before promoting any reopened object:
  - object-side `GlassForObjectsTranslucent` is not the same family as character-side `SimGlass`
  - threshold/cutout transparency and `AlphaBlended` are separate object-side routes again
- use the broader transparent-decor cluster next, but in companion-bundle order: `displayShelf`, `shopDisplayTileable`, `mirror`, `lantern`, then `fishBowl`
- treat those roots as survey-backed search anchors until one reopens as a stable live fixture

Current classification boundary:

- [Build/Buy Transparent Object Classification Boundary](live-proof-packets/buildbuy-transparent-object-classification-boundary.md)

Current packet:

- [SimGlass Versus Shell Baseline](live-proof-packets/simglass-vs-shell-baseline.md)
- [SimGlass Build/Buy Evidence Limit](live-proof-packets/simglass-buildbuy-evidence-limit.md)
- [SimGlass Build/Buy Promotion Gate](live-proof-packets/simglass-buildbuy-promotion-gate.md)
- [SimGlass Build/Buy Disqualifiers](live-proof-packets/simglass-buildbuy-disqualifiers.md)
- [SimGlass Build/Buy Winning Signals](live-proof-packets/simglass-buildbuy-winning-signals.md)
- [SimGlass Build/Buy Outcome Ladder](live-proof-packets/simglass-buildbuy-outcome-ladder.md)
- [SimGlass Build/Buy Mixed-Signal Resolution](live-proof-packets/simglass-buildbuy-mixed-signal-resolution.md)
- [SimGlass Build/Buy Provisional Candidate Checklist](live-proof-packets/simglass-buildbuy-provisional-candidate-checklist.md)
- [SimGlass Build/Buy Winning Fixture Checklist](live-proof-packets/simglass-buildbuy-winning-fixture-checklist.md)

### 4. `SimSkin` versus `SimSkinMask`

Externally backed identity:

- `SimSkin` is a real GEOM-side skin family
- `SimSkinMask` is still only mask-adjacent semantics until a peer geometry branch is found

Current local candidate packet:

- bundled `.simgeom` resources in [TS4SimRipper Resources](../../references/external/TS4SimRipper/src/Resources)
- `tmp/precomp_sblk_inventory.json` includes `simskin = 51` and `SimSkinMask = 12`
- [simskin_vs_simskinmask_snapshot_2026-04-21.json](../../tmp/simskin_vs_simskinmask_snapshot_2026-04-21.json) now tightens the current local floor:
  - `simskin = 51` profile rows across `3` packed-type variants
  - `SimSkinMask = 12` profile rows across `6` packed-type variants
  - the current workspace `.simgeom` list expands only to a mirrored `tmp/research/TS4SimRipper` copy, not a new non-mirrored sample lane
- [CASPart GEOM Shader Census Baseline](caspart-geom-shader-census-baseline.md) now gives a completed direct family floor under the same character-side authority track:
  - `RowsWithResolvedGeometryShader = 281271`
  - `SimSkin = 280983` across `401` packages by `CASPart` rows
  - `SimGlass = 6048` across `189` packages by `CASPart` rows
  - `SimSkin = 86697` across `147` packages by unique linked `GEOM`
- the checked-in census outputs now also preserve the stronger negative floor directly:
  - [caspart_geom_shader_census_fullscan.json](../../tmp/caspart_geom_shader_census_fullscan.json) currently surfaces `SimSkin` and `SimGlass`, but no `SimSkinMask`
  - the `414` per-package result shards under `tmp/caspart_geom_shader_census_run/package-results` also currently stay negative for `SimSkinMask`
- [CASPart Linkage Census Baseline](caspart-linkage-census-baseline.md) now gives a broader direct linkage floor under the same character-side authority track:
  - `ParsedResources = 299028`
  - `RowsWithAnyGeometryCandidate = 281303`
  - `RowsWithTextureCandidates = 236668`
  - `RowsWithRegionMapCandidate = 108906`
- public refresh stays aligned with that same bounded reading:
  - current public `TS4SimRipper` still exposes `SimSkin` and `SimGlass`, not a peer `SimSkinMask` geometry/export branch
  - public `Sims 4: CASPFlags` keeps the nearest public skin-mask category at `SkinOverlay`

Concrete local search targets:

- `docs/references/external/TS4SimRipper/src/Resources/cuBodyComplete_lod0.simgeom`
- `docs/references/external/TS4SimRipper/src/Resources/cuHead_lod0.simgeom`
- `docs/references/external/TS4SimRipper/src/Resources/yfBodyComplete_lod0.simgeom`
- `docs/references/external/TS4SimRipper/src/Resources/ymBodyComplete_lod0.simgeom`
- `tmp/precomp_sblk_inventory.json`: `simskin = 51`
- `tmp/precomp_sblk_inventory.json`: `SimSkinMask = 12`

What the next live-proof packet should prove:

- whether a wider live corpus outside bundled samples ever shows `SimSkinMask` as a real peer geometry branch rather than only a helper or compositor-adjacent signal

Best current next target:

- inspect the bundled `.simgeom` packet first as the baseline, then deliberately look for a counterexample elsewhere instead of assuming one exists
- use the completed `CASPart -> GEOM -> family` floor to keep this row in the main queue as a whole-character issue rather than a narrow sample-only packet
- treat this row as the leading character-side packet ahead of narrower `SimGlass` carry-over work
- treat the current local workspace search as bounded:
  - no new non-mirrored `.simgeom` counterexample currently survives inside the repo
  - no `SimSkinMask` row currently survives in the checked-in direct family census floor either
  - the next proof burden is a genuinely new external or live sample
- do not spend another repo-local grep batch here unless a genuinely new sample lane appears

Current packet:

- [SimSkin Versus SimSkinMask](live-proof-packets/simskin-vs-simskinmask.md)

### 5. `CASHotSpotAtlas` carry-through

Externally backed identity:

- `CASHotSpotAtlas` is a real EA hotspot atlas mapped to `UV1` and tied to morph or slider logic

Current local candidate packet:

- [CASHotSpotAtlas family sheet](family-sheets/cas-hotspot-atlas.md)
- `tmp/precomp_sblk_inventory.json` shows concentrated carry-through counts such as `121`, `47`, `18`, and `16`
- the current external identity packet is now explicit enough to freeze:
  - `CASHotSpotAtlas -> color value -> HotSpotControl -> SimModifier -> BGEO/DMap/bone delta style morph resources`
- the checked-in `TS4SimRipper` snapshot now sharpens the local external packet:
  - `HotSpotControl` currently exists only as a resource-type enum
  - the stronger proven downstream chain is `SimModifier -> SMOD -> BGEO/DMap/BOND`

Concrete local search targets:

- `tmp/precomp_sblk_inventory.json`: `CASHotSpotAtlas = 121`
- `tmp/precomp_sblk_inventory.json`: `CASHotSpotAtlas = 47`
- `tmp/precomp_sblk_inventory.json`: `CASHotSpotAtlas = 18`
- `tmp/precomp_sblk_inventory.json`: `CASHotSpotAtlas = 16`

What the next live-proof packet should prove:

- whether hotspot-atlas provenance can survive into adjacent render metadata without becoming a true ordinary surface-material slot

Best current next target:

- treat the high local carry-through counts as a search queue only; the proof still needs a real CAS/morph-side fixture

Current packet:

- [CASHotSpotAtlas Carry-Through](live-proof-packets/cas-hotspotatlas-carry-through.md)

### 6. `ShaderDayNightParameters`

Externally backed identity:

- layered day/night or reveal-aware family with helper provenance

Current local candidate packet:

- [ShaderDayNight Evidence Ledger](shader-daynight-evidence-ledger.md)
- `tmp/precomp_sblk_inventory.json` shows `name_guess = "ShaderDayNightParameters"`
- `tmp/probe_sample_ep06_ep10_coverage.txt` includes `ClientFullBuild0.package | 01661233:00000000:0737711577697F1C`
- `tmp/probe_sample_next24_coverage_after_nonvisual_fix.txt` includes `ClientFullBuild0.package | 01661233:00000000:00B6ABED04A8F593`
- `tmp/sample_payload_batch_after_full5.txt` now also includes `ClientFullBuild0.package | 01661233:00000000:1463BD19EE39DC8C`

Concrete local search targets:

- `tmp/precomp_sblk_inventory.json`: `name_guess = "ShaderDayNightParameters"` with `occurrences = 5`
- `tmp/precomp_sblk_inventory.json`: `LightsAnimLookupMap = 94`
- `tmp/precomp_sblk_inventory.json`: `samplerRevealMap = 32`
- `tmp/probe_sample_ep06_ep10_coverage.txt`: `ClientFullBuild0.package | 01661233:00000000:0737711577697F1C`
- `tmp/probe_sample_next24_coverage_after_nonvisual_fix.txt`: `ClientFullBuild0.package | 01661233:00000000:00B6ABED04A8F593`
- `tmp/sample_payload_batch_after_full5.txt`: `ClientFullBuild0.package | 01661233:00000000:1463BD19EE39DC8C`

What the next live-proof packet should prove:

- whether the current runtime helper-family route is already narrow enough to prefer one cluster candidate before context-tagged capture starts

Best current next target:

- keep the three current `ClientFullBuild0.package` roots as the visible comparison packet
- start the runtime helper-family side from `F04`, not the broad `F03/F04/F05` bucket
- keep `F05` as the nearest color-aware comparator, not the leading target
- keep the checked-in broad runtime sessions under the blocker packet, not the proof packet:
  - they preserve `F04` and `F05`
  - they do not yet separate them by scene/context

Current packet:

- [ShaderDayNightParameters Visible-Pass Proof](live-proof-packets/shader-daynight-visible-pass.md)
- [ShaderDayNight Runtime Cluster Candidate Floor](live-proof-packets/shader-daynight-runtime-cluster-candidate-floor.md)
- [ShaderDayNight Runtime Context Gap](live-proof-packets/shader-daynight-runtime-context-gap.md)
- [TS4 DX11 Context-Tagged Capture Contract](../../../satellites/ts4-dx11-introspection/docs/context-tagged-capture-contract.md)

### 7. `GenerateSpotLightmap` and `NextFloorLightMapXform`

Externally backed identity:

- generated-light or lightmap-helper family

Current local candidate packet:

- [Generated-Light Evidence Ledger](generated-light-evidence-ledger.md)
- `tmp/precomp_sblk_inventory.json` shows `name_guess = "GenerateSpotLightmap"` with `occurrences = 6` and `NextFloorLightMapXform = 14`
- the same inventory also shows a weaker secondary `NextFloorLightMapXform = 3` packet

Concrete local search targets:

- `tmp/precomp_sblk_inventory.json`: `name_guess = "GenerateSpotLightmap"` with `occurrences = 6`
- `tmp/precomp_sblk_inventory.json`: `NextFloorLightMapXform = 14`
- `tmp/precomp_sblk_inventory.json`: secondary `NextFloorLightMapXform = 3`

What the next live-proof packet should prove:

- whether a concrete live asset can confirm generated-light helper provenance without forcing these names into ordinary material-slot or UV-transform semantics

Best current next target:

- start with the stronger `GenerateSpotLightmap` packet tied to `NextFloorLightMapXform = 14`
- start the runtime helper-family side from `F03`, not a broad `F03/F04/F05` bucket
- treat the stable `maptex + tex + Constants` packet with `compx`, `compy`, `mapScale`, and `scale` as the leading generated-light runtime bridge
- keep `F04` as the broader comparator, not the first generated-light runtime target

Current packet:

- [GenerateSpotLightmap And NextFloorLightMapXform](live-proof-packets/generate-spotlightmap-nextfloorlightmapxform.md)
- [Generated-Light Runtime Cluster Candidate Floor](live-proof-packets/generated-light-runtime-cluster-candidate-floor.md)
- [Generated-Light Runtime Context Gap](live-proof-packets/generated-light-runtime-context-gap.md)
- [TS4 DX11 Context-Tagged Capture Contract](../../../satellites/ts4-dx11-introspection/docs/context-tagged-capture-contract.md)

### 8. `RefractionMap`

Externally backed identity:

- projection or refraction family, not ordinary diffuse material

Current local candidate packet:

- [Projection, Reveal, And Lightmap Families](family-sheets/projection-reveal-lightmap.md)
- [Refraction Evidence Ledger](refraction-evidence-ledger.md)
- [Refraction Bridge Fixture Boundary](refraction-bridge-fixture-boundary.md)
- current local corpus keeps `RefractionMap` as a named branch
- `tmp/probe_all_buildbuy_summary_full.json` now records `"RefractionMap": 33`, which upgrades the family from precomp-only archaeology to survey-level TS4-facing presence
- the current narrower `EP10` identity pass now maps `01661233:00000000:00F643B0FDD2F1F7` back to `ClientFullBuild0.package | sculptFountainSurface3x3_EP10GENlilyPad` through an `instance-swap32` transform from `OBJD` candidate `01661233:00000000:FDD2F1F700F643B0`
- nearest adjacent projective roots are `00F643B0FDD2F1F7` and `0124E3B8AC7BEE62`
- strongest visible comparison roots are `0737711577697F1C` and `00B6ABED04A8F593`

What the next live-proof packet should prove:

- whether a concrete asset can be found where refraction-family identity and helper params are visible without collapsing them into generic surface semantics
- whether the object/material seam can prove shared refraction-family semantics without turning the fixture into asset-specific shader logic

Best current next target:

- keep the named lily-pad row as the current bounded floor/ceiling reference rather than the default next deep target
- inspect `0389A352F5EDFD45` as the next clean route:
  - `EP11\\ClientFullBuild0.package`
  - `SceneReady`
  - `textured=1`
  - `WorldToDepthMapSpaceMatrix=1`
  - `ProjectiveMaterialDecodeStrategy=1`
- keep the new route-specific packets explicit:
  - [Refraction 0389 Clean-Route Baseline](live-proof-packets/refraction-0389-clean-route-baseline.md)
  - [Refraction 0124 Mixed-Control Floor](live-proof-packets/refraction-0124-mixed-control-floor.md)
  - [Refraction 0389 Identity Gap](live-proof-packets/refraction-0389-identity-gap.md)
  - [Refraction 0389 Versus LilyPad Floor](live-proof-packets/refraction-0389-vs-lilypad-floor.md)
  - [Refraction 0389 No Signal Upgrade](live-proof-packets/refraction-0389-no-signal-upgrade.md)
  - [Refraction Post-0389 Handoff Boundary](live-proof-packets/refraction-post-0389-handoff-boundary.md)
- do not burn time on blind grep over `probe_all_buildbuy.txt`; current workspace shows that family totals and root/header lists are split across different artifacts
- keep `0124E3B8AC7BEE62` as the mixed boundary/control case rather than the next clean route, because it still carries the noisier `WorldToDepthMapSpaceMatrix=2` packet plus earlier `FresnelOffset` and fallback-diffuse behavior
- keep `0737711577697F1C` and `00B6ABED04A8F593` as visible neighborhood controls only
- only after the next clean route is honestly bounded should the batch widen out to `SimGlass`

Current packet:

- [RefractionMap Live Proof](live-proof-packets/refractionmap-live-proof.md)
- [Refraction Companion-Material Outcome Ladder](live-proof-packets/refraction-companion-material-outcome-ladder.md)
- [Refraction Companion-Material Checklist](live-proof-packets/refraction-companion-material-checklist.md)
- [Refraction Companion MATD-vs-MTST Boundary](live-proof-packets/refraction-companion-matd-vs-mtst-boundary.md)
- [Refraction Adjacent-Helper Boundary](live-proof-packets/refraction-adjacent-helper-boundary.md)
- [Refraction LilyPad Direct MATD Floor](live-proof-packets/refraction-lilypad-direct-matd-floor.md)
- [Refraction LilyPad Projective Floor Boundary](live-proof-packets/refraction-lilypad-projective-floor-boundary.md)
- [Refraction LilyPad No Direct Family Surface](live-proof-packets/refraction-lilypad-no-direct-family-surface.md)
- [Refraction LilyPad Escalation Boundary](live-proof-packets/refraction-lilypad-escalation-boundary.md)
- [Refraction Post-LilyPad Pivot](live-proof-packets/refraction-post-lilypad-pivot.md)
- [Refraction Next-Route Priority](live-proof-packets/refraction-next-route-priority.md)
- [Refraction 0389 Clean-Route Baseline](live-proof-packets/refraction-0389-clean-route-baseline.md)
- [Refraction 0124 Mixed-Control Floor](live-proof-packets/refraction-0124-mixed-control-floor.md)
- [Refraction 0389 Identity Gap](live-proof-packets/refraction-0389-identity-gap.md)
- [Refraction 0389 Versus LilyPad Floor](live-proof-packets/refraction-0389-vs-lilypad-floor.md)

## Immediate order

`Current concrete packets`

1. `SimSkin` versus `SimSkinMask`
2. `Build/Buy` transparent-object route
3. `CASHotSpotAtlas` carry-through
4. `ShaderDayNightParameters`
5. `GenerateSpotLightmap` / `NextFloorLightMapXform`
6. `SimGlass` versus shell baseline
7. `RefractionMap`

`Next unfinished order`

1. rebuild whole-game priority before choosing a narrow packet:
   - use corpus-wide family prevalence
   - use rendering/material importance
   - use external evidence strength
   - use cross-domain representativeness
   - use implementation-spec value
2. prefer Tier A rows first:
   - object-side transparency
   - `SimSkin` and character compositor-authority seams
   - keep the new `Hair` / `Accessory` / `Shoes` authority table as the sibling worn-slot baseline, then return to `SimSkin` versus `SimSkinMask` before reopening narrower Tier B or Tier C rows by momentum
3. prefer Tier B rows next:
   - `CASHotSpotAtlas`
   - `ShaderDayNightParameters`
   - generated-light helpers
4. treat Tier C rows honestly:
   - `SimGlass` is directly real, but much narrower than `SimSkin` on the completed character-side family floor
   - `RefractionMap` is bounded at the current inspection layer
   - the `EP10` transparent-decor route is stalled at the current inspection layer
   - neither should keep driving the queue without a new inspection layer and a corpus-level reason
5. when a pack-local lane is chosen, treat it as secondary validation only:
   - use it to validate or falsify a family-level claim
   - do not let it silently become the new global priority because it has convenient search anchors
