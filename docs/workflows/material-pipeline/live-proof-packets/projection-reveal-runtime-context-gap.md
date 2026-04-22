# Projection-Reveal Runtime Context Gap

This packet records the current ceiling of the checked-in DX11 runtime captures for the narrowed projection/reveal helper branch.

Question:

- do the current checked-in broad captures already separate the leading `F04` `srctex + tex` packet by scene or context strongly enough to promote a context-bound projection/reveal family reading?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Projection, Reveal, And Lightmap Families](../family-sheets/projection-reveal-lightmap.md)
- [Projection-Reveal Runtime Cluster Candidate Floor](projection-reveal-runtime-cluster-candidate-floor.md)
- [Runtime Helper-Family Clustering Floor](../runtime-helper-family-clustering-floor.md)
- [group-compare-broad-vs-20260421-220041.md](../../../satellites/ts4-dx11-introspection/captures/live/reports/group-compare-broad-vs-20260421-220041.md)
- [projection reveal runtime context gap snapshot](../../../tmp/projection_reveal_runtime_context_gap_snapshot_2026-04-22.json)

## Scope status (`v0.1`)

```text
Projection-Reveal Runtime Context Gap
├─ Umbrella-family identity floor ~ 63%
├─ Broad-capture persistence floor ~ 84%
├─ srctex packet parity ceiling ~ 82%
├─ Context-tagged capture availability ~ 31%
└─ Exact scene-bound ownership ~ 12%
```

## What this packet is for

The previous runtime-cluster packet already narrowed the remaining projection/reveal helper branch from:

- a broad `F03/F04/F05` middle bucket

to:

- one stable `F04` `srctex + tex` packet

This packet answers the next narrower question:

- whether the current checked-in broad captures already contain enough scene/context separation to go farther without another tagged capture run

## Local snapshot of external tooling

Current bounded snapshot:

- `tmp/projection_reveal_runtime_context_gap_snapshot_2026-04-22.json`

Broad comparison captures checked here:

- `20260421-212139`
- `20260421-212533`
- `20260421-220041`

Useful checked-in comparison layer:

- [compare-20260421-212533-vs-20260421-220041.md](../../../satellites/ts4-dx11-introspection/captures/live/reports/compare-20260421-212533-vs-20260421-220041.md)
- [group-compare-broad-vs-20260421-220041.md](../../../satellites/ts4-dx11-introspection/captures/live/reports/group-compare-broad-vs-20260421-220041.md)

## What the current broad captures do prove

Representative `F04` `srctex + tex` hashes persist across all three checked broad captures:

- `fb1c5cb93d1af69d2056dc812eac97efd231f177a6c6691f106622fe7afcc45d`
- `5dda2c6adf1652635de15dd38357b633cec3fc009f91e3950fb8fe205ec2074f`
- `dfc13424296e6d0b825afca3ace4d3669239c2254cbee935a9f74122efef3f39`

The repeated packet is structurally stable too:

- resources:
  - `sampler_srctex`
  - `sampler_tex`
  - `srctex`
  - `tex`
  - `Constants`
- shared variables:
  - `fsize`
  - `offset`
  - `scolor`
  - `srctexscale`
  - `texscale`
- richer member:
  - `scolor2`
- shared inputs:
  - `TEXCOORD0`
  - `TEXCOORD1`
  - `TEXCOORD2`

Safe reading:

- the narrowed `srctex` packet is a real recurring part of the current broad runtime surface
- the current broad sessions preserve candidate stability
- the current broad sessions still do not create a capture-level split between this packet and one specific scene class

## What the current broad captures do not prove

Current manifests still expose runtime/session metadata only:

- session id
- timestamps
- binary paths
- frame count

They do not currently carry:

- scene labels
- capture-purpose labels
- reveal-heavy versus refraction-adjacent labels
- explicit `Build/Buy` versus `CAS` context tags

Safe reading:

- the checked-in broad capture corpus is good enough to freeze the current ceiling
- it is not yet context-tagged enough to bind the stable `srctex` packet to one projection/reveal scene class

## Why this matters

Without this packet, the next move still looks like:

- inspect more of the same broad captures

With this packet, the next move is narrower and more honest:

- stop expecting broad untagged sessions to close scene-specific ownership
- keep the current `F04` `srctex + tex` narrowing
- require one context-tagged capture step before stronger promotion

## Best next inspection step

1. Keep [Projection-Reveal Runtime Cluster Candidate Floor](projection-reveal-runtime-cluster-candidate-floor.md) as the narrowed runtime bridge.
2. Keep [Projection, Reveal, And Lightmap Families](../family-sheets/projection-reveal-lightmap.md) as the umbrella identity boundary.
3. Run one context-tagged projection/reveal or refraction-adjacent capture and check the stable `F04` packet before widening into other helper families.

## Honest limit

This packet does not yet prove:

- whether the `srctex` packet is specifically `RefractionMap`
- whether it is reveal-only, projection-only, or one intermediate-combine subfamily
- exact draw/pass ownership or matrix semantics

What it does prove:

- the checked-in broad runtime corpus already has a real ceiling for this branch
- that ceiling is not lack of runtime data in general
- that ceiling is lack of scene/context tagging needed to promote the narrowed `F04` `srctex + tex` packet into stronger family ownership
