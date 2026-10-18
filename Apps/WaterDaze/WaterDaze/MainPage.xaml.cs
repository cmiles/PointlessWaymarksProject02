using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WaterDaze.Services;
using WaterDaze.ViewModels;

namespace WaterDaze;

public partial class MainPage
{
    private bool _isUpdatingThemeSelector;
    private readonly Dictionary<DependencyObject, (Brush? Bg, Brush? Border, Brush? Fg)> _originalLightBrushes = new();

    public MainPage()
    {
        InitializeComponent();

        ViewModel = new MainViewModel();
        DataContext = ViewModel;

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        UpdateBannerVisibilities();

        Loaded += (_, _) =>
        {
            UpdateResponsiveLayout(ActualWidth > 0 ? ActualWidth : RankingsGrid?.ActualWidth ?? 0);
            ApplyTheme();
            Dispatcher.BeginInvoke(() =>
            {
                if (LayoutRoot != null)
                {
                    ApplyThemeToTree(LayoutRoot, ThemeManager.IsDark);
                }
                _ = ViewModel.CheckForUpdatesAsync(silent: true);
            });
        };

        SizeChanged += (_, e) => UpdateResponsiveLayout(e.NewSize.Width > 0 ? e.NewSize.Width : ActualWidth);
        ThemeManager.ThemeChanged += (_, _) => ApplyTheme();
        ApplyTheme();
    }

    public MainViewModel ViewModel { get; }

    public void ApplyTheme()
    {
        bool isDark = ThemeManager.IsDark;

        if (MainScrollViewer != null)
        {
            MainScrollViewer.Background = ThemeManager.PageBackground;
        }

        if (LayoutRoot != null)
        {
            LayoutRoot.Background = ThemeManager.PageBackground;
        }

        SyncThemeSelector();

        UpdateWindowControlElement?.ApplyTheme(isDark);

        if (LayoutRoot != null)
        {
            ApplyThemeToTree(LayoutRoot, isDark);
        }
    }

    private void SyncThemeSelector()
    {
        if (TxtThemeToggle != null)
        {
            TxtThemeToggle.Text = ThemeManager.ThemeButtonLabel;
        }
    }

