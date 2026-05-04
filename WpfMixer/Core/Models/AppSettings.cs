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

    // Advanced / extensibility
    public int OscRateLimitPerSecond { get; set; } = 120;
    public int MeterUpdateRateHz { get; set; } = 40;
    public int AutomationPrecisionMs { get; set; } = 20;
    public int LocalApiPort { get; set; } = 8090;
    public string ApiToken { get; set; } = "";
    public PluginPermissions PluginPermissions { get; set; } = new();
    public ScriptSandboxSettings ScriptSandboxing { get; set; } = new();
    public string ThemeOverridesJson { get; set; } = "";

    // Future-proofing
    public string MixerFamily { get; set; } = "XAir"; // XAir | X32 | XR
    public bool EnableOfflineEditing { get; set; } = true;
    public bool EnableCloudSceneSync { get; set; } = false;
    public bool EnableMobileIntegration { get; set; } = true;
    public bool EnableMultiMixer { get; set; } = false;
}

public class PerformanceModeOptions
{
    public bool LockEditing { get; set; } = true;
    public bool ShowOnlyCriticalControls { get; set; } = false;
    public bool ShowLargeMeters { get; set; } = true;
}

public class PluginPermissions
{
    public bool AllowExternalPlugins { get; set; } = true;
    public bool AllowPluginUiPanels { get; set; } = true;
    public bool AllowPluginRemoteEndpoints { get; set; } = false;
    public bool AllowPluginAutomationHooks { get; set; } = false;
}

public class ScriptSandboxSettings
{
    public bool Enabled { get; set; } = true;
    public bool UseIsolatedScriptHost { get; set; } = true;
    public bool AllowFileSystem { get; set; } = false;
    public bool AllowNetwork { get; set; } = false;
    public int MaxExecutionSeconds { get; set; } = 15;
}
