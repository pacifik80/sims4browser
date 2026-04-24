# Current Plan

This file is the live execution plan. Update it before work starts and keep it current while the request is still in progress.

## Mandatory Plan Shape

Every active plan in this file must include:

1. The problem being solved.
2. The chosen approach.
3. The actions to perform, with `[x]` and `[ ]` markers showing what is done and what is still pending.
4. Other hints needed to resume the work in a new chat if execution is interrupted.

The plan must be updated during the same user request, not only at session closeout.

Additional rule for the browser material-pipeline refactor:

- keep one stable conceptual progress tree with percentages across iterations
- update the percentages when a meaningful implementation boundary changes
- always mark:
  - what is materially solved
  - what is the current working zone
  - what is still essentially untouched

Status-reporting rule for this research track:

- use only progress-bearing docs in the user-facing status snapshot:
  - deep dives with explicit numeric status
  - family sheets with explicit numeric status
  - live-proof packets with explicit numeric status
- exclude core guides, hubs, tracking docs, and source-layer census docs from the percentage status view
- show status rows as:
  - colored square
  - document name
  - current percent
  - optional delta only if the current iteration changed that doc, for example `(+12%)`
- close the snapshot with one overall catalog-fill percentage computed from the shown docs only

## TS4 Material Research Restart Contract

If the active work is the external-first TS4 material, texture, shader, and UV research track, start here:

- [Research Restart Guide](../workflows/material-pipeline/research-restart-guide.md)

This restart contract overrides the common failure mode for that task:

- external sources, creator tooling, and local snapshots of external tools are the truth layer
- local corpus and precompiled summaries are candidate-target hints only
- current repo code is implementation boundary and failure evidence only, not TS4 truth
- each normal run should work as an autonomous long batch and advance multiple bounded packets when the next step is already clear from the docs
- each packet should still update the queue, matrix, and plan before the batch moves on
- stop a batch only at a real blocker, a context-safety boundary, or a natural integration checkpoint
- recovery heartbeat runs are insurance against interruption, not the primary work cadence
- each run should close with the compact report defined in the restart guide, including explicit trust-boundary separation

## Active Task

Status: `In Progress`

### Current Request Addendum (`2026-04-22`, browser material-pipeline refactor)

#### Problem

The browser preview still flattened too much material meaning before render time:

- shader-family and decode-strategy knowledge stopped at diagnostics instead of reaching viewport decisions
- slot-level UV intent could still be borrowed across unrelated slots
- helper, utility, projective, and layered-family inputs could still be misread as ordinary visible surface color
- there was no restart-safe implementation plan in the repo for continuing this refactor across chats

#### Chosen Approach

- move incrementally from heuristic texture-bag preview toward a canonical material pipeline
- preserve material authority in `CanonicalMaterial` first, then tighten viewport selection around that richer IR
- split work by boundary:
  - authority and decode propagation
  - viewport slot and UV routing
  - transparency classification
  - surface-versus-helper filtering
- later compositor staging for skintone and overlay/detail families
- keep the live implementation status in this file so a new chat can resume from repo state instead of reconstructing context manually

#### Conceptual Progress Tree

Status scale:

- `100%` = materially solved for the current browser scope
- `1-99%` = partially implemented / still approximation-heavy
- `0%` = not started
- parent nodes use conservative child rollups instead of momentum-style milestone estimates
- the root percentage should be read as an approximate weighted rollup, not as “remaining work is only X%”

```text
Browser Material-Pipeline Refactor [97%]
├─ 1. Canonical Material Authority And IR [100%]
│  ├─ 1.1 CanonicalMaterial field coverage [100%]
│  ├─ 1.2 Decode/scene propagation of authority [100%]
│  └─ 1.3 Scoped-material provenance preservation [100%]
├─ 2. Viewport Texture Selection And UV Routing [98%]
│  ├─ 2.1 Slot-local sampling and UV routing [100%]
│  ├─ 2.2 Surface vs helper/utility filtering [100%]
│  └─ 2.3 Selected-slot inspection safety [93%]
├─ 3. Transparency And Surface Classification [91%]
│  ├─ 3.1 Family-aware transparency ordering [100%]
│  ├─ 3.2 Non-visual/helper suppression [100%]
│  └─ 3.3 Stage-specific transparency policies [72%]
├─ 4. Preview Compositor Staging [97%]
│  ├─ 4.1 Stage metadata in scene IR [100%]
│  ├─ 4.2 Sim skintone base/overlay split [100%]
│  ├─ 4.3 CAS overlay base/detail split [94%]
│  ├─ 4.4 High-layer 32|65536 overlay family [93%]
│  └─ 4.5 Helper/projective staged handling [92%]
├─ 5. CompositionMethod And SortLayer Behavior [97%]
│  ├─ 5.1 CompositionMethod 2/3/4 approximation [96%]
│  ├─ 5.2 CompositionMethod 32 worn-slot lane [99%]
│  ├─ 5.3 SortLayer pass bucketing [99%]
│  └─ 5.4 True compositor pass math [100%]
└─ 6. TS4-Parity Compositor Behavior [84%]
   ├─ 6.1 Skintone-carried overlay parity [99%]
   ├─ 6.2 Ordinary CAS overlay/detail parity [99%]
   └─ 6.3 Runtime-faithful blend/order equivalence [98%]
```

Current working zone:

- `5.2 CompositionMethod 32 worn-slot lane`
- `5.3 SortLayer pass bucketing`
- `6.1 Skintone-carried overlay parity`
- `6.2 Ordinary CAS overlay/detail parity`
- `6.3 Runtime-faithful blend/order equivalence`

Not started / essentially untouched:
- no equally large compositor-family split remains inside the browser preview path; the dominant remaining gap is now exact runtime-faithful behavior layered on top of the separated preview/material paths

Interpretation note for `6.3`:

- work on architecture, pass planning, coarse blend families, and execution-policy cleanup does help `6.3`, but it is counted under `5.4`, `6.1`, and `6.2`
- `6.3` only moves once source-backed TS4 runtime blend/order rules are explicitly captured and enforced as browser behavior, not just approximated structurally

#### Actions

- [x] Extend `CanonicalMaterial` so shader family, decode strategy, sampling, and visual payload kind survive into viewport code.
- [x] Stop collapsing viewport materials into one synthetic texture-group before slot selection.
- [x] Make scoped viewport selection preserve material provenance instead of rebuilding a metadata-poor synthetic material.
- [x] Prevent per-slot preview from borrowing UV sampling from a different slot when the selected slot has no explicit sampling entry.
- [x] Use family-aware transparency ordering in the viewport:
  - object-glass
  - threshold/cutout
  - `AlphaBlended`
  - `SimGlass`
  - generic fallback
- [x] Keep non-visual helper/control materials out of normal transparency handling.
- [x] Propagate `UtilityTextureSlots` into `CanonicalMaterial` and keep them when the material is scoped.
- [x] Add a separate viewport `surfaceTextures` subset so base/layered/emissive/opacity selection ignores helper or utility inputs while normal/specular selection still sees them.
- [x] Propagate `CASPart` compositor metadata into `CanonicalMaterial`:
  - slot category
  - `CompositionMethod`
  - `SortLayer`
- [x] Use that metadata for an initial layer-aware mesh ordering pass in the viewport so shell/body layers and compositor-like rows no longer rely only on source mesh order.
- [x] Introduce explicit preview compositor stage metadata in `CanonicalMaterial` and propagate it from:
  - material decode (`surface`, layered surface, helper/projective, non-visual)
  - `CASPart` compositor metadata (`cas-shell-base`, `cas-overlay`)
  - skintone routing (`sim-skintone-base`)
- [x] Make normal preview stage-aware:
  - helper stages are no longer rendered as ordinary visible geometry in normal `Lit/Flat` preview
  - helper stages remain available in UV and slot-inspection modes
- [x] Start replacing single-material stage approximation with stage-specific preview texture behavior:
  - `cas-overlay` now prefers layered color payload and stage-aware alpha handling instead of default diffuse-first routing
  - `sim-skintone-base` now supports a first compositor approximation for `tinted base + layered detail (+ opacity)` in normal preview
- [x] Introduce the first real staged skintone preview split:
  - skintone-routed materials with both base and overlay payload can now expand into separate `sim-skintone-base` and `sim-skintone-overlay` material passes
  - viewport ordering now draws `sim-skintone-overlay` after `sim-skintone-base`
- [x] Introduce the first real staged CAS overlay preview split:
  - `cas-overlay` materials with both base and detail payload can now expand into separate `cas-overlay-base` and `cas-overlay-detail` preview passes
  - viewport ordering and overlay-specific alpha/color routing now target `cas-overlay-detail` instead of forcing one merged material path
- [x] Introduce the first `CompositionMethod`-aware overlay preview behavior:
  - `CompositionMethod = 3` now uses a grayscale-shading approximation for `cas-overlay-detail`
  - `CompositionMethod = 2` and `4` now adjust overlay alpha/emphasis as makeup-like preview lanes
  - `CompositionMethod = 32` now has a separate worn-slot preview policy for `Full Body` / `Top` / `Bottom` / `Shoes` / `Accessory`
  - `0/1` currently stay in the straight-overlay lane until stronger runtime math is available
- [x] Start turning `SortLayer` into compositor pass grouping instead of only one numeric tie-break:
  - `cas-overlay*` stages now split into low cosmetic, standard worn-slot, and `65536+` high-layer pass buckets
  - the dominant `32|65536` clothing/accessory lane now lands in a dedicated late overlay bucket before final per-layer numeric ordering
- [x] Start promoting the dominant `32|65536` lane into explicit scene stages:
  - eligible `CAS` overlay materials now expand into `cas-overlay-highlayer-base` and `cas-overlay-highlayer-detail`
  - viewport stage ordering now treats those rows as a separate late overlay family instead of only a bucketed sort case
- [x] Add stage-specific material rules for late high-layer overlays:
  - `cas-overlay-highlayer-detail` now uses explicit-emissive-only routing instead of generic layered/emissive fallback
  - the same stage now forces a more matte lit response so high worn-slot overlays stop behaving like cosmetic/detail rows
- [x] Add stage-specific transparency policy for high-layer overlay bases:
  - `cas-overlay-highlayer-base` now stays opaque unless an explicit opacity slot exists
  - high-layer base rows no longer inherit generic alpha-from-color fallback
- [x] Keep material-detail maps on overlay base passes instead of leaking them into overlay detail passes:
  - `cas-overlay-detail` and `cas-overlay-highlayer-detail` now exclude `normal/specular/gloss` textures during scene-stage split
  - material-detail maps stay attached to base-family passes where they belong
- [x] Mirror that contract in viewport material-detail application:
  - `cas-overlay-highlayer-detail` now explicitly suppresses `normal/specular` routing in the viewport
  - `highlayer-detail` is now treated as a color/opacity overlay lane instead of relying on IR split side-effects
- [x] Add a regression test for the CAS overlay split contract:
  - `CasOverlayDetailSplit_ExcludesMaterialDetailMapsFromDetailStageSelection` now locks the rule that `normal/specular/gloss` stay out of overlay detail-stage selection
  - test execution is still blocked by the unrelated `DiscoveredCasPartFact(... GenderLabel)` compile error in `IndexingPipelineTests.cs`
- [x] Add an explicit inspection contract for helper/projective stages:
  - `helper-projective`, `helper-layered`, and `helper-utility` now show a plain selected/first texture in inspection instead of surface-like material fallback
  - those helper stages now suppress emissive/alpha/normal/specular application when they are shown for inspection
- [x] Split helper inspection selection by helper family instead of one shared fallback:
  - `helper-projective` now prefers projective/refraction/depth-like slots during inspection
  - `helper-layered` now prefers layered/reveal/lightmap-like slots during inspection
  - `helper-utility` now prefers declared utility slots before generic first-texture fallback
- [x] Split helper sampling and UV routing policy by helper family:
  - `helper-projective` no longer borrows generic `diffuse/default` sampling fallback during inspection
  - `helper-layered` now prefers sampling from declared layered slots before any generic fallback
  - `helper-utility` now prefers sampling from declared utility slots before any generic fallback
- [x] Split helper UV/apply behavior by helper family:
  - helper-stage geometry UV selection now uses helper-aware UV reference textures instead of the generic primary-texture scorer
  - `helper-projective` no longer applies ordinary material UV transforms in preview, so projective helpers stop pretending to be surface UV lanes
- [x] Route UV preview panels through the helper-aware UV texture selector:
  - `RawUv` and `MaterialUv` preview panels now group around helper-specific UV reference textures instead of the old generic primary-texture choice
  - helper inspection is now more consistent between viewport geometry UVs and the generated UV panel preview
- [x] Add helper-family visual policy to UV inspection preview:
  - helper UV panels now receive stage-aware accent outlines instead of looking identical to ordinary surface panels
  - helper wireframes now use family-specific colors so `projective`, `layered`, and `utility` lanes are visually distinguishable in `RawUv/MaterialUv`
- [x] Add helper-family background and mixed-lane markers to UV inspection preview:
  - helper UV panels now use family-aware tinted panel backgrounds instead of a single neutral backdrop
  - mixed helper/surface groupings now get an explicit side marker so ambiguous UV packets stand out during inspection
- [x] Refine overlay compositor bucketing for cosmetic `CompositionMethod` lanes:
  - `CompositionMethod 2/3/4` detail overlays on `Head` / `Body` / `Hair` now get their own early cosmetic pass bucket
  - low nonzero `SortLayer` rows no longer collapse into the same bucket as true zero-layer overlays
  - high `65536+` / `CompositionMethod 32` worn-slot rows remain isolated as the latest overlay bucket
- [x] Align high-layer worn-slot bucketing with the actual scene-stage contract:
  - the latest overlay bucket now applies only to true `CompositionMethod 32` plus `SortLayer >= 65536` worn-slot rows
  - ordinary worn-slot `CompositionMethod 32` overlays no longer get promoted into the same late bucket as explicit `cas-overlay-highlayer-*` stages
- [x] Split ordinary worn `CompositionMethod 32` behavior from true high-layer `32|65536` behavior:
  - ordinary worn-slot `32` overlays now keep a less aggressive transparency fallback than explicit high-layer rows
  - high-layer rows keep the stricter subdued transparency/emissive contract, while ordinary worn rows use a milder shading response
- [x] Start consolidating overlay compositor behavior behind one lane classifier:
  - viewport pass bucket, `CompositionMethod 32` transparency policy, and overlay emissive policy now share the same overlay-lane classification
  - this is the first explicit bridge into `5.4`: compositor behavior is starting to move from scattered conditionals toward one pass-policy model
- [x] Extend the overlay lane classifier into alpha/composite policy:
  - grayscale overlay composite for `CompositionMethod 3` now runs through the shared overlay-lane model instead of a standalone condition
  - overlay alpha scaling now routes through lane-aware policy instead of a direct `CompositionMethod` switch
- [x] Replace scattered overlay-lane decisions with one `OverlayCompositorPolicy` object:
  - pass bucket, grayscale composite usage, alpha scaling, emissive response, and implicit-alpha permission now come from one shared policy builder
  - this is the first real `5.4` policy object rather than a loose set of helper predicates
- [x] Extend `OverlayCompositorPolicy` into specular/shininess response:
  - overlay detail lanes now read lane-specific specular color and shininess from the shared policy instead of local conditional branches
  - build stays clean after this move, so the policy object now covers nearly all overlay material-response decisions except a true staged blend engine
- [x] Add the first coarse staged blend model on top of `OverlayCompositorPolicy`:
  - overlay policy now carries explicit `PassIntent` and `BlendFamily`, instead of only a lane label plus independent booleans
  - overlay transparency and grayscale-composite decisions now route through those coarse staged blend families, giving `5.4` its first real pass-intent model
- [x] Extend the coarse staged blend model into overlay color/emissive routing:
  - overlay lanes now choose explicit-emissive-only vs fallback-emissive behavior from the shared policy object
  - overlay viewport color selection now follows policy-driven lane routing instead of special-casing high-layer rows inline
- [x] Add `OverlayExecutionPlan` as the first execution layer above compositor policy:
  - overlay detail passes now resolve emissive texture, viewport color texture, and alpha-render decision once, then pass that execution plan through the material path
  - this is the first explicit execution object for overlay detail rendering, not just a policy description
- [x] Turn `OverlayExecutionPlan` into executable texture/alpha branches:
  - overlay detail texture creation now routes through explicit execution helpers keyed by `PassIntent`
  - alpha texture generation now uses the resolved `AlphaSourceTexture` from the execution plan instead of recomputing overlay alpha inputs ad hoc
- [x] Move final overlay-detail material response into an execution-plan helper:
  - overlay detail `EmissiveColor`, `RenderEmissiveMap`, `EmissiveMap`, `SpecularColor`, and `SpecularShininess` now come from `OverlayMaterialResponsePlan`
  - final `PhongMaterial` application for overlay detail is no longer assembled only from inline local conditionals
- [x] Move final overlay-detail map application into an execution-plan helper:
  - overlay detail `RenderDiffuseMap`, `DiffuseMap`, `RenderDiffuseAlphaMap`, `DiffuseAlphaMap`, and final overlay map suppression now come from `OverlayMaterialApplicationPlan`
  - overlay detail no longer falls through the generic `PhongMaterial` map-application path for its final diffuse/emissive/alpha/normal/specular decisions
- [x] Add the first real multi-pass overlay execution step in the viewport render loop:
  - eligible worn/high-layer overlay detail rows now emit a late emissive-only secondary pass instead of forcing all overlay behavior through one `PhongMaterial`
  - the browser now has a coarse two-pass approximation for the strongest overlay family without claiming runtime-faithful TS4 blend math
- [x] Extend explicit secondary-pass rendering to cosmetic and ordered overlay detail lanes:
  - `CosmeticDetail` and `OrderedDetail` rows now emit a dedicated late detail pass instead of staying collapsed into one primary material path
  - the viewport now has separate coarse pass behavior for cosmetic/ordered detail rows versus worn/high-layer rows, which moves ordinary CAS overlay/detail parity forward without claiming exact TS4 blending
- [x] Move coarse multi-pass rendering from “per-mesh immediate extra draw” to explicit pass phases:
  - viewport render entries now carry `Primary`, `DetailLate`, and `EmissiveLate` phases instead of appending extra overlay draws only as an inline side effect of one mesh loop
  - coarse secondary passes are now globally ordered inside the browser render loop, which is a more honest approximation of compositor execution than drawing every extra pass immediately after its primary mesh
- [x] Add pass-specific alpha policy for coarse secondary overlay passes:
  - `EmissiveLate` now keeps a stricter alpha contract for worn/high-layer rows instead of inheriting generic secondary-pass transparency
  - `DetailLate` now uses a separate alpha rule for cosmetic versus ordered detail rows, so secondary overlay passes differ not only by phase but also by coarse opacity behavior
- [x] Consolidate coarse secondary-pass behavior behind `SecondaryOverlayPassPolicy`:
  - secondary overlay passes now derive `blend intent`, `alpha scale`, and `emissive response` from one policy builder instead of separate late-pass helper conditionals
  - `DetailLate` and `EmissiveLate` now differ by explicit coarse blend families, not only by phase ordering and ad hoc alpha checks
- [x] Rebuild viewport overlay execution around explicit render-pass plans:
  - viewport now builds `ViewportRenderPassPlan` entries first and sorts them globally by stage, bucket, phase, slot order, `CompositionMethod`, and `SortLayer`
  - overlay execution is no longer modeled as “iterate meshes, then opportunistically append extras”; it is now a planned pass list
- [x] Promote `DefaultDetail` into the explicit secondary-pass family:
  - default overlay-detail rows now use the same explicit `DetailLate` infrastructure as cosmetic and ordered rows
  - this gives ordinary overlay/detail families a broader coarse multi-pass contract instead of reserving explicit secondary passes only for special lanes
- [x] Extend explicit render-pass planning into `sim-skintone-overlay`:
  - skintone overlay rows now emit an explicit `SkintoneLate` pass inside the same planned viewport render-pass list instead of staying a one-material branch
  - the browser now applies a dedicated skintone late-pass policy with its own blend intent, alpha scale, and emissive response, which materially advances the `sim-skintone` compositor branch beyond the earlier base/overlay split
- [x] Unify late-pass material construction behind a shared viewport late-pass plan:
  - `DetailLate`, `EmissiveLate`, and `SkintoneLate` now converge on one `ViewportLatePassMaterialPlan` + one generic late-pass material builder instead of three isolated `PhongMaterial` code paths
  - skintone late-pass alpha now uses a real alpha-source contract again instead of always falling back to viewport color bytes
- [x] Add `SkintoneOverlayExecutionPlan` so skintone overlay primary and late passes share one execution seam:
  - `sim-skintone-overlay` now resolves viewport color and alpha-source textures once before primary/late material application
  - primary transparency and late-pass alpha routing for skintone overlays now use the same execution data instead of divergent local heuristics
- [x] Make planned viewport pass phases intent-aware instead of variant-only:
  - `DetailLate` now separates cosmetic/default/ordered detail rows by pass phase
  - `EmissiveLate` now separates ordinary worn rows from explicit high-layer rows by pass phase, which makes the planned pass list closer to a compositor execution order
- [x] Consolidate all late-pass execution behind one shared execution-policy layer:
  - `SkintoneLate`, `DetailLate`, and `EmissiveLate` now classify into one shared `ViewportLatePassBlendIntent` space instead of keeping separate skintone-vs-overlay pass policy trees
  - late-pass phase, alpha scaling, alpha enablement, and emissive response are now derived from one `ViewportLatePassExecutionPolicy`
- [x] Replace parallel skintone/overlay late-pass policy stacks with one coarse compositor contract:
  - skintone late passes now use the same policy seam as ordinary overlay detail and worn/high-layer late passes
  - the browser no longer needs separate `SecondaryOverlayPassPolicy` and `SkintoneOverlayPassPolicy` layers to describe late-pass behavior
- [x] Start `6.3` with the first explicit source-backed compositor ordering rule:
  - planned viewport render passes now keep `SkintoneLate` ahead of CAS overlay late passes as a parity-specific family rule
  - within CAS late-pass compositor rows, parity ordering now uses `CompositionMethod` before `SortLayer`, matching the current research packet's strongest safe reference-code-backed rule
- [x] Tighten `6.3` so ordinary CAS late rows no longer let blend-intent phases override composition-order evidence:
  - ordinary `DetailLate` rows now stay in one coarse pass phase, so source-backed `CompositionMethod -> SortLayer` ordering can actually govern their internal order
  - the browser now uses blend-intent phases only for coarse family separation, not as a hidden replacement for documented composition-order precedence
- [x] Extend `6.3` from parity ordering into parity opacity authority:
  - planned viewport passes now carry a `ViewportParityRuleProfile` instead of isolated parity integers, so source-backed compositor rules stay attached to the pass all the way into execution policy
  - documented `CompositionMethod 2` and `4` makeup lanes now stop borrowing plain viewport-color alpha fallback when no separate opacity authority exists
  - `SkintoneLate` now follows the same stricter parity-opacity contract, so late skintone opacity no longer quietly falls back to generic color-alpha authority
- [x] Extend `6.3` from parity opacity authority into a parity behavior matrix:
  - planned viewport passes now classify source-backed late lanes into explicit parity blend families instead of leaving that behavior implicit inside generic late-pass switches
  - documented `CompositionMethod 2/3/4` lanes now carry distinct runtime-facing late-pass behavior profiles (`makeup primary`, `grayscale`, `makeup secondary`) through the shared execution-policy seam
  - `SkintoneLate` now participates in the same parity behavior matrix, so skintone and CAS late lanes share one source-backed behavior layer instead of parallel special cases
- [x] Extend `6.3` from a parity behavior matrix into explicit late-family stack order:
  - planned viewport passes now carry an explicit `ParityStackOrder`, so late-family precedence is no longer inferred only from coarse phase plus incidental tie-breakers
  - skintone, ordinary detail, makeup-primary, grayscale, makeup-secondary, worn emissive, and high-layer emissive late families now sit on one explicit stack-order ladder inside the planned renderer
- [x] Start using the new `6.3` stack contract to suppress unsafe runtime approximations:
  - makeup-driven `CompositionMethod 2/4` late passes are now dropped when the browser cannot confirm dedicated opacity authority, instead of silently rendering a generic fallback late pass
  - this removes one concrete parity error where documented makeup-opacity lanes could previously survive as ordinary late overlays even after the new parity-opacity rules said they should not
- [x] Extend `6.3` from stack/authority rules into late-pass material modes:
  - source-backed late families now choose explicit late material modes instead of all collapsing into one emissive-only material path
  - ordinary detail, skintone, and makeup late lanes now render through diffuse-overlay late materials, while worn/high-layer emissive lanes stay on emissive-only late materials
  - the grayscale late lane now uses its own diffuse-grayscale material mode, so `CompositionMethod 3` is no longer only a policy label on top of the same generic late-pass renderer
- [x] Extend `6.3` from late-pass material modes into an explicit inter-family stack contract:
  - late families now carry stack contracts with family-specific tint and alpha-scale multipliers instead of sharing one neutral diffuse/emissive response inside each material mode
  - skintone, makeup-primary, makeup-secondary, grayscale, generic detail, and emissive late families now coexist through one shared renderer seam with visibly different stack behavior
  - the renderer now applies those stack contracts directly when building late-pass materials, so inter-family blend approximation no longer lives only in sort order and policy metadata
- [x] Extend the new `6.3` stack contract into family-specific no-alpha coexistence behavior:
  - late-pass materials now use family-specific fallback alpha and emissive attenuation instead of one shared no-alpha response
  - skintone, makeup-primary, makeup-secondary, grayscale, generic detail, and emissive late families now degrade differently when alpha authority is missing, which prevents them from collapsing into one generic fully-opaque late overlay look
  - this pushes the browser one step closer to a real compositor coexistence model instead of treating “no alpha map” as the same case for every late family
- [x] Extend `6.3` into an explicit late-pass transparency contract:
  - planned render passes no longer mark every late family as transparent by default
  - emissive late families now stay transparent only when they actually have explicit opacity authority, while diffuse-like late families still keep transparent handling
  - this moves one more compositor distinction out of `PhongMaterial` internals and into the render-plan contract itself
- [x] Extend `6.3` into an explicit late-pass underlay-carrier contract:
  - secondary late passes are now planned only when the scene contains a compatible underlay family instead of being emitted unconditionally
  - skintone late passes now require `sim-skintone-base`, while ordinary detail and emissive late families require shell/surface/base overlay carrier stages
  - this prevents isolated late overlays from being planned as if they were self-sufficient compositor layers
- [x] Tighten the underlay-carrier contract for makeup-driven late lanes:
  - `CompositionMethod 2/4` late passes now require `sim-skintone-base` specifically instead of sharing the same generic overlay underlay rule as ordinary detail rows
  - this follows the current strongest local packet, where makeup-oriented composition lanes depend on `TONE` makeup opacity inputs rather than behaving like free-standing generic overlay rows
- [x] Consolidate late-family runtime relations into one explicit contract layer:
  - stack order, underlay-carrier requirements, and transparency mode for late families now come from one `ViewportLatePassFamilyContract` instead of three separate helper trees
  - skintone, generic detail, makeup-primary, grayscale, makeup-secondary, emissive-soft, and emissive-restricted late lanes now carry explicit relation tags in code
  - this turns the remaining `6.3` work from scattered rule edits into a real runtime-gap matrix seam
- [x] Extend the late-family runtime matrix into scene-aware relation profiles:
  - planned late passes now carry scene-aware sibling-anchor/coexistence state instead of relying only on family-local defaults
  - render-plan transparency for late families now uses that relation profile, so emissive families can degrade differently when no earlier diffuse-like late anchor exists in the scene
  - final late-pass material response now also uses the same relation profile, so skintone, makeup, grayscale, generic detail, and emissive families get scene-aware alpha/emissive attenuation instead of one family-only fallback
- [x] Add an explicit pairwise runtime-relation matrix for late families:
  - `GenericDetail` versus `GrayscaleDetail`, `MakeupPrimary` versus `MakeupSecondary`, and `EmissiveSoft` versus `EmissiveRestricted` now have dedicated pairwise relation entries instead of being implied only by base family order
  - those pairwise relations now feed effective late-pass stack order and scene-aware alpha/emissive modulation, so the browser no longer relies only on family-local defaults when multiple late families coexist
  - this turns the remaining `6.3` work from “one family at a time” tweaks into a concrete pairwise parity seam that can absorb additional TS4 runtime rules directly
- [x] Extend the pairwise runtime-relation matrix with skintone and `2 -> 3 -> 4` compositor rules:
  - `MakeupPrimary` and `MakeupSecondary` now carry explicit pairwise relations against `Skintone`, instead of relying only on a generic skintone underlay requirement
  - `MakeupPrimary`, `GrayscaleDetail`, and `MakeupSecondary` now also carry explicit pairwise relations that preserve the documented `CompositionMethod 2 -> 3 -> 4` lane ordering as a runtime-facing coexistence rule
  - those relations now affect effective late-pass stack order plus scene-aware alpha/emissive modulation, so the browser no longer treats that `2/3/4` separation as only a base family-order convention
- [x] Extend the pairwise runtime-relation matrix with `Skintone -> Generic/Grayscale` late-family rules:
  - `Skintone` now carries an explicit pairwise relation against `GenericDetail`, instead of relying only on the coarse base family-order gap
  - `GenericDetail` and `GrayscaleDetail` now also carry explicit pairwise relations against `Skintone`, so ordinary post-skintone CAS detail late rows are represented as runtime-facing coexistence rules rather than only as separate family buckets
  - this pushes the strongest current “skintone branch before ordinary overlay/detail branch” reading deeper into the actual pairwise matrix seam used by the browser
- [x] Extend the pairwise runtime-relation matrix with diffuse-like versus emissive late-family rules:
  - `Skintone`, `GenericDetail`, `MakeupPrimary`, `GrayscaleDetail`, and `MakeupSecondary` now all carry explicit pairwise relations against `EmissiveSoft` and `EmissiveRestricted`
  - `EmissiveSoft` and `EmissiveRestricted` now also carry reciprocal pairwise relations against those diffuse-like late families, instead of relying only on coarse base stack numbers and family-local fallback logic
  - this pushes the browser’s “detail/makeup/skintone before emissive late lanes” reading into the real pairwise runtime matrix used for effective late-pass stack order and scene-aware response
- [x] Extend the pairwise runtime-relation matrix with `GenericDetail <-> Makeup*` ordinary CAS late rules:
  - `GenericDetail` now carries explicit pairwise relations against `MakeupPrimary` and `MakeupSecondary`, instead of leaving that ordering entirely to coarse family/local stack defaults
  - `MakeupPrimary` and `MakeupSecondary` now also carry reciprocal pairwise relations against `GenericDetail`, so ordinary CAS late rows are represented more completely inside the matrix rather than split between matrix rules and implicit base-family gaps
  - this closes most of the remaining safe pairwise ordinary-CAS late relations that were still missing after the earlier `2 -> 3 -> 4` and skintone closures
