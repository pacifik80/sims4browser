// ModOverride — a MOD-OVERRIDE layer for the headless exporter.
//
// PROBLEM
// -------
// exportsim assembles the default Adult-Female Sim from the indexed GAME packages. We want a set
// of MOD packages (ModsFromDev) to OVERRIDE the game by TGI (mod wins; same "Delta > Full" override
// spirit) so the body GEOMs and the skintone BASE texture come from the mods instead of EA.
//
// CHOKE POINT
// -----------
// Every GEOM/material/texture in the Sim assembly is fetched through the SINGLE low-level
// IResourceCatalogService:
//   - GEOM + material bytes  -> GetResourceBytesAsync(packagePath, key, ...)
//   - skintone base texture  -> GetTexturePngAsync(packagePath, key, ...)
// (Verified: BuildBuySceneBuildService(.Cas).cs and AssetServices.cs route ALL Sim-asset reads
// through those two methods, with the resource's TGI carried in `key`.) The higher services
// (SyntheticSimService / SimAssetGraphRenderer) first RESOLVE which resources exist (via the index,
// which points at EA packages), then fetch their bytes by (packagePath, key). So a decorator over
// IResourceCatalogService that intercepts by KEY (TGI) — regardless of the packagePath the caller
// passes — is the exact place to make the mod win.
//
// THREE OVERRIDE FORMS (discovered by scanning the four mod packages):
//   1. INSTANCE-REUSED (clean mod-wins-by-TGI). Better Body [Female] Bottom + FeetDefault reuse
//      EA's exact GEOM full TGI (instance 89D45E57C8A23CC6 / CA169679E2CD5DF1, same Geometry type
//      015A1849 and same per-LOD groups). The EA recipe requests exactly those TGIs, so we simply
//      serve the bytes from the mod package. Likewise any wild_guy LRLE whose instance equals the
//      EA skintone-base instance the tone resolves to.
//   2. INSTANCE-DIFFERENT GEOM REDIRECT (the Top). Better Body [Female] Top is a default-replacement
//      CASP (bodyType 6) whose GEOM lives at a NEW instance FDC4902930068ACF (groups 807B99F8..FB),
//      NOT EA's default-female top instance B6DCAB99F33C43EE. The synthetic recipe never asks for the
//      mod's instance, so we REDIRECT: any Geometry read whose instance == EA top instance is served
//      from the Better Body top LOD0 GEOM (015A1849:807B99F8:FDC4902930068ACF, 1963 verts).
//   3. SKINTONE-BASE REDIRECT (the wild_guy skin). [DISABLED 2026-06-19 — see below.]
//      Originally this REDIRECTED the resolved EA base-texture TGI (2BC04EDF:…:3275143DEC141D18)
//      to a wild_guy LRLE (53F13B3669333A6A) to give the Sim an "HD base skin". That was WRONG:
//      a dump+classify of all 28 wild_guy LRLEImages (the `dumpwildguy` command) proved EVERY one
//      is GRAYSCALE / NEAR-WHITE (sat≈0, R≈G≈B, value ~168–193) — they are per-physique grayscale
//      DETAIL/relief maps, NOT color bases. 53F13B36… is the BRIGHTEST of them (mean 193), so it
//      drowned the skin to white/gray. wild_guy contains NO usable warm color base, so the base
//      override is REVERTED: the skin keeps EA's warm tone base (3275143DEC141D18) and the Better
//      Body mesh overrides are retained. wild_guy is a DETAIL OVERLAY (to be multiplied over the
//      colored EA base in a future detail-compositing step), not a base.
//
// All other reads fall straight through to the inner (real) catalog — EA behaviour is unchanged
// except for the TGIs the override set defines/redirects.

using Sims4ResourceExplorer.Core;

namespace Sims4UnityExport;

