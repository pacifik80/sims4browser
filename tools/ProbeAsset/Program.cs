using LlamaLogic.Packages;
using Sims4ResourceExplorer.Assets;
using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Indexing;
using Sims4ResourceExplorer.Packages;
using Sims4ResourceExplorer.Preview;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

if (args.Length > 0 && string.Equals(args[0], "--find-root", StringComparison.OrdinalIgnoreCase))
{
    var searchRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    var rootTgiToFind = args.Length > 2 ? args[2] : "01661233:00000000:00F643B0FDD2F1F7";
    return await FindBuildBuyAssetAsync(searchRoot, rootTgiToFind);
}

if (args.Length > 0 && string.Equals(args[0], "--find-resource", StringComparison.OrdinalIgnoreCase))
{
    var searchRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    var resourceTgiToFind = args.Length > 2 ? args[2] : "00B2D882:00000000:02B59E39A0F574BE";
    return await FindResourceAsync(searchRoot, resourceTgiToFind);
}

if (args.Length > 0 && string.Equals(args[0], "--inspect-resource", StringComparison.OrdinalIgnoreCase))
{
    var packagePathToInspect = args.Length > 1 ? args[1] : string.Empty;
    var resourceTgiToInspect = args.Length > 2 ? args[2] : string.Empty;
    return await InspectResourceAsync(packagePathToInspect, resourceTgiToInspect);
}

if (args.Length > 0 && string.Equals(args[0], "--find-stbl-hash", StringComparison.OrdinalIgnoreCase))
{
    var searchRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    var hashText = args.Length > 2 ? args[2] : string.Empty;
    var maxPackages = args.Length > 3 && int.TryParse(args[3], out var parsedMaxPackages) ? parsedMaxPackages : 0;
    return await FindStringTableHashAsync(searchRoot, hashText, maxPackages);
}

if (args.Length > 0 && string.Equals(args[0], "--batch-coverage", StringComparison.OrdinalIgnoreCase))
{
    var inputPath = args.Length > 1 ? args[1] : Path.Combine(Environment.CurrentDirectory, "tmp", "probe_batch.txt");
    return await RunBatchCoverageAsync(inputPath);
}

if (args.Length > 0 && string.Equals(args[0], "--batch-coverage-summary", StringComparison.OrdinalIgnoreCase))
{
    var inputPath = args.Length > 1 ? args[1] : Path.Combine(Environment.CurrentDirectory, "tmp", "probe_batch.txt");
    var summaryPath = args.Length > 2 ? args[2] : Path.Combine(Environment.CurrentDirectory, "tmp", "probe_batch_summary.json");
    var maxEntries = args.Length > 3 && int.TryParse(args[3], out var parsedMaxEntries) ? parsedMaxEntries : 0;
    var timeoutSeconds = args.Length > 4 && int.TryParse(args[4], out var parsedTimeoutSeconds) ? parsedTimeoutSeconds : 120;
    return await RunBatchCoverageSummaryAsync(inputPath, summaryPath, maxEntries, timeoutSeconds);
}

if (args.Length > 0 && string.Equals(args[0], "--probe-json", StringComparison.OrdinalIgnoreCase))
{
    var probePackagePath = args.Length > 1 ? args[1] : string.Empty;
    var probeRootTgi = args.Length > 2 ? args[2] : string.Empty;
    return await RunProbeJsonAsync(probePackagePath, probeRootTgi);
}

if (args.Length > 0 && string.Equals(args[0], "--sample-buildbuy", StringComparison.OrdinalIgnoreCase))
{
    var searchRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    var maxPackages = args.Length > 2 && int.TryParse(args[2], out var parsedMaxPackages) ? parsedMaxPackages : 8;
    var assetsPerPackage = args.Length > 3 && int.TryParse(args[3], out var parsedAssetsPerPackage) ? parsedAssetsPerPackage : 5;
    var outputPath = args.Length > 4 ? args[4] : Path.Combine(Environment.CurrentDirectory, "tmp", "probe_sample_batch.txt");
    return await SampleBuildBuyRootsAsync(searchRoot, maxPackages, assetsPerPackage, outputPath);
}

if (args.Length > 0 && string.Equals(args[0], "--find-complex-buildbuy", StringComparison.OrdinalIgnoreCase))
{
    var searchRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    var maxPackages = args.Length > 2 && int.TryParse(args[2], out var parsedMaxPackages) ? parsedMaxPackages : 12;
    var assetsPerPackage = args.Length > 3 && int.TryParse(args[3], out var parsedAssetsPerPackage) ? parsedAssetsPerPackage : 6;
    var maxMatches = args.Length > 4 && int.TryParse(args[4], out var parsedMaxMatches) ? parsedMaxMatches : 10;
    return await FindComplexBuildBuyAsync(searchRoot, maxPackages, assetsPerPackage, maxMatches);
}

if (args.Length > 0 && string.Equals(args[0], "--survey-3d", StringComparison.OrdinalIgnoreCase))
{
    var searchRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    var maxPackages = args.Length > 2 && int.TryParse(args[2], out var parsedMaxPackages) ? parsedMaxPackages : 24;
    var nameSamplesPerType = args.Length > 3 && int.TryParse(args[3], out var parsedNameSamplesPerType) ? parsedNameSamplesPerType : 5;
    var outputPath = args.Length > 4 ? args[4] : Path.Combine(Environment.CurrentDirectory, "tmp", "probe_3d_survey.json");
    return await RunThreeDimensionalSurveyAsync(searchRoot, maxPackages, nameSamplesPerType, outputPath);
}

if (args.Length > 0 && string.Equals(args[0], "--survey-buildbuy-identity", StringComparison.OrdinalIgnoreCase))
{
    var searchRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    var maxPackages = args.Length > 2 && int.TryParse(args[2], out var parsedMaxPackages) ? parsedMaxPackages : 12;
    var pairsPerPackage = args.Length > 3 && int.TryParse(args[3], out var parsedPairsPerPackage) ? parsedPairsPerPackage : 6;
    var outputPath = args.Length > 4 ? args[4] : Path.Combine(Environment.CurrentDirectory, "tmp", "probe_buildbuy_identity_survey.json");
    return await RunBuildBuyIdentitySurveyAsync(searchRoot, maxPackages, pairsPerPackage, outputPath);
}

if (args.Length > 0 && string.Equals(args[0], "--resolve-buildbuy-candidates", StringComparison.OrdinalIgnoreCase))
{
    var searchRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    var surveyPath = args.Length > 2 ? args[2] : Path.Combine(Environment.CurrentDirectory, "tmp", "probe_buildbuy_identity_survey.json");
    var outputPath = args.Length > 3 ? args[3] : Path.Combine(Environment.CurrentDirectory, "tmp", "probe_buildbuy_candidate_resolution.json");
    var maxPackages = args.Length > 4 && int.TryParse(args[4], out var parsedMaxPackages) ? parsedMaxPackages : 0;
    return await ResolveBuildBuyCandidatesAsync(searchRoot, surveyPath, outputPath, maxPackages);
}

if (args.Length > 0 && string.Equals(args[0], "--survey-cobj-fields", StringComparison.OrdinalIgnoreCase))
{
    var surveyPath = args.Length > 1 ? args[1] : Path.Combine(Environment.CurrentDirectory, "tmp", "probe_buildbuy_identity_survey.json");
    var outputPath = args.Length > 2 ? args[2] : Path.Combine(Environment.CurrentDirectory, "tmp", "probe_cobj_field_summary.json");
    return await SummarizeObjectCatalogFieldsAsync(surveyPath, outputPath);
}

if (args.Length > 0 && string.Equals(args[0], "--profile-index", StringComparison.OrdinalIgnoreCase))
{
    var searchRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    var maxPackages = args.Length > 2 && int.TryParse(args[2], out var parsedMaxPackages) ? parsedMaxPackages : 48;
    var workerCount = args.Length > 3 && int.TryParse(args[3], out var parsedWorkerCount) ? parsedWorkerCount : 16;
    var packageOrder = args.Length > 4 ? args[4] : "largest";
    return await RunIndexProfileAsync(searchRoot, maxPackages, workerCount, packageOrder);
}

var packagePath = args.Length > 0 ? args[0] : @"C:\GAMES\The Sims 4\EP10\ClientFullBuild0.package";
var rootTgi = args.Length > 1 ? args[1] : "01661233:00000000:00F643B0FDD2F1F7";

if (!File.Exists(packagePath))
{
    Console.Error.WriteLine($"Package not found: {packagePath}");
    return 1;
}

var rootDirectory = Path.Combine(AppContext.BaseDirectory, "probe-cache");
var cache = new ProbeCacheService(rootDirectory);
cache.EnsureCreated();

var store = new SqliteIndexStore(cache);
await store.InitializeAsync(CancellationToken.None);

var source = new DataSourceDefinition(
    Guid.Parse("11111111-1111-1111-1111-111111111111"),
    "ProbeSource",
    Path.GetDirectoryName(packagePath) ?? packagePath,
    SourceKind.Game);

var catalog = new LlamaResourceCatalogService();
var graphBuilder = new ExplicitAssetGraphBuilder(catalog);
var sceneBuilder = new BuildBuySceneBuildService(catalog, store);

Console.WriteLine($"Scanning package: {packagePath}");
var scan = await catalog.ScanPackageAsync(source, packagePath, progress: null, CancellationToken.None);
var assets = graphBuilder.BuildAssetSummaries(scan);
await store.ReplacePackageAsync(scan, assets, CancellationToken.None);

var asset = assets.FirstOrDefault(candidate =>
    candidate.AssetKind == AssetKind.BuildBuy &&
    string.Equals(candidate.RootKey.FullTgi, rootTgi, StringComparison.OrdinalIgnoreCase));

if (asset is null)
{
    Console.Error.WriteLine($"Build/Buy asset not found for root TGI: {rootTgi}");
    return 2;
}

Console.WriteLine();
Console.WriteLine("== Asset ==");
Console.WriteLine($"Display: {asset.DisplayName}");
Console.WriteLine($"Root: {asset.RootKey.FullTgi}");
Console.WriteLine($"Thumbnail: {asset.ThumbnailTgi ?? "(none)"}");
Console.WriteLine($"Support: {asset.SupportLabel}");
Console.WriteLine($"Notes: {asset.SupportNotes}");
Console.WriteLine($"Snapshot: SceneRoot={asset.CapabilitySnapshot.HasSceneRoot}, ExactGeom={asset.CapabilitySnapshot.HasExactGeometryCandidate}, Textures={asset.CapabilitySnapshot.HasTextureReferences}");

var packageResources = await store.GetPackageResourcesAsync(asset.PackagePath, CancellationToken.None);
var graph = await graphBuilder.BuildAssetGraphAsync(asset, packageResources, CancellationToken.None);

Console.WriteLine();
Console.WriteLine("== Graph Diagnostics ==");
WriteLines(graph.Diagnostics);

if (graph.BuildBuyGraph is null)
{
    Console.Error.WriteLine("Build/Buy graph was not constructed.");
    return 3;
}

Console.WriteLine();
Console.WriteLine("== Graph Resources ==");
Console.WriteLine($"Model root: {graph.BuildBuyGraph.ModelResource.Key.FullTgi}");
Console.WriteLine($"ModelLOD candidates: {graph.BuildBuyGraph.ModelLodResources.Count}");
foreach (var lod in graph.BuildBuyGraph.ModelLodResources)
{
    Console.WriteLine($"  LOD: {lod.Key.FullTgi}");
}
Console.WriteLine($"Texture candidates: {graph.BuildBuyGraph.TextureResources.Count}");
foreach (var texture in graph.BuildBuyGraph.TextureResources)
{
    Console.WriteLine($"  TEX: {texture.Key.FullTgi} ({texture.Key.TypeName})");
}

Console.WriteLine();
Console.WriteLine("== Scene From Model Root ==");
Console.WriteLine("Root RCOL summary:");
await DumpRcolChunkSummary(catalog, graph.BuildBuyGraph.ModelResource);
Console.WriteLine("Root material summary:");
var resolvedRootResource = await ResolveCompanionResourceAsync(catalog, graph.BuildBuyGraph.ModelResource);
var rootRawBytes = await catalog.GetResourceBytesAsync(resolvedRootResource.PackagePath, resolvedRootResource.Key, raw: false, CancellationToken.None);
Console.WriteLine("Root model summary:");
Console.WriteLine(PreviewDebugProbe.InspectModelLod(rootRawBytes));
Console.WriteLine(PreviewDebugProbe.InspectMaterialChunks(rootRawBytes));
var modelResult = await sceneBuilder.BuildSceneAsync(graph.BuildBuyGraph.ModelResource, CancellationToken.None);
WriteSceneResult(modelResult);

Console.WriteLine();
Console.WriteLine("== Scene Per Indexed ModelLOD ==");
foreach (var lod in graph.BuildBuyGraph.ModelLodResources)
{
    Console.WriteLine($"-- {lod.Key.FullTgi}");
    DumpRcolChunkSummary(catalog, lod).GetAwaiter().GetResult();
    Console.WriteLine("  Preview assembly parse:");
    var resolvedLod = await ResolveCompanionResourceAsync(catalog, lod);
    var rawBytes = await catalog.GetResourceBytesAsync(resolvedLod.PackagePath, resolvedLod.Key, raw: false, CancellationToken.None);
    Console.WriteLine(PreviewDebugProbe.InspectModelLod(rawBytes));
    Console.WriteLine("  Material summary:");
    Console.WriteLine(PreviewDebugProbe.InspectMaterialChunks(rawBytes));
    var lodResult = await sceneBuilder.BuildSceneAsync(lod, CancellationToken.None);
    WriteSceneResult(lodResult);
    Console.WriteLine();
}

return 0;

