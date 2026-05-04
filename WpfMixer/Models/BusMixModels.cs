namespace WpfMixer.Models;

public sealed class BusMixModel
{
    public int BusIndex { get; set; }
    public Dictionary<int, float> ChannelSendLevels { get; set; } = new();
    public Dictionary<int, bool> ChannelSendOn { get; set; } = new();
    public Dictionary<int, bool> ChannelSendPre { get; set; } = new();
    public float BusMasterLevel { get; set; } = 0.75f;
    public bool BusMasterMute { get; set; }

    public BusMixModel() { }

    public BusMixModel(int busIndex, int channels)
    {
        BusIndex = busIndex;
        for (int ch = 1; ch <= channels; ch++)
        {
            ChannelSendLevels[ch] = 0f;
            ChannelSendOn[ch] = false;
            ChannelSendPre[ch] = false;
        }
    }
}

public sealed class FxReturnMixModel
{
    public int FxIndex { get; set; }
    public Dictionary<int, float> ChannelSendLevels { get; set; } = new();
    public Dictionary<int, bool> ChannelSendOn { get; set; } = new();
    public Dictionary<int, bool> ChannelSendPre { get; set; } = new();
    public float BusMasterLevel { get; set; } = 0.75f;
    public bool BusMasterMute { get; set; }

    public FxReturnMixModel() { }

    public FxReturnMixModel(int fxIndex, int channels)
    {
        FxIndex = fxIndex;
        for (int ch = 1; ch <= channels; ch++)
        {
            ChannelSendLevels[ch] = 0f;
            ChannelSendOn[ch] = false;
            ChannelSendPre[ch] = false;
        }
    }
}
