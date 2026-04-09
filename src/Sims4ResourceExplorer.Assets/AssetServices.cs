using System.Text;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.Assets;

public sealed class ExplicitAssetGraphBuilder : IAssetGraphBuilder
{
    private readonly IResourceCatalogService resourceCatalogService;

    public ExplicitAssetGraphBuilder(IResourceCatalogService resourceCatalogService)
    {
        this.resourceCatalogService = resourceCatalogService;
    }

    public IReadOnlyList<AssetSummary> BuildAssetSummaries(PackageScanResult packageScan)
    {
        var resources = packageScan.Resources;
        var sameInstanceLookup = resources
            .GroupBy(static resource => resource.Key.FullInstance)
            .ToDictionary(static group => group.Key, static group => group.ToArray());

        var summaries = new List<AssetSummary>();
        summaries.AddRange(BuildBuildBuySummaries(resources, sameInstanceLookup));
        summaries.AddRange(BuildCasSummaries(resources, sameInstanceLookup));
        return summaries;
    }

    public async Task<AssetGraph> BuildAssetGraphAsync(AssetSummary summary, IReadOnlyList<ResourceMetadata> packageResources, CancellationToken cancellationToken)
    {
        return summary.AssetKind switch
        {
            AssetKind.BuildBuy => BuildBuildBuyGraph(summary, packageResources),
            AssetKind.Cas => await BuildCasGraphAsync(summary, packageResources, cancellationToken).ConfigureAwait(false),
            _ => new AssetGraph(summary, [], [$"Unsupported asset kind: {summary.AssetKind}."])
        };
    }

    private static IEnumerable<AssetSummary> BuildBuildBuySummaries(
        IReadOnlyList<ResourceMetadata> resources,
        IReadOnlyDictionary<ulong, ResourceMetadata[]> sameInstanceLookup)
    {
        foreach (var model in resources.Where(static resource => resource.Key.TypeName == "Model"))
        {
            sameInstanceLookup.TryGetValue(model.Key.FullInstance, out var related);
            related ??= [];

            var objectDefinition = related.FirstOrDefault(static resource => resource.Key.TypeName == "ObjectDefinition");
            var objectCatalog = related.FirstOrDefault(static resource => resource.Key.TypeName == "ObjectCatalog");
            var modelLods = related.Where(static resource => resource.Key.TypeName == "ModelLOD").ToArray();
            var textures = related.Where(static resource => IsTextureType(resource.Key.TypeName)).ToArray();
            var thumbnail = related.FirstOrDefault(static resource => resource.Key.TypeName == "BuyBuildThumbnail")
                ?? textures.FirstOrDefault();

            var displayName = objectDefinition?.Name
                ?? objectCatalog?.Name
                ?? model.Name
                ?? $"Build/Buy Model {model.Key.FullInstance:X16}";

            var diagnostics = new List<string>();
            if (objectCatalog is null && objectDefinition is null)
            {
                diagnostics.Add("Catalog metadata could not be matched by exact instance; using a model-rooted Build/Buy asset identity.");
            }

            if (modelLods.Length == 0)
            {
                diagnostics.Add("No exact-instance ModelLOD resources were indexed for this model.");
            }

            if (textures.Length == 0)
            {
                diagnostics.Add("No exact-instance texture resources were indexed for this model.");
            }

            yield return new AssetSummary(
                Guid.NewGuid(),
                model.DataSourceId,
                model.SourceKind,
                AssetKind.BuildBuy,
                displayName,
                "Build/Buy",
                model.PackagePath,
                model.Key,
                thumbnail?.Key.FullTgi,
                1,
                related.Length - 1,
                string.Join(" ", diagnostics));
        }
    }

    private static IEnumerable<AssetSummary> BuildCasSummaries(
        IReadOnlyList<ResourceMetadata> resources,
        IReadOnlyDictionary<ulong, ResourceMetadata[]> sameInstanceLookup)
    {
        foreach (var casPart in resources.Where(static resource => resource.Key.TypeName == "CASPart"))
        {
            sameInstanceLookup.TryGetValue(casPart.Key.FullInstance, out var related);
            related ??= [];

            var thumbnail = related.FirstOrDefault(static resource => resource.Key.TypeName is "CASPartThumbnail" or "BodyPartThumbnail")
                ?? related.FirstOrDefault(static resource => IsTextureType(resource.Key.TypeName));
            var displayName = casPart.Name ?? $"CAS Part {casPart.Key.FullInstance:X16}";

            var diagnostics = new List<string>();
            if (thumbnail is null)
            {
                diagnostics.Add("No exact-instance CAS thumbnail was indexed for this CAS part.");
            }

            yield return new AssetSummary(
                Guid.NewGuid(),
                casPart.DataSourceId,
                casPart.SourceKind,
                AssetKind.Cas,
                displayName,
                "CAS",
                casPart.PackagePath,
                casPart.Key,
                thumbnail?.Key.FullTgi,
                1,
                related.Length - 1,
                string.Join(" ", diagnostics));
        }
    }

