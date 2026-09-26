# System: M4 — In-game CAS

Status: **built, awaiting Play-test.** The "author the world" pillar comes into the game: possess a Sim and edit their appearance/clothes/personality with the full Create-A-Sim editor, without leaving the running world. This is **part 1 of M4** (CAS); in-game **Build** is the next increment.

## How it plays

- Possess a Sim → the HUD shows an **Edit Look (CAS)** button.
- Press it → the world **pauses**, the camera frames the Sim, and the full **CAS panel** appears (skin/tone/detail, eyes, hair + colour, clothing per slot, body + face morph sliders, emotion/viseme — everything the standalone CAS editor offers, because it *is* that editor).
- Edit live; the Sim updates as you go.
- **◄ Back to Game** (or **Esc**) → the world resumes and you're back in gameplay with the Sim changed.

## Design — reusing the existing CAS editor

The mature `CasController` (the runtime CAS panel that drives every `Sims4Character` knob) is reused wholesale. It binds its target in **Awake** (once per instance), so to edit an arbitrary Sim, `GameCasMode` creates a **fresh** controller each session: it makes the GameObject inactive, sets `character` to the Sim's body, then activates it so Awake binds correctly. On exit it destroys the controller and re-enables the Sim's idle (which CasController disables while editing).

## Code (`unity/Sims4Creator/Assets/Scripts/Runtime/Game/`)

| File | Role |
|---|---|
| `GameCasMode.cs` (new) | `EnterCas(sim)` / `ExitCas()`: pause (`Time.timeScale = 0`), frame the camera, spin up a `CasController` bound to the Sim, hide the game HUD + disable world clicks; reverse it all on exit and restore the Sim's idle. Draws the "Back to Game" button. |
| `GameDebugHud.cs` (updated) | Adds the **Edit Look (CAS)** button beside Release when a Sim is possessed. |

Scene builder adds a `Game CAS Mode` object wired to the HUD/player/clock.

## Deliberate simplifications / known caveats (tracked)

- **Game lighting, not CAS lighting.** Editing happens under the game scene's default lighting, not the dedicated CAS 3-point rig — functional, just less flattering. A polish pass could swap lighting on enter.
- **Camera**: on entry the Sim turns to face the camera and `GameCasMode` frames a full-body front view; since there's no `CasCameraRig`, it supplies its own controls — **right-drag to orbit, scroll to zoom**. (Assumes the character's forward is its face, as the walk code does; if a Sim opens back-first, just orbit around.)
- **Clones share materials.** Editing a cloned Sim's skin/hair may bleed to its siblings (the edit-time clones share style materials). Editing the two originals (Adam/Eve) is clean; per-instance material cloning is the fix.
- **Look isn't persisted to the soul yet.** Edits live on the body for the session; capturing the edited look back into the Sim's template/soul (so it survives respawn/save) reuses the existing template Capture/Apply and is a follow-up.

## How to run

Re-run **Sims4 Creator → Game → Build M0 Scene**, **Play**, **click a Sim** to possess, then **Edit Look (CAS)** on the HUD. Change hair/clothing/skin/morphs, then **Back to Game**.

## Next

**In-game Build** (M4 part 2): a lightweight build mode to place/move/delete objects on the lot (the full walls/rooms building editor is earlier-stage and integrates later). See [../roadmap.md](../roadmap.md).
