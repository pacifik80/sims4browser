// RigExport — emits a rig.json next to each exported asset, carrying the full rigged mesh:
// geometry (positions/UVs/triangles), the skeleton (bones with bind/inverse-bind matrices),
// and per-vertex skin weights (4 bone influences per vertex, normalized to sum 1). A Unity
// builder reads this file directly, so the JSON field names below are LOAD-BEARING and must
// match the agreed schema EXACTLY (camelCase as written).
//
// rig.json lives at <UnityAssetsDir>/<slug>/rig.json — the SAME asset folder as the OBJ and
// Textures (the texture PNGs referenced by material.* are already written by the main export).
//
// Unlike the OBJ/FBX path (which exports static / bind-pose only), rig.json keeps the skinning
// so the mesh can be rebuilt as a SkinnedMeshRenderer in Unity.

using System.Text.Json;
using System.Text.Json.Serialization;
using Sims4ResourceExplorer.Core;

namespace Sims4UnityExport;

// ---------------------------------------------------------------------------
// rig.json data contract (serialized verbatim per the agreed schema).
// ---------------------------------------------------------------------------
internal sealed class RigManifest
{
    [JsonPropertyName("asset")] public string Asset { get; set; } = string.Empty;
    [JsonPropertyName("mesh")] public RigMesh Mesh { get; set; } = new();
    [JsonPropertyName("bones")] public List<RigBone> Bones { get; set; } = new();
    [JsonPropertyName("material")] public RigMaterial Material { get; set; } = new();
}

internal sealed class RigMesh
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("vertexCount")] public int VertexCount { get; set; }
    [JsonPropertyName("positions")] public List<float> Positions { get; set; } = new();
    // EA's AUTHORED per-vertex normals (flat 3*vertexCount, SAME order/space as positions, emitted
    // verbatim). Empty when the source CanonicalMesh carried none (builder recomputes as a fallback).
    [JsonPropertyName("normals")] public List<float> Normals { get; set; } = new();
    [JsonPropertyName("uv0")] public List<float> Uv0 { get; set; } = new();
    [JsonPropertyName("uv1")] public List<float> Uv1 { get; set; } = new();
    [JsonPropertyName("triangles")] public List<int> Triangles { get; set; } = new();
    [JsonPropertyName("boneIndices")] public List<int> BoneIndices { get; set; } = new();
    [JsonPropertyName("boneWeights")] public List<float> BoneWeights { get; set; } = new();
}

internal sealed class RigBone
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("parentName")] public string? ParentName { get; set; }
    [JsonPropertyName("bindPose")] public float[] BindPose { get; set; } = Array.Empty<float>();
    [JsonPropertyName("inverseBindPose")] public float[] InverseBindPose { get; set; } = Array.Empty<float>();
}

internal sealed class RigMaterial
{
    [JsonPropertyName("diffuse")] public string? Diffuse { get; set; }
    [JsonPropertyName("normal")] public string? Normal { get; set; }
    [JsonPropertyName("specular")] public string? Specular { get; set; }
    [JsonPropertyName("shadow")] public string? Shadow { get; set; }
}

