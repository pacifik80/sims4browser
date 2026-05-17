using CommunityToolkit.Mvvm.ComponentModel;
using Sims4ResourceExplorer.Assets;

namespace Sims4ResourceExplorer.App.ViewModels;

public sealed partial class SimConstructorViewModel : ObservableObject
{
    private readonly ISyntheticSimService syntheticSimService;

    private string selectedAge = "Adult";
    private string selectedGender = "Female";
    private SimConstructorSeed currentSeed;

    public SimConstructorViewModel(ISyntheticSimService syntheticSimService)
    {
        this.syntheticSimService = syntheticSimService;
        AvailableAges = syntheticSimService.AvailableHumanAges;
        AvailableGenders = syntheticSimService.AvailableHumanGenders;
        currentSeed = syntheticSimService.CreateHumanSeed(selectedAge, selectedGender);
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
                RebuildSeed();
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
                RebuildSeed();
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
                OnPropertyChanged(nameof(RenderStatusText));
            }
        }
    }

    public string SeedDisplayName => $"{CurrentSeed.SpeciesLabel} | {CurrentSeed.AgeLabel} | {CurrentSeed.GenderLabel}";

    public string SeedSummary => CurrentSeed.SummaryText;

    public string SyntheticFullInstanceHex => $"0x{CurrentSeed.SyntheticFullInstance:X16}";

    public string OutfitPartCountText => $"Body-driving outfit: {CurrentSeed.OutfitPartCount} part(s)";

    public string RenderStatusText => "Render plumbing lands in P0.4b (graph builder is mid-refactor in the working tree).";

    private void RebuildSeed()
    {
        CurrentSeed = syntheticSimService.CreateHumanSeed(selectedAge, selectedGender);
    }
}
