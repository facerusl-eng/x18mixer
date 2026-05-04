using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfMixer.Models;

public enum ChannelType { Mono, Stereo, FxReturn, MainLR }

public class Channel : ObservableObject
{
    // ── Identity ────────────────────────────────────────────────────────────
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>1-based index on the X Air bus (1-18 for inputs, 99 = main LR).</summary>
    public int XAirIndex { get; set; } = 1;

    public ChannelType Type { get; set; } = ChannelType.Mono;

    // ── Name / color ────────────────────────────────────────────────────────
    private string _name = "Ch";
    public string Name { get => _name; set => SetProperty(ref _name, value); }

    /// <summary>ARGB hex string, e.g. "#FF2196F3". Shown as color strip at top of channel.</summary>
    private string _colorHex = "#FF607D8B";
    public string ColorHex { get => _colorHex; set => SetProperty(ref _colorHex, value); }

    [JsonIgnore]
    public Color StripColor => (Color)ColorConverter.ConvertFromString(ColorHex);

    // ── Fader / pan ─────────────────────────────────────────────────────────
    private double _volume = 0.75;
    public double Volume
    {
        get => _volume;
        set => SetProperty(ref _volume, Math.Clamp(value, 0.0, 1.0));
    }

    private double _pan = 0.5;   // 0.0=L  0.5=center  1.0=R
    public double Pan { get => _pan; set => SetProperty(ref _pan, Math.Clamp(value, 0.0, 1.0)); }

    // ── Mute / solo ─────────────────────────────────────────────────────────
    private bool _isMuted;
    public bool IsMuted { get => _isMuted; set => SetProperty(ref _isMuted, value); }

    private bool _isSoloed;
    public bool IsSoloed { get => _isSoloed; set => SetProperty(ref _isSoloed, value); }

    private bool _isSelected;
    [JsonIgnore]
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    // ── FX sends (4 buses) ──────────────────────────────────────────────────
    public ObservableCollection<double> FxSends { get; set; } = [0.0, 0.0, 0.0, 0.0];

    // ── Routing: input source ────────────────────────────────────────────────
    private InputSource _inputSource = InputSource.Analog;
    public InputSource InputSource { get => _inputSource; set => SetProperty(ref _inputSource, value); }

    private int _analogInput = 1;   // 1-18
    public int AnalogInput { get => _analogInput; set => SetProperty(ref _analogInput, Math.Clamp(value, 1, 18)); }

    private int _usbReturn = 1;     // 1-18
    public int UsbReturn { get => _usbReturn; set => SetProperty(ref _usbReturn, Math.Clamp(value, 1, 18)); }

    // ── Routing: Main LR send ─────────────────────────────────────────────────
    private bool _sendToLr = true;
    public bool SendToLr { get => _sendToLr; set => SetProperty(ref _sendToLr, value); }

    // ── Routing: Direct out source ────────────────────────────────────────────
    private OutputSource _directOutSource = OutputSource.DirectOut;
    public OutputSource DirectOutSource { get => _directOutSource; set => SetProperty(ref _directOutSource, value); }

    // ── Routing: Bus + FX sends (10 total: bus 1-6, fx 1-4) ─────────────────
    public ObservableCollection<BusSend> BusSends { get; set; } = [];

    // ── Meter (real-time, not persisted) ─────────────────────────────────────
    [JsonIgnore] private double _meterLevel;
    [JsonIgnore]
    public double MeterLevel { get => _meterLevel; set => SetProperty(ref _meterLevel, value); }

    // ── Keyboard assignment ─────────────────────────────────────────────────
    private string? _assignedKey;
    public string? AssignedKey { get => _assignedKey; set => SetProperty(ref _assignedKey, value); }

    private bool _isMomentaryMute;
    public bool IsMomentaryMute { get => _isMomentaryMute; set => SetProperty(ref _isMomentaryMute, value); }

    [JsonIgnore] public bool IsKeyHighlighted { get => _isKeyHighlighted; set => SetProperty(ref _isKeyHighlighted, value); }
    private bool _isKeyHighlighted;

    [JsonIgnore] public bool PreMomentaryMutedState { get; set; }

    // ── OSC path helpers ─────────────────────────────────────────────────────
    [JsonIgnore]
    public string OscBase => Type == ChannelType.MainLR
        ? "/lr"
        : $"/ch/{XAirIndex:D2}";
}

