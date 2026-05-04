namespace WpfMixer.Models;

/// <summary>FX slot state for X Air engine slots 1..4.</summary>
public sealed class FxSlotModel
{
    public int SlotIndex { get; set; }
    public string FxType { get; set; } = FxCatalog.FxTypes[0];
    public Dictionary<string, float> Parameters { get; set; } = new();
    public float ReturnLevel { get; set; } = 0.75f;
    public bool IsOn { get; set; } = true;

    public FxSlotModel() { }

    public FxSlotModel(int slotIndex)
    {
        SlotIndex = slotIndex;
        ResetParametersForType(FxType);
    }

    public void ResetParametersForType(string fxType)
    {
        FxType = fxType;
        Parameters.Clear();
        foreach (var p in FxCatalog.GetParameters(fxType))
            Parameters[p.Name] = p.DefaultValue;
    }
}
