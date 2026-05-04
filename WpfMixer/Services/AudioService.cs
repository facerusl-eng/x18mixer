using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using WpfMixer.Models;

namespace WpfMixer.Services;

/// <summary>
/// Simple audio engine using NAudio.
/// Each channel gets a sine-wave test tone (replaceable with real input sources).
/// Volume and mute state changes are applied in real time.
/// </summary>
public class AudioService : IDisposable
{
    private IWavePlayer? _outputDevice;
    private MixingSampleProvider? _mixer;
    private readonly List<ChannelAudioTrack> _tracks = [];
    private bool _disposed;

    public void Initialise(IReadOnlyList<Channel> channels)
    {
        _outputDevice?.Dispose();
        _tracks.Clear();

        _outputDevice = new WaveOutEvent();
        _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2))
        {
            ReadFully = true
        };

        foreach (var ch in channels)
        {
            var track = new ChannelAudioTrack(ch);
            _tracks.Add(track);
            _mixer.AddMixerInput(track.SampleProvider);
        }

        _outputDevice.Init(_mixer);
        _outputDevice.Play();
    }

    public void UpdateChannel(Channel channel)
    {
        var track = _tracks.FirstOrDefault(t => t.ChannelId == channel.Id);
        track?.Apply(channel);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _outputDevice?.Stop();
        _outputDevice?.Dispose();
    }

    // ─── Inner type ──────────────────────────────────────────────────────────

    private sealed class ChannelAudioTrack
    {
        private readonly VolumeSampleProvider _volume;

        public string ChannelId { get; }
        public ISampleProvider SampleProvider => _volume;

        public ChannelAudioTrack(Channel channel)
        {
            ChannelId = channel.Id;

            // Test-tone sine wave; replace with file/input source as needed
            var sine = new SignalGenerator(44100, 2)
            {
                Type = SignalGeneratorType.Sin,
                Frequency = 220 + (channel.Name.GetHashCode() % 440),
                Gain = 0.05,
            };

            _volume = new VolumeSampleProvider(sine);
            Apply(channel);
        }

        public void Apply(Channel channel)
        {
            _volume.Volume = channel.IsMuted ? 0f : (float)channel.Volume;
        }
    }
}
