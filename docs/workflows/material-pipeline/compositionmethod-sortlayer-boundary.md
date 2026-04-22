# CompositionMethod And SortLayer Boundary

This document fixes the current safest reading for `CompositionMethod` and `SortLayer` in `CAS/Sim` material work.

Related docs:

- [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md)
- [BodyType Translation Boundary](bodytype-translation-boundary.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [Body And Head Shell Authority Table](body-head-shell-authority-table.md)
- [CompositionMethod Census Baseline](compositionmethod-census-baseline.md)
- [SortLayer Census Baseline](sortlayer-census-baseline.md)
- [Skintone And Overlay Compositor](skintone-and-overlay-compositor.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
CompositionMethod And SortLayer Boundary
├─ SortLayer as real CAS metadata ~ 79%
├─ CompositionMethod as compositor-side control ~ 86%
├─ Boundary against shell selection ~ 86%
└─ Exact runtime math ~ 38%
```

## Current safe reading

- `CompositionMethod` and `SortLayer` belong to the layer-composition side of the model
- they do not choose body shell or head shell
- they matter after the relevant `CASP` part and shell-compatible material targets are already selected

Short form:

```text
choose shell or part
        ->
find material candidates
        ->
apply skintone and layer-composition controls
```

## What is already strong enough

From the current docs and code:

- [Skintone And Overlay Compositor](skintone-and-overlay-compositor.md) already treats both fields as compositor-facing controls
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md) already places them after canonical material candidates and before final compositor output
- current code already parses and carries `SortLayer` out of `CASPart` in [AssetServices.cs](../../src/Sims4ResourceExplorer.Assets/AssetServices.cs)
- current code now also parses and carries `CompositionMethod` out of `CASPart` in [AssetServices.cs](../../src/Sims4ResourceExplorer.Assets/AssetServices.cs)
- direct whole-install counts now exist in:
  - [CompositionMethod Census Baseline](compositionmethod-census-baseline.md)
  - [SortLayer Census Baseline](sortlayer-census-baseline.md)
- creator-facing practice keeps overlay/detail content in layered composition workflows, not in shell-identity selection

## Direct same-layer pair floor

The current boundary now also has one compact same-layer query packet:

- [composition_sortlayer_boundary_snapshot_2026-04-21.json](../../tmp/composition_sortlayer_boundary_snapshot_2026-04-21.json)

Current strongest whole-layer pairs from `cas_part_facts`:

- `composition=0 | sort=0 = 18668`
- `composition=32 | sort=65536 = 12212`
- `composition=0 | sort=16000 = 8970`
- `composition=0 | sort=12000 = 5960`
- `composition=0 | sort=14000 = 3750`
- `composition=0 | sort=17100 = 3503`
- `composition=4 | sort=5500 = 496`
- `composition=4 | sort=5200 = 304`

Readable-slot pairs now stay visibly different from ordinary overlay/detail rows:

- `Hair -> 0 | 12000 = 5771`
- `Head -> 0 | 1000 = 88`; `3 | 1000 = 2`
- `Full Body -> 32 | 65536 = 3437`; `0 | 16000 = 2824`
- `Top -> 0 | 16000 = 4894`; `32 | 65536 = 3715`
- `Bottom -> 0 | 14000 = 3032`; `32 | 65536 = 2943`
- `Shoes -> 0 | 10700 = 1420`; `32 | 65536 = 918`
- `Accessory -> 0 | 17100 = 2035`; `32 | 65536 = 188`

Ordinary low-value overlay/detail rows now also stay cleaner than the mixed high-byte families in the same query layer:

- `Lipstick` / `29 -> 4 | 5500 = 496`
- `Eyeshadow` / `30 -> 4 | 5200 = 304`; `4 | 2100 = 136`
- `Eyeliner` / `31 -> 0 | 5300 = 158`
- `Blush` / `32 -> 4 | 2100 = 78`
- `Facepaint` / `33 -> 0 | 7500 = 118`
- `SkinOverlay` / `58 -> 1 | 1100 = 5`
- mixed high-byte comparison rows still look much noisier:
  - `0x41000000 -> 0 | 0 = 806`; `255 | 0 = 214`
  - `0x80000000 -> 0 | 196608 = 15`; `0 | 0 = 6`

Safe reading:

- `CompositionMethod` and `SortLayer` now have one shared direct evidence layer instead of only separate baselines
- readable shell or worn-slot rows still behave like selected-part compositor lanes, not like shell selectors
- ordinary low-value overlay/detail rows remain the safest direct anchors for compositor precedence
- mixed high-byte families still belong to interpretation guards, not to the first-line overlay/detail vocabulary

## Current boundary table

| Question | Current answer |
| --- | --- |
| Do these fields choose body shell or head shell? | no |
| Do they belong on the shell-identity side of the model? | no |
| Do they belong after part and shell selection? | yes |
| Are they more relevant to overlay/detail families than to shell identity? | yes |
| Is `SortLayer` visible in the current code path? | yes |
| Is exact runtime math already implemented? | no |

## Why this matters

Without this boundary it is too easy to mix together two different questions:

1. which part becomes the body or head shell
2. in what order already selected layers are combined

The current safest model keeps them separate:

- body/head shell selection is an identity problem
- `CompositionMethod` and `SortLayer` are layer-ordering and composition controls

## Current implementation boundary

What the repo already does:

- `SortLayer` is parsed from `CASPart` and stored in structured metadata in [AssetServices.cs](../../src/Sims4ResourceExplorer.Assets/AssetServices.cs)
- `CompositionMethod` is now also parsed from `CASPart`, carried in summaries, and wired into indexing payloads in:
  - [AssetServices.cs](../../src/Sims4ResourceExplorer.Assets/AssetServices.cs)
  - [Domain.cs](../../src/Sims4ResourceExplorer.Core/Domain.cs)
  - [IndexingServices.cs](../../src/Sims4ResourceExplorer.Indexing/IndexingServices.cs)
- body/head assembly remains body-first in [SimSceneComposer.cs](../../src/Sims4ResourceExplorer.Core/SimSceneComposer.cs)

What the repo does not yet do:

- enforce exact `CompositionMethod` behavior
- drive final compositor order from `SortLayer`
- prove category-specific exceptions from live assets

## Honest limit

This document does not prove:

- exact blend math for every `CompositionMethod`
- exact numeric ordering rules for all `SortLayer` values
- exact relation between skintone passes and every overlay/detail family in the live game

It also does not erase the current cache asymmetry:

- `sort_layer` has a direct shard-backed census in [SortLayer Census Baseline](sortlayer-census-baseline.md)
- `CompositionMethod` now has a direct whole-install package census in [CompositionMethod Census Baseline](compositionmethod-census-baseline.md)
- `CompositionMethod` is now also backfilled into the shard cache, so later work can query it directly from `cas_part_facts`
- the new same-layer snapshot now makes that much clearer:
  - infrastructure is good enough for joint `CompositionMethod + SortLayer` queries
  - the remaining asymmetry is semantic, not infrastructural:
  - low-value `BodyType` rows now map cleanly to external enum names
  - the biggest high-bit `BodyType` rows still behave like mixed buckets

## Recommended next work

1. Keep using this boundary when writing per-family tables so shell selection does not get mixed with layer ordering.
2. Keep [Overlay-Detail Priority After High-Byte Stack](live-proof-packets/overlay-detail-priority-after-highbyte-stack.md) above mixed high-byte families in precedence reasoning.
3. Reopen sparse overlay rows only when a stronger external or parsed packet appears.
