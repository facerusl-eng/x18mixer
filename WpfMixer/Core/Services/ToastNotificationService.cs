using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WpfMixer.Core.Interfaces;

namespace WpfMixer.Core.Services;

public sealed class ToastNotificationService : IToastNotificationService
{
    public void ShowInfo(string message, int milliseconds = 2500) => Show(message, Colors.DodgerBlue, milliseconds);
    public void ShowWarning(string message, int milliseconds = 3500) => Show(message, Colors.DarkOrange, milliseconds);
    public void ShowError(string message, int milliseconds = 5000) => Show(message, Colors.IndianRed, milliseconds);

    private static void Show(string message, Color accent, int milliseconds)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            var owner = Application.Current.MainWindow;
            var toast = new Window
            {
                Width = 360,
                Height = 88,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Topmost = true,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Opacity = 0.96,
            };

            var root = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromRgb(24, 24, 24)),
                BorderBrush = new SolidColorBrush(accent),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 10, 12, 10),
                Child = new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12,
                },
            };

            toast.Content = root;
            toast.Loaded += (_, _) =>
            {
                if (owner is null) return;
                toast.Left = owner.Left + owner.Width - toast.Width - 20;
                toast.Top = owner.Top + owner.Height - toast.Height - 40;
            };

            toast.Show();

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                toast.Close();
            };
            timer.Start();
        }, DispatcherPriority.Background);
    }
}
