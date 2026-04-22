# SimGlass Versus Shell Baseline

This packet is the first concrete `P1` live-proof target under the edge-family research track.

Question:

- can a real glass-family branch survive next to ordinary shell or skin-family logic without being flattened into generic alpha or generic shell fallback?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Edge-Family Matrix](../edge-family-matrix.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [SimGlass Build/Buy Evidence Limit](simglass-buildbuy-evidence-limit.md)
- [SimGlass Build/Buy Promotion Gate](simglass-buildbuy-promotion-gate.md)
- [SimGlass Build/Buy Disqualifiers](simglass-buildbuy-disqualifiers.md)
- [SimGlass Build/Buy Winning Signals](simglass-buildbuy-winning-signals.md)
- [SimGlass Build/Buy Outcome Ladder](simglass-buildbuy-outcome-ladder.md)
- [SimGlass Build/Buy Mixed-Signal Resolution](simglass-buildbuy-mixed-signal-resolution.md)
- [SimGlass Build/Buy Provisional Candidate Checklist](simglass-buildbuy-provisional-candidate-checklist.md)
- [SimGlass Build/Buy Winning Fixture Checklist](simglass-buildbuy-winning-fixture-checklist.md)
- [Build/Buy Transparent Object Classification Boundary](buildbuy-transparent-object-classification-boundary.md)
- [SimGlass EP10 Transparent-Decor Route](simglass-ep10-transparent-decor-route.md)
- [SimSkin, SimGlass, And SimSkinMask](../family-sheets/simskin-simglass-simskinmask.md)
- [Object Glass And Transparency](../family-sheets/object-glass-and-transparency.md)
- [CAS/Sim Material Authority Matrix](../cas-sim-material-authority-matrix.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
SimGlass Versus Shell Baseline
├─ Externally proved identity ~ 87%
├─ Local external snapshot packet ~ 86%
├─ Candidate live-target isolation ~ 88%
├─ Exact shell-ranking proof ~ 24%
└─ Implementation-diagnostic value ~ 68%
```

## Externally proved identity

What is already strong enough:

- local external [TS4SimRipper Enums.cs](../../../references/external/TS4SimRipper/src/Enums.cs) explicitly defines `SimGlass = 0x5EDA9CDE`
- local external [TS4SimRipper PreviewControl.cs](../../../references/external/TS4SimRipper/src/PreviewControl.cs) tracks glass meshes separately through `gotGlass` and `GlassModel`
- local external [TS4SimRipper ColladaDAE.cs](../../../references/external/TS4SimRipper/src/ColladaDAE.cs) exports a dedicated `glass` texture suffix instead of treating the family like generic skin
- [Sims_3:Shaders](https://modthesims.info/wiki.php?title=Sims_3%3AShaders) keeps `simglass` as a distinct family with refraction, overlay, and UV-related parameters
- creator-facing TS4 transparency guidance also points at the same family direction:
  - [Transparency in clothing tutorial](https://maxismatchccworld.tumblr.com/post/645249485712326656/transparency-in-clothing-tutorial) tells creators to change the shader to `Simglass` for transparent clothing parts
  - [Semi-Square Eyeglasses](https://kijiko-catfood.com/semi-square-eyeglasses/) notes that making the frame semi-transparent would require `SimGlass` and that this shader interacts badly with transparent hair
  - [Lashes and hair cc clashing](https://forums.ea.com/discussions/the-sims-4-mods-and-custom-content-en/lashes-and-hair-cc-clashing-pics-included-please-help-/12047424) describes alpha hair and lashes as using the same "glass" shader family as glasses and other transparent CAS items

Safe reading:

- `SimGlass` is a real edge family
- `SimGlass` should survive as provenance even when the full TS4 material contract is not yet solved
- the burden of proof is on any implementation that wants to flatten it into generic shell or alpha behavior
- the packet may use `BuildBuy` or CAS-adjacent fixtures, but those fixtures only prove authority paths and family survival before the shared shader/material contract
- after the new object-transparent split, transparent `Build/Buy` fixtures must also be checked against object-side glass/transparency branches before they are promoted into the `SimGlass` row
- current creator evidence makes one narrower search rule safe too:
  - `SimGlass` is strongly associated with thin transparent layered content
  - that makes obvious architectural windows a weaker next fixture class than transparent decor or accessory-like objects with isolated transparent submeshes

## Local external snapshot packet

Strongest local packet in this repo:

- [Enums.cs](../../../references/external/TS4SimRipper/src/Enums.cs): `SimGlass = 0x5EDA9CDE`
- [PreviewControl.cs](../../../references/external/TS4SimRipper/src/PreviewControl.cs): separate `GlassModel` array, `gotGlass` branch, mesh grouping, and morph application path for glass meshes
- [ColladaDAE.cs](../../../references/external/TS4SimRipper/src/ColladaDAE.cs): explicit `_glass_*` export naming and `SimGlass` suffix selection

Why this packet matters:

- it is stronger than our own preview code because it is an external creator-tool snapshot checked into the repo
- it shows that glass-family identity is not just a name in a list; it affects mesh grouping, export naming, and morph application flow

## Current candidate live targets

These are queue targets, not proof.

### Candidate group A: external tool packet

Best current target:

- the `TS4SimRipper` glass branch itself

Why it is first:

- it is the strongest local non-repo packet that already preserves glass-family identity through preview, export, and morph handling

### Candidate group B: local corpus packet

Useful local clues:

- `tmp/precomp_sblk_inventory.json`: `name_guess = "SimGlass"` with `occurrences = 1`
- `tmp/precomp_sblk_inventory.json`: `simglassCAS = 1`
- `tmp/precomp_sblk_inventory.json`: `name_guess = "SimGhostGlassCAS"` with `occurrences = 4`
- `tmp/precomp_shader_profiles.json`: named rows for `SimGlass`, `simglassCAS`, and `SimGhostGlassCAS`
- the current direct owning archaeology row for `SimGlass` is `hash_hex = 0xB6F2B1B1` / `3069358513` in `tmp/precomp_sblk_inventory.json`
- the same owning row survives in `tmp/precomp_shader_profiles.json` under `0xB6F2B1B1`

What this means safely:

- glass-family vocabulary survives in the local corpus
- the packet is narrow, which fits the current reading that `SimGlass` is real but edge-case
- the workspace now has one stable direct owning archaeology row for `SimGlass`, even though it is still not a live asset root
- the archaeology packet is noisy and should not be promoted to truth: named params around `0xB6F2B1B1` include mixed items like `uvMapping`, `samplerCASMedatorGridTexture`, `samplerBlueRampTexture`, `samplerSnowSharedNormals`, `VideoVTexture`, `WaterScrollSpeedLayer1`, and even obviously non-glass-looking names such as `FloorGridTexture`
- corpus presence still does not prove live authority order

### Candidate group C: build or object-side survey hints

Useful local clue:

- `tmp/probe_all_buildbuy_summary_full.json` currently includes `"SimGlass": 5`
- the same summary is built from `ProcessedEntries = 1380` resolved Build/Buy scene entries rather than from one narrow precompiled bucket
- the current full survey text artifact `tmp/probe_all_buildbuy.txt` is a root/header list rather than a family-annotated coverage table, so the `SimGlass` survey hit is currently stronger as an aggregate presence signal than as a directly extractable named row
- a broader `EP10` identity slice now gives a more useful candidate cluster than the earlier window-only packet:
  - `fishBowl_EP10GENmarimo_set1..6`
    - `OBJD` model candidate `01661233:00000000:3711431DFAE0318F`
    - `instance-swap32` match `01661233:00000000:FAE0318F3711431D`
    - same transformed root appears in `tmp/probe_all_buildbuy.txt`
  - `shelfFloor2x1_EP10TEAdisplayShelf_set1..10`
    - `OBJD` model candidate `01661233:00000000:25406B73E779C31F`
    - `instance-swap32` match `01661233:00000000:E779C31F25406B73`
    - same transformed root appears in `tmp/probe_all_buildbuy.txt`
  - `shelfFloor2x1_EP10TEAshopDisplayTileable_set1..8`
    - `OBJD` model candidate `01661233:00000000:F97A386193EE8A0C`
    - `instance-swap32` match `01661233:00000000:93EE8A0CF97A3861`
    - same transformed root appears in `tmp/probe_all_buildbuy.txt`
  - `lightWall_EP10GENlantern_set1..9`
    - `OBJD` model candidate `01661233:00000000:857F08D4F4A27FC1`
    - `instance-swap32` match `01661233:00000000:F4A27FC1857F08D4`
    - same transformed root appears in `tmp/probe_all_buildbuy.txt`
  - `mirrorWall1x1_EP10BATHsunrise_set1..10`
    - `OBJD` model candidate `01661233:00000000:1824BDDD3CD0344C`
    - `instance-swap32` match `01661233:00000000:3CD0344C1824BDDD`
    - same transformed root appears in `tmp/probe_all_buildbuy.txt`
- the same broader `EP10` slice also clarifies why windows are a weak forward path:
  - `window1X1_EP10TRADglassPanelTileable3Tile_*` and `window1X1_EP10TRADglassShortTileable2Tile_*` do appear frequently in the identity survey
  - but the current `EP10` survey does not surface a clean resolved `Model` candidate for them, unlike the fish-bowl/display/lantern/mirror cluster

Safe reading:

- the family now survives at a stronger TS4-facing layer than `name_guess` archaeology alone
- this is enough to keep the family on the candidate list as a survey-visible branch
- it is not enough to claim that Build/Buy `SimGlass` already means the same thing as character-side `SimGlass`
- it is not enough to outrank the external object-side transparent split by itself
- it is still not a row-level asset root
- the survey-backed next-step route is now narrower:
  - use the fish-bowl/display/lantern/mirror cluster first
  - do not treat the window-heavy packet as the best next extraction class just because its names say `glass`

Current negative extraction result:

- the first narrower `EP10` identity sweep against obvious glass-like internal names now gives a useful boundary case instead of a clean root:
  - object rows like `window2X1_EP10GENsliding2Tile`, `window2X1_EP10TRADwindowBox2Tile`, `mirrorWall1x1_EP10BATHsunrise`, `sculptFountainEmitterSingle1x1_EP10GARDstoneBowl`, and `sculptWall_EP10TRADwindowBars` do expose `OBJD` model candidates
  - but the first candidate roots checked from that packet did not resolve into Build/Buy asset roots through either exact lookup or the same `instance-swap32` transform that succeeded for the `RefractionMap` lily-pad fixture
  - that means the obvious-name `EP10` glass/window packet is not currently the cleanest row-level `SimGlass` extraction route

Safe reading:

- this is a bounded negative result, not evidence against `SimGlass`
- it narrows the next search boundary: do not spend the next packet on more obvious-name `EP10` windows first
- the next cleaner target should come from the already-proved Build/Buy survey packet or another package-level identity slice, not from one more pass over the same `EP10` names

Current reopen boundary:

- the broader `EP10` cluster above is better than the original window-only sweep, but it is still not a stable live fixture packet yet
- current direct reopen attempts on these transformed roots through `ProbeAsset` still fail with `Build/Buy asset not found`, including:
  - `01661233:00000000:FAE0318F3711431D`
  - `01661233:00000000:E779C31F25406B73`
  - `01661233:00000000:93EE8A0CF97A3861`
  - `01661233:00000000:F4A27FC1857F08D4`
  - `01661233:00000000:3CD0344C1824BDDD`
- that means these rows are currently survey-backed candidate roots, not reopenable stable fixtures like the named `RefractionMap` lily-pad bridge

Safe reading:

- candidate-resolution plus root-list presence is enough to rank the next search route
- it is not enough to claim a closed `SimGlass` live fixture
- current implementation inconsistency here is diagnostic boundary, not evidence that the candidate cluster is irrelevant
- the stronger survey-backed route is now promoted into its own packet:
  - [SimGlass EP10 Transparent-Decor Route](simglass-ep10-transparent-decor-route.md)

### Candidate group D: shell baseline controls

Useful local controls:

- bundled [TS4SimRipper Resources](../../../references/external/TS4SimRipper/src/Resources) include shell-like `.simgeom` controls such as `cuBodyComplete_lod0.simgeom`, `cuHead_lod0.simgeom`, `WaistFiller.simgeom`, `yfBodyComplete_lod0.simgeom`, and `ymBodyComplete_lod0.simgeom`
- current bounded reading for that bundled control set is still `SimSkin = 0x548394B9`, not `SimGlass`

Safe reading:

- the workspace now has a better shell-baseline control packet for glass-versus-shell comparison
- these are still controls, not glass fixtures

## What this packet is trying to prove

Exact target claim:

- if a real asset carries glass-family identity, that identity should stay preserved as a separate branch before broader shell or compositor logic is applied
- after that preservation step, the result should still converge into the same shared material/shader pipeline rather than a `BuildBuy`- or `CAS`-specific glass shader

Not being proved yet:

- exact slot contract for `SimGlass`
- exact ranking of `SimGlass` against `CASP`, embedded `MTNF`, or skintone in every case
- whether every transparent shell-like case is `SimGlass`

## Current implementation boundary

Current repo behavior is useful only as a diagnostic boundary:

- if current preview collapses glass-family content into broad shell or alpha fallback, that is an implementation limitation
- it is not evidence that `SimGlass` is not a real authority seam

Diagnostic value of this packet:

- it gives one concrete external-first counterweight against “generic alpha fallback” reasoning
- it gives a better standard for future shell-family investigations

## Best next inspection step

1. Use the `TS4SimRipper` glass branch as the baseline external packet.
2. Compare that packet against one stronger TS4-facing survey hint:
   - `tmp/probe_all_buildbuy_summary_full.json`: `"SimGlass": 5`
3. Compare that packet against one candidate local corpus branch:
   - `SimGlass`
   - `simglassCAS`
   - `SimGhostGlassCAS`
4. Keep the bundled `SimSkin` shell controls as the comparison floor for the shell side of the seam.
5. Treat the current survey hit as aggregate-only until a family-annotated row or named object linkage is extracted.
6. Keep the current `EP10` obvious glass/window object sweep as a negative control, not as the main forward path.
7. Use the broader `EP10` transparent-decor cluster first:
   - `fishBowl_EP10GENmarimo`
   - `shelfFloor2x1_EP10TEAdisplayShelf`
   - `shelfFloor2x1_EP10TEAshopDisplayTileable`
   - `lightWall_EP10GENlantern`
   - `mirrorWall1x1_EP10BATHsunrise`
8. Treat the transformed roots in that cluster as survey-backed search anchors until one of them can be reopened cleanly as a stable fixture.
9. Classify any reopened transparent object against the object-side transparent branches before attaching it to the `SimGlass` row.
10. Only after that fails should the search widen back out to other package slices.

## Honest limit

This packet does not yet prove full live authority order.

What it does prove:

- `SimGlass` is strong enough to keep as a real edge-family branch
- the current workspace now also sees `SimGlass` at Build/Buy survey level, not only in external-tool and precompiled packets
- the first obvious-name `EP10` extraction pass is now also honestly bounded as a negative control rather than an open-ended "maybe this window root works" bucket
- creator-facing TS4 transparency guidance now makes the search class narrower too: `SimGlass` is tied more strongly to transparent CAS-like content than to generic architectural glass
- the current `EP10` survey now also gives one broader candidate cluster for the next object-side pass: fish-bowl, display-shelf, shop-display, lantern, and mirror roots beat the window-heavy packet as the next search route
- those transformed roots are still candidate anchors rather than closed fixtures, because current direct reopen attempts do not yet reconstruct them as Build/Buy assets
- the current evidence ceiling for that whole Build/Buy side is now frozen separately:
  - [SimGlass Build/Buy Evidence Limit](simglass-buildbuy-evidence-limit.md)
- the current winning-branch burden for the first valid `Build/Buy SimGlass` fixture is now frozen separately too:
  - [SimGlass Build/Buy Promotion Gate](simglass-buildbuy-promotion-gate.md)
- the branch-specific losing conditions and winning record are now frozen separately too:
  - [SimGlass Build/Buy Disqualifiers](simglass-buildbuy-disqualifiers.md)
  - [SimGlass Build/Buy Winning Signals](simglass-buildbuy-winning-signals.md)
  - [SimGlass Build/Buy Outcome Ladder](simglass-buildbuy-outcome-ladder.md)
  - [SimGlass Build/Buy Mixed-Signal Resolution](simglass-buildbuy-mixed-signal-resolution.md)
  - [SimGlass Build/Buy Provisional Candidate Checklist](simglass-buildbuy-provisional-candidate-checklist.md)
  - [SimGlass Build/Buy Winning Fixture Checklist](simglass-buildbuy-winning-fixture-checklist.md)
- current repo approximation must be judged against that preserved identity, not against a generic fallback story
