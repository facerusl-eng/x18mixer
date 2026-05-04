namespace WpfMixer.Core.Interfaces;

public interface IScriptingEngineService
{
    Task<ScriptExecutionResult> ExecuteAsync(string scriptCode, CancellationToken ct = default);
    void CancelRunningScript();
}

public sealed record ScriptExecutionResult(bool Success, string Output, Exception? Exception = null);
