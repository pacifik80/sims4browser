using System.Numerics;
using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Packages;

namespace Sims4ResourceExplorer.Preview;

/// <summary>
/// Applies DMap (DeformerMap) shape morphs to a <see cref="CanonicalScene"/> at the per-vertex
/// level. Each <see cref="Ts4SimDeformerMorph"/> entry carries a parsed sampler plus a weight;
/// for every vertex with UV1 coordinates we sample the appropriate map cell and subtract the
/// scaled delta from the vertex position. Mirrors the algorithm in TS4SimRipper
/// <c>PreviewControl.LoadDMapMorph</c> at <c>docs/references/external/TS4SimRipper/src/PreviewControl.cs:204-290</c>:
///
/// <code>
/// For each vertex v in mesh with UV1:
///   uv = mesh.Uv1[v]
///   x = (int)(map.Width * |uv.x| - map.MinCol - 0.5)
///   y = (int)(map.Height * uv.y - map.MinRow - 0.5)
///   if (x, y) in [0, MaxCol-MinCol] x [0, MaxRow-MinRow]:
///     delta = map.SampleSkinDelta(x, y)
///     v.Position -= delta * morph.Weight * vertWeight   (vertWeight = 1 for now; TS4 uses tags)
/// </code>
///
/// Normal-channel DMaps (<see cref="Ts4SimDeformerMorph.IsNormalMap"/>) are skipped in this
/// pass — applying them needs per-vertex tangent recomputation which we defer until visible
/// shape morphs land.
/// </summary>
public static class DeformerMapMorpher
{
    /// <summary>
    /// Apply the given DMap morphs to <paramref name="scene"/>. Returns a new
    /// <see cref="CanonicalScene"/> with morphed positions; the input is not mutated.
    /// </summary>
    public static CanonicalScene MorphScene(
        CanonicalScene scene,
        IReadOnlyList<Ts4SimDeformerMorph> morphs) =>
        MorphScene(scene, morphs, diagnostics: null);

