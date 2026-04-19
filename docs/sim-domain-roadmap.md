# Sim Domain Roadmap

## Why This Domain Exists

The current asset model covers:

- `Build/Buy`
- `CAS`
- `General 3D`

That still leaves a large, distinct slice of 3D game data unstructured: assembled Sims and character-like entities. This includes the resources needed to describe bodies, outfits, skintones, deformations, occult forms, and the higher-level records that bind those pieces into actual character presets or in-game Sims.

This domain matters for two reasons:

- it is a first-class 3D category in the game, not just a side effect of `CAS`
- its rig/body/deformation data is also the missing bridge for fuller `CAS` preview fidelity

## Evidence In The Current Catalog

The current active shard catalog already contains a large amount of character-related data:

| Type | Indexed rows |
|---|---:|
| `CASPart` | 423,290 |
| `SimData` | 269,078 |
| `CASPartThumbnail` | 239,108 |
| `Geometry` | 142,933 |
| `Rig` | 15,794 |
| `SimInfo` | 15,096 |
| `RegionMap` | 12,997 |
| `CASPreset` | 5,930 |
| `SimModifier` | 4,943 |
| `BlendGeometry` | 3,461 |
| `SimPreset` | 2,695 |
| `BonePose` | 1,628 |
| `DeformerMap` | 1,318 |
| `Skintone` | 580 |

These are not niche leftovers. They represent a real parallel content domain that the app should eventually browse as structured logical assets.

## Input Model

The character domain naturally splits into two layers.

### Package-side building blocks

These resources describe reusable parts, body adjustments, rigging, or texture logic:

- `CASPart`
- `CASPreset`
- `Rig`
- `Geometry`
- `RegionMap`
- `Skintone`
- `SimModifier`
- `BlendGeometry`
- `DeformerMap`
- `BoneDelta`
- `BonePose`
- related thumbnails and texture resources

### Assembly / instance-side records

These resources describe how the reusable pieces are actually combined into a Sim or preset:

- `SimData`
- `SimInfo`
- `SimPreset`
- outfit lists
- age/gender/species/frame
- occult state
- body and face modifier weights
- links to the selected `CASPart` set

This suggests a future `SimCharacterGraph` that composes:

- base rig
- base body geometry
- outfit/body-part `CASPart` selections
- deformation inputs such as `BGEO`, `DMAP`, and `BOND`
- skintone and region-map driven material behavior
- occult/species/age/frame specializations

## External References

The following sources are useful starting points for reverse-engineering and coverage planning:

- TS4 Sim Ripper: <https://github.com/CmarNYC-Tools/TS4SimRipper>
- Llama Logic Binary Templates: <https://github.com/Llama-Logic/Binary-Templates>
- Sims 4 Toolkit models / `SimData` schemas: <https://github.com/sims4toolkit/models>

Particularly relevant format references include:

- `CASPart_0x034aeecb.bt`
- `RegionMap_0xac16fbec.bt`
- `DeformerMap_0xdb43e069.bt`
- `BlendGeometry_0x067caa11.bt`
- `Skintone_0x0354796a.bt`
- `CASPreset_0xeaa32add.bt`
- `SimData_0x545ac67a.bt`

## Reality Check Against Known Character Assembly Logic

The current project now has useful character-domain discovery groundwork, but it is important to be precise about what is already real format-driven logic and what is still proxy scaffolding.

### What already follows real TS4 data structures

- `SimInfo` rows are parsed into real factual fields such as species, age, gender, skintone, outfits, and modifier counts.
- `CASPart` parsing now follows a real versioned format model closely enough to recover stable names, body-type/category facts, swatch rows, several indexed links, and compatibility summaries.
- `GEOM` parsing now follows the known TS4 geometry structure closely enough to recover mesh data from resources that previously failed on malformed tail handling.
- body candidate discovery can resolve real `CASPart` roots and real `Geometry` resources instead of browsing only by name.

### What is still proxy / heuristic behavior

- top-level `Sim Archetypes` are grouped metadata rows, not real game character assemblies.
- body shell choice still falls back to a compatibility search over the indexed CAS catalog when explicit authoritative part selections are missing.
- the current body preview is still a proxy composition of chosen `CASPart` assets, not a true assembled Sim graph.
- head selection, rig resolution for the assembled Sim, skintone/region-map synthesis, and modifier/deformer application are still incomplete.
- shoes and apparel are still modeled as preview layers on top of the proxy shell rather than being applied through a complete body/outfit assembly pipeline.

