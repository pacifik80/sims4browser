# Sim Body-Shell Contract

Related docs:

- [Knowledge map](knowledge-map.md)
- [Shared TS4 Material, Texture, And UV Pipeline](shared-ts4-material-texture-pipeline.md)
- [CAS/Sim Material Authority Matrix](workflows/material-pipeline/cas-sim-material-authority-matrix.md)
- [Sim domain roadmap](sim-domain-roadmap.md)

This document freezes the current project rule for how `Sim Archetype` builds its initial body shell.

If the concept changes later, update this file together with:

- `docs/planning/current-plan.md`
- `AGENT.md`
- tests and durable probe tooling that enforce or audit the contract

## Goal

The first `Sim Archetype` preview must show an honest base body assembled from authoritative body-recipe inputs.

It must not silently drift into a clothing preview, compatibility cosplay, or broad search that happens to look plausible.

## Current Contract

### 1. Template Selection

For one top-level `Sim Archetype`, select one `SimInfo` template for base-body inspection and preview.

The selection order is:

1. a template with an explicit body-driving recipe
2. otherwise, a template with authoritative body-part facts
3. otherwise, a template with indexed `species/age/gender` metadata that can drive the indexed default/naked recipe
4. only then plain metadata-only templates

Within the same recipe-confidence tier, prefer the leaner body template over the richer styled preset:

- fewer outfit categories
- fewer outfit entries
- fewer outfit parts

This is deliberate. When a template does not expose an explicit body-driving outfit, the body-shell path should minimize styling noise instead of maximizing wardrobe richness.

Package preference is a separate concern and should only choose between package variants of the same template identity, not between different logical templates.

### 2. Allowed Body-Recipe Sources

The current base-body recipe may come from only these sources, in this order:

1. explicit `SimInfo -> body-driving outfit -> concrete part state -> exact CASPart`
2. indexed default/naked base-body CAS parts matched by the same `species/age/gender`

Today, the explicit body-driving outfit path is still defined narrowly as the parsed `Nude` outfit path when it exists.

The indexed default/naked path is the only allowed cross-template fallback for first-open `Sim Archetype` preview. It exists to keep archetypes with no explicit `Nude` outfit from collapsing into metadata-only, while still staying in a body-only slice.

### 3. Allowed Body Layers

`Sim Archetype` body-shell preview may include only body-recipe layers:

- `Full Body`
- `Body`
- `Head`
- `Top`
- `Bottom`
- `Shoes`

These layers are allowed only when they are part of the chosen body recipe.

`Top` / `Bottom` / `Shoes` in this context mean nude or base-body recipe layers, not arbitrary styled CAS apparel.

### 4. Forbidden Inputs For Sim Archetype Body Shell

Do not use these as part of the initial `Sim Archetype` body shell:

- ordinary clothing search or styling outfits
- accessories, jewelry, makeup, or similar appearance overlays
- human-only compatibility fallback search across broad CAS pools
- cross-species or cross-age "good enough" shell substitution
- package-wide candidate probing that is not tied to the chosen recipe

Those belong to later CAS/styling flows, not the initial body-shell contract.

### 5. Diagnostics And Support Language

Diagnostics must distinguish these cases honestly:

- `body-driving SimInfo template` when preview came from an explicit body-driving outfit path
- `indexed default/naked body recipe` when preview came from the indexed fallback body recipe
- `body-shell inspection` when a template was selected but no renderable recipe was resolved

Do not describe a template as `body-driving` when the graph actually used indexed default/naked fallback or remained unresolved.

### 6. Audit Rule

The contract is considered satisfied for an archetype when:

- the selected template follows the selection rule above
- body candidates come only from the allowed body-recipe sources
- preview does not mix in clothing/accessory styling paths
- diagnostics describe the real source honestly

## Current Known Gap

This contract is still stricter than the live game format knowledge we want eventually.

We do not yet persist or resolve a richer species-specific explicit body recipe for every archetype family. Until that broader recipe exists and is proven, the project should keep using:

- explicit body-driving outfit path when present
- indexed default/naked body recipe otherwise

and should not widen back into broad compatibility search.
