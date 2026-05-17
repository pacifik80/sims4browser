using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Sims4ResourceExplorer.App.Converters;

/// <summary>
/// XAML converter mapping an ARGB-encoded <c>uint</c> (or nullable <c>uint?</c>) to a
/// <see cref="SolidColorBrush"/>. Null / 0 values fall back to a neutral checker-like
/// gray so the skintone picker still shows a placeholder when a TONE failed to parse.
/// </summary>
public sealed class UintArgbToBrushConverter : IValueConverter
{
    public object Convert(object value, System.Type targetType, object parameter, string language)
    {
        uint argb;
        switch (value)
        {
            case uint u: argb = u; break;
            case int i: argb = unchecked((uint)i); break;
            case null: return new SolidColorBrush(Colors.DimGray);
            default: return new SolidColorBrush(Colors.DimGray);
        }
        if (argb == 0)
        {
            return new SolidColorBrush(Colors.DimGray);
        }
        var a = (byte)((argb >> 24) & 0xFF);
        if (a == 0)
        {
            a = 0xFF;
        }
        var r = (byte)((argb >> 16) & 0xFF);
        var g = (byte)((argb >> 8) & 0xFF);
        var b = (byte)(argb & 0xFF);
        return new SolidColorBrush(ColorHelper.FromArgb(a, r, g, b));
    }

    public object ConvertBack(object value, System.Type targetType, object parameter, string language) =>
        throw new System.NotSupportedException();
}
