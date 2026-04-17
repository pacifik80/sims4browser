# Current Plan

This file tracks the active block of work and its immediate progress.

## Active Block

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
- [x] Canonical human body-foundation search now paginates beyond the first shell-query page, so buried default/nude body shells can still replace withheld clothing-like `Full Body` candidates in large human archetypes
- [x] Canonical human body-foundation search now also accepts generic nude/unisex adult shells such as `acBody_Nude` and `ahBody_nude` in the fallback path, so real human archetypes are not blocked on a strict `yfBody_`-only prefix assumption
- [x] Body-first candidate resolution now prefers authoritative `SimInfo` `Nude` outfit records over a flattened union of all outfit parts, so the shell path starts from one concrete body-driving outfit instead of mixing every archetype outfit into one noisy candidate pool
- [x] Primary body preview no longer falls back to flattened outfit unions or archetype-wide compatibility shell search when a `SimInfo` template has no authoritative body-driving `Nude` outfit record, so unresolved human archetypes fail honestly instead of rendering cross-species junk bodies
- [x] Exact `Hair` / `Accessory` slot resolution now uses the correct human CAS-slot predicate instead of the body-only filter, so authoritative head-related selections are no longer downgraded to compatibility fallback by a bad predicate
- [ ] Replace the remaining scene-level assembly inside that final stage with a true rig-centered torso/head assembly graph
- [ ] Add rig/skintone/morph layers to the rendered path

## Immediate Next Step

Keep the current body-first path authoritative and faster:

- start from the selected `SimInfo` template
- prefer one authoritative body-driving outfit record instead of flattening all outfit parts
- keep the current `body + head shell` preview honest and authoritative
- keep primary body preview unresolved when no authoritative body-driving outfit exists, instead of fabricating a shell from broad compatibility search
- keep clothing/accessories out of preview until they can be layered truthfully

## After This Block

Resume the next structural layers in order:

- rig/body/head integration
- skintone, region map, and morph/deformer application
