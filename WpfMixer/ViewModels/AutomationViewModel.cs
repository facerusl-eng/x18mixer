using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfMixer.Core.Interfaces;
using WpfMixer.Core.Models;

namespace WpfMixer.ViewModels;

public partial class AutomationViewModel : ObservableObject
{
    private readonly IAutomationEngineService _automation;

    public ObservableCollection<AutomationTimeline> Timelines { get; } = [];
    public ObservableCollection<AutomationInterpolation> Interpolations { get; } =
    [
        AutomationInterpolation.Hold,
        AutomationInterpolation.Linear,
        AutomationInterpolation.EaseIn,
        AutomationInterpolation.EaseOut,
    ];

    [ObservableProperty] private AutomationTimeline? _selectedTimeline;
    [ObservableProperty] private AutomationTrack? _selectedTrack;
    [ObservableProperty] private AutomationKeyframe? _selectedKeyframe;
    [ObservableProperty] private float _newKeyframeValue = 0.75f;
    [ObservableProperty] private double _newKeyframeTimeSeconds = 0.0;
    [ObservableProperty] private double _snapDivisionSeconds = 0.25;

    public AutomationViewModel(IAutomationEngineService automation)
    {
        _automation = automation;
        Seed();
    }

    [RelayCommand]
    public async Task PlayAsync()
    {
        if (SelectedTimeline is null) return;
        SortCurrentTrack();
        await _automation.StartAsync(SelectedTimeline);
    }

    [RelayCommand]
    public void Stop() => _automation.Stop();

    [RelayCommand]
    public void AddTrack()
    {
        if (SelectedTimeline is null) return;
        var track = new AutomationTrack { TargetPath = "/ch/01/mix/fader" };
        track.Keyframes.Add(new AutomationKeyframe { TimeSeconds = 0, Value = 0.75f });
        SelectedTimeline.Tracks.Add(track);
        SelectedTrack = track;
    }

    [RelayCommand]
    public void RemoveTrack()
    {
        if (SelectedTimeline is null || SelectedTrack is null) return;
        SelectedTimeline.Tracks.Remove(SelectedTrack);
        SelectedTrack = SelectedTimeline.Tracks.FirstOrDefault();
    }

    [RelayCommand]
    public void AddKeyframe()
    {
        if (SelectedTrack is null) return;
        var time = Math.Clamp(NewKeyframeTimeSeconds, 0, SelectedTimeline?.DurationSeconds ?? 999);
        if (SnapDivisionSeconds > 0)
            time = Math.Round(time / SnapDivisionSeconds) * SnapDivisionSeconds;

        SelectedTrack.Keyframes.Add(new AutomationKeyframe
        {
            TimeSeconds = time,
            Value = Math.Clamp(NewKeyframeValue, 0f, 1f),
            Interpolation = AutomationInterpolation.Linear,
        });

        SortCurrentTrack();
        SelectedKeyframe = SelectedTrack.Keyframes.OrderBy(k => Math.Abs(k.TimeSeconds - time)).FirstOrDefault();
    }

    [RelayCommand]
    public void RemoveSelectedKeyframe()
    {
        if (SelectedTrack is null || SelectedKeyframe is null) return;
        SelectedTrack.Keyframes.Remove(SelectedKeyframe);
        SelectedKeyframe = SelectedTrack.Keyframes.FirstOrDefault();
    }

    [RelayCommand]
    public void SnapAllToGrid()
    {
        if (SelectedTrack is null || SnapDivisionSeconds <= 0) return;

        foreach (var kf in SelectedTrack.Keyframes)
        {
            kf.TimeSeconds = Math.Round(kf.TimeSeconds / SnapDivisionSeconds) * SnapDivisionSeconds;
        }

        SortCurrentTrack();
        OnPropertyChanged(nameof(SelectedTrack));
    }

    partial void OnSelectedTimelineChanged(AutomationTimeline? value)
    {
        SelectedTrack = value?.Tracks.FirstOrDefault();
        SelectedKeyframe = SelectedTrack?.Keyframes.FirstOrDefault();
    }

    partial void OnSelectedTrackChanged(AutomationTrack? value)
    {
        SelectedKeyframe = value?.Keyframes.FirstOrDefault();
    }

    private void SortCurrentTrack()
    {
        if (SelectedTrack is null) return;
        var sorted = SelectedTrack.Keyframes.OrderBy(k => k.TimeSeconds).ToList();
        SelectedTrack.Keyframes.Clear();
        foreach (var k in sorted) SelectedTrack.Keyframes.Add(k);
    }

    private void Seed()
    {
        var t = new AutomationTimeline
        {
            Name = "Show Intro",
            DurationSeconds = 12,
            Tracks =
            [
                new AutomationTrack
                {
                    TargetPath = "/ch/01/mix/fader",
                    Keyframes =
                    [
                        new AutomationKeyframe { TimeSeconds = 0, Value = 0.10f, Interpolation = AutomationInterpolation.EaseIn },
                        new AutomationKeyframe { TimeSeconds = 5, Value = 0.60f, Interpolation = AutomationInterpolation.Linear },
                        new AutomationKeyframe { TimeSeconds = 12, Value = 0.75f, Interpolation = AutomationInterpolation.EaseOut },
                    ]
                }
            ]
        };
        Timelines.Add(t);
        SelectedTimeline = t;
        SelectedTrack = t.Tracks.FirstOrDefault();
    }
}