static async Task<int> FindBuildBuyAssetAsync(string searchRoot, string rootTgi)
{
    if (!Directory.Exists(searchRoot))
    {
        Console.Error.WriteLine($"Directory not found: {searchRoot}");
        return 2;
    }

    var packagePaths = Directory
        .EnumerateFiles(searchRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    if (packagePaths.Length == 0)
    {
        Console.Error.WriteLine($"No package files found under: {searchRoot}");
        return 3;
    }

    var source = new DataSourceDefinition(
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        "ProbeSearch",
        searchRoot,
        SourceKind.Game);

    var catalog = new LlamaResourceCatalogService();
    var graphBuilder = new ExplicitAssetGraphBuilder(catalog);
    var scanned = 0;
    foreach (var packagePath in packagePaths)
    {
        scanned++;
        Console.WriteLine($"[{scanned}/{packagePaths.Length}] {packagePath}");
        try
        {
            var scan = await catalog.ScanPackageAsync(source, packagePath, progress: null, CancellationToken.None);
            var assets = graphBuilder.BuildAssetSummaries(scan);
            var match = assets.FirstOrDefault(candidate =>
                candidate.AssetKind == AssetKind.BuildBuy &&
                string.Equals(candidate.RootKey.FullTgi, rootTgi, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                Console.WriteLine();
                Console.WriteLine("MATCH");
                Console.WriteLine($"Package: {packagePath}");
                Console.WriteLine($"Display: {match.DisplayName}");
                Console.WriteLine($"Root: {match.RootKey.FullTgi}");
                Console.WriteLine($"Thumbnail: {match.ThumbnailTgi ?? "(none)"}");
                Console.WriteLine($"Support: {match.SupportLabel}");
                Console.WriteLine($"Notes: {match.SupportNotes}");
                return 0;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Scan failed: {ex.Message}");
        }
    }

    Console.Error.WriteLine($"Build/Buy asset not found for root TGI: {rootTgi}");
    return 4;
}

static async Task<int> FindResourceAsync(string searchRoot, string fullTgi)
{
    if (!Directory.Exists(searchRoot))
    {
        Console.Error.WriteLine($"Directory not found: {searchRoot}");
        return 1;
    }

    if (!TryParseTgi(fullTgi, out var key))
    {
        Console.Error.WriteLine($"Invalid TGI: {fullTgi}");
        return 2;
    }

    var packages = Directory.EnumerateFiles(searchRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    var rootDirectory = Path.Combine(AppContext.BaseDirectory, "probe-cache-find-resource");
    var cache = new ProbeCacheService(rootDirectory);
    cache.EnsureCreated();

    var store = new SqliteIndexStore(cache);
    await store.InitializeAsync(CancellationToken.None);

    var catalog = new LlamaResourceCatalogService();
    var source = new DataSourceDefinition(
        Guid.Parse("33333333-3333-3333-3333-333333333333"),
        "ProbeFindResource",
        searchRoot,
        SourceKind.Game);

    foreach (var packagePath in packages)
    {
        Console.WriteLine(packagePath);
        try
        {
            var scan = await catalog.ScanPackageAsync(source, packagePath, progress: null, CancellationToken.None);
            var resource = scan.Resources.FirstOrDefault(candidate =>
                candidate.Key.Type == key.Type &&
                candidate.Key.Group == key.Group &&
                candidate.Key.FullInstance == key.FullInstance);

            if (resource is not null)
            {
                Console.WriteLine();
                Console.WriteLine("MATCH");
                Console.WriteLine($"Package: {packagePath}");
                Console.WriteLine($"TGI: {resource.Key.FullTgi}");
                Console.WriteLine($"Type: {resource.Key.TypeName}");
                Console.WriteLine($"Compressed: {resource.IsCompressed}");
                Console.WriteLine($"Preview Kind: {resource.PreviewKind}");
                return 0;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to scan {packagePath}: {ex.Message}");
        }
    }

    Console.WriteLine();
    Console.WriteLine("No matching resource found.");
    return 3;
}

static async Task<int> InspectResourceAsync(string packagePath, string fullTgi)
{
    if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
    {
        Console.Error.WriteLine($"Package not found: {packagePath}");
        return 1;
    }

    if (!TryParseTgi(fullTgi, out var key))
    {
        Console.Error.WriteLine($"Invalid TGI: {fullTgi}");
        return 2;
    }

    var catalog = new LlamaResourceCatalogService();
    var source = new DataSourceDefinition(
        Guid.Parse("77777777-7777-7777-7777-777777777777"),
        "ProbeInspectResource",
        Path.GetDirectoryName(packagePath) ?? packagePath,
        SourceKind.Game);

    var scan = await catalog.ScanPackageAsync(source, packagePath, progress: null, CancellationToken.None);
    var resource = scan.Resources.FirstOrDefault(candidate =>
        candidate.Key.Type == key.Type &&
        candidate.Key.Group == key.Group &&
        candidate.Key.FullInstance == key.FullInstance);

    if (resource is null)
    {
        Console.Error.WriteLine($"Resource not found in package: {fullTgi}");
        return 3;
    }

    var enriched = await catalog.EnrichResourceAsync(resource, CancellationToken.None);
    var decodedBytes = await catalog.GetResourceBytesAsync(packagePath, resource.Key, raw: false, CancellationToken.None);
    var rawBytes = await catalog.GetResourceBytesAsync(packagePath, resource.Key, raw: true, CancellationToken.None);
    var text = await catalog.GetTextAsync(packagePath, resource.Key, CancellationToken.None);

    Console.WriteLine("== Resource ==");
    Console.WriteLine($"Package: {packagePath}");
    Console.WriteLine($"TGI: {resource.Key.FullTgi}");
    Console.WriteLine($"Type: {resource.Key.TypeName}");
    Console.WriteLine($"PreviewKind: {resource.PreviewKind}");
    Console.WriteLine($"Name: {enriched.Name ?? "(null)"}");
    Console.WriteLine($"CompressedSize: {enriched.CompressedSize?.ToString() ?? "(null)"}");
    Console.WriteLine($"UncompressedSize: {enriched.UncompressedSize?.ToString() ?? "(null)"}");
    Console.WriteLine($"IsCompressed: {enriched.IsCompressed?.ToString() ?? "(null)"}");
    Console.WriteLine($"Diagnostics: {enriched.Diagnostics}");
    try
    {
        var stblKey = SmartSimUtilities.GetStringTableResourceKey(new System.Globalization.CultureInfo("en-US"), resource.Key.Group, resource.Key.FullInstance);
        Console.WriteLine($"SuggestedEnUsStbl: {((uint)stblKey.Type):X8}:{stblKey.Group:X8}:{stblKey.FullInstance:X16}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"SuggestedEnUsStbl: (unavailable: {ex.Message})");
    }
    Console.WriteLine();

    if (!string.IsNullOrWhiteSpace(text))
    {
        Console.WriteLine("== Text ==");
        WriteTrimmedText(text, 4000);
        Console.WriteLine();
    }

    if (resource.Key.TypeName == nameof(ResourceType.StringTable))
    {
        await DumpStringTableAsync(packagePath, resource.Key);
        Console.WriteLine();
    }

    if (resource.Key.TypeName == "ObjectDefinition")
    {
        Console.WriteLine("== Object Definition Decode ==");
        DumpObjectDefinitionDecode(decodedBytes);
        Console.WriteLine();
    }

    if (resource.Key.TypeName == "ObjectCatalog")
    {
        Console.WriteLine("== Object Catalog Decode ==");
        DumpObjectCatalogDecode(decodedBytes);
        Console.WriteLine();
    }

    if (resource.Key.TypeName == "CASPart")
    {
        Console.WriteLine("== CAS Part Decode ==");
        DumpCasPartDecode(decodedBytes);
        Console.WriteLine();
    }

    Console.WriteLine("== Decoded Bytes ==");
    DumpHex(decodedBytes, 256);
    Console.WriteLine();
    Console.WriteLine("== Raw Bytes ==");
    DumpHex(rawBytes, 256);
    return 0;
}

static async Task<int> FindStringTableHashAsync(string searchRoot, string hashText, int maxPackages)
{
    if (!Directory.Exists(searchRoot))
    {
        Console.Error.WriteLine($"Directory not found: {searchRoot}");
        return 1;
    }

    hashText = hashText.Trim().Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase);
    if (!uint.TryParse(hashText, System.Globalization.NumberStyles.HexNumber, null, out var hash))
    {
        Console.Error.WriteLine($"Invalid hash: {hashText}");
        return 2;
    }

    var packages = Directory
        .EnumerateFiles(searchRoot, "Strings_ENG_US.package", SearchOption.AllDirectories)
        .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    if (maxPackages > 0 && packages.Length > maxPackages)
    {
        packages = packages.Take(maxPackages).ToArray();
    }

    if (packages.Length == 0)
    {
        Console.Error.WriteLine("No Strings_ENG_US.package files found.");
        return 3;
    }

    var matches = new List<(string PackagePath, string Tgi, string Value)>();
    for (var index = 0; index < packages.Length; index++)
    {
        var packagePath = packages[index];
        Console.WriteLine($"[{index + 1}/{packages.Length}] {packagePath}");

        await using var stream = new FileStream(
            packagePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 131072,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var package = await DataBasePackedFile.FromStreamAsync(stream, CancellationToken.None);

        foreach (var key in package.Keys)
        {
            if ((uint)key.Type != 0x220557DA)
            {
                continue;
            }

            try
            {
                var table = await package.GetStringTableAsync(key, false, CancellationToken.None);
                var value = table.Get(hash);
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var tgi = $"{(uint)key.Type:X8}:{key.Group:X8}:{key.FullInstance:X16}";
                matches.Add((packagePath, tgi, value));
                Console.WriteLine($"  MATCH {tgi} => {value}");
            }
            catch
            {
            }
        }
    }

    Console.WriteLine();
    if (matches.Count == 0)
    {
        Console.WriteLine($"No STBL entries found for hash 0x{hash:X8}.");
        return 0;
    }

    Console.WriteLine($"Found {matches.Count} STBL entr{(matches.Count == 1 ? "y" : "ies")} for hash 0x{hash:X8}.");
    return 0;
}

static async Task<int> RunBatchCoverageAsync(string inputPath)
{
    if (!File.Exists(inputPath))
    {
        Console.Error.WriteLine($"Batch input not found: {inputPath}");
        return 1;
    }

    var entries = File.ReadAllLines(inputPath)
        .Select(static line => line.Trim())
        .Where(static line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
        .Select(ParseBatchEntry)
        .ToArray();

    if (entries.Length == 0)
    {
        Console.Error.WriteLine("Batch input did not contain any probe entries.");
        return 2;
    }

    var sceneStatusCounts = new Dictionary<SceneBuildStatus, int>();
    var materialCoverageCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var materialSamplingSourceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var materialVisualPayloadCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var materialFamilyCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var materialPayloadByFamilyCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var materialStrategyCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var assetCoverage = new List<BatchCoverageRow>();

    foreach (var entry in entries)
    {
        Console.WriteLine($"== {entry.RootTgi} ==");
        var result = await ProbeSingleAssetAsync(entry.PackagePath, entry.RootTgi, verbose: false);
        Console.WriteLine($"Package: {entry.PackagePath}");
        Console.WriteLine($"Asset: {result.DisplayName ?? "(missing)"}");
        Console.WriteLine($"Found: {result.Found}");
        Console.WriteLine($"Scene Success: {result.SceneResult?.Success}");
        Console.WriteLine($"Scene Status: {result.SceneResult?.Status}");

        if (!result.Found || result.SceneResult is null)
        {
            Console.WriteLine();
            continue;
        }

        Increment(sceneStatusCounts, result.SceneResult.Status);
        var coverage = ParseMaterialCoverage(result.SceneResult.Diagnostics);
        if (coverage.Count == 0 && result.SceneResult.Scene is not null)
        {
            coverage["Unknown"] = result.SceneResult.Scene.Materials.Count;
        }

        foreach (var pair in coverage)
        {
            Add(materialCoverageCounts, pair.Key, pair.Value);
        }

        var samplingSources = ParseMaterialSamplingSources(result.SceneResult.Diagnostics);
        foreach (var pair in samplingSources)
        {
            Add(materialSamplingSourceCounts, pair.Key, pair.Value);
        }

        var visualPayloads = ParseMaterialVisualPayloads(result.SceneResult.Diagnostics);
        foreach (var pair in visualPayloads)
        {
            Add(materialVisualPayloadCounts, pair.Key, pair.Value);
        }

        var families = ParseNamedCountLine(result.SceneResult.Diagnostics, "Material families:");
        foreach (var pair in families)
        {
            Add(materialFamilyCounts, pair.Key, pair.Value);
        }

        AddPayloadByFamily(materialPayloadByFamilyCounts, families, visualPayloads);

        var strategies = ParseNamedCountLine(result.SceneResult.Diagnostics, "Material decode strategies:");
        foreach (var pair in strategies)
        {
            Add(materialStrategyCounts, pair.Key, pair.Value);
        }

        assetCoverage.Add(new BatchCoverageRow(
            entry.PackagePath,
            entry.RootTgi,
            result.DisplayName ?? entry.RootTgi,
            result.SceneResult.Status.ToString(),
            coverage,
            samplingSources,
            visualPayloads,
            families,
            strategies));

        Console.WriteLine($"Material Coverage: {FormatCoverageMap(coverage)}");
        Console.WriteLine($"Material Sampling Sources: {FormatCoverageMap(samplingSources)}");
        Console.WriteLine($"Material Visual Payloads: {FormatCoverageMap(visualPayloads)}");
        Console.WriteLine($"Material Families: {FormatCoverageMap(families)}");
        Console.WriteLine($"Material Decode Strategies: {FormatCoverageMap(strategies)}");
        Console.WriteLine();
    }

    Console.WriteLine("== Batch Summary ==");
    Console.WriteLine($"Entries: {entries.Length}");
    Console.WriteLine($"Resolved scenes: {assetCoverage.Count}");
    Console.WriteLine($"Scene statuses: {string.Join(", ", sceneStatusCounts.OrderBy(static pair => pair.Key).Select(static pair => $"{pair.Key}={pair.Value}"))}");
    Console.WriteLine($"Material coverage totals: {FormatCoverageMap(materialCoverageCounts)}");
    Console.WriteLine($"Material sampling source totals: {FormatCoverageMap(materialSamplingSourceCounts)}");
    Console.WriteLine($"Material visual payload totals: {FormatCoverageMap(materialVisualPayloadCounts)}");
    Console.WriteLine($"Material family totals: {FormatCoverageMap(materialFamilyCounts)}");
    Console.WriteLine($"Material payload-by-family totals: {FormatCoverageMap(materialPayloadByFamilyCounts)}");
    Console.WriteLine($"Material decode strategy totals: {FormatCoverageMap(materialStrategyCounts)}");
    Console.WriteLine("Per-asset coverage:");
    foreach (var row in assetCoverage.OrderBy(static item => item.PackagePath, StringComparer.OrdinalIgnoreCase).ThenBy(static item => item.RootTgi, StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine($"  {Path.GetFileName(row.PackagePath)} | {row.RootTgi} | {row.SceneStatus} | coverage={FormatCoverageMap(row.MaterialCoverage)} | sampling={FormatCoverageMap(row.MaterialSamplingSources)} | payload={FormatCoverageMap(row.MaterialVisualPayloads)} | families={FormatCoverageMap(row.MaterialFamilies)} | strategies={FormatCoverageMap(row.MaterialStrategies)}");
    }

    return 0;
}

static async Task<int> RunBatchCoverageSummaryAsync(string inputPath, string summaryPath, int maxEntries, int timeoutSeconds)
{
    if (!File.Exists(inputPath))
    {
        Console.Error.WriteLine($"Batch input not found: {inputPath}");
        return 1;
    }

    var entries = File.ReadAllLines(inputPath)
        .Select(static line => line.Trim())
        .Where(static line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
        .Select(ParseBatchEntry)
        .ToArray();

    if (entries.Length == 0)
    {
        Console.Error.WriteLine("Batch input did not contain any probe entries.");
        return 2;
    }

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(summaryPath))!);

    var summary = LoadExistingSummary(summaryPath) ?? new BatchCoverageSummary(
        inputPath,
        entries.Length,
        0,
        0,
        0,
        0,
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        DateTimeOffset.UtcNow,
        null,
        null);

    if (!string.Equals(summary.InputPath, inputPath, StringComparison.OrdinalIgnoreCase) || summary.TotalEntries != entries.Length)
    {
        summary = new BatchCoverageSummary(
            inputPath,
            entries.Length,
            0,
            0,
            0,
            0,
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            DateTimeOffset.UtcNow,
            null,
            null);
    }

    maxEntries = maxEntries <= 0 ? entries.Length : Math.Min(entries.Length, maxEntries);
    var processedThisRun = 0;
    var elapsedBeforeRun = summary.Elapsed ?? TimeSpan.Zero;
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    var perEntryTimeout = timeoutSeconds <= 0 ? TimeSpan.FromSeconds(120) : TimeSpan.FromSeconds(timeoutSeconds);
    for (var index = summary.ProcessedEntries; index < maxEntries; index++)
    {
        var entry = entries[index];
        summary = summary with { ProcessedEntries = index + 1 };
        var rowResult = await ProbeSingleAssetOutOfProcessAsync(entry.PackagePath, entry.RootTgi, perEntryTimeout);
        if (rowResult.TimedOut)
        {
            summary = summary with { TimedOutEntries = summary.TimedOutEntries + 1 };
            Add(summary.SceneStatuses, "TimedOut", 1);
        }
        else if (rowResult.Row is null)
        {
            summary = summary with { FailedEntries = summary.FailedEntries + 1 };
            Add(summary.SceneStatuses, "Failed", 1);
        }
        else
        {
            var row = rowResult.Row;
            summary = summary with { ResolvedScenes = summary.ResolvedScenes + 1 };
            Add(summary.SceneStatuses, row.SceneStatus, 1);

            foreach (var pair in row.MaterialCoverage)
            {
                Add(summary.MaterialCoverage, pair.Key, pair.Value);
            }

            foreach (var pair in row.MaterialSamplingSources)
            {
                Add(summary.MaterialSamplingSources, pair.Key, pair.Value);
            }

            foreach (var pair in row.MaterialVisualPayloads)
            {
                Add(summary.MaterialVisualPayloads, pair.Key, pair.Value);
            }

            foreach (var pair in row.MaterialFamilies)
            {
                Add(summary.MaterialFamilies, pair.Key, pair.Value);
            }

            AddPayloadByFamily(summary.MaterialPayloadByFamily, row.MaterialFamilies, row.MaterialVisualPayloads);

            foreach (var pair in row.MaterialStrategies)
            {
                Add(summary.MaterialStrategies, pair.Key, pair.Value);
            }
        }

        processedThisRun++;
        if (processedThisRun % 10 == 0 || rowResult.TimedOut || rowResult.Row is null || summary.ProcessedEntries == maxEntries)
        {
            summary = UpdateSummaryTiming(summary, elapsedBeforeRun, stopwatch.Elapsed);
            SaveSummary(summaryPath, summary);
            Console.WriteLine(
                $"Processed {summary.ProcessedEntries}/{summary.TotalEntries} | resolved={summary.ResolvedScenes} | timedOut={summary.TimedOutEntries} | failed={summary.FailedEntries} | " +
                $"elapsed={summary.Elapsed:hh\\:mm\\:ss} | eta={(summary.EstimatedRemaining ?? TimeSpan.Zero):hh\\:mm\\:ss}");
        }
    }

    summary = UpdateSummaryTiming(summary, elapsedBeforeRun, stopwatch.Elapsed);
    SaveSummary(summaryPath, summary);

    Console.WriteLine();
    Console.WriteLine($"Summary written to {summaryPath}");
    Console.WriteLine($"Processed entries: {summary.ProcessedEntries}/{summary.TotalEntries}");
    Console.WriteLine($"Resolved scenes: {summary.ResolvedScenes}");
    Console.WriteLine($"Timed out entries: {summary.TimedOutEntries}");
    Console.WriteLine($"Failed entries: {summary.FailedEntries}");
    Console.WriteLine($"Scene statuses: {FormatCoverageMap(summary.SceneStatuses)}");
    Console.WriteLine($"Material coverage: {FormatCoverageMap(summary.MaterialCoverage)}");
    Console.WriteLine($"Material sampling sources: {FormatCoverageMap(summary.MaterialSamplingSources)}");
    Console.WriteLine($"Material visual payloads: {FormatCoverageMap(summary.MaterialVisualPayloads)}");
    Console.WriteLine($"Material families: {FormatCoverageMap(summary.MaterialFamilies)}");
    Console.WriteLine($"Material payload-by-family: {FormatCoverageMap(summary.MaterialPayloadByFamily)}");
    Console.WriteLine($"Material decode strategies: {FormatCoverageMap(summary.MaterialStrategies)}");
    Console.WriteLine($"Elapsed: {summary.Elapsed:hh\\:mm\\:ss}");
    if (summary.EstimatedRemaining is not null)
    {
        Console.WriteLine($"Estimated remaining: {summary.EstimatedRemaining.Value:hh\\:mm\\:ss}");
    }

    return 0;
}

static async Task<int> RunProbeJsonAsync(string packagePath, string rootTgi)
{
    if (string.IsNullOrWhiteSpace(packagePath) || string.IsNullOrWhiteSpace(rootTgi))
    {
        Console.Error.WriteLine("Usage: --probe-json <packagePath> <rootTgi>");
        return 1;
    }

    var result = await ProbeSingleAssetAsync(packagePath, rootTgi, verbose: false);
    if (!result.Found || result.SceneResult is null)
    {
        Console.WriteLine(JsonSerializer.Serialize<object?>(null));
        return 0;
    }

    var row = CreateBatchCoverageRow(result);
    Console.WriteLine(JsonSerializer.Serialize(row));
    return 0;
}

static async Task<ProbeProcessResult> ProbeSingleAssetOutOfProcessAsync(string packagePath, string rootTgi, TimeSpan timeout)
{
    var processPath = Environment.ProcessPath;
    if (string.IsNullOrWhiteSpace(processPath) || !File.Exists(processPath))
    {
        throw new InvalidOperationException("Could not resolve the current ProbeAsset executable path.");
    }

    using var process = new System.Diagnostics.Process
    {
        StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = processPath,
            Arguments = $"--probe-json {QuoteArg(packagePath)} {QuoteArg(rootTgi)}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Environment.CurrentDirectory
        }
    };

    process.Start();
    using var cts = new CancellationTokenSource(timeout);
    try
    {
        await process.WaitForExitAsync(cts.Token);
    }
    catch (OperationCanceledException)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
        }

        return new ProbeProcessResult(null, TimedOut: true);
    }

    var stdout = await process.StandardOutput.ReadToEndAsync();
    var stderr = await process.StandardError.ReadToEndAsync();
    if (process.ExitCode != 0)
    {
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            Console.WriteLine($"Probe failed for {rootTgi}: {stderr.Trim()}");
        }

        return new ProbeProcessResult(null, TimedOut: false);
    }

    var json = stdout
        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
        .LastOrDefault(static line => line.TrimStart().StartsWith("{", StringComparison.Ordinal) || string.Equals(line.Trim(), "null", StringComparison.OrdinalIgnoreCase));

    if (string.IsNullOrWhiteSpace(json) || string.Equals(json.Trim(), "null", StringComparison.OrdinalIgnoreCase))
    {
        return new ProbeProcessResult(null, TimedOut: false);
    }

    var row = JsonSerializer.Deserialize<BatchCoverageRow>(json);
    return new ProbeProcessResult(row, TimedOut: false);
}

static BatchCoverageRow CreateBatchCoverageRow(ProbeAssetResult result)
{
    var sceneResult = result.SceneResult!;
    var coverage = ParseMaterialCoverage(sceneResult.Diagnostics);
    if (coverage.Count == 0 && sceneResult.Scene is not null)
    {
        coverage["Unknown"] = sceneResult.Scene.Materials.Count;
    }

    return new BatchCoverageRow(
        result.PackagePath,
        result.RootTgi,
        result.DisplayName ?? "(unknown)",
        sceneResult.Status.ToString(),
        coverage,
        ParseMaterialSamplingSources(sceneResult.Diagnostics),
        ParseMaterialVisualPayloads(sceneResult.Diagnostics),
        ParseNamedCountLine(sceneResult.Diagnostics, "Material families:"),
        ParseNamedCountLine(sceneResult.Diagnostics, "Material decode strategies:"));
}

static string QuoteArg(string value) => "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

static BatchCoverageSummary UpdateSummaryTiming(BatchCoverageSummary summary, TimeSpan elapsedBeforeRun, TimeSpan elapsedThisRun)
{
    var totalElapsed = elapsedBeforeRun + elapsedThisRun;
    TimeSpan? eta = null;
    if (summary.ProcessedEntries > 0 && summary.ProcessedEntries < summary.TotalEntries)
    {
        var secondsPerEntry = totalElapsed.TotalSeconds / summary.ProcessedEntries;
        eta = TimeSpan.FromSeconds(secondsPerEntry * (summary.TotalEntries - summary.ProcessedEntries));
    }

    return summary with
    {
        Elapsed = totalElapsed,
        LastUpdatedUtc = DateTimeOffset.UtcNow,
        EstimatedRemaining = eta
    };
}

static BatchCoverageSummary? LoadExistingSummary(string summaryPath)
{
    if (!File.Exists(summaryPath))
    {
        return null;
    }

    try
    {
        return JsonSerializer.Deserialize<BatchCoverageSummary>(File.ReadAllText(summaryPath));
    }
    catch
    {
        return null;
    }
}

static void SaveSummary(string summaryPath, BatchCoverageSummary summary)
{
    var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions
    {
        WriteIndented = true
    });
    File.WriteAllText(summaryPath, json);
}

static async Task<int> SampleBuildBuyRootsAsync(string searchRoot, int maxPackages, int assetsPerPackage, string outputPath)
{
    if (!Directory.Exists(searchRoot))
    {
        Console.Error.WriteLine($"Directory not found: {searchRoot}");
        return 1;
    }

    maxPackages = Math.Max(1, maxPackages);
    assetsPerPackage = Math.Max(1, assetsPerPackage);

    var packagePaths = Directory
        .EnumerateFiles(searchRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    if (packagePaths.Length == 0)
    {
        Console.Error.WriteLine($"No package files found under: {searchRoot}");
        return 2;
    }

    packagePaths = SelectRepresentativePackagePaths(searchRoot, packagePaths, maxPackages);

    var source = new DataSourceDefinition(
        Guid.Parse("44444444-4444-4444-4444-444444444444"),
        "ProbeSample",
        searchRoot,
        SourceKind.Game);

    var catalog = new LlamaResourceCatalogService();
    var graphBuilder = new ExplicitAssetGraphBuilder(catalog);
    var lines = new List<string>();

    for (var index = 0; index < packagePaths.Length; index++)
    {
        var packagePath = packagePaths[index];
        Console.WriteLine($"[{index + 1}/{packagePaths.Length}] {packagePath}");
        try
        {
            var scan = await catalog.ScanPackageAsync(source, packagePath, progress: null, CancellationToken.None);
            var assets = graphBuilder.BuildAssetSummaries(scan)
                .Where(static asset => asset.AssetKind == AssetKind.BuildBuy)
                .OrderByDescending(static asset => asset.CapabilitySnapshot.HasTextureReferences)
                .ThenByDescending(static asset => asset.CapabilitySnapshot.HasExactGeometryCandidate)
                .ThenBy(static asset => asset.RootKey.FullTgi, StringComparer.OrdinalIgnoreCase)
                .Take(assetsPerPackage)
                .ToArray();

            Console.WriteLine($"  Selected: {assets.Length}");
            foreach (var asset in assets)
            {
                var line = $"{packagePath}|{asset.RootKey.FullTgi}";
                lines.Add(line);
                Console.WriteLine($"    {asset.RootKey.FullTgi} | {asset.DisplayName}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Scan failed: {ex.Message}");
        }
    }

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
    File.WriteAllLines(outputPath, lines, System.Text.Encoding.UTF8);
    Console.WriteLine();
    Console.WriteLine($"Wrote {lines.Count} sampled Build/Buy roots to {outputPath}");
    return 0;
}

static async Task<int> FindComplexBuildBuyAsync(string searchRoot, int maxPackages, int assetsPerPackage, int maxMatches)
{
    if (!Directory.Exists(searchRoot))
    {
        Console.Error.WriteLine($"Directory not found: {searchRoot}");
        return 1;
    }

    maxPackages = Math.Max(1, maxPackages);
    assetsPerPackage = Math.Max(1, assetsPerPackage);
    maxMatches = Math.Max(1, maxMatches);

    var packagePaths = Directory
        .EnumerateFiles(searchRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    if (packagePaths.Length == 0)
    {
        Console.Error.WriteLine($"No package files found under: {searchRoot}");
        return 2;
    }

    packagePaths = SelectRepresentativePackagePaths(searchRoot, packagePaths, maxPackages);

    var source = new DataSourceDefinition(
        Guid.Parse("55555555-5555-5555-5555-555555555555"),
        "ProbeComplex",
        searchRoot,
        SourceKind.Game);

    var catalog = new LlamaResourceCatalogService();
    var graphBuilder = new ExplicitAssetGraphBuilder(catalog);
    var matches = new List<(string PackagePath, string RootTgi, string SceneStatus, Dictionary<string, int> Coverage, Dictionary<string, int> SamplingSources, Dictionary<string, int> VisualPayloads)>();

    for (var index = 0; index < packagePaths.Length && matches.Count < maxMatches; index++)
    {
        var packagePath = packagePaths[index];
        Console.WriteLine($"[{index + 1}/{packagePaths.Length}] {packagePath}");

        PackageScanResult scan;
        try
        {
            scan = await catalog.ScanPackageAsync(source, packagePath, progress: null, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Scan failed: {ex.Message}");
            continue;
        }

        var assets = graphBuilder.BuildAssetSummaries(scan)
            .Where(static asset => asset.AssetKind == AssetKind.BuildBuy)
            .OrderByDescending(static asset => asset.CapabilitySnapshot.HasTextureReferences)
            .ThenByDescending(static asset => asset.CapabilitySnapshot.HasExactGeometryCandidate)
            .ThenByDescending(static asset => asset.CapabilitySnapshot.HasSceneRoot)
            .ThenBy(static asset => asset.RootKey.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Take(assetsPerPackage)
            .ToArray();

        foreach (var asset in assets)
        {
            if (matches.Count >= maxMatches)
            {
                break;
            }

            var probe = await ProbeSingleAssetAsync(packagePath, asset.RootKey.FullTgi, verbose: false);
            if (!probe.Found || probe.SceneResult is null)
            {
                continue;
            }

            var coverage = ParseMaterialCoverage(probe.SceneResult.Diagnostics);
            var samplingSources = ParseMaterialSamplingSources(probe.SceneResult.Diagnostics);
            var visualPayloads = ParseMaterialVisualPayloads(probe.SceneResult.Diagnostics);
            var isComplex = coverage.Keys.Any(static key => !string.Equals(key, "StaticReady", StringComparison.OrdinalIgnoreCase))
                || samplingSources.Keys.Any(static key =>
                    !string.Equals(key, "diffuse-material-path", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(key, "default-uv0", StringComparison.OrdinalIgnoreCase))
                || visualPayloads.Keys.Any(static key => !string.Equals(key, "textured", StringComparison.OrdinalIgnoreCase));

            if (!isComplex)
            {
                continue;
            }

            matches.Add((packagePath, asset.RootKey.FullTgi, probe.SceneResult.Status.ToString(), coverage, samplingSources, visualPayloads));
            Console.WriteLine($"  Complex match: {asset.RootKey.FullTgi} | {probe.SceneResult.Status} | coverage={FormatCoverageMap(coverage)} | sampling={FormatCoverageMap(samplingSources)} | payload={FormatCoverageMap(visualPayloads)}");
        }
    }

    Console.WriteLine();
    Console.WriteLine("== Complex Build/Buy Matches ==");
    if (matches.Count == 0)
    {
        Console.WriteLine("No non-static material matches found in the scanned sample.");
        return 0;
    }

    foreach (var match in matches)
    {
        Console.WriteLine($"{match.PackagePath}|{match.RootTgi} | {match.SceneStatus} | coverage={FormatCoverageMap(match.Coverage)} | sampling={FormatCoverageMap(match.SamplingSources)} | payload={FormatCoverageMap(match.VisualPayloads)}");
    }

    return 0;
}

static async Task<int> RunThreeDimensionalSurveyAsync(string searchRoot, int maxPackages, int nameSamplesPerType, string outputPath)
{
    if (!Directory.Exists(searchRoot))
    {
        Console.Error.WriteLine($"Directory not found: {searchRoot}");
        return 1;
    }

    nameSamplesPerType = Math.Max(1, nameSamplesPerType);

    var packagePaths = Directory
        .EnumerateFiles(searchRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    if (packagePaths.Length == 0)
    {
        Console.Error.WriteLine($"No package files found under: {searchRoot}");
        return 2;
    }

    var totalPackageCount = packagePaths.Length;
    if (maxPackages > 0 && packagePaths.Length > maxPackages)
    {
        packagePaths = SelectRepresentativePackagePaths(searchRoot, packagePaths, maxPackages);
    }

    var source = new DataSourceDefinition(
        Guid.Parse("66666666-6666-6666-6666-666666666666"),
        "Probe3DSurvey",
        searchRoot,
        SourceKind.Game);

    var catalog = new LlamaResourceCatalogService();
    var graphBuilder = new ExplicitAssetGraphBuilder(catalog);
    var resourceTypeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var threeDimensionalComponentCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var cooccurringTypeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var groupPatternCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var assetKindCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var currentAssetDisplaySamples = new List<SurveyAssetDisplaySample>();
    var packageRows = new List<SurveyPackageRow>();
    var nameProbeStates = new Dictionary<string, SurveyNameProbeState>(StringComparer.OrdinalIgnoreCase);
    var threeDimensionalGroupCount = 0;

    for (var index = 0; index < packagePaths.Length; index++)
    {
        var packagePath = packagePaths[index];
        Console.WriteLine($"[{index + 1}/{packagePaths.Length}] {packagePath}");

        PackageScanResult scan;
        try
        {
            scan = await catalog.ScanPackageAsync(source, packagePath, progress: null, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Scan failed: {ex.Message}");
            packageRows.Add(new SurveyPackageRow(
                packagePath,
                RelativeToRoot(searchRoot, packagePath),
                ResourceCount: 0,
                ThreeDimensionalGroupCount: 0,
                AssetCounts: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                ThreeDimensionalTypes: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                Error: ex.Message));
            continue;
        }

        var package3dTypes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var resource in scan.Resources)
        {
            IncrementNamed(resourceTypeCounts, resource.Key.TypeName);
            if (IsThreeDimensionalStructuralType(resource.Key.TypeName))
            {
                IncrementNamed(threeDimensionalComponentCounts, resource.Key.TypeName);
                IncrementNamed(package3dTypes, resource.Key.TypeName);
            }
        }

        foreach (var resource in scan.Resources.Where(resource => ShouldProbeGlobalNameType(resource.Key.TypeName)))
        {
            var state = GetOrCreateNameProbeState(nameProbeStates, resource.Key.TypeName);
            if (state.Sampled >= nameSamplesPerType)
            {
                continue;
            }

            state.Sampled++;
            try
            {
                var enriched = await catalog.EnrichResourceAsync(resource, CancellationToken.None);
                if (string.IsNullOrWhiteSpace(enriched.Name))
                {
                    state.Empty++;
                }
                else
                {
                    state.Named++;
                    if (state.Examples.Count < nameSamplesPerType)
                    {
                        state.Examples.Add($"{resource.Key.FullTgi} => {enriched.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                state.Failed++;
                if (state.Failures.Count < 3)
                {
                    state.Failures.Add(ex.Message);
                }
            }
        }

        var assets = graphBuilder.BuildAssetSummaries(scan);
        var packageAssetCounts = assets
            .GroupBy(static asset => asset.AssetKind.ToString(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.OrdinalIgnoreCase);
        foreach (var pair in packageAssetCounts)
        {
            IncrementNamed(assetKindCounts, pair.Key, pair.Value);
        }

        foreach (var sample in assets
            .OrderBy(static asset => asset.AssetKind.ToString(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(static asset => asset.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(4))
        {
            if (currentAssetDisplaySamples.Count >= 40)
            {
                break;
            }

            currentAssetDisplaySamples.Add(new SurveyAssetDisplaySample(
                sample.AssetKind.ToString(),
                RelativeToRoot(searchRoot, sample.PackagePath),
                sample.RootKey.FullTgi,
                sample.DisplayName,
                sample.Category));
        }

        var sameInstanceGroups = scan.Resources
            .GroupBy(static resource => resource.Key.FullInstance)
            .Select(static group => group.ToArray())
            .ToArray();

        var packageThreeDimensionalGroupCount = 0;
        foreach (var group in sameInstanceGroups)
        {
            if (!group.Any(resource => IsThreeDimensionalStructuralType(resource.Key.TypeName)))
            {
                continue;
            }

            packageThreeDimensionalGroupCount++;
            threeDimensionalGroupCount++;

            var groupTypes = group
                .Select(static resource => resource.Key.TypeName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static typeName => typeName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            IncrementNamed(groupPatternCounts, string.Join(" + ", groupTypes));
            foreach (var typeName in groupTypes)
            {
                IncrementNamed(cooccurringTypeCounts, typeName);
            }

            foreach (var typeGroup in group
                .GroupBy(static resource => resource.Key.TypeName, StringComparer.OrdinalIgnoreCase)
                .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                var state = GetOrCreateNameProbeState(nameProbeStates, typeGroup.Key);
                if (state.Sampled >= nameSamplesPerType)
                {
                    continue;
                }

                foreach (var resource in typeGroup)
                {
                    if (state.Sampled >= nameSamplesPerType)
                    {
                        break;
                    }

                    state.Sampled++;
                    try
                    {
                        var enriched = await catalog.EnrichResourceAsync(resource, CancellationToken.None);
                        if (string.IsNullOrWhiteSpace(enriched.Name))
                        {
                            state.Empty++;
                        }
                        else
                        {
                            state.Named++;
                            if (state.Examples.Count < nameSamplesPerType)
                            {
                                state.Examples.Add($"{resource.Key.FullTgi} => {enriched.Name}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        state.Failed++;
                        if (state.Failures.Count < 3)
                        {
                            state.Failures.Add(ex.Message);
                        }
                    }
                }
            }
        }

        packageRows.Add(new SurveyPackageRow(
            packagePath,
            RelativeToRoot(searchRoot, packagePath),
            scan.Resources.Count,
            packageThreeDimensionalGroupCount,
            packageAssetCounts,
            package3dTypes,
            Error: null));

        Console.WriteLine(
            $"  resources={scan.Resources.Count:N0} | 3d-groups={packageThreeDimensionalGroupCount:N0} | " +
            $"3d-types={FormatCoverageMap(package3dTypes)} | assets={FormatCoverageMap(packageAssetCounts)}");
    }

    var report = new ThreeDimensionalSurveyReport(
        searchRoot,
        totalPackageCount,
        packagePaths.Length,
        nameSamplesPerType,
        DateTimeOffset.UtcNow,
        threeDimensionalGroupCount,
        packageRows,
        assetKindCounts.OrderByDescending(static pair => pair.Value).ThenBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(),
        resourceTypeCounts.OrderByDescending(static pair => pair.Value).ThenBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(),
        threeDimensionalComponentCounts.OrderByDescending(static pair => pair.Value).ThenBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(),
        cooccurringTypeCounts.OrderByDescending(static pair => pair.Value).ThenBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(static pair => new SurveyCountRow(pair.Key, pair.Value)).Take(80).ToArray(),
        groupPatternCounts.OrderByDescending(static pair => pair.Value).ThenBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(static pair => new SurveyCountRow(pair.Key, pair.Value)).Take(80).ToArray(),
        nameProbeStates
            .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static pair => pair.Value.ToSummary())
            .ToArray(),
        currentAssetDisplaySamples);

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
    File.WriteAllText(
        outputPath,
        JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        }));

    Console.WriteLine();
    Console.WriteLine($"Survey written to {outputPath}");
    Console.WriteLine($"Packages processed: {packagePaths.Length}/{totalPackageCount}");
    Console.WriteLine($"3D groups: {threeDimensionalGroupCount:N0}");
    Console.WriteLine($"Current asset kinds: {FormatCoverageMap(assetKindCounts)}");
    Console.WriteLine($"3D component types: {FormatCoverageMap(threeDimensionalComponentCounts)}");
    Console.WriteLine($"Top 3D co-occurring types: {FormatCoverageRows(report.ThreeDimensionalCooccurringTypes.Take(12))}");
    Console.WriteLine($"Top 3D group patterns: {FormatCoverageRows(report.ThreeDimensionalGroupPatterns.Take(8))}");
    Console.WriteLine($"Name lookup summary: {FormatNameProbeSummary(report.NameLookupByType)}");

    return 0;
}

static string[] SelectRepresentativePackagePaths(string searchRoot, string[] packagePaths, int maxPackages)
{
    if (packagePaths.Length <= maxPackages)
    {
        return packagePaths;
    }

    var grouped = packagePaths
        .GroupBy(path => GetPackageBucket(searchRoot, path), StringComparer.OrdinalIgnoreCase)
        .Select(group => new Queue<string>(group
            .OrderBy(path => GetPackagePriority(path))
            .ThenBy(static path => path, StringComparer.OrdinalIgnoreCase)))
        .OrderBy(queue => GetBucketPriority(queue.Peek()))
        .ThenBy(queue => GetPackageBucket(searchRoot, queue.Peek()), StringComparer.OrdinalIgnoreCase)
        .ToArray();

    var selected = new List<string>(capacity: maxPackages);
    while (selected.Count < maxPackages)
    {
        var addedAny = false;
        foreach (var queue in grouped)
        {
            if (queue.Count == 0)
            {
                continue;
            }

            selected.Add(queue.Dequeue());
            addedAny = true;
            if (selected.Count >= maxPackages)
            {
                break;
            }
        }

        if (!addedAny)
        {
            break;
        }
    }

    return selected.ToArray();
}

static string GetPackageBucket(string searchRoot, string packagePath)
{
    var relativePath = Path.GetRelativePath(searchRoot, packagePath);
    var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    return parts.Length > 1 ? parts[0] : "(root)";
}

static bool IsThreeDimensionalStructuralType(string typeName) =>
    typeName is "Model" or "ModelLOD" or "Geometry" or "BlendGeometry" or "Rig" or "MaterialDefinition";

static bool ShouldProbeGlobalNameType(string typeName) =>
    typeName is "ObjectCatalog"
        or "ObjectDefinition"
        or "CASPart"
        or "CASPartThumbnail"
        or "BodyPartThumbnail"
        or "BuyBuildThumbnail"
        or "StringTable";

static SurveyNameProbeState GetOrCreateNameProbeState(IDictionary<string, SurveyNameProbeState> states, string typeName)
{
    if (states.TryGetValue(typeName, out var existing))
    {
        return existing;
    }

    var created = new SurveyNameProbeState(typeName);
    states[typeName] = created;
    return created;
}

static string RelativeToRoot(string root, string path)
{
    try
    {
        return Path.GetRelativePath(root, path);
    }
    catch
    {
        return path;
    }
}

static string FormatCoverageRows(IEnumerable<SurveyCountRow> rows) =>
    string.Join(", ", rows.Select(static row => $"{row.Label}={row.Count}"));

static string FormatNameProbeSummary(IEnumerable<SurveyNameLookupSummary> summaries) =>
    string.Join(
        ", ",
        summaries
            .Where(static summary => summary.Sampled > 0)
            .Select(static summary => $"{summary.TypeName}: named={summary.Named}/{summary.Sampled}, empty={summary.Empty}, failed={summary.Failed}"));

static async Task<int> ResolveBuildBuyCandidatesAsync(string searchRoot, string surveyPath, string outputPath, int maxPackages)
{
    if (!Directory.Exists(searchRoot))
    {
        Console.Error.WriteLine($"Directory not found: {searchRoot}");
        return 2;
    }

    if (!File.Exists(surveyPath))
    {
        Console.Error.WriteLine($"Survey file not found: {surveyPath}");
        return 3;
    }

    var report = JsonSerializer.Deserialize<BuildBuyIdentitySurveyReport>(await File.ReadAllTextAsync(surveyPath, CancellationToken.None));
    if (report is null)
    {
        Console.Error.WriteLine($"Survey file could not be parsed: {surveyPath}");
        return 4;
    }

    var baseCandidateMap = report.Samples
        .SelectMany(
            static sample => sample.ObjectDefinitionReferenceCandidates,
            static (sample, candidate) => new BuildBuyCandidateSource(
                sample.PackagePath,
                sample.ObjectDefinitionInternalName,
                candidate.Offset,
                candidate.Marker,
                candidate.TypeName,
                candidate.FullTgi))
        .GroupBy(static candidate => candidate.FullTgi, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(
            static group => group.Key,
            static group => group.ToArray(),
            StringComparer.OrdinalIgnoreCase);
    if (baseCandidateMap.Count == 0)
    {
        Console.Error.WriteLine("No candidate references were found in the survey.");
        return 5;
    }

    var lookupMap = new Dictionary<string, List<BuildBuyCandidateLookup>>(StringComparer.OrdinalIgnoreCase);
    foreach (var (fullTgi, sources) in baseCandidateMap)
    {
        if (!TryParseTgi(fullTgi, out var key))
        {
            continue;
        }

        AddCandidateLookup(lookupMap, "exact", key, fullTgi, sources);
        AddCandidateLookup(lookupMap, "instance-byte-reversed", key with { FullInstance = ReverseBytes(key.FullInstance) }, fullTgi, sources);
        AddCandidateLookup(lookupMap, "instance-swap32", key with { FullInstance = SwapUInt32Halves(key.FullInstance) }, fullTgi, sources);
        AddCandidateLookup(lookupMap, "instance-swap32-byte-reversed", key with { FullInstance = ReverseBytes(SwapUInt32Halves(key.FullInstance)) }, fullTgi, sources);
    }

    var packagePaths = Directory
        .EnumerateFiles(searchRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    if (maxPackages > 0)
    {
        packagePaths = SelectRepresentativePackagePaths(searchRoot, packagePaths, maxPackages);
    }

    var matchesByTgi = new Dictionary<string, List<BuildBuyCandidateMatch>>(StringComparer.OrdinalIgnoreCase);
    foreach (var tgi in baseCandidateMap.Keys)
    {
        matchesByTgi[tgi] = [];
    }

    for (var index = 0; index < packagePaths.Length; index++)
    {
        var packagePath = packagePaths[index];
        Console.WriteLine($"[{index + 1}/{packagePaths.Length}] {packagePath}");
        try
        {
            await using var stream = new FileStream(
                packagePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 131072,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var package = await DataBasePackedFile.FromStreamAsync(stream, CancellationToken.None);

            foreach (var key in package.Keys)
            {
                var fullTgi = $"{(uint)key.Type:X8}:{key.Group:X8}:{key.FullInstance:X16}";
                if (!lookupMap.TryGetValue(fullTgi, out var lookups))
                {
                    continue;
                }

                foreach (var lookup in lookups)
                {
                    matchesByTgi[lookup.SourceFullTgi].Add(new BuildBuyCandidateMatch(
                        RelativeToRoot(searchRoot, packagePath),
                        key.Type.ToString(),
                        fullTgi,
                        lookup.TransformName,
                        lookup.SourceCount));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  failed: {ex.Message}");
        }
    }

    var resolved = baseCandidateMap
        .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
        .Select(pair => new BuildBuyCandidateResolution(
            pair.Key,
            pair.Value.Select(static source => source).ToArray(),
            matchesByTgi[pair.Key].ToArray()))
        .ToArray();

    var resolutionReport = new BuildBuyCandidateResolutionReport(
        searchRoot,
        surveyPath,
        packagePaths.Length,
        DateTimeOffset.UtcNow,
        resolved,
        resolved.Count(static item => item.Matches.Count > 0),
        resolved.Count(static item => item.Matches.Count == 0));

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
    await File.WriteAllTextAsync(
        outputPath,
        JsonSerializer.Serialize(resolutionReport, new JsonSerializerOptions { WriteIndented = true }),
        CancellationToken.None);

    Console.WriteLine();
    Console.WriteLine($"Candidate resolution written to {outputPath}");
    Console.WriteLine($"Candidates: {resolved.Length}");
    Console.WriteLine($"Resolved: {resolutionReport.ResolvedCandidateCount}");
    Console.WriteLine($"Unresolved: {resolutionReport.UnresolvedCandidateCount}");

    return 0;
}

static void AddCandidateLookup(
    IDictionary<string, List<BuildBuyCandidateLookup>> lookupMap,
    string transformName,
    ResourceKeyRecord key,
    string sourceFullTgi,
    IReadOnlyList<BuildBuyCandidateSource> sources)
{
    var lookupTgi = key.FullTgi;
    if (!lookupMap.TryGetValue(lookupTgi, out var lookups))
    {
        lookups = [];
        lookupMap[lookupTgi] = lookups;
    }

    lookups.Add(new BuildBuyCandidateLookup(transformName, sourceFullTgi, sources.Count));
}

static ulong ReverseBytes(ulong value) => BinaryPrimitives.ReverseEndianness(value);

static ulong SwapUInt32Halves(ulong value) =>
    ((value & 0xFFFFFFFFUL) << 32) | (value >> 32);

static async Task<int> RunBuildBuyIdentitySurveyAsync(string searchRoot, int maxPackages, int pairsPerPackage, string outputPath)
{
    if (!Directory.Exists(searchRoot))
    {
        Console.Error.WriteLine($"Directory not found: {searchRoot}");
        return 2;
    }

    var packagePaths = Directory
        .EnumerateFiles(searchRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    if (packagePaths.Length == 0)
    {
        Console.Error.WriteLine($"No package files found under: {searchRoot}");
        return 3;
    }

    packagePaths = SelectRepresentativePackagePaths(searchRoot, packagePaths, Math.Max(1, maxPackages));

    var source = new DataSourceDefinition(
        Guid.Parse("66666666-6666-6666-6666-666666666666"),
        "BuildBuyIdentitySurvey",
        searchRoot,
        SourceKind.Game);
    var catalog = new LlamaResourceCatalogService();

    var sampledPairs = new List<BuildBuyIdentitySample>();
    var packageRows = new List<BuildBuyIdentityPackageRow>();
    var catalogLengthCounts = new Dictionary<int, int>();
    var definitionLengthCounts = new Dictionary<int, int>();
    var firstCatalogWordCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var objectDefinitionTypeHitCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    for (var index = 0; index < packagePaths.Length; index++)
    {
        var packagePath = packagePaths[index];
        Console.WriteLine($"[{index + 1}/{packagePaths.Length}] {packagePath}");
        try
        {
            var scan = await catalog.ScanPackageAsync(source, packagePath, progress: null, CancellationToken.None);
            var sameInstanceGroups = scan.Resources
                .GroupBy(static resource => resource.Key.FullInstance)
                .Select(static group => group.ToArray())
                .Where(static group =>
                    group.Any(static resource => resource.Key.TypeName == "ObjectCatalog") &&
                    group.Any(static resource => resource.Key.TypeName == "ObjectDefinition"))
                .OrderBy(static group => group[0].Key.FullInstance)
                .Take(Math.Max(1, pairsPerPackage))
                .ToArray();

            var localSamples = new List<BuildBuyIdentitySample>();
            foreach (var group in sameInstanceGroups)
            {
                var objectCatalog = group.First(static resource => resource.Key.TypeName == "ObjectCatalog");
                var objectDefinition = group.First(static resource => resource.Key.TypeName == "ObjectDefinition");

                var objectCatalogBytes = await catalog.GetResourceBytesAsync(objectCatalog.PackagePath, objectCatalog.Key, raw: false, CancellationToken.None);
                var objectDefinitionBytes = await catalog.GetResourceBytesAsync(objectDefinition.PackagePath, objectDefinition.Key, raw: false, CancellationToken.None);

                var catalogView = CreateObjectCatalogSurveyView(objectCatalogBytes);
                var definitionView = CreateObjectDefinitionSurveyView(objectDefinitionBytes, scan.Resources);

                IncrementCount(catalogLengthCounts, objectCatalogBytes.Length);
                IncrementCount(definitionLengthCounts, objectDefinitionBytes.Length);
                if (catalogView.FirstWords.Count > 0)
                {
                    IncrementNamed(firstCatalogWordCounts, $"0x{catalogView.FirstWords[0]:X8}");
                }

                foreach (var typeHit in definitionView.TypeHits.Select(static hit => hit.TypeName).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    IncrementNamed(objectDefinitionTypeHitCounts, typeHit);
                }

                var sample = new BuildBuyIdentitySample(
                    RelativeToRoot(searchRoot, packagePath),
                    objectCatalog.Key.FullInstance.ToString("X16"),
                    objectDefinition.Key.FullTgi,
                    objectCatalog.Key.FullTgi,
                    definitionView.InternalName,
                    objectDefinitionBytes.Length,
                    objectCatalogBytes.Length,
                    definitionView.HeaderVersion,
                    definitionView.DeclaredSize,
                    definitionView.InternalNameByteLength,
                    catalogView.FirstWords,
                    catalogView.NonZeroWordOffsets,
                    catalogView.TailQwordCandidates,
                    definitionView.TypeHits,
                    definitionView.ReferenceCandidates,
                    definitionView.TailQwordCandidates,
                    definitionView.Note,
                    catalogView.Note);

                sampledPairs.Add(sample);
                localSamples.Add(sample);
            }

            packageRows.Add(new BuildBuyIdentityPackageRow(
                RelativeToRoot(searchRoot, packagePath),
                scan.Resources.Count,
                sameInstanceGroups.Length,
                localSamples));

            Console.WriteLine($"  objd/cobj-pairs={sameInstanceGroups.Length} | sampled={localSamples.Count}");
        }
        catch (Exception ex)
        {
            packageRows.Add(new BuildBuyIdentityPackageRow(
                RelativeToRoot(searchRoot, packagePath),
                0,
                0,
                [],
                ex.Message));
            Console.WriteLine($"  failed: {ex.Message}");
        }
    }

    var report = new BuildBuyIdentitySurveyReport(
        searchRoot,
        packagePaths.Length,
        pairsPerPackage,
        DateTimeOffset.UtcNow,
        packageRows,
        sampledPairs,
        catalogLengthCounts.OrderByDescending(static pair => pair.Value).ThenBy(static pair => pair.Key).Select(static pair => new SurveyCountRow(pair.Key.ToString(), pair.Value)).ToArray(),
        definitionLengthCounts.OrderByDescending(static pair => pair.Value).ThenBy(static pair => pair.Key).Select(static pair => new SurveyCountRow(pair.Key.ToString(), pair.Value)).ToArray(),
        firstCatalogWordCounts.OrderByDescending(static pair => pair.Value).ThenBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(static pair => new SurveyCountRow(pair.Key, pair.Value)).Take(16).ToArray(),
        objectDefinitionTypeHitCounts.OrderByDescending(static pair => pair.Value).ThenBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(static pair => new SurveyCountRow(pair.Key, pair.Value)).ToArray());

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
    File.WriteAllText(
        outputPath,
        JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        }));

    Console.WriteLine();
    Console.WriteLine($"Build/Buy identity survey written to {outputPath}");
    Console.WriteLine($"Packages processed: {packagePaths.Length}");
    Console.WriteLine($"Sampled pairs: {sampledPairs.Count}");
    Console.WriteLine($"Catalog lengths: {FormatCoverageRows(report.ObjectCatalogLengths.Take(8))}");
    Console.WriteLine($"Definition lengths: {FormatCoverageRows(report.ObjectDefinitionLengths.Take(8))}");
    Console.WriteLine($"Catalog first-word frequencies: {FormatCoverageRows(report.ObjectCatalogFirstWordFrequencies.Take(8))}");
    Console.WriteLine($"OBJD embedded type hits: {FormatCoverageRows(report.ObjectDefinitionEmbeddedTypeHits.Take(12))}");

    return 0;
}

static async Task<int> SummarizeObjectCatalogFieldsAsync(string surveyPath, string outputPath)
{
    if (!File.Exists(surveyPath))
    {
        Console.Error.WriteLine($"Survey file not found: {surveyPath}");
        return 2;
    }

    BuildBuyIdentitySurveyReport? survey;
    await using (var stream = File.OpenRead(surveyPath))
    {
        survey = await JsonSerializer.DeserializeAsync<BuildBuyIdentitySurveyReport>(stream);
    }

    if (survey is null)
    {
        Console.Error.WriteLine($"Failed to read survey JSON: {surveyPath}");
        return 3;
    }

    var offsets = new[]
    {
        0x0000, 0x0004, 0x0008, 0x000C, 0x0010, 0x001C, 0x0020, 0x002C,
        0x0030, 0x0034, 0x0038, 0x003C, 0x0040, 0x0044, 0x0048, 0x004C,
        0x0050, 0x0054, 0x0058, 0x005C, 0x0068, 0x006C, 0x0074, 0x0078,
        0x007C, 0x0080, 0x0084, 0x0088, 0x0090, 0x0098, 0x00A8, 0x00AC, 0x00B0, 0x00B8, 0x00BC
    };

    var perOffset = new List<ObjectCatalogFieldSummaryRow>();
    foreach (var offset in offsets)
    {
        var hits = new List<(uint Value, string Name)>();
        foreach (var sample in survey.Samples)
        {
            var value = TryGetWordAtOffset(sample.ObjectCatalogNonZeroWordOffsets, offset);
            if (value.HasValue)
            {
                hits.Add((value.Value, sample.ObjectDefinitionInternalName ?? sample.FullInstance));
            }
        }

        if (hits.Count == 0)
        {
            continue;
        }

        var topValues = hits
            .GroupBy(static hit => hit.Value)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key)
            .Take(8)
            .Select(group => new SurveyCountRow($"0x{group.Key:X8}", group.Count()))
            .ToArray();
        var examples = hits
            .Take(8)
            .Select(static hit => $"0x{hit.Value:X8} => {hit.Name}")
            .ToArray();

        perOffset.Add(new ObjectCatalogFieldSummaryRow(
            $"+0x{offset:X4}",
            hits.Count,
            hits.Select(static hit => hit.Value).Distinct().Count(),
            topValues,
            examples));
    }

    var report = new ObjectCatalogFieldSummaryReport(
        survey.SearchRoot,
        surveyPath,
        DateTimeOffset.UtcNow,
        survey.Samples.Count,
        perOffset);

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
    await File.WriteAllTextAsync(
        outputPath,
        JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }),
        CancellationToken.None);

    Console.WriteLine($"ObjectCatalog field summary written to {outputPath}");
    foreach (var row in perOffset.Take(16))
    {
        Console.WriteLine($"{row.Offset}: samples={row.SampleCount}, distinct={row.DistinctValueCount}, top={FormatCoverageRows(row.TopValues)}");
    }

    return 0;
}

static async Task<int> RunIndexProfileAsync(string searchRoot, int maxPackages, int workerCount, string packageOrder)
{
    if (!Directory.Exists(searchRoot))
    {
        Console.Error.WriteLine($"Search root not found: {searchRoot}");
        return 1;
    }

    var profileRoot = Path.Combine(Environment.CurrentDirectory, "tmp", "profile-index-cache");
    if (Directory.Exists(profileRoot))
    {
        Directory.Delete(profileRoot, recursive: true);
    }

    var cache = new ProbeCacheService(profileRoot);
    cache.EnsureCreated();

    var scanner = new ProfilingPackageScanner(searchRoot, maxPackages, packageOrder);
    var catalog = new LlamaResourceCatalogService();
    var graphBuilder = new ExplicitAssetGraphBuilder(catalog);
    var store = new SqliteIndexStore(cache);
    await store.InitializeAsync(CancellationToken.None);

    var coordinator = new PackageIndexCoordinator(scanner, catalog, graphBuilder, store);
    var source = new DataSourceDefinition(
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        "ProfileSource",
        searchRoot,
        SourceKind.Game);

    var progress = new Progress<IndexingProgress>(snapshot =>
    {
        if (snapshot.Summary is not null)
        {
            Console.WriteLine($"SUMMARY elapsed={snapshot.Summary.TotalElapsed:hh\\:mm\\:ss} packages={snapshot.Summary.PackagesProcessed} resources={snapshot.Summary.ResourcesProcessed}");
            foreach (var phase in snapshot.Summary.PhaseBreakdown)
            {
                Console.WriteLine($"PHASE {phase}");
            }
        }
    });

    var stopwatch = Stopwatch.StartNew();
    await coordinator.RunAsync([source], progress, CancellationToken.None, workerCount);
    stopwatch.Stop();
    Console.WriteLine($"PROFILE complete in {stopwatch.Elapsed:hh\\:mm\\:ss} root={searchRoot} maxPackages={maxPackages} workers={workerCount} order={packageOrder}");
    return 0;
}

static uint? TryGetWordAtOffset(IReadOnlyList<string> nonZeroOffsets, int offset)
{
    var prefix = $"+0x{offset:X4}=";
    foreach (var entry in nonZeroOffsets)
    {
        if (!entry.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var hex = entry[prefix.Length..];
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            hex = hex[2..];
        }

        if (uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var value))
        {
            return value;
        }
    }

    return null;
}

static ObjectCatalogSurveyView CreateObjectCatalogSurveyView(byte[] bytes)
{
    var firstWords = new List<uint>();
    for (var offset = 0; offset + 4 <= bytes.Length && firstWords.Count < 12; offset += 4)
    {
        firstWords.Add(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)));
    }

    var nonZeroWordOffsets = new List<string>();
    for (var offset = 0; offset + 4 <= bytes.Length && nonZeroWordOffsets.Count < 24; offset += 4)
    {
        var value = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
        if (value != 0)
        {
            nonZeroWordOffsets.Add($"+0x{offset:X4}=0x{value:X8}");
        }
    }

    var tailQwordCandidates = new List<string>();
    for (var offset = Math.Max(0, bytes.Length - 32); offset + 8 <= bytes.Length; offset += 8)
    {
        var value = BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset, 8));
        tailQwordCandidates.Add($"+0x{offset:X4}=0x{value:X16}");
    }

    return new ObjectCatalogSurveyView(
        firstWords,
        nonZeroWordOffsets,
        tailQwordCandidates,
        "Heuristic only. These are stable binary views, not confirmed display-name/category fields.");
}

static ObjectDefinitionSurveyView CreateObjectDefinitionSurveyView(byte[] bytes, IReadOnlyList<ResourceMetadata> packageResources)
{
    if (bytes.Length < 10)
    {
        return new ObjectDefinitionSurveyView(0, 0, 0, null, [], [], [], "Too small to decode confirmed ObjectDefinition header.");
    }

    var version = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0, 2));
    var declaredSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(2, 4));
    var nameByteLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(6, 4));
    var internalName = nameByteLength > 0 && 10 + nameByteLength <= bytes.Length
        ? TryReadAscii(bytes, 10, (int)nameByteLength)
        : null;

    var typeHits = FindEmbeddedResourceTypeHits(bytes).ToArray();
                var referenceCandidates = ExtractObjectDefinitionReferenceCandidates(bytes, packageResources).ToArray();
    var tailQwordCandidates = new List<string>();
    for (var offset = Math.Max(0, bytes.Length - 64); offset + 8 <= bytes.Length; offset += 8)
    {
        var value = BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset, 8));
        tailQwordCandidates.Add($"+0x{offset:X4}=0x{value:X16}");
    }

    return new ObjectDefinitionSurveyView(
        version,
        declaredSize,
        nameByteLength,
        internalName,
        typeHits,
        referenceCandidates,
        tailQwordCandidates,
        "Header/internal name are confirmed. Embedded resource-type hits are byte-pattern candidates only, not decoded field semantics.");
}

static IEnumerable<ObjectDefinitionReferenceCandidate> ExtractObjectDefinitionReferenceCandidates(byte[] bytes, IReadOnlyList<ResourceMetadata> packageResources)
{
    var byTgi = packageResources.ToDictionary(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase);
    var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var yieldedByOffset = new HashSet<int>();

    for (var offset = 0; offset + 20 <= bytes.Length; offset++)
    {
        var marker = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
        if (marker is not 4u and not 9u and not 12u)
        {
            continue;
        }

        var instance = BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset + 4, 8));
        var type = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 12, 4));
        var group = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 16, 4));
        if (type == 0 || !Enum.IsDefined(typeof(ResourceType), type))
        {
            continue;
        }

        var key = new ResourceKeyRecord(type, group, instance, ((ResourceType)type).ToString());
        var isPackageLocalMatch = byTgi.TryGetValue(key.FullTgi, out var resource);
        if (!yieldedByOffset.Add(offset))
        {
            continue;
        }

        var dedupeKey = $"{offset}:{key.FullTgi}";
        if (!yielded.Add(dedupeKey))
        {
            continue;
        }

        yield return new ObjectDefinitionReferenceCandidate(
            offset,
            marker,
            key.FullTgi,
            key.TypeName,
            isPackageLocalMatch,
            isPackageLocalMatch ? resource!.PackagePath : null);
    }
}

static IEnumerable<EmbeddedTypeHit> FindEmbeddedResourceTypeHits(byte[] bytes)
{
    var typeMap = new (uint TypeId, string TypeName)[]
    {
        ((uint)ResourceType.Model, "Model"),
        ((uint)ResourceType.ModelLOD, "ModelLOD"),
        ((uint)ResourceType.ObjectCatalog, "ObjectCatalog"),
        ((uint)ResourceType.ObjectDefinition, "ObjectDefinition"),
        ((uint)ResourceType.MaterialDefinition, "MaterialDefinition"),
        ((uint)ResourceType.Rig, "Rig"),
        ((uint)ResourceType.Footprint, "Footprint"),
        ((uint)ResourceType.Slot, "Slot"),
        ((uint)ResourceType.Light, "Light"),
    };

    foreach (var (typeId, typeName) in typeMap)
    {
        var pattern = BitConverter.GetBytes(typeId);
        for (var offset = 0; offset <= bytes.Length - pattern.Length; offset++)
        {
            if (!bytes.AsSpan(offset, pattern.Length).SequenceEqual(pattern))
            {
                continue;
            }

            yield return new EmbeddedTypeHit(
                typeName,
                $"0x{typeId:X8}",
                offset,
                BuildHexWindow(bytes, Math.Max(0, offset - 8), Math.Min(bytes.Length - Math.Max(0, offset - 8), 24)));
        }
    }
}

static string BuildHexWindow(byte[] bytes, int offset, int length)
{
    if (length <= 0)
    {
        return string.Empty;
    }

    var slice = bytes.AsSpan(offset, length).ToArray();
    return string.Join(" ", slice.Select(static value => value.ToString("X2")));
}

static int GetBucketPriority(string packagePath)
{
    var fileName = Path.GetFileName(packagePath);
    var directory = Path.GetFileName(Path.GetDirectoryName(packagePath));
    return directory switch
    {
        "Data" => 0,
        "Delta" => 1,
        _ when directory is not null && directory.StartsWith("EP", StringComparison.OrdinalIgnoreCase) => 2,
        _ when directory is not null && directory.StartsWith("GP", StringComparison.OrdinalIgnoreCase) => 3,
        _ when directory is not null && directory.StartsWith("SP", StringComparison.OrdinalIgnoreCase) => 4,
        _ when directory is not null && directory.StartsWith("FP", StringComparison.OrdinalIgnoreCase) => 5,
        _ => fileName.Contains("FullBuild0", StringComparison.OrdinalIgnoreCase) ? 6 : 7
    };
}

static int GetPackagePriority(string packagePath)
{
    var fileName = Path.GetFileName(packagePath);
    if (fileName.Equals("ClientFullBuild0.package", StringComparison.OrdinalIgnoreCase))
    {
        return 0;
    }

    if (fileName.Equals("ClientDeltaBuild0.package", StringComparison.OrdinalIgnoreCase))
    {
        return 1;
    }

    if (fileName.Contains("FullBuild0", StringComparison.OrdinalIgnoreCase))
    {
        return 2;
    }

    if (fileName.Contains("DeltaBuild0", StringComparison.OrdinalIgnoreCase))
    {
        return 3;
    }

    if (fileName.Contains("Build0", StringComparison.OrdinalIgnoreCase))
    {
        return 4;
    }

    if (fileName.Contains("FullBuild", StringComparison.OrdinalIgnoreCase))
    {
        return 5;
    }

    if (fileName.Contains("DeltaBuild", StringComparison.OrdinalIgnoreCase))
    {
        return 6;
    }

    return 7;
}

static BatchInputEntry ParseBatchEntry(string line)
{
    var separators = new[] { '|', ';', '\t' };
    var parts = line.Split(separators, 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length != 2)
    {
        throw new InvalidDataException($"Invalid batch line: {line}");
    }

    return new BatchInputEntry(parts[0], parts[1]);
}

static async Task<ProbeAssetResult> ProbeSingleAssetAsync(string packagePath, string rootTgi, bool verbose)
{
    if (!File.Exists(packagePath))
    {
        return new ProbeAssetResult(packagePath, rootTgi, null, false, null);
    }

    var rootDirectory = Path.Combine(AppContext.BaseDirectory, "probe-cache");
    var cache = new ProbeCacheService(rootDirectory);
    cache.EnsureCreated();

    var store = new SqliteIndexStore(cache);
    await store.InitializeAsync(CancellationToken.None);

    var source = new DataSourceDefinition(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "ProbeSource",
        Path.GetDirectoryName(packagePath) ?? packagePath,
        SourceKind.Game);

    var catalog = new LlamaResourceCatalogService();
    var graphBuilder = new ExplicitAssetGraphBuilder(catalog);
    var sceneBuilder = new BuildBuySceneBuildService(catalog, store);

    if (verbose)
    {
        Console.WriteLine($"Scanning package: {packagePath}");
    }

    var scan = await catalog.ScanPackageAsync(source, packagePath, progress: null, CancellationToken.None);
    var assets = graphBuilder.BuildAssetSummaries(scan);
    await store.ReplacePackageAsync(scan, assets, CancellationToken.None);

    var asset = assets.FirstOrDefault(candidate =>
        candidate.AssetKind == AssetKind.BuildBuy &&
        string.Equals(candidate.RootKey.FullTgi, rootTgi, StringComparison.OrdinalIgnoreCase));

    if (asset is null)
    {
        return new ProbeAssetResult(packagePath, rootTgi, null, false, null);
    }

    var packageResources = await store.GetPackageResourcesAsync(asset.PackagePath, CancellationToken.None);
    var graph = await graphBuilder.BuildAssetGraphAsync(asset, packageResources, CancellationToken.None);
    var sceneResult = graph.BuildBuyGraph is null
        ? null
        : await sceneBuilder.BuildSceneAsync(graph.BuildBuyGraph.ModelResource, CancellationToken.None);

    return new ProbeAssetResult(packagePath, rootTgi, asset.DisplayName, true, sceneResult);
}

static Dictionary<string, int> ParseMaterialCoverage(IReadOnlyList<string> diagnostics)
    => ParseNamedCountLine(diagnostics, "Material coverage:");

static Dictionary<string, int> ParseMaterialSamplingSources(IReadOnlyList<string> diagnostics)
    => ParseNamedCountLine(diagnostics, "Material sampling sources:");

static Dictionary<string, int> ParseMaterialVisualPayloads(IReadOnlyList<string> diagnostics)
    => ParseNamedCountLine(diagnostics, "Material visual payloads:");

static Dictionary<string, int> ParseNamedCountLine(IReadOnlyList<string> diagnostics, string prefix)
{
    var line = diagnostics.FirstOrDefault(item => item.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    if (string.IsNullOrWhiteSpace(line))
    {
        return result;
    }

    var payload = line[prefix.Length..].Trim().TrimEnd('.');
    foreach (var token in payload.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
    {
        var pair = token.Split('=', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (pair.Length == 2 && int.TryParse(pair[1], out var value))
        {
            result[pair[0]] = value;
        }
    }

    return result;
}

static void Increment(Dictionary<SceneBuildStatus, int> counts, SceneBuildStatus status) =>
    counts[status] = counts.TryGetValue(status, out var current) ? current + 1 : 1;

static void IncrementNamed(Dictionary<string, int> counts, string key, int amount = 1) =>
    counts[key] = counts.TryGetValue(key, out var current) ? current + amount : amount;

static void IncrementCount(Dictionary<int, int> counts, int key, int amount = 1) =>
    counts[key] = counts.TryGetValue(key, out var current) ? current + amount : amount;

static void Add(Dictionary<string, int> counts, string key, int value) =>
    IncrementNamed(counts, key, value);

static void AddPayloadByFamily(
    Dictionary<string, int> counts,
    IReadOnlyDictionary<string, int> families,
    IReadOnlyDictionary<string, int> payloads)
{
    foreach (var family in families)
    {
        foreach (var payload in payloads)
        {
            Add(counts, $"{family.Key}/{payload.Key}", Math.Min(family.Value, payload.Value));
        }
    }
}

static string FormatCoverageMap(IReadOnlyDictionary<string, int> coverage) =>
    coverage.Count == 0
        ? "(none)"
        : string.Join(", ", coverage.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(static pair => $"{pair.Key}={pair.Value}"));

static void WriteTrimmedText(string text, int maxCharacters)
{
    if (text.Length <= maxCharacters)
    {
        Console.WriteLine(text);
        return;
    }

    Console.WriteLine(text[..maxCharacters]);
    Console.WriteLine($"... ({text.Length - maxCharacters:N0} more characters)");
}

static async Task DumpStringTableAsync(string packagePath, ResourceKeyRecord key)
{
    await using var stream = new FileStream(
        packagePath,
        FileMode.Open,
        FileAccess.Read,
        FileShare.ReadWrite | FileShare.Delete,
        bufferSize: 131072,
        options: FileOptions.Asynchronous | FileOptions.SequentialScan);
    await using var package = await DataBasePackedFile.FromStreamAsync(stream, CancellationToken.None);
    var table = await package.GetStringTableAsync(new ResourceKey((ResourceType)key.Type, key.Group, key.FullInstance), false, CancellationToken.None);

    Console.WriteLine("== String Table ==");
    Console.WriteLine($"Count: {table.Count}");
    var shown = 0;
    foreach (var hash in table.KeyHashes)
    {
        Console.WriteLine($"  0x{hash:X8} => {table.Get(hash)}");
        shown++;
        if (shown >= 12)
        {
            break;
        }
    }
}

static void DumpHex(byte[] bytes, int maxLength)
{
    var limit = Math.Min(bytes.Length, maxLength);
    Console.WriteLine($"Length: {bytes.Length:N0}");
    for (var offset = 0; offset < limit; offset += 16)
    {
        var slice = bytes.AsSpan(offset, Math.Min(16, limit - offset));
        var hex = string.Join(" ", slice.ToArray().Select(static b => b.ToString("X2")));
        var ascii = new string(slice.ToArray().Select(static b => b is >= 32 and <= 126 ? (char)b : '.').ToArray());
        Console.WriteLine($"{offset:X8}  {hex,-47}  {ascii}");
    }

    if (limit < bytes.Length)
    {
        Console.WriteLine($"... ({bytes.Length - limit:N0} more bytes)");
    }
}

static void DumpObjectDefinitionDecode(byte[] bytes)
{
    if (bytes.Length < 10)
    {
        Console.WriteLine("Too small to decode.");
        return;
    }

    var version = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0, 2));
    var declaredSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(2, 4));
    var nameByteLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(6, 4));
    var name = TryReadAscii(bytes, 10, (int)nameByteLength);
    var cursor = 10 + (int)nameByteLength;

    Console.WriteLine($"Confirmed internal name: {name ?? "(unreadable)"}");
    Console.WriteLine($"Header version(u16): {version}");
    Console.WriteLine($"Header declared-size(u32): {declaredSize}");
    Console.WriteLine($"Internal-name byte length(u32): {nameByteLength}");
    Console.WriteLine($"Remaining payload bytes after name: {Math.Max(0, bytes.Length - cursor)}");

    Console.WriteLine("Remaining payload as uint32 words:");
    DumpUInt32Table(bytes, cursor);

    if (bytes.Length - cursor >= 32)
    {
        Console.WriteLine("Tail qword candidates:");
        for (var offset = Math.Max(cursor, bytes.Length - 64); offset + 8 <= bytes.Length; offset += 8)
        {
            var value = BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset, 8));
            Console.WriteLine($"  +0x{offset:X4} = 0x{value:X16}");
        }
    }
}

