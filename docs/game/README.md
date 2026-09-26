# Game Documentation

Documentation for **the game** (working title: TBD) — a city-scale life-simulation sandbox built in Unity 6.3 HDRP, where autonomous NPCs live their lives across a city and the player can take direct control ("possess") any of them.

> **This is a separate documentation set from the Sims4Browser resource-explorer docs.**
> The explorer/exporter (see [`../README.md`](../README.md), [`../../AGENT.md`](../../AGENT.md)) is the **asset toolchain** that extracts and rebuilds Sims 4 content. This game is a **different product** built on top of that toolchain's output. These docs may *reference* the browser docs (e.g. for asset-pipeline truth) but do not reuse or modify them.

## The documentation discipline (applies to all game work)

1. **Every significant decision is recorded** in [decisions.md](decisions.md) — dated, with context, the decision, rationale, and alternatives considered. Decisions are superseded by new entries, never silently rewritten.
2. **Every system gets a design doc** under this folder (added as systems are built) and is kept in sync with the code that implements it.
3. **Rules and conventions** are written down when set and followed everywhere; conventions live in [architecture.md](architecture.md#conventions) until they need their own file.
4. **Docs are updated in the same change** as the code or decision they describe. If reality diverges from a doc, the doc is wrong — fix it or supersede it.
5. **The live implementation plan** for whatever is currently being built lives in [roadmap.md](roadmap.md#current-focus).

## Index

- [gdd.md](gdd.md) — **what the game is**: vision, pillars, core loop, scope
- [architecture.md](architecture.md) — **how the engine is built**: model, systems, patterns, Unity approach, conventions
- [decisions.md](decisions.md) — the decision log (open questions + accepted decisions)
- [roadmap.md](roadmap.md) — milestones + current focus
- [systems/](systems/) — per-system design docs (added as each system is built)
  - [systems/m0-foundations.md](systems/m0-foundations.md) — soul model, clock, needs, debug HUD
  - [systems/m1-autonomy.md](systems/m1-autonomy.md) — utility-AI, smart objects, interactions
  - [systems/m2-possession.md](systems/m2-possession.md) — point-and-click possession (completes the vertical slice)
  - [systems/m3-social.md](systems/m3-social.md) — Sim-to-Sim social + relationships
  - [systems/m4-ingame-cas.md](systems/m4-ingame-cas.md) — in-game Create-A-Sim (edit a Sim's look live)
  - [systems/m4-ingame-build.md](systems/m4-ingame-build.md) — in-game Build (place/move/delete lot objects)
  - [systems/build-catalogue.md](systems/build-catalogue.md) — **Buy catalogue**: bottom glass dock, category rail + room filter, virtualized thumbnail grid, colour swatches
  - [systems/locomotion-navigation.md](systems/locomotion-navigation.md) — **how Sims move**: grid vs continuous, pathfinding, idle/walk/run animation
  - [systems/ui-framework.md](systems/ui-framework.md) — **game UI**: UI Toolkit shell, theming, conventions
  - [systems/debug-tools.md](systems/debug-tools.md) — **debug menu** (F3): grid/occupancy/sim-cell/path visual debugging

## Relationship to existing work

The game reuses, as *content and edit tools*, what already exists under [`../../unity/Sims4Creator/`](../../unity/Sims4Creator/): the CAS character creator, the building editor, the animation-clip library, and the save/load templates. The headless `exportchar`/`exportcloth`/`exporthair` pipeline (in `tools/Sims4UnityExport`) produces the game's characters and wardrobe. Asset-extraction truth (formats, materials, UV) is owned by the browser docs and referenced, not duplicated, here.
