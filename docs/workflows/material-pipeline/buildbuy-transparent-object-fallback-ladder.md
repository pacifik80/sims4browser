# Build/Buy Transparent Object Fallback Ladder

This document is the fallback companion for transparent `Build/Buy` object authority.

Use it when the question is:

- what to do when a reopened transparent-object candidate exposes only partial transparent-family evidence
- how far the docs currently allow fallback before the result becomes too weak to promote
- where “generic transparency” is still allowed as a temporary boundary and where it is no longer safe

Related docs:

- [Material Pipeline Deep Dives](README.md)
- [Build/Buy Material Authority Matrix](buildbuy-material-authority-matrix.md)
- [Build/Buy Transparent Object Authority Order](buildbuy-transparent-object-authority-order.md)
- [Build/Buy Transparent Object Classification Signals](buildbuy-transparent-object-classification-signals.md)
- [Object Glass And Transparency](family-sheets/object-glass-and-transparency.md)
- [Live-Proof Packets](live-proof-packets/README.md)

## Scope status (`v0.1`)

```text
Build/Buy Transparent Object Fallback Ladder
├─ Transparent-family fallback order ~ 93%
├─ Generic-transparency boundary ~ 96%
└─ First live-fixture application ~ 23%
```

## Core rule

For `Build/Buy` transparent objects, fallback is allowed only downward through the currently documented transparent-object branches.

Safe reading:

- do not jump directly from weak object-glass evidence to generic transparency
- do not jump directly from incomplete reopen evidence to `SimGlass`
- generic transparency is currently a last-resort implementation boundary, not a preferred research conclusion

## Current fallback ladder

Use the following order:

1. explicit object-glass branch
2. threshold/cutout transparency branch
3. explicit `AlphaBlended` branch
4. last-choice `SimGlass` consideration
5. generic transparent-object provisional boundary

## Per-step rules

### 1. Explicit object-glass branch

Prefer this when the reopened fixture exposes:

- explicit `GlassForObjectsTranslucent`
- glass-family object workflow naming
- glass-family parameter packet strong enough to survive inspection

Do not fall below this branch unless those signals are absent or contradicted strongly enough.

### 2. Threshold/cutout transparency branch

Prefer this when the reopened fixture exposes:

- `AlphaMap` plus `AlphaMaskThreshold`
- `AlphaThresholdMask`
- threshold-style creator guidance signals

Do not fall below this branch only because the object looks glass-like.

### 3. Explicit `AlphaBlended` branch

Prefer this when the reopened fixture exposes:

- explicit `AlphaBlended`

Do not treat this as generic alpha just because other alpha helpers coexist.

### 4. Last-choice `SimGlass`

Only consider this when:

- stronger object-side branches do not survive, and
- the reopened evidence still points toward a real glass-family reading rather than a generic transparent-object reading

Safe reading:

- `SimGlass` is not the default glass label for `Build/Buy`
- it is only the last named family branch currently left after stronger object-side signals fail

### 5. Generic transparent-object provisional boundary

This is the weakest currently safe fallback.

Use it only when:

- the object is stably reopened
- transparent behavior is still real
- but the surviving signal set is too weak or contradictory to promote into a stronger named branch

Safe reading:

- this state is provisional
- it is acceptable as a temporary documentation boundary
- it is not strong enough for a final family conclusion

## What generic transparency may mean

Current safest reading:

- “transparent-object provisional boundary” means:
  - object is transparent-relevant
  - object-side reopen is real
  - family naming is still unresolved

Current unsafe reading:

- do not use generic transparency when a stronger named branch still survives
- do not promote a generic transparent reading into a stable transparent live fixture unless the packet explicitly says the family remains unresolved

## Promotion interaction

Current safest rule:

- a stable live fixture prefers a named winning branch
- a generic transparent provisional reading may still be useful for route continuity
- but it is weaker than object-glass, threshold/cutout, `AlphaBlended`, or a justified `SimGlass` classification

## Exact target claim for this doc

- the current workspace already has enough external-first structure to define how transparent-object fallback should degrade before generic transparency is allowed

## Recommended next work

1. Keep this ladder paired with the transparent-object authority-order companion.
2. Use it if the first stable reopen yields incomplete or contradictory family signals.
3. Only permit generic transparent fallback after the named transparent-object branches have been checked and documented as insufficient.
