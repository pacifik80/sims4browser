# Build/Buy Transparent Object Authority Order

This document is the narrow companion for transparent `Build/Buy` object authority after the base object-side chain has already been accepted.

Use it when the question is:

- where transparent-object family classification sits inside the `OBJD -> MODL/MLOD -> MATD/MTST` chain
- what transparent-object evidence is strong enough to affect family choice
- which steps are authority steps and which are only live-proof or restart steps

Related docs:

- [Material Pipeline Deep Dives](README.md)
- [Build/Buy Material Authority Matrix](buildbuy-material-authority-matrix.md)
- [Build/Buy Transparent Object Fallback Ladder](buildbuy-transparent-object-fallback-ladder.md)
- [Build/Buy Transparent Object Classification Signals](buildbuy-transparent-object-classification-signals.md)
- [Object Glass And Transparency](family-sheets/object-glass-and-transparency.md)
- [Object Transparency Evidence Ledger](object-transparency-evidence-ledger.md)
- [Edge-Family Matrix](edge-family-matrix.md)
- [P1 Live-Proof Queue](p1-live-proof-queue.md)
- [Live-Proof Packets](live-proof-packets/README.md)

## Scope status (`v0.1`)

```text
Build/Buy Transparent Object Authority Order
├─ Transparent-object authority graph ~ 91%
├─ Family-classification insertion point ~ 94%
├─ Restart-safe fixture progression ~ 96%
└─ First stable live-fixture application ~ 24%
```

## What this doc is for

- make the transparent-object branch reusable without reopening the full `Build/Buy Material Authority Matrix`
- separate base object authority from transparent-family choice
- keep family classification as an object-side authority step, not a renderer-specific branch

## Transparent-object authority graph

```text
COBJ / OBJD
      ->
object-side identity and swatch anchor
      ->
MODL
      ->
MLOD
      ->
MATD / MTST candidates
      ->
transparent-object signal check
      ->
current winning transparent branch
      ->
shared shader/material contract
```

Safe reading:

- transparent-family choice happens after the object-side chain reaches material candidates
- transparent-family choice does not replace the object-side chain
- transparent-family choice still feeds the same shared shader/material contract

## Current insertion point

The current safest insertion point for transparent-object classification is:

1. after stable object-side reopen
2. after `MATD` or `MTST` material candidates are available strongly enough to inspect
3. before the fixture is promoted into a stable transparent-object live fixture

Safe reading:

- classification is not a pre-object shortcut
- classification is not a post-render cosmetic label
- it is an authority step between material candidate inspection and stable fixture promotion

## Current decision order inside the authority step

At the current evidence level, inspect transparent-object candidates in this order:

1. explicit object-glass shader or glass-family object signals
2. threshold/cutout signal set
3. explicit `AlphaBlended`
4. only then `SimGlass`

Companion source:

- [Build/Buy Transparent Object Classification Signals](buildbuy-transparent-object-classification-signals.md)
- [Build/Buy Transparent Object Fallback Ladder](buildbuy-transparent-object-fallback-ladder.md)

## Current fixture progression inside the authority step

At the current evidence level, the fixture should move through:

1. search anchor
2. priority reopen target
3. reopened candidate
4. classified reopened candidate
5. provisional transparent fixture
6. stable transparent live fixture

Companion sources:

- [Build/Buy Transparent Object Candidate State Ladder](live-proof-packets/buildbuy-transparent-object-candidate-state-ladder.md)
- [Build/Buy Transparent Object Fixture Promotion Boundary](live-proof-packets/buildbuy-transparent-object-fixture-promotion-boundary.md)

## Mixed-signal rule

If a reopened transparent-object fixture exposes more than one positive signal set:

- keep the currently winning branch explicit
- keep the losing or contradictory branch explicit
- do not collapse the result into generic transparency wording

Companion source:

- [Build/Buy Transparent Object Mixed-Signal Resolution](live-proof-packets/buildbuy-transparent-object-mixed-signal-resolution.md)

## Current route and exit boundary

Current safest route:

- the transparent-decor cluster

Current safest target order:

1. `displayShelf`
2. `shopDisplayTileable`
3. `mirror`
4. `lantern`
5. `fishBowl`

Current exit rule:

- widen back out only after the whole prioritized cluster fails to yield a stable or still-promising provisional fixture

Companion sources:

- [Build/Buy Transparent-Decor Route](live-proof-packets/buildbuy-transparent-decor-route.md)
- [Build/Buy Transparent Object Target Priority](live-proof-packets/buildbuy-transparent-object-target-priority.md)
- [Build/Buy Transparent Object Route Stall Boundary](live-proof-packets/buildbuy-transparent-object-route-stall-boundary.md)

## What remains open

- the first stable reopened transparent-object fixture that actually exercises the authority step end to end
- exact material-slot closure for the winning transparent-family branch
- whether the first stable fixture will classify into object-glass, threshold/cutout, or `AlphaBlended`

## Recommended next work

1. Keep this companion as the transparent-object authority summary instead of restating the route across many packets.
2. Use `displayShelf` as the first serious reopen target.
3. Promote the first candidate only after it crosses the current transparent-object authority step cleanly.