    /// <summary>
    /// Diagnostic overload — appends summary lines about hit rate and max displacement to
    /// <paramref name="diagnostics"/> if provided.
    /// </summary>
    public static CanonicalScene MorphScene(
        CanonicalScene scene,
        IReadOnlyList<Ts4SimDeformerMorph> morphs,
        IList<string>? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(morphs);
        if (morphs.Count == 0)
        {
            diagnostics?.Add("DeformerMapMorpher.MorphScene: no morphs to apply.");
            return scene;
        }

        var shapeMorphs = morphs.Where(static m => !m.IsNormalMap).ToArray();
        var normalMorphs = morphs.Where(static m => m.IsNormalMap).ToArray();
        if (shapeMorphs.Length == 0 && normalMorphs.Length == 0)
        {
            diagnostics?.Add($"DeformerMapMorpher.MorphScene: {morphs.Count} morphs supplied but none are usable.");
            return scene;
        }

        var anyMeshChanged = false;
        var totalSamples = 0;
        var totalHits = 0;
        var maxDisplacement = 0f;
        var meshesWithoutUv1 = 0;
        var morphedMeshes = new CanonicalMesh[scene.Meshes.Count];
        for (var meshIdx = 0; meshIdx < scene.Meshes.Count; meshIdx++)
        {
            var mesh = scene.Meshes[meshIdx];
            if (mesh.Uv1s is null || mesh.Uv1s.Count == 0)
            {
                meshesWithoutUv1++;
                morphedMeshes[meshIdx] = mesh;
                continue;
            }
            var changed = MorphMesh(mesh, shapeMorphs, normalMorphs, ref totalSamples, ref totalHits, ref maxDisplacement, out var morphed);
            morphedMeshes[meshIdx] = morphed;
            if (changed) anyMeshChanged = true;
        }

        diagnostics?.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"DeformerMapMorpher.MorphScene: {shapeMorphs.Length} shape + {normalMorphs.Length} normal morphs ({morphs.Count} total), {meshesWithoutUv1}/{scene.Meshes.Count} meshes had no UV1, {totalSamples} vertex×morph samples, {totalHits} hits, max displacement = {maxDisplacement:0.######}"));

        if (!anyMeshChanged) return scene;

        var bounds = ComputeBounds(morphedMeshes);
        return scene with { Meshes = morphedMeshes, Bounds = bounds };
    }

    private static bool MorphMesh(
        CanonicalMesh mesh,
        IReadOnlyList<Ts4SimDeformerMorph> shapeMorphs,
        IReadOnlyList<Ts4SimDeformerMorph> normalMorphs,
        ref int totalSamples,
        ref int totalHits,
        ref float maxDisplacement,
        out CanonicalMesh morphed)
    {
        if (mesh.Uv1s is null || mesh.Uv1s.Count == 0 || mesh.Positions.Count == 0)
        {
            morphed = mesh;
            return false;
        }

        var vertexCount = mesh.Positions.Count / 3;
        // UV1 layout in CanonicalMesh is interleaved [u0, v0, u1, v1, ...].
        var uvCount = mesh.Uv1s.Count / 2;
        if (uvCount < vertexCount)
        {
            morphed = mesh;
            return false;
        }

        var morphedPositions = mesh.Positions.ToArray();
        var hasNormals = mesh.Normals.Count == mesh.Positions.Count;
        var morphedNormals = hasNormals && normalMorphs.Count > 0 ? mesh.Normals.ToArray() : null;
        var anyChanged = false;
        var hasTags = mesh.VertexTags is not null && mesh.VertexTags.Count >= vertexCount;
        for (var v = 0; v < vertexCount; v++)
        {
            // TS4SimRipper PreviewControl.cs:226-244 prefers the first stitch UV when a vertex
            // sits on a UV seam — this keeps adjacent body parts at the same map cell so the
            // morph applies consistently across the seam (no asymmetric crack at body junctions).
            float u, t;
            if (mesh.StitchUv1ByVertex is not null && mesh.StitchUv1ByVertex.TryGetValue(v, out var stitchUv) && stitchUv.Length >= 2)
            {
                u = stitchUv[0];
                t = stitchUv[1];
            }
            else
            {
                u = mesh.Uv1s[v * 2];
                t = mesh.Uv1s[v * 2 + 1];
            }
            // TS4SimRipper DMAP.cs:969-983 (`GetAdjustedDelta`) flips delta.X when the vertex
            // lives on the LEFT side of the body (X < 0 in mesh-local coordinates). DMaps only
            // store HALF the body's deltas (positive-X side); the left side reuses the same
            // map cell with a mirrored X. Without this flip both sides receive the same
            // X-direction push, producing the asymmetric chest/face the user reported in
            // build 0257. PreviewControl.cs:262 calls it as `origPos[0] < 0`.
            var mirrorX = mesh.Positions[v * 3] < 0f;
            // TS4SimRipper PreviewControl.cs:271-272 — vertWeight gates each vertex's morph
            // contribution so seam vertices fade smoothly instead of popping. Defaults to 1
            // when the GEOM doesn't carry per-vertex tag bytes.
            var vertWeight = 1f;
            if (hasTags)
            {
                var tag = mesh.VertexTags![v];
                var bits = (tag >> 8) & 0xFFu;
                // Same nuance as BlendGeometryMorpher: an all-zero bit field means "no
                // weighting configured for this vertex," not "exclude this vertex." Default
                // to 1 in that case so meshes without face-morph tag data still receive the
                // morph contribution.
                vertWeight = bits == 0 ? 1f : MathF.Min(bits / 64f, 1f);
            }
            var totalShapeDelta = Vector3.Zero;
            var totalNormalDelta = Vector3.Zero;
            foreach (var morph in shapeMorphs)
            {
                totalSamples++;
                if (TrySampleAt(morph.Sampler, u, t, out var delta))
                {
                    if (mirrorX) delta.X = -delta.X;
                    totalShapeDelta += delta * (morph.Weight * vertWeight);
                    totalHits++;
                }
            }
            if (morphedNormals is not null)
            {
                foreach (var morph in normalMorphs)
                {
                    totalSamples++;
                    if (TrySampleAt(morph.Sampler, u, t, out var delta))
                    {
                        if (mirrorX) delta.X = -delta.X;
                        totalNormalDelta += delta * (morph.Weight * vertWeight);
                        totalHits++;
                    }
                }
            }
            if (totalShapeDelta == Vector3.Zero && totalNormalDelta == Vector3.Zero) continue;
            if (totalShapeDelta != Vector3.Zero)
            {
                // TS4SimRipper subtracts the position delta (PreviewControl.cs:273-275).
                morphedPositions[v * 3 + 0] -= totalShapeDelta.X;
                morphedPositions[v * 3 + 1] -= totalShapeDelta.Y;
                morphedPositions[v * 3 + 2] -= totalShapeDelta.Z;
            }
            if (totalNormalDelta != Vector3.Zero && morphedNormals is not null)
            {
                // PreviewControl.cs:278-280 also subtracts the normal delta. Renderer-side
                // normalisation handles small drifts.
                morphedNormals[v * 3 + 0] -= totalNormalDelta.X;
                morphedNormals[v * 3 + 1] -= totalNormalDelta.Y;
                morphedNormals[v * 3 + 2] -= totalNormalDelta.Z;
            }
            anyChanged = true;
            var mag = totalShapeDelta.Length();
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

    private static bool TrySampleAt(Ts4DeformerMapSampler sampler, float u, float t, out Vector3 delta)
    {
        var map = sampler.Map;
        var x = (int)(MathF.Abs((int)map.Width * u) - map.MinCol - 0.5f);
        var y = (int)((int)map.Height * t - map.MinRow - 0.5f);
        if (x < 0 || x > map.MaxCol - map.MinCol)
        {
            delta = Vector3.Zero;
            return false;
        }
        if (y < 0 || y > map.MaxRow - map.MinRow)
        {
            delta = Vector3.Zero;
            return false;
        }
        delta = sampler.SampleSkinDelta(x, y);
        return delta != Vector3.Zero;
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
