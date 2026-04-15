# Workflow: Exporting a CAS Asset

## Current workflow

1. Add Game/DLC/Mods folders manually.
2. Run indexing.
3. Switch to `Asset Browser` and set `Domain` to `CAS`.
4. Search or filter for the CAS part you want. The current reliable subset is adult/young-adult human hair, full body, top, bottom, and shoes.
5. Select the asset and wait for graph resolution plus skinned-scene preview. If reconstruction fails, the app stays diagnostic-first and can fall back to image/diagnostic preview.
6. When `Export Asset` becomes enabled, choose an output folder. The app writes an FBX bundle with decoded textures plus `manifest.json`, `metadata.json`, and `material_manifest.json`.
7. If the asset is outside the supported subset, use `Raw Resource Browser` and `Raw Export` for the root or linked resources instead.

## Current supported subset

- adult/young-adult human CAS hair, full body, top, bottom, and shoes parts
- a direct package-local skinned `Geometry` LOD exposed by the `CASPart`
- optional exact-instance `Rig` support and package-local texture candidates when the current resolver can confirm them

## Current limitations

- The app does not assemble full Sims; export stays scoped to the selected CAS part.
- Broader CAS categories such as children, pets, occult-specific overrides, accessory edge cases, and generalized container/list-based geometry paths still fall back to diagnostics plus raw export.
- Material/shader mapping remains best-effort rather than exact in-game parity.
