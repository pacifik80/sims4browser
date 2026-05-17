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

Status: `Build 0289+. Sim Character Constructor — new dedicated authoring Window. v1 scope locked: humans only, all ages × genders, synthesised SimInfo from age/gender defaults, idle animation IS v1 must-have (minimal — single clip per age/gender, no blend tree). Replaces the deferred-items execution plan A-J (all shipped except B per-physique blending and 1.2 face-overlay 3-pass — both still deferred and unrelated to this track). Animal pipeline progressed independently through builds 0247-0289 (per-species pet rigs, age-aware scene cache, child bind correction, pelt tint).`

### Problem

Today the app is read-only browse over an indexed package set. Users can inspect a Sim by clicking on its SimInfo in the resource tree, but they cannot author one: there's no way to pick "Adult Female" and see what a default Sim looks like, slide morphs, swap a hair, or try a different outfit. The Sim assembly + render pipeline (BuildSimGraph → SimSceneComposer → BondMorpher/DeformerMapMorpher/BlendGeometryMorpher → CAS material routing) is mature enough to drive an interactive constructor — what's missing is a Window, a synthesised-SimInfo path, knob-to-SimInfo wiring, and an animation loop.

### Scope locked (user answers, 2026-05-17)

- **View placement**: separate top-level WinUI Window. Must NOT trigger app shutdown when closed (the App.xaml.cs `OnMainWindowClosed` shutdown handler is currently scoped to MainWindow — confirm it stays scoped).
- **Coverage**: humans only, **all ages × genders** (Infant, Toddler, Child, Teen, YA, Adult, Elder × Female, Male — 14 tuples). No animals in v1.
- **Starting state**: synthesise a default Ts4SimInfo from age/gender + canonical baseline body/head + a default skintone. No fork-from-existing-SimInfo path in v1.
- **Idle animation**: v1 must-have, minimal. Single hard-coded idle CLIP per (age, gender), no blend tree, linear keyframe interp.

### Chosen Approach

Five phases. P0 ships the shell + synthesis on a single tuple, then sweeps the other 13. P1 adds skintone + morph knobs. P2 adds CAS part picking. P3 builds the animation pipeline from scratch (this is the largest sub-project — zero animation code exists today). P4 polish + persistence + docs.

P0 must serialise (other phases depend on the synthesis path and Window shell). P1 and P2 can run in parallel once P0 lands. P3 is independent infrastructure and can be developed alongside P1/P2 in a separate worker if write sets stay disjoint.

### Architectural anchors (from landscape survey)

