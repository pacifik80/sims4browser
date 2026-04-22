# CASPart Parser Boundary

This document records the current structured-parser ceiling for the direct character-side `CASPart` census stack.

It exists so the current parsed subset is not silently overread as if it covered the full raw `CASPart` corpus.

Related docs:

- [CASPart Linkage Census Baseline](caspart-linkage-census-baseline.md)
- [CASPart GEOM Shader Census Baseline](caspart-geom-shader-census-baseline.md)
- [Corpus-Wide Family Census Baseline](corpus-wide-family-census-baseline.md)
- [Corpus-Wide Family Priority](corpus-wide-family-priority.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [Research Restart Guide](research-restart-guide.md)

## Primary rule

- raw `CASPart` prevalence and parsed `CASPart` prevalence are not the same thing
- the current parsed subset is strong enough for a direct floor
- it is not strong enough to erase the unresolved tail

Safe reading:

- use the parsed subset to measure the current floor
- use the raw total to measure how incomplete that floor still is
- do not treat unresolved rows as semantically absent

## Current counted boundary

From `tmp/caspart_linkage_census_fullscan.json`:

- `CasPartResources = 530507`
- `ParsedResources = 299028`
- `ZeroLengthResources = 766`
- `TotalFailures = 230713`

Current safe split:

- parsed structured subset = `299028`
- explicit zero-length subset = `766`
- unresolved structured tail = `230713`

## Failure buckets

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

## What this boundary means

- the current structured parser already covers a large, useful, install-wide subset
- the unresolved tail is still too large to ignore
- the dominant failure mode is not random noise; it is concentrated in one broad layout family:
  - `LOD asset list extends beyond payload`

Safe reading:

- current linkage and family counts are valid as `parsable-subset floors`
- they are not valid as final whole-`CAS` prevalence

## What this boundary does not mean

It does not mean:

- the failing rows are rare
- the failing rows are semantically unimportant
- unresolved character-side families are absent
- parsed-family dominance is already whole-corpus dominance

## Practical consequence

The current character-side census stack should now be read as:

1. raw whole-`CAS` carrier prevalence
2. parsed `CASPart` linkage prevalence
3. parsed `CASPart -> GEOM -> shader family` prevalence
4. explicit parser boundary over everything that still fails

That means the next deeper integrity work is not "find another family name", but:

- either widen parser coverage for the dominant failing layouts
- or keep the current parsed floor and phrase all family conclusions as `reachable-subset` results

