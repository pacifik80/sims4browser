# System: M2 Possession

Status: **✅ verified working (2026-07-14).** The game's core hook, and the milestone that completes the **vertical slice**: the player can drop into any Sim and drive it, then let go. Implements decision [D-002 (point-and-click)](../decisions.md) and [architecture §4](../architecture.md#4-possession--brain-swap-directed-control).

## How it plays

- **Click a Sim** → you possess it. Its autonomy suspends (the AI stops choosing for it) and a green marker floats over its head.
- **Click a smart object** → the possessed Sim walks over and performs that object's interaction (the *same* interaction the AI would run — you're just the one picking now).
- **Esc**, or the HUD **Release** button → autonomy resumes; the AI takes over again from wherever the Sim is.
- Needs keep ticking throughout, so a possessed Sim still gets hungry — you're responsible for it while you hold it.

This is the "brain swap": possession replaces the AI brain with player-issued interactions on the same soul. Nothing else changes — no separate control scheme, no character controller.

## Code (`unity/Sims4Creator/Assets/Scripts/Runtime/Game/`)

| File | Role |
|---|---|
| `PlayerController.cs` (new) | Raycasts clicks (old Input backend): a hit on a `SimBody` possesses it; a hit on a `SmartObject` while possessing directs the Sim there; Esc releases. Owns the floating possession marker. |
| `SimAgent.cs` (updated) | New `AutonomyEnabled` flag — false while possessed so the AI won't auto-pick; the agent still *executes* whatever it's told (AI or player). |
| `SimulationDirector.cs` (updated) | Skips AI picking for agents with `AutonomyEnabled == false`; ensures each Sim has a `CapsuleCollider` so it's clickable. |
| `GameDebugHud.cs` (updated) | Shows "POSSESSING: name", a Release button, the last action message, and a ▶ marker on the possessed Sim. |

Scene builder adds a `PlayerController` object and wires it to the HUD.

## Deliberate M2 simplifications (tracked)

- **First advertisement only** — clicking an object queues its first interaction. Objects with several interactions need a small context menu (next iteration).
- **Static overview camera** — clicks raycast from the fixed camera; an orbit/pan "god" camera is a polish pass.
- Same M1 caveats still apply (slide instead of walk animation; no object occupancy).

## How to run

Re-run **Sims4 Creator → Game → Build M0 Scene**, press **Play**, then **click Adam** (green marker appears, autonomy stops), **click the Fridge** (he walks over and eats), and **Release** (Esc) to watch the AI take back over.

## Vertical slice — complete

M0 (foundations) + M1 (autonomy) + M2 (possession) together are the slice: a lot of Sims living their own lives, and you can jump into any of them. Next milestones (see [../roadmap.md](../roadmap.md)) add breadth: real Sim-to-Sim social (M3), in-game Build/CAS (M4), the city + scale (M5).
