using System;
using System.Globalization;
using System.Windows.Data;

namespace WpfMixer.Converters;

/// <summary>
/// Used to place the 0dB tick mark on the fader canvas.
/// Converts a container height + percent parameter to a canvas Y offset from bottom.
/// Not used in XAML (canvas positioning done in code-behind for simplicity).
/// </summary>
public class PercentToCanvasYConverter : IValueConverter
{
    public static readonly PercentToCanvasYConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double height) return 0.0;
        if (!double.TryParse(parameter?.ToString(), out double pct)) pct = 0.75;
        return height * (1.0 - pct);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
