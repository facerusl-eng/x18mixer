using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfMixer.Models;

public class MuteGroup : ObservableObject
{
    private string _name = "Group";
    private string? _assignedKey;
    private bool _isMomentaryMute;
    private bool _isActive;
    private bool _isKeyHighlighted;

    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string? AssignedKey
    {
        get => _assignedKey;
        set => SetProperty(ref _assignedKey, value);
    }

    public bool IsMomentaryMute
    {
        get => _isMomentaryMute;
        set => SetProperty(ref _isMomentaryMute, value);
    }

    /// <summary>True when this group has toggled all its channels to muted.</summary>
    [JsonIgnore]
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    [JsonIgnore]
    public bool IsKeyHighlighted
    {
        get => _isKeyHighlighted;
        set => SetProperty(ref _isKeyHighlighted, value);
    }

    /// <summary>IDs of channels that belong to this group.</summary>
    public ObservableCollection<string> ChannelIds { get; set; } = [];

    /// <summary>Saved per-channel mute states before momentary group mute.</summary>
    [JsonIgnore]
    public Dictionary<string, bool> PreMomentaryStates { get; set; } = [];
}
