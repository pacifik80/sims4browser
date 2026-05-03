# BOND morph application pipeline — implementation plan

## Why this matters

The Adult Female waist gap, the "elder-looking" Child/Toddler/Infant faces, and most other body-shape artifacts in the Sim render share one root cause: **EA's mesh bind poses depend on a runtime morph application step that we haven't implemented.** TS4 the game itself never displays an unmorphed Sim — body modifiers and sculpts are always applied. Our renderer skips this step, so meshes float at their bind positions which were designed to be reshaped, not displayed directly.

Foundation laid in build 0243:
- `Ts4BondResource` parser (`src/Sims4ResourceExplorer.Packages/BondResource.cs`)
- `--scan-bonds` probe — verified all 692 EA-shipped BONDs parse (13,712 bone adjustments across 75 distinct bone slots)
- 1 unit test exercising the parser

What remains is wiring this data through the scene-build pipeline.

## TS4SimRipper reference

Three parts to study:
1. `BOND.cs` — binary format (DONE — mirrored as `Ts4BondResource`)
2. `PreviewControl.LoadBONDMorph` (lines 74-155) — the algorithm
3. `RIG.BoneMorpher` and `GEOM.BoneMorpher` — the per-bone-and-per-vertex math

## Pipeline architecture

```
SimInfo
  ├── BodyModifiers : List<(channelId, weight)>
  ├── Sculpts       : List<TGI>
  └── GeneticBodyModifiers, GeneticFaceModifiers, ...

For each modifier:
  channelId / TGI ─→ resolve to BOND or DMap resource
                   ─→ apply with weight to RIG and/or GEOMs

Modified RIG + GEOMs ─→ rendered scene
```

Two morph types coexist:
- **BOND** = bone delta (offset/scale/rotation per bone). Affects both rig and any mesh skinned to that bone.
- **DMap** = deformation map (per-vertex shape + normal deltas, sampled via UV1). Affects only meshes with UV1 + tags.

This plan covers BOND only. DMap is a separate parser + application step (similar scope).

## Sub-steps for BOND application

### B.1 — Resolve channelId → BOND TGI mapping (½–1 day)

`BodyModifiers` are `(channelId, weight)` pairs but channelId is an opaque uint — we don't know which BOND each maps to. Two paths to discovery:
1. CASTuning lookup — TS4SimRipper's `CASTuning.cs` has `DmapConversions` and similar maps. Find the BOND-specific equivalent.
2. SimModifier resource type (TS4 has its own resource: `0x0CB82EB8 SimModifier`) that wraps a channelId + a list of BOND/DMap TGIs. The Sim's BodyModifier channelId points to a SimModifier resource which lists the actual BOND/DMap to apply.

Action: probe a SimModifier resource. Find its TGI structure. Add a parser. Build a `channelId → BOND[] + DMap[]` lookup table.

### B.2 — Add a `BondMorpher` service (1 day)

Pure-math service that takes:
- A canonical rig (`Ts4RigResource` — probably needs an internal mutable copy)
- A list of (BOND, weight) pairs
- A list of meshes (`Ts4GeomResource`)

And produces:
- Modified rig with bone transforms updated
- Modified meshes with vertex positions updated by skinning against the modified rig

The math (per `LoadBONDMorph`):
```
For each BoneAdjust delta in BOND:
    Bone bone = rig.GetBone(delta.SlotHash)
    if bone is null: skip
    Vector3 localScale  = (delta.scaleX, scaleY, scaleZ)
    Vector3 localOffset = (delta.offsetX, offsetY, offsetZ)
    Quaternion localRot = (delta.quatX, quatY, quatZ, quatW)

    // Transform the local delta by the bone's morph rotation, weighted by the BOND weight
    Vector3 worldScale  = (bone.MorphRotation * Matrix.FromScale(localScale + ones)).Scale - ones
    Vector3 worldOffset = (bone.MorphRotation * localOffset * bone.MorphRotation.Conjugate())
    Quaternion worldRot = bone.MorphRotation * localRot * bone.MorphRotation.Conjugate()

    // Apply to mesh vertices weighted by skin weight
    For each vertex v in mesh:
        For each (boneIdx, skinW) in v.Skinning:
            if boneIdx points to this bone:
                v.position += worldOffset * BOND.weight * skinW
                v.position = ScaleAroundBone(v.position, bone.AbsolutePosition, ones + worldScale * BOND.weight * skinW)
                v.position = RotateAroundBone(v.position, bone.AbsolutePosition, Slerp(Identity, worldRot, BOND.weight * skinW))
                v.normal   = (RotateNormal similarly)

    // Optionally update the rig too (for chained morphs)
    bone.MorphRotation *= localRot
    bone.MorphPosition += localOffset
    bone.MorphScale    *= (ones + localScale)
```

