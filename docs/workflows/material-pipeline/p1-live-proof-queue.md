# P1 Live-Proof Queue

This document is the working queue for the next high-value live-proof packets in the TS4 material research track.

If the task is being resumed in a new chat, read [Research Restart Guide](research-restart-guide.md) first so the queue is not mistaken for a truth layer.

Primary rule:

- candidate targets are not proof
- local coverage and corpus files are used here only to pick what to inspect next
- externally backed family identity still lives in the family sheets and matrix docs
- domain- or asset-heavy candidates in this queue are authority-discovery hints only; any proved result still has to converge into the shared shader/material contract

Related docs:

- [Research Restart Guide](research-restart-guide.md)
- [Material Pipeline Deep Dives](README.md)
- [Build/Buy Material Authority Matrix](buildbuy-material-authority-matrix.md)
- [Edge-Family Matrix](edge-family-matrix.md)
- [Live-Proof Packets](live-proof-packets/README.md)
- [Family Sheets](family-sheets/README.md)
- [Shader Family Registry](shader-family-registry.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [Open questions](../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Queue status (`v0.1`)

```text
P1 Live-Proof Queue
├─ SimGlass versus shell baseline ~ 66%
├─ SimSkin versus SimSkinMask ~ 71%
├─ CASHotSpotAtlas carry-through ~ 68%
├─ ShaderDayNightParameters visible-pass proof ~ 72%
├─ GenerateSpotLightmap / NextFloorLightMapXform ~ 66%
└─ RefractionMap live proof ~ 84%
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

## P1 rows

### 1. `SimGlass` versus shell baseline

Externally backed identity:

- `SimGlass` is a real narrow glass-family branch in local external `TS4SimRipper` snapshots and Sims-lineage shader docs

Current local candidate packet:

- [TS4SimRipper Enums.cs](../../references/external/TS4SimRipper/src/Enums.cs)
- [TS4SimRipper ColladaDAE.cs](../../references/external/TS4SimRipper/src/ColladaDAE.cs)
- [TS4SimRipper PreviewControl.cs](../../references/external/TS4SimRipper/src/PreviewControl.cs)
- `tmp/precomp_sblk_inventory.json` currently includes `name_guess = "SimGlass"` with a narrow occurrence packet
- `tmp/probe_all_buildbuy_summary_full.json` now records `"SimGlass": 5` across the resolved Build/Buy survey
- creator-facing transparency guidance now also strengthens the family shape:
  - [Transparency in clothing tutorial](https://maxismatchccworld.tumblr.com/post/645249485712326656/transparency-in-clothing-tutorial)
  - [Semi-Square Eyeglasses](https://kijiko-catfood.com/semi-square-eyeglasses/)
  - [Lashes and hair cc clashing](https://forums.ea.com/discussions/the-sims-4-mods-and-custom-content-en/lashes-and-hair-cc-clashing-pics-included-please-help-/12047424)
- bundled shell-control `.simgeom` fixtures in `docs/references/external/TS4SimRipper/src/Resources/` now give a cleaner `SimSkin` baseline for glass-versus-shell comparison
- the first narrower `EP10` identity sweep against obvious glass/window object names is now a useful negative control: checked candidate model refs from `window2X1_EP10GENsliding2Tile`, `window2X1_EP10TRADwindowBox2Tile`, `mirrorWall1x1_EP10BATHsunrise`, `sculptFountainEmitterSingle1x1_EP10GARDstoneBowl`, and `sculptWall_EP10TRADwindowBars` did not resolve to Build/Buy asset roots through either exact lookup or `instance-swap32`
- a broader `EP10` survey-backed cluster now gives a better next-step route than the window-heavy packet:
  - `fishBowl_EP10GENmarimo -> 01661233:00000000:FAE0318F3711431D`
  - `shelfFloor2x1_EP10TEAdisplayShelf -> 01661233:00000000:E779C31F25406B73`
  - `shelfFloor2x1_EP10TEAshopDisplayTileable -> 01661233:00000000:93EE8A0CF97A3861`
  - `lightWall_EP10GENlantern -> 01661233:00000000:F4A27FC1857F08D4`
  - `mirrorWall1x1_EP10BATHsunrise -> 01661233:00000000:3CD0344C1824BDDD`
- those transformed roots already appear in `tmp/probe_all_buildbuy.txt`, but current direct reopen attempts still fail with `Build/Buy asset not found`, so they are candidate anchors rather than stable fixtures

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

Best current next target:

- start from the external `TS4SimRipper` glass-family code path, then use the Build/Buy survey hit as the stronger TS4-facing hint layer before falling back to narrow precompiled packets
- treat the current Build/Buy survey hit as aggregate-only until one family-annotated row or object-name linkage is extracted
- keep the current `EP10` obvious-name window packet as a negative boundary
- use the broader transparent-decor cluster next: `fishBowl`, `displayShelf`, `shopDisplayTileable`, `lantern`, then `mirror`
- treat those roots as survey-backed search anchors until one reopens as a stable live fixture

Current packet:

- [SimGlass Versus Shell Baseline](live-proof-packets/simglass-vs-shell-baseline.md)

### 2. `SimSkin` versus `SimSkinMask`

Externally backed identity:

- `SimSkin` is a real GEOM-side skin family
- `SimSkinMask` is still only mask-adjacent semantics until a peer geometry branch is found

Current local candidate packet:

- bundled `.simgeom` resources in [TS4SimRipper Resources](../../references/external/TS4SimRipper/src/Resources)
- `tmp/precomp_sblk_inventory.json` includes `simskin = 51` and `SimSkinMask = 12`

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

Current packet:

- [SimSkin Versus SimSkinMask](live-proof-packets/simskin-vs-simskinmask.md)

### 3. `CASHotSpotAtlas` carry-through

Externally backed identity:

- `CASHotSpotAtlas` is a real EA hotspot atlas mapped to `UV1` and tied to morph or slider logic

Current local candidate packet:

- [CASHotSpotAtlas family sheet](family-sheets/cas-hotspot-atlas.md)
- `tmp/precomp_sblk_inventory.json` shows concentrated carry-through counts such as `121`, `47`, `18`, and `16`

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

### 4. `ShaderDayNightParameters`

Externally backed identity:

- layered day/night or reveal-aware family with helper provenance

Current local candidate packet:

- `tmp/precomp_sblk_inventory.json` shows `name_guess = "ShaderDayNightParameters"`
- `tmp/probe_sample_ep06_ep10_coverage.txt` includes `ClientFullBuild0.package | 01661233:00000000:0737711577697F1C`
- `tmp/probe_sample_next24_coverage_after_nonvisual_fix.txt` includes `ClientFullBuild0.package | 01661233:00000000:00B6ABED04A8F593`

Concrete local search targets:

- `tmp/precomp_sblk_inventory.json`: `name_guess = "ShaderDayNightParameters"` with `occurrences = 5`
- `tmp/probe_sample_ep06_ep10_coverage.txt`: `ClientFullBuild0.package | 01661233:00000000:0737711577697F1C`
- `tmp/probe_sample_next24_coverage_after_nonvisual_fix.txt`: `ClientFullBuild0.package | 01661233:00000000:00B6ABED04A8F593`

What the next live-proof packet should prove:

- whether one of these sampled objects can be used as a visible day/night or reveal target without coercing helper names into plain slots

Best current next target:

- start with the two current `ClientFullBuild0.package` candidate roots because they are already isolated as textured family hits

Current packet:

- [ShaderDayNightParameters Visible-Pass Proof](live-proof-packets/shader-daynight-visible-pass.md)

### 5. `GenerateSpotLightmap` and `NextFloorLightMapXform`

Externally backed identity:

- generated-light or lightmap-helper family

Current local candidate packet:

- `tmp/precomp_sblk_inventory.json` shows `name_guess = "GenerateSpotLightmap"` and `NextFloorLightMapXform = 14`
- the same inventory also shows a weaker secondary `NextFloorLightMapXform = 3` packet

Concrete local search targets:

- `tmp/precomp_sblk_inventory.json`: `name_guess = "GenerateSpotLightmap"` with `occurrences = 6`
- `tmp/precomp_sblk_inventory.json`: `NextFloorLightMapXform = 14`
- `tmp/precomp_sblk_inventory.json`: secondary `NextFloorLightMapXform = 3`

What the next live-proof packet should prove:

- whether a concrete live asset can confirm generated-light helper provenance without forcing these names into ordinary material-slot or UV-transform semantics

Best current next target:

- start with the stronger `GenerateSpotLightmap` packet tied to `NextFloorLightMapXform = 14`

Current packet:

- [GenerateSpotLightmap And NextFloorLightMapXform](live-proof-packets/generate-spotlightmap-nextfloorlightmapxform.md)

### 6. `RefractionMap`

Externally backed identity:

- projection or refraction family, not ordinary diffuse material

Current local candidate packet:

- [Projection, Reveal, And Lightmap Families](family-sheets/projection-reveal-lightmap.md)
- current local corpus keeps `RefractionMap` as a named branch
- `tmp/probe_all_buildbuy_summary_full.json` now records `"RefractionMap": 33`, which upgrades the family from precomp-only archaeology to survey-level TS4-facing presence
- the current narrower `EP10` identity pass now maps `01661233:00000000:00F643B0FDD2F1F7` back to `ClientFullBuild0.package | sculptFountainSurface3x3_EP10GENlilyPad` through an `instance-swap32` transform from `OBJD` candidate `01661233:00000000:FDD2F1F700F643B0`
- nearest adjacent projective roots are `00F643B0FDD2F1F7` and `0124E3B8AC7BEE62`
- strongest visible comparison roots are `0737711577697F1C` and `00B6ABED04A8F593`

What the next live-proof packet should prove:

- whether a concrete asset can be found where refraction-family identity and helper params are visible without collapsing them into generic surface semantics
- whether the object/material seam can prove shared refraction-family semantics without turning the fixture into asset-specific shader logic

Best current next target:

- use the named lily-pad row as the first durable refraction/projective fixture, then inspect its companion/material chain before widening back out to broader survey hints
- do not burn time on blind grep over `probe_all_buildbuy.txt`; current workspace shows that family totals and root/header lists are split across different artifacts
- keep `0124E3B8AC7BEE62` as the mixed boundary/control case, because `00F643` is no longer just unnamed and still keeps the cleaner explicit `diffuse + texture_5` path
- after that narrower fixture pass, repeat the same extraction method for `SimGlass`

Current packet:

- [RefractionMap Live Proof](live-proof-packets/refractionmap-live-proof.md)

## Immediate order

`Current concrete packets`

1. `SimGlass` versus shell baseline
2. `SimSkin` versus `SimSkinMask`
3. `CASHotSpotAtlas` carry-through
4. `ShaderDayNightParameters`
5. `GenerateSpotLightmap` / `NextFloorLightMapXform`
6. `RefractionMap`

`Next unfinished order`

1. inspect the named `RefractionMap` bridge root `sculptFountainSurface3x3_EP10GENlilyPad -> 01661233:00000000:00F643B0FDD2F1F7` at the object/material companion seam
2. then repeat row-level extraction for `SimGlass` from the broader `EP10` transparent-decor cluster:
   - `fishBowl_EP10GENmarimo`
   - `shelfFloor2x1_EP10TEAdisplayShelf`
   - `shelfFloor2x1_EP10TEAshopDisplayTileable`
   - `lightWall_EP10GENlantern`
   - `mirrorWall1x1_EP10BATHsunrise`
3. keep the original `EP10` window-heavy packet only as a bounded negative control
