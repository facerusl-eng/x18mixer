using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfMixer.Models;
using WpfMixer.Services;

namespace WpfMixer.ViewModels;

public sealed partial class FxRackViewModel : ObservableObject
{
    private readonly OscClient _osc;

    public FxSlotViewModel Fx1 { get; }
    public FxSlotViewModel Fx2 { get; }
    public FxSlotViewModel Fx3 { get; }
    public FxSlotViewModel Fx4 { get; }

    public ObservableCollection<FxSlotViewModel> Slots { get; }
    public IReadOnlyList<string> AvailableFxTypes => FxCatalog.FxTypes;

    [ObservableProperty] private FxSlotViewModel _selectedFxSlot;

    public FxRackViewModel(MixerModel mixer, OscClient osc)
    {
        _osc = osc;

        Fx1 = new FxSlotViewModel(mixer.Fx1 ?? new FxSlotModel(1), _osc);
        Fx2 = new FxSlotViewModel(mixer.Fx2 ?? new FxSlotModel(2), _osc);
        Fx3 = new FxSlotViewModel(mixer.Fx3 ?? new FxSlotModel(3), _osc);
        Fx4 = new FxSlotViewModel(mixer.Fx4 ?? new FxSlotModel(4), _osc);

        Slots =
        [
            Fx1,
            Fx2,
            Fx3,
            Fx4
        ];

        _selectedFxSlot = Fx1;
    }

    [RelayCommand]
    private void SelectSlot(FxSlotViewModel? slot)
    {
        if (slot is not null)
            SelectedFxSlot = slot;
    }

    public void RequestState()
    {
        foreach (var slot in Slots)
            slot.RequestState();
    }

    public bool ApplyOscMessage(string address, object[] args)
    {
        if (!address.StartsWith("/fx/", StringComparison.Ordinal) &&
            !address.StartsWith("/fxr/", StringComparison.Ordinal))
            return false;

        foreach (var slot in Slots)
        {
            if (address.StartsWith($"/fx/{slot.SlotIndex}/", StringComparison.Ordinal) ||
                address.StartsWith($"/fxr/{slot.SlotIndex}/", StringComparison.Ordinal))
            {
                slot.ApplyOscMessage(address, args);
                return true;
            }
        }

        return true;
    }

    public void RebindModels(MixerModel mixer)
    {
        Fx1.RebindModel(mixer.Fx1 ?? new FxSlotModel(1));
        Fx2.RebindModel(mixer.Fx2 ?? new FxSlotModel(2));
        Fx3.RebindModel(mixer.Fx3 ?? new FxSlotModel(3));
        Fx4.RebindModel(mixer.Fx4 ?? new FxSlotModel(4));

        SelectedFxSlot = Slots.Contains(SelectedFxSlot) ? SelectedFxSlot : Fx1;
    }
}
