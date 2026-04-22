# TS4 Material Research Restart Guide

Use this file when the material/shader research task is resumed in a new chat.

This is a continuation guide for the external-first TS4 material, texture, shader, and UV research track. It is not an implementation guide.

## Task goal

Build a durable knowledge base for how TS4 materials actually work across the whole game corpus by prioritizing external sources, creator tooling, and externally sourced local snapshots, then distilling those findings into bounded family and authority documents.

Corpus-wide priority rule:

- do not let one convenient package slice, pack, or fixture route drive the overall research queue
- set priorities from the whole-game view first:
  - which shader/material families exist at all
  - how often they appear across the corpus
  - how important they are to correct rendering/material behavior
  - how strong the external evidence is
  - how directly the result can be turned into an implementation-ready spec
- use pack-specific lanes such as `EP10` only as secondary validation/evidence layers after the wider family priority is already clear

## Red lines

- do not treat current repo code as the source of truth for TS4 material behavior
- do not rewrite external evidence so it matches the current broken implementation
- do not promote local decoder output, corpus buckets, or probe summaries into proof
- do not collapse narrow edge families back into generic diffuse, alpha, shell, or fallback stories without external proof

## Trust ladder

Use sources in this order:

1. external TS4 creator-facing or format-facing sources
2. local snapshots of external tools checked into the repo, such as `TS4SimRipper`
3. local corpus and precompiled summaries only for candidate-target isolation
4. current repo code only as implementation boundary, failure boundary, or hypothesis source

Safe rule:

- if a statement depends mainly on current repo behavior, phrase it as a repo limitation or current boundary, not as TS4 truth
- if a statement depends mainly on where an input was discovered (`BuildBuy`, `CAS`, `Sim`), do not turn that into a domain-specific shader rule
- if a statement depends mainly on one package slice or one convenient fixture cluster, do not let that package slice define the whole queue or the general shader/material priority

## Execution mode

Primary mode:

- treat each normal run as one autonomous long batch, not as one tiny packet plus a forced stop
- in one run, complete multiple bounded packets in sequence when the next packet is already clear from the docs and does not require a risky reframe
- choose those packets from the widest safe family priority first, not from the most convenient single-pack route
- after each packet, sync the smallest continuity layer that keeps restart safety:
  - the packet itself
  - queue, matrix, and plan layers as needed
- stop the batch only at one of these boundaries:
  - a real blocker
  - a context-safety boundary
  - a natural integration checkpoint where the next packet would require a fresh read of the evidence stack

Recovery mode:

- if the run is interrupted, resumed, or compacted, restart from this guide plus `current-plan`, not from chat memory
- a thread heartbeat may exist as recovery insurance, but it is not the primary work cadence
- recovery runs should continue the same long-batch mode after rebuilding context from the docs

Expected closeout for each run:

- separate `externally confirmed`, `local snapshot of external tooling` or `local package evidence`, `bounded synthesis`, `blockers`, and `next highest-priority step`
- report progress only for the rubrics actually advanced in that run
- when a status snapshot is requested, report only the progress-bearing material docs:
  - deep dives with explicit numeric status
  - family sheets with explicit numeric status
  - live-proof packets with explicit numeric status
- do not include core guides, hubs, tracking docs, or source-layer census docs in the percentage status view
- use this exact visual status format:
  - `🟩` for `100%`
  - `🟨` for `67%` and above
  - `🟧` for `34%` to `66%`
  - `🟥` for `33%` and below
- print each status row as:
  - `icon document-name current%`
  - append change only when the current run changed that document:
    - `(+12%)`
- end the status snapshot with one overall catalog-fill percentage computed from the current percentages of the progress-bearing docs only
- when helper-family tagged sessions are discussed, describe target and control as equally tagged sessions unless one side genuinely lacks usable metadata and is being rejected

## Working route

Resume in this order:

