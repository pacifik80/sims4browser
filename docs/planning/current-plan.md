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

Status: `In Progress`

### Current Request Addendum

#### Problem

The repo now has a dense `CAS/Sim` authority companion, but `Build/Buy` authority is still split between the shared guide, edge-family packets, and source notes. That makes the strongest object-side contract harder to reuse and leaves the `Full Build/Buy family authority order` open question broader than it needs to be. The next bounded gap is a dedicated external-first `Build/Buy` material authority matrix that turns the already-proved base path and current family deviations into one restart-safe deep dive.

#### Chosen Approach

- Keep this as a documentation and synthesis packet, not a code packet.
- Use external Build/Buy resource references as the truth layer: `Resource Type Index`, `File Types`, `RCOL`, `MLOD`, `LITE`, and the community `COBJ/OBJD` / material-variant linkage notes.
- Reuse existing live-proof packets only as fixture-backed examples of the already-proved authority chain.
- Add one new companion doc for Build/Buy authority, then sync the shared guide, workflow indexes, source map, open questions, and plan.

#### Actions

- [x] Re-open the current shared/workflow/source docs and confirm that no dedicated Build/Buy authority companion exists yet.
- [x] Create a dedicated external-first `Build/Buy` material authority matrix under `docs/workflows/material-pipeline/`.
- [x] Link the new matrix from the shared guide, workflow index, and source-map docs.
- [x] Narrow the `Full Build/Buy family authority order` open question so future packets can continue from concrete family gaps instead of rediscovering the base path.
- [x] Fold the continuation hints back into the current plan.

#### Restart Hints

- This packet should not try to solve exact per-family shader semantics; it should freeze the Build/Buy authority-discovery and material-linkage contract.
- The safe base order is already strong: `Object Catalog/Object Definition -> Model/Model LOD -> Material Set/Material Definition`, with `Light` as a parallel branch and `VPXY` kept bounded as a linkage helper rather than a base authority node.
- Existing edge-family packets (`RefractionMap`, `ShaderDayNightParameters`, `GenerateSpotLightmap`, `SimGlass`) should be linked as family-specific deviations or live fixtures, not re-explained from scratch.
- The new restart-safe companion is `docs/workflows/material-pipeline/buildbuy-material-authority-matrix.md`.
- The next Build/Buy continuation should start from one of three narrower gaps: `SimGlass` row-level fixture closure, stronger `MTST` state/variant fixtures, or bounded linked-object/`VPXY` family packets.

#### Problem

The main shared docs now state the universal shader/material contract clearly, but the narrower family and live-proof docs can still be misread if they mention domains, fixtures, or authority branches without repeating the architectural split. The next bounded gap is consistency: the lower-layer docs should reinforce “domain-specific authority, shared shader semantics” instead of relying on the reader to remember it from the top-level guide.

#### Chosen Approach

- Keep this as a wording and structure packet, not a new evidence packet.
- Audit the narrow workflow docs that most often talk about family usage, fixtures, and edge cases.
- Add short, repeated “safe reading” reminders where domain-specific wording could be mistaken for shader specialization.
- Fold the clarification into the restart-facing docs and status layers only where it materially improves future continuity.

#### Actions

- [x] Re-open the narrow workflow docs most likely to blur discovery/authority with shader semantics.
- [x] Tighten family, edge-family, and live-proof wording so domain mentions stay clearly on the authority/discovery side.
- [x] Tighten restart/status docs so future packets inherit the same wording automatically.
- [x] Fold the clarification back into the current plan.

#### Restart Hints

- The architecture is already fixed at the top level: one shared shader/material contract after authoritative inputs are found.
- This packet is only about propagating that wording into lower-level docs.
- Target the docs that talk most about families and fixtures, because those are the ones easiest to misread as asset-bound shader branches.
- The lower-layer reinforcement now lives in `edge-family-matrix.md`, `p1-live-proof-queue.md`, `live-proof-packets/simglass-vs-shell-baseline.md`, and `live-proof-packets/refractionmap-live-proof.md`.
- Safe reading to preserve on future packets: fixtures and domains prove authority routes and family survival, then flow back into the same shared material/shader contract.

#### Problem

The docs now have stronger live-proof packets, but a reader can still misread the evidence layer as if the project were building separate `BuildBuy`, `CAS`, or `Sim` shaders. That is the wrong architectural reading. The remaining gap is not shader specialization by asset class; it is clearer wording that domain families only control discovery and authority order before everything converges into one shared shader/material contract.

#### Chosen Approach

- Keep the clarification architectural, not code-driven.
- Tighten the shared guide first, because it is the source-of-truth statement for the universal material/shader contract.
- Then align the shader-family registry and `CAS/Sim` authority companion so they explicitly read as input-authority docs, not domain-specific shader docs.
- Add one short rule for live fixtures: fixtures prove authoritative inputs and family semantics, not asset-bound renderer branches.

#### Actions

- [x] Re-open the shared guide and the two main companion docs that are most likely to be misread as domain-specific shader packets.
- [x] Tighten the shared guide so the universal shader/material contract is explicit and easy to find.
- [x] Tighten the shader registry and `CAS/Sim` authority matrix so domain usage is documented only as discovery/authority context, not as separate shader logic.
- [x] Fold the clarification back into restart-facing docs and the current plan.

#### Restart Hints

- The architectural rule to preserve is: domain-specific discovery, shared post-discovery shader/material semantics.
- `BuildBuy`, `CAS`, and `Sim` stay relevant only for identity roots, authoritative input order, and family-local constraints before canonical-material decoding.
- Live fixtures are still useful, but only as evidence that certain inputs/families exist and enter the shared pipeline; they are not evidence for domain-specific renderer branches.

#### Problem

The current edge-family documentation now has a named `RefractionMap` bridge fixture, but the next restart-safe gap is still open: the docs do not yet record one broader survey-backed row-level extraction route for `SimGlass`, and the refraction packet still needs a tighter statement about what the lily-pad object/material seam does and does not prove. Without that bounded follow-up, the next run would have to rediscover the same search boundary.

#### Chosen Approach

- Keep the work external-first and documentation-first.
- Use the already named `RefractionMap` lily-pad fixture only to tighten the object/material seam wording, not to infer shader semantics from current code.
- Use local survey and probe artifacts only to isolate the next `SimGlass` candidate route.
- Add any new source-backed or survey-backed narrowing to the live-proof packet, queue, matrix, and restart docs so the next run can continue without rescanning.

#### Actions

- [x] Re-open the current edge-family packet state and identify the next bounded documentation gap.
- [x] Reconfirm the named `RefractionMap` object/material seam and tighten its documented safe reading only if the evidence supports a narrower statement.
- [x] Isolate one broader survey-backed next-step route for `SimGlass` that is better than the already-bounded `EP10` obvious-name window/mirror sweep.
- [x] Fold the proved part back into the live-proof packet, queue, matrix, restart guide, source/open-question indexes, and current plan.

#### Restart Hints

- The active continuation packet is still the edge-family research track, not implementation work.
- `RefractionMap` currently has one named bridge fixture: `EP10\\ClientFullBuild0.package | sculptFountainSurface3x3_EP10GENlilyPad -> 01661233:00000000:00F643B0FDD2F1F7`.
- `RefractionMap` safe-reading is now tighter too: the lily-pad seam proves a durable object/material bridge fixture, but similar `instance-swap32` transformed roots in the broader `SimGlass` packet do not yet reopen cleanly, so that transform must not be treated as a universal proof rule.
- The next `SimGlass` step should come from the broader `EP10` transparent-decor cluster rather than from another `EP10` obvious-name sweep:
  - `fishBowl_EP10GENmarimo -> 01661233:00000000:FAE0318F3711431D`
  - `shelfFloor2x1_EP10TEAdisplayShelf -> 01661233:00000000:E779C31F25406B73`
  - `shelfFloor2x1_EP10TEAshopDisplayTileable -> 01661233:00000000:93EE8A0CF97A3861`
  - `lightWall_EP10GENlantern -> 01661233:00000000:F4A27FC1857F08D4`
  - `mirrorWall1x1_EP10BATHsunrise -> 01661233:00000000:3CD0344C1824BDDD`
- Treat those five roots as survey-backed search anchors only. Current direct reopen attempts still return `Build/Buy asset not found`.
- After this packet, the minimum doc sync set is `live-proof-packets/`, `p1-live-proof-queue.md`, `edge-family-matrix.md`, `research-restart-guide.md`, `01-source-map.md`, `03-open-questions.md`, and this file.

#### Problem

The repo now has strong material/render knowledge, but the navigation layer was lagging behind the research layer. Important `CAS/Sim` authority detail had already been split out once, yet there was still no clear task-oriented route between normative guides, workflow deep dives, evidence snapshots, and planning/open-gap docs. That makes narrow follow-up tasks slower because the reader still has to guess where the dense knowledge lives.

#### Chosen Approach

- Keep the shared cross-domain guide as the top-level render/material source of truth.
- Add a repo-level knowledge hub and section-level README files so readers can enter by task, not by folder guesswork.
- Keep dense family-specific authority content in workflow deep dives, but make those deep dives bidirectionally linked from the top-level docs and evidence layers.
- Update supporting indexes so no deep-dive doc becomes orphaned.

#### Actions

- [x] Audit the current documentation entry points and identify missing navigation/index layers.
- [x] Add a repo-level knowledge hub for durable knowledge layers and task-oriented routes.
- [x] Add workflow-level indexes so `docs/workflows/` and `docs/workflows/material-pipeline/` stop being unindexed buckets.
- [x] Add an external-snapshots index so evidence backups have a visible entry point.
- [x] Update top-level docs, Sim docs, workflow docs, and evidence docs with bidirectional cross-links into the new structure.
- [ ] Continue splitting dense material/render deep dives when they outgrow their current file boundaries.

#### Restart Hints

- The new top-level navigation hub is `docs/knowledge-map.md`.
- `docs/README.md` now points to the knowledge map, workflow indexes, the shared guide, and the family-specific authority matrix directly.
- `docs/workflows/README.md` and `docs/workflows/material-pipeline/README.md` are now the section hubs for procedural docs and render/material deep dives.
- `docs/references/external/README.md` now gives external snapshots a visible landing page instead of leaving them as an implicit folder.
- The main shared render/material guide remains `docs/shared-ts4-material-texture-pipeline.md`.
- The dense family-specific companion remains `docs/workflows/material-pipeline/cas-sim-material-authority-matrix.md`.

#### Problem

The new shared pipeline guide is in place, but its main open gaps are still open: there is no full shader-family registry, no complete authoritative `CAS/Sim` material-linkage contract, no complete compositor/layer rule set, and no full per-slot UV-transform coverage. Without that deeper evidence, the guide is useful but still incomplete for systematic implementation work.

#### Chosen Approach

- Continue the same request as a second research/consolidation packet rather than jumping back into code fixes.
- Use the new repo guide as the anchor and now gather missing evidence specifically for the open gaps it lists.
- Combine live web research with community tooling and local snapshots of external tooling (`TS4SimRipper`, external snapshots) instead of relying on only one source type.
- Treat current repo code and local decoder output only as implementation boundary or failure evidence, not as a truth source for TS4 material semantics.
- Distill any newly proven rules back into the shared guide and supporting source-map docs, while keeping unresolved areas explicitly marked as open.
- Close each run with one status matrix that shows current percentage, progress, and per-run increment for every top-level section and important subsection.

#### Actions

