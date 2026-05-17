// Reference: TS4SimRipper (GPL-3.0)
//   docs/references/external/TS4SimRipper/src/PreviewControl.cs:74-155 (LoadBONDMorph)
// Build 0289: Sim character pipeline rewrite — applies a list of BOND morphs to a
// CanonicalScene, vertex-by-vertex per the TS4SimRipper algorithm. Replaces the
// legacy BondMorpher.cs which had two defects:
//   1. It applied translation only (BondMorpher.cs:167-169 explicitly defers scale +
//      rotation as future work).
//   2. It modified scene bones rather than vertex positions; for non-animated render
//      contexts (which is all we have right now), the modified bones never propagate
//      through skinning, so the visible mesh stayed at bind pose.
//
// This version applies (scale, offset, rotation) in the bone's local frame to vertex
// positions directly via SimBondMorpher, mirroring TS4SimRipper's GEOM.BoneMorpher
// + UpdatePositions flow.

using System.Numerics;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.Preview.SimRender;

// SimBondMorph moved to Core/Domain.cs in build 0289.

/// <summary>
/// Applies BOND morphs to a CanonicalScene. Each mesh's vertices are morphed in place
/// against the scene's bones (which carry the canonical rig's bone hashes); vertex
/// blend indices/weights are taken from the mesh's <see cref="VertexWeight"/> entries.
/// </summary>
public static class SimBondSceneMorpher
{
    /// <summary>
    /// Returns a new CanonicalScene with all BOND morphs applied. The rig parameter
    /// supplies bone bind-pose world transforms; the scene itself only gives us
    /// canonical bones with NameHash but no parent traversal data.
    /// </summary>
    public static CanonicalScene MorphScene(
        CanonicalScene scene,
        SimRig rig,
        IReadOnlyList<SimBondMorph> morphs,
        IList<string>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(rig);
        ArgumentNullException.ThrowIfNull(morphs);
        if (morphs.Count == 0)
        {
            diagnostics?.Add("SimBondSceneMorpher: no morphs to apply.");
            return scene;
        }

        // Resolve scene-bone-index → rig-bone-name-hash. The scene's Bones list is built
        // from each CASPart's GEOM at scene assembly time; bone NameHashes carry through.
        // For BOND morphs we need a hash list ordered by scene bone index so a vertex's
        // BlendIndices (which point into the scene's bone array) map correctly to rig
        // bone hashes.
        var sceneBoneHashes = new uint[scene.Bones.Count];
        for (var i = 0; i < scene.Bones.Count; i++)
        {
            sceneBoneHashes[i] = scene.Bones[i].NameHash ?? 0u;
        }

        var totalAppliedAdjustments = 0;
        var totalUnmatchedAdjustments = 0;
        var maxDelta = 0f;

        var morphedMeshes = new CanonicalMesh[scene.Meshes.Count];
        for (var meshIndex = 0; meshIndex < scene.Meshes.Count; meshIndex++)
        {
            var mesh = scene.Meshes[meshIndex];
            morphedMeshes[meshIndex] = MorphMesh(mesh, sceneBoneHashes, rig, morphs,
                ref totalAppliedAdjustments, ref totalUnmatchedAdjustments, ref maxDelta);
        }

        diagnostics?.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"SimBondSceneMorpher: {morphs.Count} morph(s) × {scene.Meshes.Count} mesh(es), {totalAppliedAdjustments} applied + {totalUnmatchedAdjustments} unmatched adjustment(s), max vertex delta = {maxDelta:0.######}"));

        return scene with { Meshes = morphedMeshes, Bounds = ComputeBounds(morphedMeshes) };
    }

    private static CanonicalMesh MorphMesh(
        CanonicalMesh mesh,
        uint[] sceneBoneHashes,
        SimRig rig,
        IReadOnlyList<SimBondMorph> morphs,
        ref int totalApplied,
        ref int totalUnmatched,
        ref float maxDelta)
    {
        if (mesh.Positions.Count == 0) return mesh;
        var vertexCount = mesh.Positions.Count / 3;

        // Convert mesh.Positions (List<float> XYZ-triples) → Vector3[].
        var positions = new Vector3[vertexCount];
        for (var v = 0; v < vertexCount; v++)
        {
            positions[v] = new Vector3(mesh.Positions[v * 3], mesh.Positions[v * 3 + 1], mesh.Positions[v * 3 + 2]);
        }
        var originalPositions = (Vector3[])positions.Clone();

        // Build dense per-vertex blend indices/weights from the sparse VertexWeight list.
        // GEOM stores up to 4 bone influences per vertex; we follow the same convention.
        var blendIndices = new byte[vertexCount][];
        var blendWeights = new byte[vertexCount][];
        for (var v = 0; v < vertexCount; v++)
        {
            blendIndices[v] = new byte[4];
            blendWeights[v] = new byte[4];
        }
        var vertexFillCount = new byte[vertexCount];
        foreach (var sw in mesh.SkinWeights)
        {
            if (sw.VertexIndex < 0 || sw.VertexIndex >= vertexCount) continue;
            var slot = vertexFillCount[sw.VertexIndex];
            if (slot >= 4) continue;
            blendIndices[sw.VertexIndex][slot] = (byte)Math.Clamp(sw.BoneIndex, 0, 255);
            blendWeights[sw.VertexIndex][slot] = (byte)Math.Clamp((int)Math.Round(sw.Weight * 255f), 0, 255);
            vertexFillCount[sw.VertexIndex]++;
        }

        // Apply each BOND morph in order. SimBondMorpher modifies positions in place;
        // we accumulate per-morph application counts for diagnostics.
        foreach (var morph in morphs)
        {
            if (Math.Abs(morph.Weight) < 1e-6f || morph.Adjustments.Count == 0) continue;

            var adjustmentsBefore = totalApplied;
            foreach (var adjustment in morph.Adjustments)
            {
                if (rig.BoneIndexByHash.ContainsKey(adjustment.BoneHash))
                {
                    totalApplied++;
                }
                else
                {
                    totalUnmatched++;
                }
            }

            SimBondMorpher.ApplyBond(rig, positions, blendIndices, blendWeights, sceneBoneHashes, morph.Adjustments, morph.Weight);
        }

        // Track the maximum displacement across all vertices for diagnostics.
        for (var v = 0; v < vertexCount; v++)
        {
            var delta = (positions[v] - originalPositions[v]).Length();
            if (delta > maxDelta) maxDelta = delta;
        }

        // Convert back to List<float>.
        var morphedPositions = new float[vertexCount * 3];
        for (var v = 0; v < vertexCount; v++)
        {
            morphedPositions[v * 3 + 0] = positions[v].X;
            morphedPositions[v * 3 + 1] = positions[v].Y;
            morphedPositions[v * 3 + 2] = positions[v].Z;
        }

        return mesh with { Positions = morphedPositions };
    }

    private static Bounds3D ComputeBounds(IReadOnlyList<CanonicalMesh> meshes)
    {
        var minX = float.PositiveInfinity; var minY = float.PositiveInfinity; var minZ = float.PositiveInfinity;
        var maxX = float.NegativeInfinity; var maxY = float.NegativeInfinity; var maxZ = float.NegativeInfinity;
        var any = false;
        foreach (var mesh in meshes)
        {
            for (var v = 0; v < mesh.Positions.Count / 3; v++)
            {
                var x = mesh.Positions[v * 3];
                var y = mesh.Positions[v * 3 + 1];
                var z = mesh.Positions[v * 3 + 2];
                if (x < minX) minX = x; if (y < minY) minY = y; if (z < minZ) minZ = z;
                if (x > maxX) maxX = x; if (y > maxY) maxY = y; if (z > maxZ) maxZ = z;
                any = true;
            }
        }
        return any
            ? new Bounds3D(minX, minY, minZ, maxX, maxY, maxZ)
            : new Bounds3D(0, 0, 0, 0, 0, 0);
    }
}
