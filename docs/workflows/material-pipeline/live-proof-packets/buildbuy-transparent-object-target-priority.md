# Build/Buy Transparent Object Target Priority

This packet freezes the current reopen priority inside the `Build/Buy` transparent-object route.

Question:

- does the current workspace already know enough to justify one stable reopen order inside the transparent-decor cluster instead of treating all five roots as equal?

Related docs:

- [Build/Buy Transparent-Decor Route](buildbuy-transparent-decor-route.md)
- [Build/Buy Transparent Object DisplayShelf Anchor](buildbuy-transparent-object-displayshelf-anchor.md)
- [Build/Buy Transparent Object ShopDisplayTileable Anchor](buildbuy-transparent-object-shopdisplay-anchor.md)
- [Build/Buy Transparent Object Top-Anchor Negative Reopen](buildbuy-transparent-object-top-anchor-negative-reopen.md)
- [Build/Buy Transparent Object Post-Top-Anchor Handoff](buildbuy-transparent-object-post-top-anchor-handoff.md)
- [Build/Buy Transparent Object Top-Anchor Tiebreak](buildbuy-transparent-object-top-anchor-tiebreak.md)
- [Build/Buy Transparent Object Top-Anchor Exhaustion Boundary](buildbuy-transparent-object-top-anchor-exhaustion-boundary.md)
- [Build/Buy Transparent Object Classification Boundary](buildbuy-transparent-object-classification-boundary.md)
- [Object Glass And Transparency](../family-sheets/object-glass-and-transparency.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [Research Restart Guide](../research-restart-guide.md)

## Scope status (`v0.1`)

```text
Build/Buy Transparent Object Target Priority
├─ Companion-bundle ranking evidence ~ 91%
├─ Restart-safe reopen order ~ 96%
└─ Stable live-fixture closure ~ 19%
```

## Externally proved floor

What is already strong enough:

- transparent `Build/Buy` objects now have a separate semantic floor from `SimGlass`
- the transparent-decor cluster is already the best current transparent-object route
- reopened fixtures from that cluster must be classified after reopen, not before

This packet does not add a new semantic rule.

It only freezes the current reopen order inside the existing route.

## Current candidate order

Current safest target order:

1. `displayShelf`
2. `shopDisplayTileable`
3. `mirror`
4. `lantern`
5. `fishBowl`

## Why this order is currently the safest

### 1. `displayShelf`

Current structural packet:

- `Model`
- `Rig`
- `Slot`
- `Footprint`

Safe reading:

- this is the strongest current companion bundle in the cluster
- it gives the best chance of reopening one stable object-side fixture without widening back out to other package slices
- exact current survey/candidate-resolution identity now lives in:
  - [Build/Buy Transparent Object DisplayShelf Anchor](buildbuy-transparent-object-displayshelf-anchor.md)

### 2. `shopDisplayTileable`

Current structural packet:

- `Model`
- `Rig`
- `Slot`
- `Footprint`

Safe reading:

- structurally almost as strong as `displayShelf`
- kept second only because the current docs already use `displayShelf` as the simplest first representative
- exact current survey/candidate-resolution identity now lives in:
  - [Build/Buy Transparent Object ShopDisplayTileable Anchor](buildbuy-transparent-object-shopdisplay-anchor.md)
- the current non-semantic tiebreak now also lives in:
  - [Build/Buy Transparent Object Top-Anchor Tiebreak](buildbuy-transparent-object-top-anchor-tiebreak.md)

### 3. `mirror`

Current structural packet:

- `Model`
- `Rig`
- `Footprint`

Safe reading:

- stronger than `lantern` because it still preserves `Rig`
- weaker than `displayShelf` and `shopDisplayTileable` because the current companion bundle is smaller

### 4. `lantern`

Current structural packet:

- `Model`
- `Footprint`

Safe reading:

- still better than `fishBowl`
- weaker than `mirror` because the current packet preserves fewer direct companions

### 5. `fishBowl`

Current structural packet:

- transformed `Model` root only

Safe reading:

- plausible by name and route membership
- weakest structural reopen candidate in the current cluster

## Exact target claim for this packet

- the current workspace already has enough candidate-resolution evidence to justify one restart-safe reopen order inside the transparent-decor cluster

## Current state after the strongest-anchor pair

The generic route order above still stands.

But the current route state is now narrower:

- `displayShelf` and `shopDisplayTileable` have both been attempted as real reopen targets
- both currently stop at the same negative ceiling:
  - `Build/Buy asset not found`
- the current post-top-anchor continuation therefore starts at:
  - `mirror`

Use these packets to preserve that narrowed current state:

- [Build/Buy Transparent Object Top-Anchor Negative Reopen](buildbuy-transparent-object-top-anchor-negative-reopen.md)
- [Build/Buy Transparent Object Post-Top-Anchor Handoff](buildbuy-transparent-object-post-top-anchor-handoff.md)

## What this packet does not prove

It does not prove:

- which transparent family any one of these fixtures belongs to
- that `displayShelf` is semantically more likely than `mirror` to be object-glass or `AlphaBlended`
- that lower-ranked targets are invalid

Current honest limit:

- this is a structural reopen-order packet only

## Best next step after this packet

1. Preserve the full structural order:
   - `displayShelf`
   - `shopDisplayTileable`
   - `mirror`
   - `lantern`
   - `fishBowl`
2. When resuming from the current evidence state, continue at `mirror`.
3. Then continue to `lantern` and `fishBowl`.
4. Only after that cluster stalls should the search widen or return to the window-heavy negative control.

Before dropping below the two strongest anchors, use the explicit top-tier boundary too:

- [Build/Buy Transparent Object Top-Anchor Exhaustion Boundary](buildbuy-transparent-object-top-anchor-exhaustion-boundary.md)

## Honest limit

What this packet proves:

- the route now has a stable internal priority order

What remains open:

- the first stable reopened transparent-object fixture
- its actual family classification
