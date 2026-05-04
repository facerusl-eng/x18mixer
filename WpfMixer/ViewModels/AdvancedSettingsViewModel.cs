using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfMixer.Core.Interfaces;
using WpfMixer.Core.Models;

namespace WpfMixer.ViewModels;

public partial class AdvancedSettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private AppSettings _model;

    [ObservableProperty] private int _oscRateLimitPerSecond;
    [ObservableProperty] private int _meterUpdateRateHz;
    [ObservableProperty] private int _automationPrecisionMs;
    [ObservableProperty] private int _localApiPort;
    [ObservableProperty] private string _apiToken = string.Empty;
    [ObservableProperty] private bool _allowExternalPlugins;
    [ObservableProperty] private bool _scriptSandboxEnabled;

    public AdvancedSettingsViewModel(ISettingsService settings)
    {
        _settings = settings;
        _model = settings.LoadAppSettings();

        _oscRateLimitPerSecond = _model.OscRateLimitPerSecond;
        _meterUpdateRateHz = _model.MeterUpdateRateHz;
        _automationPrecisionMs = _model.AutomationPrecisionMs;
        _localApiPort = _model.LocalApiPort;
        _apiToken = _model.ApiToken;
        _allowExternalPlugins = _model.PluginPermissions.AllowExternalPlugins;
        _scriptSandboxEnabled = _model.ScriptSandboxing.Enabled;
    }

    [RelayCommand]
    public void Save()
    {
        _model.OscRateLimitPerSecond = OscRateLimitPerSecond;
        _model.MeterUpdateRateHz = MeterUpdateRateHz;
        _model.AutomationPrecisionMs = AutomationPrecisionMs;
        _model.LocalApiPort = LocalApiPort;
        _model.ApiToken = ApiToken;
        _model.PluginPermissions.AllowExternalPlugins = AllowExternalPlugins;
        _model.ScriptSandboxing.Enabled = ScriptSandboxEnabled;
        _settings.SaveAppSettings(_model);
    }
}