- [x] Split documented `CompositionMethod 2/3/4` lanes into explicit preview pass variants:
  - `MakeupPrimaryLate`, `GrayscaleLate`, and `MakeupSecondaryLate` now exist as separate late-pass variants instead of being hidden inside one generic `DetailLate`
  - viewport secondary-pass planning now emits those explicit variants directly from `CompositionMethod 2/3/4`, so the renderer distinguishes them at pass-type level before it even reaches blend-intent or parity-policy logic
  - this is the first direct move from “one generic detail-late approximation with smarter policy” toward real compositor-facing preview stages for ordinary CAS overlay/detail families
- [x] Split ordinary generic detail late lanes into explicit preview pass variants:
  - `CosmeticDetailLate`, `DefaultDetailLate`, and `OrderedDetailLate` now exist as separate late-pass variants instead of sharing one catch-all `DetailLate`
  - viewport secondary-pass planning now emits those explicit variants directly from `OverlayPassIntent`, so ordinary CAS late rows are distinguished at pass-type level before blend-intent/policy resolution
  - this pushes ordinary CAS overlay/detail families further out of the single-variant approximation path and closer to real compositor-facing preview stages
- [x] Split emissive late lanes into explicit preview pass variants:
  - `WornEmissiveLate` and `HighLayerEmissiveLate` now exist as separate late-pass variants instead of sharing one generic `EmissiveLate`
  - viewport secondary-pass planning now emits those explicit variants directly from `OverlayPassIntent.WornLayer` and `OverlayPassIntent.HighLayer`, so worn/high-layer emissive rows are distinguished at pass-type level before late-pass policy resolution
  - this completes the current explicit late-pass taxonomy for ordinary CAS overlay/detail families and the `32|65536` high-layer family, pushing preview staging closer to real compositor-facing pass structure
- [x] Add explicit primary-pass material plans for the strongest base-side compositor carriers:
  - `sim-skintone-base` and `cas-overlay-highlayer-base` now route through `ViewportPrimaryPassMaterialPlan` instead of surviving only as local branches inside the generic primary `PhongMaterial` builder
  - those base stages now use explicit carrier-stage rules for lit emissive suppression, alpha authority, and base-side shading response before the fallback generic primary path is even considered
  - this is the first real primary/base-side execution seam parallel to the already explicit late-pass taxonomy, and it moves `4.2` and `4.4` closer to true preview stages instead of one shared material-path approximation
- [x] Extend the explicit primary-pass carrier seam to ordinary `cas-overlay-base`:
  - `cas-overlay-base` now routes through the same `ViewportPrimaryPassMaterialPlan` layer as `sim-skintone-base` and `cas-overlay-highlayer-base`, instead of falling back to the generic primary `PhongMaterial` builder
  - ordinary overlay-base carriers now use explicit carrier-stage alpha authority and base-side ambient/specular response, which keeps the base/detail split more honest on the primary side before late overlay passes are added on top
  - this materially advances `4.3`, because ordinary overlay-base is no longer the main unplanned carrier-family while skintone/high-layer already had explicit primary staging
- [x] Promote strongest base carriers into explicit primary-pass planning families:
  - `sim-skintone-base`, `cas-overlay-base`, and `cas-overlay-highlayer-base` now resolve through a shared `ViewportPrimaryPassContract` with explicit family identity and primary stack order before the renderer reaches material assembly
  - `ViewportRenderPassPlan` now carries primary-family ordering, so these carrier stages differ already at planning/sort time instead of only diverging later inside `CreateMaterial(...)`
  - this turns the new primary carrier seam into a compositor-facing planning layer, not just a material-builder refactor, and pushes `4.2/4.3/4.4` closer to true preview stages
- [x] Split strongest base carriers into explicit primary pass variants:
  - `SkintoneBasePrimary`, `OverlayBasePrimary`, and `HighLayerBasePrimary` now exist as explicit renderer-facing primary pass variants instead of being folded into one generic `Primary`
  - viewport render-pass planning now emits those explicit primary variants up front, and `CreateMaterial(...)` resolves primary carrier contracts through the variant itself instead of relying only on stage-local branching
  - this makes the strongest base carriers match the explicit late-pass taxonomy shape and materially advances the browser toward real preview stages rather than one generic primary material path with smarter contracts
- [x] Add explicit base-pass execution plans for strongest primary carriers:
  - `SkintoneBasePrimary`, `OverlayBasePrimary`, and `HighLayerBasePrimary` now resolve through `ViewportPrimaryPassExecutionPlan`, so strongest base carriers differ not only by variant and contract but also by an explicit execution seam before final material assembly
  - `BuildViewportPrimaryPassMaterialPlan(...)` now consumes `ViewportPrimaryPassExecutionPlan` instead of deciding all primary map application inline, which makes the base side structurally closer to the already explicit late-pass execution layer
  - this materially advances `4.2/4.3/4.4`, because the strongest base carriers now have explicit pass type, explicit contract, and explicit execution plan instead of only one of those layers
- [x] Split strongest primary carriers into response/application plans:
  - strongest base carriers now go through `ViewportPrimaryPassResponsePlan` and `ViewportPrimaryPassApplicationPlan`, not just one `ViewportPrimaryPassExecutionPlan`
  - `BuildViewportPrimaryPassMaterialPlan(...)` now consumes explicit primary execution, response, and application layers before final `PhongMaterial` assembly, which makes the base side structurally parallel to the overlay late-pass architecture instead of stopping at one generic base-material builder
  - this materially advances `4.2/4.3/4.4`, because skintone/overlay/highlayer base carriers now have explicit pass type, contract, execution, response, and application seams
- [x] Add family-specific primary carrier behavior on the base side:
  - `SkintoneCarrier`, `OverlayCarrier`, and `HighLayerCarrier` now differ in primary alpha scaling and detail-map application, instead of only sharing the same plans with different tags
  - skintone base now keeps a softer alpha profile and suppresses primary specular-map application, ordinary overlay-base keeps the full detail-map lane, and high-layer base keeps a more attenuated alpha profile
  - this pushes the strongest base carriers from “structurally explicit” to “behaviorally distinct” and is the first real family-specific base-pass behavior layer parallel to the late-pass family contracts
- [x] Add contract-level primary response scales for strongest base carriers:
  - `SkintoneCarrier`, `OverlayCarrier`, and `HighLayerCarrier` now differ in diffuse/ambient/specular response through explicit contract scales and shininess multipliers, instead of relying only on a few local `if` branches
  - skintone base now carries a softer specular and slightly warmer ambient response, ordinary overlay-base stays near-neutral, and high-layer base keeps a flatter/more attenuated specular response
  - this pushes the strongest base carriers from merely explicit primary staging into explicit family-level primary response behavior, and effectively closes the main base-side staging seam for `4.2` and `4.3`
- [x] Promote helper/projective families into explicit primary-pass contracts:
  - `helper-projective`, `helper-layered`, and `helper-utility` now resolve through explicit `Helper*Primary` pass variants plus dedicated `ViewportPrimaryPassFamily` / `ViewportPrimaryPassIntent` entries instead of falling through one generic primary path
  - helper primary families now have their own stack order, execution plan, and family-specific base response/application behavior, including suppressed alpha/normal/specular routing and non-shadowing inspection-style rendering
  - this materially advances `4.5`, because helper/projective handling now reaches compositor-facing primary planning and behavior layers instead of stopping at slot-inspection-only heuristics
- [x] Move primary transparency onto the explicit primary-pass contract:
  - primary render-pass planning now resolves transparency through `ViewportPrimaryPassContract` instead of always deferring to the generic `IsTransparentMaterial(...)` fallback
  - helper primary families are now forced opaque as inspection carriers, while `skintone-base`, `overlay-base`, and `highlayer-base` only become transparent when their own explicit opacity-authority rules allow it
  - this is a correctness-first boundary: material families now keep their own transparency semantics even if that means the old preview UX stops getting permissive alpha fallback for convenience
- [x] Move primary alpha-map assembly onto the explicit primary-pass contract:
  - `CreateMaterial(...)` now routes primary `renderAlphaMap` and primary alpha-texture creation through the same `ViewportPrimaryPassContract` rules instead of letting base carriers silently keep the old generic alpha fallback in material assembly
  - `skintone-base`, `overlay-base`, `highlayer-base`, and helper primary families now use one shared primary transparency truth layer for both pass planning and actual `PhongMaterial` alpha application
  - this removes a real correctness bug where primary carrier contracts could already say “explicit opacity only” while the assembled material still inherited alpha from the generic viewport path
- [x] Move primary-pass inclusion onto the explicit primary-family contract:
  - viewport render-pass planning no longer drops whole materials through the old `ShouldRenderViewportStage(...)` stage-switch before pass expansion; instead, primary passes are included per-pass through the resolved `ViewportPrimaryPassContract`
  - helper primary families are now excluded from normal `Lit/Flat` planning because their own primary-family contract says they are helper carriers, not because the old Preview stage gate happened to hard-code helper stage names
  - this pushes `4.5` further away from legacy UX-driven gating and toward true pass-based planning, which is the right correctness boundary before any future Preview UX rewrite
- [x] Move primary render-stage ordering onto the explicit primary-family contract:
  - primary `ViewportRenderPassPlan.RenderStage` no longer depends on `PreviewCompositorStage` alone when a resolved primary-family contract already exists; `SkintoneBase`, `OverlayBase`, `HighLayerOverlayBase`, and helper primary families now carry their own explicit render-stage order
  - this means primary pass ordering is now contract-backed at the same planning layer as primary inclusion, transparency, and stack order, instead of still leaning on the legacy stage-string switch for final ordering
  - this is another correctness-first step toward real pass planning: the primary pipeline is now materially less coupled to old Preview stage labels even before the UX is rewritten around it
- [x] Move skintone-base texture composite selection onto the explicit primary-family contract:
  - the primary texture-composite path no longer checks raw `PreviewCompositorStage == sim-skintone-base` to decide whether to build the tinted skintone base composite; it now keys off resolved `ViewportPrimaryPassContract.Family`
  - this means primary-side texture composition is now more consistent with the already contract-driven primary inclusion, transparency, stack order, and render-stage rules
  - this effectively closes the main structural seam for `4.2`: skintone base behavior on the primary side is now routed through the explicit primary-family contract instead of lingering as a stage-string-only special case
- [x] Move helper inspection behavior inside `CreateMaterial(...)` onto the explicit primary-family contract:
  - helper inspection texture selection, helper inspection suppression, and projective zero-UV behavior on the primary path no longer derive their identity from raw `IsHelper*Stage(...)` checks alone; they now key off resolved helper primary families from `ViewportPrimaryPassContract`
  - this means helper primary material assembly is now more aligned with the already contract-driven helper pass inclusion, transparency, stack order, render stage, and execution behavior
  - this pushes `4.5` further away from legacy stage-bool branching inside the primary material path and makes the remaining helper/projective gap much narrower and more isolated
- [x] Centralize primary-family resolution and move helper sampling onto the explicit primary-family contract:
  - strongest primary families now resolve through one `ResolveViewportPrimaryPassVariant(...)` seam before `BuildViewportPrimaryPassContract(...)`, instead of repeating raw `passVariant || Is*Stage(...)` checks inside the contract builder itself
  - helper sampling in the core primary material path now accepts the resolved `ViewportPrimaryPassContract` and keys its projective/layered/utility behavior off explicit helper families rather than re-deriving that identity only from scattered `IsHelper*Stage(...)` branches
  - this narrows the remaining `4.5` gap further: the primary/base path is now more consistently contract-driven across variant resolution, contract assembly, helper inspection behavior, and helper UV/sampling behavior
- [x] Move high-layer base transparency through the resolved primary-family contract across both primary and shared alpha helpers:
  - high-layer base transparency on the primary path no longer falls back to `IsCasOverlayHighLayerBaseStage(...)`; `CreateMaterial(...)`, `ShouldRenderViewportPrimaryTransparency(...)`, and the shared `CreateViewportAlphaTextureModel(...)` helper now all route through the resolved `ViewportPrimaryPassContract`
  - this removes another split-brain seam where high-layer base transparency could already be contract-driven in one primary path but still be stage-driven in shared alpha assembly helpers
  - this pushes `4.4` further away from raw stage-string/base-stage booleans and keeps high-layer base behavior more consistently aligned with the new contract-driven primary pipeline
- [x] Move helper UV routing in the core preview path onto the explicit primary-family contract:
  - `SelectTextureCoordinates(...)`, `SelectViewportUvTexture(...)`, and `SelectViewportSampling(...)` now resolve helper projective/layered/utility behavior through `ViewportPrimaryPassContract` instead of re-deriving helper identity only from raw `IsHelper*Stage(...)` checks
  - this means the core preview UV/material path now treats helper families the same way the primary material path already does: through resolved family identity first, with stage-string helpers remaining mostly isolated to UV-panel cosmetics and restart-safe legacy fallbacks
  - this narrows the remaining `4.5` gap again, because helper/projective handling is now more consistently contract-driven across inclusion, transparency, material assembly, sampling, UV texture selection, and UV transform suppression
- [x] Move helper UV preview styling and wireframe coloring onto the explicit primary-family contract:
  - `GetUvPreviewPanelStyle(...)`, `GetUvPreviewWireframeColor(...)`, and the legacy `IsHelperInspectionStage(...)` helper now resolve helper identity through `ViewportPrimaryPassContract` / `ViewportPrimaryPassFamily` instead of reading helper stage names directly
  - this isolates raw helper stage-string checks even further: helper-family recognition for UV preview cosmetics now uses the same contract seam as pass planning, transparency, material assembly, and UV routing
  - this pushes `4.5` close to closure for the current browser scope, with remaining helper-stage raw checks now mostly concentrated inside the primary-family resolver itself and a few intentionally legacy fallbacks
- [x] Isolate strongest primary-family stage-string logic behind one resolver:
  - the old raw helpers for `sim-skintone-base`, `cas-overlay-base`, `cas-overlay-highlayer-base`, and helper `projective/layered/utility` no longer exist as six separate mini-predicates; strongest primary-family fallback now routes through `ResolveViewportPrimaryPassFamilyFromStage(...)`
  - `ResolveViewportPrimaryPassVariant(...)`, helper inspection detection, and overlay-pass stage checks now all consume that shared family resolver instead of repeating separate stage-name tests
  - this is the cleanest remaining `4.5` cleanup boundary for the current browser scope: raw stage-string logic for strongest primary families is now largely isolated to one resolver/fallback seam instead of being scattered across the material and preview code
- [x] Make `SortLayer` bucketing explicit and separate ordinary worn `CompositionMethod 32` from ordered rows:
  - overlay pass classification now resolves through an explicit `OverlaySortLayerBucket` helper instead of re-deriving `SortLayer` thresholds ad hoc in multiple places
  - ordinary worn `CompositionMethod 32` rows now receive their own later pass bucket instead of sharing the same approximate bucket value as `OrderedHigh` rows, which makes `5.2` and `5.3` less entangled
  - generic detail late-family stack order no longer hangs on the old `100 + CompositionMethod` shortcut; it now resolves through explicit `blend-intent + SortLayerBucket` rules so ordered detail rows carry a clearer compositor-facing stack contract
- [x] Centralize `CompositionMethod 2/3/4` into one shared composition-rule layer:
  - `MakeupPrimary`, `Grayscale`, and `MakeupSecondary` no longer get decoded through three separate local switch trees; `BuildViewportCompositionRuleProfile(...)` now resolves their pass variant, late blend-intent, parity opacity authority, parity blend family, and composition order in one place
  - `GetViewportSecondaryPassVariants(...)`, `BuildViewportParityRuleProfile(...)`, and `GetViewportLatePassBlendIntent(...)` now all consume that shared composition-rule profile instead of repeating parallel `2/3/4` mappings
  - this is a real `5.1` boundary rather than a cosmetic refactor: explicit `CompositionMethod 2/3/4` stages, parity behavior, and late-pass intent are now less likely to drift apart because they share one rule source
- [x] Extend the shared `CompositionMethod 2/3/4` rule layer into late-family contract and parity behavior:
  - `BuildViewportLatePassFamilyContract(...)` and `BuildViewportParityBehaviorMatrixEntry(...)` now consume `ViewportCompositionRuleProfile(...)` for makeup-primary, grayscale, and makeup-secondary lanes instead of keeping separate local stacks/material-mode/opacity-contract numbers for those same three methods
  - this means `CompositionMethod 2/3/4` now share one rule source across pass variant, late blend-intent, parity profile, late-family stack contract, and parity behavior matrix
  - this materially reduces drift risk in `5.1`: those explicit compositor lanes are now driven by one richer rule profile instead of multiple parallel switch blocks with duplicated constants
- [x] Extend the shared `CompositionMethod 2/3/4` rule layer into pass phase and initial alpha gating:
  - `GetViewportParityPassPhase(...)` now resolves `2/3/4` pass phases through `ViewportCompositionRuleProfile(...)` instead of keeping dedicated local parity-blend-family branches for those methods
  - `BuildViewportLatePassExecutionPolicy(...)` now resolves initial alpha-render gating for makeup-primary, grayscale, and makeup-secondary through the same shared profile instead of a separate local `blendIntent` switch
  - this narrows the remaining `5.1` gap further: the explicit `2/3/4` lanes now drive pass variant, blend intent, parity profile, family contract, behavior matrix, pass phase, and initial alpha gating from one shared rule source
- [x] Extend the shared `CompositionMethod 2/3/4` rule layer into coarse overlay policy:
  - `BuildOverlayCompositorPolicy(...)` no longer keeps separate local `2/3/4` special-cases for grayscale blend-family selection, makeup opacity scale, and makeup emissive response; those now resolve from `ViewportCompositionRuleProfile(...)`
  - this means explicit `2/3/4` lanes now influence not only late-pass staging and parity behavior but also the earlier coarse overlay material-policy layer through the same shared rule source
  - this is another real `5.1` boundary: `CompositionMethod 2/3/4` are now materially less fragmented across the browser compositor pipeline because overlay policy, pass selection, parity profile, family contract, behavior matrix, pass phase, and initial alpha gating all consume the same richer profile
- [x] Align the shared `CompositionMethod 2/3/4` rule layer with the cosmetic-lane classifier:
  - `BuildViewportCompositionRuleProfile(...)` now only activates for the same cosmetic lane contract that `ClassifyOverlayCompositorLane(...)` uses, instead of firing for every `cas-overlay-detail` row with `CompositionMethod 2/3/4`
  - this removes a real drift seam where explicit makeup/grayscale stages could have been emitted more broadly than the overlay lane classifier considered valid for cosmetic composition rows
  - this makes `5.1` more trustworthy: the shared `2/3/4` rule source is now not only richer but also better aligned with the lane-classification boundary that feeds the rest of the compositor policy stack
- [x] Extend the shared `CompositionMethod 2/3/4` rule layer into early overlay alpha-source and transparency routing:
  - `BuildOverlayExecutionPlan(...)`, `CreateViewportAlphaTextureModel(...)`, and `ShouldRenderOverlayPolicyTransparency(...)` now consume `ViewportCompositionRuleProfile(...)` when deciding whether a `2/3/4` overlay row may render transparency at all and whether it may borrow alpha from the viewport color texture
  - this removes another split-brain seam where late-pass execution already respected `RequiresDedicatedOpacityInput` / `AllowsImplicitAlphaFallback`, but the earlier overlay execution path could still quietly fall back to generic color-alpha behavior
  - this is a real `5.1` correctness boundary: explicit makeup/grayscale lanes now use the same shared rule source for early overlay alpha-source selection, transparency gating, pass selection, parity profile, family contract, behavior matrix, pass phase, and coarse overlay policy
- [x] Centralize `SortLayer` bucket semantics behind one shared bucket profile:
  - `BuildOverlaySortLayerBucketProfile(...)` now owns the default lane, pass-bucket, ordinary-worn pass-bucket, and generic detail stack-order semantics for `Default`, `OrderedLow`, `OrderedHigh`, and `HighLayer` rows instead of leaving those numbers split across multiple local switch blocks
  - `ClassifyOverlayCompositorLane(...)`, `BuildOverlayCompositorPolicy(...)`, and `GetViewportGenericDetailStackOrder(...)` now all consume that same bucket profile, so ordered-row routing and stack ordering no longer drift independently
  - this is a real `5.3` boundary: `SortLayer` handling is now less fragmented across overlay lane classification, pass bucketing, and generic detail stack order
- [x] Make generic overlay parity ordering bucket-aware instead of raw-`CompositionMethod`-aware:
  - `BuildViewportParityRuleProfile(...)` now resolves generic overlay `CompositionOrder` through `GetViewportGenericOverlayParityOrder(...)`, so ordinary default/ordered/emissive rows no longer reuse raw `CompositionMethod` as their late-pass parity ordering key
  - `BuildViewportParityBehaviorMatrixEntry(...)` now reuses that same bucket-aware parity order for generic overlay stack-order fallback instead of reconstructing a separate `100 + CompositionOrder` heuristic
  - this is another real `5.3/6.3` boundary: generic overlay ordering is now better aligned across pass-plan tie-breaking, parity ordering, and generic overlay behavior fallback
- [x] Restrict raw `SortLayer` / `CompositionMethod` final sort tie-breakers to the overlay rows that still need them:
  - `BuildViewportRenderPassPlans(...)` now routes its final `SortLayer` and `CompositionMethod` fields through `GetViewportMaterialSortLayerTieBreaker(...)` and `GetViewportMaterialCompositionTieBreaker(...)` instead of always carrying the raw material values into the last sort stages
  - ordinary cosmetic/default late overlay rows no longer quietly inherit raw `SortLayer` / `CompositionMethod` as universal final ordering keys once bucket-aware parity and stack-order decisions have already been made; raw `SortLayer` is now kept mainly for ordered/worn/high-layer rows where it still acts as an intra-bucket tie-break
  - this is another real `5.3/6.3` boundary: final pass-plan ordering is now less dependent on raw material numerics and more aligned with the resolved overlay lane/bucket contract
- [x] Centralize `CompositionMethod 32` worn/high-layer lane semantics behind one shared worn-lane profile:
  - `BuildOverlayWornLaneProfile(...)` now owns the explicit worn/high-layer `32` lane mapping for pass intent, pass variant, late blend intent, blend family, pass bucket, parity order, emissive/specular response, and alpha/emissive fallback rules instead of leaving those decisions split across several late/overlay switch blocks
  - `GetViewportSecondaryPassVariants(...)`, `BuildOverlayCompositorPolicy(...)`, `GetViewportLatePassBlendIntent(...)`, and `GetViewportGenericOverlayParityOrder(...)` now all consume that same worn-lane profile, so ordinary worn and high-layer worn rows no longer re-derive their behavior independently at each stage
  - this is a real `5.2` boundary: the dominant `CompositionMethod 32` worn-slot lane is now materially less approximate and less fragmented across variant selection, coarse overlay policy, late-pass intent, and parity ordering
- [x] Extend the shared worn-lane profile into late-family contract and parity behavior:
  - `BuildViewportLatePassFamilyContract(...)` and `BuildViewportParityBehaviorMatrixEntry(...)` now also consume `BuildOverlayWornLaneProfile(...)`, so ordinary worn and high-layer worn rows no longer fall back to separate local `EmissiveSoft/Restricted` family/stack/material heuristics after the earlier overlay-policy stage has already resolved them
  - this means the dominant `CompositionMethod 32` lane now carries one shared source for pass variant, pass intent, late blend intent, family identity, stack order, material mode, alpha scale, emissive response, and alpha-fallback rules
  - this is another real `5.2` boundary: worn/high-layer `32` behavior is now less fragmented not only in selection/policy but also in late-family contract and parity behavior assembly
- [x] Extend the shared worn-lane profile into scene-level coexistence fallback:
  - `BuildViewportLatePassSceneRelationProfile(...)` now also consumes `BuildOverlayWornLaneProfile(...)` for the no-diffuse-sibling fallback of `EmissiveSoft` and `EmissiveRestricted`, instead of keeping separate local transparency/alpha/emissive attenuation numbers for those two worn/high-layer lanes
  - this pushes the dominant `CompositionMethod 32` lane one step deeper into a single rule source: scene-level coexistence now reuses the same worn/high-layer contract that already drives pass variant, coarse policy, late intent, family contract, and parity behavior
  - this is another real `5.2/6.2` boundary: worn/high-layer `32` coexistence fallback is now less fragmented and less dependent on standalone local emissive-lane heuristics
- [x] Extend the shared worn-lane profile into late-pass phase selection:
  - `BuildOverlayWornLaneProfile(...)` now carries explicit `PassPhase` for ordinary worn and high-layer worn `32` lanes, and `GetViewportParityPassPhase(...)` now consumes that shared phase instead of relying only on the generic local `EmissiveSoft/Restricted => 3/4` switch
  - this removes one more residual split between the shared worn/high-layer contract and the late-pass phase layer, so those lanes now reuse the same rule source for phase as well as variant, policy, family contract, parity behavior, and scene-level coexistence fallback
  - this is another narrow but real `5.2/6.2` boundary: the dominant `CompositionMethod 32` lane is now less fragmented even in late-pass phase selection
- [x] Isolate emissive worn/high-layer coexistence rules behind a dedicated late-family helper:
  - emissive-family base stack order and all `EmissiveSoft` / `EmissiveRestricted` pairwise coexistence rules now flow through `BuildViewportLateEmissiveFamilyProfile(...)` and `BuildViewportEmissiveLatePairwiseRelation(...)` instead of remaining embedded directly inside the large generic late-family pairwise switch
  - this keeps the worn/high-layer `32` coexistence layer easier to evolve independently from generic detail/makeup relations and makes the remaining `6.2` cleanup more targeted
  - this is another narrow but real `5.2/6.2` boundary: emissive worn/high-layer coexistence is now less scattered across the late-family relation layer
- [x] Extend the shared emissive fallback profile into pass-variant, parity-order, family-contract, and behavior-matrix fallback:
  - `BuildViewportLateEmissiveFamilyProfile(...)` now also owns the fallback `PassVariant`, `PassPhase`, `MaterialMode`, `TransparencyMode`, underlay contract, and relation tag for `EmissiveSoft` / `EmissiveRestricted`, instead of leaving those `300/400` emissive semantics split across `GetViewportSecondaryPassVariants(...)`, `GetViewportGenericOverlayParityOrder(...)`, `BuildViewportLatePassFamilyContract(...)`, and `BuildViewportParityBehaviorMatrixEntry(...)`
  - this removes another residual split-brain seam between the dedicated worn/high-layer profile and the generic emissive fallback path: when the material has already resolved to an emissive late family, the browser now reuses one shared emissive fallback source for pass selection, fallback parity order, late-family contract, and fallback parity behavior
  - this is another narrow but real `5.2/6.2` boundary: emissive worn/high-layer fallback behavior is now less fragmented even when execution falls back from the full worn-lane profile to the generic late-emissive family layer
- [x] Extend the shared emissive fallback profile into actual fallback execution behavior:
  - `BuildViewportLateEmissiveFamilyProfile(...)` now also owns fallback `AlphaScale`, emissive-color attenuation, and the `renderAlphaMap` gating contract (`RequiresExplicitOpacityForRenderAlpha`, `DisallowViewportColorAlphaSource`) for `EmissiveSoft` / `EmissiveRestricted`, instead of leaving those execution semantics in local `BuildViewportParityBehaviorMatrixEntry(...)` and `BuildViewportLatePassExecutionPolicy(...)` switch branches
  - this means the generic emissive fallback path now reuses one source not only for routing/order/family identity, but also for actual fallback alpha/emissive behavior and alpha-source acceptance rules
  - this is another narrow but real `5.2/6.2` boundary: emissive worn/high-layer fallback behavior is now less fragmented at execution time, not just at selection and parity-contract time
- [x] Extend the shared worn/emissive lane contract into overlay execution/material response:
  - `BuildOverlayWornLaneProfile(...)` now carries `RequiresDedicatedEmissiveMap`, and `BuildOverlayCompositorPolicy(...)` forwards that into `OverlayCompositorPolicy(...)` instead of leaving high-layer emissive-map behavior as a raw `PassIntent.HighLayer` special-case inside `BuildOverlayMaterialResponsePlan(...)`
  - `TryCreateOverlayExecutionTextureModel(...)` and `BuildOverlayMaterialApplicationPlan(...)` now also use one shared `IsOverlayExecutionPassIntent(...)` helper instead of repeating the same five-pass raw `PassIntent` allow-list in multiple execution/material-assembly switches
  - this is another narrow but real `5.2/6.2` boundary: worn/high-layer execution semantics are now slightly less fragmented because overlay execution acceptance and dedicated-emissive-map behavior both flow through the shared worn/policy contract instead of local pass-intent branches
- [x] Extend the shared emissive fallback profile into scene-level missing-diffuse coexistence fallback:
  - `BuildViewportLateEmissiveFamilyProfile(...)` now also owns the generic `MissingDiffuseTransparencyMode`, `MissingDiffuseAlphaScaleMultiplier`, and `MissingDiffuseEmissiveScaleMultiplier` contract for `EmissiveSoft` / `EmissiveRestricted`, and `BuildViewportLatePassSceneRelationProfile(...)` now resolves those values through `TryApplyViewportLateEmissiveMissingDiffuseFallback(...)` instead of requiring the full worn-lane profile to be present as the only source of truth
  - this removes another residual split between the dedicated worn/high-layer profile and the generic late-emissive family layer: scene-level coexistence fallback now has one shared path for emissive missing-diffuse behavior, with the worn profile overriding only when the more specific lane contract is available
  - this is another narrow but real `5.2/6.2` boundary: emissive worn/high-layer coexistence fallback is now less fragmented even in the scene-level missing-diffuse path
- [x] Remove dead emissive pairwise duplicates from the generic late-family relation switch:
  - after `BuildViewportEmissiveLatePairwiseRelation(...)` became the dedicated source for `EmissiveSoft` / `EmissiveRestricted` coexistence rules, the old duplicate emissive cases inside the generic `BuildViewportLatePassPairwiseRelation(...)` switch were left behind as dead fallback logic
  - the generic late-family switch now keeps only non-emissive relations, while emissive worn/high-layer pairwise ordering lives in one dedicated helper
  - this is a narrow but honest `5.2/6.2` cleanup boundary: emissive coexistence is now less split not only behaviorally, but also structurally at the relation-source level
- [x] Remove dead local emissive defaults from parity behavior assembly:
  - `BuildViewportParityBehaviorMatrixEntry(...)` no longer keeps standalone `EmissiveSoft` / `EmissiveRestricted` entries inside the local `defaultAlphaScale` and `resolvedEmissiveColor` switches once `BuildViewportLateEmissiveFamilyProfile(...)` has already been resolved
  - fallback alpha scale and emissive-color attenuation for late emissive families now flow through the same shared emissive profile instead of surviving as parallel local defaults beside it
  - this is another narrow but honest `5.2/6.2` cleanup boundary: shared emissive fallback semantics are now less fragmented even inside parity-behavior assembly
