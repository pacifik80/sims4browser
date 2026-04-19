using Microsoft.Data.Sqlite;
using System.Collections.Concurrent;
using System.Threading.Channels;
using Sims4ResourceExplorer.Assets;
using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Indexing;
using System.Buffers.Binary;
using System.Text;

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
    public async Task PackageIndexCoordinator_CompletesScopeBeforeStartingProcessing()
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
            var startedTooEarly = await Task.WhenAny(firstScanStarted.Task, Task.Delay(TimeSpan.FromMilliseconds(200))) == firstScanStarted.Task;
            Assert.False(startedTooEarly);
            allowSecondYield.TrySetResult();
            await firstScanStarted.Task;
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

            Assert.True(progressEvents.Count < 25, $"Expected throttled progress events, got {progressEvents.Count}.");
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
            Assert.Contains(progressEvents, progress => string.Equals(progress.Stage, "preparing", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(progressEvents, progress => string.Equals(progress.Stage, "scope", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(progressEvents, progress => progress.Message.Contains("rebuild session", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(progressEvents, progress => progress.Message.Contains("scope", StringComparison.OrdinalIgnoreCase));
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
    public async Task SqliteIndexStore_BulkReplaceUpdatesFtsWithoutLeavingStaleSearchRows()
    {
        var root = CreatePackageRoot();

        try
        {
            var cache = new TestCacheService(root);
            var store = new SqliteIndexStore(cache);
            await store.InitializeAsync(CancellationToken.None);

            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            await store.UpsertDataSourcesAsync([source], CancellationToken.None);

            var packagePath = Path.Combine(root, "searchable.package");
            ResourceMetadata[] initialResources =
            [
                CreateNamedResource(source.Id, packagePath, 1, "OldChair"),
                CreateNamedResource(source.Id, packagePath, 2, "OldLamp")
            ];
            AssetSummary[] initialAssets =
            [
                CreateAsset(source.Id, packagePath, "OldChair Asset")
            ];

            await using (var session = await store.OpenWriteSessionAsync(CancellationToken.None))
            {
                await session.ReplacePackagesAsync(
                    [(new PackageScanResult(source.Id, SourceKind.Game, packagePath, 10, DateTimeOffset.UtcNow, initialResources, []), initialAssets)],
                    CancellationToken.None);
                await session.FinalizeAsync(progress: null, CancellationToken.None);
            }

            var oldResourceSearch = await store.QueryResourcesAsync(
                new RawResourceBrowserQuery(
                    new SourceScope(),
                    "OldChair",
                    RawResourceDomain.All,
                    string.Empty,
                    packagePath,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    false,
                    ResourceLinkFilter.Any,
                    RawResourceSort.TypeName,
                    0,
                    20),
                CancellationToken.None);
            Assert.Single(oldResourceSearch.Items);

            var oldAssetSearch = await store.QueryAssetsAsync(
                new AssetBrowserQuery(
                    new SourceScope(),
                    "OldChair",
                    AssetBrowserDomain.BuildBuy,
                    string.Empty,
                    packagePath,
                    string.Empty,
                    false,
                    false,
                    AssetBrowserSort.Name,
                    0,
                    20),
                CancellationToken.None);
            Assert.Single(oldAssetSearch.Items);

            ResourceMetadata[] updatedResources =
            [
                CreateNamedResource(source.Id, packagePath, 3, "NewChair")
            ];

            await using (var session = await store.OpenWriteSessionAsync(CancellationToken.None))
            {
                await session.ReplacePackagesAsync(
                    [(new PackageScanResult(source.Id, SourceKind.Game, packagePath, 11, DateTimeOffset.UtcNow, updatedResources, []), Array.Empty<AssetSummary>())],
                    CancellationToken.None);
                await session.FinalizeAsync(progress: null, CancellationToken.None);
            }

            var staleResourceSearch = await store.QueryResourcesAsync(
                new RawResourceBrowserQuery(
                    new SourceScope(),
                    "OldChair",
                    RawResourceDomain.All,
                    string.Empty,
                    packagePath,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    false,
                    ResourceLinkFilter.Any,
                    RawResourceSort.TypeName,
                    0,
                    20),
                CancellationToken.None);
            Assert.Empty(staleResourceSearch.Items);

            var freshResourceSearch = await store.QueryResourcesAsync(
                new RawResourceBrowserQuery(
                    new SourceScope(),
                    "NewChair",
                    RawResourceDomain.All,
                    string.Empty,
                    packagePath,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    false,
                    ResourceLinkFilter.Any,
                    RawResourceSort.TypeName,
                    0,
                    20),
                CancellationToken.None);
            Assert.Single(freshResourceSearch.Items);

            var staleAssetSearch = await store.QueryAssetsAsync(
                new AssetBrowserQuery(
                    new SourceScope(),
                    "OldChair",
                    AssetBrowserDomain.BuildBuy,
                    string.Empty,
                    packagePath,
                    string.Empty,
                    false,
                    false,
                    AssetBrowserSort.Name,
                    0,
                    20),
                CancellationToken.None);
            Assert.Empty(staleAssetSearch.Items);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task SqliteIndexStore_PersistsSimTemplateFactsAndBodyPartFacts()
    {
        var root = CreatePackageRoot();

        try
        {
            var cache = new TestCacheService(root);
            var store = new SqliteIndexStore(cache);
            await store.InitializeAsync(CancellationToken.None);

            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            await store.UpsertDataSourcesAsync([source], CancellationToken.None);

            var packagePath = Path.Combine(root, "sim-template.package");
            var simInfo = new ResourceMetadata(
                Guid.NewGuid(),
                source.Id,
                source.Kind,
                packagePath,
                new ResourceKeyRecord(0x025ED6F4, 0, 0xFDCCF77200000001ul, "SimInfo"),
                "SimInfo template: Human | Young Adult | Female | outfits 1/1/2 [00000001]",
                100L,
                120L,
                false,
                PreviewKind.Hex,
                true,
                true,
                "Sim/character seed",
                string.Empty,
                Description: "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=1 categories / 1 entries / 2 parts");
            var fact = new SimTemplateFactSummary(
                simInfo.Id,
                source.Id,
                source.Kind,
                packagePath,
                simInfo.Key.FullTgi,
                "sim-archetype:human|young-adult|female",
                "Human",
                "Young Adult",
                "Female",
                simInfo.Name!,
                simInfo.Description!,
                1,
                1,
                2,
                4,
                8,
                1,
                true,
                1,
                2);
            var bodyPart = new SimTemplateBodyPartFact(
                simInfo.Id,
                source.Id,
                source.Kind,
                packagePath,
                simInfo.Key.FullTgi,
                5,
                "Nude",
                0,
                0,
                5,
                "Full Body",
                0x6000000000000031ul,
                true);

            await store.ReplacePackageAsync(
                new PackageScanResult(source.Id, source.Kind, packagePath, 10, DateTimeOffset.UtcNow, [simInfo], [])
                {
                    SimTemplateFacts = [fact],
                    SimTemplateBodyPartFacts = [bodyPart]
                },
                [],
                CancellationToken.None);

            var loadedFacts = await store.GetSimTemplateFactsByArchetypeAsync(fact.ArchetypeKey, CancellationToken.None);
            var loadedBodyParts = await store.GetSimTemplateBodyPartFactsAsync(simInfo.Id, CancellationToken.None);

            var loadedFact = Assert.Single(loadedFacts);
            Assert.Equal(fact.RootTgi, loadedFact.RootTgi);
            Assert.Equal(fact.AuthoritativeBodyDrivingOutfitCount, loadedFact.AuthoritativeBodyDrivingOutfitCount);
            Assert.Equal(fact.AuthoritativeBodyDrivingOutfitPartCount, loadedFact.AuthoritativeBodyDrivingOutfitPartCount);

            var loadedBodyPart = Assert.Single(loadedBodyParts);
            Assert.Equal(bodyPart.PartInstance, loadedBodyPart.PartInstance);
            Assert.Equal(bodyPart.BodyType, loadedBodyPart.BodyType);
            Assert.True(loadedBodyPart.IsBodyDriving);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task SqliteIndexStore_GetIndexedDefaultBodyRecipeAssets_ReturnsNudeCandidatesWithoutExactGeometryFlag()
    {
        var root = CreatePackageRoot();

        try
        {
            var cache = new TestCacheService(root);
            var store = new SqliteIndexStore(cache);
            await store.InitializeAsync(CancellationToken.None);

            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            await store.UpsertDataSourcesAsync([source], CancellationToken.None);

            var packagePath = Path.Combine(root, "nude-body.package");
            var nudeAsset = new AssetSummary(
                Guid.NewGuid(),
                source.Id,
                source.Kind,
                AssetKind.Cas,
                "ahBody_nude",
                "Full Body",
                packagePath,
                new ResourceKeyRecord(0x034AEECB, 0, 0x7000000000000001ul, "CASPart"),
                null,
                1,
                1,
                string.Empty,
                new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true),
                CategoryNormalized: "full-body",
                Description: "slot=Full Body | bodyType=5 | internalName=ahBody_nude | species=Human | age=Teen / Young Adult / Adult / Elder | gender=Unisex | defaultBodyType=false | defaultBodyTypeFemale=false | defaultBodyTypeMale=false | nakedLink=false | restrictOppositeGender=false | restrictOppositeFrame=false | sortLayer=0");
            var bathrobeAsset = new AssetSummary(
                Guid.NewGuid(),
                source.Id,
                source.Kind,
                AssetKind.Cas,
                "yfBody_Bathrobe_SolidWhiteGray",
                "Full Body",
                packagePath,
                new ResourceKeyRecord(0x034AEECB, 0, 0x7000000000000002ul, "CASPart"),
                null,
                1,
                1,
                string.Empty,
                new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true),
                CategoryNormalized: "full-body",
                Description: "slot=Full Body | bodyType=5 | internalName=yfBody_Bathrobe_SolidWhiteGray | species=Human | age=Teen / Young Adult / Adult / Elder | gender=Female | defaultBodyType=true | defaultBodyTypeFemale=false | defaultBodyTypeMale=false | nakedLink=false | restrictOppositeGender=false | restrictOppositeFrame=false | sortLayer=16000");
            var nudeFact = new DiscoveredCasPartFact(
                source.Id,
                source.Kind,
                packagePath,
                nudeAsset.RootKey.FullTgi,
                "Full Body",
                "full-body",
                5,
                "ahBody_nude",
                false,
                false,
                false,
                false,
                false,
                false,
                0,
                "Human",
                "Teen / Young Adult / Adult / Elder",
                "Unisex");
            var bathrobeFact = new DiscoveredCasPartFact(
                source.Id,
                source.Kind,
                packagePath,
                bathrobeAsset.RootKey.FullTgi,
                "Full Body",
                "full-body",
                5,
                "yfBody_Bathrobe_SolidWhiteGray",
                true,
                false,
                false,
                false,
                false,
                false,
                16000,
                "Human",
                "Teen / Young Adult / Adult / Elder",
                "Female");

            await store.ReplacePackageAsync(
                new PackageScanResult(source.Id, source.Kind, packagePath, 10, DateTimeOffset.UtcNow, [], [])
                {
                    CasPartFacts = [nudeFact, bathrobeFact]
                },
                [nudeAsset, bathrobeAsset],
                CancellationToken.None);

            var results = await store.GetIndexedDefaultBodyRecipeAssetsAsync(
                new SimInfoSummary(32, "Human", "Adult", "Female", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
                "Full Body",
                CancellationToken.None);

            Assert.NotEmpty(results);
            Assert.Equal("ahBody_nude", results[0].DisplayName);
            Assert.Contains(results, asset => asset.DisplayName == "yfBody_Bathrobe_SolidWhiteGray");
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task SqliteIndexStore_GetIndexedDefaultBodyRecipeAssets_DoesNotUsePrefixOnlyHumanInfantShellsWithoutDefaultSignals()
    {
        var root = CreatePackageRoot();

        try
        {
            var cache = new TestCacheService(root);
            var store = new SqliteIndexStore(cache);
            await store.InitializeAsync(CancellationToken.None);

            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            await store.UpsertDataSourcesAsync([source], CancellationToken.None);

            var packagePath = Path.Combine(root, "infant-prefix-only.package");
            var fullBodyAsset = new AssetSummary(
                Guid.NewGuid(),
                source.Id,
                source.Kind,
                AssetKind.Cas,
                "iuBody_RomperDuckie",
                "Full Body",
                packagePath,
                new ResourceKeyRecord(0x034AEECB, 0, 0x7000000000000011ul, "CASPart"),
                null,
                1,
                1,
                string.Empty,
                new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true),
                CategoryNormalized: "full-body",
                Description: "slot=Full Body | bodyType=5 | internalName=iuBody_RomperDuckie | species=Human | age=Infant | gender=Unisex | defaultBodyType=false | defaultBodyTypeFemale=false | defaultBodyTypeMale=false | nakedLink=false | restrictOppositeGender=false | restrictOppositeFrame=false | sortLayer=0");
            var topAsset = new AssetSummary(
                Guid.NewGuid(),
                source.Id,
                source.Kind,
                AssetKind.Cas,
                "iuTop_Duckie",
                "Top",
                packagePath,
                new ResourceKeyRecord(0x034AEECB, 0, 0x7000000000000012ul, "CASPart"),
                null,
                1,
                1,
                string.Empty,
                new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true),
                CategoryNormalized: "top",
                Description: "slot=Top | bodyType=6 | internalName=iuTop_Duckie | species=Human | age=Infant | gender=Unisex | defaultBodyType=false | defaultBodyTypeFemale=false | defaultBodyTypeMale=false | nakedLink=false | restrictOppositeGender=false | restrictOppositeFrame=false | sortLayer=0");
            var fullBodyFact = new DiscoveredCasPartFact(
                source.Id,
                source.Kind,
                packagePath,
                fullBodyAsset.RootKey.FullTgi,
                "Full Body",
                "full-body",
                5,
                "iuBody_RomperDuckie",
                false,
                false,
                false,
                false,
                false,
                false,
                0,
                "Human",
                "Infant",
                "Unisex");
            var topFact = new DiscoveredCasPartFact(
                source.Id,
                source.Kind,
                packagePath,
                topAsset.RootKey.FullTgi,
                "Top",
                "top",
                6,
                "iuTop_Duckie",
                false,
                false,
                false,
                false,
                false,
                false,
                0,
                "Human",
                "Infant",
                "Unisex");

            await store.ReplacePackageAsync(
                new PackageScanResult(source.Id, source.Kind, packagePath, 10, DateTimeOffset.UtcNow, [], [])
                {
                    CasPartFacts = [fullBodyFact, topFact]
                },
                [fullBodyAsset, topAsset],
                CancellationToken.None);

            var metadata = new SimInfoSummary(32, "Human", "Infant", "Female", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

            var fullBodyResults = await store.GetIndexedDefaultBodyRecipeAssetsAsync(
                metadata,
                "Full Body",
                CancellationToken.None);
            var topResults = await store.GetIndexedDefaultBodyRecipeAssetsAsync(
                metadata,
                "Top",
                CancellationToken.None);

            Assert.Empty(fullBodyResults);
            Assert.Empty(topResults);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task SqliteIndexStore_GetIndexedDefaultBodyRecipeAssets_AllowsLittleDogChildToReuseDogDefaultBodyShell()
    {
        var root = CreatePackageRoot();

        try
        {
            var cache = new TestCacheService(root);
            var store = new SqliteIndexStore(cache);
            await store.InitializeAsync(CancellationToken.None);

            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            await store.UpsertDataSourcesAsync([source], CancellationToken.None);

            var packagePath = Path.Combine(root, "little-dog-child-default-body.package");
            var defaultBodyAsset = new AssetSummary(
                Guid.NewGuid(),
                source.Id,
                source.Kind,
                AssetKind.Cas,
                "cdBody_Nude",
                "Full Body",
                packagePath,
                new ResourceKeyRecord(0x034AEECB, 0, 0x7000000000000013ul, "CASPart"),
                null,
                1,
                1,
                string.Empty,
                new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true),
                CategoryNormalized: "full-body",
                Description: "slot=Full Body | bodyType=5 | internalName=cdBody_Nude | species=Dog | age=Child | gender=Unisex | defaultBodyType=true | defaultBodyTypeFemale=false | defaultBodyTypeMale=false | nakedLink=false | restrictOppositeGender=false | restrictOppositeFrame=false | sortLayer=0");
            var defaultBodyFact = new DiscoveredCasPartFact(
                source.Id,
                source.Kind,
                packagePath,
                defaultBodyAsset.RootKey.FullTgi,
                "Full Body",
                "full-body",
                5,
                "cdBody_Nude",
                true,
                false,
                false,
                false,
                false,
                false,
                0,
                "Dog",
                "Child",
                "Unisex");

            await store.ReplacePackageAsync(
                new PackageScanResult(source.Id, source.Kind, packagePath, 10, DateTimeOffset.UtcNow, [], [])
                {
                    CasPartFacts = [defaultBodyFact]
                },
                [defaultBodyAsset],
                CancellationToken.None);

            var results = await store.GetIndexedDefaultBodyRecipeAssetsAsync(
                new SimInfoSummary(32, "Little Dog", "Child", "Female", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
                "Full Body",
                CancellationToken.None);

            Assert.NotEmpty(results);
            Assert.Equal("cdBody_Nude", results[0].DisplayName);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task SqliteIndexStore_InitializeInvalidatesStaleSeedFactTablesWithoutDroppingPackageFingerprints()
    {
        var root = CreatePackageRoot();

        try
        {
            var cache = new TestCacheService(root);
            var store = new SqliteIndexStore(cache);
            await store.InitializeAsync(CancellationToken.None);

            await using (var connection = await OpenConnectionAsync(cache.DatabasePath))
            {
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        """
                        INSERT INTO packages(data_source_id, package_path, file_size, last_write_utc, indexed_utc)
                        VALUES ('11111111-1111-1111-1111-111111111111', 'stale.package', 10, '2026-04-18T00:00:00.0000000+00:00', '2026-04-18T00:00:00.0000000+00:00');

                        INSERT INTO sim_template_facts(resource_id, data_source_id, source_kind, package_path, root_tgi, archetype_key, species_label, age_label, gender_label, display_name, notes, outfit_category_count, outfit_entry_count, outfit_part_count, body_modifier_count, face_modifier_count, sculpt_count, has_skintone, authoritative_body_driving_outfit_count, authoritative_body_driving_outfit_part_count)
                        VALUES ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', '11111111-1111-1111-1111-111111111111', 'Game', 'stale.package', '025ED6F4:00000000:0000000000000001', 'sim-archetype:human|adult|female', 'Human', 'Adult', 'Female', 'stale fact', '', 1, 1, 1, 0, 0, 0, 0, 1, 1);

                        INSERT INTO sim_template_body_parts(resource_id, data_source_id, source_kind, package_path, root_tgi, outfit_category_value, outfit_category_label, outfit_index, part_index, body_type, body_type_label, part_instance_hex, is_body_driving)
                        VALUES ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', '11111111-1111-1111-1111-111111111111', 'Game', 'stale.package', '025ED6F4:00000000:0000000000000001', 5, 'Nude', 0, 0, 5, 'Full Body', '0000000000000001', 1);

                        INSERT INTO cas_part_facts(asset_id, data_source_id, source_kind, package_path, root_tgi, slot_category, category_normalized, body_type, internal_name, default_body_type, default_body_type_female, default_body_type_male, has_naked_link, restrict_opposite_gender, restrict_opposite_frame, sort_layer, species_label, age_label, gender_label)
                        VALUES ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', '11111111-1111-1111-1111-111111111111', 'Game', 'stale.package', '034AEECB:00000000:0000000000000002', 'Full Body', 'full-body', 5, 'stale_body', 1, 1, 1, 1, 0, 0, 0, 'Human', 'Adult', 'Female');

                        INSERT INTO cache_metadata(key, value)
                        VALUES ('seed_fact_content_version', 'stale-version')
                        ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                        """;
                    await command.ExecuteNonQueryAsync(CancellationToken.None);
                }
            }

            await store.InitializeAsync(CancellationToken.None);

            await using var verifyConnection = await OpenConnectionAsync(cache.DatabasePath);
            await using (var countCommand = verifyConnection.CreateCommand())
            {
                countCommand.CommandText =
                    """
                    SELECT
                        (SELECT COUNT(*) FROM packages),
                        (SELECT COUNT(*) FROM sim_template_facts),
                        (SELECT COUNT(*) FROM sim_template_body_parts),
                        (SELECT COUNT(*) FROM cas_part_facts);
                    """;
                await using var reader = await countCommand.ExecuteReaderAsync(CancellationToken.None);
                Assert.True(await reader.ReadAsync(CancellationToken.None));
                Assert.Equal(1, reader.GetInt32(0));
                Assert.Equal(0, reader.GetInt32(1));
                Assert.Equal(0, reader.GetInt32(2));
                Assert.Equal(0, reader.GetInt32(3));
            }

            await using (var metadataCommand = verifyConnection.CreateCommand())
            {
                metadataCommand.CommandText = "SELECT value FROM cache_metadata WHERE key = 'seed_fact_content_version';";
                var storedVersion = Convert.ToString(await metadataCommand.ExecuteScalarAsync(CancellationToken.None));
                Assert.False(string.IsNullOrWhiteSpace(storedVersion));
                Assert.NotEqual("stale-version", storedVersion);
            }
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task PackageIndexCoordinator_RebuildsShadowDatabaseAndRemovesStalePackages()
    {
        var root = CreatePackageRoot();

        try
        {
            var cache = new TestCacheService(root);
            var store = new SqliteIndexStore(cache);
            await store.InitializeAsync(CancellationToken.None);

            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            var stalePackagePath = Path.Combine(root, "stale.package");
            await store.UpsertDataSourcesAsync([source], CancellationToken.None);
            await store.ReplacePackageAsync(
                new PackageScanResult(
                    source.Id,
                    SourceKind.Game,
                    stalePackagePath,
                    10,
                    DateTimeOffset.UtcNow,
                    [CreateNamedResource(source.Id, stalePackagePath, 1, "StaleChair")],
                    []),
                [],
                CancellationToken.None);

            var freshPackages = CreatePackageFiles(root, "fresh.package");
            var scanner = new FakePackageScanner(source, freshPackages);
            var catalog = new TrackingResourceCatalogService(TimeSpan.Zero, 1)
            {
                ScanResourceFactory = packagePath => CreateNamedResource(source.Id, packagePath, 1, "FreshChair")
            };
            var coordinator = new PackageIndexCoordinator(
                scanner,
                catalog,
                new FakeAssetGraphBuilder(),
                store,
                new IndexingRunOptions(1, 2, 100, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(60)));

            var progressEvents = new List<IndexingProgress>();
            await coordinator.RunAsync([source], new Progress<IndexingProgress>(progressEvents.Add), CancellationToken.None);

            var indexedPackages = await store.GetIndexedPackagesAsync([source.Id], CancellationToken.None);
            Assert.Single(indexedPackages);
            Assert.Equal(freshPackages[0], indexedPackages[0].PackagePath);

            var staleRows = await store.GetPackageResourcesAsync(stalePackagePath, CancellationToken.None);
            Assert.Empty(staleRows);

            var freshSearch = await store.QueryResourcesAsync(
                new RawResourceBrowserQuery(
                    new SourceScope(),
                    "FreshChair",
                    RawResourceDomain.All,
                    string.Empty,
                    freshPackages[0],
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    false,
                    ResourceLinkFilter.Any,
                    RawResourceSort.TypeName,
                    0,
                    20),
                CancellationToken.None);
            Assert.Single(freshSearch.Items);
            Assert.Contains(progressEvents, progress => string.Equals(progress.Stage, "finalizing", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(progressEvents, progress => progress.Message.Contains("Activating rebuilt catalog", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task SqliteIndexStore_InitializeCreatesAssetSourceIndex()
    {
        var root = CreatePackageRoot();

        try
        {
            var cache = new TestCacheService(root);
            var store = new SqliteIndexStore(cache);
            await store.InitializeAsync(CancellationToken.None);

            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = cache.DatabasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            }.ToString());
            await connection.OpenAsync(CancellationToken.None);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'ix_assets_source';";
            var count = Convert.ToInt32(await command.ExecuteScalarAsync(CancellationToken.None));
            Assert.Equal(1, count);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task PackageIndexCoordinator_RebuildsShardCatalogAndQueriesAcrossAllShards()
    {
        var root = CreatePackageRoot();

        try
        {
            var cache = new TestCacheService(root);
            var store = new SqliteIndexStore(cache);
            await store.InitializeAsync(CancellationToken.None);

            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            var packagePaths = CreatePackageFiles(
                root,
                "alpha.package",
                "beta.package",
                "gamma.package",
                "delta.package",
                "epsilon.package",
                "zeta.package",
                "eta.package",
                "theta.package");

            var scanner = new FakePackageScanner(source, packagePaths);
            var catalog = new TrackingResourceCatalogService(TimeSpan.Zero, 1)
            {
                ScanResourceFactory = packagePath => CreateNamedResource(
                    source.Id,
                    packagePath,
                    1,
                    $"SharedNeedle {Path.GetFileNameWithoutExtension(packagePath)}")
            };
            var coordinator = new PackageIndexCoordinator(
                scanner,
                catalog,
                new PerPackageAssetGraphBuilder("SharedNeedle"),
                store,
                new IndexingRunOptions(4, 8, 100, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(60)));

            await coordinator.RunAsync([source], progress: null, CancellationToken.None);

            Assert.True(File.Exists(cache.DatabasePath));
            Assert.True(File.Exists(Path.Combine(cache.CacheRoot, "index.shard01.sqlite")));
            Assert.True(File.Exists(Path.Combine(cache.CacheRoot, "index.shard02.sqlite")));
            Assert.True(File.Exists(Path.Combine(cache.CacheRoot, "index.shard03.sqlite")));

            var expectedPackageOrder = packagePaths.OrderBy(static path => path, StringComparer.OrdinalIgnoreCase).ToArray();

            var allResources = await store.QueryResourcesAsync(
                new RawResourceBrowserQuery(
                    new SourceScope(),
                    "SharedNeedle",
                    RawResourceDomain.All,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    false,
                    ResourceLinkFilter.Any,
                    RawResourceSort.PackagePath,
                    0,
                    32),
                CancellationToken.None);
            Assert.Equal(packagePaths.Count, allResources.TotalCount);
            Assert.Equal(expectedPackageOrder, allResources.Items.Select(static item => item.PackagePath).ToArray());

            var pagedResources = await store.QueryResourcesAsync(
                new RawResourceBrowserQuery(
                    new SourceScope(),
                    "SharedNeedle",
                    RawResourceDomain.All,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    false,
                    ResourceLinkFilter.Any,
                    RawResourceSort.PackagePath,
                    2,
                    3),
                CancellationToken.None);
            Assert.Equal(expectedPackageOrder.Skip(2).Take(3).ToArray(), pagedResources.Items.Select(static item => item.PackagePath).ToArray());

            var allAssets = await store.QueryAssetsAsync(
                new AssetBrowserQuery(
                    new SourceScope(),
                    "SharedNeedle",
                    AssetBrowserDomain.BuildBuy,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    AssetBrowserSort.Package,
                    0,
                    32),
                CancellationToken.None);
            Assert.Equal(packagePaths.Count, allAssets.TotalCount);
            Assert.Equal(expectedPackageOrder, allAssets.Items.Select(static item => item.PackagePath).ToArray());

            var packageResources = await store.GetPackageResourcesAsync(packagePaths[3], CancellationToken.None);
            Assert.Single(packageResources);
            Assert.Equal(packagePaths[3], packageResources[0].PackagePath);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task PackageIndexCoordinator_FinalizedShardSchemaUsesUniqueIndexesInsteadOfPrimaryKeys()
    {
        var root = CreatePackageRoot();

        try
        {
            var cache = new TestCacheService(root);
            var store = new SqliteIndexStore(cache);
            await store.InitializeAsync(CancellationToken.None);

            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            var packagePaths = CreatePackageFiles(root, "schema.package");
            var scanner = new FakePackageScanner(source, packagePaths);
            var catalog = new TrackingResourceCatalogService(TimeSpan.Zero, 1)
            {
                ScanResourceFactory = packagePath => CreateNamedResource(source.Id, packagePath, 1, "SchemaNeedle")
            };
            var coordinator = new PackageIndexCoordinator(
                scanner,
                catalog,
                new PerPackageAssetGraphBuilder("SchemaNeedle"),
                store,
                new IndexingRunOptions(1, 2, 100, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(60)));

            await coordinator.RunAsync([source], progress: null, CancellationToken.None);

            foreach (var databasePath in new[]
                     {
                         cache.DatabasePath,
                         Path.Combine(cache.CacheRoot, "index.shard01.sqlite"),
                         Path.Combine(cache.CacheRoot, "index.shard02.sqlite"),
                         Path.Combine(cache.CacheRoot, "index.shard03.sqlite")
                     })
            {
                await using var connection = await OpenConnectionAsync(databasePath);

                var resourcesTableSql = await GetSqlDefinitionAsync(connection, "table", "resources");
                Assert.Contains("id TEXT NOT NULL", resourcesTableSql, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("PRIMARY KEY", resourcesTableSql, StringComparison.OrdinalIgnoreCase);

                var assetsTableSql = await GetSqlDefinitionAsync(connection, "table", "assets");
                Assert.Contains("id TEXT NOT NULL", assetsTableSql, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("PRIMARY KEY", assetsTableSql, StringComparison.OrdinalIgnoreCase);

                Assert.Equal(1, await CountObjectAsync(connection, "index", "ux_resources_id"));
                Assert.Equal(1, await CountObjectAsync(connection, "index", "ux_assets_id"));
            }
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task SqliteIndexStore_AssetBrowserReturnsOneCanonicalAssetAcrossBaseAndDeltaShards()
    {
        var root = CreatePackageRoot();

        try
        {
            var cache = new TestCacheService(root);
            var store = new SqliteIndexStore(cache);
            await store.InitializeAsync(CancellationToken.None);

            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            await store.UpsertDataSourcesAsync([source], CancellationToken.None);

            var basePackagePath = CreatePackageFiles(root, "base.package").Single();
            var baseShard = ComputeCatalogShardIndex(basePackagePath);
            var deltaRoot = Path.Combine(root, "Delta");
            Directory.CreateDirectory(deltaRoot);

            string? deltaPackagePath = null;
            for (var index = 0; index < 64; index++)
            {
                var candidate = Path.Combine(deltaRoot, $"delta_{index:00}.package");
                File.WriteAllText(candidate, string.Empty);
                if (ComputeCatalogShardIndex(candidate) != baseShard)
                {
                    deltaPackagePath = candidate;
                    break;
                }
            }

            Assert.NotNull(deltaPackagePath);

            var rootKey = new ResourceKeyRecord(0x034AEECB, 0x00000000, 0x0000000000002000, "CASPart");
            var canonicalAsset = new AssetSummary(
                StableEntityIds.ForAsset(source.Id, AssetKind.Cas, basePackagePath, rootKey),
                source.Id,
                SourceKind.Game,
                AssetKind.Cas,
                "puHair_TestHero",
                "Hair",
                basePackagePath,
                rootKey,
                null,
                1,
                4,
                string.Empty,
                AssetCapabilitySnapshot.Empty);
            var deltaShadowAsset = new AssetSummary(
                StableEntityIds.ForAsset(source.Id, AssetKind.Cas, deltaPackagePath!, rootKey),
                source.Id,
                SourceKind.Game,
                AssetKind.Cas,
                "CAS Part 0000000000002000",
                "Hair",
                deltaPackagePath!,
                rootKey,
                null,
                1,
                1,
                string.Empty,
                AssetCapabilitySnapshot.Empty);

            await using (var session = await store.OpenRebuildSessionAsync([source], CancellationToken.None))
            {
                await session.ReplacePackagesAsync(
                    [
                        (new PackageScanResult(source.Id, SourceKind.Game, basePackagePath, 10, DateTimeOffset.UtcNow, [], []), [canonicalAsset]),
                        (new PackageScanResult(source.Id, SourceKind.Game, deltaPackagePath!, 11, DateTimeOffset.UtcNow, [], []), [deltaShadowAsset])
                    ],
                    CancellationToken.None);
                await session.FinalizeAsync(progress: null, CancellationToken.None);
            }

            var allAssets = await store.QueryAssetsAsync(
                new AssetBrowserQuery(
                    new SourceScope(),
                    string.Empty,
                    AssetBrowserDomain.Cas,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    AssetBrowserSort.Name,
                    0,
                    20),
                CancellationToken.None);
            Assert.Equal(1, allAssets.TotalCount);
            var winner = Assert.Single(allAssets.Items);
            Assert.Equal("puHair_TestHero", winner.DisplayName);
            Assert.Equal(basePackagePath, winner.PackagePath);

            var searchResults = await store.QueryAssetsAsync(
                new AssetBrowserQuery(
                    new SourceScope(),
                    "puHair_TestHero",
                    AssetBrowserDomain.Cas,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    AssetBrowserSort.Name,
                    0,
                    20),
                CancellationToken.None);
            Assert.Equal(1, searchResults.TotalCount);
            Assert.Equal("puHair_TestHero", Assert.Single(searchResults.Items).DisplayName);

            var assetDatabasePaths = new[]
            {
                cache.DatabasePath,
                Path.Combine(cache.CacheRoot, "index.shard01.sqlite"),
                Path.Combine(cache.CacheRoot, "index.shard02.sqlite"),
                Path.Combine(cache.CacheRoot, "index.shard03.sqlite")
            };

            var canonicalRows = 0;
            foreach (var databasePath in assetDatabasePaths)
            {
                await using var connection = await OpenConnectionAsync(databasePath);
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT COUNT(*)
                    FROM assets
                    WHERE root_tgi = $rootTgi AND COALESCE(is_canonical, 1) = 1;
                    """;
                command.Parameters.AddWithValue("$rootTgi", rootKey.FullTgi);
                canonicalRows += Convert.ToInt32(await command.ExecuteScalarAsync(CancellationToken.None));
            }

            Assert.Equal(1, canonicalRows);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task SqliteIndexStore_BuildBuyCanonicalizesAcrossSharedLogicalRootTgi()
    {
        var root = CreatePackageRoot();

        try
        {
            var cache = new TestCacheService(root);
            var store = new SqliteIndexStore(cache);
            await store.InitializeAsync(CancellationToken.None);

            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            await store.UpsertDataSourcesAsync([source], CancellationToken.None);

            var packagePath = CreatePackageFiles(root, "buildbuy.package").Single();
            const string logicalRootTgi = "01661233:00000000:039E5CF151195E00";

            var rootKeyA = new ResourceKeyRecord(0xC0DB5AE7, 0x00000000, 0x0000000000031D40, "ObjectDefinition");
            var rootKeyB = new ResourceKeyRecord(0xC0DB5AE7, 0x00000000, 0x0000000000031D41, "ObjectDefinition");
            var groupedAssetA = new AssetSummary(
                StableEntityIds.ForAsset(source.Id, AssetKind.BuildBuy, packagePath, rootKeyA),
                source.Id,
                SourceKind.Game,
                AssetKind.BuildBuy,
                "Still LIfe Store Front Prop",
                "Build/Buy",
                packagePath,
                rootKeyA,
                null,
                1,
                4,
                string.Empty,
                AssetCapabilitySnapshot.Empty,
                LogicalRootTgi: logicalRootTgi);
            var groupedAssetB = new AssetSummary(
                StableEntityIds.ForAsset(source.Id, AssetKind.BuildBuy, packagePath, rootKeyB),
                source.Id,
                SourceKind.Game,
                AssetKind.BuildBuy,
                "Still LIfe Store Front Prop",
                "Build/Buy",
                packagePath,
                rootKeyB,
                null,
                1,
                4,
                string.Empty,
                AssetCapabilitySnapshot.Empty,
                LogicalRootTgi: logicalRootTgi);

            await using (var session = await store.OpenRebuildSessionAsync([source], CancellationToken.None))
            {
                await session.ReplacePackagesAsync(
                    [(new PackageScanResult(source.Id, SourceKind.Game, packagePath, 10, DateTimeOffset.UtcNow, [], []), [groupedAssetA, groupedAssetB])],
                    CancellationToken.None);
                await session.FinalizeAsync(progress: null, CancellationToken.None);
            }

            var allAssets = await store.QueryAssetsAsync(
                new AssetBrowserQuery(
                    new SourceScope(),
                    "Still LIfe Store Front Prop",
                    AssetBrowserDomain.BuildBuy,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    AssetBrowserSort.Name,
                    0,
                    20),
                CancellationToken.None);
            Assert.Equal(1, allAssets.TotalCount);
            var winner = Assert.Single(allAssets.Items);
            Assert.Equal("Still LIfe Store Front Prop", winner.DisplayName);
            Assert.Equal(logicalRootTgi, winner.LogicalRootTgi);

            var assetDatabasePaths = new[]
            {
                cache.DatabasePath,
                Path.Combine(cache.CacheRoot, "index.shard01.sqlite"),
                Path.Combine(cache.CacheRoot, "index.shard02.sqlite"),
                Path.Combine(cache.CacheRoot, "index.shard03.sqlite")
            };

            var canonicalRows = 0;
            foreach (var databasePath in assetDatabasePaths)
            {
                await using var connection = await OpenConnectionAsync(databasePath);
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT COUNT(*)
                    FROM assets
                    WHERE logical_root_tgi = $logicalRootTgi AND COALESCE(is_canonical, 1) = 1;
                    """;
                command.Parameters.AddWithValue("$logicalRootTgi", logicalRootTgi);
                canonicalRows += Convert.ToInt32(await command.ExecuteScalarAsync(CancellationToken.None));
            }

            Assert.Equal(1, canonicalRows);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task SqliteIndexStore_BuildBuyCanonicalizesCatalogOnlyEntriesViaMatchingObjectDefinitionIdentity()
    {
        var root = CreatePackageRoot();

        try
        {
            var cache = new TestCacheService(root);
            var store = new SqliteIndexStore(cache);
            await store.InitializeAsync(CancellationToken.None);

            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            await store.UpsertDataSourcesAsync([source], CancellationToken.None);

            var fullPackagePath = CreatePackageFiles(root, "sp54-full.package").Single();
            var deltaPackagePath = CreatePackageFiles(root, "sp54-delta.package").Single();
            const string logicalRootTgi = "01661233:00000000:D2C57F6424389E2F";
            const string displayName = "\"Anything's a Surface\" Pedestal";

            var definitionKeyA = new ResourceKeyRecord(0xC0DB5AE7, 0x00000000, 0x000000000006B18B, "ObjectDefinition");
            var definitionKeyB = new ResourceKeyRecord(0xC0DB5AE7, 0x00000000, 0x000000000006B18C, "ObjectDefinition");
            var catalogOnlyKey = new ResourceKeyRecord(0x319E4F1D, 0x00000000, 0x000000000006B18C, "ObjectCatalog");
            var fullScan = new PackageScanResult(
                source.Id,
                SourceKind.Game,
                fullPackagePath,
                10,
                DateTimeOffset.UtcNow,
                [
                    new ResourceMetadata(
                        Guid.NewGuid(),
                        source.Id,
                        SourceKind.Game,
                        fullPackagePath,
                        definitionKeyA,
                        "pedestal_SP54GENstool_set1",
                        null,
                        null,
                        null,
                        PreviewKind.Metadata,
                        true,
                        true,
                        string.Empty,
                        string.Empty,
                        SceneRootTgiHint: logicalRootTgi),
                    new ResourceMetadata(
                        Guid.NewGuid(),
                        source.Id,
                        SourceKind.Game,
                        fullPackagePath,
                        definitionKeyB,
                        "pedestal_SP54GENstool_set2",
                        null,
                        null,
                        null,
                        PreviewKind.Metadata,
                        true,
                        true,
                        string.Empty,
                        string.Empty,
                        SceneRootTgiHint: logicalRootTgi)
                ],
                []);

            var groupedFamilyAsset = new AssetSummary(
                StableEntityIds.ForAsset(source.Id, AssetKind.BuildBuy, fullPackagePath, definitionKeyA),
                source.Id,
                SourceKind.Game,
                AssetKind.BuildBuy,
                displayName,
                "Build/Buy",
                fullPackagePath,
                definitionKeyA,
                null,
                2,
                4,
                string.Empty,
                AssetCapabilitySnapshot.Empty,
                PackageName: Path.GetFileName(fullPackagePath),
                RootTypeName: "ObjectDefinition",
                IdentityType: "ObjectDefinition",
                LogicalRootTgi: logicalRootTgi);
            var catalogOnlyDelta = new AssetSummary(
                StableEntityIds.ForAsset(source.Id, AssetKind.BuildBuy, deltaPackagePath, catalogOnlyKey),
                source.Id,
                SourceKind.Game,
                AssetKind.BuildBuy,
                displayName,
                "Build/Buy",
                deltaPackagePath,
                catalogOnlyKey,
                null,
                1,
                0,
                string.Empty,
                AssetCapabilitySnapshot.Empty,
                PackageName: Path.GetFileName(deltaPackagePath),
                RootTypeName: "ObjectCatalog",
                IdentityType: "ObjectCatalog");
            var deltaScan = new PackageScanResult(
                source.Id,
                SourceKind.Game,
                deltaPackagePath,
                10,
                DateTimeOffset.UtcNow,
                [
                    new ResourceMetadata(
                        Guid.NewGuid(),
                        source.Id,
                        SourceKind.Game,
                        deltaPackagePath,
                        catalogOnlyKey,
                        displayName,
                        null,
                        null,
                        null,
                        PreviewKind.Metadata,
                        true,
                        true,
                        string.Empty,
                        string.Empty)
                ],
                []);

            await using (var session = await store.OpenRebuildSessionAsync([source], CancellationToken.None))
            {
                await session.ReplacePackagesAsync(
                    [
                        (fullScan, [groupedFamilyAsset]),
                        (deltaScan, [catalogOnlyDelta])
                    ],
                    CancellationToken.None);
                await session.FinalizeAsync(progress: null, CancellationToken.None);
            }

            var results = await store.QueryAssetsAsync(
                new AssetBrowserQuery(
                    new SourceScope(),
                    displayName,
                    AssetBrowserDomain.BuildBuy,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    AssetBrowserSort.Name,
                    0,
                    20),
                CancellationToken.None);
            Assert.Equal(1, results.TotalCount);
            var winner = Assert.Single(results.Items);
            Assert.Equal(displayName, winner.DisplayName);
            Assert.Equal(logicalRootTgi, winner.LogicalRootTgi);

            var assetDatabasePaths = new[]
            {
                cache.DatabasePath,
                Path.Combine(cache.CacheRoot, "index.shard01.sqlite"),
                Path.Combine(cache.CacheRoot, "index.shard02.sqlite"),
                Path.Combine(cache.CacheRoot, "index.shard03.sqlite")
            };

            var canonicalRows = 0;
            var catalogOnlyLogicalRootMatches = 0;
            foreach (var databasePath in assetDatabasePaths)
            {
                await using var connection = await OpenConnectionAsync(databasePath);

                await using (var canonicalCommand = connection.CreateCommand())
                {
                    canonicalCommand.CommandText =
                        """
                        SELECT COUNT(*)
                        FROM assets
                        WHERE display_name = $displayName AND COALESCE(is_canonical, 1) = 1;
                        """;
                    canonicalCommand.Parameters.AddWithValue("$displayName", displayName);
                    canonicalRows += Convert.ToInt32(await canonicalCommand.ExecuteScalarAsync(CancellationToken.None));
                }

                await using (var catalogCommand = connection.CreateCommand())
                {
                    catalogCommand.CommandText =
                        """
                        SELECT COUNT(*)
                        FROM assets
                        WHERE root_tgi = $rootTgi AND logical_root_tgi = $logicalRootTgi;
                        """;
                    catalogCommand.Parameters.AddWithValue("$rootTgi", catalogOnlyKey.FullTgi);
                    catalogCommand.Parameters.AddWithValue("$logicalRootTgi", logicalRootTgi);
                    catalogOnlyLogicalRootMatches += Convert.ToInt32(await catalogCommand.ExecuteScalarAsync(CancellationToken.None));
                }
            }

            Assert.Equal(1, canonicalRows);
            Assert.Equal(1, catalogOnlyLogicalRootMatches);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task ResourceMetadataEnrichmentService_EnrichesDeferredMetadataWithoutPersistingToIndex()
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

        var service = new ResourceMetadataEnrichmentService(catalog);
        var enriched = await service.EnrichAsync(resource, CancellationToken.None);

        Assert.Equal("Enriched", enriched.Name);
        Assert.Equal(1, catalog.EnrichCalls);
        Assert.Empty(store.EnrichedResources);
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
    public async Task PackageIndexCoordinator_EnrichesSeedTechnicalNamesBeforeBuildingAssetSummaries()
    {
        var root = CreatePackageRoot();

        try
        {
            var packagePaths = CreatePackageFiles(root, "identity.package");
            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            var scanner = new FakePackageScanner(source, packagePaths);
            var store = new FakeIndexStore();
            var objectDefinition = new ResourceMetadata(
                Guid.NewGuid(),
                source.Id,
                source.Kind,
                packagePaths[0],
                new ResourceKeyRecord(0xC0DB5AE7, 0x00000000, 0x0000000000000001, "ObjectDefinition"),
                null,
                null,
                null,
                null,
                PreviewKind.Hex,
                true,
                true,
                "Build/Buy seed",
                string.Empty);
            var catalog = new TrackingResourceCatalogService(TimeSpan.FromMilliseconds(5), 1)
            {
                ScanResourceFactory = _ => objectDefinition,
                ResourceBytesFactory = resource => resource.Key.TypeName == "ObjectDefinition"
                    ? CreateSyntheticObjectDefinitionBytes("Chair_Internal")
                    : throw new NotSupportedException()
            };

            var coordinator = new PackageIndexCoordinator(
                scanner,
                catalog,
                new ExplicitAssetGraphBuilder(catalog),
                store,
                new IndexingRunOptions(1, 1, 100, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(60)));

            await coordinator.RunAsync([source], progress: null, CancellationToken.None);

            Assert.Equal(0, catalog.EnrichCalls);
            Assert.Equal(1, catalog.ResourceBytesCalls);
            var persisted = store.PersistedScans.Single().Resources.Single();
            Assert.Equal("Chair_Internal", persisted.Name);
            var asset = store.PersistedAssets[packagePaths[0]].Single();
            Assert.Equal("Chair_Internal", asset.DisplayName);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Theory]
    [InlineData("CASPreset", "CAS preset v12", "region=Neck")]
    [InlineData("RegionMap", "Region map v1", "entries=2")]
    [InlineData("Skintone", "Skintone v6", "overlays=2")]
    public async Task PackageIndexCoordinator_EnrichesStructuredSeedDescriptionsBeforePersistingRawResources(
        string typeName,
        string expectedSnippet,
        string expectedSecondarySnippet)
    {
        var root = CreatePackageRoot();

        try
        {
            var packagePaths = CreatePackageFiles(root, $"{typeName}.package");
            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            var scanner = new FakePackageScanner(source, packagePaths);
            var store = new FakeIndexStore();
            var resource = CreateStructuredSeedResource(source, packagePaths[0], typeName);
            var catalog = new TrackingResourceCatalogService(TimeSpan.FromMilliseconds(5), 1)
            {
                ScanResourceFactory = _ => resource,
                ResourceBytesFactory = candidate => candidate.Key.TypeName == typeName
                    ? CreateStructuredSeedBytes(typeName)
                    : throw new NotSupportedException()
            };

            var coordinator = new PackageIndexCoordinator(
                scanner,
                catalog,
                new FakeAssetGraphBuilder(),
                store,
                new IndexingRunOptions(1, 1, 100, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(60)));

            await coordinator.RunAsync([source], progress: null, CancellationToken.None);

            Assert.Equal(0, catalog.EnrichCalls);
            Assert.Equal(1, catalog.ResourceBytesCalls);
            var persisted = store.PersistedScans.Single().Resources.Single();
            Assert.Null(persisted.Name);
            var description = Assert.IsType<string>(persisted.Description);
            Assert.Contains(expectedSnippet, description, StringComparison.Ordinal);
            Assert.Contains(expectedSecondarySnippet, description, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task PackageIndexCoordinator_SlicedPackagePersistsSimTemplateFactsOnlyOnFinalChunk()
    {
        var root = CreatePackageRoot();

        try
        {
            var packagePaths = CreatePackageFiles(root, "sim-large.package");
            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            var scanner = new FakePackageScanner(source, packagePaths);
            var store = new FakeIndexStore();
            var packagePath = packagePaths[0];
            var resources = Enumerable.Range(0, 2050)
                .Select(index => new ResourceMetadata(
                    Guid.NewGuid(),
                    source.Id,
                    source.Kind,
                    packagePath,
                    new ResourceKeyRecord(0x545AC67A, 0x00000001, (ulong)index + 1, "CombinedTuning"),
                    $"Resource {index}",
                    100L,
                    120L,
                    false,
                    PreviewKind.Hex,
                    true,
                    true,
                    "Synthetic",
                    string.Empty))
                .ToArray();
            var simInfo = new ResourceMetadata(
                Guid.NewGuid(),
                source.Id,
                source.Kind,
                packagePath,
                new ResourceKeyRecord(0x025ED6F4, 0, 0xFDCCF77200000001ul, "SimInfo"),
                "SimInfo template: Human | Young Adult | Female | outfits 1/1/2 [00000001]",
                100L,
                120L,
                false,
                PreviewKind.Hex,
                true,
                true,
                "Sim/character seed",
                string.Empty,
                Description: "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=1 categories / 1 entries / 2 parts");
            var fact = new SimTemplateFactSummary(
                simInfo.Id,
                source.Id,
                source.Kind,
                packagePath,
                simInfo.Key.FullTgi,
                "sim-archetype:human|young-adult|female",
                "Human",
                "Young Adult",
                "Female",
                simInfo.Name!,
                simInfo.Description!,
                1,
                1,
                2,
                4,
                8,
                1,
                true,
                1,
                2);
            var bodyPart = new SimTemplateBodyPartFact(
                simInfo.Id,
                source.Id,
                source.Kind,
                packagePath,
                simInfo.Key.FullTgi,
                5,
                "Nude",
                0,
                0,
                5,
                "Full Body",
                0x6000000000000031ul,
                true);
            var catalog = new TrackingResourceCatalogService(TimeSpan.Zero, 1)
            {
                ScanResultFactory = (_, _) => new PackageScanResult(
                    source.Id,
                    source.Kind,
                    packagePath,
                    10,
                    DateTimeOffset.UtcNow,
                    resources,
                    [])
                {
                    SimTemplateFacts = [fact],
                    SimTemplateBodyPartFacts = [bodyPart]
                }
            };

            var coordinator = new PackageIndexCoordinator(
                scanner,
                catalog,
                new FakeAssetGraphBuilder(),
                store,
                new IndexingRunOptions(1, 2, 100, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(60)));

            await coordinator.RunAsync([source], progress: null, CancellationToken.None);

            Assert.Equal(2, store.PersistedScans.Count);
            Assert.Empty(store.PersistedScans[0].SimTemplateFacts);
            Assert.Empty(store.PersistedScans[0].SimTemplateBodyPartFacts);
            Assert.Single(store.PersistedScans[1].SimTemplateFacts);
            Assert.Single(store.PersistedScans[1].SimTemplateBodyPartFacts);
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

            var finalSnapshot = progressEvents.LastOrDefault(progress => progress.Summary is not null) ?? progressEvents.Last();
            Assert.NotNull(finalSnapshot.RecentEvents);
            Assert.True(finalSnapshot.RecentEvents!.Count <= 5);
            Assert.Contains(finalSnapshot.RecentEvents, activity => activity.Kind is "complete" or "failed");
            if (finalSnapshot.Summary is not null)
            {
                Assert.Equal(4, finalSnapshot.Summary.PackagesDiscovered);
                Assert.Equal(4, finalSnapshot.Summary.PackagesQueued);
            }
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task PackageIndexCoordinator_ReportsByteWeightedProgressAndSummary()
    {
        var root = CreatePackageRoot();

        try
        {
            var packagePaths = CreateSizedPackageFiles(root, ("small.package", 1024), ("medium.package", 3 * 1024), ("large.package", 5 * 1024));
            var source = new DataSourceDefinition(Guid.NewGuid(), "Game", root, SourceKind.Game);
            var scanner = new FakePackageScanner(source, packagePaths);
            var store = new FakeIndexStore();
            var catalog = new TrackingResourceCatalogService(delayPerPackage: TimeSpan.Zero, progressBursts: 1);
            var coordinator = new PackageIndexCoordinator(
                scanner,
                catalog,
                new FakeAssetGraphBuilder(),
                store,
                new IndexingRunOptions(1, 4, 100, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(40)));

            var progressEvents = new List<IndexingProgress>();
            await coordinator.RunAsync([source], new Progress<IndexingProgress>(progressEvents.Add), CancellationToken.None);

            var finalSnapshot = progressEvents.Last();
            Assert.Equal(9 * 1024, finalSnapshot.PackageBytesTotal);
            Assert.Equal(9 * 1024, finalSnapshot.PackageBytesProcessed);
            var summary = finalSnapshot.Summary ?? progressEvents.Select(static progress => progress.Summary).LastOrDefault(static summary => summary is not null);
            if (summary is not null)
            {
                Assert.Equal(9 * 1024, summary.PackageBytesDiscovered);
                Assert.Equal(9 * 1024, summary.PackageBytesProcessed);
            }
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

    private static ResourceMetadata CreateNamedResource(Guid sourceId, string packagePath, int index, string name) =>
        CreateResource(sourceId, packagePath, index) with { Name = name };

    private static AssetSummary CreateAsset(Guid sourceId, string packagePath, string displayName) =>
        new(
            Guid.NewGuid(),
            sourceId,
            SourceKind.Game,
            AssetKind.BuildBuy,
            displayName,
            "Decor",
            packagePath,
            new ResourceKeyRecord(0x319E4F1D, 0x00000001, 0x0000000000001000, "ObjectCatalog"),
            null,
            1,
            1,
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

    private static IReadOnlyList<string> CreateSizedPackageFiles(string root, params (string Name, int Size)[] packages)
    {
        var results = new List<string>(packages.Length);
        foreach (var (name, size) in packages)
        {
            var path = Path.Combine(root, name);
            File.WriteAllBytes(path, new byte[size]);
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
        public int ResourceBytesCalls { get; private set; }
        public Func<DataSourceDefinition, string, PackageScanResult>? ScanResultFactory { get; init; }
        public Func<string, ResourceMetadata>? ScanResourceFactory { get; init; }
        public Func<ResourceMetadata, ResourceMetadata>? EnrichedResourceFactory { get; init; }
        public Func<ResourceMetadata, byte[]>? ResourceBytesFactory { get; init; }

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

                if (ScanResultFactory is not null)
                {
                    return ScanResultFactory(source, packagePath);
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

        public Task<byte[]> GetResourceBytesAsync(string packagePath, ResourceKeyRecord key, bool raw, CancellationToken cancellationToken, IProgress<ResourceReadProgress>? progress = null)
        {
            ResourceBytesCalls++;
            if (ResourceBytesFactory is null)
            {
                throw new NotSupportedException();
            }

            var resource = new ResourceMetadata(
                Guid.NewGuid(),
                Guid.Empty,
                SourceKind.Game,
                packagePath,
                key,
                null,
                null,
                null,
                null,
                PreviewKind.Hex,
                true,
                true,
                string.Empty,
                string.Empty);
            return Task.FromResult(ResourceBytesFactory(resource));
        }

        public Task<string?> GetTextAsync(string packagePath, ResourceKeyRecord key, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<byte[]?> GetTexturePngAsync(string packagePath, ResourceKeyRecord key, CancellationToken cancellationToken, IProgress<ResourceReadProgress>? progress = null) =>
            throw new NotSupportedException();
    }

    private sealed class FakeAssetGraphBuilder : IAssetGraphBuilder
    {
        public Task<AssetGraph> BuildAssetGraphAsync(AssetSummary summary, IReadOnlyList<ResourceMetadata> packageResources, CancellationToken cancellationToken) =>
            Task.FromResult(new AssetGraph(summary, packageResources, []));

        public Task<AssetGraph> BuildPreviewGraphAsync(AssetSummary summary, IReadOnlyList<ResourceMetadata> packageResources, CancellationToken cancellationToken) =>
            BuildAssetGraphAsync(summary, packageResources, cancellationToken);

        public IReadOnlyList<AssetSummary> BuildAssetSummaries(PackageScanResult packageScan) =>
            [];
    }

    private sealed class SingleAssetGraphBuilder(AssetSummary summary) : IAssetGraphBuilder
    {
        public Task<AssetGraph> BuildAssetGraphAsync(AssetSummary summary, IReadOnlyList<ResourceMetadata> packageResources, CancellationToken cancellationToken) =>
            Task.FromResult(new AssetGraph(summary, packageResources, []));

        public Task<AssetGraph> BuildPreviewGraphAsync(AssetSummary summary, IReadOnlyList<ResourceMetadata> packageResources, CancellationToken cancellationToken) =>
            BuildAssetGraphAsync(summary, packageResources, cancellationToken);

        public IReadOnlyList<AssetSummary> BuildAssetSummaries(PackageScanResult packageScan) => [summary];
    }

    private sealed class PerPackageAssetGraphBuilder(string searchToken) : IAssetGraphBuilder
    {
        public Task<AssetGraph> BuildAssetGraphAsync(AssetSummary summary, IReadOnlyList<ResourceMetadata> packageResources, CancellationToken cancellationToken) =>
            Task.FromResult(new AssetGraph(summary, packageResources, []));

        public Task<AssetGraph> BuildPreviewGraphAsync(AssetSummary summary, IReadOnlyList<ResourceMetadata> packageResources, CancellationToken cancellationToken) =>
            BuildAssetGraphAsync(summary, packageResources, cancellationToken);

        public IReadOnlyList<AssetSummary> BuildAssetSummaries(PackageScanResult packageScan) =>
        [
            new AssetSummary(
                StableEntityIds.ForAsset(
                    packageScan.DataSourceId,
                    AssetKind.BuildBuy,
                    packageScan.PackagePath,
                    new ResourceKeyRecord(
                        0x319E4F1D,
                        0x00000001,
                        BitConverter.ToUInt64(System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(packageScan.PackagePath)), 0),
                        "ObjectCatalog")),
                packageScan.DataSourceId,
                SourceKind.Game,
                AssetKind.BuildBuy,
                $"{Path.GetFileNameWithoutExtension(packageScan.PackagePath)} {searchToken}",
                "Decor",
                packageScan.PackagePath,
                new ResourceKeyRecord(
                    0x319E4F1D,
                    0x00000001,
                    BitConverter.ToUInt64(System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(packageScan.PackagePath)), 0),
                    "ObjectCatalog"),
                null,
                1,
                1,
                string.Empty)
        ];
    }

    private sealed class FakeIndexStore : IIndexStore
    {
        public List<string> ReplacedPackages { get; } = [];
        public List<PackageScanResult> PersistedScans { get; } = [];
        public Dictionary<string, IReadOnlyList<AssetSummary>> PersistedAssets { get; } = new(StringComparer.OrdinalIgnoreCase);
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
        public Task<AssetFacetOptions> GetAssetFacetOptionsAsync(AssetKind assetKind, CancellationToken cancellationToken) =>
            Task.FromResult(new AssetFacetOptions([], [], [], [], []));
        public Task<IReadOnlyList<IndexedPackageRecord>> GetIndexedPackagesAsync(IEnumerable<Guid> dataSourceIds, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<IndexedPackageRecord>>([]);
        public Task<IReadOnlyList<ResourceMetadata>> GetPackageResourcesAsync(string packagePath, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ResourceMetadata>>([]);
        public Task<IReadOnlyList<ResourceMetadata>> GetResourcesByInstanceAsync(string packagePath, ulong fullInstance, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ResourceMetadata>>([]);
        public Task<IReadOnlyList<ResourceMetadata>> GetResourcesByFullInstanceAsync(ulong fullInstance, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ResourceMetadata>>([]);
        public Task<IReadOnlyList<ResourceMetadata>> GetCasPartResourcesByInstancesAsync(IEnumerable<ulong> fullInstances, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ResourceMetadata>>([]);
        public Task<IReadOnlyList<AssetSummary>> GetPackageAssetsAsync(string packagePath, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AssetSummary>>([]);
        public Task<AssetSummary?> GetPackageAssetByIdAsync(string packagePath, Guid assetId, CancellationToken cancellationToken) => Task.FromResult<AssetSummary?>(null);
        public Task<IReadOnlyList<AssetVariantSummary>> GetAssetVariantsAsync(Guid assetId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AssetVariantSummary>>([]);
        public Task<ResourceMetadata?> GetResourceByTgiAsync(string packagePath, string fullTgi, CancellationToken cancellationToken) => Task.FromResult<ResourceMetadata?>(null);
        public Task<IReadOnlyList<ResourceMetadata>> GetResourcesByTgiAsync(string fullTgi, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ResourceMetadata>>([]);
        public Task<IReadOnlyList<AssetSummary>> GetIndexedDefaultBodyRecipeAssetsAsync(SimInfoSummary metadata, string slotCategory, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AssetSummary>>([]);
        public Task<IReadOnlyList<SimTemplateFactSummary>> GetSimTemplateFactsByArchetypeAsync(string archetypeKey, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<SimTemplateFactSummary>>([]);
        public Task<IReadOnlyList<SimTemplateBodyPartFact>> GetSimTemplateBodyPartFactsAsync(Guid resourceId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<SimTemplateBodyPartFact>>([]);
        public Task UpdatePackageAssetsAsync(Guid dataSourceId, string packagePath, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ReplacePackageAsync(PackageScanResult packageScan, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken)
        {
            Persist(packageScan, assets);
            return Task.CompletedTask;
        }

        public Task<IIndexWriteSession> OpenWriteSessionAsync(CancellationToken cancellationToken)
        {
            OpenWriteSessionCount++;
            return Task.FromResult<IIndexWriteSession>(new FakeWriteSession(this));
        }

        public Task<IIndexWriteSession> OpenRebuildSessionAsync(IEnumerable<DataSourceDefinition> sources, CancellationToken cancellationToken)
        {
            OpenWriteSessionCount++;
            return Task.FromResult<IIndexWriteSession>(new FakeWriteSession(this));
        }

        private void Persist(PackageScanResult packageScan)
        {
            ReplacedPackages.Add(packageScan.PackagePath);
            PersistedScans.Add(packageScan);
        }

        private void Persist(PackageScanResult packageScan, IReadOnlyList<AssetSummary> assets)
        {
            Persist(packageScan);
            PersistedAssets[packageScan.PackagePath] = assets;
        }

        private sealed class FakeWriteSession(FakeIndexStore store) : IIndexWriteSession, IIndexWriteSessionMetricsProvider
        {
            private IndexWriteMetrics? lastMetrics;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;

            public Task ReplacePackageAsync(PackageScanResult packageScan, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken)
            {
                store.Persist(packageScan, assets);
                lastMetrics = new IndexWriteMetrics(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, packageScan.Resources.Count, assets.Count, 1);
                return Task.CompletedTask;
            }

            public Task ReplacePackagesAsync(IReadOnlyList<(PackageScanResult PackageScan, IReadOnlyList<AssetSummary> Assets)> batch, CancellationToken cancellationToken)
            {
                foreach (var item in batch)
                {
                    store.Persist(item.PackageScan, item.Assets);
                }

                lastMetrics = new IndexWriteMetrics(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, batch.Sum(static item => item.PackageScan.Resources.Count), batch.Sum(static item => item.Assets.Count), batch.Count);
                return Task.CompletedTask;
            }

            public Task FinalizeAsync(IProgress<IndexWriteStageProgress>? progress, CancellationToken cancellationToken)
            {
                progress?.Report(new IndexWriteStageProgress("finalizing", "Building browse indexes."));
                return Task.CompletedTask;
            }

            public IndexWriteMetrics? ConsumeLastMetrics()
            {
                var metrics = lastMetrics;
                lastMetrics = null;
                return metrics;
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

    private static async Task<SqliteConnection> OpenConnectionAsync(string databasePath)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString());
        await connection.OpenAsync(CancellationToken.None);
        return connection;
    }

    private static async Task<string> GetSqlDefinitionAsync(SqliteConnection connection, string objectType, string objectName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT sql FROM sqlite_master WHERE type = $type AND name = $name;";
        command.Parameters.AddWithValue("$type", objectType);
        command.Parameters.AddWithValue("$name", objectName);
        return Convert.ToString(await command.ExecuteScalarAsync(CancellationToken.None)) ?? string.Empty;
    }

    private static async Task<int> CountObjectAsync(SqliteConnection connection, string objectType, string objectName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = $type AND name = $name;";
        command.Parameters.AddWithValue("$type", objectType);
        command.Parameters.AddWithValue("$name", objectName);
        return Convert.ToInt32(await command.ExecuteScalarAsync(CancellationToken.None));
    }

    private static int ComputeCatalogShardIndex(string packagePath)
    {
        var input = Encoding.UTF8.GetBytes(packagePath);
        var hash = System.Security.Cryptography.MD5.HashData(input);
        return (int)(BitConverter.ToUInt32(hash, 0) % 4);
    }

    private static byte[] CreateSyntheticObjectDefinitionBytes(string internalName)
    {
        var nameBytes = Encoding.ASCII.GetBytes(internalName);
        var bytes = new byte[10 + nameBytes.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0, 2), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(2, 4), (uint)bytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(6, 4), (uint)nameBytes.Length);
        nameBytes.CopyTo(bytes.AsSpan(10));
        return bytes;
    }

    private static ResourceMetadata CreateStructuredSeedResource(DataSourceDefinition source, string packagePath, string typeName) =>
        new(
            Guid.NewGuid(),
            source.Id,
            source.Kind,
            packagePath,
            new ResourceKeyRecord(GetStructuredSeedTypeId(typeName), 0x00000000, 0x0000000000000001, typeName),
            null,
            null,
            null,
            null,
            PreviewKind.Hex,
            true,
            true,
            $"{typeName} seed",
            string.Empty);

    private static uint GetStructuredSeedTypeId(string typeName) => typeName switch
    {
        "CASPreset" => 0xC9C81E27,
        "RegionMap" => 0xAC16FBEC,
        "Skintone" => 0x0354796A,
        _ => throw new NotSupportedException(typeName)
    };

    private static byte[] CreateStructuredSeedBytes(string typeName) => typeName switch
    {
        "CASPreset" => CreateSyntheticCasPresetBytes(),
        "RegionMap" => CreateSyntheticRegionMapBytes(),
        "Skintone" => CreateSyntheticSkintoneBytes(),
        _ => throw new NotSupportedException(typeName)
    };

    private static byte[] CreateSyntheticCasPresetBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(12u);
        writer.Write(0x00003030u);
        writer.Write(0x00002000u);
        writer.Write(0x00000001u);
        writer.Write(16u);
        writer.Write(12u);
        writer.Write(0x00000001u);
        writer.Write(1.25f);
        writer.Write(0x11223344u);
        writer.Write(0x55667788u);

        writer.Write(1u);
        writer.Write(0x0123456789ABCDEFul);

        writer.Write(2u);
        writer.Write(0x1000000000000001ul);
        writer.Write(0.35f);
        writer.Write(0x2000000000000002ul);
        writer.Write(0.85f);

        writer.Write((byte)1);
        writer.Write(0.1f);
        writer.Write(0.2f);
        writer.Write(0.3f);
        writer.Write(0.4f);

        writer.Write((byte)1);
        writer.Write(0xCAFEBABE00000010ul);
        writer.Write(12u);

        writer.Write(0.75f);
        writer.Write(2u);
        writer.Write((ushort)14);
        writer.Write(0x01020304u);
        writer.Write((ushort)16);
        writer.Write(0x05060708u);
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] CreateSyntheticRegionMapBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(3u);
        writer.Write(1u);
        writer.Write(1u);
        writer.Write(0u);
        writer.Write(1u);

        WriteResourceKey(writer, new ResourceKeyRecord(0x015A1849u, 0u, 0x1111111111111111ul, "Geometry"));
        WriteResourceKey(writer, new ResourceKeyRecord(0x015A1849u, 0u, 0x2222222222222222ul, "Geometry"));
        writer.Write(0u);
        writer.Write(16u);

        writer.Write(1u);
        writer.Write(2u);

        writer.Write(13u);
        writer.Write(1.5f);
        writer.Write((byte)0);
        writer.Write(1u);
        WriteResourceKey(writer, new ResourceKeyRecord(0x015A1849u, 0u, 0x3333333333333333ul, "Geometry"));

        writer.Write(14u);
        writer.Write(2.0f);
        writer.Write((byte)1);
        writer.Write(2u);
        WriteResourceKey(writer, new ResourceKeyRecord(0x015A1849u, 0u, 0x4444444444444444ul, "Geometry"));
        WriteResourceKey(writer, new ResourceKeyRecord(0x015A1849u, 0u, 0x5555555555555555ul, "Geometry"));

        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] CreateSyntheticSkintoneBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(6u);
        writer.Write(0x1020304050607080ul);
        writer.Write(2u);
        writer.Write(0x00003030u);
        writer.Write(0x1111111111111111ul);
        writer.Write(0x0000C000u);
        writer.Write(0x2222222222222222ul);
        writer.Write(0x00FF8040u);
        writer.Write(85u);
        writer.Write(1u);
        writer.Write((ushort)7);
        writer.Write((ushort)42);
        writer.Write(0.55f);
        writer.Write((byte)3);
        writer.Write(0xFFBBAA99u);
        writer.Write(0xFF112233u);
        writer.Write(0xFF445566u);
        writer.Write(2.5f);
        writer.Write(0.65f);
        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteResourceKey(BinaryWriter writer, ResourceKeyRecord key)
    {
        writer.Write(key.Type);
        writer.Write(key.Group);
        writer.Write(key.FullInstance);
    }
}
