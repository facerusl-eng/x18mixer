using WpfMixer.Models;

namespace WpfMixer.Services;

public sealed class SceneTransitionService
{
    private readonly OscClient _osc;

    public SceneTransitionService(OscClient osc)
    {
        _osc = osc;
    }

    public async Task ApplySceneAsync(MixerModel current, MixerModel target, int durationMs = 450)
    {
        int ms = Math.Clamp(durationMs, 300, 600);
        int steps = Math.Max(6, ms / 25);

        ApplyInstant(current, target);
        await ApplySmooth(current, target, steps, ms);
    }

    private void ApplyInstant(MixerModel current, MixerModel target)
    {
        for (int i = 0; i < Math.Min(current.InputChannels.Count, target.InputChannels.Count); i++)
        {
            var from = current.InputChannels[i];
            var to = target.InputChannels[i];

            from.Name = to.Name;
            from.ColorHex = to.ColorHex;
            from.IsMuted = to.IsMuted;
            from.IsSoloed = to.IsSoloed;
            from.InputSource = to.InputSource;
            from.AnalogInput = to.AnalogInput;
            from.UsbReturn = to.UsbReturn;
            from.SendToLr = to.SendToLr;
            from.DirectOutSource = to.DirectOutSource;
            from.AssignedKey = to.AssignedKey;
            from.IsMomentaryMute = to.IsMomentaryMute;

            _osc.Send($"{from.OscBase}/config/name", from.Name);
            _osc.Send($"{from.OscBase}/mix/on", from.IsMuted ? 0 : 1);
            _osc.Send($"{from.OscBase}/config/source", from.InputSource switch
            {
                InputSource.Analog => 0,
                InputSource.UsbReturn => 1,
                _ => 2
            });
            _osc.Send($"{from.OscBase}/mix/lr", from.SendToLr ? 1 : 0);
            _osc.Send($"{from.OscBase}/config/directout", (int)from.DirectOutSource);

            for (int s = 0; s < Math.Min(from.BusSends.Count, to.BusSends.Count); s++)
            {
                from.BusSends[s].IsOn = to.BusSends[s].IsOn;
                from.BusSends[s].PrePost = to.BusSends[s].PrePost;
                string path = $"{from.OscBase}/mix/{from.BusSends[s].OscToken}";
                _osc.Send($"{path}/on", from.BusSends[s].IsOn ? 1 : 0);
                _osc.Send($"{path}/pre", from.BusSends[s].IsPre ? 1 : 0);
            }
        }

        for (int i = 0; i < Math.Min(current.Outputs.Count, target.Outputs.Count); i++)
        {
            current.Outputs[i].Source = target.Outputs[i].Source;
            _osc.Send($"{current.Outputs[i].OscBase}/source", current.Outputs[i].OscSourceIndex);
        }

        current.Usb.Mode = target.Usb.Mode;
        _osc.Send("/usb/config/mode", current.Usb.Mode == UsbMode.Multitrack ? 1 : 0);

        for (int i = 0; i < current.Usb.SendAssignments.Length && i < target.Usb.SendAssignments.Length; i++)
        {
            current.Usb.SendAssignments[i] = target.Usb.SendAssignments[i];
            _osc.Send($"/usb/config/send/{i + 1:D2}", current.Usb.SendAssignments[i]);
        }

        for (int i = 0; i < current.Usb.ReturnAssignments.Length && i < target.Usb.ReturnAssignments.Length; i++)
        {
            current.Usb.ReturnAssignments[i] = target.Usb.ReturnAssignments[i];
            _osc.Send($"/usb/config/return/{i + 1:D2}", current.Usb.ReturnAssignments[i]);
        }

        ApplyFxInstant(current, target);
    }