- [x] Reuse the shared emissive family profile inside the worn-lane profile builder:
  - `BuildOverlayWornLaneProfile(...)` no longer redefines `PassVariant`, `BlendIntent`, `ParityOrder`, `PassPhase`, `MaterialMode`, `TransparencyMode`, `MissingDiffuse*`, and `RelationTag` for ordinary/high-layer `CompositionMethod 32` lanes from scratch once `BuildViewportLateEmissiveFamilyProfile(...)` is already known
  - the worn-lane profile still owns lane-specific pieces such as `PassIntent`, `BlendFamily`, pass-bucket selection, emissive/specular surface response, and explicit-vs-fallback emissive usage, but the shared emissive family layer now owns the common family semantics underneath
  - this is another narrow but real `5.2/6.2` boundary: the dominant `32` worn/high-layer lane is now less fragmented even inside its own profile builder
- [x] Remove raw emissive pass-variant checks from final ordering and generic parity grouping:
  - `GetViewportMaterialCompositionTieBreaker(...)`, `GetViewportMaterialSortLayerTieBreaker(...)`, and the generic overlay branch in `BuildViewportParityRuleProfile(...)` now recognize late emissive rows through `IsViewportLateEmissivePassVariant(...)` instead of hard-coding `WornEmissiveLate` / `HighLayerEmissiveLate`
  - this is a small but useful cleanup seam: final ordering and parity grouping now key off the shared emissive-pass layer rather than repeating explicit late emissive variant lists
  - this is another narrow `5.2/5.3/6.3` boundary: pass-planning tie-breaks are now slightly less coupled to raw variant enumeration for emissive worn/high-layer rows
- [x] Centralize generic overlay/emissive late ordering behind one shared late-ordering profile:
  - `BuildViewportOverlayLateOrderingProfile(...)` now owns parity order plus final `CompositionMethod` and `SortLayer` tie-break semantics for generic overlay late rows and emissive worn/high-layer late rows, instead of leaving `GetViewportGenericOverlayParityOrder(...)`, `GetViewportMaterialCompositionTieBreaker(...)`, and `GetViewportMaterialSortLayerTieBreaker(...)` as three partially overlapping helpers
  - `BuildViewportParityRuleProfile(...)` and the generic fallback branch in `BuildViewportParityBehaviorMatrixEntry(...)` now also consume that same ordering profile, so generic overlay parity grouping, generic fallback stack order, and final render-pass tie-breaks no longer drift independently
  - this is a real `5.3/6.3` boundary: final ordering and generic overlay parity fallback are now more consistently driven by one resolved late-ordering contract instead of a scattered mix of raw numerics and helper-local constants
- [x] Move high-layer overlay alpha gating onto the shared worn/policy contract:
  - `BuildOverlayWornLaneProfile(...)` now carries `RequiresExplicitOpacityForTransparency` and `DisallowViewportColorAlphaFallback`, and `BuildOverlayCompositorPolicy(...)` forwards those into `OverlayCompositorPolicy(...)` instead of leaving high-layer transparency acceptance as a local `RestrictedHighLayer` branch inside `ShouldRenderOverlayPolicyTransparency(...)`
  - `ResolveOverlayAlphaSourceTexture(...)` now also consumes that same policy contract, so early overlay alpha-source selection and transparency gating follow one path for worn/high-layer rows instead of splitting into separate render-alpha and alpha-source heuristics
  - this is another real `5.2/6.2` boundary: high-layer/worn early alpha behavior is now less fragmented because explicit-opacity requirements and viewport-color alpha suppression are driven from the shared lane/policy contract rather than a late local blend-family special-case
- [x] Centralize ordinary detail-late defaults behind one shared generic-detail profile:
  - `BuildViewportGenericDetailLateProfile(...)` now owns stack order, pass phase, alpha scale, emissive response, and render-alpha gating constraints for `DetailMasked`, `DetailSoft`, `DetailDefault`, and `OrderedSoft` instead of leaving those values split across `GetViewportGenericDetailStackOrder(...)`, `GetViewportLatePassDefaultPhase(...)`, `BuildViewportParityBehaviorMatrixEntry(...)`, `GetOverlayDetailLatePassEmissiveColor(...)`, and `BuildViewportLatePassExecutionPolicy(...)`
  - this means the ordinary CAS overlay/detail branch now reuses one source for generic detail stack/default behavior in both late-pass assembly and parity/execution fallback paths, rather than reconstructing the same four-lane semantics in several local switch blocks
  - this is a real `5.1/5.3/6.2` boundary: ordinary detail-late rows are now less fragmented not only in ordering, but also in alpha/emissive/renderAlpha defaults across the browser compositor path
- [x] Centralize ordinary detail blend-intent resolution behind shared helpers:
  - `GetViewportLatePassBlendIntent(...)` no longer keeps a standalone local tuple-switch for ordinary `CosmeticDetailLate` / `DefaultDetailLate` / `OrderedDetailLate`; that mapping now flows through `ResolveViewportOrdinaryDetailBlendIntent(...)`
  - `ShouldRenderOverlayDetailLatePass(...)` and `GetOverlayDetailLatePassEmissiveColor(...)` now also reuse that same ordinary-detail intent layer instead of repeating local `PassIntent` checks and separate detail-late emissive defaults
  - this is another narrow but real `5.1/6.2` boundary: ordinary detail rows are now less split between late-pass blend-intent selection and detail-late material helper behavior
- [x] Replace raw final render-order fields with an explicit final-order profile:
  - `ViewportRenderPassPlan` no longer carries separate bare `SortLayer` and `CompositionMethod` fields just to feed the final `.ThenBy(...)` chain; instead it now carries `ViewportRenderPassFinalOrderProfile`, built through `BuildViewportRenderPassFinalOrderProfile(...)`
  - this keeps the final render-pass sort aligned with the existing ordering helpers while making the last ordering layer explicit and easier to evolve beyond raw numerics
  - this is another honest `5.3/6.3` boundary: final ordering is now slightly less “plan + two leftover material numbers” and slightly more explicit compositor contract
- [x] Make the final-order profile the only source of final material tie-breaks:
  - `BuildViewportRenderPassFinalOrderProfile(...)` now resolves overlay late ordering directly from `BuildViewportOverlayLateOrderingProfile(...)` and otherwise falls back to raw material numerics itself; it no longer wraps separate `GetViewportMaterialSortLayerTieBreaker(...)` / `GetViewportMaterialCompositionTieBreaker(...)` helpers
  - this removes one more “profile on top of wrappers on top of raw numerics” layer from final ordering and leaves the final material-order contract in one place
  - this is another honest `5.3/6.3` cleanup boundary: the last render-order layer is now more explicit and less mechanically layered
- [x] Extract late-pass render-stage resolution into its own seam:
  - `GetViewportMaterialRenderStage(...)` now resolves late overlay/skintone stages through `ResolveViewportLatePassRenderStage(...)` before falling back to raw `PreviewCompositorStage`, so late-pass stage routing is no longer mixed directly into the general material-stage fallback path
  - behavior is intentionally preserved for now, but this creates a proper seam for the next pass over overlay/high-layer stage ordering instead of forcing those changes into the broad generic render-stage helper
  - this is a narrow but useful `4.3/4.4/6.3` preparation boundary: late-pass stage resolution is now more explicit and easier to evolve independently from primary/base stage routing
- [x] Make late-pass render-stage routing family-aware for overlay/high-layer rows:
  - `ResolveViewportLatePassRenderStage(...)` now resolves through `BuildViewportLatePassStageProfile(...)` instead of bouncing directly from any recognized late pass back to raw `PreviewCompositorStage`
  - `BuildViewportLatePassStageProfile(...)` gives `SkintoneLate`, ordinary overlay late families, and `EmissiveRestricted` / high-layer late families explicit stage routing (`sim-skintone-overlay`, `cas-overlay-detail`, `cas-overlay-highlayer-detail`) from late family / pass semantics, with raw stage fallback only as the last resort
  - this is a real `4.4/6.3` step: high-layer late rows are now less dependent on raw stage strings for stage ordering once the browser has already resolved them into explicit late families
- [x] Centralize primary/base alpha authority and base/high-layer surface response behind explicit primary-family contracts:
  - primary/base `renderAlphaMap`, alpha-source selection, and primary alpha texture assembly now flow through `BuildViewportPrimaryAlphaProfile(...)` and `ResolveViewportPrimaryAlphaSourceTexture(...)` instead of splitting between `ShouldRenderViewportPrimaryTransparency(...)`, `CreateViewportPrimarySourceAlphaTextureModel(...)`, raw `opacity ?? viewportColor alpha` fallback, and the standalone `ShouldRenderHighLayerBaseTransparency(...)` helper
  - `ViewportPrimaryPassContract(...)` now also carries `DisallowViewportColorAlphaFallback`, so explicit-opacity primary families (`SkintoneBase`, `OverlayBase`, `HighLayerOverlayBase`) can suppress generic viewport-color alpha fallback from one contract layer rather than reintroducing that rule later in alpha assembly
  - lit primary/base ambient/specular response for `SkintoneBase`, `OverlayBase`, `HighLayerOverlayBase`, and helper families now flows through `BuildViewportPrimarySurfaceResponseProfile(...)` instead of staying distributed across local `isSkintoneBase/isOverlayBase/isHighLayerBase/helper` branches inside `BuildViewportPrimaryPassResponsePlan(...)`
  - this is a real `4.3/4.4/6.1` boundary: base/high-layer primary behavior is now less fragmented because primary-family authority covers alpha acceptance, alpha-source resolution, and base-surface response together instead of leaving those three seams partially disconnected
- [x] Centralize strongest primary-family identity and stage-local texture composite rules behind shared primary-family profiles:
  - `BuildViewportPrimaryFamilyProfile(...)` and `BuildViewportPrimaryFamilyProfileFromStage(...)` now own the strongest primary-family mapping for `SkintoneBase`, `OverlayBase`, `HighLayerOverlayBase`, and helper families, so `stage -> family`, `family -> pass variant`, and `pass variant -> contract` no longer duplicate the same strongest-primary identity across separate switch layers
  - `BuildViewportPrimaryPassContract(...)` now reuses that shared primary-family profile instead of redefining the same stack/render/intent/opacity/material-response constants a second time
  - `CreateViewportTextureModel(...)` now resolves stage-local skintone/swatch composite behavior through `BuildViewportPrimaryTextureCompositeProfile(...)` and `ViewportPrimaryTextureCompositeProfile(...)` instead of mixing one explicit primary-family composite branch with a second raw `ApproximateCas` swatch branch
  - this is another real `4.3/6.1` boundary: strongest primary families are now less fragmented not only in alpha/response assembly, but also in identity resolution and stage-local texture composite behavior
- [x] Centralize stage-local primary material behavior behind a shared primary behavior profile:
  - `CreateMaterial(...)` now resolves `forceOpaqueViewportTexture`, matte-vs-default lit response, and default lit specular/shininess through `BuildViewportPrimaryMaterialBehaviorProfile(...)` instead of keeping those strongest-primary defaults as a local mix of `isHighLayerDetail`, `hasNormal/specular`, and broad fallback booleans
  - `BuildViewportPrimaryPassApplicationPlan(...)` now also consumes that same `ViewportPrimaryMaterialBehaviorProfile(...)` for `RenderShadowMap` instead of re-deriving helper-family shadow suppression locally
  - this is another real `4.3/6.1` boundary: strongest primary families now reuse one material-behavior layer for opaque texture forcing, lit ambient/specular defaults, and shadow eligibility instead of reconstructing those decisions across `CreateMaterial(...)` and primary application assembly
- [x] Centralize strongest primary execution semantics behind a shared primary execution profile:
  - `BuildViewportPrimaryPassExecutionPlan(...)` no longer keeps six near-duplicate branches for `SkintoneCarrier`, `OverlayCarrier`, `HighLayerCarrier`, and helper carriers; those families now resolve through `BuildViewportPrimaryExecutionProfile(...)`
  - `ViewportPrimaryExecutionProfile(...)` now owns the primary execution-layer distinctions for alpha-map acceptance, normal/specular execution, auto-tangent eligibility, and execution rule tagging, instead of leaving those semantics embedded directly inside the large primary execution switch
  - this is another real `4.3/6.1` boundary: strongest primary families are now less fragmented in execution assembly, so the primary path differs more through explicit execution contracts and less through repeated branch-local construction of the same execution plan
- [x] Centralize stage-local primary/overlay interaction and viewport-color routing behind a shared interaction profile:
  - `CreateMaterial(...)` no longer reconstructs helper-inspection, `cas-overlay-detail`, `cas-overlay-highlayer-detail`, and `sim-skintone-overlay` interaction semantics as a scattered local boolean cluster; those stage-local decisions now resolve through `BuildViewportStageInteractionProfile(...)` and `ViewportStageInteractionProfile(...)`
  - stage-local activation of `BuildOverlayExecutionPlan(...)` and `BuildSkintoneOverlayExecutionPlan(...)`, helper inspection gating, `renderEmissiveMap`, `renderAlphaMap`, and generic viewport-color routing now flow through one interaction layer instead of being split across multiple local branches inside `CreateMaterial(...)`
  - `BuildViewportPrimaryMaterialBehaviorProfile(...)` now also consumes that same shared interaction profile for opaque-texture forcing and high-layer matte-lit behavior instead of taking separate `isCasOverlayDetail/isSimSkintoneOverlay/isCasOverlayHighLayerDetail` booleans
  - this is another real `4.3/4.4/6.1` boundary: strongest primary/base families are now less fragmented not only in contract/response/execution assembly, but also in the stage-local interaction layer that decides when overlay/skintone execution paths participate in the primary material path at all
- [x] Reuse one helper-inspection texture selection seam across material preview and UV preview:
  - helper inspection texture selection for `HelperProjective`, `HelperLayered`, and `HelperUtility` no longer lives as two separate branch clusters in `CreateMaterial(...)` and `SelectViewportUvTexture(...)`; both paths now resolve through `SelectViewportHelperInspectionTexture(...)`
  - that shared helper preserves selected-slot priority while keeping helper-family-specific inspection routing in one place, instead of letting material-preview and UV-preview helper selection drift independently
  - this is a smaller but still real `4.5/6.1` cleanup boundary: helper/projective inspection behavior is now less fragmented across preview modes, which makes the eventual helper-stage redesign less likely to reintroduce split helper-selection logic
- [x] Centralize render-pass stage/bucket/phase selection behind an explicit render-pass ordering profile:
  - `BuildViewportRenderPassPlans(...)` no longer stitches `RenderStage`, `PassBucket`, and `PassPhase` together from three separate helper calls at the callsite; those ordering inputs now resolve through `BuildViewportRenderPassOrderingProfile(...)` and `ViewportRenderPassOrderingProfile(...)`
  - this keeps render-stage routing, pass-bucket classification, and pass-phase selection behavior-preserving for now, but makes the remaining `5.3/6.3` ordering work more explicit by turning pass-order assembly itself into a shared contract layer rather than another local tuple of independent helper outputs
  - this is another real `5.3/6.3` cleanup boundary: the browser's final pass-plan ordering now depends less on scattered stage/bucket/phase reads and more on one render-pass ordering seam that can evolve independently of the rest of `BuildViewportRenderPassPlans(...)`
- [x] Centralize raw overlay/skintone stage-family detection behind a shared overlay stage profile:
  - `BuildViewportOverlayStageProfile(...)` and `ViewportOverlayStageProfile(...)` now own the raw `PreviewCompositorStage` mapping for `cas-overlay-detail`, `cas-overlay-highlayer-detail`, `sim-skintone-overlay`, and broader overlay-pass detection, instead of leaving that family detection duplicated across `IsCasOverlayDetailStage(...)`, `IsCasOverlayHighLayerDetailStage(...)`, `IsSimSkintoneOverlayStage(...)`, and their downstream callsites
  - `GetViewportSecondaryPassVariants(...)`, `BuildViewportLatePassStageProfile(...)`, `BuildViewportStageInteractionProfile(...)`, `BuildSkintoneOverlayExecutionPlan(...)`, `BuildSkintoneLatePassPlan(...)`, and ordinary detail/skintone late blend-intent helpers now reuse that one stage-family seam instead of each rediscovering the same overlay/skintone stage identity from raw stage strings
  - this is another real `4.3/4.4/6.1/6.3` boundary: overlay/high-layer/skintone stage identity is now less fragmented across the late/base bridge, which makes the eventual preview-stage split less likely to reintroduce duplicated stage-family detection in both primary and late paths
- [x] Centralize render-pass participation behind an explicit participation profile:
  - `BuildViewportRenderPassPlans(...)` no longer assembles inclusion, transparency, and wireframe behavior directly from separate `ShouldIncludeViewportPrimaryPass(...)`, `ShouldIncludeViewportSecondaryPass(...)`, `IsViewportPrimaryPassTransparent(...)`, and `IsViewportLatePassTransparent(...)` calls at the callsite; those pass-participation decisions now flow through `BuildViewportRenderPassParticipationProfile(...)` and `ViewportRenderPassParticipationProfile(...)`
  - this keeps actual participation behavior unchanged for now, but turns pass admission itself into a shared render-plan seam rather than another local tuple of booleans glued together inside the final LINQ pipeline
  - this is another real `5.3/6.3` cleanup boundary: final pass-plan participation now depends less on scattered include/transparency helper calls and more on one participation layer that can evolve independently when the remaining runtime-faithful ordering/coexistence work is tackled
- [x] Centralize late-pass source selection and material binding behind shared late-pass source/binding profiles:
  - `BuildViewportLatePassMaterialPlan(...)` no longer splits late material-path assembly across separate detail, emissive, and skintone plan builders; those rows now resolve through `BuildViewportLatePassSourceProfile(...)`, `BuildViewportLatePassOverlayEmissiveSourceProfile(...)`, `BuildViewportLatePassOverlayDetailSourceProfile(...)`, and `BuildViewportLatePassSkintoneSourceProfile(...)`, which carry late source texture selection, alpha-source evidence, dedicated-opacity authority, and emissive response through one late material-source seam
  - `BuildViewportLatePassMaterial(...)` no longer keeps a repeated `PhongMaterial` map-binding switch beside that source assembly; diffuse-vs-emissive map binding now resolves through `BuildViewportLatePassMaterialBindingProfile(...)`, so late material binding is no longer a separate local branch cluster after the late source contract has already been established
  - this is another real `4.3/6.1/6.3` boundary: late material-path assembly for overlay detail, worn/high-layer emissive rows, and skintone late rows is now less fragmented and closer to an explicit preview-stage/material-path split instead of three partially independent late-material branches
- [x] Centralize late parity/execution alpha gating behind one shared parity behavior contract:
  - `BuildViewportParityBehaviorMatrixEntry(...)` now carries not only stack/material/alpha defaults, but also `RequiresExplicitOpacityForRenderAlpha` and `DisallowViewportColorAlphaSource`, so late alpha gating rules for composition-driven rows, ordinary detail rows, and emissive worn/high-layer rows now travel with the same parity behavior contract that already owns their phase/order/material semantics
  - `BuildViewportLatePassExecutionPolicy(...)` no longer re-runs a second composition/emissive/detail classification pass just to decide `RenderAlphaMap`; it now resolves late alpha acceptance from the shared parity behavior contract plus explicit-opacity / viewport-color evidence
  - this is another real `5.3/6.3` boundary: late ordering/coexistence and late alpha execution are now slightly less split because render-alpha gating no longer depends on a second scattered family of branch-local rule checks after parity behavior has already been resolved
- [x] Split stage-local render routing and generic fallback material binding behind explicit stage/fallback plans:
  - `CreateMaterial(...)` no longer keeps stage-local `renderDiffuseMap`, `renderEmissiveMap`, and `renderAlphaMap` routing as a local boolean cluster beside the final generic `PhongMaterial` fallback; those routing decisions now resolve through `BuildViewportStageRenderRoutingProfile(...)`
  - the remaining non-primary/non-late `PhongMaterial` assembly path now resolves through `BuildViewportFallbackMaterialPlan(...)` and `BuildViewportFallbackMaterial(...)`, so stage-local fallback material binding for overlay/skintone/helper/generic rows is no longer embedded as one large object initializer inside `CreateMaterial(...)`
  - this is another real `4.3/6.1` boundary: strongest families and stage-local fallback rows are now closer to an explicit material-path split because stage routing and fallback material binding have become first-class seams instead of the last large in-method fallback cluster
- [x] Iteration target: close the real preview-stage/material-path split for compositor-facing families instead of routing them back into one `PhongMaterial` approximation:
  - [x] Pass 1. Skintone-carried late split:
    - `SkintoneLate` no longer terminates in the same generic late material builder as ordinary overlay/detail rows; late skintone rows now resolve through a dedicated `ViewportLatePassMaterialPathProfile(SkintoneOverlay)` path and `BuildViewportSkintoneLatePassMaterial(...)`
    - skintone late source selection, alpha authority, material-path selection, and final map binding now terminate in a skintone-specific late material builder instead of rejoining the ordinary late fallback
    - completion criterion met: `SkintoneLate` no longer shares the generic fallback late material builder with ordinary overlay/detail rows
  - [x] Pass 2. Ordinary CAS overlay/detail split:
    - ordinary overlay/detail late families now resolve through explicit `ViewportLatePassMaterialPathProfile(...)` lanes and dedicated builders: `BuildViewportOrdinaryDiffuseLatePassMaterial(...)`, `BuildViewportGrayscaleLatePassMaterial(...)`, and `BuildViewportSoftEmissiveLatePassMaterial(...)`
    - `CosmeticDetailLate` / `DefaultDetailLate` / `OrderedDetailLate` no longer collapse back into one generic late `PhongMaterial` initializer after routing
    - completion criterion met: ordinary overlay/detail late families now terminate in dedicated late material builders keyed by resolved late material path rather than the old generic late material builder
  - [x] Pass 3. High-layer late split:
    - high-layer `32|65536` late rows no longer terminate in the same generic emissive late builder as ordinary soft emissive rows; they now resolve through `ViewportLatePassMaterialPathProfile(HighLayerEmissiveOverlay)` and `BuildViewportHighLayerEmissiveLatePassMaterial(...)`
    - high-layer late rows now keep their dedicated high-layer render stage and a dedicated high-layer terminal material path end-to-end instead of only differing by contracts inside a shared late material builder
    - completion criterion met: high-layer late rows no longer collapse back into the same generic late emissive material builder as ordinary overlay/detail rows
  - [x] Pass 4. Helper/projective split:
    - helper/projective/layered/utility inspection rows now resolve through `BuildViewportFallbackMaterialPathProfile(...)` and `ViewportFallbackMaterialPathKind.HelperInspection`, then terminate in `BuildViewportHelperInspectionFallbackMaterial(...)` instead of reusing the generic fallback material builder directly
    - completion criterion met: helper families no longer reach the generic fallback material builder except through a dedicated helper material path
  - [x] Pass 5. Final collapse and deletion:
    - obsolete late reunification for emissive rows has been removed: soft emissive and high-layer emissive lanes no longer pass through one generic late emissive terminal path after routing
    - fallback planning now resolves through explicit `BuildViewportHelperInspectionFallbackMaterialPlan(...)`, `BuildViewportSkintoneFallbackMaterialPlan(...)`, `BuildViewportOverlayFallbackMaterialPlan(...)`, and `BuildViewportGenericFallbackMaterialPlan(...)`, so compositor-facing fallback rows no longer immediately reunify in one plan-selection branch after path classification
    - completion criterion met: compositor-facing family splits are now structural at the terminal preview/material-path layer, not just profile-driven inside one fallback material path
- [x] Centralize the remaining late-side coexistence and parity behavior seams behind explicit profiles:
  - scene-level late coexistence now resolves through `BuildViewportLatePassCoexistenceProfile(...)` and `BuildViewportLatePassFamilySceneAdjustmentProfile(...)` instead of mixing family-local multipliers and pairwise offsets directly inside `BuildViewportLatePassSceneRelationProfile(...)`
  - parity behavior matrix assembly now routes through specialized composition, worn-lane, emissive, and fallback builders instead of one large mixed branch
  - this is a real `5.3/6.1/6.2` boundary: the remaining runtime-faithful ordering/coexistence numerics are now localized behind explicit profile seams rather than scattered across render-plan assembly
- [x] Collapse late pairwise ordering/coexistence numerics into reusable profile builders:
  - pairwise late-family interaction now aggregates through `BuildViewportLatePassPairwiseCoexistenceProfile(...)` instead of recomputing stack/alpha/emissive totals directly at the scene-relation callsite
  - ordinary late-family relations and emissive late-family relations are now split into dedicated builders before aggregation, so the remaining runtime ordering table is no longer one monolithic mixed switch
  - fallback parity behavior now resolves through `BuildViewportFallbackParityBehaviorProfile(...)`, which keeps default alpha/emissive/order semantics out of the mixed parity matrix assembly path
- [x] Collapse generic late fallback ordering/defaults into explicit profiles:
  - generic/skintone late fallback ordering now resolves through `BuildViewportLateFallbackOrderingProfile(...)` instead of living separately inside `BuildViewportOverlayLateOrderingProfile(...)`
  - generic/skintone late fallback alpha/emissive/order defaults now resolve through `BuildViewportLateFallbackBehaviorDefaultsProfile(...)` instead of keeping `105/110`, `0.72/0.88`, and related defaults in scattered parity fallback switches
  - this moves another real `5.3/6.1/6.2` seam into reusable truth-layer profiles rather than leaving it as raw numeric glue
- [x] Unify generic/skintone late fallback phase/order/behavior defaults under one shared defaults profile:
  - `BuildViewportLateFallbackDefaultsProfile(...)` now feeds fallback `PassPhase`, stack order, alpha scale, emissive color, and tie-break metadata for skintone and generic late lanes
  - `BuildViewportOverlayLateOrderingProfile(...)`, `GetViewportLatePassDefaultPhase(...)`, and `BuildViewportFallbackParityBehaviorProfile(...)` no longer keep separate local fallback constants for the same late families
  - this is another real `5.3/6.1/6.2` boundary: the remaining late fallback numerics are more centralized and easier to replace with source-backed runtime rules
- [x] Extend the shared late fallback defaults profile into late-family contracts:
  - `BuildViewportLatePassFamilyContract(...)` now uses `BuildViewportLateFallbackDefaultsProfile(...)` for skintone/generic late lanes instead of keeping separate direct family/underlay/transparency constants beside the fallback defaults
  - skintone and generic late fallback families now share one truth source for family identity, underlay requirement, transparency mode, stack order, phase, and fallback behavior metadata
  - this removes another direct-constant seam from the runtime truth layer and leaves fewer remaining places where late-family behavior can drift
- [x] Lift `CompositionMethod` and `SortLayer bucket` defaults into named runtime-default profiles:
  - `BuildViewportCompositionRuleProfile(...)` now resolves through `BuildViewportCompositionLaneDefaultsProfile(...)`, so the `2/3/4` late-lane constants are no longer embedded directly in the rule builder
  - `BuildOverlaySortLayerBucketProfile(...)` now resolves through `BuildOverlaySortLayerBucketRuntimeDefaultsProfile(...)`, which isolates the bucket pass/stack defaults from downstream consumers
  - this is another real `5.1/5.3/6.2` boundary: the remaining runtime math gap is less about hidden constants and more about missing source-backed rules
- [x] Centralize parity-rule and composition-family contract defaults into explicit profiles:
  - `BuildViewportParityRuleProfile(...)` now resolves primary, skintone-late, and generic overlay parity defaults through `BuildViewportParityRuleDefaultsProfile(...)` instead of keeping those fallback constructors inline beside the truth layer
  - `BuildViewportLatePassFamilyContract(...)` now resolves composition-family underlay/relation defaults through `BuildViewportCompositionLateFamilyContractDefaultsProfile(...)` instead of embedding those defaults directly in the contract builder
  - this is another real `5.1/6.2` boundary: primary/skintone/generic parity defaults and composition-family underlay/relation defaults are now explicit profile data instead of scattered inline constructors
- [x] Centralize late pairwise relation numerics and ordinary detail defaults into named profiles:
  - `BuildViewportOrdinaryLatePairwiseRelation(...)` and `BuildViewportEmissiveLatePairwiseRelation(...)` now resolve through dedicated defaults builders instead of carrying their stack/alpha/emissive constants inline in the relation wrappers
  - `BuildViewportGenericDetailLateProfile(...)` now resolves `DetailMasked`, `DetailSoft`, `DetailDefault`, and `OrderedSoft` defaults through `BuildViewportGenericDetailLateDefaultsProfile(...)` instead of keeping those stack/phase/alpha/emissive constants inline in the detail builder
  - this is another real `5.3/6.2` boundary: late coexistence math and ordinary detail defaults are more restart-safe and more isolated from downstream render/parity assembly
- [x] Centralize late emissive-family defaults and generic overlay fallback parity defaults into named profiles:
  - `BuildViewportLateEmissiveFamilyProfile(...)` now resolves `EmissiveSoft` and `EmissiveRestricted` through `BuildViewportLateEmissiveFamilyDefaultsProfile(...)` instead of carrying that whole alpha/emissive/transparency/missing-diffuse contract inline
  - `BuildViewportLateFallbackBehaviorDefaultsProfile(...)` now resolves the final generic-overlay fallback through `BuildViewportGenericOverlayFallbackParityDefaultsProfile(...)` instead of keeping the remaining generic parity defaults inline in the last fallback constructor
  - this is another real `5.2/6.2` boundary: worn/high-layer late emissive semantics and generic overlay fallback parity behavior are less scattered and easier to replace with source-backed runtime rules
- [x] Centralize late-family base stack defaults and late fallback seed profiles:
  - `GetViewportLatePassFamilyBaseStackOrder(...)` now resolves ordinary late-family base orders through `BuildViewportLateFamilyBaseDefaultsProfile(...)` instead of keeping those remaining `10/100/120/130/140` values inline in the family helper
  - `BuildViewportLateFallbackDefaultsProfile(...)` now routes skintone and generic-detail seed construction through `BuildViewportSkintoneLateFallbackDefaultsProfile(...)` and `BuildViewportGenericDetailLateFallbackDefaultsProfile(...)` instead of mixing those seed constants directly into the main fallback builder
  - this is another real `6.1/6.2` boundary: late-family base/fallback seeds are now more profile-driven, leaving fewer source-less constants in the runtime truth layer
- [x] Centralize overlay late ordering assembly behind explicit defaults:
  - `BuildViewportOverlayLateOrderingProfile(...)` now resolves worn, emissive, and fallback late-ordering paths through `BuildViewportOverlayLateOrderingDefaultsProfile(...)` instead of assembling those branches directly inside the final ordering builder
  - this is another real `5.3/6.2` boundary: late-ordering truth is more isolated from downstream parity/final-order consumers, leaving fewer mixed numeric branches in the ordering layer
