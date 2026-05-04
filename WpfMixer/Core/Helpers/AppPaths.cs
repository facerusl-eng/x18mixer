using System.IO;

namespace WpfMixer.Core.Helpers;

public static class AppPaths
{
    public static readonly string Root = AppContext.BaseDirectory;
    public static readonly string Data = Path.Combine(Root, "Data");
    public static readonly string Logs = Path.Combine(Root, "Logs");
    public static readonly string Scenes = Path.Combine(Data, "Scenes");
    public static readonly string Backups = Path.Combine(Data, "Backups");
    public static readonly string KeyboardProfiles = Path.Combine(Data, "KeyboardProfiles");
    public static readonly string AppSettingsPath = Path.Combine(Data, "AppSettings.json");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(Data);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Scenes);
        Directory.CreateDirectory(Backups);
        Directory.CreateDirectory(KeyboardProfiles);
    }
}
