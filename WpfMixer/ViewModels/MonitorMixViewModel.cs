using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WpfMixer.ViewModels;

public sealed partial class MonitorMixViewModel : ObservableObject
{
    private readonly BusMixViewModel _busMix;

    public ObservableCollection<ChannelSendViewModel> ChannelSends => _busMix.ChannelSends;

    [ObservableProperty] private int _selectedBusIndex;
    [ObservableProperty] private bool _myMixMode;

    public float BusMasterLevel
    {
        get => _busMix.BusMasterLevel;
        set => _busMix.BusMasterLevel = value;
    }

    public bool BusMasterMute
    {
        get => _busMix.BusMasterMute;
        set => _busMix.BusMasterMute = value;
    }

    public MonitorMixViewModel(BusMixViewModel busMix)
    {
        _busMix = busMix;
        _selectedBusIndex = busMix.SelectedBusIndex;

        _busMix.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BusMixViewModel.SelectedBusIndex))
                SelectedBusIndex = _busMix.SelectedBusIndex;
            if (e.PropertyName == nameof(BusMixViewModel.BusMasterLevel))
                OnPropertyChanged(nameof(BusMasterLevel));
            if (e.PropertyName == nameof(BusMixViewModel.BusMasterMute))
                OnPropertyChanged(nameof(BusMasterMute));
            if (e.PropertyName == nameof(BusMixViewModel.ChannelSends))
                OnPropertyChanged(nameof(ChannelSends));
        };
    }

    partial void OnSelectedBusIndexChanged(int value)
    {
        if (_busMix.SelectedBusIndex != value)
            _busMix.SelectedBusIndex = value;
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

        SelectedBusIndex = index;
    }
}
