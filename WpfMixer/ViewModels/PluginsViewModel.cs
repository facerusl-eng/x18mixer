using System.Collections.ObjectModel;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfMixer.Core.Interfaces;

namespace WpfMixer.ViewModels;

public partial class PluginsViewModel : ObservableObject
{
    private readonly IPluginHostService _plugins;

    public ObservableCollection<IMixerPlugin> Plugins { get; } = [];

    [ObservableProperty] private IMixerPlugin? _selectedPlugin;
    [ObservableProperty] private UserControl? _selectedPanel;

    public PluginsViewModel(IPluginHostService plugins)
    {
        _plugins = plugins;
        Refresh();
    }

    [RelayCommand]
    public void Refresh()
    {
        Plugins.Clear();
        foreach (var p in _plugins.LoadedPlugins)
            Plugins.Add(p);

        SelectedPlugin = Plugins.FirstOrDefault();
    }

    partial void OnSelectedPluginChanged(IMixerPlugin? value)
    {
        SelectedPanel = value?.GetPanel();
    }
}
