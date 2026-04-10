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
| 3D | Geometry | Yes | Yes, for the supported CAS skinned-part subset | FBX + textures when selected through a supported CAS logical asset |
| 3D | Model | Yes | Yes, for the supported static Build/Buy subset | FBX + textures when selected through a supported Build/Buy logical asset |
| 3D | Model LOD | Yes | Yes, for the supported static Build/Buy subset | FBX + textures when selected through a supported Build/Buy logical asset |
| 3D | Rig | Yes | Used as supporting skeleton data for the supported CAS subset | Supporting data only; exported through the selected logical asset bundle |
| Build/Buy | Static furniture/decor objects with model-rooted triangle-list `ModelLOD` geometry, no skinning, and package-local material/texture candidates | Yes | Yes, with a real in-app 3D viewport when scene reconstruction succeeds | FBX + textures + manifest bundle |
| Build/Buy | Other Build/Buy objects | Yes | Diagnostics + metadata only | Raw/root export only |
| CAS | Adult/young-adult human hair, full body, top, bottom, and shoes parts with a direct skinned `Geometry` LOD in the same package | Yes | Yes | FBX + textures + manifest bundle |
| CAS | Other CAS assets | Yes | Diagnostics + metadata only | Raw/root export only |
| Audio | RIFF/WAV payloads | Yes | Yes | WAV + Raw |
| Audio | Unsupported/unknown audio | Yes | Unsupported reason only | Raw |
| Animation | CLIP / animation | Yes | No | Raw only in v1 |
| Morphs | Blendshape/morph data | Yes | Not targeted | Raw only unless naturally preserved |

## Deferred

- full in-game Sim assembly
- generalized Sims 4 scene reconstruction beyond the current Build/Buy and narrow CAS vertical slices
- Build/Buy support beyond the current static model-rooted furniture/decor subset
- CAS support beyond the current adult/young-adult human hair/full body/top/bottom/shoes subset with direct package-local `Geometry` roots
- animation export
- exact material/shader parity
- guaranteed morph export

## Cached capability fields

The cache stores cheap factual fields such as:

- has scene root
- has exact geometry candidate
- has material references
- has texture references

The app derives convenience labels and filter presets from those fields at runtime instead of persisting a support verdict in the cache.
