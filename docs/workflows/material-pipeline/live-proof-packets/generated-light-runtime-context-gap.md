# Generated-Light Runtime Context Gap

This packet records the current ceiling of the checked-in DX11 runtime captures for the narrowed generated-light helper branch.

Question:

- do the current checked-in broad captures already separate the leading `F03` `maptex + tex` packet by scene or context strongly enough to promote a context-bound generated-light family reading?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [GenerateSpotLightmap And NextFloorLightMapXform](../family-sheets/generate-spotlightmap-nextfloorlightmapxform.md)
- [Generated-Light Runtime Cluster Candidate Floor](generated-light-runtime-cluster-candidate-floor.md)
- [Runtime Helper-Family Clustering Floor](../runtime-helper-family-clustering-floor.md)
- [group-compare-broad-vs-20260421-220041.md](../../../satellites/ts4-dx11-introspection/captures/live/reports/group-compare-broad-vs-20260421-220041.md)
- [generated light runtime context gap snapshot](../../../tmp/generated_light_runtime_context_gap_snapshot_2026-04-22.json)

## Scope status (`v0.1`)

```text
Generated-Light Runtime Context Gap
├─ Externally proved family identity ~ 74%
├─ Broad-capture persistence floor ~ 84%
├─ maptex packet parity ceiling ~ 82%
├─ Context-tagged capture availability ~ 31%
└─ Exact scene-bound ownership ~ 12%
```

## What this packet is for

The previous runtime-cluster packet already narrowed the generated-light helper branch from:

- a broad `F03/F04/F05` helper bucket

to:

- one stable `F03` `maptex + tex` packet

This packet answers the next narrower question:

- whether the current checked-in broad captures already contain enough scene/context separation to go farther without another tagged capture run

## Local snapshot of external tooling

Current bounded snapshot:

- `tmp/generated_light_runtime_context_gap_snapshot_2026-04-22.json`

Broad comparison captures checked here:

- `20260421-212139`
- `20260421-212533`
- `20260421-220041`

Useful checked-in comparison layer:

- [compare-20260421-212533-vs-20260421-220041.md](../../../satellites/ts4-dx11-introspection/captures/live/reports/compare-20260421-212533-vs-20260421-220041.md)
- [group-compare-broad-vs-20260421-220041.md](../../../satellites/ts4-dx11-introspection/captures/live/reports/group-compare-broad-vs-20260421-220041.md)

## What the current broad captures do prove

Representative `F03` `maptex + tex` hashes persist across all three checked broad captures:

- `9821193ee6bb5acb80457ebb30966a4da0978bb9ad1312ad9a6b2bf31f007cf1`
- `0d5a27655d43f10a7a98be014cb73741ff6c3139e45273e1ed93183b5e775287`
- `23d9bec47a49aa01dd054ee3d5e37983dc620b474052887a6c8018e48169eb52`

The repeated packet is structurally stable too:

- resources:
  - `sampler_maptex`
  - `sampler_tex`
  - `maptex`
  - `tex`
  - `Constants`
- shared variables:
  - `compx`
  - `compy`
  - `mapScale`
  - `scale`
- richer member:
  - `boundColor`
- shared inputs:
  - `TEXCOORD0`

Safe reading:

- the narrowed `maptex` packet is a real recurring part of the current broad runtime surface
- the current broad sessions preserve candidate stability
- the current broad sessions still do not create a capture-level split between this packet and one specific lighting-heavy scene class

## What the current broad captures do not prove

Current manifests still expose runtime/session metadata only:

- session id
- timestamps
- binary paths
- frame count

They do not currently carry:

- scene labels
- capture-purpose labels
- explicit lighting-heavy tags
- generated-light versus non-generated-light capture context

Safe reading:

- the checked-in broad capture corpus is good enough to freeze the current ceiling
- it is not yet context-tagged enough to bind the stable `maptex` packet to one generated-light scene class

## Why this matters

Without this packet, the next move still looks like:

- inspect more of the same broad captures

With this packet, the next move is narrower and more honest:

- stop expecting broad untagged sessions to close scene-specific ownership
- keep the current `F03` `maptex + tex` narrowing
- require one context-tagged lighting-heavy capture before stronger promotion

## Best next inspection step

1. Keep [Generated-Light Runtime Cluster Candidate Floor](generated-light-runtime-cluster-candidate-floor.md) as the narrowed runtime bridge.
2. Keep [GenerateSpotLightmap And NextFloorLightMapXform](../family-sheets/generate-spotlightmap-nextfloorlightmapxform.md) as the generated-light identity boundary.
3. Run one lighting-heavy context-tagged capture and check the stable `F03` packet before widening into broader helper families.

## Honest limit

This packet does not yet prove:

- that the stable `maptex` packet is definitively `GenerateSpotLightmap`
- that it is definitively `NextFloorLightMapXform`
- exact matrix semantics for any generated-light helper
- exact draw/pass ownership

What it does prove:

- the checked-in broad runtime corpus already has a real ceiling for this branch
- that ceiling is not lack of runtime data in general
- that ceiling is lack of scene/context tagging needed to promote the narrowed `F03` `maptex + tex` packet into stronger family ownership
