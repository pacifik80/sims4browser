# Family Sheets

This folder holds narrow external-first evidence sheets for specific TS4 material or shader families.

Use these sheets when the broad registry is already too high-level and the task needs one bounded family packet with explicit evidence order, safe readings, and open gaps.

Primary rule:

- external references and creator tooling come first
- local snapshots of external tooling come second
- current repo code is recorded only as implementation boundary

## Current Sheets

- [SimSkin, SimGlass, And SimSkinMask](simskin-simglass-simskinmask.md)
- [CASHotSpotAtlas](cas-hotspot-atlas.md)
- [Projection, Reveal, And Lightmap Families](projection-reveal-lightmap.md)
- [ShaderDayNightParameters](shader-daynight-parameters.md)
- [GenerateSpotLightmap And NextFloorLightMapXform](generate-spotlightmap-nextfloorlightmapxform.md)

## Intended Growth Rule

Add a new family sheet only when the topic is narrow enough that:

- it has its own external evidence packet
- it has its own failure mode or authority seam
- keeping it inside `shader-family-registry.md` would make that file less clear
