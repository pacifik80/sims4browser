# Generated-Light Runtime Cluster Candidate Floor

This packet narrows the runtime helper-family route for `GenerateSpotLightmap` and `NextFloorLightMapXform`.

Question:

- does the checked-in DX11 runtime corpus already narrow the generated-light helper route enough to prefer one seeded runtime cluster before context-tagged lighting captures begin?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Runtime Helper-Family Clustering Floor](../runtime-helper-family-clustering-floor.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [Projection, Reveal, And Lightmap Families](../family-sheets/projection-reveal-lightmap.md)
- [GenerateSpotLightmap And NextFloorLightMapXform](../family-sheets/generate-spotlightmap-nextfloorlightmapxform.md)
- [GenerateSpotLightmap And NextFloorLightMapXform](generate-spotlightmap-nextfloorlightmapxform.md)
- [generated light runtime cluster snapshot](../../../tmp/generated_light_runtime_cluster_candidates_2026-04-22.json)

## Scope status (`v0.1`)

```text
Generated-Light Runtime Cluster Candidate Floor
├─ Externally proved family identity ~ 74%
├─ Runtime cluster narrowing ~ 78%
├─ F03 maptex packet floor ~ 83%
├─ Exact family ownership ~ 24%
└─ Exact scene/draw mapping ~ 12%
```

## External identity baseline

What is already strong enough:

- [Sims 4 lighting in Sims 3?](https://modthesims.info/showthread.php?t=646135) still groups `GenerateSpotLightmap`, `GenerateWindowLightmap`, `GenerateRectAreaLightmap`, `GenerateTubeLightmap`, and `NextFloorLightMapXform` inside one TS4 lightmap-oriented vocabulary
- the names themselves still signal generated-light or transform/helper semantics rather than ordinary surface slots

Safe reading:

- the generated-light family identity is already externally anchored
- runtime clustering is only narrowing the best next local route

## Local runtime cluster floor

The checked-in runtime corpus now freezes one stronger generated-light-side candidate floor:

- `tmp/generated_light_runtime_cluster_candidates_2026-04-22.json`

Current strongest runtime family:

- `F03 = Single-Texcoord Pixel`

Representative stable hashes:

- `9821193ee6bb5acb80457ebb30966a4da0978bb9ad1312ad9a6b2bf31f007cf1`
- `0d5a27655d43f10a7a98be014cb73741ff6c3139e45273e1ed93183b5e775287`
- `23d9bec47a49aa01dd054ee3d5e37983dc620b474052887a6c8018e48169eb52`

Each of these survives in all three checked broad captures.

## Why `F03` is the stronger current candidate

Useful direct runtime signal:

- all three stable representatives are single-texcoord pixel shaders
- all three expose the same map/helper-style resource packet:
  - `sampler_maptex`
  - `sampler_tex`
  - `maptex`
  - `tex`
  - `Constants`
- all three expose the same transform-like variable packet:
  - `compx`
  - `compy`
  - `mapScale`
  - `scale`
- one richer representative also adds:
  - `boundColor`

Safe reading:

- `F03` is now the strongest current runtime home for the tiny `maptex` branch
- that makes it the strongest current generated-light or projection-map helper candidate floor
- this is a stronger next route than starting from the broader `F04/F05` material/combine families

## What this changes for generated-light work

Before this packet, the honest runtime move was:

- keep `F03`, `F04`, and `F05` in one broad helper-family bucket

After this packet, the honest runtime move is narrower:

- start generated-light follow-up from `F03`
- keep `F04` as the nearest broader parameter-heavy helper/combine comparator
- only widen farther if `F03` fails under context-tagged lighting capture

## Honest limit

This packet does not yet prove:

- that `F03` is definitively `GenerateSpotLightmap`
- that `F03` is definitively `NextFloorLightMapXform`
- exact matrix semantics for any generated-light helper
- exact scene/draw ownership

What it does prove:

- the runtime helper-family route for generated-light is no longer one undifferentiated `F03/F04/F05` bucket
- `F03` is now the strongest current runtime cluster candidate floor for generated-light follow-up
