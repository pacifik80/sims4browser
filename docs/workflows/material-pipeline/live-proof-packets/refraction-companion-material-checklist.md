# Refraction Companion-Material Checklist

This packet fixes the minimum evidence checklist for the next `lilyPad` companion-material inspection run.

Question:

- what exactly must be recorded at the `lilyPad -> 00F643B0FDD2F1F7` seam before the docs are allowed to say anything stronger than “named refraction bridge fixture”?

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
Refraction Companion-Material Checklist
├─ Named fixture identity capture ~ 97%
├─ Companion-material evidence fields ~ 94%
├─ Bridge-versus-closure discipline ~ 96%
└─ Exact slot closure ~ 22%
```

## Required checklist

The next inspection should explicitly record:

1. named object identity
2. bridge root identity
3. model and `MLOD` evidence
4. whether the seam resolves through direct `MATD`, meaningful `MTST`, or only adjacent projective helpers
5. whether family-local refraction names surface directly or only remain indirect
6. which of the allowed outcome-ladder results applies
7. what still blocks exact slot closure

## Minimum fields

- `object row = sculptFountainSurface3x3_EP10GENlilyPad`
- `bridge root = 01661233:00000000:00F643B0FDD2F1F7`
- `OBJD candidate = 01661233:00000000:FDD2F1F700F643B0`
- evidence that the seam still sits on the external `OBJD/COBJ -> Model -> MLOD -> MATD/MTST` chain
- explicit note whether `MATD` is direct, `MTST` is meaningful, or the result stays at adjacent helper level
- explicit note that `tex1` semantics remain open unless directly justified

## Promotion boundary

This checklist is enough to promote the fixture from:

- named refraction bridge fixture

to:

- named refraction companion-material inspection result

It is not enough by itself to promote the fixture to:

- exact `RefractionMap` slot closure

## Unsafe shortcut

Do not skip from:

- confirmed bridge fixture

to:

- solved refraction-family material contract

without filling this checklist.
