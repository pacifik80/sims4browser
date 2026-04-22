# Build/Buy Transparent Object Candidate State Ladder

This packet freezes the current lifecycle ladder for transparent-object candidates.

Question:

- does the current workspace already know which states a transparent-object candidate should pass through before it becomes the first stable live fixture?

Related docs:

- [Build/Buy Transparent-Decor Route](buildbuy-transparent-decor-route.md)
- [Build/Buy Transparent Object Target Priority](buildbuy-transparent-object-target-priority.md)
- [Build/Buy Transparent Object Reopen Checklist](buildbuy-transparent-object-reopen-checklist.md)
- [Build/Buy Transparent Object Fixture Promotion Boundary](buildbuy-transparent-object-fixture-promotion-boundary.md)
- [Build/Buy Transparent Object Mixed-Signal Resolution](buildbuy-transparent-object-mixed-signal-resolution.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)

## Scope status (`v0.1`)

```text
Build/Buy Transparent Object Candidate State Ladder
├─ Candidate lifecycle states ~ 96%
├─ Restart-safe transition rules ~ 95%
└─ First live-fixture application ~ 24%
```

## Current state ladder

Use the following states in order:

1. `survey-backed search anchor`
2. `priority reopen target`
3. `reopened candidate`
4. `classified reopened candidate`
5. `provisional transparent fixture`
6. `stable transparent live fixture`

## State meanings

### 1. `survey-backed search anchor`

What it means:

- the root exists in survey and candidate-resolution output
- no stable reopen has been achieved yet

Current examples:

- `displayShelf`
- `shopDisplayTileable`
- `mirror`
- `lantern`
- `fishBowl`

### 2. `priority reopen target`

What it means:

- the search anchor is now ranked inside the current route
- it has a justified order in the reopen queue

Current order:

1. `displayShelf`
2. `shopDisplayTileable`
3. `mirror`
4. `lantern`
5. `fishBowl`

### 3. `reopened candidate`

What it means:

- the object reopens through a stable object-side path
- but branch classification has not yet been fixed

Safe reading:

- this is stronger than a search anchor
- it is still not a fixture

### 4. `classified reopened candidate`

What it means:

- the reopened candidate now has a current winning branch:
  - object-glass
  - threshold/cutout
  - `AlphaBlended`
  - or only if necessary `SimGlass`
- mixed signals, if any, have been recorded

Safe reading:

- classification alone still does not make it a stable fixture

### 5. `provisional transparent fixture`

What it means:

- reopen is stable
- classification is explicit
- the evidence packet is restart-safe
- but one or more promotion-threshold items may still be weak or provisional

Safe reading:

- this is the last state before stable fixture promotion

### 6. `stable transparent live fixture`

What it means:

- the candidate satisfies the current promotion boundary
- the path back to the fixture is restart-safe
- the current winning transparent-family branch is explicit

## Transition rules

### Anchor -> Priority target

Requires:

- route membership
- structural ranking inside the current cluster

### Priority target -> Reopened candidate

Requires:

- actual reopen through a stable object-side path

### Reopened candidate -> Classified reopened candidate

Requires:

- application of the current classification signals
- mixed-signal handling, if needed

### Classified reopened candidate -> Provisional fixture

Requires:

- checklist-complete evidence packet
- restart-safe writeup

### Provisional fixture -> Stable fixture

Requires:

- promotion-boundary closure

## Exact target claim for this packet

- the current workspace already has enough route structure to define a full candidate-state ladder for transparent-object fixture work

## Best next step after this packet

1. Keep `displayShelf` as the first priority reopen target.
2. If it reopens, treat it first as a reopened candidate, not as an instant fixture.
3. Move it through classification, checklist, and promotion in order.

## Honest limit

What this packet proves:

- transparent-object candidates now have an explicit lifecycle ladder

What remains open:

- the first real candidate that climbs the full ladder
