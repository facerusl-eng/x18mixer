using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfMixer.Models;
using WpfMixer.Services;

namespace WpfMixer.ViewModels;

public sealed partial class PerformanceViewModel : ObservableObject
{
    private readonly MixerModel _mixer;
    private readonly OscClient _osc;
    private readonly DispatcherTimer _autoZoomTimer;
    private readonly DispatcherTimer _panicFlashTimer;

    private readonly Dictionary<Channel, bool> _panicMuteSnapshot = [];
    private readonly Dictionary<int, bool> _panicBusSnapshot = [];

    public ObservableCollection<PerformanceChannelViewModel> Channels { get; } = [];
    public ObservableCollection<PerformanceChannelViewModel> VisibleChannels { get; } = [];

    [ObservableProperty] private PerformanceChannelViewModel? _selectedChannel;
    [ObservableProperty] private PerformanceChannelViewModel? _mainLrChannel;
    [ObservableProperty] private bool _isLocked;
    [ObservableProperty] private bool _isPanicActive;
    [ObservableProperty] private bool _isPanicBlink;
    [ObservableProperty] private bool _isSingerView;
    [ObservableProperty] private string _lockPinInput = string.Empty;
    [ObservableProperty] private double _zoomScale = 1.0;
    [ObservableProperty] private bool _keepSelectedVocalLive = true;

    [ObservableProperty] private int _singerMicChannelIndex = 1;
    [ObservableProperty] private int _singerBackingChannelIndex = 2;
    [ObservableProperty] private int _singerMonitorBusIndex = 1;

    public bool IsEngineerView => !IsSingerView;
    public bool AreControlsEnabled => !IsLocked;

    public string ActiveRoleLabel => IsSingerView ? "SINGER VIEW" : "ENGINEER VIEW";

