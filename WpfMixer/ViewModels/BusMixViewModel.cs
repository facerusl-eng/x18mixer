using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfMixer.Models;
using WpfMixer.Services;

namespace WpfMixer.ViewModels;

public sealed partial class BusMixViewModel : ObservableObject
{
    private readonly MixerModel _mixer;
    private readonly OscClient _osc;
    private bool _suppressBusMasterSend;
    private int _busMasterAnimationVersion;

    public ObservableCollection<BusMixModel> BusMixModels { get; } = [];
    public ObservableCollection<FxReturnMixModel> FxReturnMixModels { get; } = [];
    public ObservableCollection<FxReturnMixSlotViewModel> FxReturnMixSlots { get; } = [];

    public ObservableCollection<ChannelSendViewModel> ChannelSends { get; } = [];

    [ObservableProperty] private int _selectedBusIndex = 1;
    [ObservableProperty] private float _busMasterLevel = 0.75f;
    [ObservableProperty] private bool _busMasterMute;
    [ObservableProperty] private bool _isSofActive;

    public BusMixViewModel(MixerModel mixer, OscClient osc)
    {
        _mixer = mixer;
        _osc = osc;

        foreach (var bus in mixer.BusMixModels)
            BusMixModels.Add(bus);
        foreach (var fx in mixer.FxReturnMixModels)
        {
            FxReturnMixModels.Add(fx);
            FxReturnMixSlots.Add(new FxReturnMixSlotViewModel(fx, _osc));
        }

        RebuildChannelSends();
        SyncSelectedBusMaster();
    }

    partial void OnSelectedBusIndexChanged(int value)
    {
        if (value < 1 || value > 6) return;
        RebuildChannelSends();
        SyncSelectedBusMaster();
    }

    partial void OnBusMasterLevelChanged(float value)
    {
        var model = BusMixModels.FirstOrDefault(b => b.BusIndex == SelectedBusIndex);
        if (model is null) return;
        var clamped = Math.Clamp(value, 0f, 1f);
        model.BusMasterLevel = clamped;
        if (_suppressBusMasterSend) return;
        _osc.Send($"/bus/{SelectedBusIndex:D2}/mix/fader", clamped);
    }

    partial void OnBusMasterMuteChanged(bool value)
    {
        var model = BusMixModels.FirstOrDefault(b => b.BusIndex == SelectedBusIndex);
        if (model is null) return;
        model.BusMasterMute = value;
        _osc.Send($"/bus/{SelectedBusIndex:D2}/mix/on", value ? 0 : 1);
    }

    [RelayCommand]
    private void SelectBus(object? busIndex)
    {
        var index = busIndex switch
        {
            int i => i,
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => SelectedBusIndex
        };

        SelectedBusIndex = Math.Clamp(index, 1, 6);
    }

    [RelayCommand]
    private void EnterSof()
    {
        IsSofActive = true;
    }

    [RelayCommand]
    private void ExitSof()
    {
        IsSofActive = false;
    }

    public void RequestState()
    {
        foreach (var bus in BusMixModels)
        {
            _osc.Send($"/bus/{bus.BusIndex:D2}/mix/fader");
            _osc.Send($"/bus/{bus.BusIndex:D2}/mix/on");

            for (int ch = 1; ch <= 18; ch++)
            {
                _osc.Send($"/ch/{ch:D2}/mix/{bus.BusIndex:D2}/level");
                _osc.Send($"/ch/{ch:D2}/mix/{bus.BusIndex:D2}/on");
                _osc.Send($"/ch/{ch:D2}/mix/{bus.BusIndex:D2}/pre");
            }
        }

        for (int fx = 1; fx <= 4; fx++)
        {
            _osc.Send($"/fxr/{fx:D2}/mix/fader");
            _osc.Send($"/fxr/{fx:D2}/mix/on");
        }
    }

    public bool ApplyOscMessage(string address, object[] args)
    {
        if (address.StartsWith("/bus/", StringComparison.Ordinal))
        {
            var parts = address.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4 && int.TryParse(parts[1], out int busIdx))
            {
                var bus = BusMixModels.FirstOrDefault(b => b.BusIndex == busIdx);
                if (bus is null) return true;

                if (address.EndsWith("/mix/fader", StringComparison.Ordinal) && args.Length > 0)
                {
                    float f = args[0] is float ff ? ff : args[0] is int fi ? fi : bus.BusMasterLevel;
                    bus.BusMasterLevel = Math.Clamp(f, 0f, 1f);
                    if (busIdx == SelectedBusIndex) AnimateBusMasterFromOsc(bus.BusMasterLevel);
                    return true;
                }

                if (address.EndsWith("/mix/on", StringComparison.Ordinal) && args.Length > 0)
                {
                    bool muted = args[0] is int i ? i == 0 : args[0] is bool b && !b;
                    bus.BusMasterMute = muted;
                    if (busIdx == SelectedBusIndex) BusMasterMute = muted;
                    return true;
                }
            }
            return true;
        }

