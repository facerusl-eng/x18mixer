using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WpfMixer.Views;

/// <summary>
/// Segmented LED-style vertical meter.
/// Bind <see cref="Level"/> to a 0.0–1.0 value (updated from audio engine or OSC meter).
/// </summary>
public partial class LedMeterControl : UserControl
{
    private const int Segments = 32;
    private const double SegmentGap = 1.5;

    private readonly Rectangle[] _leds = new Rectangle[Segments];

    // ── Level dependency property ─────────────────────────────────────────
    public static readonly DependencyProperty LevelProperty =
        DependencyProperty.Register(nameof(Level), typeof(double), typeof(LedMeterControl),
            new PropertyMetadata(0.0, OnLevelChanged));

    public double Level
    {
        get => (double)GetValue(LevelProperty);
        set => SetValue(LevelProperty, Math.Clamp(value, 0.0, 1.0));
    }

    private static void OnLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((LedMeterControl)d).UpdateLeds();

    // ── Init ──────────────────────────────────────────────────────────────
    public LedMeterControl()
    {
        InitializeComponent();
        Loaded += (_, _) => BuildLeds();
        SizeChanged += (_, _) => BuildLeds();
    }

    private void BuildLeds()
    {
        MeterCanvas.Children.Clear();
        double totalH = MeterCanvas.ActualHeight;
        if (totalH < 1) return;

        double segH = (totalH - (Segments - 1) * SegmentGap) / Segments;
        segH = Math.Max(segH, 2);

        for (int i = 0; i < Segments; i++)
        {
            var rect = new Rectangle
            {
                Width = MeterCanvas.ActualWidth,
                Height = segH,
                Fill = OffColor(i),
                RadiusX = 1, RadiusY = 1,
            };
            double y = totalH - (i + 1) * (segH + SegmentGap);
            Canvas.SetTop(rect, y);
            Canvas.SetLeft(rect, 0);
            MeterCanvas.Children.Add(rect);
            _leds[i] = rect;
        }

        UpdateLeds();
    }

    private void UpdateLeds()
    {
        int lit = (int)(Level * Segments);
        for (int i = 0; i < Segments && i < _leds.Length; i++)
        {
            if (_leds[i] == null) continue;
            _leds[i].Fill = i < lit ? OnColor(i) : OffColor(i);
        }
    }

    // i=0 is bottom (green), i=Segments-1 is top (red)
    private static Brush OnColor(int i)
    {
        if (i >= Segments - 3) return new SolidColorBrush(Color.FromRgb(0xFF, 0x17, 0x44));  // red
        if (i >= Segments - 8) return new SolidColorBrush(Color.FromRgb(0xFF, 0xD6, 0x00));  // yellow
        return new SolidColorBrush(Color.FromRgb(0x00, 0xC8, 0x53));                          // green
    }

    private static Brush OffColor(int i)
    {
        if (i >= Segments - 3) return new SolidColorBrush(Color.FromRgb(0x3A, 0x0A, 0x0A));
        if (i >= Segments - 8) return new SolidColorBrush(Color.FromRgb(0x3A, 0x30, 0x00));
        return new SolidColorBrush(Color.FromRgb(0x0A, 0x2A, 0x0A));
    }
}
