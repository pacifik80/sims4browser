# References

This folder holds durable research material that should be easy to find during future sessions.

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

These external snapshots are local working material. They may be ignored by Git and might not exist in a clean clone unless restored locally.

## Placement Rule

If a research artifact is still useful beyond one session, move it here or distill it into a normal doc under `docs/`. Do not leave durable reference material in root, `.tmp_*`, or `tmp/`.