- [x] Update the live plan before continuing this follow-up research packet.
- [x] Gather stronger evidence for Build/Buy authority order and separate it from the weaker `VPXY` question.
- [x] Gather stronger evidence for `SharedUVMapSpace`, `CompositionMethod`, and `SortLayer` from code-backed and creator-facing sources.
- [x] Update the shared guide and supporting source-map docs with the newly proven rules and narrower wording.
- [x] Record which gaps still remain unresolved after this deeper pass.
- [x] Build the first `v0` working shader-family registry packet, then reframe it so external sources remain primary and local decoder buckets stay only as implementation boundary.
- [x] Produce the first `v0` authority/fallback matrix for major `Build/Buy`, `CAS`, and `Sim` family groups.
- [x] Document the current decoder slot-name and UV-interpretation heuristics explicitly as implementation boundary, not as family truth.
- [x] Add representative per-family `v0` slot/parameter tables, including still-raw parameters that need later semantics work.
- [x] Split the remaining `raw/unmapped` names into narrower semantic buckets instead of one undifferentiated unknown class.
- [x] Split edge-case families into structurally runtime-dependent cases vs narrow family-local unresolved cases, with a priority queue for the next semantic pass.
- [x] Narrow the `P1` unresolved set into per-target sheets for `RefractionMap/tex1`, `ShaderDayNightParameters`, `NextFloorLightMapXform`, and `CASHotSpotAtlas`, using local code/corpus evidence plus narrow external lightmap corroboration where available.
- [x] Strengthen the `P1` pass with local `precomp_sblk_inventory` concentration evidence and external `CASHotSpotAtlas` hotspot-atlas corroboration from TS4 morph/slider documentation.
- [x] Separate `CASHotSpotAtlas` into an explicit CAS editing/morph branch (`HotSpotControl -> SimModifier -> DMap/BGEO/BOND -> GEOM`) instead of leaving it mixed into the ordinary surface-material chain.
- [x] Split the remaining narrow semantic gaps into explicit `surface-material`, `lightmap/projection`, and `CAS editing/morph` branches, and add engine-lineage corroboration for `samplerRevealMap` / refraction naming where direct TS4 writeups remain weak.
- [x] Add a co-presence pass for `samplerRevealMap`, `LightsAnimLookupMap`, `NextFloorLightMapXform`, and `RefractionMap/tex1`, using local corpus concentration only as supporting evidence for narrower external-first hypotheses.
- [x] Add an explicit document coverage map and narrow `VPXY` into a bounded scenegraph/linkage helper instead of leaving it as a diffuse all-purpose unknown.
- [x] Turn the current `CAS/Sim` evidence into a bounded material-input graph that separates primary identity/material roots from modifier layers and reserve-only fallback paths.
- [x] Turn the bounded `CAS/Sim` graph into a first family-level slot matrix that separates body shell, head shell, footwear, hair/accessory, and overlay behavior.
- [x] Tighten the worn-slot `CAS/Sim` boundary so `Hair`/`Accessory` are exact-part-link-first, `Shoes` stay overlay-first, cross-package geometry companions stay valid, and skintone remains shell-only.
- [x] Tighten the shell-family `CAS/Sim` boundary so body/head shell selection now explicitly preserves default/nude shell gating, body-as-anchor, head-as-mergeable-shell, and shell-only skintone targeting.
- [x] Narrow the shell-family material truth source so parsed `CASP` field-routing is recorded as the current shell-material floor, while explicit companion `MaterialDefinition` stays a stronger upgrade path when present.
- [x] Separate `MTNF` evidence into “format-backed geometry-side material carrier” versus “not yet repo-decoded / barely fixture-backed in shell families,” so the remaining shell gap is about prevalence and implementation, not existence.
- [x] Add the first local sample-corpus prevalence hint for shell-like `MTNF`: the bundled `TS4SimRipper` body/head/waist `.simgeom` samples currently check out as `9/9` containing `MTNF`.
- [x] Record the current shell `ExplicitMatd` asymmetry explicitly: strong on generic CAS / worn-slot scene-build paths and at the composer-level shell merge seam, but still weak on shell-specific end-to-end asset-graph fixtures.
- [x] Add modern TS4 creator-tooling evidence that malformed embedded `MTNF` shader payloads can cause save/game-visible issues, so `MTNF` stays documented as behaviorally relevant payload, not only format archaeology.
- [x] Add TS4-specific creator evidence that `GEOM`-side shader identity is itself behaviorally relevant (`SimGlass`, `SimSkin`, `SimEyes`, `SimAlphaBlended`), so future authority work must preserve shader provenance instead of flattening it into generic surface slots.
- [x] Add a first bounded GEOM-side shader behavior matrix tying `SimGlass` / `SimSkin` / `SimEyes` / `SimAlphaBlended` to visible behavior and likely authority seams, backed by creator evidence plus local external `TS4SimRipper` code.
- [x] Add the first local corpus-weight hint for GEOM-side shader families: `SimSkin` / `SimSkinMask` look core in the current precompiled snapshot, while `SimGlass` is present but narrow.
- [x] Turn that evidence mix into a bounded implementation-priority hint: `SimSkin` as `P1 core`, `SimSkinMask` as `P1 adjacent`, `SimGlass` as `P2 edge but real`, with `SimEyes` / `SimAlphaBlended` preserved but deferred.
- [x] Turn that `P1` hint into the first bounded authority-seam table: `SimSkin` as the safe baseline skin-family seam, `SimSkinMask` as adjacent auxiliary skin-family semantics until stronger live-asset proof appears.
- [x] Refine the `SimSkinMask` packet from vague “core-like” evidence to a tighter statement: repeated parameter-level corpus signal plus repo-code absence of a standalone authority branch, which strengthens the adjacent-family reading.
- [x] Add the first direct sample-asset packet for `SimSkin`: bundled `TS4SimRipper` body/head/waist `.simgeom` resources currently check `9/9` for `SimSkin` shader hash, while no peer asset-level `SimSkinMask` geometry branch has been found.
- [x] Tighten that asymmetry into an explicit negative finding for the current corpus: the current repo render/composition paths plus bundled external tool code still expose named `SimSkin` / `SimGlass` branches but no peer named `SimSkinMask` authority/export branch.
- [x] Add broader tooling corroboration for the same reading: current `TS4 Skininator`, `TS4 Skin Converter`, and `Sims 4 Studio` sources keep skin masks inside skintone/overlay/image-mask workflows rather than surfacing a standalone `SimSkinMask` geometry family.
- [x] Re-check the wider in-repo sample corpus beyond the first folder: no broader local `.simgeom` family surfaced outside the mirrored `TS4SimRipper` resource copies, so the current no-find is not just a one-folder artifact.
- [x] Re-check the broader mainstream toolchain packet: `TS4CASTools` and public `TS4SimRipper` sources still expose `SimSkin` / `SimGlass` but no peer named `SimSkinMask` asset/export/import branch.
- [x] Split the densest `CAS/Sim` authority packet into a dedicated workflow doc with cross-links, so the main shared guide stays cross-domain while family-specific authority notes continue to deepen separately.
- [x] Add stronger body/head/slot corroboration into that dedicated authority doc using external references, creator tooling, and local external snapshots, with current repo code demoted to implementation anchors only.
- [x] Add a dedicated skintone/compositor deep-dive that separates current implementation routing from still-open exact in-game blend math, using `TS4SimRipper` `TONE` / `SkinBlender`, `Skininator`, `Skin Converter`, and creator-facing overlay references as the primary evidence packet.
- [x] Split the shader-family packet into its own deep-dive so live family tables can grow without bloating the shared guide.
- [x] Expand the `v0` shader registry into per-family slot/parameter tables with representative local live assets and corpus-backed edge packets.
- [x] Expand the `v0` authority/fallback matrix to edge-case `CAS/Sim` and `Build/Buy` families.
- [x] Turn `SimSkin` versus `SimSkinMask` into a concrete live-proof packet instead of leaving it only in the queue or matrix.
- [x] Turn `CASHotSpotAtlas` carry-through into a concrete live-proof packet instead of leaving it only in the queue or matrix.
- [x] Turn `ShaderDayNightParameters` into a concrete visible-pass live-proof packet with isolated Build/Buy roots.
- [x] Turn `GenerateSpotLightmap / NextFloorLightMapXform` into a concrete generated-light live-proof packet instead of leaving it only at the family-sheet layer.
- [x] Turn `RefractionMap` into a concrete live-proof packet with family-local `tex1` isolation and an explicit weak-live-root boundary.
- [x] Lift `SimGlass` and `RefractionMap` one tier higher in the live-proof layer by recording their Build/Buy survey-level presence separately from precompiled name archaeology.
- [x] Add the first adjacent-root bridge for the refraction/projective neighborhood and the first explicit shell-control baseline for `SimGlass`, without overstating either one as closed proof.
- [x] Record the current direct owning archaeology rows for `SimGlass` (`0xB6F2B1B1`) and `RefractionMap` (`0xBB85A5B7`) separately from live-fixture evidence, so later passes can re-anchor the same packets without re-scanning the whole corpus.
- [x] Split the current refraction bridge into a cleaner first target (`00F643B0FDD2F1F7`) versus a mixed boundary case (`0124E3B8AC7BEE62` with one `FresnelOffset` LOD), and record that asymmetry explicitly.
- [x] Record that the cleaner refraction bridge root (`00F643B0FDD2F1F7`) currently keeps explicit local `diffuse + texture_5`, while the weaker bridge root (`0124E3B8AC7BEE62`) leans on fallback diffuse resolution.
- [x] Record that `00F643B0FDD2F1F7` now repeats across multiple sampled coverage artifacts, so it is the current best unnamed extraction target even though object-name linkage is still missing.

#### Restart Hints

