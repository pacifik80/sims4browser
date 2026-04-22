# Material Documentation Status Catalog

This file is the full catalog for the external-first TS4 material documentation set.

Use it when the question is not only "what changed in the last packet?" but "what documentation exists right now, and how mature is each part?"

Primary rule:

- use only statuses the docs already declare themselves
- if a doc has no explicit scope-status block, do not invent percentages
- mark those docs honestly as `navigation-only`, `tracking-only`, or `source-layer`

Related docs:

- [Knowledge map](../../knowledge-map.md)
- [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md)
- [Material pipeline deep dives](README.md)
- [Research Restart Guide](research-restart-guide.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)
- [Open questions](../../references/codex-wiki/04-research-and-sources/03-open-questions.md)
- [Current plan](../../planning/current-plan.md)

## Status legend

- `closed for implementation baseline`: strong enough to drive current architecture
- `partial`: structurally useful and bounded, but still incomplete
- `navigation-only`: index or hub doc, not a progress-bearing research packet
- `tracking-only`: planning or unresolved-gap tracker, not a truth-layer doc
- `source-layer`: provenance/trust doc, not a maturity-scored synthesis packet

## Full catalog tree

```text
Material Documentation Catalog
├─ Core guides and hubs
│  ├─ Shared TS4 Material, Texture, And UV Pipeline -> partial; architecture baseline closed
│  ├─ Knowledge map -> navigation-only
│  ├─ Material pipeline README -> navigation-only
│  ├─ Research restart guide -> navigation-only
│  ├─ Source map and trust levels -> source-layer
│  ├─ Open questions -> tracking-only
│  └─ Current plan -> tracking-only
├─ Census baselines
│  ├─ Corpus-Wide Family Census Baseline -> source-layer
│  ├─ MATD Shader Census Baseline -> source-layer
│  ├─ Sim Archetype Material Carrier Census -> source-layer
│  ├─ CAS Carrier Census Baseline -> source-layer
│  ├─ CASPart Linkage Census Baseline -> source-layer
│  ├─ CASPart GEOM Shader Census Baseline -> source-layer
│  └─ CompositionMethod Census Baseline -> source-layer
├─ Deep dives
│  ├─ Build/Buy Material Authority Matrix -> 51% to 95%
│  ├─ Build/Buy Stateful Material-Set Seam -> 44% to 90%
│  ├─ CAS/Sim Material Authority Matrix -> 44% to 88%
│  ├─ Body And Head Shell Authority Table -> 55% to 88%
│  ├─ Hair, Accessory, And Shoes Authority Table -> 58% to 94%
│  ├─ BodyType Translation Boundary -> 46% to 96%
│  ├─ CompositionMethod And SortLayer Boundary -> 38% to 86%
│  ├─ CompositionMethod Census Baseline -> 41% to 96%
│  ├─ Overlay And Detail Family Authority Table -> 39% to 95%
│  ├─ SortLayer Census Baseline -> 46% to 96%
│  ├─ Shader Family Registry -> 56% to 88%
│  ├─ Runtime Shader Interface Baseline -> 15% to 92%
│  ├─ External GPU Scene-Pass Baseline -> 36% to 85%
│  ├─ Package-Material Pass Filtering Contract -> 44% to 87%
│  ├─ Package / Runtime / Scene Bridge Boundary -> 28% to 82%
│  ├─ Helper-Family Package Carrier Boundary -> 22% to 81%
│  ├─ Helper-Family Carrier Plausibility Matrix -> 34% to 84%
│  ├─ Runtime Helper-Family Clustering Floor -> 34% to 86%
│  ├─ Skintone And Overlay Compositor -> 38% to 86%
│  ├─ Edge-Family Matrix -> 57% to 95%
│  └─ P1 Live-Proof Queue -> 78% to 95%
├─ Family sheets
│  ├─ Object Glass And Transparency -> 41% to 93%
│  ├─ SimSkin / SimGlass / SimSkinMask -> 27% to 84%
│  ├─ CASHotSpotAtlas -> 19% to 91%
│  ├─ ShaderDayNightParameters -> 24% to 74%
│  ├─ Projection / Reveal / Lightmap families -> 23% to 63%
│  └─ GenerateSpotLightmap / NextFloorLightMapXform -> 20% to 78%
└─ Live-proof packets
   ├─ Build/Buy MTST Default-State Boundary -> 24% to 89%
   ├─ Build/Buy MTST Portable-State Delta -> 29% to 88%
   ├─ Build/Buy Window-Curtain Widening Route -> 16% to 98%
   ├─ Build/Buy Window-Curtain Family Verdict Boundary -> 24% to 86%
   ├─ Build/Buy Window-Curtain Strongest-Pair Material Divergence -> 31% to 89%
   ├─ Build/Buy Window CutoutInfoTable Companion Floor -> 36% to 91%
   ├─ Build/Buy Window ModelCutout Companion Closure -> 38% to 93%
   ├─ Build/Buy Window Structural-Cutout Verdict Floor -> 41% to 94%
   ├─ Build/Buy Curtain Route Closure -> 89% to 92%
   ├─ Build/Buy Window-Curtain Quartet Family Split -> 89% to 94%
   ├─ SimSkin Body/Head Shell Authority -> 69% to 95%
   ├─ Overlay-Detail Priority After High-Byte Stack -> 27% to 89%
   ├─ BodyType 0x44 Family Boundary -> 24% to 86%
   ├─ BodyType 0x41 Family Boundary -> 24% to 88%
   ├─ BodyType 0x6D Family Boundary -> 24% to 87%
   ├─ BodyType 0x6F Family Boundary -> 24% to 89%
   ├─ BodyType 0x52 Family Boundary -> 24% to 88%
   ├─ BodyType 0x80 Family Boundary -> 24% to 76%
   ├─ SimGlass Versus Shell Baseline -> 24% to 87%
   ├─ SimSkin Versus SimSkinMask -> 53% to 90%
   ├─ CASHotSpotAtlas Carry-Through -> 28% to 94%
   ├─ ShaderDayNight Visible-Pass Proof -> 36% to 84%
   ├─ ShaderDayNight Runtime Cluster Candidate Floor -> 22% to 83%
   ├─ ShaderDayNight Runtime Context Gap -> 31% to 84%
   ├─ Generated-Light Runtime Cluster Candidate Floor -> 24% to 83%
   ├─ Generated-Light Runtime Context Gap -> 31% to 83%
   ├─ Projection-Reveal Runtime Cluster Candidate Floor -> 26% to 83%
   ├─ Projection-Reveal Runtime Context Gap -> 31% to 83%
   ├─ GenerateSpotLightmap / NextFloorLightMapXform -> 18% to 82%
   └─ RefractionMap Live Proof -> 21% to 86%
```

