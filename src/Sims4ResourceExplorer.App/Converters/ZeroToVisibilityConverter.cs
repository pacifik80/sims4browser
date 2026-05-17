using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Sims4ResourceExplorer.App.Converters;

/// <summary>
/// XAML converter mapping numeric "is zero" to <see cref="Visibility"/>.
/// Used to overlay a "None" indicator on the zero-instance skintone swatch.
/// </summary>
public sealed class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, System.Type targetType, object parameter, string language)
    {
        bool isZero = value switch
        {
            ulong u => u == 0ul,
            long l => l == 0L,
            uint ui => ui == 0u,
            int i => i == 0,
            null => true,
            _ => false,
        };
        return isZero ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, System.Type targetType, object parameter, string language) =>
        throw new System.NotSupportedException();
}
