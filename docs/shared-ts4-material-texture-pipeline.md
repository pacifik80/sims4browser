# Shared TS4 Material, Texture, And UV Pipeline

This document is the working synthesis for the shared The Sims 4 material, texture, shader, and UV pipeline.

Primary rule:

- external references, creator tooling, and external snapshots are the authority base
- this guide turns that evidence into one shared architectural model
- current repo code is useful only as implementation boundary, failure evidence, or a place where the current approximation is visible

Use it when the task is about texture linkage, shader/material decoding, UV routing, viewport rendering, or export for `BuildBuy`, `CAS`, or `Sim`.

When local package probes or manual UI checks reveal shader-family behavior that is not publicly documented, record it under
[Shader Family Registry / Local Confirmed Findings](workflows/material-pipeline/shader-family-registry.md#local-confirmed-findings).
Those entries are allowed only as clearly labelled project findings, not as public EA/Maxis specification claims.

Related docs:

- [Knowledge map](knowledge-map.md)
- [Workflows index](workflows/README.md)
- [Material pipeline deep dives](workflows/material-pipeline/README.md)
- [Build/Buy material authority matrix](workflows/material-pipeline/buildbuy-material-authority-matrix.md)
- [Shader Family Registry](workflows/material-pipeline/shader-family-registry.md)
- [Skintone And Overlay Compositor](workflows/material-pipeline/skintone-and-overlay-compositor.md)
- [Architecture](architecture.md)
- [Sim domain roadmap](sim-domain-roadmap.md)
- [CAS/Sim material authority matrix](workflows/material-pipeline/cas-sim-material-authority-matrix.md)
- [TS4 UV and Material Mapping](references/codex-wiki/03-implementation-guides/05-TS4_UV_AND_MATERIAL_MAPPING.md)
- [Build/Buy object pipeline](references/codex-wiki/02-pipelines/01-buildbuy-object-pipeline.md)
- [CAS part pipeline](references/codex-wiki/02-pipelines/02-cas-part-pipeline.md)
- [Full Sim and morph pipeline](references/codex-wiki/02-pipelines/03-full-sim-and-morphs.md)
- [Source map and trust levels](references/codex-wiki/04-research-and-sources/01-source-map.md)

## Why this exists

The repo already had useful notes, but they were split across architecture docs, pipeline notes, and reverse-engineering references. That made it too easy to drift into domain-specific handling like:

- one texture path for `BuildBuy`
- another for `CAS`
- another for `Sim`

That is not the target architecture.

The correct architectural stance is:

- discovery roots may differ by domain
- once material-relevant resources are found, all domains must converge on one shared pipeline
- shader family, texture role, UV channel, UV transform, alpha mode, and layering rules must be interpreted the same way everywhere unless a real format rule proves otherwise

This matches both the current repo direction and the way the game content is structured: object content, CAS content, and assembled Sims use different identity and discovery paths, but they still resolve into the same broad class of render inputs.

## Universal shader contract

The project is not trying to build:

- one shader system for `BuildBuy`
- another for `CAS`
- another for `Sim`

The project is trying to build:

- one shared shader/material contract
- one shared canonical material IR
- one shared decode/render/export path after authoritative inputs are found

Safe reading:

- `BuildBuy`, `CAS`, and `Sim` are discovery and authority domains
- they are not separate shader domains
- family-local authority order may differ before decoding
- shader semantics must not fork by asset class unless a real format rule proves that a shader family itself is different

What asset fixtures are for:

- proving that a family or helper input really exists
- proving where authoritative inputs come from
- proving where the current implementation still flattens or loses provenance

What asset fixtures are not for:

- inventing asset-bound shader variants
- implying that one object class needs its own renderer branch just because its discovery path is different

## Confidence model

Every rule in this guide should be read with one of these confidence levels:

- `spec-backed`: explicit on a format/reference page
- `reference-code-backed`: visible in mature community tooling or repo code
- `fixture-backed`: verified on real package fixtures or live app diagnostics
- `community-backed`: repeated creator/modder guidance that is useful, but not a formal binary spec
- `inference`: strong synthesis from multiple sources, but not directly specified
- `open gap`: not proven enough yet to be treated as authoritative

## Coverage map (`v1.2`)

This section should be updated after every research pass.

Status legend for document coverage:

- `closed for implementation baseline`: strong enough to drive the shared repo architecture today
- `partial`: the section is structurally useful and bounded, but not yet complete or authoritative in every family
- `dark zone`: the section has a known topic and some boundary markers, but the exact TS4 contract is still weak

Current section status:

| Section | Status | What that means right now |
| --- | --- | --- |
| `Architectural contract` | `closed for implementation baseline` | the cross-domain rule is now stable: discovery may differ, but post-discovery material handling must converge |
| `Shared pipeline stages` | `closed for implementation baseline` | the repo now has a stable stage model for shared material/render handling |
| `Resource-role registry` | `partial` | major resource identities are documented, but some roles remain weaker than others, especially `VPXY` |
| `Proven resource-field rules` | `partial` | Build/Buy, `CASP`, `RegionMap`, `GEOM/MTNF`, `Skintone`, and core compositor inputs are substantially narrowed, a first bounded `CAS/Sim` material-input graph exists, character family groups are better separated, worn-slot families have a stronger exact-part-link boundary, and shell families now have a tighter default/nude, skintone, material-truth-source, and `MTNF` boundary, but full authority order is not closed |
| `Texture-role registry` | `partial` | canonical texture-role vocabulary is useful, but not yet complete for all shader families |
| `UV and transform rules` | `partial` | multi-UV, shared atlas, `SharedUVMapSpace`, and edit/morph UV facts are well bounded, but full per-family UV/transform coverage is still missing |
| `Layering and compositing rules` | `partial` | Build/Buy, CAS, and Sim layering structure is documented more strongly now, and a dedicated skintone/overlay compositor deep-dive exists, but exact end-to-end compositor math is still incomplete |
| `Registry requirements` and `v0 shader-family registry` | `partial` | the document now has a real registry baseline plus a linked external-first family deep-dive, but it is still support-oriented rather than fully authoritative |
| `Raw/unmapped taxonomy`, `edge-case matrix`, and `P1 target sheets` | `partial` | this space is no longer a blind bucket; the remaining gaps are narrow and named |
| `Authority and fallback matrix` | `partial` | the first cross-domain matrix exists, separates the main character family groups, gives a stronger worn-slot boundary for hair/accessory/footwear, treats body/head shell as a tighter foundation branch with a narrower field-routing-versus-material-definition boundary, and now has a first bounded edge-family packet, but full authority proofs still need more work |
| `Current repo status`, `practical rules`, and `validation checklist` | `closed for implementation baseline` | these sections are now strong enough to guide implementation packets and reviews |
| `Open gaps` | `dark zone` | these are the still-real holes that block full parity or full authority claims |

Current darkest pockets:

- full cross-domain shader-family slot/parameter contract from live assets, especially outside the current sampled Build/Buy packet
- full per-family `CAS/Sim` authority order, especially inside body/head shell material-definition versus field-routing rules, embedded `MTNF`, and overlay/detail families after the worn-slot boundary
- exact skintone/compositor math
- full per-family UV transform matrix
- exact TS4-specific `VPXY` traversal contract

What this pass improved:

- the document now has an explicit self-status map instead of forcing the reader to infer maturity from scattered notes
- `VPXY` is now easier to frame as a bounded dark zone rather than an undefined mystery spread across the whole guide
- `CAS/Sim` material linkage is now described as a bounded input graph instead of a loose set of unrelated facts
- `CAS/Sim` is no longer treated as one undifferentiated blob; the guide now has a first family-level split for shell, head, footwear, hair/accessory, and overlay behavior
- worn-slot families now have a tighter rule boundary: exact part identity first, cross-package geometry companions allowed, material-definition before manifest approximation, and no skintone retargeting
- body/head shell now has a tighter rule boundary too: default or nude shell gating, head-as-mergeable-shell rather than body replacement, shell-only skintone targeting, and direct source pointers in the section
- shell material truth source is now narrower too: explicit material definitions are a real upgrade path when geometry companions expose them, but current shell baseline is still parsed `CASP` field-routing plus shell-scoped approximation rather than `MATD` as a universal prerequisite
- the remaining `MTNF` ambiguity is now also tighter: `MTNF` clearly exists as a GEOM-side material carrier, but current repo shell fixtures rarely exercise it and the current GEOM parser skips the embedded payload rather than decoding it
- `MTNF` is now also better bounded as behaviorally relevant TS4 payload, not only a structural chunk: current creator tooling and external code both treat broken embedded shader/material data as something that can affect saved `GEOM` behavior
- TS4-specific creator evidence now also ties `GEOM`-side shader identity to visible family behavior: `SimGlass`, `SimSkin`, `SimEyes`, and `SimAlphaBlended` are all treated as practical mesh/shader choices rather than abstract names
- the `SimSkin` versus `SimSkinMask` asymmetry is now one step tighter: current repo/local external code exposes named `SimSkin` and `SimGlass` paths, while current local and external evidence still frames `SimSkinMask` as parameter-level plus overlay/skintone-adjacent semantics rather than a proven standalone geometry family
- that asymmetry is now also checked against a wider in-repo corpus and a broader mainstream toolchain packet: no additional local geometry branch surfaced beyond the duplicated `TS4SimRipper` sample set, and no checked mainstream TS4 tool source in hand exposes a peer named `SimSkinMask` asset/export/import branch
- the densest `CAS/Sim` authority packet is now split into its own linked subdocument, so the main guide can stay cross-domain and the family-specific matrix can keep growing without turning this file into one giant authority dump
- the shader-family packet is now also split into its own linked deep-dive, so the shared guide can keep the cross-domain rules while live family tables and representative asset packets grow separately
- the densest skintone/compositor packet is now also split into its own linked deep-dive, so the main guide can keep the cross-domain rule while the exact boundary between `routing`, `overlay/detail` families, `tan/burn`, and `exact blend math` evolves separately

## Progress tree (`v1.1`)

Use this tree as the quick navigation/status layer for the guide.

If the question becomes authority-specific, use the narrower companions instead of continuing to expand this file:

- [Build/Buy Material Authority Matrix](workflows/material-pipeline/buildbuy-material-authority-matrix.md) for object-side authority and object/material linkage
- [CAS/Sim Material Authority Matrix](workflows/material-pipeline/cas-sim-material-authority-matrix.md) for narrow `CAS/Sim` authority detail

Legend:

- `✓` closed enough for current implementation baseline
- `~ NN%` partially closed; useful and bounded, but still incomplete
- `✗` not meaningfully started yet as a repo-quality proof packet
- reporting color convention:
  - `green` for `100%`
  - `yellow` for anything `> 30%` and `< 100%`
  - `red` for anything `<= 30%`

```text
Shared TS4 Material / Texture / UV Pipeline
├─ Architectural contract ✓ 100%
├─ Shared pipeline stages ✓ 100%
├─ Resource-role registry ~ 90%
│  ├─ Build/Buy object path ✓ 100%
│  ├─ CASP / RegionMap / Skintone / GEOM-MTNF roles ~ 97%
│  └─ VPXY traversal contract ~ 35%
├─ Proven resource-field rules ~ 84%
│  ├─ Build/Buy authority order ✓ 100%
│  ├─ CAS/Sim bounded input graph ~ 85%
│  ├─ Worn-slot family boundary ~ 85%
│  ├─ Body/head shell identity boundary ~ 90%
│  ├─ Shell material truth source ~ 76%
│  └─ Shell MTNF prevalence and ranking ~ 59%
├─ Texture-role registry ~ 65%
├─ UV and transform rules ~ 68%
│  ├─ Vertex-layout and multi-UV basics ✓ 100%
│  ├─ SharedUVMapSpace / shared atlas ~ 85%
│  ├─ CAS editing/morph UV branch ~ 80%
│  └─ Full per-family UV transform matrix ✗ 0%
├─ Layering and compositing rules ~ 66%
│  ├─ Build/Buy layering ~ 75%
│  ├─ CAS CompositionMethod / SortLayer ~ 70%
│  └─ Exact skintone/compositor math ~ 38%
├─ Shader-family registry ~ 76%
│  ├─ v0 family registry and representative tables ~ 93%
│  ├─ Raw/unmapped taxonomy ~ 80%
│  ├─ P1 narrow target sheets ~ 78%
│  └─ Live-asset slot/parameter contract ~ 42%
├─ Authority and fallback matrix ~ 78%
│  ├─ Build/Buy base families ~ 82%
│  ├─ CAS/Sim major family groups ~ 78%
│  ├─ Shell field-routing vs explicit material boundary ~ 65%
│  └─ Edge-family full matrix ~ 46%
├─ Current repo status ✓ 100%
├─ Practical implementation rules ✓ 100%
├─ Validation checklist ✓ 100%
└─ Open gaps ~ 53%
   ├─ Full shader-family live-asset contract ~ 42%
   ├─ Full per-family CAS/Sim authority order ~ 55%
   ├─ Exact skintone compositor math ~ 38%
   ├─ Full UV transform matrix ✗ 0%
   └─ TS4-specific VPXY traversal contract ~ 20%
```

## Architectural contract

### 1. Domain-specific discovery is allowed

The discovery roots differ by domain:

- `BuildBuy`: `ObjectCatalog/ObjectDefinition -> MODL -> MLOD`
- `CAS`: `CASP -> LOD/GEOM/material-relevant fields`
- `Sim`: `SimInfo/outfit/body-part selections -> CASP families -> GEOM/CASP/Skintone/RegionMap/material-relevant fields`

This is acceptable.

### 2. Post-discovery material handling must be shared

After discovery, all three domains must flow through the same stages:

1. enumerate material-definition candidates
2. enumerate texture-bearing candidates
3. decode shader/material semantics into shared IR
4. extract canonical texture roles
5. choose UV channel per sampled map
6. apply per-map UV transforms
7. classify alpha, blend, and layer behavior
8. emit one canonical material model for preview and export

No domain-specific renderer branch is allowed after this stage unless a real format rule proves the divergence.

Corollary:

- family-specific authority docs may be domain-heavy because discovery and precedence differ
- that does not authorize family-specific renderer branches by domain
- the output of those docs must still feed one shared shader/material pipeline

### 3. The canonical material layer is the stable handoff

The stable repo contract is the canonical material layer in [Domain.cs](../src/Sims4ResourceExplorer.Core/Domain.cs):

- `CanonicalMaterial`
- `CanonicalTexture`
- `MaterialManifestEntry`
- `UvChannel`
- `UvScale`
- `UvOffset`

Everything downstream of discovery should prefer this shared model.

## Shared pipeline stages

### Stage 0. Resolve the gameplay root

This step is still domain-specific:

- `BuildBuy` resolves identity and scene roots through object resources
- `CAS` resolves the selected `CASP` and its geometry/material candidates
- `Sim` resolves the selected Sim/outfit/body-part graph into `CASP` and geometry candidates

The shared pipeline begins only after the asset graph has identified material-relevant resources.

Safe rule:

- differences before Stage 1 are expected
- differences after Stage 1 must be justified as shader-family facts, not as `BuildBuy/CAS/Sim` special cases

### Stage 1. Collect material-bearing inputs

Material-bearing inputs can come from different places:

- `MATD`
- `MTST`
- embedded `MTNF` inside `GEOM`
- `CASP` material and texture fields
- skintone and region-map inputs that modify or route material behavior

The collector must preserve provenance. Do not flatten everything into one anonymous texture bag.

### Stage 2. Decode material semantics

Decode the source material into shared semantic IR:

- shader/profile name
- texture-slot references
- scalar/vector parameters
- UV-channel selectors
- UV scale and offset
- alpha and cutout hints
- layered or utility slots

The existing repo decoder already points in this direction:

- [MaterialDecoding.cs](../src/Sims4ResourceExplorer.Preview/MaterialDecoding.cs)
- [ShaderSemantics.cs](../src/Sims4ResourceExplorer.Preview/ShaderSemantics.cs)
- [ShaderProfileRegistry.cs](../src/Sims4ResourceExplorer.Preview/ShaderProfileRegistry.cs)

Safe rule:

- when a material family is discovered through different authority paths in different domains, normalize them into the same family semantics unless evidence proves they are actually different families

### Stage 3. Normalize texture roles

Texture roles are shader semantics, not domain semantics.

The engine-facing question is not "is this `BuildBuy` or `CAS`?" but:

- which shader family is this material using
- which texture slots are authoritative for that family
- which slots are optional utility inputs
- which inputs are routing masks rather than directly sampled color maps

Unsafe question:

- "which domain-specific slot table should this asset class use?"

### Stage 4. Route UVs per sampled map

UV choice belongs to texture sampling, not to the mesh globally.

That means:

- one mesh cannot assume "all maps use UV0"
- one material cannot choose one mesh-global UV set and call the job done
- `CanonicalTexture` must keep UV routing per map

### Stage 5. Preserve blend and layering behavior

The canonical material must be able to preserve, even if preview cannot fully emulate:

- alpha/cutout hints
- sort-layer intent
- composition/blending mode
- region-map routing
- skintone routing
- swatch and color-shift masking
- alternate or stateful material sets

### Stage 6. Emit shared canonical output

Preview and export consume the same shared handoff:

- viewport material application
- scene export manifests
- diagnostics
- future validation fixtures

## Resource-role registry

The table below captures the currently proven resource roles that matter for rendering.

| Resource | Type ID | Role in render pipeline | Typical domains | Confidence | Notes |
| --- | --- | --- | --- | --- | --- |
| `MATD` | `0x01D0E75D` | Material definition | `BuildBuy`, `General3D`, some `CAS/Sim` paths | `spec-backed` | Older RCOL references place it directly in object mesh pipelines. |
| `MTST` | `0x02019972` | Material set / stateful material variant input | `BuildBuy` | `spec-backed` | `MLOD` can reference `MATD` or `MTST` by private index. |
| `MODL` | `0x01661233` | Object model root / object geometry container | `BuildBuy`, `General3D` | `spec-backed` | Leads to one or more `MLOD` entries. |
| `MLOD` | `0x01D10F34` | Per-LOD mesh-group linkage to vertex/index/material data | `BuildBuy`, `General3D` | `spec-backed` | Authoritative object mesh-group truth source. |
| `VRTF` | `0x01D0E723` | Vertex declaration / semantic layout | `BuildBuy`, `General3D` | `spec-backed` | Governs dynamic UV and attribute decoding. |
| `GEOM` | `0x015A1849` | Body geometry container | `CAS`, `Sim` | `spec-backed` | Can embed `MTNF` and multiple UV sets. |
| `MTNF` | embedded in `GEOM` | Embedded material hint block | `CAS`, `Sim` | `spec-backed` | Present when `GEOM` embedded material id is non-zero. |
| `CAS Part` / `CASP` | `0x034AEECB` | CAS identity plus material-routing fields | `CAS`, `Sim` | `spec-backed` and `reference-code-backed` | Holds LOD links, texture refs, composition, sort layer, region map, normal/specular/emission, and newer mask fields. |
| `Region Map` | `0xAC16FBEC` | Mesh-region/layer and replacement-geometry routing input | `CAS`, `Sim` | `spec-backed` and `reference-code-backed` | Entries carry region, layer, replacement flag, and linked geometry keys. |
| `Skintone` | `0x0354796A` | Skin routing and compositing input | `Sim`, some `CAS` interactions | `spec-backed` and `reference-code-backed` | Carries base skin-set texture, skin-set overlays, overlay multipliers, colorize values, opacity values, and UI display order. |
| `_IMG` / DST image | `0x00B2D882` | Texture payload | all | `spec-backed` | Common texture carrier outside CAS-specific compressed image types. |
| `LRLE` | `0x2BC04EDF` | CAS diffuse image | `CAS`, `Sim` | `spec-backed` | Modders Reference labels it as a CAS diffuse image. |
| `RLE 2` | `0x3453CF95` | CAS diffuse/shadow image | `CAS`, `Sim` | `spec-backed` | Used for some CAS diffuse and shadow resources. |
| `RLES` | `0xBA856C78` | CAS specular image | `CAS`, `Sim` | `spec-backed` | Community tools use it as specular payload. |
| `VPXY` | `0x736884F1` | Model links / proxy linkage | object and scene ecosystems | `spec-backed` for existence, `open gap` for full TS4 role | Confirmed in RCOL listings, but TS4-specific structural render use is not fully documented. |

## Proven resource-field rules

### Build/Buy object path

The strongest documented base path is:

```text
ObjectCatalog / ObjectDefinition
        ->
      MODL
        ->
      MLOD
        ->
 VRTF / VBUF / IBUF / SKIN / MATD or MTST
        ->
   texture references and shader params
```

Proven rules:

- `Object Definition` is the swatch-level linkage record for a Build/Buy item and can carry the linked `MODL`, footprint, tuning, and material-variant-facing metadata.
- `MLOD` is the authoritative source for mesh-group linkage to `VRTF`, `VBUF`, `IBUF`, `SKIN`, and `MATD/MTST`.
- `MLOD` private indexes are not package-wide TGI lookups.
- `MTST` is stateful material input and must not be silently ignored.
- `MATD` remains the leaf material-definition payload for the surface state actually bound through the model graph.
- `LITE` is a parallel Build/Buy resource family for light/emitter behavior, not a replacement for the main surface-material chain.

The best-supported practical authority order today is:

```text
Object Definition
        ->
  Model / Model LOD
        ->
 Material Set
        ->
Material Definition
```

Treat `LITE` as a sibling branch that can matter for object behavior and lighting, but not as a downstream step in the ordinary material-slot chain.

### `VPXY` is now a bounded scenegraph/linkage helper, not a base material authority node

Current evidence is still too weak for a full TS4 `VPXY` traversal contract, but it is now strong enough to narrow what `VPXY` is not.

What is supported:

- TS4 `RCOL` lists `VPXY` as `Model Links`, which at minimum confirms a scenegraph/object-linkage role inside the TS4 chunk ecosystem ([Sims_4:RCOL](https://modthesims.info/wiki.php?title=Sims_4%3ARCOL))
- older engine-lineage docs for the same type id describe `VPXY` as a link structure that groups object parts and carries references to `GEOM`, `MODL`, `MLOD`, `LITE`, `RSLT`, and `FTPT` ([Sims_3:0x736884F1](https://modthesims.info/wiki.php?title=Sims_3%3A0x736884F1))

Safe current reading:

- `VPXY` belongs to the object/scene linkage branch, not the surface-material leaf branch
- it is plausible that `VPXY` matters for some object ecosystems, modular cases, slots, footprints, or linked-part traversal
- it is not currently needed to state the strongest Build/Buy base authority order, which already closes as `Object Definition -> Model/Model LOD -> Material Set -> Material Definition`
- so `VPXY` should stay preserved as object-graph metadata, but should not be promoted into the base material authority chain without stronger TS4-specific proof

What still remains open:

- the exact TS4 structural payload of `VPXY`
- whether TS4 uses it for all object families or only selected ecosystems
- whether any current repo traversal should actively depend on it outside specific object-linking cases

Confidence:

- `spec-backed` for TS4 existence and category as `Model Links`
- `inference` from engine-lineage material for its broader linkage/proxy role
- `open gap` for exact TS4 traversal semantics

Build/Buy object path confidence:

- `spec-backed`
- `community-backed` for the swatch/material-variant linkage interpretation
- `fixture-backed` in the current repo Build/Buy implementation

### CASP material-routing fields

The local binary-template and reference-code material agree that modern `CASP` carries explicit material-routing fields:

- diffuse key
- shadow key
- `CompositionMethod`
- region map key
- normal map key
- specular map key
- `SharedUVMapSpace` UV-sharing field
- emission map key
- `ColorShiftMask` key in newer versions

Confidence:

- `reference-code-backed` via local snapshots:
  - [CASPart_0x034aeecb.bt](references/external/Binary-Templates/CASPart_0x034aeecb.bt)
  - [TS4SimRipper CASP.cs](references/external/TS4SimRipper/src/CASP.cs)
- `community-backed` for the newer `ColorShiftMask` vocabulary via Sims 4 Studio release notes

Practical meaning:

- `CASP` is not just identity metadata
- it is part of the material-routing contract
- any unified registry must preserve these fields as first-class inputs

Naming note:

- code-backed references expose the field as `SharedUVMapSpace`
- some older reverse-engineering notes and repo text refer to a `NormalUVBodyType`
- `NormalUVBodyType` is not currently confirmed as the upstream field name in Sims4Tools, so treat it as a legacy explanatory label rather than the authoritative identifier
- the safe interpretation today is still that this field carries cross-body UV-space borrowing semantics for normals and related atlas routing

### RegionMap behavior is stronger than a generic routing hint

The local region-map template and reference implementation show that `RegionMap` entries carry:

- a named body region
- a layer value
- an `isReplacement` flag
- one or more linked geometry keys

This means `RegionMap` is not just a vague appearance hint. It is part of the geometry and layering contract for CAS and Sim assembly.

In community reference code, region maps are used to:

- discover which mesh regions a `GEOM` belongs to
- compare region layers such as shoes vs. calf/knee coverage
- reject or replace geometry that should be hidden by higher-priority parts

Confidence:

- `spec-backed` via [RegionMap_0xac16fbec.bt](references/external/Binary-Templates/RegionMap_0xac16fbec.bt)
- `reference-code-backed` via [RegionMap.cs](references/external/TS4SimRipper/src/RegionMap.cs) and [PreviewControl.cs](references/external/TS4SimRipper/src/PreviewControl.cs)

Practical meaning:

- `RegionMap` belongs in the same canonical input graph as `CASP`, `GEOM`, and `Skintone`
- it should not be reduced to an optional note or late viewport-only hint

### GEOM embedded material behavior

`GEOM` has its own render-relevant material behavior:

- it stores its own vertex-format entries and vertex data
- it may carry an embedded material hint block (`MTNF`) when embedded material id is non-zero
- the embedded material id points at `SimSkin` for `morphskin` and `morphskincloth`, and `SimEyes` for `morpheye`
- TS4 tooling evidence also shows that wrong `MTNF` shader-size handling can break saved `GEOM` behavior in-game, so the chunk should be treated as live material payload rather than dead metadata
- TS4 creator-facing shader practice now also gives a stronger family-level reading for `GEOM`-side shader identity:
  - transparency meshes are routinely handled as `SimGlass` rather than plain `SimSkin`
  - eye-specific dynamic behavior is routed through `SimEyes`
  - modern toolchains explicitly add support for `SimAlphaBlended`
- it may carry more than one UV channel

Current GEOM-side shader behavior matrix:

| Shader family | Evidence currently in hand | Observable behavior | Safe architectural reading |
| --- | --- | --- | --- |
| `SimSkin` | format page, local `TS4SimRipper` enums/export code, creator advice for skin-compatible mesh parts | ordinary body or skin-compatible mesh path; also used as the safe non-glass baseline when transparency parts are split out | treat as a real `GEOM`-side shader family, not a repo-local label; preserve it in shader provenance |
| `SimGlass` | local `TS4SimRipper` preview/export code, TS4 creator threads, `S4 CAS Tools` fixes | transparency parts often need separate handling; toolchains export or group them separately and creators split alpha parts into separate mesh groups | treat transparency-capable glass parts as their own shader-family seam; do not collapse them into generic alpha on top of `SimSkin` |
| `SimEyes` | format page (`morpheye` -> `SimEyes`), creator-facing GEOM edits for dynamic eyes | eye-specific behavior and catchlight-style rendering are attached to a distinct mesh/shader path | preserve eye-family shader provenance; do not assume eye meshes are just ordinary skin submeshes |
| `SimAlphaBlended` | modern TS4 creator-tooling support in `CAS Designer Toolkit` | explicit transparency/blended support is treated as a shader capability of mesh groups | keep this as a distinct shader-family candidate rather than folding it into one broad transparency bucket |

What this matrix does and does not prove:

- it does prove that `GEOM`-side shader identity already has practical meaning in TS4 creator/tooling workflows
- it does not yet prove the full in-game slot table or full authority rank of each shader family against `CASP`, `MATD`, and decoded `MTNF`

Current local corpus hint for relative weight:

- `simskin` appears as a repeated param name in the local precompiled corpus (`51` counted occurrences in the current `precomp_sblk_inventory` snapshot)
- `SimSkinMask` also appears as a recurring family-local signal (`12` counted occurrences in the same snapshot)
- `SimGlass` is present, but currently rare in the local precompiled corpus (`1` counted occurrence in the current snapshot)
- `SimEyes` and `SimAlphaBlended` are currently supported by format/tooling evidence in this guide, but are not yet strongly represented in the local precompiled shader snapshot

Safe current reading from that mix:

- `SimSkin` and adjacent skin-mask vocabulary look like core-family signals rather than edge curiosities
- `SimGlass` already looks like a real but narrower edge-family seam
- `SimEyes` and `SimAlphaBlended` should remain preserved in provenance even before they become strong corpus families in the local decoder snapshot

Current authority-seam priority hint:

| Shader family | Current priority | Why |
| --- | --- | --- |
| `SimSkin` | `P1 core` | strong format backing, repeated local corpus presence, and local/external code paths treating it as the ordinary baseline family |
| `SimSkinMask` | `P1 adjacent` | local corpus presence is strong enough that skin-mask semantics should be treated as part of the core Sim-skin family rather than optional trivia |
| `SimGlass` | `P2 edge but real` | corpus prevalence is low, but creator/tooling and local external code both show that it needs a distinct path and cannot be flattened into generic alpha behavior |
| `SimEyes` | `P2 special-family provenance` | format backing and creator evidence are strong enough to preserve it as a special family even before local corpus prevalence is strong |
| `SimAlphaBlended` | `P3 preserve and defer` | enough evidence exists to keep it distinct, but current repo-local corpus support is still weak |

What this priority hint is for:

- it is for implementation ordering and documentation structure
- it is not yet a full proof of in-game global frequency or final authority rank

First bounded authority-seam table for the `P1` skin families:

| Family | Strongest current safe seam | What this already supports | What is still not safe to claim |
| --- | --- | --- | --- |
| `SimSkin` | preserve `GEOM`-side shader identity as the baseline skin-compatible family and carry it into canonical provenance before flattening into generic texture slots | body/skin-compatible meshes can safely use `SimSkin` as the ordinary non-glass baseline; unknown or unsplit cases should not silently erase this family identity | this still does not prove that `SimSkin` outranks explicit `CASP`, decoded `MTNF`, or companion `MATD` in every family or every live asset |
| `SimSkinMask` | treat it as an adjacent skin-family signal attached to the `SimSkin` packet unless stronger live-asset proof shows a separate authority root | implementation can keep skin-mask semantics in the core Sim-skin work queue without inventing a standalone `GEOM` family branch | it is not yet safe to claim that `SimSkinMask` is a first-class `GEOM` authority root equal to `SimSkin`, `SimGlass`, or `SimEyes` |

Why `SimSkinMask` is now better bounded:

- the local precompiled profile snapshot contains repeated parameter-level `SimSkinMask` entries, so it is not just a one-off stray param
- the local inventory snapshot also shows `SimSkinMask` co-present with more skin-adjacent names such as `DetailNormalMapScale`, `CASAmbientIntensity`, and `ShadowDebug`
- local external code and creator tooling still expose explicit branches for `SimGlass`, while a similarly explicit `SimSkinMask` mesh/shader branch is not yet in hand
- the current local code sweep also makes that asymmetry concrete: exact `SimSkinMask` hits are absent across the current repo rendering/composition paths and bundled external tool code checked for this pass, while named `SimSkin` and `SimGlass` branches are present
- external skintone tooling currently frames mask-bearing skin content as `overlay`, `burn mask`, `skin detail`, or `color_shift_mask` style image semantics rather than a separate `GEOM` shader family
- the wider in-repo corpus sweep performed after that still did not surface extra `.simgeom` or comparable local sample families beyond the duplicated `TS4SimRipper` resources, so the current negative finding is not just a byproduct of checking one tiny folder
- the broader mainstream toolchain packet checked after that still points the same way: `TS4CASTools`, `TS4SimRipper`, `Skininator`, and `Sims 4 Studio` all expose `SimSkin`, `SimGlass`, or generic mask/image semantics, but not a peer named `SimSkinMask` geometry/export branch

Current repo-code packet for this seam:

- current CAS graph building still resolves explicit `CASP` texture references and geometry companions first, then emits `ApproximateCasMaterial` as the current portable material floor rather than branching into a dedicated `SimSkinMask` family path
- current Sim scene composition routes skintone only through region-map-aware merged materials and keeps that as `ApproximateCas`-style application data
- current viewport code treats `mask` mostly as generic opacity/mask slot vocabulary, not as a special family authority root
- current repo code also exposes `color_shift_mask` as a generic texture slot and swatch-composition helper, which fits mask semantics without implying a separate `SimSkinMask` geometry branch

Current live/sample asset packet:

- bundled external `TS4SimRipper` sample geometries now provide the first direct sample-asset anchor for `SimSkin`: `9/9` checked body/head/waist `.simgeom` resources carry shader hash `0x548394B9`
- those sample files are:
  - `cuBodyComplete_lod0.simgeom`
  - `cuHead_lod0.simgeom`
  - `puBodyComplete_lod0.simgeom`
  - `puHead_lod0.simgeom`
  - `WaistFiller.simgeom`
  - `yfBodyComplete_lod0.simgeom`
  - `yfHead_lod0.simgeom`
  - `ymBodyComplete_lod0.simgeom`
  - `ymHead_lod0.simgeom`
- in the current local repo snapshots and fixtures, no comparable asset-level `SimSkinMask` geometry branch has been found yet; its current strongest evidence still lives at parameter/profile level
- the same negative result currently extends to the bundled external code and sample packet in-repo: the checked `TS4SimRipper` readers/exporters and sample resources expose `SimSkin`/`SimGlass`, but no peer named `SimSkinMask` geometry/export branch
- the wider workspace asset sweep still does not add a broader peer sample family: outside the mirrored `TS4SimRipper` resource copies, no extra local `.simgeom` corpus in this repo currently surfaces a standalone `SimSkinMask` branch

What that sample packet changes:

- `SimSkin` is now better supported as a real baseline geometry family, not just a format name plus tooling fallback
- `SimSkinMask` now looks even less like a peer geometry root and more like adjacent semantics layered around the skin packet
- this does not close live-asset authority order globally, but it does strengthen the asymmetry between `SimSkin` and `SimSkinMask`
- for the current repo-quality baseline, the safest working assumption is now stronger: promote `SimSkin` as the sample-backed baseline family and keep `SimSkinMask` in parameter/overlay semantics until a real peer branch appears
- widening the checked corpus without finding a counterexample now strengthens that working assumption more than the previous pass did: the current gap is less “maybe we just have not looked at enough obvious places” and more “we may need genuinely new live assets or rarer toolchains before this changes”

Safe reading from repo code:

- current repo implementation already has a bounded skin-family floor through `CASP` texture refs, geometry companions, region maps, and skintone routing
- current repo implementation still does not expose a dedicated `SimSkinMask` branch comparable to explicit `SimGlass` handling in local external tooling
- this makes it safer to keep `SimSkinMask` attached to the `SimSkin` packet for now, instead of inventing a standalone authority node before live-asset proof exists

Safe current reading:

- `SimSkin` is the current baseline family seam for core skin-compatible mesh handling
- `SimSkinMask` is strong enough to keep in the same `P1` packet, but currently looks more like auxiliary or adjacent skin-family semantics than like its own independent geometry root
- this narrows the next question from "what is `SimSkinMask` at all?" to "does live-asset evidence ever justify promoting it from adjacent-family semantics to standalone authority branch?"

Source pointers for this packet:

- local corpus snapshots: [precomp_shader_profiles.json](../tmp/precomp_shader_profiles.json), [precomp_sblk_inventory.json](../tmp/precomp_sblk_inventory.json)
- local repo code: [AssetServices.cs](../src/Sims4ResourceExplorer.Assets/AssetServices.cs), [SimSceneComposer.cs](../src/Sims4ResourceExplorer.Core/SimSceneComposer.cs), [MainWindow.xaml.cs](../src/Sims4ResourceExplorer.App/MainWindow.xaml.cs), [ExplorerTests.cs](../tests/Sims4ResourceExplorer.Tests/ExplorerTests.cs)
- local external code: [Enums.cs](references/external/TS4SimRipper/src/Enums.cs), [PreviewControl.cs](references/external/TS4SimRipper/src/PreviewControl.cs), [ColladaDAE.cs](references/external/TS4SimRipper/src/ColladaDAE.cs)
- local sample assets and reader: [GEOM.cs](references/external/TS4SimRipper/src/GEOM.cs), [cuBodyComplete_lod0.simgeom](references/external/TS4SimRipper/src/Resources/cuBodyComplete_lod0.simgeom), [cuHead_lod0.simgeom](references/external/TS4SimRipper/src/Resources/cuHead_lod0.simgeom), [WaistFiller.simgeom](references/external/TS4SimRipper/src/Resources/WaistFiller.simgeom)
- additional local external anchors: [ShaderData.cs](../.external/s4pe/s4pi%20Wrappers/s4piRCOLChunks/ShaderData.cs), [Enums.cs](../tmp/research/TS4SimRipper/src/Enums.cs), [PreviewControl.cs](../tmp/research/TS4SimRipper/src/PreviewControl.cs), [ColladaDAE.cs](../tmp/research/TS4SimRipper/src/ColladaDAE.cs)
- format archaeology: [Sims_4:0x015A1849](https://modthesims.info/wiki.php?title=Sims_4%3A0x015A1849)
- creator evidence: [Clothes with alpha channel not showing correctly in game](https://modthesims.info/t/596815), [Plastic gloss on Simglass shader how to remove it](https://modthesims.info/t/535913), [Hair conversion gone wrong - Update - still need help](https://modthesims.info/t/541802)
- skintone/tooling corroboration: [The Sims 4 Modders Reference: Resource Type Index](https://thesims4moddersreference.org/reference/resource-types/), [Modifying Sim Appearances](https://thesims4moddersreference.org/tutorials/modifying-sim-appearances/), [TS4 Skininator](https://modthesims.info/d/568474/ts4-skininator-updated-8-6-2018-version-1-12.html), [TS4 Skin Converter V2.3](https://modthesims.info/d/650407/ts4-skin-converter-v2-3-enable-cc-skintones-in-cas.html), [Sims 4 Studio 3.2.4.7 release notes](https://sims4studio.com/thread/29786/sims-studio-windows-star-open), [TS4CASTools ClonePackMeshes.cs](https://github.com/CmarNYC-Tools/TS4CASTools/blob/67d00ebb9016092b8f64ef94ec7ffb5329cf3342/src/CASTools/ClonePackMeshes.cs#L2313-L2315), [TS4SimRipper Enums.cs](https://github.com/CmarNYC-Tools/TS4SimRipper/blob/862a32949e5156b371e2d2f83de4e37e0bb1afcc/src/Enums.cs#L3065-L3079), [TS4SimRipper ColladaDAE.cs](https://github.com/CmarNYC-Tools/TS4SimRipper/blob/862a32949e5156b371e2d2f83de4e37e0bb1afcc/src/ColladaDAE.cs#L3636-L3662), [S4S skin detail tutorial](https://mmoutfitters.notion.site/Sims-4-Studio-How-to-create-a-skin-detail-610cb46220c347f1ab606cd01e1fc9df), [Bakie face mask tutorial](https://www.tumblr.com/bakiegaming/162546871143/the-sims-4-tutorial-how-to-make-a-face-mask-in)

Confidence:

- `spec-backed`

### Skintone resource behavior

The local skintone template and community tooling confirm that `Skintone` is not just a color picker label. It carries:

- one or more skin-set texture instances
- per-skin-set overlay instances
- per-skin-set overlay multipliers
- overlay texture entries keyed by age/gender
- colorize values
- opacity values used during compositing
- display-order data for the UI

Confidence:

- `reference-code-backed` via [Skintone_0x0354796a.bt](references/external/Binary-Templates/Skintone_0x0354796a.bt)
- `community-backed` via Sims 4 Studio and broader modding guidance

Practical meaning:

- `Sim` rendering must treat skintone as routing/compositing input
- it must not be reduced to a late viewport tint hack

### The skintone compositing model is now partially constrained

Reference code does not give us an official engine formula, but it does narrow the working model considerably.

The strongest current reconstruction is:

1. choose the active skin-set base texture
2. optionally mix in the selected alternate skin-set texture and its overlay mask
3. blend skin details over the skin texture using hue, saturation, and opacity-controlled passes
4. apply age/gender-specific overlay textures
5. composite CAS layers such as apparel, makeup, and other overlays on top according to `CompositionMethod` and `SortLayer`

What is proven:

- skintone resources carry the inputs needed for this layered model
- community reference code actually uses skin-set base textures, overlay instances, overlay multipliers, hue, saturation, and opacity values during synthesis

What is still not proven:

- that the exact blend math in community tools matches the game 1:1

Confidence:

- `reference-code-backed` for structure and approximate pass order
- `open gap` for exact in-game blend math

### `CAS` and `Sim` now have a bounded material-input graph (`v0.2`)

The detailed `CAS/Sim` authority packet now lives in a separate companion doc:

- [CAS/Sim material authority matrix](workflows/material-pipeline/cas-sim-material-authority-matrix.md)

Current strongest shared reading still fits this graph:

```text
selected CASP
        ->
linked GEOM
        ->
embedded MTNF / explicit MATD-MTST / CASP field-routing
        ->
canonical material candidates
        ->
RegionMap / SharedUVMapSpace / CompositionMethod / SortLayer
        ->
Sim-only skintone routing and compositing inputs
        ->
shared canonical material output
```

The detailed matrix now carries:

- shell versus worn-slot family split
- body/head shell authority notes
- shell material truth-source boundaries
- hair/accessory/shoes authority
- `SimSkin` versus `SimSkinMask` authority seam

Safe current rule:

- `CASP`, `GEOM`, embedded `MTNF`, explicit material definitions, `RegionMap`, and `Skintone` all belong to one shared authority model
- manifest approximation and package-local texture bags remain reserve-only

### `CAS` and `Sim` family groups now have a bounded slot split (`v0.2`)

The detailed family split is now maintained in:

- [CAS/Sim material authority matrix](workflows/material-pipeline/cas-sim-material-authority-matrix.md#family-split)

Short current reading:

- body foundation shell: `Full Body`, repo-visible `Body`, `Top`, `Bottom`
- head shell: `Head`
- footwear overlay: `Shoes`
- head-related worn slots: `Hair`, `Accessory`
- compositor-driven overlays: skin details, makeup, tattoos, face paint, similar non-shell layers

That split is now strong enough to keep shell debugging, worn-slot debugging, and overlay/compositor debugging separate.

### Body and head shell authority is now tighter (`v0.2`)

Detailed shell notes now live in:

- [CAS/Sim material authority matrix](workflows/material-pipeline/cas-sim-material-authority-matrix.md#body-and-head-shell-authority)

Short current reading:

- body shell is the current assembly anchor
- head shell is a mergeable sibling branch, not a body replacement
- default/nude shell gating is real in current repo logic
- skintone routing is currently shell-scoped in practice

### Shell material truth source is now narrower (`v0.2`)

Detailed shell truth-source notes now live in:

- [CAS/Sim material authority matrix](workflows/material-pipeline/cas-sim-material-authority-matrix.md#shell-material-truth-source)

Short current reading:

- parsed `CASP` field-routing is the current shell-material floor
- explicit `MaterialDefinition` is a stronger upgrade path when present
- embedded `MTNF` stays preserved as a geometry-side candidate
- current repo code still should not be described as already decoding shell `MTNF`

### `Hair`, `Accessory`, and `Shoes` now have a tighter authority boundary (`v0.2`)

Detailed worn-slot authority notes now live in:

- [CAS/Sim material authority matrix](workflows/material-pipeline/cas-sim-material-authority-matrix.md#hair-accessory-and-shoes-authority)

Short current reading:

- `Hair` / `Accessory`: exact part-link first, compatibility fallback second
- `Shoes`: overlay/body-assembly family, not shell identity
- cross-package geometry companions remain valid for these families
- skintone remains bounded away from hair/accessory/footwear targets

### `CompositionMethod` and `SortLayer` are part of the real CAS compositor

We do not have a formal EA spec for every composition mode integer, but the available reference code is strong enough to narrow the rule set.

What is confirmed:

- CAS layer ordering is not texture-only; it depends on `SortLayer`
- composition modes affect opacity treatment and whether a layer behaves like regular overlay, makeup-oriented blend, or grayscale-shading-oriented overlay
- reference implementations sort by composition mode first and then by sort layer when compositing the 2D CAS texture stack

What can be stated safely from the current evidence:

- `CompositionMethod = 0` behaves like a direct overlay path in community tooling
- `CompositionMethod = 1` appears commonly as a tattoo-oriented default in creator tooling, but still lacks a strong authoritative binary description
- `CompositionMethod = 2` is treated as makeup-like and uses skintone makeup opacity
- `CompositionMethod = 3` is treated as grayscale-shading-oriented overlay logic in community tooling
- `CompositionMethod = 4` is treated as a second makeup-like path using a second makeup opacity input
- higher `SortLayer` values draw above lower values, and equal values are a practical instability risk in creator workflows

What is still open:

- the exact meaning of every composition mode value across all game versions and categories
- whether community labels like "hard light?" are exact or only close approximations

Confidence:

- `reference-code-backed` for compositor relevance and current working mode interpretations
- `open gap` for a complete authoritative integer-to-mode table

## Texture-role registry

These roles are currently grounded enough to preserve in canonical materials.

| Canonical role | Typical raw names or fields | Typical sources | Confidence | Notes |
| --- | --- | --- | --- | --- |
| `diffuse` | diffuse, albedo, basecolor, source texture | `MATD`, `CASP`, image payloads | `spec-backed` and `reference-code-backed` | Primary color map. |
| `shadow` | shadow map, diffuse shadow | `CASP`, image payloads | `spec-backed` | Keep as explicit role; final shading semantics can differ by family. |
| `normal` | normal, detail normal | `MATD`, `CASP` | `spec-backed` and `reference-code-backed` | May use different UV routing than diffuse. |
| `specular` | specular, roughness/gloss-ish buckets, env map-ish slots | `MATD`, `CASP`, `RLES` | `reference-code-backed` | Do not over-normalize away source slot identity when uncertain. |
| `emissive` | emission, emissive, glow | `MATD`, `CASP` | `reference-code-backed` | Proven in both object shader families and CAS alien-glow style fields. |
| `region_map` | region map | `CASP`, `RegionMap` | `spec-backed` and `reference-code-backed` | Routing input for region/layer/replacement logic, not just a cosmetic overlay. |
| `color_shift_mask` | `ColorShiftMask` | `CASP`, Sims 4 Studio | `community-backed` and `reference-code-backed` | Real slot vocabulary used by current tools and content. |
| `alpha` | alpha, opacity, cutout, mask | `MATD`, shader params | `reference-code-backed` | Often routing rather than standalone visual output. |
| `overlay` | overlay, ramp, variation, decal-like slots | shader-family dependent | `reference-code-backed` | Preserve until family-specific semantics are proven. |
| `detail` | detail, detail normal, dirt/grime variants | shader-family dependent | `reference-code-backed` | Utility class; exact role depends on family. |

Rule: when the exact end-user meaning is still uncertain, preserve both the canonical semantic and the original slot/source metadata.

## UV and transform rules

### 1. UV decoding must follow the declared vertex layout

Do not decode UVs from a hardcoded struct.

Shared handling must:

- parse declared vertex layout first
- collect UV sets by semantic plus usage index
- allow packed and short-based UV formats
- keep UV selection per sampled map

Confidence:

- `spec-backed` for dynamic `VRTF` and `GEOM` layouts

### 2. Different maps may use different UV channels

The material contract can carry map-specific selectors such as:

- `DiffuseMapUVChannel`
- `NormalMapUVChannel`
- `AmbientMapUVChannel`
- `EmissionMapUVChannel`

Practical consequence:

- UV channel belongs on each `CanonicalTexture`
- not on the mesh alone

Confidence:

- `reference-code-backed` in the current shared decoder
- `open gap` for a complete asset-family proof table across all live content

### 3. CAS shared atlas rules are real

Community creator guidance consistently confirms:

- CAS items share one common UV atlas space
- UV overlap causes cross-item texture bleed
- correct `uv_1` transfer and handling matters
- there are designated regions and practical creator conventions inside that shared space

Confidence:

- `community-backed`
- `reference-code-backed` because `CASP` also contains `NormalUVBodyType`

Architectural meaning:

- `CAS` does not get a special renderer
- it gets a shared renderer with stronger atlas-aware and per-map UV routing rules

### 4. `SharedUVMapSpace` is part of real routing logic

The `CASP` field exposed in code-backed references as `SharedUVMapSpace` means a part can reuse the normal-map UV space of another body type.

Some older notes call this `NormalUVBodyType`, but that label is currently weaker than the upstream code-backed name.

That has two direct consequences:

- normal-map UV routing cannot be assumed to match diffuse routing
- material routing must retain body-type-aware UV semantics where present

Confidence:

- `reference-code-backed`

### 5. `DeformerMap` UV1 use is a separate but related signal

The current external references also describe `Deformer Map` as using `UV1`.

This is not the same thing as regular texture sampling, but it is strong evidence that multi-UV handling is a real part of the character pipeline and cannot be dismissed as rare edge-case metadata.

Confidence:

- `spec-backed`

### 6. `CASHotSpotAtlas` belongs to the CAS editing and morph branch

The current evidence is now strong enough to separate `CASHotSpotAtlas` from the ordinary surface-material chain.

What is now strongly supported:

- `CASHotSpotAtlas` is an EA atlas resource mapped to `UV1` of Sim meshes and used for CAS editing hotspots, not a normal base surface texture ([Making a CAS slider with TS4MorphMaker using a Deformer Map](https://modthesims.info/t/613057))
- the atlas colors map into `HotSpotControl` resources, which in turn select `SimModifier` resources for different directions and views in CAS editing ([Making a CAS slider with TS4MorphMaker using a Deformer Map](https://modthesims.info/t/613057))
- `TS4SimRipper` resource enums also expose `HotSpotControl`, `SimModifier`, and `DeformerMap` as distinct resource families near the core CAS resource set ([Enums.cs](references/external/TS4SimRipper/src/Enums.cs))
- local `TS4SimRipper` code shows that `SimModifier` resources carry direct `DeformerMap` shape/normal references ([SMOD.cs](references/external/TS4SimRipper/src/SMOD.cs))
- local `TS4SimRipper` preview code shows those DMaps being applied to `GEOM`, with `UV1` required on the mesh ([PreviewControl.cs](references/external/TS4SimRipper/src/PreviewControl.cs))
- the local binary template for `DeformerMap` explicitly states that the second UV set in CAS geometry maps into the displacement data used when the Sim is built ([DeformerMap_0xdb43e069.bt](references/external/Binary-Templates/DeformerMap_0xdb43e069.bt))

The resulting branch looks like:

```text
CASHotSpotAtlas
        ->
 HotSpotControl
        ->
  SimModifier
        ->
 DMap / BGEO / BOND
        ->
 GEOM deformation during Sim build or CAS editing
```

Architectural meaning:

- this is a real part of the full character pipeline
- but it is not the same thing as ordinary surface-material texture sampling
- so `CASHotSpotAtlas` should stay out of the canonical `diffuse/specular/overlay` slot vocabulary unless a separate runtime render path proves otherwise
- when it appears in shader/profile corpora, that provenance should be preserved without pretending it is a normal surface map

What still remains open:

- why hotspot-atlas provenance survives in some render/profile families such as `diffuse`, `VertexLightColors`, and `staticTerrainCompositor`
- whether those cases are pure carry-through metadata, editor-linked overlays, or a real runtime render path

Confidence:

- `community-backed` for the hotspot/editing atlas role
- `reference-code-backed` for `HotSpotControl` / `SimModifier` / `DeformerMap` resource existence and DMap application
- `spec-backed` for the `DeformerMap` / `UV1` geometry-deformation relation

### 7. The remaining narrow gaps now belong to different pipeline branches

The current open names should no longer be treated as one undifferentiated "shader mystery" bucket.

The evidence is now strong enough to split them into separate architectural branches:

| Branch | What belongs there now | Why this matters for the shared registry |
| --- | --- | --- |
| `Surface-material branch` | canonical `diffuse` / `normal` / `specular` / `overlay` slots, plus layered visible-pass helpers such as `samplerRevealMap` | this branch still affects visible surface compositing, but reveal-style helpers should remain distinct from ordinary base-color slots |
| `Lightmap/projection branch` | `NextFloorLightMapXform`, `PremadeLightmap`, `OverlayLightMap`, `GenerateSpotLightmap`, `gPosToUVDest`, `gPosToUVSrc`, `WorldToDepthMapSpaceMatrix`, `tex1` inside `RefractionMap` | these names imply projected-space, scene-space, or lightmap-space behavior and should not be flattened into ordinary UV0/UV1 texture-slot semantics |
| `CAS editing/morph branch` | `CASHotSpotAtlas`, `HotSpotControl`, `SimModifier`, `DMap`, `BGEO`, `BOND` | this branch is part of the full character pipeline, but it is not the same contract as surface-material sampling |

Safe architectural rule:

- the shared registry must cover all three branches
- but it must not pretend that all three branches are the same kind of texture-slot contract
- implementation should preserve branch identity explicitly instead of forcing every unresolved input into `diffuse/specular/overlay`

## Layering and compositing rules

### Build/Buy

Build/Buy layering most clearly exposes:

- swatch-level authority through `Object Definition`
- base model/LOD authority through `MODL/MLOD`
- stateful material variants via `MTST`
- leaf material payloads via `MATD`
- mesh-group-specific material assignment via `MLOD`
- separate lighting resources via `LITE`
- shader-family-specific alpha and utility slots

### CAS

CAS layering clearly exposes:

- `CompositionMethod`
- `SortLayer`
- region-map routing
- region-map replacement geometry behavior
- shadow, normal, specular, emission, and newer mask fields
- shared atlas and designated texture-space conventions

### Sim

Sim layering adds:

- skintone base and overlay compositing
- selected CAS-part layering
- region-map-aware routing
- body/head/material assembly interactions

Unified rule:

- these are not three separate material systems
- they are three discovery contexts feeding the same layered-material problem

What must be preserved in canonical form even when the preview remains approximate:

- source material family
- source texture role
- sort-layer intent
- composition method
- region-map and skintone routing inputs
- stateful alternates

## Registry requirements for implementation

We still do not have a complete authoritative registry, but the repo now has enough external and creator-backed evidence to keep a first working registry instead of leaving this as a blank future task.

### Current implementation registry boundary (`v0`)

The current local corpus comes from the profile snapshot in `tmp/precomp_shader_profiles.json`, loaded by [ShaderProfileRegistry.cs](../src/Sims4ResourceExplorer.Preview/ShaderProfileRegistry.cs) and classified by [MaterialCoverageMetrics.cs](../src/Sims4ResourceExplorer.Preview/MaterialCoverageMetrics.cs).

For the external-first family packet, use [Shader Family Registry](workflows/material-pipeline/shader-family-registry.md).

Current corpus snapshot:

- `211` named shader profiles
- `558` weighted occurrences in the local precompiled-profile snapshot
- `163` profiles / `359` occurrences currently classify as `StaticReady`
- `21` profiles / `70` occurrences currently classify as `Approximate`
- `27` profiles / `129` occurrences currently classify as `RuntimeDependent`

Important limit:

- these tiers describe current repo preview support, not authoritative in-game equivalence
- family names are currently normalized from the first token of the profile name before `-`, `_`, or whitespace
- this section is implementation archaeology, not a truth source for family semantics

The current practical boundary is:

| Registry bucket | Representative profiles seen locally | Current decoder path | Current support shape | Notes |
| --- | --- | --- | --- | --- |
| `StandardSurface` | `Phong`, `DiffuseMap`, `Interior`, `ObjectWithPerfectLight`, `CASRoom`, `BlockPreview` | `StandardSurfaceMaterialDecodeStrategy` | mostly `StaticReady`, but can degrade per-property | Best current generic surface bucket for ordinary lit materials. |
| `ColorMap` | `colorMap4`, `colorMap7` | `ColorMapMaterialDecodeStrategy`, `ColorMap7MaterialDecodeStrategy` | `StaticReady` where legacy selectors are recognized | Local decoder already applies legacy UV-channel and atlas-crop interpretation. |
| `AlphaCutout` | `PhongAlpha`, `CutoutMap`, explicit alpha/mask/cutout families, mask-driven families | `AlphaCutoutMaterialDecodeStrategy` | mixed `StaticReady` and `Approximate` | Preview forces alpha-test-or-blend handling when family or slots indicate cutout semantics. |
| `SeasonalFoliage` | `SeasonalFoliage`, `SeasonalFoliageKeepsLeaves` | `SeasonalFoliageMaterialDecodeStrategy` | mixed `StaticReady` and `RuntimeDependent` | Treated as alpha-tested silhouette content by default; some foliage profiles still carry runtime-dependent UV semantics. |
| `StairRailings` | `StairRailings` | `StairRailingsMaterialDecodeStrategy` | `StaticReady` in the current corpus | Separate bucket exists even though the decode path is still mostly generic. |
| `SpecularEnvMap` | `SpecularEnvMap` | `SpecularEnvMapMaterialDecodeStrategy` | `Approximate` | Uses sparse UV-mapping approximation today; keep provenance. |
| `Projective` | `WorldToDepthMapSpaceMatrix`, depth-space and projective families | `ProjectiveMaterialDecodeStrategy` | `RuntimeDependent` | Current preview only offers still-frame approximation for projective/world-space UV controls. |
| `Animated/special runtime families` | `DiffuseMapVideo`, `RefractionMap`, `RenderParticleTexture`, `Water*`, `PreTranslucentSim` | generic plus family heuristics | mixed, often `Approximate` or `RuntimeDependent` | These families expose animation, refraction, particle, water, or screen/depth-space assumptions beyond the current static preview contract. |
| `Sim-special surface families` | `SimGlass`, `SimGhost`, `SimGhostGlassCAS`, `SimWingsUV` | generic plus current slot heuristics | mixed, currently not fully specialized | Keep as a separate tracking bucket because these are likely to matter for later `CAS/Sim` parity work. |
| `Unknown/default` | everything not matched by a dedicated strategy | `DefaultMaterialDecodeStrategy` | mixed | This is the remaining research bucket; do not treat it as one real shader family. |

### Representative implementation detail samples

These are implementation-oriented notes, not final authoritative contracts. The local profile corpus contains a lot of shared global parameters, so the useful signal here is the combination of texture-like slots, UV-like controls, and the strategy bucket that currently catches the profile.

| Representative profile | Current bucket | Useful local slot/UV hints | Safe current reading |
| --- | --- | --- | --- |
| `Phong` | `StandardSurface` | `uvMapping`, `samplerSpecMapTileable`, `samplerClothWithAlphaTexture` | generic lit surface with ordinary UV mapping plus optional alpha/spec detail paths |
| `CASRoom` | `StandardSurface` | `g_colorTexture`, `uvMapping`, `ObjOutlineUVScale` | room/display-style surface family with ordinary UV routing and extra environment/display controls |
| `colorMap7` | `ColorMap` | `paint_colorMap`, `DetailMap`, `NormalMapTileable`, `uvMapping` | color-map family with paint/detail/normal layering and legacy UV selector/crop handling |
| `SeasonalFoliage` | `SeasonalFoliage` | `paint_colorMap`, `samplerEmissionMap`, `samplerSpecMap`, `uvMapping`, `gPosToUVDest` | foliage/cutout content with extra runtime-sensitive UV projection hints |
| `ShaderDayNightParameters` | runtime-adjacent generic family | `samplerSourceTexture`, `samplerroutingMap`, `samplerRevealMap`, `SunTexture`, `LightsAnimLookupMap`, `uvMapping` | day/night or reveal-style layered family that should preserve routing/emissive intent even if preview stays approximate |
| `DecalMap` | mixed generic / decal-like | `DetailMap`, `NormalMapTileable`, `paint_colorMap`, `uvMapping` | decal/overlay-oriented family where texture layering matters more than one plain diffuse slot |
| `WorldToDepthMapSpaceMatrix` | `Projective` | `DetailMap`, `GhostNoiseTexture`, `DirtOverlayUsesUV1`, `gPosToUVDest`, `uvMapping` | projective or depth-space family that is not a true static UV material in the current preview |
| `RefractionMap` | runtime-adjacent generic family | `tex1`, `NormalMapTileable`, `gPosToUVDest`, `uvMapping` | refraction/glass-like family that still needs specialized treatment beyond generic slot mapping |
| `SimGhostGlassCAS` | `Sim-special surface families` | `DecalTexture0`, `samplerOverlayControlMap`, `gPosToUVSrc`, `WorldUVScaleAndOffset` | strong evidence that some Sim-special families need their own later registry row rather than being folded into generic glass/decal logic |

Current practical lesson:

- the first registry should be driven by representative live profiles, not by family names alone
- shared/global params are noisy, so later per-family tables should prioritize texture-like slots and UV-routing controls over every scalar in the profile dump

### Current decoder slot and UV heuristics

The repo already has one concrete set of slot and UV heuristics. They are not the TS4 contract; they are documented here only so current approximation behavior stays explicit and auditable.

Current canonical slot-name normalization in [ShaderSemantics.cs](../src/Sims4ResourceExplorer.Preview/ShaderSemantics.cs):

| Profile or property name fragment | Current canonical slot |
| --- | --- |
| `diffuse`, `albedo`, `basecolor`, `SourceTexture` | `diffuse` |
| `normal`, `detailNormal` | `normal` |
| `rough`, `gloss`, `smooth`, `metal`, `envCube`, `cubeMap`, `reflection`, `spec` | `specular` |
| `alpha`, `opacity`, `cutout`, `mask`, `routingMap` | `alpha` |
| `emission`, `emissive`, `SunTexture` | `emissive` |
| `overlay` | `overlay` |
| `detail` | `detail` |
| `decal`, `lotPaint`, `mural` | `decal` |
| `dirt`, `grime` | `dirt` |
| `ramp`, `paint`, `variation`, `grid`, `foliageColor`, `lightMap`, `WallTopBottomShadow`, `GhostNoise` | `overlay` |

Current shared-material UV-transform policy in [MaterialDecoding.cs](../src/Sims4ResourceExplorer.Preview/MaterialDecoding.cs):

- diffuse-like slots share the decoded material UV transform by default
- the current shared set includes `diffuse`, `basecolor`, `albedo`, `texture_*`, `alpha`, `opacity`, `mask`, `overlay`, `cutout`, `normal`, `specular`, `emissive`, `detail`, `decal`, `dirt`, `grime`, `ao`, `occlusion`, `height`, `displacement`, `gloss`, `roughness`, `smoothness`, `metallic`
- slots outside that set fall back to `UV0` with identity scale/offset unless slot-specific rules override them

Current UV-parameter interpretation baseline in [ShaderSemantics.cs](../src/Sims4ResourceExplorer.Preview/ShaderSemantics.cs):

| Parameter pattern | Current interpretation |
| --- | --- |
| `UsesUV1` | switch between `UV0` and `UV1` |
| `UVChannel` | choose UV channel `0..3` |
| `UVScaleU`, `UScale` | update `UvScaleU` |
| `UVScaleV`, `VScale` | update `UvScaleV` |
| `UVScale` | update both `UvScaleU` and `UvScaleV` |
| `UVOffsetU`, `UOffset` | update `UvOffsetU` |
| `UVOffsetV`, `VOffset` | update `UvOffsetV` |
| `ScaleAndOffset`, `ScaleOffset` | interpret `vec4` as `scaleU`, `scaleV`, `offsetU`, `offsetV` |
| `AtlasMin`, `MapMin` | interpret `vec2` as atlas/window origin |
| `AtlasMax`, `MapMax` | interpret `vec2` as atlas/window max and derive scale from `max - min` |
| `uvMapping`, `MapAtlas`, `AtlasRect` | interpret `vec4` as atlas-like scale/offset data |

Implementation meaning:

- later registry tables should describe not only slot names, but also which UV-parameter patterns each family actually uses
- when a family relies on `gPosToUVDest`, `gPosToUVSrc`, `WorldToDepthMapSpace`, depth-space, or projective controls, the current preview should stay explicitly `Approximate` or `RuntimeDependent`

### Representative per-family tables (`v0`)

The tables below are derived from the representative local profiles plus the current decoder heuristics. They are intentionally narrow: they describe what the repo can currently say safely, and they preserve a visible `raw/unmapped` column where the current semantics are still incomplete.

#### 1. Standard surface family examples

| Profile | Current tier | Canonical slots seen | UV controls seen | Raw or still-unmapped params worth tracking |
| --- | --- | --- | --- | --- |
| `Phong` | `StaticReady` | `alpha`, `normal`, `overlay`, `specular` | `uvMapping` | `BasicWingTextures`, `g_hsv_tweaker_offset_texture`, `samplerFloorThicknessTexture`, `VideoVTexture` |
| `CASRoom` | `StaticReady` | `normal`, `overlay`, `specular` | `uvMapping`, `ObjOutlineUVScale` | `g_colorTexture`, `g_hsv_tweaker_offset_texture`, `VideoVTexture` |

Safe current reading:

- these profiles already behave like ordinary lit surface families in the current preview
- the remaining unmapped params look more like environment/display/runtime helpers than proof of a different base material contract

#### 2. `ColorMap` family example

| Profile | Current tier | Canonical slots seen | UV controls seen | Raw or still-unmapped params worth tracking |
| --- | --- | --- | --- | --- |
| `colorMap7` | `StaticReady` | `alpha`, `detail`, `normal`, `overlay`, `specular` | `uvMapping`, `ClipSpaceOffset` | `LightBasisMap0`, `g_hsv_tweaker_offset_texture`, `samplerFloorThicknessTexture`, `VideoVTexture` |

Safe current reading:

- `ColorMap` is already one of the stronger families for implementation work
- the current decoder can already treat it as layered color content with detail/normal/spec support and legacy UV-crop handling

#### 3. Foliage and cutout-adjacent family example

| Profile | Current tier | Canonical slots seen | UV controls seen | Raw or still-unmapped params worth tracking |
| --- | --- | --- | --- | --- |
| `SeasonalFoliage` | `RuntimeDependent` | `alpha`, `emissive`, `normal`, `overlay`, `specular` | `uvMapping`, `ClipSpaceOffset`, `gPosToUVDest` | `BasicWingTextures`, `heightMapScale`, `VideoVTexture` |

Safe current reading:

- foliage is not just “alpha cutout”; it also carries runtime-sensitive UV/projective hints
- the current decoder is right to keep this family out of the fully static-ready bucket unless the runtime-sensitive controls can be reduced safely

#### 4. Day/night and decal-like layered families

| Profile | Current tier | Canonical slots seen | UV controls seen | Raw or still-unmapped params worth tracking |
| --- | --- | --- | --- | --- |
| `ShaderDayNightParameters` | `StaticReady` | `alpha`, `diffuse`, `emissive`, `normal`, `overlay`, `specular` | `uvMapping` | `LightsAnimLookupMap`, `samplerRevealMap`, `VideoVTexture` |
| `DecalMap` | `StaticReady` | `alpha`, `detail`, `normal`, `overlay`, `specular` | `uvMapping`, `ClipSpaceOffset` | `LightBasisMap0`, `g_hsv_tweaker_offset_texture`, `samplerFloorThicknessTexture`, `VideoVTexture` |

Safe current reading:

- these profiles are already strong evidence that some families are inherently layered even before any CAS/Sim compositor is involved
- `samplerRevealMap`, routing-style masks, and decal-oriented overlays should be preserved as first-class semantics, not flattened into plain diffuse

#### 5. Projective and refraction-adjacent families

| Profile | Current tier | Canonical slots seen | UV controls seen | Raw or still-unmapped params worth tracking |
| --- | --- | --- | --- | --- |
| `WorldToDepthMapSpaceMatrix` | `RuntimeDependent` | `alpha`, `detail`, `emissive`, `normal`, `overlay` | `uvMapping`, `DirtOverlayUsesUV1`, `gPosToUVDest` | `BasicWingTextures`, `samplerFloorThicknessTexture`, `VideoVTexture` |
| `RefractionMap` | `RuntimeDependent` | `alpha`, `emissive`, `normal`, `overlay`, `specular` | `uvMapping`, `gPosToUVDest` | `tex1`, `BasicWingTextures`, `samplerFloorThicknessTexture`, `VideoVTexture` |

Safe current reading:

- these are the clearest current examples of families that should remain `RuntimeDependent` in docs and diagnostics
- `gPosToUVDest` is a stronger signal than ordinary atlas or scale/offset controls and should not be silently treated as plain mesh UV routing

#### 6. Sim-special families

| Profile | Current tier | Canonical slots seen | UV controls seen | Raw or still-unmapped params worth tracking |
| --- | --- | --- | --- | --- |
| `SimGhostGlassCAS` | `RuntimeDependent` | `decal`, `overlay` | `gPosToUVSrc`, `WorldUVScaleAndOffset` | `g_hsv_tweaker_input_texture_linear`, `VideoVTexture`, `BasicWingTextures` |
| `PreTranslucentSim` | `StaticReady` in the current corpus, but still Sim-special | `normal`, `overlay` | `uvMapping` | `BasicWingTextures` |

Safe current reading:

- Sim-special families already justify a dedicated tracking bucket
- even when one example classifies as `StaticReady`, that does not mean the whole Sim-special family is solved or interchangeable with ordinary object materials

Current implementation lesson:

- the next useful registry step is not more bucket naming
- it is to convert the `raw/unmapped` params above into either:
  - proven slot semantics,
  - proven UV/compositor semantics,
  - or explicit “leave as preserved unknown” metadata

### Raw or still-unmapped parameter taxonomy (`v0`)

The current local corpus is now strong enough to split the remaining `raw/unmapped` names into smaller buckets. This is still not a final semantic decode, but it is better than treating all unresolved names as one class.

#### A. Pervasive cross-family runtime or helper parameters

These names appear across many unrelated families, which is evidence against treating them as one specific material slot:

| Parameter | Local spread | Safe current reading |
| --- | --- | --- |
| `BasicWingTextures` | `121` profiles / `439` weighted occurrences | broad engine/helper toggle or shared runtime resource family; not a good candidate for one canonical texture slot |
| `VideoVTexture` | `90` profiles / `407` weighted occurrences | broad animated/video/runtime helper, often coexisting with otherwise unrelated families |
| `samplerFloorThicknessTexture` | `62` profiles / `335` weighted occurrences | shared scene or placement/depth helper; too cross-family to map directly into a core material slot |
| `g_hsv_tweaker_offset_texture` | `35` profiles / `158` weighted occurrences | color-correction or HSV-tweaker helper, not evidence of a primary surface map |

Implementation policy:

- preserve these names as raw provenance
- do not promote them to canonical `diffuse`/`overlay`/`detail` slots without stronger evidence

#### B. Color-correction or display-space helper parameters

| Parameter | Local spread | Safe current reading |
| --- | --- | --- |
| `g_hsv_tweaker_offset_texture` | broad cross-family presence | color-adjustment helper |
| `g_hsv_tweaker_input_texture_linear` | only `SimGhostGlassCAS`, `BlockPreview`, `samplercolorMap4` in the current corpus | likely linear-color or preview/display-space input, still too weakly documented for a canonical texture role |

Implementation policy:

- keep these in a `display/color-adjustment helper` bucket until a stronger shader-family contract proves more

#### C. Lighting, reveal, and lightmap-adjacent helper inputs

| Parameter | Local spread | Safe current reading |
| --- | --- | --- |
| `LightBasisMap0` | `14` profiles / `62` occurrences | light-basis or lightmap helper, not a generic surface slot |
| `LightsAnimLookupMap` | `ShaderDayNightParameters`, `TerrainLight_SunShadowOnly` | animated lighting lookup helper |
| `samplerRevealMap` | `7` profiles / `38` occurrences | reveal/mask-style helper that should stay distinct from plain diffuse or overlay |
| `NextFloorLightMapXform` | `GenerateSpotLightmap`, `SimGhostGlassCAS` | transform or routing helper for next-floor/lightmap behavior |
| `WaterReflectionMap` | seen across `CASRoom`, `SimWingsUV`, `particleFogMultiplier`, and other mixed families | reflection/environment helper rather than one family-defining material slot |

Implementation policy:

- treat these as `lighting/reveal/runtime helper` inputs
- preserve them, but do not collapse them into the same semantics as ordinary `specular` or `overlay`

#### D. Projection and coordinate-space controls

| Parameter | Local spread | Safe current reading |
| --- | --- | --- |
| `ObjectToClipSpaceMatrix` | currently only `Phong` in the local representative set | projection-space transform, not an ordinary UV-scale hint |
| `ClipSpaceOffset` | seen in `colorMap7`, `DecalMap`, `SeasonalFoliage`, `SourceVScaleOffset` | screen/projection-space offset helper; supports nontrivial UV routing |
| `CASHotSpotAtlas` | `StairRailings`, `diffuse`, `VertexLightColors`, `staticTerrainCompositor` | atlas-space or hotspot helper, currently too weakly explained for direct slot semantics |

Implementation policy:

- keep these in the `UV/projection control` bucket
- if these parameters materially affect sampling, the family should stay at least `Approximate` until the transform model is proven

#### E. Family-local unresolved texture-like inputs

| Parameter | Local spread | Safe current reading |
| --- | --- | --- |
| `tex1` | currently only `RefractionMap` | unresolved secondary texture input; likely family-local and should stay raw until `RefractionMap` gets a dedicated semantic pass |

Implementation policy:

- these are the best next candidates for family-specific semantic work because they are narrow and do not look like global helpers

Current implementation lesson:

- the next semantic pass should prioritize narrow family-local unresolved names before broad cross-family helpers
- broad shared helper names are more likely to be engine/runtime controls than missing canonical material slots

### Edge-case family matrix (`v0`)

The current local decoder evidence is now strong enough to distinguish between edge-case families that are merely incomplete and families that are structurally runtime-dependent.

| Family or profile group | Current structural reading | Why it is still not fully closed | Safe current policy |
| --- | --- | --- | --- |
| `ShaderDayNightParameters` | layered static-ready family with preserved reveal/routing inputs | `LightsAnimLookupMap` and `samplerRevealMap` still need better semantics, but they do not currently force projective UV behavior | keep `StaticReady` in current preview, but preserve reveal/lighting helper provenance |
| `DecalMap` | layered static-ready family with decal/detail/normal/spec structure | still carries helper params like `LightBasisMap0`, `g_hsv_tweaker_offset_texture`, `samplerFloorThicknessTexture` | keep static-ready decode path, do not flatten decal/reveal-style layering |
| `SeasonalFoliage` | alpha-cutout plus runtime-sensitive UV family | `gPosToUVDest` and projection-like controls make it more than a plain foliage cutout family | keep `RuntimeDependent` until projective/static reduction is proven |
| `SelfIllumination` | emissive/detail family with runtime-sensitive UV controls | local profile still exposes `gPosToUVDest`/`DirtOverlayUsesUV1` style controls | treat as runtime-adjacent emissive family, not as a solved plain glow map |
| `WorldToDepthMapSpaceMatrix` | explicitly projective/depth-space family | the family name and parameters already say it is not ordinary mesh UV sampling | keep `RuntimeDependent` |
| `RefractionMap` | refraction/glass family with family-local unresolved texture input | `gPosToUVDest` plus unresolved `tex1` mean the family still needs dedicated semantic work | keep `RuntimeDependent`; prioritize `tex1` semantic pass |
| `SimGhostGlassCAS` | Sim-specific runtime family with overlay/decal and projection controls | `gPosToUVSrc`, `WorldUVScaleAndOffset`, and display-space helpers are too specialized for generic handling | keep dedicated Sim-special tracking and `RuntimeDependent` status |
| `PreTranslucentSim` | narrow Sim-special family that is locally static-ready but still weakly generalized | only one small corpus example; not enough to treat the whole class as solved | keep current support, but do not generalize beyond the observed profile set |
| `StairRailings` | narrow static-ready family with atlas/helper inputs | `CASHotSpotAtlas` still lacks strong semantics | keep static-ready decode, but preserve atlas/helper metadata |

Current structural lesson:

- some families are blocked by one or two narrow unresolved names
- other families are blocked because their entire transform model is runtime- or projection-dependent
- the docs and diagnostics should distinguish those two cases explicitly

### Priority queue for the next semantic pass

The next useful pass should attack unresolved names in this order:

| Priority | Target | Why first |
| --- | --- | --- |
| `P1` | `tex1` in `RefractionMap` | narrow, family-local, and likely to unlock a whole family without touching broad helper semantics |
| `P1` | `samplerRevealMap` and `LightsAnimLookupMap` in `ShaderDayNightParameters` | narrow and repeated enough to justify dedicated semantics, while the family is otherwise already stable |
| `P1` | `NextFloorLightMapXform` in `SimGhostGlassCAS` and lightmap-adjacent families | narrow routing helper with a small profile set; good candidate for a targeted semantic note |
| `P2` | carry-through of `CASHotSpotAtlas` into non-obvious rendering profiles | base identity is now stronger; the remaining question is why hotspot-atlas provenance survives in profiles like `diffuse`, `VertexLightColors`, and `staticTerrainCompositor` |
| `P2` | `WaterReflectionMap` in mixed families | likely environment/reflection helper, but spread across unrelated families means it needs more caution |
| `P3` | `BasicWingTextures`, `VideoVTexture`, `samplerFloorThicknessTexture`, `g_hsv_tweaker_offset_texture` | broad shared helpers; high blast radius and poor return if tackled before the narrow family-local names |

### `P1` target sheets (`v0.1`)

The first `P1` pass is now narrow enough to keep per-target notes instead of one generic "unknown shader params" bucket.

#### 1. `RefractionMap` + `tex1`

Local decoder/corpus evidence:

- `tex1` currently appears only in the representative `RefractionMap` profile in the local precompiled-profile corpus
- the same profile also carries `gPosToUVDest`, `uvMapping`, `samplerEmissionMap`, and multiple glass/refraction-adjacent helper names
- local `precomp_sblk_inventory` counters keep `tex1` fully isolated to `RefractionMap` (`87`) while `gPosToUVDest` spreads across multiple projected/runtime families such as `particleFogMultiplier` (`42`), `RefractionMap` (`36`), `PickInstancedVertexAnim` (`36`), `Bloom_Blur` (`35`), and `WorldToDepthMapSpaceMatrix` (`24`)
- current decoder code already treats `gPosToUVDest` as a strong runtime/projective signal rather than ordinary UV-scale metadata
- the current narrower `EP10` Build/Buy identity pass now links the cleaner projective bridge root `01661233:00000000:00F643B0FDD2F1F7` back to `ClientFullBuild0.package | sculptFountainSurface3x3_EP10GENlilyPad` through an `instance-swap32` resolution from `OBJD` candidate `01661233:00000000:FDD2F1F700F643B0`

Engine-lineage corroboration:

- Sims 3 shader docs already separate refraction-oriented semantics from normal surface slots: `Shaders\\Params` lists `RefractionDistortionScale`, while `simglass` and water/glass-oriented families expose dedicated refraction/index-of-refraction behavior rather than ordinary base-material routing ([Sims_3:Shaders\\Params](https://modthesims.info/wiki.php?title=Sims_3%3AShaders%5CParams), [Sims_3:Shaders](https://modthesims.info/wiki.php?title=Sims_3%3AShaders))
- this is not authoritative TS4 proof for `tex1`, but it strengthens the safer inference that `RefractionMap` belongs to the projection/refraction branch rather than to generic surface-slot decoding

Safe current reading:

- `tex1` is best treated as a family-local secondary texture-like input inside a refraction/projective family
- it is not yet safe to alias `tex1` to `diffuse`, `specular`, `emissive`, or `overlay`
- `RefractionMap` should remain `RuntimeDependent` even if `tex1` later gets a stronger slot description
- `RefractionMap/tex1` should now be treated as part of the `lightmap/projection` branch, not the ordinary surface-material branch

What would close it:

- live-asset extraction that shows how `RefractionMap` binds `tex1` in real materials
- stronger code/spec evidence for whether `tex1` is a scene/refraction buffer, secondary lookup, or ordinary sampled texture
- a stronger end-to-end confirmation that the named lily-pad bridge root carries direct `RefractionMap`-family material identity rather than only the adjacent `WorldToDepthMapSpaceMatrix` bridge packet

#### 2. `ShaderDayNightParameters` + `samplerRevealMap` / `LightsAnimLookupMap`

Local decoder/corpus evidence:

- current decoder fallback rules in [ShaderSemantics.cs](../src/Sims4ResourceExplorer.Preview/ShaderSemantics.cs) already map `texture_1 -> emissive` and other `texture_* -> overlay` for `ShaderDayNightParameters`
- current slot normalization also maps `SourceTexture -> diffuse` and `routingMap -> alpha`
- the representative local profile groups `samplerSourceTexture`, `samplerroutingMap`, `samplerRevealMap`, `LightsAnimLookupMap`, `PremadeLightmap`, and `SunTexture` in one layered family
- `LightsAnimLookupMap` appears in a very small profile set and stays concentrated in day/night or terrain-light-adjacent material families
- local `precomp_sblk_inventory` counters strengthen that concentration:
  - `LightsAnimLookupMap` is strongly concentrated in `ShaderDayNightParameters` (`94`) and `TerrainLight_SunShadowOnly` (`87`)
  - `samplerRevealMap` is broader and appears most strongly in `colorMap4` (`41`), `ObjOutlineColorStateTexture` (`39`), `ReflectionStrength` (`39`), and `ShaderDayNightParameters` (`32`)
- the same local co-presence pass makes the split sharper:
  - `LightsAnimLookupMap` repeatedly sits next to `samplerroutingMap`, `SunTexture`, and `PremadeLightmap`, which keeps it close to day/night and terrain-light helper behavior
  - `samplerRevealMap` instead shows up in profiles with outline/highlight/visibility-looking baggage such as `ObjOutlineColorStateTexture`, `ReflectionStrength`, `colorMap4`, and `g_ssao_ps_apply_params`

Engine-lineage corroboration:

- Sims 3 shader docs list `RevealMap` as a distinct texture parameter rather than a synonym for `DiffuseMap` or `OverlayTexture` ([Sims_3:Shaders\\Params](https://modthesims.info/wiki.php?title=Sims_3%3AShaders%5CParams))
- the same Sims 3 shader list shows `RevealMap` used in a concrete shader family (`Painting`) as a dedicated reveal-style input ([Sims_3:Shaders](https://modthesims.info/wiki.php?title=Sims_3%3AShaders))
- this does not prove the exact TS4 visible-pass math, but it strengthens the engine-lineage inference that reveal-named params should stay distinct from canonical base-color slots

Safe current reading:

- this family is no longer an amorphous "special shader" bucket; it is a layered diffuse/emissive/alpha family with separate reveal and lighting-helper provenance
- `samplerRevealMap` is safer as a reveal/mask helper than as a normal surface slot
- `LightsAnimLookupMap` is safer as a lighting-animation lookup helper than as a plain overlay or emissive slot
- `samplerRevealMap` no longer looks family-local to `ShaderDayNightParameters`; it looks like a cross-family reveal/helper input that still needs explicit preservation
- `samplerRevealMap` now has enough support to stay in the `surface-material` branch as a visible-pass helper, but not enough to collapse into ordinary canonical slot semantics
- `LightsAnimLookupMap` currently looks narrower than `samplerRevealMap` and can stay documented as a `ShaderDayNightParameters` / terrain-light lookup helper until stronger cross-family evidence appears

What would close it:

- live materials showing how these helper maps affect visible passes in actual day/night content
- a stronger TS4-specific shader writeup or bytecode-backed parameter contract

#### 3. `NextFloorLightMapXform`

Local decoder/corpus evidence:

- in the current local corpus `NextFloorLightMapXform` is concentrated in `GenerateSpotLightmap` and `SimGhostGlassCAS`
- it co-occurs with clearly lightmap-adjacent names such as `OverlayLightMap`, `lightmapgeneration`, and `GenerateSpotLightmap`
- local `precomp_sblk_inventory` counters make the asymmetry clearer:
  - `GenerateSpotLightmap` carries `NextFloorLightMapXform` with counter `14`
  - `SimGhostGlassCAS` carries it only weakly with counter `3`
- the same co-presence pass also keeps the stronger branch signal on the lightmap side:
  - `GenerateSpotLightmap` keeps `NextFloorLightMapXform` next to `OverlayLightMap`, `lightmapgeneration`, and `DayOpacity`
  - `SimGhostGlassCAS` carries it only as weak provenance next to projective/runtime names such as `gPosToUVSrc` and `AddZFailRenderPass`

External corroboration:

- a Mod The Sims thread on TS4 lighting/lightmaps lists `NextFloorLightMapXform` together with `GenerateSpotLightmap`, `GenerateWindowLightmap`, `GenerateRectAreaLightmap`, `GenerateTubeLightmap`, and other TS4-specific lightmap names ([Mod The Sims](https://modthesims.info/showthread.php?t=646135))

Safe current reading:

- `NextFloorLightMapXform` is much more likely to be a lightmap transform/helper than a surface texture slot
- current docs can safely keep it in the `lighting/reveal/lightmap helper` bucket rather than under ordinary `diffuse/specular/overlay` semantics
- `SimGhostGlassCAS` now looks more like a weak carry-through case than the defining semantic home of this parameter

What would close it:

- a stronger format/tool/code reference that explains the exact matrix or transform role
- live fixtures showing how the parameter changes multi-floor or projected lightmap behavior

#### 4. `CASHotSpotAtlas`

Local decoder/corpus evidence:

- `CASHotSpotAtlas` remains narrowly concentrated in `StairRailings`, `diffuse`, `VertexLightColors`, and `staticTerrainCompositor`
- these profiles do not currently force a projective/runtime classification by themselves
- the parameter still looks more like atlas/helper metadata than a base surface slot
- local `precomp_sblk_inventory` counters show that it is strongest in `diffuse` (`121`) and `VertexLightColors` (`47`), stronger there than in `StairRailings` (`16`)

External corroboration:

- Mod The Sims TS4 MorphMaker documentation identifies `CASHotSpotAtlas` as an EA image resource mapped to `UV1` of sim meshes and used to define face/body editing hotspots and their linked controls/modifiers ([Making a CAS slider with TS4MorphMaker using a Deformer Map](https://modthesims.info/t/613057))
- a second Mod The Sims thread on pointed-ear sliders describes the same role in simplified form: `CASHotSpotAtlas` assigns colors to morphable regions and links them to `HotSpotControl` / `SimModifier` behavior ([Pointed Ears as CAS Sliders](https://db.modthesims.info/showthread.php?t=596028))

Safe current reading:

- `CASHotSpotAtlas` is now better described as a CAS edit/hotspot atlas bound through `UV1`, not as an unresolved ordinary material slot
- when it appears in rendering/profile corpora, the safe default is still to preserve it as atlas/helper provenance rather than map it into `diffuse`, `overlay`, or `alpha`
- `StairRailings` can stay `StaticReady` in the current preview while preserving the unresolved atlas/helper field

What would close it:

- stronger evidence for why non-obvious rendering profiles such as `diffuse`, `VertexLightColors`, and `staticTerrainCompositor` still carry `CASHotSpotAtlas` provenance
- live representative assets that expose whether these profiles touch the atlas only for editor/hotspot logic or also for a runtime overlay path

Current narrowing result:

- `P1` is no longer "unknown shader stuff"
- `RefractionMap/tex1` is now a family-local unresolved input inside a projective family
- `ShaderDayNightParameters` is now a layered diffuse/emissive/alpha family with unresolved reveal/light helpers
- `NextFloorLightMapXform` is now a narrow lightmap-helper problem
- `CASHotSpotAtlas` is now better grounded as a CAS hotspot atlas on `UV1`, so the remaining question is its exact carry-through into render/profile metadata rather than its base identity
- the remaining narrow gaps are now explicitly split across `surface-material`, `lightmap/projection`, and `CAS editing/morph` branches instead of one mixed bucket

Every shader/material family entry must capture:

- family name
- representative shader/profile names
- authoritative texture slots
- optional texture slots
- scalar and vector params that change routing or blending
- alpha or cutout policy
- slot-specific UV channel selectors
- slot-specific UV transforms
- layered/compositor notes
- known domain usage (`BuildBuy`, `CAS`, `Sim`)
- evidence status and sources
- current repo support status (`authoritative`, `approximate`, `unsupported`)

This registry is the right place for future consolidation work. It should be driven by:

- live asset samples
- external references and creator tooling
- local snapshots of external tooling
- format references
- explicit fixture validation

It should not be replaced by more domain-specific renderer branches.

### First authority and fallback matrix (`v0`)

The repo also now has enough evidence to keep a first family-specific authority matrix. This is still incomplete, but it is already stronger than one global fallback rule.

For the authority/family expansion, use [Build/Buy Material Authority Matrix](workflows/material-pipeline/buildbuy-material-authority-matrix.md), [CAS/Sim Material Authority Matrix](workflows/material-pipeline/cas-sim-material-authority-matrix.md), and [Shader Family Registry](workflows/material-pipeline/shader-family-registry.md).

| Asset family | Primary authority today | Additional render-relevant inputs | Fallback boundary | Confidence |
| --- | --- | --- | --- | --- |
| Build/Buy static object | `Object Definition -> MODL/MLOD -> MATD/MTST` | `VRTF`, shader profile, texture payloads | package-local or manifest texture fallback only after explicit object linkage fails | `spec-backed` plus `fixture-backed` |
| Build/Buy stateful or lit object | `Object Definition -> MODL/MLOD -> MTST -> MATD` | `LITE` for lighting behavior, object state/material variants | do not collapse `MTST` state into one anonymous texture bag | `spec-backed` plus `community-backed` |
| CAS body foundation shell | exact selected `CASP` plus linked `GEOM`, with default/nude shell gating for body-first foundation work; explicit companion `MaterialDefinition` upgrades the path when present, otherwise parsed `CASP` field-routing is the current material floor | `RegionMap`, `SharedUVMapSpace`, `CompositionMethod`, normal/specular/emission/shadow/color-shift fields, embedded `MTNF` where present | manifest or same-instance texture fallback is reserve-only after parsed `CASP` fields and explicit material-definition paths are exhausted | `reference-code-backed`, `fixture-backed`, `community-backed`, and `inference` |
| CAS head shell | exact selected `CASP` plus linked `GEOM`, as a separate shell contribution rather than a body replacement; explicit companion `MaterialDefinition` upgrades the path when present, otherwise parsed `CASP` field-routing is the current material floor | `RegionMap`, `SharedUVMapSpace`, `CompositionMethod`, normal/specular/emission/shadow/color-shift fields, embedded `MTNF` where present | reserve-only fallback after parsed `CASP` fields and explicit material-definition paths are exhausted | `reference-code-backed`, `fixture-backed`, `community-backed`, and `inference` |
| CAS split apparel layers and footwear | exact selected `CASP` plus linked `GEOM` | `RegionMap` where present, `CompositionMethod`, `SortLayer`, category-specific atlas conventions, overlay semantics for footwear | broad package-local fallback is not safe architecture | `reference-code-backed` and `community-backed` |
| CAS hair and accessory slots | exact selected `CASP` plus linked `GEOM`, sometimes resolved cross-package | `CompositionMethod`, `SortLayer`, atlas/shared-UV conventions, optional explicit companion `MaterialDefinition` resources | broad package-local fallback is not safe architecture | `reference-code-backed`, `fixture-backed`, and `community-backed` |
| CAS skin details, tattoos, makeup, overlays | exact selected `CASP` | `CompositionMethod`, `SortLayer`, shared atlas rules, skintone interaction | do not flatten to plain diffuse-only layering; preserve compositor intent | `reference-code-backed` and `community-backed` |
| Sim body and head shell | selected Sim outfit/body-part graph resolving to exact shell `CASP` families, with body shell as anchor and head shell as mergeable contribution; explicit `MaterialDefinition` upgrades the path when present, otherwise shell-scoped `ApproximateCas` from parsed `CASP` fields is the current material floor before skintone routing | `Skintone`, `RegionMap`, `CASP` routing fields, `GEOM/MTNF`, selected layer order | approximation is acceptable only as labeled fallback after exact shell identity and parsed `CASP` routing have already been preserved | `reference-code-backed`, `fixture-backed`, and `inference` |
| Sim footwear layers | selected Sim outfit/body-part graph resolving to exact worn-slot `CASP` families | overlay layer order, `CompositionMethod`, `SortLayer`, category-specific atlas behavior | do not let skintone fallback rewrite footwear semantics | `reference-code-backed` and `inference` |
| Sim hair and accessory selections | exact part-link CAS slot selection first, compatibility fallback second, then ordinary `CASP -> GEOM -> material candidate` resolution | head-related worn-slot identity, category-specific atlas behavior, optional cross-package companion resources | do not let skintone fallback retarget hair or accessories | `reference-code-backed`, `fixture-backed`, and `inference` |

## Current repo status

### What is already shared

- One canonical material layer exists in [Domain.cs](../src/Sims4ResourceExplorer.Core/Domain.cs).
- One shared shader/material decoder exists in:
  - [ShaderSemantics.cs](../src/Sims4ResourceExplorer.Preview/ShaderSemantics.cs)
  - [MaterialDecoding.cs](../src/Sims4ResourceExplorer.Preview/MaterialDecoding.cs)
- One shared viewport material application path exists in [MainWindow.xaml.cs](../src/Sims4ResourceExplorer.App/MainWindow.xaml.cs).
- `SimSceneComposer` already mutates canonical materials instead of bypassing the shared renderer.

### What is strongest today

`BuildBuy` currently has the most authoritative base path because it most often reaches real material-definition inputs through `Object Definition -> MODL/MLOD -> MTST/MATD`, with `LITE` remaining a separate parallel branch where present.

### What is still approximate

`CAS` and `Sim` still fall back too often to approximations:

- `ApproximateCasMaterial`
- body/head shell materials reconstructed from parsed `CASP` fields because explicit material definitions are still absent in many current shell flows
- narrow slot extraction from `CASP` texture refs
- manifest-first fallback when full material-definition linkage is missing
- mesh-global UV selection when the real rule should stay map-specific
- skintone and region-map behavior that is partly preserved as routing metadata and partly flattened to viewport behavior

This is the main architectural gap behind the current character-texture bug class.

## Practical implementation rules

Until the remaining gaps are closed, follow these rules:

1. Do not add a new domain-specific texture or shader path unless an external rule proves the divergence.
2. Prefer promoting missing data into the shared canonical material model over inventing UI-only or Sim-only behavior.
3. If a material path is still approximate, surface that explicitly in diagnostics and docs.
4. Treat wrong UV panel selection, wrong texture-group selection, and wrong layer order as shared pipeline bugs first, not as Sim-only bugs.
5. If the index lacks stable facts required for this shared pipeline, add them to explicit indexing passes rather than runtime lazy mutation.
6. Preserve raw field identity when semantics are uncertain. Losing provenance is worse than carrying an extra approximate slot.
7. `CASP`, `GEOM/MTNF`, `Skintone`, and `RegionMap` are all render-relevant inputs. Do not collapse them into a single "texture candidate" bucket.

## Validation checklist for future packets

Any future material/texture/UV packet should answer:

1. Which discovery step changed?
2. Which shared post-discovery stage changed?
3. Which evidence class justifies the change?
4. Does the new behavior now match across `BuildBuy`, `CAS`, and `Sim` for the same shader/material family?
5. If not, what exact open gap still prevents convergence?
6. Which fixtures or live assets prove the behavior?
7. Did the packet preserve provenance and diagnostics for approximate paths?

And it should avoid this failure mode:

- using a fixture to justify a new asset-class-specific shader path instead of a better shared-family rule

## Open gaps

These gaps are still real. Do not treat them as solved.

### 1. The shader-family registry is still incomplete

We now have a first working registry plus a linked external-first family deep-dive, but we still do not have one complete cross-domain table of:

- shader family
- authoritative texture roles
- scalar and color params
- alpha and blend rules
- map-specific UV selectors
- UV transforms
- layering and compositor behavior

Status: `open gap`

### 2. CAS and Sim material linkage is still incomplete

We now have a bounded `CASP -> GEOM -> MTNF/material-definition-or-field-routing -> RegionMap/SharedUVMapSpace/CompositionMethod/SortLayer -> Skintone` input graph, a first family-level split between body shell, head shell, footwear, hair/accessory, and overlay behavior, a stronger worn-slot boundary for exact-part-link families, and a narrower shell-material baseline where parsed `CASP` field-routing is the current floor until explicit material definitions appear. But we still do not yet have a fully authoritative rule for all `CAS/Sim` cases that answers:

- where the real material definition comes from inside each family
- when embedded `GEOM` material hints are enough
- when `CASP` field-based routing is authoritative
- when same-instance linkage is valid
- when manifest approximation is only fallback and not the truth source

Status: `open gap`

### 3. Full skintone and region-map compositing is not documented end-to-end

We now have a stronger input model, but we still lack a complete, source-backed rule for:

- exact in-game blend math for skintone base plus overlay passes
- exact interaction between region-driven geometry replacement and final material compositing in every category
- how all of that maps onto final shader slots across all character families

Status: `open gap`

### 4. Complete slot-specific UV transform coverage is missing

Map-specific UV routing is clearly real, but we do not yet have a complete proof and implementation matrix for all live families and all transform encodings.

Status: `open gap`

### 5. `VPXY` still lacks a strong TS4-specific rendering writeup

Its existence and category are documented, and it is now better bounded as an object/scene linkage helper rather than a base material-authority node. But its exact TS4 traversal role still remains less clear than `Object Definition`, `MLOD`, `GEOM`, or `CASP`. Current stronger Build/Buy documentation is sufficient to describe the base authority order without relying on `VPXY`.

Status: `open gap`

## Recommended next work

1. Expand the external-first shader-family packet into per-family slot/param tables derived from live assets, creator tooling, and external snapshots.
2. Extend the current family packet from sampled Build/Buy roots into sampled `CASP/GEOM` families with real material dumps.
3. Add a validation corpus that exercises the same material families across `BuildBuy`, `CAS`, and `Sim`.
4. Promote `CASP` routing fields, `GEOM/MTNF`, `Skintone`, and `RegionMap` into one explicit material-input graph rather than scattered helpers.
5. Keep extending the shared decoder, not the number of special-case renderers.

## Sources used for this guide

Primary reference hubs:

- [The Sims 4 Modders Reference: File Types](https://thesims4moddersreference.org/reference/file-types/)
- [The Sims 4 Modders Reference: Resource Type Index](https://thesims4moddersreference.org/reference/resource-types/)

Format archaeology:

- [Mod The Sims: Sims 4 Packed File Types](https://modthesims.info/wiki.php?title=Sims_4%3APackedFileTypes)
- [Mod The Sims: Sims 4 RCOL](https://modthesims.info/wiki.php?title=Sims_4%3ARCOL)
- [Mod The Sims: GEOM 0x015A1849](https://modthesims.info/wiki.php?title=Sims_4%3A0x015A1849)
- [Mod The Sims: MLOD 0x01D10F34](https://modthesims.info/wiki.php?title=Sims_4%3A0x01D10F34)
- [Mod The Sims: Object Definition 0xC0DB5AE7](https://modthesims.info/wiki.php?title=Sims_4%3A0xC0DB5AE7)
- [Mod The Sims: LITE 0x03B4C61D](https://modthesims.info/wiki.php?title=Sims_4%3A0x03B4C61D)
- [EA Forums: object definition and material variant discussion](https://forums.ea.com/discussions/the-sims-4-mods-and-custom-content-en/simgurumodsquad-looking-for-cross-reference-between-catalogobject-or-object-defi/1213694/replies/1213695)

Community tooling and creator guidance:

- [Sims 4 Studio: Mesh glitches and gets color from other CC](https://sims4studio.com/thread/18038/solved-mesh-glitches-gets-color)
- [Sims 4 Studio: bake texture problem with Blender when creating cc](https://sims4studio.com/thread/26090/bake-texture-problem-blender-creating)
- [Sims 4 Studio: Texture Bake](https://sims4studio.com/thread/27008/texture-bake)
- [Sims 4 Studio: 3.2.4.7 release notes](https://sims4studio.com/thread/29786/sims-studio-windows-star-open)
- [The Sims 4 Modders Reference: Modifying Sim Appearances](https://thesims4moddersreference.org/tutorials/modifying-sim-appearances/)
- [Mod The Sims: Several CASP questions](https://modthesims.info/t/589486)
- [Mod The Sims: Adding new GEOMs to a CAS part with s4pe and S4CASTools](https://modthesims.info/t/536671)
- [Mod The Sims: Manually editing the BodyPart thread](https://modthesims.info/showthread.php?t=542283)
- [pyxiidis: skins and makeup info](https://pyxiidis.tumblr.com/post/123291105281/skins-makeup-info-post)
- [Maxis Match CC World: Composition Method 0](https://maxismatchccworld.tumblr.com/post/622238734033797120/composition-method-0)

Community code and local snapshots:

- [Binary Templates local snapshot](references/external/Binary-Templates/README.md)
- [TS4 SimRipper local snapshot](references/external/TS4SimRipper/README.md)
- [TS4 UV and Material Mapping](references/codex-wiki/03-implementation-guides/05-TS4_UV_AND_MATERIAL_MAPPING.md)

## Honest limit of this guide

This is now the best consolidated guide in the repo for the shared pipeline, but it is still not a claim that the full Sims 4 material contract is completely solved.

What is now strong:

- one shared post-discovery architecture
- a clearer rendering-role registry for major resource types
- a stronger statement that `CASP`, `GEOM/MTNF`, `Skintone`, and `RegionMap` are all part of the material problem
- a stronger separation between authoritative rules, inference, and open gaps

What remains unresolved:

- full shader-family inventory
- full authoritative CAS and Sim material linkage
- full layered and compositor semantics
- full per-slot UV transform coverage
- some TS4-specific linkage details around `VPXY` and related object graph helpers

Those are now explicit research and implementation targets rather than hidden assumptions.