- This is a continuation of the same documentation/research request, not a new implementation packet.
- The anchor doc remains `docs/shared-ts4-material-texture-pipeline.md`.
- The specific remaining target gaps are now: per-family slot/parameter shader tables, full authority/fallback coverage, exact skintone/compositor math, and full slot-specific UV coverage.
- The narrowest open semantic targets are now explicitly documented: `RefractionMap/tex1`, `ShaderDayNightParameters` reveal/light helpers, `NextFloorLightMapXform`, and `CASHotSpotAtlas`.
- `CASHotSpotAtlas` itself is now better grounded; the remaining question there is its carry-through into render/profile metadata, not its base identity.
- The `CASHotSpotAtlas` side now has a separate editing/morph pipeline branch and should not be collapsed back into ordinary surface-slot semantics.
- `samplerRevealMap` now has additional engine-lineage support as a reveal/mask helper, but not enough TS4-specific proof to collapse it into a canonical base surface slot.
- The remaining narrow names should now be resumed branch-first: `samplerRevealMap` under `surface-material`, `RefractionMap/tex1` and `NextFloorLightMapXform` under `lightmap/projection`, `CASHotSpotAtlas` under `CAS editing/morph`.
- `LightsAnimLookupMap` currently looks narrower than `samplerRevealMap` and should be resumed as a day/night or terrain-light lookup helper, not as a broad shared slot.
- `NextFloorLightMapXform` now looks most native to `GenerateSpotLightmap`; `SimGhostGlassCAS` should be treated as the weaker carry-through case unless stronger evidence appears.
- `VPXY` is still an open gap, but it is now a bounded one: preserve it as object/scene linkage metadata and do not promote it into the base material authority chain without stronger TS4-specific proof.
- The guide now contains an explicit section-level coverage map; future passes should update that map so the document stays self-diagnosing.
- `CAS/Sim` linkage is still partial, but it now has a bounded shared input graph: primary `CASP/GEOM/MTNF` plus material-definition or field-routing candidates, then `RegionMap`/UV/compositor modifiers, then Sim-only `Skintone` routing.
- `CAS/Sim` also now has a bounded family split: body shell, head shell, footwear overlay, hair/accessory slots, and compositor-driven overlay/detail families. The remaining gap is no longer "what are the families?" but "what is the exact authority order inside each family?"
- worn-slot families are now also better bounded: `Hair`/`Accessory` are exact-part-link-first and can legally pick up cross-package geometry companions, `Shoes` stay overlay/body-assembly content, and skintone should remain excluded from these families unless stronger evidence appears.
- shell families are now better bounded too: default/nude shell gating is explicit, body shell is the current assembly anchor, head shell is a mergeable sibling branch, and skintone targeting is currently shell-only. The next remaining shell question is material truth source, not shell identity selection.
- shell-material truth source is now narrower too: parsed `CASP` field-routing is the current repo floor for body/head shell materials, explicit companion `MaterialDefinition` is a stronger upgrade path when present, and the next remaining question is live-family prevalence plus the exact role of embedded `MTNF`.
- `MTNF` is now also better bounded: existence as a GEOM-side material carrier is strong, but current repo GEOM parsing still skips the payload and the current shell fixture corpus barely exercises it. The next `MTNF` question is prevalence plus implementation, not whether it exists.
- the first local prevalence hint is now recorded too: the bundled external `TS4SimRipper` shell-like sample corpus checks `9/9` for `MTNF`, but this is still a local snapshot signal, not yet a repo-wide live-asset survey.
- shell `ExplicitMatd` coverage is now also known to be asymmetric: it is already strong at the composer seam and on generic CAS / worn-slot scene-build fixtures, but shell-specific end-to-end asset-graph coverage still leans `ApproximateCas`.
- `MTNF` is now narrowed one step further too: besides format-backed existence and the `9/9` local sample hint, there is now TS4 creator-tooling evidence that broken MTNF shader payload handling can affect saved/game-visible `GEOM` behavior.
- `GEOM`-side shader identity is now narrowed too: TS4-specific creator evidence ties `SimGlass`, `SimSkin`, `SimEyes`, and `SimAlphaBlended` to real visible family behavior, so future authority work should preserve shader-family provenance instead of flattening it into generic alpha/diffuse guesses.
- there is now also a first bounded GEOM-side shader behavior matrix; the remaining question is no longer whether these shader families matter, but how they rank against decoded `MTNF` params and higher-level `CASP` routing in the shared contract.
- the local precompiled corpus now adds one more prioritization hint: `SimSkin`-adjacent names look central in the current snapshot, while `SimGlass` looks real but narrow. This should influence implementation order, but not be overstated as full in-game proof.
- there is now also a bounded implementation-priority split for these families. The next step is no longer choosing where to start, but turning `P1 core` families into live-asset-backed authority tables.
- the first bounded `P1` authority-seam table now exists too: `SimSkin` is safe to treat as the baseline skin-family seam, while `SimSkinMask` should stay attached to that packet unless live assets prove a stronger standalone branch.
- `SimSkinMask` is now also better bounded in the wording of the guide itself: current evidence shows it as repeated parameter-level corpus signal and adjacent skin-family semantics, while current repo code still lacks a dedicated standalone branch for it.
- there is now also a first direct sample-asset packet for that asymmetry: bundled local body/head/waist `.simgeom` resources check out as `SimSkin`, while `SimSkinMask` still has no peer asset-level geometry branch in current repo evidence.
- the current negative result is now stronger too: exact code/tool sweeps inside the repo snapshot still expose `SimSkin` / `SimGlass` but not a peer named `SimSkinMask` authority/export branch.
- external creator/tooling evidence now also leans the same way more explicitly: current skin-mask workflows stay under skintone, overlay, burn-mask, and image-mask semantics rather than surfacing a standalone `SimSkinMask` geometry family.
- the widened corpus pass still stayed negative: outside the mirrored `TS4SimRipper` resource copies, the current workspace does not yet contain a broader local sample family that would challenge that reading.
- the widened mainstream toolchain pass also stayed negative: checked `TS4CASTools` and public `TS4SimRipper` code still expose `SimSkin` / `SimGlass` only, so a counterexample likely requires genuinely new live assets or rarer toolchains rather than one more pass over the same obvious sources.
- the authority topic is now also split structurally: the main guide keeps the shared cross-domain contract, while the detailed `CAS/Sim` family matrix lives in `docs/workflows/material-pipeline/cas-sim-material-authority-matrix.md`.
- the shader-family topic is now also split structurally: `docs/workflows/material-pipeline/shader-family-registry.md` is the external-first family packet and must keep current decoder/corpus behavior only as implementation boundary, not as truth source.
- the next accumulation layer under that packet is now also present: `docs/workflows/material-pipeline/family-sheets/` holds narrow external-first evidence sheets so future runs can deepen one family at a time.
- that family-sheet layer now has first concrete packets for `SimSkin/SimGlass/SimSkinMask`, `CASHotSpotAtlas`, `ShaderDayNightParameters`, and `GenerateSpotLightmap/NextFloorLightMapXform`; the next synthesis step is a stricter edge-family matrix built from those sheets.
- that synthesis step is now started too: `docs/workflows/material-pipeline/edge-family-matrix.md` is the compact row-by-row authority layer for narrow families, and future runs should deepen it row by row instead of widening generic fallback language.
- `edge-family-matrix.md` now also carries a proof-packet summary and a `P1/P2` live-proof queue; future runs should resume from that queue instead of inventing a new ordering.
- `p1-live-proof-queue.md` now turns that ordering into one concrete working queue with candidate targets; future runs should update that queue instead of scattering target notes back into chat.
- the first concrete live-proof packet is now also started: `docs/workflows/material-pipeline/live-proof-packets/simglass-vs-shell-baseline.md` turns the first `P1` row into an actual inspection document instead of leaving it as only a queue item.
- the live-proof layer is now broader too: `docs/workflows/material-pipeline/live-proof-packets/simskin-vs-simskinmask.md` and `docs/workflows/material-pipeline/live-proof-packets/cas-hotspotatlas-carry-through.md` now turn the next two `P1` rows into concrete packets.
- the live-proof layer now also covers `ShaderDayNightParameters` and `GenerateSpotLightmap / NextFloorLightMapXform`, so the next unfinished concrete packet is now `RefractionMap`.
- the live-proof layer now also covers `RefractionMap`, so the current edge-family queue is structurally complete and the next step is a stronger live-fixture pass rather than another new packet type.
- the stronger live-fixture pass is now narrowed one step further too: `SimGlass` and `RefractionMap` both have Build/Buy survey-level family hits, so the next step is row-level root extraction from that survey layer rather than another generic corpus pass.
- the current bridge packet is now slightly stronger too: `RefractionMap` has adjacent projective roots (`00F643B0FDD2F1F7`, `0124E3B8AC7BEE62`) plus visible comparison roots (`0737711577697F1C`, `00B6ABED04A8F593`), while `SimGlass` now has explicit bundled `SimSkin` shell controls for side-by-side comparison. None of these should be restated as direct family proof.
- the archaeology layer is now also easier to resume precisely: `SimGlass` currently anchors to `0xB6F2B1B1`, `RefractionMap` to `0xBB85A5B7`, and adjacent `samplerRefractionMap` to `0xF087D458` in the current precompiled snapshots. These are packet anchors, not live roots.
- the refraction bridge is now also internally ranked: `00F643B0FDD2F1F7` is the cleaner first extraction target, while `0124E3B8AC7BEE62` is useful mainly as a mixed boundary case because one sampled LOD lands on `FresnelOffset` instead of `WorldToDepthMapSpaceMatrix`.
- that ranking is now one step stronger too: `00F643B0FDD2F1F7` currently preserves explicit local `diffuse + texture_5`, while `0124E3B8AC7BEE62` currently reaches visible texture only through a fallback same-instance diffuse path.
- `00F643B0FDD2F1F7` is now also repeated across multiple sampled coverage artifacts, so the next pass should treat it as the best unnamed extraction target rather than as just one more adjacent bridge root.
- the skintone/compositor topic is now split structurally too: `docs/workflows/material-pipeline/skintone-and-overlay-compositor.md` keeps the dense boundary between routing, overlay/detail families, tan/burn state, and still-open exact blend math.
- the first live-backed shader packet is now no longer purely hypothetical: sampled Build/Buy coverage files now anchor `SeasonalFoliage`, `colorMap7`, `WriteDepthMask`, `ShaderDayNightParameters`, `WorldToDepthMapSpaceMatrix`, `SpecularEnvMap`, `DecalMap`, `painting`, `SimWingsUV`, and `samplerCASPeltEditTexture` to concrete package roots.
- the first edge-family matrix is now no longer one generic open bucket either: `CAS/Sim` docs now keep explicit rows for transparent glass companions, skin-family GEOM seams, hotspot/morph atlas behavior, pelt/edit helpers, and lightmap or reveal carry-through families.
- the guide now also carries more direct section-level source pointers; future passes should keep adding them in-place instead of leaving provenance only in chat.
- Prefer sources that can improve implementation confidence directly: format pages, creator tooling evidence, `TS4SimRipper`, repo decoder code, and local external snapshots.

#### Problem

The current material research queue is structurally complete, but the next bounded gap is still open: `SimGlass` and `RefractionMap` only have survey-level Build/Buy presence plus unnamed bridge roots. Without one row-level root tied back to a concrete object identity, the live-proof layer is still one step short of a durable implementation-facing fixture.

#### Chosen Approach

- Continue from the restart guide's first unfinished packet instead of opening a new family branch.
- Use the existing local Build/Buy identity-survey tooling and probe artifacts to extract one named row-level root, starting with the cleaner `RefractionMap` bridge target `00F643B0FDD2F1F7`.
- Treat current repo probes only as extraction and implementation-boundary evidence; the family reading itself still comes from external and external-snapshot sources.
- Fold only the newly proved linkage back into the live-proof packet, queue, matrix, and supporting indexes.

#### Actions

- [x] Re-read the restart guide, queue, matrix, and current live-proof packets to recover the exact continuation point.
- [x] Run a narrow Build/Buy identity extraction pass for the `EP10` refraction bridge root `00F643B0FDD2F1F7`.
- [x] If the identity pass succeeds, record the named row-level root and the exact package/object linkage in the `RefractionMap` live-proof packet.
- [x] Update the queue, edge-family matrix, and top-level restart hints to reflect the stronger live-fixture footing.
- [x] Close the run with the compact tree-style status report required by the restart guide.

#### Restart Hints

- The next bounded packet is no longer "create another doc"; it is "name one row-level root".
- The first row-level root is now named: `EP10\\ClientFullBuild0.package | sculptFountainSurface3x3_EP10GENlilyPad -> 01661233:00000000:00F643B0FDD2F1F7`.
- The durable linkage anchor is the `OBJD` candidate `01661233:00000000:FDD2F1F700F643B0`, which resolves to the model root via `instance-swap32`.
- The next move is no longer identity extraction for `00F643`; it is object/material inspection of that named lily-pad fixture, then the same extraction method for `SimGlass`.
- Keep `0124E3B8AC7BEE62` only as the mixed boundary/control case, not as the primary proof target.

#### Problem

The refraction bridge now has one named fixture, but the documentation is still missing the next two closure layers: an external-first object/material chain packet that explains why this Build/Buy fixture is a valid inspection root, and a matching row-level fixture for `SimGlass` so the two narrow edge families stop progressing asymmetrically.

#### Chosen Approach

- Keep the named lily-pad root as the current refraction anchor and strengthen it with external resource-chain evidence rather than repo-code reasoning.
- Use local probes only to read package/object/resource linkage for that specific fixture and to extract the next `SimGlass` candidate root.
- Add only bounded documentation: one stronger note for the Build/Buy object/material seam and one stronger note for the `SimGlass` live-fixture hunt.

#### Actions