    private static AssetGraph BuildBuildBuyGraph(AssetSummary summary, IReadOnlyList<ResourceMetadata> packageResources)
    {
        var root = packageResources.FirstOrDefault(resource => resource.Key.FullTgi == summary.RootKey.FullTgi);
        var linked = root is null
            ? []
            : packageResources
                .Where(resource => resource.Key.FullTgi != root.Key.FullTgi && resource.Key.FullInstance == root.Key.FullInstance)
                .OrderBy(static resource => BuildBuyLinkOrder(resource.Key.TypeName))
                .ThenBy(static resource => resource.Key.TypeName, StringComparer.Ordinal)
                .ToArray();

        var diagnostics = new List<string>();
        if (root is null)
        {
            diagnostics.Add("The model root resource is not available in the currently loaded package metadata.");
            return new AssetGraph(summary, linked, diagnostics);
        }

        var identityResources = linked.Where(static resource => resource.Key.TypeName is "ObjectCatalog" or "ObjectDefinition").ToArray();
        var hasObjectIdentity = identityResources.Length > 0;
        var modelLods = linked.Where(static resource => resource.Key.TypeName == "ModelLOD").ToArray();
        var textures = linked.Where(static resource => IsTextureType(resource.Key.TypeName)).ToArray();
        var materialResources = linked.Where(static resource => resource.Key.TypeName is "MaterialDefinition").ToArray();
        var materialManifest = textures.Length == 0
            ? []
            : new[]
            {
                new MaterialManifestEntry(
                    "ApproximateMaterial",
                    null,
                    false,
                    null,
                    "Material/shader chunks are not fully parsed yet. Diffuse preview/export falls back to exact-instance texture candidates.",
                    textures.Select(static texture => new MaterialTextureEntry(
                        texture.Key.TypeName,
                        $"{texture.Key.TypeName}_{texture.Key.Type:X8}_{texture.Key.Group:X8}_{texture.Key.FullInstance:X16}.png",
                        texture.Key,
                        texture.PackagePath)).ToArray())
            };

        if (!hasObjectIdentity)
        {
            diagnostics.Add("Exact-instance ObjectCatalog/ObjectDefinition metadata was not found for this model; the asset remains usable through its model-rooted identity.");
        }

        if (modelLods.Length == 0)
        {
            diagnostics.Add("No exact-instance ModelLOD resources were indexed for this model. The scene builder may still succeed if the model contains an embedded MLOD.");
        }

        if (materialResources.Length == 0)
        {
            diagnostics.Add("No exact-instance MaterialDefinition resources were indexed for this model. Scene reconstruction will rely on embedded chunks or texture approximation.");
        }

        return new AssetGraph(
            summary,
            linked,
            diagnostics,
            new BuildBuyAssetGraph(
                root,
                identityResources,
                modelLods,
                materialResources,
                textures,
                [],
                materialManifest,
                diagnostics,
                true,
                "Static Build/Buy furniture/decor objects with a model root, triangle-list MLOD geometry, no skinning/animation path, and package-local texture candidates."));
    }

