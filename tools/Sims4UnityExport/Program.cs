// Sims4UnityExport — headless console tool that reuses the existing Sims4ResourceExplorer
// libraries to (a) SEARCH the prebuilt resource index for Build/Buy objects by name, and
// (b) EXPORT one Build/Buy object to FBX + PNG textures into the Unity project.
//
// Subcommands (args[0]):
//   search <term> [buildbuy|cas|sim]            domain defaults to buildbuy (back-compat)
//   export <packagePath> <fullTgi> <slug>       Build/Buy; <fullTgi> = Type:Group:Instance (hex)
//   exportcas <packagePath> <fullTgi> <slug>    CAS (Create-a-Sim) part, static / bind-pose
//   exportsim <age> <gender> [<skintone>] <slug>  Full default human Sim → character.json
//
// Errors are reported via printed diagnostics; we avoid throwing past the top level.

using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sims4ResourceExplorer.Assets;
using Sims4ResourceExplorer.Audio;
using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Export;
using Sims4ResourceExplorer.Indexing;
using Sims4ResourceExplorer.Packages;
using Sims4ResourceExplorer.Preview;
using Sims4UnityExport;

// Output directory inside the Unity project (per the brief).
const string UnityAssetsDir =
    @"c:\Users\stani\PROJECTS\#GAMES\Sims4Browser\unity\Sims4Creator\Assets\Sims4";

// Stable id for the ModsFromDev data source (probemod / reindex).
Guid ModsSourceId = Guid.Parse("5a1e0d0c-6b2f-4b1e-9b7e-000000000001");

// Page size for searches.
const int PageSize = 50;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

// Build the DI host. This mirrors tmp/uvinspect/Program.cs (the proven registration set)
// and ADDS IFbxExportService = AssimpFbxExportService for the export path.
//
// MOD-OVERRIDE wiring: the real LlamaResourceCatalogService is registered as a concrete singleton,
// and IResourceCatalogService is resolved through a factory that consults ModOverrideHolder. When
// the holder has been armed with a pre-built ModOverrideResourceCatalogService (the HD export path),
// EVERY consumer (SyntheticSimService, the Sim renderer, the scene builder, …) transparently gets
// the decorator so the mod packages win by TGI. When the holder is empty, the real catalog is used
// verbatim — all other subcommands behave exactly as before.
// Built via a factory so the character-customizer export can spin a FRESH host per body-mesh
// variant (the IResourceCatalogService singleton binds the ModOverride decorator on first
// resolution, so swapping body meshes in one process needs a new host each time).
var host = BuildExportHost();

var indexStore = host.Services.GetRequiredService<IIndexStore>();
// IAssetGraphBuilder + ISceneBuildService take IResourceCatalogService in their constructors, so
// they MUST be resolved lazily — eager resolution here would materialise the IResourceCatalogService
// singleton with an EMPTY ModOverrideHolder, permanently caching the real catalog and defeating the
// HD override path. Each subcommand resolves them only when needed (after the holder is armed/left
// empty as appropriate).
IAssetGraphBuilder GraphBuilder() => host.Services.GetRequiredService<IAssetGraphBuilder>();
ISceneBuildService SceneBuilder() => host.Services.GetRequiredService<ISceneBuildService>();
IFbxExportService FbxExporter() => host.Services.GetRequiredService<IFbxExportService>();
// Resolved lazily: the IResourceCatalogService factory consults ModOverrideHolder, so we must NOT
// force the singleton to materialise before the HD export path has had a chance to arm the holder.
// All non-HD subcommands call ResourceCatalog() AFTER the holder is (left) empty, so they get the
// real catalog; the HD path arms the holder first and then everything sees the decorator.
IResourceCatalogService ResourceCatalog() => host.Services.GetRequiredService<IResourceCatalogService>();

var ct = CancellationToken.None;

// exportsim helpers (resolved lazily inside the case so the other subcommands don't pay for
// constructing the Sim renderer graph).
ISyntheticSimService SyntheticSimService() => host.Services.GetRequiredService<ISyntheticSimService>();
Sims4ResourceExplorer.Preview.SimRender.ISimAssetGraphRenderer SimRenderer() =>
    host.Services.GetRequiredService<Sims4ResourceExplorer.Preview.SimRender.ISimAssetGraphRenderer>();

