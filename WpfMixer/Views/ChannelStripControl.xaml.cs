using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfMixer.ViewModels;

namespace WpfMixer.Views;

public partial class ChannelStripControl : UserControl
{
    public ChannelStripControl() => InitializeComponent();

    private MixerViewModel Vm => (MixerViewModel)((FrameworkElement)Parent).DataContext;

    // DataContext can be ChannelViewModel (Mixer tab) or the legacy Channel (routing panel).
    // Handle both safely.
    private ChannelViewModel? ChVm  => DataContext as ChannelViewModel;

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        if (ChVm is not null) ChVm.IsMuted = !ChVm.IsMuted;
    }

    private void Solo_Click(object sender, RoutedEventArgs e)
    {
        if (ChVm is null) return;
        // Exclusive solo via ViewModel collection
        foreach (var vm in Vm.ChannelViewModels) vm.IsSolo = false;
        ChVm.IsSolo = true;
    }

    private void Select_Click(object sender, MouseButtonEventArgs e)
    {
        if (ChVm is null) return;
        foreach (var vm in Vm.ChannelViewModels) vm.IsSelected = false;
        ChVm.IsSelected = true;
    }

    private void KeyBadge_Click(object sender, MouseButtonEventArgs e)
    {
        if (ChVm is null) return;

        var dlg = new KeyAssignDialog(ChVm.AssignedKey, ChVm.IsMomentaryMute);
        if (dlg.ShowDialog() != true) return;

        // Conflict check
        var conflict = Vm.ChannelViewModels
            .Where(v => v != ChVm && v.AssignedKey == dlg.SelectedKey)
            .Select(v => v.Name)
            .FirstOrDefault();

        if (conflict != null)
        {
            var result = MessageBox.Show(
                $"Key '{dlg.SelectedKey}' is already used by {conflict}.\nOverride?",
                "Key Conflict", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            // Clear the conflict
            var conflicted = Vm.ChannelViewModels.First(v => v.AssignedKey == dlg.SelectedKey);
            conflicted.AssignedKey = null;
        }

        ChVm.AssignedKey = string.IsNullOrWhiteSpace(dlg.SelectedKey) ? null : dlg.SelectedKey;
        ChVm.IsMomentaryMute = dlg.IsMomentary;
    }
}

