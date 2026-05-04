using System.IO;
using WpfMixer.Core.Interfaces;
using WpfMixer.Core.Models;
using WpfMixer.Services;
using WpfMixer.ViewModels;

namespace WpfMixer.Core.Services;

public sealed class AppLifecycleService : IAppLifecycleService
{
    private readonly ISettingsService _settings;
    private readonly ILoggingService _logging;
    private readonly INavigationService _navigation;
    private readonly IMixerSyncService _mixerSync;
    private readonly IToastNotificationService _toast;
    private readonly IPluginHostService _plugins;
    private readonly ILocalApiService _localApi;
    private readonly IModuleManager _modules;

    public AppLifecycleService(
        ISettingsService settings,
        ILoggingService logging,
        INavigationService navigation,
        IMixerSyncService mixerSync,
        IToastNotificationService toast,
        IPluginHostService plugins,
        ILocalApiService localApi,
        IModuleManager modules)
    {
        _settings = settings;
        _logging = logging;
        _navigation = navigation;
        _mixerSync = mixerSync;
        _toast = toast;
        _plugins = plugins;
        _localApi = localApi;
        _modules = modules;
    }

    public async Task StartupAsync(MixerViewModel mixerViewModel, CancellationToken ct = default)
    {
        AppSettings app = _settings.LoadAppSettings();
        _logging.LogInfo("App startup flow started");

        ThemeService.Apply(Enum.TryParse<AppTheme>(app.Theme, out var th) ? th : AppTheme.Dark);

        if (_modules.IsEnabled("PluginModule"))
            _plugins.LoadPlugins();

        if (_modules.IsEnabled("RemoteModule"))
        {
            try
            {
                await _localApi.StartAsync(ct).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logging.LogError("Local API start failed", ex);
                _toast.ShowWarning("Local API failed to start. App will continue without API endpoint.");
            }
        }

        await mixerViewModel.InitializeAsync().ConfigureAwait(true);

        if (!mixerViewModel.IsConnected && !string.IsNullOrWhiteSpace(app.LastConnectedMixerIP))
        {
            mixerViewModel.MixerIpInput = app.LastConnectedMixerIP;
            await mixerViewModel.ConnectManualCommand.ExecuteAsync(null);
        }

        if (mixerViewModel.IsConnected)
        {
            _mixerSync.UpdateConnectionHint(mixerViewModel.MixerIpInput);
            await _mixerSync.OnConnectedAsync(mixerViewModel.MixerIpInput, ct).ConfigureAwait(true);
        }

        if (app.AutoLoadLastScene && !string.IsNullOrWhiteSpace(app.LastScenePath) && File.Exists(app.LastScenePath))
            await mixerViewModel.LoadSceneFromPathAsync(app.LastScenePath).ConfigureAwait(true);

        mixerViewModel.RemoteControl.StartServerCommand.Execute(null);

        _navigation.NavigateTo<MixerViewModel>();
        _logging.LogInfo("App startup flow completed");
    }

    public async Task ShutdownAsync(MixerViewModel mixerViewModel, CancellationToken ct = default)
    {
        _logging.LogInfo("App shutdown flow started");

        try
        {
            mixerViewModel.Cleanup();
            mixerViewModel.RemoteControl.StopServerCommand.Execute(null);
            _mixerSync.OnDisconnected();
            _plugins.UnloadPlugins();
            await _localApi.StopAsync().ConfigureAwait(true);

            var app = _settings.CurrentAppSettings;
            app.Theme = ThemeService.CurrentTheme.ToString();
            app.LastConnectedMixerIP = mixerViewModel.MixerIpInput;
            _settings.SaveAppSettings(app);

            _logging.LogInfo("App shutdown flow completed");
        }
        catch (Exception ex)
        {
            _logging.LogError("Shutdown flow failed", ex);
            _toast.ShowError("A shutdown task failed. See logs for details.");
        }

        await Task.CompletedTask;
    }
}
