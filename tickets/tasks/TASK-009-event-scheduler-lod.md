---
id: TASK-009
title: Discrete-event scheduler for Background/Dormant LOD tiers
status: open
area: game
priority: medium
created: 2026-07-21
---
**Goal** — a game-time event queue so off-lot Sims advance via scheduled events ("arrives 09:15",
"meal ends 09:45") popped as the clock advances, instead of being ticked every frame. This is what
keeps fast-forward cheap and makes a city viable.

**Acceptance** — Background/Dormant Sims cost ~nothing per frame; their needs/schedules advance
correctly on the game clock; a background day's outcome matches the continuously-simulated version
within tolerance. The Active tier stays continuously simulated.

**Notes** — needed for M5 (city & scale); worth building before the city rather than retrofitting. See
`docs/game/architecture.md` §2b (speed-invariance) + §2 (sim-LOD). The current sim ticks everything
every frame — correct for one lot, wrong for a neighbourhood at 5×.
