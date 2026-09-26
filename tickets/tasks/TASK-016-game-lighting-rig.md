---
id: TASK-016
title: Game scene lighting — sky ambient + exposure rig + bounce (fix black shadows)
status: in-progress
area: game
priority: high
created: 2026-07-22
---
**BUILT 2026-07-22 (awaiting user verify + tuning)** — user approved 1+2, and directed that the artistic
knobs (3) and grade (4) become DEBUG-MENU RENDER OPTIONS first, baked only after live tuning. Done:
game-scene rig (PBR sky ambient + groundTint, FIXED exposure via SkyTimeController incl. new
`exposureOffset`, moon, sun shadowDimmer 0.8, sun interactsWithSky), SSGI on by default (HDRP-asset
supportSSGI auto-enabled by the scene builder), shadowless fill light (default OFF), and volume
overrides for IndirectLightingController/Tonemapping(Neutral)/ShadowsMidtonesHighlights. All live-tunable
in the debug menu's RENDER section (`RenderDebugController`); "Log tuning values" prints the
`[RenderTuning]` bake handoff. **Remaining:** user tunes → bake chosen values as scene-builder defaults;
revisit bounce (SSGI quality vs APV) when TASK-011 interiors land; optional final grade pass.

**Night fix 2026-07-22** — user: "why is the moon brighter than the sun?" Three stacked causes: moon
exaggerated 100× (25 lux, home-editor inheritance), night EV floor 3 over-exposing it, and the PBR sky
Rayleigh-scattering the boosted moonlight into a day-blue night sky + blazing disk. Fix: `moonMaxLux` +
`nightEv` are now SkyTimeController fields (defaults 25/3 preserve the home editor), the GAME scene
starts at 6 lux / EV 3 (~9 % of day brightness) + moon `surfaceTint`, and both are debug-menu sliders
("Moon max lux", "Night exposure EV") in the RENDER section — tune, then bake.
**Goal** — user: "extremely high unrealistic contrast between lighted and shadowed areas." Cause: the M0
game scene has NO sky/ambient/exposure rig (only a sun + HDShadowSettings volume — `GameSceneBuilder`)
and no indirect bounce, so shadows are lit by nearly nothing and HDRP auto-exposure meters for the sunlit
ground. The home-editor scene already has the full proven rig (`HomeEditorSceneBuilder`): PhysicallyBased
Sky + groundTint fix, FIXED exposure driven per time-of-day by SkyTimeController, sun shadowDimmer 0.8,
moon for nights.

**Plan (recommended)**
1. Port the home-editor sky + fixed-exposure rig into the game scene builder (sky ambient fills shadows,
   deterministic day/night exposure, moon).
2. Add **SSGI** as a quality-toggleable volume override (no baking — survives build-mode edits and the
   moving sun); judge on target GPU.
3. Later (with TASK-011 walls/interiors): revisit bounce — APV bake vs SSGI quality; finishing grade
   (tonemap + gentle shadow lift, like the CAS volume treatment).

**Alternatives considered** — Adaptive Probe Volumes (bake step; stale after build-mode wall edits),
ray-traced GI (needs RTX + frame budget — overkill now), shadowless fill light / Indirect Lighting
Controller multiplier (fast artistic fallbacks, not physical).

**Acceptance** — outdoor shadows read as shadow (blue-ish, detailed), not black holes; look stays stable
across game speeds and time-of-day; night still reads dark.
