using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace WpfMixer.Core.Services;

public static class ScriptHostProcess
{
    private sealed record ScriptHostResult(bool Success, string Output, string? Error);

    public static bool IsScriptHostMode(string[] args) =>
        args.Length >= 3 && string.Equals(args[0], "--script-host", StringComparison.OrdinalIgnoreCase);

    public static async Task<int> RunAsScriptHostAsync(string[] args)
    {
        string base64Script = args[1];
        string outputFile = args[2];

        var port = Environment.GetEnvironmentVariable("WPFMIXER_API_PORT") ?? "8090";
        var token = Environment.GetEnvironmentVariable("WPFMIXER_API_TOKEN") ?? "";

        var api = new ScriptHostApi(int.Parse(port), token);
        string script = Encoding.UTF8.GetString(Convert.FromBase64String(base64Script));

        var options = ScriptOptions.Default
            .AddReferences(typeof(Task).Assembly)
            .AddImports("System", "System.Threading.Tasks");

        var globals = new ScriptGlobals { mixer = api, api = api };

        ScriptHostResult result;
        try
        {
            var state = await CSharpScript.RunAsync(script, options, globals);
            var output = state.ReturnValue?.ToString() ?? "Script completed.";
            result = new ScriptHostResult(true, output, null);
        }
        catch (Exception ex)
        {
            result = new ScriptHostResult(false, ex.Message, ex.ToString());
        }

        await File.WriteAllTextAsync(outputFile, JsonSerializer.Serialize(result));
        return result.Success ? 0 : 1;
    }

    public static async Task<(bool Success, string Output, string? Error)> ExecuteOutOfProcessAsync(
        string script,
        int apiPort,
        string apiToken,
        int timeoutSeconds,
        CancellationToken ct)
    {
        string exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Process path unavailable.");

        string outputFile = Path.Combine(Path.GetTempPath(), $"wpfmixer-script-{Guid.NewGuid():N}.json");
        string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(script));

        using var p = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"--script-host {b64} \"{outputFile}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        p.StartInfo.Environment["WPFMIXER_API_PORT"] = apiPort.ToString();
        p.StartInfo.Environment["WPFMIXER_API_TOKEN"] = apiToken;

        p.Start();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)));

        try
        {
            await p.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { if (!p.HasExited) p.Kill(true); } catch { }
            return (false, "Script timed out.", "timeout");
        }

        if (!File.Exists(outputFile))
            return (false, "Script host did not produce output.", "no-output");

        var text = await File.ReadAllTextAsync(outputFile, ct);
        try { File.Delete(outputFile); } catch { }

        var parsed = JsonSerializer.Deserialize<ScriptHostResult>(text);
        return parsed is null
            ? (false, "Script host returned invalid output.", text)
            : (parsed.Success, parsed.Output, parsed.Error);
    }

    public sealed class ScriptGlobals
    {
        public required ScriptHostApi mixer { get; init; }
        public required ScriptHostApi api { get; init; }
    }

    public sealed class ScriptHostApi
    {
        private readonly HttpClient _http;

        public ScriptHostApi(int apiPort, string apiToken)
        {
            _http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{apiPort}/") };
            if (!string.IsNullOrWhiteSpace(apiToken))
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        }

        public async Task<string> GetStateAsync()
        {
            return await _http.GetStringAsync("api/mixer/state");
        }

        public Task SetFaderAsync(int channel, float value) =>
            _http.PostAsJsonAsync("api/mixer/fader", new { Channel = channel, Value = value });

        public Task SetMuteAsync(int channel, bool isMuted) =>
            _http.PostAsJsonAsync("api/mixer/mute", new { Channel = channel, IsMuted = isMuted });

        public Task LoadSceneAsync(string path) =>
            _http.PostAsJsonAsync("api/mixer/scene/load", new { Path = path });

        public Task StartAutomationAsync(string timelineName) =>
            _http.PostAsJsonAsync("api/mixer/automation/start", new { TimelineName = timelineName });

        public Task SendOscAsync(string address, string arg) =>
            _http.PostAsJsonAsync("api/mixer/osc", new { Address = address, Arg = arg });
    }
}
