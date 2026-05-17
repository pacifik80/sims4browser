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
                Rebuild();
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
        rebuildCts?.Cancel();
        rebuildCts = new CancellationTokenSource();
        var token = rebuildCts.Token;
        AssetGraphStatus = "Building asset graph…";
        SceneStatus = "Waiting for asset graph…";
        BodyCandidatesSummary = string.Empty;
        AssetGraphDiagnostics = string.Empty;
        CurrentScene = null;
        _ = RebuildSceneAsync(token);
    }

    private async Task RebuildSceneAsync(CancellationToken token)
    {
        try
        {
            var graph = await syntheticSimService.BuildHumanAssetGraphAsync(currentSeed, token).ConfigureAwait(true);
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
            AssetGraphStatus = $"Asset graph ready — body assembly: {sim.BodyAssembly.Mode}, layers: {sim.BodyAssembly.Layers.Count}.";

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
            BodyCandidatesSummary = sb.ToString().TrimEnd();

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
                SceneStatus = "Scene build failed — viewport remains empty.";
                CurrentScene = null;
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
                combinedDiagnostics.Add($"Skin atlas: skipped (no SkintoneRender resolved for synthetic seed with skintoneInstance=0x{currentSeed.SkintoneInstance:X16}).");
            }

            AssetGraphDiagnostics = string.Join("\n", combinedDiagnostics);
            var b = scene.Bounds;
            SceneStatus = System.FormattableString.Invariant(
                $"Scene ready — meshes={scene.Meshes.Count}, materials={scene.Materials.Count}, bones={scene.Bones.Count}, height≈{b.MaxY - b.MinY:0.00}m.");
            CurrentScene = scene;
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
