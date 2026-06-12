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

## Active Task: Skin-pipeline ground-truth correction (2026-06-12)

Status: `Build 0310 baseline. Audit found the 0306-0310 "unified atlas from head-CASP region_map texture" path (Model B) rests on an unverified texture identity AND makes human skintone swaps a visual no-op (Pass 3 gated by saturation>=100; binder nulls ViewportTintColor). User-visible defects at 0310: blurry false-toned lit renders, toddler dark face "mask", child disjointed feet (geometry track, separate). User decisions: TS4SimRipper-parity = automated validation oracle; final target = in-game CAS quality; custom shaders / stronger renderer acceptable; priority = make skintones actually work.`

### Problem

Two competing skin-atlas models coexist and both are unproven:

- **Model A** (`SimSkinAtlasComposer.BuildAsync`): tone.SkinSets[0] texture + hardcoded detail TGIs + Pass 1/2/3. Faithful to TS4SimRipper SkinBlender structurally, but missing physique blending, SkintoneShift application, tan/burn states; and its inputs were never validated (the 0305 "hue-preserving Pass 1" deviation may compensate an input bug).
- **Model B** (`BuildAtlasFromPreRenderedBaseAsync`, current default): head-CASPart "region_map"-slot texture (instance `3E68F8B6F44DA2AA`) as fixed atlas base. Breaks human skintone selection by construction.

Plus: the same instance ships in multiple packages with DIFFERENT content (ClientDeltaBuild8 copy of `3E68F8B6F44DA2AA` decodes to an EMPTY 1024×2048; a ClientFullBuild copy carries the full-body diffuse). Resource resolution honors no Full→Delta override order, so any single-copy conclusion is unsafe.

### Chosen Approach

External-first, oracle-driven: (0) make TS4SimRipper's SkinBlender an executable parity oracle; (1) settle texture identities with all-copies probes; (2) fix the TONE v12 parser to the full TS4SimRipper layout; (3) converge on ONE atlas model with per-tone color + shift working, delete the other; (4) calibrate the viewport against in-game CAS reference screenshots once, document constants.

### Actions

