# System: Locomotion & Navigation

Status: **design agreed → implementing in phases.** How Sims move, animate, and route around a lot. This doc settles the grid question and the animation architecture; phases below track what's built.

## The core decision: a gridded world, continuously-moving Sims (D-106)

**The 1 m TILE grid is the first-class space authority** (decision D-106, user-set 2026-07-21): furniture
and build placement snap to tiles, a per-tile **occupancy map** is the placement-validity authority (not
collider physics), and Sims **stand and interact occupying a full tile** — the approach slot in front of
an object is a tile, two chatting Sims stand in adjacent tile centres. *Ruled exceptions:* intimate
interactions (hugs, kisses) may break the tile rule; sitting/sleeping get furniture-slot mechanics later.

Sims still MOVE continuously: routing runs on a finer **nav sub-grid** (tile / 2 = 0.5 m), and bodies
glide along the smoothed path with a soft radius. Cell-locked *movement* reads robotic and jams doorways;
tile-locked *standing* is what reads as The Sims. The grid constrains where things ARE and where Sims
STAND — not how they travel between.

What the grid buys us:
- **Exact footprints** — an object snapped to tiles owns exactly those tiles (occupancy), and nav blocks
  the sub-cells under those same tiles; one source of truth.
- **Instant updates** — build mode re-registers occupancy + re-marks nav (no NavMesh re-bake).
- **Cheap, deterministic pathfinding** — A* over a small grid, easy to visualise and debug.
- **Natural use-slots** — an object exposes the tile in front of its footprint to stand in.

| | Value |
|---|---|
| MAIN tile (placement/occupancy/interaction) | **1 m** — Sims-standard; shared with the home-editor engine (walls, TASK-011) |
| Nav sub-cell (routing only) | **0.5 m** (tile / `navSubdivision`, fine enough for doorway gaps) |
| Sim radius | ~**0.22 m** (clearance: nav inflates footprints by it; Sims are soft bodies, not hard colliders) |
| Sim position | **Continuous** (float x/z) while moving; **tile centres** when standing to use/talk |

### Alternative considered — Unity NavMesh
`NavMeshSurface` + `NavMeshAgent` would give pathfinding and dynamic avoidance for free. **Rejected for now**: it needs the AI Navigation package, and every build-mode edit means a re-bake or `NavMeshObstacle` carving, whereas the grid the build system *already implies* gives exact footprints and instant updates. Revisit if multi-floor/stairs or crowd avoidance gets heavy.

## Navigation design

1. **`LotGrid`** — ONE definition (origin, 1 m tile, nav subdivision): tile **occupancy map** (`TilesFree`
   = placement validity, `OwnerAt`) + sub-cell `walkable` bitmap derived from the registered footprints
   (later: room id, reservation).
2. **Footprints** — each `SmartObject`'s snapped tiles are registered as owned; nav blocks the sub-cells
   under those SAME tiles (inflated by the Sim radius). The **approach slot** is the tile centred in
   front of the footprint (rotates with the item). Build mode re-registers on move/place/rotate/delete.
3. **Pathfinding** — A* over walkable sub-cells from the Sim's position to the approach-slot tile centre,
   respecting Sim clearance (footprints are inflated at mark time).
4. **Path smoothing** — string-pulling (line-of-sight shortcutting) so the Sim walks clean diagonals instead of stair-steps.
5. **Steering** — follow smoothed waypoints: move toward the next point, rotate at a max turn rate, arrival tolerance. Speed comes from the locomotion state (walk/run).
6. **Re-planning** — if the lot changes (build mode) or the path is blocked for N seconds, re-plan.

### Obstacle avoidance
- **Static** (furniture, later walls): handled entirely by grid blocking + A*.
- **Dynamic** (other Sims): Sims are **soft** obstacles — a gentle separation steer when within ~0.5 m, and they may slip past each other. Hard collision between Sims causes deadlocks and is deliberately avoided.
- **Reservations**: an object in use is reserved (`reservedBy`) so two Sims don't share one bed/toilet. *(Currently missing — a known gap from M1.)*

## Animation design

Clips already exported (per Sim, in `<slug>/animations/`) — enough for the full loop:

| Purpose | Clips |
|---|---|
| Standing idles (varied) | `a_idle_waiting_loop_1`, `a_idle_waiting_loop_4`, `a_idle_lookAround_female`, `a_idle_female_lookBothWays`, `a_idle_handFidget_female`, `a_idle_female_handfidget_smile` |
| Walk | `a_loco_default_walk_LFoot_long` (neutral), `a_loco_walk_feminine_LFoot_medium` (feminine) |
| Run | `a_loco_run_LFoot` |

