---
id: TASK-008
title: Locomotion & animation polish (easing, turn-in-place)
status: open
area: game
priority: low
created: 2026-07-21
---
**Goal** — smooth out movement now that routing is real: ease speed in/out (accel/decel instead of
instant start/stop), turn-in-place for large heading changes (so Sims don't pivot-slide on sharp
corners), and confirm the measured walk/run clip speeds hold across game speeds.

**Acceptance** — Sims accelerate/decelerate rather than snapping between idle and full walk; a >~100°
turn is a turn-in-place, not a sliding pivot; feet stay planted at 1×–5×.

**Notes** — extends `SimLocomotion` (clip-speed measurement is done) + `SimAgent` steering. This is
"L4 — polish" in `docs/game/systems/locomotion-navigation.md`. Foot-slide root cause already solved by
measuring the planted foot; this is the remaining feel work.
