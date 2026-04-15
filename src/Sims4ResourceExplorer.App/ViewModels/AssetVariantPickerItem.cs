using System.Globalization;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.App.ViewModels;

public sealed class AssetVariantPickerItem
{
    private AssetVariantPickerItem(AssetVariantSummary? variant, string label, string kindText, string? swatchHex)
    {
        Variant = variant;
        Label = label;
        KindText = kindText;
        SwatchHex = swatchHex;
    }

    public AssetVariantSummary? Variant { get; }
    public string Label { get; }
    public string KindText { get; }
    public string? SwatchHex { get; }

    public static AssetVariantPickerItem CreateDefault(int indexedVariantCount) =>
        new(
            variant: null,
            label: "Catalog default",
            kindText: indexedVariantCount == 1 ? "1 indexed variant" : $"{indexedVariantCount:N0} indexed variants",
            swatchHex: null);

    public static AssetVariantPickerItem Create(AssetVariantSummary variant) =>
        new(
            variant,
            variant.DisplayLabel,
            variant.VariantKind,
            TryNormalizeSwatchHex(variant.SwatchHex));

    private static string? TryNormalizeSwatchHex(string? swatchHex)
    {
        if (string.IsNullOrWhiteSpace(swatchHex))
        {
            return null;
        }

        var normalized = swatchHex.Trim();
        if (normalized.StartsWith('#'))
        {
            normalized = normalized[1..];
        }

        if (normalized.Length == 6 &&
            uint.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        {
            return $"#{normalized.ToUpperInvariant()}";
        }

        if (normalized.Length == 8 &&
            uint.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        {
            return $"#{normalized.ToUpperInvariant()}";
        }

        return null;
    }

    public override string ToString() =>
        string.IsNullOrWhiteSpace(SwatchHex)
            ? $"{Label} ({KindText})"
            : $"{Label} ({KindText}, {SwatchHex})";
}
