using System.Windows;
using System.Windows.Media;

namespace WpfMixer.Services;

/// <summary>
/// Manages runtime theme switching by swapping the active theme
/// ResourceDictionary in <see cref="Application.Resources"/>.
/// </summary>
public static class ThemeService
{
    private const string ThemePrefix = "pack://application:,,,/WpfMixer;component/Themes/";

    private static readonly Dictionary<AppTheme, string> _themePaths = new()
    {
        [AppTheme.Dark]          = ThemePrefix + "Dark.xaml",
        [AppTheme.Light]         = ThemePrefix + "Light.xaml",
        [AppTheme.Neon]          = ThemePrefix + "Neon.xaml",
        [AppTheme.HighContrast]  = ThemePrefix + "HighContrast.xaml",
    };

    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    /// <summary>Switch to the given theme instantly.</summary>
    public static void Apply(AppTheme theme)
    {
        if (theme == CurrentTheme) return;
        CurrentTheme = theme;

        var uri  = new Uri(_themePaths[theme], UriKind.Absolute);
        var dict = new ResourceDictionary { Source = uri };

        var merged = Application.Current.Resources.MergedDictionaries;

        // Replace the existing theme entry (position 0) or insert if absent.
        var existing = merged.FirstOrDefault(d =>
            d.Source?.AbsolutePath.Contains("/Themes/") == true);

        if (existing != null)
        {
            int idx = merged.IndexOf(existing);
            merged.RemoveAt(idx);
            merged.Insert(idx, dict);
        }
        else
        {
            merged.Insert(0, dict);
        }

        // Sync the flat Color/Brush keys that old XAML binds directly.
        SyncLegacyKeys(dict);
    }

    /// <summary>
    /// The existing App.xaml uses flat resource keys (BgDeepColor, Accent, …).
    /// After a theme swap those keys keep their old values unless we update them here.
    /// This syncs all Color/Brush keys from the new theme dict into the app resources.
    /// </summary>
    private static void SyncLegacyKeys(ResourceDictionary newTheme)
    {
        var res = Application.Current.Resources;

        foreach (var key in newTheme.Keys)
        {
            var val = newTheme[key];
            if (val is Color || val is SolidColorBrush || val is CornerRadius || val is double)
            {
                res[key] = val;
            }
        }

        // Rebuild dependent Brush entries from the new Colors.
        RebuildBrushes(res);
    }

    private static void RebuildBrushes(ResourceDictionary res)
    {
        SetBrush(res, "BgDeep",      "BgDeepColor");
        SetBrush(res, "BgPanel",     "BgPanelColor");
        SetBrush(res, "BgStrip",     "BgStripColor");
        SetBrush(res, "BgControl",   "BgControlColor");
        SetBrush(res, "BorderBrush", "BorderColor");
        SetBrush(res, "Accent",      "AccentColor");
        SetBrush(res, "MuteBrush",   "MuteColor");
        SetBrush(res, "SoloBrush",   "SoloColor");
        SetBrush(res, "SelectBrush", "SelectColor");
        SetBrush(res, "TextPrimary",    "TextPrimaryColor");
        SetBrush(res, "TextSecondary",  "TextSecondaryColor");
        SetBrush(res, "FaderTrackBrush","FaderTrackColor");
    }

    private static void SetBrush(ResourceDictionary res, string brushKey, string colorKey)
    {
        if (res[colorKey] is Color c)
            res[brushKey] = new SolidColorBrush(c);
    }

    /// <summary>Load persisted theme choice on startup.</summary>
    public static void LoadSaved()
    {
        var svc   = new SettingsService();
        var saved = svc.Load().Theme;
        if (Enum.TryParse<AppTheme>(saved, out var theme) && theme != AppTheme.Dark)
            Apply(theme);
        else
            CurrentTheme = AppTheme.Dark;
    }
}

public enum AppTheme
{
    Dark,
    Light,
    Neon,
    HighContrast
}
