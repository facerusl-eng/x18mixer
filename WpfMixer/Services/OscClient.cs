using System.Net;
using System.Net.Sockets;
using System.Text;

namespace WpfMixer.Services;

/// <summary>
/// Async UDP OSC client for the Behringer X Air protocol (port 10024).
/// Never blocks the UI thread.  All callbacks are raised on a thread-pool thread.
/// </summary>
public sealed class OscClient : IDisposable
{
    private UdpClient? _udp;
    private IPEndPoint _target = new(IPAddress.Loopback, 10024);
    private volatile bool _running;
    private bool _disposed;
    private System.Timers.Timer? _heartbeat;

    // ── Events ────────────────────────────────────────────────────────────────
    /// <summary>Raised on every complete OSC message received from the mixer.</summary>
    public event Action<string, object[]>? MessageReceived;

    /// <summary>Raised for informational / error log messages (never throws).</summary>
    public event Action<string>? OnLog;

    public bool IsConnected { get; private set; }

    // ─── Connection ───────────────────────────────────────────────────────────

    public async Task ConnectAsync(string host, int port = 10024)
    {
        Disconnect();   // clean up any previous socket

        try
        {
            _target = new IPEndPoint(IPAddress.Parse(host), port);
            _udp    = new UdpClient();
            _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            _udp.Client.ReceiveTimeout = 0;   // async receive — no timeout needed
            IsConnected = true;
            _running    = true;

            Log($"Connected to {host}:{port}");
            _ = ReceiveLoopAsync();
            StartHeartbeat();
        }
        catch (Exception ex)
        {
            Log($"ConnectAsync failed: {ex.Message}");
            throw;
        }

        await Task.CompletedTask;
    }

    public void Disconnect()
    {
        _running = false;
        StopHeartbeat();
        IsConnected = false;
        _udp?.Close();
        _udp?.Dispose();
        _udp = null;
        Log("Disconnected");
    }

    // ─── Heartbeat (/xremote every 5 s) ──────────────────────────────────────

    private void StartHeartbeat()
    {
        StopHeartbeat();
        _heartbeat = new System.Timers.Timer(5_000) { AutoReset = true };
        _heartbeat.Elapsed += (_, _) =>
        {
            if (IsConnected) Send("/xremote");
        };
        _heartbeat.Start();
        Send("/xremote");   // immediate first ping
    }

    private void StopHeartbeat()
    {
        _heartbeat?.Stop();
        _heartbeat?.Dispose();
        _heartbeat = null;
    }

    // ─── Sending ──────────────────────────────────────────────────────────────

    public void Send(string address)                 => SendPacket(BuildOscMessage(address));
    public void Send(string address, float value)    => SendPacket(BuildOscMessage(address, value));
    public void Send(string address, int value)      => SendPacket(BuildOscMessage(address, value));
    public void Send(string address, string value)   => SendPacket(BuildOscMessage(address, value));

    private void SendPacket(byte[] packet)
    {
        if (_udp == null || !IsConnected) return;
        try
        {
            _udp.Send(packet, packet.Length, _target);
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex) { Log($"Send error: {ex.Message}"); }
    }

    // ─── Receive loop ─────────────────────────────────────────────────────────

    private async Task ReceiveLoopAsync()
    {
        Log("Receive loop started");
        while (_running && _udp != null)
        {
            try
            {
                var result = await _udp.ReceiveAsync().ConfigureAwait(false);
                ParseAndDispatch(result.Buffer);
            }
            catch (ObjectDisposedException) { break; }
            catch (SocketException ex) when (!_running)
            {
                _ = ex;
                break;   // clean shutdown
            }
            catch (Exception ex)
            {
                Log($"Receive error: {ex.Message}");
            }
        }
        Log("Receive loop ended");
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
                    case 'f': args.Add(ReadFloat(data,  ref offset)); break;
                    case 'i': args.Add(ReadInt32(data,  ref offset)); break;
                    case 's': args.Add(ReadOscString(data, ref offset)); break;
                    case 'T': args.Add(true);  break;
                    case 'F': args.Add(false); break;
                    case ',': break;   // type-tag prefix
                }
            }

            MessageReceived?.Invoke(address, args.ToArray());
        }
        catch (Exception ex)
        {
            Log($"Parse error: {ex.Message}");
        }
    }

    // ─── OSC encoding ─────────────────────────────────────────────────────────

    private static byte[] BuildOscMessage(string address, params object[] args)
    {
        var buf  = new List<byte>(64);
        WriteOscString(buf, address);

        var tags     = new StringBuilder(",");
        var argBytes = new List<byte>(32);
        foreach (var arg in args)
        {
            switch (arg)
            {
                case float f:  tags.Append('f'); WriteFloat(argBytes, f);  break;
                case int   i:  tags.Append('i'); WriteInt32(argBytes, i);  break;
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
        buf.Add(0);
        while (buf.Count % 4 != 0) buf.Add(0);
    }

    private static void WriteFloat(List<byte> buf, float f)
    {
        var b = BitConverter.GetBytes(f);
        if (BitConverter.IsLittleEndian) Array.Reverse(b);
        buf.AddRange(b);
    }

    private static void WriteInt32(List<byte> buf, int i)
    {
        var b = BitConverter.GetBytes(i);
        if (BitConverter.IsLittleEndian) Array.Reverse(b);
        buf.AddRange(b);
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

    private static float ReadFloat(byte[] data, ref int offset)
    {
        var b = data[offset..(offset + 4)];
        if (BitConverter.IsLittleEndian) Array.Reverse(b);
        offset += 4;
        return BitConverter.ToSingle(b, 0);
    }

    private static int ReadInt32(byte[] data, ref int offset)
    {
        var b = data[offset..(offset + 4)];
        if (BitConverter.IsLittleEndian) Array.Reverse(b);
        offset += 4;
        return BitConverter.ToInt32(b, 0);
    }

    // ─── Logging ──────────────────────────────────────────────────────────────

    private void Log(string message) => OnLog?.Invoke($"[OscClient] {message}");

    // ─── Dispose ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
    }
}
