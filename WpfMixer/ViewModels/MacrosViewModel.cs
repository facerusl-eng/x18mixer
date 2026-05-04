using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfMixer.Core.Interfaces;
using WpfMixer.Core.Models;

namespace WpfMixer.ViewModels;

public partial class MacrosViewModel : ObservableObject
{
    private readonly IMacroService _macros;

    public ObservableCollection<MacroDefinition> MacroItems => _macros.Macros;

    [ObservableProperty] private MacroDefinition? _selectedMacro;

    public MacrosViewModel(IMacroService macros)
    {
        _macros = macros;
        SelectedMacro = MacroItems.FirstOrDefault();
    }

    [RelayCommand]
    public async Task RunSelectedAsync()
    {
        if (SelectedMacro is null) return;
        await _macros.ExecuteAsync(SelectedMacro);
    }
}
