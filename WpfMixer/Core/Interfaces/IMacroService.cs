using System.Collections.ObjectModel;
using WpfMixer.Core.Models;

namespace WpfMixer.Core.Interfaces;

public interface IMacroService
{
    ObservableCollection<MacroDefinition> Macros { get; }
    Task ExecuteAsync(MacroDefinition macro, CancellationToken ct = default);
}
