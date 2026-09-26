---
id: TASK-001
title: Cutaway walls keep their full-height shadow (Light != Render)
status: done
area: home-editor
priority: high
created: 2026-07-13
---
**Goal** — when a wall is lowered for viewing (cutaway), the room stays lit and shadowed exactly as if
the wall were full height. Lowering is a RENDER-only change; Light must not change with it.

**Acceptance** — cutting a foreground wall does not brighten the room or shorten its shadow; a placed
lamp stays contained. Verified by render at a fixed sun (wall up vs wall cut → same interior shadow).

**Notes** — implements engine-requirements **R3-Light** (`docs/home-editor-engine.md`): render the
lowered stub with `shadowCastingMode = Off`, and keep a full-height invisible `ShadowsOnly` "ghost" of
the wall so the shadow footprint is unchanged. The collider already stays full-height (Sim form). This
is the first concrete piece of the render/light/sim separation.

**Implemented 2026-07-13** (compiles clean; awaiting Play-test verification — runtime/interactive, not
headless-renderable). `HomeEditor.SetWallLowered(seg, key, lowered)`: lowered → stub mesh +
`shadowCastingMode.Off` + a `__wallShadow` child (full `customMesh`, `ShadowsOnly`, wall material);
raised → full mesh + `shadowCastingMode.On` + ghost hidden. Both `RefreshCutaway` (camera orbit) and
`RefreshSegmentMesh` (mesh rebuild) route through it, so the ghost tracks openings/miters. Ghost is a
child of `seg.go` → destroyed with the wall automatically.
**Verify (Play):** draw a closed room + place a floor lamp inside; orbit so a foreground wall cuts
down → the interior shadow/darkness should NOT change vs the wall up (before this fix, cutting a wall
shortened its shadow to the ~0.4 m stub and let light flood in).

**Resolution** — user Play-test confirmed working 2026-07-13.
