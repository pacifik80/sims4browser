using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Packages;

namespace Sims4ResourceExplorer.Assets;

/// <summary>
/// Resolves a Sim's body and face modifiers into a flat list of bone-translation adjustments
/// suitable for <c>BondMorpher.MorphScene</c>. Each modifier is a (linkIndex → SMOD TGI,
/// weight) pair from the SimInfo's BodyModifier or FaceModifier list. The resolver:
/// <list type="number">
///   <item>Iterates BodyModifiers and FaceModifiers</item>
///   <item>Loads each modifier's SMOD resource</item>
///   <item>Reads the SMOD's BonePoseKey (BOND TGI)</item>
///   <item>Loads the BOND, expands BoneAdjusts into <see cref="SimBoneMorphAdjustment"/> entries scaled by the modifier weight</item>
/// </list>
/// DMap-based morphs (face shape via UV1) are not handled here; they need a separate
/// per-vertex application pipeline (Plan B.4).
/// </summary>
public sealed class BondMorphResolver
{
    private readonly IIndexStore indexStore;
    private readonly IResourceCatalogService resourceCatalogService;
    // Per-session memo: walking SMODs/BONDs for a SimInfo touches dozens of index lookups,
    // so cache the resolved adjustments by SimInfo FullInstance. Cleared when the app exits;
    // never persisted to disk because morph code evolves between sessions.
    private readonly Dictionary<ulong, IReadOnlyList<SimBoneMorphAdjustment>> resolutionCache = new();
    private readonly Dictionary<ulong, IReadOnlyList<SimBondMorph>> bondMorphCache = new();
    private readonly object cacheLock = new();

    public BondMorphResolver(IIndexStore indexStore, IResourceCatalogService resourceCatalogService)
    {
        this.indexStore = indexStore;
        this.resourceCatalogService = resourceCatalogService;
    }

