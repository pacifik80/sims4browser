# CASPart GEOM Resolution Boundary

This document records the residual geometry-resolution boundary for the direct character-side `CASPart -> GEOM -> shader family` census.

It exists so the completed cross-package recovery path does not get overstated as full whole-`CAS` family closure.

Related docs:

- [CASPart GEOM Shader Census Baseline](caspart-geom-shader-census-baseline.md)
- [CASPart Linkage Census Baseline](caspart-linkage-census-baseline.md)
- [Corpus-Wide Family Census Baseline](corpus-wide-family-census-baseline.md)
- [Corpus-Wide Family Priority](corpus-wide-family-priority.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [Research Restart Guide](research-restart-guide.md)

## Primary rule

- current `CASPart -> GEOM -> family` counts cover the linked `GEOM` resources that the resumable recovery path could resolve from the current package or indexed external packages
- unresolved geometry rows are not proof that the linked family is absent
- the dominant remaining ceiling is now the parser boundary, not same-package reachability

## Current counted boundary

From `tmp/caspart_geom_shader_census_fullscan.json`:

- `RowsWithAnyGeometryCandidate = 281303`
- `RowsWithResolvedGeometryShader = 281271`
- `RowsWithUnknownGeometryShader = 32`
- `GeometryCandidatesTotal = 3628904`
- `UniqueGeometryCandidates = 87466`
- `GeometryResourcesResolved = 369202`
- `GeometryResolvedFromCurrentPackage = 356291`
- `GeometryResolvedFromExternalPackage = 12911`
- `UniqueResolvedGeometryResources = 87348`
- `UniqueGeometryResolvedFromExternalPackage = 2854`
- `GeometryResourcesMissing = 531`
- `UniqueUnresolvedGeometryCandidates = 118`
- `GeometryDecodeFailures = 0`

Safe reading:

- the resolved family floor is real and broad
- inside the parsed subset, geometry resolution is now close to closed
- the structured-parser gap is much larger than the residual geometry-resolution tail

## Resolution boundary

Current resolution rule in the recovery path:

- resolve linked `GEOM` resources from the current source package
- chase indexed external package ownership when the linked key lives elsewhere

Current geometry-missing reasons:

- `GeometryKeyNotIndexed = 531`

Safe reading:

- the current unresolved geometry tail is now mainly an index-coverage boundary, not a decode boundary
- `GeometryDecodeFailures = 0` means the reachable `GEOM` subset decodes cleanly
- it does not mean the missing `GEOM` rows are semantically empty

## What this boundary means for family counts

Current direct family floor remains useful:

- `SimSkin = 280983` across `401` packages by `CASPart` rows
- `SimGlass = 6048` across `189` packages by `CASPart` rows
- `Phong = 33` across `5` packages by `CASPart` rows

But safe reading still has to stay explicit:

- these are `currently parsable CASPart rows with broadly resolved linked GEOM families`
- they are not yet final whole-`CAS` family prevalence

## What this boundary does not mean

It does not mean:

- unresolved linked `GEOM` resources are broken assets
- the residual tail belongs to no known family
- the current resolved ratio is the final family ratio for the whole corpus

## Practical consequence

The next deeper character-side census step is now split cleanly:

- if the goal is integrity, choose between parser expansion and auditing the residual `GeometryKeyNotIndexed` tail
- if the goal is priority, do not wait on more geometry-ownership chase before reprioritizing the docs and packets

Until deeper integrity work happens:

- keep the current family counts
- keep them labeled as `currently parsable character-side family` counts
- do not promote them to final whole-character prevalence
