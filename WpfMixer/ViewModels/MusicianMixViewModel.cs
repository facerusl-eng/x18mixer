using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WpfMixer.Models;
using WpfMixer.Services;

namespace WpfMixer.ViewModels;

/// <summary>
/// Represents one musician's mix configuration on the desktop side.
/// Shown in the Remote Control panel's musician list.
/// </summary>
public partial class MusicianMixViewModel : ObservableObject
{
    private readonly MusicianConfig _config;

    public string Id       => _config.Id;
    public string Name     => _config.Name;
    public string Slug     => _config.Slug;
    public int    BusIndex => _config.BusIndex;
    public string Color    => _config.Color;
    public IReadOnlyList<MusicianChannelConfig> Channels => _config.Channels;

    [ObservableProperty] private string _mixUrl    = string.Empty;
    [ObservableProperty] private bool   _isOnline;
    [ObservableProperty] private int    _clientCount;

    public MusicianMixViewModel(MusicianConfig config, string baseUrl)
    {
        _config = config;
        MixUrl  = config.BuildUrl(baseUrl);
    }

    public MusicianConfig Config => _config;

    public void RegenerateToken()
    {
        _config.Token = MusicianConfig.GenerateToken();
        OnPropertyChanged(nameof(MixUrl));
    }

    public void UpdateBaseUrl(string baseUrl)
    {
        MixUrl = _config.BuildUrl(baseUrl);
    }
}
