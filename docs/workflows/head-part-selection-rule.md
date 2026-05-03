# Authoritative head-part selection rule (Plan 3.2)

## Summary

The "A head shell candidate is resolved, but it is not yet guaranteed by authoritative SimInfo head-part selection" disclaimer at [AssetServices.cs:4107](../../src/Sims4ResourceExplorer.Assets/AssetServices.cs#L4107) describes a real but narrow gap. The selection rule itself is well-defined and already implemented — the gap is one specific edge case where the rule is bypassed.

## The rule

**Head shell = the CASPart referenced by `bodyType=3` in the SimInfo's authoritative body-driving outfit.**

Probe verification on test SimInfo `025ED6F4:00000000:369CA7F9DE882B52`:

```
Body parts referenced by this SimInfo:
  bodyType=  2 (Hair        ) partInstance=0x3CF57    bodyDriving=1
  bodyType=  3 (Head        ) partInstance=0x1B41     bodyDriving=1   <-- head shell
  bodyType=  4 (?           ) partInstance=0x24237    bodyDriving=0
  bodyType=  6 (Top         ) partInstance=0x198C     bodyDriving=1
  bodyType=  7 (Bottom      ) partInstance=0x19AE     bodyDriving=1
  bodyType=  8 (Shoes       ) partInstance=0x19A3     bodyDriving=1
  bodyType= 29..35           ...                       bodyDriving=0
```

`MapCasBodyType` ([AssetServices.cs:5896-5906](../../src/Sims4ResourceExplorer.Assets/AssetServices.cs#L5896)):

| BodyType | Label |
|---:|---|
| 2 | Hair |
| 3 | **Head** |
| 5 | Full Body |
| 6 | Top |
| 7 | Bottom |
| 8 | Shoes |
| 12 | Accessory |
| _other_ | (no slot) |

## How the selection executes

The `BuildAssetGraphAsync` flow [AssetServices.cs:2125-2230](../../src/Sims4ResourceExplorer.Assets/AssetServices.cs#L2125) walks `authoritativeBodyDrivingParts`, maps each `bodyType` to a slot label, resolves the part instance against the index, and registers the resolved CASPart as a `SimBodyCandidateSourceKind.ExactPartLink` summary. For the test SimInfo's `bodyType=3, instance=0x1B41`, this works: probe confirms 5 catalog copies of CASPart `0x1B41` in `Client/SimulationFullBuild0.package`.

## Why fallbacks don't normally produce a "Head"

Of the four fallback paths in `BuildSimBodyAssemblyLayersAsync`, three iterate slot lists that exclude "Head":

| Fallback | Slot iterator | Includes "Head"? |
|---|---|---|
| `IndexedDefaultBodyRecipe` | `GetIndexedDefaultBodyRecipeSlotCategories` → `["Full Body", "Body", "Top", "Bottom", "Shoes"]` | No |
| `CanonicalFoundation` | `GetBodyAssemblySlotCategories.Where(IsShellFamilyLabel)` → subset of `["Full Body", "Body", "Top", "Bottom", "Shoes"]` | No |
| `ArchetypeCompatibilityFallback` | `GetBodyAssemblySlotCategories` → `["Full Body", "Body", "Top", "Bottom", "Shoes"]` | No |
| `BodyTypeFallback` | `BuildSimBodyFallbackSources(metadata)` keys, derived via `MapCasBodyType` from `Outfit.Parts` and `metadata.GeneticPartBodyTypes` | **Yes (rare)** |

Only `BodyTypeFallback` can produce a "Head" labeled summary with a non-ExactPartLink source kind.

## The actual edge case the disclaimer covers

The disclaimer is reachable only when:

1. SimInfo's authoritative body-driving outfit lacks a `bodyType=3` part (so ExactPartLink for "Head" never fires), **AND**
2. SimInfo has `bodyType=3` somewhere in `GeneticPartBodyTypes` or in a non-body-driving outfit (so `BuildSimBodyFallbackSources` injects "Head" as a key), **AND**
3. The compatibility candidate pool for "Head" returns at least one match.

In that case `BodyTypeFallback` populates a "Head" summary with `SourceKind = BodyTypeFallback`, the head layer's source kind is no longer `ExactPartLink`, and the disclaimer at line 4107 fires (correctly).

## Fix design (Phase 2.2)

Two complementary actions:

1. **Promote genetic Head parts to authoritative.** Extend `authoritativeBodyDrivingParts` (line 2125) to also include `bodyType=3` parts from `metadata.GeneticPartBodyTypes` when the body-driving outfit has none. Genetic head selection is reproducible and exact — there is no semantic reason to label it as "approximate".

2. **Delete the dead disclaimer branch** (Phase 1.6). After (1), the only paths to a "Head" layer are ExactPartLink and (rare-and-now-converted) genetic. The middle ternary branch at lines 4102-4107 collapses to unreachable.

If (1) is not pursued, the disclaimer wording should at least be sharpened: "Head shell selected by genetic-part compatibility because no body-driving outfit Head was present" — that's the actual condition, not a vague "not yet guaranteed".

## Reproduction

```bash
# Probe a SimInfo's body part composition (TGI = optional override of the auto-pick):
dotnet run --project tools/ProbeAsset/ProbeAsset.csproj --no-build -- --probe-sim-outfit-parts

# Check whether a specific head part instance is in the catalog:
dotnet run --project tools/ProbeAsset/ProbeAsset.csproj --no-build -- --probe-instance "C:\GAMES\The Sims 4" 0000000000001B41
```

## Status

3.2 closed. Selection rule documented; gap is one specific edge case (genetic-only Head). Ready to implement Phase 2.2 (promote genetic Head to authoritative) and Phase 1.6 (delete dead disclaimer branch).
