# TS4 DX11 Context-Tagged Capture Recipes

This document turns the context-tag contract into the minimum runnable recipe set for the next helper-family capture batch.

Use it when the question is not:

- "what fields belong in `context-tags.json`?"

but:

- "what exact runner invocations should be used for the three next helper-family sessions?"

Related docs:

- [TS4 DX11 Context-Tagged Capture Contract](context-tagged-capture-contract.md)
- [TS4 Material Shader Spec](ts4-material-shader-spec.md)

## Why this exists

The contract already defines the required metadata.

The remaining operational gap was smaller but still real:

- which fields can be defaulted safely
- which fields still must be scene-specific
- what one honest command looks like for each helper-family target

The standard runner now accepts `-HelperPreset`, so the common helper-family fields do not need to be rebuilt manually every time.

## Preset rule

Current standard runner helper presets:

- `ShaderDayNight`
- `GeneratedLight`
- `ProjectionReveal`

Each preset fills these fields if the caller does not override them:

- `world_mode`
- `family_focus`
- `scene_class`
- `expected_candidate_clusters`

The caller must still provide scene-specific fields:

- `SceneLabel`
- `TargetAssetsOrEffects`
- `Notes`

Safe rule:

- keep one stable intentional scene per session
- prefer one focused target session over one broad wandering showcase
- compare against broad controls only after the tagged target session is clean
- for the standard helper-family compare flow, tag the control with the same helper-family focus and change the scene emphasis instead of dropping the tags

## Recipe 1: ShaderDayNight

Goal:

- test `F04-parameter-heavy` first
- keep `F05-color-aware` as the nearest sibling control inside the same family lane

Recommended command:

```powershell
powershell -ExecutionPolicy Bypass -File .\satellites\ts4-dx11-introspection\scripts\run-live-capture.ps1 `
  -HelperPreset ShaderDayNight `
  -SceneLabel "shader-daynight reveal-aware fixture" `
  -TargetAssetsOrEffects "reveal-aware object","light-reactive object" `
  -Notes "camera held on one reveal-aware or strongly light-reactive scene for the full bounded capture"
```

What the preset supplies:

- `WorldMode = LiveMode`
- `FamilyFocus = shader-daynight`
- `SceneClass = lighting-heavy,reveal-aware`
- `ExpectedCandidateClusters = F04-parameter-heavy,F05-color-aware`

Nearby control command:

```powershell
powershell -ExecutionPolicy Bypass -File .\satellites\ts4-dx11-introspection\scripts\run-live-capture.ps1 `
  -HelperPreset ShaderDayNight `
  -SceneLabel "shader-daynight nearby control" `
  -TargetAssetsOrEffects "nearby non-emphasized scene","same mode control" `
  -Notes "camera held on a nearby control scene in the same mode without the strongest reveal-aware emphasis"
```

## Recipe 2: GeneratedLight

Goal:

- test whether the narrowed `F03-maptex` packet concentrates in lighting-heavy indoor scenes

Recommended command:

```powershell
powershell -ExecutionPolicy Bypass -File .\satellites\ts4-dx11-introspection\scripts\run-live-capture.ps1 `
  -HelperPreset GeneratedLight `
  -SceneLabel "generated-light indoor room" `
  -TargetAssetsOrEffects "spot lights","visible room lighting" `
  -Notes "camera held on one indoor lit room with visible light contribution for the full bounded capture"
```

What the preset supplies:

- `WorldMode = LiveMode`
- `FamilyFocus = generated-light`
- `SceneClass = lighting-heavy,indoor-lit`
- `ExpectedCandidateClusters = F03-maptex`

Nearby control command:

```powershell
powershell -ExecutionPolicy Bypass -File .\satellites\ts4-dx11-introspection\scripts\run-live-capture.ps1 `
  -HelperPreset GeneratedLight `
  -SceneLabel "generated-light nearby control" `
  -TargetAssetsOrEffects "same room control","reduced light emphasis" `
  -Notes "camera held on a nearby control scene in the same mode without the strongest generated-light emphasis"
```

## Recipe 3: ProjectionReveal

Goal:

- test whether the narrowed `F04-srctex` packet concentrates in projection/reveal or nearby refraction-adjacent scenes

Recommended command:

```powershell
powershell -ExecutionPolicy Bypass -File .\satellites\ts4-dx11-introspection\scripts\run-live-capture.ps1 `
  -HelperPreset ProjectionReveal `
  -SceneLabel "projection-reveal target scene" `
  -TargetAssetsOrEffects "projection-heavy fixture","reveal-heavy fixture" `
  -Notes "camera held on one projection-heavy, reveal-aware, or refraction-adjacent fixture family for the full bounded capture"
```

What the preset supplies:

- `WorldMode = LiveMode`
- `FamilyFocus = projection-reveal`
- `SceneClass = projection-heavy,reveal-aware`
- `ExpectedCandidateClusters = F04-srctex`

Nearby control command:

```powershell
powershell -ExecutionPolicy Bypass -File .\satellites\ts4-dx11-introspection\scripts\run-live-capture.ps1 `
  -HelperPreset ProjectionReveal `
  -SceneLabel "projection-reveal nearby control" `
  -TargetAssetsOrEffects "same mode control","reduced projection emphasis" `
  -Notes "camera held on a nearby control scene in the same mode without the strongest projection or reveal emphasis"
```

## Minimum honest batch

The next useful helper-family batch is still small:

1. one `ShaderDayNight` session
2. one `GeneratedLight` session
3. one `ProjectionReveal` session

Optional next strengthening step:

1. rerun one nearby tagged control session for the strongest target above
2. compare the tagged target against the control using the existing compare helpers

## What this does not change

It does not:

- prove ownership by itself
- replace draw/pass/state closure
- remove the need for later manifest integration

It does:

- remove most of the mechanical setup burden from the next helper-family capture batch
- standardize how the three current weak rows should be gathered
- make the next runtime truth step start from one bounded recipe set instead of free-form manual tagging
