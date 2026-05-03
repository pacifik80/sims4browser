using System.Numerics;
using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Packages;

namespace Sims4ResourceExplorer.Preview;

/// <summary>
/// Applies BGEO (BlendGeometry) shape morphs to a <see cref="CanonicalScene"/> at the per-vertex
/// level by looking up each vertex's <c>VertexID</c> in the BGEO's blend map. Mirrors the
/// algorithm in TS4SimRipper <c>PreviewControl.LoadBGEOMorph</c> at
/// <c>docs/references/external/TS4SimRipper/src/PreviewControl.cs:157-195</c>:
///
/// <code>
/// for each vertex i in mesh:
///   vertexID = mesh.VertexIds[i]
///   for each LOD in BGEO:
///     if vertexID in [LOD.IndexBase, LOD.IndexBase + LOD.NumberVertices):
///       blendIdx = startIndex + (vertexID - LOD.IndexBase)
///       blend = BlendMap[blendIdx]
///       if blend.PositionDelta:
///         delta = VectorData[blend.Index].ToVector3()
///         pos += delta * weight   (BGEO ADDS — DMap subtracts)
///       break
/// </code>
///
/// Targets only LOD 0 (the highest-detail mesh) — handling more requires the GEOM parser to
/// distinguish which LOD a vertex belongs to. Normal deltas are skipped in this pass; applying
/// them would need per-vertex tangent recomputation downstream.
/// </summary>
public static class BlendGeometryMorpher
{
    public static CanonicalScene MorphScene(
        CanonicalScene scene,
        IReadOnlyList<Ts4SimBlendGeometryMorph> morphs) =>
        MorphScene(scene, morphs, diagnostics: null);

