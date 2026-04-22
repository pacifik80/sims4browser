# SimGlass Build/Buy Promotion Gate

This packet freezes the burden of proof for the first `Build/Buy` fixture that tries to remain under `SimGlass`.

Question:

- what exact evidence must a reopened `Build/Buy` transparent fixture satisfy before the workspace is allowed to keep it under `SimGlass` instead of object-side transparency?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Edge-Family Matrix](../edge-family-matrix.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [SimGlass Versus Shell Baseline](simglass-vs-shell-baseline.md)
- [SimGlass Build/Buy Evidence Limit](simglass-buildbuy-evidence-limit.md)
- [SimGlass Build/Buy Disqualifiers](simglass-buildbuy-disqualifiers.md)
- [SimGlass Build/Buy Winning Signals](simglass-buildbuy-winning-signals.md)
- [SimGlass Build/Buy Outcome Ladder](simglass-buildbuy-outcome-ladder.md)
- [SimGlass Build/Buy Winning Fixture Checklist](simglass-buildbuy-winning-fixture-checklist.md)
- [SimGlass EP10 Transparent-Decor Route](simglass-ep10-transparent-decor-route.md)
- [Build/Buy Transparent Object Classification Boundary](buildbuy-transparent-object-classification-boundary.md)
- [Build/Buy Transparent Object Classification Signals](../buildbuy-transparent-object-classification-signals.md)
- [Build/Buy Transparent Object Fallback Ladder](../buildbuy-transparent-object-fallback-ladder.md)
- [Object Glass And Transparency](../family-sheets/object-glass-and-transparency.md)

## Scope status (`v0.1`)

```text
SimGlass Build/Buy Promotion Gate
├─ Winning-branch burden ~ 92%
├─ Object-side exclusion burden ~ 91%
├─ Reopened-fixture proof gate ~ 93%
├─ Generic-transparent fallback exclusion ~ 90%
└─ First live winning fixture ~ 16%
```

## External semantic floor

What is already strong enough:

- `SimGlass` is externally backed as a narrow transparent layered family
- object-side transparent branches are externally backed separately:
  - `GlassForObjectsTranslucent`
  - threshold/cutout transparency
  - `AlphaBlended`

Safe reading:

- a reopened `Build/Buy` transparent fixture does not start inside `SimGlass`
- it starts at the classification gate
- `SimGlass` is currently the winning branch only if stronger object-side branches fail

## Promotion rule

The first reopened `Build/Buy` fixture may remain under `SimGlass` only if all five gates pass:

1. `reopen gate`
   - the root reopens stably
   - object identity is restart-safe
2. `classification gate`
   - material-candidate inspection is strong enough to compare branch candidates honestly
3. `object-side exclusion gate`
   - object-glass does not explain the fixture better
   - threshold/cutout transparency does not explain the fixture better
   - `AlphaBlended` does not explain the fixture better
4. `generic fallback exclusion gate`
   - the surviving reading is still narrower than “generic transparent object”
5. `SimGlass winning gate`
   - after the losses above are recorded, `SimGlass` still explains the fixture better than the remaining branches

Safe reading:

- `SimGlass` is not the starting label
- `SimGlass` is the last surviving named label
- the losing conditions and winning record are now frozen separately too:
  - [SimGlass Build/Buy Disqualifiers](simglass-buildbuy-disqualifiers.md)
  - [SimGlass Build/Buy Winning Signals](simglass-buildbuy-winning-signals.md)
  - [SimGlass Build/Buy Outcome Ladder](simglass-buildbuy-outcome-ladder.md)
  - [SimGlass Build/Buy Winning Fixture Checklist](simglass-buildbuy-winning-fixture-checklist.md)

## What must be recorded when the gate is passed

The first winning `Build/Buy SimGlass` fixture must record all of the following:

- reopened root and stable object identity
- the stronger object-side branches that were checked
- why `GlassForObjectsTranslucent` lost
- why threshold/cutout lost
- why `AlphaBlended` lost
- why generic transparent fallback is too weak
- why `SimGlass` remains the narrowest surviving explanation

If those losses are not explicit:

- the fixture is not yet promotable into the `SimGlass` row

## What this packet blocks

This packet blocks four failure modes:

1. aggregate overread
   - `SimGlass = 5` in survey is not enough
2. route overread
   - transparent-decor candidate anchors are not enough
3. appearance overread
   - “looks glass-like” is not enough
4. weak fallback overread
   - “none of the object-side branches closed cleanly” is not enough if the only remaining reading is generic transparency

## Exact claim this packet is making

Exact target claim:

- the first `Build/Buy SimGlass` fixture requires a winning-branch proof, not only a reopened transparent object

Why this matters:

- it keeps `SimGlass` alive as a real possibility without letting it absorb weakly classified transparent objects
- it turns the next successful reopen into a decision-grade packet rather than another ambiguous route note

## Honest limit

This packet does not prove a winning `Build/Buy SimGlass` fixture.

What it does prove:

- the promotion burden is now explicit
- the first valid `Build/Buy SimGlass` promotion must be a documented branch win, not just a surviving guess
