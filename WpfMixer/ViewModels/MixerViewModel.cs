using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WpfMixer.Models;
using WpfMixer.Services;

namespace WpfMixer.ViewModels;

public partial class MixerViewModel : ObservableObject, IDisposable
{
    // ── Services ────────────────────────────────────────────────────────────
    private readonly OscClient _osc = new();
    private readonly DiscoveryService _discovery = new();
    private readonly SceneService _sceneService = new();
    public readonly KeyboardService KeyboardService;

    private System.Timers.Timer? _heartbeatTimer;
    private System.Timers.Timer? _overlayTimer;

    // ── Model ────────────────────────────────────────────────────────────────
    [ObservableProperty] private MixerModel _mixer = MixerModel.CreateDefault();
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _statusText = "Not connected";
    [ObservableProperty] private bool _isDiscovering;
    [ObservableProperty] private bool _showKeyOverlay;
    [ObservableProperty] private string _overlayText = string.Empty;
    [ObservableProperty] private bool _isPerformanceMode;
    [ObservableProperty] private bool _isPanicMuted;
    [ObservableProperty] private string _mixerIpInput = string.Empty;
    [ObservableProperty] private ObservableCollection<string> _discoveredMixers = [];

    public ObservableCollection<Channel> Channels => Mixer.InputChannels;
    public ObservableCollection<MuteGroup> MuteGroups => Mixer.MuteGroups;

    // ── Constructor ──────────────────────────────────────────────────────────
    public MixerViewModel()
    {
        KeyboardService = new KeyboardService();
        KeyboardService.Bind(Mixer.InputChannels, Mixer.MuteGroups);
        KeyboardService.KeyActionFired += OnKeyActionFired;

        _osc.MessageReceived += OnOscMessage;

        // Wire channel property changes → OSC sends
        foreach (var ch in Mixer.AllChannels)
            WireChannel(ch);

        Mixer.InputChannels.CollectionChanged += (_, _) => RebindKeyboard();
        Mixer.MuteGroups.CollectionChanged    += (_, _) => RebindKeyboard();
    }