    public Brush PanicBrush
    {
        get
        {
            if (!IsPanicActive) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3A0000"));
            var flashing = IsPanicBlink ? "#FFFF5A5A" : "#FFB30000";
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(flashing));
        }
    }

    public float SingerMonitorLevel
    {
        get => _mixer.BusMixModels.FirstOrDefault(b => b.BusIndex == SingerMonitorBusIndex)?.BusMasterLevel ?? 0.75f;
        set
        {
            var model = _mixer.BusMixModels.FirstOrDefault(b => b.BusIndex == SingerMonitorBusIndex);
            if (model is null) return;
            var clamped = Math.Clamp(value, 0f, 1f);
            model.BusMasterLevel = clamped;
            _osc.Send($"/bus/{SingerMonitorBusIndex:D2}/mix/fader", clamped);
            OnPropertyChanged();
        }
    }

    public bool SingerMonitorMute
    {
        get => _mixer.BusMixModels.FirstOrDefault(b => b.BusIndex == SingerMonitorBusIndex)?.BusMasterMute ?? false;
        set
        {
            var model = _mixer.BusMixModels.FirstOrDefault(b => b.BusIndex == SingerMonitorBusIndex);
            if (model is null) return;
            model.BusMasterMute = value;
            _osc.Send($"/bus/{SingerMonitorBusIndex:D2}/mix/on", value ? 0 : 1);
            OnPropertyChanged();
        }
    }

    public PerformanceViewModel(MixerModel mixer, OscClient osc)
    {
        _mixer = mixer;
        _osc = osc;

        foreach (var ch in _mixer.InputChannels)
            Channels.Add(new PerformanceChannelViewModel(ch, _osc, SelectChannelInternal));

        foreach (var fx in _mixer.FxReturns)
            Channels.Add(new PerformanceChannelViewModel(fx, _osc, SelectChannelInternal));

        MainLrChannel = new PerformanceChannelViewModel(_mixer.MainLR, _osc, SelectChannelInternal);

        _autoZoomTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _autoZoomTimer.Tick += (_, _) =>
        {
            _autoZoomTimer.Stop();
            if (SelectedChannel is not null)
                SelectedChannel.IsZoomed = false;
            SelectedChannel = null;
        };

        _panicFlashTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _panicFlashTimer.Tick += (_, _) =>
        {
            if (!IsPanicActive)
            {
                IsPanicBlink = false;
                _panicFlashTimer.Stop();
                return;
            }

            IsPanicBlink = !IsPanicBlink;
            OnPropertyChanged(nameof(PanicBrush));
        };

        RefreshVisibleChannels();
    }

    partial void OnIsSingerViewChanged(bool value)
    {
        OnPropertyChanged(nameof(IsEngineerView));
        OnPropertyChanged(nameof(ActiveRoleLabel));
        RefreshVisibleChannels();
    }

    partial void OnIsLockedChanged(bool value)
    {
        OnPropertyChanged(nameof(AreControlsEnabled));
    }

    partial void OnIsPanicActiveChanged(bool value)
    {
        if (value) _panicFlashTimer.Start();
        else
        {
            _panicFlashTimer.Stop();
            IsPanicBlink = false;
        }

        OnPropertyChanged(nameof(PanicBrush));
    }

    partial void OnSingerMicChannelIndexChanged(int value)
    {
        RefreshVisibleChannels();
    }

    partial void OnSingerBackingChannelIndexChanged(int value)
    {
        RefreshVisibleChannels();
    }

    partial void OnSingerMonitorBusIndexChanged(int value)
    {
        OnPropertyChanged(nameof(SingerMonitorLevel));
        OnPropertyChanged(nameof(SingerMonitorMute));
    }

    [RelayCommand]
    private void ToggleViewMode()
    {
        IsSingerView = !IsSingerView;
    }

    [RelayCommand]
    private void ToggleLock()
    {
        if (!IsLocked)
        {
            IsLocked = true;
            return;
        }

        TryUnlock();
    }

    [RelayCommand]
    private void TryUnlock()
    {
        // Simple local PIN for live protection, intentionally offline.
        if (LockPinInput == "2468")
        {
            IsLocked = false;
            LockPinInput = string.Empty;
            return;
        }

        MessageBox.Show("Incorrect PIN", "Unlock", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    [RelayCommand]
    private void TogglePanicMute()
    {
        if (!IsPanicActive)
        {
            ActivatePanic();
            return;
        }

        RestoreFromPanic();
    }

    [RelayCommand]
    private void SelectChannel(PerformanceChannelViewModel? channel)
    {
        if (channel is null) return;
        SelectChannelInternal(channel);
    }

    [RelayCommand]
    private void ResetFader(PerformanceChannelViewModel? channel)
    {
        if (channel is null) return;
        channel.ResetFaderCommand.Execute(null);
    }

    private void SelectChannelInternal(PerformanceChannelViewModel channel)
    {
        if (IsLocked) return;

        if (SelectedChannel is not null)
            SelectedChannel.IsZoomed = false;

        SelectedChannel = channel;
        channel.IsZoomed = true;

        _autoZoomTimer.Stop();
        _autoZoomTimer.Start();
    }

    public void ApplyZoomDelta(double deltaScale)
    {
        ZoomScale = Math.Clamp(ZoomScale * deltaScale, 0.8, 2.6);
    }

    private void RefreshVisibleChannels()
    {
        VisibleChannels.Clear();

        if (!IsSingerView)
        {
            foreach (var channel in Channels.Where(c => !c.IsMainLr))
                VisibleChannels.Add(channel);
            return;
        }

        var mic = Channels.FirstOrDefault(c => c.XAirIndex == SingerMicChannelIndex && !c.IsFxReturn);
        var backing = Channels.FirstOrDefault(c => c.XAirIndex == SingerBackingChannelIndex && !c.IsFxReturn);
        var fx = Channels.FirstOrDefault(c => c.IsFxReturn);

        if (mic is not null) VisibleChannels.Add(mic);
        if (backing is not null && backing != mic) VisibleChannels.Add(backing);
        if (fx is not null) VisibleChannels.Add(fx);
    }

    private void ActivatePanic()
    {
        _panicMuteSnapshot.Clear();
        _panicBusSnapshot.Clear();

        foreach (var bus in _mixer.BusMixModels)
        {
            _panicBusSnapshot[bus.BusIndex] = bus.BusMasterMute;
            bus.BusMasterMute = true;
            _osc.Send($"/bus/{bus.BusIndex:D2}/mix/on", 0);
        }

        foreach (var fx in _mixer.FxReturns.Select((channel, idx) => (channel, idx)))
        {
            _panicMuteSnapshot[fx.channel] = fx.channel.IsMuted;
            fx.channel.IsMuted = true;
            _osc.Send($"/fxr/{fx.idx + 1:D2}/mix/on", 0);
        }

        foreach (var channel in _mixer.InputChannels)
        {
            _panicMuteSnapshot[channel] = channel.IsMuted;

            if (KeepSelectedVocalLive && SelectedChannel is not null && ReferenceEquals(channel, SelectedChannel.Channel))
                continue;

            channel.IsMuted = true;
            _osc.Send($"/ch/{channel.XAirIndex:D2}/mix/on", 0);
        }

        IsPanicActive = true;
    }

    private void RestoreFromPanic()
    {
        foreach (var (busIndex, priorMuted) in _panicBusSnapshot)
        {
            var bus = _mixer.BusMixModels.FirstOrDefault(b => b.BusIndex == busIndex);
            if (bus is null) continue;
            bus.BusMasterMute = priorMuted;
            _osc.Send($"/bus/{busIndex:D2}/mix/on", priorMuted ? 0 : 1);
        }

        foreach (var (channel, priorMuted) in _panicMuteSnapshot)
        {
            channel.IsMuted = priorMuted;

            if (channel.Type == ChannelType.FxReturn)
            {
                var fxIndex = Math.Max(1, _mixer.FxReturns.IndexOf(channel) + 1);
                _osc.Send($"/fxr/{fxIndex:D2}/mix/on", priorMuted ? 0 : 1);
            }
            else if (channel.Type != ChannelType.MainLR)
            {
                _osc.Send($"/ch/{channel.XAirIndex:D2}/mix/on", priorMuted ? 0 : 1);
            }
        }

        IsPanicActive = false;
    }
}
