using System.Windows.Controls;

namespace WpfMixer.Core.Interfaces;

public interface IMixerPlugin
{
    string Name { get; }
    void Initialize(IServiceProvider services);
    UserControl GetPanel();
}
