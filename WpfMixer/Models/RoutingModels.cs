using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfMixer.Models;

public enum InputSource { Off, Analog, UsbReturn }
public enum PrePost { Post, Pre }
public enum OutputSource { Main, Bus1, Bus2, Bus3, Bus4, Bus5, Bus6, Fx1, Fx2, Fx3, Fx4, Usb, DirectOut }
public enum UsbMode { Stereo, Multitrack }

/// <summary>One bus or FX send slot for a channel.</summary>
public class BusSend : ObservableObject
{
    /// <summary>Bus index: 1-6 = mix buses, 7-10 = FX 1-4</summary>
    public int BusIndex { get; init; }
    public string Label { get; init; } = "";

    private double _level = 0.75;
    public double Level { get => _level; set => SetProperty(ref _level, Math.Clamp(value, 0.0, 1.0)); }

    private bool _isOn;
    public bool IsOn { get => _isOn; set => SetProperty(ref _isOn, value); }

    private PrePost _prePost = PrePost.Post;
    public PrePost PrePost { get => _prePost; set => SetProperty(ref _prePost, value); }

    [JsonIgnore] public bool IsPre => PrePost == PrePost.Pre;
    [JsonIgnore] public bool IsPost => PrePost == PrePost.Post;

    /// <summary>OSC bus address token e.g. "01", "02", "fx1"</summary>
    [JsonIgnore]
    public string OscToken => BusIndex <= 6 ? $"{BusIndex:D2}" : $"fx{BusIndex - 6}";
}

/// <summary>Physical output routing (Aux 1-6, Main LR, Phones).</summary>
public class OutputRoute : ObservableObject
{
    public int OutputIndex { get; init; }   // 1 = Aux1 … 6 = Aux6, 7 = Phones, 8 = Main
    public string Label { get; init; } = "";

    private OutputSource _source = OutputSource.Main;
    public OutputSource Source { get => _source; set => SetProperty(ref _source, value); }

    private double _level = 0.75;
    public double Level { get => _level; set => SetProperty(ref _level, Math.Clamp(value, 0.0, 1.0)); }

    /// <summary>OSC source integer: X Air /outputs/XX/src uses 0-based enum</summary>
    [JsonIgnore] public int OscSourceIndex => (int)Source;
    [JsonIgnore] public string OscBase => $"/outputs/{OutputIndex:D2}";
}

/// <summary>USB routing configuration.</summary>
public class UsbConfig : ObservableObject
{
    private UsbMode _mode = UsbMode.Multitrack;
    public UsbMode Mode { get => _mode; set => SetProperty(ref _mode, value); }

    /// <summary>Which channel feeds each USB send slot (index = USB slot 0-17, value = channel 1-18 or 0=off)</summary>
    public int[] SendAssignments { get; set; } = new int[18];

    /// <summary>Which USB return slot feeds which channel (index = channel 0-17, value = USB slot 1-18 or 0=off)</summary>
    public int[] ReturnAssignments { get; set; } = new int[18];
}
