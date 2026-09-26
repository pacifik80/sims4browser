# System: Debug tools

*Status: **v2 built — proper UI Toolkit menu + render tunables (awaiting user verify)** — 2026-07-22.
Owner tickets: `tickets/tasks/TASK-015` (menu), `tickets/tasks/TASK-016` (lighting).*

Dev-facing tooling for seeing what the engine is actually doing. Per user direction the debug menu is a
**proper menu in the game's own UI framework** (UI Toolkit + GameTheme glass, normal scale) — not an
IMGUI side box. It lives in its own UIDocument (sortingOrder 30), so it draws and works in every mode
(play, build, CAS).

## The debug menu — **DBG button** (HUD, top-right) or `F3` (GameDebugMenu)

Self-healing: if the scene predates a piece, the menu still opens, bootstraps what it can, and shows a
"re-run Build M0 Scene" hint for parts that need scene-baked assets.

### GRID section (visual truth for D-106, drawn by GridDebugOverlay)

| Option | Shows |
| --- | --- |
| **1 m grid** | the shared grid overlay quad (same visual build mode uses) |
| **Furniture occupancy** | an orange quad on every tile OWNED in `LotGrid`'s occupancy map — the placement authority itself (+ live owned-tile count) |
| **Sim cells** | per Sim: **current** tile (cyan), **goal** tile (green), **transit** tiles of the remaining route (violet) |
| **Sim paths** | the actual routed waypoints on the 0.5 m nav sub-grid, as a line per Sim |
| **Nav blocked sub-cells** | the 0.5 m cells routing refuses (registered footprints + sim-radius inflation) |

Plus a live per-Sim list (name, status, current tile → goal tile).
**Known-by-design (visible here):** Sims do **not** reserve tiles — standing or in transit (soft
separation only). Whether they should is TASK-014 phase 2; judge it with this view.

### RENDER section (TASK-016 — tune live FIRST, bake after)

The artistic/grade knobs stay menu-tunable until values are settled, then get baked as scene defaults:

| Knob | What it drives |
| --- | --- |
| **SSGI** | screen-space bounce light (GlobalIllumination volume override) |
| **Sun shadow dimmer** | lifts sun shadows (HDAdditionalLightData.shadowDimmer, default 0.8) |
| **Ambient boost ×** | IndirectLightingController indirect-diffuse multiplier |
| **Exposure offset (EV)** | added to SkyTimeController's per-time-of-day fixed exposure |
| **Fill light + lux** | shadowless opposite-the-sun directional (default OFF) |
| **Tonemap** | None / Neutral / ACES |
| **Shadow lift (grade)** | Shadows-Midtones-Highlights shadows offset |
| **Moon max lux (night)** | moonlight strength (real moon = 0.25 lx; the PBR sky ALSO scatters it — too high turns night into blue day; game default 6) |
| **Night exposure EV** | fixed EV at full night (day = 13; LOWER = brighter night; game default 3) |

**Bake handoff:** volume-override edits write to `GameVolumeProfile.asset` (persist in-editor), and the
**"Log tuning values"** button prints a one-line `[RenderTuning]` snapshot to the Console — paste it in
chat and the values get baked as defaults.

## Implementation

- `Runtime/Game/GameDebugMenu.cs` — the UI Toolkit menu (code-built, GameTheme classes `.debug-menu` etc.,
  own UIDocument, registers with `UiPointer` so its clicks/sliders never leak to the world).
- `Runtime/Game/GridDebugOverlay.cs` — WORLD visuals only (pooled quads/LineRenderers, scene-baked
  `Debug*.mat` — HDRP transparency keywords are editor-only).
- `Runtime/Game/RenderDebugController.cs` — the render knobs (volume sharedProfile + sun/fill/sky refs).
- Introspection: `LotGrid.OwnerAt/NavX/NavZ/IsNavBlockedAt`, `SimAgent.DebugPath/DebugGoal/DebugMoving`,
  `SkyTimeController.exposureOffset`.
- Wired by `GameSceneBuilder` ("Debug" GO); HUD's DBG button + F3 both toggle it, with a runtime
  bootstrap if the scene predates the menu.

## Backlog (add here as needed)

Need bars/decision scores over heads, utility-AI pick explanations, object reservation states, clock/
speed diagnostics, relationship matrix, teleport-sim and fill/drain-need cheats.
