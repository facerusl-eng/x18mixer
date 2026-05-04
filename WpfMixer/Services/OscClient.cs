using System.Net;
using System.Net.Sockets;
using System.Text;

namespace WpfMixer.Services;

/// <summary>
/// Minimal UDP OSC client for Behringer X Air protocol.
/// Sends OSC messages to the mixer and receives replies on a bound local port.
/// </summary>
public sealed class OscClient : IDisposable
{
    private UdpClient? _udp;
    private IPEndPoint _target = new(IPAddress.Loopback, 10024);
    private bool _running;
    private bool _disposed;

    public event Action<string, object[]>? MessageReceived;

    public bool IsConnected { get; private set; }

    // ─── Connection ───────────────────────────────────────────────────────────

    public async Task ConnectAsync(string host, int port = 10024)
    {
        _udp?.Dispose();
        _target = new IPEndPoint(IPAddress.Parse(host), port);
        _udp = new UdpClient();
        _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));
        IsConnected = true;
        _running = true;
        _ = ReceiveLoopAsync();
        await Task.CompletedTask;
    }

    public void Disconnect()
    {
        _running = false;
        IsConnected = false;
        _udp?.Dispose();
        _udp = null;
    }

    // ─── Sending ──────────────────────────────────────────────────────────────

    public void Send(string address) => SendPacket(BuildOscMessage(address));
    public void Send(string address, float value) => SendPacket(BuildOscMessage(address, value));
    public void Send(string address, int value) => SendPacket(BuildOscMessage(address, value));
    public void Send(string address, string value) => SendPacket(BuildOscMessage(address, value));

    private void SendPacket(byte[] packet)
    {
        if (_udp == null || !IsConnected) return;
        try { _udp.Send(packet, packet.Length, _target); }
        catch { /* swallow on disconnect */ }
    }

    // ─── Receive loop ─────────────────────────────────────────────────────────

    private async Task ReceiveLoopAsync()
    {
        while (_running && _udp != null)
        {
            try
            {
                var result = await _udp.ReceiveAsync();
                ParseAndDispatch(result.Buffer);
            }
            catch (ObjectDisposedException) { break; }
            catch { /* ignore malformed packets */ }
        }
    }

    private void ParseAndDispatch(byte[] data)
    {
        try
        {
            int offset = 0;
            string address = ReadOscString(data, ref offset);
            string typetag = ReadOscString(data, ref offset);

            var args = new List<object>();
            foreach (char c in typetag)
            {
                switch (c)
                {
                    case 'f':
                        args.Add(ReadFloat(data, ref offset));
                        break;
                    case 'i':
                        args.Add(ReadInt32(data, ref offset));
                        break;
                    case 's':
                        args.Add(ReadOscString(data, ref offset));
                        break;
                    case ',': break; // type tag prefix
                }
            }
            MessageReceived?.Invoke(address, args.ToArray());
        }
        catch { /* malformed */ }
    }

    // ─── OSC encoding ─────────────────────────────────────────────────────────

    private static byte[] BuildOscMessage(string address, params object[] args)
    {
        var buf = new List<byte>();
        WriteOscString(buf, address);

        var tags = new StringBuilder(",");
        var argBytes = new List<byte>();
        foreach (var arg in args)
        {
            switch (arg)
            {
                case float f: tags.Append('f'); WriteFloat(argBytes, f); break;
                case int i:   tags.Append('i'); WriteInt32(argBytes, i); break;
                case string s: tags.Append('s'); WriteOscString(argBytes, s); break;
            }
        }

        WriteOscString(buf, tags.ToString());
        buf.AddRange(argBytes);
        return buf.ToArray();
    }

    private static void WriteOscString(List<byte> buf, string s)
    {
        buf.AddRange(Encoding.ASCII.GetBytes(s));
        buf.Add(0); // null terminator
        while (buf.Count % 4 != 0) buf.Add(0); // pad to 4 bytes
    }

    private static void WriteFloat(List<byte> buf, float f)
    {
        var bytes = BitConverter.GetBytes(f);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        buf.AddRange(bytes);
    }

    private static void WriteInt32(List<byte> buf, int i)
    {
        var bytes = BitConverter.GetBytes(i);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        buf.AddRange(bytes);
    }

    private static string ReadOscString(byte[] data, ref int offset)
    {
        int start = offset;
        while (offset < data.Length && data[offset] != 0) offset++;
        var s = Encoding.ASCII.GetString(data, start, offset - start);
        offset++; // past null
        while (offset % 4 != 0) offset++; // skip padding
        return s;
    }

    private static float ReadFloat(byte[] data, ref int offset)
    {
        var bytes = data[offset..(offset + 4)];
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        offset += 4;
        return BitConverter.ToSingle(bytes, 0);
    }

    private static int ReadInt32(byte[] data, ref int offset)
    {
        var bytes = data[offset..(offset + 4)];
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        offset += 4;
        return BitConverter.ToInt32(bytes, 0);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
    }
}
