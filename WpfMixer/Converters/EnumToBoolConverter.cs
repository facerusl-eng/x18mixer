using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfMixer.Converters;

/// <summary>
/// Converts an enum value to bool for RadioButton binding.
/// ConverterParameter must be the string name of the enum value to match.
/// Usage: IsChecked="{Binding MyEnum, Converter={StaticResource EnumToBool}, ConverterParameter=ValueName}"
/// </summary>
public sealed class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return value.ToString() == parameter.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter != null)
            return Enum.Parse(targetType, parameter.ToString()!);
        return DependencyProperty.UnsetValue;
    }
}
