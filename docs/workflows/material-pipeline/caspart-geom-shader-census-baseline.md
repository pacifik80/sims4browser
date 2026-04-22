# CASPart GEOM Shader Census Baseline

This document records the completed direct package-derived `CASPart -> linked GEOM -> shader family/profile` census built from the fresh whole-install shard set.

It is stronger than the broader [CASPart Linkage Census Baseline](caspart-linkage-census-baseline.md), because it counts resolved `GEOM` shader identities rather than only linkage carriers.

It is still not a full whole-character family census.

Related docs:

- [Corpus-Wide Family Census Baseline](corpus-wide-family-census-baseline.md)
- [Corpus-Wide Family Priority](corpus-wide-family-priority.md)
- [CASPart Linkage Census Baseline](caspart-linkage-census-baseline.md)
- [CAS Carrier Census Baseline](cas-carrier-census-baseline.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [Shader Family Registry](shader-family-registry.md)
- [Research Restart Guide](research-restart-guide.md)

## Primary rule

- this is a direct package-derived character-side `GEOM` family floor
- it counts the currently parsable `CASPart` subset and the linked `GEOM` resources that the resumable recovery path could resolve from the current package or indexed external packages
- it does not replace external family identity
- it does not justify treating unresolved `GEOM` rows as semantically absent

Safe reading:

- this layer is good for direct character-side family urgency across the currently parsable `CASPart -> GEOM` floor
- this layer is not yet good for full whole-`CAS` family prevalence, because the structured-parser gap is still explicit and the residual geometry-index gap is still non-zero

## Source and naming boundary

Source:

- fresh whole-install shard set under `tmp/profile-index-cache/cache/`
- raw output:
  - `tmp/caspart_geom_shader_census_fullscan.json`
- resumable runner:
  - `tmp/caspart_geom_shader_census_resumable.ps1`
- safe-point run root:
  - `tmp/caspart_geom_shader_census_run/`

Execution boundary:

- temporary PowerShell reflection loaded the already-built `ProbeAsset` assemblies from:
  - `tools/ProbeAsset/bin/Release/net8.0/win-x64/`
- the script used the internal `Ts4CasPart` parser and `LlamaLogic.Packages.DataBasePackedFile`
- the run was executed through PowerShell 7 because the current assembly set did not load cleanly under Windows PowerShell

GEOM-side naming boundary:

- `tmp/precomp_shader_profiles.json` is useful as a hash-to-name helper, but it is not safe by itself for `GEOM` family naming
- the runner therefore overrides known character-side shader hashes with the external code-backed `TS4SimRipper` enum packet:
  - `0x548394B9 -> SimSkin`
  - `0x5EDA9CDE -> SimGlass`
  - `0x941695AE -> SimWings`
  - `0x9516A357 -> SimGhost`
  - `0xB9105A6D -> Phong`
- this prevents cross-domain drift such as reading `0x548394B9` as `GenerateSpotLightmap` from the precompiled profile snapshot

## Direct counted baseline

Current counted baseline:

- `CasPartResources = 530507`
- `CasPartPackages = 414`
- `ParsedResources = 299028`
- `ZeroLengthResources = 766`
- `TotalFailures = 230713`
- `RowsWithAnyGeometryCandidate = 281303`
- `RowsWithResolvedGeometryShader = 281271`
- `RowsWithUnknownGeometryShader = 32`
- `GeometryCandidatesTotal = 3628904`
- `UniqueGeometryCandidates = 87466`
- `GeometryResourcesResolved = 369202`
- `GeometryResolvedFromCurrentPackage = 356291`
- `GeometryResolvedFromExternalPackage = 12911`
- `UniqueGeometryResolvedFromExternalPackage = 2854`
- `UniqueResolvedGeometryResources = 87348`
- `GeometryResourcesMissing = 531`
- `UniqueUnresolvedGeometryCandidates = 118`
- `GeometryDecodeFailures = 0`

Safe reading:

- this is the first completed direct character-side family-count layer below the `CASPart` linkage floor
- `SimSkin` and `SimGlass` prevalence can now be discussed from package payloads rather than mainly from external tooling or derived hints
- cross-package geometry resolution is now materially broad rather than same-package only
- the remaining unresolved geometry tail inside the parsed subset is small relative to the parser gap

## Direct counted GEOM family floor

Current counted families by `CASPart` rows:

- `SimSkin = 280983` across `401` packages
- `SimGlass = 6048` across `189` packages
- `Phong = 33` across `5` packages

Current counted profiles by `CASPart` rows:

- `SimSkin = 280983` across `401` packages
- `SimGlass = 6048` across `189` packages
- `Phong = 33` across `5` packages

Current counted families by unique linked `GEOM` resources:

- `SimSkin = 86697` across `147` packages
- `SimGlass = 645` across `47` packages
- `Phong = 6` across `1` package

Current counted profiles by unique linked `GEOM` resources:

- `SimSkin = 86697` across `147` packages
- `SimGlass = 645` across `47` packages
- `Phong = 6` across `1` package

Safe reading:

- `SimSkin` is now directly dominant on the resolved character-side `GEOM` floor
- `SimGlass` is now directly confirmed as real but much narrower on the same floor
- `Phong` exists, but it is currently only a tiny tail here

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

- this family floor is broad across the install, not a one-pack curiosity
- delta-heavy prevalence is real here too, but it is still the prevalence of the currently parsable `CASPart -> GEOM` subset

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

- the direct family floor sits on the same broad shell and worn-slot structure already seen in the slot/fact and linkage baselines
- character-side documentation priority no longer depends only on slot prevalence or a narrow `Sim archetype` subset

## Honest resolution boundary

Current parser failure buckets:

- `LOD asset list extends beyond payload = 201500`
- `Read beyond end of stream = 8650`
- `Structured body beyond TGI table = 7283`
- `CASPart tag multimap extends beyond payload = 6325`
- `CASPart override list extends beyond payload = 5755`
- `CASPart linked part list extends beyond payload = 729`
- `CASPart swatch color extends beyond payload = 333`
- `CASPart slot key list extends beyond payload = 138`

Current geometry-missing reasons:

- `GeometryKeyNotIndexed = 531`

Current resolution boundary:

- the resumable runner resolves linked `GEOM` resources through the current source package and indexed external packages across the shard set
- unresolved rows therefore now mean:
  - parser failure, or
  - linked `GEOM` keys that do not currently exist in the shard index
- they do not mean that the unresolved families are semantically absent

Safe reading:

- the direct family floor is already useful for priority
- it is still a floor, but it is now parser-bounded much more than same-package-bounded

## What this layer proves

- there is now a direct package-derived character-side family-count layer below the old slot/fact and linkage layers with broad cross-package `GEOM` resolution
- `SimSkin` is not only externally real; it is also directly dominant across `281271` resolved character-side `CASPart -> GEOM` rows and `401` packages
- `SimGlass` is not only externally real; it is also directly present across a non-trivial but much narrower resolved character-side `GEOM` floor
- the documentation queue can now prioritize `SimSkin`, body/head shell authority, and compositor work from direct package counts rather than mostly from hints

## Honest limit

This layer still does not prove:

- full whole-`CAS` family prevalence across all `530507` raw `CASPart` rows
- exact cross-domain popularity order when object-side family counts are still asymmetric
- absence or rarity of families that mostly live outside the current parser reach or the residual geometry index
- final per-family material-slot prevalence or compositor order

That means:

- this is the completed current direct `CASPart -> GEOM -> family` floor
- it is not yet the final whole-character family census

## Next strong step

- preserve this layer as the current direct character-side family floor
- treat the structured-parser gap as the main remaining census integrity blocker
- keep the residual `GeometryKeyNotIndexed` tail explicit, but do not use it to defer priority rebind
- next, combine this completed character-side floor with the object-side `MATD` census into one stricter cross-domain ranking layer
- after that, use the `SimSkin`-dominant floor to drive body/head shell and compositor-authority packets before narrower carry-over lanes such as `SimGlass` or `RefractionMap`
