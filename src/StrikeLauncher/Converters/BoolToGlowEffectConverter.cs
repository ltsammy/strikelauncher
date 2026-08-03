using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Effects;

namespace StrikeLauncher.Converters;

public sealed class BoolToGlowEffectConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value is true ? "OkGlowEffect" : "WarnGlowEffect";
        return Application.Current.TryFindResource(key) as Effect ?? new DropShadowEffect();
    }

    public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
