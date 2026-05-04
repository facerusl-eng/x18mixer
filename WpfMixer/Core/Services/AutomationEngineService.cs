using WpfMixer.Core.Interfaces;
using WpfMixer.Core.Models;
using WpfMixer.Services;

namespace WpfMixer.Core.Services;

public sealed class AutomationEngineService : IAutomationEngineService
{
    private readonly OscClient _osc;
    private readonly IEventBus _eventBus;
    private readonly ILoggingService _logging;
    private CancellationTokenSource? _runCts;

    public bool IsRunning => _runCts is { IsCancellationRequested: false };

    public AutomationEngineService(OscClient osc, IEventBus eventBus, ILoggingService logging)
    {
        _osc = osc;
        _eventBus = eventBus;
        _logging = logging;
    }

    public async Task StartAsync(AutomationTimeline timeline, CancellationToken ct = default)
    {
        Stop();
        _runCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var localCt = _runCts.Token;
        _eventBus.Publish(new AutomationStartedEvent(timeline.Name));
        _logging.LogInfo($"Automation started: {timeline.Name}");

        try
        {
            var start = DateTime.UtcNow;
            int intervalMs = 20; // 50 fps

            while (!localCt.IsCancellationRequested)
            {
                double t = (DateTime.UtcNow - start).TotalSeconds;
                if (t > timeline.DurationSeconds) break;

                foreach (var track in timeline.Tracks)
                {
                    float value = EvaluateTrack(track, t);
                    _osc.Send(track.TargetPath, value);
                }

                await Task.Delay(intervalMs, localCt).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _eventBus.Publish(new AutomationStoppedEvent(timeline.Name));
            _logging.LogInfo($"Automation stopped: {timeline.Name}");
            Stop();
        }
    }

    public void Stop()
    {
        _runCts?.Cancel();
        _runCts?.Dispose();
        _runCts = null;
    }

    private static float EvaluateTrack(AutomationTrack track, double timeSeconds)
    {
        if (track.Keyframes.Count == 0) return 0f;
        var sorted = track.Keyframes.OrderBy(k => k.TimeSeconds).ToList();

        if (timeSeconds <= sorted[0].TimeSeconds) return sorted[0].Value;
        if (timeSeconds >= sorted[^1].TimeSeconds) return sorted[^1].Value;

        for (int i = 0; i < sorted.Count - 1; i++)
        {
            var a = sorted[i];
            var b = sorted[i + 1];
            if (timeSeconds < a.TimeSeconds || timeSeconds > b.TimeSeconds) continue;

            var span = b.TimeSeconds - a.TimeSeconds;
            if (span <= 0.0001) return a.Value;
            var x = (float)((timeSeconds - a.TimeSeconds) / span);

            x = a.Interpolation switch
            {
                AutomationInterpolation.Hold => 0f,
                AutomationInterpolation.EaseIn => x * x,
                AutomationInterpolation.EaseOut => 1f - ((1f - x) * (1f - x)),
                _ => x,
            };

            return a.Value + (b.Value - a.Value) * x;
        }

        return sorted[^1].Value;
    }
}
