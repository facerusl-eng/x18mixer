namespace WpfMixer.Models;

/// <summary>
/// Pure data model for the X Air Main L/R bus.
/// OSC path: /main/st/mix/...
/// </summary>
public sealed class MainBusModel
{
    /// <summary>Fader position 0.0–1.0  (unity = 0.75).</summary>
    public float FaderLevel { get; set; } = 0.75f;

    /// <summary>true = muted (on=0), false = live (on=1).</summary>
    public bool IsMuted { get; set; }

    /// <summary>Last received RMS meter level 0.0–1.0.</summary>
    public float MeterLevel { get; set; }

    public const string OscBase = "/main/st";
}
