# Helper-Family Carrier Plausibility Matrix

This document turns the current helper-family carrier boundary into one compact matrix.

Use it when the question is not:

- "is helper-family ownership closed already?"

but:

- "which package-side carriers are currently plausible for each narrowed helper-family row?"
- "which ownership claims are still premature?"
- "what exact new evidence would promote the next claim?"

Related docs:

- [Package, Runtime, And Scene Bridge Boundary](package-runtime-scene-bridge-boundary.md)
- [Helper-Family Package Carrier Boundary](helper-family-package-carrier-boundary.md)
- [Runtime Helper-Family Clustering Floor](runtime-helper-family-clustering-floor.md)
- [Shader Family Registry](shader-family-registry.md)
- [ShaderDayNightParameters](family-sheets/shader-daynight-parameters.md)
- [Projection, Reveal, And Lightmap Families](family-sheets/projection-reveal-lightmap.md)
- [GenerateSpotLightmap And NextFloorLightMapXform](family-sheets/generate-spotlightmap-nextfloorlightmapxform.md)

## Scope status (`v0.1`)

```text
Helper-Family Carrier Plausibility Matrix
├─ Narrowed helper-row comparison floor ~ 83%
├─ Package-carrier plausibility wording ~ 71%
├─ Premature-claim rejection floor ~ 79%
├─ Promotion-trigger wording ~ 58%
└─ Final authored ownership closure ~ 19%
```

## Why this exists

The current helper-family packet stack already says many true things.

It still leaves one practical problem during offline work:

- the same carrier-order boundary has to be mentally reapplied to three different weak family rows

This matrix exists so the next offline step is not:

- "read several boundary docs and reconstruct the same answer again"

It is:

- "read one matrix and see which carrier is plausible, which claim is premature, and what evidence would promote it"

## Shared rule

Current package-side carrier order stays:

1. `MATD`
2. `MTST`
3. `Geometry` / `Model` / `ModelLOD`

Current narrowed runtime rows stay:

- `ShaderDayNightParameters -> F04`, with `F05` as nearest comparator
- generated-light -> `F03 maptex`
- projection/reveal -> `F04 srctex + tex`

Current global limit stays:

- no helper row is closed until the bridge `package carrier -> runtime cluster -> scene/pass context` is closed

## Matrix

| Helper row | Narrowed runtime candidate | Current plausible package-side reading | Current premature claim | What would promote the next claim |
| --- | --- | --- | --- | --- |
| `ShaderDayNightParameters` | `F04`; `F05` as comparator | package-side ownership should be tested against `MATD`, then `MTST`, then the model chain; current evidence is strong enough to say the ownership question is constrained, not free-form | direct `MATD` family claim; `MTST` ownership claim from visible object roots alone | tagged capture that concentrates `F04` in a lighting/reveal-heavy scene, or a stronger package-to-runtime structural match that isolates one authored carrier above the others |
| generated-light | `F03 maptex` | generated-light is plausibly carried as narrower helper provenance that must still pass through authored carriers and the model chain; current evidence is strong enough to reject default `MTST` ownership | direct `MATD` ownership for `GenerateSpotLightmap`; treating `MTST` as default owner of generated-light semantics | tagged lighting-heavy capture that concentrates the `F03 maptex` packet, or stronger structure evidence showing one authored carrier consistently aligns with the narrowed runtime packet |
| projection/reveal | `F04 srctex + tex` | projection/reveal is plausibly an authored helper branch that still needs the same `MATD -> MTST -> model-chain` test order; current evidence is strong enough to keep it out of one collapsed authored family claim | direct package-side owner from current counts alone; collapsing projection/reveal into one proved authored row | tagged projection/reveal-heavy capture that concentrates the narrowed `F04` packet, or a stronger package-side structure match that separates it from neighboring helper rows |

## Safe reading

What is already safe:

- each weak helper row now has one narrowed runtime candidate and one shared carrier-order test
- current package-side evidence is strong enough to separate:
  - plausible carrier order
  - premature ownership claim
- the next offline packet can now compare helper rows without widening back into generic prose

What is not yet safe:

- selecting one final authored carrier for any of the three rows
- treating narrowed runtime candidates as family closure
- skipping scene/pass context just because package-side order is now cleaner

## Best current use

Use this matrix in two cases:

1. when the game is not being run
   - keep offline work at the plausibility layer
   - reject premature ownership claims row by row
2. when tagged captures finally exist
   - use the matrix as the starting expectation sheet
   - check which row actually gets promoted and which stays open

## Honest limit

This matrix does not solve helper-family ownership.

It exists to make the current offline bridge sharper:

- plausibility is now explicit
- premature claims are now explicit
- promotion triggers are now explicit
