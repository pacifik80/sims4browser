# Technical Debt

This file tracks debt we already understand and expect to address.

## Architecture And Code Shape

- `src/Sims4ResourceExplorer.Assets/AssetServices.cs` is carrying too many roles: discovery helpers, selection policy, graph assembly, diagnostics, and fallback logic.
- `src/Sims4ResourceExplorer.App/ViewModels/MainViewModel.cs` and related inspector view models are growing into a large orchestration surface that should eventually be split into clearer feature slices.
- `tools/ProbeAsset/Program.cs` is a real tool, but it has accumulated many commands in one large file and needs its own internal structure or documentation.

## Project Organization

- Probe workflows still depend heavily on `tmp/` scratch outputs and one-off local files instead of documented repeatable scripts.
- Some project instructions historically lived under `src/Sims4ResourceExplorer.App/`; they now need to remain thin pointers rather than parallel sources of truth.
- The root `README.md` is valuable but dense. Over time it should stay product-facing and push operational details into `docs/`.

## Domain / Product Debt

- The current `Sim` body preview still contains proxy scaffolding that should disappear once authoritative assembly is real.
- Naming in some diagnostics and inspector sections still reflects transitional implementation states rather than the final domain model.
- Several supported-subset statements across docs will need tightening as the `Sim` and CAS pipelines become more authoritative.

## Browser Preview Deferred Debt

- Preview-side canonical UV/state truth is still not fully source-proven. The viewport now consumes one resolved UV contract much more directly, but the contract still depends on upstream choices such as `MTST` default-state selection, UV decode-mode heuristics, and potentially unused mesh `ScaleOffsetReference` data.
- Exact overlay order for `CAS` late/detail families is still partly approximation-based. This affects which makeup, skin-detail, tattoo, or clothing overlay appears above another one in difficult combinations. We intentionally deferred the final pass until there are side-by-side build examples to compare against.
- Exact overlay blend behavior is still partly approximation-based. This affects whether a late/detail layer looks too strong, too flat, too transparent, or too emissive even when it is drawn in roughly the right place.
- Stage-specific transparency behavior for late/high-layer paths is still partly approximation-based. This affects when a layer should require a dedicated opacity input and when alpha may safely come from the color texture.
- Rare `CompositionMethod 32` plus high `SortLayer` edge cases are still treated as functional debt. The dominant paths are handled, but unusual worn/high-layer combinations may still render slightly too early, too late, or with the wrong emphasis.
- Helper/projective/reveal/lightmap family policy in normal preview is intentionally deferred. We currently favor safe inspection behavior over showing those families as ordinary visible surface layers.
- Selected-slot inspection edge cases are intentionally deferred until a real reproducible bad example appears in a build. The current path is much safer than before, but we are not treating it as fully source-proven yet.

### Debt Closure Inputs We Expect

- Visual comparison examples from the user after build review:
  - what asset or outfit is loaded
  - what currently looks wrong
  - what should happen instead
  - ideally a browser screenshot and an in-game screenshot of the same case
- For UV/state-truth bugs:
  - one asset where both `3D` and `MaterialUv` are visibly wrong in the same way
  - the diagnostics block for that asset
  - confirmation whether the wrong result survives after viewport-side cleanup, which tells us to move upstream into decode/state truth rather than preview consumption
- Clear policy choice for helper/projective families:
  - inspection-only
  - partially visible in normal preview
  - leave as debt for now

## Debt Handling Rule

When a debt item starts blocking current work, either:

- pay it down in the same change set, or
- record the constraint explicitly in `docs/planning/current-plan.md` and keep moving with eyes open.
