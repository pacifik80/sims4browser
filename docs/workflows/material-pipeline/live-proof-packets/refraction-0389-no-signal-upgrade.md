# Refraction 0389 No Signal Upgrade

This packet records the current honest ceiling on the `0389...` clean route: with the evidence currently in the workspace, it does not yet upgrade the refraction branch above the named `lilyPad` floor.

Question:

- what stronger refraction claim does `0389A352F5EDFD45` still fail to support with the current local evidence stack?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Refraction 0389 Clean-Route Baseline](refraction-0389-clean-route-baseline.md)
- [Refraction 0389 Identity Gap](refraction-0389-identity-gap.md)
- [Refraction 0389 Versus LilyPad Floor](refraction-0389-vs-lilypad-floor.md)
- [RefractionMap Live Proof](refractionmap-live-proof.md)

## Scope status (`v0.1`)

```text
Refraction 0389 No Signal Upgrade
├─ No-upgrade ceiling capture ~ 96%
├─ Anti-overread discipline ~ 97%
├─ Route-stall wording ~ 95%
└─ Exact refraction-slot closure ~ 22%
```

## Current local ceiling

Current workspace support for `0389A352F5EDFD45` is still only:

- `tmp/probe_sample_medium_coverage.txt`
- `EP11\ClientFullBuild0.package`
- `Build/Buy Model 0389A352F5EDFD45`
- `SceneReady`
- `textured=1`
- `WorldToDepthMapSpaceMatrix=1`
- `ProjectiveMaterialDecodeStrategy=1`

Current workspace still does not add:

- named object linkage
- `OBJD` / `COBJ`-backed identity
- direct embedded `MATD` floor packet
- meaningful `MTST` packet
- direct family-local `RefractionMap`
- direct `tex1`
- direct `samplerRefractionMap`

## Safe reading

`0389...` currently supports:

- a second clean projective/refraction-bearing route
- a second instance of the same projective floor shape already seen on `lilyPad`

`0389...` does not currently support:

- a stronger family-local surface than `lilyPad`
- promotion above the named `lilyPad` floor/ceiling packet
- any claim that the refraction branch is now closer to exact slot closure just because the clean route repeats

## Why this matters

Without this packet, the route could still drift from:

- “the next clean refraction route currently repeats the same floor”

into:

- “the next clean refraction route is probably the stronger refraction fixture”

The current evidence does not support that promotion.

## What remains open

- whether a new inspection layer can surface stronger evidence on `0389...`
- whether a narrower identity sweep can name `0389...`
- whether the route should stay only a confirming ceiling packet and then hand off to the next family track
