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
| Images | RLE 2 Image | Yes | Yes, including the internal `RLE2 -> DXT5 DDS -> PNG` fallback when the upstream helper rejects `DXT5RLE2` payloads | PNG + Raw |
| Images | RLES Image | Yes | Yes, including the internal `RLES -> DXT5 DDS -> PNG` fallback when needed | PNG + Raw |
| 3D | Geometry | Yes | Yes, for the supported CAS skinned-part subset, including wrapped single-chunk `GEOM` resources carried inside containerized payloads | FBX + textures when selected through a supported CAS logical asset |
| 3D | Model | Yes | Yes, for the supported static Build/Buy subset | FBX + textures when selected through a supported Build/Buy logical asset |
| 3D | Model LOD | Yes | Yes, for the supported static Build/Buy subset | FBX + textures when selected through a supported Build/Buy logical asset |
| 3D | Rig | Yes | Used as supporting skeleton data for the supported CAS subset | Supporting data only; exported through the selected logical asset bundle |
| Build/Buy | Static furniture/decor objects with model-rooted triangle-list `ModelLOD` geometry, no skinning, and package-local material/texture candidates | Yes | Yes, with a real in-app 3D viewport when scene reconstruction succeeds | FBX + textures + manifest bundle |
| Build/Buy | Other Build/Buy objects | Yes | Diagnostics + metadata only | Raw/root export only |
| CAS | Adult/young-adult human parts, including accessories, when the `CASPart` exposes a direct skinned `Geometry` LOD either in the same package, through indexed cross-package lookup, or through direct `Geometry` keys in the `CASPart` TGI table when parsed LOD envelopes are absent | Yes, plus indexed swatch/preset variant rows from `CASPart` payloads and indexed slot/compatibility summaries (`Hair`, `Top`, `Bottom`, `Shoes`, `Full Body`, `Accessory`) | Yes, including the explicit variant picker / thumbnail slice driven by indexed `asset_variants` rows and swatch-aware approximate preview that uses indexed `diffuse` / `color_shift_mask` roles when available | FBX + textures + manifest bundle |
| CAS | Other CAS assets | Yes | Diagnostics + metadata only | Raw/root export only |
| Sim | Grouped `SimInfo` archetype roots with first-pass template switching and body proxy preview | Yes, including indexed species/age/gender/outfit/trait/skintone summaries in the logical asset catalog | Metadata/details plus a body-first archetype inspector for grouped template variations, base frame, skintone, concrete body-source references, body-assembly candidate families, an explicit current body-assembly recipe, explicit body-graph stages, a resolved base-body graph, body layers, morph stack, a first proxy body preview driven by exact body-part links plus compatible body-type-token and archetype-compatibility fallbacks, with per-family fallback when one chosen body candidate has a malformed GEOM payload; for human shell families that fallback now widens to include broader unisex nude/base shell candidates before settling on occult or clothing full-body rows. The same slice also exposes an explicit base-body assembly mode and per-layer `Active` / `Available` / `Blocked` state, centralized preview policy that can keep `Shoes` active above a body shell but will not render overlay-only fragments without a renderable body shell, explicit proxy-preview coverage, visible active preview layers, visible body-graph node states, visible resolved shell/overlay/alternate layers, and secondary human CAS slot-family pickers | Raw/root export only |
| Sim assembly | `SimData`, `SimPreset`, `CASPreset`, `RegionMap`, `SimModifier`, `BlendGeometry`, `DeformerMap`, `BoneDelta`, `BonePose`, `Skintone` | Yes, including raw-browser linkage hints that distinguish `Sim/character seed` rows from `Sim assembly component` rows; `CASPreset`, `RegionMap`, and `Skintone` also expose structured factual summaries in lazy raw-resource enrichment | Not yet; roadmap and raw inspection first | Raw only in the current pass |
| General 3D | Standalone package-local `Model`, `ModelLOD`, and `Geometry` roots that are not yet harmonized into Build/Buy or CAS identity graphs | Yes | Yes, through the shared `Model` / `ModelLOD` / `Geometry` scene path when a supported root is present | Raw/root export first; semantic bundle export is deferred until the data model is harmonized |
| Audio | RIFF/WAV payloads | Yes | Yes | WAV + Raw |
| Audio | Unsupported/unknown audio | Yes | Unsupported reason only | Raw |
| Animation | CLIP / animation | Yes | No | Raw only in v1 |
| Morphs | Blendshape/morph data | Yes | Not targeted | Raw only unless naturally preserved |

## Deferred

- full in-game Sim assembly
- logical `Sim` browsing beyond the current metadata-only grouped `SimInfo` archetype slice
- full variant/swatch/state-family harmonization across Build/Buy, CAS, and generalized 3D roots beyond the first indexed CAS variant layer
- Build/Buy support beyond the current static model-rooted furniture/decor subset
- CAS support beyond the current adult/young-adult human slice with direct `Geometry` roots or indexed cross-package `Geometry` resolution
- generalized Sims 4 scene reconstruction beyond the current Build/Buy, narrow CAS, and package-local General 3D slices
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
