#nullable enable
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace PointedHorizons.Services;

public enum AppThemeMode
{
    System,
    Dark,
    Light
}

public sealed class ThemeManager : INotifyPropertyChanged
{
    private static readonly ThemeManager _instance = new();
    public static ThemeManager Current => _instance;

    private AppThemeMode _mode = AppThemeMode.System;
    private bool _isSystemDark;
    private bool _isInitialized;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? ThemeChanged;

    private ThemeManager() { }

    public void Initialize()
    {
        if (_isInitialized) return;

        try
        {
            object? savedObj = OpenSilver.Interop.ExecuteJavaScript(
                "localStorage.getItem('pointedhorizons_theme_mode') || 'System'");
            string saved = savedObj?.ToString() ?? "System";
            if (Enum.TryParse<AppThemeMode>(saved, out var savedMode))
            {
                _mode = savedMode;
            }

            _isSystemDark = QuerySystemDarkTheme();
            RegisterSystemThemeListener();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("ThemeManager Init error: " + ex.Message);
            _isSystemDark = QuerySystemDarkTheme();
        }

        _isInitialized = true;
        NotifyThemeChanged();
    }

    public static bool QuerySystemDarkTheme()
    {
        try
        {
            object? darkObj = OpenSilver.Interop.ExecuteJavaScript(
                "Boolean(window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches)");

            if (darkObj is bool b) return b;
            if (darkObj is int i) return i != 0;
            if (darkObj is double d) return d != 0;
            if (bool.TryParse(darkObj?.ToString(), out var parsedDark)) return parsedDark;
            if (int.TryParse(darkObj?.ToString(), out var parsedInt)) return parsedInt != 0;
        }
        catch
        {
            // Fallback for simulator or non-browser contexts
        }

        return false;
    }

