using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace WaterDaze.Services;

public enum AppTheme
{
    System,
    Light,
    Dark
}

public static class ThemeManager
{
    private static AppTheme _currentTheme = AppTheme.System;
    private static bool _isSystemDark;
    private static bool _isDark;
    private static bool _isInitialized;

    public static event EventHandler<bool>? ThemeChanged;

    public static AppTheme CurrentTheme => _currentTheme;

    public static bool IsDark => _isDark;

    public static bool IsSystemDark => _isSystemDark;

    public static void Initialize()
    {
        try
        {
            object? savedObj = OpenSilver.Interop.ExecuteJavaScript(
                "localStorage.getItem('waterdaze_theme_mode') || 'System'");
            string saved = savedObj?.ToString() ?? "System";
            if (Enum.TryParse<AppTheme>(saved, out var savedTheme))
            {
                _currentTheme = savedTheme;
            }

            _isSystemDark = QuerySystemDarkTheme();
            _isDark = _currentTheme switch
            {
                AppTheme.Dark => true,
                AppTheme.Light => false,
                _ => _isSystemDark
            };

            RegisterSystemThemeListener();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("ThemeManager Init error: " + ex.Message);
            _isSystemDark = QuerySystemDarkTheme();
            _isDark = _currentTheme switch
            {
                AppTheme.Dark => true,
                AppTheme.Light => false,
                _ => _isSystemDark
            };
        }

        _isInitialized = true;
        ApplyThemeInternal(_isDark, notify: true);
    }

    public static void SetTheme(AppTheme theme)
    {
        _currentTheme = theme;

        try
        {
            OpenSilver.Interop.ExecuteJavaScriptVoid(
                $"localStorage.setItem('waterdaze_theme_mode', '{_currentTheme}');");
        }
        catch { }

        var effectiveDark = theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => _isSystemDark
        };

