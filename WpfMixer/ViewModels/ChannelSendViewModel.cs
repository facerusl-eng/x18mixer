using CommunityToolkit.Mvvm.ComponentModel;
using WpfMixer.Models;
using WpfMixer.Services;

namespace WpfMixer.ViewModels;

public sealed partial class ChannelSendViewModel : ObservableObject
{
    private readonly OscClient _osc;
    private readonly Channel _channel;
    private readonly BusMixModel _bus;
    private readonly int _busIndex;
    private bool _suppress;
    private int _levelAnimationVersion;

    public int ChannelIndex => _channel.XAirIndex;
    public string ChannelName => _channel.Name;
    public string ColorHex => _channel.ColorHex;

    [ObservableProperty] private float _sendLevel;
    [ObservableProperty] private bool _sendOn;
    [ObservableProperty] private bool _prePost;

    public ChannelSendViewModel(Channel channel, BusMixModel bus, int busIndex, OscClient osc)
    {
        _channel = channel;
        _bus = bus;
        _busIndex = busIndex;
        _osc = osc;

        _sendLevel = bus.ChannelSendLevels.TryGetValue(channel.XAirIndex, out var lv) ? lv : 0f;
        _sendOn = bus.ChannelSendOn.TryGetValue(channel.XAirIndex, out var on) && on;
        _prePost = bus.ChannelSendPre.TryGetValue(channel.XAirIndex, out var pre) && pre;
    }

    partial void OnSendLevelChanged(float value)
    {
        var clamped = Math.Clamp(value, 0f, 1f);
        if (!_suppress && clamped != value)
        {
            _suppress = true;
            SendLevel = clamped;
            _suppress = false;
            return;
        }

        _bus.ChannelSendLevels[ChannelIndex] = clamped;
        if (_suppress) return;
        _osc.Send($"/ch/{ChannelIndex:D2}/mix/{_busIndex:D2}/level", clamped);
    }

    partial void OnSendOnChanged(bool value)
    {
        _bus.ChannelSendOn[ChannelIndex] = value;
        if (_suppress) return;
        _osc.Send($"/ch/{ChannelIndex:D2}/mix/{_busIndex:D2}/on", value ? 1 : 0);
    }

    partial void OnPrePostChanged(bool value)
    {
        _bus.ChannelSendPre[ChannelIndex] = value;
        if (_suppress) return;
        _osc.Send($"/ch/{ChannelIndex:D2}/mix/{_busIndex:D2}/pre", value ? 1 : 0);
    }

    public void ApplyFromOsc(float? level, bool? on, bool? pre)
    {
        if (level.HasValue)
            AnimateLevelFromOsc(Math.Clamp(level.Value, 0f, 1f));

        _suppress = true;
        try
        {
            if (on.HasValue) SendOn = on.Value;
            if (pre.HasValue) PrePost = pre.Value;
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
            float current = SendLevel;
            for (int i = 0; i < 12 && version == _levelAnimationVersion; i++)
            {
                current += (target - current) * 0.35f;
                if (Math.Abs(target - current) < 0.0025f)
                {
                    SendLevel = target;
                    return;
                }

                SendLevel = current;
                await Task.Delay(16);
            }

            if (version == _levelAnimationVersion)
                SendLevel = target;
        }
        finally
        {
            if (version == _levelAnimationVersion)
                _suppress = false;
        }
    }
}
