# CAS/Sim Material Authority Matrix

This document is the detailed companion for the shared TS4 material guide when the task is specifically about `CAS` / `Sim` material authority, shell versus worn-slot behavior, skintone routing, or the boundary between `CASP`, `GEOM/MTNF`, `MATD/MTST`, `RegionMap`, and `Skintone`.

Related docs:

- [Knowledge map](../../knowledge-map.md)
- [Workflows index](../README.md)
- [Material pipeline deep dives](README.md)
- [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md)
- [Shader Family Registry](shader-family-registry.md)
- [Edge-Family Matrix](edge-family-matrix.md)
- [Skintone And Overlay Compositor](skintone-and-overlay-compositor.md)
- [Sim domain roadmap](../../sim-domain-roadmap.md)
- [Sim body-shell contract](../../sim-body-shell-contract.md)
- [Full Sim and morph pipeline](../../references/codex-wiki/02-pipelines/03-full-sim-and-morphs.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)
- [Open questions](../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Scope status (`v0.1`)

```text
CAS/Sim Material Authority Matrix
├─ Shared authority graph ~ 88%
├─ Family split ~ 84%
├─ Body/head shell authority ~ 80%
├─ Shell material truth source ~ 72%
├─ Hair/accessory/shoes authority ~ 78%
├─ Overlay/detail family boundary ~ 74%
├─ SimSkin vs SimSkinMask authority seam ~ 74%
├─ Edge-family authority packet ~ 57%
└─ Full live-asset authority order ~ 43%
```

What this doc is for:

- keep the big `CAS/Sim` authority topic out of the main shared guide
- preserve one place where external evidence, creator tooling, local external snapshots, and current implementation boundaries are aligned without conflating them
- make later family-by-family passes incremental instead of rewriting one giant monolith
- document how `CAS/Sim` chooses authoritative material inputs before those inputs enter the shared shader/material pipeline

What this doc is not:

- not a claim that full in-game authority order is solved
- not a replacement for the shared cross-domain pipeline guide
- not a definition of `CAS`-specific or `Sim`-specific shader semantics after canonical-material decoding begins

## Current strongest shared authority graph

```text
selected CASP
        ->
linked GEOM
        ->
GEOM embedded MTNF (when present)
        ->        \
material-definition path   field-routing path
   (MATD/MTST if present)   (CASP texture/material fields)
             \             /
        canonical material candidates
                   ->
     RegionMap / SharedUVMapSpace / CompositionMethod / SortLayer
                   ->
    Sim-only skintone routing and compositing inputs
          (Skintone, shift, region-aware targets)
                   ->
       shared canonical material output
```

What is already strong enough:

- exact selected `CASP` plus linked `GEOM` is the primary identity root for `CAS` and `Sim`
- embedded `MTNF` is a real geometry-side material candidate, even though the current repo still does not decode that payload end-to-end
- explicit `MATD/MTST` is stronger than manifest approximation when it really exists and decodes
- `CASP` field-routing is real authority input, not a fake fallback
- `RegionMap`, `SharedUVMapSpace`, `CompositionMethod`, and `SortLayer` are real post-selection modifiers
- `Skintone` is a Sim-only routing/compositing layer acting on selected material targets, not a replacement renderer

Current implementation boundary:

- current CAS scene build prefers decoded `MaterialDefinition` before manifest approximation
- if explicit material decoding and manifest material reconstruction are both too weak, repo preview falls back to `ApproximateCasMaterial`
- current Sim composition then applies skintone routing only to merged canonical materials selected by shell-aware and region-map-aware targeting

Current safe architectural rule:

- treat `MATD/MTST` and `CASP` field-routing as competing authoritative inputs
- keep manifest approximation and same-instance texture bags as reserve-only paths
- let `Skintone` and `RegionMap` refine selected materials instead of overwriting the authority model
- once authoritative inputs are chosen, feed them into the same shared shader/material contract used everywhere else

Current implementation anchors:

- [AssetServices.cs](../../src/Sims4ResourceExplorer.Assets/AssetServices.cs)
- [BuildBuySceneBuildService.Cas.cs](../../src/Sims4ResourceExplorer.Preview/BuildBuySceneBuildService.Cas.cs)
- [SimSceneComposer.cs](../../src/Sims4ResourceExplorer.Core/SimSceneComposer.cs)
- [ExplorerTests.cs](../../tests/Sims4ResourceExplorer.Tests/ExplorerTests.cs)

## Family split

External evidence plus creator-facing body-type guidance are now strong enough to keep these families separate, and current repo graph work does not presently contradict that split:

| Family group | Typical slots | Current strongest authority reading | Current confidence |
| --- | --- | --- | --- |
| body foundation shell | `Full Body`, repo-visible `Body`, `Top`, `Bottom` | selected shell `CASP` plus linked `GEOM`, then `MTNF`/`MATD` if present, then field-routing, then compositor modifiers | medium-high |
| head shell | `Head` | selected head `CASP` plus linked `GEOM`, same candidate stack, shell-aware skintone allowed on top; broader whole-head identity also intersects with `Sim Preset` / head-driven selection semantics in external references | medium-high |
| footwear overlay | `Shoes` | selected footwear `CASP` plus linked `GEOM`, overlay semantics preserved through compositor fields | medium |
| head-related worn slots | `Hair`, `Accessory` | exact part-link first, compatibility fallback second, then ordinary `CASP -> GEOM -> material candidate` resolution | high |
| compositor-driven overlays | skin details, makeup, tattoos, face paint, similar non-shell layers | selected `CASP` plus compositor inputs and atlas/shared-UV rules, with skintone interaction where relevant | medium |

External corroboration:

- [Modifying Sim Appearances](https://thesims4moddersreference.org/tutorials/modifying-sim-appearances/)
- [The Sims 4 Modders Reference: File Types](https://thesims4moddersreference.org/reference/file-types/)
- [Adding new GEOMs to a CAS part with s4pe and S4CASTools](https://modthesims.info/t/536671)

## Body and head shell authority

What is now strong enough to say:

- shell filtering is explicit and does not apply to all CAS families
- preferred default shell selection uses parsed `CASP` facts rather than only names
- `defaultBodyType`, `nakedLink`, and related shell signals already matter in repo selection logic
- head shell is a mergeable sibling branch, not a body replacement
- skintone routing currently targets shell-family materials in practice

Current safe reading:

- body shell is the current assembly anchor
- head shell merges onto that anchor when rig/bone compatibility allows
- apparel-like full-body content should not silently become the base body
- skintone is currently shell-scoped, not a universal CAS-material mutation pass
- this section is about shell input authority, not about inventing shell-only shader semantics

Current implementation anchors:

- [AssetServices.cs](../../src/Sims4ResourceExplorer.Assets/AssetServices.cs)
- [SimSceneComposer.cs](../../src/Sims4ResourceExplorer.Core/SimSceneComposer.cs)
- [ExplorerTests.cs](../../tests/Sims4ResourceExplorer.Tests/ExplorerTests.cs)

External corroboration:

- [Modifying Sim Appearances](https://thesims4moddersreference.org/tutorials/modifying-sim-appearances/)
- [The Sims 4 Modders Reference: File Types](https://thesims4moddersreference.org/reference/file-types/)
- [Things made based on "Naked" mesh not showing up?](https://modthesims.info/t/545850)

## Shell material truth source

What is now strong enough:

- current CAS graph eagerly resolves parsed `CASP` texture refs and `region_map`
- geometry companions can legally add `Rig`, `MaterialDefinition`, and texture resources from the resolved geometry package
- scene build already prefers explicit material-definition decoding before manifest approximation
- current shell fixtures still often land on `ApproximateCas` while carrying shell-scoped `region_map`, `color_shift_mask`, and skintone provenance

Current safe reading:

- exact selected shell `CASP` plus linked `GEOM` is still the shell identity root
- explicit `MaterialDefinition` is a stronger upgrade path when present
- parsed `CASP` field-routing is the current shell-material truth floor when explicit material resources do not materialize
- embedded `MTNF` stays in the model as a geometry-side candidate, but repo code should not be described as already decoding it
- all four of those inputs still converge into the shared canonical material path; they do not define a separate shell renderer

Current asymmetry:

- explicit shell `MaterialDefinition` is strong at the composer seam and on generic CAS/worn-slot fixtures
- full shell-specific end-to-end asset-graph coverage still leans approximate

Current sample hint:

| Sample slice | With `MTNF` | Total checked | Status |
| --- | --- | --- | --- |
| bundled external `Body` `.simgeom` samples | `4` | `4` | all checked samples carry `MTNF` |
| bundled external `Head` `.simgeom` samples | `4` | `4` | all checked samples carry `MTNF` |
| bundled external `Waist` `.simgeom` samples | `1` | `1` | checked sample carries `MTNF` |
| synthetic repo shell fixtures | weak | n/a | current tests still bias toward GEOM without embedded payload |

Current implementation anchors:

- [AssetServices.cs](../../src/Sims4ResourceExplorer.Assets/AssetServices.cs)
- [BuildBuySceneBuildService.Cas.cs](../../src/Sims4ResourceExplorer.Preview/BuildBuySceneBuildService.Cas.cs)
- [SimSceneComposer.cs](../../src/Sims4ResourceExplorer.Core/SimSceneComposer.cs)
- [ExplorerTests.cs](../../tests/Sims4ResourceExplorer.Tests/ExplorerTests.cs)

External corroboration:

- [Sims_4:0x015A1849](https://modthesims.info/wiki.php?title=Sims_4%3A0x015A1849)
- [CAS Designer Toolkit](https://modthesims.info/d/694549)
- [TS4 SimRipper Classic](https://modthesims.info/d/635720/ts4-simripper-classic-rip-sims-from-savegames-v3-14-2-0-updated-4-19-2023.html)

## Hair, accessory, and shoes authority

What is now strong enough:

- `Hair` and `Accessory` resolve through exact part-link first, compatibility fallback second
- `Shoes` stay in body-assembly overlay logic and can sit on top of a body shell
- after `CASP -> GEOM`, CAS graph already allows geometry-side companion discovery for `Rig`, `MaterialDefinition`, and texture resources
- once explicit material resources exist, scene build tries material-definition decoding before manifest approximation
- current implementation also keeps skintone routing bounded away from hair, accessory, and footwear targets

Current safe reading:

- worn-slot identity comes from exact selected part slots first
- `CASP -> linked GEOM` is still the entry into the material graph
- cross-package geometry companions are valid for these families
- manifest approximation remains reserve-only after explicit material-definition decoding
- after that point, hair/accessory/shoes still use the same shared shader/material semantics as the rest of the pipeline

Current implementation anchors:

- [AssetServices.cs](../../src/Sims4ResourceExplorer.Assets/AssetServices.cs)
- [BuildBuySceneBuildService.Cas.cs](../../src/Sims4ResourceExplorer.Preview/BuildBuySceneBuildService.Cas.cs)
- [ExplorerTests.cs](../../tests/Sims4ResourceExplorer.Tests/ExplorerTests.cs)

External corroboration:

- [Modifying Sim Appearances](https://thesims4moddersreference.org/tutorials/modifying-sim-appearances/)
- [Adding new GEOMs to a CAS part with s4pe and S4CASTools](https://modthesims.info/t/536671)

## Overlay and detail families

This is still weaker than shells and worn-slot identity, but it is no longer a blind area.

Current safe reading:

- skin details, makeup, tattoos, face paint, and similar non-shell layers belong in compositor-driven overlay families
- these families are still rooted in selected `CASP`, but their authority picture is dominated more by `CompositionMethod`, `SortLayer`, shared atlas behavior, and skintone interaction than by shell-style body-foundation logic
- current evidence is good enough to keep them separate from shell and worn-slot authority tables even before their own per-family live-asset tables are built
- current external tooling also frames skin masks and similar face/body detail content as overlay, burn-mask, or skin-detail image workflows rather than as separate geometry-family roots
- this is still about authoritative inputs and compositor precedence, not a separate overlay-only shader system

Useful sources:

- [Modifying Sim Appearances](https://thesims4moddersreference.org/tutorials/modifying-sim-appearances/)
- [Please explain Skin Overlay, Skin Mask, Normal Skin, Etc](https://modthesims.info/t/594620)
- [TS4 Skininator](https://modthesims.info/d/568474/ts4-skininator-updated-8-6-2018-version-1-12.html)
- [TS4 Skin Converter V2.3](https://modthesims.info/d/650407/ts4-skin-converter-v2-3-enable-cc-skintones-in-cas.html)
- [Sims 4 Studio 3.2.4.7 release notes](https://sims4studio.com/thread/29786/sims-studio-windows-star-open)

For the denser compositor packet, use [Skintone And Overlay Compositor](skintone-and-overlay-compositor.md).

## Edge-family authority packet

The main family split is now no longer the whole story. The repo also has enough bounded evidence to keep a first edge-family matrix instead of leaving all narrow cases under one generic fallback rule.

Use [Edge-Family Matrix](edge-family-matrix.md) for the stricter row-by-row packet. Keep this section as the compact summary layer.

Current concrete edge-family packets:

- [SimGlass Versus Shell Baseline](live-proof-packets/simglass-vs-shell-baseline.md)
- [SimSkin Versus SimSkinMask](live-proof-packets/simskin-vs-simskinmask.md)
- [CASHotSpotAtlas Carry-Through](live-proof-packets/cas-hotspotatlas-carry-through.md)
- [ShaderDayNightParameters Visible-Pass Proof](live-proof-packets/shader-daynight-visible-pass.md)
- [GenerateSpotLightmap And NextFloorLightMapXform](live-proof-packets/generate-spotlightmap-nextfloorlightmapxform.md)
- [RefractionMap Live Proof](live-proof-packets/refractionmap-live-proof.md)

| Edge family | Primary authority today | Additional render-relevant inputs | Fallback boundary | Current confidence |
| --- | --- | --- | --- | --- |
| Sim glass and transparent shell companions | exact selected `CASP` or resolved shell `GEOM` companion, while preserving `GEOM`-side shader identity like `SimGlass` when it exists | projective or ghost-glass helpers, optional overlay-control maps, shell-region and skintone routing only after the family identity is preserved | do not collapse `SimGlass`-like companions into generic skin or plain alpha just because the rest of the shell packet is skin-family | medium |
| Skin-family GEOM seam | exact selected shell `CASP` plus linked `GEOM`, with `SimSkin` kept as the baseline sample-backed geometry-family seam | `Skintone`, `RegionMap`, embedded `MTNF`, parsed `CASP` routing fields | keep `SimSkinMask` attached as adjacent skin-family semantics until a peer geometry branch appears | medium-high |
| Overlay/detail family edge cases | exact selected `CASP` plus compositor-facing metadata | `CompositionMethod`, `SortLayer`, shared atlas rules, skintone interaction, age-gated or burn-mask overlays | do not widen these families into shell authority or generic diffuse-only fallback | medium |
| CAS hotspot and morph atlas branch | editing or morph packet rooted in `CASHotSpotAtlas -> HotSpotControl -> SimModifier -> DMap/BGEO/BOND`, not in ordinary surface slots | `UV1`, modifier routing, geometry deformation resources | never flatten hotspot-atlas evidence into ordinary `diffuse`, `overlay`, or shell-material truth source | medium-high |
| Pelt or animal-edit helper branch | exact selected `CASP` plus helper texture families like `samplerCASPeltEditTexture` where they appear | shared atlas or edit helpers, overlay-like ramps, species-specific skin or pelt behavior | do not collapse edit/pelt helper families into the body-shell skintone packet without stronger live-family proof | medium |
| Lightmap or reveal carry-through inside CAS-adjacent families | explicit family-local shader identity first, then selected CAS or Sim ownership context second | `RefractionMap`, `ShaderDayNightParameters`, `LightsAnimLookupMap`, `NextFloorLightMapXform`, `GenerateSpotLightmap`, weak `SimGhostGlassCAS` carry-through | do not reinterpret projection or lightmap helpers as ordinary surface-slot truth just because they show up near CAS-family names | medium |

Current safe reading:

- the edge-family question is now narrower than before: it is not "does fallback exist?" but "which narrow family must stay outside the ordinary shell or overlay fallback story?"
- `SimGlass`, `CASHotSpotAtlas`, and lightmap or reveal helpers all have enough structure to stay separate in docs and diagnostics
- `samplerCASPeltEditTexture` is still weak as a broad gameplay family, but already strong enough to preserve as a helper branch instead of flattening into standard surface logic

## `SimSkin` versus `SimSkinMask`

This seam is now narrow enough to treat as a bounded authority question rather than a vague unknown.

What is now strong enough:

- `SimSkin` has direct sample-asset support in bundled `TS4SimRipper` `.simgeom` resources
- repeated `SimSkinMask` hits exist in the local precompiled shader corpus, but as parameter-level signals rather than as a proven peer geometry family
- current implementation plus bundled external tool code expose named `SimSkin` and `SimGlass` paths, but not a peer named `SimSkinMask` authority/export branch
- widening the in-repo sample sweep did not surface a broader local `.simgeom` family outside mirrored `TS4SimRipper` resources
- mainstream tooling checked for this pass still exposes `SimSkin`, `SimGlass`, `ColorShiftMask`, overlays, and burn-mask/image-mask workflows, but not a peer named `SimSkinMask` geometry/export/import branch

Current safe reading:

- keep `SimSkin` as the baseline skin-compatible geometry family
- keep `SimSkinMask` inside adjacent parameter/overlay/skintone-adjacent semantics until a real peer live-asset branch appears
- do not invent a standalone `SimSkinMask` authority node before new live assets justify it

Current implementation anchors:

- [precomp_shader_profiles.json](../../tmp/precomp_shader_profiles.json)
- [precomp_sblk_inventory.json](../../tmp/precomp_sblk_inventory.json)
- [AssetServices.cs](../../src/Sims4ResourceExplorer.Assets/AssetServices.cs)
- [SimSceneComposer.cs](../../src/Sims4ResourceExplorer.Core/SimSceneComposer.cs)
- [MainWindow.xaml.cs](../../src/Sims4ResourceExplorer.App/MainWindow.xaml.cs)

External corroboration:

- [TS4CASTools ClonePackMeshes.cs](https://github.com/CmarNYC-Tools/TS4CASTools/blob/67d00ebb9016092b8f64ef94ec7ffb5329cf3342/src/CASTools/ClonePackMeshes.cs#L2313-L2315)
- [TS4SimRipper Enums.cs](https://github.com/CmarNYC-Tools/TS4SimRipper/blob/862a32949e5156b371e2d2f83de4e37e0bb1afcc/src/Enums.cs#L3065-L3079)
- [TS4SimRipper ColladaDAE.cs](https://github.com/CmarNYC-Tools/TS4SimRipper/blob/862a32949e5156b371e2d2f83de4e37e0bb1afcc/src/ColladaDAE.cs#L3636-L3662)
- [TS4 Skininator](https://modthesims.info/d/568474/ts4-skininator-updated-8-6-2018-version-1-12.html)
- [TS4 Skin Converter V2.3](https://modthesims.info/d/650407/ts4-skin-converter-v2-3-enable-cc-skintones-in-cas.html)
- [Sims 4 Studio 3.2.4.7 release notes](https://sims4studio.com/thread/29786/sims-studio-windows-star-open)

Current concrete packet:

- [SimSkin Versus SimSkinMask](live-proof-packets/simskin-vs-simskinmask.md)

## What still remains open

- exact authority order between `CASP`, embedded `GEOM/MTNF`, `MATD/MTST`, `Skintone`, and `RegionMap`
- which family groups require explicit material definitions and which truly live on field-routing floors
- how often live body/head shell families upgrade from parsed `CASP` floor to explicit `MATD`
- how often shell-specific end-to-end asset graphs reach explicit `MATD`, not just composer-level synthetic shell scenes
- where embedded `MTNF` actually changes authority order inside shell families
- whether any wider live-asset evidence ever justifies promoting `SimSkinMask` into a standalone `GEOM` or material-authority branch
- full overlay/detail family authority tables by real live assets

## Recommended next work

1. Find a genuinely new live asset corpus outside the current mirrored `TS4SimRipper` sample set.
2. Build the first per-family authority table for:
   - body shell
   - head shell
   - hair
   - accessory
   - shoes
   - one overlay/detail family
3. After that, separate “current implementation-safe baseline” from “game-faithful proven order” in a stricter matrix.
