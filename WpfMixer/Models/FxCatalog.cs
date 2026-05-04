namespace WpfMixer.Models;

public sealed record FxParameterDefinition(string Name, float DefaultValue, float Min = 0f, float Max = 1f);

/// <summary>Static FX type/parameter definitions used by the FX rack.</summary>
public static class FxCatalog
{
    public static readonly string[] FxTypes =
    [
        "Hall Reverb",
        "Plate Reverb",
        "Vintage Reverb",
        "Delay",
        "Ping-Pong Delay",
        "Chorus",
        "Flanger",
        "Phaser",
        "Enhancer",
        "De-Esser",
        "Graphic EQ",
        "Dual Pitch",
        "Tremolo",
        "Rotary Speaker"
    ];

    private static readonly Dictionary<string, IReadOnlyList<FxParameterDefinition>> Map = new(StringComparer.Ordinal)
    {
        ["Hall Reverb"] =
        [
            new("PreDelay", 0.18f),
            new("Decay", 0.62f),
            new("Damp", 0.45f),
            new("Diffusion", 0.56f),
            new("LowCut", 0.22f),
            new("HighCut", 0.68f),
            new("Mix", 0.30f)
        ],
        ["Plate Reverb"] =
        [
            new("PreDelay", 0.10f),
            new("Decay", 0.54f),
            new("Damp", 0.52f),
            new("Density", 0.60f),
            new("Mix", 0.28f)
        ],
        ["Vintage Reverb"] =
        [
            new("Tone", 0.55f),
            new("Decay", 0.50f),
            new("ModDepth", 0.24f),
            new("Mix", 0.26f)
        ],
        ["Delay"] =
        [
            new("Time", 0.38f),
            new("Feedback", 0.40f),
            new("LowCut", 0.18f),
            new("HighCut", 0.72f),
            new("Mix", 0.25f)
        ],
        ["Ping-Pong Delay"] =
        [
            new("Time", 0.36f),
            new("Feedback", 0.44f),
            new("Width", 0.84f),
            new("Damp", 0.46f),
            new("Mix", 0.24f)
        ],
        ["Chorus"] =
        [
            new("Rate", 0.32f),
            new("Depth", 0.46f),
            new("Delay", 0.20f),
            new("Feedback", 0.20f),
            new("Mix", 0.30f)
        ],
        ["Flanger"] =
        [
            new("Rate", 0.42f),
            new("Depth", 0.52f),
            new("Feedback", 0.38f),
            new("Phase", 0.50f),
            new("Mix", 0.26f)
        ],
        ["Phaser"] =
        [
            new("Rate", 0.34f),
            new("Depth", 0.48f),
            new("Stages", 0.50f),
            new("Feedback", 0.30f),
            new("Mix", 0.24f)
        ],
        ["Enhancer"] =
        [
            new("Air", 0.44f),
            new("Detail", 0.50f),
            new("BassTight", 0.30f),
            new("Mix", 0.20f)
        ],
        ["De-Esser"] =
        [
            new("Frequency", 0.58f),
            new("Threshold", 0.50f),
            new("Range", 0.38f),
            new("Listen", 0.00f)
        ],
        ["Graphic EQ"] =
        [
            new("31Hz", 0.50f),
            new("63Hz", 0.50f),
            new("125Hz", 0.50f),
            new("250Hz", 0.50f),
            new("500Hz", 0.50f),
            new("1kHz", 0.50f),
            new("2kHz", 0.50f),
            new("4kHz", 0.50f),
            new("8kHz", 0.50f),
            new("16kHz", 0.50f)
        ],
        ["Dual Pitch"] =
        [
            new("Voice1", 0.50f),
            new("Voice2", 0.50f),
            new("Fine", 0.50f),
            new("Feedback", 0.20f),
            new("Mix", 0.24f)
        ],
        ["Tremolo"] =
        [
            new("Rate", 0.36f),
            new("Depth", 0.58f),
            new("Shape", 0.40f),
            new("Mix", 0.34f)
        ],
        ["Rotary Speaker"] =
        [
            new("Speed", 0.44f),
            new("Drive", 0.25f),
            new("Balance", 0.50f),
            new("Distance", 0.36f),
            new("Mix", 0.28f)
        ]
    };

    public static IReadOnlyList<FxParameterDefinition> GetParameters(string fxType)
        => Map.TryGetValue(fxType, out var list) ? list :
            [new FxParameterDefinition("Param 1", 0.50f)];

    public static string FromTypeIndex(int typeIndex)
    {
        if (typeIndex < 0 || typeIndex >= FxTypes.Length)
            return FxTypes[0];
        return FxTypes[typeIndex];
    }

    public static int ToTypeIndex(string fxType)
    {
        for (int i = 0; i < FxTypes.Length; i++)
            if (string.Equals(FxTypes[i], fxType, StringComparison.Ordinal))
                return i;
        return 0;
    }
}
