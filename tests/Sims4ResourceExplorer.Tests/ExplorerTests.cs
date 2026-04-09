using LlamaLogic.Packages;
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
        var builder = new ExplicitBuildBuyAssetGraphBuilder();
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

        Assert.Contains(summaries, summary => summary.AssetKind == AssetKind.BuildBuy);
    }

    [Fact]
    public void AssetGraphBuilder_BuildsExplicitBuildBuyGraphForSupportedSubset()
    {
        var builder = new ExplicitBuildBuyAssetGraphBuilder();
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
        var graph = builder.BuildAssetGraph(summary, resources);

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
        var asset = new AssetSummary(Guid.NewGuid(), source.Id, SourceKind.Game, AssetKind.BuildBuy, "Chair", "Object", "test.package", resource.Key, null, 1, 1, string.Empty);
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
                false,
                false,
                AssetBrowserSort.Name,
                0,
                10),
            CancellationToken.None);

        Assert.Single(resources.Items);
        Assert.Single(assets.Items);
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
        var graphBuilder = new ExplicitBuildBuyAssetGraphBuilder();
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
        var assetGraph = graphBuilder.BuildAssetGraph(asset, packageResources);
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

        return resources
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
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
