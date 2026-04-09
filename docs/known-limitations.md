# Known Limitations

- v1 does not attempt full Sim assembly from CAS parts.
- Animation and CLIP export are out of scope.
- Real 3D preview/export is currently limited to a narrow Build/Buy subset: static model-rooted furniture/decor objects with triangle-list `ModelLOD` geometry, no skinning, and package-local material/texture candidates.
- Geometry roots, rigs, animated/skinned content, moving parts, and generalized Build/Buy dependency resolution remain unsupported.
- Logical Build/Buy asset resolution is still package-local and best-effort rather than a fully reverse-engineered cross-package dependency graph.
- Audio playback/export currently supports RIFF/WAV payloads only.
- Material/shader mapping for FBX export is best-effort and may omit Sims-specific semantics.
- Unsupported preview/export paths degrade to metadata plus raw export.
