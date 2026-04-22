# Helper-Family Package Carrier Boundary

This document isolates what the current package-side material layer can and cannot already prove about the weak helper-family rows.

Use it when the question is not:

- "does this helper family exist at all?"
- "what runtime cluster candidate is strongest right now?"

but:

- "how far can `MATD`, `MTST`, `Geometry`, `Model`, and `ModelLOD` already carry ownership claims for helper-family rows before tagged runtime captures exist?"

Related docs:

- [Package, Runtime, And Scene Bridge Boundary](package-runtime-scene-bridge-boundary.md)
- [Runtime Shader Interface Baseline](runtime-shader-interface-baseline.md)
- [Runtime Helper-Family Clustering Floor](runtime-helper-family-clustering-floor.md)
- [Build/Buy Material Authority Matrix](buildbuy-material-authority-matrix.md)
- [Build/Buy Stateful Material-Set Seam](buildbuy-stateful-material-set-seam.md)
- [MATD Shader Census Baseline](matd-shader-census-baseline.md)
- [ShaderDayNightParameters](family-sheets/shader-daynight-parameters.md)
- [Projection, Reveal, And Lightmap Families](family-sheets/projection-reveal-lightmap.md)
- [GenerateSpotLightmap And NextFloorLightMapXform](family-sheets/generate-spotlightmap-nextfloorlightmapxform.md)
- [TS4 Material Shader Spec](../../../satellites/ts4-dx11-introspection/docs/ts4-material-shader-spec.md)

## Scope status (`v0.1`)

```text
Helper-Family Package Carrier Boundary
├─ Package-side carrier priority reading ~ 84%
├─ MATD-versus-MTST helper ownership boundary ~ 62%
├─ Geometry/Model/MLOD bridge reading ~ 58%
├─ Helper-family negative ownership limits ~ 73%
└─ Final helper-family carrier closure ~ 18%
```

## Why this exists

The updated shader spec made the package-side layer more concrete.

That still leaves one easy failure mode:

- overreading package carriers as if they already proved helper-family ownership

This document exists to keep that boundary explicit.

## Current strongest package-side reading

The current checked-in package-side priority is now strong enough to keep in one compact rule:

1. `MATD` is the first authored package-side material carrier
2. `MTST` is the next state or variant layer on top of material definitions
3. `Geometry`, `Model`, and `ModelLOD` stay in the bridge path because runtime vertex and material expectations must line up there

Current counted carrier surface from checked-in sources:

- `MaterialDefinition = 28225`
- `MaterialSet = 514`
- `Geometry = 187832`
- `ModelLOD = 105743`
- `Model = 52122`

Safe reading:

- package-side carrier priority is materially clearer than before
- helper-family rows should now be discussed against this carrier order instead of against one vague "material blob" layer

## What `MATD` does and does not already tell us

The current `MATD` census is strong enough for one narrow point:

- it proves what object-side authored material profiles are actually common in package data

It is not strong enough for these helper-family claims:

- `ShaderDayNightParameters` is a top-level `MATD` family
- generated-light is a top-level `MATD` family
- projection/reveal ownership is visible directly from dominant `MATD` profile names

Current top `MATD` profiles are:

- `FadeWithIce = 27434`
- `g_ssao_ps_apply_params = 480`
- `ObjOutlineColorStateTexture = 157`
- `texgen = 128`
- `ReflectionStrength = 2`

Safe reading:

- package-side `MATD` prevalence is very real
- helper-family rows are not currently promoted just because they have helper-like names elsewhere
- top `MATD` profiles do not yet give a direct helper-family ownership closure

## What `MTST` does and does not already tell us

The current `MTST` seam is also much better bounded than before:

- `MTST` is a real object-side authority seam
- `MaterialVariant` can select into that seam
- `MTST` clearly matters for stateful or swatch-heavy object families

That is still not enough to say:

- helper-family rows are owned by `MTST` by default
- any helper-looking parameter automatically belongs to the `MTST` layer rather than to a narrower `MATD` or geometry-side bridge

Safe reading:

- `MTST` is a state or variant carrier
- it is not yet the automatic answer for weak helper-family ownership

## What `Geometry`, `Model`, and `ModelLOD` add

These carriers matter for a different reason:

- runtime shader interfaces expect concrete geometry channels
- `Geometry`, `Model`, and `ModelLOD` are where those expectations have to stay compatible with authored content

That makes them important for the bridge.

It does not yet make them enough to say:

- this helper-family runtime cluster definitely belongs to this package-side model branch

Safe reading:

- `Geometry` / `Model` / `ModelLOD` are critical bridge carriers
- they are not yet a substitute for scene/pass context

## Current safe ownership boundary by helper row

### `ShaderDayNightParameters`

What is already safe:

- family identity is externally anchored
- runtime narrowing already points to `F04` first and `F05` second
- package-side carrier priority now says any future ownership claim should be tested against `MATD`, `MTST`, and the model chain in that order

What is not yet safe:

- claiming `ShaderDayNightParameters` is a direct top-level `MATD` family from current package-side counts
- claiming it is an `MTST`-owned family just because it survives in object-side visible roots

### Generated-light

What is already safe:

- generated-light identity is externally anchored
- runtime narrowing already points to `F03 maptex`
- package-side carrier order now gives a clean place to ask whether generated-light survives as authored material state or only as narrower helper provenance

What is not yet safe:

- promoting `GenerateSpotLightmap` or `NextFloorLightMapXform` into a direct `MATD` ownership claim
- treating `MTST` as the default owner of generated-light helper semantics

### Projection/reveal

What is already safe:

- umbrella identity is externally anchored
- runtime narrowing already points to `F04 srctex + tex`
- package-side bridge wording can now say that any future ownership closure must pass through the same `MATD` / `MTST` / model-chain order

What is not yet safe:

- claiming a direct package-side owner from current counts alone
- collapsing projection/reveal into one authored material-family row without scene/pass proof

## Strongest negative ownership rule

The strongest current safe negative rule is:

- do not promote a weak helper family to package-side ownership just because:
  - the family exists externally
  - the runtime cluster exists
  - the object-side material chain exists

All three can be true while the actual ownership join is still missing.

## Best current offline next step

If the game is not being run, the best honest next step is:

1. keep the narrowed runtime helper candidates fixed:
   - `F04` for `ShaderDayNight`
   - `F03 maptex` for generated-light
   - `F04 srctex + tex` for projection/reveal
2. compare those candidates against package-side carrier expectations only at the boundary level:
   - which carriers are plausible
   - which ownership claims are still premature
3. keep the result as ownership-boundary wording, not as final closure

## Honest limit

This document does not prove helper-family ownership.

It exists to keep one thing honest:

- package-side evidence is now good enough to constrain the ownership question
- it is still not good enough to close it without the missing runtime-context join
