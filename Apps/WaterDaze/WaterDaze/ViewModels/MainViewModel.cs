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

        CloseUpdateWindowCommand = new RelayCommand(() =>
        {
            if (!IsDownloadingUpdate)
            {
                IsUpdateWindowOpen = false;
            }
        });
        StartUpdateCommand = new RelayCommand(async () => await StartUpdateAsync());
        CheckForUpdatesManualCommand = new RelayCommand(async () => await CheckForUpdatesAsync(silent: false));

        // Default select the first gauge (Rincon Creek -- 09485000)
        SelectedSite = AvailableSites.FirstOrDefault();
    }

    public ICommand CloseUpdateWindowCommand { get; }
    public ICommand StartUpdateCommand { get; }
    public ICommand CheckForUpdatesManualCommand { get; }

    // Application Updates
    private bool _isUpdateWindowOpen;
    public bool IsUpdateWindowOpen
    {
        get => _isUpdateWindowOpen;
        set
        {
            if (SetProperty(ref _isUpdateWindowOpen, value))
            {
                OnPropertyChanged(nameof(UpdateWindowVisibility));
            }
        }
    }

    public System.Windows.Visibility UpdateWindowVisibility => IsUpdateWindowOpen ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    private SoftwareReleaseEntry? _availableRelease;
    public SoftwareReleaseEntry? AvailableRelease
    {
        get => _availableRelease;
        set
        {
            if (SetProperty(ref _availableRelease, value))
            {
                ((RelayCommand)StartUpdateCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    private string _updateCurrentVersion = "Unknown";
    public string UpdateCurrentVersion
    {
        get => _updateCurrentVersion;
        set => SetProperty(ref _updateCurrentVersion, value);
    }

    private string _updateNewVersion = string.Empty;
    public string UpdateNewVersion
    {
        get => _updateNewVersion;
        set => SetProperty(ref _updateNewVersion, value);
    }

    private string _updateInstallerFileName = string.Empty;
    public string UpdateInstallerFileName
    {
        get => _updateInstallerFileName;
        set => SetProperty(ref _updateInstallerFileName, value);
    }

    private string _updateStatusMessage = "A newer version of Pointless Waymarks Water Daze is available.";
    public string UpdateStatusMessage
    {
        get => _updateStatusMessage;
        set => SetProperty(ref _updateStatusMessage, value);
    }

    private bool _isDownloadingUpdate;
    public bool IsDownloadingUpdate
    {
        get => _isDownloadingUpdate;
        set
        {
            if (SetProperty(ref _isDownloadingUpdate, value))
            {
                OnPropertyChanged(nameof(IsNotDownloadingUpdate));
                OnPropertyChanged(nameof(DownloadSectionVisibility));
                OnPropertyChanged(nameof(UpdateActionButtonText));
                ((RelayCommand)StartUpdateCommand)?.RaiseCanExecuteChanged();
                ((RelayCommand)CloseUpdateWindowCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotDownloadingUpdate => !IsDownloadingUpdate;
    public System.Windows.Visibility DownloadSectionVisibility => IsDownloadingUpdate ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    public string UpdateActionButtonText => IsDownloadingUpdate ? "Downloading..." : "Download & Install Now";

    private double _updateDownloadPercent;
    public double UpdateDownloadPercent
    {
        get => _updateDownloadPercent;
        set => SetProperty(ref _updateDownloadPercent, value);
    }

    private string _updateDownloadProgressText = "0%";
    public string UpdateDownloadProgressText
    {
        get => _updateDownloadProgressText;
        set => SetProperty(ref _updateDownloadProgressText, value);
    }

    private string _downloadOperationStatus = "Connecting...";
    public string DownloadOperationStatus
    {
        get => _downloadOperationStatus;
        set => SetProperty(ref _downloadOperationStatus, value);
    }

    private bool _isUpdateFailed;
    public bool IsUpdateFailed
    {
        get => _isUpdateFailed;
        set
        {
            if (SetProperty(ref _isUpdateFailed, value))
            {
                OnPropertyChanged(nameof(UpdateErrorVisibility));
            }
        }
    }

    public System.Windows.Visibility UpdateErrorVisibility => _isUpdateFailed ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    private string _updateErrorMessage = string.Empty;
    public string UpdateErrorMessage
    {
        get => _updateErrorMessage;
        set => SetProperty(ref _updateErrorMessage, value);
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

    public async Task CheckForUpdatesAsync(bool silent = true)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[Update] Checking online catalog for application updates...");
            var result = await AppUpdateService.CheckForUpdatesAsync();

            string probeSummary = result.ProbedDirectories.Count > 0
                ? string.Join("; ", result.ProbedDirectories)
                : "(none)";

            if (!string.IsNullOrEmpty(result.FoundVersionFilePath))
            {
                System.Diagnostics.Debug.WriteLine($"[Update] Probed [{probeSummary}] for pattern '{result.SearchPattern}'. Found installed version marker: '{result.FoundVersionFilePath}' (Version: {result.InstalledVersionString}, Date: {result.InstalledVersionDate:yyyy-MM-dd HH:mm}).");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[Update] Probed [{probeSummary}] for pattern '{result.SearchPattern}'. No matching version file found. Application running in development or unversioned standalone mode.");
            }

            if (result.HasUpdate && result.AvailableRelease != null)
            {
                AvailableRelease = result.AvailableRelease;
                UpdateCurrentVersion = result.InstalledVersionString ?? "Unknown";
                UpdateNewVersion = result.AvailableRelease.ApplicationVersion ?? "Latest";
                UpdateInstallerFileName = result.AvailableRelease.InstallerFileName ?? "Installer.exe";
                UpdateStatusMessage = "A newer version of Pointless Waymarks Water Daze is available.";
                IsUpdateFailed = false;
                UpdateErrorMessage = string.Empty;
                IsDownloadingUpdate = false;
                UpdateDownloadPercent = 0;
                UpdateDownloadProgressText = "0%";

                ((RelayCommand)StartUpdateCommand)?.RaiseCanExecuteChanged();
                ((RelayCommand)CloseUpdateWindowCommand)?.RaiseCanExecuteChanged();

                IsUpdateWindowOpen = true;
                System.Diagnostics.Debug.WriteLine($"[Update] Update available: {UpdateNewVersion} (Installed: {UpdateCurrentVersion}).");
            }
            else
            {
                string status = result.Message ?? "Water Daze is up to date.";
                if (!silent)
                {
                    LoadingStatus = status;
                }
                System.Diagnostics.Debug.WriteLine($"[Update] {status}");
            }
        }
        catch (Exception ex)
        {
            string errMsg = $"Update check failed: {ex.Message}";
            if (!silent)
            {
                LoadingStatus = errMsg;
            }
            System.Diagnostics.Debug.WriteLine($"[Update] Error: {errMsg}");
        }
    }

    public async Task StartUpdateAsync()
    {
        if (AvailableRelease == null || IsDownloadingUpdate) return;

        IsDownloadingUpdate = true;
        IsUpdateFailed = false;
        UpdateErrorMessage = string.Empty;
        UpdateDownloadPercent = 0;
        UpdateDownloadProgressText = "0%";
        DownloadOperationStatus = "Connecting to update server...";

        var progress = new Progress<(long bytesDownloaded, long totalBytes, double percent)>(p =>
        {
            UpdateDownloadPercent = Math.Round(p.percent, 1);
            if (p.totalBytes > 0)
            {
                UpdateDownloadProgressText = $"{p.bytesDownloaded / (1024.0 * 1024.0):F1} MB / {p.totalBytes / (1024.0 * 1024.0):F1} MB ({p.percent:F0}%)";
            }
            else
            {
                UpdateDownloadProgressText = $"{p.bytesDownloaded / (1024.0 * 1024.0):F1} MB downloaded";
            }
        });

        try
        {
            System.Diagnostics.Debug.WriteLine($"[Update] Downloading installer: {AvailableRelease.InstallerFileName}...");
            await AppUpdateService.DownloadAndExecuteInstallerAsync(
                AvailableRelease,
                progress,
                status => DownloadOperationStatus = status
            );
        }
        catch (Exception ex)
        {
            IsDownloadingUpdate = false;
            IsUpdateFailed = true;
            UpdateErrorMessage = ex.Message;
            System.Diagnostics.Debug.WriteLine($"[Update] Error during update installation: {ex.Message}");
        }
    }
}