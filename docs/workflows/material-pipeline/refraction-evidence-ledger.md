# Refraction Evidence Ledger

This companion keeps `RefractionMap` restart-safe by separating externally corroborated refraction-family semantics from local corpus/survey evidence and bounded synthesis.

Related docs:

- [Material Pipeline Deep Dives](README.md)
- [Shader Family Registry](shader-family-registry.md)
- [Edge-Family Matrix](edge-family-matrix.md)
- [P1 Live-Proof Queue](p1-live-proof-queue.md)
- [Projection, Reveal, And Lightmap Families](family-sheets/projection-reveal-lightmap.md)
- [RefractionMap Live Proof](live-proof-packets/refractionmap-live-proof.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
Refraction Evidence Ledger
├─ External corroboration labeling ~ 94%
├─ Local survey and bridge-root separation ~ 93%
├─ Bounded synthesis separation ~ 92%
└─ Exact TS4 refraction-slot closure ~ 22%
```

## Externally confirmed

What the current source pack supports directly:

- [Sims_3:Shaders](https://modthesims.info/wiki.php?title=Sims_3%3AShaders) keeps refraction-oriented families separate from ordinary surface sampling
- [Sims_3:Shaders\Params](https://modthesims.info/wiki.php?title=Sims_3%3AShaders%5CParams) keeps refraction-oriented helper semantics distinct from ordinary base-material slots
- [Sims_3:0xEA5118B0](https://modthesims.info/wiki.php?title=Sims_3%3A0xEA5118B0) exposes `Refraction Distortion Scale`, which is strong lineage support for refraction-specific helper behavior
- current external Build/Buy authority chain docs support the named object/material seam:
  - `OBJD/COBJ -> Model -> MLOD -> MATD/MTST`

Safe externally backed reading:

- `RefractionMap` stays under projection/refraction families
- `tex1` stays a family-local unresolved input until stronger TS4-facing proof appears
- a named Build/Buy fixture can prove the inspection seam without proving final slot semantics

## Local package evidence

What the local workspace adds without becoming a truth layer:

- `tmp/precomp_sblk_inventory.json` keeps a concentrated `RefractionMap` packet and a family-local `tex1` packet
- `tmp/probe_all_buildbuy_summary_full.json` raises the branch to survey-level TS4-facing presence with `RefractionMap = 33`
- `tmp/probe_00F6_after_projective_packed_fix.txt` and related coverage artifacts keep `00F643B0FDD2F1F7` as the current best named bridge root
- the current `EP10` identity pass links that root back to `sculptFountainSurface3x3_EP10GENlilyPad`
- `0124E3B8AC7BEE62` remains a weaker mixed/control root with fallback diffuse and one `FresnelOffset` LOD

Safe local reading:

- the branch is alive in the current workspace both as profile archaeology and as survey-level Build/Buy evidence
- the lily-pad root is a strong inspection bridge
- the local packet still does not prove exact refraction-slot or visible-pass math

## Bounded synthesis

What this repo can now say as a bounded conclusion:

- `RefractionMap` is stronger than a generic unresolved texture bucket
- `tex1` is stronger as a family-local unresolved refraction input than as a generic secondary surface slot
- `sculptFountainSurface3x3_EP10GENlilyPad -> 00F643B0FDD2F1F7` is the current best named refraction bridge fixture
- `0124E3B8AC7BEE62` should stay a mixed/control comparison root, not the primary bridge

This is synthesis, not a quoted external claim:

- no current source in the packet proves exact TS4 sampled slot semantics for `tex1`
- no current source in the packet proves one direct end-to-end asset where `RefractionMap` itself is surfaced by name at the final slot layer

## Still open

Not closed by this ledger:

- exact TS4 refraction-slot semantics
- exact visible-pass math for refraction-family content
- whether the lily-pad seam resolves through direct `MATD` only or through a meaningful `MTST` variant path
