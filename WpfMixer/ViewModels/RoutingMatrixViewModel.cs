using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WpfMixer.Models;
using WpfMixer.Services;

namespace WpfMixer.ViewModels;

public sealed class RoutingMatrixViewModel
{
    private readonly MixerModel _mixer;
    private readonly OscClient _osc;

    public IReadOnlyList<string> ColumnHeaders { get; } =
    [
        "BUS 1", "BUS 2", "BUS 3", "BUS 4", "BUS 5", "BUS 6",
        "FX 1", "FX 2", "FX 3", "FX 4", "MAIN LR"
    ];

    public ObservableCollection<RoutingMatrixRowViewModel> Rows { get; } = [];

    public RoutingMatrixViewModel(MixerModel mixer, OscClient osc)
    {
        _mixer = mixer;
        _osc = osc;
        RebuildRows();
    }

    public void RebuildRows()
    {
        Rows.Clear();

        foreach (var channel in _mixer.InputChannels)
            Rows.Add(BuildRow(channel, prefix: "CH"));

        foreach (var fx in _mixer.FxReturns)
            Rows.Add(BuildRow(fx, prefix: "FXR"));

        for (int i = 0; i < _mixer.InputChannels.Count; i++)
            Rows.Add(BuildRow(_mixer.InputChannels[i], prefix: "USB"));
    }

    private RoutingMatrixRowViewModel BuildRow(Channel channel, string prefix)
    {
        var row = new RoutingMatrixRowViewModel($"{prefix} {channel.XAirIndex:D2}");

        for (int bus = 1; bus <= 10; bus++)
        {
            var send = channel.BusSends.FirstOrDefault(s => s.BusIndex == bus);
            if (send is null)
            {
                row.Cells.Add(new RoutingMatrixCellViewModel($"{bus}", false, 0f, false, false,
                    _ => { }, (_, _) => { }, _ => { }));
                continue;
            }

            bool supportsPre = bus <= 6;
            row.Cells.Add(new RoutingMatrixCellViewModel(
                send.Label,
                send.IsOn,
                (float)send.Level,
                supportsPre,
                send.IsPre,
                isOn =>
                {
                    send.IsOn = isOn;
                    _osc.Send($"{channel.OscBase}/mix/{send.OscToken}/on", isOn ? 1 : 0);
                },
                (level, isPre) =>
                {
                    send.Level = level;
                    _osc.Send($"{channel.OscBase}/mix/{send.OscToken}/level", level);
                    if (supportsPre)
                    {
                        send.PrePost = isPre ? PrePost.Pre : PrePost.Post;
                        _osc.Send($"{channel.OscBase}/mix/{send.OscToken}/pre", isPre ? 1 : 0);
                    }
                },
                isPre =>
                {
                    if (!supportsPre) return;
                    send.PrePost = isPre ? PrePost.Pre : PrePost.Post;
                    _osc.Send($"{channel.OscBase}/mix/{send.OscToken}/pre", isPre ? 1 : 0);
                }));
        }

        row.Cells.Add(new RoutingMatrixCellViewModel(
            "LR",
            channel.SendToLr,
            (float)channel.Pan,
            false,
            false,
            isOn =>
            {
                channel.SendToLr = isOn;
                _osc.Send($"{channel.OscBase}/mix/lr", isOn ? 1 : 0);
            },
            (level, _) =>
            {
                channel.Pan = level;
                _osc.Send($"{channel.OscBase}/mix/pan", level);
            },
            _ => { }));

        return row;
    }
}

public sealed class RoutingMatrixRowViewModel
{
    public string RowName { get; }
    public ObservableCollection<RoutingMatrixCellViewModel> Cells { get; } = [];

    public RoutingMatrixRowViewModel(string rowName)
    {
        RowName = rowName;
    }
}

public sealed partial class RoutingMatrixCellViewModel : ObservableObject
{
    private readonly Action<bool> _toggleAction;
    private readonly Action<float, bool> _levelAction;
    private readonly Action<bool> _prePostAction;

    public string Label { get; }
    public bool SupportsPrePost { get; }

    [ObservableProperty] private bool _isOn;
    [ObservableProperty] private float _level;
    [ObservableProperty] private bool _isPre;

    public RoutingMatrixCellViewModel(
        string label,
        bool isOn,
        float level,
        bool supportsPrePost,
        bool isPre,
        Action<bool> toggleAction,
        Action<float, bool> levelAction,
        Action<bool> prePostAction)
    {
        Label = label;
        SupportsPrePost = supportsPrePost;
        _isOn = isOn;
        _level = Math.Clamp(level, 0f, 1f);
        _isPre = isPre;
        _toggleAction = toggleAction;
        _levelAction = levelAction;
        _prePostAction = prePostAction;
    }

    partial void OnIsOnChanged(bool value) => _toggleAction(value);
    partial void OnLevelChanged(float value) => _levelAction(Math.Clamp(value, 0f, 1f), IsPre);
    partial void OnIsPreChanged(bool value) => _prePostAction(value);
}