### How this differs from known modding tools

Tools such as TS4 Sim Ripper do not start from a generic "compatible body CASPart" search. They work from the selected Sim/outfit records and follow a more authoritative pipeline:

1. choose the concrete Sim or outfit record
2. resolve the correct rig for species/age/occult/frame
3. resolve actual selected body-part `CASPart` resources by body type
4. use `CASPart` fallback/default/naked/opposite-gender logic where applicable
5. assemble the body/head/outfit parts into one character graph
6. apply skintone, region-map, and material logic
7. apply body/face modifiers and deformer resources (`BGEO`, `DMAP`, `BOND`, etc.)

Our current `Sim` slice only touches parts of steps 1, 3, and some early factual pieces of 6. It does not yet implement the authoritative body/head/modifier assembly path.

### Practical conclusion

The current body preview should be treated as a debugging scaffold, not as trustworthy character assembly. If it renders the wrong species body, a dress instead of a torso shell, or overlay layers without a correct body foundation, that is a real architectural gap rather than a small bug in an otherwise complete pipeline.

The target end state is now explicit:

- indexing should persist the authoritative character-assembly graph inputs and links needed for runtime body/head/outfit/rig/skintone/morph selection
- runtime `Sim` preview should start from the chosen archetype/template root and traverse those indexed links instead of rediscovering candidate resources heuristically during preview
- when the indexed graph does not yet provide enough authoritative data for the next step, the result should stay honestly `Unresolved` and reopen indexing/parser work rather than widening runtime search

## Implementation Phases

### Phase 1: Discovery and indexing groundwork

- make character-related resources easy to spot in the raw browser
- keep indexing them as first-class factual rows
- document the domain and the format map

### Phase 2: Factual parsers for low-risk support types

Start with data that improves both future Sim assembly and current CAS fidelity:

- `Skintone`
- `RegionMap`
- `CASPreset`
- later `DeformerMap`, `BoneDelta`, and `SimModifier`

Current state:

- `CASPreset`, `RegionMap`, and `Skintone` now already parse into cached factual summaries during lazy raw-resource enrichment
- those summaries are intentionally descriptive, not authoritative assembled-character logic yet
- the long-term contract is to move any persisted facts that runtime assembly depends on into explicit indexing passes with version invalidation, not to keep them in lazy browse/open write paths

### Phase 3: Metadata-only logical `Sim` assets

Introduce a new logical asset domain rooted in `SimInfo`, `SimPreset`, and selected `SimData` records. The first slice should surface:

- display identity
- age / gender / species / occult flags
- outfit counts
- linked body-part counts
- available thumbnails / portraits when present

Current state:

