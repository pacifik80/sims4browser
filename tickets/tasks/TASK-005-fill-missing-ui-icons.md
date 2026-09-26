---
id: TASK-005
title: Source + wire the missing game UI icons
status: open
area: game-ui
priority: medium
created: 2026-07-21
---
**Goal** — fill the gaps in the icon set and wire them into the UI. The starter pack ("Sim Game Icon
Design System", 29 monochrome `currentColor` SVGs in `Assets/Game/UI/Icons/svg/`) covers speeds, modes,
debug, settings, 4/6 needs and 4/6 emotes. The user is preparing the rest; show placeholders until then.

**Acceptance** — these exist as vector icons and display in-UI, with the placeholder shown for any not
yet provided:
- **Needs:** `hygiene`, `social` (the two need-meters currently uncovered)
- **Emotes:** `angry`, `surprised` (to complete the 6 facial emotions)
- **CAS categories:** `hair`, `clothing`, `skin`, `body`, `makeup` (needed for the CAS panel, TASK-006)
- **Control:** a `release` / deselect icon for un-possessing (reuse a `control-*` meanwhile)
- **Furniture types:** fridge/bed/couch/tv/shower (superseded if TASK-004 uses rendered thumbnails)

**Notes** — placeholder fallback is handled by `IconLibrary.Get(name)` (returns the placeholder when a
name is absent). Icons import via the Vector Graphics package as UI Toolkit vector images; run the
"Rebuild Icon Library" menu after adding new SVGs. Speed icons are `x2/x3/x5` — game multipliers were
set to match.
