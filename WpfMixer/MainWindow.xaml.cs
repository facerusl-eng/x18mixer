using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WpfMixer.ViewModels;
using WpfMixer.Views;

namespace WpfMixer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is System.Windows.Controls.TextBox)
            return;
        if (_vm.KeyboardService.HandleKeyDown(e.Key))
            e.Handled = true;
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (_vm.KeyboardService.HandleKeyUp(e.Key))
            e.Handled = true;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _vm.Cleanup();
    }

    private void ShowKeyMap_Click(object sender, RoutedEventArgs e)
    {
        new KeyMapWindow(_vm).ShowDialog();
    }

    private void ExportProfile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "Key Profile (*.json)|*.json", FileName = "keyboard-profile.json" };
        if (dlg.ShowDialog() == true) _vm.ExportProfileCommand.Execute(dlg.FileName);
    }

    private void ImportProfile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Key Profile (*.json)|*.json" };
        if (dlg.ShowDialog() == true) _vm.ImportProfileCommand.Execute(dlg.FileName);
    }
}