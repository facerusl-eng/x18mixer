using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WpfMixer.ViewModels;

namespace WpfMixer.Views;

public partial class PerformanceView : UserControl
{
    private DispatcherTimer? _longPressTimer;
    private PerformanceChannelViewModel? _pendingLongPressChannel;

    public PerformanceView()
    {
        InitializeComponent();
    }

    private void MixerScroll_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
    {
        if (DataContext is not PerformanceViewModel vm) return;
        if (sender is not ScrollViewer scrollViewer) return;

        var isTwoFinger = e.Manipulators.Count() >= 2;
        if (!isTwoFinger) return;

        // Two-finger swipe to scroll horizontally.
        scrollViewer.ScrollToHorizontalOffset(Math.Max(0, scrollViewer.HorizontalOffset - e.DeltaManipulation.Translation.X));

        // Pinch to zoom fader strips.
        var scale = e.DeltaManipulation.Scale.X;
        if (!double.IsNaN(scale) && !double.IsInfinity(scale) && Math.Abs(scale - 1.0) > 0.001)
            vm.ApplyZoomDelta(scale);

        e.Handled = true;
    }

    private void ChannelStrip_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not PerformanceChannelViewModel channelVm) return;

        // Double-tap resets the fader to 0 dB reference.
        if (e.ClickCount >= 2)
        {
            channelVm.ResetFaderCommand.Execute(null);
            e.Handled = true;
            return;
        }

        channelVm.SelectChannelCommand.Execute(null);

        // Long-press opens quick rename prompt.
        _pendingLongPressChannel = channelVm;
        _longPressTimer?.Stop();
        _longPressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _longPressTimer.Tick += (_, _) =>
        {
            _longPressTimer?.Stop();
            if (_pendingLongPressChannel is null) return;
            ShowRenamePrompt(_pendingLongPressChannel);
            _pendingLongPressChannel = null;
        };
        _longPressTimer.Start();
    }

    private void ChannelStrip_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _longPressTimer?.Stop();
        _pendingLongPressChannel = null;
    }

    private void ChannelStrip_MouseLeave(object sender, MouseEventArgs e)
    {
        _longPressTimer?.Stop();
        _pendingLongPressChannel = null;
    }

    private static void ShowRenamePrompt(PerformanceChannelViewModel channelVm)
    {
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

        var dialog = new Window
        {
            Title = "Rename Channel",
            Width = 360,
            Height = 160,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner,
            Background = System.Windows.Media.Brushes.Black,
            Foreground = System.Windows.Media.Brushes.White,
            Content = BuildRenameDialogContent(channelVm)
        };

        dialog.ShowDialog();
    }

    private static UIElement BuildRenameDialogContent(PerformanceChannelViewModel channelVm)
    {
        var root = new Grid { Margin = new Thickness(12) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var label = new TextBlock { Text = $"Rename {channelVm.Name}", Margin = new Thickness(0, 0, 0, 8), FontSize = 14 };
        Grid.SetRow(label, 0);

        var input = new TextBox
        {
            Text = channelVm.Name,
            FontSize = 16,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(input, 1);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", Width = 88, Margin = new Thickness(0, 0, 8, 0) };
        var ok = new Button { Content = "Save", Width = 88 };

        cancel.Click += (_, _) => Window.GetWindow(root)?.Close();
        ok.Click += (_, _) =>
        {
            channelVm.RenameChannelCommand.Execute(input.Text);
            Window.GetWindow(root)?.Close();
        };

        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);

        Grid.SetRow(buttons, 2);

        root.Children.Add(label);
        root.Children.Add(input);
        root.Children.Add(buttons);

        return root;
    }
}