static void DumpObjectCatalogDecode(byte[] bytes)
{
    if (bytes.Length < 16)
    {
        Console.WriteLine("Too small to decode.");
        return;
    }

    Console.WriteLine("Heuristic decode only. Human-readable display-name fields are not confirmed yet.");
    DumpUInt32Table(bytes, 0);

    if (bytes.Length >= 16)
    {
        Console.WriteLine("Tail qword candidates:");
        for (var offset = Math.Max(0, bytes.Length - 32); offset + 8 <= bytes.Length; offset += 8)
        {
            var value = BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset, 8));
            Console.WriteLine($"  +0x{offset:X4} = 0x{value:X16}");
        }
    }
}

static void DumpCasPartDecode(byte[] bytes)
{
    try
    {
        var internalName = TryReadBigEndianUnicodeString(bytes);
        var version = bytes.Length >= 4 ? BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0, 4)) : 0u;
        Console.WriteLine($"Confirmed internal name: {internalName ?? "(unreadable)"}");
        Console.WriteLine($"Header version(u32): {version}");
        Console.WriteLine("Heuristic decode only. Full CASP semantics still come from the dedicated parser in Assets.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"CASPart decode failed: {ex.Message}");
    }
}

static void DumpUInt32Table(byte[] bytes, int startOffset)
{
    var alignedStart = Math.Max(0, startOffset);
    for (var offset = alignedStart; offset + 4 <= bytes.Length; offset += 4)
    {
        var value = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
        Console.WriteLine($"  +0x{offset:X4} = 0x{value:X8} ({value})");
    }
}

