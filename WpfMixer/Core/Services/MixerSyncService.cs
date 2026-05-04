using System.Collections.Concurrent;
using System.Timers;
using Timer = System.Timers.Timer;
using WpfMixer.Core.Interfaces;
using WpfMixer.Services;

namespace WpfMixer.Core.Services;

public sealed class MixerSyncService : IMixerSyncService
{
    private readonly OscClient _oscClient;
    private readonly ILoggingService _logging;
    private readonly IToastNotificationService _toast;
    private readonly ConcurrentDictionary<string, DateTime> _pending = new(StringComparer.Ordinal);
    private readonly Timer _timeoutTimer;

    private DateTime _lastOscMessageUtc = DateTime.MinValue;
    private string _lastKnownIp = string.Empty;
    private int _reconnectAttempts;
    private volatile bool _isConnected;

    public MixerSyncService(OscClient oscClient, ILoggingService logging, IToastNotificationService toast)
    {
        _oscClient = oscClient;
        _logging = logging;
        _toast = toast;

        _timeoutTimer = new Timer(2000) { AutoReset = true };
        _timeoutTimer.Elapsed += TimeoutTimerOnElapsed;
    }

    public async Task OnConnectedAsync(string ipAddress, CancellationToken ct = default)
    {
        _isConnected = true;
        _reconnectAttempts = 0;
        _lastKnownIp = ipAddress;
        _lastOscMessageUtc = DateTime.UtcNow;
        _timeoutTimer.Start();

        _logging.LogInfo($"Mixer sync connected to {ipAddress}");

        await RequestFullStateAsync(ct).ConfigureAwait(false);
    }

    public void OnDisconnected()
    {
        _isConnected = false;
        _timeoutTimer.Stop();
        _pending.Clear();
        _logging.LogWarning("Mixer sync disconnected");
    }

    public void OnOscMessageReceived(string address)
    {
        _lastOscMessageUtc = DateTime.UtcNow;
        _pending.TryRemove(address, out _);
    }

    public void TrackOutgoing(string address)
    {
        _pending[address] = DateTime.UtcNow;
    }

    public void UpdateConnectionHint(string ipAddress)
    {
        _lastKnownIp = ipAddress;
    }

    private async void TimeoutTimerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        if (!_isConnected) return;

        var silence = DateTime.UtcNow - _lastOscMessageUtc;
        if (silence < TimeSpan.FromSeconds(12)) return;

        _logging.LogWarning($"OSC timeout detected ({silence.TotalSeconds:F1}s silence)");
        _toast.ShowWarning("Mixer connection timeout. Attempting auto-reconnect...");

        if (string.IsNullOrWhiteSpace(_lastKnownIp) || _reconnectAttempts >= 3)
            return;

        try
        {
            _reconnectAttempts++;
            await _oscClient.ConnectAsync(_lastKnownIp).ConfigureAwait(false);
            await OnConnectedAsync(_lastKnownIp).ConfigureAwait(false);
            _toast.ShowInfo("Mixer reconnected.");
        }
        catch (Exception ex)
        {
            _logging.LogError("Auto-reconnect failed", ex);
        }
    }

    private Task RequestFullStateAsync(CancellationToken ct)
    {
        // Core mixer-state sync requests to avoid UI drift after reconnect.
        var addresses = new List<string>();

        for (int i = 1; i <= 16; i++)
        {
            var basePath = $"/ch/{i:D2}";
            addresses.Add($"{basePath}/mix/fader");
            addresses.Add($"{basePath}/mix/on");
            addresses.Add($"{basePath}/mix/pan");
            addresses.Add($"{basePath}/config/name");
        }

        for (int bus = 1; bus <= 6; bus++)
        {
            var basePath = $"/bus/{bus:D2}";
            addresses.Add($"{basePath}/mix/fader");
            addresses.Add($"{basePath}/mix/on");
            addresses.Add($"{basePath}/config/name");
        }

        addresses.Add("/lr/mix/fader");
        addresses.Add("/lr/mix/on");

        foreach (var address in addresses)
        {
            if (ct.IsCancellationRequested) break;
            TrackOutgoing(address);
            _oscClient.Send(address);
        }

        _logging.LogInfo($"Requested mixer sync state ({addresses.Count} OSC paths)");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timeoutTimer.Stop();
        _timeoutTimer.Dispose();
    }
}