- [x] Centralize remaining bucket thresholds and generic overlay fallback ordering defaults:
  - `IsHighLayerComposition32WornOverlayLane(...)` and `ClassifyOverlaySortLayerBucket(...)` now read their `10000/65536` bucket thresholds through `BuildOverlaySortLayerThresholdsProfile(...)` instead of keeping those cut points inline in the helpers
  - `BuildViewportLateFallbackBehaviorDefaultsProfile(...)` now resolves the last generic overlay fallback stack-order seed through `BuildViewportGenericOverlayFallbackOrderingDefaultsProfile(...)` instead of carrying that ordering fallback inline beside parity assembly
  - this is another real `5.3/6.3` boundary: the remaining late ordering scaffolding is more explicit, so the open gap is increasingly the missing TS4 runtime truth rather than hidden fallback constants
- [x] Centralize compositor-stage, slot-category, and broad material fallback stage ordering:
  - `GetViewportCompositorStageOrder(...)`, `GetViewportCasSlotCategoryOrder(...)`, and the broad fallback branch inside `GetViewportMaterialRenderStage(...)` now resolve through explicit stage-order profiles instead of keeping those stage numerics inline in the helper layer
  - this is another real `4.3/5.3` cleanup boundary: stage-order truth is more isolated and restart-safe, which makes the remaining gap look more like missing runtime semantics than hidden browser-side constants
- [x] Collapse late pairwise relation defaults into one shared truth source:
  - `BuildViewportLatePassPairwiseRelation(...)` now resolves all ordinary and emissive coexistence numerics through `BuildViewportLatePairwiseRelationDefaultsProfile(...)` instead of keeping two parallel defaults tables for the same late-scene relation layer
  - this is another real `6.2/6.3` cleanup boundary: late coexistence math is more centralized, so the remaining open problem is more directly the approximation values themselves rather than split truth sources
- [x] Centralize late stack-contract defaults behind one explicit stack-defaults profile:
  - `BuildViewportLatePassStackContract(...)` now resolves skintone, makeup, grayscale, emissive-only, and generic overlay stack constants through `BuildViewportLatePassStackDefaultsProfile(...)` instead of carrying those alpha/emissive/fallback values inline inside the final stack-contract builder
  - this is another real `6.3` cleanup boundary: the remaining gap is even more clearly the approximation values themselves, not hidden browser-side stack-contract constructors
- [x] Centralize family-local late scene adjustments behind explicit defaults:
  - `BuildViewportLatePassFamilySceneAdjustmentProfile(...)` now resolves ordinary family-local alpha/emissive adjustments through `BuildViewportLateFamilySceneAdjustmentDefaultsProfile(...)` instead of keeping that adjustment switch inline beside the pairwise coexistence layer
  - this is another real `6.3` cleanup boundary: late coexistence behavior is now more cleanly split into family-local defaults plus pairwise defaults, leaving the remaining open problem closer to the approximation values themselves
- [x] Resolve final render-pass ordering/participation through one explicit execution contract:
  - `BuildViewportRenderPassPlans(...)` now resolves pass participation, ordering, parity tie-breaks, and final-order tie-breaks through `BuildViewportResolvedRenderPassContract(...)` instead of reassembling those truth sources piecemeal on the render-plan callsite
  - this is another real `5.3/6.3` execution boundary: the final execution path now drifts less from the centralized truth layer, so the remaining open problem is even more clearly the approximation values themselves rather than browser-side recomposition of pass-order state
- [x] Extract stabilized viewport render-pass/compositor planning into a dedicated partial file:
  - the render-pass planning, late-family ordering, parity, and stage-order layer now lives in `src/Sims4ResourceExplorer.App/MainWindow.ViewportRenderPasses.cs` instead of remaining embedded inside `MainWindow.xaml.cs`
  - this is a structure-only step, but it makes the remaining functional work and later file-split passes safer because the compositor truth layer is now easier to navigate and isolate
- [x] Extract the broader viewport material/texture/compositor factory layer into a dedicated partial file:
  - geometry creation, material assembly, overlay policy/execution planning, late-pass material planning, and the associated viewport records/enums now live in `src/Sims4ResourceExplorer.App/MainWindow.ViewportMaterials.cs` instead of remaining inside `MainWindow.xaml.cs`
  - this is another structure-only step, but it sharply reduces the size of the main window file and makes the remaining browser-preview debt easier to inspect in isolation before the build review pass
- [x] Prepare the next user-facing verification release build:
  - incremented `BuildNumber` in `src/Sims4ResourceExplorer.App/Sims4ResourceExplorer.App.csproj` from `0178` to `0179` because the next step is explicit manual release testing
  - the release handoff for this packet must name the exact build id and one exact launch command, per repo protocol
- [x] Publish the manual-test release artifact for build `0179`:
  - published the app with `dotnet publish ... -c Release -p:Platform=x64 -p:PublishProfile=win-x64`
  - verified that both the publish exe and the standard `run.ps1` release exe report `ProductVersion = build-0179`
  - the exact manual-test rerun command for this already-built packet is `.\run.ps1 -Configuration Release -NoBuild`
- [x] Fix the UV preview bitmap/wireframe sampling mismatch and roll a fresh verification build:
  - `BlitScaledBgra(...)` now maps scaled destination pixels onto the same inclusive endpoint grid used by `DrawUvWireframe(...)`, which removes the small top-left bias that could make the UV overlay appear shifted right/down relative to the bitmap
  - incremented `BuildNumber` again from `0179` to `0180` because this is another user-facing visual verification packet and must ship as a distinct runnable build
- [ ] Replace the initial mesh-order approximation with true `CompositionMethod` / `SortLayer` compositor staging and blend behavior for CAS overlay/detail families.
- [ ] Revisit `Projective`, `ShaderDayNightParameters`, reveal/lightmap, and related helper families so selected-slot inspection stays available without promoting those helpers into ordinary surface authority.

#### Restart Hints

- The current implementation seam is centered in:
  - `src/Sims4ResourceExplorer.App/MainWindow.xaml.cs`
  - `src/Sims4ResourceExplorer.App/MainWindow.ViewportRenderPasses.cs`
  - `src/Sims4ResourceExplorer.App/MainWindow.ViewportMaterials.cs`
  - `src/Sims4ResourceExplorer.Preview/BuildBuySceneBuildService.cs`
  - `src/Sims4ResourceExplorer.Preview/BuildBuySceneBuildService.Cas.cs`
  - `src/Sims4ResourceExplorer.Core/Domain.cs`
- The current viewport model is now stronger at:
  - preserving canonical material provenance during scoping
  - honoring slot-local sampling
  - consuming one resolved viewport UV contract for both `3D` and `MaterialUv` paths instead of letting each path reinterpret channel/transform independently
  - filtering utility/helper inputs out of general surface-color selection
  - keeping transparency classification family-aware
  - carrying `CASPart` compositor metadata into scene materials and using it for initial mesh ordering
  - carrying explicit preview compositor stages across decode, CAS, and Sim routing
  - suppressing helper stages from normal preview while keeping them available for inspection modes
  - approximating `sim-skintone-base` as a stage-local texture composite instead of a plain tint multiplier
  - treating `cas-overlay` as overlay-first color/alpha routing instead of plain diffuse fallback
  - isolating the stabilized render-pass/compositor planning layer in `MainWindow.ViewportRenderPasses.cs` so future work no longer has to search one monolithic window file for every late-pass/order rule
  - isolating the broader viewport material/texture/compositor factory layer in `MainWindow.ViewportMaterials.cs`, leaving `MainWindow.xaml.cs` much closer to actual window/UI glue
- Functional debt intentionally deferred until visual build review now lives in:
  - `docs/planning/technical-debt.md`
  - expanding eligible routed skintone materials into separate base and overlay passes instead of one merged preview material
  - expanding eligible `cas-overlay` materials into separate base and detail passes before viewport rendering
  - applying the first `CompositionMethod`-aware viewport behavior for `cas-overlay-detail` without overclaiming exact TS4 math
  - keeping the dominant `32|65536` clothing/accessory lane out of the overly-cosmetic generic overlay fallback path
  - grouping `SortLayer` into overlay pass buckets instead of relying only on one flat numeric sort
  - expressing the dominant high worn-slot overlay lane as explicit `highlayer` stages in scene IR
  - giving `cas-overlay-highlayer-detail` a separate material-application policy instead of only a different stage order
- The current honest boundary is still:
  - no full skintone/compositor parity
  - no true `CompositionMethod` / `SortLayer` blend math yet; current `2/3/4/32` handling is an explicit approximation
  - current `SortLayer` handling is bucketed ordering, not exact runtime compositor pass math
  - compositor-facing family split is now structural in the browser preview path, but the browser still approximates TS4 runtime behavior through separated `PhongMaterial`-backed lanes rather than true compositor-native preview stages
  - preview consumption is now much less heuristic, but preview-side truth still depends on earlier decode/state choices such as `MTST` default-state selection, UV decode-mode heuristics, and any still-unused mesh `ScaleOffsetReference` data
- The most valuable next implementation move is:
  - capture the remaining source-backed runtime ordering/coexistence rules for the separated late families instead of widening architectural family routing again
- Use these docs before widening the next implementation packet:
  - `docs/workflows/material-pipeline/overlay-detail-family-authority-table.md`
  - `docs/workflows/material-pipeline/skintone-and-overlay-compositor.md`
  - `docs/workflows/material-pipeline/family-sheets/shader-daynight-parameters.md`
  - `docs/workflows/material-pipeline/family-sheets/projection-reveal-lightmap.md`

### Current Request Addendum (`2026-04-22`, browser-facing pass filtering contract)

#### Problem

The RenderDoc handoff already improved scene/pass truth, but the main catalog still lacked one browser-facing rule set that said how package-side candidates should actually be filtered against that new pass evidence.

#### Chosen Approach

- turn the external GPU pass-family layer into a concrete package-matching contract
- bind that contract into the shared guide and both major authority matrices
- treat this as a TЗ-layer improvement, not only as another research note

#### Actions

- [x] Add `docs/workflows/material-pipeline/package-material-pass-filtering-contract.md`.
- [x] Rebind the contract into:
  - `shared-ts4-material-texture-pipeline.md`
  - `buildbuy-material-authority-matrix.md`
  - `cas-sim-material-authority-matrix.md`
  - `documentation-status-catalog.md`
  - `current-plan.md`

#### Restart Hints

- Safe strengthened reading to preserve:
  - the browser now has a written first-pass filtering contract for package-material matching
  - `SceneDomain` and `PassClass` are now explicit hard filters, not just good ideas
  - `CompositorOrUi` and `DepthOnly` are now explicitly excluded from normal final visible-material ownership
  - the next implementation-facing move is to mirror this contract in matching logic and diagnostics

### Current Request Addendum (`2026-04-22`, RenderDoc handoff ingestion)

#### Problem

The research track already had stronger package-side carrier logic and stronger live DX11 runtime-interface logic, but scene/pass truth was still the weakest side of the global bridge. A new RenderDoc handoff now supplies external GPU-capture evidence that could strengthen that side immediately.

#### Chosen Approach

- treat the RenderDoc handoff as a new external scene/pass baseline, not as a replacement for runtime or package evidence
- bind it into the bridge docs and queue as positive/negative pass-family evidence
- avoid overclaiming exact hash closure, because RenderDoc replay hashes still do not match the live DX11 catalog identity space

#### Actions

- [x] Read `satellites/ts4-dx11-introspection/docs/ts4-renderdoc-handoff.md` and the checked-in capture summaries.
- [x] Add `docs/workflows/material-pipeline/external-gpu-scene-pass-baseline.md`.
- [x] Rebind the new scene/pass layer into the runtime baseline, bridge boundary, queue, status catalog, and current plan.

#### Restart Hints

- Safe strengthened reading to preserve:
  - external GPU capture now gives real `SceneDomain` and `PassClass` truth
  - repeated compositor-style `tex[0]/tex[1]` plus screen inputs is now strong exclusion evidence
  - repeated `texture0..N` plus depth-adjacent large geometry passes is now strong positive material-like evidence
  - exact RenderDoc-to-live shader-hash closure is still open and must stay a separate bridge task

### Current Request Addendum (`2026-04-22`, helper-family carrier plausibility matrix)

#### Problem

The helper-family boundary docs now constrained ownership more honestly, but the offline route still lacked one compact row-by-row comparison layer. That kept the next offline step too verbal: plausible versus premature carrier claims still had to be reconstructed across several docs.

#### Chosen Approach

- add one matrix-style synthesis doc instead of another generic boundary note
- compare the three narrowed helper-family rows against the shared carrier order
- record not only what is plausible now, but also what exact evidence would promote the next claim

#### Actions

- [x] Add `docs/workflows/material-pipeline/helper-family-carrier-plausibility-matrix.md`.
- [x] Rebind the matrix into the three weak helper-family sheets, shader family registry, queue, status catalog, and current plan.

#### Restart Hints

- Safe strengthened reading to preserve:
  - the current offline helper-family route now has a compact row-by-row matrix
  - each row now explicitly separates:
    - narrowed runtime candidate
    - plausible carrier reading
    - premature claim
    - promotion trigger
  - the next offline step should build from this matrix, not reopen the same generic carrier-order argument

### Current Request Addendum (`2026-04-22`, helper-family package-boundary propagation)

#### Problem

The offline bridge docs now bounded helper-family carrier ownership more honestly, but that stronger wording still lived mostly in bridge/boundary docs instead of the weak family sheets and family registry rows that users actually read first.

#### Chosen Approach

- do not create another abstract helper-family note
- push the new package-side carrier boundary directly into the weak family sheets and the family registry
- raise maturity only where the family docs themselves become stronger

#### Actions

- [x] Rebind helper-family package-carrier limits into:
  - `ShaderDayNightParameters`
  - `Projection, Reveal, And Lightmap Families`
  - `GenerateSpotLightmap And NextFloorLightMapXform`
  - `Shader Family Registry`
- [x] Rebind the new reading into the queue, status catalog, and current plan.

#### Restart Hints

- Safe strengthened reading to preserve:
  - helper-family sheets now explicitly carry package-side carrier order
  - helper-family sheets now explicitly separate "carrier plausibility" from "carrier closure"
  - the strongest current offline move is no longer another abstract boundary doc
  - the strongest current offline move is pushing narrowed carrier plausibility into the family rows while keeping the final package-to-runtime-to-scene join open

### Current Request Addendum (`2026-04-22`, projection-reveal runtime-cluster narrowing)

#### Problem

After `ShaderDayNightParameters` narrowed to `F04` and generated-light narrowed to `F03`, the umbrella still had one weak middle branch: the remaining projection/reveal helper packet inside the broader runtime corpus.

#### Chosen Approach

- stay inside the checked-in DX11 runtime corpus
- isolate the stable `srctex` packet inside `F04`
- promote only the narrowest honest middle-branch conclusion

#### Actions

- [x] Freeze the projection/reveal runtime-candidate snapshot in `tmp/projection_reveal_runtime_cluster_candidates_2026-04-22.json`.
- [x] Add `docs/workflows/material-pipeline/live-proof-packets/projection-reveal-runtime-cluster-candidate-floor.md`.
- [x] Rebind the `F04` `srctex + tex` reading into the umbrella sheet, runtime helper-family clustering floor, packet index, status catalog, and current plan.

#### Restart Hints

- The projection/reveal runtime-candidate snapshot is now:
  - `tmp/projection_reveal_runtime_cluster_candidates_2026-04-22.json`
- Safe strengthened reading to preserve:
  - the remaining projection/reveal branch should now start from the stable `F04` `srctex + tex` packet
  - the leading variable packet is:
    - `fsize`
    - `offset`
    - `scolor`
    - `srctexscale`
    - `texscale`
  - this middle branch no longer needs to start from a broad `F03/F04/F05` bucket

### Current Request Addendum (`2026-04-22`, projection-reveal runtime context-gap closure)

#### Problem

The remaining projection/reveal helper branch was already narrowed to one stable `F04` `srctex + tex` packet, but the docs still left one ambiguity open: whether the current checked-in broad runtime captures already separated that packet by scene/context enough to promote a stronger family claim without another tagged capture run.

#### Chosen Approach

- stay inside the checked-in DX11 runtime corpus
- compare representative `srctex` packet hashes across the same broad sessions already used for helper-family narrowing
- promote only the honest blocker conclusion:
  - the narrowed packet persists across the broad sessions
  - the current manifests are still not scene-tagged enough for family-context closure

#### Actions

- [x] Freeze the broad-capture parity snapshot in `tmp/projection_reveal_runtime_context_gap_snapshot_2026-04-22.json`.
- [x] Add `docs/workflows/material-pipeline/live-proof-packets/projection-reveal-runtime-context-gap.md`.
- [x] Rebind the context-gap result into the umbrella family sheet, runtime helper-family clustering floor, packet index, status catalog, and current plan.

#### Restart Hints

- The context-gap snapshot is now:
  - `tmp/projection_reveal_runtime_context_gap_snapshot_2026-04-22.json`
- Safe strengthened reading to preserve:
  - representative `F04` `srctex + tex` hashes survive across all current broad checked-in captures
  - current session manifests still do not carry scene/context labels strong enough for projection/reveal family-context closure
  - the next honest move is no longer “inspect more of the same broad captures”
  - the next honest move is one context-tagged projection/reveal or refraction-adjacent capture

### Current Request Addendum (`2026-04-22`, generated-light runtime-cluster narrowing)

#### Problem

The generated-light row already had a carry-through packet, but the runtime side still sat inside one broad helper-family bucket. That left `GenerateSpotLightmap / NextFloorLightMapXform` with a weaker next step than the neighboring `ShaderDayNight` lane.

#### Chosen Approach

- stay inside the checked-in DX11 runtime corpus
- promote only the narrowest honest generated-light runtime bridge
- prefer the stable `maptex` packet over a broader `F04/F05` first pass

#### Actions

- [x] Freeze the generated-light runtime-candidate snapshot in `tmp/generated_light_runtime_cluster_candidates_2026-04-22.json`.
- [x] Add `docs/workflows/material-pipeline/live-proof-packets/generated-light-runtime-cluster-candidate-floor.md`.
- [x] Rebind the `F03`-first reading into the generated-light family sheet, umbrella sheet, runtime helper-family clustering floor, queue row, packet index, status catalog, and current plan.

#### Restart Hints

- The generated-light runtime-candidate snapshot is now:
  - `tmp/generated_light_runtime_cluster_candidates_2026-04-22.json`
- Safe strengthened reading to preserve:
  - generated-light should now start from `F03`
  - the leading runtime packet is:
    - `maptex + tex + Constants`
    - `compx`
    - `compy`
    - `mapScale`
    - `scale`
  - `F04` remains the broader comparator, not the first generated-light runtime target

### Current Request Addendum (`2026-04-22`, generated-light runtime context-gap closure)

#### Problem

The generated-light helper branch was already narrowed to one stable `F03` `maptex + tex` packet, but the docs still left one ambiguity open: whether the current checked-in broad runtime captures already separated that packet by scene/context enough to promote a stronger family claim without another tagged capture run.

#### Chosen Approach

- stay inside the checked-in DX11 runtime corpus
- compare representative `maptex` packet hashes across the same broad sessions already used for helper-family narrowing
- promote only the honest blocker conclusion:
  - the narrowed packet persists across the broad sessions
  - the current manifests are still not scene-tagged enough for family-context closure

#### Actions

- [x] Freeze the broad-capture parity snapshot in `tmp/generated_light_runtime_context_gap_snapshot_2026-04-22.json`.
- [x] Add `docs/workflows/material-pipeline/live-proof-packets/generated-light-runtime-context-gap.md`.
- [x] Rebind the context-gap result into the generated-light family sheet, runtime helper-family clustering floor, queue row, packet index, status catalog, and current plan.

#### Restart Hints

- The context-gap snapshot is now:
  - `tmp/generated_light_runtime_context_gap_snapshot_2026-04-22.json`
- Safe strengthened reading to preserve:
  - representative `F03` `maptex + tex` hashes survive across all current broad checked-in captures
  - current session manifests still do not carry scene/context labels strong enough for generated-light family-context closure
  - the next honest move is no longer “inspect more of the same broad captures”
  - the next honest move is one lighting-heavy context-tagged capture

### Current Request Addendum (`2026-04-22`, context-tagged capture contract)

#### Problem

The weak helper-family rows now all agree on the same blocker: current broad DX11 sessions are useful enough to narrow candidates, but not tagged enough to promote family ownership. Without a concrete capture contract, “run a tagged capture” is still too vague to be restart-safe.

#### Chosen Approach

- define one explicit capture-labeling contract in the DX11 satellite track
- allow a temporary manual sidecar instead of waiting for manifest-schema code changes
- rebind that contract into the main queue and restart docs as the next honest data-gathering step

#### Actions

- [x] Add `satellites/ts4-dx11-introspection/docs/context-tagged-capture-contract.md`.
- [x] Add `satellites/ts4-dx11-introspection/schemas/context-tag.schema.json`.
- [x] Rebind the contract into the DX11 README, research restart guide, runtime helper-family clustering floor, P1 queue, and current plan.

#### Restart Hints

- The operational contract is now:
  - `satellites/ts4-dx11-introspection/docs/context-tagged-capture-contract.md`
- The temporary sidecar schema is now:
  - `satellites/ts4-dx11-introspection/schemas/context-tag.schema.json`
- Safe strengthened reading to preserve:
  - helper-family progress no longer blocks on immediate manifest-tooling changes
  - `run-live-capture.ps1` can now write `context-tags.json` directly through context-tag parameters
  - a manual `context-tags.json` sidecar is still an accepted temporary truth layer for scene labels
  - the next honest capture set is:
    - one `shader-daynight` tagged session
    - one `generated-light` tagged session
    - one `projection-reveal` tagged session

### Current Request Addendum (`2026-04-22`, helper preset capture recipes)

#### Problem

The helper-family blocker was already operationalized at the schema level, but the next batch still required too much manual field assembly. That made the tagged-capture step restart-safe in theory, but still clumsy in practice.

#### Chosen Approach

- add helper presets to the standard runner
- add one recipe doc with exact runnable commands for the three minimum helper-family sessions
- rebind that recipe layer into the main research docs so the next step is procedural rather than free-form

#### Actions

- [x] Extend `satellites/ts4-dx11-introspection/scripts/run-live-capture.ps1` with `-HelperPreset`.
- [x] Add `satellites/ts4-dx11-introspection/docs/context-tagged-capture-recipes.md`.
- [x] Rebind the recipe layer into the DX11 README, research restart guide, runtime helper-family clustering floor, P1 queue, and current plan.

#### Restart Hints

- The runner now accepts:
  - `-HelperPreset ShaderDayNight`
  - `-HelperPreset GeneratedLight`
  - `-HelperPreset ProjectionReveal`
- Each preset now fills:
  - `WorldMode`
  - `FamilyFocus`
  - `SceneClass`
  - `ExpectedCandidateClusters`
- The caller still must supply:
  - `SceneLabel`
  - `TargetAssetsOrEffects`
  - `Notes`
- The exact runnable commands now live in:
  - `satellites/ts4-dx11-introspection/docs/context-tagged-capture-recipes.md`

### Current Request Addendum (`2026-04-22`, tagged helper compare workflow)

#### Problem

The tagged-capture step was now runnable, but the next post-capture action was still underdefined. Without a standard compare path, one tagged session could still be overread in isolation.

#### Chosen Approach

- add one wrapper script for tagged helper-session comparison
- define one small analysis workflow doc that forces target-vs-control reading
- rebind that workflow into the DX11 and material-pipeline docs

#### Actions

- [x] Add `satellites/ts4-dx11-introspection/scripts/compare-tagged-helper-captures.ps1`.
- [x] Add `satellites/ts4-dx11-introspection/docs/context-tagged-capture-analysis-workflow.md`.
- [x] Rebind the workflow into the DX11 README, runtime helper-family clustering floor, P1 queue, research restart guide, and current plan.

#### Restart Hints

- Tagged helper-session comparison now has one standard wrapper:
  - `satellites/ts4-dx11-introspection/scripts/compare-tagged-helper-captures.ps1`
- The wrapper now:
  - resolves target/control capture paths
  - requires `context-tags.json` on the target side
  - chooses `compare` or `compare-groups` automatically
- The interpretation rule now lives in:
  - `satellites/ts4-dx11-introspection/docs/context-tagged-capture-analysis-workflow.md`
- Safe reading to preserve:
  - one tagged session alone is not a helper-family uplift
  - the next honest runtime promotion requires target-vs-control comparison
  - target and control should keep the same helper-family focus and differ mainly by scene emphasis

### Current Request Addendum (`2026-04-22`, updated shader spec synthesis)

#### Problem

The updated DX11 shader spec now carries more than reflection tables. It also has an explicit package-side carrier layer and executable-linked disassembly signals, but the main research docs were still describing the runtime spec mostly as a reflection-only unlock.

#### Chosen Approach

- absorb only the parts that genuinely change the research picture
- keep the main blocker wording honest
- avoid pretending that the new spec already closes family ownership or pass truth

#### Actions

- [x] Re-read the updated `TS4 Material Shader Spec`.
- [x] Rebind the useful additions into `Runtime Shader Interface Baseline`.
- [x] Keep the global blocker wording at the three-way bridge:
  - package carrier
  - runtime shader family
  - scene/pass context

#### Restart Hints

- The useful new spec additions are:
  - explicit package-side carrier priority:
    - `MATD` first
    - `MTST` second
    - `Geometry` / `Model` / `ModelLOD` kept in the same bridge path
  - executable-linked disassembly signals:
    - texture sampling
    - `sample_l`
    - loops
    - dynamic constant-buffer indexing
- Safe strengthened reading to preserve:
  - this narrows the runtime-to-package bridge
  - it does not yet close family ownership
  - it does not yet close draw/pass truth

### Current Request Addendum (`2026-04-22`, explicit bridge-boundary document)

#### Problem

The main blocker had become clearer in the running commentary, but it still was not isolated as its own durable boundary doc. That made it too easy to fall back to vague wording like "we need more research" instead of naming the missing join precisely.

#### Chosen Approach

- add one narrow bridge-boundary doc
- phrase the blocker as the missing three-way join:
  - package-side carrier
  - runtime shader cluster/family
  - scene/pass context
- link that boundary back into the runtime baseline and queue

#### Actions

- [x] Add `docs/workflows/material-pipeline/package-runtime-scene-bridge-boundary.md`.
- [x] Rebind it into `Runtime Shader Interface Baseline`, `P1 Live-Proof Queue`, and `current-plan`.

#### Restart Hints

- The strongest current blocker wording is now isolated in:
  - `docs/workflows/material-pipeline/package-runtime-scene-bridge-boundary.md`
- Safe reading to preserve:
  - package-side carrier priority is materially better now
  - runtime interface evidence is materially better now
  - helper-family clustering is materially better now
  - the missing join is still:
    - package carrier -> runtime family/cluster -> scene/pass context

### Current Request Addendum (`2026-04-22`, helper-family package-carrier boundary)

#### Problem

The bridge blocker was now explicit, but the offline route was still too vague. Without a helper-family carrier-boundary doc, package-side work could still drift into overclaiming `MATD` or `MTST` ownership for weak helper rows.

#### Chosen Approach

- add one narrow package-carrier boundary doc for helper-family rows
- keep the result at ownership-boundary wording only
- rebind it into the global bridge doc and queue as the honest offline route

#### Actions

- [x] Add `docs/workflows/material-pipeline/helper-family-package-carrier-boundary.md`.
- [x] Rebind it into `Package, Runtime, And Scene Bridge Boundary`, `P1 Live-Proof Queue`, and `current-plan`.

#### Restart Hints

- The current offline bridge companion is now:
  - `docs/workflows/material-pipeline/helper-family-package-carrier-boundary.md`
- Safe reading to preserve:
  - `MATD` and `MTST` are better constrained than before
  - they still do not close helper-family ownership by themselves
  - the offline route is now:
    - compare narrowed runtime helper candidates against package-side carrier expectations
    - stop at ownership-boundary wording

### Current Request Addendum (`2026-04-22`, ShaderDayNight runtime context-gap closure)

#### Problem

The `ShaderDayNightParameters` runtime route was already narrowed to `F04` first and `F05` second, but the docs still left one ambiguity open: whether the checked-in broad runtime captures already separated those candidates by scene/context enough to promote a stronger claim without another capture run.

#### Chosen Approach

- stay inside the checked-in DX11 runtime corpus
- compare representative `F04` and `F05` hashes across the current broad sessions
- promote only the honest blocker conclusion:
  - both candidate clusters persist across all broad captures
  - the captures are not scene-tagged enough to separate them

#### Actions

- [x] Freeze the broad-capture parity snapshot in `tmp/shaderdaynight_runtime_context_gap_snapshot_2026-04-22.json`.
- [x] Add `docs/workflows/material-pipeline/live-proof-packets/shader-daynight-runtime-context-gap.md`.
- [x] Rebind the context-gap result into the family sheet, runtime helper-family clustering floor, queue row, packet index, status catalog, and current plan.

#### Restart Hints

- The context-gap snapshot is now:
  - `tmp/shaderdaynight_runtime_context_gap_snapshot_2026-04-22.json`
- Safe strengthened reading to preserve:
  - representative `F04` and `F05` hashes survive across all current broad checked-in captures
  - current session manifests do not carry scene/context labels strong enough for family-context closure
  - the next honest move is no longer “inspect more of the same broad captures”
  - the next honest move is:
    - keep the visible-root packet
    - keep the `F04`-first candidate packet
    - run one context-tagged capture before promoting stronger scene-bound ownership

### Current Request Addendum (`2026-04-21` continued again, ShaderDayNight runtime-cluster narrowing)

#### Problem

The helper-family runtime route was no longer empty, but `ShaderDayNightParameters` still sat inside one broad `F03/F04/F05` clustering bucket. That was enough to suggest a direction, but not enough to tell the next autonomous batch where to start.

#### Chosen Approach

- keep the work bounded inside the checked-in DX11 runtime corpus
- compare concrete seeded `F04` and `F05` members instead of reopening broad name hunting
- promote only the narrowest honest conclusion:
  - `F04` is the leading runtime cluster candidate
  - `F05` is the nearest color-aware sibling comparator

#### Actions

- [x] Freeze the representative `F04` and `F05` packet in `tmp/shaderdaynight_runtime_cluster_candidates_2026-04-21.json`.
- [x] Add `docs/workflows/material-pipeline/live-proof-packets/shader-daynight-runtime-cluster-candidate-floor.md`.
- [x] Rebind the narrowed `F04`-first reading into the family sheet, runtime helper-family clustering floor, packet index, queue row, and status catalog.

#### Restart Hints

- The narrowed runtime-cluster snapshot is now:
  - `tmp/shaderdaynight_runtime_cluster_candidates_2026-04-21.json`
