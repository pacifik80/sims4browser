---
id: BUG-002
title: Glass door renders opaque, not transparent
status: open
area: home-editor
severity: minor
created: 2026-07-13
---
**Context** — `door_ep01gen` ("Glass Door") does not use the SkyDark glass shader family, so the
transparent-glass path (SkyDark → MTL `d 0.2` + shadow-off) never fires and the pane renders solid.

**Repro** — place the Glass Door → its pane is opaque, not see-through.

**Expected** — the pane renders transparent and passes light, like windows.

**Notes** — its glass is `colorMap7` + a green `SeasonalFoliage` element, not SkyDark. Needs
door-specific glass handling (identify the pane sub-material, force transparent). See the M8 note in
the `home-editor` memory.
