using System.IO;
using System.Text.Json;
using WpfMixer.Core.Helpers;
using WpfMixer.Models;

namespace WpfMixer.Services;

public sealed class SceneService
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true
    };

    public string RootDirectory { get; }
    public string ScenesDirectory { get; }
    public string BackupsDirectory { get; }
    public string PresetsDirectory { get; }

    public SceneService()
    {
        AppPaths.EnsureDirectories();
        RootDirectory = AppPaths.Data;

        ScenesDirectory = AppPaths.Scenes;
        BackupsDirectory = AppPaths.Backups;
        PresetsDirectory = Path.Combine(ScenesDirectory, "Presets");

        Directory.CreateDirectory(ScenesDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(PresetsDirectory);
        EnsureBuiltInPresets();
    }

    public string SaveScene(SceneModel scene)
    {
        var safeName = SanitizeFileName(scene.Name);
        var file = Path.Combine(ScenesDirectory, $"{safeName}.json");
        var json = JsonSerializer.Serialize(scene, _opts);
        File.WriteAllText(file, json);
        return file;
    }

    // Compatibility overload used by older UI actions.
    public void SaveScene(MixerModel model, string filePath)
    {
        var scene = new SceneModel
        {
            Name = Path.GetFileNameWithoutExtension(filePath),
            Timestamp = DateTime.UtcNow,
            Snapshot = CloneMixer(model)
        };
        var json = JsonSerializer.Serialize(scene, _opts);
        File.WriteAllText(filePath, json);
    }

    public SceneModel? LoadSceneModel(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        try
        {
            var json = File.ReadAllText(filePath);
            var scene = JsonSerializer.Deserialize<SceneModel>(json, _opts);
            if (scene?.Snapshot == null) return null;
            scene.Snapshot = RepairMixer(scene.Snapshot);
            return scene;
        }
        catch
        {
            return null;
        }
    }

    // Compatibility overload used by older workflow.
    public MixerModel? LoadScene(string filePath)
        => LoadSceneModel(filePath)?.Snapshot;

    public IEnumerable<string> ListSceneFiles()
        => Directory.GetFiles(ScenesDirectory, "*.json")
                    .Where(path => !path.StartsWith(BackupsDirectory, StringComparison.OrdinalIgnoreCase))
                    .Where(path => !path.StartsWith(PresetsDirectory, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(File.GetLastWriteTimeUtc);

    public void DeleteScene(string filePath)
    {
        if (File.Exists(filePath)) File.Delete(filePath);
    }

    public void RenameScene(string filePath, string newName)
    {
        if (!File.Exists(filePath)) return;
        var dir = Path.GetDirectoryName(filePath) ?? ScenesDirectory;
        var target = Path.Combine(dir, $"{SanitizeFileName(newName)}.json");
        if (string.Equals(filePath, target, StringComparison.OrdinalIgnoreCase)) return;
        File.Move(filePath, target, overwrite: true);

        var scene = LoadSceneModel(target);
        if (scene is null) return;
        scene.Name = newName;
        File.WriteAllText(target, JsonSerializer.Serialize(scene, _opts));
    }

    public void ExportScene(string sourcePath, string exportPath)
    {
        if (!File.Exists(sourcePath)) return;
        File.Copy(sourcePath, exportPath, overwrite: true);
    }

    public void ImportScene(string importPath)
    {
        if (!File.Exists(importPath)) return;
        var name = Path.GetFileName(importPath);
        var target = Path.Combine(ScenesDirectory, name);
        File.Copy(importPath, target, overwrite: true);
    }

    public async Task AutoBackupAsync(MixerModel model, string reason = "exit")
    {
        await Task.Run(() =>
        {
            var scene = new SceneModel
            {
                Name = $"backup_{reason}_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
                Timestamp = DateTime.UtcNow,
                Notes = $"Auto backup ({reason})",
                Snapshot = CloneMixer(model)
            };

            var file = Path.Combine(BackupsDirectory, $"{SanitizeFileName(scene.Name)}.json");
            File.WriteAllText(file, JsonSerializer.Serialize(scene, _opts));

            var old = Directory.GetFiles(BackupsDirectory, "backup_*.json")
                               .OrderByDescending(File.GetCreationTimeUtc)
                               .Skip(20)
                               .ToList();

            foreach (var stale in old)
            {
                try { File.Delete(stale); } catch { }
            }
        });
    }

    public void AutoBackup(MixerModel model)
        => _ = AutoBackupAsync(model);

    public Task BackupBeforeLoadAsync(MixerModel model)
        => AutoBackupAsync(model, "before_load");

    public MixerModel CloneMixer(MixerModel model)
    {
        var json = JsonSerializer.Serialize(model, _opts);
        return RepairMixer(JsonSerializer.Deserialize<MixerModel>(json, _opts) ?? MixerModel.CreateDefault());
    }

    public void CreatePreset(string presetName)
    {
        var scene = BuildPresetScene(presetName);
        var path = Path.Combine(PresetsDirectory, $"{SanitizeFileName(scene.Name)}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(scene, _opts));
    }

    public static void ApplyPreset(MixerModel model, string preset)
    {
        switch (preset)
        {
            case "SoloSinger":
                ApplyVolumes(model, [0.86, 0.12, 0.08, 0.05, 0.42, 0.36]);
                NameChannels(model, ["Vox", "Gtr", "Spare", "Spare"]);
                break;
            case "AcousticDuo":
                ApplyVolumes(model, [0.78, 0.74, 0.64, 0.0, 0.36, 0.3]);
                NameChannels(model, ["Vox A", "Vox B", "Gtr A", "Gtr B"]);
                break;
            case "Karaoke":
                ApplyVolumes(model, [0.82, 0.82, 0.0, 0.0, 0.74, 0.0]);
                NameChannels(model, ["Mic 1", "Mic 2", "Mic 3", "Mic 4"]);
                break;
            case "FullBand":
                ApplyVolumes(model, [0.75, 0.73, 0.72, 0.71, 0.68, 0.64, 0.66, 0.69]);
                NameChannels(model, ["Kick", "Snare", "OH L", "OH R", "Bass", "Gtr", "Keys", "Lead Vox"]);
                break;
            case "DjSet":
                ApplyVolumes(model, [0.0, 0.0, 0.0, 0.0, 0.82, 0.82]);
                NameChannels(model, ["Deck L", "Deck R", "Mic", "Sampler"]);
                break;
        }
    }

    private void EnsureBuiltInPresets()
    {
        var presets = new[]
        {
            "Solo Singer",
            "Acoustic Duo",
            "Karaoke Night",
            "Full Band",
            "DJ Set"
        };

        foreach (var preset in presets)
        {
            var path = Path.Combine(PresetsDirectory, $"{SanitizeFileName(preset)}.json");
            if (File.Exists(path)) continue;
            var scene = BuildPresetScene(preset);
            File.WriteAllText(path, JsonSerializer.Serialize(scene, _opts));
        }
    }

    private SceneModel BuildPresetScene(string presetName)
    {
        var model = MixerModel.CreateDefault();
        ApplyPreset(model, presetName.Replace(" ", "", StringComparison.OrdinalIgnoreCase) switch
        {
            "SoloSinger" => "SoloSinger",
            "AcousticDuo" => "AcousticDuo",
            "KaraokeNight" => "Karaoke",
            "FullBand" => "FullBand",
            "DjSet" => "DjSet",
            _ => "SoloSinger"
        });

        // Generic bus and FX setup baseline for presets.
        foreach (var channel in model.InputChannels)
        {
            foreach (var send in channel.BusSends)
            {
                if (send.BusIndex <= 2)
                {
                    send.Level = 0.55;
                    send.IsOn = true;
                    send.PrePost = PrePost.Pre;
                }
                else if (send.BusIndex is >= 7 and <= 10)
                {
                    send.Level = 0.24;
                    send.IsOn = true;
                    send.PrePost = PrePost.Post;
                }
            }
        }

        model.Fx1.FxType = "Hall Reverb";
        model.Fx2.FxType = "Delay";
        model.Fx3.FxType = "Chorus";
        model.Fx4.FxType = "Graphic EQ";

        return new SceneModel
        {
            Name = presetName,
            Timestamp = DateTime.UtcNow,
            Notes = "Built-in preset",
            Snapshot = model
        };
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Where(ch => !invalid.Contains(ch)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "scene" : safe;
    }

    private static void ApplyVolumes(MixerModel model, double[] vols)
    {
        for (int i = 0; i < Math.Min(vols.Length, model.InputChannels.Count); i++)
            model.InputChannels[i].Volume = vols[i];
    }

    private static void NameChannels(MixerModel model, string[] names)
    {
        for (int i = 0; i < Math.Min(names.Length, model.InputChannels.Count); i++)
            model.InputChannels[i].Name = names[i];
    }

    private static MixerModel RepairMixer(MixerModel model)
    {
        // Ensure derived collections exist after loading older files.
        model.BusMixModels ??= [];
        model.FxReturnMixModels ??= [];
        if (model.BusMixModels.Count == 0)
            for (int b = 1; b <= 6; b++) model.BusMixModels.Add(new BusMixModel(b, 18));
        if (model.FxReturnMixModels.Count == 0)
            for (int f = 1; f <= 4; f++) model.FxReturnMixModels.Add(new FxReturnMixModel(f, 18));

        model.OutputRoutingModel ??= new OutputRoutingModel();
        model.UsbRoutingModel ??= new UsbRoutingModel();
        model.OutputRoutingModel.Outputs = model.Outputs;
        model.UsbRoutingModel.Mode = model.Usb.Mode;
        model.UsbRoutingModel.SendAssignments = model.Usb.SendAssignments;
        model.UsbRoutingModel.ReturnAssignments = model.Usb.ReturnAssignments;

        return model;
    }
}