static string? TryReadAscii(byte[] bytes, int offset, int byteLength)
{
    if (offset < 0 || byteLength < 0 || offset + byteLength > bytes.Length)
    {
        return null;
    }

    try
    {
        return Encoding.ASCII.GetString(bytes, offset, byteLength);
    }
    catch
    {
        return null;
    }
}

static string? TryReadBigEndianUnicodeString(byte[] bytes)
{
    if (bytes.Length < 13)
    {
        return null;
    }

    try
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        var byteLength = Read7BitEncodedInt(reader);
        if (byteLength <= 0 || stream.Position + byteLength > bytes.Length)
        {
            return null;
        }

        return Encoding.BigEndianUnicode.GetString(reader.ReadBytes(byteLength));
    }
    catch
    {
        return null;
    }
}

static int Read7BitEncodedInt(BinaryReader reader)
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

static bool TryParseTgi(string fullTgi, out ResourceKeyRecord key)
{
    key = default!;
    var parts = fullTgi.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length != 3 ||
        !uint.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, null, out var type) ||
        !uint.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, null, out var group) ||
        !ulong.TryParse(parts[2], System.Globalization.NumberStyles.HexNumber, null, out var instance))
    {
        return false;
    }

    key = new ResourceKeyRecord(type, group, instance, type.ToString("X8"));
    return true;
}

