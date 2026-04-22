# Build/Buy Transparent Object Mixed-Signal Resolution

This packet freezes the current tie-break rules for transparent `Build/Buy` fixtures that surface more than one signal set at reopen time.

Question:

- does the current workspace already know enough to resolve mixed transparent-object signals without collapsing back into generic transparency wording?

Related docs:

- [Build/Buy Transparent Object Classification Signals](../buildbuy-transparent-object-classification-signals.md)
- [Build/Buy Transparent Object Fixture Promotion Boundary](buildbuy-transparent-object-fixture-promotion-boundary.md)
- [Build/Buy Transparent-Decor Route](buildbuy-transparent-decor-route.md)
- [Object Glass And Transparency](../family-sheets/object-glass-and-transparency.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)

## Scope status (`v0.1`)

```text
Build/Buy Transparent Object Mixed-Signal Resolution
├─ Mixed-signal tie-break rules ~ 93%
├─ Transparent-family contradiction handling ~ 91%
└─ First live-fixture application ~ 24%
```

## Why this packet exists

The current workspace already has:

1. a transparent-object route
2. a stable reopen order
3. a classification signal table
4. a promotion threshold for the first stable fixture

What was still missing:

- one explicit rule for what to do when a reopened fixture shows more than one positive signal set

## Current tie-break rules

### Rule 1. Explicit object-glass shader naming wins over generic alpha helpers

If the reopened fixture surfaces explicit object-glass shader naming such as `GlassForObjectsTranslucent`, that outranks:

- `AlphaMap` by itself
- threshold-style alpha vocabulary by itself
- generic transparent naming

Safe reading:

- object-glass may still carry alpha-bearing helpers
- those helpers do not demote it automatically into threshold/cutout transparency

### Rule 2. Threshold/cutout wins over generic “looks like glass”

If the reopened fixture surfaces:

- `AlphaMap` plus `AlphaMaskThreshold`, or
- `AlphaThresholdMask`

and does not surface stronger explicit object-glass or `AlphaBlended` naming, keep it in threshold/cutout transparency even if:

- the object name looks glass-like
- the object visually resembles glass

### Rule 3. Explicit `AlphaBlended` wins over generic alpha interpretation

If the reopened fixture surfaces explicit `AlphaBlended`, keep it in the blended-transparency branch unless an even stronger explicit object-glass signal is present.

Safe reading:

- blended transparency is not merely “threshold plus softer visuals”
- it remains its own named object-side branch

### Rule 4. `SimGlass` stays excluded while stronger object-side signals survive

If any stronger object-side transparent signal set survives, do not classify the reopened `Build/Buy` fixture as `SimGlass`.

Current strongest exclusion cases:

- explicit `GlassForObjectsTranslucent`
- explicit `AlphaBlended`
- threshold/cutout signal set with no stronger contradiction

### Rule 5. Contradictory signals must be recorded, not erased

If a reopened fixture shows conflicting signals, the packet should record both:

- the currently winning branch
- the contradictory losing signal set

Safe reading:

- mixed signals do not justify dropping back to “generic transparency”
- they justify a narrower “current best classification, with contradiction noted” statement

## Minimum mixed-signal output shape

When a reopened transparent-object fixture is mixed, the packet should record:

1. the winning current branch
2. the losing or contradictory branch
3. the exact signal that made the winner stronger
4. whether the contradiction is strong enough to keep the fixture provisional

## Exact target claim for this packet

- the current workspace already has enough external signal structure to resolve mixed transparent-object cases without collapsing them into generic transparency or prematurely using `SimGlass`

## Best next step after this packet

1. Reopen `displayShelf` first.
2. Apply the existing signal table.
3. If multiple signal sets survive, apply the tie-break rules above.
4. Promote the fixture only if it also satisfies the current fixture-promotion boundary.

## Honest limit

What this packet proves:

- mixed transparent-object signals now have an explicit tie-break policy

What remains open:

- the first real reopened fixture that actually requires these tie-break rules