        if (_isDark != effectiveDark || !_isInitialized)
        {
            _isDark = effectiveDark;
            _isInitialized = true;
            ApplyThemeInternal(_isDark, notify: true);
        }
        else
        {
            // Still notify so selectors can update e.g. "System (Dark)" labels
            ThemeChanged?.Invoke(null, _isDark);
        }
    }

    public static void CycleTheme()
    {
        var next = _currentTheme switch
        {
            AppTheme.System => AppTheme.Dark,
            AppTheme.Dark => AppTheme.Light,
            AppTheme.Light => AppTheme.System,
            _ => AppTheme.System
        };
        SetTheme(next);
    }

    public static void SetSystemDark(bool isDark)
    {
        _isSystemDark = isDark;
        if (_currentTheme == AppTheme.System)
        {
            if (_isDark != isDark || !_isInitialized)
            {
                _isDark = isDark;
                _isInitialized = true;
                ApplyThemeInternal(_isDark, notify: true);
            }
        }
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
            // Fallback for unit testing, simulator, or non-DOM environments
        }

        return false;
    }

    private static void RegisterSystemThemeListener()
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

    private static void ApplyThemeInternal(bool isDark, bool notify)
    {
        try
        {
            string hostBg = isDark ? "#0F172A" : "#F9FAFB";
            string themeClass = isDark ? "dark-theme" : "light-theme";
            OpenSilver.Interop.ExecuteJavaScriptVoid($@"
                if (document) {{
                    if (document.documentElement) {{
                        document.documentElement.style.backgroundColor = '{hostBg}';
                    }}
                    if (document.body) {{
                        document.body.style.backgroundColor = '{hostBg}';
                        document.body.classList.remove('dark-theme', 'light-theme');
                        document.body.classList.add('{themeClass}');
                    }}
                }}

                    var styleEl = document.getElementById('waterdaze-theme-controls');
                    if (!styleEl) {{
                        styleEl = document.createElement('style');
                        styleEl.id = 'waterdaze-theme-controls';
                        document.head.appendChild(styleEl);
                    }}
                    styleEl.textContent = `
                        /* Dark theme controls */
                        body.dark-theme [dataid=""BackgroundGradient""],
                        body.dark-theme [dataid=""BackgroundGradient2""] {{
                            background: transparent !important;
                            border-color: transparent !important;
                        }}
                        body.dark-theme [dataid=""BackgroundOverlay""],
                        body.dark-theme [dataid=""BackgroundOverlay2""],
                        body.dark-theme [dataid=""BackgroundOverlay3""] {{
                            background: #334155 !important;
                            border-color: #475569 !important;
                        }}
                        body.dark-theme .System\\.Windows\\.Controls\\.ComboBox {{
                            background-color: #1E293B !important;
                            border-color: #475569 !important;
                            color: #F8FAFC !important;
                        }}
                        body.dark-theme .System\\.Windows\\.Controls\\.ComboBox [dataid=""Background""] {{
                            background: #1E293B !important;
                            border-color: #475569 !important;
                            border-radius: 6px !important;
                        }}
                        body.dark-theme .System\\.Windows\\.Controls\\.ComboBox svg path,
                        body.dark-theme .System\\.Windows\\.Controls\\.ComboBox [dataid=""BtnArrow""] path,
                        body.dark-theme .System\\.Windows\\.Controls\\.ComboBox [dataid=""BtnArrow""] {{
                            fill: #CBD5E1 !important;
                            stroke: #CBD5E1 !important;
                        }}
                        body.dark-theme .System\\.Windows\\.Controls\\.ComboBox div,
                        body.dark-theme .System\\.Windows\\.Controls\\.ComboBox span,
                        body.dark-theme .System\\.Windows\\.Controls\\.ComboBox text,
                        body.dark-theme .System\\.Windows\\.Controls\\.ComboBox .System\\.Windows\\.Controls\\.TextBlock {{
                            color: #F8FAFC !important;
                        }}
                        body.dark-theme .System\\.Windows\\.Controls\\.Primitives\\.Popup,
                        body.dark-theme [dataid=""Popup""] {{
                            background: transparent !important;
                            background-color: transparent !important;
                            border: none !important;
                            box-shadow: none !important;
                        }}
                        body.dark-theme [dataid=""PopupBorder""],
                        body.dark-theme [dataid=""ContentPresenterBorder""] {{
                            background-color: #1E293B !important;
                            border: 1px solid #475569 !important;
                            border-radius: 6px !important;
                            box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.5) !important;
                        }}
                        body.dark-theme .System\\.Windows\\.Controls\\.Primitives\\.Popup [dataid=""Background""],
                        body.dark-theme .System\\.Windows\\.Controls\\.Primitives\\.Popup [dataid=""ContentPresenterBorder""],
                        body.dark-theme .System\\.Windows\\.Controls\\.Primitives\\.Popup [dataid=""ScrollViewer""],
                        body.dark-theme .System\\.Windows\\.Controls\\.Primitives\\.Popup div {{
                            background-color: #1E293B !important;
                            border-color: #475569 !important;
                        }}
                        body.dark-theme .System\\.Windows\\.Controls\\.ComboBoxItem {{
                            background-color: #1E293B !important;
                            color: #F8FAFC !important;
                        }}
                        body.dark-theme .System\\.Windows\\.Controls\\.ComboBoxItem div,
                        body.dark-theme .System\\.Windows\\.Controls\\.ComboBoxItem span,
                        body.dark-theme .System\\.Windows\\.Controls\\.ComboBoxItem text,
                        body.dark-theme .System\\.Windows\\.Controls\\.ComboBoxItem .System\\.Windows\\.Controls\\.TextBlock {{
                            color: #F8FAFC !important;
                            background-color: transparent !important;
                        }}
                        body.dark-theme .System\\.Windows\\.Controls\\.ComboBoxItem:hover,
                        body.dark-theme .System\\.Windows\\.Controls\\.ComboBoxItem:focus {{
                            background-color: #334155 !important;
                            color: #FFFFFF !important;
                        }}
                        body.dark-theme .System\\.Windows\\.Controls\\.ComboBoxItem[data-selected=""true""],
                        body.dark-theme .System\\.Windows\\.Controls\\.ComboBoxItem.Selected {{
                            background-color: #2563EB !important;
                            color: #FFFFFF !important;
                        }}

                        /* Light theme controls */
                        body.light-theme [dataid=""BackgroundGradient""] {{
                            display: block !important;
                            visibility: visible !important;
                        }}
                        body.light-theme .System\\.Windows\\.Controls\\.ComboBox {{
                            background-color: #FFFFFF !important;
                            border-color: #D1D5DB !important;
                            color: #111827 !important;
                        }}
                        body.light-theme .System\\.Windows\\.Controls\\.ComboBox [dataid=""Background""] {{
                            background: #FFFFFF !important;
                            border-color: #D1D5DB !important;
                            border-radius: 6px !important;
                        }}
                        body.light-theme .System\\.Windows\\.Controls\\.ComboBox svg path,
                        body.light-theme .System\\.Windows\\.Controls\\.ComboBox [dataid=""BtnArrow""] path,
                        body.light-theme .System\\.Windows\\.Controls\\.ComboBox [dataid=""BtnArrow""] {{
                            fill: #374151 !important;
                            stroke: #374151 !important;
                        }}
                        body.light-theme .System\\.Windows\\.Controls\\.ComboBox div,
                        body.light-theme .System\\.Windows\\.Controls\\.ComboBox span,
                        body.light-theme .System\\.Windows\\.Controls\\.ComboBox text,
                        body.light-theme .System\\.Windows\\.Controls\\.ComboBox .System\\.Windows\\.Controls\\.TextBlock {{
                            color: #111827 !important;
                        }}
                        body.light-theme .System\\.Windows\\.Controls\\.Primitives\\.Popup,
                        body.light-theme [dataid=""Popup""] {{
                            background: transparent !important;
                            background-color: transparent !important;
                            border: none !important;
                            box-shadow: none !important;
                        }}
                        body.light-theme [dataid=""PopupBorder""],
                        body.light-theme [dataid=""ContentPresenterBorder""] {{
                            background-color: #FFFFFF !important;
                            border: 1px solid #D1D5DB !important;
                            border-radius: 6px !important;
                            box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.1) !important;
                        }}
                        body.light-theme .System\\.Windows\\.Controls\\.Primitives\\.Popup [dataid=""Background""],
                        body.light-theme .System\\.Windows\\.Controls\\.Primitives\\.Popup [dataid=""ContentPresenterBorder""],
                        body.light-theme .System\\.Windows\\.Controls\\.Primitives\\.Popup [dataid=""ScrollViewer""],
                        body.light-theme .System\\.Windows\\.Controls\\.Primitives\\.Popup div {{
                            background-color: #FFFFFF !important;
                            border-color: #D1D5DB !important;
                        }}
                        body.light-theme .System\\.Windows\\.Controls\\.ComboBoxItem {{
                            background-color: #FFFFFF !important;
                            color: #111827 !important;
                        }}
                        body.light-theme .System\\.Windows\\.Controls\\.ComboBoxItem div,
                        body.light-theme .System\\.Windows\\.Controls\\.ComboBoxItem span,
                        body.light-theme .System\\.Windows\\.Controls\\.ComboBoxItem text,
                        body.light-theme .System\\.Windows\\.Controls\\.ComboBoxItem .System\\.Windows\\.Controls\\.TextBlock {{
                            color: #111827 !important;
                            background-color: transparent !important;
                        }}
                        body.light-theme .System\\.Windows\\.Controls\\.ComboBoxItem:hover,
                        body.light-theme .System\\.Windows\\.Controls\\.ComboBoxItem:focus {{
                            background-color: #F3F4F6 !important;
                            color: #111827 !important;
                        }}
                        body.light-theme .System\\.Windows\\.Controls\\.ComboBoxItem[data-selected=""true""],
                        body.light-theme .System\\.Windows\\.Controls\\.ComboBoxItem.Selected {{
                            background-color: #DBEAFE !important;
                            color: #1D4ED8 !important;
                        }}
                    `;
                }}
            ");
        }
        catch { }

        UpdateApplicationResources(isDark);

        if (notify)
        {
            ThemeChanged?.Invoke(null, isDark);
        }
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
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("UpdateApplicationResources error: " + ex.Message);
        }
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
    public static SolidColorBrush BrushLightPageBg => GetOrCreateBrush("LightPageBg", 255, 249, 250, 251); // #F9FAFB
    public static SolidColorBrush BrushDarkPageBg => GetOrCreateBrush("DarkPageBg", 255, 15, 23, 42);     // #0F172A

    public static SolidColorBrush BrushLightCardBg => GetOrCreateBrush("LightCardBg", 255, 255, 255, 255); // #FFFFFF
    public static SolidColorBrush BrushDarkCardBg => GetOrCreateBrush("DarkCardBg", 255, 30, 41, 59);     // #1E293B

    public static SolidColorBrush BrushLightCardBorder => GetOrCreateBrush("LightCardBorder", 255, 229, 231, 235); // #E5E7EB
    public static SolidColorBrush BrushDarkCardBorder => GetOrCreateBrush("DarkCardBorder", 255, 51, 65, 85);    // #334155

    public static SolidColorBrush BrushLightSubCardBg => GetOrCreateBrush("LightSubCardBg", 255, 243, 244, 246); // #F3F4F6
    public static SolidColorBrush BrushDarkSubCardBg => GetOrCreateBrush("DarkSubCardBg", 255, 22, 32, 50);     // #162032

    public static SolidColorBrush BrushLightSubCardBorder => GetOrCreateBrush("LightSubCardBorder", 255, 229, 231, 235); // #E5E7EB
    public static SolidColorBrush BrushDarkSubCardBorder => GetOrCreateBrush("DarkSubCardBorder", 255, 51, 65, 85);     // #334155

    public static SolidColorBrush BrushLightDivider => GetOrCreateBrush("LightDivider", 255, 229, 231, 235);    // #E5E7EB
    public static SolidColorBrush BrushDarkDivider => GetOrCreateBrush("DarkDivider", 255, 51, 65, 85);       // #334155

    // Palette values - Text
    public static SolidColorBrush BrushLightTextPrimary => GetOrCreateBrush("LightTextPrimary", 255, 17, 24, 39);   // #111827
    public static SolidColorBrush BrushDarkTextPrimary => GetOrCreateBrush("DarkTextPrimary", 255, 248, 250, 252);  // #F8FAFC

    public static SolidColorBrush BrushLightTextSecondary => GetOrCreateBrush("LightTextSecondary", 255, 75, 85, 99); // #4B5563
    public static SolidColorBrush BrushDarkTextSecondary => GetOrCreateBrush("DarkTextSecondary", 255, 203, 213, 225);// #CBD5E1

    public static SolidColorBrush BrushLightTextMuted => GetOrCreateBrush("LightTextMuted", 255, 107, 114, 128);   // #6B7280
    public static SolidColorBrush BrushDarkTextMuted => GetOrCreateBrush("DarkTextMuted", 255, 148, 163, 184);    // #94A3B8

    public static SolidColorBrush BrushLightTextHeading => GetOrCreateBrush("LightTextHeading", 255, 31, 41, 55);   // #1F2937
    public static SolidColorBrush BrushDarkTextHeading => GetOrCreateBrush("DarkTextHeading", 255, 241, 245, 249);  // #F1F5F9

    public static SolidColorBrush BrushLightTextLabel => GetOrCreateBrush("LightTextLabel", 255, 55, 65, 81);     // #374151
    public static SolidColorBrush BrushDarkTextLabel => GetOrCreateBrush("DarkTextLabel", 255, 226, 232, 240);   // #E2E8F0

    // Palette values - Accents & Links
    public static SolidColorBrush BrushLightHyperlink => GetOrCreateBrush("LightHyperlink", 255, 37, 99, 235);    // #2563EB
    public static SolidColorBrush BrushDarkHyperlink => GetOrCreateBrush("DarkHyperlink", 255, 96, 165, 250);    // #60A5FA

    public static SolidColorBrush BrushLightSiteCode => GetOrCreateBrush("LightSiteCode", 255, 29, 78, 216);     // #1D4ED8
    public static SolidColorBrush BrushDarkSiteCode => GetOrCreateBrush("DarkSiteCode", 255, 96, 165, 250);      // #60A5FA

    public static SolidColorBrush BrushLightFlowDays => GetOrCreateBrush("LightFlowDays", 255, 5, 150, 105);     // #059669
    public static SolidColorBrush BrushDarkFlowDays => GetOrCreateBrush("DarkFlowDays", 255, 52, 211, 153);     // #34D399

    // Palette values - Status Banners
    public static SolidColorBrush BrushLightWarningBg => GetOrCreateBrush("LightWarningBg", 255, 255, 251, 235);  // #FFFBEB
    public static SolidColorBrush BrushDarkWarningBg => GetOrCreateBrush("DarkWarningBg", 255, 45, 29, 3);       // #2D1D03
    public static SolidColorBrush BrushLightWarningBorder => GetOrCreateBrush("LightWarningBorder", 255, 252, 211, 77); // #FCD34D
    public static SolidColorBrush BrushDarkWarningBorder => GetOrCreateBrush("DarkWarningBorder", 255, 120, 53, 15);  // #78350F
    public static SolidColorBrush BrushLightWarningText => GetOrCreateBrush("LightWarningText", 255, 146, 64, 14);   // #92400E
    public static SolidColorBrush BrushDarkWarningText => GetOrCreateBrush("DarkWarningText", 255, 253, 230, 138);  // #FDE68A

    public static SolidColorBrush BrushLightErrorBg => GetOrCreateBrush("LightErrorBg", 255, 254, 242, 242);    // #FEF2F2
    public static SolidColorBrush BrushDarkErrorBg => GetOrCreateBrush("DarkErrorBg", 255, 49, 11, 11);       // #310B0B
    public static SolidColorBrush BrushLightErrorBorder => GetOrCreateBrush("LightErrorBorder", 255, 254, 202, 202);// #FECACA
    public static SolidColorBrush BrushDarkErrorBorder => GetOrCreateBrush("DarkErrorBorder", 255, 127, 29, 29);   // #7F1D1D
    public static SolidColorBrush BrushLightErrorText => GetOrCreateBrush("LightErrorText", 255, 220, 38, 38);    // #DC2626
    public static SolidColorBrush BrushDarkErrorText => GetOrCreateBrush("DarkErrorText", 255, 252, 165, 165);   // #FCA5A5

    // Palette values - Controls & Buttons
    public static SolidColorBrush BrushLightControlBg => GetOrCreateBrush("LightControlBg", 255, 255, 255, 255);  // #FFFFFF
    public static SolidColorBrush BrushDarkControlBg => GetOrCreateBrush("DarkControlBg", 255, 30, 41, 59);     // #1E293B
    public static SolidColorBrush BrushLightControlBorder => GetOrCreateBrush("LightControlBorder", 255, 209, 213, 219); // #D1D5DB
    public static SolidColorBrush BrushDarkControlBorder => GetOrCreateBrush("DarkControlBorder", 255, 71, 85, 105);   // #475569
    public static SolidColorBrush BrushLightControlFg => GetOrCreateBrush("LightControlFg", 255, 17, 24, 39);     // #111827
    public static SolidColorBrush BrushDarkControlFg => GetOrCreateBrush("DarkControlFg", 255, 248, 250, 252);   // #F8FAFC

    public static SolidColorBrush BrushActionButtonBg => GetOrCreateBrush("ActionButtonBg", 255, 37, 99, 235);    // #2563EB
    public static SolidColorBrush BrushLightActionButtonBorder => GetOrCreateBrush("LightActionButtonBorder", 255, 29, 78, 216); // #1D4ED8
    public static SolidColorBrush BrushDarkActionButtonBorder => GetOrCreateBrush("DarkActionButtonBorder", 255, 59, 130, 246);  // #3B82F6
    public static SolidColorBrush BrushWhite => GetOrCreateBrush("White", 255, 255, 255, 255);

    // Palette values - Rankings
    public static SolidColorBrush BrushLightRankingWetStreaksTitle => GetOrCreateBrush("LightRankingWetStreaksTitle", 255, 4, 120, 87); // #047857
    public static SolidColorBrush BrushLightRankingDryTitle => GetOrCreateBrush("LightRankingDryTitle", 255, 180, 83, 9);       // #B45309
    public static SolidColorBrush BrushDarkRankingDryTitle => GetOrCreateBrush("DarkRankingDryTitle", 255, 251, 191, 36);      // #FBBF24
    public static SolidColorBrush BrushLightRankingPeakTitle => GetOrCreateBrush("LightRankingPeakTitle", 255, 124, 58, 237);    // #7C3AED
    public static SolidColorBrush BrushDarkRankingPeakTitle => GetOrCreateBrush("DarkRankingPeakTitle", 255, 192, 132, 252);    // #C084FC
    public static SolidColorBrush BrushLightRankingDriestHeader => GetOrCreateBrush("LightRankingDriestHeader", 255, 217, 119, 6);   // #D97706

    // Palette values - Charts & Heatmaps
    public static SolidColorBrush BrushLightChartGrid => GetOrCreateBrush("LightChartGrid", 255, 240, 240, 240);
    public static SolidColorBrush BrushDarkChartGrid => GetOrCreateBrush("DarkChartGrid", 255, 51, 65, 85);
    public static SolidColorBrush BrushLightBandFill => GetOrCreateBrush("LightBandFill", 50, 31, 119, 180);
    public static SolidColorBrush BrushDarkBandFill => GetOrCreateBrush("DarkBandFill", 64, 96, 165, 250);
    public static SolidColorBrush BrushLightBandBorder => GetOrCreateBrush("LightBandBorder", 100, 31, 119, 180);
    public static SolidColorBrush BrushDarkBandBorder => GetOrCreateBrush("DarkBandBorder", 128, 96, 165, 250);
    public static SolidColorBrush BrushLightMedianLine => GetOrCreateBrush("LightMedianLine", 255, 31, 119, 180);
    public static SolidColorBrush BrushDarkMedianLine => GetOrCreateBrush("DarkMedianLine", 255, 96, 165, 250);
    public static SolidColorBrush BrushLightHeatmapNoData => GetOrCreateBrush("LightHeatmapNoData", 255, 245, 247, 250);
    public static SolidColorBrush BrushDarkHeatmapNoData => GetOrCreateBrush("DarkHeatmapNoData", 255, 19, 29, 45);
    public static SolidColorBrush BrushLightHeatmapCellBorder => GetOrCreateBrush("LightHeatmapCellBorder", 255, 235, 238, 242);
    public static SolidColorBrush BrushDarkHeatmapCellBorder => GetOrCreateBrush("DarkHeatmapCellBorder", 255, 30, 41, 59);
    public static SolidColorBrush BrushLightYearFacetCardBorder => GetOrCreateBrush("LightYearFacetCardBorder", 255, 224, 224, 224);
    public static SolidColorBrush BrushLightYearFacetBar => GetOrCreateBrush("LightYearFacetBar", 180, 200, 200, 200);
    public static SolidColorBrush BrushDarkYearFacetBar => GetOrCreateBrush("DarkYearFacetBar", 128, 100, 116, 139);
    public static SolidColorBrush BrushLightYearFacetLine => GetOrCreateBrush("LightYearFacetLine", 255, 51, 102, 204);

    // Dynamic semantic brushes (mirroring HistoricTempests)
    public static SolidColorBrush PageBackground => IsDark ? BrushDarkPageBg : BrushLightPageBg;
    public static SolidColorBrush CardBackground => IsDark ? BrushDarkCardBg : BrushLightCardBg;
    public static SolidColorBrush CardBorder => IsDark ? BrushDarkCardBorder : BrushLightCardBorder;
    public static SolidColorBrush SubCardBackground => IsDark ? BrushDarkSubCardBg : BrushLightSubCardBg;
    public static SolidColorBrush SubCardBorder => IsDark ? BrushDarkSubCardBorder : BrushLightSubCardBorder;
    public static SolidColorBrush Divider => IsDark ? BrushDarkDivider : BrushLightDivider;

    public static SolidColorBrush TextPrimary => IsDark ? BrushDarkTextPrimary : BrushLightTextPrimary;
    public static SolidColorBrush TextSecondary => IsDark ? BrushDarkTextSecondary : BrushLightTextSecondary;
    public static SolidColorBrush TextMuted => IsDark ? BrushDarkTextMuted : BrushLightTextMuted;
    public static SolidColorBrush TextHeading => IsDark ? BrushDarkTextHeading : BrushLightTextHeading;
    public static SolidColorBrush TextLabel => IsDark ? BrushDarkTextLabel : BrushLightTextLabel;

    public static SolidColorBrush ControlBackground => IsDark ? BrushDarkControlBg : BrushLightControlBg;
    public static SolidColorBrush ControlBorder => IsDark ? BrushDarkControlBorder : BrushLightControlBorder;
    public static SolidColorBrush ControlForeground => IsDark ? BrushDarkControlFg : BrushLightControlFg;

    public static SolidColorBrush ActionButtonBackground => BrushActionButtonBg;
    public static SolidColorBrush ActionButtonBorder => IsDark ? BrushDarkActionButtonBorder : BrushLightActionButtonBorder;
    public static SolidColorBrush ActionButtonForeground => BrushWhite;

    public static SolidColorBrush GetBrush(string resourceKey, SolidColorBrush fallback)
    {
        bool dark = IsDark;
        return resourceKey switch
        {
            "AppBackgroundBrush" => dark ? BrushDarkPageBg : BrushLightPageBg,
            "CardBackgroundBrush" => dark ? BrushDarkCardBg : BrushLightCardBg,
            "CardBorderBrush" => dark ? BrushDarkCardBorder : BrushLightCardBorder,
            "SubtleCardBackgroundBrush" => dark ? BrushDarkSubCardBg : BrushLightSubCardBg,
            "SubtleCardBorderBrush" => dark ? BrushDarkSubCardBorder : BrushLightSubCardBorder,
            "DividerBrush" => dark ? BrushDarkDivider : BrushLightDivider,

            "TextPrimaryBrush" => dark ? BrushDarkTextPrimary : BrushLightTextPrimary,
            "TextSecondaryBrush" => dark ? BrushDarkTextSecondary : BrushLightTextSecondary,
            "TextMutedBrush" => dark ? BrushDarkTextMuted : BrushLightTextMuted,
            "TextHeadingBrush" => dark ? BrushDarkTextHeading : BrushLightTextHeading,
            "TextLabelBrush" => dark ? BrushDarkTextLabel : BrushLightTextLabel,

            "HyperlinkBrush" => dark ? BrushDarkHyperlink : BrushLightHyperlink,
            "SiteCodeBrush" => dark ? BrushDarkSiteCode : BrushLightSiteCode,
            "FlowDaysBrush" => dark ? BrushDarkFlowDays : BrushLightFlowDays,

            "WarningBannerBackgroundBrush" => dark ? BrushDarkWarningBg : BrushLightWarningBg,
            "WarningBannerBorderBrush" => dark ? BrushDarkWarningBorder : BrushLightWarningBorder,
            "WarningBannerTextBrush" => dark ? BrushDarkWarningText : BrushLightWarningText,

            "ErrorBannerBackgroundBrush" => dark ? BrushDarkErrorBg : BrushLightErrorBg,
            "ErrorBannerBorderBrush" => dark ? BrushDarkErrorBorder : BrushLightErrorBorder,
            "ErrorBannerTextBrush" => dark ? BrushDarkErrorText : BrushLightErrorText,

            "RankingWetStreaksTitleBrush" => dark ? BrushDarkFlowDays : BrushLightRankingWetStreaksTitle,
            "RankingDryStreaksTitleBrush" => dark ? BrushDarkRankingDryTitle : BrushLightRankingDryTitle,
            "RankingAnnualMeanTitleBrush" => dark ? BrushDarkHyperlink : BrushLightSiteCode,
            "RankingPeakFlowTitleBrush" => dark ? BrushDarkRankingPeakTitle : BrushLightRankingPeakTitle,
            "RankingWettestHeaderBrush" => dark ? BrushDarkFlowDays : BrushLightFlowDays,
            "RankingDriestHeaderBrush" => dark ? BrushDarkRankingDryTitle : BrushLightRankingDriestHeader,

            "ChartGridLineBrush" => dark ? BrushDarkChartGrid : BrushLightChartGrid,
            "ChartBandFillBrush" => dark ? BrushDarkBandFill : BrushLightBandFill,
            "ChartBandBorderBrush" => dark ? BrushDarkBandBorder : BrushLightBandBorder,
            "ChartMedianLineBrush" => dark ? BrushDarkMedianLine : BrushLightMedianLine,
            "ChartMarkerFillBrush" => dark ? BrushDarkMedianLine : BrushLightMedianLine,
            "ChartMarkerStrokeBrush" => dark ? BrushDarkCardBg : BrushLightCardBg,

            "HeatmapNoDataBrush" => dark ? BrushDarkHeatmapNoData : BrushLightHeatmapNoData,
            "HeatmapCellBorderBrush" => dark ? BrushDarkHeatmapCellBorder : BrushLightHeatmapCellBorder,
            "HeatmapHeaderTextBrush" => dark ? BrushDarkTextMuted : BrushLightTextSecondary,
            "HeatmapLegendBorderBrush" => dark ? BrushDarkControlBorder : BrushLightControlBorder,

            "YearFacetCardBackgroundBrush" => dark ? BrushDarkCardBg : BrushLightCardBg,
            "YearFacetCardBorderBrush" => dark ? BrushDarkCardBorder : BrushLightYearFacetCardBorder,
            "YearFacetTextBrush" => dark ? BrushDarkTextMuted : BrushLightTextMuted,
            "YearFacetBarBrush" => dark ? BrushDarkYearFacetBar : BrushLightYearFacetBar,
            "YearFacetLineBrush" => dark ? BrushDarkHyperlink : BrushLightYearFacetLine,

            "ControlBackgroundBrush" => dark ? BrushDarkControlBg : BrushLightControlBg,
            "ControlBorderBrush" => dark ? BrushDarkControlBorder : BrushLightControlBorder,
            "ControlForegroundBrush" => dark ? BrushDarkControlFg : BrushLightControlFg,

            "ButtonBackgroundBrush" => dark ? BrushDarkSubCardBg : BrushLightSubCardBg,
            "ButtonForegroundBrush" => dark ? BrushDarkTextHeading : BrushLightTextHeading,
            "ButtonBorderBrush" => dark ? BrushDarkControlBorder : BrushLightControlBorder,

            "ActionButtonBackgroundBrush" => BrushActionButtonBg,
            "ActionButtonBorderBrush" => dark ? BrushDarkActionButtonBorder : BrushLightActionButtonBorder,
            "ActionButtonForegroundBrush" => BrushWhite,

            _ => fallback
        };
    }

    public static string ThemeButtonLabel => _currentTheme switch
    {
        AppTheme.System => _isDark ? "🌓 System (Dark)" : "🌓 System (Light)",
        AppTheme.Dark => "🌙 Dark",
        AppTheme.Light => "☀️ Light",
        _ => "🌓 Theme"
    };
}