static void WriteSceneResult(SceneBuildResult result)
{
    Console.WriteLine($"Success: {result.Success}");
    Console.WriteLine($"Status: {result.Status}");
    if (result.Scene is null)
    {
        Console.WriteLine("Scene: (null)");
    }
    else
    {
        Console.WriteLine($"Scene: {result.Scene.Name}");
        Console.WriteLine($"Meshes: {result.Scene.Meshes.Count}");
        Console.WriteLine($"Materials: {result.Scene.Materials.Count}");
        Console.WriteLine($"Bones: {result.Scene.Bones.Count}");
        for (var materialIndex = 0; materialIndex < result.Scene.Materials.Count; materialIndex++)
        {
            var material = result.Scene.Materials[materialIndex];
            Console.WriteLine(
                $"  Material[{materialIndex}] {material.Name}: " +
                $"shader={material.ShaderName ?? "(null)"} " +
                $"source={material.SourceKind} " +
                $"alpha={material.AlphaMode ?? "(none)"} " +
                $"transparent={material.IsTransparent} " +
                $"approx={material.Approximation ?? "(none)"} " +
                $"textures={material.Textures.Count}");
            foreach (var texture in material.Textures)
            {
                var key = texture.SourceKey;
                Console.WriteLine(
                    $"    Texture slot={texture.Slot} " +
                    $"semantic={texture.Semantic} " +
                    $"sourceKind={texture.SourceKind} " +
                    $"file={texture.FileName} " +
                    $"source={(key is null ? "(null)" : key.FullTgi)} " +
                    $"package={DescribePackagePath(texture.SourcePackagePath)}");
                Console.WriteLine($"      PNG {DescribePng(texture.PngBytes)}");
                Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, "tmp", "probe-textures"));
                var outputPath = Path.Combine(Environment.CurrentDirectory, "tmp", "probe-textures", texture.FileName);
                File.WriteAllBytes(outputPath, texture.PngBytes);
                Console.WriteLine($"      Saved: {outputPath}");
            }
        }

        foreach (var mesh in result.Scene.Meshes)
        {
            Console.WriteLine($"  Mesh {mesh.Name}: positions={mesh.Positions.Count} indices={mesh.Indices.Count} material={mesh.MaterialIndex}");
            DumpMeshStats(mesh);
        }
    }

    Console.WriteLine("Diagnostics:");
    WriteLines(result.Diagnostics);
}