try
{
    switch (args[0].ToLowerInvariant())
    {
        case "search":
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: search <term> [buildbuy|cas|sim]");
                return 1;
            }
            // Optional 3rd arg picks the domain; default buildbuy for back-compat.
            var searchDomain = AssetBrowserDomain.BuildBuy;
            if (args.Length >= 3 && !TryParseDomain(args[2], out searchDomain))
            {
                Console.Error.WriteLine($"Unknown domain '{args[2]}'. Expected one of: buildbuy, cas, sim.");
                return 1;
            }
            await RunSearchAsync(args[1], searchDomain);
            return 0;

        case "export":
            if (args.Length < 4)
            {
                Console.Error.WriteLine("Usage: export <packagePath> <fullTgi> <slug>");
                return 1;
            }
            await RunExportAsync(args[1], args[2], args[3]);
            return 0;

        case "exportcas":
            if (args.Length < 4)
            {
                Console.Error.WriteLine("Usage: exportcas <packagePath> <fullTgi> <slug>");
                return 1;
            }
            await RunExportCasAsync(args[1], args[2], args[3]);
            return 0;

        case "exporthair":
            // exporthair <charSlug> <styleInternalName> [maxColors]
            // Resolves a hairstyle's colour-variant CASPs, builds its hair mesh (skinned to the target
            // character's shared skeleton) + a baked diffuse per colour, and APPENDS a hairCatalog to
            // <charSlug>/character.json (leaves everything else untouched).
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Usage: exporthair <charSlug> <styleInternalName> [maxColors]");
                return 1;
            }
            {
                var maxColors = args.Length > 3 && int.TryParse(args[3], out var mc) ? mc : int.MaxValue;
                await HairExporter.RunAsync(
                    UnityAssetsDir, args[1], args[2], maxColors,
                    indexStore, GraphBuilder(), SceneBuilder(), ct);
            }
            return 0;

        case "exporthairmany":
            // exporthairmany <charSlug> <maxColors> <style1> [style2] ...  — one index init for the batch.
            if (args.Length < 4)
            {
                Console.Error.WriteLine("Usage: exporthairmany <charSlug> <maxColors> <style1> [style2] ...");
                return 1;
            }
            {
                var maxColors = int.TryParse(args[2], out var mc) ? mc : int.MaxValue;
                var gb = GraphBuilder();
                var sb = SceneBuilder();
                for (var i = 3; i < args.Length; i++)
                {
                    Console.WriteLine($"\n===== [{i - 2}/{args.Length - 3}] {args[i]} =====");
                    try { await HairExporter.RunAsync(UnityAssetsDir, args[1], args[i], maxColors, indexStore, gb, sb, ct); }
                    catch (Exception ex) { Console.Error.WriteLine($"  '{args[i]}' FAILED ({ex.GetType().Name}: {ex.Message}); continuing."); }
                }
            }
            return 0;

        case "exportcloth":
            // exportcloth <charSlug> <yfInternalName> [maxColors]
            // Resolves a garment's colour variants → its mesh(es) skinned to the character's shared
            // skeleton + a diffuse per colour, and APPENDS it to the right clothingCatalog slot in
            // <charSlug>/character.json (top/bottom/full/shoes inferred from the yf* prefix).
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Usage: exportcloth <charSlug> <yfInternalName> [maxColors]");
                return 1;
            }
            {
                var maxColors = args.Length > 3 && int.TryParse(args[3], out var mc) ? mc : int.MaxValue;
                await ClothingExporter.RunAsync(
                    UnityAssetsDir, args[1], args[2], maxColors,
                    indexStore, GraphBuilder(), SceneBuilder(), ResourceCatalog(), ct);
            }
            return 0;

        case "exportclothmany":
            // exportclothmany <charSlug> <maxColors> <name1> [name2] ...  — one index init for the whole batch.
            if (args.Length < 4)
            {
                Console.Error.WriteLine("Usage: exportclothmany <charSlug> <maxColors> <name1> [name2] ...");
                return 1;
            }
            {
                var maxColors = int.TryParse(args[2], out var mc) ? mc : int.MaxValue;
                var gb = GraphBuilder();
                var sb = SceneBuilder();
                for (var i = 3; i < args.Length; i++)
                {
                    Console.WriteLine($"\n===== [{i - 2}/{args.Length - 3}] {args[i]} =====");
                    try { await ClothingExporter.RunAsync(UnityAssetsDir, args[1], args[i], maxColors, indexStore, gb, sb, ResourceCatalog(), ct); }
                    catch (Exception ex) { Console.Error.WriteLine($"  '{args[i]}' FAILED ({ex.GetType().Name}: {ex.Message}); continuing."); }
                }
            }
            return 0;

        case "exportsim":
            // exportsim <age> <gender> [<skintoneInstanceHexOrDefault>] <slug>
            if (args.Length < 4)
            {
                Console.Error.WriteLine("Usage: exportsim <age> <gender> [<skintoneInstanceHexOrDefault>] <slug>");
                return 1;
            }
            await RunExportSimAsync(args);
            return 0;

        case "exportsimhd":
            // exportsimhd <age> <gender> [<skintoneInstanceHexOrDefault>] <slug>
            // Same as exportsim but ARMS the ModsFromDev mod-override layer first so the assembled
            // Sim uses the Better Body [Female] body GEOMs + the wild_guy HD base skin (mod wins by
            // TGI). The override set is currently the hardcoded Adult-Female HD set.
            if (args.Length < 4)
            {
                Console.Error.WriteLine("Usage: exportsimhd <age> <gender> [<skintoneInstanceHexOrDefault>] <slug>");
                return 1;
            }
            await RunExportSimHdAsync(args);
            return 0;

        case "bakeskindebug":
            // Diagnostic: for one Sim's resolved skintone, dump every compositor INPUT and STAGE
            // as its own PNG into <slug>/Textures/_debug/ so the artifact can be localized.
            // Usage: bakeskindebug <age> <gender> [<skintoneInstanceHexOrDefault>] <slug>
            if (args.Length < 4)
            {
                Console.Error.WriteLine("Usage: bakeskindebug <age> <gender> [<skintoneInstanceHexOrDefault>] <slug>");
                return 1;
            }
            await RunBakeSkinDebugAsync(args);
            return 0;

        case "dumpskinlayers":
            // Diagnostic: dump the skin-texture LAYER set (base + detail rows + face overlays +
            // Model-B head-shell full-body diffuse candidate) for one default Sim into
            // <UnityAssetsDir>/_skinlayers/ for visual review. Investigation only — changes no export.
            // Usage: dumpskinlayers <age> <gender> [<skintoneInstanceHexOrDefault>]
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Usage: dumpskinlayers <age> <gender> [<skintoneInstanceHexOrDefault>]");
                return 1;
            }
            await RunDumpSkinLayersAsync(args);
            return 0;

        case "dumpwildguy":
            // Diagnostic: dump + classify every LRLEImage in a package (default = the wild_guy
            // FemaleDefaultNudeSkin) as PNGs into <UnityAssetsDir>/_wildguy/, reporting mean RGB
            // and COLORED-WARM-SKIN vs GRAYSCALE/NEAR-WHITE for each. Used to decide whether the
            // skin-base override should pick a warm wild_guy LRLE or revert to EA's warm base.
            // Usage: dumpwildguy [<packagePath>]   (defaults to the wild_guy nude-skin package)
            {
                var wgPkg = args.Length >= 2
                    ? args[1]
                    : @"c:\Users\stani\PROJECTS\#GAMES\Sims4Browser\ModsFromDev\WIldGuy\FBD\wild_guy FemaleDefaultNudeSkin.package";
                await indexStore.InitializeAsync(ct);
                await WildGuyDumper.RunAsync(ResourceCatalog(), UnityAssetsDir, wgPkg, ct);
            }
            return 0;

        case "exportthumbs":
            // exportthumbs <slug=instanceHex> [...] — dump the game's BuyBuildThumbnail for
            // already-exported items into <UnityAssetsDir>/<slug>/thumb.(png|jpg).
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: exportthumbs <slug=instanceHex> [...]  e.g. exportthumbs home/sofa=100B");
                return 1;
            }
            {
                await indexStore.InitializeAsync(ct);
                for (var i = 1; i < args.Length; i++)
                {
                    var eq = args[i].IndexOf('=');
                    if (eq <= 0) { Console.Error.WriteLine($"Bad spec '{args[i]}'"); continue; }
                    var slug = args[i][..eq];
                    var inst = Convert.ToUInt64(args[i][(eq + 1)..], 16);
                    var folder = Path.Combine(UnityAssetsDir, slug);
                    Directory.CreateDirectory(folder);
                    var file = await CoveringExporter.TryWriteThumbAsync(inst, folder, "thumb", indexStore, ResourceCatalog(), ct);
                    Console.WriteLine($"{slug}: {(file ?? "NO THUMBNAIL")}");
                }
            }
            return 0;

        case "exportcoverings":
            // exportcoverings [countPerKind=12] — export wall + floor covering textures (+ game
            // thumbnails) into <UnityAssetsDir>/home/coverings/ and write coverings.json.
            {
                var count = args.Length >= 2 && int.TryParse(args[1], out var c) ? c : 12;
                await indexStore.InitializeAsync(ct);
                await CoveringExporter.RunAsync(UnityAssetsDir, count, indexStore, ResourceCatalog(), ct);
            }
            return 0;

        case "exportcutouts":
            // exportcutouts — decode each exported home item's ModelCutout (the game's own
            // wall-cut silhouette polygon) into <folder>/cutout.json. No args; walks home/*.
            await indexStore.InitializeAsync(ct);
            await CutoutExporter.RunAsync(UnityAssetsDir, indexStore, ResourceCatalog(), ct);
            return 0;

        case "dumpres":
            // Diagnostic: dump ONE resource's decompressed bytes to a file for offline analysis.
            // Usage: dumpres <packagePath> <fullTgi> <outFile>
            if (args.Length < 4)
            {
                Console.Error.WriteLine("Usage: dumpres <packagePath> <fullTgi> <outFile>");
                return 1;
            }
            {
                var parts = args[2].Split(':');
                var key = new ResourceKeyRecord(
                    Convert.ToUInt32(parts[0], 16), Convert.ToUInt32(parts[1], 16), Convert.ToUInt64(parts[2], 16), "");
                var bytes = await ResourceCatalog().GetResourceBytesAsync(args[1], key, raw: false, ct);
                await File.WriteAllBytesAsync(args[3], bytes, ct);
                Console.WriteLine($"Wrote {bytes.Length} bytes to {args[3]}.");
            }
            return 0;

        case "scancasmask":
            // Diagnostic: scan one package file's CASP resources and report those that carry a
            // color_shift_mask texture slot AND more than one SwatchColors (i.e. a true recolor).
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: scancasmask <packagePath>");
                return 1;
            }
            await RunScanCasMaskAsync(args[1]);
            return 0;

        case "scanmod":
            // Scanner: open ONE mod package, enumerate ALL resources, and report CASP parts,
            // GEOM meshes (vertex/triangle/bone-hash counts, UV0 range, uv1 presence, morph
            // flags, EA-rig subset verdict) and textures (TypeName + decoded resolution) —
            // the evidence for "is there an HD body mesh / HD skin textures?".
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: scanmod <packagePath>");
                return 1;
            }
            await ModScanner.RunAsync(
                ResourceCatalog(),
                indexStore,
                host.Services.GetRequiredService<ITextureDecodeService>(),
                args[1],
                ct);
            return 0;

        case "scangeoms":
            // Content-based hunt for a HEAD mesh hidden under ANY asset type (the EVE lesson): walk
            // every package in a dir, classify EVERY GEOM by geometry (centroid + bbox), flag heads.
            // Usage: scangeoms <dir>
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: scangeoms <dir>");
                return 1;
            }
            await indexStore.InitializeAsync(ct);
            await ModScanner.ScanGeomsInDirAsync(ResourceCatalog(), args[1], ct);
            return 0;

        case "normalfromrelief":
            // Generate a tangent-space NORMAL map from a grayscale RELIEF (height) PNG (R = height).
            // Usage: normalfromrelief <reliefPng> <outNormalPng> [strength]
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Usage: normalfromrelief <reliefPng> <outNormalPng> [strength]");
                return 1;
            }
            {
                var strength = args.Length >= 4 &&
                    float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : 4.0f;
                NormalGen.Generate(args[1], args[2], strength);
                Console.WriteLine($"normalfromrelief: wrote {args[2]} (strength={strength}).");
            }
            return 0;

        case "bakeselftest":
            // Diagnostic: prove the CAS recolor bake produces distinct colored diffuses for
            // distinct swatch tints, using a real exported diffuse + a synthetic white mask.
            // Usage: bakeselftest <baseDiffusePng> <outputDir> <hexAARRGGBB> [<hex> ...]
            if (args.Length < 4)
            {
                Console.Error.WriteLine("Usage: bakeselftest <baseDiffusePng> <outputDir> <hexAARRGGBB> [<hex> ...]");
                return 1;
            }
            var tints = new List<uint>();
            for (var i = 3; i < args.Length; i++)
            {
                var hex = args[i].TrimStart('#');
                if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
                {
                    tints.Add(argb);
                }
            }
            SwatchExporter.ComposeSelfTest(args[1], args[2], tints);
            return 0;

        case "catalogprobe":
            // Discovery: enumerate EA Human skintones + EyeColor CAS parts to populate catalog.json.
            await indexStore.InitializeAsync(ct);
            await CatalogProbe.RunAsync(SyntheticSimService(), ResourceCatalog(), maxTones: 30, maxEyes: 30, ct);
            return 0;

        case "exportchar":
            // exportchar <definitionPath> [<slug>]
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: exportchar <definitionPath> [<slug>]");
                return 1;
            }
            await RunExportCharAsync(args[1], args.Length >= 3 ? args[2] : null);
            return 0;

        case "scanmorphs":
            // scanmorphs <definitionPath> — score+rank ALL adult-female BGEO morphs against the scene
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: scanmorphs <definitionPath>");
                return 1;
            }
            await RunScanMorphsAsync(args[1]);
            return 0;

        case "exportclip":
            // exportclip <slug> [<clipSelector>] — decode game CLIP animation(s) into
            // <slug>/animations/<clipName>.json (the Unity builder offers them as a switchable set).
            // With NO selector: exports a CURATED set of adult standing idles. With a selector
            // (16-hex CLIP instance id OR clip-name substring): ADDS that one clip to the set.
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: exportclip <slug> [<clipInstanceHex|clipNameSubstring>]");
                return 1;
            }
            {
                Console.WriteLine("Initializing index...");
                await indexStore.InitializeAsync(ct);
                if (args.Length >= 3)
                {
                    var okClip = await ClipAnimationExporter.RunAsync(UnityAssetsDir, args[1], indexStore, ResourceCatalog(), args[2], ct);
                    return okClip ? 0 : 1;
                }
                var n = await ClipAnimationExporter.RunCuratedAsync(UnityAssetsDir, args[1], indexStore, ResourceCatalog(), ct);
                return n > 0 ? 0 : 1;
            }

        case "probemod":
            // probemod <packagePath> — scan a loose mod .package with the REAL index scanner and print
            // its resource histogram + CAS part facts (internal names, body types), WITHOUT indexing it.
            if (args.Length < 2 || !File.Exists(args[1]))
            {
                Console.Error.WriteLine("Usage: probemod <packagePath>");
                return 1;
            }
            {
                var full = Path.GetFullPath(args[1]);
                var src = new DataSourceDefinition(ModsSourceId, "ModsFromDev", Path.GetDirectoryName(full)!, SourceKind.Mods, true);
                var scan = await ResourceCatalog().ScanPackageAsync(src, full, null, ct);
                Console.WriteLine($"Package: {full}");
                Console.WriteLine($"Resources: {scan.Resources.Count}");
                foreach (var g in scan.Resources.GroupBy(r => r.Key.TypeName ?? $"0x{r.Key.Type:X8}").OrderByDescending(g => g.Count()))
                    Console.WriteLine($"  {g.Key,-24} x{g.Count()}");
                Console.WriteLine($"CAS part facts: {scan.CasPartFacts.Count}");
                foreach (var f in scan.CasPartFacts)
                    Console.WriteLine($"  body_type={f.BodyType,-3} name='{f.InternalName}' tgi={f.RootTgi} slot={f.SlotCategory}");
                foreach (var d in scan.Diagnostics.Take(10)) Console.WriteLine($"  [diag] {d}");
            }
            return 0;

        case "reindex":
            // reindex [modsRoot] — FULL shadow index rebuild over the Game source + a ModsFromDev source,
            // so vetted mod packages become first-class citizens of the resolution pipeline (enumeration,
            // graph building and byte fetches all flow through the same index rows as EA content).
            {
                var modsRoot = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\ModsFromDev"));
                if (!Directory.Exists(modsRoot))
                {
                    Console.Error.WriteLine($"Mods root not found: {modsRoot}");
                    return 1;
                }
                var sources = new[]
                {
                    new DataSourceDefinition(Guid.Parse("8f65d36b-33ef-4923-8017-463d89fbcc44"), "Game: The Sims 4", @"C:\GAMES\The Sims 4", SourceKind.Game, true),
                    new DataSourceDefinition(ModsSourceId, "ModsFromDev", modsRoot, SourceKind.Mods, true),
                };
                Console.WriteLine($"Reindexing sources:\n  {sources[0].RootPath}\n  {sources[1].RootPath}");
                var coordinator = new PackageIndexCoordinator(
                    host.Services.GetRequiredService<IPackageScanner>(), ResourceCatalog(), GraphBuilder(), indexStore);
                var lastStage = "";
                var progress = new Progress<IndexingProgress>(p =>
                {
                    var line = $"[{p.Stage}] {p.Message}";
                    if (line != lastStage) { Console.WriteLine(line); lastStage = line; }
                });
                await coordinator.RunAsync(sources, progress, ct);
                Console.WriteLine("Reindex complete.");
            }
            return 0;

        default:
            PrintUsage();
            return 1;
    }
}
catch (Exception ex)
{
    // Top-level guard: surface as a diagnostic rather than a stack-trace crash.
    Console.Error.WriteLine($"FATAL: {ex.GetType().Name}: {ex.Message}");
    return 2;
}