Need quaternion math helpers (Slerp, Conjugate, normalize, multiply) and Matrix3D for scale composition. Existing geometry math in our codebase is minimal — needs additions.

### B.3 — Wire `BondMorpher` into the CAS scene build (½ day)

Where geometry is converted to a renderable mesh in `BuildBuySceneBuildService.Cas.cs`:
1. Resolve the Sim's `Ts4SimInfo.BodyModifiers` and `Sculpts`.
2. For each, look up the BOND/DMap TGI(s) via the channelId table from B.1.
3. Apply via `BondMorpher` BEFORE building `CanonicalMesh` from `Ts4GeomResource`.
4. Use the morphed `Ts4RigResource` for skinning.

### B.4 — DMap parser + application (1-2 days, follow-up)

For face morphs and per-vertex shape deformation. Symmetric to BOND but:
- Uses UV1 to sample a 2D deformation map
- Applies per-vertex (not per-bone)
- Works only on meshes with `hasTags + hasUVset(1)` (face meshes mostly)

### B.5 — Visual verification (½ day)

Test against:
- Adult Female with Top + Bottom (waist gap should close)
- Child Female face (should look child-like, not elder)
- Toddler / Infant body proportions (should be age-appropriate)

## Estimate

| Sub-step | Days |
|---|---:|
| B.1 — channelId mapping | 0.5–1 |
| B.2 — BondMorpher | 1 |
| B.3 — CAS scene build wire-up | 0.5 |
| B.4 — DMap parser + application | 1–2 |
| B.5 — Visual verification | 0.5 |
| **Total** | **3.5–5 days** |

This is genuine multi-session work. The build 0243 BOND parser is step 0 of B.2.

## Status (build 0245)

### Foundation + BOND pipeline shipped
- ✅ **BOND parser** (`Ts4BondResource`) — 1 unit test, **692/692 EA BONDs parse**, 13,712 bone adjustments across 75 distinct bone slots
- ✅ **SMOD parser** (`Ts4SimModifierResource`) — 1 unit test, **2,601/2,601 EA SMODs parse**, 782 reference BONDs, 461 each reference shape/normal DMaps
- ✅ **SimInfo modifier TGI capture** — `Ts4SimModifierEntry` now carries `ModifierKey: ResourceKeyRecord?` resolved via linkTable
- ✅ **`SimBoneMorphAdjustment`** — flat carrier in Core (no Packages dependency)
- ✅ **`BondMorpher.MorphScene(scene, adjustments)`** — applies bone-translation deltas to CanonicalScene meshes via per-vertex skin-weighted accumulation. Operates at the CanonicalScene level so the upstream scene-build cache stays valid
- ✅ **`BondMorphResolver`** in Assets — resolves SimInfo → SMOD chain → BOND list → flat `SimBoneMorphAdjustment` array. Public DI service.
- ✅ **MainViewModel wire-up** — after both body and head previews are resolved, calls `BondMorphResolver.ResolveAsync(simInfoResource)`, applies `BondMorpher.MorphScene` to both, then `SimSceneComposer.ComposeBodyAndHead` runs on morphed scenes. Diagnostics include `"BOND morph: resolved N bone adjustment(s)"`.
- ✅ **3 BondMorpher unit tests** — translation works, accumulates across multiple adjustments, blends across bones via skin weights
- ✅ Probes: `--scan-bonds`, `--scan-smods`, `--probe-modifier-tgis`

### Remaining work — concrete sub-steps

**B.2 — BondMorpher service** (1-2 days):

