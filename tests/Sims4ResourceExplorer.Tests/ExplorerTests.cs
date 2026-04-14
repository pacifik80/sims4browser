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
    public void AssetGraphBuilder_BuildBuySummary_PrefersObjectCatalogDisplayNameOverObjectDefinitionTechnicalName()
    {
        var builder = new ExplicitAssetGraphBuilder(new FakeResourceCatalogService(new Dictionary<string, byte[]>(), new Dictionary<string, string?>()));
        var packagePath = Path.Combine(tempRoot, "localized.package");
        var sourceId = Guid.NewGuid();
        var resources = new[]
        {
            CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectCatalog", 1, "Unpolished Geode"),
            CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectDefinition", 1, "collectGeode_EP01GENrough"),
            CreateResource(sourceId, packagePath, SourceKind.Game, "Model", 1, "collectGeode_EP01GENrough"),
            CreateResource(sourceId, packagePath, SourceKind.Game, "ModelLOD", 1),
        };

        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, resources, []))
            .Single(asset => asset.AssetKind == AssetKind.BuildBuy);

        Assert.Equal("Unpolished Geode", summary.DisplayName);
    }

    [Fact]
    public void AssetGraphBuilder_BuildBuySummary_CarriesObjectCatalogDescription()
    {
        var builder = new ExplicitAssetGraphBuilder(new FakeResourceCatalogService(new Dictionary<string, byte[]>(), new Dictionary<string, string?>()));
        var packagePath = Path.Combine(tempRoot, "described.package");
        var sourceId = Guid.NewGuid();
        var resources = new[]
        {
            CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectCatalog", 1, "Unpolished Geode") with { Description = "What lies beneath the surface of this alien space rock?" },
            CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectDefinition", 1, "collectGeode_EP01GENrough"),
            CreateResource(sourceId, packagePath, SourceKind.Game, "Model", 1, "collectGeode_EP01GENrough"),
            CreateResource(sourceId, packagePath, SourceKind.Game, "ModelLOD", 1),
        };

        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, resources, []))
            .Single(asset => asset.AssetKind == AssetKind.BuildBuy);

        Assert.Equal("What lies beneath the surface of this alien space rock?", summary.Description);
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
    public async Task AssetGraphBuilder_BuildsExplicitBuildBuyGraph_IncludesParsedObjectDefinitionInternalName()
    {
        var packagePath = Path.Combine(tempRoot, "objectdefinition.package");
        var sourceId = Guid.NewGuid();
        var model = CreateResource(sourceId, packagePath, SourceKind.Game, "Model", 1, "Chair");
        var objectDefinition = CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectDefinition", 1);
        var resources = new[]
        {
            CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectCatalog", 1),
            objectDefinition,
            model,
            CreateResource(sourceId, packagePath, SourceKind.Game, "ModelLOD", 1),
            CreateResource(sourceId, packagePath, SourceKind.Game, "PNGImage", 1),
            CreateResource(sourceId, packagePath, SourceKind.Game, "MaterialDefinition", 1)
        };

        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [model.Key.FullTgi] = [],
                [objectDefinition.Key.FullTgi] = CreateSyntheticObjectDefinitionBytes("ArchEP03_DockApartment_6_01", 2, 173, 0x8EA7CE98u, 0x84911568u)
            },
            new Dictionary<string, string?>());
        var builder = new ExplicitAssetGraphBuilder(catalog);

        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, resources, [])).Single();
        var graph = await builder.BuildAssetGraphAsync(summary, resources, CancellationToken.None);

        Assert.Contains(graph.Diagnostics, message => message.Contains("ObjectDefinition internal name: ArchEP03_DockApartment_6_01", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildsExplicitBuildBuyGraph_IncludesObjectDefinitionReferenceCandidateDiagnostics()
    {
        var packagePath = Path.Combine(tempRoot, "objectdefinition-refs.package");
        var sourceId = Guid.NewGuid();
        var model = CreateResource(sourceId, packagePath, SourceKind.Game, "Model", 1, "Chair");
        var objectDefinition = CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectDefinition", 1);
        var resources = new[]
        {
            CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectCatalog", 1),
            objectDefinition,
            model,
            CreateResource(sourceId, packagePath, SourceKind.Game, "ModelLOD", 1),
            CreateResource(sourceId, packagePath, SourceKind.Game, "PNGImage", 1),
            CreateResource(sourceId, packagePath, SourceKind.Game, "MaterialDefinition", 1)
        };

        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [model.Key.FullTgi] = [],
                [objectDefinition.Key.FullTgi] = CreateSyntheticObjectDefinitionBytes(
                    "collectGeode_EP01GENrough",
                    2,
                    227,
                    0x00000004u,
                    0x915EFC3Bu,
                    0x177C9B5Fu,
                    0x01661233u,
                    0x00000000u,
                    0x00000004u,
                    0x86C5F3A5u,
                    0x18A66232u,
                    0xD382BF57u,
                    0x00000000u)
            },
            new Dictionary<string, string?>());
        var builder = new ExplicitAssetGraphBuilder(catalog);

        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, resources, [])).Single();
        var graph = await builder.BuildAssetGraphAsync(summary, resources, CancellationToken.None);

        Assert.Contains(graph.Diagnostics, message => message.Contains("ObjectDefinition reference candidates:", StringComparison.Ordinal));
        Assert.Contains(graph.Diagnostics, message => message.Contains("Model raw=01661233:00000000:177C9B5F915EFC3B", StringComparison.Ordinal));
        Assert.Contains(graph.Diagnostics, message => message.Contains("swap32=01661233:00000000:915EFC3B177C9B5F", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildsExplicitBuildBuyGraph_IncludesSwap32ResolvedCrossPackageReferences()
    {
        var packagePath = Path.Combine(tempRoot, "objectdefinition-cross.package");
        var sourceId = Guid.NewGuid();
        var model = CreateResource(sourceId, packagePath, SourceKind.Game, "Model", 1, "Chair");
        var objectDefinition = CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectDefinition", 1);
        var resolvedModel = new ResourceMetadata(
            Guid.NewGuid(),
            sourceId,
            SourceKind.Game,
            Path.Combine(tempRoot, "external.package"),
            new ResourceKeyRecord(0x01661233u, 0, 0x915EFC3B177C9B5Ful, "Model"),
            "ResolvedModel",
            1,
            1,
            false,
            PreviewKind.Scene,
            true,
            true,
            string.Empty,
            string.Empty);
        var resources = new[]
        {
            CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectCatalog", 1),
            objectDefinition,
            model,
            CreateResource(sourceId, packagePath, SourceKind.Game, "ModelLOD", 1),
            CreateResource(sourceId, packagePath, SourceKind.Game, "PNGImage", 1),
            CreateResource(sourceId, packagePath, SourceKind.Game, "MaterialDefinition", 1)
        };

        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [model.Key.FullTgi] = [],
                [objectDefinition.Key.FullTgi] = CreateSyntheticObjectDefinitionBytes(
                    "collectGeode_EP01GENrough",
                    2,
                    227,
                    0x00000004u,
                    0x915EFC3Bu,
                    0x177C9B5Fu,
                    0x01661233u,
                    0x00000000u)
            },
            new Dictionary<string, string?>());
        var indexStore = new FakeGraphIndexStore([resolvedModel]);
        var builder = new ExplicitAssetGraphBuilder(catalog, indexStore);

        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, resources, [])).Single();
        var graph = await builder.BuildAssetGraphAsync(summary, resources, CancellationToken.None);

        Assert.Contains(graph.Diagnostics, message => message.Contains("ObjectDefinition swap32-resolved references:", StringComparison.Ordinal));
        Assert.Contains(graph.Diagnostics, message => message.Contains("Model -> 01661233:00000000:915EFC3B177C9B5F @ external.package", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildsExplicitBuildBuyGraph_UsesSwap32ResolvedModelRootWhenExactPathIsWeak()
    {
        var packagePath = Path.Combine(tempRoot, "objectdefinition-altmodel.package");
        var sourceId = Guid.NewGuid();
        var weakModel = CreateResource(sourceId, packagePath, SourceKind.Game, "Model", 1, "WeakModel");
        var objectDefinition = CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectDefinition", 1);
        var resolvedModel = new ResourceMetadata(
            Guid.NewGuid(),
            sourceId,
            SourceKind.Game,
            Path.Combine(tempRoot, "external.package"),
            new ResourceKeyRecord(0x01661233u, 0, 0x915EFC3B177C9B5Ful, "Model"),
            "ResolvedModel",
            1,
            1,
            false,
            PreviewKind.Scene,
            true,
            true,
            string.Empty,
            string.Empty);
        var resolvedModelLod = new ResourceMetadata(
            Guid.NewGuid(),
            sourceId,
            SourceKind.Game,
            resolvedModel.PackagePath,
            new ResourceKeyRecord((uint)Enum.Parse<LlamaLogic.Packages.ResourceType>("ModelLOD"), 0, resolvedModel.Key.FullInstance, "ModelLOD"),
            "ResolvedModelLod",
            1,
            1,
            false,
            PreviewKind.Scene,
            true,
            true,
            string.Empty,
            string.Empty);
        var resources = new[]
        {
            CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectCatalog", 1),
            objectDefinition,
            weakModel
        };

        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [weakModel.Key.FullTgi] = [],
                [objectDefinition.Key.FullTgi] = CreateSyntheticObjectDefinitionBytes(
                    "collectGeode_EP01GENrough",
                    2,
                    227,
                    0x00000004u,
                    0x915EFC3Bu,
                    0x177C9B5Fu,
                    0x01661233u,
                    0x00000000u)
            },
            new Dictionary<string, string?>());
        var indexStore = new FakeGraphIndexStore([resolvedModel, resolvedModelLod]);
        var builder = new ExplicitAssetGraphBuilder(catalog, indexStore);

        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, resources, [])).Single();
        var graph = await builder.BuildAssetGraphAsync(summary, resources, CancellationToken.None);

        Assert.Equal(resolvedModel.Key.FullTgi, graph.BuildBuyGraph!.ModelResource.Key.FullTgi);
        Assert.Contains(graph.BuildBuyGraph.ModelLodResources, resource => resource.Key.FullTgi == resolvedModelLod.Key.FullTgi);
        Assert.Contains(graph.Diagnostics, message => message.Contains("Using swap32-resolved ObjectDefinition model root:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildsExplicitBuildBuyGraph_IncludesObjectCatalogHeuristicDiagnostics()
    {
        var packagePath = Path.Combine(tempRoot, "objectcatalog.package");
        var sourceId = Guid.NewGuid();
        var model = CreateResource(sourceId, packagePath, SourceKind.Game, "Model", 1, "Chair");
        var objectCatalog = CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectCatalog", 1);
        var resources = new[]
        {
            objectCatalog,
            CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectDefinition", 1),
            model,
            CreateResource(sourceId, packagePath, SourceKind.Game, "ModelLOD", 1),
            CreateResource(sourceId, packagePath, SourceKind.Game, "PNGImage", 1),
            CreateResource(sourceId, packagePath, SourceKind.Game, "MaterialDefinition", 1)
        };

        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [model.Key.FullTgi] = [],
                [objectCatalog.Key.FullTgi] = CreateSyntheticObjectCatalogBytes(0x00000019u, 0x0000000Bu, 0x00000000u, 0xA141F327u, 0x9BCAEA4Cu, 0x00010000u)
            },
            new Dictionary<string, string?>());
        var builder = new ExplicitAssetGraphBuilder(catalog);

        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, resources, [])).Single();
        var graph = await builder.BuildAssetGraphAsync(summary, resources, CancellationToken.None);

        Assert.Contains(graph.Diagnostics, message => message.Contains("ObjectCatalog word count:", StringComparison.Ordinal));
        Assert.Contains(graph.Diagnostics, message => message.Contains("ObjectCatalog heuristic tail qwords:", StringComparison.Ordinal));
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
    public void Ts4ObjectDefinition_Parse_ReturnsConfirmedHeaderFields()
    {
        var assetsAssembly = typeof(ExplicitAssetGraphBuilder).Assembly;
        var objectDefinitionType = RequireType(assetsAssembly, "Sims4ResourceExplorer.Assets.Ts4ObjectDefinition");
        var payload = CreateSyntheticObjectDefinitionBytes("ArchEP03_DockApartment_6_01", 2, 173, 0x8EA7CE98u, 0x84911568u, 0x01661233u);

        var parsed = objectDefinitionType
            .GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [payload])!;

        Assert.Equal((ushort)2, (ushort)objectDefinitionType.GetProperty("Version")!.GetValue(parsed)!);
        Assert.Equal(173u, (uint)objectDefinitionType.GetProperty("DeclaredSize")!.GetValue(parsed)!);
        Assert.Equal("ArchEP03_DockApartment_6_01", (string)objectDefinitionType.GetProperty("InternalName")!.GetValue(parsed)!);
        Assert.Equal(12, (int)objectDefinitionType.GetProperty("RemainingByteCount")!.GetValue(parsed)!);

        var remainingWords = ((IReadOnlyList<uint>)objectDefinitionType.GetProperty("RemainingWords")!.GetValue(parsed)!).ToArray();
        Assert.Equal([0x8EA7CE98u, 0x84911568u, 0x01661233u], remainingWords);
        var referenceCandidates = ((System.Collections.IEnumerable)objectDefinitionType.GetProperty("ReferenceCandidates")!.GetValue(parsed)!).Cast<object>().ToArray();
        Assert.Empty(referenceCandidates);
    }

    [Fact]
    public void Ts4ObjectDefinition_Parse_ExtractsReferenceCandidatesAndSwap32Keys()
    {
        var assetsAssembly = typeof(ExplicitAssetGraphBuilder).Assembly;
        var objectDefinitionType = RequireType(assetsAssembly, "Sims4ResourceExplorer.Assets.Ts4ObjectDefinition");
        var payload = CreateSyntheticObjectDefinitionBytes(
            "collectGeode_EP01GENrough",
            2,
            227,
            0x00000004u,
            0x8681F0A5u,
            0x186C903Au,
            0x8EAF13DEu,
            0x00000000u,
            0x00000004u,
            0x915EFC3Bu,
            0x177C9B5Fu,
            0x01661233u,
            0x00000000u,
            0x00000004u,
            0x86C5F3A5u,
            0x18A66232u,
            0xD382BF57u,
            0x00000000u);

        var parsed = objectDefinitionType
            .GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [payload])!;

        var referenceCandidates = ((System.Collections.IEnumerable)objectDefinitionType.GetProperty("ReferenceCandidates")!.GetValue(parsed)!)
            .Cast<object>()
            .ToArray();
        Assert.Equal(3, referenceCandidates.Length);

        var candidateType = referenceCandidates[0].GetType();
        var rawKeyType = candidateType.GetProperty("RawKey")!.PropertyType;
        var swap32KeyType = candidateType.GetProperty("Swap32Key")!.PropertyType;

        var rigCandidate = referenceCandidates.Single(candidate =>
            string.Equals((string)rawKeyType.GetProperty("TypeName")!.GetValue(candidateType.GetProperty("RawKey")!.GetValue(candidate)!)!, "Rig", StringComparison.Ordinal));
        Assert.Equal("8EAF13DE:00000000:186C903A8681F0A5", (string)rawKeyType.GetProperty("FullTgi")!.GetValue(candidateType.GetProperty("RawKey")!.GetValue(rigCandidate)!)!);
        Assert.Equal("8EAF13DE:00000000:8681F0A5186C903A", (string)swap32KeyType.GetProperty("FullTgi")!.GetValue(candidateType.GetProperty("Swap32Key")!.GetValue(rigCandidate)!)!);

        var modelCandidate = referenceCandidates.Single(candidate =>
            string.Equals((string)rawKeyType.GetProperty("TypeName")!.GetValue(candidateType.GetProperty("RawKey")!.GetValue(candidate)!)!, "Model", StringComparison.Ordinal));
        Assert.Equal("01661233:00000000:177C9B5F915EFC3B", (string)rawKeyType.GetProperty("FullTgi")!.GetValue(candidateType.GetProperty("RawKey")!.GetValue(modelCandidate)!)!);
        Assert.Equal("01661233:00000000:915EFC3B177C9B5F", (string)swap32KeyType.GetProperty("FullTgi")!.GetValue(candidateType.GetProperty("Swap32Key")!.GetValue(modelCandidate)!)!);
    }

    [Fact]
    public void Ts4SeedMetadataExtractor_ExtractsCasPartTechnicalName()
    {
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var bytes = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "ymShoes_AnkleWork_Brown");
        var resource = new ResourceMetadata(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SourceKind.Game,
            "cas.package",
            new ResourceKeyRecord(0x034AEECB, 0, 4, "CASPart"),
            null,
            null,
            null,
            null,
            PreviewKind.Hex,
            true,
            true,
            string.Empty,
            string.Empty);

        var technicalName = Ts4SeedMetadataExtractor.TryExtractTechnicalName(resource, bytes);

        Assert.Equal("ymShoes_AnkleWork_Brown", technicalName);
    }

    [Fact]
    public void Ts4ObjectCatalog_Parse_ExtractsConfirmedLocalizationHashes()
    {
        var assetsAssembly = typeof(ExplicitAssetGraphBuilder).Assembly;
        var objectCatalogType = RequireType(assetsAssembly, "Sims4ResourceExplorer.Assets.Ts4ObjectCatalog");
        var payload = CreateSyntheticObjectCatalogBytes(0x0000001Au, 0x0000000Bu, 0xA517377Au, 0x12552F9Bu, 0x00000005u, 0, 0, 176, 768, 0, 0, 2560, 0x000D0300u, 0x000D1B00u);

        var parsed = objectCatalogType
            .GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [payload])!;

        Assert.Equal(0xA517377Au, (uint?)objectCatalogType.GetProperty("NameHash")!.GetValue(parsed));
        Assert.Equal(0x12552F9Bu, (uint?)objectCatalogType.GetProperty("DescriptionHash")!.GetValue(parsed));
        Assert.Equal(0x00000300u, (uint?)objectCatalogType.GetProperty("Word0020")!.GetValue(parsed));
        Assert.Equal(0x00000A00u, (uint?)objectCatalogType.GetProperty("Word002C")!.GetValue(parsed));
        Assert.Equal(0x000D0300u, (uint?)objectCatalogType.GetProperty("Word0030")!.GetValue(parsed));
        Assert.Equal(0x000D1B00u, (uint?)objectCatalogType.GetProperty("Word0034")!.GetValue(parsed));
        Assert.Contains("+0x002C=0x00000A00", (string?)objectCatalogType.GetProperty("RawCategorySignalSummary")!.GetValue(parsed), StringComparison.Ordinal);
    }

    [Fact]
    public void Ts4SeedMetadataExtractor_ExtractsObjectCatalogNameHash()
    {
        var payload = CreateSyntheticObjectCatalogBytes(0x0000001Au, 0x0000000Bu, 0xA517377Au, 0x12552F9Bu, 0x00000005u);

        var nameHash = Ts4SeedMetadataExtractor.TryExtractObjectCatalogNameHash(payload);

        Assert.Equal(0xA517377Au, nameHash);
    }

    [Fact]
    public void Ts4SeedMetadataExtractor_ExtractsObjectCatalogDescriptionHash()
    {
        var payload = CreateSyntheticObjectCatalogBytes(0x0000001Au, 0x0000000Bu, 0xA517377Au, 0x12552F9Bu, 0x00000005u);

        var descriptionHash = Ts4SeedMetadataExtractor.TryExtractObjectCatalogDescriptionHash(payload);

        Assert.Equal(0x12552F9Bu, descriptionHash);
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildsExplicitBuildBuyGraph_IncludesObjectCatalogRawCategorySignals()
    {
        var packagePath = Path.Combine(tempRoot, "objectcatalog-signals.package");
        var sourceId = Guid.NewGuid();
        var objectCatalog = CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectCatalog", 1);
        var objectDefinition = CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectDefinition", 1);
        var model = CreateResource(sourceId, packagePath, SourceKind.Game, "Model", 1);
        var modelLod = CreateResource(sourceId, packagePath, SourceKind.Game, "ModelLOD", 1);
        var resources = new[] { objectCatalog, objectDefinition, model, modelLod };
        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [objectCatalog.Key.FullTgi] = CreateSyntheticObjectCatalogBytes(0x0000001Au, 0x0000000Bu, 0xA517377Au, 0x12552F9Bu, 0x00000005u, 0, 0, 176, 768, 0, 0, 2560, 0x000D0300u, 0x000D1B00u),
                [objectDefinition.Key.FullTgi] = CreateSyntheticObjectDefinitionBytes("collectGeode_EP01GENrough", 2, 173)
            },
            new Dictionary<string, string?>());
        var builder = new ExplicitAssetGraphBuilder(catalog);
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, resources, [])).Single();

        var graph = await builder.BuildAssetGraphAsync(summary, resources, CancellationToken.None);

        Assert.Contains(graph.Diagnostics, message => message.Contains("ObjectCatalog raw category signals:", StringComparison.Ordinal));
        Assert.Contains(graph.Diagnostics, message => message.Contains("+0x002C=0x00000A00", StringComparison.Ordinal));
    }

    [Fact]
    public void Ts4ObjectCatalog_Parse_ReturnsHeuristicWordAndTailViews()
    {
        var assetsAssembly = typeof(ExplicitAssetGraphBuilder).Assembly;
        var objectCatalogType = RequireType(assetsAssembly, "Sims4ResourceExplorer.Assets.Ts4ObjectCatalog");
        var payload = CreateSyntheticObjectCatalogBytes(
            0x00000019u,
            0x0000000Bu,
            0x00000000u,
            0x00000081u,
            0xA141F327u,
            0x9BCAEA4Cu,
            0x00010000u,
            0x00000000u);

        var parsed = objectCatalogType
            .GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [payload])!;

        var words = ((IReadOnlyList<uint>)objectCatalogType.GetProperty("Words")!.GetValue(parsed)!).ToArray();
        var tailQwords = ((IReadOnlyList<ulong>)objectCatalogType.GetProperty("TailQwordCandidates")!.GetValue(parsed)!).ToArray();
        var note = (string)objectCatalogType.GetProperty("ApproximationNote")!.GetValue(parsed)!;

        Assert.Equal([0x00000019u, 0x0000000Bu, 0x00000000u, 0x00000081u, 0xA141F327u, 0x9BCAEA4Cu, 0x00010000u, 0x00000000u], words);
        Assert.Equal([0x0000000B00000019ul, 0x0000008100000000ul, 0x9BCAEA4CA141F327ul, 0x0000000000010000ul], tailQwords);
        Assert.Contains("Category and tag fields remain heuristic", note, StringComparison.Ordinal);
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

    [Theory]
    [InlineData("DetailMap", "detail")]
    [InlineData("LotPaint", "decal")]
    [InlineData("DecalCenters2X", "decal")]
    [InlineData("samplerBlueRampTexture", "overlay")]
    [InlineData("WallTopBottomShadow", "overlay")]
    [InlineData("GhostNoiseTexture", "overlay")]
    [InlineData("samplerCASMedatorGridTexture", "overlay")]
    [InlineData("samplerEnvCubeMap", "specular")]
    [InlineData("samplerSnowSharedNormals", "normal")]
    public void ShaderSemantics_ResolvesExtendedTextureSlotRoles(string parameterName, string expectedSlot)
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
            args: [0x12345678u, parameterName, 0x0102C601u, 0x00004000u, samplerCategory],
            culture: null)!;
        var parameters = Array.CreateInstance(shaderParameterType, 1);
        parameters.SetValue(parameter, 0);
        var profile = Activator.CreateInstance(
            shaderProfileType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x0CB82EB8u, "WorldToDepthMapSpaceMatrix", parameters],
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
            args: ["texture_0", key, 0x12345678u],
            culture: null)!;

        var resolvedSlot = (string)semanticsType
            .GetMethod("ResolveTextureSlotName", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [reference, profile])!;

        Assert.Equal(expectedSlot, resolvedSlot);
    }

    [Fact]
    public void ShaderSemantics_UsesGlobalAliasForKnownStateControlHash()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var propertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MatdProperty");
        var categoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var valueRepresentationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");
        var semanticsType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ShaderSemantics");

        var property = Activator.CreateInstance(
            propertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                0x415368B4u,
                1u,
                1u,
                1u,
                0,
                Enum.Parse(categoryType, "BoolLike"),
                Enum.Parse(valueRepresentationType, "PackedUInt32"),
                "packed32=[0x00000001]",
                null,
                new uint[] { 1u },
                null
            ],
            culture: null)!;

        var resolvedName = (string)semanticsType
            .GetMethod("ResolveMaterialPropertyName", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [property, null])!;

        Assert.Equal("InstancedObjectAura", resolvedName);
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
    public void ShaderSemantics_DoesNotTreatGenericResourceKeyParameterAsTextureBinding()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderParameterType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile");
        var shaderCategoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var semanticsType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ShaderSemantics");

        var resourceCategory = Enum.Parse(shaderCategoryType, "ResourceKey");
        var parameter = Activator.CreateInstance(
            shaderParameterType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x0950E7D4u, "0x0950E7D4", 0x0102C601u, 0x00004000u, resourceCategory],
            culture: null)!;

        var result = (bool)semanticsType
            .GetMethod("IsLikelyTextureParameter", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [2u, 1u, parameter])!;

        Assert.False(result);
    }

    [Fact]
    public void ShaderSemantics_DoesNotTreatUvMappingParameterAsTextureBinding()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderParameterType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile");
        var shaderCategoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var semanticsType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ShaderSemantics");

        var uvCategory = Enum.Parse(shaderCategoryType, "UvMapping");
        var parameter = Activator.CreateInstance(
            shaderParameterType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x420520E9u, "uvMapping", 0x01374601u, 0x80004000u, uvCategory],
            culture: null)!;

        var result = (bool)semanticsType
            .GetMethod("IsLikelyTextureParameter", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [1u, 4u, parameter])!;

        Assert.False(result);
    }

    [Fact]
    public void ShaderSemantics_DoesNotTreatNormalMapTileableControlAsTextureBinding()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderParameterType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile");
        var shaderCategoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var semanticsType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ShaderSemantics");

        var scalarCategory = Enum.Parse(shaderCategoryType, "Scalar");
        var parameter = Activator.CreateInstance(
            shaderParameterType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0xE77A2B60u, "NormalMapTileable", 0x0103C001u, 0x00001004u, scalarCategory],
            culture: null)!;

        var result = (bool)semanticsType
            .GetMethod("IsLikelyTextureParameter", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [1u, 1u, parameter])!;

        Assert.False(result);
    }

    [Fact]
    public void ShaderSemantics_TreatsWriteDepthMaskExtraTextureSlotAsAlpha()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var resourceKeyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ResourceKey");
        var semanticsType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ShaderSemantics");

        var profile = Activator.CreateInstance(
            shaderProfileType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x58F649EBu, "WriteDepthMask", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)],
            culture: null)!;
        var key = Activator.CreateInstance(resourceKeyType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0x00B2D882u, 0u, 0x213F2BF8EAE65122ul], null)!;
        var reference = Activator.CreateInstance(
            textureReferenceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: ["texture_1", key, 0x0950E7D4u],
            culture: null)!;

        var resolved = (string)semanticsType
            .GetMethod("ResolveTextureSlotName", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [reference, profile])!;

        Assert.Equal("alpha", resolved);
    }

    [Theory]
    [InlineData("colorMap7", "texture_1", "alpha")]
    [InlineData("DecalMap", "texture_1", "alpha")]
    [InlineData("DecalMap", "texture_2", "overlay")]
    [InlineData("ShaderDayNightParameters", "texture_1", "emissive")]
    [InlineData("ShaderDayNightParameters", "texture_2", "overlay")]
    [InlineData("UseVertAlpha", "texture_1", "decal")]
    [InlineData("UseVertAlpha", "texture_2", "overlay")]
    [InlineData("WorldToDepthMapSpaceMatrix", "texture_1", "alpha")]
    [InlineData("WorldToDepthMapSpaceMatrix", "texture_4", "overlay")]
    public void ShaderSemantics_UsesFamilyFallbackTextureRoles(string profileName, string inputSlot, string expectedSlot)
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var resourceKeyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ResourceKey");
        var semanticsType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ShaderSemantics");

        var emptyParameters = Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0);
        var profile = Activator.CreateInstance(
            shaderProfileType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x11111111u, profileName, emptyParameters],
            culture: null)!;
        var key = Activator.CreateInstance(resourceKeyType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0x00B2D882u, 0u, 0x1ul], null)!;
        var reference = Activator.CreateInstance(
            textureReferenceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [inputSlot, key, 0x12345678u],
            culture: null)!;

        var resolved = (string)semanticsType
            .GetMethod("ResolveTextureSlotName", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [reference, profile])!;

        Assert.Equal(expectedSlot, resolved);
    }

    [Theory]
    [InlineData("samplerSourceTexture", "diffuse")]
    [InlineData("samplerDetailNormalMap", "normal")]
    [InlineData("samplerroutingMap", "alpha")]
    [InlineData("PremadeLightmap", "overlay")]
    [InlineData("SunTexture", "emissive")]
    public void ShaderSemantics_ResolvesExtendedRuntimeTextureRoles(string parameterName, string expectedSlot)
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var shaderParameterType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile");
        var shaderCategoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var resourceKeyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ResourceKey");
        var semanticsType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ShaderSemantics");

        var category = Enum.Parse(shaderCategoryType, "Sampler");
        var parameter = Activator.CreateInstance(
            shaderParameterType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x12345678u, parameterName, 0u, 0u, category],
            culture: null)!;
        var parameterArray = Array.CreateInstance(shaderParameterType, 1);
        parameterArray.SetValue(parameter, 0);
        var profile = Activator.CreateInstance(
            shaderProfileType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x11111111u, "ShaderDayNightParameters", parameterArray],
            culture: null)!;
        var key = Activator.CreateInstance(resourceKeyType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0x00B2D882u, 0u, 0x1ul], null)!;
        var reference = Activator.CreateInstance(
            textureReferenceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: ["texture_1", key, 0x12345678u],
            culture: null)!;

        var resolved = (string)semanticsType
            .GetMethod("ResolveTextureSlotName", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [reference, profile])!;

        Assert.Equal(expectedSlot, resolved);
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
    public void ShaderSemantics_InterpretsSeparateUvScalarControls()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderParameterType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile");
        var shaderCategoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var semanticsType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ShaderSemantics");

        var scalarCategory = Enum.Parse(shaderCategoryType, "Scalar");
        var mapping = Activator.CreateInstance(
            uvMappingType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0, 1f, 1f, 0f, 0f],
            culture: null)!;

        object Apply(string name, float value, object current)
        {
            var parameter = Activator.CreateInstance(
                shaderParameterType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: [0x10000000u, name, 0x0103C001u, 0x00001004u, scalarCategory],
                culture: null)!;
            var args = new object?[] { parameter, value, current, null };
            var handled = (bool)semanticsType
                .GetMethod("TryInterpretDiffuseUvMappingScalar", BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, args)!;
            Assert.True(handled);
            Assert.NotNull(args[3]);
            return args[3]!;
        }

        mapping = Apply("DiffuseUVScaleU", 0.25f, mapping);
        mapping = Apply("DiffuseUVScaleV", 0.5f, mapping);
        mapping = Apply("DiffuseUVOffsetU", 0.125f, mapping);
        mapping = Apply("DiffuseUVOffsetV", -0.375f, mapping);

        Assert.Equal(0.25f, (float)uvMappingType.GetProperty("UvScaleU")!.GetValue(mapping)!);
        Assert.Equal(0.5f, (float)uvMappingType.GetProperty("UvScaleV")!.GetValue(mapping)!);
        Assert.Equal(0.125f, (float)uvMappingType.GetProperty("UvOffsetU")!.GetValue(mapping)!);
        Assert.Equal(-0.375f, (float)uvMappingType.GetProperty("UvOffsetV")!.GetValue(mapping)!);
    }

    [Fact]
    public void ShaderSemantics_InterpretsAtlasMinMaxVectors()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderParameterType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile");
        var shaderCategoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var semanticsType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ShaderSemantics");

        var vectorCategory = Enum.Parse(shaderCategoryType, "Vector2");
        var atlasMin = Activator.CreateInstance(
            shaderParameterType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x20000000u, "DiffuseAtlasMin", 0x01324601u, 0x80004000u, vectorCategory],
            culture: null)!;
        var atlasMax = Activator.CreateInstance(
            shaderParameterType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x20000001u, "DiffuseAtlasMax", 0x01324601u, 0x80004000u, vectorCategory],
            culture: null)!;
        var mapping = Activator.CreateInstance(
            uvMappingType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0, 1f, 1f, 0f, 0f],
            culture: null)!;
        var vectorMethod = semanticsType.GetMethod(
            "TryInterpretDiffuseUvMappingVector",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [shaderParameterType, typeof(float[]), uvMappingType, uvMappingType.MakeByRefType()],
            modifiers: null)!;

        var minArgs = new object?[] { atlasMin, new[] { 0.2f, 0.3f }, mapping, null };
        var minHandled = (bool)vectorMethod.Invoke(null, minArgs)!;
        Assert.True(minHandled);
        mapping = minArgs[3]!;

        var maxArgs = new object?[] { atlasMax, new[] { 0.7f, 0.9f }, mapping, null };
        var maxHandled = (bool)vectorMethod.Invoke(null, maxArgs)!;
        Assert.True(maxHandled);
        Assert.NotNull(maxArgs[3]);

        Assert.Equal(0.2f, (float)uvMappingType.GetProperty("UvOffsetU")!.GetValue(maxArgs[3])!, 3);
        Assert.Equal(0.3f, (float)uvMappingType.GetProperty("UvOffsetV")!.GetValue(maxArgs[3])!, 3);
        Assert.Equal(0.5f, (float)uvMappingType.GetProperty("UvScaleU")!.GetValue(maxArgs[3])!, 3);
        Assert.Equal(0.6f, (float)uvMappingType.GetProperty("UvScaleV")!.GetValue(maxArgs[3])!, 3);
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

        Assert.Equal(Enum.Parse(representationType, "PackedUInt32"), representation);
        Assert.Contains("00B2D882", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void MatdParser_DoesNotTreatFloatVectorPayloadAsTextureReference()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var matdType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MatdChunk");
        var parseMethod = matdType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)!;
        var parse = (ParseMatdDelegate)parseMethod.CreateDelegate(typeof(ParseMatdDelegate));

        var bytes = CreateMinimalMatd(
            materialNameHash: 0x11111111u,
            shaderNameHash: 0x22222222u,
            propertyHash: 0x1B9D3AC5u,
            propertyType: 4u,
            propertyArity: 4u,
            values: BitConverter.GetBytes(0.1f)
                .Concat(BitConverter.GetBytes(0.2f))
                .Concat(BitConverter.GetBytes(0.3f))
                .Concat(BitConverter.GetBytes(0.4f))
                .ToArray());

        var chunk = parse(bytes);
        var textureReferences = (System.Collections.IEnumerable)matdType.GetProperty("TextureReferences")!.GetValue(chunk)!;

        Assert.Empty(textureReferences.Cast<object>());
    }

    [Fact]
    public void MatdParser_SupplementsExplicitTextureReferencesWithHeuristicCandidates()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var matdType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MatdChunk");
        var parseMethod = matdType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)!;
        var parse = (ParseMatdDelegate)parseMethod.CreateDelegate(typeof(ParseMatdDelegate));

        var bytes = CreateMinimalMatd(
            0x11111111u,
            0xB9105A6Du,
            (0x1B9D3AC5u, 2u, 1u, CreateEmbeddedResourceKeyBytes(0x00B2D882u, 0u, 0x0115AAEAD51B0391ul)),
            (0xB95C43EBu, 1u, 4u, CreatePackedTextureLikePayload(0x00B2D882u, 0u, 0x018AD2B576C4802Cul)));

        var chunk = parse(bytes);
        var textureReferences = ((System.Collections.IEnumerable)matdType.GetProperty("TextureReferences")!.GetValue(chunk)!)
            .Cast<object>()
            .ToArray();

        Assert.Equal(2, textureReferences.Length);
        Assert.Contains(
            textureReferences,
            reference => string.Equals(
                (string)reference.GetType().GetProperty("Slot")!.GetValue(reference)!,
                "diffuse",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MatdParser_ReadsTrailingTypeTextureLayoutAsExplicitTextureReference()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var matdType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MatdChunk");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var parseMethod = matdType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)!;
        var parse = (ParseMatdDelegate)parseMethod.CreateDelegate(typeof(ParseMatdDelegate));

        var payload = BitConverter.GetBytes(0u)
            .Concat(BitConverter.GetBytes(0x5AE9066Bu))
            .Concat(BitConverter.GetBytes(0xA4D80FB4u))
            .Concat(BitConverter.GetBytes(0x00B2D882u))
            .ToArray();

        var bytes = CreateMinimalMatd(
            0x11111111u,
            0xB9105A6Du,
            (0x1B9D3AC5u, 2u, 1u, payload));

        var chunk = parse(bytes);
        var textureReferences = ((System.Collections.IEnumerable)matdType.GetProperty("TextureReferences")!.GetValue(chunk)!)
            .Cast<object>()
            .ToArray();

        var textureReference = Assert.Single(textureReferences);
        var slot = (string)textureReferenceType.GetProperty("Slot")!.GetValue(textureReference)!;
        var key = textureReferenceType.GetProperty("Key")!.GetValue(textureReference)!;
        var keyType = key.GetType();
        var type = (uint)keyType.GetProperty("Type")!.GetValue(key)!;
        var group = (uint)keyType.GetProperty("Group")!.GetValue(key)!;
        var instance = (ulong)keyType.GetProperty("Instance")!.GetValue(key)!;

        Assert.Equal("diffuse", slot);
        Assert.Equal(0x00B2D882u, type);
        Assert.Equal(0u, group);
        Assert.Equal(0xA4D80FB45AE9066Bul, instance);
    }

    [Fact]
    public void BuildBuySceneBuildService_MergesSiblingAndGlobalTextureLookupPackages()
    {
        var method = typeof(BuildBuySceneBuildService).GetMethod(
            "MergeCrossPackageTextureLookupPaths",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;

        var packagePath = @"C:\Game\EP04\ClientFullBuild0.package";
        var siblingCandidates = new[]
        {
            packagePath,
            @"C:\Game\EP04\ClientFullBuild2.package",
            @"C:\Game\EP04\ClientFullBuild1.package",
            @"C:\Game\EP04\ClientFullBuild2.package"
        };
        var globalCandidates = new[]
        {
            @"C:\Game\Data\Client\ClientFullBuild0.package",
            @"C:\Game\EP04\ClientFullBuild1.package",
            @"C:\Game\Data\Client\ClientFullBuild1.package"
        };

        var merged = (IReadOnlyList<string>)method.Invoke(null, [packagePath, siblingCandidates, globalCandidates])!;

        Assert.Equal(
            [
                @"C:\Game\EP04\ClientFullBuild2.package",
                @"C:\Game\EP04\ClientFullBuild1.package",
                @"C:\Game\Data\Client\ClientFullBuild0.package",
                @"C:\Game\Data\Client\ClientFullBuild1.package"
            ],
            merged);
    }

    [Fact]
    public void MatdParser_ReadsImageKeyFromVector4PackedPayload()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var matdType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MatdChunk");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var parseMethod = matdType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)!;
        var parse = (ParseMatdDelegate)parseMethod.CreateDelegate(typeof(ParseMatdDelegate));

        var payload = BitConverter.GetBytes(0x000001D0u)
            .Concat(BitConverter.GetBytes(0xA63BED14u))
            .Concat(BitConverter.GetBytes(0xDE685457u))
            .Concat(BitConverter.GetBytes(0x00B2D882u))
            .ToArray();

        var bytes = CreateMinimalMatd(
            0x11111111u,
            0xFC5FC212u,
            (0x79C44C9Bu, 4u, 4u, payload));

        var chunk = parse(bytes);
        var textureReferences = ((System.Collections.IEnumerable)matdType.GetProperty("TextureReferences")!.GetValue(chunk)!)
            .Cast<object>()
            .ToArray();

        var textureReference = Assert.Single(textureReferences);
        var slot = (string)textureReferenceType.GetProperty("Slot")!.GetValue(textureReference)!;
        var key = textureReferenceType.GetProperty("Key")!.GetValue(textureReference)!;
        var keyType = key.GetType();
        var type = (uint)keyType.GetProperty("Type")!.GetValue(key)!;
        var group = (uint)keyType.GetProperty("Group")!.GetValue(key)!;
        var instance = (ulong)keyType.GetProperty("Instance")!.GetValue(key)!;

        Assert.Equal("diffuse", slot);
        Assert.Equal(0x00B2D882u, type);
        Assert.Equal(0u, group);
        Assert.Equal(0xDE685457A63BED14ul, instance);
    }

    [Fact]
    public void MatdParser_ReadsInstanceOnlyImageLayoutAsExplicitTextureReference()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var matdType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MatdChunk");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var parseMethod = matdType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)!;
        var parse = (ParseMatdDelegate)parseMethod.CreateDelegate(typeof(ParseMatdDelegate));

        var payload = BitConverter.GetBytes(0x00000004u)
            .Concat(BitConverter.GetBytes(0x0000020Cu))
            .Concat(BitConverter.GetBytes(0xA63BED14u))
            .Concat(BitConverter.GetBytes(0xDE685457u))
            .ToArray();

        var bytes = CreateMinimalMatd(
            0x11111111u,
            0x78805394u,
            (0x3F89C2EFu, 1u, 4u, payload));

        var chunk = parse(bytes);
        var textureReferences = ((System.Collections.IEnumerable)matdType.GetProperty("TextureReferences")!.GetValue(chunk)!)
            .Cast<object>()
            .ToArray();

        var textureReference = Assert.Single(textureReferences);
        var slot = (string)textureReferenceType.GetProperty("Slot")!.GetValue(textureReference)!;
        var key = textureReferenceType.GetProperty("Key")!.GetValue(textureReference)!;
        var keyType = key.GetType();
        var type = (uint)keyType.GetProperty("Type")!.GetValue(key)!;
        var group = (uint)keyType.GetProperty("Group")!.GetValue(key)!;
        var instance = (ulong)keyType.GetProperty("Instance")!.GetValue(key)!;

        Assert.Equal("specular", slot);
        Assert.Equal(0x00B2D882u, type);
        Assert.Equal(0u, group);
        Assert.Equal(0xDE685457A63BED14ul, instance);
    }

    [Fact]
    public void MatdParser_DoesNotTreatDegenerateHeuristicImageLayoutAsTextureReference()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var matdType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MatdChunk");
        var parseMethod = matdType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)!;
        var parse = (ParseMatdDelegate)parseMethod.CreateDelegate(typeof(ParseMatdDelegate));

        var payload = BitConverter.GetBytes(0x00000004u)
            .Concat(BitConverter.GetBytes(0x0000020Cu))
            .Concat(BitConverter.GetBytes(0x00000000u))
            .Concat(BitConverter.GetBytes(0x1FC51644u))
            .ToArray();

        var bytes = CreateMinimalMatd(
            0x11111111u,
            0x78805394u,
            (0x3F89C2EFu, 1u, 4u, payload));

        var chunk = parse(bytes);
        var textureReferences = (System.Collections.IEnumerable)matdType.GetProperty("TextureReferences")!.GetValue(chunk)!;

        Assert.Empty(textureReferences.Cast<object>());
    }

    [Fact]
    public void MatdParser_DoesNotTreatFloatConstantHeuristicImageLayoutAsTextureReference()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var matdType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MatdChunk");
        var parseMethod = matdType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)!;
        var parse = (ParseMatdDelegate)parseMethod.CreateDelegate(typeof(ParseMatdDelegate));

        var payload = BitConverter.GetBytes(0x00000004u)
            .Concat(BitConverter.GetBytes(0x0000020Cu))
            .Concat(BitConverter.GetBytes(0x3F000000u))
            .Concat(BitConverter.GetBytes(0x37FF45E8u))
            .ToArray();

        var bytes = CreateMinimalMatd(
            0x11111111u,
            0x78805394u,
            (0x3F89C2EFu, 1u, 4u, payload));

        var chunk = parse(bytes);
        var textureReferences = (System.Collections.IEnumerable)matdType.GetProperty("TextureReferences")!.GetValue(chunk)!;

        Assert.Empty(textureReferences.Cast<object>());
    }

    [Fact]
    public void MatdParser_DoesNotTreatEmbeddedFloatPairImageLayoutAsHeuristicTextureReference()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var matdType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MatdChunk");
        var parseMethod = matdType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)!;
        var parse = (ParseMatdDelegate)parseMethod.CreateDelegate(typeof(ParseMatdDelegate));

        var payload = BitConverter.GetBytes(0x00B2D882u)
            .Concat(BitConverter.GetBytes(0x00000000u))
            .Concat(BitConverter.GetBytes(0x3F800000u))
            .Concat(BitConverter.GetBytes(0x37FF45E8u))
            .ToArray();

        var bytes = CreateMinimalMatd(
            0x11111111u,
            0x78805394u,
            (0x3A9F5D1Cu, 1u, 4u, payload));

        var chunk = parse(bytes);
        var textureReferences = (System.Collections.IEnumerable)matdType.GetProperty("TextureReferences")!.GetValue(chunk)!;

        Assert.Empty(textureReferences.Cast<object>());
    }

    [Fact]
    public void MatdParser_DoesNotTreatSamplerFloatVectorPayloadAsHeuristicTextureReference()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var matdType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MatdChunk");
        var parseMethod = matdType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)!;
        var parse = (ParseMatdDelegate)parseMethod.CreateDelegate(typeof(ParseMatdDelegate));

        var bytes = CreateMinimalMatd(
            0x11111111u,
            0xB9105A6Du,
            (0x637DAA05u, 1u, 4u, BitConverter.GetBytes(0.25f)
                .Concat(BitConverter.GetBytes(0.5f))
                .Concat(BitConverter.GetBytes(0.75f))
                .Concat(BitConverter.GetBytes(1f))
                .ToArray()));

        var chunk = parse(bytes);
        var textureReferences = (System.Collections.IEnumerable)matdType.GetProperty("TextureReferences")!.GetValue(chunk)!;

        Assert.Empty(textureReferences.Cast<object>());
    }

    [Fact]
    public void MatdParser_ClassifiesImplausibleScalarPayloadAsPackedUInt32()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var matdType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MatdChunk");
        var propertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MatdProperty");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");
        var parseMethod = matdType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)!;
        var parse = (ParseMatdDelegate)parseMethod.CreateDelegate(typeof(ParseMatdDelegate));

        var bytes = CreateMinimalMatd(
            materialNameHash: 0x11111111u,
            shaderNameHash: 0x22222222u,
            (0xC45A5F41u, 1u, 1u, BitConverter.GetBytes(0x637DAA05u))
        );

        var chunk = parse(bytes);
        var properties = ((System.Collections.IEnumerable)matdType.GetProperty("Properties")!.GetValue(chunk)!).Cast<object>().ToArray();
        var property = properties.Single();
        var valueRepresentation = propertyType.GetProperty("ValueRepresentation")!.GetValue(property);

        Assert.Equal(Enum.Parse(representationType, "PackedUInt32"), valueRepresentation);
    }

    [Fact]
    public void MatdParser_ClassifiesFloatLikeType2PayloadAsPackedUInt32()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var matdType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MatdChunk");
        var propertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MatdProperty");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");
        var parseMethod = matdType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)!;
        var parse = (ParseMatdDelegate)parseMethod.CreateDelegate(typeof(ParseMatdDelegate));

        var payload = BitConverter.GetBytes(0f)
            .Concat(BitConverter.GetBytes(1f))
            .Concat(BitConverter.GetBytes(20f))
            .Concat(BitConverter.GetBytes(unchecked((int)0xC5E6DD87u)))
            .ToArray();

        var bytes = CreateMinimalMatd(
            materialNameHash: 0x11111111u,
            shaderNameHash: 0x22222222u,
            (0x449A3A67u, 2u, 1u, payload)
        );

        var chunk = parse(bytes);
        var properties = ((System.Collections.IEnumerable)matdType.GetProperty("Properties")!.GetValue(chunk)!).Cast<object>().ToArray();
        var property = properties.Single();
        var valueRepresentation = propertyType.GetProperty("ValueRepresentation")!.GetValue(property);

        Assert.Equal(Enum.Parse(representationType, "PackedUInt32"), valueRepresentation);
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
    public void MaterialDecoder_ExtractsApproximateBaseColor_FromBaseColorVector()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var categoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");

        var vector4Category = Enum.Parse(categoryType, "Vector4");
        var floatVectorRepresentation = Enum.Parse(representationType, "FloatVector");
        var baseColorProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x100u, "BaseColorTint", 4u, 4u, 4u, vector4Category, floatVectorRepresentation, "vector=[0.2,0.4,0.6,1]", new[] { 0.2f, 0.4f, 0.6f, 1f }, null, null],
            culture: null)!;
        var properties = Array.CreateInstance(materialPropertyType, 1);
        properties.SetValue(baseColorProperty, 0);

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
            args: [0xB9105A6Du, "Phong", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)],
            culture: null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var approximateBaseColor = ((System.Collections.IEnumerable?)decoded.GetType().GetProperty("ApproximateBaseColor")!.GetValue(decoded))?.Cast<float>().ToArray();

        Assert.NotNull(approximateBaseColor);
        Assert.Equal([0.2f, 0.4f, 0.6f, 1f], approximateBaseColor!);
    }

    [Fact]
    public void MaterialDecoder_PrefersBaseColorTint_OverSpecularColor_ForApproximateBaseColor()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var categoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");

        var vector4Category = Enum.Parse(categoryType, "Vector4");
        var floatVectorRepresentation = Enum.Parse(representationType, "FloatVector");
        var specularColorProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x101u, "SpecularColor", 4u, 4u, 4u, vector4Category, floatVectorRepresentation, "vector=[1,0,0,1]", new[] { 1f, 0f, 0f, 1f }, null, null],
            culture: null)!;
        var baseColorProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x102u, "BaseColorTint", 4u, 4u, 4u, vector4Category, floatVectorRepresentation, "vector=[0.25,0.5,0.75,1]", new[] { 0.25f, 0.5f, 0.75f, 1f }, null, null],
            culture: null)!;
        var properties = Array.CreateInstance(materialPropertyType, 2);
        properties.SetValue(specularColorProperty, 0);
        properties.SetValue(baseColorProperty, 1);

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
            args: [0xB9105A6Du, "Phong", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)],
            culture: null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var approximateBaseColor = ((System.Collections.IEnumerable?)decoded.GetType().GetProperty("ApproximateBaseColor")!.GetValue(decoded))?.Cast<float>().ToArray();

        Assert.NotNull(approximateBaseColor);
        Assert.Equal([0.25f, 0.5f, 0.75f, 1f], approximateBaseColor!);
    }

    [Fact]
    public void MaterialDecoder_FallsBackToAnonymousVectorColor_WhenNoNamedColorSemanticExists()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var categoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");

        var vector4Category = Enum.Parse(categoryType, "Vector4");
        var floatVectorRepresentation = Enum.Parse(representationType, "FloatVector");
        var anonymousColorProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x6E56548Au, "0x6E56548A", 4u, 4u, 4u, vector4Category, floatVectorRepresentation, "vector=[0.97,0.83,0.55,0]", new[] { 0.97f, 0.83f, 0.55f, 0f }, null, null],
            culture: null)!;
        var properties = Array.CreateInstance(materialPropertyType, 1);
        properties.SetValue(anonymousColorProperty, 0);

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
            args: [0x0CB82EB8u, "SeasonalFoliage", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)],
            culture: null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var approximateBaseColor = ((System.Collections.IEnumerable?)decoded.GetType().GetProperty("ApproximateBaseColor")!.GetValue(decoded))?.Cast<float>().ToArray();

        Assert.NotNull(approximateBaseColor);
        Assert.Equal([0.97f, 0.83f, 0.55f, 1f], approximateBaseColor!);
    }

    [Fact]
    public void MaterialDecoder_AllowsSlightlyNegativeAlpha_InApproximateColorFallback()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var categoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");

        var vector4Category = Enum.Parse(categoryType, "Vector4");
        var floatVectorRepresentation = Enum.Parse(representationType, "FloatVector");
        var anonymousColorProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x6E56548Au, "0x6E56548A", 4u, 4u, 4u, vector4Category, floatVectorRepresentation, "vector=[0.5,0.5,0.5,-0.003655]", new[] { 0.5f, 0.5f, 0.5f, -0.003655f }, null, null],
            culture: null)!;
        var properties = Array.CreateInstance(materialPropertyType, 1);
        properties.SetValue(anonymousColorProperty, 0);

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
            args: [0xFC5FC212u, "DecalMap", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)],
            culture: null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var approximateBaseColor = ((System.Collections.IEnumerable?)decoded.GetType().GetProperty("ApproximateBaseColor")!.GetValue(decoded))?.Cast<float>().ToArray();

        Assert.NotNull(approximateBaseColor);
        Assert.Equal([0.5f, 0.5f, 0.5f, 1f], approximateBaseColor!);
    }

    [Theory]
    [InlineData("PointLightTemplates", "Shader_Test")]
    [InlineData("samplerCASPeltEditTexture", "Shader_Test")]
    [InlineData(null, "Shader_PointLightTemplates")]
    public void BuildBuySceneBuildService_RecognizesNonVisualHelperFamilies(string? familyName, string shaderName)
    {
        var method = typeof(BuildBuySceneBuildService).GetMethod("LooksLikeNonVisualHelperFamily", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        var result = (bool?)method?.Invoke(null, [familyName, shaderName]);
        Assert.True(result);
    }

    [Fact]
    public void BuildBuySceneBuildService_RecognizesUtilityOnlyDecalAsNonVisualHelper()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var decodeResultType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecodeResult");
        var familyKindType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialFamilyKind");
        var coverageTierType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialCoverageTier");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var samplingInstructionType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialSamplingInstruction");

        var decodeResult = Activator.CreateInstance(
            decodeResultType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "DecalMap",
                "DecalMap",
                Enum.Parse(familyKindType, "Unknown"),
                "AlphaCutoutMaterialDecodeStrategy",
                Enum.Parse(coverageTierType, "StaticReady"),
                Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 20f, 1f, 0f, 0f], null)!,
                Array.CreateInstance(samplingInstructionType, 0),
                null!,
                null!,
                Array.Empty<string>(),
                new[] { "normal" },
                true,
                "alpha-test-or-blend",
                Array.Empty<string>()
            ],
            culture: null)!;

        var method = typeof(BuildBuySceneBuildService).GetMethod("LooksLikeNonVisualHelperMaterial", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        var result = (bool?)method?.Invoke(null, [decodeResult, "Shader_FC5FC212"]);
        Assert.True(result);
    }

    [Fact]
    public void BuildBuySceneBuildService_RecognizesPaintingWithoutPortablePayloadAsNonVisualHelper()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var decodeResultType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecodeResult");
        var familyKindType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialFamilyKind");
        var coverageTierType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialCoverageTier");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var samplingInstructionType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialSamplingInstruction");

        var decodeResult = Activator.CreateInstance(
            decodeResultType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "painting",
                "painting",
                Enum.Parse(familyKindType, "Unknown"),
                "DefaultMaterialDecodeStrategy",
                Enum.Parse(coverageTierType, "StaticReady"),
                Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 20f, 1f, 0f, 0f], null)!,
                Array.CreateInstance(samplingInstructionType, 0),
                null!,
                null!,
                Array.Empty<string>(),
                Array.Empty<string>(),
                false,
                null!,
                Array.Empty<string>()
            ],
            culture: null)!;

        var method = typeof(BuildBuySceneBuildService).GetMethod("LooksLikeNonVisualHelperMaterial", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        var result = (bool?)method?.Invoke(null, [decodeResult, "Shader_5AF16731"]);
        Assert.True(result);
    }

    [Theory]
    [InlineData("diffuse", true)]
    [InlineData("overlay", true)]
    [InlineData("decal", true)]
    [InlineData("emissive", true)]
    [InlineData("detail", true)]
    [InlineData("dirt", true)]
    [InlineData("alpha", false)]
    [InlineData("specular", false)]
    [InlineData("normal", false)]
    [InlineData("texture_1", false)]
    public void BuildBuySceneBuildService_ClassifiesVisualColorTextureSlots(string slot, bool expected)
    {
        var method = typeof(BuildBuySceneBuildService).GetMethod("IsVisualColorTextureSlot", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        var result = (bool?)method?.Invoke(null, [slot]);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildBuySceneBuildService_AddsFallbackBaseColor_WhenOnlyUtilityTexturesExist()
    {
        var textures = new[]
        {
            new CanonicalTexture(
                "alpha",
                "alpha.png",
                Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAASsJTYQAAAAASUVORK5CYII="),
                null,
                null,
                CanonicalTextureSemantic.Opacity,
                CanonicalTextureSourceKind.ExplicitLocal,
                UvChannel: 0)
        };

        var method = typeof(BuildBuySceneBuildService).GetMethod("ShouldAddFallbackBaseColor", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        var result = (bool?)method?.Invoke(null, [textures]);
        Assert.True(result);
    }

    [Fact]
    public void BuildBuySceneBuildService_DoesNotBlockFallbackBaseColor_WhenSameTextureIsAlreadyUsedAsAlpha()
    {
        var sourceKey = new ResourceKeyRecord(0x00B2D882, 0, 0x00DB8294F40944D4, "DSTImage");
        var existingTextures = new[]
        {
            new CanonicalTexture(
                "alpha",
                "alpha.png",
                Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAASsJTYQAAAAASUVORK5CYII="),
                sourceKey,
                null,
                CanonicalTextureSemantic.Opacity,
                CanonicalTextureSourceKind.ExplicitLocal,
                UvChannel: 0)
        };
        var fallbackBaseColor = new CanonicalTexture(
            "diffuse",
            "diffuse.png",
            Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAASsJTYQAAAAASUVORK5CYII="),
            sourceKey,
            null,
            CanonicalTextureSemantic.BaseColor,
            CanonicalTextureSourceKind.FallbackSameInstanceLocal,
            UvChannel: 0);

        var method = typeof(BuildBuySceneBuildService).GetMethod("HasMatchingBaseColorTexture", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        var result = (bool?)method?.Invoke(null, [existingTextures, fallbackBaseColor]);
        Assert.False(result);
    }

    [Fact]
    public void MaterialDecoder_CreateSyntheticSamplingInstruction_UsesDiffusePathForMaterialColorPreview()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var coverageTierType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialCoverageTier");

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

        var instruction = decoderType.GetMethod("CreateSyntheticSamplingInstruction", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.Invoke(
            null,
            [material, "material-color", Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 1f, 1f, 0f, 0f], null)!, "SeasonalFoliage", "SeasonalFoliage", Enum.Parse(coverageTierType, "StaticReady"), null])!;

        var source = (string)instruction.GetType().GetProperty("Source")!.GetValue(instruction)!;
        var slot = (string)instruction.GetType().GetProperty("Slot")!.GetValue(instruction)!;

        Assert.Equal("material-color", slot);
        Assert.Equal("diffuse-material-path", source);
    }

    [Fact]
    public void MaterialDecoder_TreatsGenericTextureSlotsAsSharedMaterialUvPath()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var resourceKeyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ResourceKey");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var coverageTierType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialCoverageTier");

        var key = Activator.CreateInstance(
            resourceKeyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x00B2D882u, 0u, 0xDE685457A63BED14ul],
            culture: null)!;
        var textureReference = Activator.CreateInstance(
            textureReferenceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: ["texture_1", key, 0x12345678u],
            culture: null)!;
        var references = Array.CreateInstance(textureReferenceType, 1);
        references.SetValue(textureReference, 0);

        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "Material_Test",
                "Shader_Test",
                Array.CreateInstance(materialPropertyType, 0),
                references,
                Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 2f, 2f, 0.25f, 0.5f], null)!
            ],
            culture: null)!;

        var instructions = ((System.Collections.IEnumerable)decoderType
            .GetMethod("BuildSamplingInstructions", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [material, Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 2f, 2f, 0.25f, 0.5f], null)!, "Shader_Test", "Shader_Test", Enum.Parse(coverageTierType, "StaticReady")])!)
            .Cast<object>()
            .ToArray();

        var instruction = Assert.Single(instructions);
        var source = (string)instruction.GetType().GetProperty("Source")!.GetValue(instruction)!;
        var scaleU = (float)instruction.GetType().GetProperty("UvScaleU")!.GetValue(instruction)!;
        var offsetV = (float)instruction.GetType().GetProperty("UvOffsetV")!.GetValue(instruction)!;

        Assert.Equal("diffuse-material-path", source);
        Assert.Equal(2f, scaleU);
        Assert.Equal(0.5f, offsetV);
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
    public void MaterialDecoder_SelectsProjectiveStrategy_ForWorldDepthFamily()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var familyKindType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialFamilyKind");
        var categoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");

        var uvCategory = Enum.Parse(categoryType, "UvMapping");
        var floatVectorRepresentation = Enum.Parse(representationType, "FloatVector");
        var uvProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x420520E9u, "uvMapping", 1u, 4u, 4u, uvCategory, floatVectorRepresentation, "vector=[0,0,20,0.5]", new[] { 0f, 0f, 20f, 0.5f }, null, null],
            culture: null)!;
        var properties = Array.CreateInstance(materialPropertyType, 1);
        properties.SetValue(uvProperty, 0);

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
            args: [0xAC62C597u, "WorldToDepthMapSpaceMatrix", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)],
            culture: null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var strategyName = (string)decoded.GetType().GetProperty("StrategyName")!.GetValue(decoded)!;
        var familyKind = decoded.GetType().GetProperty("FamilyKind")!.GetValue(decoded)!;

        Assert.Equal("ProjectiveMaterialDecodeStrategy", strategyName);
        Assert.Equal(Enum.Parse(familyKindType, "Projective"), familyKind);
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
    public void MaterialDecoder_SelectsAlphaCutoutStrategy_ForPhongAlpha()
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
            args: [0xDEADBEEFu, "PhongAlpha", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)],
            culture: null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var strategyName = (string)decoded.GetType().GetProperty("StrategyName")!.GetValue(decoded)!;
        var familyKind = decoded.GetType().GetProperty("FamilyKind")!.GetValue(decoded)!;
        var suggestsAlpha = (bool)decoded.GetType().GetProperty("SuggestsAlphaCutout")!.GetValue(decoded)!;

        Assert.Equal("AlphaCutoutMaterialDecodeStrategy", strategyName);
        Assert.Equal(Enum.Parse(familyKindType, "AlphaCutout"), familyKind);
        Assert.True(suggestsAlpha);
    }

    [Fact]
    public void MaterialDecoder_SelectsColorMapStrategy_ForOtherColorMapFamilies()
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
            args: [0xCAFEBABEu, "colorMap4", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)],
            culture: null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var strategyName = (string)decoded.GetType().GetProperty("StrategyName")!.GetValue(decoded)!;
        var familyKind = decoded.GetType().GetProperty("FamilyKind")!.GetValue(decoded)!;

        Assert.Equal("ColorMapMaterialDecodeStrategy", strategyName);
        Assert.Equal(Enum.Parse(familyKindType, "ColorMap"), familyKind);
    }

    [Fact]
    public void MaterialDecoder_SelectsStandardSurfaceStrategy_ForPhongFamily()
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
            args: [0xFACEFEEDu, "Phong", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)],
            culture: null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var strategyName = (string)decoded.GetType().GetProperty("StrategyName")!.GetValue(decoded)!;
        var familyKind = decoded.GetType().GetProperty("FamilyKind")!.GetValue(decoded)!;

        Assert.Equal("StandardSurfaceMaterialDecodeStrategy", strategyName);
        Assert.Equal(Enum.Parse(familyKindType, "StandardSurface"), familyKind);
    }

    [Fact]
    public void MaterialDecoder_ColorMapFamilyAppliesLegacyUvSelector()
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
            args: [0xCAFEBABEu, "colorMap4", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)],
            culture: null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var mapping = decoded.GetType().GetProperty("DiffuseUvMapping")!.GetValue(decoded)!;
        var notes = (System.Collections.IEnumerable)decoded.GetType().GetProperty("Notes")!.GetValue(decoded)!;
        var noteText = string.Join(" | ", notes.Cast<object>());

        Assert.Equal(1, (int)uvMappingType.GetProperty("UvChannel")!.GetValue(mapping)!);
        Assert.Contains("colorMap legacy UV channel selector", noteText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MaterialDecoder_ProducesSamplingInstructions_ForTextureSlots()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var resourceKeyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ResourceKey");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");

        var key = Activator.CreateInstance(
            resourceKeyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x00B2D882u, 0u, 0x0115AAEAD51B0391ul],
            culture: null)!;
        var diffuseRef = Activator.CreateInstance(
            textureReferenceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: ["diffuse", key, 0x1B9D3AC5u],
            culture: null)!;
        var normalRef = Activator.CreateInstance(
            textureReferenceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: ["normal", key, 0x9F5D1C9Bu],
            culture: null)!;
        var textureRefs = Array.CreateInstance(textureReferenceType, 2);
        textureRefs.SetValue(diffuseRef, 0);
        textureRefs.SetValue(normalRef, 1);
        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "Material_Test",
                "Shader_Test",
                Array.CreateInstance(materialPropertyType, 0),
                textureRefs,
                Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [1, 0.5f, 0.25f, 0.125f, 0.75f], null)!
            ],
            culture: null)!;
        var profile = Activator.CreateInstance(
            shaderProfileType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0xB9105A6Du, "Phong", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)],
            culture: null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var instructions = ((System.Collections.IEnumerable)decoded.GetType().GetProperty("SamplingInstructions")!.GetValue(decoded)!).Cast<object>().ToArray();

        Assert.Equal(2, instructions.Length);
        var diffuse = instructions.Single(instruction => string.Equals((string)instruction.GetType().GetProperty("Slot")!.GetValue(instruction)!, "diffuse", StringComparison.OrdinalIgnoreCase));
        var normal = instructions.Single(instruction => string.Equals((string)instruction.GetType().GetProperty("Slot")!.GetValue(instruction)!, "normal", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(1, (int)diffuse.GetType().GetProperty("UvChannel")!.GetValue(diffuse)!);
        Assert.Equal(0.5f, (float)diffuse.GetType().GetProperty("UvScaleU")!.GetValue(diffuse)!, 3);
        Assert.Equal("diffuse-material-path", (string)diffuse.GetType().GetProperty("Source")!.GetValue(diffuse)!);

        Assert.Equal(1, (int)normal.GetType().GetProperty("UvChannel")!.GetValue(normal)!);
        Assert.Equal(0.5f, (float)normal.GetType().GetProperty("UvScaleU")!.GetValue(normal)!, 3);
        Assert.Equal("diffuse-material-path", (string)normal.GetType().GetProperty("Source")!.GetValue(normal)!);
    }

    [Fact]
    public void MaterialDecoder_AppliesOverlayUvRules_FromRampAndPaintSemantics()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var resourceKeyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ResourceKey");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var shaderCategoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");

        var boolLikeCategory = Enum.Parse(shaderCategoryType, "BoolLike");
        var scalarRepresentation = Enum.Parse(representationType, "Scalar");
        var overlayUvProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x23F2B29Fu, "PaintUsesUV1", 1u, 1u, 1u, boolLikeCategory, scalarRepresentation, "scalar=1", new[] { 1f }, null, null],
            culture: null)!;
        var properties = Array.CreateInstance(materialPropertyType, 1);
        properties.SetValue(overlayUvProperty, 0);

        var key = Activator.CreateInstance(
            resourceKeyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x00B2D882u, 0u, 0x0297B6B3762A64EAul],
            culture: null)!;
        var overlayRef = Activator.CreateInstance(
            textureReferenceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: ["overlay", key, 0x96D804A0u],
            culture: null)!;
        var textureRefs = Array.CreateInstance(textureReferenceType, 1);
        textureRefs.SetValue(overlayRef, 0);

        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "Material_Test",
                "Shader_Test",
                properties,
                textureRefs,
                Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 1f, 1f, 0f, 0f], null)!
            ],
            culture: null)!;
        var profile = Activator.CreateInstance(
            shaderProfileType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0xAC62C597u, "WorldToDepthMapSpaceMatrix", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)],
            culture: null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var instructions = ((System.Collections.IEnumerable)decoded.GetType().GetProperty("SamplingInstructions")!.GetValue(decoded)!).Cast<object>().ToArray();
        var overlay = instructions.Single(instruction => string.Equals((string)instruction.GetType().GetProperty("Slot")!.GetValue(instruction)!, "overlay", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(1, (int)overlay.GetType().GetProperty("UvChannel")!.GetValue(overlay)!);
    }

    [Fact]
    public void MaterialDecoder_DecodesPackedUvMapping_FromHalfFloatWindow()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var shaderCategoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");

        var uvCategory = Enum.Parse(shaderCategoryType, "UvMapping");
        var packedRepresentation = Enum.Parse(representationType, "PackedUInt32");
        var property = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x420520E9u, "uvMapping", 1u, 1u, 4u, uvCategory, packedRepresentation, "packed32=[...]", null, new uint[] { 0x34003800, 0x3A003400 }, null],
            culture: null)!;
        var mapping = Activator.CreateInstance(
            uvMappingType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0, 1f, 1f, 0f, 0f],
            culture: null)!;
        var args = new object?[] { property, mapping, null, null };

        var interpreted = (bool)decoderType
            .GetMethod("TryInterpretPackedUvMapping", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, args)!;

        Assert.True(interpreted);
        Assert.NotNull(args[2]);
        Assert.Contains("half-float", (string)args[3]!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0.5f, (float)uvMappingType.GetProperty("UvScaleU")!.GetValue(args[2])!, 3);
        Assert.Equal(0.25f, (float)uvMappingType.GetProperty("UvScaleV")!.GetValue(args[2])!, 3);
        Assert.Equal(0.25f, (float)uvMappingType.GetProperty("UvOffsetU")!.GetValue(args[2])!, 3);
        Assert.Equal(0.75f, (float)uvMappingType.GetProperty("UvOffsetV")!.GetValue(args[2])!, 3);
    }

    [Fact]
    public void MaterialDecoder_DecodesPackedAtlasMinIntoOffsets()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var shaderCategoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");

        var vectorCategory = Enum.Parse(shaderCategoryType, "Vector2");
        var packedRepresentation = Enum.Parse(representationType, "PackedUInt32");
        var property = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x20000000u, "DiffuseAtlasMin", 1u, 1u, 2u, vectorCategory, packedRepresentation, "packed32=[...]", null, new uint[] { 0x34CD3266 }, null],
            culture: null)!;
        var mapping = Activator.CreateInstance(
            uvMappingType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0, 1f, 1f, 0f, 0f],
            culture: null)!;
        var args = new object?[] { property, mapping, null, null };

        var interpreted = (bool)decoderType
            .GetMethod("TryInterpretPackedUvProperty", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, args)!;

        Assert.True(interpreted);
        Assert.NotNull(args[2]);
        Assert.Contains("DiffuseAtlasMin", (string)args[3]!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0.2f, (float)uvMappingType.GetProperty("UvOffsetU")!.GetValue(args[2])!, 3);
        Assert.Equal(0.3f, (float)uvMappingType.GetProperty("UvOffsetV")!.GetValue(args[2])!, 3);
    }

    [Fact]
    public void ShaderSemantics_InterpretsScaleAndOffsetVectorAsScaleAndOffset()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var semanticsType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ShaderSemantics");
        var parameterType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile");
        var categoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");

        var vector4Category = Enum.Parse(categoryType, "Vector4");
        var parameter = Activator.CreateInstance(
            parameterType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x11111111u, "OverlayWorldUVScaleAndOffset", 0u, 0u, vector4Category],
            culture: null)!;
        var current = Activator.CreateInstance(
            uvMappingType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0, 1f, 1f, 0f, 0f],
            culture: null)!;

        var args = new object?[] { parameter, new[] { 2f, 3f, 0.25f, 0.5f }, current, null };
        var interpreted = (bool)semanticsType
            .GetMethod("TryInterpretDiffuseUvMappingVector", BindingFlags.Public | BindingFlags.Static, null, [parameterType, typeof(float[]), uvMappingType, uvMappingType.MakeByRefType()], null)!
            .Invoke(null, args)!;

        Assert.True(interpreted);
        Assert.NotNull(args[3]);
        Assert.Equal(2f, (float)uvMappingType.GetProperty("UvScaleU")!.GetValue(args[3])!, 3);
        Assert.Equal(3f, (float)uvMappingType.GetProperty("UvScaleV")!.GetValue(args[3])!, 3);
        Assert.Equal(0.25f, (float)uvMappingType.GetProperty("UvOffsetU")!.GetValue(args[3])!, 3);
        Assert.Equal(0.5f, (float)uvMappingType.GetProperty("UvOffsetV")!.GetValue(args[3])!, 3);
    }

    [Fact]
    public void ShaderSemantics_InterpretsGenericAtlasVectorAsScaleAndOffset()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var semanticsType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ShaderSemantics");
        var parameterType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile");
        var categoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");

        var vector4Category = Enum.Parse(categoryType, "Vector4");
        var parameter = Activator.CreateInstance(
            parameterType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x22222222u, "CASHotSpotAtlas", 0u, 0u, vector4Category],
            culture: null)!;
        var current = Activator.CreateInstance(
            uvMappingType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0, 1f, 1f, 0f, 0f],
            culture: null)!;

        var args = new object?[] { parameter, new[] { 0.5f, 0.25f, 0.125f, 0.75f }, current, null };
        var interpreted = (bool)semanticsType
            .GetMethod("TryInterpretDiffuseUvMappingVector", BindingFlags.Public | BindingFlags.Static, null, [parameterType, typeof(float[]), uvMappingType, uvMappingType.MakeByRefType()], null)!
            .Invoke(null, args)!;

        Assert.True(interpreted);
        Assert.NotNull(args[3]);
        Assert.Equal(0.5f, (float)uvMappingType.GetProperty("UvScaleU")!.GetValue(args[3])!, 3);
        Assert.Equal(0.25f, (float)uvMappingType.GetProperty("UvScaleV")!.GetValue(args[3])!, 3);
        Assert.Equal(0.125f, (float)uvMappingType.GetProperty("UvOffsetU")!.GetValue(args[3])!, 3);
        Assert.Equal(0.75f, (float)uvMappingType.GetProperty("UvOffsetV")!.GetValue(args[3])!, 3);
    }

    [Fact]
    public void MaterialCoverageAnalyzer_ClassifiesRepresentativeProfiles()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var analyzerType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialCoverageAnalyzer");
        var tierType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialCoverageTier");

        var classify = analyzerType.GetMethod("ClassifyProfile", BindingFlags.NonPublic | BindingFlags.Static)!;

        var staticReady = classify.Invoke(null, ["Phong", new[] { "uvMapping", "DiffuseUVScaleAndOffset" }])!;
        var approximate = classify.Invoke(null, ["Phong", new[] { "uvMapping", "OverlayUVScale", "samplerOverlayMap", "samplerDecalMap" }])!;
        var runtimeDependent = classify.Invoke(null, ["Projected", new[] { "uvMapping", "gPosToUVDest", "UVScrollSpeed" }])!;
        var animatedApproximate = classify.Invoke(null, ["Animated", new[] { "uvMapping", "UVScrollSpeed", "VideoVTexture" }])!;
        var worldDepthRuntime = classify.Invoke(null, ["WorldToDepthMapSpaceMatrix", new[] { "uvMapping" }])!;

        Assert.Equal(Enum.Parse(tierType, "StaticReady"), staticReady);
        Assert.Equal(Enum.Parse(tierType, "Approximate"), approximate);
        Assert.Equal(Enum.Parse(tierType, "RuntimeDependent"), runtimeDependent);
        Assert.Equal(Enum.Parse(tierType, "Approximate"), animatedApproximate);
        Assert.Equal(Enum.Parse(tierType, "RuntimeDependent"), worldDepthRuntime);
    }

    [Fact]
    public void MaterialCoverageAnalyzer_ClassifiesMaterialInstancesMorePreciselyThanProfiles()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var analyzerType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialCoverageAnalyzer");
        var tierType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialCoverageTier");

        var classify = analyzerType.GetMethod("ClassifyMaterial", BindingFlags.NonPublic | BindingFlags.Static)!;

        var staticWorldDepth = classify.Invoke(null, ["WorldToDepthMapSpaceMatrix", new[] { "uvMapping" }])!;
        var animatedWorldDepth = classify.Invoke(null, ["WorldToDepthMapSpaceMatrix", new[] { "uvMapping", "WaterScrollSpeedLayer1" }])!;
        var clipSpaceWorldDepth = classify.Invoke(null, ["WorldToDepthMapSpaceMatrix", new[] { "uvMapping", "ClipSpaceOffset" }])!;
        var runtimeWorldDepth = classify.Invoke(null, ["WorldToDepthMapSpaceMatrix", new[] { "uvMapping", "gPosToUVDest" }])!;
        var weakAnimatedColorMap = classify.Invoke(null, ["colorMap7", new[] { "uvMapping", "WaterScrollSpeedLayer1" }])!;

        Assert.Equal(Enum.Parse(tierType, "StaticReady"), staticWorldDepth);
        Assert.Equal(Enum.Parse(tierType, "StaticReady"), animatedWorldDepth);
        Assert.Equal(Enum.Parse(tierType, "StaticReady"), clipSpaceWorldDepth);
        Assert.Equal(Enum.Parse(tierType, "RuntimeDependent"), runtimeWorldDepth);
        Assert.Equal(Enum.Parse(tierType, "StaticReady"), weakAnimatedColorMap);

        var staticSimWings = classify.Invoke(null, ["SimWingsUV", new[] { "uvMapping", "VideoUVScale", "WaterScrollSpeedLayer1", "DetailNormalMapScale", "NormalMapTileable" }])!;
        Assert.Equal(Enum.Parse(tierType, "StaticReady"), staticSimWings);
    }

    [Fact]
    public void MaterialDecoder_ExposesCoverageTier()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var coverageTierType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialCoverageTier");

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
            args: [0xB9105A6Du, "Phong", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)],
            culture: null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var coverageTier = decoded.GetType().GetProperty("CoverageTier")!.GetValue(decoded);

        Assert.Equal(Enum.Parse(coverageTierType, "StaticReady"), coverageTier);
    }

    [Fact]
    public void MaterialDecoder_DowngradesWorldDepthFamilyToApproximate_WhenMaterialUsesOnlyAnimatedStaticSubset()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var resourceKeyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ResourceKey");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var coverageTierType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialCoverageTier");
        var shaderCategoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");

        var uvCategory = Enum.Parse(shaderCategoryType, "UvMapping");
        var boolLikeCategory = Enum.Parse(shaderCategoryType, "BoolLike");
        var floatVectorRepresentation = Enum.Parse(representationType, "FloatVector");
        var scalarRepresentation = Enum.Parse(representationType, "Scalar");

        var uvProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x420520E9u, "uvMapping", 4u, 4u, 4u, uvCategory, floatVectorRepresentation, "vector=[1,1,0,0]", new[] { 1f, 1f, 0f, 0f }, null, null],
            culture: null)!;
        var animatedProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x2CE11842u, "WaterScrollSpeedLayer1", 1u, 1u, 1u, boolLikeCategory, scalarRepresentation, "scalar=1", new[] { 1f }, null, null],
            culture: null)!;
        var properties = Array.CreateInstance(materialPropertyType, 2);
        properties.SetValue(uvProperty, 0);
        properties.SetValue(animatedProperty, 1);

        var key = Activator.CreateInstance(resourceKeyType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0x00B2D882u, 0u, 0x0115AAEAD51B0391ul], null)!;
        var diffuseRef = Activator.CreateInstance(textureReferenceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ["diffuse", key, 0x1B9D3AC5u], null)!;
        var textureRefs = Array.CreateInstance(textureReferenceType, 1);
        textureRefs.SetValue(diffuseRef, 0);

        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "Material_Test",
                "Shader_Test",
                properties,
                textureRefs,
                Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 1f, 1f, 0f, 0f], null)!
            ],
            culture: null)!;
        var profile = Activator.CreateInstance(
            shaderProfileType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0xAC62C597u, "WorldToDepthMapSpaceMatrix", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)],
            culture: null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var coverageTier = decoded.GetType().GetProperty("CoverageTier")!.GetValue(decoded);
        var notes = string.Join(" | ", ((System.Collections.IEnumerable)decoded.GetType().GetProperty("Notes")!.GetValue(decoded)!).Cast<object>());
        var instruction = ((System.Collections.IEnumerable)decoded.GetType().GetProperty("SamplingInstructions")!.GetValue(decoded)!).Cast<object>().Single();

        Assert.Equal(Enum.Parse(coverageTierType, "StaticReady"), coverageTier);
        Assert.DoesNotContain("Projective or world-space", notes, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("diffuse-material-path", (string)instruction.GetType().GetProperty("Source")!.GetValue(instruction)!);
    }

    [Fact]
    public void MaterialDecoder_TreatsClipSpaceOffsetWithoutStrongProjectiveProps_AsDiffuseMaterialPath()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var resourceKeyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ResourceKey");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var shaderCategoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");

        var vector2Category = Enum.Parse(shaderCategoryType, "Vector2");
        var boolLikeCategory = Enum.Parse(shaderCategoryType, "BoolLike");
        var floatVectorRepresentation = Enum.Parse(representationType, "FloatVector");
        var clipSpaceProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0xAFC3355Fu, "ClipSpaceOffset", 4u, 4u, 2u, vector2Category, floatVectorRepresentation, "vector=[0.5,0.5]", new[] { 0.5f, 0.5f }, null, null],
            culture: null)!;
        var animatedProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x2CE11842u, "WaterScrollSpeedLayer1", 4u, 4u, 2u, boolLikeCategory, floatVectorRepresentation, "vector=[0,0]", new[] { 0f, 0f }, null, null],
            culture: null)!;
        var properties = Array.CreateInstance(materialPropertyType, 2);
        properties.SetValue(clipSpaceProperty, 0);
        properties.SetValue(animatedProperty, 1);

        var key = Activator.CreateInstance(resourceKeyType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0x00B2D882u, 0u, 0x0124E3B8AC7BEE62ul], null)!;
        var diffuseRef = Activator.CreateInstance(textureReferenceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ["diffuse", key, 0x1B9D3AC5u], null)!;
        var textureRefs = Array.CreateInstance(textureReferenceType, 1);
        textureRefs.SetValue(diffuseRef, 0);

        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "Material_Test",
                "Shader_Test",
                properties,
                textureRefs,
                Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 1f, 1f, 0f, 0f], null)!
            ],
            culture: null)!;
        var profile = Activator.CreateInstance(
            shaderProfileType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0xAC62C597u, "SeasonalFoliage", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)],
            culture: null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var instruction = ((System.Collections.IEnumerable)decoded.GetType().GetProperty("SamplingInstructions")!.GetValue(decoded)!).Cast<object>().Single();
        var notes = string.Join(" | ", ((System.Collections.IEnumerable)decoded.GetType().GetProperty("Notes")!.GetValue(decoded)!).Cast<object>());

        Assert.Equal("diffuse-material-path", (string)instruction.GetType().GetProperty("Source")!.GetValue(instruction)!);
        Assert.DoesNotContain("Projective or world-space", notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MaterialDecoder_DoesNotTreatWeakAnimatedControl_AsAnimatedStillFrame_ForStaticColorMap()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var resourceKeyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ResourceKey");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var shaderCategoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");
        var coverageTierType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialCoverageTier");

        var uvCategory = Enum.Parse(shaderCategoryType, "UvMapping");
        var boolLikeCategory = Enum.Parse(shaderCategoryType, "BoolLike");
        var floatVectorRepresentation = Enum.Parse(representationType, "FloatVector");
        var uvProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x420520E9u, "uvMapping", 4u, 4u, 4u, uvCategory, floatVectorRepresentation, "vector=[1,0,0,1]", new[] { 1f, 0f, 0f, 1f }, null, null],
            culture: null)!;
        var animatedProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x2CE11842u, "WaterScrollSpeedLayer1", 4u, 4u, 2u, boolLikeCategory, floatVectorRepresentation, "vector=[0,0]", new[] { 0f, 0f }, null, null],
            culture: null)!;
        var properties = Array.CreateInstance(materialPropertyType, 2);
        properties.SetValue(uvProperty, 0);
        properties.SetValue(animatedProperty, 1);

        var key = Activator.CreateInstance(resourceKeyType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0x00B2D882u, 0u, 0x19B7AB077DB8A98Aul], null)!;
        var diffuseRef = Activator.CreateInstance(textureReferenceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ["diffuse", key, 0x1B9D3AC5u], null)!;
        var textureRefs = Array.CreateInstance(textureReferenceType, 1);
        textureRefs.SetValue(diffuseRef, 0);

        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "Material_Test",
                "Shader_Test",
                properties,
                textureRefs,
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
        var coverageTier = decoded.GetType().GetProperty("CoverageTier")!.GetValue(decoded);
        var notes = string.Join(" | ", ((System.Collections.IEnumerable)decoded.GetType().GetProperty("Notes")!.GetValue(decoded)!).Cast<object>());
        var instruction = ((System.Collections.IEnumerable)decoded.GetType().GetProperty("SamplingInstructions")!.GetValue(decoded)!).Cast<object>().Single();

        Assert.Equal(Enum.Parse(coverageTierType, "StaticReady"), coverageTier);
        Assert.Equal("diffuse-material-path", (string)instruction.GetType().GetProperty("Source")!.GetValue(instruction)!);
        Assert.DoesNotContain("Animated UV controls are ignored", notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MaterialDecoder_CreatesSyntheticSamplingInstruction_ForFallbackTextures()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var tierType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialCoverageTier");
        var shaderCategoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");

        var scalarCategory = Enum.Parse(shaderCategoryType, "Scalar");
        var scalarRepresentation = Enum.Parse(representationType, "Scalar");
        var animatedProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x2CE11842u, "WaterScrollSpeedLayer1", 1u, 1u, 1u, scalarCategory, scalarRepresentation, "scalar=1", new[] { 1f }, null, null],
            culture: null)!;
        var properties = Array.CreateInstance(materialPropertyType, 1);
        properties.SetValue(animatedProperty, 0);

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
        var coverageTier = Enum.Parse(tierType, "Approximate");
        var mapping = Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 1f, 1f, 0f, 0f], null)!;

        var instruction = decoderType
            .GetMethod("CreateSyntheticSamplingInstruction", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [material, "diffuse", mapping, "WorldToDepthMapSpaceMatrix", "WorldToDepthMapSpaceMatrix", coverageTier, null])!;

        Assert.Equal("diffuse-material-path", (string)instruction.GetType().GetProperty("Source")!.GetValue(instruction)!);
        Assert.False((bool)instruction.GetType().GetProperty("IsApproximate")!.GetValue(instruction)!);
    }

    [Fact]
    public void MaterialDecoder_ProducesSlotSpecificSamplingInstructions()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var resourceKeyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ResourceKey");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var shaderCategoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");

        var vector2Category = Enum.Parse(shaderCategoryType, "Vector2");
        var boolLikeCategory = Enum.Parse(shaderCategoryType, "BoolLike");
        var floatVectorRepresentation = Enum.Parse(representationType, "FloatVector");
        var scalarRepresentation = Enum.Parse(representationType, "Scalar");
        var normalScaleProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x22F8EAC9u, "NormalUVScale", 4u, 4u, 2u, vector2Category, floatVectorRepresentation, "vector=[2, 3]", new[] { 2f, 3f }, null, null],
            culture: null)!;
        var overlayUv1Property = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x23F2B29Fu, "DirtOverlayUsesUV1", 1u, 1u, 1u, boolLikeCategory, scalarRepresentation, "scalar=1", new[] { 1f }, null, null],
            culture: null)!;
        var properties = Array.CreateInstance(materialPropertyType, 2);
        properties.SetValue(normalScaleProperty, 0);
        properties.SetValue(overlayUv1Property, 1);

        var key = Activator.CreateInstance(
            resourceKeyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x00B2D882u, 0u, 0x0115AAEAD51B0391ul],
            culture: null)!;
        var diffuseRef = Activator.CreateInstance(textureReferenceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ["diffuse", key, 0x1B9D3AC5u], null)!;
        var normalRef = Activator.CreateInstance(textureReferenceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ["normal", key, 0x9F5D1C9Bu], null)!;
        var overlayRef = Activator.CreateInstance(textureReferenceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ["overlay", key, 0x3A9F5D1Cu], null)!;
        var textureRefs = Array.CreateInstance(textureReferenceType, 3);
        textureRefs.SetValue(diffuseRef, 0);
        textureRefs.SetValue(normalRef, 1);
        textureRefs.SetValue(overlayRef, 2);

        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "Material_Test",
                "Shader_Test",
                properties,
                textureRefs,
                Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 0.5f, 0.25f, 0.125f, 0.75f], null)!
            ],
            culture: null)!;
        var profile = Activator.CreateInstance(
            shaderProfileType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0xB9105A6Du, "Phong", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)],
            culture: null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var instructions = ((System.Collections.IEnumerable)decoded.GetType().GetProperty("SamplingInstructions")!.GetValue(decoded)!).Cast<object>().ToArray();

        var normal = instructions.Single(instruction => string.Equals((string)instruction.GetType().GetProperty("Slot")!.GetValue(instruction)!, "normal", StringComparison.OrdinalIgnoreCase));
        var overlay = instructions.Single(instruction => string.Equals((string)instruction.GetType().GetProperty("Slot")!.GetValue(instruction)!, "overlay", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(2f, (float)normal.GetType().GetProperty("UvScaleU")!.GetValue(normal)!, 3);
        Assert.Equal(3f, (float)normal.GetType().GetProperty("UvScaleV")!.GetValue(normal)!, 3);
        Assert.Equal(1, (int)overlay.GetType().GetProperty("UvChannel")!.GetValue(overlay)!);
    }

    [Fact]
    public void MaterialDecoder_ProducesSlotSpecificSamplingInstructions_FromPackedUvPayload()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var resourceKeyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ResourceKey");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var shaderCategoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");

        var uvCategory = Enum.Parse(shaderCategoryType, "UvMapping");
        var packedRepresentation = Enum.Parse(representationType, "PackedUInt32");
        var normalPackedProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x30000000u, "NormalUvMapping", 1u, 1u, 4u, uvCategory, packedRepresentation, "packed32=[...]", null, new uint[] { 0x34003800, 0x3A003C00 }, null],
            culture: null)!;
        var properties = Array.CreateInstance(materialPropertyType, 1);
        properties.SetValue(normalPackedProperty, 0);

        var key = Activator.CreateInstance(
            resourceKeyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x00B2D882u, 0u, 0x0115AAEAD51B0391ul],
            culture: null)!;
        var normalRef = Activator.CreateInstance(textureReferenceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ["normal", key, 0x9F5D1C9Bu], null)!;
        var textureRefs = Array.CreateInstance(textureReferenceType, 1);
        textureRefs.SetValue(normalRef, 0);

        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "Material_Test",
                "Shader_Test",
                properties,
                textureRefs,
                Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 1f, 1f, 0f, 0f], null)!
            ],
            culture: null)!;
        var profile = Activator.CreateInstance(
            shaderProfileType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0xB9105A6Du, "Phong", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)],
            culture: null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var instructions = ((System.Collections.IEnumerable)decoded.GetType().GetProperty("SamplingInstructions")!.GetValue(decoded)!).Cast<object>().ToArray();
        var normal = instructions.Single();

        Assert.Equal("normal", (string)normal.GetType().GetProperty("Slot")!.GetValue(normal)!);
        Assert.Equal(0.5f, (float)normal.GetType().GetProperty("UvScaleU")!.GetValue(normal)!, 3);
        Assert.Equal(0.25f, (float)normal.GetType().GetProperty("UvScaleV")!.GetValue(normal)!, 3);
        Assert.Equal(1f, (float)normal.GetType().GetProperty("UvOffsetU")!.GetValue(normal)!, 3);
        Assert.Equal(0.75f, (float)normal.GetType().GetProperty("UvOffsetV")!.GetValue(normal)!, 3);
    }

    [Fact]
    public void MaterialDecoder_MarksProjectiveSamplingAsApproximate()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var resourceKeyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ResourceKey");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var categoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");

        var vector2Category = Enum.Parse(categoryType, "Vector2");
        var floatVectorRepresentation = Enum.Parse(representationType, "FloatVector");
        var projectiveProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x33333333u, "gPosToUVDest", 4u, 4u, 2u, vector2Category, floatVectorRepresentation, "vector=[1,1]", new[] { 1f, 1f }, null, null],
            culture: null)!;
        var properties = Array.CreateInstance(materialPropertyType, 1);
        properties.SetValue(projectiveProperty, 0);

        var key = Activator.CreateInstance(resourceKeyType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0x00B2D882u, 0u, 0x0115AAEAD51B0391ul], null)!;
        var diffuseRef = Activator.CreateInstance(textureReferenceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ["diffuse", key, 0x1B9D3AC5u], null)!;
        var textureRefs = Array.CreateInstance(textureReferenceType, 1);
        textureRefs.SetValue(diffuseRef, 0);

        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "Material_Test",
                "Shader_Test",
                properties,
                textureRefs,
                Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 1f, 1f, 0f, 0f], null)!
            ],
            culture: null)!;
        var profile = Activator.CreateInstance(shaderProfileType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0xB9105A6Du, "Phong", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)], null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var instruction = ((System.Collections.IEnumerable)decoded.GetType().GetProperty("SamplingInstructions")!.GetValue(decoded)!).Cast<object>().Single();

        Assert.Equal("projective-uv-approximation", (string)instruction.GetType().GetProperty("Source")!.GetValue(instruction)!);
        Assert.True((bool)instruction.GetType().GetProperty("IsApproximate")!.GetValue(instruction)!);
    }

    [Fact]
    public void MaterialDecoder_MarksAnimatedSamplingAsApproximateStillFrame()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var resourceKeyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ResourceKey");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var scalarCategory = Enum.Parse(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory"), "Scalar");
        var scalarRepresentation = Enum.Parse(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation"), "Scalar");

        var animatedProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x44444444u, "UVScrollSpeed", 1u, 1u, 1u, scalarCategory, scalarRepresentation, "scalar=1", new[] { 1f }, null, null],
            culture: null)!;
        var properties = Array.CreateInstance(materialPropertyType, 1);
        properties.SetValue(animatedProperty, 0);

        var key = Activator.CreateInstance(resourceKeyType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0x00B2D882u, 0u, 0x0115AAEAD51B0391ul], null)!;
        var diffuseRef = Activator.CreateInstance(textureReferenceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ["diffuse", key, 0x1B9D3AC5u], null)!;
        var textureRefs = Array.CreateInstance(textureReferenceType, 1);
        textureRefs.SetValue(diffuseRef, 0);

        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "Material_Test",
                "Shader_Test",
                properties,
                textureRefs,
                Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 1f, 1f, 0f, 0f], null)!
            ],
            culture: null)!;
        var profile = Activator.CreateInstance(shaderProfileType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0xB9105A6Du, "Phong", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)], null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var instruction = ((System.Collections.IEnumerable)decoded.GetType().GetProperty("SamplingInstructions")!.GetValue(decoded)!).Cast<object>().Single();

        Assert.Equal("animated-still-frame", (string)instruction.GetType().GetProperty("Source")!.GetValue(instruction)!);
        Assert.True((bool)instruction.GetType().GetProperty("IsApproximate")!.GetValue(instruction)!);
    }

    [Fact]
    public void MaterialDecoder_KeepsWeakProjectiveFamilyWithoutAtlasSignal_OnDiffusePath()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var resourceKeyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ResourceKey");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var uvCategory = Enum.Parse(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory"), "UvMapping");
        var floatVectorRepresentation = Enum.Parse(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation"), "FloatVector");

        var uvProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x420520E9u, "uvMapping", 1u, 4u, 4u, uvCategory, floatVectorRepresentation, "vector=[0,0,20,0.5]", new[] { 0f, 0f, 20f, 0.5f }, null, null],
            culture: null)!;
        var properties = Array.CreateInstance(materialPropertyType, 1);
        properties.SetValue(uvProperty, 0);

        var key = Activator.CreateInstance(resourceKeyType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0x00B2D882u, 0u, 0x0124E3B8AC7BEE62ul], null)!;
        var diffuseRef = Activator.CreateInstance(textureReferenceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ["diffuse", key, 0x1B9D3AC5u], null)!;
        var textureRefs = Array.CreateInstance(textureReferenceType, 1);
        textureRefs.SetValue(diffuseRef, 0);

        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "Material_Test",
                "Shader_Test",
                properties,
                textureRefs,
                Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 1f, 1f, 0f, 0f], null)!
            ],
            culture: null)!;
        var profile = Activator.CreateInstance(shaderProfileType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0xAC62C597u, "WorldToDepthMapSpaceMatrix", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)], null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var instruction = ((System.Collections.IEnumerable)decoded.GetType().GetProperty("SamplingInstructions")!.GetValue(decoded)!).Cast<object>().Single();
        var notes = ((System.Collections.IEnumerable)decoded.GetType().GetProperty("Notes")!.GetValue(decoded)!).Cast<string>().ToArray();
        var coverageTierType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialCoverageTier");
        var coverageTier = decoded.GetType().GetProperty("CoverageTier")!.GetValue(decoded);

        Assert.Equal("diffuse-material-path", (string)instruction.GetType().GetProperty("Source")!.GetValue(instruction)!);
        Assert.False((bool)instruction.GetType().GetProperty("IsApproximate")!.GetValue(instruction)!);
        Assert.Equal(Enum.Parse(coverageTierType, "StaticReady"), coverageTier);
        Assert.DoesNotContain(notes, static note => note.Contains("Projective or world-space UV controls", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MaterialDecoder_ProjectiveStrategy_UsesStaticAtlasPath_FromUvMappingAndAtlasSampler()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var resourceKeyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ResourceKey");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var categoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");
        var coverageTierType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialCoverageTier");

        var uvCategory = Enum.Parse(categoryType, "UvMapping");
        var samplerCategory = Enum.Parse(categoryType, "Sampler");
        var floatVectorRepresentation = Enum.Parse(representationType, "FloatVector");
        var uvProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x420520E9u, "uvMapping", 1u, 4u, 4u, uvCategory, floatVectorRepresentation, "vector=[0,0.5,20,0.5]", new[] { 0f, 0.5f, 20f, 0.5f }, null, null],
            culture: null)!;
        var atlasProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x637DAA05u, "samplerCASMedatorGridTexture", 1u, 4u, 4u, samplerCategory, floatVectorRepresentation, "vector=[0.5,1,0,0]", new[] { 0.5f, 1f, 0f, 0f }, null, null],
            culture: null)!;
        var properties = Array.CreateInstance(materialPropertyType, 2);
        properties.SetValue(uvProperty, 0);
        properties.SetValue(atlasProperty, 1);

        var key = Activator.CreateInstance(resourceKeyType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0x00B2D882u, 0u, 0x0124E3B8AC7BEE62ul], null)!;
        var diffuseRef = Activator.CreateInstance(textureReferenceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ["diffuse", key, 0x1B9D3AC5u], null)!;
        var textureRefs = Array.CreateInstance(textureReferenceType, 1);
        textureRefs.SetValue(diffuseRef, 0);

        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "Material_Test",
                "Shader_Test",
                properties,
                textureRefs,
                Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 1f, 1f, 0f, 0f], null)!
            ],
            culture: null)!;
        var profile = Activator.CreateInstance(shaderProfileType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0xAC62C597u, "WorldToDepthMapSpaceMatrix", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)], null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var instruction = ((System.Collections.IEnumerable)decoded.GetType().GetProperty("SamplingInstructions")!.GetValue(decoded)!).Cast<object>().Single();
        var notes = ((System.Collections.IEnumerable)decoded.GetType().GetProperty("Notes")!.GetValue(decoded)!).Cast<string>().ToArray();
        var coverageTier = decoded.GetType().GetProperty("CoverageTier")!.GetValue(decoded);

        Assert.Equal("projective-static-atlas-path", (string)instruction.GetType().GetProperty("Source")!.GetValue(instruction)!);
        Assert.False((bool)instruction.GetType().GetProperty("IsApproximate")!.GetValue(instruction)!);
        Assert.Equal(0.5f, (float)instruction.GetType().GetProperty("UvScaleU")!.GetValue(instruction)!, 3);
        Assert.Equal(0.5f, (float)instruction.GetType().GetProperty("UvScaleV")!.GetValue(instruction)!, 3);
        Assert.Equal(0.5f, (float)instruction.GetType().GetProperty("UvOffsetV")!.GetValue(instruction)!, 3);
        Assert.Equal(Enum.Parse(coverageTierType, "StaticReady"), coverageTier);
        Assert.Contains(notes, static note => note.Contains("static atlas window", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MaterialDecoder_ProjectiveStrategy_CreatesSyntheticStaticAtlasInstruction_WhenNoTextureRefsExist()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var categoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");
        var coverageTierType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialCoverageTier");

        var uvCategory = Enum.Parse(categoryType, "UvMapping");
        var samplerCategory = Enum.Parse(categoryType, "Sampler");
        var floatVectorRepresentation = Enum.Parse(representationType, "FloatVector");
        var uvProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x420520E9u, "uvMapping", 1u, 4u, 4u, uvCategory, floatVectorRepresentation, "vector=[0,0.5,20,0.5]", new[] { 0f, 0.5f, 20f, 0.5f }, null, null],
            culture: null)!;
        var atlasProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x637DAA05u, "samplerCASMedatorGridTexture", 1u, 4u, 4u, samplerCategory, floatVectorRepresentation, "vector=[0.5,1,0,0]", new[] { 0.5f, 1f, 0f, 0f }, null, null],
            culture: null)!;
        var properties = Array.CreateInstance(materialPropertyType, 2);
        properties.SetValue(uvProperty, 0);
        properties.SetValue(atlasProperty, 1);

        var textureRefs = Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference"), 0);
        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "Material_Test",
                "Shader_Test",
                properties,
                textureRefs,
                Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 1f, 1f, 0f, 0f], null)!
            ],
            culture: null)!;
        var profile = Activator.CreateInstance(shaderProfileType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0xAC62C597u, "WorldToDepthMapSpaceMatrix", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)], null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var instruction = ((System.Collections.IEnumerable)decoded.GetType().GetProperty("SamplingInstructions")!.GetValue(decoded)!).Cast<object>().Single();
        var coverageTier = decoded.GetType().GetProperty("CoverageTier")!.GetValue(decoded);

        Assert.Equal("projective-static-atlas-path", (string)instruction.GetType().GetProperty("Source")!.GetValue(instruction)!);
        Assert.False((bool)instruction.GetType().GetProperty("IsApproximate")!.GetValue(instruction)!);
        Assert.Equal(Enum.Parse(coverageTierType, "StaticReady"), coverageTier);
    }

    [Fact]
    public void MaterialDecoder_ProjectiveStrategy_UsesPackedUvMapping_WhenCurrentMappingIsAlreadyNonIdentity()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var categoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");
        var coverageTierType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialCoverageTier");

        var uvCategory = Enum.Parse(categoryType, "UvMapping");
        var samplerCategory = Enum.Parse(categoryType, "Sampler");
        var packedRepresentation = Enum.Parse(representationType, "PackedUInt32");
        var floatVectorRepresentation = Enum.Parse(representationType, "FloatVector");
        var uvProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x420520E9u, "uvMapping", 1u, 4u, 4u, uvCategory, packedRepresentation, "packed32=[0x00B2D882, 0x00000000, 0x97451FED, 0xEF266310]", null, new uint[] { 0x00B2D882u, 0x00000000u, 0x97451FEDu, 0xEF266310u }, null],
            culture: null)!;
        var atlasProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x637DAA05u, "samplerCASMedatorGridTexture", 1u, 4u, 4u, samplerCategory, floatVectorRepresentation, "vector=[0.00003,0,0,0.65]", new[] { 0.00003f, 0f, 0f, 0.65f }, null, null],
            culture: null)!;
        var properties = Array.CreateInstance(materialPropertyType, 2);
        properties.SetValue(uvProperty, 0);
        properties.SetValue(atlasProperty, 1);

        var textureRefs = Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference"), 0);
        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "Material_Test",
                "Shader_Test",
                properties,
                textureRefs,
                Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 0.846f, 0.003f, 0f, 0f], null)!
            ],
            culture: null)!;
        var profile = Activator.CreateInstance(shaderProfileType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0xAC62C597u, "WorldToDepthMapSpaceMatrix", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)], null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var instruction = ((System.Collections.IEnumerable)decoded.GetType().GetProperty("SamplingInstructions")!.GetValue(decoded)!).Cast<object>().Single();
        var coverageTier = decoded.GetType().GetProperty("CoverageTier")!.GetValue(decoded);

        var notes = ((System.Collections.IEnumerable)decoded.GetType().GetProperty("Notes")!.GetValue(decoded)!).Cast<string>().ToArray();

        Assert.Equal("diffuse-material-path", (string)instruction.GetType().GetProperty("Source")!.GetValue(instruction)!);
        Assert.True((bool)instruction.GetType().GetProperty("IsApproximate")!.GetValue(instruction)!);
        Assert.Equal(1f, (float)instruction.GetType().GetProperty("UvScaleU")!.GetValue(instruction)!, 3);
        Assert.Equal(1f, (float)instruction.GetType().GetProperty("UvScaleV")!.GetValue(instruction)!, 3);
        Assert.Equal(0f, (float)instruction.GetType().GetProperty("UvOffsetV")!.GetValue(instruction)!, 3);
        Assert.Equal(Enum.Parse(coverageTierType, "Approximate"), coverageTier);
        Assert.Contains(notes, static note => note.Contains("rejected as implausibly thin", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MaterialDecoder_RejectsPackedNormalizedUvMapping_WhenWindowFallsOutsideUnitSquare()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var resourceKeyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ResourceKey");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var categoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");

        var uvCategory = Enum.Parse(categoryType, "UvMapping");
        var packedRepresentation = Enum.Parse(representationType, "PackedUInt32");
        var uvProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x420520E9u, "uvMapping", 1u, 4u, 4u, uvCategory, packedRepresentation, "packed32", null, new uint[] { 0x451EC082u, 0xF3F74395u }, null],
            culture: null)!;
        var properties = Array.CreateInstance(materialPropertyType, 1);
        properties.SetValue(uvProperty, 0);

        var key = Activator.CreateInstance(resourceKeyType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0x00B2D882u, 0u, 0x018AD2B576C4802Cul], null)!;
        var diffuseRef = Activator.CreateInstance(textureReferenceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ["diffuse", key, 0x1B9D3AC5u], null)!;
        var textureRefs = Array.CreateInstance(textureReferenceType, 1);
        textureRefs.SetValue(diffuseRef, 0);

        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "Material_Test",
                "Shader_B9105A6D",
                properties,
                textureRefs,
                Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 1f, 1f, 0f, 0f], null)!
            ],
            culture: null)!;
        var profile = Activator.CreateInstance(shaderProfileType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0xB9105A6Du, "colorMap7", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)], null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var instruction = ((System.Collections.IEnumerable)decoded.GetType().GetProperty("SamplingInstructions")!.GetValue(decoded)!).Cast<object>().Single();

        Assert.Equal(1f, (float)instruction.GetType().GetProperty("UvScaleU")!.GetValue(instruction)!, 3);
        Assert.Equal(1f, (float)instruction.GetType().GetProperty("UvScaleV")!.GetValue(instruction)!, 3);
        Assert.Equal(0f, (float)instruction.GetType().GetProperty("UvOffsetU")!.GetValue(instruction)!, 3);
        Assert.Equal(0f, (float)instruction.GetType().GetProperty("UvOffsetV")!.GetValue(instruction)!, 3);
    }

    [Fact]
    public void MaterialDecoder_SpecularEnvMap_UsesSparseUvMappingApproximation()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var resourceKeyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ResourceKey");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");
        var categoryType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterCategory");
        var representationType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialValueRepresentation");

        var uvCategory = Enum.Parse(categoryType, "UvMapping");
        var floatVectorRepresentation = Enum.Parse(representationType, "FloatVector");
        var uvProperty = Activator.CreateInstance(
            materialPropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [0x420520E9u, "uvMapping", 1u, 4u, 4u, uvCategory, floatVectorRepresentation, "vector=[20,0,0,0]", new[] { 20f, 0f, 0f, 0f }, null, null],
            culture: null)!;
        var properties = Array.CreateInstance(materialPropertyType, 1);
        properties.SetValue(uvProperty, 0);

        var key = Activator.CreateInstance(resourceKeyType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0x00B2D882u, 0u, 0x029CEBD3D822E09Eul], null)!;
        var diffuseRef = Activator.CreateInstance(textureReferenceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ["diffuse", key, 0x1B9D3AC5u], null)!;
        var textureRefs = Array.CreateInstance(textureReferenceType, 1);
        textureRefs.SetValue(diffuseRef, 0);

        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "Material_Test",
                "Shader_Test",
                properties,
                textureRefs,
                Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 1f, 1f, 0f, 0f], null)!
            ],
            culture: null)!;
        var profile = Activator.CreateInstance(shaderProfileType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0x3BACC0D7u, "SpecularEnvMap", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)], null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var instruction = ((System.Collections.IEnumerable)decoded.GetType().GetProperty("SamplingInstructions")!.GetValue(decoded)!).Cast<object>().Single();
        var notes = ((System.Collections.IEnumerable)decoded.GetType().GetProperty("Notes")!.GetValue(decoded)!).Cast<string>().ToArray();
        var strategyName = (string)decoded.GetType().GetProperty("StrategyName")!.GetValue(decoded)!;

        Assert.Equal("SpecularEnvMapMaterialDecodeStrategy", strategyName);
        Assert.Equal(20f, (float)instruction.GetType().GetProperty("UvScaleU")!.GetValue(instruction)!, 3);
        Assert.Equal(1f, (float)instruction.GetType().GetProperty("UvScaleV")!.GetValue(instruction)!, 3);
        Assert.Equal("diffuse-material-path", (string)instruction.GetType().GetProperty("Source")!.GetValue(instruction)!);
        Assert.Contains(notes, static note => note.Contains("SpecularEnvMap sparse uvMapping approximation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MaterialDecoder_ChoosesExplicitAlphaSourceSlot()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var resourceKeyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ResourceKey");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");

        var key = Activator.CreateInstance(resourceKeyType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0x00B2D882u, 0u, 0x0115AAEAD51B0391ul], null)!;
        var diffuseRef = Activator.CreateInstance(textureReferenceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ["diffuse", key, 0x1B9D3AC5u], null)!;
        var opacityRef = Activator.CreateInstance(textureReferenceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ["opacity", key, 0x3A9F5D1Cu], null)!;
        var textureRefs = Array.CreateInstance(textureReferenceType, 2);
        textureRefs.SetValue(diffuseRef, 0);
        textureRefs.SetValue(opacityRef, 1);

        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "Material_Test",
                "Shader_Test",
                Array.CreateInstance(materialPropertyType, 0),
                textureRefs,
                Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 1f, 1f, 0f, 0f], null)!
            ],
            culture: null)!;
        var profile = Activator.CreateInstance(shaderProfileType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0xB9105A6Du, "PhongAlpha", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)], null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var alphaSourceSlot = (string?)decoded.GetType().GetProperty("AlphaSourceSlot")!.GetValue(decoded);

        Assert.Equal("opacity", alphaSourceSlot);
    }

    [Fact]
    public void MaterialDecoder_DoesNotTreatOverlayAsAlphaWithoutHints()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var resourceKeyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ResourceKey");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");

        var key = Activator.CreateInstance(resourceKeyType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0x00B2D882u, 0u, 0x0115AAEAD51B0391ul], null)!;
        var overlayRef = Activator.CreateInstance(textureReferenceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ["overlay", key, 0x3A9F5D1Cu], null)!;
        var textureRefs = Array.CreateInstance(textureReferenceType, 1);
        textureRefs.SetValue(overlayRef, 0);

        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "Material_Test",
                "Shader_Test",
                Array.CreateInstance(materialPropertyType, 0),
                textureRefs,
                Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 1f, 1f, 0f, 0f], null)!
            ],
            culture: null)!;
        var profile = Activator.CreateInstance(shaderProfileType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0xB9105A6Du, "Phong", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)], null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var alphaSourceSlot = (string?)decoded.GetType().GetProperty("AlphaSourceSlot")!.GetValue(decoded);

        Assert.Null(alphaSourceSlot);
    }

    [Fact]
    public void MaterialDecoder_LayeredColorSlotsExcludeUtilityMaps()
    {
        var previewAssembly = typeof(BuildBuySceneBuildService).Assembly;
        var shaderProfileType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderBlockProfile");
        var materialIrType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIr");
        var materialPropertyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.MaterialIrProperty");
        var uvMappingType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureUvMapping");
        var textureReferenceType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4TextureReference");
        var resourceKeyType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4ResourceKey");
        var decoderType = RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.Ts4MaterialDecoder");

        var key = Activator.CreateInstance(resourceKeyType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0x00B2D882u, 0u, 0x0297B6B3762A64EAul], null)!;
        var overlayRef = Activator.CreateInstance(textureReferenceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ["overlay", key, 0x1u], null)!;
        var emissiveRef = Activator.CreateInstance(textureReferenceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ["emissive", key, 0x2u], null)!;
        var normalRef = Activator.CreateInstance(textureReferenceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ["normal", key, 0x3u], null)!;
        var specularRef = Activator.CreateInstance(textureReferenceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ["specular", key, 0x4u], null)!;
        var textureRefs = Array.CreateInstance(textureReferenceType, 4);
        textureRefs.SetValue(overlayRef, 0);
        textureRefs.SetValue(emissiveRef, 1);
        textureRefs.SetValue(normalRef, 2);
        textureRefs.SetValue(specularRef, 3);

        var material = Activator.CreateInstance(
            materialIrType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "Material_Test",
                "Shader_Test",
                Array.CreateInstance(materialPropertyType, 0),
                textureRefs,
                Activator.CreateInstance(uvMappingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0, 1f, 1f, 0f, 0f], null)!
            ],
            culture: null)!;
        var profile = Activator.CreateInstance(shaderProfileType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [0xB9105A6Du, "Phong", Array.CreateInstance(RequireType(previewAssembly, "Sims4ResourceExplorer.Preview.ShaderParameterProfile"), 0)], null)!;

        var decoded = decoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [material, profile])!;
        var layeredSlots = ((System.Collections.IEnumerable)decoded.GetType().GetProperty("LayeredColorSlots")!.GetValue(decoded)!).Cast<string>().ToArray();
        var utilitySlots = ((System.Collections.IEnumerable)decoded.GetType().GetProperty("UtilityTextureSlots")!.GetValue(decoded)!).Cast<string>().ToArray();

        Assert.Equal(["emissive", "overlay"], layeredSlots);
        Assert.Equal(["normal", "specular"], utilitySlots);
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
    public async Task CasLogicalAsset_FallsBackWhenCasPartParsingFails()
    {
        var packagePath = Path.Combine(tempRoot, "cas-fallback.package");
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

        var invalidCasPartBytes = CreateSyntheticCasPartBytes(geometry.Key, thumbnail.Key, texture.Key)[..40];
        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [casPart.Key.FullTgi] = invalidCasPartBytes,
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

        Assert.NotNull(assetGraph.CasGraph);
        Assert.NotNull(assetGraph.CasGraph!.GeometryResource);
        Assert.Contains(assetGraph.Diagnostics, message => message.Contains("CASPart parsing failed:", StringComparison.Ordinal));
        Assert.Contains(assetGraph.Diagnostics, message => message.Contains("Using fallback CAS graph", StringComparison.Ordinal));
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
                null,
                [],
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

    private static byte[] CreateSyntheticCasPartBytes(ResourceKeyRecord geometryKey, ResourceKeyRecord thumbnailKey, ResourceKeyRecord textureKey, string internalName = "Short Hair")
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.BigEndianUnicode, leaveOpen: true);

        writer.Write(0x0000001Cu);
        var tgiOffsetPosition = stream.Position;
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(internalName);
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

    private static byte[] CreateSyntheticObjectDefinitionBytes(string internalName, ushort version, uint declaredSize, params uint[] remainingWords)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        var nameBytes = Encoding.ASCII.GetBytes(internalName);

        writer.Write(version);
        writer.Write(declaredSize);
        writer.Write((uint)nameBytes.Length);
        writer.Write(nameBytes);
        foreach (var word in remainingWords)
        {
            writer.Write(word);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] CreateSyntheticObjectCatalogBytes(params uint[] words)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        foreach (var word in words)
        {
            writer.Write(word);
        }

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

        public Task<byte[]> GetResourceBytesAsync(string packagePath, ResourceKeyRecord key, bool raw, CancellationToken cancellationToken, IProgress<ResourceReadProgress>? progress = null) =>
            Task.FromResult(bytesByTgi[key.FullTgi]);

        public Task<string?> GetTextAsync(string packagePath, ResourceKeyRecord key, CancellationToken cancellationToken) =>
            Task.FromResult(textByTgi.TryGetValue(key.FullTgi, out var value) ? value : null);

        public Task<byte[]?> GetTexturePngAsync(string packagePath, ResourceKeyRecord key, CancellationToken cancellationToken, IProgress<ResourceReadProgress>? progress = null) =>
            Task.FromResult<byte[]?>(bytesByTgi[key.FullTgi]);
    }

    private sealed class FakeSceneBuildService(SceneBuildResult result) : ISceneBuildService
    {
        public Task<SceneBuildResult> BuildSceneAsync(ResourceMetadata resource, CancellationToken cancellationToken, IProgress<PreviewBuildProgress>? progress = null) =>
            Task.FromResult(result);
    }

    private sealed class FakeGraphIndexStore : IIndexStore
    {
        private readonly IReadOnlyList<ResourceMetadata> resources;

        public FakeGraphIndexStore(IReadOnlyList<ResourceMetadata> resources)
        {
            this.resources = resources;
        }

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpsertDataSourcesAsync(IEnumerable<DataSourceDefinition> sources, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyDictionary<string, PackageFingerprint>> LoadPackageFingerprintsAsync(IEnumerable<Guid> dataSourceIds, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyDictionary<string, PackageFingerprint>>(new Dictionary<string, PackageFingerprint>());
        public Task<bool> NeedsRescanAsync(Guid dataSourceId, string packagePath, long fileSize, DateTimeOffset lastWriteTimeUtc, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task ReplacePackageAsync(PackageScanResult packageScan, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IIndexWriteSession> OpenWriteSessionAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ResourceMetadata> PersistResourceEnrichmentAsync(ResourceMetadata resource, CancellationToken cancellationToken) => Task.FromResult(resource);
        public Task<WindowedQueryResult<ResourceMetadata>> QueryResourcesAsync(RawResourceBrowserQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WindowedQueryResult<AssetSummary>> QueryAssetsAsync(AssetBrowserQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<DataSourceDefinition>> GetDataSourcesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DataSourceDefinition>>([]);
        public Task<AssetFacetOptions> GetAssetFacetOptionsAsync(AssetKind assetKind, CancellationToken cancellationToken) => Task.FromResult(new AssetFacetOptions([], [], [], [], []));
        public Task<IReadOnlyList<IndexedPackageRecord>> GetIndexedPackagesAsync(IEnumerable<Guid> dataSourceIds, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<IndexedPackageRecord>>([]);
        public Task<IReadOnlyList<ResourceMetadata>> GetPackageResourcesAsync(string packagePath, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ResourceMetadata>>([]);
        public Task<IReadOnlyList<ResourceMetadata>> GetResourcesByInstanceAsync(string packagePath, ulong fullInstance, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ResourceMetadata>>(resources.Where(resource => resource.PackagePath == packagePath && resource.Key.FullInstance == fullInstance).ToArray());
        public Task<IReadOnlyList<AssetSummary>> GetPackageAssetsAsync(string packagePath, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AssetSummary>>([]);
        public Task<ResourceMetadata?> GetResourceByTgiAsync(string packagePath, string fullTgi, CancellationToken cancellationToken) => Task.FromResult<ResourceMetadata?>(null);
        public Task<IReadOnlyList<ResourceMetadata>> GetResourcesByTgiAsync(string fullTgi, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ResourceMetadata>>(resources.Where(resource => string.Equals(resource.Key.FullTgi, fullTgi, StringComparison.OrdinalIgnoreCase)).ToArray());
        public Task UpdatePackageAssetsAsync(Guid dataSourceId, string packagePath, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken) => Task.CompletedTask;
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

    private delegate object ParseMatdDelegate(ReadOnlySpan<byte> bytes);

    private static byte[] CreateMinimalMatd(
        uint materialNameHash,
        uint shaderNameHash,
        uint propertyHash,
        uint propertyType,
        uint propertyArity,
        byte[] values)
        => CreateMinimalMatd(materialNameHash, shaderNameHash, (propertyHash, propertyType, propertyArity, values));

    private static byte[] CreateMinimalMatd(
        uint materialNameHash,
        uint shaderNameHash,
        params (uint PropertyHash, uint PropertyType, uint PropertyArity, byte[] Values)[] properties)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("MATD"u8.ToArray());
        writer.Write(0u);
        writer.Write(materialNameHash);
        writer.Write(shaderNameHash);
        writer.Write("MTRL"u8.ToArray());
        writer.Write(0u);
        writer.Write(0u);
        writer.Write((uint)properties.Length);

        var propertyOffset = 16 + 16 + (properties.Length * 16);
        foreach (var (propertyHash, propertyType, propertyArity, values) in properties)
        {
            writer.Write(propertyHash);
            writer.Write(propertyType);
            writer.Write(propertyArity);
            writer.Write((uint)propertyOffset);
            propertyOffset += values.Length;
        }

        foreach (var (_, _, _, values) in properties)
        {
            writer.Write(values);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] CreateEmbeddedResourceKeyBytes(uint type, uint group, ulong instance)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(instance).CopyTo(bytes, 0);
        BitConverter.GetBytes(type).CopyTo(bytes, 8);
        BitConverter.GetBytes(group).CopyTo(bytes, 12);
        return bytes;
    }

    private static byte[] CreatePackedTextureLikePayload(uint type, uint group, ulong instance)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(instance).CopyTo(bytes, 0);
        BitConverter.GetBytes(type).CopyTo(bytes, 8);
        BitConverter.GetBytes(group).CopyTo(bytes, 12);
        return bytes;
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
