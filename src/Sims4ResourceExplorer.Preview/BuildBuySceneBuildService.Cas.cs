using System.Numerics;
using System.Text;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.Preview;

public sealed partial class BuildBuySceneBuildService
{
    private async Task<SceneBuildResult> BuildGeometrySceneAsync(
        ResourceMetadata geometryResource,
        ResourceMetadata logicalRootResource,
        CancellationToken cancellationToken)
    {
        var bytes = await resourceCatalogService.GetResourceBytesAsync(geometryResource.PackagePath, geometryResource.Key, raw: false, cancellationToken).ConfigureAwait(false);
        var diagnostics = new List<string>();

        Ts4GeomResource geom;
        try
        {
            geom = Ts4GeomResource.Parse(bytes);
        }
        catch (Exception ex)
        {
            return new SceneBuildResult(false, null, [$"Geometry parsing failed for {geometryResource.Key.FullTgi}: {ex.Message}"], SceneBuildStatus.Unsupported);
        }

        if (geom.Vertices.Count == 0 || geom.Indices.Count == 0)
        {
            return new SceneBuildResult(false, null, [$"Geometry {geometryResource.Key.FullTgi} did not expose any triangle data."], SceneBuildStatus.Unsupported);
        }

        if (!geom.HasSkinning)
        {
            diagnostics.Add("Geometry did not expose explicit blend indices/weights. Preview will render the bind-pose mesh without skinned export data.");
        }

        var textureCache = new Dictionary<string, CanonicalTexture?>(StringComparer.OrdinalIgnoreCase);
        var textures = await ResolveFallbackTexturesAsync(geometryResource, textureCache, cancellationToken).ConfigureAwait(false);
        if (textures.Count == 0)
        {
            diagnostics.Add("No exact-instance texture candidates were decoded for this Geometry root.");
        }

        var rig = await TryResolveRigAsync(geometryResource, cancellationToken).ConfigureAwait(false);
        diagnostics.AddRange(rig.Diagnostics);
        var bones = BuildCanonicalBones(geom, rig.Rig);
        var skinWeights = BuildSkinWeights(geom, diagnostics);

        var mesh = new CanonicalMesh(
            $"Mesh_{geometryResource.Key.FullInstance:X16}",
            geom.Vertices.SelectMany(static vertex => vertex.Position).ToArray(),
            geom.Vertices.SelectMany(static vertex => vertex.Normal ?? []).ToArray(),
            geom.Vertices.SelectMany(static vertex => vertex.Tangent ?? []).ToArray(),
            geom.Vertices.SelectMany(static vertex => vertex.Uv0 ?? []).ToArray(),
            geom.Indices,
            0,
            skinWeights);

        var scene = new CanonicalScene(
            logicalRootResource.Name ?? geometryResource.Name ?? $"Cas_{logicalRootResource.Key.FullInstance:X16}",
            [mesh],
            [new CanonicalMaterial(
                "ApproximateCasMaterial",
                textures,
                null,
                false,
                "portable-approximation",
                "CAS materials are exported as a portable approximation of package-local texture candidates.",
                CanonicalMaterialSourceKind.ApproximateCas)],
            bones,
            ComputeBounds([mesh]));

        diagnostics.Insert(0, $"Selected geometry root: {geometryResource.Key.FullTgi}");
        var status = textures.Count == 0 || rig.Rig is null
            ? SceneBuildStatus.Partial
            : SceneBuildStatus.SceneReady;
        return new SceneBuildResult(true, scene, diagnostics, status);
    }

    private async Task<Ts4RigResolution> TryResolveRigAsync(ResourceMetadata geometryResource, CancellationToken cancellationToken)
    {
        var packageResources = await indexStore.GetPackageResourcesAsync(geometryResource.PackagePath, cancellationToken).ConfigureAwait(false);
        var rig = packageResources
            .Where(resource => resource.Key.TypeName == "Rig" && resource.Key.FullInstance == geometryResource.Key.FullInstance)
            .OrderBy(static resource => resource.Key.Group)
            .FirstOrDefault();

        if (rig is null)
        {
            return new Ts4RigResolution(null, ["No exact-instance Rig resource was resolved for this geometry. Bone names will fall back to GEOM hashes."]);
        }

        try
        {
            var bytes = await resourceCatalogService.GetResourceBytesAsync(rig.PackagePath, rig.Key, raw: false, cancellationToken).ConfigureAwait(false);
            return new Ts4RigResolution(Ts4RigResource.Parse(bytes), [$"Resolved rig: {rig.Key.FullTgi}"]);
        }
        catch (Exception ex)
        {
            return new Ts4RigResolution(null, [$"Rig parsing failed for {rig.Key.FullTgi}: {ex.Message}"]);
        }
    }

