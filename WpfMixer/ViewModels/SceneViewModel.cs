using CommunityToolkit.Mvvm.ComponentModel;
using WpfMixer.Models;

namespace WpfMixer.ViewModels;

public sealed class SceneViewModel : ObservableObject
{
    public string Name { get; }
    public DateTime Timestamp { get; }
    public string Notes { get; }
    public int ChannelCount { get; }
    public string FxTypes { get; }
    public string RoutingSummary { get; }
    public string FilePath { get; }

    public SceneViewModel(SceneModel scene, string filePath)
    {
        Name = scene.Name;
        Timestamp = scene.Timestamp.ToLocalTime();
        Notes = scene.Notes ?? string.Empty;
        ChannelCount = scene.Snapshot.InputChannels.Count;
        FxTypes = string.Join(", ", new[]
        {
            scene.Snapshot.Fx1.FxType,
            scene.Snapshot.Fx2.FxType,
            scene.Snapshot.Fx3.FxType,
            scene.Snapshot.Fx4.FxType
        });
        RoutingSummary = $"Outputs: {scene.Snapshot.Outputs.Count}, USB: {scene.Snapshot.Usb.Mode}";
        FilePath = filePath;
    }
}
