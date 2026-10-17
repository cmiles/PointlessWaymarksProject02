using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using HistoricTempests.Models;
using HistoricTempests.Services;

namespace HistoricTempests.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IWeatherReportService _weatherService;
    private bool _isLoading;
    private string _loadingStatus = "Ready";
    private string? _errorMessage;
    private string _dataLoadedTime = string.Empty;
    private string _selectedTab = "Timeline14Days"; // Default to 14 Days first page!
    private string _selectedMetric = "Rainfall";
    private RecentDayItem? _selectedRecentDay;
    private string _dailySearchText = string.Empty;
    private int _selectedDailyYear = 0;

    private StationMetadata? _station;
    private OverviewReport? _overview;

    // 6-Month Comparison Properties
    private string _sixMonthAvgTempThisYearText = "Avg Temp: 82.8°F";
    private string _sixMonthAvgTempDeltaText = "+0.7°F vs 82.1°F prev yrs avg";
    private SolidColorBrush _sixMonthAvgTempDeltaBrush = new(Color.FromArgb(255, 220, 38, 38));
    private string _sixMonthTempAvgMonthly = "May: 76.2°F  |  Jun: 86.5°F  |  Jul: 86.0°F  |  Aug: 86.7°F  |  Sep: 79.9°F  |  Oct: 78.5°F";

    private string _sixMonthAvgHighThisYearText = "Avg Max: 92.8°F";
    private string _sixMonthAvgHighDeltaText = "+0.1°F vs 92.7°F prev yrs avg";
    private SolidColorBrush _sixMonthAvgHighDeltaBrush = new(Color.FromArgb(255, 220, 38, 38));
    private string _sixMonthAvgLowThisYearText = "Avg Min: 72.7°F";
    private string _sixMonthAvgLowDeltaText = "+1.3°F vs 71.4°F prev yrs avg";
    private SolidColorBrush _sixMonthAvgLowDeltaBrush = new(Color.FromArgb(255, 220, 38, 38));
    private string _sixMonthTempRangeMonthly = "May: 99.0° / 52.0°  |  Jun: 100.2° / 68.4°  |  Jul: 104.5° / 68.5°  |  Aug: 105.4° / 67.1°  |  Sep: 94.8° / 60.3°  |  Oct: 92.3° / 61.0°";

    private string _sixMonthRainThisYearText = "Total Rain: 7.93 in";
    private string _sixMonthRainDeltaText = "-2.63 in vs 10.56 in prev yrs avg";
    private SolidColorBrush _sixMonthRainDeltaBrush = new(Color.FromArgb(255, 37, 99, 235));
    private string _sixMonthRainDaysText = "Rain Days: 51";
    private string _sixMonthRainDaysDeltaText = "+11.7 vs 39.3 prev yrs avg";
    private SolidColorBrush _sixMonthRainDaysDeltaBrush = new(Color.FromArgb(255, 5, 150, 105));
    private string _sixMonthRainMonthly = "May: 0.28\" (2d)  |  Jun: 0.45\" (6d)  |  Jul: 2.12\" (16d)  |  Aug: 3.15\" (17d)  |  Sep: 1.51\" (9d)  |  Oct: 0.42\" (1d)";

    private string _sixMonthWindThisYearText = "Avg Daily Gust: 23.4 mph";
    private string _sixMonthWindDeltaText = "+2.2 mph vs 21.2 mph prev yrs avg";
    private SolidColorBrush _sixMonthWindDeltaBrush = new(Color.FromArgb(255, 220, 38, 38));
    private string _sixMonthWindPeakText = "Peak Gust: 41.4 mph (Jun 29)";
    private string _sixMonthWindPeakDeltaText = "";
    private SolidColorBrush _sixMonthWindPeakDeltaBrush = new(Color.FromArgb(255, 220, 38, 38));
    private string _sixMonthWindMonthly = "May: 39.4 mph  |  Jun: 41.4 mph  |  Jul: 35.2 mph  |  Aug: 30.6 mph  |  Sep: 31.9 mph  |  Oct: 29.6 mph";

    private string _sixMonthLightningThisYearText = "Total Strikes: 19,534";
    private string _sixMonthLightningDeltaText = "+7,032 vs 12,502 prev yrs avg";
    private SolidColorBrush _sixMonthLightningDeltaBrush = new(Color.FromArgb(255, 124, 58, 237));
    private string _sixMonthLightningDaysText = "Days with Lightning: 74";
    private string _sixMonthLightningDaysDeltaText = "+9.0 vs 65.0 prev yrs avg";
    private SolidColorBrush _sixMonthLightningDaysDeltaBrush = new(Color.FromArgb(255, 217, 119, 6));
    private string _sixMonthLightningMonthly = "May: 96 (2d)  |  Jun: 1,555 (9d)  |  Jul: 5,498 (22d)  |  Aug: 7,307 (24d)  |  Sep: 5,015 (15d)  |  Oct: 63 (2d)";

    // 6-Month Tab Dedicated Summaries
    private string _sixMonthTabAvgTempThisYearText = "Avg Temp: --";
    private string _sixMonthTabAvgTempDeltaText = "";
    private SolidColorBrush _sixMonthTabAvgTempDeltaBrush = new(Color.FromArgb(255, 220, 38, 38));

    private string _sixMonthTabAvgHighThisYearText = "Avg Max: --";
    private string _sixMonthTabAvgHighDeltaText = "";
    private SolidColorBrush _sixMonthTabAvgHighDeltaBrush = new(Color.FromArgb(255, 220, 38, 38));
    private string _sixMonthTabAvgLowThisYearText = "Avg Min: --";
    private string _sixMonthTabAvgLowDeltaText = "";
    private SolidColorBrush _sixMonthTabAvgLowDeltaBrush = new(Color.FromArgb(255, 220, 38, 38));

    private string _sixMonthTabRainThisYearText = "Total Rain: --";
    private string _sixMonthTabRainDeltaText = "";
    private SolidColorBrush _sixMonthTabRainDeltaBrush = new(Color.FromArgb(255, 37, 99, 235));
    private string _sixMonthTabRainDaysText = "";
    private string _sixMonthTabRainDaysDeltaText = "";
    private SolidColorBrush _sixMonthTabRainDaysDeltaBrush = new(Color.FromArgb(255, 5, 150, 105));

    private string _sixMonthTabWindThisYearText = "Avg Daily Gust: --";
    private string _sixMonthTabWindDeltaText = "";
    private SolidColorBrush _sixMonthTabWindDeltaBrush = new(Color.FromArgb(255, 220, 38, 38));
    private string _sixMonthTabWindPeakText = "";
    private string _sixMonthTabWindPeakDeltaText = "";
    private SolidColorBrush _sixMonthTabWindPeakDeltaBrush = new(Color.FromArgb(255, 220, 38, 38));

    private string _sixMonthTabLightningThisYearText = "Total Strikes: --";
    private string _sixMonthTabLightningDeltaText = "";
    private SolidColorBrush _sixMonthTabLightningDeltaBrush = new(Color.FromArgb(255, 124, 58, 237));
    private string _sixMonthTabLightningDaysText = "";
    private string _sixMonthTabLightningDaysDeltaText = "";
    private SolidColorBrush _sixMonthTabLightningDaysDeltaBrush = new(Color.FromArgb(255, 217, 119, 6));

    // Header Info Properties
    private string _lastDataDateText = "--";
    private string _archiveDaysText = "--";

    // 12-Month Tab Dedicated Summaries
    private string _twelveMonthTabAvgTempThisYearText = "Avg Temp: --";
    private string _twelveMonthTabAvgTempDeltaText = "";
    private SolidColorBrush _twelveMonthTabAvgTempDeltaBrush = new(Color.FromArgb(255, 220, 38, 38));
    private string _twelveMonthTempAvgMonthly = "";

    private string _twelveMonthTabAvgHighThisYearText = "Avg Max: --";
    private string _twelveMonthTabAvgHighDeltaText = "";
    private SolidColorBrush _twelveMonthTabAvgHighDeltaBrush = new(Color.FromArgb(255, 220, 38, 38));
    private string _twelveMonthTabAvgLowThisYearText = "Avg Min: --";
    private string _twelveMonthTabAvgLowDeltaText = "";
    private SolidColorBrush _twelveMonthTabAvgLowDeltaBrush = new(Color.FromArgb(255, 220, 38, 38));
    private string _twelveMonthTempRangeMonthly = "";

    private string _twelveMonthTabRainThisYearText = "Total Rain: --";
    private string _twelveMonthTabRainDeltaText = "";
    private SolidColorBrush _twelveMonthTabRainDeltaBrush = new(Color.FromArgb(255, 37, 99, 235));
    private string _twelveMonthTabRainDaysText = "";
    private string _twelveMonthTabRainDaysDeltaText = "";
    private SolidColorBrush _twelveMonthTabRainDaysDeltaBrush = new(Color.FromArgb(255, 5, 150, 105));
    private string _twelveMonthRainMonthly = "";

    private string _twelveMonthTabWindThisYearText = "Avg Daily Gust: --";
    private string _twelveMonthTabWindDeltaText = "";
    private SolidColorBrush _twelveMonthTabWindDeltaBrush = new(Color.FromArgb(255, 220, 38, 38));
    private string _twelveMonthTabWindPeakText = "";
    private string _twelveMonthTabWindPeakDeltaText = "";
    private SolidColorBrush _twelveMonthTabWindPeakDeltaBrush = new(Color.FromArgb(255, 220, 38, 38));
    private string _twelveMonthWindMonthly = "";

    private string _twelveMonthTabLightningThisYearText = "Total Strikes: --";
    private string _twelveMonthTabLightningDeltaText = "";
    private SolidColorBrush _twelveMonthTabLightningDeltaBrush = new(Color.FromArgb(255, 124, 58, 237));
    private string _twelveMonthTabLightningDaysText = "";
    private string _twelveMonthTabLightningDaysDeltaText = "";
    private SolidColorBrush _twelveMonthTabLightningDaysDeltaBrush = new(Color.FromArgb(255, 217, 119, 6));
    private string _twelveMonthLightningMonthly = "";

    // Executive Overview Glance Blocks (12-Month)
    private string _glance12MonthRainDaysText = "--";
    private SolidColorBrush _glance12MonthRainDaysBrush = SlateGlanceBrush;
    private string _glance12MonthRainAmountText = "--";
    private SolidColorBrush _glance12MonthRainAmountBrush = SlateGlanceBrush;

    private string _glance12MonthTempMaxText = "--";
    private SolidColorBrush _glance12MonthTempMaxBrush = SlateGlanceBrush;
    private string _glance12MonthTempMinText = "--";
    private SolidColorBrush _glance12MonthTempMinBrush = SlateGlanceBrush;
    private string _glance12MonthTempAvgText = "--";
    private SolidColorBrush _glance12MonthTempAvgBrush = SlateGlanceBrush;

    private string _glance12MonthWindAvgText = "--";
    private SolidColorBrush _glance12MonthWindAvgBrush = SlateGlanceBrush;
    private string _glance12MonthWindPeakText = "--";

    private string _glance12MonthLightningStrikesText = "--";
    private SolidColorBrush _glance12MonthLightningStrikesBrush = SlateGlanceBrush;
    private string _glance12MonthLightningDaysText = "--";
    private SolidColorBrush _glance12MonthLightningDaysBrush = SlateGlanceBrush;

    // Executive Overview Glance Brushes & Helpers
    private static readonly SolidColorBrush RedGlanceBrush = new(Color.FromArgb(255, 220, 38, 38));    // #DC2626
    private static readonly SolidColorBrush BlueGlanceBrush = new(Color.FromArgb(255, 37, 99, 235));   // #2563EB
    private static readonly SolidColorBrush GreenGlanceBrush = new(Color.FromArgb(255, 5, 150, 105));  // #059669
    private static readonly SolidColorBrush AmberGlanceBrush = new(Color.FromArgb(255, 217, 119, 6)); // #D97706
    private static readonly SolidColorBrush SlateGlanceBrush = new(Color.FromArgb(255, 71, 85, 105));  // #475569

    private static SolidColorBrush GetRainBrush(double delta) =>
        delta > 0.001 ? GreenGlanceBrush : delta < -0.001 ? RedGlanceBrush : SlateGlanceBrush;

    private static SolidColorBrush GetTempBrush(double delta) =>
        delta > 0.001 ? RedGlanceBrush : delta < -0.001 ? GreenGlanceBrush : SlateGlanceBrush;

    private static SolidColorBrush GetWindBrush(double delta) =>
        delta > 0.001 ? RedGlanceBrush : delta < -0.001 ? BlueGlanceBrush : SlateGlanceBrush;

    private static SolidColorBrush GetLightningBrush(double delta) =>
        delta > 0.001 ? RedGlanceBrush : delta < -0.001 ? BlueGlanceBrush : SlateGlanceBrush;

    private static SolidColorBrush GetSolarBrush(double delta) =>
        delta > 0.5 ? AmberGlanceBrush : delta < -0.5 ? BlueGlanceBrush : SlateGlanceBrush;

    // Executive Overview Glance Blocks (14-Day)
    private string _glance14DayRainDaysText = "--";
    private SolidColorBrush _glance14DayRainDaysBrush = SlateGlanceBrush;
    private string _glance14DayRainAmountText = "--";
    private SolidColorBrush _glance14DayRainAmountBrush = SlateGlanceBrush;

    private string _glance14DayTempMaxText = "--";
    private SolidColorBrush _glance14DayTempMaxBrush = SlateGlanceBrush;
    private string _glance14DayTempMinText = "--";
    private SolidColorBrush _glance14DayTempMinBrush = SlateGlanceBrush;
    private string _glance14DayTempAvgText = "--";
    private SolidColorBrush _glance14DayTempAvgBrush = SlateGlanceBrush;

    private string _glance14DayWindAvgText = "--";
    private SolidColorBrush _glance14DayWindAvgBrush = SlateGlanceBrush;
    private string _glance14DayWindPeakText = "--";

    private string _glance14DayLightningStrikesText = "--";
    private SolidColorBrush _glance14DayLightningStrikesBrush = SlateGlanceBrush;
    private string _glance14DayLightningDaysText = "--";
    private SolidColorBrush _glance14DayLightningDaysBrush = SlateGlanceBrush;

    // Executive Overview Glance Blocks (6-Month)
    private string _glance6MonthRainDaysText = "--";
    private SolidColorBrush _glance6MonthRainDaysBrush = SlateGlanceBrush;
    private string _glance6MonthRainAmountText = "--";
    private SolidColorBrush _glance6MonthRainAmountBrush = SlateGlanceBrush;

    private string _glance6MonthTempMaxText = "--";
    private SolidColorBrush _glance6MonthTempMaxBrush = SlateGlanceBrush;
    private string _glance6MonthTempMinText = "--";
    private SolidColorBrush _glance6MonthTempMinBrush = SlateGlanceBrush;
    private string _glance6MonthTempAvgText = "--";
    private SolidColorBrush _glance6MonthTempAvgBrush = SlateGlanceBrush;

    private string _glance6MonthWindAvgText = "--";
    private SolidColorBrush _glance6MonthWindAvgBrush = SlateGlanceBrush;
    private string _glance6MonthWindPeakText = "--";

    private string _glance6MonthLightningStrikesText = "--";
    private SolidColorBrush _glance6MonthLightningStrikesBrush = SlateGlanceBrush;
    private string _glance6MonthLightningDaysText = "--";
    private SolidColorBrush _glance6MonthLightningDaysBrush = SlateGlanceBrush;

    // Solar Glance Blocks
    private string _glance14DaySolarAvgText = "--";
    private SolidColorBrush _glance14DaySolarAvgBrush = SlateGlanceBrush;
    private string _glance6MonthSolarAvgText = "--";
    private SolidColorBrush _glance6MonthSolarAvgBrush = SlateGlanceBrush;
    private string _glance12MonthSolarAvgText = "--";
    private SolidColorBrush _glance12MonthSolarAvgBrush = SlateGlanceBrush;

    // Dedicated Solar Tab Summaries
    private string _fourteenDayTabSolarThisYearText = "Avg Solar: --";
    private string _fourteenDayTabSolarDeltaText = "";
    private SolidColorBrush _fourteenDayTabSolarDeltaBrush = SlateGlanceBrush;

    private string _sixMonthTabSolarThisYearText = "Avg Solar: --";
    private string _sixMonthTabSolarDeltaText = "";
    private SolidColorBrush _sixMonthTabSolarDeltaBrush = SlateGlanceBrush;
    private string _sixMonthSolarMonthly = "";

    private string _twelveMonthSolarMonthly = "";

    public MainViewModel(IWeatherReportService? weatherService = null)
    {
        _weatherService = weatherService ?? new WeatherReportService();

        DaysTimeline = [];
        MonthsTimeline = [];
        Months12Timeline = [];
        FullYearRainDaysTimeline = [];
        RainDaysSummaries = [];
        AnnualSummaries = [];
        MonthlyMatrix = [];
        RecentDays = [];
        UpcomingWeek = [];
        DailyRecords = [];
        FilteredDailyRecords = [];
        AvailableYears = [0];

        Top10HottestDays = [];
        Top10ColdestDays = [];
        Top10HighestGusts = [];
        Top10RainiestDays = [];
        Top3DryStreaks = [];
        Top3WetStreaks = [];

        RefreshCommand = new RelayCommand(async () => await LoadDataAsync());
        ToggleUnitsCommand = new RelayCommand(() => { });
        SelectTabCommand = new RelayCommand<string>(tab => { if (!string.IsNullOrEmpty(tab)) SelectedTab = tab; });
        SelectMetricCommand = new RelayCommand<string>(m => { if (!string.IsNullOrEmpty(m)) SelectedMetric = m; });
        SelectRecentDayCommand = new RelayCommand<RecentDayItem>(day => SelectedRecentDay = day);

        ThemeManager.Current.ThemeChanged += (_, _) => OnPropertyChanged(nameof(Theme));
    }

    public ThemeManager Theme => ThemeManager.Current;

    public ObservableCollection<DayTimelinePoint> DaysTimeline { get; }
    public ObservableCollection<MonthTimelinePoint> MonthsTimeline { get; }
    public ObservableCollection<MonthTimelinePoint> Months12Timeline { get; }
    public ObservableCollection<DayTimelinePoint> FullYearRainDaysTimeline { get; }
    public ObservableCollection<RainDaysYearSummary> RainDaysSummaries { get; }

    public ObservableCollection<AnnualSummaryRecord> AnnualSummaries { get; }
    public ObservableCollection<MonthMatrixItem> MonthlyMatrix { get; }
    public ObservableCollection<RecentDayItem> RecentDays { get; }
    public ObservableCollection<UpcomingDayItem> UpcomingWeek { get; }
    public ObservableCollection<DailyWeatherRecord> DailyRecords { get; }
    public ObservableCollection<DailyWeatherRecord> FilteredDailyRecords { get; }
    public ObservableCollection<int> AvailableYears { get; }

    public ObservableCollection<WeatherRecordRankItem> Top10HottestDays { get; }
    public ObservableCollection<WeatherRecordRankItem> Top10ColdestDays { get; }
    public ObservableCollection<WeatherRecordRankItem> Top10HighestGusts { get; }
    public ObservableCollection<WeatherRecordRankItem> Top10RainiestDays { get; }
    public ObservableCollection<WeatherStreakRecord> Top3DryStreaks { get; }
    public ObservableCollection<WeatherStreakRecord> Top3WetStreaks { get; }

    public ICommand RefreshCommand { get; }
    public ICommand ToggleUnitsCommand { get; }
    public ICommand SelectTabCommand { get; }
    public ICommand SelectMetricCommand { get; }
    public ICommand SelectRecentDayCommand { get; }

    public StationMetadata? Station
    {
        get => _station;
        set => SetProperty(ref _station, value);
    }

    public OverviewReport? Overview
    {
        get => _overview;
        set => SetProperty(ref _overview, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string LoadingStatus
    {
        get => _loadingStatus;
        set => SetProperty(ref _loadingStatus, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public string DataLoadedTime
    {
        get => _dataLoadedTime;
        set => SetProperty(ref _dataLoadedTime, value);
    }

    public bool IsMetric
    {
        get => false;
        set { }
    }

    public string TemperatureUnit => IsMetric ? "°C" : "°F";
    public string PrecipUnit => IsMetric ? "mm" : "in";
    public string WindUnit => IsMetric ? "km/h" : "mph";

    public string SelectedTab
    {
        get => _selectedTab;
        set => SetProperty(ref _selectedTab, value);
    }

    public string SelectedMetric
    {
        get => _selectedMetric;
        set => SetProperty(ref _selectedMetric, value);
    }

    public RecentDayItem? SelectedRecentDay
    {
        get => _selectedRecentDay;
        set => SetProperty(ref _selectedRecentDay, value);
    }

    public string DailySearchText
    {
        get => _dailySearchText;
        set
        {
            if (SetProperty(ref _dailySearchText, value))
            {
                ApplyDailyFilter();
            }
        }
    }

    public int SelectedDailyYear
    {
        get => _selectedDailyYear;
        set
        {
            if (SetProperty(ref _selectedDailyYear, value))
            {
                OnPropertyChanged(nameof(SelectedDailyYearDisplay));
                ApplyDailyFilter();
            }
        }
    }

    public string SelectedDailyYearDisplay => _selectedDailyYear == 0 ? "All Years" : _selectedDailyYear.ToString();

    // Formatted Overview Strings
    public string HottestDayDisplay
    {
        get
        {
            var h = Overview?.AllTimeRecords?.HottestDay;
            if (h == null) return "--";
            double val = IsMetric ? h.TempC : h.TempF;
            return $"{val:F1}{TemperatureUnit} on {h.Date}";
        }
    }

    public string ColdestDayDisplay
    {
        get
        {
            var c = Overview?.AllTimeRecords?.ColdestDay;
            if (c == null) return "--";
            double val = IsMetric ? c.TempC : c.TempF;
            return $"{val:F1}{TemperatureUnit} on {c.Date}";
        }
    }

    public string RainiestDayDisplay
    {
        get
        {
            var r = Overview?.AllTimeRecords?.RainiestDay;
            if (r == null) return "--";
            double val = IsMetric ? r.PrecipMm : r.PrecipInches;
            return $"{val:F2} {PrecipUnit} on {r.Date}";
        }
    }

    public string WindiestDayDisplay
    {
        get
        {
            var w = Overview?.AllTimeRecords?.WindiestDay;
            if (w == null) return "--";
            double val = IsMetric ? w.GustMps * 3.6 : w.GustMph;
            return $"{val:F1} {WindUnit} on {w.Date}";
        }
    }

    public string TotalPrecipDisplay
    {
        get
        {
            var p = Overview?.AllTimeRecords?.TotalPrecipitation;
            if (p == null) return "--";
            double val = IsMetric ? p.TotalMm : p.TotalInches;
            return $"{val:F2} {PrecipUnit} ({p.RainyDaysCount} rainy days)";
        }
    }

    // 6-Month Compact Text Summaries (under 14-day charts)
    public string SixMonthAvgTempThisYearText { get => _sixMonthAvgTempThisYearText; private set => SetProperty(ref _sixMonthAvgTempThisYearText, value); }
    public string SixMonthAvgTempDeltaText { get => _sixMonthAvgTempDeltaText; private set => SetProperty(ref _sixMonthAvgTempDeltaText, value); }
    public SolidColorBrush SixMonthAvgTempDeltaBrush { get => _sixMonthAvgTempDeltaBrush; private set => SetProperty(ref _sixMonthAvgTempDeltaBrush, value); }
    public string SixMonthTempAvgMonthly { get => _sixMonthTempAvgMonthly; private set => SetProperty(ref _sixMonthTempAvgMonthly, value); }

    public string SixMonthAvgHighThisYearText { get => _sixMonthAvgHighThisYearText; private set => SetProperty(ref _sixMonthAvgHighThisYearText, value); }
    public string SixMonthAvgHighDeltaText { get => _sixMonthAvgHighDeltaText; private set => SetProperty(ref _sixMonthAvgHighDeltaText, value); }
    public SolidColorBrush SixMonthAvgHighDeltaBrush { get => _sixMonthAvgHighDeltaBrush; private set => SetProperty(ref _sixMonthAvgHighDeltaBrush, value); }
    public string SixMonthAvgLowThisYearText { get => _sixMonthAvgLowThisYearText; private set => SetProperty(ref _sixMonthAvgLowThisYearText, value); }
    public string SixMonthAvgLowDeltaText { get => _sixMonthAvgLowDeltaText; private set => SetProperty(ref _sixMonthAvgLowDeltaText, value); }
    public SolidColorBrush SixMonthAvgLowDeltaBrush { get => _sixMonthAvgLowDeltaBrush; private set => SetProperty(ref _sixMonthAvgLowDeltaBrush, value); }
    public string SixMonthTempRangeMonthly { get => _sixMonthTempRangeMonthly; private set => SetProperty(ref _sixMonthTempRangeMonthly, value); }

    public string SixMonthRainThisYearText { get => _sixMonthRainThisYearText; private set => SetProperty(ref _sixMonthRainThisYearText, value); }
    public string SixMonthRainDeltaText { get => _sixMonthRainDeltaText; private set => SetProperty(ref _sixMonthRainDeltaText, value); }
    public SolidColorBrush SixMonthRainDeltaBrush { get => _sixMonthRainDeltaBrush; private set => SetProperty(ref _sixMonthRainDeltaBrush, value); }
    public string SixMonthRainDaysText { get => _sixMonthRainDaysText; private set => SetProperty(ref _sixMonthRainDaysText, value); }
    public string SixMonthRainDaysDeltaText { get => _sixMonthRainDaysDeltaText; private set => SetProperty(ref _sixMonthRainDaysDeltaText, value); }
    public SolidColorBrush SixMonthRainDaysDeltaBrush { get => _sixMonthRainDaysDeltaBrush; private set => SetProperty(ref _sixMonthRainDaysDeltaBrush, value); }
    public string SixMonthRainMonthly { get => _sixMonthRainMonthly; private set => SetProperty(ref _sixMonthRainMonthly, value); }

    public string SixMonthWindThisYearText { get => _sixMonthWindThisYearText; private set => SetProperty(ref _sixMonthWindThisYearText, value); }
    public string SixMonthWindDeltaText { get => _sixMonthWindDeltaText; private set => SetProperty(ref _sixMonthWindDeltaText, value); }
    public SolidColorBrush SixMonthWindDeltaBrush { get => _sixMonthWindDeltaBrush; private set => SetProperty(ref _sixMonthWindDeltaBrush, value); }
    public string SixMonthWindPeakText { get => _sixMonthWindPeakText; private set => SetProperty(ref _sixMonthWindPeakText, value); }
    public string SixMonthWindPeakDeltaText { get => _sixMonthWindPeakDeltaText; private set => SetProperty(ref _sixMonthWindPeakDeltaText, value); }
    public SolidColorBrush SixMonthWindPeakDeltaBrush { get => _sixMonthWindPeakDeltaBrush; private set => SetProperty(ref _sixMonthWindPeakDeltaBrush, value); }
    public string SixMonthWindMonthly { get => _sixMonthWindMonthly; private set => SetProperty(ref _sixMonthWindMonthly, value); }

    public string SixMonthLightningThisYearText { get => _sixMonthLightningThisYearText; private set => SetProperty(ref _sixMonthLightningThisYearText, value); }
    public string SixMonthLightningDeltaText { get => _sixMonthLightningDeltaText; private set => SetProperty(ref _sixMonthLightningDeltaText, value); }
    public SolidColorBrush SixMonthLightningDeltaBrush { get => _sixMonthLightningDeltaBrush; private set => SetProperty(ref _sixMonthLightningDeltaBrush, value); }
    public string SixMonthLightningDaysText { get => _sixMonthLightningDaysText; private set => SetProperty(ref _sixMonthLightningDaysText, value); }
    public string SixMonthLightningDaysDeltaText { get => _sixMonthLightningDaysDeltaText; private set => SetProperty(ref _sixMonthLightningDaysDeltaText, value); }
    public SolidColorBrush SixMonthLightningDaysDeltaBrush { get => _sixMonthLightningDaysDeltaBrush; private set => SetProperty(ref _sixMonthLightningDaysDeltaBrush, value); }
    public string SixMonthLightningMonthly { get => _sixMonthLightningMonthly; private set => SetProperty(ref _sixMonthLightningMonthly, value); }

    // 6-Month Tab Dedicated Summaries
    public string SixMonthTabAvgTempThisYearText { get => _sixMonthTabAvgTempThisYearText; private set => SetProperty(ref _sixMonthTabAvgTempThisYearText, value); }
    public string SixMonthTabAvgTempDeltaText { get => _sixMonthTabAvgTempDeltaText; private set => SetProperty(ref _sixMonthTabAvgTempDeltaText, value); }
    public SolidColorBrush SixMonthTabAvgTempDeltaBrush { get => _sixMonthTabAvgTempDeltaBrush; private set => SetProperty(ref _sixMonthTabAvgTempDeltaBrush, value); }

    public string SixMonthTabAvgHighThisYearText { get => _sixMonthTabAvgHighThisYearText; private set => SetProperty(ref _sixMonthTabAvgHighThisYearText, value); }
    public string SixMonthTabAvgHighDeltaText { get => _sixMonthTabAvgHighDeltaText; private set => SetProperty(ref _sixMonthTabAvgHighDeltaText, value); }
    public SolidColorBrush SixMonthTabAvgHighDeltaBrush { get => _sixMonthTabAvgHighDeltaBrush; private set => SetProperty(ref _sixMonthTabAvgHighDeltaBrush, value); }
    public string SixMonthTabAvgLowThisYearText { get => _sixMonthTabAvgLowThisYearText; private set => SetProperty(ref _sixMonthTabAvgLowThisYearText, value); }
    public string SixMonthTabAvgLowDeltaText { get => _sixMonthTabAvgLowDeltaText; private set => SetProperty(ref _sixMonthTabAvgLowDeltaText, value); }
    public SolidColorBrush SixMonthTabAvgLowDeltaBrush { get => _sixMonthTabAvgLowDeltaBrush; private set => SetProperty(ref _sixMonthTabAvgLowDeltaBrush, value); }

    public string SixMonthTabRainThisYearText { get => _sixMonthTabRainThisYearText; private set => SetProperty(ref _sixMonthTabRainThisYearText, value); }
    public string SixMonthTabRainDeltaText { get => _sixMonthTabRainDeltaText; private set => SetProperty(ref _sixMonthTabRainDeltaText, value); }
    public SolidColorBrush SixMonthTabRainDeltaBrush { get => _sixMonthTabRainDeltaBrush; private set => SetProperty(ref _sixMonthTabRainDeltaBrush, value); }
    public string SixMonthTabRainDaysText { get => _sixMonthTabRainDaysText; private set => SetProperty(ref _sixMonthTabRainDaysText, value); }
    public string SixMonthTabRainDaysDeltaText { get => _sixMonthTabRainDaysDeltaText; private set => SetProperty(ref _sixMonthTabRainDaysDeltaText, value); }
    public SolidColorBrush SixMonthTabRainDaysDeltaBrush { get => _sixMonthTabRainDaysDeltaBrush; private set => SetProperty(ref _sixMonthTabRainDaysDeltaBrush, value); }

    public string SixMonthTabWindThisYearText { get => _sixMonthTabWindThisYearText; private set => SetProperty(ref _sixMonthTabWindThisYearText, value); }
    public string SixMonthTabWindDeltaText { get => _sixMonthTabWindDeltaText; private set => SetProperty(ref _sixMonthTabWindDeltaText, value); }
    public SolidColorBrush SixMonthTabWindDeltaBrush { get => _sixMonthTabWindDeltaBrush; private set => SetProperty(ref _sixMonthTabWindDeltaBrush, value); }
    public string SixMonthTabWindPeakText { get => _sixMonthTabWindPeakText; private set => SetProperty(ref _sixMonthTabWindPeakText, value); }
    public string SixMonthTabWindPeakDeltaText { get => _sixMonthTabWindPeakDeltaText; private set => SetProperty(ref _sixMonthTabWindPeakDeltaText, value); }
    public SolidColorBrush SixMonthTabWindPeakDeltaBrush { get => _sixMonthTabWindPeakDeltaBrush; private set => SetProperty(ref _sixMonthTabWindPeakDeltaBrush, value); }

    public string SixMonthTabLightningThisYearText { get => _sixMonthTabLightningThisYearText; private set => SetProperty(ref _sixMonthTabLightningThisYearText, value); }
    public string SixMonthTabLightningDeltaText { get => _sixMonthTabLightningDeltaText; private set => SetProperty(ref _sixMonthTabLightningDeltaText, value); }
    public SolidColorBrush SixMonthTabLightningDeltaBrush { get => _sixMonthTabLightningDeltaBrush; private set => SetProperty(ref _sixMonthTabLightningDeltaBrush, value); }
    public string SixMonthTabLightningDaysText { get => _sixMonthTabLightningDaysText; private set => SetProperty(ref _sixMonthTabLightningDaysText, value); }
    public string SixMonthTabLightningDaysDeltaText { get => _sixMonthTabLightningDaysDeltaText; private set => SetProperty(ref _sixMonthTabLightningDaysDeltaText, value); }
    public SolidColorBrush SixMonthTabLightningDaysDeltaBrush { get => _sixMonthTabLightningDaysDeltaBrush; private set => SetProperty(ref _sixMonthTabLightningDaysDeltaBrush, value); }

    // Executive Overview Glance Blocks (14-Day)
    public string Glance14DayRainDaysText { get => _glance14DayRainDaysText; private set => SetProperty(ref _glance14DayRainDaysText, value); }
    public SolidColorBrush Glance14DayRainDaysBrush { get => _glance14DayRainDaysBrush; private set => SetProperty(ref _glance14DayRainDaysBrush, value); }
    public string Glance14DayRainAmountText { get => _glance14DayRainAmountText; private set => SetProperty(ref _glance14DayRainAmountText, value); }
    public SolidColorBrush Glance14DayRainAmountBrush { get => _glance14DayRainAmountBrush; private set => SetProperty(ref _glance14DayRainAmountBrush, value); }

    public string Glance14DayTempMaxText { get => _glance14DayTempMaxText; private set => SetProperty(ref _glance14DayTempMaxText, value); }
    public SolidColorBrush Glance14DayTempMaxBrush { get => _glance14DayTempMaxBrush; private set => SetProperty(ref _glance14DayTempMaxBrush, value); }
    public string Glance14DayTempMinText { get => _glance14DayTempMinText; private set => SetProperty(ref _glance14DayTempMinText, value); }
    public SolidColorBrush Glance14DayTempMinBrush { get => _glance14DayTempMinBrush; private set => SetProperty(ref _glance14DayTempMinBrush, value); }
    public string Glance14DayTempAvgText { get => _glance14DayTempAvgText; private set => SetProperty(ref _glance14DayTempAvgText, value); }
    public SolidColorBrush Glance14DayTempAvgBrush { get => _glance14DayTempAvgBrush; private set => SetProperty(ref _glance14DayTempAvgBrush, value); }

    public string Glance14DayWindAvgText { get => _glance14DayWindAvgText; private set => SetProperty(ref _glance14DayWindAvgText, value); }
    public SolidColorBrush Glance14DayWindAvgBrush { get => _glance14DayWindAvgBrush; private set => SetProperty(ref _glance14DayWindAvgBrush, value); }
    public string Glance14DayWindPeakText { get => _glance14DayWindPeakText; private set => SetProperty(ref _glance14DayWindPeakText, value); }

    public string Glance14DayLightningStrikesText { get => _glance14DayLightningStrikesText; private set => SetProperty(ref _glance14DayLightningStrikesText, value); }
    public SolidColorBrush Glance14DayLightningStrikesBrush { get => _glance14DayLightningStrikesBrush; private set => SetProperty(ref _glance14DayLightningStrikesBrush, value); }
    public string Glance14DayLightningDaysText { get => _glance14DayLightningDaysText; private set => SetProperty(ref _glance14DayLightningDaysText, value); }
    public SolidColorBrush Glance14DayLightningDaysBrush { get => _glance14DayLightningDaysBrush; private set => SetProperty(ref _glance14DayLightningDaysBrush, value); }

    // Executive Overview Glance Blocks (6-Month)
    public string Glance6MonthRainDaysText { get => _glance6MonthRainDaysText; private set => SetProperty(ref _glance6MonthRainDaysText, value); }
    public SolidColorBrush Glance6MonthRainDaysBrush { get => _glance6MonthRainDaysBrush; private set => SetProperty(ref _glance6MonthRainDaysBrush, value); }
    public string Glance6MonthRainAmountText { get => _glance6MonthRainAmountText; private set => SetProperty(ref _glance6MonthRainAmountText, value); }
    public SolidColorBrush Glance6MonthRainAmountBrush { get => _glance6MonthRainAmountBrush; private set => SetProperty(ref _glance6MonthRainAmountBrush, value); }

    public string Glance6MonthTempMaxText { get => _glance6MonthTempMaxText; private set => SetProperty(ref _glance6MonthTempMaxText, value); }
    public SolidColorBrush Glance6MonthTempMaxBrush { get => _glance6MonthTempMaxBrush; private set => SetProperty(ref _glance6MonthTempMaxBrush, value); }
    public string Glance6MonthTempMinText { get => _glance6MonthTempMinText; private set => SetProperty(ref _glance6MonthTempMinText, value); }
    public SolidColorBrush Glance6MonthTempMinBrush { get => _glance6MonthTempMinBrush; private set => SetProperty(ref _glance6MonthTempMinBrush, value); }
    public string Glance6MonthTempAvgText { get => _glance6MonthTempAvgText; private set => SetProperty(ref _glance6MonthTempAvgText, value); }
    public SolidColorBrush Glance6MonthTempAvgBrush { get => _glance6MonthTempAvgBrush; private set => SetProperty(ref _glance6MonthTempAvgBrush, value); }

    public string Glance6MonthWindAvgText { get => _glance6MonthWindAvgText; private set => SetProperty(ref _glance6MonthWindAvgText, value); }
    public SolidColorBrush Glance6MonthWindAvgBrush { get => _glance6MonthWindAvgBrush; private set => SetProperty(ref _glance6MonthWindAvgBrush, value); }
    public string Glance6MonthWindPeakText { get => _glance6MonthWindPeakText; private set => SetProperty(ref _glance6MonthWindPeakText, value); }

    public string Glance6MonthLightningStrikesText { get => _glance6MonthLightningStrikesText; private set => SetProperty(ref _glance6MonthLightningStrikesText, value); }
    public SolidColorBrush Glance6MonthLightningStrikesBrush { get => _glance6MonthLightningStrikesBrush; private set => SetProperty(ref _glance6MonthLightningStrikesBrush, value); }
    public string Glance6MonthLightningDaysText { get => _glance6MonthLightningDaysText; private set => SetProperty(ref _glance6MonthLightningDaysText, value); }
    public SolidColorBrush Glance6MonthLightningDaysBrush { get => _glance6MonthLightningDaysBrush; private set => SetProperty(ref _glance6MonthLightningDaysBrush, value); }

    // Header Properties
    public string LastDataDateText { get => _lastDataDateText; private set => SetProperty(ref _lastDataDateText, value); }
    public string ArchiveDaysText { get => _archiveDaysText; private set => SetProperty(ref _archiveDaysText, value); }

    // Executive Overview Glance Blocks (12-Month)
    public string Glance12MonthRainDaysText { get => _glance12MonthRainDaysText; private set => SetProperty(ref _glance12MonthRainDaysText, value); }
    public SolidColorBrush Glance12MonthRainDaysBrush { get => _glance12MonthRainDaysBrush; private set => SetProperty(ref _glance12MonthRainDaysBrush, value); }
    public string Glance12MonthRainAmountText { get => _glance12MonthRainAmountText; private set => SetProperty(ref _glance12MonthRainAmountText, value); }
    public SolidColorBrush Glance12MonthRainAmountBrush { get => _glance12MonthRainAmountBrush; private set => SetProperty(ref _glance12MonthRainAmountBrush, value); }

    public string Glance12MonthTempMaxText { get => _glance12MonthTempMaxText; private set => SetProperty(ref _glance12MonthTempMaxText, value); }
    public SolidColorBrush Glance12MonthTempMaxBrush { get => _glance12MonthTempMaxBrush; private set => SetProperty(ref _glance12MonthTempMaxBrush, value); }
    public string Glance12MonthTempMinText { get => _glance12MonthTempMinText; private set => SetProperty(ref _glance12MonthTempMinText, value); }
    public SolidColorBrush Glance12MonthTempMinBrush { get => _glance12MonthTempMinBrush; private set => SetProperty(ref _glance12MonthTempMinBrush, value); }
    public string Glance12MonthTempAvgText { get => _glance12MonthTempAvgText; private set => SetProperty(ref _glance12MonthTempAvgText, value); }
    public SolidColorBrush Glance12MonthTempAvgBrush { get => _glance12MonthTempAvgBrush; private set => SetProperty(ref _glance12MonthTempAvgBrush, value); }

    public string Glance12MonthWindAvgText { get => _glance12MonthWindAvgText; private set => SetProperty(ref _glance12MonthWindAvgText, value); }
    public SolidColorBrush Glance12MonthWindAvgBrush { get => _glance12MonthWindAvgBrush; private set => SetProperty(ref _glance12MonthWindAvgBrush, value); }
    public string Glance12MonthWindPeakText { get => _glance12MonthWindPeakText; private set => SetProperty(ref _glance12MonthWindPeakText, value); }

    public string Glance12MonthLightningStrikesText { get => _glance12MonthLightningStrikesText; private set => SetProperty(ref _glance12MonthLightningStrikesText, value); }
    public SolidColorBrush Glance12MonthLightningStrikesBrush { get => _glance12MonthLightningStrikesBrush; private set => SetProperty(ref _glance12MonthLightningStrikesBrush, value); }
    public string Glance12MonthLightningDaysText { get => _glance12MonthLightningDaysText; private set => SetProperty(ref _glance12MonthLightningDaysText, value); }
    public SolidColorBrush Glance12MonthLightningDaysBrush { get => _glance12MonthLightningDaysBrush; private set => SetProperty(ref _glance12MonthLightningDaysBrush, value); }

    // 12-Month Tab Dedicated Summaries
    public string TwelveMonthTabAvgTempThisYearText { get => _twelveMonthTabAvgTempThisYearText; private set => SetProperty(ref _twelveMonthTabAvgTempThisYearText, value); }
    public string TwelveMonthTabAvgTempDeltaText { get => _twelveMonthTabAvgTempDeltaText; private set => SetProperty(ref _twelveMonthTabAvgTempDeltaText, value); }
    public SolidColorBrush TwelveMonthTabAvgTempDeltaBrush { get => _twelveMonthTabAvgTempDeltaBrush; private set => SetProperty(ref _twelveMonthTabAvgTempDeltaBrush, value); }
    public string TwelveMonthTempAvgMonthly { get => _twelveMonthTempAvgMonthly; private set => SetProperty(ref _twelveMonthTempAvgMonthly, value); }

    public string TwelveMonthTabAvgHighThisYearText { get => _twelveMonthTabAvgHighThisYearText; private set => SetProperty(ref _twelveMonthTabAvgHighThisYearText, value); }
    public string TwelveMonthTabAvgHighDeltaText { get => _twelveMonthTabAvgHighDeltaText; private set => SetProperty(ref _twelveMonthTabAvgHighDeltaText, value); }
    public SolidColorBrush TwelveMonthTabAvgHighDeltaBrush { get => _twelveMonthTabAvgHighDeltaBrush; private set => SetProperty(ref _twelveMonthTabAvgHighDeltaBrush, value); }
    public string TwelveMonthTabAvgLowThisYearText { get => _twelveMonthTabAvgLowThisYearText; private set => SetProperty(ref _twelveMonthTabAvgLowThisYearText, value); }
    public string TwelveMonthTabAvgLowDeltaText { get => _twelveMonthTabAvgLowDeltaText; private set => SetProperty(ref _twelveMonthTabAvgLowDeltaText, value); }
    public SolidColorBrush TwelveMonthTabAvgLowDeltaBrush { get => _twelveMonthTabAvgLowDeltaBrush; private set => SetProperty(ref _twelveMonthTabAvgLowDeltaBrush, value); }
    public string TwelveMonthTempRangeMonthly { get => _twelveMonthTempRangeMonthly; private set => SetProperty(ref _twelveMonthTempRangeMonthly, value); }

    public string TwelveMonthTabWindThisYearText { get => _twelveMonthTabWindThisYearText; private set => SetProperty(ref _twelveMonthTabWindThisYearText, value); }
    public string TwelveMonthTabWindDeltaText { get => _twelveMonthTabWindDeltaText; private set => SetProperty(ref _twelveMonthTabWindDeltaText, value); }
    public SolidColorBrush TwelveMonthTabWindDeltaBrush { get => _twelveMonthTabWindDeltaBrush; private set => SetProperty(ref _twelveMonthTabWindDeltaBrush, value); }
    public string TwelveMonthTabWindPeakText { get => _twelveMonthTabWindPeakText; private set => SetProperty(ref _twelveMonthTabWindPeakText, value); }
    public string TwelveMonthTabWindPeakDeltaText { get => _twelveMonthTabWindPeakDeltaText; private set => SetProperty(ref _twelveMonthTabWindPeakDeltaText, value); }
    public SolidColorBrush TwelveMonthTabWindPeakDeltaBrush { get => _twelveMonthTabWindPeakDeltaBrush; private set => SetProperty(ref _twelveMonthTabWindPeakDeltaBrush, value); }
    public string TwelveMonthWindMonthly { get => _twelveMonthWindMonthly; private set => SetProperty(ref _twelveMonthWindMonthly, value); }

    public string TwelveMonthTabRainThisYearText { get => _twelveMonthTabRainThisYearText; private set => SetProperty(ref _twelveMonthTabRainThisYearText, value); }
    public string TwelveMonthTabRainDeltaText { get => _twelveMonthTabRainDeltaText; private set => SetProperty(ref _twelveMonthTabRainDeltaText, value); }
    public SolidColorBrush TwelveMonthTabRainDeltaBrush { get => _twelveMonthTabRainDeltaBrush; private set => SetProperty(ref _twelveMonthTabRainDeltaBrush, value); }
    public string TwelveMonthTabRainDaysText { get => _twelveMonthTabRainDaysText; private set => SetProperty(ref _twelveMonthTabRainDaysText, value); }
    public string TwelveMonthTabRainDaysDeltaText { get => _twelveMonthTabRainDaysDeltaText; private set => SetProperty(ref _twelveMonthTabRainDaysDeltaText, value); }
    public SolidColorBrush TwelveMonthTabRainDaysDeltaBrush { get => _twelveMonthTabRainDaysDeltaBrush; private set => SetProperty(ref _twelveMonthTabRainDaysDeltaBrush, value); }
    public string TwelveMonthRainMonthly { get => _twelveMonthRainMonthly; private set => SetProperty(ref _twelveMonthRainMonthly, value); }

    public string TwelveMonthTabLightningThisYearText { get => _twelveMonthTabLightningThisYearText; private set => SetProperty(ref _twelveMonthTabLightningThisYearText, value); }
    public string TwelveMonthTabLightningDeltaText { get => _twelveMonthTabLightningDeltaText; private set => SetProperty(ref _twelveMonthTabLightningDeltaText, value); }
    public SolidColorBrush TwelveMonthTabLightningDeltaBrush { get => _twelveMonthTabLightningDeltaBrush; private set => SetProperty(ref _twelveMonthTabLightningDeltaBrush, value); }
    public string TwelveMonthTabLightningDaysText { get => _twelveMonthTabLightningDaysText; private set => SetProperty(ref _twelveMonthTabLightningDaysText, value); }
    public string TwelveMonthTabLightningDaysDeltaText { get => _twelveMonthTabLightningDaysDeltaText; private set => SetProperty(ref _twelveMonthTabLightningDaysDeltaText, value); }
    public SolidColorBrush TwelveMonthTabLightningDaysDeltaBrush { get => _twelveMonthTabLightningDaysDeltaBrush; private set => SetProperty(ref _twelveMonthTabLightningDaysDeltaBrush, value); }
    public string TwelveMonthLightningMonthly { get => _twelveMonthLightningMonthly; private set => SetProperty(ref _twelveMonthLightningMonthly, value); }

    // Executive Overview Solar Glance
    public string Glance14DaySolarAvgText { get => _glance14DaySolarAvgText; private set => SetProperty(ref _glance14DaySolarAvgText, value); }
    public SolidColorBrush Glance14DaySolarAvgBrush { get => _glance14DaySolarAvgBrush; private set => SetProperty(ref _glance14DaySolarAvgBrush, value); }
    public string Glance6MonthSolarAvgText { get => _glance6MonthSolarAvgText; private set => SetProperty(ref _glance6MonthSolarAvgText, value); }
    public SolidColorBrush Glance6MonthSolarAvgBrush { get => _glance6MonthSolarAvgBrush; private set => SetProperty(ref _glance6MonthSolarAvgBrush, value); }
    public string Glance12MonthSolarAvgText { get => _glance12MonthSolarAvgText; private set => SetProperty(ref _glance12MonthSolarAvgText, value); }
    public SolidColorBrush Glance12MonthSolarAvgBrush { get => _glance12MonthSolarAvgBrush; private set => SetProperty(ref _glance12MonthSolarAvgBrush, value); }

    // Dedicated Solar Tab Summaries
    public string FourteenDayTabSolarThisYearText { get => _fourteenDayTabSolarThisYearText; private set => SetProperty(ref _fourteenDayTabSolarThisYearText, value); }
    public string FourteenDayTabSolarDeltaText { get => _fourteenDayTabSolarDeltaText; private set => SetProperty(ref _fourteenDayTabSolarDeltaText, value); }
    public SolidColorBrush FourteenDayTabSolarDeltaBrush { get => _fourteenDayTabSolarDeltaBrush; private set => SetProperty(ref _fourteenDayTabSolarDeltaBrush, value); }

    public string SixMonthTabSolarThisYearText { get => _sixMonthTabSolarThisYearText; private set => SetProperty(ref _sixMonthTabSolarThisYearText, value); }
    public string SixMonthTabSolarDeltaText { get => _sixMonthTabSolarDeltaText; private set => SetProperty(ref _sixMonthTabSolarDeltaText, value); }
    public SolidColorBrush SixMonthTabSolarDeltaBrush { get => _sixMonthTabSolarDeltaBrush; private set => SetProperty(ref _sixMonthTabSolarDeltaBrush, value); }
    public string SixMonthSolarMonthly { get => _sixMonthSolarMonthly; private set => SetProperty(ref _sixMonthSolarMonthly, value); }

    public string TwelveMonthSolarMonthly { get => _twelveMonthSolarMonthly; private set => SetProperty(ref _twelveMonthSolarMonthly, value); }

    public async Task LoadDataAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        LoadingStatus = "Loading historic weather data...";

        try
        {
            // 1. Fetch Overview
            LoadingStatus = "Fetching station overview...";
            var overview = await _weatherService.GetOverviewAsync();
            if (overview != null)
            {
                Overview = overview;
                if (overview.Station != null) Station = overview.Station;
            }

            // 2. Fetch Monthly & Multi-Year Comparisons
            LoadingStatus = "Fetching multi-year monthly comparisons...";
            var comparisons = await _weatherService.GetMonthlyYearlyComparisonsAsync();

            // 3. Fetch Recent Days & Upcoming Week in parallel
            LoadingStatus = "Fetching recent days & next week forecast...";
            var recentTask = _weatherService.GetRecentDaysAsync();
            var upcomingTask = _weatherService.GetUpcomingWeekAsync();
            var dailyTask = _weatherService.GetAllDailySummariesAsync();

            await Task.WhenAll(recentTask, upcomingTask, dailyTask);

            var recent = await recentTask;
            var upcoming = await upcomingTask;
            var daily = await dailyTask;

            // Enrich monthly & annual comparisons with daily summaries (lightning strikes, solar radiation)
            if (daily?.DailySummaries != null && daily.DailySummaries.Count > 0)
            {
                LoadingStatus = "Analyzing lightning & solar irradiance history...";
                WeatherReportService.EnrichWithDailySummaries(comparisons, daily.DailySummaries);
            }

            // Update UI Collections
            if (comparisons != null)
            {
                AnnualSummaries.Clear();
                foreach (var a in comparisons.AnnualSummaries.OrderByDescending(x => x.Year))
                    AnnualSummaries.Add(a);

                MonthlyMatrix.Clear();
                foreach (var kvp in comparisons.MonthByMonthMultiYearComparison.OrderBy(k => k.Value.MonthNumber))
                    MonthlyMatrix.Add(kvp.Value);

                if (Station == null && comparisons.Station != null)
                    Station = comparisons.Station;
            }

            if (recent != null)
            {
                RecentDays.Clear();
                foreach (var r in recent.RecentDays.OrderByDescending(x => x.Date))
                    RecentDays.Add(r);

                SelectedRecentDay = RecentDays.FirstOrDefault();
            }

            if (upcoming != null)
            {
                UpcomingWeek.Clear();
                foreach (var u in upcoming.UpcomingWeek.OrderBy(x => x.DayOffset))
                    UpcomingWeek.Add(u);
            }

            if (daily?.DailySummaries != null)
            {
                DailyRecords.Clear();
                var yrs = new HashSet<int>();
                foreach (var d in daily.DailySummaries.OrderByDescending(x => x.Date))
                {
                    DailyRecords.Add(d);
                    if (DateTime.TryParse(d.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    {
                        yrs.Add(dt.Year);
                    }
                }

                AvailableYears.Clear();
                AvailableYears.Add(0); // All
                foreach (var y in yrs.OrderByDescending(x => x))
                    AvailableYears.Add(y);

                ApplyDailyFilter();
            }

            // Build 14-Day Timeline (-14 to +14), 6-Month Timeline (-5 to +6), and 12-Month Timeline (Jan to Dec)
            BuildDaysTimeline(daily?.DailySummaries);
            BuildMonthsTimeline(comparisons, daily?.DailySummaries);
            BuildSixMonthSummaries(daily?.DailySummaries);
            BuildTwelveMonthSummaries(daily?.DailySummaries);
            BuildFullYearRainDaysTimeline(daily?.DailySummaries);
            BuildRecordsRankings(daily?.DailySummaries);

            // Header date info
            string lastDateStr = "";
            if (Overview?.TimeRange?.EndDate != null && DateTime.TryParse(Overview.TimeRange.EndDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDt))
            {
                lastDateStr = endDt.ToString("MMM d, yyyy");
            }
            else if (DailyRecords.Count > 0 && DateTime.TryParse(DailyRecords.First().Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var lastRecDt))
            {
                lastDateStr = lastRecDt.ToString("MMM d, yyyy");
            }
            else if (!string.IsNullOrEmpty(Overview?.TimeRange?.EndDate))
            {
                lastDateStr = Overview.TimeRange.EndDate;
            }
            LastDataDateText = !string.IsNullOrEmpty(lastDateStr) ? lastDateStr : "--";

            int totalDays = Overview?.TimeRange?.TotalDaysRecorded ?? DailyRecords.Count;
            ArchiveDaysText = totalDays > 0 ? totalDays.ToString("N0") : "--";

            DataLoadedTime = DateTime.Now.ToString("h:mm:ss tt");
            LoadingStatus = "Data up to date";
            OnPropertyChanged(nameof(HottestDayDisplay));
            OnPropertyChanged(nameof(ColdestDayDisplay));
            OnPropertyChanged(nameof(RainiestDayDisplay));
            OnPropertyChanged(nameof(WindiestDayDisplay));
            OnPropertyChanged(nameof(TotalPrecipDisplay));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading data: {ex.Message}";
            LoadingStatus = "Failed to load data";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void BuildDaysTimeline(List<DailyWeatherRecord>? dailySummaries)
    {
        DaysTimeline.Clear();
        if (dailySummaries == null || dailySummaries.Count == 0) return;

        DateTime refDate = DateTime.Today;
        if (Overview?.TimeRange?.AsOfLocal != null && DateTime.TryParse(Overview.TimeRange.AsOfLocal, CultureInfo.InvariantCulture, DateTimeStyles.None, out var asOfDt))
        {
            refDate = asOfDt;
        }
        else if (DailyRecords.Count > 0 && DateTime.TryParse(DailyRecords.First().Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var lastDt))
        {
            refDate = lastDt.AddDays(1);
        }

        var dailyMap = dailySummaries.ToDictionary(d => d.Date, StringComparer.OrdinalIgnoreCase);
        int[] prevYears = [2025, 2024, 2023];

        for (int offset = -14; offset <= 14; offset++)
        {
            var targetDt = refDate.AddDays(offset);
            int m = targetDt.Month;
            int d = targetDt.Day;

            var point = new DayTimelinePoint
            {
                DayOffset = offset,
                CalendarDay = $"{m:D2}-{d:D2}",
                DisplayDate = targetDt.ToString("MMM d", CultureInfo.InvariantCulture),
                DayOfWeek = targetDt.ToString("ddd", CultureInfo.InvariantCulture),
                IsUpcoming = offset > 0,
                IsToday = offset == 0
            };

            // This Year (2026)
            int thisYear = refDate.Year;
            string thisKey = $"{thisYear:D4}-{m:D2}-{d:D2}";
            if (offset <= 0 && dailyMap.TryGetValue(thisKey, out var thisRecord))
            {
                point.ThisYearMaxTempF = thisRecord.TempHighF;
                point.ThisYearMinTempF = thisRecord.TempLowF;
                point.ThisYearAvgTempF = thisRecord.TempAvgF;
                point.ThisYearMaxTempC = thisRecord.TempHighC;
                point.ThisYearMinTempC = thisRecord.TempLowC;
                point.ThisYearAvgTempC = thisRecord.TempAvgC;
                point.ThisYearRainIn = thisRecord.PrecipInches;
                point.ThisYearRainMm = thisRecord.PrecipMm;
                point.ThisYearGustMph = thisRecord.WindGustMph;
                point.ThisYearGustKmh = thisRecord.WindGustMph.HasValue ? thisRecord.WindGustMph.Value * 1.60934 : null;
                point.ThisYearLightningStrikes = thisRecord.LightningStrikes;
                point.ThisYearLightningMinDistanceKm = thisRecord.LightningMinDistanceKm;
                point.ThisYearSolarRadiationAvg = thisRecord.SolarRadiationAvg;
                point.ThisYearUvAvg = thisRecord.UvAvg;
            }

            // Previous Years (2025, 2024, 2023)
            foreach (var yr in prevYears)
            {
                string yrKey = $"{yr:D4}-{m:D2}-{d:D2}";
                if (dailyMap.TryGetValue(yrKey, out var yrRecord))
                {
                    point.PrevYears.Add(new YearPointData
                    {
                        Year = yr,
                        MaxTempF = yrRecord.TempHighF,
                        MinTempF = yrRecord.TempLowF,
                        AvgTempF = yrRecord.TempAvgF,
                        MaxTempC = yrRecord.TempHighC,
                        MinTempC = yrRecord.TempLowC,
                        AvgTempC = yrRecord.TempAvgC,
                        RainIn = yrRecord.PrecipInches,
                        RainMm = yrRecord.PrecipMm,
                        GustMph = yrRecord.WindGustMph,
                        GustKmh = yrRecord.WindGustMph.HasValue ? yrRecord.WindGustMph.Value * 1.60934 : null,
                        LightningStrikes = yrRecord.LightningStrikes,
                        LightningMinDistanceKm = yrRecord.LightningMinDistanceKm,
                        SolarRadiationAvg = yrRecord.SolarRadiationAvg,
                        UvAvg = yrRecord.UvAvg
                    });
                }
            }

            DaysTimeline.Add(point);
        }
    }

    private void BuildMonthsTimeline(MonthlyYearlyComparisonsReport? comparisons, List<DailyWeatherRecord>? dailySummaries)
    {
        MonthsTimeline.Clear();
        DateTime refDate = DateTime.Today;
        if (Overview?.TimeRange?.AsOfLocal != null && DateTime.TryParse(Overview.TimeRange.AsOfLocal, CultureInfo.InvariantCulture, DateTimeStyles.None, out var asOfDt))
        {
            refDate = asOfDt;
        }

        int currentMonth = refDate.Month;
        int currentYear = refDate.Year;
        int[] prevYears = [2025, 2024, 2023];

        for (int offset = -5; offset <= 6; offset++)
        {
            int targetMonth = ((currentMonth - 1 + offset) % 12 + 12) % 12 + 1;
            string monthKey = targetMonth.ToString("D2");
            string monthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(targetMonth);
            string shortName = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(targetMonth);

            var point = new MonthTimelinePoint
            {
                MonthOffset = offset,
                MonthNumber = targetMonth,
                MonthName = monthName,
                DisplayLabel = shortName,
                IsUpcoming = offset > 0,
                IsCurrent = offset == 0
            };

            if (comparisons != null && comparisons.MonthByMonthMultiYearComparison.TryGetValue(monthKey, out var matrixItem))
            {
                // This year (for offset <= 0)
                if (offset <= 0)
                {
                    var thisY = matrixItem.Years.FirstOrDefault(y => y.Year == currentYear);
                    if (thisY != null)
                    {
                        point.ThisYearMaxTempF = thisY.MaxTempF;
                        point.ThisYearMinTempF = thisY.MinTempF;
                        point.ThisYearAvgTempF = thisY.AvgTempF;
                        point.ThisYearMaxTempC = thisY.MaxTempF.HasValue ? (thisY.MaxTempF.Value - 32) * 5 / 9 : null;
                        point.ThisYearMinTempC = thisY.MinTempF.HasValue ? (thisY.MinTempF.Value - 32) * 5 / 9 : null;
                        point.ThisYearAvgTempC = thisY.AvgTempC;
                        point.ThisYearRainIn = thisY.TotalPrecipInches;
                        point.ThisYearRainMm = thisY.TotalPrecipMm;
                        point.ThisYearGustMph = thisY.MaxGustMph;
                        point.ThisYearGustKmh = thisY.MaxGustMph.HasValue ? thisY.MaxGustMph.Value * 1.60934 : null;
                        point.ThisYearLightningStrikes = thisY.LightningStrikes;
                        point.ThisYearLightningMinDistanceKm = thisY.LightningMinDistanceKm;
                        point.ThisYearSolarRadiationAvg = thisY.AvgSolarRadiation;
                        point.ThisYearUvAvg = thisY.AvgUvIndex;
                    }
                }

                // Previous years
                foreach (var yr in prevYears)
                {
                    var yData = matrixItem.Years.FirstOrDefault(y => y.Year == yr);
                    if (yData != null)
                    {
                        point.PrevYears.Add(new YearPointData
                        {
                            Year = yr,
                            MaxTempF = yData.MaxTempF,
                            MinTempF = yData.MinTempF,
                            AvgTempF = yData.AvgTempF,
                            MaxTempC = yData.MaxTempF.HasValue ? (yData.MaxTempF.Value - 32) * 5 / 9 : null,
                            MinTempC = yData.MinTempF.HasValue ? (yData.MinTempF.Value - 32) * 5 / 9 : null,
                            AvgTempC = yData.AvgTempC,
                            RainIn = yData.TotalPrecipInches,
                            RainMm = yData.TotalPrecipMm,
                            GustMph = yData.MaxGustMph,
                            GustKmh = yData.MaxGustMph.HasValue ? yData.MaxGustMph.Value * 1.60934 : null,
                            LightningStrikes = yData.LightningStrikes,
                            LightningMinDistanceKm = yData.LightningMinDistanceKm,
                            SolarRadiationAvg = yData.AvgSolarRadiation,
                            UvAvg = yData.AvgUvIndex
                        });
                    }
                }
            }

            MonthsTimeline.Add(point);
        }

        // 12 Months in Calendar Order (Jan through Dec)
        Months12Timeline.Clear();
        for (int m = 1; m <= 12; m++)
        {
            string monthKey = m.ToString("D2");
            string monthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(m);
            string shortName = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(m);
            bool isUpcoming = m > currentMonth;
            bool isCurrent = m == currentMonth;

            var point = new MonthTimelinePoint
            {
                MonthOffset = m - currentMonth,
                MonthNumber = m,
                MonthName = monthName,
                DisplayLabel = shortName,
                IsUpcoming = isUpcoming,
                IsCurrent = isCurrent
            };

            if (comparisons != null && comparisons.MonthByMonthMultiYearComparison.TryGetValue(monthKey, out var matrixItem))
            {
                if (m <= currentMonth)
                {
                    var thisY = matrixItem.Years.FirstOrDefault(y => y.Year == currentYear);
                    if (thisY != null)
                    {
                        point.ThisYearMaxTempF = thisY.MaxTempF;
                        point.ThisYearMinTempF = thisY.MinTempF;
                        point.ThisYearAvgTempF = thisY.AvgTempF;
                        point.ThisYearMaxTempC = thisY.MaxTempF.HasValue ? (thisY.MaxTempF.Value - 32) * 5 / 9 : null;
                        point.ThisYearMinTempC = thisY.MinTempF.HasValue ? (thisY.MinTempF.Value - 32) * 5 / 9 : null;
                        point.ThisYearAvgTempC = thisY.AvgTempC;
                        point.ThisYearRainIn = thisY.TotalPrecipInches;
                        point.ThisYearRainMm = thisY.TotalPrecipMm;
                        point.ThisYearGustMph = thisY.MaxGustMph;
                        point.ThisYearGustKmh = thisY.MaxGustMph.HasValue ? thisY.MaxGustMph.Value * 1.60934 : null;
                        point.ThisYearLightningStrikes = thisY.LightningStrikes;
                        point.ThisYearLightningMinDistanceKm = thisY.LightningMinDistanceKm;
                        point.ThisYearSolarRadiationAvg = thisY.AvgSolarRadiation;
                        point.ThisYearUvAvg = thisY.AvgUvIndex;
                    }
                }

                // Previous years
                foreach (var yr in prevYears)
                {
                    var yData = matrixItem.Years.FirstOrDefault(y => y.Year == yr);
                    if (yData != null)
                    {
                        point.PrevYears.Add(new YearPointData
                        {
                            Year = yr,
                            MaxTempF = yData.MaxTempF,
                            MinTempF = yData.MinTempF,
                            AvgTempF = yData.AvgTempF,
                            MaxTempC = yData.MaxTempF.HasValue ? (yData.MaxTempF.Value - 32) * 5 / 9 : null,
                            MinTempC = yData.MinTempF.HasValue ? (yData.MinTempF.Value - 32) * 5 / 9 : null,
                            AvgTempC = yData.AvgTempC,
                            RainIn = yData.TotalPrecipInches,
                            RainMm = yData.TotalPrecipMm,
                            GustMph = yData.MaxGustMph,
                            GustKmh = yData.MaxGustMph.HasValue ? yData.MaxGustMph.Value * 1.60934 : null,
                            LightningStrikes = yData.LightningStrikes,
                            LightningMinDistanceKm = yData.LightningMinDistanceKm,
                            SolarRadiationAvg = yData.AvgSolarRadiation,
                            UvAvg = yData.AvgUvIndex
                        });
                    }
                }
            }

            Months12Timeline.Add(point);
        }
    }

    private void ApplyDailyFilter()
    {
        FilteredDailyRecords.Clear();
        var query = DailyRecords.AsEnumerable();

        if (SelectedDailyYear > 0)
        {
            string yrPrefix = SelectedDailyYear.ToString();
            query = query.Where(d => d.Date.StartsWith(yrPrefix));
        }

        if (!string.IsNullOrWhiteSpace(DailySearchText))
        {
            string s = DailySearchText.Trim();
            query = query.Where(d => d.Date.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in query.Take(200))
        {
            FilteredDailyRecords.Add(item);
        }
    }

    private void BuildSixMonthSummaries(List<DailyWeatherRecord>? dailySummaries)
    {
        if (dailySummaries == null || dailySummaries.Count == 0) return;

        var recordsWithDate = new List<(DateTime Date, DailyWeatherRecord Record)>();
        foreach (var d in dailySummaries)
        {
            if (DateTime.TryParse(d.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                recordsWithDate.Add((dt, d));
            }
        }

        if (recordsWithDate.Count == 0) return;

        var maxDt = recordsWithDate.Max(x => x.Date);
        int endYear = maxDt.Year;
        int endMonth = maxDt.Month;
        int[] prevYears = [2025, 2024, 2023];

        // ---------------------------------------------------------
        // A. 14-DAY SUMMARY (from the 14 days in the chart above)
        // ---------------------------------------------------------
        var past14Points = DaysTimeline.Where(p => p.DayOffset <= 0 && !p.IsUpcoming).TakeLast(14).ToList();

        // This Year (2026) 14-day metrics:
        var thisYearAvgTemps = past14Points.Where(p => p.ThisYearAvgTempF.HasValue).Select(p => p.ThisYearAvgTempF!.Value).ToList();
        double thisYear14DayAvgTemp = thisYearAvgTemps.Count > 0 ? thisYearAvgTemps.Average() : 0;

        var thisYearHighs = past14Points.Where(p => p.ThisYearMaxTempF.HasValue).Select(p => p.ThisYearMaxTempF!.Value).ToList();
        double thisYear14DayAvgHigh = thisYearHighs.Count > 0 ? thisYearHighs.Average() : 0;

        var thisYearLows = past14Points.Where(p => p.ThisYearMinTempF.HasValue).Select(p => p.ThisYearMinTempF!.Value).ToList();
        double thisYear14DayAvgLow = thisYearLows.Count > 0 ? thisYearLows.Average() : 0;

        var thisYearGusts = past14Points.Where(p => p.ThisYearGustMph.HasValue).Select(p => p.ThisYearGustMph!.Value).ToList();
        double thisYear14DayAvgGust = thisYearGusts.Count > 0 ? thisYearGusts.Average() : 0;
        double thisYear14DayPeakGust = thisYearGusts.Count > 0 ? thisYearGusts.Max() : 0;
        var peakGustPoint = past14Points.FirstOrDefault(p => p.ThisYearGustMph == thisYear14DayPeakGust);
        string peakGustDate = peakGustPoint?.DisplayDate ?? "";

        double thisYear14DayTotalRain = past14Points.Sum(p => p.ThisYearRainIn ?? 0);
        int thisYear14DayRainDays = past14Points.Count(p => (p.ThisYearRainIn ?? 0) > 0.001);

        int thisYear14DayTotalStrikes = past14Points.Sum(p => p.ThisYearLightningStrikes ?? 0);
        int thisYear14DayStrikeDays = past14Points.Count(p => (p.ThisYearLightningStrikes ?? 0) > 0);

        var thisYearSolar = past14Points.Where(p => p.ThisYearSolarRadiationAvg.HasValue).Select(p => p.ThisYearSolarRadiationAvg!.Value).ToList();
        double thisYear14DayAvgSolar = thisYearSolar.Count > 0 ? thisYearSolar.Average() : 0;

        // Previous Years 14-day metrics (for the exact same calendar days):
        var prevYearAvgTemps = new List<double>();
        var prevYearAvgHighs = new List<double>();
        var prevYearAvgLows = new List<double>();
        var prevYearAvgGusts = new List<double>();
        var prevYearPeakGusts = new List<double>();
        var prevYearTotalRains = new List<double>();
        var prevYearRainDays = new List<int>();
        var prevYearTotalStrikes = new List<int>();
        var prevYearStrikeDays = new List<int>();
        var prevYearAvgSolar = new List<double>();

        foreach (int py in prevYears)
        {
            var pyPoints = past14Points.Select(p => p.PrevYears.FirstOrDefault(y => y.Year == py)).Where(y => y != null).ToList();
            if (pyPoints.Count > 0)
            {
                var temps = pyPoints.Where(y => y!.AvgTempF.HasValue).Select(y => y!.AvgTempF!.Value).ToList();
                if (temps.Count > 0) prevYearAvgTemps.Add(temps.Average());

                var highs = pyPoints.Where(y => y!.MaxTempF.HasValue).Select(y => y!.MaxTempF!.Value).ToList();
                if (highs.Count > 0) prevYearAvgHighs.Add(highs.Average());

                var lows = pyPoints.Where(y => y!.MinTempF.HasValue).Select(y => y!.MinTempF!.Value).ToList();
                if (lows.Count > 0) prevYearAvgLows.Add(lows.Average());

                var gusts = pyPoints.Where(y => y!.GustMph.HasValue).Select(y => y!.GustMph!.Value).ToList();
                if (gusts.Count > 0)
                {
                    prevYearAvgGusts.Add(gusts.Average());
                    prevYearPeakGusts.Add(gusts.Max());
                }

                prevYearTotalRains.Add(pyPoints.Sum(y => y!.RainIn));
                prevYearRainDays.Add(pyPoints.Count(y => y!.RainIn > 0.001));

                prevYearTotalStrikes.Add(pyPoints.Sum(y => y!.LightningStrikes));
                prevYearStrikeDays.Add(pyPoints.Count(y => y!.LightningStrikes > 0));

                var s = pyPoints.Where(y => y!.SolarRadiationAvg.HasValue).Select(y => y!.SolarRadiationAvg!.Value).ToList();
                if (s.Count > 0) prevYearAvgSolar.Add(s.Average());
            }
        }

        double prevYears14DayAvgTemp = prevYearAvgTemps.Count > 0 ? prevYearAvgTemps.Average() : 0;
        double prevYears14DayAvgHigh = prevYearAvgHighs.Count > 0 ? prevYearAvgHighs.Average() : 0;
        double prevYears14DayAvgLow = prevYearAvgLows.Count > 0 ? prevYearAvgLows.Average() : 0;
        double prevYears14DayAvgGust = prevYearAvgGusts.Count > 0 ? prevYearAvgGusts.Average() : 0;
        double prevYears14DayAvgPeakGust = prevYearPeakGusts.Count > 0 ? prevYearPeakGusts.Average() : 0;
        double prevYears14DayAvgRain = prevYearTotalRains.Count > 0 ? prevYearTotalRains.Average() : 0;
        double prevYears14DayAvgRainDays = prevYearRainDays.Count > 0 ? prevYearRainDays.Average() : 0;
        double prevYears14DayAvgStrikes = prevYearTotalStrikes.Count > 0 ? prevYearTotalStrikes.Average() : 0;
        double prevYears14DayAvgStrikeDays = prevYearStrikeDays.Count > 0 ? prevYearStrikeDays.Average() : 0;
        double prevYears14DayAvgSolar = prevYearAvgSolar.Count > 0 ? prevYearAvgSolar.Average() : 0;

        // Deltas:
        double deltaAvgTemp = thisYear14DayAvgTemp - prevYears14DayAvgTemp;
        double deltaAvgHigh = thisYear14DayAvgHigh - prevYears14DayAvgHigh;
        double deltaAvgLow = thisYear14DayAvgLow - prevYears14DayAvgLow;
        double deltaGust = thisYear14DayAvgGust - prevYears14DayAvgGust;
        double deltaPeakGust = thisYear14DayPeakGust - prevYears14DayAvgPeakGust;
        double deltaRain = thisYear14DayTotalRain - prevYears14DayAvgRain;
        double deltaRainDays = thisYear14DayRainDays - prevYears14DayAvgRainDays;
        double deltaStrikes = thisYear14DayTotalStrikes - prevYears14DayAvgStrikes;
        double deltaStrikeDays = thisYear14DayStrikeDays - prevYears14DayAvgStrikeDays;
        double deltaSolar14 = thisYear14DayAvgSolar - prevYears14DayAvgSolar;

        // 1. Average Daily Temp:
        SixMonthAvgTempThisYearText = $"Avg Temp: {thisYear14DayAvgTemp:F1}°F";
        SixMonthAvgTempDeltaText = $"{(deltaAvgTemp >= 0 ? "+" : "")}{deltaAvgTemp:F1}°F vs {prevYears14DayAvgTemp:F1}°F prev yrs avg";
        SixMonthAvgTempDeltaBrush = deltaAvgTemp >= 0 
            ? new SolidColorBrush(Color.FromArgb(255, 220, 38, 38))  // Red (#DC2626)
            : new SolidColorBrush(Color.FromArgb(255, 22, 163, 74)); // Green (#16A34A)

        // 2. Max/Min Temperature Dual:
        SixMonthAvgHighThisYearText = $"Avg Max: {thisYear14DayAvgHigh:F1}°F";
        SixMonthAvgHighDeltaText = $"{(deltaAvgHigh >= 0 ? "+" : "")}{deltaAvgHigh:F1}°F vs {prevYears14DayAvgHigh:F1}°F prev yrs avg";
        SixMonthAvgHighDeltaBrush = deltaAvgHigh >= 0 
            ? new SolidColorBrush(Color.FromArgb(255, 220, 38, 38))
            : new SolidColorBrush(Color.FromArgb(255, 22, 163, 74));
        SixMonthAvgLowThisYearText = $"Avg Min: {thisYear14DayAvgLow:F1}°F";
        SixMonthAvgLowDeltaText = $"{(deltaAvgLow >= 0 ? "+" : "")}{deltaAvgLow:F1}°F vs {prevYears14DayAvgLow:F1}°F prev yrs avg";
        SixMonthAvgLowDeltaBrush = deltaAvgLow >= 0 
            ? new SolidColorBrush(Color.FromArgb(255, 220, 38, 38))
            : new SolidColorBrush(Color.FromArgb(255, 22, 163, 74));

        // 3. Peak Daily Wind Gust:
        SixMonthWindThisYearText = $"Avg Daily Gust: {thisYear14DayAvgGust:F1} mph";
        SixMonthWindDeltaText = $"{(deltaGust >= 0 ? "+" : "")}{deltaGust:F1} mph vs {prevYears14DayAvgGust:F1} mph prev yrs avg";
        SixMonthWindDeltaBrush = deltaGust >= 0
            ? new SolidColorBrush(Color.FromArgb(255, 220, 38, 38))
            : new SolidColorBrush(Color.FromArgb(255, 37, 99, 235));
        SixMonthWindPeakText = !string.IsNullOrEmpty(peakGustDate) ? $"Peak Gust: {thisYear14DayPeakGust:F1} mph ({peakGustDate})" : $"Peak Gust: {thisYear14DayPeakGust:F1} mph";
        SixMonthWindPeakDeltaText = $"{(deltaPeakGust >= 0 ? "+" : "")}{deltaPeakGust:F1} mph vs {prevYears14DayAvgPeakGust:F1} mph prev yrs peak";
        SixMonthWindPeakDeltaBrush = deltaPeakGust >= 0
            ? new SolidColorBrush(Color.FromArgb(255, 220, 38, 38))
            : new SolidColorBrush(Color.FromArgb(255, 37, 99, 235));

        // 4. Precipitation:
        SixMonthRainThisYearText = $"Total Rain: {thisYear14DayTotalRain:F2} in";
        SixMonthRainDeltaText = $"{(deltaRain >= 0 ? "+" : "")}{deltaRain:F2} in vs {prevYears14DayAvgRain:F2} in prev yrs avg";
        SixMonthRainDeltaBrush = deltaRain >= 0
            ? new SolidColorBrush(Color.FromArgb(255, 5, 150, 105))  // Green (#059669)
            : new SolidColorBrush(Color.FromArgb(255, 37, 99, 235)); // Blue (#2563EB)
        SixMonthRainDaysText = $"Rain Days: {thisYear14DayRainDays}";
        SixMonthRainDaysDeltaText = $"{(deltaRainDays >= 0 ? "+" : "")}{deltaRainDays:F1} vs {prevYears14DayAvgRainDays:F1} prev yrs avg";
        SixMonthRainDaysDeltaBrush = deltaRainDays >= 0
            ? new SolidColorBrush(Color.FromArgb(255, 5, 150, 105))
            : new SolidColorBrush(Color.FromArgb(255, 37, 99, 235));

        // 5. Lightning:
        SixMonthLightningThisYearText = $"Total Strikes: {thisYear14DayTotalStrikes:N0}";
        SixMonthLightningDeltaText = $"{(deltaStrikes >= 0 ? "+" : "")}{deltaStrikes:N0} vs {prevYears14DayAvgStrikes:N0} prev yrs avg";
        SixMonthLightningDeltaBrush = deltaStrikes >= 0
            ? new SolidColorBrush(Color.FromArgb(255, 217, 119, 6)) // Amber 600 (#D97706)
            : new SolidColorBrush(Color.FromArgb(255, 37, 99, 235)); // Blue (#2563EB)
        SixMonthLightningDaysText = $"Days with Lightning: {thisYear14DayStrikeDays}";
        SixMonthLightningDaysDeltaText = $"{(deltaStrikeDays >= 0 ? "+" : "")}{deltaStrikeDays:F1} vs {prevYears14DayAvgStrikeDays:F1} prev yrs avg";
        SixMonthLightningDaysDeltaBrush = deltaStrikeDays >= 0
            ? new SolidColorBrush(Color.FromArgb(255, 217, 119, 6))
            : new SolidColorBrush(Color.FromArgb(255, 37, 99, 235));

        // 6. Solar (14-Day Tab):
        FourteenDayTabSolarThisYearText = $"Avg Solar: {thisYear14DayAvgSolar:F0} W/m²";
        FourteenDayTabSolarDeltaText = $"{(deltaSolar14 >= 0 ? "+" : "")}{deltaSolar14:F0} W/m² vs {prevYears14DayAvgSolar:F0} W/m² prev yrs avg";
        FourteenDayTabSolarDeltaBrush = GetSolarBrush(deltaSolar14);

        // Executive Glance Overview (14-Day)
        Glance14DayRainDaysText = $"{(deltaRainDays >= 0 ? "+" : "")}{deltaRainDays:F1} days";
        Glance14DayRainDaysBrush = GetRainBrush(deltaRainDays);
        Glance14DayRainAmountText = $"{(deltaRain >= 0 ? "+" : "")}{deltaRain:F2}\"";
        Glance14DayRainAmountBrush = GetRainBrush(deltaRain);

        Glance14DayTempMaxText = $"{(deltaAvgHigh >= 0 ? "+" : "")}{deltaAvgHigh:F1}°";
        Glance14DayTempMaxBrush = GetTempBrush(deltaAvgHigh);
        Glance14DayTempMinText = $"{(deltaAvgLow >= 0 ? "+" : "")}{deltaAvgLow:F1}°";
        Glance14DayTempMinBrush = GetTempBrush(deltaAvgLow);
        Glance14DayTempAvgText = $"{(deltaAvgTemp >= 0 ? "+" : "")}{deltaAvgTemp:F1}°";
        Glance14DayTempAvgBrush = GetTempBrush(deltaAvgTemp);

        Glance14DayWindAvgText = $"{(deltaGust >= 0 ? "+" : "")}{deltaGust:F1} mph";
        Glance14DayWindAvgBrush = GetWindBrush(deltaGust);
        Glance14DayWindPeakText = $"Peak {thisYear14DayPeakGust:F1} mph";

        Glance14DayLightningStrikesText = $"{(deltaStrikes >= 0 ? "+" : "")}{deltaStrikes:N0} strikes";
        Glance14DayLightningStrikesBrush = GetLightningBrush(deltaStrikes);
        Glance14DayLightningDaysText = $"{(deltaStrikeDays >= 0 ? "+" : "")}{deltaStrikeDays:F1} days";
        Glance14DayLightningDaysBrush = GetLightningBrush(deltaStrikeDays);

        Glance14DaySolarAvgText = $"{(deltaSolar14 >= 0 ? "+" : "")}{deltaSolar14:F0} W/m²";
        Glance14DaySolarAvgBrush = GetSolarBrush(deltaSolar14);

        // ---------------------------------------------------------
        // B. 6 MONTHS: 12 MONTHS IN CHART ORDER (-5 to +6)
        // ---------------------------------------------------------
        var targetMonths = new List<(int Year, int Month, string ShortName, bool IsFuture, bool IsCurrent)>();
        for (int offset = -5; offset <= 6; offset++)
        {
            var mDt = new DateTime(endYear, endMonth, 1).AddMonths(offset);
            targetMonths.Add((mDt.Year, mDt.Month, mDt.ToString("MMM", CultureInfo.InvariantCulture), offset > 0, offset == 0));
        }

        var monthlyTempRangeParts = new List<string>();
        var monthlyTempAvgParts = new List<string>();
        var monthlyRainParts = new List<string>();
        var monthlyWindParts = new List<string>();
        var monthlyLightningParts = new List<string>();
        var monthlySolarParts = new List<string>();

        foreach (var tm in targetMonths)
        {
            if (!tm.IsFuture)
            {
                var monthRecs = recordsWithDate.Where(x => x.Date.Year == tm.Year && x.Date.Month == tm.Month).ToList();
                if (monthRecs.Count > 0)
                {
                    double mHigh = monthRecs.Max(x => x.Record.TempHighF ?? double.MinValue);
                    double mLow = monthRecs.Min(x => x.Record.TempLowF ?? double.MaxValue);

                    var avgRecs = monthRecs.Where(x => x.Record.TempAvgF.HasValue).ToList();
                    double mAvg = avgRecs.Count > 0 ? avgRecs.Average(x => x.Record.TempAvgF!.Value) : 0;

                    double mRain = monthRecs.Sum(x => x.Record.PrecipInches);
                    int mRainDays = monthRecs.Count(x => x.Record.PrecipInches > 0.001);

                    double mGust = monthRecs.Max(x => x.Record.WindGustMph ?? 0);

                    int mStrikes = monthRecs.Sum(x => x.Record.LightningStrikes);
                    int mStrikeDays = monthRecs.Count(x => x.Record.LightningStrikes > 0);

                    var sRecs = monthRecs.Where(x => x.Record.SolarRadiationAvg.HasValue).ToList();
                    double mSolar = sRecs.Count > 0 ? sRecs.Average(x => x.Record.SolarRadiationAvg!.Value) : 0;

                    monthlyTempRangeParts.Add($"{tm.ShortName}: {mHigh:F1}° / {mLow:F1}°");
                    monthlyTempAvgParts.Add($"{tm.ShortName}: {mAvg:F1}°F");
                    monthlyRainParts.Add($"{tm.ShortName}: {mRain:F2}\" ({mRainDays}d)");
                    monthlyWindParts.Add($"{tm.ShortName}: {mGust:F1} mph");
                    monthlyLightningParts.Add($"{tm.ShortName}: {mStrikes:N0} ({mStrikeDays}d)");
                    monthlySolarParts.Add($"{tm.ShortName}: {mSolar:F0} W/m²");
                }
            }
            else
            {
                // Future months: multi-year average of previous years (2025, 2024, 2023)
                var pyMonthHighs = new List<double>();
                var pyMonthLows = new List<double>();
                var pyMonthAvgs = new List<double>();
                var pyMonthRains = new List<double>();
                var pyMonthRainDays = new List<int>();
                var pyMonthGusts = new List<double>();
                var pyMonthStrikes = new List<int>();
                var pyMonthStrikeDays = new List<int>();
                var pyMonthSolar = new List<double>();

                foreach (int py in prevYears)
                {
                    var pyRecs = recordsWithDate.Where(x => x.Date.Year == py && x.Date.Month == tm.Month).ToList();
                    if (pyRecs.Count > 0)
                    {
                        var highs = pyRecs.Where(r => r.Record.TempHighF.HasValue).Select(r => r.Record.TempHighF!.Value).ToList();
                        if (highs.Count > 0) pyMonthHighs.Add(highs.Max());

                        var lows = pyRecs.Where(r => r.Record.TempLowF.HasValue).Select(r => r.Record.TempLowF!.Value).ToList();
                        if (lows.Count > 0) pyMonthLows.Add(lows.Min());

                        var avgs = pyRecs.Where(r => r.Record.TempAvgF.HasValue).Select(r => r.Record.TempAvgF!.Value).ToList();
                        if (avgs.Count > 0) pyMonthAvgs.Add(avgs.Average());

                        pyMonthRains.Add(pyRecs.Sum(r => r.Record.PrecipInches));
                        pyMonthRainDays.Add(pyRecs.Count(r => r.Record.PrecipInches > 0.001));

                        var gusts = pyRecs.Where(r => r.Record.WindGustMph.HasValue).Select(r => r.Record.WindGustMph!.Value).ToList();
                        if (gusts.Count > 0) pyMonthGusts.Add(gusts.Max());

                        pyMonthStrikes.Add(pyRecs.Sum(r => r.Record.LightningStrikes));
                        pyMonthStrikeDays.Add(pyRecs.Count(r => r.Record.LightningStrikes > 0));

                        var sList = pyRecs.Where(r => r.Record.SolarRadiationAvg.HasValue).Select(r => r.Record.SolarRadiationAvg!.Value).ToList();
                        if (sList.Count > 0) pyMonthSolar.Add(sList.Average());
                    }
                }

                double hHigh = pyMonthHighs.Count > 0 ? pyMonthHighs.Average() : 0;
                double hLow = pyMonthLows.Count > 0 ? pyMonthLows.Average() : 0;
                double hAvg = pyMonthAvgs.Count > 0 ? pyMonthAvgs.Average() : 0;
                double hRain = pyMonthRains.Count > 0 ? pyMonthRains.Average() : 0;
                double hRainDays = pyMonthRainDays.Count > 0 ? pyMonthRainDays.Average() : 0;
                double hGust = pyMonthGusts.Count > 0 ? pyMonthGusts.Average() : 0;
                double hStrikes = pyMonthStrikes.Count > 0 ? pyMonthStrikes.Average() : 0;
                double hStrikeDays = pyMonthStrikeDays.Count > 0 ? pyMonthStrikeDays.Average() : 0;
                double hSolar = pyMonthSolar.Count > 0 ? pyMonthSolar.Average() : 0;

                monthlyTempRangeParts.Add($"{tm.ShortName}: {hHigh:F1}° / {hLow:F1}°");
                monthlyTempAvgParts.Add($"{tm.ShortName}: {hAvg:F1}°F");
                monthlyRainParts.Add($"{tm.ShortName}: {hRain:F2}\" ({hRainDays:F0}d)");
                monthlyWindParts.Add($"{tm.ShortName}: {hGust:F1} mph");
                monthlyLightningParts.Add($"{tm.ShortName}: {hStrikes:N0} ({hStrikeDays:F0}d)");
                monthlySolarParts.Add($"{tm.ShortName}: {hSolar:F0} W/m²");
            }
        }

        SixMonthTempAvgMonthly = string.Join("  |  ", monthlyTempAvgParts);
        SixMonthTempRangeMonthly = string.Join("  |  ", monthlyTempRangeParts);
        SixMonthRainMonthly = string.Join("  |  ", monthlyRainParts);
        SixMonthWindMonthly = string.Join("  |  ", monthlyWindParts);
        SixMonthLightningMonthly = string.Join("  |  ", monthlyLightningParts);
        SixMonthSolarMonthly = string.Join("  |  ", monthlySolarParts);

        // ---------------------------------------------------------
        // C. 6-MONTH TAB SUMMARY
        // ---------------------------------------------------------
        var sixMonthStart = new DateTime(endYear, endMonth, 1).AddMonths(-5);
        var ty6Recs = recordsWithDate.Where(x => x.Date >= sixMonthStart && x.Date <= maxDt).ToList();

        if (ty6Recs.Count > 0)
        {
            var ty6AvgTemps = ty6Recs.Where(r => r.Record.TempAvgF.HasValue).Select(r => r.Record.TempAvgF!.Value).ToList();
            double thisYear6MoAvgTemp = ty6AvgTemps.Count > 0 ? ty6AvgTemps.Average() : 0;

            var ty6Highs = ty6Recs.Where(r => r.Record.TempHighF.HasValue).Select(r => r.Record.TempHighF!.Value).ToList();
            double thisYear6MoAvgHigh = ty6Highs.Count > 0 ? ty6Highs.Average() : 0;

            var ty6Lows = ty6Recs.Where(r => r.Record.TempLowF.HasValue).Select(r => r.Record.TempLowF!.Value).ToList();
            double thisYear6MoAvgLow = ty6Lows.Count > 0 ? ty6Lows.Average() : 0;

            var ty6Gusts = ty6Recs.Where(r => r.Record.WindGustMph.HasValue).Select(r => r.Record.WindGustMph!.Value).ToList();
            double thisYear6MoAvgGust = ty6Gusts.Count > 0 ? ty6Gusts.Average() : 0;
            double thisYear6MoPeakGust = ty6Gusts.Count > 0 ? ty6Gusts.Max() : 0;
            var peakGustRec = ty6Recs.FirstOrDefault(r => r.Record.WindGustMph == thisYear6MoPeakGust);
            string peakGustDate6Mo = peakGustRec.Date != default ? peakGustRec.Date.ToString("MMM d") : "";

            double thisYear6MoTotalRain = ty6Recs.Sum(r => r.Record.PrecipInches);
            int thisYear6MoRainDays = ty6Recs.Count(r => r.Record.PrecipInches > 0.001);

            int thisYear6MoTotalStrikes = ty6Recs.Sum(r => r.Record.LightningStrikes);
            int thisYear6MoStrikeDays = ty6Recs.Count(r => r.Record.LightningStrikes > 0);

            var ty6Solar = ty6Recs.Where(r => r.Record.SolarRadiationAvg.HasValue).Select(r => r.Record.SolarRadiationAvg!.Value).ToList();
            double thisYear6MoAvgSolar = ty6Solar.Count > 0 ? ty6Solar.Average() : 0;

            // Previous Years for the corresponding calendar months
            var targetMonthNumbers = new HashSet<int>();
            for (int offset = -5; offset <= 0; offset++)
            {
                targetMonthNumbers.Add(new DateTime(endYear, endMonth, 1).AddMonths(offset).Month);
            }

            var py6AvgTemps = new List<double>();
            var py6AvgHighs = new List<double>();
            var py6AvgLows = new List<double>();
            var py6AvgGusts = new List<double>();
            var py6PeakGusts = new List<double>();
            var py6TotalRains = new List<double>();
            var py6RainDays = new List<int>();
            var py6TotalStrikes = new List<int>();
            var py6StrikeDays = new List<int>();
            var py6AvgSolar = new List<double>();

            foreach (int py in prevYears)
            {
                var pyRecs = recordsWithDate.Where(r => r.Date.Year == py && targetMonthNumbers.Contains(r.Date.Month)).ToList();
                if (pyRecs.Count > 0)
                {
                    var t = pyRecs.Where(r => r.Record.TempAvgF.HasValue).Select(r => r.Record.TempAvgF!.Value).ToList();
                    if (t.Count > 0) py6AvgTemps.Add(t.Average());

                    var h = pyRecs.Where(r => r.Record.TempHighF.HasValue).Select(r => r.Record.TempHighF!.Value).ToList();
                    if (h.Count > 0) py6AvgHighs.Add(h.Average());

                    var l = pyRecs.Where(r => r.Record.TempLowF.HasValue).Select(r => r.Record.TempLowF!.Value).ToList();
                    if (l.Count > 0) py6AvgLows.Add(l.Average());

                    var g = pyRecs.Where(r => r.Record.WindGustMph.HasValue).Select(r => r.Record.WindGustMph!.Value).ToList();
                    if (g.Count > 0)
                    {
                        py6AvgGusts.Add(g.Average());
                        py6PeakGusts.Add(g.Max());
                    }

                    py6TotalRains.Add(pyRecs.Sum(r => r.Record.PrecipInches));
                    py6RainDays.Add(pyRecs.Count(r => r.Record.PrecipInches > 0.001));

                    py6TotalStrikes.Add(pyRecs.Sum(r => r.Record.LightningStrikes));
                    py6StrikeDays.Add(pyRecs.Count(r => r.Record.LightningStrikes > 0));

                    var s = pyRecs.Where(r => r.Record.SolarRadiationAvg.HasValue).Select(r => r.Record.SolarRadiationAvg!.Value).ToList();
                    if (s.Count > 0) py6AvgSolar.Add(s.Average());
                }
            }

            double prevYears6MoAvgTemp = py6AvgTemps.Count > 0 ? py6AvgTemps.Average() : 0;
            double prevYears6MoAvgHigh = py6AvgHighs.Count > 0 ? py6AvgHighs.Average() : 0;
            double prevYears6MoAvgLow = py6AvgLows.Count > 0 ? py6AvgLows.Average() : 0;
            double prevYears6MoAvgGust = py6AvgGusts.Count > 0 ? py6AvgGusts.Average() : 0;
            double prevYears6MoAvgPeakGust = py6PeakGusts.Count > 0 ? py6PeakGusts.Average() : 0;
            double prevYears6MoAvgRain = py6TotalRains.Count > 0 ? py6TotalRains.Average() : 0;
            double prevYears6MoAvgRainDays = py6RainDays.Count > 0 ? py6RainDays.Average() : 0;
            double prevYears6MoAvgStrikes = py6TotalStrikes.Count > 0 ? py6TotalStrikes.Average() : 0;
            double prevYears6MoAvgStrikeDays = py6StrikeDays.Count > 0 ? py6StrikeDays.Average() : 0;
            double prevYears6MoAvgSolar = py6AvgSolar.Count > 0 ? py6AvgSolar.Average() : 0;

            double delta6AvgTemp = thisYear6MoAvgTemp - prevYears6MoAvgTemp;
            double delta6AvgHigh = thisYear6MoAvgHigh - prevYears6MoAvgHigh;
            double delta6AvgLow = thisYear6MoAvgLow - prevYears6MoAvgLow;
            double delta6Gust = thisYear6MoAvgGust - prevYears6MoAvgGust;
            double delta6PeakGust = thisYear6MoPeakGust - prevYears6MoAvgPeakGust;
            double delta6Rain = thisYear6MoTotalRain - prevYears6MoAvgRain;
            double delta6RainDays = thisYear6MoRainDays - prevYears6MoAvgRainDays;
            double delta6Strikes = thisYear6MoTotalStrikes - prevYears6MoAvgStrikes;
            double delta6StrikeDays = thisYear6MoStrikeDays - prevYears6MoAvgStrikeDays;
            double delta6Solar = thisYear6MoAvgSolar - prevYears6MoAvgSolar;

            SixMonthTabAvgTempThisYearText = $"Avg Temp: {thisYear6MoAvgTemp:F1}°F";
            SixMonthTabAvgTempDeltaText = $"{(delta6AvgTemp >= 0 ? "+" : "")}{delta6AvgTemp:F1}°F vs {prevYears6MoAvgTemp:F1}°F prev yrs avg";
            SixMonthTabAvgTempDeltaBrush = delta6AvgTemp >= 0
                ? new SolidColorBrush(Color.FromArgb(255, 220, 38, 38))
                : new SolidColorBrush(Color.FromArgb(255, 22, 163, 74));

            SixMonthTabAvgHighThisYearText = $"Avg Max: {thisYear6MoAvgHigh:F1}°F";
            SixMonthTabAvgHighDeltaText = $"{(delta6AvgHigh >= 0 ? "+" : "")}{delta6AvgHigh:F1}°F vs {prevYears6MoAvgHigh:F1}°F prev yrs avg";
            SixMonthTabAvgHighDeltaBrush = delta6AvgHigh >= 0
                ? new SolidColorBrush(Color.FromArgb(255, 220, 38, 38))
                : new SolidColorBrush(Color.FromArgb(255, 22, 163, 74));
            SixMonthTabAvgLowThisYearText = $"Avg Min: {thisYear6MoAvgLow:F1}°F";
            SixMonthTabAvgLowDeltaText = $"{(delta6AvgLow >= 0 ? "+" : "")}{delta6AvgLow:F1}°F vs {prevYears6MoAvgLow:F1}°F prev yrs avg";
            SixMonthTabAvgLowDeltaBrush = delta6AvgLow >= 0
                ? new SolidColorBrush(Color.FromArgb(255, 220, 38, 38))
                : new SolidColorBrush(Color.FromArgb(255, 22, 163, 74));

            SixMonthTabWindThisYearText = $"Avg Daily Gust: {thisYear6MoAvgGust:F1} mph";
            SixMonthTabWindDeltaText = $"{(delta6Gust >= 0 ? "+" : "")}{delta6Gust:F1} mph vs {prevYears6MoAvgGust:F1} mph prev yrs avg";
            SixMonthTabWindDeltaBrush = delta6Gust >= 0
                ? new SolidColorBrush(Color.FromArgb(255, 220, 38, 38))
                : new SolidColorBrush(Color.FromArgb(255, 37, 99, 235));
            SixMonthTabWindPeakText = !string.IsNullOrEmpty(peakGustDate6Mo) ? $"Peak Gust: {thisYear6MoPeakGust:F1} mph ({peakGustDate6Mo})" : $"Peak Gust: {thisYear6MoPeakGust:F1} mph";
            SixMonthTabWindPeakDeltaText = $"{(delta6PeakGust >= 0 ? "+" : "")}{delta6PeakGust:F1} mph vs {prevYears6MoAvgPeakGust:F1} mph prev yrs peak";
            SixMonthTabWindPeakDeltaBrush = delta6PeakGust >= 0
                ? new SolidColorBrush(Color.FromArgb(255, 220, 38, 38))
                : new SolidColorBrush(Color.FromArgb(255, 37, 99, 235));

            SixMonthTabRainThisYearText = $"Total Rain: {thisYear6MoTotalRain:F2} in";
            SixMonthTabRainDeltaText = $"{(delta6Rain >= 0 ? "+" : "")}{delta6Rain:F2} in vs {prevYears6MoAvgRain:F2} in prev yrs avg";
            SixMonthTabRainDeltaBrush = delta6Rain >= 0
                ? new SolidColorBrush(Color.FromArgb(255, 5, 150, 105))
                : new SolidColorBrush(Color.FromArgb(255, 37, 99, 235));
            SixMonthTabRainDaysText = $"Rain Days: {thisYear6MoRainDays}";
            SixMonthTabRainDaysDeltaText = $"{(delta6RainDays >= 0 ? "+" : "")}{delta6RainDays:F1} vs {prevYears6MoAvgRainDays:F1} prev yrs avg";
            SixMonthTabRainDaysDeltaBrush = delta6RainDays >= 0
                ? new SolidColorBrush(Color.FromArgb(255, 5, 150, 105))
                : new SolidColorBrush(Color.FromArgb(255, 37, 99, 235));

            SixMonthTabLightningThisYearText = $"Total Strikes: {thisYear6MoTotalStrikes:N0}";
            SixMonthTabLightningDeltaText = $"{(delta6Strikes >= 0 ? "+" : "")}{delta6Strikes:N0} vs {prevYears6MoAvgStrikes:N0} prev yrs avg";
            SixMonthTabLightningDeltaBrush = delta6Strikes >= 0
                ? new SolidColorBrush(Color.FromArgb(255, 217, 119, 6))
                : new SolidColorBrush(Color.FromArgb(255, 37, 99, 235));
            SixMonthTabLightningDaysText = $"Days with Lightning: {thisYear6MoStrikeDays}";
            SixMonthTabLightningDaysDeltaText = $"{(delta6StrikeDays >= 0 ? "+" : "")}{delta6StrikeDays:F1} vs {prevYears6MoAvgStrikeDays:F1} prev yrs avg";
            SixMonthTabLightningDaysDeltaBrush = delta6StrikeDays >= 0
                ? new SolidColorBrush(Color.FromArgb(255, 217, 119, 6))
                : new SolidColorBrush(Color.FromArgb(255, 37, 99, 235));

            SixMonthTabSolarThisYearText = $"Avg Solar: {thisYear6MoAvgSolar:F0} W/m²";
            SixMonthTabSolarDeltaText = $"{(delta6Solar >= 0 ? "+" : "")}{delta6Solar:F0} W/m² vs {prevYears6MoAvgSolar:F0} W/m² prev yrs avg";
            SixMonthTabSolarDeltaBrush = GetSolarBrush(delta6Solar);

            // Executive Glance Overview (6-Month)
            Glance6MonthRainDaysText = $"{(delta6RainDays >= 0 ? "+" : "")}{delta6RainDays:F1} days";
            Glance6MonthRainDaysBrush = GetRainBrush(delta6RainDays);
            Glance6MonthRainAmountText = $"{(delta6Rain >= 0 ? "+" : "")}{delta6Rain:F2}\"";
            Glance6MonthRainAmountBrush = GetRainBrush(delta6Rain);

            Glance6MonthTempMaxText = $"{(delta6AvgHigh >= 0 ? "+" : "")}{delta6AvgHigh:F1}°";
            Glance6MonthTempMaxBrush = GetTempBrush(delta6AvgHigh);
            Glance6MonthTempMinText = $"{(delta6AvgLow >= 0 ? "+" : "")}{delta6AvgLow:F1}°";
            Glance6MonthTempMinBrush = GetTempBrush(delta6AvgLow);
            Glance6MonthTempAvgText = $"{(delta6AvgTemp >= 0 ? "+" : "")}{delta6AvgTemp:F1}°";
            Glance6MonthTempAvgBrush = GetTempBrush(delta6AvgTemp);

            Glance6MonthWindAvgText = $"{(delta6Gust >= 0 ? "+" : "")}{delta6Gust:F1} mph";
            Glance6MonthWindAvgBrush = GetWindBrush(delta6Gust);
            Glance6MonthWindPeakText = $"Peak {thisYear6MoPeakGust:F1} mph";

            Glance6MonthLightningStrikesText = $"{(delta6Strikes >= 0 ? "+" : "")}{delta6Strikes:N0} strikes";
            Glance6MonthLightningStrikesBrush = GetLightningBrush(delta6Strikes);
            Glance6MonthLightningDaysText = $"{(delta6StrikeDays >= 0 ? "+" : "")}{delta6StrikeDays:F1} days";
            Glance6MonthLightningDaysBrush = GetLightningBrush(delta6StrikeDays);

            Glance6MonthSolarAvgText = $"{(delta6Solar >= 0 ? "+" : "")}{delta6Solar:F0} W/m²";
            Glance6MonthSolarAvgBrush = GetSolarBrush(delta6Solar);
        }
    }

    private void BuildTwelveMonthSummaries(List<DailyWeatherRecord>? dailySummaries)
    {
        if (dailySummaries == null || dailySummaries.Count == 0) return;

        var recordsWithDate = dailySummaries
            .Select(d => DateTime.TryParse(d.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? (Date: dt, Record: d) : (Date: default, Record: d))
            .Where(x => x.Date != default)
            .OrderBy(x => x.Date)
            .ToList();

        if (recordsWithDate.Count == 0) return;

        var latestDate = recordsWithDate.Last().Date;
        int endYear = latestDate.Year;
        int endMonth = latestDate.Month;

        var target12Months = new List<(int Year, int Month, string ShortName)>();
        for (int offset = -11; offset <= 0; offset++)
        {
            var mDt = new DateTime(endYear, endMonth, 1).AddMonths(offset);
            target12Months.Add((mDt.Year, mDt.Month, mDt.ToString("MMM", CultureInfo.InvariantCulture)));
        }

        // Current 12-month period
        var ty12Recs = recordsWithDate.Where(r => target12Months.Any(tm => tm.Year == r.Date.Year && tm.Month == r.Date.Month)).ToList();
        if (ty12Recs.Count == 0) return;

        var ty12Temps = ty12Recs.Where(r => r.Record.TempAvgF.HasValue).Select(r => r.Record.TempAvgF!.Value).ToList();
        double thisYear12MoAvgTemp = ty12Temps.Count > 0 ? ty12Temps.Average() : 0;

        var ty12Highs = ty12Recs.Where(r => r.Record.TempHighF.HasValue).Select(r => r.Record.TempHighF!.Value).ToList();
        double thisYear12MoAvgHigh = ty12Highs.Count > 0 ? ty12Highs.Average() : 0;

        var ty12Lows = ty12Recs.Where(r => r.Record.TempLowF.HasValue).Select(r => r.Record.TempLowF!.Value).ToList();
        double thisYear12MoAvgLow = ty12Lows.Count > 0 ? ty12Lows.Average() : 0;

        var ty12Gusts = ty12Recs.Where(r => r.Record.WindGustMph.HasValue).Select(r => r.Record.WindGustMph!.Value).ToList();
        double thisYear12MoAvgGust = ty12Gusts.Count > 0 ? ty12Gusts.Average() : 0;
        double thisYear12MoPeakGust = ty12Gusts.Count > 0 ? ty12Gusts.Max() : 0;
        var peakGustRec = ty12Recs.FirstOrDefault(r => r.Record.WindGustMph == thisYear12MoPeakGust);
        string peakGustDate12Mo = peakGustRec.Date != default ? peakGustRec.Date.ToString("MMM d") : "";

        double thisYear12MoTotalRain = ty12Recs.Sum(r => r.Record.PrecipInches);
        int thisYear12MoRainDays = ty12Recs.Count(r => r.Record.PrecipInches > 0.001);

        int thisYear12MoTotalStrikes = ty12Recs.Sum(r => r.Record.LightningStrikes);
        int thisYear12MoStrikeDays = ty12Recs.Count(r => r.Record.LightningStrikes > 0);

        var ty12Solar = ty12Recs.Where(r => r.Record.SolarRadiationAvg.HasValue).Select(r => r.Record.SolarRadiationAvg!.Value).ToList();
        double thisYear12MoAvgSolar = ty12Solar.Count > 0 ? ty12Solar.Average() : 0;

        // Previous periods (1, 2, 3 years back)
        var py12AvgTemps = new List<double>();
        var py12AvgHighs = new List<double>();
        var py12AvgLows = new List<double>();
        var py12AvgGusts = new List<double>();
        var py12PeakGusts = new List<double>();
        var py12TotalRains = new List<double>();
        var py12RainDays = new List<int>();
        var py12TotalStrikes = new List<int>();
        var py12StrikeDays = new List<int>();
        var py12AvgSolar = new List<double>();

        foreach (int yrOffset in new[] { 1, 2, 3 })
        {
            var pyRecs = recordsWithDate.Where(r => target12Months.Any(tm => (tm.Year - yrOffset) == r.Date.Year && tm.Month == r.Date.Month)).ToList();
            if (pyRecs.Count > 0)
            {
                var t = pyRecs.Where(r => r.Record.TempAvgF.HasValue).Select(r => r.Record.TempAvgF!.Value).ToList();
                if (t.Count > 0) py12AvgTemps.Add(t.Average());

                var h = pyRecs.Where(r => r.Record.TempHighF.HasValue).Select(r => r.Record.TempHighF!.Value).ToList();
                if (h.Count > 0) py12AvgHighs.Add(h.Average());

                var l = pyRecs.Where(r => r.Record.TempLowF.HasValue).Select(r => r.Record.TempLowF!.Value).ToList();
                if (l.Count > 0) py12AvgLows.Add(l.Average());

                var g = pyRecs.Where(r => r.Record.WindGustMph.HasValue).Select(r => r.Record.WindGustMph!.Value).ToList();
                if (g.Count > 0)
                {
                    py12AvgGusts.Add(g.Average());
                    py12PeakGusts.Add(g.Max());
                }

                py12TotalRains.Add(pyRecs.Sum(r => r.Record.PrecipInches));
                py12RainDays.Add(pyRecs.Count(r => r.Record.PrecipInches > 0.001));

                py12TotalStrikes.Add(pyRecs.Sum(r => r.Record.LightningStrikes));
                py12StrikeDays.Add(pyRecs.Count(r => r.Record.LightningStrikes > 0));

                var s = pyRecs.Where(r => r.Record.SolarRadiationAvg.HasValue).Select(r => r.Record.SolarRadiationAvg!.Value).ToList();
                if (s.Count > 0) py12AvgSolar.Add(s.Average());
            }
        }

        double prevYears12MoAvgTemp = py12AvgTemps.Count > 0 ? py12AvgTemps.Average() : 0;
        double prevYears12MoAvgHigh = py12AvgHighs.Count > 0 ? py12AvgHighs.Average() : 0;
        double prevYears12MoAvgLow = py12AvgLows.Count > 0 ? py12AvgLows.Average() : 0;
        double prevYears12MoAvgGust = py12AvgGusts.Count > 0 ? py12AvgGusts.Average() : 0;
        double prevYears12MoAvgPeakGust = py12PeakGusts.Count > 0 ? py12PeakGusts.Average() : 0;
        double prevYears12MoAvgRain = py12TotalRains.Count > 0 ? py12TotalRains.Average() : 0;
        double prevYears12MoAvgRainDays = py12RainDays.Count > 0 ? py12RainDays.Average() : 0;
        double prevYears12MoAvgStrikes = py12TotalStrikes.Count > 0 ? py12TotalStrikes.Average() : 0;
        double prevYears12MoAvgStrikeDays = py12StrikeDays.Count > 0 ? py12StrikeDays.Average() : 0;
        double prevYears12MoAvgSolar = py12AvgSolar.Count > 0 ? py12AvgSolar.Average() : 0;

        double delta12AvgTemp = thisYear12MoAvgTemp - prevYears12MoAvgTemp;
        double delta12AvgHigh = thisYear12MoAvgHigh - prevYears12MoAvgHigh;
        double delta12AvgLow = thisYear12MoAvgLow - prevYears12MoAvgLow;
        double delta12Gust = thisYear12MoAvgGust - prevYears12MoAvgGust;
        double delta12PeakGust = thisYear12MoPeakGust - prevYears12MoAvgPeakGust;
        double delta12Rain = thisYear12MoTotalRain - prevYears12MoAvgRain;
        double delta12RainDays = thisYear12MoRainDays - prevYears12MoAvgRainDays;
        double delta12Strikes = thisYear12MoTotalStrikes - prevYears12MoAvgStrikes;
        double delta12StrikeDays = thisYear12MoStrikeDays - prevYears12MoAvgStrikeDays;
        double delta12Solar = thisYear12MoAvgSolar - prevYears12MoAvgSolar;

        TwelveMonthTabAvgTempThisYearText = $"Avg Temp: {thisYear12MoAvgTemp:F1}°F";
        TwelveMonthTabAvgTempDeltaText = $"{(delta12AvgTemp >= 0 ? "+" : "")}{delta12AvgTemp:F1}°F vs {prevYears12MoAvgTemp:F1}°F prev yrs avg";
        TwelveMonthTabAvgTempDeltaBrush = delta12AvgTemp >= 0
            ? new SolidColorBrush(Color.FromArgb(255, 220, 38, 38))
            : new SolidColorBrush(Color.FromArgb(255, 22, 163, 74));

        TwelveMonthTabAvgHighThisYearText = $"Avg Max: {thisYear12MoAvgHigh:F1}°F";
        TwelveMonthTabAvgHighDeltaText = $"{(delta12AvgHigh >= 0 ? "+" : "")}{delta12AvgHigh:F1}°F vs {prevYears12MoAvgHigh:F1}°F prev yrs avg";
        TwelveMonthTabAvgHighDeltaBrush = delta12AvgHigh >= 0
            ? new SolidColorBrush(Color.FromArgb(255, 220, 38, 38))
            : new SolidColorBrush(Color.FromArgb(255, 22, 163, 74));
        TwelveMonthTabAvgLowThisYearText = $"Avg Min: {thisYear12MoAvgLow:F1}°F";
        TwelveMonthTabAvgLowDeltaText = $"{(delta12AvgLow >= 0 ? "+" : "")}{delta12AvgLow:F1}°F vs {prevYears12MoAvgLow:F1}°F prev yrs avg";
        TwelveMonthTabAvgLowDeltaBrush = delta12AvgLow >= 0
            ? new SolidColorBrush(Color.FromArgb(255, 220, 38, 38))
            : new SolidColorBrush(Color.FromArgb(255, 22, 163, 74));

        TwelveMonthTabWindThisYearText = $"Avg Daily Gust: {thisYear12MoAvgGust:F1} mph";
        TwelveMonthTabWindDeltaText = $"{(delta12Gust >= 0 ? "+" : "")}{delta12Gust:F1} mph vs {prevYears12MoAvgGust:F1} mph prev yrs avg";
        TwelveMonthTabWindDeltaBrush = delta12Gust >= 0
            ? new SolidColorBrush(Color.FromArgb(255, 220, 38, 38))
            : new SolidColorBrush(Color.FromArgb(255, 37, 99, 235));
        TwelveMonthTabWindPeakText = !string.IsNullOrEmpty(peakGustDate12Mo) ? $"Peak Gust: {thisYear12MoPeakGust:F1} mph ({peakGustDate12Mo})" : $"Peak Gust: {thisYear12MoPeakGust:F1} mph";
        TwelveMonthTabWindPeakDeltaText = $"{(delta12PeakGust >= 0 ? "+" : "")}{delta12PeakGust:F1} mph vs {prevYears12MoAvgPeakGust:F1} mph prev yrs peak";
        TwelveMonthTabWindPeakDeltaBrush = delta12PeakGust >= 0
            ? new SolidColorBrush(Color.FromArgb(255, 220, 38, 38))
            : new SolidColorBrush(Color.FromArgb(255, 37, 99, 235));

        TwelveMonthTabRainThisYearText = $"Total Rain: {thisYear12MoTotalRain:F2} in";
        TwelveMonthTabRainDeltaText = $"{(delta12Rain >= 0 ? "+" : "")}{delta12Rain:F2} in vs {prevYears12MoAvgRain:F2} in prev yrs avg";
        TwelveMonthTabRainDeltaBrush = delta12Rain >= 0
            ? new SolidColorBrush(Color.FromArgb(255, 5, 150, 105))
            : new SolidColorBrush(Color.FromArgb(255, 37, 99, 235));
        TwelveMonthTabRainDaysText = $"Rain Days: {thisYear12MoRainDays}";
        TwelveMonthTabRainDaysDeltaText = $"{(delta12RainDays >= 0 ? "+" : "")}{delta12RainDays:F1} vs {prevYears12MoAvgRainDays:F1} prev yrs avg";
        TwelveMonthTabRainDaysDeltaBrush = delta12RainDays >= 0
            ? new SolidColorBrush(Color.FromArgb(255, 5, 150, 105))
            : new SolidColorBrush(Color.FromArgb(255, 37, 99, 235));

        TwelveMonthTabLightningThisYearText = $"Total Strikes: {thisYear12MoTotalStrikes:N0}";
        TwelveMonthTabLightningDeltaText = $"{(delta12Strikes >= 0 ? "+" : "")}{delta12Strikes:N0} vs {prevYears12MoAvgStrikes:N0} prev yrs avg";
        TwelveMonthTabLightningDeltaBrush = delta12Strikes >= 0
            ? new SolidColorBrush(Color.FromArgb(255, 217, 119, 6))
            : new SolidColorBrush(Color.FromArgb(255, 37, 99, 235));
        TwelveMonthTabLightningDaysText = $"Days with Lightning: {thisYear12MoStrikeDays}";
        TwelveMonthTabLightningDaysDeltaText = $"{(delta12StrikeDays >= 0 ? "+" : "")}{delta12StrikeDays:F1} vs {prevYears12MoAvgStrikeDays:F1} prev yrs avg";
        TwelveMonthTabLightningDaysDeltaBrush = delta12StrikeDays >= 0
            ? new SolidColorBrush(Color.FromArgb(255, 217, 119, 6))
            : new SolidColorBrush(Color.FromArgb(255, 37, 99, 235));

        // Glance Overview (12-Month)
        Glance12MonthRainDaysText = $"{(delta12RainDays >= 0 ? "+" : "")}{delta12RainDays:F1} days";
        Glance12MonthRainDaysBrush = GetRainBrush(delta12RainDays);
        Glance12MonthRainAmountText = $"{(delta12Rain >= 0 ? "+" : "")}{delta12Rain:F2}\"";
        Glance12MonthRainAmountBrush = GetRainBrush(delta12Rain);

        Glance12MonthTempMaxText = $"{(delta12AvgHigh >= 0 ? "+" : "")}{delta12AvgHigh:F1}°";
        Glance12MonthTempMaxBrush = GetTempBrush(delta12AvgHigh);
        Glance12MonthTempMinText = $"{(delta12AvgLow >= 0 ? "+" : "")}{delta12AvgLow:F1}°";
        Glance12MonthTempMinBrush = GetTempBrush(delta12AvgLow);
        Glance12MonthTempAvgText = $"{(delta12AvgTemp >= 0 ? "+" : "")}{delta12AvgTemp:F1}°";
        Glance12MonthTempAvgBrush = GetTempBrush(delta12AvgTemp);

        Glance12MonthWindAvgText = $"{(delta12Gust >= 0 ? "+" : "")}{delta12Gust:F1} mph";
        Glance12MonthWindAvgBrush = GetWindBrush(delta12Gust);
        Glance12MonthWindPeakText = $"Peak {thisYear12MoPeakGust:F1} mph";

        Glance12MonthLightningStrikesText = $"{(delta12Strikes >= 0 ? "+" : "")}{delta12Strikes:N0} strikes";
        Glance12MonthLightningStrikesBrush = GetLightningBrush(delta12Strikes);
        Glance12MonthLightningDaysText = $"{(delta12StrikeDays >= 0 ? "+" : "")}{delta12StrikeDays:F1} days";
        Glance12MonthLightningDaysBrush = GetLightningBrush(delta12StrikeDays);

        Glance12MonthSolarAvgText = $"{(delta12Solar >= 0 ? "+" : "")}{delta12Solar:F0} W/m²";
        Glance12MonthSolarAvgBrush = GetSolarBrush(delta12Solar);

        // Yearly Breakdown Strings across all recorded calendar years (descending)
        var calTempRangeParts = new List<string>();
        var calTempAvgParts = new List<string>();
        var calRainParts = new List<string>();
        var calWindParts = new List<string>();
        var calLightningParts = new List<string>();
        var calSolarParts = new List<string>();

        var distinctYears = recordsWithDate.Select(x => x.Date.Year).Distinct().OrderByDescending(y => y).ToList();

        foreach (var yr in distinctYears)
        {
            var yRecs = recordsWithDate.Where(x => x.Date.Year == yr).ToList();
            if (yRecs.Count == 0) continue;

            var avgRecs = yRecs.Where(x => x.Record.TempAvgF.HasValue).Select(x => x.Record.TempAvgF!.Value).ToList();
            double yAvg = avgRecs.Count > 0 ? avgRecs.Average() : 0;

            var highRecs = yRecs.Where(x => x.Record.TempHighF.HasValue).Select(x => x.Record.TempHighF!.Value).ToList();
            double yHigh = highRecs.Count > 0 ? highRecs.Max() : 0;

            var lowRecs = yRecs.Where(x => x.Record.TempLowF.HasValue).Select(x => x.Record.TempLowF!.Value).ToList();
            double yLow = lowRecs.Count > 0 ? lowRecs.Min() : 0;

            double yRain = yRecs.Sum(x => x.Record.PrecipInches);
            int yRainDays = yRecs.Count(x => x.Record.PrecipInches > 0.001);

            var gustRecs = yRecs.Where(x => x.Record.WindGustMph.HasValue).Select(x => x.Record.WindGustMph!.Value).ToList();
            double yGust = gustRecs.Count > 0 ? gustRecs.Max() : 0;
            var windAvgRecs = yRecs.Where(x => x.Record.WindAvgMph.HasValue).Select(x => x.Record.WindAvgMph!.Value).ToList();
            double yWindAvg = windAvgRecs.Count > 0 ? windAvgRecs.Average() : 0;

            int yStrikes = yRecs.Sum(x => x.Record.LightningStrikes);
            int yStrikeDays = yRecs.Count(x => x.Record.LightningStrikes > 0);

            var solarRecs = yRecs.Where(x => x.Record.SolarRadiationAvg.HasValue).Select(x => x.Record.SolarRadiationAvg!.Value).ToList();
            double ySolar = solarRecs.Count > 0 ? solarRecs.Average() : 0;

            calTempAvgParts.Add($"{yr}: {yAvg:F1}°F");
            calTempRangeParts.Add($"{yr}: Max {yHigh:F1}° / Min {yLow:F1}°");
            calWindParts.Add($"{yr}: Peak {yGust:F1} mph (Avg {yWindAvg:F1} mph)");
            calRainParts.Add($"{yr}: {yRain:F2}\" ({yRainDays} days)");
            calLightningParts.Add($"{yr}: {yStrikes:N0} strikes ({yStrikeDays} days)");
            calSolarParts.Add($"{yr}: Avg {ySolar:F0} W/m²");
        }

        TwelveMonthTempRangeMonthly = string.Join("  |  ", calTempRangeParts);
        TwelveMonthTempAvgMonthly = string.Join("  |  ", calTempAvgParts);
        TwelveMonthRainMonthly = string.Join("  |  ", calRainParts);
        TwelveMonthWindMonthly = string.Join("  |  ", calWindParts);
        TwelveMonthLightningMonthly = string.Join("  |  ", calLightningParts);
        TwelveMonthSolarMonthly = string.Join("  |  ", calSolarParts);
    }

    private void BuildFullYearRainDaysTimeline(List<DailyWeatherRecord>? dailySummaries)
    {
        FullYearRainDaysTimeline.Clear();
        RainDaysSummaries.Clear();

        if (dailySummaries == null || dailySummaries.Count == 0) return;

        var dailyMap = dailySummaries.ToDictionary(d => d.Date, StringComparer.OrdinalIgnoreCase);

        DateTime latestDate = DateTime.MinValue;
        foreach (var d in dailySummaries)
        {
            if (DateTime.TryParse(d.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                if (dt > latestDate) latestDate = dt;
            }
        }

        int[] allYears = [2026, 2025, 2024, 2023];
        int[] prevYears = [2025, 2024, 2023];

        // 366-day leap year template (2024)
        DateTime leapRef = new DateTime(2024, 1, 1);
        for (int dayIdx = 0; dayIdx < 366; dayIdx++)
        {
            var dayDt = leapRef.AddDays(dayIdx);
            int m = dayDt.Month;
            int d = dayDt.Day;

            var point = new DayTimelinePoint
            {
                DayOffset = dayIdx,
                CalendarDay = $"{m:D2}-{d:D2}",
                DisplayDate = dayDt.ToString("MMM d", CultureInfo.InvariantCulture),
                DayOfWeek = dayDt.ToString("ddd", CultureInfo.InvariantCulture),
                IsUpcoming = false,
                IsToday = false
            };

            // This Year: 2026
            DateTime day2026;
            bool isValid2026 = false;
            try { day2026 = new DateTime(2026, m, d); isValid2026 = true; }
            catch { day2026 = default; }

            if (isValid2026)
            {
                if (day2026 <= latestDate)
                {
                    string key2026 = $"2026-{m:D2}-{d:D2}";
                    if (dailyMap.TryGetValue(key2026, out var rec2026))
                    {
                        point.ThisYearRainIn = rec2026.PrecipInches;
                        point.ThisYearRainMm = rec2026.PrecipMm;
                        point.ThisYearLightningStrikes = rec2026.LightningStrikes;
                        point.ThisYearLightningMinDistanceKm = rec2026.LightningMinDistanceKm;
                    }
                    else
                    {
                        point.ThisYearRainIn = 0.0;
                        point.ThisYearLightningStrikes = 0;
                    }
                }
                else
                {
                    point.ThisYearRainIn = null;
                    point.ThisYearLightningStrikes = null;
                }
            }
            else
            {
                point.ThisYearRainIn = null;
                point.ThisYearLightningStrikes = null;
            }

            // Previous Years
            foreach (var yr in prevYears)
            {
                DateTime dayYr;
                bool isValidYr = false;
                try { dayYr = new DateTime(yr, m, d); isValidYr = true; }
                catch { dayYr = default; }

                if (isValidYr)
                {
                    string yrKey = $"{yr:D4}-{m:D2}-{d:D2}";
                    if (dailyMap.TryGetValue(yrKey, out var yrRec))
                    {
                        point.PrevYears.Add(new YearPointData
                        {
                            Year = yr,
                            RainIn = yrRec.PrecipInches,
                            RainMm = yrRec.PrecipMm,
                            LightningStrikes = yrRec.LightningStrikes,
                            LightningMinDistanceKm = yrRec.LightningMinDistanceKm
                        });
                    }
                }
            }

            FullYearRainDaysTimeline.Add(point);
        }

        // Annual Summaries for 2026..2023
        foreach (var yr in allYears)
        {
            var yrRecs = dailySummaries
                .Where(x => x.Date.StartsWith(yr.ToString()))
                .ToList();

            if (yrRecs.Count > 0)
            {
                double totalRain = yrRecs.Sum(r => r.PrecipInches);
                int rainDays = yrRecs.Count(r => r.PrecipInches > 0.001);
                var wettest = yrRecs.OrderByDescending(r => r.PrecipInches).FirstOrDefault();

                int totalStrikes = yrRecs.Sum(r => r.LightningStrikes);
                int strikeDays = yrRecs.Count(r => r.LightningStrikes > 0);
                var mostActiveStrikes = yrRecs.OrderByDescending(r => r.LightningStrikes).FirstOrDefault();

                RainDaysSummaries.Add(new RainDaysYearSummary
                {
                    Year = yr,
                    DaysRecorded = yrRecs.Count,
                    TotalRainInches = totalRain,
                    RainDays = rainDays,
                    TotalLightningStrikes = totalStrikes,
                    LightningDays = strikeDays,
                    MaxDailyRainInches = wettest?.PrecipInches ?? 0,
                    MaxDailyRainDate = wettest != null && DateTime.TryParse(wettest.Date, out var wDt) ? wDt.ToString("MMM d") : wettest?.Date ?? "",
                    MaxDailyStrikes = mostActiveStrikes?.LightningStrikes ?? 0,
                    MaxDailyStrikesDate = mostActiveStrikes != null && DateTime.TryParse(mostActiveStrikes.Date, out var sDt) ? sDt.ToString("MMM d") : mostActiveStrikes?.Date ?? ""
                });
            }
        }
    }

    private void BuildRecordsRankings(List<DailyWeatherRecord>? dailySummaries)
    {
        Top10HottestDays.Clear();
        Top10ColdestDays.Clear();
        Top10HighestGusts.Clear();
        Top10RainiestDays.Clear();
        Top3DryStreaks.Clear();
        Top3WetStreaks.Clear();

        if (dailySummaries == null || dailySummaries.Count == 0) return;

        static string FormatDay(string dateStr)
        {
            if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
            return dateStr;
        }

        // 1. Top 10 Hottest Days
        var hottest = dailySummaries
            .Where(d => d.TempHighF.HasValue)
            .OrderByDescending(d => d.TempHighF!.Value)
            .ThenBy(d => d.Date)
            .Take(10);
        int rank = 1;
        foreach (var h in hottest)
        {
            Top10HottestDays.Add(new WeatherRecordRankItem
            {
                Rank = rank++,
                Date = h.Date,
                FormattedDate = FormatDay(h.Date),
                ValueDisplay = $"{h.TempHighF!.Value:F1}°F",
                SecondaryInfo = h.TempLowF.HasValue ? $"Low: {h.TempLowF.Value:F1}°F" : (h.TempAvgF.HasValue ? $"Avg: {h.TempAvgF.Value:F1}°F" : "")
            });
        }

        // 2. Top 10 Coldest Days
        var coldest = dailySummaries
            .Where(d => d.TempLowF.HasValue)
            .OrderBy(d => d.TempLowF!.Value)
            .ThenBy(d => d.Date)
            .Take(10);
        rank = 1;
        foreach (var c in coldest)
        {
            Top10ColdestDays.Add(new WeatherRecordRankItem
            {
                Rank = rank++,
                Date = c.Date,
                FormattedDate = FormatDay(c.Date),
                ValueDisplay = $"{c.TempLowF!.Value:F1}°F",
                SecondaryInfo = c.TempHighF.HasValue ? $"High: {c.TempHighF.Value:F1}°F" : (c.TempAvgF.HasValue ? $"Avg: {c.TempAvgF.Value:F1}°F" : "")
            });
        }

        // 3. Top 10 Highest Gusts
        var gusts = dailySummaries
            .Where(d => d.WindGustMph.HasValue && d.WindGustMph.Value > 0)
            .OrderByDescending(d => d.WindGustMph!.Value)
            .ThenBy(d => d.Date)
            .Take(10);
        rank = 1;
        foreach (var g in gusts)
        {
            Top10HighestGusts.Add(new WeatherRecordRankItem
            {
                Rank = rank++,
                Date = g.Date,
                FormattedDate = FormatDay(g.Date),
                ValueDisplay = $"{g.WindGustMph!.Value:F1} mph",
                SecondaryInfo = g.WindAvgMph.HasValue ? $"Avg: {g.WindAvgMph.Value:F1} mph" : ""
            });
        }

        // 4. Top 10 Rainiest Days
        var rainiest = dailySummaries
            .Where(d => d.PrecipInches > 0)
            .OrderByDescending(d => d.PrecipInches)
            .ThenBy(d => d.Date)
            .Take(10);
        rank = 1;
        foreach (var r in rainiest)
        {
            string sec = r.LightningStrikes > 0 ? $"{r.LightningStrikes} lightning strikes" : (r.TempHighF.HasValue ? $"High: {r.TempHighF.Value:F1}°F" : "");
            Top10RainiestDays.Add(new WeatherRecordRankItem
            {
                Rank = rank++,
                Date = r.Date,
                FormattedDate = FormatDay(r.Date),
                ValueDisplay = $"{r.PrecipInches:F2} in",
                SecondaryInfo = sec
            });
        }

        // 5. Streaks (Top 3 Dry and Top 3 Wet)
        var parsed = new List<(DateTime Date, DailyWeatherRecord Rec)>();
        foreach (var d in dailySummaries)
        {
            if (DateTime.TryParse(d.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                parsed.Add((dt, d));
            }
        }
        var chronological = parsed
            .GroupBy(p => p.Date)
            .Select(g => g.First())
            .OrderBy(p => p.Date)
            .ToList();

        var dryStreaks = new List<(DateTime Start, DateTime End, int Count)>();
        var wetStreaks = new List<(DateTime Start, DateTime End, int Count, double TotalPrecip, double MaxDailyPrecip)>();

        (DateTime Start, DateTime End, int Count)? curDry = null;
        (DateTime Start, DateTime End, int Count, double TotalPrecip, double MaxDailyPrecip)? curWet = null;

        for (int i = 0; i < chronological.Count; i++)
        {
            var (dt, rec) = chronological[i];
            bool isGap = i > 0 && dt != chronological[i - 1].Date.AddDays(1);
            bool isWet = rec.PrecipInches >= 0.01;
            bool isDry = !isWet;

            if (isGap)
            {
                if (curDry != null) { dryStreaks.Add(curDry.Value); curDry = null; }
                if (curWet != null) { wetStreaks.Add(curWet.Value); curWet = null; }
            }

            if (isDry)
            {
                if (curWet != null) { wetStreaks.Add(curWet.Value); curWet = null; }
                if (curDry == null)
                {
                    curDry = (dt, dt, 1);
                }
                else
                {
                    curDry = (curDry.Value.Start, dt, curDry.Value.Count + 1);
                }
            }
            else
            {
                if (curDry != null) { dryStreaks.Add(curDry.Value); curDry = null; }
                if (curWet == null)
                {
                    curWet = (dt, dt, 1, rec.PrecipInches, rec.PrecipInches);
                }
                else
                {
                    var prev = curWet.Value;
                    curWet = (prev.Start, dt, prev.Count + 1, prev.TotalPrecip + rec.PrecipInches, Math.Max(prev.MaxDailyPrecip, rec.PrecipInches));
                }
            }
        }

        if (curDry != null) dryStreaks.Add(curDry.Value);
        if (curWet != null) wetStreaks.Add(curWet.Value);

        // Top 3 Dry Streaks
        var topDry = dryStreaks
            .OrderByDescending(s => s.Count)
            .ThenByDescending(s => s.End)
            .Take(3);
        rank = 1;
        foreach (var s in topDry)
        {
            Top3DryStreaks.Add(new WeatherStreakRecord
            {
                Rank = rank++,
                StreakType = "Dry",
                DaysCount = s.Count,
                StartDate = s.Start.ToString("yyyy-MM-dd"),
                EndDate = s.End.ToString("yyyy-MM-dd"),
                DateRangeDisplay = $"{s.Start:MMM d, yyyy} – {s.End:MMM d, yyyy}",
                Details = "0.00 in precipitation recorded"
            });
        }

        // Top 3 Wet Streaks
        var topWet = wetStreaks
            .OrderByDescending(s => s.Count)
            .ThenByDescending(s => s.TotalPrecip)
            .ThenByDescending(s => s.End)
            .Take(3);
        rank = 1;
        foreach (var s in topWet)
        {
            Top3WetStreaks.Add(new WeatherStreakRecord
            {
                Rank = rank++,
                StreakType = "Wet",
                DaysCount = s.Count,
                StartDate = s.Start.ToString("yyyy-MM-dd"),
                EndDate = s.End.ToString("yyyy-MM-dd"),
                DateRangeDisplay = $"{s.Start:MMM d, yyyy} – {s.End:MMM d, yyyy}",
                Details = $"{s.TotalPrecip:F2} in total rain (max {s.MaxDailyPrecip:F2} in/day)"
            });
        }
    }
}