    private static IReadOnlyList<CanonicalBone> BuildCanonicalBones(Ts4GeomResource geom, Ts4RigResource? rig)
    {
        if (geom.BoneHashes.Count == 0)
        {
            return [];
        }

        var rigBonesByHash = rig?.Bones.ToDictionary(static bone => bone.NameHash) ?? new Dictionary<uint, Ts4RigBone>();
        var worldMatrices = new Dictionary<uint, Matrix4x4>();
        var results = new List<CanonicalBone>(geom.BoneHashes.Count);

        foreach (var hash in geom.BoneHashes)
        {
            if (rigBonesByHash.TryGetValue(hash, out var rigBone))
            {
                var bindPose = ComputeWorldMatrix(rigBone, rigBonesByHash, worldMatrices);
                Matrix4x4.Invert(bindPose, out var inverseBindPose);
                var parentName = rigBone.ParentHash is uint parentHash && rigBonesByHash.TryGetValue(parentHash, out var parentBone)
                    ? parentBone.Name
                    : null;

                results.Add(new CanonicalBone(
                    rigBone.Name,
                    parentName,
                    ToArray(bindPose),
                    ToArray(inverseBindPose),
                    hash));
            }
            else
            {
                results.Add(new CanonicalBone($"Bone_{hash:X8}", null, null, null, hash));
            }
        }

        return results;
    }

    private static Matrix4x4 ComputeWorldMatrix(
        Ts4RigBone bone,
        IReadOnlyDictionary<uint, Ts4RigBone> bonesByHash,
        Dictionary<uint, Matrix4x4> cache)
    {
        if (cache.TryGetValue(bone.NameHash, out var cached))
        {
            return cached;
        }

        var local = Matrix4x4.CreateScale(bone.Scale)
            * Matrix4x4.CreateFromQuaternion(bone.Rotation)
            * Matrix4x4.CreateTranslation(bone.Position);

        var world = bone.ParentHash is uint parentHash && bonesByHash.TryGetValue(parentHash, out var parent)
            ? local * ComputeWorldMatrix(parent, bonesByHash, cache)
            : local;

        cache[bone.NameHash] = world;
        return world;
    }

    private static IReadOnlyList<VertexWeight> BuildSkinWeights(Ts4GeomResource geom, List<string> diagnostics)
    {
        var results = new List<VertexWeight>();
        if (!geom.HasSkinning || geom.BoneHashes.Count == 0)
        {
            return results;
        }

        for (var vertexIndex = 0; vertexIndex < geom.Vertices.Count; vertexIndex++)
        {
            var vertex = geom.Vertices[vertexIndex];
            if (vertex.BlendIndices is null || vertex.BlendWeights is null)
            {
                continue;
            }

            var total = vertex.BlendWeights.Where(static weight => weight > 0f).Sum();
            if (total <= 0f && vertex.BlendIndices.Any(static index => index > 0))
            {
                total = 1f;
                vertex = vertex with { BlendWeights = [1f, 0f, 0f, 0f] };
            }

            for (var influence = 0; influence < Math.Min(vertex.BlendIndices.Length, vertex.BlendWeights.Length); influence++)
            {
                var weight = vertex.BlendWeights[influence];
                if (weight <= 0f)
                {
                    continue;
                }

                var boneIndex = vertex.BlendIndices[influence];
                if (boneIndex < 0 || boneIndex >= geom.BoneHashes.Count)
                {
                    diagnostics.Add($"Skipped out-of-range bone influence {boneIndex} on vertex {vertexIndex}.");
                    continue;
                }

                results.Add(new VertexWeight(vertexIndex, boneIndex, total <= 0f ? weight : weight / total));
            }
        }

        return results;
    }

    private static float[] ToArray(Matrix4x4 matrix) =>
    [
        matrix.M11, matrix.M12, matrix.M13, matrix.M14,
        matrix.M21, matrix.M22, matrix.M23, matrix.M24,
        matrix.M31, matrix.M32, matrix.M33, matrix.M34,
        matrix.M41, matrix.M42, matrix.M43, matrix.M44
    ];
}

internal sealed class Ts4GeomResource
{
    public required uint Version { get; init; }
    public required IReadOnlyList<Ts4GeomVertex> Vertices { get; init; }
    public required IReadOnlyList<int> Indices { get; init; }
    public required IReadOnlyList<uint> BoneHashes { get; init; }
    public bool HasSkinning => Vertices.Any(static vertex => vertex.BlendIndices is not null && vertex.BlendWeights is not null);

    public static Ts4GeomResource Parse(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        ExpectTag(reader, "GEOM");
        var version = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        var shader = reader.ReadUInt32();
        if (shader != 0)
        {
            var shaderSize = reader.ReadUInt32();
            stream.Position += shaderSize;
        }

        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        var vertexCount = reader.ReadInt32();
        var formats = ReadFormats(reader);
        var vertices = ReadVertices(reader, version, vertexCount, formats);

        _ = reader.ReadInt32();
        _ = reader.ReadByte();
        var indices = ReadIndices(reader);

        if (version == 0x00000005)
        {
            _ = reader.ReadInt32();
        }
        else
        {
            SkipUnknownThingList(reader);
            SkipUnknownThing2List(reader);
        }

        var boneHashCount = reader.ReadInt32();
        var boneHashes = new List<uint>(boneHashCount);
        for (var index = 0; index < boneHashCount; index++)
        {
            boneHashes.Add(reader.ReadUInt32());
        }

        return new Ts4GeomResource
        {
            Version = version,
            Vertices = vertices,
            Indices = indices,
            BoneHashes = boneHashes
        };
    }

