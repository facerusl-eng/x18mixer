namespace WpfMixer.Core.Interfaces;

public interface IMixerSyncService : IDisposable
{
    Task OnConnectedAsync(string ipAddress, CancellationToken ct = default);
    void OnDisconnected();
    void OnOscMessageReceived(string address);
    void TrackOutgoing(string address);
    void UpdateConnectionHint(string ipAddress);
}
