# Corpus-Wide Family Priority

This document fixes one specific failure mode in the TS4 material research track:

- letting one convenient package slice or fixture lane drive the whole queue

Use it when deciding what to research next across the whole game corpus.

Related docs:

- [Corpus-Wide Family Census Baseline](corpus-wide-family-census-baseline.md)
- [MATD Shader Census Baseline](matd-shader-census-baseline.md)
- [Sim Archetype Material Carrier Census](sim-archetype-material-carrier-census.md)
- [CAS Carrier Census Baseline](cas-carrier-census-baseline.md)
- [CASPart Linkage Census Baseline](caspart-linkage-census-baseline.md)
- [CASPart GEOM Shader Census Baseline](caspart-geom-shader-census-baseline.md)
- [CASPart Parser Boundary](caspart-parser-boundary.md)
- [CASPart GEOM Resolution Boundary](caspart-geom-resolution-boundary.md)
- [Research Restart Guide](research-restart-guide.md)
- [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md)
- [Shader Family Registry](shader-family-registry.md)
- [Edge-Family Matrix](edge-family-matrix.md)
- [P1 Live-Proof Queue](p1-live-proof-queue.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Primary rule

- whole-game family priority comes before pack-local convenience
- pack-specific routes such as `EP10` or `EP11` are secondary validation lanes only
- a narrow lane may still be important, but only after it is selected by the wider family priority

## Evidence split

This doc separates priority inputs into three layers:

1. `externally confirmed family identity`
2. `counted corpus and package-slice prevalence`
3. `implementation-spec leverage`

Safe rule:

- counted corpus layout is priority input, not proof of semantics
- external sources define what a family is
- local counts only help decide how urgent it is to close that family for the app
- use [Corpus-Wide Family Census Baseline](corpus-wide-family-census-baseline.md) to distinguish:
  - counted corpus coverage
  - counted package-slice prevalence
  - derived family hints
- if a direct package-derived census exists for the exact scope being ranked, prefer it over older hint layers for that scope
- do not widen a direct scope-specific census beyond its scope:
  - object-side `MATD` prevalence is not the same thing as whole-game family prevalence

## Priority factors

Each family or seam should be ranked from the combined effect of:

1. how clearly the family is confirmed externally
2. how widely it appears in the current counted corpus or counted package slices
3. how much rendering/material behavior it affects if implemented wrongly
4. how much of the future implementation spec becomes clearer once it is closed
5. how representative it is across `Build/Buy`, `CAS`, and `Sim`

Do not rank only by:

- one pack
- one fixture
- one convenient reopen route
- one especially noisy local counter

## Current whole-game priority baseline

### Tier A: highest implementation priority

These are the families or seams that should dominate the queue unless a new stronger corpus-wide signal appears.

#### 1. `SimSkin` and the character material/compositor foundation

Why it stays high:

- externally confirmed as a real character family through `TS4SimRipper` and creator/tool packets
- direct package-derived prevalence is now much stronger than narrow edge families
- mistakes here affect the core body/head rendering path, skintone routing, and shell authority

Current whole-game basis:

- [Corpus-Wide Family Census Baseline](corpus-wide-family-census-baseline.md) now shows a fresh full-scan corpus of:
  - `743150` indexed assets
  - `530507` `Cas` assets across `414` package paths
  - `142941` `BuildBuy` assets across `491` package paths
- [Sim Archetype Material Carrier Census](sim-archetype-material-carrier-census.md) now adds a direct graph-backed character-side carrier floor:
  - `38` supported archetype graphs
  - `15` assets with surfaced skintone render carriers
  - `10` assets with surfaced base-texture carriers
  - `10` assets with overlays
  - `23` `FullBodyShell` and `12` `SplitBodyLayers`
- [CAS Carrier Census Baseline](cas-carrier-census-baseline.md) now adds a direct whole-`CAS` slot/fact floor:
  - `530507` `CAS` assets across `414` packages
  - `299028` `cas_part_facts` across `407` packages
  - `Top = 34896`
  - `Bottom = 23141`
  - `Hair = 22669`
  - `Full Body = 22651`
  - `Shoes = 7560`
  - `Accessory = 5652`
- [CASPart Linkage Census Baseline](caspart-linkage-census-baseline.md) now adds a deeper direct package-derived linkage floor:
  - `ParsedResources = 299028`
  - `RowsWithAnyGeometryCandidate = 281303`
  - `RowsWithTextureCandidates = 236668`
  - `RowsWithRegionMapCandidate = 108906`
  - `UniqueGeometryResources = 87466`
  - `UniqueTextureResources = 58252`
- [CASPart GEOM Shader Census Baseline](caspart-geom-shader-census-baseline.md) now adds the first direct character-side `GEOM` family floor:
  - `RowsWithResolvedGeometryShader = 281271`
  - `SimSkin = 280983` across `401` packages by `CASPart` rows
  - `SimGlass = 6048` across `189` packages by `CASPart` rows
  - `SimSkin = 86697` across `147` packages by unique linked `GEOM`
  - `SimGlass = 645` across `47` packages by unique linked `GEOM`
  - `GeometryResolvedFromExternalPackage = 12911`
- `tmp/precomp_sblk_inventory.json` still carries `simskin = 51` only as a derived family hint
- the surrounding docs still show open high-impact gaps in character authority order and compositor math

Safe reading:

- this is a core-family implementation track, not an edge curiosity
- the new carrier and linkage layers strengthened the priority floor for shell/compositor/skintone work before family counts existed
- the new `CASPart -> GEOM -> family` layer now also makes `SimSkin` versus `SimGlass` priority much less dependent on hints
- the large parser gap and the residual `GeometryKeyNotIndexed` tail still mean this is not yet full whole-`CAS` family closure

#### 2. Object-side glass and transparency

Why it stays high:

- externally confirmed through creator-facing object workflows and lineage glass-family vocabulary
- transparent objects are common enough in practical content to matter even when exact live-fixture closure is still incomplete
- wrong handling here causes broad visible regressions and misclassification between object glass, cutout transparency, and blended transparency

Current whole-game basis:

- the fresh full-scan census now shows `BuildBuy = 142941` indexed assets across `491` package paths
- the direct object-side `MATD` census now shows:
  - `28201` decoded rows
  - `FadeWithIce = 27434`
  - `g_ssao_ps_apply_params = 480`
  - `ObjOutlineColorStateTexture = 157`
- `tmp/precomp_sblk_inventory.json` shows `GlassForObjectsTranslucent = 24` and `AlphaBlended = 2` only as derived family hints
- current `Build/Buy` survey work proved that object-side transparency is common enough to deserve its own authority branch, even though one `EP10` decor cluster stalled at the present inspection layer

Safe reading:

- this family stays high because of semantic breadth and implementation impact, not because one `EP10` route happened to be convenient
- the new `MATD` census is a real object-side prevalence layer, but it does not replace character-side family counting

#### 3. `CAS/Sim` authority order and skintone/compositor math

Why it stays high:

- this is still one of the biggest unresolved blockers for turning research into a trustworthy implementation spec
- even where family identity is strong, exact authority order and compositor math remain partially open
- errors here cascade into almost every character-rendering result

Current whole-game basis:

- the shared guide still marks `Exact skintone/compositor math` as a dark gap
- the matrix docs still mark `Full per-family CAS/Sim authority order` as materially unfinished
- the fresh full-scan census confirms that character-side asset volume is large enough that these blockers remain whole-app, not niche, implementation problems
- the new `Sim archetype` carrier census shows that shell assembly, skintone references, and base-texture carry-through all surface in the graph-backed character subset, so this blocker is now backed by direct character-side carrier data, not only by external prose and broad corpus size
- the new whole-`CAS` census shows that shell and worn-slot structure are broad enough that authority/compositor mistakes remain high-priority even before direct `GEOM` linkage prevalence is counted
- the new `CASPart` linkage census now shows that parsed `CASPart -> GEOM/texture/region_map` carry-through is also broad in the character corpus, even though the current structured parser still leaves a large unresolved tail

Safe reading:

- this is not one family but one cross-family implementation blocker, and it should stay near the top of the queue

### Tier B: high priority, but narrower than Tier A

#### 4. `CASHotSpotAtlas`

Why it stays high:

- externally confirmed strongly through MorphMaker and slider tooling
- local corpus hints are unusually strong
- implementation leverage is high because it helps separate morph/edit atlas behavior from ordinary material slots

Current whole-game basis:

- `tmp/precomp_sblk_inventory.json` carries strong derived hints:
  - `CASHotSpotAtlas = 121`
  - `CASHotSpotAtlas = 47`
  - `CASHotSpotAtlas = 18`
  - `CASHotSpotAtlas = 16`
- the fresh full-scan census confirms that `CAS` is the dominant indexed asset domain, which makes high-confidence `CAS` helper families more valuable to close

Safe reading:

- this is a strong whole-game priority because both external certainty and local carry-through are high

#### 5. `ShaderDayNightParameters`

Why it stays high:

- externally supported as a real layered reveal/light-aware branch
- affects visible pass behavior rather than only metadata
- still lacks exact visible-pass closure, which blocks confident implementation

Current whole-game basis:

- `tmp/precomp_sblk_inventory.json` carries `name_guess = "ShaderDayNightParameters"` with `occurrences = 5` as a derived family hint
- the edge-family matrix already treats it as a distinct branch, not a generic unresolved bucket

Safe reading:

- this remains a good whole-game priority because it is externally real, visible, and not yet fully closed

#### 6. `GenerateSpotLightmap` and `NextFloorLightMapXform`

Why it stays high:

- externally supported as generated-light vocabulary
- local hints show a concentrated helper family rather than a one-off oddity
- implementation leverage is meaningful because it keeps generated-light helpers out of fake surface-slot logic

Current whole-game basis:

- `tmp/precomp_sblk_inventory.json` carries:
  - `name_guess = "GenerateSpotLightmap"` with `occurrences = 6`
  - `NextFloorLightMapXform = 14`
  - secondary `NextFloorLightMapXform = 3`

Safe reading:

- this is narrower than the core character and object-transparency tracks, but still high enough to stay in the main queue

### Tier C: real but currently narrower

#### 7. `RefractionMap`

Why it is still real:

- externally supported as a refraction/projective family
- local corpus hints show it is not imaginary
- the current route discipline is now strong and bounded

Current whole-game basis:

- `tmp/precomp_sblk_inventory.json` carries `name_guess = "RefractionMap"` with `occurrences = 6` as a derived family hint
- `tmp/probe_all_buildbuy_summary_full.json` carries `"RefractionMap": 33` as counted package-slice prevalence inside the processed `Build/Buy` survey
- the fresh full-scan census confirms that `BuildBuy` is broad enough to matter, but does not yet provide direct whole-game `RefractionMap` counts

Why it is not the main queue driver right now:

- the current refraction route is already bounded at the present inspection layer
- further work needs a new inspection layer or a stronger corpus-level reason
- it should not keep winning priority just because it recently had a clean route stack

Safe reading:

- real family, real queue row, but not the default next step after its current bounded ceiling

#### 8. `SimGlass`

Why it is still real:

- externally supported as a real character-side family
- current local external tooling packet remains strong
- it matters for preserving narrow character transparency semantics

Current whole-game basis:

- `tmp/precomp_sblk_inventory.json` carries `name_guess = "SimGlass"` with `occurrences = 1` as a derived family hint
- `tmp/probe_all_buildbuy_summary_full.json` carries `"SimGlass": 5` as counted package-slice prevalence inside the processed `Build/Buy` survey
- [CASPart GEOM Shader Census Baseline](caspart-geom-shader-census-baseline.md) now adds a direct character-side family floor:
  - `SimGlass = 6048` across `189` packages by `CASPart` rows
  - `SimGlass = 645` across `47` packages by unique linked `GEOM`

Why it is lower than Tier A:

- the current evidence now makes it much safer to say that `SimGlass` is real on the character side, but it is still far narrower than `SimSkin = 280983`, object-side transparency, or compositor authority
- `Build/Buy` carry-over presence keeps it alive, but does not promote it above broader families

Safe reading:

- preserve it carefully, but do not let it outrank the broader core rendering/material contracts

## Current queue discipline

Read the queue in this order:

1. check whether the next step strengthens a Tier A blocker
2. if not, check whether it closes a Tier B family with strong external support and strong local hints
3. only then continue a Tier C narrow lane
4. if a Tier C lane is already bounded at the current inspection layer, hand off instead of deepening it by inertia

Current safe examples:

- do not let bounded refraction routes keep winning by momentum
- do not let a stalled `EP10` transparent-decor cluster define object-transparency priority by itself
- do let object-side transparency remain high priority because the family is broad and implementation-relevant across the game
- do let the completed character-side family floor push `SimSkin` and compositor-authority work above narrower `SimGlass` carry-over lanes

## Honest limit

This document does not claim that the current counters are already a perfect census of TS4 shader/material frequency.

It only claims a safer priority rule:

- external evidence defines the family
- counted whole-game corpus layout helps rank urgency
- one package slice must not dominate the queue by convenience alone
- exact whole-game cross-domain popularity still needs stronger direct family counts beyond the current full-scan corpus layout, the object-side `MATD` profile layer, the completed character-side family floor, partial counted slices, and derived hint layers
- the next priority refinement should combine object-side and character-side direct family counts into one stricter cross-domain ranking layer
- the next implementation-facing packet should prefer `SimSkin` and character compositor-authority work before narrower `SimGlass` or `RefractionMap` lanes
