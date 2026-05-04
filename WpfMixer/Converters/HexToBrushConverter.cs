using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfMixer.Converters;

/// <summary>Converts a "#RRGGBB" or "#AARRGGBB" hex string to a SolidColorBrush.</summary>
public class HexToBrushConverter : IValueConverter
{
    public static readonly HexToBrushConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(value?.ToString() ?? "#FF607D8B");
            return new SolidColorBrush(color);
        }
        catch
        {
            return new SolidColorBrush(Color.FromRgb(0x60, 0x7D, 0x8B));
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
