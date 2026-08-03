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

        var brush = Application.Current.TryFindResource(key) as SolidColorBrush ?? Brushes.Gray;

        if (string.Equals(parameter as string, "Subtle", StringComparison.OrdinalIgnoreCase))
        {
            var c = brush.Color;
            return new SolidColorBrush(Color.FromArgb(38, c.R, c.G, c.B));
        }

        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
