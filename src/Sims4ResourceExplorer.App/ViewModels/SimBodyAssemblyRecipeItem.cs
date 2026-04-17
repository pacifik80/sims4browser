using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.App.ViewModels;

public sealed class SimBodyAssemblyRecipeItem
{
    private SimBodyAssemblyRecipeItem(
        string label,
        string contribution,
        bool isActive,
        string activityLabel,
        string selectedAssetLabel,
        string sourceLabel,
        string notes)
    {
        Label = label;
        Contribution = contribution;
        IsActive = isActive;
        ActivityLabel = activityLabel;
        SelectedAssetLabel = selectedAssetLabel;
        SourceLabel = sourceLabel;
        Notes = notes;
    }

    public string Label { get; }
    public string Contribution { get; }
    public bool IsActive { get; }
    public string ActivityLabel { get; }
    public string SelectedAssetLabel { get; }
    public string SourceLabel { get; }
    public string Notes { get; }

    public static SimBodyAssemblyRecipeItem Create(
        SimBodyCandidateFamilyPickerItem family,
        SimBodyAssemblyLayerState state,
        string stateNotes) =>
        new(
            family.Label,
            BuildContributionLabel(family.Label),
            state == SimBodyAssemblyLayerState.Active,
            BuildActivityLabel(state),
            family.SelectedOption?.DisplayLabel ?? "No resolved body asset selected",
            BuildSourceLabel(family.Summary.SourceKind),
            stateNotes);

    private static string BuildContributionLabel(string label) => label switch
    {
        "Full Body" => "Whole-body shell",
        "Body" => "Primary body shell",
        "Top" => "Upper-body layer",
        "Bottom" => "Lower-body layer",
        "Shoes" => "Footwear layer",
        _ => "Body assembly layer"
    };

    private static string BuildSourceLabel(SimBodyCandidateSourceKind sourceKind) => sourceKind switch
    {
        SimBodyCandidateSourceKind.ExactPartLink => "Authoritative SimInfo outfit/body-part selection",
        SimBodyCandidateSourceKind.CanonicalFoundation => "Canonical default foundation",
        SimBodyCandidateSourceKind.BodyTypeFallback => "Template body-type fallback",
        SimBodyCandidateSourceKind.ArchetypeCompatibilityFallback => "Archetype compatibility fallback",
        _ => "Unknown source"
    };

    private static string BuildActivityLabel(SimBodyAssemblyLayerState state) => state switch
    {
        SimBodyAssemblyLayerState.Active => "Active in current base-body recipe",
        SimBodyAssemblyLayerState.Blocked => "Currently suppressed by higher-priority body shell",
        _ => "Available alternate layer"
    };
}
