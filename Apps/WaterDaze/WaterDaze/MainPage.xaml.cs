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

        CmbSites.DropDownOpened += (_, _) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                ApplyThemeToComboBox(CmbSites, ThemeManager.IsDark);
            });
        };

        CmbSites.SelectionChanged += (_, _) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                ApplyThemeToComboBox(CmbSites, ThemeManager.IsDark);
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

        this.Background = isDark ? ThemeManager.BrushDarkPageBg : ThemeManager.BrushLightPageBg;

        if (MainScrollViewer != null)
        {
            MainScrollViewer.Background = isDark ? ThemeManager.BrushDarkPageBg : ThemeManager.BrushLightPageBg;
        }

        if (LayoutRoot != null)
        {
            LayoutRoot.Background = isDark ? ThemeManager.BrushDarkPageBg : ThemeManager.BrushLightPageBg;
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

    private void ApplyThemeToTree(DependencyObject obj, bool isDark, HashSet<DependencyObject>? visited = null)
    {
        if (obj == null) return;
        visited ??= new HashSet<DependencyObject>();
        if (!visited.Add(obj)) return;

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
            TraverseTree(obj, isDark, visited);
            return;
        }

        if (obj is ComboBox cmb)
        {
            ApplyThemeToComboBox(cmb, isDark);
            TraverseTree(obj, isDark, visited);
            return;
        }

        if (obj != LayoutRoot && obj != MainScrollViewer)
        {
            if (isDark)
            {
                if (obj is Border b)
                {
                    if (b.Background is SolidColorBrush scbBg)
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
                        else if (IsColor(scbBg, 219, 234, 254) || IsColor(scbBg, 239, 246, 255) || IsColor(scbBg, 217, 219, 234, 254))
                            b.Background = ThemeManager.BrushDarkCardBg;
                        else if (IsColor(scbBg, 102, 0, 0, 0))
                            b.Background = new SolidColorBrush(Color.FromArgb(153, 0, 0, 0));
                    }

                    if (b.BorderBrush is SolidColorBrush scbBd)
                    {
                        if (IsColor(scbBd, 229, 231, 235) || IsColor(scbBd, 224, 224, 224))
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
                    if (tb.Foreground is SolidColorBrush scbFg)
                    {
                        if (IsColor(scbFg, 17, 24, 39))
                            tb.Foreground = ThemeManager.BrushDarkTextPrimary;
                        else if (IsColor(scbFg, 31, 41, 55))
                            tb.Foreground = ThemeManager.BrushDarkTextHeading;
                        else if (IsColor(scbFg, 55, 65, 81))
                            tb.Foreground = ThemeManager.BrushDarkTextLabel;
                        else if (IsColor(scbFg, 75, 85, 99))
                            tb.Foreground = ThemeManager.BrushDarkTextSecondary;
                        else if (IsColor(scbFg, 107, 114, 128) || IsColor(scbFg, 112, 112, 112))
                            tb.Foreground = ThemeManager.BrushDarkTextMuted;
                        else if (IsColor(scbFg, 37, 99, 235))
                            tb.Foreground = ThemeManager.BrushDarkHyperlink;
                        else if (IsColor(scbFg, 29, 78, 216))
                            tb.Foreground = ThemeManager.BrushDarkSiteCode;
                        else if (IsColor(scbFg, 5, 150, 105) || IsColor(scbFg, 4, 120, 87))
                            tb.Foreground = ThemeManager.BrushDarkFlowDays;
                        else if (IsColor(scbFg, 180, 83, 9) || IsColor(scbFg, 217, 119, 6))
                            tb.Foreground = ThemeManager.BrushDarkRankingDryTitle;
                        else if (IsColor(scbFg, 124, 58, 237))
                            tb.Foreground = ThemeManager.BrushDarkRankingPeakTitle;
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
                else if (obj is ComboBoxItem cbi)
                {
                    cbi.Background = ThemeManager.BrushDarkControlBg;
                    cbi.BorderBrush = ThemeManager.BrushDarkControlBorder;
                    cbi.Foreground = ThemeManager.BrushDarkControlFg;
                }
                else if (obj is Control ctrl && ctrl is not Button)
                {
                    ctrl.Background = ThemeManager.BrushDarkControlBg;
                    ctrl.BorderBrush = ThemeManager.BrushDarkControlBorder;
                    ctrl.Foreground = ThemeManager.BrushDarkControlFg;
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
                    if (b.Background is SolidColorBrush scbBg)
                    {
                        if (IsColor(scbBg, 30, 41, 59))
                            b.Background = ThemeManager.BrushLightCardBg;
                        else if (IsColor(scbBg, 22, 32, 50))
                            b.Background = ThemeManager.BrushLightSubCardBg;
                        else if (IsColor(scbBg, 15, 23, 42))
                            b.Background = ThemeManager.BrushLightPageBg;
                        else if (IsColor(scbBg, 51, 65, 85))
                            b.Background = ThemeManager.BrushLightDivider;
                        else if (IsColor(scbBg, 45, 29, 3))
                            b.Background = ThemeManager.BrushLightWarningBg;
                        else if (IsColor(scbBg, 49, 11, 11))
                            b.Background = ThemeManager.BrushLightErrorBg;
                        else if (IsColor(scbBg, 153, 0, 0, 0))
                            b.Background = new SolidColorBrush(Color.FromArgb(102, 0, 0, 0));
                    }

                    if (b.BorderBrush is SolidColorBrush scbBd)
                    {
                        if (IsColor(scbBd, 51, 65, 85))
                            b.BorderBrush = ThemeManager.BrushLightCardBorder;
                        else if (IsColor(scbBd, 71, 85, 105))
                            b.BorderBrush = ThemeManager.BrushLightControlBorder;
                        else if (IsColor(scbBd, 120, 53, 15))
                            b.BorderBrush = ThemeManager.BrushLightWarningBorder;
                        else if (IsColor(scbBd, 127, 29, 29))
                            b.BorderBrush = ThemeManager.BrushLightErrorBorder;
                        else if (IsColor(scbBd, 59, 130, 246))
                            b.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 191, 219, 254));
                    }
                }
                else if (obj is TextBlock tb)
                {
                    if (tb.Foreground is SolidColorBrush scbFg)
                    {
                        if (IsColor(scbFg, 248, 250, 252))
                            tb.Foreground = ThemeManager.BrushLightTextPrimary;
                        else if (IsColor(scbFg, 241, 245, 249))
                            tb.Foreground = ThemeManager.BrushLightTextHeading;
                        else if (IsColor(scbFg, 226, 232, 240))
                            tb.Foreground = ThemeManager.BrushLightTextLabel;
                        else if (IsColor(scbFg, 203, 213, 225))
                            tb.Foreground = ThemeManager.BrushLightTextSecondary;
                        else if (IsColor(scbFg, 148, 163, 184))
                            tb.Foreground = ThemeManager.BrushLightTextMuted;
                        else if (IsColor(scbFg, 96, 165, 250))
                            tb.Foreground = ThemeManager.BrushLightHyperlink;
                        else if (IsColor(scbFg, 52, 211, 153))
                            tb.Foreground = ThemeManager.BrushLightFlowDays;
                        else if (IsColor(scbFg, 251, 191, 36) || IsColor(scbFg, 217, 119, 6))
                            tb.Foreground = ThemeManager.BrushLightRankingDryTitle;
                        else if (IsColor(scbFg, 192, 132, 252))
                            tb.Foreground = ThemeManager.BrushLightRankingPeakTitle;
                        else if (IsColor(scbFg, 253, 230, 138))
                            tb.Foreground = ThemeManager.BrushLightWarningText;
                        else if (IsColor(scbFg, 252, 165, 165))
                            tb.Foreground = ThemeManager.BrushLightErrorText;
                    }
                }
                else if (obj is TextBox tbBox)
                {
                    tbBox.Background = ThemeManager.BrushLightControlBg;
                    tbBox.BorderBrush = ThemeManager.BrushLightControlBorder;
                    tbBox.Foreground = ThemeManager.BrushLightControlFg;
                }
                else if (obj is ComboBoxItem cbi)
                {
                    cbi.Background = ThemeManager.BrushLightControlBg;
                    cbi.BorderBrush = ThemeManager.BrushLightControlBorder;
                    cbi.Foreground = ThemeManager.BrushLightControlFg;
                }
                else if (obj is Control c && c is not Button)
                {
                    c.Background = ThemeManager.BrushLightControlBg;
                    c.BorderBrush = ThemeManager.BrushLightControlBorder;
                    c.Foreground = ThemeManager.BrushLightControlFg;
                }
                else if (obj is HyperlinkButton hyp)
                {
                    hyp.Foreground = ThemeManager.BrushLightHyperlink;
                }
            }
        }

        TraverseTree(obj, isDark, visited);
    }

    private void TraverseTree(DependencyObject obj, bool isDark, HashSet<DependencyObject> visited)
    {
        int childCount = VisualTreeHelper.GetChildrenCount(obj);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(obj, i);
            if (child != null)
            {
                ApplyThemeToTree(child, isDark, visited);
            }
        }

        if (obj is ScrollViewer sv && sv.Content is DependencyObject svContent)
        {
            ApplyThemeToTree(svContent, isDark, visited);
        }
        else if (obj is ContentControl cc && cc.Content is DependencyObject ccContent)
        {
            ApplyThemeToTree(ccContent, isDark, visited);
        }
        else if (obj is Panel p)
        {
            foreach (UIElement child in p.Children)
            {
                ApplyThemeToTree(child, isDark, visited);
            }
        }
        else if (obj is Border b && b.Child != null)
        {
            ApplyThemeToTree(b.Child, isDark, visited);
        }
    }

    private void ApplyThemeToComboBox(ComboBox cmb, bool isDark)
    {
        if (cmb == null) return;
        cmb.Background = ThemeManager.ControlBackground;
        cmb.BorderBrush = ThemeManager.ControlBorder;
        cmb.Foreground = ThemeManager.ControlForeground;

        int childCount = VisualTreeHelper.GetChildrenCount(cmb);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(cmb, i);
            if (child != null)
            {
                ApplyThemeToTree(child, isDark);
            }
        }

        try
        {
            for (int i = 0; i < cmb.Items.Count; i++)
            {
                if (cmb.ItemContainerGenerator?.ContainerFromIndex(i) is ComboBoxItem item)
                {
                    item.Background = ThemeManager.ControlBackground;
                    item.BorderBrush = ThemeManager.ControlBorder;
                    item.Foreground = ThemeManager.ControlForeground;
                    int icCount = VisualTreeHelper.GetChildrenCount(item);
                    for (int c = 0; c < icCount; c++)
                    {
                        var itemChild = VisualTreeHelper.GetChild(item, c);
                        if (itemChild != null)
                        {
                            ApplyThemeToTree(itemChild, isDark);
                        }
                    }
                }
            }
        }
        catch { }
    }

    private static bool IsColor(SolidColorBrush? brush, byte r, byte g, byte b)
    {
        if (brush == null) return false;
        return brush.Color.R == r && brush.Color.G == g && brush.Color.B == b;
    }

    private static bool IsColor(SolidColorBrush? brush, byte a, byte r, byte g, byte b)
    {
        if (brush == null) return false;
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
