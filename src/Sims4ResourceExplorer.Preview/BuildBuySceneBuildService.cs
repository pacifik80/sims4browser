using System.Buffers.Binary;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.Preview;

public sealed class BuildBuySceneBuildService : ISceneBuildService
{
    private readonly IResourceCatalogService resourceCatalogService;
    private readonly IIndexStore indexStore;

    public BuildBuySceneBuildService(IResourceCatalogService resourceCatalogService, IIndexStore indexStore)
    {
        this.resourceCatalogService = resourceCatalogService;
        this.indexStore = indexStore;
    }

    public async Task<SceneBuildResult> BuildSceneAsync(ResourceMetadata resource, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (resource.Key.TypeName is not ("Model" or "ModelLOD"))
        {
            return new SceneBuildResult(
                false,
                null,
                [$"Scene reconstruction is currently supported for Build/Buy Model and ModelLOD roots only. Selected type: {resource.Key.TypeName}."]);
        }

        try
        {
            return resource.Key.TypeName == "Model"
                ? await BuildModelSceneAsync(resource, cancellationToken)
                : await BuildModelLodSceneAsync(resource, resource, cancellationToken);
        }
        catch (Exception ex)
        {
            return new SceneBuildResult(false, null, [$"Scene reconstruction failed for {resource.Key.FullTgi}: {ex.Message}"]);
        }
    }

    private async Task<SceneBuildResult> BuildModelSceneAsync(ResourceMetadata modelResource, CancellationToken cancellationToken)
    {
        var bytes = await resourceCatalogService.GetResourceBytesAsync(modelResource.PackagePath, modelResource.Key, raw: false, cancellationToken);
        var root = Ts4RcolResource.Parse(bytes);
        var modlChunk = root.Chunks.FirstOrDefault(static chunk => chunk.Tag == "MODL");
        if (modlChunk is null)
        {
            return new SceneBuildResult(false, null, [$"Model {modelResource.Key.FullTgi} does not contain a MODL chunk."]);
        }

        var modl = Ts4ModlChunk.Parse(modlChunk.Data.Span);
        var lodEntries = modl.LodEntries
            .OrderBy(static lod => lod.IsShadow)
            .ThenBy(static lod => lod.Id)
            .ToArray();

        if (lodEntries.Length == 0)
        {
            return new SceneBuildResult(false, null, [$"Model {modelResource.Key.FullTgi} does not expose any LOD entries."]);
        }

        var diagnostics = new List<string>
        {
            $"Available LODs: {string.Join(", ", lodEntries.Select(static lod => lod.DisplayName))}"
        };

        foreach (var lod in lodEntries)
        {
            var result = await TryResolveModelLodAsync(modelResource, root, lod.Reference, cancellationToken);
            diagnostics.AddRange(result.Diagnostics);
            if (result.Resource is not null)
            {
                var sceneResult = await BuildModelLodSceneAsync(result.Resource, modelResource, cancellationToken);
                if (sceneResult.Success)
                {
                    return new SceneBuildResult(true, sceneResult.Scene, diagnostics.Concat(sceneResult.Diagnostics).ToArray());
                }

                diagnostics.AddRange(sceneResult.Diagnostics);
            }
        }

        return new SceneBuildResult(false, null, diagnostics);
    }