    private async Task<AssetGraph> BuildCasGraphAsync(
        AssetSummary summary,
        IReadOnlyList<ResourceMetadata> packageResources,
        CancellationToken cancellationToken)
    {
        var root = packageResources.FirstOrDefault(resource => resource.Key.FullTgi == summary.RootKey.FullTgi);
        if (root is null)
        {
            return new AssetGraph(summary, [], ["The CASPart root resource is not available in the currently loaded package metadata."]);
        }

        var diagnostics = new List<string>();
        Ts4CasPart casPart;
        try
        {
            var bytes = await resourceCatalogService.GetResourceBytesAsync(root.PackagePath, root.Key, raw: false, cancellationToken).ConfigureAwait(false);
            casPart = Ts4CasPart.Parse(bytes);
        }
        catch (Exception ex)
        {
            diagnostics.Add($"CASPart parsing failed: {ex.Message}");
            return new AssetGraph(summary, [], diagnostics);
        }

        var sameInstance = packageResources
            .Where(resource => resource.Key.FullInstance == root.Key.FullInstance && resource.Key.FullTgi != root.Key.FullTgi)
            .OrderBy(static resource => resource.Key.TypeName, StringComparer.Ordinal)
            .ToArray();

        var identityResources = new List<ResourceMetadata> { root };
        identityResources.AddRange(sameInstance.Where(static resource => resource.Key.TypeName is "CASPartThumbnail" or "BodyPartThumbnail"));

        var geometryResources = new List<ResourceMetadata>();
        var rigResources = sameInstance.Where(static resource => resource.Key.TypeName == "Rig").ToList();
        var textureResources = new List<ResourceMetadata>();
        var materialResources = new List<ResourceMetadata>();
        var selectedLodLabel = default(string);

        foreach (var slot in ResolveCasTextures(casPart, packageResources, diagnostics))
        {
            textureResources.Add(slot);
        }

        foreach (var lod in casPart.Lods.OrderBy(static lod => lod.Level))
        {
            var resolvedGeometry = lod.KeyIndices
                .Select(index => ResolveTgi(packageResources, casPart.TgiList, index))
                .Where(static resource => resource is not null)
                .ToArray();

            var directGeometry = resolvedGeometry
                .Where(static resource => string.Equals(resource!.Key.TypeName, "Geometry", StringComparison.Ordinal))
                .Cast<ResourceMetadata>()
                .ToArray();

            if (directGeometry.Length == 0)
            {
                diagnostics.Add($"LOD {lod.Level} did not expose a direct Geometry reference in the CASPart TGI list.");
                continue;
            }

            geometryResources.AddRange(directGeometry);
            selectedLodLabel = $"LOD {lod.Level}";
            break;
        }

        geometryResources = geometryResources
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToList();

        if (geometryResources.Count == 0)
        {
            diagnostics.Add("No direct Geometry resource could be resolved from the CASPart LOD references. GEOM list containers and cross-package CAS graphs remain unsupported in this pass.");
        }

        var category = MapCasBodyType(casPart.BodyType);
        if (category is null)
        {
            diagnostics.Add($"Body type {casPart.BodyType} is outside the supported CAS subset.");
        }

        if (!casPart.IsAdultOrYoungAdult)
        {
            diagnostics.Add("This CAS part is outside the supported adult/young-adult age subset.");
        }

        if (!casPart.IsMasculineOrFeminineHumanPresentation)
        {
            diagnostics.Add("This CAS part does not expose the expected adult human male/female age-gender flags for the supported subset.");
        }

        if (rigResources.Count == 0)
        {
            diagnostics.Add("No exact-instance Rig resource was indexed for this CAS part. Bone hierarchy names may fall back to GEOM bone hashes.");
        }

        var explicitTextures = textureResources
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
        var materialManifest = explicitTextures.Length == 0
            ? []
            : new[]
            {
                new MaterialManifestEntry(
                    "ApproximateCasMaterial",
                    null,
                    explicitTextures.Any(static texture => texture.Key.TypeName.Contains("PNG", StringComparison.OrdinalIgnoreCase)),
                    "portable-approximation",
                    "CAS material/shader semantics are approximated to a portable texture bundle. Explicit CASPart texture references are used where available.",
                    explicitTextures.Select(texture => new MaterialTextureEntry(
                        GuessCasTextureSlot(texture.Key.TypeName),
                        $"{GuessCasTextureSlot(texture.Key.TypeName)}_{texture.Key.Type:X8}_{texture.Key.Group:X8}_{texture.Key.FullInstance:X16}.png",
                        texture.Key,
                        texture.PackagePath)).ToArray())
            };

        var isSupported = category is not null
            && casPart.IsAdultOrYoungAdult
            && casPart.IsMasculineOrFeminineHumanPresentation
            && geometryResources.Count > 0;

        var linkedResources = identityResources
            .Concat(geometryResources)
            .Concat(rigResources)
            .Concat(materialResources)
            .Concat(explicitTextures)
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static resource => CasLinkOrder(resource.Key.TypeName))
            .ThenBy(static resource => resource.Key.TypeName, StringComparer.Ordinal)
            .ToArray();