static string DescribePackagePath(string? packagePath)
{
    if (string.IsNullOrWhiteSpace(packagePath))
    {
        return "(unknown)";
    }

    var fileName = Path.GetFileName(packagePath);
    var directoryName = Path.GetFileName(Path.GetDirectoryName(packagePath));
    if (string.IsNullOrWhiteSpace(directoryName))
    {
        return fileName;
    }

    return $"{directoryName}\\{fileName}";
}

static void WriteLines(IEnumerable<string> lines)
{
    foreach (var line in lines.DefaultIfEmpty("(none)"))
    {
        Console.WriteLine($"  {line}");
    }
}

static void DumpMeshStats(CanonicalMesh mesh)
{
    var vertexCount = mesh.Positions.Count / 3;
    var minIndex = mesh.Indices.Count == 0 ? -1 : mesh.Indices.Min();
    var maxIndex = mesh.Indices.Count == 0 ? -1 : mesh.Indices.Max();
    var validTriangles = 0;
    var invalidTriangles = 0;
    for (var i = 0; i + 2 < mesh.Indices.Count; i += 3)
    {
        var a = mesh.Indices[i];
        var b = mesh.Indices[i + 1];
        var c = mesh.Indices[i + 2];
        if (a >= 0 && b >= 0 && c >= 0 && a < vertexCount && b < vertexCount && c < vertexCount)
        {
            validTriangles++;
        }
        else
        {
            invalidTriangles++;
        }
    }

    Console.WriteLine($"    vertexCount={vertexCount} minIndex={minIndex} maxIndex={maxIndex} validTriangles={validTriangles} invalidTriangles={invalidTriangles}");
    var sampleIndices = mesh.Indices.Take(18).ToArray();
    Console.WriteLine($"    firstIndices: {string.Join(", ", sampleIndices)}");
    DumpTriangleQuality(mesh);
    var sampleVertexCount = Math.Min(vertexCount, 4);
    for (var i = 0; i < sampleVertexCount; i++)
    {
        var baseIndex = i * 3;
        Console.WriteLine($"    v[{i}] = ({mesh.Positions[baseIndex]}, {mesh.Positions[baseIndex + 1]}, {mesh.Positions[baseIndex + 2]})");
    }

    if (mesh.Uvs.Count >= 2)
    {
        var uvMinU = float.PositiveInfinity;
        var uvMinV = float.PositiveInfinity;
        var uvMaxU = float.NegativeInfinity;
        var uvMaxV = float.NegativeInfinity;
        for (var i = 0; i + 1 < mesh.Uvs.Count; i += 2)
        {
            var u = mesh.Uvs[i];
            var v = mesh.Uvs[i + 1];
            uvMinU = Math.Min(uvMinU, u);
            uvMinV = Math.Min(uvMinV, v);
            uvMaxU = Math.Max(uvMaxU, u);
            uvMaxV = Math.Max(uvMaxV, v);
        }

        Console.WriteLine($"    uvRange=({uvMinU}, {uvMinV})..({uvMaxU}, {uvMaxV})");
        var uvSampleCount = Math.Min(mesh.Uvs.Count / 2, 6);
        for (var i = 0; i < uvSampleCount; i++)
        {
            var baseIndex = i * 2;
            Console.WriteLine($"    uv[{i}] = ({mesh.Uvs[baseIndex]}, {mesh.Uvs[baseIndex + 1]})");
        }
    }

    DumpNamedUvRange("uv0", mesh.Uv0s);
    DumpNamedUvRange("uv1", mesh.Uv1s);
}

