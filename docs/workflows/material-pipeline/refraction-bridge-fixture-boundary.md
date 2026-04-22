# Refraction Bridge Fixture Boundary

This companion keeps the named lily-pad `Build/Buy` fixture restart-safe by separating a valid object/material inspection bridge from false closure on exact `RefractionMap` slot semantics.

Related docs:

- [Material Pipeline Deep Dives](README.md)
- [Shader Family Registry](shader-family-registry.md)
- [Edge-Family Matrix](edge-family-matrix.md)
- [P1 Live-Proof Queue](p1-live-proof-queue.md)
- [Refraction Evidence Ledger](refraction-evidence-ledger.md)
- [RefractionMap Live Proof](live-proof-packets/refractionmap-live-proof.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
Refraction Bridge Fixture Boundary
├─ Named fixture identity boundary ~ 95%
├─ Object/material seam boundary ~ 93%
├─ Bridge-versus-closure wording ~ 96%
└─ Exact slot closure ~ 24%
```

## What is already strong enough

The current packet is strong enough to say:

- `EP10\ClientFullBuild0.package | sculptFountainSurface3x3_EP10GENlilyPad`
- `OBJD` candidate `01661233:00000000:FDD2F1F700F643B0`
- `instance-swap32 -> 01661233:00000000:00F643B0FDD2F1F7`

This is now a restart-safe named bridge fixture.

It is also strong enough to say:

- the fixture sits on an externally backed `OBJD/COBJ -> Model -> MLOD -> MATD/MTST` inspection seam
- future inspection can target the companion-material layer without rescanning the broader survey

## What this fixture does not prove

This fixture does not yet prove:

- exact `RefractionMap` slot semantics
- exact `tex1` semantics
- exact visible-pass behavior
- that `instance-swap32` should be generalized into a universal Build/Buy rule

Safe reading:

- a named bridge fixture is an inspection route
- it is not yet a solved family contract

## Why this boundary matters

Without this boundary, a later restart could overread:

- survey-level `RefractionMap = 33`
- one named object linkage
- one strong adjacent projective root

into a false conclusion that refraction-family slot semantics are already basically closed.

That would be wrong.

## Safe next step

The next step stays narrow:

1. inspect the lily-pad fixture at the object/material companion seam
2. record whether the seam resolves through direct `MATD`, meaningful `MTST`, or only adjacent projective helpers
3. only then revisit `tex1` or direct slot semantics
