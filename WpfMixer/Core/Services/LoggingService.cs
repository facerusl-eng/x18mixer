using System.IO;
using System.Threading.Channels;
using WpfMixer.Core.Helpers;
using WpfMixer.Core.Interfaces;

namespace WpfMixer.Core.Services;

public sealed class LoggingService : ILoggingService
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false,
    });
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _writerTask;

    public LoggingService()
    {
        AppPaths.EnsureDirectories();
        _writerTask = Task.Run(WriterLoopAsync);
    }

    public void LogInfo(string message) => Enqueue("INFO", message);
    public void LogWarning(string message) => Enqueue("WARN", message);

    public void LogError(string message, Exception? exception = null)
    {
        var suffix = exception is null ? string.Empty : $" | {exception.GetType().Name}: {exception.Message}";
        Enqueue("ERROR", message + suffix);
    }

    private void Enqueue(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        _channel.Writer.TryWrite(line);
    }

    private async Task WriterLoopAsync()
    {
        try
        {
            await foreach (var line in _channel.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                var path = Path.Combine(AppPaths.Logs, $"{DateTime.Now:yyyy-MM-dd}.txt");
                await File.AppendAllTextAsync(path, line + Environment.NewLine, _cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _channel.Writer.TryComplete();
        try
        {
            await _writerTask.ConfigureAwait(false);
        }
        catch
        {
        }
        _cts.Dispose();
    }
}
