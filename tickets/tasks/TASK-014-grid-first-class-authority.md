---
id: TASK-014
title: Make the cell grid the first-class space authority (occupancy, unified tile size, nav from footprints, visible grid)
status: in-progress
area: game
priority: high
created: 2026-07-21
---
**DECIDED + BUILT 2026-07-21 (awaiting user verify)** — user decision recorded as **D-106**: MAIN grid =
**1 m tiles** (placement/occupancy/interaction authority); navigation uses finer subgrids for routes
(0.5 m sub-cells); Sims stand/interact occupying a full tile (approach slot = tile in front; socials =
adjacent tile centres); exceptions: intimate interactions later, sitting/sleeping via furniture-slot
mechanics. Implemented: `LotGrid` rework (tiles + occupancy map + nav sub-cells derived from the SAME
footprints + snap API), `GameBuildMode` validity via `TilesFree` (physics only for Sims), baker
footprints in 1 m tiles, factory approach-slot anchors, `SimAgent` tile-centre stands (`GoTo`,
`SocialStandPoint`) + faces object while using, snapped starter lot, visible 1 m grid overlay toggled by
build mode. **Remaining (this ticket):** standing Sims optionally reserving their tile in nav (phase 2 —
decide after play-feel), per-tile room ids once walls arrive (TASK-011), furniture-slot mechanics for
sit/sleep.
**Goal** — user directive: "Grid is first-class citizen, everything should respect cell-grid as it is
main game space for everything. Since this is engine part." Today the grid is real but only advisory:
placement snaps to LotGrid (0.5 m) yet validity is `Physics.OverlapBox` (no occupancy layer); nav
blocking is derived from renderer AABBs inflated by simRadius (not the snapped footprint cells); the
starter lot bypasses snapping (hand positions); the grid is invisible (bare ground plane); and the
home-editor engine (the future TASK-011 build-structure core) runs its OWN true occupancy grid at 1 m
tiles — two grids that will disagree the moment walls arrive.

**OPEN DECISION (user)** — authoritative tile size: **1 m** (Sims standard; matches walls/doors and the
home-editor engine; recommended — optionally allow ruled 0.5 m half-tile snap for small décor) vs
keeping 0.5 m everywhere (then the home editor must re-map to 2 cells per tile).

**Plan**
1. One shared grid definition (origin/tile/extents) used by game LotGrid AND the home-editor engine.
2. Occupancy layer on LotGrid: per-cell owner; place/move/rotate/delete register + free cells;
   `CellsFree()` becomes THE placement validity (physics only for Sim overlap).
3. Nav walkability derived from registered footprint cells (exactly the doc's promise in
   `docs/game/systems/locomotion-navigation.md`), not renderer bounds; keep sim-radius clearance.
4. Visible grid overlay in build mode (home-editor-style ground grid).
5. Route ALL placement through the snapped path incl. `GameSceneBuilder.BuildLot`.
6. Record the decision in `docs/game/decisions.md`; update locomotion + build-catalogue docs.

**Acceptance** — an item's snapped footprint cells, its occupancy record, and the cells nav marks
blocked are the SAME cells; placement rejects on occupancy (not collider luck); the grid is visible in
build mode; home-editor walls and game furniture share one grid without remapping.
