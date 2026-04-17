# Known Limitations

- v1 does not attempt full Sim assembly from CAS parts.
- v1 now exposes a first logical `Sim` browser domain rooted in grouped `SimInfo` rows, and those archetypes can switch between grouped `SimInfo` templates plus drive a proxy body preview from resolved body-part CAS assets; however, the app still does not assemble full bodies, outfits, occult forms, or exportable character scenes from real Sim-domain data, and any `SimInfo` row that still cannot be classified into an archetype is intentionally hidden from the top-level list until parser coverage improves.
- the current `Sim` body preview is still a heuristic proxy path. It may select the wrong shell, wrong species body, or wrong apparel-like layer because it still relies on compatibility/fallback candidate search instead of a fully authoritative outfit/body-part assembly path from the selected template.
- Animation and CLIP export are out of scope.
- Real 3D preview/export is currently limited to two narrow subsets: static Build/Buy model-rooted furniture/decor objects, and adult/young-adult human CAS hair/full body/top/bottom/shoes parts whose CAS part exposes a direct package-local skinned `Geometry` LOD.
- Build/Buy logical assets that only resolve partially still appear in the browser, but they may fall back to diagnostics if no triangle meshes can be reconstructed.
- The cache stores only cheap factual capability fields. Previewability/exportability labels are derived in the app at runtime, so some assets may still look promising in filters and fail later during actual scene reconstruction.
- Generalized Geometry-root browsing, GEOM-list/container CAS resolution, occult/pet/child CAS coverage, assembled character preview, and broader Build/Buy dependency resolution remain unsupported.
- Sim/character resources are now labeled more explicitly in the raw browser, `CASPreset` / `RegionMap` / `Skintone` expose structured factual summaries, and `SimInfo` now drives a logical Sim archetype slice with a first proxy body preview, but that slice is still discovery scaffolding rather than real assembled-character support. In particular, the app does not yet follow the same authoritative character-assembly path used by mature modding tools that start from actual outfit/body-type selections, rig choice, and modifier/deformer application.
- Logical Build/Buy asset resolution is still package-local and best-effort rather than a fully reverse-engineered cross-package dependency graph.
- CAS logical asset resolution is still partial: it now covers direct `CASPart -> Geometry` links plus indexed cross-package `Geometry` fallback, but it does not yet assemble the broader character/body graph.
- Audio playback/export currently supports RIFF/WAV payloads only.
- Material/shader mapping for FBX export is best-effort and may omit Sims-specific semantics.
- Unsupported preview/export paths degrade to metadata plus raw export.
