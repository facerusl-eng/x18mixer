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

    private System.Timers.Timer? _overlayTimer;

    // ── Core mixer model / viewmodels (clean MVVM layer) ──────────────────────
    public ObservableCollection<ChannelViewModel> ChannelViewModels { get; } = [];
    public MainBusViewModel MainLR { get; }
    public FxRackViewModel FxRack { get; }
    [ObservableProperty] private RoutingViewModel _routing;
    [ObservableProperty] private BusMixViewModel _busMix;
    [ObservableProperty] private MonitorMixViewModel _monitorMix;

    public bool IsSofActive => BusMix.IsSofActive;
    public int SelectedSofBusIndex => BusMix.SelectedBusIndex;

    // ── Routing / scene model (used by routing panel & scene save) ────────────
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
    [ObservableProperty] private string _logText = string.Empty;

    /// <summary>Legacy alias kept for routing panel bindings.</summary>
    public ObservableCollection<Channel> Channels => Mixer.InputChannels;
    public ObservableCollection<MuteGroup> MuteGroups => Mixer.MuteGroups;

    // ── Constructor ──────────────────────────────────────────────────────────
    public MixerViewModel()
    {
        // ── Core ChannelViewModels (18 channels + main LR) ───────────────────
        foreach (var model in ChannelModel.CreateDefaults())
            ChannelViewModels.Add(new ChannelViewModel(model, _osc));
        MainLR = new MainBusViewModel(new MainBusModel(), _osc);
        FxRack = new FxRackViewModel(Mixer, _osc);
        _routing = new RoutingViewModel(Mixer, _osc);
        _busMix = new BusMixViewModel(Mixer, _osc);
        _monitorMix = new MonitorMixViewModel(_busMix);
        WireBusMix(_busMix);

        // ── Keyboard / routing (uses legacy Channel model) ───────────────────
        KeyboardService = new KeyboardService();
        KeyboardService.Bind(Mixer.InputChannels, Mixer.MuteGroups);
        KeyboardService.KeyActionFired += OnKeyActionFired;

        // ── OSC events ───────────────────────────────────────────────────────
        _osc.MessageReceived += OnOscMessage;
        _osc.OnLog += msg => Application.Current.Dispatcher.InvokeAsync(() =>
        {
            LogText = msg;
        });

        // Wire routing-model property changes → OSC (legacy)
        foreach (var ch in Mixer.AllChannels)
            WireChannel(ch);

        Mixer.InputChannels.CollectionChanged += (_, _) => RebindKeyboard();
        Mixer.MuteGroups.CollectionChanged    += (_, _) => RebindKeyboard();
    }

    // ── InitializeAsync: discover → connect → request state ──────────────────

    public async Task InitializeAsync()
    {
        StatusText = "Starting auto-discovery…";

        string? foundIp = null;
        _discovery.MixerFound += (ip, name, model) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foundIp = ip;
                var entry = $"{name} ({model}) @ {ip}";
                if (!DiscoveredMixers.Contains(entry)) DiscoveredMixers.Add(entry);
                StatusText = $"Found: {entry} — connecting…";
                if (DiscoveredMixers.Count == 1)
                    _ = ConnectToAsync(ip);
            });
        };

        await _discovery.DiscoverAsync(3000);
        IsDiscovering = false;

        if (foundIp == null)
            StatusText = "No X Air mixer found. Enter IP manually.";
    }

    /// <summary>
    /// Route an incoming OSC message to the correct ChannelViewModel or MainBus.
    /// Called on any thread; dispatches to UI thread internally.
    /// </summary>
    public void HandleOscMessage(string address, object[] args)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            // ── Core channel VMs ─────────────────────────────────────────────
            foreach (var vm in ChannelViewModels)
            {
                if (address.StartsWith(vm.OscBase))
                {
                    vm.ApplyOscMessage(address, args);
                    return;
                }
            }

            // ── Main LR ──────────────────────────────────────────────────────
            if (address.StartsWith(MainBusModel.OscBase))
            {
                MainLR.ApplyOscMessage(address, args);
                return;
            }

            // ── FX rack ──────────────────────────────────────────────────────
            if (FxRack.ApplyOscMessage(address, args))
                return;

            // ── Bus mix / monitor mix ───────────────────────────────────────
            if (BusMix.ApplyOscMessage(address, args))
                return;

            // ── Dedicated routing VMs (output/USB matrix) ───────────────────
            if (Routing.ApplyOscMessage(address, args))
                return;

            // ── Routing / output model (legacy) ──────────────────────────────
            HandleRoutingOsc(address, args);
        });
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
            await _osc.ConnectAsync(ip);   // heartbeat is started inside ConnectAsync
            Mixer.MixerIp = ip;
            IsConnected = true;
            StatusText = $"Connected to {ip}";
            RequestFullState();
            RequestRoutingState();
        }
        catch (Exception ex)
        {
            StatusText = $"Connection failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Disconnect()
    {
        _osc.Disconnect();   // stops heartbeat internally
        IsConnected = false;
        StatusText = "Disconnected";
    }

    private void RequestFullState()
    {
        // Request state for core channel VMs
        foreach (var vm in ChannelViewModels)
        {
            _osc.Send($"{vm.OscBase}/mix/fader");
            _osc.Send($"{vm.OscBase}/mix/on");
            _osc.Send($"{vm.OscBase}/mix/pan");
            _osc.Send($"{vm.OscBase}/config/name");
        }
        // Main LR
        _osc.Send($"{MainBusModel.OscBase}/mix/fader");
        _osc.Send($"{MainBusModel.OscBase}/mix/on");
        // FX rack
        FxRack.RequestState();
        // Bus mix + monitor state
        BusMix.RequestState();
        // Legacy routing model channels
        foreach (var ch in Mixer.AllChannels)
        {
            _osc.Send($"{ch.OscBase}/config/name");
        }
    }

    // ── Incoming OSC ─────────────────────────────────────────────────────────

    private void OnOscMessage(string address, object[] args)
    {
        // Delegate to the unified handler (routes to ChannelViewModel + legacy routing)
        HandleOscMessage(address, args);
    }

    private void HandleRoutingOsc(string address, object[] args)
    {
        // Channel fader / mute / pan in legacy routing model
        var ch = FindChannelByOscAddress(address);
        if (ch != null)
        {
            if (address.EndsWith("/mix/fader") && args.Length > 0 && args[0] is float f)
                ch.Volume = f;
            else if (address.EndsWith("/mix/on") && args.Length > 0 && args[0] is int on)
                ch.IsMuted = on == 0;
            else if (address.EndsWith("/mix/pan") && args.Length > 0 && args[0] is float p)
                ch.Pan = p;
            else if (address.EndsWith("/config/name") && args.Length > 0 && args[0] is string name)
                ch.Name = name;
            else if (address.EndsWith("/mix/lr") && args.Length > 0 && args[0] is int lr)
                ch.SendToLr = lr == 1;
            else if (address.EndsWith("/config/source") && args.Length > 0 && args[0] is int src)
                ApplyInputSource(ch, src);
            else if (address.EndsWith("/config/directout") && args.Length > 0 && args[0] is int direct)
                ch.DirectOutSource = (OutputSource)Math.Clamp(direct, 0, (int)OutputSource.DirectOut);
            else
                TryApplyBusSend(ch, address, args);
            return;
        }

        // Output routing
        var output = Mixer.Outputs.FirstOrDefault(o => address.StartsWith(o.OscBase));
        if (output != null)
        {
            if (address.EndsWith("/source") && args.Length > 0 && args[0] is int osrc)
                output.Source = (OutputSource)Math.Clamp(osrc, 0, (int)OutputSource.DirectOut);
            else if (address.EndsWith("/level") && args.Length > 0 && args[0] is float ol)
                output.Level = ol;
        }
    }

    private static void ApplyInputSource(Channel ch, int src)
    {
        // Requested routing mode mapping:
        // 0 = Analog, 1 = USB, 2 = Off
        if (src == 0) { ch.InputSource = InputSource.Analog; return; }
        if (src == 1) { ch.InputSource = InputSource.UsbReturn; return; }
        ch.InputSource = InputSource.Off;
    }

    private static void TryApplyBusSend(Channel ch, string address, object[] args)
    {
        foreach (var send in ch.BusSends)
        {
            string path = $"{ch.OscBase}/mix/{send.OscToken}";
            if (address == $"{path}/level" && args.Length > 0 && args[0] is float lv)
                send.Level = lv;
            else if (address == $"{path}/on" && args.Length > 0 && args[0] is int on)
                send.IsOn = on == 1;
            else if (address == $"{path}/pre" && args.Length > 0 && args[0] is int pre)
                send.PrePost = pre == 1 ? PrePost.Pre : PrePost.Post;
        }
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
                case nameof(Channel.SendToLr):
                    _osc.Send($"{ch.OscBase}/mix/lr", ch.SendToLr ? 1 : 0);
                    break;
                case nameof(Channel.InputSource):
                case nameof(Channel.AnalogInput):
                case nameof(Channel.UsbReturn):
                    SendInputSource(ch);
                    break;
                case nameof(Channel.DirectOutSource):
                    _osc.Send($"{ch.OscBase}/config/directout", (int)ch.DirectOutSource);
                    break;
            }
        };

        // Wire bus sends
        foreach (var send in ch.BusSends)
            WireBusSend(ch, send);
    }

    private void WireBusSend(Channel ch, BusSend send)
    {
        send.PropertyChanged += (_, e) =>
        {
            if (!IsConnected) return;
            string path = $"{ch.OscBase}/mix/{send.OscToken}";
            switch (e.PropertyName)
            {
                case nameof(BusSend.Level):
                    _osc.Send($"{path}/level", (float)send.Level);
                    break;
                case nameof(BusSend.IsOn):
                    _osc.Send($"{path}/on", send.IsOn ? 1 : 0);
                    break;
                case nameof(BusSend.PrePost):
                    _osc.Send($"{path}/pre", send.IsPre ? 1 : 0);
                    break;
            }
        };
    }

    private void WireOutput(OutputRoute output)
    {
        output.PropertyChanged += (_, e) =>
        {
            if (!IsConnected) return;
            switch (e.PropertyName)
            {
                case nameof(OutputRoute.Source):
                    _osc.Send($"{output.OscBase}/source", output.OscSourceIndex);
                    break;
                case nameof(OutputRoute.Level):
                    _osc.Send($"{output.OscBase}/level", (float)output.Level);
                    break;
            }
        };
    }

    private void SendInputSource(Channel ch)
    {
        if (!IsConnected) return;
        // Requested routing mode mapping:
        // 0=Analog, 1=USB, 2=Off
        int src = ch.InputSource switch
        {
            InputSource.Analog => 0,
            InputSource.UsbReturn => 1,
            InputSource.Off => 2,
            _ => 2
        };
        _osc.Send($"{ch.OscBase}/config/source", src);
    }

    // ── Routing commands ─────────────────────────────────────────────────────

    [RelayCommand]
    private void SetInputSource(Channel ch) => SendInputSource(ch);

    [RelayCommand]
    private void ToggleBusSendOn(BusSend send) => send.IsOn = !send.IsOn;

    [RelayCommand]
    private void ToggleBusSendPre(BusSend send) =>
        send.PrePost = send.IsPre ? PrePost.Post : PrePost.Pre;

    [RelayCommand]
    private void SetOutputSource(OutputRoute output) =>
        _osc.Send($"{output.OscBase}/source", output.OscSourceIndex);

    // ── Routing: request state from mixer ────────────────────────────────────

    private void RequestRoutingState()
    {
        Routing.RequestState();

        foreach (var ch in Mixer.AllChannels)
        {
            _osc.Send($"{ch.OscBase}/config/source");
            _osc.Send($"{ch.OscBase}/config/directout");
            _osc.Send($"{ch.OscBase}/mix/lr");
            foreach (var s in ch.BusSends)
            {
                string path = $"{ch.OscBase}/mix/{s.OscToken}";
                _osc.Send($"{path}/level");
                _osc.Send($"{path}/on");
                _osc.Send($"{path}/pre");
            }
        }
        foreach (var out_ in Mixer.Outputs)
        {
            _osc.Send($"{out_.OscBase}/source");
            _osc.Send($"{out_.OscBase}/level");
        }
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
        Routing = new RoutingViewModel(Mixer, _osc);
        BusMix = new BusMixViewModel(Mixer, _osc);
        MonitorMix = new MonitorMixViewModel(BusMix);
        WireBusMix(BusMix);
        FxRack.RebindModels(Mixer);
        OnPropertyChanged(nameof(Channels));
        OnPropertyChanged(nameof(MuteGroups));
        OnPropertyChanged(nameof(IsSofActive));
        OnPropertyChanged(nameof(SelectedSofBusIndex));
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

    private void WireBusMix(BusMixViewModel busMix)
    {
        busMix.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BusMixViewModel.IsSofActive)) OnPropertyChanged(nameof(IsSofActive));
            if (e.PropertyName == nameof(BusMixViewModel.SelectedBusIndex)) OnPropertyChanged(nameof(SelectedSofBusIndex));
        };
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
        _osc.Disconnect();   // stops heartbeat
        _sceneService.AutoBackup(Mixer);
    }

    public void Dispose()
    {
        Cleanup();
        _osc.Dispose();
        _discovery.Dispose();
    }
}
