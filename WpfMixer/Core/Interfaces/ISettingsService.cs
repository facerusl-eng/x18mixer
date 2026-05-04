using WpfMixer.Core.Models;

namespace WpfMixer.Core.Interfaces;

public interface ISettingsService
{
    AppSettings CurrentAppSettings { get; }
    AppSettings LoadAppSettings();
    void SaveAppSettings(AppSettings settings);
    void SaveAppSettings();
}