The math (per TS4SimRipper `LoadBONDMorph`):
1. Need a mutable rig representation. Our `Ts4RigResource` is immutable — add a `Ts4MutableRig` working copy with `Bone` records that include `MorphRotation: Quaternion`, `MorphPosition: Vector3`, `MorphScale: Vector3` (all start as identity/zero).
2. Need quaternion math primitives in our codebase. Add `Quaternion4` struct with: Identity, FromXyzw, Conjugate, Multiply, Slerp, Normalize, IsEmpty. Also `Vector3` arithmetic (we have positions as float[3], need actual struct ops). Use `System.Numerics.Quaternion` and `Vector3` directly to avoid reinventing.
3. Per `BoneAdjust`:
   ```
   Bone bone = rig.GetBone(delta.SlotHash)  // null → skip
   Vector3 localScale = (delta.scaleX, scaleY, scaleZ)
   Vector3 localOffset = (delta.offsetX, offsetY, offsetZ)
   Quaternion localRot = (delta.quatX, quatY, quatZ, quatW)

   // Transform local delta into the bone's morph-space, weighted by BOND.weight
   Vector3 worldOffset = Quaternion.Multiply(bone.MorphRotation, localOffset, Quaternion.Conjugate(bone.MorphRotation))
   Vector3 worldScale  = (bone.MorphRotation.ToMatrix() * Matrix.FromScale(localScale + ones)).Scale - ones
   Quaternion worldRot = Quaternion.Multiply(bone.MorphRotation, localRot, Quaternion.Conjugate(bone.MorphRotation))

   // Apply to mesh vertices (per-bone-per-vertex skinning)
   For each vertex v in mesh with skinning:
       For each (boneIdx, skinW) in v.Skinning where boneHashes[boneIdx] == delta.SlotHash:
           Vector3 effectiveOffset = worldOffset * BOND.weight * skinW
           Vector3 effectiveScale  = ones + worldScale * BOND.weight * skinW
           Quaternion effectiveRot = Quaternion.Slerp(Identity, worldRot, BOND.weight * skinW)
           v.Position = effectiveRot.Rotate(v.Position - bone.AbsolutePosition) * effectiveScale + bone.AbsolutePosition + effectiveOffset
           v.Normal   = effectiveRot.Rotate(v.Normal)

   // Update rig (for chained morphs)
   bone.MorphRotation *= localRot
   bone.MorphPosition += localOffset
   bone.MorphScale    *= (ones + localScale)
   ```

**B.3 — Wire into CAS scene build** (½ day):

In `BuildBuySceneBuildService.Cas.cs` `BuildGeometrySceneAsync`:
1. After parsing `Ts4GeomResource` and resolving `Ts4RigResource`, but BEFORE building the `CanonicalMesh`:
   ```csharp
   // Gather modifiers from the SimInfo (passed via metadata).
   var bonds = await ResolveBondsForSimAsync(simMetadata, cancellationToken);
   if (bonds.Count > 0)
   {
       var morpher = new BondMorpher();
       morpher.Apply(rig, geom, bonds);  // mutates rig + geom in place
   }
   ```
2. `ResolveBondsForSimAsync`:
   ```csharp
   var bonds = new List<(Ts4BondResource bond, float weight)>();
   foreach (var modifier in simMetadata.BodyModifiers.Concat(simMetadata.FaceModifiers))
   {
       if (modifier.ModifierKey is null) continue;
       var smodBytes = await catalog.GetResourceBytesAsync(...);
       var smod = Ts4SimModifierResource.Parse(smodBytes);
       if (!smod.HasBondReference) continue;
       var bondBytes = await catalog.GetResourceBytesAsync(smod.BonePoseKey...);
       var bond = Ts4BondResource.Parse(bondBytes);
       bonds.Add((bond, modifier.Value));
   }
   return bonds;
   ```

**B.4 — DMap parser + application** (1-2 days, follow-up):

Symmetric to BOND but per-vertex via UV1 sampling. Separate session.

**B.5 — Visual verification** (½ day):

After B.2+B.3 the Adult Female waist gap should close (BondMorpher adjusts pelvis/spine bones, both Top and Bottom meshes follow via skin weights). Child face will look LESS skull-like but won't be perfect until B.4 (face uses DMaps not BONDs).

### Estimate (revised)

| Sub-step | Days | Status |
|---|---:|---|
| B.1 Foundation (parsers, probes) | — | ✅ DONE in 0243-0244 |
| B.2 BondMorpher with quaternion math | 1-2 | ✅ DONE in 0245 (translation only; no quat/scale yet) |
| B.3 CAS scene build wire-up | 0.5 | ✅ DONE in 0245 via MainViewModel + BondMorphResolver |
| B.4 DMap parser + application | 1-2 | Following session |
| B.5 Visual verification | 0.5 | ✅ Probe verifies pipeline; visible morph blocked by B.6 |
| B.6 SimInfo modifier weight parser bug | 1 | NEW — see below |
| **Total remaining** | **2-4 days** | |

### B.6a — SimInfo FACE modifier weight parser bug (FIXED in build 0247)

**Root cause:** EA's shipped SimInfos store the FACE modifier weight as a **big-endian** float, not little-endian. TS4SimRipper's `SimModifierData(BinaryReader)` parser reads little-endian for save-game write-back, but their *displayed* weights come from a separate `BlobSimFacialCustomizationData.amount` protobuf path (`Form1.cs:871-932`) — they never tested the binary parser against EA's data, so they didn't notice.

