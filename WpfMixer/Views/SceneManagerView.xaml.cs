using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WpfMixer.ViewModels;

namespace WpfMixer.Views;

public partial class SceneManagerView : UserControl
{
    public SceneManagerView()
    {
        InitializeComponent();
    }

    private SceneManagerViewModel? Vm => DataContext as SceneManagerViewModel;

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        var dlg = new OpenFileDialog { Filter = "Scene (*.json)|*.json" };
        if (dlg.ShowDialog() == true)
            Vm.ImportSceneCommand.Execute(dlg.FileName);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null || Vm.SelectedScene is null) return;
        var dlg = new SaveFileDialog { Filter = "Scene (*.json)|*.json", FileName = Vm.SelectedScene.Name + ".json" };
        if (dlg.ShowDialog() == true)
            Vm.ExportSceneCommand.Execute(dlg.FileName);
    }

    private void CreatePreset_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;

        var result = MessageBox.Show(
            "Create built-in presets now? (Solo Singer, Acoustic Duo, Karaoke Night, Full Band, DJ Set)",
            "Create Presets",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        Vm.CreatePresetCommand.Execute("Solo Singer");
        Vm.CreatePresetCommand.Execute("Acoustic Duo");
        Vm.CreatePresetCommand.Execute("Karaoke Night");
        Vm.CreatePresetCommand.Execute("Full Band");
        Vm.CreatePresetCommand.Execute("DJ Set");
    }
}