- App shell: [App.xaml.cs:67-72](../../src/Sims4ResourceExplorer.App/App.xaml.cs#L67) — new Window registers Transient at line 59, retrieved via `App.GetRequiredService<T>()`. Shutdown handler at line 74 is bound only to the main `window` field; that scoping stays unchanged so a constructor-window close is independent.
- Render entry: `MainViewModel.BuildSimGraphAsync` → `ExplicitAssetGraphBuilder` → `BuildBuySceneBuildService.BuildSceneAsync(SimAssetGraph)` → `SimSceneComposer.ComposeBodyAndHead`. A synthesised SimInfo can flow through this chain unchanged as long as `Ts4SimInfo` invariants hold.
- Morph entry: `BondMorpher.ApplyBond`, `DeformerMapMorpher.MorphScene`, `BlendGeometryMorpher.MorphScene`. Inputs are flat lists of `(adjustment, weight)`. Resolvers (`BondMorphResolver`, `DeformerMapResolver`, `BlendGeometryResolver`) memoise by SimInfo `FullInstance` — synthetic SimInfos need a unique-per-state cache key (or a bypass) so slider movements re-resolve.
- Skintone path: `SkintoneInstanceHex` on SimInfo drives `TryResolveSimSkintoneRenderSummaryAsync`. No enumeration query today — need a new `IIndexStore.EnumerateSkintones()`.
- CAS enumeration: `cas_part_facts` SQL table is queryable by (body_type, species, age, gender). Pattern in `tools/ProbeAsset/Program.cs:412-420` (--probe-face-cas-types).
- Animation: ZERO code today. Type IDs `0x6B20C4F3` (CLIP) and `0x6B20C4F2` (CLIB) are unregistered. Vertex skinning currently runs in the viewport shader — animation needs CPU-side bone-matrix evaluation → shader uniform update path.
- Multi-window pattern: app currently opens no secondary windows. Constructor will be the first; pattern needs to be defined cleanly (DI Transient + factory hook on MainWindow menu).

### Actions — Phase P0: Shell + synthesis (serial, single worker)

- [x] **P0.1** `SimConstructorWindow` XAML + code-behind shipped. NavigationView left rail (Genetics / Outfits / Animation), center viewport stub, right knob panel. DI Transient registered in `App.xaml.cs`. Close-window safety: `App.OnMainWindowClosed` handler is scoped to the main `window` field only — constructor close does not trigger app shutdown by construction. Build clean (0 warnings, 0 errors). Files: [SimConstructorWindow.xaml](../../src/Sims4ResourceExplorer.App/SimConstructorWindow.xaml), [SimConstructorWindow.xaml.cs](../../src/Sims4ResourceExplorer.App/SimConstructorWindow.xaml.cs), [App.xaml.cs](../../src/Sims4ResourceExplorer.App/App.xaml.cs#L60).
- [x] **P0.2** "Sim Constructor" button added to MainWindow's top toolbar. Handler `SimConstructor_Click` in [MainWindow.xaml.cs:127](../../src/Sims4ResourceExplorer.App/MainWindow.xaml.cs#L127) resolves the window via DI and activates it.
- [x] **P0.3** [Ts4SimInfoBuilder](../../src/Sims4ResourceExplorer.Assets/Ts4SimInfoBuilder.cs) shipped. Public API: `BuildHuman(ageLabel, genderLabel, skintoneInstance=0) → Ts4SimInfo` (internal type; accessed via reflection in tests) and `SyntheticFullInstance(age, gender) → ulong` (deterministic FNV-1a 64 of `synthetic:human:{age}:{gender}` for resolver cache-key distinctness). Populates body-driving outfit (category 5 = Nude) with canonical Head/Top/Bottom/Shoes from `Ts4CanonicalBaselineBodyParts` per (age × gender). All modifier/sculpt/pelt/genetic lists empty; counts consistent with list sizes. SimInfo version pinned to 33 (modern: pronouns + skintone shift supported). Five new xUnit tests (Adult/Female outfit shape, all 7 ages mapped, skintone preserved, synthetic FullInstance determinism + distinctness, unknown-age yields empty outfit) — 5/5 pass.
- [x] **P0.4a** Public `ISyntheticSimService` + `SimConstructorSeed` record shipped at [SyntheticSimService.cs](../../src/Sims4ResourceExplorer.Assets/SyntheticSimService.cs). Wraps the internal `Ts4SimInfoBuilder` so the App can construct a synthetic Sim without seeing the internal `Ts4SimInfo`. DI Singleton in `App.xaml.cs`. [SimConstructorViewModel](../../src/Sims4ResourceExplorer.App/ViewModels/SimConstructorViewModel.cs) (CommunityToolkit `ObservableObject`) injects the service, exposes age/gender pickers, rebuilds the seed on pick. SimConstructorWindow XAML wires age + gender ComboBoxes under Genetics → Base; centre panel displays `SeedDisplayName`, `OutfitPartCountText`, `SyntheticFullInstanceHex`, `SeedSummary`, `RenderStatusText`. DI Transient registration for the VM. Build clean (0 warnings, 0 errors). 394/394 tests pass.
- [x] **P0.4b** Synthetic-seed → AssetGraph pipeline shipped (commit `bba9763` cleared the conflict, this packet follows). Approach (b) per the plan note: extended `IAssetGraphBuilder` with `BuildSyntheticHumanSimGraphAsync(age, gender, skintoneInstance)`. `BuildSimGraphAsync` (private) now accepts optional `Ts4SimInfo? preParsedSimInfo` — when provided, the resource-lookup + package-read + parse steps are skipped and the same downstream pipeline runs unchanged. Downstream resolvers (body candidates / CAS slots / skintone) get `preferredPackagePath = null` for the synthetic path so they search the index globally instead of preferring a non-existent file. `ISyntheticSimService.BuildHumanAssetGraphAsync(SimConstructorSeed)` delegates to the new builder method. **Verified end-to-end via** new ProbeAsset subcommand `--probe-synthetic-sim <age> <gender>`. Adult Female resolves yfHead/yfTop_Nude/yfBottom_Nude/yfShoes_Nude as ExactPartLink (all 4 layers, SplitBodyLayers mode). Child Male resolves cuHead/cuTop_Nude/cuBottom_Nude/cuShoes_Nude. 394/394 tests pass (after stubbing the new method on 3 fake `IAssetGraphBuilder` impls in `IndexingPipelineTests.cs`).
- [ ] **P0.4c** Wire the AssetGraph into a multi-mesh scene + viewport in `SimConstructorWindow`. Steps: (1) inject `IAssetGraphBuilder` + `ISceneBuildService` + `IIndexStore` into `SimConstructorViewModel`; (2) on age/gender change call `BuildHumanAssetGraphAsync`, walk `SimGraph.BodyCandidates`, resolve each candidate to a real CASPart `ResourceMetadata` via the index, build a `CasAssetGraph` per candidate, call `ISceneBuildService.BuildSceneAsync(CasAssetGraph)`; (3) compose multiple scenes into a single viewport via Helix3D `Viewport3DX` (mirror MainWindow's setup); (4) smoke-sweep all 14 (age × gender) tuples confirming each shows at least the body shell.
- [ ] **P0.5** TS4-style three-pane layout: left tab-rail (Genetics / Outfits / Animation) via WinUI NavigationView; center viewport (reuse the Helix3D control already used by MainWindow); right knob panel that swaps content per tab. P0.5 just stubs the layout — knobs are filled in P1/P2/P3.
- [ ] **P0.6** Smoke-sweep all 14 (age × gender) tuples. Each must render without crash. Bugs caught here usually mean `Ts4CanonicalBaselineBodyParts` is missing an instance for that tuple — fix by extending the catalog or routing the tuple to its nearest neighbour with a documented note.

### Actions — Phase P1: Skintone + morphs (parallelisable after P0)

- [ ] **P1.1** `IIndexStore.EnumerateSkintones()` query — filter by valid TONE versions (v6 + v12 known good), return (instance, displayName, swatchColor, age/gender flags). New ProbeAsset `--list-skintones` verifies the enumeration.
- [ ] **P1.2** Skintone swatch grid in the Genetics tab. On selection → `Ts4SimInfoBuilder.WithSkintone(...)` → rebuild.
- [ ] **P1.3** Body modifier sliders (BOND). Research packet: enumerate the SimModifier (SMOD) resources that target body regions for each (age, gender) and categorise them (height? muscle? weight? specific body parts?). Probe: `--list-body-modifiers <age> <gender>`. Then UI sliders bound to a `Dictionary<linkInstance, float>` fed back through SimInfo.BodyModifiers.
- [ ] **P1.4** Face modifier sliders (DMap + BGEO). Same pattern as P1.3 but for SimInfo.FaceModifiers. Group by face region if metadata permits.
- [ ] **P1.5** Resolver cache invalidation. Morph resolvers memoise per `FullInstance` — synthetic SimInfos either (a) get a fresh GUID per knob change, or (b) the resolvers accept a `bypass` flag for synthetic SimInfos. Pick (b) — cleaner, doesn't break real-SimInfo caching upstream. Add a `bool IsSynthetic` discriminator on the SimInfo summary.

### Actions — Phase P2: CAS parts (parallelisable after P0)

- [ ] **P2.1** `IIndexStore.EnumerateCasParts(species, age, gender, bodyType)` query. ProbeAsset `--list-cas-parts <species> <age> <gender> <bt>` verifies.
- [ ] **P2.2** Outfits tab layout — sub-tabs per slot (Body / Head / Top / Bottom / Shoes / Hair / Accessories / Makeup). Thumbnail grid per slot.
- [ ] **P2.3** Wire selections back into the synthesised SimInfo's body-driving outfit. `Ts4SimInfoBuilder.WithOutfitPart(bodyType, instance)` mutator.
- [ ] **P2.4** Thumbnail rendering — CAS swatches/icons exist on disk; otherwise lazy-render a small preview into a thumb cache (in-memory only, per session — per memory rules, no cross-session disk cache of decoded data).
- [ ] **P2.5** Makeup pickers (bt=29-35) interplay with face-overlay strict-only path. Verify the existing `IsFaceOverlayBodyType` widening (Build 0234) picks them up via the synthesised non-body-driving outfit slot.

### Actions — Phase P3: Idle animation (serial, dedicated worker, after P0)

**This is a green-field sub-project.** No animation code exists. Each step has a verification probe.

- [ ] **P3.1** **Research CLIP/CLIB format.** Start with local `docs/references/codex-wiki/`. If insufficient, consult external (s4pi, TS4SimRipper sources locally at `docs/references/external/`, and last resort: live external). Document in `docs/references/codex-wiki/02-pipelines/animation-clip-format.md`.
- [ ] **P3.2** **Pick idle clips.** ProbeAsset `--scan-clips-by-name <pattern>` to enumerate idle CLIPs per (age, gender). Likely name pattern is `a2o_idle_*` or similar; confirm via research. Pin one canonical idle per tuple to a static catalog `Ts4CanonicalIdleClipCatalog`.
- [ ] **P3.3** `Ts4ClipResource.Parse` in `src/Sims4ResourceExplorer.Packages/`. Tests against the pinned canonical idles — all must parse 100%. Output: per-bone keyframe streams (time, T/R/S).
- [ ] **P3.4** `SkeletalAnimationEvaluator` in `src/Sims4ResourceExplorer.Core/` or `Preview/`. API: `Evaluate(parsedClip, rig, time) → Dictionary<boneHash, Matrix4x4>`. Linear interp between keyframes. Unit tests with synthetic keyframes verify interp correctness.
- [ ] **P3.5** **Per-frame bone matrix → shader.** Hardest infrastructure step. Today bind-pose bone matrices are baked at scene-build time; for animation we need to override them per frame in the viewport. Two options:
  - (a) CPU-skinning: re-skin vertices each frame on CPU. Slow but no shader changes.
  - (b) Bone-matrix buffer: shader reads bone matrices from a constant buffer; per frame we update the buffer with evaluator output. Faster, requires shader path update.
  - Pick (b); the existing shader likely already has a bone matrix uniform path used for bind pose.
- [ ] **P3.6** **Idle timer**: `IdleAnimationController` in the constructor view. Default disabled. After N seconds (default 8s) of no UI interaction, start playing the pinned idle for the current (age, gender). Any UI event resets the timer and pauses. Loop seamlessly when the clip end is reached.
- [ ] **P3.7** **Acceptance**: Adult Female with default skintone idles smoothly with no popping at loop boundaries. Verified visually by user (this is the one P-phase that genuinely needs a build because the test target is "smooth motion in the viewport").

### Actions — Phase P4: Polish

- [ ] **P4.1** Session persistence — save the user's current constructor state (age/gender/skintone/morph weights/part picks) to a small JSON sidecar in `%LocalAppData%/Sims4ResourceExplorer/constructor-session.json`. NOT a cache of decoded data; this is purely user state.
- [ ] **P4.2** Rebuild-scope optimisation. Skintone change ≠ rebuild geometry; morph change ≠ rebuild materials; part swap = full rebuild. Mark each knob with its rebuild scope and route through the scene-build service accordingly.
- [ ] **P4.3** Workflow doc `docs/workflows/sim-character-constructor.md`. Covers entry point, knob layout, scope assumptions, known limits.
- [ ] **P4.4** Update `docs/knowledge-map.md` with the constructor as a documented feature route.

### Multi-agent split

- **P0**: single Worker, serial. Manager (this thread) reviews each commit.
- **P1 + P2**: two Workers in parallel. Write sets are disjoint (P1 touches SimInfoBuilder + skintone + morph wiring; P2 touches CAS enumeration + outfit pickers). Both depend on P0 landing first.
- **P3**: single Worker, can start as soon as P3.1 research is in (parallel to P1+P2 work).
- **Explorers** (read-only) precede each phase if research is needed (especially P3.1 and P1.3 body-modifier discovery).
- **Verifier** runs separately on each packet — runs the test suite + the relevant ProbeAsset commands.

### Restart Hints

- This task replaces the deferred-items execution plan (A-J) — those are all shipped except B (per-physique blending, deferred for separate research track) and 1.2 (face overlay 3-pass restore, deferred until visual verification of CAS makeup overlays).
- Synthesised-SimInfo verification path: extend ProbeAsset with `--synthesise-sim <age> <gender>` that builds a synthetic SimInfo and dumps it the same way `--probe-sim-graph` dumps a parsed one. Use this for ALL P0/P1/P2 verification — do NOT ask user to launch app.
- Idle animation (P3) is the one phase where the user must visually verify. Build a single dedicated user-facing verification build at end of P3.6, bump `<BuildNumber>`, give them the exact `.\run.ps1` command, and confirm the build id appears in the window title.
- v1 = humans only. If pet support is added later, the `Ts4SimInfoBuilder` API needs a species param and the canonical-baseline catalog gains pet entries.
- Morph resolver cache: synthetic SimInfos use `IsSynthetic = true` to bypass memoisation. Real SimInfos retain the existing cache.
- Animation infrastructure (P3) is fully green-field. Budget it accordingly — likely 1-2 weeks alone.
- DO NOT bump `<BuildNumber>` for non-visual verification. Use tests + ProbeAsset.

### Open research questions (must close before phase start)

- **Before P1.3**: where do body-modifier weight ranges come from? Are they free-floating in `[-1, 1]` or `[0, 1]`? Does the game clamp? (Memory note: `BondMorphResolver` has a defensive `[-2, 2]` clamp.)
- **Before P3.1**: is the CLIP format already parsed by any local reference repo (s4pi)? If yes, we can mirror their parser.
- **Before P3.5**: does the existing viewport shader already accept a bone matrix array, or does animation require a shader update?

### Out of scope for v1

- Pets (cats / dogs / horses / little dogs / foxes). Animal pelt + per-species rig pipeline is still maturing per `project_sim_render_status.md`.
- Save-game integration (read existing characters out of save files).
- Persisting the constructed Sim back to a `.package` (write-side; the project is explicitly read-only on packages).
- Blend trees, animation transitions, animated facial expressions, lip-sync.
- Multi-outfit support (TS4 has 5 outfit categories — Everyday, Formal, Athletic, Sleep, Party — plus situational. v1 is one outfit.)

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