/// <summary>
/// Process-wide arming point for the mod-override layer. The DI factory for IResourceCatalogService
/// consults <see cref="Current"/>: when armed (the HD export path), every consumer transparently
/// gets the decorator; when null, the real catalog is used. Armed BEFORE any service that needs the
/// catalog is resolved, so the singleton materialises wrapped.
/// </summary>
internal static class ModOverrideHolder
{
    public static ModOverrideResourceCatalogService? Current { get; set; }
}

/// <summary>
/// One mod-override entry: a resource that lives in <see cref="ModPackagePath"/> under
/// <see cref="ModKey"/>, which should be served whenever a caller requests the resource keyed by
/// <see cref="RequestType"/> + <see cref="RequestInstance"/> (group is intentionally ignored on the
/// request side because EA LOD groups differ between EA and the mod for the Top redirect; for
/// instance-reused overrides the mod key equals the request key, so it is a no-op match).
/// </summary>
internal sealed record ModOverrideEntry(
    uint RequestType,
    ulong RequestInstance,
    string ModPackagePath,
    ResourceKeyRecord ModKey,
    string Reason);

/// <summary>
/// Decorator over <see cref="IResourceCatalogService"/> that makes a set of MOD packages win by TGI.
/// Built once (BuildAsync), then injected in place of the real catalog so EVERY Sim-asset byte/texture
/// fetch passes through it. The override decision is made on the requested KEY, not the packagePath,
/// so it catches both the index-resolved EA reads and the renderer's direct-probe reads.
/// </summary>
internal sealed class ModOverrideResourceCatalogService : IResourceCatalogService
{
    private readonly IResourceCatalogService _inner;

    // Form (1): exact-TGI hits — instance-reused overrides + texture instance hits. Keyed by
    // "TYPE:INSTANCE" (group-insensitive, because a default-replacement reuses type+instance and the
    // group is part of the same logical resource family). Value = the mod package that defines it +
    // the exact mod key to read with.
    private readonly Dictionary<string, ModOverrideEntry> _byTypeInstance;

    // Form (2)+(3): explicit REDIRECTS keyed by the EA request "TYPE:INSTANCE" -> the mod resource to
    // serve instead (with a possibly DIFFERENT type/group/instance on the mod side).
    private readonly Dictionary<string, ModOverrideEntry> _redirects;

    private readonly List<string> _log;

    private ModOverrideResourceCatalogService(
        IResourceCatalogService inner,
        Dictionary<string, ModOverrideEntry> byTypeInstance,
        Dictionary<string, ModOverrideEntry> redirects,
        List<string> log)
    {
        _inner = inner;
        _byTypeInstance = byTypeInstance;
        _redirects = redirects;
        _log = log;
    }

    /// <summary>Override-resolution diagnostics + the live "served from mod" hit log.</summary>
    public IReadOnlyList<string> Log => _log;

    private static string TypeInstanceKey(uint type, ulong instance) =>
        $"{type:X8}:{instance:X16}";

