# Visual verification findings — build 0238 (resolved in 0239)

User ran the app and reported four issues. Investigation findings:

## Bug 1 — Brown/red blotches on the face ✅ FIXED in 0239

**Cause**: Item 2.4 (build 0234) widened `IsFaceOverlayBodyType` to include Lipstick (29), Eyeshadow (30), Eyeliner (31), Blush (32) alongside EyeColor (4) / Brows (14) / Brow (34) / EyeColor (35). The audit at [face-cas-bodytype-audit.md](face-cas-bodytype-audit.md) confirmed those parts have 0 LODs and 1-2 textures — structurally compatible with the atlas compositor. But "atlas-compatible" was wrong: the makeup textures are sized for the **face-UV region** of the atlas, not the full atlas. Drawing them stretched across the whole atlas creates the brown/red blotches the user sees on cheeks, lips, around eyes.

**Fix**: Reverted `IsFaceOverlayBodyType` to bt=4/14/34/35 only. EyeColor/Brows produce correct positioning per user verification ("eyes, brows, lips, eyeshadows in place"). Lipstick/Eyeshadow/Eyeliner/Blush stay disabled until we can read each makeup texture's intended atlas sub-rect from its MATD/RegionMap and composite into the right rect.

## Bug 2 — Toddler/Teen/Infant Sims render as white silhouettes ⏸️ Deferred

**Cause investigated**: Probed `iuTop_Nude` (`0x45585`) — it's CASPart **version 52**. Our parser at `Ts4CasPart.Parse` handles up to version 43 with explicit branches (`if (header.Version >= 36/39/41/43)`). For version 44+ the CASPart layout adds additional fields we don't account for, so subsequent reads (`bodyType`, `ageGender`, `species`) end up at wrong byte offsets. The parser returns `BodyType = -2147483637` (0x80000B0B garbage) and `AgeGenderFlags = 0xBD006D00`.

