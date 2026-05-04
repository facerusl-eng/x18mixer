using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfMixer.ViewModels;

namespace WpfMixer;

public partial class MainWindow : Window
{
    private readonly MixerViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        // Auto-discover and connect on launch
        _ = _vm.InitializeAsync();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F10)
        {
            _vm.TogglePerformanceModeCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (_vm.IsPerformanceMode && e.Key == Key.Escape)
        {
            _vm.TogglePerformanceModeCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (Keyboard.FocusedElement is TextBox) return;
        if (_vm.KeyboardService.HandleKeyDown(e.Key)) e.Handled = true;
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (_vm.KeyboardService.HandleKeyUp(e.Key)) e.Handled = true;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        => _vm.Cleanup();

    private void Preset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item)
        {
            var tag = item.Tag?.ToString();
            if (!string.IsNullOrEmpty(tag))
                _vm.ApplyPresetCommand.Execute(tag);
        }
    }
}
