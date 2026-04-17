namespace Sims4ResourceExplorer.App.ViewModels;

public sealed class SimBodyPreviewCoverageItem
{
    public SimBodyPreviewCoverageItem(string label, string statusLabel, string notes)
    {
        Label = label;
        StatusLabel = statusLabel;
        Notes = notes;
    }

    public string Label { get; }
    public string StatusLabel { get; }
    public string Notes { get; }
}