    private async Task<SceneBuildResult> BuildModelLodSceneAsync(
        ResourceMetadata modelLodResource,
        ResourceMetadata logicalRootResource,
        CancellationToken cancellationToken)
    {
        var bytes = await resourceCatalogService.GetResourceBytesAsync(modelLodResource.PackagePath, modelLodResource.Key, raw: false, cancellationToken);
        var rcol = Ts4RcolResource.Parse(bytes);
        var mlodChunk = rcol.Chunks.FirstOrDefault(static chunk => chunk.Tag == "MLOD");
        if (mlodChunk is null)
        {
            return new SceneBuildResult(false, null, [$"ModelLOD {modelLodResource.Key.FullTgi} does not contain an MLOD chunk."]);
        }

        var mlod = Ts4MlodChunk.Parse(mlodChunk.Data.Span);
        var diagnostics = new List<string>();
        var meshes = new List<CanonicalMesh>();
        var materials = new List<CanonicalMaterial>();
        var materialCache = new Dictionary<uint, int>();
        var textureCache = new Dictionary<string, CanonicalTexture?>(StringComparer.OrdinalIgnoreCase);

        foreach (var mesh in mlod.Meshes)
        {
            if (mesh.IsSkinned)
            {
                diagnostics.Add($"Skipped mesh 0x{mesh.Name:X8}: skinned/static-blend meshes are outside the supported static Build/Buy subset.");
                continue;
            }

            if (mesh.PrimitiveType != Ts4PrimitiveType.TriangleList)
            {
                diagnostics.Add($"Skipped mesh 0x{mesh.Name:X8}: primitive type {mesh.PrimitiveType} is not supported in this pass.");
                continue;
            }

            try
            {
                var vertexBufferChunk = rcol.ResolveChunk(mesh.VertexBufferReference);
                var indexBufferChunk = rcol.ResolveChunk(mesh.IndexBufferReference);
                if (vertexBufferChunk is null || indexBufferChunk is null)
                {
                    diagnostics.Add($"Skipped mesh 0x{mesh.Name:X8}: vertex/index buffers could not be resolved.");
                    continue;
                }

                Ts4VrtfChunk vrtf;
                var vrtfChunk = rcol.ResolveChunk(mesh.VertexFormatReference);
                if (vrtfChunk is not null)
                {
                    vrtf = Ts4VrtfChunk.Parse(vrtfChunk.Data.Span);
                }
                else if (mesh.IsShadowCaster)
                {
                    vrtf = Ts4VrtfChunk.CreateShadowDefault();
                    diagnostics.Add($"Mesh 0x{mesh.Name:X8} uses the default shadow vertex format because no VRTF chunk was linked.");
                }
                else
                {
                    diagnostics.Add($"Skipped mesh 0x{mesh.Name:X8}: vertex format could not be resolved.");
                    continue;
                }

                var vbuf = Ts4VbufChunk.Parse(vertexBufferChunk.Data.Span);
                var ibuf = Ts4IbufChunk.Parse(indexBufferChunk.Data.Span);
                var materialInfo = await ResolveMaterialAsync(rcol.ResolveChunk(mesh.MaterialReference), rcol, modelLodResource, textureCache, cancellationToken);
                diagnostics.AddRange(materialInfo.Diagnostics);

                var materialKey = mesh.MaterialReference.Raw;
                if (!materialCache.TryGetValue(materialKey, out var materialIndex))
                {
                    materialIndex = materials.Count;
                    materialCache[materialKey] = materialIndex;
                    materials.Add(new CanonicalMaterial(
                        materialInfo.Name,
                        materialInfo.Textures,
                        materialInfo.ShaderName,
                        materialInfo.IsTransparent,
                        materialInfo.AlphaMode,
                        materialInfo.Approximation));
                }

                var vertices = vbuf.ReadVertices(vrtf, mesh.StreamOffset, mesh.VertexCount, materialInfo.UvScales);
                var indices = ibuf.ReadIndices(mesh.StartIndex, mesh.PrimitiveCount * 3)
                    .Select(static value => (int)value)
                    .ToArray();

                meshes.Add(new CanonicalMesh(
                    $"Mesh_{mesh.Name:X8}",
                    vertices.SelectMany(static vertex => vertex.Position).ToArray(),
                    vertices.SelectMany(static vertex => vertex.Normal ?? []).ToArray(),
                    vertices.SelectMany(static vertex => vertex.Tangent ?? []).ToArray(),
                    vertices.SelectMany(static vertex => vertex.Uv0 ?? []).ToArray(),
                    indices,
                    materialIndex,
                    []));
            }
            catch (Exception ex) when (ex is NotSupportedException or InvalidDataException)
            {
                diagnostics.Add($"Skipped mesh 0x{mesh.Name:X8}: {ex.Message}");
            }
        }

        if (meshes.Count == 0)
        {
            diagnostics.Insert(0, $"No triangle meshes could be reconstructed from {modelLodResource.Key.FullTgi}.");
            return new SceneBuildResult(false, null, diagnostics);
        }

        var scene = new CanonicalScene(
            logicalRootResource.Name ?? modelLodResource.Name ?? $"BuildBuy_{logicalRootResource.Key.FullInstance:X16}",
            meshes,
            materials,
            [],
            ComputeBounds(meshes));

        diagnostics.Insert(0, $"Selected LOD root: {modelLodResource.Key.FullTgi}");
        return new SceneBuildResult(true, scene, diagnostics);
    }

    private async Task<Ts4MaterialInfo> ResolveMaterialAsync(
        Ts4RcolChunk? materialChunk,
        Ts4RcolResource rcol,
        ResourceMetadata ownerResource,
        Dictionary<string, CanonicalTexture?> textureCache,
        CancellationToken cancellationToken)
    {
        if (materialChunk is null)
        {
            return Ts4MaterialInfo.CreateFallback("MissingMaterial", "Material chunk is missing.");
        }

        if (materialChunk.Tag == "MATD")
        {
            var matd = Ts4MatdChunk.Parse(materialChunk.Data.Span);
            return await BuildMaterialInfoAsync(matd, ownerResource, textureCache, cancellationToken);
        }

        if (materialChunk.Tag == "MTST")
        {
            var mtst = Ts4MtstChunk.Parse(materialChunk.Data.Span);
            var selectedReference = mtst.DefaultMaterialReference ?? mtst.MaterialReferences.FirstOrDefault();
            if (!selectedReference.IsNull)
            {
                var matdChunk = rcol.ResolveChunk(selectedReference);
                if (matdChunk is not null && matdChunk.Tag == "MATD")
                {
                    var matd = Ts4MatdChunk.Parse(matdChunk.Data.Span);
                    return await BuildMaterialInfoAsync(matd, ownerResource, textureCache, cancellationToken);
                }
            }

            return Ts4MaterialInfo.CreateFallback(
                $"MaterialSet_{materialChunk.Key.Instance:X16}",
                "MTST did not expose a usable embedded MATD.");
        }

        return Ts4MaterialInfo.CreateFallback(
            $"Material_{materialChunk.Key.Instance:X16}",
            $"Unsupported material chunk tag {materialChunk.Tag}.");
    }

