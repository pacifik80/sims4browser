using System.Numerics;
using System.Text;
using System.Diagnostics;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.Preview;

public sealed partial class BuildBuySceneBuildService
{
    public async Task<SceneBuildResult> BuildSceneAsync(CasAssetGraph casGraph, CancellationToken cancellationToken, IProgress<PreviewBuildProgress>? progress = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();

        var geometryRoots = casGraph.GeometryResources
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
        if (geometryRoots.Length == 0)
        {
            return new SceneBuildResult(
                false,
                null,
                ["No CAS geometry roots were resolved."],
                SceneBuildStatus.Unsupported);
        }

        if (geometryRoots.Length == 1)
        {
            var singleResult = await BuildGeometrySceneAsync(
                geometryRoots[0],
                casGraph.CasPartResource,
                cancellationToken,
                progress,
                casGraph.RigResources,
                casGraph.MaterialResources,
                casGraph.TextureResources,
                casGraph.Materials).ConfigureAwait(false);
            return singleResult with
            {
                Diagnostics = [.. singleResult.Diagnostics, $"CAS scene build timings: total {stopwatch.Elapsed.TotalMilliseconds:0} ms."]
            };
        }

        var previews = new List<ScenePreviewContent>(geometryRoots.Length);
        var diagnostics = new List<string>();
        var allReady = true;
        for (var index = 0; index < geometryRoots.Length; index++)
        {
            var geometry = geometryRoots[index];
            var start = 0.02 + ((index / (double)Math.Max(geometryRoots.Length, 1)) * 0.94);
            progress?.Report(new PreviewBuildProgress($"Building CAS geometry {index + 1}/{geometryRoots.Length}...", start));
            var scene = await BuildGeometrySceneAsync(
                geometry,
                casGraph.CasPartResource,
                cancellationToken,
                progress,
                casGraph.RigResources,
                casGraph.MaterialResources,
                casGraph.TextureResources,
                casGraph.Materials).ConfigureAwait(false);
            diagnostics.AddRange(scene.Diagnostics);
            allReady &= scene.Status == SceneBuildStatus.SceneReady;
            if (scene.Scene is not null)
            {
                previews.Add(new ScenePreviewContent(geometry, scene.Scene, string.Join(Environment.NewLine, scene.Diagnostics), scene.Status));
            }
        }

        if (previews.Count == 0)
        {
            return new SceneBuildResult(false, null, diagnostics, SceneBuildStatus.Unsupported);
        }

        var name = casGraph.CasPartResource.Name
            ?? casGraph.CasPartResource.Key.FullTgi;
        var composed = CanonicalSceneComposer.Compose(name, previews);
        return new SceneBuildResult(
            true,
            composed.Scene,
            [.. diagnostics, $"CAS scene build timings: total {stopwatch.Elapsed.TotalMilliseconds:0} ms."],
            allReady && previews.Count == geometryRoots.Length ? SceneBuildStatus.SceneReady : SceneBuildStatus.Partial);
    }

