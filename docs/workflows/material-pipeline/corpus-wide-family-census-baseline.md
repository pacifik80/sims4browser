# Corpus-Wide Family Census Baseline

This document records the current honest baseline for family popularity and corpus coverage.

Use it before saying that one shader/material family is "common", "rare", or "high-priority because it is widespread".

Related docs:

- [Corpus-Wide Family Priority](corpus-wide-family-priority.md)
- [MATD Shader Census Baseline](matd-shader-census-baseline.md)
- [Sim Archetype Material Carrier Census](sim-archetype-material-carrier-census.md)
- [CAS Carrier Census Baseline](cas-carrier-census-baseline.md)
- [CASPart Linkage Census Baseline](caspart-linkage-census-baseline.md)
- [CASPart GEOM Shader Census Baseline](caspart-geom-shader-census-baseline.md)
- [CASPart Parser Boundary](caspart-parser-boundary.md)
- [CASPart GEOM Resolution Boundary](caspart-geom-resolution-boundary.md)
- [Research Restart Guide](research-restart-guide.md)
- [P1 Live-Proof Queue](p1-live-proof-queue.md)
- [Shader Family Registry](shader-family-registry.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Primary rule

- family popularity must come from packages or the live index when possible
- docs alone are not a valid source for prevalence claims
- derived family hints are still useful, but they must be labeled as hints, not as a full census

## Counted layers

### 1. Full filesystem profile-index census

These numbers come from a fresh full scan of the installed game corpus, not from the older live cache:

- command:
  - `tools/ProbeAsset/bin/Release/net8.0/win-x64/ProbeAsset.exe --profile-index "C:\GAMES\The Sims 4" 6000 16 largest`
- log:
  - `tmp/profile_index_fullscan_2026-04-20.log`
- cache root:
  - `tmp/profile-index-cache/cache/`
- shard set:
  - `index.sqlite`
  - `index.shard01.sqlite`
  - `index.shard02.sqlite`
  - `index.shard03.sqlite`

Current counted baseline:

- filesystem package files selected by the scan = `4965`
- indexed package paths persisted across the shard set = `4963`
- indexed resources across the shard set = `4789589`
- indexed assets across the shard set = `743150`
- indexed asset-bearing package paths = `603`

Current counted asset-domain totals:

- `Cas = 530507` assets across `414` package paths
- `BuildBuy = 142941` assets across `491` package paths
- `General3D = 68158` assets across `203` package paths
- `Sim = 1544` assets across `276` package paths

Current counted top-level package totals:

- `Delta = 2437`
- `SP = 1741`
- `EP = 477`
- `GP = 256`
- `BaseGame-Data = 49`
- `FP = 3`

Current counted asset-bearing package totals by top-level slice:

- `Delta = 323`
- `SP = 200`
- `EP = 49`
- `GP = 24`
- `BaseGame-Data = 5`
- `FP = 2`

Current counted asset rows by top-level slice:

- `Delta = 342916`
- `EP = 178838`
- `BaseGame-Data = 130559`
- `SP = 56846`
- `GP = 33876`
- `FP = 115`

Current counted package classes inside the indexed corpus:

- total package rows:
  - `Strings_* = 3945`
  - `SimulationPreload = 167`
  - `ClipHeader = 167`
  - `ClientDeltaBuild = 127`
  - `ClientFullBuild = 126`
  - `thumbnails = 113`
  - `SimulationDeltaBuild = 112`
  - `SimulationFullBuild = 111`
  - `magalog = 92`
  - `thumbnailsdeltabg = 1`
  - `thumbnailsdeltapack = 1`
  - `UI = 1`
- asset-bearing package rows:
  - `SimulationPreload = 167`
  - `SimulationFullBuild = 111`
  - `ClientFullBuild = 111`
  - `SimulationDeltaBuild = 107`
  - `ClientDeltaBuild = 107`

Current counted asset rows by package class:

- `SimulationPreload = 179966`
- `ClientDeltaBuild = 170051`
- `ClientFullBuild = 145620`
- `SimulationDeltaBuild = 139438`
- `SimulationFullBuild = 108075`

Integrity note:

- the scan selected `4965` package files, but only `4963` package rows persisted in the shard set
- the two currently missing package paths are:
  - `C:\GAMES\The Sims 4\EP18\ClientFullBuild0.package`
  - `C:\GAMES\The Sims 4\EP18\SimulationFullBuild0.package`
- no package/resource/asset rows for those two paths currently appear in any shard
- therefore this census is already much stronger than the old partial numbers, but it still carries one explicit scan-integrity gap

Safe reading:

- the older live-cache counts are now superseded by this fresh full filesystem census
- the whole-game corpus is much larger than the earlier `1240 / 161303 / 1125911` layer suggested
- only `603` package paths currently carry indexed 3D-ish assets, so raw package-file counts and asset-bearing package counts must not be conflated
- `CAS` is still the dominant asset domain by row count, but `Build/Buy` remains broad by package coverage

What this layer does not currently give directly:

- there is still no ready-made family column for `SimGlass`, `RefractionMap`, `ShaderDayNightParameters`, and similar names
- the fresh full scan gives a real corpus-size baseline and a real domain/package layout baseline
- it does not yet give direct whole-game family counts by shader/material row

### 1.5. Full-corpus material-carrying resource strata

The fresh shard set already gives one stronger statistics layer than raw package or asset totals: resource-type prevalence for the material pipeline.

Current counted material-relevant resource totals:

- `CASPart = 530507` resources across `414` package paths
- `Geometry = 187832` resources across `152` package paths
- `ModelLOD = 105743` resources across `162` package paths
- `Model = 52122` resources across `161` package paths
- `MaterialDefinition = 28225` resources across `75` package paths
- `MaterialSet = 514` resources across `80` package paths
- `ObjectDefinition = 433310` resources across `467` package paths
- `ObjectCatalog = 451578` resources across `489` package paths
- `RegionMap = 16896` resources across `256` package paths
- `Skintone = 635` resources across `19` package paths
- `Light = 33795` resources across `141` package paths
- `Rig = 18866` resources across `439` package paths
- `Slot = 19762` resources across `429` package paths
- `Footprint = 51734` resources across `294` package paths
- `SimModifier = 5935` resources across `18` package paths
- `DeformerMap = 1568` resources across `7` package paths
- `BlendGeometry = 4778` resources across `10` package paths

Current counted top-level distribution for selected material-relevant resource types:

- `CASPart`:
  - `Delta = 250465`
  - `EP = 123892`
  - `BaseGame-Data = 87979`
  - `SP = 47491`
  - `GP = 20608`
  - `FP = 72`
- `Geometry`:
  - `Delta = 81287`
  - `EP = 49811`
  - `BaseGame-Data = 25941`
  - `SP = 18454`
  - `GP = 12183`
  - `FP = 156`
- `ModelLOD`:
  - `EP = 37342`
  - `BaseGame-Data = 29586`
  - `Delta = 22096`
  - `GP = 10753`
  - `SP = 5939`
  - `FP = 27`
- `Model`:
  - `EP = 17968`
  - `BaseGame-Data = 16047`
  - `Delta = 11176`
  - `GP = 4715`
  - `SP = 2205`
  - `FP = 11`
- `MaterialDefinition`:
  - `BaseGame-Data = 15421`
  - `EP = 6786`
  - `GP = 2413`
  - `Delta = 1841`
  - `SP = 1764`
- `MaterialSet`:
  - `BaseGame-Data = 242`
  - `EP = 148`
  - `Delta = 88`
  - `GP = 20`
  - `SP = 16`
- `RegionMap`:
  - `EP = 7772`
  - `BaseGame-Data = 3450`
  - `SP = 2920`
  - `GP = 1982`
  - `Delta = 752`
  - `FP = 20`
- `Skintone`:
  - `BaseGame-Data = 473`
  - `EP = 90`
  - `Delta = 72`
- `Light`:
  - `BaseGame-Data = 13180`
  - `EP = 12789`
  - `GP = 3784`
  - `SP = 2084`
  - `Delta = 1947`
  - `FP = 11`

Safe reading:

- this is still not direct shader-family prevalence
- it is already a real whole-corpus statistics layer for the material pipeline
- it shows that character-side and object-side material carriers are both broad, but they are broad in different ways:
  - `CASPart` and `Geometry` dominate by count
  - `ObjectDefinition`, `ObjectCatalog`, `Rig`, and `Slot` dominate by package coverage
  - `MaterialDefinition` is much broader than `MaterialSet`
- `Skintone`, `SimModifier`, `DeformerMap`, and `BlendGeometry` are much narrower but still clearly real corpus strata

### 1.6. Direct object-side MATD shader-profile census

Current source:

- [MATD Shader Census Baseline](matd-shader-census-baseline.md)
- `tmp/matd_shader_census_fullscan.json`

Current counted baseline:

- `MaterialDefinitionResources = 28225`
- `DecodedResources = 28201`
- `EmptyResources = 24`
- `Failures = 0`

Current counted top profiles:

- `FadeWithIce = 27434` across `73` packages
- `g_ssao_ps_apply_params = 480` across `20` packages
- `ObjOutlineColorStateTexture = 157` across `5` packages
- `texgen = 128` across `2` packages
- `ReflectionStrength = 2` across `1` package

Safe reading:

- this is the first real direct shader-profile prevalence layer from the fresh full-install scan
- it is stronger than old family hints because it comes from package resources, not from prose
- it is still object-side only and must not be overread as a whole-game family census

### 1.7. Direct Sim archetype material-carrier census

Current source:

- [Sim Archetype Material Carrier Census](sim-archetype-material-carrier-census.md)
- `tmp/sim_material_carrier_census.json`

Current counted baseline:

- `TotalAssets = 38`
- `SupportedAssets = 38`
- `AssetsWithSkintoneRender = 15`
- `UniqueSkintoneResources = 11`
- `AssetsWithBaseTexture = 10`
- `UniqueBaseTextureResources = 8`
- `AssetsWithOverlayTextures = 10`
- `AssetsWithViewportTint = 10`

Current counted assembly structure:

- `FullBodyShell = 23`
- `SplitBodyLayers = 12`
- `None = 3`

Current counted contract structure:

- `IndexedDefaultBodyRecipe = 29`
- `ExplicitBodyDriving = 6`
- `Unresolved = 3`

Current counted top carrier coverage:

- body foundations:
  - `Base frame = total 38 across 38 assets`
  - `Skin pipeline = total 21 across 21 assets`
- body sources:
  - `Skintone reference = total 15 across 15 assets`
- slot groups:
  - `Skintone = total 15 across 15 assets`

Safe reading:

- this is the first direct character-side carrier census from built preview graphs
- it is stronger than old character-side prose or hint layers
- it is still only a `Sim archetype` subset census, not a whole `CAS/Sim` family census
- use it to ground Tier A character-side priority more honestly
- do not overread it as whole-game `SimSkin` / `SimGlass` frequency

### 1.8. Direct whole-CAS carrier census

Current source:

- [CAS Carrier Census Baseline](cas-carrier-census-baseline.md)
- `tmp/cas_carrier_census_fullscan.json`

Current counted baseline:

- `CasAssets = 530507`
- `CasAssetPackages = 414`
- `CasPartFacts = 299028`
- `CasPartPackages = 407`

Current counted slot structure:

- `Top = 34896 across 149 packages`
- `Bottom = 23141 across 145 packages`
- `Hair = 22669 across 48 packages`
- `Full Body = 22651 across 147 packages`
- `Shoes = 7560 across 132 packages`
- `Accessory = 5652 across 96 packages`

Current counted constraint prevalence:

- `FactsWithNakedLink = 41522`
- `FactsRestrictOppositeGender = 19978`
- `FactsRestrictOppositeFrame = 112799`

Current direct index boundary:

- for `CAS` asset rows, the current index surfaces:
  - `IdentityType = CASPart` for all rows
  - `PrimaryGeometryType = Unknown` for all rows
  - `0` for all current geometry/material carrier booleans

Safe reading:

- this is now a strong whole-`CAS` slot/fact prevalence layer
- it is stronger than the narrower `Sim archetype` subset for `CASP`/slot structure
- it is not yet a direct `GEOM`/material linkage census
- the all-zero `CAS` asset carrier booleans are an index boundary, not a semantic claim that `CAS` assets have no geometry or materials

### 1.9. Direct `CASPart` linkage census

Current source:

- [CASPart Linkage Census Baseline](caspart-linkage-census-baseline.md)
- `tmp/caspart_linkage_census_fullscan.json`

Current counted baseline:

- `CasPartResources = 530507`
- `ParsedResources = 299028`
- `ZeroLengthResources = 766`
- `TotalFailures = 230713`
- `RowsWithAnyGeometryCandidate = 281303`
- `RowsWithTextureCandidates = 236668`
- `RowsWithRegionMapCandidate = 108906`
- `UniqueGeometryResources = 87466`
- `UniqueTextureResources = 58252`
- `UniqueRegionMapResources = 3598`

Current counted failure boundary:

- `LOD asset list extends beyond payload = 201500`
- `Read beyond end of stream = 8650`
- `Structured body beyond TGI table = 7283`
- `CASPart tag multimap extends beyond payload = 6325`
- `CASPart override list extends beyond payload = 5755`

Safe reading:

- this is the first direct package-derived `CASPart -> GEOM/texture/region_map` prevalence floor
- it is much stronger than slot/fact counts alone
- it still covers only the currently parsable structured subset, not the entire raw `CASPart` corpus
- therefore this layer is good for character-side linkage urgency and authority weighting
- it is not yet a whole-character family census

### 1.10. Direct `CASPart -> GEOM -> shader family` census

Current source:

- [CASPart GEOM Shader Census Baseline](caspart-geom-shader-census-baseline.md)
- `tmp/caspart_geom_shader_census_fullscan.json`

Current counted baseline:

- `CasPartResources = 530507`
- `ParsedResources = 299028`
- `RowsWithAnyGeometryCandidate = 281303`
- `RowsWithResolvedGeometryShader = 281271`
- `RowsWithUnknownGeometryShader = 32`
- `GeometryCandidatesTotal = 3628904`
- `UniqueGeometryCandidates = 87466`
- `UniqueResolvedGeometryResources = 87348`
- `GeometryResourcesResolved = 369202`
- `GeometryResolvedFromCurrentPackage = 356291`
- `GeometryResolvedFromExternalPackage = 12911`
- `UniqueGeometryResolvedFromExternalPackage = 2854`
- `GeometryResourcesMissing = 531`
- `GeometryDecodeFailures = 0`

Current counted families by `CASPart` rows:

- `SimSkin = 280983` across `401` packages
- `SimGlass = 6048` across `189` packages
- `Phong = 33` across `5` packages

Current counted families by unique linked `GEOM`:

- `SimSkin = 86697` across `147` packages
- `SimGlass = 645` across `47` packages
- `Phong = 6` across `1` package

Current counted naming safeguard:

- the recovery path overrides known character-side `GEOM` hashes from the external `TS4SimRipper` enum packet so `0x548394B9` is counted as `SimSkin` rather than inheriting a cross-domain precompiled guess such as `GenerateSpotLightmap`

Current integrity ceilings:

- parser ceiling:
  - [CASPart Parser Boundary](caspart-parser-boundary.md)
- geometry-resolution ceiling:
  - [CASPart GEOM Resolution Boundary](caspart-geom-resolution-boundary.md)

Safe reading:

- this is the first completed direct character-side family-count layer from package payloads
- it is much stronger than slot/fact or linkage prevalence alone for ranking `SimSkin` versus `SimGlass`
- it now covers the currently parsable `CASPart` subset with broad cross-package geometry resolution across the shard set
- the residual unknown-row tail is now narrow; the parser gap remains the dominant coverage boundary, not evidence of family absence

### 2. Package-derived family survey layers

These are real package-based counts, but still narrower than a full whole-game family census.

#### Build/Buy family survey summary

Current source:

- `tmp/probe_all_buildbuy_summary_full.json`

Current counted baseline:

- input entries = `53113`
- processed entries = `1380`
- resolved scenes = `1380`
- status mix:
  - `Partial = 1337`
  - `SceneReady = 35`
  - `Unsupported = 8`

Current family counts from that processed `Build/Buy` slice:

- `RefractionMap = 33`
- `SimGlass = 5`
- `WorldToDepthMapSpaceMatrix = 8`
- `DecalMap = 83`
- `SeasonalFoliage = 172`
- `WriteDepthMask = 24`
- `painting = 28`
- `colorMap7 = 971`

Safe reading:

- this is real package-derived evidence
- it is useful for relative strength inside the processed `Build/Buy` slice
- it is not a full whole-game family census because:
  - it only covers `Build/Buy`
  - it only covers the processed subset, not all `53113` entries
  - it reflects current survey reach and current decoder visibility

### 3. Derived local family-hint layers

Current source:

- `tmp/precomp_sblk_inventory.json`

Examples now used as hints:

- `simskin = 51`
- `CASHotSpotAtlas = 121 / 47 / 18 / 16`
- `GlassForObjectsTranslucent = 24`
- `AlphaBlended = 2`
- `ShaderDayNightParameters occurrences = 5`
- `GenerateSpotLightmap occurrences = 6`
- `NextFloorLightMapXform = 14 / 3`
- `RefractionMap occurrences = 6`
- `SimGlass occurrences = 1`
- `SimSkinMask = 12`

Safe reading:

- these are derived hints from local corpus archaeology
- they are useful for priority shaping when no direct family field exists in the live index
- they are not a substitute for package-level or index-level census

## Current confidence ladder for popularity claims

When describing popularity or prevalence, use these labels:

### `counted corpus coverage`

Allowed only for:

- package totals
- asset totals
- domain totals
- any future direct family counts that come from package/index data

### `counted package-slice prevalence`

Allowed for:

- `Build/Buy` survey outputs like `probe_all_buildbuy_summary_full.json`
- any future per-domain family surveys built directly from packages

### `derived family hint`

Allowed for:

- `precomp_sblk_inventory.json`
- similar local archaeology outputs where family visibility is indirect

Do not say:

- "family X is common in the whole game"

unless the statement is backed by either:

- direct whole-corpus family counts, or
- a clearly labeled approximation built from multiple counted slices

## Current honest conclusions

What is already safe to say:

- the old partial live-cache counts are no longer the census baseline
- the fresh full scan now gives a real whole-install corpus baseline:
  - `4965` selected package files
  - `4963` indexed package paths
  - `4789589` resources
  - `743150` assets
- only `603` package paths currently carry indexed assets, which matters for material/shader prioritization far more than raw package-file count
- `CAS` is the dominant indexed asset domain by row count
- `Build/Buy` remains broad by asset-bearing package coverage
- the completed direct character-side `GEOM` family floor now makes one whole-app ranking point materially safer:
  - `SimSkin = 280983` across `401` packages by `CASPart` rows on the resolved character-side geometry floor
  - `SimGlass = 6048` across `189` packages by `CASPart` rows on that same floor
  - `12911` resolved geometry hits already come from external packages, so same-package reachability is no longer the active explanation for the old low counts
- family counts for rows like `RefractionMap` and `SimGlass` are still only partially counted:
  - `SimGlass` is now directly counted on the character side and still only partially counted on the `Build/Buy` side
  - rows like `RefractionMap` are still mainly counted within a processed `Build/Buy` slice plus narrower derived/local layers

What is not yet safe to say:

- exact whole-game ranking of all shader/material families by raw frequency
- exact cross-domain popularity order for `SimGlass`, `RefractionMap`, `ShaderDayNightParameters`, `GenerateSpotLightmap`, and similar rows

## Next census step

The next stronger census packet should build one direct family-count layer from package/index data across more than one domain.

Best next target:

- keep the completed character-side `CASPart -> GEOM -> family` floor as the current direct character-side prevalence baseline
- then add one direct cross-domain family-count pass over the fresh shard set for the current high-priority families across at least:
  - object-side `MATD`
  - character-side `CASPart -> GEOM`
  - any available `Sim`-side indexed/resource layer
- separately decide whether to spend census effort on:
  - the large `CASPart` parser gap, or
  - the residual `GeometryKeyNotIndexed = 531` geometry tail
- separately resolve the current scan-integrity gap for the two missing `EP18` package paths so future whole-corpus numbers do not silently drift

That would allow the queue to say:

- not only which families are externally important
- but also which ones are actually widespread across the live corpus and which ones are only narrow tails inside one counted domain
