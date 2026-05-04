namespace WpfMixer.Core.Models;

public sealed record SetFaderRequest(int Channel, float Value);
public sealed record SetMuteRequest(int Channel, bool IsMuted);
public sealed record SendOscRequest(string Address, string Arg);
public sealed record LoadSceneRequest(string Path);
public sealed record StartAutomationRequest(string TimelineName);
