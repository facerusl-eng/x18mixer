using CommunityToolkit.Mvvm.ComponentModel;
using WpfMixer.Models;
using WpfMixer.Services;

namespace WpfMixer.ViewModels;

public sealed partial class RoutingViewModel : ObservableObject
{
    public MixerModel Mixer { get; }
    public RoutingMatrixViewModel Matrix { get; }
    public OutputRoutingViewModel OutputRouting { get; }
    public UsbRoutingViewModel UsbRouting { get; }

    [ObservableProperty] private Channel? _selectedChannel;

    public RoutingViewModel(MixerModel mixer, OscClient osc)
    {
        Mixer = mixer;
        Matrix = new RoutingMatrixViewModel(mixer, osc);
        OutputRouting = new OutputRoutingViewModel(mixer, osc);
        UsbRouting = new UsbRoutingViewModel(mixer, osc);
        SelectedChannel = mixer.InputChannels.FirstOrDefault();
    }

    public void RequestState()
    {
        OutputRouting.RequestState();
        UsbRouting.RequestState();
    }

    public bool ApplyOscMessage(string address, object[] args)
    {
        if (OutputRouting.ApplyOscMessage(address, args)) return true;
        if (UsbRouting.ApplyOscMessage(address, args)) return true;
        return false;
    }

    public void RebuildMatrix() => Matrix.RebuildRows();
}
