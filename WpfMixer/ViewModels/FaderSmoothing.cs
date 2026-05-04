namespace WpfMixer.ViewModels;

/// <summary>
/// Shared motorized-fader easing used for OSC-driven UI updates.
/// </summary>
internal static class FaderSmoothing
{
    // Tuned for "motorized" feel: fast pickup, soft settle, no jitter.
    private const int MaxFrames = 18;
    private const int FrameDelayMs = 14;
    private const float Stiffness = 0.55f;
    private const float Damping = 0.62f;
    private const float SnapDistance = 0.0015f;
    private const float SnapVelocity = 0.0008f;

    public static async Task AnimateAsync(
        Func<float> getCurrent,
        Action<float> setValue,
        float target,
        Func<bool> isCancelled)
    {
        float current = getCurrent();
        float velocity = 0f;

        for (int i = 0; i < MaxFrames && !isCancelled(); i++)
        {
            var delta = target - current;
            velocity = (velocity + delta * Stiffness) * Damping;
            current += velocity;

            if (Math.Abs(target - current) < SnapDistance && Math.Abs(velocity) < SnapVelocity)
            {
                setValue(target);
                return;
            }

            setValue(current);
            await Task.Delay(FrameDelayMs);
        }

        if (!isCancelled())
            setValue(target);
    }
}
