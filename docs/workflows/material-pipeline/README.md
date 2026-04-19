# Material Pipeline Deep Dives

This folder holds deep-dive companions for the shared TS4 material/render pipeline.

These files are not the first repo entry point. Start with [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md), then come here when the question is narrow enough that the shared guide would be too broad.

Architectural reminder:

- these deep dives may be domain-heavy because authority order and discovery differ
- they still feed one shared shader/material contract after authoritative inputs are found
- none of these docs should be read as justification for separate `BuildBuy`, `CAS`, or `Sim` shader systems

## Current Deep Dives

- [Research Restart Guide](research-restart-guide.md)
- [Build/Buy Material Authority Matrix](buildbuy-material-authority-matrix.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [Shader Family Registry](shader-family-registry.md)
- [Edge-Family Matrix](edge-family-matrix.md)
- [P1 Live-Proof Queue](p1-live-proof-queue.md)
- [Live-Proof Packets](live-proof-packets/README.md)
- [Skintone And Overlay Compositor](skintone-and-overlay-compositor.md)
- [Family Sheets](family-sheets/README.md)

## Intended Split Strategy

When the shared guide or one deep-dive grows too dense, split by one bounded seam:

- authority and fallback by family
- shader-family registry details
- narrow edge-family authority seams
- narrow family evidence sheets
- skintone/compositor specifics
- overlay/detail family behavior

Each new deep-dive should:

- keep a back-link to the shared guide
- link to the relevant evidence layer in `docs/references/`
- state its own scope and confidence boundary

If this research track is resumed in a new chat, start with [Research Restart Guide](research-restart-guide.md) before opening the narrower queue or packet docs.
