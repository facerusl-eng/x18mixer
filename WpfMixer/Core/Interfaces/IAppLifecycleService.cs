using WpfMixer.ViewModels;

namespace WpfMixer.Core.Interfaces;

public interface IAppLifecycleService
{
    Task StartupAsync(MixerViewModel mixerViewModel, CancellationToken ct = default);
    Task ShutdownAsync(MixerViewModel mixerViewModel, CancellationToken ct = default);
}
