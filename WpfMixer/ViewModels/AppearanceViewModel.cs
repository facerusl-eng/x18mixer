using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfMixer.Models;
using WpfMixer.Services;

namespace WpfMixer.ViewModels;

/// <summary>
/// Exposes theme selection, font scale, and accessibility options to the UI.
/// </summary>
public partial class AppearanceViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private MixerSettings _model;

    // ── Selectable items ──────────────────────────────────────────────────
    public ObservableCollection<ThemeItem> Themes { get; } =
    [
        new("Dark",         AppTheme.Dark),
        new("Light",        AppTheme.Light),
        new("Neon",         AppTheme.Neon),
        new("High Contrast",AppTheme.HighContrast),
    ];

    // ── Observable properties ─────────────────────────────────────────────
    [ObservableProperty] private ThemeItem _selectedTheme;
    [ObservableProperty] private double    _fontScale   = 1.0;
    [ObservableProperty] private bool      _largeText   = false;
    [ObservableProperty] private bool      _reduceMotion = false;
    [ObservableProperty] private bool      _uiSounds     = false;
    [ObservableProperty] private float     _meterAttack  = 0.15f;
    [ObservableProperty] private float     _meterRelease = 0.08f;
    [ObservableProperty] private float     _peakHoldSecs = 2.0f;

    public AppearanceViewModel(SettingsService settings, MixerSettings model)
    {
        _settings     = settings;
        _model        = model;
        _fontScale    = model.FontScale;
        _largeText    = model.LargeText;
        _reduceMotion = model.ReduceMotion;
        _uiSounds     = model.UiSounds;
        _meterAttack  = model.MeterAttack;
        _meterRelease = model.MeterRelease;
        _peakHoldSecs = model.PeakHoldSecs;

        _selectedTheme = Themes.FirstOrDefault(t => t.Name == model.Theme) ?? Themes[0];
    }

    // ── Property-change handlers ─────────────────────────────────────────

    partial void OnSelectedThemeChanged(ThemeItem value)
    {
        ThemeService.Apply(value.Theme);
        _model.Theme = value.Theme.ToString();
        SaveAsync();
    }

    partial void OnFontScaleChanged(double value)
    {
        ApplyFontScale(value);
        _model.FontScale = value;
        SaveAsync();
    }

    partial void OnLargeTextChanged(bool value)
    {
        _model.LargeText = value;
        ApplyFontScale(value ? Math.Max(FontScale, 1.2) : FontScale);
        SaveAsync();
    }

    partial void OnReduceMotionChanged(bool value)
    {
        _model.ReduceMotion = value;
        SaveAsync();
    }

    partial void OnUiSoundsChanged(bool value)
    {
        _model.UiSounds = value;
        SaveAsync();
    }

    partial void OnMeterAttackChanged(float value)  { _model.MeterAttack  = value; SaveAsync(); }
    partial void OnMeterReleaseChanged(float value) { _model.MeterRelease = value; SaveAsync(); }
    partial void OnPeakHoldSecsChanged(float value) { _model.PeakHoldSecs = value; SaveAsync(); }

    // ── Commands ──────────────────────────────────────────────────────────

    [RelayCommand] public void ResetDefaults()
    {
        FontScale    = 1.0;
        LargeText    = false;
        ReduceMotion = false;
        UiSounds     = false;
        MeterAttack  = 0.15f;
        MeterRelease = 0.08f;
        PeakHoldSecs = 2.0f;
        SelectedTheme = Themes[0];
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void ApplyFontScale(double scale)
    {
        scale = Math.Clamp(scale, 0.8, 1.6);
        Application.Current.Resources["GlobalFontSize"] = 12.0 * scale;
    }

    private void SaveAsync()
    {
        try { _settings.Save(_model); }
        catch { }
    }
}

/// <summary>Item in the themes dropdown.</summary>
public record ThemeItem(string Name, AppTheme Theme);