- [x] Update the live plan before continuing the next documentation packet.
- [x] Gather external evidence for the Build/Buy object/material chain relevant to named fixture inspection (`OBJD/COBJ -> model -> material set/definition`).
- [x] Inspect the named lily-pad fixture only far enough to document its object/material companion seam without inventing unsupported material semantics.
- [x] Extract one row-level `SimGlass` candidate root or, if the search stays negative, document the negative result and the narrowest next search boundary.
- [x] Fold the proved results back into the live-proof packet, queue/matrix docs, and open-questions layer.

#### Restart Hints

- The current refraction fixture work should stay object-chain-first, not slot-semantics-first.
- The exact thing to prove next is not "`tex1` means X"; it is "this named object is a valid durable inspection root inside the known Build/Buy authority chain".
- `SimGlass` should resume through row-level extraction only after the lily-pad object/material seam is documented clearly enough to reuse as a pattern.
- The first obvious-name `EP10` glass/window/mirror packet is now a bounded negative control, not the next recommended search frontier.

#### Problem

The repo still does not have one complete, authoritative guide for the shared Sims 4 material / texture / shader / UV pipeline. Current knowledge is split across code, local reference notes, and partial implementation docs, and that prevents a systematic fix for the current character-texture mapping problems.

#### Chosen Approach

- Pause narrow implementation work and treat this request as a documentation and research packet.
- Audit the current repo docs and local references first, then collect missing external knowledge from live community/reference sources.
- Consolidate the results into one repo guide focused on a unified cross-domain pipeline: resource discovery, shader/material decoding, texture roles, UV routing, atlas/compositor behavior, and validation rules shared across `BuildBuy`, `CAS`, and `Sim`.
- Record open gaps explicitly if the available sources still do not prove a complete rule.

#### Actions

- [x] Update the live plan before continuing this request.
- [x] Audit the current repo documentation and local references for material / texture / shader / UV coverage.
- [x] Gather missing external sources from live Sims 4 modding / reverse-engineering references.
- [x] Write a consolidated guide in the repo and update the documentation map to point to it.
- [x] Record unresolved gaps and risks if the available sources still do not close the full contract.

#### Restart Hints

- This request intentionally pauses the current narrow `Sim` UV bug packet in favor of a broader documentation/research pass.
- The target outcome is one consolidated repo guide for the shared Sims 4 material / texture / shader / UV pipeline, not three separate domain-specific notes.
- The guide should help future implementation converge `BuildBuy`, `CAS`, and `Sim` onto one decoder and viewport/export contract.
- The new repo-level source of truth is `docs/shared-ts4-material-texture-pipeline.md`; supporting detail remains in `docs/references/codex-wiki/`.
- External sources newly folded into the repo guide/source map include Modders Reference resource/file indexes, Mod The Sims packed-file/GEOM/MLOD/VRTF pages, and Sims 4 Studio threads on CAS UV space and `ColorShiftMask`.

#### Problem

The latest manual retest after `build-0178` sharpened the seam further: the body is no longer just a tint-application problem. The user's `Material UV` screenshot shows torso and arm UV islands landing on non-skin panels instead of the actual character diffuse area, which points to a shared texture-group / UV-routing selection bug.

#### Chosen Approach

- Treat the user's `Material UV` screenshot as authoritative evidence that the next bug is in texture-group selection or UV-channel routing, not in Sim assembly or skintone note propagation.
- Re-check the newest session log to confirm the active build and diagnostics before changing code.
- Inspect the shared `MainWindow` viewport selection path (`SelectViewportTextureGroup`, `SelectPrimaryViewportTexture`, `SelectTextureCoordinates`) together with the manifest/material construction path that feeds it.
- Keep the fix shared for `BuildBuy`, `CAS`, and `Sim`: no new Sim-only renderer branch.

#### Actions

- [x] Update the live plan before continuing this request.
- [ ] Inspect the newest `build-0178` session log and confirm the exact diagnostics for the problematic YA Male archetype.
- [ ] Trace the shared texture-group and UV-channel selection path that feeds `Material UV` and `LitTexture`.
- [ ] Land the next narrow shared fix and produce the next manual-test build.

#### Restart Hints

- The latest user observation is stronger than the previous color symptom: the torso/arm UV shells do not overlay the actual skin diffuse panel in `Material UV`.
- That means `MainWindow` is likely selecting the wrong viewport texture group or the wrong primary texture within the correct material, even after the earlier `ViewportTintColor` multiplier fix.
- The next inspection seam is shared UI/render logic first, then manifest/material construction only if the UI is faithfully rendering a bad manifest payload.

#### Problem

The latest real `build-0177` retest proved that `region-map-aware skintone routing` now materializes in runtime diagnostics, but the rendered `Sim` body still appears flat green. That means the current seam has moved downstream from `SimSceneComposer` into the shared viewport material application path.

#### Chosen Approach

- Treat the `build-0177` session log as proof that routing notes and tint payloads now reach `CanonicalMaterial`.
- Inspect the shared `MainWindow` material builder instead of adding another `Sim`-specific branch.
- Verify whether the textured viewport path is multiplying the entire diffuse layer by `ViewportTintColor` even when there is no swatch-mask composite, which would explain the flat green body.
- Land one renderer-side fix that keeps the shared material/texture pipeline unified across `BuildBuy`, `CAS`, and `Sim`.

#### Actions

- [x] Update the live plan before continuing this request.
- [x] Confirm from the real `build-0177` session that `Applied region-map-aware skintone routing outcome...` now appears in diagnostics.
- [x] Narrow the next seam to the shared viewport material application path in `MainWindow.xaml.cs`.
- [x] Inspect whether textured `PhongMaterial` creation wrongly multiplies full diffuse textures by `ViewportTintColor` when no swatch-mask composite is active.
- [x] Land the renderer-side fix and produce the next manual-test build.

#### Restart Hints

- `session_20260419_161210_28736` is the authoritative `build-0177` run for the current seam.
- That session now includes `Applied region-map-aware skintone routing outcome to 5 merged material target(s).`
- It also still reports `Resolved 1 manifest-driven CAS material(s) before considering same-instance fallback textures.`
- Because the viewport still shows the body in flat green, the next likely seam is that `MainWindow.CreateMaterial(...)` uses `ViewportTintColor` as the lit diffuse multiplier even when no swatch-mask composite was produced.
- That renderer seam is now patched in `src/Sims4ResourceExplorer.App/MainWindow.xaml.cs`: textured `LitTexture` materials no longer multiply the whole diffuse map by `ViewportTintColor`; tint metadata remains available for base-color fallback and dedicated swatch compositing only.
- The next manual verification build is `build-0178`.

#### Problem

The latest user retest uncovered two distinct issues: `.\run.ps1 -NoBuild` relaunched the old standard output build instead of the newer alternate-folder build, and `build-0177` still looks visually unchanged even though the new region-map-aware skintone routing now materializes in diagnostics.

#### Chosen Approach

- Separate launch-contract issues from render-pipeline issues instead of treating them as one problem.
- Accept the `build-0177` session log as proof that the skintone-routing packet is now logically active.
- Move the next debugging seam downstream into the preview renderer/material application path, because the composed `CanonicalMaterial` objects already carry the new routing notes but the viewport still renders the body as flat green.
- Keep launch instructions explicit: `-NoBuild` only reopens the already-built standard output folder and is not valid for alternate-folder builds produced to avoid locked binaries.

#### Actions

- [x] Update the live plan before continuing this request.
- [x] Confirm from fresh session metadata that `.\run.ps1 -NoBuild` reopened the old standard-output build (`0176`) rather than the alternate-folder `0177` build.
- [x] Confirm from a real `build-0177` session that region-map-aware skintone routing now materializes in diagnostics.
- [ ] Inspect where `CanonicalMaterial.ViewportTintColor` is consumed in the preview renderer and why it does not visibly tint the rendered Sim body.
- [ ] Land the next narrow renderer-side fix and retest the same YA Male archetype.

#### Restart Hints

- `session_20260419_161142_6448` is the `build-0176` run reopened by `.\run.ps1 -NoBuild`; this is expected because `-NoBuild` launches the standard output folder, not the alternate `tmp/build-0177-app/` folder.
- `session_20260419_161210_28736` is the real `build-0177` run.
- That `build-0177` session proves the previous packet worked logically:
  - `Applied region-map-aware skintone routing outcome to 5 merged material target(s).`
  - each `ApproximateCasMaterial` now includes `Sim skintone route 000000000000AFC5 | region_map Base | source ...`
- Because the viewport still shows the body in flat green despite those diagnostics, the next seam is in preview material rendering rather than in `SimSceneComposer` routing.

### Current Request Addendum

#### Problem

Manual verification packets have become ambiguous because the repo has two launch modes through `run.ps1`, but the instructions given to the user did not always say exactly which one to use. That caused at least one retest to run an older build than the packet being discussed.

#### Chosen Approach

- Treat launch instructions as part of the verification contract, not as an informal aside.
- Standardize on `run.ps1` as the authoritative user launch path for repo-root manual testing unless a packet explicitly requires a nonstandard output folder.
- Make every future runnable packet state one exact command and whether it rebuilds or reuses the existing binaries.
- Freeze the rule in the minimal protocol docs so the same ambiguity does not return in the next chat.

#### Actions

- [x] Update the live plan before changing the protocol docs.
- [x] Inspect `run.ps1` and confirm the actual semantics of default launch vs `-NoBuild`.
- [x] Update the repo protocols so future runnable packets always specify one exact launch command and whether it rebuilds.
- [ ] Use the new protocol immediately for the next manual `build-0176` retest instruction.

#### Restart Hints

- `run.ps1` without `-NoBuild` is the "fresh build and launch" path: it stops running app instances, removes previous output, rebuilds, and launches the new executable.
- `run.ps1 -NoBuild` is only the "relaunch already-built binaries" path: it does not rebuild and should only be used when the user is intentionally rerunning the exact same build.
- The previous retest ambiguity happened because the user effectively reran `build-0175`, while the packet under discussion was `build-0176`.
- Future runnable instructions should always be phrased as one explicit command, with a short note like `rebuilds and launches fresh` or `launches the already-built binaries`.

### Current Request Addendum

#### Problem

The latest manual `Sim` preview session shows that head textures are acceptable but body textures still map incorrectly across the mesh. `build-0175` improved texture-source selection, yet the persisted diagnostics still show approximate CAS material routing rather than authoritative material-definition decoding for the body shell.

#### Chosen Approach

- Use the newest persisted asset-session log as the authoritative runtime signal for this packet.
- Keep the shared-pipeline direction intact: `BuildBuy`, `CAS`, and `Sim` must converge on one material/texture/UV-routing algorithm after asset-specific graph resolution.
- Narrow this packet to the next render seam: prefer decoded `MATD` / `MTST` material-definition resources for `CAS` / `Sim` before manifest approximation, while preserving manifest and same-instance texture paths as ordered reserve routes.
- Keep the existing `UV1` preservation fix in place and do not reopen body-assembly or fallback-policy work in this packet.

#### Actions

