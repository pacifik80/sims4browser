---
id: TASK-007
title: World persistence — save / load a running lot
status: open
area: game
priority: medium
created: 2026-07-21
---
**Goal** — serialize and restore a session: the Sims (soul = identity, appearance, needs, mood,
relationships), the placed object layout, and the game clock — so a lot survives quitting.

**Acceptance** — Save then reload restores each Sim's look, needs, and relationships; the object layout
(types + positions); and the time of day. A reloaded lot continues living identically.

**Notes** — souls, `NeedValue`, and `RelationshipBook` are plain serializable C# already. Appearance =
the existing `CharacterDefinition`; reuse the character-template Capture/Apply for looks. Object layout
= per-object `{catalog name, position}` via `SmartObjectCatalog`. Foundations exist (template save/load);
this widens it to the whole world.