- Safe strengthened reading to preserve:
  - `F04` is now the strongest current runtime cluster candidate for `ShaderDayNightParameters`
  - `F05` stays the nearest color-aware sibling comparator
  - the best next step is no longer broad `F03/F04/F05` clustering first
  - the best next step is:
    - keep the visible-root packet
    - run context-tagged capture against `F04`
    - compare against `F05` before widening farther

### Current Request Addendum (`2026-04-21` continued again, runtime shader-interface baseline integration)

#### Problem

The new `TS4 Material Shader Spec` created a strong live-runtime shader-interface corpus, but the main material research docs still treated the biggest global blocker as if we had almost no runtime contract layer. Without integration, that evidence would remain siloed in the satellite track and would not change the main queue, registry, or status surfaces.

#### Chosen Approach

- treat the new spec as a runtime-interface baseline, not as a final family or package-material truth layer
- add one explicit material-pipeline doc that explains:
  - what the new runtime corpus closes
  - what it still does not close
  - how it changes the next research steps
- only raise statuses where the new runtime corpus genuinely narrows the blocker:
  - shader-family registry
  - weak helper-family handoff wording
  - queue guidance
  - status catalog

#### Actions

- [x] Read the new `TS4 Material Shader Spec` and its raw interface summary.
- [x] Add a `Runtime Shader Interface Baseline` doc under the main material-pipeline workflow set.
- [x] Rebind the new runtime-interface layer into the shader-family registry, queue guidance, status catalog, and current plan.
- [x] Add the first helper-family clustering floor from the runtime corpus for the weakest helper-family rows.
- [ ] Next follow-up: use `F03`, `F04`, and `F05` plus context-tagged captures to narrow one helper-family row further.

#### Restart Hints

- New baseline doc:
  - `docs/workflows/material-pipeline/runtime-shader-interface-baseline.md`
- Safe strengthened reading to preserve:
  - runtime shader inventory is now real at the hash/interface layer:
    - `1922` unique shaders
    - `1043` pixel shaders
    - `879` vertex shaders
    - `364` executable-linked
  - the largest global blocker narrowed:
    - we no longer lack runtime shader-interface truth in general
    - we now mainly lack family clustering and package-field ownership
  - weak helper-family rows now have a better next path:
    - family-local clustering over runtime interfaces first
    - package ownership mapping second
    - more narrative name-hunting only if clustering stalls
  - the first helper-family clustering floor is now checked in:
    - `docs/workflows/material-pipeline/runtime-helper-family-clustering-floor.md`
    - strongest current bridge clues are `F03`, `F04`, `F05` plus `srctex`, `dsttex`, `maptex`, `alphatex`, `texscale`, `offset`, `scolor`, `srctexscale`, and `texgen`

### Current Request Addendum (`2026-04-21` continued again, compositor same-layer boundary floor)

#### Problem

The compositor-order surfaces were already structurally strong, but `CompositionMethod` and `SortLayer` still leaned on separate baselines plus local external tooling instead of one compact same-layer query floor. That left the next character-side compositor handoff more prose-heavy than the sibling shell and worn-slot surfaces.

#### Chosen Approach

- keep the work inside the existing character-side compositor lane:
  - no new family search
  - no new runtime math claim
  - no return to `SimSkinMask`
- generate one narrow joint snapshot from `cas_part_facts` for:
  - overall `CompositionMethod + SortLayer` pairs
  - readable shell/worn-slot rows
  - ordinary low-value overlay rows
  - mixed high-byte comparison rows
- sync only the compositor boundary, overlay/detail table, status catalog, and current plan

#### Actions

- [x] Query the top same-layer `CompositionMethod + SortLayer` pairs from `cas_part_facts`.
- [x] Query readable shell/worn-slot pairs and low-value overlay/detail pairs from the same layer.
- [x] Freeze the result in `tmp/composition_sortlayer_boundary_snapshot_2026-04-21.json`.
- [x] Rebind the new floor into the boundary doc, overlay/detail table, status catalog, and current plan.

#### Restart Hints

- The same-layer compositor snapshot is now:
  - `tmp/composition_sortlayer_boundary_snapshot_2026-04-21.json`
- Safe strengthened reading to preserve:
  - overall top pairs are now directly visible in one layer:
    - `0 | 0 = 18668`
    - `32 | 65536 = 12212`
    - `0 | 16000 = 8970`
  - readable shell/worn-slot rows still keep different dominant pairs than ordinary overlay rows:
    - `Head -> 0 | 1000 = 88`
    - `Hair -> 0 | 12000 = 5771`
    - `Full Body -> 32 | 65536 = 3437`
  - ordinary low-value overlay rows stay cleaner than mixed high-byte families:
    - `Lipstick -> 4 | 5500 = 496`
    - `Eyeshadow -> 4 | 5200 = 304`
    - `0x41000000 -> 0 | 0 = 806`, `255 | 0 = 214`
  - the `Overlay-Detail Priority After High-Byte Stack` packet now also carries this same-layer floor directly
  - the next honest handoff stays inside narrow compositor interpretation, not back into census plumbing

### Current Request Addendum (`2026-04-21` continued again, body-head shell direct floor)

#### Problem

The `SimSkin` body/head shell packet and body/head authority table were already strong in synthesis, but they still leaned more on external identity plus implementation boundary than on one compact direct local shell-floor snapshot. That made the character-side shell lane less restart-safe than the sibling worn-slot table.

#### Chosen Approach

- keep the packet inside the existing character-side authority lane:
  - no new family search
  - no new shader claim
  - no return to `SimSkinMask`
- generate one narrow direct shell-floor snapshot from:
  - `cas_part_facts`
  - `sim_template_body_parts`
  - `sim_archetype_body_shell_audit_fresh.json`
- sync only the packet, body/head table, `CAS/Sim` matrix, queue row, status catalog, and current plan

#### Actions

- [x] Query direct `cas_part_facts` rows for `Head`, `Full Body`, `Top`, `Bottom`, and `Shoes`.
- [x] Query `sim_template_body_parts` for the current exact head-template lane.
- [x] Freeze the result in `tmp/body_head_shell_authority_snapshot_2026-04-21.json`.
- [x] Rebind the direct floor into the live-proof packet, body/head table, `CAS/Sim` matrix, queue row, status catalog, and current plan.

#### Restart Hints

- The direct shell-floor snapshot is now:
  - `tmp/body_head_shell_authority_snapshot_2026-04-21.json`
- Safe strengthened reading to preserve:
  - `Head` currently stays narrow and exact on the parsed layer:
    - `90` rows
    - `composition=0 = 88`
  - the body-driving shell lane stays broader through:
    - `Full Body = 6276`
    - `Top = 9287`
    - `Bottom = 6191`
  - the graph-backed archetype audit still keeps:
    - `FullBodyShell = 23`
    - `SplitBodyLayers = 12`
  - this strengthens the current authority order:
    - body shell anchors assembly
    - head shell merges as a sibling branch
    - compositor-order follow-up is now a cleaner next handoff than another `SimSkinMask` pass

### Current Request Addendum (`2026-04-21` continued again, SimSkinMask direct-census negative floor)

#### Problem

The earlier bounded `SimSkinMask` refresh already proved that the public and local sample lanes stayed negative, but the restart-safe docs still did not capture the stronger new fact that the completed direct `CASPart -> GEOM -> family` floor also fails to surface any `SimSkinMask` family row.

#### Chosen Approach

- keep the packet bounded instead of widening the search again:
  - verify the checked-in fullscan plus per-package result shards directly
  - record one small public reinforcement only where it sharpens the same semantic split:
    - public `TS4SimRipper`
    - public `Sims 4: CASPFlags`
- sync only the `SimSkinMask` packet, family sheet, queue row, matrix row, status catalog, and current plan

#### Actions

- [x] Verify that the checked-in direct `CASPart -> GEOM -> family` fullscan currently surfaces `SimSkin` and `SimGlass`, but no `SimSkinMask`.
- [x] Verify that the `414` per-package result shards under `tmp/caspart_geom_shader_census_run/package-results` also stay negative for `SimSkinMask`.
- [x] Re-check the local checked-in wrapper/export packet and the bounded public packet for the same semantic split.
- [x] Rebind the stronger negative floor into the live-proof packet, family sheet, queue row, matrix row, status catalog, and current plan.

#### Restart Hints

- Safe strengthened reading to preserve:
  - `SimSkinMask` is now bounded negative across four local layers:
    - bundled or mirrored `.simgeom` samples
    - profile archaeology
    - direct `CASPart -> GEOM -> family` census
    - checked-in external wrapper and export snapshots
  - the bounded public refresh still stops at:
    - `TS4SimRipper` exposing `SimSkin` and `SimGlass`
    - `Sims 4: CASPFlags` exposing `SkinOverlay`
  - the next honest proof burden is still one genuinely new external or live sample, not another repo-local grep batch

### Current Request Addendum (`2026-04-21` continued again, documentation readiness plus SimSkinMask bounded refresh)

#### Problem

After the transparent-object quartet was frozen, the next request needed two things at once: an honest status readout of documentation readiness, and a continuation into the next unfinished Tier A lane without drifting back into already-closed quartet work.

#### Chosen Approach

- report readiness directly from the checked-in status surfaces:
  - `documentation-status-catalog`
  - `P1` queue
  - family sheets
- continue from the queue handoff instead of reopening the frozen quartet:
  - run one bounded external-first refresh on `SimSkin` versus `SimSkinMask`
  - stop if no genuinely new peer-geometry or export sample appears

#### Actions

- [x] Re-read the current status surfaces for deep dives, family sheets, and live-proof packets.
- [x] Re-open the current `SimSkin` versus `SimSkinMask` packet and family sheet.
- [x] Run one bounded local/public refresh to check whether a genuinely new peer `SimSkinMask` sample surfaced.
- [x] Tighten the `SimSkinMask` docs only enough to record that the public refresh stayed negative.

#### Restart Hints

- Safe readiness summary to preserve:
  - `Build/Buy` object authority and object-transparency docs are now among the strongest surfaces, with the widened quartet frozen at the current layer
  - `CAS/Sim` authority is strong but still less closed than the best `Build/Buy` seams
  - `SimSkin / SimGlass / SimSkinMask` remains a strong family sheet, but `SimSkinMask` is still a bounded negative-result lane rather than a promoted peer branch
- Safe `SimSkinMask` reading to preserve:
  - the fresh bounded public refresh on `2026-04-21` still did not surface a peer named `SimSkinMask` geometry/export branch
  - the next honest proof burden is still one genuinely new live or exported sample outside the mirrored `TS4SimRipper` packet

### Current Request Addendum (`2026-04-21` continued again, Build/Buy curtain route closure and quartet family split)

#### Problem

The window-side transparent-object question was already bounded, but the shared restart surfaces still stopped one step before a stable quartet verdict. After the window structural-cutout floor, the remaining question was whether the surviving curtains really closed through explicit `AlphaBlended` or only through a weaker threshold/cutout route, and whether that was enough to freeze the widened quartet as a family split.

#### Chosen Approach

- keep the packet external-first:
  - windows/openings remain on the structural `Model Cutout` / `Cut Info Table` branch
  - curtains only promote to `AlphaBlended` when that route is explicit
  - object glass stays separate from both
- use direct local resource inspection only for the surviving curtains:
  - `norenShortTileable`
  - `strawTileable2Tile`
- stop at the first honest closure:
  - if explicit `AlphaBlended` does not survive locally, keep the curtain side at weaker threshold/cutout
  - once both sides are bounded, freeze the widened quartet instead of reopening the same route by inertia

#### Actions

- [x] Re-open the window structural-verdict packet, family sheet, queue row, matrix row, ledger, packet index, status catalog, and current plan.
- [x] Directly inspect same-instance curtain companions for `norenShortTileable` and `strawTileable2Tile`.
- [x] Directly inspect both curtain `CutoutInfoTable` resources and confirm the weak companion floor plus missing same-instance `ModelCutout`.
- [x] Freeze the curtain-side route in `tmp/buildbuy_curtain_route_snapshot_2026-04-21.json`.
- [x] Create `docs/workflows/material-pipeline/live-proof-packets/buildbuy-curtain-route-closure.md`.
- [x] Create `docs/workflows/material-pipeline/live-proof-packets/buildbuy-window-curtain-quartet-family-split.md`.
- [x] Rebind the new packets into the queue, matrix, ledger, packet index, status catalog, family sheet, and current plan.

#### Restart Hints

- The curtain-route snapshot is now:
  - `tmp/buildbuy_curtain_route_snapshot_2026-04-21.json`
- The current narrow handoff stack is now:
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-window-structural-cutout-verdict-floor.md`
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-curtain-route-closure.md`
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-window-curtain-quartet-family-split.md`
- Safe reading to preserve:
  - neither surviving curtain currently closes as explicit `AlphaBlended`
  - `norenShortTileable` survives only through cutout-leaning transparency
  - `strawTileable2Tile` stays opaque as the negative control
  - the widened quartet is now safely frozen as:
    - windows -> structural cutout/opening first
    - curtains -> weaker threshold/cutout
    - object glass not selected
  - the next highest-priority step is no longer another packet on this same quartet
  - hand the next autonomous batch to the next unfinished Tier A lane:
    - `SimSkin` versus `SimSkinMask`
  - `ProbeAsset` still needs sequential runs here because the shared sqlite probe-cache locks under parallel use

### Current Request Addendum (`2026-04-21` continued again, Build/Buy window structural-cutout verdict floor)

#### Problem

The surviving window pair no longer needed another existence check for structural companions, but the restart-safe docs still stopped one step short of a usable family verdict. After the `CutoutInfoTable` floor, the next question was whether `ModelCutout` also survived on the same model roots and whether that full structural pair was now strong enough to carry the window-side verdict.

#### Chosen Approach

- keep the packet external-first:
  - windows/openings stay on the structural `Model Cutout` / `Cut Info Table` branch
  - object glass stays separate
  - material cutout hints can remain secondary evidence instead of the main verdict
- use direct local resource inspection only for the surviving windows:
  - `sliding2Tile`
  - `windowBox2Tile`
- stop at the first honest verdict floor:
  - same-instance `ModelCutout` closure is enough to close the structural pair
  - once the pair is closed, the window-side family verdict can move forward even if exact runtime precedence remains open

#### Actions

- [x] Re-open the cutout-info packet, strongest-pair packet, family sheet, queue row, matrix row, ledger, packet index, status catalog, and current plan.
- [x] Directly confirm same-instance `ModelCutout` on the surviving window pair.
- [x] Freeze the full structural-pair state in `tmp/buildbuy_window_structural_cutout_snapshot_2026-04-21.json`.
- [x] Create `docs/workflows/material-pipeline/live-proof-packets/buildbuy-window-modelcutout-companion-closure.md`.
- [x] Create `docs/workflows/material-pipeline/live-proof-packets/buildbuy-window-structural-cutout-verdict-floor.md`.
- [x] Rebind the new packets into the queue, matrix, ledger, packet index, status catalog, family sheet, and current plan.

#### Restart Hints

- The structural-cutout snapshot is now:
  - `tmp/buildbuy_window_structural_cutout_snapshot_2026-04-21.json`
- The current narrow handoff stack is now:
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-window-cutoutinfotable-companion-floor.md`
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-window-modelcutout-companion-closure.md`
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-window-structural-cutout-verdict-floor.md`
- Safe reading to preserve:
  - both surviving windows now have same-instance `ModelCutout + CutoutInfoTable`
  - both structural companions sit on the exact promoted model roots
  - the window-side family verdict is now safely structural cutout/opening first, with material cutout hints secondary
  - exact runtime precedence against `AlphaCutoutMaterialDecodeStrategy` remains open, but no longer blocks verdict wording
  - the next highest-value question is now curtain-side route closure:
    - explicit `AlphaBlended`
    - or weaker threshold/cutout routing
  - `ProbeAsset` still needs sequential runs here because the shared sqlite probe-cache locks under parallel use

### Current Request Addendum (`2026-04-21` continued again, Build/Buy window cutout-info companion floor)

#### Problem

The strongest-pair packet already showed that the surviving windows were still under structural cutout pressure, but the docs still phrased that pressure as a hypothesis. The next restart-safe question was whether explicit structural companions actually existed on the surviving window pair.

#### Chosen Approach

- keep the packet narrow and external-first:
  - structural `Model Cutout` / `Cut Info Table` branch for windows/openings
  - no new widening
  - no final family relabel yet
- use direct local resource inspection only for the surviving windows:
  - `sliding2Tile`
  - `windowBox2Tile`
- stop at the first honest companion floor:
  - explicit `CutoutInfoTable` proof is enough
  - `ModelCutout` closure and authority order can remain next-step questions

#### Actions

- [x] Re-open the strongest-pair packet, family sheet, queue row, matrix row, ledger, packet index, status catalog, and current plan.
- [x] Directly locate same-instance `CutoutInfoTable` resources for the surviving window pair.
- [x] Directly inspect both `CutoutInfoTable` resources and confirm model-root-matching entries plus cutout/portal flags.
- [x] Freeze the result in `tmp/buildbuy_window_cutoutinfo_companion_snapshot_2026-04-21.json`.
- [x] Create `docs/workflows/material-pipeline/live-proof-packets/buildbuy-window-cutoutinfotable-companion-floor.md`.
- [x] Rebind the new packet into the queue, matrix, ledger, packet index, status catalog, and current plan.

#### Restart Hints

- The cutout-companion snapshot is now:
  - `tmp/buildbuy_window_cutoutinfo_companion_snapshot_2026-04-21.json`
- The current narrow handoff stack is now:
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-window-curtain-strongest-pair-material-divergence.md`
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-window-cutoutinfotable-companion-floor.md`
- Safe reading to preserve:
  - both surviving windows now have explicit same-instance `CutoutInfoTable`
  - both entries point back to the exact promoted model root
  - both entries carry `flags=0x321` with `IS_PORTAL` plus `USES_CUTOUT`
  - this is enough to promote a window-side structural companion floor
  - the next highest-value question is now matching `ModelCutout` closure and authority order versus `AlphaCutout` material hints
  - `ProbeAsset` still needs sequential runs here because the shared sqlite probe-cache locks under parallel use

### Current Request Addendum (`2026-04-21` continued again, Build/Buy window-curtain strongest-pair divergence)

#### Problem

The widened transparent-object lane no longer needed more route expansion, but the docs still stopped one step before the strongest direct material split. That left the next handoff too broad: it was clear that quartet-wide widening was done, but not yet clear which exact pair should drive the next verdict packet.

#### Chosen Approach

- keep the packet external-first:
  - windows/openings still lead with structural `Model Cutout` / `Cut Info Table` pressure
  - curtains still lead with explicit `AlphaBlended` when it exists, then threshold/cutout if that is the surviving material route
  - object glass stays separate from both
- use local probe text only for the strongest direct pair:
  - strongest window = `sliding2Tile`
  - strongest curtain = `norenShortTileable`
- stop at the first honest divergence claim:
  - do not overpromote a final winning family
  - do update the docs so the next step becomes window-side cutout-companion inspection

#### Actions

- [x] Re-open the window-curtain verdict boundary packet, family sheet, queue row, matrix row, ledger, packet index, status catalog, and current plan.
- [x] Reconfirm the strongest current window-side direct packet from local `sliding2Tile` probe output.
- [x] Reconfirm the strongest current curtain-side direct packet from local `norenShortTileable` probe output.
- [x] Freeze the pair in `tmp/buildbuy_window_curtain_strongest_pair_snapshot_2026-04-21.json`.
- [x] Create `docs/workflows/material-pipeline/live-proof-packets/buildbuy-window-curtain-strongest-pair-material-divergence.md`.
- [x] Rebind the strongest-pair packet into the queue, matrix, ledger, packet index, status catalog, and current plan.

#### Restart Hints

- The strongest-pair snapshot is now:
  - `tmp/buildbuy_window_curtain_strongest_pair_snapshot_2026-04-21.json`
- The current handoff stack is now:
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-window-curtain-widening-route.md`
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-window-curtain-family-verdict-boundary.md`
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-window-curtain-strongest-pair-material-divergence.md`
- Safe reading to preserve:
  - the widened quartet is still the live floor
  - the strongest window-side direct packet is `sliding2Tile`
  - the strongest curtain-side direct packet is `norenShortTileable`
  - that strongest pair already diverges enough to block one quartet-wide family label
  - the next highest-value question is now explicit `Model Cutout` / `Cut Info Table` closure on the surviving windows
  - `ProbeAsset` still needs sequential runs here because the shared sqlite probe-cache locks under parallel use

### Current Request Addendum (`2026-04-21` continued again, Build/Buy window-curtain family-verdict boundary sync)

#### Problem

The restart-safe surfaces drifted after the widened route moved again. The widening packet and local probe artifacts already show that the curtain pair survives to `Partial`, but several queue/status surfaces still describe curtain widening as the next unresolved step.

#### Chosen Approach

- keep the packet external-first:
  - object-side glass
  - threshold/cutout transparency
  - `AlphaBlended` curtains
  - structural `Model Cutout` / `Cut Info Table` opening resources
- use local probe and snapshot artifacts only to freeze the widened quartet floor:
  - windows first
  - curtains second
- stop at the first honest verdict boundary:
  - do not pretend the winning family is already closed
  - do update the docs so the next step is family-verdict closure instead of more widening

#### Actions

- [x] Re-open the widened route packet, family sheet, evidence ledger, queue, matrix, packet index, status catalog, and current plan.
- [x] Reconfirm direct local evidence that both curtain anchors now survive to `Partial`.
- [x] Reconfirm the external object-side branch split for:
  - object glass
  - threshold/cutout transparency
  - `AlphaBlended` curtains
  - structural cutout/opening resources
- [x] Refresh `tmp/buildbuy_window_curtain_widening_snapshot_2026-04-21.json` so the live floor matches the curtain probe outputs.
- [x] Create `docs/workflows/material-pipeline/live-proof-packets/buildbuy-window-curtain-family-verdict-boundary.md`.
- [x] Rebind the new verdict boundary into:
  - `docs/workflows/material-pipeline/live-proof-packets/README.md`
  - `docs/workflows/material-pipeline/p1-live-proof-queue.md`
  - `docs/workflows/material-pipeline/edge-family-matrix.md`
  - `docs/workflows/material-pipeline/family-sheets/object-glass-and-transparency.md`
  - `docs/workflows/material-pipeline/object-transparency-evidence-ledger.md`
  - `docs/workflows/material-pipeline/buildbuy-material-authority-matrix.md`
  - `docs/workflows/material-pipeline/documentation-status-catalog.md`
  - `docs/planning/current-plan.md`

#### Restart Hints

- The widened-route snapshot now matches the real four-fixture live floor:
  - `tmp/buildbuy_window_curtain_widening_snapshot_2026-04-21.json`
- The widening and handoff packets are now:
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-window-curtain-widening-route.md`
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-window-curtain-family-verdict-boundary.md`
- Safe reading to preserve:
  - the transparent-decor route stays stalled and should not be reopened by inertia
  - the widened quartet is now live:
    - `sliding2Tile`
    - `windowBox2Tile`
    - `strawTileable2Tile`
    - `norenShortTileable`
  - the next gap is family-verdict closure across the quartet, not more widening
  - external object-side evidence now keeps windows/openings and curtains under different leading hypotheses inside the same widened route
  - `ProbeAsset` still needs sequential runs here because the shared sqlite probe-cache locks under parallel use
  - family classification remains frozen:
    - object-glass first only when that branch is actually shown
    - threshold/cutout next when the fixture proves it
    - `AlphaBlended` stays the leading curtain-side hypothesis
    - `SimGlass` stays last-choice only

### Current Request Addendum (`2026-04-21` continued again, CASHotSpotAtlas control-bridge clarification)

#### Problem

The `CASHotSpotAtlas` docs already kept the family out of ordinary render-slot logic, but the external identity chain was still underspecified. The stronger creator packet already says more than “atlas plus hotspot plus modifiers,” and that sharper control-bridge wording was not restart-safe in the docs.

#### Chosen Approach

- keep the packet external-first:
  - `CASHotSpotAtlas`
  - `UV1`
  - atlas color values
  - `HotSpotControl`
  - `SimModifier`
- use the local `TS4SimRipper` snapshot only for the downstream bridge:
  - `SimModifier -> SMOD -> BGEO/DMap/BOND`
- stop before inventing runtime render semantics:
  - tighten the control chain
  - keep direct surface-slot claims out

#### Actions

- [x] Re-open the `CASHotSpotAtlas` family sheet, live-proof packet, queue row, edge-family matrix row, packet index, status catalog, and current plan.
- [x] Reconfirm the strongest external creator-facing packet for:
  - `CASHotSpotAtlas -> color value -> HotSpotControl -> SimModifier`
  - slider-direction and viewing-angle routing
- [x] Keep the local `TS4SimRipper` packet explicitly downstream-only.
- [x] Tighten:
  - `docs/workflows/material-pipeline/family-sheets/cas-hotspot-atlas.md`
  - `docs/workflows/material-pipeline/live-proof-packets/cas-hotspotatlas-carry-through.md`
  - `docs/workflows/material-pipeline/p1-live-proof-queue.md`
  - `docs/workflows/material-pipeline/edge-family-matrix.md`
  - `docs/workflows/material-pipeline/live-proof-packets/README.md`
  - `docs/workflows/material-pipeline/documentation-status-catalog.md`
  - `docs/planning/current-plan.md`

#### Restart Hints

- Safe reading to preserve:
  - the strongest current external identity chain is now:
    - `CASHotSpotAtlas -> color value -> HotSpotControl -> SimModifier`
  - the strongest local external bridge still starts later:
    - `SimModifier -> SMOD -> BGEO/DMap/BOND`
  - the docs now say explicitly that `HotSpotControl` carries slider-direction and viewing-angle routing in the external packet
  - none of this upgrades `CASHotSpotAtlas` into an ordinary runtime surface slot

### Current Request Addendum (`2026-04-21` continued again, Build/Buy window-curtain bridge and fixture pass)

#### Problem

The bounded window/curtain route was already frozen, but the real reopen status was stale. The docs still stopped at an `ObjectDefinition`-rooted geometry bridge gap even though the same-package `swap32` `Model` lane had not been honestly tested inside the current probe path.

#### Chosen Approach

- keep the packet external-first:
  - object-side glass/transparency split
  - creator-facing `Model Cutout` / `Cut Info Table` workflow for windows, doors, and archways
- use local survey/candidate-resolution artifacts only to preserve widened-route order:
  - full-bundle windows first
  - weaker curtain anchors second
- use current repo code only as tooling boundary:
  - inspect why `ProbeAsset` still stayed on `ObjectDefinition`
  - unblock same-package `swap32` model promotion without turning current decoder behavior into TS4 truth
- use live probes only to record bounded fixture results:
  - first on `sliding2Tile`
  - then on `windowBox2Tile`

#### Actions

- [x] Re-open the transparent-object route, negative-control, stall, and authority docs after the exhausted decor cluster.
- [x] Re-open external creator-facing sources for object-side cutouts and transparency.
- [x] Re-query current survey/candidate-resolution artifacts for widened window/curtain anchors.
- [x] Attempt direct `ProbeAsset` reopen on the strongest widened transformed model roots and record the ceiling honestly.
- [x] Reopen the strongest widened `set1` `ObjectDefinition` roots and record the exact identity-root outcome.
- [x] Compare raw versus `swap32` model-reference inspection for the widened route.
- [x] Inspect the local `AssetServices` / `ProbeAsset` path around `ObjectDefinition -> swap32 Model` promotion.
- [x] Add a bounded local fix so same-package `swap32` model promotion no longer depends on `indexStore` presence.
- [x] Record the new live floor:
  - `sliding2Tile -> Partial`
  - `windowBox2Tile -> Partial`
- [x] Refresh `tmp/buildbuy_window_curtain_widening_snapshot_2026-04-21.json`.
- [x] Update `docs/workflows/material-pipeline/live-proof-packets/buildbuy-window-curtain-widening-route.md`.
- [x] Rebind the stronger result into:
  - `docs/workflows/material-pipeline/family-sheets/object-glass-and-transparency.md`
  - `docs/workflows/material-pipeline/object-transparency-evidence-ledger.md`
  - `docs/workflows/material-pipeline/buildbuy-material-authority-matrix.md`
  - `docs/workflows/material-pipeline/p1-live-proof-queue.md`
  - `docs/workflows/material-pipeline/edge-family-matrix.md`
  - `docs/workflows/material-pipeline/live-proof-packets/README.md`
  - `docs/workflows/material-pipeline/documentation-status-catalog.md`
  - `docs/planning/current-plan.md`

#### Restart Hints

- The refreshed widened-route snapshot is:
  - `tmp/buildbuy_window_curtain_widening_snapshot_2026-04-21.json`
- The widening packet remains:
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-window-curtain-widening-route.md`
- Safe reading to preserve:
  - the stalled decor cluster stays exhausted and should not be retried without a stronger inspection layer
  - the next transparent-object widening phase is now a bounded window/curtain route, not one vague “window-heavy” sweep
  - `window2X1_EP10GENsliding2Tile` and `window2X1_EP10TRADwindowBox2Tile` are the strongest widened anchors because they preserve full `Model/Rig/Slot/Footprint` bundles
  - the real widened-route entry lane is now the exact `ObjectDefinition` identity roots, not the transformed model roots
  - the current leading window pair now survives to real `Partial` scenes through same-package `swap32` model promotion plus embedded `MLOD`
  - the next gap is curtain widening plus transparent-family verdict closure, not basic identity-root recovery
  - `ProbeAsset` currently needs sequential runs for this packet because the shared sqlite probe-cache locks under parallel use
  - family classification remains frozen:
    - object-glass first
    - threshold/cutout second
    - `AlphaBlended` third
    - `SimGlass` only last-choice

### Current Request Addendum (`2026-04-21` continued again, Tier A SimSkinMask boundary pass)

#### Problem

After the new worn-slot sibling table, the next highest-priority character-side gap was `SimSkin` versus `SimSkinMask`. The old packet was still honest, but it left too much ambiguity about whether the workspace might still hide a local counterexample rather than only profile-level mask vocabulary.

#### Chosen Approach

- keep the packet external-first:
  - lineage `SimSkin`
  - creator-facing skintone/overlay/mask workflows
- use local external tooling as the geometry/export truth layer:
  - `Enums.cs`
  - `ColladaDAE.cs`
  - `GEOM.cs`
  - bundled `.simgeom` resources
- use local profile archaeology only to tighten the bounded negative result:
  - `precomp_sblk_inventory`
  - `precomp_shader_profiles`
  - workspace-wide `.simgeom` inventory
- stop at the first honest integration checkpoint if the workspace search closes without a new non-mirrored sample

#### Actions

