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

## Debt Handling Rule

When a debt item starts blocking current work, either:

- pay it down in the same change set, or
- record the constraint explicitly in `docs/planning/current-plan.md` and keep moving with eyes open.
