# SimGlass Build/Buy Outcome Ladder

This packet freezes the only currently acceptable verdicts after a `Build/Buy` transparent fixture is reopened and compared against the `SimGlass` branch.

Question:

- after a transparent `Build/Buy` fixture reopens, what final outcome labels are currently allowed, and which fuzzy intermediate verdicts should be rejected?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [SimGlass Build/Buy Promotion Gate](simglass-buildbuy-promotion-gate.md)
- [SimGlass Build/Buy Disqualifiers](simglass-buildbuy-disqualifiers.md)
- [SimGlass Build/Buy Winning Signals](simglass-buildbuy-winning-signals.md)
- [SimGlass Build/Buy Mixed-Signal Resolution](simglass-buildbuy-mixed-signal-resolution.md)
- [SimGlass Build/Buy Provisional Candidate Checklist](simglass-buildbuy-provisional-candidate-checklist.md)
- [SimGlass Build/Buy Winning Fixture Checklist](simglass-buildbuy-winning-fixture-checklist.md)
- [Build/Buy Transparent Object Reopen Checklist](buildbuy-transparent-object-reopen-checklist.md)

## Scope status (`v0.1`)

```text
SimGlass Build/Buy Outcome Ladder
├─ Allowed outcome set ~ 96%
├─ Fuzzy-verdict rejection ~ 95%
├─ Provisional-candidate boundary ~ 93%
└─ First real outcome application ~ 14%
```

## Allowed outcomes

After reopen, only the following verdicts are currently allowed:

1. stronger object-side branch win
   - object-glass
   - threshold/cutout
   - `AlphaBlended`
2. generic transparent provisional boundary
   - transparent object is real
   - family remains unresolved
3. provisional `SimGlass` candidate
   - stronger object-side branches have not won cleanly
   - at least one provisional positive `SimGlass` signal survives
   - winning burden is not yet fully satisfied
   - candidate should now be recorded through:
     - [SimGlass Build/Buy Provisional Candidate Checklist](simglass-buildbuy-provisional-candidate-checklist.md)
4. winning `SimGlass` fixture
   - promotion gate passes
   - disqualifiers do not trigger
   - winning checklist is satisfied

## Rejected fuzzy verdicts

The following outcome labels should now be rejected:

- “almost `SimGlass`”
- “probably `SimGlass`”
- “glass-like so likely `SimGlass`”
- “not object-glass, so maybe `SimGlass`”
- “`SimGlass` unless contradicted later”

Safe reading:

- if the evidence is not enough for a branch win, the result must stay provisional
- if the evidence is too weak even for a provisional `SimGlass` candidate, it must fall back to generic transparent or a stronger object-side branch

## Provisional `SimGlass` candidate boundary

A reopened fixture may stay as a provisional `SimGlass` candidate only if:

- stronger object-side branches did not win cleanly
- generic transparent fallback is still too weak to describe the surviving evidence honestly
- at least one current positive `SimGlass` signal remains alive

If those conditions fail:

- do not keep the fixture in a provisional `SimGlass` state
- if signals are mixed, apply:
  - [SimGlass Build/Buy Mixed-Signal Resolution](simglass-buildbuy-mixed-signal-resolution.md)

## Winning boundary

A winning `SimGlass` fixture is allowed only when all of the following are true:

- positive `SimGlass` signals survive
- stronger object-side branches are recorded as losing
- generic transparent fallback is too weak
- the branch-specific winning checklist is complete

## Exact claim this packet is making

Exact target claim:

- the current workspace now has a strict verdict ladder for `Build/Buy SimGlass` after reopen

Why this matters:

- it prevents future packets from drifting into vague “near-win” wording
- it turns the next reopen into one of a small number of restart-safe outcomes

## Honest limit

This packet does not apply the ladder to a real fixture yet.

What it does prove:

- the allowed verdict space is now explicit
