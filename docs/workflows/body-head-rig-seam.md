# Body/head rig seam unification (Plan 3.6)

## TL;DR

The body/head rig seam isn't a bone-overlap problem — it's a **rig basis selection** problem. TS4SimRipper resolves **one canonical rig per Sim** (species + age + occult) and uses it as the single bind-pose authority for every mesh in the assembly. Our current code at [SimSceneComposer.EvaluateRigCompatibility:382-428](../../src/Sims4ResourceExplorer.Core/SimSceneComposer.cs#L382) resolves rigs **per-mesh** and downgrades to a "canonical-bone fallback" when body/head GEOMs don't reference the same rig resource — but the canonical fallback still treats them as independent skinning targets.

## How TS4SimRipper does it

[`Form1.cs:520`](../references/external/TS4SimRipper/src/Form1.cs#L520):

```csharp
baseRig = GetTS4Rig(currentSpecies, adjustedAge, currentOccult, out rigInstance, out rigPackage);
LogMe(log, "Got Sim Rig: " + rigInstance.ToString("X16") + " Package: " + rigPackage);
currentRig = new RIG(baseRig);
```

[`Form1.cs:1404-1421`](../references/external/TS4SimRipper/src/Form1.cs#L1404) — rig name resolution by species+age+occult, hashed to a fixed instance:

```csharp
public RIG GetTS4Rig(Species species, AgeGender age, SimOccult occult, ...)
{
    if (occult == SimOccult.Werewolf)        rigTGI = new TGI(Rig, 0, 0x60FAA42F9B0B4E39);
    else if (occult == SimOccult.Fairy)      rigTGI = new TGI(Rig, 0, FNV64("nuRig"));
    else
    {
        String rigName = GetRigPrefix(species, age, AgeGender.Unisex) + "Rig";
        rigTGI = new TGI(Rig, 0, FNV64(rigName));   // e.g. "auRig", "cuRig", "puRig"
    }
    rig = FetchGameRig(rigTGI, ...);
}
```

`baseRig` is immutable (bind pose). `currentRig` is a copy that morphs are applied to. **Every mesh is skinned against `currentRig`.** The body mesh and the head mesh look up their bones in the same rig — a body-only bone (e.g. `b__L_Calf__`) and a head-only bone (e.g. `b__BrowL__`) just exist in different sub-trees of the same 165-bone rig, with the spine/neck bones acting as the structural seam.

## Why our seam fails

[`SimSceneComposer.EvaluateRigCompatibility`](../../src/Sims4ResourceExplorer.Core/SimSceneComposer.cs#L382) only succeeds if `bodyRigResources` and `headRigResources` (resources that the body and head GEOMs explicitly reference) intersect — by FullTgi or by FullInstance. When they don't, it returns `BodyOnly` or `CanonicalBoneFallback`. In the canonical-fallback case both meshes get the same `auRig`, but the calling code still skins them as if they were two independent skeletons — bone hashes are resolved per-mesh, transforms aren't shared, and the spine/neck don't align.

The user-visible symptom (per [project_sim_render_status.md](../../C:/Users/stani/.claude/projects/c--Users-stani-PROJECTS-Sims4Browser/memory/project_sim_render_status.md) gap 3): "Only 3 shared canonical bones across 62 body bones and 41 head bones." — that's not a problem. 3 shared bones (head-of-neck, bottom-of-neck, spine-top) is exactly enough to define the seam. The problem is we never USE the shared rig; we treat each mesh's bone subset as its own skeleton.

## Fix design (Phase 2.6)

Three steps:

1. **Resolve rig name from species+age+occult, not from the GEOM.** Mirror TS4SimRipper's `GetRigPrefix(species, age, Unisex) + "Rig"` → FNV-1 64 → instance lookup. ProbeAsset already has `--probe-rig` for this transformation. The result is the single-source-of-truth rig for the Sim, regardless of what individual GEOMs reference.

2. **Load that rig once, store as `Sim.BaseRig`.** Make it immutable and pass it to all mesh-batch builders.

3. **Skin every mesh batch against `BaseRig`.** Each mesh's bone-index → bone-hash → `BaseRig.GetBone(hash)` → world transform. Body and head end up with their bones resolved by the same lookup, the spine/neck bones get the same world transforms in both meshes, and the seam closes.

This obsoletes the `EvaluateRigCompatibility` decision tree — the only failure mode becomes "the species+age combination has no canonical rig" (e.g. unknown new species), and that's a different gap. The `SharedRigResource`/`SharedRigInstance`/`CanonicalBoneFallback`/`BodyOnly` distinctions all collapse into one path: "use the canonical rig for the Sim's species/age".

## Acceptance test (post-fix)

After Phase 2.6, the body and head should pose together when the same morph chain is applied. Verifiable visually (the user reports no neck-seam discontinuity) and structurally:

```
auRig.GetBone(FNV1("b__Spine2__")).WorldTransform
==
the world transform applied to spine2-skinned vertices in BOTH the body mesh and (any head mesh skinned to spine2)
```

A unit test can compute these and assert byte-equal results across body and head batches.

## Status

3.6 closed. The seam isn't a bone-overlap research gap — it's a rig-basis architectural mismatch with TS4SimRipper's proven design. Phase 2.6 is a refactor of the rig resolution path: select rig by species+age+occult, load once, skin all meshes against it. No additional research needed.
