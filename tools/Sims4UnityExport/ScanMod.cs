// ScanMod — headless package SCANNER for dropped adult-female mods. Answers two questions:
//   Q1: is there a higher-poly adult-female BODY MESH that is rig/UV/slider-compatible with EA?
//   Q2: are there HD base/detail SKIN TEXTURES?
//
// It opens ONE package via the existing LlamaResourceCatalogService.ScanPackageAsync (the SAME
// path scancasmask/RunScanCasMaskAsync use, so EP/override quirks don't bite), enumerates ALL
// resources, and reports:
//   - CASP parts: internal name, BodyType, species/age/gender flags, swatch count, referenced
//     GEOM/texture TGIs (from the CASP's own LOD key-indices + texture slots).
//   - GEOM meshes: vertexCount, triangle count, bone-hash count, UV0 range (min/max u,v) +
//     whether a uv1 exists, plus a bone-hash sample + the EA-rig subset-match verdict.
//   - Textures: TGI TypeName (LRLEImage/RLE2Image/DSTImage/PNGImage/...) and DECODED w×h.
//
// EA baseline (loaded once, lazily, for the compatibility comparison): the canonical adult rig
// (auRig) bone-hash set, resolved by FNV-1 64-bit name hash exactly like the renderer's CAS path
// (BuildBuySceneBuildService.Cas.cs ComputeTs4Fnv64 + Rig type 0x8EAF13DE), parsed with the
// PUBLIC Ts4RigResource. EA's default adult-female body GEOM numbers (vertexCount + UV0 range)
// come from parsing the GEOMs of EA's default nude-female CASParts found in the index.
//
// The GEOM binary parser below is a faithful, self-contained re-implementation of the internal
// Ts4GeomResource.Parse (BuildBuySceneBuildService.Cas.cs) — that type is internal to the Preview
// assembly and not referenceable here, and the brief restricts edits to tools/. The byte layout,
// version gates, formats/stride table, submesh index reader, UV-stitch/seam/slot skips, and bone
// hash table read are mirrored 1:1 from that proven parser.

using System.Globalization;
using System.Text;
using Sims4ResourceExplorer.Assets;
using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Preview;

namespace Sims4UnityExport;

internal static class ModScanner
{
    private const uint RigType = 0x8EAF13DEu;