- [x] Update the live plan before starting the packet.
- [x] Inspect the newest persisted `Sim` preview session and confirm that body layers still route through approximate CAS materials.
- [x] Isolate the next shared-pipeline seam after manifest routing: `CAS` / `Sim` preview still does not prefer decoded material-definition resources when they are present.
- [x] Implement `MaterialDefinition`-first routing for `CAS` / `Sim` preview scenes with focused coverage.
- [x] Run focused tests for material-definition routing plus the recent UV/manifest seams.
- [x] Produce a new manual-test build for this packet without disturbing the user's currently running app instance.
- [x] Ask the user to retest the same problematic `Sim` archetype and inspect whether body materials now report material-definition diagnostics instead of manifest approximation.
- [x] Inspect the newest post-request session and verify whether the user actually launched `build-0176`.
- [x] Re-run the manual check on a real `build-0176` session before making any conclusion about visual improvement or the next seam.
- [ ] Inspect the real `build-0176` diagnostics/logs and determine why `MaterialDefinition` routing still does not win for the problematic body layer.
- [x] Inspect the real `build-0176` diagnostics/logs and determine why `MaterialDefinition` routing still does not win for the problematic body layer.
- [x] Land the next narrow fix only after the missing authoritative material path is identified from code and runtime evidence.
- [x] Prove from the live index that the problematic body geometry instance does not expose same-instance `MaterialDefinition` resources, so the `0176` packet cannot change that case visually.
- [x] Identify the next live seam: skintone routing currently rejects region-map-backed `ApproximateCas` body/head materials because it only accepts `color_shift_mask`.
- [x] Implement the narrow skintone-routing fix and cover it with focused tests.
- [ ] Produce the next runnable verification build and retest the same YA Male archetype on the fresh build.

#### Restart Hints

- The latest manual evidence comes from `%LOCALAPPDATA%\Sims4ResourceExplorer\Telemetry\AssetSessions\session_20260419_153657_32880`.
- That session shows `Sim archetype: Human | Young Adult | Male` with body textures still looking mis-mapped even though the preview log already says `Resolved 1 manifest-driven CAS material(s) before considering same-instance fallback textures.`
- The missing seam is deeper than texture-source choice alone: body layers still surface `ApproximateCasMaterial` diagnostics rather than decoded `MATD` / `MTST` material semantics.
- The current packet lands in `src/Sims4ResourceExplorer.Preview/BuildBuySceneBuildService.Cas.cs` and keeps the shared resolver direction: material-definition first, manifest second, same-instance fallback last.
- Focused coverage lives in `tests/Sims4ResourceExplorer.Tests/ExplorerTests.cs`, especially `CasGraphScene_PrefersMaterialDefinitionRoutingBeforeManifestApproximation`.
- The latest manual-test build for this packet is `build-0176`.
- The next manual-test build for the current seam is `build-0177`.
- Because the user's existing app process locked the default output folder, the runnable build for this packet was produced in `tmp/build-0176-app/` instead of the standard `bin/.../win-x64/` location.
- The newest user session after the retest attempt is `session_20260419_155013_27672`, but its `metadata.json` still says `build-0175`. The old path was re-run, so that session cannot validate the `0176` material-definition change.
- That `build-0175` session still shows only `Resolved 1 manifest-driven CAS material(s) before considering same-instance fallback textures.` and repeated `ApproximateCasMaterial` entries; there are no `material-definition` diagnostics yet.
- The user has now shown a real `build-0176` UI screenshot. Its diagnostics still say `Resolved 1 manifest-driven CAS material(s) before considering same-instance fallback textures.` and still do not show any `material-definition` diagnostics, so the new path is not winning for this archetype at runtime.
- Live index inspection proved why: for body geometry instance `FACB14F02CD72951` in `ClientFullBuild0.package`, the same-instance resource set contains only `Geometry` and `RegionMap`, with no `MaterialDefinition` resources at all.
- The next actual visual seam is therefore in `src/Sims4ResourceExplorer.Core/SimSceneComposer.cs`, not in `MATD/MTST` lookup. The current skintone application path already exists, but `MaterialSupportsRegionMapSkintoneRouting(...)` was too narrow and only accepted `color_shift_mask`.
- That filter now also accepts `region_map` textures and `ApproximateCas` materials, so body/head payload materials that are region-map-backed but only approximated can finally receive the resolved skintone viewport tint.

### Current Request Addendum

#### Problem

Investigate and improve incorrect texture-to-mesh mapping in character preview scenes, because badly mapped `Sim` textures now hide whether body/head assembly is actually correct.

#### Chosen Approach

- Start from the current `build-0173` runtime behavior and inspect the exact `Sim` material-routing and UV/mapping seams that feed the assembled preview meshes.
- Use the latest persisted session log as evidence, then trace the code paths that select CAS textures, bind them to materials, and route UV semantics into the final preview scene.
- Keep the packet narrow: fix mapping/assignment errors in the current preview pipeline first, not broad scene/assembly architecture.
- Preserve the current authoritative character-assembly rules while improving visual correctness of the already selected body/head layers.
- Treat the local `UV1` fix only as a first seam closure. Follow-up packets must move `BuildBuy`, `CAS`, and `Sim` toward one shared material/texture/UV-routing algorithm rather than separate per-domain mapping behavior.
- The next convergence seam is manifest-driven texture routing: `CAS`/`Sim` preview should consume the same canonical material manifest inputs it already indexes, with same-instance geometry fallback only as a reserve path rather than a parallel texture-selection implementation.

#### Actions

- [x] Update the live plan before starting this packet.
- [x] Inspect the current `Sim` texture/material/UV seams and the latest session evidence for mis-mapped character textures.
- [x] Isolate the narrowest render/material seam causing wrong texture placement on `Sim` meshes.
- [x] Implement the fix with focused tests.
- [x] Build and verify a new manual-test app version for character preview inspection.
- [x] Isolate the first cross-domain convergence seam: `CAS`/`Sim` scene build currently bypasses `MaterialManifestEntry` and resolves textures through a separate fallback-only path.
- [x] Implement the first manifest-driven shared-pipeline step with focused tests.
- [x] Build and verify the next manual-test app version after the shared-pipeline step lands.

#### Restart Hints

- The next packet is about visual texture mapping on already-selected character meshes, not about fallback policy or index mutability.
- The latest manual-test build is `build-0175`.
- The most recent session log is `session_20260419_145647_29872`; it confirms `Sim` previews open successfully but still show approximate CAS materials and repeated rig/region-map limitations.
- The isolated first seam is in `src/Sims4ResourceExplorer.Preview/BuildBuySceneBuildService.Cas.cs`: the CAS GEOM parser/emitter currently does not preserve a real second UV set into `CanonicalMesh`.
- The viewport already supports `UV1` selection and UV transforms in `src/Sims4ResourceExplorer.App/MainWindow.xaml.cs`, so the first fix should stay inside the CAS GEOM parse/emission path and focused preview tests.
- The landed fix now preserves a real secondary UV set into `CanonicalMesh` for CAS/Sim preview scenes, with focused coverage in `tests/Sims4ResourceExplorer.Tests/ExplorerTests.cs`.
- The user expectation is now explicit: texture parsing and mapping must converge on one documented, shared pipeline across `BuildBuy`, `CAS`, and `Sim`; knowledge gained in one domain should be reusable in the others instead of copied into domain-specific logic.
- The next concrete seam is that `CasAssetGraph.Materials` already carries `MaterialManifestEntry`, but `BuildBuySceneBuildService.Cas.cs` still resolves textures directly from geometry fallback. The next packet should make manifest-driven texture resolution primary in CAS/Sim preview and leave fallback textures only as reserve coverage.
- That manifest-driven step is now landed: `BuildBuySceneBuildService.Cas.cs` first resolves `CasAssetGraph.Materials` into `CanonicalMaterial`/`CanonicalTexture` and only then falls back to same-instance geometry textures if manifest routing yields nothing.

#### Problem

Inspect the newest persisted app logs after the user's explicit reindex plus manual browsing across `Sim Archetype`, `BuildBuy`, and `CAS`, so the next packet starts from the real post-rebuild runtime behavior instead of anecdotal UI impressions.

#### Chosen Approach