## Core guides and hubs

| Document | Role | Current status | Notes |
| --- | --- | --- | --- |
| [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md) | cross-domain source-of-truth synthesis | `partial`; architecture baseline closed | architecture, shared pipeline stages, current repo status, practical rules, and validation are closed enough for current implementation; open gaps remain around full family contracts, compositor math, UV matrix, and `VPXY` |
| [Knowledge map](../../knowledge-map.md) | repo-wide navigation hub | `navigation-only` | route map into the durable knowledge layers |
| [Material pipeline deep dives](README.md) | section-level hub | `navigation-only` | entry point into narrow material docs |
| [Research Restart Guide](research-restart-guide.md) | continuity and execution hub | `navigation-only` | restart contract, continuation order, and report format |
| [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md) | provenance and trust ladder | `source-layer` | defines evidence order rather than maturity |
| [Open questions](../../references/codex-wiki/04-research-and-sources/03-open-questions.md) | unresolved-gap register | `tracking-only` | narrowed open questions, not a truth-layer packet |
| [Current plan](../../planning/current-plan.md) | live execution tracker | `tracking-only` | current bounded packets and restart hints |

## Census baselines

| Document | Role | Current status | Notes |
| --- | --- | --- | --- |
| [Corpus-Wide Family Census Baseline](corpus-wide-family-census-baseline.md) | whole-install corpus and prevalence baseline | `source-layer` | current package, resource, asset, and domain totals plus direct counted prevalence layers |
| [MATD Shader Census Baseline](matd-shader-census-baseline.md) | direct object-side shader-profile census | `source-layer` | first package-derived object-side family/profile count layer |
| [Sim Archetype Material Carrier Census](sim-archetype-material-carrier-census.md) | direct graph-backed `Sim` subset census | `source-layer` | first built-preview character-side carrier floor |
| [CAS Carrier Census Baseline](cas-carrier-census-baseline.md) | whole-`CAS` slot/fact prevalence census | `source-layer` | direct whole-`CAS` structure floor, but not yet linkage or family closure |
| [CASPart Linkage Census Baseline](caspart-linkage-census-baseline.md) | direct parsed `CASPart -> GEOM/texture/region_map` linkage census | `source-layer` | first strong character-side linkage floor below slot/fact counts |
| [CASPart GEOM Shader Census Baseline](caspart-geom-shader-census-baseline.md) | direct parsed `CASPart -> GEOM -> shader family/profile` census | `source-layer` | first direct character-side family-count floor; still bounded by parser and geometry-resolution gaps |
| [CompositionMethod Census Baseline](compositionmethod-census-baseline.md) | direct parsed `CASPart -> CompositionMethod / SortLayer` census | `source-layer` | first whole-install compositor-side count layer; cache repopulation still pending |