**Fix:** [SimInfoServices.cs](src/Sims4ResourceExplorer.Assets/SimInfoServices.cs) face/body modifier weight reads switched to a new `ReadSingleBigEndian` helper.

**Verified via** `--probe-siminfo-trace` (build 0247) and `--probe-bond-morph`:
- Face weight stats post-fix: `[-0.000038, 0.655484]` — proper genetics-blend range
- Bone-hash overlap still 15/15 for canonical `auRig`
- BondMorphResolver returns 43 adjustments with sane weights and accumulated bone offsets in the `1e-6` to `3e-6` range (small because face BOND offsets themselves are micro-scale CAS adjustments)

### B.5 — BGEO (BlendGeometry) full pipeline shipped (build 0250)

**End-to-end working.** All three morph types now have full pipelines: BOND (bone-delta), DMap (UV1-sampled), and BGEO (per-vertex by VertexID). For the v21 Adult Female test SimInfo:
- 38 modifiers parsed (build 0247 unified-stream parser)
- BondMorpher: 0 adjustments (no SMODs reference BONDs for this Sim)
- DMap morpher: 0 morphs (no SMODs reference DMaps)
- **BGEO morpher: 10 morph entries** with weights 7e-7 to 1.44, max single-vertex displacement 0.008288

**Files added in build 0250:**
- [BlendGeometryResolver.cs](src/Sims4ResourceExplorer.Assets/BlendGeometryResolver.cs) — chains SimInfo → SMOD → BgeoKeys → parsed BGEO
- [BlendGeometryMorpher.cs](src/Sims4ResourceExplorer.Preview/BlendGeometryMorpher.cs) — per-vertex application using LOD 0 vertex range and `mesh.VertexIds[v]` lookup; ADDS the scaled delta (per TS4SimRipper convention; DMap subtracts)
- `Ts4SimBlendGeometryMorph` record in [BlendGeometryResource.cs](src/Sims4ResourceExplorer.Packages/BlendGeometryResource.cs)
- `--probe-bgeo-morph` end-to-end probe in [tools/ProbeAsset/Program.cs](tools/ProbeAsset/Program.cs)

**Critical GEOM parser fix:** `Ts4GeomVertex` now extracts vertex usage codes that were previously skipped:
- **0x07 TagVal** — per-vertex tag bits (used for `vertWeight = ((tag >> 8) & 0xFF) / 64f` in DMap morphing — currently ignored by DMapMorpher; fold in next session for finer-grained morphs)
- **0x0A VertexID** — uint32 used by BGEO blend-map lookup

`CanonicalMesh` gained an optional `IReadOnlyList<uint>? VertexIds` field; BGEO morpher skips meshes without it.

