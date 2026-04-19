# References

This folder holds durable research material that should be easy to find during future sessions.

Start with [Knowledge map](../knowledge-map.md) if the task is broad and you first need to know which layer of repo knowledge to trust or open.

## Layers

- `codex-wiki/`
  - curated internal guidance for package parsing, pipelines, validation, and source trust
- `external/`
  - local snapshots of third-party reference material used during reverse engineering

## Trust Order

When designing or validating a new path, prefer sources in this order:

1. project code and tests
2. `docs/` product and planning docs
3. `docs/references/codex-wiki/`
4. `docs/references/external/` local snapshots
5. live official or modding sources on the web

## Current External Snapshots

- `external/Binary-Templates/`
- `external/TS4SimRipper/`
- [external/README.md](external/README.md)

These external snapshots are local working material. They may be ignored by Git and might not exist in a clean clone unless restored locally.

## Placement Rule

If a research artifact is still useful beyond one session, move it here or distill it into a normal doc under `docs/`. Do not leave durable reference material in root, `.tmp_*`, or `tmp/`.

For the shared Sims 4 material/texture/shader/UV contract, the normative repo guide now lives in [../shared-ts4-material-texture-pipeline.md](../shared-ts4-material-texture-pipeline.md). For family-specific `CAS/Sim` authority detail, use [../workflows/material-pipeline/cas-sim-material-authority-matrix.md](../workflows/material-pipeline/cas-sim-material-authority-matrix.md). Use `references/` as supporting evidence and detailed backup material, not as the first entry point.
