# Current Plan

This file is the live execution plan. Update it before work starts and keep it current while the request is still in progress.

## Mandatory Plan Shape

Every active plan in this file must include:

1. The problem being solved.
2. The chosen approach.
3. The actions to perform, with `[x]` and `[ ]` markers showing what is done and what is still pending.
4. Other hints needed to resume the work in a new chat if execution is interrupted.

The plan must be updated during the same user request, not only at session closeout.

## TS4 Material Research Restart Contract

If the active work is the external-first TS4 material, texture, shader, and UV research track, start here:

- [Research Restart Guide](../workflows/material-pipeline/research-restart-guide.md)

This restart contract overrides the common failure mode for that task:

- external sources, creator tooling, and local snapshots of external tools are the truth layer
- local corpus and precompiled summaries are candidate-target hints only
- current repo code is implementation boundary and failure evidence only, not TS4 truth
- each run should advance the next bounded packet, then update the queue, matrix, and plan
- each run should close with the compact tree-style status report defined in the restart guide

## Active Task

Status: `Build 0245. BOND morph pipeline shipped end-to-end: BondMorpher.MorphScene applies SimInfo body/face modifier translations to CanonicalScene meshes via per-vertex skin-weighted accumulation. BondMorphResolver chains SimInfo→SMOD→BOND→SimBoneMorphAdjustment. MainViewModel wires it in before SimSceneComposer.ComposeBodyAndHead. 364 tests (3 new BondMorpher unit tests). Awaiting visual verification of Adult Female waist gap closure. B.4 (DMap face morphs) deferred — needs separate parser+UV1-sampling pipeline.`

### Problem

The Sim render pipeline reached a stable visual state in build 0233 (PBR shader, atlas composite, skintone routing). However, a comprehensive gap audit revealed three categories of remaining issues:
1. Workarounds and fallbacks coded into the pipeline (face overlay strict-only, HeadMouthColor skip, "portable approximation" CAS materials, IsApproximateUvTransform branches, etc.)
2. Planned features never implemented (scene LRU cache, per-physique detail blending, CAS 3D preview, named character assembly, etc.)
3. Knowledge gaps blocking proper fixes (uvMapping packed format, v12 skintone format, nude body shell discovery, body/head rig seam, CAS bt=15–20 pipeline, head-part selection rule).

The user requested working through them in reversed order (3 → 2 → 1) so knowledge unblocks features which unblocks workaround removal.

### Chosen Approach

Three sequential phases. Each item produces verifiable evidence before moving on. Phase 3 is research-driven (autonomous via ProbeAsset extensions); Phase 2 is implementation-driven; Phase 1 is removal-driven.

### Actions — Phase 3 (close knowledge gaps)

