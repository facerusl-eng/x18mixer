using System.Collections.ObjectModel;

namespace WpfMixer.Core.Interfaces;

public interface IPluginHostService
{
    ReadOnlyObservableCollection<IMixerPlugin> LoadedPlugins { get; }
    void LoadPlugins();
    void UnloadPlugins();
}
