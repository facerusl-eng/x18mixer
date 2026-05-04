using System.Collections.ObjectModel;

namespace WpfMixer.Core.Models;

public enum MacroActionType
{
    SendOsc,
    LoadScene,
    RunScript,
    StartAutomation,
}

public sealed class MacroDefinition
{
    public string Name { get; set; } = "Macro";
    public string AssignedKey { get; set; } = string.Empty;
    public ObservableCollection<MacroActionDefinition> Actions { get; set; } = [];
}

public sealed class MacroActionDefinition
{
    public MacroActionType Type { get; set; }
    public string Arg1 { get; set; } = string.Empty;
    public string Arg2 { get; set; } = string.Empty;
    public float Value { get; set; }
}
