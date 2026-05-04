using System.Windows.Input;
using WpfMixer.Models;

namespace WpfMixer.Services;

/// <summary>
/// Routes global KeyDown / KeyUp events to channel mutes and mute groups.
/// Raised by the MainWindow and consumed by the ViewModel via this service.
/// </summary>
public class KeyboardService
{
    // Tracks which keys are currently physically held (to prevent repeat-fire)
    private readonly HashSet<Key> _heldKeys = [];

    // Injected collections (live references from ViewModel)
    private IReadOnlyList<Channel> _channels = [];
    private IReadOnlyList<MuteGroup> _groups = [];

    public event Action<string, string>? KeyActionFired; // (keyString, actionDescription)

    public void Bind(IReadOnlyList<Channel> channels, IReadOnlyList<MuteGroup> groups)
    {
        _channels = channels;
        _groups = groups;
    }

    /// <summary>Call from Window.PreviewKeyDown. Returns true if the key was handled.</summary>
    public bool HandleKeyDown(Key key)
    {
        // Ignore auto-repeat events (key held generates many KeyDown)
        if (_heldKeys.Contains(key))
            return false;

        _heldKeys.Add(key);

        var keyStr = key.ToString();
        bool handled = false;

        // --- Individual channel keys ---
        foreach (var ch in _channels)
        {
            if (ch.AssignedKey != keyStr)
                continue;

            ch.IsKeyHighlighted = true;

            if (ch.IsMomentaryMute)
            {
                ch.PreMomentaryMutedState = ch.IsMuted;
                ch.IsMuted = true;
            }
            else
            {
                ch.IsMuted = !ch.IsMuted;
            }

            KeyActionFired?.Invoke(keyStr, $"{ch.Name}: {(ch.IsMuted ? "Muted" : "Unmuted")}");
            handled = true;
        }

        // --- Mute group keys ---
        foreach (var grp in _groups)
        {
            if (grp.AssignedKey != keyStr)
                continue;

            grp.IsKeyHighlighted = true;

            if (grp.IsMomentaryMute)
            {
                // Save individual states then mute all
                grp.PreMomentaryStates.Clear();
                foreach (var ch in _channels.Where(c => grp.ChannelIds.Contains(c.Id)))
                {
                    grp.PreMomentaryStates[ch.Id] = ch.IsMuted;
                    ch.IsMuted = true;
                }
                grp.IsActive = true;
            }
            else
            {
                // Toggle: if any are unmuted → mute all; else unmute all
                var groupChannels = _channels.Where(c => grp.ChannelIds.Contains(c.Id)).ToList();
                bool anyUnmuted = groupChannels.Any(c => !c.IsMuted);
                foreach (var ch in groupChannels)
                    ch.IsMuted = anyUnmuted;
                grp.IsActive = anyUnmuted;
            }

            KeyActionFired?.Invoke(keyStr, $"Group '{grp.Name}': {(grp.IsActive ? "Muted" : "Unmuted")}");
            handled = true;
        }

        return handled;
    }

    /// <summary>Call from Window.PreviewKeyUp.</summary>
    public bool HandleKeyUp(Key key)
    {
        _heldKeys.Remove(key);

        var keyStr = key.ToString();
        bool handled = false;

        // Restore momentary-muted channels
        foreach (var ch in _channels)
        {
            if (ch.AssignedKey != keyStr)
                continue;

            ch.IsKeyHighlighted = false;

            if (ch.IsMomentaryMute)
            {
                ch.IsMuted = ch.PreMomentaryMutedState;
                handled = true;
            }
        }

        // Restore momentary-muted groups
        foreach (var grp in _groups)
        {
            if (grp.AssignedKey != keyStr)
                continue;

            grp.IsKeyHighlighted = false;

            if (grp.IsMomentaryMute)
            {
                foreach (var ch in _channels.Where(c => grp.ChannelIds.Contains(c.Id)))
                {
                    if (grp.PreMomentaryStates.TryGetValue(ch.Id, out var prev))
                        ch.IsMuted = prev;
                }
                grp.IsActive = false;
                handled = true;
            }
        }

        return handled;
    }

    /// <summary>Returns the key string if that key is already bound to any channel or group, else null.</summary>
    public string? FindConflict(string keyStr, string? excludeChannelId = null, string? excludeGroupId = null)
    {
        foreach (var ch in _channels)
        {
            if (ch.AssignedKey == keyStr && ch.Id != excludeChannelId)
                return $"Channel '{ch.Name}'";
        }
        foreach (var grp in _groups)
        {
            if (grp.AssignedKey == keyStr && grp.Id != excludeGroupId)
                return $"Group '{grp.Name}'";
        }
        return null;
    }
}