## Deep dives

| Document | Status surface | Current reading |
| --- | --- | --- |
| [Build/Buy Material Authority Matrix](buildbuy-material-authority-matrix.md) | `51%` to `95%` | base authority graph is strong; the object-side transparency branch now has a frozen widened-quartet split with window-side structural cutout/opening and curtain-side weaker threshold/cutout, broader family-specific authority order still remains open elsewhere, and the downstream external GPU filter is now explicit: `BuildBuy` candidates should currently pass through `WorldRoom + MaterialLike` rather than competing equally with compositor/depth passes |
| [Build/Buy Stateful Material-Set Seam](buildbuy-stateful-material-set-seam.md) | `44%` to `90%` | authority wording is stable; fixture-backed family closure remains the weak point |
| [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md) | `44%` to `88%` | shared graph and family split are strong; body/head shell authority now also has a direct shell-floor snapshot, full live-asset authority order remains open, and the downstream external GPU filter is now explicit: `CAS/Sim` candidates should currently pass through `CAS + MaterialLike` rather than competing equally with compositor/depth passes |
| [Body And Head Shell Authority Table](body-head-shell-authority-table.md) | `55%` to `88%` | first explicit table for how body shell outranks head shell and where post-shell modifiers begin; the direct shell-floor snapshot now makes the body-versus-head asymmetry less prose-heavy |
| [Hair, Accessory, And Shoes Authority Table](hair-accessory-shoes-authority-table.md) | `58%` to `94%` | explicit sibling table for exact head-adjacent worn slots versus footwear overlay logic, with a bounded direct pair floor and a separate skintone boundary |
| [BodyType Translation Boundary](bodytype-translation-boundary.md) | `46%` to `96%` | separates directly named low-value `BodyType` rows from the large mixed high-bit buckets, and now links the full current restart-safe packet stack through `0x44`, `0x41`, `0x6D`, `0x6F`, `0x52`, and `0x80` |
| [CompositionMethod And SortLayer Boundary](compositionmethod-sortlayer-boundary.md) | `38%` to `86%` | keeps layer ordering separate from shell selection, now points at direct counts for both fields, and now also has a compact same-layer `CompositionMethod + SortLayer` snapshot for readable slots, ordinary overlay rows, and mixed high-byte comparison rows |
| [CompositionMethod Census Baseline](compositionmethod-census-baseline.md) | `41%` to `96%` | whole-install count layer for `CompositionMethod` and `CompositionMethod + SortLayer` pairs, now matched by a backfilled shard cache column |
| [Overlay And Detail Family Authority Table](overlay-detail-family-authority-table.md) | `39%` to `95%` | first explicit table for ordinary overlay/detail families versus skintone-carried overlay logic, now tied to the stricter post-high-byte precedence packet and the new same-layer compositor snapshot |
| [SortLayer Census Baseline](sortlayer-census-baseline.md) | `46%` to `96%` | first direct whole-index count layer for `sort_layer`, now aligned with the separate direct `CompositionMethod` census |
| [Shader Family Registry](shader-family-registry.md) | `56%` to `88%` | representative live asset packet is still strong, the runtime shader-interface corpus now closes much more of the per-shader slot/param/geometry layer, the helper-family rows now also carry both an explicit package-side carrier boundary and a compact plausibility matrix, and the new external GPU pass-family layer gives the registry a stronger scene/pass exclusion floor without yet closing final ownership |
| [Runtime Shader Interface Baseline](runtime-shader-interface-baseline.md) | `15%` to `92%` | new live-runtime baseline: runtime shader inventory, per-shader reflection contracts, executable-linked subset, and the updated spec's package-side carrier priorities plus lightweight disassembly signals are now all in one place; reflection still does not close draw-state/pass-order truth by itself, but the new external GPU scene/pass baseline now gives that gap a real neighboring evidence layer instead of leaving it empty |
| [External GPU Scene-Pass Baseline](external-gpu-scene-pass-baseline.md) | `36%` to `85%` | first checked-in external GPU-capture baseline for the main project: it now gives real `CAS` versus `WorldRoom` scene-domain truth, `MaterialLike` versus `CompositorOrUi` versus `DepthOnly` pass-family truth, and strong positive/negative pass signatures that are immediately useful for filtering package-material hypotheses before exact shader-hash closure exists |
| [Package-Material Pass Filtering Contract](package-material-pass-filtering-contract.md) | `44%` to `87%` | first browser-facing contract for using the new external GPU layer: it turns `SceneDomain` and `PassClass` into hard filters, defines positive and negative pass-signature evidence, and gives the main project a concrete first-pass ranking rule for package-material hypotheses before exact shader-hash closure exists |
| [Package / Runtime / Scene Bridge Boundary](package-runtime-scene-bridge-boundary.md) | `28%` to `82%` | isolates the current global blocker as one explicit missing join instead of vague "more research": package-side carrier priority is now strong enough to discuss alongside runtime interface evidence and helper-family clustering, and the new external GPU scene/pass baseline now materially strengthens the scene/pass side of the bridge even though final ownership closure still depends on proving either `package carrier -> runtime cluster` or RenderDoc-to-live identity normalization strongly enough to complete the three-way join |
| [Helper-Family Package Carrier Boundary](helper-family-package-carrier-boundary.md) | `22%` to `81%` | isolates what the current package-side layer already constrains for weak helper-family rows: `MATD` and `MTST` are now bounded strongly enough to ask ownership questions in the right order, but current package-side evidence is still not enough to promote `ShaderDayNight`, generated-light, or projection/reveal into final carrier ownership without the missing runtime-context join |
| [Helper-Family Carrier Plausibility Matrix](helper-family-carrier-plausibility-matrix.md) | `34%` to `84%` | compact row-by-row comparison layer for the three weak helper-family rows: each row now keeps its narrowed runtime candidate, current plausible carrier reading, current premature claim, and the exact evidence that would promote the next ownership claim |
| [Runtime Helper-Family Clustering Floor](runtime-helper-family-clustering-floor.md) | `34%` to `86%` | first shape-based bridge from the runtime corpus into the weakest helper-family rows; generic runtime names remain dominant, but helper-like parameter/resource clusters now also support three real family-local narrowing steps: `ShaderDayNightParameters` can start from `F04`, generated-light can start from the stable `F03` `maptex` packet with its broad-capture ceiling now frozen, and the remaining projection/reveal branch can start from the stable `F04` `srctex + tex` packet with its own broad-capture ceiling now frozen; the next tagged-capture step is now operationalized through the standard runner, helper presets, exact recipe commands, and a standardized post-capture compare workflow, with target/control pairs now explicitly kept inside the same helper-family focus instead of drifting into under-tagged controls |
| [Skintone And Overlay Compositor](skintone-and-overlay-compositor.md) | `38%` to `86%` | current repo routing is strong; exact in-game blend math remains open |
| [Edge-Family Matrix](edge-family-matrix.md) | `57%` to `95%` | object-transparency and `SimSkin` seams are strongest; object transparency now preserves a frozen widened-quartet split instead of one unresolved family-verdict bundle |
| [P1 Live-Proof Queue](p1-live-proof-queue.md) | `78%` to `95%` | queue is now rebuilt around object-side transparency and `SimSkin`; the widened transparent-object route is frozen at the current inspection layer, `ShaderDayNightParameters` now has a sharper blocker packet for the broad runtime-capture ceiling, generated-light now also has the matching blocker packet above its narrowed `F03` runtime bridge, and the queue now has both a checked-in external GPU pass-family baseline and a browser-facing filtering contract so offline matching work can exclude compositor/depth-helper false positives earlier instead of treating all captured passes alike |

