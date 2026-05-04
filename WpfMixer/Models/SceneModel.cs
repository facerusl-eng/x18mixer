namespace WpfMixer.Models;

public sealed class SceneModel
{
    public string Name { get; set; } = "Untitled";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public MixerModel Snapshot { get; set; } = MixerModel.CreateDefault();
}