    // ---------------------------------------------------------------------------
    // scanmod <packagePath>
    // ---------------------------------------------------------------------------
    public static async Task<bool> RunAsync(
        IResourceCatalogService catalog,
        IIndexStore indexStore,
        ITextureDecodeService textureDecoder,
        string packagePath,
        CancellationToken ct)
    {
        if (!File.Exists(packagePath))
        {
            Console.Error.WriteLine($"Package not found: {packagePath}");
            return false;
        }

        Console.WriteLine("============================================================");
        Console.WriteLine($"scanmod: {packagePath}");
        Console.WriteLine($"file size: {new FileInfo(packagePath).Length:N0} bytes");
        Console.WriteLine("============================================================");

        // Resolve the EA adult-rig bone-hash baseline once (best-effort; null on miss).
        var eaRigHashes = await ResolveEaAdultRigHashesAsync(catalog, indexStore, packagePath, ct).ConfigureAwait(false);
        if (eaRigHashes is { Count: > 0 })
        {
            Console.WriteLine($"[baseline] EA adult rig (auRig) bone-hash set: {eaRigHashes.Count} bones (used for rig-subset verdicts).");
        }
        else
        {
            Console.WriteLine("[baseline] EA adult rig (auRig) could not be resolved; rig-subset verdict will be UNKNOWN.");
        }
        Console.WriteLine();

        var source = new DataSourceDefinition(Guid.NewGuid(), Path.GetFileName(packagePath), packagePath, SourceKind.Game);
        PackageScanResult scan;
        try
        {
            scan = await catalog.ScanPackageAsync(source, packagePath, progress: null, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ScanPackageAsync FAILED ({ex.GetType().Name}: {ex.Message}).");
            return false;
        }

        var resources = scan.Resources;
        Console.WriteLine($"Total resources: {resources.Count}");

        // Resource-type histogram so the package's nature is obvious at a glance.
        var byType = resources
            .GroupBy(r => r.Key.TypeName, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ToList();
        Console.WriteLine("Resource-type histogram:");
        foreach (var g in byType)
        {
            Console.WriteLine($"  {g.Key,-22} {g.Count(),5}");
        }
        Console.WriteLine();

        await ReportCaspsAsync(catalog, packagePath, resources, ct).ConfigureAwait(false);
        await ReportGeomsAsync(catalog, packagePath, resources, eaRigHashes, ct).ConfigureAwait(false);
        await ReportTexturesAsync(catalog, textureDecoder, packagePath, resources, ct).ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine($"scanmod DONE for {Path.GetFileName(packagePath)}.");
        return true;
    }

    // ---------------------------------------------------------------------------
    // scangeoms <dir> — content-based hunt for a HEAD mesh hidden under ANY asset type.
    //
    // The lesson from EVE (a full nude body shipped under a "bralette" clothing CASP): never trust
    // the CASP name/type. So this walks every .package in <dir>, parses EVERY GEOM, and classifies it
    // purely by geometry — position centroid + bounding box — flagging head-region meshes (high Y,
    // small bbox) no matter what CASP they hang off. A real face/head mesh is the goal; hair/hats sit
    // high too and are flagged as candidates for eyeballing.
    // ---------------------------------------------------------------------------
    public static async Task ScanGeomsInDirAsync(IResourceCatalogService catalog, string dir, CancellationToken ct)
    {
        if (!Directory.Exists(dir))
        {
            Console.Error.WriteLine($"Directory not found: {dir}");
            return;
        }

        var pkgs = Directory.GetFiles(dir, "*.package", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        Console.WriteLine("============================================================");
        Console.WriteLine($"scangeoms: {pkgs.Count} package(s) under {dir}");
        Console.WriteLine("Classifying EVERY GEOM by geometry (centroid + bbox), independent of CASP type.");
        Console.WriteLine("HEAD? = centroid Y >= 1.40 and 0.12 <= max-bbox-dim <= 0.6 (face/head sized, up high).");
        Console.WriteLine("============================================================");

        var headCandidates = new List<string>();
        var totalGeoms = 0;
        foreach (var pkg in pkgs)
        {
            var source = new DataSourceDefinition(Guid.NewGuid(), Path.GetFileName(pkg), pkg, SourceKind.Mods);
            PackageScanResult scan;
            try { scan = await catalog.ScanPackageAsync(source, pkg, progress: null, ct).ConfigureAwait(false); }
            catch (Exception ex) { Console.Error.WriteLine($"  [skip] {Path.GetFileName(pkg)}: {ex.GetType().Name}"); continue; }

            var geoms = scan.Resources
                .Where(r => string.Equals(r.Key.TypeName, "Geometry", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (geoms.Count == 0) continue;

            foreach (var g in geoms)
            {
                GeomInfo info;
                try
                {
                    var bytes = await catalog.GetResourceBytesAsync(pkg, g.Key, raw: false, ct).ConfigureAwait(false);
                    info = ParseGeom(bytes);
                }
                catch { continue; }
                totalGeoms++;

                var dx = info.MaxX - info.MinX;
                var dy = info.MaxY - info.MinY;
                var dz = info.MaxZ - info.MinZ;
                var maxDim = Math.Max(dx, Math.Max(dy, dz));

                string region;
                if (info.CenY >= 1.40f && maxDim >= 0.12f && maxDim <= 0.60f) region = "HEAD?";
                else if (dx >= 1.0f) region = "top(arms)";
                else if (info.CenY >= 0.95f) region = "top";
                else if (info.CenY <= 0.40f) region = "feet";
                else if (maxDim < 0.12f) region = "accessory/tiny";
                else region = "bottom/other";

                var pkgName = Path.GetFileName(pkg);
                var line = FormattableString.Invariant(
                    $"  {pkgName,-46} {g.Key.FullTgi}  v={info.VertexCount,5}  cen=({info.CenX:0.00},{info.CenY:0.00},{info.CenZ:0.00})  bbox=({dx:0.00},{dy:0.00},{dz:0.00})  -> {region}");
                Console.WriteLine(line);
                if (region == "HEAD?") headCandidates.Add(line);
            }
        }

        Console.WriteLine();
        Console.WriteLine($"scangeoms DONE: {totalGeoms} GEOM(s) across {pkgs.Count} package(s).");
        Console.WriteLine(headCandidates.Count == 0
            ? "VERDICT: NO head-region GEOM found in any package (no hidden head mesh)."
            : $"VERDICT: {headCandidates.Count} HEAD-candidate GEOM(s) — inspect:\n" + string.Join("\n", headCandidates));
    }

    // ---------------------------------------------------------------------------
    // CASP report
    // ---------------------------------------------------------------------------
    private static async Task ReportCaspsAsync(
        IResourceCatalogService catalog,
        string packagePath,
        IReadOnlyList<ResourceMetadata> resources,
        CancellationToken ct)
    {
        var casps = resources
            .Where(r => string.Equals(r.Key.TypeName, "CASPart", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Console.WriteLine("------------------------------------------------------------");
        Console.WriteLine($"CASP parts: {casps.Count}");
        Console.WriteLine("------------------------------------------------------------");

        var shown = 0;
        foreach (var resource in casps)
        {
            Ts4CasPart part;
            try
            {
                var bytes = await catalog.GetResourceBytesAsync(packagePath, resource.Key, raw: false, ct).ConfigureAwait(false);
                part = Ts4CasPart.Parse(bytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  CASP {resource.Key.FullTgi}: parse FAILED ({ex.GetType().Name}: {ex.Message}).");
                continue;
            }

            // GEOM TGIs referenced by this CASP's LODs (KeyIndices -> TgiList Geometry entries).
            var geomTgis = part.Lods
                .SelectMany(lod => lod.KeyIndices)
                .Where(i => i < part.TgiList.Count)
                .Select(i => part.TgiList[i])
                .Where(k => string.Equals(k.TypeName, "Geometry", StringComparison.OrdinalIgnoreCase))
                .Select(k => k.FullTgi)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var texRefs = part.TextureReferences
                .Select(t => $"{t.Slot}={t.Key.FullTgi}({t.Key.TypeName})")
                .ToList();

            Console.WriteLine($"  CASP {resource.Key.FullTgi}");
            Console.WriteLine($"    name='{part.InternalName ?? "(none)"}'  bodyType={part.BodyType}  species={part.SpeciesLabel ?? "?"}  age='{part.AgeLabel}'  gender='{part.GenderLabel}'");
            Console.WriteLine($"    swatches={part.SwatchColors.Count}  defaultForBodyType={part.DefaultForBodyType}  defFemale={part.DefaultForBodyTypeFemale}  defMale={part.DefaultForBodyTypeMale}  sortLayer={part.SortLayer}");
            Console.WriteLine($"    GEOM refs ({geomTgis.Count}): {(geomTgis.Count == 0 ? "(none)" : string.Join(", ", geomTgis))}");
            Console.WriteLine($"    texture refs ({texRefs.Count}): {(texRefs.Count == 0 ? "(none)" : string.Join(", ", texRefs))}");

            shown++;
            if (shown >= 40)
            {
                Console.WriteLine($"  ... ({casps.Count - shown} more CASP(s) not shown)");
                break;
            }
        }

        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------
    // GEOM report (the Q1 mesh evidence)
    // ---------------------------------------------------------------------------
    private static async Task ReportGeomsAsync(
        IResourceCatalogService catalog,
        string packagePath,
        IReadOnlyList<ResourceMetadata> resources,
        IReadOnlyCollection<uint>? eaRigHashes,
        CancellationToken ct)
    {
        var geoms = resources
            .Where(r => string.Equals(r.Key.TypeName, "Geometry", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Console.WriteLine("------------------------------------------------------------");
        Console.WriteLine($"GEOM meshes: {geoms.Count}");
        Console.WriteLine("------------------------------------------------------------");

        var eaSet = eaRigHashes is null ? null : new HashSet<uint>(eaRigHashes);

        foreach (var resource in geoms)
        {
            GeomInfo info;
            try
            {
                var bytes = await catalog.GetResourceBytesAsync(packagePath, resource.Key, raw: false, ct).ConfigureAwait(false);
                info = ParseGeom(bytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  GEOM {resource.Key.FullTgi}: parse FAILED ({ex.GetType().Name}: {ex.Message}).");
                continue;
            }

            var tris = info.IndexCount / 3;
            var uv0 = info.HasUv0
                ? FormattableString.Invariant($"u[{info.MinU:0.####}..{info.MaxU:0.####}] v[{info.MinV:0.####}..{info.MaxV:0.####}]")
                : "(no uv0)";

            // Rig-subset verdict: are ALL the geom's bone hashes present in EA's adult rig set?
            string rigVerdict;
            int matched;
            if (eaSet is null)
            {
                rigVerdict = "UNKNOWN (no EA rig baseline)";
                matched = 0;
            }
            else if (info.BoneHashes.Count == 0)
            {
                rigVerdict = "n/a (unrigged mesh — no bone hashes)";
                matched = 0;
            }
            else
            {
                matched = info.BoneHashes.Count(h => eaSet.Contains(h));
                var isSubset = matched == info.BoneHashes.Count;
                if (isSubset)
                {
                    rigVerdict = $"SUBSET ✓ ({matched}/{info.BoneHashes.Count} bones in EA rig — binds to EA skeleton)";
                }
                else
                {
                    var foreign = info.BoneHashes.Where(h => !eaSet.Contains(h)).Select(h => $"0x{h:X8}");
                    rigVerdict = $"NOT a subset ✗ ({matched}/{info.BoneHashes.Count} bones in EA rig — foreign: {string.Join(", ", foreign)})";
                }
            }

            var sampleHashes = info.BoneHashes.Take(6).Select(h => $"0x{h:X8}");
            var sampleStr = info.BoneHashes.Count == 0
                ? "(none)"
                : string.Join(", ", sampleHashes) + (info.BoneHashes.Count > 6 ? ", ..." : string.Empty);

            var dx = info.MaxX - info.MinX;
            var dy = info.MaxY - info.MinY;
            var dz = info.MaxZ - info.MinZ;
            var uvCellsTotal = info.UvGrid * info.UvGrid;
            var uvPct = uvCellsTotal > 0 ? 100.0 * info.UvCellsOccupied / uvCellsTotal : 0.0;

            Console.WriteLine($"  GEOM {resource.Key.FullTgi}  (version 0x{info.Version:X})");
            Console.WriteLine($"    vertices={info.VertexCount}  triangles={tris}  (indices={info.IndexCount}, submeshes={info.SubMeshCount})");
            Console.WriteLine(FormattableString.Invariant(
                $"    bbox size: dx={dx:0.###} dy={dy:0.###} dz={dz:0.###}  (X[{info.MinX:0.###}..{info.MaxX:0.###}] Y[{info.MinY:0.###}..{info.MaxY:0.###}] Z[{info.MinZ:0.###}..{info.MaxZ:0.###}])"));
            Console.WriteLine(FormattableString.Invariant(
                $"    centroid: ({info.CenX:0.###}, {info.CenY:0.###}, {info.CenZ:0.###})   UV0 occupancy: {info.UvCellsOccupied}/{uvCellsTotal} cells ({uvPct:0.#}% of {info.UvGrid}x{info.UvGrid} grid)"));
            Console.WriteLine($"    UV0: {uv0}   uv1 present={info.HasUv1}   skinned={info.HasSkinning}   morph/BGEO data (VertexId)={info.HasVertexId}   tag/DMap data (TagVal)={info.HasTagVal}");
            Console.WriteLine($"    bone hashes: count={info.BoneHashes.Count}  sample=[{sampleStr}]");
            Console.WriteLine($"    rig compat: {rigVerdict}");
        }

        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------
    // Texture report (the Q2 evidence)
    // ---------------------------------------------------------------------------
    private static async Task ReportTexturesAsync(
        IResourceCatalogService catalog,
        ITextureDecodeService textureDecoder,
        string packagePath,
        IReadOnlyList<ResourceMetadata> resources,
        CancellationToken ct)
    {
        var textures = resources
            .Where(r => IsTextureType(r.Key.TypeName))
            .ToList();

        Console.WriteLine("------------------------------------------------------------");
        Console.WriteLine($"Textures (image resources): {textures.Count}");
        Console.WriteLine("------------------------------------------------------------");

        // Group by decoded resolution for an at-a-glance HD summary.
        var resolutionHistogram = new Dictionary<string, int>(StringComparer.Ordinal);
        var rows = new List<(string Tgi, string TypeName, int W, int H, string Px, long Bytes)>();

        var shown = 0;
        foreach (var resource in textures)
        {
            int w = -1, h = -1;
            string px = "?";
            long rawBytes = 0;
            try
            {
                rawBytes = (await catalog.GetResourceBytesAsync(packagePath, resource.Key, raw: false, ct).ConfigureAwait(false)).LongLength;
            }
            catch { /* size best-effort */ }

            try
            {
                var decoded = await textureDecoder.DecodeAsync(resource, ct).ConfigureAwait(false);
                if (decoded.Success)
                {
                    w = decoded.Width ?? -1;
                    h = decoded.Height ?? -1;
                    px = decoded.PixelFormat ?? "?";
                }
                else
                {
                    px = "decode-failed";
                }
            }
            catch (Exception ex)
            {
                px = $"decode-error:{ex.GetType().Name}";
            }

            var resKey = (w > 0 && h > 0) ? $"{w}x{h}" : "unknown";
            resolutionHistogram[resKey] = resolutionHistogram.GetValueOrDefault(resKey) + 1;
            rows.Add((resource.Key.FullTgi, resource.Key.TypeName, w, h, px, rawBytes));
        }

        Console.WriteLine("Resolution histogram (decoded w×h):");
        foreach (var kv in resolutionHistogram.OrderByDescending(k => k.Value))
        {
            Console.WriteLine($"  {kv.Key,-12} {kv.Value,4}");
        }
        Console.WriteLine();

        // Largest-first so the HD candidates are at the top.
        foreach (var row in rows.OrderByDescending(r => (long)Math.Max(r.W, 0) * Math.Max(r.H, 0)))
        {
            var dim = (row.W > 0 && row.H > 0) ? $"{row.W}x{row.H}" : "unknown";
            Console.WriteLine($"  TEX {row.Tgi}  type={row.TypeName,-12} decoded={dim,-12} px={row.Px,-20} payload={row.Bytes:N0}B");
            shown++;
            if (shown >= 60)
            {
                Console.WriteLine($"  ... ({rows.Count - shown} more texture(s) not shown)");
                break;
            }
        }

        Console.WriteLine();
    }

    private static bool IsTextureType(string typeName) => typeName is
        "LRLEImage" or "RLE2Image" or "RLESImage" or "DSTImage" or
        "PNGImage" or "PNGImage2" or "DDS" or
        "CASPartThumbnail" or "BodyPartThumbnail" or "BuyBuildThumbnail";

    // ---------------------------------------------------------------------------
    // EA adult-rig (auRig) bone-hash baseline
    //
    // Resolve auRig by FNV-1 64-bit name hash (the renderer's canonical-rig resolution), look it
    // up via the index (GetResourcesByFullInstanceAsync) AND probe the install root's known rig
    // packages, parse with the public Ts4RigResource, and return the bone NameHash set (uint32).
    // Best-effort: returns null when nothing resolves.
    // ---------------------------------------------------------------------------
    private static async Task<IReadOnlyCollection<uint>?> ResolveEaAdultRigHashesAsync(
        IResourceCatalogService catalog,
        IIndexStore indexStore,
        string scannedPackagePath,
        CancellationToken ct)
    {
        try
        {
            await indexStore.InitializeAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[baseline] index init failed ({ex.GetType().Name}: {ex.Message}).");
        }

        var auHash = ComputeTs4Fnv64("auRig");
        var directKey = new ResourceKeyRecord(RigType, 0u, auHash, "Rig");

        // 1) Index lookup by full instance.
        var candidates = new List<ResourceMetadata>();
        try
        {
            candidates.AddRange((await indexStore.GetResourcesByFullInstanceAsync(auHash, ct).ConfigureAwait(false))
                .Where(r => r.Key.TypeName == "Rig"));
        }
        catch { /* fall through to direct probe */ }

        // 2) Direct probe of well-known install rig packages (mirrors the renderer's fallback).
        var installRoot = TryResolveGameInstallRoot(scannedPackagePath);
        var probePaths = new List<string>();
        if (installRoot is not null)
        {
            probePaths.Add(Path.Combine(installRoot, "Data", "Simulation", "SimulationFullBuild0.package"));
            probePaths.Add(Path.Combine(installRoot, "Data", "Simulation", "SimulationDeltaBuild0.package"));
            probePaths.Add(Path.Combine(installRoot, "Data", "Client", "ClientFullBuild0.package"));
            probePaths.Add(Path.Combine(installRoot, "Data", "Client", "ClientDeltaBuild0.package"));
        }

        foreach (var resource in candidates)
        {
            var hashes = await TryParseRigHashesAsync(catalog, resource.PackagePath, directKey, ct).ConfigureAwait(false);
            if (hashes is { Count: > 0 })
            {
                Console.WriteLine($"[baseline] auRig resolved via index: {resource.PackagePath}");
                return hashes;
            }
        }

        foreach (var probe in probePaths.Where(File.Exists))
        {
            var hashes = await TryParseRigHashesAsync(catalog, probe, directKey, ct).ConfigureAwait(false);
            if (hashes is { Count: > 0 })
            {
                Console.WriteLine($"[baseline] auRig resolved via install probe: {probe}");
                return hashes;
            }
        }

        return null;
    }

    private static async Task<IReadOnlyCollection<uint>?> TryParseRigHashesAsync(
        IResourceCatalogService catalog, string packagePath, ResourceKeyRecord key, CancellationToken ct)
    {
        try
        {
            var bytes = await catalog.GetResourceBytesAsync(packagePath, key, raw: false, ct).ConfigureAwait(false);
            if (bytes is not { Length: > 0 })
            {
                return null;
            }
            var rig = Ts4RigResource.Parse(bytes);
            return rig.Bones.Select(b => b.NameHash).ToHashSet();
        }
        catch
        {
            return null;
        }
    }

    // Walk up from a package path looking for an "Electronic Arts/The Sims 4" install root that
    // contains a Data/ folder (where the rig packages live). Best-effort.
    private static string? TryResolveGameInstallRoot(string anyPackagePath)
    {
        try
        {
            var dir = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(anyPackagePath)) ?? string.Empty);
            for (var i = 0; i < 12 && dir is not null; i++, dir = dir.Parent)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Data", "Simulation")) ||
                    Directory.Exists(Path.Combine(dir.FullName, "Data", "Client")))
                {
                    return dir.FullName;
                }
            }
        }
        catch { /* ignore */ }

        // Fallback: the common Windows install path.
        foreach (var guess in new[]
        {
            @"C:\Program Files (x86)\Origin Games\The Sims 4",
            @"C:\Program Files\EA Games\The Sims 4",
            @"C:\Program Files (x86)\Steam\steamapps\common\The Sims 4",
            @"C:\Program Files\The Sims 4",
        })
        {
            if (Directory.Exists(Path.Combine(guess, "Data", "Simulation")) ||
                Directory.Exists(Path.Combine(guess, "Data", "Client")))
            {
                return guess;
            }
        }

        return null;
    }

    // FNV-1 (NOT 1a) 64-bit over the lowercased ASCII name — identical to the renderer's
    // ComputeTs4Fnv64 (BuildBuySceneBuildService.Cas.cs).
    private static ulong ComputeTs4Fnv64(string name)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offsetBasis;
        foreach (var b in Encoding.ASCII.GetBytes(name.ToLowerInvariant()))
        {
            unchecked { hash *= prime; }
            hash ^= b;
        }
        return hash;
    }

    // ===========================================================================
    // Self-contained GEOM header parser (mirrors Ts4GeomResource.Parse, internal to Preview).
    // Reads only what the scan needs: vertex count, UV0 min/max, uv1 presence, index/triangle
    // count, bone-hash table, and morph-data presence (VertexId/TagVal usages). Heavy per-vertex
    // attribute arrays are streamed past, not retained.
    // ===========================================================================
    private readonly record struct GeomInfo(
        uint Version,
        int VertexCount,
        int IndexCount,
        int SubMeshCount,
        bool HasUv0,
        bool HasUv1,
        float MinU, float MaxU, float MinV, float MaxV,
        bool HasSkinning,
        bool HasVertexId,
        bool HasTagVal,
        IReadOnlyList<uint> BoneHashes,
        // Actual mesh composition (decisive for body-vs-clothing): the position bounding box +
        // centroid in mesh-local space, and a UV0 occupancy fraction over a fixed grid.
        float MinX, float MinY, float MinZ,
        float MaxX, float MaxY, float MaxZ,
        float CenX, float CenY, float CenZ,
        int UvGrid, int UvCellsOccupied);

    private static GeomInfo ParseGeom(byte[] bytes)
    {
        var payload = ResolveGeomPayload(bytes);
        using var stream = new MemoryStream(payload, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        ExpectTag(reader, "GEOM");
        var version = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        var shader = reader.ReadUInt32();
        if (shader != 0)
        {
            var shaderSize = reader.ReadUInt32();
            SkipBytes(reader, shaderSize, "shader payload");
        }

        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        var vertexCount = ReadNonNegativeInt32(reader, "vertex count");
        var formats = ReadFormats(reader);

        // Per-vertex read: accumulate UV0 min/max + detect uv1/skinning/morph usages.
        var hasUv0 = false;
        var hasUv1 = false;
        var hasSkinning = false;
        var hasVertexId = false;
        var hasTagVal = false;
        float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;

        // Position bbox + centroid accumulation, and a UV0 occupancy grid (decisive evidence for
        // whether a GEOM is a full body or a small clothing shell).
        float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
        double sumX = 0, sumY = 0, sumZ = 0;
        const int uvGrid = 32;
        var uvCells = new HashSet<int>();

        var stride = GetVertexStride(version, formats);
        EnsureBytesAvailable(reader, checked((long)vertexCount * stride), "vertex buffer");
        for (var v = 0; v < vertexCount; v++)
        {
            var uvSeenThisVertex = 0;
            foreach (var usage in formats)
            {
                switch (usage)
                {
                    case 0x01: // position (Float3) — capture bbox + centroid
                        var x = reader.ReadSingle();
                        var y = reader.ReadSingle();
                        var z = reader.ReadSingle();
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                        if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
                        sumX += x; sumY += y; sumZ += z;
                        break;
                    case 0x02: // normal
                    case 0x06: // tangent
                        reader.BaseStream.Position += 12;
                        break;
                    case 0x03: // uv (first = uv0, second = uv1)
                        var u = reader.ReadSingle();
                        var vv = reader.ReadSingle();
                        if (uvSeenThisVertex == 0)
                        {
                            hasUv0 = true;
                            if (u < minU) minU = u;
                            if (u > maxU) maxU = u;
                            if (vv < minV) minV = vv;
                            if (vv > maxV) maxV = vv;
                            // Mark the UV cell this vertex falls in (clamped to [0,1) grid).
                            var cu = (int)Math.Clamp(u * uvGrid, 0, uvGrid - 1);
                            var cv = (int)Math.Clamp(vv * uvGrid, 0, uvGrid - 1);
                            uvCells.Add(cv * uvGrid + cu);
                        }
                        else
                        {
                            hasUv1 = true;
                        }
                        uvSeenThisVertex++;
                        break;
                    case 0x04: // blend indices
                        reader.BaseStream.Position += 4;
                        hasSkinning = true;
                        break;
                    case 0x05 when version == 0x00000005: // blend weights (float4)
                        reader.BaseStream.Position += 16;
                        hasSkinning = true;
                        break;
                    case 0x05: // blend weights (byte4)
                        reader.BaseStream.Position += 4;
                        hasSkinning = true;
                        break;
                    case 0x07: // TagVal (DMap/BGEO tag bits)
                        reader.BaseStream.Position += 4;
                        hasTagVal = true;
                        break;
                    case 0x0A: // VertexID (BGEO morph indexing)
                        reader.BaseStream.Position += 4;
                        hasVertexId = true;
                        break;
                    default:
                        throw new InvalidDataException($"Unsupported GEOM usage 0x{usage:X8}.");
                }
            }
        }

        // Submesh index buffers.
        var subMeshCount = ReadNonNegativeInt32(reader, "submesh count");
        var indexCount = 0;
        for (var s = 0; s < subMeshCount; s++)
        {
            EnsureBytesAvailable(reader, sizeof(byte) + sizeof(int), $"submesh header #{s}");
            var indexSize = reader.ReadByte();
            var count = ReadNonNegativeInt32(reader, $"submesh index count #{s}");
            var byteCount = indexSize switch
            {
                2 => checked((long)count * sizeof(ushort)),
                4 => checked((long)count * sizeof(uint)),
                _ => throw new InvalidDataException($"GEOM index size {indexSize} is invalid.")
            };
            SkipBytes(reader, byteCount, $"submesh index buffer #{s}");
            indexCount += count;
        }

        // UV stitch / seam / slot-intersection skips (version-gated, mirrors the parser).
        if (version == 0x00000005)
        {
            _ = reader.ReadInt32();
        }
        else if (version >= 0x0000000C)
        {
            SkipUvStitchData(reader);
            if (version >= 0x0000000D)
            {
                SkipSeamStitchData(reader);
            }
            SkipSlotIntersectionData(reader, version);
        }

        // Bone-hash table.
        var boneHashCount = ReadNonNegativeInt32(reader, "bone hash count");
        EnsureBytesAvailable(reader, checked((long)boneHashCount * sizeof(uint)), "bone hash table");
        var boneHashes = new List<uint>(boneHashCount);
        for (var i = 0; i < boneHashCount; i++)
        {
            boneHashes.Add(reader.ReadUInt32());
        }

        var hasPos = vertexCount > 0 && minX != float.MaxValue;
        var cen = vertexCount > 0
            ? ((float)(sumX / vertexCount), (float)(sumY / vertexCount), (float)(sumZ / vertexCount))
            : (0f, 0f, 0f);

        return new GeomInfo(
            version,
            vertexCount,
            indexCount,
            subMeshCount,
            hasUv0,
            hasUv1,
            hasUv0 ? minU : 0f, hasUv0 ? maxU : 0f, hasUv0 ? minV : 0f, hasUv0 ? maxV : 0f,
            hasSkinning,
            hasVertexId,
            hasTagVal,
            boneHashes,
            hasPos ? minX : 0f, hasPos ? minY : 0f, hasPos ? minZ : 0f,
            hasPos ? maxX : 0f, hasPos ? maxY : 0f, hasPos ? maxZ : 0f,
            cen.Item1, cen.Item2, cen.Item3,
            uvGrid, uvCells.Count);
    }

    private static byte[] ResolveGeomPayload(byte[] bytes)
    {
        if (bytes.Length >= 4 && bytes[0] == (byte)'G' && bytes[1] == (byte)'E' && bytes[2] == (byte)'O' && bytes[3] == (byte)'M')
        {
            return bytes;
        }

        // RCOL-wrapped (or otherwise prefixed) GEOM: scan for the "GEOM" magic and parse from there.
        // The internal parser uses Ts4RcolResource; scanning for the tag is a dependency-free
        // equivalent that works for both clear RCOL and raw payloads.
        for (var i = 0; i + 4 <= bytes.Length; i++)
        {
            if (bytes[i] == (byte)'G' && bytes[i + 1] == (byte)'E' && bytes[i + 2] == (byte)'O' && bytes[i + 3] == (byte)'M')
            {
                var slice = new byte[bytes.Length - i];
                Array.Copy(bytes, i, slice, 0, slice.Length);
                return slice;
            }
        }

        return bytes;
    }

    private static IReadOnlyList<uint> ReadFormats(BinaryReader reader)
    {
        var count = ReadNonNegativeInt32(reader, "format count");
        EnsureBytesAvailable(reader, checked((long)count * 9), "format table");
        var results = new List<uint>(count);
        for (var i = 0; i < count; i++)
        {
            var usage = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            _ = reader.ReadByte();
            results.Add(usage);
        }
        return results;
    }

    private static int GetVertexStride(uint version, IReadOnlyList<uint> formats)
    {
        var stride = 0;
        foreach (var usage in formats)
        {
            stride += usage switch
            {
                0x01 or 0x02 or 0x06 => 12,
                0x03 => 8,
                0x04 => 4,
                0x05 when version == 0x00000005 => 16,
                0x05 => 4,
                0x07 or 0x0A => 4,
                _ => throw new InvalidDataException($"Unsupported GEOM usage 0x{usage:X8}.")
            };
        }
        return stride;
    }

    private static void SkipUvStitchData(BinaryReader reader)
    {
        var stitchCount = ReadNonNegativeInt32(reader, "uv stitch count");
        for (var i = 0; i < stitchCount; i++)
        {
            EnsureBytesAvailable(reader, sizeof(int) + sizeof(int), $"uv stitch header #{i}");
            _ = reader.ReadInt32(); // vertex index
            var coordCount = ReadNonNegativeInt32(reader, $"uv stitch coordinate count #{i}");
            SkipBytes(reader, checked((long)coordCount * 8), $"uv stitch payload #{i}");
        }
    }

    private static void SkipSeamStitchData(BinaryReader reader)
    {
        var seamCount = ReadNonNegativeInt32(reader, "seam stitch count");
        SkipBytes(reader, checked((long)seamCount * 6), "seam stitch payload");
    }

    private static void SkipSlotIntersectionData(BinaryReader reader, uint version)
    {
        var slotCount = ReadNonNegativeInt32(reader, "slot intersection count");
        var size = version >= 0x0000000E ? 66 : 63;
        SkipBytes(reader, checked((long)slotCount * size), "slot intersection payload");
    }

    private static int ReadNonNegativeInt32(BinaryReader reader, string label)
    {
        var value = reader.ReadInt32();
        if (value < 0)
        {
            throw new InvalidDataException($"GEOM {label} {value} is invalid.");
        }
        return value;
    }

    private static void EnsureBytesAvailable(BinaryReader reader, long byteCount, string label)
    {
        if (byteCount < 0)
        {
            throw new InvalidDataException($"GEOM {label} length {byteCount} is invalid.");
        }
        var remaining = reader.BaseStream.Length - reader.BaseStream.Position;
        if (byteCount > remaining)
        {
            throw new InvalidDataException($"GEOM {label} extends beyond the payload.");
        }
    }

    private static void SkipBytes(BinaryReader reader, long byteCount, string label)
    {
        EnsureBytesAvailable(reader, byteCount, label);
        reader.BaseStream.Position += byteCount;
    }

    private static void ExpectTag(BinaryReader reader, string expected)
    {
        EnsureBytesAvailable(reader, expected.Length, "tag");
        var tag = Encoding.ASCII.GetString(reader.ReadBytes(expected.Length));
        if (!string.Equals(tag, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Expected tag '{expected}', found '{tag}'.");
        }
    }
}
