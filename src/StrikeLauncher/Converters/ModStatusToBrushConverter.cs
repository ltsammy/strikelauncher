using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using StrikeLauncher.Models;

namespace StrikeLauncher.Converters;

public sealed class ModStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value switch
        {
            ModStatus.Installed => "OkBrush",
            ModStatus.Missing => "WarnBrush",
            ModStatus.Subscribing => "AccentBrush",
            ModStatus.Failed => "ErrorBrush",
            _ => "MutedBrush"
        };

        return Application.Current.TryFindResource(key) as SolidColorBrush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
