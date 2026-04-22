# Build/Buy Transparent Object Route Stall Boundary

This packet freezes the current stop condition for the transparent-object route.

Question:

- does the current workspace already know when the transparent-decor route should be considered stalled strongly enough to justify widening back out to lower-priority transparent-object search paths?

Related docs:

- [Build/Buy Transparent-Decor Route](buildbuy-transparent-decor-route.md)
- [Build/Buy Transparent Object Candidate State Ladder](buildbuy-transparent-object-candidate-state-ladder.md)
- [Build/Buy Transparent Object Top-Anchor Negative Reopen](buildbuy-transparent-object-top-anchor-negative-reopen.md)
- [Build/Buy Transparent Object Lower-Anchor Negative Reopen](buildbuy-transparent-object-lower-anchor-negative-reopen.md)
- [Build/Buy Transparent Object Full-Route Stall](buildbuy-transparent-object-full-route-stall.md)
- [Build/Buy Transparent Object Post-Top-Anchor Handoff](buildbuy-transparent-object-post-top-anchor-handoff.md)
- [Build/Buy Transparent Object Target Priority](buildbuy-transparent-object-target-priority.md)
- [Build/Buy Window-Heavy Transparent Negative Control](buildbuy-window-heavy-transparent-negative-control.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)

## Scope status (`v0.1`)

```text
Build/Buy Transparent Object Route Stall Boundary
├─ Route-stall criteria ~ 95%
├─ Widening-back-out rule ~ 94%
└─ First live-fixture application ~ 20%
```

## Why this packet exists

The transparent-object branch already has:

1. a route
2. a target order
3. a state ladder
4. classification and promotion rules
5. a lower-priority window-heavy negative control

What was still missing:

- one explicit rule for when the primary route is exhausted enough to justify leaving it

## Current stall criteria

Treat the transparent-decor route as stalled only when all of the following are true:

1. the current reopen order has been attempted through the whole prioritized cluster:
   - `displayShelf`
   - `shopDisplayTileable`
   - `mirror`
   - `lantern`
   - `fishBowl`
2. none of those candidates crosses the current promotion boundary
3. none of those candidates remains a strong provisional fixture worth one immediate follow-up pass

Safe reading:

- one failed reopen is not a stalled route
- one mixed or provisional candidate is not automatically a stalled route
- the route stalls only after the current cluster is genuinely exhausted or reduced to weaker provisional residue
- the current pair of top-anchor failures is not a stalled route either:
  - it is only the point where the route may hand off to `mirror`

## Widening-back-out rule

Only after the route stalls may the next pass:

1. return to the window-heavy negative control, or
2. widen to another transparent-object package slice

Safe reading:

- widening is a later phase, not a parallel first move
- the window-heavy sweep stays explicitly second-line while the decor route still has stronger unresolved candidates

## Exact target claim for this packet

- the current workspace already has enough structure to define when the transparent-decor route is exhausted enough to justify widening the search

## Best next step after this packet

1. Preserve the completed full-route negative reopen packet stack.
2. Mark the route stalled once all five anchors have failed without a provisional or stable fixture.
3. Only then widen to the window-heavy path or another package slice.
4. Do not return to the decor cluster without a new inspection layer.

## Honest limit

What this packet proves:

- the transparent-object route now has an explicit stall boundary

What remains open:

- whether the route will actually stall before producing the first stable fixture