// ---------------------------------------------------------------------------
// search <term>
// ---------------------------------------------------------------------------
async Task RunSearchAsync(string term, AssetBrowserDomain domain)
{
    Console.WriteLine($"Initializing index...");
    await indexStore.InitializeAsync(ct);

    var matches = await SearchAssetsAsync(term, domain);

    Console.WriteLine($"Found {matches.TotalCount} {DomainLabel(domain)} match(es) for \"{term}\" (showing up to {PageSize}).");
    Console.WriteLine();
    PrintHeader();

    var rows = matches.Items.Take(PageSize).ToList();
    for (var i = 0; i < rows.Count; i++)
    {
        PrintRow(i, rows[i]);
    }
}

// Builds a "search everything" query in the requested domain: all sources, all the optional
// facet filters left empty, sorted by name, first PageSize rows.
async Task<WindowedQueryResult<AssetSummary>> SearchAssetsAsync(string term, AssetBrowserDomain domain)
{
    var query = new AssetBrowserQuery(
        SourceScope: new SourceScope(IncludeGame: true, IncludeDlc: true, IncludeMods: true),
        SearchText: term.Trim(),
        Domain: domain,
        CategoryText: string.Empty,
        PackageText: string.Empty,
        PackageRelativeText: string.Empty,
        HasThumbnailOnly: false,
        VariantsOnly: false,
        Sort: AssetBrowserSort.Name,
        Offset: 0,
        WindowSize: PageSize);

    return await indexStore.QueryAssetsAsync(query, ct);
}

// Parse the optional search-domain argument. Accepts a few friendly spellings.
static bool TryParseDomain(string text, out AssetBrowserDomain domain)
{
    switch (text.Trim().ToLowerInvariant())
    {
        case "buildbuy":
        case "build/buy":
        case "bb":
            domain = AssetBrowserDomain.BuildBuy;
            return true;
        case "cas":
            domain = AssetBrowserDomain.Cas;
            return true;
        case "sim":
            domain = AssetBrowserDomain.Sim;
            return true;
        default:
            domain = AssetBrowserDomain.BuildBuy;
            return false;
    }
}

static string DomainLabel(AssetBrowserDomain domain) => domain switch
{
    AssetBrowserDomain.BuildBuy => "Build/Buy",
    AssetBrowserDomain.Cas => "CAS",
    AssetBrowserDomain.Sim => "Sim",
    _ => domain.ToString()
};

// ---------------------------------------------------------------------------
// export <packagePath> <fullTgi> <slug>
// ---------------------------------------------------------------------------
async Task RunExportAsync(string packagePath, string fullTgi, string slug)
{
    Console.WriteLine($"Initializing index...");
    await indexStore.InitializeAsync(ct);

    // Parse Type:Group:Instance (hex) back into numeric components.
    if (!TryParseFullTgi(fullTgi, out var type, out var group, out var fullInstance))
    {
        Console.Error.WriteLine($"Could not parse fullTgi '{fullTgi}'. Expected Type:Group:Instance in hex, e.g. 0319E4F1:00000000:0123456789ABCDEF.");
        return;
    }

    Console.WriteLine($"Resolving asset: package='{packagePath}' tgi={type:X8}:{group:X8}:{fullInstance:X16} slug='{slug}'");

    // Preferred path: re-query Build/Buy assets in this package and match by FullTgi to
    // recover the *real* AssetSummary (capabilities, category, linked counts, etc.).
    var summary = await ResolveAssetSummaryAsync(packagePath, fullTgi, AssetBrowserDomain.BuildBuy);
    if (summary is null)
    {
        Console.Error.WriteLine("Could not resolve an AssetSummary matching that package + fullTgi. Aborting.");
        return;
    }
    Console.WriteLine($"Resolved AssetSummary: '{summary.DisplayName}' category='{summary.Category}' rootType={summary.RootKey.TypeName} linked={summary.LinkedResourceCount}");

    // Gather the linked resources for this asset instance, then build the asset graph.
    var graphBuilder = GraphBuilder();
    var sceneBuilder = SceneBuilder();
    var fbxExporter = FbxExporter();
    var packageResources = await indexStore.GetResourcesByInstanceAsync(packagePath, fullInstance, ct);
    Console.WriteLine($"GetResourcesByInstanceAsync returned {packageResources.Count} linked resource(s).");

    var graph = await graphBuilder.BuildAssetGraphAsync(summary, packageResources, ct);
    foreach (var d in graph.Diagnostics)
    {
        Console.WriteLine($"[graph] {d}");
    }

    SceneBuildResult sceneResult;
    var usedFallback = false;

    if (graph.BuildBuyGraph is { } bbGraph)
    {
        Console.WriteLine($"Building scene from BuildBuyAssetGraph (model={bbGraph.ModelResource.Key.FullTgi}, materials={bbGraph.MaterialResources.Count}, textures={bbGraph.TextureResources.Count}, supported={bbGraph.IsSupported})...");
        sceneResult = await sceneBuilder.BuildSceneAsync(bbGraph, ct);
    }
    else
    {
        // Fallback: no BuildBuyGraph available — build directly from the Model resource.
        usedFallback = true;
        Console.WriteLine("No BuildBuyGraph on the asset graph; falling back to BuildSceneAsync(ResourceMetadata) on the Model TGI.");
        var modelResource = FindModelResource(packageResources, summary, type, group, fullInstance);
        if (modelResource is null)
        {
            Console.Error.WriteLine("Fallback failed: could not locate a Model/ModelLOD/Geometry resource for this instance.");
            return;
        }
        Console.WriteLine($"Fallback model resource: {modelResource.Key.FullTgi} ({modelResource.Key.TypeName}).");
        sceneResult = await sceneBuilder.BuildSceneAsync(modelResource, ct);
    }

    Console.WriteLine();
    Console.WriteLine($"Scene build: Success={sceneResult.Success} Status={sceneResult.Status}{(usedFallback ? " (via Model fallback)" : string.Empty)}");
    foreach (var d in sceneResult.Diagnostics)
    {
        Console.WriteLine($"[scene] {d}");
    }

    if (!sceneResult.Success || sceneResult.Scene is null)
    {
        Console.Error.WriteLine("Scene build did not succeed; nothing to export.");
        return;
    }

    var scene = sceneResult.Scene;
    var meshCount = scene.Meshes.Count;
    var totalVertices = scene.Meshes.Sum(m => m.Positions.Count / 3);
    Console.WriteLine($"Scene ready: {meshCount} mesh(es), {totalVertices} total vertices, {scene.Materials.Count} material(s).");

    // Textures come from the scene's materials.
    var textures = scene.Materials.SelectMany(m => m.Textures).ToList();

    // Source resources: prefer the BuildBuyGraph's resource set when present, else the linked set.
    IReadOnlyList<ResourceMetadata> sourceResources = graph.BuildBuyGraph is { } g
        ? new[] { g.ModelResource }
            .Concat(g.ModelLodResources)
            .Concat(g.MaterialResources)
            .Concat(g.TextureResources)
            .Concat(g.IdentityResources)
            .ToList()
        : packageResources;

    var materialManifest = graph.BuildBuyGraph?.Materials;

    var request = new SceneExportRequest(
        AssetSlug: slug,
        Scene: scene,
        OutputDirectory: UnityAssetsDir,
        SourceResources: sourceResources,
        Textures: textures,
        Diagnostics: sceneResult.Diagnostics,
        MaterialManifest: materialManifest);

    Console.WriteLine($"Exporting to: {Path.Combine(UnityAssetsDir, slug)}");
    var export = await fbxExporter.ExportAsync(request, ct);

    Console.WriteLine();
    Console.WriteLine($"Export: Success={export.Success} Message={export.Message}");
    Console.WriteLine($"Output FBX path: {export.OutputPath}");

    // List the texture PNGs that were written.
    Console.WriteLine($"Textures written ({textures.Count}):");
    foreach (var tex in textures)
    {
        Console.WriteLine($"  - {tex.Slot}: {tex.FileName} ({tex.PngBytes.Length} bytes)");
    }

    // Post-export cleanup: blacken the green that EA's RLE2 textures leave in fully
    // transparent blocks, so Unity's opaque HDRP material does not bleed acid-green.
    CleanupDiffuseTextures(slug);

    // ALSO write OBJ + MTL (plain text) into the SAME asset folder. Assimp's ASCII FBX
    // imports into Unity as an empty GameObject; a straightforward OBJ avoids the FBX
    // node-graph issues and gives a visible mesh.
    WriteObjAndMtl(slug, scene);

    // Emit swatches.json + per-swatch textures for every MTST variant the scene carries.
    // (Additive — the single-asset OBJ/FBX above is unchanged.)
    SwatchExporter.WriteBuildBuySwatches(UnityAssetsDir, slug, scene);

    // Emit rig.json. Build/Buy scenes are usually unskinned (bones may be empty) but we still
    // write a valid rig.json so the Unity builder has a uniform per-asset input.
    RigExporter.WriteRigJson(UnityAssetsDir, slug, scene);
}

