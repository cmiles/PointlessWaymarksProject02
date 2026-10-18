#nullable enable
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using PwImageMetadataViewer.Services;

namespace PwImageMetadataViewer.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
            : this(_ => execute(), canExecute != null ? _ => canExecute() : null)
        {
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public class UpdateViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

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

        private string _updateStatusMessage = "A newer version of Pointless Waymarks Image Metadata Viewer is available.";
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

        public ICommand CloseUpdateWindowCommand { get; }
        public ICommand StartUpdateCommand { get; }
        public ICommand CheckForUpdatesManualCommand { get; }

        public Action<string, bool>? OnStatusNotification { get; set; }

        public UpdateViewModel()
        {
            CloseUpdateWindowCommand = new RelayCommand(() =>
            {
                if (!IsDownloadingUpdate)
                {
                    IsUpdateWindowOpen = false;
                }
            });
            StartUpdateCommand = new RelayCommand(async () => await StartUpdateAsync());
            CheckForUpdatesManualCommand = new RelayCommand(async () => await CheckForUpdatesAsync(silent: false));
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
                    UpdateStatusMessage = "A newer version of Pointless Waymarks Image Metadata Viewer is available.";
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
                    string status = result.Message ?? "Image Metadata Viewer is up to date.";
                    if (!silent)
                    {
                        OnStatusNotification?.Invoke(status, false);
                    }
                    System.Diagnostics.Debug.WriteLine($"[Update] {status}");
                }
            }
            catch (Exception ex)
            {
                string errMsg = $"Update check failed: {ex.Message}";
                if (!silent)
                {
                    OnStatusNotification?.Invoke(errMsg, true);
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

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
