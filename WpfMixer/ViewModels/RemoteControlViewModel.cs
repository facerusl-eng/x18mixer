using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRCoder;
using WpfMixer.Models;
using WpfMixer.Services;

namespace WpfMixer.ViewModels;

/// <summary>
/// Desktop-side viewmodel for the REMOTE CONTROL tab.
/// Manages the embedded web server, musician list, QR codes, and token management.
/// </summary>
public partial class RemoteControlViewModel : ObservableObject, IDisposable
{
    private RemoteWebServer? _server;
    private RemoteConfig     _config;
    private readonly OscClient _osc;
    private bool _disposed;

    // ── Observable state ──────────────────────────────────────────────────────
    [ObservableProperty] private bool   _isRunning;
    [ObservableProperty] private string _serverUrl     = string.Empty;
    [ObservableProperty] private string _statusText    = "Server stopped";
    [ObservableProperty] private int    _connectedClients;
    [ObservableProperty] private string _logText       = string.Empty;
    [ObservableProperty] private int    _port          = 8080;

    [ObservableProperty] private MusicianMixViewModel? _selectedMusician;
    [ObservableProperty] private BitmapSource?          _qrCode;
    [ObservableProperty] private string                 _qrUrl = string.Empty;

    public ObservableCollection<MusicianMixViewModel> Musicians { get; } = [];

    // ── Constructor ───────────────────────────────────────────────────────────
    public RemoteControlViewModel(OscClient osc)
    {
        _osc    = osc;
        _config = RemoteConfig.Load();
        Port    = _config.Port;
        RebuildMusicians();
    }

    // ── Server start/stop ────────────────────────────────────────────────────

    [RelayCommand]
    public void StartServer()
    {
        if (IsRunning) return;

        _config.Port = Port;
        _config.Save();

        _server = new RemoteWebServer(_config, _osc);
        _server.OnLog               += AppendLog;
        _server.OnClientConnected   += OnClientConnected;
        _server.OnClientDisconnected += OnClientDisconnected;

        try
        {
            _server.Start();
            IsRunning  = true;
            ServerUrl  = _server.BaseUrl;
            StatusText = $"Running on {_server.LanIp}:{Port}";
            RebuildMusicians(_server.BaseUrl);
            RefreshQrCode();
            AppendLog($"Server started → {_server.BaseUrl}");
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to start: {ex.Message}";
            AppendLog($"[ERROR] {ex.Message}");
            _server.Dispose();
            _server = null;
        }
    }

    [RelayCommand]
    public void StopServer()
    {
        if (!IsRunning) return;
        _server?.Stop();
        _server?.Dispose();
        _server        = null;
        IsRunning      = false;
        ServerUrl      = string.Empty;
        StatusText     = "Server stopped";
        ConnectedClients = 0;
    }

    [RelayCommand]
    public void ToggleServer()
    {
        if (IsRunning) StopServer();
        else           StartServer();
    }

    // ── Musician management ──────────────────────────────────────────────────

    partial void OnSelectedMusicianChanged(MusicianMixViewModel? value)
    {
        RefreshQrCode();
    }

    [RelayCommand]
    public void SelectMusician(MusicianMixViewModel vm)
    {
        SelectedMusician = vm;
    }

    [RelayCommand]
    public void RegenerateToken()
    {
        if (SelectedMusician is null) return;
        SelectedMusician.RegenerateToken();
        _config.Save();
        RefreshQrCode();
        AppendLog($"Token regenerated for {SelectedMusician.Name}");
    }

    [RelayCommand]
    public void CopyLink()
    {
        if (SelectedMusician is null) return;
        try { Clipboard.SetText(SelectedMusician.MixUrl); }
        catch { }
        AppendLog($"Copied: {SelectedMusician.MixUrl}");
    }

    [RelayCommand]
    public void SaveConfig()
    {
        _config.Port = Port;
        _config.Save();
        AppendLog("Config saved.");
    }

    // ── QR code generation ───────────────────────────────────────────────────

    private void RefreshQrCode()
    {
        if (SelectedMusician is null)
        {
            QrCode = null;
            QrUrl  = string.Empty;
            return;
        }

        var url  = SelectedMusician.MixUrl;
        QrUrl    = url;

        try
        {
            var generator = new QRCodeGenerator();
            var data      = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
            var qrCode    = new QRCode(data);

            using var bmp    = qrCode.GetGraphic(6, Color.White, Color.FromArgb(0xFF, 0x0D, 0x0D, 0x0D), drawQuietZones: true);
            using var stream = new MemoryStream();
            bmp.Save(stream, ImageFormat.Png);
            stream.Seek(0, SeekOrigin.Begin);

            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption  = BitmapCacheOption.OnLoad;
            bi.StreamSource = stream;
            bi.EndInit();
            bi.Freeze();
            QrCode = bi;
        }
        catch (Exception ex)
        {
            AppendLog($"[QR] {ex.Message}");
            QrCode = null;
        }
    }

    // ── Client tracking ───────────────────────────────────────────────────────

    private void OnClientConnected(string slug)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            ConnectedClients++;
            var vm = Musicians.FirstOrDefault(m => m.Slug == slug);
            if (vm is not null) { vm.IsOnline = true; vm.ClientCount++; }
            AppendLog($"Client connected: {slug}  ({ConnectedClients} total)");
        });
    }

    private void OnClientDisconnected(string slug)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            ConnectedClients = Math.Max(0, ConnectedClients - 1);
            var vm = Musicians.FirstOrDefault(m => m.Slug == slug);
            if (vm is not null)
            {
                vm.ClientCount = Math.Max(0, vm.ClientCount - 1);
                if (vm.ClientCount == 0) vm.IsOnline = false;
            }
            AppendLog($"Client disconnected: {slug}  ({ConnectedClients} total)");
        });
    }

    // ── OSC bridge: forward received OSC to WS clients ───────────────────────

    public void ForwardOscToClients(string address, object[] args)
    {
        _server?.BroadcastOscUpdate(address, args);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RebuildMusicians(string? baseUrl = null)
    {
        var resolvedBase = baseUrl ?? (IsRunning ? ServerUrl : $"http://0.0.0.0:{Port}");

        Musicians.Clear();
        foreach (var cfg in _config.Musicians)
        {
            var vm = new MusicianMixViewModel(cfg, resolvedBase);
            Musicians.Add(vm);
        }

        SelectedMusician = Musicians.FirstOrDefault();
    }

    private void AppendLog(string msg)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            var lines = LogText.Split('\n');
            if (lines.Length > 200)
                LogText = string.Join('\n', lines.TakeLast(150));
            LogText += $"\n[{DateTime.Now:HH:mm:ss}] {msg}";
        });
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopServer();
    }
}