- the first metadata-only `Sim` slice is now live for grouped `SimInfo` archetype roots
- indexing seed-enrichment parses `SimInfo` into searchable display names plus factual summary text
- the `Asset Browser` can now surface those grouped `SimInfo` roots as logical `Sim` archetypes with metadata/details, while named characters, preset selection, slot editing, and full scene assembly are still future layers
- `SimInfo` rows that still fail archetype classification are now intentionally hidden from the top-level archetype list instead of surfacing as singleton fallback rows
- selected archetypes now expose a body-first foundation summary plus concrete body-source references and body-assembly candidate families in details so the future editor has an honest structural foothold before real slot switching exists
- indexed `CASPart` rows now also carry first-pass slot/compatibility summaries, so the selected human archetype can surface compatible `Hair` / `Top` / `Bottom` / `Shoes` / `Full Body` / `Accessory` family counts and sample names directly from the indexed CAS catalog
- the tabbed archetype inspector now also exposes the first compatible CAS asset names inside each slot family and keeps one explicit selected candidate per family, so the current `Sim` slice already behaves more like a browseable slot navigator than a pure diagnostics panel
- the `Body` tab now keeps the focus on body-assembly families (`Full Body`, `Body`, `Top`, `Bottom`, `Shoes`) instead of broader apparel categories, prefers exact SimInfo body-part links, adds compatible fallback candidates inferred from body-type tokens when exact links are missing, and if a chosen template still has no body-specific references it promotes archetype-compatible body families so the body-first path stays usable. Those selected body-family assets drive a first proxy body preview, giving the project a real base-body stand-in before full assembled-character synthesis exists
- the `Body` tab now also exposes grouped `SimInfo` template variations under the top-level archetype, and choosing one template rebinds the body-first inspector plus the proxy body preview inside that archetype instead of forcing the user back down to raw `SimInfo` rows
- the `Body` tab now also exposes a `Current Body Assembly Recipe` block that makes the active proxy-body layers explicit: which selected family currently participates in the composed body preview, which source mode supplied it, and which other body families are present but overridden by a stronger body shell
- the same body-first slice now materializes an explicit `SimBodyAssembly` model with a resolved assembly mode and per-layer `Active` / `Available` / `Blocked` state so the current base-body shell policy is visible in details and the inspector instead of living only inside preview heuristics
- the `Body` tab now also shows the exact CAS layers that successfully made it into the current proxy preview, so the runtime composition is visible as concrete rendered layers instead of only as a theoretical assembly recipe
- the `Body` tab now also shows explicit proxy-preview coverage, making it visible when an active body layer from the current recipe is still missing from the rendered proxy preview even though the archetype has a valid fallback body path overall
- the body-assembly policy is now centralized so the runtime preview uses the same shell/layer rules as the stored assembly summary; notably, `Shoes` can remain an active rendered layer on top of a `Full Body` or `Body` shell instead of being suppressed by a local preview-only heuristic
- proxy body preview now treats selected body candidates as ordered fallbacks within each body family: malformed GEOM payloads are skipped with diagnostics and the preview keeps trying the next compatible asset instead of aborting the whole archetype on the first broken candidate
- the `Body` tab now also exposes an explicit `Body Graph` stage list with `Resolved` / `Approximate` / `Pending` / `Unavailable` states, so the project can see which parts of the current base-body path are already grounded in data (`Base frame`, `Skin pipeline`) and which are still approximation or future work (`Geometry shell`, morph application)
- the `Body` tab now also materializes a resolved base-body graph from the currently chosen families, so it is visible which layer is acting as the current shell, which layer is acting as footwear overlay, which layers are suppressed by shell policy, and which active layers are still not making it into the proxy preview
- proxy body preview no longer renders overlay-only fragments such as standalone shoes when no renderable body shell exists, so body-first debugging stays focused on torso/body assembly instead of accidentally presenting clothing fragments as a character preview
- body-first preview now also refuses to treat `Top` / `Bottom` split clothing layers as a fake torso shell when no real `Full Body` or `Body` foundation exists
- clothing-like authoritative `Full Body` outfit selections no longer automatically become the base-body preview shell; if they do not resolve to a real default/nude human foundation, the preview stays without a base shell until a canonical foundation is found
- the current rendered body preview now resolves one explicit base-body shell at a time instead of composing multiple clothing-like CAS layers into a pseudo-body
- dedicated `Head` candidates can now be surfaced directly from authoritative `SimInfo` body-part links, and the current preview can compose `body shell + head shell` without pulling in clothing/accessory layers
- sim-specific preview assembly no longer blindly merges body/head scenes: a dedicated composer now checks canonical bone overlap and withholds the head layer when the resolved scenes do not expose a believable shared skeleton basis
- when body/head CAS graphs do expose rig resources, the same sim-specific preview assembly now prefers shared rig compatibility over loose scene heuristics and only falls back to canonical-bone overlap when one side is still missing rig metadata
- the active `Sim` preview now also materializes an explicit assembly basis summary, so diagnostics and inspector text can say whether the current preview is built from a shared exact rig resource, a shared rig instance id, a canonical-bone fallback, or a body-only stop
- the same preview path now also materializes a first explicit `SimAssemblyGraph` node set (`Body shell scene`, `Head shell scene`, `Assembly basis`, `Sim assembly result`), so future rig/skintone/morph work can attach to named graph stages instead of implicit preview-only flow
- the same `SimAssemblyGraph` now also exposes explicit named inputs (`Body shell input`, `Head shell input`, `Assembly basis input`), so future fidelity stages can consume durable assembly inputs rather than reconstructing state from diagnostics or temporary VM locals
- the same `SimAssemblyGraph` now also executes through explicit assembly stages (`Resolve body shell scene`, `Resolve head shell scene`, `Resolve assembly basis`, `Compose assembled scene`), so the current preview result already comes from a named stage pipeline rather than one flat conditional compose path
- the same `SimAssemblyGraph` now also materializes an explicit assembly output summary, so the current path already has a durable `inputs -> stages -> output/result` shape even before true rig-centered geometry assembly replaces the current scene-level compose implementation
- the current `Compose assembled scene` stage no longer delegates final assembly to the generic `CanonicalSceneComposer`; it now uses a sim-specific skeletal-anchor merge that treats the body shell scene as the primary skeleton basis for accepted inputs
- that skeletal-anchor merge now also remaps accepted head mesh skin weights onto the body scene bone basis directly inside the sim-specific assembler, which is a better structural starting point for later rig-centered assembly than a generic scene composer call
- the same `SimAssemblyGraph` now also surfaces explicit assembly contributions for accepted body/head shells, including mesh/material/bone counts plus rebased-weight and added-bone facts for each contribution
- the same `SimAssemblyGraph` now also surfaces an explicit assembly payload summary, so the project can see anchor-bone counts, accepted contribution counts, mapped bone references, and rebased-weight totals before those data become true rig-native assembly nodes
- the same `SimAssemblyGraph` now also surfaces explicit payload anchor, bone-map, and mesh-batch records, so the current body/head payload already has inspectable rig-native structure beneath the summary level
- the same `SimAssemblyGraph` now also surfaces explicit payload nodes for the anchor skeleton, contribution bone-remap tables, and merged mesh sets, so the current rig-centered payload already has a node-oriented shape before real morph/skintone stages land
- the sim-specific assembler now also materializes an internal payload-data layer for the anchor skeleton, remap tables, and merged mesh sets, and the current public payload summaries/nodes are derived from that data instead of being built directly from local merge temporaries
- the same `SimAssemblyGraph` now also surfaces modifier-aware application passes from authoritative skintone and morph metadata, so the project can see when those inputs are prepared without falsely implying that deformation or material application has already happened
- the same application layer now also surfaces explicit skintone and morph target planning from payload materials and meshes, so prepared passes are tied to real payload targets before any true transform/deformation step is applied
- the same application layer now also materializes explicit skintone material-routing and morph mesh-transform plans from payload-data, so the next modifier pass can build on internal planning data instead of stopping at summaries and target counts
- the same application layer now also materializes explicit skintone routing records and morph transform operation records from those plans, so the next modifier pass can start from internal transform data instead of only plan summaries
- the same application layer now also materializes explicit internal skintone and morph outcomes and threads them into preview diagnostics, so modifier progress is visible in the real preview path instead of staying buried in graph internals
- the same preview path now also applies skintone routing to downstream assembled-scene material state through application-adjusted payload data, so modifier progress no longer stops at graph-only bookkeeping
- the same body-first path now also prefers one authoritative `Nude` outfit record from `SimInfo` when resolving body/head parts, instead of flattening every outfit entry in the template into one mixed candidate pool
- the same primary body-preview path now stays unresolved when a `SimInfo` template has no authoritative body-driving `Nude` outfit record, instead of falling back to flattened outfit unions or archetype-wide shell compatibility search that can produce cross-species junk previews
- exact `Hair` and `Accessory` resolution now also uses the correct human CAS-slot predicate rather than the body-only family filter, so authoritative head-related selections stay exact instead of silently degrading to compatibility fallback
- human body-shell fallback now widens from exact `species + age + gender` search to a broader `species + age` pass for shell families, so generic unisex nude/base body shells can outrank occult or clothing full-body rows when the gender-specific pool is misleading
- shell filtering now also checks real `CASPart` species/default flags plus TS4-style canonical body prefixes (for example `yfBody_`, `ymBody_`, `cuBody_`) so the body-first path is less likely to accept a formally "compatible" but structurally wrong shell candidate

