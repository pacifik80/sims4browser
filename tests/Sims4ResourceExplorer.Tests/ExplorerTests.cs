using LlamaLogic.Packages;
using System.Reflection;
using System.Text;
using Sims4ResourceExplorer.Assets;
using Sims4ResourceExplorer.Audio;
using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Export;
using Sims4ResourceExplorer.Indexing;
using Sims4ResourceExplorer.Packages;
using Sims4ResourceExplorer.Preview;
namespace Sims4ResourceExplorer.Tests;

public sealed class ExplorerTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), "Sims4ResourceExplorer.Tests", Guid.NewGuid().ToString("N"));

    public ExplorerTests()
    {
        Directory.CreateDirectory(tempRoot);
    }

    [Fact]
    public async Task FileSystemPackageScanner_FindsPackagesRecursively()
    {
        var gameRoot = Path.Combine(tempRoot, "Game");
        Directory.CreateDirectory(Path.Combine(gameRoot, "Nested"));
        File.WriteAllText(Path.Combine(gameRoot, "base.package"), string.Empty);
        File.WriteAllText(Path.Combine(gameRoot, "Nested", "dlc.package"), string.Empty);

        var scanner = new FileSystemPackageScanner();
        var results = new List<DiscoveredPackage>();
        await foreach (var package in scanner.DiscoverPackagesAsync(
            [new DataSourceDefinition(Guid.NewGuid(), "Game", gameRoot, SourceKind.Game)],
            CancellationToken.None))
        {
            results.Add(package);
        }

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task ResourceCatalogService_ScansSyntheticPackage()
    {
        var packagePath = await CreateSyntheticPackageAsync();
        var source = new DataSourceDefinition(Guid.NewGuid(), "Game", Path.GetDirectoryName(packagePath)!, SourceKind.Game);
        var service = new LlamaResourceCatalogService();

        var result = await service.ScanPackageAsync(source, packagePath, progress: null, CancellationToken.None);

        Assert.NotEmpty(result.Resources);
        Assert.Contains(result.Resources, resource => resource.Key.TypeName == nameof(ResourceType.CombinedTuning));
        Assert.Contains(result.Resources, resource => resource.Key.TypeName == nameof(ResourceType.PNGImage));
    }

    [Fact]
    public void AssetGraphBuilder_CreatesBuildBuySummaries()
    {
        var builder = new ExplicitAssetGraphBuilder(new FakeResourceCatalogService(new Dictionary<string, byte[]>(), new Dictionary<string, string?>()));
        var packagePath = Path.Combine(tempRoot, "synthetic.package");
        var sourceId = Guid.NewGuid();
        var resources = new[]
        {
            CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectCatalog", 1),
            CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectDefinition", 1, "Chair"),
            CreateResource(sourceId, packagePath, SourceKind.Game, "Model", 1, "Chair"),
            CreateResource(sourceId, packagePath, SourceKind.Game, "ModelLOD", 1),
        };

        var summaries = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, resources, []));

        var summary = Assert.Single(summaries.Where(summary => summary.AssetKind == AssetKind.BuildBuy));
        Assert.Equal("Geometry", summary.SupportLabel);
        Assert.Contains("geometry", summary.SupportNotes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AssetGraphBuilder_MarksBuildBuySummaryPartialWhenExactLodCoverageIsMissing()
    {
        var builder = new ExplicitAssetGraphBuilder(new FakeResourceCatalogService(new Dictionary<string, byte[]>(), new Dictionary<string, string?>()));
        var packagePath = Path.Combine(tempRoot, "partial.package");
        var sourceId = Guid.NewGuid();
        var resources = new[]
        {
            CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectCatalog", 1),
            CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectDefinition", 1, "Chair"),
            CreateResource(sourceId, packagePath, SourceKind.Game, "Model", 1, "Chair"),
        };

        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, resources, []))
            .Single(asset => asset.AssetKind == AssetKind.BuildBuy);

        Assert.Equal("Metadata", summary.SupportLabel);
        Assert.Contains("no exact geometry candidate", summary.SupportNotes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildsExplicitBuildBuyGraphForSupportedSubset()
    {
        var builder = new ExplicitAssetGraphBuilder(new FakeResourceCatalogService(new Dictionary<string, byte[]>(), new Dictionary<string, string?>()));
        var packagePath = Path.Combine(tempRoot, "supported.package");
        var sourceId = Guid.NewGuid();
        var model = CreateResource(sourceId, packagePath, SourceKind.Game, "Model", 1, "Chair");
        var resources = new[]
        {
            CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectCatalog", 1),
            CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectDefinition", 1, "Chair"),
            model,
            CreateResource(sourceId, packagePath, SourceKind.Game, "ModelLOD", 1),
            CreateResource(sourceId, packagePath, SourceKind.Game, "PNGImage", 1),
            CreateResource(sourceId, packagePath, SourceKind.Game, "MaterialDefinition", 1)
        };

        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, resources, [])).Single();
        var graph = await builder.BuildAssetGraphAsync(summary, resources, CancellationToken.None);

        Assert.NotNull(graph.BuildBuyGraph);
        Assert.True(graph.BuildBuyGraph!.IsSupported);
        Assert.Equal(model.Key.FullTgi, graph.BuildBuyGraph.ModelResource.Key.FullTgi);
        Assert.Equal(2, graph.BuildBuyGraph.IdentityResources.Count);
        Assert.Single(graph.BuildBuyGraph.ModelLodResources);
        Assert.Single(graph.BuildBuyGraph.MaterialResources);
        Assert.Single(graph.BuildBuyGraph.TextureResources);
        Assert.NotEmpty(graph.BuildBuyGraph.Materials);
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildsExplicitCasGraphForSupportedSubset()
    {
        var packagePath = Path.Combine(tempRoot, "cas-supported.package");
        var sourceId = Guid.NewGuid();
        var casPart = CreateResource(sourceId, packagePath, SourceKind.Game, "CASPart", 1, "Short Hair");
        var geometry = CreateResource(sourceId, packagePath, SourceKind.Game, "Geometry", 1, "HairGeom");
        var rig = CreateResource(sourceId, packagePath, SourceKind.Game, "Rig", 1, "HumanRig");
        var texture = CreateResource(sourceId, packagePath, SourceKind.Game, "PNGImage", 1);
        var thumbnail = CreateResource(sourceId, packagePath, SourceKind.Game, "CASPartThumbnail", 1);

        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [casPart.Key.FullTgi] = CreateSyntheticCasPartBytes(geometry.Key, thumbnail.Key, texture.Key),
                [geometry.Key.FullTgi] = CreateSyntheticSkinnedGeometryBytes(),
                [rig.Key.FullTgi] = CreateSyntheticRigBytes(),
                [texture.Key.FullTgi] = TestAssets.OnePixelPng,
                [thumbnail.Key.FullTgi] = TestAssets.OnePixelPng
            },
            new Dictionary<string, string?>());
        var builder = new ExplicitAssetGraphBuilder(catalog);
        var resources = new[] { casPart, geometry, rig, texture, thumbnail };

        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, resources, []))
            .Single(asset => asset.AssetKind == AssetKind.Cas);
        var graph = await builder.BuildAssetGraphAsync(summary, resources, CancellationToken.None);

        Assert.True(graph.CasGraph is not null, string.Join(Environment.NewLine, graph.Diagnostics));
        Assert.True(graph.CasGraph!.IsSupported, string.Join(Environment.NewLine, graph.Diagnostics));
        Assert.Equal("Hair", graph.CasGraph.Category);
        Assert.Equal(geometry.Key.FullTgi, graph.CasGraph.GeometryResource?.Key.FullTgi);
        Assert.Single(graph.CasGraph.GeometryResources);
        Assert.Single(graph.CasGraph.RigResources);
        Assert.NotEmpty(graph.CasGraph.TextureResources);
    }

    [Fact]
    public async Task GeometrySceneBuildService_BuildsSkinnedSceneFromSyntheticGeometry()
    {
        var packagePath = Path.Combine(tempRoot, "cas-scene.package");
        var cacheService = new TestCacheService(tempRoot);
        var store = new SqliteIndexStore(cacheService);
        await store.InitializeAsync(CancellationToken.None);

        var source = new DataSourceDefinition(Guid.NewGuid(), "Fixture", tempRoot, SourceKind.Game);
        var geometry = CreateResource(source.Id, packagePath, SourceKind.Game, "Geometry", 1, "HairGeom");
        var rig = CreateResource(source.Id, packagePath, SourceKind.Game, "Rig", 1, "HumanRig");
        var texture = CreateResource(source.Id, packagePath, SourceKind.Game, "PNGImage", 1);
        await store.ReplacePackageAsync(
            new PackageScanResult(source.Id, SourceKind.Game, packagePath, 10, DateTimeOffset.UtcNow, [geometry, rig, texture], []),
            [],
            CancellationToken.None);

        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [geometry.Key.FullTgi] = CreateSyntheticSkinnedGeometryBytes(),
                [rig.Key.FullTgi] = CreateSyntheticRigBytes(),
                [texture.Key.FullTgi] = TestAssets.OnePixelPng
            },
            new Dictionary<string, string?>());

        var builder = new BuildBuySceneBuildService(catalog, store);
        var scene = await builder.BuildSceneAsync(geometry, CancellationToken.None);

        Assert.True(scene.Success, string.Join(Environment.NewLine, scene.Diagnostics));
        Assert.NotNull(scene.Scene);
        Assert.Single(scene.Scene!.Meshes);
        Assert.Equal(2, scene.Scene.Bones.Count);
        Assert.NotEmpty(scene.Scene.Meshes[0].SkinWeights);
    }

    [Fact]
    public async Task BuildBuySceneBuildService_ReturnsDiagnosticForMalformedModelLod()
    {
        var packagePath = Path.Combine(tempRoot, "broken.package");
        var cacheService = new TestCacheService(tempRoot);
        var store = new SqliteIndexStore(cacheService);
        await store.InitializeAsync(CancellationToken.None);

        var source = new DataSourceDefinition(Guid.NewGuid(), "Fixture", tempRoot, SourceKind.Game);
        var modelLod = CreateResource(source.Id, packagePath, SourceKind.Game, "ModelLOD", 1, "BrokenLod");
        await store.ReplacePackageAsync(
            new PackageScanResult(source.Id, SourceKind.Game, packagePath, 10, DateTimeOffset.UtcNow, [modelLod], []),
            [],
            CancellationToken.None);

        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [modelLod.Key.FullTgi] = [0x52, 0x43, 0x4F, 0x4C, 0x01]
            },
            new Dictionary<string, string?>());

        var builder = new BuildBuySceneBuildService(catalog, store);
        var scene = await builder.BuildSceneAsync(modelLod, CancellationToken.None);

        Assert.False(scene.Success);
        Assert.Null(scene.Scene);
        Assert.Contains(scene.Diagnostics, message => message.Contains("could not be parsed cleanly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildBuySceneBuildService_NormalizesSharedMeshWindowIndices()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var chunkReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ChunkReference");
        var primitiveType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4PrimitiveType");
        var meshType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MlodMesh");
        var meshWindowType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MeshWindow");
        var ibufType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4IbufChunk");

        var nullChunkReference = Activator.CreateInstance(
            chunkReferenceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0u],
            culture: null)!;

        var mesh = Activator.CreateInstance(
            meshType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                0x12345678u,
                nullChunkReference,
                nullChunkReference,
                nullChunkReference,
                nullChunkReference,
                nullChunkReference,
                Enum.ToObject(primitiveType, 3u),
                0u,
                0u,
                5,
                0,
                7,
                4,
                2,
                0,
                nullChunkReference,
                0
            ],
            culture: null)!;

        var meshWindow = meshWindowType.GetMethod("From", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [mesh])!;
        Assert.Equal(12, (int)meshWindowType.GetProperty("VertexReadBase")!.GetValue(meshWindow)!);
        Assert.Equal(7, (int)meshWindowType.GetProperty("IndexNormalizeBase")!.GetValue(meshWindow)!);

        var ibuf = Activator.CreateInstance(
            ibufType,
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [1u, 4, CreateSyntheticIbufPayload([7u, 8u, 9u, 10u])],
            culture: null)!;
        var indices = (IReadOnlyList<uint>)ibufType.GetMethod("ReadIndices", BindingFlags.Public | BindingFlags.Instance)!.Invoke(ibuf, [0, 4, 7, 4])!;

        Assert.Equal([0u, 1u, 2u, 3u], indices);
    }

    [Fact]
    public void BuildBuySceneBuildService_UsesCompactDeltaLow16WhenDirectWindowIsOutOfRange()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var ibufType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4IbufChunk");

        var ibuf = Activator.CreateInstance(
            ibufType,
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [1u, 4, CreateCompactDeltaPayload([32767, 32767, 2, 1, 1])],
            culture: null)!;

        var indices = (IReadOnlyList<uint>)ibufType.GetMethod("ReadIndices", BindingFlags.Public | BindingFlags.Instance)!.Invoke(ibuf, [2, 3, 0, 3])!;
        Assert.Equal([0u, 1u, 2u], indices);
    }

    [Fact]
    public void ShaderSemantics_ResolvesTextureSlotNamesFromShaderProfiles()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var shaderParameterType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile");
        var shaderCategoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var resourceKeyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ResourceKey");
        var semanticsType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ShaderSemantics");

        var samplerCategory = Enum.Parse(shaderCategoryType, "Sampler");
        var parameter = Activator.CreateInstance(
            shaderParameterType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0xAD983A7Cu, "samplerDiffuseMap", 0x0102C601u, 0x00004000u, samplerCategory],
            culture: null)!;
        var parameters = Array.CreateInstance(shaderParameterType, 1);
        parameters.SetValue(parameter, 0);
        var profile = Activator.CreateInstance(
            shaderProfileType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x0CB82EB8u, "SeasonalFoliage", parameters],
            culture: null)!;
        var key = Activator.CreateInstance(
            resourceKeyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x00B2D882u, 0u, 0x0297B6B3762A64EAul],
            culture: null)!;
        var reference = Activator.CreateInstance(
            textureReferenceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: ["texture_0", key, 0xAD983A7Cu],
            culture: null)!;

        var resolvedSlot = (string)semanticsType
            .GetMethod("ResolveTextureSlotName", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [reference, profile])!;

        Assert.Equal("diffuse", resolvedSlot);
    }

    [Fact]
    public void ShaderSemantics_TreatsSamplerProfileParametersAsTextureBindings()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderParameterType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile");
        var shaderCategoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var semanticsType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ShaderSemantics");

        var samplerCategory = Enum.Parse(shaderCategoryType, "Sampler");
        var parameter = Activator.CreateInstance(
            shaderParameterType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x637DAA05u, "samplerCASMedatorGridTexture", 0x0102C601u, 0x00004000u, samplerCategory],
            culture: null)!;

        var result = (bool)semanticsType
            .GetMethod("IsLikelyTextureParameter", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [1u, 4u, parameter])!;

        Assert.True(result);
    }

    [Fact]
    public void ShaderSemantics_InterpretsUsesUv1ScalarAsUvChannelSwitch()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderParameterType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile");
        var shaderCategoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var semanticsType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ShaderSemantics");

        var boolLikeCategory = Enum.Parse(shaderCategoryType, "BoolLike");
        var parameter = Activator.CreateInstance(
            shaderParameterType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x23F2B29Fu, "DirtOverlayUsesUV1", 0x01034001u, 0x00003000u, boolLikeCategory],
            culture: null)!;
        var mapping = Activator.CreateInstance(
            uvMappingType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0, 1f, 1f, 0f, 0f],
            culture: null)!;
        var args = new object?[] { parameter, 1f, mapping, null };

        var interpreted = (bool)semanticsType
            .GetMethod("TryInterpretDiffuseUvMappingScalar", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, args)!;

        Assert.True(interpreted);
        Assert.NotNull(args[3]);
        Assert.Equal(1, (int)uvMappingType.GetProperty("UvChannel")!.GetValue(args[3])!);
    }

    [Fact]
    public void ShaderSemantics_InterpretsUvMappingVectorAsScaleAndOffset()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderParameterType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile");
        var shaderCategoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var semanticsType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ShaderSemantics");

        var uvCategory = Enum.Parse(shaderCategoryType, "UvMapping");
        var parameter = Activator.CreateInstance(
            shaderParameterType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x420520E9u, "uvMapping", 0x01374601u, 0x80004000u, uvCategory],
            culture: null)!;
        var mapping = Activator.CreateInstance(
            uvMappingType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0, 1f, 1f, 0f, 0f],
            culture: null)!;
        var args = new object?[] { parameter, new[] { 0.5f, 0.25f, 0.125f, 0.75f }, mapping, null };

        var interpreted = (bool)semanticsType
            .GetMethod(
                "TryInterpretDiffuseUvMappingVector",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [shaderParameterType, typeof(float[]), uvMappingType, uvMappingType.MakeByRefType()],
                modifiers: null)!
            .Invoke(null, args)!;

        Assert.True(interpreted);
        Assert.NotNull(args[3]);
        Assert.Equal(0.5f, (float)uvMappingType.GetProperty("UvScaleU")!.GetValue(args[3])!);
        Assert.Equal(0.25f, (float)uvMappingType.GetProperty("UvScaleV")!.GetValue(args[3])!);
        Assert.Equal(0.125f, (float)uvMappingType.GetProperty("UvOffsetU")!.GetValue(args[3])!);
        Assert.Equal(0.75f, (float)uvMappingType.GetProperty("UvOffsetV")!.GetValue(args[3])!);
    }

    [Fact]
    public void MatdDecoder_ClassifiesPackedVectorPayloadAsPackedUInt32()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var serviceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MatdChunk");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");
        var decodeMethod = serviceType.GetMethod(
            "DecodeMatdValue",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(uint), typeof(uint), typeof(int), typeof(byte[])],
            modifiers: null)!;

        var bytes = new byte[16];
        BitConverter.GetBytes(0x6BBDC905u).CopyTo(bytes, 0);
        BitConverter.GetBytes(0x00B2D882u).CopyTo(bytes, 4);
        BitConverter.GetBytes(0x00000000u).CopyTo(bytes, 8);
        BitConverter.GetBytes(0x38000100u).CopyTo(bytes, 12);

        var decoded = decodeMethod.Invoke(null, [1u, 4u, 0, bytes])!;
        var representation = decoded.GetType().GetProperty("Representation")!.GetValue(decoded)!;
        var summary = (string?)decoded.GetType().GetProperty("Summary")!.GetValue(decoded);

        Assert.Equal(Enum.Parse(representationType, "PackedUInt32"), representation);
        Assert.Contains("0x6BBDC905", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void MatdDecoder_ClassifiesResourceKeyPayloadAsResourceKey()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var serviceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MatdChunk");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");
        var decodeMethod = serviceType.GetMethod(
            "DecodeMatdValue",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(uint), typeof(uint), typeof(int), typeof(byte[])],
            modifiers: null)!;

        var bytes = new byte[16];
        BitConverter.GetBytes(0x0297B6B3762A64EAul).CopyTo(bytes, 0);
        BitConverter.GetBytes(0x00B2D882u).CopyTo(bytes, 8);
        BitConverter.GetBytes(0u).CopyTo(bytes, 12);

        var decoded = decodeMethod.Invoke(null, [2u, 1u, 0, bytes])!;
        var representation = decoded.GetType().GetProperty("Representation")!.GetValue(decoded)!;
        var summary = (string?)decoded.GetType().GetProperty("Summary")!.GetValue(decoded);

        Assert.Equal(Enum.Parse(representationType, "ResourceKey"), representation);
        Assert.Contains("00B2D882", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void MaterialDecoder_NormalizesFamilyAndFlagsPackedUvMapping()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var shaderParameterType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile");
        var shaderCategoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");

        var uvCategory = Enum.Parse(shaderCategoryType, "UvMapping");
        var packedRepresentation = Enum.Parse(representationType, "PackedUInt32");
        var parameter = Activator.CreateInstance(
            shaderParameterType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x420520E9u, "uvMapping", 0x01374601u, 0x80004000u, uvCategory],
            culture: null)!;
        var parameters = Array.CreateInstance(shaderParameterType, 1);
        parameters.SetValue(parameter, 0);
        var profile = Activator.CreateInstance(
            shaderProfileType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x0CB82EB8u, "SeasonalFoliage-Variant", parameters],
            culture: null)!;
        var property = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x420520E9u, "uvMapping", 1u, 1u, 4u, uvCategory, packedRepresentation, "packed32=[0x1]", null, new uint[] { 1, 2, 3, 4 }, null],
            culture: null)!;
        var properties = Array.CreateInstance(materialPropertyType, 1);
        properties.SetValue(property, 0);
        var mapping = Activator.CreateInstance(
            uvMappingType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0, 1f, 1f, 0f, 0f],
            culture: null)!;
        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: ["Material_Test", "Shader_Test", properties, Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference"), 0), mapping],
            culture: null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var family = (string)decoded.GetType().GetProperty("ShaderFamilyName")!.GetValue(decoded)!;
        var notes = (System.Collections.IEnumerable)decoded.GetType().GetProperty("Notes")!.GetValue(decoded)!;
        var noteText = string.Join(" | ", notes.Cast<object>());

        Assert.Equal("SeasonalFoliage", family);
        Assert.Contains("packed data", noteText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MaterialDecoder_UsesFoliageFamilyAsAlphaCutoutHint()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");

        var properties = Array.CreateInstance(materialPropertyType, 0);
        var textures = Array.CreateInstance(textureReferenceType, 0);
        var mapping = Activator.CreateInstance(
            uvMappingType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0, 1f, 1f, 0f, 0f],
            culture: null)!;
        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: ["Material_Test", "Shader_Test", properties, textures, mapping],
            culture: null)!;
        var profile = Activator.CreateInstance(
            shaderProfileType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x0CB82EB8u, "SeasonalFoliage", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)],
            culture: null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var suggestsAlpha = (bool)decoded.GetType().GetProperty("SuggestsAlphaCutout")!.GetValue(decoded)!;
        var alphaHint = (string?)decoded.GetType().GetProperty("AlphaModeHint")!.GetValue(decoded);

        Assert.True(suggestsAlpha);
        Assert.Equal("alpha-test-or-blend", alphaHint);
    }

    [Fact]
    public void MaterialDecoder_SelectsSeasonalFoliageStrategy()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var familyKindType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialFamilyKind");

        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "Material_Test",
                "Shader_Test",
                Array.CreateInstance(materialPropertyType, 0),
                Array.CreateInstance(textureReferenceType, 0),
                Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 1f, 1f, 0f, 0f], null)!
            ],
            culture: null)!;
        var profile = Activator.CreateInstance(
            shaderProfileType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x0CB82EB8u, "SeasonalFoliage", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)],
            culture: null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var strategyName = (string)decoded.GetType().GetProperty("StrategyName")!.GetValue(decoded)!;
        var familyKind = decoded.GetType().GetProperty("FamilyKind")!.GetValue(decoded)!;

        Assert.Equal("SeasonalFoliageMaterialDecodeStrategy", strategyName);
        Assert.Equal(Enum.Parse(familyKindType, "SeasonalFoliage"), familyKind);
    }

    [Fact]
    public void MaterialDecoder_SelectsColorMap7Strategy()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var familyKindType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialFamilyKind");

        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "Material_Test",
                "Shader_Test",
                Array.CreateInstance(materialPropertyType, 0),
                Array.CreateInstance(textureReferenceType, 0),
                Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 1f, 1f, 0f, 0f], null)!
            ],
            culture: null)!;
        var profile = Activator.CreateInstance(
            shaderProfileType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0xB9105A6Du, "colorMap7", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)],
            culture: null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var strategyName = (string)decoded.GetType().GetProperty("StrategyName")!.GetValue(decoded)!;
        var familyKind = decoded.GetType().GetProperty("FamilyKind")!.GetValue(decoded)!;

        Assert.Equal("ColorMap7MaterialDecodeStrategy", strategyName);
        Assert.Equal(Enum.Parse(familyKindType, "ColorMap7"), familyKind);
    }

    [Fact]
    public void MaterialDecoder_ColorMap7AppliesLegacyUvSelector()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var shaderCategoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");

        var scalarCategory = Enum.Parse(shaderCategoryType, "Scalar");
        var scalarRepresentation = Enum.Parse(representationType, "Scalar");
        var property = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0xB95C43EBu, "samplerEnvCubeMap", 1u, 1u, 1u, scalarCategory, scalarRepresentation, "scalar=1", new[] { 1f }, null, null],
            culture: null)!;
        var properties = Array.CreateInstance(materialPropertyType, 1);
        properties.SetValue(property, 0);
        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "Material_Test",
                "Shader_Test",
                properties,
                Array.CreateInstance(textureReferenceType, 0),
                Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 1f, 1f, 0f, 0f], null)!
            ],
            culture: null)!;
        var profile = Activator.CreateInstance(
            shaderProfileType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0xB9105A6Du, "colorMap7", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)],
            culture: null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var mapping = decoded.GetType().GetProperty("DiffuseUvMapping")!.GetValue(decoded)!;
        var notes = (System.Collections.IEnumerable)decoded.GetType().GetProperty("Notes")!.GetValue(decoded)!;
        var noteText = string.Join(" | ", notes.Cast<object>());

        Assert.Equal(1, (int)uvMappingType.GetProperty("UvChannel")!.GetValue(mapping)!);
        Assert.Contains("legacy UV channel selector", noteText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CasLogicalAsset_ExportsBundleFromSyntheticFixture()
    {
        var packagePath = Path.Combine(tempRoot, "cas-export.package");
        var cacheService = new TestCacheService(tempRoot);
        var store = new SqliteIndexStore(cacheService);
        await store.InitializeAsync(CancellationToken.None);

        var source = new DataSourceDefinition(Guid.NewGuid(), "Fixture", tempRoot, SourceKind.Game);
        var casPart = CreateResource(source.Id, packagePath, SourceKind.Game, "CASPart", 1, "Short Hair");
        var geometry = CreateResource(source.Id, packagePath, SourceKind.Game, "Geometry", 1, "HairGeom");
        var rig = CreateResource(source.Id, packagePath, SourceKind.Game, "Rig", 1, "HumanRig");
        var texture = CreateResource(source.Id, packagePath, SourceKind.Game, "PNGImage", 1);
        var thumbnail = CreateResource(source.Id, packagePath, SourceKind.Game, "CASPartThumbnail", 1);
        var scan = new PackageScanResult(source.Id, SourceKind.Game, packagePath, 10, DateTimeOffset.UtcNow, [casPart, geometry, rig, texture, thumbnail], []);

        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [casPart.Key.FullTgi] = CreateSyntheticCasPartBytes(geometry.Key, thumbnail.Key, texture.Key),
                [geometry.Key.FullTgi] = CreateSyntheticSkinnedGeometryBytes(),
                [rig.Key.FullTgi] = CreateSyntheticRigBytes(),
                [texture.Key.FullTgi] = TestAssets.OnePixelPng,
                [thumbnail.Key.FullTgi] = TestAssets.OnePixelPng
            },
            new Dictionary<string, string?>());
        var graphBuilder = new ExplicitAssetGraphBuilder(catalog);
        var assets = graphBuilder.BuildAssetSummaries(scan);
        await store.ReplacePackageAsync(scan, assets, CancellationToken.None);

        var asset = assets.Single(result => result.AssetKind == AssetKind.Cas);
        var packageResources = await store.GetPackageResourcesAsync(asset.PackagePath, CancellationToken.None);
        var assetGraph = await graphBuilder.BuildAssetGraphAsync(asset, packageResources, CancellationToken.None);
        Assert.True(assetGraph.CasGraph is not null, string.Join(Environment.NewLine, assetGraph.Diagnostics));
        Assert.True(assetGraph.CasGraph!.IsSupported, string.Join(Environment.NewLine, assetGraph.Diagnostics));
        Assert.NotNull(assetGraph.CasGraph.GeometryResource);

        var sceneBuilder = new BuildBuySceneBuildService(catalog, store);
        var scene = await sceneBuilder.BuildSceneAsync(assetGraph.CasGraph.GeometryResource!, CancellationToken.None);
        Assert.True(scene.Success, string.Join(Environment.NewLine, scene.Diagnostics));
        Assert.NotNull(scene.Scene);

        var outputRoot = Path.Combine(tempRoot, "cas-export-out");
        var exportService = new AssimpFbxExportService();
        var assetSlug = "fixture_cas";
        var export = await exportService.ExportAsync(
            new SceneExportRequest(
                assetSlug,
                scene.Scene!,
                outputRoot,
                BuildSourceResources(assetGraph, assetGraph.CasGraph.GeometryResource!),
                scene.Scene!.Materials.SelectMany(static material => material.Textures).ToArray(),
                scene.Diagnostics,
                assetGraph.CasGraph.Materials),
            CancellationToken.None);

        Assert.True(export.Success);
        Assert.True(File.Exists(Path.Combine(outputRoot, assetSlug, $"{assetSlug}.fbx")));
        Assert.True(File.Exists(Path.Combine(outputRoot, assetSlug, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(outputRoot, assetSlug, "material_manifest.json")));
        Assert.True(File.Exists(Path.Combine(outputRoot, assetSlug, "metadata.json")));
        Assert.NotEmpty(Directory.EnumerateFiles(Path.Combine(outputRoot, assetSlug, "Textures"), "*.png"));
    }

    [Fact]
    public async Task TextureDecodeService_DecodesPngPayload()
    {
        var resource = CreateResource(Guid.NewGuid(), "fake.package", SourceKind.Game, "PNGImage", 1);
        var service = new BasicTextureDecodeService(new FakeResourceCatalogService(
            new Dictionary<string, byte[]> { [resource.Key.FullTgi] = TestAssets.OnePixelPng },
            new Dictionary<string, string?>()));

        var result = await service.DecodeAsync(resource, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.PngBytes);
        Assert.Equal(1, result.Width);
        Assert.Equal(1, result.Height);
    }

    [Fact]
    public async Task SqliteIndexStore_PersistsAndQueriesResourcesAndAssets()
    {
        var cacheService = new TestCacheService(tempRoot);
        var store = new SqliteIndexStore(cacheService);
        await store.InitializeAsync(CancellationToken.None);

        var source = new DataSourceDefinition(Guid.NewGuid(), "Game", tempRoot, SourceKind.Game);
        await store.UpsertDataSourcesAsync([source], CancellationToken.None);

        var resource = CreateResource(source.Id, "test.package", SourceKind.Game, "ObjectCatalog", 1);
        var asset = new AssetSummary(Guid.NewGuid(), source.Id, SourceKind.Game, AssetKind.BuildBuy, "Chair", "Object", "test.package", resource.Key, null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, true, true, true));
        await store.ReplacePackageAsync(new PackageScanResult(source.Id, SourceKind.Game, "test.package", 10, DateTimeOffset.UtcNow, [resource], []), [asset], CancellationToken.None);

        var resources = await store.QueryResourcesAsync(
            new RawResourceBrowserQuery(
                new SourceScope(),
                "objectcatalog",
                RawResourceDomain.All,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                false,
                false,
                false,
                ResourceLinkFilter.Any,
                RawResourceSort.TypeName,
                0,
                10),
            CancellationToken.None);
        var assets = await store.QueryAssetsAsync(
            new AssetBrowserQuery(
                new SourceScope(),
                "chair",
                AssetBrowserDomain.BuildBuy,
                string.Empty,
                string.Empty,
                string.Empty,
                false,
                false,
                AssetBrowserSort.Name,
                0,
                10,
                new AssetCapabilityFilter()),
            CancellationToken.None);

        Assert.Single(resources.Items);
        Assert.Single(assets.Items);
        Assert.Equal("Geometry+Textures", assets.Items[0].SupportLabel);
    }

    [Fact]
    public void PreviewPresentationState_SwitchesBetweenSceneImageAndDiagnostics()
    {
        var resource = CreateResource(Guid.NewGuid(), "fake.package", SourceKind.Game, "Model", 1);
        var sceneContent = new ScenePreviewContent(resource, new CanonicalScene("test", [], [], [], new Bounds3D(0, 0, 0, 1, 1, 1)), "ok");
        var partialSceneContent = new ScenePreviewContent(resource, new CanonicalScene("test", [], [], [], new Bounds3D(0, 0, 0, 1, 1, 1)), "ok", SceneBuildStatus.Partial);
        var imageContent = new TexturePreviewContent(resource, TestAssets.OnePixelPng, 1, 1, "PNG", 1, "decoded");
        var diagnosticContent = new UnsupportedPreviewContent(resource, "unsupported");

        Assert.Equal(PreviewSurfaceMode.Scene, PreviewPresentationState.FromPreviewContent(sceneContent).SurfaceMode);
        Assert.Equal("3D Preview", PreviewPresentationState.FromPreviewContent(sceneContent).SurfaceTitle);
        Assert.Equal("3D Preview (Partial)", PreviewPresentationState.FromPreviewContent(partialSceneContent).SurfaceTitle);
        Assert.Equal(PreviewSurfaceMode.Image, PreviewPresentationState.FromPreviewContent(imageContent).SurfaceMode);
        Assert.Equal(PreviewSurfaceMode.Diagnostics, PreviewPresentationState.FromPreviewContent(diagnosticContent).SurfaceMode);
    }

    [Fact]
    public void PreviewInteractionPolicy_OnlyEnablesAssetExportWhenSceneIsReady()
    {
        var sourceId = Guid.NewGuid();
        var root = CreateResource(sourceId, "fake.package", SourceKind.Game, "Model", 1);
        var summary = new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.BuildBuy, "Chair", "Build/Buy", "fake.package", root.Key, null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, true, true, true));
        var graph = new AssetGraph(summary, [], [], new BuildBuyAssetGraph(root, [], [], [], [], [], [], [], [], true, "subset"));
        var scene = new CanonicalScene("chair", [], [], [], new Bounds3D(0, 0, 0, 1, 1, 1));

        Assert.False(PreviewInteractionPolicy.CanExportSelectedAsset(summary, graph, null, null));
        Assert.False(PreviewInteractionPolicy.CanExportSelectedAsset(summary, graph, root, null));
        Assert.True(PreviewInteractionPolicy.CanExportSelectedAsset(summary, graph, root, scene));
    }

    [Fact]
    public void AssetDetailsFormatter_ReportsBuildBuySceneSuccessAndFailureExplicitly()
    {
        var sourceId = Guid.NewGuid();
        var root = CreateResource(sourceId, "fake.package", SourceKind.Game, "Model", 1);
        var summary = new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.BuildBuy, "Chair", "Build/Buy", "fake.package", root.Key, null, 1, 1, "No exact-instance ModelLOD resources were indexed for this model.", new AssetCapabilitySnapshot(true, false, false, false));
        var graph = new AssetGraph(summary, [], ["No exact-instance ModelLOD resources were indexed for this model."], new BuildBuyAssetGraph(root, [], [], [], [], [], [], [], ["No exact-instance ModelLOD resources were indexed for this model."], true, "subset"));
        var successfulScene = new ScenePreviewContent(root, new CanonicalScene("chair", [], [], [], new Bounds3D(0, 0, 0, 1, 1, 1)), "Selected LOD root: MLOD0", SceneBuildStatus.SceneReady);
        var failedScene = new ScenePreviewContent(root, null, "No triangle meshes could be reconstructed.", SceneBuildStatus.Unsupported);

        var successDetails = AssetDetailsFormatter.BuildAssetDetails(summary, graph, root, successfulScene);
        var failureDetails = AssetDetailsFormatter.BuildAssetDetails(summary, graph, root, failedScene);

        Assert.Contains("Support Status: Metadata", successDetails);
        Assert.Contains("Scene Reconstruction: SceneReady", successDetails);
        Assert.Contains("Scene Reconstruction: Failed", failureDetails);
        Assert.Contains("No triangle meshes could be reconstructed.", failureDetails);
    }

    [Fact]
    public async Task FbxExportService_WritesBundleManifestAndTextures()
    {
        var outputRoot = Path.Combine(tempRoot, "exports");
        var scene = new CanonicalScene(
            "TestScene",
            [
                new CanonicalMesh(
                    "Triangle",
                    [0f, 0f, 0f, 0f, 1f, 0f, 1f, 0f, 0f],
                    [0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f],
                    [],
                    [0f, 0f, 0f, 1f, 1f, 0f],
                    [0f, 0f, 0f, 1f, 1f, 0f],
                    [],
                    0,
                    [0, 1, 2],
                    0,
                    []),
            ],
            [new CanonicalMaterial("Default", [new CanonicalTexture("Diffuse", "diffuse.png", TestAssets.OnePixelPng)])],
            [],
            new Bounds3D(0, 0, 0, 1, 1, 0));

        var sourceResource = CreateResource(Guid.NewGuid(), "test.package", SourceKind.Game, "Geometry", 1);
        var service = new AssimpFbxExportService();
        var materialManifest = new[]
        {
            new MaterialManifestEntry(
                "Default",
                "PortablePhong",
                false,
                "opaque",
                "Synthetic test material",
                [new MaterialTextureEntry("Diffuse", "diffuse.png", sourceResource.Key, sourceResource.PackagePath)])
        };

        var result = await service.ExportAsync(
            new SceneExportRequest("triangle_asset", scene, outputRoot, [sourceResource], scene.Materials.SelectMany(static material => material.Textures).ToArray(), [], materialManifest),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(File.Exists(result.OutputPath!));
        Assert.True(File.Exists(Path.Combine(outputRoot, "triangle_asset", "manifest.json")));
        Assert.True(File.Exists(Path.Combine(outputRoot, "triangle_asset", "material_manifest.json")));
        Assert.True(File.Exists(Path.Combine(outputRoot, "triangle_asset", "metadata.json")));
        Assert.True(File.Exists(Path.Combine(outputRoot, "triangle_asset", "Textures", "diffuse.png")));
    }

    [Fact]
    public async Task BuildBuySceneBuildService_BuildsSceneFromLocalFixture_WhenConfigured()
    {
        var fixturePackage = Environment.GetEnvironmentVariable("SIMS4_BUILD_BUY_FIXTURE_PACKAGE");
        var fixtureTgi = Environment.GetEnvironmentVariable("SIMS4_BUILD_BUY_FIXTURE_TGI");
        if (string.IsNullOrWhiteSpace(fixturePackage) || string.IsNullOrWhiteSpace(fixtureTgi) || !File.Exists(fixturePackage))
        {
            return;
        }

        var cacheService = new TestCacheService(tempRoot);
        var store = new SqliteIndexStore(cacheService);
        await store.InitializeAsync(CancellationToken.None);

        var source = new DataSourceDefinition(Guid.NewGuid(), "Fixture", Path.GetDirectoryName(fixturePackage)!, SourceKind.Game);
        var catalog = new LlamaResourceCatalogService();
        var scan = await catalog.ScanPackageAsync(source, fixturePackage, progress: null, CancellationToken.None);
        await store.ReplacePackageAsync(scan, [], CancellationToken.None);

        var resource = scan.Resources.FirstOrDefault(resource => string.Equals(resource.Key.FullTgi, fixtureTgi, StringComparison.OrdinalIgnoreCase));
        if (resource is null)
        {
            return;
        }

        var builder = new BuildBuySceneBuildService(catalog, store);
        var scene = await builder.BuildSceneAsync(resource, CancellationToken.None);

        Assert.True(scene.Success, string.Join(Environment.NewLine, scene.Diagnostics));
        Assert.NotNull(scene.Scene);
        Assert.NotEmpty(scene.Scene!.Meshes);
    }

    [Fact]
    public async Task BuildBuyLogicalAsset_ExportsBundleFromLocalFixture_WhenConfigured()
    {
        var fixturePackage = Environment.GetEnvironmentVariable("SIMS4_BUILD_BUY_FIXTURE_PACKAGE");
        var fixtureTgi = Environment.GetEnvironmentVariable("SIMS4_BUILD_BUY_FIXTURE_TGI");
        if (string.IsNullOrWhiteSpace(fixturePackage) || string.IsNullOrWhiteSpace(fixtureTgi) || !File.Exists(fixturePackage))
        {
            return;
        }

        var cacheService = new TestCacheService(tempRoot);
        var store = new SqliteIndexStore(cacheService);
        await store.InitializeAsync(CancellationToken.None);

        var source = new DataSourceDefinition(Guid.NewGuid(), "Fixture", Path.GetDirectoryName(fixturePackage)!, SourceKind.Game);
        var catalog = new LlamaResourceCatalogService();
        var scan = await catalog.ScanPackageAsync(source, fixturePackage, progress: null, CancellationToken.None);
        var graphBuilder = new ExplicitAssetGraphBuilder(catalog);
        var assets = graphBuilder.BuildAssetSummaries(scan);
        await store.ReplacePackageAsync(scan, assets, CancellationToken.None);

        var fixtureResource = scan.Resources.FirstOrDefault(resource => string.Equals(resource.Key.FullTgi, fixtureTgi, StringComparison.OrdinalIgnoreCase));
        if (fixtureResource is null)
        {
            return;
        }

        var asset = assets.FirstOrDefault(asset => asset.RootKey.FullInstance == fixtureResource.Key.FullInstance);
        if (asset is null)
        {
            return;
        }

        var packageResources = await store.GetPackageResourcesAsync(asset.PackagePath, CancellationToken.None);
        var assetGraph = await graphBuilder.BuildAssetGraphAsync(asset, packageResources, CancellationToken.None);
        Assert.NotNull(assetGraph.BuildBuyGraph);
        Assert.True(assetGraph.BuildBuyGraph!.IsSupported, string.Join(Environment.NewLine, assetGraph.Diagnostics));

        var sceneBuilder = new BuildBuySceneBuildService(catalog, store);
        var scene = await sceneBuilder.BuildSceneAsync(assetGraph.BuildBuyGraph.ModelResource, CancellationToken.None);
        Assert.True(scene.Success, string.Join(Environment.NewLine, scene.Diagnostics));
        Assert.NotNull(scene.Scene);

        var outputRoot = Path.Combine(tempRoot, "fixture-export");
        var exportService = new AssimpFbxExportService();
        var assetSlug = "fixture_buildbuy";
        var export = await exportService.ExportAsync(
            new SceneExportRequest(
                assetSlug,
                scene.Scene!,
                outputRoot,
                BuildSourceResources(assetGraph, assetGraph.BuildBuyGraph.ModelResource),
                scene.Scene!.Materials.SelectMany(static material => material.Textures).ToArray(),
                scene.Diagnostics,
                assetGraph.BuildBuyGraph.Materials),
            CancellationToken.None);

        Assert.True(export.Success);
        Assert.True(File.Exists(Path.Combine(outputRoot, assetSlug, $"{assetSlug}.fbx")));
        Assert.True(File.Exists(Path.Combine(outputRoot, assetSlug, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(outputRoot, assetSlug, "material_manifest.json")));
        Assert.True(File.Exists(Path.Combine(outputRoot, assetSlug, "metadata.json")));
        var texturesPath = Path.Combine(outputRoot, assetSlug, "Textures");
        Assert.True(Directory.Exists(texturesPath));
        Assert.NotEmpty(Directory.EnumerateFiles(texturesPath, "*.png"));
    }

    [Fact]
    public async Task BuildBuyLogicalAsset_PreviewServiceReturnsSceneFromLocalFixture_WhenConfigured()
    {
        var fixturePackage = Environment.GetEnvironmentVariable("SIMS4_BUILD_BUY_FIXTURE_PACKAGE");
        var fixtureTgi = Environment.GetEnvironmentVariable("SIMS4_BUILD_BUY_FIXTURE_TGI");
        if (string.IsNullOrWhiteSpace(fixturePackage) || string.IsNullOrWhiteSpace(fixtureTgi) || !File.Exists(fixturePackage))
        {
            return;
        }

        var cacheService = new TestCacheService(tempRoot);
        var store = new SqliteIndexStore(cacheService);
        await store.InitializeAsync(CancellationToken.None);

        var source = new DataSourceDefinition(Guid.NewGuid(), "Fixture", Path.GetDirectoryName(fixturePackage)!, SourceKind.Game);
        var catalog = new LlamaResourceCatalogService();
        var scan = await catalog.ScanPackageAsync(source, fixturePackage, progress: null, CancellationToken.None);
        var graphBuilder = new ExplicitAssetGraphBuilder(catalog);
        var assets = graphBuilder.BuildAssetSummaries(scan);
        await store.ReplacePackageAsync(scan, assets, CancellationToken.None);

        var fixtureResource = scan.Resources.FirstOrDefault(resource => string.Equals(resource.Key.FullTgi, fixtureTgi, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The configured Build/Buy fixture TGI was not found in the local package.");
        var asset = assets.FirstOrDefault(asset => asset.RootKey.FullInstance == fixtureResource.Key.FullInstance)
            ?? throw new InvalidOperationException("No Build/Buy logical asset matched the configured local fixture.");
        var packageResources = await store.GetPackageResourcesAsync(asset.PackagePath, CancellationToken.None);
        var assetGraph = await graphBuilder.BuildAssetGraphAsync(asset, packageResources, CancellationToken.None);
        Assert.NotNull(assetGraph.BuildBuyGraph);

        var previewService = new ResourcePreviewService(
            catalog,
            new BasicTextureDecodeService(catalog),
            new BuildBuySceneBuildService(catalog, store),
            new BasicAudioDecodeService(catalog));

        var preview = await previewService.CreatePreviewAsync(assetGraph.BuildBuyGraph!.ModelResource, CancellationToken.None);

        var scenePreview = Assert.IsType<ScenePreviewContent>(preview.Content);
        Assert.NotNull(scenePreview.Scene);
        Assert.NotEmpty(scenePreview.Scene!.Meshes);
    }

    [Fact]
    public async Task AudioDecodeService_RecognizesWavePayload()
    {
        var resource = CreateResource(Guid.NewGuid(), "fake.package", SourceKind.Game, "AudioConfiguration", 1);
        var service = new BasicAudioDecodeService(new FakeResourceCatalogService(
            new Dictionary<string, byte[]> { [resource.Key.FullTgi] = TestAssets.WaveBytes },
            new Dictionary<string, string?>()));

        var result = await service.DecodeAsync(resource, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.WavBytes);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private async Task<string> CreateSyntheticPackageAsync()
    {
        var path = Path.Combine(tempRoot, "synthetic.package");
        var package = new DataBasePackedFile();
        package.Set(new ResourceKey(ResourceType.CombinedTuning, 0x00000001, 0x0000000000000001), "<I c=\"Example\" />", CompressionMode.ForceOff);
        package.Set(new ResourceKey(ResourceType.PNGImage, 0x00000001, 0x0000000000000002), TestAssets.OnePixelPng, CompressionMode.ForceOff);
        await package.SaveAsAsync(path, ResourceKeyOrder.Preserve, CancellationToken.None);
        return path;
    }

    private static ResourceMetadata CreateResource(Guid dataSourceId, string packagePath, SourceKind sourceKind, string typeName, uint group, string? name = null)
    {
        var type = (uint)Enum.Parse<ResourceType>(typeName);
        return new ResourceMetadata(
            Guid.NewGuid(),
            dataSourceId,
            sourceKind,
            packagePath,
            new ResourceKeyRecord(type, group, 1, typeName),
            name,
            1L,
            1L,
            false,
            typeName.Contains("PNG", StringComparison.OrdinalIgnoreCase) ? PreviewKind.Texture :
            typeName is "Geometry" or "Rig" ? PreviewKind.Scene :
            typeName.Contains("Audio", StringComparison.OrdinalIgnoreCase) ? PreviewKind.Audio :
            PreviewKind.Hex,
            true,
            true,
            string.Empty,
            string.Empty);
    }

    private static IReadOnlyList<ResourceMetadata> BuildSourceResources(AssetGraph graph, ResourceMetadata sceneRoot)
    {
        var resources = new List<ResourceMetadata> { sceneRoot };
        if (graph.BuildBuyGraph is not null)
        {
            resources.AddRange(graph.BuildBuyGraph.IdentityResources);
            resources.AddRange(graph.BuildBuyGraph.ModelLodResources);
            resources.AddRange(graph.BuildBuyGraph.MaterialResources);
            resources.AddRange(graph.BuildBuyGraph.TextureResources);
        }
        else if (graph.CasGraph is not null)
        {
            resources.AddRange(graph.CasGraph.IdentityResources);
            resources.AddRange(graph.CasGraph.GeometryResources);
            resources.AddRange(graph.CasGraph.RigResources);
            resources.AddRange(graph.CasGraph.MaterialResources);
            resources.AddRange(graph.CasGraph.TextureResources);
        }

        return resources
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }

    private static byte[] CreateSyntheticCasPartBytes(ResourceKeyRecord geometryKey, ResourceKeyRecord thumbnailKey, ResourceKeyRecord textureKey)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.BigEndianUnicode, leaveOpen: true);

        writer.Write(0x0000001Cu);
        var tgiOffsetPosition = stream.Position;
        writer.Write(0u);
        writer.Write(0u);
        writer.Write("Short Hair");
        writer.Write(0f);
        writer.Write((ushort)0);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write((byte)0);
        writer.Write(0ul);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write((byte)0);
        writer.Write(2);
        writer.Write(0);
        writer.Write(0x00003010u);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)1);
        writer.Write(unchecked((int)0xFFFFCCAA));
        writer.Write((byte)0);
        writer.Write((byte)2);
        writer.Write(0ul);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write(0);

        var lodListPosition = stream.Position;
        writer.Write((byte)1);
        writer.Write((byte)0);
        writer.Write(0u);
        writer.Write((byte)1);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write((byte)1);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)1);
        writer.Write((byte)2);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)2);
        writer.Write((byte)1);
        writer.Write((byte)0);
        writer.Write((byte)0);

        var tgiOffset = (uint)(stream.Position - 8);
        var current = stream.Position;
        stream.Position = tgiOffsetPosition;
        writer.Write(tgiOffset);
        stream.Position = current;

        writer.Write((byte)3);
        WriteResourceKey(writer, geometryKey);
        WriteResourceKey(writer, thumbnailKey);
        WriteResourceKey(writer, textureKey);
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] CreateSyntheticSkinnedGeometryBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write("GEOM"u8.ToArray());
        writer.Write(0x0000000Cu);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(3);
        writer.Write(5);
        WriteGeomFormat(writer, 0x01);
        WriteGeomFormat(writer, 0x02);
        WriteGeomFormat(writer, 0x03);
        WriteGeomFormat(writer, 0x04);
        WriteGeomFormat(writer, 0x05);

        WriteGeomVertex(writer, [0f, 0f, 0f], [0f, 0f, 1f], [0f, 0f], [0, 1, 0, 0], [255, 0, 0, 0]);
        WriteGeomVertex(writer, [0f, 1f, 0f], [0f, 0f, 1f], [0f, 1f], [0, 1, 0, 0], [128, 127, 0, 0]);
        WriteGeomVertex(writer, [1f, 0f, 0f], [0f, 0f, 1f], [1f, 0f], [1, 0, 0, 0], [255, 0, 0, 0]);

        writer.Write(1);
        writer.Write((byte)2);
        writer.Write(3);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)2);
        writer.Write(0);
        writer.Write(0);
        writer.Write(2);
        writer.Write(0x11111111u);
        writer.Write(0x22222222u);
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] CreateSyntheticRigBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(4u);
        writer.Write(2u);
        writer.Write(2);

        WriteRigBone(writer, "root", -1, 0x11111111u, new float[] { 0f, 0f, 0f });
        WriteRigBone(writer, "tip", 0, 0x22222222u, new float[] { 0f, 1f, 0f });
        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteGeomFormat(BinaryWriter writer, uint usage)
    {
        writer.Write(usage);
        writer.Write(usage == 0x04 ? 2u : 1u);
        writer.Write((byte)(usage switch
        {
            0x01 or 0x02 => 12,
            0x03 => 8,
            0x04 or 0x05 => 4,
            _ => 0
        }));
    }

    private static void WriteGeomVertex(BinaryWriter writer, float[] position, float[] normal, float[] uv, byte[] blendIndices, byte[] blendWeights)
    {
        foreach (var value in position)
        {
            writer.Write(value);
        }

        foreach (var value in normal)
        {
            writer.Write(value);
        }

        foreach (var value in uv)
        {
            writer.Write(value);
        }

        writer.Write(blendIndices);
        writer.Write(blendWeights);
    }

    private static void WriteRigBone(BinaryWriter writer, string name, int parentIndex, uint hash, float[] position)
    {
        foreach (var value in position)
        {
            writer.Write(value);
        }

        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(1f);
        writer.Write(1f);
        writer.Write(1f);
        writer.Write(1f);
        writer.Write(name.Length);
        writer.Write(name.ToCharArray());
        writer.Write(-1);
        writer.Write(parentIndex);
        writer.Write(hash);
        writer.Write(0u);
    }

    private static void WriteResourceKey(BinaryWriter writer, ResourceKeyRecord key)
    {
        writer.Write(key.Type);
        writer.Write(key.Group);
        writer.Write(key.FullInstance);
    }

    private sealed class TestCacheService : ICacheService
    {
        public TestCacheService(string root)
        {
            AppRoot = Path.Combine(root, "app");
            CacheRoot = Path.Combine(AppRoot, "cache");
            ExportRoot = Path.Combine(AppRoot, "exports");
            DatabasePath = Path.Combine(CacheRoot, "index.sqlite");
        }

        public string AppRoot { get; }
        public string CacheRoot { get; }
        public string ExportRoot { get; }
        public string DatabasePath { get; }

        public void EnsureCreated()
        {
            Directory.CreateDirectory(AppRoot);
            Directory.CreateDirectory(CacheRoot);
            Directory.CreateDirectory(ExportRoot);
        }
    }

    private sealed class FakeResourceCatalogService : IResourceCatalogService
    {
        private readonly IReadOnlyDictionary<string, byte[]> bytesByTgi;
        private readonly IReadOnlyDictionary<string, string?> textByTgi;

        public FakeResourceCatalogService(IReadOnlyDictionary<string, byte[]> bytesByTgi, IReadOnlyDictionary<string, string?> textByTgi)
        {
            this.bytesByTgi = bytesByTgi;
            this.textByTgi = textByTgi;
        }

        public Task<PackageScanResult> ScanPackageAsync(DataSourceDefinition source, string packagePath, IProgress<PackageScanProgress>? progress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ResourceMetadata> EnrichResourceAsync(ResourceMetadata resource, CancellationToken cancellationToken) =>
            Task.FromResult(resource);

        public Task<byte[]> GetResourceBytesAsync(string packagePath, ResourceKeyRecord key, bool raw, CancellationToken cancellationToken) =>
            Task.FromResult(bytesByTgi[key.FullTgi]);

        public Task<string?> GetTextAsync(string packagePath, ResourceKeyRecord key, CancellationToken cancellationToken) =>
            Task.FromResult(textByTgi.TryGetValue(key.FullTgi, out var value) ? value : null);

        public Task<byte[]?> GetTexturePngAsync(string packagePath, ResourceKeyRecord key, CancellationToken cancellationToken) =>
            Task.FromResult<byte[]?>(bytesByTgi[key.FullTgi]);
    }

    private sealed class FakeSceneBuildService(SceneBuildResult result) : ISceneBuildService
    {
        public Task<SceneBuildResult> BuildSceneAsync(ResourceMetadata resource, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private static Type RequireType(Assembly assembly, string fullName) =>
        assembly.GetType(fullName, throwOnError: true)!;

    private static byte[] CreateSyntheticIbufPayload(IReadOnlyList<uint> indices)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        foreach (var index in indices)
        {
            writer.Write(index);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] CreateCompactDeltaPayload(IReadOnlyList<short> deltas)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(0u);
        foreach (var delta in deltas)
        {
            writer.Write(delta);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static class TestAssets
    {
        public static readonly byte[] OnePixelPng =
        [
            137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82,
            0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0, 31, 21, 196,
            137, 0, 0, 0, 13, 73, 68, 65, 84, 120, 156, 99, 248, 15, 4, 0,
            9, 251, 3, 253, 160, 166, 88, 27, 0, 0, 0, 0, 73, 69, 78, 68,
            174, 66, 96, 130
        ];

        public static readonly byte[] WaveBytes = CreateWave();

        private static byte[] CreateWave()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write("RIFF"u8.ToArray());
            writer.Write(36);
            writer.Write("WAVE"u8.ToArray());
            writer.Write("fmt "u8.ToArray());
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(22050);
            writer.Write(22050 * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write("data"u8.ToArray());
            writer.Write(0);
            writer.Flush();
            return stream.ToArray();
        }
    }
}
