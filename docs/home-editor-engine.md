# Home Editor — Engine Requirements

*Living document.* We're modelling the build/render engine through prototypes, so this is where
the rules those prototypes reveal get written down. Add to it whenever a new case appears — the
point is to accumulate a coherent set of requirements, not to design everything up front.

Scope: how the **home model behaves** — geometry, light, and simulation. (Asset *decoding* rules —
how EA packages turn into meshes/materials — live in the code + `home-editor` memory, not here.)

---

## The core idea — one home, three representations

A home is authored once (wall runs, floors, doors, windows, furniture). But each authored element
emits geometry into **three parallel representations that do not have to match**:

| Representation | Answers | Optimised for |
| --- | --- | --- |
| **Render** | *What does the camera show?* | Seeing **into** the room — walls lowered/cut when they face the camera, ceilings hidden, glass see-through |
| **Light** | *What shapes illumination & shadow?* | Physical correctness — full-height walls, closed ceilings, real occlusion, **regardless of what Render is doing** |
| **Sim** | *What does interaction/gameplay use?* | Rules — collision for placement & selection, room enclosure, (later) routing, temperature, sound |

The whole point: **one wall segment can be a short stub in Render, a full-height occluder in Light,
and a full-height collider in Sim — all at the same time.** When these three diverge *on purpose*,
the editor feels right (you see in) while the world stays honest (it's still a closed, shadowed room).

This is the rule to hold onto: **"lowered/hidden for viewing" is a Render decision only. It must
never silently change Light or Sim.**

---

## How the split is expressed (Unity / HDRP)

| Need | Mechanism |
| --- | --- |
| Exists for shadow / occludes light, **but is not drawn** | `MeshRenderer.shadowCastingMode = ShadowsOnly` |
| Drawn, but **casts no shadow** (light passes through it) | `shadowCastingMode = Off` |
| Block a point/spot light (a lamp inside a room) | any shadow-caster between the light and the lit surface |
| Interaction volume ≠ visible mesh | `Collider` sized independently of the render mesh |
| Two forms of one element | sibling GameObjects, or one render mesh + one separate shadow mesh |

`ShadowsOnly` is the key primitive for this whole subsystem: it is exactly "exists for light, invisible
to the camera."

---

## Requirements captured so far

Status: ✅ done · 🟡 partial · ⬜ todo

### R1 — A closed loop of walls is a ROOM ⬜
Enclosed wall segments define a **room**; a room owns a floor and a ceiling. Rooms are the unit that
"has an interior" for lighting and (later) simulation.
*Today walls are independent segments — there is no room/enclosure detection yet.*

### R2 — A room's CEILING lights but does not render ✅ *(TASK-002, done 2026-07-13)*
The ceiling blocks skylight/sun (so the interior is correctly shadowed, not flooded from above) and
helps contain lamp light — but is **never drawn**, so you always see into the room from above.
→ enclosure detected by flood-fill over the cell grid; a per-level double-sided ceiling mesh over the
enclosed cells, set to `ShadowsOnly` (`HomeEditor.RefreshRooms` / `BuildCeiling`). Double-sided so it
blocks the sun from above AND contains lamp light from below. v1 limits: diagonal walls don't count
toward enclosure; interior is lit by ambient + lamps only.

### R3 — Lowering a wall for VIEW must not change LIGHT or SIM ✅ *(TASK-001, done 2026-07-13)*
The cutaway drops a foreground wall to a short stub **visually**. For light and collision it stays
**full-height**: the room stays as shadowed and as enclosed as if the wall were whole.
- **Render** — lowered stub (miters + door/window cutouts preserved — the stub is CLIPPED at stub
  height, not clamped, so lintels drop out and openings stay real gaps; see BUG-004). ✅
- **Sim** — full-height collider kept under the stub (selection/paint still work). ✅
- **Light** — a full-height invisible `ShadowsOnly` "ghost" wall (`HomeEditor.SetWallLowered`): the
  stub renders with `shadowCastingMode = Off`, the ghost casts the whole wall's shadow (openings
  included), so the shadow footprint is identical whether the wall is up or cut. ✅

### R4 — Glass renders transparent AND passes light ✅
Windows: transparent material (Render = see-through) + `shadowCastingMode = Off` (Light = light passes,
no solid shadow). The two forms deliberately disagree with a normal wall.

### R5 — Openings / portals are holes in all three ✅
A portal is the *absence* of geometry in Render, Light, and Sim alike — nothing to draw, nothing to
occlude, nothing to collide.

### R6 — Doors gate light by state ⬜
A **closed** door is an opaque occluder (blocks light like a wall); an **open** door passes light.
Today the leaf is always opaque-rendered with no open/closed light state.

---

## Open questions / to design

- **Room detection** — how a closed wall loop becomes a "room" object (flood-fill over the wall graph?
  explicit room paint? per-cell interior/exterior classification?).
- **Multi-level** — an upper floor *is* the ceiling of the room below; do they share one surface, or is
  the ShadowsOnly ceiling separate from the rendered upper floor? (Floor tiles are already two-faced.)
- **Direct vs GI** — `ShadowsOnly` handles **direct** occlusion cleanly. Indoor ambient/bounce and AO
  from the hidden ceiling only behave if the GI system includes that invisible geometry.
- **Cutaway granularity & timing** — whole-wall vs per-height-band; the ghost/full occluder must stay
  put while only the visible stub swaps as the camera orbits.
- **Performance** — a full-height ghost per lowered wall roughly doubles wall shadow-draw; merge/batch
  the ShadowsOnly occluders per room.
- **Windows through walls** — sun through a window should light the interior in a window-shaped patch;
  needs the wall to occlude but the glass opening to pass (R3 + R4 interacting).

---

*Next likely step when we act on this: R2 + R3-Light together — generate a per-room `ShadowsOnly`
ceiling and swap lowered walls to a full-height `ShadowsOnly` ghost — so a cut-open room stays lit
exactly like a closed one.*
