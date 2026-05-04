namespace WpfMixer.Core.Models;

public class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public string LastConnectedMixerIP { get; set; } = string.Empty;
    public bool AutoLoadLastScene { get; set; } = false;
    public string LastScenePath { get; set; } = string.Empty;
    public double MeterSmoothing { get; set; } = 0.15;
    public PerformanceModeOptions PerformanceModeOptions { get; set; } = new();
    public int RemoteServerPort { get; set; } = 8080;
    public string AccessibilityMode { get; set; } = "Normal";
    public bool EnableOscLogging { get; set; } = false;
    public bool AutoReconnect { get; set; } = true;
}

public class PerformanceModeOptions
{
    public bool LockEditing { get; set; } = true;
    public bool ShowOnlyCriticalControls { get; set; } = false;
    public bool ShowLargeMeters { get; set; } = true;
}
