using CommunityToolkit.Mvvm.ComponentModel;
using WpfMixer.Models;
using WpfMixer.Services;

namespace WpfMixer.ViewModels;

/// <summary>
/// MVVM wrapper around <see cref="ChannelModel"/>.
/// Sends OSC when the user changes a value; suppresses OSC when the hardware
/// pushes an update so we don't echo it back.
/// </summary>
public sealed class ChannelViewModel : ObservableObject
{
    public ChannelModel Model { get; }
    private readonly OscClient _osc;
    private bool _suppressOsc;

    public int Index => Model.Index;
    public string OscBase => Model.OscBase;
    public string ColorHex => Model.ColorHex;

    // ── Name ──────────────────────────────────────────────────────────────────
    private string _name;
    public string Name
    {
        get => _name;
        set
        {
            if (!SetProperty(ref _name, value)) return;
            Model.Name = value;
            if (!_suppressOsc)
                _osc.Send($"{OscBase}/config/name", value);
        }
    }

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

    // ── Pan ───────────────────────────────────────────────────────────────────
    private float _pan;
    public float Pan
    {
        get => _pan;
        set
        {
            value = Math.Clamp(value, 0f, 1f);
            if (!SetProperty(ref _pan, value)) return;
            Model.Pan = value;
            if (!_suppressOsc)
                _osc.Send($"{OscBase}/mix/pan", value);
        }
    }

    // ── Mute ──────────────────────────────────────────────────────────────────
    // X Air convention: /mix/on  1 = live (unmuted), 0 = muted/off
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

    // ── Solo ──────────────────────────────────────────────────────────────────
    private bool _isSolo;
    public bool IsSolo
    {
        get => _isSolo;
        set
        {
            if (!SetProperty(ref _isSolo, value)) return;
            Model.IsSolo = value;
            if (!_suppressOsc)
                _osc.Send($"{OscBase}/mix/solo", value ? 1 : 0);
        }
    }

    // ── Meter (hardware only, never sends OSC) ────────────────────────────────
    private float _meterLevel;
    public float MeterLevel
    {
        get => _meterLevel;
        set { Model.MeterLevel = value; SetProperty(ref _meterLevel, value); }
    }

    // ── UI-only state (keyboard assignment, selection) ────────────────────────
    private string? _assignedKey;
    public string? AssignedKey
    {
        get => _assignedKey;
        set => SetProperty(ref _assignedKey, value);
    }

    private bool _isMomentaryMute;
    public bool IsMomentaryMute
    {
        get => _isMomentaryMute;
        set => SetProperty(ref _isMomentaryMute, value);
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private bool _isKeyHighlighted;
    public bool IsKeyHighlighted
    {
        get => _isKeyHighlighted;
        set => SetProperty(ref _isKeyHighlighted, value);
    }

    // ── Bus sends ─────────────────────────────────────────────────────────────
    public void SetBusSend(int busIndex, float level)
    {
        level = Math.Clamp(level, 0f, 1f);
        Model.BusSends[busIndex] = level;
        _osc.Send($"{OscBase}/mix/{busIndex:D2}/level", level);
    }

    // ── FX sends ──────────────────────────────────────────────────────────────
    public void SetFxSend(int fxIndex, float level)
    {
        level = Math.Clamp(level, 0f, 1f);
        Model.FxSends[fxIndex] = level;
        // FX buses are 07–10 on X Air (FX1=07 … FX4=10)
        int oscBus = 6 + fxIndex;
        _osc.Send($"{OscBase}/mix/{oscBus:D2}/level", level);
    }

    // ── Incoming OSC update (from hardware) ───────────────────────────────────
    public void ApplyOscMessage(string address, object[] args)
    {
        _suppressOsc = true;
        try
        {
            if (address == $"{OscBase}/mix/fader" && args is [float f])
                FaderLevel = f;
            else if (address == $"{OscBase}/mix/on" && args is [int on])
                IsMuted = on == 0;                   // 0 = muted, 1 = live
            else if (address == $"{OscBase}/mix/pan" && args is [float pan])
                Pan = pan;
            else if (address == $"{OscBase}/config/name" && args is [string name])
                SetNameFromOsc(name);
            else if (address == $"{OscBase}/mix/solo" && args is [int solo])
                IsSolo = solo == 1;
            else
                TryApplySendLevel(address, args);
        }
        finally
        {
            _suppressOsc = false;
        }
    }

    private void SetNameFromOsc(string name)
    {
        Model.Name = name;
        SetProperty(ref _name, name, nameof(Name));
    }

    private void TryApplySendLevel(string address, object[] args)
    {
        if (args is not [float level]) return;
        // /ch/XX/mix/YY/level  where YY 01-06 = bus, 07-10 = FX 1-4
        var prefix = $"{OscBase}/mix/";
        if (!address.StartsWith(prefix) || !address.EndsWith("/level")) return;
        var token = address[prefix.Length..address.LastIndexOf('/')];
        if (!int.TryParse(token, out int bus)) return;

        if (bus is >= 1 and <= 6)
            Model.BusSends[bus] = level;
        else if (bus is >= 7 and <= 10)
            Model.FxSends[bus - 6] = level;
    }

    // ── Constructor ───────────────────────────────────────────────────────────
    public ChannelViewModel(ChannelModel model, OscClient osc)
    {
        Model     = model;
        _osc      = osc;
        _name       = model.Name;
        _faderLevel = model.FaderLevel;
        _pan        = model.Pan;
        _isMuted    = model.IsMuted;
        _isSolo     = model.IsSolo;
        _meterLevel = model.MeterLevel;
    }
}
