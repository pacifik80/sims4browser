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

if (args.Length > 0 && string.Equals(args[0], "--decode-png", StringComparison.OrdinalIgnoreCase))
{
    var packageFile = args.Length > 1 ? args[1] : string.Empty;
    var tgi = args.Length > 2 ? args[2] : string.Empty;
    if (!File.Exists(packageFile))
    {
        Console.Error.WriteLine($"Package not found: {packageFile}");
        return 1;
    }
    var parts = tgi.Split(':');
    if (parts.Length != 3 ||
        !uint.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var typeId) ||
        !uint.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var groupId) ||
        !ulong.TryParse(parts[2], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var instanceId))
    {
        Console.Error.WriteLine("Usage: --decode-png <packagePath> <Type:Group:Instance>");
        return 2;
    }
    var dpngCatalog = new LlamaResourceCatalogService();
    var dpngKey = new ResourceKeyRecord(typeId, groupId, instanceId, typeId == 0x2BC04EDFu ? "LRLEImage" : typeId == 0x3453CF95u ? "RLE2Image" : "_IMG");
    var pngBytes = await dpngCatalog.GetTexturePngAsync(packageFile, dpngKey, CancellationToken.None);
    if (pngBytes is null)
    {
        Console.WriteLine("Decoder returned null.");
        return 3;
    }
    Console.WriteLine($"Decoded PNG: {pngBytes.Length:N0} bytes; first8=0x{Convert.ToHexString(pngBytes[..8])}.");
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--find-instance", StringComparison.OrdinalIgnoreCase))
{
    var searchRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    var instanceHex = args.Length > 2 ? args[2] : string.Empty;
    if (!ulong.TryParse(instanceHex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var instanceValue))
    {
        Console.Error.WriteLine("Usage: --find-instance <searchRoot> <instanceHex16>");
        return 2;
    }
    if (!Directory.Exists(searchRoot))
    {
        Console.Error.WriteLine($"Directory not found: {searchRoot}");
        return 1;
    }
    var packagesToScan = Directory.EnumerateFiles(searchRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    var instanceCatalog = new LlamaResourceCatalogService();
    var instanceSource = new DataSourceDefinition(
        Guid.Parse("44444444-4444-4444-4444-444444444444"),
        "ProbeFindInstance",
        searchRoot,
        SourceKind.Game);
    var matches = 0;
    foreach (var pkg in packagesToScan)
    {
        try
        {
            var instanceScan = await instanceCatalog.ScanPackageAsync(instanceSource, pkg, progress: null, CancellationToken.None);
            foreach (var resource in instanceScan.Resources.Where(r => r.Key.FullInstance == instanceValue))
            {
                Console.WriteLine($"MATCH {resource.Key.FullTgi} type={resource.Key.TypeName} pkg={pkg}");
                matches++;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to scan {pkg}: {ex.Message}");
        }
    }
    Console.WriteLine($"Total matches: {matches}");
    return matches > 0 ? 0 : 3;
}

if (args.Length > 0 && string.Equals(args[0], "--inspect-resource", StringComparison.OrdinalIgnoreCase))
{
    var packagePathToInspect = args.Length > 1 ? args[1] : string.Empty;
    var resourceTgiToInspect = args.Length > 2 ? args[2] : string.Empty;
    return await InspectResourceAsync(packagePathToInspect, resourceTgiToInspect);
}

if (args.Length > 0 && string.Equals(args[0], "--scene-resource", StringComparison.OrdinalIgnoreCase))
{
    var scenePackagePath = args.Length > 1 ? args[1] : string.Empty;
    var sceneResourceTgi = args.Length > 2 ? args[2] : string.Empty;
    var useLiveIndex = args.Any(static arg => string.Equals(arg, "--live-index", StringComparison.OrdinalIgnoreCase));
    return await RunSceneResourceAsync(scenePackagePath, sceneResourceTgi, useLiveIndex);
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

if (args.Length > 0 && string.Equals(args[0], "--rebuild-live-sim-archetypes", StringComparison.OrdinalIgnoreCase))
{
    var workerCount = args.Length > 1 && int.TryParse(args[1], out var parsedWorkerCount) ? parsedWorkerCount : (int?)null;
    return await RebuildLiveCacheAndRunSimArchetypeSurveyAsync(workerCount);
}

if (args.Length > 0 && string.Equals(args[0], "--survey-sim-archetypes", StringComparison.OrdinalIgnoreCase))
{
    var outputPath = args.Length > 1 ? args[1] : Path.Combine(Environment.CurrentDirectory, "tmp", "sim_archetype_body_shell_audit.json");
    var maxEntries = args.Length > 2 && int.TryParse(args[2], out var parsedMaxEntries) ? parsedMaxEntries : 0;
    return await RunSimArchetypeBodyShellSurveyAsync(outputPath, maxEntries);
}if (args.Length > 0 && string.Equals(args[0], "--query-indexed-categories", StringComparison.OrdinalIgnoreCase))
{
    // Dumps the Category histogram of the indexed AssetSummary corpus from the existing
    // probe-cache-indexed-bodies SQLite store. Tells us what slotCategory values to pass
    // to the production query path.
    var qicCacheDir = Path.Combine(AppContext.BaseDirectory, "probe-cache-indexed-bodies");
    if (!Directory.Exists(qicCacheDir))
    {
        Console.Error.WriteLine($"Cache not found: {qicCacheDir}.");
        return 1;
    }
    var qicCache = new ProbeCacheService(qicCacheDir);
    qicCache.EnsureCreated();
    var qicStore = new SqliteIndexStore(qicCache);
    await qicStore.InitializeAsync(CancellationToken.None);
    Console.WriteLine("query-indexed-categories: querying all CAS assets...");

    // No slot filter — get everything.
    var query = new AssetBrowserQuery(
        new SourceScope(),
        string.Empty, AssetBrowserDomain.Cas, string.Empty, string.Empty, string.Empty,
        false, false, AssetBrowserSort.Name, 0, 100000);
    var result = await qicStore.QueryAssetsAsync(query, CancellationToken.None);
    Console.WriteLine($"  Total CAS assets: {result.TotalCount:N0}");
    Console.WriteLine($"  Returned: {result.Items.Count}");

    var byCategory = new Dictionary<string, int>(StringComparer.Ordinal);
    var byCategoryNorm = new Dictionary<string, int>(StringComparer.Ordinal);
    foreach (var a in result.Items)
    {
        var c = a.Category ?? "<null>";
        byCategory[c] = byCategory.GetValueOrDefault(c) + 1;
        var cn = a.CategoryNormalized ?? "<null>";
        byCategoryNorm[cn] = byCategoryNorm.GetValueOrDefault(cn) + 1;
    }
    Console.WriteLine("\n  Category histogram (top 30):");
    foreach (var kv in byCategory.OrderByDescending(static k => k.Value).Take(30))
        Console.WriteLine($"    {kv.Value,8:N0}  '{kv.Key}'");
    Console.WriteLine("\n  CategoryNormalized histogram (top 30):");
    foreach (var kv in byCategoryNorm.OrderByDescending(static k => k.Value).Take(30))
        Console.WriteLine($"    {kv.Value,8:N0}  '{kv.Key}'");
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-face-cas-types", StringComparison.OrdinalIgnoreCase))
{
    // Counts CAS parts in the index by body_type, focused on face-related slots.
    // TS4 BodyType values per community docs:
    //   2=Hair, 3=Head/Face mesh, 4=eyeColor, 13=Glasses, 14=Brows, 15=Eyeliner,
    //   16=Lipstick, 17=Mascara, 18=Blush, 19=skinDetails, 20=Eyeshadow, etc.
    var dbPath = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
    conn.Open();

    using (var c = conn.CreateCommand())
    {
        c.CommandText = """
            SELECT body_type, slot_category, COUNT(*),
                   SUM(CASE WHEN lower(species_label) = 'human' THEN 1 ELSE 0 END) AS human_count,
                   SUM(CASE WHEN lower(species_label) = 'human' AND lower(age_label) LIKE '%young adult%'
                                 AND lower(gender_label) LIKE '%female%' THEN 1 ELSE 0 END) AS yaf_count
            FROM cas_part_facts
            GROUP BY body_type, slot_category
            ORDER BY 3 DESC
            LIMIT 40
            """;
        Console.WriteLine("body_type × slot_category distribution (top 40, with Human + YA Female counts):");
        using var r = c.ExecuteReader();
        while (r.Read())
        {
            var bt = r.GetInt32(0);
            var slot = r.IsDBNull(1) ? "<null>" : r.GetString(1);
            var total = r.GetInt32(2);
            var human = r.GetInt32(3);
            var yaf = r.GetInt32(4);
            Console.WriteLine($"  bt={bt,4}  slot='{slot,-25}' total={total,7:N0} human={human,6:N0} ya-female={yaf,5:N0}");
        }
    }

    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-sim-outfit-parts", StringComparison.OrdinalIgnoreCase))
{
    // For a representative Human YA Female SimInfo, dump every body part referenced in
    // sim_template_body_parts. Lets us see what BodyType slots (eyes, brows, lips, etc.)
    // the Sim's outfit already references and whether those CASParts are in the index.
    var dbPath = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
    conn.Open();

    // First, see what columns sim_template_body_parts has.
    using (var c = conn.CreateCommand())
    {
        c.CommandText = "PRAGMA table_info(sim_template_body_parts)";
        using var r = c.ExecuteReader();
        Console.WriteLine("sim_template_body_parts columns:");
        while (r.Read())
            Console.WriteLine($"  {r.GetInt32(0),3}  {r.GetString(1),-30}  {r.GetString(2)}");
    }

    // Histogram of body_type across all sim_template_body_parts, for context.
    Console.WriteLine("\nbody_type distribution in sim_template_body_parts (top 30):");
    using (var c = conn.CreateCommand())
    {
        c.CommandText = "SELECT body_type, COUNT(*) FROM sim_template_body_parts GROUP BY body_type ORDER BY 2 DESC LIMIT 30";
        using var r = c.ExecuteReader();
        while (r.Read())
            Console.WriteLine($"  body_type={r.GetInt32(0),5:N0}  count={r.GetInt32(1):N0}");
    }

    // Pick a test SimInfo and dump its outfit parts.
    // If --probe-sim-outfit-parts <tgi> was provided, use it; else pick a SimInfo
    // that ACTUALLY has body-part rows in the index (most don't).
    string? testTgi = args.Length > 1 ? args[1] : null;
    if (testTgi is null)
    {
        using var c = conn.CreateCommand();
        c.CommandText = "SELECT root_tgi FROM sim_template_body_parts GROUP BY root_tgi LIMIT 1";
        using var r = c.ExecuteReader();
        if (r.Read()) testTgi = r.GetString(0);
    }
    if (testTgi is null) { Console.WriteLine("\nNo SimInfo with indexed body parts found."); return 0; }
    Console.WriteLine($"\nTest SimInfo: {testTgi}");

    // Show distinct root_tgi values that exist in sim_template_body_parts.
    Console.WriteLine("\n  Distinct root_tgi count in sim_template_body_parts:");
    using (var c = conn.CreateCommand())
    {
        c.CommandText = "SELECT COUNT(DISTINCT root_tgi) FROM sim_template_body_parts";
        using var r = c.ExecuteReader();
        if (r.Read()) Console.WriteLine($"    {r.GetInt32(0)} distinct root_tgi values");
    }
    using (var c = conn.CreateCommand())
    {
        c.CommandText = "SELECT root_tgi, COUNT(*) FROM sim_template_body_parts GROUP BY root_tgi LIMIT 5";
        using var r = c.ExecuteReader();
        while (r.Read())
            Console.WriteLine($"    {r.GetString(0)}  count={r.GetInt32(1)}");
    }

    using (var c = conn.CreateCommand())
    {
        c.CommandText = """
            SELECT body_type, body_type_label, part_instance_hex, is_body_driving, outfit_category_label, outfit_index, part_index
            FROM sim_template_body_parts
            WHERE root_tgi = $tgi
            ORDER BY outfit_index, body_type
            """;
        c.Parameters.AddWithValue("$tgi", testTgi);
        using var r = c.ExecuteReader();
        Console.WriteLine($"\n  Body parts referenced by this SimInfo:");
        while (r.Read())
        {
            var bodyType = r.GetInt32(0);
            var label = r.IsDBNull(1) ? "?" : r.GetString(1);
            var partHex = r.IsDBNull(2) ? "?" : r.GetString(2);
            var driving = r.GetInt32(3);
            var outfitLabel = r.IsDBNull(4) ? "?" : r.GetString(4);
            var outfitIdx = r.GetInt32(5);
            var partIdx = r.GetInt32(6);
            Console.WriteLine($"    bodyType={bodyType,3} ({label,-12}) partInstance={partHex} bodyDriving={driving} outfit={outfitLabel}#{outfitIdx} partIdx={partIdx}");
        }
    }

    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--compose-skin-atlas", StringComparison.OrdinalIgnoreCase))
{
    // Replicates SimSkinAtlasComposer.BuildAsync math (Pass 1 soft-light + ×1.2 brighten,
    // Pass 2 overlay-blend, contrast around 0.75) using System.Drawing on the dumped
    // input PNGs from tmp/skin-dump/. Outputs the composed atlas + per-pass intermediates
    // for autonomous visual inspection. Mirrors SimSkinAtlasComposer:55-160.
    //
    // Usage: --compose-skin-atlas [<inputDir>] [<pass2Opacity>] [<skintoneHue>] [<skintoneSaturation>] [<bundledMouthPng>]
    var inputDir = args.Length > 1 ? args[1] : Path.Combine(Environment.CurrentDirectory, "tmp", "skin-dump");
    var pass2Op  = args.Length > 2 && float.TryParse(args[2], System.Globalization.CultureInfo.InvariantCulture, out var p2) ? p2 : 0.0f;
    var hue      = args.Length > 3 && ushort.TryParse(args[3], out var hv) ? hv : (ushort)10;
    var sat      = args.Length > 4 && ushort.TryParse(args[4], out var sv) ? sv : (ushort)15;
    var mouthPng = args.Length > 5 ? args[5] : @"C:\Users\stani\PROJECTS\Sims4Browser\src\Sims4ResourceExplorer.App\Assets\HeadMouthColor.png";
    if (!Directory.Exists(inputDir)) { Console.Error.WriteLine($"Input dir not found: {inputDir}"); return 1; }
    Console.WriteLine($"compose-skin-atlas: inputDir={inputDir}");
    Console.WriteLine($"  pass2Opacity={pass2Op}  hue={hue}  saturation={sat}  mouth={Path.GetFileName(mouthPng)}");

    string? Find(string prefix) => Directory.EnumerateFiles(inputDir, $"{prefix}*.png").FirstOrDefault();
    var basePath    = Find("01_base_skin");
    var detNeutral  = Find("02_detail_neutral");
    var detOverlay  = Find("03_detail_overlay");
    var faceOverlay = Find("04_face_overlay");
    if (basePath is null) { Console.Error.WriteLine("missing 01_base_skin*.png"); return 1; }
    Console.WriteLine($"  base:           {Path.GetFileName(basePath)}");
    Console.WriteLine($"  detail neutral: {Path.GetFileName(detNeutral) ?? "<none>"}");
    Console.WriteLine($"  detail overlay: {Path.GetFileName(detOverlay) ?? "<none>"}");
    Console.WriteLine($"  face overlay:   {Path.GetFileName(faceOverlay) ?? "<none>"}");

    static (int W, int H, byte[] BGRA) LoadBgra(string path)
    {
        using var bmp = new System.Drawing.Bitmap(path);
        var w = bmp.Width; var h = bmp.Height;
        var pixels = new byte[w * h * 4];
        var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, w, h),
            System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try { System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length); }
        finally { bmp.UnlockBits(data); }
        return (w, h, pixels);
    }
    static (int W, int H, byte[] BGRA) LoadBgraResized(string path, int targetW, int targetH)
    {
        using var src = new System.Drawing.Bitmap(path);
        if (src.Width == targetW && src.Height == targetH) return LoadBgra(path);
        using var resized = new System.Drawing.Bitmap(targetW, targetH);
        using (var g = System.Drawing.Graphics.FromImage(resized))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, 0, 0, targetW, targetH);
        }
        var pixels = new byte[targetW * targetH * 4];
        var data = resized.LockBits(new System.Drawing.Rectangle(0, 0, targetW, targetH),
            System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try { System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length); }
        finally { resized.UnlockBits(data); }
        return (targetW, targetH, pixels);
    }
    static void SavePng(string path, int w, int h, byte[] bgra)
    {
        using var bmp = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, w, h),
            System.Drawing.Imaging.ImageLockMode.WriteOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try { System.Runtime.InteropServices.Marshal.Copy(bgra, 0, data.Scan0, bgra.Length); }
        finally { bmp.UnlockBits(data); }
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
    static void BlendStraightAlphaOver(byte[] dst, byte[] src)
    {
        for (var i = 0; i < dst.Length; i += 4)
        {
            var sa = src[i + 3] / 255f;
            if (sa <= 0f) continue;
            for (var c = 0; c < 3; c++)
            {
                var b = src[i + c] * sa + dst[i + c] * (1f - sa);
                if (b < 0) b = 0; else if (b > 255) b = 255;
                dst[i + c] = (byte)b;
            }
        }
    }

    var skin = LoadBgra(basePath);
    var w = skin.W; var h = skin.H;
    Console.WriteLine($"  canvas: {w}×{h}");

    // 1. Build details = neutral OVER overlay
    byte[]? details = null;
    if (detNeutral is not null) details = LoadBgraResized(detNeutral, w, h).BGRA;
    if (detOverlay is not null)
    {
        var ov = LoadBgraResized(detOverlay, w, h).BGRA;
        if (details is null) details = ov;
        else BlendStraightAlphaOver(details, ov);
    }

    var skinPixels = skin.BGRA;
    SavePng(Path.Combine(inputDir, "step_00_base_only.png"), w, h, (byte[])skinPixels.Clone());
    if (details is not null)
        SavePng(Path.Combine(inputDir, "step_01_details_composited.png"), w, h, (byte[])details.Clone());

    // 2. Pass 1 + Pass 2 + Pass 3 + contrast (faithful to SimSkinAtlasComposer:96-148)
    if (details is not null && details.Length == skinPixels.Length)
    {
        var pass2 = Math.Clamp(pass2Op, 0f, 1f);
        const float contrast = 1.1f;
        const float midpoint = 0.75f;

        // Pass 3 hue conversion (HSL midpoint to RGB) — mirror SimSkinAtlasComposer:191-233.
        float HslChannel(float v, float a1, float a2)
        {
            if (v < 0) v += 1; else if (v > 1) v -= 1;
            float ch;
            if (6f * v < 1f) ch = a2 + (a1 - a2) * 6f * v;
            else if (2f * v < 1f) ch = a1;
            else if (3f * v < 2f) ch = a2 + (a1 - a2) * (0.666f - v) * 6f;
            else ch = a2;
            ch *= 255f;
            if (ch < 0) ch = 0; else if (ch > 255) ch = 255;
            return ch + 0.5f;
        }
        const ushort hSat = 127, hLum = 127;
        var l = hLum / 240f; if (l > 1) l = 1;
        byte[] rgbOver;
        if (hSat == 0) { var g = (byte)(255f * l); rgbOver = new[] { g, g, g }; }
        else
        {
            var s = hSat / 240f;
            var t1 = l < 0.5f ? l * (1f + s) : (l + s) - (l * s);
            var t2 = 2f * l - t1;
            var hN = hue / 239f;
            rgbOver = new[] { (byte)HslChannel(hN + 0.333f, t1, t2), (byte)HslChannel(hN, t1, t2), (byte)HslChannel(hN - 0.333f, t1, t2) };
        }
        var overFactor = (float)(sat / 100); // INTEGER division — faithful to SkinBlender
        var pass3Active = sat > 0 && overFactor > 0f;

        for (var i = 0; i < skinPixels.Length; i += 4)
        {
            for (var c = 0; c < 3; c++)
            {
                var color = skinPixels[i + c];
                var detail = details[i + c];
                var detF = detail / 255f;
                var colF = color / 255f;
                var p1 = ((1f - 2f * detF) * colF * colF + 2f * detF * colF) * 255f;
                p1 = Math.Min(p1 * 1.2f, 255f);
                float p2v;
                if (p1 > 128f) p2v = 255f - ((255f - 2f * (detail - 128f)) * (255f - p1) / 256f);
                else           p2v = (2f * detail * p1) / 256f;
                var blended = (p2v * pass2) + (p1 * (1f - pass2));
                if (pass3Active)
                {
                    var oc = rgbOver[2 - c];
                    var p3 = (blended / 255f) * (blended + ((2f * oc) / 255f) * (255f - blended));
                    blended = p3 * overFactor + blended * (1f - overFactor);
                }
                blended = (((blended / 255f) - midpoint) * contrast + midpoint) * 255f;
                if (blended < 0) blended = 0; else if (blended > 255) blended = 255;
                skinPixels[i + c] = (byte)blended;
            }
        }
    }
    SavePng(Path.Combine(inputDir, "step_02_after_pass1_pass2_contrast.png"), w, h, (byte[])skinPixels.Clone());

    // 3. Face overlay (straight alpha over)
    if (faceOverlay is not null)
    {
        var fo = LoadBgraResized(faceOverlay, w, h).BGRA;
        BlendStraightAlphaOver(skinPixels, fo);
    }
    SavePng(Path.Combine(inputDir, "step_03_after_face_overlay.png"), w, h, (byte[])skinPixels.Clone());

    // 4. Bundled mouth overlay
    if (File.Exists(mouthPng))
    {
        var mo = LoadBgraResized(mouthPng, w, h).BGRA;
        BlendStraightAlphaOver(skinPixels, mo);
    }
    SavePng(Path.Combine(inputDir, "step_04_final_atlas.png"), w, h, skinPixels);

    Console.WriteLine();
    Console.WriteLine("  Steps written:");
    foreach (var f in Directory.EnumerateFiles(inputDir, "step_*.png").OrderBy(static p => p))
    {
        Console.WriteLine($"    {Path.GetFileName(f)}  {new FileInfo(f).Length:N0} bytes");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-naked-link", StringComparison.OrdinalIgnoreCase))
{
    // Parses a CASPart binary and reports its NakedKey + the resolved nakedLink TGI from
    // its TgiList. Then looks up that target TGI in the prod cache to see what it is.
    // Helps validate whether following nakedLink actually produces a Full Body nude shell.
    //
    // Usage: --probe-naked-link <searchRoot> <caspartTgi> [<caspartTgi> ...]
    var searchRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    if (args.Length < 3) { Console.Error.WriteLine("Usage: --probe-naked-link <searchRoot> <caspartTgi> [...]"); return 1; }
    var targets = args.Skip(2).ToArray();

    var pnlCat = new LlamaResourceCatalogService();
    var pnlSrc = new DataSourceDefinition(Guid.NewGuid(), "ProbeNakedLink", searchRoot, SourceKind.Game);
    var pnlPkgs = Directory.EnumerateFiles(searchRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase).ToArray();

    // Build lookup of all CASPart resources keyed by FullTgi (uppercase) for fast resolution.
    Console.WriteLine($"  scanning {pnlPkgs.Length} package(s) for CASPart index...");
    var caspartByTgi = new Dictionary<string, (string Pkg, ResourceKeyRecord Key)>(StringComparer.OrdinalIgnoreCase);
    foreach (var pkg in pnlPkgs)
    {
        try
        {
            var s = await pnlCat.ScanPackageAsync(pnlSrc, pkg, progress: null, CancellationToken.None);
            foreach (var r in s.Resources)
            {
                if (r.Key.TypeName == "CASPart")
                    caspartByTgi.TryAdd(r.Key.FullTgi.ToUpperInvariant(), (pkg, r.Key));
            }
        }
        catch { }
    }
    Console.WriteLine($"  indexed {caspartByTgi.Count:N0} CASParts");

    // Read prod cache for facts/category lookup.
    var dbPath = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
    conn.Open();

    foreach (var tgi in targets)
    {
        Console.WriteLine($"\n=== {tgi} ===");
        if (!caspartByTgi.TryGetValue(tgi.ToUpperInvariant(), out var loc))
        {
            Console.WriteLine($"  CASPart not found in any scanned package.");
            continue;
        }
        try
        {
            var bytes = await pnlCat.GetResourceBytesAsync(loc.Pkg, loc.Key, raw: false, CancellationToken.None, null);
            var casPart = Ts4CasPart.Parse(bytes);
            Console.WriteLine($"  InternalName: {casPart.InternalName}");
            Console.WriteLine($"  BodyType: {casPart.BodyType} (slot={MapCasPartSlotCategoryProbe(casPart.BodyType)})");
            Console.WriteLine($"  AgeGenderFlags: 0x{casPart.AgeGenderFlags:X8}  Species: {casPart.SpeciesLabel ?? "?"}  Age: {casPart.AgeLabel}  Gender: {casPart.GenderLabel}");
            Console.WriteLine($"  PartFlags: defaultBT={casPart.DefaultForBodyType} defBTF={casPart.DefaultForBodyTypeFemale} defBTM={casPart.DefaultForBodyTypeMale}");
            Console.WriteLine($"  NakedKey (byte index into TgiList): {casPart.NakedKey}");
            Console.WriteLine($"  TgiList ({casPart.TgiList.Count} entries):");
            for (var i = 0; i < casPart.TgiList.Count; i++)
            {
                var entry = casPart.TgiList[i];
                var marker = i == casPart.NakedKey ? "  ← nakedLink target" : "";
                Console.WriteLine($"    [{i}] {entry.FullTgi} (type={entry.TypeName ?? "?"}){marker}");
            }
            // Resolve the nakedLink target.
            if (casPart.NakedKey < byte.MaxValue && casPart.NakedKey < casPart.TgiList.Count)
            {
                var linkTgi = casPart.TgiList[casPart.NakedKey];
                if (linkTgi.Type != 0)
                {
                    Console.WriteLine($"\n  Following nakedLink → {linkTgi.FullTgi}");
                    using var c = conn.CreateCommand();
                    c.CommandText = """
                        SELECT a.display_name, a.category, f.species_label, f.age_label, f.gender_label,
                               f.slot_category, f.body_type, f.internal_name, f.has_naked_link,
                               f.default_body_type, f.default_body_type_female, f.default_body_type_male
                        FROM assets a LEFT JOIN cas_part_facts f ON f.asset_id = a.id
                        WHERE a.root_tgi = $tgi
                        """;
                    c.Parameters.AddWithValue("$tgi", linkTgi.FullTgi);
                    using var r = c.ExecuteReader();
                    if (r.Read())
                    {
                        Console.WriteLine($"  → display_name: {(r.IsDBNull(0) ? "?" : r.GetString(0))}");
                        Console.WriteLine($"  → asset.category: {(r.IsDBNull(1) ? "?" : r.GetString(1))}");
                        Console.WriteLine($"  → facts: sp={(r.IsDBNull(2) ? "?" : r.GetString(2))} age='{(r.IsDBNull(3) ? "?" : r.GetString(3))}' gn={(r.IsDBNull(4) ? "?" : r.GetString(4))} slot='{(r.IsDBNull(5) ? "?" : r.GetString(5))}' bt={(r.IsDBNull(6) ? -1 : r.GetInt32(6))} iname='{(r.IsDBNull(7) ? "" : r.GetString(7))}'  naked={(r.IsDBNull(8) ? "?" : r.GetInt32(8).ToString())} defBT={(r.IsDBNull(9) ? "?" : r.GetInt32(9).ToString())} defBTF={(r.IsDBNull(10) ? "?" : r.GetInt32(10).ToString())} defBTM={(r.IsDBNull(11) ? "?" : r.GetInt32(11).ToString())}");
                    }
                    else
                    {
                        Console.WriteLine($"  → not in prod cache.");
                    }
                }
                else
                {
                    Console.WriteLine($"\n  nakedLink target is null (Type=0)");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  parse error: {ex.Message}");
        }
    }
    conn.Close();
    return 0;

    static string MapCasPartSlotCategoryProbe(int bodyType) => bodyType switch
    {
        2 => "Hair", 3 => "Head", 5 => "Full Body", 6 => "Top", 7 => "Bottom", 8 => "Shoes",
        12 => "Accessory", _ => $"BodyType{bodyType}"
    };
}

if (args.Length > 0 && string.Equals(args[0], "--probe-actual-nude", StringComparison.OrdinalIgnoreCase))
{
    var dbPath = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
    conn.Open();
    // Search for candidates by name patterns that suggest they're THE nude body.
    using var c = conn.CreateCommand();
    c.CommandText = """
        SELECT a.display_name, f.species_label, f.age_label, f.gender_label, f.internal_name,
               f.has_naked_link, f.default_body_type, f.default_body_type_female, f.default_body_type_male, f.sort_layer
        FROM assets a JOIN cas_part_facts f ON f.asset_id = a.id
        WHERE f.body_type = 5 AND lower(f.species_label) = 'human'
          AND (lower(coalesce(f.internal_name, '')) LIKE '%_nude'
               OR lower(coalesce(f.internal_name, '')) = 'yfbody'
               OR lower(coalesce(f.internal_name, '')) = 'ymbody'
               OR lower(coalesce(a.display_name, '')) LIKE '%_nude'
               OR lower(coalesce(a.display_name, '')) = 'yfbody'
               OR lower(coalesce(a.display_name, '')) = 'ymbody')
        ORDER BY f.gender_label, f.internal_name
        LIMIT 30
        """;
    using var r = c.ExecuteReader();
    Console.WriteLine("Human BodyType=5 entries with name pattern suggesting actual nude body:");
    var n = 0;
    while (r.Read())
    {
        n++;
        Console.WriteLine($"  dn='{(r.IsDBNull(0) ? "" : r.GetString(0))}' iname='{(r.IsDBNull(4) ? "" : r.GetString(4))}' sp={r.GetString(1)} age='{r.GetString(2)}' gn={r.GetString(3)} naked={r.GetInt32(5)} defBT={r.GetInt32(6)} defBTF={r.GetInt32(7)} defBTM={r.GetInt32(8)} sortLayer={r.GetInt32(9)}");
    }
    if (n == 0) Console.WriteLine("  (no rows — hm, no parts with _Nude suffix? Try other patterns)");
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-yfbody-descriptions", StringComparison.OrdinalIgnoreCase))
{
    // Pull a few yfBody_* descriptions from the assets table to see what flags are encoded.
    var dbPath = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
    conn.Open();
    using var c = conn.CreateCommand();
    c.CommandText = """
        SELECT a.display_name, a.description, f.has_naked_link, f.default_body_type, f.default_body_type_female
        FROM assets a JOIN cas_part_facts f ON f.asset_id = a.id
        WHERE lower(coalesce(f.internal_name, '')) LIKE 'yfbody_%'
          AND f.body_type = 5
          AND (f.has_naked_link = 1 OR f.default_body_type = 1 OR f.default_body_type_female = 1)
        LIMIT 3
        """;
    using var r = c.ExecuteReader();
    while (r.Read())
    {
        Console.WriteLine($"\n  display_name: {r.GetString(0)}");
        Console.WriteLine($"  facts_table:   has_naked_link={r.GetInt32(2)}  default_body_type={r.GetInt32(3)}  default_body_type_female={r.GetInt32(4)}");
        Console.WriteLine($"  asset.description:");
        var desc = r.IsDBNull(1) ? "<null>" : r.GetString(1);
        Console.WriteLine($"    {desc.Replace(" | ", "\n    | ")}");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-category-mismatch", StringComparison.OrdinalIgnoreCase))
{
    // For CASPart facts with slot_category in {Full Body, Body, Top, Bottom, Shoes}, count
    // how many have asset.category matching vs not. If many mismatch, the C# filter
    // MatchesBodyAssemblySlotCategory rejects them.
    var dbPath = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
    conn.Open();
    using var c = conn.CreateCommand();
    c.CommandText = """
        SELECT f.slot_category, a.category, COUNT(*)
        FROM cas_part_facts f
        JOIN assets a ON a.id = f.asset_id
        WHERE f.slot_category IN ('Full Body', 'Body', 'Top', 'Bottom', 'Shoes')
        GROUP BY f.slot_category, a.category
        ORDER BY f.slot_category, 3 DESC
        """;
    using var r = c.ExecuteReader();
    Console.WriteLine("slot_category × asset.category mismatch matrix:");
    while (r.Read())
        Console.WriteLine($"  facts.slot='{r.GetString(0)}'  assets.category='{r.GetString(1)}'  count={r.GetInt32(2):N0}");
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-prefix-species", StringComparison.OrdinalIgnoreCase))
{
    // Counts species_label distribution for CASParts whose internal_name starts with given
    // prefixes. Confirms whether a prefix is exclusively non-human or has legitimate human use.
    var dbPath = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
    conn.Open();
    foreach (var prefix in new[] { "acBody", "ahBody", "auBody", "afBody", "amBody", "alBody", "adBody", "yuBody", "yfBody", "ymBody", "yhBody" })
    {
        using var c = conn.CreateCommand();
        c.CommandText = """
            SELECT f.species_label, COUNT(*)
            FROM cas_part_facts f
            WHERE lower(coalesce(f.internal_name, '')) LIKE $pattern
              AND f.body_type = 5
            GROUP BY f.species_label
            ORDER BY 2 DESC
            """;
        c.Parameters.AddWithValue("$pattern", prefix.ToLowerInvariant() + "%");
        Console.WriteLine($"\n  Prefix '{prefix}*' (BodyType=5):");
        using var r = c.ExecuteReader();
        var n = 0;
        while (r.Read())
        {
            Console.WriteLine($"    {r.GetInt32(1),5}  species='{(r.IsDBNull(0) ? "<null>" : r.GetString(0))}'");
            n++;
        }
        if (n == 0) Console.WriteLine($"    (no rows)");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-tgi-facts", StringComparison.OrdinalIgnoreCase))
{
    // Looks up facts for one or more CASPart TGIs in the prod cache.
    var dbPath = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
    conn.Open();
    var tgis = args.Skip(1).ToArray();
    foreach (var tgi in tgis)
    {
        using var c = conn.CreateCommand();
        c.CommandText = """
            SELECT a.display_name, a.category, f.species_label, f.age_label, f.gender_label,
                   f.slot_category, f.body_type, f.internal_name, f.has_naked_link,
                   f.default_body_type, f.default_body_type_female, f.default_body_type_male
            FROM assets a LEFT JOIN cas_part_facts f ON f.asset_id = a.id
            WHERE a.root_tgi = $tgi
            """;
        c.Parameters.AddWithValue("$tgi", tgi);
        using var r = c.ExecuteReader();
        if (r.Read())
        {
            Console.WriteLine($"\n  TGI: {tgi}");
            Console.WriteLine($"    display_name: {(r.IsDBNull(0) ? "<null>" : r.GetString(0))}");
            Console.WriteLine($"    asset.category: {(r.IsDBNull(1) ? "<null>" : r.GetString(1))}");
            Console.WriteLine($"    facts.species_label: '{(r.IsDBNull(2) ? "<null>" : r.GetString(2))}'");
            Console.WriteLine($"    facts.age_label: '{(r.IsDBNull(3) ? "<null>" : r.GetString(3))}'");
            Console.WriteLine($"    facts.gender_label: '{(r.IsDBNull(4) ? "<null>" : r.GetString(4))}'");
            Console.WriteLine($"    facts.slot_category: '{(r.IsDBNull(5) ? "<null>" : r.GetString(5))}'");
            Console.WriteLine($"    facts.body_type: {(r.IsDBNull(6) ? "<null>" : r.GetInt32(6).ToString())}");
            Console.WriteLine($"    facts.internal_name: '{(r.IsDBNull(7) ? "<null>" : r.GetString(7))}'");
            Console.WriteLine($"    facts.has_naked_link: {(r.IsDBNull(8) ? "<null>" : r.GetInt32(8).ToString())}");
            Console.WriteLine($"    facts.default_body_type: {(r.IsDBNull(9) ? "<null>" : r.GetInt32(9).ToString())}");
            Console.WriteLine($"    facts.default_body_type_female: {(r.IsDBNull(10) ? "<null>" : r.GetInt32(10).ToString())}");
            Console.WriteLine($"    facts.default_body_type_male: {(r.IsDBNull(11) ? "<null>" : r.GetInt32(11).ToString())}");
        }
        else
        {
            Console.WriteLine($"\n  TGI: {tgi}  [NOT FOUND]");
        }
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-sim-graph", StringComparison.OrdinalIgnoreCase))
{
    // Picks the first YA-Female-Human SimInfo asset from the production cache and runs
    // ExplicitAssetGraphBuilder.BuildAssetGraphAsync against it (using the prod cache as
    // the index store). Dumps the resulting SimGraph.BodyCandidates so we can see EXACTLY
    // what the body assembly receives — including whether Full Body is present.
    var defaultProd = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache";
    var cacheDir = args.Length > 1 ? args[1] : defaultProd;
    var simTgi = args.Length > 2 ? args[2] : null;  // optional explicit Sim TGI
    if (!Directory.Exists(cacheDir)) { Console.Error.WriteLine($"Cache dir not found: {cacheDir}"); return 1; }
    Console.WriteLine($"probe-sim-graph: cache={cacheDir}");

    // Open prod cache in READ-WRITE mode (the IndexStore opens like that). We're not actually
    // mutating anything, but the InitializeAsync may want write access for migrations. We'll
    // use a copy if write fails.
    var psgCacheParent = Path.GetDirectoryName(cacheDir.TrimEnd('/', '\\'))!;
    var psgCache = new ProbeCacheService(Path.GetFullPath(cacheDir + "/.."));
    psgCache.EnsureCreated();
    var psgStore = new SqliteIndexStore(psgCache);
    await psgStore.InitializeAsync(CancellationToken.None);

    // Find a YA-Female-Human Sim in the cache (or use the user-supplied TGI).
    var dbPath = Path.Combine(cacheDir, "index.sqlite");
    string? simRootTgi = simTgi;
    string? simPackagePath = null;
    if (simRootTgi is null)
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        conn.Open();
        using var c = conn.CreateCommand();
        c.CommandText = """
            SELECT s.root_tgi, s.package_path
            FROM sim_template_facts s
            WHERE lower(coalesce(s.species_label, '')) = 'human'
              AND lower(coalesce(s.age_label, '')) LIKE '%young adult%'
              AND lower(coalesce(s.gender_label, '')) = 'female'
            LIMIT 1
            """;
        try
        {
            using var r = c.ExecuteReader();
            if (r.Read())
            {
                simRootTgi = r.GetString(0);
                simPackagePath = r.GetString(1);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  sim_template_facts query failed: {ex.Message}");
        }
    }
    if (simRootTgi is null)
    {
        Console.Error.WriteLine("  Could not locate a YA-Female-Human SimInfo. Pass an explicit TGI as 3rd arg.");
        return 1;
    }
    Console.WriteLine($"  Selected SimInfo TGI: {simRootTgi}");
    Console.WriteLine($"  Package: {simPackagePath ?? "<unknown>"}");

    // Build catalog + graph builder pointed at the cache.
    var psgCat = new LlamaResourceCatalogService();
    var psgBld = new ExplicitAssetGraphBuilder(psgCat, psgStore);

    // Diagnostic: see what the SimInfo's actual cache entry looks like (any kind, not just Sim).
    using (var conn3 = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly"))
    {
        conn3.Open();
        using (var c = conn3.CreateCommand())
        {
            c.CommandText = "SELECT COUNT(*), MIN(root_tgi), MAX(root_tgi) FROM assets WHERE asset_kind = 'Sim'";
            using var r = c.ExecuteReader();
            if (r.Read())
                Console.WriteLine($"  Sim assets in cache: {r.GetInt32(0)}  sample range: {(r.IsDBNull(1) ? "-" : r.GetString(1))} … {(r.IsDBNull(2) ? "-" : r.GetString(2))}");
        }
        using (var c = conn3.CreateCommand())
        {
            c.CommandText = "SELECT asset_kind, root_tgi, display_name FROM assets WHERE root_tgi = $tgi";
            c.Parameters.AddWithValue("$tgi", simRootTgi);
            using var r = c.ExecuteReader();
            var found = false;
            while (r.Read())
            {
                Console.WriteLine($"  asset for TGI: kind={r.GetString(0)} dn='{(r.IsDBNull(2) ? "" : r.GetString(2))}'");
                found = true;
            }
            if (!found) Console.WriteLine($"  No assets row for TGI {simRootTgi}.");
        }
        // Try a different YA Female Human SimInfo: pick one whose root_tgi exists in BOTH assets and sim_template_facts.
        using (var c = conn3.CreateCommand())
        {
            c.CommandText = """
                SELECT s.root_tgi, s.species_label, s.age_label, s.gender_label, a.display_name, a.asset_kind, a.package_path
                FROM sim_template_facts s
                JOIN assets a ON a.root_tgi = s.root_tgi
                WHERE lower(coalesce(s.species_label, '')) = 'human'
                  AND lower(coalesce(s.age_label, '')) LIKE '%young adult%'
                  AND lower(coalesce(s.gender_label, '')) = 'female'
                LIMIT 5
                """;
            using var r = c.ExecuteReader();
            Console.WriteLine($"  YA Female Human SimInfos with matching asset row:");
            while (r.Read())
                Console.WriteLine($"    {r.GetString(0)}  kind={r.GetString(5)} dn='{(r.IsDBNull(4) ? "" : r.GetString(4))}'");
        }
    }
    // If user passed an explicit TGI, keep it. Otherwise, find one that exists in both tables.
    if (args.Length <= 2)
    {
        using var conn4 = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        conn4.Open();
        using var c = conn4.CreateCommand();
        c.CommandText = """
            SELECT s.root_tgi, a.package_path
            FROM sim_template_facts s
            JOIN assets a ON a.root_tgi = s.root_tgi
            WHERE lower(coalesce(s.species_label, '')) = 'human'
              AND lower(coalesce(s.age_label, '')) LIKE '%young adult%'
              AND lower(coalesce(s.gender_label, '')) = 'female'
            LIMIT 1
            """;
        using var r = c.ExecuteReader();
        if (r.Read())
        {
            simRootTgi = r.GetString(0);
            simPackagePath = r.GetString(1);
            Console.WriteLine($"  Re-selected SimInfo (joined with assets): {simRootTgi}");
        }
    }

    // Look up the AssetSummary directly from the SQLite assets table.
    AssetSummary? simAsset = null;
    using (var conn2 = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly"))
    {
        conn2.Open();
        using var c = conn2.CreateCommand();
        c.CommandText = """
            SELECT id, data_source_id, source_kind, asset_kind, display_name, category, package_path, root_tgi,
                   thumbnail_tgi, variant_count, linked_resource_count, diagnostics, package_name, root_type_name,
                   thumbnail_type_name, primary_geometry_type, identity_type, category_normalized, description
            FROM assets WHERE root_tgi = $tgi LIMIT 1
            """;
        c.Parameters.AddWithValue("$tgi", simRootTgi);
        using var r = c.ExecuteReader();
        if (r.Read())
        {
            // Construct an AssetSummary just enough for BuildAssetGraphAsync.
            // ResourceKeyRecord parsing from "TYPE:GROUP:INSTANCE" hex.
            var tgiParts = simRootTgi.Split(':');
            var rootKey = new ResourceKeyRecord(
                Convert.ToUInt32(tgiParts[0], 16),
                Convert.ToUInt32(tgiParts[1], 16),
                Convert.ToUInt64(tgiParts[2], 16),
                r.IsDBNull(13) ? null : r.GetString(13));
            simAsset = new AssetSummary(
                Guid.Parse(r.GetString(0)),
                Guid.Parse(r.GetString(1)),
                Enum.Parse<SourceKind>(r.GetString(2)),
                Enum.Parse<AssetKind>(r.GetString(3)),
                r.IsDBNull(4) ? "" : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetString(5),
                r.GetString(6),
                rootKey,
                r.IsDBNull(8) ? null : r.GetString(8),
                r.GetInt32(9),
                r.GetInt32(10),
                r.IsDBNull(11) ? "" : r.GetString(11),
                null,
                r.IsDBNull(12) ? null : r.GetString(12),
                r.IsDBNull(13) ? null : r.GetString(13),
                r.IsDBNull(14) ? null : r.GetString(14),
                r.IsDBNull(15) ? null : r.GetString(15),
                r.IsDBNull(16) ? null : r.GetString(16),
                r.IsDBNull(17) ? null : r.GetString(17),
                r.IsDBNull(18) ? null : r.GetString(18));
        }
    }
    if (simAsset is null)
    {
        Console.Error.WriteLine($"  Sim asset not found in index for TGI {simRootTgi}");
        return 1;
    }
    Console.WriteLine($"  Sim asset: {simAsset.DisplayName}  (kind={simAsset.AssetKind})");

    // Need package resources. Scan the sim's package.
    var pkgPath = simAsset.PackagePath;
    Console.WriteLine($"  Scanning sim's package: {pkgPath}");
    var psgSrc = new DataSourceDefinition(simAsset.DataSourceId, "ProbeSimGraph", Path.GetDirectoryName(pkgPath) ?? pkgPath, SourceKind.Game);
    var packageScan = await psgCat.ScanPackageAsync(psgSrc, pkgPath, progress: null, CancellationToken.None);
    var pkgResources = packageScan.Resources;

    // Run the actual production graph builder.
    Console.WriteLine($"  Running BuildAssetGraphAsync (production code path)...");
    var simGraphResult = await psgBld.BuildAssetGraphAsync(simAsset, pkgResources, CancellationToken.None);
    Console.WriteLine($"  Graph kind: {simGraphResult.Summary.AssetKind}  Diagnostics count: {simGraphResult.Diagnostics.Count}");
    foreach (var d in simGraphResult.Diagnostics.Take(10))
        Console.WriteLine($"    diag: {d}");

    if (simGraphResult.SimGraph is null)
    {
        Console.WriteLine("  No SimGraph produced.");
        return 1;
    }
    var sg = simGraphResult.SimGraph;
    Console.WriteLine($"\n  SimGraph metadata: species='{sg.Metadata.SpeciesLabel}' age='{sg.Metadata.AgeLabel}' gender='{sg.Metadata.GenderLabel}'");
    Console.WriteLine($"  BodyCandidates ({sg.BodyCandidates.Count}):");
    foreach (var bc in sg.BodyCandidates.OrderBy(static x => x.Label))
    {
        Console.WriteLine($"    label='{bc.Label}' count={bc.Count} sourceKind={bc.SourceKind}  options={bc.Candidates.Count}");
        foreach (var opt in bc.Candidates.Take(3))
            Console.WriteLine($"      → {opt.DisplayName}  ({opt.RootTgi})");
    }
    Console.WriteLine($"\n  BodyAssembly mode: {sg.BodyAssembly.Mode}");
    Console.WriteLine($"  BodyAssembly layers ({sg.BodyAssembly.Layers.Count}):");
    foreach (var layer in sg.BodyAssembly.Layers)
        Console.WriteLine($"    {layer.Label}  state={layer.State}  count={layer.CandidateCount}");

    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--simulate-body-candidates", StringComparison.OrdinalIgnoreCase))
{
    // Replicates the EXACT production SQL from GetIndexedDefaultBodyRecipeAssetsFromDatabaseAsync
    // (IndexingServices.cs:1004-1050) against the production cache, with the 0218 strict species
    // filter applied. Tells us if the SQL pool would return Full Body candidates for the user's Sim.
    var defaultProd = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    var dbPath  = args.Length > 1 ? args[1] : defaultProd;
    var age     = args.Length > 2 ? args[2] : "Young Adult";
    var gender  = args.Length > 3 ? args[3] : "Female";
    var species = args.Length > 4 ? args[4] : "Human";
    if (!File.Exists(dbPath)) { Console.Error.WriteLine($"DB not found: {dbPath}"); return 1; }
    Console.WriteLine($"simulate-body-candidates: prod cache; age={age} gender={gender} species={species}");

    var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
    conn.Open();

    // Build prefix patterns mirroring BuildIndexedBodyRecipePrefixPatterns logic.
    // For YA Female Human, prefix is "yf"; patterns: "yfbody_%", "yfbody%nude%", "yfbody%default%", "yfbody%bare%".
    var ageNorm = age.Trim().ToLowerInvariant();
    var (agePrefix, _) = ageNorm switch
    {
        "infant" => ("i", "u"),
        "toddler" => ("p", "u"),
        "child" => ("c", "u"),
        _ => ("y", "f")  // YA/Adult/Elder default
    };
    var genderPrefix = gender.Trim().ToLowerInvariant() switch { "male" => "m", "female" => "f", _ => "u" };
    var fullPrefix = (agePrefix + genderPrefix + "Body").ToLowerInvariant();
    var pp0 = $"{fullPrefix}_%";
    var pp1 = $"{fullPrefix}%nude%";
    var pp2 = $"{fullPrefix}%default%";
    var pp3 = $"{fullPrefix}%bare%";
    Console.WriteLine($"  prefix patterns: {pp0}, {pp1}, {pp2}, {pp3}");

    foreach (var slot in new[] { "Full Body", "Body" })
    {
        var bodyType = slot switch { "Full Body" => 5, "Body" => 5, _ => -1 };
        Console.WriteLine($"\n  Querying slot='{slot}' bodyType={bodyType} (production SQL with 0218 strict species filter)...");

        using var c = conn.CreateCommand();
        c.CommandText = """
            SELECT COUNT(*),
                   SUM(CASE WHEN f.has_naked_link = 1 THEN 1 ELSE 0 END) AS naked_count,
                   SUM(CASE WHEN f.default_body_type = 1 THEN 1 ELSE 0 END) AS def_count,
                   SUM(CASE WHEN f.default_body_type_female = 1 THEN 1 ELSE 0 END) AS deff_count
            FROM assets a
            JOIN cas_part_facts f ON f.asset_id = a.id
            WHERE a.asset_kind = 'Cas'
              AND a.has_scene_root = 1
              AND f.body_type = $bodyType
              AND f.slot_category = $slotCategory
              AND (lower(f.species_label) = lower($speciesLabel))
              AND (f.age_label IS NULL OR f.age_label = '' OR lower(f.age_label) = 'unknown' OR lower(f.age_label) LIKE '%' || lower($ageLabel) || '%')
              AND (f.gender_label IS NULL OR f.gender_label = '' OR lower(f.gender_label) = 'unknown' OR lower(f.gender_label) = 'unisex' OR lower(f.gender_label) LIKE '%' || lower($genderLabel) || '%')
              AND (
                    f.has_naked_link = 1 OR
                    f.default_body_type = 1 OR
                    ($genderLabel = 'Female' AND f.default_body_type_female = 1) OR
                    ($genderLabel = 'Male' AND f.default_body_type_male = 1) OR
                    lower(coalesce(f.internal_name, '')) LIKE $pp0 OR
                    lower(coalesce(f.internal_name, '')) LIKE $pp1 OR
                    lower(coalesce(f.internal_name, '')) LIKE $pp2 OR
                    lower(coalesce(f.internal_name, '')) LIKE $pp3 OR
                    lower(coalesce(a.display_name, '')) LIKE $pp0 OR
                    lower(coalesce(a.display_name, '')) LIKE $pp1 OR
                    lower(coalesce(a.display_name, '')) LIKE $pp2 OR
                    lower(coalesce(a.display_name, '')) LIKE $pp3 OR
                    lower(coalesce(f.internal_name, '')) LIKE '%nude%' OR
                    lower(coalesce(a.display_name, '')) LIKE '%nude%' OR
                    lower(coalesce(a.description, '')) LIKE '%nude%'
                  )
            """;
        c.Parameters.AddWithValue("$bodyType", bodyType);
        c.Parameters.AddWithValue("$slotCategory", slot);
        c.Parameters.AddWithValue("$speciesLabel", species);
        c.Parameters.AddWithValue("$ageLabel", age);
        c.Parameters.AddWithValue("$genderLabel", gender);
        c.Parameters.AddWithValue("$pp0", pp0);
        c.Parameters.AddWithValue("$pp1", pp1);
        c.Parameters.AddWithValue("$pp2", pp2);
        c.Parameters.AddWithValue("$pp3", pp3);
        using var r = c.ExecuteReader();
        if (r.Read())
        {
            Console.WriteLine($"    Total candidates: {r.GetInt32(0)}");
            Console.WriteLine($"    With has_naked_link=1: {r.GetInt32(1)}");
            Console.WriteLine($"    With default_body_type=1: {r.GetInt32(2)}");
            Console.WriteLine($"    With default_body_type_female=1: {r.GetInt32(3)}");
        }
    }

    // Top-priority sample
    Console.WriteLine($"\n  Top-priority Full Body candidates that would pass IsPreferredDefaultBodyShellCandidate:");
    using (var c = conn.CreateCommand())
    {
        c.CommandText = """
            SELECT a.display_name, f.species_label, f.age_label, f.gender_label, f.internal_name,
                   f.has_naked_link, f.default_body_type, f.default_body_type_female, f.default_body_type_male
            FROM assets a
            JOIN cas_part_facts f ON f.asset_id = a.id
            WHERE a.asset_kind = 'Cas'
              AND f.body_type = 5
              AND f.slot_category = 'Full Body'
              AND lower(f.species_label) = 'human'
              AND lower(f.age_label) LIKE '%young adult%'
              AND lower(f.gender_label) LIKE '%female%'
              AND (f.has_naked_link = 1 OR f.default_body_type = 1 OR f.default_body_type_female = 1)
              AND lower(coalesce(f.internal_name, '')) LIKE 'yfbody%'
            ORDER BY
                CASE WHEN f.has_naked_link = 1 THEN 0
                     WHEN f.default_body_type = 1 THEN 1
                     WHEN f.default_body_type_female = 1 THEN 2
                     ELSE 9 END,
                a.display_name
            LIMIT 15
            """;
        using var r = c.ExecuteReader();
        var n = 0;
        while (r.Read())
        {
            n++;
            Console.WriteLine($"    dn='{(r.IsDBNull(0) ? "" : r.GetString(0))}' iname='{(r.IsDBNull(4) ? "" : r.GetString(4))}' naked={r.GetInt32(5)} defBT={r.GetInt32(6)} defBTF={r.GetInt32(7)}");
        }
        Console.WriteLine($"    ({n} rows)");
    }

    conn.Close();
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--query-prod-cache", StringComparison.OrdinalIgnoreCase))
{
    // Reads the app's PRODUCTION index.sqlite directly (read-only) and dumps:
    //   - assets.category histogram for AssetKind=Cas (was production enrichment populating slot categories?)
    //   - cas_part_facts row count + body_type=5 distribution
    //   - simulated query for human YA female body shells
    var defaultProd = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    var dbPath = args.Length > 1 ? args[1] : defaultProd;
    if (!File.Exists(dbPath)) { Console.Error.WriteLine($"DB not found: {dbPath}"); return 1; }
    Console.WriteLine($"query-prod-cache: {dbPath}  ({new FileInfo(dbPath).Length / 1024 / 1024:N0} MB)");

    var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
    conn.Open();

    // CAS asset Category histogram
    using (var c = conn.CreateCommand())
    {
        c.CommandText = "SELECT category, COUNT(*) FROM assets WHERE asset_kind='Cas' GROUP BY category ORDER BY 2 DESC LIMIT 20";
        Console.WriteLine("\n  CAS Category histogram:");
        using var r = c.ExecuteReader();
        while (r.Read())
            Console.WriteLine($"    {r.GetInt32(1),8:N0}  {(r.IsDBNull(0) ? "<null>" : r.GetString(0))}");
    }

    // cas_part_facts body_type histogram
    using (var c = conn.CreateCommand())
    {
        c.CommandText = "SELECT body_type, COUNT(*) FROM cas_part_facts GROUP BY body_type ORDER BY 2 DESC LIMIT 20";
        Console.WriteLine("\n  cas_part_facts body_type histogram:");
        using var r = c.ExecuteReader();
        while (r.Read())
            Console.WriteLine($"    {r.GetInt32(1),8:N0}  body_type={r.GetInt32(0)}");
    }

    // Run the actual production-style query for Human Young Adult Female "Full Body"
    using (var c = conn.CreateCommand())
    {
        c.CommandText = """
            SELECT a.id, a.category, a.display_name, f.species_label, f.age_label, f.gender_label,
                   f.slot_category, f.internal_name, f.has_naked_link, f.default_body_type,
                   f.default_body_type_female, f.default_body_type_male
            FROM assets a
            JOIN cas_part_facts f ON f.asset_id = a.id
            WHERE a.asset_kind = 'Cas'
              AND a.has_scene_root = 1
              AND f.body_type = 5
              AND f.slot_category = 'Full Body'
              AND lower(f.species_label) = 'human'
              AND (f.age_label IS NULL OR f.age_label = '' OR lower(f.age_label) = 'unknown' OR lower(f.age_label) LIKE '%young adult%')
              AND (f.gender_label IS NULL OR f.gender_label = '' OR lower(f.gender_label) = 'unknown' OR lower(f.gender_label) = 'unisex' OR lower(f.gender_label) LIKE '%female%')
              AND (f.has_naked_link = 1 OR f.default_body_type = 1 OR f.default_body_type_female = 1
                   OR lower(coalesce(f.internal_name, '')) LIKE '%nude%'
                   OR lower(coalesce(f.internal_name, '')) LIKE 'yfbody%'
                   OR lower(coalesce(a.display_name, '')) LIKE '%nude%')
            LIMIT 20
            """;
        Console.WriteLine("\n  Sample of YA Female Human Full-Body candidates (production query approximation):");
        using var r = c.ExecuteReader();
        var n = 0;
        while (r.Read())
        {
            n++;
            Console.WriteLine($"    [cat='{r.GetString(1)}' dn='{(r.IsDBNull(2) ? "" : r.GetString(2))}'] sp='{r.GetString(3)}' age='{r.GetString(4)}' gn='{r.GetString(5)}' slot='{r.GetString(6)}' iname='{(r.IsDBNull(7) ? "" : r.GetString(7))}' nakedLink={r.GetInt32(8)} defBT={r.GetInt32(9)} defBTF={r.GetInt32(10)} defBTM={r.GetInt32(11)}");
        }
        Console.WriteLine($"    ({n} rows shown)");
    }

    // Targeted: actual nude/default body candidates for YA Female Human.
    using (var c = conn.CreateCommand())
    {
        c.CommandText = """
            SELECT a.category, a.display_name, f.species_label, f.age_label, f.gender_label, f.slot_category, f.internal_name,
                   f.has_naked_link, f.default_body_type, f.default_body_type_female, f.default_body_type_male
            FROM assets a
            JOIN cas_part_facts f ON f.asset_id = a.id
            WHERE a.asset_kind = 'Cas'
              AND a.has_scene_root = 1
              AND f.body_type = 5
              AND f.slot_category IN ('Full Body','Body')
              AND lower(f.species_label) = 'human'
              AND (f.has_naked_link = 1 OR f.default_body_type = 1 OR f.default_body_type_female = 1
                   OR lower(coalesce(f.internal_name, '')) LIKE '%nude%')
            LIMIT 30
            """;
        Console.WriteLine("\n  ACTUAL NUDE/DEFAULT body candidates (Human, with naked_link/default_body flags or 'nude' in name):");
        using var r = c.ExecuteReader();
        var n = 0;
        while (r.Read())
        {
            n++;
            Console.WriteLine($"    [{r.GetString(0)}] dn='{(r.IsDBNull(1) ? "" : r.GetString(1))}' sp={r.GetString(2)} age='{r.GetString(3)}' gn={r.GetString(4)} slot={r.GetString(5)} iname='{(r.IsDBNull(6) ? "" : r.GetString(6))}' naked={r.GetInt32(7)} defBT={r.GetInt32(8)} defBTF={r.GetInt32(9)} defBTM={r.GetInt32(10)}");
        }
        Console.WriteLine($"    ({n} rows)");
    }

    // Group by gender for naked humans
    using (var c = conn.CreateCommand())
    {
        c.CommandText = """
            SELECT f.species_label, f.gender_label, f.age_label, COUNT(*)
            FROM cas_part_facts f
            WHERE f.body_type = 5
              AND f.slot_category IN ('Full Body','Body')
              AND (f.has_naked_link = 1 OR f.default_body_type = 1 OR f.default_body_type_female = 1 OR f.default_body_type_male = 1)
            GROUP BY f.species_label, f.gender_label, f.age_label
            ORDER BY 4 DESC
            """;
        Console.WriteLine("\n  Naked/default Body Type=5 distribution by species×gender×age:");
        using var r = c.ExecuteReader();
        while (r.Read())
            Console.WriteLine($"    {r.GetInt32(3),5}  sp='{r.GetString(0)}' gn='{r.GetString(1)}' age='{r.GetString(2)}'");
    }

    // And the dog-equivalent (same age/gender/slot, species=Dog) — should NOT be selected for human Sims.
    using (var c = conn.CreateCommand())
    {
        c.CommandText = """
            SELECT a.id, a.category, a.display_name, f.species_label, f.age_label, f.gender_label,
                   f.slot_category, f.internal_name
            FROM assets a
            JOIN cas_part_facts f ON f.asset_id = a.id
            WHERE a.asset_kind = 'Cas'
              AND f.body_type = 5
              AND lower(f.species_label) IN ('dog', 'little dog', 'cat', 'fox', 'horse')
            LIMIT 10
            """;
        Console.WriteLine("\n  Sample of NON-HUMAN BodyType=5 (sanity — what could leak in if filter is permissive):");
        using var r = c.ExecuteReader();
        var n = 0;
        while (r.Read())
        {
            n++;
            Console.WriteLine($"    [cat='{r.GetString(1)}' dn='{(r.IsDBNull(2) ? "" : r.GetString(2))}'] sp='{r.GetString(3)}' slot='{r.GetString(6)}' iname='{(r.IsDBNull(7) ? "" : r.GetString(7))}'");
        }
        Console.WriteLine($"    ({n} rows shown)");
    }

    conn.Close();
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--scan-bodytype5-casparts", StringComparison.OrdinalIgnoreCase))
{
    // Bypasses the SQLite index entirely. Scans selected packages, downloads each
    // CASPart's bytes, runs Ts4SeedMetadataExtractor.TryExtractCasPartSeedMetadata
    // directly, and dumps the species×age×gender×slot distribution of BodyType=5
    // CASParts. This is the source-of-truth for what canonical-foundation queries
    // would see at runtime.
    //
    // Usage: --scan-bodytype5-casparts <searchRoot> [packageGlob]
    //   packageGlob defaults to ClientFullBuild* and ClientDeltaBuild* (where default
    //   nude body parts ship). Pass "*" to scan everything.
    var searchRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    var pkgGlob    = args.Length > 2 ? args[2] : "ClientFullBuild*";
    var sbcCat = new LlamaResourceCatalogService();
    var sbcSrc = new DataSourceDefinition(
        Guid.NewGuid(), "ScanBodyType5", searchRoot, SourceKind.Game);
    IEnumerable<string> sbcPkgs = pkgGlob == "*"
        ? Directory.EnumerateFiles(searchRoot, "*.package", SearchOption.AllDirectories)
        : Directory.EnumerateFiles(searchRoot, pkgGlob + ".package", SearchOption.AllDirectories);
    var sbcPkgList = sbcPkgs.OrderBy(static p => p, StringComparer.OrdinalIgnoreCase).ToArray();
    Console.WriteLine($"scan-bodytype5-casparts: scanning {sbcPkgList.Length} package(s) matching '{pkgGlob}.package'");

    var rows = new List<(string PkgFile, string Tgi, int BodyType, string Slot, string Species, string Age, string Gender, string InternalName, bool DefBT, bool DefBTF, bool DefBTM, bool HasNakedLink)>();
    int totalCas = 0, parsed = 0, parseFailed = 0;
    var sw = System.Diagnostics.Stopwatch.StartNew();
    foreach (var pkg in sbcPkgList)
    {
        try
        {
            var s = await sbcCat.ScanPackageAsync(sbcSrc, pkg, progress: null, CancellationToken.None);
            foreach (var r in s.Resources)
            {
                if (r.Key.TypeName != "CASPart") continue;
                totalCas++;
                try
                {
                    var bytes = await sbcCat.GetResourceBytesAsync(pkg, r.Key, raw: false, CancellationToken.None, null);
                    var seed = Ts4SeedMetadataExtractor.TryExtractCasPartSeedMetadata(r, bytes);
                    if (seed?.Fact is null) { parseFailed++; continue; }
                    parsed++;
                    var f = seed.Fact;
                    if (f.BodyType != 5) continue;
                    rows.Add((Path.GetFileName(pkg), r.Key.FullTgi, f.BodyType,
                        f.SlotCategory ?? "", f.SpeciesLabel ?? "", f.AgeLabel ?? "", f.GenderLabel ?? "",
                        f.InternalName ?? "", f.DefaultForBodyType, f.DefaultForBodyTypeFemale, f.DefaultForBodyTypeMale, f.HasNakedLink));
                }
                catch { parseFailed++; }
            }
        }
        catch (Exception ex) { Console.Error.WriteLine($"  scan error: {pkg}: {ex.Message}"); }
    }
    sw.Stop();
    Console.WriteLine($"  CASParts seen: {totalCas:N0}  parsed: {parsed:N0}  parse failed: {parseFailed:N0}  elapsed: {sw.Elapsed.TotalSeconds:N1}s");
    Console.WriteLine($"  BodyType=5 rows: {rows.Count}");

    var groups = rows
        .GroupBy(static x => $"sp='{x.Species}' age='{x.Age}' gn='{x.Gender}' slot='{x.Slot}'")
        .OrderByDescending(static g => g.Count());
    Console.WriteLine($"\n  Distribution (species × age × gender × slot):");
    foreach (var g in groups.Take(30))
        Console.WriteLine($"    {g.Count(),5}  {g.Key}");

    // Highlight the rows that would slip past the species filter for a Human Sim:
    // empty species_label is the bug we're chasing.
    var emptySpecies = rows.Where(static x => string.IsNullOrWhiteSpace(x.Species)).ToList();
    Console.WriteLine($"\n  ROWS WITH EMPTY SPECIES_LABEL (the species-filter bypass): {emptySpecies.Count}");
    foreach (var x in emptySpecies.Take(20))
        Console.WriteLine($"    [age='{x.Age}' gn='{x.Gender}' slot='{x.Slot}' iname='{x.InternalName}' defBT={x.DefBT} defBTF={x.DefBTF} defBTM={x.DefBTM} naked={x.HasNakedLink}]  {x.Tgi}  {x.PkgFile}");

    // Also dump non-human BodyType=5 rows for comparison.
    var nonHuman = rows.Where(static x => !string.IsNullOrWhiteSpace(x.Species) && !x.Species.Equals("Human", StringComparison.OrdinalIgnoreCase)).ToList();
    Console.WriteLine($"\n  Non-human BodyType=5: {nonHuman.Count}");
    foreach (var g in nonHuman.GroupBy(static x => x.Species).OrderByDescending(static g => g.Count()))
        Console.WriteLine($"    {g.Count(),4}  species='{g.Key}'");
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--query-body-recipe-facts", StringComparison.OrdinalIgnoreCase))
{
    // Reads the body_recipe_facts table directly from the partial cache to see what
    // BodyType=5 candidates exist and what their species/age/gender labels are.
    // Bypasses C#-level filters entirely.
    var qbrCacheDir = Path.Combine(AppContext.BaseDirectory, "probe-cache-indexed-bodies");
    var dbPath = Path.Combine(qbrCacheDir, "cache", "index.sqlite");
    if (!File.Exists(dbPath))
    {
        Console.Error.WriteLine($"DB not found: {dbPath}");
        return 1;
    }
    var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
    conn.Open();

    // Discover available tables/columns first.
    using (var c = conn.CreateCommand())
    {
        c.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
        using var r = c.ExecuteReader();
        Console.WriteLine("Tables in cache:");
        while (r.Read()) Console.WriteLine($"  {r.GetString(0)}");
    }

    // Discover columns of cas_part_facts.
    using (var c = conn.CreateCommand())
    {
        c.CommandText = "PRAGMA table_info(cas_part_facts)";
        using var r = c.ExecuteReader();
        Console.WriteLine("\ncas_part_facts columns:");
        while (r.Read()) Console.WriteLine($"  {r.GetInt32(0),3}  {r.GetString(1),-30}  {r.GetString(2)}");
    }

    using (var c = conn.CreateCommand())
    {
        c.CommandText = "SELECT body_type, COUNT(*) FROM cas_part_facts GROUP BY body_type ORDER BY 2 DESC";
        using var r = c.ExecuteReader();
        Console.WriteLine("\ncas_part_facts body_type distribution:");
        while (r.Read()) Console.WriteLine($"  body_type={r.GetInt32(0),3}  count={r.GetInt32(1):N0}");
    }
    using (var c = conn.CreateCommand())
    {
        c.CommandText = @"SELECT body_type, slot_category, species_label, age_label, gender_label,
                                 default_body_type, default_body_type_female, default_body_type_male,
                                 has_naked_link, internal_name
                          FROM cas_part_facts
                          WHERE body_type = 5
                          LIMIT 5000";
        try
        {
            using var r = c.ExecuteReader();
            Console.WriteLine("\nbody_recipe_facts (BodyType=5) — top species×age×gender groupings:");
            var rows = new List<(int bt, string slot, string sp, string ag, string gn, int dbt, int dbtf, int dbtm, int hnl, string inm)>();
            while (r.Read())
            {
                rows.Add((
                    r.GetInt32(0),
                    r.IsDBNull(1) ? "" : r.GetString(1),
                    r.IsDBNull(2) ? "" : r.GetString(2),
                    r.IsDBNull(3) ? "" : r.GetString(3),
                    r.IsDBNull(4) ? "" : r.GetString(4),
                    r.IsDBNull(5) ? 0 : r.GetInt32(5),
                    r.IsDBNull(6) ? 0 : r.GetInt32(6),
                    r.IsDBNull(7) ? 0 : r.GetInt32(7),
                    r.IsDBNull(8) ? 0 : r.GetInt32(8),
                    r.IsDBNull(9) ? "" : r.GetString(9)));
            }
            Console.WriteLine($"  Total BodyType=5 rows: {rows.Count}");
            var grouped = rows
                .GroupBy(static x => $"sp='{x.sp}' age='{x.ag}' gn='{x.gn}'")
                .OrderByDescending(static g => g.Count())
                .Take(20);
            foreach (var g in grouped)
                Console.WriteLine($"    {g.Count(),5} × {g.Key}");
            Console.WriteLine("\n  First 20 rows:");
            foreach (var x in rows.Take(20))
                Console.WriteLine($"    bt={x.bt} slot='{x.slot}' sp='{x.sp}' age='{x.ag}' gn='{x.gn}' dbt={x.dbt} dbtf={x.dbtf} dbtm={x.dbtm} hnl={x.hnl} inm='{x.inm}'");
        }
        catch (Exception ex) { Console.WriteLine($"  query error: {ex.Message}"); }
    }
    conn.Close();
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--query-indexed-bodies", StringComparison.OrdinalIgnoreCase))
{
    // Query-only counterpart to --probe-indexed-bodies. Uses an EXISTING
    // probe-cache-indexed-bodies/ SQLite store (created by an earlier --probe-indexed-bodies
    // run, possibly partial) and dumps the species distribution of Body/Full Body slot
    // candidates without re-indexing. Prints unbuffered (Console.Out is flushed) so
    // killing the process doesn't lose output.
    var qibCacheDir = Path.Combine(AppContext.BaseDirectory, "probe-cache-indexed-bodies");
    if (!Directory.Exists(qibCacheDir))
    {
        Console.Error.WriteLine($"Cache not found: {qibCacheDir}. Run --probe-indexed-bodies first.");
        return 1;
    }
    var qibCache = new ProbeCacheService(qibCacheDir);
    qibCache.EnsureCreated();
    var qibStore = new SqliteIndexStore(qibCache);
    await qibStore.InitializeAsync(CancellationToken.None);
    Console.WriteLine("query-indexed-bodies: store initialized, querying...");

    foreach (var slot in new[] { "Body", "Full Body" })
    {
        Console.WriteLine($"\n  Querying CAS slotCategory='{slot}'...");
        var query = new AssetBrowserQuery(
            new SourceScope(),
            string.Empty,
            AssetBrowserDomain.Cas,
            slot,
            string.Empty,
            string.Empty,
            false,
            false,
            AssetBrowserSort.Name,
            0,
            500);
        var result = await qibStore.QueryAssetsAsync(query, CancellationToken.None);
        Console.WriteLine($"    Total: {result.TotalCount:N0}");
        Console.WriteLine($"    Returned: {result.Items.Count}");
        if (result.Items.Count == 0) continue;

        var bySpecies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var samples = new List<string>();
        foreach (var item in result.Items)
        {
            var sp = Ts4SeedMetadataExtractor.TryExtractCasPartSummaryField(item.Description, "species") ?? "<empty>";
            bySpecies[sp] = bySpecies.GetValueOrDefault(sp) + 1;
            if (samples.Count < 30)
            {
                var ag = Ts4SeedMetadataExtractor.TryExtractCasPartSummaryField(item.Description, "age") ?? "<empty>";
                var gn = Ts4SeedMetadataExtractor.TryExtractCasPartSummaryField(item.Description, "gender") ?? "<empty>";
                var iname = Ts4SeedMetadataExtractor.TryExtractCasPartSummaryField(item.Description, "internalName") ?? "<empty>";
                var dn = item.DisplayName ?? "";
                if (dn.Length > 32) dn = dn.Substring(0, 32) + "…";
                if (iname.Length > 36) iname = iname.Substring(0, 36) + "…";
                samples.Add($"      [sp={sp,-12} age={ag,-12} gn={gn,-7}] DN=\"{dn,-34}\" iname=\"{iname}\"  {item.RootKey.FullTgi}");
            }
        }
        Console.WriteLine($"    Species distribution:");
        foreach (var kv in bySpecies.OrderByDescending(static k => k.Value))
            Console.WriteLine($"      {kv.Value,5:N0}  {kv.Key}");
        Console.WriteLine($"    Sample of {samples.Count}:");
        foreach (var s in samples) Console.WriteLine(s);
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-indexed-bodies", StringComparison.OrdinalIgnoreCase))
{
    // Runs the heavy indexer (BuildAssetSummaries + ReplacePackageAsync) across all packages
    // to populate a SQLite index — same path the app uses on startup. Then queries for CAS
    // assets with slot category "Body" or "Full Body" and dumps their facts so we can see
    // what canonical-foundation candidates would match for a given species/age/gender — and
    // critically, whether any non-human (dog/cat/etc.) CASParts slip through with an empty
    // SpeciesLabel.
    //
    // Usage: --probe-indexed-bodies <searchRoot> [age] [gender] [species]
    var searchRoot   = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    var ageLabel     = args.Length > 2 ? args[2] : "Young Adult";
    var genderLabel  = args.Length > 3 ? args[3] : "Female";
    var speciesLabel = args.Length > 4 ? args[4] : "Human";
    if (!Directory.Exists(searchRoot)) { Console.Error.WriteLine($"Directory not found: {searchRoot}"); return 1; }

    var pibCacheDir = Path.Combine(AppContext.BaseDirectory, "probe-cache-indexed-bodies");
    var pibCache = new ProbeCacheService(pibCacheDir);
    pibCache.EnsureCreated();
    var pibStore = new SqliteIndexStore(pibCache);
    await pibStore.InitializeAsync(CancellationToken.None);

    var pibCat = new LlamaResourceCatalogService();
    var pibSrc = new DataSourceDefinition(
        Guid.Parse("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF"), "ProbeIndexedBodies", searchRoot, SourceKind.Game);
    var pibBld = new ExplicitAssetGraphBuilder(pibCat, pibStore);
    var pibPkgs = Directory.EnumerateFiles(searchRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase).ToArray();
    Console.WriteLine($"probe-indexed-bodies: indexing {pibPkgs.Length} package(s) — this takes ~3-5 min on first run, fast on re-runs (cached)...");

    var sw = System.Diagnostics.Stopwatch.StartNew();
    var indexedCount = 0;
    var totalAssetsAdded = 0;
    foreach (var pkg in pibPkgs)
    {
        try
        {
            var s = await pibCat.ScanPackageAsync(pibSrc, pkg, progress: null, CancellationToken.None);
            var pibAssets = pibBld.BuildAssetSummaries(s);
            await pibStore.ReplacePackageAsync(s, pibAssets, CancellationToken.None);
            totalAssetsAdded += pibAssets.Count;
            indexedCount++;
            if (indexedCount % 500 == 0)
                Console.WriteLine($"  ... {indexedCount}/{pibPkgs.Length} packages indexed, {totalAssetsAdded:N0} assets, {sw.Elapsed.TotalSeconds:N0}s elapsed");
        }
        catch (Exception ex) { Console.Error.WriteLine($"  index error: {pkg}: {ex.Message}"); }
    }
    sw.Stop();
    Console.WriteLine($"  indexed {indexedCount}/{pibPkgs.Length} packages, {totalAssetsAdded:N0} assets, total {sw.Elapsed.TotalSeconds:N1}s");

    // Now query for body shell candidates. Use the AssetBrowserQuery path AssetServices uses.
    foreach (var slot in new[] { "Body", "Full Body" })
    {
        Console.WriteLine($"\n  Querying CAS slotCategory='{slot}' (no search text — full slot scan)...");
        var query = new AssetBrowserQuery(
            new SourceScope(),
            string.Empty,
            AssetBrowserDomain.Cas,
            slot,
            string.Empty,
            string.Empty,
            false,
            false,
            AssetBrowserSort.Name,
            0,
            500);
        var result = await pibStore.QueryAssetsAsync(query, CancellationToken.None);
        Console.WriteLine($"    Total: {result.TotalCount:N0}");
        if (result.Items.Count == 0) continue;

        // Group by extracted species/age/gender to see distribution.
        var bySpecies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var samples = new List<string>();
        foreach (var item in result.Items)
        {
            var sp = Ts4SeedMetadataExtractor.TryExtractCasPartSummaryField(item.Description, "species") ?? "<empty>";
            bySpecies[sp] = bySpecies.GetValueOrDefault(sp) + 1;
            if (samples.Count < 25)
            {
                var ag = Ts4SeedMetadataExtractor.TryExtractCasPartSummaryField(item.Description, "age") ?? "<empty>";
                var gen = Ts4SeedMetadataExtractor.TryExtractCasPartSummaryField(item.Description, "gender") ?? "<empty>";
                var iname = Ts4SeedMetadataExtractor.TryExtractCasPartSummaryField(item.Description, "internalName") ?? "<empty>";
                var dn = item.DisplayName ?? "";
                if (dn.Length > 28) dn = dn.Substring(0, 28) + "…";
                samples.Add($"      [species={sp,-12} age={ag,-12} gen={gen,-7}] {dn,-30} internalName={iname}  |  {item.RootKey.FullTgi}");
            }
        }
        Console.WriteLine($"    Species distribution:");
        foreach (var kv in bySpecies.OrderByDescending(static k => k.Value))
            Console.WriteLine($"      {kv.Value,5:N0}  {kv.Key}");
        Console.WriteLine($"    Sample of {samples.Count}:");
        foreach (var s in samples) Console.WriteLine(s);
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-cas-descriptions", StringComparison.OrdinalIgnoreCase))
{
    // Inspects what's in CASPart resource Description fields and whether
    // Ts4SeedMetadataExtractor can extract a slot category from them. Helps diagnose
    // why every CAS asset ends up with Category="CAS" instead of "Body"/"Top"/etc.
    var searchRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    var sampleSize = args.Length > 2 && int.TryParse(args[2], out var ss) ? ss : 30;
    var pcdCat = new LlamaResourceCatalogService();
    var pcdSrc = new DataSourceDefinition(
        Guid.Parse("EEEEEEEE-EEEE-EEEE-EEEE-EEEEEEEEEEEE"), "ProbeCasDesc", searchRoot, SourceKind.Game);
    var pcdPkgs = Directory.EnumerateFiles(searchRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase).Take(5).ToArray();
    Console.WriteLine($"probe-cas-descriptions: scanning {pcdPkgs.Length} package(s) (first 5 only)...");

    var totalCasResources = 0;
    var emptyDescriptions = 0;
    var nonEmptyDescriptions = 0;
    var extractorMatches = 0;
    var extractorNullForNonEmpty = 0;
    var samples = new List<(string Tgi, string Desc, string? ExtractedCategory)>();
    foreach (var pkg in pcdPkgs)
    {
        try
        {
            var s = await pcdCat.ScanPackageAsync(pcdSrc, pkg, progress: null, CancellationToken.None);
            foreach (var r in s.Resources)
            {
                if (r.Key.TypeName != "CASPart") continue;
                totalCasResources++;
                var desc = r.Description;
                if (string.IsNullOrWhiteSpace(desc)) { emptyDescriptions++; continue; }
                nonEmptyDescriptions++;
                var extracted = Ts4SeedMetadataExtractor.TryExtractCasPartSlotCategory(desc);
                if (extracted is not null) extractorMatches++;
                else extractorNullForNonEmpty++;
                if (samples.Count < sampleSize)
                {
                    var snip = desc.Length > 200 ? desc.Substring(0, 200) + "..." : desc;
                    samples.Add((r.Key.FullTgi, snip, extracted));
                }
            }
        }
        catch (Exception ex) { Console.Error.WriteLine($"  scan error: {pkg}: {ex.Message}"); }
    }
    Console.WriteLine($"  total CASPart resources: {totalCasResources:N0}");
    Console.WriteLine($"  empty descriptions: {emptyDescriptions:N0}");
    Console.WriteLine($"  non-empty descriptions: {nonEmptyDescriptions:N0}");
    Console.WriteLine($"  extractor matched a category: {extractorMatches:N0}");
    Console.WriteLine($"  extractor returned null on non-empty: {extractorNullForNonEmpty:N0}");
    Console.WriteLine();
    foreach (var sm in samples)
    {
        Console.WriteLine($"  {sm.Tgi}");
        Console.WriteLine($"    extracted: {(sm.ExtractedCategory ?? "<null>")}");
        Console.WriteLine($"    desc[0:200]: {sm.Desc.Replace("\n", " | ").Replace("\r", "")}");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-cas-categories", StringComparison.OrdinalIgnoreCase))
{
    // Histogram of Category values across ALL CAS asset summaries — to discover what
    // category strings the indexer actually uses (since "Body" / "Full Body" returned 0).
    var searchRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    var pccCat = new LlamaResourceCatalogService();
    var pccSrc = new DataSourceDefinition(
        Guid.Parse("DDDDDDDD-DDDD-DDDD-DDDD-DDDDDDDDDDDD"), "ProbeCasCats", searchRoot, SourceKind.Game);
    var pccBld = new ExplicitAssetGraphBuilder(pccCat);
    var pccPkgs = Directory.EnumerateFiles(searchRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase).ToArray();
    Console.WriteLine($"probe-cas-categories: scanning {pccPkgs.Length} package(s)...");
    var categoryHist = new Dictionary<string, int>(StringComparer.Ordinal);
    var normHist = new Dictionary<string, int>(StringComparer.Ordinal);
    foreach (var pkg in pccPkgs)
    {
        try
        {
            var s = await pccCat.ScanPackageAsync(pccSrc, pkg, progress: null, CancellationToken.None);
            foreach (var a in pccBld.BuildAssetSummaries(s))
            {
                if (a.AssetKind != AssetKind.Cas) continue;
                var cat = a.Category ?? "<null>";
                categoryHist[cat] = categoryHist.GetValueOrDefault(cat) + 1;
                var ncat = a.CategoryNormalized ?? "<null>";
                normHist[ncat] = normHist.GetValueOrDefault(ncat) + 1;
            }
        }
        catch { }
    }
    Console.WriteLine("\n  Category histogram (top 30):");
    foreach (var kv in categoryHist.OrderByDescending(static k => k.Value).Take(30))
        Console.WriteLine($"    {kv.Value,8:N0}  {kv.Key}");
    Console.WriteLine("\n  CategoryNormalized histogram (top 30):");
    foreach (var kv in normHist.OrderByDescending(static k => k.Value).Take(30))
        Console.WriteLine($"    {kv.Value,8:N0}  {kv.Key}");
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-canonical-body", StringComparison.OrdinalIgnoreCase))
{
    // Mirrors the canonical-foundation search that BuildSimBodyCandidatesAsync runs at
    // AssetServices.cs:2229 — looks for CASPart resources whose name matches one of the
    // species/age/gender prefixes followed by a "Nude"/"Default"/"Bare"/"Base Body" keyword.
    // Confirms whether build 0217 will find a Body shell candidate for a given metadata
    // BEFORE the user takes the build for a spin.
    //
    // Usage: --probe-canonical-body <searchRoot> [age=YoungAdult|Adult|Teen|...] [gender=Female|Male]
    var searchRoot   = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    var ageLabel     = args.Length > 2 ? args[2] : "Young Adult";
    var genderLabel  = args.Length > 3 ? args[3] : "Female";
    if (!Directory.Exists(searchRoot)) { Console.Error.WriteLine($"Directory not found: {searchRoot}"); return 1; }

    // Single-letter prefix encoding from TS4 naming convention:
    //   a = adult, y = young adult, t = teen, c = child, p = toddler, b = baby
    //   m = male, f = female, u = unisex
    static (string AgePrefix, string GenderPrefix) PrefixFor(string age, string gender)
    {
        var a = age.Trim().ToLowerInvariant() switch
        {
            "baby" => "b", "toddler" => "p", "child" => "c", "teen" => "t",
            "young adult" or "ya" => "y", "adult" => "a", "elder" => "e",
            "infant" => "i", _ => "?"
        };
        var g = gender.Trim().ToLowerInvariant() switch
        {
            "male" => "m", "female" => "f", _ => "u"
        };
        return (a, g);
    }
    var (ageP, genP) = PrefixFor(ageLabel, genderLabel);
    // Build the prefixes the production code uses (BuildExpectedBodyShellPrefixes +
    // BuildGenericHumanFoundationPrefixes). We approximate with a few common forms.
    var prefixes = new[]
    {
        $"{ageP}{genP}Body",                    // e.g. yfBody — adult/age + gender + Body
        $"{ageP}uBody",                         // e.g. yuBody — age + unisex + Body
        $"auBody",                              // canonical adult-unisex (TS4SimRipper convention)
        $"a{genP}Body",                         // e.g. afBody
        $"y{genP}Body",                         // e.g. yfBody — repeat for explicit YA
    }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    var keywords = new[] { "Nude", "Default", "Base Body", "Default Body", "Bare", "Solid" };

    Console.WriteLine($"probe-canonical-body: age={ageLabel} gender={genderLabel}  prefixes={string.Join("|", prefixes)}");
    Console.WriteLine($"  scanning packages...");

    var pcbCatalog = new LlamaResourceCatalogService();
    var pcbSource = new DataSourceDefinition(
        Guid.Parse("CCCCCCCC-CCCC-CCCC-CCCC-CCCCCCCCCCCC"), "ProbeCanonicalBody", searchRoot, SourceKind.Game);
    var pcbPackages = Directory.EnumerateFiles(searchRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase).ToArray();

    var pcbBuilder = new ExplicitAssetGraphBuilder(pcbCatalog);
    var matches = new List<(string DisplayName, string Category, string Description, string Tgi, string Pkg)>();
    var totalCasAssets = 0;
    var bodyCategoryCount = 0;
    var bodySamples = new List<(string DisplayName, string Category, string Description, string Tgi, string Pkg)>();
    var packagesWithBodyAssets = 0;
    foreach (var pkg in pcbPackages)
    {
        try
        {
            var s = await pcbCatalog.ScanPackageAsync(pcbSource, pkg, progress: null, CancellationToken.None);
            var pcbAssets = pcbBuilder.BuildAssetSummaries(s);
            var pkgHasBody = false;
            foreach (var a in pcbAssets)
            {
                if (a.AssetKind != AssetKind.Cas) continue;
                totalCasAssets++;
                var category = a.Category ?? a.CategoryNormalized ?? "";
                if (!category.Equals("Body", StringComparison.OrdinalIgnoreCase) &&
                    !category.Equals("Full Body", StringComparison.OrdinalIgnoreCase))
                    continue;
                bodyCategoryCount++;
                pkgHasBody = true;

                var dn = a.DisplayName ?? "";
                var desc = a.Description ?? "";
                var hay = ($"{dn} {desc}").ToLowerInvariant();

                // Capture a few representatives regardless of pattern, for context.
                if (bodySamples.Count < 25)
                    bodySamples.Add((dn, category, desc, a.RootKey.FullTgi, pkg));

                var hasPrefix = prefixes.Any(p => hay.Contains(p.ToLowerInvariant()));
                var hasKeyword = keywords.Any(k => hay.Contains(k.ToLowerInvariant()));
                if (hasPrefix && hasKeyword)
                {
                    matches.Add((dn, category, desc, a.RootKey.FullTgi, pkg));
                }
            }
            if (pkgHasBody) packagesWithBodyAssets++;
        }
        catch (Exception ex) { Console.Error.WriteLine($"  scan error: {pkg}: {ex.Message}"); }
    }

    Console.WriteLine($"  total CAS asset summaries scanned: {totalCasAssets:N0}");
    Console.WriteLine($"  CAS assets with Body/Full Body category: {bodyCategoryCount:N0} across {packagesWithBodyAssets:N0} package(s)");
    Console.WriteLine($"  matches (prefix ∩ keyword in DisplayName/Description): {matches.Count}");
    Console.WriteLine();
    if (matches.Count > 0)
    {
        Console.WriteLine("  Matches (top 20):");
        foreach (var m in matches.Take(20))
        {
            var descSnip = m.Description.Length > 60 ? m.Description.Substring(0, 60) + "..." : m.Description;
            Console.WriteLine($"    [{m.Category}] {m.DisplayName}  |  desc=\"{descSnip}\"  |  {m.Tgi}  |  {Path.GetFileName(m.Pkg)}");
        }
        if (matches.Count > 20) Console.WriteLine($"    ... and {matches.Count - 20} more");
    }
    if (bodySamples.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"  Body-category samples ({bodySamples.Count} of {bodyCategoryCount}):");
        foreach (var m in bodySamples)
        {
            var descSnip = m.Description.Length > 80 ? m.Description.Substring(0, 80) + "..." : m.Description;
            Console.WriteLine($"    [{m.Category}] DN=\"{m.DisplayName}\"  desc=\"{descSnip}\"  |  {m.Tgi}");
        }
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-instance", StringComparison.OrdinalIgnoreCase))
{
    // Lists every resource at a given instance ID across all packages, grouped by TypeName.
    // Use this when --probe-cas-mat reports "0 MaterialDefinition resources" to discover
    // what resource types actually exist at that instance.
    //
    // Usage: --probe-instance <searchRoot> <instanceHex>
    var searchRoot   = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    var instanceArg  = args.Length > 2 ? args[2] : null;
    if (!Directory.Exists(searchRoot)) { Console.Error.WriteLine($"Directory not found: {searchRoot}"); return 1; }
    if (instanceArg is null) { Console.Error.WriteLine("Usage: --probe-instance <searchRoot> <instanceHex>"); return 1; }
    var instanceStr = instanceArg.Contains(':') ? instanceArg.Split(':')[2] : instanceArg;
    if (!ulong.TryParse(instanceStr, System.Globalization.NumberStyles.HexNumber, null, out var targetInst))
    { Console.Error.WriteLine($"Cannot parse instance: {instanceArg}"); return 1; }

    var piCatalog = new LlamaResourceCatalogService();
    var piSource = new DataSourceDefinition(
        Guid.Parse("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB"), "ProbeInstance", searchRoot, SourceKind.Game);
    var piPackages = Directory.EnumerateFiles(searchRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase).ToArray();
    Console.WriteLine($"probe-instance: 0x{targetInst:X16}  scanning {piPackages.Length} package(s)...");

    var hits = new List<(string Pkg, ResourceMetadata R)>();
    foreach (var pkg in piPackages)
    {
        try
        {
            var s = await piCatalog.ScanPackageAsync(piSource, pkg, progress: null, CancellationToken.None);
            foreach (var r in s.Resources)
                if (r.Key.FullInstance == targetInst)
                    hits.Add((pkg, r));
        }
        catch (Exception ex) { Console.Error.WriteLine($"  scan error: {pkg}: {ex.Message}"); }
    }

    Console.WriteLine($"  Total hits: {hits.Count}");
    foreach (var grp in hits.GroupBy(static h => h.R.Key.TypeName ?? "?").OrderBy(static g => g.Key, StringComparer.Ordinal))
    {
        Console.WriteLine($"\n  {grp.Key}  (type=0x{grp.First().R.Key.Type:X8})  ({grp.Count()} cop{(grp.Count() == 1 ? "y" : "ies")}):");
        foreach (var (pkg, r) in grp.OrderBy(static h => h.Pkg, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"    {r.Key.FullTgi}  in {Path.GetFileName(pkg)}");
        }
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--dump-skin-textures", StringComparison.OrdinalIgnoreCase))
{
    // Fetches the four atlas inputs that SimSkinAtlasComposer composites for a given
    // Sim's age×gender on a given skintone, decodes each via the standard PNG decoder,
    // and writes them to disk. Lets us inspect the raw inputs visually (and via PNG
    // dimensions/byte size) without launching the WinUI app — answers questions like
    // "is the base skin texture itself naturally pale, or is the math making it pale?"
    //
    // Usage: --dump-skin-textures <searchRoot> <skintoneInstance> [age] [gender] [outputDir]
    // Example: --dump-skin-textures "C:\GAMES\The Sims 4" 0000000000005545 Adult Female tmp/skin-dump
    var searchRoot   = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    var instanceArg  = args.Length > 2 ? args[2] : null;
    var ageLabel     = args.Length > 3 ? args[3] : "Adult";
    var genderLabel  = args.Length > 4 ? args[4] : "Female";
    var outputDir    = args.Length > 5 ? args[5] : Path.Combine(Environment.CurrentDirectory, "tmp", "skin-dump");

    if (!Directory.Exists(searchRoot)) { Console.Error.WriteLine($"Directory not found: {searchRoot}"); return 1; }
    if (instanceArg is null) { Console.Error.WriteLine("Usage: --dump-skin-textures <searchRoot> <skintoneInstance> [age] [gender] [outputDir]"); return 1; }
    var instanceStr = instanceArg.Contains(':') ? instanceArg.Split(':')[2] : instanceArg;
    if (!ulong.TryParse(instanceStr, System.Globalization.NumberStyles.HexNumber, null, out var skintoneInst))
    { Console.Error.WriteLine($"Cannot parse instance: {instanceArg}"); return 1; }

    // Mirror AssetServices.SkinBlenderDetailNeutralByIndex (lines 1769-1790) and
    // TryComputeSkinIndex/TryComputeAgeGenderMask (1792-1855) so the probe replicates
    // exactly what the app would request.
    var detailByIndex = new ulong[]
    {
        0x0A11C0657FBDB54FUL, 0xD19E353A4001EC4DUL, 0x9CB2C5C93E357C62UL, 0x48F11375333EDB51UL,
        0x58F8275474E1AE00UL, 0x308855B3BFF0E848UL, 0x24DFF8E30DC7E5DCUL, 0xA062AF087257C3AAUL,
        0xA3EC609A2DAB31D3UL, 0x265B16FA4E7DA19BUL, 0x25EBBD9BED791D4FUL, 0x737A5FF0EB729888UL,
        0x36C865290B1F4E79UL, 0x59093C1074E2C911UL, 0x2356ABE32AC4C255UL, 0xF85FB112905485DBUL,
        0x0A136CA1147B1772UL, 0x53F13B3669333A6AUL, 0x1E1930AE6138725EUL,
    };
    static int? CalcSkinIndex(string age, string gender)
    {
        var a = age.Trim().ToLowerInvariant();
        if (a == "infant") return 1;
        var ageBit = a switch
        {
            "baby" => 0, "toddler" => 1, "child" => 2, "teen" => 3,
            "young adult" or "ya" => 4, "adult" => 5, "elder" => 6, _ => -1
        };
        if (ageBit < 0) return null;
        return string.Equals(gender, "Female", StringComparison.OrdinalIgnoreCase) && ageBit >= 3 ? ageBit + 8 : ageBit;
    }
    static uint AgeGenderMask(string age, string gender)
    {
        var ageBit = age.Trim().ToLowerInvariant() switch
        {
            "baby" => 0x01u, "toddler" => 0x02u, "child" => 0x04u, "teen" => 0x08u,
            "young adult" or "ya" => 0x10u, "adult" => 0x20u, "elder" => 0x40u, "infant" => 0x80u,
            _ => 0u
        };
        var gBit = string.Equals(gender, "Female", StringComparison.OrdinalIgnoreCase) ? 0x2000u
                 : string.Equals(gender, "Male",   StringComparison.OrdinalIgnoreCase) ? 0x1000u : 0u;
        return ageBit | gBit;
    }

    var skinIdx = CalcSkinIndex(ageLabel, genderLabel);
    var agm     = AgeGenderMask(ageLabel, genderLabel);
    var ageBit_ = agm & 0x0FFFu;
    var genBit_ = agm & 0xF000u;
    Console.WriteLine($"dump-skin-textures: skintone=0x{skintoneInst:X16}  age={ageLabel}  gender={genderLabel}");
    Console.WriteLine($"  skinIndex={skinIdx?.ToString() ?? "<none>"}  ageGenderMask=0x{agm:X4}");
    Console.WriteLine($"  output dir: {outputDir}");
    Directory.CreateDirectory(outputDir);

    var dumpCatalog = new LlamaResourceCatalogService();
    var dumpSource  = new DataSourceDefinition(
        Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"), "DumpSkin", searchRoot, SourceKind.Game);
    var dumpPackages = Directory.EnumerateFiles(searchRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase).ToArray();
    Console.WriteLine($"  scanning {dumpPackages.Length} package(s)...");

    // Build instance → list of (pkg, key) for every texture-typed copy. Multiple packages
    // can carry the same instance under DIFFERENT image type IDs (e.g. LRLEImage 0x2BC04EDF
    // alongside RLE2Image 0x3453CF95) and the catalog can decode some types and not others.
    // Mirror AssetServices.TryFetchImageByInstanceWithProbeAsync — try each candidate until
    // one decodes, instead of TryAdd-and-stop.
    var textureByInstance = new Dictionary<ulong, List<(string Pkg, ResourceKeyRecord Key)>>();
    var skintoneCopies    = new List<(string Pkg, ResourceMetadata Resource)>();
    foreach (var pkg in dumpPackages)
    {
        try
        {
            var s = await dumpCatalog.ScanPackageAsync(dumpSource, pkg, progress: null, CancellationToken.None);
            foreach (var r in s.Resources)
            {
                if (r.Key.TypeName is "DSTImage" or "PNGImage" or "PNGImage2"
                                   or "LRLEImage" or "RLE2Image" or "RLESImage"
                    || r.Key.Type == 0x00B2D882u)
                {
                    if (!textureByInstance.TryGetValue(r.Key.FullInstance, out var list))
                    {
                        list = new List<(string, ResourceKeyRecord)>();
                        textureByInstance[r.Key.FullInstance] = list;
                    }
                    list.Add((pkg, r.Key));
                }
                if (r.Key.FullInstance == skintoneInst && r.Key.TypeName == "Skintone")
                {
                    skintoneCopies.Add((pkg, r));
                }
            }
        }
        catch (Exception ex) { Console.Error.WriteLine($"  scan error: {pkg}: {ex.Message}"); }
    }
    Console.WriteLine($"  indexed instances with image candidates: {textureByInstance.Count:N0}  skintone copies: {skintoneCopies.Count}");

    // Pick a parseable v6 skintone copy.
    Ts4Skintone? skintoneToUse = null;
    foreach (var (pkg, res) in skintoneCopies)
    {
        try
        {
            var bytes = await dumpCatalog.GetResourceBytesAsync(pkg, res.Key, raw: false, CancellationToken.None, null);
            try
            {
                skintoneToUse = Ts4StructuredResourceMetadataExtractor.ParseSkintone(bytes);
                Console.WriteLine($"  parsed v{skintoneToUse.Version} skintone from: {pkg}");
                break;
            }
            catch { /* try next copy */ }
        }
        catch { /* try next copy */ }
    }
    if (skintoneToUse is null) { Console.WriteLine("  No parseable skintone copy. Aborting."); return 0; }

    // Build the request list: base + detail-neutral + detail-overlay + face-overlay.
    var requests = new List<(string Label, ulong Instance)>();
    requests.Add(("01_base_skin",       skintoneToUse.BaseTextureInstance));
    if (skinIdx is { } idx && idx >= 0 && idx < detailByIndex.Length && detailByIndex[idx] != 0)
        requests.Add(("02_detail_neutral", detailByIndex[idx]));
    if (skinIdx is { } idx2 && idx2 + 4 < detailByIndex.Length && detailByIndex[idx2 + 4] != 0)
        requests.Add(("03_detail_overlay", detailByIndex[idx2 + 4]));

    // Three-pass face-overlay match (faithful to AssetServices Fix B).
    Ts4SkintoneOverlay? matchedOverlay = null;
    string matchPath = "none";
    var requestMask = ageBit_ & genBit_;
    foreach (var ov in skintoneToUse.OverlayTextures)
    { if (ov.TypeValue == requestMask) { matchedOverlay = ov; matchPath = "strict"; break; } }
    if (matchedOverlay is null && genBit_ != 0u)
    {
        foreach (var ov in skintoneToUse.OverlayTextures)
        { if ((ov.TypeValue & genBit_) != 0u) { matchedOverlay = ov; matchPath = "gender-bit"; break; } }
    }
    if (matchedOverlay is null && skintoneToUse.OverlayTextures.Count > 0)
    { matchedOverlay = skintoneToUse.OverlayTextures[0]; matchPath = "first-overlay"; }
    if (matchedOverlay is not null)
    {
        Console.WriteLine($"  face overlay matched via {matchPath}: flags=0x{matchedOverlay.TypeValue:X4} inst=0x{matchedOverlay.TextureInstance:X16}");
        requests.Add(("04_face_overlay",  matchedOverlay.TextureInstance));
    }
    Console.WriteLine($"  OverlayOpacity={skintoneToUse.OverlayOpacity}  → atlas Pass2 mix={skintoneToUse.OverlayOpacity / 100f:0.###}");

    // Fetch each request as PNG and write to disk.
    Console.WriteLine();
    Console.WriteLine("  Writing PNGs:");
    foreach (var (label, inst) in requests)
    {
        if (inst == 0)
        {
            Console.WriteLine($"    {label}: instance=0 (skipped)");
            continue;
        }
        if (!textureByInstance.TryGetValue(inst, out var candidateList) || candidateList.Count == 0)
        {
            Console.WriteLine($"    {label}: 0x{inst:X16} NOT INDEXED");
            continue;
        }
        byte[]? png = null;
        string? winningPkg = null;
        ResourceKeyRecord? winningKey = null;
        string? lastError = null;
        foreach (var (pkg, key) in candidateList)
        {
            try
            {
                var bytes = await dumpCatalog.GetTexturePngAsync(pkg, key, CancellationToken.None, null);
                if (bytes is { Length: > 0 })
                {
                    png = bytes; winningPkg = pkg; winningKey = key; break;
                }
                lastError = $"{key.TypeName}(0x{key.Type:X8}) decoded to 0 bytes from {Path.GetFileName(pkg)}";
            }
            catch (Exception ex)
            {
                lastError = $"{key.TypeName}(0x{key.Type:X8}) {ex.Message} from {Path.GetFileName(pkg)}";
            }
        }
        if (png is null)
        {
            Console.WriteLine($"    {label}: 0x{inst:X16} all {candidateList.Count} candidate(s) failed; last: {lastError}");
            continue;
        }
        var path = Path.Combine(outputDir, $"{label}_{inst:X16}.png");
        await File.WriteAllBytesAsync(path, png, CancellationToken.None);
        int w = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        int h = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        Console.WriteLine($"    {label}: 0x{inst:X16}  {w}x{h}  {png.Length:N0} bytes  via {winningKey!.TypeName}  →  {Path.GetFileName(path)}");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-skintone", StringComparison.OrdinalIgnoreCase))
{
    // Loads a Skintone (TONE) resource by instance and runs the same three-pass face-overlay
    // lookup that AssetServices does (strict / gender-bit / first-overlay), reporting which
    // pass would match for a given Adult-Female, YA-Male, etc. age×gender pair.
    //
    // Usage: --probe-skintone <searchRoot> <instanceHex> [age=Adult|YoungAdult|Teen|Child|...] [gender=Female|Male]
    // Example: --probe-skintone "C:\GAMES\The Sims 4" 0000000000005545 Adult Female
    var searchRoot   = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    var instanceArg  = args.Length > 2 ? args[2] : null;
    var ageLabel     = args.Length > 3 ? args[3] : "Adult";
    var genderLabel  = args.Length > 4 ? args[4] : "Female";

    if (!Directory.Exists(searchRoot))
    {
        Console.Error.WriteLine($"Directory not found: {searchRoot}");
        return 1;
    }
    if (instanceArg is null)
    {
        Console.Error.WriteLine("Usage: --probe-skintone <searchRoot> <instanceHex> [age] [gender]");
        return 1;
    }
    var instanceStr = instanceArg.Contains(':') ? instanceArg.Split(':')[2] : instanceArg;
    if (!ulong.TryParse(instanceStr, System.Globalization.NumberStyles.HexNumber, null, out var skintoneInstance))
    {
        Console.Error.WriteLine($"Cannot parse instance hex: {instanceArg}");
        return 1;
    }

    // AgeGender enum values from TS4SimRipper Enums.cs:36-51 (verified ground truth).
    static uint AgeBit(string s) => s.ToLowerInvariant() switch
    {
        "baby"        => 0x00000001u,
        "toddler"     => 0x00000002u,
        "child"       => 0x00000004u,
        "teen"        => 0x00000008u,
        "youngadult" or "ya" => 0x00000010u,
        "adult"       => 0x00000020u,
        "elder"       => 0x00000040u,
        "infant"      => 0x00000080u,
        _ => 0u
    };
    static uint GenderBit(string s) => s.ToLowerInvariant() switch
    {
        "male"   => 0x00001000u,
        "female" => 0x00002000u,
        _ => 0u
    };
    var ageBit = AgeBit(ageLabel);
    var genderBit = GenderBit(genderLabel);
    var requestMask = ageBit & genderBit;
    Console.WriteLine($"probe-skintone: target instance=0x{skintoneInstance:X16}");
    Console.WriteLine($"  age={ageLabel} (0x{ageBit:X4})  gender={genderLabel} (0x{genderBit:X4})  request mask=0x{requestMask:X4}");

    var psCatalog = new LlamaResourceCatalogService();
    var psSource  = new DataSourceDefinition(
        Guid.Parse("99999999-9999-9999-9999-999999999999"),
        "ProbeSkintone",
        searchRoot,
        SourceKind.Game);

    var psPackages = Directory.EnumerateFiles(searchRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    Console.WriteLine($"  scanning {psPackages.Length} package(s)...");

    // Collect every Skintone copy of this instance across packages, plus the global texture
    // index for overlay-reachability checks. Multiple packages can carry the same instance
    // with different binary versions (e.g. v6 in SimulationFullBuild0, v12 in SimulationPreload);
    // the parser currently only accepts v6, so we try every copy until one parses.
    var skintoneCandidates = new List<(string Pkg, ResourceMetadata Resource)>();
    var indexedTextures = new HashSet<ulong>();

    foreach (var pkg in psPackages)
    {
        try
        {
            var psScan = await psCatalog.ScanPackageAsync(psSource, pkg, progress: null, CancellationToken.None);
            foreach (var resource in psScan.Resources)
            {
                if (resource.Key.TypeName is "DSTImage" or "PNGImage" or "PNGImage2"
                                          or "LRLEImage" or "RLE2Image" or "RLESImage")
                {
                    indexedTextures.Add(resource.Key.FullInstance);
                }
                if (resource.Key.FullInstance == skintoneInstance &&
                    resource.Key.TypeName == "Skintone")
                {
                    skintoneCandidates.Add((pkg, resource));
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  scan error in {pkg}: {ex.Message}");
        }
    }

    if (skintoneCandidates.Count == 0)
    {
        Console.WriteLine($"  Skintone 0x{skintoneInstance:X16} NOT FOUND in any package.");
        return 0;
    }

    Console.WriteLine($"  Found {skintoneCandidates.Count} Skintone cop{(skintoneCandidates.Count == 1 ? "y" : "ies")} of 0x{skintoneInstance:X16}:");
    Ts4Skintone? skintone = null;
    string? skintonePkg = null;
    foreach (var (pkg, resource) in skintoneCandidates)
    {
        try
        {
            var bytes = await psCatalog.GetResourceBytesAsync(pkg, resource.Key, raw: false, CancellationToken.None, null);
            var version = BitConverter.ToUInt32(bytes, 0);
            try
            {
                var parsed = Ts4StructuredResourceMetadataExtractor.ParseSkintone(bytes);
                Console.WriteLine($"    [v{version} OK]  {pkg}");
                if (skintone is null) { skintone = parsed; skintonePkg = pkg; }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    [v{version} FAIL: {ex.Message}]  {pkg}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [READ FAIL: {ex.Message}]  {pkg}");
        }
    }

    if (skintone is null)
    {
        Console.WriteLine("  No copy of this skintone parsed successfully — probe cannot run the lookup simulation.");
        return 0;
    }
    Console.WriteLine($"  Using parsed copy from: {skintonePkg}");

    Console.WriteLine($"  Version:             {skintone.Version}");
    Console.WriteLine($"  BaseTextureInstance: 0x{skintone.BaseTextureInstance:X16}  ({(indexedTextures.Contains(skintone.BaseTextureInstance) ? "INDEXED ✓" : "NOT FOUND ✗")})");
    var skinHue = (ushort)(skintone.Colorize >> 16);
    var skinSat = (ushort)(skintone.Colorize & 0xFFFF);
    Console.WriteLine($"  Colorize:            hue=0x{skinHue:X4} ({skinHue})  saturation=0x{skinSat:X4} ({skinSat})");
    Console.WriteLine($"  OverlayOpacity:      {skintone.OverlayOpacity}  (per SkinBlender pass2opacity = OverlayOpacity / 100 = {skintone.OverlayOpacity / 100f:0.###})");
    Console.WriteLine($"  TagCount:            {skintone.TagCount}");
    Console.WriteLine($"  MakeupOpacity:       {skintone.MakeupOpacity}");
    Console.WriteLine($"  DisplayIndex:        {skintone.DisplayIndex}");
    Console.WriteLine($"  Overlay table ({skintone.OverlayTextures.Count} entr{(skintone.OverlayTextures.Count == 1 ? "y" : "ies")}):");
    foreach (var ov in skintone.OverlayTextures)
    {
        var inst = ov.TextureInstance;
        var indexed = indexedTextures.Contains(inst);
        Console.WriteLine($"    flags=0x{ov.TypeValue:X4}  inst=0x{inst:X16}  {(indexed ? "[INDEXED ✓]" : "[NOT FOUND ✗]")}");
    }

    // Replay the AssetServices.cs three-pass lookup against this overlay table.
    Console.WriteLine();
    Console.WriteLine("  Three-pass face-overlay lookup simulation:");
    Ts4SkintoneOverlay? matched = null;
    string matchPath = "none";
    foreach (var ov in skintone.OverlayTextures)
    {
        if (ov.TypeValue == requestMask) { matched = ov; matchPath = "strict"; break; }
    }
    if (matched is null && genderBit != 0u)
    {
        foreach (var ov in skintone.OverlayTextures)
        {
            if ((ov.TypeValue & genderBit) != 0u) { matched = ov; matchPath = "gender-bit"; break; }
        }
    }
    if (matched is null && skintone.OverlayTextures.Count > 0)
    {
        matched = skintone.OverlayTextures[0]; matchPath = "first-overlay";
    }
    if (matched is not null)
    {
        var indexed = indexedTextures.Contains(matched.TextureInstance);
        Console.WriteLine($"    matched via {matchPath}: flags=0x{matched.TypeValue:X4} inst=0x{matched.TextureInstance:X16} {(indexed ? "[INDEXED ✓]" : "[NOT FOUND ✗]")}");
    }
    else
    {
        Console.WriteLine($"    no overlay matched (table is empty)");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-face-cas-bodytypes", StringComparison.OrdinalIgnoreCase))
{
    // Plan 3.4 — audit CAS body types 15..20 (Eyeliner, Lipstick, Mascara, Blush, SkinDetails,
    // Eyeshadow). For each body type, pick one representative CASPart, parse it, and report:
    //   - Number of LODs (geometry references) — > 0 means it has its own mesh
    //   - Number of texture references — > 0 means it has overlay textures
    //   - TgiList type breakdown — to see what kind of resources it references
    // Compare with bt=4 (EyeColor) and bt=14 (Brows) which are KNOWN atlas overlays.
    //
    // Usage: --probe-face-cas-bodytypes [outDir]
    var pfcOutDir = args.Length > 1 ? args[1] : Path.Combine("tmp", "face-cas-bodytypes");
    Directory.CreateDirectory(pfcOutDir);

    var dbPath = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    if (!File.Exists(dbPath)) { Console.Error.WriteLine($"DB not found: {dbPath}"); return 1; }
    using var pfcConn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
    pfcConn.Open();

    var pfcCatalog = new LlamaResourceCatalogService();
    var bodyTypeLabels = new Dictionary<int, string>
    {
        [4] = "EyeColor (currently treated as atlas overlay in code)",
        [14] = "Brows (currently treated as atlas overlay in code)",
        [29] = "Lipstick (per name distribution)",
        [30] = "Eyeshadow (per name distribution)",
        [31] = "Eyeliner (per name distribution)",
        [32] = "Blush (per name distribution)",
        [34] = "Brow / Eye-related (per name distribution)",
        [35] = "EyeColor (per name distribution)"
    };

    var report = new StringBuilder();
    report.AppendLine("CAS face body-type audit — geometry/texture composition\n");

    // First, find what body_type values are used by parts whose name contains the makeup terms.
    report.AppendLine("=== body_type distribution by makeup name pattern ===");
    var namePatterns = new[]
    {
        "Eyeliner", "Lipstick", "Mascara", "Blush", "SkinDetail", "Eyeshadow", "EyeColor", "Brow",
        "Lip", "Eye", "Makeup"
    };
    foreach (var pattern in namePatterns)
    {
        using var c = pfcConn.CreateCommand();
        c.CommandText = """
            SELECT f.body_type, COUNT(*) AS n
            FROM cas_part_facts f
            WHERE lower(f.internal_name) LIKE '%' || lower($pat) || '%'
            GROUP BY f.body_type
            ORDER BY n DESC
            LIMIT 5
            """;
        c.Parameters.AddWithValue("$pat", pattern);
        using var r = c.ExecuteReader();
        var hits = new List<string>();
        while (r.Read())
        {
            hits.Add($"bt={r.GetInt32(0)}({r.GetInt32(1)})");
        }
        report.AppendLine($"  '{pattern}': {string.Join(", ", hits)}");
    }
    report.AppendLine();

    foreach (var (bt, label) in bodyTypeLabels)
    {
        report.AppendLine($"=== body_type={bt} ({label}) ===");

        // Find one representative CASPart for this body type that we can actually load.
        string? targetTgi = null;
        string? targetPkg = null;
        string? targetName = null;
        using (var c = pfcConn.CreateCommand())
        {
            // Pick a sample whose name actually matches the expected category, when possible.
            // For bt=15-20 the catalog has many false positives (accessories using those slots);
            // a name pattern filter narrows to the actual makeup parts.
            var nameHint = bt switch
            {
                29 => "Lipstick",
                30 => "Eyeshadow",
                31 => "Eyeliner",
                32 => "Blush",
                34 => "Brow",
                35 => "EyeColor",
                _ => null
            };
            c.CommandText = nameHint is null
                ? """
                    SELECT a.root_tgi, f.internal_name, a.package_path
                    FROM assets a
                    JOIN cas_part_facts f ON f.root_tgi = a.root_tgi
                    WHERE f.body_type = $bt
                    LIMIT 1
                    """
                : """
                    SELECT a.root_tgi, f.internal_name, a.package_path
                    FROM assets a
                    JOIN cas_part_facts f ON f.root_tgi = a.root_tgi
                    WHERE f.body_type = $bt
                      AND lower(f.internal_name) LIKE '%' || lower($name) || '%'
                    LIMIT 1
                    """;
            c.Parameters.AddWithValue("$bt", bt);
            if (nameHint is not null)
                c.Parameters.AddWithValue("$name", nameHint);
            using var r = c.ExecuteReader();
            if (r.Read())
            {
                targetTgi = r.GetString(0);
                targetName = r.IsDBNull(1) ? "(no name)" : r.GetString(1);
                targetPkg = r.IsDBNull(2) ? null : r.GetString(2);
            }
        }
        if (targetTgi is null || targetPkg is null)
        {
            report.AppendLine($"  No CASPart found for body_type={bt}.\n");
            continue;
        }
        report.AppendLine($"  Sample CASPart: {targetName}");
        report.AppendLine($"  TGI:            {targetTgi}");
        report.AppendLine($"  Package:        {targetPkg}");

        var parts = targetTgi.Split(':');
        if (parts.Length != 3) { report.AppendLine("  (invalid TGI format)\n"); continue; }
        if (!uint.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, null, out var typeId) ||
            !uint.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, null, out var groupId) ||
            !ulong.TryParse(parts[2], System.Globalization.NumberStyles.HexNumber, null, out var instId))
        {
            report.AppendLine("  (TGI parse failed)\n"); continue;
        }
        var resKey = new ResourceKeyRecord(typeId, groupId, instId, "CASPart");

        try
        {
            var bytes = await pfcCatalog.GetResourceBytesAsync(targetPkg, resKey, raw: false, CancellationToken.None, null);
            var casPart = Ts4CasPart.Parse(bytes);
            report.AppendLine($"  BodyType:       {casPart.BodyType}");
            report.AppendLine($"  PresetCount:    {casPart.PresetCount}");
            report.AppendLine($"  LODs:           {casPart.Lods.Count}");
            report.AppendLine($"  TextureRefs:    {casPart.TextureReferences.Count}");
            report.AppendLine($"  SwatchColors:   {casPart.SwatchColors.Count}");
            report.AppendLine($"  TgiList ({casPart.TgiList.Count} entries):");
            var typeBreakdown = casPart.TgiList
                .GroupBy(static t => t.TypeName)
                .OrderByDescending(static g => g.Count())
                .ToArray();
            foreach (var group in typeBreakdown)
            {
                report.AppendLine($"    {group.Count(),3} × {group.Key}");
            }
            // Verdict
            var verdict = casPart.Lods.Count > 0
                ? "HAS GEOMETRY → needs its own mesh layer (NOT a flat atlas overlay)"
                : casPart.TextureReferences.Count > 0
                    ? "TEXTURE-ONLY → flat overlay (compatible with skin atlas compositor)"
                    : "INDETERMINATE → no LODs and no textures (check TgiList types)";
            report.AppendLine($"  VERDICT: {verdict}");
        }
        catch (Exception ex)
        {
            report.AppendLine($"  Parse error: {ex.Message}");
        }
        report.AppendLine();
    }

    var reportPath = Path.Combine(pfcOutDir, "audit.txt");
    await File.WriteAllTextAsync(reportPath, report.ToString());
    Console.WriteLine(report);
    Console.WriteLine($"audit -> {reportPath}");
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--scan-smods", StringComparison.OrdinalIgnoreCase))
{
    // Enumerate SimModifier (SMOD) resources in EA's data and report parser success +
    // BOND/DMap reference distribution. Verifies our parser AND tells us how many SMODs
    // actually carry a BOND we'd need to apply.
    var ssRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4\Data";
    var ssCatalog = new LlamaResourceCatalogService();
    var ssSource = new DataSourceDefinition(
        Guid.Parse("11111111-2222-3333-4444-555555555555"),
        "ScanSmods",
        ssRoot,
        SourceKind.Game);
    var ssPackages = Directory.EnumerateFiles(ssRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    Console.WriteLine($"scan-smods: scanning {ssPackages.Length} package(s)");
    var totalSmods = 0;
    var parsed = 0;
    var parseFailed = 0;
    var withBond = 0;
    var withShapeMap = 0;
    var withNormalMap = 0;
    var sampleFailures = new List<string>();
    foreach (var pkg in ssPackages)
    {
        try
        {
            var ssScan = await ssCatalog.ScanPackageAsync(ssSource, pkg, progress: null, CancellationToken.None);
            foreach (var resource in ssScan.Resources)
            {
                if (resource.Key.Type != 0xC5F6763Eu) continue;
                totalSmods++;
                try
                {
                    var bytes = await ssCatalog.GetResourceBytesAsync(pkg, resource.Key, raw: false, CancellationToken.None, null);
                    var smod = Sims4ResourceExplorer.Packages.Ts4SimModifierResource.Parse(bytes);
                    parsed++;
                    if (smod.HasBondReference) withBond++;
                    if (smod.HasShapeDeformerMap) withShapeMap++;
                    if (smod.HasNormalDeformerMap) withNormalMap++;
                }
                catch (Exception ex)
                {
                    parseFailed++;
                    if (sampleFailures.Count < 5) sampleFailures.Add($"{resource.Key.FullTgi}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  scan error in {pkg}: {ex.Message}");
        }
    }
    Console.WriteLine($"  Total SMODs:           {totalSmods:N0}");
    Console.WriteLine($"  Parsed successfully:   {parsed:N0}");
    Console.WriteLine($"  Parse failures:        {parseFailed:N0}");
    Console.WriteLine($"  With BOND reference:   {withBond:N0}");
    Console.WriteLine($"  With shape DMap:       {withShapeMap:N0}");
    Console.WriteLine($"  With normal DMap:      {withNormalMap:N0}");
    if (sampleFailures.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("  Sample failures:");
        foreach (var f in sampleFailures) Console.WriteLine($"    {f}");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-modifier-tgis", StringComparison.OrdinalIgnoreCase))
{
    // Probe a SimInfo's body+face modifier TGIs after our parser update. Tells us:
    //   - What resource TYPES are referenced (BOND vs DMap vs SimModifier vs other)
    //   - Sample instance IDs so we can manually probe one and confirm the format
    var pmtTgi = args.Length > 1 ? args[1] : "025ED6F4:00000000:369CA7F9DE882B52";
    var dbPath = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    if (!File.Exists(dbPath)) { Console.Error.WriteLine($"DB not found: {dbPath}"); return 1; }

    using var pmtConn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
    pmtConn.Open();

    string? pkgPath = null;
    using (var c = pmtConn.CreateCommand())
    {
        c.CommandText = "SELECT package_path FROM assets WHERE root_tgi = $tgi LIMIT 1";
        c.Parameters.AddWithValue("$tgi", pmtTgi);
        using var r = c.ExecuteReader();
        if (r.Read()) pkgPath = r.GetString(0);
    }
    if (pkgPath is null) { Console.Error.WriteLine($"SimInfo not found in cache: {pmtTgi}"); return 1; }

    var parts = pmtTgi.Split(':');
    var resKey = new ResourceKeyRecord(
        uint.Parse(parts[0], System.Globalization.NumberStyles.HexNumber),
        uint.Parse(parts[1], System.Globalization.NumberStyles.HexNumber),
        ulong.Parse(parts[2], System.Globalization.NumberStyles.HexNumber),
        "SimInfo");
    var pmtCatalog = new LlamaResourceCatalogService();
    var bytes = await pmtCatalog.GetResourceBytesAsync(pkgPath, resKey, raw: false, CancellationToken.None, null);
    var sim = Sims4ResourceExplorer.Assets.Ts4SimInfoParser.Parse(bytes);

    Console.WriteLine($"SimInfo: {pmtTgi} ({sim.SpeciesLabel} {sim.AgeLabel} {sim.GenderLabel})");
    Console.WriteLine($"FaceModifiers: {sim.FaceModifierCount}, BodyModifiers: {sim.BodyModifierCount}");
    Console.WriteLine($"GeneticFaceModifiers: {sim.GeneticFaceModifierCount}, GeneticBodyModifiers: {sim.GeneticBodyModifierCount}");
    Console.WriteLine();

    void Dump(string label, IReadOnlyList<Sims4ResourceExplorer.Assets.Ts4SimModifierEntry> entries)
    {
        Console.WriteLine($"=== {label} ({entries.Count}) ===");
        foreach (var entry in entries.Take(20))
        {
            var key = entry.ModifierKey;
            var keyStr = key is null ? "(unresolved)" : $"{key.FullTgi} ({key.TypeName ?? $"0x{key.Type:X8}"})";
            Console.WriteLine($"  ch={entry.ChannelId,3}  weight={entry.Value,8:0.######}  key={keyStr}");
        }
    }
    Dump("FaceModifiers", sim.FaceModifiers);
    Dump("BodyModifiers", sim.BodyModifiers);
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-siminfo-trace", StringComparison.OrdinalIgnoreCase))
{
    // Inline mirror of Ts4SimInfoParser that prints the byte position before every major
    // section header. Use this to find the stride misalignment that's making BOND modifier
    // weights come back as 1e8 to 1e38 magnitudes for v21 SimInfos.
    var pstTgi = args.Length > 1 ? args[1] : "025ED6F4:00000000:386F5C479E2AE7FF";
    var pstDb = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    if (!File.Exists(pstDb)) { Console.Error.WriteLine($"DB not found: {pstDb}"); return 1; }
    string? pstPkg = null;
    using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={pstDb};Mode=ReadOnly"))
    {
        conn.Open();
        using var c = conn.CreateCommand();
        c.CommandText = "SELECT package_path FROM assets WHERE root_tgi = $tgi LIMIT 1";
        c.Parameters.AddWithValue("$tgi", pstTgi);
        using var r = c.ExecuteReader();
        if (r.Read()) pstPkg = r.GetString(0);
    }
    if (pstPkg is null) { Console.Error.WriteLine($"SimInfo {pstTgi} not in cache."); return 1; }

    var pstParts = pstTgi.Split(':');
    var pstKey = new ResourceKeyRecord(
        Convert.ToUInt32(pstParts[0], 16),
        Convert.ToUInt32(pstParts[1], 16),
        Convert.ToUInt64(pstParts[2], 16),
        "SimInfo");
    var pstCatalog = new LlamaResourceCatalogService();
    var pstBytes = await pstCatalog.GetResourceBytesAsync(pstPkg, pstKey, raw: false, CancellationToken.None, null);
    Console.WriteLine($"probe-siminfo-trace: {pstTgi}  ({pstBytes.Length} bytes)");
    Console.WriteLine($"  Package: {pstPkg}");

    using var pstStream = new MemoryStream(pstBytes, writable: false);
    using var pstReader = new BinaryReader(pstStream, System.Text.Encoding.UTF8, leaveOpen: true);

    void Trace(string label) => Console.WriteLine($"  pos=0x{pstStream.Position:X4} ({pstStream.Position,5})  {label}");

    Trace("start");
    var pstVersion = pstReader.ReadUInt32();
    Trace($"version={pstVersion}");
    var pstLinkTableOffset = pstReader.ReadUInt32();
    Trace($"linkTableOffset={pstLinkTableOffset} (links at 0x{8 + pstLinkTableOffset:X4})");

    Trace("about to skip 8 floats (physique)");
    pstStream.Position += 32;
    Trace("after physique");
    var pstAge = pstReader.ReadUInt32();
    Trace($"ageFlags=0x{pstAge:X8}");
    var pstGender = pstReader.ReadUInt32();
    Trace($"genderFlags=0x{pstGender:X8}");
    if (pstVersion > 18)
    {
        var pstSpecies = pstReader.ReadUInt32();
        Trace($"species={pstSpecies}");
        var pstUnknown1 = pstReader.ReadUInt32();
        Trace($"unknown1=0x{pstUnknown1:X8}");
    }
    if (pstVersion >= 32)
    {
        var pstPronouns = pstReader.ReadInt32();
        Trace($"pronounCount={pstPronouns}");
        for (var i = 0; i < pstPronouns; i++)
        {
            var caseValue = pstReader.ReadUInt32();
            if (caseValue > 0)
            {
                _ = pstReader.ReadString();
            }
        }
        Trace("after pronouns");
    }
    var pstSkintone = pstReader.ReadUInt64();
    Trace($"skintoneInstance=0x{pstSkintone:X16}");
    if (pstVersion >= 28)
    {
        var pstShift = pstReader.ReadSingle();
        Trace($"skintoneShift={pstShift}");
    }
    if (pstVersion > 19)
    {
        var pstPelt = pstReader.ReadByte();
        Trace($"peltLayerCount={pstPelt}");
        for (var i = 0; i < pstPelt; i++)
        {
            var lr = pstReader.ReadUInt64();
            var col = pstReader.ReadUInt32();
            Trace($"  pelt[{i}] ref=0x{lr:X16} color=0x{col:X8}");
        }
    }
    var pstSculpt = pstReader.ReadByte();
    Trace($"sculptCount={pstSculpt}");
    for (var i = 0; i < pstSculpt; i++)
    {
        var idx = pstReader.ReadByte();
        Trace($"  sculpt[{i}] linkIndex={idx}");
    }

    var pstFmcPos = pstStream.Position;
    var pstFmc = pstReader.ReadByte();
    Trace($"FACE modifier count = {pstFmc} (at byte 0x{pstFmcPos:X4})");

    // Hex-dump the next pstFmc * 5 bytes (assuming 5 bytes per modifier) and show what we'd
    // read as (linkIndex, weight) at each stride.
    var pstFmStart = pstStream.Position;
    var pstFmStride5Bytes = Math.Min((long)pstFmc * 5, pstStream.Length - pstFmStart);
    var pstFmHex = new byte[pstFmStride5Bytes];
    pstStream.Read(pstFmHex, 0, (int)pstFmStride5Bytes);
    pstStream.Position = pstFmStart;

    Console.WriteLine();
    Console.WriteLine($"  Raw face-modifier window (first {Math.Min(80, pstFmStride5Bytes)} bytes from 0x{pstFmStart:X4}):");
    var pstFmShow = (int)Math.Min(80, pstFmStride5Bytes);
    for (var off = 0; off < pstFmShow; off += 16)
    {
        var len = Math.Min(16, pstFmShow - off);
        Console.WriteLine($"    0x{pstFmStart + off:X4}  {Convert.ToHexString(pstFmHex.AsSpan(off, len))}");
    }

    Console.WriteLine();
    Console.WriteLine("  STRIDE-5 (current parser): linkIndex byte + weight float");
    for (var i = 0; i < Math.Min(16, (int)pstFmc); i++)
    {
        var off = i * 5;
        if (off + 5 > pstFmHex.Length) break;
        var li = pstFmHex[off];
        var w = BitConverter.ToSingle(pstFmHex, off + 1);
        Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"    [{i,3}] linkIdx={li,3}  weightBytes={Convert.ToHexString(pstFmHex.AsSpan(off + 1, 4))}  weight={w:0.######}"));
    }

    // Also try alternative strides to see if any give sane weights.
    Console.WriteLine();
    Console.WriteLine("  STRIDE-9 hypothesis: linkIndex byte + extra uint32 + weight float");
    for (var i = 0; i < Math.Min(16, (int)pstFmc); i++)
    {
        var off = i * 9;
        if (off + 9 > pstFmHex.Length) break;
        var li = pstFmHex[off];
        var extra = BitConverter.ToUInt32(pstFmHex, off + 1);
        var w = BitConverter.ToSingle(pstFmHex, off + 5);
        Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"    [{i,3}] linkIdx={li,3}  extra=0x{extra:X8}  weight={w:0.######}"));
    }

    Console.WriteLine();
    Console.WriteLine("  STRIDE-6 hypothesis: linkIndex byte + weight float + tail byte");
    for (var i = 0; i < Math.Min(16, (int)pstFmc); i++)
    {
        var off = i * 6;
        if (off + 6 > pstFmHex.Length) break;
        var li = pstFmHex[off];
        var w = BitConverter.ToSingle(pstFmHex, off + 1);
        var tail = pstFmHex[off + 5];
        Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"    [{i,3}] linkIdx={li,3}  weight={w:0.######}  tail=0x{tail:X2}"));
    }

    Console.WriteLine();
    Console.WriteLine("  STRIDE-5 with BIG-ENDIAN float weights:");
    var pstWb = new byte[4];
    for (var i = 0; i < Math.Min(16, (int)pstFmc); i++)
    {
        var off = i * 5;
        if (off + 5 > pstFmHex.Length) break;
        var li = pstFmHex[off];
        pstWb[0] = pstFmHex[off + 4];
        pstWb[1] = pstFmHex[off + 3];
        pstWb[2] = pstFmHex[off + 2];
        pstWb[3] = pstFmHex[off + 1];
        var w = BitConverter.ToSingle(pstWb, 0);
        Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"    [{i,3}] linkIdx={li,3}  weightBytes={Convert.ToHexString(pstFmHex.AsSpan(off + 1, 4))}  weight(BE)={w:0.######}"));
    }

    // Walk the face modifier section using stride-5 with BE-float to find where the body
    // modifier section actually starts.
    var pstFmEndStride5 = pstFmStart + (long)pstFmc * 5;
    Console.WriteLine();
    Console.WriteLine($"  Face section under stride-5 spans 0x{pstFmStart:X4}..0x{pstFmEndStride5:X4} ({(long)pstFmc * 5} bytes)");
    Console.WriteLine($"  Link table starts at 0x{8 + pstLinkTableOffset:X4}; payload available between face section start and link table = {(8 + pstLinkTableOffset) - pstFmStart} bytes");
    Console.WriteLine($"  Face section at 5 bytes/entry needs {pstFmc * 5} bytes — { (pstFmc * 5 <= (8 + pstLinkTableOffset) - pstFmStart ? "fits" : "OVERFLOWS")}");

    // Try reading bodyModifierCount immediately after a stride-5 face section ends.
    if (pstFmEndStride5 < pstBytes.Length)
    {
        var bodyAtStride5 = pstBytes[(int)pstFmEndStride5];
        Console.WriteLine($"  Byte at hypothesized body-mod-count position 0x{pstFmEndStride5:X4} = 0x{bodyAtStride5:X2} (={bodyAtStride5})");
        // Hex window after the face section.
        Console.WriteLine($"  Hex window starting at 0x{pstFmEndStride5:X4} (96 bytes):");
        var winLen = Math.Min(96, pstBytes.Length - (int)pstFmEndStride5);
        for (var off = 0; off < winLen; off += 16)
        {
            var len = Math.Min(16, winLen - off);
            Console.WriteLine($"    0x{pstFmEndStride5 + off:X4}  {Convert.ToHexString(pstBytes.AsSpan((int)(pstFmEndStride5 + off), len))}");
        }

        // Decode first 16 body modifiers under both stride-5 BE-float and stride-5 LE-float.
        var bodyStart = (int)pstFmEndStride5 + 1;
        Console.WriteLine();
        Console.WriteLine($"  Body modifiers (first 16, BE-float at stride 5 starting at 0x{bodyStart:X4}):");
        for (var i = 0; i < Math.Min(16, (int)bodyAtStride5); i++)
        {
            var off = bodyStart + i * 5;
            if (off + 5 > pstBytes.Length) break;
            var li = pstBytes[off];
            var wb = new byte[4];
            wb[0] = pstBytes[off + 4];
            wb[1] = pstBytes[off + 3];
            wb[2] = pstBytes[off + 2];
            wb[3] = pstBytes[off + 1];
            var w = BitConverter.ToSingle(wb, 0);
            Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"    [{i,3}] linkIdx={li,3}  weightBytes={Convert.ToHexString(pstBytes.AsSpan(off + 1, 4))}  weight(BE)={w:0.######}"));
        }
    }

    // Sub-hypothesis: ALL of pelt+sculpt+face+body modifiers form a continuous 5-byte record
    // stream starting from 0x66 (after sculptCount) with counter incrementing from 0x0D.
    // Record N starts at 0x66 + 5N, counter byte at 0x67 + 5N.
    // Let's find where the counter pattern actually breaks (records ending) to deduce total.
    Console.WriteLine();
    Console.WriteLine("  Walking 5-byte records from 0x66 with counter at byte+1:");
    var recStart = 0x66;
    var recIdx = 0;
    var lastSaneCounter = -1;
    while (recStart + 5 <= pstBytes.Length)
    {
        var counter = pstBytes[recStart + 1];
        var expected = (byte)(0x0D + recIdx);
        if (counter != expected)
        {
            Console.WriteLine($"    Counter pattern BREAKS at record {recIdx} (pos 0x{recStart:X4}): expected 0x{expected:X2}, got 0x{counter:X2}");
            break;
        }
        lastSaneCounter = counter;
        recIdx++;
        recStart += 5;
        if (recIdx > 200) { Console.WriteLine($"    Reached record {recIdx} without break - capping"); break; }
    }
    Console.WriteLine($"    {recIdx} contiguous 5-byte records (counters 0x0D-0x{lastSaneCounter:X2}). Section ends at 0x{recStart:X4}.");
    Console.WriteLine($"    sculptCount={28}, face/body would be {recIdx - 28} = {recIdx} - 28");

    // What 32-bit value sits in the bytes immediately after this section?
    if (recStart + 4 <= pstBytes.Length)
    {
        var afterSec = BitConverter.ToUInt32(pstBytes, recStart);
        Console.WriteLine($"    UInt32 LE at 0x{recStart:X4} = 0x{afterSec:X8} ({afterSec})");
    }

    // If sculptCount=28 and remaining = (recIdx-28) modifiers, what are the RECORD weights?
    Console.WriteLine();
    Console.WriteLine("  All 66 records decoded (linkIdx + BE-float weight):");
    var allWb = new byte[4];
    var sanity = 0;
    for (var i = 0; i < recIdx; i++)
    {
        var off = 0x66 + i * 5;
        var li = pstBytes[off];
        allWb[0] = pstBytes[off + 4];
        allWb[1] = pstBytes[off + 3];
        allWb[2] = pstBytes[off + 2];
        allWb[3] = pstBytes[off + 1];
        var w = BitConverter.ToSingle(allWb, 0);
        var role = i < 28 ? "sculpt" : (i < 28 + ((recIdx - 28) / 2) ? "face?" : "body?");
        var ok = float.IsFinite(w) && Math.Abs(w) <= 2 ? "ok" : "BAD";
        if (ok == "ok") sanity++;
        Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"    [{i,3}] {role,-7}  byte0=0x{li:X2}  weight(BE)={w:0.######}  {ok}"));
    }
    Console.WriteLine($"  Sane weights (|w| <= 2): {sanity} of {recIdx}");

    // Hypothesis: sculpts are 5-byte records (linkIdx + BE float). 28 sculpts × 5 bytes ends at
    // 0x66 + 140 = 0xF6. Face modifier count then sits at 0xF6.
    var pstSculptStart = 0x66;
    var pstSculptStride5End = pstSculptStart + 28 * 5;  // = 0xF6
    Console.WriteLine();
    Console.WriteLine($"  Hypothesis: sculpts at 5 bytes/entry → ends at 0x{pstSculptStride5End:X4}");
    if (pstSculptStride5End < pstBytes.Length)
    {
        var altFmCount = pstBytes[pstSculptStride5End];
        Console.WriteLine($"  Hypothesized face-mod-count byte at 0x{pstSculptStride5End:X4} = 0x{altFmCount:X2} ({altFmCount})");
        var altFmEnd = pstSculptStride5End + 1 + altFmCount * 5;
        Console.WriteLine($"  If face-count={altFmCount} and stride=5 → face section ends at 0x{altFmEnd:X4}");
        if (altFmEnd < pstBytes.Length)
        {
            var altBmCount = pstBytes[altFmEnd];
            Console.WriteLine($"  Hypothesized body-mod-count byte at 0x{altFmEnd:X4} = 0x{altBmCount:X2} ({altBmCount})");
            var altBmEnd = altFmEnd + 1 + altBmCount * 5;
            Console.WriteLine($"  If body-count={altBmCount} → body section ends at 0x{altBmEnd:X4}");
            // Decode first 10 face modifiers under this hypothesis.
            Console.WriteLine($"  First 10 face modifiers under stride-5 BE-float, starting at 0x{pstSculptStride5End + 1:X4}:");
            var altFmStart = pstSculptStride5End + 1;
            var altWb = new byte[4];
            for (var i = 0; i < Math.Min(10, (int)altFmCount); i++)
            {
                var off = altFmStart + i * 5;
                if (off + 5 > pstBytes.Length) break;
                var li = pstBytes[off];
                altWb[0] = pstBytes[off + 4];
                altWb[1] = pstBytes[off + 3];
                altWb[2] = pstBytes[off + 2];
                altWb[3] = pstBytes[off + 1];
                var w = BitConverter.ToSingle(altWb, 0);
                Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"    [{i,3}] linkIdx={li,3}  weight(BE)={w:0.######}"));
            }
            // Decode first 10 body modifiers under this hypothesis.
            Console.WriteLine($"  First 10 body modifiers under stride-5 BE-float, starting at 0x{altFmEnd + 1:X4}:");
            var altBmStart = altFmEnd + 1;
            for (var i = 0; i < Math.Min(10, (int)altBmCount); i++)
            {
                var off = altBmStart + i * 5;
                if (off + 5 > pstBytes.Length) break;
                var li = pstBytes[off];
                altWb[0] = pstBytes[off + 4];
                altWb[1] = pstBytes[off + 3];
                altWb[2] = pstBytes[off + 2];
                altWb[3] = pstBytes[off + 1];
                var w = BitConverter.ToSingle(altWb, 0);
                Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"    [{i,3}] linkIdx={li,3}  weight(BE)={w:0.######}"));
            }
        }
    }
    Console.WriteLine();
    Console.WriteLine("  Full hex 0x60-0x300:");
    var pstSculptDumpEnd = Math.Min(0x300, pstBytes.Length);
    for (var off = 0x60; off < pstSculptDumpEnd; off += 16)
    {
        var len = Math.Min(16, pstBytes.Length - off);
        Console.WriteLine($"    0x{off:X4}  {Convert.ToHexString(pstBytes.AsSpan(off, len))}");
    }

    // Dump prelude bytes 0x00-0x90 so we can verify upstream alignment.
    Console.WriteLine();
    Console.WriteLine("  Prelude hex (0x00-0x90):");
    for (var off = 0; off < 0x90 && off < pstBytes.Length; off += 16)
    {
        var len = Math.Min(16, pstBytes.Length - off);
        Console.WriteLine($"    0x{off:X4}  {Convert.ToHexString(pstBytes.AsSpan(off, len))}");
    }

    // Dump link-table block.
    var pstLinkPos = (int)(8 + pstLinkTableOffset);
    Console.WriteLine();
    Console.WriteLine($"  Link-table block starts at 0x{pstLinkPos:X4}, file end at 0x{pstBytes.Length:X4}, available bytes={pstBytes.Length - pstLinkPos}");
    if (pstLinkPos < pstBytes.Length)
    {
        var linkCount = pstBytes[pstLinkPos];
        var availableForEntries = pstBytes.Length - pstLinkPos - 1;
        var entriesPossible16 = availableForEntries / 16;
        Console.WriteLine($"  linkCount byte = {linkCount}  (capacity at 16 bytes/entry = {entriesPossible16})");

        Console.WriteLine($"  Link-table hex (first 80 bytes from 0x{pstLinkPos:X4}):");
        for (var off = 0; off < 80 && pstLinkPos + off < pstBytes.Length; off += 16)
        {
            var len = Math.Min(16, pstBytes.Length - pstLinkPos - off);
            Console.WriteLine($"    0x{pstLinkPos + off:X4}  {Convert.ToHexString(pstBytes.AsSpan(pstLinkPos + off, len))}");
        }

        // Decode first 5 link entries assuming IGT order (instance 8 + group 4 + type 4)
        Console.WriteLine($"  First 8 link entries decoded as IGT (instance UInt64 + group UInt32 + type UInt32):");
        for (var i = 0; i < Math.Min(8, (int)linkCount); i++)
        {
            var entryStart = pstLinkPos + 1 + i * 16;
            if (entryStart + 16 > pstBytes.Length) break;
            var inst = BitConverter.ToUInt64(pstBytes, entryStart);
            var grp = BitConverter.ToUInt32(pstBytes, entryStart + 8);
            var typ = BitConverter.ToUInt32(pstBytes, entryStart + 12);
            Console.WriteLine($"    [{i,3}] type=0x{typ:X8}  group=0x{grp:X8}  instance=0x{inst:X16}");
        }
    }

    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--scan-bgeos", StringComparison.OrdinalIgnoreCase))
{
    // Enumerate every BlendGeometry (BGEO) resource in EA's data and report parse stats.
    // BGEO type ID per --find-instance verification: 0x067CAA11.
    var sbgRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4\Data";
    var sbgCatalog = new LlamaResourceCatalogService();
    var sbgSource = new DataSourceDefinition(
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
        "ScanBGEOs",
        sbgRoot,
        SourceKind.Game);
    var sbgPackages = Directory.EnumerateFiles(sbgRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    Console.WriteLine($"scan-bgeos: scanning {sbgPackages.Length} package(s)");

    var totalBgeos = 0;
    var parsed = 0;
    var parseFailed = 0;
    var sampleFailures = new List<string>();
    var lodCounts = new Dictionary<int, int>();
    long totalVertices = 0;
    long totalVectors = 0;
    foreach (var pkg in sbgPackages)
    {
        try
        {
            var sbgScan = await sbgCatalog.ScanPackageAsync(sbgSource, pkg, progress: null, CancellationToken.None);
            foreach (var resource in sbgScan.Resources)
            {
                if (resource.Key.Type != 0x067CAA11u) continue;
                totalBgeos++;
                try
                {
                    var bytes = await sbgCatalog.GetResourceBytesAsync(pkg, resource.Key, raw: false, CancellationToken.None, null);
                    if (totalBgeos <= 3)
                    {
                        Console.WriteLine($"  Sample BGEO #{totalBgeos}: {resource.Key.FullTgi}, {bytes.Length} bytes");
                    }
                    var bgeo = Sims4ResourceExplorer.Packages.Ts4BlendGeometryResource.Parse(bytes);
                    parsed++;
                    lodCounts[bgeo.Lods.Count] = lodCounts.GetValueOrDefault(bgeo.Lods.Count) + 1;
                    totalVertices += bgeo.BlendMap.Count;
                    totalVectors += bgeo.VectorData.Count;
                }
                catch (Exception ex)
                {
                    parseFailed++;
                    if (sampleFailures.Count < 5) sampleFailures.Add($"{resource.Key.FullTgi}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  scan error in {pkg}: {ex.Message}");
        }
    }
    Console.WriteLine($"  Total BGEOs:           {totalBgeos:N0}");
    Console.WriteLine($"  Parsed successfully:   {parsed:N0}");
    Console.WriteLine($"  Parse failures:        {parseFailed:N0}");
    Console.WriteLine($"  Total blend-map vertices: {totalVertices:N0}");
    Console.WriteLine($"  Total delta vectors:      {totalVectors:N0}");
    Console.WriteLine();
    Console.WriteLine("  LOD count histogram:");
    foreach (var (l, c) in lodCounts.OrderBy(static kv => kv.Key))
        Console.WriteLine($"    {l} LODs: {c:N0}");
    if (sampleFailures.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("  Sample failures:");
        foreach (var f in sampleFailures) Console.WriteLine($"    {f}");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--scan-dmaps", StringComparison.OrdinalIgnoreCase))
{
    // Enumerate every DeformerMap (DMap) resource in EA's data, parse it, and report parse
    // success + width/height/scan-line distribution. DMaps drive per-vertex face/body shape
    // morphs via UV1 sampling — this is the missing piece for visible face shape changes.
    // DMap resource type ID per TS4 codex-wiki: 0xDB43E069.
    var sdmRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4\Data";
    var sdmCatalog = new LlamaResourceCatalogService();
    var sdmSource = new DataSourceDefinition(
        Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
        "ScanDMaps",
        sdmRoot,
        SourceKind.Game);
    var sdmPackages = Directory.EnumerateFiles(sdmRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    Console.WriteLine($"scan-dmaps: scanning {sdmPackages.Length} package(s)");

    var totalDmaps = 0;
    var parsed = 0;
    var parseFailed = 0;
    var sampleFailures = new List<string>();
    var versionCounts = new Dictionary<uint, int>();
    var scanLineCounts = new Dictionary<int, int>();
    long totalScanLines = 0;
    foreach (var pkg in sdmPackages)
    {
        try
        {
            var sdmScan = await sdmCatalog.ScanPackageAsync(sdmSource, pkg, progress: null, CancellationToken.None);
            foreach (var resource in sdmScan.Resources)
            {
                if (resource.Key.Type != 0xDB43E069u) continue;
                totalDmaps++;
                try
                {
                    var bytes = await sdmCatalog.GetResourceBytesAsync(pkg, resource.Key, raw: false, CancellationToken.None, null);
                    if (totalDmaps <= 3)
                    {
                        Console.WriteLine($"  Sample DMap #{totalDmaps}: {resource.Key.FullTgi}, {bytes.Length} bytes");
                    }
                    var dmap = Sims4ResourceExplorer.Packages.Ts4DeformerMapResource.Parse(bytes);
                    parsed++;
                    versionCounts[dmap.Version] = versionCounts.GetValueOrDefault(dmap.Version) + 1;
                    var slBucket = dmap.ScanLines.Count switch
                    {
                        0 => 0,
                        < 10 => 10,
                        < 50 => 50,
                        < 100 => 100,
                        < 500 => 500,
                        _ => 1000
                    };
                    scanLineCounts[slBucket] = scanLineCounts.GetValueOrDefault(slBucket) + 1;
                    totalScanLines += dmap.ScanLines.Count;
                }
                catch (Exception ex)
                {
                    parseFailed++;
                    if (sampleFailures.Count < 5) sampleFailures.Add($"{resource.Key.FullTgi}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  scan error in {pkg}: {ex.Message}");
        }
    }
    Console.WriteLine($"  Total DMaps:           {totalDmaps:N0}");
    Console.WriteLine($"  Parsed successfully:   {parsed:N0}");
    Console.WriteLine($"  Parse failures:        {parseFailed:N0}");
    Console.WriteLine($"  Total scan lines:      {totalScanLines:N0}");
    Console.WriteLine();
    Console.WriteLine("  Versions seen:");
    foreach (var (v, c) in versionCounts.OrderBy(static kv => kv.Key))
        Console.WriteLine($"    v{v}: {c:N0}");
    Console.WriteLine();
    Console.WriteLine("  Scan-line count buckets:");
    foreach (var (b, c) in scanLineCounts.OrderBy(static kv => kv.Key))
        Console.WriteLine($"    < {b}: {c:N0}");
    if (sampleFailures.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("  Sample failures:");
        foreach (var f in sampleFailures) Console.WriteLine($"    {f}");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--scan-bonds", StringComparison.OrdinalIgnoreCase))
{
    // Enumerate every BOND (BoneDelta) resource in EA's data, parse it, and report
    // resource counts + bone-hash distribution. BONDs are referenced by SimInfo body
    // modifiers and sculpts; applying them is the missing step that closes the Adult
    // Female waist gap and gives child/toddler/infant faces proper proportions.
    //
    // BOND resource type ID per TS4 community: 0xCB1CF21C (verified against the
    // actual SimInfo FaceModifier/BodyModifier reference resolution path).
    var sbRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4\Data";
    var sbCatalog = new LlamaResourceCatalogService();
    var sbSource = new DataSourceDefinition(
        Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
        "ScanBonds",
        sbRoot,
        SourceKind.Game);
    var sbPackages = Directory.EnumerateFiles(sbRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    Console.WriteLine($"scan-bonds: scanning {sbPackages.Length} package(s)");

    // BOND resource type names commonly seen: "BondData" / "BoneDelta" — use type ID heuristic.
    // We accept any type whose name contains "Bone" or "Bond" or is the well-known 0xCB1CF21C.
    var totalBonds = 0;
    var parsed = 0;
    var parseFailed = 0;
    var totalAdjustments = 0;
    var distinctSlotHashes = new HashSet<uint>();
    var samplesByPackage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    foreach (var pkg in sbPackages)
    {
        try
        {
            var sbScan = await sbCatalog.ScanPackageAsync(sbSource, pkg, progress: null, CancellationToken.None);
            foreach (var resource in sbScan.Resources)
            {
                var typeName = resource.Key.TypeName ?? "";
                if (resource.Key.Type != 0x0355E0A6u) continue;
                totalBonds++;
                try
                {
                    var bytes = await sbCatalog.GetResourceBytesAsync(pkg, resource.Key, raw: false, CancellationToken.None, null);
                    if (totalBonds <= 3)
                    {
                        Console.WriteLine($"  Sample BOND #{totalBonds}: {resource.Key.FullTgi}, {bytes.Length} bytes");
                        var preview = Math.Min(64, bytes.Length);
                        Console.WriteLine($"    First {preview} bytes: {Convert.ToHexString(bytes.AsSpan(0, preview))}");
                    }
                    var bond = Sims4ResourceExplorer.Packages.Ts4BondResource.Parse(bytes);
                    parsed++;
                    totalAdjustments += bond.Adjustments.Count;
                    foreach (var adj in bond.Adjustments) distinctSlotHashes.Add(adj.SlotHash);
                    samplesByPackage[Path.GetFileName(pkg)] = samplesByPackage.GetValueOrDefault(Path.GetFileName(pkg)) + 1;
                }
                catch (Exception ex)
                {
                    if (parseFailed < 3) Console.WriteLine($"    Parse failed: {ex.Message}");
                    parseFailed++;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  scan error in {pkg}: {ex.Message}");
        }
    }

    Console.WriteLine($"  Total BOND resources discovered: {totalBonds:N0}");
    Console.WriteLine($"  Parsed successfully:             {parsed:N0}");
    Console.WriteLine($"  Parse failures:                  {parseFailed:N0}");
    Console.WriteLine($"  Total bone adjustments:          {totalAdjustments:N0}");
    Console.WriteLine($"  Distinct bone slot hashes:       {distinctSlotHashes.Count:N0}");
    Console.WriteLine();
    Console.WriteLine("  BOND counts per package (top 10):");
    foreach (var (pkg, count) in samplesByPackage.OrderByDescending(static kv => kv.Value).Take(10))
    {
        Console.WriteLine($"    {count,5}  {pkg}");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-bond-morph", StringComparison.OrdinalIgnoreCase))
{
    // End-to-end check of the BOND morph pipeline (Plan B.1-B.3 + B.5):
    //   1. Load a real SimInfo from the production cache.
    //   2. Resolve adjustments via BondMorphResolver — this exercises the
    //      SimModifier → BOND chain through IIndexStore.
    //   3. Load the canonical rig matching the SimInfo's species/age via
    //      Ts4CanonicalRigCatalog and parse it inline.
    //   4. Report how many adjustment slot hashes match an actual rig bone
    //      hash, the maximum offset magnitude, and the top translations.
    //
    // This is the ProbeAsset equivalent of running the app and reading the
    // BondMorpher.MorphScene diagnostic line — no UI required.
    var defaultProd = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache";
    var pbmCacheDir = args.Length > 1 ? args[1] : defaultProd;
    var pbmSimTgi = args.Length > 2 ? args[2] : null;
    if (!Directory.Exists(pbmCacheDir)) { Console.Error.WriteLine($"Cache dir not found: {pbmCacheDir}"); return 1; }
    Console.WriteLine($"probe-bond-morph: cache={pbmCacheDir}");

    var pbmCache = new ProbeCacheService(Path.GetFullPath(pbmCacheDir + "/.."));
    pbmCache.EnsureCreated();
    var pbmStore = new SqliteIndexStore(pbmCache);
    await pbmStore.InitializeAsync(CancellationToken.None);

    // Pick a YA-Female-Human SimInfo if no explicit TGI was passed.
    var pbmDb = Path.Combine(pbmCacheDir, "index.sqlite");
    string? pbmRootTgi = pbmSimTgi;
    string? pbmPkg = null;
    string pbmSpecies = "human";
    string pbmAge = "young adult";
    string pbmGender = "female";
    if (pbmRootTgi is null)
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={pbmDb};Mode=ReadOnly");
        conn.Open();
        using var c = conn.CreateCommand();
        c.CommandText = """
            SELECT s.root_tgi, a.package_path, s.species_label, s.age_label, s.gender_label
            FROM sim_template_facts s
            JOIN assets a ON a.root_tgi = s.root_tgi
            WHERE lower(coalesce(s.species_label, '')) = 'human'
              AND lower(coalesce(s.age_label, '')) LIKE '%young adult%'
              AND lower(coalesce(s.gender_label, '')) = 'female'
            LIMIT 1
            """;
        using var r = c.ExecuteReader();
        if (r.Read())
        {
            pbmRootTgi = r.GetString(0);
            pbmPkg = r.GetString(1);
            pbmSpecies = r.IsDBNull(2) ? pbmSpecies : r.GetString(2);
            pbmAge = r.IsDBNull(3) ? pbmAge : r.GetString(3);
            pbmGender = r.IsDBNull(4) ? pbmGender : r.GetString(4);
        }
    }
    if (pbmRootTgi is null) { Console.Error.WriteLine("No SimInfo found. Pass an explicit TGI as 3rd arg."); return 1; }
    if (pbmPkg is null)
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={pbmDb};Mode=ReadOnly");
        conn.Open();
        using var c = conn.CreateCommand();
        c.CommandText = "SELECT package_path FROM assets WHERE root_tgi = $tgi LIMIT 1";
        c.Parameters.AddWithValue("$tgi", pbmRootTgi);
        using var r = c.ExecuteReader();
        if (r.Read()) pbmPkg = r.GetString(0);
    }
    if (pbmPkg is null) { Console.Error.WriteLine($"SimInfo {pbmRootTgi} not in assets index."); return 1; }
    Console.WriteLine($"  SimInfo: {pbmRootTgi}  ({pbmSpecies} / {pbmAge} / {pbmGender})");
    Console.WriteLine($"  Package: {pbmPkg}");

    // Parse the SimInfo for label-driven rig lookup (in case sim_template_facts disagrees).
    var pbmTgiParts = pbmRootTgi.Split(':');
    var pbmKey = new ResourceKeyRecord(
        Convert.ToUInt32(pbmTgiParts[0], 16),
        Convert.ToUInt32(pbmTgiParts[1], 16),
        Convert.ToUInt64(pbmTgiParts[2], 16),
        "SimInfo");
    var pbmCatalog = new LlamaResourceCatalogService();
    var pbmSimBytes = await pbmCatalog.GetResourceBytesAsync(pbmPkg, pbmKey, raw: false, CancellationToken.None, null);
    var pbmSim = Sims4ResourceExplorer.Assets.Ts4SimInfoParser.Parse(pbmSimBytes);
    Console.WriteLine($"  Parsed SimInfo v{pbmSim.Version}  Species='{pbmSim.SpeciesLabel}' Age='{pbmSim.AgeLabel}' Gender='{pbmSim.GenderLabel}'");
    Console.WriteLine($"  BodyModifiers={pbmSim.BodyModifierCount}  FaceModifiers={pbmSim.FaceModifierCount}");

    // Resolve adjustments through the production resolver.
    var pbmFakeMeta = new ResourceMetadata(
        Id: Guid.Empty,
        DataSourceId: Guid.Empty,
        SourceKind: SourceKind.Game,
        PackagePath: pbmPkg,
        Key: pbmKey,
        Name: Path.GetFileName(pbmPkg),
        CompressedSize: null,
        UncompressedSize: null,
        IsCompressed: null,
        PreviewKind: PreviewKind.Metadata,
        IsPreviewable: false,
        IsExportCapable: false,
        AssetLinkageSummary: string.Empty,
        Diagnostics: string.Empty);
    var pbmResolver = new Sims4ResourceExplorer.Assets.BondMorphResolver(pbmStore, pbmCatalog);
    var pbmAdjustments = await pbmResolver.ResolveAsync(pbmFakeMeta, CancellationToken.None);
    Console.WriteLine();
    Console.WriteLine($"  BondMorphResolver returned {pbmAdjustments.Count:N0} adjustment(s).");

    if (pbmAdjustments.Count == 0)
    {
        Console.WriteLine("  No adjustments — either every modifier weight is 0, no SMODs reference BONDs, or modifier link resolution failed.");
        return 0;
    }

    // Per-modifier weight distribution sanity check.
    var pbmWeights = pbmAdjustments.Select(a => a.Weight).ToArray();
    Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
        $"  Weight stats: min={pbmWeights.Min():0.######}  max={pbmWeights.Max():0.######}  abs-max={pbmWeights.Max(Math.Abs):0.######}"));

    // Per-offset distribution: how big are the raw BOND offsets before weight scaling?
    var pbmRawOffsetMag = pbmAdjustments.Select(a => Math.Sqrt((double)a.OffsetX * a.OffsetX + (double)a.OffsetY * a.OffsetY + (double)a.OffsetZ * a.OffsetZ)).ToArray();
    Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
        $"  Raw offset magnitudes: min={pbmRawOffsetMag.Min():0.######}  max={pbmRawOffsetMag.Max():0.######}  median={pbmRawOffsetMag.OrderBy(x => x).ElementAt(pbmRawOffsetMag.Length / 2):0.######}"));

    Console.WriteLine();
    Console.WriteLine("  First 10 raw adjustments (from BondMorphResolver):");
    foreach (var a in pbmAdjustments.Take(10))
    {
        Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"    hash=0x{a.SlotHash:X8}  rawOff=({a.OffsetX:0.######}, {a.OffsetY:0.######}, {a.OffsetZ:0.######})  weight={a.Weight:0.######}"));
    }

    // Group by slot hash and collapse to a per-bone offset like BondMorpher does.
    var byBone = new Dictionary<uint, (double X, double Y, double Z, int Count)>();
    foreach (var adj in pbmAdjustments)
    {
        if (Math.Abs(adj.Weight) < 1e-6f) continue;
        byBone.TryGetValue(adj.SlotHash, out var cur);
        cur.X += adj.OffsetX * adj.Weight;
        cur.Y += adj.OffsetY * adj.Weight;
        cur.Z += adj.OffsetZ * adj.Weight;
        cur.Count++;
        byBone[adj.SlotHash] = cur;
    }
    Console.WriteLine($"  Distinct affected bone slot hashes: {byBone.Count:N0}");
    var pbmTopBones = byBone
        .Select(kv => (Hash: kv.Key, kv.Value.X, kv.Value.Y, kv.Value.Z, Mag: Math.Sqrt(kv.Value.X * kv.Value.X + kv.Value.Y * kv.Value.Y + kv.Value.Z * kv.Value.Z), kv.Value.Count))
        .OrderByDescending(t => t.Mag)
        .Take(10)
        .ToArray();
    Console.WriteLine();
    Console.WriteLine("  Top 10 bones by accumulated offset magnitude:");
    foreach (var t in pbmTopBones)
    {
        Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"    hash=0x{t.Hash:X8}  off=({t.X:0.######}, {t.Y:0.######}, {t.Z:0.######})  |off|={t.Mag:0.######}  contrib={t.Count}"));
    }

    // Resolve the canonical rig and compare hashes.
    var pbmRigInstance = Sims4ResourceExplorer.Core.Ts4CanonicalRigCatalog.GetRigInstance(
        pbmSim.SpeciesLabel ?? pbmSpecies,
        pbmSim.AgeLabel ?? pbmAge,
        occultLabel: null);
    if (pbmRigInstance is null)
    {
        Console.WriteLine();
        Console.WriteLine("  Could not resolve canonical rig instance — skipping bone-hash match.");
        return 0;
    }
    var pbmRigName = Sims4ResourceExplorer.Core.Ts4CanonicalRigCatalog.GetRigName(
        pbmSim.SpeciesLabel ?? pbmSpecies, pbmSim.AgeLabel ?? pbmAge) ?? "?";
    Console.WriteLine();
    Console.WriteLine($"  Canonical rig: {pbmRigName} (instance 0x{pbmRigInstance.Value:X16})");
    var pbmRigResources = await pbmStore.GetResourcesByFullInstanceAsync(pbmRigInstance.Value, CancellationToken.None);
    var pbmRigRes = pbmRigResources.FirstOrDefault();
    if (pbmRigRes is null)
    {
        Console.WriteLine("  Rig not present in the index — cannot check bone-hash overlap.");
        return 0;
    }
    Console.WriteLine($"  Rig resource: {pbmRigRes.Key.FullTgi} ({pbmRigRes.PackagePath})");

    byte[] pbmRigBytes;
    try
    {
        pbmRigBytes = await pbmCatalog.GetResourceBytesAsync(pbmRigRes.PackagePath, pbmRigRes.Key, raw: false, CancellationToken.None, null);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Failed to load rig bytes: {ex.Message}");
        return 0;
    }

    // Inline rig parser — matches Ts4RigResource.Parse in BuildBuySceneBuildService.Cas.cs.
    var pbmRigBoneHashes = new Dictionary<uint, string>();
    try
    {
        using var rigStream = new MemoryStream(pbmRigBytes, writable: false);
        using var rigReader = new BinaryReader(rigStream, System.Text.Encoding.UTF8, leaveOpen: true);
        var rigMajor = rigReader.ReadUInt32();
        var rigMinor = rigReader.ReadUInt32();
        if (rigMajor is not (3u or 4u) || rigMinor is not (1u or 2u))
        {
            Console.WriteLine($"  Unexpected rig version {rigMajor}.{rigMinor} — skipping.");
            return 0;
        }
        var pbmBoneCount = rigReader.ReadInt32();
        for (var i = 0; i < pbmBoneCount; i++)
        {
            // Position(3f) + Rotation(4f) + Scale(3f)
            for (var k = 0; k < 10; k++) _ = rigReader.ReadSingle();
            var nameLen = rigReader.ReadInt32();
            var nameChars = rigReader.ReadChars(nameLen);
            var name = new string(nameChars);
            _ = rigReader.ReadInt32();
            _ = rigReader.ReadInt32();
            var nameHash = rigReader.ReadUInt32();
            _ = rigReader.ReadUInt32();
            pbmRigBoneHashes[nameHash] = name;
        }
        Console.WriteLine($"  Rig parsed: {pbmRigBoneHashes.Count:N0} bones.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Inline rig parse failed: {ex.Message}");
        return 0;
    }

    var matched = 0;
    var unmatchedSamples = new List<uint>();
    foreach (var hash in byBone.Keys)
    {
        if (pbmRigBoneHashes.ContainsKey(hash)) matched++;
        else if (unmatchedSamples.Count < 10) unmatchedSamples.Add(hash);
    }
    Console.WriteLine();
    Console.WriteLine($"  Bone-hash overlap with canonical rig: {matched:N0}/{byBone.Count:N0} adjustment hashes match a rig bone.");
    if (unmatchedSamples.Count > 0)
    {
        Console.WriteLine($"  Sample unmatched hashes (first {unmatchedSamples.Count}):");
        foreach (var h in unmatchedSamples) Console.WriteLine($"    0x{h:X8}");
    }
    if (matched > 0)
    {
        Console.WriteLine();
        Console.WriteLine("  Top 10 matched bones by magnitude:");
        foreach (var t in pbmTopBones.Where(t => pbmRigBoneHashes.ContainsKey(t.Hash)).Take(10))
        {
            Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"    {pbmRigBoneHashes[t.Hash],-32}  off=({t.X:0.######}, {t.Y:0.######}, {t.Z:0.######})  |off|={t.Mag:0.######}"));
        }
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-smod", StringComparison.OrdinalIgnoreCase))
{
    // Direct inspection of a single SMOD: load bytes, parse, dump BOND/DMap references.
    var psPkg = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4\Data\Simulation\SimulationFullBuild0.package";
    var psInstance = args.Length > 2 ? args[2] : "EF28B0B8AE53C453";
    if (!ulong.TryParse(psInstance, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var psInstanceValue))
    {
        Console.Error.WriteLine($"Could not parse instance hex: {psInstance}");
        return 2;
    }
    var psKey = new ResourceKeyRecord(0xC5F6763Eu, 0u, psInstanceValue, "SimModifier");
    var psCatalog = new LlamaResourceCatalogService();
    byte[] psBytes;
    try
    {
        psBytes = await psCatalog.GetResourceBytesAsync(psPkg, psKey, raw: false, CancellationToken.None, null);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to load SMOD: {ex.Message}");
        return 1;
    }
    var smod = Sims4ResourceExplorer.Packages.Ts4SimModifierResource.Parse(psBytes);
    Console.WriteLine($"SMOD {psKey.FullTgi} ({psBytes.Length} bytes)");
    Console.WriteLine($"  ContextVersion: {smod.ContextVersion}");
    Console.WriteLine($"  Version: {smod.Version}");
    Console.WriteLine($"  AgeGender: 0x{smod.AgeGender:X8}");
    Console.WriteLine($"  Region: {smod.Region}");
    Console.WriteLine($"  SubRegion: {smod.SubRegion?.ToString() ?? "<absent>"}");
    Console.WriteLine($"  HasBondReference: {smod.HasBondReference}");
    if (smod.HasBondReference)
        Console.WriteLine($"    BonePoseKey: {smod.BonePoseKey.FullTgi}");
    Console.WriteLine($"  HasShapeDeformerMap: {smod.HasShapeDeformerMap}");
    if (smod.HasShapeDeformerMap)
        Console.WriteLine($"    DeformerMapShapeKey: {smod.DeformerMapShapeKey.FullTgi}");
    Console.WriteLine($"  HasNormalDeformerMap: {smod.HasNormalDeformerMap}");
    if (smod.HasNormalDeformerMap)
        Console.WriteLine($"    DeformerMapNormalKey: {smod.DeformerMapNormalKey.FullTgi}");
    Console.WriteLine($"  PublicKeys: {smod.PublicKeys.Count}, ExternalKeys: {smod.ExternalKeys.Count}, BgeoKeys: {smod.BgeoKeys.Count}");
    foreach (var k in smod.PublicKeys.Take(5))
        Console.WriteLine($"    public: {k.FullTgi}");
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-dmap-raw", StringComparison.OrdinalIgnoreCase))
{
    // Dump the first ~300 bytes of a DMap raw, then attempt our parser. Use this when we hit
    // an unknown compression byte to inspect what byte structure the v9+ format uses.
    var pdrPkg = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4\Data\Client\ClientDeltaBuild0.package";
    var pdrInstance = args.Length > 2 ? args[2] : "7A9D44AB67D00802";
    if (!ulong.TryParse(pdrInstance, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var pdrInstanceValue))
    {
        Console.Error.WriteLine($"Could not parse instance hex: {pdrInstance}");
        return 2;
    }
    var pdrCatalog = new LlamaResourceCatalogService();
    var pdrKey = new ResourceKeyRecord(0xDB43E069u, 0u, pdrInstanceValue, "DeformerMap");
    var pdrBytes = await pdrCatalog.GetResourceBytesAsync(pdrPkg, pdrKey, raw: false, CancellationToken.None, null);
    Console.WriteLine($"DMap {pdrKey.FullTgi} ({pdrBytes.Length} bytes)");
    var pdrDumpEnd = Math.Min(256, pdrBytes.Length);
    Console.WriteLine($"  First {pdrDumpEnd} bytes:");
    for (var off = 0; off < pdrDumpEnd; off += 16)
    {
        var len = Math.Min(16, pdrBytes.Length - off);
        Console.WriteLine($"    0x{off:X4}  {Convert.ToHexString(pdrBytes.AsSpan(off, len))}");
    }
    // Try parsing.
    Console.WriteLine();
    try
    {
        var dmap = Sims4ResourceExplorer.Packages.Ts4DeformerMapResource.Parse(pdrBytes);
        Console.WriteLine($"  Parse: OK. v{dmap.Version} {dmap.Width}x{dmap.Height} totalBytes={dmap.TotalBytes} scanLines={dmap.ScanLines.Count}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Parse: FAILED — {ex.Message}");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--query-asset-tgi-all", StringComparison.OrdinalIgnoreCase))
{
    var qatTgi = args.Length > 1 ? args[1] : string.Empty;
    var qatDir = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache";
    foreach (var shard in Directory.EnumerateFiles(qatDir, "*.sqlite").OrderBy(x => x))
    {
        Console.WriteLine($"=== {Path.GetFileName(shard)} ===");
        try
        {
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={shard};Mode=ReadOnly");
            conn.Open();
            using var c = conn.CreateCommand();
            c.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='assets' LIMIT 1";
            using var r1 = c.ExecuteReader();
            if (!r1.Read()) { Console.WriteLine("  (no assets table)"); continue; }
            r1.Close();
            c.CommandText = "SELECT id, asset_kind, display_name, category, package_path, root_type_name FROM assets WHERE root_tgi = $tgi";
            c.Parameters.AddWithValue("$tgi", qatTgi);
            using var r = c.ExecuteReader();
            var found = false;
            while (r.Read())
            {
                Console.WriteLine($"  id={r.GetString(0)} kind={r.GetString(1)} dn='{(r.IsDBNull(2) ? "" : r.GetString(2))}' cat='{(r.IsDBNull(3) ? "" : r.GetString(3))}' pkg='{r.GetString(4)}' rootType='{(r.IsDBNull(5) ? "" : r.GetString(5))}'");
                found = true;
            }
            if (!found) Console.WriteLine("  (no row)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ERROR: {ex.Message}");
        }
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--list-asset-categories-all", StringComparison.OrdinalIgnoreCase))
{
    // Cache is sharded across index.sqlite + index.shard0[1-3].sqlite. Query each.
    var lacaDir = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache";
    foreach (var shard in Directory.EnumerateFiles(lacaDir, "*.sqlite").OrderBy(x => x))
    {
        Console.WriteLine($"=== {Path.GetFileName(shard)} ===");
        try
        {
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={shard};Mode=ReadOnly");
            conn.Open();
            using var c = conn.CreateCommand();
            c.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='assets' LIMIT 1";
            using var r1 = c.ExecuteReader();
            if (!r1.Read()) { Console.WriteLine("  (no assets table)"); continue; }
            r1.Close();
            c.CommandText = "SELECT asset_kind, category, COUNT(*) FROM assets WHERE category IS NOT NULL GROUP BY asset_kind, category ORDER BY 3 DESC LIMIT 20";
            using var r = c.ExecuteReader();
            while (r.Read())
            {
                Console.WriteLine($"  kind='{r.GetString(0)}'  cat='{(r.IsDBNull(1) ? "" : r.GetString(1))}'  count={r.GetInt32(2)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ERROR: {ex.Message}");
        }
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--list-asset-categories", StringComparison.OrdinalIgnoreCase))
{
    var lacDb = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={lacDb};Mode=ReadOnly");
    conn.Open();
    using var c = conn.CreateCommand();
    c.CommandText = "SELECT asset_kind, category, COUNT(*) FROM assets WHERE asset_kind LIKE '%Sim%' OR asset_kind LIKE '%Char%' OR category LIKE '%Cat%' OR category LIKE '%Dog%' OR category LIKE '%Fox%' OR category LIKE '%Horse%' GROUP BY asset_kind, category ORDER BY 3 DESC LIMIT 30";
    using var r = c.ExecuteReader();
    while (r.Read())
    {
        Console.WriteLine($"  kind='{r.GetString(0)}'  category='{(r.IsDBNull(1) ? "" : r.GetString(1))}'  count={r.GetInt32(2)}");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--query-instance-types", StringComparison.OrdinalIgnoreCase))
{
    // Show what types/groups exist for a given full instance ID (across all packages).
    var qitInst = args.Length > 1 ? args[1] : string.Empty;
    if (!ulong.TryParse(qitInst, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var qitInstanceValue))
    {
        Console.Error.WriteLine($"Could not parse instance hex: {qitInst}");
        return 2;
    }
    var qitDir = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache";
    var qitCache = new ProbeCacheService(Path.GetFullPath(qitDir + "/.."));
    qitCache.EnsureCreated();
    var qitStore = new SqliteIndexStore(qitCache);
    await qitStore.InitializeAsync(CancellationToken.None);
    var qitResources = await qitStore.GetResourcesByFullInstanceAsync(qitInstanceValue, CancellationToken.None);
    Console.WriteLine($"Resources at instance 0x{qitInstanceValue:X16}: {qitResources.Count}");
    foreach (var r in qitResources.Take(40))
    {
        Console.WriteLine($"  {r.Key.FullTgi} ({r.Key.TypeName ?? "?"})  pkg={r.PackagePath}");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-caspart", StringComparison.OrdinalIgnoreCase))
{
    // Inspect a single CASPart: parse it, dump TgiList + texture key indices + which slot
    // each one resolves through. Useful for diagnosing why animal CASParts don't get a manifest
    // material (we found 0 for ahHead in build 0260 logs).
    var pcpInstance = args.Length > 1 ? args[1] : "0000000000050F2A";
    if (!ulong.TryParse(pcpInstance, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var pcpInstanceValue))
    {
        Console.Error.WriteLine($"Could not parse instance hex: {pcpInstance}");
        return 2;
    }
    // Find the CASPart in any package via the index store.
    var pcpCacheDir = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache";
    var pcpCache = new ProbeCacheService(Path.GetFullPath(pcpCacheDir + "/.."));
    pcpCache.EnsureCreated();
    var pcpStore = new SqliteIndexStore(pcpCache);
    await pcpStore.InitializeAsync(CancellationToken.None);
    var pcpResources = await pcpStore.GetResourcesByFullInstanceAsync(pcpInstanceValue, CancellationToken.None);
    var pcpCasResource = pcpResources.FirstOrDefault(r => r.Key.Type == 0x034AEECBu);
    if (pcpCasResource is null) { Console.Error.WriteLine($"CASPart {pcpInstance} not found in index."); return 1; }
    Console.WriteLine($"CASPart {pcpCasResource.Key.FullTgi}");
    Console.WriteLine($"  Package: {pcpCasResource.PackagePath}");
    Console.WriteLine($"  Name: {pcpCasResource.Name ?? "(null)"}");

    var pcpCatalog = new LlamaResourceCatalogService();
    var pcpBytes = await pcpCatalog.GetResourceBytesAsync(pcpCasResource.PackagePath, pcpCasResource.Key, raw: false, CancellationToken.None, null);
    Console.WriteLine($"  Bytes: {pcpBytes.Length}");
    Sims4ResourceExplorer.Assets.Ts4CasPart casPart;
    try
    {
        casPart = Sims4ResourceExplorer.Assets.Ts4CasPart.Parse(pcpBytes);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"  Parse failed: {ex.Message}");
        return 3;
    }

    Console.WriteLine($"  Name: {casPart.InternalName}");
    Console.WriteLine($"  BodyType: {casPart.BodyType}");
    Console.WriteLine($"  Species: {casPart.SpeciesValue}");
    Console.WriteLine($"  AgeGender: 0x{casPart.AgeGenderFlags:X8}");
    Console.WriteLine($"  NakedKey: 0x{casPart.NakedKey:X2}");
    Console.WriteLine($"  TgiList: {casPart.TgiList.Count} entries");
    for (var i = 0; i < casPart.TgiList.Count; i++)
    {
        var tgi = casPart.TgiList[i];
        Console.WriteLine($"    [{i,3}] type={tgi.TypeName ?? $"0x{tgi.Type:X8}"} group=0x{tgi.Group:X8} instance=0x{tgi.FullInstance:X16}");
    }
    Console.WriteLine($"  TextureReferences: {casPart.TextureReferences.Count} entries");
    foreach (var tr in casPart.TextureReferences)
    {
        Console.WriteLine($"    slot='{tr.Slot}' tgi={tr.Key.FullTgi} semantic={tr.Semantic}");
    }
    Console.WriteLine($"  Lods: {casPart.Lods.Count}");
    foreach (var lod in casPart.Lods)
    {
        Console.WriteLine($"    Level={lod.Level} KeyIndices=[{string.Join(",", lod.KeyIndices)}]");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--dump-texture-png", StringComparison.OrdinalIgnoreCase))
{
    // Dumps the decoded PNG bytes for a texture instance to disk so we can visually inspect
    // what the rendering pipeline is binding. Args:
    //   --dump-texture-png <instance-hex> [<output-path>]
    // E.g. for the cat body texture (RLE2 from ClientFullBuild2):
    //   --dump-texture-png 56B8369D5124339A C:/tmp/cat_body.png
    var dtpInstHex = args.Length > 1 ? args[1] : "56B8369D5124339A";
    var dtpOutPath = args.Length > 2 ? args[2] : Path.Combine("C:/tmp", $"texture_{dtpInstHex}.png");
    if (!ulong.TryParse(dtpInstHex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var dtpInst))
    {
        Console.Error.WriteLine($"Could not parse instance hex: {dtpInstHex}");
        return 2;
    }
    var dtpCacheDir = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache";
    var dtpCache = new ProbeCacheService(Path.GetFullPath(dtpCacheDir + "/.."));
    dtpCache.EnsureCreated();
    var dtpStore = new SqliteIndexStore(dtpCache);
    await dtpStore.InitializeAsync(CancellationToken.None);
    var dtpResources = await dtpStore.GetResourcesByFullInstanceAsync(dtpInst, CancellationToken.None);
    var dtpTextureNames = new[] { "DSTImage", "LRLEImage", "RLE2Image", "RLESImage", "PNGImage", "PNGImage2" };
    var dtpResource = dtpResources.FirstOrDefault(r => dtpTextureNames.Contains(r.Key.TypeName, StringComparer.OrdinalIgnoreCase));
    if (dtpResource is null)
    {
        Console.Error.WriteLine($"No texture resource at instance 0x{dtpInst:X16}");
        return 1;
    }
    Console.WriteLine($"Decoding {dtpResource.Key.FullTgi} from {dtpResource.PackagePath}");
    var dtpCat = new LlamaResourceCatalogService();
    var dtpPng = await dtpCat.GetTexturePngAsync(dtpResource.PackagePath, dtpResource.Key, CancellationToken.None);
    if (dtpPng is null) { Console.Error.WriteLine("Decode returned null"); return 3; }
    Directory.CreateDirectory(Path.GetDirectoryName(dtpOutPath)!);
    await File.WriteAllBytesAsync(dtpOutPath, dtpPng);
    Console.WriteLine($"Wrote {dtpPng.Length:N0} bytes to {dtpOutPath}");

    // Also report basic stats: first bytes, dimensions if PNG.
    if (dtpPng.Length >= 24 && dtpPng[0] == 0x89 && dtpPng[1] == 0x50)
    {
        var width = (dtpPng[16] << 24) | (dtpPng[17] << 16) | (dtpPng[18] << 8) | dtpPng[19];
        var height = (dtpPng[20] << 24) | (dtpPng[21] << 16) | (dtpPng[22] << 8) | dtpPng[23];
        Console.WriteLine($"PNG dimensions: {width}x{height}");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-siminfo-genetic-bt", StringComparison.OrdinalIgnoreCase))
{
    // Loads a SimInfo by TGI directly from a package and dumps GeneticParts grouped by
    // BodyType. Used to verify the build-0266 genetic-promotion fix: the Cat from the user's
    // tray household (TGI 025ED6F4:0:F50AFC1C874EFD1E) is not in the cache index, so we
    // probe the package directly. Args: --probe-siminfo-genetic-bt <package> <tgi>
    var pgbPkg = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4\Delta\EP04\ClientDeltaBuild0.package";
    var pgbTgi = args.Length > 2 ? args[2] : "025ED6F4:00000000:F50AFC1C874EFD1E";
    if (!File.Exists(pgbPkg)) { Console.Error.WriteLine($"Package not found: {pgbPkg}"); return 1; }
    var pgbParts = pgbTgi.Split(':');
    var pgbType = Convert.ToUInt32(pgbParts[0], 16);
    var pgbGroup = Convert.ToUInt32(pgbParts[1], 16);
    var pgbInst = Convert.ToUInt64(pgbParts[2], 16);
    var pgbKey = new ResourceKeyRecord(pgbType, pgbGroup, pgbInst, "SimInfo");
    var pgbCat = new LlamaResourceCatalogService();
    var pgbBytes = await pgbCat.GetResourceBytesAsync(pgbPkg, pgbKey, raw: false, CancellationToken.None, null);
    Console.WriteLine($"SimInfo bytes from {Path.GetFileName(pgbPkg)} at {pgbTgi}: {pgbBytes.Length:N0}");

    Sims4ResourceExplorer.Assets.Ts4SimInfo simInfo;
    try { simInfo = Sims4ResourceExplorer.Assets.Ts4SimInfoParser.Parse(pgbBytes); }
    catch (Exception ex) { Console.Error.WriteLine($"Parse failed: {ex.Message}"); return 2; }

    Console.WriteLine($"\nSimInfo: species={simInfo.SpeciesLabel} age={simInfo.AgeLabel} gender={simInfo.GenderLabel}");
    Console.WriteLine($"PeltLayers: {simInfo.PeltLayers.Count}");
    foreach (var p in simInfo.PeltLayers)
        Console.WriteLine($"  inst=0x{p.Instance:X16}  variant(color)=0x{p.Variant:X8}");
    Console.WriteLine($"Outfits: {simInfo.Outfits.Count}");
    Console.WriteLine($"OutfitParts (flat): {simInfo.OutfitParts.Count}");
    foreach (var op in simInfo.OutfitParts)
        Console.WriteLine($"  bt={(int)op.BodyType,3}  inst=0x{op.PartInstance:X16}");
    Console.WriteLine($"GeneticParts: {simInfo.GeneticParts.Count}");
    var byBt = simInfo.GeneticParts.GroupBy(g => g.BodyType).OrderBy(g => g.Key);
    foreach (var grp in byBt)
    {
        var insts = string.Join(", ", grp.Select(g => $"0x{g.PartInstance:X16}"));
        Console.WriteLine($"  bt={grp.Key,3}: {grp.Count()} part(s) — {insts}");
    }

    // Show outfit parts grouped by category.
    Console.WriteLine($"\nOutfit parts (per outfit, all categories):");
    foreach (var outfit in simInfo.Outfits)
    {
        Console.WriteLine($"  Outfit categoryValue={outfit.CategoryValue} parts={outfit.Parts.Count}");
        foreach (var p in outfit.Parts)
            Console.WriteLine($"    bt={(int)p.BodyType,3}  inst=0x{p.PartInstance:X16}");
    }

    // Highlight what 0266 will promote (assuming no body-driving outfit, which is the cat case).
    Console.WriteLine($"\nBuild 0266 promotion preview (assumes no body-driving outfit found):");
    foreach (var bt in new[] { 3u, 5u, 8u })
    {
        var fromGenetic = simInfo.GeneticParts.Where(g => g.BodyType == bt).ToList();
        if (fromGenetic.Count > 0)
        {
            Console.WriteLine($"  bt={bt,3}: ★ PROMOTE {fromGenetic.Count} genetic part(s): {string.Join(", ", fromGenetic.Select(g => $"0x{g.PartInstance:X16}"))}");
            continue;
        }
        var fromOutfit = simInfo.OutfitParts.FirstOrDefault(p => (uint)p.BodyType == bt);
        if (fromOutfit is not null)
            Console.WriteLine($"  bt={bt,3}: ★ PROMOTE outfit part 0x{fromOutfit.PartInstance:X16} (genetic empty, outfit fallback)");
        else
            Console.WriteLine($"  bt={bt,3}: not in genetic or outfit — nothing to promote");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-animal-sim-graph", StringComparison.OrdinalIgnoreCase))
{
    // Like --probe-sim-graph but joins on sim_template_facts to find an animal Sim.
    // Args: --probe-animal-sim-graph [species=Cat] [age=Adult] [gender=Female]
    var pasgSpecies = args.Length > 1 ? args[1] : "Cat";
    var pasgAge     = args.Length > 2 ? args[2] : "Adult";
    var pasgGender  = args.Length > 3 ? args[3] : "Female";
    var pasgCacheDir = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache";
    var pasgCache = new ProbeCacheService(Path.GetFullPath(pasgCacheDir + "/.."));
    pasgCache.EnsureCreated();
    var pasgStore = new SqliteIndexStore(pasgCache);
    await pasgStore.InitializeAsync(CancellationToken.None);

    var dbPath = Path.Combine(pasgCacheDir, "index.sqlite");
    string? simRootTgi = null;
    string? simPkg = null;
    using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly"))
    {
        conn.Open();
        using var c = conn.CreateCommand();
        // Some animal SimInfos appear in `assets` but not in `sim_template_facts`;
        // fall back to display-name matching.
        c.CommandText = """
            SELECT root_tgi, package_path, id
            FROM assets
            WHERE asset_kind = 'Sim'
              AND lower(coalesce(category,'')) = lower($species)
              AND lower(coalesce(display_name,'')) LIKE '%' || lower($age) || '%'
              AND lower(coalesce(display_name,'')) LIKE '%' || lower($gender) || '%'
            LIMIT 1
            """;
        c.Parameters.AddWithValue("$species", pasgSpecies);
        c.Parameters.AddWithValue("$age", pasgAge);
        c.Parameters.AddWithValue("$gender", pasgGender);
        using var r = c.ExecuteReader();
        if (r.Read())
        {
            simRootTgi = r.GetString(0);
            simPkg = r.GetString(1);
        }
    }
    if (simRootTgi is null)
    {
        // Diagnostic: dump what species/age/gender combinations DO exist
        Console.Error.WriteLine($"No {pasgSpecies} {pasgAge} {pasgGender} sim found. Available combos:");
        using var conn2 = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        conn2.Open();
        using var c2 = conn2.CreateCommand();
        c2.CommandText = "SELECT category, COUNT(*) FROM assets WHERE asset_kind = 'Sim' GROUP BY category ORDER BY 2 DESC";
        using var r2 = c2.ExecuteReader();
        while (r2.Read())
            Console.Error.WriteLine($"  category={(r2.IsDBNull(0) ? "(null)" : r2.GetString(0)),-15} count={r2.GetInt64(1)}");
        return 1;
    }
    Console.WriteLine($"Probing {pasgSpecies} {pasgAge} {pasgGender}: TGI={simRootTgi} pkg={Path.GetFileName(simPkg!)}");

    AssetSummary? simAsset = null;
    using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly"))
    {
        conn.Open();
        using var c = conn.CreateCommand();
        c.CommandText = """
            SELECT id, data_source_id, source_kind, asset_kind, display_name, category, package_path, root_tgi,
                   thumbnail_tgi, variant_count, linked_resource_count, diagnostics, package_name, root_type_name,
                   thumbnail_type_name, primary_geometry_type, identity_type, category_normalized, description
            FROM assets WHERE root_tgi = $tgi LIMIT 1
            """;
        c.Parameters.AddWithValue("$tgi", simRootTgi);
        using var r = c.ExecuteReader();
        if (r.Read())
        {
            var tgiParts = simRootTgi.Split(':');
            var rootKey = new ResourceKeyRecord(
                Convert.ToUInt32(tgiParts[0], 16),
                Convert.ToUInt32(tgiParts[1], 16),
                Convert.ToUInt64(tgiParts[2], 16),
                r.IsDBNull(13) ? "SimInfo" : r.GetString(13));
            simAsset = new AssetSummary(
                Guid.Parse(r.GetString(0)),
                Guid.Parse(r.GetString(1)),
                Enum.Parse<SourceKind>(r.GetString(2)),
                Enum.Parse<AssetKind>(r.GetString(3)),
                r.IsDBNull(4) ? "" : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetString(5),
                r.GetString(6),
                rootKey,
                r.IsDBNull(8) ? null : r.GetString(8),
                r.GetInt32(9),
                r.GetInt32(10),
                r.IsDBNull(11) ? "" : r.GetString(11),
                null,
                r.IsDBNull(12) ? null : r.GetString(12),
                r.IsDBNull(13) ? null : r.GetString(13),
                r.IsDBNull(14) ? null : r.GetString(14),
                r.IsDBNull(15) ? null : r.GetString(15),
                r.IsDBNull(16) ? null : r.GetString(16),
                r.IsDBNull(17) ? null : r.GetString(17),
                r.IsDBNull(18) ? null : r.GetString(18));
        }
    }
    if (simAsset is null) { Console.Error.WriteLine("Sim asset not found"); return 1; }

    var pasgCat = new LlamaResourceCatalogService();
    var pasgSrc = new DataSourceDefinition(simAsset.DataSourceId, "Probe", Path.GetDirectoryName(simAsset.PackagePath) ?? simAsset.PackagePath, SourceKind.Game);
    var pkgScan = await pasgCat.ScanPackageAsync(pasgSrc, simAsset.PackagePath, progress: null, CancellationToken.None);
    var pasgBld = new ExplicitAssetGraphBuilder(pasgCat, pasgStore);
    var pasgGraph = await pasgBld.BuildAssetGraphAsync(simAsset, pkgScan.Resources, CancellationToken.None);
    Console.WriteLine($"Diagnostics ({pasgGraph.Diagnostics.Count}):");
    foreach (var d in pasgGraph.Diagnostics) Console.WriteLine($"  {d}");
    if (pasgGraph.SimGraph is null) { Console.WriteLine("No SimGraph."); return 1; }
    Console.WriteLine($"\nBodyAssembly mode: {pasgGraph.SimGraph.BodyAssembly.Mode}");
    foreach (var layer in pasgGraph.SimGraph.BodyAssembly.Layers)
    {
        Console.WriteLine($"  {layer.Label}  state={layer.State}  candidates={layer.CandidateCount}");
    }
    Console.WriteLine("\nBody Candidates (first 3 per slot):");
    foreach (var bc in pasgGraph.SimGraph.BodyCandidates.OrderBy(b => b.Label))
    {
        Console.WriteLine($"  {bc.Label}: {bc.Count} candidate(s) source={bc.SourceKind}");
        foreach (var opt in bc.Candidates.Take(3))
            Console.WriteLine($"    → {opt.DisplayName}  ({opt.RootTgi}) {Path.GetFileName(opt.PackagePath)}");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--list-indexed-packages", StringComparison.OrdinalIgnoreCase))
{
    var lipDb = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    using var lipConn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={lipDb};Mode=ReadOnly");
    lipConn.Open();
    using var c = lipConn.CreateCommand();
    c.CommandText = "SELECT package_path, COUNT(*) FROM resources GROUP BY package_path ORDER BY package_path LIMIT 50";
    using var r = c.ExecuteReader();
    while (r.Read()) Console.WriteLine($"  {r.GetString(0).Replace(@"C:\GAMES\The Sims 4\", "...\\")}  ({r.GetInt64(1):N0} resources)");
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--global-scan-rig", StringComparison.OrdinalIgnoreCase))
{
    // Brute-force scan EVERY .package file in the game directory for a specific Rig instance.
    // Args: --global-scan-rig <instance-hex> [<game-dir>]
    var gsrInstHex = args.Length > 1 ? args[1] : "26E972C565C68FC1";
    var gsrRoot = args.Length > 2 ? args[2] : @"C:\GAMES\The Sims 4";
    var gsrInst = Convert.ToUInt64(gsrInstHex, 16);
    var gsrCat = new LlamaResourceCatalogService();
    var gsrKey = new ResourceKeyRecord(0x8EAF13DE, 0u, gsrInst, "Rig");
    var packages = Directory.EnumerateFiles(gsrRoot, "*.package", SearchOption.AllDirectories).ToArray();
    Console.WriteLine($"Scanning {packages.Length:N0} packages for Rig 0x{gsrInst:X16}...");
    var found = 0;
    foreach (var pkg in packages)
    {
        try
        {
            var bytes = await gsrCat.GetResourceBytesAsync(pkg, gsrKey, raw: false, CancellationToken.None, null);
            if (bytes is { Length: > 0 })
            {
                Console.WriteLine($"FOUND in: {pkg.Replace(gsrRoot, "...")}  ({bytes.Length:N0} bytes)");
                found++;
            }
        }
        catch { /* not in this package */ }
    }
    Console.WriteLine($"\nTotal hits: {found}");
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--check-mesh-connectivity", StringComparison.OrdinalIgnoreCase))
{
    // Verifies whether a child-cat assembly is geometrically connected: loads body + ears
    // GEOMs, loads the ccRig, computes the WORLD-space position of every bone the meshes
    // reference (walking parent transforms in the rig), and reports whether the body's
    // head-bone position is close to the ears' attachment point.
    // Args: --check-mesh-connectivity <package> <body-geom-tgi> <ears-geom-tgi> <rig-instance-hex>
    var cmcPkg = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4\EP04\ClientFullBuild0.package";
    var cmcBody = args.Length > 2 ? args[2] : "015A1849:0071DE20:1514D0CBFC1633D4"; // child cat body
    var cmcEars = args.Length > 3 ? args[3] : "015A1849:0062AC70:81FA08FA08FD9B91"; // cat ears (shared)
    var cmcRigInst = args.Length > 4 ? args[4] : "E0FC2EB394790FE3"; // ccRig
    var cmcRigPkg = @"C:\GAMES\The Sims 4\Data\Client\ClientDeltaBuild0.package";

    var cat = new LlamaResourceCatalogService();

    static Sims4ResourceExplorer.Preview.Ts4GeomResource LoadGeom(LlamaResourceCatalogService c, string pkg, string tgi)
    {
        var p = tgi.Split(':');
        var k = new ResourceKeyRecord(Convert.ToUInt32(p[0],16), Convert.ToUInt32(p[1],16), Convert.ToUInt64(p[2],16), "Geometry");
        var b = c.GetResourceBytesAsync(pkg, k, raw: false, CancellationToken.None, null).GetAwaiter().GetResult();
        return Sims4ResourceExplorer.Preview.Ts4GeomResource.Parse(b);
    }

    var bodyGeom = LoadGeom(cat, cmcPkg, cmcBody);
    var earsGeom = LoadGeom(cat, cmcPkg, cmcEars);
    var rigKey = new ResourceKeyRecord(0x8EAF13DE, 0u, Convert.ToUInt64(cmcRigInst,16), "Rig");
    var rigBytes = await cat.GetResourceBytesAsync(cmcRigPkg, rigKey, raw: false, CancellationToken.None, null);
    var rig = Sims4ResourceExplorer.Preview.Ts4RigResource.Parse(rigBytes);

    Console.WriteLine($"Body geom: {bodyGeom.Vertices.Count} verts, {bodyGeom.BoneHashes.Count} bones");
    Console.WriteLine($"Ears geom: {earsGeom.Vertices.Count} verts, {earsGeom.BoneHashes.Count} bones");
    Console.WriteLine($"Rig: {rig.Bones.Count} bones");
    Console.WriteLine();

    // Compute world matrix for each bone via parent walk.
    var rigByHash = rig.Bones.ToDictionary(b => b.NameHash);
    var worldCache = new Dictionary<uint, System.Numerics.Matrix4x4>();
    System.Numerics.Matrix4x4 ComputeWorld(Sims4ResourceExplorer.Preview.Ts4RigBone b)
    {
        if (worldCache.TryGetValue(b.NameHash, out var c2)) return c2;
        var local = System.Numerics.Matrix4x4.CreateScale(b.Scale)
            * System.Numerics.Matrix4x4.CreateFromQuaternion(b.Rotation)
            * System.Numerics.Matrix4x4.CreateTranslation(b.Position);
        var world = b.ParentHash is uint ph && rigByHash.TryGetValue(ph, out var p) ? local * ComputeWorld(p) : local;
        worldCache[b.NameHash] = world;
        return world;
    }

    // For body, dump key bones (Head, Spine0) world position
    Console.WriteLine("Body's reference bones in WORLD space (via rig):");
    foreach (var hash in new[] { 0x0F97B21Bu /* head */ , 0x57884BB9u /* root_bind probably */ })
    {
        if (rigByHash.TryGetValue(hash, out var b))
        {
            var w = ComputeWorld(b);
            Console.WriteLine($"  {b.Name} (0x{hash:X8})  world=({w.M41:F4}, {w.M42:F4}, {w.M43:F4})");
        }
    }

    Console.WriteLine("\nEars' bones in WORLD space (via rig):");
    foreach (var hash in earsGeom.BoneHashes.Distinct())
    {
        if (rigByHash.TryGetValue(hash, out var b))
        {
            var w = ComputeWorld(b);
            Console.WriteLine($"  {b.Name,-25} (0x{hash:X8})  world=({w.M41:F4}, {w.M42:F4}, {w.M43:F4})");
        }
        else
        {
            Console.WriteLine($"  hash 0x{hash:X8}  NOT IN RIG");
        }
    }

    // Compute body mesh world bounds (use vertex positions × bone bind matrices).
    static (System.Numerics.Vector3 min, System.Numerics.Vector3 max) ComputeMeshBounds(
        Sims4ResourceExplorer.Preview.Ts4GeomResource g)
    {
        if (g.Vertices.Count == 0) return (System.Numerics.Vector3.Zero, System.Numerics.Vector3.Zero);
        var min = new System.Numerics.Vector3(float.PositiveInfinity);
        var max = new System.Numerics.Vector3(float.NegativeInfinity);
        foreach (var v in g.Vertices)
        {
            min.X = MathF.Min(min.X, v.Position[0]); max.X = MathF.Max(max.X, v.Position[0]);
            min.Y = MathF.Min(min.Y, v.Position[1]); max.Y = MathF.Max(max.Y, v.Position[1]);
            min.Z = MathF.Min(min.Z, v.Position[2]); max.Z = MathF.Max(max.Z, v.Position[2]);
        }
        return (min, max);
    }

    var (bodyMin, bodyMax) = ComputeMeshBounds(bodyGeom);
    var (earsMin, earsMax) = ComputeMeshBounds(earsGeom);
    Console.WriteLine($"\nBody mesh bind-pose bounds: min=({bodyMin.X:F3}, {bodyMin.Y:F3}, {bodyMin.Z:F3}) max=({bodyMax.X:F3}, {bodyMax.Y:F3}, {bodyMax.Z:F3})");
    Console.WriteLine($"Ears mesh bind-pose bounds: min=({earsMin.X:F3}, {earsMin.Y:F3}, {earsMin.Z:F3}) max=({earsMax.X:F3}, {earsMax.Y:F3}, {earsMax.Z:F3})");

    // Apply build 0272 per-vertex bind correction by computing per-bone delta and shifting.
    var acRigKey = new ResourceKeyRecord(0x8EAF13DE, 0u, 0x26E972C565C68FC1UL, "Rig");  // acRig
    var acRigBytes = await cat.GetResourceBytesAsync(cmcRigPkg, acRigKey, raw: false, CancellationToken.None, null);
    var acRig = Sims4ResourceExplorer.Preview.Ts4RigResource.Parse(acRigBytes);
    var acByHash = acRig.Bones.ToDictionary(b => b.NameHash);
    var acWorldCache = new Dictionary<uint, System.Numerics.Matrix4x4>();
    System.Numerics.Matrix4x4 ComputeAcWorld(Sims4ResourceExplorer.Preview.Ts4RigBone b)
    {
        if (acWorldCache.TryGetValue(b.NameHash, out var cw)) return cw;
        var local = System.Numerics.Matrix4x4.CreateScale(b.Scale)
            * System.Numerics.Matrix4x4.CreateFromQuaternion(b.Rotation)
            * System.Numerics.Matrix4x4.CreateTranslation(b.Position);
        var world = b.ParentHash is uint ph && acByHash.TryGetValue(ph, out var p) ? local * ComputeAcWorld(p) : local;
        acWorldCache[b.NameHash] = world;
        return world;
    }
    foreach (var b in acRig.Bones) ComputeAcWorld(b);
    var deltaByHash = new Dictionary<uint, System.Numerics.Vector3>();
    foreach (var hash in earsGeom.BoneHashes)
    {
        if (worldCache.TryGetValue(hash, out var c) && acWorldCache.TryGetValue(hash, out var a))
        {
            deltaByHash[hash] = new System.Numerics.Vector3(c.M41 - a.M41, c.M42 - a.M42, c.M43 - a.M43);
        }
    }
    Console.WriteLine($"\nDeltas (ccRig - acRig) for ears bones:");
    foreach (var (h, d) in deltaByHash) Console.WriteLine($"  hash=0x{h:X8}  delta=({d.X:F4}, {d.Y:F4}, {d.Z:F4})");

    // Apply weighted delta to ears vertices and recompute bounds.
    var correctedMin = new System.Numerics.Vector3(float.PositiveInfinity);
    var correctedMax = new System.Numerics.Vector3(float.NegativeInfinity);
    foreach (var v in earsGeom.Vertices)
    {
        if (v.BlendIndices is null || v.BlendWeights is null) continue;
        var totalW = 0f;
        foreach (var w in v.BlendWeights) totalW += MathF.Max(w, 0f);
        if (totalW <= 0f) continue;
        var accum = System.Numerics.Vector3.Zero;
        for (var i = 0; i < v.BlendIndices.Length && i < v.BlendWeights.Length; i++)
        {
            var w = v.BlendWeights[i];
            if (w <= 0f) continue;
            var bi = v.BlendIndices[i];
            if (bi < 0 || bi >= earsGeom.BoneHashes.Count) continue;
            if (deltaByHash.TryGetValue(earsGeom.BoneHashes[bi], out var d)) accum += d * (w / totalW);
        }
        var nx = v.Position[0] + accum.X;
        var ny = v.Position[1] + accum.Y;
        var nz = v.Position[2] + accum.Z;
        correctedMin.X = MathF.Min(correctedMin.X, nx); correctedMax.X = MathF.Max(correctedMax.X, nx);
        correctedMin.Y = MathF.Min(correctedMin.Y, ny); correctedMax.Y = MathF.Max(correctedMax.Y, ny);
        correctedMin.Z = MathF.Min(correctedMin.Z, nz); correctedMax.Z = MathF.Max(correctedMax.Z, nz);
    }
    Console.WriteLine($"\nEars mesh AFTER bind correction: min=({correctedMin.X:F3}, {correctedMin.Y:F3}, {correctedMin.Z:F3}) max=({correctedMax.X:F3}, {correctedMax.Y:F3}, {correctedMax.Z:F3})");
    Console.WriteLine($"  (compare body max Y={bodyMax.Y:F3} — ears should be just above)");
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--compare-rigs-for-geom", StringComparison.OrdinalIgnoreCase))
{
    // Compares bone overlap of multiple canonical rigs against a single GEOM, to see if
    // adult/child/etc. variants tie or differ. Args: --compare-rigs-for-geom <pkg> <geom-tgi>
    var crfPkg = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4\EP04\ClientFullBuild0.package";
    var crfTgi = args.Length > 2 ? args[2] : "015A1849:004517B8:56B8369D5124339A";  // adult cat body
    var crfParts = crfTgi.Split(':');
    var crfType = Convert.ToUInt32(crfParts[0], 16);
    var crfGroup = Convert.ToUInt32(crfParts[1], 16);
    var crfInst = Convert.ToUInt64(crfParts[2], 16);

    static ulong Fnv64(string name)
    {
        const ulong basis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong h = basis;
        foreach (var b in System.Text.Encoding.ASCII.GetBytes(name.ToLowerInvariant()))
        {
            h *= prime;
            h ^= b;
        }
        return h;
    }

    var crfCat = new LlamaResourceCatalogService();
    var geomBytes = await crfCat.GetResourceBytesAsync(crfPkg, new ResourceKeyRecord(crfType, crfGroup, crfInst, "Geometry"), raw: false, CancellationToken.None, null);
    var geom = Sims4ResourceExplorer.Preview.Ts4GeomResource.Parse(geomBytes);
    var geomBones = geom.BoneHashes.ToHashSet();
    Console.WriteLine($"GEOM {crfTgi} has {geomBones.Count} distinct bones");

    var rigNames = new[] { "auRig", "cuRig", "puRig", "acRig", "ccRig", "adRig", "cdRig", "alRig", "clRig", "ahRig", "chRig" };
    var probePaths = new[]
    {
        @"C:\GAMES\The Sims 4\Data\Client\ClientFullBuild0.package",
        @"C:\GAMES\The Sims 4\Data\Client\ClientDeltaBuild0.package",
        @"C:\GAMES\The Sims 4\Data\Simulation\SimulationDeltaBuild0.package",
    };
    Console.WriteLine($"\n  rig       overlap  total  bones-not-in-mesh");
    foreach (var name in rigNames)
    {
        var hash = Fnv64(name);
        var key = new ResourceKeyRecord(0x8EAF13DE, 0u, hash, "Rig");
        foreach (var pkg in probePaths)
        {
            try
            {
                var rigBytes = await crfCat.GetResourceBytesAsync(pkg, key, raw: false, CancellationToken.None, null);
                if (rigBytes is { Length: > 0 })
                {
                    var rig = Sims4ResourceExplorer.Preview.Ts4RigResource.Parse(rigBytes);
                    var overlap = rig.Bones.Count(b => geomBones.Contains(b.NameHash));
                    Console.WriteLine($"  {name,-8}  {overlap,7}  {rig.Bones.Count,5}    pkg={Path.GetFileName(pkg)}");
                    break;
                }
            }
            catch { }
        }
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--scan-pkg-for-rig", StringComparison.OrdinalIgnoreCase))
{
    // Direct package scan (bypassing the index) for a specific Rig instance hash.
    // Args: --scan-pkg-for-rig <package-path> <instance-hex>
    var spfPkg = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4\EP04\ClientFullBuild0.package";
    var spfInstHex = args.Length > 2 ? args[2] : "26E972C565C68FC1";  // acRig
    var spfInst = Convert.ToUInt64(spfInstHex, 16);
    if (!File.Exists(spfPkg)) { Console.Error.WriteLine($"Package not found: {spfPkg}"); return 1; }

    var spfCat = new LlamaResourceCatalogService();
    var spfKey = new ResourceKeyRecord(0x8EAF13DE, 0u, spfInst, "Rig");
    try
    {
        var bytes = await spfCat.GetResourceBytesAsync(spfPkg, spfKey, raw: false, CancellationToken.None, null);
        Console.WriteLine($"FOUND: Rig 0x{spfInst:X16} in {Path.GetFileName(spfPkg)}: {bytes.Length:N0} bytes");
        var rig = Sims4ResourceExplorer.Preview.Ts4RigResource.Parse(bytes);
        Console.WriteLine($"  bone count: {rig.Bones.Count}");
        var catEarBones = new uint[] { 0x0F97B21B, 0x34D7126B, 0x76627909, 0xB65E60B7, 0xB6609F20, 0xF4D46E82, 0xF4DB2AFD };
        var rigHashes = rig.Bones.Select(b => b.NameHash).ToHashSet();
        var earOverlap = catEarBones.Count(h => rigHashes.Contains(h));
        Console.WriteLine($"  cat-ears bone overlap: {earOverlap}/7");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Not in {Path.GetFileName(spfPkg)}: {ex.Message}");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--inspect-rig", StringComparison.OrdinalIgnoreCase))
{
    // Loads a Rig by instance and dumps its bone count + first 30 bone hashes.
    // Args: --inspect-rig <instance-hex> [<package-path>]
    var irInst = args.Length > 1 ? args[1] : "0D29F41E52C10D17";
    var irPkg = args.Length > 2 ? args[2] : @"C:\GAMES\The Sims 4\Data\Client\ClientFullBuild0.package";
    if (!File.Exists(irPkg)) { Console.Error.WriteLine($"Pkg not found: {irPkg}"); return 1; }
    var irKey = new ResourceKeyRecord(0x8EAF13DE, 0u, Convert.ToUInt64(irInst, 16), "Rig");
    var irCat = new LlamaResourceCatalogService();
    var irBytes = await irCat.GetResourceBytesAsync(irPkg, irKey, raw: false, CancellationToken.None, null);
    var rig = Sims4ResourceExplorer.Preview.Ts4RigResource.Parse(irBytes);
    Console.WriteLine($"Rig 0x{irKey.FullInstance:X16} from {Path.GetFileName(irPkg)}: {rig.Bones.Count} bones");
    // Find ear bones (the 7 hashes from cat ears geometry) and walk their parent chain.
    var catEarHashes = new uint[] { 0x0F97B21B, 0x34D7126B, 0x76627909, 0xB65E60B7, 0xB6609F20, 0xF4D46E82, 0xF4DB2AFD };
    Console.WriteLine($"\nEars-relevant bones in this rig:");
    foreach (var earHash in catEarHashes)
    {
        var idx = rig.Bones.ToList().FindIndex(b => b.NameHash == earHash);
        if (idx < 0) { Console.WriteLine($"  hash=0x{earHash:X8} NOT IN RIG"); continue; }
        var earBone = rig.Bones[idx];
        var parentName = earBone.ParentIndex >= 0 && earBone.ParentIndex < rig.Bones.Count
            ? rig.Bones[earBone.ParentIndex].Name : "(none)";
        Console.WriteLine($"  name={earBone.Name,-30} pos=({earBone.Position.X:F4},{earBone.Position.Y:F4},{earBone.Position.Z:F4})  parent={parentName}");
    }
    // Check overlap with cat ears bones.
    var catEarBones = new uint[] { 0x0F97B21B, 0x34D7126B, 0x76627909, 0xB65E60B7, 0xB6609F20, 0xF4D46E82, 0xF4DB2AFD };
    var rigHashes = rig.Bones.Select(b => b.NameHash).ToHashSet();
    var earOverlap = catEarBones.Count(h => rigHashes.Contains(h));
    Console.WriteLine($"\nCat ear bone overlap: {earOverlap} / 7");
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-pelt-layer", StringComparison.OrdinalIgnoreCase))
{
    // Dumps the raw bytes of a PetPeltLayer (type 0x26AF8338) and tries to interpret them.
    // Format is undocumented; we'll dump first 256 bytes and look for patterns
    // (TGI references, color values, version field).
    // Args: --probe-pelt-layer <instance-hex> [<package>]
    var pplInst = args.Length > 1 ? args[1] : "000000000001C50C";
    var pplPkg = args.Length > 2 ? args[2] : @"C:\GAMES\The Sims 4\Data\Client\ClientFullBuild0.package";
    if (!File.Exists(pplPkg))
        pplPkg = @"C:\GAMES\The Sims 4\Data\Simulation\SimulationFullBuild0.package";
    if (!File.Exists(pplPkg)) { Console.Error.WriteLine($"Package not found: {pplPkg}"); return 1; }

    var pplCat = new LlamaResourceCatalogService();
    var pplKey = new ResourceKeyRecord(0x26AF8338, 0u, Convert.ToUInt64(pplInst, 16), "PetPeltLayer");
    byte[] pplBytes;
    try
    {
        pplBytes = await pplCat.GetResourceBytesAsync(pplPkg, pplKey, raw: false, CancellationToken.None, null);
    }
    catch (Exception ex) { Console.Error.WriteLine($"Failed to read: {ex.Message}"); return 2; }
    Console.WriteLine($"PetPeltLayer 0x{pplKey.FullInstance:X16} from {Path.GetFileName(pplPkg)}: {pplBytes.Length} bytes");

    // Hex dump first 256 bytes.
    Console.WriteLine("\nHex dump (first 256 bytes):");
    for (var i = 0; i < Math.Min(256, pplBytes.Length); i += 16)
    {
        var hex = string.Join(" ", pplBytes.Skip(i).Take(16).Select(b => $"{b:X2}"));
        var ascii = string.Join("", pplBytes.Skip(i).Take(16).Select(b => b >= 32 && b < 127 ? (char)b : '.'));
        Console.WriteLine($"  {i:X4}: {hex,-48}  {ascii}");
    }

    // Look for plausible TGI references inside (16-byte aligned: type:4 + group:4 + instance:8).
    Console.WriteLine($"\nScanning for plausible TGI references (textures only):");
    var imgTypes = new uint[] { 0x00B2D882, 0x2BC04EDF, 0x2F7D0004, 0x2F7D0006, 0x3453CF95, 0xBA856C78 };
    for (var off = 0; off + 16 <= pplBytes.Length; off++)
    {
        var t = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(pplBytes.AsSpan(off, 4));
        if (!imgTypes.Contains(t)) continue;
        var g = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(pplBytes.AsSpan(off + 4, 4));
        var inst = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(pplBytes.AsSpan(off + 8, 8));
        if (inst == 0) continue;
        Console.WriteLine($"  offset 0x{off:X4}: type=0x{t:X8} group=0x{g:X8} instance=0x{inst:X16}");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--find-rig-for-bones", StringComparison.OrdinalIgnoreCase))
{
    // Loads a GEOM directly from a known package, extracts its bone-name hashes, then
    // scans every Rig in the index for the one with the highest bone overlap.
    // Args: --find-rig-for-bones <package-path> <geom-tgi>
    var frfPkg = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4\EP04\ClientFullBuild0.package";
    var frfTgi = args.Length > 2 ? args[2] : "015A1849:0062AC70:81FA08FA08FD9B91";
    var frfParts = frfTgi.Split(':');
    var frfType = Convert.ToUInt32(frfParts[0], 16);
    var frfGroup = Convert.ToUInt32(frfParts[1], 16);
    var frfInst = Convert.ToUInt64(frfParts[2], 16);
    if (!File.Exists(frfPkg)) { Console.Error.WriteLine($"Package not found: {frfPkg}"); return 1; }

    var frfCat = new LlamaResourceCatalogService();
    var frfKey = new ResourceKeyRecord(frfType, frfGroup, frfInst, "Geometry");
    var frfBytes = await frfCat.GetResourceBytesAsync(frfPkg, frfKey, raw: false, CancellationToken.None, null);
    var geom = Sims4ResourceExplorer.Preview.Ts4GeomResource.Parse(frfBytes);
    var boneHashes = geom.BoneHashes.ToHashSet();
    Console.WriteLine($"GEOM {frfTgi} has {boneHashes.Count} distinct bone hashes:");
    foreach (var h in boneHashes.OrderBy(x => x))
        Console.WriteLine($"  0x{h:X8}");

    Console.WriteLine($"\nScanning rigs for best overlap...");
    var frfDb = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    using var frfConn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={frfDb};Mode=ReadOnly");
    frfConn.Open();
    using var c = frfConn.CreateCommand();
    c.CommandText = "SELECT DISTINCT type_hex, group_hex, instance_hex, package_path FROM resources WHERE type_name='Rig'";
    using var r = c.ExecuteReader();
    var seenTgi = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var rigPaths = new List<(string path, ResourceKeyRecord key)>();
    while (r.Read())
    {
        var inst = r.GetString(2);
        if (!seenTgi.Add(inst)) continue;
        var rType = Convert.ToUInt32(r.GetString(0), 16);
        var rGroup = Convert.ToUInt32(r.GetString(1), 16);
        var rInst = Convert.ToUInt64(inst, 16);
        rigPaths.Add((r.GetString(3), new ResourceKeyRecord(rType, rGroup, rInst, "Rig")));
    }

    var results = new List<(int overlap, ulong inst, string pkg, int totalBones)>();
    foreach (var (path, key) in rigPaths)
    {
        try
        {
            var rigBytes = await frfCat.GetResourceBytesAsync(path, key, raw: false, CancellationToken.None, null);
            var rig = Sims4ResourceExplorer.Preview.Ts4RigResource.Parse(rigBytes);
            var overlap = rig.Bones.Count(b => boneHashes.Contains(b.NameHash));
            if (overlap > 0)
                results.Add((overlap, key.FullInstance, Path.GetFileName(path), rig.Bones.Count));
        }
        catch { }
    }
    Console.WriteLine($"\nTop 15 rigs by overlap (out of {rigPaths.Count} scanned, {results.Count} with overlap > 0):");
    foreach (var x in results.OrderByDescending(x => x.overlap).ThenBy(x => x.totalBones).Take(15))
        Console.WriteLine($"  overlap={x.overlap,3}/{x.totalBones,3}  inst=0x{x.inst:X16}  pkg={x.pkg}");
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--find-rig-for-geom", StringComparison.OrdinalIgnoreCase))
{
    // Loads a GEOM, extracts its bone-name hashes, then scans every Rig in the index for the
    // one with the highest bone overlap. Args: --find-rig-for-geom <geom-tgi>
    var frfTgi = args.Length > 1 ? args[1] : "015A1849:0062AC70:81FA08FA08FD9B91";
    var frfParts = frfTgi.Split(':');
    var frfType = Convert.ToUInt32(frfParts[0], 16);
    var frfGroup = Convert.ToUInt32(frfParts[1], 16);
    var frfInst = Convert.ToUInt64(frfParts[2], 16);

    var frfDb = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    using var frfConn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={frfDb};Mode=ReadOnly");
    frfConn.Open();
    using var c0 = frfConn.CreateCommand();
    c0.CommandText = "SELECT package_path, type_name, group_hex FROM resources WHERE lower(instance_hex)=lower($i) ORDER BY type_name LIMIT 10";
    c0.Parameters.AddWithValue("$i", $"{frfInst:X16}");
    using var r0 = c0.ExecuteReader();
    string? pkgPath = null;
    while (r0.Read())
    {
        var tn = r0.GetString(1);
        var grp = r0.GetString(2);
        Console.WriteLine($"  found: type={tn} group={grp} pkg={Path.GetFileName(r0.GetString(0))}");
        if (pkgPath is null && tn == "Geometry") pkgPath = r0.GetString(0);
    }
    if (pkgPath is null) { Console.Error.WriteLine($"No Geometry found for instance {frfInst:X16}"); return 1; }
    Console.WriteLine($"GEOM at {frfTgi} from {Path.GetFileName(pkgPath)}");

    var frfCat = new LlamaResourceCatalogService();
    var frfKey = new ResourceKeyRecord(frfType, frfGroup, frfInst, "Geometry");
    var frfBytes = await frfCat.GetResourceBytesAsync(pkgPath, frfKey, raw: false, CancellationToken.None, null);
    var geom = Sims4ResourceExplorer.Preview.Ts4GeomResource.Parse(frfBytes);
    var boneHashes = geom.BoneHashes.ToHashSet();
    Console.WriteLine($"GEOM has {boneHashes.Count} distinct bone hashes:");
    foreach (var h in boneHashes.OrderBy(x => x).Take(40))
        Console.WriteLine($"  0x{h:X8}");

    // Now scan all rigs and find best overlap.
    Console.WriteLine($"\nScanning {328} rig instances for best bone overlap...");
    using var c = frfConn.CreateCommand();
    c.CommandText = "SELECT DISTINCT type_hex, group_hex, instance_hex, package_path FROM resources WHERE type_name='Rig'";
    using var r = c.ExecuteReader();
    var seenTgi = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var rigPaths = new List<(string path, ResourceKeyRecord key)>();
    while (r.Read())
    {
        var inst = r.GetString(2);
        if (!seenTgi.Add(inst)) continue;  // one copy per instance is enough
        var rType = Convert.ToUInt32(r.GetString(0), 16);
        var rGroup = Convert.ToUInt32(r.GetString(1), 16);
        var rInst = Convert.ToUInt64(inst, 16);
        rigPaths.Add((r.GetString(3), new ResourceKeyRecord(rType, rGroup, rInst, "Rig")));
    }

    var results = new List<(int overlap, ulong inst, string pkg)>();
    foreach (var (path, key) in rigPaths)
    {
        try
        {
            var rigBytes = await frfCat.GetResourceBytesAsync(path, key, raw: false, CancellationToken.None, null);
            var rig = Sims4ResourceExplorer.Preview.Ts4RigResource.Parse(rigBytes);
            var overlap = rig.Bones.Count(b => boneHashes.Contains(b.NameHash));
            if (overlap > 0)
                results.Add((overlap, key.FullInstance, Path.GetFileName(path)));
        }
        catch { }
    }
    Console.WriteLine($"\nTop 15 rigs by overlap:");
    foreach (var x in results.OrderByDescending(x => x.overlap).Take(15))
        Console.WriteLine($"  overlap={x.overlap,3}  inst=0x{x.inst:X16}  pkg={x.pkg}");
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-rigs-in-package", StringComparison.OrdinalIgnoreCase))
{
    // List all Rig instances in the named package. Used to find species-specific rigs
    // (cat, dog, horse) that EA ships in EP packages.
    var pripFilter = args.Length > 1 ? args[1] : "EP04";
    var pripDb = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    using var pripConn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={pripDb};Mode=ReadOnly");
    pripConn.Open();
    using var c = pripConn.CreateCommand();
    c.CommandText = "SELECT instance_hex, group_hex, package_path FROM resources WHERE type_name='Rig' AND package_path LIKE '%' || $f || '%' ORDER BY package_path, instance_hex LIMIT 50";
    c.Parameters.AddWithValue("$f", pripFilter);
    using var r = c.ExecuteReader();
    var cnt = 0;
    while (r.Read())
    {
        cnt++;
        Console.WriteLine($"  inst=0x{r.GetString(0).PadLeft(16,'0')}  group=0x{r.GetString(1)}  pkg={Path.GetFileName(r.GetString(2))}");
    }
    Console.WriteLine($"Total Rig rows in '{pripFilter}': {cnt}");
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-rig-names", StringComparison.OrdinalIgnoreCase))
{
    // FNV-1 64-bit hash a candidate rig name and check if it exists in the index.
    // TS4 uses lowercase ASCII multiply-then-XOR FNV-1 (per build 0246 docs).
    static ulong Fnv64(string name)
    {
        const ulong basis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong h = basis;
        foreach (var b in System.Text.Encoding.ASCII.GetBytes(name.ToLowerInvariant()))
        {
            h *= prime;
            h ^= b;
        }
        return h;
    }
    var names = args.Length > 1 ? args.Skip(1).ToArray() : new[]
    {
        "auRig","cuRig","puRig","nuRig","tuRig","euRig","yuRig",
        "afRig","amRig","cfRig","cmRig","yfRig","ymRig","efRig","emRig","tfRig","tmRig","pfRig","pmRig","ifRig","imRig",
        "acRig","ckRig","apRig","cpRig","aRig","cRig",  // a=adult-c=cat, ck=child-cat, ap=adult-pet, cp=child-pet, just-aRig?
        "adRig","cdRig","alRig","clRig","afRig","cfRig", // d=dog, l=little dog, f=fox?
        "ahRig","chRig",  // h=horse
    };
    var prDb = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    using var prConn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={prDb};Mode=ReadOnly");
    prConn.Open();
    foreach (var name in names.Distinct())
    {
        var hash = Fnv64(name);
        using var c = prConn.CreateCommand();
        c.CommandText = "SELECT COUNT(*), MIN(package_path) FROM resources WHERE type_name='Rig' AND lower(instance_hex) = lower($i)";
        c.Parameters.AddWithValue("$i", $"{hash:X16}");
        using var r = c.ExecuteReader();
        r.Read();
        var count = r.GetInt64(0);
        var pkg = r.IsDBNull(1) ? "(none)" : Path.GetFileName(r.GetString(1));
        var marker = count > 0 ? "★" : " ";
        Console.WriteLine($"  {marker} {name,-10} → 0x{hash:X16}  count={count}  pkg={pkg}");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--list-rigs", StringComparison.OrdinalIgnoreCase))
{
    // Enumerates every Rig resource (TypeName='Rig', type=0x8EAF13DE) in the production index.
    // Used to discover what canonical rig names EA ships beyond {auRig, cuRig, puRig, nuRig}.
    // Animal-specific rigs (e.g. acRig for adult cat, ckRig for child cat) are likely needed
    // for the build 0267 child-cat ears-floating issue.
    var lrDb = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    using var lrConn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={lrDb};Mode=ReadOnly");
    lrConn.Open();
    using var c = lrConn.CreateCommand();
    c.CommandText = """
        SELECT instance_hex, group_hex, package_path
        FROM resources
        WHERE type_name = 'Rig'
        ORDER BY instance_hex
        LIMIT 400
        """;
    using var r = c.ExecuteReader();
    var byInstance = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    var cnt = 0;
    while (r.Read())
    {
        cnt++;
        var inst = r.GetString(0);
        var pkg = Path.GetFileName(r.GetString(2));
        if (!byInstance.TryGetValue(inst, out var pkgs)) { pkgs = new List<string>(); byInstance[inst] = pkgs; }
        pkgs.Add(pkg);
    }
    Console.WriteLine($"Total Rig rows: {cnt}, distinct instances: {byInstance.Count}");
    Console.WriteLine($"\nDistinct rig instances:");
    foreach (var kv in byInstance.OrderBy(k => k.Key))
    {
        Console.WriteLine($"  0x{kv.Key.PadLeft(16,'0')}  in {kv.Value.Count} pkg: {string.Join(", ", kv.Value.Take(3))}");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-cat-cas-naming", StringComparison.OrdinalIgnoreCase))
{
    // Just dump the first 50 assets with `acBody*` display name to see all the variants.
    var pccDb = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    using var pccConn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={pccDb};Mode=ReadOnly");
    pccConn.Open();
    using var c = pccConn.CreateCommand();
    c.CommandText = """
        SELECT a.display_name, lower(coalesce(f.internal_name,'')) AS iname,
               coalesce(f.species_label,''), coalesce(f.age_label,''), coalesce(f.gender_label,''),
               f.body_type, f.has_naked_link, f.default_body_type
        FROM cas_part_facts f
        JOIN assets a ON a.id = f.asset_id
        WHERE lower(coalesce(f.internal_name,'')) LIKE 'acbody%'
        ORDER BY iname
        LIMIT 200
        """;
    using var r = c.ExecuteReader();
    var distinctSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    while (r.Read())
    {
        var iname = r.GetString(1);
        // strip "acbody_" prefix if present, then take first segment.
        var suffix = iname.StartsWith("acbody_", StringComparison.OrdinalIgnoreCase) ? iname.Substring(7) : iname;
        var firstSeg = suffix.Contains('_') ? suffix.Substring(0, suffix.IndexOf('_')) : suffix;
        distinctSuffixes.Add(firstSeg);
    }
    Console.WriteLine($"Distinct first-segment suffixes after 'acbody_': {distinctSuffixes.Count}");
    foreach (var s in distinctSuffixes.OrderBy(x => x)) Console.WriteLine($"  {s}");
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-animal-body-anywhere", StringComparison.OrdinalIgnoreCase))
{
    // Looser version of --probe-animal-body-candidates: dump every cas_part_facts row whose
    // internal_name starts with the given prefix, ignoring body_type/has_scene_root/age/gender
    // filters. Goal: find out whether `acBody_Nude` exists in the index at all (it may be
    // filtered out by body_type=5 + has_scene_root=1 + the species filter).
    // Args: --probe-animal-body-anywhere [namePrefix=acbody]
    var pabaPrefix = args.Length > 1 ? args[1].ToLowerInvariant() : "acbody";
    var pabaDb = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    using var pabaConn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={pabaDb};Mode=ReadOnly");
    pabaConn.Open();
    Console.WriteLine($"probe-animal-body-anywhere: namePrefix={pabaPrefix}");

    using (var c = pabaConn.CreateCommand())
    {
        c.CommandText = """
            SELECT lower(coalesce(f.internal_name,'')) AS iname,
                   coalesce(f.species_label,''),
                   coalesce(f.age_label,''),
                   coalesce(f.gender_label,''),
                   f.body_type, f.has_naked_link, f.default_body_type,
                   a.asset_kind, a.has_scene_root,
                   a.package_path, a.root_tgi
            FROM cas_part_facts f
            JOIN assets a ON a.id = f.asset_id
            WHERE lower(coalesce(f.internal_name,'')) LIKE $prefix || '%'
              AND (lower(coalesce(f.internal_name,'')) LIKE '%nude%' OR
                   lower(coalesce(f.internal_name,'')) LIKE '%naked%' OR
                   lower(coalesce(f.internal_name,'')) LIKE '%default%')
            ORDER BY iname
            """;
        c.Parameters.AddWithValue("$prefix", pabaPrefix);
        using var r = c.ExecuteReader();
        Console.WriteLine($"  {"#",4}  {"naked",5} {"def",3} {"bt",3} {"hsr",3}  {"species",-7} {"age",-12} {"gender",-7}  {"name",-50}  {"package"}");
        var cnt = 0;
        while (r.Read())
        {
            cnt++;
            var iname = r.GetString(0);
            var species = r.GetString(1);
            var age = r.GetString(2);
            var gender = r.GetString(3);
            var bt = r.GetInt64(4);
            var nakedLink = r.GetInt64(5);
            var defaultBt = r.GetInt64(6);
            var hasSceneRoot = r.GetInt64(8);
            var pkg = Path.GetFileName(r.GetString(9));
            var nameTrim = iname.Length > 50 ? iname.Substring(0, 50) : iname;
            Console.WriteLine($"  {cnt,4}  {nakedLink,5} {defaultBt,3} {bt,3} {hasSceneRoot,3}  {species,-7} {age,-12} {gender,-7}  {nameTrim,-50}  {pkg}");
        }
        Console.WriteLine($"  ... total nude/naked/default rows: {cnt}");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-animal-body-candidates", StringComparison.OrdinalIgnoreCase))
{
    // Diagnose why animal Sims (esp. Cat Adult Female) end up assembling a costume CASPart
    // (e.g. acBody_EP04DragonOnsie_BlackGreen) as their Full Body instead of the nude
    // canonical body (acBody_Nude). Dumps every body_type=5 cas_part_facts row matching the
    // species/age/gender filter, sorted by the same priority signals the SQL pool uses
    // (has_naked_link DESC, default_body_type DESC, internal_name LIKE '%nude%' DESC).
    // Args: --probe-animal-body-candidates [species=Cat] [age=Adult] [gender=Female]
    var pabSpecies = args.Length > 1 ? args[1] : "Cat";
    var pabAge     = args.Length > 2 ? args[2] : "Adult";
    var pabGender  = args.Length > 3 ? args[3] : "Female";
    var pabDb = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    if (!File.Exists(pabDb)) { Console.Error.WriteLine($"DB not found: {pabDb}"); return 1; }
    using var pabConn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={pabDb};Mode=ReadOnly");
    pabConn.Open();
    Console.WriteLine($"probe-animal-body-candidates: species={pabSpecies} age={pabAge} gender={pabGender}");

    using (var c = pabConn.CreateCommand())
    {
        c.CommandText = """
            SELECT lower(coalesce(f.internal_name,'')) AS iname,
                   lower(coalesce(f.species_label,'')) AS species,
                   lower(coalesce(f.age_label,'')) AS age,
                   lower(coalesce(f.gender_label,'')) AS gender,
                   f.body_type, f.has_naked_link, f.default_body_type,
                   f.default_body_type_male, f.default_body_type_female,
                   a.package_path, a.root_tgi
            FROM cas_part_facts f
            JOIN assets a ON a.id = f.asset_id
            WHERE a.asset_kind = 'Cas'
              AND a.has_scene_root = 1
              AND f.body_type = 5
              AND lower(coalesce(f.species_label,'')) = lower($species)
              AND (f.age_label IS NULL OR f.age_label = '' OR lower(f.age_label) = 'unknown' OR lower(f.age_label) LIKE '%' || lower($age) || '%')
              AND (f.gender_label IS NULL OR f.gender_label = '' OR lower(f.gender_label) = 'unknown' OR lower(f.gender_label) = 'unisex' OR lower(f.gender_label) LIKE '%' || lower($gender) || '%')
            ORDER BY f.has_naked_link DESC,
                     f.default_body_type DESC,
                     CASE WHEN lower(coalesce(f.internal_name,'')) LIKE '%nude%' THEN 0 ELSE 1 END,
                     iname
            """;
        c.Parameters.AddWithValue("$species", pabSpecies);
        c.Parameters.AddWithValue("$age", pabAge);
        c.Parameters.AddWithValue("$gender", pabGender);
        using var r = c.ExecuteReader();
        var cnt = 0;
        Console.WriteLine($"  {"#",4}  {"naked",5} {"def",3} {"defM",4} {"defF",4}  {"name",-50}  {"package"}");
        while (r.Read())
        {
            cnt++;
            var iname = r.GetString(0);
            var nakedLink = r.GetInt64(5);
            var defaultBt = r.GetInt64(6);
            var defaultM = r.IsDBNull(7) ? 0 : r.GetInt64(7);
            var defaultF = r.IsDBNull(8) ? 0 : r.GetInt64(8);
            var pkg = Path.GetFileName(r.GetString(9));
            var nameTrim = iname.Length > 50 ? iname.Substring(0, 50) : iname;
            // Highlight _nude rows even if they're far down the sort.
            var marker = iname.Contains("nude") || iname.Contains("naked") || iname.Contains("default") ? " ★" : "";
            Console.WriteLine($"  {cnt,4}  {nakedLink,5} {defaultBt,3} {defaultM,4} {defaultF,4}  {nameTrim,-50}  {pkg}{marker}");
        }
        Console.WriteLine($"  ... total: {cnt} rows");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-texture-decode", StringComparison.OrdinalIgnoreCase))
{
    // Verify the production texture-decode pipeline (build 0265 LRLE/DST swap fix).
    // Args: --probe-texture-decode <instance-hex> [<expected-typename>]
    // Looks up every texture-type resource at that instance via the index, then for each
    // calls LlamaResourceCatalogService.GetTexturePngAsync exactly the way the preview
    // pipeline does. Reports per-resource: TypeName the catalog reported, byte length of
    // returned PNG (or null/exception). A pre-fix run on `3AB76830D9352E40` (Horse ahHead
    // diffuse, an LRLE) returned 0 bytes / null because the LRLE catch-clause never fired.
    var ptdInstanceHex = args.Length > 1 ? args[1] : "3AB76830D9352E40";
    if (!ulong.TryParse(ptdInstanceHex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var ptdInstance))
    {
        Console.Error.WriteLine($"Could not parse instance hex: {ptdInstanceHex}");
        return 2;
    }
    var ptdCacheDir = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache";
    var ptdCache = new ProbeCacheService(Path.GetFullPath(ptdCacheDir + "/.."));
    ptdCache.EnsureCreated();
    var ptdStore = new SqliteIndexStore(ptdCache);
    await ptdStore.InitializeAsync(CancellationToken.None);
    var ptdResources = await ptdStore.GetResourcesByFullInstanceAsync(ptdInstance, CancellationToken.None);
    var ptdTextureNames = new[] { "DSTImage", "LRLEImage", "RLE2Image", "RLESImage", "PNGImage", "PNGImage2" };
    var ptdTextures = ptdResources
        .Where(r => ptdTextureNames.Contains(r.Key.TypeName, StringComparer.OrdinalIgnoreCase))
        .ToArray();
    Console.WriteLine($"Instance 0x{ptdInstance:X16}: {ptdTextures.Length} texture-type resource(s) indexed.");
    if (ptdTextures.Length == 0)
    {
        Console.WriteLine("  (no texture rows found; nothing to decode)");
        return 0;
    }
    var ptdCatalog = new LlamaResourceCatalogService();
    foreach (var ptdTex in ptdTextures)
    {
        Console.WriteLine();
        Console.WriteLine($"Resource {ptdTex.Key.FullTgi}");
        Console.WriteLine($"  TypeName (catalog): {ptdTex.Key.TypeName}");
        Console.WriteLine($"  Package: {ptdTex.PackagePath}");
        try
        {
            var ptdPng = await ptdCatalog.GetTexturePngAsync(ptdTex.PackagePath, ptdTex.Key, CancellationToken.None);
            if (ptdPng is null)
            {
                Console.WriteLine("  Decode result: NULL (catalog returned no bytes — pipeline gave up)");
            }
            else if (ptdPng.Length < 8)
            {
                Console.WriteLine($"  Decode result: short ({ptdPng.Length} bytes — not a valid PNG)");
            }
            else
            {
                var isPngHeader = ptdPng[0] == 0x89 && ptdPng[1] == 0x50 && ptdPng[2] == 0x4E && ptdPng[3] == 0x47;
                Console.WriteLine($"  Decode result: OK ({ptdPng.Length:N0} bytes, PNG header = {isPngHeader})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Decode result: EXCEPTION {ex.GetType().Name}: {ex.Message}");
        }
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--query-asset-tgi", StringComparison.OrdinalIgnoreCase))
{
    var qatTgi = args.Length > 1 ? args[1] : string.Empty;
    var qatDb = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={qatDb};Mode=ReadOnly");
    conn.Open();
    using var c = conn.CreateCommand();
    c.CommandText = "SELECT id, asset_kind, display_name, category, package_path, root_type_name FROM assets WHERE root_tgi = $tgi";
    c.Parameters.AddWithValue("$tgi", qatTgi);
    using var r = c.ExecuteReader();
    while (r.Read())
    {
        Console.WriteLine($"  id={r.GetString(0)} kind={r.GetString(1)} dn='{(r.IsDBNull(2) ? "" : r.GetString(2))}' cat='{(r.IsDBNull(3) ? "" : r.GetString(3))}' pkg='{r.GetString(4)}' rootType='{(r.IsDBNull(5) ? "" : r.GetString(5))}'");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--list-species", StringComparison.OrdinalIgnoreCase))
{
    // Dump (species, age, gender) histogram from the production cache so we can spot Sims
    // with quirky labels (e.g., Cat/Dog/Fox/Little Dog that the morph-coverage probe filters out).
    var lspDb = args.Length > 1 ? args[1] : @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    if (!File.Exists(lspDb)) { Console.Error.WriteLine($"DB not found: {lspDb}"); return 1; }
    using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={lspDb};Mode=ReadOnly");
    conn.Open();
    using var c = conn.CreateCommand();
    c.CommandText = """
        SELECT s.species_label, s.age_label, s.gender_label, COUNT(*), MIN(s.root_tgi)
        FROM sim_template_facts s
        JOIN assets a ON a.root_tgi = s.root_tgi
        GROUP BY s.species_label, s.age_label, s.gender_label
        ORDER BY s.species_label, s.age_label, s.gender_label
        """;
    using var r = c.ExecuteReader();
    Console.WriteLine($"  {"Species",-12} {"Age",-14} {"Gender",-10}  Count  MinTGI");
    while (r.Read())
    {
        var species = r.IsDBNull(0) ? "(null)" : r.GetString(0);
        var age = r.IsDBNull(1) ? "(null)" : r.GetString(1);
        var gender = r.IsDBNull(2) ? "(null)" : r.GetString(2);
        var count = r.GetInt32(3);
        var min = r.IsDBNull(4) ? "" : r.GetString(4);
        Console.WriteLine($"  {species,-12} {age,-14} {gender,-10}  {count,5}  {min}");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-morph-coverage", StringComparison.OrdinalIgnoreCase))
{
    // Sweep multiple SimInfos (one per age × gender × species combo if available) and report
    // total BOND adjustments + DMap morphs + BGEO morphs + max displacement for each. Lets us
    // confirm the morph pipeline works across the whole catalogue, not just the v21 Adult Female
    // we've been debugging.
    var defaultProd = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache";
    var pmcCacheDir = args.Length > 1 ? args[1] : defaultProd;
    if (!Directory.Exists(pmcCacheDir)) { Console.Error.WriteLine($"Cache dir not found: {pmcCacheDir}"); return 1; }

    var pmcCache = new ProbeCacheService(Path.GetFullPath(pmcCacheDir + "/.."));
    pmcCache.EnsureCreated();
    var pmcStore = new SqliteIndexStore(pmcCache);
    await pmcStore.InitializeAsync(CancellationToken.None);

    var pmcDb = Path.Combine(pmcCacheDir, "index.sqlite");
    var pmcCatalog = new LlamaResourceCatalogService();
    var bondResolver = new Sims4ResourceExplorer.Assets.BondMorphResolver(pmcStore, pmcCatalog);
    var dmapResolver = new Sims4ResourceExplorer.Assets.DeformerMapResolver(pmcStore, pmcCatalog);
    var bgeoResolver = new Sims4ResourceExplorer.Assets.BlendGeometryResolver(pmcStore, pmcCatalog);

    // One representative SimInfo per (species, age, gender) combo.
    var rows = new List<(string Tgi, string Pkg, string Species, string Age, string Gender)>();
    using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={pmcDb};Mode=ReadOnly"))
    {
        conn.Open();
        using var c = conn.CreateCommand();
        c.CommandText = """
            SELECT MIN(s.root_tgi), MIN(a.package_path), s.species_label, s.age_label, s.gender_label
            FROM sim_template_facts s
            JOIN assets a ON a.root_tgi = s.root_tgi
            WHERE s.species_label IS NOT NULL AND s.age_label IS NOT NULL AND s.gender_label IS NOT NULL
            GROUP BY s.species_label, s.age_label, s.gender_label
            ORDER BY s.species_label, s.age_label, s.gender_label
            """;
        using var r = c.ExecuteReader();
        while (r.Read())
        {
            rows.Add((r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4)));
        }
    }
    Console.WriteLine($"probe-morph-coverage: {rows.Count} (species, age, gender) combos");
    Console.WriteLine();
    Console.WriteLine($"  {"Species",-10} {"Age",-14} {"Gender",-8}  BOND  DMap  BGEO   maxBgeoDisp  TGI");

    var totalCovered = 0;
    foreach (var row in rows)
    {
        var parts = row.Tgi.Split(':');
        var key = new ResourceKeyRecord(
            Convert.ToUInt32(parts[0], 16),
            Convert.ToUInt32(parts[1], 16),
            Convert.ToUInt64(parts[2], 16),
            "SimInfo");
        var meta = new ResourceMetadata(
            Id: Guid.Empty, DataSourceId: Guid.Empty, SourceKind: SourceKind.Game,
            PackagePath: row.Pkg, Key: key, Name: Path.GetFileName(row.Pkg),
            CompressedSize: null, UncompressedSize: null, IsCompressed: null,
            PreviewKind: PreviewKind.Metadata, IsPreviewable: false, IsExportCapable: false,
            AssetLinkageSummary: string.Empty, Diagnostics: string.Empty);

        IReadOnlyList<SimBoneMorphAdjustment> bonds = [];
        IReadOnlyList<Sims4ResourceExplorer.Packages.Ts4SimDeformerMorph> dmaps = [];
        IReadOnlyList<Sims4ResourceExplorer.Packages.Ts4SimBlendGeometryMorph> bgeos = [];
        try
        {
            bonds = await bondResolver.ResolveAsync(meta, CancellationToken.None);
            dmaps = await dmapResolver.ResolveAsync(meta, CancellationToken.None);
            bgeos = await bgeoResolver.ResolveAsync(meta, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  {row.Species,-10} {row.Age,-14} {row.Gender,-8}  -     -     -      ERROR  {row.Tgi}  ({ex.Message})");
            continue;
        }

        var maxBgeoDisp = 0f;
        foreach (var m in bgeos)
        {
            if (m.Bgeo.Lods.Count == 0) continue;
            var lod = m.Bgeo.Lods[0];
            for (var i = 0; i < (int)lod.NumberVertices && i < m.Bgeo.BlendMap.Count; i++)
            {
                var b = m.Bgeo.BlendMap[i];
                if (!b.PositionDelta) continue;
                if (b.Index < 0 || b.Index >= m.Bgeo.VectorData.Count) continue;
                var v = m.Bgeo.VectorData[b.Index].ToVector3();
                var scaled = v.Length() * Math.Abs(m.Weight);
                if (scaled > maxBgeoDisp) maxBgeoDisp = scaled;
            }
        }
        Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"  {row.Species,-10} {row.Age,-14} {row.Gender,-8}  {bonds.Count,4} {dmaps.Count,5} {bgeos.Count,5}   {maxBgeoDisp,11:0.######}  {row.Tgi}"));
        if (bonds.Count > 0 || dmaps.Count > 0 || bgeos.Count > 0) totalCovered++;
    }
    Console.WriteLine();
    Console.WriteLine($"  {totalCovered}/{rows.Count} Sim types received at least one morph adjustment.");
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-tray-morphs", StringComparison.OrdinalIgnoreCase))
{
    // Like --probe-tray-modifiers, but for each Sim it ALSO looks up what morph types each
    // SMOD key references (BOND / DMap / BGEO) and reports per-type counts + max scaled
    // displacement. Lets us compare working vs broken Sims and isolate the bug.
    var ptmrPath = args.Length > 1 ? args[1] : @"C:\Users\stani\OneDrive\Документы\Electronic Arts\The Sims 4\Tray\0x00000000!0x00961679e47f0089.householdbinary";
    var ptmrCacheDir = args.Length > 2 ? args[2] : @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache";
    if (!File.Exists(ptmrPath)) { Console.Error.WriteLine($"Household not found: {ptmrPath}"); return 1; }
    if (!Directory.Exists(ptmrCacheDir)) { Console.Error.WriteLine($"Cache not found: {ptmrCacheDir}"); return 1; }
    var bytes = await File.ReadAllBytesAsync(ptmrPath);

    var ptmrCache = new ProbeCacheService(Path.GetFullPath(ptmrCacheDir + "/.."));
    ptmrCache.EnsureCreated();
    var ptmrStore = new SqliteIndexStore(ptmrCache);
    await ptmrStore.InitializeAsync(CancellationToken.None);
    var ptmrCatalog = new LlamaResourceCatalogService();

    // Parse Sims (same as --probe-tray-modifiers).
    var simRegions = new List<(int NameOffset, string FirstName, string LastName)>();
    for (var i = 0; i < bytes.Length - 4; i++)
    {
        if (bytes[i] != 0x2a) continue;
        var nameLen = bytes[i + 1];
        if (nameLen == 0 || nameLen > 64) continue;
        if (i + 2 + nameLen >= bytes.Length) continue;
        var firstName = System.Text.Encoding.UTF8.GetString(bytes, i + 2, nameLen);
        if (!System.Linq.Enumerable.All(firstName, c => char.IsLetterOrDigit(c) || c == ' ')) continue;
        var lastTagOff = i + 2 + nameLen;
        if (lastTagOff >= bytes.Length || bytes[lastTagOff] != 0x32) continue;
        var lastLen = bytes[lastTagOff + 1];
        if (lastLen == 0 || lastLen > 64) continue;
        if (lastTagOff + 2 + lastLen >= bytes.Length) continue;
        var lastName = System.Text.Encoding.UTF8.GetString(bytes, lastTagOff + 2, lastLen);
        if (!System.Linq.Enumerable.All(lastName, c => char.IsLetterOrDigit(c) || c == ' ')) continue;
        simRegions.Add((i, firstName, lastName));
        i = lastTagOff + 2 + lastLen;
    }

    var sims = simRegions.Select(s => (s.FirstName, s.LastName, NameOffset: s.NameOffset, Modifiers: new List<(ulong Key, float Amount)>())).ToArray();
    for (var i = 0; i + 6 < bytes.Length; i++)
    {
        if (bytes[i] != 0x12) continue;
        var len = bytes[i + 1];
        if (len < 6 || len > 32) continue;
        if (bytes[i + 2] != 0x08) continue;
        var off = i + 3;
        ulong key = 0;
        var shift = 0;
        var ok = true;
        while (off < bytes.Length)
        {
            var b = bytes[off++];
            key |= (ulong)(b & 0x7F) << shift;
            shift += 7;
            if ((b & 0x80) == 0) break;
            if (shift > 64) { ok = false; break; }
        }
        if (!ok) continue;
        if (off >= bytes.Length || bytes[off] != 0x15) continue;
        if (off + 5 > bytes.Length) continue;
        var amount = BitConverter.ToSingle(bytes, off + 1);
        if (!float.IsFinite(amount)) continue;
        if (off + 5 != i + 2 + len) continue;
        var owner = sims.LastOrDefault(s => s.NameOffset < i);
        if (owner.Modifiers is null) continue;
        owner.Modifiers.Add((key, amount));
        i += 2 + len - 1;
    }

    // For each Sim, look up each unique key in the SMOD index and tally morph types.
    // Cache SMOD parses to avoid duplicate work.
    var smodCache = new Dictionary<ulong, (bool HasBond, bool HasShapeDmap, bool HasNormalDmap, int BgeoCount)?>();
    async Task<(bool HasBond, bool HasShapeDmap, bool HasNormalDmap, int BgeoCount)?> ResolveSmodAsync(ulong smodInstance)
    {
        if (smodCache.TryGetValue(smodInstance, out var cached)) return cached;
        var smodResources = await ptmrStore.GetResourcesByFullInstanceAsync(smodInstance, CancellationToken.None).ConfigureAwait(false);
        var smodResource = smodResources.FirstOrDefault(r => r.Key.Type == 0xC5F6763Eu);
        if (smodResource is null)
        {
            smodCache[smodInstance] = null;
            return null;
        }
        try
        {
            var smodBytes = await ptmrCatalog.GetResourceBytesAsync(smodResource.PackagePath, smodResource.Key, raw: false, CancellationToken.None, null);
            var smod = Sims4ResourceExplorer.Packages.Ts4SimModifierResource.Parse(smodBytes);
            var info = (smod.HasBondReference, smod.HasShapeDeformerMap, smod.HasNormalDeformerMap, smod.BgeoKeys.Count);
            smodCache[smodInstance] = info;
            return info;
        }
        catch
        {
            smodCache[smodInstance] = null;
            return null;
        }
    }

    Console.WriteLine($"  {"Sim",-22}  total nonZero  resolved BOND DMap BGEO  unresolved");
    foreach (var sim in sims)
    {
        var nonZero = sim.Modifiers.Where(m => Math.Abs(m.Amount) > 1e-6f).ToArray();
        var resolved = 0; var unresolved = 0;
        var bond = 0; var shape = 0; var normal = 0; var bgeo = 0;
        foreach (var m in nonZero)
        {
            var info = await ResolveSmodAsync(m.Key);
            if (info is null) { unresolved++; continue; }
            resolved++;
            if (info.Value.HasBond) bond++;
            if (info.Value.HasShapeDmap) shape++;
            if (info.Value.HasNormalDmap) normal++;
            if (info.Value.BgeoCount > 0) bgeo++;
        }
        var label = $"{sim.FirstName} {sim.LastName}";
        Console.WriteLine($"  {label,-22}  {sim.Modifiers.Count,5} {nonZero.Length,7}  {resolved,8} {bond,4} {shape,4} {bgeo,4}  {unresolved,10}");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-tray-modifiers", StringComparison.OrdinalIgnoreCase))
{
    // Walk the householdbinary protobuf and extract per-Sim modifier (key, amount) pairs.
    // The protobuf wire format: <varint tag> <payload>; tag = (field << 3) | wire_type.
    //   wire_type 0 = varint, 1 = 64-bit, 2 = length-delimited, 5 = 32-bit.
    // Modifier messages match `Modifier { uint64 key = 1; float amount = 2; }` and serialise as
    //   [12 LL] [08 <varint key>] [15 <4-byte LE float>]
    // The Sim's first/last name appear earlier as length-delimited string fields, which lets us
    // attribute each modifier block to a Sim.
    var ptmPath = args.Length > 1 ? args[1] : @"C:\Users\stani\OneDrive\Документы\Electronic Arts\The Sims 4\Tray\0x00000000!0x00961679e47f0089.householdbinary";
    if (!File.Exists(ptmPath)) { Console.Error.WriteLine($"Household not found: {ptmPath}"); return 1; }
    var bytes = await File.ReadAllBytesAsync(ptmPath);
    Console.WriteLine($"probe-tray-modifiers: {ptmPath} ({bytes.Length:N0} bytes)");

    // Pass 1: locate Sim name pairs. Each Sim has fields:
    //   2a LL <FirstName>  ← protobuf field 5 (string)
    //   32 LL <LastName>   ← protobuf field 6 (string)
    var simRegions = new List<(int NameOffset, string FirstName, string LastName)>();
    for (var i = 0; i < bytes.Length - 4; i++)
    {
        if (bytes[i] != 0x2a) continue;  // field 5 string
        var nameLen = bytes[i + 1];
        if (nameLen == 0 || nameLen > 64) continue;
        if (i + 2 + nameLen >= bytes.Length) continue;
        var firstName = System.Text.Encoding.UTF8.GetString(bytes, i + 2, nameLen);
        if (!System.Linq.Enumerable.All(firstName, c => char.IsLetterOrDigit(c) || c == ' ')) continue;
        // Look for the LastName tag immediately after.
        var lastTagOff = i + 2 + nameLen;
        if (lastTagOff >= bytes.Length || bytes[lastTagOff] != 0x32) continue;
        var lastLen = bytes[lastTagOff + 1];
        if (lastLen == 0 || lastLen > 64) continue;
        if (lastTagOff + 2 + lastLen >= bytes.Length) continue;
        var lastName = System.Text.Encoding.UTF8.GetString(bytes, lastTagOff + 2, lastLen);
        if (!System.Linq.Enumerable.All(lastName, c => char.IsLetterOrDigit(c) || c == ' ')) continue;
        simRegions.Add((i, firstName, lastName));
        i = lastTagOff + 2 + lastLen;
    }
    Console.WriteLine($"  Found {simRegions.Count} Sim name pair(s).");

    // Pass 2: scan modifier records (12 LL 08 ... 15 ...) and assign each to the most recent
    // preceding Sim by name offset.
    var sims = simRegions.Select(s => (s.FirstName, s.LastName, NameOffset: s.NameOffset, Modifiers: new List<(ulong Key, float Amount)>())).ToArray();
    var modifierStarts = new List<int>();
    for (var i = 0; i + 6 < bytes.Length; i++)
    {
        if (bytes[i] != 0x12) continue;
        var len = bytes[i + 1];
        if (len < 6 || len > 32) continue;
        if (bytes[i + 2] != 0x08) continue;  // first inner field = varint key
        // Decode varint at i+3.
        var off = i + 3;
        ulong key = 0;
        var shift = 0;
        var ok = true;
        while (off < bytes.Length)
        {
            var b = bytes[off++];
            key |= (ulong)(b & 0x7F) << shift;
            shift += 7;
            if ((b & 0x80) == 0) break;
            if (shift > 64) { ok = false; break; }
        }
        if (!ok) continue;
        if (off >= bytes.Length || bytes[off] != 0x15) continue;  // float tag (field 2, wire 5)
        if (off + 5 > bytes.Length) continue;
        var amount = BitConverter.ToSingle(bytes, off + 1);
        if (!float.IsFinite(amount)) continue;
        // Verify the modifier ends at i+2+len.
        if (off + 5 != i + 2 + len) continue;

        // Assign to nearest Sim whose NameOffset is before i.
        var owner = sims.LastOrDefault(s => s.NameOffset < i);
        if (owner.Modifiers is null) continue;
        owner.Modifiers.Add((key, amount));
        modifierStarts.Add(i);
        i += 2 + len - 1;
    }
    Console.WriteLine($"  Found {modifierStarts.Count} modifier records across all Sims.");
    Console.WriteLine();
    foreach (var s in sims)
    {
        var nonZero = s.Modifiers.Count(m => Math.Abs(m.Amount) > 1e-6f);
        Console.WriteLine($"  {s.FirstName,-12} {s.LastName,-10} (@0x{s.NameOffset:X4})  total={s.Modifiers.Count,3}  nonZero={nonZero,3}");
        // Show top 10 highest absolute weights.
        var top = s.Modifiers.OrderByDescending(m => Math.Abs(m.Amount)).Take(10);
        foreach (var m in top)
        {
            Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"      key=0x{m.Key:X16}  amount={m.Amount:0.######}"));
        }
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-tray-household", StringComparison.OrdinalIgnoreCase))
{
    // Tray householdbinary files are protobuf-encoded. We don't carry a full protobuf reader
    // yet, but we can scan for SimInfo blobs by looking for the (version UInt32, linkTableOffset
    // UInt32) header pattern that our existing Ts4SimInfoParser knows. This is a heuristic but
    // reliable enough to extract the per-Sim modifier data we need to reproduce visible bugs.
    var pthPath = args.Length > 1 ? args[1] : @"C:\Users\stani\OneDrive\Документы\Electronic Arts\The Sims 4\Tray\0x00000000!0x00961679e47f0089.householdbinary";
    if (!File.Exists(pthPath)) { Console.Error.WriteLine($"Household not found: {pthPath}"); return 1; }
    var bytes = await File.ReadAllBytesAsync(pthPath);
    Console.WriteLine($"probe-tray-household: {pthPath} ({bytes.Length:N0} bytes)");

    // Scan for plausible SimInfo embeddings. The first 8 bytes of a SimInfo are:
    //   version (UInt32 LE, expected 17..36) + linkTableOffset (UInt32 LE, expected < total bytes).
    // For each candidate, try Ts4SimInfoParser.Parse on a window starting there.
    var candidates = new List<(int Offset, int Length, Sims4ResourceExplorer.Assets.Ts4SimInfo Sim)>();
    for (var i = 0; i + 8 < bytes.Length; i++)
    {
        var version = BitConverter.ToUInt32(bytes, i);
        if (version is < 17 or > 40) continue;  // Plausible SimInfo versions seen in EA data.
        var offset = BitConverter.ToUInt32(bytes, i + 4);
        if (offset == 0 || offset > bytes.Length - i) continue;
        // Try parsing a window. Use the rest of the file from here.
        try
        {
            var window = bytes.AsMemory(i).ToArray();
            var sim = Sims4ResourceExplorer.Assets.Ts4SimInfoParser.Parse(window);
            // Minimal sanity check: realistic age + species + at least 1 modifier or sculpt.
            if (sim.SpeciesLabel is null) continue;
            if (sim.AgeLabel is null) continue;
            candidates.Add((i, window.Length, sim));
            i += 64;  // skip ahead so we don't double-detect
        }
        catch
        {
            // not a SimInfo here
        }
    }
    Console.WriteLine($"  Found {candidates.Count} candidate SimInfo blob(s) inside the protobuf.");
    foreach (var c in candidates)
    {
        Console.WriteLine($"    @0x{c.Offset:X4}  v{c.Sim.Version}  {c.Sim.SpeciesLabel,-10} {c.Sim.AgeLabel,-14} {c.Sim.GenderLabel,-8}  faceMods={c.Sim.FaceModifierCount,-3} bodyMods={c.Sim.BodyModifierCount,-3} sculpts={c.Sim.SculptCount,-3}");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--list-siminfos", StringComparison.OrdinalIgnoreCase))
{
    // List every SimInfo (025ED6F4) resource in a single package, parse it, and report
    // species/age/gender plus modifier counts. Useful for finding Sims inside a save file
    // (.save files are DBPF packages too) or a Tray export.
    var lsiPath = args.Length > 1 ? args[1] : @"C:\Users\stani\OneDrive\Документы\Electronic Arts\The Sims 4\saves\Slot_0000000f.save";
    if (!File.Exists(lsiPath)) { Console.Error.WriteLine($"Package not found: {lsiPath}"); return 1; }
    var lsiCatalog = new LlamaResourceCatalogService();
    var lsiSource = new DataSourceDefinition(
        Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
        "ListSimInfos",
        Path.GetDirectoryName(lsiPath) ?? string.Empty,
        SourceKind.Game);
    Console.WriteLine($"list-siminfos: {lsiPath} ({new FileInfo(lsiPath).Length / 1024:N0} KB)");
    var lsiScan = await lsiCatalog.ScanPackageAsync(lsiSource, lsiPath, progress: null, CancellationToken.None);
    Console.WriteLine($"  Total resources: {lsiScan.Resources.Count}");
    var typeHistogram = lsiScan.Resources
        .GroupBy(r => r.Key.Type)
        .Select(g => (Type: g.Key, Count: g.Count(), TypeName: g.First().Key.TypeName))
        .OrderByDescending(t => t.Count)
        .Take(20)
        .ToArray();
    Console.WriteLine($"  Top 20 resource types:");
    foreach (var t in typeHistogram)
    {
        Console.WriteLine($"    0x{t.Type:X8}  {t.Count,8:N0}  {t.TypeName}");
    }
    var siminfos = lsiScan.Resources.Where(r => r.Key.Type == 0x025ED6F4u).ToArray();
    Console.WriteLine($"  Found {siminfos.Length} SimInfo (025ED6F4) resource(s).");
    var success = 0;
    foreach (var resource in siminfos.Take(50))
    {
        try
        {
            var bytes = await lsiCatalog.GetResourceBytesAsync(lsiPath, resource.Key, raw: false, CancellationToken.None, null);
            var sim = Sims4ResourceExplorer.Assets.Ts4SimInfoParser.Parse(bytes);
            Console.WriteLine($"  {resource.Key.FullTgi}  v{sim.Version}  {sim.SpeciesLabel,-10} {sim.AgeLabel,-14} {sim.GenderLabel,-8}  faceMods={sim.FaceModifierCount,-3} bodyMods={sim.BodyModifierCount}");
            success++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  {resource.Key.FullTgi}  PARSE FAILED — {ex.Message}");
        }
    }
    Console.WriteLine($"  Parsed {success}/{Math.Min(50, siminfos.Length)} successfully.");
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-bgeo-morph", StringComparison.OrdinalIgnoreCase))
{
    // End-to-end check of the BGEO morph pipeline. Resolves SimInfo → SMOD → BGEO via
    // BlendGeometryResolver and reports per-morph stats (vertex count, max delta magnitude).
    var defaultProd = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache";
    var pbgmCacheDir = args.Length > 1 ? args[1] : defaultProd;
    var pbgmSimTgi = args.Length > 2 ? args[2] : null;
    if (!Directory.Exists(pbgmCacheDir)) { Console.Error.WriteLine($"Cache dir not found: {pbgmCacheDir}"); return 1; }
    Console.WriteLine($"probe-bgeo-morph: cache={pbgmCacheDir}");

    var pbgmCache = new ProbeCacheService(Path.GetFullPath(pbgmCacheDir + "/.."));
    pbgmCache.EnsureCreated();
    var pbgmStore = new SqliteIndexStore(pbgmCache);
    await pbgmStore.InitializeAsync(CancellationToken.None);

    var pbgmDb = Path.Combine(pbgmCacheDir, "index.sqlite");
    string? pbgmRootTgi = pbgmSimTgi;
    string? pbgmPkg = null;
    if (pbgmRootTgi is null)
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={pbgmDb};Mode=ReadOnly");
        conn.Open();
        using var c = conn.CreateCommand();
        c.CommandText = """
            SELECT s.root_tgi, a.package_path
            FROM sim_template_facts s
            JOIN assets a ON a.root_tgi = s.root_tgi
            WHERE lower(coalesce(s.species_label, '')) = 'human'
              AND lower(coalesce(s.age_label, '')) LIKE '%young adult%'
              AND lower(coalesce(s.gender_label, '')) = 'female'
            LIMIT 1
            """;
        using var r = c.ExecuteReader();
        if (r.Read())
        {
            pbgmRootTgi = r.GetString(0);
            pbgmPkg = r.GetString(1);
        }
    }
    if (pbgmRootTgi is null) { Console.Error.WriteLine("No SimInfo found. Pass an explicit TGI as 3rd arg."); return 1; }
    if (pbgmPkg is null)
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={pbgmDb};Mode=ReadOnly");
        conn.Open();
        using var c = conn.CreateCommand();
        c.CommandText = "SELECT package_path FROM assets WHERE root_tgi = $tgi LIMIT 1";
        c.Parameters.AddWithValue("$tgi", pbgmRootTgi);
        using var r = c.ExecuteReader();
        if (r.Read()) pbgmPkg = r.GetString(0);
    }
    if (pbgmPkg is null) { Console.Error.WriteLine($"SimInfo {pbgmRootTgi} not in assets index."); return 1; }
    Console.WriteLine($"  SimInfo: {pbgmRootTgi}");
    Console.WriteLine($"  Package: {pbgmPkg}");

    var pbgmTgiParts = pbgmRootTgi.Split(':');
    var pbgmKey = new ResourceKeyRecord(
        Convert.ToUInt32(pbgmTgiParts[0], 16),
        Convert.ToUInt32(pbgmTgiParts[1], 16),
        Convert.ToUInt64(pbgmTgiParts[2], 16),
        "SimInfo");
    var pbgmCatalog = new LlamaResourceCatalogService();
    var pbgmFakeMeta = new ResourceMetadata(
        Id: Guid.Empty,
        DataSourceId: Guid.Empty,
        SourceKind: SourceKind.Game,
        PackagePath: pbgmPkg,
        Key: pbgmKey,
        Name: Path.GetFileName(pbgmPkg),
        CompressedSize: null,
        UncompressedSize: null,
        IsCompressed: null,
        PreviewKind: PreviewKind.Metadata,
        IsPreviewable: false,
        IsExportCapable: false,
        AssetLinkageSummary: string.Empty,
        Diagnostics: string.Empty);

    var pbgmResolver = new Sims4ResourceExplorer.Assets.BlendGeometryResolver(pbgmStore, pbgmCatalog);
    var pbgmMorphs = await pbgmResolver.ResolveAsync(pbgmFakeMeta, CancellationToken.None);
    Console.WriteLine();
    Console.WriteLine($"  BlendGeometryResolver returned {pbgmMorphs.Count:N0} morph entries.");

    if (pbgmMorphs.Count == 0)
    {
        Console.WriteLine("  No morphs — either modifier weights are 0, no SMODs reference BGEOs, or BGEO resolution failed.");
        return 0;
    }

    var weights = pbgmMorphs.Select(m => m.Weight).ToArray();
    Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
        $"  Weight stats: min={weights.Min():0.######}  max={weights.Max():0.######}  abs-max={weights.Max(Math.Abs):0.######}"));

    Console.WriteLine();
    Console.WriteLine("  First 10 morphs (LOD 0 vertex range + max delta magnitude):");
    foreach (var m in pbgmMorphs.Take(10))
    {
        var bgeo = m.Bgeo;
        var lod0 = bgeo.Lods.Count > 0 ? bgeo.Lods[0] : new Sims4ResourceExplorer.Packages.Ts4BlendGeometryLod(0, 0, 0);
        var maxMag = 0f;
        var positionDeltaCount = 0;
        for (var i = 0; i < (int)lod0.NumberVertices && i < bgeo.BlendMap.Count; i++)
        {
            var blend = bgeo.BlendMap[i];
            if (!blend.PositionDelta) continue;
            positionDeltaCount++;
            if (blend.Index < 0 || blend.Index >= bgeo.VectorData.Count) continue;
            var v = bgeo.VectorData[blend.Index].ToVector3();
            var mag = v.Length();
            if (mag > maxMag) maxMag = mag;
        }
        var scaledMax = maxMag * Math.Abs(m.Weight);
        Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"    region={m.Region,3}  weight={m.Weight:0.######}  LOD0=[{lod0.IndexBase}..{lod0.IndexBase + lod0.NumberVertices - 1}] ({lod0.NumberVertices} verts, {positionDeltaCount} with posDelta)  maxRawMag={maxMag:0.######}  scaledMax={scaledMax:0.######}"));
    }

    var totalScaledMax = 0f;
    var totalAffectedVertices = 0L;
    foreach (var m in pbgmMorphs)
    {
        var bgeo = m.Bgeo;
        var lod0 = bgeo.Lods.Count > 0 ? bgeo.Lods[0] : new Sims4ResourceExplorer.Packages.Ts4BlendGeometryLod(0, 0, 0);
        for (var i = 0; i < (int)lod0.NumberVertices && i < bgeo.BlendMap.Count; i++)
        {
            var blend = bgeo.BlendMap[i];
            if (!blend.PositionDelta) continue;
            totalAffectedVertices++;
            if (blend.Index < 0 || blend.Index >= bgeo.VectorData.Count) continue;
            var v = bgeo.VectorData[blend.Index].ToVector3();
            var scaled = v.Length() * Math.Abs(m.Weight);
            if (scaled > totalScaledMax) totalScaledMax = scaled;
        }
    }
    Console.WriteLine();
    Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
        $"  Across all morphs: {totalAffectedVertices:N0} vertex positions can be deltad; max single-vertex displacement = {totalScaledMax:0.######}"));
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-dmap-morph", StringComparison.OrdinalIgnoreCase))
{
    // End-to-end check of the DMap morph pipeline:
    //   1. Load a real SimInfo from the production cache.
    //   2. Resolve DMap morphs via DeformerMapResolver — exercises the SMOD → DMap chain.
    //   3. Report counts, weight stats, sampler dimensions, and diagnostic max-displacement
    //      against a synthetic grid so we know the morpher will actually move vertices.
    var defaultProd = @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache";
    var pdmCacheDir = args.Length > 1 ? args[1] : defaultProd;
    var pdmSimTgi = args.Length > 2 ? args[2] : null;
    if (!Directory.Exists(pdmCacheDir)) { Console.Error.WriteLine($"Cache dir not found: {pdmCacheDir}"); return 1; }
    Console.WriteLine($"probe-dmap-morph: cache={pdmCacheDir}");

    var pdmCache = new ProbeCacheService(Path.GetFullPath(pdmCacheDir + "/.."));
    pdmCache.EnsureCreated();
    var pdmStore = new SqliteIndexStore(pdmCache);
    await pdmStore.InitializeAsync(CancellationToken.None);

    var pdmDb = Path.Combine(pdmCacheDir, "index.sqlite");
    string? pdmRootTgi = pdmSimTgi;
    string? pdmPkg = null;
    if (pdmRootTgi is null)
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={pdmDb};Mode=ReadOnly");
        conn.Open();
        using var c = conn.CreateCommand();
        c.CommandText = """
            SELECT s.root_tgi, a.package_path
            FROM sim_template_facts s
            JOIN assets a ON a.root_tgi = s.root_tgi
            WHERE lower(coalesce(s.species_label, '')) = 'human'
              AND lower(coalesce(s.age_label, '')) LIKE '%young adult%'
              AND lower(coalesce(s.gender_label, '')) = 'female'
            LIMIT 1
            """;
        using var r = c.ExecuteReader();
        if (r.Read())
        {
            pdmRootTgi = r.GetString(0);
            pdmPkg = r.GetString(1);
        }
    }
    if (pdmRootTgi is null) { Console.Error.WriteLine("No SimInfo found. Pass an explicit TGI as 3rd arg."); return 1; }
    if (pdmPkg is null)
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={pdmDb};Mode=ReadOnly");
        conn.Open();
        using var c = conn.CreateCommand();
        c.CommandText = "SELECT package_path FROM assets WHERE root_tgi = $tgi LIMIT 1";
        c.Parameters.AddWithValue("$tgi", pdmRootTgi);
        using var r = c.ExecuteReader();
        if (r.Read()) pdmPkg = r.GetString(0);
    }
    if (pdmPkg is null) { Console.Error.WriteLine($"SimInfo {pdmRootTgi} not in assets index."); return 1; }
    Console.WriteLine($"  SimInfo: {pdmRootTgi}");
    Console.WriteLine($"  Package: {pdmPkg}");

    var pdmTgiParts = pdmRootTgi.Split(':');
    var pdmKey = new ResourceKeyRecord(
        Convert.ToUInt32(pdmTgiParts[0], 16),
        Convert.ToUInt32(pdmTgiParts[1], 16),
        Convert.ToUInt64(pdmTgiParts[2], 16),
        "SimInfo");
    var pdmCatalog = new LlamaResourceCatalogService();
    var pdmFakeMeta = new ResourceMetadata(
        Id: Guid.Empty,
        DataSourceId: Guid.Empty,
        SourceKind: SourceKind.Game,
        PackagePath: pdmPkg,
        Key: pdmKey,
        Name: Path.GetFileName(pdmPkg),
        CompressedSize: null,
        UncompressedSize: null,
        IsCompressed: null,
        PreviewKind: PreviewKind.Metadata,
        IsPreviewable: false,
        IsExportCapable: false,
        AssetLinkageSummary: string.Empty,
        Diagnostics: string.Empty);

    var pdmResolver = new Sims4ResourceExplorer.Assets.DeformerMapResolver(pdmStore, pdmCatalog);
    var pdmMorphs = await pdmResolver.ResolveAsync(pdmFakeMeta, CancellationToken.None);
    Console.WriteLine();
    Console.WriteLine($"  DeformerMapResolver returned {pdmMorphs.Count:N0} morph entries.");

    if (pdmMorphs.Count == 0)
    {
        Console.WriteLine("  No morphs — either every modifier weight is 0, no SMODs reference DMaps, or DMap resolution failed.");
        return 0;
    }

    var shape = pdmMorphs.Count(m => !m.IsNormalMap);
    var normal = pdmMorphs.Count - shape;
    Console.WriteLine($"  Shape morphs: {shape}, Normal morphs: {normal}");
    var weights = pdmMorphs.Select(m => m.Weight).ToArray();
    Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
        $"  Weight stats: min={weights.Min():0.######}  max={weights.Max():0.######}  abs-max={weights.Max(Math.Abs):0.######}"));

    Console.WriteLine();
    Console.WriteLine("  First 10 morphs (with sampler dimensions and max sampled magnitude):");
    foreach (var m in pdmMorphs.Take(10))
    {
        var sampler = m.Sampler;
        var maxMag = 0f;
        for (var y = 0; y < sampler.Height; y++)
        {
            for (var x = 0; x < sampler.Width; x++)
            {
                var d = sampler.SampleSkinDelta(x, y);
                var mag = d.Length();
                if (mag > maxMag) maxMag = mag;
            }
        }
        var kind = m.IsNormalMap ? "normal" : "shape";
        Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"    {kind,-6}  {sampler.Width}x{sampler.Height}  region={m.Region}  weight={m.Weight:0.######}  maxSampledMag={maxMag:0.######}  scaledMax={maxMag * Math.Abs(m.Weight):0.######}"));
    }

    // What's the largest expected per-vertex displacement when all morphs are summed?
    var totalMaxScaled = 0f;
    foreach (var m in pdmMorphs.Where(x => !x.IsNormalMap))
    {
        var sampler = m.Sampler;
        for (var y = 0; y < sampler.Height; y++)
        {
            for (var x = 0; x < sampler.Width; x++)
            {
                var d = sampler.SampleSkinDelta(x, y);
                var scaled = d.Length() * Math.Abs(m.Weight);
                if (scaled > totalMaxScaled) totalMaxScaled = scaled;
            }
        }
    }
    Console.WriteLine();
    Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
        $"  Max single-pixel displacement across all shape morphs (per-vertex peak when UV1 hits the right cell) = {totalMaxScaled:0.######}"));
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--scan-canonical-heads", StringComparison.OrdinalIgnoreCase))
{
    // List every BodyType=3 (Head) CASPart shipped by EA, grouped by age/gender. The goal
    // is to identify the canonical head baseline IDs per (age, gender) — analogous to the
    // canonical nudes for Top/Bottom/Shoes.
    var schRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4\Data";
    var schCatalog = new LlamaResourceCatalogService();
    var schSource = new DataSourceDefinition(
        Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
        "ScanCanonicalHeads",
        schRoot,
        SourceKind.Game);
    var schPackages = Directory.EnumerateFiles(schRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    Console.WriteLine($"scan-canonical-heads: scanning {schPackages.Length} package(s)");

    var hits = new List<(ulong Instance, string Name, int BodyType, uint AgeGender, string Species, int Lods, bool DefBT, bool DefBTF, bool DefBTM)>();
    foreach (var pkg in schPackages)
    {
        try
        {
            var schScan = await schCatalog.ScanPackageAsync(schSource, pkg, progress: null, CancellationToken.None);
            foreach (var resource in schScan.Resources.Where(static r => r.Key.TypeName == "CASPart"))
            {
                try
                {
                    var bytes = await schCatalog.GetResourceBytesAsync(pkg, resource.Key, raw: false, CancellationToken.None, null);
                    var casPart = Sims4ResourceExplorer.Assets.Ts4CasPart.Parse(bytes);
                    if (casPart.BodyType != 3) continue;
                    if (casPart.SpeciesValue != 1) continue; // Human only
                    hits.Add((resource.Key.FullInstance, casPart.InternalName ?? "?", casPart.BodyType,
                        casPart.AgeGenderFlags, casPart.SpeciesLabel ?? "?", casPart.Lods.Count,
                        casPart.DefaultForBodyType, casPart.DefaultForBodyTypeFemale, casPart.DefaultForBodyTypeMale));
                }
                catch { /* skip */ }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  scan error in {pkg}: {ex.Message}");
        }
    }

    var grouped = hits
        .GroupBy(static h => (h.Instance, h.Name, h.AgeGender))
        .Select(static g => g.First())
        .OrderBy(static h => h.AgeGender)
        .ThenBy(static h => h.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    Console.WriteLine($"  Found {grouped.Length} unique BT=3 (Head) Human CASParts.");
    Console.WriteLine();
    Console.WriteLine("  Instance              Name                                         AgeGender  LODs  DefaultBT/F/M");
    foreach (var h in grouped)
    {
        Console.WriteLine($"  0x{h.Instance:X16}  {h.Name,-44}  0x{h.AgeGender:X8}  {h.Lods}     {h.DefBT}/{h.DefBTF}/{h.DefBTM}");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--scan-bt5-nakedlinks", StringComparison.OrdinalIgnoreCase))
{
    // Find every BodyType=5 (Full Body) CASPart that has DefaultForBodyType* set OR a
    // naked-link, then resolve its naked-link target to see what the canonical "nude full
    // body" target is. The hypothesis: there's a CASPart that, when worn, makes the Sim
    // appear nude head-to-toe; that's the part we should treat as the canonical full-body
    // baseline (and use its geometry for the waist gap).
    var sblRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4\Data";
    var sblCatalog = new LlamaResourceCatalogService();
    var sblSource = new DataSourceDefinition(
        Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
        "ScanBt5NakedLinks",
        sblRoot,
        SourceKind.Game);
    var sblPackages = Directory.EnumerateFiles(sblRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    Console.WriteLine($"scan-bt5-nakedlinks: scanning {sblPackages.Length} package(s)");

    var hits = new List<(string Pkg, ulong Instance, string Name, int BodyType, uint AgeGender, bool DefBT, bool DefBTF, bool DefBTM, bool HasNaked, ulong? NakedTarget)>();
    foreach (var pkg in sblPackages)
    {
        try
        {
            var sblScan = await sblCatalog.ScanPackageAsync(sblSource, pkg, progress: null, CancellationToken.None);
            foreach (var resource in sblScan.Resources.Where(static r => r.Key.TypeName == "CASPart"))
            {
                try
                {
                    var bytes = await sblCatalog.GetResourceBytesAsync(pkg, resource.Key, raw: false, CancellationToken.None, null);
                    var casPart = Sims4ResourceExplorer.Assets.Ts4CasPart.Parse(bytes);
                    if (casPart.BodyType != 5) continue;
                    if (!casPart.SupportsHumanSkinnedPreviewAge) continue;
                    if (!casPart.DefaultForBodyType && !casPart.DefaultForBodyTypeFemale && !casPart.DefaultForBodyTypeMale && !casPart.HasNakedLink) continue;
                    ulong? nakedTarget = null;
                    if (casPart.HasNakedLink && casPart.NakedKey < casPart.TgiList.Count)
                    {
                        var t = casPart.TgiList[casPart.NakedKey];
                        nakedTarget = t.FullInstance;
                    }
                    hits.Add((Path.GetFileName(pkg), resource.Key.FullInstance, casPart.InternalName ?? "?",
                        casPart.BodyType, casPart.AgeGenderFlags,
                        casPart.DefaultForBodyType, casPart.DefaultForBodyTypeFemale, casPart.DefaultForBodyTypeMale,
                        casPart.HasNakedLink, nakedTarget));
                }
                catch { /* skip parse errors */ }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  scan error in {pkg}: {ex.Message}");
        }
    }

    Console.WriteLine($"  Found {hits.Count} BT=5 candidates with default flags or nakedlinks.");
    var nakedTargets = hits.Where(h => h.NakedTarget is not null).Select(h => h.NakedTarget!.Value).Distinct().ToArray();
    Console.WriteLine($"  Distinct naked-link targets: {nakedTargets.Length}");
    foreach (var t in nakedTargets.Take(10)) Console.WriteLine($"    -> 0x{t:X16}");
    Console.WriteLine();
    Console.WriteLine("  First 30 hits:");
    foreach (var h in hits.Take(30))
    {
        Console.WriteLine($"    0x{h.Instance:X16}  {h.Name,-50}  age=0x{h.AgeGender:X8}  defBT/F/M={h.DefBT}/{h.DefBTF}/{h.DefBTM}  nakedTarget={(h.NakedTarget is { } n ? $"0x{n:X16}" : "(none)")}");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--scan-canonical-nudes", StringComparison.OrdinalIgnoreCase))
{
    // Enumerate every CASPart whose InternalName matches a naked-baseline pattern
    // (`_Nude`, generic `*Head`, `*Shoes`-without-clothing-keywords, etc). The goal is
    // to build the complete canonical table of EA-shipped baseline body parts that the
    // app can fall back to when a Sim's outfit is missing a slot.
    //
    // Usage: --scan-canonical-nudes <searchRoot> [outFile]
    var scnRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4\Data\Client";
    var scnOutFile = args.Length > 2 ? args[2] : Path.Combine("tmp", "canonical-nudes.txt");
    Directory.CreateDirectory(Path.GetDirectoryName(scnOutFile)!);

    var scnCatalog = new LlamaResourceCatalogService();
    var scnSource = new DataSourceDefinition(
        Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
        "ScanCanonicalNudes",
        scnRoot,
        SourceKind.Game);

    var scnPackages = Directory.EnumerateFiles(scnRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    Console.WriteLine($"scan-canonical-nudes: scanning {scnPackages.Length} package(s) under {scnRoot}");

    var hits = new List<(string Pkg, ulong Instance, string Name, int BodyType, uint AgeGender, string Species, int Lods, bool DefaultBT, bool DefaultBTF, bool DefaultBTM)>();
    foreach (var pkg in scnPackages)
    {
        try
        {
            var scnScan = await scnCatalog.ScanPackageAsync(scnSource, pkg, progress: null, CancellationToken.None);
            foreach (var resource in scnScan.Resources.Where(static r => r.Key.TypeName == "CASPart"))
            {
                try
                {
                    var bytes = await scnCatalog.GetResourceBytesAsync(pkg, resource.Key, raw: false, CancellationToken.None, null);
                    var name = Sims4ResourceExplorer.Assets.Ts4CasPart.TryReadInternalName(bytes);
                    if (name is null) continue;
                    if (!name.Contains("_Nude", StringComparison.OrdinalIgnoreCase) &&
                        !name.Contains("Nude", StringComparison.OrdinalIgnoreCase)) continue;
                    var casPart = Sims4ResourceExplorer.Assets.Ts4CasPart.Parse(bytes);
                    hits.Add((Path.GetFileName(pkg), resource.Key.FullInstance, name, casPart.BodyType,
                        casPart.AgeGenderFlags, casPart.SpeciesLabel ?? "?", casPart.Lods.Count,
                        casPart.DefaultForBodyType, casPart.DefaultForBodyTypeFemale, casPart.DefaultForBodyTypeMale));
                }
                catch { /* CASPart parse error; ignore */ }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  scan error in {pkg}: {ex.Message}");
        }
    }

    var grouped = hits
        .GroupBy(static h => (h.Instance, h.Name, h.BodyType, h.AgeGender, h.Species))
        .Select(static g => g.First())
        .OrderBy(static h => h.BodyType)
        .ThenBy(static h => h.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    var sb = new StringBuilder();
    sb.AppendLine($"Canonical baseline CASParts (InternalName contains 'Nude'): {grouped.Length} unique");
    sb.AppendLine();
    sb.AppendLine("BodyType  Instance              Name                                         AgeGender  Species  LODs  DefaultBT/F/M");
    foreach (var h in grouped)
    {
        sb.AppendLine($"{h.BodyType,8}  0x{h.Instance:X16}  {h.Name,-44}  0x{h.AgeGender:X8}  {h.Species,-7}  {h.Lods}     {h.DefaultBT}/{h.DefaultBTF}/{h.DefaultBTM}");
    }
    Console.WriteLine(sb);
    await File.WriteAllTextAsync(scnOutFile, sb.ToString());
    Console.WriteLine($"\nWrote {scnOutFile}");
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-mod", StringComparison.OrdinalIgnoreCase))
{
    // Plan D investigation — probe a mod .package to extract:
    //   - All TGIs grouped by type
    //   - For each CASPart: InternalName, BodyType, AgeGenderFlags, default-body flags,
    //     NakedKey, LOD/texture/swatch counts, TgiList type breakdown
    //   - For each Geometry referenced by a CASPart: vertex/triangle/bone counts and a
    //     small sample of bone hashes
    // The goal is to learn (a) which EA instance ID(s) the mod overrides, (b) the CASPart
    // structure modders use, and (c) the GEOM mesh shape (vertex/bone scale).
    //
    // Usage: --probe-mod <packagePath> [--geom]
    var pmPackagePath = args.Length > 1 ? args[1] : null;
    var pmIncludeGeom = args.Any(static a => string.Equals(a, "--geom", StringComparison.OrdinalIgnoreCase));
    if (pmPackagePath is null || !File.Exists(pmPackagePath))
    {
        Console.Error.WriteLine($"Usage: --probe-mod <packagePath> [--geom]");
        Console.Error.WriteLine($"Path not found: {pmPackagePath}");
        return 1;
    }

    var pmCatalog = new LlamaResourceCatalogService();
    var pmSource = new DataSourceDefinition(
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
        "ProbeMod",
        Path.GetDirectoryName(pmPackagePath) ?? pmPackagePath,
        SourceKind.Game);

    var pmScan = await pmCatalog.ScanPackageAsync(pmSource, pmPackagePath, progress: null, CancellationToken.None);
    Console.WriteLine($"probe-mod: {pmPackagePath}");
    Console.WriteLine($"probe-mod: {pmScan.Resources.Count} resources");
    Console.WriteLine();

    // Resource type histogram.
    var byType = pmScan.Resources
        .GroupBy(static r => r.Key.TypeName ?? $"(type=0x{r.Key.Type:X8})")
        .OrderByDescending(static g => g.Count())
        .ToArray();
    Console.WriteLine("Resource type histogram:");
    foreach (var g in byType)
    {
        Console.WriteLine($"  {g.Count(),5}  {g.Key}");
    }
    Console.WriteLine();

    // Dump every CASPart.
    var casParts = pmScan.Resources.Where(static r => r.Key.TypeName == "CASPart").ToArray();
    Console.WriteLine($"=== CASParts ({casParts.Length}) ===");
    foreach (var resource in casParts)
    {
        Console.WriteLine();
        Console.WriteLine($"  TGI: {resource.Key.FullTgi}");
        try
        {
            var bytes = await pmCatalog.GetResourceBytesAsync(pmPackagePath, resource.Key, raw: false, CancellationToken.None, null);
            var casPart = Ts4CasPart.Parse(bytes);
            Console.WriteLine($"  InternalName:                {casPart.InternalName ?? "(null)"}");
            Console.WriteLine($"  BodyType:                    {casPart.BodyType}");
            Console.WriteLine($"  AgeGenderFlags:              0x{casPart.AgeGenderFlags:X8}  ({casPart.AgeLabel} {casPart.GenderLabel})");
            Console.WriteLine($"  Species:                     0x{casPart.SpeciesValue:X8}  ({casPart.SpeciesLabel ?? "(null)"})");
            Console.WriteLine($"  PresetCount:                 {casPart.PresetCount}");
            Console.WriteLine($"  PartFlags1:                  0x{casPart.PartFlags1:X2}");
            Console.WriteLine($"  PartFlags2:                  0x{casPart.PartFlags2:X2}");
            Console.WriteLine($"  DefaultForBodyType:          {casPart.DefaultForBodyType}        <- if true, this OVERRIDES the EA default body for this slot");
            Console.WriteLine($"  DefaultForBodyTypeFemale:    {casPart.DefaultForBodyTypeFemale}");
            Console.WriteLine($"  DefaultForBodyTypeMale:      {casPart.DefaultForBodyTypeMale}");
            Console.WriteLine($"  NakedKey (TgiList index):    {casPart.NakedKey}");
            Console.WriteLine($"  HasNakedLink:                {casPart.HasNakedLink}");
            Console.WriteLine($"  OppositeGenderPart:          0x{casPart.OppositeGenderPart:X16}");
            Console.WriteLine($"  FallbackPart:                0x{casPart.FallbackPart:X16}");
            Console.WriteLine($"  SortLayer:                   {casPart.SortLayer}");
            Console.WriteLine($"  LODs:                        {casPart.Lods.Count}");
            Console.WriteLine($"  TextureRefs:                 {casPart.TextureReferences.Count}");
            Console.WriteLine($"  SwatchColors:                {casPart.SwatchColors.Count}");
            Console.WriteLine($"  TgiList ({casPart.TgiList.Count} entries):");
            for (var i = 0; i < casPart.TgiList.Count; i++)
            {
                var entry = casPart.TgiList[i];
                var marker = i == casPart.NakedKey ? "  <- NakedKey" : "";
                Console.WriteLine($"    [{i,2}] {entry.FullTgi}  ({entry.TypeName ?? $"0x{entry.Type:X8}"}){marker}");
            }

            if (pmIncludeGeom)
            {
                // Resolve each Geometry reference and dump basic mesh stats.
                var geomEntries = casPart.TgiList
                    .Where(static t => t.TypeName == "Geometry")
                    .ToArray();
                if (geomEntries.Length > 0)
                {
                    Console.WriteLine($"  GEOM details ({geomEntries.Length} LOD(s)):");
                    foreach (var geomKey in geomEntries)
                    {
                        try
                        {
                            // GEOMs may be in the same mod package, OR in EA packages (when the
                            // mod only overrides the CASPart but reuses EA geometry). Try the mod
                            // first, fall back to scanning game packages.
                            byte[]? geomBytes = null;
                            try
                            {
                                geomBytes = await pmCatalog.GetResourceBytesAsync(pmPackagePath, geomKey, raw: false, CancellationToken.None, null);
                            }
                            catch { }

                            if (geomBytes is null || geomBytes.Length == 0)
                            {
                                Console.WriteLine($"    [{geomKey.FullTgi}] not in mod package; skipping (would need EA scan)");
                                continue;
                            }
                            var geom = Sims4ResourceExplorer.Preview.Ts4GeomResource.Parse(geomBytes);
                            var triangleCount = geom.Indices.Count / 3;
                            var hasSkin = geom.HasSkinning;
                            var firstBones = string.Join(", ", geom.BoneHashes.Take(5).Select(static h => $"0x{h:X8}"));
                            Console.WriteLine($"    [{geomKey.FullTgi}] v{geom.Version}  verts={geom.Vertices.Count,6:N0}  tris={triangleCount,6:N0}  bones={geom.BoneHashes.Count,3}  skin={hasSkin}");
                            if (pmIncludeGeom && geom.BoneHashes.Count > 0)
                            {
                                var hashes = string.Join(" ", geom.BoneHashes.Take(geom.BoneHashes.Count > 60 ? 60 : geom.BoneHashes.Count).Select(static h => $"0x{h:X8}"));
                                Console.WriteLine($"      bones: {hashes}{(geom.BoneHashes.Count > 60 ? " ..." : "")}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"    [{geomKey.FullTgi}] parse error: {ex.Message}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Parse error: {ex.Message}");
        }
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--dump-skintone-versions", StringComparison.OrdinalIgnoreCase))
{
    // Plan 3.3 — v12 skintone TONE binary format.
    //
    // Finds every Skintone copy of the requested instance, dumps each one as annotated hex.
    // Lets us diff v6 (working) vs v12 (unsupported) byte layouts to figure out what changed.
    //
    // Usage: --dump-skintone-versions <searchRoot> <instanceHex> [outDir]
    // Example: --dump-skintone-versions "C:\GAMES\The Sims 4" 0000000000005545 tmp/skintone-versions
    var dsvSearchRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    var dsvInstanceArg = args.Length > 2 ? args[2] : null;
    var dsvOutDir = args.Length > 3 ? args[3] : Path.Combine("tmp", "skintone-versions");
    if (dsvInstanceArg is null)
    {
        Console.Error.WriteLine("Usage: --dump-skintone-versions <searchRoot> <instanceHex> [outDir]");
        return 1;
    }
    if (!ulong.TryParse(dsvInstanceArg, System.Globalization.NumberStyles.HexNumber, null, out var dsvInstance))
    {
        Console.Error.WriteLine($"Cannot parse instance hex: {dsvInstanceArg}");
        return 1;
    }
    Directory.CreateDirectory(dsvOutDir);

    var dsvCatalog = new LlamaResourceCatalogService();
    var dsvSource = new DataSourceDefinition(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        "ProbeDumpSkintoneVersions",
        dsvSearchRoot,
        SourceKind.Game);
    var dsvPackages = Directory.EnumerateFiles(dsvSearchRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    Console.WriteLine($"dump-skintone-versions: target instance=0x{dsvInstance:X16}");
    Console.WriteLine($"dump-skintone-versions: scanning {dsvPackages.Length} package(s)...");

    var collected = new List<(string Pkg, ResourceMetadata Resource, byte[] Bytes, uint Version)>();
    foreach (var pkg in dsvPackages)
    {
        try
        {
            var dsvScan = await dsvCatalog.ScanPackageAsync(dsvSource, pkg, progress: null, CancellationToken.None);
            foreach (var resource in dsvScan.Resources)
            {
                if (resource.Key.FullInstance != dsvInstance) continue;
                if (resource.Key.TypeName != "Skintone") continue;
                var bytes = await dsvCatalog.GetResourceBytesAsync(pkg, resource.Key, raw: false, CancellationToken.None, null);
                if (bytes.Length < 4) continue;
                var version = BitConverter.ToUInt32(bytes, 0);
                collected.Add((pkg, resource, bytes, version));
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  scan error in {pkg}: {ex.Message}");
        }
    }

    if (collected.Count == 0)
    {
        Console.WriteLine($"  No Skintone copies of 0x{dsvInstance:X16} found.");
        return 0;
    }

    Console.WriteLine($"  Found {collected.Count} copies:");
    foreach (var (pkg, resource, bytes, version) in collected)
    {
        Console.WriteLine($"    v{version}  {bytes.Length} bytes  {pkg}");
    }

    static string FormatHex(byte[] bytes, int bytesPerLine = 16)
    {
        var sb = new StringBuilder(bytes.Length * 4);
        for (var offset = 0; offset < bytes.Length; offset += bytesPerLine)
        {
            sb.Append($"{offset:X4}  ");
            var lineLen = Math.Min(bytesPerLine, bytes.Length - offset);
            for (var i = 0; i < bytesPerLine; i++)
            {
                if (i < lineLen) sb.Append($"{bytes[offset + i]:X2} ");
                else sb.Append("   ");
                if (i == 7) sb.Append(' ');
            }
            sb.Append(' ');
            for (var i = 0; i < lineLen; i++)
            {
                var b = bytes[offset + i];
                sb.Append(b is >= 0x20 and < 0x7F ? (char)b : '.');
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    var seenVersions = new HashSet<uint>();
    foreach (var (pkg, _, bytes, version) in collected)
    {
        if (!seenVersions.Add(version)) continue; // one example per version is enough
        var fileName = Path.Combine(dsvOutDir, $"skintone_0x{dsvInstance:X16}_v{version}.hex.txt");
        var header = $"Skintone instance 0x{dsvInstance:X16}, version {version}, {bytes.Length} bytes\nSource: {pkg}\n\n";
        await File.WriteAllTextAsync(fileName, header + FormatHex(bytes));
        Console.WriteLine($"  wrote {fileName}");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-cas-mat", StringComparison.OrdinalIgnoreCase))
{
    // Dumps every MATD texture slot for the MaterialDefinition resources that share a given
    // geometry instance ID. The instance ID appears in the app diagnostic panel as:
    //   "Selected geometry root: TYPE:GROUP:INSTANCE"
    // Reports slot name, resolved semantic, texture key, and whether it is indexed.
    // Use this to diagnose why a body diffuse texture is missing or wrong.
    //
    // Usage: --probe-cas-mat <searchRoot> <instanceHex>
    // Example: --probe-cas-mat "C:\GAMES\The Sims 4" ABCD1234EF567890
    var searchRoot    = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    var instanceArg   = args.Length > 2 ? args[2] : null;

    if (!Directory.Exists(searchRoot))
    {
        Console.Error.WriteLine($"Directory not found: {searchRoot}");
        return 1;
    }
    if (instanceArg is null)
    {
        Console.Error.WriteLine("Usage: --probe-cas-mat <searchRoot> <instanceHex>");
        Console.Error.WriteLine("  instanceHex: the 16-char hex instance from the app diagnostic 'Selected geometry root: TYPE:GROUP:INSTANCE'");
        return 1;
    }

    // Accept both bare instance "ABCD..." and full TGI "TYPE:GROUP:INSTANCE"
    var instanceStr = instanceArg.Contains(':') ? instanceArg.Split(':')[2] : instanceArg;
    if (!ulong.TryParse(instanceStr, System.Globalization.NumberStyles.HexNumber, null, out var targetInstance))
    {
        Console.Error.WriteLine($"Cannot parse instance hex from: {instanceArg}");
        return 1;
    }

    var pcmCatalog = new LlamaResourceCatalogService();
    var pcmSource  = new DataSourceDefinition(
        Guid.Parse("88888888-8888-8888-8888-888888888888"),
        "ProbeCasMat",
        searchRoot,
        SourceKind.Game);

    var pcmPackages = Directory.EnumerateFiles(searchRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    Console.WriteLine($"probe-cas-mat: target instance=0x{targetInstance:X16}");
    Console.WriteLine($"probe-cas-mat: scanning {pcmPackages.Length} package(s)...");

    // Pass 1 — collect all texture resource keys (type+group+instance) across all packages.
    // Used in pass 2 to report whether each MATD texture reference is actually indexed.
    var indexedTextureKeys = new HashSet<(uint Type, uint Group, ulong Instance)>();
    var matdResources = new List<(string Pkg, ResourceMetadata Resource)>();

    foreach (var pkg in pcmPackages)
    {
        try
        {
            var pcmScan = await pcmCatalog.ScanPackageAsync(pcmSource, pkg, progress: null, CancellationToken.None);
            foreach (var resource in pcmScan.Resources)
            {
                if (resource.Key.TypeName is "DSTImage" or "PNGImage" or "PNGImage2"
                                          or "LRLEImage" or "RLE2Image" or "RLESImage")
                {
                    indexedTextureKeys.Add((resource.Key.Type, resource.Key.Group, resource.Key.FullInstance));
                }

                if (resource.Key.FullInstance == targetInstance &&
                    resource.Key.TypeName == "MaterialDefinition")
                {
                    matdResources.Add((pkg, resource));
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  scan error in {pkg}: {ex.Message}");
        }
    }

    Console.WriteLine($"probe-cas-mat: indexed texture keys = {indexedTextureKeys.Count:N0}");
    Console.WriteLine($"probe-cas-mat: MaterialDefinition resources for 0x{targetInstance:X16} = {matdResources.Count}");

    if (matdResources.Count == 0)
    {
        Console.WriteLine("  No MaterialDefinition resources found for this instance. The MATD may be embedded in a different format.");
        return 0;
    }

    // Pass 2 — parse each MATD and dump every texture reference.
    static string GuessSlotFromSamplerName(string name)
    {
        if (name.StartsWith("sampler", StringComparison.OrdinalIgnoreCase))
            name = name["sampler".Length..];
        var n = name.ToLowerInvariant();
        if (n.Contains("diffuse") || n.Contains("albedo") || n.Contains("basecolor") || n.Contains("sourcetexture"))
            return "→ diffuse (BaseColor)";
        if (n.Contains("normal") || n.Contains("bump"))
            return "→ normal (DetailNormal)";
        if (n.Contains("spec") || n.Contains("rough") || n.Contains("gloss") || n.Contains("metal") || n.Contains("reflection"))
            return "→ specular (Specular)";
        if (n.Contains("overlay") || n.Contains("ramp") || n.Contains("paint") || n.Contains("variation"))
            return "→ overlay (Overlay)";
        if (n.Contains("alpha") || n.Contains("opacity") || n.Contains("cutout") || n.Contains("mask") || n.Contains("routingmap"))
            return "→ alpha/mask (AlphaSource/Mask)";
        if (n.Contains("emiss") || n.Contains("emit") || n.Contains("sun"))
            return "→ emissive (Emissive)";
        if (n.Contains("detail"))
            return "→ detail (Detail)";
        return "→ unknown slot";
    }

    foreach (var (pkg, resource) in matdResources.OrderBy(static x => x.Pkg, StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine($"\n  MaterialDefinition: {resource.Key.FullTgi}");
        Console.WriteLine($"  Package:            {pkg}");
        try
        {
            var bytes = await pcmCatalog.GetResourceBytesAsync(pkg, resource.Key, raw: false, CancellationToken.None, null);
            var rcol  = Ts4RcolResource.Parse(bytes);
            var matdChunk = rcol.Chunks.FirstOrDefault(static c => c.Tag is "MATD");
            if (matdChunk is null)
            {
                var mtstChunk = rcol.Chunks.FirstOrDefault(static c => c.Tag is "MTST");
                if (mtstChunk is not null)
                    Console.WriteLine("  (contains MTST — multiple material states; showing first resolved MATD)");
                if (mtstChunk is not null)
                {
                    var mtst = Ts4MtstChunk.Parse(mtstChunk.Data.Span);
                    var entries = mtst.Entries.Where(static e => !e.Reference.IsNull).ToList();
                    if (entries.Count > 0)
                        matdChunk = rcol.ResolveChunk(entries[0].Reference);
                }
            }
            if (matdChunk is null)
            {
                Console.WriteLine("  (no MATD chunk found in this RCOL)");
                continue;
            }
            var matd = Ts4MatdChunk.Parse(matdChunk.Data.Span);
            Console.WriteLine($"  Shader:  {matd.ShaderName ?? "(none)"}  hash=0x{matd.ShaderNameHash:X8}");
            Console.WriteLine($"  Texture references ({matd.TextureReferences.Count}):");
            if (matd.TextureReferences.Count == 0)
            {
                Console.WriteLine("    (none)");
                continue;
            }
            foreach (var texRef in matd.TextureReferences)
            {
                var rawSlot    = texRef.Slot ?? "(null)";
                var keyTgi     = $"{texRef.Key.Type:X8}:{texRef.Key.Group:X8}:{texRef.Key.Instance:X16}";
                var isNull     = texRef.Key.Type == 0 && texRef.Key.Group == 0 && texRef.Key.Instance == 0;
                var isIndexed  = !isNull && indexedTextureKeys.Contains((texRef.Key.Type, texRef.Key.Group, texRef.Key.Instance));
                var slotGuess  = GuessSlotFromSamplerName(rawSlot);
                var found      = isNull ? "[null key]" : isIndexed ? "[INDEXED ✓]" : "[NOT FOUND ✗]";
                Console.WriteLine($"    slot={rawSlot,-30}  propHash=0x{texRef.PropertyHash:X8}  key={keyTgi}  {found}  {slotGuess}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  parse error: {ex.Message}");
        }
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-uv-mapping", StringComparison.OrdinalIgnoreCase))
{
    // Plan 3.1 — Decode `uvMapping` packed-data fall-through cases.
    //
    // Scans every MATD chunk in every package under <searchRoot>, runs the production
    // Ts4MaterialDecoder.TryInterpretPackedUvProperty() against every `uvMapping`
    // property, and buckets the outcome. The "decode-fail" bucket is the actual gap
    // — those properties hit the "uvMapping is stored as packed data and is not yet
    // decoded by the generic material pipeline." note in MaterialDecoding.cs:153.
    //
    // For decode-fail rows we dump the raw uint32 words plus their re-interpretations
    // (LE-float32, half-float pairs, normalized-uint16) so we can identify whether a
    // third encoding exists or whether those properties are non-UV data misnamed.
    //
    // Usage: --probe-uv-mapping <searchRoot> [maxMaterials] [outDir]
    // Example: --probe-uv-mapping "C:\GAMES\The Sims 4" 20000 tmp/uv-mapping
    var puvSearchRoot   = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    var puvMaxMaterials = args.Length > 2 && int.TryParse(args[2], out var puvMax) ? puvMax : int.MaxValue;
    var puvOutDir       = args.Length > 3 ? args[3] : Path.Combine("tmp", "uv-mapping");

    if (!Directory.Exists(puvSearchRoot))
    {
        Console.Error.WriteLine($"Directory not found: {puvSearchRoot}");
        return 1;
    }
    Directory.CreateDirectory(puvOutDir);

    var puvCatalog = new LlamaResourceCatalogService();
    var puvSource  = new DataSourceDefinition(
        Guid.Parse("99999999-9999-9999-9999-999999999999"),
        "ProbeUvMapping",
        puvSearchRoot,
        SourceKind.Game);

    var puvPackages = Directory.EnumerateFiles(puvSearchRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    Console.WriteLine($"probe-uv-mapping: scanning {puvPackages.Length} package(s) under {puvSearchRoot}");
    Console.WriteLine($"probe-uv-mapping: cap at {puvMaxMaterials:N0} materials, output -> {puvOutDir}");

    // Buckets for the histogram.
    var bucketDecodedHalfFloat = 0;
    var bucketDecodedNormUint16 = 0;
    var bucketResourceKeyMarker = 0;
    var bucketAtlasWindowImplausible = 0;
    var bucketNoPackedData = 0;
    var bucketDecodeFail = 0;
    var bucketNoUvMappingProperty = 0;
    var totalMaterials = 0;
    var representationCounts = new Dictionary<string, int>(StringComparer.Ordinal);

    // Per-shader histogram of decode-fails (so we know which shaders to study first).
    var decodeFailByShader = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var decodeFailSamples = new List<string>(); // first ~20 samples written verbatim

    foreach (var pkg in puvPackages)
    {
        if (totalMaterials >= puvMaxMaterials) break;
        Sims4ResourceExplorer.Core.PackageScanResult puvScan;
        try
        {
            puvScan = await puvCatalog.ScanPackageAsync(puvSource, pkg, progress: null, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  scan error in {pkg}: {ex.Message}");
            continue;
        }

        foreach (var resource in puvScan.Resources)
        {
            if (totalMaterials >= puvMaxMaterials) break;
            if (resource.Key.TypeName != "MaterialDefinition") continue;

            byte[] bytes;
            try
            {
                bytes = await puvCatalog.GetResourceBytesAsync(pkg, resource.Key, raw: false, CancellationToken.None, null);
            }
            catch
            {
                continue;
            }

            Sims4ResourceExplorer.Preview.Ts4RcolResource rcol;
            try
            {
                rcol = Sims4ResourceExplorer.Preview.Ts4RcolResource.Parse(bytes);
            }
            catch
            {
                continue;
            }

            var matdChunks = rcol.Chunks.Where(static c => c.Tag is "MATD").ToList();
            // Also follow MTST -> referenced MATDs.
            foreach (var mtstChunk in rcol.Chunks.Where(static c => c.Tag is "MTST"))
            {
                Sims4ResourceExplorer.Preview.Ts4MtstChunk mtst;
                try { mtst = Sims4ResourceExplorer.Preview.Ts4MtstChunk.Parse(mtstChunk.Data.Span); }
                catch { continue; }
                foreach (var entry in mtst.Entries.Where(static e => !e.Reference.IsNull))
                {
                    var resolved = rcol.ResolveChunk(entry.Reference);
                    if (resolved is { Tag: "MATD" } && !matdChunks.Contains(resolved))
                        matdChunks.Add(resolved);
                }
            }

            foreach (var matdChunk in matdChunks)
            {
                if (totalMaterials >= puvMaxMaterials) break;
                Sims4ResourceExplorer.Preview.Ts4MatdChunk matd;
                try { matd = Sims4ResourceExplorer.Preview.Ts4MatdChunk.Parse(matdChunk.Data.Span); }
                catch { continue; }
                totalMaterials++;

                var ir = Sims4ResourceExplorer.Preview.Ts4ShaderSemantics.BuildMaterialIr(matd);
                var uvMappingProps = ir.Properties
                    .Where(static p => string.Equals(p.Name, "uvMapping", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (uvMappingProps.Length == 0)
                {
                    bucketNoUvMappingProperty++;
                    continue;
                }

                foreach (var prop in uvMappingProps)
                {
                    var repKey = prop.ValueRepresentation.ToString();
                    representationCounts[repKey] = representationCounts.GetValueOrDefault(repKey) + 1;

                    if (prop.PackedUInt32Values is not { Length: > 0 })
                    {
                        bucketNoPackedData++;
                        continue;
                    }

                    var ok = Sims4ResourceExplorer.Preview.Ts4MaterialDecoder.TryInterpretPackedUvProperty(
                        prop,
                        new Sims4ResourceExplorer.Preview.Ts4TextureUvMapping(0, 1f, 1f, 0f, 0f),
                        out _,
                        out var note);

                    if (ok)
                    {
                        if (note.Contains("half-float", StringComparison.OrdinalIgnoreCase))
                            bucketDecodedHalfFloat++;
                        else if (note.Contains("normalized-uint16", StringComparison.OrdinalIgnoreCase))
                            bucketDecodedNormUint16++;
                        continue;
                    }

                    if (!string.IsNullOrEmpty(note))
                    {
                        // The decoder itself emitted a note explaining why it returned false.
                        // Today the only such note is the resource-key marker rejection.
                        if (note.Contains("texture resource-key marker", StringComparison.OrdinalIgnoreCase))
                            bucketResourceKeyMarker++;
                        else
                            bucketAtlasWindowImplausible++;
                        continue;
                    }

                    // Genuine decode-fail bucket — this is the one the production code
                    // labels "uvMapping is stored as packed data and is not yet decoded".
                    bucketDecodeFail++;
                    var shaderKey = string.IsNullOrEmpty(matd.ShaderName) ? $"Shader_{matd.ShaderNameHash:X8}" : matd.ShaderName;
                    decodeFailByShader[shaderKey] = decodeFailByShader.GetValueOrDefault(shaderKey) + 1;

                    if (decodeFailSamples.Count < 25)
                    {
                        var packed = prop.PackedUInt32Values;
                        var hexWords = string.Join(" ", packed.Select(static w => $"0x{w:X8}"));
                        var asFloat32 = string.Join(", ", packed.Select(static w =>
                        {
                            var bytes32 = BitConverter.GetBytes(w);
                            return BitConverter.ToSingle(bytes32, 0).ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
                        }));
                        var asHalfPairs = string.Join(", ", packed.Select(static w =>
                        {
                            var lo = (ushort)(w & 0xFFFF);
                            var hi = (ushort)(w >> 16);
                            return $"({HalfFloatToSingle(lo):0.###}, {HalfFloatToSingle(hi):0.###})";
                        }));
                        var asNormPairs = string.Join(", ", packed.Select(static w =>
                        {
                            var lo = (ushort)(w & 0xFFFF);
                            var hi = (ushort)(w >> 16);
                            return $"({lo / 65535f:0.###}, {hi / 65535f:0.###})";
                        }));
                        var sample = $"  shader={shaderKey}\n" +
                                     $"  matName={matd.MaterialName ?? "(null)"}\n" +
                                     $"  matd-tgi={resource.Key.FullTgi}\n" +
                                     $"  pkg={pkg}\n" +
                                     $"  propHash=0x{prop.Hash:X8} arity={prop.Arity} rawType=0x{prop.RawType:X8} category={prop.Category}\n" +
                                     $"  packed-words: {hexWords}\n" +
                                     $"  as-float32   : [{asFloat32}]\n" +
                                     $"  as-half-pairs: [{asHalfPairs}]\n" +
                                     $"  as-norm-pairs: [{asNormPairs}]\n";
                        decodeFailSamples.Add(sample);
                    }
                }
            }
        }
    }

    Console.WriteLine();
    Console.WriteLine($"probe-uv-mapping: scanned {totalMaterials:N0} MATD chunks total");
    Console.WriteLine();
    Console.WriteLine("Bucket histogram (counts uvMapping properties, not materials):");
    Console.WriteLine($"  decoded as half-float        : {bucketDecodedHalfFloat,8:N0}");
    Console.WriteLine($"  decoded as normalized-uint16 : {bucketDecodedNormUint16,8:N0}");
    Console.WriteLine($"  rejected as resource-key     : {bucketResourceKeyMarker,8:N0}");
    Console.WriteLine($"  rejected atlas-window range  : {bucketAtlasWindowImplausible,8:N0}");
    Console.WriteLine($"  no PackedUInt32Values present: {bucketNoPackedData,8:N0}");
    Console.WriteLine($"  DECODE-FAIL (the gap)        : {bucketDecodeFail,8:N0}  <-- 'not yet decoded' fall-through");
    Console.WriteLine($"  materials w/o uvMapping prop : {bucketNoUvMappingProperty,8:N0}");

    Console.WriteLine();
    Console.WriteLine("uvMapping ValueRepresentation distribution (only for materials that HAVE the property):");
    foreach (var (rep, count) in representationCounts.OrderByDescending(static kv => kv.Value))
    {
        Console.WriteLine($"  {count,8:N0}  {rep}");
    }

    if (decodeFailByShader.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Decode-fails by shader (top 20):");
        foreach (var (shader, count) in decodeFailByShader.OrderByDescending(static kv => kv.Value).Take(20))
        {
            Console.WriteLine($"  {count,6:N0}  {shader}");
        }
    }

    var summaryPath = Path.Combine(puvOutDir, "summary.txt");
    var samplesPath = Path.Combine(puvOutDir, "decode-fail-samples.txt");
    await File.WriteAllTextAsync(summaryPath,
        $"materials scanned: {totalMaterials}\n" +
        $"decoded half-float: {bucketDecodedHalfFloat}\n" +
        $"decoded norm-uint16: {bucketDecodedNormUint16}\n" +
        $"resource-key marker: {bucketResourceKeyMarker}\n" +
        $"atlas-window rejected: {bucketAtlasWindowImplausible}\n" +
        $"no packed data: {bucketNoPackedData}\n" +
        $"decode-fail: {bucketDecodeFail}\n" +
        $"no uvMapping property: {bucketNoUvMappingProperty}\n" +
        "\nDecode-fails by shader:\n" +
        string.Join("\n", decodeFailByShader.OrderByDescending(static kv => kv.Value).Select(kv => $"  {kv.Value,6}  {kv.Key}")));
    await File.WriteAllTextAsync(samplesPath, string.Join("\n----\n", decodeFailSamples));
    Console.WriteLine();
    Console.WriteLine($"summary -> {summaryPath}");
    Console.WriteLine($"samples -> {samplesPath} ({decodeFailSamples.Count} entries)");
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--find-by-name", StringComparison.OrdinalIgnoreCase))
{
    var searchRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    var nameToFind = args.Length > 2 ? args[2] : string.Empty;
    if (string.IsNullOrEmpty(nameToFind))
    {
        Console.Error.WriteLine("Usage: --find-by-name <searchRoot> <name>");
        Console.Error.WriteLine("  Hashes <name> with FNV-1 64-bit (TS4 scheme: lowercase ASCII, multiply-then-XOR) and scans all .package files for that instance.");
        Console.Error.WriteLine("  Useful names: yfBodyComplete_lod0, ymBodyComplete_lod0, cuBodyComplete_lod0, auRig, cuRig, nuRig");
        return 2;
    }
    if (!Directory.Exists(searchRoot))
    {
        Console.Error.WriteLine($"Directory not found: {searchRoot}");
        return 1;
    }

    // FNV-1 64-bit hash: same algorithm as TS4SimRipper FNVhash.FNV64 — lowercase, ASCII, multiply THEN XOR
    const ulong fnvOffsetBasis = 14695981039346656037UL;
    const ulong fnvPrime = 1099511628211UL;
    var nameHash = fnvOffsetBasis;
    foreach (var b in Encoding.ASCII.GetBytes(nameToFind.ToLowerInvariant()))
    {
        unchecked { nameHash *= fnvPrime; }
        nameHash ^= b;
    }

    Console.WriteLine($"find-by-name: name={nameToFind}  fnv1a64=0x{nameHash:X16}");

    var fnbPackages = Directory.EnumerateFiles(searchRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    Console.WriteLine($"find-by-name: scanning {fnbPackages.Length} package(s) under {searchRoot}");

    var fnbCatalog = new LlamaResourceCatalogService();
    var fnbSource = new DataSourceDefinition(
        Guid.Parse("55555555-5555-5555-5555-555555555555"),
        "ProbeFindByName",
        searchRoot,
        SourceKind.Game);
    var fnbMatches = 0;
    foreach (var pkg in fnbPackages)
    {
        try
        {
            var fnbScan = await fnbCatalog.ScanPackageAsync(fnbSource, pkg, progress: null, CancellationToken.None);
            foreach (var r in fnbScan.Resources.Where(r => r.Key.FullInstance == nameHash))
            {
                Console.WriteLine($"MATCH  type={r.Key.TypeName,-20} tgi={r.Key.FullTgi}  pkg={pkg}");
                fnbMatches++;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"scan failed {pkg}: {ex.Message}");
        }
    }
    Console.WriteLine($"find-by-name: total matches: {fnbMatches}");
    return fnbMatches > 0 ? 0 : 3;
}

if (args.Length > 0 && string.Equals(args[0], "--probe-rig", StringComparison.OrdinalIgnoreCase))
{
    // Full-path simulation: FNV-1 name hash → index scan → raw bytes → parse → bone report.
    // Validates the complete canonical-rig-fallback chain in TryResolveRigAsync without the UI.
    var searchRoot = args.Length > 1 ? args[1] : @"C:\GAMES\The Sims 4";
    var rigNames = args.Length > 2
        ? args[2..].ToArray()
        : ["auRig", "cuRig", "nuRig"];

    if (!Directory.Exists(searchRoot))
    {
        Console.Error.WriteLine($"Directory not found: {searchRoot}");
        return 1;
    }

    static ulong ProbeFnv64(string name)
    {
        const ulong basis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var h = basis;
        foreach (var b in Encoding.ASCII.GetBytes(name.ToLowerInvariant()))
        {
            unchecked { h *= prime; }
            h ^= b;
        }
        return h;
    }

    static string[] ParseRigBoneNames(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes, writable: false);
        using var br = new BinaryReader(ms);
        var major = br.ReadUInt32();
        var minor = br.ReadUInt32();
        if ((major is not (3u or 4u)) || (minor is not (1u or 2u)))
            throw new InvalidDataException($"Unexpected rig version {major}.{minor}");
        var count = br.ReadInt32();
        var names = new string[count];
        for (var i = 0; i < count; i++)
        {
            br.ReadBytes(3 * 4);               // position (3 floats)
            br.ReadBytes(4 * 4);               // rotation (4 floats)
            br.ReadBytes(3 * 4);               // scale (3 floats)
            var nameLen = br.ReadInt32();
            names[i] = new string(br.ReadChars(nameLen));
            br.ReadInt32();                    // skip
            br.ReadInt32();                    // parentIndex
            br.ReadUInt32();                   // nameHash
            br.ReadUInt32();                   // skip
        }
        return names;
    }

    var probeCatalog = new LlamaResourceCatalogService();
    var probeSource = new DataSourceDefinition(
        Guid.Parse("66666666-6666-6666-6666-666666666666"),
        "ProbeRig",
        searchRoot,
        SourceKind.Game);

    var probePackages = Directory.EnumerateFiles(searchRoot, "*.package", SearchOption.AllDirectories)
        .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    Console.WriteLine($"probe-rig: scanning {probePackages.Length} package(s) under {searchRoot}");

    foreach (var rigName in rigNames)
    {
        var hash = ProbeFnv64(rigName);
        Console.WriteLine($"\nprobe-rig: {rigName}  fnv1-64=0x{hash:X16}");
        var found = false;
        foreach (var pkg in probePackages)
        {
            try
            {
                var rigScan = await probeCatalog.ScanPackageAsync(probeSource, pkg, progress: null, CancellationToken.None);
                var rigResource = rigScan.Resources.FirstOrDefault(r => r.Key.FullInstance == hash && r.Key.TypeName == "Rig");
                if (rigResource is null) continue;

                Console.WriteLine($"  found: {rigResource.Key.FullTgi}  pkg={pkg}");
                var rigKey = rigResource.Key;
                var bytes = await probeCatalog.GetResourceBytesAsync(pkg, rigKey, raw: false, CancellationToken.None, null);
                var names = ParseRigBoneNames(bytes);
                Console.WriteLine($"  parsed: {names.Length} bone(s)");
                Console.WriteLine($"  first 8: {string.Join(", ", names.Take(8))}");
                found = true;
                break; // one copy is enough
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  error in {pkg}: {ex.Message}");
            }
        }
        if (!found)
            Console.WriteLine($"  NOT FOUND in any package.");
    }
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--dump-texture", StringComparison.OrdinalIgnoreCase))
{
    // Fetches one or more texture instances directly from game packages and saves them as PNG.
    //
    // Usage: --dump-texture <hexInstance[,hexInstance,...]> [gameRoot] [outputDir]
    // Example: --dump-texture 3275143DEC141D18,A4F4ECD610FF3176
    var dtHexList  = args.Length > 1 ? args[1] : "3275143DEC141D18";
    var dtRoot     = args.Length > 2 ? args[2] : @"C:\GAMES\The Sims 4";
    var dtOutDir   = args.Length > 3 ? args[3] : Path.Combine(Environment.CurrentDirectory, "tmp", "textures");

    if (!Directory.Exists(dtRoot)) { Console.Error.WriteLine($"Game root not found: {dtRoot}"); return 1; }
    Directory.CreateDirectory(dtOutDir);

    var dtInstances = dtHexList.Split(',')
        .Select(static h => h.Trim().Replace("0x","").Replace("0X",""))
        .Where(static h => h.Length > 0)
        .Select(static h => ulong.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out var v) ? v : 0UL)
        .Where(static v => v != 0UL)
        .ToHashSet();

    Console.WriteLine($"dump-texture: fetching {dtInstances.Count} instance(s)");
    Console.WriteLine($"  game root: {dtRoot}   output: {dtOutDir}");
    Console.WriteLine($"  Scanning packages ...");

    var dtTextureByInst = new Dictionary<ulong, List<(string Pkg, ResourceKeyRecord Key)>>();
    var dtCatalog = new LlamaResourceCatalogService();
    var dtSource  = new DataSourceDefinition(Guid.NewGuid(), "DtScan", dtRoot, SourceKind.Game);
    foreach (var pkg in Directory.EnumerateFiles(dtRoot, "*.package", SearchOption.AllDirectories)
                                  .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
    {
        try
        {
            var dtPkgScan = await dtCatalog.ScanPackageAsync(dtSource, pkg, progress: null, CancellationToken.None);
            foreach (var r in dtPkgScan.Resources)
            {
                if (!dtInstances.Contains(r.Key.FullInstance)) continue;
                if (!dtTextureByInst.TryGetValue(r.Key.FullInstance, out var dtLst))
                {
                    dtLst = [];
                    dtTextureByInst[r.Key.FullInstance] = dtLst;
                }
                dtLst.Add((pkg, r.Key));
            }
        }
        catch { }
    }
    Console.WriteLine($"  Located {dtTextureByInst.Count}/{dtInstances.Count} requested instance(s) across packages.");

    foreach (var (inst, candidates) in dtTextureByInst)
    {
        byte[]? png = null;
        string? fromPkg = null;
        foreach (var (pkg, key) in candidates)
        {
            try
            {
                var bytes = await dtCatalog.GetTexturePngAsync(pkg, key, CancellationToken.None);
                if (bytes is { Length: > 0 }) { png = bytes; fromPkg = Path.GetFileName(pkg); break; }
            }
            catch { }
        }
        if (png is { Length: > 0 })
        {
            int w = png.Length >= 24 ? (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19] : 0;
            int h = png.Length >= 24 ? (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23] : 0;
            var fn = $"0x{inst:X16}.png";
            await File.WriteAllBytesAsync(Path.Combine(dtOutDir, fn), png, CancellationToken.None);
            Console.WriteLine($"  0x{inst:X16} → {fn}  [{w}×{h}] from {fromPkg}");
        }
        else Console.WriteLine($"  0x{inst:X16}: not decodable");
    }
    foreach (var missing in dtInstances.Where(i => !dtTextureByInst.ContainsKey(i)))
        Console.WriteLine($"  0x{missing:X16}: not found in packages");

    Console.WriteLine($"\n  Done. Open {dtOutDir}");
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--dump-face-overlays", StringComparison.OrdinalIgnoreCase))
{
    // Dumps EVERY face-adjacent CAS part diffuse texture for one Sim to disk,
    // covering all body types in the body-driving outfit (not just the ones the
    // app currently applies as atlas overlays). Lets us visually identify which
    // layer contributes the dark patches visible on the rendered Sim's face.
    //
    // Usage: --dump-face-overlays [dbPath] [gameRoot] [simTgi|auto] [outputDir]
    // Example: --dump-face-overlays auto tmp\face-overlays
    var dfoDb      = args.Length > 1 ? args[1] : @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    var dfoRoot    = args.Length > 2 ? args[2] : @"C:\GAMES\The Sims 4";
    var dfoSimTgi  = args.Length > 3 ? args[3] : "auto";
    var dfoOutDir  = args.Length > 4 ? args[4] : Path.Combine(Environment.CurrentDirectory, "tmp", "face-overlays");

    if (!File.Exists(dfoDb))    { Console.Error.WriteLine($"DB not found: {dfoDb}");       return 1; }
    if (!Directory.Exists(dfoRoot)) { Console.Error.WriteLine($"Game root not found: {dfoRoot}"); return 1; }
    Directory.CreateDirectory(dfoOutDir);
    Console.WriteLine($"dump-face-overlays: db={dfoDb}");
    Console.WriteLine($"  game root: {dfoRoot}   output: {dfoOutDir}");

    // Direct SQL for sim_template_body_parts (the index already has this denormalized).
    var dfoConn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dfoDb};Mode=ReadOnly");
    dfoConn.Open();

    // Resolve target Sim TGI, age_label, and gender_label.
    string? dfoTgi      = string.Equals(dfoSimTgi, "auto", StringComparison.OrdinalIgnoreCase) ? null : dfoSimTgi;
    string  dfoAgeLabel = "Young Adult";
    string  dfoGender   = "Female";
    if (dfoTgi is null)
    {
        using var c = dfoConn.CreateCommand();
        c.CommandText = """
            SELECT s.root_tgi, coalesce(s.age_label,'Young Adult'), coalesce(s.gender_label,'Female')
            FROM sim_template_facts s
            JOIN sim_template_body_parts b ON b.root_tgi = s.root_tgi
            WHERE lower(coalesce(s.species_label,'')) = 'human'
              AND lower(coalesce(s.age_label,'')) LIKE '%young adult%'
              AND lower(coalesce(s.gender_label,'')) = 'female'
              AND b.is_body_driving = 1
            GROUP BY s.root_tgi
            LIMIT 1
            """;
        using var r = c.ExecuteReader();
        if (r.Read()) { dfoTgi = r.GetString(0); dfoAgeLabel = r.GetString(1); dfoGender = r.GetString(2); }
    }
    else
    {
        using var c = dfoConn.CreateCommand();
        c.CommandText = "SELECT coalesce(age_label,'Young Adult'), coalesce(gender_label,'Female') FROM sim_template_facts WHERE root_tgi = $tgi LIMIT 1";
        c.Parameters.AddWithValue("$tgi", dfoTgi);
        using var r = c.ExecuteReader();
        if (r.Read()) { dfoAgeLabel = r.GetString(0); dfoGender = r.GetString(1); }
    }
    if (dfoTgi is null) { Console.WriteLine("No Sim found in index."); return 1; }
    Console.WriteLine($"\n  Sim TGI: {dfoTgi}  ({dfoAgeLabel} / {dfoGender})");

    // Pull all body-driving outfit parts ordered by body type.
    var dfoParts = new List<(int BodyType, string Label, string InstanceHex)>();
    using (var c = dfoConn.CreateCommand())
    {
        c.CommandText = """
            SELECT body_type, coalesce(body_type_label,'?'), part_instance_hex
            FROM sim_template_body_parts
            WHERE root_tgi = $tgi
              AND is_body_driving = 1
            ORDER BY body_type
            """;
        c.Parameters.AddWithValue("$tgi", dfoTgi);
        using var r = c.ExecuteReader();
        while (r.Read())
        {
            var hex = r.IsDBNull(2) ? "" : r.GetString(2);
            if (!string.IsNullOrEmpty(hex))
                dfoParts.Add((r.GetInt32(0), r.GetString(1), hex));
        }
    }
    Console.WriteLine($"\n  Body-driving outfit parts: {dfoParts.Count}");
    foreach (var (bt, label, hex) in dfoParts)
        Console.WriteLine($"    bt={bt,3} ({label,-22}) instance={hex}");

    // Scan packages to build texture and CASPart instance maps.
    Console.WriteLine($"\n  Scanning packages in {dfoRoot} ...");
    var dfoTextureByInst = new Dictionary<ulong, List<(string Pkg, ResourceKeyRecord Key)>>();
    var dfoCasPartByInst = new Dictionary<ulong, (string Pkg, ResourceKeyRecord Key)>();
    var dfoCatalog = new LlamaResourceCatalogService();
    var dfoSource  = new DataSourceDefinition(Guid.NewGuid(), "DfoScan", dfoRoot, SourceKind.Game);
    foreach (var pkg in Directory.EnumerateFiles(dfoRoot, "*.package", SearchOption.AllDirectories)
                                  .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
    {
        try
        {
            var pkgScan = await dfoCatalog.ScanPackageAsync(dfoSource, pkg, progress: null, CancellationToken.None);
            foreach (var r in pkgScan.Resources)
            {
                if (r.Key.TypeName is "DSTImage" or "PNGImage" or "PNGImage2"
                                   or "LRLEImage" or "RLE2Image" or "RLESImage"
                    || r.Key.Type == 0x00B2D882u)
                {
                    if (!dfoTextureByInst.TryGetValue(r.Key.FullInstance, out var lst))
                    {
                        lst = [];
                        dfoTextureByInst[r.Key.FullInstance] = lst;
                    }
                    lst.Add((pkg, r.Key));
                }
                else if (string.Equals(r.Key.TypeName, "CASPart", StringComparison.OrdinalIgnoreCase))
                {
                    dfoCasPartByInst.TryAdd(r.Key.FullInstance, (pkg, r.Key));
                }
            }
        }
        catch (Exception ex) { Console.Error.WriteLine($"  scan error {Path.GetFileName(pkg)}: {ex.Message}"); }
    }
    Console.WriteLine($"  indexed: {dfoTextureByInst.Count:N0} texture instances, {dfoCasPartByInst.Count:N0} CASPart instances");

    // For each outfit part, fetch and save its diffuse texture.
    Console.WriteLine($"\n  Fetching diffuse PNGs ...");
    static async Task<byte[]?> FetchTexturePng(
        ulong instance,
        Dictionary<ulong, List<(string Pkg, ResourceKeyRecord Key)>> byInst,
        LlamaResourceCatalogService cat)
    {
        if (!byInst.TryGetValue(instance, out var candidates)) return null;
        foreach (var (pkg, key) in candidates)
        {
            try
            {
                var bytes = await cat.GetTexturePngAsync(pkg, key, CancellationToken.None);
                if (bytes is { Length: > 0 }) return bytes;
            }
            catch { }
        }
        return null;
    }

    foreach (var (bt, label, instanceHex) in dfoParts)
    {
        var hexClean = instanceHex.TrimStart('0').TrimStart('x', 'X');
        if (!ulong.TryParse(hexClean, System.Globalization.NumberStyles.HexNumber, null, out var partInst)
            && !ulong.TryParse(instanceHex.Replace("0x","").Replace("0X",""), System.Globalization.NumberStyles.HexNumber, null, out partInst))
        {
            Console.WriteLine($"  bt={bt,3} ({label,-22}): cannot parse instance '{instanceHex}'");
            continue;
        }

        if (!dfoCasPartByInst.TryGetValue(partInst, out var casRef))
        {
            Console.WriteLine($"  bt={bt,3} ({label,-22}) 0x{partInst:X16}: CASPart not found in packages");
            continue;
        }

        Ts4CasPart? casPart = null;
        try
        {
            var casBytes = await dfoCatalog.GetResourceBytesAsync(casRef.Pkg, casRef.Key, raw: false, CancellationToken.None);
            casPart = Ts4CasPart.Parse(casBytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  bt={bt,3} ({label,-22}) 0x{partInst:X16}: CASPart parse error: {ex.Message}");
            continue;
        }

        var diffuseRef = casPart.TextureReferences
            .FirstOrDefault(static t => string.Equals(t.Slot, "diffuse", StringComparison.OrdinalIgnoreCase));
        if (diffuseRef.Key is null || diffuseRef.Key.Type == 0 || diffuseRef.Key.FullInstance == 0)
        {
            Console.WriteLine($"  bt={bt,3} ({label,-22}) 0x{partInst:X16}: no diffuse in CASPart (slots: {string.Join(", ", casPart.TextureReferences.Select(static t => t.Slot))})");
            continue;
        }

        var png = await FetchTexturePng(diffuseRef.Key.FullInstance, dfoTextureByInst, dfoCatalog);
        if (png is null)
        {
            Console.WriteLine($"  bt={bt,3} ({label,-22}) 0x{partInst:X16}: diffuse 0x{diffuseRef.Key.FullInstance:X16} not decodable");
            continue;
        }

        int w = png.Length >= 24 ? (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19] : 0;
        int h = png.Length >= 24 ? (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23] : 0;
        var safeLabel = label.Replace(' ', '_').Replace('/', '-').Replace('\\', '-');
        var fileName = $"bt{bt:D3}_{safeLabel}_{diffuseRef.Key.FullInstance:X16}.png";
        await File.WriteAllBytesAsync(Path.Combine(dfoOutDir, fileName), png, CancellationToken.None);
        Console.WriteLine($"  bt={bt,3} ({label,-22}) 0x{partInst:X16}: diffuse {w}x{h}  → {fileName}");
    }

    // Also dump the hardcoded SkinBlender detail neutral and overlay textures for this age/gender.
    // These are the layers blended into the skin atlas BEFORE any skintone face overlay.
    // If the dark cheek/nose patches come from these layers, we'll see it here.
    var dfoDetailInstances = new ulong[]
    {
        0x0A11C0657FBDB54FUL, // 0  baby
        0xD19E353A4001EC4DUL, // 1  toddler
        0x9CB2C5C93E357C62UL, // 2  child
        0x48F11375333EDB51UL, // 3  teen male
        0x58F8275474E1AE00UL, // 4  YA male
        0x308855B3BFF0E848UL, // 5  adult male
        0x24DFF8E30DC7E5DCUL, // 6  elder male
        0xA062AF087257C3AAUL, // 7  teen male overlay
        0xA3EC609A2DAB31D3UL, // 8  YA male overlay
        0x265B16FA4E7DA19BUL, // 9  adult male overlay
        0x25EBBD9BED791D4FUL, // 10 elder male overlay
        0x737A5FF0EB729888UL, // 11 teen female
        0x36C865290B1F4E79UL, // 12 YA female
        0x59093C1074E2C911UL, // 13 adult female
        0x2356ABE32AC4C255UL, // 14 elder female
        0xF85FB112905485DBUL, // 15 teen female overlay
        0x0A136CA1147B1772UL, // 16 YA female overlay
        0x53F13B3669333A6AUL, // 17 adult female overlay
        0x1E1930AE6138725EUL, // 18 elder female overlay
    };
    var dfoAge    = dfoAgeLabel.ToLowerInvariant();
    var dfoGenderL = dfoGender.ToLowerInvariant();
    int dfoNeutralIdx = (dfoAge, dfoGenderL) switch
    {
        var (a, g) when a.Contains("baby")  => 0,
        var (a, g) when a.Contains("toddler") => 1,
        var (a, g) when a.Contains("child") => 2,
        var (a, g) when a.Contains("teen")  && g == "male"   => 3,
        var (a, g) when (a.Contains("young") || a.Contains("ya")) && g == "male" => 4,
        var (a, g) when a.Contains("adult") && g == "male"   => 5,
        var (a, g) when a.Contains("elder") && g == "male"   => 6,
        var (a, g) when a.Contains("teen")  && g == "female" => 11,
        var (a, g) when (a.Contains("young") || a.Contains("ya")) && g == "female" => 12,
        var (a, g) when a.Contains("adult") && g == "female" => 13,
        var (a, g) when a.Contains("elder") && g == "female" => 14,
        _ => 12  // default YA female
    };
    int dfoOverlayIdx = dfoNeutralIdx + 4;
    Console.WriteLine($"\n  Skin detail layers: neutral-idx={dfoNeutralIdx}  overlay-idx={dfoOverlayIdx}");
    var dfoNeutralInst = dfoDetailInstances[dfoNeutralIdx];
    var dfoNeutralPng  = await FetchTexturePng(dfoNeutralInst, dfoTextureByInst, dfoCatalog);
    if (dfoNeutralPng is { Length: > 0 })
    {
        var fn = $"skin_detail_neutral_0x{dfoNeutralInst:X16}.png";
        await File.WriteAllBytesAsync(Path.Combine(dfoOutDir, fn), dfoNeutralPng, CancellationToken.None);
        Console.WriteLine($"  detail_neutral → {fn}");
    }
    else Console.WriteLine($"  detail_neutral 0x{dfoNeutralInst:X16}: not found in packages");

    if (dfoOverlayIdx < dfoDetailInstances.Length)
    {
        var dfoOverlayInst = dfoDetailInstances[dfoOverlayIdx];
        var dfoOverlayPng  = await FetchTexturePng(dfoOverlayInst, dfoTextureByInst, dfoCatalog);
        if (dfoOverlayPng is { Length: > 0 })
        {
            var fn = $"skin_detail_overlay_0x{dfoOverlayInst:X16}.png";
            await File.WriteAllBytesAsync(Path.Combine(dfoOutDir, fn), dfoOverlayPng, CancellationToken.None);
            Console.WriteLine($"  detail_overlay → {fn}");
        }
        else Console.WriteLine($"  detail_overlay 0x{dfoDetailInstances[dfoOverlayIdx]:X16}: not found in packages");
    }

    Console.WriteLine($"\n  Done. Open {dfoOutDir} to inspect each layer.");
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "--survey-head-shells", StringComparison.OrdinalIgnoreCase))
{
    // Queries the index for N Sims and reports, for each, whether their body-driving outfit
    // contains both a base-body part (body type 6) AND a head part (body types 2, 3, 7, or 8).
    // Having BOTH is the prerequisite for head shell assembly; this survey tells us whether
    // "headless Sim" failures are due to missing head parts in the outfit OR due to rig
    // incompatibility (which requires the full build pipeline to diagnose).
    //
    // Usage: --survey-head-shells [dbPath] [maxSims]
    var shsDb   = args.Length > 1 ? args[1] : @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";
    var shsMax  = args.Length > 2 && int.TryParse(args[2], out var mx) ? mx : 200;

    if (!File.Exists(shsDb)) { Console.Error.WriteLine($"DB not found: {shsDb}"); return 1; }
    var shsConn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={shsDb};Mode=ReadOnly");
    shsConn.Open();

    // Print sim_template_facts schema so we know available columns.
    Console.WriteLine("sim_template_facts columns:");
    using (var c = shsConn.CreateCommand())
    {
        c.CommandText = "PRAGMA table_info(sim_template_facts)";
        using var r = c.ExecuteReader();
        while (r.Read()) Console.Write($"  {r.GetString(1)}");
        Console.WriteLine();
    }

    // Aggregate: for each Sim, which body types appear in the body-driving outfit?
    // body_type 6 = base body shell; 2/3/7/8 = head-related (hair/face/hat-slot/head-LOD).
    // We treat presence of bt=6 as "has body" and any of {2,3,7,8} as "has head part".
    Console.WriteLine($"\nSurveying up to {shsMax:N0} Sims from the index ...");

    var shsResults = new List<(string Tgi, string Species, string Age, string Gender, bool HasBody, bool HasHead, string HeadTypes)>();
    using (var c = shsConn.CreateCommand())
    {
        c.CommandText = $"""
            SELECT
                f.root_tgi,
                coalesce(f.species_label,'?') AS species,
                coalesce(f.age_label,'?')     AS age,
                coalesce(f.gender_label,'?')  AS gender,
                MAX(CASE WHEN b.body_type = 6 THEN 1 ELSE 0 END)              AS has_body,
                MAX(CASE WHEN b.body_type IN (2,3,7,8) THEN 1 ELSE 0 END)     AS has_head,
                group_concat(DISTINCT CASE WHEN b.body_type IN (2,3,7,8)
                             THEN cast(b.body_type AS TEXT) ELSE NULL END)     AS head_body_types
            FROM sim_template_facts f
            JOIN sim_template_body_parts b ON b.root_tgi = f.root_tgi
            WHERE b.is_body_driving = 1
            GROUP BY f.root_tgi
            LIMIT {shsMax}
            """;
        using var r = c.ExecuteReader();
        while (r.Read())
        {
            shsResults.Add((
                r.GetString(0),
                r.GetString(1),
                r.GetString(2),
                r.GetString(3),
                r.GetInt32(4) == 1,
                r.GetInt32(5) == 1,
                r.IsDBNull(6) ? "" : r.GetString(6)));
        }
    }

    var total    = shsResults.Count;
    var hasBody  = shsResults.Count(static s => s.HasBody);
    var hasHead  = shsResults.Count(static s => s.HasHead);
    var hasBoth  = shsResults.Count(static s => s.HasBody && s.HasHead);
    var bodyOnly = shsResults.Count(static s => s.HasBody && !s.HasHead);

    Console.WriteLine($"\nSummary ({total} Sims surveyed):");
    Console.WriteLine($"  Has body shell (bt=6):          {hasBody,5}  ({hasBody * 100.0 / total:F1}%)");
    Console.WriteLine($"  Has head part (bt∈{{2,3,7,8}}):  {hasHead,5}  ({hasHead * 100.0 / total:F1}%)");
    Console.WriteLine($"  Has BOTH body + head:            {hasBoth,5}  ({hasBoth * 100.0 / total:F1}%)");
    Console.WriteLine($"  Body-only (head part missing):   {bodyOnly,5}  ({bodyOnly * 100.0 / total:F1}%)");

    // Breakdown by species × age × gender for Sims that have BOTH.
    Console.WriteLine($"\nBreakdown of Sims with BOTH body + head:");
    var groups = shsResults
        .Where(static s => s.HasBody && s.HasHead)
        .GroupBy(static s => (s.Species, s.Age, s.Gender))
        .OrderByDescending(static g => g.Count())
        .Take(20);
    foreach (var g in groups)
        Console.WriteLine($"  {g.Key.Species,-10} {g.Key.Age,-25} {g.Key.Gender,-8}  count={g.Count(),4}  head_types=[{string.Join(",", g.Select(static s => s.HeadTypes).Distinct().Take(4))}]");

    // Show a few Sims that are body-only so we know which archetype is most affected.
    Console.WriteLine($"\nSample body-only Sims (head part missing — rig check not reached):");
    foreach (var s in shsResults.Where(static s => s.HasBody && !s.HasHead).Take(10))
        Console.WriteLine($"  {s.Species,-10} {s.Age,-25} {s.Gender,-8}  {s.Tgi}");

    // Head body type distribution across all Sims that have head parts.
    Console.WriteLine($"\nHead body-type distribution (across Sims with head parts):");
    using (var c = shsConn.CreateCommand())
    {
        c.CommandText = """
            SELECT b.body_type, coalesce(b.body_type_label,'?'), COUNT(DISTINCT b.root_tgi)
            FROM sim_template_body_parts b
            JOIN sim_template_facts f ON f.root_tgi = b.root_tgi
            WHERE b.is_body_driving = 1
              AND b.body_type IN (2,3,4,7,8,13,14,15,16,17,18,19,20)
            GROUP BY b.body_type
            ORDER BY b.body_type
            """;
        using var r = c.ExecuteReader();
        while (r.Read())
            Console.WriteLine($"  bt={r.GetInt32(0),3} ({r.GetString(1),-22})  sim_count={r.GetInt32(2):N0}");
    }

    return 0;
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
var modelResult = await sceneBuilder.BuildSceneAsync(graph.BuildBuyGraph, CancellationToken.None);
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

static float HalfFloatToSingle(ushort half)
{
    var sign = (half >> 15) & 0x1;
    var exponent = (half >> 10) & 0x1F;
    var mantissa = half & 0x3FF;
    if (exponent == 0)
    {
        if (mantissa == 0) return sign == 0 ? 0f : -0f;
        var subnormal = mantissa / 1024f * MathF.Pow(2f, -14f);
        return sign == 0 ? subnormal : -subnormal;
    }
    if (exponent == 0x1F)
    {
        if (mantissa == 0) return sign == 0 ? float.PositiveInfinity : float.NegativeInfinity;
        return float.NaN;
    }
    var normalized = (1f + mantissa / 1024f) * MathF.Pow(2f, exponent - 15);
    return sign == 0 ? normalized : -normalized;
}

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

static async Task<int> RunSceneResourceAsync(string packagePath, string fullTgi, bool useLiveIndex)
{
    if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
    {
        Console.Error.WriteLine($"Package not found: {packagePath}");
        return 1;
    }

    if (!TryParseTgi(fullTgi, out var key))
    {
        Console.Error.WriteLine("Usage: --scene-resource <packagePath> <resourceTgi> [--live-index]");
        return 2;
    }

    key = key with { TypeName = GuessSceneResourceTypeName(key.Type) };
    if (key.TypeName is not ("Model" or "ModelLOD" or "Geometry"))
    {
        Console.Error.WriteLine($"Unsupported scene resource type 0x{key.Type:X8}. Expected Model, ModelLOD, or Geometry.");
        return 3;
    }

    ICacheService cache = useLiveIndex
        ? new FileSystemCacheService()
        : new ProbeCacheService(Path.Combine(AppContext.BaseDirectory, "probe-cache-scene-resource"));
    cache.EnsureCreated();

    var store = new SqliteIndexStore(cache);
    await store.InitializeAsync(CancellationToken.None);

    var catalog = new LlamaResourceCatalogService();
    ResourceMetadata? resource = null;

    if (useLiveIndex)
    {
        resource = await store.GetResourceByTgiAsync(packagePath, key.FullTgi, CancellationToken.None);
        resource ??= (await store.GetResourcesByTgiAsync(key.FullTgi, CancellationToken.None))
            .FirstOrDefault();
    }
    else
    {
        var source = new DataSourceDefinition(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            "ProbeSceneResource",
            Path.GetDirectoryName(packagePath) ?? packagePath,
            SourceKind.Game);
        var graphBuilder = new ExplicitAssetGraphBuilder(catalog);
        var scan = await catalog.ScanPackageAsync(source, packagePath, progress: null, CancellationToken.None);
        var assets = graphBuilder.BuildAssetSummaries(scan);
        await store.ReplacePackageAsync(scan, assets, CancellationToken.None);
        resource = scan.Resources.FirstOrDefault(candidate => candidate.Key.FullTgi.Equals(key.FullTgi, StringComparison.OrdinalIgnoreCase));
    }

    resource ??= new ResourceMetadata(
        Guid.NewGuid(),
        Guid.Empty,
        SourceKind.Game,
        packagePath,
        key,
        Path.GetFileNameWithoutExtension(packagePath),
        CompressedSize: null,
        UncompressedSize: null,
        IsCompressed: null,
        PreviewKind.Scene,
        IsPreviewable: true,
        IsExportCapable: true,
        AssetLinkageSummary: string.Empty,
        Diagnostics: "Synthetic resource metadata created by ProbeAsset --scene-resource.");

    Console.WriteLine("== Direct Scene Resource ==");
    Console.WriteLine($"Package: {packagePath}");
    Console.WriteLine($"Resource: {resource.Key.FullTgi} ({resource.Key.TypeName})");
    Console.WriteLine($"Index: {(useLiveIndex ? "live read-only" : "probe cache")}");
    Console.WriteLine($"Metadata source: {(resource.Diagnostics.Contains("Synthetic resource metadata", StringComparison.OrdinalIgnoreCase) ? "synthetic" : "indexed/scan")}");

    Console.WriteLine();
    Console.WriteLine("== Resource Summary ==");
    ResourceMetadata resolvedResource;
    byte[] rawBytes;
    try
    {
        await DumpRcolChunkSummary(catalog, resource);
        resolvedResource = await ResolveCompanionResourceAsync(catalog, resource);
        rawBytes = await catalog.GetResourceBytesAsync(resolvedResource.PackagePath, resolvedResource.Key, raw: false, CancellationToken.None);
    }
    catch (Exception ex) when (ex is KeyNotFoundException or InvalidDataException or NotSupportedException)
    {
        Console.Error.WriteLine($"Resource bytes could not be resolved for {resource.Key.FullTgi}: {ex.Message}");
        return 4;
    }

    Console.WriteLine(PreviewDebugProbe.InspectModelLod(rawBytes));
    Console.WriteLine("Material summary:");
    Console.WriteLine(PreviewDebugProbe.InspectMaterialChunks(rawBytes));

    Console.WriteLine();
    Console.WriteLine("== Scene Result ==");
    var sceneBuilder = new BuildBuySceneBuildService(catalog, store);
    var sceneResult = await sceneBuilder.BuildSceneAsync(resource, CancellationToken.None);
    WriteSceneResult(sceneResult);

    return sceneResult.Success ? 0 : 4;
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
        : await sceneBuilder.BuildSceneAsync(graph.BuildBuyGraph, CancellationToken.None);

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

static string GuessSceneResourceTypeName(uint type) => type switch
{
    var value when value == (uint)ResourceType.Model => "Model",
    var value when value == (uint)ResourceType.ModelLOD => "ModelLOD",
    var value when value == (uint)ResourceType.Geometry => "Geometry",
    _ => type.ToString("X8")
};

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

static async Task<int> RunSimArchetypeBodyShellSurveyAsync(string outputPath, int maxEntries)
{
    var cache = new FileSystemCacheService();
    Directory.CreateDirectory(cache.CacheRoot);

    var store = new SqliteIndexStore(cache);
    await store.InitializeAsync(CancellationToken.None);

    var catalog = new LlamaResourceCatalogService();
    var graphBuilder = new ExplicitAssetGraphBuilder(catalog, store);
    var assets = await LoadAllSimArchetypeAssetsAsync(store, maxEntries, CancellationToken.None);
    var rows = new List<SimArchetypeBodyShellSurveyRow>(assets.Count);
    var stopwatch = Stopwatch.StartNew();

    Console.WriteLine($"Surveying {assets.Count:N0} Sim Archetype asset(s) from live index...");

    foreach (var (asset, index) in assets.Select(static (asset, index) => (asset, index)))
    {
        Console.WriteLine($"[{index + 1:N0}/{assets.Count:N0}] {asset.DisplayName}");

        try
        {
            var packageResources = await store.GetPackageResourcesAsync(asset.PackagePath, CancellationToken.None);
            var graph = await graphBuilder.BuildPreviewGraphAsync(asset, packageResources, CancellationToken.None);
            rows.Add(BuildSimArchetypeBodyShellSurveyRow(asset, graph));
        }
        catch (Exception ex)
        {
            rows.Add(new SimArchetypeBodyShellSurveyRow(
                asset.DisplayName,
                asset.RootKey.FullTgi,
                asset.PackagePath,
                string.Empty,
                string.Empty,
                "Error",
                SimBodyAssemblyMode.None.ToString(),
                [],
                [],
                0,
                false,
                string.Empty,
                [$"Exception: {ex.GetType().Name}: {ex.Message}"]));
        }
    }

    stopwatch.Stop();

    var report = new SimArchetypeBodyShellSurveyReport(
        outputPath,
        assets.Count,
        stopwatch.Elapsed,
        rows.GroupBy(static row => row.ContractStatus, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.OrdinalIgnoreCase),
        rows.GroupBy(static row => row.AssemblyMode, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.OrdinalIgnoreCase),
        rows);

    var outputDirectory = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrWhiteSpace(outputDirectory))
    {
        Directory.CreateDirectory(outputDirectory);
    }

    await File.WriteAllTextAsync(
        outputPath,
        JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }),
        CancellationToken.None);

    Console.WriteLine();
    Console.WriteLine($"Wrote Sim Archetype body-shell survey to {outputPath}");
    foreach (var pair in report.StatusCounts)
    {
        Console.WriteLine($"  {pair.Key}: {pair.Value:N0}");
    }

    return 0;
}

static async Task<int> RebuildLiveCacheAndRunSimArchetypeSurveyAsync(int? requestedWorkerCount)
{
    var cache = new FileSystemCacheService();
    cache.EnsureCreated();

    var store = new SqliteIndexStore(cache);
    await store.InitializeAsync(CancellationToken.None);

    var configuredSources = await store.GetDataSourcesAsync(CancellationToken.None);
    if (configuredSources.Count == 0)
    {
        Console.Error.WriteLine($"No configured data sources were found in the live index at {cache.DatabasePath}.");
        return 2;
    }

    var activeSources = configuredSources.Where(static source => source.IsEnabled).ToArray();
    if (activeSources.Length == 0)
    {
        Console.Error.WriteLine("Configured data sources exist, but all of them are disabled.");
        return 3;
    }

    Console.WriteLine($"Live cache root: {cache.CacheRoot}");
    Console.WriteLine($"Live index database: {cache.DatabasePath}");
    Console.WriteLine($"Configured data sources: {configuredSources.Count} total, {activeSources.Length} enabled");
    foreach (var source in configuredSources)
    {
        Console.WriteLine($"  - {source.DisplayName} | {source.Kind} | enabled={source.IsEnabled} | root={source.RootPath}");
    }

    var catalog = new LlamaResourceCatalogService();
    var graphBuilder = new ExplicitAssetGraphBuilder(catalog, store);
    var scanner = new FileSystemPackageScanner();
    var coordinator = new PackageIndexCoordinator(scanner, catalog, graphBuilder, store, IndexingRunOptions.CreateDefault());

    var progress = new Progress<IndexingProgress>(snapshot =>
    {
        Console.WriteLine(
            $"[{snapshot.Stage}] {snapshot.Message} | processed={snapshot.PackagesProcessed:N0}/{snapshot.PackagesTotal:N0} " +
            $"completed={snapshot.PackagesCompleted:N0} skipped={snapshot.PackagesSkipped:N0} failed={snapshot.PackagesFailed:N0} " +
            $"resources={snapshot.ResourcesProcessed:N0} workers={snapshot.ActiveWorkerCount}/{snapshot.ConfiguredWorkerCount} " +
            $"pending={snapshot.PendingPackageCount:N0} persist={snapshot.PendingPersistCount:N0} writerBusy={snapshot.WriterBusy}");
        if (snapshot.Summary is null)
        {
            return;
        }

        Console.WriteLine(
            $"  summary elapsed={snapshot.Summary.TotalElapsed:hh\\:mm\\:ss} packages={snapshot.Summary.PackagesProcessed:N0} " +
            $"failed={snapshot.Summary.PackagesFailed:N0} throughput={snapshot.Summary.AverageThroughput:N0} res/s");
    });

    Console.WriteLine();
    Console.WriteLine("== Live Cache Rebuild ==");
    var rebuildStopwatch = Stopwatch.StartNew();
    await coordinator.RunAsync(configuredSources, progress, CancellationToken.None, requestedWorkerCount);
    rebuildStopwatch.Stop();
    Console.WriteLine($"Live cache rebuild complete in {rebuildStopwatch.Elapsed:hh\\:mm\\:ss}.");

    Console.WriteLine();
    Console.WriteLine("== Live Sim Archetype Survey ==");
    var outputPath = Path.Combine(Environment.CurrentDirectory, "tmp", "sim_archetype_body_shell_audit.json");
    return await RunSimArchetypeBodyShellSurveyAsync(outputPath, 0);
}

static async Task<IReadOnlyList<AssetSummary>> LoadAllSimArchetypeAssetsAsync(
    IIndexStore store,
    int maxEntries,
    CancellationToken cancellationToken)
{
    const int pageSize = 128;
    var results = new List<AssetSummary>();

    for (var offset = 0; ; offset += pageSize)
    {
        var query = new AssetBrowserQuery(
            new SourceScope(),
            string.Empty,
            AssetBrowserDomain.Sim,
            string.Empty,
            string.Empty,
            string.Empty,
            false,
            false,
            AssetBrowserSort.Name,
            offset,
            pageSize);
        var page = await store.QueryAssetsAsync(query, cancellationToken);
        if (page.Items.Count == 0)
        {
            break;
        }

        results.AddRange(page.Items);
        if (maxEntries > 0 && results.Count >= maxEntries)
        {
            return results.Take(maxEntries).ToArray();
        }

        if (!page.HasMore)
        {
            break;
        }
    }

    return results;
}

static SimArchetypeBodyShellSurveyRow BuildSimArchetypeBodyShellSurveyRow(AssetSummary asset, AssetGraph graph)
{
    var simGraph = graph.SimGraph;
    if (simGraph is null)
    {
        return new SimArchetypeBodyShellSurveyRow(
            asset.DisplayName,
            asset.RootKey.FullTgi,
            asset.PackagePath,
            string.Empty,
            string.Empty,
            "GraphMissing",
            SimBodyAssemblyMode.None.ToString(),
            [],
            [],
            0,
            false,
            string.Empty,
            ["Sim graph was not constructed."]);
    }

    var bodyDrivingSource = simGraph.BodySources.FirstOrDefault(static source => source.Label == "Body-driving outfit records");
    var bodyDrivingCount = bodyDrivingSource?.Count ?? 0;
    var activeLayers = simGraph.BodyAssembly.Layers
        .Where(static layer => layer.State == SimBodyAssemblyLayerState.Active)
        .Select(static layer => layer.Label)
        .ToArray();
    var candidateSources = simGraph.BodyCandidates
        .Select(candidate => $"{candidate.Label}:{candidate.SourceKind}")
        .ToArray();
    var usesIndexedDefaultRecipe = simGraph.BodyCandidates.Any(static candidate => candidate.SourceKind == SimBodyCandidateSourceKind.IndexedDefaultBodyRecipe);
    var hasResolvedBodyShell = simGraph.BodyAssembly.Mode != SimBodyAssemblyMode.None;
    var contractStatus =
        usesIndexedDefaultRecipe
            ? "IndexedDefaultBodyRecipe"
            : bodyDrivingCount > 0 && hasResolvedBodyShell
                ? "ExplicitBodyDriving"
                : hasResolvedBodyShell
                    ? "OtherBodyRecipe"
                    : "Unresolved";
    var issues = new List<string>();
    if (candidateSources.Any(static source =>
            source.Contains(nameof(SimBodyCandidateSourceKind.ArchetypeCompatibilityFallback), StringComparison.Ordinal) ||
            source.Contains(nameof(SimBodyCandidateSourceKind.BodyTypeFallback), StringComparison.Ordinal) ||
            source.Contains(nameof(SimBodyCandidateSourceKind.CanonicalFoundation), StringComparison.Ordinal)))
    {
        issues.Add("Broad fallback candidate source surfaced in preview graph.");
    }

    if (bodyDrivingCount > 0 && !usesIndexedDefaultRecipe && !hasResolvedBodyShell)
    {
        issues.Add("Explicit body-driving outfit records existed, but no renderable body-shell layer was resolved.");
    }

    if (bodyDrivingCount == 0 && !usesIndexedDefaultRecipe && !hasResolvedBodyShell)
    {
        issues.Add("No explicit body-driving recipe and no indexed default/naked body recipe were resolved.");
    }

    if (activeLayers.Length == 0 && hasResolvedBodyShell)
    {
        issues.Add("Assembly mode is non-empty but no active body layers were resolved.");
    }

    var timingLine = graph.Diagnostics.FirstOrDefault(static line => line.StartsWith("Sim graph timings:", StringComparison.Ordinal));
    return new SimArchetypeBodyShellSurveyRow(
        asset.DisplayName,
        asset.RootKey.FullTgi,
        asset.PackagePath,
        simGraph.SimInfoResource.Name ?? string.Empty,
        simGraph.SimInfoResource.PackagePath,
        contractStatus,
        simGraph.BodyAssembly.Mode.ToString(),
        activeLayers,
        candidateSources,
        bodyDrivingCount,
        usesIndexedDefaultRecipe,
        timingLine ?? string.Empty,
        issues);
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

file sealed record SimArchetypeBodyShellSurveyRow(
    string AssetDisplayName,
    string RootTgi,
    string AssetPackagePath,
    string SelectedTemplateDisplayName,
    string SelectedTemplatePackagePath,
    string ContractStatus,
    string AssemblyMode,
    IReadOnlyList<string> ActiveLayers,
    IReadOnlyList<string> CandidateSources,
    int BodyDrivingOutfitCount,
    bool UsesIndexedDefaultBodyRecipe,
    string TimingLine,
    IReadOnlyList<string> Issues);

file sealed record SimArchetypeBodyShellSurveyReport(
    string OutputPath,
    int TotalAssets,
    TimeSpan Elapsed,
    IReadOnlyDictionary<string, int> StatusCounts,
    IReadOnlyDictionary<string, int> AssemblyModeCounts,
    IReadOnlyList<SimArchetypeBodyShellSurveyRow> Rows);

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