static void DumpNamedUvRange(string label, IReadOnlyList<float>? uvs)
{
    if (uvs is null || uvs.Count < 2)
    {
        Console.WriteLine($"    {label}Range=(none)");
        return;
    }

    var uvMinU = float.PositiveInfinity;
    var uvMinV = float.PositiveInfinity;
    var uvMaxU = float.NegativeInfinity;
    var uvMaxV = float.NegativeInfinity;
    for (var i = 0; i + 1 < uvs.Count; i += 2)
    {
        var u = uvs[i];
        var v = uvs[i + 1];
        uvMinU = Math.Min(uvMinU, u);
        uvMinV = Math.Min(uvMinV, v);
        uvMaxU = Math.Max(uvMaxU, u);
        uvMaxV = Math.Max(uvMaxV, v);
    }

    Console.WriteLine($"    {label}Range=({uvMinU}, {uvMinV})..({uvMaxU}, {uvMaxV})");
}

static void DumpTriangleQuality(CanonicalMesh mesh)
{
    var vertexCount = mesh.Positions.Count / 3;
    if (vertexCount == 0 || mesh.Indices.Count < 3)
    {
        return;
    }

    double totalEdge = 0;
    double maxEdge = 0;
    var edgeCount = 0;
    for (var i = 0; i + 2 < mesh.Indices.Count; i += 3)
    {
        var ia = mesh.Indices[i];
        var ib = mesh.Indices[i + 1];
        var ic = mesh.Indices[i + 2];
        if (ia < 0 || ib < 0 || ic < 0 || ia >= vertexCount || ib >= vertexCount || ic >= vertexCount)
        {
            continue;
        }

        var a = ReadPosition(mesh.Positions, ia);
        var b = ReadPosition(mesh.Positions, ib);
        var c = ReadPosition(mesh.Positions, ic);
        AddEdge(a, b, ref totalEdge, ref maxEdge, ref edgeCount);
        AddEdge(b, c, ref totalEdge, ref maxEdge, ref edgeCount);
        AddEdge(c, a, ref totalEdge, ref maxEdge, ref edgeCount);
    }

    if (edgeCount > 0)
    {
        Console.WriteLine($"    avgEdge={(totalEdge / edgeCount):0.####} maxEdge={maxEdge:0.####}");
    }
}

static (double X, double Y, double Z) ReadPosition(IReadOnlyList<float> positions, int vertexIndex)
{
    var offset = vertexIndex * 3;
    return (positions[offset], positions[offset + 1], positions[offset + 2]);
}

static void AddEdge((double X, double Y, double Z) a, (double X, double Y, double Z) b, ref double totalEdge, ref double maxEdge, ref int edgeCount)
{
    var dx = a.X - b.X;
    var dy = a.Y - b.Y;
    var dz = a.Z - b.Z;
    var len = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    totalEdge += len;
    maxEdge = Math.Max(maxEdge, len);
    edgeCount++;
}

static string DescribePng(byte[] pngBytes)
{
    try
    {
        if (pngBytes.Length < 24 || !pngBytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            return $"bytes={pngBytes.Length} invalid-png";
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(pngBytes.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(pngBytes.AsSpan(20, 4));
        using var source = new MemoryStream(pngBytes, writable: false);
        using var png = new GZipStream(source, System.IO.Compression.CompressionMode.Decompress, leaveOpen: true);
        return $"bytes={pngBytes.Length} size={width}x{height}";
    }
    catch
    {
        try
        {
            var width = BinaryPrimitives.ReadInt32BigEndian(pngBytes.AsSpan(16, 4));
            var height = BinaryPrimitives.ReadInt32BigEndian(pngBytes.AsSpan(20, 4));
            return $"bytes={pngBytes.Length} size={width}x{height}";
        }
        catch
        {
            return $"bytes={pngBytes.Length}";
        }
    }
}

static async Task DumpRcolChunkSummary(LlamaResourceCatalogService catalog, ResourceMetadata resource)
{
    var resolved = await ResolveCompanionResourceAsync(catalog, resource);
    if (!resolved.PackagePath.Equals(resource.PackagePath, StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"Resolved from companion: {resolved.PackagePath}");
    }

    var bytes = await catalog.GetResourceBytesAsync(resolved.PackagePath, resolved.Key, raw: false, CancellationToken.None);
    Console.WriteLine($"Bytes: {bytes.Length}");

    if (bytes.Length < 20)
    {
        Console.WriteLine("RCOL: too small");
        return;
    }

    var publicChunks = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4, 4));
    var externalCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12, 4));
    var chunkCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16, 4));
    Console.WriteLine($"RCOL: public={publicChunks} external={externalCount} chunks={chunkCount}");

    var offset = 20;
    var chunkKeys = new List<(uint Type, uint Group, ulong Instance)>();
    for (var index = 0; index < chunkCount && offset + 16 <= bytes.Length; index++)
    {
        var type = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
        var group = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4, 4));
        var instance = BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset + 8, 8));
        chunkKeys.Add((type, group, instance));
        offset += 16;
    }

    if (externalCount > 0)
    {
        Console.WriteLine("External keys:");
    }

    for (var index = 0; index < externalCount && offset + 16 <= bytes.Length; index++)
    {
        var type = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
        var group = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4, 4));
        var instance = BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset + 8, 8));
        Console.WriteLine($"  External {index}: {type:X8}:{group:X8}:{instance:X16}");
        offset += 16;
    }

    for (var index = 0; index < chunkCount && offset + 8 <= bytes.Length; index++)
    {
        var position = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
        var length = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + 4, 4));
        var tag = position + 4 <= bytes.Length
            ? System.Text.Encoding.ASCII.GetString(bytes, (int)position, Math.Min(4, bytes.Length - (int)position))
            : "(oob)";
        Console.WriteLine($"  Chunk {index}: pos=0x{position:X} len={length} tag={tag}");
        if (tag == "MLOD")
        {
            DumpMlod(bytes.AsSpan((int)position, Math.Min(length, bytes.Length - (int)position)));
            ManualWalkMlod(bytes.AsSpan((int)position, Math.Min(length, bytes.Length - (int)position)));
        }
        else if (tag == "MODL")
        {
            DumpModl(bytes.AsSpan((int)position, Math.Min(length, bytes.Length - (int)position)));
        }
        else if (tag == "VRTF")
        {
            DumpVrtf(bytes.AsSpan((int)position, Math.Min(length, bytes.Length - (int)position)));
        }
        else if (tag == "VBUF")
        {
            DumpVbuf(bytes.AsSpan((int)position, Math.Min(length, bytes.Length - (int)position)), chunkKeys, publicChunks);
        }
        else if (tag == "IBUF")
        {
            DumpIbuf(bytes.AsSpan((int)position, Math.Min(length, bytes.Length - (int)position)));
        }
        else if (tag == "MATD")
        {
            DumpMatd(bytes.AsSpan((int)position, Math.Min(length, bytes.Length - (int)position)));
        }
        else if (tag == "MTST")
        {
            DumpMtst(bytes.AsSpan((int)position, Math.Min(length, bytes.Length - (int)position)));
        }
        else if (tag == "\u0001\u0000\u0000\u0000" || string.IsNullOrWhiteSpace(tag) || tag.Any(static ch => ch < 32))
        {
            DumpUnknownChunk(bytes.AsSpan((int)position, Math.Min(length, bytes.Length - (int)position)));
        }

        offset += 8;
    }
}

static async Task<ResourceMetadata> ResolveCompanionResourceAsync(LlamaResourceCatalogService catalog, ResourceMetadata resource)
{
    var bytes = await catalog.GetResourceBytesAsync(resource.PackagePath, resource.Key, raw: false, CancellationToken.None);
    if (bytes.Length > 0)
    {
        return resource;
    }

    foreach (var companionPackagePath in EnumerateCompanionPackagePaths(resource.PackagePath))
    {
        var companionBytes = await catalog.GetResourceBytesAsync(companionPackagePath, resource.Key, raw: false, CancellationToken.None);
        if (companionBytes.Length > 0)
        {
            return resource with { PackagePath = companionPackagePath };
        }
    }

    return resource;
}

static IEnumerable<string> EnumerateCompanionPackagePaths(string packagePath)
{
    if (string.IsNullOrWhiteSpace(packagePath))
    {
        yield break;
    }

    var deltaMarker = $"{Path.DirectorySeparatorChar}Delta{Path.DirectorySeparatorChar}";
    if (packagePath.IndexOf(deltaMarker, StringComparison.OrdinalIgnoreCase) < 0)
    {
        yield break;
    }

    var fullPath = packagePath.Replace(deltaMarker, $"{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    fullPath = fullPath.Replace("ClientDeltaBuild", "ClientFullBuild", StringComparison.OrdinalIgnoreCase);
    fullPath = fullPath.Replace("SimulationDeltaBuild", "SimulationFullBuild", StringComparison.OrdinalIgnoreCase);
    if (!fullPath.Equals(packagePath, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
    {
        yield return fullPath;
    }
}

static void DumpMatd(ReadOnlySpan<byte> bytes)
{
    Console.WriteLine("  MATD dump:");
    Console.WriteLine($"    bytes={bytes.Length}");
    DumpMatdPropertyTable(bytes);
    var dumpLength = Math.Min(bytes.Length, 0x340);
    for (var i = 0; i < dumpLength; i += 16)
    {
        var line = bytes.Slice(i, Math.Min(16, dumpLength - i));
        var hex = string.Join(" ", line.ToArray().Select(static b => b.ToString("X2")));
        var ascii = new string(line.ToArray().Select(static b => b >= 32 && b < 127 ? (char)b : '.').ToArray());
        Console.WriteLine($"    0x{i:X4}: {hex,-47} {ascii}");
    }
}

static void DumpMatdPropertyTable(ReadOnlySpan<byte> bytes)
{
    var mtrlOffset = bytes.IndexOf("MTRL"u8);
    if (mtrlOffset < 0 || mtrlOffset + 16 > bytes.Length)
    {
        Console.WriteLine("    MTRL property table: (missing)");
        return;
    }

    var propertyCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(mtrlOffset + 12, 4));
    var tableOffset = mtrlOffset + 16;
    Console.WriteLine($"    MTRL property count={propertyCount}");
    for (var i = 0; i < propertyCount && tableOffset + 16 <= bytes.Length; i++, tableOffset += 16)
    {
        var propertyHash = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(tableOffset, 4));
        var rawType = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(tableOffset + 4, 4));
        var normalizedType = rawType & 0xFFFF;
        var propertyArity = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(tableOffset + 8, 4));
        var propertyOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(tableOffset + 12, 4));
        Console.WriteLine(
            $"    prop[{i}] hash=0x{propertyHash:X8} rawType=0x{rawType:X8} type={normalizedType} arity={propertyArity} offset=0x{propertyOffset:X}");

        if (propertyOffset + 16 <= bytes.Length)
        {
            var valueBytes = bytes.Slice((int)propertyOffset, 16).ToArray();
            Console.WriteLine($"      value16={string.Join(" ", valueBytes.Select(static b => b.ToString("X2")))}");
        }
    }
}

static void DumpMtst(ReadOnlySpan<byte> bytes)
{
    Console.WriteLine("  MTST dump:");
    Console.WriteLine($"    bytes={bytes.Length}");
    var dumpLength = Math.Min(bytes.Length, 0x140);
    for (var i = 0; i < dumpLength; i += 16)
    {
        var line = bytes.Slice(i, Math.Min(16, dumpLength - i));
        var hex = string.Join(" ", line.ToArray().Select(static b => b.ToString("X2")));
        var ascii = new string(line.ToArray().Select(static b => b >= 32 && b < 127 ? (char)b : '.').ToArray());
        Console.WriteLine($"    0x{i:X4}: {hex,-47} {ascii}");
    }
}

static void DumpMlod(ReadOnlySpan<byte> bytes)
{
    Console.WriteLine($"  MLOD bytes: {bytes.Length}");
    if (bytes.Length < 12)
    {
        return;
    }

    var version = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4));
    var meshCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(8, 4));
    Console.WriteLine($"  MLOD version=0x{version:X8} meshCount={meshCount}");

    var dumpLength = Math.Min(bytes.Length, 0xC0);
    for (var i = 0; i < dumpLength; i += 16)
    {
        var line = bytes.Slice(i, Math.Min(16, dumpLength - i));
        var hex = string.Join(" ", line.ToArray().Select(static b => b.ToString("X2")));
        Console.WriteLine($"    0x{i:X4}: {hex}");
    }
}

static void DumpModl(ReadOnlySpan<byte> bytes)
{
    Console.WriteLine($"  MODL bytes: {bytes.Length}");
    if (bytes.Length < 12)
    {
        return;
    }

    var version = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4));
    var lodCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(8, 4));
    Console.WriteLine($"  MODL version=0x{version:X8} lodCount={lodCount}");

    var dumpLength = Math.Min(bytes.Length, 0x140);
    for (var i = 0; i < dumpLength; i += 16)
    {
        var line = bytes.Slice(i, Math.Min(16, dumpLength - i));
        var hex = string.Join(" ", line.ToArray().Select(static b => b.ToString("X2")));
        Console.WriteLine($"    0x{i:X4}: {hex}");
    }

    try
    {
        var modl = PreviewDebugProbe.ParseModl(bytes.ToArray());
        Console.WriteLine("  MODL parsed entries:");
        for (var i = 0; i < modl.Count; i++)
        {
            var entry = modl[i];
            Console.WriteLine(
                $"    [{i}] ref=0x{entry.ReferenceRaw:X8} type={entry.ReferenceType} index={entry.ReferenceIndex} " +
                $"flags=0x{entry.Flags:X8} id=0x{entry.Id:X8} minZ={entry.MinZ} maxZ={entry.MaxZ}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  MODL parsed entries: failed ({ex.Message})");
    }
}

static void ManualWalkMlod(ReadOnlySpan<byte> bytes)
{
    Console.WriteLine("  Manual MLOD walk:");
    try
    {
        var offset = 0;
        offset += 4; // tag
        var version = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
        offset += 4;
        var meshCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));
        offset += 4;
        Console.WriteLine($"    version=0x{version:X8} meshCount={meshCount}");

        for (var i = 0; i < meshCount; i++)
        {
            var expectedSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
            offset += 4;
            var meshStart = offset;
            Console.WriteLine($"    mesh[{i}] expectedSize={expectedSize}");

            offset += 4;  // name
            offset += 4;  // material
            offset += 4;  // vrtf
            offset += 4;  // vbuf
            offset += 4;  // ibuf
            offset += 4;  // primitive/flags
            offset += 4;  // streamOffset
            offset += 4;  // startVertex
            offset += 4;  // startIndex
            offset += 4;  // minVertexIndex
            offset += 4;  // vertexCount
            offset += 4;  // primitiveCount
            offset += 24; // bounds

            var bytesRemainingInMesh = (int)expectedSize - (offset - meshStart);
            Console.WriteLine($"      after bounds pos=0x{offset:X} remaining={bytesRemainingInMesh}");

            if (bytesRemainingInMesh >= 4)
            {
                var skinRef = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
                offset += 4;
                bytesRemainingInMesh = (int)expectedSize - (offset - meshStart);
                Console.WriteLine($"      skin=0x{skinRef:X8} remaining={bytesRemainingInMesh}");
            }

            var jointCount = 0;
            if (bytesRemainingInMesh >= 4)
            {
                jointCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));
                offset += 4;
                bytesRemainingInMesh = (int)expectedSize - (offset - meshStart);
                Console.WriteLine($"      jointCount={jointCount} remaining={bytesRemainingInMesh}");
            }

            if (jointCount > 0)
            {
                offset += jointCount * 4;
                bytesRemainingInMesh = (int)expectedSize - (offset - meshStart);
                Console.WriteLine($"      after joints pos=0x{offset:X} remaining={bytesRemainingInMesh}");
            }

            if (bytesRemainingInMesh >= 4)
            {
                var scaleOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
                offset += 4;
                bytesRemainingInMesh = (int)expectedSize - (offset - meshStart);
                Console.WriteLine($"      scaleOffset=0x{scaleOffset:X8} remaining={bytesRemainingInMesh}");
            }

            var geometryStateCount = 0;
            if (bytesRemainingInMesh >= 4)
            {
                geometryStateCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));
                offset += 4;
                bytesRemainingInMesh = (int)expectedSize - (offset - meshStart);
                Console.WriteLine($"      geometryStateCount={geometryStateCount} remaining={bytesRemainingInMesh}");
            }

            if (geometryStateCount > 0)
            {
                offset += geometryStateCount * 20;
                bytesRemainingInMesh = (int)expectedSize - (offset - meshStart);
                Console.WriteLine($"      after geostates pos=0x{offset:X} remaining={bytesRemainingInMesh}");
            }

            if (version > 0x00000201 && bytesRemainingInMesh >= 20)
            {
                offset += 20;
                bytesRemainingInMesh = (int)expectedSize - (offset - meshStart);
                Console.WriteLine($"      after parent/mirror pos=0x{offset:X} remaining={bytesRemainingInMesh}");
            }

            if (version > 0x00000203 && bytesRemainingInMesh >= 4)
            {
                offset += 4;
                bytesRemainingInMesh = (int)expectedSize - (offset - meshStart);
                Console.WriteLine($"      after unknown1 pos=0x{offset:X} remaining={bytesRemainingInMesh}");
            }
        }

        Console.WriteLine($"    final pos=0x{offset:X} total={bytes.Length}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"    manual walk failed: {ex.Message}");
    }
}