// ---------------------------------------------------------------------------
// exportcas <packagePath> <fullTgi> <slug>
//
// CAS (Create-a-Sim) export path. Resolves the CASP AssetSummary from the CAS domain,
// gathers linked resources, builds the CAS asset graph, builds the scene via the CAS
// path (BuildSceneAsync(CasAssetGraph)), then reuses the SAME WriteObjAndMtl + texture
// writing as the Build/Buy path. Exports STATIC / bind-pose — bones/skin weights are
// ignored (OBJ cannot carry skinning).
// ---------------------------------------------------------------------------
async Task RunExportCasAsync(string packagePath, string fullTgi, string slug)
{
    Console.WriteLine($"Initializing index...");
    await indexStore.InitializeAsync(ct);

    if (!TryParseFullTgi(fullTgi, out var type, out var group, out var fullInstance))
    {
        Console.Error.WriteLine($"Could not parse fullTgi '{fullTgi}'. Expected Type:Group:Instance in hex, e.g. 0319E4F1:00000000:0123456789ABCDEF.");
        return;
    }

    Console.WriteLine($"Resolving CAS asset: package='{packagePath}' tgi={type:X8}:{group:X8}:{fullInstance:X16} slug='{slug}'");

    // Re-query the CAS domain and match by FullTgi to recover the real CASP AssetSummary.
    var summary = await ResolveAssetSummaryAsync(packagePath, fullTgi, AssetBrowserDomain.Cas);
    if (summary is null)
    {
        Console.Error.WriteLine("Could not resolve a CAS AssetSummary matching that package + fullTgi. Aborting.");
        return;
    }
    Console.WriteLine($"Resolved CAS AssetSummary: '{summary.DisplayName}' kind={summary.AssetKind} category='{summary.Category}' rootType={summary.RootTypeName ?? summary.RootKey.TypeName} linked={summary.LinkedResourceCount}");

    // Gather the linked resources for this CASP instance, then build the CAS asset graph.
    var graphBuilder = GraphBuilder();
    var sceneBuilder = SceneBuilder();
    var fbxExporter = FbxExporter();
    var packageResources = await indexStore.GetResourcesByInstanceAsync(packagePath, fullInstance, ct);
    Console.WriteLine($"GetResourcesByInstanceAsync returned {packageResources.Count} linked resource(s).");

    var graph = await graphBuilder.BuildAssetGraphAsync(summary, packageResources, ct);
    foreach (var d in graph.Diagnostics)
    {
        Console.WriteLine($"[graph] {d}");
    }

    if (graph.CasGraph is not { } casGraph)
    {
        Console.Error.WriteLine("Asset graph did not produce a CasGraph; cannot use the CAS scene path. Aborting.");
        return;
    }

    Console.WriteLine(
        $"Building scene from CasAssetGraph (geometries={casGraph.GeometryResources.Count}, rigs={casGraph.RigResources.Count}, materials={casGraph.MaterialResources.Count}, textures={casGraph.TextureResources.Count}, supported={casGraph.IsSupported}, subset='{casGraph.SupportedSubset}')...");

    var sceneResult = await sceneBuilder.BuildSceneAsync(casGraph, ct);

    Console.WriteLine();
    Console.WriteLine($"Scene build: Success={sceneResult.Success} Status={sceneResult.Status}");
    foreach (var d in sceneResult.Diagnostics)
    {
        Console.WriteLine($"[scene] {d}");
    }

    if (!sceneResult.Success || sceneResult.Scene is null)
    {
        Console.Error.WriteLine("CAS scene build did not succeed; nothing to export.");
        return;
    }

    var scene = sceneResult.Scene;
    var meshCount = scene.Meshes.Count;
    var totalVertices = scene.Meshes.Sum(m => m.Positions.Count / 3);
    var boneCount = scene.Bones.Count;
    Console.WriteLine($"Scene ready: {meshCount} mesh(es), {totalVertices} total vertices, {scene.Materials.Count} material(s), {boneCount} bone(s) (exported STATIC / bind-pose — skinning ignored).");

    // Textures come from the scene's materials, same as the Build/Buy path.
    var textures = scene.Materials.SelectMany(m => m.Textures).ToList();

    // Source resources: the full CAS graph resource set so the FBX exporter can copy textures.
    var sourceResources = new[] { casGraph.CasPartResource }
        .Concat(casGraph.GeometryResources)
        .Concat(casGraph.RigResources)
        .Concat(casGraph.MaterialResources)
        .Concat(casGraph.TextureResources)
        .Concat(casGraph.IdentityResources)
        .GroupBy(static r => r.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
        .Select(static g => g.First())
        .ToList();

    var request = new SceneExportRequest(
        AssetSlug: slug,
        Scene: scene,
        OutputDirectory: UnityAssetsDir,
        SourceResources: sourceResources,
        Textures: textures,
        Diagnostics: sceneResult.Diagnostics,
        MaterialManifest: casGraph.Materials);

    Console.WriteLine($"Exporting to: {Path.Combine(UnityAssetsDir, slug)}");
    var export = await fbxExporter.ExportAsync(request, ct);

    Console.WriteLine();
    Console.WriteLine($"Export: Success={export.Success} Message={export.Message}");
    Console.WriteLine($"Output FBX path: {export.OutputPath}");

    Console.WriteLine($"Textures written ({textures.Count}):");
    foreach (var tex in textures)
    {
        Console.WriteLine($"  - {tex.Slot}: {tex.FileName} ({tex.PngBytes.Length} bytes)");
    }

    // Post-export cleanup: blacken the green that EA's RLE2 textures leave in fully
    // transparent blocks, so Unity's opaque HDRP material does not bleed acid-green.
    CleanupDiffuseTextures(slug);

    // Reuse the SAME OBJ + MTL writer as the Build/Buy path (with the baked-in normals fix).
    WriteObjAndMtl(slug, scene);

    // Emit swatches.json + baked per-swatch diffuses for the CAS recolor (color_shift_mask +
    // SwatchColors). SwatchColors come from the CASP itself; parse the CASP resource bytes.
    var swatchColors = await ReadCaspSwatchColorsAsync(casGraph.CasPartResource);
    Console.WriteLine($"[swatches] CASP swatch colors parsed: {swatchColors.Count}.");
    SwatchExporter.WriteCasSwatches(UnityAssetsDir, slug, scene, swatchColors);

    // Emit rig.json: the rigged mesh (geometry + skeleton + per-vertex skin weights) for the
    // Unity SkinnedMeshRenderer builder. Additive — the static OBJ/FBX above is unchanged.
    RigExporter.WriteRigJson(UnityAssetsDir, slug, scene);
}

// Parse the CASP resource bytes and return its packed AARRGGBB SwatchColors. Returns an empty
// list on any read/parse failure (the swatch writer then emits a single-swatch manifest).
async Task<IReadOnlyList<uint>> ReadCaspSwatchColorsAsync(ResourceMetadata casPartResource)
{
    try
    {
        var bytes = await ResourceCatalog().GetResourceBytesAsync(
            casPartResource.PackagePath, casPartResource.Key, raw: false, ct);
        var casPart = Ts4CasPart.Parse(bytes);
        return casPart.SwatchColors;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[swatches] Failed to parse CASP swatch colors ({ex.GetType().Name}: {ex.Message}); single-swatch manifest will be emitted.");
        return Array.Empty<uint>();
    }
}

// ---------------------------------------------------------------------------
// exportsim <age> <gender> [<skintoneInstanceHexOrDefault>] <slug>
//
// Assembles a FULL default human Sim (body + head + eyes) headlessly and writes a multi-part
// character.json for Unity. The skintone arg is optional: "default" (or omitted) uses the
// resolver's default Human tone (the App's DefaultHumanSkintoneInstance / earliest Human tone);
// otherwise a 16-hex instance id selects a specific TONE. The index must already be built.
// ---------------------------------------------------------------------------
async Task RunExportSimAsync(string[] argv)
{
    // Positional parse: the LAST arg is always the slug. arg[1]=age, arg[2]=gender, and the
    // optional arg[3] (when there are 5 args total) is the skintone selector.
    var age = argv[1];
    var gender = argv[2];
    string slug;
    string skintoneArg;
    if (argv.Length >= 5)
    {
        skintoneArg = argv[3];
        slug = argv[4];
    }
    else
    {
        skintoneArg = "default";
        slug = argv[3];
    }

    // Resolve the skintone instance. "default"/empty → the App's default Human tone; else hex.
    ulong skintoneInstance;
    if (string.IsNullOrWhiteSpace(skintoneArg) ||
        string.Equals(skintoneArg, "default", StringComparison.OrdinalIgnoreCase))
    {
        // Matches SimConstructorViewModel.DefaultHumanSkintoneInstance — the earliest Human tone.
        skintoneInstance = 0x0000000000005545ul;
    }
    else
    {
        var hex = skintoneArg.TrimStart('#').Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (!ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out skintoneInstance))
        {
            Console.Error.WriteLine($"Could not parse skintone instance '{skintoneArg}'. Use 'default' or a 16-hex instance id.");
            return;
        }
    }

    Console.WriteLine("Initializing index...");
    await indexStore.InitializeAsync(ct);

    var ok = await CharacterExporter.RunAsync(
        UnityAssetsDir,
        SyntheticSimService(),
        SimRenderer(),
        age,
        gender,
        skintoneInstance,
        slug,
        ct);

    if (!ok)
    {
        Console.Error.WriteLine("exportsim did not complete successfully.");
    }
}

