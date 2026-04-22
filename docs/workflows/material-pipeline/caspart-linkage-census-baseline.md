# CASPart Linkage Census Baseline

This document records the first direct package-derived `CASPart -> GEOM/texture/region_map` linkage census built from the fresh whole-install shard set.

It is stronger than the broader [CAS Carrier Census Baseline](cas-carrier-census-baseline.md), because it counts real linkage carriers surfaced from package payloads rather than only `CAS` asset rows and `cas_part_facts`.

It is still not a whole character-side family census.

Related docs:

- [Corpus-Wide Family Census Baseline](corpus-wide-family-census-baseline.md)
- [Corpus-Wide Family Priority](corpus-wide-family-priority.md)
- [CAS Carrier Census Baseline](cas-carrier-census-baseline.md)
- [Sim Archetype Material Carrier Census](sim-archetype-material-carrier-census.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [Shader Family Registry](shader-family-registry.md)
- [Research Restart Guide](research-restart-guide.md)

## Primary rule

- this is a direct package-derived linkage census
- it counts `CASPart` rows, parsed `CASPart` rows, and surfaced linkage carriers from package payloads
- it does not yet count whole character-side shader families such as `SimSkin` or `SimGlass`
- it does not justify ignoring the rows that the current structured parser still fails to decode

Safe reading:

- this layer is good for a first direct prevalence floor for `GEOM`, texture, and `region_map` carry-through on the character side
- this layer is not yet good for whole-`CAS` linkage closure, because the current structured parser only cleanly covers one large subset of raw `CASPart` rows

## Source and execution boundary

Source:

- fresh whole-install shard set under `tmp/profile-index-cache/cache/`
- raw output:
  - `tmp/caspart_linkage_census_fullscan.json`

Recovery execution path used for this first baseline:

- temporary PowerShell reflection scripts loaded the already-built `ProbeAsset` assemblies from:
  - `tools/ProbeAsset/bin/Release/net8.0/win-x64/`
- the scripts used the internal `Ts4CasPart` parser to count linkage carriers directly from package payloads

Current tool boundary:

- source code for a future direct command was added in:
  - [Program.cs](../../../tools/ProbeAsset/Program.cs)
- but the new binary was **not** rebuilt successfully, because `dotnet build` is currently blocked by SDK workload-resolution failures captured in:
  - `tmp/probeasset_build_diag.txt`
- the first honest baseline therefore comes from the recovery scripts and JSON output, not from a newly shipped `ProbeAsset` command

## Direct counted baseline

Current counted baseline:

- `CasPartResources = 530507`
- `CasPartPackages = 414`
- `ParsedResources = 299028`
- `ZeroLengthResources = 766`
- `TotalFailures = 230713`

Safe reading:

- `530507` is the raw whole-corpus `CASPart` floor
- `299028` is the currently parsable structured-linkage floor
- the large remaining gap is explicit and must not be smoothed away

## Direct counted linkage carriers

Current counted linkage carriers inside the parsed subset:

- `RowsWithLods = 106640`
- `RowsWithGeometryInLods = 106400`
- `RowsWithFallbackGeometry = 174906`
- `RowsWithAnyGeometryCandidate = 281303`
- `RowsWithLodGeometryOnly = 106397`
- `RowsWithFallbackGeometryOnly = 174903`
- `RowsWithBothGeometryPaths = 3`
- `RowsWithRigCandidates = 0`
- `RowsWithTextureCandidates = 236668`
- `RowsWithDiffuseCandidate = 116330`
- `RowsWithShadowCandidate = 129585`
- `RowsWithRegionMapCandidate = 108906`
- `RowsWithNormalCandidate = 33355`
- `RowsWithSpecularCandidate = 112582`
- `RowsWithEmissionCandidate = 53306`
- `RowsWithColorShiftMaskCandidate = 59319`

Current counted unique carrier resources:

- `UniqueGeometryResources = 87466`
- `UniqueTextureResources = 58252`
- `UniqueRegionMapResources = 3598`

Safe reading:

- direct `CASPart -> GEOM` carry-through is now proven as a broad character-side carrier stratum
- direct texture and `region_map` carry-through are also broad in the same parsed subset
- this is still a linkage-carrier floor, not a family census

## Direct counted package distribution

Top buckets:

- `Delta = 250465`
- `EP = 123892`
- `BaseGame-Data = 87979`
- `SP = 47491`
- `GP = 20608`
- `FP = 72`

Package classes:

- `SimulationPreload = 147119`
- `ClientDeltaBuild = 106891`
- `SimulationDeltaBuild = 106891`
- `ClientFullBuild = 84803`
- `SimulationFullBuild = 84803`

Safe reading:

- the parsed linkage floor is broad across the install, not one-pack local
- delta-heavy prevalence is real here, but it is still prevalence of parsable `CASPart` linkage rows, not prevalence of resolved final families

## Direct counted slot structure inside the parsed subset

Top slot categories:

- `Body Type 1140850688 = 113301 across 247 packages`
- `Top = 34896 across 149 packages`
- `Bottom = 23141 across 145 packages`
- `Hair = 22669 across 48 packages`
- `Full Body = 22651 across 147 packages`
- `Body Type 1090519040 = 12292 across 156 packages`
- `Shoes = 7560 across 132 packages`
- `Accessory = 5652 across 96 packages`

Safe reading:

- the broader whole-`CAS` slot/fact census is now complemented by a narrower parsed-linkage layer for the same worn-slot structure
- shell and worn-slot authority work can now stand on both:
  - whole-`CAS` slot prevalence
  - direct parsed linkage prevalence

## Honest parser boundary

Current failure buckets:

- `LOD asset list extends beyond payload = 201500`
- `Read beyond end of stream = 8650`
- `Structured body beyond TGI table = 7283`
- `CASPart tag multimap extends beyond payload = 6325`
- `CASPart override list extends beyond payload = 5755`
- `CASPart linked part list extends beyond payload = 729`
- `CASPart swatch color extends beyond payload = 333`
- `CASPart slot key list extends beyond payload = 138`

Current failure top buckets:

- `Delta = 136514`
- `BaseGame-Data = 49278`
- `EP = 30457`
- `SP = 10510`
- `GP = 3954`

Current failure package classes:

- `SimulationPreload = 73147`
- `ClientDeltaBuild = 61302`
- `SimulationDeltaBuild = 61302`
- `ClientFullBuild = 17481`
- `SimulationFullBuild = 17481`

Safe reading:

- the current structured parser clearly does not cover all raw `CASPart` layouts in the corpus
- this is a parser and tooling boundary, not a semantic statement that the remaining rows are unimportant
- the current direct linkage layer must therefore be read as:
  - strong parsable-subset prevalence
  - not full whole-`CAS` linkage closure

## What this layer proves

- there is now a direct package-derived character-side linkage census below the old `cas_part_facts` layer
- `CASPart -> GEOM`, texture, and `region_map` carry-through are broad and implementation-significant in the parsable subset
- character-side priority no longer rests only on:
  - external family wording
  - whole-`CAS` slot/fact prevalence
  - the narrower `Sim archetype` subset

## Honest limit

This layer still does not prove:

- whole-character family frequency for `SimSkin`, `SimGlass`, or `SimSkinMask`
- full whole-`CAS` linkage prevalence across all `530507` raw `CASPart` rows
- exact `GEOM`-side family prevalence
- final per-family material-slot prevalence

That means:

- this is the first strong direct `CASPart` linkage floor
- it is not the final character-side family census

## Next strong step

- preserve this layer as the current character-side linkage floor
- do not overread the `299028` parsed rows as full closure
- either:
  - use this stronger prevalence floor to reprioritize Tier A character-side family/spec work, or
  - build the next parser-boundary packet for the `230713` failing rows before claiming a full whole-`CAS` linkage census