    private void ApplyThemeToTree(DependencyObject obj, bool isDark)
    {
        if (obj is Controls.MonthlyFlowBandChart ||
            obj is Controls.MonthlyHeatmapControl ||
            obj is Controls.YearFacetControl)
        {
            return;
        }

        if (obj is Button btn)
        {
            if (btn == BtnRefresh || btn == BtnThemeToggle || btn == BtnUpdates)
            {
                btn.Background = ThemeManager.ActionButtonBackground;
                btn.BorderBrush = ThemeManager.ActionButtonBorder;
                btn.Foreground = ThemeManager.ActionButtonForeground;
            }
            return;
        }

        if (obj is ComboBox cmb)
        {
            cmb.Background = ThemeManager.ControlBackground;
            cmb.BorderBrush = ThemeManager.ControlBorder;
            cmb.Foreground = ThemeManager.ControlForeground;
            return;
        }

        if (!_originalLightBrushes.ContainsKey(obj))
        {
            Brush? origBg = (obj as Control)?.Background ?? (obj as Border)?.Background ?? (obj as Panel)?.Background;
            Brush? origBorder = (obj as Control)?.BorderBrush ?? (obj as Border)?.BorderBrush;
            Brush? origFg = (obj as Control)?.Foreground ?? (obj as TextBlock)?.Foreground;
            _originalLightBrushes[obj] = (origBg, origBorder, origFg);
        }

        var (lightBg, lightBorder, lightFg) = _originalLightBrushes[obj];

        if (isDark)
        {
            if (obj is Border b)
            {
                if (lightBg is SolidColorBrush scbBg)
                {
                    if (IsColor(scbBg, 255, 255, 255))
                        b.Background = ThemeManager.BrushDarkCardBg;
                    else if (IsColor(scbBg, 243, 244, 246))
                        b.Background = ThemeManager.BrushDarkSubCardBg;
                    else if (IsColor(scbBg, 249, 250, 251))
                        b.Background = ThemeManager.BrushDarkPageBg;
                    else if (IsColor(scbBg, 229, 231, 235))
                        b.Background = ThemeManager.BrushDarkDivider;
                    else if (IsColor(scbBg, 255, 251, 235))
                        b.Background = ThemeManager.BrushDarkWarningBg;
                    else if (IsColor(scbBg, 254, 242, 242))
                        b.Background = ThemeManager.BrushDarkErrorBg;
                    else if (IsColor(scbBg, 217, 219, 234, 254))
                        b.Background = ThemeManager.BrushDarkCardBg;
                    else if (IsColor(scbBg, 102, 0, 0, 0))
                        b.Background = new SolidColorBrush(Color.FromArgb(153, 0, 0, 0));
                }

                if (lightBorder is SolidColorBrush scbBd)
                {
                    if (IsColor(scbBd, 229, 231, 235))
                        b.BorderBrush = ThemeManager.BrushDarkCardBorder;
                    else if (IsColor(scbBd, 209, 213, 219))
                        b.BorderBrush = ThemeManager.BrushDarkControlBorder;
                    else if (IsColor(scbBd, 252, 211, 77))
                        b.BorderBrush = ThemeManager.BrushDarkWarningBorder;
                    else if (IsColor(scbBd, 254, 202, 202))
                        b.BorderBrush = ThemeManager.BrushDarkErrorBorder;
                    else if (IsColor(scbBd, 191, 219, 254))
                        b.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 59, 130, 246));
                }
            }
            else if (obj is TextBlock tb)
            {
                if (lightFg is SolidColorBrush scbFg)
                {
                    if (IsColor(scbFg, 17, 24, 39))
                        tb.Foreground = ThemeManager.BrushDarkTextPrimary;
                    else if (IsColor(scbFg, 31, 41, 55))
                        tb.Foreground = ThemeManager.BrushDarkTextHeading;
                    else if (IsColor(scbFg, 55, 65, 81))
                        tb.Foreground = ThemeManager.BrushDarkTextLabel;
                    else if (IsColor(scbFg, 75, 85, 99))
                        tb.Foreground = ThemeManager.BrushDarkTextSecondary;
                    else if (IsColor(scbFg, 107, 114, 128))
                        tb.Foreground = ThemeManager.BrushDarkTextMuted;
                    else if (IsColor(scbFg, 37, 99, 235))
                        tb.Foreground = ThemeManager.BrushDarkHyperlink;
                    else if (IsColor(scbFg, 29, 78, 216))
                        tb.Foreground = ThemeManager.BrushDarkSiteCode;
                    else if (IsColor(scbFg, 5, 150, 105))
                        tb.Foreground = ThemeManager.BrushDarkFlowDays;
                    else if (IsColor(scbFg, 4, 120, 87))
                        tb.Foreground = ThemeManager.BrushDarkFlowDays;
                    else if (IsColor(scbFg, 180, 83, 9))
                        tb.Foreground = ThemeManager.BrushDarkRankingDryTitle;
                    else if (IsColor(scbFg, 124, 58, 237))
                        tb.Foreground = ThemeManager.BrushDarkRankingPeakTitle;
                    else if (IsColor(scbFg, 217, 119, 6))
                        tb.Foreground = ThemeManager.BrushDarkRankingDryTitle;
                    else if (IsColor(scbFg, 146, 64, 14))
                        tb.Foreground = ThemeManager.BrushDarkWarningText;
                    else if (IsColor(scbFg, 220, 38, 38))
                        tb.Foreground = ThemeManager.BrushDarkErrorText;
                }
            }
            else if (obj is TextBox tbBox)
            {
                tbBox.Background = ThemeManager.BrushDarkControlBg;
                tbBox.BorderBrush = ThemeManager.BrushDarkControlBorder;
                tbBox.Foreground = ThemeManager.BrushDarkControlFg;
            }
            else if (obj is HyperlinkButton hyp)
            {
                hyp.Foreground = ThemeManager.BrushDarkHyperlink;
            }
        }
        else
        {
            if (obj is Border b)
            {
                b.Background = lightBg;
                b.BorderBrush = lightBorder;
            }
            else if (obj is TextBlock tb)
            {
                tb.Foreground = lightFg;
            }
            else if (obj is Control c)
            {
                c.Background = lightBg;
                c.BorderBrush = lightBorder;
                c.Foreground = lightFg;
            }
            else if (obj is HyperlinkButton hyp)
            {
                hyp.Foreground = ThemeManager.BrushLightHyperlink;
            }
        }

        int childCount = VisualTreeHelper.GetChildrenCount(obj);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(obj, i);
            if (child != null)
            {
                ApplyThemeToTree(child, isDark);
            }
        }
    }

    private static bool IsColor(SolidColorBrush brush, byte r, byte g, byte b)
    {
        return brush.Color.R == r && brush.Color.G == g && brush.Color.B == b;
    }

    private static bool IsColor(SolidColorBrush brush, byte a, byte r, byte g, byte b)
    {
        return brush.Color.A == a && brush.Color.R == r && brush.Color.G == g && brush.Color.B == b;
    }

    private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.CycleTheme();
    }

    private void Hyperlink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton btn && btn.NavigateUri != null)
        {
            try
            {
                OpenSilver.Interop.ExecuteJavaScriptVoid($"window.open('{btn.NavigateUri.AbsoluteUri}', '_blank');");
            }
            catch { }
        }
    }

    public void UpdateResponsiveLayout(double width)
    {
        if (RankingsGrid == null || CardWetStreaks == null || CardDryStreaks == null ||
            CardAnnualMeanFlow == null || CardPeakFlowDays == null)
            return;

        var isCompact = width is > 0 and < 720;

        if (isCompact)
        {
            if (RankingsCol1 != null)
                RankingsCol1.Width = new GridLength(0);

            Grid.SetRow(CardWetStreaks, 0);
            Grid.SetColumn(CardWetStreaks, 0);
            Grid.SetColumnSpan(CardWetStreaks, 2);
            CardWetStreaks.Margin = new Thickness(0, 0, 0, 12);

            Grid.SetRow(CardDryStreaks, 1);
            Grid.SetColumn(CardDryStreaks, 0);
            Grid.SetColumnSpan(CardDryStreaks, 2);
            CardDryStreaks.Margin = new Thickness(0, 0, 0, 12);

            Grid.SetRow(CardAnnualMeanFlow, 2);
            Grid.SetColumn(CardAnnualMeanFlow, 0);
            Grid.SetColumnSpan(CardAnnualMeanFlow, 2);
            CardAnnualMeanFlow.Margin = new Thickness(0, 0, 0, 12);

            Grid.SetRow(CardPeakFlowDays, 3);
            Grid.SetColumn(CardPeakFlowDays, 0);
            Grid.SetColumnSpan(CardPeakFlowDays, 2);
            CardPeakFlowDays.Margin = new Thickness(0, 0, 0, 12);
        }
        else
        {
            if (RankingsCol1 != null)
                RankingsCol1.Width = new GridLength(1, GridUnitType.Star);

            Grid.SetRow(CardWetStreaks, 0);
            Grid.SetColumn(CardWetStreaks, 0);
            Grid.SetColumnSpan(CardWetStreaks, 1);
            CardWetStreaks.Margin = new Thickness(0, 0, 6, 12);

            Grid.SetRow(CardDryStreaks, 0);
            Grid.SetColumn(CardDryStreaks, 1);
            Grid.SetColumnSpan(CardDryStreaks, 1);
            CardDryStreaks.Margin = new Thickness(6, 0, 0, 12);

            Grid.SetRow(CardAnnualMeanFlow, 1);
            Grid.SetColumn(CardAnnualMeanFlow, 0);
            Grid.SetColumnSpan(CardAnnualMeanFlow, 1);
            CardAnnualMeanFlow.Margin = new Thickness(0, 0, 6, 12);

            Grid.SetRow(CardPeakFlowDays, 1);
            Grid.SetColumn(CardPeakFlowDays, 1);
            Grid.SetColumnSpan(CardPeakFlowDays, 1);
            CardPeakFlowDays.Margin = new Thickness(6, 0, 0, 12);
        }
    }

    private void UpdateBannerVisibilities()
    {
        if (LoadingBanner != null)
            LoadingBanner.Visibility = ViewModel.IsLoading ? Visibility.Visible : Visibility.Collapsed;

        if (ErrorBanner != null)
            ErrorBanner.Visibility = ViewModel.HasError ? Visibility.Visible : Visibility.Collapsed;

        if (WarningBanner != null)
            WarningBanner.Visibility = ViewModel.HasWarning ? Visibility.Visible : Visibility.Collapsed;
    }
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsLoading) or nameof(MainViewModel.HasError) or nameof(MainViewModel.HasWarning))
            UpdateBannerVisibilities();

        if (e.PropertyName == nameof(MainViewModel.UpdateWindowVisibility))
        {
            if (ViewModel.UpdateWindowVisibility == Visibility.Visible)
            {
                UpdateWindowControlElement?.ApplyTheme(ThemeManager.IsDark);
            }
        }

        if (e.PropertyName is nameof(MainViewModel.DashboardData) or nameof(MainViewModel.SelectedSite))
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (LayoutRoot != null)
                {
                    ApplyThemeToTree(LayoutRoot, ThemeManager.IsDark);
                }
            });
        }
    }

    private void BtnUpdates_Click(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.CheckForUpdatesAsync(silent: false);
    }
}
