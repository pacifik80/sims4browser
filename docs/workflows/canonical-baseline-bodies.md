# Canonical EA baseline body CASPart IDs (mod-confirmed reference table)

## Source

Probe results from `--scan-canonical-nudes` against `C:\GAMES\The Sims 4\Data` (49 packages, EA-shipped only). Mod investigations (`wild_guy CmarNude*`, `ClassicWWMaleBottom*`) confirmed which IDs modders override — those ARE the canonical EA baselines.

## Complete baseline table

### Adult / Teen / Young Adult / Elder

| Slot | Gender | InternalName | EA Instance | DefaultBT | DefaultBTF | DefaultBTM |
|---|---|---|---|---|---|---|
| Top | **Female** | `yfTop_Nude` | `0x000000000000198C` | False | False | False |
| Top | **Male** | `ymTop_Nude` | `0x00000000000019A2` | True | False | False |
| Bottom | **Female** | `yfBottom_Nude` | `0x0000000000001990` | False | False | False |
| Bottom | **Male** | `ymBottom_Nude` | `0x00000000000019AE` | False | False | False |
| Shoes | **Female** | `yfShoes_Nude` | `0x000000000000198F` | True | False | False |
| Shoes | **Male** | `ymShoes_Nude` | `0x00000000000019A3` | True | False | False |
| Head | **Female** | `yfHead` | `0x0000000000001B41` | — | — | — |

AgeGender flags `0x00002078` (Female) and `0x00001078` (Male) cover Teen / YA / Adult / Elder.

### Child

| Slot | Gender | InternalName | EA Instance | DefaultBT | DefaultBTF | DefaultBTM |
|---|---|---|---|---|---|---|
| Top | **Female** | `cfTop_Nude` | `0x000000000000F3BA` | False | False | False |
| Top | **Universal** | `cuTop_Nude` | `0x0000000000005635` | True | False | False |
| Bottom | **Universal** | `cuBottom_Nude` | `0x000000000000563A` | False | False | False |
| Shoes | **Universal** | `cuShoes_Nude` | `0x0000000000005602` | True | False | False |

AgeGender `0x00003004` = Child Unisex; `0x00002004` = Child Female.

### Toddler (Preschooler) / Infant

`puTop_Nude` (`0x000206C7`), `puShoes_Nude` (`0x000206D2`), `iuTop_Nude` (`0x00045585`), `iuShoes_Nude` (`0x00045588`), `iuBottom_Nude` (`0x00045587`) — all use packed BodyType encodings (toddler/infant body type extensions). The `iuTop_Nude` and `iuShoes_Nude` set BOTH `DefaultBTF` and `DefaultBTM = True`.

## What modders confirm (mod overrides at canonical IDs)

| Mod file | Overrides EA instance | EA InternalName | Mod sets DefaultBT* |
|---|---|---|---|
| `wild_guy CmarNudeTopFemaleDefault.package` | `0x198C` | `yfTop_Nude` | DefaultBTF=True |
| `wild_guy CmarNudeBottomFemaleDefault.package` | `0x1990` | `yfBottom_Nude` | DefaultBTF=True |
| `wild_guy ClassicWWMaleBottomSoftDefault.package` | `0x19AE` | `ymBottom_Nude` | DefaultBT=True, DefaultBTM=True |

Mods ADD the `DefaultForBodyType*` flags that EA leaves unset — that's how the override forces the engine to select the modded part as the default body. EA's selection rule for the unflagged variants must be different (likely SimInfo outfit reference + InternalName matching).

## Mesh complexity (all GEOM v14)

From `--probe-mod ... --geom`:

| Source | Slot | LOD0 verts | LOD0 tris | LOD0 bones |
|---|---|---:|---:|---:|
| `wild_guy_yfBottom_Nude` (mod) | Female Bottom | 627 | 1,012 | 12 |
| `wild_guy_yfTop_Nude` (mod) | Female Top | **1,919** | 3,276 | **52** |
| `ymBottom_Nude` (Soft mod) | Male Bottom | 1,338-1,964 | 2,350-3,388 | 8-19 |
| `wild_guy_Classic_WW_Penis_Hard_Male` (additive) | Male anatomy | 1,934 | 3,390 | 19 |

**Earlier "62 body bones / 41 head bones" assumption was wrong** — Female Top alone uses **52 bones**. The bone-count distribution across mesh subsets is much wider than the prior memory suggested, which means the rig basis unification (Item E) is even more important — every mesh references its own bone subset of the canonical rig.

All 4 LODs (LOD0 highest detail through LOD3 lowest) per CASPart. Group ID encoding for LODs: `015A1849:GROUP:INSTANCE` where the LOD ordering is encoded in the group's lower bits.

## How TS4 selects the nude body when the outfit is "missing" parts

Two mechanisms combine:

1. **`DefaultForBodyType*` flags on CASParts** — when a Sim has no Top/Bottom/Shoes assigned for a frame, TS4 picks any CASPart that:
   - Matches the slot's BodyType
   - Matches the Sim's age × gender × species
   - Has `DefaultForBodyTypeMale = True` (for male) or `DefaultForBodyTypeFemale = True` (for female)
   - Per the table above, EA has `ymTop_Nude`, `ymShoes_Nude`, `yfShoes_Nude`, `cuTop_Nude`, `cuShoes_Nude`, `iuTop_Nude`, `iuShoes_Nude` flagged as defaults.

2. **Direct SimInfo outfit reference** — for the parts that AREN'T flagged as default (most notably `yfTop_Nude` and `yfBottom_Nude`), TS4 explicitly references them in the SimInfo's outfit by instance ID. The test SimInfo `369CA7F9DE882B52` confirms this — its "Nude" outfit lists Top=`0x198C`, Bottom=`0x19AE`, Shoes=`0x19A3`.

For our app this means we have two paths:
- **Outfit-driven** (preferred): when SimInfo outfit lists a Top/Bottom/Shoes, render those CASParts. Already works.
- **Default-driven** (fallback): when SimInfo outfit lacks a slot, pick the canonical baseline from the table above for the Sim's gender/age. This is the new fallback for Item D.

## Implementation plan for Item D (revised, simplified)

```csharp
internal static class Ts4CanonicalBaselineBodyParts
{
    // Top (BodyType 6)
    public const ulong YfTopNude = 0x000000000000198Cul;
    public const ulong YmTopNude = 0x00000000000019A2ul;
    public const ulong CuTopNude = 0x0000000000005635ul;
    public const ulong CfTopNude = 0x000000000000F3BAul;
    public const ulong PuTopNude = 0x00000000000206C7ul;
    public const ulong IuTopNude = 0x0000000000045585ul;

    // Bottom (BodyType 7)
    public const ulong YfBottomNude = 0x0000000000001990ul;
    public const ulong YmBottomNude = 0x00000000000019AEul;
    public const ulong CuBottomNude = 0x000000000000563Aul;
    public const ulong IuBottomNude = 0x0000000000045587ul;

    // Shoes (BodyType 8)
    public const ulong YfShoesNude = 0x000000000000198Ful;
    public const ulong YmShoesNude = 0x00000000000019A3ul;
    public const ulong CuShoesNude = 0x0000000000005602ul;
    public const ulong PuShoesNude = 0x00000000000206D2ul;
    public const ulong IuShoesNude = 0x0000000000045588ul;

    public static ulong? PickTop(string ageLabel, string genderLabel) => /* match */;
    public static ulong? PickBottom(string ageLabel, string genderLabel) => /* match */;
    public static ulong? PickShoes(string ageLabel, string genderLabel) => /* match */;
}
```

In `BuildAssetGraphAsync`, after `authoritativeBodyDrivingParts` is built, check for missing Top/Bottom/Shoes per the Sim's age/gender. If missing, append the canonical baseline instance as `(BodyType, Instance)` in the same authoritative list. The existing `LoadExactSimBodyPartResourceMatchesAsync` will resolve the EA-shipped CASPart, and the existing CASPart-to-mesh-batch pipeline will render it.

**No GEOM extraction. No bundling. No new rendering code.** The CASParts are in the player's local game data; we just need to reference them.

## Mesh learnings for future improvements

1. **Bone subsets are mesh-specific**: Female Top uses 52 bones, Female Bottom uses 12 bones, Male Bottom uses 8-19 bones. The canonical rig (`auRig` / `cuRig` etc.) is the SUPERSET; each mesh references its own subset. This validates Item E's plan: load ONE rig, let each mesh pick its own bones.

2. **LOD ordering**: TgiList lists Geometries in (LOD0..LOD3) order with descending vertex counts. We currently render LOD 0 only; distance-based LOD selection is a future optimisation.

3. **GEOM version 14** is consistent across all probed body parts. Our parser handles it.

4. **Mod GEOM compatibility**: mod GEOMs use the SAME bone hashes as EA's rig — they don't introduce new bones. This means a mod can replace mesh geometry but cannot add new rig joints. Good news for Item E: there's no rig divergence to handle across mods.

5. **`PartFlags1=0x80` (RestrictOppositeGender)** — most mods set this so the modded body doesn't appear for the wrong gender. Our pipeline should respect this flag when filtering CAS candidates.

## Reproduction

```bash
dotnet run --project tools/ProbeAsset/ProbeAsset.csproj --no-build -- --probe-mod "<mod.package>" --geom
dotnet run --project tools/ProbeAsset/ProbeAsset.csproj --no-build -- --scan-canonical-nudes "C:\GAMES\The Sims 4\Data" tmp/canonical-nudes.txt
```

Output: `tmp/canonical-nudes.txt` and the per-mod CASPart structure dump.