- Read the newest per-session asset logs under `%LOCALAPPDATA%\Sims4ResourceExplorer\Telemetry\AssetSessions\`.
- Correlate them with the newest indexing telemetry folder to confirm the rebuild path completed cleanly.
- Focus on warnings, unresolved preview states, crashes, and any evidence that the new structured metadata packet still depended on lazy runtime enrichment.
- Do not open a new implementation packet unless the logs show a concrete regression or unfinished contract seam.

#### Actions

- [x] Update the live plan before reading logs for this request.
- [x] Inspect the newest asset-session folders produced by the user's manual run.
- [x] Inspect the newest indexing telemetry folder produced by the explicit rebuild.
- [x] Summarize what the logs confirm about `Sim Archetype`, `BuildBuy`, and `CAS` behavior after rebuild.
- [x] Note any remaining risks or missing observability seams if the logs are still too shallow.

#### Restart Hints

- The user has already rebuilt the index in the app and manually browsed `Sim Archetype`, `BuildBuy`, and `CAS`.
- The immediate question is not whether the UI "seems fine", but whether persisted logs show hidden failures, unresolved states, or read-path writes after the explicit rebuild.
- Relevant log roots remain `%LOCALAPPDATA%\Sims4ResourceExplorer\Telemetry\AssetSessions\` and `%LOCALAPPDATA%\Sims4ResourceExplorer\Telemetry\Indexing\`.
- The current app build used for this packet should be at least `0173`, because that is the first build where structured `CASPreset` / `RegionMap` / `Skintone` descriptions are indexed explicitly instead of waiting for raw-resource lazy enrichment.
- The newest manual session is `session_20260419_145647_29872` on `build-0173`; it contains `6` `Sim` previews, `1` `BuildBuy`, and `1` `Cas`, with no `Exception`, `Unhandled`, or `Crash` markers.
- The newest rebuild telemetry is `indexing_20260419_145701`; it completed in about `5m43s`, processed `4,965` packages and `4,789,589` resources, and still reports the same `2` known `EP18` package failures (`Too many bytes in what should have been a 7-bit encoded integer.`).
- The current log evidence confirms post-rebuild asset browsing works on the persisted index, but it does not yet prove the new `CASPreset` / `RegionMap` / `Skintone` descriptions in the raw browser because the user session did not open those raw resource types directly.
- Performance tails remain visible and should stay on the backlog: some `Sim` / pet / `Cas` previews still spend about `4.9s` to `5.7s` in texture-heavy scene reconstruction, and the sampled `BuildBuy` bench took about `51.7s` to build its scene.

#### Problem

Clarify the real runtime contract of the persisted index after a user-reported resize crash during preview load, so the next bug packet starts from the actual read/write behavior instead of the assumed one.

#### Chosen Approach

- Inspect the concrete UI and indexing service call sites that run during index creation, asset browsing, preview opening, and raw export.
- Separate the stable persisted index/rebuild path from any lazy metadata-enrichment writes that can still occur during normal app use.
- Answer the user from code evidence first, then freeze the new architectural rule in the minimal durable docs set before opening an implementation packet.
- Freeze the target state for character assembly as an index-driven graph walk: runtime assembly should start from the archetype/template root and follow persisted authoritative links/facts instead of rediscovering them heuristically during preview.
- Do not broaden the scope into a crash fix packet until the index contract is stated clearly.
- After removing runtime writes, move runtime-needed metadata into explicit indexing/finalization passes starting with the cheapest safe fields, and keep version invalidation explicit.

#### Actions

- [x] Update the live plan before continuing this request.
- [x] Re-read the current active plan and preserve the previous character-assembly frontier.
- [x] Inspect the concrete read/write index paths used by app indexing and asset opening.
- [x] Inspect the newest session log after the reported resize crash and confirm what it does or does not capture yet.
- [x] Summarize the exact index contract for the user, including the one narrow in-session write path that still exists.
- [x] Confirm the architectural direction: serving index mutability on read paths is not acceptable as a long-lived contract.
- [x] Freeze the new rule in the minimal durable docs set so future packets treat the serving index as immutable post-build.
- [x] Confirm the target expectation for character assembly: the index should persist enough authoritative links, facts, and markers that runtime assembly mostly traverses indexed graph state rather than guessing/searching.
- [x] Extend the durable docs so `Sim` roadmap and big-plan both describe index-driven character assembly and explicit indexing passes instead of runtime lazy persistence.
- [x] Land the first code packet: runtime raw-resource enrichment no longer persists back into the live serving SQLite catalog.
- [x] Remove the `PersistResourceEnrichmentAsync` serving-index write seam from `IIndexStore` so runtime reads no longer have a backdoor for mutating indexed metadata.
- [x] Update focused tests to prove the enrichment service still enriches the current operation without persisting to the index.
- [x] Open the next indexing packet from the new immutable-index contract instead of returning to runtime lazy writes.
- [x] Inspect which deferred metadata fields are still missing from initial indexing and rank them by cost/value.
- [x] Implement the first explicit indexing-side metadata pass for the cheapest high-value fields.
- [ ] Add version invalidation and focused tests for the new indexing-side metadata pass.
- [x] Narrow the first explicit metadata packet to structured descriptions for `CASPreset`, `RegionMap`, and `Skintone`, because those types already have deterministic extractors and currently rely on runtime enrichment for user-visible metadata.
- [x] Persist those structured descriptions during explicit indexing/finalization instead of on lazy raw-resource opens.
- [x] Verify that runtime read paths remain write-free while indexed metadata for those three types becomes available immediately after rebuild.
- [x] Decide that the resize crash should open a separate follow-up packet after the index contract is clarified, because the current session logger still records only startup and not the in-flight preview failure.

#### Restart Hints

- The immediate question is not about body-policy drift; it is about whether the persisted SQLite/shard index is immutable during normal asset work.
- The likely answer from current code is "mostly yes for browse/preview queries, but not fully", because resource metadata enrichment can still persist missing fields on demand.
- The architectural target for the next indexing packet is stricter than the current code: the serving index must become immutable after a successful explicit index build, and any persisted derived data must be produced inside explicit indexing passes with version invalidation rather than on browse/open read paths.
- The rule is now frozen in `docs/architecture.md` and `AGENT.md`; the next implementation packet can remove the current lazy `PersistResourceEnrichmentAsync` write path from runtime read flows under that contract.
- The desired end state for `Sim` assembly is now explicit: index-time extraction should persist the authoritative assembly graph inputs needed for body/head/outfit/rig/skintone/morph selection, so runtime preview starts at the root archetype/template and follows indexed links to the exact resources it must load; unsupported gaps should stay explicit rather than triggering a new heuristic search path.
- That first implementation packet is now landed: `ResourceMetadataEnrichmentService` still enriches the resource in memory for the current raw open/export operation, but the live serving catalog is no longer mutated on read paths.
- The next indexing packet is now sharper: identify which runtime-needed raw-resource metadata fields should move into explicit indexing/finalization passes, starting with the cheapest high-value fields and keeping serving-index version invalidation explicit.
- This packet should prefer fields that do not require heuristic rediscovery at runtime. If one field is expensive or format-specific, leave it deferred for a later explicit pass instead of reintroducing any runtime persistence.
- The current narrow packet should start with `CASPreset`, `RegionMap`, and `Skintone` structured descriptions, because `Ts4StructuredResourceMetadataExtractor` already deterministically supports those formats and they do not require any new runtime fallback path.
- That structured-metadata packet is now landed in source and covered by focused tests: seed-pass indexing now persists deterministic descriptions for `CASPreset`, `RegionMap`, and `Skintone`, while runtime `EnrichResourceAsync` remains unused on the read path.
- One infrastructure tail remains open after this packet: package-level content-version invalidation still does not force rescans when resource-metadata extraction semantics change for unchanged package files, so the next index-contract follow-up should decide whether `packages` needs an explicit content-version column or equivalent rebuild gate.
- The latest crash-session folder is `session_20260419_140804_25936` on `build-0172`; it contains `metadata.json` plus only the startup banner in `asset_openings.log`, so resize-crash observability is still incomplete before final preview apply/log emission.
- Concrete call sites already inspected for this addendum include `MainViewModel.RunIndexAsync`, `RefreshAssetBrowserAsync`, `RefreshRawBrowserAsync`, `SelectResourceAsync`, `ExportSelectedRawAsync`, `SqliteIndexStore.PersistResourceEnrichmentAsync`, and `ResourceMetadataEnrichmentService.EnrichAsync`.

### Problem

Resume character/body assembly work from concrete runtime/index evidence and remove the stale `GP01` legacy archetype rows that still surface as `Character archetype: Species 0x...`, because they hide the real human archetypes and keep the unresolved body-shell tail artificially open.

### Chosen Approach

- Keep the frozen body-shell contract intact and treat the new session logs as the first evidence source, but do not block on them when they contain no asset-opening entries yet.
- Start from build `0171`, which already writes per-launch session logs under `%LOCALAPPDATA%\Sims4ResourceExplorer\Telemetry\AssetSessions\`.
- Use the live shard-backed index as the authoritative current-state source for this packet.
- Suppress only the untrusted legacy `SimInfo` summary rows that currently parse as `SimInfo v20 | species=Species 0x...` and become separate `Sim Archetype` assets, while leaving body assembly policy and default-body selection rules unchanged.
- Rebuild the live shard-backed index after the code change, because the visible app/query behavior is served from persisted canonical asset summaries rather than the current source tree.
- Verify that the visible `Sim Archetype` list and survey now prefer the sane delta-backed `Human` archetypes over the stale full-build `Species 0x...` rows.

### Actions

- [x] Carry forward build `0171` and the new persisted session logging capability into the next character-assembly packet.
- [x] Update this live plan before reading logs or editing code.
- [x] Inspect the newest persisted asset session log from the latest available app runs.
- [x] Confirm that the current session logs still contain only startup banners and no asset-opening entries.
- [x] Correlate the unresolved `GP01` rows with the live shard-backed index and isolate the real seam.
- [x] Confirm that the bad `Species 0x...` archetypes come from `GP01` full-build `SimInfo v20` summaries in `index.shard01.sqlite`, while matching delta rows already persist sane `Human` `SimInfo v38` identity for the same `root_tgi`.
- [x] Implement the narrow summary/index fix so those untrusted legacy opaque-species rows stop surfacing as canonical `Sim Archetype` assets.
- [x] Verify the code path with focused tests and the full test suite.
- [x] Rebuild the live shard-backed cache so the new canonical `Sim` asset summaries replace the stale `Species 0x...` entries in serving SQLite.
- [x] Confirm that the fresh live survey now reports `38` visible archetypes with `ExplicitBodyDriving=6`, `IndexedDefaultBodyRecipe=29`, and `Unresolved=3`.
- [x] Produce a new manual-test app build for the updated live cache and visible archetype list.

### Restart Hints

- This repository already has unrelated uncommitted code changes. Keep the write set narrow and do not revert unrelated edits.
- Preserve the body-shell policy recorded below and in `docs/sim-body-shell-contract.md`: no broad compatibility search, no styling-layer mixing, no new workaround branch.
- The current manual-test build is `0172`, produced from `src/Sims4ResourceExplorer.App`.
- The persisted session logging service lives in `src/Sims4ResourceExplorer.App/Services/AssetSessionLogService.cs` and writes to `%LOCALAPPDATA%\Sims4ResourceExplorer\Telemetry\AssetSessions\session_yyyyMMdd_HHmmss_<pid>\`.
- The latest available session folders still contain only the startup banner in `asset_openings.log`, so this packet currently relies on live index evidence rather than user-opened asset logs.
- The current live-store query path serves from shard databases (`index.sqlite` plus `index.shard01-03.sqlite`) through `SqliteIndexStore.QueryAssetsAsync`.
- The concrete drift for this packet is now closed: the stale `GP01` canonical `Sim` assets built from `SimInfo v20 | species=Species 0x...` summaries were removed from the visible canonical archetype list after the live rebuild.
- The current live survey file is `tmp/sim_archetype_body_shell_audit.json`, and it now reports `38` total archetypes with only `3` unresolved `Human | Infant` rows remaining.
- The rebuild still reported `2` failed packages globally, but the canonical `Sim Archetype` survey completed and the `Species 0x...` rows are gone from the serving shard set.
- If the next chat resumes from here, the next narrow packet is the honest `Human | Infant` unresolved seam, not a return to the old `GP01` opaque-species drift.

## Preserved Product Frontier

`Sim Archetypes honest body-first preview`

Goal: move `Sim Archetypes` from proxy/clothing-like stand-ins toward an honest full-body-first preview path built from real body foundations.

## Progress

- [x] Body-first inspector and template selection are in place
- [x] Authoritative `SimInfo` part links are parsed and surfaced in graph metadata
- [x] Clothing-like compatibility shells are withheld unless a real default/nude body shell exists
- [x] `Top` / `Bottom` no longer act as a fake split-body preview path when no real body shell exists
- [x] Clothing-like authoritative `Full Body` selections no longer masquerade as base-body preview shells; canonical/default foundation can take over instead
- [x] Current body preview now resolves one rendered base-body shell instead of composing multi-layer clothing-like proxy parts
- [x] A dedicated authoritative `Head` shell candidate is now surfaced from `SimInfo` part links and threaded into the same body-first preview path as the torso/body shell
- [x] Sim preview now uses a dedicated `body + head` assembly composer with rig-aware compatibility gating and canonical-bone fallback, so incompatible head scenes are withheld instead of being blindly merged
- [x] The current preview path now materializes an explicit sim assembly basis (`shared rig resource`, `shared rig instance`, `canonical-bone fallback`, or `body-only`) so the inspector reflects the real reason a head shell was or was not assembled
- [x] The current preview path now materializes a first `SimAssemblyGraph` node set around the chosen body/head shells, assembly basis, and final assembly result so the next fidelity layers can hang off stable graph stages instead of free-form diagnostics
- [x] The current `SimAssemblyGraph` now also materializes named assembly inputs (`body shell input`, `head shell input`, `assembly basis input`) so future rig/skintone/morph stages can consume stable inputs instead of composer-local state
- [x] The current `SimAssemblyGraph` now materializes explicit assembly stages (`Resolve body shell scene`, `Resolve head shell scene`, `Resolve assembly basis`, `Compose assembled scene`), and the current preview result is produced through that stage pipeline rather than by direct inline flow
- [x] The current `SimAssemblyGraph` now also materializes an explicit assembly output summary, so the graph already has the shape `inputs -> stages -> output/result` before higher-fidelity rig/skintone/morph work starts
- [x] The current `Compose assembled scene` stage now uses a sim-specific skeletal-anchor assembler instead of delegating final assembly to the generic scene composer
- [x] The current assembly output is now produced by a sim-specific body-anchored skeletal merge, so accepted head meshes are remapped onto the body skeleton basis instead of relying on a generic multi-scene merge
- [x] The current `SimAssemblyGraph` now also materializes explicit assembly contributions, so body/head shell inputs expose anchor/merged mesh, material, bone, and rebased-weight facts before true rig-native graph nodes exist
- [x] The current `SimAssemblyGraph` now also materializes an explicit assembly payload summary, so rig-native anchor/contribution counts exist as a layer distinct from the final rendered scene output
- [x] The current `SimAssemblyGraph` now also materializes explicit payload anchor, bone-map, and mesh-batch records, so the body/head assembly payload has inspectable rig-native structure below the summary layer
- [x] The current `SimAssemblyGraph` now also materializes explicit payload nodes for the anchor skeleton, contribution bone-remap tables, and mesh batches, so later rig-native assembly work already has a node-oriented shape to replace
- [x] The current sim-specific assembler now materializes an internal payload-data layer for the anchor skeleton, remap tables, and merged mesh sets, and all public payload summaries/nodes are derived from that data instead of ad hoc local merge state
- [x] The current `SimAssemblyGraph` now also materializes modifier-aware application passes from authoritative skintone/morph metadata on top of the internal payload-data layer, without pretending that geometry deformation is already applied
- [x] The current application layer now also materializes explicit skintone/morph target planning from payload materials/meshes, so prepared passes are bound to real payload targets instead of staying purely declarative
- [x] The current application layer now also materializes explicit skintone material-routing and morph mesh-transform plans from payload-data, so the next modifier pass can build on internal planning data instead of only summaries/targets
- [x] The current application layer now also materializes explicit skintone routing and morph transform records from those plans, so the next modifier pass can start from internal transform data instead of only plan summaries
- [x] The current application layer now also materializes explicit internal skintone/morph outcomes and threads them into preview diagnostics, so modifier progress is visible in the real preview path instead of only in graph internals
- [x] The current preview scene now also flows through application-adjusted payload data, so skintone routing already changes downstream material state in the assembled scene path instead of staying only in graph bookkeeping
- [x] The current preview path now also resolves authoritative skintone resources and selected body/head `region_map` inputs, then applies region-map-aware viewport tint routing into rendered Sim materials instead of leaving skintone as approximation text only
- [x] Canonical human body-foundation search now paginates beyond the first shell-query page, so buried default/nude body shells can still replace withheld clothing-like `Full Body` candidates in large human archetypes
- [x] Indexed default/naked body-recipe evaluation now also accepts generic nude/unisex adult shells such as `acBody_Nude` and `ahBody_nude`, so real human archetypes are not blocked on a strict `yfBody_`-only prefix assumption
- [x] Body-first candidate resolution now prefers authoritative `SimInfo` `Nude` outfit records over a flattened union of all outfit parts, so the shell path starts from one concrete body-driving outfit instead of mixing every archetype outfit into one noisy candidate pool
- [x] Primary body preview no longer falls back to flattened outfit unions or archetype-wide compatibility shell search when a `SimInfo` template has no authoritative body-driving `Nude` outfit record, so unresolved human archetypes fail honestly instead of rendering cross-species junk bodies
- [x] Exact `Hair` / `Accessory` slot resolution now uses the correct human CAS-slot predicate instead of the body-only filter, so authoritative head-related selections are no longer downgraded to compatibility fallback by a bad predicate
- [x] The current preview path now materializes an explicit rig-centered torso/head payload seam with durable accepted-input, anchor, bone-remap, and mesh-range identity data, so later modifier packets can consume one stable seam instead of treating the final stage as just an assembled scene summary
- [x] SQLite index now persists authoritative `SimInfo` template facts and body-driving part links, and Sim preview source/body-candidate resolution now consumes those indexed facts before falling back to broad runtime searches
- [x] The repo now has an explicit `Sim Archetype` body-shell contract: template selection is recipe-first, package preference only chooses between package variants of the same template, and the only allowed fallback after explicit body-driving outfits is indexed default/naked body recipe
- [x] The repo now has a durable live-index `Sim Archetype` body-shell audit plus a headless `ProbeAsset` live-cache rebuild/survey seam, so one clean rebuild against `%LOCALAPPDATA%\Sims4ResourceExplorer\Cache` can repopulate current seed facts and immediately resurvey the live archetype set; the latest rebuilt audit reports `46` rows with `ExplicitBodyDriving=23`, `IndexedDefaultBodyRecipe=0`, and `Unresolved=23`
- [x] SQLite cache initialization now version-invalidates stale seed-derived fact tables (`sim_template_facts`, `sim_template_body_parts`, `cas_part_facts`), so old extractor semantics do not silently survive in live template/body-recipe selection after code changes
- [ ] Add rig/skintone/morph layers to the rendered path

## Immediate Next Step

Use the updated live survey and the seed-fact invalidation fix to open the next narrow follow-up packet without widening fallback rules:

- the first follow-up on the `3` former `ExplicitBodyDriving + AssemblyMode=None` rows is now closed: they stay `AssemblyMode=None`, are reclassified as honest `Unresolved`, and surface an explicit issue that body-driving outfit records existed but no renderable body-shell layer resolved
- one clean live rebuild is now also closed: headless `ProbeAsset --rebuild-live-sim-archetypes 8` repopulated the live cache from the current extractor logic and moved the rebuilt live audit to `ExplicitBodyDriving=6`, `IndexedDefaultBodyRecipe=0`, and `Unresolved=40`
- the false explicit bucket is also closed: the former `6` explicit-but-unrenderable rows were mostly caused by the single-non-`Nude` drift and now report honest unresolved diagnostics instead of fake body-driving status
- the next live-query repair is now also closed: `ProbeAsset --survey-sim-archetypes` against the rebuilt cache now reports `ExplicitBodyDriving=6`, `IndexedDefaultBodyRecipe=27`, and `Unresolved=13`
- the still-explicit human rows remain contract-valid `SplitBodyLayers` results sourced from authoritative `ExactPartLink` `Nude` parts, not evidence of fallback drift
- next, inspect the remaining `13` unresolved live archetypes that still report neither an explicit renderable body-shell result nor an indexed default/naked recipe hit
- after that, decide whether the remaining unresolved set is missing richer authoritative recipes, additional indexed nude-body patterns, or real parser support for younger body families
- keep the current `body + head shell` preview honest and authoritative
- do not reintroduce broad compatibility search or styling-layer mixing as a shortcut
- keep diagnostics honest about whether a template resolved through explicit body-driving, indexed default/naked body recipe, or unresolved body-shell inspection

## Multi-Agent Packets

This active block now runs in the repo's default multi-agent mode.

### Packet 1: Authoritative Assembly Input Map

- Goal: confirm the exact `SimInfo -> Nude outfit -> body/head part -> rig basis` path and record any unresolved format gaps before more implementation lands
- Status: `Completed`
- Owner: explorer
- Allowed write set: none
- Expected write set after research: `src/Sims4ResourceExplorer.Assets/SimInfoServices.cs`, `src/Sims4ResourceExplorer.Assets/AssetServices.cs`, `src/Sims4ResourceExplorer.Core/Domain.cs`, related tests/docs
- Verification: repo docs/references first, targeted synthetic `SimInfo` tests for surfaced authoritative inputs, web only for unresolved authoritative questions
- Red lines: no new compatibility fallback proposals disguised as implementation
- Deliverable: a frozen input contract for `body`, `head`, `rig`, `skintone`, and `morph` inputs plus any unresolved authoritative gaps

Packet 1 findings:

- `body`: authoritative path is `SimInfo -> Nude outfit (Category 5) -> concrete outfit entry -> selected CAS part state -> exact CASPart`. The current repo already narrows to `Nude`, but it still reduces part state too aggressively toward `(bodyType, partInstance)` instead of preserving full outfit-entry identity, full CAS part key/group, and per-part color shift. Only after the exact path is exhausted may the body path widen into canonical foundation or compatibility search.
- `head`: authoritative path is the same `Nude` outfit-entry selection path, with `Head` as a dedicated shell and `Hair` / `Accessory` as exact head-related CAS selections. The graph surfaces head candidates and assembly input summaries, but still lacks one durable authoritative head-shell identity on `SimAssetGraph`, and authoritative head-related selections can still exist without a dedicated head shell.
- `rig`: authoritative rig choice should be driven by the resolved body/head path plus species/age/occult/frame state, then validated against attached rig resources or rig instance ids. The current composer surfaces `Assembly basis` and payload summaries, but `SimAssetGraph` still does not expose one durable authoritative rig identity sourced from the selected template path, and canonical-bone overlap remains a fallback/diagnostic rather than the true rig-selection contract.
- `skintone`: authoritative source is `SimInfo.SkintoneInstance + SkintoneShift`, which should resolve to a real `Skintone` resource and later bind into `RegionMap`-aware material application. The graph and composer already surface skintone metadata, skin-pipeline/body-source summaries, and skintone application planning/outcomes, but not yet one resolved authoritative skintone resource input with region-map binding.
- `morph`: authoritative source is the real `SimInfo` modifier/sculpt state, including direct, genetic, growth, and other modifier channels. The graph/composer surface this as morph groups and transform planning/outcomes, but not yet as per-channel authoritative inputs bound to real `BGEO` / `DMAP` / `BOND` application or actual mesh/bone changes.
- frozen rule at Packet 1 time: the only authoritative starting point for the current block was `SimInfo -> Nude outfit -> concrete selected part state` plus direct scalar/channel metadata from `SimInfo`; later packets may widen only into the explicit indexed default/naked body recipe contract and must not collapse back into a lossy compatibility pool.

Open gaps carried forward:

- `SimAssetGraph` still exposes summaries/candidates where Packet 2+ needs durable authoritative input identities.
- the current repo still drops outfit-entry identity, full CAS part key/group, and per-part color shift too early in the path
- direct `SimInfo -> rig` identity surfacing is still missing
- `RegionMap` binding is still outside the current rendered skintone path
- morph/deformer inputs are still planner-level rather than real mesh/bone application

### Packet 2: Rig-Centered Assembly Seam

- Goal: replace the remaining scene-level final assembly boundary with an explicit rig-centered torso/head assembly seam that later modifier layers can consume
- Status: `Completed`
- Owner: worker
- Allowed write set: `src/Sims4ResourceExplorer.Core/SimSceneComposer.cs`, `src/Sims4ResourceExplorer.Core/Domain.cs`, related tests, and docs touched by behavior changes
- Verification: targeted composer tests for shared-rig, fallback, mismatch, payload counts, bone remaps, and output stage state, then full suite if shared graph/composer behavior moves
- Red lines: no new broad fallback search, no UI-first workaround that hides unresolved assembly structure, no promotion of canonical-bone overlap into the real rig-selection contract, and no new lossy seam that drops outfit-entry identity or authoritative part state
- Deliverable: a real rig-centered torso/head payload seam that later modifier packets consume instead of the current merged-scene endpoint

Packet 2 verification:

- targeted verifier pass: `dotnet test tests/Sims4ResourceExplorer.Tests/Sims4ResourceExplorer.Tests.csproj --no-restore --filter "SimSceneComposer"` -> passed `4/4`
- full verifier pass: `dotnet test Sims4ResourceExplorer.sln --no-restore` -> passed `262/262`
- accepted change: `SimSceneComposer` and `Domain` now describe the final assembly boundary as a torso/head payload seam with explicit accepted seam inputs, source TGIs/package paths, mesh ranges, and bone-remap entries

### Packet 3: Rendered Skintone And Region-Map Material Path

- Goal: turn skintone from diagnostic/material-planning bookkeeping into real rendered material routing, and add region-map-aware material application
- Status: `Completed`
- Owner: worker
- Allowed write set: `src/Sims4ResourceExplorer.Assets/AssetServices.cs`, `src/Sims4ResourceExplorer.Core/SimSceneComposer.cs`, likely a parser/helper beside `src/Sims4ResourceExplorer.Packages/StructuredMetadataServices.cs`, plus related tests/docs
- Verification: assertions on effective material state rather than approximation text, plus one focused probe/resource query if tests cannot prove region-map binding
- Red lines: no support-label drift and no silent proxy-path widening
- Deliverable: rendered material-state changes driven by authoritative skintone and region-map inputs rather than graph-only summaries

Packet 3 verification:

- targeted verifier pass: `dotnet test tests/Sims4ResourceExplorer.Tests/Sims4ResourceExplorer.Tests.csproj --no-restore --filter "SimSceneComposer|StructuredResourceMetadataExtractor|AssetGraphBuilder_BuildsSimMetadataGraph_WithResolvedSkintoneRenderInput|AssetGraphBuilder_PreservesCasRegionMapSummary|AssetGraphBuilder_PreservesCasColorShiftMaskTextureRole"` -> passed `12/12`
- full verifier pass: `dotnet test Sims4ResourceExplorer.sln --no-restore` -> passed `266/266`
- accepted change: `Skintone` and `RegionMap` now have typed parser support, `SimAssetGraph` resolves authoritative skintone render input, selected body/head `CASPart` graphs preserve parsed `region_map` summaries, and `SimSceneComposer` now turns those inputs into region-map-aware rendered material tint state instead of writing skintone only into approximation text

### Packet 4: Rendered Morph And Deformer Mesh Path

- Goal: turn morph from planned operations into real mesh or bone-affecting application, starting from authoritative `SimInfo` channels and then binding into `BGEO` / `DMAP` / `BOND` style resources
- Status: `Pending`
- Owner: worker
- Allowed write set: `src/Sims4ResourceExplorer.Assets/SimInfoServices.cs`, `src/Sims4ResourceExplorer.Assets/AssetServices.cs`, `src/Sims4ResourceExplorer.Core/SimSceneComposer.cs`, likely new deformer helpers, plus related tests/docs
- Verification: synthetic tests that assert mesh/bone output changes rather than pending/prepared counts, plus one real-resource probe if channel-to-resource binding stays uncertain
- Red lines: no fake "applied" state without geometry or bone output changes, and no morph path that bypasses the authoritative packet-1 inputs
- Deliverable: a first real rendered morph/deformer path with honest diagnostics where resource binding is still incomplete

### Packet 5: Independent Verification And Support-State Ratchet

- Goal: confirm that each landed packet actually addressed the intended problem and tighten support statements/graph labels so docs match the real path
- Status: `In Progress`
- Owner: verifier
- Allowed write set: none unless the manager explicitly opens a follow-up fix packet
- Verification: tests, probes, SQLite/resource inspection, UI only when the packet is genuinely visual
- Red lines: no quiet fixups during verification; failures reopen a packet instead
- Deliverable: pass/fail evidence for each packet plus any required support-label/doc tightening

Packet 5 verification:

- reopened drift found: rendered Sim body/head preview existed in code, but `Support Status`, `Support Notes`, `Scene Reconstruction`, and Sim-template auto-selection still reflected the older metadata-only story
- accepted follow-up fix: Sim template redirect now prefers templates with authoritative `Nude` / body-driving outfits over richer-but-non-body-driving variants, and selected-asset Sim details now report rendered assembled preview state honestly instead of hard-coding `metadata-only`
- live verifier probe on `Sim archetype: Human | Young Adult | Female` exposed a second-order source-selection bug: real authoritative `Nude` templates existed in the grouped member set, but could still sit deeper than the previous bounded inspection window. The follow-up fix now inspects the whole grouped `SimInfo` member set when needed so grouped archetype preview source selection is driven by real `Nude/body-driving` evidence instead of a capped metadata window.
- accepted index upgrade: rebuilds now persist authoritative `sim_template_facts` and `sim_template_body_parts` rows, and `AssetGraphBuilder` now consumes those indexed facts for grouped template selection and exact body-part candidate resolution before falling back to runtime search/parsing
- focused verifier pass: `dotnet test tests/Sims4ResourceExplorer.Tests/Sims4ResourceExplorer.Tests.csproj --no-restore --filter "SqliteIndexStore_PersistsSimTemplateFactsAndBodyPartFacts|AssetGraphBuilder_UsesIndexedTemplateFacts_WhenTemplateSearchFallbackIsUnavailable|AssetGraphBuilder_UsesIndexedBodyPartFacts_WhenRawResourceLookupFallbackIsUnavailable"` -> passed `3/3`
- focused verifier pass: `dotnet test tests/Sims4ResourceExplorer.Tests/Sims4ResourceExplorer.Tests.csproj --no-restore --filter "AssetGraphBuilder_BuildsSimGraphFromPreferredBodyDrivingTemplate|AssetGraphBuilder_PrefersTemplateWithAuthoritativeNudeOutfitOverRicherNonNudeVariant|AssetGraphBuilder_InspectsLeanAuthoritativeTemplateBeyondRicherSummaryWindow|SimTemplateSelectionPolicy_PrefersTemplateWithAuthoritativeBodyPartsOverRepresentative"` -> passed `4/4`
- live probe evidence: on the real indexed `Sim archetype: Human | Young Adult | Female`, the selected template now resolves to `SimulationFullBuild0.package | 025ED6F4:00000000:C51FC162CDEDDB26` with `Body-driving outfit records=1`, `BodyCandidates=5`, and `AssemblyMode=FullBodyShell`
- full verifier pass: `dotnet test Sims4ResourceExplorer.sln --no-restore` -> passed `272/272`
- post-contract verifier pass: `dotnet test Sims4ResourceExplorer.sln --no-restore` -> passed `286/286`

### Packet 6: Body-Shell Contract And Archetype Audit

- Goal: freeze the current cross-archetype body-shell contract, apply it consistently in template selection, and add a durable live-index survey for all `Sim Archetype` rows
- Status: `Completed`
- Owner: manager + worker + verifier
- Allowed write set: `docs/sim-body-shell-contract.md`, `docs/README.md`, `AGENT.md`, `docs/operations/tooling.md`, `docs/planning/current-plan.md`, `src/Sims4ResourceExplorer.Core/Domain.cs`, `src/Sims4ResourceExplorer.Assets/AssetServices.cs`, `tools/ProbeAsset/Program.cs`, related tests
- Verification: targeted template-selection/body-shell tests, full suite, then `tools/ProbeAsset` live-index survey across current `Sim Archetype` rows
- Red lines: no return to broad compatibility shell search, no mixing clothing/accessories into `Sim Archetype` body shell, no package-variant preference leaking into logical template selection
- Deliverable: one documented contract plus live evidence of which archetypes currently comply, resolve via indexed default/naked body recipe, or remain unresolved

Packet 6 verification:

- targeted verifier pass: `dotnet test tests/Sims4ResourceExplorer.Tests/Sims4ResourceExplorer.Tests.csproj --no-restore --filter "SqliteIndexStore_PersistsSimTemplateFactsAndBodyPartFacts|AssetGraphBuilder_UsesIndexedTemplateFacts_WhenTemplateSearchFallbackIsUnavailable|AssetGraphBuilder_UsesIndexedBodyPartFacts_WhenRawResourceLookupFallbackIsUnavailable|AssetGraphBuilder_BuildsSimGraphFromPreferredBodyDrivingTemplate|AssetGraphBuilder_PrefersTemplateWithAuthoritativeNudeOutfitOverRicherNonNudeVariant|AssetGraphBuilder_InspectsLeanAuthoritativeTemplateBeyondRicherSummaryWindow|SimTemplateSelectionPolicy_PrefersTemplateWithAuthoritativeBodyPartsOverRepresentative"` -> passed `7/7`
- full verifier pass after aligning stale body-shell fixtures and generic human fallback gating: `dotnet test Sims4ResourceExplorer.sln --no-restore` -> passed `286/286`
- follow-up audit-honesty fix: redirect notes and `ProbeAsset` survey no longer label `AssemblyMode=None` graphs as `ExplicitBodyDriving` just because `Body-driving outfit records > 0`
- focused verifier pass for the audit-honesty packet: `dotnet test tests/Sims4ResourceExplorer.Tests/Sims4ResourceExplorer.Tests.csproj --no-restore --filter "AssetGraphBuilder_UsesIndexedTemplateFacts_WhenTemplateSearchFallbackIsUnavailable|AssetGraphBuilder_LabelsUnresolvedExplicitTemplateAsBodyShellInspection|AssetGraphBuilder_BuildsSimMetadataGraph_WithAuthoritativeHeadCasSelections|AssetGraphBuilder_DoesNotUseFlattenedOutfitUnion_WhenNoAuthoritativeBodyDrivingOutfitExists|AssetGraphBuilder_PrefersTemplateWithAuthoritativeNudeOutfitOverRicherNonNudeVariant|AssetGraphBuilder_InspectsLeanAuthoritativeTemplateBeyondRicherSummaryWindow|SimTemplateSelectionPolicy_PrefersTemplateWithAuthoritativeBodyPartsOverRepresentative"` -> passed `7/7`
- full verifier pass after the audit-honesty fix: `dotnet test Sims4ResourceExplorer.sln --no-restore` -> passed `287/287`
- live archetype audit after the audit-honesty fix: `dotnet run --project tools/ProbeAsset/ProbeAsset.csproj -- --survey-sim-archetypes tmp/sim_archetype_body_shell_audit.json` -> surveyed `46` live rows with `ExplicitBodyDriving=25`, `IndexedDefaultBodyRecipe=0`, `Unresolved=21`
- live audit detail after the audit-honesty fix: assembly modes remain `FullBodyShell=18`, `SplitBodyLayers=7`, and `None=21`; the former `3` `ExplicitBodyDriving + None` rows (`Fox | Adult | Unisex`, `Human | Child | Male`, `Human | Infant | Female`) are now classified as `Unresolved` and report `Explicit body-driving outfit records existed, but no renderable body-shell layer was resolved.`
- seed-fact cache drift probe: the current live cache still stores zeroed facts for the same `3` selected templates, but fresh temp `ProbeAsset --profile-index` rebuilds on `EP11` and `EP17` repopulate them correctly (`Fox` `1/6`, `Human | Child | Male` `1/7`, `Human | Infant | Female` `1/8`), which isolates the defect to stale live cache content rather than the current extractor/persistence source path
- accepted follow-up fix: `SqliteIndexStore.InitializeAsync` now version-invalidates stale `sim_template_facts`, `sim_template_body_parts`, and `cas_part_facts` on cache open instead of silently trusting older seed-fact semantics
- focused verifier pass for the seed-fact invalidation packet: `dotnet test tests/Sims4ResourceExplorer.Tests/Sims4ResourceExplorer.Tests.csproj --no-restore --filter "SqliteIndexStore_PersistsSimTemplateFactsAndBodyPartFacts|SqliteIndexStore_InitializeInvalidatesStaleSeedFactTablesWithoutDroppingPackageFingerprints|PackageIndexCoordinator_SlicedPackagePersistsSimTemplateFactsOnlyOnFinalChunk"` -> passed `3/3`
- headless live rebuild seam: `ProbeAsset` now exposes `--rebuild-live-sim-archetypes [workerCount]`, which opens the real `%LOCALAPPDATA%\Sims4ResourceExplorer\Cache` through `FileSystemCacheService`, runs `PackageIndexCoordinator` against the configured live data sources, and immediately reruns the live `Sim Archetype` survey
- verifier pass after adding the headless live rebuild seam: `dotnet build tools/ProbeAsset/ProbeAsset.csproj --no-restore` -> succeeded; `dotnet test Sims4ResourceExplorer.sln --no-restore` -> passed `288/288`
- rebuilt live audit after seed-fact invalidation: `dotnet run --project tools/ProbeAsset/ProbeAsset.csproj -- --rebuild-live-sim-archetypes 8` -> rebuilt the real cache in `00:04:50`, then surveyed `46` live rows with `ExplicitBodyDriving=23`, `IndexedDefaultBodyRecipe=0`, and `Unresolved=23`
- rebuilt live audit detail: assembly modes now read `FullBodyShell=15`, `SplitBodyLayers=6`, `FallbackSingleLayer=2`, and `None=23`; the explicit-but-unrenderable frontier is now `6` rows (`Fox | Adult | Unisex`, `Human | Adult | Female`, `Human | Child | Female`, `Human | Child | Male`, `Human | Elder | Female`, `Human | Infant | Female`)

## Sequencing

Run Packet 1 first. Packet 2 should not start until the authoritative input map is concrete enough to define the seam cleanly. Packets 3 and 4 both depend on Packet 2's seam and may run in parallel only after that seam has been split enough to avoid collisions in `SimSceneComposer.cs`. Packet 5 runs after every implementation packet, not only at the end of the block.

## After This Block

Resume the next structural layers in order:

- rig/body/head integration
- skintone, region map, and morph/deformer application
