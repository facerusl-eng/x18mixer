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
    [ObservableProperty] private AutomationTimeline? _selectedTimeline;

    public AutomationViewModel(IAutomationEngineService automation)
    {
        _automation = automation;
        Seed();
    }

    [RelayCommand]
    public async Task PlayAsync()
    {
        if (SelectedTimeline is null) return;
        await _automation.StartAsync(SelectedTimeline);
    }

    [RelayCommand]
    public void Stop() => _automation.Stop();

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
                        new AutomationKeyframe { TimeSeconds = 0, Value = 0.10f },
                        new AutomationKeyframe { TimeSeconds = 5, Value = 0.60f },
                        new AutomationKeyframe { TimeSeconds = 12, Value = 0.75f },
                    ]
                }
            ]
        };
        Timelines.Add(t);
        SelectedTimeline = t;
    }
}
