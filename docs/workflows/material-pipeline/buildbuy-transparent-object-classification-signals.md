# Build/Buy Transparent Object Classification Signals

This document is the signal-level companion for transparent `Build/Buy` fixture work.

Use it when the question is no longer “which transparent object should be reopened next?” and has become “which externally backed branch does this reopened object currently point to?”

Related docs:

- [Material Pipeline Deep Dives](README.md)
- [Build/Buy Material Authority Matrix](buildbuy-material-authority-matrix.md)
- [Object Glass And Transparency](family-sheets/object-glass-and-transparency.md)
- [Object Transparency Evidence Ledger](object-transparency-evidence-ledger.md)
- [Edge-Family Matrix](edge-family-matrix.md)
- [P1 Live-Proof Queue](p1-live-proof-queue.md)
- [Live-Proof Packets](live-proof-packets/README.md)
- [Open questions](../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Scope status (`v0.1`)

```text
Build/Buy Transparent Object Classification Signals
├─ Object-glass signal matrix ~ 92%
├─ Threshold/cutout signal matrix ~ 90%
├─ AlphaBlended signal matrix ~ 88%
├─ SimGlass exclusion boundary ~ 94%
└─ First live-fixture application ~ 22%
```

## Evidence order

Use signal claims in this order:

1. creator-facing object tutorials that name the shader or material fields directly
2. Sims-lineage shader vocabulary when it preserves the same family and param packet
3. object-material guidance that distinguishes in-game object behavior classes
4. reopened local fixtures only as evidence that one already-proved signal set is present
5. current repo behavior only as implementation boundary

## Signal table

| Candidate branch | Strongest positive signals | Strongest negative or cautionary signals | Current safest reading |
| --- | --- | --- | --- |
| object-side glass | explicit `GlassForObjectsTranslucent`; glass-oriented object workflow; glass-family params like `RefractionDistortionScale` or `Transparency` surviving together | `AlphaMap` alone is not enough; transparent naming alone is not enough | prefer when the reopened fixture surfaces explicit object-glass shader or glass-family parameter packet |
| threshold/cutout transparency | `AlphaMap` plus `AlphaMaskThreshold`; `AlphaThresholdMask`; creator workflow describes holes or invisible texture sections rather than semi-transparent surfaces | do not upgrade to object-glass only because the object is visually glass-like | prefer when the reopened fixture shows threshold-style alpha controls without stronger glass-family signals |
| `AlphaBlended` object transparency | explicit `AlphaBlended`; creator workflow describes semi-transparent object surfaces such as curtains | do not collapse into threshold/cutout only because alpha-bearing textures exist | prefer when the reopened fixture surfaces the blended-transparency shader path directly |
| `SimGlass` | direct character-side shader naming from external tool packets; GEOM-side family evidence; creator guidance for CAS glasses, lashes, hair, and similar thin transparent layered content | object tutorials naming `GlassForObjectsTranslucent`, `AlphaMap`, `AlphaMaskThreshold`, or `AlphaBlended` are stronger for `Build/Buy` than generic reuse of the `SimGlass` label | last-choice label for a `Build/Buy` transparent fixture unless the reopened object truly surfaces glass-family evidence that does not fit the object-side branches |

## Object-side glass signals

Strongest current signals:

- explicit `GlassForObjectsTranslucent`
- glass-oriented object workflow in `Model LOD` material entries
- glass-family helper names such as `RefractionDistortionScale`
- `Transparency` surviving next to the glass-family shader packet

Safe reading:

- if a reopened fixture shows explicit object-glass shader naming, that outranks generic transparent-object naming
- `AlphaMap` may still be present, but it is not the deciding signal by itself

## Threshold and cutout signals

Strongest current signals:

- `AlphaMap` plus `AlphaMaskThreshold`
- `AlphaThresholdMask`
- creator guidance describing holes, invisible texture sections, or threshold behavior rather than semi-transparent surfaces

Safe reading:

- threshold-style transparency is the current safest home when the reopened fixture exposes alpha-threshold helpers but no stronger object-glass or blended-transparency signal

## `AlphaBlended` signals

Strongest current signals:

- explicit `AlphaBlended`
- creator guidance describing semi-transparent object surfaces rather than hard holes

Safe reading:

- use `AlphaBlended` as its own branch when it is named directly
- do not collapse it into threshold/cutout just because alpha textures are present

## `SimGlass` exclusion boundary

What is already strong enough:

- `SimGlass` remains a real family
- but its strongest current external packet is still character-side, not object-side

Safe exclusion rule:

- a `Build/Buy` transparent fixture should not be classified as `SimGlass` while a stronger object-side signal set is present
- for `Build/Buy`, `SimGlass` is currently an exclusion-last branch, not the default glass label

## Current decision order

When the first transparent-object fixture reopens, classify it in this order:

1. explicit `GlassForObjectsTranslucent` or glass-family object signals
2. threshold/cutout signal set
3. explicit `AlphaBlended`
4. only then consider `SimGlass`

Safe reading:

- this is a decision order for the current evidence base
- it is not a permanent claim about every future TS4 asset family
- direct source-backed versus bounded synthesis claims for this decision stack are now split in [Object Transparency Evidence Ledger](object-transparency-evidence-ledger.md)

## Current implementation boundary

Current repo behavior is useful only as boundary evidence:

- it can tell us whether a reopened fixture exposes some subset of these signals
- it cannot redefine which signal set is authoritative

## Recommended next work

1. Use this table when `displayShelf` is reopened first.
2. Record whichever signal set survives the first stable reopen.
3. Promote that reopen into the first stable transparent-object fixture only if it also passes the current fixture-promotion boundary.
