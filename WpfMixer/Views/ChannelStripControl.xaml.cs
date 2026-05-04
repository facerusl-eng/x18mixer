using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfMixer.Models;
using WpfMixer.ViewModels;

namespace WpfMixer.Views;

public partial class ChannelStripControl : UserControl
{
    public ChannelStripControl() => InitializeComponent();

    private MixerViewModel Vm => (MixerViewModel)((FrameworkElement)Parent).DataContext;
    private Channel Ch => (Channel)DataContext;

    private void Mute_Click(object sender, RoutedEventArgs e)
        => Vm.ToggleMuteCommand.Execute(Ch);

    private void Solo_Click(object sender, RoutedEventArgs e)
        => Vm.ToggleSoloCommand.Execute(Ch);

    private void Select_Click(object sender, MouseButtonEventArgs e)
        => Vm.SelectChannelCommand.Execute(Ch);

    private void KeyBadge_Click(object sender, MouseButtonEventArgs e)
    {
        var dlg = new KeyAssignDialog(Ch.AssignedKey, Ch.IsMomentaryMute);
        if (dlg.ShowDialog() != true) return;

        var conflict = Vm.AssignKeyToChannel(Ch, dlg.SelectedKey ?? string.Empty);
        if (conflict != null)
        {
            var result = MessageBox.Show(
                $"Key '{dlg.SelectedKey}' is already used by {conflict}.\nOverride?",
                "Key Conflict", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
                Vm.AssignKeyToChannel(Ch, dlg.SelectedKey ?? string.Empty, forceAssign: true);
        }
        Ch.IsMomentaryMute = dlg.IsMomentary;
    }
}

