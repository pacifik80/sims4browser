# SimGlass Build/Buy Mixed-Signal Resolution

This packet freezes the tie-break rules for reopened `Build/Buy` fixtures where `SimGlass` stays alive at the same time as competing transparent-object signals.

Question:

- when a reopened `Build/Buy` transparent fixture keeps some `SimGlass`-positive evidence alive but also surfaces competing object-side transparent signals, how should the current workspace resolve that conflict?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [SimGlass Build/Buy Winning Signals](simglass-buildbuy-winning-signals.md)
- [SimGlass Build/Buy Outcome Ladder](simglass-buildbuy-outcome-ladder.md)
- [SimGlass Build/Buy Disqualifiers](simglass-buildbuy-disqualifiers.md)
- [Build/Buy Transparent Object Mixed-Signal Resolution](buildbuy-transparent-object-mixed-signal-resolution.md)

## Scope status (`v0.1`)

```text
SimGlass Build/Buy Mixed-Signal Resolution
├─ SimGlass-versus-object-side tie-break rules ~ 94%
├─ Provisional-survival rules ~ 92%
├─ False-win prevention ~ 95%
└─ First real mixed-signal application ~ 13%
```

## Core rule

Mixed signals do not automatically produce either:

- a `SimGlass` win
- or a `SimGlass` loss

Safe reading:

- mixed signals must be forced through an explicit tie-break
- if the tie-break does not produce a clean win or loss, the result stays provisional

## Tie-break rules

### Rule 1. Stronger explicit object-side naming still wins

If a reopened fixture surfaces explicit object-side branch naming strongly enough to win under the current transparent-object rules, `SimGlass` loses even if a weaker glass-family hint survives.

### Rule 2. `SimGlass` may stay provisional only if a real positive signal survives

If the reopened fixture keeps at least one current `SimGlass`-positive signal alive and no stronger object-side branch wins cleanly, the result may remain a provisional `SimGlass` candidate.

### Rule 3. `SimGlass` does not win on mixed signals alone

If contradictory object-side signals remain meaningful and the surviving `SimGlass` evidence is not decisive, the result cannot be a winning `SimGlass` fixture.

### Rule 4. Generic fallback still defeats weak mixed cases

If the surviving `SimGlass` evidence is too weak to stay narrower than generic transparent fallback, mixed signals do not save it; the result drops out of the `SimGlass` row.

## Minimum mixed-signal output shape

When a reopened fixture is mixed around `SimGlass`, record:

1. the strongest surviving `SimGlass` signal
2. the strongest competing object-side signal
3. why the competing signal did not fully win, if it did not
4. why the `SimGlass` signal did not fully win, if it did not
5. final verdict from the allowed outcome ladder

## Exact claim this packet is making

Exact target claim:

- the current workspace already has enough structure to keep mixed `SimGlass` reopen cases out of fuzzy wording

Why this matters:

- it prevents mixed reopen cases from collapsing into “probably `SimGlass`”
- it also prevents overcorrecting every mixed case into an immediate `SimGlass` loss

## Honest limit

This packet does not resolve a real mixed `SimGlass` fixture yet.

What it does prove:

- mixed `SimGlass` cases now have an explicit tie-break policy
