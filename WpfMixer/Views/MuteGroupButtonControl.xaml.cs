using System.Windows;
using System.Windows.Controls;
using WpfMixer.Models;
using WpfMixer.ViewModels;

namespace WpfMixer.Views;

public partial class MuteGroupButtonControl : UserControl
{
    public MuteGroupButtonControl() => InitializeComponent();

    private MixerViewModel Vm => (MixerViewModel)((FrameworkElement)Parent).DataContext;
    private MuteGroup Grp => (MuteGroup)DataContext;

    private void FireGroup_Click(object sender, RoutedEventArgs e)
        => Vm.ActivateMuteGroupCommand.Execute(Grp);

    private void KeyBadge_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var dlg = new KeyAssignDialog(Grp.AssignedKey, Grp.IsMomentaryMute);
        if (dlg.ShowDialog() != true) return;

        var conflict = Vm.AssignKeyToGroup(Grp, dlg.SelectedKey ?? string.Empty);
        if (conflict != null)
        {
            var result = MessageBox.Show(
                $"Key '{dlg.SelectedKey}' is already assigned to {conflict}.\nOverride?",
                "Key Conflict", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
                Vm.AssignKeyToGroup(Grp, dlg.SelectedKey ?? string.Empty, forceAssign: true);
        }

        Grp.IsMomentaryMute = dlg.IsMomentary;
    }

    private void EditGroup_Click(object sender, RoutedEventArgs e)
    {
        new MuteGroupEditorDialog(Grp, Vm.Mixer.InputChannels).ShowDialog();
    }

    private void RemoveGroup_Click(object sender, RoutedEventArgs e)
        => Vm.RemoveMuteGroupCommand.Execute(Grp);
}
