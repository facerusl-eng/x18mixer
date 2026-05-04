using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfMixer.Converters;

public class BoolToSoloColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (bool)value
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0xA0, 0x00))   // amber
            : new SolidColorBrush(Color.FromRgb(0x3A, 0x2A, 0x00));  // dim amber

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
