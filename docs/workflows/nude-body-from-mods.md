# Nude body shell — findings from mod investigation (Plan D update)

## TL;DR

**Item D's original premise was wrong.** EA SHIPS canonical nude CASParts (`yfTop_Nude`, `yfBottom_Nude`, `ymBottom_Nude`, etc.) directly in `ClientFullBuild0.package`. The earlier conclusion that "TS4 has no discoverable nude body" was based on filtering for `BodyType=5` (Full Body) only — TS4 actually represents a nude body as **separate Top + Bottom CASParts** (`BodyType=6` and `BodyType=7`), not as a single Full Body. We don't need to bundle anything; we just need to use the right canonical instance IDs.

## Reproduction

```bash
dotnet run --project tools/ProbeAsset/ProbeAsset.csproj --no-build -- \
    --probe-mod "C:\GAMES\Sims4Mods\Anatomy\wild_guy CmarNudeBottomFemaleDefault.package"
```

The `--probe-mod` subcommand parses every CASPart in a `.package` and dumps its full structure (BodyType, AgeGenderFlags, default-body flags, NakedKey, TgiList).

## Canonical EA nude CASPart instance IDs (confirmed by mod overrides)

| BodyType | Slot | Gender | Age | EA Instance ID | EA InternalName |
|---:|---|---|---|---|---|
| 6 | Top | Female | Teen / YA / Adult / Elder | `0x000000000000198C` | `yfTop_Nude` |
| 7 | Bottom | Female | Teen / YA / Adult / Elder | `0x0000000000001990` | `yfBottom_Nude` |
| 7 | Bottom | Male | Teen / YA / Adult / Elder | `0x00000000000019AE` | `ymBottom_Nude` |

Each is present in 5 game packages (`ClientDeltaBuild0`, `ClientFullBuild0`, `SimulationDeltaBuild0`, `SimulationFullBuild0`, `SimulationPreload`).

## How modders override these

Mods like `wild_guy CmarNudeBottomFemaleDefault.package` work by:
1. Defining a CASPart at the **same instance ID** (`0x1990`) as EA — TS4's package loading prefers the mod over EA.
2. Setting the geometry references in TgiList to NEW Geometry instances (the mod's custom mesh).
3. Setting `DefaultForBodyTypeFemale = True` (PartFlags2 bit 0x04) — this is the flag that makes TS4 select this CASPart as the default for the Female body-type slot.

The EA-shipped CASParts at these IDs have `DefaultForBodyType = False` and `DefaultForBodyTypeFemale = False`. EA must use a different mechanism (probably hard-coded CASPart references in SimInfo or in the engine) to select the nude when no clothing is equipped.

## The "Full Body" misconception

The original Item D research filtered for `BodyType=5` (Full Body) and found 1,326 candidates, all clothing. But TS4 represents the nude body as **Top + Bottom**, not Full Body:
- Per `MapCasBodyType`: 5 = Full Body (jumpsuits, dresses, full-body costumes — clothing)
- The "nude" state = Female Top `0x198C` + Female Bottom `0x1990` (or Male equivalents)

The test SimInfo `369CA7F9DE882B52` confirms this — its outfit body parts are `bodyType=6 (Top) part=0x198C` and `bodyType=7 (Bottom) part=0x19AE`. Those ARE the canonical nudes (the same instance IDs the mods override).

## Implications for Item D rendering

The "Female waist gap" gap collapses to a much simpler problem. When a Sim's authoritative outfit lacks Top or Bottom (e.g., outfit overrode only Top with a custom shirt and didn't include Bottom), inject the canonical nude CASPart for the missing slot.

**No asset bundling needed.** The EA-shipped CASParts at `0x198C`/`0x1990`/`0x19AE` provide the geometry. Our pipeline already handles CASPart resolution and GEOM rendering. The only new work is: detect missing Top/Bottom and add the canonical nude as a fill.

### Implementation outline (revised D)

1. Add `Ts4CanonicalNudeBodyParts` constants:
   ```csharp
   public static readonly ulong FemaleTopNude = 0x000000000000198Cul;
   public static readonly ulong FemaleBottomNude = 0x0000000000001990ul;
   public static readonly ulong MaleBottomNude = 0x00000000000019AEul;
   // TODO: probe for ymTop_Nude, child variants (cuTop_Nude, cuBottom_Nude, etc.)
   ```
2. In `BuildAssetGraphAsync` body-driving-parts assembly, when the resolved authoritative parts lack a Top or Bottom, append the canonical nude CASPart instance for the Sim's gender. Treat it as ExactPartLink (it IS authoritative — same way the test SimInfo references it).
3. Existing rendering pipeline picks up the additional CASPart and renders the nude geometry as a normal mesh batch.

This is a 1-day fix, not the multi-day asset-bundling-and-rendering project.

## Other mod findings (out-of-scope for D, noted for follow-up)

- **WickedWhims** has 167 CASParts, 102 Geometries, 59 Rigs — large body-related mod with extensive geometry. Not needed for D but might be relevant for body-type extension research.
- **Skin Overlay by PsBoss** uses three CASParts at custom instance IDs (`CDD06C..`, `A97977..`, `AC544A..` with group `0x80000000` indicating custom content). Our parser fails on these with "CASPart structured body extends beyond the declared TGI table offset" — a CASPart parser robustness gap not related to D.

## Status

D's premise falsified. The new approach is much simpler. Recommended sub-tasks:

- [ ] Probe for additional canonical nude CASPart instance IDs: `ymTop_Nude` (likely near `0x19AE`), child variants (`cuTop_Nude`, `cuBottom_Nude`).
- [ ] Add `Ts4CanonicalNudeBodyParts` constants.
- [ ] In `AssetServices.BuildAssetGraphAsync`, after `authoritativeBodyDrivingParts` is built, check for missing Top/Bottom and append canonical nudes for the Sim's gender.
- [ ] Visual verify Female Top+Bottom outfits no longer have waist gap.
- [ ] Fix the CASPart parser to handle the "structured body extends beyond TGI table offset" variant seen in custom mods.
