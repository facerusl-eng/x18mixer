using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WpfMixer.Views;

/// <summary>
/// Segmented LED-style vertical meter with smoothing, peak-hold, and clip LED.
/// Bind <see cref="Level"/> (0.0–1.0) and optionally <see cref="IsClipping"/>.
///
/// Rendering uses <c>CompositionTarget.Rendering</c> at the display refresh rate,
/// but updates are gated to ~40 fps via a frame counter so the meter runs
/// smoothly without hogging the thread at 60+fps.
/// </summary>
public partial class LedMeterControl : UserControl
{
    private const int    Segments    = 32;
    private const double SegmentGap  = 1.5;
    private const int    GreenEnd    = 24;   // segments 0-23 = green
    private const int    YellowEnd   = 29;   // segments 24-28 = yellow; 29-31 = red
    private const float  AttackDef   = 0.15f;
    private const float  ReleaseDef  = 0.08f;

    // pre-allocated brushes — avoids allocations in the render loop
    private static readonly SolidColorBrush[] _onBrushes  = new SolidColorBrush[Segments];
    private static readonly SolidColorBrush[] _offBrushes = new SolidColorBrush[Segments];

    static LedMeterControl()
    {
        for (int i = 0; i < Segments; i++)
        {
            (_onBrushes[i], _offBrushes[i]) = i >= YellowEnd
                ? (Frozen(0xFF, 0x17, 0x44), Frozen(0x3A, 0x0A, 0x0A))
                : i >= GreenEnd
                    ? (Frozen(0xFF, 0xD6, 0x00), Frozen(0x3A, 0x30, 0x00))
                    : (Frozen(0x00, 0xC8, 0x53), Frozen(0x0A, 0x2A, 0x0A));
        }

        static SolidColorBrush Frozen(byte r, byte g, byte b)
        {
            var b2 = new SolidColorBrush(Color.FromRgb(r, g, b));
            b2.Freeze();
            return b2;
        }
    }

    private static readonly SolidColorBrush _peakBrush =
        Freeze(new SolidColorBrush(Colors.White));
    private static readonly SolidColorBrush _clipOnBrush =
        Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x20, 0x20)));
    private static readonly SolidColorBrush _clipOffBrush =
        Freeze(new SolidColorBrush(Color.FromRgb(0x3A, 0x0A, 0x0A)));

    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

    // ── State ─────────────────────────────────────────────────────────────
    private readonly Rectangle[] _leds      = new Rectangle[Segments];
    private float  _smoothed      = 0f;
    private int    _peakSegment   = -1;
    private int    _peakHoldFrames= 0;
    private const int PeakHoldFrameCount = 80;   // ~2 s at 40 fps
    private bool   _registered    = false;
    private int    _frameCounter  = 0;

    // ── Dependency properties ─────────────────────────────────────────────
    public static readonly DependencyProperty LevelProperty =
        DependencyProperty.Register(nameof(Level), typeof(double), typeof(LedMeterControl),
            new PropertyMetadata(0.0));

    public double Level
    {
        get => (double)GetValue(LevelProperty);
        set => SetValue(LevelProperty, Math.Clamp(value, 0.0, 1.0));
    }

    public static readonly DependencyProperty IsClippingProperty =
        DependencyProperty.Register(nameof(IsClipping), typeof(bool), typeof(LedMeterControl),
            new PropertyMetadata(false));

    public bool IsClipping
    {
        get => (bool)GetValue(IsClippingProperty);
        set => SetValue(IsClippingProperty, value);
    }

    /// <summary>Exponential attack coefficient (0-1; lower = faster).</summary>
    public float Attack  { get; set; } = AttackDef;
    /// <summary>Exponential release coefficient (0-1; lower = faster decay).</summary>
    public float Release { get; set; } = ReleaseDef;

    // ── Init ──────────────────────────────────────────────────────────────
    public LedMeterControl()
    {
        InitializeComponent();
        Loaded       += (_, _) => { BuildLeds(); RegisterRendering(); };
        Unloaded     += (_, _) => UnregisterRendering();
        SizeChanged  += (_, _) => BuildLeds();
    }

    private void RegisterRendering()
    {
        if (_registered) return;
        CompositionTarget.Rendering += OnRendering;
        _registered = true;
    }

    private void UnregisterRendering()
    {
        if (!_registered) return;
        CompositionTarget.Rendering -= OnRendering;
        _registered = false;
    }

    // ── Render loop (60 fps delivery, gated to ~40 fps) ───────────────────
    private void OnRendering(object? sender, EventArgs e)
    {
        // Gate to every other frame → ~30-40 fps effective update
        if ((_frameCounter++ & 1) != 0) return;

        float raw    = (float)Level;
        float coeff  = raw > _smoothed ? Attack : Release;
        _smoothed    = _smoothed + (raw - _smoothed) * coeff;

        // Peak hold
        int lit = (int)(_smoothed * Segments);
        if (lit > _peakSegment)
        {
            _peakSegment    = lit;
            _peakHoldFrames = PeakHoldFrameCount;
        }
        else if (_peakHoldFrames > 0)
        {
            _peakHoldFrames--;
        }
        else if (_peakSegment > 0)
        {
            _peakSegment--;
        }

        UpdateLedsInternal(lit);
        UpdatePeakBar();
        UpdateClipLed();
    }

    // ── Visual updates ────────────────────────────────────────────────────
    private void UpdateLedsInternal(int lit)
    {
        for (int i = 0; i < Segments && i < _leds.Length; i++)
        {
            if (_leds[i] == null) continue;
            _leds[i].Fill = i < lit ? _onBrushes[i] : _offBrushes[i];
        }
    }

    private void UpdatePeakBar()
    {
        if (_peakSegment <= 0 || _peakSegment >= Segments)
        {
            PeakBar.Visibility = Visibility.Collapsed;
            return;
        }

        double totalH = MeterCanvas.ActualHeight;
        double segH   = (totalH - (Segments - 1) * SegmentGap) / Segments;
        segH = Math.Max(segH, 2);
        double y = totalH - (_peakSegment + 1) * (segH + SegmentGap);

        PeakBar.Margin     = new Thickness(0, y, 0, 0);
        PeakBar.Visibility = Visibility.Visible;
    }

    private void UpdateClipLed()
    {
        ClipLed.Fill = IsClipping ? _clipOnBrush : _clipOffBrush;
    }

    // ── LED construction ──────────────────────────────────────────────────
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
                Width    = MeterCanvas.ActualWidth,
                Height   = segH,
                Fill     = _offBrushes[i],
                RadiusX  = 1,
                RadiusY  = 1,
            };
            double y = totalH - (i + 1) * (segH + SegmentGap);
            Canvas.SetTop(rect, y);
            Canvas.SetLeft(rect, 0);
            MeterCanvas.Children.Add(rect);
            _leds[i] = rect;
        }
    }
}