    private async Task<SceneBuildResult> BuildGeometrySceneAsync(
        ResourceMetadata geometryResource,
        ResourceMetadata logicalRootResource,
        CancellationToken cancellationToken,
        IProgress<PreviewBuildProgress>? progress,
        IReadOnlyList<ResourceMetadata>? preferredRigResources = null,
        IReadOnlyList<ResourceMetadata>? preferredMaterialResources = null,
        IReadOnlyList<ResourceMetadata>? preferredTextureResources = null,
        IReadOnlyList<MaterialManifestEntry>? preferredMaterialManifest = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var geometryReadElapsed = TimeSpan.Zero;
        var geometryParseElapsed = TimeSpan.Zero;
        var textureResolveElapsed = TimeSpan.Zero;
        var rigResolveElapsed = TimeSpan.Zero;
        var geometryPackageName = Path.GetFileName(geometryResource.PackagePath);
        ReportProgress(progress, $"Loading geometry bytes from {geometryPackageName}...", 0.15);
        var readStopwatch = Stopwatch.StartNew();
        var bytes = await resourceCatalogService.GetResourceBytesAsync(
            geometryResource.PackagePath,
            geometryResource.Key,
            raw: false,
            cancellationToken,
            CreateReadProgressReporter(progress, 0.15, 0.35, geometryPackageName)).ConfigureAwait(false);
        geometryReadElapsed = readStopwatch.Elapsed;
        var diagnostics = new List<string>();

        Ts4GeomResource geom;
        try
        {
            ReportProgress(progress, "Parsing geometry...", 0.35);
            var parseStopwatch = Stopwatch.StartNew();
            geom = Ts4GeomResource.Parse(bytes);
            geometryParseElapsed = parseStopwatch.Elapsed;
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
        ReportProgress(progress, "Resolving textures...", 0.55);
        var textureStopwatch = Stopwatch.StartNew();
        var materials = await ResolveCasMaterialsFromMaterialResourcesAsync(
            geometryResource,
            preferredMaterialResources,
            textureCache,
            cancellationToken,
            diagnostics,
            progress).ConfigureAwait(false);
        if (materials.Count == 0)
        {
            materials = await ResolveCasMaterialsFromManifestAsync(
                preferredMaterialManifest,
                preferredTextureResources,
                textureCache,
                cancellationToken,
                diagnostics,
                progress).ConfigureAwait(false);
        }
        if (materials.Count == 0)
        {
            var fallbackTextures = await ResolveFallbackTexturesAsync(
                geometryResource,
                textureCache,
                cancellationToken,
                diagnostics,
                preferredTextureResources).ConfigureAwait(false);
            if (fallbackTextures.Count == 0)
            {
                diagnostics.Add("No manifest-driven or exact-instance texture candidates were decoded for this Geometry root.");
            }
            else
            {
                diagnostics.Add("CAS preview fell back to exact-instance texture candidates because no manifest-driven material textures were resolved.");
            }

            materials =
            [
                new CanonicalMaterial(
                    "ApproximateCasMaterial",
                    fallbackTextures,
                    null,
                    false,
                    "portable-approximation",
                    null,
                    [],
                    "CAS materials are exported as a portable approximation of package-local texture candidates.",
                    CanonicalMaterialSourceKind.ApproximateCas)
            ];
        }
        var textures = materials.SelectMany(static material => material.Textures).ToArray();
        textureResolveElapsed = textureStopwatch.Elapsed;

        ReportProgress(progress, "Resolving rig...", 0.72);
        var rigStopwatch = Stopwatch.StartNew();
        var rig = await TryResolveRigAsync(geometryResource, geom, preferredRigResources, cancellationToken).ConfigureAwait(false);
        rigResolveElapsed = rigStopwatch.Elapsed;
        diagnostics.AddRange(rig.Diagnostics);
        var bones = BuildCanonicalBones(geom, rig.Rig);
        var skinWeights = BuildSkinWeights(geom, diagnostics);

        var uv0s = geom.Vertices.SelectMany(static vertex => vertex.Uv0 ?? []).ToArray();
        var uv1s = geom.Vertices.SelectMany(static vertex => vertex.Uv1 ?? []).ToArray();
        var primaryUvs = uv0s.Length > 0
            ? uv0s
            : uv1s;

        var mesh = new CanonicalMesh(
            $"Mesh_{geometryResource.Key.FullInstance:X16}",
            geom.Vertices.SelectMany(static vertex => vertex.Position).ToArray(),
            geom.Vertices.SelectMany(static vertex => vertex.Normal ?? []).ToArray(),
            geom.Vertices.SelectMany(static vertex => vertex.Tangent ?? []).ToArray(),
            primaryUvs,
            uv0s.Length > 0 ? uv0s : null,
            uv1s.Length > 0 ? uv1s : null,
            0,
            geom.Indices,
            0,
            skinWeights);

        var scene = new CanonicalScene(
            logicalRootResource.Name ?? geometryResource.Name ?? $"Cas_{logicalRootResource.Key.FullInstance:X16}",
            [mesh],
            materials,
            bones,
            ComputeBounds([mesh]));

        diagnostics.Insert(0, $"Selected geometry root: {geometryResource.Key.FullTgi}");
        diagnostics.Add(
            $"Geometry scene timings: read {geometryReadElapsed.TotalMilliseconds:0} ms | parse {geometryParseElapsed.TotalMilliseconds:0} ms | textures {textureResolveElapsed.TotalMilliseconds:0} ms | rig {rigResolveElapsed.TotalMilliseconds:0} ms | total {stopwatch.Elapsed.TotalMilliseconds:0} ms.");
        ReportProgress(progress, "Scene ready.", 1.0);
        var status = textures.Length == 0 || rig.Rig is null
            ? SceneBuildStatus.Partial
            : SceneBuildStatus.SceneReady;
        return new SceneBuildResult(true, scene, diagnostics, status);
    }

    private async Task<IReadOnlyList<CanonicalMaterial>> ResolveCasMaterialsFromMaterialResourcesAsync(
        ResourceMetadata geometryResource,
        IReadOnlyList<ResourceMetadata>? preferredMaterialResources,
        Dictionary<string, CanonicalTexture?> textureCache,
        CancellationToken cancellationToken,
        List<string> diagnostics,
        IProgress<PreviewBuildProgress>? progress = null)
    {
        if (preferredMaterialResources is null || preferredMaterialResources.Count == 0)
        {
            return [];
        }

        var candidateMaterials = preferredMaterialResources
            .Where(resource => resource.Key.FullInstance == geometryResource.Key.FullInstance)
            .OrderBy(static resource => resource.PackagePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static resource => resource.Key.Group)
            .ToArray();
        if (candidateMaterials.Length == 0)
        {
            candidateMaterials = preferredMaterialResources
                .OrderBy(static resource => resource.PackagePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static resource => resource.Key.Group)
                .ToArray();
        }

        var resolvedMaterials = new List<CanonicalMaterial>(candidateMaterials.Length);
        for (var index = 0; index < candidateMaterials.Length; index++)
        {
            var materialResource = candidateMaterials[index];
            try
            {
                var bytes = await resourceCatalogService.GetResourceBytesAsync(
                    materialResource.PackagePath,
                    materialResource.Key,
                    raw: false,
                    cancellationToken).ConfigureAwait(false);
                var rcol = Ts4RcolResource.Parse(bytes);
                var materialChunk = rcol.Chunks.FirstOrDefault(static chunk => chunk.Tag is "MATD" or "MTST");
                var materialInfo = await ResolveMaterialAsync(
                    materialChunk,
                    rcol,
                    materialResource,
                    geometryResource,
                    textureCache,
                    cancellationToken,
                    progress,
                    label: $"CAS material {index + 1}/{candidateMaterials.Length}").ConfigureAwait(false);
                diagnostics.AddRange(materialInfo.Diagnostics);
                if (materialInfo.Textures.Count == 0)
                {
                    continue;
                }

                resolvedMaterials.Add(new CanonicalMaterial(
                    materialInfo.Name,
                    materialInfo.Textures,
                    materialInfo.ShaderName,
                    materialInfo.IsTransparent,
                    materialInfo.AlphaMode,
                    materialInfo.AlphaTextureSlot,
                    materialInfo.LayeredTextureSlots,
                    materialInfo.Approximation,
                    materialInfo.SourceKind,
                    materialInfo.ApproximateBaseColor is { Length: >= 3 } color
                        ? new CanonicalColor(color[0], color[1], color[2], color.Length >= 4 ? color[3] : 1f)
                        : null,
                    ShaderFamily: materialInfo.ShaderFamily,
                    DecodeStrategy: materialInfo.DecodeStrategy,
                    Sampling: materialInfo.SamplingInstructions,
                    VisualPayloadKind: materialInfo.VisualPayloadKind));
            }
            catch (Exception ex)
            {
                diagnostics.Add($"CAS material resource {materialResource.Key.FullTgi} could not be decoded: {ex.Message}");
            }
        }

        if (resolvedMaterials.Count > 0)
        {
            diagnostics.Add($"Resolved {resolvedMaterials.Count:N0} CAS material-definition candidate(s) before considering manifest approximation.");
        }

        return resolvedMaterials;
    }

    private async Task<IReadOnlyList<CanonicalMaterial>> ResolveCasMaterialsFromManifestAsync(
        IReadOnlyList<MaterialManifestEntry>? preferredMaterialManifest,
        IReadOnlyList<ResourceMetadata>? preferredTextureResources,
        Dictionary<string, CanonicalTexture?> textureCache,
        CancellationToken cancellationToken,
        List<string> diagnostics,
        IProgress<PreviewBuildProgress>? progress = null)
    {
        if (preferredMaterialManifest is null || preferredMaterialManifest.Count == 0)
        {
            return [];
        }

        var resolvedMaterials = new List<CanonicalMaterial>(preferredMaterialManifest.Count);
        foreach (var manifestMaterial in preferredMaterialManifest)
        {
            var resolvedTextures = new List<CanonicalTexture>(manifestMaterial.Textures.Count);
            foreach (var manifestTexture in manifestMaterial.Textures)
            {
                var resolvedTexture = await TryResolveManifestTextureAsync(
                    manifestTexture,
                    preferredTextureResources,
                    textureCache,
                    cancellationToken,
                    progress).ConfigureAwait(false);
                if (resolvedTexture is null)
                {
                    diagnostics.Add($"CAS material manifest texture slot '{manifestTexture.Slot}' did not resolve to a previewable texture.");
                    continue;
                }

                resolvedTextures.Add(resolvedTexture);
            }

            if (resolvedTextures.Count == 0)
            {
                continue;
            }

            resolvedMaterials.Add(new CanonicalMaterial(
                manifestMaterial.MaterialName,
                resolvedTextures,
                manifestMaterial.ShaderName,
                manifestMaterial.IsTransparent,
                manifestMaterial.AlphaMode,
                manifestMaterial.AlphaTextureSlot,
                manifestMaterial.LayeredTextureSlots,
                manifestMaterial.Approximation,
                manifestMaterial.SourceKind));
        }

        if (resolvedMaterials.Count > 0)
        {
            diagnostics.Add($"Resolved {resolvedMaterials.Count:N0} manifest-driven CAS material(s) before considering same-instance fallback textures.");
        }

        return resolvedMaterials;
    }

    private async Task<Ts4RigResolution> TryResolveRigAsync(
        ResourceMetadata geometryResource,
        Ts4GeomResource geom,
        IReadOnlyList<ResourceMetadata>? preferredRigResources,
        CancellationToken cancellationToken)
    {
        static string BuildRigCacheKey(ResourceMetadata resource) =>
            $"{resource.PackagePath}|{resource.Key.FullTgi}";

        var diagnostics = new List<string>();
        var packageResources = await GetPackageInstanceResourcesAsync(geometryResource.PackagePath, geometryResource.Key.FullInstance, cancellationToken).ConfigureAwait(false);
        var candidateResources = new List<(ResourceMetadata Resource, bool IsPreferred, bool IsExactInstance)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void AddCandidates(IEnumerable<ResourceMetadata>? resources, bool isPreferred)
        {
            if (resources is null)
            {
                return;
            }

            foreach (var resource in resources.Where(static resource => resource.Key.TypeName == "Rig"))
            {
                var cacheKey = BuildRigCacheKey(resource);
                if (!seen.Add(cacheKey))
                {
                    continue;
                }

                candidateResources.Add((resource, isPreferred, string.Equals(resource.PackagePath, geometryResource.PackagePath, StringComparison.OrdinalIgnoreCase) &&
                                                            resource.Key.FullInstance == geometryResource.Key.FullInstance));
            }
        }

        AddCandidates(preferredRigResources, isPreferred: true);
        AddCandidates(
            packageResources
                .Where(resource => resource.Key.TypeName == "Rig" && resource.Key.FullInstance == geometryResource.Key.FullInstance)
                .OrderBy(static resource => resource.Key.Group),
            isPreferred: false);

        if (candidateResources.Count == 0)
        {
            return new Ts4RigResolution(null, ["No exact-instance Rig resource was resolved for this geometry. Bone names will fall back to GEOM hashes."]);
        }

        var geomBoneHashes = geom.BoneHashes.Count == 0
            ? null
            : geom.BoneHashes.ToHashSet();
        Ts4RigResource? bestRig = null;
        ResourceMetadata? bestRigResource = null;
        var bestOverlap = -1;
        var bestIsExactInstance = false;
        var bestIsPreferred = false;

        foreach (var (resource, isPreferred, isExactInstance) in candidateResources)
        {
            try
            {
                var rigPackageName = Path.GetFileName(resource.PackagePath);
                var bytes = await resourceCatalogService.GetResourceBytesAsync(
                    resource.PackagePath,
                    resource.Key,
                    raw: false,
                    cancellationToken,
                    CreateReadProgressReporter(null, 0d, 1d, rigPackageName)).ConfigureAwait(false);
                var parsedRig = Ts4RigResource.Parse(bytes);
                var overlap = geomBoneHashes is null
                    ? 0
                    : parsedRig.Bones.Count(bone => geomBoneHashes.Contains(bone.NameHash));
                if (bestRig is null ||
                    overlap > bestOverlap ||
                    (overlap == bestOverlap && isExactInstance && !bestIsExactInstance))
                {
                    bestRig = parsedRig;
                    bestRigResource = resource;
                    bestOverlap = overlap;
                    bestIsExactInstance = isExactInstance;
                    bestIsPreferred = isPreferred;
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add($"Rig parsing failed for {resource.Key.FullTgi}: {ex.Message}");
            }
        }

        if (bestRig is null || bestRigResource is null)
        {
            diagnostics.Add("No usable Rig resource was resolved for this geometry. Bone names will fall back to GEOM hashes.");
            return new Ts4RigResolution(null, diagnostics);
        }

        var sharedBoneSummary = bestOverlap > 0
            ? $" ({bestOverlap:N0} shared GEOM bone hash(es))"
            : string.Empty;
        diagnostics.Insert(0, bestIsPreferred && !bestIsExactInstance
            ? $"Resolved rig from CAS-linked companion resource: {bestRigResource.Key.FullTgi}{sharedBoneSummary}"
            : $"Resolved rig: {bestRigResource.Key.FullTgi}{sharedBoneSummary}");
        return new Ts4RigResolution(bestRig, diagnostics);
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
        var payload = ResolveGeomPayload(bytes);
        using var stream = new MemoryStream(payload, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        ExpectTag(reader, "GEOM");
        var version = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        var shader = reader.ReadUInt32();
        if (shader != 0)
        {
            var shaderSize = reader.ReadUInt32();
            SkipBytes(reader, shaderSize, "shader payload");
        }

        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        var vertexCount = ReadNonNegativeInt32(reader, "vertex count");
        var formats = ReadFormats(reader);
        var vertices = ReadVertices(reader, version, vertexCount, formats);

        var indices = ReadSubMeshIndices(reader);

        if (version == 0x00000005)
        {
            _ = reader.ReadInt32();
        }
        else if (version >= 0x0000000C)
        {
            SkipUvStitchData(reader);
            if (version >= 0x0000000D)
            {
                SkipSeamStitchData(reader);
            }

            SkipSlotIntersectionData(reader, version);
        }

        var boneHashCount = ReadNonNegativeInt32(reader, "bone hash count");
        EnsureBytesAvailable(reader, checked((long)boneHashCount * sizeof(uint)), "bone hash table");
        var boneHashes = new List<uint>(boneHashCount);
        for (var index = 0; index < boneHashCount; index++)
        {
            boneHashes.Add(reader.ReadUInt32());
        }

        if (version >= 0x0000000F)
        {
            SkipGeometryStates(reader);
        }

        if (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            SkipResourceKeyTable(reader);
        }

        return new Ts4GeomResource
        {
            Version = version,
            Vertices = vertices,
            Indices = indices,
            BoneHashes = boneHashes
        };
    }

    private static byte[] ResolveGeomPayload(byte[] bytes)
    {
        if (bytes.Length >= 4 &&
            bytes[0] == (byte)'G' &&
            bytes[1] == (byte)'E' &&
            bytes[2] == (byte)'O' &&
            bytes[3] == (byte)'M')
        {
            return bytes;
        }

        try
        {
            var rcol = Ts4RcolResource.Parse(bytes);
            var geomChunk = rcol.Chunks.FirstOrDefault(static chunk => chunk.Tag == "GEOM");
            if (geomChunk is not null)
            {
                return geomChunk.Data.ToArray();
            }
        }
        catch
        {
        }

        return bytes;
    }

    private static IReadOnlyList<Ts4GeomFormat> ReadFormats(BinaryReader reader)
    {
        var count = ReadNonNegativeInt32(reader, "format count");
        EnsureBytesAvailable(reader, checked((long)count * 9), "format table");
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
        var vertexStride = GetVertexStride(version, formats);
        EnsureBytesAvailable(reader, checked((long)vertexCount * vertexStride), "vertex buffer");
        var results = new List<Ts4GeomVertex>(vertexCount);
        for (var index = 0; index < vertexCount; index++)
        {
            float[]? position = null;
            float[]? normal = null;
            float[]? uv0 = null;
            float[]? uv1 = null;
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
                        var uv = new[] { reader.ReadSingle(), reader.ReadSingle() };
                        if (uv0 is null)
                        {
                            uv0 = uv;
                        }
                        else if (uv1 is null)
                        {
                            uv1 = uv;
                        }
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
                        SkipBytes(reader, 4, $"vertex {index} usage 0x{format.Usage:X8}");
                        break;
                    default:
                        throw new InvalidDataException($"Unsupported GEOM usage 0x{format.Usage:X8}.");
                }
            }

            if (position is not null)
            {
                results.Add(new Ts4GeomVertex(position, normal, tangent, uv0, uv1, blendIndices, blendWeights));
            }
        }

        return results;
    }

    private static IReadOnlyList<int> ReadSubMeshIndices(BinaryReader reader)
    {
        var subMeshCount = ReadNonNegativeInt32(reader, "submesh count");
        var results = new List<int>();
        for (var subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
        {
            EnsureBytesAvailable(reader, sizeof(byte) + sizeof(int), $"submesh header #{subMeshIndex}");
            var indexSize = reader.ReadByte();
            var indexCount = ReadNonNegativeInt32(reader, $"submesh index count #{subMeshIndex}");
            var indexByteCount = indexSize switch
            {
                2 => checked((long)indexCount * sizeof(ushort)),
                4 => checked((long)indexCount * sizeof(uint)),
                _ => throw new InvalidDataException($"GEOM index size {indexSize} is invalid.")
            };

            EnsureBytesAvailable(reader, indexByteCount, $"submesh index buffer #{subMeshIndex}");
            for (var index = 0; index < indexCount; index++)
            {
                var value = indexSize == 2 ? reader.ReadUInt16() : reader.ReadUInt32();
                if (value > int.MaxValue)
                {
                    throw new InvalidDataException($"GEOM index value {value} is too large.");
                }

                results.Add((int)value);
            }
        }

        return results;
    }

    private static void SkipUvStitchData(BinaryReader reader)
    {
        var stitchCount = ReadNonNegativeInt32(reader, "uv stitch count");
        for (var stitchIndex = 0; stitchIndex < stitchCount; stitchIndex++)
        {
            EnsureBytesAvailable(reader, sizeof(uint) + sizeof(int), $"uv stitch header #{stitchIndex}");
            _ = reader.ReadUInt32();
            var coordinateCount = ReadNonNegativeInt32(reader, $"uv stitch coordinate count #{stitchIndex}");
            SkipBytes(reader, checked((long)coordinateCount * 8), $"uv stitch payload #{stitchIndex}");
        }
    }

    private static void SkipSeamStitchData(BinaryReader reader)
    {
        var seamStitchCount = ReadNonNegativeInt32(reader, "seam stitch count");
        SkipBytes(reader, checked((long)seamStitchCount * 6), "seam stitch payload");
    }

    private static void SkipSlotIntersectionData(BinaryReader reader, uint version)
    {
        var slotCount = ReadNonNegativeInt32(reader, "slot intersection count");
        var slotIntersectionSize = version >= 0x0000000E ? 66 : 63;
        SkipBytes(reader, checked((long)slotCount * slotIntersectionSize), "slot intersection payload");
    }

    private static void SkipGeometryStates(BinaryReader reader)
    {
        var geometryStateCount = ReadNonNegativeInt32(reader, "geometry state count");
        SkipBytes(reader, checked((long)geometryStateCount * 20), "geometry state payload");
    }

    private static void SkipResourceKeyTable(BinaryReader reader)
    {
        var keyCount = ReadNonNegativeInt32(reader, "resource key count");
        SkipBytes(reader, checked((long)keyCount * 16), "resource key table");
    }

    private static int GetVertexStride(uint version, IReadOnlyList<Ts4GeomFormat> formats)
    {
        var stride = 0;
        foreach (var format in formats)
        {
            stride += format.Usage switch
            {
                0x01 or 0x02 or 0x06 => 12,
                0x03 => 8,
                0x04 => 4,
                0x05 when version == 0x00000005 => 16,
                0x05 => 4,
                0x07 or 0x0A => 4,
                _ => throw new InvalidDataException($"Unsupported GEOM usage 0x{format.Usage:X8}.")
            };
        }

        return stride;
    }

    private static int ReadNonNegativeInt32(BinaryReader reader, string label)
    {
        var value = reader.ReadInt32();
        if (value < 0)
        {
            throw new InvalidDataException($"GEOM {label} {value} is invalid.");
        }

        return value;
    }

    private static void EnsureBytesAvailable(BinaryReader reader, long byteCount, string label)
    {
        if (byteCount < 0)
        {
            throw new InvalidDataException($"GEOM {label} length {byteCount} is invalid.");
        }

        var remaining = reader.BaseStream.Length - reader.BaseStream.Position;
        if (byteCount > remaining)
        {
            throw new InvalidDataException($"GEOM {label} extends beyond the payload.");
        }
    }

    private static void SkipBytes(BinaryReader reader, long byteCount, string label)
    {
        EnsureBytesAvailable(reader, byteCount, label);
        reader.BaseStream.Position += byteCount;
    }

    private static void ExpectTag(BinaryReader reader, string expected)
    {
        EnsureBytesAvailable(reader, expected.Length, "tag");
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
    float[]? Uv1,
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
