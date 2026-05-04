using Microsoft.Extensions.DependencyInjection;
using WpfMixer.Core.Interfaces;
using WpfMixer.Core.Services;
using WpfMixer.Services;
using WpfMixer.ViewModels;

namespace WpfMixer.Core.Helpers;

public static class ServiceRegistration
{
    public static IServiceCollection AddWpfMixerCore(this IServiceCollection services)
    {
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<IToastNotificationService, ToastNotificationService>();

        services.AddSingleton<OscClient>();
        services.AddSingleton<DiscoveryService>();
        services.AddSingleton<SceneService>();
        services.AddSingleton(sp => new UndoRedoService(50));
        services.AddSingleton<KeyboardService>();
        services.AddSingleton<SettingsService>();

        services.AddSingleton<ISettingsService>(sp => sp.GetRequiredService<SettingsService>());
        services.AddSingleton<IMixerSyncService, MixerSyncService>();

        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IAppLifecycleService, AppLifecycleService>();

        services.AddSingleton<MixerViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddSingleton(sp => sp.GetRequiredService<MixerViewModel>().Appearance);
        services.AddSingleton(sp => sp.GetRequiredService<MixerViewModel>().Routing);
        services.AddSingleton(sp => sp.GetRequiredService<MixerViewModel>().BusMix);
        services.AddSingleton(sp => sp.GetRequiredService<MixerViewModel>().MonitorMix);
        services.AddSingleton(sp => sp.GetRequiredService<MixerViewModel>().SceneManager);
        services.AddSingleton(sp => sp.GetRequiredService<MixerViewModel>().Performance);
        services.AddSingleton(sp => sp.GetRequiredService<MixerViewModel>().RemoteControl);

        services.AddSingleton<MainWindow>();
        return services;
    }
}