Downstream at [AssetServices.cs:5421-5423](../../src/Sims4ResourceExplorer.Assets/AssetServices.cs#L5421):

```csharp
var isSupported = casPart.SupportsHumanSkinnedPreviewAge   // false: 0xBD006D00 & 0xFF == 0
    && casPart.IsMasculineOrFeminineHumanPresentation       // false
    && geometryResources.Count > 0;
```

`SupportsHumanSkinnedPreviewAge` returns false because the AgeGender mask test against the standard age flags (Infant=0x80, Toddler=0x02, Child=0x04, etc.) finds nothing in `0xBD006D00 & 0xFF = 0x00`. → `isSupported = false` → `CasGraph.IsSupported = false` → "Skipped candidate iuTop_Nude: no supported CAS geometry root was resolved" diagnostic the user saw.

**Fix path** (multi-day): research the version 44-52 CASPart byte layout (likely from TS4SimRipper's `CASP.cs`), add the missing field reads in `Ts4CasPart.Parse`, then `BodyType` and `AgeGenderFlags` will read from correct offsets and Infant/Toddler CASParts will pass the support check. Geometry resolution should then work since GEOM v15 is already supported.

**Workaround for the immediate-term**: when our canonical-baseline picker injects a CASPart instance ID, we know the BodyType from the picker (not from re-parsing the CASPart). We could plumb that "trusted" BodyType through the asset graph build to bypass the CASPart-derived support check for injected entries. That is also non-trivial — requires touching the AssetGraph contract.

**Decision**: ~~defer to a dedicated CASPart-version-extension session~~ → **FIXED in build 0240.** Two missing version-handlers added to `Ts4CasPart.Parse`:

1. **v >= 50**: read `LayerID = UInt16` after `parameterFlags2` (per TS4SimRipper CASP.cs:518-521).
2. **v >= 51**: replace fixed `ExcludePartFlags + ExcludePartFlags2` with length-prefixed list `Int32 count + UInt64[count]` (per TS4SimRipper CASP.cs:523-537).

Verification: re-running `--scan-canonical-nudes` after the fix shows `iuTop_Nude` (`0x45585`) parses correctly with `BodyType=6, AgeGenderFlags=0x3080 (Infant Unisex), Species=0x1 (Human), DefaultBT=False/F=True/M=True` — matches the TS4SimRipper layout. Same scan also discovered `puBottom_Nude` (`0x000206CC`) for Toddler that we'd been missing — added to `Ts4CanonicalBaselineBodyParts.PuBottomNude` and the `PickBottom` picker.

Also confirmed all older-version CASParts (Adult Female `yfTop_Nude`, etc.) still parse correctly — no regressions on the v<50 path because the new branches are gated by version.

## Bug 3 — Adult Female waist gap with vanilla EA data ⏸️ Rig-binding investigation needed (build 0240 update)

**Cause**: My Item D simplification (build 0237) only injects baselines when slots are **missing** from the SimInfo's outfit. The Adult Female test SimInfo has Top + Bottom + Shoes set, so no injection runs. The visible waist gap is from EA's vanilla `yfTop_Nude` + `yfBottom_Nude` meshes which are **designed with an inherent gap at the waist** — they assume clothing covers the abdomen→hip transition. Without clothing AND without a mod that extends the meshes, the gap is unavoidable with EA-shipped data.

The user has body-replacement mods at `C:\GAMES\Sims4Mods\Anatomy\` (`wild_guy CmarNudeTopFemaleDefault.package` etc.) that fix this gap by replacing the meshes. **But our app's data sources only include `C:\GAMES\The Sims 4\` — the mod path is not scanned.**

**Updated investigation (build 0240):** Ran `--scan-bt5-nakedlinks` against EA's full data dir. Result: 63 BodyType=5 (Full Body) CASParts have `DefaultForBodyType*` flags set, but NONE have a usable naked-link target — they're all clothing (Bathrobe, AthleticMascot, Overalls). Confirmed: **EA does not ship a Full Body Nude CASPart.** TS4 the game itself never renders a Sim with `*_Nude` Top + `*_Nude` Bottom alone — the engine ALWAYS overlays clothing.

So the gap isn't a missing-asset problem. The fact that TS4 the game never displays this state means the meshes' bind poses might depend on a body-shape morph (sculpt) that's normally applied at runtime. Our renderer doesn't apply morphs ("Body morph application: Pending" in the diagnostic), which leaves Top + Bottom at their raw bind poses — and that's where the visual gap appears.

**Real fix path (multi-day)**: implement body-morph/sculpt application during scene build. The SimInfo's `BodyModifiers` (channel-id × float-value pairs) need to be applied as deformation maps to the rendered geometry. TS4SimRipper does this via `LoadBONDMorph` (PreviewControl.cs:74-96). Without this, both top and bottom meshes float at their bind positions which were designed to be morphed together.

**Workaround until then**: bundle a baseline complete-body GEOM (extracted from EA data, e.g. `yfBody_Bathrobe` geometry stripped of the bathrobe-specific UVs) and inject it as a body-shell underlay when no Full Body is present. Same legal posture as `HeadMouthColor.png` — derived from EA's data, embedded in the app.

## Bug 4 — Toddler/Teen "look like adult" ⏸️ Same as Bug 2

The white silhouettes for Toddler/Teen are the same root cause as Infant — CASPart parser fails to read BodyType/AgeGenderFlags correctly for version 44+ CASParts. The Toddler/Teen-specific body parts use the same newer CASPart format as Infant.

## What's working ✅ (positive verification)

- **Skintone atlas + base texture loading** (Item G build 0236) — user confirmed "skin details visible".
- **Face CAS overlays bt=4/14/34/35** (Item 2.4 partial) — user confirmed "eyes, brows, lips, eyeshadows in place".
- **Neck seam** (Item E build 0238) — user confirmed "neck seems fine".
- **Canonical rig basis classification** — diagnostic implies `auRig` (or equivalent) is being matched.

## Action items

| # | Item | Owner | When |
|---|---|---|---|
| 1 | Add `C:\GAMES\Sims4Mods\` as data source, re-index, retest Adult Female gap | User | Now |
| 2 | Investigate makeup texture UV layout for proper bt=29-32 atlas sub-rect compositing | App | Future |
| 3 | Extend `Ts4CasPart.Parse` for versions 44-52 (research from TS4SimRipper CASP.cs) | App | Dedicated session |
| 4 | Bundle waist-filler GEOM as embedded resource (TS4SimRipper-style) | App | Tier 2 |