    private async Task<Ts4MaterialInfo> BuildMaterialInfoAsync(
        Ts4MatdChunk matd,
        ResourceMetadata ownerResource,
        Dictionary<string, CanonicalTexture?> textureCache,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<string>();
        var textures = new List<CanonicalTexture>();
        foreach (var reference in matd.TextureReferences)
        {
            var cacheKey = $"{reference.Key.Type:X8}:{reference.Key.Group:X8}:{reference.Key.Instance:X16}";
            if (!textureCache.TryGetValue(cacheKey, out var cachedTexture))
            {
                var coreKey = new ResourceKeyRecord(
                    reference.Key.Type,
                    reference.Key.Group,
                    reference.Key.Instance,
                    GuessTypeName(reference.Key.Type));

                var pngBytes = await resourceCatalogService.GetTexturePngAsync(ownerResource.PackagePath, coreKey, cancellationToken);
                cachedTexture = pngBytes is null
                    ? null
                    : new CanonicalTexture(
                        reference.Slot,
                        BuildTextureFileName(reference.Slot, reference.Key),
                        pngBytes,
                        coreKey,
                        ownerResource.PackagePath);

                textureCache[cacheKey] = cachedTexture;
            }

            if (cachedTexture is null)
            {
                diagnostics.Add($"Texture decode failed or resource was unavailable for slot {reference.Slot} ({reference.Key.Type:X8}:{reference.Key.Group:X8}:{reference.Key.Instance:X16}).");
            }
            else
            {
                textures.Add(cachedTexture with { Slot = reference.Slot });
            }
        }

        if (textures.Count == 0)
        {
            var approximatedTextures = await ResolveFallbackTexturesAsync(ownerResource, textureCache, cancellationToken).ConfigureAwait(false);
            if (approximatedTextures.Count > 0)
            {
                textures.AddRange(approximatedTextures);
                diagnostics.Add("No explicit MATD texture references were resolved; using exact-instance texture candidates as a portable diffuse approximation.");
            }
        }

        var isTransparent = matd.IsTransparent || matd.TextureReferences.Any(static texture => texture.Slot is "alpha" or "overlay");
        return new Ts4MaterialInfo(
            !string.IsNullOrWhiteSpace(matd.MaterialName) ? matd.MaterialName : $"Material_{matd.MaterialNameHash:X8}",
            matd.ShaderName,
            matd.UvScales,
            textures,
            diagnostics,
            isTransparent,
            isTransparent ? "alpha-test-or-blend" : "opaque",
            "Maxis shaders are approximated to a portable texture-set export.");
    }

    private async Task<IReadOnlyList<CanonicalTexture>> ResolveFallbackTexturesAsync(
        ResourceMetadata ownerResource,
        Dictionary<string, CanonicalTexture?> textureCache,
        CancellationToken cancellationToken)
    {
        var packageResources = await indexStore.GetPackageResourcesAsync(ownerResource.PackagePath, cancellationToken).ConfigureAwait(false);
        var textureCandidates = packageResources
            .Where(resource => resource.Key.FullInstance == ownerResource.Key.FullInstance && IsTextureType(resource.Key.TypeName))
            .OrderBy(static resource => resource.Key.TypeName == "BuyBuildThumbnail" ? 1 : 0)
            .ThenBy(static resource => resource.Key.TypeName, StringComparer.Ordinal)
            .ToArray();

        var results = new List<CanonicalTexture>(textureCandidates.Length);
        foreach (var candidate in textureCandidates)
        {
            var cacheKey = candidate.Key.FullTgi;
            if (!textureCache.TryGetValue(cacheKey, out var cachedTexture))
            {
                var pngBytes = await resourceCatalogService.GetTexturePngAsync(candidate.PackagePath, candidate.Key, cancellationToken).ConfigureAwait(false);
                cachedTexture = pngBytes is null
                    ? null
                    : new CanonicalTexture(
                        results.Count == 0 ? "diffuse" : $"extra_{results.Count}",
                        BuildTextureFileName(results.Count == 0 ? "diffuse" : $"extra_{results.Count}", new Ts4ResourceKey(candidate.Key.Type, candidate.Key.Group, candidate.Key.FullInstance)),
                        pngBytes,
                        candidate.Key,
                        candidate.PackagePath);

                textureCache[cacheKey] = cachedTexture;
            }

            if (cachedTexture is not null)
            {
                results.Add(cachedTexture);
            }
        }

        return results;
    }

