using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfMixer.Core.Interfaces;
using WpfMixer.ViewModels;

namespace WpfMixer;

public partial class MainWindow : Window
{
    private readonly MixerViewModel _vm;
    private readonly IAppLifecycleService _lifecycle;
    private readonly ILoggingService _logging;
    private bool _shutdownStarted;

    public MainWindow(
        MixerViewModel vm,
        IAppLifecycleService lifecycle,
        ILoggingService logging)
    {
        _vm = vm;
        _lifecycle = lifecycle;
        _logging = logging;

        InitializeComponent();
        DataContext = _vm;

        Loaded += async (_, _) =>
        {
            try
            {
                await _lifecycle.StartupAsync(_vm);
            }
            catch (Exception ex)
            {
                _logging.LogError("Startup flow failed", ex);
            }
        };
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

    private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_shutdownStarted) return;
        _shutdownStarted = true;
        e.Cancel = true;

        try
        {
            await _lifecycle.ShutdownAsync(_vm);
        }
        finally
        {
            e.Cancel = false;
            Close();
        }
    }

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
