using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Sims4ResourceExplorer.App.Services;
using Sims4ResourceExplorer.Assets;
using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Preview.SimRender;

namespace Sims4ResourceExplorer.App.ViewModels;

public sealed partial class SimConstructorViewModel : ObservableObject
{
    // Build 0290: default Human skintone instance. Confirmed present in the user's index
    // per project memory (5 copies across v6 + v12 TONEs). Used as the initial fallback
    // until the runtime skintone enumeration completes; once the user picks a swatch from
    // AvailableSkintones the value comes from SelectedSkintone instead.
    private const ulong DefaultHumanSkintoneInstance = 0x0000000000005545ul;

    private readonly ISyntheticSimService syntheticSimService;
    private readonly ISimAssetGraphRenderer simRenderer;
    // In-memory cache of built scenes keyed by (age, gender, skintone). Per the user's
    // brief: cache accumulates across selections and survives age/gender switches; new
    // picks short-circuit to the cached scene if available. No cross-session disk cache
    // (per feedback_no_cross_session_disk_cache). Cache is unbounded — the constructor
    // window's process lifetime is the eviction window.
    private readonly Dictionary<SceneCacheKey, CachedSceneEntry> sceneCache = new();

    private string selectedAge = "Adult";
    private string selectedGender = "Female";
    private SimConstructorSeed currentSeed;
    private CancellationTokenSource? rebuildCts;
    private string assetGraphStatus = "Building asset graph…";
    private string sceneStatus = "Waiting for asset graph…";
    private string bodyCandidatesSummary = string.Empty;
    private string assetGraphDiagnostics = string.Empty;
    private CanonicalScene? currentScene;
    private SceneRenderMode selectedRenderMode = SceneRenderMode.LitTexture;
    private IReadOnlyList<SkintoneOption> availableSkintones = Array.Empty<SkintoneOption>();
    private SkintoneOption? selectedSkintone;

    private readonly record struct SceneCacheKey(string Age, string Gender, ulong Skintone);

    private sealed record CachedSceneEntry(
        CanonicalScene Scene,
        string AssetGraphStatus,
        string BodyCandidatesSummary,
        string Diagnostics,
        string SceneStatus);

    public SimConstructorViewModel(ISyntheticSimService syntheticSimService, ISimAssetGraphRenderer simRenderer)
    {
        this.syntheticSimService = syntheticSimService;
        this.simRenderer = simRenderer;
        AvailableAges = syntheticSimService.AvailableHumanAges;
        AvailableGenders = syntheticSimService.AvailableHumanGenders;
        currentSeed = syntheticSimService.CreateHumanSeed(selectedAge, selectedGender, DefaultHumanSkintoneInstance);
        _ = LoadSkintonesAsync();
        TriggerRebuild();
    }

    public IReadOnlyList<string> AvailableAges { get; }
    public IReadOnlyList<string> AvailableGenders { get; }
    public IReadOnlyList<SceneRenderMode> AvailableRenderModes { get; } =
        [SceneRenderMode.LitTexture, SceneRenderMode.FlatTexture, SceneRenderMode.MaterialUv, SceneRenderMode.RawUv, SceneRenderMode.Wireframe];

    public SceneRenderMode SelectedRenderMode
    {
        get => selectedRenderMode;
        set => SetProperty(ref selectedRenderMode, value);
    }

    public IReadOnlyList<SkintoneOption> AvailableSkintones
    {
        get => availableSkintones;
        private set => SetProperty(ref availableSkintones, value);
    }

    public SkintoneOption? SelectedSkintone
    {
        get => selectedSkintone;
        set
        {
            if (SetProperty(ref selectedSkintone, value) && value is not null)
            {
                RebuildForSkintoneChange(value);
            }
        }
    }

    public string SelectedAge
    {
        get => selectedAge;
        set
        {
            if (SetProperty(ref selectedAge, value))
            {
                Rebuild();
            }
        }
    }

    public string SelectedGender
    {
        get => selectedGender;
        set
        {
            if (SetProperty(ref selectedGender, value))
            {
                Rebuild();
            }
        }
    }

    public SimConstructorSeed CurrentSeed
    {
        get => currentSeed;
        private set
        {
            if (SetProperty(ref currentSeed, value))
            {
                OnPropertyChanged(nameof(SeedDisplayName));
                OnPropertyChanged(nameof(SeedSummary));
                OnPropertyChanged(nameof(SyntheticFullInstanceHex));
                OnPropertyChanged(nameof(OutfitPartCountText));
            }
        }
    }

