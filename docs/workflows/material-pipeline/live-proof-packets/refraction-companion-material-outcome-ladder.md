# Refraction Companion-Material Outcome Ladder

This packet fixes the allowed outcome set for the named `lilyPad` refraction bridge fixture at the companion-material seam.

Question:

- when `sculptFountainSurface3x3_EP10GENlilyPad -> 00F643B0FDD2F1F7` is inspected at the `OBJD/COBJ -> Model -> MLOD -> MATD/MTST` seam, what outcomes are safe to record without pretending that exact `RefractionMap` slot semantics are already solved?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Build/Buy Material Authority Matrix](../buildbuy-material-authority-matrix.md)
- [Build/Buy Stateful Material-Set Seam](../buildbuy-stateful-material-set-seam.md)
- [Refraction Evidence Ledger](../refraction-evidence-ledger.md)
- [Refraction Bridge Fixture Boundary](../refraction-bridge-fixture-boundary.md)
- [RefractionMap Live Proof](refractionmap-live-proof.md)

## Scope status (`v0.1`)

```text
Refraction Companion-Material Outcome Ladder
├─ Allowed outcome set ~ 95%
├─ False-closure prevention ~ 96%
├─ MATD-versus-MTST recording boundary ~ 92%
└─ Exact slot closure ~ 23%
```

## Allowed outcomes

The next inspection should end only in one of these bounded outcomes:

1. direct `MATD` companion seam
2. meaningful `MTST` companion seam
3. adjacent projective-helper seam only
4. companion seam still unresolved

## Outcome meanings

### 1. Direct `MATD` companion seam

Safe reading:

- the named fixture reaches a direct `MATD` layer cleanly enough to keep inspecting refraction-family provenance there
- this still does not close exact `RefractionMap` or `tex1` slot semantics

### 2. Meaningful `MTST` companion seam

Safe reading:

- the named fixture reaches a real `MTST` layer that matters for companion-material inspection
- this still does not automatically turn the fixture into a stateful-material closure or prove refraction-family slot semantics

### 3. Adjacent projective-helper seam only

Safe reading:

- the fixture survives as a valid bridge into the projective neighborhood
- the inspection still did not reach a strong direct material-definition closure for the refraction family

### 4. Companion seam still unresolved

Safe reading:

- object identity and bridge status remain valid
- the current evidence still does not support a stronger material-layer conclusion

## Unsafe outcomes

Do not record:

- “almost closed refraction slot semantics”
- “basically `RefractionMap` now”
- “generic projective material solved”
- any verdict that skips from bridge fixture to final slot contract

## Why this matters

Without an outcome ladder, the next inspection can drift from:

- named object identity
- valid bridge seam
- family-local refraction evidence

into fake closure language.

This ladder blocks that.