Playback uses the existing **legacy `Animation`** component that `Sims4AnimationBuilder` already populates (no Mecanim rebuild). `Animation.CrossFade` gives adequate transitions; if blending quality demands it later, this layer can be swapped for an Animator blend tree behind the same interface.

**`SimLocomotion`** owns the animation state:
- Measures **ground speed** from the transform delta (decoupled — works no matter who moves the Sim).
- Picks a state: **Idle** (≈0) → **Walk** → **Run** (above a threshold), and cross-fades (~0.2 s).
- **Speed-matched playback**: `clip.speed = groundSpeed / clipNativeSpeed` — this is what kills foot-sliding. `clipNativeSpeed` is **measured, not guessed**: while a locomotion clip plays, the **planted foot** slides backward in character-local space at exactly the ground speed the animation depicts. On first use of a clip we play it at rate 1 for ~0.6 s, sample the lower (stance) foot's backward local travel, and cache the result statically (measured once per clip per session, not per Sim). Frames where the body is turning are skipped (rotation pollutes a local-space measurement), and an out-of-range result falls back to a hand-set value.
- **Idle variety**: while idle, cross-fades to a different random idle every few seconds.
- **Per-gender sets**: male → neutral walk + neutral waiting idles; female → feminine walk + female idles.

> **Important:** `Sims4IdleSwitcher.autoCycle` currently cycles **every** imported clip (yoga, sleep, dance, walk), which is why Sims strike odd poses. `SimLocomotion` takes over animation and disables that auto-cycling.

**Run trigger ("hurry")**: the *agent* decides speed, the animation just follows.
- Player: **double-click** anything while possessing — an object, another Sim, or **bare ground** → hurry there at a run.
- Autonomy: a critical need (e.g. bladder below a threshold) → hurry.

**Click-to-move**: clicking bare ground while possessing issues a plain "go stand there" order (`SimAgent.GoTo`, state `MovingTo`) — a routed move with no interaction at the end. Sims are no longer limited to travelling only between objects.

## Phases

- **L1 — Animation states** *(built, awaiting Play-test)*: `SimLocomotion` resolves clips by name fragment off the legacy `Animation` component, derives Idle/Walk/Run from measured ground speed, cross-fades (~0.2 s), scales clip playback to ground speed (anti-slide), and cycles a random standing idle every ~7 s. It disables `Sims4IdleSwitcher.autoCycle` (the cause of yoga/sleep poses while standing). `SimAgent` gained `runSpeed` + a per-action `Hurry` flag; the director sets `Hurry` when the driving need is critical (<15), and the player sets it by **double-clicking** a target. Per-gender sets: neutral walk + waiting idles for male, feminine walk + female idles for female.
- **L2 — Grid + A*** *(built, awaiting Play-test)*: `NavGrid` (pure C#: 8-connected A* with no diagonal corner-cutting, plus string-pull smoothing) + `LotGrid` (MonoBehaviour: world↔cell conversion, footprints from every `SmartObject`'s bounds **inflated by the Sim radius** so a Sim's centre can follow the path safely, path API, blocked-cell gizmos). `SimAgent` now follows smoothed waypoints via `StepAlongPath` and re-plans when the goal moves or the grid `Version` changes; with no `LotGrid` present it degrades to the old straight line. Build-mode place/move/delete calls `Rebuild()`, so **Sims re-route around furniture the moment you move it**. Both path ends snap to the nearest free cell (a Sim can stand inside an inflated footprint, and an object's use-anchor often sits inside its own).
- **L3 — Avoidance + reservations** *(built, awaiting Play-test)*:
  - **Reservations** — `SmartObject` gains `exclusive` + `ReservedBy` / `TryReserve` / `Release`. The brain skips objects claimed by someone else, `SimAgent.Begin` claims on departure (losing the race just re-decides next frame), and any exit path releases. Bed/toilet/shower/fridge are exclusive; **TV and couch are shared** (`exclusive = false`).
  - **Soft avoidance** — moving Sims steer away from other Sims within `separationRadius` (0.6 m), strength-scaled by closeness, blended into the movement direction (and they face where they actually travel). Deliberately a *steer*, never a collision, so two Sims can't deadlock.
  - **Stall guard** — a route is abandoned after `maxTravelSeconds` (30 s) so an unreachable target can never freeze a Sim permanently.
- **L4 — Polish**: start/stop easing, turn handling (turn-in-place for large turns), sit/use transitions so Sims actually sit on things.
