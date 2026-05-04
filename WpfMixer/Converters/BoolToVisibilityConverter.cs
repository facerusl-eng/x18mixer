using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfMixer.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public static readonly BoolToVisibilityConverter Instance = new();
    /// <summary>Returns Collapsed when true, Visible when false.</summary>
    public static readonly BoolToVisibilityConverter NegInstance = new() { Invert = true };

    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = (bool)value;
        bool invert = Invert || parameter?.ToString() == "invert";
        return (b ^ invert) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => (Visibility)value == Visibility.Visible;
}