    private Task<Ts4ModelLodResolution> TryResolveModelLodAsync(
        ResourceMetadata modelResource,
        Ts4RcolResource root,
        Ts4ChunkReference reference,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var diagnostics = new List<string>();
        if (reference.IsNull)
        {
            diagnostics.Add("Encountered a null ModelLOD reference in MODL.");
            return Task.FromResult(new Ts4ModelLodResolution(null, diagnostics));
        }

        if (reference.ReferenceType is Ts4ReferenceType.Public or Ts4ReferenceType.Private)
        {
            var embeddedChunk = root.ResolveChunk(reference);
            if (embeddedChunk is null || embeddedChunk.Tag != "MLOD")
            {
                diagnostics.Add($"Embedded ModelLOD reference {reference.Raw:X8} could not be resolved to an MLOD chunk.");
                return Task.FromResult(new Ts4ModelLodResolution(null, diagnostics));
            }

            return Task.FromResult(new Ts4ModelLodResolution(
                modelResource with
                {
                    Key = new ResourceKeyRecord(embeddedChunk.Key.Type, embeddedChunk.Key.Group, embeddedChunk.Key.Instance, "ModelLOD"),
                    PreviewKind = PreviewKind.Scene,
                    Name = modelResource.Name
                },
                diagnostics));
        }

        if (reference.ReferenceType == Ts4ReferenceType.Delayed)
        {
            var delayedKey = root.ResolveExternalKey(reference);
            if (delayedKey is null)
            {
                diagnostics.Add($"Delayed ModelLOD reference {reference.Raw:X8} could not be mapped to an external resource key.");
                return Task.FromResult(new Ts4ModelLodResolution(null, diagnostics));
            }

            return Task.FromResult(new Ts4ModelLodResolution(
                new ResourceMetadata(
                    Guid.NewGuid(),
                    modelResource.DataSourceId,
                    modelResource.SourceKind,
                    modelResource.PackagePath,
                    new ResourceKeyRecord(delayedKey.Value.Type, delayedKey.Value.Group, delayedKey.Value.Instance, "ModelLOD"),
                    modelResource.Name,
                    0,
                    0,
                    false,
                    PreviewKind.Scene,
                    true,
                    true,
                    "Referenced by Model MODL LOD entry.",
                    string.Empty),
                diagnostics));
        }

        diagnostics.Add($"Unsupported MODL reference type {reference.ReferenceType}.");
        return Task.FromResult(new Ts4ModelLodResolution(null, diagnostics));
    }

    private static string BuildTextureFileName(string slot, Ts4ResourceKey key) =>
        $"{slot}_{key.Type:X8}_{key.Group:X8}_{key.Instance:X16}.png";

    private static string GuessTypeName(uint type) => type switch
    {
        0x00B2D882 => "PNGImage",
        0x2F7D0004 => "PNGImage2",
        0x2BC04EDF => "DSTImage",
        0x0988C7E1 => "LRLEImage",
        0x3453CF95 => "RLE2Image",
        0x1B4D2A70 => "RLESImage",
        _ => $"0x{type:X8}"
    };

    private static bool IsTextureType(string typeName) => typeName is
        "BuyBuildThumbnail" or
        "PNGImage" or
        "PNGImage2" or
        "DSTImage" or
        "LRLEImage" or
        "RLE2Image" or
        "RLESImage";

    private static Bounds3D ComputeBounds(IEnumerable<CanonicalMesh> meshes)
    {
        var positions = meshes.SelectMany(static mesh => mesh.Positions).Chunk(3).ToArray();
        if (positions.Length == 0)
        {
            return new Bounds3D(0, 0, 0, 0, 0, 0);
        }

        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var minZ = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;
        var maxZ = float.NegativeInfinity;

        foreach (var position in positions)
        {
            minX = Math.Min(minX, position[0]);
            minY = Math.Min(minY, position[1]);
            minZ = Math.Min(minZ, position[2]);
            maxX = Math.Max(maxX, position[0]);
            maxY = Math.Max(maxY, position[1]);
            maxZ = Math.Max(maxZ, position[2]);
        }

        return new Bounds3D(minX, minY, minZ, maxX, maxY, maxZ);
    }
}

internal sealed record Ts4ModelLodResolution(ResourceMetadata? Resource, IReadOnlyList<string> Diagnostics);

internal readonly record struct Ts4ChunkReference(uint Raw)
{
    public bool IsNull => Raw == 0;
    public Ts4ReferenceType ReferenceType => IsNull ? Ts4ReferenceType.Invalid : (Ts4ReferenceType)(Raw >> 28);
    public int Index => IsNull ? -1 : (int)(Raw & 0x0FFFFFFF) - 1;
}

internal enum Ts4ReferenceType : byte
{
    Invalid = 255,
    Public = 0,
    Private = 1,
    Delayed = 3
}

internal readonly record struct Ts4ResourceKey(uint Type, uint Group, ulong Instance);

internal sealed record Ts4RcolChunk(Ts4ResourceKey Key, Ts4ChunkReference SelfReference, int Position, int Length, ReadOnlyMemory<byte> Data, string Tag);

