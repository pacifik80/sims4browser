# Build/Buy Transparent Object Top-Anchor Tiebreak

This packet records why `displayShelf` stays first and `shopDisplayTileable` stays second even though both currently preserve the same full companion bundle.

Question:

- what current evidence justifies a stable tiebreak between the two strongest transparent-object anchors without pretending that one already has stronger family semantics?

Related docs:

- [Build/Buy Transparent Object DisplayShelf Anchor](buildbuy-transparent-object-displayshelf-anchor.md)
- [Build/Buy Transparent Object ShopDisplayTileable Anchor](buildbuy-transparent-object-shopdisplay-anchor.md)
- [Build/Buy Transparent Object Target Priority](buildbuy-transparent-object-target-priority.md)
- [Build/Buy Transparent Object Survey-Versus-Reopen Boundary](buildbuy-transparent-object-survey-vs-reopen-boundary.md)

## Scope status (`v0.1`)

```text
Build/Buy Transparent Object Top-Anchor Tiebreak
├─ Strongest-anchor parity capture ~ 96%
├─ Non-semantic tiebreak clarity ~ 97%
├─ Restart-safe first-versus-second order ~ 96%
└─ Stable live-fixture closure ~ 19%
```

## Current parity

Current workspace already shows that both top anchors preserve the same bundle shape:

- `displayShelf`
  - `Model`
  - `Rig`
  - `Slot`
  - `Footprint`
- `shopDisplayTileable`
  - `Model`
  - `Rig`
  - `Slot`
  - `Footprint`

## Current tiebreak

Current safest reading is still:

1. `displayShelf`
2. `shopDisplayTileable`

Why this tiebreak survives:

- `displayShelf` is already the simpler first representative in the current route packet stack
- `shopDisplayTileable` is now a peer anchor, but not a stronger one
- the current evidence does not justify semantic ranking between them

## Safe reading

This tiebreak is:

- structural
- restart-safe
- intentionally non-semantic

It is not:

- a claim that `displayShelf` is more likely to be object-glass
- a claim that `shopDisplayTileable` is less transparent-family-relevant
- a claim that either anchor is already a reopened fixture

## What this packet prevents

Without this packet, the branch could still drift into one of two bad readings:

- the two strongest anchors are interchangeable, so the first-target order does not matter
- `displayShelf` must already be semantically stronger because it is listed first

The current evidence supports neither reading.

## Best next use of this packet

- reopen `displayShelf` first
- if it stays below fixture grade, move immediately to `shopDisplayTileable`
- do not widen to weaker anchors before both strongest peer anchors are exhausted honestly
