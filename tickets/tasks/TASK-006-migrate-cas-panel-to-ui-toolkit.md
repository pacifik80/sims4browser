---
id: TASK-006
title: Migrate the in-game CAS panel from IMGUI to UI Toolkit
status: open
area: cas
priority: high
created: 2026-07-21
---
**Goal** — replace the giant IMGUI `CasController` panel used by in-game CAS (`GameCasMode`) with a UI
Toolkit panel, so CAS matches the rest of the game UI (D-105) and its long lists get virtualized. This
is the **last IMGUI holdout** in the game UI.

**Acceptance** — in-game "Edit Look" opens a UI Toolkit CAS panel (no IMGUI); every current knob is
reachable — base skin / tone / detail / normal, eye colour, hair (style + colour), clothing per slot,
body + face morph sliders, emotion + viseme; hair/clothing/swatch lists are `CatalogList`s with
thumbnails and virtualization.

**Notes** — big task; do after the catalog widget + icons + swatch thumbnails are proven. Reuse
`CatalogList` for the swatch lists. Needs CAS category icons (TASK-005). The standalone CAS *scene*
(`Sims4CasSceneBuilder` + IMGUI `CasController`) can stay as-is; this is only the in-game overlay.
