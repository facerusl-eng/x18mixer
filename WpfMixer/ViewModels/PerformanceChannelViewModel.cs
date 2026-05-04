using System.ComponentModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfMixer.Models;
using WpfMixer.Services;

namespace WpfMixer.ViewModels;

public sealed partial class PerformanceChannelViewModel : ObservableObject
{
    private readonly Channel _channel;
    private readonly OscClient _osc;
    private readonly Action<PerformanceChannelViewModel> _select;

    [ObservableProperty] private bool _isZoomed;

    public Channel Channel => _channel;
    public string Name => _channel.Name;
    public int XAirIndex => _channel.XAirIndex;
    public bool IsMainLr => _channel.Type == ChannelType.MainLR;
    public bool IsFxReturn => _channel.Type == ChannelType.FxReturn;
    public Brush Color => new SolidColorBrush(_channel.StripColor);

    public float FaderLevel
    {
        get => (float)_channel.Volume;
        set
        {
            _channel.Volume = Math.Clamp(value, 0f, 1f);
            OnPropertyChanged();
            OnPropertyChanged(nameof(MeterLevel));
        }
    }

    public float Pan
    {
        get => (float)_channel.Pan;
        set
        {
            _channel.Pan = Math.Clamp(value, 0f, 1f);
            OnPropertyChanged();
        }
    }

    public float MeterLevel
    {
        get => (float)Math.Clamp(_channel.MeterLevel <= 0 ? _channel.Volume : _channel.MeterLevel, 0.0, 1.0);
        set
        {
            _channel.MeterLevel = Math.Clamp(value, 0f, 1f);
            OnPropertyChanged();
        }
    }

    public bool IsMuted
    {
        get => _channel.IsMuted;
        set
        {
            _channel.IsMuted = value;
            OnPropertyChanged();
        }
    }

    public bool IsSolo
    {
        get => _channel.IsSoloed;
        set
        {
            _channel.IsSoloed = value;
            if (_channel.Type != ChannelType.MainLR)
                _osc.Send($"/ch/{XAirIndex:D2}/mix/solo", value ? 1 : 0);
            OnPropertyChanged();
        }
    }

    public PerformanceChannelViewModel(Channel channel, OscClient osc, Action<PerformanceChannelViewModel> select)
    {
        _channel = channel;
        _osc = osc;
        _select = select;
        _channel.PropertyChanged += OnChannelPropertyChanged;
    }

    private void OnChannelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Channel.Name):
                OnPropertyChanged(nameof(Name));
                break;
            case nameof(Channel.Volume):
                OnPropertyChanged(nameof(FaderLevel));
                OnPropertyChanged(nameof(MeterLevel));
                break;
            case nameof(Channel.Pan):
                OnPropertyChanged(nameof(Pan));
                break;
            case nameof(Channel.IsMuted):
                OnPropertyChanged(nameof(IsMuted));
                break;
            case nameof(Channel.IsSoloed):
                OnPropertyChanged(nameof(IsSolo));
                break;
            case nameof(Channel.MeterLevel):
                OnPropertyChanged(nameof(MeterLevel));
                break;
            case nameof(Channel.ColorHex):
                OnPropertyChanged(nameof(Color));
                break;
        }
    }

    [RelayCommand]
    private void SelectChannel()
    {
        _select(this);
    }

    [RelayCommand]
    private void ResetFader()
    {
        FaderLevel = 0.75f;
    }

    [RelayCommand]
    private void RenameChannel(string? nextName)
    {
        if (string.IsNullOrWhiteSpace(nextName)) return;
        _channel.Name = nextName.Trim();
    }
}
