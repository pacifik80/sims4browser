# Refraction Post-LilyPad Pivot

This packet records the safe pivot after the named `lilyPad` fixture reached a stable floor/ceiling boundary.

Question:

- after the current `lilyPad` result is frozen honestly, what is the next refraction packet trying to prove, and what should it stop trying to prove from the same fixture?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Refraction Evidence Ledger](../refraction-evidence-ledger.md)
- [Refraction Bridge Fixture Boundary](../refraction-bridge-fixture-boundary.md)
- [Refraction LilyPad Direct MATD Floor](refraction-lilypad-direct-matd-floor.md)
- [Refraction LilyPad Projective Floor Boundary](refraction-lilypad-projective-floor-boundary.md)
- [Refraction LilyPad No Direct Family Surface](refraction-lilypad-no-direct-family-surface.md)
- [Refraction LilyPad Escalation Boundary](refraction-lilypad-escalation-boundary.md)
- [RefractionMap Live Proof](refractionmap-live-proof.md)

## Scope status (`v0.1`)

```text
Refraction Post-LilyPad Pivot
├─ Floor/ceiling carry-forward clarity ~ 96%
├─ Anti-loop pivot discipline ~ 97%
├─ Next-route problem framing ~ 94%
└─ Exact refraction-slot closure ~ 21%
```

## What is already fixed by `lilyPad`

The current named fixture is already strong enough to stay preserved as:

- a named `Build/Buy` bridge root
- a direct embedded `MATD` floor
- a stable `WorldToDepthMapSpaceMatrix` / `ProjectiveMaterialDecodeStrategy` floor
- a negative-result ceiling for direct family-local `RefractionMap`, `tex1`, and `samplerRefractionMap`

## What this packet changes

The next refraction packet should no longer ask:

- can `lilyPad` maybe still be pushed into exact slot closure if we phrase the same seam differently?

The safer next question is narrower:

- is there a second clean refraction-bearing route that can surface family-local evidence above the current projective floor, or does the same ceiling repeat on another route too?

## Safe reading

`lilyPad` remains the strongest named floor/ceiling fixture.

It is no longer the whole refraction track.

After this point, `lilyPad` should be used as:

- the current named reference fixture
- the current best direct `MATD` floor
- the current best stable projective floor
- the current first negative-result ceiling

And the next route should carry the burden of any stronger closure.

## Next packet shape

The next route packet should prove or falsify one of these claims:

1. a second clean projective/refraction-bearing root reproduces the same floor without adding direct family-local surfacing
2. a second clean projective/refraction-bearing root surfaces direct family-local evidence above the current `lilyPad` ceiling
3. the remaining nearby routes are only mixed/control cases and do not outrank the current `lilyPad` floor

## Implementation-diagnostic value

This pivot makes the implementation gap easier to diagnose:

- if multiple clean routes stall at the same projective floor, the current repo is likely missing a higher family-local inspection layer
- if a second route surfaces direct family-local evidence where `lilyPad` does not, then `lilyPad` is a bounded floor/ceiling packet rather than the universal refraction proxy