**Build 0258 — DMap X-mirror + per-Sim resolver cache:** EA's DMaps store HALF the body's deltas (positive-X side only); left-side vertices reuse the same map cell with a flipped X. Without this flip, build 0257 produced asymmetric chest/face (verified visually by the user). Fix: `DeformerMapMorpher.MorphMesh` now flips `delta.X` when `mesh.Positions[v*3] < 0`, mirroring TS4SimRipper [DMAP.cs:969-983](docs/references/external/TS4SimRipper/src/DMAP.cs#L969-L983) `GetAdjustedDelta`. Same flip applies to normal deltas. Plus: each morph resolver (`BondMorphResolver`, `DeformerMapResolver`, `BlendGeometryResolver`) memoizes results by SimInfo `FullInstance` — repeat opens of the same Sim skip the SMOD/BOND/DMap/BGEO walk entirely. **User confirmed symmetry fixed in build 0258.**

**Build 0257 — TagVal vertWeight all-zero fallback:** Build 0256 session log revealed BGEO morpher reporting `0 hits, 0 displacement` for every Sim — my TagVal `vertWeight = ((tag & 0x003F0000) >> 16) / 63` was producing 0 when those bits weren't configured (which is the case for nearly all face vertices on default meshes), and the morpher then `continue`d before sampling. TS4SimRipper gates this with a `copyFaceMorphs` flag we don't track. Fix: `bits == 0 ? 1 : bits / 63`, treating "no weighting configured" as "no fade" instead of "exclude vertex." Same fix applied to DMap morpher's `(tag >> 8) & 0xFF / 64`.

**Build 0255 — Stitched UV handling for DMap morphs:** GEOM parser now captures the UVStitch table (vertexIndex → first alternate UV1) instead of skipping it. `CanonicalMesh.StitchUv1ByVertex` carries the dictionary; `DeformerMapMorpher` prefers the stitch UV when sampling so adjacent body parts at a UV seam see the same map cell and stay synchronised under face/body shape morphs. Mirrors TS4SimRipper [PreviewControl.cs:226-244](docs/references/external/TS4SimRipper/src/PreviewControl.cs#L226-L244). Without this, seam vertices could move asymmetrically and create visible cracks at body junctions.

**Build 0254 — v9 DMap defensive skip:** v9 DMaps have a shifted header layout we haven't reverse-engineered yet (TS4SimRipper has no v9 support either). The parser now returns an empty DMap for `version > 7` instead of throwing on the unknown compression byte that surfaced ~30 reads later. `--scan-dmaps` parse rate climbed from 547/564 (97.0%) to 556/564 (98.6%); the remaining 8 failures are all 0-byte payloads (legitimately empty).

**Build 0253 — coverage sweep:** new `--probe-morph-coverage` enumerates one representative SimInfo per (species, age, gender) combo and reports per-resolver counts. Findings against EA's 17 combos:
- **Horse Sims (Adult/Child Female/Male/Unisex):** 14-72 BOND adjustments + 32-44 DMap morphs each, 0 BGEO. Horses use bone-deltas + face DMaps, not BGEOs.
- **Human Child Female (`09FFECB9E92664CB`):** 386 BOND + 70 BGEO morphs (max scaled vertex displacement 0.007738) — should produce visible body+face shape changes.
- **Human Adult Female (test Sim `386F5C479E2AE7FF`):** 0 BOND + 0 DMap + 10 BGEO (max 0.008288, ~8mm visible).
- **Most other Human combos:** 0/0/0 because the picked SimInfo is a default template (all sliders neutral). Not a bug.

5/17 Sim types received at least one morph; the rest are default-slider templates.

**Build 0252 polish:**
- DMap morpher now applies the **normal channel** alongside shape: when a SMOD has a separate `DeformerMapNormalKey` DMap, that morph is now applied to `mesh.Normals` (subtracts the scaled delta, mirroring TS4SimRipper [PreviewControl.cs:278-280](docs/references/external/TS4SimRipper/src/PreviewControl.cs#L278-L280)). Same `vertWeight` from TagVal applies to both shape and normal contributions.

**Build 0251 polish:**
- `CanonicalMesh.VertexTags` plumbed through from the GEOM parser (usage 0x07 was already extracted in build 0250 but not propagated).
- DMap morpher now respects per-vertex `vertWeight = min(((tag >> 8) & 0xFF) / 64, 1)` per TS4SimRipper PreviewControl.cs:271-272 — seam vertices fade smoothly instead of all-or-nothing.
- BGEO morpher now respects per-vertex `vertWeight = ((tag & 0x003F0000) >> 16) / 63` per TS4SimRipper PreviewControl.cs:177-178 (faceMorphs path).
- BGEO morpher now applies normal deltas alongside positions: when a BlendMap entry has `NormalDelta=true`, it fetches `VectorData[blend.Index + (positionDelta ? 1 : 0)]` and adds `delta * weight * vertWeight` to `mesh.Normals`. Renderer-side normalisation handles small drifts (TS4SimRipper convention).

**Current limitations:**
- Only LOD 0 covered (the highest-detail mesh; matches what our scenes actually carry).
- SMOD `region` field is informational; we don't yet route morphs to specific face/body regions per SMOD. Region filtering is currently implicit — BGEO morphs filter via vertex-ID range (each mesh has IDs in a region-specific range) and DMap morphs filter via UV1 bounds (with stitch-aware seam handling as of build 0255). Visual review may reveal cases where this is insufficient.
- v9 DMap header layout — the format shifted between v7 and v9; we defensively skip v9 maps (build 0254) since reverse-engineering needs more samples. Affects ~9 of 564 EA-shipped DMaps (1.6%).



**Discovery (build 0249):** When we wired up the DMap pipeline and ran `--probe-dmap-morph` end-to-end, the resolver returned 0 morph entries even though 38 modifiers resolved to SMODs. Direct SMOD inspection via `--probe-smod EF28B0B8AE53C453` revealed:
- `HasBondReference: False`
- `HasShapeDeformerMap: False`
- `HasNormalDeformerMap: False`
- `BgeoKeys: 1` ← all the morph data lives here

So the v21 Adult Female Sim's modifiers point to **BGEO (BlendGeometry, type `0x067CAA11`)** SMODs, NOT BOND or DMap. BGEO is a third morph type — per-vertex deltas indexed by GEOM vertex ID (not bone, not UV1).

**Shipped (build 0249):**
- [Ts4BlendGeometryResource](src/Sims4ResourceExplorer.Packages/BlendGeometryResource.cs) — full binary BGEO parser. Header (publicKeyCount + externalKeyCount + delayLoadKeyCount + objectCount + ITG-ordered keys + ObjectData), `'BGEO'` magic check, version 0x00000600 check, LOD records (indexBase/numVerts/numDeltaVectors), packed BlendMap (UInt16 per vertex with low-2-bit flags + 14-bit signed offset; running-index walk handles LOD jumps), VectorData (3×UInt16 per vector with sign-bit XOR + /8000 decode).
- `--scan-bgeos` probe: **100% parse rate** (3,166/3,166 EA BGEOs parse cleanly; 11.2M blend-map vertices, 7.6M delta vectors, all 4-LOD).
- 1 unit test for header + LOD + blend flags + vector decode.

**What's blocking visible morphs:** the BGEO application algorithm (TS4SimRipper [PreviewControl.cs:157-195](docs/references/external/TS4SimRipper/src/PreviewControl.cs#L157-L195)) requires per-vertex VertexIDs in the mesh:
```csharp
for each vertex i in mesh:
    vertexID = mesh.getVertexID(i)
    if vertexID in [LOD.IndexBase, LOD.IndexBase + LOD.NumberVertices):
        blendIdx = startIndex + (vertexID - LOD.IndexBase)
        blend = BlendMap[blendIdx]
        if blend.PositionDelta:
            delta = VectorData[blend.Index].ToVector3()
            pos += delta * weight * vertWeight  // BGEO ADDS (DMap subtracts)
```

Our `CanonicalMesh` doesn't currently carry per-vertex VertexIDs. Plumbing them through requires:
1. Adding `IReadOnlyList<uint>? VertexIds` to `CanonicalMesh`
2. Updating the GEOM parser in `BuildBuySceneBuildService.Cas.cs` to extract the VertexID attribute from the GEOM vertex format
3. Building `BlendGeometryResolver` (mirror of `DeformerMapResolver`, follows `SMOD.BgeoKeys` instead)
4. Building `BlendGeometryMorpher` (per-vertex application)
5. Wiring into `MainViewModel.MorphPreviewIfNeeded` after Bond + DMap morphers

**Estimated next session:** 3-5 hours. Once shipped, the Adult Female face modifiers will produce visible shape changes (lipstick subtleties, eye area, cheekbones, etc.).

### B.4 — DMap pipeline (parser + sampler shipped in build 0248; resolver + application next)

**Shipped (build 0248):**
- [Ts4DeformerMapResource](src/Sims4ResourceExplorer.Packages/DeformerMapResource.cs) — binary DMap parser. Handles versions 5/7/9, uncompressed and RLE scan lines, robe-channel detection, totalBytes=0 sentinel for empty maps. Verified via `--scan-dmaps` probe: 547/564 EA-shipped DMaps parse cleanly (97% rate; the 17 failures are zero-byte payloads or v9-specific compression bytes 0xBD/0xBE/0xBF that look like a different scheme — defer until needed).
- [Ts4DeformerMapSampler](src/Sims4ResourceExplorer.Packages/DeformerMapSampler.cs) — eagerly decompresses every scan line into a `Vector3[height, width]` grid. `SampleSkinDelta(x, y) → Vector3` returns the per-pixel offset that vertices sampling that UV1 cell should apply. Handles `0x80 = neutral byte`, RLE walk with per-row index lookup, robe-channel propagation.
- 3 new unit tests (367 total): v7 + uncompressed scan line, v5 fallback defaults, sampler delta math.

**Remaining sub-steps:**
1. **`DeformerMapResolver`** (mirror of `BondMorphResolver`) — given a SimInfo, walk modifiers → SMODs → resolve `DeformerMapShapeKey` and `DeformerMapNormalKey` from the index, parse each DMap, build samplers, and return a list of `(sampler, weight, region)` triples.
2. **Per-vertex DMap application** — given a `CanonicalMesh` with UV1 + tags, for each vertex:
   - Sample the active DMap samplers at `(uv1.X * sampler.Map.Width - sampler.Map.MinCol, uv1.Y * sampler.Map.Height - sampler.Map.MinRow)`
   - Multiply the returned `Vector3` by `(modifier.Weight × vertWeight)` where `vertWeight = min((tag >> 8) & 0xFF / 64, 1)` per TS4SimRipper [PreviewControl.cs:271-275](docs/references/external/TS4SimRipper/src/PreviewControl.cs#L271-L275)
   - Apply: `pos -= scaledDelta` (subtraction, not addition — matches reference)
   - Handle stitched UVs via `GetStitchUVs` for vertices on UV seams (deferred — not all of our meshes carry stitch metadata yet)
3. **Wire into MainViewModel** alongside `BondMorpher`. The `MorphPreviewIfNeeded` helper already runs morphers in sequence; just add a `DeformerMapMorpher.MorphScene(scene, samplers)` call before `BondMorpher.MorphScene`.

**Why this is the unblock for visible morphs:** the v21 Adult Female SimInfo's 38 modifiers all reference DMap-only SMODs (build 0247 `--probe-bond-morph` confirmed 0 BOND adjustments). With sub-step 1+2+3, those 38 modifiers will produce visible face shape morphs (eyes, nose, mouth, cheekbones).

### B.6b — SimInfo unified modifier stream parser (FIXED in build 0247)

**Implementation shipped:** [SimInfoServices.cs](src/Sims4ResourceExplorer.Assets/SimInfoServices.cs) `ReadUnifiedModifierStream` + auto-detect via `LooksLikeUnifiedModifierStream`. Synthetic test data still parses through the legacy TS4SimRipper code path; EA-shipped SimInfos take the unified path.

**Verified result for the v21 Adult Female SimInfo:**
- 38 modifiers parsed (was: 215 face + 246 body of which only ~5 resolved + most weights garbage)
- All 38 weights are in `[0, 0.66]` range (proper genetics blend coefficients)
- All 38 resolve to SimModifier (`0xC5F6763E`) instances — link indices 60-63 hit the actual SMOD region of the linkTable
- 0 BOND adjustments produced because **all 38 SMODs are DMap-only** (face shape morphs use per-vertex DMaps, not bone deltas). This is the correct answer for this Sim.

**What this unblocks:** Once B.4 (DMap parser + UV1 sampling) ships, these 38 modifiers will produce visible face shape morphs. For body morphs (waist/breast/hip), we need a Sim with BodyModifiers that reference BOND-bearing SMODs — those SMODs do exist in EA's data (`--scan-smods` reported `withBond=N` count), but our current test Sim's modifiers are face-only.

**Open follow-ups (low priority, file later):**
- Sculpt records (the first sculptCount=28 records in the unified stream) have byte[0] in [60..63] like modifiers, not in [0..5] (the linkTable's sculpt-typed region). Sculpts likely use a channel-ID lookup, not linkTable index. Doesn't matter for BOND/DMap morphing but matters for any future sculpt-based feature.
- Records 51-56 in the modifier stream had weights 6.4 → 2221 — clamped out by `MaxAbsModifierWeight=2`. Probably encode something other than blend coefficients (translation magnitudes? scale values?). Document if found.
- The 6-byte gap between counters 0x45 and 0x47 (counter 0x46 is missing in the v21 sample) — unknown purpose. The auto-resume in `ReadUnifiedModifierStream` handles it.

### B.6b (initial finding) — SimInfo modifier section is one unified stream (LANDED)

**Major discovery (build 0247 trace):** TS4SimRipper's documented format `[sculptCount byte] [N×1-byte sculpt linkIndices] [faceCount byte] [N×5-byte face mods] [bodyCount byte] [M×5-byte body mods]` is **wrong for EA-shipped SimInfos**. The actual format is a **single contiguous 5-byte record stream** with a **global incrementing counter** at byte+1 of each record.

The `--probe-siminfo-trace` walk produced this layout for the v21 Adult Female SimInfo at TGI `025ED6F4:00000000:386F5C479E2AE7FF`:

| Region | Position | Records | Counter range | Notes |
|---|---|---|---|---|
| sculptCount byte | 0x65 | — | — | Value=28 (matches first 28 records below) |
| Modifier stream A | 0x66–0x182 | 57 | 0x0D – 0x45 | First 51 records have weights ≤ 1.4 (sane); records 51-56 have weights 6.4 → 2221 (probably translation magnitudes, not blend coefficients) |
| Gap | 0x183–0x188 | — | (counter 0x46 missing) | 6 unexplained bytes: `3C 09 46 B8 1E 85` |
| Modifier stream B | 0x189–0x1AF | 8 | 0x47 – 0x4E | Continuation |
| Voice block | 0x1B0–? | — | — | Outfit count UInt32 at 0x1CA = 10 (sane), so voice ends at 0x1CA |

**Key data point:** sculpts are NOT 1 byte each. Records 0..27 (which `sculptCount=28` indicates are sculpts) have the same 5-byte structure as records 28..56. The `[linkIndex(1)] + [BE-float weight(4)]` decode gives weight=0 for all 28 sculpts and ≤1.4 for records 28..50 — exactly what a [-1, 1] genetics blend should look like.

**Crucial implications:**
- The bytes our parser was reading as `faceModifierCount=215` (at position 0x82) are actually the high-bytes of an EARLY modifier record's weight float — pure misalignment artifact.
- There is no separate `faceModifierCount` or `bodyModifierCount` byte in EA-shipped SimInfos. The stream is unified.
- TS4SimRipper's `SIMInfo.cs:109-127` parser doesn't match EA's binary layout because TS4SimRipper only reads SimInfos from save-game protobuf (`Form1.cs:533-551`); their binary parser is for write-back where they choose the layout themselves.

**Open questions for the next session:**
1. **What separates sculpts from face/body modifiers in the unified stream?** sculptCount=28 says first 28 records are sculpts, but how do we identify face vs body in records 28..64? Maybe by the linkIndex byte's value range, or by the resource type the linkIndex resolves to.
2. **Why do records 51-56 have weights 6.4 → 2221?** They might be *not* weight floats — possibly pre-multiplied translation values (CAS slider scale?), OR the format starts changing past a certain channel ID.
3. **What is the 6-byte gap at 0x183?** Counter 0x46 is missing — might be a per-section trailer or padding, or a separator between modifier categories (face vs body).
4. **What does the linkIndex byte map to?** linkTable has 87 entries but observed indices range 0..255 — most don't resolve through the documented linkList. There must be a different lookup path. Possible: indices >= linkList.Count map to a global SMOD registry by hash.

**Diagnostic plan:**
1. Build a `--probe-siminfo-decode` that strips out the unified stream + tries all known weight encodings + reports per-record validity.
2. Cross-check against a v32 SimInfo (post-pronouns) and a non-Adult-Female SimInfo to see if the structure varies.
3. Try replacing the SimInfo modifier section parsing in `SimInfoServices.cs` with a "read until counter pattern breaks, then read voice block" loop. Record the modifiers as a flat list, then partition: first sculptCount records → sculpts, remainder → face+body combined.
4. For BondMorphResolver, treat ALL non-sculpt modifiers as candidate BOND drivers (don't try to distinguish face from body) — let the SMOD itself tell us via its `region` field whether it targets face or body.

Until B.6b ships, body morphs remain inert. Face morphs work for the ~23 records (28..50) that came through with sane weights. Records 51+ are clamped out by the `MaxAbsModifierWeight=2` guard.

### B.6 — SimInfo modifier weight parser bug (initial finding, build 0246)

**Discovery:** The `--probe-bond-morph` probe (added build 0246) confirmed the BOND pipeline is wired correctly end-to-end:
- 21/21 BondMorphResolver bone-slot hashes match canonical `auRig` bone NameHashes
- Raw BOND offsets are sane (max magnitude ~0.026, median 0)
- `BondMorphResolver` returns 112 adjustments for an Adult Female SimInfo

**But:** The SimInfo `BodyModifier` and `FaceModifier` weights come back wildly out of range — observed weights span `-1.42e38` to `0.26` for a single Sim (genetics blend coefficients should be in `[-1, 1]`). When BondMorpher multiplies these into BOND offsets it produces `~1e36` accumulated bone translations, which explode meshes off-screen — visually identical to "no morph at all."

**Defensive workaround applied in 0246:** `BondMorphResolver` now drops any modifier whose weight isn't in `[-2, 2]`. This prevents geometry corruption but means ~64% of modifiers are skipped, so visible morph effect is currently near zero.

**Hypothesis (to verify):** The SimInfo modifier reader (`SimInfoServices.cs` lines 308-324) reads `byte linkIndex + float weight` per entry — same as `TS4SimRipper`'s `SimModifierData(BinaryReader, references)` (`docs/references/external/TS4SimRipper/src/SIMInfo.cs:547-551`). But our parser also returns only 243 of the declared 246 BodyModifier entries — it silently truncates with `BuildPartial`. That truncation suggests stride misalignment somewhere upstream (peltLayer? sculpt? a version-gated extra block we missed for v21).

**Diagnostic plan for next session:**
1. Add a byte-position trace to `Ts4SimInfoParser` gated by an env-var (`SIM_PARSE_TRACE=1`).
2. Re-run `--probe-modifier-tgis` for the v21 Adult Female and the existing v28+ samples; compare deltas.
3. Hex-dump the raw bytes around the face/body modifier section using a new `--dump-siminfo-bytes <tgi> <position> <length>` probe.
4. Likely fix locations: peltLayer record size, sculpt entry size, or a missing version-gated block between `version > 19` and `faceModifierCount`.

Once B.6 is fixed, raise the clamp ceiling in `BondMorphResolver.MaxAbsModifierWeight` and rerun `--probe-bond-morph` — accumulated bone offsets should land in the 1e-3 to 1e-1 range (i.e. visible mesh deformation, the kind that closes the Adult Female waist gap).
