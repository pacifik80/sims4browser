using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Sims4ResourceExplorer.App.Converters;

/// <summary>
/// True → Visible, anything else (false / null) → Collapsed.
/// Used to gate the busy overlay on <see cref="ViewModels.SimConstructorViewModel.IsBuilding"/>.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, System.Type targetType, object parameter, string language) =>
        value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, System.Type targetType, object parameter, string language) =>
        throw new System.NotSupportedException();
}