    // ---------------------------------------------------------------------------
    // Build the override layer for the Adult-Female "HD" set.
    // ---------------------------------------------------------------------------
    public static async Task<ModOverrideResourceCatalogService> BuildAdultFemaleHdAsync(
        IResourceCatalogService inner,
        IIndexStore indexStore,
        CancellationToken ct)
    {
        const uint GeometryType = 0x015A1849u;

        // EA default-female body part instances (mesh names Mesh_<instance> from the EA baseline run).
        const ulong EaTopInstance = 0xB6DCAB99F33C43EEul;     // EA default-female TOP body GEOM (1884 v)

        // Better Body top GEOM (NEW instance) — LOD0 high-poly (1963 v).
        const uint BbTopType = 0x015A1849u;
        const uint BbTopLod0Group = 0x807B99F8u;
        const ulong BbTopInstance = 0xFDC4902930068ACFul;

        // The skintone-base texture instance the default Adult-Female tone (0x5545) resolves to in
        // this install. The skin BASE override is now REVERTED to EA's warm tone base (this very
        // instance), because all 28 wild_guy LRLEs proved grayscale (no warm color base exists in
        // wild_guy — see the Form (3) note above). SkinBaseType/EaDefaultFemaleSkinBaseInstance are
        // kept only for the diagnostic log line that records the revert.
        const uint SkinBaseType = 0x2BC04EDFu;                  // LRLEImage
        const ulong EaDefaultFemaleSkinBaseInstance = 0x3275143DEC141D18ul;

        var log = new List<string>();
        var byTypeInstance = new Dictionary<string, ModOverrideEntry>(StringComparer.OrdinalIgnoreCase);
        var redirects = new Dictionary<string, ModOverrideEntry>(StringComparer.OrdinalIgnoreCase);

        var modRoot = @"c:\Users\stani\PROJECTS\#GAMES\Sims4Browser\ModsFromDev";
        var betterBodyDir = Path.Combine(modRoot, "!!!! Better Body");
        var wildGuyDir = Path.Combine(modRoot, "WIldGuy", "FBD");

        // The override packages, in priority order (later entries do NOT clobber earlier ones for the
        // same TYPE:INSTANCE — first writer wins, mirroring a deterministic load order).
        var bottomPkg = Path.Combine(betterBodyDir, "[BB][Female]Bottom.package");
        var feetPkg = Path.Combine(betterBodyDir, "[BB][Female]FeetDefault.package");
        var topPkg = Path.Combine(betterBodyDir, "[BB][Female]Top.package");
        var skinPkg = Path.Combine(wildGuyDir, "wild_guy FemaleDefaultNudeSkin.package");

        // ---- Form (1): index every resource the mod packages define, keyed by TYPE:INSTANCE. ----
        // Only the GEOM (Geometry) and texture (LRLE/RLE2/DST/PNG) families participate in the Sim
        // assembly's byte/texture fetch; we index them all so any reused-instance resource wins.
        async Task IndexModPackageAsync(string packagePath, string label)
        {
            if (!File.Exists(packagePath))
            {
                log.Add($"[modoverride] MISSING mod package ({label}): {packagePath}");
                return;
            }
            var source = new DataSourceDefinition(Guid.NewGuid(), Path.GetFileName(packagePath), packagePath, SourceKind.Mods);
            PackageScanResult scan;
            try
            {
                scan = await inner.ScanPackageAsync(source, packagePath, progress: null, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.Add($"[modoverride] FAILED to scan {label} ({ex.GetType().Name}: {ex.Message}).");
                return;
            }

            var added = 0;
            foreach (var resource in scan.Resources)
            {
                if (!IsOverridableType(resource.Key.TypeName))
                {
                    continue;
                }
                var k = TypeInstanceKey(resource.Key.Type, resource.Key.FullInstance);
                if (byTypeInstance.ContainsKey(k))
                {
                    continue; // first writer (higher priority) wins
                }
                byTypeInstance[k] = new ModOverrideEntry(
                    resource.Key.Type, resource.Key.FullInstance, packagePath, resource.Key,
                    $"{label} reuses TGI {resource.Key.FullTgi}");
                added++;
            }
            log.Add($"[modoverride] indexed {label}: {scan.Resources.Count} resource(s), {added} override TGI(s) registered.");
        }

        // Priority: Bottom + Feet (instance-reused GEOMs) first, then Top (for its own instance,
        // though it is reached via the redirect below).
        // NOTE: the wild_guy skin package is INTENTIONALLY NOT indexed in Form (1). Its 28 LRLEs are
        // all grayscale DETAIL maps; indexing them would let a grayscale LRLE clobber an EA skin
        // texture by accidental instance reuse and white-out the skin again. The base stays EA's.
        await IndexModPackageAsync(bottomPkg, "BetterBody[Female]Bottom").ConfigureAwait(false);
        await IndexModPackageAsync(feetPkg, "BetterBody[Female]FeetDefault").ConfigureAwait(false);
        await IndexModPackageAsync(topPkg, "BetterBody[Female]Top").ConfigureAwait(false);

        // ---- Form (2): the Top GEOM REDIRECT (EA top instance -> Better Body top LOD0). ----
        if (File.Exists(topPkg))
        {
            var bbTopKey = new ResourceKeyRecord(BbTopType, BbTopLod0Group, BbTopInstance, "Geometry");
            redirects[TypeInstanceKey(GeometryType, EaTopInstance)] = new ModOverrideEntry(
                GeometryType, EaTopInstance, topPkg, bbTopKey,
                $"Better Body top GEOM substituted for EA default-female top (EA {GeometryType:X8}:*:{EaTopInstance:X16} -> mod {bbTopKey.FullTgi}, 1963 v)");
            log.Add($"[modoverride] Top REDIRECT registered: EA {GeometryType:X8}:*:{EaTopInstance:X16} -> {bbTopKey.FullTgi}.");
        }
        else
        {
            log.Add($"[modoverride] Top package MISSING; Top will NOT be overridden (EA top {EaTopInstance:X16} kept): {topPkg}");
        }

        // ---- Form (3): DISABLED — the wild_guy skin-base REDIRECT is REVERTED. ----
        // All 28 wild_guy LRLEs are grayscale (no warm color base), so we DO NOT redirect the EA
        // base. The skin keeps EA's warm tone base (SkinBaseType:00000000:EaDefaultFemaleSkinBaseInstance),
        // which falls straight through to the inner catalog. wild_guy is a DETAIL overlay for a
        // future multiply-over-base step, not a base. (skinPkg presence is logged for provenance.)
        if (File.Exists(skinPkg))
        {
            log.Add($"[modoverride] Skin base REVERTED to EA warm tone base {SkinBaseType:X8}:00000000:{EaDefaultFemaleSkinBaseInstance:X16} (wild_guy LRLEs are all grayscale DETAIL maps; NO redirect registered).");
        }
        else
        {
            log.Add($"[modoverride] Skin base kept at EA warm tone base (wild_guy package not present anyway): {skinPkg}");
        }

        _ = indexStore; // reserved (kept in the signature for future index-driven LOD discovery)
        log.Add($"[modoverride] READY: {byTypeInstance.Count} instance-reuse TGI(s), {redirects.Count} redirect(s).");
        return new ModOverrideResourceCatalogService(inner, byTypeInstance, redirects, log);
    }

    // ---------------------------------------------------------------------------
    // DATA-DRIVEN builder: arm the override layer for a catalog BodyMeshOption.
    //
    // Generalizes BuildAdultFemaleHdAsync so exportchar can apply WHATEVER bodyMesh the
    // CharacterDefinition picks. The option's `overrides` carry the mod GEOM TGIs (top/bottom/feet)
    // plus the EA default-female part instances they REDIRECT from. Form (1) (instance-reused GEOMs:
    // bottom + feet) is realized by indexing the mod packages; Form (2) (the Top, which lives at a
    // NEW instance) is realized by an explicit redirect EA-top-instance -> mod-top-LOD0. The skin
    // BASE is left at EA's warm tone (Form (3) stays reverted — wild_guy LRLEs are grayscale detail).
    //
    // Returns null when the option carries no overrides (EA default — caller uses the real catalog).
    // ---------------------------------------------------------------------------
    public static async Task<ModOverrideResourceCatalogService?> BuildFromBodyMeshAsync(
        IResourceCatalogService inner,
        IIndexStore indexStore,
        BodyMeshOption option,
        string modsRoot,
        CancellationToken ct)
    {
        var ov = option.Overrides;
        if (ov is null ||
            (string.IsNullOrWhiteSpace(ov.Top) && string.IsNullOrWhiteSpace(ov.Bottom) && string.IsNullOrWhiteSpace(ov.Feet) &&
             string.IsNullOrWhiteSpace(ov.TopPackage) && string.IsNullOrWhiteSpace(ov.BottomPackage) && string.IsNullOrWhiteSpace(ov.FeetPackage)))
        {
            return null; // EA default — no override. (A package alone is enough: Form-1 reuse mods
                         // like Helio/DallasGirl/Necros set only <region>Package, not a redirect TGI.)
        }

        var log = new List<string>();
        var byTypeInstance = new Dictionary<string, ModOverrideEntry>(StringComparer.OrdinalIgnoreCase);
        var redirects = new Dictionary<string, ModOverrideEntry>(StringComparer.OrdinalIgnoreCase);

        string Resolve(string? rel) =>
            string.IsNullOrWhiteSpace(rel) ? string.Empty : Path.Combine(modsRoot, rel);

        // ---- Form (1): index the bottom + feet mod packages (instance-reused GEOMs). ----
        async Task IndexModPackageAsync(string packagePath, string label)
        {
            if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
            {
                log.Add($"[modoverride] MISSING mod package ({label}): {packagePath}");
                return;
            }
            var source = new DataSourceDefinition(Guid.NewGuid(), Path.GetFileName(packagePath), packagePath, SourceKind.Mods);
            PackageScanResult scan;
            try { scan = await inner.ScanPackageAsync(source, packagePath, progress: null, ct).ConfigureAwait(false); }
            catch (Exception ex) { log.Add($"[modoverride] FAILED to scan {label} ({ex.GetType().Name}: {ex.Message})."); return; }

            var added = 0;
            foreach (var resource in scan.Resources)
            {
                if (!IsOverridableType(resource.Key.TypeName)) continue;
                var k = TypeInstanceKey(resource.Key.Type, resource.Key.FullInstance);
                if (byTypeInstance.ContainsKey(k)) continue;
                byTypeInstance[k] = new ModOverrideEntry(
                    resource.Key.Type, resource.Key.FullInstance, packagePath, resource.Key,
                    $"{label} reuses TGI {resource.Key.FullTgi}");
                added++;
            }
            log.Add($"[modoverride] indexed {label}: {scan.Resources.Count} resource(s), {added} override TGI(s) registered.");
        }

        var bottomPkg = Resolve(ov.BottomPackage);
        var feetPkg = Resolve(ov.FeetPackage);
        var topPkg = Resolve(ov.TopPackage);

        await IndexModPackageAsync(bottomPkg, $"{option.Id}/Bottom").ConfigureAwait(false);
        await IndexModPackageAsync(feetPkg, $"{option.Id}/FeetDefault").ConfigureAwait(false);
        await IndexModPackageAsync(topPkg, $"{option.Id}/Top").ConfigureAwait(false);

        // ---- Form (2): GEOM REDIRECTS (EA region instance -> mod region LOD0). ----
        // Needed when the mod ships a body region under a NEW instance hash, so Form-1
        // instance-reuse can't catch it (e.g. Better Body's top, or EVE's CAS-wrapped top AND
        // bottom). When the mod instead REUSES the EA instance (Better Body's bottom/feet), Form 1
        // already serves it and a redirect would only risk pinning a non-LOD0 mesh — so we skip it.
        void RegisterRedirect(string? modTgi, string? eaTgi, string pkg, string region)
        {
            if (!TryParseTgi(modTgi, out var t, out var g, out var inst) ||
                !TryParseTgi(eaTgi, out _, out _, out var eaInst) ||
                !File.Exists(pkg))
            {
                log.Add($"[modoverride] {region} REDIRECT skipped (mod='{modTgi}', ea='{eaTgi}', pkgExists={File.Exists(pkg)}).");
                return;
            }
            if (inst == eaInst)
            {
                // Mod reuses the EA instance — Form-1 instance-reuse already serves it.
                log.Add($"[modoverride] {region} reuses EA instance {eaInst:X16}; served via Form-1 (no redirect).");
                return;
            }
            var modKey = new ResourceKeyRecord(t, g, inst, "Geometry");
            redirects[TypeInstanceKey(t, eaInst)] = new ModOverrideEntry(
                t, eaInst, pkg, modKey,
                $"{option.Id} {region} GEOM substituted for EA default-female {region} (EA {t:X8}:*:{eaInst:X16} -> mod {modKey.FullTgi})");
            log.Add($"[modoverride] {region} REDIRECT registered: EA {t:X8}:*:{eaInst:X16} -> {modKey.FullTgi}.");
        }

        RegisterRedirect(ov.Top, ov.EaTop, topPkg, "top");
        RegisterRedirect(ov.Bottom, ov.EaBottom, bottomPkg, "bottom");
        RegisterRedirect(ov.Feet, ov.EaFeet, feetPkg, "feet");

        _ = indexStore;
        log.Add($"[modoverride] READY ({option.Id}): {byTypeInstance.Count} instance-reuse TGI(s), {redirects.Count} redirect(s).");
        return new ModOverrideResourceCatalogService(inner, byTypeInstance, redirects, log);
    }

    // ---------------------------------------------------------------------------
    // BuildFromSkinPackagesAsync — index a default-skin package's TEXTURES by TYPE:INSTANCE so a
    // skintone resolve serves the mod's color diffuse for the reused EA skintone instance. GEOM is
    // deliberately EXCLUDED so a skin override can never substitute a body mesh. Returns null when
    // nothing indexes (caller falls back to the EA skin).
    // ---------------------------------------------------------------------------
    public static async Task<ModOverrideResourceCatalogService?> BuildFromSkinPackagesAsync(
        IResourceCatalogService inner, IReadOnlyList<string> packages, CancellationToken ct)
    {
        if (packages is null || packages.Count == 0) return null;

        var log = new List<string>();
        var byTypeInstance = new Dictionary<string, ModOverrideEntry>(StringComparer.OrdinalIgnoreCase);
        var redirects = new Dictionary<string, ModOverrideEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var pkg in packages)
        {
            if (string.IsNullOrWhiteSpace(pkg) || !File.Exists(pkg))
            {
                log.Add($"[skinoverride] MISSING skin package: {pkg}");
                continue;
            }
            var source = new DataSourceDefinition(Guid.NewGuid(), Path.GetFileName(pkg), pkg, SourceKind.Mods);
            PackageScanResult scan;
            try { scan = await inner.ScanPackageAsync(source, pkg, progress: null, ct).ConfigureAwait(false); }
            catch (Exception ex) { log.Add($"[skinoverride] scan FAILED {Path.GetFileName(pkg)} ({ex.GetType().Name}: {ex.Message})."); continue; }

            var added = 0;
            foreach (var resource in scan.Resources)
            {
                // TEXTURES ONLY — never a GEOM (a skin must not change the body mesh).
                if (!IsOverridableType(resource.Key.TypeName)) continue;
                if (string.Equals(resource.Key.TypeName, "Geometry", StringComparison.OrdinalIgnoreCase)) continue;
                var k = TypeInstanceKey(resource.Key.Type, resource.Key.FullInstance);
                if (byTypeInstance.ContainsKey(k)) continue; // first writer wins
                byTypeInstance[k] = new ModOverrideEntry(
                    resource.Key.Type, resource.Key.FullInstance, pkg, resource.Key,
                    $"skin {Path.GetFileName(pkg)} reuses TGI {resource.Key.FullTgi}");
                added++;
            }
            log.Add($"[skinoverride] indexed {Path.GetFileName(pkg)}: {scan.Resources.Count} resource(s), {added} texture TGI(s).");
        }

        if (byTypeInstance.Count == 0) return null;
        return new ModOverrideResourceCatalogService(inner, byTypeInstance, redirects, log);
    }