## Family sheets

| Document | Status surface | Current reading |
| --- | --- | --- |
| [SimSkin, SimGlass, And SimSkinMask](family-sheets/simskin-simglass-simskinmask.md) | `27%` to `84%` | identity packets are strong; the `SimSkinMask` negative result is now tighter because the current workspace search no longer hides a non-mirrored local `.simgeom` sample lane, the direct `CASPart -> GEOM -> family` floor still stays negative, and the bounded public refresh still keeps skin-mask semantics under `SkinOverlay` or overlay-style workflows rather than a peer geometry branch |
| [Object Glass And Transparency](family-sheets/object-glass-and-transparency.md) | `41%` to `93%` | object-side glass and cutout/blended transparency separation is strong; the widened EP10 quartet is now frozen as window-side structural cutout/opening versus curtain-side weaker threshold/cutout, with object glass still unselected |
| [CASHotSpotAtlas](family-sheets/cas-hotspot-atlas.md) | `19%` to `91%` | identity, `UV1`, atlas-color-to-`HotSpotControl`, and morph/edit routing are strong; the local external packet now sharply confirms the downstream `SimModifier -> SMOD -> BGEO/DMap/BOND` bridge while keeping the missing local atlas/parser path explicit |
| [ShaderDayNightParameters](family-sheets/shader-daynight-parameters.md) | `24%` to `74%` | reveal-helper reading is still bounded, but the family row now carries narrowed runtime candidates, an explicit package-side carrier limit, and one compact plausibility reading: `F04` remains the leading runtime cluster candidate, `F05` remains the nearest color-aware sibling comparator, and the next promotable ownership step is now explicit instead of implied |
| [Projection, Reveal, And Lightmap Families](family-sheets/projection-reveal-lightmap.md) | `23%` to `63%` | generated-light helper reading is still stronger than exact slot contracts, and the umbrella now carries narrowed runtime candidates, a stronger authored-carrier boundary, and one compact plausibility reading across its three sub-rows: generated-light still starts from the stable `F03` `maptex` packet, the remaining projection/reveal branch still starts from the stable `F04` `srctex + tex` packet, and current package-side evidence still constrains but does not close authored ownership |
| [GenerateSpotLightmap And NextFloorLightMapXform](family-sheets/generate-spotlightmap-nextfloorlightmapxform.md) | `20%` to `78%` | helper reading is still bounded, but the family row now carries the narrowed `F03` runtime bridge, an explicit package-side carrier limit, and one compact plausibility reading: the stable `maptex + tex + Constants` packet remains the strongest runtime bridge, while the next promotable ownership step is now explicit instead of implied |
| [Family Sheets README](family-sheets/README.md) | `navigation-only` | index for family evidence sheets |

