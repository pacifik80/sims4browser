---
id: TASK-002
title: Enclosed room gets a ShadowsOnly ceiling
status: done
area: home-editor
priority: high
created: 2026-07-13
---
**Goal** — a closed loop of walls = a room with a ceiling that shadows the interior and contains lamp
light, but is never drawn (you still see into the room from above).

**Acceptance** — inside a closed room the interior is shadowed from the sky/sun (not flooded from
above) and lamps don't leak up through a missing ceiling; nothing occludes the top-down view.

**Notes** — engine-requirements **R2** (+ **R1** room detection). Needs enclosure detection
(flood-fill / polygon over the wall graph → room area), then a per-room ceiling mesh set to
`ShadowsOnly`. Bigger than TASK-001 because of the room detection. Multi-level: the upper floor is the
ceiling of the room below.

**Implemented 2026-07-13** (compiles clean; flood-fill verified standalone; awaiting Play-test).
`HomeEditor.RefreshRooms(levelIndex)`: flood-fills the cell grid from outside the walls (axis-aligned
o=0/1 only; diagonals ignored in v1); cells the flood can't reach are enclosed. `BuildCeiling` emits a
**double-sided** mesh over the enclosed cells at `y = levelY + wallHeight`, `ShadowsOnly` (invisible)
— double-sided so it blocks the sun from above AND contains lamp light from below. Wired into
`AddWall`/`RemoveWall` (active level) and `LoadLayout` (all levels); ceiling GO is a child of the level
root (follows level visibility, torn down with it). Flood-fill test: closed 2×2 room → 4 enclosed
cells; one wall removed → 0.
**Verify (Play):** draw a closed room → its interior should be shadowed (no direct skylight flooding
in from the open top); with cutaway on, the room stays shadowed as you orbit; leave a gap in the walls
→ no ceiling (open area stays sunlit).
**Known v1 limits:** diagonal walls don't count toward enclosure; interior brightness under the ceiling
is ambient + lamps only (realistic — may want window light later); ceiling rebuilds per wall add (fine
for interactive counts, batch if it ever drags).

**Resolution** — user Play-test confirmed working 2026-07-13.
