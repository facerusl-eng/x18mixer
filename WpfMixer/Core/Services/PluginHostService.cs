using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using WpfMixer.Core.Interfaces;

namespace WpfMixer.Core.Services;

public sealed class PluginHostService : IPluginHostService
{
    private readonly IServiceProvider _services;
    private readonly ILoggingService _logging;
    private readonly ISettingsService _settings;
    private readonly ObservableCollection<IMixerPlugin> _loaded = [];

    public ReadOnlyObservableCollection<IMixerPlugin> LoadedPlugins { get; }

    public PluginHostService(IServiceProvider services, ILoggingService logging, ISettingsService settings)
    {
        _services = services;
        _logging = logging;
        _settings = settings;
        LoadedPlugins = new ReadOnlyObservableCollection<IMixerPlugin>(_loaded);
    }

    public void LoadPlugins()
    {
        _loaded.Clear();

        var cfg = _settings.LoadAppSettings();
        if (!cfg.PluginPermissions.AllowExternalPlugins)
        {
            _logging.LogWarning("Plugin loading skipped: external plugins disabled by settings.");
            return;
        }

        var pluginDir = Path.Combine(AppContext.BaseDirectory, "Plugins");
        Directory.CreateDirectory(pluginDir);

        foreach (var dll in Directory.GetFiles(pluginDir, "*.dll"))
        {
            try
            {
                var asm = Assembly.LoadFrom(dll);
                foreach (var type in asm.GetTypes().Where(t => typeof(IMixerPlugin).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (!IsAllowedByPermissions(type, cfg.PluginPermissions))
                    {
                        _logging.LogWarning($"Plugin blocked by permissions: {type.FullName}");
                        continue;
                    }

                    if (Activator.CreateInstance(type) is not IMixerPlugin plugin) continue;
                    plugin.Initialize(_services);
                    _loaded.Add(plugin);
                    _logging.LogInfo($"Plugin loaded: {plugin.Name}");
                }
            }
            catch (Exception ex)
            {
                _logging.LogError($"Failed to load plugin assembly: {dll}", ex);
            }
        }
    }

    public void UnloadPlugins()
    {
        _loaded.Clear();
    }

    private static bool IsAllowedByPermissions(Type pluginType, Core.Models.PluginPermissions permissions)
    {
        bool isUi = typeof(IUiPanelPlugin).IsAssignableFrom(pluginType);
        bool isRemote = typeof(IRemoteEndpointPlugin).IsAssignableFrom(pluginType);
        bool isAutomation = typeof(IAutomationHookPlugin).IsAssignableFrom(pluginType);
        bool known = isUi || isRemote || isAutomation;

        if (isUi && !permissions.AllowPluginUiPanels) return false;
        if (isRemote && !permissions.AllowPluginRemoteEndpoints) return false;
        if (isAutomation && !permissions.AllowPluginAutomationHooks) return false;

        // Strict mode: unknown-capability plugins are blocked when any capability is disabled.
        if (!known)
        {
            bool anyCapabilityRestricted =
                !permissions.AllowPluginUiPanels ||
                !permissions.AllowPluginRemoteEndpoints ||
                !permissions.AllowPluginAutomationHooks;

            if (anyCapabilityRestricted)
                return false;
        }

        return true;
    }
}
