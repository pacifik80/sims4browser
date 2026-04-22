# Build/Buy Transparent-Decor Route

This packet reclassifies the current `EP10` transparent-decor cluster into a dedicated transparent-object route instead of leaving it attached only to the `SimGlass` track.

Question:

- does the current workspace already have one better classification-neutral route for transparent `Build/Buy` fixture work than the earlier window-heavy sweep?

Related docs:

- [Build/Buy Transparent Object Classification Boundary](buildbuy-transparent-object-classification-boundary.md)
- [Build/Buy Transparent Object DisplayShelf Anchor](buildbuy-transparent-object-displayshelf-anchor.md)
- [Build/Buy Transparent Object ShopDisplayTileable Anchor](buildbuy-transparent-object-shopdisplay-anchor.md)
- [Build/Buy Transparent Object Top-Anchor Negative Reopen](buildbuy-transparent-object-top-anchor-negative-reopen.md)
- [Build/Buy Transparent Object Lower-Anchor Negative Reopen](buildbuy-transparent-object-lower-anchor-negative-reopen.md)
- [Build/Buy Transparent Object Full-Route Stall](buildbuy-transparent-object-full-route-stall.md)
- [Build/Buy Transparent Object Post-Top-Anchor Handoff](buildbuy-transparent-object-post-top-anchor-handoff.md)
- [Build/Buy Transparent Object Top-Anchor Tiebreak](buildbuy-transparent-object-top-anchor-tiebreak.md)
- [Build/Buy Transparent Object Survey-Versus-Reopen Boundary](buildbuy-transparent-object-survey-vs-reopen-boundary.md)
- [Object Glass And Transparency](../family-sheets/object-glass-and-transparency.md)
- [SimGlass EP10 Transparent-Decor Route](simglass-ep10-transparent-decor-route.md)
- [Build/Buy Material Authority Matrix](../buildbuy-material-authority-matrix.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [Edge-Family Matrix](../edge-family-matrix.md)

## Scope status (`v0.1`)

```text
Build/Buy Transparent-Decor Route
├─ Survey-backed candidate extraction ~ 89%
├─ Companion-bundle route quality ~ 86%
├─ Classification-neutral route wording ~ 95%
└─ Stable live-fixture closure ~ 19%
```

## Externally proved floor

What is already strong enough:

- transparent `Build/Buy` objects now have their own semantic floor in [Object Glass And Transparency](../family-sheets/object-glass-and-transparency.md)
- the classification order is now frozen in [Build/Buy Transparent Object Classification Boundary](buildbuy-transparent-object-classification-boundary.md)

Safe reading:

- this packet does not prove which transparent family the cluster belongs to
- it only proves that the cluster is the best current transparent-object route

## Current route cluster

Current strongest transparent-decor candidates:

- `fishBowl_EP10GENmarimo`
- `shelfFloor2x1_EP10TEAdisplayShelf`
- `shelfFloor2x1_EP10TEAshopDisplayTileable`
- `lightWall_EP10GENlantern`
- `mirrorWall1x1_EP10BATHsunrise`

Current transformed roots:

- `01661233:00000000:FAE0318F3711431D`
- `01661233:00000000:E779C31F25406B73`
- `01661233:00000000:93EE8A0CF97A3861`
- `01661233:00000000:F4A27FC1857F08D4`
- `01661233:00000000:3CD0344C1824BDDD`

## Why this route is currently the strongest

From the current candidate-resolution packet:

- `displayShelf` preserves `Model`, `Rig`, `Slot`, and `Footprint`
- `shopDisplayTileable` preserves `Model`, `Rig`, `Slot`, and `Footprint`
- `mirror` preserves `Model`, `Rig`, and `Footprint`
- `lantern` preserves `Model` and `Footprint`
- `fishBowl` currently preserves only the transformed `Model`

Safe reading:

- this is enough to rank the route structurally
- it is not enough to classify the material family yet

## Why this route now stands apart from the old `SimGlass` wording

The same cluster first appeared under the `SimGlass` route because it was the strongest transparent-content search path after the window-heavy sweep failed.

That wording is now too loose.

Current safer reading:

- the cluster is a transparent-object route first
- classification comes after fixture reopen, not before
- `SimGlass` is now only one possible outcome of that classification step

## Exact target claim for this packet

- the current workspace already has one restart-safe transparent-object route that is stronger than the earlier window-heavy packet and should be tracked separately from the `SimGlass` row

## Best next step after this packet

1. Reopen one root from this cluster as a stable transparent-object fixture.
2. Classify it using the current object-transparent classification boundary.
3. Keep the resulting packet under:
   - object-side glass/transparency, or
   - `SimGlass`

Current safest target order:

1. `displayShelf`
2. `shopDisplayTileable`
3. `mirror`
4. `lantern`
5. `fishBowl`

The first target is now also backed by a dedicated identity anchor:

- [Build/Buy Transparent Object DisplayShelf Anchor](buildbuy-transparent-object-displayshelf-anchor.md)

The second target is now also backed by a peer identity anchor and an explicit tiebreak:

- [Build/Buy Transparent Object ShopDisplayTileable Anchor](buildbuy-transparent-object-shopdisplay-anchor.md)
- [Build/Buy Transparent Object Top-Anchor Tiebreak](buildbuy-transparent-object-top-anchor-tiebreak.md)

Current route state is now narrower than that original starting order:

- `displayShelf` and `shopDisplayTileable` have both already hit the same negative reopen ceiling:
  - `Build/Buy asset not found`
- current next target inside the cluster is now:
  - `mirror`

Preserve that narrowed state through:

- [Build/Buy Transparent Object Top-Anchor Negative Reopen](buildbuy-transparent-object-top-anchor-negative-reopen.md)
- [Build/Buy Transparent Object Post-Top-Anchor Handoff](buildbuy-transparent-object-post-top-anchor-handoff.md)

Current route state is now narrower again:

- `mirror`, `lantern`, and `fishBowl` have also now hit the same negative reopen ceiling:
  - `Build/Buy asset not found`
- the full transparent-decor cluster is therefore stalled at the present inspection layer

Preserve that stalled state through:

- [Build/Buy Transparent Object Lower-Anchor Negative Reopen](buildbuy-transparent-object-lower-anchor-negative-reopen.md)
- [Build/Buy Transparent Object Full-Route Stall](buildbuy-transparent-object-full-route-stall.md)

## Honest limit

What this packet proves:

- the cluster is the strongest current transparent-object route
- it should now be tracked independently from the narrower `SimGlass` branch

What remains open:

- the first stable reopened transparent-object fixture
- which transparent family that fixture belongs to
- which widened route should replace the exhausted `EP10` decor cluster
- until that exists, keep the current route stack below fixture grade:
  - [Build/Buy Transparent Object Survey-Versus-Reopen Boundary](buildbuy-transparent-object-survey-vs-reopen-boundary.md)
