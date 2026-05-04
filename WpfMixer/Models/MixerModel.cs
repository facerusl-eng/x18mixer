using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfMixer.Models;

/// <summary>Top-level mixer state – matches the X Air X18 layout.</summary>
public class MixerModel : ObservableObject
{
    public string MixerName { get; set; } = "X Air X18";
    public string MixerIp { get; set; } = string.Empty;

    /// <summary>Input channels 1-18.</summary>
    public ObservableCollection<Channel> InputChannels { get; set; } = [];

    /// <summary>FX return buses (Bus 1-4).</summary>
    public ObservableCollection<Channel> FxReturns { get; set; } = [];

    /// <summary>Main LR strip.</summary>
    public Channel MainLR { get; set; } = new Channel
    {
        Name = "MAIN",
        XAirIndex = 99,
        Type = ChannelType.MainLR,
        ColorHex = "#FFFFFFFF",
        Volume = 0.75,
    };

    public ObservableCollection<MuteGroup> MuteGroups { get; set; } = [];

    /// <summary>All channels in display order: inputs + FX returns + main.</summary>
    [JsonIgnore]
    public IEnumerable<Channel> AllChannels =>
        InputChannels.Concat(FxReturns).Append(MainLR);

    // ── Factory ────────────────────────────────────────────────────────────
    public static MixerModel CreateDefault()
    {
        var model = new MixerModel();

        var colors = new[]
        {
            "#FF2196F3","#FF4CAF50","#FFFF9800","#FFE91E63",
            "#FF9C27B0","#FF00BCD4","#FFCDDC39","#FFFF5722",
            "#FF795548","#FF607D8B","#FF3F51B5","#FF009688",
            "#FFFFF000","#FF8BC34A","#FFFF4081","#FF00E5FF",
            "#FF69F0AE","#FFFF6D00",
        };

        for (int i = 1; i <= 18; i++)
        {
            model.InputChannels.Add(new Channel
            {
                Name = $"Ch {i:D2}",
                XAirIndex = i,
                Type = i % 2 == 0 ? ChannelType.Stereo : ChannelType.Mono,
                ColorHex = colors[(i - 1) % colors.Length],
                Volume = 0.75,
            });
        }

        for (int i = 1; i <= 4; i++)
        {
            model.FxReturns.Add(new Channel
            {
                Name = $"FX {i}",
                XAirIndex = 100 + i,
                Type = ChannelType.FxReturn,
                ColorHex = "#FF7B1FA2",
                Volume = 0.75,
            });
        }

        return model;
    }
}