    // Parse "Type:Group:Instance" hex into components. Returns false on malformed/empty input.
    private static bool TryParseTgi(string? tgi, out uint type, out uint group, out ulong instance)
    {
        type = 0; group = 0; instance = 0;
        if (string.IsNullOrWhiteSpace(tgi)) return false;
        var parts = tgi.Split(':');
        if (parts.Length != 3) return false;
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var ns = System.Globalization.NumberStyles.HexNumber;
        return uint.TryParse(parts[0], ns, ci, out type)
            && uint.TryParse(parts[1], ns, ci, out group)
            && ulong.TryParse(parts[2], ns, ci, out instance);
    }

    private static bool IsOverridableType(string typeName) => typeName is
        "Geometry" or
        "LRLEImage" or "RLE2Image" or "RLESImage" or "DSTImage" or
        "PNGImage" or "PNGImage2" or "DDS";

    // Resolve the override entry (redirect first, then instance-reuse) for a requested key, or null
    // when the request should fall through to EA. Logs the hit exactly once per unique TGI served.
    private ModOverrideEntry? ResolveOverride(ResourceKeyRecord key)
    {
        var tik = TypeInstanceKey(key.Type, key.FullInstance);

        if (_redirects.TryGetValue(tik, out var redirect))
        {
            LogHitOnce(key, redirect);
            return redirect;
        }
        if (_byTypeInstance.TryGetValue(tik, out var reuse))
        {
            LogHitOnce(key, reuse);
            return reuse;
        }
        return null;
    }

