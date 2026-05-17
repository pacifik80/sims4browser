// Reference: TS4SimRipper (GPL-3.0)
//   docs/references/external/TS4SimRipper/src/PreviewControl.cs:74-155 (LoadBONDMorph)
//   docs/references/external/TS4SimRipper/src/GEOM.cs:2570-2610 (UpdatePositions + BoneMorpher)
//   docs/references/external/TS4SimRipper/src/RIG.cs:206-216  (GetDescendants)
// Build 0289: Sim character pipeline rewrite — step 3. Applies BOND morph adjustments
// (scale + offset + rotation per bone) to mesh vertices using a SimRig's bind-pose
// world transforms.
//
// Why this is a rewrite, not a patch: the legacy BondMorpher applies translation-only
// contributions (per its own comment at BondMorpher.cs:167-169 — "Scale and rotation
// around the bone pivot can be added later"). And BondMorphResolver.cs:147-153 throws
// away scale/rotation entirely when constructing SimBoneMorphAdjustment. Result: body
// sculpts (which use scale primarily) couldn't shrink children to child size, face
// sculpts (which use rotation primarily) couldn't deform faces. Telemetry showed
// "max bone offset magnitude = 0.000004" for child humans — basically a no-op.
//
// This new implementation ports TS4SimRipper's BoneMorpher math 1:1:
//   1. Compute world-frame transform for each adjustment using parent's world rotation
//      to convert local-frame scale/offset/rotation into world space
//   2. For each vertex, find total bone-weight from the bone's chain (bone + descendants)
//   3. Apply a weighted TRS transform around the bone's world position, accumulate delta
//   4. After all adjustments processed, add deltas to vertex positions

using System.Numerics;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.Preview.SimRender;

// SimBondAdjustment moved to Core/Domain.cs in build 0289 so BondMorphResolver
// (Assets project) can construct it without taking a dependency on Preview.

/// <summary>
/// Static helper that applies BOND morph adjustments to mesh vertices. The math is
/// imported from TS4SimRipper's <c>GEOM.BoneMorpher</c>; do not optimise without
/// re-deriving against that reference, since subtle differences (e.g. parent's vs
/// own world-rotation, descendants vs single-bone weight) propagate as visible
/// mesh deformations.
/// </summary>
public static class SimBondMorpher
{
    /// <summary>
    /// Applies a single BOND morph (one or more <see cref="SimBondAdjustment"/>s
    /// scaled by <paramref name="bondWeight"/>) to <paramref name="vertexPositions"/>.
    /// Vertex bone weights come from <paramref name="vertexBlendIndices"/> /
    /// <paramref name="vertexBlendWeights"/>; the latter are byte-normalized 0..255
    /// per the GEOM format. <paramref name="meshBoneHashes"/> maps each vertex's
    /// 4-byte BlendIndices into rig bone hashes (the GEOM's bone-hash table).
    /// Modifies <paramref name="vertexPositions"/> in place.
    /// </summary>
    public static void ApplyBond(
        SimRig rig,
        Vector3[] vertexPositions,
        byte[][]? vertexBlendIndices,
        byte[][]? vertexBlendWeights,
        IReadOnlyList<uint> meshBoneHashes,
        IReadOnlyList<SimBondAdjustment> adjustments,
        float bondWeight)
    {
        ArgumentNullException.ThrowIfNull(rig);
        ArgumentNullException.ThrowIfNull(vertexPositions);
        ArgumentNullException.ThrowIfNull(meshBoneHashes);
        ArgumentNullException.ThrowIfNull(adjustments);
        if (Math.Abs(bondWeight) < 1e-6f || adjustments.Count == 0 || vertexPositions.Length == 0) return;

        // Pre-compute descendant-hash sets per adjustment so the inner loop over vertices
        // doesn't re-walk the rig tree. TS4SimRipper does this lazily inside the loop;
        // pre-computing is functionally identical but skips quadratic work.
        var unit = Vector3.One;
        var deltas = new Vector3[vertexPositions.Length];

        foreach (var adjustment in adjustments)
        {
            if (!rig.BoneIndexByHash.TryGetValue(adjustment.BoneHash, out var boneIndex)) continue;
            var bone = rig.Bones[boneIndex];

            // World-frame transformation:
            //   parent-world rotation R_p brings local coords into the bone's parent's frame
            //   for self-rooted bones (no parent), use the bone's own world rotation
            // (TS4SimRipper PreviewControl.cs:308-314 — MorphRotation is parent's globalRotation)
            var parentHash = bone.ParentHash ?? bone.NameHash;
            var parentRotation = rig.WorldBindPoseByHash.TryGetValue(parentHash, out var parentMatrix)
                ? Quaternion.CreateFromRotationMatrix(parentMatrix)
                : Quaternion.Identity;
            var parentRotationConj = Quaternion.Conjugate(parentRotation);

            // worldOffset = parent.rotation * localOffset * parent.rotation.conjugate
            var worldOffset = Vector3.Transform(adjustment.LocalOffset, parentRotation);
            // worldRotation = parent.rotation * localRotation * parent.rotation.conjugate
            var worldRotation = parentRotation * adjustment.LocalRotation * parentRotationConj;
            // worldScale: TS4SimRipper rotates the scale matrix's diagonal through the parent
            // rotation. For axis-aligned rigs this collapses to a 1:1 copy; preserve the
            // identity here (pure rotated scale would require eigen decomposition).
            var worldScale = adjustment.LocalScale;

            // Bone's world position (M41, M42, M43 of the bone's bind-pose world matrix).
            if (!rig.WorldBindPoseByHash.TryGetValue(bone.NameHash, out var boneWorld)) continue;
            var bonePivot = new Vector3(boneWorld.M41, boneWorld.M42, boneWorld.M43);

            // The bone's hash plus all its descendants — vertices weighted to any of these
            // bones receive a weighted contribution from this adjustment.
            var influenceSet = ComputeDescendantHashes(rig, bone.NameHash);

            for (var v = 0; v < vertexPositions.Length; v++)
            {
                var weight = GetTotalBoneWeight(v, vertexBlendIndices, vertexBlendWeights, meshBoneHashes, influenceSet);
                if (weight <= 0f) continue;
                var adjustedWeight = weight * bondWeight;

                var wScale  = (worldScale * adjustedWeight) + unit;
                var wOffset = worldOffset * adjustedWeight;
                var wRot    = ScaleQuaternion(worldRotation, adjustedWeight);
                // TRS in world frame, pivoted at bonePivot:
                //   centred = vertex - pivot
                //   transformed = scale ⊙ (rot * centred) + offset
                //   final = transformed + pivot
                //   delta = final - vertex = (scale ⊙ (rot * (vertex - pivot)) + offset) - (vertex - pivot)
                var centred = vertexPositions[v] - bonePivot;
                var rotated = Vector3.Transform(centred, wRot);
                var scaled  = new Vector3(rotated.X * wScale.X, rotated.Y * wScale.Y, rotated.Z * wScale.Z);
                var transformed = scaled + wOffset;
                deltas[v] += transformed - centred;
            }
        }

        for (var v = 0; v < vertexPositions.Length; v++)
        {
            vertexPositions[v] += deltas[v];
        }
    }