    public string AssetGraphStatus
    {
        get => assetGraphStatus;
        private set => SetProperty(ref assetGraphStatus, value);
    }

    public string SceneStatus
    {
        get => sceneStatus;
        private set => SetProperty(ref sceneStatus, value);
    }

    public string BodyCandidatesSummary
    {
        get => bodyCandidatesSummary;
        private set => SetProperty(ref bodyCandidatesSummary, value);
    }

    public string AssetGraphDiagnostics
    {
        get => assetGraphDiagnostics;
        private set => SetProperty(ref assetGraphDiagnostics, value);
    }

    public CanonicalScene? CurrentScene
    {
        get => currentScene;
        private set
        {
            if (SetProperty(ref currentScene, value))
            {
                OnPropertyChanged(nameof(HasScene));
            }
        }
    }

    public bool HasScene => currentScene is not null;

    public string SeedDisplayName => $"{CurrentSeed.SpeciesLabel} | {CurrentSeed.AgeLabel} | {CurrentSeed.GenderLabel}";

    public string SeedSummary => CurrentSeed.SummaryText;

    public string SyntheticFullInstanceHex => $"0x{CurrentSeed.SyntheticFullInstance:X16}";

    public string OutfitPartCountText => $"Body-driving outfit: {CurrentSeed.OutfitPartCount} part(s)";

    private void Rebuild()
    {
        var skintone = selectedSkintone?.Instance ?? DefaultHumanSkintoneInstance;
        CurrentSeed = syntheticSimService.CreateHumanSeed(selectedAge, selectedGender, skintone);
        TriggerRebuild();
    }

    /// <summary>
    /// Skintone-only change. Tries the fast path first: if any scene for the current
    /// (age, gender) is already in the cache, we can reuse its geometry + bones and only
    /// re-compose the skin atlas and rebind the skintone-routed materials. Falls back to
    /// the full rebuild when no base scene is available yet.
    /// </summary>
    private void RebuildForSkintoneChange(SkintoneOption newSkintone)
    {
        CurrentSeed = syntheticSimService.CreateHumanSeed(selectedAge, selectedGender, newSkintone.Instance);
        var key = new SceneCacheKey(selectedAge, selectedGender, newSkintone.Instance);

        if (sceneCache.TryGetValue(key, out var cached))
        {
            ApplyCachedEntry(cached);
            return;
        }

        var baseEntry = FindBaseSceneForArchetype(selectedAge, selectedGender);
        if (baseEntry is null)
        {
            // No prior build for this (age, gender) — fall back to the full path.
            TriggerRebuild();
            return;
        }

        rebuildCts?.Cancel();
        rebuildCts = new CancellationTokenSource();
        SceneStatus = "Re-composing skin atlas (fast path)…";
        _ = ApplySkintoneInPlaceAsync(baseEntry, newSkintone, key, rebuildCts.Token);
    }