internal sealed record Ts4MaterialInfo(
    string Name,
    string ShaderName,
    float[] UvScales,
    IReadOnlyList<CanonicalTexture> Textures,
    IReadOnlyList<string> Diagnostics,
    bool IsTransparent,
    string AlphaMode,
    string Approximation)
{
    public static Ts4MaterialInfo CreateFallback(string name, string reason) =>
        new(
            name,
            "Unsupported",
            [1f / short.MaxValue, 1f / short.MaxValue, 1f / short.MaxValue],
            [],
            [reason],
            false,
            "opaque",
            "Material fell back to a placeholder because the source material chunk was unsupported.");
}

internal readonly record struct Ts4TextureReference(string Slot, Ts4ResourceKey Key);

internal sealed record Ts4MatdChunk(
    string MaterialName,
    uint MaterialNameHash,
    string ShaderName,
    float[] UvScales,
    IReadOnlyList<Ts4TextureReference> TextureReferences,
    bool IsTransparent)
{
    public static Ts4MatdChunk Parse(ReadOnlySpan<byte> bytes) =>
        new(
            "Material",
            0,
            "Unsupported",
            [1f / short.MaxValue, 1f / short.MaxValue, 1f / short.MaxValue],
            [],
            false);
}

internal sealed record Ts4MtstChunk(Ts4ChunkReference? DefaultMaterialReference, IReadOnlyList<Ts4ChunkReference> MaterialReferences)
{
    public static Ts4MtstChunk Parse(ReadOnlySpan<byte> bytes) =>
        new(null, []);
}

internal ref struct SpanReader
{
    private ReadOnlySpan<byte> remaining;

    public SpanReader(ReadOnlySpan<byte> data)
    {
        remaining = data;
        Position = 0;
    }

    public int Position { get; private set; }

    public void Skip(int count)
    {
        Ensure(count);
        remaining = remaining[count..];
        Position += count;
    }

    public void ExpectTag(string tag)
    {
        var bytes = ReadBytes(tag.Length);
        var actual = System.Text.Encoding.ASCII.GetString(bytes);
        if (!string.Equals(actual, tag, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Expected tag '{tag}', found '{actual}'.");
        }
    }

    public byte ReadByte()
    {
        Ensure(1);
        var value = remaining[0];
        remaining = remaining[1..];
        Position += 1;
        return value;
    }

    public ushort ReadUInt16()
    {
        var value = BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(2));
        Position += 2;
        remaining = remaining[2..];
        return value;
    }

    public short ReadInt16()
    {
        var value = BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(2));
        Position += 2;
        remaining = remaining[2..];
        return value;
    }

    public uint ReadUInt32()
    {
        var value = BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(4));
        Position += 4;
        remaining = remaining[4..];
        return value;
    }

    public int ReadInt32()
    {
        var value = BinaryPrimitives.ReadInt32LittleEndian(ReadBytes(4));
        Position += 4;
        remaining = remaining[4..];
        return value;
    }

    public float ReadSingle()
    {
        var value = BinaryPrimitives.ReadSingleLittleEndian(ReadBytes(4));
        Position += 4;
        remaining = remaining[4..];
        return value;
    }

    public Ts4ResourceKey ReadResourceKey() =>
        new(ReadUInt32(), ReadUInt32(), ReadUInt64());

    public ulong ReadUInt64()
    {
        var value = BinaryPrimitives.ReadUInt64LittleEndian(ReadBytes(8));
        Position += 8;
        remaining = remaining[8..];
        return value;
    }

    public ReadOnlySpan<byte> ReadBytes(int count)
    {
        Ensure(count);
        return remaining[..count];
    }

    private void Ensure(int count)
    {
        if (count < 0 || remaining.Length < count)
        {
            throw new InvalidDataException("Unexpected end of TS4 chunk data.");
        }
    }
}

internal enum Ts4VertexUsage : byte
{
    Position = 0,
    Normal = 1,
    Uv = 2,
    BlendIndex = 3,
    BlendWeight = 4,
    Color = 5,
    Tangent = 6
}

internal sealed record Ts4VertexElement(Ts4VertexUsage Usage, byte UsageIndex, byte Format, ushort Offset);

internal sealed class Ts4VrtfChunk
{
    public required int VertexStride { get; init; }
    public required IReadOnlyList<Ts4VertexElement> Elements { get; init; }

    public static Ts4VrtfChunk Parse(ReadOnlySpan<byte> data)
    {
        var reader = new SpanReader(data);
        reader.ExpectTag("VRTF");
        _ = reader.ReadUInt32();
        var stride = reader.ReadUInt16();
        var elementCount = reader.ReadUInt16();
        _ = reader.ReadUInt32();

        var elements = new List<Ts4VertexElement>(elementCount);
        for (var index = 0; index < elementCount; index++)
        {
            var usage = (Ts4VertexUsage)reader.ReadByte();
            var usageIndex = reader.ReadByte();
            var format = reader.ReadByte();
            _ = reader.ReadByte();
            var offset = reader.ReadUInt16();
            _ = reader.ReadUInt16();
            elements.Add(new Ts4VertexElement(usage, usageIndex, format, offset));
        }

        return new Ts4VrtfChunk
        {
            VertexStride = stride,
            Elements = elements
        };
    }