## Live-proof packets

| Document | Status surface | Current reading |
| --- | --- | --- |
| [Build/Buy MTST Default-State Boundary](live-proof-packets/buildbuy-mtst-default-state-boundary.md) | `24%` to `89%` | strong default-state floor; no swatch-level closure |
| [Build/Buy MTST Portable-State Delta](live-proof-packets/buildbuy-mtst-portable-state-delta.md) | `29%` to `88%` | stronger texture-bearing `MTST` boundary; still model-rooted only |
| [Build/Buy Window-Curtain Widening Route](live-proof-packets/buildbuy-window-curtain-widening-route.md) | `16%` to `98%` | after the stalled decor route, the next transparent-object widening lane is now a bounded window/curtain packet with four real `Partial` fixtures, exact `ObjectDefinition` identity roots, and same-package `swap32` model promotion |
| [Build/Buy Window-Curtain Family Verdict Boundary](live-proof-packets/buildbuy-window-curtain-family-verdict-boundary.md) | `24%` to `86%` | after the widened quartet went live, the next restart-safe question is now family-verdict closure across the quartet rather than more widening |
| [Build/Buy Window-Curtain Strongest-Pair Material Divergence](live-proof-packets/buildbuy-window-curtain-strongest-pair-material-divergence.md) | `31%` to `89%` | after the quartet-level verdict boundary, the strongest direct material-entry window and curtain now diverge enough to block one quartet-wide family label and to hand the next step specifically to window-side structural cutout inspection |
| [Build/Buy Window CutoutInfoTable Companion Floor](live-proof-packets/buildbuy-window-cutoutinfotable-companion-floor.md) | `36%` to `91%` | the surviving window pair now has explicit same-instance `CutoutInfoTable` companions with model-root-matching entries and `USES_CUTOUT` / `IS_PORTAL` flags, which is strong enough to promote a structural cutout companion floor without yet claiming full `ModelCutout` closure |
| [Build/Buy Window ModelCutout Companion Closure](live-proof-packets/buildbuy-window-modelcutout-companion-closure.md) | `38%` to `93%` | the surviving window pair now also has same-instance `ModelCutout`, closing the full local structural companion pair on the exact promoted model roots |
| [Build/Buy Window Structural-Cutout Verdict Floor](live-proof-packets/buildbuy-window-structural-cutout-verdict-floor.md) | `41%` to `94%` | with the full structural companion pair closed and only secondary material cutout hints remaining, the window-side family verdict can now move to structural cutout/opening first while the next proof shifts to the curtain side |
| [Build/Buy Curtain Route Closure](live-proof-packets/buildbuy-curtain-route-closure.md) | `89%` to `92%` | the surviving curtain pair now stays below explicit `AlphaBlended` closure and is safest as a weaker threshold/cutout route, not structural opening or object glass |
| [Build/Buy Window-Curtain Quartet Family Split](live-proof-packets/buildbuy-window-curtain-quartet-family-split.md) | `89%` to `94%` | with both window-side and curtain-side verdict floors bounded, the widened EP10 quartet is now frozen as a family split rather than one unresolved transparent-object bundle |
| [SimSkin Body/Head Shell Authority](live-proof-packets/simskin-body-head-shell-authority.md) | `69%` to `95%` | strongest current character-side bridge from `SimSkin` family existence into shell authority and skintone/compositor ordering |
| [Overlay-Detail Priority After High-Byte Stack](live-proof-packets/overlay-detail-priority-after-highbyte-stack.md) | `27%` to `89%` | strongest current bridge from the closed high-byte `BodyType` packet stack into ordinary overlay/detail precedence and the separate `TONE` branch; it now also carries a same-layer `CompositionMethod + SortLayer` floor that keeps ordinary overlay rows, readable shell/worn-slot rows, and mixed high-byte comparison rows in one direct packet |
| [BodyType 0x44 Family Boundary](live-proof-packets/bodytype-0x44-family-boundary.md) | `24%` to `86%` | first concrete packet for the largest mixed high-byte family; keeps `OccultLeftCheek` overlap separate from literal slot renaming |
| [BodyType 0x41 Family Boundary](live-proof-packets/bodytype-0x41-family-boundary.md) | `24%` to `88%` | second concrete packet for the next largest apparel-heavy family; freezes the first clothing-like compositor sub-lane inside that family |
| [BodyType 0x6D Family Boundary](live-proof-packets/bodytype-0x6d-family-boundary.md) | `24%` to `87%` | strongest current counterexample to naive low-byte decoding; keeps the `HairFeathers` overlap at family level instead of pretending the slot is already solved |
| [BodyType 0x6F Family Boundary](live-proof-packets/bodytype-0x6f-family-boundary.md) | `24%` to `89%` | mixed special-content packet that now ties head-decoration and tail-base creator evidence back into the high-byte family track |
| [BodyType 0x52 Family Boundary](live-proof-packets/bodytype-0x52-family-boundary.md) | `24%` to `88%` | concentrated bottom-heavy family packet that keeps the `BodyScarArmLeft` overlap separate from any literal arm-scar decode |
| [BodyType 0x80 Family Boundary](live-proof-packets/bodytype-0x80-family-boundary.md) | `24%` to `76%` | honest weaker packet for the sign-bit family; the repeated family is real, but no direct external vocabulary anchor has survived yet |
| [SimGlass Versus Shell Baseline](live-proof-packets/simglass-vs-shell-baseline.md) | `24%` to `87%` | identity and search isolation are strong; shell-ranking proof remains weak |
| [SimSkin Versus SimSkinMask](live-proof-packets/simskin-vs-simskinmask.md) | `53%` to `90%` | negative-result packet is now tighter: `SimSkinMask` survives in profile archaeology, but the current workspace no longer surfaces a non-mirrored local `.simgeom` or export branch, the checked-in direct family census also stays negative, and the bounded public refresh still stops at `SkinOverlay` or overlay-style semantics rather than peer GEOM-family status |
| [CASHotSpotAtlas Carry-Through](live-proof-packets/cas-hotspotatlas-carry-through.md) | `28%` to `94%` | identity and carry-through concentration are strong; the external atlas-color-to-`HotSpotControl` packet plus the local downstream bridge now make the control chain much sharper, while exact runtime render proof remains open |
| [ShaderDayNightParameters Visible-Pass Proof](live-proof-packets/shader-daynight-visible-pass.md) | `36%` to `84%` | visible-root isolation is now stronger with three anchors and explicit helper counts; exact visible-pass contract remains open |
| [ShaderDayNight Runtime Cluster Candidate Floor](live-proof-packets/shader-daynight-runtime-cluster-candidate-floor.md) | `22%` to `83%` | first family-local narrowing step inside the runtime helper-family route: `F04` is now the leading parameter-heavy cluster candidate for `ShaderDayNightParameters`, while `F05` stays the nearest color-aware sibling comparator |
| [ShaderDayNight Runtime Context Gap](live-proof-packets/shader-daynight-runtime-context-gap.md) | `31%` to `84%` | current checked-in broad captures now have an explicit blocker packet: representative `F04` and `F05` hashes persist across all broad sessions, but the sessions are not scene-tagged enough to promote context-bound family ownership yet |
| [Generated-Light Runtime Cluster Candidate Floor](live-proof-packets/generated-light-runtime-cluster-candidate-floor.md) | `24%` to `83%` | first family-local narrowing step for generated-light on the runtime side: the stable `F03` `maptex + tex + Constants` packet with `compx`, `compy`, `mapScale`, and `scale` is now the leading generated-light cluster candidate |
| [Generated-Light Runtime Context Gap](live-proof-packets/generated-light-runtime-context-gap.md) | `31%` to `83%` | current broad checked-in runtime sessions now also have an explicit blocker packet for the generated-light branch: the narrowed `F03` `maptex + tex` packet survives across all checked broad captures, but the manifests still do not carry scene/context labels strong enough for stronger family ownership |
| [Projection-Reveal Runtime Cluster Candidate Floor](live-proof-packets/projection-reveal-runtime-cluster-candidate-floor.md) | `26%` to `83%` | first family-local narrowing step for the remaining projection/reveal runtime branch: the stable `F04` `srctex + tex + Constants` packet with `fsize`, `offset`, `scolor`, `srctexscale`, and `texscale` is now the leading candidate floor |
| [Projection-Reveal Runtime Context Gap](live-proof-packets/projection-reveal-runtime-context-gap.md) | `31%` to `83%` | current broad checked-in runtime sessions now also have an explicit blocker packet for that same branch: the narrowed `F04` `srctex + tex` packet survives across all checked broad captures, but the manifests still do not carry scene/context labels strong enough for stronger family ownership |
| [GenerateSpotLightmap And NextFloorLightMapXform](live-proof-packets/generate-spotlightmap-nextfloorlightmapxform.md) | `18%` to `82%` | carry-through packet is stronger with explicit `6 / 14 / 3` local floors; exact matrix semantics remain open |
| [RefractionMap Live Proof](live-proof-packets/refractionmap-live-proof.md) | `21%` to `86%` | live-root isolation is strong; exact slot contract remains open |
| [Live-Proof Packets README](live-proof-packets/README.md) | `navigation-only` | packet index and strongest-current-fixture summary |

