using CommunityToolkit.Mvvm.ComponentModel;
using WpfMixer.Models;
using WpfMixer.Services;

namespace WpfMixer.ViewModels;

/// <summary>MVVM wrapper around <see cref="MainBusModel"/> (Main L/R bus).</summary>
public sealed class MainBusViewModel : ObservableObject
{
    public MainBusModel Model { get; }
    private readonly OscClient _osc;
    private bool _suppressOsc;

    private static string OscBase => MainBusModel.OscBase;

    // ── Fader ─────────────────────────────────────────────────────────────────
    private float _faderLevel;
    public float FaderLevel
    {
        get => _faderLevel;
        set
        {
            value = Math.Clamp(value, 0f, 1f);
            if (!SetProperty(ref _faderLevel, value)) return;
            Model.FaderLevel = value;
            if (!_suppressOsc)
                _osc.Send($"{OscBase}/mix/fader", value);
        }
    }

    // ── Mute ──────────────────────────────────────────────────────────────────
    private bool _isMuted;
    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (!SetProperty(ref _isMuted, value)) return;
            Model.IsMuted = value;
            if (!_suppressOsc)
                _osc.Send($"{OscBase}/mix/on", value ? 0 : 1);
        }
    }

    // ── Meter ─────────────────────────────────────────────────────────────────
    private float _meterLevel;
    public float MeterLevel
    {
        get => _meterLevel;
        set { Model.MeterLevel = value; SetProperty(ref _meterLevel, value); }
    }

    // ── Incoming OSC ──────────────────────────────────────────────────────────
    public void ApplyOscMessage(string address, object[] args)
    {
        _suppressOsc = true;
        try
        {
            if (address == $"{OscBase}/mix/fader" && args is [float f])
                FaderLevel = f;
            else if (address == $"{OscBase}/mix/on" && args is [int on])
                IsMuted = on == 0;
        }
        finally { _suppressOsc = false; }
    }

    public MainBusViewModel(MainBusModel model, OscClient osc)
    {
        Model       = model;
        _osc        = osc;
        _faderLevel = model.FaderLevel;
        _isMuted    = model.IsMuted;
        _meterLevel = model.MeterLevel;
    }
}