internal static class RigExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // parentName is JSON null for the root bone, and material.* are null when absent —
        // the schema requires those nulls to be present, so never drop them.
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    // 16-float row-major identity matrix, reused for bones missing a bind/inverse-bind pose.
    private static float[] Identity() => new float[]
    {
        1f, 0f, 0f, 0f,
        0f, 1f, 0f, 0f,
        0f, 0f, 1f, 0f,
        0f, 0f, 0f, 1f
    };

    public static void WriteRigJson(string unityAssetsDir, string slug, CanonicalScene scene)
    {
        var assetFolder = Path.Combine(unityAssetsDir, slug);
        Directory.CreateDirectory(assetFolder);

        var manifest = new RigManifest { Asset = slug };

        // Primary mesh: the one carrying the MOST skin weights (so a rigged body wins over an
        // unskinned prop). Fall back to the most vertices, then the first mesh.
        var primary = scene.Meshes
                          .OrderByDescending(m => m.SkinWeights.Count)
                          .ThenByDescending(m => m.Positions.Count)
                          .FirstOrDefault();

        if (scene.Meshes.Count > 1)
        {
            Console.WriteLine(
                $"[rig] Scene has {scene.Meshes.Count} meshes; exporting only the primary " +
                $"('{primary?.Name}', {primary?.SkinWeights.Count ?? 0} skin weights) to rig.json for now.");
        }

        if (primary is null)
        {
            // No geometry at all — still write a valid (empty-mesh) rig.json plus whatever
            // bones/material the scene carries so the Unity builder never hits a missing file.
            Console.WriteLine("[rig] Scene has no meshes; writing a rig.json with an empty mesh.");
            manifest.Bones = BuildBones(scene);
            manifest.Material = BuildMaterial(scene, primaryMaterialIndex: 0);
            WriteManifest(assetFolder, manifest);
            return;
        }

        var vertexCount = primary.Positions.Count / 3;

        // --- Geometry ---------------------------------------------------------------------
        manifest.Mesh.Name = primary.Name;
        manifest.Mesh.VertexCount = vertexCount;
        manifest.Mesh.Positions = primary.Positions.ToList();

        // EA's authored normals, verbatim (same flat 3*N order/space as positions; empty if none).
        manifest.Mesh.Normals = (primary.Normals ?? (IReadOnlyList<float>)Array.Empty<float>()).ToList();

        // uv0 = Uvs if present (the resolved/preferred channel), else Uv0s; uv1 = Uv1s or empty.
        var uv0 = primary.Uvs is { Count: > 0 } ? primary.Uvs : (primary.Uv0s ?? (IReadOnlyList<float>)Array.Empty<float>());
        manifest.Mesh.Uv0 = uv0.ToList();
        manifest.Mesh.Uv1 = (primary.Uv1s ?? (IReadOnlyList<float>)Array.Empty<float>()).ToList();

        manifest.Mesh.Triangles = primary.Indices.ToList();

        // --- Skin weights (4 influences per vertex, normalized) ---------------------------
        var (boneIndices, boneWeights) = BuildBlendWeights(primary, vertexCount);
        manifest.Mesh.BoneIndices = boneIndices;
        manifest.Mesh.BoneWeights = boneWeights;

        // --- Bones ------------------------------------------------------------------------
        manifest.Bones = BuildBones(scene);

        // --- Material ---------------------------------------------------------------------
        manifest.Material = BuildMaterial(scene, primary.MaterialIndex);

        WriteManifest(assetFolder, manifest);
        Console.WriteLine(
            $"[rig] Wrote rig.json: mesh='{manifest.Mesh.Name}' verts={vertexCount} " +
            $"tris={manifest.Mesh.Triangles.Count / 3} bones={manifest.Bones.Count} " +
            $"diffuse='{manifest.Material.Diffuse ?? "(none)"}'.");
    }

    // Build the per-vertex 4-influence boneIndices/boneWeights arrays (length 4*vertexCount).
    // Each vertex takes its up-to-4 highest-weight influences, then the 4 weights are normalized
    // to sum to 1. A vertex with no influences binds to bone 0 with weight 1 (harmless).
    private static (List<int> BoneIndices, List<float> BoneWeights) BuildBlendWeights(
        CanonicalMesh mesh, int vertexCount)
    {
        var slots = Math.Max(0, vertexCount) * 4;
        var boneIndices = new int[slots];
        var boneWeights = new float[slots];

        // Group skin weights by vertex.
        var byVertex = new Dictionary<int, List<VertexWeight>>();
        foreach (var weight in mesh.SkinWeights)
        {
            if (weight.VertexIndex < 0 || weight.VertexIndex >= vertexCount)
            {
                continue; // out-of-range influence — skip defensively
            }

            if (!byVertex.TryGetValue(weight.VertexIndex, out var list))
            {
                list = new List<VertexWeight>();
                byVertex[weight.VertexIndex] = list;
            }

            list.Add(weight);
        }

        for (var v = 0; v < vertexCount; v++)
        {
            var baseSlot = v * 4;

            if (!byVertex.TryGetValue(v, out var influences) || influences.Count == 0)
            {
                // No influence: bind to bone 0 with full weight so the vertex is not orphaned.
                boneIndices[baseSlot] = 0;
                boneWeights[baseSlot] = 1f;
                continue;
            }

            // Take up to the 4 highest-weight influences.
            var top = influences
                .OrderByDescending(w => w.Weight)
                .Take(4)
                .ToList();

            var sum = 0f;
            for (var i = 0; i < top.Count; i++)
            {
                boneIndices[baseSlot + i] = top[i].BoneIndex;
                boneWeights[baseSlot + i] = top[i].Weight;
                sum += top[i].Weight;
            }

            if (sum > 0f)
            {
                // Normalize the (up to) 4 weights so they sum to 1.
                for (var i = 0; i < top.Count; i++)
                {
                    boneWeights[baseSlot + i] /= sum;
                }
            }
            else
            {
                // All influences were zero-weight: fall back to bone 0 with full weight.
                boneIndices[baseSlot] = top[0].BoneIndex;
                boneWeights[baseSlot] = 1f;
            }
        }

        return (boneIndices.ToList(), boneWeights.ToList());
    }

    // Build the bone list in scene order (this order is what every VertexWeight.BoneIndex
    // references). Missing bind/inverse-bind matrices fall back to identity.
    private static List<RigBone> BuildBones(CanonicalScene scene)
    {
        var bones = new List<RigBone>(scene.Bones.Count);
        foreach (var bone in scene.Bones)
        {
            bones.Add(new RigBone
            {
                Name = bone.Name,
                ParentName = string.IsNullOrEmpty(bone.ParentName) ? null : bone.ParentName,
                BindPose = bone.BindPoseMatrix is { Length: 16 } bind ? bind : Identity(),
                InverseBindPose = bone.InverseBindPoseMatrix is { Length: 16 } inv ? inv : Identity()
            });
        }

        return bones;
    }

    // Resolve the primary mesh's material textures into relative "Textures/<file>.png" paths.
    private static RigMaterial BuildMaterial(CanonicalScene scene, int primaryMaterialIndex)
    {
        var material =
            (primaryMaterialIndex >= 0 && primaryMaterialIndex < scene.Materials.Count)
                ? scene.Materials[primaryMaterialIndex]
                : scene.Materials.FirstOrDefault();

        if (material is null)
        {
            return new RigMaterial();
        }

        return new RigMaterial
        {
            Diffuse = ToRelPath(PickBySlot(material.Textures, "diffuse")),
            Normal = ToRelPath(PickBySlot(material.Textures, "normal")),
            Specular = ToRelPath(PickBySlot(material.Textures, "specular")),
            Shadow = ToRelPath(PickBySlot(material.Textures, "shadow"))
        };
    }

    private static string? ToRelPath(CanonicalTexture? texture) =>
        texture is null ? null : $"Textures/{texture.FileName}";

    // ---------------------------------------------------------------------------
    // Slot resolution — mirrors SwatchExporter.PickBySlot (semantic first, then slot-name).
    // ---------------------------------------------------------------------------
    private static CanonicalTexture? PickBySlot(IReadOnlyList<CanonicalTexture> textures, string slot)
    {
        if (textures.Count == 0)
        {
            return null;
        }

        switch (slot)
        {
            case "diffuse":
                return textures.FirstOrDefault(t => t.Semantic == CanonicalTextureSemantic.BaseColor)
                       ?? textures.FirstOrDefault(t => SlotContains(t, "diffuse", "albedo", "basecolor", "base color"));
            case "normal":
                return textures.FirstOrDefault(t => t.Semantic == CanonicalTextureSemantic.Normal)
                       ?? textures.FirstOrDefault(t => SlotContains(t, "normal", "bump"));
            case "specular":
                return textures.FirstOrDefault(t => t.Semantic == CanonicalTextureSemantic.Specular)
                       ?? textures.FirstOrDefault(t => SlotContains(t, "specular", "spec", "gloss", "rough", "smooth"));
            case "shadow":
                // CanonicalTextureSemantic has no Shadow/AO member, so resolve by slot name only.
                return textures.FirstOrDefault(t => SlotContains(t, "shadow", "ao", "occlusion", "ambient"));
            default:
                return null;
        }
    }

    private static bool SlotContains(CanonicalTexture t, params string[] needles)
    {
        if (string.IsNullOrEmpty(t.Slot))
        {
            return false;
        }

        foreach (var needle in needles)
        {
            if (t.Slot.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void WriteManifest(string assetFolder, RigManifest manifest)
    {
        var path = Path.Combine(assetFolder, "rig.json");
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, JsonOptions));
        Console.WriteLine($"[rig] Wrote {path}");
    }
}
