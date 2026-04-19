# Material Pipeline Deep Dives

This folder holds deep-dive companions for the shared TS4 material/render pipeline.

These files are not the first repo entry point. Start with [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md), then come here when the question is narrow enough that the shared guide would be too broad.

## Current Deep Dives

- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)

## Intended Split Strategy

When the shared guide or one deep-dive grows too dense, split by one bounded seam:

- authority and fallback by family
- shader-family registry details
- skintone/compositor specifics
- overlay/detail family behavior

Each new deep-dive should:

- keep a back-link to the shared guide
- link to the relevant evidence layer in `docs/references/`
- state its own scope and confidence boundary
