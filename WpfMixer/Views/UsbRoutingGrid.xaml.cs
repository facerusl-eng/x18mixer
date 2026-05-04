using System.Windows.Controls;
using WpfMixer.Models;

namespace WpfMixer.Views;

public partial class UsbRoutingGrid : UserControl
{
    public static readonly InputSource[] InputSources = Enum.GetValues<InputSource>();

    public UsbRoutingGrid()
    {
        InitializeComponent();
    }
}
