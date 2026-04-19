# Live-Proof Packets

This folder holds concrete inspection packets for the highest-priority live-proof targets.

If this track is being resumed in a new chat, read [Research Restart Guide](../research-restart-guide.md) first.

Use these when the work has already moved past:

- broad family identity
- edge-family separation
- candidate-target selection
- base object/material authority, which now lives in [Build/Buy Material Authority Matrix](../buildbuy-material-authority-matrix.md) for object-side packets

At this layer, each document should answer:

1. what is already externally proved
2. what local candidate target is being inspected next
3. what exact claim the packet is trying to prove or falsify
4. what current implementation mistake would become easier to diagnose after that proof

Safe reading:

- these packets use concrete assets as evidence
- they do not define asset-bound shader systems
- they exist to prove authoritative inputs and shared family semantics

## Current Packets

- [SimGlass Versus Shell Baseline](simglass-vs-shell-baseline.md)
- [SimSkin Versus SimSkinMask](simskin-vs-simskinmask.md)
- [CASHotSpotAtlas Carry-Through](cas-hotspotatlas-carry-through.md)
- [ShaderDayNightParameters Visible-Pass Proof](shader-daynight-visible-pass.md)
- [GenerateSpotLightmap And NextFloorLightMapXform](generate-spotlightmap-nextfloorlightmapxform.md)
- [RefractionMap Live Proof](refractionmap-live-proof.md)

Current strongest named fixture:

- `RefractionMap` now has one named Build/Buy bridge root anchored to `EP10\\ClientFullBuild0.package | sculptFountainSurface3x3_EP10GENlilyPad -> 01661233:00000000:00F643B0FDD2F1F7`

Current strongest narrowed `SimGlass` search route:

- do not resume from the earlier `EP10` window-heavy packet first
- resume from the broader transparent-decor cluster instead:
  - `fishBowl_EP10GENmarimo`
  - `shelfFloor2x1_EP10TEAdisplayShelf`
  - `shelfFloor2x1_EP10TEAshopDisplayTileable`
  - `lightWall_EP10GENlantern`
  - `mirrorWall1x1_EP10BATHsunrise`
- these are survey-backed candidate anchors, not yet stable reopenable fixtures
