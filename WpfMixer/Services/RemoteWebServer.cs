using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WpfMixer.Models;

namespace WpfMixer.Services;

/// <summary>
/// Lightweight HTTP + WebSocket server that:
///  • serves the static mobile-mix UI from wwwroot/
///  • validates per-musician tokens
///  • bridges WebSocket JSON messages → OSC (via <see cref="OscClient"/>)
///  • broadcasts OSC updates to all connected WebSocket clients
/// </summary>
public sealed class RemoteWebServer : IDisposable
{
    // ── Events (UI thread will subscribe) ────────────────────────────────────
    public event Action<string>? OnLog;
    public event Action<string>? OnClientConnected;    // musician slug
    public event Action<string>? OnClientDisconnected; // musician slug

    // ── State ────────────────────────────────────────────────────────────────
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<string, WebSocketConnection> _clients = new();
    private RemoteConfig _config;
    private OscClient? _osc;
    private bool _disposed;

    public bool IsRunning { get; private set; }
    public string BaseUrl  { get; private set; } = string.Empty;
    public string LanIp    { get; private set; } = "127.0.0.1";

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public RemoteWebServer(RemoteConfig config, OscClient? osc = null)
    {
        _config = config;
        _osc    = osc;
    }

    public void UpdateOscClient(OscClient osc) => _osc = osc;

    public void UpdateConfig(RemoteConfig config) => _config = config;

    public void Start()
    {
        if (IsRunning) return;

        LanIp   = GetLanIp();
        BaseUrl = $"http://{LanIp}:{_config.Port}";

        _cts      = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://+:{_config.Port}/");

        try
        {
            _listener.Start();
        }
        catch (HttpListenerException ex)
        {
            Log($"[RemoteWeb] Failed to start listener: {ex.Message}");
            Log("[RemoteWeb] Try running as Administrator or reserve the URL with netsh.");
            throw;
        }

        IsRunning = true;
        Log($"[RemoteWeb] Server started → {BaseUrl}");
        _ = AcceptLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;
        _cts?.Cancel();
        _listener?.Stop();
        _listener?.Close();
        _listener = null;
        Log("[RemoteWeb] Server stopped");
    }

    // ── Forward an OSC message to all connected WS clients ───────────────────

    public void BroadcastOscUpdate(string address, object[] args)
    {
        if (_clients.IsEmpty) return;

        var payload = new JsonObject
        {
            ["type"]    = "oscUpdate",
            ["address"] = address,
            ["args"]    = new JsonArray(args.Select(a => JsonValue.Create(a?.ToString())).ToArray())
        };

        var json = payload.ToJsonString();
        var dead = new List<string>();

        foreach (var (id, conn) in _clients)
        {
            if (!conn.Send(json))
                dead.Add(id);
        }

        foreach (var id in dead)
        {
            if (_clients.TryRemove(id, out var c))
            {
                OnClientDisconnected?.Invoke(c.Slug);
                c.Dispose();
            }
        }
    }

