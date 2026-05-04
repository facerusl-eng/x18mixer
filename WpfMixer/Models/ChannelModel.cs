namespace WpfMixer.Models;

/// <summary>
/// Pure data model for a single X Air input channel (no INotifyPropertyChanged).
/// Mutated only by MixerCore; ChannelViewModel wraps this and raises UI events.
/// </summary>
public sealed class ChannelModel
{
    /// <summary>1-based channel index (1–18).</summary>
    public int Index { get; init; }

    public string Name { get; set; } = "Ch";

    /// <summary>Fader position 0.0–1.0  (unity = 0.75 on X Air scale).</summary>
    public float FaderLevel { get; set; } = 0.75f;

    /// <summary>Pan 0.0 (full-left) → 0.5 (center) → 1.0 (full-right).</summary>
    public float Pan { get; set; } = 0.5f;

    /// <summary>Main LR route on/off.</summary>
    public bool LrOn { get; set; } = true;

    /// <summary>Input source mode.</summary>
    public InputSource InputSource { get; set; } = InputSource.Analog;

    /// <summary>Selected analog input 1-18.</summary>
    public int AnalogInput { get; set; } = 1;

    /// <summary>Selected USB return 1-18.</summary>
    public int UsbReturn { get; set; } = 1;

    /// <summary>Direct-out source for this channel.</summary>
    public OutputSource DirectOutSource { get; set; } = OutputSource.DirectOut;

    /// <summary>X Air /ch/XX/mix/on: false = live (on=1), true = muted (on=0).</summary>
    public bool IsMuted { get; set; }

    public bool IsSolo { get; set; }

    /// <summary>Last received RMS meter level 0.0–1.0 (read-only from hardware).</summary>
    public float MeterLevel { get; set; }

    /// <summary>Bus sends: key = bus index 1–6, value = level 0.0–1.0.</summary>
    public Dictionary<int, float> BusSends { get; init; } = new();

    /// <summary>Bus send levels (1-6).</summary>
    public Dictionary<int, float> BusSendLevels { get; init; } = new();

    /// <summary>Bus send on/off states (1-6).</summary>
    public Dictionary<int, bool> BusSendOn { get; init; } = new();

    /// <summary>Bus send pre/post states (true = pre, false = post), keys 1-6.</summary>
    public Dictionary<int, bool> BusSendPre { get; init; } = new();

    /// <summary>FX sends: key = FX index 1–4, value = level 0.0–1.0.</summary>
    public Dictionary<int, float> FxSends { get; init; } = new();

    /// <summary>FX send levels (1-4).</summary>
    public Dictionary<int, float> FxSendLevels { get; init; } = new();

    /// <summary>FX send on/off states (1-4).</summary>
    public Dictionary<int, bool> FxSendOn { get; init; } = new();

    /// <summary>Strip colour (hex ARGB string e.g. "#FF2196F3") for UI colour coding.</summary>
    public string ColorHex { get; set; } = "#FF607D8B";

    // ── Factory ───────────────────────────────────────────────────────────────

    public static IReadOnlyList<ChannelModel> CreateDefaults()
    {
        var palette = new[]
        {
            "#FF2196F3","#FF4CAF50","#FFFF9800","#FFE91E63",
            "#FF9C27B0","#FF00BCD4","#FFCDDC39","#FFFF5722",
            "#FF795548","#FF607D8B","#FF3F51B5","#FF009688",
            "#FFFFF000","#FF8BC34A","#FFFF4081","#FF00E5FF",
            "#FF69F0AE","#FFFF6D00",
        };

        var channels = new List<ChannelModel>(18);
        for (int i = 1; i <= 18; i++)
        {
            var ch = new ChannelModel
            {
                Index    = i,
                Name     = $"Ch {i:D2}",
                ColorHex = palette[(i - 1) % palette.Length],
            };
            for (int b = 1; b <= 6; b++)
            {
                ch.BusSends[b] = 0f;
                ch.BusSendLevels[b] = 0f;
                ch.BusSendOn[b] = false;
                ch.BusSendPre[b] = false;
            }
            for (int f = 1; f <= 4; f++)
            {
                ch.FxSends[f] = 0f;
                ch.FxSendLevels[f] = 0f;
                ch.FxSendOn[f] = false;
            }
            channels.Add(ch);
        }
        return channels;
    }

    /// <summary>OSC path prefix for this channel, e.g. "/ch/01".</summary>
    public string OscBase => $"/ch/{Index:D2}";
}
