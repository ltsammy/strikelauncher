using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace StrikeLauncher.Converters;

public sealed class BoolToStatusBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value is true ? "OkBrush" : "WarnBrush";
        return Application.Current.TryFindResource(key) as SolidColorBrush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
