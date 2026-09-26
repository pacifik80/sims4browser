# Game Roadmap

Durable milestone plan for the game. **Vertical-slice-first**: prove the core loop on one lot before adding breadth. Each milestone should end in something runnable.

## Current focus

**Decisions locked (2026-07-14):** single active lot, point-and-click directed control, needs + autonomy first (see [decisions.md](decisions.md)); foundation architecture accepted.

**M0 — ✅ verified working (2026-07-14).** Soul model + game clock + decaying needs + debug HUD run in Play mode; two characters spawn as Sims and their needs decay on the HUD. See [systems/m0-foundations.md](systems/m0-foundations.md).

**M1 — ✅ verified working (2026-07-14).** Utility-AI brain + smart-object advertisements + straight-line locomotion: Sims choose the best object for their biggest need, walk to it, and satisfy it. See [systems/m1-autonomy.md](systems/m1-autonomy.md). NavMesh deferred until lots have obstacles.

**M2 — ✅ verified working (2026-07-14).** Point-and-click possession: click a Sim to take control (its autonomy suspends), click objects to direct it, Esc/Release to hand back to the AI. See [systems/m2-possession.md](systems/m2-possession.md).

**🎉 Vertical slice complete (M0+M1+M2).** A lot of Sims live autonomously and the player can possess any of them. Core mechanics proven; from here the roadmap is breadth.

**M3 — ✅ verified (6 Sims, 3× stable).** Sim-to-Sim social + relationships: Sims walk to each other, chat, and build a relationship (autonomously and when directed while possessing). Lot populated with six Sims; the 3× stack-overflow (cross-recursive social teardown) was fixed with a self-healing handshake. See [systems/m3-social.md](systems/m3-social.md).

**M4 — built, awaiting Play-test (both halves).** The "author the world" pillar, in-game:
- **CAS** — possess a Sim → "Edit Look (CAS)" pauses the world and opens the full CAS editor (own orbit/zoom camera) on that Sim. Reuses `CasController`. See [systems/m4-ingame-cas.md](systems/m4-ingame-cas.md).
- **Build** — "Build Mode" pauses the world; a palette places objects, drag moves them, Delete removes them; newly placed objects are used by the AI immediately. Shared `SmartObjectCatalog`/`SmartObjectFactory`. See [systems/m4-ingame-build.md](systems/m4-ingame-build.md).

## Milestones

### M0 — Foundations (bootstrap the loop's skeleton)
Bootstrap scene + manager singletons; **Time system** (reuse `SkyTimeController`); **Sim soul model** + registry; spawn a few existing characters (`am_char`/`af_char`) as bodies on one lot; **needs decay** over time; a **debug HUD** (needs, clock, sim list). No AI yet — proves soul/body + time + needs.

### M1 — Autonomy (the core sim)
**Smart objects** + **utility AI** + **NavMesh**: Sims autonomously choose and perform need-satisfying interactions on one lot (eat, sleep, etc.). This is the milestone that proves the game is a *simulation*.

### M2 — Possession
God camera + **possess/release** any Sim; drive the possessed body (per D-002); needs keep ticking; release resumes autonomy. Completes the **vertical slice** (M0–M2): a lot of Sims living autonomously, and you can jump into any of them.

### M3 — Social & relationships
Sim↔Sim interactions; a relationship graph; social need driven by it.

### M4 — In-game Build & CAS
Enter the existing building editor and character creator on the active lot, live.

### M5 — City & scale
Multiple lots; streaming; the sim-LOD tiers; inter-lot routing; jobs + schedules. (Depth here depends on D-001.)

### M6 — Events, economy, polish
Scripted/emergent events; money/economy; wandering, ambient city life; polish.

## Definition of "vertical slice" (the near-term target)

One lot · a handful of existing characters · a day/night clock · needs that decay · autonomous need-satisfaction · possess/release of one NPC · debug HUD. When this is fun to watch and to jump into, the core loop is proven and breadth (M3+) is justified.
