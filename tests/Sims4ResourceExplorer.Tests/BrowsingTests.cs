using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Indexing;

namespace Sims4ResourceExplorer.Tests;

public sealed class BrowsingTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), "Sims4ResourceExplorer.BrowsingTests", Guid.NewGuid().ToString("N"));

    public BrowsingTests()
    {
        Directory.CreateDirectory(tempRoot);
    }

    [Fact]
    public void AssetBrowserState_BuildsSummaryAndFilterChips()
    {
        var state = new AssetBrowserState
        {
            SearchText = "chair",
            Domain = AssetBrowserDomain.BuildBuy,
            CategoryText = "comfort",
            HasThumbnailOnly = true,
            VariantsOnly = true,
            Sort = AssetBrowserSort.Category
        };

        var chips = state.BuildFilterChips(new SourceScope(IncludeMods: false));
        var summary = state.BuildSummary(new SourceScope(IncludeMods: false), 2431, 200);

        Assert.Contains(chips, chip => chip.Key == "sourceScope");
        Assert.Contains(chips, chip => chip.Key == "search");
        Assert.Contains(chips, chip => chip.Key == "category");
        Assert.Contains(chips, chip => chip.Key == "hasThumbnail");
        Assert.Contains(chips, chip => chip.Key == "variants");
        Assert.Contains("Assets > Build/Buy > Game + DLC", summary);
        Assert.Contains("showing 200 sorted by Category", summary);
    }

    [Fact]
    public void RawResourceBrowserState_RemoveFilter_ClearsOnlyRequestedFacet()
    {
        var state = new RawResourceBrowserState
        {
            SearchText = "00b2",
            Domain = RawResourceDomain.Audio,
            TypeNameText = "Audio",
            PackageText = "mods",
            GroupHexText = "0000",
            InstanceHexText = "0001",
            PreviewableOnly = true,
            ExportCapableOnly = true,
            CompressedKnownOnly = true,
            LinkFilter = ResourceLinkFilter.LinkedOnly
        };

        state.RemoveFilter("link");
        state.RemoveFilter("domain");

        Assert.Equal(ResourceLinkFilter.Any, state.LinkFilter);
        Assert.Equal(RawResourceDomain.All, state.Domain);
        Assert.True(state.PreviewableOnly);
        Assert.Equal("Audio", state.TypeNameText);
    }

    [Fact]
    public async Task SqliteIndexStore_QueryResources_ReturnsTotalCountAndStableWindow()
    {
        var cache = new TestCacheService(tempRoot);
        var store = new SqliteIndexStore(cache);
        await store.InitializeAsync(CancellationToken.None);

        var gameSource = new DataSourceDefinition(Guid.NewGuid(), "Game", tempRoot, SourceKind.Game);
        var modsSource = new DataSourceDefinition(Guid.NewGuid(), "Mods", tempRoot, SourceKind.Mods);
        await store.UpsertDataSourcesAsync([gameSource, modsSource], CancellationToken.None);

        await store.ReplacePackageAsync(
            new PackageScanResult(gameSource.Id, SourceKind.Game, "game.package", 10, DateTimeOffset.UtcNow, [
                CreateResource(gameSource.Id, "game.package", SourceKind.Game, "ObjectCatalog", 1, "Chair A"),
                CreateResource(gameSource.Id, "game.package", SourceKind.Game, "ObjectCatalog", 2, "Chair B")
            ], []),
            [],
            CancellationToken.None);

        await store.ReplacePackageAsync(
            new PackageScanResult(modsSource.Id, SourceKind.Mods, "mods.package", 10, DateTimeOffset.UtcNow, [
                CreateResource(modsSource.Id, "mods.package", SourceKind.Mods, "ObjectCatalog", 3, "Chair C")
            ], []),
            [],
            CancellationToken.None);

        var firstWindow = await store.QueryResourcesAsync(
            new RawResourceBrowserQuery(
                new SourceScope(IncludeMods: false),
                "chair",
                RawResourceDomain.All,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                false,
                false,
                false,
                ResourceLinkFilter.Any,
                RawResourceSort.Tgi,
                0,
                1),
            CancellationToken.None);

        var secondWindow = await store.QueryResourcesAsync(
            new RawResourceBrowserQuery(
                new SourceScope(IncludeMods: false),
                "chair",
                RawResourceDomain.All,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                false,
                false,
                false,
                ResourceLinkFilter.Any,
                RawResourceSort.Tgi,
                1,
                1),
            CancellationToken.None);

        Assert.Equal(2, firstWindow.TotalCount);
        Assert.Single(firstWindow.Items);
        Assert.True(firstWindow.HasMore);
        Assert.Single(secondWindow.Items);
        Assert.False(secondWindow.HasMore);
        Assert.NotEqual(firstWindow.Items[0].Key.FullTgi, secondWindow.Items[0].Key.FullTgi);
    }

    [Fact]
    public async Task SqliteIndexStore_QueryAssets_UsesModeSpecificFilters()
    {
        var cache = new TestCacheService(tempRoot);
        var store = new SqliteIndexStore(cache);
        await store.InitializeAsync(CancellationToken.None);

        var source = new DataSourceDefinition(Guid.NewGuid(), "Game", tempRoot, SourceKind.Game);
        await store.UpsertDataSourcesAsync([source], CancellationToken.None);

        var buildBuyRoot = CreateResource(source.Id, "objects.package", SourceKind.Game, "ObjectCatalog", 1, "Chair");
        var casRoot = CreateResource(source.Id, "cas.package", SourceKind.Game, "CASPart", 2, "Hat");
        var assets = new[]
        {
            new AssetSummary(Guid.NewGuid(), source.Id, SourceKind.Game, AssetKind.BuildBuy, "Chair", "Comfort", "objects.package", buildBuyRoot.Key, "thumbnail", 2, 4, string.Empty, new AssetCapabilitySnapshot(true, true, true, true)),
            new AssetSummary(Guid.NewGuid(), source.Id, SourceKind.Game, AssetKind.Cas, "Hat", "Head", "cas.package", casRoot.Key, null, 1, 2, string.Empty, new AssetCapabilitySnapshot(true, true, false, false))
        };

        await store.ReplacePackageAsync(
            new PackageScanResult(source.Id, SourceKind.Game, "objects.package", 10, DateTimeOffset.UtcNow, [buildBuyRoot], []),
            [assets[0]],
            CancellationToken.None);
        await store.ReplacePackageAsync(
            new PackageScanResult(source.Id, SourceKind.Game, "cas.package", 10, DateTimeOffset.UtcNow, [casRoot], []),
            [assets[1]],
            CancellationToken.None);

        var result = await store.QueryAssetsAsync(
            new AssetBrowserQuery(
                new SourceScope(),
                string.Empty,
                AssetBrowserDomain.BuildBuy,
                "comfort",
                string.Empty,
                string.Empty,
                true,
                true,
                AssetBrowserSort.Name,
                0,
                10,
                new AssetCapabilityFilter()),
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("Chair", result.Items[0].DisplayName);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task SqliteIndexStore_QueryAssets_CanFilterBySceneRoot()
    {
        var cache = new TestCacheService(tempRoot);
        var store = new SqliteIndexStore(cache);
        await store.InitializeAsync(CancellationToken.None);

        var source = new DataSourceDefinition(Guid.NewGuid(), "Game", tempRoot, SourceKind.Game);
        await store.UpsertDataSourcesAsync([source], CancellationToken.None);

        var readyRoot = CreateResource(source.Id, "objects.package", SourceKind.Game, "ObjectCatalog", 1, "Chair");
        var unsupportedRoot = CreateResource(source.Id, "objects.package", SourceKind.Game, "ObjectCatalog", 2, "Lamp");
        await store.ReplacePackageAsync(
            new PackageScanResult(source.Id, SourceKind.Game, "objects.package", 10, DateTimeOffset.UtcNow, [readyRoot, unsupportedRoot], []),
            [
                new AssetSummary(Guid.NewGuid(), source.Id, SourceKind.Game, AssetKind.BuildBuy, "Chair", "Comfort", "objects.package", readyRoot.Key, "thumbnail", 1, 4, string.Empty, new AssetCapabilitySnapshot(true, true, true, true)),
                new AssetSummary(Guid.NewGuid(), source.Id, SourceKind.Game, AssetKind.BuildBuy, "Lamp", "Lighting", "objects.package", unsupportedRoot.Key, null, 1, 2, string.Empty, new AssetCapabilitySnapshot(false, false, false, false))
            ],
            CancellationToken.None);

        var result = await store.QueryAssetsAsync(
            new AssetBrowserQuery(
                new SourceScope(),
                string.Empty,
                AssetBrowserDomain.BuildBuy,
                string.Empty,
                string.Empty,
                string.Empty,
                false,
                false,
                AssetBrowserSort.Name,
                0,
                10,
                new AssetCapabilityFilter(RequireSceneRoot: true)),
            CancellationToken.None);

        var asset = Assert.Single(result.Items);
        Assert.Equal("Chair", asset.DisplayName);
        Assert.True(asset.CapabilitySnapshot.HasSceneRoot);
    }

    [Fact]
    public async Task SqliteIndexStore_QueryAssets_CanFilterByTextureReferences()
    {
        var cache = new TestCacheService(tempRoot);
        var store = new SqliteIndexStore(cache);
        await store.InitializeAsync(CancellationToken.None);

        var source = new DataSourceDefinition(Guid.NewGuid(), "Game", tempRoot, SourceKind.Game);
        await store.UpsertDataSourcesAsync([source], CancellationToken.None);

        var texturedRoot = CreateResource(source.Id, "objects.package", SourceKind.Game, "ObjectCatalog", 1, "Chair");
        var untexturedRoot = CreateResource(source.Id, "objects.package", SourceKind.Game, "ObjectCatalog", 2, "Lamp");
        await store.ReplacePackageAsync(
            new PackageScanResult(source.Id, SourceKind.Game, "objects.package", 10, DateTimeOffset.UtcNow, [texturedRoot, untexturedRoot], []),
            [
                new AssetSummary(Guid.NewGuid(), source.Id, SourceKind.Game, AssetKind.BuildBuy, "Chair", "Comfort", "objects.package", texturedRoot.Key, "thumbnail", 1, 4, string.Empty, new AssetCapabilitySnapshot(true, true, true, true)),
                new AssetSummary(Guid.NewGuid(), source.Id, SourceKind.Game, AssetKind.BuildBuy, "Lamp", "Lighting", "objects.package", untexturedRoot.Key, null, 1, 2, string.Empty, new AssetCapabilitySnapshot(true, true, true, false))
            ],
            CancellationToken.None);

        var result = await store.QueryAssetsAsync(
            new AssetBrowserQuery(
                new SourceScope(),
                string.Empty,
                AssetBrowserDomain.BuildBuy,
                string.Empty,
                string.Empty,
                string.Empty,
                false,
                false,
                AssetBrowserSort.Name,
                0,
                10,
                new AssetCapabilityFilter(RequireTextureReferences: true)),
            CancellationToken.None);

        var asset = Assert.Single(result.Items);
        Assert.Equal("Chair", asset.DisplayName);
        Assert.True(asset.CapabilitySnapshot.HasTextureReferences);
    }

    [Fact]
    public async Task SqliteIndexStore_QueryAssets_CanFilterByExactGeometryCandidate()
    {
        var cache = new TestCacheService(tempRoot);
        var store = new SqliteIndexStore(cache);
        await store.InitializeAsync(CancellationToken.None);

        var source = new DataSourceDefinition(Guid.NewGuid(), "Game", tempRoot, SourceKind.Game);
        await store.UpsertDataSourcesAsync([source], CancellationToken.None);

        var exactRoot = CreateResource(source.Id, "objects.package", SourceKind.Game, "ObjectCatalog", 1, "Chair");
        var nonExactRoot = CreateResource(source.Id, "objects.package", SourceKind.Game, "ObjectCatalog", 2, "Lamp");
        await store.ReplacePackageAsync(
            new PackageScanResult(source.Id, SourceKind.Game, "objects.package", 10, DateTimeOffset.UtcNow, [exactRoot, nonExactRoot], []),
            [
                new AssetSummary(Guid.NewGuid(), source.Id, SourceKind.Game, AssetKind.BuildBuy, "Chair", "Comfort", "objects.package", exactRoot.Key, "thumbnail", 1, 4, string.Empty, new AssetCapabilitySnapshot(true, true, true, true)),
                new AssetSummary(Guid.NewGuid(), source.Id, SourceKind.Game, AssetKind.BuildBuy, "Lamp", "Lighting", "objects.package", nonExactRoot.Key, null, 1, 2, string.Empty, new AssetCapabilitySnapshot(true, false, false, false))
            ],
            CancellationToken.None);

        var result = await store.QueryAssetsAsync(
            new AssetBrowserQuery(
                new SourceScope(),
                string.Empty,
                AssetBrowserDomain.BuildBuy,
                string.Empty,
                string.Empty,
                string.Empty,
                false,
                false,
                AssetBrowserSort.Name,
                0,
                10,
                new AssetCapabilityFilter(RequireExactGeometryCandidate: true)),
            CancellationToken.None);

        var asset = Assert.Single(result.Items);
        Assert.Equal("Chair", asset.DisplayName);
        Assert.True(asset.CapabilitySnapshot.HasExactGeometryCandidate);
    }

    [Fact]
    public async Task SqliteIndexStore_GetAssetFacetOptions_ReturnsDistinctValues()
    {
        var cache = new TestCacheService(tempRoot);
        var store = new SqliteIndexStore(cache);
        await store.InitializeAsync(CancellationToken.None);

        var source = new DataSourceDefinition(Guid.NewGuid(), "Game", tempRoot, SourceKind.Game);
        await store.UpsertDataSourcesAsync([source], CancellationToken.None);

        var rootA = CreateResource(source.Id, "objects.package", SourceKind.Game, "ObjectCatalog", 1, "Chair");
        var rootB = CreateResource(source.Id, "objects.package", SourceKind.Game, "ObjectCatalog", 2, "Lamp");
        await store.ReplacePackageAsync(
            new PackageScanResult(source.Id, SourceKind.Game, "objects.package", 10, DateTimeOffset.UtcNow, [rootA, rootB], []),
            [
                new AssetSummary(Guid.NewGuid(), source.Id, SourceKind.Game, AssetKind.BuildBuy, "Chair", "Comfort", "objects.package", rootA.Key, "thumbnail", 1, 4, string.Empty, new AssetCapabilitySnapshot(true, true, true, true), PackageName: "objects.package", RootTypeName: "Model", ThumbnailTypeName: "BuyBuildThumbnail", PrimaryGeometryType: "ModelLOD", IdentityType: "ObjectDefinition", CategoryNormalized: "buildbuy"),
                new AssetSummary(Guid.NewGuid(), source.Id, SourceKind.Game, AssetKind.BuildBuy, "Lamp", "Lighting", "objects.package", rootB.Key, null, 1, 2, string.Empty, new AssetCapabilitySnapshot(true, false, false, false), PackageName: "objects.package", RootTypeName: "Model", ThumbnailTypeName: null, PrimaryGeometryType: null, IdentityType: "ObjectCatalog", CategoryNormalized: "buildbuy")
            ],
            CancellationToken.None);

        var options = await store.GetAssetFacetOptionsAsync(AssetKind.BuildBuy, CancellationToken.None);

        Assert.Contains("Comfort", options.Categories);
        Assert.Contains("Lighting", options.Categories);
        Assert.Equal(["Model"], options.RootTypeNames);
        Assert.Contains("ObjectDefinition", options.IdentityTypes);
        Assert.Contains("ObjectCatalog", options.IdentityTypes);
        Assert.Equal(["ModelLOD"], options.PrimaryGeometryTypes);
        Assert.Equal(["BuyBuildThumbnail"], options.ThumbnailTypeNames);
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

    private static ResourceMetadata CreateResource(Guid dataSourceId, string packagePath, SourceKind sourceKind, string typeName, uint group, string? name)
    {
        return new ResourceMetadata(
            Guid.NewGuid(),
            dataSourceId,
            sourceKind,
            packagePath,
            new ResourceKeyRecord(group, group, group, typeName),
            name,
            1,
            1,
            false,
            typeName.Contains("Audio", StringComparison.OrdinalIgnoreCase) ? PreviewKind.Audio : PreviewKind.Hex,
            true,
            true,
            string.Empty,
            string.Empty);
    }

    private sealed class TestCacheService(string root) : ICacheService
    {
        public string AppRoot => Path.Combine(root, "app");
        public string CacheRoot => Path.Combine(AppRoot, "cache");
        public string ExportRoot => Path.Combine(AppRoot, "exports");
        public string DatabasePath => Path.Combine(CacheRoot, "index.sqlite");

        public void EnsureCreated()
        {
            Directory.CreateDirectory(AppRoot);
            Directory.CreateDirectory(CacheRoot);
            Directory.CreateDirectory(ExportRoot);
        }
    }
}
