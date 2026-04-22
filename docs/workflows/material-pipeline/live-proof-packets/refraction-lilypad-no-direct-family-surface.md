# Refraction LilyPad No Direct Family Surface

This packet records the current honest negative result on the named `lilyPad` seam: the current probe does not surface direct family-local `RefractionMap` names at the companion-material layer.

Question:

- what direct family-local evidence is still missing on `sculptFountainSurface3x3_EP10GENlilyPad -> 00F643B0FDD2F1F7` even after the current companion-material probe succeeds?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Refraction Evidence Ledger](../refraction-evidence-ledger.md)
- [Refraction LilyPad Direct MATD Floor](refraction-lilypad-direct-matd-floor.md)
- [Refraction LilyPad Projective Floor Boundary](refraction-lilypad-projective-floor-boundary.md)
- [Refraction Adjacent-Helper Boundary](refraction-adjacent-helper-boundary.md)
- [RefractionMap Live Proof](refractionmap-live-proof.md)

## Scope status (`v0.1`)

```text
Refraction LilyPad No Direct Family Surface
├─ Negative-result capture ~ 96%
├─ Family-surface ceiling discipline ~ 97%
├─ Projective-floor coexistence boundary ~ 94%
└─ Exact refraction-slot closure ~ 19%
```

## Current negative result

Current `Select-String` sweep over `tmp/probe_00F6_after_projective_packed_fix.txt` is already strong enough to record:

- repeated `WorldToDepthMapSpaceMatrix`
- repeated `ProjectiveMaterialDecodeStrategy`
- no surfaced direct `RefractionMap`
- no surfaced direct `tex1`
- no surfaced direct `samplerRefractionMap`

## Safe reading

This fixture currently proves:

- named bridge identity
- direct embedded `MATD` floor
- stable projective/material floor

This fixture currently does not prove:

- direct family-local `RefractionMap` surfacing at the seam
- direct `tex1` surfacing at the seam
- direct `samplerRefractionMap` surfacing at the seam

## Why this matters

Without this negative-result packet, the docs could still drift into:

- “the refraction family is basically surfaced here already”

The current probe does not support that.

The honest reading is narrower:

- refraction family remains alive above the current floor
- but the current seam result still stops below direct family-local surface evidence

## What remains open

- whether a stronger inspection pass on the same fixture can surface direct family-local evidence
- whether another fixture is needed for that level of closure