        if (address.StartsWith("/fxr/", StringComparison.Ordinal))
        {
            var parts = address.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4 && int.TryParse(parts[1], out int fxIdx) && fxIdx >= 1 && fxIdx <= 4)
            {
                var fx = FxReturnMixModels.FirstOrDefault(f => f.FxIndex == fxIdx);
                var fxVm = FxReturnMixSlots.FirstOrDefault(f => f.FxIndex == fxIdx);
                if (fx is null || fxVm is null) return true;

                if (address.EndsWith("/mix/fader", StringComparison.Ordinal) && args.Length > 0)
                {
                    float f = args[0] is float ff ? ff : args[0] is int fi ? fi : fx.BusMasterLevel;
                    fx.BusMasterLevel = Math.Clamp(f, 0f, 1f);
                    fxVm.SetFromOsc(fx.BusMasterLevel, fx.BusMasterMute);
                }
                else if (address.EndsWith("/mix/on", StringComparison.Ordinal) && args.Length > 0)
                {
                    fx.BusMasterMute = args[0] is int i ? i == 0 : args[0] is bool b && !b;
                    fxVm.SetFromOsc(fx.BusMasterLevel, fx.BusMasterMute);
                }
            }
            return true;
        }

        if (address.StartsWith("/ch/", StringComparison.Ordinal) && address.Contains("/mix/", StringComparison.Ordinal))
        {
            var parts = address.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 6 && int.TryParse(parts[1], out int channelIndex) && int.TryParse(parts[3], out int busIndex))
            {
                var bus = BusMixModels.FirstOrDefault(b => b.BusIndex == busIndex);
                if (bus is null) return false;

                float? level = null;
                bool? on = null;
                bool? pre = null;

                if (parts[4] == "level" && args.Length > 0)
                    level = args[0] is float lf ? lf : args[0] is int li ? li : 0f;
                else if (parts[4] == "on" && args.Length > 0)
                    on = args[0] is int oi ? oi == 1 : args[0] is bool ob && ob;
                else if (parts[4] == "pre" && args.Length > 0)
                    pre = args[0] is int pi ? pi == 1 : args[0] is bool pb && pb;
                else
                    return false;

                if (level.HasValue) bus.ChannelSendLevels[channelIndex] = Math.Clamp(level.Value, 0f, 1f);
                if (on.HasValue) bus.ChannelSendOn[channelIndex] = on.Value;
                if (pre.HasValue) bus.ChannelSendPre[channelIndex] = pre.Value;

                if (busIndex == SelectedBusIndex)
                {
                    var vm = ChannelSends.FirstOrDefault(c => c.ChannelIndex == channelIndex);
                    vm?.ApplyFromOsc(level, on, pre);
                }

                return true;
            }
        }

        return false;
    }

    private void RebuildChannelSends()
    {
        ChannelSends.Clear();
        var bus = BusMixModels.First(b => b.BusIndex == SelectedBusIndex);
        foreach (var channel in _mixer.InputChannels.OrderBy(c => c.XAirIndex).Take(18))
            ChannelSends.Add(new ChannelSendViewModel(channel, bus, SelectedBusIndex, _osc));
    }

    private void SyncSelectedBusMaster()
    {
        var bus = BusMixModels.FirstOrDefault(b => b.BusIndex == SelectedBusIndex);
        if (bus is null) return;
        BusMasterLevel = bus.BusMasterLevel;
        BusMasterMute = bus.BusMasterMute;
    }

    private async void AnimateBusMasterFromOsc(float target)
    {
        int version = ++_busMasterAnimationVersion;
        _suppressBusMasterSend = true;
        try
        {
            await FaderSmoothing.AnimateAsync(
                getCurrent: () => BusMasterLevel,
                setValue: v => BusMasterLevel = v,
                target: target,
                isCancelled: () => version != _busMasterAnimationVersion);
        }
        finally
        {
            if (version == _busMasterAnimationVersion)
                _suppressBusMasterSend = false;
        }
    }
}

public sealed partial class FxReturnMixSlotViewModel : ObservableObject
{
    private readonly FxReturnMixModel _model;
    private readonly OscClient _osc;
    private bool _suppress;
    private int _levelAnimationVersion;

    public int FxIndex => _model.FxIndex;

    [ObservableProperty] private float _level;
    [ObservableProperty] private bool _mute;

    public FxReturnMixSlotViewModel(FxReturnMixModel model, OscClient osc)
    {
        _model = model;
        _osc = osc;
        _level = model.BusMasterLevel;
        _mute = model.BusMasterMute;
    }

    partial void OnLevelChanged(float value)
    {
        var clamped = Math.Clamp(value, 0f, 1f);
        if (!_suppress && clamped != value)
        {
            _suppress = true;
            Level = clamped;
            _suppress = false;
            return;
        }

        _model.BusMasterLevel = clamped;
        if (_suppress) return;
        _osc.Send($"/fxr/{FxIndex:D2}/mix/fader", clamped);
    }

    partial void OnMuteChanged(bool value)
    {
        _model.BusMasterMute = value;
        if (_suppress) return;
        _osc.Send($"/fxr/{FxIndex:D2}/mix/on", value ? 0 : 1);
    }

    public void SetFromOsc(float level, bool mute)
    {
        AnimateLevelFromOsc(Math.Clamp(level, 0f, 1f));

        _suppress = true;
        try
        {
            Mute = mute;
        }
        finally
        {
            _suppress = false;
        }
    }

    private async void AnimateLevelFromOsc(float target)
    {
        int version = ++_levelAnimationVersion;
        _suppress = true;
        try
        {
            await FaderSmoothing.AnimateAsync(
                getCurrent: () => Level,
                setValue: v => Level = v,
                target: target,
                isCancelled: () => version != _levelAnimationVersion);
        }
        finally
        {
            if (version == _levelAnimationVersion)
                _suppress = false;
        }
    }
}
