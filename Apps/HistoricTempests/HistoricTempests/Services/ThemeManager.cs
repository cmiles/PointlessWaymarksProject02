using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace HistoricTempests.Services;

public enum AppThemeMode
{
    System,
    Dark,
    Light
}

public enum AppFontScale
{
    Small,      // 85%
    Normal,     // 100%
    Large,      // 115%
    ExtraLarge  // 130%
}

public sealed class ThemeManager : INotifyPropertyChanged
{
    private static readonly ThemeManager _instance = new();
    public static ThemeManager Current => _instance;

    private AppThemeMode _mode = AppThemeMode.System;
    private double _fontScalePercent = 150.0;
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
                "localStorage.getItem('ht_theme_mode') || 'System'");
            string saved = savedObj?.ToString() ?? "System";
            if (Enum.TryParse<AppThemeMode>(saved, out var savedMode))
            {
                _mode = savedMode;
            }

            object? savedFontObj = OpenSilver.Interop.ExecuteJavaScript(
                "localStorage.getItem('ht_font_scale_percent_v2') || '150'");
            string savedFont = savedFontObj?.ToString() ?? "150";
            if (double.TryParse(savedFont, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedPercent))
            {
                _fontScalePercent = Math.Clamp(parsedPercent, 50.0, 300.0);
            }
            else if (Enum.TryParse<AppFontScale>(savedFont, out var savedScale))
            {
                _fontScalePercent = savedScale switch
                {
                    AppFontScale.Small => 85.0,
                    AppFontScale.Normal => 100.0,
                    AppFontScale.Large => 115.0,
                    AppFontScale.ExtraLarge => 130.0,
                    _ => 150.0
                };
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
            // Non-browser / test environment fallback
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
                        $"localStorage.setItem('ht_theme_mode', '{_mode}');");
                }
                catch { }

                NotifyThemeChanged();
            }
        }
    }

    public bool IsDark => _mode == AppThemeMode.System ? _isSystemDark : (_mode == AppThemeMode.Dark);

    public string ThemeButtonLabel => _mode switch
    {
        AppThemeMode.System => IsDark ? "System (Dark)" : "System (Light)",
        AppThemeMode.Dark => "Dark",
        AppThemeMode.Light => "Light",
        _ => "Theme"
    };

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

    public double FontScalePercent
    {
        get => _fontScalePercent;
        set
        {
            double clamped = Math.Clamp(Math.Round(value), 50.0, 300.0);
            if (Math.Abs(_fontScalePercent - clamped) > 0.01)
            {
                _fontScalePercent = clamped;
                try
                {
                    OpenSilver.Interop.ExecuteJavaScriptVoid(
                        $"localStorage.setItem('ht_font_scale_percent_v2', '{_fontScalePercent.ToString(System.Globalization.CultureInfo.InvariantCulture)}'); localStorage.setItem('ht_font_scale_percent', '{_fontScalePercent.ToString(System.Globalization.CultureInfo.InvariantCulture)}');");
                }
                catch { }

                NotifyThemeChanged();
            }
        }
    }

    public double FontScaleFactor => _fontScalePercent / 100.0;

    public AppFontScale FontScale
    {
        get => _fontScalePercent switch
        {
            <= 90.0 => AppFontScale.Small,
            <= 107.0 => AppFontScale.Normal,
            <= 122.0 => AppFontScale.Large,
            _ => AppFontScale.ExtraLarge
        };
        set => FontScalePercent = value switch
        {
            AppFontScale.Small => 85.0,
            AppFontScale.Normal => 100.0,
            AppFontScale.Large => 115.0,
            AppFontScale.ExtraLarge => 130.0,
            _ => 100.0
        };
    }

    public string FontScaleLabel => $"Font: {_fontScalePercent:F0}%";

    public void CycleFontScale()
    {
        FontScalePercent = _fontScalePercent switch
        {
            < 95.0 => 100.0,
            < 110.0 => 115.0,
            < 125.0 => 130.0,
            _ => 85.0
        };
    }

    // Dynamic Typography Scale Tokens (50% to 300%)
    public double FontSizeSmall => Math.Max(6.0, Math.Round(11.0 * FontScaleFactor, 1));
    public double FontSizeLegend => Math.Max(6.0, Math.Round(11.0 * FontScaleFactor, 1));
    public double FontSizeRegular => Math.Max(7.0, Math.Round(12.0 * FontScaleFactor, 1));
    public double FontSizeBody => Math.Max(7.5, Math.Round(13.0 * FontScaleFactor, 1));
    public double FontSizeMedium => Math.Max(8.0, Math.Round(14.0 * FontScaleFactor, 1));
    public double FontSizeSubheading => Math.Max(9.0, Math.Round(16.0 * FontScaleFactor, 1));
    public double FontSizeHeading => Math.Max(11.0, Math.Round(20.0 * FontScaleFactor, 1));
    public double FontSizeTitle => Math.Max(14.0, Math.Round(26.0 * FontScaleFactor, 1));

    // Dynamic semantic brushes
    public SolidColorBrush PageBackground => IsDark ? BrushDarkPageBg : BrushLightPageBg;
    public SolidColorBrush CardBackground => IsDark ? BrushDarkCardBg : BrushLightCardBg;
    public SolidColorBrush CardBorder => IsDark ? BrushDarkCardBorder : BrushLightCardBorder;
    public SolidColorBrush SubCardBackground => IsDark ? BrushDarkSubCardBg : BrushLightSubCardBg;
    public SolidColorBrush SubCardBorder => IsDark ? BrushDarkSubCardBorder : BrushLightSubCardBorder;

    public SolidColorBrush TextPrimary => IsDark ? BrushDarkTextPrimary : BrushLightTextPrimary;
    public SolidColorBrush TextSecondary => IsDark ? BrushDarkTextSecondary : BrushLightTextSecondary;
    public SolidColorBrush TextMuted => IsDark ? BrushDarkTextMuted : BrushLightTextMuted;

    public SolidColorBrush HeaderBackground => IsDark ? BrushDarkSubCardBg : BrushLightCardBg;
    public SolidColorBrush HeaderBorder => IsDark ? BrushDarkCardBorder : BrushLightCardBorder;
    public SolidColorBrush HeaderTitle => IsDark ? BrushDarkTextPrimary : BrushLightTextPrimary;
    public SolidColorBrush HeaderSubtitle => IsDark ? BrushDarkTextSecondary : BrushLightSubtitle;
    public SolidColorBrush HeaderAccent => IsDark ? BrushSkyAccentDark : BrushSkyAccentLight;
    public SolidColorBrush HeaderBullet => IsDark ? BrushDarkCardBorder : BrushLightSubCardBorder;

    public SolidColorBrush GridLineBrush => IsDark ? BrushDarkGridLine : BrushLightGridLine;
    public SolidColorBrush AxisTextBrush => IsDark ? BrushDarkAxisText : BrushLightAxisText;
    public SolidColorBrush FutureBandBrush => IsDark ? BrushDarkFutureBand : BrushLightFutureBand;
    public SolidColorBrush DividerBrush => IsDark ? BrushDarkDivider : BrushLightDivider;

    public SolidColorBrush NavActiveBg => IsDark ? BrushDarkNavActiveBg : BrushLightNavActiveBg;
    public SolidColorBrush NavActiveBorder => IsDark ? BrushDarkNavActiveBorder : BrushLightNavActiveBorder;
    public SolidColorBrush NavActiveFg => BrushWhite;

    public SolidColorBrush NavInactiveBg => IsDark ? BrushDarkNavInactiveBg : BrushLightNavInactiveBg;
    public SolidColorBrush NavInactiveBorder => IsDark ? BrushDarkNavInactiveBorder : BrushLightNavInactiveBorder;
    public SolidColorBrush NavInactiveFg => IsDark ? BrushDarkNavInactiveFg : BrushLightNavInactiveFg;

    public SolidColorBrush InputBg => IsDark ? BrushDarkSubCardBg : BrushWhite;
    public SolidColorBrush InputBorder => IsDark ? BrushDarkSubCardBorder : BrushLightSubCardBorder;
    public SolidColorBrush InputFg => IsDark ? BrushDarkTextPrimary : BrushLightTextPrimary;

    // Palette values
    public static readonly SolidColorBrush BrushLightPageBg = new(Color.FromArgb(255, 248, 250, 252)); // Slate 50 (#F8FAFC)
    public static readonly SolidColorBrush BrushDarkPageBg = new(Color.FromArgb(255, 11, 15, 25));     // Deep Slate 950 (#0B0F19)

    public static readonly SolidColorBrush BrushLightCardBg = new(Color.FromArgb(255, 255, 255, 255)); // White (#FFFFFF)
    public static readonly SolidColorBrush BrushDarkCardBg = new(Color.FromArgb(255, 30, 41, 59));     // Slate 800 (#1E293B)

    public static readonly SolidColorBrush BrushLightCardBorder = new(Color.FromArgb(255, 226, 232, 240)); // Slate 200 (#E2E8F0)
    public static readonly SolidColorBrush BrushDarkCardBorder = new(Color.FromArgb(255, 51, 65, 85));    // Slate 700 (#334155)

    public static readonly SolidColorBrush BrushLightSubCardBg = new(Color.FromArgb(255, 248, 250, 252)); // Slate 50 (#F8FAFC)
    public static readonly SolidColorBrush BrushDarkSubCardBg = new(Color.FromArgb(255, 15, 23, 42));     // Slate 900 (#0F172A)

    public static readonly SolidColorBrush BrushLightSubCardBorder = new(Color.FromArgb(255, 203, 213, 225)); // Slate 300 (#CBD5E1)
    public static readonly SolidColorBrush BrushDarkSubCardBorder = new(Color.FromArgb(255, 51, 65, 85));     // Slate 700 (#334155)

    public static readonly SolidColorBrush BrushLightTextPrimary = new(Color.FromArgb(255, 15, 23, 42));    // Slate 900 (#0F172A)
    public static readonly SolidColorBrush BrushDarkTextPrimary = new(Color.FromArgb(255, 248, 250, 252));   // Slate 50 (#F8FAFC)

    public static readonly SolidColorBrush BrushLightTextSecondary = new(Color.FromArgb(255, 100, 116, 139)); // Slate 500 (#64748B)
    public static readonly SolidColorBrush BrushDarkTextSecondary = new(Color.FromArgb(255, 148, 163, 184)); // Slate 400 (#94A3B8)

    public static readonly SolidColorBrush BrushLightTextMuted = new(Color.FromArgb(255, 148, 163, 184));    // Slate 400 (#94A3B8)
    public static readonly SolidColorBrush BrushDarkTextMuted = new(Color.FromArgb(255, 100, 116, 139));    // Slate 500 (#64748B)

    public static readonly SolidColorBrush BrushLightSubtitle = new(Color.FromArgb(255, 71, 85, 105));      // Slate 600 (#475569)
    public static readonly SolidColorBrush BrushSkyAccentDark = new(Color.FromArgb(255, 56, 189, 248));     // Sky 400 (#38BDF8)
    public static readonly SolidColorBrush BrushSkyAccentLight = new(Color.FromArgb(255, 2, 132, 199));     // Sky 600 (#0284C7)

    public static readonly SolidColorBrush BrushLightGridLine = new(Color.FromArgb(255, 226, 232, 240));     // Slate 200
    public static readonly SolidColorBrush BrushDarkGridLine = new(Color.FromArgb(255, 51, 65, 85));         // Slate 700

    public static readonly SolidColorBrush BrushLightAxisText = new(Color.FromArgb(255, 100, 116, 139));     // Slate 500
    public static readonly SolidColorBrush BrushDarkAxisText = new(Color.FromArgb(255, 148, 163, 184));     // Slate 400

    public static readonly SolidColorBrush BrushLightFutureBand = new(Color.FromArgb(255, 241, 245, 249));   // Slate 100
    public static readonly SolidColorBrush BrushDarkFutureBand = new(Color.FromArgb(255, 20, 28, 42));       // Subtle Dark slate (#141C2A)

    public static readonly SolidColorBrush BrushLightDivider = new(Color.FromArgb(255, 203, 213, 225));      // Slate 300
    public static readonly SolidColorBrush BrushDarkDivider = new(Color.FromArgb(255, 51, 65, 85));         // Slate 700

    public static readonly SolidColorBrush BrushLightNavActiveBg = new(Color.FromArgb(255, 15, 23, 42));     // Slate 900
    public static readonly SolidColorBrush BrushLightNavActiveBorder = new(Color.FromArgb(255, 15, 23, 42));
    public static readonly SolidColorBrush BrushDarkNavActiveBg = new(Color.FromArgb(255, 2, 132, 199));     // Sky 600
    public static readonly SolidColorBrush BrushDarkNavActiveBorder = new(Color.FromArgb(255, 56, 189, 248));// Sky 400

    public static readonly SolidColorBrush BrushLightNavInactiveBg = new(Color.FromArgb(255, 241, 245, 249));   // Slate 100
    public static readonly SolidColorBrush BrushLightNavInactiveBorder = new(Color.FromArgb(255, 203, 213, 225));// Slate 300
    public static readonly SolidColorBrush BrushLightNavInactiveFg = new(Color.FromArgb(255, 30, 41, 59));      // Slate 800

    public static readonly SolidColorBrush BrushDarkNavInactiveBg = new(Color.FromArgb(255, 30, 41, 59));       // Slate 800
    public static readonly SolidColorBrush BrushDarkNavInactiveBorder = new(Color.FromArgb(255, 51, 65, 85));   // Slate 700
    public static readonly SolidColorBrush BrushDarkNavInactiveFg = new(Color.FromArgb(255, 226, 232, 240));     // Slate 200

    public static readonly SolidColorBrush BrushWhite = new(Colors.White);

    private void NotifyThemeChanged()
    {
        UpdateApplicationResources();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontSizeSmall)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontSizeLegend)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontSizeRegular)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontSizeBody)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontSizeMedium)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontSizeSubheading)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontSizeHeading)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontSizeTitle)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontScalePercent)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontScaleFactor)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontScaleLabel)));
        ThemeChanged?.Invoke(this, EventArgs.Empty);

        try
        {
            string hostBg = IsDark ? "#0B0F19" : "#F8FAFC";
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

    private void UpdateApplicationResources()
    {
        try
        {
            var app = Application.Current;
            if (app?.Resources?.MergedDictionaries != null)
            {
                app.Resources.MergedDictionaries.Clear();
                if (IsDark)
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
                var targetPalette = IsDark 
                    ? OpenSilver.Themes.Modern.ModernTheme.Palettes.Dark 
                    : OpenSilver.Themes.Modern.ModernTheme.Palettes.Light;

                if (modernTheme.CurrentPalette != targetPalette)
                {
                    modernTheme.CurrentPalette = targetPalette;
                    app.Theme = modernTheme;

                    if (app.RootVisual is System.Windows.Controls.Grid mainGrid && mainGrid.Children.Count > 0)
                    {
                        var existingPage = System.Linq.Enumerable.FirstOrDefault(mainGrid.Children) as MainPage;
                        var existingVm = existingPage?.ViewModel;
                        mainGrid.Children.Clear();
                        mainGrid.Children.Add(new MainPage(existingVm));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("UpdateApplicationResources error: " + ex.Message);
        }
    }
}