1. [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md)
2. [Corpus-Wide Family Priority](corpus-wide-family-priority.md)
3. [Corpus-Wide Family Census Baseline](corpus-wide-family-census-baseline.md)
4. [MATD Shader Census Baseline](matd-shader-census-baseline.md)
5. [Sim Archetype Material Carrier Census](sim-archetype-material-carrier-census.md)
6. [CAS Carrier Census Baseline](cas-carrier-census-baseline.md)
7. [CASPart Linkage Census Baseline](caspart-linkage-census-baseline.md)
8. [CASPart GEOM Shader Census Baseline](caspart-geom-shader-census-baseline.md)
9. [CASPart Parser Boundary](caspart-parser-boundary.md)
10. [CASPart GEOM Resolution Boundary](caspart-geom-resolution-boundary.md)
11. [Shader Family Registry](shader-family-registry.md)
12. [Edge-Family Matrix](edge-family-matrix.md)
13. [P1 Live-Proof Queue](p1-live-proof-queue.md)
14. [Build/Buy Material Authority Matrix](buildbuy-material-authority-matrix.md)
15. [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
16. [Build/Buy Stateful Material-Set Seam](buildbuy-stateful-material-set-seam.md)
17. [Documentation Status Catalog](documentation-status-catalog.md)
18. [Live-Proof Packets](live-proof-packets/README.md)

Working-route interpretation:

- first rebuild the whole-game family priority from the shared guide, registry, matrix, and queue
- first rebuild the whole-game family priority and census floor from the shared guide, census companion, `MATD` census companion, registry, matrix, and queue
- only after that choose one bounded packet
- treat package-specific clusters as evidence lanes chosen in service of the wider queue, not as queue drivers by themselves

Use these supporting layers as needed:

- [Family Sheets](family-sheets/README.md)
- [Body And Head Shell Authority Table](body-head-shell-authority-table.md)
- [BodyType Translation Boundary](bodytype-translation-boundary.md)
- [BodyType 0x44 Family Boundary](live-proof-packets/bodytype-0x44-family-boundary.md)
- [BodyType 0x41 Family Boundary](live-proof-packets/bodytype-0x41-family-boundary.md)
- [BodyType 0x6D Family Boundary](live-proof-packets/bodytype-0x6d-family-boundary.md)
- [BodyType 0x6F Family Boundary](live-proof-packets/bodytype-0x6f-family-boundary.md)
- [BodyType 0x52 Family Boundary](live-proof-packets/bodytype-0x52-family-boundary.md)
- [BodyType 0x80 Family Boundary](live-proof-packets/bodytype-0x80-family-boundary.md)
- [CompositionMethod And SortLayer Boundary](compositionmethod-sortlayer-boundary.md)
- [CompositionMethod Census Baseline](compositionmethod-census-baseline.md)
- [Overlay And Detail Family Authority Table](overlay-detail-family-authority-table.md)
- [SortLayer Census Baseline](sortlayer-census-baseline.md)
- [Build/Buy Transparent Object Fallback Ladder](buildbuy-transparent-object-fallback-ladder.md)
- [Build/Buy Transparent Object Authority Order](buildbuy-transparent-object-authority-order.md)
- [SimGlass Build/Buy Evidence Order](simglass-buildbuy-evidence-order.md)
- [Object Glass And Transparency](family-sheets/object-glass-and-transparency.md)
- [Skintone And Overlay Compositor](skintone-and-overlay-compositor.md)
- [External GPU Scene-Pass Baseline](external-gpu-scene-pass-baseline.md)
- [TS4 DX11 Context-Tagged Capture Contract](../../../satellites/ts4-dx11-introspection/docs/context-tagged-capture-contract.md)
- [TS4 DX11 Context-Tagged Capture Recipes](../../../satellites/ts4-dx11-introspection/docs/context-tagged-capture-recipes.md)
- [TS4 DX11 Context-Tagged Capture Analysis Workflow](../../../satellites/ts4-dx11-introspection/docs/context-tagged-capture-analysis-workflow.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)
- [Open questions](../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Current state

These layers already exist and should be continued, not replaced:

- external-first `Build/Buy Material Authority Matrix`
- external-first `Shader Family Registry`
- `Family Sheets` for narrow family packets
- `Edge-Family Matrix` for row-by-row authority synthesis
- `P1 Live-Proof Queue` for candidate-target ordering
- `Live-Proof Packets` for concrete inspection packets

Current concrete live-proof packets:

- [Build/Buy MTST Default-State Boundary](live-proof-packets/buildbuy-mtst-default-state-boundary.md)
- [Build/Buy MTST Portable-State Delta](live-proof-packets/buildbuy-mtst-portable-state-delta.md)
- [Build/Buy MTST State-Selector Structure](live-proof-packets/buildbuy-mtst-state-selector-structure.md)
- [Build/Buy Transparent Object Classification Boundary](live-proof-packets/buildbuy-transparent-object-classification-boundary.md)
- [Build/Buy Transparent Object Candidate State Ladder](live-proof-packets/buildbuy-transparent-object-candidate-state-ladder.md)
- [Build/Buy Transparent-Decor Route](live-proof-packets/buildbuy-transparent-decor-route.md)
- [Build/Buy Transparent Object Fixture Promotion Boundary](live-proof-packets/buildbuy-transparent-object-fixture-promotion-boundary.md)
- [Build/Buy Transparent Object Mixed-Signal Resolution](live-proof-packets/buildbuy-transparent-object-mixed-signal-resolution.md)
- [Build/Buy Transparent Object Reopen Checklist](live-proof-packets/buildbuy-transparent-object-reopen-checklist.md)
- [Build/Buy Transparent Object Route Stall Boundary](live-proof-packets/buildbuy-transparent-object-route-stall-boundary.md)
- [Build/Buy Transparent Object Target Priority](live-proof-packets/buildbuy-transparent-object-target-priority.md)
- [Build/Buy Transparent Object Top-Anchor Negative Reopen](live-proof-packets/buildbuy-transparent-object-top-anchor-negative-reopen.md)
- [Build/Buy Transparent Object Lower-Anchor Negative Reopen](live-proof-packets/buildbuy-transparent-object-lower-anchor-negative-reopen.md)
- [Build/Buy Transparent Object Full-Route Stall](live-proof-packets/buildbuy-transparent-object-full-route-stall.md)
- [Build/Buy Transparent Object Post-Top-Anchor Handoff](live-proof-packets/buildbuy-transparent-object-post-top-anchor-handoff.md)
- [Build/Buy Window-Heavy Transparent Negative Control](live-proof-packets/buildbuy-window-heavy-transparent-negative-control.md)
- [SimGlass Versus Shell Baseline](live-proof-packets/simglass-vs-shell-baseline.md)
- [SimGlass Build/Buy Evidence Limit](live-proof-packets/simglass-buildbuy-evidence-limit.md)
- [SimGlass Build/Buy Promotion Gate](live-proof-packets/simglass-buildbuy-promotion-gate.md)
- [SimGlass Build/Buy Disqualifiers](live-proof-packets/simglass-buildbuy-disqualifiers.md)
- [SimGlass Build/Buy Winning Signals](live-proof-packets/simglass-buildbuy-winning-signals.md)
- [SimGlass Build/Buy Outcome Ladder](live-proof-packets/simglass-buildbuy-outcome-ladder.md)
- [SimGlass Build/Buy Mixed-Signal Resolution](live-proof-packets/simglass-buildbuy-mixed-signal-resolution.md)
- [SimGlass Build/Buy Provisional Candidate Checklist](live-proof-packets/simglass-buildbuy-provisional-candidate-checklist.md)
- [SimGlass Build/Buy Winning Fixture Checklist](live-proof-packets/simglass-buildbuy-winning-fixture-checklist.md)
- [SimGlass EP10 Transparent-Decor Route](live-proof-packets/simglass-ep10-transparent-decor-route.md)
- [SimSkin Body/Head Shell Authority](live-proof-packets/simskin-body-head-shell-authority.md)
- [BodyType 0x44 Family Boundary](live-proof-packets/bodytype-0x44-family-boundary.md)
- [BodyType 0x41 Family Boundary](live-proof-packets/bodytype-0x41-family-boundary.md)
- [BodyType 0x6D Family Boundary](live-proof-packets/bodytype-0x6d-family-boundary.md)
- [BodyType 0x6F Family Boundary](live-proof-packets/bodytype-0x6f-family-boundary.md)
- [BodyType 0x52 Family Boundary](live-proof-packets/bodytype-0x52-family-boundary.md)
- [BodyType 0x80 Family Boundary](live-proof-packets/bodytype-0x80-family-boundary.md)
- [SimSkin Versus SimSkinMask](live-proof-packets/simskin-vs-simskinmask.md)
- [CASHotSpotAtlas Carry-Through](live-proof-packets/cas-hotspotatlas-carry-through.md)
- [ShaderDayNightParameters Visible-Pass Proof](live-proof-packets/shader-daynight-visible-pass.md)
- [GenerateSpotLightmap And NextFloorLightMapXform](live-proof-packets/generate-spotlightmap-nextfloorlightmapxform.md)
- [RefractionMap Live Proof](live-proof-packets/refractionmap-live-proof.md)
- [Refraction Post-LilyPad Pivot](live-proof-packets/refraction-post-lilypad-pivot.md)
- [Refraction Next-Route Priority](live-proof-packets/refraction-next-route-priority.md)
- [Refraction 0389 Clean-Route Baseline](live-proof-packets/refraction-0389-clean-route-baseline.md)
- [Refraction 0124 Mixed-Control Floor](live-proof-packets/refraction-0124-mixed-control-floor.md)
- [Refraction 0389 Identity Gap](live-proof-packets/refraction-0389-identity-gap.md)
- [Refraction 0389 Versus LilyPad Floor](live-proof-packets/refraction-0389-vs-lilypad-floor.md)
- [Refraction 0389 No Signal Upgrade](live-proof-packets/refraction-0389-no-signal-upgrade.md)
- [Refraction Post-0389 Handoff Boundary](live-proof-packets/refraction-post-0389-handoff-boundary.md)

## Immediate next work

Resume from the widest unfinished family priority, not from the most convenient package lane:

1. rebuild whole-game family priority first:
   - use [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md)
   - use [Shader Family Registry](shader-family-registry.md)
   - use [Edge-Family Matrix](edge-family-matrix.md)
   - use [P1 Live-Proof Queue](p1-live-proof-queue.md)
2. choose the next bounded packet by these factors together:
   - whole-corpus prevalence
   - rendering/material importance
   - external evidence strength
   - cross-domain representativeness
   - implementation-spec value
3. treat pack-specific lanes only as secondary validation:
   - `EP10`, `EP11`, and similar slices may still supply good fixtures
   - they must not define the global queue by themselves
4. current safe whole-game priority is:
    - complete corpus-wide family prioritization and queue wording first if it drifts
    - rebuild that priority from the fresh full-scan census baseline, not from the older partial live-cache totals
    - then use the widest unfinished high-value family track:
      - object-side transparency
      - `SimSkin` and character compositor-authority
    - only then select one representative fixture lane
    - the current restart-safe character anchor inside that track is:
      - [SimSkin Body/Head Shell Authority](live-proof-packets/simskin-body-head-shell-authority.md)
      - [Body And Head Shell Authority Table](body-head-shell-authority-table.md)
      - [CompositionMethod Census Baseline](compositionmethod-census-baseline.md)
      - [Overlay And Detail Family Authority Table](overlay-detail-family-authority-table.md)
      - [SortLayer Census Baseline](sortlayer-census-baseline.md)
5. current narrow lanes stay bounded as evidence only:
   - the `RefractionMap` route is already fully bounded at the current inspection layer
   - the `EP10` transparent-decor route is already exhausted at the current inspection layer
   - `SimGlass` is now directly counted on the character side, but it remains much narrower than `SimSkin`
   - the new `SimSkin` shell packet now makes body/head shell authority the first character-side follow-up before narrower edge lanes
   - overlay/detail ordering now also has its own explicit table instead of living only as prose inside the skintone/compositor doc
   - `sort_layer` now has a direct shard-backed count layer and `CompositionMethod` now has a direct whole-install package count layer
- the `composition_method` column is now also populated in the shard set
- the next character-side bottleneck is translation of the remaining large mixed `BodyType` families by high byte
- the two biggest apparel-heavy families now already have their own packet layers:
  - [BodyType 0x44 Family Boundary](live-proof-packets/bodytype-0x44-family-boundary.md)
  - [BodyType 0x41 Family Boundary](live-proof-packets/bodytype-0x41-family-boundary.md)
- the next two semantically concentrated mixed families now also have their own packet layers:
  - [BodyType 0x6D Family Boundary](live-proof-packets/bodytype-0x6d-family-boundary.md)
  - [BodyType 0x6F Family Boundary](live-proof-packets/bodytype-0x6f-family-boundary.md)
- the next queued families now also have their own packet layers:
  - [BodyType 0x52 Family Boundary](live-proof-packets/bodytype-0x52-family-boundary.md)
  - [BodyType 0x80 Family Boundary](live-proof-packets/bodytype-0x80-family-boundary.md)
   - the leading external hypothesis for that high-byte layer is now `AdditionalTextureSpace` or a closely related secondary texture-space signal
   - neither lane should keep driving the main queue without a new inspection layer and a corpus-level reason
6. for the weak helper-family rows, do not reopen broad unlabeled DX11 sessions:
   - use [TS4 DX11 Context-Tagged Capture Contract](../../../satellites/ts4-dx11-introspection/docs/context-tagged-capture-contract.md)
   - `run-live-capture.ps1` can now write `context-tags.json` directly through context-tag parameters
   - a manual `context-tags.json` sidecar is still acceptable until manifest tagging lands in tooling
   - the next honest runtime uplift for those rows is tagged capture, not more broad-session rereading
6. when a narrow lane is chosen, preserve its status honestly:
    - use it to validate or falsify a family-level claim
    - do not let it silently become the new research priority just because it has convenient assets

Current census floor to preserve:

- the fresh whole-install census lives in [Corpus-Wide Family Census Baseline](corpus-wide-family-census-baseline.md)
- the first direct object-side shader-profile census lives in [MATD Shader Census Baseline](matd-shader-census-baseline.md)
- the first direct graph-backed character-side carrier census lives in [Sim Archetype Material Carrier Census](sim-archetype-material-carrier-census.md)
- the first broad whole-`CAS` slot/fact census lives in [CAS Carrier Census Baseline](cas-carrier-census-baseline.md)
- the first direct package-derived `CASPart -> GEOM/texture/region_map` linkage floor now lives in [CASPart Linkage Census Baseline](caspart-linkage-census-baseline.md)
- the first direct package-derived `CASPart -> GEOM -> shader family` floor now lives in [CASPart GEOM Shader Census Baseline](caspart-geom-shader-census-baseline.md)
- the first direct whole-install `CompositionMethod` floor now lives in [CompositionMethod Census Baseline](compositionmethod-census-baseline.md)
- the first direct `BodyType` translation boundary above that floor now lives in [BodyType Translation Boundary](bodytype-translation-boundary.md)
- the resumable runner and safe-point root for that census are:
  - `tmp/caspart_geom_shader_census_resumable.ps1`
  - `tmp/caspart_geom_shader_census_run/`
- the current integrity ceilings for that character-side stack now also live in:
  - [CASPart Parser Boundary](caspart-parser-boundary.md)
  - [CASPart GEOM Resolution Boundary](caspart-geom-resolution-boundary.md)
- do not fall back to the older `1240 / 161303 / 1125911` cache layer as if it were the current whole-game baseline
- keep the current integrity note explicit:
  - full scan selected `4965` package files
  - the shard set currently persists `4963` package rows
  - `EP18\\ClientFullBuild0.package` and `EP18\\SimulationFullBuild0.package` are currently missing from the persisted package tables
- keep the current `MATD` census boundary explicit:
  - `28225` object-side `MaterialDefinition` rows were counted
  - `28201` decode to embedded `MATD`
  - `24` are currently zero-length decoded rows
  - this is real object-side prevalence, not yet whole-game family prevalence
- keep the current `Sim archetype` carrier-census boundary explicit:
  - `38` supported archetype graphs were counted
  - `15` surface skintone render carriers
  - `10` surface base-texture carriers
  - `10` surface overlays
  - this is a direct character-side carrier layer, not yet a whole `CAS/Sim` family census
- keep the current whole-`CAS` carrier-census boundary explicit:
  - `530507` `CAS` assets and `299028` `cas_part_facts` were counted directly
  - this is already a strong whole-`CAS` prevalence layer
  - the current all-zero geometry/material booleans on `CAS` asset rows are an index boundary, not a semantic result
  - that index boundary is now complemented by a direct package-derived linkage floor
- keep the current `CASPart` linkage boundary explicit:
  - `530507` raw `CASPart` rows were counted
  - `299028` rows currently parse through the structured linkage path
  - those parsed rows already surface `281303` geometry candidates, `236668` texture candidates, and `108906` `region_map` candidates
  - `230713` rows still sit outside the current structured parser boundary
  - the current direct linkage floor comes from the recovery JSON/script path, not from a rebuilt `ProbeAsset` command
  - `dotnet build` for `ProbeAsset` is currently blocked by workload resolver failures captured in `tmp/probeasset_build_diag.txt`
- keep the current completed `CASPart -> GEOM -> family` boundary explicit:
  - `RowsWithResolvedGeometryShader = 281271`
  - `RowsWithUnknownGeometryShader = 32`
  - `GeometryResolvedFromExternalPackage = 12911`
  - `SimSkin = 280983` across `401` packages by `CASPart` rows
  - `SimGlass = 6048` across `189` packages by `CASPart` rows
  - the old same-package geometry-resolution ceiling is no longer the active blocker for this census
  - the residual geometry-resolution gap is `GeometryKeyNotIndexed = 531`
- keep the new cache/query state explicit:
  - `composition_method` is now populated in all four shard databases
  - all four shard databases now report `seed_fact_content_version = 2026-04-21.seed-facts-v2`
  - direct package counts and ordinary SQLite queries now agree on the same top `CompositionMethod` values
- keep the new `BodyType` translation boundary explicit:
  - low-value rows like `10` and `28` now align directly with external enum names: `Earrings` and `FacialHair`
  - the biggest rows such as `1140850688`, `1090519040`, and `1090519046` still behave as mixed high-bit buckets, not ordinary readable slots
  - the unresolved space should now be read family-by-family by high byte, not as one flat set of decimal values
  - the strongest external lead for those families is now a separate texture-space vocabulary rather than a hidden ordinary slot enum
  - the first two family-specific continuation packets now live in:
    - [BodyType 0x44 Family Boundary](live-proof-packets/bodytype-0x44-family-boundary.md)
    - [BodyType 0x41 Family Boundary](live-proof-packets/bodytype-0x41-family-boundary.md)
  - the next two continuation packets now also live in:
    - [BodyType 0x6D Family Boundary](live-proof-packets/bodytype-0x6d-family-boundary.md)
    - [BodyType 0x6F Family Boundary](live-proof-packets/bodytype-0x6f-family-boundary.md)
  - the next queued continuation packets now also live in:
    - [BodyType 0x52 Family Boundary](live-proof-packets/bodytype-0x52-family-boundary.md)
    - [BodyType 0x80 Family Boundary](live-proof-packets/bodytype-0x80-family-boundary.md)

Safe continuation rule:

- finish the next concrete packet and then fold only the proved part back into the matrix and registry layers
- keep the architectural split explicit:
  - domain-specific discovery and authority order are allowed
  - shared shader/material semantics after canonical-material decoding are the target

## Expected packet shape

Each new live-proof packet should answer:

1. what is already externally proved
2. what local candidate target is being inspected next
3. what exact claim the packet is trying to prove or falsify
4. what current implementation mistake becomes easier to diagnose after that proof

## Mandatory update targets after each run

When a packet advances, update the smallest set that preserves restart continuity:

- the packet itself under `live-proof-packets/`
- [Live-Proof Packets](live-proof-packets/README.md)
- [P1 Live-Proof Queue](p1-live-proof-queue.md)
- [Edge-Family Matrix](edge-family-matrix.md)
- [Current plan](../../planning/current-plan.md)

Current refraction named-fixture anchor:

- `EP10\\ClientFullBuild0.package | sculptFountainSurface3x3_EP10GENlilyPad`
- `ObjectDefinition = C0DB5AE7:00000000:000000000003FC7F`
- `ObjectCatalog = 319E4F1D:00000000:000000000003FC7F`
- `OBJD model candidate = 01661233:00000000:FDD2F1F700F643B0`
- `resolved model root = 01661233:00000000:00F643B0FDD2F1F7` via `instance-swap32`

Current broader `SimGlass` candidate cluster:

- `fishBowl_EP10GENmarimo_set1..6`
  - `OBJD model candidate = 01661233:00000000:3711431DFAE0318F`
  - transformed root = `01661233:00000000:FAE0318F3711431D`
- `shelfFloor2x1_EP10TEAdisplayShelf_set1..10`
  - `OBJD model candidate = 01661233:00000000:25406B73E779C31F`
  - transformed root = `01661233:00000000:E779C31F25406B73`
- `shelfFloor2x1_EP10TEAshopDisplayTileable_set1..8`
  - `OBJD model candidate = 01661233:00000000:F97A386193EE8A0C`
  - transformed root = `01661233:00000000:93EE8A0CF97A3861`
- `lightWall_EP10GENlantern_set1..9`
  - `OBJD model candidate = 01661233:00000000:857F08D4F4A27FC1`
  - transformed root = `01661233:00000000:F4A27FC1857F08D4`
- `mirrorWall1x1_EP10BATHsunrise_set1..10`
  - `OBJD model candidate = 01661233:00000000:1824BDDD3CD0344C`
  - transformed root = `01661233:00000000:3CD0344C1824BDDD`
- all five transformed roots are present in `tmp/probe_all_buildbuy.txt`
- current direct reopen attempts still fail, so this is a narrowed route for the next packet, not a closed live-proof result

Update broader indexes only if navigation changed:

- [Material Pipeline Deep Dives](README.md)
- [Documentation Status Catalog](documentation-status-catalog.md)
- [Knowledge Map](../../knowledge-map.md)
- [Documentation Map](../../README.md)

## Report format contract

End each run with a compact tree-style status report.

Rules:

- put the colored square at the start of the line
- show increments only for sections that actually changed in that run
- do not show increments on untouched rows
- do not show increments on `100%` rows
- prefer the narrow subtree that actually moved over a full giant matrix dump

Expected shape:

```text
Shared TS4 Material / Texture / UV Pipeline
├─ 🟨 Shared guide -> partial; architecture baseline closed
├─ 🟨 Build/Buy Material Authority Matrix -> 50% to 93%
├─ 🟨 Build/Buy Stateful Material-Set Seam -> 44% to 90%
├─ 🟨 CAS/Sim Material Authority Matrix -> 43% to 88%
├─ 🟨 Shader Family Registry -> 39% to 83%
├─ 🟨 Skintone And Overlay Compositor -> 38% to 86%
├─ 🟨 Edge-Family Matrix -> 57% to 91%
├─ 🟨 P1 Live-Proof Queue -> 66% to 84%
├─ 🟨 Family Sheets
│  ├─ 🟨 SimSkin / SimGlass / SimSkinMask -> 27% to 81%
│  ├─ 🟨 CASHotSpotAtlas -> 19% to 86%
│  ├─ 🟥 ShaderDayNightParameters -> 15% to 66%
│  ├─ 🟥 Projection / Reveal / Lightmap families -> 14% to 63%
│  └─ 🟥 GenerateSpotLightmap / NextFloorLightMapXform -> 12% to 71%
└─ 🟨 Live-Proof Packets
   ├─ 🟥 Build/Buy MTST Default-State Boundary -> 24% to 89%
   ├─ 🟥 Build/Buy MTST Portable-State Delta -> 29% to 88%
   ├─ 🟥 SimGlass Versus Shell Baseline -> 24% to 87%
   ├─ 🟨 SimSkin Versus SimSkinMask -> 53% to 84%
   ├─ 🟥 CASHotSpotAtlas Carry-Through -> 28% to 89%
   ├─ 🟨 ShaderDayNight Visible-Pass Proof -> 36% to 79%
   ├─ 🟥 GenerateSpotLightmap / NextFloorLightMapXform -> 18% to 77%
   └─ 🟥 RefractionMap Live Proof -> 21% to 86%
```

## Multi-agent guidance

Multiple agents are allowed when they help, but keep them bounded:

- use explorers for narrow evidence lookup or codebase location questions
- keep write ownership disjoint if workers are used
- do not delegate the immediate blocking research judgment
- if agents stall, continue locally instead of blocking the whole run

## One-sentence continuity rule

External evidence defines the knowledge; local repo behavior only explains where the current implementation still fails.
