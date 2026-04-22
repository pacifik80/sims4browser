# Build/Buy Transparent Object Post-Top-Anchor Handoff

This packet records where the transparent-object route should go after both strongest peer anchors hit the same negative reopen ceiling.

Question:

- once `displayShelf` and `shopDisplayTileable` have both failed to reopen cleanly, what is the next restart-safe route move?

Related docs:

- [Build/Buy Transparent Object Top-Anchor Negative Reopen](buildbuy-transparent-object-top-anchor-negative-reopen.md)
- [Build/Buy Transparent Object Top-Anchor Exhaustion Boundary](buildbuy-transparent-object-top-anchor-exhaustion-boundary.md)
- [Build/Buy Transparent Object Target Priority](buildbuy-transparent-object-target-priority.md)
- [Build/Buy Transparent Object Route Stall Boundary](buildbuy-transparent-object-route-stall-boundary.md)
- [Build/Buy Window-Heavy Transparent Negative Control](buildbuy-window-heavy-transparent-negative-control.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)

## Scope status (`v0.1`)

```text
Build/Buy Transparent Object Post-Top-Anchor Handoff
├─ Top-anchor handoff rule ~ 96%
├─ Anti-loop discipline ~ 96%
├─ Restart-safe next-target pivot ~ 95%
└─ Stable live-fixture closure ~ 24%
```

## Current handoff state

The current route has now satisfied the strongest-anchor exhaustion boundary:

1. `displayShelf` was attempted as a real reopen target
2. `shopDisplayTileable` was attempted as a real reopen target
3. both returned the same negative reopen result:
   - `Build/Buy asset not found`

That is already strong enough to move the route below the peer top-anchor pair.

## Current next-target order after the handoff

Current safest continuation is now:

1. `mirror`
2. `lantern`
3. `fishBowl`

Only after those weaker anchors are honestly exhausted may the route widen back out.

## Safe reading

This handoff means:

- the route may leave `displayShelf` and `shopDisplayTileable`
- the route is not stalled yet
- the next pass should stay inside the transparent-decor cluster

This handoff does not mean:

- `mirror` is now semantically stronger than the top anchors
- the route should widen back to the window-heavy packet already
- a family classification is now available

## What this packet prevents

Without this handoff packet, the branch could still drift into:

- retrying the same top anchors without a new inspection layer
- widening to the window-heavy negative control too early
- treating the whole transparent-decor route as stalled after only two negative top-anchor results

## Exact target claim for this packet

- the current workspace already has enough real reopen evidence to hand the transparent-object route from the exhausted top-anchor pair down to `mirror`

## Best next step after this packet

1. Attempt the first real reopen on `mirror`.
2. If it fails the same way, continue to `lantern`.
3. Then continue to `fishBowl`.
4. Only after that should the route be considered for widening.

## Honest limit

What this packet proves:

- the route now has a restart-safe post-top-anchor pivot

What remains open:

- whether any lower-ranked anchor will reopen at all
- the first transparent-object classification result
