# Build/Buy Transparent Object Top-Anchor Exhaustion Boundary

This packet records when the transparent-object branch is allowed to leave the two strongest peer anchors and continue to weaker targets.

Question:

- what current evidence already justifies a hard boundary that keeps `mirror`, `lantern`, and `fishBowl` behind `displayShelf` and `shopDisplayTileable` until both top anchors are honestly exhausted?

Related docs:

- [Build/Buy Transparent Object DisplayShelf Anchor](buildbuy-transparent-object-displayshelf-anchor.md)
- [Build/Buy Transparent Object ShopDisplayTileable Anchor](buildbuy-transparent-object-shopdisplay-anchor.md)
- [Build/Buy Transparent Object Top-Anchor Tiebreak](buildbuy-transparent-object-top-anchor-tiebreak.md)
- [Build/Buy Transparent Object Target Priority](buildbuy-transparent-object-target-priority.md)
- [Build/Buy Transparent Object Route Stall Boundary](buildbuy-transparent-object-route-stall-boundary.md)

## Scope status (`v0.1`)

```text
Build/Buy Transparent Object Top-Anchor Exhaustion Boundary
├─ Strongest-anchor exhaustion rule ~ 97%
├─ Anti-premature widening discipline ~ 97%
├─ Restart-safe top-tier handoff wording ~ 96%
└─ Stable live-fixture closure ~ 19%
```

## Current top-tier rule

With the current workspace evidence, the route must not widen to:

- `mirror`
- `lantern`
- `fishBowl`

until both of these top anchors have been honestly exhausted:

1. `displayShelf`
2. `shopDisplayTileable`

## Why this boundary now exists

Current workspace already proves:

- both top anchors preserve the same full companion bundle:
  - `Model`
  - `Rig`
  - `Slot`
  - `Footprint`
- the current tiebreak between them is structural rather than semantic
- the weaker anchors preserve smaller bundles:
  - `mirror` lacks `Slot`
  - `lantern` lacks `Rig` and `Slot`
  - `fishBowl` currently preserves only `Model`

That is already strong enough to freeze a top-tier exhaustion boundary.

## Safe reading

This boundary means:

- `displayShelf` first
- `shopDisplayTileable` second
- only after both are exhausted honestly may the route continue to `mirror`

This boundary does not mean:

- either top anchor is already a reopened fixture
- weaker anchors are invalid
- family classification is already known for either top anchor

## What this packet prevents

Without this packet, the branch could still drift into:

- jumping to `mirror` after one failed or ambiguous pass on `displayShelf`
- treating `shopDisplayTileable` as optional even though it is a peer strongest anchor
- widening to weaker anchors before the strongest pair is actually exhausted

## Best next use of this packet

1. Attempt the first real reopen on `displayShelf`.
2. If it stays below fixture grade, move immediately to `shopDisplayTileable`.
3. Only after both top anchors are honestly exhausted should the route continue to `mirror`, `lantern`, and `fishBowl`.
