using System.Windows;
using System.Windows.Controls;
using WpfMixer.Models;
using WpfMixer.ViewModels;

namespace WpfMixer.Views;

public partial class ChannelStripControl : UserControl
{
    public ChannelStripControl()
    {
        InitializeComponent();
    }

    private MainViewModel Vm => (MainViewModel)((FrameworkElement)Parent).DataContext;
    private Channel Ch => (Channel)DataContext;

    private void MuteButton_Click(object sender, RoutedEventArgs e)
    {
        Vm.ToggleMuteCommand.Execute(Ch);
    }

    private void KeyBadge_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var dlg = new KeyAssignDialog(Ch.AssignedKey, Ch.IsMomentaryMute);
        if (dlg.ShowDialog() != true) return;

        var conflict = Vm.AssignKeyToChannel(Ch, dlg.SelectedKey ?? string.Empty);
        if (conflict != null)
        {
            var result = MessageBox.Show(
                $"Key '{dlg.SelectedKey}' is already assigned to {conflict}.\nOverride?",
                "Key Conflict", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
                Vm.AssignKeyToChannel(Ch, dlg.SelectedKey ?? string.Empty, forceAssign: true);
        }

        Ch.IsMomentaryMute = dlg.IsMomentary;
    }

    private void RemoveChannel_Click(object sender, RoutedEventArgs e)
    {
        Vm.RemoveChannelCommand.Execute(Ch);
    }
}
