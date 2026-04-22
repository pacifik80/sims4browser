# Material Pipeline Deep Dives

This folder holds deep-dive companions for the shared TS4 material/render pipeline.

These files are not the first repo entry point. Start with [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md), then come here when the question is narrow enough that the shared guide would be too broad.

Architectural reminder:

- these deep dives may be domain-heavy because authority order and discovery differ
- they still feed one shared shader/material contract after authoritative inputs are found
- none of these docs should be read as justification for separate `BuildBuy`, `CAS`, or `Sim` shader systems
- research priority should be set corpus-wide across the full TS4 asset space, not by one convenient pack-local fixture lane

Operational reminder:

- the preferred execution mode for this research track is an autonomous long batch, not one tiny packet per run
- use [Research Restart Guide](research-restart-guide.md) plus [Current plan](../../planning/current-plan.md) as the durable state between runs
- use any heartbeat automation only as interruption recovery, not as the normal cadence of progress
- pack-specific fixture routes are secondary validation/evidence layers only; they do not set the main queue by themselves

## Current Deep Dives

- [Documentation Status Catalog](documentation-status-catalog.md)
- [Research Restart Guide](research-restart-guide.md)
- [Corpus-Wide Family Priority](corpus-wide-family-priority.md)
- [Corpus-Wide Family Census Baseline](corpus-wide-family-census-baseline.md)
- [Sim Archetype Material Carrier Census](sim-archetype-material-carrier-census.md)
- [CAS Carrier Census Baseline](cas-carrier-census-baseline.md)
- [CASPart Linkage Census Baseline](caspart-linkage-census-baseline.md)
- [CASPart GEOM Shader Census Baseline](caspart-geom-shader-census-baseline.md)
- [CASPart Parser Boundary](caspart-parser-boundary.md)
- [CASPart GEOM Resolution Boundary](caspart-geom-resolution-boundary.md)
- [Build/Buy Material Authority Matrix](buildbuy-material-authority-matrix.md)
- [Build/Buy Transparent Object Fallback Ladder](buildbuy-transparent-object-fallback-ladder.md)
- [Build/Buy Transparent Object Authority Order](buildbuy-transparent-object-authority-order.md)
- [Build/Buy Transparent Object Classification Signals](buildbuy-transparent-object-classification-signals.md)
- [Build/Buy Stateful Material-Set Seam](buildbuy-stateful-material-set-seam.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [Body And Head Shell Authority Table](body-head-shell-authority-table.md)
- [BodyType Translation Boundary](bodytype-translation-boundary.md)
- [CompositionMethod And SortLayer Boundary](compositionmethod-sortlayer-boundary.md)
- [CompositionMethod Census Baseline](compositionmethod-census-baseline.md)
- [Overlay And Detail Family Authority Table](overlay-detail-family-authority-table.md)
- [SortLayer Census Baseline](sortlayer-census-baseline.md)
- [ShaderDayNight Evidence Ledger](shader-daynight-evidence-ledger.md)
- [Generated-Light Evidence Ledger](generated-light-evidence-ledger.md)
- [Projection, Reveal, And Generated-Light Boundary](projection-reveal-generated-light-boundary.md)
- [Refraction Evidence Ledger](refraction-evidence-ledger.md)
- [Refraction Bridge Fixture Boundary](refraction-bridge-fixture-boundary.md)
- [Shader Family Registry](shader-family-registry.md)
- [Runtime Shader Interface Baseline](runtime-shader-interface-baseline.md)
- [Runtime Helper-Family Clustering Floor](runtime-helper-family-clustering-floor.md)
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