        return new AssetGraph(
            summary,
            linkedResources,
            diagnostics,
            null,
            new CasAssetGraph(
                root,
                geometryResources.FirstOrDefault(),
                rigResources.FirstOrDefault(),
                identityResources,
                geometryResources,
                rigResources,
                materialResources,
                explicitTextures,
                materialManifest,
                category,
                casPart.SwatchSummary,
                selectedLodLabel,
                isSupported,
                "Adult/young-adult human CAS parts in the hair, full body, top, bottom, or shoes categories when the CASPart exposes a direct skinned Geometry LOD in the same package."));
    }

    private IEnumerable<ResourceMetadata> ResolveCasTextures(
        Ts4CasPart casPart,
        IReadOnlyList<ResourceMetadata> packageResources,
        List<string> diagnostics)
    {
        foreach (var index in casPart.TextureKeyIndices)
        {
            var texture = ResolveTgi(packageResources, casPart.TgiList, index);
            if (texture is null)
            {
                diagnostics.Add($"CASPart texture key index {index} did not resolve to a package-local resource.");
                continue;
            }

            if (IsTextureType(texture.Key.TypeName))
            {
                yield return texture;
            }
        }
    }

    private static ResourceMetadata? ResolveTgi(IReadOnlyList<ResourceMetadata> packageResources, IReadOnlyList<ResourceKeyRecord> tgiList, byte index)
    {
        if (index >= tgiList.Count)
        {
            return null;
        }

        var key = tgiList[index];
        return packageResources.FirstOrDefault(resource =>
            resource.Key.Type == key.Type &&
            resource.Key.Group == key.Group &&
            resource.Key.FullInstance == key.FullInstance);
    }

    private static string? MapCasBodyType(int bodyType) => bodyType switch
    {
        2 => "Hair",
        5 => "Full Body",
        6 => "Top",
        7 => "Bottom",
        8 => "Shoes",
        _ => null
    };

    private static string GuessCasTextureSlot(string typeName) => typeName switch
    {
        "CASPartThumbnail" or "BodyPartThumbnail" => "thumbnail",
        "PNGImage" or "PNGImage2" => "diffuse",
        "DSTImage" or "LRLEImage" or "RLE2Image" or "RLESImage" => "compressed",
        _ => "texture"
    };

    private static int BuildBuyLinkOrder(string typeName) => typeName switch
    {
        "ObjectCatalog" => 0,
        "ObjectDefinition" => 1,
        "ModelLOD" => 2,
        "MaterialDefinition" => 3,
        "PNGImage" or "PNGImage2" or "DSTImage" or "LRLEImage" or "RLE2Image" or "RLESImage" => 4,
        _ => 10
    };

    private static int CasLinkOrder(string typeName) => typeName switch
    {
        "CASPart" => 0,
        "CASPartThumbnail" or "BodyPartThumbnail" => 1,
        "Geometry" => 2,
        "Rig" => 3,
        "PNGImage" or "PNGImage2" or "DSTImage" or "LRLEImage" or "RLE2Image" or "RLESImage" => 4,
        _ => 10
    };

    private static bool IsTextureType(string typeName) => typeName is
        "BuyBuildThumbnail" or
        "CASPartThumbnail" or
        "BodyPartThumbnail" or
        "PNGImage" or
        "PNGImage2" or
        "DSTImage" or
        "LRLEImage" or
        "RLE2Image" or
        "RLESImage";
}

internal sealed class Ts4CasPart
{
    private const uint YoungAdultFlag = 0x00000010;
    private const uint AdultFlag = 0x00000020;
    private const uint MaleFlag = 0x00001000;
    private const uint FemaleFlag = 0x00002000;

    public required int BodyType { get; init; }
    public required uint AgeGenderFlags { get; init; }
    public required IReadOnlyList<ResourceKeyRecord> TgiList { get; init; }
    public required IReadOnlyList<Ts4CasLod> Lods { get; init; }
    public required IReadOnlyList<byte> TextureKeyIndices { get; init; }
    public required string? SwatchSummary { get; init; }

    public bool IsAdultOrYoungAdult => (AgeGenderFlags & (YoungAdultFlag | AdultFlag)) != 0;
    public bool IsMasculineOrFeminineHumanPresentation => (AgeGenderFlags & (MaleFlag | FemaleFlag)) != 0;

