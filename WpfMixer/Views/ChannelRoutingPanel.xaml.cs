using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WpfMixer.Models;
using WpfMixer.ViewModels;

namespace WpfMixer.Views;

public partial class ChannelRoutingPanel : UserControl
{
    // Static item source for InputSource combobox
    public static readonly InputSource[] InputSources =
        Enum.GetValues<InputSource>();

    // Converters used inline in XAML DataTriggers to filter Bus vs FX sends
    public static readonly IValueConverter IsBusConverter = new BusIndexConverter(false);
    public static readonly IValueConverter IsFxConverter  = new BusIndexConverter(true);

    public ChannelRoutingPanel()
    {
        InitializeComponent();
    }

    // Pre/Post toggle click — delegates to ViewModel command
    private void BusSendPre_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is BusSend send)
        {
            var vm = GetViewModel();
            vm?.ToggleBusSendPreCommand.Execute(send);
        }
    }

    private MixerViewModel? GetViewModel()
    {
        var el = this as FrameworkElement;
        while (el != null)
        {
            if (el.DataContext is MixerViewModel vm) return vm;
            el = el.Parent as FrameworkElement;
        }
        return null;
    }
}

/// <summary>Returns true if a BusIndex is a bus send (false) or FX send (true).</summary>
internal sealed class BusIndexConverter : IValueConverter
{
    private readonly bool _wantFx;
    public BusIndexConverter(bool wantFx) => _wantFx = wantFx;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int idx)
            return _wantFx ? idx > 6 : idx <= 6;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}
