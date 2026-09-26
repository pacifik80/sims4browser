---
id: TASK-011
title: Build mode — structure tools (walls, rooms, floors, doors, windows)
status: open
area: game-ui
priority: high
created: 2026-07-21
---
**Goal** — the BUILD (structure) half of build mode, the **immediate next step after the Buy catalogue**
(TASK-004) — the user asked explicitly not to forget it. Draw/paint the building itself: walls & rooms
(drag), floor + wall coverings (paint per-cell), doors & windows (place onto walls), then stairs /
fences / roofs / terrain.

**Acceptance** — in build mode a **Build** tab (beside Buy) exposes structure tools; the player can draw
walls, form rooms, paint floors/coverings, and hang doors/windows on walls, all reflected in the sim
(collision, enclosure) and routing (nav grid re-marks).

**Notes** — reuse the **home-editor** engine (walls/rooms/ceilings + the three-representation
Render/Light/Sim model already exist there — see `docs/home-editor-engine.md`). Doors/windows are the
Buy↔Build hybrid (objects snapped to walls). Coverings export exists (`exportcoverings`); real
door/window/wall assets come from the same object pipeline as TASK-004's furniture.
