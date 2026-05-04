using System.Collections.ObjectModel;

namespace WpfMixer.Models;

/// <summary>Root settings model — serialised to JSON on disk.</summary>
public class MixerSettings
{
    public ObservableCollection<Channel> Channels { get; set; } = [];
    public ObservableCollection<MuteGroup> MuteGroups { get; set; } = [];

    /// <summary>Named profiles that can be exported / imported.</summary>
    public ObservableCollection<KeyProfile> KeyProfiles { get; set; } = [];

    public string ActiveProfileName { get; set; } = "Default";
}

/// <summary>A named snapshot of all key assignments.</summary>
public class KeyProfile
{
    public string Name { get; set; } = "Default";

    /// <summary>ChannelId → key string.</summary>
    public Dictionary<string, string> ChannelKeys { get; set; } = [];

    /// <summary>GroupId → key string.</summary>
    public Dictionary<string, string> GroupKeys { get; set; } = [];

    /// <summary>ChannelId → IsMomentaryMute flag.</summary>
    public Dictionary<string, bool> ChannelMomentary { get; set; } = [];

    /// <summary>GroupId → IsMomentaryMute flag.</summary>
    public Dictionary<string, bool> GroupMomentary { get; set; } = [];
}