    /// <summary>
    /// Load the SimInfo at the given resource, parse it, then expand its BodyModifier and
    /// FaceModifier entries into bone-translation adjustments. Returns an empty list when the
    /// SimInfo can't be loaded or parsed.
    /// </summary>
    public async Task<IReadOnlyList<SimBoneMorphAdjustment>> ResolveAsync(
        ResourceMetadata simInfoResource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(simInfoResource);
        var cacheKey = simInfoResource.Key.FullInstance;
        lock (cacheLock)
        {
            if (resolutionCache.TryGetValue(cacheKey, out var cached)) return cached;
        }
        byte[] bytes;
        try
        {
            bytes = await resourceCatalogService.GetResourceBytesAsync(
                simInfoResource.PackagePath, simInfoResource.Key, raw: false, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return Memoize(cacheKey, []);
        }

        Ts4SimInfo simInfo;
        try { simInfo = Ts4SimInfoParser.Parse(bytes); }
        catch { return Memoize(cacheKey, []); }

        var resolved = await ResolveAsync(simInfo, cancellationToken).ConfigureAwait(false);
        return Memoize(cacheKey, resolved);
    }

    private IReadOnlyList<SimBoneMorphAdjustment> Memoize(ulong key, IReadOnlyList<SimBoneMorphAdjustment> value)
    {
        lock (cacheLock) resolutionCache[key] = value;
        return value;
    }

    /// <summary>
    /// Build 0289 — TRS-preserving BOND morph resolver. Returns one
    /// <see cref="SimBondMorph"/> per modifier, with the BOND's full per-bone scale,
    /// offset, and rotation adjustments preserved (the legacy
    /// <see cref="ResolveAsync(ResourceMetadata, CancellationToken)"/> drops scale +
    /// rotation by collapsing each adjustment to translation-only).
    /// </summary>
    public async Task<IReadOnlyList<SimBondMorph>> ResolveSimBondMorphsAsync(
        ResourceMetadata simInfoResource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(simInfoResource);
        var cacheKey = simInfoResource.Key.FullInstance;
        lock (cacheLock)
        {
            if (bondMorphCache.TryGetValue(cacheKey, out var cached)) return cached;
        }
        byte[] bytes;
        try
        {
            bytes = await resourceCatalogService.GetResourceBytesAsync(
                simInfoResource.PackagePath, simInfoResource.Key, raw: false, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return MemoizeBondMorphs(cacheKey, []);
        }

        Ts4SimInfo simInfo;
        try { simInfo = Ts4SimInfoParser.Parse(bytes); }
        catch { return MemoizeBondMorphs(cacheKey, []); }

        var morphs = new List<SimBondMorph>();
        await AppendSimBondMorphsAsync(simInfo.BodyModifiers, morphs, cancellationToken).ConfigureAwait(false);
        await AppendSimBondMorphsAsync(simInfo.FaceModifiers, morphs, cancellationToken).ConfigureAwait(false);
        return MemoizeBondMorphs(cacheKey, morphs);
    }

    private IReadOnlyList<SimBondMorph> MemoizeBondMorphs(ulong key, IReadOnlyList<SimBondMorph> value)
    {
        lock (cacheLock) bondMorphCache[key] = value;
        return value;
    }

    private async Task AppendSimBondMorphsAsync(
        IReadOnlyList<Ts4SimModifierEntry> modifiers,
        List<SimBondMorph> sink,
        CancellationToken cancellationToken)
    {
        foreach (var modifier in modifiers)
        {
            if (modifier.ModifierKey is not { } smodKey) continue;
            if (Math.Abs(modifier.Value) < 1e-6f) continue;
            if (!float.IsFinite(modifier.Value) || Math.Abs(modifier.Value) > MaxAbsModifierWeight) continue;

            var smodResources = await indexStore.GetResourcesByFullInstanceAsync(smodKey.FullInstance, cancellationToken).ConfigureAwait(false);
            var smodResource = smodResources.FirstOrDefault(r => r.Key.Type == smodKey.Type);
            if (smodResource is null) continue;

            byte[] smodBytes;
            try
            {
                smodBytes = await resourceCatalogService.GetResourceBytesAsync(
                    smodResource.PackagePath, smodResource.Key, raw: false, cancellationToken).ConfigureAwait(false);
            }
            catch { continue; }

            Ts4SimModifierResource smod;
            try { smod = Ts4SimModifierResource.Parse(smodBytes); }
            catch { continue; }
            if (!smod.HasBondReference) continue;

            var bondResources = await indexStore.GetResourcesByFullInstanceAsync(smod.BonePoseKey.Instance, cancellationToken).ConfigureAwait(false);
            var bondResource = bondResources.FirstOrDefault(r => r.Key.Type == smod.BonePoseKey.Type);
            if (bondResource is null) continue;

            byte[] bondBytes;
            try
            {
                bondBytes = await resourceCatalogService.GetResourceBytesAsync(
                    bondResource.PackagePath, bondResource.Key, raw: false, cancellationToken).ConfigureAwait(false);
            }
            catch { continue; }

            Ts4BondResource bond;
            try { bond = Ts4BondResource.Parse(bondBytes); }
            catch { continue; }

            // Preserve scale + offset + rotation for each per-bone adjustment.
            var adjustments = new List<SimBondAdjustment>(bond.Adjustments.Count);
            foreach (var adj in bond.Adjustments)
            {
                adjustments.Add(new SimBondAdjustment(
                    BoneHash: adj.SlotHash,
                    LocalOffset: new System.Numerics.Vector3(adj.OffsetX, adj.OffsetY, adj.OffsetZ),
                    LocalScale: new System.Numerics.Vector3(adj.ScaleX, adj.ScaleY, adj.ScaleZ),
                    LocalRotation: new System.Numerics.Quaternion(adj.QuatX, adj.QuatY, adj.QuatZ, adj.QuatW)));
            }
            sink.Add(new SimBondMorph(adjustments, modifier.Value));
        }
    }

    internal async Task<IReadOnlyList<SimBoneMorphAdjustment>> ResolveAsync(
        Ts4SimInfo simInfo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(simInfo);
        var adjustments = new List<SimBoneMorphAdjustment>();
        await AppendAsync(simInfo.BodyModifiers, adjustments, cancellationToken).ConfigureAwait(false);
        await AppendAsync(simInfo.FaceModifiers, adjustments, cancellationToken).ConfigureAwait(false);
        return adjustments;
    }

    /// <summary>
    /// Defensive sanity ceiling on a modifier weight. Genetics blend coefficients are normally
    /// within [-1, 1]; the SimInfo parser was fixed in build 0247 to read these as big-endian
    /// floats (EA's shipped data uses BE in this one field — see <c>SimInfoServices.cs</c>
    /// face/body modifier loops). The ±2 ceiling stays as a guard rail in case a corrupted
    /// SimInfo or unsupported version slips through.
    /// </summary>
    private const float MaxAbsModifierWeight = 2f;

    private async Task AppendAsync(
        IReadOnlyList<Ts4SimModifierEntry> modifiers,
        List<SimBoneMorphAdjustment> sink,
        CancellationToken cancellationToken)
    {
        foreach (var modifier in modifiers)
        {
            if (modifier.ModifierKey is not { } smodKey) continue;
            if (Math.Abs(modifier.Value) < 1e-6f) continue;
            if (!float.IsFinite(modifier.Value) || Math.Abs(modifier.Value) > MaxAbsModifierWeight) continue;
            // Locate the SMOD resource. SMODs ship in fixed packages; the IIndexStore knows
            // which package contains the instance.
            var smodResources = await indexStore.GetResourcesByFullInstanceAsync(smodKey.FullInstance, cancellationToken).ConfigureAwait(false);
            var smodResource = smodResources.FirstOrDefault(r => r.Key.Type == smodKey.Type);
            if (smodResource is null) continue;

            byte[] smodBytes;
            try
            {
                smodBytes = await resourceCatalogService.GetResourceBytesAsync(
                    smodResource.PackagePath, smodResource.Key, raw: false, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                continue;
            }

            Ts4SimModifierResource smod;
            try { smod = Ts4SimModifierResource.Parse(smodBytes); }
            catch { continue; }
            if (!smod.HasBondReference) continue;

            // Resolve the BOND.
            var bondResources = await indexStore.GetResourcesByFullInstanceAsync(smod.BonePoseKey.Instance, cancellationToken).ConfigureAwait(false);
            var bondResource = bondResources.FirstOrDefault(r => r.Key.Type == smod.BonePoseKey.Type);
            if (bondResource is null) continue;

            byte[] bondBytes;
            try
            {
                bondBytes = await resourceCatalogService.GetResourceBytesAsync(
                    bondResource.PackagePath, bondResource.Key, raw: false, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                continue;
            }

            Ts4BondResource bond;
            try { bond = Ts4BondResource.Parse(bondBytes); }
            catch { continue; }

            foreach (var adj in bond.Adjustments)
            {
                sink.Add(new SimBoneMorphAdjustment(
                    adj.SlotHash,
                    adj.OffsetX, adj.OffsetY, adj.OffsetZ,
                    Weight: modifier.Value));
            }
        }
    }
}