    private CachedSceneEntry? FindBaseSceneForArchetype(string age, string gender)
    {
        foreach (var (k, v) in sceneCache)
        {
            if (string.Equals(k.Age, age, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(k.Gender, gender, StringComparison.OrdinalIgnoreCase))
            {
                return v;
            }
        }
        return null;
    }

    private async Task ApplySkintoneInPlaceAsync(
        CachedSceneEntry baseEntry,
        SkintoneOption newSkintone,
        SceneCacheKey key,
        CancellationToken token)
    {
        try
        {
            var skintone = await syntheticSimService
                .ResolveSkintoneRenderAsync(selectedAge, selectedGender, newSkintone.Instance, token)
                .ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (skintone is null)
            {
                SceneStatus = "Skintone resolution failed; viewport unchanged.";
                return;
            }

            var atlas = await SimSkinAtlasComposer.BuildAsync(
                skintone.BaseTexturePngBytes,
                skintone.DetailNeutralPngBytes,
                skintone.DetailOverlayPngBytes,
                skintone.FaceOverlayPngBytes,
                skintone.FaceCasOverlayPngBytes,
                pass2Opacity: skintone.OverlayOpacity / 100f,
                skintoneHue: skintone.SkintoneHue,
                skintoneSaturation: skintone.SkintoneSaturation,
                cancellationToken: token).ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            var rebound = atlas is { Length: > 0 }
                ? SimSkintoneMaterialBinder.RebindWithAtlas(baseEntry.Scene, atlas)
                : baseEntry.Scene;

            var diagnostics = atlas is { Length: > 0 }
                ? $"{baseEntry.Diagnostics}\nSkin atlas (fast path): re-composed {atlas.Length:N0} bytes for skintone 0x{newSkintone.Instance:X16}."
                : $"{baseEntry.Diagnostics}\nSkin atlas (fast path): composition failed for skintone 0x{newSkintone.Instance:X16}; materials retain the previous binding.";
            var b = rebound.Bounds;
            var statusText = System.FormattableString.Invariant(
                $"Scene ready (fast path) — meshes={rebound.Meshes.Count}, materials={rebound.Materials.Count}, height≈{b.MaxY - b.MinY:0.00}m.");

            var newEntry = new CachedSceneEntry(
                Scene: rebound,
                AssetGraphStatus: baseEntry.AssetGraphStatus,
                BodyCandidatesSummary: baseEntry.BodyCandidatesSummary,
                Diagnostics: diagnostics,
                SceneStatus: statusText);
            sceneCache[key] = newEntry;

            var currentKey = new SceneCacheKey(currentSeed.AgeLabel, currentSeed.GenderLabel, currentSeed.SkintoneInstance);
            if (currentKey.Equals(key))
            {
                AssetGraphStatus = baseEntry.AssetGraphStatus;
                BodyCandidatesSummary = baseEntry.BodyCandidatesSummary;
                AssetGraphDiagnostics = diagnostics;
                SceneStatus = $"{statusText}  [{sceneCache.Count} cached]";
                CurrentScene = rebound;
            }
        }
        catch (System.OperationCanceledException)
        {
        }
        catch (System.Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                SceneStatus = $"Skintone update error: {ex.GetType().Name}: {ex.Message}";
            }
        }
    }

    private async Task LoadSkintonesAsync()
    {
        try
        {
            var skintones = await syntheticSimService.EnumerateSkintonesAsync(CancellationToken.None).ConfigureAwait(true);
            AvailableSkintones = skintones;
            // Align SelectedSkintone with the seed's current instance so the picker reflects state
            // without triggering an extra rebuild via the SelectedSkintone setter.
            selectedSkintone = skintones.FirstOrDefault(s => s.Instance == currentSeed.SkintoneInstance);
            OnPropertyChanged(nameof(SelectedSkintone));
        }
        catch
        {
            // Picker stays empty; the constructor still works with the default skintone.
        }
    }

    private void TriggerRebuild()
    {
        var seed = currentSeed;
        var key = new SceneCacheKey(seed.AgeLabel, seed.GenderLabel, seed.SkintoneInstance);

        // Cache hit: instant apply, no build, leave any in-flight build alone so its
        // result still lands in the cache.
        if (sceneCache.TryGetValue(key, out var cached))
        {
            ApplyCachedEntry(cached);
            return;
        }

        rebuildCts?.Cancel();
        rebuildCts = new CancellationTokenSource();
        var token = rebuildCts.Token;
        AssetGraphStatus = "Building asset graph…";
        SceneStatus = currentScene is null ? "Waiting for asset graph…" : "Rebuilding scene (viewport keeps the previous one until ready)…";
        // Leave CurrentScene alone — keep the previously rendered Sim visible while the
        // new build runs. The viewport will swap in the new scene atomically on success.
        _ = RebuildSceneAsync(seed, key, token);
    }

    private void ApplyCachedEntry(CachedSceneEntry entry)
    {
        AssetGraphStatus = entry.AssetGraphStatus;
        BodyCandidatesSummary = entry.BodyCandidatesSummary;
        AssetGraphDiagnostics = entry.Diagnostics;
        SceneStatus = $"{entry.SceneStatus}  [from cache]";
        CurrentScene = entry.Scene;
    }