    // ── Discovery ────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task DiscoverAsync()
    {
        IsDiscovering = true;
        StatusText = "Discovering…";
        DiscoveredMixers.Clear();

        _discovery.MixerFound += (ip, name, model) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var entry = $"{name} ({model}) @ {ip}";
                if (!DiscoveredMixers.Contains(entry))
                    DiscoveredMixers.Add(entry);
                StatusText = $"Found: {entry}";
                // Auto-connect to the first one
                if (DiscoveredMixers.Count == 1)
                    _ = ConnectToAsync(ip);
            });
        };

        await _discovery.DiscoverAsync(3000);
        IsDiscovering = false;
        if (DiscoveredMixers.Count == 0) StatusText = "No mixer found on network.";
    }

    [RelayCommand]
    private async Task ConnectManualAsync()
    {
        if (string.IsNullOrWhiteSpace(MixerIpInput)) return;
        await ConnectToAsync(MixerIpInput.Trim());
    }

    private async Task ConnectToAsync(string ip)
    {
        try
        {
            await _osc.ConnectAsync(ip);
            Mixer.MixerIp = ip;
            IsConnected = true;
            StatusText = $"Connected to {ip}";
            StartHeartbeat();
            RequestFullState();
        }
        catch (Exception ex)
        {
            StatusText = $"Connection failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Disconnect()
    {
        _heartbeatTimer?.Stop();
        _osc.Disconnect();
        IsConnected = false;
        StatusText = "Disconnected";
    }

    // ── OSC heartbeat ────────────────────────────────────────────────────────

    private void StartHeartbeat()
    {
        _heartbeatTimer?.Stop();
        _heartbeatTimer = new System.Timers.Timer(5000) { AutoReset = true };
        _heartbeatTimer.Elapsed += (_, _) => _osc.Send("/xremote");
        _heartbeatTimer.Start();
    }

    private void RequestFullState()
    {
        // Ask mixer to dump all fader/mute values
        _osc.Send("/xremote");
        foreach (var ch in Mixer.AllChannels)
        {
            _osc.Send($"{ch.OscBase}/mix/fader");
            _osc.Send($"{ch.OscBase}/mix/on");
            _osc.Send($"{ch.OscBase}/mix/pan");
            _osc.Send($"{ch.OscBase}/config/name");
        }
    }

    // ── Incoming OSC ─────────────────────────────────────────────────────────

    private void OnOscMessage(string address, object[] args)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            // /ch/XX/mix/fader  or /lr/mix/fader
            var ch = FindChannelByOscAddress(address);
            if (ch == null) return;

            if (address.EndsWith("/mix/fader") && args.Length > 0 && args[0] is float f)
                ch.Volume = f;
            else if (address.EndsWith("/mix/on") && args.Length > 0 && args[0] is int on)
                ch.IsMuted = on == 0;   // X Air: 0 = muted, 1 = live
            else if (address.EndsWith("/mix/pan") && args.Length > 0 && args[0] is float p)
                ch.Pan = p;
            else if (address.EndsWith("/config/name") && args.Length > 0 && args[0] is string name)
                ch.Name = name;
        });
    }

    private Channel? FindChannelByOscAddress(string address)
    {
        foreach (var ch in Mixer.AllChannels)
            if (address.StartsWith(ch.OscBase)) return ch;
        return null;
    }

    // ── Outgoing OSC (called by WireChannel) ─────────────────────────────────

    private void WireChannel(Channel ch)
    {
        ch.PropertyChanged += (_, e) =>
        {
            if (!IsConnected) return;
            switch (e.PropertyName)
            {
                case nameof(Channel.Volume):
                    _osc.Send($"{ch.OscBase}/mix/fader", (float)ch.Volume);
                    break;
                case nameof(Channel.IsMuted):
                    _osc.Send($"{ch.OscBase}/mix/on", ch.IsMuted ? 0 : 1);
                    break;
                case nameof(Channel.Pan):
                    _osc.Send($"{ch.OscBase}/mix/pan", (float)ch.Pan);
                    break;
            }
        };
    }

    // ── Mute / Solo / Select commands ────────────────────────────────────────

    [RelayCommand]
    private void ToggleMute(Channel ch)
    {
        ch.IsMuted = !ch.IsMuted;
    }

    [RelayCommand]
    private void ToggleSolo(Channel ch)
    {
        bool wasOn = ch.IsSoloed;
        // Exclusive solo: clear all others first
        foreach (var c in Mixer.AllChannels) c.IsSoloed = false;
        ch.IsSoloed = !wasOn;
    }

    [RelayCommand]
    private void SelectChannel(Channel ch)
    {
        foreach (var c in Mixer.AllChannels) c.IsSelected = false;
        ch.IsSelected = true;
    }

    // ── Panic mute ───────────────────────────────────────────────────────────

    [RelayCommand]
    private void PanicMute()
    {
        IsPanicMuted = !IsPanicMuted;
        foreach (var ch in Mixer.InputChannels)
            ch.IsMuted = IsPanicMuted;
    }

    // ── Mute groups ──────────────────────────────────────────────────────────

    [RelayCommand]
    private void AddMuteGroup()
    {
        Mixer.MuteGroups.Add(new MuteGroup { Name = $"Group {Mixer.MuteGroups.Count + 1}" });
        RebindKeyboard();
    }

    [RelayCommand]
    private void RemoveMuteGroup(MuteGroup grp)
    {
        Mixer.MuteGroups.Remove(grp);
        RebindKeyboard();
    }

    [RelayCommand]
    private void ActivateMuteGroup(MuteGroup grp)
    {
        var groupChannels = Mixer.InputChannels.Where(c => grp.ChannelIds.Contains(c.Id)).ToList();
        bool anyUnmuted = groupChannels.Any(c => !c.IsMuted);
        foreach (var c in groupChannels) c.IsMuted = anyUnmuted;
        grp.IsActive = anyUnmuted;
    }

    // ── Key assignment ────────────────────────────────────────────────────────

    public string? AssignKeyToChannel(Channel ch, string keyStr, bool forceAssign = false)
    {
        var conflict = KeyboardService.FindConflict(keyStr, excludeChannelId: ch.Id);
        if (conflict != null && !forceAssign) return conflict;
        ch.AssignedKey = string.IsNullOrWhiteSpace(keyStr) ? null : keyStr;
        return null;
    }

    public string? AssignKeyToGroup(MuteGroup grp, string keyStr, bool forceAssign = false)
    {
        var conflict = KeyboardService.FindConflict(keyStr, excludeGroupId: grp.Id);
        if (conflict != null && !forceAssign) return conflict;
        grp.AssignedKey = string.IsNullOrWhiteSpace(keyStr) ? null : keyStr;
        return null;
    }

    // ── Scene I/O ────────────────────────────────────────────────────────────

    [RelayCommand]
    private void SaveScene()
    {
        var dlg = new SaveFileDialog { Filter = "Scene (*.json)|*.json", FileName = "scene.json" };
        if (dlg.ShowDialog() == true) _sceneService.SaveScene(Mixer, dlg.FileName);
    }

    [RelayCommand]
    private void LoadScene()
    {
        var dlg = new OpenFileDialog { Filter = "Scene (*.json)|*.json" };
        if (dlg.ShowDialog() != true) return;
        var loaded = _sceneService.LoadScene(dlg.FileName);
        if (loaded == null) return;
        Mixer = loaded;
        OnPropertyChanged(nameof(Channels));
        OnPropertyChanged(nameof(MuteGroups));
        RebindKeyboard();
    }

    [RelayCommand] private void Undo() { var m = _sceneService.Undo(Mixer); if (m != null) { Mixer = m; RebindKeyboard(); } }
    [RelayCommand] private void Redo() { var m = _sceneService.Redo(Mixer); if (m != null) { Mixer = m; RebindKeyboard(); } }

    // ── Preset ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private void ApplyPreset(string name) => SceneService.ApplyPreset(Mixer, name);

    // ── Performance mode ─────────────────────────────────────────────────────

    [RelayCommand]
    private void TogglePerformanceMode() => IsPerformanceMode = !IsPerformanceMode;

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void RebindKeyboard()
    {
        KeyboardService.Bind(Mixer.InputChannels, Mixer.MuteGroups);
    }

    private void OnKeyActionFired(string key, string description)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            OverlayText = $"[{key}]  {description}";
            ShowKeyOverlay = true;
            _overlayTimer?.Stop();
            _overlayTimer = new System.Timers.Timer(1500) { AutoReset = false };
            _overlayTimer.Elapsed += (_, _) =>
                Application.Current.Dispatcher.Invoke(() => ShowKeyOverlay = false);
            _overlayTimer.Start();
        });
    }

    public void Cleanup()
    {
        _heartbeatTimer?.Stop();
        _osc.Disconnect();
        _sceneService.AutoBackup(Mixer);
    }

    public void Dispose()
    {
        Cleanup();
        _osc.Dispose();
        _discovery.Dispose();
    }
}
