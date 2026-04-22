# Refraction Companion MATD-vs-MTST Boundary

This packet keeps the named `lilyPad` refraction fixture from drifting into a fake `MTST` story before the companion-material seam is actually inspected.

Question:

- when `sculptFountainSurface3x3_EP10GENlilyPad -> 00F643B0FDD2F1F7` is inspected at the companion-material layer, how should `MATD` versus `MTST` be recorded without overpromoting either side?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Build/Buy Material Authority Matrix](../buildbuy-material-authority-matrix.md)
- [Build/Buy Stateful Material-Set Seam](../buildbuy-stateful-material-set-seam.md)
- [Refraction Evidence Ledger](../refraction-evidence-ledger.md)
- [Refraction Bridge Fixture Boundary](../refraction-bridge-fixture-boundary.md)
- [Refraction Companion-Material Outcome Ladder](refraction-companion-material-outcome-ladder.md)
- [RefractionMap Live Proof](refractionmap-live-proof.md)

## Scope status (`v0.1`)

```text
Refraction Companion MATD-vs-MTST Boundary
├─ Direct-MATD preservation ~ 95%
├─ Meaningful-MTST preservation ~ 92%
├─ False-MTST inflation prevention ~ 96%
└─ Exact state/material closure ~ 24%
```

## Safe boundary

The next `lilyPad` inspection must preserve these rules:

- direct `MATD` is the stronger reading if the seam reaches `MATD` cleanly and no meaningful `MTST` role is shown
- `MTST` is only the stronger reading if it is explicitly surfaced as meaningful at the companion-material seam
- a named `Build/Buy` bridge fixture is not enough by itself to assume that `MTST` must matter

## Why this matters

The broader object/material chain externally allows both:

- `MLOD -> MATD`
- `MLOD -> MTST`

But that does not mean every named edge-family fixture is automatically an `MTST`-meaningful case.

For this refraction fixture, the current safe reading is still:

- `MATD` may be direct
- `MTST` may be meaningful
- both remain inspection outcomes, not assumptions

## Allowed recordings

Safe recordings for the next seam result:

1. direct `MATD`, no meaningful `MTST` shown
2. direct `MATD` plus meaningful `MTST`
3. meaningful `MTST` without a stronger direct `MATD` conclusion
4. neither side strong enough yet

## Unsafe shortcut

Do not record:

- “this is an `MTST` fixture now” only because the external chain allows `MTST`
- “`MATD` no longer matters” only because the family is narrow or projective

## What remains open

- whether the named refraction fixture is really a direct-`MATD` case, a meaningful-`MTST` case, or a mixed case
- whether any `MTST` that appears is semantically relevant or only structurally present
