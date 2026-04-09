# Known Limitations

- v1 does not attempt full Sim assembly from CAS parts.
- Animation and CLIP export are out of scope.
- Real 3D preview/export is currently limited to two narrow subsets: static Build/Buy model-rooted furniture/decor objects, and adult/young-adult human CAS hair/full body/top/bottom/shoes parts whose CAS part exposes a direct package-local skinned `Geometry` LOD.
- Generalized Geometry-root browsing, GEOM-list/container CAS resolution, occult/pet/child CAS coverage, assembled character preview, and broader Build/Buy dependency resolution remain unsupported.
- Logical Build/Buy asset resolution is still package-local and best-effort rather than a fully reverse-engineered cross-package dependency graph.
- CAS logical asset resolution is also still package-local and only supports direct `CASPart -> Geometry` links in the current pass.
- Audio playback/export currently supports RIFF/WAV payloads only.
- Material/shader mapping for FBX export is best-effort and may omit Sims-specific semantics.
- Unsupported preview/export paths degrade to metadata plus raw export.
