# Shader Family Registry

This document is the family-level companion for the shared TS4 material guide when the task is specifically about shader/material families, texture-slot vocabulary, UV-space hints, or narrow family-specific edge cases.

Primary rule for this document:

- external references, creator tooling, and local snapshots of external tooling are the evidence base
- current repo code is not the truth source for family semantics
- current repo behavior is recorded only as an implementation boundary or failure boundary

Related docs:

- [Knowledge map](../../knowledge-map.md)
- [Workflows index](../README.md)
- [Material pipeline deep dives](README.md)
- [Corpus-Wide Family Priority](corpus-wide-family-priority.md)
- [Sim Archetype Material Carrier Census](sim-archetype-material-carrier-census.md)
- [CAS Carrier Census Baseline](cas-carrier-census-baseline.md)
- [CASPart Linkage Census Baseline](caspart-linkage-census-baseline.md)
- [CASPart GEOM Shader Census Baseline](caspart-geom-shader-census-baseline.md)
- [Family Sheets](family-sheets/README.md)
- [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [Edge-Family Matrix](edge-family-matrix.md)
- [Skintone And Overlay Compositor](skintone-and-overlay-compositor.md)
- [Package, Runtime, And Scene Bridge Boundary](package-runtime-scene-bridge-boundary.md)
- [Helper-Family Package Carrier Boundary](helper-family-package-carrier-boundary.md)
- [Helper-Family Carrier Plausibility Matrix](helper-family-carrier-plausibility-matrix.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)
- [Open questions](../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Scope status (`v0.2`)

```text
Shader Family Registry
├─ External family identity packet ~ 76%
├─ CAS/Sim GEOM-side family packet ~ 72%
├─ Lightmap / projection edge packet ~ 79%
├─ Live representative asset packet ~ 83%
├─ Runtime shader-interface corpus ~ 88%
└─ Full per-family slot + param + UV contract ~ 54%
```

What this doc is for:

- keep family-specific research out of the already-dense shared guide
- preserve a family registry that is driven by external evidence, not by current decoder assumptions
- separate `proved family identity`, `practical creator/tool evidence`, `local external snapshots`, and `still-open gaps`
- keep shader-family semantics universal even when the same family is discovered through different domain-specific authority paths
- support corpus-wide prioritization of families before any narrow pack-local fixture lane is chosen

What this doc is not:

- not a statement that the current repo preview already implements these families correctly
- not a claim that every family here has full in-game slot math, UV math, or compositor parity
- not a reason to create separate `BuildBuy`, `CAS`, or `Sim` shader variants for the same family

## Evidence order for this document

Use family claims in this order:

1. format/reference pages and creator-facing documentation that identify a real TS4 resource or family role
2. community tooling that treats a family as a real behavioral branch
3. local snapshots of external tooling checked into this repo, especially `TS4SimRipper`
4. local live runtime shader-interface captures, but only after a family identity is already externally anchored
5. narrow corroboration from creator forum threads showing practical behavior or failure modes
6. current repo code or local corpus output only as implementation boundary, not as authority

Safe rule:

- if the family identity is only visible in our current decoder or our own corpus dumps, it stays a hypothesis or a local implementation bucket
- if the family identity is visible in external tooling, creator workflows, or external references, it can be documented as a real branch even when the exact math is still open
- if the family identity is already externally anchored, the runtime interface corpus can now tighten slot, parameter, and geometry-contract wording without waiting for full package-field ownership
- domain usage is evidence about where a family appears and how it is discovered
- domain usage is not evidence that the same family needs different renderer logic by asset class
- direct character-side carrier counts can raise the urgency of a family row, but they do not replace external family identity
- direct `CASPart` linkage counts can raise the urgency of shell/compositor-family work, but they still do not replace external family identity
- direct `CASPart -> GEOM -> shader family` counts can now raise the urgency of specific character-side families such as `SimSkin` and `SimGlass`, but they still do not replace external family identity or full whole-`CAS` closure

Current narrow evidence sheets:

- [SimSkin, SimGlass, And SimSkinMask](family-sheets/simskin-simglass-simskinmask.md)
- [SimGlass Build/Buy Evidence Order](simglass-buildbuy-evidence-order.md)
- [SimGlass Domain Home Boundary](simglass-domain-home-boundary.md)
- [SimGlass Character Transparency Boundary](simglass-character-transparency-boundary.md)
- [SimGlass Character Transparency Order](simglass-character-transparency-order.md)
- [Character Transparency Open Edge](character-transparency-open-edge.md)
- [Character Transparency Evidence Ledger](character-transparency-evidence-ledger.md)
- [Object Glass And Transparency](family-sheets/object-glass-and-transparency.md)
- [ShaderDayNight Evidence Ledger](shader-daynight-evidence-ledger.md)
- [Generated-Light Evidence Ledger](generated-light-evidence-ledger.md)
- [Projection, Reveal, And Generated-Light Boundary](projection-reveal-generated-light-boundary.md)
- [Refraction Evidence Ledger](refraction-evidence-ledger.md)
- [Refraction Bridge Fixture Boundary](refraction-bridge-fixture-boundary.md)
- [CASHotSpotAtlas](family-sheets/cas-hotspot-atlas.md)
- [Projection, Reveal, And Lightmap Families](family-sheets/projection-reveal-lightmap.md)
- [ShaderDayNightParameters](family-sheets/shader-daynight-parameters.md)
- [GenerateSpotLightmap And NextFloorLightMapXform](family-sheets/generate-spotlightmap-nextfloorlightmapxform.md)
- [Runtime Shader Interface Baseline](runtime-shader-interface-baseline.md)

Current synthesis layer:

- [Edge-Family Matrix](edge-family-matrix.md)
- [Package, Runtime, And Scene Bridge Boundary](package-runtime-scene-bridge-boundary.md)
- [Helper-Family Package Carrier Boundary](helper-family-package-carrier-boundary.md)
- [Helper-Family Carrier Plausibility Matrix](helper-family-carrier-plausibility-matrix.md)

Current concrete live-proof packets:

- [SimSkin Body/Head Shell Authority](live-proof-packets/simskin-body-head-shell-authority.md)
- [SimGlass Versus Shell Baseline](live-proof-packets/simglass-vs-shell-baseline.md)
- [SimSkin Versus SimSkinMask](live-proof-packets/simskin-vs-simskinmask.md)
- [CASHotSpotAtlas Carry-Through](live-proof-packets/cas-hotspotatlas-carry-through.md)
- [ShaderDayNightParameters Visible-Pass Proof](live-proof-packets/shader-daynight-visible-pass.md)
- [ShaderDayNight Runtime Cluster Candidate Floor](live-proof-packets/shader-daynight-runtime-cluster-candidate-floor.md)
- [GenerateSpotLightmap And NextFloorLightMapXform](live-proof-packets/generate-spotlightmap-nextfloorlightmapxform.md)
- [RefractionMap Live Proof](live-proof-packets/refractionmap-live-proof.md)

## Current bridge limit

The strongest global blocker is now narrower than "family contracts are missing."

Current safe reading:

- the package-side carrier order is materially clearer:
  1. `MATD`
  2. `MTST`
  3. `Geometry` / `Model` / `ModelLOD`
- the runtime side is also materially clearer:
  - per-shader interface contracts are now a real checked-in layer
  - helper-family rows now have narrowed runtime cluster candidates
- the missing join is still explicit:
  - `package carrier -> runtime shader cluster/family -> scene/pass context`

That means this registry can now say more about:

- which family claims are plausible
- which ownership claims are still premature

It still cannot honestly say, for the weak helper rows:

- which authored package carrier owns the family
- which draw/pass context closes the final family reading

The current compact offline comparison layer for that limit is now:

- [Helper-Family Carrier Plausibility Matrix](helper-family-carrier-plausibility-matrix.md)

## External-backed family packets

### 1. `SimSkin`

Strongest evidence:

- local external `TS4SimRipper` snapshot explicitly names `SimSkin` in [Enums.cs](../../references/external/TS4SimRipper/src/Enums.cs)
- bundled `.simgeom` samples inside [TS4SimRipper Resources](../../references/external/TS4SimRipper/src/Resources) currently check out as `SimSkin` on body/head/waist sample meshes
- creator tooling and creator-facing docs consistently treat skin-family meshes as a real separate branch rather than generic object-style diffuse materials

Safe reading:

- `SimSkin` is a real GEOM-side character skin family
- it is the current safest baseline branch for body/head shell geometry-family reasoning
- it should not be flattened into a generic `alpha` or `standard surface` bucket just because the current repo preview lacks exact parity

What is still open:

- exact slot contract for every `SimSkin` material variant
- exact ranking between GEOM-side shader identity, embedded `MTNF`, parsed `CASP` routing, and skintone/compositor layers

### 2. `SimGlass`

Strongest evidence:

- local external `TS4SimRipper` snapshot explicitly names `SimGlass` in [Enums.cs](../../references/external/TS4SimRipper/src/Enums.cs)
- [ColladaDAE.cs](../../references/external/TS4SimRipper/src/ColladaDAE.cs) and [PreviewControl.cs](../../references/external/TS4SimRipper/src/PreviewControl.cs) preserve a separate branch for glass-like mesh handling/export
- creator-facing TS4 guidance treats glass-like character shaders as visibly different behavior, not just a naming accident

Safe reading:

- `SimGlass` is a real narrow transparent character family
- it should stay separate from `SimSkin`
- it should also stay separate from a generic “just alpha-blended” fallback story
- current creator-facing evidence is now also strong enough to keep `SimGlass` separate from `SimAlphaBlended` in family provenance
- `CAS/Sim` is still the current semantic home for `SimGlass`
- current `Build/Buy` appearances of `SimGlass` should be read as a bounded carry-over domain, not as the family's primary semantic home
- current safe weighting for that carry-over now lives in:
  - [SimGlass Build/Buy Evidence Order](simglass-buildbuy-evidence-order.md)
  - [SimGlass Domain Home Boundary](simglass-domain-home-boundary.md)
  - [SimGlass Character Transparency Boundary](simglass-character-transparency-boundary.md)
  - [SimGlass Character Transparency Order](simglass-character-transparency-order.md)
  - [Character Transparency Open Edge](character-transparency-open-edge.md)

What is still open:

- exact relation between `SimGlass`, projective helpers, and ghost-glass edge names
- exact material-slot semantics across different glass-like character cases
- exact family-order placement of `SimEyes` relative to the current `SimGlass` / `SimAlphaBlended` packet

### 3. `SimSkinMask`

Strongest evidence:

- local external and creator-tool packets support skin-mask semantics as part of skintone, overlay, burn-mask, or skin-detail workflows
- checked mainstream tooling in the current research packet did not expose a peer asset/export/import branch equivalent to `SimSkin` or `SimGlass`
- creator tools like `Skininator`, `Skin Converter`, and recent Sims 4 Studio notes keep masks in overlay/skintone workflows rather than as a separate geometry family

Safe reading:

- `SimSkinMask` should currently be treated as adjacent skin-family semantics, not as a proven standalone GEOM-side family
- the safe default is to keep it attached to the skin/compositor packet until a real live geometry branch is found

What is still open:

- whether wider live assets outside the current local sample/tool corpus ever expose `SimSkinMask` as a distinct geometry or export family

### 4. `CASHotSpotAtlas`

Strongest evidence:

- [TS4 MorphMaker / DMap tutorial](https://modthesims.info/t/613057) identifies `CASHotSpotAtlas` as an EA atlas mapped to `UV1`
- [Pointed Ears as CAS Sliders](https://db.modthesims.info/showthread.php?t=596028) describes the same atlas as hotspot-routing input tied to slider or modifier logic
- both sources connect the atlas to `HotSpotControl`, `SimModifier`, and morph/edit workflows rather than to ordinary surface color sampling

Safe reading:

- `CASHotSpotAtlas` is an edit or morph atlas family
- it belongs under the CAS editing/morph branch, not under ordinary surface-material slots
- if it shows up in render/profile archaeology, preserve it as helper provenance instead of coercing it into `diffuse`, `overlay`, or `alpha`

What is still open:

- why the name carries through some non-obvious render/profile packets
- whether any runtime render path samples it directly outside editing or morph workflows

### 5. `GenerateSpotLightmap` and `NextFloorLightMapXform`

Strongest evidence:

- a Mod The Sims thread on TS4 lighting/lightmaps groups `NextFloorLightMapXform` with `GenerateSpotLightmap` and related lightmap-generation names
- the name family itself is strongly lightmap-oriented rather than ordinary surface-slot vocabulary

Safe reading:

- `GenerateSpotLightmap` is a lightmap/helper family
- `NextFloorLightMapXform` is much safer to treat as a lightmap transform/helper than as a base texture slot
- this branch belongs in `lighting / projection / generated light` territory, not in ordinary surface-material decoding
- [Generated-Light Evidence Ledger](generated-light-evidence-ledger.md) now keeps external corroboration, local carry-through, and bounded synthesis separate for this row
- [Helper-Family Package Carrier Boundary](helper-family-package-carrier-boundary.md) now also keeps the authored-carrier limit explicit:
  - generated-light is still not a direct `MATD` ownership claim
  - `MTST` is still not the default owner just because object-side seams exist

What is still open:

- exact matrix semantics
- exact crossover between this branch and any CAS-adjacent glass or projected edge cases

### 6. `RefractionMap`

Strongest evidence:

- name lineage and older Sims shader references strongly support refraction as its own branch rather than a synonym for ordinary diffuse material
- creator-facing family names and shader vocabulary in the same engine lineage make it unsafe to flatten `RefractionMap` into generic surface semantics

Safe reading:

- `RefractionMap` belongs under projection/refraction families
- unresolved family-local names near it, including `tex1`, should stay in that branch until stronger TS4-specific proof appears
- [Refraction Evidence Ledger](refraction-evidence-ledger.md) now keeps external corroboration, local survey/bridge evidence, and bounded synthesis separate for this row
- package-side carrier counts still do not close authored ownership for this row without the missing runtime-context join

What is still open:

- exact sampled slots and math in TS4
- exact visible-pass behavior for family-local helper names around refraction

### 7. `ShaderDayNightParameters` and reveal/light helpers

Strongest evidence:

- the name clearly signals a layered day/night or lighting-aware family rather than an ordinary one-pass surface material
- engine-lineage documentation supports the idea that reveal maps are real helper inputs rather than aliases for diffuse textures
- creator-facing light and reveal behavior in TS4 makes it unsafe to collapse these names into ordinary base-color slots

Safe reading:

- `ShaderDayNightParameters` is a layered family with reveal/light helper behavior
- `samplerRevealMap` should stay documented as reveal/helper provenance
- `LightsAnimLookupMap` should stay documented as a narrow light-lookup helper
- [ShaderDayNight Evidence Ledger](shader-daynight-evidence-ledger.md) now keeps external corroboration, local carry-through, and bounded synthesis separate for this row
- [ShaderDayNight Runtime Cluster Candidate Floor](live-proof-packets/shader-daynight-runtime-cluster-candidate-floor.md) now narrows the runtime side too:
  - `F04` is the leading parameter-heavy helper/combine candidate
  - `F05` is the nearest color-aware sibling comparator
- [Helper-Family Package Carrier Boundary](helper-family-package-carrier-boundary.md) now also keeps the authored-carrier limit explicit:
  - `ShaderDayNightParameters` is still not a proved direct `MATD` family
  - `MTST` is still not a proved family owner

What is still open:

- exact TS4 visible-pass math
- exact slot-by-slot contract for reveal and light helper textures

### 8. Object-side glass and transparency

Strongest evidence:

- creator-facing object workflows in Sims 4 Studio explicitly separate object-glass edits from threshold or blended transparency edits
- [DaraSims glass-object tutorial](https://darasims.com/stati/tutorial/tutor_sims4/2980-urok-po-sozdaniyu-steklyannyh-obektov-pri-pomoschi-programmy-sims-4-studio.html) names `GlassForObjectsTranslucent` directly as the object shader switch
- [DaraSims transparency tutorial for objects without `AlphaBlended`](https://darasims.com/stati/tutorial/tutor_sims4/3196-dobavlenie-obektam-prozrachnosti-gde-net-parametra-alphablended-v-sims-4-studio.html) treats `AlphaMap` plus `AlphaMaskThreshold` as a separate object-side path
- [DaraSims transparent-curtain tutorial](https://darasims.com/stati/tutorial/tutor_sims4/2984-sozdanie-prozrachnyh-shtor-v-sims-4.html) keeps `AlphaBlended` as a further semi-transparent object path
- [Sims_3:Shaders](https://modthesims.info/wiki.php?title=Sims_3%3AShaders) preserves `GlassForObjectsTranslucent` as a distinct glass-family shader with refraction and alpha helpers

Safe reading:

- object-side transparent content is not one generic family
- `GlassForObjectsTranslucent` is a better current semantic home for object glass than character-side `SimGlass`
- threshold or cutout transparency and blended curtain-like transparency should stay separate until stronger live TS4 closure says otherwise

What is still open:

- exact TS4 live-fixture closure for the object-glass family
- exact ranking between object-glass family semantics and object-state or variant selectors inside `MATD` or `MTST`

## CAS/Sim GEOM-side packet from local external snapshots

The strongest local evidence in this repo for character-family shader identity is not our own preview code. It is the checked-in external snapshot:

- [TS4SimRipper README](../../references/external/TS4SimRipper/README.md)
- [TS4SimRipper Enums.cs](../../references/external/TS4SimRipper/src/Enums.cs)
- [TS4SimRipper ColladaDAE.cs](../../references/external/TS4SimRipper/src/ColladaDAE.cs)
- [TS4SimRipper PreviewControl.cs](../../references/external/TS4SimRipper/src/PreviewControl.cs)
- [TS4SimRipper Resources](../../references/external/TS4SimRipper/src/Resources)

What this packet supports safely:

- GEOM-side shader identity is behaviorally meaningful in TS4 creator tooling
- `SimSkin` and `SimGlass` are strong enough to treat as real family branches
- the new direct package-derived `CASPart -> GEOM` census now also shows that `SimSkin = 280983` across `401` packages dominates the currently resolved character-side geometry floor, while `SimGlass = 6048` across `189` packages remains real but much narrower
- `12911` resolved geometry hits already come from external packages, so that character-side floor is no longer a same-package-only packet
- `SimGlass` may survive into `Build/Buy` research as a bounded carry-over branch without becoming the default label for transparent objects
- bundled sample geometries are enough to anchor the existence of a skin-family branch even if they are not yet a wide live-asset survey
- the lack of a peer `SimSkinMask` geometry/export branch in this tooling packet is itself a useful negative result

What this packet does not prove:

- full in-game frequency of any family
- exact family slot tables
- exact compositor math

## Creator-workflow corroboration packet

These sources matter because they show real failure modes and practical invariants:

- Sims 4 Studio release notes and forum posts
- creator troubleshooting threads on Mod The Sims
- skin and overlay tooling such as `Skininator` and `Skin Converter`

What this packet supports safely:

- `SharedUVMapSpace`, `uv_1`, atlas overlap, and `ColorShiftMask` are real creator-visible constraints
- `CompositionMethod` and `SortLayer` are practical compositor controls
- body-type slots are real identity boundaries, not interchangeable material families
- mask-bearing skin workflows belong with skintone or overlay logic more than with standalone mesh-family branches

## Narrow live representative packet

This document intentionally keeps the “representative live asset” packet weaker than the external family-identity packet.

Reason:

- the current repo-local coverage dumps are useful for triage
- but they are downstream of the current extractor and preview path
- therefore they are not strong enough to promote a family from hypothesis to truth by themselves

Safe use of the live packet:

- use it to choose which family to investigate next
- use it to collect candidate package roots for manual comparison
- do not use it as the primary proof that a slot contract is correct
- Build/Buy survey-level hits are stronger than one-off `precomp` name archaeology, but they still remain below row-level asset proof
- adjacent-root bridges are useful only when they keep a narrow family neighborhood bounded; they do not close direct family semantics by themselves
- fixtures prove that shared family semantics survive different discovery routes
- fixtures do not justify asset-bound shader subclasses

## Current repo boundary

Current repo code may still be useful in three narrow ways:

1. to show which family names the current implementation is already trying to preserve
2. to show which families are currently being flattened into broad fallback buckets
3. to explain why a preview result is only approximate or wrong

What the repo is not allowed to do in this document:

- define family truth
- define authoritative slot semantics
- define authoritative UV semantics

Safe wording for current repo observations:

- “current preview approximates this family as ...”
- “current decoder collapses these names together ...”
- “current implementation still lacks evidence for ...”

Unsafe wording:

- “this family means X because the decoder maps it that way”
- “the slot is authoritative because current code uses it”

## What still remains open

- full per-family slot tables from wider live assets
- full per-family UV selectors and transform rules
- exact alpha/blend/compositor rules by family
- wider CAS/Sim live family packet outside the current `TS4SimRipper` sample set
- stronger TS4-specific corroboration for reveal, refraction, and generated-light helper names

## Recommended next work

1. Extend the external-backed family packet before extending the repo-backed family packet.
2. Use the completed character-side family floor to reprioritize the family sheets:
   - keep `SimSkin` and character-foundation work ahead of narrower `SimGlass` carry-over lanes
   - keep `SimGlass`, `RefractionMap`, and other edge rows explicit, but lower than the `SimSkin` and compositor-authority seam
- keep overlay/detail reprioritization under the same `SimSkin`/compositor seam now that `0x44`, `0x41`, `0x6D`, `0x6F`, `0x52`, and `0x80` have concrete packet layers
3. For each sheet, separate:
   - externally proved identity
   - creator-workflow corroboration
   - local external snapshot evidence
   - current repo failure boundary
4. After that, build per-family slot tables for implementation starting from [SimSkin Body/Head Shell Authority](live-proof-packets/simskin-body-head-shell-authority.md) and only then widening back to narrower edge families.
