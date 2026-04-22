# TS4 DX11 Context-Tagged Capture Contract

This document defines the minimum capture-labeling contract needed to move the weak helper-family rows past the current broad-capture ceiling.

Use it when the question is not:

- "can we collect more DX11 runtime data at all?"

but:

- "what exact capture metadata is required so new sessions can separate `ShaderDayNight`, generated-light, and projection/reveal candidates honestly?"

Related docs:

- [TS4 DX11 Introspection](../README.md)
- [TS4 Material Shader Spec](ts4-material-shader-spec.md)
- [TS4 DX11 Context-Tagged Capture Recipes](context-tagged-capture-recipes.md)

## Why this exists

The current broad checked-in sessions are structurally useful:

- they preserve stable representative hashes
- they preserve stable reflection packets
- they prove that the narrowed helper-family candidates are real recurring runtime members

They do not currently carry enough scene/context metadata to answer the next question:

- which narrowed helper packet belongs to which scene class

That means the next real uplift is not "one more broad capture". The next real uplift is:

- one or more captures with explicit context tags

## Temporary rule

Until the native or companion manifests are extended, each new tagged capture session should include a sidecar file in the session directory:

- `context-tags.json`

This is the temporary truth layer for scene labels.

Safe rule:

- the sidecar may be written either:
  - directly by `run-live-capture.ps1` through its context-tag parameters
  - directly by `run-live-capture.ps1 -HelperPreset ...` plus scene-specific fields
  - or manually immediately after the run
- it must describe what was intentionally on screen during the capture
- it must not be reconstructed later from memory if the session is already ambiguous

## Required sidecar shape

Create `context-tags.json` inside the capture directory with this minimum shape:

```json
{
  "schema_version": 1,
  "session_id": "20260422-000000",
  "world_mode": "LiveMode",
  "scene_label": "generated-light indoor room",
  "family_focus": [
    "generated-light"
  ],
  "scene_class": [
    "lighting-heavy",
    "indoor-lit"
  ],
  "expected_candidate_clusters": [
    "F03-maptex"
  ],
  "target_assets_or_effects": [
    "spot lights",
    "visible room lighting"
  ],
  "notes": "camera held on indoor lit scene for the full bounded capture"
}
```

## JSON schema

The matching machine-readable schema lives at:

- [context-tag.schema.json](../schemas/context-tag.schema.json)

Use the schema as the stable field list for future manifest integration too.

## Required fields

- `schema_version`
  - currently `1`
- `session_id`
  - must match the capture directory name
- `world_mode`
  - one of:
    - `CAS`
    - `BuildBuy`
    - `LiveMode`
- `scene_label`
  - short human-readable identifier for the intentional scene
- `family_focus`
  - one or more of:
    - `shader-daynight`
    - `generated-light`
    - `projection-reveal`
- `scene_class`
  - one or more tags from:
    - `lighting-heavy`
    - `reveal-aware`
    - `refraction-adjacent`
    - `projection-heavy`
    - `indoor-lit`
    - `night-scene`
    - `day-scene`
- `expected_candidate_clusters`
  - one or more of:
    - `F03-maptex`
    - `F04-srctex`
    - `F04-parameter-heavy`
    - `F05-color-aware`
- `target_assets_or_effects`
  - plain-language list of the fixtures/effects intentionally kept on screen
- `notes`
  - one sentence describing what was held steady during the capture

## Capture recipes

The exact runnable command set now lives in:

- [TS4 DX11 Context-Tagged Capture Recipes](context-tagged-capture-recipes.md)

### 1. ShaderDayNight

Goal:

- separate the current `F04`-first route from the `F05` sibling

Required tags:

- `family_focus = ["shader-daynight"]`
- `scene_class` must include at least one of:
  - `lighting-heavy`
  - `reveal-aware`

Recommended expectations:

- `expected_candidate_clusters = ["F04-parameter-heavy", "F05-color-aware"]`

Minimum scene discipline:

- keep one reveal-aware or strongly light-reactive object family centered for the whole bounded run
- avoid mixing with obviously unrelated refraction or generated-light showcase scenes

### 2. Generated-light

Goal:

- test whether the narrowed `F03 maptex` packet really concentrates in lighting-heavy scenes

Required tags:

- `family_focus = ["generated-light"]`
- `scene_class` must include:
  - `lighting-heavy`
- plus one of:
  - `indoor-lit`
  - `night-scene`

Recommended expectations:

- `expected_candidate_clusters = ["F03-maptex"]`

Minimum scene discipline:

- keep visible room lighting or spot-light-heavy fixtures on screen for the whole bounded run
- do not reuse a generic broad wandering session

### 3. Projection/reveal

Goal:

- test whether the narrowed `F04 srctex` packet concentrates in projection/reveal or refraction-adjacent scenes

Required tags:

- `family_focus = ["projection-reveal"]`
- `scene_class` must include at least one of:
  - `projection-heavy`
  - `reveal-aware`
  - `refraction-adjacent`

Recommended expectations:

- `expected_candidate_clusters = ["F04-srctex"]`

Minimum scene discipline:

- keep one projection/reveal/refraction-adjacent fixture family centered for the whole bounded run
- avoid mixing it with generic lighting showcase scenes in the same session

## Minimum run set

The next honest helper-family uplift does not need a huge matrix.

Minimum useful set:

1. one `shader-daynight` tagged session
2. one `generated-light` tagged session
3. one `projection-reveal` tagged session

Preferred set:

1. one target session
2. one nearby control session

Example:

- one lighting-heavy indoor generated-light session
- one nearby indoor session without the same generated-light emphasis

## Success criteria

A new tagged session is useful only if all of these are true:

1. the session has `context-tags.json`
2. the sidecar fields are unambiguous
3. the scene stayed stable for the bounded capture window
4. the session can be compared against at least one other tagged or broad control session

A helper-family claim can be promoted only if a narrowed packet shows a real contextual shift, for example:

- present in the target tagged session and absent from the control
- or materially enriched in the target tagged session versus controls

## What this contract does not do

It does not:

- prove family ownership by itself
- replace draw/state-level capture work
- remove the need for later manifest integration

It does:

- make the next capture batch actionable immediately
- prevent more broad unlabeled sessions from being mistaken for progress
- create a stable bridge from the current blocker packets to the next honest data-gathering step
