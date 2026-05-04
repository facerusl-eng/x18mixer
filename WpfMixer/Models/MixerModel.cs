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

    /// <summary>Physical output routing: aux 1-6, phones (7), main (8).</summary>
    public ObservableCollection<OutputRoute> Outputs { get; set; } = [];

    /// <summary>USB routing configuration.</summary>
    public UsbConfig Usb { get; set; } = new();

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
            var ch = new Channel
            {
                Name = $"Ch {i:D2}",
                XAirIndex = i,
                Type = i % 2 == 0 ? ChannelType.Stereo : ChannelType.Mono,
                ColorHex = colors[(i - 1) % colors.Length],
                Volume = 0.75,
                AnalogInput = i,
            };
            foreach (var s in CreateDefaultBusSends()) ch.BusSends.Add(s);
            model.InputChannels.Add(ch);
        }

        for (int i = 1; i <= 4; i++)
        {
            var ch = new Channel
            {
                Name = $"FX {i}",
                XAirIndex = 100 + i,
                Type = ChannelType.FxReturn,
                ColorHex = "#FF7B1FA2",
                Volume = 0.75,
            };
            foreach (var s in CreateDefaultBusSends()) ch.BusSends.Add(s);
            model.FxReturns.Add(ch);
        }

        model.MainLR.BusSends.Clear();
        foreach (var s in CreateDefaultBusSends()) model.MainLR.BusSends.Add(s);

        // Outputs
        for (int i = 1; i <= 6; i++)
            model.Outputs.Add(new OutputRoute { OutputIndex = i, Label = $"AUX {i}", Source = (OutputSource)(i - 1 < 6 ? OutputSource.Bus1 + i - 1 : OutputSource.Main) });
        model.Outputs.Add(new OutputRoute { OutputIndex = 7, Label = "PHONES", Source = OutputSource.Main });
        model.Outputs.Add(new OutputRoute { OutputIndex = 8, Label = "MAIN LR", Source = OutputSource.Main });

        return model;
    }

    private static IEnumerable<BusSend> CreateDefaultBusSends()
    {
        for (int b = 1; b <= 6; b++)
            yield return new BusSend { BusIndex = b, Label = $"BUS {b}" };
        for (int f = 1; f <= 4; f++)
            yield return new BusSend { BusIndex = 6 + f, Label = $"FX {f}" };
    }
}
