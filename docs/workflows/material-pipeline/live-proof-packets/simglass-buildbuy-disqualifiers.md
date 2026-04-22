# SimGlass Build/Buy Disqualifiers

This packet freezes the conditions that block a reopened `Build/Buy` transparent fixture from staying under `SimGlass`.

Question:

- which currently documented outcomes are strong enough to disqualify a reopened `Build/Buy` transparent fixture from the `SimGlass` row even if the object still looks glass-like?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Edge-Family Matrix](../edge-family-matrix.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [SimGlass Build/Buy Evidence Limit](simglass-buildbuy-evidence-limit.md)
- [SimGlass Build/Buy Promotion Gate](simglass-buildbuy-promotion-gate.md)
- [Build/Buy Transparent Object Classification Signals](../buildbuy-transparent-object-classification-signals.md)
- [Build/Buy Transparent Object Fallback Ladder](../buildbuy-transparent-object-fallback-ladder.md)
- [Object Glass And Transparency](../family-sheets/object-glass-and-transparency.md)

## Scope status (`v0.1`)

```text
SimGlass Build/Buy Disqualifiers
├─ Object-side stronger-branch disqualifiers ~ 94%
├─ Weak-evidence disqualifiers ~ 92%
├─ Generic-fallback disqualifier ~ 93%
└─ First real disqualified fixture ~ 15%
```

## Core rule

A reopened `Build/Buy` transparent fixture is disqualified from `SimGlass` as soon as a stronger or safer explanation survives.

Safe reading:

- disqualification does not mean the fixture is unimportant
- it means the fixture belongs under a stronger transparent-object branch or must remain provisional

## Strong disqualifiers

The fixture is disqualified from `SimGlass` if any of the following survives as the stronger reading:

1. explicit object-glass branch
   - `GlassForObjectsTranslucent`
   - glass-family object shader naming
   - stronger object-glass parameter packet
2. threshold/cutout branch
   - `AlphaMap` plus `AlphaMaskThreshold`
   - `AlphaThresholdMask`
   - threshold-style object reading
3. explicit `AlphaBlended`
   - direct blended-transparency naming

Safe reading:

- once one of these wins, `SimGlass` loses
- no extra “glass-like appearance” argument can override that loss

## Weak-evidence disqualifiers

The fixture is also disqualified from `SimGlass` if:

- the root is not restart-safely reopenable
- the object identity remains unstable
- the material-candidate inspection is too weak to compare branches honestly
- the only positive evidence is aggregate survey presence
- the only positive evidence is route membership inside the transparent-decor cluster
- the only positive evidence is visual resemblance to glass

Safe reading:

- these are not partial wins for `SimGlass`
- they are non-promotable states

## Generic-fallback disqualifier

The fixture is disqualified from `SimGlass` if the strongest surviving reading is only:

- “transparent object”
- “glass-like object”
- “probably glass-family”
- any other generic transparent provisional wording

Safe reading:

- generic transparent fallback keeps route continuity
- it does not preserve a `SimGlass` win

## Exact claim this packet is making

Exact target claim:

- the current workspace already knows which outcomes are strong enough to block a `Build/Buy SimGlass` promotion

Why this matters:

- it prevents weak reopens from being reported as “almost-`SimGlass`”
- it keeps the first real `SimGlass` win narrow and defensible

## Honest limit

This packet does not prove a disqualified live fixture yet.

What it does prove:

- the losing conditions are now explicit
- a future reopened transparent object can now be blocked from `SimGlass` cleanly instead of drifting into ambiguous wording
