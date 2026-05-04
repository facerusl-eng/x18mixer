namespace WpfMixer.Core.Models;

public sealed record ChannelChangedEvent(int ChannelIndex, string Property, object? Value);
public sealed record FaderMovedEvent(int ChannelIndex, float Value);
public sealed record MuteChangedEvent(int ChannelIndex, bool IsMuted);
public sealed record SceneLoadedEvent(string SceneName);
public sealed record MixerConnectedEvent(string IpAddress);
public sealed record MixerDisconnectedEvent(string? Reason = null);
public sealed record AutomationStartedEvent(string TimelineName);
public sealed record AutomationStoppedEvent(string TimelineName);
