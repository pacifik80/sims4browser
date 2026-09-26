---
id: BUG-003
title: Long-sleeve tops (Shirt, SweaterCrewBasic) render glitches
status: open
area: clothing
severity: major
created: 2026-07-13
---
**Context** — Shirt shows black square patches on the torso/sleeves; Sweater shows partially invisible
sleeves. Not a texture/composite problem — a Python replay of the skin/fabric `lerp` over the skin
atlas is clean (0% pure black).

**Repro** — dress a Sim in Shirt or SweaterCrewBasic (long sleeves) → artifacts on the sleeve tubes.

**Expected** — clean sleeves, like the short-sleeve tee from the same pipeline.

**Notes** — Unity-side mesh issue on the sleeve tubes; suspect bad normals or double-sided back-face
z-fighting. Next: inspect the two meshes (recompute normals / try single-sided material / check
winding). Migrated from `unity/Sims4Creator/BACKLOG.md` (Clothing #1).
