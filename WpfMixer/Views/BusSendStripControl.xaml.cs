using System.Windows;
using System.Windows.Controls;
using WpfMixer.ViewModels;

namespace WpfMixer.Views;

public partial class BusSendStripControl : UserControl
{
    public BusSendStripControl()
    {
        InitializeComponent();
    }

    private void OnButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChannelSendViewModel vm)
            vm.SendOn = !vm.SendOn;
    }
}