- [x] **A0** Audit complete (this session): Model A/B split mapped, skintone-swap no-op confirmed in code, CASP field-order verified against TS4SimRipper CASP.cs (v44 slider block = 44 bytes, order matches; TgiOffset guard catches long-drift only).
- [x] **A1** New probes shipped in ProbeAsset: `--audit-instance <hex[,..]>` (every package copy of an instance + decode + channel stats, Delta-over-Full ordering) and `--list-skintones` (all TONE copies, version coverage, base-texture instances, CSV).
- [ ] **A2** Run `--audit-instance 3E68F8B6F44DA2AA` → determine game-effective content (Delta override?) and whether Model B's base is a stale pre-patch resource. (Running.)
- [ ] **A3** Run `--list-skintones` → pick light/medium/dark human tones; `--audit-instance` their `BaseTextureInstance` textures → settle "full-anatomy per-tone texture vs flat palette". This decides Model A vs B. (Running.)
- [ ] **A4** Fix TONE v12 parser to TS4SimRipper layout: SkinSetDesc = {textureInstance, overlayInstance, overlayMultiplier, makeupOpacity, makeupOpacity2} ×N (repo currently labels bytes 8–16 "reserved" and drops per-set fields); trailing block = tuningInstance + skinPanel:UInt16 + sliderLow/sliderHigh/sliderIncrement. Surface SkinSets list + slider range. Tests on real v6+v12 fixtures.
- [ ] **A5** Oracle: port SkinBlender.DisplayableSkintone 1:1 (per-channel Pass 1, physique loop, shift via ShiftTexture, tan states, age/gender overlay lookup) as a reference compositor + ProbeAsset `--skin-parity <age> <gender> <toneHex>` writing repo.png / reference.png / diff stats. Existing `--compose-skin-atlas` (System.Drawing replica) is the starting skeleton.
- [ ] **A6** Decision packet: pick the single atlas model from A2+A3+A5 evidence; implement per-tone base + SkintoneShift; delete the losing path; replace magic-string contracts ("Sim skintone route"/"Head shell" in Approximation, `skin_atlas.png` filename → shader path) with typed CanonicalMaterial flags.
- [x] **A7** Game package override order implemented (build 0311). `Ts4PackageOverridePrecedence` in Core (Mods 500 > `\Delta\` 400 > DeltaBuild 300 > Preload 200 > FullBuild 100); rewired `GetSimTemplatePackagePreference`, `GetBodyAssemblyPackagePreference` (rank-major, preferred-package = tiebreak), `ScoreCasCrossPackageCandidate` (was actively PENALIZING Delta — root cause of the stale-texture era), `ResolveCasGraphResourceAsync` + `TryResolveAnyTextureAtInstanceAsync` (local-first no longer shadows patch copies), image-by-instance walker (Delta-first walk), skintone base texture (ordered candidates + decode-walk for truncated patch siblings). Template-variant selection stays renderability-major (v38 SimInfo deep-parse gap) with seed-description richness breaking deep ties BEFORE package order. Live-verified via extended `--probe-synthetic-scene` texture provenance: head now resolves the game-effective ClientDeltaBuild8 LRLE copy; `TryExtractFullBodyDiffuse` consequently finds no ≥200KB texture → constructor auto-falls-back to Model A (SkinBlender chain) with modern v12 tone inputs. 398/398 tests.
- [ ] **A8** Viewport calibration: in-game CAS reference screenshots (known tone), decide gamma policy, fit light rig once, document in a calibration doc. Custom HLSL / renderer change allowed if stock HelixToolkit PBR can't match.
- [ ] **A9** Separate geometry track (after skin): child feet disjoint (suspect bind/rig correction missing for human children — analogous to animal "child bind correction" in commit 98d108a); toddler face-mask artifact should fall out of A6 (tone-mismatched overlay over fixed base) — re-verify after.

### Restart Hints

- Audit memo with full evidence: chat session 2026-06-12; texture dumps in `c:\tmp\s4probe\`; app scene dump at `%LOCALAPPDATA%\Sims4ResourceExplorer\ConstructorDump\manifest.txt` (Adult Female, tone 0xAFC5, route note says 5545 — discrepancy unexplained, check).
- Key code: [SimSkinAtlasComposer.cs](../../src/Sims4ResourceExplorer.App/SimSkinAtlasComposer.cs) (both models), [SimSkintoneMaterialBinder.cs](../../src/Sims4ResourceExplorer.App/Services/SimSkintoneMaterialBinder.cs), [SimConstructorViewModel.cs](../../src/Sims4ResourceExplorer.App/ViewModels/SimConstructorViewModel.cs) `TryExtractFullBodyDiffuse` (200KB threshold), [SceneViewportRenderer.cs](../../src/Sims4ResourceExplorer.App/Services/SceneViewportRenderer.cs) (lighting 0310, `skin_atlas.png` filename dispatch ~line 659), TONE parse in [StructuredMetadataServices.cs](../../src/Sims4ResourceExplorer.Packages/StructuredMetadataServices.cs#L112).
- Reference truth: `docs/references/external/TS4SimRipper/src/SkinBlender.cs` (DisplayableSkintone 46–321), `TONE.cs` (SkinSetDesc, v10/v11 layout incl. sliders), `CASP.cs:595-660` (field order).
- Do NOT trust a single package copy of any instance; use `--audit-instance`.

## Umbrella Task: Sim Character Constructor (paused below the skin track)

P0 shipped through build 0310: constructor window + synthetic SimInfo (14 age×gender tuples) + live viewport (R1–R3) + skintone picker + skin-texture sliders + scene cache + dump diagnostics. R4 (MainWindow cleanup) still pending. P1.1/P1.2 (skintone enumeration/picker) shipped in 0293–0295; P1.3+ (morph sliders), P2 (CAS part picking), P3 (idle animation, green-field), P4 (polish) — pending, resume after the skin track lands. Details of the original phased plan: see git history of this file (pre-2026-06-12 version).

## Completed History (compact)

Earlier packets (builds 0176–0310) delivered: RenderableMaterial IR + appliers, multi-pass overlays, UV override controls, scene-build perf 54s→1.78s, SimSkin atlas chain (SkinBlender subset), face-overlay strict matching, Build/Buy + CAS material authority matrices, multi-agent operating model, animal sim pipeline (builds 0247–0289), Sim Constructor P0 + R1–R3 refactor, skintone picker + in-memory scene cache, atlas saga 0303–0310 (hue-preserving Pass 1, unified atlas, lighting rescale).
