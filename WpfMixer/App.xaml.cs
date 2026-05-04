using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using WpfMixer.Core.Helpers;
using WpfMixer.Core.Interfaces;
using WpfMixer.Services;

namespace WpfMixer;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _services = new ServiceCollection()
            .AddWpfMixerCore()
            .BuildServiceProvider();

        var logger = _services.GetRequiredService<ILoggingService>();
        var toast = _services.GetRequiredService<IToastNotificationService>();

        DispatcherUnhandledException += (s, ex) =>
        {
            logger.LogError("Unhandled UI exception", ex.Exception);
            toast.ShowError("An unexpected UI error occurred. The app will continue running.");
            ex.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
        {
            logger.LogError("Unhandled app-domain exception", ex.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (s, ex) =>
        {
            logger.LogError("Unobserved task exception", ex.Exception);
            ex.SetObserved();
        };

        ThemeService.LoadSaved();

        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_services is not null)
        {
            var logger = _services.GetRequiredService<ILoggingService>();
            logger.LogInfo("Application exiting");

            if (logger is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();

            _services.Dispose();
        }

        base.OnExit(e);
    }
}

