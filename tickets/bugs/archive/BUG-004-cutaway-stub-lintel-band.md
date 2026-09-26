---
id: BUG-004
title: Cutaway stub shows a band across door/window openings
status: done
area: home-editor
severity: minor
created: 2026-07-13
---
**Context** — when a wall is cut down for viewing, its short stub had a horizontal polygon spanning
each door/window opening instead of keeping the opening as a real gap.

**Repro** — build a wall with a door or window, orbit so it cuts down (cutaway) → a band appears
across the opening in the low curb.

**Expected** — the opening stays a full gap in the stub (doors), or the curb is solid where a window
sill sits below the stub height.

**Notes** — root cause: `StubFor` built the stub by CLAMPING every vertex down to `StubHeight`, so a
door/window LINTEL (a box at ~2.1–2.8 m) collapsed onto the stub top as a flat band.

**Fixed 2026-07-13** (compiles clean; awaiting Play-test — Unity was open so the headless stub render
skipped): the stub is now re-generated CLIPPED at `StubHeight` instead of clamped. Added a `clipTop`
param to `BuildWallBoxesMesh` (default +inf = no clip, so the full mesh is untouched — zero
regression); it drops boxes/traps entirely above the clip (the lintel) and caps crossing boxes/wedges
at the clip height (each self-caps). `StubFor` → `BuildSegMesh(seg, key, StubHeight)` (new dispatcher,
opening vs solid), threading `clipTop` through `BuildSolidSegMesh`/`BuildOpeningSegMesh`/
`BuildCutoutSegMesh`. Door stub → open gap; window stub → solid curb (sill below stub); miters
preserved (wedges cap at stub height).
**Verify (Play):** cut a wall with a door → open gap, no band; a window → solid low curb.

**Resolution** — user Play-test confirmed working 2026-07-13.