- [x] Re-open the current `SimSkin` / `SimSkinMask` packet, family sheet, queue row, and edge-family seam.
- [x] Re-open external creator-facing sources for skintone, overlays, masks, and lineage `SimSkin`.
- [x] Re-open local `TS4SimRipper` enum/export/geometry snapshots for `SimSkin`.
- [x] Search the workspace for `SimSkinMask` code/tool branches and `.simgeom` samples.
- [x] Create `tmp/simskin_vs_simskinmask_snapshot_2026-04-21.json`.
- [x] Tighten:
  - `docs/workflows/material-pipeline/live-proof-packets/simskin-vs-simskinmask.md`
  - `docs/workflows/material-pipeline/family-sheets/simskin-simglass-simskinmask.md`
  - `docs/workflows/material-pipeline/p1-live-proof-queue.md`
  - `docs/workflows/material-pipeline/edge-family-matrix.md`
  - `docs/workflows/material-pipeline/documentation-status-catalog.md`
  - `docs/planning/current-plan.md`

#### Restart Hints

- The new bounded local snapshot is:
  - `tmp/simskin_vs_simskinmask_snapshot_2026-04-21.json`
- Safe reading to preserve:
  - `SimSkin` remains the only current enum/export/geometry-positive branch in the local external tooling packet
  - `SimSkinMask` still survives in local profile archaeology, but only as a weaker fractured packet:
    - `12` profile rows
    - `6` packed-type variants
  - the current workspace `.simgeom` inventory no longer suggests a hidden local counterexample:
    - the only wider files beyond the checked-in `TS4SimRipper` resources are a mirrored `tmp/research/TS4SimRipper` copy
  - the next proof burden is now a genuinely new live or external sample, not another pass over mirrored local resources

### Current Request Addendum (`2026-04-21` continued, Tier A worn-slot sibling table)

#### Problem

The overlay/detail precedence packet closed the compositor bridge, but the next implementation-facing sibling gap was still open: there was no explicit restart-safe table for how `Hair`, `Accessory`, and `Shoes` should sit under the `SimSkin` shell packet without collapsing back into shell identity, skintone routing, or generic clothing-like compositor prose.

#### Chosen Approach

- keep the packet external-first:
  - `CAS Parts`
  - body-type slot vocabulary
  - `CASP -> GEOM -> RegionMap` linkage
- use local external tooling only as the structure layer for:
  - `CASP` fields
  - footwear `RegionMap` layering
  - `CompositionMethod` / `SortLayer` handling
- use local shard-backed counts only as a bounded pair-pattern floor, not as the meaning layer
- bind the result into one sibling authority table and sync the queue, matrix, status catalog, and plan before reopening another packet

#### Actions

- [x] Re-open the current shell, overlay/detail, queue, and status docs around the `Hair` / `Accessory` / `Shoes` gap.
- [x] Re-open external creator-facing sources for `CAS Parts`, body types, `GEOM`, and `RegionMap`.
- [x] Re-open local `TS4SimRipper` snapshots for `CASP`, `PreviewControl`, `TONE`, and `SkinBlender`.
- [x] Re-open the current repo implementation boundary in `AssetServices.cs` for exact part-link versus footwear body-assembly handling.
- [x] Create `tmp/hair_accessory_shoes_authority_snapshot_2026-04-21.json`.
- [x] Create `docs/workflows/material-pipeline/hair-accessory-shoes-authority-table.md`.
- [x] Rebind the new table into:
  - `docs/workflows/material-pipeline/cas-sim-material-authority-matrix.md`
  - `docs/workflows/material-pipeline/overlay-detail-family-authority-table.md`
  - `docs/workflows/material-pipeline/skintone-and-overlay-compositor.md`
  - `docs/workflows/material-pipeline/p1-live-proof-queue.md`
  - `docs/workflows/material-pipeline/documentation-status-catalog.md`
  - `docs/planning/current-plan.md`

#### Restart Hints

- The new sibling authority table is:
  - `docs/workflows/material-pipeline/hair-accessory-shoes-authority-table.md`
- The local bounded snapshot is:
  - `tmp/hair_accessory_shoes_authority_snapshot_2026-04-21.json`
- Safe reading to preserve:
  - `Hair` and `Accessory` stay in exact worn-slot authority first
  - `Shoes` stay a real worn slot, but inside footwear/body-assembly overlay logic rather than shell identity
  - `CASP -> GEOM -> material candidates` remains the post-selection chain for all three
  - skintone routing remains bounded away from these worn slots
  - the next highest-priority live-proof packet is now `SimSkin` versus `SimSkinMask`, not a drift back into Tier B or Tier C

### Current Request Addendum (`2026-04-21` long-batch resume, Tier B evidence pass)

#### Problem

The top addendum in this file is stale for the current restart point: `0x52` and `0x80` are already closed, the `EP10` transparent-object route is already stalled at its current reopen layer, and the next safe documentation batch is to tighten the unfinished Tier B external-first packets without pretending they outrank the still-open Tier A compositor-authority work.

#### Chosen Approach

- Rebuild context from the restart/process docs before touching any packet.
- Keep the priority split explicit:
  - Tier A still owns the next implementation-facing step.
  - this batch only strengthens Tier B packet floors and restart safety.
- Advance multiple bounded documentation packets in sequence in one run:
  - `CASHotSpotAtlas` carry-through
  - `ShaderDayNightParameters` visible-pass proof
  - `GenerateSpotLightmap` / `NextFloorLightMapXform`
- Fold only the proved parts back into the queue, matrix, packet index, status catalog, and current plan.

#### Actions

- [x] Re-read the restart guide, current plan, source map, shared guide, family priority, shader registry, edge-family matrix, queue, packet index, status catalog, and multi-agent workflow for the discovery-phase contract.
- [x] Reconfirm that the `0x52` / `0x80` packet stack is already closed and should not be reopened by inertia.
- [x] Reconfirm that the `EP10` transparent-decor route is already stalled at the current inspection layer and should not silently drive the queue.
- [x] Re-open external creator-facing evidence for:
  - `CASHotSpotAtlas`
  - `RevealMap`
  - `GenerateSpotLightmap`
  - `NextFloorLightMapXform`
- [x] Re-open local external tooling snapshots and local artifact evidence for the same packets.
- [x] Tighten the `CASHotSpotAtlas` family sheet and live-proof packet with explicit local external snapshot boundaries.
- [x] Tighten the `ShaderDayNightParameters` family sheet, evidence ledger, and live-proof packet with exact local helper counts and visible-root anchors.
- [x] Tighten the `GenerateSpotLightmap` / `NextFloorLightMapXform` family sheet, evidence ledger, and live-proof packet with exact local carry-through counts and the current adjacent-root boundary.
- [x] Rebind the proved result into:
  - `docs/workflows/material-pipeline/live-proof-packets/README.md`
  - `docs/workflows/material-pipeline/p1-live-proof-queue.md`
  - `docs/workflows/material-pipeline/edge-family-matrix.md`
  - `docs/workflows/material-pipeline/documentation-status-catalog.md`
  - `docs/planning/current-plan.md`
- [x] Keep the next highest-priority step honest: Tier A overlay/detail or broader compositor-authority follow-up still outranks new Tier B or Tier C deepening.

#### Restart Hints

- This batch did not reopen the completed high-byte `BodyType` packets.
- This batch did not reopen the stalled `EP10` transparent-decor route.
- The tightened Tier B packet stack now lives in:
  - `docs/workflows/material-pipeline/family-sheets/cas-hotspot-atlas.md`
  - `docs/workflows/material-pipeline/live-proof-packets/cas-hotspotatlas-carry-through.md`
  - `docs/workflows/material-pipeline/family-sheets/shader-daynight-parameters.md`
  - `docs/workflows/material-pipeline/shader-daynight-evidence-ledger.md`
  - `docs/workflows/material-pipeline/live-proof-packets/shader-daynight-visible-pass.md`
  - `docs/workflows/material-pipeline/family-sheets/generate-spotlightmap-nextfloorlightmapxform.md`
  - `docs/workflows/material-pipeline/generated-light-evidence-ledger.md`
  - `docs/workflows/material-pipeline/live-proof-packets/generate-spotlightmap-nextfloorlightmapxform.md`
- Safe reading to preserve:
  - `CASHotSpotAtlas` stays grounded first in the creator-side `UV1 -> HotSpotControl -> SimModifier` packet.
  - the local `TS4SimRipper` snapshot strengthens the downstream `SimModifier -> SMOD -> BGEO/DMap/BOND` chain, but does not supply a local `CASHotSpotAtlas` parser/use path.
  - `ShaderDayNightParameters` now has a stronger local helper packet with `occurrences = 5`, `LightsAnimLookupMap = 94`, and `samplerRevealMap = 32`, plus three visible-root anchors.
  - `GenerateSpotLightmap` now has a stronger local helper packet with `occurrences = 6` and `NextFloorLightMapXform = 14`, while the weaker `NextFloorLightMapXform = 3` carry-through remains secondary.
  - the next implementation-facing step is still Tier A:
    - revisit overlay/detail and broader `CAS/Sim` compositor authority under the `SimSkin` shell packet.

### Current Request Addendum (`2026-04-21` continued, Tier A overlay/detail precedence pass)

#### Problem

The next highest-priority character-side gap after the closed high-byte `BodyType` packet stack was no longer another family translation packet. The missing bridge was one stricter overlay/detail precedence packet that keeps ordinary low-value overlay rows and skintone-carried overlays above the mixed high-byte families in compositor reasoning.

#### Chosen Approach

- keep the packet external-first:
  - `CompositionMethod`
  - `SortLayer`
  - `CAS Parts` versus `Skin Tones`
  - `TONE` overlay structure
- use the local `TS4SimRipper` snapshot only as the structure packet for `CompositionMethod`, `SortLayer`, and skintone-carried overlay handling
- use direct shard-backed counts to compare:
  - low-value overlay/detail rows
  - mixed high-byte families
  - `0x6D` and `0x6F` cosmetic-heavy comparison subrows
- bind the result into one bounded Tier A packet instead of widening the queue back out to Tier B

#### Actions

- [x] Re-read the shell/compositor and overlay/detail docs after the high-byte family closure.
- [x] Re-open external creator-facing sources for `CompositionMethod`, `SortLayer`, `CAS Parts`, `Skin Tones`, and `TONE` overlays.
- [x] Re-open the local `TS4SimRipper` `PreviewControl.cs`, `TONE.cs`, and `SkinBlender.cs` packet for compositor structure.
- [x] Query the shard cache directly for low-value overlay rows versus the mixed high-byte families.
- [x] Create `tmp/overlay_detail_priority_snapshot_2026-04-21.json`.
- [x] Create `docs/workflows/material-pipeline/live-proof-packets/overlay-detail-priority-after-highbyte-stack.md`.
- [x] Rebind the new packet into:
  - `docs/workflows/material-pipeline/overlay-detail-family-authority-table.md`
  - `docs/workflows/material-pipeline/cas-sim-material-authority-matrix.md`
  - `docs/workflows/material-pipeline/skintone-and-overlay-compositor.md`
  - `docs/workflows/material-pipeline/p1-live-proof-queue.md`
  - `docs/workflows/material-pipeline/live-proof-packets/README.md`
  - `docs/workflows/material-pipeline/documentation-status-catalog.md`
  - `docs/planning/current-plan.md`

#### Restart Hints

- The new Tier A compositor-order packet is:
  - `docs/workflows/material-pipeline/live-proof-packets/overlay-detail-priority-after-highbyte-stack.md`
- The direct comparison artifact is:
  - `tmp/overlay_detail_priority_snapshot_2026-04-21.json`
- Safe reading to preserve:
  - the closed high-byte family stack narrows interpretation, but does not replace ordinary overlay/detail precedence anchors
  - low-value overlay/detail rows still provide the cleanest direct compositor anchors where pair patterns are stable
  - the `TONE` overlay branch stays separate from ordinary `CASPart` overlay/detail rows
  - `0x6D` and `0x6F` are now explicit comparison packets showing how mixed high-byte families can echo cosmetic lanes without replacing them
  - the next highest-priority sibling packet after this pass is `Hair` / `Accessory` / `Shoes` authority, not a return to Tier B by momentum

### Current Request Addendum (`2026-04-21` long-batch resume, continued again)

#### Problem

The `SimSkin` shell/compositor lane is still the highest unfinished character-side priority. `0x44`, `0x41`, `0x6D`, and `0x6F` are now closed as family packets, and the next unfinished mixed `BodyType` families are `0x52` and `0x80` before overlay/detail ordering is revisited.

#### Chosen Approach

- Rebuild context from the restart/process docs before touching any packet.
- Use external creator-facing references and local external snapshots first, with shard-backed corpus facts only as candidate-isolation evidence.
- Advance multiple bounded packets in sequence in one run:
  - tighten `0x52`
  - tighten `0x80`
- Fold only the proved parts back into the translation boundary, queue, matrix, registry, restart, and status layers.

#### Actions

- [x] Re-read the restart guide, current plan, source map, shader registry, edge-family matrix, queue, packet index, and status catalog for the discovery-phase working contract.
- [x] Reconfirm that the current safe next lane is the character-side `BodyType` high-byte translation track under the `SimSkin` authority packet.
- [x] Re-open the external-first source stack for `AdditionalTextureSpace` / `UniqueTextureSpace` and current `BodyType` vocabulary.
- [x] Tighten the `0x44` family into one bounded packet or boundary pass with explicit external anchors, local snapshot evidence, and safe reading.
- [x] Tighten the `0x41` family into the next bounded packet or boundary pass with the same trust split.
- [x] Tighten the `0x52` family into the next bounded packet or boundary pass with explicit external anchors, local snapshot evidence, and safe reading.
- [x] Tighten the `0x80` family into the next bounded packet or boundary pass with the same trust split or an honest weaker-boundary reading if direct external vocabulary still fails.
- [x] Rebind the proved result into:
  - `docs/workflows/material-pipeline/bodytype-translation-boundary.md`
  - `docs/workflows/material-pipeline/p1-live-proof-queue.md`
  - `docs/workflows/material-pipeline/edge-family-matrix.md`
  - `docs/workflows/material-pipeline/shader-family-registry.md`
  - `docs/workflows/material-pipeline/research-restart-guide.md`
  - `docs/workflows/material-pipeline/documentation-status-catalog.md`
- [x] Close the run with the restart-guide report split:
  - externally confirmed
  - local snapshots of external tooling
  - bounded synthesis
  - blockers
  - next highest-priority step

#### Restart Hints

- This run resumes from the existing character-side authority stack:
  - `docs/workflows/material-pipeline/live-proof-packets/simskin-body-head-shell-authority.md`
  - `docs/workflows/material-pipeline/body-head-shell-authority-table.md`
  - `docs/workflows/material-pipeline/compositionmethod-sortlayer-boundary.md`
  - `docs/workflows/material-pipeline/overlay-detail-family-authority-table.md`
  - `docs/workflows/material-pipeline/bodytype-translation-boundary.md`
- The next bounded families for this batch are:
  - `0x52`
  - `0x80`
- Preserve the trust boundary:
  - external creator-facing references and local external tooling snapshots define the meaning layer
  - shard-backed counts define only prevalence and candidate isolation
  - current repo code remains implementation boundary only
- Safe reading to preserve on resume:
  - `0x44` now has its own packet:
    - `docs/workflows/material-pipeline/live-proof-packets/bodytype-0x44-family-boundary.md`
  - `0x41` now also has its own packet:
    - `docs/workflows/material-pipeline/live-proof-packets/bodytype-0x41-family-boundary.md`
  - `0x6D` now also has its own packet:
    - `docs/workflows/material-pipeline/live-proof-packets/bodytype-0x6d-family-boundary.md`
  - `0x6F` now also has its own packet:
    - `docs/workflows/material-pipeline/live-proof-packets/bodytype-0x6f-family-boundary.md`
  - `0x41` now also freezes the first clothing-like `composition=32 | sort=65536` sub-lane inside the mixed family
  - `0x6D` is now closed as the strongest current counterexample to naive low-byte decoding
  - `0x6F` is now closed as the mixed head-decoration / horse-tail-adjacent special-content family packet
  - `0x52` is now closed as the concentrated bottom-heavy family with `BodyScarArmLeft` vocabulary overlap
  - `0x80` is now closed as the honest weaker sign-bit family packet with no direct external vocabulary anchor yet
  - the low byte is still not a safe standalone decoder for any of these high-byte families
  - the leading external hypothesis remains a secondary texture-space or `AdditionalTextureSpace` layer rather than a flat hidden-slot enum
  - the next packet should revisit overlay/detail priority against the now-frozen high-byte packet stack before reopening narrower `SimGlass`, `RefractionMap`, or pack-local transparent-object lanes

### Current Request Addendum

#### Problem

The finished character-side `CASPart -> GEOM -> shader family` census now makes `SimSkin` the dominant direct family floor, but the docs still lacked one restart-safe packet that converts that prevalence into body/head shell authority and skintone/compositor ordering guidance.

#### Chosen Approach

- Re-read the `SimSkin` family sheet, shell/compositor deep dives, queue, and local `TS4SimRipper` skintone snapshots.
- Create one bounded live-proof packet for `SimSkin` body/head shell authority.
- Sync that packet into the queue, matrix, restart, source-map, and status surfaces.

#### Actions

- [x] Re-read the current `SimSkin` family, shell-authority, and skintone/compositor docs.
- [x] Re-open the local `TS4SimRipper` `TONE.cs` and `SkinBlender.cs` snapshots as the current compositor-structure packet.
- [x] Create `docs/workflows/material-pipeline/live-proof-packets/simskin-body-head-shell-authority.md`.
- [x] Rebind the queue, matrix, restart, packet index, source map, and status catalog around that packet.
- [x] Build the first explicit body-shell versus head-shell authority table from the new packet.
- [x] Add one narrow boundary document for `CompositionMethod` and `SortLayer` so layer ordering stays separate from shell selection.
- [x] Extend the same explicit table style to one overlay/detail family.
- [x] Add one direct whole-index `sort_layer` census layer and record the current `CompositionMethod` parser/index gap.
- [x] Extend the parser/index layer so `CompositionMethod` can be counted directly alongside `sort_layer`.
- [x] Add the first direct whole-install `CompositionMethod` census plus `CompositionMethod + SortLayer` pair counts.
- [x] Rebind the compositor-side docs around the new direct `CompositionMethod` floor.
- [x] Backfill the shard set so `composition_method` becomes queryable in `cas_part_facts`.
- [x] Record the new cache-backed `CompositionMethod` state in the baseline, boundary, restart, and source-map layers.
- [x] Translate the first batch of largest unresolved `Body Type` buckets into one boundary document that separates direct enum matches from mixed high-bit buckets.
- [x] Record the repeated high-byte family structure for the largest unresolved `Body Type` values.
- [x] Record the first family-level profiles and priority order for `0x44`, `0x41`, `0x6D`, `0x6F`, `0x52`, and `0x80`.
- [x] Add the new external `AdditionalTextureSpace` lead and align it with the current high-byte families.
- [ ] Continue translating the remaining large mixed `Body Type` families before over-reading slot-local compositor patterns.

#### Restart Hints

- The new character-side shell packet is:
  - `docs/workflows/material-pipeline/live-proof-packets/simskin-body-head-shell-authority.md`
- The new companion docs that continue that packet are:
  - `docs/workflows/material-pipeline/body-head-shell-authority-table.md`
  - `docs/workflows/material-pipeline/compositionmethod-sortlayer-boundary.md`
  - `docs/workflows/material-pipeline/compositionmethod-census-baseline.md`
  - `docs/workflows/material-pipeline/overlay-detail-family-authority-table.md`
  - `docs/workflows/material-pipeline/sortlayer-census-baseline.md`
- Safe reading to preserve:
  - `SimSkin` now has both external family identity and direct package-derived prevalence strength
  - body shell stays the assembly anchor and head shell stays a mergeable sibling branch
  - skintone and overlay/detail logic stay in post-selection compositor authority, not alternate shell identity
  - `CompositionMethod` and `SortLayer` belong to layer ordering after shell selection, not to shell choice itself
  - overlay/detail families now also have their own explicit authority table instead of being only a prose boundary
  - `sort_layer` now has a direct whole-index count layer from `cas_part_facts`
  - `CompositionMethod` now also has a direct whole-install package census layer
  - `composition_method` is now queryable in `cas_part_facts` across all four shard databases
  - the new translation boundary is:
    - `docs/workflows/material-pipeline/bodytype-translation-boundary.md`
  - low-value rows like `10` and `28` now align directly with the external enum names `Earrings` and `FacialHair`
  - the largest rows such as `1140850688`, `1090519040`, and `1090519046` still behave as mixed high-bit buckets rather than ordinary slots
  - the unresolved space now also shows repeated high-byte families:
    - `0x44`
    - `0x41`
    - `0x52`
    - `0x6D`
    - `0x6F`
    - `0x80`
  - the low byte is not a safe standalone decoder for those families
  - the first safe family-level priority is now:
    - `0x44`
    - `0x41`
    - `0x6D`
    - `0x6F`
    - `0x52`
    - `0x80`
  - the leading external hypothesis for those families is now:
    - a secondary texture-space or `AdditionalTextureSpace` layer
    - not a flat hidden-slot enum
  - `composition=32 | sort=65536` is now a counted clothing-like compositor lane, not just a tooling suspicion
  - exact `MTNF` versus `CASP` versus explicit material-definition order is still open

#### Problem

The earlier census numbers were stale and incomplete. A previous "full scan" was still capped at `2000` package files and was being overread as if it were a whole-game corpus result. That is not acceptable for corpus-wide shader/material prioritization.

#### Chosen Approach

- Run a true full filesystem profile-index pass across all `.package` files under `C:\GAMES\The Sims 4`.
- Reconcile the resulting shard set instead of reading only `index.sqlite`.
- Replace the old partial census numbers in the corpus-wide census docs.
- Treat any scan-integrity mismatch explicitly as an open blocker rather than smoothing it over.

#### Actions

- [x] Count real `.package` files on disk before rerunning the scan.
- [x] Run `ProbeAsset --profile-index` with a high enough limit to include the full install.
- [x] Reconcile `index.sqlite` plus `index.shard*.sqlite` instead of reading only the main DB.
- [x] Compute fresh whole-corpus package/resource/asset totals from the full shard set.
- [x] Record the two currently missing `EP18` package paths as an explicit integrity gap.
- [x] Replace the old census baseline in the material docs.
- [ ] Add a direct whole-corpus family-count layer on top of the new full scan.
- [x] Add the first direct object-side shader-profile count layer on top of the new full scan.
- [x] Add the first direct graph-backed `Sim`-side carrier-count layer on top of the same full scan.
- [x] Add the broader direct whole-`CAS` slot/fact carrier-count layer on top of the same full scan.
- [x] Add the deeper direct `CAS`/`GEOM` linkage-count layer on top of the same full scan.
- [x] Add the first direct character-side `CASPart -> GEOM -> shader family` count layer on top of the same full scan.
- [x] Record the current `CASPart` parser/failure boundary and the residual `GEOM` key-index boundary as the current integrity blockers for the character-side census stack.
- [x] Rebind the priority, queue, restart, and status docs from the completed cross-package character-side family census.

#### Restart Hints

- The fresh full-scan log is:
  - `tmp/profile_index_fullscan_2026-04-20.log`
- The fresh shard set is:
  - `tmp/profile-index-cache/cache/index.sqlite`
  - `tmp/profile-index-cache/cache/index.shard01.sqlite`
  - `tmp/profile-index-cache/cache/index.shard02.sqlite`
  - `tmp/profile-index-cache/cache/index.shard03.sqlite`
- Current counted whole-corpus totals from the full shard set:
  - `indexed package paths = 4963`
  - `indexed resources = 4789589`
  - `indexed assets = 743150`
  - `asset-bearing package paths = 603`
  - `Cas = 530507`
  - `BuildBuy = 142941`
  - `General3D = 68158`
  - `Sim = 1544`
- Current integrity gap to preserve:
  - the scan selected `4965` filesystem package files, but only `4963` package rows persisted
  - the missing paths are:
    - `C:\GAMES\The Sims 4\EP18\ClientFullBuild0.package`
    - `C:\GAMES\The Sims 4\EP18\SimulationFullBuild0.package`
- Do not fall back to the older `1240 / 161303 / 1125911` live-cache layer as if it were the current whole-game census.
- The first direct object-side shader-profile census now lives in:
  - `docs/workflows/material-pipeline/matd-shader-census-baseline.md`
  - `tmp/matd_shader_census_fullscan.json`
- The first direct graph-backed character-side carrier census now lives in:
  - `docs/workflows/material-pipeline/sim-archetype-material-carrier-census.md`
  - `tmp/sim_material_carrier_census.json`
- The first broad whole-`CAS` slot/fact census now lives in:
  - `docs/workflows/material-pipeline/cas-carrier-census-baseline.md`
  - `tmp/cas_carrier_census_fullscan.json`
- The first direct package-derived `CASPart` linkage census now lives in:
  - `docs/workflows/material-pipeline/caspart-linkage-census-baseline.md`
  - `tmp/caspart_linkage_census_fullscan.json`
- The first direct package-derived character-side `CASPart -> GEOM -> shader family` census now lives in:
  - `docs/workflows/material-pipeline/caspart-geom-shader-census-baseline.md`
  - `tmp/caspart_geom_shader_census_fullscan.json`
- The resumable runner and safe-point root for that census are:
  - `tmp/caspart_geom_shader_census_resumable.ps1`
  - `tmp/caspart_geom_shader_census_run/`
- The current integrity ceilings for that character-side stack now also live in:
  - `docs/workflows/material-pipeline/caspart-parser-boundary.md`
  - `docs/workflows/material-pipeline/caspart-geom-resolution-boundary.md`
- Current direct `MATD` census totals to preserve:
  - `MaterialDefinitionResources = 28225`
  - `DecodedResources = 28201`
  - `EmptyResources = 24`
  - `Failures = 0`
- Current direct character-side `GEOM` family totals to preserve:
  - `RowsWithResolvedGeometryShader = 281271`
  - `RowsWithUnknownGeometryShader = 32`
  - `GeometryResolvedFromExternalPackage = 12911`
  - `SimSkin = 280983` across `401` packages by `CASPart` rows
  - `SimGlass = 6048` across `189` packages by `CASPart` rows
  - `SimSkin = 86697` across `147` packages by unique linked `GEOM`
  - `SimGlass = 645` across `47` packages by unique linked `GEOM`
- Safe reading to preserve:
  - this is a real package-derived object-side shader-profile count layer
  - it is not yet a whole-game family census
  - the new `Sim archetype` carrier census is the first direct character-side prevalence layer
  - the new whole-`CAS` census is a strong slot/fact prevalence layer, but not yet by itself a `GEOM`/material-linkage layer
  - the new `CASPart` linkage census is the first direct package-derived `GEOM`/texture/`region_map` linkage layer on the character side
  - the new `CASPart -> GEOM -> shader family` census is the completed current direct package-derived character-side family-count layer
  - its GEOM-side names must prefer the external `TS4SimRipper` enum packet over cross-domain `precomp` guesses
  - the all-zero `CAS` asset carrier booleans are still an index boundary, not a semantic result
  - the current deeper integrity blocker is now the large `CASPart` structured-parser gap:
    - `ParsedResources = 299028`
    - `TotalFailures = 230713`
  - a second direct-resolution boundary now also matters:
    - cross-package `GEOM` resolution is now already present through the shard set
    - the current residual geometry-resolution gap is `GeometryKeyNotIndexed = 531`
  - the current direct linkage floor was recovered through reflection scripts over the already-built assemblies because `dotnet build` for `ProbeAsset` is blocked by workload resolver errors in `tmp/probeasset_build_diag.txt`

#### Problem

The wide-priority companion existed, but it still mixed true counted coverage with weaker derived family hints. Without a separate census baseline, future restarts could still overread hint layers as real popularity measurements.

#### Chosen Approach

- Create one separate corpus-wide census baseline companion.
- Separate three things explicitly:
  - live index corpus totals
  - counted package-slice prevalence
  - derived family hints
- Sync that baseline into restart, queue, source-map, deep-dive index, and priority docs.

#### Actions

- [x] Inspect the live SQLite index for package, asset, and resource totals.
- [x] Re-open the existing package-derived family survey summaries.
- [x] Create one `corpus-wide-family-census-baseline.md` companion.
- [x] Sync that baseline into the wide-priority and restart layers.

#### Restart Hints

- The new census baseline is:
  - `docs/workflows/material-pipeline/corpus-wide-family-census-baseline.md`
- Safe reading to preserve:
  - whole-corpus totals are now counted from the live index
  - current family popularity for rows like `RefractionMap` and `SimGlass` is still only partially counted
  - package-derived `Build/Buy` survey counts are real, but still domain-limited
  - `precomp_sblk_inventory.json` remains a derived hint layer, not a direct census

#### Problem

The corpus-wide framing rule had been written into the restart contract, but it still did not exist as its own durable working companion. Without that, a later restart could still treat the rule as a one-off warning instead of as an operational ranking layer that sits above the queue.

#### Chosen Approach

- Create one dedicated whole-game family-priority companion.
- Base it on three bounded inputs:
  - externally confirmed family identity
  - local corpus-wide prevalence hints
  - implementation-spec leverage
- Sync it into the restart guide, shader registry, queue, source map, deep-dive index, and plan.

#### Actions

- [x] Re-open the registry, queue, source-map, and current local prevalence hints.
- [x] Create one dedicated `corpus-wide-family-priority.md` companion.
- [x] Sync that companion into the restart and navigation layers.

#### Restart Hints

- The new durable wide-priority companion is:
  - `docs/workflows/material-pipeline/corpus-wide-family-priority.md`
- Safe reading to preserve:
  - `SimSkin`/character foundation, object-side transparency, and CAS/Sim compositor authority stay above narrow pack-local lanes
  - `CASHotSpotAtlas`, `ShaderDayNightParameters`, and generated-light helpers remain whole-game rows with strong enough evidence to stay in the main queue
  - `RefractionMap` and `SimGlass` remain real rows, but should not dominate by recent route momentum alone

#### Problem

The research track had started to drift into pack-local priority, especially through repeated `EP10`-centric live-proof lanes. That was still acceptable as bounded evidence work, but it is not the real task goal. Without a correction in the foundational docs, future restarts could keep mistaking one convenient package slice for the main queue driver.

#### Chosen Approach

- Re-center the task framing on corpus-wide shader/material prioritization across the whole game.
- Freeze package-specific lanes as secondary validation/evidence layers only.
- Update the foundational restart, shared-guide, deep-dive index, queue, and plan wording so restarts rebuild the wide priority before choosing any narrow fixture route.

#### Actions

- [x] Re-read the foundational restart and queue docs that currently drive this research track.
- [x] Patch the restart contract so whole-game family priority outranks package-local convenience.
- [x] Patch the shared guide and deep-dive index so the same rule appears outside the restart doc.
- [x] Patch the live-proof queue so pack-specific routes are read as secondary validation layers rather than main-priority drivers.
- [x] Record the correction in the live plan so the change survives restarts.

#### Restart Hints

