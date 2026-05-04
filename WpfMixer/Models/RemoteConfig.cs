using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfMixer.Models;

/// <summary>
/// Describes which input channels appear in a musician's personal mix page.
/// </summary>
public class MusicianChannelConfig
{
    public int ChannelIndex { get; set; }   // 1-based (matches X Air ch/01 .. ch/18)
    public string Label     { get; set; } = string.Empty;
    public string Color     { get; set; } = "#FFFFFF";
}

/// <summary>
/// One musician entry: name, bus assignment, channel list, and security token.
/// </summary>
public class MusicianConfig
{
    public string Id          { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name        { get; set; } = "Musician";
    public string Slug        { get; set; } = "musician";   // URL path segment
    public int    BusIndex    { get; set; } = 1;            // 1-6
    public string Token       { get; set; } = GenerateToken();
    public string Color       { get; set; } = "#1DB954";
    public List<MusicianChannelConfig> Channels { get; set; } = [];

    public static string GenerateToken()
    {
        var bytes = new byte[16];
        Random.Shared.NextBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>URL path that this musician opens in the browser.</summary>
    [JsonIgnore]
    public string Path => $"/mix/{Slug}";

    /// <summary>Full URL including token query parameter.</summary>
    public string BuildUrl(string baseUrl) => $"{baseUrl}/mix/{Slug}?token={Token}";
}

/// <summary>
/// Root config object serialised to/from RemoteConfig.json.
/// </summary>
public class RemoteConfig
{
    public int    Port       { get; set; } = 8080;
    public bool   Enabled    { get; set; } = true;
    public List<MusicianConfig> Musicians { get; set; } = DefaultMusicians();

    // ── Serialisation ─────────────────────────────────────────────────────────

    private static readonly string ConfigPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RemoteConfig.json");

    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static RemoteConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var text = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<RemoteConfig>(text, _json) ?? CreateDefault();
            }
        }
        catch { /* corrupt file — fall through to defaults */ }
        var cfg = CreateDefault();
        cfg.Save();
        return cfg;
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, _json));
        }
        catch { /* silently ignore I/O errors during save */ }
    }

    private static RemoteConfig CreateDefault()
    {
        return new RemoteConfig { Musicians = DefaultMusicians() };
    }

    // ── Default musician roster (matches common full-band setup) ──────────────

    private static List<MusicianConfig> DefaultMusicians() =>
    [
        new MusicianConfig
        {
            Name = "Drummer", Slug = "drums", BusIndex = 1, Color = "#FF5722",
            Channels =
            [
                new MusicianChannelConfig { ChannelIndex = 1,  Label = "Kick",   Color = "#FF7043" },
                new MusicianChannelConfig { ChannelIndex = 2,  Label = "Snare",  Color = "#FFA726" },
                new MusicianChannelConfig { ChannelIndex = 3,  Label = "HiHat",  Color = "#FFCA28" },
                new MusicianChannelConfig { ChannelIndex = 4,  Label = "OH L",   Color = "#26C6DA" },
                new MusicianChannelConfig { ChannelIndex = 5,  Label = "OH R",   Color = "#26C6DA" },
            ]
        },
        new MusicianConfig
        {
            Name = "Vocalist", Slug = "vocals", BusIndex = 2, Color = "#E91E63",
            Channels =
            [
                new MusicianChannelConfig { ChannelIndex = 6,  Label = "Lead Vox",  Color = "#F06292" },
                new MusicianChannelConfig { ChannelIndex = 7,  Label = "BV 1",      Color = "#CE93D8" },
                new MusicianChannelConfig { ChannelIndex = 8,  Label = "BV 2",      Color = "#CE93D8" },
                new MusicianChannelConfig { ChannelIndex = 9,  Label = "Guitar",    Color = "#80CBC4" },
                new MusicianChannelConfig { ChannelIndex = 10, Label = "Keys",      Color = "#FFD54F" },
            ]
        },
        new MusicianConfig
        {
            Name = "Guitarist", Slug = "guitar", BusIndex = 3, Color = "#00BCD4",
            Channels =
            [
                new MusicianChannelConfig { ChannelIndex = 9,  Label = "Guitar",    Color = "#80CBC4" },
                new MusicianChannelConfig { ChannelIndex = 6,  Label = "Lead Vox",  Color = "#F06292" },
                new MusicianChannelConfig { ChannelIndex = 10, Label = "Keys",      Color = "#FFD54F" },
                new MusicianChannelConfig { ChannelIndex = 11, Label = "Bass DI",   Color = "#A5D6A7" },
            ]
        },
        new MusicianConfig
        {
            Name = "Bassist", Slug = "bass", BusIndex = 4, Color = "#4CAF50",
            Channels =
            [
                new MusicianChannelConfig { ChannelIndex = 11, Label = "Bass DI",   Color = "#A5D6A7" },
                new MusicianChannelConfig { ChannelIndex = 9,  Label = "Guitar",    Color = "#80CBC4" },
                new MusicianChannelConfig { ChannelIndex = 6,  Label = "Lead Vox",  Color = "#F06292" },
                new MusicianChannelConfig { ChannelIndex = 1,  Label = "Kick",      Color = "#FF7043" },
            ]
        },
        new MusicianConfig
        {
            Name = "Keys", Slug = "keys", BusIndex = 5, Color = "#9C27B0",
            Channels =
            [
                new MusicianChannelConfig { ChannelIndex = 10, Label = "Keys",      Color = "#FFD54F" },
                new MusicianChannelConfig { ChannelIndex = 6,  Label = "Lead Vox",  Color = "#F06292" },
                new MusicianChannelConfig { ChannelIndex = 9,  Label = "Guitar",    Color = "#80CBC4" },
                new MusicianChannelConfig { ChannelIndex = 11, Label = "Bass DI",   Color = "#A5D6A7" },
            ]
        },
        new MusicianConfig
        {
            Name = "Backing Tracks", Slug = "backing", BusIndex = 6, Color = "#607D8B",
            Channels =
            [
                new MusicianChannelConfig { ChannelIndex = 17, Label = "BT L",     Color = "#78909C" },
                new MusicianChannelConfig { ChannelIndex = 18, Label = "BT R",     Color = "#78909C" },
                new MusicianChannelConfig { ChannelIndex = 6,  Label = "Lead Vox", Color = "#F06292" },
            ]
        },
    ];
}
