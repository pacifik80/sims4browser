---
id: TASK-013
title: Export real catalogue prices + friendly swatch names
status: open
area: home-editor
priority: low
created: 2026-07-21
---
**Goal** — the Buy catalogue currently shows **hand-authored placeholder prices**
(`GameCatalogBaker.PriceOf`) and swatch tooltips are raw state hashes ("State 0x5961C8A5") because
neither is in the export: `home_catalog.json` has no price field and `swatches.json` labels are hashes.

**Fix** — extend `tools/Sims4UnityExport` to pull each item's real catalogue price (the game's catalog
tuning carries SimoleonPrice) and, where possible, the swatch colour/variant names from the CASP/OBJD
colour tags; write them into `home_catalog.json` / `swatches.json`; then read them in
`GameCatalogBaker` instead of the hard-coded maps.

**Acceptance** — §prices in the Buy grid match the real game; swatch tooltips read like colours
("Charcoal", "Oak"), not hashes.
