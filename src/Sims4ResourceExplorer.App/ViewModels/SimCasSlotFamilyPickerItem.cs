using CommunityToolkit.Mvvm.ComponentModel;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.App.ViewModels;

public sealed partial class SimCasSlotFamilyPickerItem : ObservableObject
{
    public SimCasSlotFamilyPickerItem(SimCasSlotCandidateSummary summary)
    {
        Summary = summary;
        Options = summary.Candidates
            .Select(SimCasSlotOptionPickerItem.Create)
            .ToArray();
        selectedOption = Options.FirstOrDefault();
    }

    public SimCasSlotCandidateSummary Summary { get; }
    public string Label => Summary.Label;
    public int Count => Summary.Count;
    public string Notes => Summary.Notes;
    public IReadOnlyList<SimCasSlotOptionPickerItem> Options { get; }
    public bool HasOptions => Options.Count > 0;
    public string SourceLabel => Summary.SourceKind switch
    {
        SimCasSlotCandidateSourceKind.ExactPartLink => "Authoritative SimInfo outfit/body-part selection",
        SimCasSlotCandidateSourceKind.CompatibilityFallback => "Compatibility fallback",
        _ => "Unknown source"
    };

    [ObservableProperty]
    private SimCasSlotOptionPickerItem? selectedOption;

    public string SelectedOptionLabel =>
        SelectedOption?.DisplayLabel ?? "No indexed candidate example is available yet.";

    public string OverviewSelectionText =>
        SelectedOption is null
            ? "No indexed example is available yet."
            : $"{SelectedOption.DisplayLabel} | {SelectedOption.PackageName ?? "(unknown package)"}";

    public string SelectionDetailsText =>
        SelectedOption is null
            ? "No indexed candidate example is available yet."
            : string.IsNullOrWhiteSpace(SelectedOption.RootTgi)
                ? (SelectedOption.PackageName ?? "(unknown package)")
                : $"{SelectedOption.PackageName ?? "(unknown package)"}{Environment.NewLine}{SelectedOption.RootTgi}";

    partial void OnSelectedOptionChanged(SimCasSlotOptionPickerItem? value)
    {
        OnPropertyChanged(nameof(SelectedOptionLabel));
        OnPropertyChanged(nameof(OverviewSelectionText));
        OnPropertyChanged(nameof(SelectionDetailsText));
    }
}
