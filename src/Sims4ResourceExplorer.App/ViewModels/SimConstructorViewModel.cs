using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Sims4ResourceExplorer.Assets;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.App.ViewModels;

public sealed partial class SimConstructorViewModel : ObservableObject
{
    private readonly ISyntheticSimService syntheticSimService;

    private string selectedAge = "Adult";
    private string selectedGender = "Female";
    private SimConstructorSeed currentSeed;
    private CancellationTokenSource? rebuildCts;
    private string assetGraphStatus = "Building asset graph…";
    private string bodyCandidatesSummary = string.Empty;
    private string assetGraphDiagnostics = string.Empty;

    public SimConstructorViewModel(ISyntheticSimService syntheticSimService)
    {
        this.syntheticSimService = syntheticSimService;
        AvailableAges = syntheticSimService.AvailableHumanAges;
        AvailableGenders = syntheticSimService.AvailableHumanGenders;
        currentSeed = syntheticSimService.CreateHumanSeed(selectedAge, selectedGender);
        TriggerRebuild();
    }

    public IReadOnlyList<string> AvailableAges { get; }
    public IReadOnlyList<string> AvailableGenders { get; }

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

    public string SeedDisplayName => $"{CurrentSeed.SpeciesLabel} | {CurrentSeed.AgeLabel} | {CurrentSeed.GenderLabel}";

    public string SeedSummary => CurrentSeed.SummaryText;

    public string SyntheticFullInstanceHex => $"0x{CurrentSeed.SyntheticFullInstance:X16}";

    public string OutfitPartCountText => $"Body-driving outfit: {CurrentSeed.OutfitPartCount} part(s)";

    private void Rebuild()
    {
        CurrentSeed = syntheticSimService.CreateHumanSeed(selectedAge, selectedGender);
        TriggerRebuild();
    }

    private void TriggerRebuild()
    {
        rebuildCts?.Cancel();
        rebuildCts = new CancellationTokenSource();
        var token = rebuildCts.Token;
        AssetGraphStatus = "Building asset graph…";
        BodyCandidatesSummary = string.Empty;
        AssetGraphDiagnostics = string.Empty;
        _ = RebuildAssetGraphAsync(token);
    }

    private async Task RebuildAssetGraphAsync(CancellationToken token)
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

            AssetGraphDiagnostics = string.Join("\n", graph.Diagnostics);
        }
        catch (System.OperationCanceledException)
        {
        }
        catch (System.Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                AssetGraphStatus = $"Asset graph build error: {ex.GetType().Name}: {ex.Message}";
            }
        }
    }
}
