# Nude body shell discovery (Plan 3.5)

## TL;DR

**TS4 has no discoverable nude body CASPart in the game catalog.** TS4SimRipper — the most complete community reference for Sim rendering — confirms this by **bundling its own baseline body meshes** as embedded resources. The "Female waist gap" is therefore not a discovery problem (there is nothing in the game data to discover), it is an **asset-bundling-or-synthesis problem**.

## Evidence

### Local corpus (already known)

Per [project_sim_render_status.md](../../%7Eor%20your%20memory%20file%7E):
- 1,326 Adult-Female nude-flagged Full Body candidates exist in the prod cache. All probed are clothing (`yfBody_Bathrobe_*`, `yfBody_EP02ArmorSuit_*`, `yfBody_EP03Animal_Cat*`).
- Zero CASParts with `_Nude` suffix exist in the catalog (probed).
- `nakedLink` byte-key in `Ts4CasPart.TgiList` resolves to type `0x6017E896` (metadata), not a Full Body CASPart.

### TS4SimRipper does not solve this — it bundles meshes

[`docs/references/external/TS4SimRipper/src/Resources/`](../references/external/TS4SimRipper/src/Resources/) contains:

| Bundled resource | Purpose |
|---|---|
| `yfBodyComplete_lod0.simgeom` | Female young/adult nude body |
| `ymBodyComplete_lod0.simgeom` | Male young/adult nude body |
| `cuBodyComplete_lod0.simgeom` | Child universal nude body |
| `puBodyComplete_lod0.simgeom` | (P)? universal nude body |
| `yfHead_lod0.simgeom`, `cuHead_lod0.simgeom`, `puHead_lod0.simgeom` | Head meshes |
| **`WaistFiller.simgeom`** | Strip mesh that fills the gap when a Female Top sits on a Male Bottom |
| `yfBody_Male_Normals.deformermap`, `yfBody_Male_Shape.deformermap` | Cross-frame deformation maps |
| `HeadMouthColor.png`, `CatSkin.png`, `DogSkin.png`, `HorseSkin.png`, `WerewolfSkin.png`, `FairySkin.png`, `Starter.png` | Bundled skin textures |

The waist filler is wired in [`PreviewControl.cs:697-706`](../references/external/TS4SimRipper/src/PreviewControl.cs#L697):

```csharp
if (partGenders[(int)BodyType.Top] == AgeGender.Female &&
    partGenders[(int)BodyType.Bottom] == AgeGender.Male)
{
    LogMe(log, "Adding waist filler");
    BaseModel[(int)BodyType.Top].AutoSeamStitches(Species.Human, AgeGender.Adult, AgeGender.Female, 0);
    BaseModel[(int)BodyType.Bottom].AutoSeamStitches(Species.Human, AgeGender.Adult, AgeGender.Male, 0);
    Stream s = new MemoryStream(Properties.Resources.WaistFiller);
    BinaryReader br = new BinaryReader(s);
    BaseModel[(int)BodyType.Top].AppendMesh(new GEOM(br));
}
```

### TS4SimRipper does NOT search for a nude body in game data

`Form1.cs` and `PreviewControl.cs` searched for keywords `naked`, `nude`, `FullBody`, `Underlay`, `defaultBody`: zero matches. The outfit array is iterated as-is — whatever the SimInfo says is in the outfit is what gets rendered. No fallback discovery, no "find me a nude body".

This means **the absence of a nude body in the SimInfo's outfit is the normal expected state for many Sims**, and the engine handles it via:

1. Top + Bottom mesh designs that overlap enough to cover the body, OR
2. A waist-filler bundled mesh stitched in for cross-frame mismatches.

EA's runtime almost certainly does (1) by mesh design. (2) is a TS4SimRipper-specific workaround for cases where (1) breaks.

## License caveat

TS4SimRipper is **GPL-3.0** ([`docs/references/external/TS4SimRipper/LICENSE`](../references/external/TS4SimRipper/LICENSE)). We cannot copy its `.simgeom` files into a non-GPL project without imposing GPL on the whole codebase. The meshes themselves are derived from EA game data — that derivation status is what TS4SimRipper relies on, but the act of bundling within a GPL project changes the redistribution terms for the bundle.

## Fix design (Phase 2.5) — three viable paths

### Path A — Bundle our own EA-derived baseline meshes
Extract `yfBody*`/`ymBody*`/`cuBody*` baseline meshes directly from EA game packages, ship them as embedded resources in our app. Same legal posture as bundling `HeadMouthColor.png` (already in project at [src/Sims4ResourceExplorer.App/Assets/HeadMouthColor.png](../../src/Sims4ResourceExplorer.App/Assets/HeadMouthColor.png)). Lowest implementation cost.

### Path B — Algorithmic waist-strip synthesis
At runtime, when an outfit has Top + Bottom but no Full Body:
1. Find the Top mesh's bottom edge loop (vertices at the lowest V coordinate within the torso UV island).
2. Find the Bottom mesh's top edge loop.
3. Generate a triangle strip between them (single quad ring).
4. Apply the skin atlas as the diffuse texture.

Pure code, no bundled assets. Higher implementation cost, geometry may not match the underlying body shape exactly.

### Path C — Hybrid (preferred)
- Bundle ONE baseline body shell per (species, age, gender) combination — minimal asset footprint (~6 meshes for human alone).
- Use those as **underlays drawn beneath Top/Bottom**, not as fillers.
- The bundled body is masked by region maps from the Top/Bottom CASParts so it only shows through the gap.
- This is conceptually how the EA runtime is likely doing it.

Path C matches the SkinBlender contract ("one bitmap, two meshes" — the bundled body shares the skin atlas) and avoids per-Sim per-gap geometry synthesis.

## Recommendation

**Path C** (bundled baseline body shell drawn under Top + Bottom with region-map masking). Plan 2.5 should:
1. Extract one EA `yfBody`/`ymBody`/`cuBody` LOD-0 mesh per supported (species, age, gender) tuple.
2. Embed as `Sims4ResourceExplorer.App.YfBodyBaseline.simgeom` etc. (similar to `HeadMouthColor.png`).
3. In `BuildBuySceneBuildService.Cas.cs` body-assembly path, when no Full Body part is present and Top + Bottom are, emit the baseline body underlay as the first mesh batch with region-map mask sourced from Top/Bottom.

## Status

3.5 closed. The "discovery" gap is misnamed — there is nothing to discover in the game data. The fix is asset-bundling (Path C). Implementation belongs in Phase 2.5.
