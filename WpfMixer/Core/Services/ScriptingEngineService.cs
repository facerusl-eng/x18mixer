using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.DependencyInjection;
using WpfMixer.Core.Interfaces;
using WpfMixer.Core.Models;
using WpfMixer.Services;
using WpfMixer.ViewModels;

namespace WpfMixer.Core.Services;

public sealed class ScriptingEngineService : IScriptingEngineService
{
    private readonly IServiceProvider _services;
    private readonly OscClient _osc;
    private readonly ILoggingService _logging;
    private readonly ISettingsService _settings;
    private CancellationTokenSource? _runningScriptCts;

    public ScriptingEngineService(
        IServiceProvider services,
        OscClient osc,
        ILoggingService logging,
        ISettingsService settings)
    {
        _services = services;
        _osc = osc;
        _logging = logging;
        _settings = settings;
    }

    public async Task<ScriptExecutionResult> ExecuteAsync(string scriptCode, CancellationToken ct = default)
    {
        CancelRunningScript();
        _runningScriptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var cfg = _settings.LoadAppSettings();
        if (!cfg.ScriptSandboxing.Enabled)
            return new ScriptExecutionResult(false, "Script execution blocked: sandbox disabled in settings.");

        var mixer = _services.GetRequiredService<MixerViewModel>();

        var globals = new ScriptContext
        {
            Mixer = mixer,
            Osc = _osc,
        };

        var options = ScriptOptions.Default
            .AddReferences(typeof(MixerViewModel).Assembly)
            .AddReferences(typeof(Task).Assembly)
            .AddImports("System", "System.Linq", "System.Threading.Tasks", "WpfMixer");

        try
        {
            var state = await CSharpScript.RunAsync(scriptCode, options, globals, cancellationToken: _runningScriptCts.Token)
                .ConfigureAwait(false);

            string output = state.ReturnValue?.ToString() ?? "Script completed.";
            _logging.LogInfo("Script executed successfully.");
            return new ScriptExecutionResult(true, output);
        }
        catch (OperationCanceledException)
        {
            return new ScriptExecutionResult(false, "Script cancelled.");
        }
        catch (Exception ex)
        {
            _logging.LogError("Script execution failed", ex);
            return new ScriptExecutionResult(false, ex.Message, ex);
        }
    }

    public void CancelRunningScript()
    {
        _runningScriptCts?.Cancel();
        _runningScriptCts?.Dispose();
        _runningScriptCts = null;
    }
}