static void DumpVrtf(ReadOnlySpan<byte> bytes)
{
    Console.WriteLine("  VRTF decode:");
    if (bytes.Length < 16)
    {
        Console.WriteLine("    too small");
        return;
    }

    var version = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4));
    var stride = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(8, 2));
    var elementCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(10, 2));
    Console.WriteLine($"    version=0x{version:X8} stride={stride} elements={elementCount}");

    var dumpLength = Math.Min(bytes.Length, 0x30);
    for (var i = 0; i < dumpLength; i += 16)
    {
        var line = bytes.Slice(i, Math.Min(16, dumpLength - i));
        var hex = string.Join(" ", line.ToArray().Select(static b => b.ToString("X2")));
        Console.WriteLine($"    0x{i:X4}: {hex}");
    }

    var offset = 16;
    for (var i = 0; i < elementCount && offset + 8 <= bytes.Length; i++)
    {
        var usage = bytes[offset];
        var usageIndex = bytes[offset + 1];
        var format = bytes[offset + 2];
        var elementOffset = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset + 4, 2));
        Console.WriteLine($"    elem[{i}] usage={usage} usageIndex={usageIndex} format=0x{format:X2} offset={elementOffset}");
        offset += 8;
    }
}

static void DumpIbuf(ReadOnlySpan<byte> bytes)
{
    Console.WriteLine("  IBUF decode:");
    if (bytes.Length < 12)
    {
        Console.WriteLine("    too small");
        return;
    }

    var version = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4));
    var flags = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4));
    Console.WriteLine($"    version=0x{version:X8} flags=0x{flags:X8} payload={bytes.Length - 12}");

    var dumpLength = Math.Min(bytes.Length, 0x30);
    for (var i = 0; i < dumpLength; i += 16)
    {
        var line = bytes.Slice(i, Math.Min(16, dumpLength - i));
        var hex = string.Join(" ", line.ToArray().Select(static b => b.ToString("X2")));
        Console.WriteLine($"    0x{i:X4}: {hex}");
    }

    var payload = bytes.Slice(12);
    var sample16 = new List<ushort>();
    for (var i = 0; i + 1 < payload.Length && sample16.Count < 12; i += 2)
    {
        sample16.Add(BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(i, 2)));
    }

    var sample32 = new List<uint>();
    for (var i = 0; i + 3 < payload.Length && sample32.Count < 12; i += 4)
    {
        sample32.Add(BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(i, 4)));
    }

    Console.WriteLine($"    sample16: {string.Join(", ", sample16)}");
    Console.WriteLine($"    sample32: {string.Join(", ", sample32)}");
}

static void DumpVbuf(ReadOnlySpan<byte> bytes, IReadOnlyList<(uint Type, uint Group, ulong Instance)> chunkKeys, int publicChunks)
{
    Console.WriteLine("  VBUF decode:");
    if (bytes.Length < 16)
    {
        Console.WriteLine("    too small");
        return;
    }

    var version = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4));
    var flags = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4));
    var swizzleRefRaw = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(12, 4));
    Console.WriteLine($"    version=0x{version:X8} flags=0x{flags:X8} swizzleRef=0x{swizzleRefRaw:X8} payload={bytes.Length - 16}");

    var dumpLength = Math.Min(bytes.Length, 0x30);
    for (var i = 0; i < dumpLength; i += 16)
    {
        var line = bytes.Slice(i, Math.Min(16, dumpLength - i));
        var hex = string.Join(" ", line.ToArray().Select(static b => b.ToString("X2")));
        Console.WriteLine($"    0x{i:X4}: {hex}");
    }

    if (swizzleRefRaw == 0)
    {
        return;
    }

    var referenceType = swizzleRefRaw >> 28;
    var referenceIndex = (int)(swizzleRefRaw & 0x0FFFFFFF) - 1;
    var resolvedIndex = referenceType switch
    {
        0 => referenceIndex,
        1 => publicChunks + referenceIndex,
        _ => -1
    };

    Console.WriteLine($"    swizzleRef type={referenceType} refIndex={referenceIndex} resolvedChunk={resolvedIndex}");
    if (resolvedIndex >= 0 && resolvedIndex < chunkKeys.Count)
    {
        var key = chunkKeys[resolvedIndex];
        Console.WriteLine($"    swizzleKey={key.Type:X8}:{key.Group:X8}:{key.Instance:X16}");
    }
}

static void DumpUnknownChunk(ReadOnlySpan<byte> bytes)
{
    Console.WriteLine("  Unknown chunk dump:");
    var dumpLength = Math.Min(bytes.Length, 0x40);
    for (var i = 0; i < dumpLength; i += 16)
    {
        var line = bytes.Slice(i, Math.Min(16, dumpLength - i));
        var hex = string.Join(" ", line.ToArray().Select(static b => b.ToString("X2")));
        Console.WriteLine($"    0x{i:X4}: {hex}");
    }
}

file sealed class ProbeCacheService(string root) : ICacheService
{
    public string AppRoot { get; } = root;
    public string CacheRoot { get; } = Path.Combine(root, "cache");
    public string ExportRoot { get; } = Path.Combine(root, "exports");
    public string DatabasePath { get; } = Path.Combine(root, "cache", "index.sqlite");

    public void EnsureCreated()
    {
        Directory.CreateDirectory(AppRoot);
        Directory.CreateDirectory(CacheRoot);
        Directory.CreateDirectory(ExportRoot);
    }
}

file sealed record BatchInputEntry(string PackagePath, string RootTgi);

file sealed record BatchCoverageRow(
    string PackagePath,
    string RootTgi,
    string DisplayName,
    string SceneStatus,
    IReadOnlyDictionary<string, int> MaterialCoverage,
    IReadOnlyDictionary<string, int> MaterialSamplingSources,
    IReadOnlyDictionary<string, int> MaterialVisualPayloads,
    IReadOnlyDictionary<string, int> MaterialFamilies,
    IReadOnlyDictionary<string, int> MaterialStrategies);

file sealed record ProbeAssetResult(
    string PackagePath,
    string RootTgi,
    string? DisplayName,
    bool Found,
    SceneBuildResult? SceneResult);

file sealed record BatchCoverageSummary(
    string InputPath,
    int TotalEntries,
    int ProcessedEntries,
    int ResolvedScenes,
    int TimedOutEntries,
    int FailedEntries,
    Dictionary<string, int> SceneStatuses,
    Dictionary<string, int> MaterialCoverage,
    Dictionary<string, int> MaterialSamplingSources,
    Dictionary<string, int> MaterialVisualPayloads,
    Dictionary<string, int> MaterialFamilies,
    Dictionary<string, int> MaterialPayloadByFamily,
    Dictionary<string, int> MaterialStrategies,
    DateTimeOffset LastUpdatedUtc,
    TimeSpan? Elapsed,
    TimeSpan? EstimatedRemaining);

file sealed record ProbeProcessResult(BatchCoverageRow? Row, bool TimedOut);

file sealed record BuildBuyCandidateSource(
    string SourcePackagePath,
    string? ObjectDefinitionInternalName,
    int Offset,
    uint Marker,
    string TypeName,
    string FullTgi);

file sealed record BuildBuyCandidateMatch(
    string PackagePath,
    string TypeName,
    string FullTgi,
    string TransformName,
    int SourceCount);

file sealed record BuildBuyCandidateLookup(
    string TransformName,
    string SourceFullTgi,
    int SourceCount);

file sealed record BuildBuyCandidateResolution(
    string FullTgi,
    IReadOnlyList<BuildBuyCandidateSource> Sources,
    IReadOnlyList<BuildBuyCandidateMatch> Matches);

file sealed record BuildBuyCandidateResolutionReport(
    string SearchRoot,
    string SurveyPath,
    int ProcessedPackageCount,
    DateTimeOffset GeneratedUtc,
    IReadOnlyList<BuildBuyCandidateResolution> Candidates,
    int ResolvedCandidateCount,
    int UnresolvedCandidateCount);

file sealed record ThreeDimensionalSurveyReport(
    string SearchRoot,
    int TotalPackageCount,
    int ProcessedPackageCount,
    int NameSamplesPerType,
    DateTimeOffset GeneratedUtc,
    int ThreeDimensionalGroupCount,
    IReadOnlyList<SurveyPackageRow> Packages,
    IReadOnlyDictionary<string, int> AssetKindCounts,
    IReadOnlyDictionary<string, int> ResourceTypeCounts,
    IReadOnlyDictionary<string, int> ThreeDimensionalComponentCounts,
    IReadOnlyList<SurveyCountRow> ThreeDimensionalCooccurringTypes,
    IReadOnlyList<SurveyCountRow> ThreeDimensionalGroupPatterns,
    IReadOnlyList<SurveyNameLookupSummary> NameLookupByType,
    IReadOnlyList<SurveyAssetDisplaySample> CurrentAssetDisplaySamples);

file sealed record BuildBuyIdentitySurveyReport(
    string SearchRoot,
    int ProcessedPackageCount,
    int PairsPerPackage,
    DateTimeOffset GeneratedUtc,
    IReadOnlyList<BuildBuyIdentityPackageRow> Packages,
    IReadOnlyList<BuildBuyIdentitySample> Samples,
    IReadOnlyList<SurveyCountRow> ObjectCatalogLengths,
    IReadOnlyList<SurveyCountRow> ObjectDefinitionLengths,
    IReadOnlyList<SurveyCountRow> ObjectCatalogFirstWordFrequencies,
    IReadOnlyList<SurveyCountRow> ObjectDefinitionEmbeddedTypeHits);

file sealed record BuildBuyIdentityPackageRow(
    string RelativePath,
    int ResourceCount,
    int SameInstancePairCount,
    IReadOnlyList<BuildBuyIdentitySample> Samples,
    string? Error = null);

file sealed record BuildBuyIdentitySample(
    string PackagePath,
    string FullInstance,
    string ObjectDefinitionTgi,
    string ObjectCatalogTgi,
    string? ObjectDefinitionInternalName,
    int ObjectDefinitionLength,
    int ObjectCatalogLength,
    ushort ObjectDefinitionVersion,
    uint ObjectDefinitionDeclaredSize,
    uint ObjectDefinitionInternalNameByteLength,
    IReadOnlyList<uint> ObjectCatalogFirstWords,
    IReadOnlyList<string> ObjectCatalogNonZeroWordOffsets,
    IReadOnlyList<string> ObjectCatalogTailQwordCandidates,
    IReadOnlyList<EmbeddedTypeHit> ObjectDefinitionEmbeddedTypeHits,
    IReadOnlyList<ObjectDefinitionReferenceCandidate> ObjectDefinitionReferenceCandidates,
    IReadOnlyList<string> ObjectDefinitionTailQwordCandidates,
    string ObjectDefinitionNote,
    string ObjectCatalogNote);

file sealed record EmbeddedTypeHit(
    string TypeName,
    string TypeIdHex,
    int Offset,
    string HexWindow);

file sealed record ObjectCatalogSurveyView(
    IReadOnlyList<uint> FirstWords,
    IReadOnlyList<string> NonZeroWordOffsets,
    IReadOnlyList<string> TailQwordCandidates,
    string Note);

file sealed record ObjectDefinitionSurveyView(
    ushort HeaderVersion,
    uint DeclaredSize,
    uint InternalNameByteLength,
    string? InternalName,
    IReadOnlyList<EmbeddedTypeHit> TypeHits,
    IReadOnlyList<ObjectDefinitionReferenceCandidate> ReferenceCandidates,
    IReadOnlyList<string> TailQwordCandidates,
    string Note);

file sealed record ObjectDefinitionReferenceCandidate(
    int Offset,
    uint Marker,
    string FullTgi,
    string TypeName,
    bool IsPackageLocalMatch,
    string? PackagePath);

file sealed record SurveyPackageRow(
    string PackagePath,
    string RelativePath,
    int ResourceCount,
    int ThreeDimensionalGroupCount,
    IReadOnlyDictionary<string, int> AssetCounts,
    IReadOnlyDictionary<string, int> ThreeDimensionalTypes,
    string? Error);

file sealed record SurveyCountRow(string Label, int Count);

file sealed record ObjectCatalogFieldSummaryReport(
    string SearchRoot,
    string SurveyPath,
    DateTimeOffset GeneratedUtc,
    int SampleCount,
    IReadOnlyList<ObjectCatalogFieldSummaryRow> Fields);

file sealed record ObjectCatalogFieldSummaryRow(
    string Offset,
    int SampleCount,
    int DistinctValueCount,
    IReadOnlyList<SurveyCountRow> TopValues,
    IReadOnlyList<string> Examples);

file sealed class ProfilingPackageScanner(string rootPath, int maxPackages, string packageOrder) : IPackageScanner
{
    public async IAsyncEnumerable<DiscoveredPackage> DiscoverPackagesAsync(IEnumerable<DataSourceDefinition> sources, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var source = sources.FirstOrDefault();
        if (source is null || !Directory.Exists(rootPath))
        {
            yield break;
        }

        var orderedPackages = Directory
            .EnumerateFiles(rootPath, "*.package", SearchOption.AllDirectories)
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new { Path = path, Info = info };
            })
            .Where(static item => item.Info.Exists)
            .OrderByDescending(item => string.Equals(packageOrder, "largest", StringComparison.OrdinalIgnoreCase) ? item.Info.Length : 0L)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, maxPackages))
            .ToArray();

        Console.WriteLine($"PROFILE selected {orderedPackages.Length} package(s) from {rootPath}");
        foreach (var package in orderedPackages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Console.WriteLine($"PROFILE package {package.Info.Length,12:N0} bytes  {package.Path}");
            yield return new DiscoveredPackage(source, package.Path, package.Info.Length, package.Info.LastWriteTimeUtc);
            await Task.Yield();
        }
    }
}

file sealed record SurveyAssetDisplaySample(
    string AssetKind,
    string PackagePath,
    string RootTgi,
    string DisplayName,
    string? Category);

file sealed record SurveyNameLookupSummary(
    string TypeName,
    int Sampled,
    int Named,
    int Empty,
    int Failed,
    IReadOnlyList<string> Examples,
    IReadOnlyList<string> Failures);

file sealed class SurveyNameProbeState
{
    public SurveyNameProbeState(string typeName)
    {
        TypeName = typeName;
    }

    public string TypeName { get; }

    public int Sampled { get; set; }

    public int Named { get; set; }

    public int Empty { get; set; }

    public int Failed { get; set; }

    public List<string> Examples { get; } = [];

    public List<string> Failures { get; } = [];

    public SurveyNameLookupSummary ToSummary() =>
        new(
            TypeName,
            Sampled,
            Named,
            Empty,
            Failed,
            Examples.ToArray(),
            Failures.ToArray());
}
