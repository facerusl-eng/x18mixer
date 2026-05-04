using System.Text.Json;
using WpfMixer.Models;

namespace WpfMixer.Services;

public sealed class UndoRedoService
{
    private readonly int _maxEntries;
    private readonly Stack<SceneModel> _undoStack = new();
    private readonly Stack<SceneModel> _redoStack = new();

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = false
    };

    public UndoRedoService(int maxEntries = 50)
    {
        _maxEntries = Math.Max(1, maxEntries);
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void PushSnapshot(MixerModel current, string name = "Snapshot", string? notes = null)
    {
        _undoStack.Push(CreateScene(name, current, notes));
        _redoStack.Clear();
        TrimStack(_undoStack);
    }

    public SceneModel? Undo(MixerModel current)
    {
        if (_undoStack.Count == 0) return null;
        _redoStack.Push(CreateScene("Redo", current));
        return _undoStack.Pop();
    }

    public SceneModel? Redo(MixerModel current)
    {
        if (_redoStack.Count == 0) return null;
        _undoStack.Push(CreateScene("Undo", current));
        return _redoStack.Pop();
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }

    private SceneModel CreateScene(string name, MixerModel model, string? notes = null)
    {
        // Deep clone through JSON keeps scene snapshots immutable.
        var json = JsonSerializer.Serialize(model, _opts);
        var clone = JsonSerializer.Deserialize<MixerModel>(json) ?? MixerModel.CreateDefault();
        return new SceneModel
        {
            Name = name,
            Notes = notes,
            Timestamp = DateTime.UtcNow,
            Snapshot = clone
        };
    }

    private void TrimStack(Stack<SceneModel> stack)
    {
        if (stack.Count <= _maxEntries) return;
        var kept = stack.Take(_maxEntries).ToArray();
        stack.Clear();
        for (int i = kept.Length - 1; i >= 0; i--)
            stack.Push(kept[i]);
    }
}
