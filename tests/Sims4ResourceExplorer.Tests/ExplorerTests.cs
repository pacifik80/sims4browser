using LlamaLogic.Packages;
using System.Buffers.Binary;
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

    [Theory]
    [InlineData("SimData", true, false, "Sim/character seed")]
    [InlineData("SimInfo", true, false, "Sim/character seed")]
    [InlineData("SimPreset", true, false, "Sim/character seed")]
    [InlineData("CASPreset", false, true, "Sim assembly component")]
    [InlineData("SimModifier", false, true, "Sim assembly component")]
    [InlineData("BlendGeometry", false, true, "Sim assembly component")]
    [InlineData("DeformerMap", false, true, "Sim assembly component")]
    [InlineData("BoneDelta", false, true, "Sim assembly component")]
    [InlineData("BonePose", false, true, "Sim assembly component")]
    [InlineData("Skintone", false, true, "Sim assembly component")]
    [InlineData("RegionMap", false, true, "Sim assembly component")]
    [InlineData("CASPart", false, false, "CAS seed")]
    public void ResourceTypeHints_ClassifiesSimAssemblyResources(string typeName, bool expectedSeed, bool expectedComponent, string expectedSummary)
    {
        var type = RequireType(typeof(LlamaResourceCatalogService).Assembly, "Sims4ResourceExplorer.Packages.ResourceTypeHints");
        var isSeedMethod = type.GetMethod("IsSimAssemblySeed", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        var isComponentMethod = type.GetMethod("IsSimAssemblyComponent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        var summaryMethod = type.GetMethod("GetAssetLinkageSummary", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(isSeedMethod);
        Assert.NotNull(isComponentMethod);
        Assert.NotNull(summaryMethod);

        var isSeed = Assert.IsType<bool>(isSeedMethod!.Invoke(null, [typeName]));
        var isComponent = Assert.IsType<bool>(isComponentMethod!.Invoke(null, [typeName]));
        var summary = Assert.IsType<string>(summaryMethod!.Invoke(null, [typeName]));

        Assert.Equal(expectedSeed, isSeed);
        Assert.Equal(expectedComponent, isComponent);
        Assert.Equal(expectedSummary, summary);
    }

    [Fact]
    public void StructuredResourceMetadataExtractor_DescribesCasPreset()
    {
        var description = ReadStructuredMetadataDescription("CASPreset", CreateSyntheticCasPresetBytes());

        Assert.NotNull(description);
        Assert.Contains("CAS preset v12", description, StringComparison.Ordinal);
        Assert.Contains("region=Neck", description, StringComparison.Ordinal);
        Assert.Contains("sculpts=1", description, StringComparison.Ordinal);
        Assert.Contains("modifiers=2", description, StringComparison.Ordinal);
        Assert.Contains("partSet=yes", description, StringComparison.Ordinal);
    }

    [Fact]
    public void StructuredResourceMetadataExtractor_DescribesRegionMap()
    {
        var description = ReadStructuredMetadataDescription("RegionMap", CreateSyntheticRegionMapBytes());

        Assert.NotNull(description);
        Assert.Contains("Region map v1", description, StringComparison.Ordinal);
        Assert.Contains("entries=2", description, StringComparison.Ordinal);
        Assert.Contains("replacementEntries=1", description, StringComparison.Ordinal);
        Assert.Contains("regions=Chest, Neck", description, StringComparison.Ordinal);
    }

    [Fact]
    public void StructuredResourceMetadataExtractor_DescribesSkintone()
    {
        var description = ReadStructuredMetadataDescription("Skintone", CreateSyntheticSkintoneBytes());

        Assert.NotNull(description);
        Assert.Contains("Skintone v6", description, StringComparison.Ordinal);
        Assert.Contains("overlays=2", description, StringComparison.Ordinal);
        Assert.Contains("swatches=3", description, StringComparison.Ordinal);
        Assert.Contains("swatchColors=#FFBBAA99, #FF112233, #FF445566", description, StringComparison.Ordinal);
    }

    [Fact]
    public void StructuredResourceMetadataExtractor_ParsesRegionMap()
    {
        var parsed = Ts4StructuredResourceMetadataExtractor.ParseRegionMap(CreateSyntheticRegionMapBytes());

        Assert.Equal(3u, parsed.ContextVersion);
        Assert.Equal(1u, parsed.Version);
        Assert.Equal(2, parsed.Entries.Count);
        Assert.Contains(parsed.Entries, entry => entry.RegionLabel == "Chest" && entry.IsReplacement);
        Assert.Contains(parsed.Entries, entry => entry.RegionLabel == "Neck" && entry.LinkedKeys.Count == 1);
    }

    [Fact]
    public void StructuredResourceMetadataExtractor_ParsesSkintone()
    {
        var parsed = Ts4StructuredResourceMetadataExtractor.ParseSkintone(CreateSyntheticSkintoneBytes());

        Assert.Equal(6u, parsed.Version);
        Assert.Equal(0x1020304050607080ul, parsed.BaseTextureInstance);
        Assert.Equal(2, parsed.OverlayTextures.Count);
        Assert.Equal(3, parsed.SwatchColors.Count);
        Assert.Equal(0xFFBBAA99u, parsed.SwatchColors[0]);
        Assert.Equal(0x00FF8040u, parsed.Colorize);
    }

    [Fact]
    public async Task ResourceMetadataEnrichmentService_ReEnrichesStructuredResourcesWhenDescriptionIsMissing()
    {
        var resource = CreateResource(Guid.NewGuid(), Path.Combine(tempRoot, "structured.package"), SourceKind.Game, "Skintone", 1, "StructuredSkintone") with
        {
            CompressedSize = 32,
            UncompressedSize = 64,
            IsCompressed = false,
            Description = null
        };

        var expectedDescription = "Skintone v6 | overlays=2";
        var catalog = new TrackingEnrichmentCatalogService(resource with { Description = expectedDescription });
        var service = new ResourceMetadataEnrichmentService(catalog);

        var enriched = await service.EnrichAsync(resource, CancellationToken.None);

        Assert.Equal(1, catalog.EnrichmentCalls);
        Assert.Equal(expectedDescription, enriched.Description);
    }

    [Fact]
    public void Ts4SeedMetadataExtractor_ExtractsSimInfoSeedMetadata()
    {
        var packagePath = Path.Combine(tempRoot, "siminfo.package");
        var resource = CreateResource(Guid.NewGuid(), packagePath, SourceKind.Game, "SimInfo", 1);
        var metadata = Ts4SeedMetadataExtractor.TryExtractSimInfoSeedMetadata(resource, CreateSyntheticSimInfoBytes());

        Assert.NotNull(metadata);
        Assert.StartsWith("SimInfo template:", metadata!.DisplayName, StringComparison.Ordinal);
        Assert.Contains("Human", metadata!.DisplayName, StringComparison.Ordinal);
        Assert.Contains("Female", metadata.DisplayName, StringComparison.Ordinal);
        Assert.Contains("outfits 2/3/6", metadata.DisplayName, StringComparison.Ordinal);
        Assert.Contains("traits 4", metadata.DisplayName, StringComparison.Ordinal);
        Assert.Contains("species=Human", metadata.Description, StringComparison.Ordinal);
        Assert.Contains("traits=4", metadata.Description, StringComparison.Ordinal);
        Assert.Contains("outfits=2 categories / 3 entries / 6 parts", metadata.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Ts4SeedMetadataExtractor_DoesNotTreatSingleNonNudeOutfitAsAuthoritativeBodyDriving()
    {
        var packagePath = Path.Combine(tempRoot, "single-non-nude.package");
        var resource = CreateResource(Guid.NewGuid(), packagePath, SourceKind.Game, "SimInfo", 1);
        var metadata = Ts4SeedMetadataExtractor.TryExtractSimTemplateSeedMetadata(resource, CreateSyntheticSimInfoBytesWithSingleNonNudeOutfit());

        Assert.NotNull(metadata);
        Assert.Equal(0, metadata!.Fact.AuthoritativeBodyDrivingOutfitCount);
        Assert.Equal(0, metadata.Fact.AuthoritativeBodyDrivingOutfitPartCount);
        Assert.Empty(metadata.BodyPartFacts);
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildsSimMetadataGraphFromLegacyV27OutfitLayout()
    {
        var packagePath = Path.Combine(tempRoot, "sim-graph-v27.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 1/1/6 | traits 4 [00000001]",
            Description = "SimInfo v27 | species=Human | age=Young Adult | gender=Female | outfits=1 categories / 1 entries / 6 parts | traits=4"
        };
        var bytes = CreateSyntheticSimInfoV27Bytes();
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]> { [simInfo.Key.FullTgi] = bytes },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.Equal(1, graph.SimGraph!.Metadata.OutfitCategoryCount);
        Assert.Equal(1, graph.SimGraph.Metadata.OutfitEntryCount);
        Assert.Equal(6, graph.SimGraph.Metadata.OutfitPartCount);
        Assert.Contains(graph.SimGraph.BodySources, group => group.Label == "Body-part instances" && group.Count == 6 && group.Notes.Contains("6000000000000000", StringComparison.Ordinal));
    }

    [Fact]
    public void SimTemplateSelectionPolicy_PrefersTemplateWithAuthoritativeBodyPartsOverRepresentative()
    {
        var representative = new SimTemplateOptionSummary(
            Guid.NewGuid(),
            "SimInfo template: Human | Young Adult | Female [00000001]",
            @"C:\game\ClientDeltaBuild0.package",
            "ClientDeltaBuild0.package",
            "025ED6F4:00000000:0000000000000001",
            true,
            "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=0 categories / 0 entries / 0 parts",
            0,
            0,
            0,
            13,
            74,
            6,
            true);
        var bodyDriving = new SimTemplateOptionSummary(
            Guid.NewGuid(),
            "SimInfo template: Human | Young Adult | Female [00000002]",
            @"C:\game\ClientDeltaBuild0.package",
            "ClientDeltaBuild0.package",
            "025ED6F4:00000000:0000000000000002",
            false,
            "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=2 categories / 3 entries / 6 parts",
            2,
            3,
            6,
            4,
            12,
            1,
            true);

        var selected = SimTemplateSelectionPolicy.SelectPreferredTemplate([representative, bodyDriving]);

        Assert.NotNull(selected);
        Assert.Equal(bodyDriving.RootTgi, selected!.RootTgi);
    }

    [Fact]
    public void SimTemplateSelectionPolicy_PrefersLeanTemplateWhenNoExplicitBodyDrivingRecipeExists()
    {
        var richerStyled = new SimTemplateOptionSummary(
            Guid.NewGuid(),
            "SimInfo template: Human | Child | Male | outfits 12/591/256 [00000010]",
            @"C:\game\ClientFullBuild0.package",
            "ClientFullBuild0.package",
            "025ED6F4:00000000:0000000000000010",
            false,
            "SimInfo v21 | species=Human | age=Child | gender=Male | outfits=12 categories / 591 entries / 256 parts",
            12,
            591,
            256,
            1,
            63,
            35,
            true,
            0,
            0);
        var leanerTemplate = new SimTemplateOptionSummary(
            Guid.NewGuid(),
            "SimInfo template: Human | Child | Male | outfits 8/8/82 [00000011]",
            @"C:\game\SimulationFullBuild0.package",
            "SimulationFullBuild0.package",
            "025ED6F4:00000000:0000000000000011",
            false,
            "SimInfo v21 | species=Human | age=Child | gender=Male | outfits=8 categories / 8 entries / 82 parts",
            8,
            8,
            82,
            1,
            63,
            35,
            true,
            0,
            0);

        var selected = SimTemplateSelectionPolicy.SelectPreferredTemplate([richerStyled, leanerTemplate]);

        Assert.NotNull(selected);
        Assert.Equal(leanerTemplate.RootTgi, selected!.RootTgi);
    }

    [Fact]
    public void AssetGraphBuilder_CreatesSimSummariesFromSeedEnrichedSimInfo()
    {
        var builder = new ExplicitAssetGraphBuilder(new FakeResourceCatalogService(new Dictionary<string, byte[]>(), new Dictionary<string, string?>()));
        var packagePath = Path.Combine(tempRoot, "sim-summary.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 2/3/6 | traits 4 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=2 categories / 3 entries / 6 parts | traits=4"
        };

        var summaries = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []));

        var summary = Assert.Single(summaries.Where(asset => asset.AssetKind == AssetKind.Sim));
        Assert.Equal("Sim archetype: Human | Young Adult | Female", summary.DisplayName);
        Assert.Equal("Human", summary.Category);
        Assert.Equal("Metadata", summary.SupportLabel);
        Assert.Contains("Sim archetype", summary.SupportNotes, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("SimArchetype", summary.RootTypeName);
        Assert.Equal("SimInfo", summary.IdentityType);
        Assert.Equal(1, summary.VariantCount);
    }

    [Fact]
    public void AssetGraphBuilder_GroupsSimSummariesIntoArchetypes()
    {
        var builder = new ExplicitAssetGraphBuilder(new FakeResourceCatalogService(new Dictionary<string, byte[]>(), new Dictionary<string, string?>()));
        var packagePath = Path.Combine(tempRoot, "sim-archetype-summary.package");
        var sourceId = Guid.NewGuid();
        var first = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Adult | Male | outfits 2/3/6 | traits 4 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Adult | gender=Male | outfits=2 categories / 3 entries / 6 parts | traits=4"
        };
        var second = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 2) with
        {
            Name = "SimInfo template: Human | Adult | Male | outfits 1/2/4 | traits 2 [00000002]",
            Description = "SimInfo v32 | species=Human | age=Adult | gender=Male | outfits=1 categories / 2 entries / 4 parts | traits=2"
        };
        var third = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 3) with
        {
            Name = "Character template: Cat | Adult | Unisex | outfits 1/1/3 | traits 0 [00000003]",
            Description = "SimInfo v32 | species=Cat | age=Adult | gender=Unisex | outfits=1 categories / 1 entries / 3 parts | traits=0"
        };

        var summaries = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [first, second, third], []))
            .Where(asset => asset.AssetKind == AssetKind.Sim)
            .OrderBy(asset => asset.DisplayName, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(2, summaries.Length);
        var human = Assert.Single(summaries.Where(asset => asset.DisplayName == "Sim archetype: Human | Adult | Male"));
        Assert.Equal(2, human.VariantCount);
        Assert.Equal("sim-archetype:human|adult|male", human.LogicalRootTgi);
        var cat = Assert.Single(summaries.Where(asset => asset.DisplayName == "Character archetype: Cat | Adult | Unisex"));
        Assert.Equal(1, cat.VariantCount);
    }

    [Fact]
    public void AssetGraphBuilder_IgnoresLegacyOpaqueSpeciesSimSummaryRows()
    {
        var builder = new ExplicitAssetGraphBuilder(new FakeResourceCatalogService(new Dictionary<string, byte[]>(), new Dictionary<string, string?>()));
        var packagePath = Path.Combine(tempRoot, "gp01-legacy-sim-summary.package");
        var sourceId = Guid.NewGuid();
        var suspiciousLegacy = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "Character template: Species 0x0000AFC5 | Elder | Female | outfits 275/0/0 [00000001]",
            Description = "SimInfo v20 | species=Species 0x0000AFC5 | age=Elder | gender=Female | outfits=275 categories / 0 entries / 0 parts | traits=0 | sculpts=204 | faceMods=245 | bodyMods=189 | geneticSculpts=0 | geneticFaceMods=0 | geneticBodyMods=0 | geneticParts=0 | growthParts=0 | peltLayers=6 | skintone=4605040302010006"
        };
        var trustworthy = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 2) with
        {
            Name = "SimInfo template: Human | Elder | Female [00000002]",
            Description = "SimInfo v38 | species=Human | age=Elder | gender=Female | outfits=0 categories / 0 entries / 0 parts | traits=0 | sculpts=6 | faceMods=70 | bodyMods=9 | geneticSculpts=0 | geneticFaceMods=0 | geneticBodyMods=0 | geneticParts=0 | growthParts=0 | peltLayers=0 | pronouns=5 | skintone=000000000000AFC5 | skintoneShift=0"
        };

        var summaries = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [suspiciousLegacy, trustworthy], []))
            .Where(asset => asset.AssetKind == AssetKind.Sim)
            .ToArray();

        var summary = Assert.Single(summaries);
        Assert.Equal("Sim archetype: Human | Elder | Female", summary.DisplayName);
        Assert.DoesNotContain("Species 0x", summary.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AssetGraphBuilder_KeepsLegacyHumanSimSummaryRows()
    {
        var builder = new ExplicitAssetGraphBuilder(new FakeResourceCatalogService(new Dictionary<string, byte[]>(), new Dictionary<string, string?>()));
        var packagePath = Path.Combine(tempRoot, "legacy-human-sim-summary.package");
        var sourceId = Guid.NewGuid();
        var legacyHuman = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Child | Male | outfits 12/591/256 [00000001]",
            Description = "SimInfo v21 | species=Human | age=Child | gender=Male | outfits=12 categories / 591 entries / 256 parts | traits=0"
        };

        var summaries = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [legacyHuman], []))
            .Where(asset => asset.AssetKind == AssetKind.Sim)
            .ToArray();

        var summary = Assert.Single(summaries);
        Assert.Equal("Sim archetype: Human | Child | Male", summary.DisplayName);
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildsSimMetadataGraphFromSimInfoResource()
    {
        var packagePath = Path.Combine(tempRoot, "sim-graph.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 2/3/6 | traits 4 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=2 categories / 3 entries / 6 parts | traits=4"
        };
        var bytes = CreateSyntheticSimInfoBytes();
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]> { [simInfo.Key.FullTgi] = bytes },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.Equal("Human", graph.SimGraph!.Metadata.SpeciesLabel);
        Assert.Equal("Young Adult", graph.SimGraph.Metadata.AgeLabel);
        Assert.Equal("Female", graph.SimGraph.Metadata.GenderLabel);
        Assert.Equal(2, graph.SimGraph.Metadata.OutfitCategoryCount);
        Assert.Equal(3, graph.SimGraph.Metadata.OutfitEntryCount);
        Assert.Equal(6, graph.SimGraph.Metadata.OutfitPartCount);
        Assert.Equal(4, graph.SimGraph.Metadata.TraitCount);
        Assert.Equal(1, graph.SimGraph.Metadata.PronounCount);
        Assert.Contains(graph.SimGraph.BodyFoundation, group => group.Label == "Base frame" && group.Count == 1);
        Assert.Contains(graph.SimGraph.BodyFoundation, group => group.Label == "Skin pipeline" && group.Count == 1);
        Assert.Contains(graph.SimGraph.BodyFoundation, group => group.Label == "Body morph stack" && group.Count == 6);
        Assert.Contains(graph.SimGraph.BodySources, group => group.Label == "Skintone reference" && group.Count == 1 && group.Notes.Contains("1020304050607080", StringComparison.Ordinal));
        Assert.Contains(graph.SimGraph.BodySources, group => group.Label == "Body-part instances" && group.Count == 6 && group.Notes.Contains("6000000000000000", StringComparison.Ordinal));
        Assert.Contains(graph.SimGraph.BodySources, group => group.Label == "Genetic body-type tokens" && group.Count == 3 && group.Notes.Contains("bodyType 9", StringComparison.Ordinal));
        Assert.Contains(graph.SimGraph.BodySources, group => group.Label == "Direct body channels" && group.Count == 1 && group.Notes.Contains("ch 0=", StringComparison.Ordinal));
        Assert.Contains(graph.SimGraph.SlotGroups, group => group.Label == "Outfit / body part selections" && group.Count == 6);
        Assert.Contains(graph.SimGraph.SlotGroups, group => group.Label == "Skintone" && group.Count == 1);
        Assert.Contains(graph.SimGraph.MorphGroups, group => group.Label == "Face modifiers" && group.Count == 2);
        Assert.Contains(graph.SimGraph.MorphGroups, group => group.Label == "Body modifiers" && group.Count == 1);
        Assert.Contains(graph.SimGraph.MorphGroups, group => group.Label == "Genetic body modifiers" && group.Count == 2);
        Assert.True(graph.SimGraph.IsSupported);
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildsSimMetadataGraph_WithResolvedSkintoneRenderInput()
    {
        var packagePath = Path.Combine(tempRoot, "sim-graph-skintone.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 2/3/6 | traits 4 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=2 categories / 3 entries / 6 parts | traits=4"
        };
        var skintone = CreateResource(sourceId, Path.Combine(tempRoot, "sim-skintone.package"), SourceKind.Game, "Skintone", 0, "ToneResource") with
        {
            Key = new ResourceKeyRecord((uint)Enum.Parse<ResourceType>("Skintone"), 0, 0x1020304050607080ul, "Skintone")
        };
        var baseTexture = CreateResource(sourceId, Path.Combine(tempRoot, "sim-skintone.package"), SourceKind.Game, "PNGImage", 0, "ToneTexture") with
        {
            Key = new ResourceKeyRecord((uint)Enum.Parse<ResourceType>("PNGImage"), 0, 0x1020304050607080ul, "PNGImage")
        };
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = CreateSyntheticSimInfoBytes(),
                    [skintone.Key.FullTgi] = CreateSyntheticSkintoneBytes(),
                    [baseTexture.Key.FullTgi] = TestAssets.OnePixelPng
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([skintone, baseTexture]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.NotNull(graph.SimGraph!.SkintoneRender);
        Assert.Equal(skintone.Key.FullTgi, graph.SimGraph.SkintoneRender!.SkintoneResourceTgi);
        Assert.Equal(skintone.PackagePath, graph.SimGraph.SkintoneRender.SkintonePackagePath);
        Assert.Equal(baseTexture.Key.FullTgi, graph.SimGraph.SkintoneRender.BaseTextureResourceTgi);
        Assert.Equal(baseTexture.PackagePath, graph.SimGraph.SkintoneRender.BaseTexturePackagePath);
        Assert.Equal(2, graph.SimGraph.SkintoneRender.OverlayTextureCount);
        Assert.Equal(3, graph.SimGraph.SkintoneRender.SwatchColorCount);
        Assert.NotNull(graph.SimGraph.SkintoneRender.ViewportTintColor);
        Assert.Equal(0xBB / 255f, graph.SimGraph.SkintoneRender.ViewportTintColor!.R, 3);
        Assert.Equal(0xAA / 255f, graph.SimGraph.SkintoneRender.ViewportTintColor.G, 3);
        Assert.Equal(0x99 / 255f, graph.SimGraph.SkintoneRender.ViewportTintColor.B, 3);
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildsSimMetadataGraph_WithCasSlotCandidateFamilies()
    {
        var packagePath = Path.Combine(tempRoot, "sim-slot-candidates.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 2/3/6 | traits 4 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=2 categories / 3 entries / 6 parts | traits=4"
        };
        var bytes = CreateSyntheticSimInfoBytes();
        var casAssets = new[]
        {
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfHair_Wavy", "Hair", "hair.package", new ResourceKeyRecord(0x034AEECB, 0, 10, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "hair", Description: "slot=Hair | species=Human | age=Young Adult | gender=Female"),
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfTop_Crop", "Top", "top.package", new ResourceKeyRecord(0x034AEECB, 0, 11, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "top", Description: "slot=Top | species=Human | age=Young Adult | gender=Female"),
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "ymShoes_Boot", "Shoes", "shoes.package", new ResourceKeyRecord(0x034AEECB, 0, 12, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "shoes", Description: "slot=Shoes | species=Human | age=Young Adult | gender=Male")
        };
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]> { [simInfo.Key.FullTgi] = bytes },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], casAssets));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        var hair = Assert.Single(graph.SimGraph!.CasSlotCandidates.Where(candidate => candidate.Label == "Hair" && candidate.Count == 1));
        var top = Assert.Single(graph.SimGraph.CasSlotCandidates.Where(candidate => candidate.Label == "Top" && candidate.Count == 1));
        Assert.Equal(SimCasSlotCandidateSourceKind.CompatibilityFallback, hair.SourceKind);
        Assert.Equal(SimCasSlotCandidateSourceKind.CompatibilityFallback, top.SourceKind);
        Assert.Contains(hair.Candidates, candidate => candidate.DisplayName == "yfHair_Wavy");
        Assert.Contains(top.Candidates, candidate => candidate.DisplayName == "yfTop_Crop");
        Assert.DoesNotContain(graph.SimGraph.CasSlotCandidates, candidate => candidate.Label == "Shoes");
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildsSimMetadataGraph_WithAuthoritativeHeadCasSelections()
    {
        var packagePath = Path.Combine(tempRoot, "sim-head-candidates.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 1/1/2 | traits 0 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=1 categories / 1 entries / 2 parts | traits=0"
        };
        var bytes = CreateSyntheticSimInfoBytesWithHeadPartLinks();
        var hair = new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfHair_Wavy", "Hair", "hair.package", new ResourceKeyRecord(0x034AEECB, 0, 0x6000000000000010ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "hair", Description: "slot=Hair | species=Human | age=Young Adult | gender=Female");
        var accessory = new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfAccessory_Glasses", "Accessory", "accessory.package", new ResourceKeyRecord(0x034AEECB, 0, 0x6000000000000011ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "accessory", Description: "slot=Accessory | species=Human | age=Young Adult | gender=Female");
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = bytes,
                    [hair.RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfHair_Wavy", bodyType: 2),
                    [accessory.RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfAccessory_Glasses", bodyType: 12)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], [hair, accessory]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.Contains(graph.SimGraph!.BodySources, group => group.Label == "Body-part instances" && group.Notes.Contains("6000000000000010", StringComparison.Ordinal));
        Assert.Contains(graph.SimGraph.BodySources, group => group.Label == "Body-part instances" && group.Notes.Contains("6000000000000011", StringComparison.Ordinal));
        var hairCandidate = Assert.Single(graph.SimGraph.CasSlotCandidates.Where(candidate => candidate.Label == "Hair"));
        var accessoryCandidate = Assert.Single(graph.SimGraph.CasSlotCandidates.Where(candidate => candidate.Label == "Accessory"));
        Assert.Equal(SimCasSlotCandidateSourceKind.ExactPartLink, hairCandidate.SourceKind);
        Assert.Equal(SimCasSlotCandidateSourceKind.ExactPartLink, accessoryCandidate.SourceKind);
        Assert.Contains(hairCandidate.Candidates, candidate => candidate.DisplayName == "yfHair_Wavy");
        Assert.Contains(accessoryCandidate.Candidates, candidate => candidate.DisplayName == "yfAccessory_Glasses");
        Assert.Contains(graph.SimGraph.BodyAssembly.GraphNodes, node => node.Label == "Head CAS selections" && node.State == SimBodyGraphNodeState.Resolved);
        Assert.Contains(graph.SimGraph.BodyAssembly.GraphNodes, node => node.Label == "Head shell" && node.State == SimBodyGraphNodeState.Approximate);
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildsSimMetadataGraph_WithAuthoritativeHeadShellCandidate()
    {
        var packagePath = Path.Combine(tempRoot, "sim-head-shell.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 1/1/2 | traits 0 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=1 categories / 1 entries / 2 parts | traits=0"
        };
        var bytes = CreateSyntheticSimInfoBytesWithBodyAndHeadPartLinks();
        var fullBody = new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfBody_Base", "Full Body", "body.package", new ResourceKeyRecord(0x034AEECB, 0, 0x6000000000000020ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "full-body", Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female");
        var head = new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfHead_Base", "Head", "head.package", new ResourceKeyRecord(0x034AEECB, 0, 0x6000000000000021ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "head", Description: "slot=Head | species=Human | age=Young Adult | gender=Female");
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = bytes,
                    [fullBody.RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBody_Base", bodyType: 5, partFlags2: 0x04),
                    [head.RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfHead_Base", bodyType: 3)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], [fullBody, head]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.Contains(graph.SimGraph!.BodyCandidates, candidate => candidate.Label == "Full Body" && candidate.Count == 1 && candidate.SourceKind == SimBodyCandidateSourceKind.ExactPartLink);
        Assert.Contains(graph.SimGraph.BodyCandidates, candidate => candidate.Label == "Head" && candidate.Count == 1 && candidate.SourceKind == SimBodyCandidateSourceKind.ExactPartLink);
        Assert.Contains(graph.SimGraph.BodyAssembly.Layers, layer => layer.Label == "Head" && layer.CandidateCount == 1);
        Assert.Contains(graph.SimGraph.BodyAssembly.GraphNodes, node => node.Label == "Head shell" && node.State == SimBodyGraphNodeState.Resolved);
    }

    [Fact]
    public async Task AssetGraphBuilder_PrefersNudeOutfitParts_ForAuthoritativeBodyAssembly()
    {
        var packagePath = Path.Combine(tempRoot, "sim-body-nude-outfit.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 2/2/2 | traits 0 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=2 categories / 2 entries / 2 parts | traits=0"
        };
        var bytes = CreateSyntheticSimInfoBytesWithEverydayAndNudeBodyPartLinks();
        var everydayBody = new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfBody_EverydayDress", "Full Body", "ClientFullBuild0.package", new ResourceKeyRecord(0x034AEECB, 0, 0x6000000000000030ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "full-body", Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female");
        var nudeBody = new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfBody_Nude", "Full Body", "ClientFullBuild0.package", new ResourceKeyRecord(0x034AEECB, 0, 0x6000000000000031ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "full-body", Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female");
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = bytes,
                    [everydayBody.RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBody_EverydayDress", bodyType: 5),
                    [nudeBody.RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBody_Nude", bodyType: 5, partFlags2: 0x04)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], [everydayBody, nudeBody]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        var fullBody = Assert.Single(graph.SimGraph!.BodyCandidates.Where(candidate => candidate.Label == "Full Body"));
        Assert.Equal(SimBodyCandidateSourceKind.ExactPartLink, fullBody.SourceKind);
        Assert.Single(fullBody.Candidates);
        Assert.Equal("yfBody_Nude", fullBody.Candidates[0].DisplayName);
        Assert.DoesNotContain(fullBody.Candidates, candidate => candidate.DisplayName == "yfBody_EverydayDress");
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildPreviewGraphAsync_DefersBroadCasCandidateDiscovery()
    {
        var packagePath = Path.Combine(tempRoot, "sim-preview-fast-graph.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 1/1/1 | traits 0 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=1 categories / 1 entries / 1 parts | traits=0"
        };
        var bytes = CreateSyntheticSimInfoBytesWithBodyAndHeadPartLinks();
        var exactBody = new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfBody_Nude", "Full Body", "ClientFullBuild0.package", new ResourceKeyRecord(0x034AEECB, 0, 0x6000000000000020ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "full-body", Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female");
        var broadHair = new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfHair_Long", "Hair", "ClientFullBuild0.package", new ResourceKeyRecord(0x034AEECB, 0, 0x6000000000000900ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "hair", Description: "slot=Hair | species=Human | age=Young Adult | gender=Female");
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = bytes,
                    [exactBody.RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBody_Nude", bodyType: 5, partFlags2: 0x04)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], [exactBody, broadHair]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildPreviewGraphAsync(summary, [simInfo], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.Contains(graph.SimGraph!.BodyCandidates, candidate => candidate.Label == "Full Body" && candidate.Count == 1);
        Assert.Empty(graph.SimGraph.CasSlotCandidates);
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildPreviewGraphAsync_UsesOnlyAuthoritativeSplitBodyParts()
    {
        var packagePath = Path.Combine(tempRoot, "sim-preview-authoritative-split-body.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 1/1/3 | traits 0 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=1 categories / 1 entries / 3 parts | traits=0"
        };
        var bytes = CreateSyntheticSimInfoBytesWithAuthoritativeSplitNudeOutfits();
        var top = new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfTop_Nude", "Top", "body.package", new ResourceKeyRecord(0x034AEECB, 0, 0x6100000000000000ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "top", Description: "slot=Top | species=Human | age=Young Adult | gender=Female");
        var bottom = new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfBottom_Nude", "Bottom", "body.package", new ResourceKeyRecord(0x034AEECB, 0, 0x6100000000000001ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "bottom", Description: "slot=Bottom | species=Human | age=Young Adult | gender=Female");
        var shoes = new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfShoes_Nude", "Shoes", "body.package", new ResourceKeyRecord(0x034AEECB, 0, 0x6100000000000002ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "shoes", Description: "slot=Shoes | species=Human | age=Young Adult | gender=Female");
        var strayCanonicalBody = new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "acBody_Nude", "Full Body", "ClientFullBuild0.package", new ResourceKeyRecord(0x034AEECB, 0, 0x6100000000000003ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "full-body", Description: "slot=Full Body | species=Cat | age=Adult | gender=Female");
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = bytes,
                    [top.RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfTop_Nude", bodyType: 6, partFlags2: 0x04),
                    [bottom.RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBottom_Nude", bodyType: 7, partFlags2: 0x04),
                    [shoes.RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfShoes_Nude", bodyType: 8),
                    [strayCanonicalBody.RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "acBody_Nude", bodyType: 5, partFlags2: 0x04, speciesValue: 1u)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], [top, bottom, shoes, strayCanonicalBody]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildPreviewGraphAsync(summary, [simInfo], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.DoesNotContain(graph.SimGraph!.BodyCandidates, candidate => candidate.Label == "Full Body" && candidate.Count > 0);
        Assert.Contains(graph.SimGraph.BodyCandidates, candidate => candidate.Label == "Top" && candidate.Count == 1 && candidate.SourceKind == SimBodyCandidateSourceKind.ExactPartLink);
        Assert.Contains(graph.SimGraph.BodyCandidates, candidate => candidate.Label == "Bottom" && candidate.Count == 1 && candidate.SourceKind == SimBodyCandidateSourceKind.ExactPartLink);
        Assert.Contains(graph.SimGraph.BodyCandidates, candidate => candidate.Label == "Shoes" && candidate.Count == 1 && candidate.SourceKind == SimBodyCandidateSourceKind.ExactPartLink);
        Assert.Equal(SimBodyAssemblyMode.SplitBodyLayers, graph.SimGraph.BodyAssembly.Mode);
        Assert.DoesNotContain(graph.SimGraph.BodyAssembly.Layers, layer => layer.Label == "Full Body" && layer.State == SimBodyAssemblyLayerState.Active);
        Assert.Contains(graph.SimGraph.BodyAssembly.Layers, layer => layer.Label == "Top" && layer.State == SimBodyAssemblyLayerState.Active);
        Assert.Contains(graph.SimGraph.BodyAssembly.Layers, layer => layer.Label == "Bottom" && layer.State == SimBodyAssemblyLayerState.Active);
        Assert.Contains(graph.SimGraph.BodyAssembly.Layers, layer => layer.Label == "Shoes" && layer.State == SimBodyAssemblyLayerState.Active);
        Assert.Contains(
            graph.SimGraph.BodyAssembly.GraphNodes,
            node => node.Label == "Geometry shell" &&
                    node.Notes.Contains("Split body shell composed from 2 active layer(s), including authoritative SimInfo selections.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AssetGraphBuilder_DoesNotUseFlattenedOutfitUnion_WhenNoAuthoritativeBodyDrivingOutfitExists()
    {
        var packagePath = Path.Combine(tempRoot, "sim-body-no-authoritative-outfit.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 2/3/6 | traits 4 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=2 categories / 3 entries / 6 parts | traits=4"
        };
        var bytes = CreateSyntheticSimInfoBytes();
        var candidate = new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "acBody_Nude", "Full Body", "ClientFullBuild0.package", new ResourceKeyRecord(0x034AEECB, 0, 0x6120000000000000ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "full-body", Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female");
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = bytes,
                    [candidate.RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "acBody_Nude", bodyType: 5, partFlags2: 0x04, speciesValue: 1u)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], [candidate]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        Assert.DoesNotContain(graph.SimGraph!.BodyCandidates, candidate => candidate.Label == "Full Body");
        Assert.Contains(graph.SimGraph.BodySources, source => source.Label == "Body-driving outfit records" && source.Count == 0);
        Assert.Contains(graph.SimGraph.BodyAssembly.GraphNodes, node => node.Label == "Geometry shell" && node.State == SimBodyGraphNodeState.Unavailable);
    }

    [Fact]
    public async Task AssetGraphBuilder_UsesIndexedTemplateFacts_WhenTemplateSearchFallbackIsUnavailable()
    {
        var sourceId = Guid.NewGuid();
        var representativePackage = Path.Combine(tempRoot, "sim-template-representative.package");
        var preferredPackage = Path.Combine(tempRoot, "sim-template-preferred.package");
        var representative = CreateResource(sourceId, representativePackage, SourceKind.Game, "SimInfo", 1) with
        {
            Key = new ResourceKeyRecord((uint)Enum.Parse<ResourceType>("SimInfo"), 0, 0xFDCCF77200000001ul, "SimInfo"),
            Name = "SimInfo template: Human | Young Adult | Female [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=0 categories / 0 entries / 0 parts | traits=0"
        };
        var preferred = CreateResource(sourceId, preferredPackage, SourceKind.Game, "SimInfo", 1) with
        {
            Key = new ResourceKeyRecord((uint)Enum.Parse<ResourceType>("SimInfo"), 0, 0xFDCCF77200000002ul, "SimInfo"),
            Name = "SimInfo template: Human | Young Adult | Female | outfits 2/3/6 [00000002]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=2 categories / 3 entries / 6 parts | traits=0"
        };
        SimTemplateFactSummary[] indexedFacts =
        [
            new SimTemplateFactSummary(
                representative.Id,
                sourceId,
                SourceKind.Game,
                representativePackage,
                representative.Key.FullTgi,
                "sim-archetype:human|young-adult|female",
                "Human",
                "Young Adult",
                "Female",
                representative.Name!,
                representative.Description!,
                0,
                0,
                0,
                0,
                0,
                0,
                false,
                0,
                0),
            new SimTemplateFactSummary(
                preferred.Id,
                sourceId,
                SourceKind.Game,
                preferredPackage,
                preferred.Key.FullTgi,
                "sim-archetype:human|young-adult|female",
                "Human",
                "Young Adult",
                "Female",
                preferred.Name!,
                preferred.Description!,
                2,
                3,
                6,
                4,
                12,
                1,
                true,
                1,
                6)
        ];
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [representative.Key.FullTgi] = CreateSyntheticSimInfoBytes(),
                    [preferred.Key.FullTgi] = CreateSyntheticSimInfoBytes()
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore(
                [representative, preferred],
                queryResources: [],
                simTemplateFacts: indexedFacts));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, representativePackage, 0, DateTimeOffset.UtcNow, [representative], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [representative], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.Equal(preferred.Id, graph.SimGraph!.SimInfoResource.Id);
        Assert.Equal(preferred.PackagePath, graph.SimGraph.SimInfoResource.PackagePath);
        Assert.Contains(graph.Diagnostics, diagnostic => diagnostic.Contains("Automatically selected SimInfo template for body-shell inspection", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AssetGraphBuilder_LabelsUnresolvedExplicitTemplateAsBodyShellInspection()
    {
        var sourceId = Guid.NewGuid();
        var representativePackage = Path.Combine(tempRoot, "sim-template-inspection-representative.package");
        var preferredPackage = Path.Combine(tempRoot, "sim-template-inspection-preferred.package");
        var representative = CreateResource(sourceId, representativePackage, SourceKind.Game, "SimInfo", 1) with
        {
            Key = new ResourceKeyRecord((uint)Enum.Parse<ResourceType>("SimInfo"), 0, 0xFDCCF77200000011ul, "SimInfo"),
            Name = "SimInfo template: Human | Young Adult | Female [00000011]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=0 categories / 0 entries / 0 parts | traits=0"
        };
        var preferred = CreateResource(sourceId, preferredPackage, SourceKind.Game, "SimInfo", 1) with
        {
            Key = new ResourceKeyRecord((uint)Enum.Parse<ResourceType>("SimInfo"), 0, 0xFDCCF77200000012ul, "SimInfo"),
            Name = "SimInfo template: Human | Young Adult | Female | outfits 1/1/2 | traits 0 [00000012]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=1 categories / 1 entries / 2 parts | traits=0"
        };
        SimTemplateFactSummary[] indexedFacts =
        [
            new SimTemplateFactSummary(
                representative.Id,
                sourceId,
                SourceKind.Game,
                representativePackage,
                representative.Key.FullTgi,
                "sim-archetype:human|young-adult|female",
                "Human",
                "Young Adult",
                "Female",
                representative.Name!,
                representative.Description!,
                0,
                0,
                0,
                0,
                0,
                0,
                false,
                0,
                0),
            new SimTemplateFactSummary(
                preferred.Id,
                sourceId,
                SourceKind.Game,
                preferredPackage,
                preferred.Key.FullTgi,
                "sim-archetype:human|young-adult|female",
                "Human",
                "Young Adult",
                "Female",
                preferred.Name!,
                preferred.Description!,
                1,
                1,
                2,
                0,
                0,
                0,
                false,
                1,
                2)
        ];
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [representative.Key.FullTgi] = CreateSyntheticSimInfoHeaderOnlyBytes(0x00000010u, 0x00002000u, 1u),
                    [preferred.Key.FullTgi] = CreateSyntheticSimInfoBytesWithHeadPartLinks()
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore(
                [representative, preferred],
                queryResources: [],
                simTemplateFacts: indexedFacts));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, representativePackage, 0, DateTimeOffset.UtcNow, [representative], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [representative], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.Equal(preferred.Id, graph.SimGraph!.SimInfoResource.Id);
        Assert.Equal(SimBodyAssemblyMode.None, graph.SimGraph.BodyAssembly.Mode);
        Assert.Contains(graph.Diagnostics, diagnostic => diagnostic.Contains("Automatically selected SimInfo template for body-shell inspection", StringComparison.Ordinal));
        Assert.DoesNotContain(graph.Diagnostics, diagnostic => diagnostic.Contains("Automatically selected body-driving SimInfo template", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AssetGraphBuilder_UsesIndexedBodyPartFacts_WhenRawResourceLookupFallbackIsUnavailable()
    {
        var simPackagePath = Path.Combine(tempRoot, "sim-indexed-body-parts.package");
        var bodyPackagePath = Path.Combine(tempRoot, "sim-indexed-body.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, simPackagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 1/1/2 | traits 0 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=1 categories / 1 entries / 2 parts | traits=0"
        };
        var fullBodyResource = CreateResource(sourceId, bodyPackagePath, SourceKind.Game, "CASPart", 0, "yfBody_Base") with
        {
            Key = new ResourceKeyRecord((uint)Enum.Parse<ResourceType>("CASPart"), 0, 0x6000000000000020ul, "CASPart"),
            Description = "slot=Full Body | species=Human | age=Young Adult | gender=Female"
        };
        var geometryResource = CreateResource(sourceId, bodyPackagePath, SourceKind.Game, "Geometry", 0) with
        {
            Key = new ResourceKeyRecord((uint)Enum.Parse<ResourceType>("Geometry"), 0, 0x6000000000000020ul, "Geometry")
        };
        SimTemplateBodyPartFact[] indexedBodyPartFacts =
        [
            new SimTemplateBodyPartFact(
                simInfo.Id,
                sourceId,
                SourceKind.Game,
                simPackagePath,
                simInfo.Key.FullTgi,
                5,
                "Nude",
                0,
                0,
                5,
                "Full Body",
                0x6000000000000020ul,
                true)
        ];
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = CreateSyntheticSimInfoBytesWithBodyAndHeadPartLinks(),
                    [fullBodyResource.Key.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBody_Base", bodyType: 5, partFlags2: 0x04)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore(
                [simInfo, fullBodyResource, geometryResource],
                queryResources: [],
                simTemplateBodyPartFacts: indexedBodyPartFacts));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, simPackagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.Contains(graph.SimGraph!.BodyCandidates, candidate => candidate.Label == "Full Body" && candidate.Count == 1 && candidate.SourceKind == SimBodyCandidateSourceKind.ExactPartLink);
        Assert.Contains(graph.SimGraph.BodyCandidates.SelectMany(static candidate => candidate.Candidates), option => option.DisplayName == "yfBody_Base");
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildsSimMetadataGraph_WithResolvedBodyCandidates()
    {
        var packagePath = Path.Combine(tempRoot, "sim-body-candidates.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 2/3/6 | traits 4 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=2 categories / 3 entries / 6 parts | traits=4"
        };
        var bytes = CreateSyntheticSimInfoBytesWithAuthoritativeNudeOutfits();
        var casAssets = new[]
        {
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfBody_Base", "Full Body", "body.package", new ResourceKeyRecord(0x034AEECB, 0, 0x6000000000000000ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "full-body", Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female"),
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfBody_Alt", "Top", "body.package", new ResourceKeyRecord(0x034AEECB, 0, 0x6000000000000001ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "top", Description: "slot=Top | species=Human | age=Young Adult | gender=Female"),
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfShoes_Base", "Shoes", "body.package", new ResourceKeyRecord(0x034AEECB, 0, 0x6000000000000002ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "shoes", Description: "slot=Shoes | species=Human | age=Young Adult | gender=Female")
        };
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = bytes,
                    [casAssets[0].RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBody_Base", bodyType: 5, partFlags2: 0x04),
                    [casAssets[1].RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBody_Alt", bodyType: 6),
                    [casAssets[2].RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfShoes_Base", bodyType: 8)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], casAssets));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.Contains(graph.SimGraph!.BodyCandidates, candidate => candidate.Label == "Full Body" && candidate.Count == 1 && candidate.SourceKind == SimBodyCandidateSourceKind.ExactPartLink);
        Assert.Contains(graph.SimGraph.BodyCandidates, candidate => candidate.Label == "Top" && candidate.Count == 1 && candidate.SourceKind == SimBodyCandidateSourceKind.ExactPartLink);
        Assert.Contains(graph.SimGraph.BodyCandidates.SelectMany(static candidate => candidate.Candidates), option => option.DisplayName == "yfBody_Base");
        Assert.Contains(graph.SimGraph.BodyCandidates.SelectMany(static candidate => candidate.Candidates), option => option.DisplayName == "yfBody_Alt");
        Assert.Equal(SimBodyAssemblyMode.FullBodyShell, graph.SimGraph.BodyAssembly.Mode);
        Assert.Contains(graph.SimGraph.BodyAssembly.Layers, layer => layer.Label == "Full Body" && layer.State == SimBodyAssemblyLayerState.Active);
        Assert.Contains(graph.SimGraph.BodyAssembly.Layers, layer => layer.Label == "Top" && layer.State == SimBodyAssemblyLayerState.Blocked);
        Assert.DoesNotContain(graph.SimGraph.BodyAssembly.Layers, layer => layer.Label == "Shoes");
        Assert.Contains(graph.SimGraph.BodyAssembly.GraphNodes, node => node.Label == "Base frame" && node.State == SimBodyGraphNodeState.Resolved);
        Assert.Contains(graph.SimGraph.BodyAssembly.GraphNodes, node => node.Label == "Geometry shell" && node.State == SimBodyGraphNodeState.Approximate);
        Assert.Contains(graph.SimGraph.BodyAssembly.GraphNodes, node => node.Label == "Footwear overlay" && node.State == SimBodyGraphNodeState.Pending);
        Assert.Contains(graph.SimGraph.BodyAssembly.GraphNodes, node => node.Label == "Body morph application" && node.State == SimBodyGraphNodeState.Pending);
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildsSimMetadataGraph_WithOnlyAuthoritativeSplitBodyLayers()
    {
        var packagePath = Path.Combine(tempRoot, "sim-body-split-layers.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 1/1/3 | traits 4 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=1 categories / 1 entries / 3 parts | traits=4"
        };
        var bytes = CreateSyntheticSimInfoBytesWithAuthoritativeSplitNudeOutfits();
        var casAssets = new[]
        {
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfTop_Nude", "Top", "body.package", new ResourceKeyRecord(0x034AEECB, 0, 0x6100000000000000ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "top", Description: "slot=Top | species=Human | age=Young Adult | gender=Female"),
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfBottom_Nude", "Bottom", "body.package", new ResourceKeyRecord(0x034AEECB, 0, 0x6100000000000001ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "bottom", Description: "slot=Bottom | species=Human | age=Young Adult | gender=Female"),
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfShoes_Nude", "Shoes", "body.package", new ResourceKeyRecord(0x034AEECB, 0, 0x6100000000000002ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "shoes", Description: "slot=Shoes | species=Human | age=Young Adult | gender=Female"),
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "acBody_Nude", "Full Body", "ClientFullBuild0.package", new ResourceKeyRecord(0x034AEECB, 0, 0x6100000000000003ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "full-body", Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female")
        };
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = bytes,
                    [casAssets[0].RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfTop_Nude", bodyType: 6, partFlags2: 0x04),
                    [casAssets[1].RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBottom_Nude", bodyType: 7, partFlags2: 0x04),
                    [casAssets[2].RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfShoes_Nude", bodyType: 8),
                    [casAssets[3].RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "acBody_Nude", bodyType: 5, partFlags2: 0x04)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], casAssets));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.DoesNotContain(graph.SimGraph!.BodyCandidates, candidate => candidate.Label == "Full Body" && candidate.Count > 0);
        Assert.Contains(graph.SimGraph.BodyCandidates, candidate => candidate.Label == "Top" && candidate.Count == 1 && candidate.SourceKind == SimBodyCandidateSourceKind.ExactPartLink);
        Assert.Contains(graph.SimGraph.BodyCandidates, candidate => candidate.Label == "Bottom" && candidate.Count == 1 && candidate.SourceKind == SimBodyCandidateSourceKind.ExactPartLink);
        Assert.Contains(graph.SimGraph.BodyCandidates, candidate => candidate.Label == "Shoes" && candidate.Count == 1 && candidate.SourceKind == SimBodyCandidateSourceKind.ExactPartLink);
        Assert.Equal(SimBodyAssemblyMode.SplitBodyLayers, graph.SimGraph.BodyAssembly.Mode);
        Assert.DoesNotContain(graph.SimGraph.BodyAssembly.Layers, layer => layer.Label == "Full Body" && layer.State == SimBodyAssemblyLayerState.Active);
        Assert.Contains(graph.SimGraph.BodyAssembly.Layers, layer => layer.Label == "Top" && layer.State == SimBodyAssemblyLayerState.Active);
        Assert.Contains(graph.SimGraph.BodyAssembly.Layers, layer => layer.Label == "Bottom" && layer.State == SimBodyAssemblyLayerState.Active);
        Assert.Contains(graph.SimGraph.BodyAssembly.Layers, layer => layer.Label == "Shoes" && layer.State == SimBodyAssemblyLayerState.Active);
        Assert.Contains(
            graph.SimGraph.BodyAssembly.GraphNodes,
            node => node.Label == "Geometry shell" &&
                    node.State == SimBodyGraphNodeState.Resolved &&
                    node.Notes.Contains("Split body shell composed from 2 active layer(s), including authoritative SimInfo selections.", StringComparison.Ordinal));
        Assert.Contains(
            graph.SimGraph.BodyAssembly.GraphNodes,
            node => node.Label == "Footwear overlay" && node.State == SimBodyGraphNodeState.Approximate);
    }

    [Fact]
    public async Task AssetGraphBuilder_PrefersIndexedDefaultBodyRecipeOverAuthoritativeClothingLikeFullBodySelection()
    {
        var packagePath = Path.Combine(tempRoot, "sim-body-authoritative-clothing.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 2/3/6 | traits 4 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=2 categories / 3 entries / 6 parts | traits=4"
        };
        var bytes = CreateSyntheticSimInfoBytesWithAuthoritativeNudeOutfits();
        var authoritativeDress = new AssetSummary(
            Guid.NewGuid(),
            sourceId,
            SourceKind.Game,
            AssetKind.Cas,
            "yfBody_DetectiveDress",
            "Full Body",
            "body.package",
            new ResourceKeyRecord(0x034AEECB, 0, 0x6000000000000000ul, "CASPart"),
            null,
            1,
            1,
            string.Empty,
            new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true),
            CategoryNormalized: "full-body",
            Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female");
        var canonicalFoundation = new AssetSummary(
            Guid.NewGuid(),
            sourceId,
            SourceKind.Game,
            AssetKind.Cas,
            "yfBody_Nude_Default",
            "Full Body",
            "ClientFullBuild0.package",
            new ResourceKeyRecord(0x034AEECB, 0, 0x6000000000000001ul, "CASPart"),
            null,
            1,
            1,
            string.Empty,
            new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true),
            CategoryNormalized: "full-body",
            Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female | defaultBodyType=true | nakedLink=true");
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = bytes,
                    [authoritativeDress.RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBody_DetectiveDress", bodyType: 5),
                    [canonicalFoundation.RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBody_Nude_Default", bodyType: 5, partFlags2: 0x04)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], [authoritativeDress, canonicalFoundation]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        var fullBody = Assert.Single(graph.SimGraph!.BodyCandidates.Where(candidate => candidate.Label == "Full Body"));
        Assert.Equal(SimBodyCandidateSourceKind.IndexedDefaultBodyRecipe, fullBody.SourceKind);
        Assert.Equal(1, fullBody.Count);
        Assert.Contains(fullBody.Candidates, candidate => candidate.DisplayName == "yfBody_Nude_Default");
        Assert.DoesNotContain(fullBody.Candidates, candidate => candidate.DisplayName == "yfBody_DetectiveDress");
        Assert.Contains("indexed default/naked body-recipe CASParts", fullBody.Notes, StringComparison.Ordinal);
        Assert.Equal(SimBodyAssemblyMode.FullBodyShell, graph.SimGraph.BodyAssembly.Mode);
        Assert.Contains(
            graph.SimGraph.BodyAssembly.GraphNodes,
            node => node.Label == "Geometry shell" &&
                    node.Notes.Contains("compatible CAS body candidates", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AssetGraphBuilder_WithholdsAuthoritativeClothingLikeFullBodySelection_WhenNoRealFoundationExists()
    {
        var packagePath = Path.Combine(tempRoot, "sim-body-authoritative-clothing-only.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 2/3/6 | traits 4 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=2 categories / 3 entries / 6 parts | traits=4"
        };
        var bytes = CreateSyntheticSimInfoBytesWithAuthoritativeNudeOutfits();
        var authoritativeDress = new AssetSummary(
            Guid.NewGuid(),
            sourceId,
            SourceKind.Game,
            AssetKind.Cas,
            "yfBody_DetectiveDress",
            "Full Body",
            "body.package",
            new ResourceKeyRecord(0x034AEECB, 0, 0x6000000000000000ul, "CASPart"),
            null,
            1,
            1,
            string.Empty,
            new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true),
            CategoryNormalized: "full-body",
            Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female");
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = bytes,
                    [authoritativeDress.RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBody_DetectiveDress", bodyType: 5)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], [authoritativeDress]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        var fullBody = Assert.Single(graph.SimGraph!.BodyCandidates.Where(candidate => candidate.Label == "Full Body"));
        Assert.Equal(SimBodyCandidateSourceKind.ExactPartLink, fullBody.SourceKind);
        Assert.Equal(0, fullBody.Count);
        Assert.Empty(fullBody.Candidates);
        Assert.Contains("withheld authoritative non-default shell candidates", fullBody.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SimBodyAssemblyMode.None, graph.SimGraph.BodyAssembly.Mode);
        Assert.Contains(
            graph.SimGraph.BodyAssembly.GraphNodes,
            node => node.Label == "Geometry shell" && node.State == SimBodyGraphNodeState.Unavailable);
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildsSimMetadataGraph_WithExactResourceResolvedBodyCandidates()
    {
        var packagePath = Path.Combine(tempRoot, "sim-body-candidate-resources.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 2/3/6 | traits 4 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=2 categories / 3 entries / 6 parts | traits=4"
        };
        var bytes = CreateSyntheticSimInfoBytesWithAuthoritativeNudeOutfits();
        var bodyPackage = Path.Combine(tempRoot, "body-resources.package");
        var fullBodyCasPart = CreateResource(sourceId, bodyPackage, SourceKind.Game, "CASPart", 1) with
        {
            Key = new ResourceKeyRecord((uint)ResourceType.CASPart, 0, 0x6000000000000000ul, "CASPart"),
            Name = "yfBody_Base",
            Description = "slot=Full Body | species=Human | age=Young Adult | gender=Female"
        };
        var fullBodyGeometry = CreateResource(sourceId, bodyPackage, SourceKind.Game, "Geometry", 1) with
        {
            Key = new ResourceKeyRecord((uint)ResourceType.Geometry, 0, 0x6000000000000000ul, "Geometry")
        };
        var fullBodyThumbnail = CreateResource(sourceId, bodyPackage, SourceKind.Game, "CASPartThumbnail", 1) with
        {
            Key = new ResourceKeyRecord((uint)ResourceType.CASPartThumbnail, 0, 0x6000000000000000ul, "CASPartThumbnail")
        };
        var shoesCasPart = CreateResource(sourceId, bodyPackage, SourceKind.Game, "CASPart", 2) with
        {
            Key = new ResourceKeyRecord((uint)ResourceType.CASPart, 0, 0x6000000000000002ul, "CASPart"),
            Name = "yfShoes_Base",
            Description = "slot=Shoes | species=Human | age=Young Adult | gender=Female"
        };
        var shoesGeometry = CreateResource(sourceId, bodyPackage, SourceKind.Game, "Geometry", 2) with
        {
            Key = new ResourceKeyRecord((uint)ResourceType.Geometry, 0, 0x6000000000000002ul, "Geometry")
        };
        var shoesThumbnail = CreateResource(sourceId, bodyPackage, SourceKind.Game, "CASPartThumbnail", 2) with
        {
            Key = new ResourceKeyRecord((uint)ResourceType.CASPartThumbnail, 0, 0x6000000000000002ul, "CASPartThumbnail")
        };
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = bytes,
                    [fullBodyCasPart.Key.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBody_Base", bodyType: 5, partFlags2: 0x04),
                    [shoesCasPart.Key.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfShoes_Base", bodyType: 8)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([fullBodyCasPart, fullBodyGeometry, fullBodyThumbnail, shoesCasPart, shoesGeometry, shoesThumbnail]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.Contains(graph.SimGraph!.BodyCandidates, candidate => candidate.Label == "Full Body" && candidate.Count == 1 && candidate.SourceKind == SimBodyCandidateSourceKind.ExactPartLink);
        Assert.Contains(graph.SimGraph.BodyCandidates.SelectMany(static candidate => candidate.Candidates), option => option.DisplayName == "yfBody_Base");
        Assert.DoesNotContain(graph.SimGraph.BodyCandidates.SelectMany(static candidate => candidate.Candidates), option => option.DisplayName == "yfShoes_Base");
    }

    [Fact]
    public async Task AssetGraphBuilder_CollapsesDuplicateAuthoritativeBodyCandidates_ByPartInstance()
    {
        var packagePath = Path.Combine(tempRoot, "sim-body-candidate-dedup.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 2/3/6 | traits 4 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=2 categories / 3 entries / 6 parts | traits=4"
        };
        var bytes = CreateSyntheticSimInfoBytesWithAuthoritativeNudeOutfits();
        var lowQualityPackage = Path.Combine(tempRoot, "ClientDeltaBuild0.package");
        var preferredPackage = Path.Combine(tempRoot, "ClientFullBuild0.package");
        var partKey = new ResourceKeyRecord((uint)ResourceType.CASPart, 0, 0x6000000000000000ul, "CASPart");
        var lowQualityCasPart = CreateResource(sourceId, lowQualityPackage, SourceKind.Game, "CASPart", 1) with
        {
            Key = partKey,
            Name = "yfBody_Base",
            Description = "slot=Full Body | species=Human | age=Young Adult | gender=Female"
        };
        var preferredCasPart = CreateResource(sourceId, preferredPackage, SourceKind.Game, "CASPart", 1) with
        {
            Key = partKey,
            Name = "yfBody_Base",
            Description = "slot=Full Body | species=Human | age=Young Adult | gender=Female"
        };
        var preferredGeometry = CreateResource(sourceId, preferredPackage, SourceKind.Game, "Geometry", 1) with
        {
            Key = new ResourceKeyRecord((uint)ResourceType.Geometry, 0, partKey.FullInstance, "Geometry")
        };
        var preferredThumbnail = CreateResource(sourceId, preferredPackage, SourceKind.Game, "CASPartThumbnail", 1) with
        {
            Key = new ResourceKeyRecord((uint)ResourceType.CASPartThumbnail, 0, partKey.FullInstance, "CASPartThumbnail")
        };
        var preferredRig = CreateResource(sourceId, preferredPackage, SourceKind.Game, "Rig", 1) with
        {
            Key = new ResourceKeyRecord((uint)ResourceType.Rig, 0, partKey.FullInstance, "Rig")
        };
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = bytes,
                    [$"{lowQualityPackage}|{lowQualityCasPart.Key.FullTgi}"] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBody_Base", bodyType: 5, partFlags2: 0x04),
                    [$"{preferredPackage}|{preferredCasPart.Key.FullTgi}"] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBody_Base", bodyType: 5, partFlags2: 0x04)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([lowQualityCasPart, preferredCasPart, preferredGeometry, preferredThumbnail, preferredRig]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        var fullBody = Assert.Single(graph.SimGraph!.BodyCandidates.Where(candidate => candidate.Label == "Full Body"));
        Assert.Equal(1, fullBody.Count);
        Assert.Single(fullBody.Candidates);
        Assert.Equal(preferredPackage, fullBody.Candidates[0].PackagePath);
        Assert.Contains("collapsed 1 package duplicate", fullBody.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SimBodyAssemblyPolicy_ClassifiesShellAndOverlayFamilies()
    {
        Assert.True(SimBodyAssemblyPolicy.IsShellFamilyLabel("Full Body"));
        Assert.True(SimBodyAssemblyPolicy.IsShellFamilyLabel("Body"));
        Assert.False(SimBodyAssemblyPolicy.IsShellFamilyLabel("Top"));
        Assert.False(SimBodyAssemblyPolicy.IsShellFamilyLabel("Bottom"));
        Assert.False(SimBodyAssemblyPolicy.IsShellFamilyLabel("Shoes"));

        Assert.True(SimBodyAssemblyPolicy.IsOverlayFamilyLabel("Shoes"));
        Assert.False(SimBodyAssemblyPolicy.IsOverlayFamilyLabel("Full Body"));
    }

    [Fact]
    public void SimBodyAssemblyPolicy_ActivatesSplitBodyLayersWhenNoShellExists()
    {
        var active = SimBodyAssemblyPolicy.ResolveActiveLabels(["Top", "Bottom", "Shoes"]);

        Assert.Equal(3, active.Count);
        Assert.Contains("Top", active);
        Assert.Contains("Bottom", active);
        Assert.Contains("Shoes", active);
        Assert.Equal(SimBodyAssemblyMode.SplitBodyLayers, SimBodyAssemblyPolicy.GetMode(active));
    }

    [Fact]
    public void SimBodyAssemblyPolicy_ActivatesShellUnderlayAlongsideSplitBodyLayers()
    {
        var active = SimBodyAssemblyPolicy.ResolveActiveLabels(["Full Body", "Top", "Bottom", "Shoes"]);

        Assert.Equal(4, active.Count);
        Assert.Contains("Full Body", active);
        Assert.Contains("Top", active);
        Assert.Contains("Bottom", active);
        Assert.Contains("Shoes", active);
        Assert.Equal(SimBodyAssemblyMode.BodyUnderlayWithSplitLayers, SimBodyAssemblyPolicy.GetMode(active));
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildsExplicitCasGraph_ForInfantHumanSubset()
    {
        var packagePath = Path.Combine(tempRoot, "cas-infant-supported.package");
        var sourceId = Guid.NewGuid();
        var casPart = CreateResource(sourceId, packagePath, SourceKind.Game, "CASPart", 1, "imBody_Nude");
        var geometry = CreateResource(sourceId, packagePath, SourceKind.Game, "Geometry", 1, "InfantGeom");
        var rig = CreateResource(sourceId, packagePath, SourceKind.Game, "Rig", 1, "InfantRig");
        var texture = CreateResource(sourceId, packagePath, SourceKind.Game, "PNGImage", 1);
        var thumbnail = CreateResource(sourceId, packagePath, SourceKind.Game, "CASPartThumbnail", 1);

        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [casPart.Key.FullTgi] = CreateSyntheticCasPartBytes(
                    geometry.Key,
                    thumbnail.Key,
                    texture.Key,
                    internalName: "imBody_Nude",
                    bodyType: 5,
                    ageGenderFlags: 0x00001080u),
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

        Assert.NotNull(graph.CasGraph);
        Assert.True(graph.CasGraph!.IsSupported, string.Join(Environment.NewLine, graph.Diagnostics));
        Assert.Equal(geometry.Key.FullTgi, graph.CasGraph.GeometryResource?.Key.FullTgi);
        Assert.DoesNotContain(graph.Diagnostics, message => message.Contains("outside the supported human skinned age subset", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildPreviewGraphAsync_UsesIndexedDefaultBodyRecipe_ForInfantWithoutOutfits()
    {
        var packagePath = Path.Combine(tempRoot, "sim-infant-indexed-default-body.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Infant | Male [00000001]",
            Description = "SimInfo v32 | species=Human | age=Infant | gender=Male | outfits=0 categories / 0 entries / 0 parts | traits=0"
        };
        var defaultBody = new AssetSummary(
            Guid.NewGuid(),
            sourceId,
            SourceKind.Game,
            AssetKind.Cas,
            "iuBody_Nude_Default",
            "Full Body",
            "body.package",
            new ResourceKeyRecord(0x034AEECB, 0, 0x6600000000000000ul, "CASPart"),
            null,
            1,
            1,
            "slot=Full Body | bodyType=5 | internalName=iuBody_Nude_Default | species=Human | age=Infant | gender=Male | defaultBodyType=true | defaultBodyTypeFemale=false | defaultBodyTypeMale=false | nakedLink=false | restrictOppositeGender=false | restrictOppositeFrame=false | sortLayer=0",
            new AssetCapabilitySnapshot(true, true, false, false, HasIdentityMetadata: true),
            CategoryNormalized: "full-body",
            Description: "slot=Full Body | bodyType=5 | internalName=iuBody_Nude_Default | species=Human | age=Infant | gender=Male | defaultBodyType=true | defaultBodyTypeFemale=false | defaultBodyTypeMale=false | nakedLink=false | restrictOppositeGender=false | restrictOppositeFrame=false | sortLayer=0");
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = CreateSyntheticSimInfoBytesWithoutOutfits(ageFlags: 0x00000080u, genderFlags: 0x00001000u, speciesValue: 1u)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], [defaultBody]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildPreviewGraphAsync(summary, [simInfo], CancellationToken.None);

        var fullBody = Assert.Single(graph.SimGraph!.BodyCandidates.Where(candidate => candidate.Label == "Full Body"));
        Assert.Equal(SimBodyCandidateSourceKind.IndexedDefaultBodyRecipe, fullBody.SourceKind);
        Assert.Equal("iuBody_Nude_Default", Assert.Single(fullBody.Candidates).DisplayName);
        Assert.Equal(SimBodyAssemblyMode.FullBodyShell, graph.SimGraph.BodyAssembly.Mode);
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildPreviewGraphAsync_UsesIndexedDefaultBodyRecipe_ForNonHumanWithoutOutfits()
    {
        var packagePath = Path.Combine(tempRoot, "sim-dog-indexed-default-body.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "Character template: Dog | Adult | Male [00000001]",
            Description = "SimInfo v32 | species=Dog | age=Adult | gender=Male | outfits=0 categories / 0 entries / 0 parts | traits=0"
        };
        var defaultBody = new AssetSummary(
            Guid.NewGuid(),
            sourceId,
            SourceKind.Game,
            AssetKind.Cas,
            "adBody_Nude_Default",
            "Full Body",
            "dog-body.package",
            new ResourceKeyRecord(0x034AEECB, 0, 0x6610000000000000ul, "CASPart"),
            null,
            1,
            1,
            "slot=Full Body | bodyType=5 | internalName=adBody_Nude_Default | species=Dog | age=Adult | gender=Male | defaultBodyType=true | defaultBodyTypeFemale=false | defaultBodyTypeMale=false | nakedLink=false | restrictOppositeGender=false | restrictOppositeFrame=false | sortLayer=0",
            new AssetCapabilitySnapshot(true, true, false, false, HasIdentityMetadata: true),
            CategoryNormalized: "full-body",
            Description: "slot=Full Body | bodyType=5 | internalName=adBody_Nude_Default | species=Dog | age=Adult | gender=Male | defaultBodyType=true | defaultBodyTypeFemale=false | defaultBodyTypeMale=false | nakedLink=false | restrictOppositeGender=false | restrictOppositeFrame=false | sortLayer=0");
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = CreateSyntheticSimInfoBytesWithoutOutfits(ageFlags: 0x00000020u, genderFlags: 0x00001000u, speciesValue: 2u)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], [defaultBody]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildPreviewGraphAsync(summary, [simInfo], CancellationToken.None);

        var fullBody = Assert.Single(graph.SimGraph!.BodyCandidates.Where(candidate => candidate.Label == "Full Body"));
        Assert.Equal(SimBodyCandidateSourceKind.IndexedDefaultBodyRecipe, fullBody.SourceKind);
        Assert.Equal("adBody_Nude_Default", Assert.Single(fullBody.Candidates).DisplayName);
        Assert.Equal(SimBodyAssemblyMode.FullBodyShell, graph.SimGraph.BodyAssembly.Mode);
    }

    [Fact]
    public void AssetGraphBuilder_AllowsDogSpeciesAlias_ForLittleDogChildIndexedBodyRecipe()
    {
        var method = typeof(ExplicitAssetGraphBuilder).GetMethod("MatchesCompatibleBodyRecipeSpecies", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var littleDogChild = new SimInfoSummary(32, "Little Dog", "Child", "Female", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var littleDogAdult = new SimInfoSummary(32, "Little Dog", "Adult", "Female", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        Assert.True(Assert.IsType<bool>(method!.Invoke(null, ["Dog", littleDogChild])));
        Assert.False(Assert.IsType<bool>(method.Invoke(null, ["Dog", littleDogAdult])));
    }

    [Fact]
    public void AssetGraphBuilder_UsesYoungAgeBodyShellPrefixes_ForInfantToddlerAndChild()
    {
        var expectedMethod = typeof(ExplicitAssetGraphBuilder).GetMethod("BuildExpectedBodyShellPrefixes", BindingFlags.Static | BindingFlags.NonPublic);
        var genericMethod = typeof(ExplicitAssetGraphBuilder).GetMethod("BuildGenericHumanFoundationPrefixes", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(expectedMethod);
        Assert.NotNull(genericMethod);

        var infant = new SimInfoSummary(32, "Human", "Infant", "Male", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var toddler = new SimInfoSummary(32, "Human", "Toddler", "Female", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var child = new SimInfoSummary(32, "Human", "Child", "Male", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var infantExpected = Assert.IsAssignableFrom<IReadOnlyList<string>>(expectedMethod!.Invoke(null, [infant]));
        var toddlerExpected = Assert.IsAssignableFrom<IReadOnlyList<string>>(expectedMethod.Invoke(null, [toddler]));
        var childExpected = Assert.IsAssignableFrom<IReadOnlyList<string>>(expectedMethod.Invoke(null, [child]));
        var infantGeneric = Assert.IsAssignableFrom<IReadOnlyList<string>>(genericMethod!.Invoke(null, [infant]));
        var toddlerGeneric = Assert.IsAssignableFrom<IReadOnlyList<string>>(genericMethod.Invoke(null, [toddler]));
        var childGeneric = Assert.IsAssignableFrom<IReadOnlyList<string>>(genericMethod.Invoke(null, [child]));

        Assert.Contains("iuBody_", infantExpected);
        Assert.Contains("iuBody", infantExpected);
        Assert.Contains("iuBody_", infantGeneric);
        Assert.Contains("iuBody", infantGeneric);

        Assert.Contains("pfBody_", toddlerExpected);
        Assert.Contains("pfBody", toddlerExpected);
        Assert.Contains("puBody_", toddlerGeneric);
        Assert.Contains("puBody", toddlerGeneric);
        Assert.Contains("pfBody_", toddlerGeneric);
        Assert.Contains("pfBody", toddlerGeneric);

        Assert.Contains("cmBody_", childExpected);
        Assert.Contains("cmBody", childExpected);
        Assert.Contains("cuBody_", childGeneric);
        Assert.Contains("cuBody", childGeneric);
        Assert.Contains("cmBody_", childGeneric);
        Assert.Contains("cmBody", childGeneric);
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildsSimMetadataGraph_WithIndexedDefaultBodyRecipeCandidates()
    {
        var packagePath = Path.Combine(tempRoot, "sim-body-fallback.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 2/3/6 | traits 4 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=2 categories / 3 entries / 6 parts | traits=4"
        };
        var bytes = CreateSyntheticSimInfoBytesWithAuthoritativeNudeOutfits();
        var casAssets = new[]
        {
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfBody_Fallback", "Full Body", "body.package", new ResourceKeyRecord(0x034AEECB, 0, 0x7000000000000000ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "full-body", Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female | defaultBodyType=true | nakedLink=true")
        };
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = bytes,
                    [casAssets[0].RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBody_Fallback", bodyType: 5, partFlags2: 0x04)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], casAssets));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        var fallback = Assert.Single(graph.SimGraph!.BodyCandidates.Where(candidate => candidate.Label == "Full Body"));
        Assert.Equal(1, fallback.Count);
        Assert.Equal(SimBodyCandidateSourceKind.IndexedDefaultBodyRecipe, fallback.SourceKind);
        Assert.Contains("indexed default/naked body-recipe CASParts", fallback.Notes, StringComparison.Ordinal);
        Assert.Contains(fallback.Candidates, candidate => candidate.DisplayName == "yfBody_Fallback");
    }

    [Fact]
    public async Task AssetGraphBuilder_PrefersGenericHumanBodyCandidatesOverOccultFallbacks()
    {
        var packagePath = Path.Combine(tempRoot, "sim-body-priority.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 0/0/0 | traits 0 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=0 categories / 0 entries / 0 parts | traits=0"
        };
        var bytes = CreateSyntheticSimInfoBytesWithAuthoritativeNudeOutfits();
        var casAssets = new[]
        {
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfBody_EP01AlienAlpha_BlackGreenTrim", "Full Body", "ep01alien.package", new ResourceKeyRecord(0x034AEECB, 0, 0x7100000000000000ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "full-body", Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female"),
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfBody_NudeBase", "Full Body", "ClientFullBuild0.package", new ResourceKeyRecord(0x034AEECB, 0, 0x7100000000000001ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "full-body", Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female | defaultBodyType=true | nakedLink=true")
        };
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = bytes,
                    [casAssets[0].RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBody_EP01AlienAlpha_BlackGreenTrim", bodyType: 5),
                    [casAssets[1].RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBody_NudeBase", bodyType: 5, partFlags2: 0x04)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], casAssets));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        var fullBody = Assert.Single(graph.SimGraph!.BodyCandidates.Where(candidate => candidate.Label == "Full Body"));
        Assert.Equal("yfBody_NudeBase", fullBody.Candidates[0].DisplayName);
        Assert.Equal(SimBodyCandidateSourceKind.IndexedDefaultBodyRecipe, fullBody.SourceKind);
        Assert.DoesNotContain(fullBody.Candidates, candidate => candidate.DisplayName == "yfBody_EP01AlienAlpha_BlackGreenTrim");
        Assert.Contains("indexed default/naked", fullBody.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssetGraphBuilder_WithholdsNonHumanNudeShell_WhenNoAuthoritativeHumanDefaultShellExists()
    {
        var packagePath = Path.Combine(tempRoot, "sim-body-unisex-shell.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 0/0/0 | traits 0 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=0 categories / 0 entries / 0 parts | traits=0"
        };
        var bytes = CreateSyntheticSimInfoBytesWithAuthoritativeNudeOutfits();
        var casAssets = new[]
        {
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfBody_EP01AlienAlpha_BlackGreenTrim", "Full Body", "ep01alien.package", new ResourceKeyRecord(0x034AEECB, 0, 0x7200000000000000ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "full-body", Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female"),
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfBody_EP01Detective_SolidBlack", "Full Body", "ep01detective.package", new ResourceKeyRecord(0x034AEECB, 0, 0x7200000000000001ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "full-body", Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female"),
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "acBody_Nude", "Full Body", "ClientFullBuild0.package", new ResourceKeyRecord(0x034AEECB, 0, 0x7200000000000002ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "full-body", Description: "slot=Full Body | species=Cat | age=Young Adult | gender=Unisex | defaultBodyType=true | nakedLink=true")
        };
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = bytes,
                    [casAssets[0].RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBody_EP01AlienAlpha_BlackGreenTrim", bodyType: 5),
                    [casAssets[1].RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBody_EP01Detective_SolidBlack", bodyType: 5),
                    [casAssets[2].RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "acBody_Nude", bodyType: 5, partFlags2: 0x04, speciesValue: 3u)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], casAssets));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        Assert.DoesNotContain(graph.SimGraph!.BodyCandidates, candidate => candidate.Label == "Full Body" && candidate.Count > 0);
        Assert.Equal(SimBodyAssemblyMode.None, graph.SimGraph.BodyAssembly.Mode);
        Assert.Contains(graph.SimGraph.BodyAssembly.GraphNodes, node => node.Label == "Geometry shell" && node.State == SimBodyGraphNodeState.Unavailable);
    }

    [Fact]
    public async Task AssetGraphBuilder_AcceptsGenericHumanNudeShell_ForHumanArchetypeIndexedDefaultFallback()
    {
        var packagePath = Path.Combine(tempRoot, "sim-body-prefix-filter.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 0/0/0 | traits 0 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=0 categories / 0 entries / 0 parts | traits=0"
        };
        var bytes = CreateSyntheticSimInfoBytesWithAuthoritativeNudeOutfits();
        var casAssets = new[]
        {
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "acBody_Nude", "Full Body", "ClientFullBuild0.package", new ResourceKeyRecord(0x034AEECB, 0, 0x7210000000000000ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "full-body", Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female | defaultBodyType=true | nakedLink=true")
        };
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = bytes,
                    [casAssets[0].RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "acBody_Nude", bodyType: 5, partFlags2: 0x04, speciesValue: 1u)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], casAssets));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        var fullBody = Assert.Single(graph.SimGraph!.BodyCandidates.Where(candidate => candidate.Label == "Full Body"));
        Assert.Equal(1, fullBody.Count);
        Assert.Equal(SimBodyCandidateSourceKind.IndexedDefaultBodyRecipe, fullBody.SourceKind);
        Assert.Contains(fullBody.Candidates, candidate => candidate.DisplayName == "acBody_Nude");
        Assert.Contains("indexed default/naked body-recipe CASParts", fullBody.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssetGraphBuilder_AcceptsGenericHumanNudeShell_WhenIndexedRecipeUsesGenericUnisexAsset()
    {
        var packagePath = Path.Combine(tempRoot, "sim-body-prefix-no-facts.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 0/0/0 | traits 0 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=0 categories / 0 entries / 0 parts | traits=0"
        };
        var bytes = CreateSyntheticSimInfoBytesWithAuthoritativeNudeOutfits();
        var casAssets = new[]
        {
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "ahBody_nude", "Full Body", "SimulationFullBuild0.package", new ResourceKeyRecord(0x034AEECB, 0, 0x7211000000000000ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "full-body", Description: "slot=Full Body | species=Human | age=Teen / Young Adult / Adult / Elder | gender=Unisex | defaultBodyType=true | nakedLink=true")
        };
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = bytes
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], casAssets));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        var fullBody = Assert.Single(graph.SimGraph!.BodyCandidates.Where(candidate => candidate.Label == "Full Body"));
        Assert.Equal(1, fullBody.Count);
        Assert.Equal(SimBodyCandidateSourceKind.IndexedDefaultBodyRecipe, fullBody.SourceKind);
        Assert.Contains(fullBody.Candidates, candidate => candidate.DisplayName == "ahBody_nude");
        Assert.Contains("indexed default/naked body-recipe CASParts", fullBody.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssetGraphBuilder_DoesNotMarkEmptyShellFamilyAsActive_WhenOnlyOverlayCandidatesRemain()
    {
        var packagePath = Path.Combine(tempRoot, "sim-body-empty-shell-state.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 0/0/0 | traits 0 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=0 categories / 0 entries / 0 parts | traits=0"
        };
        var bytes = CreateSyntheticSimInfoBytesWithAuthoritativeNudeOutfits();
        var casAssets = new[]
        {
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfBody_DetectiveDress", "Full Body", "ClientFullBuild0.package", new ResourceKeyRecord(0x034AEECB, 0, 0x7220000000000000ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "full-body", Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female"),
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfShoes_Base", "Shoes", "ClientFullBuild0.package", new ResourceKeyRecord(0x034AEECB, 0, 0x7220000000000001ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "shoes", Description: "slot=Shoes | species=Human | age=Young Adult | gender=Female | nakedLink=true")
        };
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = bytes,
                    [casAssets[0].RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBody_DetectiveDress", bodyType: 5, speciesValue: 1u),
                    [casAssets[1].RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfShoes_Base", bodyType: 8)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], casAssets));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        Assert.Equal(SimBodyAssemblyMode.None, graph.SimGraph!.BodyAssembly.Mode);
        Assert.DoesNotContain(graph.SimGraph.BodyAssembly.Layers, layer => layer.Label == "Full Body");
        Assert.Contains(graph.SimGraph.BodyAssembly.Layers, layer => layer.Label == "Shoes" && layer.CandidateCount == 1 && layer.State == SimBodyAssemblyLayerState.Available);
        Assert.Contains(graph.SimGraph.BodyAssembly.GraphNodes, node => node.Label == "Geometry shell" && node.State == SimBodyGraphNodeState.Unavailable);
        Assert.Contains(graph.SimGraph.BodyAssembly.GraphNodes, node => node.Label == "Footwear overlay" && node.State == SimBodyGraphNodeState.Pending);
    }

    [Fact]
    public async Task AssetGraphBuilder_FiltersClothingLikeFullBodyCandidates_WhenBaseShellExists()
    {
        var packagePath = Path.Combine(tempRoot, "sim-body-shell-filter.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 0/0/0 | traits 0 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=0 categories / 0 entries / 0 parts | traits=0"
        };
        var bytes = CreateSyntheticSimInfoBytesWithAuthoritativeNudeOutfits();
        var casAssets = new[]
        {
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfBody_DetectiveDress", "Full Body", "detective.package", new ResourceKeyRecord(0x034AEECB, 0, 0x7300000000000000ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "full-body", Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female"),
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfBody_Nude", "Full Body", "ClientFullBuild0.package", new ResourceKeyRecord(0x034AEECB, 0, 0x7300000000000001ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "full-body", Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female | defaultBodyType=true | nakedLink=true")
        };
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = bytes,
                    [casAssets[0].RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBody_DetectiveDress", bodyType: 5),
                    [casAssets[1].RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBody_Nude", bodyType: 5, partFlags2: 0x04)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], casAssets));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        var fullBody = Assert.Single(graph.SimGraph!.BodyCandidates.Where(candidate => candidate.Label == "Full Body"));
        Assert.Equal("yfBody_Nude", fullBody.Candidates[0].DisplayName);
        Assert.Equal(SimBodyCandidateSourceKind.IndexedDefaultBodyRecipe, fullBody.SourceKind);
        Assert.DoesNotContain(fullBody.Candidates, candidate => candidate.DisplayName == "yfBody_DetectiveDress");
        Assert.Contains("indexed default/naked body-recipe CASParts", fullBody.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssetGraphBuilder_PrefersDefaultForBodyTypeFemaleShell_OverClothingLikeShell()
    {
        var packagePath = Path.Combine(tempRoot, "sim-body-default-flag.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 0/0/0 | traits 0 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=0 categories / 0 entries / 0 parts | traits=0"
        };
        var bytes = CreateSyntheticSimInfoBytesWithAuthoritativeNudeOutfits();
        var clothingShell = new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfTop_EP01BlazerOversized_SolidBlack", "Full Body", "clothing.package", new ResourceKeyRecord(0x034AEECB, 0, 0x7310000000000000ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "full-body", Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female");
        var defaultShell = new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfBody_Base_Default", "Full Body", "ClientFullBuild0.package", new ResourceKeyRecord(0x034AEECB, 0, 0x7310000000000001ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "full-body", Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female | defaultBodyTypeFemale=true | nakedLink=true");
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");

        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = bytes,
                    [clothingShell.RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfTop_EP01BlazerOversized_SolidBlack", bodyType: 5),
                    [defaultShell.RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBody_Base_Default", bodyType: 5, partFlags2: 0x04)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], [clothingShell, defaultShell]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        var fullBody = Assert.Single(graph.SimGraph!.BodyCandidates.Where(candidate => candidate.Label == "Full Body"));
        Assert.Equal("yfBody_Base_Default", fullBody.Candidates[0].DisplayName);
        Assert.Equal(SimBodyCandidateSourceKind.IndexedDefaultBodyRecipe, fullBody.SourceKind);
        Assert.Contains("restricted to CASPart default/nude body shells", fullBody.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain(fullBody.Candidates, candidate => candidate.DisplayName == "yfTop_EP01BlazerOversized_SolidBlack");
    }

    [Fact]
    public async Task AssetGraphBuilder_PaginatesCanonicalBodyShellSearch_WhenDefaultFoundationIsBuriedAfterFirstPage()
    {
        var packagePath = Path.Combine(tempRoot, "sim-body-buried-foundation.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 0/0/0 | traits 0 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=0 categories / 0 entries / 0 parts | traits=0"
        };
        var bytes = CreateSyntheticSimInfoBytesWithAuthoritativeNudeOutfits();
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var assetBytes = new Dictionary<string, byte[]>
        {
            [simInfo.Key.FullTgi] = bytes
        };
        var casAssets = new List<AssetSummary>();

        for (var index = 0; index < 320; index++)
        {
            var asset = new AssetSummary(
                Guid.NewGuid(),
                sourceId,
                SourceKind.Game,
                AssetKind.Cas,
                $"yfBody_A{index:000}_Dress",
                "Full Body",
                "clothing.package",
                new ResourceKeyRecord(0x034AEECB, 0, 0x7330000000000000ul + (ulong)index, "CASPart"),
                null,
                1,
                1,
                string.Empty,
                new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true),
                CategoryNormalized: "full-body",
                Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female");
            casAssets.Add(asset);
            assetBytes[asset.RootKey.FullTgi] = CreateSyntheticCasPartBytes(
                geometryKey,
                thumbnailKey,
                textureKey,
                internalName: $"yfBody_A{index:000}_Dress",
                bodyType: 5);
        }

        var buriedDefaultShell = new AssetSummary(
            Guid.NewGuid(),
            sourceId,
            SourceKind.Game,
            AssetKind.Cas,
            "yfBody_ZFoundation",
            "Full Body",
            "ClientFullBuild0.package",
            new ResourceKeyRecord(0x034AEECB, 0, 0x7330000000000400ul, "CASPart"),
            null,
            1,
            1,
            string.Empty,
            new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true),
            CategoryNormalized: "full-body",
            Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female");
        casAssets.Add(buriedDefaultShell);
        assetBytes[buriedDefaultShell.RootKey.FullTgi] = CreateSyntheticCasPartBytes(
            geometryKey,
            thumbnailKey,
            textureKey,
            internalName: "yfBody_ZFoundation",
            bodyType: 5,
            partFlags2: 0x04);
        casAssets[casAssets.Count - 1] = buriedDefaultShell with
        {
            Description = "slot=Full Body | species=Human | age=Young Adult | gender=Female | defaultBodyType=true | nakedLink=true"
        };

        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                assetBytes,
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], casAssets));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        var fullBody = Assert.Single(graph.SimGraph!.BodyCandidates.Where(candidate => candidate.Label == "Full Body"));
        Assert.Equal(SimBodyCandidateSourceKind.IndexedDefaultBodyRecipe, fullBody.SourceKind);
        Assert.Equal("yfBody_ZFoundation", fullBody.Candidates[0].DisplayName);
        Assert.Contains("indexed default/naked body-recipe CASParts", fullBody.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssetGraphBuilder_WithholdsClothingLikeShellCandidates_WhenNoRealBodyShellIsFound()
    {
        var packagePath = Path.Combine(tempRoot, "sim-body-withhold-clothing.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 0/0/0 | traits 0 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=0 categories / 0 entries / 0 parts | traits=0"
        };
        var bytes = CreateSyntheticSimInfoBytesWithAuthoritativeNudeOutfits();
        var clothingShell = new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfTop_EP01BlazerOversized_SolidBlack", "Full Body", "clothing.package", new ResourceKeyRecord(0x034AEECB, 0, 0x7320000000000000ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "full-body", Description: "slot=Full Body | species=Human | age=Young Adult | gender=Female");
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");

        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = bytes,
                    [clothingShell.RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfTop_EP01BlazerOversized_SolidBlack", bodyType: 5)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], [clothingShell]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        Assert.DoesNotContain(graph.SimGraph!.BodyCandidates, candidate => candidate.Label == "Full Body" && candidate.Count > 0);
        Assert.Equal(SimBodyAssemblyMode.None, graph.SimGraph.BodyAssembly.Mode);
        Assert.Contains(graph.SimGraph.BodyAssembly.GraphNodes, node => node.Label == "Geometry shell" && node.State == SimBodyGraphNodeState.Unavailable);
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildsSimMetadataGraph_BodyCandidatesExcludeNonBodyCategories()
    {
        var packagePath = Path.Combine(tempRoot, "sim-body-filter.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 2/3/6 | traits 4 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=2 categories / 3 entries / 6 parts | traits=4"
        };
        var bytes = CreateSyntheticSimInfoBytesWithAuthoritativeNudeOutfits();
        var casAssets = new[]
        {
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfAccessory_Test", "Accessory", "body.package", new ResourceKeyRecord(0x034AEECB, 0, 0x6000000000000000ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "accessory", Description: "slot=Accessory | species=Human | age=Young Adult | gender=Female")
        };
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = bytes
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], casAssets));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.DoesNotContain(graph.SimGraph!.BodyCandidates, candidate => candidate.Label == "Accessory");
    }

    [Fact]
    public async Task AssetGraphBuilder_DoesNotUseArchetypeCompatibilityBodyFallback_WithoutAuthoritativeBodyDrivingOutfit()
    {
        var packagePath = Path.Combine(tempRoot, "sim-body-archetype-fallback.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Adult | Female [00000001]",
            Description = "SimInfo v32 | species=Human | age=Adult | gender=Female | outfits=0 categories / 0 entries / 0 parts | traits=0"
        };
        var bytes = CreateSyntheticSimInfoHeaderOnlyBytes(ageFlags: 0x00000020u, genderFlags: 0x00002000u, speciesValue: 1u);
        var casAssets = new[]
        {
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfBody_Fallback", "Full Body", "body.package", new ResourceKeyRecord(0x034AEECB, 0, 0x7100000000000000ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "full-body", Description: "slot=Full Body | species=Human | age=Adult | gender=Female"),
            new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfTop_Fallback", "Top", "body.package", new ResourceKeyRecord(0x034AEECB, 0, 0x7100000000000001ul, "CASPart"), null, 1, 1, string.Empty, new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true), CategoryNormalized: "top", Description: "slot=Top | species=Human | age=Adult | gender=Female")
        };
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = bytes,
                    [casAssets[0].RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfBody_Fallback", bodyType: 5, partFlags2: 0x04),
                    [casAssets[1].RootKey.FullTgi] = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "yfTop_Fallback", bodyType: 6)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([], casAssets));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.Empty(graph.SimGraph!.BodyCandidates);
        Assert.Equal(SimBodyAssemblyMode.None, graph.SimGraph.BodyAssembly.Mode);
        Assert.Contains(
            graph.SimGraph.BodySources,
            source => source.Label == "Body-driving outfit records" &&
                      source.Count == 0 &&
                      source.Notes.Contains("No authoritative nude/body-driving outfit record", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            graph.SimGraph.BodyAssembly.GraphNodes,
            node => node.Label == "Geometry shell" && node.State == SimBodyGraphNodeState.Unavailable);
    }

    [Fact]
    public async Task AssetGraphBuilder_DoesNotTreatSingleNonNudeOutfitAsBodyDriving()
    {
        var packagePath = Path.Combine(tempRoot, "sim-single-non-nude.package");
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Child | Male | outfits 1/1/2 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Child | gender=Male | outfits=1 categories / 1 entries / 2 parts | traits=0"
        };
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [simInfo.Key.FullTgi] = CreateSyntheticSimInfoBytesWithSingleNonNudeOutfit()
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [simInfo], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [simInfo], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.Contains(
            graph.SimGraph!.BodySources,
            source => source.Label == "Body-driving outfit records" &&
                      source.Count == 0 &&
                      source.Notes.Contains("No authoritative nude/body-driving outfit record", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(graph.SimGraph.BodyCandidates);
        Assert.Equal(SimBodyAssemblyMode.None, graph.SimGraph.BodyAssembly.Mode);
    }

    [Fact]
    public void CanonicalSceneComposer_ComposesBodyProxyPartsIntoSingleScene()
    {
        var firstResource = CreateResource(Guid.NewGuid(), "body-top.package", SourceKind.Game, "Geometry", 1, "TopGeom");
        var secondResource = CreateResource(firstResource.DataSourceId, "body-bottom.package", SourceKind.Game, "Geometry", 2, "BottomGeom");

        var firstScene = new CanonicalScene(
            "Top",
            [
                new CanonicalMesh(
                    "TopMesh",
                    [0f, 0f, 0f, 1f, 1f, 1f],
                    [],
                    [],
                    [],
                    null,
                    null,
                    0,
                    [0, 1],
                    0,
                    [new VertexWeight(0, 0, 1f)])
            ],
            [
                new CanonicalMaterial("TopMaterial", [])
            ],
            [
                new CanonicalBone("ROOT", null)
            ],
            new Bounds3D(0f, 0f, 0f, 1f, 1f, 1f));
        var secondScene = new CanonicalScene(
            "Bottom",
            [
                new CanonicalMesh(
                    "BottomMesh",
                    [2f, 2f, 2f, 3f, 3f, 3f],
                    [],
                    [],
                    [],
                    null,
                    null,
                    0,
                    [0, 1],
                    0,
                    [new VertexWeight(0, 0, 1f)])
            ],
            [
                new CanonicalMaterial("BottomMaterial", [])
            ],
            [
                new CanonicalBone("ROOT", null)
            ],
            new Bounds3D(2f, 2f, 2f, 3f, 3f, 3f));

        var composed = CanonicalSceneComposer.Compose(
            "Composed Body Proxy",
            [
                new ScenePreviewContent(firstResource, firstScene, "top diagnostic", SceneBuildStatus.SceneReady),
                new ScenePreviewContent(secondResource, secondScene, "bottom diagnostic", SceneBuildStatus.Partial)
            ]);

        Assert.NotNull(composed.Scene);
        Assert.Equal("Composed Body Proxy", composed.Scene!.Name);
        Assert.Equal(SceneBuildStatus.Partial, composed.Status);
        Assert.Equal(2, composed.Scene.Meshes.Count);
        Assert.Equal(2, composed.Scene.Materials.Count);
        Assert.Single(composed.Scene.Bones);
        Assert.Equal(0, composed.Scene.Meshes[0].MaterialIndex);
        Assert.Equal(1, composed.Scene.Meshes[1].MaterialIndex);
        Assert.Contains("top diagnostic", composed.Diagnostics, StringComparison.Ordinal);
        Assert.Contains("bottom diagnostic", composed.Diagnostics, StringComparison.Ordinal);
        Assert.Equal(0f, composed.Scene.Bounds.MinX);
        Assert.Equal(3f, composed.Scene.Bounds.MaxZ);
    }

    [Fact]
    public void SimSceneComposer_ComposesBodyAndHeadWhenCanonicalBonesOverlap()
    {
        var bodyResource = CreateResource(Guid.NewGuid(), "body.package", SourceKind.Game, "Geometry", 10, "BodyGeom");
        var headResource = CreateResource(bodyResource.DataSourceId, "head.package", SourceKind.Game, "Geometry", 11, "HeadGeom");

        var bodyScene = new CanonicalScene(
            "Body",
            [
                new CanonicalMesh(
                    "BodyMesh",
                    [0f, 0f, 0f, 0f, 1f, 0f, 1f, 0f, 0f],
                    [],
                    [],
                    [],
                    null,
                    null,
                    0,
                    [0, 1, 2],
                    0,
                    [new VertexWeight(0, 1, 1f)])
            ],
            [
                new CanonicalMaterial(
                    "BodyMaterial",
                    [
                        new CanonicalTexture("diffuse", "body_diffuse.png", TestAssets.OnePixelPng),
                        new CanonicalTexture("color_shift_mask", "body_mask.png", TestAssets.OnePixelPng)
                    ],
                    SourceKind: CanonicalMaterialSourceKind.ExplicitMatd)
            ],
            [
                new CanonicalBone("ROOT", null),
                new CanonicalBone("b__Head__", "ROOT")
            ],
            new Bounds3D(0f, 0f, 0f, 1f, 1f, 0f));
        var headScene = new CanonicalScene(
            "Head",
            [
                new CanonicalMesh(
                    "HeadMesh",
                    [0f, 1f, 0f, 0f, 2f, 0f, 1f, 1f, 0f],
                    [],
                    [],
                    [],
                    null,
                    null,
                    0,
                    [0, 1, 2],
                    0,
                    [new VertexWeight(0, 1, 1f)])
            ],
            [
                new CanonicalMaterial(
                    "HeadMaterial",
                    [
                        new CanonicalTexture("diffuse", "head_diffuse.png", TestAssets.OnePixelPng),
                        new CanonicalTexture("color_shift_mask", "head_mask.png", TestAssets.OnePixelPng)
                    ],
                    SourceKind: CanonicalMaterialSourceKind.ExplicitMatd)
            ],
            [
                new CanonicalBone("ROOT", null),
                new CanonicalBone("b__Head__", "ROOT"),
                new CanonicalBone("b__Jaw__", "b__Head__")
            ],
            new Bounds3D(0f, 1f, 0f, 1f, 2f, 0f));

        var assembled = SimSceneComposer.ComposeBodyAndHead(
            "Assembled Sim",
            new ScenePreviewContent(bodyResource, bodyScene, "body diagnostic", SceneBuildStatus.SceneReady),
            [],
            new ScenePreviewContent(headResource, headScene, "head diagnostic", SceneBuildStatus.SceneReady),
            [],
            new SimInfoSummary(
                Version: 32,
                SpeciesLabel: "Human",
                AgeLabel: "Young Adult",
                GenderLabel: "Female",
                PronounCount: 1,
                OutfitCategoryCount: 1,
                OutfitEntryCount: 1,
                OutfitPartCount: 2,
                TraitCount: 0,
                FaceModifierCount: 2,
                BodyModifierCount: 1,
                GeneticFaceModifierCount: 0,
                GeneticBodyModifierCount: 0,
                SculptCount: 1,
                GeneticSculptCount: 0,
                GeneticPartCount: 0,
                GrowthPartCount: 0,
                PeltLayerCount: 0,
                SkintoneInstanceHex: "1020304050607080",
                SkintoneShift: 0.25f),
            [
                new SimMorphGroupSummary("Face modifiers", 2, "Direct face-shape morph channels"),
                new SimMorphGroupSummary("Body modifiers", 1, "Direct body-shape morph channels")
            ],
            new SimSkintoneRenderSummary(
                "1020304050607080",
                0.25f,
                "0354796A:00000000:1020304050607080",
                "skintone.package",
                "00B2D882:00000000:1020304050607080",
                "skintone.package",
                2,
                3,
                new CanonicalColor(0.733f, 0.667f, 0.6f, 1f),
                "Resolved skintone resource and viewport tint."),
            [new CasRegionMapSummary("region_map", "AC16FBEC:00000000:0000000000009001", bodyResource.PackagePath, 2, 4, false, ["Base", "Chest"], "Body region map resolved.")],
            [new CasRegionMapSummary("region_map", "AC16FBEC:00000000:0000000000009002", headResource.PackagePath, 1, 2, false, ["Base"], "Head region map resolved.")]);

        Assert.True(assembled.IncludesHeadShell);
        Assert.NotNull(assembled.Preview.Scene);
        Assert.Equal("Assembled Sim", assembled.Preview.Scene!.Name);
        Assert.Equal(SceneBuildStatus.SceneReady, assembled.Preview.Status);
        Assert.Equal(SimAssemblyBasisKind.CanonicalBoneFallback, assembled.Plan.BasisKind);
        Assert.True(assembled.Plan.IncludesHeadShell);
        Assert.Contains(assembled.Graph.Inputs, input => input.Label == "Body shell input" && input.StatusLabel == "Resolved" && input.IsAccepted);
        Assert.Contains(assembled.Graph.Inputs, input => input.Label == "Head shell input" && input.StatusLabel == "Accepted" && input.IsAccepted);
        Assert.Contains(assembled.Graph.Inputs, input => input.Label == "Assembly basis input" && input.StatusLabel == "Fallback");
        Assert.Contains(assembled.Graph.Stages, stage => stage.Label == "Resolve body shell scene" && stage.State == SimAssemblyStageState.Resolved);
        Assert.Contains(assembled.Graph.Stages, stage => stage.Label == "Resolve head shell scene" && stage.State == SimAssemblyStageState.Resolved);
        Assert.Contains(assembled.Graph.Stages, stage => stage.Label == "Resolve assembly basis" && stage.State == SimAssemblyStageState.Approximate);
        Assert.Contains(assembled.Graph.Stages, stage => stage.Label == "Materialize torso/head payload seam" && stage.State == SimAssemblyStageState.Resolved);
        Assert.Equal("Multi-part rig payload", assembled.Graph.Payload.StatusLabel);
        Assert.Equal(2, assembled.Graph.Payload.AnchorBoneCount);
        Assert.Equal(2, assembled.Graph.Payload.AcceptedContributionCount);
        Assert.Equal(2, assembled.Graph.Payload.MergedMeshCount);
        Assert.Equal(1, assembled.Graph.Payload.RebasedWeightCount);
        Assert.Equal(2, assembled.Graph.Payload.MappedBoneReferenceCount);
        Assert.Equal(1, assembled.Graph.Payload.AddedBoneCount);
        Assert.Equal("Body shell anchor", assembled.Graph.PayloadAnchor.StatusLabel);
        Assert.Equal("Body", assembled.Graph.PayloadAnchor.SourceLabel);
        Assert.Equal(bodyResource.Key.FullTgi, assembled.Graph.PayloadAnchor.SourceResourceTgi);
        Assert.Equal(bodyResource.PackagePath, assembled.Graph.PayloadAnchor.SourcePackagePath);
        Assert.Equal(2, assembled.Graph.PayloadAnchor.BoneCount);
        Assert.Contains("ROOT", assembled.Graph.PayloadAnchor.BoneNames);
        Assert.Contains("b__Head__", assembled.Graph.PayloadAnchor.BoneNames);
        Assert.Single(assembled.Graph.PayloadBoneMaps);
        Assert.Contains(assembled.Graph.PayloadBoneMaps, boneMap => boneMap.Label == "Head shell bone map" && boneMap.SourceLabel == "Head" && boneMap.SourceResourceTgi == headResource.Key.FullTgi && boneMap.SourcePackagePath == headResource.PackagePath && boneMap.SourceBoneCount == 3 && boneMap.ReusedBoneReferenceCount == 2 && boneMap.AddedBoneCount == 1 && boneMap.RebasedWeightCount == 1 && boneMap.Entries.Count == 3);
        Assert.Equal(2, assembled.Graph.PayloadMeshBatches.Count);
        Assert.Contains(assembled.Graph.PayloadMeshBatches, batch => batch.Label == "Body shell mesh batch" && batch.SourceLabel == "Body shell contribution" && batch.SourceResourceTgi == bodyResource.Key.FullTgi && batch.SourcePackagePath == bodyResource.PackagePath && batch.MeshStartIndex == 0 && batch.MeshCount == 1 && batch.MaterialStartIndex == 0 && batch.MaterialCount == 1);
        Assert.Contains(assembled.Graph.PayloadMeshBatches, batch => batch.Label == "Head shell mesh batch" && batch.SourceLabel == "Head shell contribution" && batch.SourceResourceTgi == headResource.Key.FullTgi && batch.SourcePackagePath == headResource.PackagePath && batch.MeshStartIndex == 1 && batch.MeshCount == 1 && batch.MaterialStartIndex == 1 && batch.MaterialCount == 1);
        Assert.Equal(4, assembled.Graph.PayloadNodes.Count);
        Assert.Contains(assembled.Graph.PayloadNodes, node => node.Label == "Current payload anchor" && node.Order == 0 && node.Kind == SimAssemblyPayloadNodeKind.AnchorSkeleton && node.StatusLabel == "Body shell anchor");
        Assert.Contains(assembled.Graph.PayloadNodes, node => node.Label == "Head shell bone map" && node.Kind == SimAssemblyPayloadNodeKind.BoneRemapTable && node.Summary.Contains("source 3", StringComparison.Ordinal));
        Assert.Contains(assembled.Graph.PayloadNodes, node => node.Label == "Body shell mesh batch" && node.Kind == SimAssemblyPayloadNodeKind.MeshSet && node.Summary.Contains("meshes 0..0", StringComparison.Ordinal));
        Assert.Contains(assembled.Graph.PayloadNodes, node => node.Label == "Head shell mesh batch" && node.Kind == SimAssemblyPayloadNodeKind.MeshSet && node.Summary.Contains("materials 1..1", StringComparison.Ordinal));
        Assert.Equal("Materialized internal outcomes", assembled.Graph.Application.StatusLabel);
        Assert.Equal(2, assembled.Graph.Application.PreparedPassCount);
        Assert.Equal(0, assembled.Graph.Application.PendingPassCount);
        Assert.Equal(0, assembled.Graph.Application.UnavailablePassCount);
        Assert.Contains(assembled.Graph.ApplicationPasses, pass => pass.Label == "Skintone application" && pass.State == SimAssemblyApplicationPassState.Prepared && pass.InputCount == 1);
        Assert.Contains(assembled.Graph.ApplicationPasses, pass => pass.Label == "Morph application" && pass.State == SimAssemblyApplicationPassState.Prepared && pass.InputCount == 3);
        Assert.Equal(2, assembled.Graph.ApplicationTargets.Count);
        Assert.Contains(assembled.Graph.ApplicationTargets, target => target.Label == "Skintone material targets" && target.PassLabel == "Skintone application" && target.TargetCount == 2);
        Assert.Contains(assembled.Graph.ApplicationTargets, target => target.Label == "Morph mesh targets" && target.PassLabel == "Morph application" && target.TargetCount == 2);
        Assert.Equal(2, assembled.Graph.ApplicationPlans.Count);
        Assert.Contains(assembled.Graph.ApplicationPlans, plan => plan.Label == "Skintone material routing" && plan.PassLabel == "Skintone application" && plan.TargetCount == 2 && plan.OperationCount == 2);
        Assert.Contains(assembled.Graph.ApplicationPlans, plan => plan.Label == "Morph mesh transform planning" && plan.PassLabel == "Morph application" && plan.TargetCount == 2 && plan.OperationCount == 6);
        Assert.Equal(2, assembled.Graph.ApplicationTransforms.Count);
        Assert.Contains(assembled.Graph.ApplicationTransforms, transform => transform.Label == "Skintone routing transform" && transform.PassLabel == "Skintone application" && transform.TargetCount == 2 && transform.OperationCount == 2);
        Assert.Contains(assembled.Graph.ApplicationTransforms, transform => transform.Label == "Morph transform preparation" && transform.PassLabel == "Morph application" && transform.TargetCount == 2 && transform.OperationCount == 6);
        Assert.Equal(2, assembled.Graph.ApplicationOutcomes.Count);
        Assert.Contains(assembled.Graph.ApplicationOutcomes, outcome => outcome.Label == "Skintone routing outcome" && outcome.PassLabel == "Skintone application" && outcome.TargetCount == 2 && outcome.AppliedCount == 2);
        Assert.Contains(assembled.Graph.ApplicationOutcomes, outcome => outcome.Label == "Morph transform outcome" && outcome.PassLabel == "Morph application" && outcome.TargetCount == 2 && outcome.AppliedCount == 6);
        Assert.Equal("Resolved multi-part rig seam", assembled.Graph.Output.StatusLabel);
        Assert.Equal(2, assembled.Graph.Output.MeshCount);
        Assert.Equal(2, assembled.Graph.Output.MaterialCount);
        Assert.Equal(3, assembled.Graph.Output.BoneCount);
        Assert.Equal(2, assembled.Graph.Output.AcceptedContributionCount);
        Assert.Equal(2, assembled.Graph.Output.AcceptedInputs.Count);
        Assert.Contains(assembled.Graph.Output.AcceptedInputs, input => input.Label == "Body shell seam input" && input.StatusLabel == "Anchor" && input.SourceResourceTgi == bodyResource.Key.FullTgi && input.MeshStartIndex == 0 && input.MeshCount == 1 && input.MaterialStartIndex == 0 && input.MaterialCount == 1);
        Assert.Contains(assembled.Graph.Output.AcceptedInputs, input => input.Label == "Head shell seam input" && input.StatusLabel == "Merged" && input.SourceResourceTgi == headResource.Key.FullTgi && input.MeshStartIndex == 1 && input.MeshCount == 1 && input.MaterialStartIndex == 1 && input.MaterialCount == 1 && input.RebasedWeightCount == 1 && input.AddedBoneCount == 1);
        Assert.Contains(assembled.Graph.Contributions, contribution => contribution.Label == "Body shell contribution" && contribution.StatusLabel == "Anchor" && contribution.MeshCount == 1 && contribution.MaterialCount == 1 && contribution.BoneCount == 2);
        Assert.Contains(assembled.Graph.Contributions, contribution => contribution.Label == "Head shell contribution" && contribution.StatusLabel == "Merged" && contribution.MeshCount == 1 && contribution.MaterialCount == 1 && contribution.BoneCount == 3 && contribution.RebasedWeightCount == 1 && contribution.AddedBoneCount == 1);
        Assert.Contains("skeletal anchor", assembled.Preview.Diagnostics, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(assembled.Graph.Nodes, node => node.Label == "Body shell scene" && node.State == SimBodyGraphNodeState.Resolved);
        Assert.Contains(assembled.Graph.Nodes, node => node.Label == "Head shell scene" && node.State == SimBodyGraphNodeState.Resolved);
        Assert.Contains(assembled.Graph.Nodes, node => node.Label == "Assembly basis" && node.State == SimBodyGraphNodeState.Approximate);
        Assert.Contains(assembled.Graph.Nodes, node => node.Label == "Torso/head payload seam" && node.State == SimBodyGraphNodeState.Resolved);
        Assert.Equal(2, assembled.Preview.Scene.Meshes.Count);
        Assert.Equal(2, assembled.Preview.Scene.Materials.Count);
        Assert.Equal(3, assembled.Preview.Scene.Bones.Count);
        Assert.Equal(CanonicalMaterialSourceKind.ApproximateCas, assembled.Preview.Scene.Materials[0].SourceKind);
        Assert.Equal(CanonicalMaterialSourceKind.ApproximateCas, assembled.Preview.Scene.Materials[1].SourceKind);
        Assert.NotNull(assembled.Preview.Scene.Materials[0].ViewportTintColor);
        Assert.NotNull(assembled.Preview.Scene.Materials[1].ViewportTintColor);
        Assert.Contains("Sim skintone route 1020304050607080", assembled.Preview.Scene.Materials[0].Approximation, StringComparison.Ordinal);
        Assert.Contains("region_map", assembled.Preview.Scene.Materials[0].Approximation, StringComparison.Ordinal);
        Assert.Contains("Sim skintone route 1020304050607080", assembled.Preview.Scene.Materials[1].Approximation, StringComparison.Ordinal);
        Assert.Contains("Applied region-map-aware skintone routing outcome", assembled.Preview.Diagnostics, StringComparison.Ordinal);
        Assert.Contains("Applied internal morph transform outcome", assembled.Preview.Diagnostics, StringComparison.Ordinal);
        Assert.Contains("Assembled Sim body/head scene using 2 shared canonical bone(s)", assembled.Preview.Diagnostics, StringComparison.Ordinal);
    }

    [Fact]
    public void SimSceneComposer_AppliesSkintoneRoutingToApproximateCasMaterialsWhenRegionMapsExist()
    {
        var bodyResource = CreateResource(Guid.NewGuid(), "body.package", SourceKind.Game, "Geometry", 12, "BodyGeom");
        var headResource = CreateResource(bodyResource.DataSourceId, "head.package", SourceKind.Game, "Geometry", 13, "HeadGeom");

        var bodyScene = new CanonicalScene(
            "Body",
            [
                new CanonicalMesh(
                    "BodyMesh",
                    [0f, 0f, 0f, 0f, 1f, 0f, 1f, 0f, 0f],
                    [],
                    [],
                    [],
                    null,
                    null,
                    0,
                    [0, 1, 2],
                    0,
                    [new VertexWeight(0, 1, 1f)])
            ],
            [
                new CanonicalMaterial(
                    "ApproximateBody",
                    [new CanonicalTexture("diffuse", "body_diffuse.png", TestAssets.OnePixelPng)],
                    SourceKind: CanonicalMaterialSourceKind.ApproximateCas)
            ],
            [
                new CanonicalBone("ROOT", null),
                new CanonicalBone("b__Head__", "ROOT")
            ],
            new Bounds3D(0f, 0f, 0f, 1f, 1f, 0f));
        var headScene = new CanonicalScene(
            "Head",
            [
                new CanonicalMesh(
                    "HeadMesh",
                    [0f, 1f, 0f, 0f, 2f, 0f, 1f, 1f, 0f],
                    [],
                    [],
                    [],
                    null,
                    null,
                    0,
                    [0, 1, 2],
                    0,
                    [new VertexWeight(0, 1, 1f)])
            ],
            [
                new CanonicalMaterial(
                    "ApproximateHead",
                    [new CanonicalTexture("diffuse", "head_diffuse.png", TestAssets.OnePixelPng)],
                    SourceKind: CanonicalMaterialSourceKind.ApproximateCas)
            ],
            [
                new CanonicalBone("ROOT", null),
                new CanonicalBone("b__Head__", "ROOT"),
                new CanonicalBone("b__Jaw__", "b__Head__")
            ],
            new Bounds3D(0f, 1f, 0f, 1f, 2f, 0f));

        var assembled = SimSceneComposer.ComposeBodyAndHead(
            "Assembled Sim",
            new ScenePreviewContent(bodyResource, bodyScene, "body diagnostic", SceneBuildStatus.SceneReady),
            [],
            new ScenePreviewContent(headResource, headScene, "head diagnostic", SceneBuildStatus.SceneReady),
            [],
            new SimInfoSummary(
                Version: 32,
                SpeciesLabel: "Human",
                AgeLabel: "Young Adult",
                GenderLabel: "Male",
                PronounCount: 0,
                OutfitCategoryCount: 1,
                OutfitEntryCount: 1,
                OutfitPartCount: 2,
                TraitCount: 0,
                FaceModifierCount: 0,
                BodyModifierCount: 0,
                GeneticFaceModifierCount: 0,
                GeneticBodyModifierCount: 0,
                SculptCount: 0,
                GeneticSculptCount: 0,
                GeneticPartCount: 0,
                GrowthPartCount: 0,
                PeltLayerCount: 0,
                SkintoneInstanceHex: "1020304050607080",
                SkintoneShift: 0.15f),
            [],
            new SimSkintoneRenderSummary(
                "1020304050607080",
                0.15f,
                "0354796A:00000000:1020304050607080",
                "skintone.package",
                "00B2D882:00000000:1020304050607080",
                "skintone.package",
                2,
                3,
                new CanonicalColor(0.72f, 0.63f, 0.55f, 1f),
                "Resolved skintone resource and viewport tint."),
            [new CasRegionMapSummary("region_map", "AC16FBEC:00000000:0000000000009001", bodyResource.PackagePath, 2, 4, false, ["Base", "Chest"], "Body region map resolved.")],
            [new CasRegionMapSummary("region_map", "AC16FBEC:00000000:0000000000009002", headResource.PackagePath, 1, 2, false, ["Base"], "Head region map resolved.")]);

        Assert.NotNull(assembled.Preview.Scene);
        Assert.Equal(2, assembled.Preview.Scene!.Materials.Count);
        Assert.All(assembled.Preview.Scene.Materials, material =>
        {
            Assert.Equal(CanonicalMaterialSourceKind.ApproximateCas, material.SourceKind);
            Assert.NotNull(material.ViewportTintColor);
            Assert.Contains("Sim skintone route 1020304050607080", material.Approximation, StringComparison.Ordinal);
            Assert.Contains("region_map", material.Approximation, StringComparison.Ordinal);
        });
        Assert.Contains("Applied region-map-aware skintone routing outcome", assembled.Preview.Diagnostics, StringComparison.Ordinal);
    }

    [Fact]
    public void SimSceneComposer_WithholdsHeadWhenCanonicalBonesDoNotOverlap()
    {
        var bodyResource = CreateResource(Guid.NewGuid(), "body.package", SourceKind.Game, "Geometry", 20, "BodyGeom");
        var headResource = CreateResource(bodyResource.DataSourceId, "head.package", SourceKind.Game, "Geometry", 21, "HeadGeom");

        var bodyScene = new CanonicalScene(
            "Body",
            [
                new CanonicalMesh(
                    "BodyMesh",
                    [0f, 0f, 0f, 0f, 1f, 0f, 1f, 0f, 0f],
                    [],
                    [],
                    [],
                    null,
                    null,
                    0,
                    [0, 1, 2],
                    0,
                    [new VertexWeight(0, 0, 1f)])
            ],
            [new CanonicalMaterial("BodyMaterial", [])],
            [
                new CanonicalBone("b__ROOT__", null)
            ],
            new Bounds3D(0f, 0f, 0f, 1f, 1f, 0f));
        var headScene = new CanonicalScene(
            "Head",
            [
                new CanonicalMesh(
                    "HeadMesh",
                    [0f, 1f, 0f, 0f, 2f, 0f, 1f, 1f, 0f],
                    [],
                    [],
                    [],
                    null,
                    null,
                    0,
                    [0, 1, 2],
                    0,
                    [new VertexWeight(0, 0, 1f)])
            ],
            [new CanonicalMaterial("HeadMaterial", [])],
            [
                new CanonicalBone("b__FACE_ROOT__", null)
            ],
            new Bounds3D(0f, 1f, 0f, 1f, 2f, 0f));

        var assembled = SimSceneComposer.ComposeBodyAndHead(
            "Assembled Sim",
            new ScenePreviewContent(bodyResource, bodyScene, "body diagnostic", SceneBuildStatus.SceneReady),
            [],
            new ScenePreviewContent(headResource, headScene, "head diagnostic", SceneBuildStatus.SceneReady),
            []);

        Assert.False(assembled.IncludesHeadShell);
        Assert.NotNull(assembled.Preview.Scene);
        Assert.Equal("Assembled Sim", assembled.Preview.Scene!.Name);
        Assert.Equal(SceneBuildStatus.Partial, assembled.Preview.Status);
        Assert.Equal(SimAssemblyBasisKind.BodyOnly, assembled.Plan.BasisKind);
        Assert.False(assembled.Plan.IncludesHeadShell);
        Assert.Contains(assembled.Graph.Inputs, input => input.Label == "Head shell input" && input.StatusLabel == "Withheld" && !input.IsAccepted);
        Assert.Contains(assembled.Graph.Inputs, input => input.Label == "Assembly basis input" && input.StatusLabel == "Body-only");
        Assert.Contains(assembled.Graph.Stages, stage => stage.Label == "Resolve assembly basis" && stage.State == SimAssemblyStageState.Pending);
        Assert.Contains(assembled.Graph.Stages, stage => stage.Label == "Materialize torso/head payload seam" && stage.State == SimAssemblyStageState.Resolved);
        Assert.Equal("Anchor-only rig payload", assembled.Graph.Payload.StatusLabel);
        Assert.Equal(1, assembled.Graph.Payload.AnchorBoneCount);
        Assert.Equal(1, assembled.Graph.Payload.AcceptedContributionCount);
        Assert.Equal(1, assembled.Graph.Payload.MergedMeshCount);
        Assert.Equal(0, assembled.Graph.Payload.RebasedWeightCount);
        Assert.Equal(0, assembled.Graph.Payload.MappedBoneReferenceCount);
        Assert.Equal(0, assembled.Graph.Payload.AddedBoneCount);
        Assert.Equal("Body shell anchor", assembled.Graph.PayloadAnchor.StatusLabel);
        Assert.Equal("Body", assembled.Graph.PayloadAnchor.SourceLabel);
        Assert.Equal(bodyResource.Key.FullTgi, assembled.Graph.PayloadAnchor.SourceResourceTgi);
        Assert.Equal(bodyResource.PackagePath, assembled.Graph.PayloadAnchor.SourcePackagePath);
        Assert.Equal(1, assembled.Graph.PayloadAnchor.BoneCount);
        Assert.Contains("b__ROOT__", assembled.Graph.PayloadAnchor.BoneNames);
        Assert.Empty(assembled.Graph.PayloadBoneMaps);
        Assert.Single(assembled.Graph.PayloadMeshBatches);
        Assert.Contains(assembled.Graph.PayloadMeshBatches, batch => batch.Label == "Body shell mesh batch" && batch.SourceResourceTgi == bodyResource.Key.FullTgi && batch.MeshStartIndex == 0 && batch.MeshCount == 1 && batch.MaterialStartIndex == 0 && batch.MaterialCount == 1);
        Assert.Equal(2, assembled.Graph.PayloadNodes.Count);
        Assert.Contains(assembled.Graph.PayloadNodes, node => node.Label == "Current payload anchor" && node.Order == 0 && node.Kind == SimAssemblyPayloadNodeKind.AnchorSkeleton && node.StatusLabel == "Body shell anchor");
        Assert.Contains(assembled.Graph.PayloadNodes, node => node.Label == "Body shell mesh batch" && node.Order == 1 && node.Kind == SimAssemblyPayloadNodeKind.MeshSet);
        Assert.Equal("Pending authoritative inputs", assembled.Graph.Application.StatusLabel);
        Assert.Equal(0, assembled.Graph.Application.PreparedPassCount);
        Assert.Equal(2, assembled.Graph.Application.PendingPassCount);
        Assert.Contains(assembled.Graph.ApplicationPasses, pass => pass.Label == "Skintone application" && pass.State == SimAssemblyApplicationPassState.Pending && pass.InputCount == 0);
        Assert.Contains(assembled.Graph.ApplicationPasses, pass => pass.Label == "Morph application" && pass.State == SimAssemblyApplicationPassState.Pending && pass.InputCount == 0);
        Assert.Empty(assembled.Graph.ApplicationTargets);
        Assert.Empty(assembled.Graph.ApplicationPlans);
        Assert.Empty(assembled.Graph.ApplicationTransforms);
        Assert.Empty(assembled.Graph.ApplicationOutcomes);
        Assert.Equal("Resolved anchor-only rig seam", assembled.Graph.Output.StatusLabel);
        Assert.Equal(1, assembled.Graph.Output.MeshCount);
        Assert.Equal(1, assembled.Graph.Output.AcceptedContributionCount);
        Assert.Single(assembled.Graph.Output.AcceptedInputs);
        Assert.Contains(assembled.Graph.Output.AcceptedInputs, input => input.Label == "Body shell seam input" && input.StatusLabel == "Anchor" && input.SourceResourceTgi == bodyResource.Key.FullTgi && input.MeshStartIndex == 0 && input.MeshCount == 1 && input.MaterialStartIndex == 0 && input.MaterialCount == 1);
        Assert.Single(assembled.Graph.Contributions);
        Assert.Contains(assembled.Graph.Contributions, contribution => contribution.Label == "Body shell contribution" && contribution.StatusLabel == "Anchor" && contribution.MeshCount == 1 && contribution.MaterialCount == 1 && contribution.BoneCount == 1 && contribution.RebasedWeightCount == 0 && contribution.AddedBoneCount == 0);
        Assert.Contains(assembled.Graph.Nodes, node => node.Label == "Head shell scene" && node.State == SimBodyGraphNodeState.Approximate);
        Assert.Contains(assembled.Graph.Nodes, node => node.Label == "Assembly basis" && node.State == SimBodyGraphNodeState.Pending);
        Assert.Contains(assembled.Graph.Nodes, node => node.Label == "Torso/head payload seam" && node.State == SimBodyGraphNodeState.Resolved);
        Assert.Single(assembled.Preview.Scene.Meshes);
        Assert.Single(assembled.Preview.Scene.Materials);
        Assert.Single(assembled.Preview.Scene.Bones);
        Assert.Contains("do not share any canonical bone names", assembled.Preview.Diagnostics, StringComparison.Ordinal);
    }

    [Fact]
    public void SimSceneComposer_ComposesBodyAndHeadFromSharedRigResourceWhenBoneOverlapIsInconclusive()
    {
        var sourceId = Guid.NewGuid();
        var bodyResource = CreateResource(sourceId, "body.package", SourceKind.Game, "Geometry", 25, "BodyGeom");
        var headResource = CreateResource(sourceId, "head.package", SourceKind.Game, "Geometry", 26, "HeadGeom");
        var sharedRig = CreateResource(sourceId, "shared.package", SourceKind.Game, "Rig", 250, "SharedRig") with
        {
            Key = new ResourceKeyRecord((uint)ResourceType.Rig, 250, 0xABCDEF0011223344ul, "Rig")
        };

        var bodyScene = new CanonicalScene(
            "Body",
            [
                new CanonicalMesh(
                    "BodyMesh",
                    [0f, 0f, 0f, 0f, 1f, 0f, 1f, 0f, 0f],
                    [],
                    [],
                    [],
                    null,
                    null,
                    0,
                    [0, 1, 2],
                    0,
                    [new VertexWeight(0, 0, 1f)])
            ],
            [new CanonicalMaterial("BodyMaterial", [])],
            [
                new CanonicalBone("b__ROOT__", null)
            ],
            new Bounds3D(0f, 0f, 0f, 1f, 1f, 0f));
        var headScene = new CanonicalScene(
            "Head",
            [
                new CanonicalMesh(
                    "HeadMesh",
                    [0f, 1f, 0f, 0f, 2f, 0f, 1f, 1f, 0f],
                    [],
                    [],
                    [],
                    null,
                    null,
                    0,
                    [0, 1, 2],
                    0,
                    [new VertexWeight(0, 0, 1f)])
            ],
            [new CanonicalMaterial("HeadMaterial", [])],
            [
                new CanonicalBone("b__FACE_ROOT__", null)
            ],
            new Bounds3D(0f, 1f, 0f, 1f, 2f, 0f));

        var assembled = SimSceneComposer.ComposeBodyAndHead(
            "Assembled Sim",
            new ScenePreviewContent(bodyResource, bodyScene, "body diagnostic", SceneBuildStatus.SceneReady),
            [sharedRig],
            new ScenePreviewContent(headResource, headScene, "head diagnostic", SceneBuildStatus.SceneReady),
            [sharedRig]);

        Assert.True(assembled.IncludesHeadShell);
        Assert.NotNull(assembled.Preview.Scene);
        Assert.Equal(SimAssemblyBasisKind.SharedRigResource, assembled.Plan.BasisKind);
        Assert.Contains(assembled.Graph.Inputs, input => input.Label == "Assembly basis input" && input.StatusLabel == "Authoritative" && input.IsAccepted);
        Assert.Contains(assembled.Graph.Stages, stage => stage.Label == "Resolve assembly basis" && stage.State == SimAssemblyStageState.Resolved);
        Assert.Contains(assembled.Graph.Stages, stage => stage.Label == "Materialize torso/head payload seam" && stage.State == SimAssemblyStageState.Resolved);
        Assert.Equal("Multi-part rig payload", assembled.Graph.Payload.StatusLabel);
        Assert.Equal(1, assembled.Graph.Payload.AnchorBoneCount);
        Assert.Equal(2, assembled.Graph.Payload.AcceptedContributionCount);
        Assert.Equal(2, assembled.Graph.Payload.MergedMeshCount);
        Assert.Equal(1, assembled.Graph.Payload.RebasedWeightCount);
        Assert.Equal(0, assembled.Graph.Payload.MappedBoneReferenceCount);
        Assert.Equal(1, assembled.Graph.Payload.AddedBoneCount);
        Assert.Equal("Body shell anchor", assembled.Graph.PayloadAnchor.StatusLabel);
        Assert.Equal("Body", assembled.Graph.PayloadAnchor.SourceLabel);
        Assert.Equal(bodyResource.Key.FullTgi, assembled.Graph.PayloadAnchor.SourceResourceTgi);
        Assert.Equal(bodyResource.PackagePath, assembled.Graph.PayloadAnchor.SourcePackagePath);
        Assert.Equal(1, assembled.Graph.PayloadAnchor.BoneCount);
        Assert.Contains("b__ROOT__", assembled.Graph.PayloadAnchor.BoneNames);
        Assert.Single(assembled.Graph.PayloadBoneMaps);
        Assert.Contains(assembled.Graph.PayloadBoneMaps, boneMap => boneMap.Label == "Head shell bone map" && boneMap.SourceLabel == "Head" && boneMap.SourceResourceTgi == headResource.Key.FullTgi && boneMap.SourcePackagePath == headResource.PackagePath && boneMap.SourceBoneCount == 1 && boneMap.ReusedBoneReferenceCount == 0 && boneMap.AddedBoneCount == 1 && boneMap.RebasedWeightCount == 1 && boneMap.Entries.Count == 1);
        Assert.Equal(2, assembled.Graph.PayloadMeshBatches.Count);
        Assert.Contains(assembled.Graph.PayloadMeshBatches, batch => batch.Label == "Body shell mesh batch" && batch.SourceResourceTgi == bodyResource.Key.FullTgi && batch.MeshStartIndex == 0 && batch.MeshCount == 1 && batch.MaterialStartIndex == 0 && batch.MaterialCount == 1);
        Assert.Contains(assembled.Graph.PayloadMeshBatches, batch => batch.Label == "Head shell mesh batch" && batch.SourceResourceTgi == headResource.Key.FullTgi && batch.MeshStartIndex == 1 && batch.MeshCount == 1 && batch.MaterialStartIndex == 1 && batch.MaterialCount == 1);
        Assert.Equal(4, assembled.Graph.PayloadNodes.Count);
        Assert.Contains(assembled.Graph.PayloadNodes, node => node.Label == "Current payload anchor" && node.Kind == SimAssemblyPayloadNodeKind.AnchorSkeleton && node.StatusLabel == "Body shell anchor");
        Assert.Contains(assembled.Graph.PayloadNodes, node => node.Label == "Head shell bone map" && node.Kind == SimAssemblyPayloadNodeKind.BoneRemapTable && node.Summary.Contains("added 1", StringComparison.Ordinal));
        Assert.Contains(assembled.Graph.PayloadNodes, node => node.Label == "Head shell mesh batch" && node.Kind == SimAssemblyPayloadNodeKind.MeshSet && node.Summary.Contains("materials 1..1", StringComparison.Ordinal));
        Assert.Equal("Resolved multi-part rig seam", assembled.Graph.Output.StatusLabel);
        Assert.Equal(2, assembled.Graph.Output.AcceptedContributionCount);
        Assert.Equal(2, assembled.Graph.Output.AcceptedInputs.Count);
        Assert.Contains(assembled.Graph.Output.AcceptedInputs, input => input.Label == "Body shell seam input" && input.StatusLabel == "Anchor" && input.SourceResourceTgi == bodyResource.Key.FullTgi && input.MeshStartIndex == 0 && input.MeshCount == 1);
        Assert.Contains(assembled.Graph.Output.AcceptedInputs, input => input.Label == "Head shell seam input" && input.StatusLabel == "Merged" && input.SourceResourceTgi == headResource.Key.FullTgi && input.MeshStartIndex == 1 && input.MeshCount == 1 && input.RebasedWeightCount == 1 && input.AddedBoneCount == 1);
        Assert.Contains("rig-centered", assembled.Graph.Output.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(assembled.Graph.Contributions, contribution => contribution.Label == "Body shell contribution" && contribution.StatusLabel == "Anchor" && contribution.BoneCount == 1);
        Assert.Contains(assembled.Graph.Contributions, contribution => contribution.Label == "Head shell contribution" && contribution.StatusLabel == "Merged" && contribution.MeshCount == 1 && contribution.RebasedWeightCount == 1 && contribution.AddedBoneCount == 1);
        Assert.Contains(assembled.Graph.Nodes, node => node.Label == "Assembly basis" && node.State == SimBodyGraphNodeState.Resolved);
        Assert.Contains(assembled.Graph.Nodes, node => node.Label == "Torso/head payload seam" && node.State == SimBodyGraphNodeState.Resolved);
        Assert.Equal(2, assembled.Preview.Scene!.Meshes.Count);
        Assert.Contains("shared rig resource", assembled.Preview.Diagnostics, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SimSceneComposer_WithholdsHeadWhenRigResourcesDoNotMatchEvenIfBonesOverlap()
    {
        var sourceId = Guid.NewGuid();
        var bodyResource = CreateResource(sourceId, "body.package", SourceKind.Game, "Geometry", 30, "BodyGeom");
        var headResource = CreateResource(sourceId, "head.package", SourceKind.Game, "Geometry", 31, "HeadGeom");
        var bodyRig = CreateResource(sourceId, "body.package", SourceKind.Game, "Rig", 300, "BodyRig") with
        {
            Key = new ResourceKeyRecord((uint)ResourceType.Rig, 300, 0x1111111111111111ul, "Rig")
        };
        var headRig = CreateResource(sourceId, "head.package", SourceKind.Game, "Rig", 301, "HeadRig") with
        {
            Key = new ResourceKeyRecord((uint)ResourceType.Rig, 301, 0x2222222222222222ul, "Rig")
        };

        var bodyScene = new CanonicalScene(
            "Body",
            [
                new CanonicalMesh(
                    "BodyMesh",
                    [0f, 0f, 0f, 0f, 1f, 0f, 1f, 0f, 0f],
                    [],
                    [],
                    [],
                    null,
                    null,
                    0,
                    [0, 1, 2],
                    0,
                    [new VertexWeight(0, 1, 1f)])
            ],
            [new CanonicalMaterial("BodyMaterial", [])],
            [
                new CanonicalBone("ROOT", null),
                new CanonicalBone("b__Head__", "ROOT")
            ],
            new Bounds3D(0f, 0f, 0f, 1f, 1f, 0f));
        var headScene = new CanonicalScene(
            "Head",
            [
                new CanonicalMesh(
                    "HeadMesh",
                    [0f, 1f, 0f, 0f, 2f, 0f, 1f, 1f, 0f],
                    [],
                    [],
                    [],
                    null,
                    null,
                    0,
                    [0, 1, 2],
                    0,
                    [new VertexWeight(0, 1, 1f)])
            ],
            [new CanonicalMaterial("HeadMaterial", [])],
            [
                new CanonicalBone("ROOT", null),
                new CanonicalBone("b__Head__", "ROOT")
            ],
            new Bounds3D(0f, 1f, 0f, 1f, 2f, 0f));

        var assembled = SimSceneComposer.ComposeBodyAndHead(
            "Assembled Sim",
            new ScenePreviewContent(bodyResource, bodyScene, "body diagnostic", SceneBuildStatus.SceneReady),
            [bodyRig],
            new ScenePreviewContent(headResource, headScene, "head diagnostic", SceneBuildStatus.SceneReady),
            [headRig]);

        Assert.False(assembled.IncludesHeadShell);
        Assert.NotNull(assembled.Preview.Scene);
        Assert.Equal(SimAssemblyBasisKind.BodyOnly, assembled.Plan.BasisKind);
        Assert.Contains(assembled.Graph.Inputs, input => input.Label == "Assembly basis input" && input.StatusLabel == "Body-only" && input.IsAccepted);
        Assert.Contains(assembled.Graph.Stages, stage => stage.Label == "Resolve assembly basis" && stage.State == SimAssemblyStageState.Pending);
        Assert.Contains(assembled.Graph.Stages, stage => stage.Label == "Materialize torso/head payload seam" && stage.State == SimAssemblyStageState.Resolved);
        Assert.Equal("Anchor-only rig payload", assembled.Graph.Payload.StatusLabel);
        Assert.Equal(2, assembled.Graph.Payload.AnchorBoneCount);
        Assert.Equal(1, assembled.Graph.Payload.AcceptedContributionCount);
        Assert.Equal(1, assembled.Graph.Payload.MergedMeshCount);
        Assert.Equal(0, assembled.Graph.Payload.RebasedWeightCount);
        Assert.Equal(0, assembled.Graph.Payload.MappedBoneReferenceCount);
        Assert.Equal(0, assembled.Graph.Payload.AddedBoneCount);
        Assert.Equal("Body shell anchor", assembled.Graph.PayloadAnchor.StatusLabel);
        Assert.Equal("Body", assembled.Graph.PayloadAnchor.SourceLabel);
        Assert.Equal(bodyResource.Key.FullTgi, assembled.Graph.PayloadAnchor.SourceResourceTgi);
        Assert.Equal(bodyResource.PackagePath, assembled.Graph.PayloadAnchor.SourcePackagePath);
        Assert.Equal(2, assembled.Graph.PayloadAnchor.BoneCount);
        Assert.Contains("ROOT", assembled.Graph.PayloadAnchor.BoneNames);
        Assert.Contains("b__Head__", assembled.Graph.PayloadAnchor.BoneNames);
        Assert.Empty(assembled.Graph.PayloadBoneMaps);
        Assert.Single(assembled.Graph.PayloadMeshBatches);
        Assert.Contains(assembled.Graph.PayloadMeshBatches, batch => batch.Label == "Body shell mesh batch" && batch.SourceResourceTgi == bodyResource.Key.FullTgi && batch.MeshStartIndex == 0 && batch.MeshCount == 1 && batch.MaterialStartIndex == 0 && batch.MaterialCount == 1);
        Assert.Equal(2, assembled.Graph.PayloadNodes.Count);
        Assert.Contains(assembled.Graph.PayloadNodes, node => node.Label == "Current payload anchor" && node.Kind == SimAssemblyPayloadNodeKind.AnchorSkeleton && node.StatusLabel == "Body shell anchor");
        Assert.Contains(assembled.Graph.PayloadNodes, node => node.Label == "Body shell mesh batch" && node.Kind == SimAssemblyPayloadNodeKind.MeshSet);
        Assert.Equal("Resolved anchor-only rig seam", assembled.Graph.Output.StatusLabel);
        Assert.Equal(1, assembled.Graph.Output.AcceptedContributionCount);
        Assert.Single(assembled.Graph.Output.AcceptedInputs);
        Assert.Contains(assembled.Graph.Output.AcceptedInputs, input => input.Label == "Body shell seam input" && input.StatusLabel == "Anchor" && input.SourceResourceTgi == bodyResource.Key.FullTgi && input.MeshStartIndex == 0 && input.MeshCount == 1);
        Assert.Single(assembled.Graph.Contributions);
        Assert.Contains(assembled.Graph.Contributions, contribution => contribution.Label == "Body shell contribution" && contribution.StatusLabel == "Anchor" && contribution.MeshCount == 1 && contribution.MaterialCount == 1 && contribution.BoneCount == 2);
        Assert.Contains(assembled.Graph.Nodes, node => node.Label == "Head shell scene" && node.State == SimBodyGraphNodeState.Approximate);
        Assert.Contains(assembled.Graph.Nodes, node => node.Label == "Assembly basis" && node.State == SimBodyGraphNodeState.Pending);
        Assert.Single(assembled.Preview.Scene!.Meshes);
        Assert.Contains("do not share an exact rig resource or rig instance id", assembled.Preview.Diagnostics, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildsSimMetadataGraph_WithTemplateVariations()
    {
        var sourceId = Guid.NewGuid();
        var packageA = Path.Combine(tempRoot, "sim-template-a.package");
        var packageB = Path.Combine(tempRoot, "sim-template-b.package");
        var representative = CreateResource(sourceId, packageA, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 2/3/6 | traits 4 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=2 categories / 3 entries / 6 parts | traits=4"
        };
        var sibling = CreateResource(sourceId, packageB, SourceKind.Game, "SimInfo", 2) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 1/1/3 | traits 1 [00000002]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=1 categories / 1 entries / 3 parts | traits=1"
        };
        var nonMatching = CreateResource(sourceId, packageB, SourceKind.Game, "SimInfo", 3) with
        {
            Name = "SimInfo template: Human | Adult | Male | outfits 1/1/3 | traits 1 [00000003]",
            Description = "SimInfo v32 | species=Human | age=Adult | gender=Male | outfits=1 categories / 1 entries / 3 parts | traits=1"
        };
        var bytes = CreateSyntheticSimInfoBytes();
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [representative.Key.FullTgi] = bytes,
                    [sibling.Key.FullTgi] = bytes,
                    [nonMatching.Key.FullTgi] = bytes
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([representative, sibling, nonMatching]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packageA, 0, DateTimeOffset.UtcNow, [representative], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [representative], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.Equal(2, graph.SimGraph!.TemplateOptions.Count);
        Assert.Contains(graph.SimGraph.TemplateOptions, option => option.IsRepresentative && option.RootTgi == representative.Key.FullTgi);
        Assert.Contains(graph.SimGraph.TemplateOptions, option => !option.IsRepresentative && option.RootTgi == sibling.Key.FullTgi);
        Assert.DoesNotContain(graph.SimGraph.TemplateOptions, option => option.RootTgi == nonMatching.Key.FullTgi);
    }

    [Fact]
    public async Task AssetGraphBuilder_PreservesPackageVariantsForSameSimTemplateTgi()
    {
        var sourceId = Guid.NewGuid();
        var deltaPackage = Path.Combine(tempRoot, "ClientDeltaBuild0.package");
        var fullPackage = Path.Combine(tempRoot, "ClientFullBuild0.package");
        var key = new ResourceKeyRecord(0x025ED6F4, 0, 0x05344CE2F7378208ul, "SimInfo");
        var representative = new ResourceMetadata(
            Guid.NewGuid(),
            sourceId,
            SourceKind.Game,
            deltaPackage,
            key,
            "SimInfo template: Human | Young Adult | Female [F7378208]",
            null,
            null,
            null,
            PreviewKind.Hex,
            true,
            true,
            "Sim/character seed",
            string.Empty,
            "SimInfo v38 | species=Human | age=Young Adult | gender=Female | outfits=0 categories / 0 entries / 0 parts | traits=0 | sculpts=6 | faceMods=74 | bodyMods=13 | geneticSculpts=0 | geneticFaceMods=0 | geneticBodyMods=0 | geneticParts=0 | growthParts=0 | peltLayers=0 | pronouns=5 | skintone=0000000000002F69 | skintoneShift=-0.025");
        var richerVariant = representative with
        {
            Id = Guid.NewGuid(),
            PackagePath = fullPackage,
            Name = "SimInfo template: Human | Young Adult | Female | outfits 8/1/13 [F7378208]",
            Description = "SimInfo v27 | species=Human | age=Young Adult | gender=Female | outfits=8 categories / 1 entries / 13 parts | traits=0 | sculpts=6 | faceMods=74 | bodyMods=13 | geneticSculpts=0 | geneticFaceMods=0 | geneticBodyMods=0 | geneticParts=0 | growthParts=0 | peltLayers=0 | skintone=0000000000002F69"
        };
        var bytes = CreateSyntheticSimInfoBytes();
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [representative.Key.FullTgi] = bytes
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([representative, richerVariant]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, deltaPackage, 0, DateTimeOffset.UtcNow, [representative], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [representative], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.Equal(2, graph.SimGraph!.TemplateOptions.Count);
        Assert.Contains(graph.SimGraph.TemplateOptions, option => option.PackagePath == deltaPackage && option.OutfitPartCount == 0);
        Assert.Contains(graph.SimGraph.TemplateOptions, option => option.PackagePath == fullPackage && option.OutfitPartCount == 13);
        var selected = SimTemplateSelectionPolicy.SelectPreferredTemplate(graph.SimGraph.TemplateOptions);
        Assert.NotNull(selected);
        Assert.Equal(fullPackage, selected!.PackagePath);
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildsSimGraphFromPreferredBodyDrivingTemplate()
    {
        var sourceId = Guid.NewGuid();
        var representativePackage = Path.Combine(tempRoot, "ClientDeltaBuild0.package");
        var bodyDrivingPackage = Path.Combine(tempRoot, "SimulationFullBuild0.package");
        var representative = CreateResource(sourceId, representativePackage, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female [00000001]",
            Description = "SimInfo v38 | species=Human | age=Young Adult | gender=Female | outfits=0 categories / 0 entries / 0 parts | traits=0 | sculpts=6 | faceMods=74 | bodyMods=13 | geneticSculpts=0 | geneticFaceMods=0 | geneticBodyMods=0 | geneticParts=0 | growthParts=0 | peltLayers=0 | pronouns=5 | skintone=0000000000002F69 | skintoneShift=-0.025"
        };
        var bodyDriving = CreateResource(sourceId, bodyDrivingPackage, SourceKind.Game, "SimInfo", 2) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 2/3/6 [00000002]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=2 categories / 3 entries / 6 parts | traits=4 | sculpts=6 | faceMods=74 | bodyMods=13 | geneticSculpts=0 | geneticFaceMods=0 | geneticBodyMods=0 | geneticParts=0 | growthParts=0 | peltLayers=0 | pronouns=1 | skintone=0000000000002F69 | skintoneShift=-0.125"
        };
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [representative.Key.FullTgi] = CreateSyntheticSimInfoHeaderOnlyBytes(0x00000010u, 0x00002000u, 1u),
                    [bodyDriving.Key.FullTgi] = CreateSyntheticSimInfoBytes()
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([representative, bodyDriving]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, representativePackage, 0, DateTimeOffset.UtcNow, [representative], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [representative], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.Equal(bodyDriving.Id, graph.SimGraph!.SimInfoResource.Id);
        Assert.Equal(bodyDrivingPackage, graph.SimGraph.SimInfoResource.PackagePath);
        Assert.Contains(graph.Diagnostics, message => message.Contains("Automatically selected", StringComparison.Ordinal));
        Assert.Equal(6, graph.SimGraph.Metadata.OutfitPartCount);
    }

    [Fact]
    public async Task AssetGraphBuilder_PrefersLeanIndexedDefaultBodyRecipeTemplateOverStyledVariant()
    {
        var sourceId = Guid.NewGuid();
        var representativePackage = Path.Combine(tempRoot, "ClientDeltaBuild0.package");
        var styledPackage = Path.Combine(tempRoot, "ClientFullBuild0.package");
        var leanPackage = Path.Combine(tempRoot, "SimulationFullBuild0.package");
        var representative = CreateResource(sourceId, representativePackage, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Child | Male [00000001]",
            Description = "SimInfo v38 | species=Human | age=Child | gender=Male | outfits=0 categories / 0 entries / 0 parts | traits=0"
        };
        var styled = CreateResource(sourceId, styledPackage, SourceKind.Game, "SimInfo", 2) with
        {
            Name = "SimInfo template: Human | Child | Male | outfits 12/591/256 [00000002]",
            Description = "SimInfo v21 | species=Human | age=Child | gender=Male | outfits=12 categories / 591 entries / 256 parts | traits=0"
        };
        var lean = CreateResource(sourceId, leanPackage, SourceKind.Game, "SimInfo", 3) with
        {
            Name = "SimInfo template: Human | Child | Male | outfits 8/8/82 [00000003]",
            Description = "SimInfo v21 | species=Human | age=Child | gender=Male | outfits=8 categories / 8 entries / 82 parts | traits=2"
        };
        var casCandidate = new AssetSummary(
            Guid.NewGuid(),
            sourceId,
            SourceKind.Game,
            AssetKind.Cas,
            "cmBody_Nude_Default",
            "Full Body",
            Path.Combine(tempRoot, "body.package"),
            new ResourceKeyRecord(0x034AEECB, 0, 0x7400000000000000ul, "CASPart"),
            null,
            1,
            1,
            string.Empty,
            new AssetCapabilitySnapshot(
                HasSceneRoot: true,
                HasExactGeometryCandidate: true,
                HasMaterialReferences: false,
                HasTextureReferences: false,
                HasThumbnail: false,
                HasVariants: false,
                HasIdentityMetadata: true),
            CategoryNormalized: "full-body",
            Description: "slot=Full Body | species=Human | age=Child | gender=Male | defaultBodyType=true | nakedLink=true");
        var bytesByKey = new Dictionary<string, byte[]>
        {
            [representative.Key.FullTgi] = CreateSyntheticSimInfoBytesWithoutOutfits(ageFlags: 0x00000004u, genderFlags: 0x00001000u, speciesValue: 1u),
            [$"{styledPackage}|{styled.Key.FullTgi}"] = CreateSyntheticSimInfoBytesWithoutOutfits(ageFlags: 0x00000004u, genderFlags: 0x00001000u, speciesValue: 1u),
            [$"{leanPackage}|{lean.Key.FullTgi}"] = CreateSyntheticSimInfoBytesWithoutOutfits(ageFlags: 0x00000004u, genderFlags: 0x00001000u, speciesValue: 1u)
        };
        const string archetypeKey = "sim-archetype:human|child|male";
        var simTemplateFacts = new[]
        {
            new SimTemplateFactSummary(representative.Id, representative.DataSourceId, representative.SourceKind, representative.PackagePath, representative.Key.FullTgi, archetypeKey, "Human", "Child", "Male", representative.Name!, representative.Description ?? string.Empty, 0, 0, 0, 0, 0, 0, false, 0, 0),
            new SimTemplateFactSummary(styled.Id, styled.DataSourceId, styled.SourceKind, styled.PackagePath, styled.Key.FullTgi, archetypeKey, "Human", "Child", "Male", styled.Name!, styled.Description ?? string.Empty, 12, 591, 256, 1, 63, 35, true, 0, 0),
            new SimTemplateFactSummary(lean.Id, lean.DataSourceId, lean.SourceKind, lean.PackagePath, lean.Key.FullTgi, archetypeKey, "Human", "Child", "Male", lean.Name!, lean.Description ?? string.Empty, 8, 8, 82, 1, 63, 35, true, 0, 0)
        };
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(bytesByKey, new Dictionary<string, string?>()),
            new FakeGraphIndexStore([representative, styled, lean], [casCandidate], simTemplateFacts: simTemplateFacts));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, representativePackage, 0, DateTimeOffset.UtcNow, [representative], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildPreviewGraphAsync(summary, [representative], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.Equal(lean.Id, graph.SimGraph!.SimInfoResource.Id);
        Assert.Contains(graph.Diagnostics, message => message.Contains("Automatically selected", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AssetGraphBuilder_PrefersTemplateWithAuthoritativeNudeOutfitOverRicherNonNudeVariant()
    {
        var sourceId = Guid.NewGuid();
        var representativePackage = Path.Combine(tempRoot, "ClientDeltaBuild0.package");
        var richerNonNudePackage = Path.Combine(tempRoot, "ClientFullBuild0.package");
        var bodyDrivingPackage = Path.Combine(tempRoot, "SimulationFullBuild0.package");
        var representative = CreateResource(sourceId, representativePackage, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Male [00000001]",
            Description = "SimInfo v38 | species=Human | age=Young Adult | gender=Male | outfits=0 categories / 0 entries / 0 parts | traits=0"
        };
        var richerNonNude = CreateResource(sourceId, richerNonNudePackage, SourceKind.Game, "SimInfo", 2) with
        {
            Name = "SimInfo template: Human | Young Adult | Male | outfits 8/257/3343 [00000002]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Male | outfits=8 categories / 257 entries / 3343 parts | traits=5"
        };
        var bodyDriving = CreateResource(sourceId, bodyDrivingPackage, SourceKind.Game, "SimInfo", 3) with
        {
            Name = "SimInfo template: Human | Young Adult | Male | outfits 8/8/157 [00000003]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Male | outfits=8 categories / 8 entries / 157 parts | traits=5"
        };
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [representative.Key.FullTgi] = CreateSyntheticSimInfoHeaderOnlyBytes(0x00000010u, 0x00001000u, 1u),
                    [$"{richerNonNudePackage}|{richerNonNude.Key.FullTgi}"] = CreateSyntheticSimInfoBytes(),
                    [$"{bodyDrivingPackage}|{bodyDriving.Key.FullTgi}"] = CreateSyntheticSimInfoBytesWithAuthoritativeNudeOutfits()
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([representative, richerNonNude, bodyDriving]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, representativePackage, 0, DateTimeOffset.UtcNow, [representative], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [representative], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.Equal(bodyDriving.Id, graph.SimGraph!.SimInfoResource.Id);
        Assert.Equal(bodyDrivingPackage, graph.SimGraph.SimInfoResource.PackagePath);
        Assert.Contains(
            graph.SimGraph.BodySources,
            source => source.Label == "Body-driving outfit records" && source.Count > 0);
        Assert.Contains(graph.Diagnostics, message => message.Contains("Automatically selected", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AssetGraphBuilder_InspectsLeanAuthoritativeTemplateBeyondRicherSummaryWindow()
    {
        var sourceId = Guid.NewGuid();
        var representativePackage = Path.Combine(tempRoot, "ClientDeltaBuild0.package");
        var richerNonNudePackage = Path.Combine(tempRoot, "ClientFullBuild0.package");
        var bodyDrivingPackage = Path.Combine(tempRoot, "SimulationFullBuild0.package");
        var representative = CreateResource(sourceId, representativePackage, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female [00000001]",
            Description = "SimInfo v38 | species=Human | age=Young Adult | gender=Female | outfits=0 categories / 0 entries / 0 parts | traits=5"
        };

        var resources = new List<ResourceMetadata> { representative };
        var bytesByKey = new Dictionary<string, byte[]>
        {
            [representative.Key.FullTgi] = CreateSyntheticSimInfoHeaderOnlyBytes(0x00000010u, 0x00002000u, 1u)
        };

        for (var index = 0; index < 140; index++)
        {
            var richerNonNude = CreateResource(sourceId, richerNonNudePackage, SourceKind.Game, "SimInfo", (uint)(100 + index)) with
            {
                Name = $"SimInfo template: Human | Young Adult | Female | outfits 8/{240 + index}/{3200 + index} [{(100 + index):X8}]",
                Description = $"SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=8 categories / {240 + index} entries / {3200 + index} parts | traits=5"
            };
            resources.Add(richerNonNude);
            bytesByKey[$"{richerNonNudePackage}|{richerNonNude.Key.FullTgi}"] = CreateSyntheticSimInfoBytes();
        }

        var bodyDriving = CreateResource(sourceId, bodyDrivingPackage, SourceKind.Game, "SimInfo", 1000) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 2/2/24 [000003E8]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=2 categories / 2 entries / 24 parts | traits=5"
        };
        resources.Add(bodyDriving);
        bytesByKey[$"{bodyDrivingPackage}|{bodyDriving.Key.FullTgi}"] = CreateSyntheticSimInfoBytesWithAuthoritativeNudeOutfits();

        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(bytesByKey, new Dictionary<string, string?>()),
            new FakeGraphIndexStore(resources));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, representativePackage, 0, DateTimeOffset.UtcNow, [representative], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [representative], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.Equal(bodyDriving.Id, graph.SimGraph!.SimInfoResource.Id);
        Assert.Equal(bodyDrivingPackage, graph.SimGraph.SimInfoResource.PackagePath);
        Assert.Contains(
            graph.SimGraph.BodySources,
            source => source.Label == "Body-driving outfit records" && source.Count > 0);

        var summaryOrderIndex = SimTemplateSelectionPolicy.OrderTemplates(graph.SimGraph.TemplateOptions)
            .Select((option, index) => new { option.ResourceId, index })
            .Single(option => option.ResourceId == bodyDriving.Id)
            .index;
        Assert.Equal(0, summaryOrderIndex);
    }

    [Fact]
    public async Task AssetGraphBuilder_RedirectsToPreferredPackageVariantForSameSimTemplateTgi()
    {
        var sourceId = Guid.NewGuid();
        var deltaPackage = Path.Combine(tempRoot, "ClientDeltaBuild0.package");
        var fullPackage = Path.Combine(tempRoot, "SimulationFullBuild0.package");
        var key = new ResourceKeyRecord(0x025ED6F4, 0, 0x83EC07AD2490B764ul, "SimInfo");
        var representative = new ResourceMetadata(
            Guid.NewGuid(),
            sourceId,
            SourceKind.Game,
            deltaPackage,
            key,
            "SimInfo template: Human | Young Adult | Female [2490B764]",
            null,
            null,
            null,
            PreviewKind.Hex,
            true,
            true,
            "Sim/character seed",
            string.Empty,
            "SimInfo v38 | species=Human | age=Young Adult | gender=Female | outfits=0 categories / 0 entries / 0 parts | traits=0 | sculpts=6 | faceMods=81 | bodyMods=18 | geneticSculpts=0 | geneticFaceMods=0 | geneticBodyMods=0 | geneticParts=0 | growthParts=0 | peltLayers=0 | pronouns=5 | skintone=000000000002EBFD | skintoneShift=0");
        var richerVariant = representative with
        {
            Id = Guid.NewGuid(),
            PackagePath = fullPackage,
            Description = "SimInfo v27 | species=Human | age=Young Adult | gender=Female | outfits=8 categories / 1 entries / 12 parts | traits=0 | sculpts=6 | faceMods=81 | bodyMods=18 | geneticSculpts=0 | geneticFaceMods=0 | geneticBodyMods=0 | geneticParts=0 | growthParts=0 | peltLayers=0 | skintone=000000000002EBFD"
        };
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [representative.Key.FullTgi] = CreateSyntheticSimInfoHeaderOnlyBytes(0x00000010u, 0x00002000u, 1u)
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([representative, richerVariant]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, deltaPackage, 0, DateTimeOffset.UtcNow, [representative], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [representative], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.Equal(fullPackage, graph.SimGraph!.SimInfoResource.PackagePath);
        Assert.Contains(graph.Diagnostics, message => message.Contains("Automatically selected", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AssetGraphBuilder_RedirectsToPreferredPackageVariantUsingPackageSpecificSimInfoBytes()
    {
        var sourceId = Guid.NewGuid();
        var deltaPackage = Path.Combine(tempRoot, "ClientDeltaBuild0.package");
        var fullPackage = Path.Combine(tempRoot, "SimulationFullBuild0.package");
        var key = new ResourceKeyRecord(0x025ED6F4, 0, 0x83EC07AD2490B764ul, "SimInfo");
        var representative = new ResourceMetadata(
            Guid.NewGuid(),
            sourceId,
            SourceKind.Game,
            deltaPackage,
            key,
            "SimInfo template: Human | Young Adult | Female [2490B764]",
            null,
            null,
            null,
            PreviewKind.Hex,
            true,
            true,
            "Sim/character seed",
            string.Empty,
            "SimInfo v38 | species=Human | age=Young Adult | gender=Female | outfits=0 categories / 0 entries / 0 parts | traits=0 | sculpts=6 | faceMods=81 | bodyMods=18 | geneticSculpts=0 | geneticFaceMods=0 | geneticBodyMods=0 | geneticParts=0 | growthParts=0 | peltLayers=0 | pronouns=5 | skintone=000000000002EBFD | skintoneShift=0");
        var richerVariant = representative with
        {
            Id = Guid.NewGuid(),
            PackagePath = fullPackage,
            Description = "SimInfo v38 | species=Human | age=Young Adult | gender=Female | outfits=0 categories / 0 entries / 0 parts | traits=0 | sculpts=6 | faceMods=81 | bodyMods=18 | geneticSculpts=0 | geneticFaceMods=0 | geneticBodyMods=0 | geneticParts=0 | growthParts=0 | peltLayers=0 | pronouns=5 | skintone=000000000002EBFD | skintoneShift=0"
        };
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [$"{deltaPackage}|{representative.Key.FullTgi}"] = CreateSyntheticSimInfoHeaderOnlyBytes(0x00000010u, 0x00002000u, 1u),
                    [$"{fullPackage}|{richerVariant.Key.FullTgi}"] = CreateSyntheticSimInfoBytes()
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore([representative, richerVariant]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, deltaPackage, 0, DateTimeOffset.UtcNow, [representative], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [representative], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.Equal(fullPackage, graph.SimGraph!.SimInfoResource.PackagePath);
        Assert.True(graph.SimGraph.Metadata.OutfitPartCount > 0);
        Assert.Contains(graph.Diagnostics, message => message.Contains("Automatically selected", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AssetGraphBuilder_PrefersRicherExactPackageVariantBeforeBroadTemplateSearch()
    {
        var sourceId = Guid.NewGuid();
        var deltaPackage = Path.Combine(tempRoot, "ClientDeltaBuild0.package");
        var fullPackage = Path.Combine(tempRoot, "ClientFullBuild0.package");
        var key = new ResourceKeyRecord(0x025ED6F4, 0, 0x83EC07AD2490B764ul, "SimInfo");
        var representative = new ResourceMetadata(
            Guid.NewGuid(),
            sourceId,
            SourceKind.Game,
            deltaPackage,
            key,
            "SimInfo template: Human | Young Adult | Female [2490B764]",
            null,
            null,
            null,
            PreviewKind.Hex,
            true,
            true,
            "Sim/character seed",
            string.Empty,
            "SimInfo v38 | species=Human | age=Young Adult | gender=Female | outfits=0 categories / 0 entries / 0 parts | traits=0 | sculpts=6 | faceMods=81 | bodyMods=18 | geneticSculpts=0 | geneticFaceMods=0 | geneticBodyMods=0 | geneticParts=0 | growthParts=0 | peltLayers=0 | pronouns=5 | skintone=000000000002EBFD | skintoneShift=0");
        var richerVariant = representative with
        {
            Id = Guid.NewGuid(),
            PackagePath = fullPackage,
            Name = "SimInfo template: Human | Young Adult | Female | outfits 8/1/12 [2490B764]",
            Description = "SimInfo v27 | species=Human | age=Young Adult | gender=Female | outfits=8 categories / 1 entries / 12 parts | traits=0 | sculpts=6 | faceMods=81 | bodyMods=18 | geneticSculpts=0 | geneticFaceMods=0 | geneticBodyMods=0 | geneticParts=0 | growthParts=0 | peltLayers=0 | skintone=000000000002EBFD"
        };
        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [representative.Key.FullTgi] = CreateSyntheticSimInfoBytes()
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore(
                [representative, richerVariant],
                queryResources: [representative]));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, deltaPackage, 0, DateTimeOffset.UtcNow, [representative], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [representative], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.Equal(fullPackage, graph.SimGraph!.SimInfoResource.PackagePath);
        Assert.True(graph.SimGraph.Metadata.OutfitPartCount > 0);
        Assert.Contains(graph.Diagnostics, message => message.Contains("Automatically selected", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AssetGraphBuilder_RedirectsToPreferredTemplateBeyondFirstSearchPage()
    {
        var sourceId = Guid.NewGuid();
        var deltaPackage = Path.Combine(tempRoot, "ClientDeltaBuild0.package");
        var simulationDeltaPackage = Path.Combine(tempRoot, "SimulationDeltaBuild0.package");
        var fullPackage = Path.Combine(tempRoot, "SimulationFullBuild0.package");

        var representative = CreateResource(sourceId, deltaPackage, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Young Adult | Female [00000001]",
            Description = "SimInfo v38 | species=Human | age=Young Adult | gender=Female | outfits=0 categories / 0 entries / 0 parts | traits=0 | sculpts=6 | faceMods=74 | bodyMods=13 | geneticSculpts=0 | geneticFaceMods=0 | geneticBodyMods=0 | geneticParts=0 | growthParts=0 | peltLayers=0 | pronouns=5 | skintone=0000000000002F69 | skintoneShift=-0.025"
        };

        var zeroVariants = Enumerable.Range(0, 1300)
            .Select(index => CreateResource(
                sourceId,
                index % 2 == 0 ? deltaPackage : simulationDeltaPackage,
                SourceKind.Game,
                "SimInfo",
                (uint)(0x1000 + index)) with
            {
                Name = $"SimInfo template: Human | Young Adult | Female [{(0x1000 + index):X8}]",
                Description = "SimInfo v38 | species=Human | age=Young Adult | gender=Female | outfits=0 categories / 0 entries / 0 parts | traits=0 | sculpts=6 | faceMods=74 | bodyMods=13 | geneticSculpts=0 | geneticFaceMods=0 | geneticBodyMods=0 | geneticParts=0 | growthParts=0 | peltLayers=0 | pronouns=5 | skintone=0000000000002F69 | skintoneShift=-0.025"
            })
            .ToArray();

        var bodyDriving = CreateResource(sourceId, fullPackage, SourceKind.Game, "SimInfo", 0xFFFF) with
        {
            Name = "SimInfo template: Human | Young Adult | Female | outfits 8/1/15 [0000FFFF]",
            Description = "SimInfo v32 | species=Human | age=Young Adult | gender=Female | outfits=8 categories / 1 entries / 15 parts | traits=0 | sculpts=6 | faceMods=74 | bodyMods=13 | geneticSculpts=0 | geneticFaceMods=0 | geneticBodyMods=0 | geneticParts=0 | growthParts=0 | peltLayers=0 | pronouns=1 | skintone=0000000000002F69 | skintoneShift=0"
        };

        var resources = new List<ResourceMetadata>(zeroVariants.Length + 2) { representative };
        resources.AddRange(zeroVariants);
        resources.Add(bodyDriving);

        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [representative.Key.FullTgi] = CreateSyntheticSimInfoHeaderOnlyBytes(0x00000010u, 0x00002000u, 1u),
                    [bodyDriving.Key.FullTgi] = CreateSyntheticSimInfoBytes()
                },
                new Dictionary<string, string?>()),
            new FakeGraphIndexStore(resources));
        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, deltaPackage, 0, DateTimeOffset.UtcNow, [representative], []))
            .Single(asset => asset.AssetKind == AssetKind.Sim);

        var graph = await builder.BuildAssetGraphAsync(summary, [representative], CancellationToken.None);

        Assert.NotNull(graph.SimGraph);
        Assert.Equal(bodyDriving.Id, graph.SimGraph!.SimInfoResource.Id);
        Assert.Equal(fullPackage, graph.SimGraph.SimInfoResource.PackagePath);
        Assert.True(graph.SimGraph.Metadata.OutfitPartCount > 0);
        Assert.Contains(graph.Diagnostics, message => message.Contains("Automatically selected", StringComparison.Ordinal));
    }

    [Fact]
    public void Ts4SeedMetadataExtractor_HandlesSimInfoWithTruncatedTraitTail()
    {
        var packagePath = Path.Combine(tempRoot, "siminfo-truncated.package");
        var resource = CreateResource(Guid.NewGuid(), packagePath, SourceKind.Game, "SimInfo", 1);
        var bytes = CreateSyntheticSimInfoBytes();
        Array.Resize(ref bytes, bytes.Length - 12);

        var metadata = Ts4SeedMetadataExtractor.TryExtractSimInfoSeedMetadata(resource, bytes);

        Assert.NotNull(metadata);
        Assert.Contains("Human", metadata!.DisplayName, StringComparison.Ordinal);
        Assert.Contains("Young Adult", metadata.DisplayName, StringComparison.Ordinal);
        Assert.Contains("Female", metadata.DisplayName, StringComparison.Ordinal);
        Assert.Contains("outfits=2 categories / 3 entries / 6 parts", metadata.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Ts4SeedMetadataExtractor_HandlesSimInfoWithInvalidOutfitSection()
    {
        var packagePath = Path.Combine(tempRoot, "siminfo-invalid-outfits.package");
        var resource = CreateResource(Guid.NewGuid(), packagePath, SourceKind.Game, "SimInfo", 1);
        var bytes = CreateSyntheticSimInfoBytes();
        bytes[137] = 0xFF;
        bytes[138] = 0xFF;
        bytes[139] = 0xFF;
        bytes[140] = 0xFF;

        var metadata = Ts4SeedMetadataExtractor.TryExtractSimInfoSeedMetadata(resource, bytes);

        Assert.NotNull(metadata);
        Assert.Contains("Human", metadata!.DisplayName, StringComparison.Ordinal);
        Assert.Contains("Young Adult", metadata.DisplayName, StringComparison.Ordinal);
        Assert.Contains("Female", metadata.DisplayName, StringComparison.Ordinal);
        Assert.Contains("species=Human", metadata.Description, StringComparison.Ordinal);
        Assert.Contains("age=Young Adult", metadata.Description, StringComparison.Ordinal);
        Assert.Contains("gender=Female", metadata.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void AssetGraphBuilder_HidesUnclassifiedSimInfoRowsFromArchetypeList()
    {
        var builder = new ExplicitAssetGraphBuilder(new FakeResourceCatalogService(new Dictionary<string, byte[]>(), new Dictionary<string, string?>()));
        var packagePath = Path.Combine(tempRoot, "sim-archetype-filtering.package");
        var sourceId = Guid.NewGuid();
        var classified = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 1) with
        {
            Name = "SimInfo template: Human | Adult | Male | outfits 2/3/6 | traits 4 [00000001]",
            Description = "SimInfo v32 | species=Human | age=Adult | gender=Male | outfits=2 categories / 3 entries / 6 parts | traits=4"
        };
        var unclassified = CreateResource(sourceId, packagePath, SourceKind.Game, "SimInfo", 2) with
        {
            Name = null,
            Description = null
        };

        var summaries = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, [classified, unclassified], []))
            .Where(asset => asset.AssetKind == AssetKind.Sim)
            .ToArray();

        var summary = Assert.Single(summaries);
        Assert.Equal("Sim archetype: Human | Adult | Male", summary.DisplayName);
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
    public void AssetGraphBuilder_BuildBuySummary_CollapsesSameSceneRootFamilyIntoOneLogicalAsset()
    {
        var builder = new ExplicitAssetGraphBuilder(new FakeResourceCatalogService(new Dictionary<string, byte[]>(), new Dictionary<string, string?>()));
        var packagePath = Path.Combine(tempRoot, "family.package");
        var sourceId = Guid.NewGuid();
        const string logicalRootTgi = "01661233:00000000:039E5CF151195E00";
        var objectCatalogType = (uint)ResourceType.ObjectCatalog;
        var objectDefinitionType = (uint)ResourceType.ObjectDefinition;
        var resources = new[]
        {
            CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectCatalog", 0, "Still LIfe Store Front Prop") with { Key = new ResourceKeyRecord(objectCatalogType, 0, 0x31D40, "ObjectCatalog") },
            CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectDefinition", 0, "sculptFloor_EP06GENsetStorefront14x4x5_set1") with { Key = new ResourceKeyRecord(objectDefinitionType, 0, 0x31D40, "ObjectDefinition"), SceneRootTgiHint = logicalRootTgi },
            CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectCatalog", 0, "Still LIfe Store Front Prop") with { Key = new ResourceKeyRecord(objectCatalogType, 0, 0x31D41, "ObjectCatalog") },
            CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectDefinition", 0, "sculptFloor_EP06GENsetStorefront14x4x5_set2") with { Key = new ResourceKeyRecord(objectDefinitionType, 0, 0x31D41, "ObjectDefinition"), SceneRootTgiHint = logicalRootTgi },
            CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectCatalog", 0, "Still LIfe Store Front Prop") with { Key = new ResourceKeyRecord(objectCatalogType, 0, 0x31D42, "ObjectCatalog") },
            CreateResource(sourceId, packagePath, SourceKind.Game, "ObjectDefinition", 0, "sculptFloor_EP06GENsetStorefront14x4x5_set3") with { Key = new ResourceKeyRecord(objectDefinitionType, 0, 0x31D42, "ObjectDefinition"), SceneRootTgiHint = logicalRootTgi }
        };

        var summaries = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, resources, []));

        var summary = Assert.Single(summaries.Where(asset => asset.AssetKind == AssetKind.BuildBuy));
        Assert.Equal("Still LIfe Store Front Prop", summary.DisplayName);
        Assert.Equal(3, summary.VariantCount);
        Assert.True(summary.HasVariants);
        Assert.Equal(logicalRootTgi, summary.LogicalRootTgi);
        Assert.Contains("Collapsed 3 Build/Buy identity entries", summary.Diagnostics, StringComparison.Ordinal);
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
    public void AssetGraphBuilder_CreatesGeneral3DSummariesForLooseModelRoots()
    {
        var builder = new ExplicitAssetGraphBuilder(new FakeResourceCatalogService(new Dictionary<string, byte[]>(), new Dictionary<string, string?>()));
        var packagePath = Path.Combine(tempRoot, "general3d.package");
        var sourceId = Guid.NewGuid();
        var resources = new[]
        {
            CreateResource(sourceId, packagePath, SourceKind.Game, "Model", 1, "LooseChair"),
            CreateResource(sourceId, packagePath, SourceKind.Game, "ModelLOD", 1),
            CreateResource(sourceId, packagePath, SourceKind.Game, "MaterialDefinition", 1),
            CreateResource(sourceId, packagePath, SourceKind.Game, "PNGImage", 1)
        };

        var summaries = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, resources, []));

        var summary = Assert.Single(summaries);
        Assert.Equal(AssetKind.General3D, summary.AssetKind);
        Assert.Equal("LooseChair", summary.DisplayName);
        Assert.Equal("Model", summary.RootTypeName);
        Assert.Equal("ModelLOD", summary.PrimaryGeometryType);
        Assert.Equal("General 3D", summary.Category);
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildsGeneral3DGraphForStandaloneModel()
    {
        var packagePath = Path.Combine(tempRoot, "general3d-graph.package");
        var sourceId = Guid.NewGuid();
        var model = CreateResource(sourceId, packagePath, SourceKind.Game, "Model", 1, "LooseChair");
        var modelLod = CreateResource(sourceId, packagePath, SourceKind.Game, "ModelLOD", 1);
        var geometry = CreateResource(sourceId, packagePath, SourceKind.Game, "Geometry", 1);
        var texture = CreateResource(sourceId, packagePath, SourceKind.Game, "PNGImage", 1);
        var material = CreateResource(sourceId, packagePath, SourceKind.Game, "MaterialDefinition", 1);
        var resources = new[]
        {
            model,
            modelLod,
            geometry,
            texture,
            material
        };

        var builder = new ExplicitAssetGraphBuilder(
            new FakeResourceCatalogService(
                new Dictionary<string, byte[]>
                {
                    [model.Key.FullTgi] = []
                },
                new Dictionary<string, string?>()));

        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, resources, []))
            .Single(asset => asset.AssetKind == AssetKind.General3D);
        var graph = await builder.BuildAssetGraphAsync(summary, resources, CancellationToken.None);

        Assert.NotNull(graph.General3DGraph);
        Assert.True(graph.General3DGraph!.IsSupported);
        Assert.Equal(model.Key.FullTgi, graph.General3DGraph.SceneRootResource.Key.FullTgi);
        Assert.Single(graph.General3DGraph.ModelResources);
        Assert.Single(graph.General3DGraph.ModelLodResources);
        Assert.Single(graph.General3DGraph.GeometryResources);
        Assert.Single(graph.General3DGraph.MaterialResources);
        Assert.Single(graph.General3DGraph.TextureResources);
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
    public async Task AssetGraphBuilder_BuildsExplicitCasGraph_FromDirectGeometryTgiWhenLodsAreMissing()
    {
        var packagePath = Path.Combine(tempRoot, "cas-direct-geometry-tgi.package");
        var sourceId = Guid.NewGuid();
        var casPart = CreateResource(sourceId, packagePath, SourceKind.Game, "CASPart", 1, "acAcc_NecklaceCollar");
        var geometry = CreateResource(sourceId, packagePath, SourceKind.Game, "Geometry", 1, "NecklaceGeom");
        var rig = CreateResource(sourceId, packagePath, SourceKind.Game, "Rig", 1, "AccessoryRig");
        var texture = CreateResource(sourceId, packagePath, SourceKind.Game, "PNGImage", 1);
        var thumbnail = CreateResource(sourceId, packagePath, SourceKind.Game, "CASPartThumbnail", 1);

        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [casPart.Key.FullTgi] = CreateSyntheticCasPartBytes(geometry.Key, thumbnail.Key, texture.Key, "acAcc_NecklaceCollar", version: 0x0000002Eu, bodyType: 12, includeLodReferences: false),
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
        Assert.Equal("Accessory", graph.CasGraph.Category);
        Assert.Equal(geometry.Key.FullTgi, graph.CasGraph.GeometryResource?.Key.FullTgi);
        Assert.Contains(graph.Diagnostics, message => message.Contains("using direct Geometry references from the CASPart TGI table", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AssetGraphBuilder_PreservesCasColorShiftMaskTextureRole()
    {
        var packagePath = Path.Combine(tempRoot, "cas-colorshift.package");
        var sourceId = Guid.NewGuid();
        var casPart = CreateResource(sourceId, packagePath, SourceKind.Game, "CASPart", 1, "Short Hair");
        var geometry = CreateResource(sourceId, packagePath, SourceKind.Game, "Geometry", 1, "HairGeom");
        var rig = CreateResource(sourceId, packagePath, SourceKind.Game, "Rig", 1, "HumanRig");
        var diffuse = CreateResource(sourceId, packagePath, SourceKind.Game, "PNGImage", 1);
        var thumbnail = CreateResource(sourceId, packagePath, SourceKind.Game, "CASPartThumbnail", 1);
        var colorShiftMask = CreateResource(sourceId, packagePath, SourceKind.Game, "PNGImage2", 1);

        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [casPart.Key.FullTgi] = CreateSyntheticCasPartBytes(geometry.Key, thumbnail.Key, diffuse.Key, colorShiftMaskKey: colorShiftMask.Key, version: 0x00000031u),
                [geometry.Key.FullTgi] = CreateSyntheticSkinnedGeometryBytes(),
                [rig.Key.FullTgi] = CreateSyntheticRigBytes(),
                [diffuse.Key.FullTgi] = TestAssets.OnePixelPng,
                [thumbnail.Key.FullTgi] = TestAssets.OnePixelPng,
                [colorShiftMask.Key.FullTgi] = TestAssets.OnePixelPng
            },
            new Dictionary<string, string?>());
        var builder = new ExplicitAssetGraphBuilder(catalog);
        var resources = new[] { casPart, geometry, rig, diffuse, thumbnail, colorShiftMask };

        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, resources, []))
            .Single(asset => asset.AssetKind == AssetKind.Cas);
        var graph = await builder.BuildAssetGraphAsync(summary, resources, CancellationToken.None);

        var materialTextures = graph.CasGraph!.Materials.SelectMany(static material => material.Textures).ToArray();
        Assert.Contains(materialTextures, texture => texture.Slot == "diffuse" && texture.SourceKey?.FullTgi == diffuse.Key.FullTgi);
        Assert.Contains(materialTextures, texture => texture.Slot == "color_shift_mask" && texture.SourceKey?.FullTgi == colorShiftMask.Key.FullTgi);
    }

    [Fact]
    public async Task AssetGraphBuilder_PreservesCasRegionMapSummary()
    {
        var packagePath = Path.Combine(tempRoot, "cas-regionmap.package");
        var sourceId = Guid.NewGuid();
        var casPart = CreateResource(sourceId, packagePath, SourceKind.Game, "CASPart", 1, "yfBody_Base");
        var geometry = CreateResource(sourceId, packagePath, SourceKind.Game, "Geometry", 1, "BodyGeom");
        var rig = CreateResource(sourceId, packagePath, SourceKind.Game, "Rig", 1, "HumanRig");
        var diffuse = CreateResource(sourceId, packagePath, SourceKind.Game, "PNGImage", 1);
        var thumbnail = CreateResource(sourceId, packagePath, SourceKind.Game, "CASPartThumbnail", 1);
        var regionMap = CreateResource(sourceId, packagePath, SourceKind.Game, "RegionMap", 1, "BodyRegionMap");

        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [casPart.Key.FullTgi] = CreateSyntheticCasPartBytes(geometry.Key, thumbnail.Key, diffuse.Key, regionMapKey: regionMap.Key, bodyType: 5),
                [geometry.Key.FullTgi] = CreateSyntheticSkinnedGeometryBytes(),
                [rig.Key.FullTgi] = CreateSyntheticRigBytes(),
                [diffuse.Key.FullTgi] = TestAssets.OnePixelPng,
                [thumbnail.Key.FullTgi] = TestAssets.OnePixelPng,
                [regionMap.Key.FullTgi] = CreateSyntheticRegionMapBytes()
            },
            new Dictionary<string, string?>());
        var builder = new ExplicitAssetGraphBuilder(catalog);
        var resources = new[] { casPart, geometry, rig, diffuse, thumbnail, regionMap };

        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, resources, []))
            .Single(asset => asset.AssetKind == AssetKind.Cas);
        var graph = await builder.BuildAssetGraphAsync(summary, resources, CancellationToken.None);

        Assert.NotNull(graph.CasGraph);
        var resolvedRegionMap = Assert.Single(graph.CasGraph!.RegionMaps);
        Assert.Equal(regionMap.Key.FullTgi, resolvedRegionMap.ResourceTgi);
        Assert.Equal(regionMap.PackagePath, resolvedRegionMap.PackagePath);
        Assert.Equal(2, resolvedRegionMap.EntryCount);
        Assert.True(resolvedRegionMap.LinkedKeyCount > 0);
        Assert.Contains("Chest", resolvedRegionMap.RegionLabels);
    }

    [Fact]
    public async Task AssetGraphBuilder_BuildsExplicitCasGraph_FromIndexedCrossPackageGeometryForAccessory()
    {
        var packagePath = Path.Combine(tempRoot, "cas-cross-package.package");
        var externalPackagePath = Path.Combine(tempRoot, "cas-cross-package-external.package");
        var sourceId = Guid.NewGuid();
        var casPart = CreateResource(sourceId, packagePath, SourceKind.Game, "CASPart", 1, "acAcc_NecklaceCollar");
        var thumbnail = CreateResource(sourceId, packagePath, SourceKind.Game, "CASPartThumbnail", 1);
        var geometry = CreateResource(sourceId, externalPackagePath, SourceKind.Game, "Geometry", 1, "NecklaceGeom");
        var rig = CreateResource(sourceId, externalPackagePath, SourceKind.Game, "Rig", 1, "AccessoryRig");
        var diffuse = CreateResource(sourceId, externalPackagePath, SourceKind.Game, "PNGImage", 1);
        var material = CreateResource(sourceId, externalPackagePath, SourceKind.Game, "MaterialDefinition", 1, "AccessoryMaterial");

        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [casPart.Key.FullTgi] = CreateSyntheticCasPartBytes(geometry.Key, thumbnail.Key, diffuse.Key, "acAcc_NecklaceCollar", bodyType: 12),
                [geometry.Key.FullTgi] = CreateSyntheticSkinnedGeometryBytes(),
                [rig.Key.FullTgi] = CreateSyntheticRigBytes(),
                [diffuse.Key.FullTgi] = TestAssets.OnePixelPng,
                [thumbnail.Key.FullTgi] = TestAssets.OnePixelPng,
                [material.Key.FullTgi] = []
            },
            new Dictionary<string, string?>());
        var indexStore = new FakeGraphIndexStore([geometry, rig, diffuse, material]);
        var builder = new ExplicitAssetGraphBuilder(catalog, indexStore);
        var localResources = new[] { casPart, thumbnail };

        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, localResources, []))
            .Single(asset => asset.AssetKind == AssetKind.Cas);
        var graph = await builder.BuildAssetGraphAsync(summary, localResources, CancellationToken.None);

        Assert.True(graph.CasGraph is not null, string.Join(Environment.NewLine, graph.Diagnostics));
        Assert.True(graph.CasGraph!.IsSupported, string.Join(Environment.NewLine, graph.Diagnostics));
        Assert.Equal("Accessory", graph.CasGraph.Category);
        Assert.Equal(geometry.Key.FullTgi, graph.CasGraph.GeometryResource?.Key.FullTgi);
        Assert.Contains(graph.CasGraph.RigResources, resource => resource.Key.FullTgi == rig.Key.FullTgi);
        Assert.Contains(graph.CasGraph.TextureResources, resource => resource.Key.FullTgi == diffuse.Key.FullTgi);
        Assert.Contains(graph.CasGraph.MaterialResources, resource => resource.Key.FullTgi == material.Key.FullTgi);
        Assert.Contains(graph.Diagnostics, message => message.Contains("resolved via indexed cross-package lookup", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(graph.Diagnostics, message => message.Contains("Resolved CAS geometry companions", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AssetDetailsFormatter_UsesResolvedCasGraphCapabilities_WhenSummaryIsMetadataOnly()
    {
        var packagePath = Path.Combine(tempRoot, "cas-details.package");
        var externalPackagePath = Path.Combine(tempRoot, "cas-details-external.package");
        var sourceId = Guid.NewGuid();
        var casPart = CreateResource(sourceId, packagePath, SourceKind.Game, "CASPart", 1, "acAcc_NecklaceCollar");
        var thumbnail = CreateResource(sourceId, packagePath, SourceKind.Game, "CASPartThumbnail", 1);
        var geometry = CreateResource(sourceId, externalPackagePath, SourceKind.Game, "Geometry", 1, "NecklaceGeom");
        var rig = CreateResource(sourceId, externalPackagePath, SourceKind.Game, "Rig", 1, "AccessoryRig");
        var diffuse = CreateResource(sourceId, externalPackagePath, SourceKind.Game, "PNGImage", 1);

        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [casPart.Key.FullTgi] = CreateSyntheticCasPartBytes(geometry.Key, thumbnail.Key, diffuse.Key, "acAcc_NecklaceCollar", bodyType: 12),
                [geometry.Key.FullTgi] = CreateSyntheticSkinnedGeometryBytes(),
                [rig.Key.FullTgi] = CreateSyntheticRigBytes(),
                [diffuse.Key.FullTgi] = TestAssets.OnePixelPng,
                [thumbnail.Key.FullTgi] = TestAssets.OnePixelPng
            },
            new Dictionary<string, string?>());
        var indexStore = new FakeGraphIndexStore([geometry, rig, diffuse]);
        var builder = new ExplicitAssetGraphBuilder(catalog, indexStore);
        var localResources = new[] { casPart, thumbnail };

        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 0, DateTimeOffset.UtcNow, localResources, []))
            .Single(asset => asset.AssetKind == AssetKind.Cas);
        Assert.Equal("Metadata", summary.SupportLabel);

        var graph = await builder.BuildAssetGraphAsync(summary, localResources, CancellationToken.None);
        var details = AssetDetailsFormatter.BuildAssetDetails(summary, graph, graph.CasGraph!.GeometryResource, null);

        Assert.Contains("Support Status: Geometry+Textures", details, StringComparison.Ordinal);
        Assert.Contains("Category: Accessory", details, StringComparison.Ordinal);
        Assert.Contains("Has Exact Geometry Candidate: Yes", details, StringComparison.Ordinal);
        Assert.Contains("Has Rig Reference: Yes", details, StringComparison.Ordinal);
        Assert.Contains("Package-Local Graph: No", details, StringComparison.Ordinal);
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
    public void Ts4SeedMetadataExtractor_ExtractsCasPartTechnicalNameFromStableHeaderWhenBodyIsTruncated()
    {
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var bytes = CreateSyntheticCasPartBytesWithTruncatedBody(geometryKey, thumbnailKey, textureKey);
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

        Assert.Equal("BrokenCasPart", technicalName);
    }

    [Fact]
    public void Ts4SeedMetadataExtractor_ReturnsNullForCasPartWithTruncatedManagedString()
    {
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var bytes = CreateSyntheticCasPartBytesWithTruncatedManagedString(geometryKey, thumbnailKey, textureKey);
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

        Assert.Null(technicalName);
    }

    [Fact]
    public void Ts4CasPart_Parse_HandlesVersion46Layout()
    {
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var bytes = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, internalName: "ymHair_ModernSweep");
        var assetsAssembly = typeof(ExplicitAssetGraphBuilder).Assembly;
        var casPartType = RequireType(assetsAssembly, "Sims4ResourceExplorer.Assets.Ts4CasPart");

        var parsed = casPartType
            .GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [bytes])!;

        Assert.Equal(2, (int)casPartType.GetProperty("BodyType")!.GetValue(parsed)!);
        Assert.Equal(0x00003010u, (uint)casPartType.GetProperty("AgeGenderFlags")!.GetValue(parsed)!);
        Assert.Equal("ymHair_ModernSweep", (string?)casPartType.GetProperty("InternalName")!.GetValue(parsed));

        var lods = ((System.Collections.IEnumerable)casPartType.GetProperty("Lods")!.GetValue(parsed)!).Cast<object>().ToArray();
        var tgi = ((System.Collections.IEnumerable)casPartType.GetProperty("TgiList")!.GetValue(parsed)!).Cast<object>().ToArray();
        Assert.Single(lods);
        Assert.Equal(4, tgi.Length);
    }

    [Fact]
    public void Ts4SeedMetadataExtractor_ExtractsCasPartVariantRowsFromSwatches()
    {
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var bytes = CreateSyntheticCasPartBytes(
            geometryKey,
            thumbnailKey,
            textureKey,
            internalName: "yfTop_Test",
            swatchColors: [0xFFFFCCAAu, 0xFF112233u, 0xFF445566u]);
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

        var seedMetadata = Ts4SeedMetadataExtractor.TryExtractCasPartSeedMetadata(resource, bytes);

        Assert.NotNull(seedMetadata);
        Assert.Equal("yfTop_Test", seedMetadata!.TechnicalName);
        Assert.Equal(3, seedMetadata.Variants.Count);
        Assert.All(seedMetadata.Variants, variant => Assert.Equal("Swatch", variant.VariantKind));
        Assert.Equal("#FFFFCCAA", seedMetadata.Variants[0].SwatchHex);
        Assert.Equal(thumbnailKey.FullTgi, seedMetadata.Variants[0].ThumbnailTgi);
    }

    [Fact]
    public void Ts4SeedMetadataExtractor_ExtractsCasPartCompatibilityMetadata()
    {
        var geometryKey = new ResourceKeyRecord(0x015A1849, 0, 1, "Geometry");
        var thumbnailKey = new ResourceKeyRecord(0x3C1AF1F2, 0, 2, "CASPartThumbnail");
        var textureKey = new ResourceKeyRecord(0x00B2D882, 0, 3, "PNGImage");
        var bytes = CreateSyntheticCasPartBytes(
            geometryKey,
            thumbnailKey,
            textureKey,
            internalName: "ymHair_ModernSweep",
            bodyType: 2);
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

        var seedMetadata = Ts4SeedMetadataExtractor.TryExtractCasPartSeedMetadata(resource, bytes);

        Assert.NotNull(seedMetadata);
        Assert.Equal("Hair", seedMetadata!.SlotCategory);
        Assert.Equal("hair", seedMetadata.CategoryNormalized);
        Assert.Contains("slot=Hair", seedMetadata.Description, StringComparison.Ordinal);
        Assert.Contains("bodyType=2", seedMetadata.Description, StringComparison.Ordinal);
        Assert.Contains("internalName=ymHair_ModernSweep", seedMetadata.Description, StringComparison.Ordinal);
        Assert.Contains("species=Human", seedMetadata.Description, StringComparison.Ordinal);
        Assert.Contains("age=Young Adult", seedMetadata.Description, StringComparison.Ordinal);
        Assert.Contains("gender=Unisex", seedMetadata.Description, StringComparison.Ordinal);
        Assert.Contains("sortLayer=", seedMetadata.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Ts4SeedMetadataExtractor_ParsesIndexedCasPartSummaryMetadataFromExistingSeedFields()
    {
        const string description = "slot=Full Body | species=Human | age=Young Adult | gender=Female | defaultBodyType=true | nakedLink=true | restrictOppositeGender=true";

        var summary = Ts4SeedMetadataExtractor.TryExtractCasPartSummaryMetadata(
            description,
            "yfBody_Nude",
            "Full Body");

        Assert.NotNull(summary);
        Assert.Equal(5, summary!.BodyType);
        Assert.Equal("yfBody_Nude", summary.InternalName);
        Assert.True(summary.DefaultForBodyType);
        Assert.True(summary.HasNakedLink);
        Assert.True(summary.RestrictOppositeGender);
        Assert.Equal("Human", summary.SpeciesLabel);
        Assert.Equal("Young Adult", summary.AgeLabel);
        Assert.Equal("Female", summary.GenderLabel);
    }

    [Fact]
    public void AssetGraphBuilder_SetsCasVariantCountFromIndexedVariantRows()
    {
        var packagePath = "cas-variants.package";
        var sourceId = Guid.NewGuid();
        var casPart = CreateResource(sourceId, packagePath, SourceKind.Game, "CASPart", 1, "yfTop_Test");
        var geometry = CreateResource(sourceId, packagePath, SourceKind.Game, "Geometry", 1);
        var thumbnail = CreateResource(sourceId, packagePath, SourceKind.Game, "CASPartThumbnail", 1);
        var builder = new ExplicitAssetGraphBuilder(new FakeResourceCatalogService(new Dictionary<string, byte[]>(), new Dictionary<string, string?>()));
        var scan = new PackageScanResult(sourceId, SourceKind.Game, packagePath, 10, DateTimeOffset.UtcNow, [casPart, geometry, thumbnail], [])
        {
            AssetVariants =
            [
                new DiscoveredAssetVariant(sourceId, SourceKind.Game, AssetKind.Cas, packagePath, casPart.Key, 0, "Swatch", "Swatch 1", "#FFFFCCAA", thumbnail.Key.FullTgi),
                new DiscoveredAssetVariant(sourceId, SourceKind.Game, AssetKind.Cas, packagePath, casPart.Key, 1, "Swatch", "Swatch 2", "#FF112233", thumbnail.Key.FullTgi),
                new DiscoveredAssetVariant(sourceId, SourceKind.Game, AssetKind.Cas, packagePath, casPart.Key, 2, "Swatch", "Swatch 3", "#FF445566", thumbnail.Key.FullTgi)
            ]
        };

        var summary = builder.BuildAssetSummaries(scan).Single();

        Assert.Equal(3, summary.VariantCount);
        Assert.True(summary.HasVariants);
        Assert.True(summary.CapabilitySnapshot.HasVariants);
    }

    [Fact]
    public void AssetGraphBuilder_UsesCasSeedMetadataForCategoryAndDescription()
    {
        var packagePath = "cas-summary.package";
        var sourceId = Guid.NewGuid();
        var casPart = CreateResource(sourceId, packagePath, SourceKind.Game, "CASPart", 1, "yfTop_Test") with
        {
            Description = "slot=Top | species=Human | age=Young Adult | gender=Female | swatches=1"
        };
        var geometry = CreateResource(sourceId, packagePath, SourceKind.Game, "Geometry", 1);
        var thumbnail = CreateResource(sourceId, packagePath, SourceKind.Game, "CASPartThumbnail", 1);
        var builder = new ExplicitAssetGraphBuilder(new FakeResourceCatalogService(new Dictionary<string, byte[]>(), new Dictionary<string, string?>()));

        var summary = builder.BuildAssetSummaries(new PackageScanResult(sourceId, SourceKind.Game, packagePath, 10, DateTimeOffset.UtcNow, [casPart, geometry, thumbnail], []))
            .Single(asset => asset.AssetKind == AssetKind.Cas);

        Assert.Equal("Top", summary.Category);
        Assert.Equal("top", summary.CategoryNormalized);
        Assert.Contains("slot=Top", summary.Description, StringComparison.Ordinal);
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
    public void Ts4SeedMetadataExtractor_ExtractsObjectDefinitionSceneRootHint()
    {
        var payload = CreateSyntheticObjectDefinitionBytes(
            "sculptFloor_EP06GENsetStorefront14x4x5_set1",
            2,
            227,
            0x00000009u,
            0x039E5CF1u,
            0x51195E00u,
            0x01661233u,
            0x00000000u);

        var metadata = Ts4SeedMetadataExtractor.TryExtractObjectDefinitionSeedMetadata(payload);

        Assert.NotNull(metadata);
        Assert.Equal("sculptFloor_EP06GENsetStorefront14x4x5_set1", metadata!.TechnicalName);
        Assert.Equal("01661233:00000000:039E5CF151195E00", metadata.SceneRootTgiHint);
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
    public async Task CasLogicalAsset_BuildsSceneFromWrappedGeometryResource()
    {
        var packagePath = Path.Combine(tempRoot, "cas-wrapped-geom.package");
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
                [geometry.Key.FullTgi] = WrapSingleChunkRcol(CreateSyntheticSkinnedGeometryBytes()),
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

        var sceneBuilder = new BuildBuySceneBuildService(catalog, store);
        var scene = await sceneBuilder.BuildSceneAsync(assetGraph.CasGraph.GeometryResource!, CancellationToken.None);

        Assert.True(scene.Success, string.Join(Environment.NewLine, scene.Diagnostics));
        Assert.NotNull(scene.Scene);
        Assert.NotEmpty(scene.Scene!.Meshes);
    }

    [Fact]
    public async Task CasGraphScene_UsesCasLinkedRigAndTextureResources_WhenGeometryCompanionsAreMissing()
    {
        var packagePath = Path.Combine(tempRoot, "cas-linked-companions.package");
        var sourceId = Guid.NewGuid();
        var casPart = CreateResource(sourceId, packagePath, SourceKind.Game, "CASPart", 1, "Base Top");
        var geometry = CreateResource(sourceId, packagePath, SourceKind.Game, "Geometry", 10, "TopGeom");
        var rig = CreateResource(sourceId, packagePath, SourceKind.Game, "Rig", 20, "HumanRig");
        var texture = CreateResource(sourceId, packagePath, SourceKind.Game, "PNGImage", 30, "TopTexture");

        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [geometry.Key.FullTgi] = CreateSyntheticSkinnedGeometryBytes(),
                [rig.Key.FullTgi] = CreateSyntheticRigBytes(),
                [texture.Key.FullTgi] = TestAssets.OnePixelPng
            },
            new Dictionary<string, string?>());
        var sceneBuilder = new BuildBuySceneBuildService(
            catalog,
            new FakeGraphIndexStore([casPart, geometry, rig, texture]));
        var casGraph = new CasAssetGraph(
            casPart,
            geometry,
            rig,
            [casPart],
            [geometry],
            [rig],
            [],
            [texture],
            [],
            [],
            "Top",
            null,
            "LOD 0",
            true,
            "Synthetic CAS graph");

        var scene = await sceneBuilder.BuildSceneAsync(casGraph, CancellationToken.None);

        Assert.True(scene.Success, string.Join(Environment.NewLine, scene.Diagnostics));
        Assert.NotNull(scene.Scene);
        Assert.NotEmpty(scene.Scene!.Materials.SelectMany(static material => material.Textures));
        Assert.Contains(scene.Scene.Bones, bone => bone.BindPoseMatrix is not null);
        Assert.Contains(scene.Diagnostics, message => message.Contains("Resolved 1 CAS-linked texture candidate", StringComparison.Ordinal));
        Assert.Contains(scene.Diagnostics, message => message.Contains("Resolved rig:", StringComparison.Ordinal));
        Assert.DoesNotContain(scene.Diagnostics, message => message.Contains("No exact-instance Rig resource", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CasGraphScene_PrefersManifestTextureRoutingBeforeSameInstanceFallback()
    {
        var packagePath = Path.Combine(tempRoot, "cas-manifest-primary.package");
        var texturePackagePath = Path.Combine(tempRoot, "cas-manifest-textures.package");
        var sourceId = Guid.NewGuid();
        var casPart = CreateResource(sourceId, packagePath, SourceKind.Game, "CASPart", 1, "Base Top");
        var geometry = CreateResource(sourceId, packagePath, SourceKind.Game, "Geometry", 10, "TopGeom");
        var rig = CreateResource(sourceId, packagePath, SourceKind.Game, "Rig", 20, "HumanRig");
        var manifestTexture = CreateResource(sourceId, texturePackagePath, SourceKind.Game, "PNGImage", 30, "ManifestTexture");
        var fallbackTexture = CreateResource(sourceId, packagePath, SourceKind.Game, "PNGImage", 10, "FallbackTexture");

        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [geometry.Key.FullTgi] = CreateSyntheticSkinnedGeometryBytes(),
                [rig.Key.FullTgi] = CreateSyntheticRigBytes(),
                [manifestTexture.Key.FullTgi] = TestAssets.OnePixelPng,
                [fallbackTexture.Key.FullTgi] = TestAssets.OnePixelPng
            },
            new Dictionary<string, string?>());
        var sceneBuilder = new BuildBuySceneBuildService(
            catalog,
            new FakeGraphIndexStore([casPart, geometry, rig, manifestTexture, fallbackTexture]));
        var casGraph = new CasAssetGraph(
            casPart,
            geometry,
            rig,
            [casPart],
            [geometry],
            [rig],
            [],
            [manifestTexture],
            [],
            [
                new MaterialManifestEntry(
                    "ApproximateCasMaterial",
                    null,
                    false,
                    "portable-approximation",
                    null,
                    [],
                    "Manifest-driven CAS preview",
                    [
                        new MaterialTextureEntry(
                            "diffuse",
                            "manifest_diffuse.png",
                            manifestTexture.Key,
                            manifestTexture.PackagePath,
                            CanonicalTextureSemantic.BaseColor,
                            CanonicalTextureSourceKind.ExplicitIndexed)
                    ],
                    CanonicalMaterialSourceKind.ApproximateCas)
            ],
            "Top",
            null,
            "LOD 0",
            true,
            "Synthetic CAS graph with manifest texture");

        var scene = await sceneBuilder.BuildSceneAsync(casGraph, CancellationToken.None);

        Assert.True(scene.Success, string.Join(Environment.NewLine, scene.Diagnostics));
        Assert.NotNull(scene.Scene);
        var material = Assert.Single(scene.Scene!.Materials);
        var texture = Assert.Single(material.Textures);
        Assert.Equal(manifestTexture.Key.FullTgi, texture.SourceKey?.FullTgi);
        Assert.Equal(texturePackagePath, texture.SourcePackagePath);
        Assert.Equal(CanonicalTextureSourceKind.ExplicitIndexed, texture.SourceKind);
        Assert.Contains(scene.Diagnostics, message => message.Contains("Resolved 1 manifest-driven CAS material", StringComparison.Ordinal));
        Assert.DoesNotContain(scene.Diagnostics, message => message.Contains("CAS preview fell back to exact-instance texture candidates", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CasGraphScene_PrefersMaterialDefinitionRoutingBeforeManifestApproximation()
    {
        var packagePath = Path.Combine(tempRoot, "cas-materialdefinition-primary.package");
        var manifestTexturePackagePath = Path.Combine(tempRoot, "cas-manifest-textures.package");
        var sourceId = Guid.NewGuid();
        var casPart = CreateResource(sourceId, packagePath, SourceKind.Game, "CASPart", 1, "Base Top");
        var geometry = CreateResource(sourceId, packagePath, SourceKind.Game, "Geometry", 10, "TopGeom");
        var rig = CreateResource(sourceId, packagePath, SourceKind.Game, "Rig", 20, "HumanRig");
        var material = CreateResource(sourceId, packagePath, SourceKind.Game, "MaterialDefinition", 10, "TopMaterial");
        var materialTexture = CreateResource(sourceId, packagePath, SourceKind.Game, "PNGImage", 40, "MaterialTexture");
        var manifestTexture = CreateResource(sourceId, manifestTexturePackagePath, SourceKind.Game, "PNGImage", 30, "ManifestTexture");

        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [geometry.Key.FullTgi] = CreateSyntheticSkinnedGeometryBytes(),
                [rig.Key.FullTgi] = CreateSyntheticRigBytes(),
                [material.Key.FullTgi] = WrapSingleChunkRcol(CreateMinimalMatd(
                    0x11111111u,
                    0xB9105A6Du,
                    (0x1B9D3AC5u, 2u, 1u, CreateEmbeddedResourceKeyBytes(materialTexture.Key.Type, materialTexture.Key.Group, materialTexture.Key.FullInstance)))),
                [materialTexture.Key.FullTgi] = TestAssets.OnePixelPng,
                [manifestTexture.Key.FullTgi] = TestAssets.OnePixelPng
            },
            new Dictionary<string, string?>());
        var sceneBuilder = new BuildBuySceneBuildService(
            catalog,
            new FakeGraphIndexStore([casPart, geometry, rig, material, materialTexture, manifestTexture]));
        var casGraph = new CasAssetGraph(
            casPart,
            geometry,
            rig,
            [casPart],
            [geometry],
            [rig],
            [material],
            [manifestTexture],
            [],
            [
                new MaterialManifestEntry(
                    "ApproximateCasMaterial",
                    null,
                    false,
                    "portable-approximation",
                    null,
                    [],
                    "Manifest-driven CAS preview",
                    [
                        new MaterialTextureEntry(
                            "diffuse",
                            "manifest_diffuse.png",
                            manifestTexture.Key,
                            manifestTexture.PackagePath,
                            CanonicalTextureSemantic.BaseColor,
                            CanonicalTextureSourceKind.ExplicitIndexed)
                    ],
                    CanonicalMaterialSourceKind.ApproximateCas)
            ],
            "Top",
            null,
            "LOD 0",
            true,
            "Synthetic CAS graph with material definition");

        var scene = await sceneBuilder.BuildSceneAsync(casGraph, CancellationToken.None);

        Assert.True(scene.Success, string.Join(Environment.NewLine, scene.Diagnostics));
        Assert.NotNull(scene.Scene);
        var resolvedMaterial = Assert.Single(scene.Scene!.Materials);
        var texture = Assert.Single(resolvedMaterial.Textures);
        Assert.Equal(materialTexture.Key.FullTgi, texture.SourceKey?.FullTgi);
        Assert.Equal(packagePath, texture.SourcePackagePath);
        Assert.Equal(CanonicalMaterialSourceKind.ExplicitMatd, resolvedMaterial.SourceKind);
        Assert.Contains(scene.Diagnostics, message => message.Contains("Resolved 1 CAS material-definition candidate", StringComparison.Ordinal));
        Assert.DoesNotContain(scene.Diagnostics, message => message.Contains("Resolved 1 manifest-driven CAS material", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CasGeometryScene_SkipsUndecodableFallbackTextureCandidate()
    {
        var packagePath = Path.Combine(tempRoot, "cas-undecodable-texture.package");
        var cacheService = new TestCacheService(tempRoot);
        var store = new SqliteIndexStore(cacheService);
        await store.InitializeAsync(CancellationToken.None);

        var source = new DataSourceDefinition(Guid.NewGuid(), "Fixture", tempRoot, SourceKind.Game);
        var geometry = CreateResource(source.Id, packagePath, SourceKind.Game, "Geometry", 1, "AccessoryGeom");
        var texture = CreateResource(source.Id, packagePath, SourceKind.Game, "PNGImage", 1, "AccessoryTexture");

        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [geometry.Key.FullTgi] = CreateSyntheticSkinnedGeometryBytes()
            },
            new Dictionary<string, string?>(),
            new Dictionary<string, Exception>
            {
                [texture.Key.FullTgi] = new InvalidDataException("Unknown image format.")
            });

        var sceneBuilder = new BuildBuySceneBuildService(
            catalog,
            new FakeGraphIndexStore([geometry, texture]));

        var scene = await sceneBuilder.BuildSceneAsync(geometry, CancellationToken.None);

        Assert.True(scene.Success, string.Join(Environment.NewLine, scene.Diagnostics));
        Assert.NotNull(scene.Scene);
        Assert.Empty(scene.Scene!.Materials.SelectMany(static material => material.Textures));
        Assert.Contains(scene.Diagnostics, message => message.Contains("Skipped same-instance fallback texture", StringComparison.Ordinal));
        Assert.Contains(scene.Diagnostics, message => message.Contains("Unknown image format.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CasGeometryScene_ReportsReadableDiagnostic_ForInvalidGeomCounts()
    {
        var packagePath = Path.Combine(tempRoot, "cas-invalid-geom-count.package");
        var geometry = CreateResource(Guid.NewGuid(), packagePath, SourceKind.Game, "Geometry", 1, "BrokenGeom");
        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [geometry.Key.FullTgi] = CreateSyntheticGeometryBytesWithNegativeFormatCount()
            },
            new Dictionary<string, string?>());

        var sceneBuilder = new BuildBuySceneBuildService(
            catalog,
            new FakeGraphIndexStore([geometry]));

        var scene = await sceneBuilder.BuildSceneAsync(geometry, CancellationToken.None);

        Assert.False(scene.Success);
        Assert.Null(scene.Scene);
        Assert.Contains(scene.Diagnostics, message => message.Contains("Geometry parsing failed", StringComparison.Ordinal));
        Assert.Contains(scene.Diagnostics, message => message.Contains("GEOM format count -1 is invalid.", StringComparison.Ordinal));
        Assert.DoesNotContain(scene.Diagnostics, message => message.Contains("capacity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CasGeometryScene_ParsesVersion15GeomTailData()
    {
        var packagePath = Path.Combine(tempRoot, "cas-version15-geom.package");
        var geometry = CreateResource(Guid.NewGuid(), packagePath, SourceKind.Game, "Geometry", 1, "Version15Geom");
        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [geometry.Key.FullTgi] = CreateSyntheticVersion15GeometryBytes()
            },
            new Dictionary<string, string?>());

        var sceneBuilder = new BuildBuySceneBuildService(
            catalog,
            new FakeGraphIndexStore([geometry]));

        var scene = await sceneBuilder.BuildSceneAsync(geometry, CancellationToken.None);

        Assert.True(scene.Success, string.Join(Environment.NewLine, scene.Diagnostics));
        Assert.NotNull(scene.Scene);
        Assert.NotEmpty(scene.Scene!.Meshes);
    }

    [Fact]
    public async Task CasGraphScene_PreservesSecondaryUvSetInCanonicalMesh()
    {
        var packagePath = Path.Combine(tempRoot, "cas-secondary-uv.package");
        var sourceId = Guid.NewGuid();
        var casPart = CreateResource(sourceId, packagePath, SourceKind.Game, "CASPart", 1, "MappedTop");
        var geometry = CreateResource(sourceId, packagePath, SourceKind.Game, "Geometry", 10, "MappedGeom");
        var rig = CreateResource(sourceId, packagePath, SourceKind.Game, "Rig", 20, "HumanRig");

        var catalog = new FakeResourceCatalogService(
            new Dictionary<string, byte[]>
            {
                [geometry.Key.FullTgi] = CreateSyntheticSkinnedGeometryBytesWithSecondaryUvSet(),
                [rig.Key.FullTgi] = CreateSyntheticRigBytes()
            },
            new Dictionary<string, string?>());
        var sceneBuilder = new BuildBuySceneBuildService(
            catalog,
            new FakeGraphIndexStore([casPart, geometry, rig]));
        var casGraph = new CasAssetGraph(
            casPart,
            geometry,
            rig,
            [casPart],
            [geometry],
            [rig],
            [],
            [],
            [],
            [],
            "Top",
            null,
            "LOD 0",
            true,
            "Synthetic CAS graph with secondary UV set");

        var scene = await sceneBuilder.BuildSceneAsync(casGraph, CancellationToken.None);

        Assert.True(scene.Success, string.Join(Environment.NewLine, scene.Diagnostics));
        Assert.NotNull(scene.Scene);

        var mesh = Assert.Single(scene.Scene!.Meshes);
        Assert.Equal(new float[] { 0f, 0f, 0f, 1f, 1f, 0f }, mesh.Uvs);
        Assert.Equal(new float[] { 0f, 0f, 0f, 1f, 1f, 0f }, Assert.IsType<float[]>(mesh.Uv0s));
        Assert.Equal(new float[] { 0.25f, 0.25f, 0.25f, 0.75f, 0.75f, 0.25f }, Assert.IsType<float[]>(mesh.Uv1s));
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
    public async Task ResourceCatalogService_DecodesSyntheticRle2TextureToPng()
    {
        var packagePath = Path.Combine(tempRoot, "synthetic-rle2.package");
        var package = new DataBasePackedFile();
        var resourceKey = new ResourceKey(ResourceType.RLE2Image, 0x00000000, 0x0000000000000001);
        package.Set(resourceKey, CreateSyntheticRle2TextureBytes(), CompressionMode.ForceOff);
        await package.SaveAsAsync(packagePath, ResourceKeyOrder.Preserve, CancellationToken.None);

        var service = new LlamaResourceCatalogService();
        var pngBytes = await service.GetTexturePngAsync(
            packagePath,
            new ResourceKeyRecord((uint)ResourceType.RLE2Image, 0x00000000, 0x0000000000000001, nameof(ResourceType.RLE2Image)),
            CancellationToken.None);

        Assert.NotNull(pngBytes);
        Assert.True(pngBytes!.Length > 8);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, pngBytes.Take(8).ToArray());
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
    public async Task SqliteIndexStore_PersistsAndQueriesAssetVariants()
    {
        var cacheService = new TestCacheService(tempRoot);
        var store = new SqliteIndexStore(cacheService);
        await store.InitializeAsync(CancellationToken.None);

        var source = new DataSourceDefinition(Guid.NewGuid(), "Game", tempRoot, SourceKind.Game);
        await store.UpsertDataSourcesAsync([source], CancellationToken.None);

        var packagePath = "variants.package";
        var resource = CreateResource(source.Id, packagePath, SourceKind.Game, "CASPart", 1, "yfTop_Test");
        var asset = new AssetSummary(
            Guid.NewGuid(),
            source.Id,
            SourceKind.Game,
            AssetKind.Cas,
            "yfTop_Test",
            "CAS",
            packagePath,
            resource.Key,
            null,
            3,
            1,
            string.Empty,
            new AssetCapabilitySnapshot(true, true, true, true, HasVariants: true));
        var scan = new PackageScanResult(source.Id, SourceKind.Game, packagePath, 10, DateTimeOffset.UtcNow, [resource], [])
        {
            AssetVariants =
            [
                new DiscoveredAssetVariant(source.Id, SourceKind.Game, AssetKind.Cas, packagePath, resource.Key, 0, "Swatch", "Swatch 1 (#FFFFCCAA)", "#FFFFCCAA"),
                new DiscoveredAssetVariant(source.Id, SourceKind.Game, AssetKind.Cas, packagePath, resource.Key, 1, "Swatch", "Swatch 2 (#FF112233)", "#FF112233"),
                new DiscoveredAssetVariant(source.Id, SourceKind.Game, AssetKind.Cas, packagePath, resource.Key, 2, "Swatch", "Swatch 3 (#FF445566)", "#FF445566")
            ]
        };

        await store.ReplacePackageAsync(scan, [asset], CancellationToken.None);

        var variants = await store.GetAssetVariantsAsync(asset.Id, CancellationToken.None);
        var variantOnlyAssets = await store.QueryAssetsAsync(
            new AssetBrowserQuery(
                new SourceScope(),
                string.Empty,
                AssetBrowserDomain.Cas,
                string.Empty,
                string.Empty,
                string.Empty,
                false,
                true,
                AssetBrowserSort.Name,
                0,
                10,
                new AssetCapabilityFilter()),
            CancellationToken.None);

        Assert.Equal(3, variants.Count);
        Assert.All(variants, variant => Assert.Equal(asset.Id, variant.AssetId));
        Assert.Single(variantOnlyAssets.Items);
        Assert.Equal(3, variantOnlyAssets.Items[0].VariantCount);
    }

    [Fact]
    public async Task SqliteIndexStore_QueriesGeneral3DAssetsSeparatelyFromBuildBuy()
    {
        var cacheService = new TestCacheService(tempRoot);
        var store = new SqliteIndexStore(cacheService);
        await store.InitializeAsync(CancellationToken.None);

        var source = new DataSourceDefinition(Guid.NewGuid(), "Game", tempRoot, SourceKind.Game);
        await store.UpsertDataSourcesAsync([source], CancellationToken.None);

        var packagePath = "general3d.package";
        var model = CreateResource(source.Id, packagePath, SourceKind.Game, "Model", 1, "LooseChair");
        var modelLod = CreateResource(source.Id, packagePath, SourceKind.Game, "ModelLOD", 1);
        var asset = new AssetSummary(
            Guid.NewGuid(),
            source.Id,
            SourceKind.Game,
            AssetKind.General3D,
            "LooseChair",
            "General 3D",
            packagePath,
            model.Key,
            null,
            1,
            1,
            string.Empty,
            new AssetCapabilitySnapshot(true, true, true, true),
            PackageName: packagePath,
            RootTypeName: "Model",
            PrimaryGeometryType: "ModelLOD",
            CategoryNormalized: "general3d");

        await store.ReplacePackageAsync(
            new PackageScanResult(source.Id, SourceKind.Game, packagePath, 10, DateTimeOffset.UtcNow, [model, modelLod], []),
            [asset],
            CancellationToken.None);

        var general3DResults = await store.QueryAssetsAsync(
            new AssetBrowserQuery(
                new SourceScope(),
                "loosechair",
                AssetBrowserDomain.General3D,
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
        var buildBuyResults = await store.QueryAssetsAsync(
            new AssetBrowserQuery(
                new SourceScope(),
                "loosechair",
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

        Assert.Single(general3DResults.Items);
        Assert.Empty(buildBuyResults.Items);
        Assert.Equal(AssetKind.General3D, general3DResults.Items[0].AssetKind);
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
        var graph = new AssetGraph(summary, [], [], BuildBuyGraph: new BuildBuyAssetGraph(root, [], [], [], [], [], [], [], [], true, "subset"));
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
        var graph = new AssetGraph(summary, [], ["No exact-instance ModelLOD resources were indexed for this model."], BuildBuyGraph: new BuildBuyAssetGraph(root, [], [], [], [], [], [], [], ["No exact-instance ModelLOD resources were indexed for this model."], true, "subset"));
        var successfulScene = new ScenePreviewContent(root, new CanonicalScene("chair", [], [], [], new Bounds3D(0, 0, 0, 1, 1, 1)), "Selected LOD root: MLOD0", SceneBuildStatus.SceneReady);
        var failedScene = new ScenePreviewContent(root, null, "No triangle meshes could be reconstructed.", SceneBuildStatus.Unsupported);

        var successDetails = AssetDetailsFormatter.BuildAssetDetails(summary, graph, root, successfulScene);
        var failureDetails = AssetDetailsFormatter.BuildAssetDetails(summary, graph, root, failedScene);

        Assert.Contains("Support Status: Geometry", successDetails);
        Assert.Contains("Scene Reconstruction: SceneReady", successDetails);
        Assert.Contains("Scene Reconstruction: Failed", failureDetails);
        Assert.Contains("No triangle meshes could be reconstructed.", failureDetails);
    }

    [Fact]
    public void AssetDetailsFormatter_ReportsRenderedSimPreviewAsGeometryInsteadOfMetadataOnly()
    {
        var sourceId = Guid.NewGuid();
        var simInfo = CreateResource(sourceId, "sim.package", SourceKind.Game, "SimInfo", 1);
        var summary = new AssetSummary(
            Guid.NewGuid(),
            sourceId,
            SourceKind.Game,
            AssetKind.Sim,
            "Sim archetype: Human | Young Adult | Male",
            "Human",
            "sim.package",
            simInfo.Key,
            null,
            1,
            1,
            string.Empty,
            new AssetCapabilitySnapshot(true, false, false, false, HasIdentityMetadata: true),
            RootTypeName: "SimArchetype",
            IdentityType: "SimInfo");
        var scene = new CanonicalScene(
            "sim",
            [],
            [new CanonicalMaterial("skin", [new CanonicalTexture("diffuse", "skin.png", [0x01])])],
            [],
            new Bounds3D(0, 0, 0, 1, 1, 1));
        var preview = new ScenePreviewContent(simInfo, scene, "Assembled body-first preview ready.", SceneBuildStatus.SceneReady);
        var simGraph = new SimAssetGraph(
            simInfo,
            new SimInfoSummary(32, "Human", "Young Adult", "Male", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            null,
            [],
            [],
            [],
            [],
            new SimBodyAssemblySummary(SimBodyAssemblyMode.None, "unresolved", [], []),
            [],
            [],
            [],
            [simInfo],
            [],
            true,
            "subset");
        var graph = new AssetGraph(summary, [simInfo], [], SimGraph: simGraph);

        var details = AssetDetailsFormatter.BuildAssetDetails(summary, graph, null, preview);

        Assert.Contains("Support Status: Geometry+Textures", details);
        Assert.Contains("Scene Reconstruction: SceneReady", details);
        Assert.DoesNotContain("metadata-only", details, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rendered body-first Sim preview is available", details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AssetDetailsFormatter_ReportsSelectedVariantAndMarksIndexedRow()
    {
        var sourceId = Guid.NewGuid();
        var root = CreateResource(sourceId, "variants.package", SourceKind.Game, "CASPart", 1, "yfTop_Test");
        var summary = new AssetSummary(Guid.NewGuid(), sourceId, SourceKind.Game, AssetKind.Cas, "yfTop_Test", "CAS", "variants.package", root.Key, null, 3, 1, string.Empty, new AssetCapabilitySnapshot(true, true, true, true, HasVariants: true));
        var graph = new AssetGraph(summary, [], [], CasGraph: new CasAssetGraph(root, null, null, [root], [], [], [], [], [], [], "Top", "Swatch data available", null, false, "subset"));
        AssetVariantSummary[] variants =
        [
            new AssetVariantSummary(summary.Id, sourceId, SourceKind.Game, AssetKind.Cas, "variants.package", root.Key.FullTgi, 0, "Swatch", "Swatch 1 (#FF112233)", "#FF112233"),
            new AssetVariantSummary(summary.Id, sourceId, SourceKind.Game, AssetKind.Cas, "variants.package", root.Key.FullTgi, 1, "Swatch", "Swatch 2 (#FF445566)", "#FF445566")
        ];

        var details = AssetDetailsFormatter.BuildAssetDetails(summary, graph, null, null, variants, variants[1]);

        Assert.Contains("Selected Variant: Swatch 2: Swatch 2 (#FF445566)", details);
        Assert.Contains("> Swatch 2: Swatch 2 (#FF445566) | #FF445566", details);
        Assert.Contains("  Swatch 1: Swatch 1 (#FF112233) | #FF112233", details);
    }

    [Fact]
    public void AssetVariantSceneAdapter_AppliesCasSwatchTintToApproximateCasMaterials()
    {
        var scene = new CanonicalScene(
            "CasScene",
            [],
            [new CanonicalMaterial("ApproximateCasMaterial", [], SourceKind: CanonicalMaterialSourceKind.ApproximateCas)],
            [],
            new Bounds3D(0, 0, 0, 1, 1, 1));
        var variant = new AssetVariantSummary(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SourceKind.Game,
            AssetKind.Cas,
            "test.package",
            "034AEECB:00000000:0000000000000001",
            0,
            "Swatch",
            "Swatch 1 (#FF3366CC)",
            "#FF3366CC");

        var adapted = AssetVariantSceneAdapter.ApplyToScene(scene, AssetKind.Cas, variant);
        var diagnostics = AssetVariantSceneAdapter.AppendVariantDiagnostics("Scene ready.", AssetKind.Cas, variant);

        Assert.NotNull(adapted.Materials[0].ViewportTintColor);
        Assert.Equal(0.2f, adapted.Materials[0].ViewportTintColor!.R, 3);
        Assert.Equal(0.4f, adapted.Materials[0].ViewportTintColor!.G, 3);
        Assert.Equal(0.8f, adapted.Materials[0].ViewportTintColor!.B, 3);
        Assert.Contains("Selected swatch tint: #FF3366CC", diagnostics);
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
        else if (graph.General3DGraph is not null)
        {
            resources.AddRange(graph.General3DGraph.ModelResources);
            resources.AddRange(graph.General3DGraph.ModelLodResources);
            resources.AddRange(graph.General3DGraph.GeometryResources);
            resources.AddRange(graph.General3DGraph.RigResources);
            resources.AddRange(graph.General3DGraph.MaterialResources);
            resources.AddRange(graph.General3DGraph.TextureResources);
        }
        else if (graph.SimGraph is not null)
        {
            resources.AddRange(graph.SimGraph.IdentityResources);
        }

        return resources
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }

    private static byte[] CreateSyntheticCasPartBytes(
        ResourceKeyRecord geometryKey,
        ResourceKeyRecord thumbnailKey,
        ResourceKeyRecord textureKey,
        string internalName = "Short Hair",
        IReadOnlyList<uint>? swatchColors = null,
        ResourceKeyRecord? regionMapKey = null,
        ResourceKeyRecord? colorShiftMaskKey = null,
        uint version = 0x0000002Eu,
        int bodyType = 2,
        bool includeLodReferences = true,
        byte partFlags1 = 0,
        byte partFlags2 = 0,
        uint speciesValue = 1u,
        uint ageGenderFlags = 0x00003010u,
        byte nakedKey = 0xFF)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.BigEndianUnicode, leaveOpen: true);
        var noneKey = new ResourceKeyRecord(0u, 0u, 0ul, "0x00000000");
        swatchColors ??= [0xFFFFCCAAu];
        var regionMapIndex = regionMapKey is null ? 0 : 4;
        var colorShiftMaskIndex = colorShiftMaskKey is null
            ? (byte)(regionMapKey is null ? 4 : 5)
            : (byte)(regionMapKey is null ? 4 : 5);

        writer.Write(version);
        var tgiOffsetPosition = stream.Position;
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(internalName);
        writer.Write(0f);
        writer.Write((ushort)0);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(partFlags1);
        writer.Write(partFlags2);
        writer.Write(0ul);
        writer.Write(0ul);
        writer.Write(0ul);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write((byte)0);
        writer.Write(bodyType);
        writer.Write(0);
        writer.Write(ageGenderFlags);
        writer.Write(speciesValue);
        writer.Write((short)0);
        writer.Write((byte)0);
        writer.Write(new byte[9]);
        writer.Write((byte)swatchColors.Count);
        foreach (var swatchColor in swatchColors)
        {
            writer.Write(swatchColor);
        }
        writer.Write((byte)0);
        writer.Write((byte)2);
        writer.Write(0ul);
        writer.Write((byte)0);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0ul);
        writer.Write(0ul);
        for (var sliderIndex = 0; sliderIndex < 11; sliderIndex++)
        {
            writer.Write(0f);
        }

        writer.Write((byte)0);
        writer.Write(nakedKey);
        writer.Write((byte)0);
        writer.Write(0);
        if (includeLodReferences)
        {
            writer.Write((byte)1);
            writer.Write((byte)0);
            writer.Write(0u);
            writer.Write((byte)1);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
        }
        else
        {
            writer.Write((byte)0);
        }
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)0);
        writer.Write((byte)3);
        writer.Write((byte)3);
        writer.Write((byte)0);
        writer.Write((byte)regionMapIndex);
        writer.Write((byte)0);
        writer.Write((byte)3);
        writer.Write((byte)3);
        writer.Write(0);
        writer.Write((byte)3);
        writer.Write((byte)0);
        if (version >= 0x00000031u)
        {
            writer.Write(colorShiftMaskIndex);
        }

        var tgiOffset = (uint)(stream.Position - 8);
        var current = stream.Position;
        stream.Position = tgiOffsetPosition;
        writer.Write(tgiOffset);
        stream.Position = current;

        writer.Write((byte)(4 + (regionMapKey is null ? 0 : 1) + (colorShiftMaskKey is null ? 0 : 1)));
        WriteResourceKey(writer, noneKey);
        WriteResourceKey(writer, geometryKey);
        WriteResourceKey(writer, thumbnailKey);
        WriteResourceKey(writer, textureKey);
        if (regionMapKey is not null)
        {
            WriteResourceKey(writer, regionMapKey);
        }
        if (colorShiftMaskKey is not null)
        {
            WriteResourceKey(writer, colorShiftMaskKey);
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] CreateSyntheticRle2TextureBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(0x35545844u); // DXT5
        writer.Write(0x32454C52u); // RLE2
        writer.Write((ushort)4);   // width
        writer.Write((ushort)4);   // height
        writer.Write((ushort)1);   // mip count
        writer.Write((ushort)0);   // reserved

        writer.Write(36); // command offset
        writer.Write(38); // offset2
        writer.Write(42); // offset3
        writer.Write(46); // offset0
        writer.Write(46); // offset1

        writer.Write((ushort)0x0006); // one opaque block
        writer.Write(new byte[] { 0xFF, 0xFF, 0x00, 0x00 }); // block2
        writer.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // block3

        return stream.ToArray();
    }

    private static string? ReadStructuredMetadataDescription(string typeName, byte[] bytes)
    {
        var extractorType = RequireType(typeof(LlamaResourceCatalogService).Assembly, "Sims4ResourceExplorer.Packages.Ts4StructuredResourceMetadataExtractor");
        var describeMethod = extractorType.GetMethod("Describe", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(describeMethod);

        var metadata = describeMethod!.Invoke(null, [typeName, bytes]);
        Assert.NotNull(metadata);

        var descriptionProperty = metadata!.GetType().GetProperty("Description", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(descriptionProperty);
        return descriptionProperty!.GetValue(metadata) as string;
    }

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

    private static byte[] CreateSyntheticSimInfoBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        var linkTable = new[]
        {
            new ResourceKeyRecord(0x0354796Au, 0u, 0x2000000000000001ul, "Sculpt"),
            new ResourceKeyRecord(0x0354796Au, 0u, 0x2000000000000002ul, "Sculpt"),
            new ResourceKeyRecord(0xB52F5055u, 0u, 0x3000000000000001ul, "SimModifier"),
            new ResourceKeyRecord(0xB52F5055u, 0u, 0x3000000000000002ul, "SimModifier"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000000ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000001ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000002ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000003ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000004ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000005ul, "CASPart"),
            new ResourceKeyRecord(0x0354796Au, 0u, 0x2000000000000010ul, "Sculpt"),
            new ResourceKeyRecord(0xB52F5055u, 0u, 0x3000000000000010ul, "SimModifier"),
            new ResourceKeyRecord(0xB52F5055u, 0u, 0x3000000000000011ul, "SimModifier"),
            new ResourceKeyRecord(0xB52F5055u, 0u, 0x3000000000000012ul, "SimModifier"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x7000000000000000ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x7000000000000001ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x7000000000000002ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x7000000000000003ul, "CASPart")
        };

        writer.Write(32u);
        var offsetPosition = stream.Position;
        writer.Write(0u);

        for (var index = 0; index < 8; index++)
        {
            writer.Write(index / 10f);
        }

        writer.Write(0x00000010u); // Young adult
        writer.Write(0x00002000u); // Female
        writer.Write(1u); // Human
        writer.Write(1u);

        writer.Write(1);
        writer.Write(1u);
        writer.Write("she");

        writer.Write(0x1020304050607080ul);
        writer.Write(0.125f);

        writer.Write((byte)1);
        writer.Write(0x1111111111111111ul);
        writer.Write(0xFFAA8844u);

        writer.Write((byte)2);
        writer.Write((byte)0);
        writer.Write((byte)0);

        writer.Write((byte)2);
        writer.Write((byte)0);
        writer.Write(0.15f);
        writer.Write((byte)0);
        writer.Write(0.45f);

        writer.Write((byte)1);
        writer.Write((byte)0);
        writer.Write(0.25f);

        writer.Write(7u);
        writer.Write(0.55f);
        writer.Write(0x0102030405060708ul);
        writer.Write(0u);
        writer.Write(0u);

        writer.Write(2u);

        writer.Write((byte)1);
        writer.Write(0u);
        writer.Write(1u);
        WriteSyntheticSimOutfitEntry(writer, 2, 4);

        writer.Write((byte)2);
        writer.Write(0u);
        writer.Write(2u);
        WriteSyntheticSimOutfitEntry(writer, 1, 6);
        WriteSyntheticSimOutfitEntry(writer, 3, 7);

        writer.Write((byte)1);
        writer.Write((byte)0);

        writer.Write((byte)1);
        writer.Write((byte)0);
        writer.Write(0.35f);

        writer.Write((byte)2);
        writer.Write((byte)0);
        writer.Write(0.65f);
        writer.Write((byte)0);
        writer.Write(0.95f);

        for (var index = 0; index < 4; index++)
        {
            writer.Write(0.2f + (index / 10f));
        }

        writer.Write((byte)3);
        WriteSyntheticGeneticPart(writer, 9u);
        WriteSyntheticGeneticPart(writer, 12u);
        WriteSyntheticGeneticPart(writer, 24u);

        writer.Write((byte)1);
        WriteSyntheticGeneticPart(writer, 5u);

        writer.Write(4u);
        writer.Write(0.85f);
        writer.Write((byte)1);
        writer.Write(0x0A0B0C0D0E0F1011ul);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);

        writer.Write((byte)4);
        writer.Write(0x2000000000000001ul);
        writer.Write(0x2000000000000002ul);
        writer.Write(0x2000000000000003ul);
        writer.Write(0x2000000000000004ul);

        var tgiOffset = (uint)(stream.Position - offsetPosition - sizeof(uint));
        var endPosition = stream.Position;
        stream.Position = offsetPosition;
        writer.Write(tgiOffset);
        stream.Position = endPosition;
        writer.Write((byte)linkTable.Length);
        foreach (var key in linkTable)
        {
            WriteResourceKey(writer, key);
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] CreateSyntheticSimInfoBytesWithAuthoritativeNudeOutfits()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        var linkTable = new[]
        {
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000000ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000001ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000002ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000003ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000004ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000005ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000006ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000007ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000008ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000009ul, "CASPart")
        };

        writer.Write(32u);
        var offsetPosition = stream.Position;
        writer.Write(0u);

        for (var index = 0; index < 8; index++)
        {
            writer.Write(index / 10f);
        }

        writer.Write(0x00000010u);
        writer.Write(0x00002000u);
        writer.Write(1u);
        writer.Write(1u);

        writer.Write(0);
        writer.Write(0x1020304050607080ul);
        writer.Write(0.125f);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);

        writer.Write(7u);
        writer.Write(0.55f);
        writer.Write(0x0102030405060708ul);
        writer.Write(0u);
        writer.Write(0u);

        writer.Write(2u);

        writer.Write((byte)5);
        writer.Write(0u);
        writer.Write(1u);
        WriteSyntheticSimOutfitEntry(writer, 2, 0);

        writer.Write((byte)5);
        writer.Write(0u);
        writer.Write(2u);
        WriteSyntheticSimOutfitEntry(writer, 1, 3);
        WriteSyntheticSimOutfitEntry(writer, 3, 4);

        writer.Write((byte)1);
        writer.Write((byte)0);

        writer.Write((byte)1);
        writer.Write((byte)0);
        writer.Write(0.35f);

        writer.Write((byte)2);
        writer.Write((byte)0);
        writer.Write(0.65f);
        writer.Write((byte)0);
        writer.Write(0.95f);

        for (var index = 0; index < 4; index++)
        {
            writer.Write(0.2f + (index / 10f));
        }

        writer.Write((byte)3);
        WriteSyntheticGeneticPart(writer, 9u);
        WriteSyntheticGeneticPart(writer, 12u);
        WriteSyntheticGeneticPart(writer, 24u);

        writer.Write((byte)1);
        WriteSyntheticGeneticPart(writer, 5u);

        writer.Write(4u);
        writer.Write(0.85f);
        writer.Write((byte)1);
        writer.Write(0x0A0B0C0D0E0F1011ul);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);

        writer.Write((byte)4);
        writer.Write(0x2000000000000001ul);
        writer.Write(0x2000000000000002ul);
        writer.Write(0x2000000000000003ul);
        writer.Write(0x2000000000000004ul);

        var tgiOffset = (uint)(stream.Position - offsetPosition - sizeof(uint));
        var endPosition = stream.Position;
        stream.Position = offsetPosition;
        writer.Write(tgiOffset);
        stream.Position = endPosition;
        writer.Write((byte)linkTable.Length);
        foreach (var key in linkTable)
        {
            WriteResourceKey(writer, key);
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] CreateSyntheticSimInfoHeaderOnlyBytes(uint ageFlags, uint genderFlags, uint speciesValue)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(32u);
        writer.Write(0u);

        for (var index = 0; index < 8; index++)
        {
            writer.Write(index / 10f);
        }

        writer.Write(ageFlags);
        writer.Write(genderFlags);
        writer.Write(speciesValue);
        writer.Write(1u);
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] CreateSyntheticSimInfoBytesWithEverydayAndNudeBodyPartLinks()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        var linkTable = new[]
        {
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000030ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000031ul, "CASPart")
        };

        writer.Write(32u);
        var offsetPosition = stream.Position;
        writer.Write(0u);

        for (var index = 0; index < 8; index++)
        {
            writer.Write(index / 10f);
        }

        writer.Write(0x00000010u);
        writer.Write(0x00002000u);
        writer.Write(1u);
        writer.Write(1u);

        writer.Write(0);
        writer.Write(0x1020304050607080ul);
        writer.Write(0.125f);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);

        writer.Write(7u);
        writer.Write(0.55f);
        writer.Write(0x0102030405060708ul);
        writer.Write(0u);
        writer.Write(0u);

        writer.Write(2u);

        writer.Write((byte)0);
        writer.Write(0u);
        writer.Write(1u);
        writer.Write(0x3000000000000001ul);
        writer.Write(0x4000000000000002ul);
        writer.Write(0x5000000000000003ul);
        writer.Write(false);
        writer.Write(1u);
        writer.Write((byte)0);
        writer.Write(5u);
        writer.Write(0x6000000000000030ul);

        writer.Write((byte)5);
        writer.Write(0u);
        writer.Write(1u);
        writer.Write(0x3000000000000004ul);
        writer.Write(0x4000000000000005ul);
        writer.Write(0x5000000000000006ul);
        writer.Write(false);
        writer.Write(1u);
        writer.Write((byte)1);
        writer.Write(5u);
        writer.Write(0x6000000000000031ul);

        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        for (var index = 0; index < 16; index++)
        {
            writer.Write((byte)0);
        }
        writer.Write((byte)0);
        writer.Write((byte)0);
        for (var index = 0; index < 17; index++)
        {
            writer.Write((byte)0);
        }
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);

        var tgiOffset = (uint)(stream.Position - offsetPosition - sizeof(uint));
        var endPosition = stream.Position;
        stream.Position = offsetPosition;
        writer.Write(tgiOffset);
        stream.Position = endPosition;
        writer.Write((byte)linkTable.Length);
        foreach (var key in linkTable)
        {
            WriteResourceKey(writer, key);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] CreateSyntheticSimInfoBytesWithAuthoritativeSplitNudeOutfits()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        var linkTable = new[]
        {
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6100000000000000ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6100000000000001ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6100000000000002ul, "CASPart")
        };

        writer.Write(32u);
        var offsetPosition = stream.Position;
        writer.Write(0u);

        for (var index = 0; index < 8; index++)
        {
            writer.Write(index / 10f);
        }

        writer.Write(0x00000010u);
        writer.Write(0x00002000u);
        writer.Write(1u);
        writer.Write(1u);

        writer.Write(0);
        writer.Write(0x1020304050607080ul);
        writer.Write(0.125f);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);

        writer.Write(7u);
        writer.Write(0.55f);
        writer.Write(0x0102030405060708ul);
        writer.Write(0u);
        writer.Write(0u);

        writer.Write(1u);

        writer.Write((byte)5);
        writer.Write(0u);
        writer.Write(1u);
        writer.Write(0x3000000000000001ul);
        writer.Write(0x4000000000000002ul);
        writer.Write(0x5000000000000003ul);
        writer.Write(false);
        writer.Write(3u);
        writer.Write((byte)0);
        writer.Write(6u);
        writer.Write(0x6100000000000000ul);
        writer.Write((byte)1);
        writer.Write(7u);
        writer.Write(0x6100000000000001ul);
        writer.Write((byte)2);
        writer.Write(8u);
        writer.Write(0x6100000000000002ul);

        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        for (var index = 0; index < 16; index++)
        {
            writer.Write((byte)0);
        }
        writer.Write((byte)0);
        writer.Write((byte)0);
        for (var index = 0; index < 17; index++)
        {
            writer.Write((byte)0);
        }
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);

        var tgiOffset = (uint)(stream.Position - offsetPosition - sizeof(uint));
        var endPosition = stream.Position;
        stream.Position = offsetPosition;
        writer.Write(tgiOffset);
        stream.Position = endPosition;
        writer.Write((byte)linkTable.Length);
        foreach (var key in linkTable)
        {
            WriteResourceKey(writer, key);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] CreateSyntheticSimInfoBytesWithHeadPartLinks()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        var linkTable = new[]
        {
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000010ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000011ul, "CASPart")
        };

        writer.Write(32u);
        var offsetPosition = stream.Position;
        writer.Write(0u);

        for (var index = 0; index < 8; index++)
        {
            writer.Write(index / 10f);
        }

        writer.Write(0x00000010u);
        writer.Write(0x00002000u);
        writer.Write(1u);
        writer.Write(1u);

        writer.Write(0);
        writer.Write(0x1020304050607080ul);
        writer.Write(0.125f);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);

        writer.Write(7u);
        writer.Write(0.55f);
        writer.Write(0x0102030405060708ul);
        writer.Write(0u);
        writer.Write(0u);

        writer.Write(1u);
        writer.Write((byte)5);
        writer.Write(0u);
        writer.Write(1u);
        writer.Write(0x3000000000000001ul);
        writer.Write(0x4000000000000002ul);
        writer.Write(0x5000000000000003ul);
        writer.Write(false);
        writer.Write(2u);
        writer.Write((byte)0);
        writer.Write(2u);
        writer.Write(0x6000000000000010ul);
        writer.Write((byte)1);
        writer.Write(12u);
        writer.Write(0x6000000000000011ul);

        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        for (var index = 0; index < 16; index++)
        {
            writer.Write((byte)0);
        }
        writer.Write((byte)0);
        writer.Write((byte)0);
        for (var index = 0; index < 17; index++)
        {
            writer.Write((byte)0);
        }
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);

        var tgiOffset = (uint)(stream.Position - offsetPosition - sizeof(uint));
        var endPosition = stream.Position;
        stream.Position = offsetPosition;
        writer.Write(tgiOffset);
        stream.Position = endPosition;
        writer.Write((byte)linkTable.Length);
        foreach (var key in linkTable)
        {
            WriteResourceKey(writer, key);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] CreateSyntheticSimInfoBytesWithoutOutfits(uint ageFlags, uint genderFlags, uint speciesValue)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(32u);
        var offsetPosition = stream.Position;
        writer.Write(0u);

        for (var index = 0; index < 8; index++)
        {
            writer.Write(0f);
        }

        writer.Write(ageFlags);
        writer.Write(genderFlags);
        writer.Write(speciesValue);
        writer.Write(1u);

        writer.Write(0);
        writer.Write(0ul);
        writer.Write(0f);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);

        writer.Write(0u);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);

        writer.Write(0u);

        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);

        for (var index = 0; index < 16; index++)
        {
            writer.Write((byte)0);
        }

        writer.Write((byte)0);
        writer.Write((byte)0);
        for (var index = 0; index < 17; index++)
        {
            writer.Write((byte)0);
        }

        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);

        var tgiOffset = (uint)(stream.Position - offsetPosition - sizeof(uint));
        var endPosition = stream.Position;
        stream.Position = offsetPosition;
        writer.Write(tgiOffset);
        stream.Position = endPosition;
        writer.Write((byte)0);

        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] CreateSyntheticSimInfoBytesWithSingleNonNudeOutfit()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        var linkTable = new[]
        {
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000020ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000021ul, "CASPart")
        };

        writer.Write(32u);
        var offsetPosition = stream.Position;
        writer.Write(0u);

        for (var index = 0; index < 8; index++)
        {
            writer.Write(0f);
        }

        writer.Write(0x00000004u); // Child
        writer.Write(0x00001000u); // Male
        writer.Write(1u); // Human
        writer.Write(1u);

        writer.Write(0);
        writer.Write(0x1020304050607080ul);
        writer.Write(0f);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);

        writer.Write(0u);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);

        writer.Write(1u);
        writer.Write((byte)1);
        writer.Write(0u);
        writer.Write(1u);
        writer.Write(0x3000000000000001ul);
        writer.Write(0x4000000000000002ul);
        writer.Write(0x5000000000000003ul);
        writer.Write(false);
        writer.Write(2u);
        writer.Write((byte)0);
        writer.Write(5u);
        writer.Write(0x6000000000000020ul);
        writer.Write((byte)1);
        writer.Write(3u);
        writer.Write(0x6000000000000021ul);

        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        for (var index = 0; index < 16; index++)
        {
            writer.Write((byte)0);
        }
        writer.Write((byte)0);
        writer.Write((byte)0);
        for (var index = 0; index < 17; index++)
        {
            writer.Write((byte)0);
        }
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);

        var tgiOffset = (uint)(stream.Position - offsetPosition - sizeof(uint));
        var endPosition = stream.Position;
        stream.Position = offsetPosition;
        writer.Write(tgiOffset);
        stream.Position = endPosition;
        writer.Write((byte)linkTable.Length);
        foreach (var key in linkTable)
        {
            WriteResourceKey(writer, key);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] CreateSyntheticSimInfoBytesWithBodyAndHeadPartLinks()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        var linkTable = new[]
        {
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000020ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000021ul, "CASPart")
        };

        writer.Write(32u);
        var offsetPosition = stream.Position;
        writer.Write(0u);

        for (var index = 0; index < 8; index++)
        {
            writer.Write(index / 10f);
        }

        writer.Write(0x00000010u);
        writer.Write(0x00002000u);
        writer.Write(1u);
        writer.Write(1u);

        writer.Write(0);
        writer.Write(0x1020304050607080ul);
        writer.Write(0.125f);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);

        writer.Write(7u);
        writer.Write(0.55f);
        writer.Write(0x0102030405060708ul);
        writer.Write(0u);
        writer.Write(0u);

        writer.Write(1u);
        writer.Write((byte)5);
        writer.Write(0u);
        writer.Write(1u);
        writer.Write(0x3000000000000001ul);
        writer.Write(0x4000000000000002ul);
        writer.Write(0x5000000000000003ul);
        writer.Write(false);
        writer.Write(2u);
        writer.Write((byte)0);
        writer.Write(5u);
        writer.Write(0x6000000000000020ul);
        writer.Write((byte)1);
        writer.Write(3u);
        writer.Write(0x6000000000000021ul);

        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        for (var index = 0; index < 16; index++)
        {
            writer.Write((byte)0);
        }
        writer.Write((byte)0);
        writer.Write((byte)0);
        for (var index = 0; index < 17; index++)
        {
            writer.Write((byte)0);
        }
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);

        var tgiOffset = (uint)(stream.Position - offsetPosition - sizeof(uint));
        var endPosition = stream.Position;
        stream.Position = offsetPosition;
        writer.Write(tgiOffset);
        stream.Position = endPosition;
        writer.Write((byte)linkTable.Length);
        foreach (var key in linkTable)
        {
            WriteResourceKey(writer, key);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] CreateSyntheticSimInfoV27Bytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        var linkTable = new[]
        {
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000000ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000001ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000002ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000003ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000004ul, "CASPart"),
            new ResourceKeyRecord(0x034AEECBu, 0u, 0x6000000000000005ul, "CASPart")
        };

        writer.Write(27u);
        var offsetPosition = stream.Position;
        writer.Write(0u);

        for (var index = 0; index < 8; index++)
        {
            writer.Write(index / 10f);
        }

        writer.Write(0x00000010u); // Young adult
        writer.Write(0x00002000u); // Female
        writer.Write(1u); // Human
        writer.Write(1u);

        writer.Write(0x1020304050607080ul);

        writer.Write((byte)0);

        writer.Write((byte)0); // sculpts
        writer.Write((byte)0); // face modifiers
        writer.Write((byte)0); // body modifiers

        writer.Write(7u);
        writer.Write(0.55f);
        writer.Write(0x0102030405060708ul);
        writer.Write(0u);
        writer.Write(0u);

        writer.Write(1u);
        writer.Write((byte)1);
        writer.Write(0u);
        writer.Write(1u);
        WriteSyntheticSimOutfitEntryV27(writer, 6, 0);

        writer.Write((byte)0); // genetic sculpts
        writer.Write((byte)0); // genetic face modifiers
        writer.Write((byte)0); // genetic body modifiers

        for (var index = 0; index < 4; index++)
        {
            writer.Write(0.2f + (index / 10f));
        }

        writer.Write((byte)0); // genetic parts
        writer.Write(4u);
        writer.Write(0.85f);
        writer.Write((byte)1);
        writer.Write(0x0A0B0C0D0E0F1011ul);
        writer.Write((byte)4);
        writer.Write(0x2000000000000001ul);
        writer.Write(0x2000000000000002ul);
        writer.Write(0x2000000000000003ul);
        writer.Write(0x2000000000000004ul);

        var tgiOffset = (uint)(stream.Position - offsetPosition - sizeof(uint));
        var endPosition = stream.Position;
        stream.Position = offsetPosition;
        writer.Write(tgiOffset);
        stream.Position = endPosition;
        writer.Write((byte)linkTable.Length);
        foreach (var key in linkTable)
        {
            WriteResourceKey(writer, key);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteSyntheticSimOutfitEntry(BinaryWriter writer, uint partCount, byte startLinkIndex)
    {
        writer.Write(0x3000000000000001ul);
        writer.Write(0x4000000000000002ul);
        writer.Write(0x5000000000000003ul);
        writer.Write(false);
        writer.Write(partCount);
        var defaultBodyTypes = new uint[] { 5, 6, 8, 12, 2, 7 };
        for (var index = 0; index < (int)partCount; index++)
        {
            writer.Write((byte)(startLinkIndex + index));
            writer.Write(defaultBodyTypes[index % defaultBodyTypes.Length]);
            writer.Write(0x6000000000000000ul + (ulong)index);
        }
    }

    private static void WriteSyntheticSimOutfitEntryV27(BinaryWriter writer, uint partCount, byte startLinkIndex)
    {
        writer.Write(0x3000000000000001ul);
        writer.Write(0x4000000000000002ul);
        writer.Write(0x5000000000000003ul);
        writer.Write(false);
        writer.Write(partCount);
        var defaultBodyTypes = new uint[] { 5, 6, 8, 12, 2, 7 };
        for (var index = 0; index < (int)partCount; index++)
        {
            writer.Write((byte)(startLinkIndex + index));
            writer.Write(defaultBodyTypes[index % defaultBodyTypes.Length]);
        }
    }

    private static void WriteSyntheticGeneticPart(BinaryWriter writer, uint bodyType)
    {
        writer.Write((byte)0);
        writer.Write(bodyType);
    }

    private static byte[] CreateSyntheticCasPartBytesWithTruncatedBody(ResourceKeyRecord geometryKey, ResourceKeyRecord thumbnailKey, ResourceKeyRecord textureKey)
    {
        var bytes = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, "BrokenCasPart");
        var bodyStartOffset = FindSyntheticCasPartBodyStartOffset(bytes);
        return bytes[..(bodyStartOffset + 2)];
    }

    private static byte[] CreateSyntheticCasPartBytesWithTruncatedManagedString(ResourceKeyRecord geometryKey, ResourceKeyRecord thumbnailKey, ResourceKeyRecord textureKey)
    {
        var bytes = CreateSyntheticCasPartBytes(geometryKey, thumbnailKey, textureKey, "BrokenCasPart");
        return bytes[..20];
    }

    private static int FindSyntheticCasPartBodyStartOffset(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        var stringLength = reader.ReadByte();
        stream.Position += stringLength;
        return checked((int)stream.Position);
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

    private static byte[] CreateSyntheticVersion15GeometryBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write("GEOM"u8.ToArray());
        writer.Write(0x0000000Fu);
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

        writer.Write(1);
        writer.Write(0u);
        writer.Write(1);
        writer.Write(0f);
        writer.Write(0f);

        writer.Write(1);
        writer.Write(0u);
        writer.Write((ushort)0x1000);

        writer.Write(1);
        writer.Write(0u);
        writer.Write((short)0);
        writer.Write((short)1);
        writer.Write((short)2);
        writer.Write(0.25f);
        writer.Write(0.5f);
        writer.Write(1f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(1f);
        writer.Write(0x11111111u);

        writer.Write(2);
        writer.Write(0x11111111u);
        writer.Write(0x22222222u);

        writer.Write(1);
        writer.Write(0u);
        writer.Write(0);
        writer.Write(0);
        writer.Write(3);
        writer.Write(1);

        writer.Write(0);
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] CreateSyntheticSkinnedGeometryBytesWithSecondaryUvSet()
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
        writer.Write(6);
        WriteGeomFormat(writer, 0x01);
        WriteGeomFormat(writer, 0x02);
        WriteGeomFormat(writer, 0x03);
        WriteGeomFormat(writer, 0x03);
        WriteGeomFormat(writer, 0x04);
        WriteGeomFormat(writer, 0x05);

        WriteGeomVertex(writer, [0f, 0f, 0f], [0f, 0f, 1f], [0f, 0f], [0.25f, 0.25f], [0, 1, 0, 0], [255, 0, 0, 0]);
        WriteGeomVertex(writer, [0f, 1f, 0f], [0f, 0f, 1f], [0f, 1f], [0.25f, 0.75f], [0, 1, 0, 0], [128, 127, 0, 0]);
        WriteGeomVertex(writer, [1f, 0f, 0f], [0f, 0f, 1f], [1f, 0f], [0.75f, 0.25f], [1, 0, 0, 0], [255, 0, 0, 0]);

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

    private static byte[] WrapSingleChunkRcol(byte[] chunkBytes)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(3u);
        writer.Write(0);
        writer.Write(0u);
        writer.Write(0);
        writer.Write(1);
        writer.Write(0ul);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0);
        writer.Write(chunkBytes);
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

    private static byte[] CreateSyntheticGeometryBytesWithNegativeFormatCount()
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
        writer.Write(0);
        writer.Write(-1);
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

    private static void WriteGeomVertex(BinaryWriter writer, float[] position, float[] normal, float[] uv0, float[] uv1, byte[] blendIndices, byte[] blendWeights)
    {
        foreach (var value in position)
        {
            writer.Write(value);
        }

        foreach (var value in normal)
        {
            writer.Write(value);
        }

        foreach (var value in uv0)
        {
            writer.Write(value);
        }

        foreach (var value in uv1)
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
        writer.Write(key.FullInstance);
        writer.Write(key.Group);
        writer.Write(key.Type);
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
        private readonly IReadOnlyDictionary<string, Exception> textureExceptionsByTgi;

        public FakeResourceCatalogService(
            IReadOnlyDictionary<string, byte[]> bytesByTgi,
            IReadOnlyDictionary<string, string?> textByTgi,
            IReadOnlyDictionary<string, Exception>? textureExceptionsByTgi = null)
        {
            this.bytesByTgi = bytesByTgi;
            this.textByTgi = textByTgi;
            this.textureExceptionsByTgi = textureExceptionsByTgi ?? new Dictionary<string, Exception>();
        }

        public Task<PackageScanResult> ScanPackageAsync(DataSourceDefinition source, string packagePath, IProgress<PackageScanProgress>? progress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ResourceMetadata> EnrichResourceAsync(ResourceMetadata resource, CancellationToken cancellationToken) =>
            Task.FromResult(resource);

        public Task<byte[]> GetResourceBytesAsync(string packagePath, ResourceKeyRecord key, bool raw, CancellationToken cancellationToken, IProgress<ResourceReadProgress>? progress = null) =>
            Task.FromResult(bytesByTgi.TryGetValue(BuildPackageScopedKey(packagePath, key), out var packageBytes)
                ? packageBytes
                : bytesByTgi[key.FullTgi]);

        public Task<string?> GetTextAsync(string packagePath, ResourceKeyRecord key, CancellationToken cancellationToken) =>
            Task.FromResult(
                textByTgi.TryGetValue(BuildPackageScopedKey(packagePath, key), out var packageValue)
                    ? packageValue
                    : textByTgi.TryGetValue(key.FullTgi, out var value)
                        ? value
                        : null);

        public Task<byte[]?> GetTexturePngAsync(string packagePath, ResourceKeyRecord key, CancellationToken cancellationToken, IProgress<ResourceReadProgress>? progress = null)
        {
            if (textureExceptionsByTgi.TryGetValue(BuildPackageScopedKey(packagePath, key), out var packageException))
            {
                return Task.FromException<byte[]?>(packageException);
            }

            if (textureExceptionsByTgi.TryGetValue(key.FullTgi, out var exception))
            {
                return Task.FromException<byte[]?>(exception);
            }

                return Task.FromResult<byte[]?>(
                    bytesByTgi.TryGetValue(BuildPackageScopedKey(packagePath, key), out var packageBytes)
                        ? packageBytes
                        : bytesByTgi[key.FullTgi]);
        }

        private static string BuildPackageScopedKey(string packagePath, ResourceKeyRecord key) =>
            $"{packagePath}|{key.FullTgi}";
    }

    private sealed class TrackingEnrichmentCatalogService(ResourceMetadata enrichedResource) : IResourceCatalogService
    {
        public int EnrichmentCalls { get; private set; }

        public Task<PackageScanResult> ScanPackageAsync(DataSourceDefinition source, string packagePath, IProgress<PackageScanProgress>? progress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ResourceMetadata> EnrichResourceAsync(ResourceMetadata resource, CancellationToken cancellationToken)
        {
            EnrichmentCalls++;
            return Task.FromResult(enrichedResource);
        }

        public Task<byte[]> GetResourceBytesAsync(string packagePath, ResourceKeyRecord key, bool raw, CancellationToken cancellationToken, IProgress<ResourceReadProgress>? progress = null) =>
            throw new NotSupportedException();

        public Task<string?> GetTextAsync(string packagePath, ResourceKeyRecord key, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<byte[]?> GetTexturePngAsync(string packagePath, ResourceKeyRecord key, CancellationToken cancellationToken, IProgress<ResourceReadProgress>? progress = null) =>
            throw new NotSupportedException();
    }

    private sealed class FakeSceneBuildService(SceneBuildResult result) : ISceneBuildService
    {
        public Task<SceneBuildResult> BuildSceneAsync(ResourceMetadata resource, CancellationToken cancellationToken, IProgress<PreviewBuildProgress>? progress = null) =>
            Task.FromResult(result);

        public Task<SceneBuildResult> BuildSceneAsync(CasAssetGraph casGraph, CancellationToken cancellationToken, IProgress<PreviewBuildProgress>? progress = null) =>
            Task.FromResult(result);
    }

    private sealed class FakeGraphIndexStore : IIndexStore
    {
        private readonly IReadOnlyList<ResourceMetadata> resources;
        private readonly IReadOnlyList<ResourceMetadata> queryResources;
        private readonly IReadOnlyList<AssetSummary> assets;
        private readonly IReadOnlyList<SimTemplateFactSummary> simTemplateFacts;
        private readonly IReadOnlyList<SimTemplateBodyPartFact> simTemplateBodyPartFacts;
        private readonly bool throwOnQueryResources;

        public FakeGraphIndexStore(
            IReadOnlyList<ResourceMetadata> resources,
            IReadOnlyList<AssetSummary>? assets = null,
            IReadOnlyList<ResourceMetadata>? queryResources = null,
            IReadOnlyList<SimTemplateFactSummary>? simTemplateFacts = null,
            IReadOnlyList<SimTemplateBodyPartFact>? simTemplateBodyPartFacts = null,
            bool throwOnQueryResources = false)
        {
            this.resources = resources;
            this.queryResources = queryResources ?? resources;
            this.assets = assets ?? [];
            this.simTemplateFacts = simTemplateFacts ?? [];
            this.simTemplateBodyPartFacts = simTemplateBodyPartFacts ?? [];
            this.throwOnQueryResources = throwOnQueryResources;
        }

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpsertDataSourcesAsync(IEnumerable<DataSourceDefinition> sources, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyDictionary<string, PackageFingerprint>> LoadPackageFingerprintsAsync(IEnumerable<Guid> dataSourceIds, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyDictionary<string, PackageFingerprint>>(new Dictionary<string, PackageFingerprint>());
        public Task<bool> NeedsRescanAsync(Guid dataSourceId, string packagePath, long fileSize, DateTimeOffset lastWriteTimeUtc, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task ReplacePackageAsync(PackageScanResult packageScan, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IIndexWriteSession> OpenWriteSessionAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IIndexWriteSession> OpenRebuildSessionAsync(IEnumerable<DataSourceDefinition> sources, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WindowedQueryResult<ResourceMetadata>> QueryResourcesAsync(RawResourceBrowserQuery query, CancellationToken cancellationToken)
        {
            if (throwOnQueryResources)
            {
                throw new NotSupportedException();
            }

            var filtered = queryResources
                .Where(resource => query.SourceScope.ToSourceKinds().Contains(resource.SourceKind))
                .Where(resource => string.IsNullOrWhiteSpace(query.TypeNameText) || resource.Key.TypeName.Contains(query.TypeNameText, StringComparison.OrdinalIgnoreCase))
                .Where(resource => string.IsNullOrWhiteSpace(query.PackageText) || resource.PackagePath.Contains(query.PackageText, StringComparison.OrdinalIgnoreCase))
                .Where(resource => string.IsNullOrWhiteSpace(query.GroupHexText) || resource.Key.Group.ToString("X8").Contains(query.GroupHexText, StringComparison.OrdinalIgnoreCase))
                .Where(resource => string.IsNullOrWhiteSpace(query.InstanceHexText) || resource.Key.FullInstance.ToString("X16").Contains(query.InstanceHexText, StringComparison.OrdinalIgnoreCase))
                .Where(resource => MatchesSearch(resource, query.SearchText))
                .OrderBy(resource => resource.Key.TypeName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(resource => resource.PackagePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var window = filtered
                .Skip(query.Offset)
                .Take(query.WindowSize)
                .ToArray();
            return Task.FromResult(new WindowedQueryResult<ResourceMetadata>(window, filtered.Length, query.Offset, query.WindowSize));
        }
        public Task<WindowedQueryResult<AssetSummary>> QueryAssetsAsync(AssetBrowserQuery query, CancellationToken cancellationToken)
        {
            var filtered = assets
                .Where(asset => query.SourceScope.ToSourceKinds().Contains(asset.SourceKind))
                .Where(asset => asset.AssetKind == query.Domain switch
                {
                    AssetBrowserDomain.BuildBuy => AssetKind.BuildBuy,
                    AssetBrowserDomain.Cas => AssetKind.Cas,
                    AssetBrowserDomain.Sim => AssetKind.Sim,
                    AssetBrowserDomain.General3D => AssetKind.General3D,
                    _ => asset.AssetKind
                })
                .Where(asset => string.IsNullOrWhiteSpace(query.CategoryText) || ((asset.Category ?? string.Empty).Contains(query.CategoryText, StringComparison.OrdinalIgnoreCase)))
                .Where(asset => MatchesSearch(asset, query.SearchText))
                .OrderBy(asset => asset.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var window = filtered
                .Skip(query.Offset)
                .Take(query.WindowSize)
                .ToArray();
            return Task.FromResult(new WindowedQueryResult<AssetSummary>(window, filtered.Length, query.Offset, query.WindowSize));
        }
        public Task<IReadOnlyList<DataSourceDefinition>> GetDataSourcesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DataSourceDefinition>>([]);
        public Task<AssetFacetOptions> GetAssetFacetOptionsAsync(AssetKind assetKind, CancellationToken cancellationToken) => Task.FromResult(new AssetFacetOptions([], [], [], [], []));
        public Task<IReadOnlyList<IndexedPackageRecord>> GetIndexedPackagesAsync(IEnumerable<Guid> dataSourceIds, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<IndexedPackageRecord>>([]);
        public Task<IReadOnlyList<ResourceMetadata>> GetPackageResourcesAsync(string packagePath, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ResourceMetadata>>([]);
        public Task<IReadOnlyList<ResourceMetadata>> GetResourcesByInstanceAsync(string packagePath, ulong fullInstance, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ResourceMetadata>>(resources.Where(resource => resource.PackagePath == packagePath && resource.Key.FullInstance == fullInstance).ToArray());
        public Task<IReadOnlyList<ResourceMetadata>> GetResourcesByFullInstanceAsync(ulong fullInstance, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ResourceMetadata>>(resources.Where(resource => resource.Key.FullInstance == fullInstance).ToArray());
        public Task<IReadOnlyList<ResourceMetadata>> GetCasPartResourcesByInstancesAsync(IEnumerable<ulong> fullInstances, CancellationToken cancellationToken)
        {
            var instances = fullInstances.ToHashSet();
            return Task.FromResult<IReadOnlyList<ResourceMetadata>>(resources
                .Where(resource =>
                    string.Equals(resource.Key.TypeName, "CASPart", StringComparison.OrdinalIgnoreCase) &&
                    instances.Contains(resource.Key.FullInstance))
                .ToArray());
        }
        public Task<IReadOnlyList<AssetSummary>> GetPackageAssetsAsync(string packagePath, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AssetSummary>>([]);
        public Task<AssetSummary?> GetPackageAssetByIdAsync(string packagePath, Guid assetId, CancellationToken cancellationToken) =>
            Task.FromResult<AssetSummary?>(assets.FirstOrDefault(asset =>
                string.Equals(asset.PackagePath, packagePath, StringComparison.OrdinalIgnoreCase) &&
                asset.Id == assetId));
        public Task<IReadOnlyList<AssetVariantSummary>> GetAssetVariantsAsync(Guid assetId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AssetVariantSummary>>([]);
        public Task<ResourceMetadata?> GetResourceByTgiAsync(string packagePath, string fullTgi, CancellationToken cancellationToken) =>
            Task.FromResult<ResourceMetadata?>(resources.FirstOrDefault(resource =>
                string.Equals(resource.PackagePath, packagePath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(resource.Key.FullTgi, fullTgi, StringComparison.OrdinalIgnoreCase)));
        public Task<IReadOnlyList<ResourceMetadata>> GetResourcesByTgiAsync(string fullTgi, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ResourceMetadata>>(resources.Where(resource => string.Equals(resource.Key.FullTgi, fullTgi, StringComparison.OrdinalIgnoreCase)).ToArray());
        public Task<IReadOnlyList<AssetSummary>> GetIndexedDefaultBodyRecipeAssetsAsync(SimInfoSummary metadata, string slotCategory, CancellationToken cancellationToken)
        {
            var expectedBodyType = slotCategory switch
            {
                "Full Body" => 5,
                "Body" => 5,
                "Top" => 6,
                "Bottom" => 7,
                "Shoes" => 8,
                _ => -1
            };
            if (expectedBodyType < 0)
            {
                return Task.FromResult<IReadOnlyList<AssetSummary>>([]);
            }

            var filtered = assets
                .Where(asset => asset.AssetKind == AssetKind.Cas)
                .Where(asset => string.Equals(asset.Category, slotCategory, StringComparison.OrdinalIgnoreCase))
                .Where(asset =>
                {
                    var facts = Ts4SeedMetadataExtractor.TryExtractCasPartSummaryMetadata(asset.Description, asset.DisplayName, asset.Category);
                    if (facts is null || facts.BodyType != expectedBodyType)
                    {
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(facts.SpeciesLabel) &&
                        !string.Equals(facts.SpeciesLabel, metadata.SpeciesLabel, StringComparison.OrdinalIgnoreCase) &&
                        !(string.Equals(metadata.SpeciesLabel, "Little Dog", StringComparison.OrdinalIgnoreCase) &&
                          string.Equals(metadata.AgeLabel, "Child", StringComparison.OrdinalIgnoreCase) &&
                          string.Equals(facts.SpeciesLabel, "Dog", StringComparison.OrdinalIgnoreCase)))
                    {
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(facts.AgeLabel) &&
                        !string.Equals(facts.AgeLabel, "Unknown", StringComparison.OrdinalIgnoreCase) &&
                        !facts.AgeLabel.Contains(metadata.AgeLabel, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(facts.GenderLabel) &&
                        !string.Equals(facts.GenderLabel, "Unknown", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(facts.GenderLabel, "Unisex", StringComparison.OrdinalIgnoreCase) &&
                        !facts.GenderLabel.Contains(metadata.GenderLabel, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    return facts.HasNakedLink ||
                           facts.DefaultForBodyType ||
                           (string.Equals(metadata.GenderLabel, "Female", StringComparison.OrdinalIgnoreCase) && facts.DefaultForBodyTypeFemale) ||
                           (string.Equals(metadata.GenderLabel, "Male", StringComparison.OrdinalIgnoreCase) && facts.DefaultForBodyTypeMale);
                })
                .OrderBy(asset => asset.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return Task.FromResult<IReadOnlyList<AssetSummary>>(filtered);
        }
        public Task<IReadOnlyList<SimTemplateFactSummary>> GetSimTemplateFactsByArchetypeAsync(string archetypeKey, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<SimTemplateFactSummary>>(simTemplateFacts.Where(fact => string.Equals(fact.ArchetypeKey, archetypeKey, StringComparison.OrdinalIgnoreCase)).ToArray());
        public Task<IReadOnlyList<SimTemplateBodyPartFact>> GetSimTemplateBodyPartFactsAsync(Guid resourceId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<SimTemplateBodyPartFact>>(simTemplateBodyPartFacts.Where(fact => fact.ResourceId == resourceId).ToArray());
        public Task UpdatePackageAssetsAsync(Guid dataSourceId, string packagePath, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken) => Task.CompletedTask;

        private static bool MatchesSearch(AssetSummary asset, string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }

            var haystack = string.Join(
                " ",
                asset.DisplayName,
                asset.Category ?? string.Empty,
                asset.Description ?? string.Empty,
                asset.PackagePath,
                asset.RootKey.FullTgi);
            return searchText
                .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .All(term => haystack.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        private static bool MatchesSearch(ResourceMetadata resource, string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }

            var haystack = string.Join(
                " ",
                resource.Key.TypeName,
                resource.Key.FullTgi,
                resource.Name ?? string.Empty,
                resource.Description ?? string.Empty,
                resource.PackagePath);
            return searchText
                .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .All(term => haystack.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
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
