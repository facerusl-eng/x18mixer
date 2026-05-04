using System;
using System.Globalization;
using System.Windows.Data;

namespace WpfMixer.Converters;

public class BoolToMuteTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (bool)value ? "MUTED" : "LIVE";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
