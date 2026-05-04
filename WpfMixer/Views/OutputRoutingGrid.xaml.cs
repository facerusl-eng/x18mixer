using System.Windows.Controls;
using WpfMixer.Models;

namespace WpfMixer.Views;

public partial class OutputRoutingGrid : UserControl
{
    public static readonly OutputSource[] OutputSources = Enum.GetValues<OutputSource>();

    public OutputRoutingGrid()
    {
        InitializeComponent();
    }
}
