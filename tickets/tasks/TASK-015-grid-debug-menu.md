---
id: TASK-015
title: Debug menu — grid visual debugging (occupancy, sim cells, paths, nav blocking)
status: in-progress
area: game
priority: high
created: 2026-07-21
---
**Goal** — user request: a debug menu to visually verify the D-106 grid engine: "see the actual in-game
grid; see occupancy of the grid by furniture; for human characters — how they occupy the grid: targeted
cell, current cell, cells in-transit".

**v2 2026-07-22 — proper UI Toolkit menu** (user: "make it finally proper menu, not side panel… same UI
we are using as main") — `GameDebugMenu`: glass-styled UITK panel in its own UIDocument (sortingOrder
30, works in all modes), GRID section + **RENDER section** (TASK-016 tunables: SSGI/shadow dimmer/
ambient/EV offset/fill light/tonemap/shadow lift + "Log tuning values" bake handoff), live per-Sim list,
runtime bootstrap when the scene predates it. `GridDebugOverlay` = world visuals only (IMGUI panel
removed).

**v1 built 2026-07-21** — `GridDebugOverlay`, opened via the **DBG button** in
the HUD top-right cluster (user asked for a GUI button, not only a hotkey; F3 remains as the shortcut
and the only access while a modal mode hides the HUD): 1 m grid toggle, furniture
occupancy straight from the LotGrid occupancy map (+ tile count), per-Sim current (cyan) / goal (green)
/ transit (violet) tiles, routed-path lines, 0.5 m nav-blocked sub-cells, per-Sim status list. Pooled
quads/lines, scene-baked `Debug*.mat` (HDRP transparency is editor-bake-only). Doc:
`docs/game/systems/debug-tools.md`. Answer surfaced by the tool: Sims do NOT reserve current/transit
tiles for each other (soft separation only) — that decision is TASK-014 phase 2.

**Acceptance** — press F3 in Play: every option renders live and matches behaviour (occupancy = exactly
the tiles placement refuses; goal tile = where the Sim stops; paths on the sub-grid).

**Backlog** — needs/decision-score overlays, reservation states, cheats (fill need, teleport), clock
diagnostics. Grows with the engine.
