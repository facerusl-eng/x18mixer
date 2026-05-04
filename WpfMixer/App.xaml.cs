using System.Windows;
using WpfMixer.Services;

namespace WpfMixer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Apply persisted theme before any window opens (avoids flash-of-default-theme)
        ThemeService.LoadSaved();
    }
}

