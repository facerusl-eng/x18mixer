namespace WpfMixer.Core.Interfaces;

public interface ILocalApiService : IDisposable
{
    bool IsRunning { get; }
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync();
}
