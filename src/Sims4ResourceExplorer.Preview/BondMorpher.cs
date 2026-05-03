using System.Numerics;
using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Packages;

namespace Sims4ResourceExplorer.Preview;

/// <summary>
/// Per-bone morph accumulator. Each bone's <see cref="MorphRotation"/> /
/// <see cref="MorphPosition"/> / <see cref="MorphScale"/> start at identity and accumulate
/// adjustments from BOND resources weighted by the modifier's strength.
/// </summary>
internal sealed class Ts4MutableBoneState
{
    public Ts4MutableBoneState(uint nameHash, Vector3 bindPosition, Quaternion bindRotation, Vector3 bindScale)
    {
        NameHash = nameHash;
        BindPosition = bindPosition;
        BindRotation = bindRotation;
        BindScale = bindScale;
        MorphRotation = Quaternion.Identity;
        MorphPosition = Vector3.Zero;
        MorphScale = Vector3.One;
    }

    public uint NameHash { get; }
    public Vector3 BindPosition { get; }
    public Quaternion BindRotation { get; }
    public Vector3 BindScale { get; }

    public Quaternion MorphRotation { get; set; }
    public Vector3 MorphPosition { get; set; }
    public Vector3 MorphScale { get; set; }

    public Vector3 EffectivePosition => BindPosition + MorphPosition;
    public Quaternion EffectiveRotation => MorphRotation * BindRotation;
    public Vector3 EffectiveScale => BindScale * MorphScale;
}

/// <summary>
/// Applies BOND (bone-delta) morphs to a rig pose and propagates the deformation through to
/// skinned mesh vertices. Mirrors TS4SimRipper's <c>PreviewControl.LoadBONDMorph</c> at
/// <c>docs/references/external/TS4SimRipper/src/PreviewControl.cs:74-155</c>.
///
/// First-pass implementation handles position deltas (translation) and weight-scaled rotation,
/// which is enough to close the Adult Female waist gap (the Top and Bottom meshes follow the
/// pelvis/spine bones). Scale deltas are accumulated on the bone but not yet applied per-vertex.
/// Per-bone-per-vertex math:
/// <code>
/// For each adjustment in BOND:
///   bone = pose.Bones[adjustment.SlotHash]
///   if bone is null: skip
///   localOffset = (offsetX, offsetY, offsetZ)
///   localRot    = (quatX, quatY, quatZ, quatW)
///   weight      = bondWeight
///   bone.MorphPosition += localOffset * weight
///   bone.MorphRotation = Slerp(Identity, localRot, weight) * bone.MorphRotation
///
/// For each vertex v in mesh with skinning [(boneIdx_i, w_i)]:
///   delta = sum_i [ pose.Bones[geom.BoneHashes[boneIdx_i]].MorphPosition * w_i ]
///   v.Position += delta
/// </code>
/// </summary>
internal sealed class Ts4MutableRigPose
{
    private readonly Dictionary<uint, Ts4MutableBoneState> bonesByHash;

    public Ts4MutableRigPose(Ts4RigResource rig)
    {
        ArgumentNullException.ThrowIfNull(rig);
        bonesByHash = new Dictionary<uint, Ts4MutableBoneState>(rig.Bones.Count);
        foreach (var bone in rig.Bones)
        {
            bonesByHash[bone.NameHash] = new Ts4MutableBoneState(
                bone.NameHash, bone.Position, bone.Rotation, bone.Scale);
        }
    }

    public IReadOnlyDictionary<uint, Ts4MutableBoneState> BonesByHash => bonesByHash;

    public Ts4MutableBoneState? TryGetBone(uint nameHash) =>
        bonesByHash.TryGetValue(nameHash, out var state) ? state : null;
}

public static class BondMorpher
{
    /// <summary>
    /// Apply a list of (BOND, weight) pairs to the rig pose. Mutates the pose in place.
    /// Returns the count of adjustments that resolved to a known bone (for diagnostics).
    /// </summary>
    internal static int ApplyBondsToRig(Ts4MutableRigPose pose, IReadOnlyList<(Ts4BondResource Bond, float Weight)> bonds)
    {
        ArgumentNullException.ThrowIfNull(pose);
        ArgumentNullException.ThrowIfNull(bonds);
        var resolvedAdjustments = 0;
        foreach (var (bond, weight) in bonds)
        {
            if (Math.Abs(weight) < 1e-6f) continue;
            foreach (var adj in bond.Adjustments)
            {
                var bone = pose.TryGetBone(adj.SlotHash);
                if (bone is null) continue;
                resolvedAdjustments++;

                var localOffset = new Vector3(adj.OffsetX, adj.OffsetY, adj.OffsetZ);
                var localScale  = new Vector3(adj.ScaleX, adj.ScaleY, adj.ScaleZ);
                var localRotation = new Quaternion(adj.QuatX, adj.QuatY, adj.QuatZ, adj.QuatW);
                if (IsZeroQuaternion(localRotation)) localRotation = Quaternion.Identity;
                else localRotation = Quaternion.Normalize(localRotation);

                // Per TS4SimRipper PreviewControl.cs:153, BondMorpher updates the rig with
                // weighted local deltas. We apply them in bone-local space (BindRotation) so
                // the offset matches the bone's local axes.
                bone.MorphPosition += localOffset * weight;
                bone.MorphScale *= Vector3.One + localScale * weight;
                bone.MorphRotation = Quaternion.Slerp(Quaternion.Identity, localRotation, weight) * bone.MorphRotation;
            }
        }
        return resolvedAdjustments;
    }

