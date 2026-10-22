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
    public MainPage() : this(null)
    {
    }

    public MainPage(MainViewModel? existingViewModel)
    {
        InitializeComponent();

        ViewModel = existingViewModel ?? new MainViewModel();
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

        _fontScaleDebounceTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _fontScaleDebounceTimer.Tick += (_, _) =>
        {
            _fontScaleDebounceTimer.Stop();
            ThemeManager.Current.FontScalePercent = _pendingFontScalePercent;
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
        if (!ViewModel.DailyRecords.Any())
        {
            await ViewModel.LoadDataAsync();
        }
        UpdateAllViews();
        PopulateYearDropdown();
        PopulateMonthlyYearDropdown();
        SwitchTab(ViewModel.SelectedTab ?? "Timeline14Days");
        ApplyTheme();
        Dispatcher.BeginInvoke(() =>
        {
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

    private readonly System.Windows.Threading.DispatcherTimer _fontScaleDebounceTimer;
    private bool _isUpdatingFontSlider;
    private double _pendingFontScalePercent = 100.0;

    private void SliderFontScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingFontSlider) return;
        _isUpdatingFontSlider = true;
        try
        {
            double newPercent = Math.Clamp(Math.Round(e.NewValue), 50.0, 300.0);
            _pendingFontScalePercent = newPercent;

            if (SliderFontScale != null && Math.Abs(SliderFontScale.Value - newPercent) > 0.5)
                SliderFontScale.Value = newPercent;
            if (SliderFontScaleMobile != null && Math.Abs(SliderFontScaleMobile.Value - newPercent) > 0.5)
                SliderFontScaleMobile.Value = newPercent;

            _fontScaleDebounceTimer.Stop();
            _fontScaleDebounceTimer.Start();
        }
        finally
        {
            _isUpdatingFontSlider = false;
        }
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
        if (ViewMonthly != null) ViewMonthly.Visibility = tabName == "Monthly" ? Visibility.Visible : Visibility.Collapsed;
        if (ViewAbout != null) ViewAbout.Visibility = tabName == "About" ? Visibility.Visible : Visibility.Collapsed;

        RenderActiveTabCharts(tabName);

        Dispatcher.BeginInvoke(() =>
        {
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
        Button[] tabBtns = [TabBtn14Days, TabBtn6Month, TabBtn12Month, TabBtnOverview, TabBtnRainDays, TabBtnDaily, TabBtnMonthly, TabBtnAbout];
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

    public void ApplyTheme()
    {
        if (TxtThemeToggle != null)
        {
            TxtThemeToggle.Text = ThemeManager.Current.ThemeButtonLabel;
        }

        if (TxtThemeToggleMobile != null)
        {
            TxtThemeToggleMobile.Text = ThemeManager.Current.ThemeButtonLabel;
        }

        if (SliderFontScale != null && !_isUpdatingFontSlider && Math.Abs(SliderFontScale.Value - ThemeManager.Current.FontScalePercent) > 0.5)
        {
            _isUpdatingFontSlider = true;
            try { SliderFontScale.Value = ThemeManager.Current.FontScalePercent; }
            finally { _isUpdatingFontSlider = false; }
        }

        if (SliderFontScaleMobile != null && !_isUpdatingFontSlider && Math.Abs(SliderFontScaleMobile.Value - ThemeManager.Current.FontScalePercent) > 0.5)
        {
            _isUpdatingFontSlider = true;
            try { SliderFontScaleMobile.Value = ThemeManager.Current.FontScalePercent; }
            finally { _isUpdatingFontSlider = false; }
        }

        if (MobileMenuDropdown != null)
        {
            MobileMenuDropdown.Background = ThemeManager.Current.CardBackground;
            MobileMenuDropdown.BorderBrush = ThemeManager.Current.CardBorder;
        }

        UpdateTabStyles(ViewModel?.SelectedTab ?? "Timeline14Days");
        PopulateYearDropdown();
        PopulateMonthlyYearDropdown();
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
                FontSize = ThemeManager.Current.FontSizeBody,
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

    private void BorderMonthlyYearFilter_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (PopupMonthlyYearFilter != null)
        {
            PopulateMonthlyYearDropdown();
            PopupMonthlyYearFilter.IsOpen = !PopupMonthlyYearFilter.IsOpen;
            e.Handled = true;
        }
    }

    private void PopulateMonthlyYearDropdown()
    {
        if (MonthlyYearOptionsStack == null) return;
        MonthlyYearOptionsStack.Children.Clear();
        bool isDark = ThemeManager.Current.IsDark;

        foreach (int yr in ViewModel.AvailableYears)
        {
            string label = yr == 0 ? "All Years" : yr.ToString();
            bool isSelected = yr == ViewModel.SelectedMonthlyYear;

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
                FontSize = ThemeManager.Current.FontSizeBody,
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
                optBorder.Background = targetYear == ViewModel.SelectedMonthlyYear
                    ? (ThemeManager.Current.IsDark ? new SolidColorBrush(Color.FromArgb(255, 30, 41, 59)) : new SolidColorBrush(Color.FromArgb(255, 226, 232, 240)))
                    : (Brush)Brushes.Transparent;
            };
            optBorder.MouseLeftButtonDown += (s, e) =>
            {
                ViewModel.SelectedMonthlyYear = targetYear;
                if (TxtSelectedMonthlyYear != null)
                    TxtSelectedMonthlyYear.Text = label;
                if (PopupMonthlyYearFilter != null)
                    PopupMonthlyYearFilter.IsOpen = false;
                e.Handled = true;
            };

            MonthlyYearOptionsStack.Children.Add(optBorder);
        }
    }

    private void BtnExportMonthlyExcel_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var records = ViewModel.MonthlyRecords;
            if (records == null || records.Count == 0) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Year,Month,MonthName,DaysCount,MeanHigh_F,MeanLow_F,AvgTemp_F,TotalPrecip_Inches,RainyDays,MaxGust_Mph,AvgWind_Mph,SolarRadiation_AvgWm2,UV_Index_Avg,LightningStrikes,LightningDays");
            foreach (var r in records.OrderBy(m => m.Year).ThenBy(m => m.Month))
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4:F1},{5:F1},{6:F1},{7:F2},{8},{9:F1},{10:F1},{11:F0},{12:F1},{13},{14}",
                    r.Year, r.Month, r.MonthName, r.DaysCount,
                    r.MeanHighF, r.MeanLowF, r.AvgTempF,
                    r.TotalPrecipInches, r.RainyDays,
                    r.MaxGustMph, r.AvgWindMph,
                    r.AvgSolarRadiation, r.AvgUvIndex,
                    r.LightningStrikes, r.LightningDays));
            }

            var csvText = sb.ToString();
            var bytes = System.Text.Encoding.UTF8.GetBytes(csvText);
            var base64 = Convert.ToBase64String(bytes);

            var js = $@"
                var link = document.createElement('a');
                link.download = 'historic_tempest_monthly_data.csv';
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

    private void TxtSearchMonthly_TextChanged(object sender, TextChangedEventArgs e)
    {
    }
}
