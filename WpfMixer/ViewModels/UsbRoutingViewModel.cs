using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WpfMixer.Models;
using WpfMixer.Services;

namespace WpfMixer.ViewModels;

public sealed partial class UsbRoutingViewModel : ObservableObject
{
    private readonly OscClient _osc;
    private readonly MixerModel _mixer;

    public MixerModel Mixer => _mixer;

    [ObservableProperty] private UsbMode _mode;

    public IReadOnlyList<int> RouteOptions { get; } = Enumerable.Range(0, 19).ToArray();

    public ObservableCollection<UsbRouteSlotViewModel> SendSlots { get; } = [];
    public ObservableCollection<UsbRouteSlotViewModel> ReturnSlots { get; } = [];

    public UsbRoutingViewModel(MixerModel mixer, OscClient osc)
    {
        _mixer = mixer;
        _osc = osc;

        _mode = mixer.UsbRoutingModel.Mode;

        for (int i = 0; i < 18; i++)
        {
            int slot = i;
            SendSlots.Add(new UsbRouteSlotViewModel(
                slot + 1,
                mixer.UsbRoutingModel.SendAssignments[slot],
                RouteOptions,
                value => SetSendAssignment(slot, value)));

            ReturnSlots.Add(new UsbRouteSlotViewModel(
                slot + 1,
                mixer.UsbRoutingModel.ReturnAssignments[slot],
                RouteOptions,
                value => SetReturnAssignment(slot, value)));
        }
    }

    partial void OnModeChanged(UsbMode value)
    {
        _mixer.Usb.Mode = value;
        _mixer.UsbRoutingModel.Mode = value;
        _osc.Send("/usb/config/mode", value == UsbMode.Multitrack ? 1 : 0);
    }

    public void SetSendAssignment(int slotIndex, int channelIndex)
    {
        if (slotIndex < 0 || slotIndex >= SendSlots.Count) return;
        SendSlots[slotIndex].SetFromModel(channelIndex);
        _mixer.Usb.SendAssignments[slotIndex] = channelIndex;
        _mixer.UsbRoutingModel.SendAssignments[slotIndex] = channelIndex;
        _osc.Send($"/usb/config/send/{slotIndex + 1:D2}", channelIndex);
    }

    public void SetReturnAssignment(int channelSlot, int usbReturn)
    {
        if (channelSlot < 0 || channelSlot >= ReturnSlots.Count) return;
        ReturnSlots[channelSlot].SetFromModel(usbReturn);
        _mixer.Usb.ReturnAssignments[channelSlot] = usbReturn;
        _mixer.UsbRoutingModel.ReturnAssignments[channelSlot] = usbReturn;
        _osc.Send($"/usb/config/return/{channelSlot + 1:D2}", usbReturn);
    }

    public void RequestState()
    {
        _osc.Send("/usb/config/mode");
        for (int i = 1; i <= 18; i++)
        {
            _osc.Send($"/usb/config/send/{i:D2}");
            _osc.Send($"/usb/config/return/{i:D2}");
        }
    }

    public bool ApplyOscMessage(string address, object[] args)
    {
        if (!address.StartsWith("/usb/config", StringComparison.Ordinal)) return false;

        if (address == "/usb/config/mode" && args.Length > 0 && args[0] is int mode)
            Mode = mode == 1 ? UsbMode.Multitrack : UsbMode.Stereo;
        else if (address.StartsWith("/usb/config/send/", StringComparison.Ordinal) && args.Length > 0 && args[0] is int send)
        {
            var token = address.Substring("/usb/config/send/".Length);
            if (int.TryParse(token, out int idx)) SetSendAssignment(idx - 1, send);
        }
        else if (address.StartsWith("/usb/config/return/", StringComparison.Ordinal) && args.Length > 0 && args[0] is int ret)
        {
            var token = address.Substring("/usb/config/return/".Length);
            if (int.TryParse(token, out int idx)) SetReturnAssignment(idx - 1, ret);
        }

        return true;
    }
}

public sealed partial class UsbRouteSlotViewModel : ObservableObject
{
    private readonly Action<int> _onChanged;
    private bool _suppress;

    public int SlotIndex { get; }
    public string Label => $"{SlotIndex:D2}";
    public IReadOnlyList<int> Options { get; }

    [ObservableProperty] private int _value;

    public UsbRouteSlotViewModel(int slotIndex, int value, IReadOnlyList<int> options, Action<int> onChanged)
    {
        SlotIndex = slotIndex;
        _value = value;
        Options = options;
        _onChanged = onChanged;
    }

    partial void OnValueChanged(int value)
    {
        if (_suppress) return;
        _onChanged(value);
    }

    public void SetFromModel(int value)
    {
        _suppress = true;
        try
        {
            Value = value;
        }
        finally
        {
            _suppress = false;
        }
    }
}