// ---------------------------------------------------------------------------
// exportsimhd <age> <gender> [<skintoneInstanceHexOrDefault>] <slug>
//
// Identical assembly to exportsim, but with the ModsFromDev MOD-OVERRIDE layer armed FIRST so the
// Better Body [Female] body GEOMs + the wild_guy HD base skin win by TGI. The decorator is built
// from the mod packages, stored in ModOverrideHolder, and then EVERY catalog consumer (synthetic
// service, Sim renderer, scene builder) transparently reads mod bytes for the overridden TGIs.
// ---------------------------------------------------------------------------
async Task RunExportSimHdAsync(string[] argv)
{
    var age = argv[1];
    var gender = argv[2];
    string slug;
    string skintoneArg;
    if (argv.Length >= 5) { skintoneArg = argv[3]; slug = argv[4]; }
    else { skintoneArg = "default"; slug = argv[3]; }

    ulong skintoneInstance;
    if (string.IsNullOrWhiteSpace(skintoneArg) ||
        string.Equals(skintoneArg, "default", StringComparison.OrdinalIgnoreCase))
    {
        skintoneInstance = 0x0000000000005545ul;
    }
    else
    {
        var hex = skintoneArg.TrimStart('#').Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (!ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out skintoneInstance))
        {
            Console.Error.WriteLine($"Could not parse skintone instance '{skintoneArg}'. Use 'default' or a 16-hex instance id.");
            return;
        }
    }

    Console.WriteLine("Initializing index...");
    await indexStore.InitializeAsync(ct);

    // Build + ARM the mod-override layer over the REAL catalog. Done before SyntheticSimService()/
    // SimRenderer() are resolved so those singletons see the decorator.
    Console.WriteLine("============================================================");
    Console.WriteLine("exportsimhd: arming ModsFromDev OVERRIDES (Better Body [Female] + wild_guy HD skin)...");
    var realCatalog = host.Services.GetRequiredService<LlamaResourceCatalogService>();
    var modOverride = await ModOverrideResourceCatalogService.BuildAdultFemaleHdAsync(realCatalog, indexStore, ct);
    foreach (var line in modOverride.Log)
    {
        Console.WriteLine(line);
    }
    ModOverrideHolder.Current = modOverride;
    Console.WriteLine("============================================================");

    var ok = await CharacterExporter.RunAsync(
        UnityAssetsDir,
        SyntheticSimService(),
        SimRenderer(),
        age,
        gender,
        skintoneInstance,
        slug,
        ct);

    // Surface the live "served from mod" hit log so the report shows exactly which TGIs the mod won.
    Console.WriteLine();
    Console.WriteLine("------------------------------------------------------------");
    Console.WriteLine("Mod-override SERVED log (TGIs actually read from mod packages):");
    var served = modOverride.Log
        .Where(l => l.Contains("SERVED", StringComparison.Ordinal))
        .ToList();
    if (served.Count == 0)
    {
        Console.WriteLine("  (none — no overridden TGI was requested during assembly!)");
    }
    foreach (var line in served)
    {
        Console.WriteLine(line);
    }
    Console.WriteLine("------------------------------------------------------------");

    ModOverrideHolder.Current = null;

    if (!ok)
    {
        Console.Error.WriteLine("exportsimhd did not complete successfully.");
    }
}

// ---------------------------------------------------------------------------
// bakeskindebug <age> <gender> [<skintone>] <slug> (diagnostic)
//
// Resolves the Sim's skintone render summary EXACTLY as exportsim does, then composes the
// skin atlas through SkinAtlasComposer.ComposeAtlasDebug — which writes every compositor
// INPUT (base, detail rows, overlays at NATIVE resolution) and every composite STAGE
// (detail canvas before/after the hole-fill, final atlas before the DilateOpaque gutter
// pass) as its own PNG into <slug>/Textures/_debug/. Then it runs the SAME DilateOpaque
// gutter pass exportsim uses and dumps the after-dilate atlas too, so the full chain is
// visible. The _debug PNGs are left on disk for review.
// ---------------------------------------------------------------------------
async Task RunBakeSkinDebugAsync(string[] argv)
{
    var age = argv[1];
    var gender = argv[2];
    string slug;
    string skintoneArg;
    if (argv.Length >= 5) { skintoneArg = argv[3]; slug = argv[4]; }
    else { skintoneArg = "default"; slug = argv[3]; }

    ulong skintoneInstance;
    if (string.IsNullOrWhiteSpace(skintoneArg) ||
        string.Equals(skintoneArg, "default", StringComparison.OrdinalIgnoreCase))
    {
        skintoneInstance = 0x0000000000005545ul;
    }
    else
    {
        var hex = skintoneArg.TrimStart('#').Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (!ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out skintoneInstance))
        {
            Console.Error.WriteLine($"Could not parse skintone instance '{skintoneArg}'. Use 'default' or a 16-hex instance id.");
            return;
        }
    }

    Console.WriteLine("Initializing index...");
    await indexStore.InitializeAsync(ct);

    var synthetic = SyntheticSimService();
    SimSkintoneRenderSummary? skin;
    try
    {
        skin = await synthetic.ResolveSkintoneRenderAsync(age, gender, skintoneInstance, ct);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[bakeskindebug] ResolveSkintoneRenderAsync FAILED ({ex.GetType().Name}: {ex.Message}).");
        return;
    }
    if (skin is null)
    {
        Console.Error.WriteLine("[bakeskindebug] No skintone render summary resolved.");
        return;
    }

    // Same default "fit-leaning" physique exportsim supplies for synthesised Sims.
    var physiqueWeights = skin.PhysiqueWeights is { Count: 4 } w &&
                          (w[0] > 0f || w[1] > 0f || w[2] > 0f || w[3] > 0f)
        ? w
        : new[] { 0f, 0.6f, 0.4f, 0f };

    var debugDir = Path.Combine(UnityAssetsDir, slug, "Textures", "_debug");
    Console.WriteLine($"[bakeskindebug] tone=0x{skintoneInstance:X16} -> dumping intermediates into {debugDir}");

    byte[]? atlas;
    try
    {
        atlas = SkinAtlasComposer.ComposeAtlasDebug(skin, physiqueWeights, debugDir);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[bakeskindebug] ComposeAtlasDebug FAILED ({ex.GetType().Name}: {ex.Message}).");
        return;
    }
    if (atlas is not { Length: > 0 })
    {
        Console.Error.WriteLine("[bakeskindebug] ComposeAtlasDebug produced no atlas.");
        return;
    }

    // After-dilate: write the atlas, run the SAME DilateOpaque exportsim uses, dump result.
    var afterDilatePath = Path.Combine(debugDir, "09_final_atlas_after_dilate.png");
    await File.WriteAllBytesAsync(afterDilatePath, atlas, ct);
    try
    {
        if (TextureCleanup.HasTransparentPixels(afterDilatePath))
        {
            var filled = TextureCleanup.DilateOpaque(afterDilatePath, 16);
            Console.WriteLine($"[bakeskindebug]   stage 09_final_atlas_after_dilate: dilated {filled} gutter pixel(s) -> {Path.GetFileName(afterDilatePath)}");
        }
        else
        {
            Console.WriteLine($"[bakeskindebug]   stage 09_final_atlas_after_dilate: atlas opaque, no dilation -> {Path.GetFileName(afterDilatePath)}");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[bakeskindebug]   stage 09_final_atlas_after_dilate: dilate FAILED ({ex.GetType().Name}: {ex.Message}).");
    }

    Console.WriteLine($"[bakeskindebug] DONE. Inspect PNGs in {debugDir}");
}

