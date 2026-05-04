using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WpfMixer.Core.Helpers;
using WpfMixer.Core.Interfaces;
using WpfMixer.Core.Models;
using WpfMixer.Models;

namespace WpfMixer.Services;

/// <summary>
/// Loads and saves MixerSettings to a JSON file next to the executable.
/// Also handles key-profile export / import.
/// </summary>
public class SettingsService : ISettingsService
{
    private static readonly object SyncLock = new();

    private static readonly string SettingsPath =
        Path.Combine(AppContext.BaseDirectory, "mixer-settings.json");

    private static readonly string AppSettingsPath = AppPaths.AppSettingsPath;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public AppSettings CurrentAppSettings { get; private set; } = new();

    public MixerSettings Load()
    {
        lock (SyncLock)
        {
            if (!File.Exists(SettingsPath))
                return CreateDefault();

            try
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<MixerSettings>(json, SerializerOptions)
                       ?? CreateDefault();
            }
            catch
            {
                return CreateDefault();
            }
        }
    }

    public void Save(MixerSettings settings)
    {
        lock (SyncLock)
        {
            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(SettingsPath, json);
        }
    }

    public void ExportProfile(KeyProfile profile, string filePath)
    {
        lock (SyncLock)
        {
            var json = JsonSerializer.Serialize(profile, SerializerOptions);
            File.WriteAllText(filePath, json);
        }
    }

    public KeyProfile? ImportProfile(string filePath)
    {
        lock (SyncLock)
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<KeyProfile>(json, SerializerOptions);
        }
    }

    public AppSettings LoadAppSettings()
    {
        lock (SyncLock)
        {
            AppPaths.EnsureDirectories();
            if (!File.Exists(AppSettingsPath))
            {
                CurrentAppSettings = new AppSettings();
                SaveAppSettings(CurrentAppSettings);
                return CurrentAppSettings;
            }

            try
            {
                var json = File.ReadAllText(AppSettingsPath);
                CurrentAppSettings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
            }
            catch
            {
                CurrentAppSettings = new AppSettings();
            }

            return CurrentAppSettings;
        }
    }

    public void SaveAppSettings(AppSettings settings)
    {
        lock (SyncLock)
        {
            AppPaths.EnsureDirectories();
            CurrentAppSettings = settings;
            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(AppSettingsPath, json);
        }
    }

    public void SaveAppSettings()
    {
        SaveAppSettings(CurrentAppSettings);
    }

    private static MixerSettings CreateDefault()
    {
        var settings = new MixerSettings();
        for (int i = 1; i <= 8; i++)
            settings.Channels.Add(new Channel { Name = $"Ch {i}" });
        return settings;
    }
}
