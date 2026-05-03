using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Packages;

namespace Sims4ResourceExplorer.Assets;

/// <summary>
/// Resolves a Sim's body and face modifiers into a flat list of <see cref="Ts4SimDeformerMorph"/>
/// entries suitable for a per-vertex DMap morpher. The chain mirrors <see cref="BondMorphResolver"/>:
/// SimInfo → SMOD → DMap shape/normal keys → parsed sampler.
/// </summary>
public sealed class DeformerMapResolver
{
    private readonly IIndexStore indexStore;
    private readonly IResourceCatalogService resourceCatalogService;
    private readonly Dictionary<ulong, IReadOnlyList<Ts4SimDeformerMorph>> resolutionCache = new();
    private readonly object cacheLock = new();

    /// <summary>
    /// Defensive sanity ceiling on a modifier weight, mirroring <c>BondMorphResolver</c>. If the
    /// SimInfo parser ever regresses and emits weights outside [-2, 2], we drop those entries
    /// rather than blasting geometry off-screen.
    /// </summary>
    private const float MaxAbsModifierWeight = 2f;

    public DeformerMapResolver(IIndexStore indexStore, IResourceCatalogService resourceCatalogService)
    {
        this.indexStore = indexStore;
        this.resourceCatalogService = resourceCatalogService;
    }

    /// <summary>
    /// Load the SimInfo at <paramref name="simInfoResource"/>, parse it, then expand its
    /// modifier list into deformer-map morph entries. Returns an empty list when the SimInfo
    /// can't be loaded, no modifiers reference DMaps, or all weights are zero.
    /// </summary>
    public async Task<IReadOnlyList<Ts4SimDeformerMorph>> ResolveAsync(
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

    private IReadOnlyList<Ts4SimDeformerMorph> Memoize(ulong key, IReadOnlyList<Ts4SimDeformerMorph> value)
    {
        lock (cacheLock) resolutionCache[key] = value;
        return value;
    }

    internal async Task<IReadOnlyList<Ts4SimDeformerMorph>> ResolveAsync(
        Ts4SimInfo simInfo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(simInfo);
        var morphs = new List<Ts4SimDeformerMorph>();
        await AppendAsync(simInfo.BodyModifiers, morphs, cancellationToken).ConfigureAwait(false);
        await AppendAsync(simInfo.FaceModifiers, morphs, cancellationToken).ConfigureAwait(false);
        return morphs;
    }

    private async Task AppendAsync(
        IReadOnlyList<Ts4SimModifierEntry> modifiers,
        List<Ts4SimDeformerMorph> sink,
        CancellationToken cancellationToken)
    {
        // Cache parsed DMaps by (type, instance) so the same DMap referenced by two modifiers
        // is only loaded + decompressed once per resolution.
        var dmapCache = new Dictionary<(uint Type, ulong Instance), Ts4DeformerMapSampler?>();

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
            catch
            {
                continue;
            }

            Ts4SimModifierResource smod;
            try { smod = Ts4SimModifierResource.Parse(smodBytes); }
            catch { continue; }

            if (smod.HasShapeDeformerMap)
            {
                var sampler = await ResolveDeformerMapAsync(smod.DeformerMapShapeKey, dmapCache, cancellationToken).ConfigureAwait(false);
                if (sampler is not null)
                {
                    sink.Add(new Ts4SimDeformerMorph(sampler, modifier.Value, IsNormalMap: false, smod.Region));
                }
            }
            if (smod.HasNormalDeformerMap)
            {
                var sampler = await ResolveDeformerMapAsync(smod.DeformerMapNormalKey, dmapCache, cancellationToken).ConfigureAwait(false);
                if (sampler is not null)
                {
                    sink.Add(new Ts4SimDeformerMorph(sampler, modifier.Value, IsNormalMap: true, smod.Region));
                }
            }
        }
    }

    private async Task<Ts4DeformerMapSampler?> ResolveDeformerMapAsync(
        Ts4BondResourceKey key,
        Dictionary<(uint Type, ulong Instance), Ts4DeformerMapSampler?> cache,
        CancellationToken cancellationToken)
    {
        var cacheKey = (key.Type, key.Instance);
        if (cache.TryGetValue(cacheKey, out var cached)) return cached;

        var dmapResources = await indexStore.GetResourcesByFullInstanceAsync(key.Instance, cancellationToken).ConfigureAwait(false);
        var dmapResource = dmapResources.FirstOrDefault(r => r.Key.Type == key.Type);
        if (dmapResource is null)
        {
            cache[cacheKey] = null;
            return null;
        }

        byte[] dmapBytes;
        try
        {
            dmapBytes = await resourceCatalogService.GetResourceBytesAsync(
                dmapResource.PackagePath, dmapResource.Key, raw: false, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            cache[cacheKey] = null;
            return null;
        }

        Ts4DeformerMapResource dmap;
        try { dmap = Ts4DeformerMapResource.Parse(dmapBytes); }
        catch { cache[cacheKey] = null; return null; }

        if (!dmap.HasData)
        {
            cache[cacheKey] = null;
            return null;
        }

        Ts4DeformerMapSampler sampler;
        try { sampler = new Ts4DeformerMapSampler(dmap); }
        catch { cache[cacheKey] = null; return null; }

        cache[cacheKey] = sampler;
        return sampler;
    }
}
