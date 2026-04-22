# Refraction 0124 Mixed-Control Floor

This packet freezes why `01661233:00000000:0124E3B8AC7BEE62` stays a mixed/control route instead of outranking the cleaner `0389...` path.

Question:

- what exactly makes `0124...` useful as a refraction boundary/control route, but too noisy to be the next clean route after `lilyPad`?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [RefractionMap Live Proof](refractionmap-live-proof.md)
- [Refraction Next-Route Priority](refraction-next-route-priority.md)

## Scope status (`v0.1`)

```text
Refraction 0124 Mixed-Control Floor
├─ Mixed-route evidence capture ~ 97%
├─ Control-route demotion clarity ~ 96%
├─ Fallback-noise boundary ~ 95%
└─ Exact refraction-slot closure ~ 20%
```

## Current local result

Current `tmp/probe_0124_projective_current.txt` is already strong enough to record:

- `Texture candidates: 0`
- repeated `WorldToDepthMapSpaceMatrix=2`
- repeated `ProjectiveMaterialDecodeStrategy=2`
- repeated `diffuse` resolution through explicit indexed cross-package lookup from `ClientFullBuild2.package`
- repeated fallback wording around portable `diffuse` approximation
- one separate `FresnelOffset` / `DefaultMaterialDecodeStrategy` branch on `LOD 00010002`

## Safe reading

This route currently proves:

- `0124...` is a real projective/refraction-neighborhood route
- it is useful as a mixed/control case
- it already carries more route noise than the cleaner one-family `0389...` path

This route does not currently support:

- promotion over `0389...` as the next clean route
- use as the safest generalizer for the `lilyPad` floor

## Why it matters

Without this packet, `0124...` could still be overread as:

- a richer projective route that should automatically outrank `0389...`

The safer reading is narrower:

- `0124...` is richer mainly because it is noisier
- that makes it better for comparison, falsification, and boundary testing than for the next clean route attempt

## Next useful role

Use `0124...` next only if:

- `0389...` fails cleanly
- or a comparison packet specifically needs a mixed/control route against the cleaner `lilyPad` / `0389...` pair
