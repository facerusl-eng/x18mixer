namespace WpfMixer.Core.Interfaces;

public interface ILoggingService : IAsyncDisposable
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? exception = null);
}
