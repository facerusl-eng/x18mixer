using WpfMixer.Core.Models;

namespace WpfMixer.Core.Interfaces;

public interface IModuleManager
{
    IReadOnlyList<AppModuleDescriptor> Modules { get; }
    bool IsEnabled(string moduleName);
    void SetEnabled(string moduleName, bool enabled);
}