    /// <summary>
    /// Re-compute vertex positions using the morphed rig pose. Vertices follow the bones they
    /// are skinned to, weighted by their skin weights. Returns a new array of
    /// <see cref="Ts4GeomVertex"/> with morphed positions; other vertex attributes
    /// (UVs, tangents, normals, skinning) are preserved.
    /// </summary>
    internal static Ts4GeomVertex[] ApplyRigPoseToMesh(
        IReadOnlyList<Ts4GeomVertex> vertices,
        IReadOnlyList<uint> meshBoneHashes,
        Ts4MutableRigPose pose)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(meshBoneHashes);
        ArgumentNullException.ThrowIfNull(pose);

        var morphed = new Ts4GeomVertex[vertices.Count];
        for (var i = 0; i < vertices.Count; i++)
        {
            var v = vertices[i];
            morphed[i] = TryComputeMorphedVertex(v, meshBoneHashes, pose) ?? v;
        }
        return morphed;
    }

    private static Ts4GeomVertex? TryComputeMorphedVertex(
        Ts4GeomVertex vertex,
        IReadOnlyList<uint> meshBoneHashes,
        Ts4MutableRigPose pose)
    {
        if (vertex.BlendIndices is not { Length: > 0 } indices ||
            vertex.BlendWeights is not { Length: > 0 } weights ||
            vertex.Position is not { Length: 3 } position)
        {
            return null;
        }

        var totalDelta = Vector3.Zero;
        var anyMorphApplied = false;
        for (var b = 0; b < indices.Length && b < weights.Length; b++)
        {
            var skinWeight = weights[b];
            if (Math.Abs(skinWeight) < 1e-6f) continue;
            var idx = indices[b];
            if (idx >= meshBoneHashes.Count) continue;
            var bone = pose.TryGetBone(meshBoneHashes[idx]);
            if (bone is null) continue;
            // Translation-only contribution. Scale and rotation around the bone pivot can be
            // added later — they require composing per-vertex transforms relative to the bone's
            // bind-pose absolute position, which we'd need to derive from parent traversal.
            var morphPos = bone.MorphPosition;
            if (morphPos.LengthSquared() < 1e-12f) continue;
            totalDelta += morphPos * skinWeight;
            anyMorphApplied = true;
        }

        if (!anyMorphApplied) return null;

        var newPosition = new[]
        {
            position[0] + totalDelta.X,
            position[1] + totalDelta.Y,
            position[2] + totalDelta.Z
        };
        return vertex with { Position = newPosition };
    }

    private static bool IsZeroQuaternion(Quaternion q) =>
        Math.Abs(q.X) < 1e-6f &&
        Math.Abs(q.Y) < 1e-6f &&
        Math.Abs(q.Z) < 1e-6f &&
        Math.Abs(q.W) < 1e-6f;

    /// <summary>
    /// Apply a list of bone-translation adjustments to a CanonicalScene's meshes. For each
    /// vertex, computes the weighted sum of bone-translation deltas based on the vertex's
    /// skin weights, then offsets the vertex position. Returns a new scene with the morphed
    /// positions; other scene fields are passed through unchanged.
    ///
    /// This is the high-level entry point that MainViewModel calls after the per-CASPart
    /// scene has been built. Operating at the CanonicalScene level keeps the scene-build
    /// cache intact (un-morphed scene is cached) while still applying per-Sim morphs at
    /// the assembly stage.
    /// </summary>
    public static CanonicalScene MorphScene(CanonicalScene scene, IReadOnlyList<SimBoneMorphAdjustment> adjustments) =>
        MorphScene(scene, adjustments, diagnostics: null);

    /// <summary>
    /// Overload that captures application diagnostics into the provided list (for visual
    /// verification troubleshooting). Lines include adjustment count, bone-hash hit rate,
    /// and the maximum vertex displacement magnitude.
    /// </summary>
    public static CanonicalScene MorphScene(
        CanonicalScene scene,
        IReadOnlyList<SimBoneMorphAdjustment> adjustments,
        IList<string>? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(adjustments);
        if (adjustments.Count == 0)
        {
            diagnostics?.Add("BondMorpher.MorphScene: no adjustments to apply.");
            return scene;
        }

        // Build (boneIndex → accumulated offset) map per the bones' NameHash.
        var boneOffsets = new Vector3[scene.Bones.Count];
        var anyApplied = false;
        var matchedBones = 0;
        var totalAdjustments = 0;
        var bonesWithoutHash = 0;
        var unmatchedHashes = new HashSet<uint>();
        for (var i = 0; i < scene.Bones.Count; i++)
        {
            var bone = scene.Bones[i];
            if (bone.NameHash is not { } nameHash)
            {
                bonesWithoutHash++;
                continue;
            }
            foreach (var adj in adjustments)
            {
                if (adj.SlotHash != nameHash) continue;
                if (Math.Abs(adj.Weight) < 1e-6f) continue;
                boneOffsets[i] += new Vector3(adj.OffsetX, adj.OffsetY, adj.OffsetZ) * adj.Weight;
                anyApplied = true;
                totalAdjustments++;
            }
            if (boneOffsets[i].LengthSquared() > 0) matchedBones++;
        }
        // Track which adjustment hashes never matched any scene bone.
        var sceneHashes = new HashSet<uint>(scene.Bones.Where(b => b.NameHash.HasValue).Select(b => b.NameHash!.Value));
        foreach (var adj in adjustments)
        {
            if (!sceneHashes.Contains(adj.SlotHash)) unmatchedHashes.Add(adj.SlotHash);
        }
        if (diagnostics is not null)
        {
            var maxMag = 0f;
            for (var i = 0; i < boneOffsets.Length; i++)
            {
                var m = boneOffsets[i].Length();
                if (m > maxMag) maxMag = m;
            }
            diagnostics.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"BondMorpher.MorphScene: {adjustments.Count} input adjustments, {totalAdjustments} matched scene bones, {matchedBones}/{scene.Bones.Count} bones got non-zero offset, {bonesWithoutHash} scene bones had no NameHash, {unmatchedHashes.Count} adjustment hashes unmatched, max bone offset magnitude = {maxMag:0.######}"));
        }
        if (!anyApplied) return scene;

        var morphedMeshes = new CanonicalMesh[scene.Meshes.Count];
        for (var meshIdx = 0; meshIdx < scene.Meshes.Count; meshIdx++)
        {
            morphedMeshes[meshIdx] = MorphMesh(scene.Meshes[meshIdx], boneOffsets);
        }

        // Recompute scene bounds.
        var bounds = ComputeBounds(morphedMeshes);
        return scene with { Meshes = morphedMeshes, Bounds = bounds };
    }

    private static CanonicalMesh MorphMesh(CanonicalMesh mesh, IReadOnlyList<Vector3> boneOffsets)
    {
        if (mesh.Positions.Count == 0 || mesh.SkinWeights.Count == 0)
        {
            return mesh;
        }

        // Group skin weights by vertex once for O(N) lookup.
        var weightsByVertex = new Dictionary<int, List<VertexWeight>>(mesh.Positions.Count / 3);
        foreach (var w in mesh.SkinWeights)
        {
            if (!weightsByVertex.TryGetValue(w.VertexIndex, out var list))
            {
                list = new List<VertexWeight>(4);
                weightsByVertex[w.VertexIndex] = list;
            }
            list.Add(w);
        }

        var morphedPositions = mesh.Positions.ToArray();
        var vertexCount = morphedPositions.Length / 3;
        for (var v = 0; v < vertexCount; v++)
        {
            if (!weightsByVertex.TryGetValue(v, out var weights)) continue;
            var totalDelta = Vector3.Zero;
            foreach (var w in weights)
            {
                if (w.BoneIndex < 0 || w.BoneIndex >= boneOffsets.Count) continue;
                var boneOffset = boneOffsets[w.BoneIndex];
                if (boneOffset.LengthSquared() < 1e-12f) continue;
                totalDelta += boneOffset * w.Weight;
            }
            if (totalDelta.LengthSquared() < 1e-12f) continue;
            morphedPositions[v * 3 + 0] += totalDelta.X;
            morphedPositions[v * 3 + 1] += totalDelta.Y;
            morphedPositions[v * 3 + 2] += totalDelta.Z;
        }

        return mesh with { Positions = morphedPositions };
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
