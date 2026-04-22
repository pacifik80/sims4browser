# Refraction Next-Route Priority

This packet fixes the restart-safe order for refraction-bearing routes after `lilyPad` stops being the only active fixture.

Question:

- after the `lilyPad` ceiling is frozen, which nearby refraction-bearing roots should be inspected next, and in what order?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Refraction Post-LilyPad Pivot](refraction-post-lilypad-pivot.md)
- [Refraction LilyPad Escalation Boundary](refraction-lilypad-escalation-boundary.md)
- [RefractionMap Live Proof](refractionmap-live-proof.md)

## Scope status (`v0.1`)

```text
Refraction Next-Route Priority
├─ Secondary-route ordering ~ 95%
├─ Mixed-control demotion clarity ~ 96%
├─ Restart-safe next-target wording ~ 94%
└─ Exact refraction-slot closure ~ 22%
```

## Current local route order

### 1. Next clean route: `0389A352F5EDFD45`

Current local packet in `tmp/probe_sample_medium_coverage.txt`:

- `EP11\ClientFullBuild0.package`
- `Build/Buy Model 0389A352F5EDFD45`
- `SceneReady`
- `textured=1`
- `WorldToDepthMapSpaceMatrix=1`
- `ProjectiveMaterialDecodeStrategy=1`

Why it ranks next:

- it currently looks like the nearest second clean projective/refraction-bearing route
- it keeps the same one-family/one-strategy shape as the current `lilyPad` floor
- unlike `lilyPad`, it is not already saturated with floor/ceiling packets

### 2. Reference floor only: `00F643B0FDD2F1F7`

Current role:

- keep `lilyPad -> 00F643...` as the named floor/ceiling reference root
- do not return to it as the default next deep target unless a genuinely new inspection layer appears

### 3. Mixed control route: `0124E3B8AC7BEE62`

Current local packet in `tmp/probe_sample_medium_coverage.txt` plus earlier route notes:

- `EP04\ClientFullBuild0.package`
- `Build/Buy Model 0124E3B8AC7BEE62`
- `SceneReady`
- `textured=2`
- `WorldToDepthMapSpaceMatrix=2`
- earlier narrower probing already exposed one `FresnelOffset` boundary and fallback diffuse behavior

Why it stays behind `0389`:

- it is still the better mixed/control case
- it already carries more boundary noise than the cleaner one-family `0389` route
- it is better for comparison or falsification than for the next clean route attempt

### 4. Neighborhood controls only: `0737711577697F1C` and `00B6ABED04A8F593`

Current role:

- keep them as visible neighborhood controls
- do not treat them as the next refraction route just because they are textured and visible

## Safe reading

The next refraction route order is now:

1. inspect `0389A352F5EDFD45` as the next clean route
2. keep `00F643B0FDD2F1F7` as the bounded named reference floor/ceiling
3. keep `0124E3B8AC7BEE62` as the mixed/control route
4. keep `0737711577697F1C` and `00B6ABED04A8F593` as neighborhood controls only

## What the next route should prove

The next route should prove one of two useful outcomes:

- `0389` reproduces the current projective floor and confirms that the `lilyPad` ceiling is not a one-off artifact
- `0389` surfaces stronger family-local evidence than `lilyPad`, which would justify promoting it above the current floor/ceiling reference

## What this packet prevents

Without this priority packet, a restart could still:

- go back to `lilyPad` by inertia
- promote `0124` too early even though it is still the noisier mixed/control case
- confuse visible neighborhood controls with the next clean refraction route