    private readonly HashSet<string> _hitsSeen = new(StringComparer.OrdinalIgnoreCase);

    private void LogHitOnce(ResourceKeyRecord requested, ModOverrideEntry entry)
    {
        var marker = $"{requested.FullTgi}->{entry.ModKey.FullTgi}";
        if (_hitsSeen.Add(marker))
        {
            _log.Add(
                $"[modoverride] SERVED {requested.FullTgi} ({requested.TypeName}) from " +
                $"{Path.GetFileName(entry.ModPackagePath)} as {entry.ModKey.FullTgi} — {entry.Reason}.");
        }
    }

    // ---------------------------------------------------------------------------
    // IResourceCatalogService — intercept the two fetch methods; pass everything else through.
    // ---------------------------------------------------------------------------
    public Task<byte[]> GetResourceBytesAsync(
        string packagePath, ResourceKeyRecord key, bool raw, CancellationToken cancellationToken,
        IProgress<ResourceReadProgress>? progress = null)
    {
        var entry = ResolveOverride(key);
        return entry is null
            ? _inner.GetResourceBytesAsync(packagePath, key, raw, cancellationToken, progress)
            : _inner.GetResourceBytesAsync(entry.ModPackagePath, entry.ModKey, raw, cancellationToken, progress);
    }

    public Task<byte[]?> GetTexturePngAsync(
        string packagePath, ResourceKeyRecord key, CancellationToken cancellationToken,
        IProgress<ResourceReadProgress>? progress = null)
    {
        var entry = ResolveOverride(key);
        return entry is null
            ? _inner.GetTexturePngAsync(packagePath, key, cancellationToken, progress)
            : _inner.GetTexturePngAsync(entry.ModPackagePath, entry.ModKey, cancellationToken, progress);
    }

    // Text/scan/enrich are not part of the Sim-asset byte path we override — pass straight through.
    public Task<PackageScanResult> ScanPackageAsync(
        DataSourceDefinition source, string packagePath, IProgress<PackageScanProgress>? progress, CancellationToken cancellationToken) =>
        _inner.ScanPackageAsync(source, packagePath, progress, cancellationToken);

    public Task<ResourceMetadata> EnrichResourceAsync(ResourceMetadata resource, CancellationToken cancellationToken) =>
        _inner.EnrichResourceAsync(resource, cancellationToken);

    public Task<string?> GetTextAsync(string packagePath, ResourceKeyRecord key, CancellationToken cancellationToken) =>
        _inner.GetTextAsync(packagePath, key, cancellationToken);
}
