using System.Collections.ObjectModel;

namespace WpfMixer.Core.Models;

public sealed class AutomationTimeline
{
    public string Name { get; set; } = "Timeline";
    public double DurationSeconds { get; set; } = 30;
    public ObservableCollection<AutomationTrack> Tracks { get; set; } = [];
}

public sealed class AutomationTrack
{
    public string TargetPath { get; set; } = "/ch/01/mix/fader";
    public ObservableCollection<AutomationKeyframe> Keyframes { get; set; } = [];
}

public sealed class AutomationKeyframe
{
    public double TimeSeconds { get; set; }
    public float Value { get; set; }
}