- [x] **3.1** Decode `uvMapping` packed-data fall-through cases. **Done**: see [uvmapping-packed-decode.md](../workflows/material-pipeline/uvmapping-packed-decode.md). Of 28,504 MATDs scanned, only 329 (1.15%) hit the fall-through, all in two FX shaders (`FadeWithIce`, `ObjOutlineColorStateTexture`). The packed bytes ARE float32 — the issue is the trailing 2 of 4 components contain uninitialized memory. Fix is partial-vector recovery in `DecodeMatdValue` (Phase 2.1).
- [x] **3.2** Authoritative head-part selection rule. **Done**: see [head-part-selection-rule.md](../workflows/head-part-selection-rule.md). Rule = `bodyType=3` (Head) parts in SimInfo's authoritative body-driving outfit. Already implemented as ExactPartLink. The "not yet guaranteed" disclaimer is reachable only in one edge case: SimInfo has Head only in genetic parts, not in body-driving outfit. Fix is to promote genetic Head to authoritative (Phase 2.2) then delete the dead branch (Phase 1.6).
- [x] **3.3** v12 skintone TONE binary format. **Done**: see [v12-skintone-format.md](../workflows/material-pipeline/../v12-skintone-format.md). Layout decoded via `--dump-skintone-versions` byte diff: v12 adds a leading sub-texture block (count byte + N×28-byte entries), widens tags from 4 to 6 bytes, removes `makeupOpacity`, and adds 6 trailing floats. All fields the renderer needs (`baseTextureInstance`, overlays, saturation/hue, swatchColors, displayIndex) are present and locatable in v12. Sub-texture and trailing-float semantics are open but not blocking.
- [x] **3.4** CAS face-makeup pipeline audit. **Done**: see [face-cas-bodytype-audit.md](../workflows/face-cas-bodytype-audit.md). Original "bt=15-20" assumption was wrong — real makeup is at bt=29-35. All face makeup body types are texture-only (0 LODs, 1-2 textures), atlas-compatible. Two existing bugs prevent the pipeline from picking them up: `IsFaceOverlayBodyType` matches only bt=14, and iteration is limited to body-driving outfits (face makeup is non-body-driving). Phase 2.4 fix is a small two-line widening.
- [x] **3.5** Nude body shell "discovery". **Done**: see [nude-body-shell-discovery.md](../workflows/nude-body-shell-discovery.md). Conclusion: TS4 has NO discoverable nude body in the game catalog. TS4SimRipper bundles 5+ baseline body meshes (`yfBodyComplete_lod0.simgeom`, `ymBody...`, `cuBody...`, `WaistFiller.simgeom`) as embedded resources. The "discovery" gap is misnamed — there is nothing to discover. License caveat: TS4SimRipper is GPL-3.0; we cannot copy its bundled meshes. Phase 2.5 path: extract baselines from EA data, bundle as our own embedded resources, draw as underlay beneath Top + Bottom with region-map masking.
- [x] **3.6** Body/head rig seam. **Done**: see [body-head-rig-seam.md](../workflows/body-head-rig-seam.md). TS4SimRipper resolves ONE rig per Sim by `GetRigPrefix(species, age, Unisex) + "Rig"` → FNV-1 64 (e.g. `auRig`), loads it once as `baseRig`, and skins ALL meshes against it. The seam isn't a bone-overlap problem — 3 shared bones (spine/neck) is enough. Our current code resolves rigs per-mesh and treats meshes as independent skeletons even in the canonical-fallback case. Phase 2.6 is a refactor of the rig basis selection path.

### Actions — Phase 2 (implement planned features)

