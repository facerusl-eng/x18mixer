using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfMixer.Converters;

/// <summary>Converts IsKeyHighlighted bool to a border brush for key-press flash.</summary>
public class BoolToHighlightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (bool)value
            ? new SolidColorBrush(Colors.Yellow)
            : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