// ---------------------------------------------------------------------------
// dumpskinlayers <age> <gender> [<skintone>] (diagnostic)
//
// Resolves the skin LAYER set EXACTLY as exportsim does and dumps each layer (base, detail rows,
// physique rows, tone face overlay, face CAS overlays) PLUS the assembled scene's head-shell
// full-body diffuse (the Model-B "with underwear" candidate) as its own native-resolution PNG
// into <UnityAssetsDir>/_skinlayers/, with a per-layer alpha/dimension report. Investigation only.
// ---------------------------------------------------------------------------
async Task RunDumpSkinLayersAsync(string[] argv)
{
    var age = argv[1];
    var gender = argv[2];
    var skintoneArg = argv.Length >= 4 ? argv[3] : "default";

    ulong skintoneInstance;
    if (string.IsNullOrWhiteSpace(skintoneArg) ||
        string.Equals(skintoneArg, "default", StringComparison.OrdinalIgnoreCase))
    {
        skintoneInstance = 0x0000000000005545ul;
    }
    else
    {
        var hex = skintoneArg.TrimStart('#').Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (!ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out skintoneInstance))
        {
            Console.Error.WriteLine($"Could not parse skintone instance '{skintoneArg}'. Use 'default' or a 16-hex instance id.");
            return;
        }
    }

    Console.WriteLine("Initializing index...");
    await indexStore.InitializeAsync(ct);

    var ok = await SkinLayerDumper.RunAsync(
        UnityAssetsDir,
        SyntheticSimService(),
        SimRenderer(),
        age,
        gender,
        skintoneInstance,
        ct);
    if (!ok)
    {
        Console.Error.WriteLine("dumpskinlayers did not complete successfully.");
    }
}

