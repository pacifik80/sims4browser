using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Packages;

namespace Sims4ResourceExplorer.Assets;

/// <summary>
/// Resolves a Sim's body and face modifiers into a flat list of <see cref="Ts4SimBlendGeometryMorph"/>
/// entries — one per (SMOD → BGEO) reference. The chain mirrors <see cref="DeformerMapResolver"/>
/// but follows <see cref="Ts4SimModifierResource.BgeoKeys"/> (one or more per SMOD) instead of the
/// shape/normal DeformerMap keys.
/// </summary>
public sealed class BlendGeometryResolver
{
    private readonly IIndexStore indexStore;
    private readonly IResourceCatalogService resourceCatalogService;
    private readonly Dictionary<ulong, IReadOnlyList<Ts4SimBlendGeometryMorph>> resolutionCache = new();
    private readonly object cacheLock = new();

    /// <summary>
    /// Defensive sanity ceiling on a modifier weight. Mirrors the guards in BondMorphResolver
    /// and DeformerMapResolver.
    /// </summary>
    private const float MaxAbsModifierWeight = 2f;

    public BlendGeometryResolver(IIndexStore indexStore, IResourceCatalogService resourceCatalogService)
    {
        this.indexStore = indexStore;
        this.resourceCatalogService = resourceCatalogService;
    }

    /// <summary>
    /// Load the SimInfo at <paramref name="simInfoResource"/>, parse it, then expand its
    /// modifier list into BGEO morph entries. Returns an empty list when the SimInfo can't be
    /// loaded, no modifiers reference BGEOs, or every weight is zero.
    /// </summary>
    public async Task<IReadOnlyList<Ts4SimBlendGeometryMorph>> ResolveAsync(
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

    private IReadOnlyList<Ts4SimBlendGeometryMorph> Memoize(ulong key, IReadOnlyList<Ts4SimBlendGeometryMorph> value)
    {
        lock (cacheLock) resolutionCache[key] = value;
        return value;
    }

    internal async Task<IReadOnlyList<Ts4SimBlendGeometryMorph>> ResolveAsync(
        Ts4SimInfo simInfo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(simInfo);
        var morphs = new List<Ts4SimBlendGeometryMorph>();
        await AppendAsync(simInfo.BodyModifiers, morphs, cancellationToken).ConfigureAwait(false);
        await AppendAsync(simInfo.FaceModifiers, morphs, cancellationToken).ConfigureAwait(false);
        return morphs;
    }

    private async Task AppendAsync(
        IReadOnlyList<Ts4SimModifierEntry> modifiers,
        List<Ts4SimBlendGeometryMorph> sink,
        CancellationToken cancellationToken)
    {
        // Cache parsed BGEOs by (type, instance) so multiple modifiers that share a BGEO
        // don't re-parse + re-walk the blend map ladder.
        var bgeoCache = new Dictionary<(uint Type, ulong Instance), Ts4BlendGeometryResource?>();

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

            foreach (var bgeoKey in smod.BgeoKeys)
            {
                if (bgeoKey.Instance == 0) continue;
                var bgeo = await ResolveBgeoAsync(bgeoKey, bgeoCache, cancellationToken).ConfigureAwait(false);
                if (bgeo is null) continue;
                sink.Add(new Ts4SimBlendGeometryMorph(bgeo, modifier.Value, smod.Region));
            }
        }
    }

    private async Task<Ts4BlendGeometryResource?> ResolveBgeoAsync(
        Ts4BondResourceKey key,
        Dictionary<(uint Type, ulong Instance), Ts4BlendGeometryResource?> cache,
        CancellationToken cancellationToken)
    {
        var cacheKey = (key.Type, key.Instance);
        if (cache.TryGetValue(cacheKey, out var cached)) return cached;

        var bgeoResources = await indexStore.GetResourcesByFullInstanceAsync(key.Instance, cancellationToken).ConfigureAwait(false);
        var bgeoResource = bgeoResources.FirstOrDefault(r => r.Key.Type == key.Type);
        if (bgeoResource is null)
        {
            cache[cacheKey] = null;
            return null;
        }

        byte[] bgeoBytes;
        try
        {
            bgeoBytes = await resourceCatalogService.GetResourceBytesAsync(
                bgeoResource.PackagePath, bgeoResource.Key, raw: false, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            cache[cacheKey] = null;
            return null;
        }

        Ts4BlendGeometryResource bgeo;
        try { bgeo = Ts4BlendGeometryResource.Parse(bgeoBytes); }
        catch { cache[cacheKey] = null; return null; }

        if (!bgeo.HasData)
        {
            cache[cacheKey] = null;
            return null;
        }

        cache[cacheKey] = bgeo;
        return bgeo;
    }
}