    private static IReadOnlyList<Ts4GeomFormat> ReadFormats(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        var results = new List<Ts4GeomFormat>(count);
        for (var index = 0; index < count; index++)
        {
            var usage = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            _ = reader.ReadByte();
            results.Add(new Ts4GeomFormat(usage));
        }

        return results;
    }

    private static IReadOnlyList<Ts4GeomVertex> ReadVertices(BinaryReader reader, uint version, int vertexCount, IReadOnlyList<Ts4GeomFormat> formats)
    {
        var results = new List<Ts4GeomVertex>(vertexCount);
        for (var index = 0; index < vertexCount; index++)
        {
            float[]? position = null;
            float[]? normal = null;
            float[]? uv0 = null;
            byte[]? blendIndices = null;
            float[]? blendWeights = null;
            float[]? tangent = null;

            foreach (var format in formats)
            {
                switch (format.Usage)
                {
                    case 0x01:
                        position = [reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()];
                        break;
                    case 0x02:
                        normal = [reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()];
                        break;
                    case 0x03:
                        uv0 = [reader.ReadSingle(), reader.ReadSingle()];
                        break;
                    case 0x04:
                        blendIndices = reader.ReadBytes(4);
                        break;
                    case 0x05 when version == 0x00000005:
                        blendWeights = [reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()];
                        break;
                    case 0x05:
                        blendWeights = reader.ReadBytes(4).Select(static value => value / 255f).ToArray();
                        break;
                    case 0x06:
                        tangent = [reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()];
                        break;
                    case 0x07:
                    case 0x0A:
                        streamSkip(reader, 4);
                        break;
                    default:
                        throw new InvalidDataException($"Unsupported GEOM usage 0x{format.Usage:X8}.");
                }
            }

            if (position is not null)
            {
                results.Add(new Ts4GeomVertex(position, normal, tangent, uv0, blendIndices, blendWeights));
            }
        }

        return results;
    }

    private static IReadOnlyList<int> ReadIndices(BinaryReader reader)
    {
        var indexCount = reader.ReadInt32();
        var results = new List<int>(indexCount);
        for (var index = 0; index < indexCount; index++)
        {
            results.Add(reader.ReadUInt16());
        }

        return results;
    }

    private static void SkipUnknownThingList(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        for (var index = 0; index < count; index++)
        {
            _ = reader.ReadUInt32();
            var vectorCount = reader.ReadInt32();
            streamSkip(reader, vectorCount * 8);
        }
    }

    private static void SkipUnknownThing2List(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        streamSkip(reader, count * 53);
    }

    private static void streamSkip(BinaryReader reader, int byteCount) =>
        reader.BaseStream.Position += byteCount;

    private static void ExpectTag(BinaryReader reader, string expected)
    {
        var tag = Encoding.ASCII.GetString(reader.ReadBytes(expected.Length));
        if (!string.Equals(tag, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Expected tag '{expected}', found '{tag}'.");
        }
    }
}

internal readonly record struct Ts4GeomFormat(uint Usage);

internal readonly record struct Ts4GeomVertex(
    float[] Position,
    float[]? Normal,
    float[]? Tangent,
    float[]? Uv0,
    byte[]? BlendIndices,
    float[]? BlendWeights);

internal sealed class Ts4RigResource
{
    public required IReadOnlyList<Ts4RigBone> Bones { get; init; }

    public static Ts4RigResource Parse(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var major = reader.ReadUInt32();
        var minor = reader.ReadUInt32();
        if ((major is not (3u or 4u)) || (minor is not (1u or 2u)))
        {
            throw new InvalidDataException("Only clear-format TS4 rig resources are supported in this pass.");
        }

        var boneCount = reader.ReadInt32();
        var results = new List<Ts4RigBone>(boneCount);
        for (var index = 0; index < boneCount; index++)
        {
            var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            var rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            var scale = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            var nameLength = reader.ReadInt32();
            var name = new string(reader.ReadChars(nameLength));
            _ = reader.ReadInt32();
            var parentIndex = reader.ReadInt32();
            var nameHash = reader.ReadUInt32();
            _ = reader.ReadUInt32();

            results.Add(new Ts4RigBone(
                name,
                nameHash,
                parentIndex,
                position,
                rotation,
                scale));
        }

        for (var index = 0; index < results.Count; index++)
        {
            var parentIndex = results[index].ParentIndex;
            results[index] = results[index] with
            {
                ParentHash = parentIndex >= 0 && parentIndex < results.Count ? results[parentIndex].NameHash : null
            };
        }

        return new Ts4RigResource { Bones = results };
    }
}

internal readonly record struct Ts4RigBone(
    string Name,
    uint NameHash,
    int ParentIndex,
    Vector3 Position,
    Quaternion Rotation,
    Vector3 Scale,
    uint? ParentHash = null);

internal readonly record struct Ts4RigResolution(Ts4RigResource? Rig, IReadOnlyList<string> Diagnostics);