    public static Ts4VrtfChunk CreateShadowDefault() =>
        new()
        {
            VertexStride = 16,
            Elements =
            [
                new Ts4VertexElement(Ts4VertexUsage.Position, 0, 0x07, 0)
            ]
        };
}

internal sealed record Ts4DecodedVertex(float[] Position, float[]? Normal, float[]? Tangent, float[]? Uv0);

internal sealed class Ts4VbufChunk
{
    private readonly byte[] rawData;
    private readonly Ts4ChunkReference swizzleInfoReference;

    private Ts4VbufChunk(byte[] rawData, Ts4ChunkReference swizzleInfoReference)
    {
        this.rawData = rawData;
        this.swizzleInfoReference = swizzleInfoReference;
    }

    public static Ts4VbufChunk Parse(ReadOnlySpan<byte> data)
    {
        var reader = new SpanReader(data);
        reader.ExpectTag("VBUF");
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        var swizzleInfoReference = new Ts4ChunkReference(reader.ReadUInt32());

        var payload = data[reader.Position..].ToArray();
        return new Ts4VbufChunk(payload, swizzleInfoReference);
    }

    public IReadOnlyList<Ts4DecodedVertex> ReadVertices(Ts4VrtfChunk vrtf, uint streamOffset, int vertexCount, float[] uvScales)
    {
        if (!swizzleInfoReference.IsNull)
        {
            throw new NotSupportedException("Swizzled VBUF layouts are not supported in this pass.");
        }

        var results = new List<Ts4DecodedVertex>(vertexCount);
        for (var index = 0; index < vertexCount; index++)
        {
            var vertexOffset = checked((int)streamOffset + (index * vrtf.VertexStride));
            if (vertexOffset < 0 || vertexOffset + vrtf.VertexStride > rawData.Length)
            {
                break;
            }

            float[]? position = null;
            float[]? normal = null;
            float[]? tangent = null;
            float[]? uv0 = null;

            foreach (var element in vrtf.Elements)
            {
                var offset = vertexOffset + element.Offset;
                if (offset < 0 || offset >= rawData.Length)
                {
                    continue;
                }

                switch (element.Usage)
                {
                    case Ts4VertexUsage.Position:
                        position = ReadVector3(rawData, offset, element.Format);
                        break;
                    case Ts4VertexUsage.Normal:
                        normal = ReadNormalLike(rawData, offset, element.Format);
                        break;
                    case Ts4VertexUsage.Tangent:
                        tangent = ReadNormalLike(rawData, offset, element.Format);
                        break;
                    case Ts4VertexUsage.Uv when element.UsageIndex == 0:
                        uv0 = ReadUv(rawData, offset, element.Format, uvScales);
                        break;
                }
            }

            if (position is null)
            {
                continue;
            }

            results.Add(new Ts4DecodedVertex(position, normal, tangent, uv0));
        }

        return results;
    }

    private static float[] ReadVector3(byte[] data, int offset, byte format) => format switch
    {
        0x02 => [ReadSingle(data, offset), ReadSingle(data, offset + 4), ReadSingle(data, offset + 8)],
        0x07 => DecodeShort4Normalized(data, offset),
        _ => throw new NotSupportedException($"Unsupported position vertex format 0x{format:X2}.")
    };

    private static float[] ReadNormalLike(byte[] data, int offset, byte format) => format switch
    {
        0x05 => DecodeColor4Normalized(data, offset),
        0x07 => DecodeShort4Normalized(data, offset),
        _ => throw new NotSupportedException($"Unsupported normal/tangent vertex format 0x{format:X2}.")
    };

    private static float[] ReadUv(byte[] data, int offset, byte format, float[] uvScales)
    {
        var scaleU = uvScales.Length > 0 ? uvScales[0] : 1f;
        var scaleV = uvScales.Length > 1 ? uvScales[1] : scaleU;

        var uv = format switch
        {
            0x01 => [ReadSingle(data, offset), ReadSingle(data, offset + 4)],
            0x06 => DecodeShort2Normalized(data, offset),
            0x07 => DecodeShort4Normalized(data, offset)[..2],
            _ => throw new NotSupportedException($"Unsupported UV vertex format 0x{format:X2}.")
        };

        uv[0] *= scaleU;
        uv[1] *= scaleV;
        return uv;
    }

    private static float[] DecodeColor4Normalized(byte[] data, int offset)
    {
        var x = ((data[offset + 2] / 255f) * 2f) - 1f;
        var y = ((data[offset + 1] / 255f) * 2f) - 1f;
        var z = ((data[offset] / 255f) * 2f) - 1f;
        return [x, y, z];
    }