    private void RegisterSystemThemeListener()
    {
        try
        {
            Action<object> callback = (res) =>
            {
                bool isDark = res is bool rb ? rb : (bool.TryParse(res?.ToString(), out var p) && p);
                if (!isDark && int.TryParse(res?.ToString(), out var ri)) isDark = (ri != 0);

                Deployment.Current?.Dispatcher?.BeginInvoke(() =>
                {
                    SetSystemDark(isDark);
                });
            };

            OpenSilver.Interop.ExecuteJavaScriptVoid(@"
                if (window.matchMedia) {
                    var mql = window.matchMedia('(prefers-color-scheme: dark)');
                    var handler = function(e) { $0(e.matches); };
                    if (mql.addEventListener) {
                        mql.addEventListener('change', handler);
                    } else if (mql.addListener) {
                        mql.addListener(handler);
                    }
                }
            ", callback);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("ThemeManager listener registration error: " + ex.Message);
        }
    }

    public void SetSystemDark(bool isDark)
    {
        if (_isSystemDark != isDark)
        {
            _isSystemDark = isDark;
            if (_mode == AppThemeMode.System)
            {
                NotifyThemeChanged();
            }
        }
    }

    public AppThemeMode Mode
    {
        get => _mode;
        set
        {
            if (_mode != value)
            {
                _mode = value;
                try
                {
                    OpenSilver.Interop.ExecuteJavaScriptVoid(
                        $"localStorage.setItem('pointedhorizons_theme_mode', '{_mode}');");
                }
                catch { }

                NotifyThemeChanged();
            }
        }
    }

    public bool IsDark => _mode == AppThemeMode.System ? _isSystemDark : (_mode == AppThemeMode.Dark);

    public void CycleTheme()
    {
        Mode = _mode switch
        {
            AppThemeMode.System => AppThemeMode.Dark,
            AppThemeMode.Dark => AppThemeMode.Light,
            AppThemeMode.Light => AppThemeMode.System,
            _ => AppThemeMode.System
        };
    }

    // Dynamic semantic brushes
    public SolidColorBrush PageBackground => IsDark ? BrushDarkPageBg : BrushLightPageBg;
    public SolidColorBrush HeaderBackground => IsDark ? BrushDarkHeaderBg : BrushLightHeaderBg;
    public SolidColorBrush HeaderBorder => IsDark ? BrushDarkHeaderBorder : BrushLightHeaderBorder;
    public SolidColorBrush MapOverlayBackground => IsDark ? BrushDarkMapOverlayBg : BrushLightMapOverlayBg;
    public SolidColorBrush MapOverlayBorder => IsDark ? BrushDarkMapOverlayBorder : BrushLightMapOverlayBorder;
    public SolidColorBrush SplitBarBackground => IsDark ? BrushDarkSplitBarBg : BrushLightSplitBarBg;
    public SolidColorBrush SplitBarBorder => IsDark ? BrushDarkSplitBarBorder : BrushLightSplitBarBorder;

    public SolidColorBrush TextPrimary => IsDark ? BrushDarkTextPrimary : BrushLightTextPrimary;
    public SolidColorBrush TextSecondary => IsDark ? BrushDarkTextSecondary : BrushLightTextSecondary;
    public SolidColorBrush TextMuted => IsDark ? BrushDarkTextMuted : BrushLightTextMuted;
    public SolidColorBrush Accent => IsDark ? BrushSkyAccentDark : BrushSkyAccentLight;

    public SolidColorBrush ButtonActiveBackground => BrushSkyActive;
    public SolidColorBrush ButtonActiveBorder => BrushSkyActive;
    public SolidColorBrush ButtonActiveForeground => BrushWhite;

    public SolidColorBrush ButtonInactiveBackground => IsDark ? BrushDarkButtonBg : BrushLightButtonBg;
    public SolidColorBrush ButtonInactiveBorder => IsDark ? BrushDarkButtonBorder : BrushLightButtonBorder;
    public SolidColorBrush ButtonInactiveForeground => IsDark ? BrushDarkButtonFg : BrushLightButtonFg;

    public SolidColorBrush CardinalMajorBackground => IsDark ? BrushDarkCardinalMajorBg : BrushLightCardinalMajorBg;
    public SolidColorBrush CardinalMajorBorder => IsDark ? BrushDarkCardinalMajorBorder : BrushLightCardinalMajorBorder;
    public SolidColorBrush CardinalMajorForeground => IsDark ? BrushSkyAccentDark : BrushSkyAccentLight;

    public SolidColorBrush CardinalMinorBackground => IsDark ? BrushDarkButtonBg : BrushLightButtonBg;
    public SolidColorBrush CardinalMinorBorder => IsDark ? BrushDarkButtonBorder : BrushLightButtonBorder;
    public SolidColorBrush CardinalMinorForeground => IsDark ? BrushDarkButtonFg : BrushLightButtonFg;

    // Palette values
    public static readonly SolidColorBrush BrushLightPageBg = new(Color.FromArgb(255, 241, 245, 249));       // Slate 100 (#F1F5F9)
    public static readonly SolidColorBrush BrushDarkPageBg = new(Color.FromArgb(255, 7, 10, 16));            // Deep Slate 950 (#070A10)

    public static readonly SolidColorBrush BrushLightHeaderBg = new(Color.FromArgb(255, 255, 255, 255));     // White (#FFFFFF)
    public static readonly SolidColorBrush BrushDarkHeaderBg = new(Color.FromArgb(255, 11, 15, 25));         // Slate 950 (#0B0F19)

    public static readonly SolidColorBrush BrushLightHeaderBorder = new(Color.FromArgb(255, 226, 232, 240)); // Slate 200 (#E2E8F0)
    public static readonly SolidColorBrush BrushDarkHeaderBorder = new(Color.FromArgb(255, 30, 41, 59));     // Slate 800 (#1E293B)

    // Map Overlays (Location badge & Direction toolbar): Light mode uses translucent white, NOT black!
    public static readonly SolidColorBrush BrushLightMapOverlayBg = new(Color.FromArgb(240, 255, 255, 255)); // Translucent White
    public static readonly SolidColorBrush BrushDarkMapOverlayBg = new(Color.FromArgb(230, 15, 23, 42));     // Translucent Slate 900

    public static readonly SolidColorBrush BrushLightMapOverlayBorder = new(Color.FromArgb(255, 203, 213, 225)); // Slate 300
    public static readonly SolidColorBrush BrushDarkMapOverlayBorder = new(Color.FromArgb(255, 51, 65, 85));    // Slate 700

    public static readonly SolidColorBrush BrushLightSplitBarBg = new(Color.FromArgb(255, 248, 250, 252));   // Slate 50
    public static readonly SolidColorBrush BrushDarkSplitBarBg = new(Color.FromArgb(255, 10, 14, 23));       // Deep Slate

    public static readonly SolidColorBrush BrushLightSplitBarBorder = new(Color.FromArgb(255, 226, 232, 240)); // Slate 200
    public static readonly SolidColorBrush BrushDarkSplitBarBorder = new(Color.FromArgb(255, 30, 41, 59));     // Slate 800

    public static readonly SolidColorBrush BrushLightTextPrimary = new(Color.FromArgb(255, 15, 23, 42));     // Slate 900 (#0F172A)
    public static readonly SolidColorBrush BrushDarkTextPrimary = new(Color.FromArgb(255, 248, 250, 252));   // Slate 50 (#F8FAFC)

    public static readonly SolidColorBrush BrushLightTextSecondary = new(Color.FromArgb(255, 100, 116, 139)); // Slate 500 (#64748B)
    public static readonly SolidColorBrush BrushDarkTextSecondary = new(Color.FromArgb(255, 148, 163, 184)); // Slate 400 (#94A3B8)

    public static readonly SolidColorBrush BrushLightTextMuted = new(Color.FromArgb(255, 148, 163, 184));    // Slate 400 (#94A3B8)
    public static readonly SolidColorBrush BrushDarkTextMuted = new(Color.FromArgb(255, 100, 116, 139));    // Slate 500 (#64748B)

    public static readonly SolidColorBrush BrushSkyAccentDark = new(Color.FromArgb(255, 56, 189, 248));     // Sky 400 (#38BDF8)
    public static readonly SolidColorBrush BrushSkyAccentLight = new(Color.FromArgb(255, 2, 132, 199));     // Sky 600 (#0284C7)
    public static readonly SolidColorBrush BrushSkyActive = new(Color.FromArgb(255, 2, 132, 199));          // Sky 600 (#0284C7)
    public static readonly SolidColorBrush BrushWhite = new(Colors.White);

    public static readonly SolidColorBrush BrushLightButtonBg = new(Color.FromArgb(255, 241, 245, 249));    // Slate 100 (#F1F5F9)
    public static readonly SolidColorBrush BrushDarkButtonBg = new(Color.FromArgb(255, 30, 41, 59));        // Slate 800 (#1E293B)

    public static readonly SolidColorBrush BrushLightButtonBorder = new(Color.FromArgb(255, 203, 213, 225)); // Slate 300 (#CBD5E1)
    public static readonly SolidColorBrush BrushDarkButtonBorder = new(Color.FromArgb(255, 51, 65, 85));    // Slate 700 (#334155)

    public static readonly SolidColorBrush BrushLightButtonFg = new(Color.FromArgb(255, 15, 23, 42));       // Slate 900 (#0F172A)
    public static readonly SolidColorBrush BrushDarkButtonFg = new(Color.FromArgb(255, 248, 250, 252));     // Slate 50 (#F8FAFC)

    public static readonly SolidColorBrush BrushLightCardinalMajorBg = new(Color.FromArgb(255, 224, 242, 254)); // Sky 100 (#E0F2FE)
    public static readonly SolidColorBrush BrushLightCardinalMajorBorder = new(Color.FromArgb(255, 186, 230, 253)); // Sky 200 (#BAE6FD)

    public static readonly SolidColorBrush BrushDarkCardinalMajorBg = new(Color.FromArgb(255, 15, 39, 68));       // Deep Sky/Navy
    public static readonly SolidColorBrush BrushDarkCardinalMajorBorder = new(Color.FromArgb(255, 2, 132, 199));  // Sky 600

    private void NotifyThemeChanged()
    {
        UpdateApplicationResources(IsDark);

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDark)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PageBackground)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HeaderBackground)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HeaderBorder)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MapOverlayBackground)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MapOverlayBorder)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SplitBarBackground)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SplitBarBorder)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextPrimary)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextSecondary)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Accent)));
        ThemeChanged?.Invoke(this, EventArgs.Empty);

        try
        {
            string hostBg = IsDark ? "#070A10" : "#F1F5F9";
            string themeClass = IsDark ? "dark-theme" : "light-theme";
            OpenSilver.Interop.ExecuteJavaScriptVoid($@"
                if (document && document.body) {{
                    document.body.style.backgroundColor = '{hostBg}';
                    document.body.classList.remove('dark-theme', 'light-theme');
                    document.body.classList.add('{themeClass}');
                }}
            ");
        }
        catch { }
    }

    public static void UpdateApplicationResources(bool isDark)
    {
        try
        {
            var app = Application.Current;
            if (app?.Resources?.MergedDictionaries != null)
            {
                app.Resources.MergedDictionaries.Clear();
                if (isDark)
                {
                    app.Resources.MergedDictionaries.Add(new Themes.DarkTheme());
                }
                else
                {
                    app.Resources.MergedDictionaries.Add(new Themes.LightTheme());
                }
            }

            if (app?.Theme is OpenSilver.Themes.Modern.ModernTheme modernTheme)
            {
                var targetPalette = isDark
                    ? OpenSilver.Themes.Modern.ModernTheme.Palettes.Dark
                    : OpenSilver.Themes.Modern.ModernTheme.Palettes.Light;

                if (modernTheme.CurrentPalette != targetPalette)
                {
                    modernTheme.CurrentPalette = targetPalette;
                    app.Theme = modernTheme;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("UpdateApplicationResources error: " + ex.Message);
        }
    }

    public static SolidColorBrush GetBrush(string resourceKey, SolidColorBrush fallback)
    {
        try
        {
            if (Application.Current?.Resources != null &&
                Application.Current.Resources.Contains(resourceKey) &&
                Application.Current.Resources[resourceKey] is SolidColorBrush brush)
            {
                return brush;
            }
        }
        catch { }

        return fallback;
    }
}
