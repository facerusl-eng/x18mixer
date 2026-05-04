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

        if (cfg.ScriptSandboxing.UseIsolatedScriptHost)
        {
            var outProc = await ScriptHostProcess.ExecuteOutOfProcessAsync(
                scriptCode,
                cfg.LocalApiPort,
                cfg.ApiToken,
                cfg.ScriptSandboxing.MaxExecutionSeconds,
                _runningScriptCts.Token);

            if (outProc.Success)
            {
                _logging.LogInfo("Script executed successfully (isolated host).");
                return new ScriptExecutionResult(true, outProc.Output);
            }

            _logging.LogError("Isolated script execution failed", new Exception(outProc.Error ?? outProc.Output));
            return new ScriptExecutionResult(false, outProc.Output, outProc.Error is null ? null : new Exception(outProc.Error));
        }

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
        finally
        {
            _runningScriptCts?.Dispose();
            _runningScriptCts = null;
        }
    }

    public void CancelRunningScript()
    {
        _runningScriptCts?.Cancel();
        _runningScriptCts?.Dispose();
        _runningScriptCts = null;
    }
}