    private static float[] DecodeShort4Normalized(byte[] data, int offset)
    {
        var x = ReadInt16(data, offset);
        var y = ReadInt16(data, offset + 2);
        var z = ReadInt16(data, offset + 4);
        var w = ReadUInt16(data, offset + 6);
        var scale = w == 0 ? (float)short.MaxValue : w;
        return [x / (float)scale, y / (float)scale, z / (float)scale];
    }

    private static float[] DecodeShort2Normalized(byte[] data, int offset)
    {
        var u = ReadInt16(data, offset) / (float)short.MaxValue;
        var v = ReadInt16(data, offset + 2) / (float)short.MaxValue;
        return [u, v];
    }

    private static float ReadSingle(byte[] data, int offset) =>
        BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset, 4));

    private static short ReadInt16(byte[] data, int offset) =>
        BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset, 2));

    private static ushort ReadUInt16(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));
}

internal sealed class Ts4IbufChunk
{
    private readonly int indexSize;
    private readonly byte[] rawData;

    private Ts4IbufChunk(int indexSize, byte[] rawData)
    {
        this.indexSize = indexSize;
        this.rawData = rawData;
    }

    public static Ts4IbufChunk Parse(ReadOnlySpan<byte> data)
    {
        var reader = new SpanReader(data);
        reader.ExpectTag("IBUF");
        _ = reader.ReadUInt32();
        var flags = reader.ReadUInt32();
        var payload = data[reader.Position..].ToArray();
        var indexSize = (flags & 0x1) != 0 ? 4 : 2;
        return new Ts4IbufChunk(indexSize, payload);
    }

    public IReadOnlyList<uint> ReadIndices(int startIndex, int count)
    {
        var results = new List<uint>(count);
        var startOffset = startIndex * indexSize;
        for (var offset = startOffset; offset + indexSize <= rawData.Length && results.Count < count; offset += indexSize)
        {
            results.Add(indexSize == 4
                ? BinaryPrimitives.ReadUInt32LittleEndian(rawData.AsSpan(offset, 4))
                : BinaryPrimitives.ReadUInt16LittleEndian(rawData.AsSpan(offset, 2)));
        }

        return results;
    }
}

internal sealed class Ts4RcolResource
{
    public required int PublicChunkCount { get; init; }
    public required IReadOnlyList<Ts4RcolChunk> Chunks { get; init; }
    public required IReadOnlyList<Ts4ResourceKey> ExternalResources { get; init; }

    public Ts4RcolChunk? ResolveChunk(Ts4ChunkReference reference)
    {
        if (reference.IsNull)
        {
            return null;
        }

        var index = reference.ReferenceType switch
        {
            Ts4ReferenceType.Public => reference.Index,
            Ts4ReferenceType.Private => PublicChunkCount + reference.Index,
            _ => -1
        };

        return index >= 0 && index < Chunks.Count ? Chunks[index] : null;
    }

    public Ts4ResourceKey? ResolveExternalKey(Ts4ChunkReference reference)
    {
        if (reference.ReferenceType != Ts4ReferenceType.Delayed)
        {
            return null;
        }

        return reference.Index >= 0 && reference.Index < ExternalResources.Count
            ? ExternalResources[reference.Index]
            : null;
    }

    public static Ts4RcolResource Parse(ReadOnlyMemory<byte> bytes)
    {
        var reader = new SpanReader(bytes.Span);
        _ = reader.ReadUInt32();
        var publicChunkCount = reader.ReadInt32();
        _ = reader.ReadUInt32();
        var externalCount = reader.ReadInt32();
        var chunkCount = reader.ReadInt32();

        var chunkKeys = new Ts4ResourceKey[chunkCount];
        for (var i = 0; i < chunkCount; i++)
        {
            chunkKeys[i] = reader.ReadResourceKey();
        }

        var externalKeys = new Ts4ResourceKey[externalCount];
        for (var i = 0; i < externalCount; i++)
        {
            externalKeys[i] = reader.ReadResourceKey();
        }

        var positions = new (int Position, int Length)[chunkCount];
        for (var i = 0; i < chunkCount; i++)
        {
            positions[i] = (checked((int)reader.ReadUInt32()), reader.ReadInt32());
        }

        var chunks = new List<Ts4RcolChunk>(chunkCount);
        for (var i = 0; i < chunkCount; i++)
        {
            var (position, length) = positions[i];
            if (position < 0 || length < 0 || position + length > bytes.Length)
            {
                continue;
            }

            var data = bytes.Slice(position, length);
            var tag = data.Length >= 4
                ? System.Text.Encoding.ASCII.GetString(data.Span[..4])
                : string.Empty;

            chunks.Add(new Ts4RcolChunk(
                chunkKeys[i],
                new Ts4ChunkReference(CreateReferenceRaw(i, publicChunkCount)),
                position,
                length,
                data,
                tag));
        }

        return new Ts4RcolResource
        {
            PublicChunkCount = publicChunkCount,
            Chunks = chunks,
            ExternalResources = externalKeys
        };
    }

    private static uint CreateReferenceRaw(int chunkIndex, int publicChunkCount)
    {
        var index = (uint)(chunkIndex + 1);
        return chunkIndex < publicChunkCount ? index : index | 0x10000000;
    }
}

