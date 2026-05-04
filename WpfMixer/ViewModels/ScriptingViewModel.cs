using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfMixer.Core.Interfaces;

namespace WpfMixer.ViewModels;

public partial class ScriptingViewModel : ObservableObject
{
    private readonly IScriptingEngineService _scripting;

    public ObservableCollection<string> ScriptLibrary { get; } =
    [
        "// Raise lead vocal\nawait mixer.SetFaderAsync(6, 0.78f);\nawait Task.Delay(150);\nawait mixer.SetMuteAsync(6, false);",
        "// Smooth intro\nfor (int i = 0; i < 10; i++) { await mixer.SetFaderAsync(1, i / 10f); await Task.Delay(60); }"
    ];

    [ObservableProperty] private string _scriptText = "// Write C# script here";
    [ObservableProperty] private string _outputText = "Ready.";
    [ObservableProperty] private bool _isRunning;

    public ScriptingViewModel(IScriptingEngineService scripting)
    {
        _scripting = scripting;
    }

    [RelayCommand]
    public async Task RunAsync()
    {
        IsRunning = true;
        var result = await _scripting.ExecuteAsync(ScriptText);
        OutputText = result.Output;
        IsRunning = false;
    }

    [RelayCommand]
    public void Stop()
    {
        _scripting.CancelRunningScript();
        IsRunning = false;
        OutputText = "Stopped.";
    }

    [RelayCommand]
    public void LoadFromLibrary(string script)
    {
        ScriptText = script;
    }
}
