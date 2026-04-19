# Workflow: Exporting a Build/Buy Object

Related docs:

- [Knowledge map](../knowledge-map.md)
- [Shared TS4 Material, Texture, And UV Pipeline](../shared-ts4-material-texture-pipeline.md)
- [Workflows index](README.md)

## Current workflow

1. Click `Update Index`, add one or more folders, and run indexing.
3. Switch to `Asset Browser` and set `Domain` to `Build/Buy`.
4. Search or filter for the object you want. Capability filters are cheap cache hints, not a final scene-readiness verdict.
5. Select the asset and wait for graph resolution plus 3D preview. If scene reconstruction fails, the details pane stays diagnostic-first and the preview can fall back to image/diagnostics.
6. When `Export Asset` becomes enabled, choose an output folder. The app writes an FBX bundle with decoded textures plus `manifest.json`, `metadata.json`, and `material_manifest.json`.
7. If the asset is outside the supported subset, use `Raw Resource Browser` and `Raw Export` for the root or linked resources instead.

## Current supported subset

- static Build/Buy furniture/decor objects with a resolved `Model` root
- triangle-list `ModelLOD` geometry
- no skinning or animation path
- package-local material/texture candidates or explicit material chunks that the current builder can carry into the portable bundle

## Current limitations

- Build/Buy scene support is intentionally narrow; many objects still stop at diagnostics plus raw export.
- Cross-package and stateful object resolution remain best-effort rather than universal.
- Material/shader mapping is approximation-first and will not always match the in-game renderer exactly.

When the question is about shared material/shader interpretation rather than the export procedure itself, use [Shared TS4 Material, Texture, And UV Pipeline](../shared-ts4-material-texture-pipeline.md).
