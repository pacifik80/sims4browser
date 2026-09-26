---
id: BUG-001
title: Table lamp has no glowing bulb
status: open
area: home-editor
severity: minor
created: 2026-07-13
---
**Context** — `SetupLamp` splits a lamp mesh into body / shade / bulb by height + radius from the
central axis. For `table_lamp` the bulb bucket came out empty (`bulb 0 tris`), so it has no emissive
source; it is lit only by the translucent shade + point light.

**Repro** — Build Home Editor Scene, place a Table Lamp, dim the scene → the shade lights but there is
no distinct glowing bulb like the floor lamp has.

**Expected** — a visible glowing bulb inside the shade, as on `lamp_floor`.

**Notes** — `table_lamp` is small (maxR 0.13, split y 0.27); the "high AND narrow" bulb test finds no
triangles. Tune the bulb heuristic for small lamps (relative thresholds, or fall back to the top-N
highest triangles near the axis). See `SetupLamp` in `HomeEditorSceneBuilder.cs`.
