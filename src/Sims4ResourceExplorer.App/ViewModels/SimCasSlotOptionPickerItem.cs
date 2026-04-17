using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.App.ViewModels;

public sealed class SimCasSlotOptionPickerItem
{
    private SimCasSlotOptionPickerItem(SimCasSlotOptionSummary option)
    {
        Option = option;
    }

    public SimCasSlotOptionSummary Option { get; }
    public Guid AssetId => Option.AssetId;
    public string DisplayLabel => Option.DisplayName;
    public string? PackagePath => Option.PackagePath;
    public string? PackageName => Option.PackageName;
    public string? RootTgi => Option.RootTgi;

    public static SimCasSlotOptionPickerItem Create(SimCasSlotOptionSummary option) =>
        new(option);

    public override string ToString() =>
        string.IsNullOrWhiteSpace(PackageName)
            ? DisplayLabel
            : $"{DisplayLabel} ({PackageName})";
}
