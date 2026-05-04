using System;
using System.Globalization;
using System.Windows.Data;

namespace WpfMixer.Converters;

/// <summary>
/// Converts a 0.0–1.0 fader value to a dB string.
/// Uses the standard linear-to-dB mapping where 0.75 ≈ 0 dB (unity).
/// </summary>
public class DoubleToDbStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double v) return "---";
        if (v <= 0.0) return "-∞";

        // Map 0.75 → 0 dB; linear scale above/below unity
        double db = 20.0 * Math.Log10(v / 0.75);
        return db >= 0 ? $"+{db:F1}" : $"{db:F1}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
