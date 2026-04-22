# SortLayer Census Baseline

This document records the first direct whole-index `sort_layer` census for parsed `CASPart` facts.

Related docs:

- [CAS Carrier Census Baseline](cas-carrier-census-baseline.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [CompositionMethod And SortLayer Boundary](compositionmethod-sortlayer-boundary.md)
- [CompositionMethod Census Baseline](compositionmethod-census-baseline.md)
- [Overlay And Detail Family Authority Table](overlay-detail-family-authority-table.md)
- [Skintone And Overlay Compositor](skintone-and-overlay-compositor.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
SortLayer Census Baseline
├─ Direct sort_layer counts ~ 96%
├─ Known-slot sort_layer patterns ~ 89%
├─ Unresolved body-type concentration ~ 78%
├─ CompositionMethod cross-check boundary ~ 95%
└─ Family-specific visual interpretation ~ 46%
```

## What this baseline is for

- separate direct counted `sort_layer` facts from weaker compositor guesses
- show which values dominate the currently parsed `CASPart` layer
- keep the `sort_layer` story aligned with the newer direct `CompositionMethod` census instead of smoothing the two layers together

## Direct whole-index counts

Source:

- [sortlayer_census_fullscan.json](../../../tmp/sortlayer_census_fullscan.json)

Current direct counts from the full shard set:

- `ParsedCasPartRows = 299028`
- `SortLayerZeroRows = 99683`
- `SortLayerPositiveRows = 196920`
- `SortLayerNegativeRows = 2425`
- `DistinctSortLayerValues = 493`
- `SortLayerMin = -1929379840`
- `SortLayerMax = 2063597568`

Current safe reading:

- `sort_layer` is not a rare edge field
- non-zero values dominate the currently parsed fact layer
- the field already has enough direct coverage to matter for ordering work
- extreme negative and very large positive values exist in the live index and should not be flattened into a tiny hand-written range

## Top direct values

Top direct `sort_layer` values across the full parsed fact layer:

| `sort_layer` | Count |
| --- | ---: |
| `0` | `99683` |
| `65536` | `44610` |
| `16000` | `35418` |
| `12000` | `23497` |
| `14000` | `16638` |
| `1536` | `13708` |
| `1792` | `8197` |
| `17100` | `6677` |
| `1280` | `6353` |
| `10700` | `4999` |

Safe reading:

- `0` is common, but it is not the majority
- a small set of repeated non-zero values carries most of the parsed fact layer
- `65536`, `16000`, `12000`, and `14000` are especially important priority values for later compositor work

## Known-slot patterns

Direct top values for the readable slot families already present in `cas_part_facts`:

| Slot | Strongest current values |
| --- | --- |
| `Hair` | `12000 = 22579`; then `11000 = 54`; `65536 = 36` |
| `Head` | `1000 = 182`; then `65536 = 12` |
| `Full Body` | `65536 = 11486`; `16000 = 11135` |
| `Top` | `16000 = 18037`; `65536 = 14108`; `13000 = 2196`; `12000 = 555` |
| `Bottom` | `14000 = 12577`; `65536 = 9954`; `10400 = 572` |
| `Shoes` | `10700 = 4384`; `65536 = 3176` |
| `Accessory` | `17100 = 4316`; `65536 = 1336` |

Safe reading:

- several worn-slot families already show stable dominant `sort_layer` values
- `Hair`, `Shoes`, and `Accessory` are especially clean on the current parsed layer
- `Full Body`, `Top`, and `Bottom` keep a stronger shared `65536`/`16000`/`14000` overlap and should not be overread as if each slot already had one universal value

## Largest unresolved body-type buckets

The biggest still-unresolved category is:

- `Body Type 1140850688 = 113301` rows total

Its top direct `sort_layer` values are:

| `sort_layer` | Count |
| --- | ---: |
| `0` | `67520` |
| `1536` | `13192` |
| `1792` | `7861` |
| `16000` | `5958` |
| `1280` | `5747` |
| `14000` | `3764` |

Two more large unresolved categories also matter:

- `Body Type 1090519040`: strongest values `0 = 7065`, `15616 = 822`, `1536 = 309`, `1280 = 306`
- `Body Type 1090519046`: strongest values `0 = 2772`, `1023410176 = 336`, `905969664 = 156`, `1280 = 129`

Safe reading:

- the unresolved body-type layer is not noise
- a large share of the current `sort_layer` story still sits in body-type buckets that are not yet translated into clear family names
- that unresolved naming layer is now a better priority target than repeating general prose about compositor order

## Honest boundary against `CompositionMethod`

Current direct data does include:

- `sort_layer` in the index table `cas_part_facts`
- `sortLayer` in the current `Ts4CasPart` parser
- a direct whole-install `CompositionMethod` census in [CompositionMethod Census Baseline](compositionmethod-census-baseline.md)

Current direct data does not include:

- `composition_method` in `cas_part_facts`
- a repopulated shard set where `composition_method` is queryable from the current SQLite cache

Why this matters:

- [TS4SimRipper PreviewControl.cs](../../references/external/TS4SimRipper/src/PreviewControl.cs) treats both fields as real compositor inputs
- the repo now has direct counted layers for both fields, but they still come from two different evidence paths:
  - `sort_layer` from `cas_part_facts`
  - `CompositionMethod` from a direct package walk
- so later SQLite-based work still needs one cache repopulation step before both fields live in the same shard-backed query layer

## Recommended next work

1. Repopulate the shard set so `composition_method` is queryable next to `sort_layer`.
2. Translate the largest unresolved body-type buckets before treating the current readable-slot patterns as the whole compositor story.
