using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfMixer.ViewModels;

public sealed class FxParameterViewModel : ObservableObject
{
    private readonly Action<int, float> _onChanged;
    private bool _suppress;

    public int ParameterIndex { get; }
    public string Name { get; }
    public float Min { get; }
    public float Max { get; }

    private float _value;
    public float Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, Min, Max);
            if (!SetProperty(ref _value, clamped)) return;
            if (!_suppress)
                _onChanged(ParameterIndex, clamped);
        }
    }

    public FxParameterViewModel(int parameterIndex, string name, float value, float min, float max, Action<int, float> onChanged)
    {
        ParameterIndex = parameterIndex;
        Name = name;
        Min = min;
        Max = max;
        _value = Math.Clamp(value, min, max);
        _onChanged = onChanged;
    }

    public void SetFromOsc(float value)
    {
        _suppress = true;
        try { Value = value; }
        finally { _suppress = false; }
    }
}
