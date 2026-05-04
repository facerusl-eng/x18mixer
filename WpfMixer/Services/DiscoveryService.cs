using System.Net;
using System.Net.Sockets;
using System.Text;

namespace WpfMixer.Services;

/// <summary>
/// Discovers a Behringer X Air mixer on the local network by broadcasting /xinfo.
/// The mixer replies with its IP, name and model.
/// </summary>
public sealed class DiscoveryService : IDisposable
{
    private const int XAirPort = 10024;
    private const string XInfoMessage = "/xinfo";
    private bool _disposed;

    public event Action<string, string, string>? MixerFound; // (ip, name, model)

    /// <summary>
    /// Broadcasts /xinfo and waits up to <paramref name="timeoutMs"/> ms for a reply.
    /// Fires <see cref="MixerFound"/> for every responder.
    /// </summary>
    public async Task DiscoverAsync(int timeoutMs = 3000, CancellationToken ct = default)
    {
        using var udp = new UdpClient();
        udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udp.EnableBroadcast = true;
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

        var packet = BuildXInfoPacket();
        var broadcast = new IPEndPoint(IPAddress.Broadcast, XAirPort);
        await udp.SendAsync(packet, packet.Length, broadcast);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                var result = await udp.ReceiveAsync().WaitAsync(cts.Token);
                ParseXInfoReply(result.Buffer, result.RemoteEndPoint.Address.ToString());
            }
            catch (OperationCanceledException) { break; }
            catch { /* malformed packet */ }
        }
    }

    private void ParseXInfoReply(byte[] data, string senderIp)
    {
        try
        {
            int offset = 0;
            string address = ReadOscString(data, ref offset);
            if (!address.StartsWith("/xinfo", StringComparison.OrdinalIgnoreCase)) return;

            ReadOscString(data, ref offset); // type tag
            string ip    = ReadOscString(data, ref offset);
            string name  = ReadOscString(data, ref offset);
            string model = ReadOscString(data, ref offset);

            MixerFound?.Invoke(string.IsNullOrWhiteSpace(ip) ? senderIp : ip, name, model);
        }
        catch { /* ignore */ }
    }

    private static byte[] BuildXInfoPacket()
    {
        var buf = new List<byte>();
        WriteOscString(buf, XInfoMessage);
        WriteOscString(buf, ",");
        return buf.ToArray();
    }

    private static void WriteOscString(List<byte> buf, string s)
    {
        buf.AddRange(Encoding.ASCII.GetBytes(s));
        buf.Add(0);
        while (buf.Count % 4 != 0) buf.Add(0);
    }

    private static string ReadOscString(byte[] data, ref int offset)
    {
        int start = offset;
        while (offset < data.Length && data[offset] != 0) offset++;
        var s = Encoding.ASCII.GetString(data, start, offset - start);
        offset++;
        while (offset % 4 != 0) offset++;
        return s;
    }

    public void Dispose() => _disposed = true;
}
