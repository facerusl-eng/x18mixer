using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfMixer.Models;
using WpfMixer.Services;

namespace WpfMixer.ViewModels;

public sealed partial class SceneManagerViewModel : ObservableObject
{
    private readonly SceneService _sceneService;
    private readonly Func<MixerModel> _getCurrentMixer;
    private readonly Func<SceneModel, Task> _applyScene;

    public ObservableCollection<SceneViewModel> Scenes { get; } = [];

    [ObservableProperty] private SceneViewModel? _selectedScene;
    [ObservableProperty] private string _newSceneName = "scene";
    [ObservableProperty] private string _sceneNotes = string.Empty;
    [ObservableProperty] private string _status = "Ready";

    public string ScenesDirectory => _sceneService.ScenesDirectory;

    public SceneManagerViewModel(SceneService sceneService, Func<MixerModel> getCurrentMixer, Func<SceneModel, Task> applyScene)
    {
        _sceneService = sceneService;
        _getCurrentMixer = getCurrentMixer;
        _applyScene = applyScene;
        RefreshScenes();
    }

    [RelayCommand]
    private void RefreshScenes()
    {
        Scenes.Clear();
        foreach (var path in _sceneService.ListSceneFiles())
        {
            var scene = _sceneService.LoadSceneModel(path);
            if (scene is null) continue;
            Scenes.Add(new SceneViewModel(scene, path));
        }

        SelectedScene = Scenes.OrderByDescending(s => s.Timestamp).FirstOrDefault();
        Status = $"{Scenes.Count} scenes";
    }

    [RelayCommand]
    private void SaveScene()
    {
        var name = string.IsNullOrWhiteSpace(NewSceneName)
            ? $"scene_{DateTime.Now:yyyyMMdd_HHmmss}"
            : NewSceneName.Trim();

        var scene = new SceneModel
        {
            Name = name,
            Notes = SceneNotes,
            Timestamp = DateTime.UtcNow,
            Snapshot = _sceneService.CloneMixer(_getCurrentMixer())
        };

        _sceneService.SaveScene(scene);
        Status = $"Saved {name}";
        RefreshScenes();
    }

    [RelayCommand]
    private async Task LoadScene()
    {
        if (SelectedScene is null) return;

        var scene = _sceneService.LoadSceneModel(SelectedScene.FilePath);
        if (scene is null)
        {
            Status = "Failed to load scene";
            return;
        }

        await _applyScene(scene);
        Status = $"Loaded {scene.Name}";
    }

    [RelayCommand]
    private void DeleteScene()
    {
        if (SelectedScene is null) return;
        _sceneService.DeleteScene(SelectedScene.FilePath);
        Status = $"Deleted {SelectedScene.Name}";
        RefreshScenes();
    }

    [RelayCommand]
    private void RenameScene()
    {
        if (SelectedScene is null) return;
        if (string.IsNullOrWhiteSpace(NewSceneName)) return;

        _sceneService.RenameScene(SelectedScene.FilePath, NewSceneName.Trim());
        Status = $"Renamed to {NewSceneName.Trim()}";
        RefreshScenes();
    }

    [RelayCommand]
    private void ExportScene(string exportFilePath)
    {
        if (SelectedScene is null || string.IsNullOrWhiteSpace(exportFilePath)) return;
        _sceneService.ExportScene(SelectedScene.FilePath, exportFilePath);
        Status = $"Exported to {exportFilePath}";
    }

    [RelayCommand]
    private void ImportScene(string importFilePath)
    {
        if (string.IsNullOrWhiteSpace(importFilePath)) return;
        _sceneService.ImportScene(importFilePath);
        Status = $"Imported {Path.GetFileName(importFilePath)}";
        RefreshScenes();
    }

    [RelayCommand]
    private void CreatePreset(string presetName)
    {
        if (string.IsNullOrWhiteSpace(presetName)) return;
        _sceneService.CreatePreset(presetName.Trim());
        RefreshScenes();
    }
}