    private async Task ApplySmooth(MixerModel current, MixerModel target, int steps, int durationMs)
    {
        var delay = Math.Max(10, durationMs / steps);

        var fromChVolumes = current.InputChannels.Select(c => c.Volume).ToArray();
        var toChVolumes = target.InputChannels.Select(c => c.Volume).ToArray();
        var fromChPans = current.InputChannels.Select(c => c.Pan).ToArray();
        var toChPans = target.InputChannels.Select(c => c.Pan).ToArray();

        var fromMainVolume = current.MainLR.Volume;
        var toMainVolume = target.MainLR.Volume;

        var fromOutputLevels = current.Outputs.Select(o => o.Level).ToArray();
        var toOutputLevels = target.Outputs.Select(o => o.Level).ToArray();

        for (int step = 1; step <= steps; step++)
        {
            float t = step / (float)steps;
            t = EaseInOutCubic(t);

            for (int i = 0; i < Math.Min(current.InputChannels.Count, target.InputChannels.Count); i++)
            {
                var c = current.InputChannels[i];
                c.Volume = Lerp(fromChVolumes[i], toChVolumes[i], t);
                c.Pan = Lerp(fromChPans[i], toChPans[i], t);
                _osc.Send($"{c.OscBase}/mix/fader", (float)c.Volume);
                _osc.Send($"{c.OscBase}/mix/pan", (float)c.Pan);

                for (int s = 0; s < Math.Min(c.BusSends.Count, target.InputChannels[i].BusSends.Count); s++)
                {
                    var send = c.BusSends[s];
                    var targetSend = target.InputChannels[i].BusSends[s];
                    var baseLevel = send.Level;
                    send.Level = Lerp(baseLevel, targetSend.Level, t);
                    _osc.Send($"{c.OscBase}/mix/{send.OscToken}/level", (float)send.Level);
                }
            }

            current.MainLR.Volume = Lerp(fromMainVolume, toMainVolume, t);
            _osc.Send("/main/st/mix/fader", (float)current.MainLR.Volume);

            for (int i = 0; i < Math.Min(current.Outputs.Count, target.Outputs.Count); i++)
            {
                current.Outputs[i].Level = Lerp(fromOutputLevels[i], toOutputLevels[i], t);
                _osc.Send($"{current.Outputs[i].OscBase}/level", (float)current.Outputs[i].Level);
            }

            ApplyFxSmooth(current, target, t);
            await Task.Delay(delay);
        }
    }

    private static void ApplyFxInstant(MixerModel current, MixerModel target)
    {
        var currentFx = new[] { current.Fx1, current.Fx2, current.Fx3, current.Fx4 };
        var targetFx = new[] { target.Fx1, target.Fx2, target.Fx3, target.Fx4 };

        for (int i = 0; i < 4; i++)
        {
            currentFx[i].FxType = targetFx[i].FxType;
            currentFx[i].IsOn = targetFx[i].IsOn;
            currentFx[i].Parameters = targetFx[i].Parameters.ToDictionary(k => k.Key, v => v.Value);
        }
    }

    private void ApplyFxSmooth(MixerModel current, MixerModel target, float t)
    {
        var currentFx = new[] { current.Fx1, current.Fx2, current.Fx3, current.Fx4 };
        var targetFx = new[] { target.Fx1, target.Fx2, target.Fx3, target.Fx4 };

        for (int i = 0; i < 4; i++)
        {
            int slot = i + 1;
            float fromReturn = currentFx[i].ReturnLevel;
            float toReturn = targetFx[i].ReturnLevel;
            currentFx[i].ReturnLevel = Lerp(fromReturn, toReturn, t);
            _osc.Send($"/fxr/{slot:D2}/mix/fader", currentFx[i].ReturnLevel);
            _osc.Send($"/fxr/{slot:D2}/mix/on", currentFx[i].IsOn ? 1 : 0);

            int paramIndex = 0;
            foreach (var kv in targetFx[i].Parameters)
            {
                currentFx[i].Parameters.TryGetValue(kv.Key, out var fromVal);
                var smoothed = Lerp(fromVal, kv.Value, t);
                currentFx[i].Parameters[kv.Key] = smoothed;
                _osc.Send($"/fx/{slot}/par/{paramIndex:D2}", smoothed);
                paramIndex++;
            }

            _osc.Send($"/fx/{slot}/type", currentFx[i].FxType);
        }
    }

    private static float EaseInOutCubic(float t)
        => t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;

    private static double Lerp(double a, double b, float t) => a + ((b - a) * t);
    private static float Lerp(float a, float b, float t) => a + ((b - a) * t);
}
