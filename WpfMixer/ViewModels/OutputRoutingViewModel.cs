using System.Collections.ObjectModel;
using WpfMixer.Models;
using WpfMixer.Services;

namespace WpfMixer.ViewModels;

public sealed class OutputRoutingViewModel
{
    private readonly OscClient _osc;
    public ObservableCollection<OutputRoute> Outputs { get; }

    public OutputRoutingViewModel(MixerModel mixer, OscClient osc)
    {
        _osc = osc;
        Outputs = mixer.OutputRoutingModel.Outputs.Count > 0 ? mixer.OutputRoutingModel.Outputs : mixer.Outputs;

        foreach (var output in Outputs)
        {
            output.PropertyChanged += (_, e) =>
            {
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
    }

    public void RequestState()
    {
        foreach (var output in Outputs)
        {
            _osc.Send($"{output.OscBase}/source");
            _osc.Send($"{output.OscBase}/level");
        }
    }

    public bool ApplyOscMessage(string address, object[] args)
    {
        var output = Outputs.FirstOrDefault(o => address.StartsWith(o.OscBase, StringComparison.Ordinal));
        if (output is null) return false;

        if (address.EndsWith("/source", StringComparison.Ordinal) && args.Length > 0 && args[0] is int src)
            output.Source = (OutputSource)Math.Clamp(src, 0, (int)OutputSource.DirectOut);
        else if (address.EndsWith("/level", StringComparison.Ordinal) && args.Length > 0)
        {
            if (args[0] is float f) output.Level = f;
            if (args[0] is int i) output.Level = i;
        }

        return true;
    }
}