### Phase 4: Partial scene assembly

Build the first honest assembled-character preview path:

- authoritative body-part selection from `SimInfo` / outfit data, not from a generic compatibility search
- rig
- base body shell and head shell
- directly resolvable `CASPart` geometry chosen from the authoritative part list
- diagnostic gaps for unsupported modifiers or missing body synthesis

### Phase 5: Higher-fidelity assembly

Add the data that makes characters look correct instead of merely present:

- skintone compositing
- region maps
- authoritative head/body split and shell composition
- `DMAP`
- `BGEO`
- `BOND`
- body and face modifier application

### Phase 4A: Stop polishing the proxy path

Before adding more body-slot UI or broader apparel coverage, the project should pivot from proxy heuristics to a real authoritative assembly path:

- stop treating broad CAS compatibility search as the main way to choose a torso/body shell
- extract the actual body-part selections and outfit/body-type choices that belong to the selected `SimInfo` template
- resolve head/body/outfit parts through those selections first, using compatibility search only as a debugging fallback
- keep proxy preview only as an explicit scaffold for diagnostics, not as the assumed future architecture

## Current progress checklist

- [x] Top-level `Sim` list is grouped into archetypes instead of exposing every raw `SimInfo`
- [x] Unclassified `SimInfo` rows stay out of the top-level archetype list until parser coverage improves
- [x] The selected archetype exposes grouped template variations
- [x] The selected template rebinds the body-first inspector inside the archetype
- [x] The inspector shows body foundation, body-source references, and morph groups
- [x] The inspector surfaces exact body-part `CAS` candidates when the template exposes them
- [x] The inspector falls back to body-type-token body candidates when exact links are missing
- [x] The inspector falls back to archetype-compatible body candidates when the template has no direct body references
- [x] The archetype can render a first proxy base-body preview from selected body families
- [x] The inspector shows a current body-assembly recipe so the active proxy-body layers are explicit
- [x] The inspector shows an explicit body-graph stage list with resolved/approximate/pending/unavailable states
- [x] The inspector shows a resolved base-body graph for the currently chosen shell/overlay/alternate layers
- [x] Authoritative `Head` shell candidates can be surfaced and composed alongside the current body shell without adding clothing/accessory layers
- [x] The current sim preview withholds resolved head scenes that do not share canonical bones with the chosen body shell instead of blindly composing them
- [x] The current sim preview prefers shared rig compatibility for body/head assembly and only falls back to canonical bones when rig metadata is still incomplete
- [x] The current inspector exposes the active assembly basis for the rendered sim preview instead of leaving that reasoning buried only in diagnostics
- [x] The current preview path materializes a first explicit `SimAssemblyGraph` node set for body scene, head scene, assembly basis, and final assembly result
- [x] The current preview path materializes explicit `SimAssemblyGraph` inputs for body shell, head shell, and assembly basis
- [x] The current preview result is produced through explicit `SimAssemblyGraph` stages instead of a flat inline compose flow
- [x] The current preview path materializes an explicit `SimAssemblyGraph` output summary for the assembled scene result
- [x] The current body-first path prefers one authoritative `Nude` outfit record instead of flattening all template outfits into one body candidate pool
- [x] The current final assembly stage uses a sim-specific skeletal-anchor assembler instead of the generic scene composer
- [x] The current final assembly stage remaps accepted head mesh weights onto the body skeleton basis inside the sim-specific assembler
- [x] The current `SimAssemblyGraph` exposes explicit assembly contributions for accepted body/head shell inputs
- [x] The current `SimAssemblyGraph` exposes an explicit assembly payload summary before final scene output
- [x] The current `SimAssemblyGraph` exposes explicit payload anchor, bone-map, and mesh-batch records
- [x] The current `SimAssemblyGraph` exposes explicit payload nodes for anchor skeleton, bone-remap tables, and mesh sets
- [x] The sim-specific assembler materializes an internal payload-data layer beneath the current public payload summaries/nodes
- [x] The current `SimAssemblyGraph` exposes modifier-aware application passes from authoritative skintone/morph metadata
- [x] The current application layer exposes explicit skintone/morph target planning from payload materials/meshes
- [x] The current application layer exposes explicit skintone routing and morph transform plans from payload-data
- [x] The current application layer exposes explicit skintone routing and morph transform records from those plans
- [x] The current application layer exposes explicit skintone/morph outcomes in both graph state and preview diagnostics
- [x] The current assembled preview scene already uses application-adjusted payload material state for skintone routing outcomes
- [ ] Base-body assembly uses authoritative selected body parts instead of broad compatibility-search fallbacks
- [ ] Base-body assembly uses a real multi-layer character graph instead of an approximate proxy composition
- [x] Head-shell selection is wired into the same authoritative assembly path as the torso/body shell
- [ ] Morph channels affect the assembled body preview
- [ ] Species beyond the current human-first path have comparable body assembly coverage
- [ ] Clothing/accessory slots can be applied on top of the assembled base body
- [ ] Full character export works from the assembled Sim graph

### Phase 6: Broader population coverage

Expand coverage beyond the first adult human slice:

- occults
- child / teen / elder
- special body frames
- pets if the project chooses to include them

## Relationship To CAS

This roadmap is not separate from `CAS` work. It is the next structural layer under it.

Better character-domain coverage should directly unlock:

- more accurate `CAS` body bindings
- better rig resolution for accessories and clothing
- better material/skintone fidelity
- fuller variant/state handling for body-linked parts

In practice, the `Sim` domain and the deeper `CAS` domain should evolve together rather than as isolated tracks.