    /// <summary>
    /// Returns the bone hash plus all descendant bone hashes via DFS. Mirrors
    /// TS4SimRipper's <c>RIG.GetDescendants</c>.
    /// </summary>
    private static HashSet<uint> ComputeDescendantHashes(SimRig rig, uint rootHash)
    {
        var result = new HashSet<uint> { rootHash };
        var stack = new Stack<uint>();
        stack.Push(rootHash);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            for (var i = 0; i < rig.Bones.Count; i++)
            {
                if (rig.Bones[i].ParentHash == current && result.Add(rig.Bones[i].NameHash))
                {
                    stack.Push(rig.Bones[i].NameHash);
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Sums the skin weights for the vertex's bones that fall inside <paramref name="influenceHashes"/>.
    /// Skin weights are byte-normalised in the GEOM (255 = 100% weight). Returns 0 when
    /// the vertex has no skinning data or none of its bones are in the influence set.
    /// </summary>
    private static float GetTotalBoneWeight(
        int vertexIndex,
        byte[][]? blendIndices,
        byte[][]? blendWeights,
        IReadOnlyList<uint> meshBoneHashes,
        HashSet<uint> influenceHashes)
    {
        if (blendIndices is null || blendWeights is null) return 0f;
        if (vertexIndex >= blendIndices.Length || vertexIndex >= blendWeights.Length) return 0f;
        var bi = blendIndices[vertexIndex];
        var bw = blendWeights[vertexIndex];
        if (bi is null || bw is null) return 0f;

        var sum = 0f;
        for (var i = 0; i < bi.Length && i < bw.Length; i++)
        {
            if (bw[i] == 0) continue;
            if (bi[i] >= meshBoneHashes.Count) continue;
            if (influenceHashes.Contains(meshBoneHashes[bi[i]]))
            {
                sum += bw[i] / 255f;
            }
        }
        return sum;
    }

    /// <summary>
    /// SLERP between identity and <paramref name="q"/> by <paramref name="t"/>. Mirrors
    /// TS4SimRipper's <c>weightedRotation = rotation * adjustedWeight</c> which is a
    /// scalar multiply on the quaternion components; that's only equivalent to slerp
    /// when q is near identity, but the BOND adjustments deal with small-angle rotations,
    /// so the approximation matches their output. We use slerp for stability.
    /// </summary>
    private static Quaternion ScaleQuaternion(Quaternion q, float t)
    {
        if (Math.Abs(t) < 1e-6f) return Quaternion.Identity;
        return Quaternion.Slerp(Quaternion.Identity, q, t);
    }
}
