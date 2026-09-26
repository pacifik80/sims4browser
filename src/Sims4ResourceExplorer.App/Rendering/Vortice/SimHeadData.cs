using System;
using System.Collections.Generic;
using System.Numerics;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.App.Rendering.Vortice;

/// <summary>
/// Pure-CPU helpers that turn a <see cref="CanonicalScene"/> into the float/index arrays the
/// Vortice PoC renderer uploads. No Vortice or WinUI types — fully unit-testable. Mirrors the
/// data conventions already used by the Helix path (<c>SceneViewportRenderer.CreateGeometry</c>
/// and the skin-atlas pick in <c>TryCreateGpuSkinCore</c>).
/// </summary>
internal static class SimHeadData
{
    internal readonly record struct HeadPick(CanonicalMesh Mesh, CanonicalMaterial Material, byte[] BaseColorPng);

    internal readonly record struct MeshBuffers(float[] Interleaved, uint[] Indices, Vector3 Center, float Radius);

    private static readonly string[] SkinAtlasNames = ["head_atlas.png", "skin_atlas.png"];

    /// <summary>
    /// Picks a skin mesh + its BaseColor atlas PNG: prefers the explicit head shell, else the
    /// first mesh carrying a skin-atlas BaseColor texture. Returns null if the scene has none.
    /// </summary>
    public static HeadPick? PickHeadMeshAndSkin(CanonicalScene scene)
    {
        HeadPick? firstSkin = null;
        foreach (var mesh in scene.Meshes)
        {
            if (mesh.MaterialIndex < 0 || mesh.MaterialIndex >= scene.Materials.Count)
            {
                continue;
            }
            var material = scene.Materials[mesh.MaterialIndex];
            var atlas = FindSkinAtlas(material);
            if (atlas?.PngBytes is not { Length: > 0 } png)
            {
                continue;
            }

            var pick = new HeadPick(mesh, material, png);
            firstSkin ??= pick;

            // Prefer the head shell when the binder tagged it (Approximation carries "Head shell").
            if (material.Approximation is { } a && a.Contains("Head shell", StringComparison.OrdinalIgnoreCase))
            {
                return pick;
            }
        }
        return firstSkin;
    }

    private static CanonicalTexture? FindSkinAtlas(CanonicalMaterial material)
    {
        foreach (var t in material.Textures)
        {
            if (t.Semantic != CanonicalTextureSemantic.BaseColor)
            {
                continue;
            }
            foreach (var name in SkinAtlasNames)
            {
                if (string.Equals(t.FileName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Builds an interleaved vertex array (pos3 + normal3 + uv2 = 8 floats/vertex) and a uint
    /// index buffer, plus the mesh's bounding sphere (centre + radius) for camera framing.
    /// </summary>
    public static MeshBuffers BuildInterleaved(CanonicalMesh mesh)
    {
        int vtxCount = mesh.Positions.Count / 3;
        var uvs = SelectUvs(mesh);
        bool hasNormals = mesh.Normals.Count == vtxCount * 3;
        bool hasUv = uvs is not null && uvs.Count == vtxCount * 2;

        const int stride = 8;
        var v = new float[vtxCount * stride];
        float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

        for (int i = 0; i < vtxCount; i++)
        {
            int o = i * stride;
            float px = mesh.Positions[i * 3 + 0];
            float py = mesh.Positions[i * 3 + 1];
            float pz = mesh.Positions[i * 3 + 2];
            v[o + 0] = px;
            v[o + 1] = py;
            v[o + 2] = pz;
            v[o + 3] = hasNormals ? mesh.Normals[i * 3 + 0] : 0f;
            v[o + 4] = hasNormals ? mesh.Normals[i * 3 + 1] : 0f;
            v[o + 5] = hasNormals ? mesh.Normals[i * 3 + 2] : 1f;
            v[o + 6] = hasUv ? uvs![i * 2 + 0] : 0f;
            v[o + 7] = hasUv ? uvs![i * 2 + 1] : 0f;

            if (px < minX) minX = px; if (px > maxX) maxX = px;
            if (py < minY) minY = py; if (py > maxY) maxY = py;
            if (pz < minZ) minZ = pz; if (pz > maxZ) maxZ = pz;
        }

        var indices = new uint[mesh.Indices.Count];
        for (int i = 0; i < indices.Length; i++)
        {
            indices[i] = (uint)mesh.Indices[i];
        }

        var center = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);
        float radius = 0.5f * new Vector3(maxX - minX, maxY - minY, maxZ - minZ).Length();
        if (radius <= 0f || float.IsNaN(radius))
        {
            radius = 1f;
        }
        return new MeshBuffers(v, indices, center, radius);
    }

    private static IReadOnlyList<float>? SelectUvs(CanonicalMesh mesh)
    {
        if (mesh.PreferredUvChannel == 1 && mesh.Uv1s is { Count: > 0 })
        {
            return mesh.Uv1s;
        }
        if (mesh.Uv0s is { Count: > 0 })
        {
            return mesh.Uv0s;
        }
        return mesh.Uvs;
    }
}
