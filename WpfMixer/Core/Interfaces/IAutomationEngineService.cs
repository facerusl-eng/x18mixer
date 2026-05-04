using WpfMixer.Core.Models;

namespace WpfMixer.Core.Interfaces;

public interface IAutomationEngineService
{
    bool IsRunning { get; }
    Task StartAsync(AutomationTimeline timeline, CancellationToken ct = default);
    void Stop();
}
