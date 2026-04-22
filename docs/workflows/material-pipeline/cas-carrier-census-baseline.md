# CAS Carrier Census Baseline

This document records the first broad whole-`CAS` carrier census built directly from the live whole-install shard set.

It is stronger than prose hints and stronger than the narrower `Sim archetype` census, but it is still not a full character-side shader-family census.

Related docs:

- [Corpus-Wide Family Census Baseline](corpus-wide-family-census-baseline.md)
- [Sim Archetype Material Carrier Census](sim-archetype-material-carrier-census.md)
- [Corpus-Wide Family Priority](corpus-wide-family-priority.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [Shader Family Registry](shader-family-registry.md)
- [Research Restart Guide](research-restart-guide.md)

## Primary rule

- this is a direct whole-`CAS` carrier census from the live index
- it counts `CAS` assets and `cas_part_facts`
- it does not yet count whole character-side shader families such as `SimSkin` or `SimGlass`

Safe reading:

- this layer is good for `CAS` prevalence, slot structure, body-part structure, and constraint prevalence
- this layer is not yet good for full character-side material-slot prevalence

## Source and command

Source:

- live shard set under `tmp/profile-index-cache/cache/`

Command:

- `dotnet tools/ProbeAsset/bin/Release/net8.0/win-x64/ProbeAsset.dll --census-cas-carriers tmp/profile-index-cache tmp/cas_carrier_census_fullscan.json`

Output:

- `tmp/cas_carrier_census_fullscan.json`

## Direct counted baseline

Current counted baseline:

- `CasAssets = 530507`
- `CasAssetPackages = 414`
- `CasPartFacts = 299028`
- `CasPartPackages = 407`

Current `CAS` asset top buckets:

- `Delta = 250465`
- `EP = 123892`
- `BaseGame-Data = 87979`
- `SP = 47491`
- `GP = 20608`
- `FP = 72`

Current `CAS` fact top buckets:

- `Delta = 113187`
- `EP = 93435`
- `BaseGame-Data = 38699`
- `SP = 36981`
- `GP = 16654`
- `FP = 72`

## Direct counted slot and body structure

Top slot categories:

- `Body Type 1140850688 = 113301 across 247 packages`
- `Top = 34896 across 149 packages`
- `Bottom = 23141 across 145 packages`
- `Hair = 22669 across 48 packages`
- `Full Body = 22651 across 147 packages`
- `Body Type 1090519040 = 12292 across 156 packages`
- `Shoes = 7560 across 132 packages`
- `Accessory = 5652 across 96 packages`

Top normalized categories:

- `body-type-1140850688 = 113301 across 247 packages`
- `top = 34896 across 149 packages`
- `bottom = 23141 across 145 packages`
- `hair = 22669 across 48 packages`
- `full-body = 22651 across 147 packages`
- `shoes = 7560 across 132 packages`
- `accessory = 5652 across 96 packages`

Constraint prevalence:

- `FactsWithNakedLink = 41522`
- `FactsRestrictOppositeGender = 19978`
- `FactsRestrictOppositeFrame = 112799`
- `FactsWithDefaultBodyType = 518`
- `FactsWithDefaultBodyTypeFemale = 160`
- `FactsWithDefaultBodyTypeMale = 148`

Safe reading:

- `Top`, `Bottom`, `Hair`, `Full Body`, `Shoes`, and `Accessory` are all broad real `CAS` strata, not narrow creator-side anecdotes
- shell and worn-slot authority work now has direct whole-`CAS` prevalence support
- `nakedLink` and opposite-frame restrictions are common enough to matter for implementation

## Direct counted species, age, and gender structure

Top species labels:

- `Unknown = 154104 across 256 packages`
- `Human = 139867 across 157 packages`
- `Horse = 3459 across 3 packages`
- `Cat = 536 across 4 packages`
- `Little Dog = 508 across 4 packages`
- `Dog = 506 across 4 packages`

Top age labels:

- `Unknown = 154128 across 256 packages`
- `Teen / Young Adult / Adult / Elder = 122030 across 144 packages`
- `Child = 16070 across 103 packages`
- `Toddler = 4012 across 54 packages`
- `Infant = 1293 across 18 packages`

Top gender labels:

- `Unknown = 130012 across 256 packages`
- `Female = 89950 across 316 packages`
- `Male = 56615 across 188 packages`
- `Unisex = 22451 across 123 packages`

Safe reading:

- `Unknown` is still a real index/data-quality bucket in `cas_part_facts`, not a reason to pretend the labels are complete
- the corpus is still clearly dominated by human and by the teen-through-elder range, but non-human species are real enough to keep species-aware boundaries in scope

## Honest index boundary

Current `assets`-table carrier flags for `CAS` rows:

- `AssetsWithExactGeometryCandidate = 0`
- `AssetsWithMaterialReferences = 0`
- `AssetsWithTextureReferences = 0`
- `AssetsWithMaterialResourceCandidate = 0`
- `AssetsWithTextureResourceCandidate = 0`
- `AssetsWithRigReference = 0`
- `AssetsWithPackageLocalGraph = 530507`
- `AssetsWithDiagnostics = 530507`

Current `assets`-table identity buckets:

- `IdentityType = CASPart` for all counted rows
- `PrimaryGeometryType = Unknown` for all counted rows

Safe reading:

- this does **not** mean `CAS` assets lack geometry or materials
- it means the current index layer does not surface those carrier flags on `CAS` asset rows
- therefore:
  - `cas_part_facts` are already a strong direct prevalence layer
  - `assets`-table geometry/material booleans are currently an index boundary for `CAS`
  - the next counting layer has to go deeper into `GEOM`/linked material carriers, not keep over-reading `assets` booleans

## What this layer proves

- there is now a broad direct whole-`CAS` prevalence layer, not just a `Sim archetype` subset
- shell and worn-slot authority work is now backed by real `CAS` slot prevalence
- constraint-heavy `CASP` semantics such as `nakedLink` and frame/gender restrictions are common enough to be implementation-significant
- character-side priority can now stand on:
  - whole-install corpus size
  - `Sim archetype` carrier census
  - whole-`CAS` slot/fact census

## Honest limit

This layer still does not prove:

- direct whole-character family frequency for `SimSkin`, `SimGlass`, `SimSkinMask`
- direct `GEOM`/`MTNF`/`RegionMap` linkage prevalence for `CAS` rows
- per-family material-slot prevalence on the character side
- full cross-domain ranking against object-side `MATD` profiles

That means:

- this is a strong `CAS` prevalence layer
- it is still not the final character-side family census

## Next strong counting step

- build the next direct `CAS`/`GEOM` linkage layer
- treat the current all-zero `CAS` asset carrier booleans as an index boundary, not as a semantic result
