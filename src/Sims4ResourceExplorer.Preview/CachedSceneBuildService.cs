using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.Preview;

/// <summary>
/// Decorator that caches successful scene build results in a bounded LRU. Failed builds
/// (Success=false) are NOT cached so the user can retry without manually clearing.
/// Progress callbacks are invoked only on cache miss; cache hits report a synthetic
/// "Restored from scene cache" progress event so the UI shows feedback.
/// </summary>
public sealed class CachedSceneBuildService : ISceneBuildService
{
    private const int Capacity = 8;
    private readonly ISceneBuildService inner;
    private readonly LinkedList<CacheEntry> lruList = new();
    private readonly Dictionary<string, LinkedListNode<CacheEntry>> lruIndex = new(StringComparer.Ordinal);
    private readonly object syncRoot = new();

    public CachedSceneBuildService(ISceneBuildService inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        this.inner = inner;
    }

    // Build 0271: include the AsyncLocal Sim-age hint in the cache key. Without this,
    // the same CASPart reused across ages (e.g. cat ears `acEarsUp` is shared by both
    // adult and child cats) would return the FIRST-built scene's rig regardless of the
    // CURRENT sim's age. Symptom: opening an Adult cat first cached the ears-with-acRig
    // scene; opening a Child cat next returned the cached adult-positioned scene, so the
    // ears floated above the smaller child body. Adding age to the key forces a separate
    // cache slot per age. `AgeKeySuffix()` returns "" when no scope is set so non-Sim
    // builds (Build/Buy, generic Resource) keep the original cache hit pattern.
    private static string AgeKeySuffix() =>
        BuildBuySceneBuildService.CurrentSimAgeHint is { Length: > 0 } age ? $"|age={age}" : string.Empty;

    public Task<SceneBuildResult> BuildSceneAsync(ResourceMetadata resource, CancellationToken cancellationToken, IProgress<PreviewBuildProgress>? progress = null) =>
        BuildOrGetCachedAsync($"resource:{resource.Key.FullTgi}{AgeKeySuffix()}", () => inner.BuildSceneAsync(resource, cancellationToken, progress), progress);

    public Task<SceneBuildResult> BuildSceneAsync(BuildBuyAssetGraph buildBuyGraph, CancellationToken cancellationToken, IProgress<PreviewBuildProgress>? progress = null) =>
        BuildOrGetCachedAsync($"buildbuy:{buildBuyGraph.ModelResource.Key.FullTgi}{AgeKeySuffix()}", () => inner.BuildSceneAsync(buildBuyGraph, cancellationToken, progress), progress);

    public Task<SceneBuildResult> BuildSceneAsync(CasAssetGraph casGraph, CancellationToken cancellationToken, IProgress<PreviewBuildProgress>? progress = null) =>
        BuildOrGetCachedAsync($"cas:{casGraph.CasPartResource.Key.FullTgi}{AgeKeySuffix()}", () => inner.BuildSceneAsync(casGraph, cancellationToken, progress), progress);

    /// <summary>Removes all cached scenes. Call after the index is rebuilt or a package is added/removed.</summary>
    public void Clear()
    {
        lock (syncRoot)
        {
            lruList.Clear();
            lruIndex.Clear();
        }
    }

    private async Task<SceneBuildResult> BuildOrGetCachedAsync(string cacheKey, Func<Task<SceneBuildResult>> buildFactory, IProgress<PreviewBuildProgress>? progress)
    {
        if (TryGetCached(cacheKey, out var cached))
        {
            progress?.Report(new PreviewBuildProgress("Restored from scene cache", 1.0));
            return cached;
        }

        var result = await buildFactory().ConfigureAwait(false);
        if (result.Success)
        {
            Store(cacheKey, result);
        }
        return result;
    }

    private bool TryGetCached(string cacheKey, out SceneBuildResult result)
    {
        lock (syncRoot)
        {
            if (lruIndex.TryGetValue(cacheKey, out var node))
            {
                lruList.Remove(node);
                lruList.AddFirst(node);
                result = node.Value.Result;
                return true;
            }
        }
        result = default!;
        return false;
    }

    private void Store(string cacheKey, SceneBuildResult result)
    {
        lock (syncRoot)
        {
            if (lruIndex.TryGetValue(cacheKey, out var existing))
            {
                lruList.Remove(existing);
                lruIndex.Remove(cacheKey);
            }

            var node = new LinkedListNode<CacheEntry>(new CacheEntry(cacheKey, result));
            lruList.AddFirst(node);
            lruIndex[cacheKey] = node;

            while (lruList.Count > Capacity)
            {
                var evict = lruList.Last!;
                lruList.RemoveLast();
                lruIndex.Remove(evict.Value.Key);
            }
        }
    }

    private readonly record struct CacheEntry(string Key, SceneBuildResult Result);
}
