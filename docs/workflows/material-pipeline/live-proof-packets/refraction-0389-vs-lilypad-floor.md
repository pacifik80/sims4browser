# Refraction 0389 Versus LilyPad Floor

This packet fixes the current comparison between the next clean route and the existing named `lilyPad` floor.

Question:

- what does `0389...` currently match from the `lilyPad` floor, and what does it still not inherit from it?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Refraction LilyPad Projective Floor Boundary](refraction-lilypad-projective-floor-boundary.md)
- [Refraction 0389 Clean-Route Baseline](refraction-0389-clean-route-baseline.md)
- [Refraction 0389 Identity Gap](refraction-0389-identity-gap.md)
- [RefractionMap Live Proof](refractionmap-live-proof.md)

## Scope status (`v0.1`)

```text
Refraction 0389 Versus LilyPad Floor
├─ Floor-shape comparison clarity ~ 96%
├─ Named-fixture non-inheritance boundary ~ 97%
├─ Generalization discipline ~ 95%
└─ Exact refraction-slot closure ~ 23%
```

## Current overlap

`0389...` currently matches the `lilyPad` floor on these points:

- `SceneReady`
- `textured=1`
- `WorldToDepthMapSpaceMatrix=1`
- `ProjectiveMaterialDecodeStrategy=1`

That is enough to say:

- `0389...` is the current best second clean route for checking whether the `lilyPad` projective floor generalizes

## Current non-overlap

`0389...` does not yet inherit these stronger `lilyPad` properties:

- named object linkage
- `OBJD` / `instance-swap32` bridge
- direct embedded `MATD` floor packet
- negative-result ceiling on direct `RefractionMap` / `tex1` / `samplerRefractionMap`

## Safe reading

The current comparison is:

- `0389...` matches the shape of the `lilyPad` floor
- `0389...` does not yet match the identity or seam strength of the `lilyPad` fixture

## Why this matters

Without this comparison packet, the next run could drift into one of two bad readings:

- `0389...` is already basically the same fixture as `lilyPad`
- or `0389...` is only another loose candidate with no meaningful relation to the existing floor

The current evidence supports neither extreme.

## What remains open

- whether `0389...` only reproduces the same floor
- whether `0389...` surfaces stronger family-local evidence than `lilyPad`
- whether `0389...` can later gain named object identity comparable to `lilyPad`
