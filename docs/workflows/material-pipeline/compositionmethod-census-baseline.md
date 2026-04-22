# CompositionMethod Census Baseline

This document records the first direct whole-install `CompositionMethod` census for parsed `CASPart` resources.

Related docs:

- [BodyType Translation Boundary](bodytype-translation-boundary.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [CompositionMethod And SortLayer Boundary](compositionmethod-sortlayer-boundary.md)
- [Overlay And Detail Family Authority Table](overlay-detail-family-authority-table.md)
- [SortLayer Census Baseline](sortlayer-census-baseline.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
CompositionMethod Census Baseline
├─ Direct whole-install counts ~ 96%
├─ CompositionMethod + SortLayer pairs ~ 92%
├─ Readable slot patterns ~ 88%
├─ Unresolved body-type concentration ~ 80%
└─ Exact visual semantics per value ~ 41%
```

## What this baseline is for

- separate direct counted `CompositionMethod` facts from weaker compositor guesses
- show whether `CompositionMethod` is rare noise or a real ordering signal in live `CASPart` data
- tie `CompositionMethod` back to the already-counted `SortLayer` layer without pretending that exact blend math is solved

## Direct whole-install counts

Source:

- [compositionmethod_census_fullscan.json](../../../tmp/compositionmethod_census_fullscan.json)
- [compositionmethod_cache_backfill.json](../../../tmp/compositionmethod_cache_backfill.json)

Current direct counts from the full package run:

- `CasPartResources = 530507`
- `CasPartPackages = 414`
- `ParsedResources = 299028`
- `ZeroLengthResources = 766`
- `ParseFailures = 230713`
- `RowsWithCompositionMethodZero = 243517`
- `RowsWithCompositionMethodNonZero = 55511`
- `DistinctCompositionMethods = 59`
- `DistinctCompositionSortPairs = 564`
- `Elapsed = 00:00:22.3856896`

Current safe reading:

- `CompositionMethod` is not a tiny edge field
- non-zero values are a minority, but still large enough to matter for ordering work
- one value dominates the non-zero subset: `composition=32`
- the big integrity blocker is still the structured-parser gap, not lack of live package evidence

## Top direct values

Top direct `CompositionMethod` values across the parsed layer:

| `CompositionMethod` | Count | Package coverage |
| --- | ---: | ---: |
| `0` | `243517` | `333` |
| `32` | `44619` | `98` |
| `255` | `6642` | `136` |
| `4` | `2641` | `22` |
| `1` | `542` | `23` |
| `3` | `241` | `10` |
| `191` | `161` | `10` |
| `2` | `160` | `12` |
| `220` | `63` | `9` |
| `24` | `57` | `6` |

Safe reading:

- `composition=0` is the main default lane
- `composition=32` is the only large non-zero lane on the current parsed subset
- the values often discussed in tooling prose as special cases, such as `2`, `3`, and `4`, are real but much narrower in whole-install counts

## Top direct `CompositionMethod + SortLayer` pairs

| Pair | Count | Package coverage |
| --- | ---: | ---: |
| `composition=0 | sort=0` | `92563` | `320` |
| `composition=32 | sort=65536` | `44598` | `89` |
| `composition=0 | sort=16000` | `35418` | `231` |
| `composition=0 | sort=12000` | `23497` | `55` |
| `composition=0 | sort=14000` | `16638` | `211` |
| `composition=0 | sort=1536` | `13708` | `190` |
| `composition=0 | sort=1792` | `8197` | `165` |
| `composition=0 | sort=17100` | `6677` | `69` |
| `composition=255 | sort=0` | `6642` | `136` |
| `composition=0 | sort=1280` | `6353` | `136` |

Safe reading:

- the strongest non-zero pair is not diffuse noise; it is a stable lane: `composition=32 | sort=65536`
- the default lane remains `composition=0`, spread across several strong `sort_layer` values
- `composition=255` is real and not tiny, but it stays much narrower than the `0` and `32` lanes

## Readable slot patterns

Current direct `CompositionMethod` patterns for the readable slot families:

| Slot | Strongest current values |
| --- | --- |
| `Hair` | `composition=0 = 22633`; `composition=32 = 36` |
| `Head` | `composition=0 = 176`; `composition=32 = 12`; `composition=3 = 6` |
| `Full Body` | `composition=32 = 11486`; `composition=0 = 11165` |
| `Top` | `composition=0 = 20788`; `composition=32 = 14108` |
| `Bottom` | `composition=0 = 13187`; `composition=32 = 9954` |
| `Shoes` | `composition=0 = 4384`; `composition=32 = 3176` |
| `Accessory` | `composition=0 = 4316`; `composition=32 = 1336` |

The strongest direct slot-plus-pair patterns are:

| Slot | Strongest current pair values |
| --- | --- |
| `Hair` | `composition=0 | sort=12000 = 22579` |
| `Head` | `composition=0 | sort=1000 = 176` |
| `Full Body` | `composition=32 | sort=65536 = 11486`; `composition=0 | sort=16000 = 11135` |
| `Top` | `composition=0 | sort=16000 = 18037`; `composition=32 | sort=65536 = 14108` |
| `Bottom` | `composition=0 | sort=14000 = 12577`; `composition=32 | sort=65536 = 9954` |
| `Shoes` | `composition=0 | sort=10700 = 4384`; `composition=32 | sort=65536 = 3176` |
| `Accessory` | `composition=0 | sort=17100 = 4316`; `composition=32 | sort=65536 = 1336` |

Safe reading:

- `Hair` is overwhelmingly `composition=0`
- wearable body overlays are split between two large lanes:
  - ordinary slot-local ordering with `composition=0`
  - a strong shared lane with `composition=32 | sort=65536`
- this makes `composition=32` a real priority for clothing-like and accessory-like compositor work

## Largest unresolved body-type buckets

The biggest unresolved category is still:

- `Body Type 1140850688 = 113301` rows

Its strongest direct `CompositionMethod` values are:

| Value | Count |
| --- | ---: |
| `composition=0` | `111159` |
| `composition=255` | `1743` |

Its strongest pair values are:

| Pair | Count |
| --- | ---: |
| `composition=0 | sort=0` | `65444` |
| `composition=0 | sort=1536` | `13192` |
| `composition=0 | sort=1792` | `7861` |
| `composition=0 | sort=16000` | `5958` |
| `composition=0 | sort=1280` | `5747` |
| `composition=0 | sort=14000` | `3764` |

Two more unresolved buckets still matter:

- `Body Type 1090519040 = 12292`, mostly `composition=0 = 11011`
- `Body Type 1090519046 = 4752`, mostly `composition=0 = 4101`

Safe reading:

- the unresolved body-type layer still dominates the default `composition=0` story
- that means family-specific conclusions should not be over-read from readable slots alone
- translating the biggest unresolved body types is now a higher-value step than repeating generic compositor prose

## Honest boundary

This baseline proves:

- whole-install direct counts for `CompositionMethod`
- whole-install direct counts for `CompositionMethod + SortLayer`
- stable readable-slot patterns for the currently parsed subset

This baseline does not prove:

- exact EA visual meaning for each `CompositionMethod` value
- exact tie-break behavior inside equal `SortLayer`
- that the largest mixed `BodyType` buckets already have exact semantic names

Current repo state is now split like this:

- parser and indexing code know about `CompositionMethod`
- the whole-install census was first run directly over package bytes
- the shard cache has now also been backfilled so `cas_part_facts.composition_method` is queryable in SQLite
- the backfill summary is:
  - `TotalFactRows = 299028`
  - `MissingCompositionBefore = 232698`
  - `MissingCompositionAfter = 0`
  - `UpdatedFactRows = 232698`
  - `ParseFailures = 0`
- all four shard databases now report `seed_fact_content_version = 2026-04-21.seed-facts-v2`

Safe reading:

- direct package counts and ordinary SQL queries now agree on the same `CompositionMethod` floor
- the next blocker is no longer cache population
- the next blocker is translation of the largest mixed `BodyType` buckets

## Recommended next work

1. Translate the largest unresolved `Body Type` buckets before over-reading the dominant `composition=0` lane.
2. Use [BodyType Translation Boundary](bodytype-translation-boundary.md) to keep low-value enum matches separate from high-bit mixed buckets.
3. Use this baseline plus [SortLayer Census Baseline](sortlayer-census-baseline.md) to tighten the next overlay/detail authority table instead of guessing from tooling prose alone.
