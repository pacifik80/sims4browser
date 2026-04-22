# MATD Shader Census Baseline

This document records the first direct shader-profile count layer built from the fresh whole-install scan.

It is stronger than old family hints because it is counted from package resources, not inferred from prose.

It is still narrower than a full whole-game shader-family census because it currently covers object-side `MaterialDefinition` rows only.

Related docs:

- [Corpus-Wide Family Census Baseline](corpus-wide-family-census-baseline.md)
- [Corpus-Wide Family Priority](corpus-wide-family-priority.md)
- [Shader Family Registry](shader-family-registry.md)
- [Research Restart Guide](research-restart-guide.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope

Current scope:

- direct count of `MaterialDefinition` (`01D0E75D`) resources from the fresh full-install shard set
- direct extraction of embedded `MATD` shader hashes from decoded package payloads
- mapping of shader hashes through `tmp/precomp_shader_profiles.json`

Current non-scope:

- `CAS/Sim` shell-family prevalence
- `GEOM` / `MTNF`-side family counting
- exact runtime shader-family semantics
- whole-game family ranking for rows such as `SimSkin`, `SimGlass`, `ShaderDayNightParameters`, `RefractionMap`

Safe reading:

- this is a direct package-derived object-side shader-profile census
- it is not yet a whole-game family census
- it is valid for ranking object-side `MATD` shader profiles by actual prevalence

## Counted source

Command:

- `dotnet tools/ProbeAsset/bin/Release/net8.0/win-x64/ProbeAsset.dll --census-matd-shaders "C:\Users\stani\PROJECTS\Sims4Browser\tmp\profile-index-cache" "C:\Users\stani\PROJECTS\Sims4Browser\tmp\matd_shader_census_fullscan.json"`

Inputs:

- fresh full-install shard set under `tmp/profile-index-cache/cache/`
- shader-profile map `tmp/precomp_shader_profiles.json`

Output:

- `tmp/matd_shader_census_fullscan.json`

## Current counted baseline

Current totals:

- `MaterialDefinitionResources = 28225`
- `MaterialDefinitionPackages = 75`
- `DecodedResources = 28201`
- `EmptyResources = 24`
- `Failures = 0`

Meaning:

- `28201` rows currently decode to a usable embedded `MATD` shader hash
- `24` rows are zero-length decoded payloads, currently observed in `ClientDeltaBuild0.package`
- there are currently no remaining parser failures in this command path

## Current top profiles

Direct counted top profiles:

- `FadeWithIce = 27434` across `73` packages
- `g_ssao_ps_apply_params = 480` across `20` packages
- `ObjOutlineColorStateTexture = 157` across `5` packages
- `texgen = 128` across `2` packages
- `ReflectionStrength = 2` across `1` package

Safe reading:

- object-side `MATD` prevalence is currently extremely skewed toward `FadeWithIce`
- the remaining directly counted profiles are real, but much narrower
- this is an actual count from package data, not a derived hint

## Current slice distribution

Top-level distribution of `MaterialDefinition` rows:

- `BaseGame-Data = 15421`
- `EP = 6786`
- `GP = 2413`
- `Delta = 1841`
- `SP = 1764`

Package-class distribution:

- `ClientFullBuild = 17909`
- `ClientDeltaBuild = 10316`

Top-profile distribution highlights:

- `FadeWithIce`
  - `BaseGame-Data = 15124`
  - `EP = 6520`
  - `GP = 2317`
  - `SP = 1737`
  - `Delta = 1736`
- `g_ssao_ps_apply_params`
  - `EP = 227`
  - `BaseGame-Data = 199`
  - `SP = 27`
  - `GP = 22`
  - `Delta = 5`

Safe reading:

- object-side `MATD` prevalence is not concentrated in one expansion pack
- the dominant profile spans the base game and multiple expansion/game/stuff slices
- this is exactly the kind of corpus-wide evidence that is stronger than one convenient fixture lane

## Derived family layer

Current `TopFamilies` in `tmp/matd_shader_census_fullscan.json` are a bounded normalization layer:

- family name is currently derived from the profile name prefix before `-`, `_`, or space

That means:

- `FadeWithIce -> FadeWithIce`
- `ObjOutlineColorStateTexture -> ObjOutlineColorStateTexture`
- `g_ssao_ps_apply_params -> g`

Safe reading:

- these derived family names are useful as a grouping helper
- they are not yet semantic family truth
- for some profiles, such as `g_ssao_ps_apply_params`, the normalized family label is obviously too weak to use as an implementation-facing family name

## Integrity boundary

One important correction from the first failed attempt:

- the package payloads do not expose bare `MATD` at byte offset `0`
- the decoded resource can carry a small wrapper/header before the embedded `MATD`
- the census command now searches for embedded `MATD` inside the decoded payload instead of assuming offset `0`

Residual boundary:

- the remaining `24` non-decoded rows are currently zero-length payloads, not parser failures
- until proven otherwise, treat them as empty delta/tombstone-like rows, not as hidden common shader profiles

## What this changes

This layer now allows one honest statement that was not safe before:

- object-side direct shader-profile popularity can now be ranked from real package counts rather than old partial hints

This layer still does not allow these statements yet:

- whole-game family prevalence for `SimSkin`, `SimGlass`, `CASHotSpotAtlas`, `RefractionMap`, `ShaderDayNightParameters`
- cross-domain family ranking between object-side and character-side families
- exact runtime material semantics from profile prevalence alone

## Next step

The next strong census step should be:

- a direct `CAS/Sim` family-count layer from `GEOM` / `MTNF` / linked character-side material carriers

Do not use this `MATD` census to overrule character-side priority by itself.