    public static Ts4CasPart Parse(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        var version = reader.ReadUInt32();
        var tgiOffset = reader.ReadUInt32() + 8;
        _ = reader.ReadUInt32();
        _ = ReadBigEndianUnicodeString(reader);
        _ = reader.ReadSingle();
        _ = reader.ReadUInt16();
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadByte();
        _ = reader.ReadUInt64();
        _ = reader.ReadUInt32();

        var flagCount = reader.ReadUInt32();
        stream.Position += flagCount * 4L;

        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadByte();
        var bodyType = reader.ReadInt32();
        _ = reader.ReadInt32();
        var ageGender = reader.ReadUInt32();
        _ = reader.ReadByte();
        _ = reader.ReadByte();

        var swatchCount = reader.ReadByte();
        var swatches = new List<string>(swatchCount);
        for (var index = 0; index < swatchCount; index++)
        {
            swatches.Add($"#{reader.ReadInt32():X8}");
        }

        _ = reader.ReadByte();
        var variantThumbnailKey = reader.ReadByte();
        if (version >= 0x1C)
        {
            _ = reader.ReadUInt64();
        }

        var nakedKey = reader.ReadByte();
        var parentKey = reader.ReadByte();
        _ = reader.ReadInt32();
        var lodCount = reader.ReadByte();
        var lods = new List<Ts4CasLod>(lodCount);
        for (var lodIndex = 0; lodIndex < lodCount; lodIndex++)
        {
            var level = reader.ReadByte();
            _ = reader.ReadUInt32();

            var lodAssetCount = reader.ReadByte();
            stream.Position += lodAssetCount * 12L;

            var keyCount = reader.ReadByte();
            var keyIndices = new byte[keyCount];
            for (var keyIndex = 0; keyIndex < keyCount; keyIndex++)
            {
                keyIndices[keyIndex] = reader.ReadByte();
            }

            lods.Add(new Ts4CasLod(level, keyIndices));
        }

        var slotKeyCount = reader.ReadByte();
        stream.Position += slotKeyCount;

        var diffuseShadowKey = reader.ReadByte();
        var shadowKey = reader.ReadByte();
        _ = reader.ReadByte();
        _ = reader.ReadByte();
        _ = reader.ReadByte();
        var normalMapKey = reader.ReadByte();
        var specularMapKey = reader.ReadByte();
        if (version >= 0x1B)
        {
            _ = reader.ReadUInt32();
        }

        stream.Position = tgiOffset;
        var tgiCount = reader.ReadByte();
        var tgiList = new List<ResourceKeyRecord>(tgiCount);
        for (var index = 0; index < tgiCount; index++)
        {
            var type = reader.ReadUInt32();
            var group = reader.ReadUInt32();
            var instance = reader.ReadUInt64();
            tgiList.Add(new ResourceKeyRecord(
                type,
                group,
                instance,
                GuessTypeName(type)));
        }

        return new Ts4CasPart
        {
            BodyType = bodyType,
            AgeGenderFlags = ageGender,
            TgiList = tgiList,
            Lods = lods,
            TextureKeyIndices = BuildTextureKeyIndices(diffuseShadowKey, shadowKey, normalMapKey, specularMapKey, variantThumbnailKey, nakedKey, parentKey),
            SwatchSummary = swatches.Count == 0 ? null : string.Join(", ", swatches.Take(4)) + (swatches.Count > 4 ? $" (+{swatches.Count - 4} more)" : string.Empty)
        };
    }

    private static IReadOnlyList<byte> BuildTextureKeyIndices(params byte[] candidates) =>
        candidates
            .Distinct()
            .Where(static value => value < byte.MaxValue)
            .ToArray();

    private static string GuessTypeName(uint type) => type switch
    {
        0x034AEECB => "CASPart",
        0x015A1849 => "Geometry",
        0x8EAF13DE => "Rig",
        0x3C1AF1F2 => "CASPartThumbnail",
        0x5B282D45 => "BodyPartThumbnail",
        0x00B2D882 => "PNGImage",
        0x2F7D0004 => "PNGImage2",
        0x2BC04EDF => "DSTImage",
        0x0988C7E1 => "LRLEImage",
        0x3453CF95 => "RLE2Image",
        0x1B4D2A70 => "RLESImage",
        _ => $"0x{type:X8}"
    };

    private static string ReadBigEndianUnicodeString(BinaryReader reader)
    {
        var length = Read7BitEncodedInt(reader);
        return Encoding.BigEndianUnicode.GetString(reader.ReadBytes(length));
    }

    private static int Read7BitEncodedInt(BinaryReader reader)
    {
        var count = 0;
        var shift = 0;
        byte currentByte;
        do
        {
            currentByte = reader.ReadByte();
            count |= (currentByte & 0x7F) << shift;
            shift += 7;
        }
        while ((currentByte & 0x80) != 0);

        return count;
    }
}

internal readonly record struct Ts4CasLod(byte Level, IReadOnlyList<byte> KeyIndices);
