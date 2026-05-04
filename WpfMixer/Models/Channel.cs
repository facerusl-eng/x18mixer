using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfMixer.Models;

public class Channel : ObservableObject
{
    private string _name = "Channel";
    private bool _isMuted;
    private double _volume = 1.0;        // 0.0 – 1.0 fader position
    private string? _assignedKey;        // e.g. "A", "F1", "D1"
    private bool _isMomentaryMute;
    private bool _isKeyHighlighted;

    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool IsMuted
    {
        get => _isMuted;
        set => SetProperty(ref _isMuted, value);
    }

    public double Volume
    {
        get => _volume;
        set => SetProperty(ref _volume, Math.Clamp(value, 0.0, 1.0));
    }

    /// <summary>Key string as returned by Key.ToString() e.g. "A", "F1", "D1".</summary>
    public string? AssignedKey
    {
        get => _assignedKey;
        set => SetProperty(ref _assignedKey, value);
    }

    /// <summary>When true: hold = muted, release = unmuted.</summary>
    public bool IsMomentaryMute
    {
        get => _isMomentaryMute;
        set => SetProperty(ref _isMomentaryMute, value);
    }

    /// <summary>Transient: true while the assigned key is held down (visual flash).</summary>
    [JsonIgnore]
    public bool IsKeyHighlighted
    {
        get => _isKeyHighlighted;
        set => SetProperty(ref _isKeyHighlighted, value);
    }

    /// <summary>Saved pre-momentary mute state so we can restore on key-up.</summary>
    [JsonIgnore]
    public bool PreMomentaryMutedState { get; set; }
}
