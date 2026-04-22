# Package-Material Pass Filtering Contract

This document defines how the main browser should filter package-side material candidates against external GPU pass evidence.

Use it when the question is not:

- "what package carrier exists?"
- "what pass family exists?"

but:

- "which package-side candidates are even allowed to match which captured pass families?"
- "which pass families must be excluded before deeper ownership scoring begins?"

Related docs:

- [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md)
- [Build/Buy Material Authority Matrix](buildbuy-material-authority-matrix.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [External GPU Scene-Pass Baseline](external-gpu-scene-pass-baseline.md)
- [Package, Runtime, And Scene Bridge Boundary](package-runtime-scene-bridge-boundary.md)
- [TS4 RenderDoc Handoff](../../../satellites/ts4-dx11-introspection/docs/ts4-renderdoc-handoff.md)

## Scope status (`v0.1`)

```text
Package-Material Pass Filtering Contract
├─ Scene-domain hard filter ~ 88%
├─ Pass-class hard filter ~ 91%
├─ Positive pass-signature evidence ~ 76%
├─ Negative pass-signature rejection ~ 86%
├─ Candidate ranking contract ~ 64%
└─ Exact package-to-pass closure ~ 24%
```

## Why this exists

The project now has all three of these layers in nontrivial form:

- package-side material candidates
- runtime and external GPU pass evidence
- family-level authority docs

The missing piece for the browser is not another family essay.

It is a concrete rule for this question:

- which package-side candidates should be filtered out before expensive or ambiguous matching logic runs

This document provides that rule.

## Inputs

### Package-side candidate

The browser should build one candidate record with at least:

- `AssetDomain`
  - `BuildBuy`
  - `CasPart`
  - `SimPart`
- `CarrierKind`
  - `MATD`
  - `MTST`
  - `GEOM_MTNF`
  - `CASPFields`
- `ShaderOrProfileName`
- `TextureSlotNames`
- `TextureSlotCount`
- `ParameterNames`
- `UvHints`

### External pass evidence

The browser should build one pass-evidence record with at least:

- `SceneDomain`
  - `CAS`
  - `WorldRoom`
- `PassClass`
  - `MaterialLike`
  - `CompositorOrUi`
  - `DepthOnly`
  - `Unknown`
- `ResourceVocabulary`
- `ConstantBufferVocabulary`
- `VertexInputVocabulary`
- `RepresentativeDrawScale`

## Hard filters

These rules happen before any deeper ownership scoring.

### 1. Scene-domain filter

Use this as a hard gate:

- `BuildBuy` candidates may match only `WorldRoom` pass evidence
- `CasPart` candidates may match only `CAS` pass evidence
- `SimPart` candidates should currently match only `CAS` pass evidence until a separate checked-in live-Sim capture layer exists

Safe rule:

- do not let a `BuildBuy` material hypothesis score against `CAS` pass evidence
- do not let a `CAS` or `Sim` material hypothesis score against `WorldRoom` pass evidence

### 2. Pass-class filter

Use this as the next hard gate:

- `MaterialLike`
  - eligible for visible authored-material matching
- `Unknown`
  - reserve-only candidate
  - may stay in ranking, but cannot win over a strong `MaterialLike` match
- `CompositorOrUi`
  - exclude from normal authored-material ownership
- `DepthOnly`
  - exclude from final visible-material ownership
  - keep only as helper or geometry-participation evidence

Safe rule:

- no package-side candidate should be promoted to probable visible ownership if its best external match is `CompositorOrUi` or `DepthOnly`

## Positive evidence

These signals increase confidence that a pass is compatible with visible authored material work:

- `texture0`, `texture1`, `texture2`, ...
- `cbuffer0`
- `cbuffer2`
- `cbuffer7`
- large indexed geometry draws
- depth-adjacent color pass structure
- non-screen vertex inputs such as:
  - `POSITION`
  - `NORMAL`
  - ordinary `TEXCOORDn`

Safe reading:

- this evidence is ranking evidence
- it still does not prove exact authored ownership by itself

## Negative evidence

These signals should strongly demote or exclude normal authored-material hypotheses:

- `tex[0]`
- `tex[1]`
- generic `tex`
- `COLOR`
- `SV_Position`
- many small draws
- late screen/compositor pass structure

Safe reading:

- this is strong exclusion evidence for ordinary authored material ownership
- it is not proof that the pass is irrelevant; it is proof that it is likely not the final visible material owner

## Ranking contract

After hard filters, score only surviving candidates.

Recommended first-pass scoring:

| Signal | Score |
| --- | ---: |
| scene-domain compatible | `+3` |
| `MaterialLike` pass class | `+4` |
| `Unknown` pass class | `+1` |
| multi-`texture0..N` resource vocabulary | `+2` |
| material-like constant-buffer vocabulary | `+1` |
| geometry-like vertex inputs | `+1` |
| large indexed draw scale | `+1` |

Recommended penalties:

| Signal | Score |
| --- | ---: |
| screen/compositor resource vocabulary | `reject` |
| `DepthOnly` pass class | `reject` for visible ownership |
| scene-domain mismatch | `reject` |

Recommended result buckets:

- `reject`
- `weak candidate`
- `candidate`
- `strong candidate`

Safe rule:

- do not turn a high score into final authored ownership unless family and carrier boundaries also agree

## Domain-specific application

### `BuildBuy`

Use:

- `WorldRoom`
- `MaterialLike`

Reject:

- `CompositorOrUi`
- `DepthOnly`

This is especially useful for:

- object-side `MATD/MTST` ranking
- refraction/projective false-positive reduction
- transparent-object family work where helper/compositor passes would otherwise pollute the candidate set

### `CAS` / `Sim`

Use:

- `CAS`
- `MaterialLike`

Reject:

- `CompositorOrUi`
- `DepthOnly`

This is especially useful for:

- shell and apparel candidate ranking
- keeping clothing/body/hair-like passes separate from late screen composition
- preventing helper/compositor captures from being mistaken for authored character-material truth

## Browser-facing rules

The browser should apply these rules in this order:

1. resolve package-side authoritative candidates first
2. apply `SceneDomain` hard filter
3. apply `PassClass` hard filter
4. rank remaining pass signatures
5. only then do deeper family/carrier matching

Diagnostics should expose:

- why a candidate was rejected
- which pass family it matched best
- whether the best match was:
  - `MaterialLike`
  - `CompositorOrUi`
  - `DepthOnly`
  - `Unknown`

## Honest limit

This contract does not close:

- exact `MATD/MTST -> shader hash`
- exact authored-material ownership per pass
- exact RenderDoc-to-live shader identity normalization

It exists to improve the browser's decision quality immediately:

- fewer false positives
- better candidate ranking
- cleaner separation between visible material work and helper/compositor/depth passes
