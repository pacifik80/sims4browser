using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.App.ViewModels;

public sealed class SimBodyPreviewLayerItem
{
    private SimBodyPreviewLayerItem(
        string label,
        string assetLabel,
        string statusLabel,
        string packageLabel,
        string rootTgi,
        string notes)
    {
        Label = label;
        AssetLabel = assetLabel;
        StatusLabel = statusLabel;
        PackageLabel = packageLabel;
        RootTgi = rootTgi;
        Notes = notes;
    }

    public string Label { get; }
    public string AssetLabel { get; }
    public string StatusLabel { get; }
    public string PackageLabel { get; }
    public string RootTgi { get; }
    public string Notes { get; }

    public static SimBodyPreviewLayerItem Create(
        string familyLabel,
        AssetSummary asset,
        ResourceMetadata sceneRoot,
        bool isPrimaryLayer) =>
        new(
            familyLabel,
            asset.DisplayName,
            isPrimaryLayer ? "Primary base-body layer" : "Composed body layer",
            asset.PackageName ?? Path.GetFileName(asset.PackagePath),
            sceneRoot.Key.FullTgi,
            $"{BuildContributionLabel(familyLabel)} | {asset.Category ?? "CAS"}");

    private static string BuildContributionLabel(string label) => label switch
    {
        "Full Body" => "Whole-body shell",
        "Body" => "Primary body shell",
        "Head" => "Head shell",
        "Top" => "Upper-body layer",
        "Bottom" => "Lower-body layer",
        "Shoes" => "Footwear layer",
        _ => "Body assembly layer"
    };
}