- Safe reading to preserve:
  - the task is about TS4 materials across the whole corpus, not about one pack such as `EP10`
  - main priority must be driven by whole-game family prevalence, rendering importance, external evidence strength, cross-domain coverage, and implementation-spec value
  - pack-specific lanes remain useful, but only as secondary validation/evidence layers after the wider family priority is already set
  - current `EP10` transparent-decor and refraction lanes stay bounded as already-exhausted or already-bounded evidence routes, not as default next work by themselves
  - the durable whole-game priority companion now lives in `docs/workflows/material-pipeline/corpus-wide-family-priority.md`

#### Problem

The transparent-object branch had already handed off from the exhausted top-anchor pair to `mirror`, but it still lacked one packet for the lower-anchor reopen results and one packet that formally marked the full `EP10` decor cluster as stalled. Without those, a restart could still reopen `mirror`, `lantern`, or `fishBowl` again or hesitate between “still active route” and “ready to widen”.

#### Chosen Approach

- Keep this as a real route-stall closure pass, not as a fake family-classification pass.
- Use the existing `ProbeAsset` binary to attempt direct reopen on `mirror`, `lantern`, and `fishBowl`.
- Create one packet freezing the lower-anchor negative reopen ceiling.
- Create one packet freezing the full-route stall and widening handoff.
- Sync both into the route, route-stall, window-heavy negative control, queue, matrix, restart guide, and plan docs.

#### Actions

- [x] Re-open the current transparent-object handoff packet, route-stall packet, negative-control packet, and the exact lower-anchor target list.
- [x] Run direct reopen attempts on `mirror`, `lantern`, and `fishBowl` through the existing `ProbeAsset` binary.
- [x] Create one lower-anchor negative-reopen packet.
- [x] Create one full-route stall packet.
- [x] Sync both packets into the supporting workflow and continuity docs.

#### Restart Hints

- The new packets are:
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-transparent-object-lower-anchor-negative-reopen.md`
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-transparent-object-full-route-stall.md`
- Safe reading to preserve:
  - `mirror`, `lantern`, and `fishBowl` now share the same first real reopen ceiling as the top-anchor pair
  - the full `EP10` transparent-decor cluster is now stalled at the present inspection layer
  - the next honest move is widening to the window-heavy negative control or another transparent-object slice, not retrying the same five roots

#### Problem

The transparent-object branch had already bounded the strongest peer anchor pair structurally, but it still lacked one packet for their first shared real reopen result and one formal handoff packet showing where the route goes next. Without those, a restart could still either retry `displayShelf`/`shopDisplayTileable` aimlessly or widen too early.

#### Chosen Approach

- Keep this as a real reopen boundary pass, not as a fake family-classification pass.
- Use the existing `ProbeAsset` binary to attempt direct reopen on `displayShelf` and `shopDisplayTileable`.
- Create one packet freezing their shared negative reopen ceiling.
- Create one packet freezing the post-top-anchor handoff to `mirror`.
- Sync both into the route, priority, survey-vs-reopen boundary, route-stall boundary, queue, matrix, packet index, and plan docs.

#### Actions

- [x] Re-open the current transparent-object route stack, target-priority packet, survey-vs-reopen packet, route-stall packet, and the exact `displayShelf` / `shopDisplayTileable` anchor files.
- [x] Run direct reopen attempts on `displayShelf` and `shopDisplayTileable` through the existing `ProbeAsset` binary.
- [x] Create one top-anchor negative-reopen packet.
- [x] Create one post-top-anchor handoff packet.
- [x] Sync both packets into the supporting workflow and continuity docs.

#### Restart Hints

- The new packets are:
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-transparent-object-top-anchor-negative-reopen.md`
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-transparent-object-post-top-anchor-handoff.md`
- Safe reading to preserve:
  - `displayShelf` and `shopDisplayTileable` now share the same first real reopen ceiling: `Build/Buy asset not found`
  - that does not stall the whole transparent-decor route
  - it does mean the next honest target inside the same route is now `mirror`

#### Problem

The transparent-object branch now had two strongest peer anchors, but it still lacked one hard boundary preventing premature widening to `mirror`, `lantern`, and `fishBowl`. Without that, a restart could still skip the strongest pair too early after one weak pass.

#### Chosen Approach

- Keep this as a top-tier exhaustion pass, not as a fake reopen or classification pass.
- Create one packet freezing the rule that both strongest anchors must be exhausted before weaker anchors are allowed to take over.
- Sync that boundary into the target-priority, queue, packet index, matrix, and plan docs.

#### Actions

- [x] Re-open the top-anchor tiebreak packet, the route-stall packet, the target-priority packet, and the current queue wording.
- [x] Create one top-anchor exhaustion-boundary packet.
- [x] Sync that packet into the supporting workflow and continuity docs.

#### Restart Hints

- The new packet is:
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-transparent-object-top-anchor-exhaustion-boundary.md`
- Safe reading to preserve:
  - the route must not widen below `displayShelf` and `shopDisplayTileable` until both are honestly exhausted
  - `mirror`, `lantern`, and `fishBowl` remain real targets, but only after the strongest peer pair is spent

#### Problem

The transparent-object branch now had one strongest first-target anchor, but the top of the route was still too easy to read as either arbitrary or semantically ranked. The docs still needed one peer anchor packet for `shopDisplayTileable` and one explicit tiebreak packet explaining why it remains second without implying weaker family relevance.

#### Chosen Approach

- Keep this as a strongest-anchor refinement pass, not as a fake reopen or family-classification pass.
- Create one packet freezing `shopDisplayTileable` as the second strongest exact survey-backed anchor.
- Create one packet freezing the current non-semantic tiebreak between `displayShelf` and `shopDisplayTileable`.
- Sync both into the route, priority, queue, packet index, and plan docs.

#### Actions

- [x] Re-open the `displayShelf` anchor packet, the target-priority packet, and the `shopDisplayTileable` survey/candidate-resolution slices.
- [x] Create one `shopDisplayTileable` anchor packet.
- [x] Create one top-anchor tiebreak packet.
- [x] Sync both packets into the supporting workflow and continuity docs.

#### Restart Hints

- The new packets are:
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-transparent-object-shopdisplay-anchor.md`
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-transparent-object-top-anchor-tiebreak.md`
- Safe reading to preserve:
  - `shopDisplayTileable` is now a peer strongest anchor, not just the next name in the list
  - `displayShelf -> shopDisplayTileable` is now an explicit structural tiebreak, not a semantic-family ranking

#### Problem

The transparent-decor branch already had route, priority, and checklist packets, but it still lacked one exact first-target identity anchor and one explicit ceiling between survey/candidate-resolution evidence and a true reopened fixture. Without those, the next batch could still overread route quality as near-fixture closure.

#### Chosen Approach

- Keep this as a pre-reopen anchor pass, not as a fake fixture pass.
- Create one packet freezing `displayShelf` as the strongest exact survey-backed first target.
- Create one packet freezing the survey-versus-reopen boundary for the whole transparent-decor cluster.
- Sync both into the route, packet index, queue, matrix, and plan docs.

#### Actions

- [x] Re-open the transparent-object route packet, target-priority packet, reopen checklist, and the EP10 survey/candidate-resolution artifacts.
- [x] Create one `displayShelf` anchor packet.
- [x] Create one survey-versus-reopen boundary packet.
- [x] Sync both packets into the supporting workflow and continuity docs.

#### Restart Hints

- The new packets are:
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-transparent-object-displayshelf-anchor.md`
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-transparent-object-survey-vs-reopen-boundary.md`
- Safe reading to preserve:
  - `displayShelf` is now the strongest exact survey-backed first target, not only a narrative preference
  - the transparent-decor cluster is still route-grade rather than fixture-grade until one real reopen fills the checklist

#### Problem

`0389...` was already the next clean refraction route, but the docs still lacked one explicit ceiling packet and one explicit exit/handoff rule. Without those, a restart could keep looping on route-only refraction wording even after the current evidence had stopped getting stronger.

#### Chosen Approach

- Keep this as an anti-loop and handoff pass, not a new refraction-semantics pass.
- Create one packet that freezes the current `0389...` no-upgrade ceiling against the named `lilyPad` floor.
- Create one packet that freezes the post-`0389` handoff boundary to the next unfinished family track.
- Sync both into the refraction live-proof packet, packet index, queue, matrix, restart guide, and plan docs.

#### Actions

- [x] Re-open the current `0389...` clean-route packet, identity-gap packet, floor-comparison packet, and route-order packet.
- [x] Create one `0389...` no-signal-upgrade packet.
- [x] Create one post-`0389` handoff-boundary packet.
- [x] Sync both packets into the supporting workflow and continuity docs.

#### Restart Hints

- The new packets are:
  - `docs/workflows/material-pipeline/live-proof-packets/refraction-0389-no-signal-upgrade.md`
  - `docs/workflows/material-pipeline/live-proof-packets/refraction-post-0389-handoff-boundary.md`
- Safe reading to preserve:
  - `0389...` is now bounded not only as a clean route, but also as a no-upgrade ceiling relative to `lilyPad`
  - if no stronger inspection layer appears, refraction should now hand off cleanly to the next unfinished family track instead of absorbing more route-only prose

#### Problem

`0389...` now has a clean-route baseline, but it was still too easy to overread it as almost the same thing as the named `lilyPad` fixture. The docs still needed one explicit identity-gap packet and one explicit floor-comparison packet.

#### Chosen Approach

- Keep this as a route-clarification pass, not a new refraction-semantics pass.
- Create one packet for the current `0389...` identity gap against named-fixture status.
- Create one packet comparing `0389...` to the current `lilyPad` floor.
- Sync both into the refraction live-proof packet, packet index, queue, matrix, restart guide, source-map layer, and plan docs.

#### Actions

- [x] Re-open the current `0389...` coverage packet, the current `lilyPad` floor packet, and the narrower `0124...` control packet.
- [x] Create one `0389...` identity-gap packet.
- [x] Create one `0389...` versus `lilyPad` floor-comparison packet.
- [x] Sync both packets into the supporting workflow and continuity docs.

#### Restart Hints

- The new packets are:
  - `docs/workflows/material-pipeline/live-proof-packets/refraction-0389-identity-gap.md`
  - `docs/workflows/material-pipeline/live-proof-packets/refraction-0389-vs-lilypad-floor.md`
- Safe reading to preserve:
  - `0389...` now has a stronger restart-safe packet than before, but it is still not a named object/material fixture
  - `0389...` currently matches the `lilyPad` projective floor shape, not the stronger named seam/identity packet

#### Problem

The post-`lilyPad` pivot is now in place, but the route order still depended too much on raw `tmp` reading. Without route-specific packets, a restart could still blur `0389...` and `0124...` back into one generic “other projective roots” bucket.

#### Chosen Approach

- Keep this as a route-baseline pass, not a new refraction-semantics pass.
- Create one packet for `0389...` as the next clean route baseline.
- Create one packet for `0124...` as the mixed/control floor.
- Sync both into the refraction live-proof packet, packet index, queue, matrix, restart guide, source-map layer, and plan docs.

#### Actions

- [x] Re-open the sampled coverage packet for `0389...` and the narrower probe packet for `0124...`.
- [x] Create one `0389...` clean-route baseline packet.
- [x] Create one `0124...` mixed/control floor packet.
- [x] Sync both packets into the supporting workflow and continuity docs.

#### Restart Hints

- The new packets are:
  - `docs/workflows/material-pipeline/live-proof-packets/refraction-0389-clean-route-baseline.md`
  - `docs/workflows/material-pipeline/live-proof-packets/refraction-0124-mixed-control-floor.md`
- Safe reading to preserve:
  - `0389A352F5EDFD45` is no longer only a promising line in sampled coverage; it is now the current clean-route baseline
  - `0124E3B8AC7BEE62` is no longer only a vague boundary note; it is now explicitly frozen as the mixed/control route

#### Problem

The named `lilyPad` refraction fixture is now honestly bounded, but the continuation path after that ceiling was still implicit. A restart could still drift back into deepening the same root or promote the noisier `0124...` control route too early.

#### Chosen Approach

- Keep this as a post-`lilyPad` route-pivot pass, not a new refraction-semantics pass.
- Create one packet that freezes `lilyPad` as a bounded floor/ceiling reference rather than the whole refraction track.
- Create one packet that fixes the next-route order after that pivot, with `0389...` as the next clean route and `0124...` as the mixed/control route.
- Sync both into the refraction live-proof packet, packet index, queue, matrix, source-map layer, and plan docs.

#### Actions

- [x] Re-open the restart stack, refraction packet stack, and local coverage artifacts around `00F643...`, `0124...`, and `0389...`.
- [x] Create one post-`lilyPad` pivot packet.
- [x] Create one next-route priority packet.
- [x] Sync both packets into the supporting workflow and continuity docs.

#### Restart Hints

- The new packets are:
  - `docs/workflows/material-pipeline/live-proof-packets/refraction-post-lilypad-pivot.md`
  - `docs/workflows/material-pipeline/live-proof-packets/refraction-next-route-priority.md`
- Safe reading to preserve:
  - `lilyPad -> 00F643...` remains the named refraction floor/ceiling reference
  - `0389A352F5EDFD45` is now the next clean route
  - `0124E3B8AC7BEE62` stays the mixed/control route unless `0389...` fails cleanly

#### Problem

The `lilyPad` seam now had a positive floor result, but the docs still did not freeze the equally important negative result: the current probe still does not surface direct family-local `RefractionMap`, `tex1`, or `samplerRefractionMap`. Without that, the fixture could keep absorbing effort indefinitely.

#### Chosen Approach

- Keep this as a ceiling/escalation pass, not a new semantics pass.
- Create one packet for the current no-direct-family-surface negative result.
- Create one escalation-boundary packet so `lilyPad` stops cleanly at the right point if no stronger family-local surfacing appears.
- Sync both into the refraction live-proof stack, packet index, queue, matrix, and plan docs.

#### Actions

- [x] Re-open the current `00F643` probe output and refraction packet stack.
- [x] Create one no-direct-family-surface packet for `lilyPad`.
- [x] Create one escalation-boundary packet for `lilyPad`.
- [x] Sync both packets into the supporting workflow and continuity docs.

#### Restart Hints

- The new packets are:
  - `docs/workflows/material-pipeline/live-proof-packets/refraction-lilypad-no-direct-family-surface.md`
  - `docs/workflows/material-pipeline/live-proof-packets/refraction-lilypad-escalation-boundary.md`
- Safe reading to preserve:
  - the current `lilyPad` probe still does not surface direct `RefractionMap`, `tex1`, or `samplerRefractionMap`
  - if repeated deeper passes keep yielding only the same projective/material floor, the fixture should remain a bounded floor/ceiling packet and not absorb the entire refraction track

#### Problem

The `lilyPad` refraction fixture now had inspection discipline, but the docs still lacked one honest current-result packet. A later restart could still talk only about “next inspection” even though the existing probe output already shows a direct embedded `MATD` floor and a stable `WorldToDepthMapSpaceMatrix` projective floor.

#### Chosen Approach

- Keep this as a fixture-result pass, not a new semantic-closure pass.
- Create one packet for the current direct embedded `MATD` floor on `lilyPad`.
- Create one packet for the current projective floor boundary on the same fixture.
- Sync both into the refraction live-proof stack, packet index, queue, matrix, `Build/Buy` authority matrix, and plan docs.

#### Actions

- [x] Re-open the current `00F643` probe output and the refraction/boundary packet stack.
- [x] Create one direct-`MATD` floor packet for `lilyPad`.
- [x] Create one projective-floor boundary packet for `lilyPad`.
- [x] Sync both packets into the supporting workflow and continuity docs.

#### Restart Hints

- The new packets are:
  - `docs/workflows/material-pipeline/live-proof-packets/refraction-lilypad-direct-matd-floor.md`
  - `docs/workflows/material-pipeline/live-proof-packets/refraction-lilypad-projective-floor-boundary.md`
- Safe reading to preserve:
  - the current `lilyPad` seam already proves a direct embedded `MATD` floor
  - the current surfaced family at that floor is still `WorldToDepthMapSpaceMatrix` / `ProjectiveMaterialDecodeStrategy`
  - that is still not direct `RefractionMap` slot closure

#### Problem

The named `lilyPad` refraction fixture now had an outcome ladder and checklist, but one ambiguity still remained at the next inspection step: the docs did not yet freeze how to record direct `MATD` versus meaningful `MTST`, and they still lacked one explicit guard against overreading adjacent projective helpers as direct refraction closure.

#### Chosen Approach

- Keep this as an inspection-discipline pass, not a new family-semantics pass.
- Create one `MATD`-versus-`MTST` boundary packet for the `lilyPad` seam.
- Create one adjacent-helper boundary packet for the same seam.
- Sync both into the refraction live-proof packet, packet index, queue, matrix, stateful-material seam, source-map layer, and plan docs.

#### Actions

- [x] Re-open the current refraction live-proof packet, bridge boundary, and `Build/Buy` material/state seam docs.
- [x] Create one refraction companion `MATD`-versus-`MTST` boundary packet.
- [x] Create one refraction adjacent-helper boundary packet.
- [x] Sync both packets into the supporting workflow and continuity docs.

#### Restart Hints

- The new packets are:
  - `docs/workflows/material-pipeline/live-proof-packets/refraction-companion-matd-vs-mtst-boundary.md`
  - `docs/workflows/material-pipeline/live-proof-packets/refraction-adjacent-helper-boundary.md`
- Safe reading to preserve:
  - the next `lilyPad` inspection must not assume `MTST` just because the broader `Build/Buy` chain allows it
  - adjacent projective helpers remain boundary evidence, not direct refraction-family closure

#### Problem

The named `lilyPad` refraction bridge fixture was now restart-safe, but the next companion-material inspection step still had no explicit outcome discipline. A later run could still skip from “valid bridge” to “almost closed refraction slot semantics” without recording what actually surfaced at the `MATD/MTST` seam.

#### Chosen Approach

- Keep this as an inspection-discipline pass, not a new semantic-closure pass.
- Create one outcome-ladder packet for the `lilyPad` companion-material seam.
- Create one evidence checklist packet for the same seam.
- Sync both into the refraction live-proof packet, packet index, queue, matrix, and plan docs.

#### Actions

- [x] Re-open the current refraction bridge packet, bridge boundary, and `Build/Buy` material/state seam docs.
- [x] Create one refraction companion-material outcome ladder.
- [x] Create one refraction companion-material checklist.
- [x] Sync both packets into the supporting workflow and continuity docs.

#### Restart Hints

- The new packets are:
  - `docs/workflows/material-pipeline/live-proof-packets/refraction-companion-material-outcome-ladder.md`
  - `docs/workflows/material-pipeline/live-proof-packets/refraction-companion-material-checklist.md`
- Safe reading to preserve:
  - the next `lilyPad` inspection must end as direct `MATD`, meaningful `MTST`, adjacent helper seam only, or still unresolved
  - no refraction fixture should jump from named bridge status straight to exact slot closure without the checklist being filled

#### Problem

The refraction branch already had a strong live-proof packet, but it still mixed three different claim levels too closely: external family identity, survey-level/local bridge evidence, and exact slot closure. That made the named lily-pad fixture easier to overread than it should be.

#### Chosen Approach

- Keep this as a trust-boundary and fixture-boundary pass, not a new slot-closure pass.
- Create one `RefractionMap` evidence ledger.
- Create one named bridge-fixture boundary companion for the lily-pad seam.
- Sync both into the family sheet, live-proof packet, registry, matrix, queue, source-map layer, and plan docs.

#### Actions

- [x] Re-open the current `RefractionMap` live-proof packet, umbrella family sheet, queue, matrix, and source-map notes.
- [x] Create one `RefractionMap` evidence ledger.
- [x] Create one named bridge-fixture boundary companion.
- [x] Sync both companions into the supporting workflow and continuity docs.

#### Restart Hints

- The new companions are:
  - `docs/workflows/material-pipeline/refraction-evidence-ledger.md`
  - `docs/workflows/material-pipeline/refraction-bridge-fixture-boundary.md`
- Safe reading to preserve:
  - external refraction family identity is now clearly separated from local survey and bridge-root evidence
  - `sculptFountainSurface3x3_EP10GENlilyPad -> 00F643B0FDD2F1F7` is a valid named inspection bridge, not a closed slot-semantics proof

#### Problem

The projection/reveal/lightmap umbrella was already narrowed semantically, but the docs still mixed external corroboration, local carry-through, and bounded synthesis for two unfinished rows: `ShaderDayNightParameters` and `GenerateSpotLightmap` / `NextFloorLightMapXform`. That made the family packet easy to overread and the umbrella sheet too easy to flatten.

#### Chosen Approach

- Keep this as a trust-boundary batch, not as a new live-fixture batch.
- Create one evidence ledger for `ShaderDayNightParameters`.
- Create one evidence ledger for `GenerateSpotLightmap` / `NextFloorLightMapXform`.
- Create one short boundary companion keeping refraction, reveal/day-night, and generated-light as parallel rows under the same umbrella.
- Sync all three into the relevant family sheets, registry, matrix, queue, source-map layer, and plan docs.

#### Actions

- [x] Re-open the current reveal/day-night sheet, generated-light sheet, umbrella packet, matrix, queue, and source-map notes.
- [x] Create one `ShaderDayNight` evidence ledger.
- [x] Create one generated-light evidence ledger.
- [x] Create one projection/reveal/generated-light boundary companion.
- [x] Sync all three companions into the supporting workflow and continuity docs.

#### Restart Hints

- The new companions are:
  - `docs/workflows/material-pipeline/shader-daynight-evidence-ledger.md`
  - `docs/workflows/material-pipeline/generated-light-evidence-ledger.md`
  - `docs/workflows/material-pipeline/projection-reveal-generated-light-boundary.md`
- Safe reading to preserve:
  - `ShaderDayNightParameters` now has explicit external-vs-local-vs-synthesis separation
  - `GenerateSpotLightmap` / `NextFloorLightMapXform` now has explicit external-vs-local-vs-synthesis separation
  - the umbrella `Projection / Reveal / Lightmap` packet should no longer blur refraction, reveal/day-night, and generated-light into one semantic row

#### Problem

The research process had drifted into user-driven micro-iterations where each small packet waited for another manual "continue". That is too fragile for a long-running knowledge-build task and too dependent on uninterrupted chat context.

#### Chosen Approach

- Make autonomous long batches the primary operating mode.
- Use the restart docs as the durable state instead of relying on conversational continuity.
- Keep heartbeat automation only as recovery insurance for interruptions, not as the main driver of work.

#### Actions

- [x] Re-open the restart and plan docs that define how this research track resumes.
- [x] Document long-batch execution as the default mode for normal runs.
- [x] Document recovery heartbeat behavior as fallback-only, not as the main cadence.

#### Restart Hints

- Primary mode is now "autonomous long batch".
- Recovery mode must rebuild context from:
  - `docs/workflows/material-pipeline/research-restart-guide.md`
  - `docs/planning/current-plan.md`
  - the queue / matrix / source-map stack
- The thread heartbeat named `TS4 Research Recovery` is recovery insurance only; normal runs should still keep going across multiple bounded packets before stopping.

#### Problem

The object-side transparency branch already had strong semantic splits and operational packets, but it still did not clearly separate direct external confirmation from local package evidence and from bounded synthesis. That made the branch harder to trust and harder to restart cleanly.

#### Chosen Approach

- Keep this as an evidence-labeling pass, not a new fixture packet.
- Create one object-transparency evidence ledger.
- Sync that ledger into the object family sheet, transparent-object signals/authority companions, source map, and plan docs.

#### Actions

- [x] Re-open the current object-side transparency family sheet, signal packet, authority companion, and source-map notes.
- [x] Create one evidence-ledger companion separating external confirmation, local package evidence, bounded synthesis, and open gaps.
- [x] Sync that ledger into the supporting workflow and reference docs.

#### Restart Hints

- The new companion is `docs/workflows/material-pipeline/object-transparency-evidence-ledger.md`.
- Safe reading to preserve:
  - externally confirmed means creator-facing or lineage sources directly support the branch
  - local package evidence is only for candidate selection and fixture routing
  - bounded synthesis is the current decision order, not quote-level proof

#### Problem

The character-transparency packet had become denser, but it still forced the reader to infer which points were externally confirmed and which points were only bounded synthesis. That made the packet look more speculative than it should.

#### Chosen Approach

- Keep this as an evidence-labeling pass, not a new semantic-family pass.
- Create one character-transparency evidence ledger.
- Sync that ledger into the active transparency companions and source-map layer.

#### Actions

- [x] Re-open the current character-transparency companions and the external source packet used in this pass.
- [x] Create one evidence-ledger companion separating external confirmation, local external snapshot, bounded synthesis, and open gaps.
- [x] Sync that ledger into the supporting workflow and reference docs.

#### Restart Hints

- The new companion is `docs/workflows/material-pipeline/character-transparency-evidence-ledger.md`.
- Safe reading to preserve:
  - `externally confirmed` means directly supported by external sources
  - `local snapshot of external tooling` means checked-in copies of outside tools, not repo truth
  - `bounded Codex synthesis` means a conclusion I assembled from the evidence and it should be read as inference, not quote-level proof

#### Problem

The new character-transparency order closed the main `SimGlass` versus generic alpha ambiguity, but one neighboring edge was still too easy to overread. A later restart could still pull `SimEyes` into the same closed order without actually having a comparably strong packet in hand.

#### Chosen Approach

- Keep this as a bounded open-edge pass, not a new family-closure pass.
- Create one character-transparency open-edge companion.
- Sync that companion into the registry, matrix, source map, open questions, and plan docs.

#### Actions

- [x] Re-open the current character-transparency order and check the local external snapshot for peer `SimAlphaBlended` / `SimEyes` branches.
- [x] Create one character-transparency open-edge companion.
- [x] Sync that companion into the supporting workflow and continuity docs.

#### Restart Hints

- The new companion is `docs/workflows/material-pipeline/character-transparency-open-edge.md`.
- Safe reading to preserve:
  - `SimGlass` still has the strongest packet
  - `SimAlphaBlended` remains preserved but weaker
  - `SimEyes` is still an unresolved neighboring family here, not a closed member of the same order

#### Problem

The new character-transparency boundary kept `SimGlass` and `SimAlphaBlended` apart, but a later restart could still read them as an unordered pair and fall back to vague "transparent sim family" wording. That still left too much room to skip from a named family packet straight to generic alpha language.

#### Chosen Approach

- Keep this as an external-first family-order pass, not a new live-asset pass.
- Create one `SimGlass` character-transparency order companion.
- Sync that companion into the family sheet, registry, edge-family matrix, queue, source map, open questions, and plan docs.

#### Actions

- [x] Re-open the current `SimGlass` character-transparency packet and the surrounding queue/matrix wording.
- [x] Create one `SimGlass` character-transparency order companion.
- [x] Sync that companion into the supporting workflow and continuity docs.

#### Restart Hints

- The new companion is `docs/workflows/material-pipeline/simglass-character-transparency-order.md`.
- Safe reading to preserve:
  - `SimGlass` currently has the strongest character-transparency packet
  - `SimAlphaBlended` remains a named secondary branch, not generic alpha noise
  - generic character alpha should stay provisional fallback wording only

#### Problem

The `SimGlass` branch already had stronger cross-domain continuity, but character-side transparency semantics were still too easy to overflatten into one generic alpha bucket. A later restart could still read `SimGlass` and `SimAlphaBlended` as basically the same thing just because both are transparency-capable names.

#### Chosen Approach

- Keep this as an external-first character-transparency pass, not a package-evidence pass.
- Create one `SimGlass` character-transparency boundary companion.
- Sync that companion into the family sheet, family registry, `CAS/Sim` matrix, shared guide, source map, open questions, and plan docs.

#### Actions

- [x] Re-open the current `SimGlass` family sheet, registry wording, and source-map evidence packet.
- [x] Create one `SimGlass` character-transparency boundary companion.
- [x] Sync that companion into the supporting workflow and continuity docs.

#### Restart Hints

- The new companion is `docs/workflows/material-pipeline/simglass-character-transparency-boundary.md`.
- Safe reading to preserve:
  - `SimGlass` is not just generic character alpha
  - `SimAlphaBlended` is also a named transparency-capable family, not just noise
  - current evidence is strong enough to keep those names separate in provenance, but not yet to claim full slot-level closure

#### Problem

The `Build/Buy SimGlass` branch already had an evidence-order companion, but the docs still did not freeze one stricter cross-domain rule high enough in the stack: a later restart could still read `Build/Buy` as almost a co-equal semantic home for `SimGlass`, instead of keeping `CAS/Sim` as the family home and `Build/Buy` as bounded carry-over evidence.

#### Chosen Approach

- Keep this as a cross-domain continuity pass, not a new live-proof packet pass.
- Create one `SimGlass` domain-home boundary companion.
- Sync that companion into the family sheet, `CAS/Sim` matrix, source map, shared guide, shader registry, and plan docs.

#### Actions

- [x] Re-open the current `SimGlass` family sheet, registry, shared guide, and source-map wording.
- [x] Create one `SimGlass` domain-home boundary companion.
- [x] Sync that companion into the supporting workflow and continuity docs.

#### Restart Hints

- The new companion is `docs/workflows/material-pipeline/simglass-domain-home-boundary.md`.
- Safe reading to preserve:
  - `CAS/Sim` is the current semantic home for `SimGlass`
  - `Build/Buy` is only a bounded carry-over evidence domain
  - one shared shader/material contract still applies after authoritative inputs are chosen

#### Problem

The `Build/Buy SimGlass` branch was already almost fully specified inside the live-proof packet stack, but that continuity still lived mostly below the family-sheet layer. Without one higher-level evidence-order companion, a future restart could still read `SimGlass` carry-over logic only through packet internals instead of from one stable summary.

#### Chosen Approach

- Keep this as a continuity-summary pass, not a new packet pass.
- Create one `Build/Buy SimGlass` evidence-order companion outside the packet stack.
- Sync that companion into the family sheet, source map, edge-family matrix, restart guide, and plan docs.

#### Actions

- [x] Re-open the `SimGlass` family sheet, source-map packet, and live-proof continuity.
- [x] Create one `Build/Buy SimGlass` evidence-order companion.
- [x] Sync that companion into the supporting workflow and continuity docs.

#### Restart Hints

- The new companion is `docs/workflows/material-pipeline/simglass-buildbuy-evidence-order.md`.
- Safe reading to preserve:
  - survey keeps the branch alive
  - route ranks the next fixture
  - reopen decides the branch
  - family-sheet continuity for `SimGlass` no longer depends only on packet-stack archaeology

#### Problem

The `SimGlass` branch now had strict win/loss/verdict rules, but it still lacked one explicit handling layer for the middle of the ladder:
- how to document a real provisional `SimGlass` candidate without fuzzy near-win language
- how to resolve mixed `SimGlass` versus object-side signals without borrowing only the generic transparent-object tie-break packet

#### Chosen Approach

- Keep this as a branch-middle-state pass, not a new route pass.
- Create one provisional-candidate checklist packet and one `SimGlass`-specific mixed-signal packet.
- Sync both into queue, matrix, restart, open-question, packet index, and plan docs.

#### Actions

- [x] Re-open the current `SimGlass` outcome ladder, winning-signal packet, and mixed-signal context.
- [x] Create one provisional-candidate checklist packet for `Build/Buy SimGlass`.
- [x] Create one mixed-signal resolution packet for `Build/Buy SimGlass`.
- [x] Sync both packets into the supporting workflow and continuity docs.

#### Restart Hints

- The new packets are:
  - `docs/workflows/material-pipeline/live-proof-packets/simglass-buildbuy-provisional-candidate-checklist.md`
  - `docs/workflows/material-pipeline/live-proof-packets/simglass-buildbuy-mixed-signal-resolution.md`
- Safe reading to preserve:
  - provisional `SimGlass` is now a bounded non-win state with explicit blockers
  - mixed `SimGlass` cases now have a branch-specific tie-break, not only the generic transparent-object one
  - reopened cases should no longer drift into fuzzy “almost `SimGlass`” wording

#### Problem

The `SimGlass` branch now had explicit ceiling, gate, disqualifiers, and win-recording burden, but one ambiguity still remained on the positive side: a later run could still let `SimGlass` survive by pure elimination, and it could still report reopened cases with fuzzy verdicts like “almost `SimGlass`” or “probably `SimGlass`”. That was too loose for the first real branch decision.

#### Chosen Approach

- Keep this as a denser branch-decision pass, not a new route pass.
- Create one positive-signals packet and one outcome-ladder packet for `Build/Buy SimGlass`.
- Sync both into queue, matrix, restart, open-question, packet index, and plan docs.

#### Actions

- [x] Re-open the current `SimGlass` family sheet, baseline packet, and mixed-signal context.
- [x] Create one positive-signals packet for `Build/Buy SimGlass`.
- [x] Create one outcome-ladder packet for `Build/Buy SimGlass`.
- [x] Sync both packets into the supporting workflow and continuity docs.

#### Restart Hints

- The new packets are:
  - `docs/workflows/material-pipeline/live-proof-packets/simglass-buildbuy-winning-signals.md`
  - `docs/workflows/material-pipeline/live-proof-packets/simglass-buildbuy-outcome-ladder.md`
- Safe reading to preserve:
  - `SimGlass` now has a positive burden, not only a negative one
  - `SimGlass` cannot win by elimination alone
  - reopened cases should now end only as a stronger object-side win, generic transparent provisional boundary, provisional `SimGlass` candidate, or winning `SimGlass` fixture

#### Problem

The `SimGlass` branch now has an evidence ceiling and a promotion gate, but the next real reopen could still be documented too loosely in either direction:
- a weak case could linger as a fuzzy near-win instead of a clean disqualification
- a real win could be recorded with only a generic transparent-object checklist instead of a branch-specific `SimGlass` record

#### Chosen Approach

- Keep this as a denser branch-operational pass, not a new search-route pass.
- Create one disqualifier packet and one winning-fixture checklist packet for `Build/Buy SimGlass`.
- Sync both into queue, matrix, restart, open-question, packet index, and plan docs.

#### Actions

- [x] Re-open the current `SimGlass` promotion gate and transparent-object operational companions.
- [x] Create one disqualifier packet for `Build/Buy SimGlass`.
- [x] Create one winning-fixture checklist packet for `Build/Buy SimGlass`.
- [x] Sync both packets into the supporting workflow and continuity docs.

#### Restart Hints

- The new packets are:
  - `docs/workflows/material-pipeline/live-proof-packets/simglass-buildbuy-disqualifiers.md`
  - `docs/workflows/material-pipeline/live-proof-packets/simglass-buildbuy-winning-fixture-checklist.md`
- Safe reading to preserve:
  - stronger object-side transparent branches now produce clean `SimGlass` losses
  - the first valid `Build/Buy SimGlass` win now has a branch-specific checklist, not only a generic transparent-object record
  - weak reopened transparent objects should now end as explicit disqualifications or provisional transparent-object results, not as fuzzy `SimGlass` near-wins

#### Problem

The new `SimGlass` evidence-limit packet now stops summary-grade overread, but it still left one operational ambiguity: the next successful reopen could still be overpromoted just because it survives classification longer than the others. Without an explicit winning-branch rule, a future run could still turn “reopened transparent object with weak `SimGlass` plausibility” into “first `Build/Buy SimGlass` fixture”.

#### Chosen Approach

- Keep this as a promotion-gate pass, not a new search-route pass.
- Create one `SimGlass` `Build/Buy` promotion-gate packet.
- Sync that gate into queue, matrix, restart, open-question, packet index, and plan docs.

#### Actions

- [x] Re-open the current `SimGlass` evidence-limit, route, and transparent-object classification docs.
- [x] Create one winning-branch promotion-gate packet for `Build/Buy SimGlass`.
- [x] Sync that gate into the supporting workflow and continuity docs.

#### Restart Hints

- The new packet is `docs/workflows/material-pipeline/live-proof-packets/simglass-buildbuy-promotion-gate.md`.
- Safe reading to preserve:
  - the first reopened transparent object is not enough
  - `SimGlass` is the last surviving named branch, not the default label
  - the first valid `Build/Buy SimGlass` promotion must explicitly beat object-glass, threshold/cutout, and `AlphaBlended`

#### Problem

The `SimGlass` branch already had a real external identity and a narrowed `Build/Buy` search route, but it still lacked one explicit ceiling on how the current local `Build/Buy` evidence may be read. Without that limit, a later run could quietly overpromote `tmp/probe_all_buildbuy_summary_full.json` with `"SimGlass": 5` or the transparent-decor anchor cluster into near-proof instead of treating them as summary-grade route evidence.

#### Chosen Approach

- Keep this as an evidence-limit pass, not a new family-semantics pass.
- Create one `SimGlass` `Build/Buy` evidence-limit packet.
- Sync that ceiling into the queue, edge-family matrix, restart guide, open-question wording, packet index, and plan.

#### Actions

- [x] Re-open the current `SimGlass` baseline, route, queue, and restart docs.
- [x] Create one `Build/Buy` evidence-limit packet for `SimGlass`.
- [x] Sync that ceiling into the supporting workflow and continuity docs.

#### Restart Hints

- The new packet is `docs/workflows/material-pipeline/live-proof-packets/simglass-buildbuy-evidence-limit.md`.
- Safe reading to preserve:
  - `SimGlass = 5` in the Build/Buy survey keeps the branch alive
  - it does not yet prove a `Build/Buy SimGlass` fixture
  - it does not outrank the object-side transparent split
  - only a reopenable classified fixture may promote `Build/Buy` content back under the `SimGlass` row

#### Problem

The transparent-object authority companion now fixes where family choice sits in the object-side chain, but the branch still lacked one explicit fallback order for weak or contradictory reopened evidence. Without that, a later run could still degrade too quickly to generic transparency or bounce too early into `SimGlass`.

#### Chosen Approach

- Keep this as a fallback-order pass, not a new route pass.
- Create one transparent-object fallback ladder companion.
- Sync that ladder into authority, queue, restart, source-map, open-question, and plan docs.

#### Actions

- [x] Re-open the transparent-object authority and signal docs.
- [x] Create one fallback ladder companion.
- [x] Sync that ladder into the supporting workflow and reference docs.

#### Restart Hints

- The new companion is `docs/workflows/material-pipeline/buildbuy-transparent-object-fallback-ladder.md`.
- Safe reading to preserve:
  - do not drop to generic transparency too early
  - do not jump from weak object-side evidence to `SimGlass`
  - degrade through the documented transparent-object branches first

#### Problem

The transparent-object branch is now operationally complete on the packet side, but its authority reading still remained distributed across the main Build/Buy matrix and several live-proof packets. That was workable, but not restart-efficient: the next real fixture pass still had to reconstruct where transparent-family choice actually sits inside the object-side authority chain.

#### Chosen Approach

- Keep this as an authority-companion pass, not a new packet-family pass.
- Create one transparent-object authority-order companion.
- Sync that companion into the matrix, queue, restart guide, workflow index, and plan docs.

#### Actions

- [x] Re-open the Build/Buy matrix and the current transparent-object packet stack.
- [x] Create one transparent-object authority-order companion.
- [x] Sync that authority summary into the relevant workflow and restart docs.

#### Restart Hints

- The new companion is `docs/workflows/material-pipeline/buildbuy-transparent-object-authority-order.md`.
- Safe reading to preserve:
  - transparent-family choice happens after object-side reopen and material-candidate inspection
  - transparent-family choice happens before stable fixture promotion
  - it is an authority step, not a renderer-specific branch

#### Problem

The transparent-object branch now has a full path toward a stable fixture, but it still lacked one explicit exit condition for the primary route itself. Without that, the next run could widen back out to windows too early or, наоборот, оставаться в decor-cluster слишком долго без формального stop rule.

#### Chosen Approach

- Keep this as a route-exit packet, not a new classification packet.
- Freeze one explicit stall boundary for the transparent-decor route.
- Sync that boundary into queue, restart guide, packet index, and plan docs.

#### Actions

- [x] Re-open the current route, lifecycle, and negative-control packets.
- [x] Create one route-stall boundary packet.
- [x] Sync the stall rule into queue, restart guide, packet index, and current plan.

#### Restart Hints

- The new packet is `docs/workflows/material-pipeline/live-proof-packets/buildbuy-transparent-object-route-stall-boundary.md`.
- Safe reading to preserve:
  - one failed reopen is not a stalled route
  - the route stalls only after the full prioritized cluster is exhausted without a stable or still-promising provisional fixture

#### Problem

The transparent-object branch is now nearly fully operationalized, but one small lifecycle ambiguity remained: the docs still described route, reopen, checklist, and promotion, yet they did not name the intermediate states a candidate passes through. That left a small risk that future runs would skip a state conceptually even while following the right files.

#### Chosen Approach

- Keep this as a lifecycle packet, not a new semantic packet.
- Define one candidate-state ladder from `search anchor` to `stable fixture`.
- Sync that ladder into queue, packet index, restart, matrix, and plan docs.

#### Actions

- [x] Re-open the current transparent-object operational packets.
- [x] Create one candidate-state ladder packet.
- [x] Sync the lifecycle ladder into queue, restart guide, packet index, matrix, and current plan.

#### Restart Hints

- The new packet is `docs/workflows/material-pipeline/live-proof-packets/buildbuy-transparent-object-candidate-state-ladder.md`.
- Safe reading to preserve:
  - do not jump from search anchor straight to stable fixture
  - move through reopen, classification, provisional fixture, then promotion

#### Problem

The transparent-object route is now almost fully structured, but the next real reopen could still fail in two practical ways:
- it might surface mixed signals
- or it might be documented too loosely to stay restart-safe

That makes the remaining bounded gap operational rather than semantic.

#### Chosen Approach

- Keep this as a denser operational pass inside the same transparent-object rubric.
- Add one mixed-signal resolution packet and one reopen-checklist packet.
- Sync both into queue, restart, packet index, matrix, authority, source-map, open-question, and plan docs.

#### Actions

- [x] Re-open the signal-level transparent-object docs and current promotion boundary.
- [x] Create one mixed-signal resolution packet.
- [x] Create one reopen-checklist packet.
- [x] Sync both packets into the supporting docs so the next reopen can be documented decision-grade and restart-safe.

#### Restart Hints

- The new packets are:
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-transparent-object-mixed-signal-resolution.md`
  - `docs/workflows/material-pipeline/live-proof-packets/buildbuy-transparent-object-reopen-checklist.md`
