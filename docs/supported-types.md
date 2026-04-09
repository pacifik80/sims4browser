# Supported Types

## v1 support policy

The browser always supports metadata inspection and raw export for every discovered resource. Semantic preview/export support is intentionally narrower.

## Resource support matrix

| Area | Resource types / concepts | Browse metadata | Semantic preview | Export |
|---|---|---:|---:|---:|
| Raw binary | Any unknown resource | Yes | Hex fallback | Raw |
| Text | XML and text-like payloads | Yes | Yes | Raw |
| Images | PNG Image | Yes | Yes | PNG + Raw |
| Images | DST Image | Yes | Yes, when decoded | PNG + Raw |
| Images | LRLE Image | Yes | Yes, when decoded | PNG + Raw |
| Images | RLE 2 Image | Yes | Yes, when decoded | PNG + Raw |
| Images | RLES Image | Yes | Yes, when decoded | PNG + Raw |
| 3D | Geometry | Yes | Diagnostics only in current build | Canonical-scene FBX pipeline exists, Sims reconstruction not yet implemented |
| 3D | Model | Yes | Diagnostics only in current build | Canonical-scene FBX pipeline exists, Sims reconstruction not yet implemented |
| 3D | Model LOD | Yes | Diagnostics only in current build | Canonical-scene FBX pipeline exists, Sims reconstruction not yet implemented |
| 3D | Rig | Yes | Diagnostics only in current build | Canonical-scene FBX pipeline exists, Sims reconstruction not yet implemented |
| Build/Buy | Object catalog/object definitions with linked model data | Yes | Heuristic asset summary only | Root/raw export; full asset bundle deferred |
| CAS | CAS part with linked geometry/rig/textures | Yes | Heuristic asset summary only | Root/raw export; full asset bundle deferred |
| Audio | RIFF/WAV payloads | Yes | Yes | WAV + Raw |
| Audio | Unsupported/unknown audio | Yes | Unsupported reason only | Raw |
| Animation | CLIP / animation | Yes | No | Raw only in v1 |
| Morphs | Blendshape/morph data | Yes | Not targeted | Raw only unless naturally preserved |

## Deferred

- full in-game Sim assembly
- real Sims 4 scene reconstruction from Geometry/Model/ModelLOD/Rig resources
- asset-bundle FBX export from actual Sims 4 assets
- animation export
- exact material/shader parity
- guaranteed morph export