internal sealed class Ts4ModlChunk
{
    public required IReadOnlyList<Ts4ModlLodEntry> LodEntries { get; init; }

    public static Ts4ModlChunk Parse(ReadOnlySpan<byte> data)
    {
        var reader = new SpanReader(data);
        reader.ExpectTag("MODL");
        var version = reader.ReadUInt32();
        var lodCount = reader.ReadInt32();
        reader.Skip(24);

        if (version >= 258 && version < 0x300)
        {
            var extraBoundsCount = reader.ReadInt32();
            reader.Skip(extraBoundsCount * 24);
            reader.Skip(8);
        }
        else if (version >= 0x300)
        {
            reader.Skip(20);
        }

        var lodEntries = new List<Ts4ModlLodEntry>(lodCount);
        for (var i = 0; i < lodCount; i++)
        {
            lodEntries.Add(new Ts4ModlLodEntry(
                new Ts4ChunkReference(reader.ReadUInt32()),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadSingle(),
                reader.ReadSingle()));
        }

        return new Ts4ModlChunk { LodEntries = lodEntries };
    }
}

internal readonly record struct Ts4ModlLodEntry(Ts4ChunkReference Reference, uint Flags, uint Id, float MinZ, float MaxZ)
{
    public bool IsShadow => (Id & 0x00010000) != 0;
    public string DisplayName => Id switch
    {
        0x00000000 => "HighDetail",
        0x00000001 => "MediumDetail",
        0x00000002 => "LowDetail",
        0x00010000 => "HighDetailShadow",
        0x00010001 => "MediumDetailShadow",
        0x00010002 => "LowDetailShadow",
        _ => $"LOD_{Id:X8}"
    };
}

internal sealed class Ts4MlodChunk
{
    public required IReadOnlyList<Ts4MlodMesh> Meshes { get; init; }

    public static Ts4MlodChunk Parse(ReadOnlySpan<byte> data)
    {
        var reader = new SpanReader(data);
        reader.ExpectTag("MLOD");
        var version = reader.ReadUInt32();
        var meshCount = reader.ReadInt32();
        var meshes = new List<Ts4MlodMesh>(meshCount);

        for (var i = 0; i < meshCount; i++)
        {
            var expectedSize = reader.ReadUInt32();
            var meshStart = reader.Position;
            var name = reader.ReadUInt32();
            var materialRef = new Ts4ChunkReference(reader.ReadUInt32());
            var vrtfRef = new Ts4ChunkReference(reader.ReadUInt32());
            var vbufRef = new Ts4ChunkReference(reader.ReadUInt32());
            var ibufRef = new Ts4ChunkReference(reader.ReadUInt32());
            var primitiveAndFlags = reader.ReadUInt32();
            var primitiveType = (Ts4PrimitiveType)(primitiveAndFlags & 0xFF);
            var flags = primitiveAndFlags >> 8;
            var streamOffset = reader.ReadUInt32();
            _ = reader.ReadInt32();
            var startIndex = reader.ReadInt32();
            _ = reader.ReadInt32();
            var vertexCount = reader.ReadInt32();
            var primitiveCount = reader.ReadInt32();
            reader.Skip(24);
            var skinRef = new Ts4ChunkReference(reader.ReadUInt32());
            var jointCount = reader.ReadInt32();
            reader.Skip(jointCount * 4);
            _ = reader.ReadUInt32();
            var geometryStateCount = reader.ReadInt32();
            reader.Skip(geometryStateCount * 20);

            if (version > 0x00000201)
            {
                reader.Skip(20);
            }

            if (version > 0x00000203)
            {
                reader.Skip(4);
            }

            var consumed = reader.Position - meshStart;
            if (consumed < expectedSize)
            {
                reader.Skip((int)(expectedSize - consumed));
            }

            meshes.Add(new Ts4MlodMesh(
                name,
                materialRef,
                vrtfRef,
                vbufRef,
                ibufRef,
                skinRef,
                primitiveType,
                flags,
                streamOffset,
                startIndex,
                vertexCount,
                primitiveCount,
                jointCount));
        }

        return new Ts4MlodChunk { Meshes = meshes };
    }
}

internal readonly record struct Ts4MlodMesh(
    uint Name,
    Ts4ChunkReference MaterialReference,
    Ts4ChunkReference VertexFormatReference,
    Ts4ChunkReference VertexBufferReference,
    Ts4ChunkReference IndexBufferReference,
    Ts4ChunkReference SkinReference,
    Ts4PrimitiveType PrimitiveType,
    uint Flags,
    uint StreamOffset,
    int StartIndex,
    int VertexCount,
    int PrimitiveCount,
    int JointCount)
{
    public bool IsShadowCaster => (Flags & 0x10) != 0;
    public bool IsSkinned => !SkinReference.IsNull || JointCount > 0;
}

internal enum Ts4PrimitiveType : uint
{
    PointList = 0,
    LineList = 1,
    LineStrip = 2,
    TriangleList = 3
}
