using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.App.ViewModels;

public sealed class SimBodyGraphLayerItem
{
    private SimBodyGraphLayerItem(
        string label,
        string roleLabel,
        string statusLabel,
        string selectedAssetLabel,
        string sourceLabel,
        string notes)
    {
        Label = label;
        RoleLabel = roleLabel;
        StatusLabel = statusLabel;
        SelectedAssetLabel = selectedAssetLabel;
        SourceLabel = sourceLabel;
        Notes = notes;
    }

    public string Label { get; }
    public string RoleLabel { get; }
    public string StatusLabel { get; }
    public string SelectedAssetLabel { get; }
    public string SourceLabel { get; }
    public string Notes { get; }
    public bool IsRendered =>
        string.Equals(StatusLabel, "Rendered head layer", StringComparison.Ordinal) ||
        string.Equals(StatusLabel, "Rendered in current base-body preview", StringComparison.Ordinal) ||
        string.Equals(StatusLabel, "Rendered alternate layer", StringComparison.Ordinal);

    public bool IsActive =>
        string.Equals(StatusLabel, "Rendered in current base-body preview", StringComparison.Ordinal) ||
        string.Equals(StatusLabel, "Active but not rendered", StringComparison.Ordinal);

    public static SimBodyGraphLayerItem Create(
        SimBodyCandidateFamilyPickerItem family,
        SimBodyAssemblyLayerState state,
        bool isRendered)
    {
        var roleLabel = BuildRoleLabel(family.Label, state);
        var statusLabel = BuildStatusLabel(family.Label, state, isRendered);
        var notes = BuildNotes(family, state, isRendered);
        return new SimBodyGraphLayerItem(
            family.Label,
            roleLabel,
            statusLabel,
            family.SelectedOption?.DisplayLabel ?? "No resolved body asset selected",
            BuildSourceLabel(family.Summary.SourceKind),
            notes);
    }

    private static string BuildRoleLabel(string label, SimBodyAssemblyLayerState state) => state switch
    {
        _ when string.Equals(label, "Head", StringComparison.OrdinalIgnoreCase) => "Head shell",
        SimBodyAssemblyLayerState.Active when string.Equals(label, "Shoes", StringComparison.OrdinalIgnoreCase) => "Footwear overlay",
        SimBodyAssemblyLayerState.Active => "Geometry shell",
        SimBodyAssemblyLayerState.Blocked => "Suppressed layer",
        _ => "Alternate layer"
    };

    private static string BuildStatusLabel(string label, SimBodyAssemblyLayerState state, bool isRendered) => state switch
    {
        _ when string.Equals(label, "Head", StringComparison.OrdinalIgnoreCase) && isRendered => "Rendered head layer",
        SimBodyAssemblyLayerState.Active when isRendered => "Rendered in current base-body preview",
        SimBodyAssemblyLayerState.Active => "Active but not rendered",
        SimBodyAssemblyLayerState.Blocked => "Suppressed by shell policy",
        _ when isRendered => "Rendered alternate layer",
        _ => "Available alternate family"
    };

    private static string BuildSourceLabel(SimBodyCandidateSourceKind sourceKind) => sourceKind switch
    {
        SimBodyCandidateSourceKind.ExactPartLink => "Authoritative SimInfo outfit/body-part selection",
        SimBodyCandidateSourceKind.IndexedDefaultBodyRecipe => "Indexed default/naked body recipe",
        SimBodyCandidateSourceKind.CanonicalFoundation => "Canonical default foundation",
        SimBodyCandidateSourceKind.BodyTypeFallback => "Template body-type fallback",
        SimBodyCandidateSourceKind.ArchetypeCompatibilityFallback => "Archetype compatibility fallback",
        _ => "Unknown source"
    };

    private static string BuildNotes(
        SimBodyCandidateFamilyPickerItem family,
        SimBodyAssemblyLayerState state,
        bool isRendered)
    {
        if (family.SelectedOption is null)
        {
            return "No resolved body asset is currently selected for this layer.";
        }

        return state switch
        {
            SimBodyAssemblyLayerState.Active when isRendered =>
                $"This layer is currently part of the rendered base-body preview. {family.Notes}",
            SimBodyAssemblyLayerState.Active =>
                $"This layer is active in the current base-body graph but did not yield a renderable preview layer yet. {family.Notes}",
            SimBodyAssemblyLayerState.Blocked =>
                $"This layer is currently suppressed by a higher-priority body shell. {family.Notes}",
            _ when isRendered =>
                $"This alternate layer still contributed to the current base-body preview. {family.Notes}",
            _ =>
                $"This layer is available as an alternate family for the current base-body graph. {family.Notes}"
        };
    }
}