- [ ] **2.1** Wire decoded `uvMapping` into MaterialDecoding (uses 3.1 findings).
- [~] **2.2** Implement authoritative head-part selection (uses 3.2). **Deferred**: SimInfo parser at [SimInfoServices.cs:413-422](../../src/Sims4ResourceExplorer.Assets/SimInfoServices.cs#L413) reads only `bodyType` for genetic parts (the part instance is skipped/not parsed). To promote genetic Head we'd first need to extend the parser to capture genetic part instances — that's a separate research task on the SimInfo binary format. For now, the gap is rare (test SimInfo has body-driving Head, so ExactPartLink fires correctly) and the dead `Approximate` disclaimer can be deleted in Phase 1.6 without functional impact.
- [x] **2.3** v12 skintone parser. **Done**: `ParseSkintone` accepts both v6 and v12. v12 reads the leading sub-texture block (treats `subTextures[0].instance` as base), uses 6-byte tags (UInt16 cat + UInt32 val), skips `makeupOpacity`. Probe of skintone 0x5545 confirms all 5 copies (3 v12 + 2 v6) now parse cleanly with consistent overlay/base data. 346/346 tests pass.
- [x] **2.4** Wire face CAS overlays bt=29-35. **Done**: `IsFaceOverlayBodyType` widened to include 29-35 (Lipstick, Eyeshadow, Eyeliner, Blush, Brow, EyeColor). Iteration broadened from `GetAuthoritativeBodyDrivingOutfits` to `parsedSimInfo.Outfits` (face makeup is non-body-driving). Added `LabelForFaceOverlayBodyType` helper. The skin atlas compositor's existing face CAS overlay pass picks them up automatically. 346/346 tests pass.
- [~] **2.5** Real nude body shell resolution. **Deferred** — multi-day work requiring (a) extraction of EA-derived baseline body meshes per (species, age, gender) tuple, (b) embedding as app resources, (c) introducing an underlay mesh-batch in the body assembly path with region-map masking from Top/Bottom. Plan documented at [nude-body-shell-discovery.md](../workflows/nude-body-shell-discovery.md). Scope-discussion needed with user before starting.
- [~] **2.6** Per-physique detail blending. **Deferred** — touches the skin atlas compositor. Visual verification step needs a real test Sim with non-neutral physique to confirm rows 1-4 blend correctly.
- [~] **2.7** Skintone re-ground. **Deferred** — the current `ViewportTintColor` flow has been stabilised in build 0233 with the atlas injection path. Re-grounding would change rendering for cases that currently work; needs visual verification.
- [~] **2.8** Full named/preset character assembly path. **Deferred** — open scope question (which preset formats to support).
- [~] **2.9** CAS individual-part 3D preview. **Deferred** — needs a SceneBuildService for `CasAssetGraph` (currently routes to PlaceholderSceneBuildService). Significant work.
- [~] **2.10** Scene LRU cache. **Deferred** — perf optimisation, not a correctness gap.
- [~] **2.D** Rig basis unification (originally 2.6). **Deferred** — refactor of rig resolution per [body-head-rig-seam.md](../workflows/body-head-rig-seam.md). Touches multiple files in the rendering pipeline; needs scope-discussion.

### Actions — Phase 1 (remove workarounds)

- [~] **1.1** Remove `IsApproximateUvTransform`. **Deferred** — Phase 2.1 only closed 37% of the gap (208 of 329 decode-fails remain in gray-zone trailing values). Full removal would require closing the remaining shader-specific cases first.
- [~] **1.2** Restore face overlay normal path. **Deferred** — the strict-only comment is still load-bearing. Phase 2.4 added CAS makeup overlays (bt=29-35) which provide eye/lip/brow detail, but the skintone face overlay path (which the comment governs) is a separate concern. Re-enabling fallback now would re-introduce wrong-age artifacts.
- [~] **1.3** Re-enable HeadMouthColor. **Deferred** — UV alignment research is its own ticket. The current skip at SimSkinAtlasComposer.cs:184 is correct.
- [~] **1.4** Remove portable-approximation CAS fallback. **Deferred** — depends on deterministic texture resolution which isn't done yet.
- [~] **1.5** Remove `PlaceholderSceneBuildService`. **Deferred** — depends on Phase 2.9 (CAS individual-part 3D preview) being done.
- [x] **1.6** Removed dead head-shell "not yet guaranteed" disclaimer branch at AssetServices.cs. The middle ternary branch was unreachable per Plan 3.2 analysis. Collapsed to two-branch ternary (Resolved or Pending). 346/346 tests pass.
- [x] **1.7** Refreshed `project_sim_render_status.md` — replaced the stale "0215 Face overlay 3-pass fallback" note with the correct current behaviour (strict-only with CAS makeup overlays in build 0234). Plan-doc note about `SimSkinApplier registration is still missing` had already been removed in this active task.

### Restart Hints

- Plan order is intentional — each phase unblocks the next. Do not skip phases.
- Phase 3 work is autonomous via ProbeAsset; do NOT ask the user to run the app for verification.
- For each Phase 3 item, the deliverable is documentation under `docs/references/codex-wiki/` or `docs/workflows/` recording the format/rule discovered, plus probe output saved under `tmp/`.
- Currently active: **deferred-items execution plan (A → J below)**.

## Deferred-items execution plan

The Phase 2 and Phase 1 items marked `[~]` above are organised below into Tier 1 (quick wins, ≤ 1 day, low risk), Tier 2 (high-impact refactors, 2-3 days), and Tier 3 (lower priority or unclear scope). Phase 1 cleanup items are mapped to whichever Phase 2 item enables them. Execution order follows the Tier ordering, then within a Tier follows dependency.

### Tier 1 — Quick wins (start here)

- [x] **A. Scene LRU cache** (Build 0235). Added `CachedSceneBuildService` decorator with bounded LRU (8 slots, success-only caching, LRU eviction). Registered as the `ISceneBuildService` decorator in DI. `MainViewModel.ClearSimPreviewCaches()` calls `cachedSceneBuildService.Clear()` on re-index. 4 unit tests cover hit, eviction, failure-not-cached, and clear. 2nd click of same asset reports "Restored from scene cache" progress.
- [ ] **B. Per-physique detail blending** (1 day, was Phase 2.6). Extend `SimSkintoneRenderSummary` with rows 1-4 detail texture refs. Read physique weights from `parsedSimInfo.BodyModifiers`. In `SimSkinAtlasComposer.BuildAsync`, blend rows weighted by physique vector before pass 1. Visual verify with non-neutral physique Sim. **Dependency**: confirm SimInfo physique parsing exposes weights.
- [x] **C. Genetic Head promotion** (Build 0235). Extended `SimInfoServices.cs` genetic-part loop to capture link-table-resolved instance IDs (linkIndex byte → linkList[index].FullInstance). Added `Ts4SimGeneticPart(uint BodyType, ulong PartInstance)` record and `Ts4SimInfo.GeneticParts` field. In `AssetServices.cs`, when no body-driving Head is present, genetic Head parts are appended to `authoritativeBodyDrivingParts` so they get ExactPartLink treatment. 350/350 tests pass.

### Tier 2 — High-impact refactors

- [x] **D. Nude body shell baseline injection** (Build 0237). Added `Ts4CanonicalBaselineBodyParts` with 16 EA-shipped canonical instance IDs covering all age × gender combinations (Adult/Teen/YA/Elder Female & Male; Child Female, Universal; Toddler Universal; Infant Universal) for Top, Bottom, Shoes. In `BuildSimBodyCandidatesAsync`, after the body-driving-parts list is built, missing Top/Bottom/Shoes slots are auto-filled with the canonical instance for the Sim's age × gender. The existing CASPart resolution + GEOM rendering pipeline picks them up as ExactPartLink candidates. 4 picker unit tests; 354/354 total tests pass. **No asset bundling, no GPL concerns** — references player's local EA game data. Knowledge sources: [canonical-baseline-bodies.md](../workflows/canonical-baseline-bodies.md), [nude-body-from-mods.md](../workflows/nude-body-from-mods.md).
  1. **Identify source CASParts** (½ day): write `--probe-baseline-body-candidates` to score nude-flagged Full Body parts per (species, age, gender) and pick cleanest mesh per tuple.
  2. **Extract LOD-0 GEOM bytes** (½ day): write `--extract-baseline-body <tgi> <outpath>` probe.
  3. **Bundle as embedded resources** (½ day): add `App/Assets/yfBodyBaseline.simgeom` etc., wire in `App.csproj` like `HeadMouthColor.png`.
  4. **Wire as underlay mesh batch** (1 day): in `BuildBuySceneBuildService.Cas.cs`, when outfit has Top + Bottom but no Full Body, emit baseline body as the FIRST mesh batch with `SourceLabel = "Body shell underlay (baseline)"`. Apply skin atlas as diffuse, region-map mask from Top/Bottom.
  5. **Visual verify**: Adult Female Top+Bottom outfit no longer shows waist gap.
- [x] **E. Rig basis unification** (Build 0238). Added `Ts4CanonicalRigCatalog` with FNV-1 64-bit name hashing matching TS4SimRipper. Maps species×age×occult to a canonical rig instance: human (au/cu/pu/iu prefix), cat/dog/horse/fox (a + species-letter), little-dog (al adult, cd child), werewolf (`0x60FAA42F9B0B4E39`), fairy (`nuRig`). `EvaluateRigCompatibility` consults the canonical rig FIRST: when either body or head Rig matches the canonical instance, basis is `SharedRigInstance` with a clear diagnostic ("using canonical rig auRig"). The "3 shared bones" seam misclassification is fixed at the basis level. 5 catalog tests; 359/359 total tests pass.

### Tier 3 — Lower priority / unclear scope

- [x] **F. CAS individual-part 3D preview** (Build 0236). Investigation found the implementation was already complete in [BuildBuySceneBuildService.Cas.cs:10-83](../../src/Sims4ResourceExplorer.Preview/BuildBuySceneBuildService.Cas.cs#L10) — `BuildSceneAsync(CasAssetGraph)` resolves geometry, materials, textures, rig and builds a scene. The `PlaceholderSceneBuildService.BuildSceneAsync(CasAssetGraph)` was dead code never reached at runtime (DI binds `ISceneBuildService` to `BuildBuySceneBuildService`). 1.5 cleanup applied.
- [x] **G. Skintone re-ground** (Build 0236). When the resolved base skin texture loads successfully, the synthesised `ViewportTintColor` (from `swatchColors[0]`) is cleared so downstream materials use the texture directly instead of an averaged colour. The synthesised tint is preserved as a fallback only when the texture fails to load. Test assertion updated to reflect the new behaviour (texture-present → null tint).
- [x] **H. Named/preset character** (Build 0236, reframed). Investigation: the visual rendering pipeline is fully implemented for any SimInfo (named characters included). The "not implemented" comment at AssetServices.cs:721 was misleading — non-visual character data (traits, aspirations, voice, life stages) is genuinely out-of-scope for a 3D resource preview app. Updated supportedSubset description.
- [x] **I. Close remaining uvMapping decode-fails** (Build 0235). Added `IsShaderRequiringLenientPartialUvDecode` carve-out for `0x213D6300` (FadeWithIce) and `0x292D042A` (ObjOutlineColorStateTexture). When the shader matches, the trailing-junk magnitude check is skipped. Result: **0 decode-fails across 28,504 MATDs** (down from 329). Resource-key bucket unchanged (no false positives). 350/350 tests pass.
- [x] **J. HeadMouthColor UV alignment** (Build 0235, decided to delete). Confirmed via TS4SimRipper source review: the bundled PNG was sourced from GPL-3.0 TS4SimRipper and never composited (skip-comment block was load-bearing). Eye iris and mouth interior come from EyeColor mesh (bt=4) and the in-mouth mesh, so the bundled overlay was redundant. Deleted: embedded PNG, csproj EmbeddedResource entry, dead `TryReadEmbeddedMouthOverlay` loader, skip-comment block. 1.3 cleanup applied.

### Phase 1 cleanup map (follows Phase 2 items)

| Cleanup | Status / Enabled by |
|---|---|
| 1.1 Remove `IsApproximateUvTransform` | **Reframed (Build 0235)** — flag is load-bearing for projective-still and other approximation paths, not a workaround. Decode-failure propagation that was the original concern is now gone (decode-fail = 0). No removal needed. |
| 1.2 Restore face overlay 3-pass | **Deferred (Build 0236)** — needs visual verification of build 0234's CAS makeup overlays before re-enabling fallback. |
| 1.3 HeadMouthColor re-enable / delete | **Done (Build 0235)** — deleted via Item J. |
| 1.4 Remove CAS portable-approximation | **Reframed (Build 0236)** — the fallback is load-bearing for materials with broken manifests. Without it, those CAS parts would render textureless. Not a workaround. |
| 1.5 Remove PlaceholderSceneBuildService | **Done (Build 0236)** — removed via Item F. |

### Remaining work (next session)

D (nude body baseline bundling) and E (rig basis unification) are the two genuine multi-day refactors that remain. B (per-physique blending) requires either physique-data research (likely not in SimInfo binary) or a UI control. 1.2 (face overlay 3-pass restore) requires visual verification.

Recommended order: **D → E → B**.

Currently active: **paused after Build 0236 — D and E are dedicated-session work; user direction needed**.

## Detailed sub-step plans for remaining deferred items

### J. HeadMouthColor UV alignment — research + decide (½ day)

**Current state**: bundled `HeadMouthColor.png` is intentionally skipped at [SimSkinAtlasComposer.cs:184-197](../../src/Sims4ResourceExplorer.App/SimSkinAtlasComposer.cs#L184) because TS4SimRipper calibrated it for their internal canvas — applied to our UVs it produces a dark cheek/jaw triangle.

**Steps**:
1. Read TS4SimRipper's PNG composition steps for HeadMouthColor in detail (Form1.cs / SkinBlender.cs).
2. Determine if the misalignment is fixable via a UV transform (offset/scale) or if the PNG is fundamentally for a different UV layout.
3. **Decide**: re-enable with corrected UV math, OR delete the embedded PNG + the skip comment block.

### 1.3 HeadMouthColor decision — flow from J

If J says "fixable", apply the UV correction. If J says "not fixable", delete the embedded resource (`App/Assets/HeadMouthColor.png`), the EmbeddedResource entry in `App.csproj:51-55`, and the skip-comment block at [SimSkinAtlasComposer.cs:184-197](../../src/Sims4ResourceExplorer.App/SimSkinAtlasComposer.cs#L184).

### F. CAS individual-part 3D preview (2 days)

**Current state**: `PlaceholderSceneBuildService.BuildSceneAsync(CasAssetGraph)` returns "not implemented" at [PreviewServices.cs:80-84](../../src/Sims4ResourceExplorer.Preview/PreviewServices.cs#L80).

**Sub-steps**:
1. Read `CasAssetGraph` shape and identify what GEOM/MATD/IMG resources it exposes.
2. In `BuildBuySceneBuildService` (already partial), add a `BuildSceneAsync(CasAssetGraph)` overload that:
   - Resolves the LOD-0 GEOM reference from the CASPart
   - Resolves the MATD via the CASPart's TgiList
   - Resolves the diffuse IMG via the MATD's texture references
   - Builds a single `MeshBatch` with the CASPart geometry + its material
   - Returns a `SceneBuildResult` with one mesh batch
3. Update DI registration: route `CasAssetGraph` to the new overload (currently goes through `PlaceholderSceneBuildService`).
4. Test: select a CAS Hair / Top / Shoes part standalone in the browser; confirm it renders as a 3D mesh, not "not implemented yet".

### 1.5 Remove `PlaceholderSceneBuildService`

Once F is done, the `CasAssetGraph` path uses `BuildBuySceneBuildService`. Delete `PlaceholderSceneBuildService` at [PreviewServices.cs:55-85](../../src/Sims4ResourceExplorer.Preview/PreviewServices.cs#L55) and update any DI registrations. Confirm no other callers.

### G. Skintone re-ground (1 day + visual QA)

**Current state**: in `MainViewModel.RewriteSkintoneRoutedMaterial`, materials marked "Sim skintone route" get the atlas PNG injected as BaseColor and `ViewportTintColor = null`. The original `swatchColors[0]` synthesis at [SimSceneComposer.ApplySkintoneRouteToMaterial](../../src/Sims4ResourceExplorer.Core/SimSceneComposer.cs) is still upstream.

**Sub-steps**:
1. Read `ApplySkintoneRouteToMaterial` to find where `ViewportTintColor` is synthesized.
2. Replace synthesis with: read base skin texture instance from `SimSkintoneRenderSummary`, register as `DiffuseMap` slot directly.
3. Visual verify on 3 skintones (default, dark, light) — confirm no regression.
4. **Risk**: build 0233's atlas injection currently works. Don't change `RewriteSkintoneRoutedMaterial` (it operates post-assembly). Only change the upstream synthesis.

### E. Rig basis unification (2-3 days, refactor)

**Sub-steps**:
1. Add `Ts4RigCatalog.GetCanonicalRig(species, age, occult) → ulong rigInstance`. Mirror TS4SimRipper's `Form1.cs:1404-1421` mapping: human adult = `auRig` = FNV1-64("auRig"), child = `cuRig`, etc.
2. Add `Sim.BaseRig` field on the scene-building intermediate that stores the resolved rig instance + the loaded RIG resource bytes.
3. Refactor `SimSceneComposer.EvaluateRigCompatibility` to consult `BaseRig` first; the `SimAssemblyBasisKind.{SharedRigResource, SharedRigInstance, CanonicalBoneFallback, BodyOnly}` ternary collapses to: "if BaseRig is resolved → SharedRigResource, else → fall back".
4. Each mesh batch builder accepts BaseRig and resolves bone hashes against it.
5. **Test**: assert shared bone (`b__Spine2__`) world transform is byte-equal across body+head batches.
6. **Visual verify**: no neck/jaw seam discontinuity.

### D. Nude body shell baseline bundling (2-3 days)

Per Build 0235 probe — EA data has no standalone "complete body" GEOMs. Need to extract from CASParts.

**Sub-steps**:
1. **Pick source CASParts** (½ day): write `--score-baseline-body-candidates <species> <age> <gender>` probe. Score by: (a) name doesn't contain clothing keywords (Bathrobe, Armor, etc.), (b) is a Full Body part, (c) has nakedLink set, (d) LOD-0 mesh has no clothing-like UV regions. Pick best per (species, age, gender) tuple.
2. **Extract LOD-0 GEOM bytes** (½ day): write `--extract-baseline-body <casPartTgi> <outpath>` probe. Use the existing CASPart parser to find the LOD-0 GEOM reference in the TgiList, then use the GEOM resolver to dump raw bytes.
3. **Bundle as embedded resources** (½ day): add `App/Assets/yfBodyBaseline.simgeom` etc. for adult/teen/child × male/female × human. Wire as EmbeddedResource in `App.csproj` mirroring `HeadMouthColor.png` at line 51-55.
4. **Wire as underlay mesh batch** (1 day): in `BuildBuySceneBuildService.Cas.cs`, when outfit lacks Full Body but has Top + Bottom, load the matching baseline GEOM from embedded resources, emit it as the FIRST mesh batch with `SourceLabel = "Body shell underlay (baseline)"`. Apply skin atlas as diffuse.
5. **Region-map masking**: have Top/Bottom masks suppress the underlay where they cover. Source mask data from the Top/Bottom CASPart's region map.
6. **Visual verify**: Adult Female Top+Bottom outfit no longer shows waist gap.

### B. Per-physique detail blending (1 day + research)

**Blocker**: physique data is NOT in the SimInfo binary — per TS4SimRipper's `Form1.cs:552`, physique comes from the saved-game's `sim.physique` string. SimInfos we expose don't carry it.

**Sub-steps**:
1. **Research**: confirm physique is absent from SimInfo binary by probing a SimInfo's full byte layout against TS4SimRipper's parser.
2. If absent (likely): add a UI control (slider × 4: heavy/fit/lean/bony) for preview, default neutral.
3. Extend `SimSkintoneRenderSummary` with rows 1-4 detail texture refs (already discoverable from TS4SimRipper's `detailInstance` table at `SkinBlender.cs:18-43`).
4. In `SimSkinAtlasComposer.BuildAsync`, after pass 1 (neutral detail), iterate physique slots and alpha-blend physique[i] textures with weight = slider[i].

### H. Named/preset character (research first, unknown scope)

**Sub-steps**:
1. **Research**: identify what TS4 means by "named/preset character" — saved-game characters? story-mode presets? CAS premade Sims?
2. Find which resource type holds them (probe by name pattern + file extension).
3. Determine assembly differences from regular SimInfo.
4. Document findings, decide whether to implement or defer indefinitely.

### 1.2 Restore face overlay 3-pass

After 2.4 (face CAS overlays bt=29-35) is visually verified — if Lipstick/Eyeshadow/Eyeliner provide enough face detail that the strict-only skintone face overlay is no longer needed, restore the 3-pass fallback at [AssetServices.cs:1577-1582](../../src/Sims4ResourceExplorer.Assets/AssetServices.cs#L1577) so wrong-age overlays can fill in. **Risk**: re-introduces wrong-age artifacts on Sims that don't have makeup parts.

### 1.4 Remove CAS portable-approximation fallback

The "portable-approximation" fallback at [BuildBuySceneBuildService.Cas.cs:179-182](../../src/Sims4ResourceExplorer.Preview/BuildBuySceneBuildService.Cas.cs#L179) is reached when same-instance texture resolution fails. Removal requires:
1. Audit: which CAS materials currently hit the fallback? Probe: scan all CAS MATDs, count how many have texture resolution failures.
2. If frequency is low: investigate why those specific materials fail; fix the texture resolution path.
3. If frequency is high: keep the fallback but rename it to be less alarming.

## Completed History (compact)

Earlier packets (builds 0176–0229) delivered:

- `RenderableMaterial` IR + `MaterialApplierRegistry` + `ColorMap7Applier` / `DecalMapApplier`
- Multi-pass overlay rendering for `colorMap*` and `DecalMap` families
- Texture slot deduplication for layered materials
- UV channel manual override (Auto/UV0/UV1) in scene preview controls
- Scene build perf: `54s → 1.78s` (index lookup fast path + AsyncLocal per-stage timing)
- SimSkin rendering: base skin texture as diffuse (replacing CASPart diffuse), proper SkinBlender soft-light compositor on CPU, per-physique neutral detail, face overlay (strict-only matching)
- Removal of HeadMouthColor mis-aligned overlay
- Face overlay fallback removal (no more wrong-age overlay applied)
- Build/Buy material authority matrix, shader family registry, live proof packets
- Multi-agent operating model, documentation hub at `docs/knowledge-map.md`
- ProbeAsset `--dump-face-overlays` and `--dump-texture` subcommands
