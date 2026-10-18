using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HistoricTempests.Models;
using HistoricTempests.Services;
using HistoricTempests.ViewModels;

namespace HistoricTempests;

public partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();

        ViewModel = new MainViewModel();
        DataContext = ViewModel;

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        Loaded += MainPage_Loaded;
        SizeChanged += MainPage_SizeChanged;
        MouseLeftButtonDown += (s, e) =>
        {
            if (PopupYearFilter != null && PopupYearFilter.IsOpen)
            {
                PopupYearFilter.IsOpen = false;
            }
        };

        ThemeManager.Current.Initialize();
        ThemeManager.Current.ThemeChanged += (_, _) => ApplyTheme();
        ApplyTheme();
    }

    public MainViewModel ViewModel { get; }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateResponsiveLayout(ActualWidth);
        ApplyTheme();
        await ViewModel.LoadDataAsync();
        UpdateAllViews();
        PopulateYearDropdown();
        ApplyTheme();
        Dispatcher.BeginInvoke(() =>
        {
            if (LayoutRoot != null)
            {
                ApplyThemeToTree(LayoutRoot, ThemeManager.Current.IsDark);
            }
            RenderActiveTabCharts(ViewModel.SelectedTab ?? "Timeline14Days");
            _ = ViewModel.CheckForUpdatesAsync(silent: true);
        });
    }

    private void MainPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveLayout(e.NewSize.Width);
    }

    private void UpdateResponsiveLayout(double width)
    {
        if (width <= 0) return;

        bool isSmall = width < 768;
        if (WideHeaderPanel != null)
            WideHeaderPanel.Visibility = isSmall ? Visibility.Collapsed : Visibility.Visible;
        if (MobileHeaderPanel != null)
            MobileHeaderPanel.Visibility = isSmall ? Visibility.Visible : Visibility.Collapsed;
        if (!isSmall && MobileMenuDropdown != null)
            MobileMenuDropdown.Visibility = Visibility.Collapsed;

        if (LayoutRoot != null)
        {
            LayoutRoot.Margin = isSmall ? new Thickness(4, 10, 4, 30) : new Thickness(16, 20, 16, 40);
        }
        if (HeaderBarBorder != null)
        {
            HeaderBarBorder.Padding = isSmall ? new Thickness(12, 10, 12, 10) : new Thickness(20, 16, 20, 16);
        }
    }

    private void BtnHamburger_Click(object sender, RoutedEventArgs e)
    {
        if (MobileMenuDropdown != null)
        {
            MobileMenuDropdown.Visibility = MobileMenuDropdown.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }

    private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.Current.CycleTheme();
    }

    private void AuthorLink_Click(object sender, RoutedEventArgs e)
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

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ErrorMessage))
        {
            if (ErrorBanner != null)
                ErrorBanner.Visibility = !string.IsNullOrEmpty(ViewModel.ErrorMessage) ? Visibility.Visible : Visibility.Collapsed;
        }
        else if (e.PropertyName == nameof(MainViewModel.IsMetric))
        {
            UpdateAllViews();
        }
    }

    private void UpdateAllViews()
    {
        // 1. 14-Day Timeline Charts
        if (Chart14DayTempDual != null)
        {
            Chart14DayTempDual.DayData = ViewModel.DaysTimeline;
            Chart14DayTempDual.IsMetric = ViewModel.IsMetric;
            Chart14DayTempDual.RenderChart();
        }
        if (Chart14DayTempAvg != null)
        {
            Chart14DayTempAvg.DayData = ViewModel.DaysTimeline;
            Chart14DayTempAvg.IsMetric = ViewModel.IsMetric;
            Chart14DayTempAvg.RenderChart();
        }
        if (Chart14DayWind != null)
        {
            Chart14DayWind.DayData = ViewModel.DaysTimeline;
            Chart14DayWind.IsMetric = ViewModel.IsMetric;
            Chart14DayWind.RenderChart();
        }
        if (Chart14DayCombined != null)
        {
            Chart14DayCombined.DayData = ViewModel.DaysTimeline;
            Chart14DayCombined.IsMetric = ViewModel.IsMetric;
            Chart14DayCombined.RenderChart();
        }
        if (Chart14DaySolar != null)
        {
            Chart14DaySolar.DayData = ViewModel.DaysTimeline;
            Chart14DaySolar.IsMetric = ViewModel.IsMetric;
            Chart14DaySolar.RenderChart();
        }

        // 2. 6-Month Timeline Charts
        if (Chart6MonthTempAvg != null)
        {
            Chart6MonthTempAvg.MonthData = ViewModel.MonthsTimeline;
            Chart6MonthTempAvg.IsMetric = ViewModel.IsMetric;
            Chart6MonthTempAvg.RenderChart();
        }
        if (Chart6MonthTempDual != null)
        {
            Chart6MonthTempDual.MonthData = ViewModel.MonthsTimeline;
            Chart6MonthTempDual.IsMetric = ViewModel.IsMetric;
            Chart6MonthTempDual.RenderChart();
        }
        if (Chart6MonthWind != null)
        {
            Chart6MonthWind.MonthData = ViewModel.MonthsTimeline;
            Chart6MonthWind.IsMetric = ViewModel.IsMetric;
            Chart6MonthWind.RenderChart();
        }
        if (Chart6MonthCombined != null)
        {
            Chart6MonthCombined.MonthData = ViewModel.MonthsTimeline;
            Chart6MonthCombined.IsMetric = ViewModel.IsMetric;
            Chart6MonthCombined.RenderChart();
        }
        if (Chart6MonthSolar != null)
        {
            Chart6MonthSolar.MonthData = ViewModel.MonthsTimeline;
            Chart6MonthSolar.IsMetric = ViewModel.IsMetric;
            Chart6MonthSolar.RenderChart();
        }

        // 3. 12-Month Timeline Charts
        if (Chart12MonthTempAvg != null)
        {
            Chart12MonthTempAvg.MonthData = ViewModel.Months12Timeline;
            Chart12MonthTempAvg.IsMetric = ViewModel.IsMetric;
            Chart12MonthTempAvg.RenderChart();
        }
        if (Chart12MonthTempDual != null)
        {
            Chart12MonthTempDual.MonthData = ViewModel.Months12Timeline;
            Chart12MonthTempDual.IsMetric = ViewModel.IsMetric;
            Chart12MonthTempDual.RenderChart();
        }
        if (Chart12MonthWind != null)
        {
            Chart12MonthWind.MonthData = ViewModel.Months12Timeline;
            Chart12MonthWind.IsMetric = ViewModel.IsMetric;
            Chart12MonthWind.RenderChart();
        }
        if (Chart12MonthCombined != null)
        {
            Chart12MonthCombined.MonthData = ViewModel.Months12Timeline;
            Chart12MonthCombined.IsMetric = ViewModel.IsMetric;
            Chart12MonthCombined.RenderChart();
        }
        if (Chart12MonthSolar != null)
        {
            Chart12MonthSolar.MonthData = ViewModel.Months12Timeline;
            Chart12MonthSolar.IsMetric = ViewModel.IsMetric;
            Chart12MonthSolar.RenderChart();
        }

        // 4. Rain Days Giant Heatmap
        if (ChartAllRainDays != null)
        {
            ChartAllRainDays.DayData = ViewModel.FullYearRainDaysTimeline;
            ChartAllRainDays.IsMetric = ViewModel.IsMetric;
            ChartAllRainDays.RenderChart();
        }
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadDataAsync();
        UpdateAllViews();
    }

    private void BtnUpdates_Click(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.CheckForUpdatesAsync(silent: false);
    }

    private void TabBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tabName)
        {
            SwitchTab(tabName);
        }
    }

    private void SwitchTab(string tabName)
    {
        ViewModel.SelectedTab = tabName;
        UpdateTabStyles(tabName);

        if (ViewTimeline14Days != null) ViewTimeline14Days.Visibility = tabName == "Timeline14Days" ? Visibility.Visible : Visibility.Collapsed;
        if (ViewTimeline6Month != null) ViewTimeline6Month.Visibility = tabName == "Timeline6Month" ? Visibility.Visible : Visibility.Collapsed;
        if (ViewTimeline12Month != null) ViewTimeline12Month.Visibility = tabName == "Timeline12Month" ? Visibility.Visible : Visibility.Collapsed;
        if (ViewOverview != null) ViewOverview.Visibility = tabName == "Overview" ? Visibility.Visible : Visibility.Collapsed;
        if (ViewRainDays != null) ViewRainDays.Visibility = tabName == "RainDays" ? Visibility.Visible : Visibility.Collapsed;
        if (ViewDaily != null) ViewDaily.Visibility = tabName == "Daily" ? Visibility.Visible : Visibility.Collapsed;
        if (ViewAbout != null) ViewAbout.Visibility = tabName == "About" ? Visibility.Visible : Visibility.Collapsed;

        RenderActiveTabCharts(tabName);

        Dispatcher.BeginInvoke(() =>
        {
            if (LayoutRoot != null)
            {
                ApplyThemeToTree(LayoutRoot, ThemeManager.Current.IsDark);
            }
            RenderActiveTabCharts(tabName);
        });
    }

    private void RenderActiveTabCharts(string tabName)
    {
        if (tabName == "Timeline14Days")
        {
            Chart14DayTempDual?.RenderChart();
            Chart14DayTempAvg?.RenderChart();
            Chart14DayWind?.RenderChart();
            Chart14DayCombined?.RenderChart();
            Chart14DaySolar?.RenderChart();
        }
        else if (tabName == "Timeline6Month")
        {
            Chart6MonthTempAvg?.RenderChart();
            Chart6MonthTempDual?.RenderChart();
            Chart6MonthWind?.RenderChart();
            Chart6MonthCombined?.RenderChart();
            Chart6MonthSolar?.RenderChart();
        }
        else if (tabName == "Timeline12Month")
        {
            Chart12MonthTempAvg?.RenderChart();
            Chart12MonthTempDual?.RenderChart();
            Chart12MonthWind?.RenderChart();
            Chart12MonthCombined?.RenderChart();
            Chart12MonthSolar?.RenderChart();
        }
        else if (tabName == "RainDays")
        {
            ChartAllRainDays?.RenderChart();
        }
    }

    private void UpdateTabStyles(string activeTab)
    {
        var tm = ThemeManager.Current;
        Button[] tabBtns = [TabBtn14Days, TabBtn6Month, TabBtn12Month, TabBtnOverview, TabBtnRainDays, TabBtnDaily, TabBtnAbout];
        foreach (var b in tabBtns)
        {
            if (b == null) continue;
            bool isActive = b.Tag?.ToString() == activeTab;
            b.Background = isActive ? tm.NavActiveBg : tm.NavInactiveBg;
            b.BorderBrush = isActive ? tm.NavActiveBorder : tm.NavInactiveBorder;
            b.BorderThickness = new Thickness(1.5);
            b.Foreground = isActive ? tm.NavActiveFg : tm.NavInactiveFg;
            b.FontWeight = isActive ? FontWeights.Bold : FontWeights.SemiBold;
        }
    }

    private readonly Dictionary<DependencyObject, (Brush? Bg, Brush? Border, Brush? Fg)> _originalLightBrushes = new();

    public void ApplyTheme()
    {
        bool isDark = ThemeManager.Current.IsDark;

        if (MainRootGrid != null) MainRootGrid.Background = ThemeManager.Current.PageBackground;
        if (MainPageScrollViewer != null) MainPageScrollViewer.Background = ThemeManager.Current.PageBackground;

        if (HeaderBarBorder != null)
        {
            HeaderBarBorder.Background = ThemeManager.Current.HeaderBackground;
            HeaderBarBorder.BorderBrush = ThemeManager.Current.HeaderBorder;
        }

        if (TxtAppTitle != null)
        {
            TxtAppTitle.Foreground = ThemeManager.Current.HeaderTitle;
        }
        if (TxtAuthorDash != null)
        {
            TxtAuthorDash.Foreground = ThemeManager.Current.HeaderSubtitle;
        }
        if (LnkAuthor != null)
        {
            LnkAuthor.Foreground = ThemeManager.Current.HeaderAccent;
        }
        if (TxtAppSubtitle != null)
        {
            TxtAppSubtitle.Foreground = ThemeManager.Current.HeaderSubtitle;
        }
        if (TxtHeaderBullet1 != null)
        {
            TxtHeaderBullet1.Foreground = ThemeManager.Current.HeaderBullet;
        }
        if (TxtHeaderBullet2 != null)
        {
            TxtHeaderBullet2.Foreground = ThemeManager.Current.HeaderBullet;
        }
        if (TxtLastDataLabel != null)
        {
            TxtLastDataLabel.Foreground = ThemeManager.Current.TextSecondary;
        }
        if (TxtLastDataDate != null)
        {
            TxtLastDataDate.Foreground = ThemeManager.Current.HeaderAccent;
        }
        if (TxtArchiveDaysLabel != null)
        {
            TxtArchiveDaysLabel.Foreground = ThemeManager.Current.TextSecondary;
        }
        if (TxtArchiveDays != null)
        {
            TxtArchiveDays.Foreground = ThemeManager.Current.HeaderAccent;
        }

        if (NavBarBorder != null)
        {
            NavBarBorder.Background = ThemeManager.Current.CardBackground;
            NavBarBorder.BorderBrush = ThemeManager.Current.CardBorder;
        }

        if (TxtThemeToggle != null)
        {
            TxtThemeToggle.Text = ThemeManager.Current.ThemeButtonLabel;
            TxtThemeToggle.Foreground = isDark ? ThemeManager.BrushWhite : ThemeManager.BrushLightTextPrimary;
        }
        if (BtnThemeToggle != null)
        {
            BtnThemeToggle.Background = isDark ? ThemeManager.BrushDarkNavInactiveBg : ThemeManager.BrushLightNavInactiveBg;
            BtnThemeToggle.BorderBrush = isDark ? ThemeManager.BrushDarkCardBorder : ThemeManager.BrushLightSubCardBorder;
            BtnThemeToggle.Foreground = isDark ? ThemeManager.BrushWhite : ThemeManager.BrushLightTextPrimary;
        }

        if (TxtThemeToggleMobile != null)
        {
            TxtThemeToggleMobile.Text = ThemeManager.Current.ThemeButtonLabel;
            TxtThemeToggleMobile.Foreground = isDark ? ThemeManager.BrushWhite : ThemeManager.BrushLightTextPrimary;
        }
        if (BtnThemeToggleMobile != null)
        {
            BtnThemeToggleMobile.Background = isDark ? ThemeManager.BrushDarkNavInactiveBg : ThemeManager.BrushLightNavInactiveBg;
            BtnThemeToggleMobile.BorderBrush = isDark ? ThemeManager.BrushDarkCardBorder : ThemeManager.BrushLightSubCardBorder;
            BtnThemeToggleMobile.Foreground = isDark ? ThemeManager.BrushWhite : ThemeManager.BrushLightTextPrimary;
        }
        if (BtnHamburger != null)
        {
            BtnHamburger.Background = isDark ? ThemeManager.BrushDarkNavInactiveBg : ThemeManager.BrushLightNavInactiveBg;
            BtnHamburger.BorderBrush = isDark ? ThemeManager.BrushDarkCardBorder : ThemeManager.BrushLightSubCardBorder;
            BtnHamburger.Foreground = isDark ? ThemeManager.BrushWhite : ThemeManager.BrushLightTextPrimary;
        }
        if (TxtAppTitleMobile != null)
        {
            TxtAppTitleMobile.Foreground = ThemeManager.Current.HeaderTitle;
        }
        if (TxtAppSubtitleMobile != null)
        {
            TxtAppSubtitleMobile.Foreground = ThemeManager.Current.HeaderSubtitle;
        }
        if (LnkAuthorMobile != null)
        {
            LnkAuthorMobile.Foreground = ThemeManager.Current.HeaderAccent;
        }
        if (MobileMenuDropdown != null)
        {
            MobileMenuDropdown.Background = ThemeManager.Current.CardBackground;
            MobileMenuDropdown.BorderBrush = ThemeManager.Current.CardBorder;
        }

        if (Resources.Contains("DailyRowDividerBrush") && Resources["DailyRowDividerBrush"] is SolidColorBrush divBrush)
        {
            divBrush.Color = isDark ? Color.FromArgb(255, 51, 65, 85) : Color.FromArgb(255, 241, 245, 249);
        }
        if (Resources.Contains("DailyDateFgBrush") && Resources["DailyDateFgBrush"] is SolidColorBrush dateBrush)
        {
            dateBrush.Color = isDark ? Color.FromArgb(255, 248, 250, 252) : Color.FromArgb(255, 30, 41, 59);
        }
        if (Resources.Contains("DailyMutedFgBrush") && Resources["DailyMutedFgBrush"] is SolidColorBrush mutedBrush)
        {
            mutedBrush.Color = isDark ? Color.FromArgb(255, 203, 213, 225) : Color.FromArgb(255, 71, 85, 105);
        }

        // Precipitation Streaks palette
        if (Resources.Contains("StreakItemBgBrush") && Resources["StreakItemBgBrush"] is SolidColorBrush streakBg)
            streakBg.Color = isDark ? Color.FromArgb(255, 30, 41, 59) : Color.FromArgb(255, 255, 255, 255);
        if (Resources.Contains("StreakItemBorderBrush") && Resources["StreakItemBorderBrush"] is SolidColorBrush streakBorder)
            streakBorder.Color = isDark ? Color.FromArgb(255, 51, 65, 85) : Color.FromArgb(255, 226, 232, 240);
        if (Resources.Contains("StreakDateFgBrush") && Resources["StreakDateFgBrush"] is SolidColorBrush streakDate)
            streakDate.Color = isDark ? Color.FromArgb(255, 248, 250, 252) : Color.FromArgb(255, 15, 23, 42);
        if (Resources.Contains("StreakDetailsFgBrush") && Resources["StreakDetailsFgBrush"] is SolidColorBrush streakDet)
            streakDet.Color = isDark ? Color.FromArgb(255, 148, 163, 184) : Color.FromArgb(255, 100, 116, 139);

        if (Resources.Contains("DryStreakTitleBrush") && Resources["DryStreakTitleBrush"] is SolidColorBrush dryTitle)
            dryTitle.Color = isDark ? Color.FromArgb(255, 251, 191, 36) : Color.FromArgb(255, 180, 83, 9);
        if (Resources.Contains("DryStreakBadgeBgBrush") && Resources["DryStreakBadgeBgBrush"] is SolidColorBrush dryBadgeBg)
            dryBadgeBg.Color = isDark ? Color.FromArgb(255, 69, 26, 3) : Color.FromArgb(255, 254, 243, 199);
        if (Resources.Contains("DryStreakBadgeBorderBrush") && Resources["DryStreakBadgeBorderBrush"] is SolidColorBrush dryBadgeBorder)
            dryBadgeBorder.Color = isDark ? Color.FromArgb(255, 180, 83, 9) : Color.FromArgb(255, 253, 230, 138);
        if (Resources.Contains("DryStreakBadgeFgBrush") && Resources["DryStreakBadgeFgBrush"] is SolidColorBrush dryBadgeFg)
            dryBadgeFg.Color = isDark ? Color.FromArgb(255, 253, 230, 138) : Color.FromArgb(255, 180, 83, 9);

        if (Resources.Contains("WetStreakTitleBrush") && Resources["WetStreakTitleBrush"] is SolidColorBrush wetTitle)
            wetTitle.Color = isDark ? Color.FromArgb(255, 56, 189, 248) : Color.FromArgb(255, 3, 105, 161);
        if (Resources.Contains("WetStreakBadgeBgBrush") && Resources["WetStreakBadgeBgBrush"] is SolidColorBrush wetBadgeBg)
            wetBadgeBg.Color = isDark ? Color.FromArgb(255, 8, 47, 73) : Color.FromArgb(255, 224, 242, 254);
        if (Resources.Contains("WetStreakBadgeBorderBrush") && Resources["WetStreakBadgeBorderBrush"] is SolidColorBrush wetBadgeBorder)
            wetBadgeBorder.Color = isDark ? Color.FromArgb(255, 2, 132, 199) : Color.FromArgb(255, 186, 230, 253);
        if (Resources.Contains("WetStreakBadgeFgBrush") && Resources["WetStreakBadgeFgBrush"] is SolidColorBrush wetBadgeFg)
            wetBadgeFg.Color = isDark ? Color.FromArgb(255, 125, 211, 252) : Color.FromArgb(255, 2, 132, 199);

        if (BorderYearFilter != null)
        {
            BorderYearFilter.Background = ThemeManager.Current.InputBg;
            BorderYearFilter.BorderBrush = ThemeManager.Current.InputBorder;
        }
        if (TxtSelectedYear != null)
        {
            TxtSelectedYear.Foreground = ThemeManager.Current.InputFg;
        }
        if (TxtSearchDaily != null)
        {
            TxtSearchDaily.Background = ThemeManager.Current.InputBg;
            TxtSearchDaily.Foreground = ThemeManager.Current.InputFg;
            TxtSearchDaily.BorderBrush = ThemeManager.Current.InputBorder;
        }

        UpdateTabStyles(ViewModel.SelectedTab ?? "Timeline14Days");
        PopulateYearDropdown();

        if (LayoutRoot != null)
        {
            ApplyThemeToTree(LayoutRoot, isDark);
        }

        RenderActiveTabCharts(ViewModel.SelectedTab ?? "Timeline14Days");
        UpdateWindowControlElement?.ApplyTheme(isDark);
    }

    private void BorderYearFilter_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (PopupYearFilter != null)
        {
            PopulateYearDropdown();
            PopupYearFilter.IsOpen = !PopupYearFilter.IsOpen;
            e.Handled = true;
        }
    }

    private void PopulateYearDropdown()
    {
        if (YearOptionsStack == null) return;
        YearOptionsStack.Children.Clear();
        bool isDark = ThemeManager.Current.IsDark;

        foreach (int yr in ViewModel.AvailableYears)
        {
            string label = yr == 0 ? "All Years" : yr.ToString();
            bool isSelected = yr == ViewModel.SelectedDailyYear;

            var optBorder = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 6, 10, 6),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = isSelected
                    ? (isDark ? new SolidColorBrush(Color.FromArgb(255, 30, 41, 59)) : new SolidColorBrush(Color.FromArgb(255, 226, 232, 240)))
                    : (Brush)Brushes.Transparent
            };
            var optText = new TextBlock
            {
                Text = label,
                FontSize = 13,
                FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isDark ? ThemeManager.BrushDarkTextPrimary : ThemeManager.BrushLightTextPrimary
            };
            optBorder.Child = optText;

            int targetYear = yr;
            optBorder.MouseEnter += (s, e) =>
            {
                optBorder.Background = ThemeManager.Current.IsDark
                    ? new SolidColorBrush(Color.FromArgb(255, 30, 41, 59))
                    : new SolidColorBrush(Color.FromArgb(255, 241, 245, 249));
            };
            optBorder.MouseLeave += (s, e) =>
            {
                optBorder.Background = targetYear == ViewModel.SelectedDailyYear
                    ? (ThemeManager.Current.IsDark ? new SolidColorBrush(Color.FromArgb(255, 30, 41, 59)) : new SolidColorBrush(Color.FromArgb(255, 226, 232, 240)))
                    : (Brush)Brushes.Transparent;
            };
            optBorder.MouseLeftButtonDown += (s, e) =>
            {
                ViewModel.SelectedDailyYear = targetYear;
                if (TxtSelectedYear != null)
                    TxtSelectedYear.Text = label;
                if (PopupYearFilter != null)
                    PopupYearFilter.IsOpen = false;
                e.Handled = true;
            };

            YearOptionsStack.Children.Add(optBorder);
        }
    }

    private void ApplyThemeToTree(DependencyObject obj, bool isDark)
    {
        if (obj == HeaderBarBorder || obj == NavBarBorder || obj == BorderYearFilter || obj == PopupYearFilter) return;

        if (obj is Controls.MultiYearTimelineLineChart ||
            obj is Controls.MultiYearTimelineBarChart ||
            obj is Controls.MultiYearDayHeatmapGrid ||
            obj is Controls.ComparisonBarBandChart ||
            obj is Controls.RecentDaysChart ||
            obj is Controls.AboutControl)
        {
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
                    else if (IsColor(scbBg, 248, 250, 252))
                        b.Background = ThemeManager.BrushDarkSubCardBg;
                    else if (IsColor(scbBg, 241, 245, 249))
                        b.Background = ThemeManager.BrushDarkCardBg;
                    else if (IsColor(scbBg, 226, 232, 240))
                        b.Background = ThemeManager.BrushDarkCardBorder;
                }
                if (lightBorder is SolidColorBrush scbBd)
                {
                    if (IsColor(scbBd, 226, 232, 240) || IsColor(scbBd, 203, 213, 225))
                        b.BorderBrush = ThemeManager.BrushDarkCardBorder;
                }
            }
            else if (obj is TextBlock tb)
            {
                if (lightFg is SolidColorBrush scbFg)
                {
                    if (IsColor(scbFg, 15, 23, 42) || IsColor(scbFg, 30, 41, 59))
                        tb.Foreground = ThemeManager.BrushDarkTextPrimary;
                    else if (IsColor(scbFg, 51, 65, 85) || IsColor(scbFg, 71, 85, 105))
                        tb.Foreground = ThemeManager.BrushDarkNavInactiveFg;
                    else if (IsColor(scbFg, 100, 116, 139))
                        tb.Foreground = ThemeManager.BrushDarkTextSecondary;
                }
            }
            else if (obj is TextBox txtBox)
            {
                txtBox.Background = ThemeManager.BrushDarkSubCardBg;
                txtBox.Foreground = ThemeManager.BrushDarkTextPrimary;
                txtBox.BorderBrush = ThemeManager.BrushDarkCardBorder;
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
            else if (obj is TextBox txtBox)
            {
                txtBox.Background = lightBg;
                txtBox.BorderBrush = lightBorder;
                txtBox.Foreground = lightFg;
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

    private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var records = ViewModel.DailyRecords;
            if (records == null || records.Count == 0) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Date,HighTemp_F,LowTemp_F,AvgTemp_F,Precip_Inches,WindGust_Mph,WindAvg_Mph,SolarRadiation_Wm2,UV_Index,LightningStrikes");
            foreach (var r in records.OrderBy(d => d.Date))
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0},{1:F1},{2:F1},{3:F1},{4:F2},{5:F1},{6:F1},{7:F0},{8:F1},{9}",
                    r.Date, r.TempHighF, r.TempLowF, r.TempAvgF, r.PrecipInches,
                    r.WindGustMph, r.WindAvgMph, r.SolarRadiationAvg, r.UvAvg, r.LightningStrikes));
            }

            var csvText = sb.ToString();
            var bytes = System.Text.Encoding.UTF8.GetBytes(csvText);
            var base64 = Convert.ToBase64String(bytes);

            var js = $@"
                var link = document.createElement('a');
                link.download = 'historic_tempest_daily_data.csv';
                link.href = 'data:text/csv;base64,{base64}';
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
            ";
            OpenSilver.Interop.ExecuteJavaScriptVoid(js);
        }
        catch (Exception ex)
        {
            ViewModel.ErrorMessage = "Export failed: " + ex.Message;
        }
    }

    private void TxtSearchDaily_TextChanged(object sender, TextChangedEventArgs e)
    {
    }
}
