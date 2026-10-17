using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using WaterDaze.Models;
using WaterDaze.Services;

namespace WaterDaze.ViewModels;

public class YearFacetItemViewModel : ViewModelBase
{
    private List<MonthlyFlowRecord> _monthlyRecords = [];
    private int _year;

    public List<MonthlyFlowRecord> MonthlyRecords
    {
        get => _monthlyRecords;
        set => SetProperty(ref _monthlyRecords, value);
    }

    public int Year
    {
        get => _year;
        set => SetProperty(ref _year, value);
    }
}

public class MainViewModel : ViewModelBase
{
    private readonly GaugeAnalyticsService _analyticsService;
    private readonly IUsgsService _usgsService;
    private CancellationTokenSource? _cts;

    private ObservableCollection<MonthSummaryRecord> _climatologySummaries = [];
    private GaugeDashboardData? _dashboardData;
    private string _errorMessage = string.Empty;
    private bool _hasError;
    private bool _hasWarning;
    private string _warningMessage = string.Empty;
    private string _dataSourceSummary = string.Empty;
    private bool _isLoading;
    private string _loadingStatus = string.Empty;
    private ObservableCollection<MonthlyFlowRecord> _monthlyRecords = [];
    private GaugeSite? _selectedSite;
    private ObservableCollection<int> _years = [];

    public MainViewModel(IUsgsService? usgsService = null, GaugeAnalyticsService? analyticsService = null)
    {
        _usgsService = usgsService ?? new UsgsService();
        _analyticsService = analyticsService ?? new GaugeAnalyticsService();

        AvailableSites = new ObservableCollection<GaugeSite>(GaugeSite.TucsonAreaSites);
        RefreshCommand = new RelayCommand(async () =>
        {
            if (SelectedSite != null) await LoadGaugeDataAsync(SelectedSite);
        });

        // Default select the first gauge (Rincon Creek -- 09485000)
        SelectedSite = AvailableSites.FirstOrDefault();
    }

    public ObservableCollection<GaugeSite> AvailableSites { get; }

    public ObservableCollection<MonthSummaryRecord> ClimatologySummaries
    {
        get => _climatologySummaries;
        set => SetProperty(ref _climatologySummaries, value);
    }

    public GaugeDashboardData? DashboardData
    {
        get => _dashboardData;
        private set => SetProperty(ref _dashboardData, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    public bool HasWarning
    {
        get => _hasWarning;
        set => SetProperty(ref _hasWarning, value);
    }

    public string WarningMessage
    {
        get => _warningMessage;
        set => SetProperty(ref _warningMessage, value);
    }

    public string DataSourceSummary
    {
        get => _dataSourceSummary;
        set => SetProperty(ref _dataSourceSummary, value);
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

    public ObservableCollection<MonthlyFlowRecord> MonthlyRecords
    {
        get => _monthlyRecords;
        set => SetProperty(ref _monthlyRecords, value);
    }

    public ICommand RefreshCommand { get; }

    public GaugeSite? SelectedSite
    {
        get => _selectedSite;
        set
        {
            if (SetProperty(ref _selectedSite, value) && value != null)
            {
                _ = LoadGaugeDataAsync(value);
            }
        }
    }

    public ObservableCollection<YearlyFlowRecord> TopDriestYears { get; } = [];
    public ObservableCollection<StreakRecord> TopDryStreaks { get; } = [];
    public ObservableCollection<DailyPeakFlowRecord> TopPeakFlowDays { get; } = [];
    public ObservableCollection<StreakRecord> TopWetStreaks { get; } = [];
    public ObservableCollection<YearlyFlowRecord> TopWettestYears { get; } = [];
    public ObservableCollection<YearFacetItemViewModel> YearFacets { get; } = [];

    public ObservableCollection<int> Years
    {
        get => _years;
        set => SetProperty(ref _years, value);
    }

    public async Task LoadGaugeDataAsync(GaugeSite site)
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
        }
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        IsLoading = true;
        HasError = false;
        HasWarning = false;
        ErrorMessage = string.Empty;
        WarningMessage = string.Empty;
        LoadingStatus = $"Fetching streamflow records for {site.DisplayTitle}...";

        try
        {
            var processed = await _analyticsService.LoadAndProcessGaugeDataAsync(site, _usgsService, token);

            if (token.IsCancellationRequested) return;

            DashboardData = processed;
            HasWarning = processed.HasWarning;
            WarningMessage = processed.WarningMessage;
            DataSourceSummary = processed.DataSourceSummary;

            // Update UI Collections
            UpdateCollections(processed);

            LoadingStatus = string.Empty;
        }
        catch (OperationCanceledException)
        {
            // Operation canceled
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Failed to load USGS streamflow data: {ex.Message}";
            DataSourceSummary = string.Empty;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateCollections(GaugeDashboardData data)
    {
        DataSourceSummary = data.DataSourceSummary;

        YearFacets.Clear();
        if (data is { Years: not null, MonthlyRecords: not null })
        {
            var grouped = data.MonthlyRecords.GroupBy(r => r.Year).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var year in data.Years)
            {
                grouped.TryGetValue(year, out var months);
                YearFacets.Add(new YearFacetItemViewModel
                {
                    Year = year,
                    MonthlyRecords = months ?? []
                });
            }
        }

        ClimatologySummaries = new ObservableCollection<MonthSummaryRecord>(data.ClimatologySummaries);
        MonthlyRecords = new ObservableCollection<MonthlyFlowRecord>(data.MonthlyRecords ?? []);
        Years = new ObservableCollection<int>(data.Years ?? []);

        TopWetStreaks.Clear();
        foreach (var item in data.TopWetStreaks) TopWetStreaks.Add(item);

        TopDryStreaks.Clear();
        foreach (var item in data.TopDryStreaks) TopDryStreaks.Add(item);

        TopWettestYears.Clear();
        foreach (var item in data.TopWettestYears) TopWettestYears.Add(item);

        TopDriestYears.Clear();
        foreach (var item in data.TopDriestYears) TopDriestYears.Add(item);

        TopPeakFlowDays.Clear();
        foreach (var item in data.TopPeakFlowDays) TopPeakFlowDays.Add(item);
    }
}