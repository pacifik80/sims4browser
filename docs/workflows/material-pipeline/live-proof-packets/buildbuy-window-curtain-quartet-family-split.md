# Build/Buy Window-Curtain Quartet Family Split

This packet closes the current widened `EP10` quartet by freezing the family split that now survives after both the window-side and curtain-side branches have been separately bounded.

Question:

- after closing the window-side and curtain-side packets, can the widened quartet now be carried as a family split rather than as one unresolved transparent-object bundle?

Related docs:

- [Build/Buy Window Structural-Cutout Verdict Floor](buildbuy-window-structural-cutout-verdict-floor.md)
- [Build/Buy Curtain Route Closure](buildbuy-curtain-route-closure.md)
- [Build/Buy Window-Curtain Family Verdict Boundary](buildbuy-window-curtain-family-verdict-boundary.md)
- [Object Glass And Transparency](../family-sheets/object-glass-and-transparency.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)

## Scope status (`v0.1`)

```text
Build/Buy Window-Curtain Quartet Family Split
├─ Window-side verdict floor ~ 94%
├─ Curtain-side verdict floor ~ 89%
├─ Object-glass non-selection ~ 90%
└─ Quartet split closure ~ 91%
```

## Externally safe order

What remains externally strong enough:

- windows/openings can close through structural opening resources
- curtains should only be carried as `AlphaBlended` when that route is explicit
- weaker threshold/cutout and object-glass remain separate object-side routes

External anchors:

- [Tutorial: how to make CC Cutout compatible with last update](https://s4cc.syboulette.fr/tutorial-how-to-make-cc-windows-doors-and-archways/)
- [Object Material Settings Cheat Sheet](https://staberindesims.wordpress.com/2021/06/05/object-material-settings-cheat-sheet/)

## The family split that now holds

Window side:

- `sliding2Tile`
- `windowBox2Tile`
- safest verdict:
  - structural cutout/opening first
  - material cutout hints second

Curtain side:

- `norenShortTileable`
- `strawTileable2Tile`
- safest verdict:
  - weaker threshold/cutout route
  - not explicit `AlphaBlended`

What does not win:

- object glass
- one shared quartet family label

## Exact claim this packet proves

- the widened `EP10` quartet now closes as a family split:
  - windows -> structural cutout/opening
  - curtains -> weaker threshold/cutout

## Safe boundary after this packet

What is safe now:

- do stop describing this quartet as one unresolved family-verdict bundle
- do keep the window-side and curtain-side routes separate in the shared docs
- do keep object glass unselected for this quartet

Implementation mistake this packet blocks:

- flattening the widened quartet into one universal transparent-object family when the surviving windows and curtains now close through different object-side branches

## Best next step

1. Keep the quartet split frozen in the shared docs.
2. Move the transparent-object lane only if a new fixture can challenge one of these two current winners:
   - object glass
   - explicit `AlphaBlended`
3. Otherwise let this row stand as closed enough for the present inspection layer and return attention to the next unfinished P1 lane.

## Honest limit

What this packet proves:

- the widened quartet is no longer waiting on one more local tiebreak
- the surviving windows and curtains already close through different branch verdicts

What remains open:

- whether a later stronger object-glass or explicit `AlphaBlended` fixture elsewhere should reopen the broader transparent-object lane
