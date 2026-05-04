using System.Collections.ObjectModel;
using WpfMixer.Core.Interfaces;
using WpfMixer.Core.Models;
using WpfMixer.Services;

namespace WpfMixer.Core.Services;

public sealed class MacroService : IMacroService
{
    private readonly ObservableCollection<MacroDefinition> _macros = [];
    private readonly OscClient _osc;
    private readonly SceneService _scene;
    private readonly IScriptingEngineService _scripting;
    private readonly IAutomationEngineService _automation;
    private readonly ILoggingService _logging;

    public ObservableCollection<MacroDefinition> Macros => _macros;

    public MacroService(
        OscClient osc,
        SceneService scene,
        IScriptingEngineService scripting,
        IAutomationEngineService automation,
        ILoggingService logging)
    {
        _osc = osc;
        _scene = scene;
        _scripting = scripting;
        _automation = automation;
        _logging = logging;

        SeedDefaults();
    }

    public async Task ExecuteAsync(MacroDefinition macro, CancellationToken ct = default)
    {
        foreach (var action in macro.Actions)
        {
            if (ct.IsCancellationRequested) break;
            switch (action.Type)
            {
                case MacroActionType.SendOsc:
                    if (float.TryParse(action.Arg2, out var fv))
                        _osc.Send(action.Arg1, fv);
                    else
                        _osc.Send(action.Arg1, action.Arg2);
                    break;
                case MacroActionType.LoadScene:
                    _scene.LoadSceneModel(action.Arg1);
                    break;
                case MacroActionType.RunScript:
                    await _scripting.ExecuteAsync(action.Arg1, ct).ConfigureAwait(false);
                    break;
                case MacroActionType.StartAutomation:
                    var timeline = new AutomationTimeline
                    {
                        Name = string.IsNullOrWhiteSpace(action.Arg1) ? "Macro Timeline" : action.Arg1,
                        DurationSeconds = Math.Max(1, action.Value),
                        Tracks =
                        [
                            new AutomationTrack
                            {
                                TargetPath = "/ch/01/mix/fader",
                                Keyframes = [ new AutomationKeyframe { TimeSeconds = 0, Value = 0.2f }, new AutomationKeyframe { TimeSeconds = Math.Max(1, action.Value), Value = 0.75f } ]
                            }
                        ]
                    };
                    await _automation.StartAsync(timeline, ct).ConfigureAwait(false);
                    break;
            }
        }

        _logging.LogInfo($"Macro executed: {macro.Name}");
    }

    private void SeedDefaults()
    {
        _macros.Add(new MacroDefinition
        {
            Name = "Fix my vocal",
            Actions =
            [
                new MacroActionDefinition { Type = MacroActionType.SendOsc, Arg1 = "/ch/06/mix/fader", Arg2 = "0.75" },
                new MacroActionDefinition { Type = MacroActionType.SendOsc, Arg1 = "/ch/06/mix/on", Arg2 = "1" },
            ]
        });

        _macros.Add(new MacroDefinition
        {
            Name = "Reset FX",
            Actions =
            [
                new MacroActionDefinition { Type = MacroActionType.SendOsc, Arg1 = "/fx/1/par/01", Arg2 = "0" },
                new MacroActionDefinition { Type = MacroActionType.SendOsc, Arg1 = "/fx/2/par/01", Arg2 = "0" },
            ]
        });
    }
}
