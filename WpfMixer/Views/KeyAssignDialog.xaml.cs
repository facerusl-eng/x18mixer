using System.Windows;
using System.Windows.Input;

namespace WpfMixer.Views;

public partial class KeyAssignDialog : Window
{
    public string? SelectedKey { get; private set; }
    public bool IsMomentary { get; private set; }

    public KeyAssignDialog(string? currentKey, bool isMomentary)
    {
        InitializeComponent();
        SelectedKey = currentKey;
        MomentaryCheck.IsChecked = isMomentary;

        if (currentKey != null)
            KeyDisplay.Text = currentKey;

        Loaded += (_, _) => KeyCaptureArea.Focus();
    }

    private void KeyCaptureArea_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Ignore modifier-only presses
        if (e.Key is Key.LeftShift or Key.RightShift or
            Key.LeftCtrl or Key.RightCtrl or
            Key.LeftAlt or Key.RightAlt or
            Key.LWin or Key.RWin or Key.System)
            return;

        SelectedKey = e.Key.ToString();
        KeyDisplay.Text = SelectedKey;
        e.Handled = true;
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        IsMomentary = MomentaryCheck.IsChecked == true;
        DialogResult = true;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        SelectedKey = null;
        KeyDisplay.Text = "[press a key]";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
        => DialogResult = false;
}
