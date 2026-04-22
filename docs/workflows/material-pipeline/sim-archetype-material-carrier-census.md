# Sim Archetype Material Carrier Census

This document records the first direct character-side carrier census built from the live index and `SimGraph`, not from prose and not from one narrow pack route.

It is intentionally narrower than a full `CAS/Sim` family census.

Related docs:

- [Corpus-Wide Family Census Baseline](corpus-wide-family-census-baseline.md)
- [Corpus-Wide Family Priority](corpus-wide-family-priority.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [Shader Family Registry](shader-family-registry.md)
- [Research Restart Guide](research-restart-guide.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Primary rule

- this is a direct `Sim archetype` carrier census
- it is not a whole `CAS/Sim` family census
- it counts graph-backed character-side carriers that really surfaced through the current `SimGraph` pipeline:
  - skintone render carriers
  - base-texture carriers
  - overlay counts
  - body assembly modes
  - active body layers
  - body candidate source kinds
  - body/source/slot/morph coverage

Safe reading:

- this layer is stronger than old character-side hints because it comes from live index assets and built preview graphs
- this layer is weaker than a full whole-game family census because it covers `Sim archetype` assets only, not every `CAS`/`Sim` asset carrier in the corpus

## Source and command

Source:

- live index under `%LOCALAPPDATA%\Sims4ResourceExplorer\Cache`
- graph build path from `ProbeAsset`

Command:

- `dotnet tools/ProbeAsset/bin/Release/net8.0/win-x64/ProbeAsset.dll --census-sim-material-carriers tmp/sim_material_carrier_census.json`

Output:

- `tmp/sim_material_carrier_census.json`

## Direct counted baseline

Current counted baseline:

- `TotalAssets = 38`
- `SupportedAssets = 38`
- `UnsupportedAssets = 0`
- `AssetsWithSkintoneRender = 15`
- `UniqueSkintoneResources = 11`
- `AssetsWithBaseTexture = 10`
- `UniqueBaseTextureResources = 8`
- `AssetsWithOverlayTextures = 10`
- `AssetsWithViewportTint = 10`

Current asset-package distribution:

- `EP = 20`
- `BaseGame-Data = 15`
- `Delta = 3`

Current asset package classes:

- `ClientFullBuild = 20`
- `ClientDeltaBuild = 18`

## Direct counted body-shell and carrier structure

Contract status:

- `IndexedDefaultBodyRecipe = 29`
- `ExplicitBodyDriving = 6`
- `Unresolved = 3`

Assembly modes:

- `FullBodyShell = 23`
- `SplitBodyLayers = 12`
- `None = 3`

Active layer coverage:

- `Full Body = 23`
- `Top = 12`
- `Bottom = 12`
- `Shoes = 12`

Body candidate source kinds:

- `IndexedDefaultBodyRecipe = 65`
- `ExactPartLink = 23`

Current `CAS` slot candidate source kinds:

- no `SimCasSlotCandidateSourceKind` rows currently surfaced in this graph-backed census

## Direct counted skintone and texture carriers

Overlay distribution:

- `0 overlays = 28`
- `1 overlay = 6`
- `2 overlays = 4`

Swatch-color distribution:

- `0 swatch colors = 28`
- `1 swatch color = 10`

Skintone package top buckets:

- `BaseGame-Data = 14`
- `Delta = 1`

Base-texture package top buckets:

- `BaseGame-Data = 10`

Safe reading:

- graph-backed skintone/base-texture carry-through is real for a meaningful subset of archetypes
- it is not universal even inside the current archetype set
- the current strongest skintone/base-texture floor is still base-game-heavy

## Direct counted body/source/slot/morph coverage

Top body-foundation coverage:

- `Face / head morph stack = total 1975 across 33 assets`
- `Current body-part references = total 902 across 36 assets`
- `Body morph stack = total 743 across 36 assets`
- `Body layers = total 225 across 33 assets`
- `Base frame = total 38 across 38 assets`
- `Skin pipeline = total 21 across 21 assets`

Top body-source coverage:

- `Direct face channels = total 1345 across 33 assets`
- `Body-part instances = total 901 across 35 assets`
- `Genetic face channels = total 630 across 17 assets`
- `Genetic body channels = total 330 across 17 assets`
- `Direct body channels = total 253 across 26 assets`
- `Genetic body-type tokens = total 154 across 32 assets`
- `Pelt layer references = total 71 across 23 assets`
- `Skintone reference = total 15 across 15 assets`

Top slot-group coverage:

- `Outfit / body part selections = total 902 across 36 assets`
- `Genetic body part layer = total 154 across 32 assets`
- `Pelt / fur layers = total 71 across 23 assets`
- `Skintone = total 15 across 15 assets`

Top morph-group coverage:

- `Face modifiers = total 1345 across 33 assets`
- `Genetic face modifiers = total 630 across 17 assets`
- `Genetic body modifiers = total 330 across 17 assets`
- `Body modifiers = total 253 across 26 assets`
- `Sculpts = total 114 across 31 assets`
- `Genetic sculpts = total 46 across 14 assets`

## What this layer proves

- there is now a direct character-side carrier census that is stronger than prose hints
- `Sim` archetype graphs do carry real skintone/base-texture/overlay structure
- body-shell assembly and skin-pipeline routing are broad enough to remain Tier A implementation concerns
- character-side prevalence can no longer be described only through object-side counts or through external-family wording

## Honest limit

This layer does not yet prove:

- whole `CAS/Sim` family frequency for `SimSkin`, `SimGlass`, or `SimSkinMask`
- whole `CAS` prevalence outside the graph-backed `Sim archetype` subset
- per-family shader/material slot prevalence
- full cross-domain ranking against object-side `MATD` profile prevalence

That means:

- this is the first direct character-side carrier layer
- it is not the last census layer needed before final queue ranking

## Practical consequence for priority

Safe consequence:

- Tier A character-side work is now grounded in two direct statistics layers instead of one:
  - whole-install corpus size and carrier strata
  - graph-backed `Sim archetype` carrier prevalence

Next strong counting step:

- add a broader direct `CAS`/`GEOM`/material-carrier count layer so character-side priority is not limited to the `Sim archetype` subset
