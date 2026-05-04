using System.Windows;
using System.Windows.Controls;
using WpfMixer.ViewModels;

namespace WpfMixer.Views;

public partial class BusMixView : UserControl
{
    public BusMixView()
    {
        InitializeComponent();
    }

    private void BusMute_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is BusMixViewModel vm)
            vm.BusMasterMute = !vm.BusMasterMute;
    }
}
