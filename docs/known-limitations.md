# Known Limitations

- v1 does not attempt full Sim assembly from CAS parts.
- Animation and CLIP export are out of scope.
- The current build does not yet reconstruct real Sims 4 Geometry/Model/ModelLOD/Rig resources into a 3D viewport scene.
- FBX export is implemented for the canonical internal scene model and covered by tests, but not yet wired to real Sims 4 mesh parsing.
- Logical asset browsing is heuristic and currently uses package-local grouping rather than a fully reverse-engineered dependency graph.
- Audio playback/export currently supports RIFF/WAV payloads only.
- Material/shader mapping for FBX export is best-effort and may omit Sims-specific semantics.
- Unsupported preview/export paths degrade to metadata plus raw export.
