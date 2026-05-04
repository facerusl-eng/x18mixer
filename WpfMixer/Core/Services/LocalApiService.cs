using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using WpfMixer.Core.Interfaces;
using WpfMixer.Core.Models;
using WpfMixer.ViewModels;

namespace WpfMixer.Core.Services;

public sealed class LocalApiService : ILocalApiService
{
    private readonly IServiceProvider _services;
    private readonly ISettingsService _settings;
    private readonly ILoggingService _logging;
    private readonly IEventBus _eventBus;
    private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;

    public bool IsRunning { get; private set; }

    public LocalApiService(
        IServiceProvider services,
        ISettingsService settings,
        ILoggingService logging,
        IEventBus eventBus)
    {
        _services = services;
        _settings = settings;
        _logging = logging;
        _eventBus = eventBus;

        // Broadcast core state events over WS.
        _eventBus.Subscribe<ChannelChangedEvent>(e => BroadcastEvent("ChannelChanged", e));
        _eventBus.Subscribe<FaderMovedEvent>(e => BroadcastEvent("FaderMoved", e));
        _eventBus.Subscribe<MuteChangedEvent>(e => BroadcastEvent("MuteChanged", e));
        _eventBus.Subscribe<SceneLoadedEvent>(e => BroadcastEvent("SceneLoaded", e));
        _eventBus.Subscribe<MixerConnectedEvent>(e => BroadcastEvent("MixerConnected", e));
        _eventBus.Subscribe<MixerDisconnectedEvent>(e => BroadcastEvent("MixerDisconnected", e));
        _eventBus.Subscribe<AutomationStartedEvent>(e => BroadcastEvent("AutomationStarted", e));
        _eventBus.Subscribe<AutomationStoppedEvent>(e => BroadcastEvent("AutomationStopped", e));
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        if (IsRunning) return Task.CompletedTask;

        var s = _settings.LoadAppSettings();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{s.LocalApiPort}/");
        _listener.Start();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        IsRunning = true;
        _ = AcceptLoopAsync(_cts.Token);
        _logging.LogInfo($"Local API started on 127.0.0.1:{s.LocalApiPort}");
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        IsRunning = false;
        _cts?.Cancel();

        foreach (var s in _sockets.Values)
        {
            try { s.Abort(); s.Dispose(); } catch { }
        }
        _sockets.Clear();

        _listener?.Stop();
        _listener?.Close();
        _listener = null;
        _logging.LogInfo("Local API stopped");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _ = StopAsync();
        _cts?.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener is not null)
        {
            try
            {
                var ctx = await _listener.GetContextAsync().WaitAsync(ct);
                _ = HandleAsync(ctx, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex) { _logging.LogError("Local API accept failed", ex); }
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        var req = ctx.Request;
        var res = ctx.Response;

        try
        {
            if (!Authorize(req))
            {
                res.StatusCode = 401;
                await WriteJson(res, new { error = "Unauthorized" });
                return;
            }

            var path = req.Url?.AbsolutePath ?? "/";
            var mixer = _services.GetRequiredService<MixerViewModel>();
            var automation = _services.GetRequiredService<IAutomationEngineService>();

            if (req.IsWebSocketRequest && path == "/ws")
            {
                await HandleWsAsync(ctx, ct);
                return;
            }

            if (req.HttpMethod == "GET" && path == "/api/mixer/state")
            {
                await WriteJson(res, mixer.GetPublicState());
                return;
            }

            if (req.HttpMethod == "POST" && path == "/api/mixer/fader")
            {
                var body = await ReadBody<SetFaderRequest>(req);
                mixer.SetChannelFader(body.Channel, body.Value);
                await WriteJson(res, new { ok = true });
                return;
            }

            if (req.HttpMethod == "POST" && path == "/api/mixer/mute")
            {
                var body = await ReadBody<SetMuteRequest>(req);
                mixer.SetChannelMute(body.Channel, body.IsMuted);
                await WriteJson(res, new { ok = true });
                return;
            }

            if (req.HttpMethod == "POST" && path == "/api/mixer/osc")
            {
                var body = await ReadBody<SendOscRequest>(req);
                mixer.SendOsc(body.Address, body.Arg);
                await WriteJson(res, new { ok = true });
                return;
            }

            if (req.HttpMethod == "POST" && path == "/api/mixer/scene/load")
            {
                var body = await ReadBody<LoadSceneRequest>(req);
                await mixer.LoadSceneFromPathAsync(body.Path);
                await WriteJson(res, new { ok = true });
                return;
            }

            if (req.HttpMethod == "POST" && path == "/api/mixer/automation/start")
            {
                var body = await ReadBody<StartAutomationRequest>(req);
                var timeline = new AutomationTimeline
                {
                    Name = body.TimelineName,
                    DurationSeconds = 12,
                    Tracks =
                    [
                        new AutomationTrack
                        {
                            TargetPath = "/ch/01/mix/fader",
                            Keyframes = [ new AutomationKeyframe { TimeSeconds = 0, Value = 0.2f }, new AutomationKeyframe { TimeSeconds = 12, Value = 0.75f } ]
                        }
                    ]
                };
                _ = automation.StartAsync(timeline, ct);
                await WriteJson(res, new { ok = true });
                return;
            }

            res.StatusCode = 404;
            await WriteJson(res, new { error = "Not found" });
        }
        catch (Exception ex)
        {
            _logging.LogError("Local API request failed", ex);
            res.StatusCode = 500;
            await WriteJson(res, new { error = ex.Message });
        }
        finally
        {
            res.Close();
        }
    }

    private bool Authorize(HttpListenerRequest req)
    {
        var token = _settings.LoadAppSettings().ApiToken;
        if (string.IsNullOrWhiteSpace(token)) return true;

        var header = req.Headers["Authorization"];
        if (string.IsNullOrWhiteSpace(header)) return false;
        return header.Equals($"Bearer {token}", StringComparison.Ordinal);
    }

    private static async Task<T> ReadBody<T>(HttpListenerRequest req)
    {
        using var r = new StreamReader(req.InputStream, req.ContentEncoding);
        var json = await r.ReadToEndAsync();
        return JsonSerializer.Deserialize<T>(json) ?? throw new InvalidOperationException("Invalid JSON payload");
    }

    private static async Task WriteJson(HttpListenerResponse res, object data)
    {
        res.ContentType = "application/json";
        var json = JsonSerializer.Serialize(data);
        var bytes = Encoding.UTF8.GetBytes(json);
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes);
    }

    private async Task HandleWsAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        var wsCtx = await ctx.AcceptWebSocketAsync(null);
        var ws = wsCtx.WebSocket;
        var id = Guid.NewGuid().ToString("N");
        _sockets[id] = ws;

        var buffer = new byte[512];
        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var msg = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (msg.MessageType == WebSocketMessageType.Close)
                    break;
            }
        }
        catch
        {
        }
        finally
        {
            _sockets.TryRemove(id, out _);
            try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); } catch { }
            ws.Dispose();
        }
    }

    private void BroadcastEvent<T>(string eventName, T data)
    {
        if (_sockets.IsEmpty) return;

        var payload = new JsonObject
        {
            ["event"] = eventName,
            ["data"] = JsonSerializer.SerializeToNode(data)
        }.ToJsonString();

        var bytes = Encoding.UTF8.GetBytes(payload);
        var dead = new List<string>();

        foreach (var kvp in _sockets)
        {
            var ws = kvp.Value;
            if (ws.State != WebSocketState.Open)
            {
                dead.Add(kvp.Key);
                continue;
            }

            _ = ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted) dead.Add(kvp.Key);
                });
        }

        foreach (var id in dead.Distinct())
            _sockets.TryRemove(id, out _);
    }
}