    // ── Main accept loop ─────────────────────────────────────────────────────

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var ctx = await _listener!.GetContextAsync().WaitAsync(ct);
                _ = HandleContextAsync(ctx, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                Log($"[RemoteWeb] Accept error: {ex.Message}");
            }
        }
    }

    // ── Per-request handler ──────────────────────────────────────────────────

    private async Task HandleContextAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        var req  = ctx.Request;
        var resp = ctx.Response;

        // ── CORS ──────────────────────────────────────────────────────────────
        resp.AddHeader("Access-Control-Allow-Origin",  "*");
        resp.AddHeader("Access-Control-Allow-Methods", "GET, OPTIONS");
        resp.AddHeader("Access-Control-Allow-Headers", "Content-Type");

        if (req.HttpMethod == "OPTIONS")
        {
            resp.StatusCode = 204;
            resp.Close();
            return;
        }

        var path = req.Url?.AbsolutePath.TrimEnd('/') ?? "/";

        try
        {
            // ── WebSocket upgrade ─────────────────────────────────────────────
            if (req.IsWebSocketRequest && path.StartsWith("/ws/"))
            {
                await HandleWebSocketAsync(ctx, path, ct);
                return;
            }

            // ── Mix page (/mix/{slug}) ────────────────────────────────────────
            if (path.StartsWith("/mix/"))
            {
                var slug  = path["/mix/".Length..];
                var token = req.QueryString["token"] ?? string.Empty;

                var musician = _config.Musicians
                    .FirstOrDefault(m => m.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

                if (musician == null)
                {
                    await RespondTextAsync(resp, 404, "text/html",
                        "<h1>404 – Musician page not found</h1>");
                    return;
                }

                if (!musician.Token.Equals(token, StringComparison.Ordinal))
                {
                    await RespondTextAsync(resp, 403, "text/html",
                        await BuildAccessDeniedHtml());
                    return;
                }

                await ServeAppShellAsync(resp, musician, token);
                return;
            }

            // ── Config API: GET /api/config/{slug}?token=... ─────────────────
            if (path.StartsWith("/api/config/"))
            {
                var slug  = path["/api/config/".Length..];
                var token = req.QueryString["token"] ?? string.Empty;

                var musician = _config.Musicians
                    .FirstOrDefault(m => m.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

                if (musician == null || !musician.Token.Equals(token, StringComparison.Ordinal))
                {
                    await RespondTextAsync(resp, 403, "application/json", "{\"error\":\"forbidden\"}");
                    return;
                }

                var cfgJson = JsonSerializer.Serialize(new
                {
                    name     = musician.Name,
                    slug     = musician.Slug,
                    busIndex = musician.BusIndex,
                    color    = musician.Color,
                    channels = musician.Channels.Select(c => new
                    {
                        index = c.ChannelIndex,
                        label = c.Label,
                        color = c.Color
                    })
                });

                await RespondTextAsync(resp, 200, "application/json", cfgJson);
                return;
            }

            // ── Static files from wwwroot/ ────────────────────────────────────
            await ServeStaticAsync(req, resp, path);
        }
        catch (Exception ex)
        {
            Log($"[RemoteWeb] Handler error for {path}: {ex.Message}");
            try
            {
                resp.StatusCode = 500;
                resp.Close();
            }
            catch { }
        }
    }

    // ── WebSocket handler ────────────────────────────────────────────────────

    private async Task HandleWebSocketAsync(HttpListenerContext ctx, string path, CancellationToken ct)
    {
        // path: /ws/{slug}?token=...
        var slug  = path["/ws/".Length..];
        var token = ctx.Request.QueryString["token"] ?? string.Empty;

        var musician = _config.Musicians
            .FirstOrDefault(m => m.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

        if (musician == null || !musician.Token.Equals(token, StringComparison.Ordinal))
        {
            ctx.Response.StatusCode = 403;
            ctx.Response.Close();
            return;
        }

        WebSocketContext wsCtx;
        try
        {
            wsCtx = await ctx.AcceptWebSocketAsync(null);
        }
        catch (Exception ex)
        {
            Log($"[RemoteWeb] WS upgrade failed: {ex.Message}");
            return;
        }

        var connId = Guid.NewGuid().ToString("N");
        var conn   = new WebSocketConnection(connId, slug, wsCtx.WebSocket, this);
        _clients[connId] = conn;
        Log($"[RemoteWeb] WS connected: {musician.Name} ({connId[..6]})");
        OnClientConnected?.Invoke(slug);

        await conn.RunAsync(ct, (json) => ProcessWsMessage(json, musician));

        _clients.TryRemove(connId, out _);
        OnClientDisconnected?.Invoke(slug);
        Log($"[RemoteWeb] WS disconnected: {musician.Name} ({connId[..6]})");
    }

    // ── OSC bridge: WS JSON → OSC ────────────────────────────────────────────

    private void ProcessWsMessage(string json, MusicianConfig musician)
    {
        try
        {
            var doc    = JsonDocument.Parse(json);
            var root   = doc.RootElement;
            var action = root.GetProperty("action").GetString() ?? string.Empty;

            switch (action)
            {
                case "setSend" when _osc?.IsConnected == true:
                {
                    int   ch    = root.GetProperty("channel").GetInt32();
                    int   bus   = root.GetProperty("bus").GetInt32();
                    float value = root.GetProperty("value").GetSingle();
                    // Validate this musician owns that bus
                    if (bus != musician.BusIndex) return;
                    _osc.Send($"/ch/{ch:D2}/mix/{bus:D2}/level", value);
                    break;
                }

                case "setMute" when _osc?.IsConnected == true:
                {
                    int  ch    = root.GetProperty("channel").GetInt32();
                    bool muted = root.GetProperty("value").GetBoolean();
                    // Only allow muting channels in this musician's list
                    if (!musician.Channels.Any(c => c.ChannelIndex == ch)) return;
                    _osc.Send($"/ch/{ch:D2}/mix/on", muted ? 0 : 1);
                    break;
                }

                case "setBusMaster" when _osc?.IsConnected == true:
                {
                    int   bus   = root.GetProperty("bus").GetInt32();
                    float value = root.GetProperty("value").GetSingle();
                    if (bus != musician.BusIndex) return;
                    _osc.Send($"/bus/{bus:D2}/mix/fader", value);
                    break;
                }

                case "setFxSend" when _osc?.IsConnected == true:
                {
                    int   ch    = root.GetProperty("channel").GetInt32();
                    int   fx    = root.GetProperty("fx").GetInt32();
                    float value = root.GetProperty("value").GetSingle();
                    if (!musician.Channels.Any(c => c.ChannelIndex == ch)) return;
                    _osc.Send($"/ch/{ch:D2}/mix/fxsend/{fx:D2}/level", value);
                    break;
                }

                case "ping":
                {
                    // Client keepalive – no OSC needed
                    break;
                }

                default:
                    Log($"[RemoteWeb] Unknown WS action: {action}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log($"[RemoteWeb] WS message error: {ex.Message}");
        }
    }

    // ── Static file serving ──────────────────────────────────────────────────

    private static readonly string WwwRoot =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");

    private async Task ServeStaticAsync(HttpListenerRequest req, HttpListenerResponse resp, string path)
    {
        var relative = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        if (string.IsNullOrEmpty(relative)) relative = "index.html";

        var filePath = Path.GetFullPath(Path.Combine(WwwRoot, relative));

        // Path traversal guard
        if (!filePath.StartsWith(WwwRoot, StringComparison.OrdinalIgnoreCase))
        {
            resp.StatusCode = 403;
            resp.Close();
            return;
        }

        if (!File.Exists(filePath))
        {
            await RespondTextAsync(resp, 404, "text/plain", "Not Found");
            return;
        }

        var bytes = await File.ReadAllBytesAsync(filePath);
        resp.ContentType   = GetMime(filePath);
        resp.ContentLength64 = bytes.Length;
        resp.StatusCode    = 200;
        await resp.OutputStream.WriteAsync(bytes, CancellationToken.None);
        resp.Close();
    }

    // ── App shell HTML ───────────────────────────────────────────────────────

    private async Task ServeAppShellAsync(HttpListenerResponse resp, MusicianConfig musician, string token)
    {
        // Read the template and inject the musician config
        var templatePath = Path.Combine(WwwRoot, "mix.html");
        string html;

        if (File.Exists(templatePath))
        {
            html = await File.ReadAllTextAsync(templatePath);
        }
        else
        {
            html = FallbackMixHtml();
        }

        // Inject bootstrap data
        var wsUrl = $"ws://{LanIp}:{_config.Port}/ws/{musician.Slug}?token={token}";
        var cfgUrl = $"/api/config/{musician.Slug}?token={token}";

        html = html
            .Replace("{{MUSICIAN_NAME}}",  musician.Name)
            .Replace("{{MUSICIAN_COLOR}}", musician.Color)
            .Replace("{{WS_URL}}",         wsUrl)
            .Replace("{{CFG_URL}}",        cfgUrl)
            .Replace("{{TOKEN}}",          token)
            .Replace("{{SLUG}}",           musician.Slug);

        await RespondTextAsync(resp, 200, "text/html; charset=utf-8", html);
    }

    private static async Task RespondTextAsync(HttpListenerResponse resp, int status,
                                                string contentType, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        resp.StatusCode      = status;
        resp.ContentType     = contentType;
        resp.ContentLength64 = bytes.Length;
        await resp.OutputStream.WriteAsync(bytes, CancellationToken.None);
        resp.Close();
    }

    private static Task<string> BuildAccessDeniedHtml() => Task.FromResult("""
        <!DOCTYPE html><html><head>
        <meta name="viewport" content="width=device-width,initial-scale=1">
        <title>Access Denied</title>
        <style>
          body{background:#0d0d0d;color:#e53935;font-family:monospace;
               display:flex;align-items:center;justify-content:center;height:100vh;margin:0}
          h1{font-size:2rem}p{color:#aaa}
        </style></head>
        <body><div style="text-align:center">
          <h1>🔒 Access Denied</h1>
          <p>Invalid or missing token. Scan your QR code again.</p>
        </div></body></html>
        """);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetMime(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".html" or ".htm" => "text/html; charset=utf-8",
        ".css"            => "text/css",
        ".js"             => "application/javascript",
        ".json"           => "application/json",
        ".png"            => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".svg"            => "image/svg+xml",
        ".ico"            => "image/x-icon",
        ".woff2"          => "font/woff2",
        _                 => "application/octet-stream"
    };

    public static string GetLanIp()
    {
        try
        {
            foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (iface.OperationalStatus != OperationalStatus.Up) continue;
                if (iface.NetworkInterfaceType is NetworkInterfaceType.Loopback) continue;

                foreach (var addr in iface.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        return addr.Address.ToString();
                }
            }
        }
        catch { }
        return "127.0.0.1";
    }

    private void Log(string msg) => OnLog?.Invoke(msg);

    // ── Fallback HTML (used when wwwroot/mix.html is absent) ─────────────────

    private static string FallbackMixHtml() => """
        <!DOCTYPE html><html><head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width,initial-scale=1">
        <title>{{MUSICIAN_NAME}} – Monitor Mix</title>
        <style>body{background:#0d0d0d;color:#fff;font-family:sans-serif;
          display:flex;align-items:center;justify-content:center;height:100vh}
          h2{color:{{MUSICIAN_COLOR}}}</style></head>
        <body><h2>Mix UI loading…</h2>
        <script>window.__WS_URL__="{{WS_URL}}";window.__CFG_URL__="{{CFG_URL}}";</script>
        </body></html>
        """;

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        foreach (var conn in _clients.Values) conn.Dispose();
        _clients.Clear();
    }
}

// ── WebSocket connection wrapper ───────────────────────────────────────────────

internal sealed class WebSocketConnection(string id, string slug, WebSocket ws, RemoteWebServer server)
    : IDisposable
{
    public string Slug => slug;

    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private bool _disposed;

    public bool Send(string json)
    {
        if (_disposed || ws.State != WebSocketState.Open) return false;
        _ = SendInternalAsync(json);
        return true;
    }

    private async Task SendInternalAsync(string json)
    {
        if (_disposed) return;
        await _sendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (ws.State != WebSocketState.Open) return;
            var bytes = Encoding.UTF8.GetBytes(json);
            await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch { }
        finally { _sendLock.Release(); }
    }

    public async Task RunAsync(CancellationToken ct, Action<string> onMessage)
    {
        var buffer = new byte[8192];
        var sb     = new StringBuilder();

        try
        {
            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result.EndOfMessage)
                {
                    onMessage(sb.ToString());
                    sb.Clear();
                }
            }
        }
        catch (OperationCanceledException) { }
        catch { }
        finally
        {
            try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); }
            catch { }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _sendLock.Dispose();
        ws.Dispose();
    }
}
