---
id: TASK-010
title: Distinct social interactions with varied outcomes
status: open
area: game
priority: low
created: 2026-07-21
---
**Goal** — replace the single always-positive "chat" with multiple social interactions (chat / joke /
flirt / argue) whose relationship outcomes depend on traits and mood, so social feels alive rather than
a uniform meter fill.

**Acceptance** — Sims choose among social interactions; outcomes vary (some raise, some lower the
relationship, some depend on compatibility/mood); the change shows in the `RelationshipBook` value and a
short reaction/emote.

**Notes** — the advertisement model + two-party handshake already support this (M3). Reactions can use
the emote icons (needs `angry`/`surprised` — TASK-005). Currently `SimAgent.BeginSocial` applies one
fixed positive interaction.
