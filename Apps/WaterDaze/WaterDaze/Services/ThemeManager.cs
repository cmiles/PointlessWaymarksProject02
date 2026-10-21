using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
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
                    var oldStyle = document.getElementById('waterdaze-theme-controls');
                    if (oldStyle) {{
                        oldStyle.remove();
                    }}
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
                        mainGrid.Children.Clear();
                        mainGrid.Children.Add(new MainPage());
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

    public static string ThemeButtonLabel => _currentTheme switch
    {
        AppTheme.System => _isDark ? "🌓 System (Dark)" : "🌓 System (Light)",
        AppTheme.Dark => "🌙 Dark",
        AppTheme.Light => "☀️ Light",
        _ => "🌓 Theme"
    };
}

