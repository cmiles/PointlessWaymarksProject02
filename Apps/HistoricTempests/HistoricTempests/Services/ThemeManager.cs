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

public sealed class ThemeManager : INotifyPropertyChanged
{
    private static readonly ThemeManager _instance = new();
    public static ThemeManager Current => _instance;

    private AppThemeMode _mode = AppThemeMode.System;
    private bool _isSystemDark;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? ThemeChanged;

    private ThemeManager() { }

    public void Initialize()
    {
        try
        {
            object? savedObj = OpenSilver.Interop.ExecuteJavaScript(
                "localStorage.getItem('ht_theme_mode') || 'System'");
            string saved = savedObj?.ToString() ?? "System";
            if (Enum.TryParse<AppThemeMode>(saved, out var savedMode))
            {
                _mode = savedMode;
            }

            object? darkObj = OpenSilver.Interop.ExecuteJavaScript(
                "Boolean(window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches)");
            if (darkObj is bool b) _isSystemDark = b;
            else if (darkObj is int i) _isSystemDark = (i != 0);
            else if (darkObj is double d) _isSystemDark = (d != 0);
            else if (bool.TryParse(darkObj?.ToString(), out var parsedDark)) _isSystemDark = parsedDark;
            else if (int.TryParse(darkObj?.ToString(), out var parsedInt)) _isSystemDark = (parsedInt != 0);

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
            System.Diagnostics.Debug.WriteLine("ThemeManager Init error: " + ex.Message);
        }

        NotifyThemeChanged();
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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        ThemeChanged?.Invoke(this, EventArgs.Empty);

        try
        {
            string hostBg = IsDark ? "#0B0F19" : "#F8FAFC";
            string themeClass = IsDark ? "dark-theme" : "light-theme";
            OpenSilver.Interop.ExecuteJavaScriptVoid($@"
                document.body.style.backgroundColor = '{hostBg}';
                document.body.classList.remove('dark-theme', 'light-theme');
                document.body.classList.add('{themeClass}');
            ");
        }
        catch { }
    }
}