- Safe reading to preserve:
  - mixed signals do not justify falling back to “generic transparency”
  - the first reopened fixture must record both winning and losing signals
  - the first reopened fixture now has a minimum evidence checklist before promotion

#### Problem

The transparent-object route now has a reopen threshold, but the current docs still spread the actual classification signals across the family sheet and packet notes. That is enough to continue, but still too loose for an intensive pass: the next reopen needs one explicit decision table saying which externally backed signals promote the fixture into object-glass, threshold/cutout, `AlphaBlended`, or only last-choice `SimGlass`.

#### Chosen Approach

- Keep this as a signal-layer pass, not a new route pass.
- Create one transparent-object classification-signal companion.
- Sync that signal order into authority, queue, restart, source-map, open-question, and matrix docs.

#### Actions

- [x] Re-open the object-transparent family sheet and current fixture-promotion packet.
- [x] Create one signal-level companion doc for transparent-object classification.
- [x] Sync the decision order into queue, matrix, authority, restart, source-map, open-question, and plan docs.

#### Restart Hints

- The new companion is `docs/workflows/material-pipeline/buildbuy-transparent-object-classification-signals.md`.
- Safe reading to preserve:
  - explicit object-glass signals outrank generic transparent naming
  - threshold/cutout signals outrank generic “looks like glass”
  - explicit `AlphaBlended` outranks generic alpha interpretation
  - `SimGlass` is currently last-choice for `Build/Buy` transparent fixtures

#### Problem

The transparent-object rubric now has a route, an order, and a negative control, but it still lacked one explicit threshold for when a reopened candidate stops being only route evidence and becomes the first stable transparent-object fixture. Without that, the next run could still overpromote a weak reopen or keep a strong reopen in limbo.

#### Chosen Approach

- Keep this as a fixture-promotion packet, not a new semantic packet.
- Freeze the minimum promotion threshold for the first stable transparent-object fixture.
- Sync that threshold into queue, restart, packet index, and matrix continuity.

#### Actions

- [x] Re-open the transparent-object route, target-priority, and classification packets.
- [x] Create one fixture-promotion boundary packet.
- [x] Sync that boundary into queue, matrix, packet index, restart guide, and current plan.

#### Restart Hints

- The new packet is `docs/workflows/material-pipeline/live-proof-packets/buildbuy-transparent-object-fixture-promotion-boundary.md`.
- Safe reading to preserve:
  - stable reopen alone is not enough
  - classification alone is not enough
  - the first stable fixture must satisfy both reopen stability and transparent-family classification

#### Problem

The transparent-object route now has a semantic split, a primary route, and a negative control, but its internal reopen order is still only described informally inside longer packets. That weakens restart continuity because the next run still has to re-read the route packet to reconstruct why `displayShelf` should be tried before `mirror` or `fishBowl`.

#### Chosen Approach

- Keep this as a narrow route-order packet, not a new semantic packet.
- Freeze one restart-safe internal order for the transparent-decor cluster.
- Sync queue, restart guide, and packet index so the `Build/Buy` transparent-object rubric gets a concrete completeness increase.

#### Actions

- [x] Re-open the transparent-decor route packet and extract the current structural ranking.
- [x] Create one target-priority packet for the cluster.
- [x] Sync that order into queue, restart guide, packet index, and current plan.

#### Restart Hints

- The new packet is `docs/workflows/material-pipeline/live-proof-packets/buildbuy-transparent-object-target-priority.md`.
- Safe reading to preserve:
  - this is a structural reopen-order packet only
  - classification still happens after reopen
  - current order is `displayShelf -> shopDisplayTileable -> mirror -> lantern -> fishBowl`

#### Problem

The transparent-object route is now separated from `SimGlass`, but its negative side is still implicit. Without one explicit window-heavy negative-control packet, a future restart can still waste the next pass on repeated `glass` names before the stronger transparent-decor route is exhausted.

#### Chosen Approach

- Keep this as a route-quality packet inside the transparent-object rubric.
- Freeze the old window-heavy sweep as a lower-priority negative control.
- Sync queue and restart wording so the transparent-decor route gains a real completeness increase, not just another link.

#### Actions

- [x] Re-open the transparent-decor route and older window-heavy wording.
- [x] Create one negative-control packet for the window-heavy transparent sweep.
- [x] Fold that boundary into queue, matrix, packet index, restart guide, and plan.

#### Restart Hints

- The new packet is `docs/workflows/material-pipeline/live-proof-packets/buildbuy-window-heavy-transparent-negative-control.md`.
- Safe reading to preserve:
  - repeated `glass` naming is not enough to outrank the transparent-decor route
  - windows stay a lower-priority transparent-object path until the decor route stalls

#### Problem

The transparent-object classification boundary is now explicit, but the working queue still mostly reads as if the transparent-decor cluster belongs inside the `SimGlass` rubric. That makes progress harder to read and makes restart priority blur the transparent-object branch back into the character-glass branch.

#### Chosen Approach

- Keep this as a queue and packet-routing pass, not a new semantic pass.
- Promote the `EP10` transparent-decor cluster into its own transparent-object live-proof route.
- Update queue, matrix, restart, and packet index so progress can be tracked under the refined rubric directly.

#### Actions

- [x] Re-open the current transparent-object boundary and transparent-decor cluster docs.
- [x] Create one live-proof route packet for the `Build/Buy` transparent-decor branch.
- [x] Split queue and matrix continuity so transparent-object route progress is visible separately from `SimGlass`.

#### Restart Hints

- The new packet is `docs/workflows/material-pipeline/live-proof-packets/buildbuy-transparent-decor-route.md`.
- Safe reading to preserve:
  - the cluster is the strongest transparent-object route
  - it is not yet classified
  - only after reopen and classification should it stay under object-side transparency or move back under `SimGlass`

#### Problem

The new object-transparent family sheet fixes the semantic split, but the existing transparent-decor route is still easy to resume incorrectly. Without one explicit classification-boundary packet, a future run could still reopen `displayShelf`, `mirror`, or `fishBowl` and immediately file it under `SimGlass` before checking object-side glass/transparency first.

#### Chosen Approach

- Keep this as a live-proof continuity packet, not a semantic re-derivation.
- Reuse the already documented external split as the truth layer.
- Freeze one honest operational rule:
  - the `EP10` transparent-decor cluster remains the best transparent-object route
  - but it is now classification-neutral until a reopened fixture is checked against object-side glass/transparency
- Sync only the docs that control restart behavior and packet routing.

#### Actions

- [x] Re-open the current transparent-decor route packet and new object-transparent family sheet.
- [x] Create one live-proof packet that freezes the transparent-object classification boundary.
- [x] Sync that boundary into the packet index, edge-family matrix, queue, restart guide, and current plan.

#### Restart Hints

- The new packet is `docs/workflows/material-pipeline/live-proof-packets/buildbuy-transparent-object-classification-boundary.md`.
- Safe reading to preserve:
  - the `EP10` transparent-decor cluster is still a good route
  - it is no longer safe to treat that route as implicitly `SimGlass`
  - the first reopened fixture must be classified against object-side glass/transparency first

#### Problem

The current `SimGlass` route work is still easy to misread because the docs had a stronger local search boundary than an external semantic boundary. The next bounded gap is external-first: object-side glass and object-side transparency need their own packet so the project does not reuse character-side `SimGlass` as a generic name for transparent `Build/Buy` content.

#### Chosen Approach

- Keep this as a family-semantics packet, not a local-fixture overclaim.
- Use external creator-facing sources and Sims-lineage shader vocabulary as the truth layer.
- Freeze one honest distinction:
  - `GlassForObjectsTranslucent` is a real object-side glass family
  - threshold/cutout transparency via `AlphaMap` plus `AlphaMaskThreshold` is a separate path
  - `AlphaBlended` is a separate blended object-transparency path
  - none of those should be silently relabeled as character-side `SimGlass`
- Sync only the docs that need the narrower semantic split for restart continuity.

#### Actions

- [x] Re-open the family registry and existing `SimGlass` documentation context.
- [x] Pull external creator-facing object-glass and object-transparency sources.
- [x] Create one external-first family sheet for object glass and transparency.
- [x] Sync that split into the registry, Build/Buy authority matrix, source map, open questions, and current plan.

#### Restart Hints

- The new semantic packet is `docs/workflows/material-pipeline/family-sheets/object-glass-and-transparency.md`.
- Safe reading to preserve:
  - `SimGlass` is the narrow character-side family
  - `GlassForObjectsTranslucent` is the stronger current semantic home for object glass
  - threshold/cutout transparency and `AlphaBlended` remain separate object-side branches
- Use local package survey only to choose the next object fixture after this, not to derive the semantic split itself.

#### Problem

The next bounded evidence gap after the `MTST` structural packet is not another state packet. It is the first stronger `SimGlass` object-side route: the current docs already had a transparent-decor cluster, but they still did not freeze why that cluster is better than the earlier window-heavy sweep. Without that, a future run still has to rediscover from raw survey files that some transformed roots preserve full companion bundles instead of only promising names.

#### Chosen Approach

- Keep this as a narrow `SimGlass` route packet, not a live-fixture overclaim.
- Use only existing local survey and candidate-resolution artifacts.
- Promote one honest structural claim:
  - the `EP10` transparent-decor cluster is stronger than the window-heavy packet because several roots preserve repeated transformed companion bundles
  - direct reopen still fails, so this remains a route packet rather than a live closure
- Sync only the docs that need the narrower route ordering for restart continuity.

#### Actions

- [x] Re-open the `SimGlass` baseline packet and current `EP10` survey artifacts.
- [x] Extract the transformed companion-bundle structure for the transparent-decor cluster.
- [x] Create one live-proof packet for the narrowed `SimGlass` transparent-decor route.
- [x] Sync that packet into the edge-family matrix, queue, open questions, restart notes, and current plan.

#### Restart Hints

- Use `tmp/probe_ep10_buildbuy_candidate_resolution_full.json` for transformed root plus companion-type evidence.
- Use `tmp/probe_ep10_buildbuy_identity_survey_full.json` for the repeated object rows and the contrast with the window-heavy packet.
- Use `tmp/probe_all_buildbuy.txt` only to confirm root-list presence, not as family truth.
- Safe reading to preserve: companion-bundle integrity now ranks the next `SimGlass` targets better than obvious `glass` naming.
- The new packet is `docs/workflows/material-pipeline/live-proof-packets/simglass-ep10-transparent-decor-route.md`.

#### Problem

The user explicitly parked the status-catalog/report-shape thread. The next bounded documentation gap is back on the evidence side: the `MTST` branch now has a default-state floor and a portable-state delta packet, but it still lacks one restart-safe note that freezes the repeated selector structure itself. Without that, a future run still has to rediscover from raw probes that the same `stateHash` values and per-state `MATD` mappings repeat structurally across fixtures.

#### Chosen Approach

- Keep this as a narrow live-proof documentation packet, not a reporting or navigation pass.
- Use only the already captured local probe artifacts for `002211...` and `0577...`.
- Promote one honest structural claim:
  - repeated `stateHash -> MATD` mapping is stable enough to preserve
  - the paired `unknown0=0x00000000` versus `0xC3867C32` split in `002211...` is structurally real
  - exact state semantics still stay open
- Sync only the docs that need this new boundary for restart continuity.

#### Actions

- [x] Re-open the two current `MTST` fixture artifacts and extract the repeated selector patterns.
- [x] Create one live-proof packet for the structural `MTST` selector boundary.
- [x] Sync that packet into the Build/Buy seam, Build/Buy matrix, packet index, queue, open questions, and restart notes.
- [x] Fold the narrower continuation hints back into the current plan.

#### Restart Hints

- Use `tmp/probe_002211_after.txt` for the repeated five-state lattice and paired `unknown0` split.
- Use `tmp/probe_0577_after_heuristic_filter.txt` for the stronger two-state portable material case and the shared `0xF4BD1CE9` selector.
- Safe reading to preserve: structural selector behavior is now proved; selector semantics are not.
- The new packet is `docs/workflows/material-pipeline/live-proof-packets/buildbuy-mtst-state-selector-structure.md`.
- The next `MTST` continuation after this packet should be named swatch/object identity or clearer runtime-state semantics, not another re-proof of repeated selector hashes.

#### Problem

This earlier status-catalog packet is currently parked after the first pass. Its continuity notes are kept below so the work is not lost, but it is not the active direction for the current run.

#### Chosen Approach

- Keep this as a documentation-structure packet, not a new evidence packet.
- Reuse only status signals already declared inside the docs themselves.
- Where a doc does not declare a percentage/status block, mark it honestly as `navigation-only`, `tracking-only`, or `source-layer` instead of inventing progress numbers.
- Add one durable status catalog for the whole material-pipeline documentation set, then link it from the existing hubs.

#### Actions

- [x] Re-open the current material-pipeline doc tree and collect all current status-bearing docs.
- [x] Create one status-indexed documentation catalog for the whole material-pipeline track.
- [x] Link that catalog from the material-pipeline hubs and repo knowledge map.
- [x] Fold the new reporting expectation back into the restart-facing docs and current plan.

#### Restart Hints

- The catalog should cover the whole external-first material-doc stack, not only the docs touched in the latest packet.
- Use explicit scope-status blocks where the doc already has them.
- For `README` / map / plan / source docs without a scope block, preserve honest labels like `navigation-only`, `tracking-only`, and `source-layer`.
- The intended catalog home is `docs/workflows/material-pipeline/documentation-status-catalog.md`.
- The restart guide now also carries the expanded full-catalog report shape, so future runs should not fall back to change-only status snippets.

#### Problem

The `Build/Buy MTST Default-State Boundary` packet proves one model-rooted stateful-material case, but it still stops short of the next stronger closure. The remaining bounded gap is a stronger stateful fixture: either a named swatch-level `MaterialVariant` case or a texture-bearing/runtime-state case that shows `MTST` changing portable shader properties and not only non-portable control properties.

#### Chosen Approach

- Keep this as a narrow continuation packet, not a broad new matrix.
- Search local probe artifacts first, because the next step is candidate isolation and fixture promotion rather than new external archaeology.
- Prefer a fixture that improves one of two axes:
  - exact object identity through `COBJ/OBJD` or swatch linkage
  - stronger material-state differentiation than the current default-state-only boundary
- The current best candidate is now `EP10\\ClientFullBuild0.package | Build/Buy Model 05773EECEE557829`, because its probe artifact shows `source=MaterialSet`, `textures=2`, and state deltas on portable shader properties.
- Only if that candidate proves weaker on re-read, document the negative result and narrow the next search boundary explicitly.

#### Actions

- [x] Update the live plan before continuing the next stateful-material packet.
- [x] Search current `tmp/` probe artifacts for a stronger `MTST` fixture than `002211BA8D2EE539`.
- [x] Promote `05773EECEE557829` into a second live-proof note as a texture-bearing portable-state boundary.
- [x] Sync the stronger boundary into the seam, matrix, restart, packet index, and open-question docs.
- [x] Fold the continuation hints back into the current plan.

#### Restart Hints

- The current floor is still `EP01\\ClientDeltaBuild0.package | Build/Buy Model 002211BA8D2EE539` as a model-rooted default-state boundary.
- The stronger second packet is now `docs/workflows/material-pipeline/live-proof-packets/buildbuy-mtst-portable-state-delta.md`.
- What makes `0577...` stronger is not object identity but state richness: one `MaterialSet`-sourced material already carries `textures=2` and state deltas on portable shader properties (`AmbientDomeBottom`, `CloudColorWRTHorizonLight1`).
- The next open gap after this packet is now explicitly narrower: named swatch/object identity or clearer runtime-state semantics, not another generic texture-bearing `MTST` proof.

#### Problem

The new `Build/Buy Stateful Material-Set Seam` narrows the safe authority reading, but it still lacks one concrete fixture-backed packet showing how `MTST` behaves on a real model root. Without that, future work still has to restart from wording instead of from an explicit stateful-material boundary case.

#### Chosen Approach

- Keep this as a narrow live-proof documentation packet, not a broad new matrix.
- Use the existing external `MTST` / `MaterialVariant` authority reading as the truth layer.
- Promote the already captured local probe artifact `tmp/probe_002211_after.txt` into one honest fixture-backed packet.
- Document exactly what this fixture proves and what it still does not prove: a model-rooted default-state boundary, not full swatch/object-state closure.

#### Actions

- [x] Re-open the `MTST` seam doc and inspect local probe artifacts for a viable stateful-material fixture.
- [x] Create one live-proof packet for the model-rooted `MTST` fixture `01661233:00000000:002211BA8D2EE539`.
- [x] Link that packet back into the Build/Buy matrix, seam doc, and live-proof index.
- [x] Narrow the next stateful-object gap so later runs can hunt swatch-level or runtime-state fixtures rather than re-proving the default-state boundary.
- [x] Fold the continuation hints back into the current plan.

#### Restart Hints

- The candidate fixture is `EP01\\ClientDeltaBuild0.package | Build/Buy Model 002211BA8D2EE539`.
- This fixture is model-rooted only: current probe says exact `ObjectCatalog/ObjectDefinition` metadata was not found.
- What it does show clearly is repeated `MTST` presence plus multiple equal-scored state hashes and a preview note that the inferred default state was kept because variants changed only non-portable control properties.
- Treat it as a default-state boundary packet, not as full swatch/state closure.
- The next stronger closure should come from either a named swatch-level `MaterialVariant` fixture or a texture-bearing/runtime-state family.

#### Problem

The new `Build/Buy Material Authority Matrix` stabilizes the base object/material chain, but its strongest unfinished seam is still too implicit: `MTST`, `MaterialVariant`, and swatch/state selection are mentioned in several docs without one bounded packet that explains their safest current reading. That keeps the `stateful/material-set` branch broader than necessary and makes future family packets start from scratch.

#### Chosen Approach

- Keep this as an external-first documentation packet, not a fixture-heavy live-proof packet.
- Use the already collected external object-side sources that talk about `Material Set`, `MLOD`, `MaterialVariant`, Type300 entries, and swatch linkage.
- Create one narrow companion doc for the `MTST` / stateful-material seam, then link it from the Build/Buy matrix and supporting restart/source/open-question docs.
- Keep exact live family closure open; this packet should freeze the safe authority reading, not overclaim a solved runtime state model.

#### Actions

- [x] Re-open the current Build/Buy matrix and confirm that the `MTST` seam is still only embedded there.
- [x] Create a dedicated Build/Buy stateful/material-set companion under `docs/workflows/material-pipeline/`.
- [x] Link the new seam doc from the Build/Buy matrix and relevant navigation/source/open-question docs.
- [x] Narrow the remaining stateful-object gap so the next packet can target fixtures instead of rediscovering the same authority reading.
- [x] Fold the continuation hints back into the current plan.

#### Restart Hints

- This packet is about the safe authority reading for `MTST` and `MaterialVariant`, not about final shader semantics.
- The current safe reading to preserve is: `COBJ/OBJD` choose the swatch/instance, `OBJD` links to the model root, and `MaterialVariant` selects into object-side material entries or sets inside the `MODL/MLOD` chain.
- The exact family-specific ranking between `MTST`, `MATD`, and runtime state packets should stay open until fixture-backed object families are documented.
- The new seam doc is `docs/workflows/material-pipeline/buildbuy-stateful-material-set-seam.md`.
- The next continuation after this packet should be a fixture-backed stateful/swatch-heavy object family, not another wording pass about `MTST`.

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