    private async Task RebuildSceneAsync(SimConstructorSeed seed, SceneCacheKey key, CancellationToken token)
    {
        try
        {
            var graph = await syntheticSimService.BuildHumanAssetGraphAsync(seed, token).ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (graph.SimGraph is null)
            {
                AssetGraphStatus = "Asset graph build failed — no SimGraph produced.";
                AssetGraphDiagnostics = string.Join("\n", graph.Diagnostics);
                SceneStatus = "Skipped — no asset graph.";
                return;
            }

            var sim = graph.SimGraph;
            var assetGraphStatusText = $"Asset graph ready — body assembly: {sim.BodyAssembly.Mode}, layers: {sim.BodyAssembly.Layers.Count}.";
            AssetGraphStatus = assetGraphStatusText;

            var sb = new StringBuilder();
            sb.AppendLine($"Body candidate buckets: {sim.BodyCandidates.Count}");
            foreach (var bucket in sim.BodyCandidates)
            {
                sb.AppendLine($"  • {bucket.Label}  [{bucket.SourceKind}]  count={bucket.Count}");
                foreach (var opt in bucket.Candidates.Take(2))
                {
                    sb.AppendLine($"      {opt.DisplayName}  tgi={opt.RootTgi ?? "<no tgi>"}");
                }
                if (bucket.Candidates.Count > 2)
                {
                    sb.AppendLine($"      … {bucket.Candidates.Count - 2} more option(s)");
                }
            }
            var bodyCandidatesText = sb.ToString().TrimEnd();
            BodyCandidatesSummary = bodyCandidatesText;

            SceneStatus = "Building scene…";
            var renderResult = await simRenderer.BuildSimSceneAsync(graph, token).ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            var combinedDiagnostics = new List<string>(graph.Diagnostics);
            combinedDiagnostics.AddRange(renderResult.Diagnostics);

            if (renderResult.Scene is null)
            {
                AssetGraphDiagnostics = string.Join("\n", combinedDiagnostics);
                SceneStatus = currentScene is null
                    ? "Scene build failed — viewport remains empty."
                    : "Scene build failed — viewport keeps the previous Sim.";
                return;
            }

            var scene = renderResult.Scene;
            if (sim.SkintoneRender is { } skintone)
            {
                SceneStatus = "Composing skin atlas…";
                var atlas = await SimSkinAtlasComposer.BuildAsync(
                    skintone.BaseTexturePngBytes,
                    skintone.DetailNeutralPngBytes,
                    skintone.DetailOverlayPngBytes,
                    skintone.FaceOverlayPngBytes,
                    skintone.FaceCasOverlayPngBytes,
                    pass2Opacity: skintone.OverlayOpacity / 100f,
                    skintoneHue: skintone.SkintoneHue,
                    skintoneSaturation: skintone.SkintoneSaturation,
                    cancellationToken: token).ConfigureAwait(true);
                if (token.IsCancellationRequested)
                {
                    return;
                }
                if (atlas is { Length: > 0 })
                {
                    scene = SimSkintoneMaterialBinder.RebindWithAtlas(scene, atlas);
                    combinedDiagnostics.Add($"Skin atlas: composed {atlas.Length:N0} bytes and rebound on every skintone-routed material.");
                }
                else
                {
                    combinedDiagnostics.Add("Skin atlas: composition failed (base skin texture missing or decode failed); skintone-routed materials may render without diffuse.");
                }
            }
            else
            {
                combinedDiagnostics.Add($"Skin atlas: skipped (no SkintoneRender resolved for synthetic seed with skintoneInstance=0x{seed.SkintoneInstance:X16}).");
            }

            var diagnosticsText = string.Join("\n", combinedDiagnostics);
            var b = scene.Bounds;
            var sceneStatusText = System.FormattableString.Invariant(
                $"Scene ready — meshes={scene.Meshes.Count}, materials={scene.Materials.Count}, bones={scene.Bones.Count}, height≈{b.MaxY - b.MinY:0.00}m.");

            // Populate the cache regardless of whether the selection still matches —
            // future selections of this tuple will short-circuit.
            sceneCache[key] = new CachedSceneEntry(
                Scene: scene,
                AssetGraphStatus: assetGraphStatusText,
                BodyCandidatesSummary: bodyCandidatesText,
                Diagnostics: diagnosticsText,
                SceneStatus: sceneStatusText);

            // Only apply to the viewport if this build still matches the current pick.
            var currentKey = new SceneCacheKey(currentSeed.AgeLabel, currentSeed.GenderLabel, currentSeed.SkintoneInstance);
            if (currentKey.Equals(key))
            {
                AssetGraphDiagnostics = diagnosticsText;
                SceneStatus = $"{sceneStatusText}  [{sceneCache.Count} cached]";
                CurrentScene = scene;
            }
        }
        catch (System.OperationCanceledException)
        {
        }
        catch (System.Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                AssetGraphStatus = $"Build error: {ex.GetType().Name}: {ex.Message}";
            }
        }
    }
}
