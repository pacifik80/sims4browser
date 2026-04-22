# Build/Buy Transparent Object Fixture Promotion Boundary

This packet freezes the minimum threshold for promoting one reopened transparent-object candidate into the first stable `Build/Buy` transparent fixture.

Question:

- does the current workspace already know enough to say what counts as “good enough” reopen evidence for the first transparent-object live fixture?

Related docs:

- [Build/Buy Transparent-Decor Route](buildbuy-transparent-decor-route.md)
- [Build/Buy Transparent Object Target Priority](buildbuy-transparent-object-target-priority.md)
- [Build/Buy Transparent Object Classification Boundary](buildbuy-transparent-object-classification-boundary.md)
- [Build/Buy Window-Heavy Transparent Negative Control](buildbuy-window-heavy-transparent-negative-control.md)
- [Object Glass And Transparency](../family-sheets/object-glass-and-transparency.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)

## Scope status (`v0.1`)

```text
Build/Buy Transparent Object Fixture Promotion Boundary
├─ Minimum reopen threshold ~ 94%
├─ Classification-after-reopen rule ~ 96%
└─ First stable live-fixture closure ~ 21%
```

## What is already strong enough

The current workspace already has four pieces of the route:

1. a transparent-object route
2. an internal reopen order
3. a classification boundary
4. a lower-priority window-heavy negative control

What was still missing:

- one explicit rule for when a reopened root stops being only a search anchor and becomes a stable transparent-object live fixture

## Minimum promotion threshold

The first reopened transparent-object candidate should be promoted only when all of the following are true:

1. the root reopens as a stable object-side fixture rather than only as a survey-backed transformed root
2. object identity is preserved strongly enough to keep the fixture restart-safe
3. the fixture is classified against the current transparent-object family split
4. the packet can state which branch currently wins:
   - object-side glass
   - threshold/cutout transparency
   - `AlphaBlended`
   - or, only if evidence really points there, `SimGlass`

Safe reading:

- a reopen without classification is not enough
- a classification guess without stable reopen is not enough
- the first stable live fixture must satisfy both the object-side reopen threshold and the family-classification threshold

## What is not required yet

The first stable transparent-object fixture does not need to close:

- exact final slot semantics for the winning family
- every variant or swatch of the object
- the whole transparent-object branch

Safe minimum:

- one stable reopened fixture
- one explicit current family classification
- one restart-safe path back to the same fixture

## Exact target claim for this packet

- the current workspace already has enough route structure to define a minimum promotion threshold for the first stable transparent-object fixture

## Best next step after this packet

1. Reopen `displayShelf`.
2. If reopen stays unstable, continue through the current target order.
3. Promote the first candidate only when it satisfies the minimum promotion threshold above.
4. Keep all earlier candidates below that threshold as route evidence, not as closed fixtures.

## Honest limit

What this packet proves:

- the transparent-object route now has an explicit promotion boundary for its first stable fixture

What remains open:

- the first real candidate that crosses that boundary
