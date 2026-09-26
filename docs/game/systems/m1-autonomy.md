# System: M1 Autonomy

Status: **✅ verified working (2026-07-14).** The life comes on: Sims now *choose* what to do and act on it. Needs go up as well as down, driven by the utility-AI + smart-object pattern from [../architecture.md §3](../architecture.md#3-autonomy-utility-ai-over-smart-object-advertisements).

## The loop

A need decays → the Sim's brain scores every reachable interaction → it walks to the best object → performs it → the need climbs → when satisfied it re-evaluates and does the next thing. This is the same interaction system the player will drive when possessing (point-and-click → queue an interaction).

## Code (`unity/Sims4Creator/Assets/Scripts/Runtime/Game/`)

| File | Role |
|---|---|
| `SmartObject.cs` | `InteractionAdvertisement` (needId, satisfy-per-game-hour, duration) + `SmartObject` (a world object with a list of advertisements and a `useAnchor` to stand at). |
| `UtilityBrain.cs` | Pure-ish scorer: `Pick(soul, objects, fromPos, minDeficit)` → best `(object, advertisement)`. Score = `deficit + 0.1·satisfyPerHour − 2·distance`; skips needs less than `minDeficit` below full. |
| `SimAgent.cs` | Body-side executor: state machine **Going → Performing**. Straight-line walks to the anchor (no NavMesh yet — the lot is open), then applies `satisfyPerHour · ΔgameHours` to the need for the interaction's duration. Exposes `IsIdle` + `StatusLabel`. |
| `SimulationDirector.cs` (updated) | Each frame: decays all needs, and for every **idle** Sim asks the brain to pick and calls `SimAgent.Begin`. Ensures every `SimBody` has a `SimAgent`; caches the scene's `SmartObject`s at play start. |
| `GameDebugHud.cs` (updated) | Now shows each Sim's current action (`→ Fridge`, `Eat`, `idle`) next to its name. |

Editor `GameSceneBuilder.cs` now also `BuildLot()`s six placeholder smart objects (labelled coloured boxes) and pulls the camera back to an overview.

## Smart objects placed (starter, tunable in the builder)

| Object | Interaction | Need | Satisfy/h · duration |
|---|---|---|---|
| Fridge | Eat | hunger | 80 · 0.8h |
| Bed | Sleep | energy | 55 · 2.0h |
| Toilet | Use toilet | bladder | 200 · 0.4h |
| Shower | Shower | hygiene | 110 · 0.5h |
| TV | Watch TV | fun | 45 · 1.2h |
| Couch | Relax | social | 50 · 1.0h |

## Deliberate M1 simplifications (tracked for later)

- **Straight-line locomotion**, no NavMesh — fine on an open lot; NavMesh arrives with walls/obstacles (around M4/build).
- **No walk animation** — the Sim slides while idle-animating. Locomotion-clip syncing is a polish pass.
- **No object occupancy** — several Sims could use one object at once. Reservations/queueing come with the interaction system's next iteration.
- **Social is a placeholder** (a couch feeds "social"); real Sim-to-Sim social is M3.

## How to run

Re-run **Sims4 Creator → Game → Build M0 Scene** (it now also places the objects), press **Play**, set speed to **2×/3×**, and watch: Adam and Eve walk to the fridge / bed / TV as their needs dip, and the HUD shows what each is doing.

## Next (M2)

Possession: select a Sim, then click a smart object to *manually* queue its interaction (suspending autonomy), and release to hand control back to the brain. See [../roadmap.md](../roadmap.md).
