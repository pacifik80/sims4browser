using System.Collections.Concurrent;
using System.Threading.Channels;
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
            var scanner = new FakePackageScanner(source, packagePaths);
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

            Assert.Equal(2, catalog.MaxConcurrency);
            Assert.Equal(4, store.ReplacedPackages.Count);
            Assert.Equal(1, store.OpenWriteSessionCount);
            Assert.Contains(progressEvents, progress => progress.ActiveWorkerCount >= 1);
            Assert.Contains(progressEvents, progress => progress.WorkerSlots?.Count == 2);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task PackageIndexCoordinator_StartsProcessingBeforeDiscoveryCompletes()
    {
        var root = CreatePackageRoot();

        try
        {
            var packagePaths = CreatePackageFiles(root, "first.package", "second.package");
            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            var firstYielded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var allowSecondYield = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstScanStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var scanner = new BlockingPackageScanner(source, packagePaths, firstYielded, allowSecondYield);
            var store = new FakeIndexStore();
            var catalog = new TrackingResourceCatalogService(TimeSpan.FromMilliseconds(40), 2, onScanStarted: path =>
            {
                if (Path.GetFileName(path) == "first.package")
                {
                    firstScanStarted.TrySetResult();
                }
            });
            var coordinator = new PackageIndexCoordinator(
                scanner,
                catalog,
                new FakeAssetGraphBuilder(),
                store,
                new IndexingRunOptions(1, 1, 100, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(60)));

            var runTask = coordinator.RunAsync([source], progress: null, CancellationToken.None);
            await firstYielded.Task;
            await firstScanStarted.Task;
            allowSecondYield.TrySetResult();
            await runTask;

            Assert.Equal(2, store.ReplacedPackages.Count);
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
            var scanner = new FakePackageScanner(source, packagePaths);
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
    public async Task PackageIndexCoordinator_ReportsPreparationStagesBeforeDiscoveryWork()
    {
        var root = CreatePackageRoot();

        try
        {
            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            var scanner = new FakePackageScanner(source, []);
            var store = new FakeIndexStore();
            var catalog = new TrackingResourceCatalogService(TimeSpan.Zero, 0);
            var coordinator = new PackageIndexCoordinator(
                scanner,
                catalog,
                new FakeAssetGraphBuilder(),
                store,
                new IndexingRunOptions(1, 1, 100, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(60)));

            var progressEvents = new List<IndexingProgress>();
            await coordinator.RunAsync([source], new Progress<IndexingProgress>(progressEvents.Add), CancellationToken.None);

            Assert.NotEmpty(progressEvents);
            Assert.Equal("preparing", progressEvents[0].Stage);
            Assert.Contains(progressEvents, progress => progress.Message.Contains("cached package fingerprints", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(progressEvents, progress => progress.Message.Contains("SQLite write session", StringComparison.OrdinalIgnoreCase));
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
            var scanner = new FakePackageScanner(source, packagePaths);
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
    public async Task SqliteIndexStore_LoadsFingerprintsInBulk()
    {
        var root = CreatePackageRoot();

        try
        {
            var cache = new TestCacheService(root);
            var store = new SqliteIndexStore(cache);
            await store.InitializeAsync(CancellationToken.None);

            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            await store.UpsertDataSourcesAsync([source], CancellationToken.None);

            var packagePath = Path.Combine(root, "bulk.package");
            var resource = CreateResource(source.Id, packagePath, 1);
            await store.ReplacePackageAsync(
                new PackageScanResult(source.Id, SourceKind.Game, packagePath, 123, DateTimeOffset.UtcNow, [resource], []),
                [],
                CancellationToken.None);

            var fingerprints = await store.LoadPackageFingerprintsAsync([source.Id], CancellationToken.None);
            Assert.Single(fingerprints);
            Assert.Contains(fingerprints.Values, fingerprint => fingerprint.PackagePath == packagePath && fingerprint.FileSize == 123);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task SqliteIndexStore_ReplacesLargePackageUsingSingleWriteSession()
    {
        var root = CreatePackageRoot();

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

            await using var session = await store.OpenWriteSessionAsync(CancellationToken.None);
            await session.ReplacePackageAsync(
                new PackageScanResult(source.Id, SourceKind.Game, packagePath, 10, DateTimeOffset.UtcNow, resources, []),
                [],
                CancellationToken.None);

            var rows = await store.QueryResourcesAsync(
                new RawResourceBrowserQuery(
                    new SourceScope(),
                    string.Empty,
                    RawResourceDomain.All,
                    string.Empty,
                    packagePath,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    false,
                    ResourceLinkFilter.Any,
                    RawResourceSort.PackagePath,
                    0,
                    1000),
                CancellationToken.None);
            Assert.Equal(450, rows.Items.Count);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task ResourceMetadataEnrichmentService_PersistsDeferredMetadata()
    {
        var sourceId = Guid.NewGuid();
        var resource = new ResourceMetadata(
            Guid.NewGuid(),
            sourceId,
            SourceKind.Game,
            "test.package",
            new ResourceKeyRecord(0x319E4F1D, 1, 1, "ObjectCatalog"),
            null,
            null,
            null,
            null,
            PreviewKind.Hex,
            true,
            true,
            string.Empty,
            string.Empty);

        var store = new FakeIndexStore();
        var catalog = new TrackingResourceCatalogService(TimeSpan.Zero, 0)
        {
            EnrichedResourceFactory = input => input with
            {
                Name = "Enriched",
                CompressedSize = 10,
                UncompressedSize = 12,
                IsCompressed = false
            }
        };

        var service = new ResourceMetadataEnrichmentService(catalog, store);
        var enriched = await service.EnrichAsync(resource, CancellationToken.None);

        Assert.Equal("Enriched", enriched.Name);
        Assert.Equal(1, catalog.EnrichCalls);
        Assert.Single(store.EnrichedResources);
    }

    [Fact]
    public async Task PackageIndexCoordinator_FastPathDoesNotInvokePerResourceEnrichment()
    {
        var root = CreatePackageRoot();

        try
        {
            var packagePaths = CreatePackageFiles(root, "fast.package");
            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            var scanner = new FakePackageScanner(source, packagePaths);
            var store = new FakeIndexStore();
            var catalog = new TrackingResourceCatalogService(TimeSpan.FromMilliseconds(5), 1)
            {
                ScanResourceFactory = packagePath => new ResourceMetadata(
                    Guid.NewGuid(),
                    source.Id,
                    source.Kind,
                    packagePath,
                    new ResourceKeyRecord(0x319E4F1D, 1, 1, "ObjectCatalog"),
                    null,
                    null,
                    null,
                    null,
                    PreviewKind.Hex,
                    true,
                    true,
                    "Build/Buy seed",
                    string.Empty)
            };

            var coordinator = new PackageIndexCoordinator(
                scanner,
                catalog,
                new FakeAssetGraphBuilder(),
                store,
                new IndexingRunOptions(1, 1, 100, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(60)));

            await coordinator.RunAsync([source], progress: null, CancellationToken.None);

            Assert.Equal(0, catalog.EnrichCalls);
            var persisted = store.PersistedScans.Single().Resources.Single();
            Assert.Null(persisted.Name);
            Assert.Null(persisted.CompressedSize);
            Assert.Null(persisted.UncompressedSize);
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
            var scanner = new FakePackageScanner(source, packagePaths);
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

            var finalSnapshot = progressEvents.Last(progress => progress.Summary is not null);
            Assert.NotNull(finalSnapshot.RecentEvents);
            Assert.True(finalSnapshot.RecentEvents!.Count <= 5);
            Assert.Contains(finalSnapshot.RecentEvents, activity => activity.Kind is "complete" or "failed");
            Assert.Equal(4, finalSnapshot.Summary!.PackagesDiscovered);
            Assert.Equal(4, finalSnapshot.Summary.PackagesQueued);
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
            100L,
            120L,
            true,
            PreviewKind.Hex,
            true,
            true,
            "Build/Buy seed",
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

    private sealed class FakePackageScanner(DataSourceDefinition source, IReadOnlyList<string> packagePaths) : IPackageScanner
    {
        public async IAsyncEnumerable<DiscoveredPackage> DiscoverPackagesAsync(IEnumerable<DataSourceDefinition> sources, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var path in packagePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var info = new FileInfo(path);
                yield return new DiscoveredPackage(source, path, info.Length, info.LastWriteTimeUtc);
                await Task.Yield();
            }
        }
    }

    private sealed class BlockingPackageScanner(
        DataSourceDefinition source,
        IReadOnlyList<string> packagePaths,
        TaskCompletionSource firstYielded,
        TaskCompletionSource allowSecondYield) : IPackageScanner
    {
        public async IAsyncEnumerable<DiscoveredPackage> DiscoverPackagesAsync(IEnumerable<DataSourceDefinition> sources, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (var index = 0; index < packagePaths.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = packagePaths[index];
                var info = new FileInfo(path);
                yield return new DiscoveredPackage(source, path, info.Length, info.LastWriteTimeUtc);
                if (index == 0)
                {
                    firstYielded.TrySetResult();
                    await allowSecondYield.Task.WaitAsync(cancellationToken);
                }
            }
        }
    }

    private sealed class TrackingResourceCatalogService(TimeSpan delayPerPackage, int progressBursts, TimeSpan? delayPerProgress = null, Action<string>? onScanStarted = null) : IResourceCatalogService
    {
        private int currentConcurrency;

        public int MaxConcurrency { get; private set; }
        public int EnrichCalls { get; private set; }
        public Func<string, ResourceMetadata>? ScanResourceFactory { get; init; }
        public Func<ResourceMetadata, ResourceMetadata>? EnrichedResourceFactory { get; init; }

        public async Task<PackageScanResult> ScanPackageAsync(DataSourceDefinition source, string packagePath, IProgress<PackageScanProgress>? progress, CancellationToken cancellationToken)
        {
            var concurrent = Interlocked.Increment(ref currentConcurrency);
            MaxConcurrency = Math.Max(MaxConcurrency, concurrent);
            onScanStarted?.Invoke(packagePath);

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

                if (delayPerPackage > TimeSpan.Zero)
                {
                    await Task.Delay(delayPerPackage, cancellationToken);
                }

                var resource = ScanResourceFactory?.Invoke(packagePath) ?? new ResourceMetadata(
                    Guid.NewGuid(),
                    source.Id,
                    source.Kind,
                    packagePath,
                    new ResourceKeyRecord(0x319E4F1D, 1, 1, "ObjectCatalog"),
                    null,
                    null,
                    null,
                    null,
                    PreviewKind.Hex,
                    true,
                    true,
                    "Build/Buy seed",
                    string.Empty);

                return new PackageScanResult(source.Id, source.Kind, packagePath, 10, DateTimeOffset.UtcNow, [resource], []);
            }
            finally
            {
                Interlocked.Decrement(ref currentConcurrency);
            }
        }

        public Task<ResourceMetadata> EnrichResourceAsync(ResourceMetadata resource, CancellationToken cancellationToken)
        {
            EnrichCalls++;
            return Task.FromResult(EnrichedResourceFactory?.Invoke(resource) ?? resource);
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
        public Task<AssetGraph> BuildAssetGraphAsync(AssetSummary summary, IReadOnlyList<ResourceMetadata> packageResources, CancellationToken cancellationToken) =>
            Task.FromResult(new AssetGraph(summary, packageResources, []));

        public IReadOnlyList<AssetSummary> BuildAssetSummaries(PackageScanResult packageScan) =>
            [];
    }

    private sealed class FakeIndexStore : IIndexStore
    {
        public List<string> ReplacedPackages { get; } = [];
        public List<PackageScanResult> PersistedScans { get; } = [];
        public List<ResourceMetadata> EnrichedResources { get; } = [];
        public int OpenWriteSessionCount { get; private set; }
        public ConcurrentDictionary<string, PackageFingerprint> Fingerprints { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpsertDataSourcesAsync(IEnumerable<DataSourceDefinition> sources, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> NeedsRescanAsync(Guid dataSourceId, string packagePath, long fileSize, DateTimeOffset lastWriteTimeUtc, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<IReadOnlyDictionary<string, PackageFingerprint>> LoadPackageFingerprintsAsync(IEnumerable<Guid> dataSourceIds, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyDictionary<string, PackageFingerprint>>(Fingerprints);
        public Task<WindowedQueryResult<ResourceMetadata>> QueryResourcesAsync(RawResourceBrowserQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new WindowedQueryResult<ResourceMetadata>([], 0, query.Offset, query.WindowSize));
        public Task<WindowedQueryResult<AssetSummary>> QueryAssetsAsync(AssetBrowserQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new WindowedQueryResult<AssetSummary>([], 0, query.Offset, query.WindowSize));
        public Task<IReadOnlyList<DataSourceDefinition>> GetDataSourcesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DataSourceDefinition>>([]);
        public Task<IReadOnlyList<ResourceMetadata>> GetPackageResourcesAsync(string packagePath, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ResourceMetadata>>([]);
        public Task<ResourceMetadata?> GetResourceByTgiAsync(string packagePath, string fullTgi, CancellationToken cancellationToken) => Task.FromResult<ResourceMetadata?>(null);

        public Task ReplacePackageAsync(PackageScanResult packageScan, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken)
        {
            Persist(packageScan);
            return Task.CompletedTask;
        }

        public Task<IIndexWriteSession> OpenWriteSessionAsync(CancellationToken cancellationToken)
        {
            OpenWriteSessionCount++;
            return Task.FromResult<IIndexWriteSession>(new FakeWriteSession(this));
        }

        public Task<ResourceMetadata> PersistResourceEnrichmentAsync(ResourceMetadata resource, CancellationToken cancellationToken)
        {
            EnrichedResources.Add(resource);
            return Task.FromResult(resource);
        }

        private void Persist(PackageScanResult packageScan)
        {
            ReplacedPackages.Add(packageScan.PackagePath);
            PersistedScans.Add(packageScan);
        }

        private sealed class FakeWriteSession(FakeIndexStore store) : IIndexWriteSession
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;

            public Task ReplacePackageAsync(PackageScanResult packageScan, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken)
            {
                store.Persist(packageScan);
                return Task.CompletedTask;
            }
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
