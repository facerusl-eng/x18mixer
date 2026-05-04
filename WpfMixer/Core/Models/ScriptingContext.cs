using WpfMixer.Services;
using WpfMixer.ViewModels;

namespace WpfMixer.Core.Models;

public sealed class ScriptContext
{
    public required MixerViewModel Mixer { get; init; }
    public required OscClient Osc { get; init; }

    public Task Delay(int milliseconds, CancellationToken ct = default) => Task.Delay(milliseconds, ct);
}
