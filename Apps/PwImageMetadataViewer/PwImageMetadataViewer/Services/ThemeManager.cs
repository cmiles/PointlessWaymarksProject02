#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PwImageMetadataViewer.Services;

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
                "localStorage.getItem('pwimagemetadataviewer_theme_mode') || 'System'");
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
                        $"localStorage.setItem('pwimagemetadataviewer_theme_mode', '{_mode}');");
                }
                catch { }

                NotifyThemeChanged();
            }
        }
    }

    public bool IsDark => _mode == AppThemeMode.System ? _isSystemDark : (_mode == AppThemeMode.Dark);

    public string ThemeButtonLabel => _mode switch
    {
        AppThemeMode.System => IsDark ? "🌓 System (Dark)" : "🌓 System (Light)",
        AppThemeMode.Dark => "🌙 Dark",
        AppThemeMode.Light => "☀️ Light",
        _ => "🌓 Theme"
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

    private static readonly Dictionary<string, SolidColorBrush> _brushCache = new();
    private static SolidColorBrush GetOrCreateBrush(string key, byte a, byte r, byte g, byte b)
    {
        if (!_brushCache.TryGetValue(key, out var brush))
        {
            try
            {
                brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
                _brushCache[key] = brush;
            }
            catch
            {
                brush = null!;
            }
        }
        return brush;
    }

    // Palette values - Surfaces
    public static SolidColorBrush BrushLightPageBg => GetOrCreateBrush("LightPageBg", 255, 248, 250, 252);     // Slate 50 (#F8FAFC)
    public static SolidColorBrush BrushDarkPageBg => GetOrCreateBrush("DarkPageBg", 255, 11, 15, 25);          // Deep Slate (#0B0F19)

    public static SolidColorBrush BrushLightHeaderBg => GetOrCreateBrush("LightHeaderBg", 255, 255, 255, 255); // White (#FFFFFF)
    public static SolidColorBrush BrushDarkHeaderBg => GetOrCreateBrush("DarkHeaderBg", 255, 17, 24, 39);      // Slate 900 (#111827)

    public static SolidColorBrush BrushLightCardBg => GetOrCreateBrush("LightCardBg", 255, 255, 255, 255);     // White (#FFFFFF)
    public static SolidColorBrush BrushDarkCardBg => GetOrCreateBrush("DarkCardBg", 255, 22, 31, 48);          // Dark Slate Card (#161F30)

    public static SolidColorBrush BrushLightCardBorder => GetOrCreateBrush("LightCardBorder", 255, 226, 232, 240); // Slate 200 (#E2E8F0)
    public static SolidColorBrush BrushDarkCardBorder => GetOrCreateBrush("DarkCardBorder", 255, 39, 53, 73);       // Slate Border (#273549)

    public static SolidColorBrush BrushLightSubCardBg => GetOrCreateBrush("LightSubCardBg", 255, 241, 245, 249); // Slate 100 (#F1F5F9)
    public static SolidColorBrush BrushDarkSubCardBg => GetOrCreateBrush("DarkSubCardBg", 255, 15, 23, 42);      // Slate 900 (#0F172A)

    public static SolidColorBrush BrushLightSubCardBorder => GetOrCreateBrush("LightSubCardBorder", 255, 226, 232, 240); // Slate 200 (#E2E8F0)
    public static SolidColorBrush BrushDarkSubCardBorder => GetOrCreateBrush("DarkSubCardBorder", 255, 30, 41, 59);      // Slate 800 (#1E293B)

    public static SolidColorBrush BrushLightDivider => GetOrCreateBrush("LightDivider", 255, 226, 232, 240); // Slate 200 (#E2E8F0)
    public static SolidColorBrush BrushDarkDivider => GetOrCreateBrush("DarkDivider", 255, 31, 41, 55);       // Slate 800 (#1F2937)

    // Palette values - Text
    public static SolidColorBrush BrushLightTextPrimary => GetOrCreateBrush("LightTextPrimary", 255, 15, 23, 42);    // Slate 900 (#0F172A)
    public static SolidColorBrush BrushDarkTextPrimary => GetOrCreateBrush("DarkTextPrimary", 255, 248, 250, 252);   // Slate 50 (#F8FAFC)

    public static SolidColorBrush BrushLightTextHeading => GetOrCreateBrush("LightTextHeading", 255, 30, 41, 59);    // Slate 800 (#1E293B)
    public static SolidColorBrush BrushDarkTextHeading => GetOrCreateBrush("DarkTextHeading", 255, 241, 245, 249);   // Slate 100 (#F1F5F9)

    public static SolidColorBrush BrushLightTextSecondary => GetOrCreateBrush("LightTextSecondary", 255, 51, 65, 85); // Slate 700 (#334155)
    public static SolidColorBrush BrushDarkTextSecondary => GetOrCreateBrush("DarkTextSecondary", 255, 203, 213, 225);// Slate 300 (#CBD5E1)

    public static SolidColorBrush BrushLightTextMuted => GetOrCreateBrush("LightTextMuted", 255, 100, 116, 139);      // Slate 500 (#64748B)
    public static SolidColorBrush BrushDarkTextMuted => GetOrCreateBrush("DarkTextMuted", 255, 148, 163, 184);       // Slate 400 (#94A3B8)

    // Palette values - Accents & Controls
    public static SolidColorBrush BrushSkyAccentLight => GetOrCreateBrush("SkyAccentLight", 255, 2, 132, 199); // Sky 600 (#0284C7)
    public static SolidColorBrush BrushSkyAccentDark => GetOrCreateBrush("SkyAccentDark", 255, 56, 189, 248);  // Sky 400 (#38BDF8)

    public static SolidColorBrush BrushLightControlBg => GetOrCreateBrush("LightControlBg", 255, 255, 255, 255);
    public static SolidColorBrush BrushDarkControlBg => GetOrCreateBrush("DarkControlBg", 255, 30, 41, 59);

    public static SolidColorBrush BrushLightControlBorder => GetOrCreateBrush("LightControlBorder", 255, 203, 213, 225); // Slate 300
    public static SolidColorBrush BrushDarkControlBorder => GetOrCreateBrush("DarkControlBorder", 255, 71, 85, 105);    // Slate 600

    public static SolidColorBrush BrushLightControlFg => GetOrCreateBrush("LightControlFg", 255, 15, 23, 42);
    public static SolidColorBrush BrushDarkControlFg => GetOrCreateBrush("DarkControlFg", 255, 248, 250, 252);

    public static SolidColorBrush BrushLightStatusBg => GetOrCreateBrush("LightStatusBg", 255, 224, 242, 254);     // Sky 100 (#E0F2FE)
    public static SolidColorBrush BrushDarkStatusBg => GetOrCreateBrush("DarkStatusBg", 255, 12, 74, 110);        // Deep Sky (#0C4A6E)
    public static SolidColorBrush BrushLightStatusBorder => GetOrCreateBrush("LightStatusBorder", 255, 125, 211, 252); // Sky 300 (#7DD3FC)
    public static SolidColorBrush BrushDarkStatusBorder => GetOrCreateBrush("DarkStatusBorder", 255, 2, 132, 199);   // Sky 600 (#0284C7)

    public static SolidColorBrush BrushWhite => GetOrCreateBrush("White", 255, 255, 255, 255);

    // Dynamic semantic brushes
    public SolidColorBrush PageBackground => IsDark ? BrushDarkPageBg : BrushLightPageBg;
    public SolidColorBrush HeaderBackground => IsDark ? BrushDarkHeaderBg : BrushLightHeaderBg;
    public SolidColorBrush CardBackground => IsDark ? BrushDarkCardBg : BrushLightCardBg;
    public SolidColorBrush CardBorder => IsDark ? BrushDarkCardBorder : BrushLightCardBorder;
    public SolidColorBrush SubCardBackground => IsDark ? BrushDarkSubCardBg : BrushLightSubCardBg;
    public SolidColorBrush SubCardBorder => IsDark ? BrushDarkSubCardBorder : BrushLightSubCardBorder;
    public SolidColorBrush Divider => IsDark ? BrushDarkDivider : BrushLightDivider;

    public SolidColorBrush TextPrimary => IsDark ? BrushDarkTextPrimary : BrushLightTextPrimary;
    public SolidColorBrush TextHeading => IsDark ? BrushDarkTextHeading : BrushLightTextHeading;
    public SolidColorBrush TextSecondary => IsDark ? BrushDarkTextSecondary : BrushLightTextSecondary;
    public SolidColorBrush TextMuted => IsDark ? BrushDarkTextMuted : BrushLightTextMuted;
    public SolidColorBrush Accent => IsDark ? BrushSkyAccentDark : BrushSkyAccentLight;

    public SolidColorBrush ControlBackground => IsDark ? BrushDarkControlBg : BrushLightControlBg;
    public SolidColorBrush ControlBorder => IsDark ? BrushDarkControlBorder : BrushLightControlBorder;
    public SolidColorBrush ControlForeground => IsDark ? BrushDarkControlFg : BrushLightControlFg;

    public SolidColorBrush StatusBackground => IsDark ? BrushDarkStatusBg : BrushLightStatusBg;
    public SolidColorBrush StatusBorder => IsDark ? BrushDarkStatusBorder : BrushLightStatusBorder;

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

                    if (app.RootVisual is Grid mainGrid)
                    {
                        var existingPage = mainGrid.Children.FirstOrDefault() as MainPage;
                        var existingReport = existingPage?.CurrentReport;
                        mainGrid.Children.Clear();
                        mainGrid.Children.Add(new MainPage(existingReport));
                    }
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

    private void NotifyThemeChanged()
    {
        UpdateApplicationResources(IsDark);

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
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

                    var styleEl = document.getElementById('pwimagemetadata-theme-controls');
                    if (!styleEl) {{
                        styleEl = document.createElement('style');
                        styleEl.id = 'pwimagemetadata-theme-controls';
                        document.head.appendChild(styleEl);
                    }}
                    styleEl.textContent = `
                        /* Leaflet Controls & Popups in Dark / Light */
                        body.dark-theme .leaflet-bar a {{
                            background-color: #1E293B !important;
                            color: #F8FAFC !important;
                            border-bottom-color: #334155 !important;
                        }}
                        body.dark-theme .leaflet-bar a:hover {{
                            background-color: #334155 !important;
                            color: #38BDF8 !important;
                        }}
                        body.light-theme .leaflet-bar a {{
                            background-color: #FFFFFF !important;
                            color: #1E293B !important;
                            border-bottom-color: #E2E8F0 !important;
                        }}
                        body.light-theme .leaflet-bar a:hover {{
                            background-color: #F1F5F9 !important;
                            color: #0284C7 !important;
                        }}
                        body.dark-theme .leaflet-popup-content-wrapper,
                        body.dark-theme .leaflet-popup-tip {{
                            background: #1E293B !important;
                            color: #F8FAFC !important;
                            box-shadow: 0 4px 12px rgba(0,0,0,0.5) !important;
                        }}
                        body.light-theme .leaflet-popup-content-wrapper,
                        body.light-theme .leaflet-popup-tip {{
                            background: #FFFFFF !important;
                            color: #0F172A !important;
                            box-shadow: 0 4px 12px rgba(0,0,0,0.15) !important;
                        }}
                        /* Scrollbars */
                        body.dark-theme ::-webkit-scrollbar-thumb {{
                            background: #334155 !important;
                            border-radius: 4px;
                        }}
                        body.light-theme ::-webkit-scrollbar-thumb {{
                            background: #CBD5E1 !important;
                            border-radius: 4px;
                        }}
                    `;
                }}
            ");
        }
        catch { }
    }
}
