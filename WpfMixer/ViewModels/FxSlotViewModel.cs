using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WpfMixer.Models;
using WpfMixer.Services;

namespace WpfMixer.ViewModels;

public sealed class FxSlotViewModel : ObservableObject
{
    private readonly OscClient _osc;
    private bool _suppressOsc;

    public FxSlotModel Model { get; private set; }
    public int SlotIndex => Model.SlotIndex;
    public string SlotLabel => $"FX {SlotIndex}";

    public ObservableCollection<FxParameterViewModel> Parameters { get; } = [];

    private string _fxType;
    public string FxType
    {
        get => _fxType;
        set
        {
            if (!SetProperty(ref _fxType, value)) return;
            Model.FxType = value;
            BuildParametersForType(value, sendDefaultsToMixer: !_suppressOsc);
            if (!_suppressOsc)
                _osc.Send($"/fx/{SlotIndex}/type", value);
        }
    }

    private float _returnLevel;
    public float ReturnLevel
    {
        get => _returnLevel;
        set
        {
            var clamped = Math.Clamp(value, 0f, 1f);
            if (!SetProperty(ref _returnLevel, clamped)) return;
            Model.ReturnLevel = clamped;
            if (!_suppressOsc)
                _osc.Send($"/fxr/{SlotIndex}/mix/fader", clamped);
        }
    }

    private bool _isOn;
    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (!SetProperty(ref _isOn, value)) return;
            Model.IsOn = value;
            if (!_suppressOsc)
                _osc.Send($"/fxr/{SlotIndex}/mix/on", value ? 1 : 0);
        }
    }

    public FxSlotViewModel(FxSlotModel model, OscClient osc)
    {
        Model = model;
        _osc = osc;
        _fxType = model.FxType;
        _returnLevel = model.ReturnLevel;
        _isOn = model.IsOn;
        BuildParametersForType(_fxType, sendDefaultsToMixer: false);
    }

    public void RebindModel(FxSlotModel model)
    {
        Model = model;
        _suppressOsc = true;
        try
        {
            FxType = model.FxType;
            ReturnLevel = model.ReturnLevel;
            IsOn = model.IsOn;
            BuildParametersFromModel();
        }
        finally
        {
            _suppressOsc = false;
        }
    }

    public void ApplyOscMessage(string address, object[] args)
    {
        _suppressOsc = true;
        try
        {
            if (address == $"/fx/{SlotIndex}/type" && args.Length > 0)
            {
                var newType = args[0] switch
                {
                    string s => s,
                    int i => FxCatalog.FromTypeIndex(i),
                    _ => FxType
                };
                FxType = newType;
                return;
            }

            if (address.StartsWith($"/fx/{SlotIndex}/par/", StringComparison.Ordinal))
            {
                var tail = address[(($"/fx/{SlotIndex}/par/").Length)..];
                if (int.TryParse(tail, out int parameterIndex) && args.Length > 0)
                {
                    float value = args[0] switch
                    {
                        float f => f,
                        int i => i,
                        _ => 0f
                    };
                    SetParameterFromOsc(parameterIndex, value);
                }
                return;
            }

            if (address == $"/fxr/{SlotIndex}/mix/fader" && args.Length > 0)
            {
                if (args[0] is float f) ReturnLevel = f;
                else if (args[0] is int i) ReturnLevel = i;
                return;
            }

            if (address == $"/fxr/{SlotIndex}/mix/on" && args.Length > 0)
            {
                if (args[0] is int on) IsOn = on == 1;
                else if (args[0] is bool b) IsOn = b;
            }
        }
        finally
        {
            _suppressOsc = false;
        }
    }

    public void RequestState()
    {
        _osc.Send($"/fx/{SlotIndex}/type");
        _osc.Send($"/fxr/{SlotIndex}/mix/fader");
        _osc.Send($"/fxr/{SlotIndex}/mix/on");
        for (int p = 0; p <= 31; p++)
            _osc.Send($"/fx/{SlotIndex}/par/{p:D2}");
    }

    private void BuildParametersForType(string fxType, bool sendDefaultsToMixer)
    {
        var definitions = FxCatalog.GetParameters(fxType);
        Parameters.Clear();
        Model.Parameters.Clear();

        for (int i = 0; i < definitions.Count; i++)
        {
            var def = definitions[i];
            Model.Parameters[def.Name] = def.DefaultValue;
            var vm = new FxParameterViewModel(i, def.Name, def.DefaultValue, def.Min, def.Max, OnParameterChanged);
            Parameters.Add(vm);

            if (sendDefaultsToMixer)
                _osc.Send($"/fx/{SlotIndex}/par/{i:D2}", def.DefaultValue);
        }
    }

    private void BuildParametersFromModel()
    {
        if (Model.Parameters.Count == 0)
        {
            BuildParametersForType(FxType, sendDefaultsToMixer: false);
            return;
        }

        var existing = Model.Parameters.ToArray();
        Parameters.Clear();
        int i = 0;
        foreach (var kv in existing)
        {
            Parameters.Add(new FxParameterViewModel(i, kv.Key, kv.Value, 0f, 1f, OnParameterChanged));
            i++;
        }
    }

    private void OnParameterChanged(int parameterIndex, float value)
    {
        if (parameterIndex < 0) return;
        _osc.Send($"/fx/{SlotIndex}/par/{parameterIndex:D2}", value);

        if (parameterIndex < Parameters.Count)
            Model.Parameters[Parameters[parameterIndex].Name] = value;
    }

    private void SetParameterFromOsc(int parameterIndex, float value)
    {
        if (parameterIndex < 0) return;

        while (parameterIndex >= Parameters.Count)
        {
            var idx = Parameters.Count;
            var autoName = $"Param {idx + 1}";
            Model.Parameters[autoName] = 0.5f;
            Parameters.Add(new FxParameterViewModel(idx, autoName, 0.5f, 0f, 1f, OnParameterChanged));
        }

        var parameter = Parameters[parameterIndex];
        parameter.SetFromOsc(value);
        Model.Parameters[parameter.Name] = parameter.Value;
    }
}
