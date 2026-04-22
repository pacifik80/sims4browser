# Refraction Post-0389 Handoff Boundary

This packet records the safe handoff after the current `0389...` clean-route ceiling: if no stronger evidence layer appears, the refraction track should stop deepening this route and hand work to the next highest-value family track.

Question:

- after `0389...` is honestly bounded as a clean route that does not yet upgrade the `lilyPad` floor, when should the batch stop deepening refraction and move on?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Refraction Post-LilyPad Pivot](refraction-post-lilypad-pivot.md)
- [Refraction Next-Route Priority](refraction-next-route-priority.md)
- [Refraction 0389 No Signal Upgrade](refraction-0389-no-signal-upgrade.md)
- [RefractionMap Live Proof](refractionmap-live-proof.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)

## Scope status (`v0.1`)

```text
Refraction Post-0389 Handoff Boundary
├─ Route-exit discipline ~ 96%
├─ Anti-loop handoff wording ~ 97%
├─ Next-track transition clarity ~ 95%
└─ Exact refraction-slot closure ~ 21%
```

## Current handoff rule

With the current workspace evidence, the refraction route should stop deepening `0389...` when all three of these remain true:

1. `0389...` still only surfaces the same `WorldToDepthMapSpaceMatrix` / `ProjectiveMaterialDecodeStrategy` floor shape already seen on `lilyPad`
2. `0389...` still lacks named object/material identity comparable to `lilyPad`
3. no new inspection layer surfaces direct family-local `RefractionMap`, `tex1`, `samplerRefractionMap`, direct embedded `MATD`, or meaningful `MTST`

## Safe reading

After that point:

- keep `lilyPad -> 00F643...` as the named refraction floor/ceiling reference
- keep `0389...` as the clean-route confirming ceiling
- keep `0124...` as the mixed/control route
- do not spend more wording-only passes trying to force a stronger refraction closure from the same evidence stack

The next highest-value handoff is:

- return to the next unfinished family track rather than looping on refraction
- current queue order makes the next strong handoff target the transparent-decor / object-transparency branch first, then `SimGlass` only after object-side classification work

## What this packet prevents

Without this handoff boundary, the batch could still:

- keep rephrasing the same `0389...` floor as if it were new closure
- bounce between `0389...` and `lilyPad` without a new inspection layer
- delay the next unfinished family track even after the refraction route is already restart-safe

## Implementation-diagnostic value

This boundary sharpens the implementation failure reading:

- if both `lilyPad` and `0389...` stall at the same projective floor, the current repo is probably missing a higher family-local inspection layer for refraction
- until that higher layer exists, the more valuable work is on the next unfinished family tracks rather than on more route-only refraction prose
