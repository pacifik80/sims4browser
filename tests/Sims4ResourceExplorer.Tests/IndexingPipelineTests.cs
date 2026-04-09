using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Indexing;

namespace Sims4ResourceExplorer.Tests;

public sealed class IndexingPipelineTests
{
    [Fact]
    public void IndexingRunOptions_ClampsWorkerCountToMachineRange()
    {
        Assert.Equal(1, IndexingRunOptions.ClampWorkerCount(0));
        Assert.Equal(IndexingRunOptions.GetMachineWorkerLimit(), IndexingRunOptions.ClampWorkerCount(int.MaxValue));
    }

    [Fact]
    public async Task PackageIndexCoordinator_UsesBoundedPackageConcurrency()
    {
        var root = CreatePackageRoot();

        try
        {
            var packagePaths = CreatePackageFiles(root, "a.package", "b.package", "c.package", "d.package");
            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            var scanner = new FakePackageScanner(packagePaths);
            var store = new FakeIndexStore();
            var catalog = new TrackingResourceCatalogService(delayPerPackage: TimeSpan.FromMilliseconds(120), progressBursts: 6);
            var coordinator = new PackageIndexCoordinator(
                scanner,
                catalog,
                new FakeAssetGraphBuilder(),
                store,
                new IndexingRunOptions(2, 2, 100, TimeSpan.FromMilliseconds(40), TimeSpan.FromMilliseconds(120)));

            var progressEvents = new List<IndexingProgress>();
            await coordinator.RunAsync([source], new Progress<IndexingProgress>(progressEvents.Add), CancellationToken.None);

            Assert.InRange(catalog.MaxConcurrency, 2, 2);
            Assert.Equal(4, store.ReplacedPackages.Count);
            Assert.Contains(progressEvents, progress => progress.ActiveWorkerCount >= 1);
            Assert.Contains(progressEvents, progress => progress.WorkerSlots?.Count == 2);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task PackageIndexCoordinator_ThrottlesUiProgressSnapshots()
    {
        var root = CreatePackageRoot();

        try
        {
            var packagePaths = CreatePackageFiles(root, "slow.package");
            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            var scanner = new FakePackageScanner(packagePaths);
            var store = new FakeIndexStore();
            var catalog = new TrackingResourceCatalogService(delayPerPackage: TimeSpan.FromMilliseconds(10), progressBursts: 30, delayPerProgress: TimeSpan.FromMilliseconds(5));
            var coordinator = new PackageIndexCoordinator(
                scanner,
                catalog,
                new FakeAssetGraphBuilder(),
                store,
                new IndexingRunOptions(1, 1, 100, TimeSpan.FromMilliseconds(40), TimeSpan.FromMilliseconds(120)));

            var progressEvents = new List<IndexingProgress>();
            await coordinator.RunAsync([source], new Progress<IndexingProgress>(progressEvents.Add), CancellationToken.None);

            Assert.True(progressEvents.Count < 20, $"Expected throttled progress events, got {progressEvents.Count}.");
            Assert.Contains(progressEvents, progress => progress.Summary is not null);
            Assert.All(progressEvents.Where(progress => progress.WorkerSlots is not null), progress =>
            {
                Assert.Equal([1], progress.WorkerSlots!.Select(static slot => slot.WorkerId).ToArray());
            });
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task PackageIndexCoordinator_CancelsResponsively()
    {
        var root = CreatePackageRoot();

        try
        {
            var packagePaths = CreatePackageFiles(root, "cancel.package", "later.package");
            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            var scanner = new FakePackageScanner(packagePaths);
            var store = new FakeIndexStore();
            var catalog = new TrackingResourceCatalogService(delayPerPackage: TimeSpan.FromSeconds(5), progressBursts: 1);
            var coordinator = new PackageIndexCoordinator(
                scanner,
                catalog,
                new FakeAssetGraphBuilder(),
                store,
                new IndexingRunOptions(1, 1, 100, TimeSpan.FromMilliseconds(40), TimeSpan.FromMilliseconds(120)));

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.RunAsync([source], progress: null, cts.Token));
            Assert.True(store.ReplacedPackages.Count <= 1);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task SqliteIndexStore_ReplacesLargePackageInBatches()
    {
        var root = Path.Combine(Path.GetTempPath(), "Sims4ResourceExplorer.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var cache = new TestCacheService(root);
            var store = new SqliteIndexStore(cache);
            await store.InitializeAsync(CancellationToken.None);

            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            await store.UpsertDataSourcesAsync([source], CancellationToken.None);

            var packagePath = Path.Combine(root, "large.package");
            var resources = Enumerable.Range(0, 450)
                .Select(index => CreateResource(source.Id, packagePath, index))
                .ToArray();

            await store.ReplacePackageAsync(
                new PackageScanResult(source.Id, SourceKind.Game, packagePath, 10, DateTimeOffset.UtcNow, resources, []),
                [],
                CancellationToken.None);

            var rows = await store.QueryResourcesAsync(new ResourceQuery(string.Empty, BrowserMode.RawResources, PackagePath: packagePath, Limit: 1000), CancellationToken.None);
            Assert.Equal(450, rows.Count);
            Assert.Equal(3, SqliteIndexStore.Batch(resources, 200).Count);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task PackageIndexCoordinator_KeepsWorkerSlotsStableAndBoundsRecentEvents()
    {
        var root = CreatePackageRoot();

        try
        {
            var packagePaths = CreatePackageFiles(root, "one.package", "two.package", "three.package", "four.package");
            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            var scanner = new FakePackageScanner(packagePaths);
            var store = new FakeIndexStore();
            var catalog = new TrackingResourceCatalogService(delayPerPackage: TimeSpan.FromMilliseconds(20), progressBursts: 3);
            var coordinator = new PackageIndexCoordinator(
                scanner,
                catalog,
                new FakeAssetGraphBuilder(),
                store,
                new IndexingRunOptions(2, 4, 100, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(40), MaxRecentEvents: 5));

            var progressEvents = new List<IndexingProgress>();
            await coordinator.RunAsync([source], new Progress<IndexingProgress>(progressEvents.Add), CancellationToken.None);

            Assert.All(progressEvents.Where(progress => progress.WorkerSlots is not null), progress =>
            {
                Assert.Equal([1, 2], progress.WorkerSlots!.Select(static slot => slot.WorkerId).ToArray());
            });

            var finalSnapshot = progressEvents.Last();
            Assert.NotNull(finalSnapshot.RecentEvents);
            Assert.True(finalSnapshot.RecentEvents!.Count <= 5);
            Assert.Contains(finalSnapshot.RecentEvents, activity => activity.Kind is "complete" or "failed");
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static ResourceMetadata CreateResource(Guid sourceId, string packagePath, int index) =>
        new(
            Guid.NewGuid(),
            sourceId,
            SourceKind.Game,
            packagePath,
            new ResourceKeyRecord((uint)(0x11111111 + index), 0x00000001, (ulong)index + 1, "ObjectCatalog"),
            $"item_{index}",
            100,
            120,
            true,
            PreviewKind.Hex,
            true,
            true,
            string.Empty,
            string.Empty);

    private static string CreatePackageRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "Sims4ResourceExplorer.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static IReadOnlyList<string> CreatePackageFiles(string root, params string[] names)
    {
        var results = new List<string>(names.Length);
        foreach (var name in names)
        {
            var path = Path.Combine(root, name);
            File.WriteAllText(path, string.Empty);
            results.Add(path);
        }

        return results;
    }

    private static void TryDelete(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class FakePackageScanner(IReadOnlyList<string> packagePaths) : IPackageScanner
    {
        public Task<IReadOnlyList<string>> DiscoverPackagePathsAsync(IEnumerable<DataSourceDefinition> sources, CancellationToken cancellationToken) =>
            Task.FromResult(packagePaths);
    }

    private sealed class TrackingResourceCatalogService(TimeSpan delayPerPackage, int progressBursts, TimeSpan? delayPerProgress = null) : IResourceCatalogService
    {
        private int currentConcurrency;

        public int MaxConcurrency { get; private set; }

        public async Task<PackageScanResult> ScanPackageAsync(DataSourceDefinition source, string packagePath, IProgress<PackageScanProgress>? progress, CancellationToken cancellationToken)
        {
            var concurrent = Interlocked.Increment(ref currentConcurrency);
            MaxConcurrency = Math.Max(MaxConcurrency, concurrent);

            try
            {
                for (var index = 0; index < progressBursts; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new PackageScanProgress("extracting metadata", progressBursts, index + 1, 0, TimeSpan.FromMilliseconds(index * 10)));
                    if (delayPerProgress is not null)
                    {
                        await Task.Delay(delayPerProgress.Value, cancellationToken);
                    }
                }

                await Task.Delay(delayPerPackage, cancellationToken);

                var resource = new ResourceMetadata(
                    Guid.NewGuid(),
                    source.Id,
                    source.Kind,
                    packagePath,
                    new ResourceKeyRecord(0x319E4F1D, 1, 1, "ObjectCatalog"),
                    Path.GetFileNameWithoutExtension(packagePath),
                    10,
                    12,
                    false,
                    PreviewKind.Hex,
                    true,
                    true,
                    string.Empty,
                    string.Empty);

                return new PackageScanResult(source.Id, source.Kind, packagePath, 10, DateTimeOffset.UtcNow, [resource], []);
            }
            finally
            {
                Interlocked.Decrement(ref currentConcurrency);
            }
        }

        public Task<byte[]> GetResourceBytesAsync(string packagePath, ResourceKeyRecord key, bool raw, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string?> GetTextAsync(string packagePath, ResourceKeyRecord key, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<byte[]?> GetTexturePngAsync(string packagePath, ResourceKeyRecord key, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeAssetGraphBuilder : IAssetGraphBuilder
    {
        public AssetGraph BuildAssetGraph(AssetSummary summary, IReadOnlyList<ResourceMetadata> packageResources) =>
            new(summary, packageResources, []);

        public IReadOnlyList<AssetSummary> BuildAssetSummaries(PackageScanResult packageScan) =>
            [];
    }

    private sealed class FakeIndexStore : IIndexStore
    {
        public List<string> ReplacedPackages { get; } = [];

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpsertDataSourcesAsync(IEnumerable<DataSourceDefinition> sources, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> NeedsRescanAsync(Guid dataSourceId, string packagePath, long fileSize, DateTimeOffset lastWriteTimeUtc, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<IReadOnlyList<ResourceMetadata>> QueryResourcesAsync(ResourceQuery query, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ResourceMetadata>>([]);
        public Task<IReadOnlyList<AssetSummary>> QueryAssetsAsync(LogicalAssetQuery query, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AssetSummary>>([]);
        public Task<IReadOnlyList<DataSourceDefinition>> GetDataSourcesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DataSourceDefinition>>([]);
        public Task<IReadOnlyList<ResourceMetadata>> GetPackageResourcesAsync(string packagePath, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ResourceMetadata>>([]);
        public Task<ResourceMetadata?> GetResourceByTgiAsync(string packagePath, string fullTgi, CancellationToken cancellationToken) => Task.FromResult<ResourceMetadata?>(null);

        public Task ReplacePackageAsync(PackageScanResult packageScan, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken)
        {
            ReplacedPackages.Add(packageScan.PackagePath);
            return Task.CompletedTask;
        }
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
