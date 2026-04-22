# Refraction 0389 Clean-Route Baseline

This packet records the first honest baseline for the next clean refraction-bearing route after `lilyPad`.

Question:

- what does the current workspace already prove about `01661233:00000000:0389A352F5EDFD45`, and why is it the next clean route instead of just another nearby control?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [RefractionMap Live Proof](refractionmap-live-proof.md)
- [Refraction Post-LilyPad Pivot](refraction-post-lilypad-pivot.md)
- [Refraction Next-Route Priority](refraction-next-route-priority.md)

## Scope status (`v0.1`)

```text
Refraction 0389 Clean-Route Baseline
├─ Coverage-backed route capture ~ 95%
├─ Clean-route qualification ~ 94%
├─ Restart-safe next-target wording ~ 96%
└─ Exact refraction-slot closure ~ 23%
```

## Current local result

Current `tmp/probe_sample_medium_coverage.txt` is already strong enough to record:

- `EP11\ClientFullBuild0.package`
- `Build/Buy Model 0389A352F5EDFD45`
- `Found: True`
- `Scene Success: True`
- `Material Visual Payloads: textured=1`
- `Material Families: WorldToDepthMapSpaceMatrix=1`
- `Material Decode Strategies: ProjectiveMaterialDecodeStrategy=1`

## Safe reading

This route currently proves:

- one second clean projective/refraction-bearing root exists beyond `lilyPad`
- it reproduces the same one-family/one-strategy shape as the current `lilyPad` projective floor
- it is strong enough to become the next clean route in the queue

This route does not yet prove:

- direct family-local `RefractionMap`
- direct `tex1`
- direct object-name linkage comparable to `lilyPad`
- exact slot closure

## Why it matters

Without this packet, `0389...` would still be only:

- a promising line in sampled coverage

The safer reading is now stronger:

- it is the current best second clean route for checking whether the `lilyPad` floor generalizes

## Next useful question

The next inspection on this route should answer:

- does `0389...` only reproduce the same projective floor, or does it surface stronger family-local refraction evidence than `lilyPad`?
