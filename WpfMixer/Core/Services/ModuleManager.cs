using WpfMixer.Core.Interfaces;
using WpfMixer.Core.Models;

namespace WpfMixer.Core.Services;

public sealed class ModuleManager : IModuleManager
{
    private readonly List<AppModuleDescriptor> _modules =
    [
        new() { Name = "MixerModule", Enabled = true },
        new() { Name = "ProcessingModule", Enabled = true },
        new() { Name = "FxModule", Enabled = true },
        new() { Name = "RoutingModule", Enabled = true },
        new() { Name = "BusMixModule", Enabled = true },
        new() { Name = "SceneModule", Enabled = true },
        new() { Name = "KeyboardModule", Enabled = true },
        new() { Name = "PerformanceModule", Enabled = true },
        new() { Name = "RemoteModule", Enabled = true },
        new() { Name = "PluginModule", Enabled = true },
        new() { Name = "AutomationModule", Enabled = true },
    ];

    public IReadOnlyList<AppModuleDescriptor> Modules => _modules;

    public bool IsEnabled(string moduleName) =>
        _modules.FirstOrDefault(m => m.Name == moduleName)?.Enabled ?? false;

    public void SetEnabled(string moduleName, bool enabled)
    {
        var mod = _modules.FirstOrDefault(m => m.Name == moduleName);
        if (mod is null) return;
        mod.Enabled = enabled;
    }
}
