# Build/Buy Window-Heavy Transparent Negative Control

This packet freezes the current negative boundary for transparent `Build/Buy` fixture search.

Question:

- does the current workspace already know enough to stop reopening the old window-heavy transparent sweep before the transparent-decor route?

Related docs:

- [Build/Buy Transparent-Decor Route](buildbuy-transparent-decor-route.md)
- [Build/Buy Transparent Object Full-Route Stall](buildbuy-transparent-object-full-route-stall.md)
- [Build/Buy Transparent Object Classification Boundary](buildbuy-transparent-object-classification-boundary.md)
- [Object Glass And Transparency](../family-sheets/object-glass-and-transparency.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [Edge-Family Matrix](../edge-family-matrix.md)

## Scope status (`v0.1`)

```text
Build/Buy Window-Heavy Transparent Negative Control
├─ Repeated-name survey evidence ~ 86%
├─ Negative-control boundary wording ~ 93%
└─ Stable live-fixture closure ~ 12%
```

## What is already externally proved

What is already strong enough:

- transparent `Build/Buy` fixtures must be classified through the object-transparent split before they are attached to narrower family rows
- the current strongest transparent-object route is the transparent-decor cluster, not the older window-heavy sweep

This packet does not change that semantic floor.

It only freezes the negative side of the search boundary.

## Current window-heavy packet

From the current `EP10` identity survey:

- `window1X1_EP10TRADglassPanelTileable3Tile_set1..13`
- `window1X1_EP10TRADglassShortTileable2Tile_set1..11`

From the narrower obvious-name extraction pass:

- `window2X1_EP10GENsliding2Tile`
- `window2X1_EP10TRADwindowBox2Tile`
- `mirrorWall1x1_EP10BATHsunrise`
- `sculptFountainEmitterSingle1x1_EP10GARDstoneBowl`
- `sculptWall_EP10TRADwindowBars`

## What this packet proves

Safe reading:

- repeated `glass` naming in the survey is not enough to make the window-heavy slice the best next route
- the first obvious-name extraction pass already failed to produce a comparably clean reopened root
- because of that, the window-heavy slice is now a negative control, not the default forward path

## What this packet does not prove

It does not prove:

- that window objects can never carry the winning transparent family
- that the older window-heavy sweep is useless forever
- that the transparent-decor route is already classified

Current honest limit:

- the window-heavy packet is lower priority, not permanently disproved

## Exact target claim for this packet

- the current workspace already has enough negative evidence to keep the window-heavy transparent sweep behind the transparent-decor route during restart and queue ordering

## Best next step after this packet

1. Keep the window-heavy sweep as a negative control while the decor route is stronger.
2. Once the decor route is explicitly stalled, it is now acceptable to return here as the next widening phase.
3. Do not treat that return as a semantic upgrade by itself; it is only the next search lane.

## Honest limit

What this packet proves:

- the project now has an explicit negative control for the old window-heavy transparent sweep

What remains open:

- whether a later transparent-object pass will need to come back to windows after the decor route is exhausted
