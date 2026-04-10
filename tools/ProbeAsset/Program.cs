using Sims4ResourceExplorer.Assets;
using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Indexing;
using Sims4ResourceExplorer.Packages;
using Sims4ResourceExplorer.Preview;
using System.Buffers.Binary;
using System.IO.Compression;

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
var rootRawBytes = await catalog.GetResourceBytesAsync(graph.BuildBuyGraph.ModelResource.PackagePath, graph.BuildBuyGraph.ModelResource.Key, raw: false, CancellationToken.None);
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
    var rawBytes = await catalog.GetResourceBytesAsync(lod.PackagePath, lod.Key, raw: false, CancellationToken.None);
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
        using var png = new GZipStream(source, CompressionMode.Decompress, leaveOpen: true);
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
    var bytes = await catalog.GetResourceBytesAsync(resource.PackagePath, resource.Key, raw: false, CancellationToken.None);
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

    offset += externalCount * 16;
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
