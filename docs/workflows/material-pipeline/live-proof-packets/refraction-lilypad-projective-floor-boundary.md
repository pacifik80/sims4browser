# Refraction LilyPad Projective Floor Boundary

This packet records the current family-level ceiling on the named `lilyPad` seam: the fixture currently surfaces a projective/material floor, not direct `RefractionMap` closure.

Question:

- how should the current `WorldToDepthMapSpaceMatrix` result on the named `lilyPad` fixture be read without demoting the refraction family or overpromoting the projective floor?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Projection, Reveal, And Generated-Light Boundary](../projection-reveal-generated-light-boundary.md)
- [Refraction Evidence Ledger](../refraction-evidence-ledger.md)
- [Refraction Adjacent-Helper Boundary](refraction-adjacent-helper-boundary.md)
- [Refraction LilyPad Direct MATD Floor](refraction-lilypad-direct-matd-floor.md)
- [RefractionMap Live Proof](refractionmap-live-proof.md)

## Scope status (`v0.1`)

```text
Refraction LilyPad Projective Floor Boundary
├─ Projective floor capture ~ 95%
├─ Refraction-family ceiling discipline ~ 96%
├─ Mixed-root comparison discipline ~ 92%
└─ Exact refraction-slot closure ~ 21%
```

## What the current probe already proves

Current local evidence from `tmp/probe_00F6_after_projective_packed_fix.txt` and sampled coverage files is strong enough to record:

- `Material families: WorldToDepthMapSpaceMatrix=1`
- `Material decode strategy: ProjectiveMaterialDecodeStrategy=1`
- the same family/strategy pair repeats for `00F643B0FDD2F1F7` in sampled coverage artifacts

## Safe reading

This fixture now safely proves:

- the named `lilyPad` seam reaches a real projective/material floor
- that floor is stable enough to be restart-safe

This does not yet prove:

- direct `RefractionMap` surfaced as the final family at the seam
- direct `tex1` closure
- that `WorldToDepthMapSpaceMatrix` should replace the refraction-family row in the docs

## Why this matters

The current result should be read as:

- projective floor reached
- refraction family still alive above that floor

Not as:

- refraction solved
- or refraction disproved

## Mixed-root control

This also keeps the weaker `0124E3B8AC7BEE62` comparison honest:

- `0124` stays a mixed/control projective root
- `00F643` stays the cleaner named projective floor
- neither result should be widened into one generic projective replacement for the refraction row
