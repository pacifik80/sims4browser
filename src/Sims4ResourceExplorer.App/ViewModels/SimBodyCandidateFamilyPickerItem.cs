using CommunityToolkit.Mvvm.ComponentModel;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.App.ViewModels;

public sealed partial class SimBodyCandidateFamilyPickerItem : ObservableObject
{
    public SimBodyCandidateFamilyPickerItem(SimBodyCandidateSummary summary)
    {
        Summary = summary;
        Options = summary.Candidates
            .Select(SimCasSlotOptionPickerItem.Create)
            .ToArray();
        selectedOption = Options.FirstOrDefault();
    }

    public SimBodyCandidateSummary Summary { get; }
    public string Label => Summary.Label;
    public int Count => Summary.Count;
    public string Notes => Summary.Notes;
    public IReadOnlyList<SimCasSlotOptionPickerItem> Options { get; }
    public bool HasOptions => Options.Count > 0;

    [ObservableProperty]
    private SimCasSlotOptionPickerItem? selectedOption;

    public string SelectedOptionLabel =>
        SelectedOption?.DisplayLabel ?? "No resolved body asset is available yet.";

    public string SelectionDetailsText =>
        SelectedOption is null
            ? "No resolved body asset is available yet."
            : string.IsNullOrWhiteSpace(SelectedOption.RootTgi)
                ? (SelectedOption.PackageName ?? "(unknown package)")
                : $"{SelectedOption.PackageName ?? "(unknown package)"}{Environment.NewLine}{SelectedOption.RootTgi}";

    partial void OnSelectedOptionChanged(SimCasSlotOptionPickerItem? value)
    {
        OnPropertyChanged(nameof(SelectedOptionLabel));
        OnPropertyChanged(nameof(SelectionDetailsText));
    }
}
