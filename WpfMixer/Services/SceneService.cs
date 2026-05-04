using System.IO;
using System.Text.Json;
using WpfMixer.Models;

namespace WpfMixer.Services;

/// <summary>
/// Saves and loads complete mixer scenes as JSON.
/// Also handles undo/redo snapshots.
/// </summary>
public sealed class SceneService
{
    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true };
    private readonly Stack<string> _undoStack = new();
    private readonly Stack<string> _redoStack = new();

    // ── Scene file I/O ──────────────────────────────────────────────────────

    public void SaveScene(MixerModel model, string filePath)
    {
        var json = JsonSerializer.Serialize(model, _opts);
        File.WriteAllText(filePath, json);
    }

    public MixerModel? LoadScene(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<MixerModel>(json);
    }

    // ── Auto-backup ─────────────────────────────────────────────────────────

    public void AutoBackup(MixerModel model)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WpfMixer", "backups");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        SaveScene(model, path);

        // Keep only 10 most recent backups
        var files = Directory.GetFiles(dir, "backup_*.json")
                             .OrderByDescending(f => f).Skip(10);
        foreach (var f in files) File.Delete(f);
    }

    // ── Undo / redo ─────────────────────────────────────────────────────────

    public void PushUndo(MixerModel model)
    {
        _undoStack.Push(JsonSerializer.Serialize(model, _opts));
        _redoStack.Clear();
    }

    public MixerModel? Undo(MixerModel current)
    {
        if (_undoStack.Count == 0) return null;
        _redoStack.Push(JsonSerializer.Serialize(current, _opts));
        return JsonSerializer.Deserialize<MixerModel>(_undoStack.Pop());
    }

    public MixerModel? Redo(MixerModel current)
    {
        if (_redoStack.Count == 0) return null;
        _undoStack.Push(JsonSerializer.Serialize(current, _opts));
        return JsonSerializer.Deserialize<MixerModel>(_redoStack.Pop());
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    // ── Magic presets ────────────────────────────────────────────────────────

    public static void ApplyPreset(MixerModel model, string preset)
    {
        switch (preset)
        {
            case "SoloSinger":
                ApplyVolumes(model, [0.85, 0.0, 0.0, 0.0, 0.6, 0.6]);
                break;
            case "AcousticDuo":
                ApplyVolumes(model, [0.8, 0.8, 0.0, 0.0, 0.5, 0.5]);
                break;
            case "Karaoke":
                ApplyVolumes(model, [0.9, 0.0, 0.0, 0.75, 0.75, 0.0]);
                break;
        }
    }

    private static void ApplyVolumes(MixerModel model, double[] vols)
    {
        for (int i = 0; i < Math.Min(vols.Length, model.InputChannels.Count); i++)
            model.InputChannels[i].Volume = vols[i];
    }
}
