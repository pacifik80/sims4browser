# System: M0 Foundations

Status: **✅ verified working (2026-07-14).** The skeleton of the core loop — souls, a clock, and decaying needs — runs in Play mode: two characters spawn as Sims and their needs decay live on the HUD with working speed controls. Proves the soul/body split, the game clock, and the needs model before autonomy (M1) lands on top.

## What it is

A runnable scene where a couple of existing characters stand on a lot, a clock runs day/night at a controllable speed, and each character's needs decay live on a debug panel.

## Code (all in `unity/Sims4Creator/Assets/Scripts/Runtime/Game/`, namespace `Sims4Creator.Game`)

| File | Role |
|---|---|
| `SimSoul.cs` | `SimSoul` (the persistent, engine-independent Sim state) + `NeedValue` (one need's 0–100 value). Plain C#, serializable, **no UnityEngine dependency**. |
| `NeedsSystem.cs` | Pure `Decay(soul, rates, gameHours)` logic. No UnityEngine dependency → unit-testable, and runs for bodiless off-lot Sims. |
| `NeedDefinition.cs` | ScriptableObject: a need's id, display name, decay-per-hour, start value, bar colour. The need set is **data**. |
| `GameClock.cs` | Master clock: day + time-of-day advanced at speed `0/1x/2x/3x`; exposes `DeltaGameHours` per frame; drives `SkyTimeController` for the visual day/night (clock owns time; sky follows, `autoAdvance=false`). |
| `SimBody.cs` | Presentation body: bridges a built character GameObject to its runtime `SimSoul`. Authored `displayName`/`appearanceSlug`; `Soul` bound at play start. |
| `SimulationDirector.cs` | Owns the souls; at play start creates one soul per `SimBody` (seeded with the need set) and binds it; each frame decays needs by `clock.DeltaGameHours`. |
| `GameDebugHud.cs` | IMGUI panel: clock + speed buttons + per-Sim need bars. The first per-aspect "test tool". |

Editor: `Assets/Scripts/Editor/GameSceneBuilder.cs` — menu **Sims4 Creator/Game/Build M0 Scene**. Creates the default need assets under `Assets/Game/Needs/`, builds `Assets/Scenes/GameM0.unity` (ground, camera, clock+sky, two characters `am_char`/`af_char` as Sims, director, HUD).

## Design notes / deliberate M0 simplifications

- **Bodies are authored in the scene**, not spawned at runtime. Pooled runtime spawning (from souls) is a later concern; M0 just needs a few Sims on screen.
- **Souls are created from `SimBody`s at play start.** Save/load of souls comes with persistence.
- **Needs only decay.** Satisfaction (needs going *up* from completed interactions) arrives with M1 autonomy + smart objects.
- Bodies reuse `Sims4CharacterBuilder.BuildCustomizerSlug` → they carry the full CAS character incl. the idle animation, so M0 Sims idle-animate for free.

## Need set (starter, tunable as assets)

Hunger (8/h), Energy (6/h), Fun (10/h), Social (7/h), Hygiene (9/h), Bladder (14/h) — decay per **game** hour; edit the assets in `Assets/Game/Needs/` to retune.

## How to run

1. Focus Unity so it compiles the new scripts (check the Console is clean).
2. Menu **Sims4 Creator → Game → Build M0 Scene**.
3. Press **Play**. Watch the need bars fall; use **Pause/1x/2x/3x** (3× ≈ 30 game-min/real-sec, so decay is visible in seconds).

## Next (M1)

Add the utility-AI brain + smart objects so Sims choose interactions that *raise* needs, and NavMesh so they walk to them. See [../roadmap.md](../roadmap.md).
