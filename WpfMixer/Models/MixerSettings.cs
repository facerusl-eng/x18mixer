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

    // ── Appearance ─────────────────────────────────────────────────────────
    public string Theme         { get; set; } = "Dark";
    public double FontScale     { get; set; } = 1.0;    // 0.8 – 1.6
    public bool   LargeText     { get; set; } = false;
    public bool   ReduceMotion  { get; set; } = false;
    public bool   UiSounds      { get; set; } = false;
    public float  MeterAttack   { get; set; } = 0.15f;  // 0–1, lower = faster attack
    public float  MeterRelease  { get; set; } = 0.08f;  // 0–1, lower = faster decay
    public float  PeakHoldSecs  { get; set; } = 2.0f;
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