## Current strongest packets by layer

| Layer | Strongest current document | Why |
| --- | --- | --- |
| shared guide | [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md) | cross-domain architectural contract is already stable |
| Build/Buy authority | [Build/Buy Material Authority Matrix](buildbuy-material-authority-matrix.md) | base object-side authority graph is already strong |
| stateful Build/Buy seam | [Build/Buy Stateful Material-Set Seam](buildbuy-stateful-material-set-seam.md) | `MTST` / `MaterialVariant` seam is now explicit and restart-safe |
| CAS/Sim authority | [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md) | strongest current family-split and authority-order packet for characters |
| shader families | [Shader Family Registry](shader-family-registry.md) | best current family-wide synthesis layer |
| edge families | [Edge-Family Matrix](edge-family-matrix.md) | strongest cross-family edge-case synthesis layer |
| family evidence sheet | [SimSkin, SimGlass, And SimSkinMask](family-sheets/simskin-simglass-simskinmask.md) | strongest currently consolidated character-family sheet |
| live-proof packet | [SimSkin Body/Head Shell Authority](live-proof-packets/simskin-body-head-shell-authority.md) | strongest current character-side live-proof packet after the completed direct `CASPart -> GEOM` family census |
| stateful live-proof packet | [Build/Buy MTST Portable-State Delta](live-proof-packets/buildbuy-mtst-portable-state-delta.md) | strongest current `MTST` texture-bearing portable-state boundary |

## Honest limit

This catalog is a visibility layer, not a new truth layer.

What it does:

- shows the full current document set
- keeps all explicit status surfaces in one place
- makes weak versus strong packets easier to identify before the next run

What it does not do:

- replace the docs it catalogs
- invent percentages for docs that do not declare them
- convert tracking/navigation docs into authority documents
