using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WpfMixer.Models;

namespace WpfMixer.Services;

/// <summary>
/// Loads and saves MixerSettings to a JSON file next to the executable.
/// Also handles key-profile export / import.
/// </summary>
public class SettingsService
{
    private static readonly string SettingsPath =
        Path.Combine(AppContext.BaseDirectory, "mixer-settings.json");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public MixerSettings Load()
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

    public void Save(MixerSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(SettingsPath, json);
    }

    public void ExportProfile(KeyProfile profile, string filePath)
    {
        var json = JsonSerializer.Serialize(profile, SerializerOptions);
        File.WriteAllText(filePath, json);
    }

    public KeyProfile? ImportProfile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<KeyProfile>(json, SerializerOptions);
    }

    private static MixerSettings CreateDefault()
    {
        var settings = new MixerSettings();
        for (int i = 1; i <= 8; i++)
            settings.Channels.Add(new Channel { Name = $"Ch {i}" });
        return settings;
    }
}