    public static CanonicalScene MorphScene(
        CanonicalScene scene,
        IReadOnlyList<Ts4SimBlendGeometryMorph> morphs,
        IList<string>? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(morphs);
        if (morphs.Count == 0)
        {
            diagnostics?.Add("BlendGeometryMorpher.MorphScene: no morphs to apply.");
            return scene;
        }

        var anyMeshChanged = false;
        var totalSamples = 0;
        var totalHits = 0;
        var maxDisplacement = 0f;
        var meshesWithoutVertexIds = 0;
        var morphedMeshes = new CanonicalMesh[scene.Meshes.Count];
        for (var meshIdx = 0; meshIdx < scene.Meshes.Count; meshIdx++)
        {
            var mesh = scene.Meshes[meshIdx];
            if (mesh.VertexIds is null || mesh.VertexIds.Count == 0)
            {
                meshesWithoutVertexIds++;
                morphedMeshes[meshIdx] = mesh;
                continue;
            }
            var changed = MorphMesh(mesh, morphs, ref totalSamples, ref totalHits, ref maxDisplacement, out var morphed);
            morphedMeshes[meshIdx] = morphed;
            if (changed) anyMeshChanged = true;
        }

        diagnostics?.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"BlendGeometryMorpher.MorphScene: {morphs.Count} morphs, {meshesWithoutVertexIds}/{scene.Meshes.Count} meshes had no VertexIds, {totalSamples} vertex×morph samples, {totalHits} hits, max displacement = {maxDisplacement:0.######}"));

        if (!anyMeshChanged) return scene;

        var bounds = ComputeBounds(morphedMeshes);
        return scene with { Meshes = morphedMeshes, Bounds = bounds };
    }

    private static bool MorphMesh(
        CanonicalMesh mesh,
        IReadOnlyList<Ts4SimBlendGeometryMorph> morphs,
        ref int totalSamples,
        ref int totalHits,
        ref float maxDisplacement,
        out CanonicalMesh morphed)
    {
        if (mesh.VertexIds is null || mesh.VertexIds.Count == 0 || mesh.Positions.Count == 0)
        {
            morphed = mesh;
            return false;
        }
        var vertexCount = mesh.Positions.Count / 3;
        if (mesh.VertexIds.Count < vertexCount)
        {
            morphed = mesh;
            return false;
        }

        var morphedPositions = mesh.Positions.ToArray();
        var hasNormals = mesh.Normals.Count == mesh.Positions.Count;
        var morphedNormals = hasNormals ? mesh.Normals.ToArray() : null;
        var anyChanged = false;
        var hasTags = mesh.VertexTags is not null && mesh.VertexTags.Count >= vertexCount;

        // Pre-compute the LOD 0 startIndex (always 0). For higher LODs we'd sum NumberVertices
        // of preceding LODs — but our scenes only carry the top-LOD mesh, so we hit LOD 0 only.
        for (var v = 0; v < vertexCount; v++)
        {
            var vertexId = mesh.VertexIds[v];
            // TS4SimRipper PreviewControl.cs:177-178 (faceMorphs path) — vertWeight scales each
            // vertex's morph contribution. The 6-bit value lives at bits 16-21 of the tag, so
            // divide by 63 to map [0..63] → [0..1]. Defaults to 1 when no tag info is present.
            var vertWeight = 1f;
            if (hasTags)
            {
                var tag = mesh.VertexTags![v];
                var bits = (tag & 0x003F0000u) >> 16;
                // TS4SimRipper PreviewControl.cs:177-178 only applies the divisor when the
                // mesh's `copyFaceMorphs` flag is set — for meshes that don't participate in
                // face-morph weighting, vertWeight stays at 1. We don't track that flag, so we
                // treat "all zero bits" as "no weighting configured" (vertWeight = 1) instead
                // of "weight is zero" (skip vertex). Without this, body meshes with no face-
                // morph tags would never receive BGEO position deltas (verified in build 0256
                // session log: 0/2 meshes had no VertexIds yet 0 samples on the head mesh).
                vertWeight = bits == 0 ? 1f : bits / 63f;
            }
            var totalPositionDelta = Vector3.Zero;
            var totalNormalDelta = Vector3.Zero;
            foreach (var morph in morphs)
            {
                totalSamples++;
                var bgeo = morph.Bgeo;
                if (bgeo.Lods.Count == 0) continue;
                var lod0 = bgeo.Lods[0];
                if (vertexId < lod0.IndexBase || vertexId >= lod0.IndexBase + lod0.NumberVertices) continue;

                var blendIdx = (int)(vertexId - lod0.IndexBase);  // startIndex == 0 for LOD 0
                if (blendIdx < 0 || blendIdx >= bgeo.BlendMap.Count) continue;
                var blend = bgeo.BlendMap[blendIdx];
                if (blend.Index < 0 || blend.Index >= bgeo.VectorData.Count) continue;

                var hit = false;
                if (blend.PositionDelta)
                {
                    var delta = bgeo.VectorData[blend.Index].ToVector3();
                    if (delta != Vector3.Zero)
                    {
                        totalPositionDelta += delta * (morph.Weight * vertWeight);
                        hit = true;
                    }
                }
                // TS4SimRipper PreviewControl.cs:188-189 — normal delta lives at index+1 when
                // position+normal both apply, otherwise at index. We only spend the lookup if
                // the mesh actually carries a normals buffer.
                if (hasNormals && blend.NormalDelta)
                {
                    var normalIdx = blend.Index + (blend.PositionDelta ? 1 : 0);
                    if (normalIdx >= 0 && normalIdx < bgeo.VectorData.Count)
                    {
                        var ndelta = bgeo.VectorData[normalIdx].ToVector3();
                        if (ndelta != Vector3.Zero)
                        {
                            totalNormalDelta += ndelta * (morph.Weight * vertWeight);
                            hit = true;
                        }
                    }
                }
                if (hit) totalHits++;
            }
            if (totalPositionDelta == Vector3.Zero && totalNormalDelta == Vector3.Zero) continue;
            if (totalPositionDelta != Vector3.Zero)
            {
                // BGEO ADDS the scaled delta (PreviewControl.cs:184); DMap subtracts.
                morphedPositions[v * 3 + 0] += totalPositionDelta.X;
                morphedPositions[v * 3 + 1] += totalPositionDelta.Y;
                morphedPositions[v * 3 + 2] += totalPositionDelta.Z;
            }
            if (totalNormalDelta != Vector3.Zero && morphedNormals is not null)
            {
                // PreviewControl.cs:190 ADDS the scaled normal delta. We don't renormalise here
                // — TS4SimRipper doesn't either; renderer-side normalisation handles small drifts.
                morphedNormals[v * 3 + 0] += totalNormalDelta.X;
                morphedNormals[v * 3 + 1] += totalNormalDelta.Y;
                morphedNormals[v * 3 + 2] += totalNormalDelta.Z;
            }
            anyChanged = true;
            var mag = totalPositionDelta.Length();
            if (mag > maxDisplacement) maxDisplacement = mag;
        }

        if (!anyChanged)
        {
            morphed = mesh;
            return false;
        }
        morphed = morphedNormals is not null
            ? mesh with { Positions = morphedPositions, Normals = morphedNormals }
            : mesh with { Positions = morphedPositions };
        return true;
    }

    private static Bounds3D ComputeBounds(IReadOnlyList<CanonicalMesh> meshes)
    {
        var minX = float.MaxValue; var minY = float.MaxValue; var minZ = float.MaxValue;
        var maxX = float.MinValue; var maxY = float.MinValue; var maxZ = float.MinValue;
        var any = false;
        foreach (var mesh in meshes)
        {
            for (var i = 0; i + 2 < mesh.Positions.Count; i += 3)
            {
                var x = mesh.Positions[i];
                var y = mesh.Positions[i + 1];
                var z = mesh.Positions[i + 2];
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (z < minZ) minZ = z;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
                if (z > maxZ) maxZ = z;
                any = true;
            }
        }
        if (!any) return new Bounds3D(0, 0, 0, 0, 0, 0);
        return new Bounds3D(minX, minY, minZ, maxX, maxY, maxZ);
    }
}
