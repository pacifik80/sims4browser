# Projection-Reveal Runtime Cluster Candidate Floor

This packet narrows the remaining projection/reveal helper branch inside the broad runtime helper-family corpus.

Question:

- after `ShaderDayNightParameters` narrowed to one `F04`-first route and generated-light narrowed to `F03`, does the checked-in DX11 runtime corpus already expose one stronger candidate floor for the remaining projection/reveal helper branch?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Runtime Helper-Family Clustering Floor](../runtime-helper-family-clustering-floor.md)
- [Projection, Reveal, And Lightmap Families](../family-sheets/projection-reveal-lightmap.md)
- [Projection, Reveal, And Generated-Light Boundary](../projection-reveal-generated-light-boundary.md)
- [ShaderDayNight Runtime Cluster Candidate Floor](shader-daynight-runtime-cluster-candidate-floor.md)
- [Generated-Light Runtime Cluster Candidate Floor](generated-light-runtime-cluster-candidate-floor.md)
- [projection reveal runtime cluster snapshot](../../../tmp/projection_reveal_runtime_cluster_candidates_2026-04-22.json)

## Scope status (`v0.1`)

```text
Projection-Reveal Runtime Cluster Candidate Floor
├─ Umbrella branch separation ~ 76%
├─ Runtime cluster narrowing ~ 79%
├─ srctex helper packet floor ~ 85%
├─ Exact family ownership ~ 24%
└─ Exact scene/draw mapping ~ 12%
```

## What this packet is for

The runtime helper-family route is no longer one flat bucket:

- generated-light now starts from `F03`
- `ShaderDayNightParameters` now starts from `F04`

This packet addresses the remaining middle question:

- whether the surviving `srctex`-style `F04` packet is already the strongest current runtime bridge for the broader projection/reveal branch

## Local runtime cluster floor

Current bounded snapshot:

- `tmp/projection_reveal_runtime_cluster_candidates_2026-04-22.json`

Current strongest runtime family:

- `F04 = Three-Texcoord Pixel`

Representative stable hashes:

- `fb1c5cb93d1af69d2056dc812eac97efd231f177a6c6691f106622fe7afcc45d`
- `5dda2c6adf1652635de15dd38357b633cec3fc009f91e3950fb8fe205ec2074f`
- `dfc13424296e6d0b825afca3ace4d3669239c2254cbee935a9f74122efef3f39`

Each of these survives in all three checked broad captures.

## Why this packet is stronger for projection/reveal

Useful direct runtime signal:

- all stable representatives are three-texcoord pixel shaders
- all expose the same source-plus-target helper packet:
  - `sampler_srctex`
  - `sampler_tex`
  - `srctex`
  - `tex`
  - `Constants`
- all expose the same transform/combine variable packet:
  - `fsize`
  - `offset`
  - `scolor`
  - `srctexscale`
  - `texscale`
- richer representatives also add:
  - `scolor2`

Safe reading:

- this is the strongest current runtime home for the `srctex` branch
- that makes it the strongest current projection/reveal helper candidate floor inside the broad umbrella
- it is stronger for this branch than the generated-light `F03` maptex packet

## Relation to the neighboring narrowed rows

Safe branch split:

- generated-light starts from the stable `F03` `maptex` packet
- the remaining projection/reveal branch starts from the stable `F04` `srctex + tex` packet
- `ShaderDayNightParameters` still keeps its own `F04`-first route, but that row is now blocked by context-tagging rather than by lack of a candidate packet

This is still a bounded runtime reading, not full ownership closure.

## Honest limit

This packet does not yet prove:

- whether the `srctex` packet belongs to `RefractionMap`
- whether it belongs to one reveal/intermediate-combine family
- exact scene/draw ownership
- exact slot or matrix semantics

What it does prove:

- the remaining projection/reveal helper branch no longer needs to start from a broad `F03/F04/F05` bucket
- the stable `F04` `srctex + tex` packet is now the strongest current runtime candidate floor for that branch
