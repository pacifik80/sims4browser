using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.App.ViewModels;

public sealed class SimTemplatePickerItem
{
    private SimTemplatePickerItem(SimTemplateOptionSummary option)
    {
        Option = option;
    }

    public SimTemplateOptionSummary Option { get; }
    public Guid ResourceId => Option.ResourceId;
    public string DisplayLabel => Option.IsRepresentative
        ? $"{Option.DisplayName} (Representative)"
        : Option.HasAuthoritativeBodyParts
            ? $"{Option.DisplayName} (Body)"
            : Option.DisplayName;
    public string PackagePath => Option.PackagePath;
    public string? PackageName => Option.PackageName;
    public string RootTgi => Option.RootTgi;
    public string DetailsText =>
        $"{Option.PackageName ?? Path.GetFileName(Option.PackagePath)}{Environment.NewLine}{Option.RootTgi}";
    public string Notes => Option.Notes;
    public bool IsRepresentative => Option.IsRepresentative;
    public bool HasAuthoritativeBodyParts => Option.HasAuthoritativeBodyParts;

    public static SimTemplatePickerItem Create(SimTemplateOptionSummary option) => new(option);

    public override string ToString() => DisplayLabel;
}
