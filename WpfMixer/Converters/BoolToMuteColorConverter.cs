using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfMixer.Converters;

public class BoolToMuteColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (bool)value
            ? new SolidColorBrush(Color.FromRgb(233, 69, 96))    // muted = red
            : new SolidColorBrush(Color.FromRgb(39, 174, 96));   // live  = green

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
