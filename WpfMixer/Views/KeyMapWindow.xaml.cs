using System.Linq;
using System.Windows;
using WpfMixer.ViewModels;

namespace WpfMixer.Views;

public partial class KeyMapWindow : Window
{
    public KeyMapWindow(MainViewModel vm)
    {
        InitializeComponent();

        ChannelGrid.ItemsSource = vm.Channels.Select(ch => new
        {
            ch.Name,
            ch.AssignedKey,
            ModeText = ch.IsMomentaryMute ? "Momentary" : "Toggle"
        }).ToList();

        GroupGrid.ItemsSource = vm.MuteGroups.Select(g => new
        {
            g.Name,
            g.AssignedKey,
            ModeText = g.IsMomentaryMute ? "Momentary" : "Toggle"
        }).ToList();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
        => Close();
}
