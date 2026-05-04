using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfMixer.Models;
using WpfMixer.Services;

namespace WpfMixer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SettingsService _settingsService = new();
    private readonly AudioService _audioService = new();
    public readonly KeyboardService KeyboardService = new();

    [ObservableProperty] private ObservableCollection<Channel> _channels = [];
    [ObservableProperty] private ObservableCollection<MuteGroup> _muteGroups = [];
    [ObservableProperty] private string _lastKeyAction = string.Empty;
    [ObservableProperty] private bool _showKeyOverlay;
    [ObservableProperty] private string _overlayText = string.Empty;

    private System.Timers.Timer? _overlayTimer;

    public MainViewModel()
    {
        var settings = _settingsService.Load();
        Channels = settings.Channels;
        MuteGroups = settings.MuteGroups;

        KeyboardService.Bind(Channels, MuteGroups);
        KeyboardService.KeyActionFired += OnKeyActionFired;

        // Propagate audio updates when mute/volume change
        foreach (var ch in Channels)
            ch.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(Channel.IsMuted) or nameof(Channel.Volume))
                    _audioService.UpdateChannel(ch);
            };

        _audioService.Initialise(Channels);
    }

    // ─── Channel commands ─────────────────────────────────────────────────────

    [RelayCommand]
    private void AddChannel()
    {
        var ch = new Channel { Name = $"Ch {Channels.Count + 1}" };
        Channels.Add(ch);
        ch.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Channel.IsMuted) or nameof(Channel.Volume))
                _audioService.UpdateChannel(ch);
        };
        _audioService.Initialise(Channels); // reinitialise mixer with new channel
        KeyboardService.Bind(Channels, MuteGroups);
        SaveSettings();
    }

    [RelayCommand]
    private void RemoveChannel(Channel channel)
    {
        // Remove from all groups first
        foreach (var grp in MuteGroups)
            grp.ChannelIds.Remove(channel.Id);

        Channels.Remove(channel);
        _audioService.Initialise(Channels);
        KeyboardService.Bind(Channels, MuteGroups);
        SaveSettings();
    }

    [RelayCommand]
    private void ToggleMute(Channel channel)
    {
        channel.IsMuted = !channel.IsMuted;
        SaveSettings();
    }

    // ─── Mute group commands ──────────────────────────────────────────────────

    [RelayCommand]
    private void AddMuteGroup()
    {
        MuteGroups.Add(new MuteGroup { Name = $"Group {MuteGroups.Count + 1}" });
        KeyboardService.Bind(Channels, MuteGroups);
        SaveSettings();
    }

    [RelayCommand]
    private void RemoveMuteGroup(MuteGroup group)
    {
        MuteGroups.Remove(group);
        KeyboardService.Bind(Channels, MuteGroups);
        SaveSettings();
    }

    [RelayCommand]
    private void ActivateMuteGroup(MuteGroup group)
    {
        var groupChannels = Channels.Where(c => group.ChannelIds.Contains(c.Id)).ToList();
        bool anyUnmuted = groupChannels.Any(c => !c.IsMuted);
        foreach (var ch in groupChannels)
            ch.IsMuted = anyUnmuted;
        group.IsActive = anyUnmuted;
        SaveSettings();
    }

    // ─── Key assignment ───────────────────────────────────────────────────────

    /// <summary>
    /// Try to assign a key to a channel. Returns null on success, or a conflict description.
    /// Pass forceAssign=true to override conflicts.
    /// </summary>
    public string? AssignKeyToChannel(Channel channel, string keyStr, bool forceAssign = false)
    {
        var conflict = KeyboardService.FindConflict(keyStr, excludeChannelId: channel.Id);
        if (conflict != null && !forceAssign)
            return conflict;

        channel.AssignedKey = string.IsNullOrWhiteSpace(keyStr) ? null : keyStr;
        SaveSettings();
        return null;
    }

    public string? AssignKeyToGroup(MuteGroup group, string keyStr, bool forceAssign = false)
    {
        var conflict = KeyboardService.FindConflict(keyStr, excludeGroupId: group.Id);
        if (conflict != null && !forceAssign)
            return conflict;

        group.AssignedKey = string.IsNullOrWhiteSpace(keyStr) ? null : keyStr;
        SaveSettings();
        return null;
    }

    // ─── Profile import / export ──────────────────────────────────────────────

    [RelayCommand]
    private void ExportProfile(string filePath)
    {
        var profile = new KeyProfile { Name = "Exported" };
        foreach (var ch in Channels)
        {
            if (ch.AssignedKey != null) profile.ChannelKeys[ch.Id] = ch.AssignedKey;
            profile.ChannelMomentary[ch.Id] = ch.IsMomentaryMute;
        }
        foreach (var grp in MuteGroups)
        {
            if (grp.AssignedKey != null) profile.GroupKeys[grp.Id] = grp.AssignedKey;
            profile.GroupMomentary[grp.Id] = grp.IsMomentaryMute;
        }
        _settingsService.ExportProfile(profile, filePath);
    }

    [RelayCommand]
    private void ImportProfile(string filePath)
    {
        var profile = _settingsService.ImportProfile(filePath);
        if (profile == null) return;

        foreach (var ch in Channels)
        {
            if (profile.ChannelKeys.TryGetValue(ch.Id, out var key)) ch.AssignedKey = key;
            if (profile.ChannelMomentary.TryGetValue(ch.Id, out var mom)) ch.IsMomentaryMute = mom;
        }
        foreach (var grp in MuteGroups)
        {
            if (profile.GroupKeys.TryGetValue(grp.Id, out var key)) grp.AssignedKey = key;
            if (profile.GroupMomentary.TryGetValue(grp.Id, out var mom)) grp.IsMomentaryMute = mom;
        }
        SaveSettings();
    }

    // ─── Persistence ──────────────────────────────────────────────────────────

    public void SaveSettings()
    {
        _settingsService.Save(new MixerSettings
        {
            Channels = Channels,
            MuteGroups = MuteGroups,
        });
    }

    // ─── Key overlay ──────────────────────────────────────────────────────────

    private void OnKeyActionFired(string key, string description)
    {
        LastKeyAction = description;
        OverlayText = $"[{key}]  {description}";
        ShowKeyOverlay = true;

        _overlayTimer?.Stop();
        _overlayTimer = new System.Timers.Timer(1500) { AutoReset = false };
        _overlayTimer.Elapsed += (_, _) =>
            Application.Current.Dispatcher.Invoke(() => ShowKeyOverlay = false);
        _overlayTimer.Start();
    }

    public void Cleanup()
    {
        SaveSettings();
        _audioService.Dispose();
    }
}
