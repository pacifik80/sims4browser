---
id: TASK-012
title: Export the 15 missing Buy-catalogue thumbnails (exportthumbs)
status: open
area: home-editor
priority: medium
created: 2026-07-21
---
**Goal** — 15 of the 37 exported furniture items have no `thumb.jpg` (the game's BuyBuildThumbnail), so
the in-game Buy catalogue falls back to showing the raw diffuse texture for them: `bar_stool`,
`bath_sink`, `bathtub`, `bed_single`, `clothes_bin`, `fridge`, `kitchen_sink`, `kitchen_table_sm`,
`makeup_table`, `ottoman`, `shower`, `table_lamp`, `toilet`, `tv`, `wardrobe`.

**Fix** — run `exportthumbs <slug>=<instanceHex>` per item (tools/Sims4UnityExport/Program.cs:255-274,
CoveringExporter.TryWriteThumbAsync) — needs each item's catalog instance id from the game packages;
then re-run **Sims4 Creator/Game/Build M0 Scene** so the baker picks them up (`GameCatalogBaker` loads
`thumb.png` ?? `thumb.jpg` per folder).

**Acceptance** — every item in the in-game Buy grid shows the game's own thumbnail (no diffuse-texture
fallbacks left).