// ---------------------------------------------------------------------------
// scancasmask <packagePath> (diagnostic)
//
// Scans every CASPart resource in ONE package file (bytes read from that same file, so EP
// packages work where the index's override-resolved package path would not), parses each,
// and prints those that expose a color_shift_mask texture reference AND have >1 SwatchColors
// — the items that produce a multi-swatch BAKED CAS swatches.json. Used to pick a real CAS
// recolor test case.
// ---------------------------------------------------------------------------
async Task RunScanCasMaskAsync(string packagePath)
{
    if (!File.Exists(packagePath))
    {
        Console.Error.WriteLine($"Package not found: {packagePath}");
        return;
    }

    var catalog = ResourceCatalog();
    var source = new DataSourceDefinition(Guid.NewGuid(), Path.GetFileName(packagePath), packagePath, SourceKind.Game);
    Console.WriteLine($"Scanning package CASPs: {packagePath}");
    var scan = await catalog.ScanPackageAsync(source, packagePath, progress: null, ct);

    var casps = scan.Resources
        .Where(r => string.Equals(r.Key.TypeName, "CASPart", StringComparison.OrdinalIgnoreCase))
        .ToList();
    Console.WriteLine($"Found {casps.Count} CASPart resource(s); checking for color_shift_mask + multi-swatch...");

    var hits = 0;
    var maskOnly = 0;
    var multiSwatchOnly = 0;
    var maxSwatches = 0;
    var parsed = 0;
    var maxVersion = 0u;
    var v49Plus = 0;
    foreach (var resource in casps)
    {
        IReadOnlyList<uint> swatchColors;
        bool hasMask;
        string? name;
        try
        {
            var bytes = await catalog.GetResourceBytesAsync(packagePath, resource.Key, raw: false, ct);
            var casPart = Ts4CasPart.Parse(bytes);
            swatchColors = casPart.SwatchColors;
            name = casPart.InternalName;
            hasMask = casPart.TextureReferences.Any(t =>
                string.Equals(t.Slot, "color_shift_mask", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            continue;
        }

        parsed++;
        // Peek the CASP version (first uint32) to understand color_shift_mask availability
        // (the mask key is only present for version >= 49).
        try
        {
            var raw = await catalog.GetResourceBytesAsync(packagePath, resource.Key, raw: false, ct);
            if (raw.Length >= 4)
            {
                var ver = BitConverter.ToUInt32(raw, 0);
                if (ver > maxVersion) maxVersion = ver;
                if (ver >= 49) v49Plus++;
            }
        }
        catch { /* ignore */ }

        if (swatchColors.Count > maxSwatches)
        {
            maxSwatches = swatchColors.Count;
        }
        if (hasMask)
        {
            maskOnly++;
        }
        if (swatchColors.Count > 1)
        {
            multiSwatchOnly++;
        }

        if (hasMask && swatchColors.Count > 1)
        {
            hits++;
            Console.WriteLine($"  HIT  {resource.Key.FullTgi}  swatches={swatchColors.Count}  hasMask=True  '{name}'");
            if (hits >= 25)
            {
                Console.WriteLine("  ... (stopping at 25 hits)");
                break;
            }
        }
    }

    Console.WriteLine($"Done. parsed={parsed} maxVersion={maxVersion} v49plus={v49Plus} maxSwatches={maxSwatches} hasMask={maskOnly} multiSwatch={multiSwatchOnly} maskHits={hits} in {Path.GetFileName(packagePath)}.");
}

// ---------------------------------------------------------------------------
// Post-export diffuse texture cleanup
//
// EA's RLE2 textures fill fully-transparent blocks with green: pixels with alpha==0
// decode to RGB ~ (0,162,0). The exported diffuse PNG is correct where it matters
// (the opaque UV island), but Unity's opaque HDRP material samples RGB and ignores
// alpha, so the green transparent background bleeds and the model renders acid-green.
//
// Fix at the source: for every exported *diffuse* PNG, set RGB to (0,0,0) wherever
// alpha==0 (the alpha channel is left UNTOUCHED). Normal/specular/shadow maps are
// skipped — only files whose name starts with "diffuse" (case-insensitive) are touched.
//
// Uses System.Drawing.Common (available on net8.0-windows) with Bitmap + LockBits for
// speed; textures can be up to 1024x2048. Pixels are read as 32bpp ARGB, RGB zeroed
// where A==0, written back, and the PNG is re-saved over the original file.
// ---------------------------------------------------------------------------
void CleanupDiffuseTextures(string slug)
{
    var texturesFolder = Path.Combine(UnityAssetsDir, slug, "Textures");
    if (!Directory.Exists(texturesFolder))
    {
        Console.WriteLine($"[cleanup] Textures folder not found, skipping: {texturesFolder}");
        return;
    }

    // Only diffuse PNGs — leave normal*, specular*, shadow* maps untouched.
    var diffuseFiles = Directory.EnumerateFiles(texturesFolder, "*.png")
        .Where(path => Path.GetFileName(path)
            .StartsWith("diffuse", StringComparison.OrdinalIgnoreCase))
        .ToList();

    if (diffuseFiles.Count == 0)
    {
        Console.WriteLine($"[cleanup] No diffuse*.png files found in {texturesFolder}.");
        return;
    }

    Console.WriteLine($"[cleanup] Blackening fully-transparent pixels in {diffuseFiles.Count} diffuse texture(s)...");
    foreach (var file in diffuseFiles)
    {
        try
        {
            var blackened = TextureCleanup.BlackenTransparentPixels(file);
            Console.WriteLine($"[cleanup]   {Path.GetFileName(file)}: blackened {blackened} transparent pixel(s).");
        }
        catch (Exception ex)
        {
            // Non-fatal: a single texture failing must not abort the export.
            Console.Error.WriteLine($"[cleanup]   {Path.GetFileName(file)}: FAILED ({ex.GetType().Name}: {ex.Message}).");
        }
    }
}

// ---------------------------------------------------------------------------
// OBJ + MTL writer
//
// Writes <slug>.obj and <slug>.mtl into the SAME folder the FBX exporter uses
// (<OutputDirectory>/<slug>/), referencing the diffuse PNGs the FBX exporter
// already wrote into the Textures/ subfolder. Coordinates/normals/UVs are emitted
// AS-IS (no handedness/winding conversion yet) — we just need it visible first.
// ---------------------------------------------------------------------------
void WriteObjAndMtl(string slug, CanonicalScene scene)
{
    var inv = CultureInfo.InvariantCulture;
    var assetFolder = Path.Combine(UnityAssetsDir, slug);
    Directory.CreateDirectory(assetFolder);

    // Slugs may carry subfolders ("home/sofa") — file names must be the leaf only.
    var leaf = Path.GetFileName(slug.TrimEnd('/', '\\'));
    var objPath = Path.Combine(assetFolder, $"{leaf}.obj");
    var mtlPath = Path.Combine(assetFolder, $"{leaf}.mtl");

    // Sanitize material names so they are valid single OBJ tokens (no whitespace).
    string MaterialName(int materialIndex)
    {
        var raw = materialIndex >= 0 && materialIndex < scene.Materials.Count
            ? scene.Materials[materialIndex].Name
            : null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return $"material_{Math.Max(materialIndex, 0)}";
        }

        var chars = raw.Select(ch => char.IsWhiteSpace(ch) ? '_' : ch).ToArray();
        return new string(chars);
    }

    static string F(float value, CultureInfo c) => value.ToString("R", c);

    var obj = new System.Text.StringBuilder();
    obj.Append("# Sims4UnityExport OBJ\n");
    obj.Append($"mtllib {leaf}.mtl\n");

    // OBJ uses a single global 1-based index space for v/vn/vt across the whole file,
    // so we accumulate offsets as each mesh is appended.
    var positionOffset = 1; // 1-based
    var normalOffset = 1;
    var uvOffset = 1;

    var totalVerts = 0;
    var totalNormals = 0;
    var totalUvs = 0;
    var totalFaces = 0;

    for (var meshIndex = 0; meshIndex < scene.Meshes.Count; meshIndex++)
    {
        var mesh = scene.Meshes[meshIndex];

        var meshName = string.IsNullOrWhiteSpace(mesh.Name) ? $"mesh_{meshIndex}" : mesh.Name;
        obj.Append($"o {meshName}\n");

        // Positions: v x y z
        var vertexCount = mesh.Positions.Count / 3;
        for (var i = 0; i < vertexCount; i++)
        {
            var x = mesh.Positions[(i * 3) + 0];
            var y = mesh.Positions[(i * 3) + 1];
            var z = mesh.Positions[(i * 3) + 2];
            obj.Append($"v {F(x, inv)} {F(y, inv)} {F(z, inv)}\n");
        }

        // Normals: intentionally NOT emitted. The authored Sims normals do not import
        // correctly into Unity regardless of sign (surfaces render black on default import).
        // Unity's "Calculate" — which derives normals from geometry + winding — is the only
        // thing that shades right. By omitting `vn` entirely, Unity calculates normals
        // automatically on DEFAULT import, with no manual toggle. (Winding is already
        // correct/outward, so the calculated normals point the right way.)
        var normalCount = 0;

        // UVs: vt u v — prefer Uvs, fall back to Uv0s. V is FLIPPED (1-v): the game authors UVs in
        // the DirectX convention (v=0 at the texture TOP) which the in-app viewer samples natively,
        // while OBJ/Unity expect v=0 at the BOTTOM — emitting "as-is" scrambled every pattern.
        IReadOnlyList<float> uvSource =
            mesh.Uvs is { Count: > 0 } ? mesh.Uvs
            : (mesh.Uv0s is { Count: > 0 } ? mesh.Uv0s : Array.Empty<float>());
        var uvCount = uvSource.Count / 2;
        for (var i = 0; i < uvCount; i++)
        {
            var u = uvSource[(i * 2) + 0];
            var v = 1f - uvSource[(i * 2) + 1];
            obj.Append($"vt {F(u, inv)} {F(v, inv)}\n");
        }

        // Faces, grouped under this mesh's material.
        obj.Append($"usemtl {MaterialName(mesh.MaterialIndex)}\n");

        var hasNormals = normalCount > 0;
        var hasUvs = uvCount > 0;

        // Build a v/vt/vn reference for a mesh-local vertex index (0-based), applying the
        // per-mesh global offsets. OBJ is 1-based, hence the +offset (which already starts at 1).
        string Ref(int localIndex)
        {
            var v = localIndex + positionOffset;
            if (hasUvs && hasNormals)
            {
                return $"{v}/{localIndex + uvOffset}/{localIndex + normalOffset}";
            }
            if (hasUvs)
            {
                return $"{v}/{localIndex + uvOffset}";
            }
            if (hasNormals)
            {
                return $"{v}//{localIndex + normalOffset}";
            }
            return $"{v}";
        }

        for (var i = 0; i + 2 < mesh.Indices.Count; i += 3)
        {
            var a = mesh.Indices[i];
            var b = mesh.Indices[i + 1];
            var c = mesh.Indices[i + 2];
            obj.Append($"f {Ref(a)} {Ref(b)} {Ref(c)}\n");
            totalFaces++;
        }

        // Advance the global offsets by what this mesh contributed.
        positionOffset += vertexCount;
        normalOffset += normalCount;
        uvOffset += uvCount;

        totalVerts += vertexCount;
        totalNormals += normalCount;
        totalUvs += uvCount;
    }

    File.WriteAllText(objPath, obj.ToString());

    // MTL: one newmtl per material; point map_Kd at the primary diffuse PNG already written
    // into Textures/ by the FBX exporter.
    var mtl = new System.Text.StringBuilder();
    mtl.Append("# Sims4UnityExport MTL\n");
    for (var materialIndex = 0; materialIndex < scene.Materials.Count; materialIndex++)
    {
        var material = scene.Materials[materialIndex];
        mtl.Append($"newmtl {MaterialName(materialIndex)}\n");
        // Real material color: doors/windows carry COLOR-ONLY materials (tinted glass, frame paint)
        // with no diffuse texture — a hardcoded gray made the Unity side slap the fallback TEXTURE
        // onto glass. Transparency rides along as the standard 'd' (dissolve) statement.
        // Some records decode to BIT-PATTERN GARBAGE — seen: (1, 1E-45, 0) = a pure-red window
        // pane, (0, 1, 0) = green door glass; every channel a denormal or an exact 0/1. Neutral
        // gray beats neon.
        static bool GarbageChannel(float c) => !float.IsFinite(c) || c < 0f || c > 1f || (c > 0f && c < 1e-6f);
        static bool GarbageColor(float r, float g, float b)
        {
            if (GarbageChannel(r) || GarbageChannel(g) || GarbageChannel(b))
            {
                return true;
            }
            static bool Extreme(float c) => c is 0f or 1f;
            return Extreme(r) && Extreme(g) && Extreme(b) && !(r == g && g == b); // saturated primaries; keeps black/white/grays
        }
        var baseColor = material.ApproximateBaseColor;
        var (kr, kg, kb) = baseColor is null ? (0.8f, 0.8f, 0.8f) : (baseColor.R, baseColor.G, baseColor.B);
        if (GarbageColor(kr, kg, kb))
        {
            (kr, kg, kb) = (0.62f, 0.62f, 0.62f);
        }
        mtl.Append($"Kd {F(kr, inv)} {F(kg, inv)} {F(kb, inv)}\n");
        if (material.IsTransparent)
        {
            var dissolve = Math.Clamp(baseColor?.A ?? 0.5f, 0.05f, 0.9f);
            mtl.Append($"d {F(dissolve, inv)}\n");
        }
        if (material.IsMirror)
        {
            mtl.Append("illum 3\n"); // reflection on — the Unity builder makes this a reflective mirror
        }

        var diffuse = FindDiffuseTexture(material);
        if (diffuse is not null)
        {
            // Textures live in the Textures/ subfolder next to the .mtl. The game material's UV
            // TRANSFORM rides along as standard MTL options (-s scale, -o offset) — Unity's importer
            // ignores them, but our scene builder parses map_Kd itself and applies the transform to
            // the HDRP material (with the V-flip compensation).
            var rel = $"Textures/{diffuse.FileName}";
            var su = diffuse.UvScaleU;
            var sv = diffuse.UvScaleV;
            var ou = diffuse.UvOffsetU;
            var ov = diffuse.UvOffsetV;
            // Degenerate transforms (seen: scale 0.001 + offset ≈0.999 on window frames — one
            // texel smeared across the whole mesh) are decode artifacts of a field with other
            // semantics; identity renders the texture as authored.
            static bool PlausibleScale(float s) => float.IsFinite(s) && Math.Abs(s) >= 0.01f && Math.Abs(s) <= 100f;
            if (!PlausibleScale(su) || !PlausibleScale(sv) || !float.IsFinite(ou) || !float.IsFinite(ov))
            {
                (su, sv, ou, ov) = (1f, 1f, 0f, 0f);
            }
            if (Math.Abs(su - 1f) > 0.0001f || Math.Abs(sv - 1f) > 0.0001f ||
                Math.Abs(ou) > 0.0001f || Math.Abs(ov) > 0.0001f)
            {
                mtl.Append($"map_Kd -s {F(su, inv)} {F(sv, inv)} 1 -o {F(ou, inv)} {F(ov, inv)} 0 {rel}\n");
            }
            else
            {
                mtl.Append($"map_Kd {rel}\n");
            }
        }

        mtl.Append('\n');
    }

    // Handle the case where the scene reported zero materials (FBX exporter emits a
    // DefaultMaterial in that case); keep parity so usemtl references resolve.
    if (scene.Materials.Count == 0)
    {
        mtl.Append("newmtl material_0\n");
        mtl.Append("Kd 0.8 0.8 0.8\n\n");
    }

    File.WriteAllText(mtlPath, mtl.ToString());

    Console.WriteLine();
    Console.WriteLine($"Output OBJ path: {objPath}");
    Console.WriteLine($"Output MTL path: {mtlPath}");
    Console.WriteLine($"OBJ counts: vertices={totalVerts} normals={totalNormals} uvs={totalUvs} faces={totalFaces}");
}

// Pick the primary diffuse texture for a material: prefer an explicit BaseColor semantic,
// then a slot/name that looks like diffuse/albedo/basecolor, else the first texture.
static CanonicalTexture? FindDiffuseTexture(CanonicalMaterial material)
{
    if (material.Textures.Count == 0)
    {
        return null;
    }

    var bySemantic = material.Textures.FirstOrDefault(t => t.Semantic == CanonicalTextureSemantic.BaseColor);
    if (bySemantic is not null)
    {
        return bySemantic;
    }

    var bySlot = material.Textures.FirstOrDefault(t =>
        (t.Slot?.IndexOf("diffuse", StringComparison.OrdinalIgnoreCase) >= 0) ||
        (t.Slot?.IndexOf("albedo", StringComparison.OrdinalIgnoreCase) >= 0) ||
        (t.Slot?.IndexOf("basecolor", StringComparison.OrdinalIgnoreCase) >= 0) ||
        (t.Slot?.IndexOf("base color", StringComparison.OrdinalIgnoreCase) >= 0));
    if (bySlot is not null)
    {
        return bySlot;
    }

    return material.Textures[0];
}

// Re-query assets in the given domain and match by FullTgi to get the real AssetSummary.
//
// First pass narrows by package (cheap paging). But the index merges multiple shards and
// applies override-order resolution (Delta > Full), so the SAME FullTgi may be surfaced
// under a DIFFERENT package than the one supplied — in which case the package-scoped pass
// misses. Second pass therefore drops the package filter and matches by FullTgi alone
// (FullTgi uniquely identifies the asset content; the override resolver picks the bytes).
async Task<AssetSummary?> ResolveAssetSummaryAsync(string packagePath, string fullTgi, AssetBrowserDomain domain)
{
    // Pass 1: package-scoped (fast common case).
    var scoped = await ScanForFullTgiAsync(fullTgi, domain, packagePath, maxPages: 400);
    if (scoped is not null)
    {
        return scoped;
    }

    // Pass 2: unscoped — match by FullTgi across all packages.
    Console.WriteLine("Package-scoped resolve missed (override-order surfaced the asset under another package); retrying unscoped by FullTgi...");
    return await ScanForFullTgiAsync(fullTgi, domain, packageFilter: string.Empty, maxPages: 4000);
}

// Page through the asset query (optionally package-filtered) and return the first asset
// whose FullTgi (or canonical root TGI) matches.
async Task<AssetSummary?> ScanForFullTgiAsync(string fullTgi, AssetBrowserDomain domain, string packageFilter, int maxPages)
{
    for (var page = 0; page < maxPages; page++)
    {
        var query = new AssetBrowserQuery(
            SourceScope: new SourceScope(true, true, true),
            SearchText: string.Empty,
            Domain: domain,
            CategoryText: string.Empty,
            PackageText: packageFilter,
            PackageRelativeText: string.Empty,
            HasThumbnailOnly: false,
            VariantsOnly: false,
            Sort: AssetBrowserSort.Name,
            Offset: page * PageSize,
            WindowSize: PageSize);

        var result = await indexStore.QueryAssetsAsync(query, ct);
        if (result.Items.Count == 0)
        {
            break;
        }

        foreach (var item in result.Items)
        {
            if (string.Equals(item.RootKey.FullTgi, fullTgi, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.CanonicalRootTgi, fullTgi, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        if (!result.HasMore)
        {
            break;
        }
    }

    return null;
}

// Locate a renderable root resource (Model preferred) among the linked resources for fallback.
ResourceMetadata? FindModelResource(
    IReadOnlyList<ResourceMetadata> resources,
    AssetSummary summary,
    uint type,
    uint group,
    ulong fullInstance)
{
    // 1) Exact root-key match.
    var exact = resources.FirstOrDefault(r =>
        r.Key.Type == type && r.Key.Group == group && r.Key.FullInstance == fullInstance);
    if (exact is not null && IsSceneRoot(exact))
    {
        return exact;
    }

    // 2) Any Model, then ModelLOD, then Geometry.
    return resources.FirstOrDefault(r => r.Key.TypeName == "Model")
        ?? resources.FirstOrDefault(r => r.Key.TypeName == "ModelLOD")
        ?? resources.FirstOrDefault(r => r.Key.TypeName == "Geometry")
        ?? (exact is not null && IsSceneRoot(exact) ? exact : null);
}

static bool IsSceneRoot(ResourceMetadata r) =>
    r.Key.TypeName is "Model" or "ModelLOD" or "Geometry";

// Parse "Type:Group:Instance" (hex, as printed by search) into numeric components.
static bool TryParseFullTgi(string fullTgi, out uint type, out uint group, out ulong fullInstance)
{
    type = 0;
    group = 0;
    fullInstance = 0;

    var parts = fullTgi.Split(':');
    if (parts.Length != 3)
    {
        return false;
    }

    return uint.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out type)
        && uint.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out group)
        && ulong.TryParse(parts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out fullInstance);
}

// ---------------------------------------------------------------------------
// exportchar <definitionPath> [<slug>]
//
// Reads a CharacterDefinition (the SELECTIONS) + catalog.json (the registry of vetted options),
// applies the chosen bodyMesh override (reusing the ModOverride decorator), assembles the Sim
// (reusing the exportsim path), resolves + exports the SKIN LAYERS as separate UV-aligned PNGs
// (base color, each selected detail relief, the eye overlay), bakes a preview skin_atlas.png +
// skin_normal.png with the existing compositor, and writes character.json with the layered skin.
// ---------------------------------------------------------------------------
async Task RunExportCharAsync(string definitionPath, string? slugOverride)
{
    Console.WriteLine("Initializing index...");
    await indexStore.InitializeAsync(ct);

    await CharacterDefinitionExporter.RunAsync(
        host.Services,
        UnityAssetsDir,
        indexStore,
        definitionPath,
        slugOverride,
        BuildExportHost,
        scanMorphs: false,
        ct);
}

async Task RunScanMorphsAsync(string definitionPath)
{
    Console.WriteLine("Initializing index...");
    await indexStore.InitializeAsync(ct);

    await CharacterDefinitionExporter.RunAsync(
        host.Services,
        UnityAssetsDir,
        indexStore,
        definitionPath,
        slugOverride: null,
        BuildExportHost,
        scanMorphs: true,
        ct);
}

// Host factory — exportchar uses it to build a FRESH host per body-mesh variant (the
// IResourceCatalogService singleton binds the ModOverride decorator on first resolution, so
// rendering a different body mesh needs a new host). Keep in sync with the top-level `host`.
static IHost BuildExportHost() =>
    Host.CreateDefaultBuilder()
        .ConfigureServices(services =>
        {
            services.AddSingleton(IndexingRunOptions.CreateDefault());
            services.AddSingleton<ICacheService, FileSystemCacheService>();
            services.AddSingleton<IIndexStore, SqliteIndexStore>();
            services.AddSingleton<IPackageScanner, FileSystemPackageScanner>();
            services.AddSingleton<LlamaResourceCatalogService>();
            services.AddSingleton<IResourceCatalogService>(sp =>
                (IResourceCatalogService?)ModOverrideHolder.Current
                    ?? sp.GetRequiredService<LlamaResourceCatalogService>());
            services.AddSingleton<IResourceMetadataEnrichmentService, ResourceMetadataEnrichmentService>();
            services.AddSingleton<IAssetGraphBuilder, ExplicitAssetGraphBuilder>();
            services.AddSingleton<ITextureDecodeService, BasicTextureDecodeService>();
            services.AddSingleton<ISceneBuildService, BuildBuySceneBuildService>();
            services.AddSingleton<IAudioDecodeService, BasicAudioDecodeService>();
            services.AddSingleton<IFbxExportService, AssimpFbxExportService>();
            services.AddSingleton<ISyntheticSimService, SyntheticSimService>();
            services.AddSingleton<BondMorphResolver>();
            services.AddSingleton<DeformerMapResolver>();
            services.AddSingleton<BlendGeometryResolver>();
            services.AddSingleton<Sims4ResourceExplorer.Preview.SimRender.SimRigLoader>();
            services.AddSingleton<Sims4ResourceExplorer.Preview.SimRender.ISimAssetGraphRenderer,
                Sims4ResourceExplorer.Preview.SimRender.SimAssetGraphRenderer>();
        })
        .Build();

// ---------------------------------------------------------------------------
// Console table helpers
// ---------------------------------------------------------------------------
void PrintHeader()
{
    Console.WriteLine($"{"#",-3} {"DisplayName",-40} {"Category",-22} {"Var",4} {"Lnk",4} {"RootKey.FullTgi",-38} {"RootType",-18} PackagePath");
    Console.WriteLine(new string('-', 200));
}

void PrintRow(int index, AssetSummary a)
{
    var name = Truncate(a.DisplayName, 40);
    var cat = Truncate(a.Category ?? string.Empty, 22);
    // The index stores AssetSummary.RootKey.TypeName as an empty string; the real
    // root type name lives in the separate RootTypeName property — prefer it.
    var rootTypeName = !string.IsNullOrEmpty(a.RootTypeName)
        ? a.RootTypeName
        : a.RootKey.TypeName;
    var rootType = Truncate(rootTypeName ?? string.Empty, 18);
    Console.WriteLine(
        $"{index,-3} {name,-40} {cat,-22} {a.VariantCount,4} {a.LinkedResourceCount,4} {a.RootKey.FullTgi,-38} {rootType,-18} {a.PackagePath}");
}

static string Truncate(string value, int max) =>
    value.Length <= max ? value : value.Substring(0, max - 1) + "…";

void PrintUsage()
{
    Console.WriteLine("Sims4UnityExport — search/export Build/Buy objects from the prebuilt index.");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  search <term> [buildbuy|cas|sim]");
    Console.WriteLine("      Search assets by name across all sources; prints up to 50 rows.");
    Console.WriteLine("      The optional domain defaults to buildbuy (back-compat).");
    Console.WriteLine();
    Console.WriteLine("  export <packagePath> <fullTgi> <slug>");
    Console.WriteLine("      Export a Build/Buy object. <fullTgi> is Type:Group:Instance in hex.");
    Console.WriteLine($"      Exports FBX + OBJ/MTL + PNG textures into {UnityAssetsDir}\\<slug>.");
    Console.WriteLine();
    Console.WriteLine("  exportcas <packagePath> <fullTgi> <slug>");
    Console.WriteLine("      Export a CAS (Create-a-Sim) part STATIC / bind-pose (skinning ignored).");
    Console.WriteLine("      <fullTgi> is Type:Group:Instance in hex (as printed by 'search ... cas').");
    Console.WriteLine($"      Exports FBX + OBJ/MTL + PNG textures into {UnityAssetsDir}\\<slug>.");
    Console.WriteLine();
    Console.WriteLine("  exportsim <age> <gender> [<skintoneInstanceHexOrDefault>] <slug>");
    Console.WriteLine("      Assemble a FULL default human Sim (body+head+eyes) headlessly and export");
    Console.WriteLine("      a multi-part character.json (shared skeleton + per-part skinned meshes).");
    Console.WriteLine("      Skintone is optional: 'default' (or omitted) uses the default Human tone.");
    Console.WriteLine($"      Writes character.json + Textures/ into {UnityAssetsDir}\\<slug>.");
    Console.WriteLine("      Example: exportsim Adult Female default full_af");
    Console.WriteLine();
    Console.WriteLine("  exportsimhd <age> <gender> [<skintoneInstanceHexOrDefault>] <slug>");
    Console.WriteLine("      Same as exportsim but applies the ModsFromDev OVERRIDES (Better Body [Female]");
    Console.WriteLine("      body GEOMs + wild_guy HD base skin win by TGI) before assembly.");
    Console.WriteLine("      Example: exportsimhd Adult Female default full_af_hd");
    Console.WriteLine();
    Console.WriteLine("  scanmod <packagePath>");
    Console.WriteLine("      Scan a mod package: report CASP parts, GEOM meshes (verts/tris/bones/UV0/uv1/");
    Console.WriteLine("      morph flags + EA-rig subset verdict) and textures (type + decoded resolution).");
}
