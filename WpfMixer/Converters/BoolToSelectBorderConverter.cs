using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfMixer.Converters;

/// <summary>IsSelected → border/dot color (blue when selected, transparent otherwise).</summary>
public class BoolToSelectBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (bool)value
            ? new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0))
            : new SolidColorBrush(Colors.Transparent);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
