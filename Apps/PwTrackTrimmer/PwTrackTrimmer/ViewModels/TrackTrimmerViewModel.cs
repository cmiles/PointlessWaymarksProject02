using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using NetTopologySuite.Geometries;
using NetTopologySuite.Simplify;
using PwTrackTrimmer.Models;
using PwTrackTrimmer.Services;

namespace PwTrackTrimmer.ViewModels
{
    public class TrackTrimmerViewModel : INotifyPropertyChanged
    {
        private TrackDocument _document;
        private TrackPoint _selectedPoint;
        private int _trimStartIndex;
        private int _trimEndIndex;
        private string _statusMessage = "Ready";
        private bool _isLoading;

        // Windowed Paging for Right Panel (Default 250 points per page for optimal balance of speed and visibility)
        private int _currentPage = 1;
        private int _pageSize = 250;
        public ObservableCollection<TrackPoint> PagedPoints { get; } = new ObservableCollection<TrackPoint>();

        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (value >= 50 && SetProperty(ref _pageSize, value))
                {
                    CurrentPage = 1;
                    UpdatePagedPoints();
                    OnPropertyChanged(nameof(TotalPages));
                    OnPropertyChanged(nameof(PageInfoText));
                    ((RelayCommand)NextPageCommand)?.RaiseCanExecuteChanged();
                    ((RelayCommand)PrevPageCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        // Progress / Busy Overlay properties
        private bool _isBusyOverlayVisible;
        private string _busyTitle = "Processing...";
        private string _busyStatus = "Please wait while operation completes...";
        private bool _isLogWindowOpen;
        private bool _isDragOver;

        public ActivityLogService Logs => ActivityLogService.Instance;
        public UndoRedoManager UndoRedo { get; } = new UndoRedoManager();

        public event Action TrackLoaded;
        public event Action TrackDataChanged;
        public event Action SelectionChanged;
        public event Action TrimChanged;

        public TrackDocument Document
        {
            get => _document;
            set
            {
                if (SetProperty(ref _document, value))
                {
                    CurrentPage = 1;
                    SelectedPoint = _document?.Points != null && _document.Points.Count > 0 ? _document.Points[0] : null;
                    UpdatePagedPoints();
                    OnPropertyChanged(nameof(TotalPages));
                    OnPropertyChanged(nameof(PageInfoText));
                    ((RelayCommand)SimplifyTrackCommand)?.RaiseCanExecuteChanged();
                    ((RelayCommand)ConfirmDialogCommand)?.RaiseCanExecuteChanged();
                    ((RelayCommand)AdjustElevationsFromDemCommand)?.RaiseCanExecuteChanged();
                    ((RelayCommand)ReverseTrackCommand)?.RaiseCanExecuteChanged();
                    ((RelayCommand)AddEndToStartCommand)?.RaiseCanExecuteChanged();
                    ((RelayCommand)StandardizePaceCommand)?.RaiseCanExecuteChanged();
                    UpdateTrimCommands();
                }
            }
        }

        private bool _isUpdatingPaging;
        private int _selectionAnchorIndex = -1;
        private int _selectionStartIndex = -1;
        private int _selectionEndIndex = -1;

        public int SelectionAnchorIndex => _selectionAnchorIndex;
        public int SelectionStartIndex => _selectionStartIndex;
        public int SelectionEndIndex => _selectionEndIndex;

        public int SelectedPointCount => (_selectionStartIndex >= 0 && _selectionEndIndex >= _selectionStartIndex)
            ? (_selectionEndIndex - _selectionStartIndex + 1)
            : 0;

        public bool IsRangeSelected => SelectedPointCount > 1;

        public double SelectedRangeDistanceMiles
        {
            get
            {
                if (!IsRangeSelected || Document?.Points == null || _selectionStartIndex < 0 || _selectionEndIndex >= Document.Points.Count)
                    return 0;
                return (Document.Points[_selectionEndIndex].DistanceFromStart - Document.Points[_selectionStartIndex].DistanceFromStart) / 1609.344;
            }
        }

        public double SelectedRangeElevationDeltaFeet
        {
            get
            {
                if (!IsRangeSelected || Document?.Points == null || _selectionStartIndex < 0 || _selectionEndIndex >= Document.Points.Count)
                    return 0;
                var startEle = Document.Points[_selectionStartIndex].ElevationFeet ?? 0;
                var endEle = Document.Points[_selectionEndIndex].ElevationFeet ?? 0;
                return endEle - startEle;
            }
        }

        public string SelectionCardTitle => IsRangeSelected ? "SELECTED RANGE" : "SELECTED POINT";

        public string SelectionCardMainText
        {
            get
            {
                if (IsRangeSelected)
                {
                    return $"#{SelectionStartIndex + 1} - #{SelectionEndIndex + 1} ({SelectedPointCount} pts)";
                }
                if (SelectedPoint != null)
                {
                    return $"#{SelectedPoint.Index + 1}";
                }
                return "None";
            }
        }

        public string SelectionCardSubText
        {
            get
            {
                if (IsRangeSelected)
                {
                    string sign = SelectedRangeElevationDeltaFeet >= 0 ? "+" : "";
                    return $"{SelectedRangeDistanceMiles:F2} mi · {sign}{SelectedRangeElevationDeltaFeet:F0} ft";
                }
                if (SelectedPoint != null)
                {
                    return SelectedPoint.ElevationFormatted;
                }
                return "--";
            }
        }

        public string DeleteButtonText => IsRangeSelected
            ? $"Delete Selected ({SelectedPointCount})"
            : "Delete Selected";

        public TrackPoint SelectedPoint
        {
            get => _selectedPoint;
            set
            {
                if (value == null)
                {
                    if (_isUpdatingPaging) return;
                    if (_selectedPoint != null && Document?.Points != null && Document.Points.Contains(_selectedPoint) && !PagedPoints.Contains(_selectedPoint))
                    {
                        return;
                    }
                    ClearSelection();
                    return;
                }

                SelectPointOrRange(value.Index, isShift: false);
            }
        }

        public void SelectPointOrRange(int targetIndex, bool isShift)
        {
            if (Document?.Points == null || Document.Points.Count == 0) return;
            targetIndex = Math.Max(0, Math.Min(targetIndex, Document.Points.Count - 1));

            if (!isShift || _selectionAnchorIndex < 0 || _selectionAnchorIndex >= Document.Points.Count)
            {
                _selectionAnchorIndex = targetIndex;
                SetSelectionRange(targetIndex, targetIndex);

                // Auto-jump to page containing selected point only on single selection
                int targetPage = (targetIndex / PageSize) + 1;
                if (targetPage != CurrentPage && targetPage <= TotalPages)
                {
                    _isUpdatingPaging = true;
                    try
                    {
                        CurrentPage = targetPage;
                    }
                    finally
                    {
                        _isUpdatingPaging = false;
                    }
                }
            }
            else
            {
                // Range selection from anchor to target
                int start = Math.Min(_selectionAnchorIndex, targetIndex);
                int end = Math.Max(_selectionAnchorIndex, targetIndex);
                SetSelectionRange(start, end);
            }
        }

        public void SetSelectionRange(int start, int end)
        {
            if (Document?.Points == null || Document.Points.Count == 0) return;

            int oldStart = _selectionStartIndex;
            int oldEnd = _selectionEndIndex;

            _selectionStartIndex = start;
            _selectionEndIndex = end;

            // Unselect points outside new range
            if (oldStart >= 0 && oldEnd >= 0)
            {
                for (int i = oldStart; i <= oldEnd && i < Document.Points.Count; i++)
                {
                    if (i < start || i > end)
                    {
                        Document.Points[i].IsSelected = false;
                    }
                }
            }

            // Select points within new range
            for (int i = start; i <= end && i < Document.Points.Count; i++)
            {
                Document.Points[i].IsSelected = true;
            }

            _selectedPoint = Document.Points[start];

            NotifySelectionProperties();

            if (start == end)
            {
                Logs.Info($"Selected Point #{start + 1} ({Document.Points[start].ElevationFormatted}, {Document.Points[start].DistanceMilesFormatted})", "Selection");
            }
            else
            {
                int count = end - start + 1;
                double distMi = (Document.Points[end].DistanceFromStart - Document.Points[start].DistanceFromStart) / 1609.344;
                Logs.Info($"Selected Range #{start + 1} to #{end + 1} ({count} points, {distMi:F2} mi)", "Selection");
            }

            ((RelayCommand)DeleteSelectedCommand)?.RaiseCanExecuteChanged();
            ((RelayCommand)JumpToSelectedCommand)?.RaiseCanExecuteChanged();
            SelectionChanged?.Invoke();
        }

        public void ClearSelection()
        {
            if (Document?.Points != null && _selectionStartIndex >= 0 && _selectionEndIndex >= 0)
            {
                for (int i = _selectionStartIndex; i <= _selectionEndIndex && i < Document.Points.Count; i++)
                {
                    Document.Points[i].IsSelected = false;
                }
            }
            _selectionAnchorIndex = -1;
            _selectionStartIndex = -1;
            _selectionEndIndex = -1;
            _selectedPoint = null;

            NotifySelectionProperties();
            ((RelayCommand)DeleteSelectedCommand)?.RaiseCanExecuteChanged();
            ((RelayCommand)JumpToSelectedCommand)?.RaiseCanExecuteChanged();
            SelectionChanged?.Invoke();
        }

        private void NotifySelectionProperties()
        {
            OnPropertyChanged(nameof(SelectedPoint));
            OnPropertyChanged(nameof(SelectionAnchorIndex));
            OnPropertyChanged(nameof(SelectionStartIndex));
            OnPropertyChanged(nameof(SelectionEndIndex));
            OnPropertyChanged(nameof(SelectedPointCount));
            OnPropertyChanged(nameof(IsRangeSelected));
            OnPropertyChanged(nameof(SelectedRangeDistanceMiles));
            OnPropertyChanged(nameof(SelectedRangeElevationDeltaFeet));
            OnPropertyChanged(nameof(SelectionCardTitle));
            OnPropertyChanged(nameof(SelectionCardMainText));
            OnPropertyChanged(nameof(SelectionCardSubText));
            OnPropertyChanged(nameof(DeleteButtonText));
        }

        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                if (SetProperty(ref _currentPage, value))
                {
                    UpdatePagedPoints();
                    OnPropertyChanged(nameof(PageInfoText));
                    ((RelayCommand)NextPageCommand)?.RaiseCanExecuteChanged();
                    ((RelayCommand)PrevPageCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public int TotalPages => Document?.Points != null && Document.Points.Count > 0 
            ? (int)Math.Ceiling((double)Document.Points.Count / PageSize) 
            : 1;

        public string PageInfoText => $"Page {CurrentPage} of {TotalPages} ({Document?.Points.Count ?? 0} pts)";

        public int TrimStartIndex
        {
            get => _trimStartIndex;
            set
            {
                if (SetProperty(ref _trimStartIndex, value))
                {
                    UpdateTrimVisuals();
                    TrimChanged?.Invoke();
                    UpdateTrimCommands();
                }
            }
        }

        public int TrimEndIndex
        {
            get => _trimEndIndex;
            set
            {
                if (SetProperty(ref _trimEndIndex, value))
                {
                    UpdateTrimVisuals();
                    TrimChanged?.Invoke();
                    UpdateTrimCommands();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool IsBusyOverlayVisible
        {
            get => _isBusyOverlayVisible;
            set
            {
                if (SetProperty(ref _isBusyOverlayVisible, value))
                {
                    OnPropertyChanged(nameof(BusyOverlayVisibility));
                }
            }
        }

        public System.Windows.Visibility BusyOverlayVisibility => IsBusyOverlayVisible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public bool IsDragOver
        {
            get => _isDragOver;
            set
            {
                if (SetProperty(ref _isDragOver, value))
                {
                    OnPropertyChanged(nameof(DragOverlayVisibility));
                }
            }
        }

        public System.Windows.Visibility DragOverlayVisibility => IsDragOver ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public string BusyTitle
        {
            get => _busyTitle;
            set => SetProperty(ref _busyTitle, value);
        }

        public string BusyStatus
        {
            get => _busyStatus;
            set => SetProperty(ref _busyStatus, value);
        }

        public bool IsLogWindowOpen
        {
            get => _isLogWindowOpen;
            set
            {
                if (SetProperty(ref _isLogWindowOpen, value))
                {
                    OnPropertyChanged(nameof(LogWindowVisibility));
                }
            }
        }

        public System.Windows.Visibility LogWindowVisibility => IsLogWindowOpen ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        private bool _isAboutWindowOpen;
        public bool IsAboutWindowOpen
        {
            get => _isAboutWindowOpen;
            set
            {
                if (SetProperty(ref _isAboutWindowOpen, value))
                {
                    OnPropertyChanged(nameof(AboutWindowVisibility));
                }
            }
        }

        public System.Windows.Visibility AboutWindowVisibility => IsAboutWindowOpen ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        private bool _isToolsMenuOpen;
        public bool IsToolsMenuOpen
        {
            get => _isToolsMenuOpen;
            set
            {
                if (SetProperty(ref _isToolsMenuOpen, value))
                {
                    OnPropertyChanged(nameof(ToolsMenuVisibility));
                }
            }
        }

        public System.Windows.Visibility ToolsMenuVisibility => IsToolsMenuOpen ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public ICommand ToggleToolsMenuCommand { get; }
        public ICommand CloseToolsMenuCommand { get; }

        public ICommand LoadFileCommand { get; }
        public ICommand ExportGpxCommand { get; }
        public ICommand ExportTcxCommand { get; }
        public ICommand ExportFitCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand TrimTrackCommand { get; }
        public ICommand ResetTrimCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }
        public ICommand LoadSampleCommand { get; }
        public ICommand ReverseTrackCommand { get; }
        public ICommand AddEndToStartCommand { get; }
        public ICommand StandardizePaceCommand { get; }

        private double _standardizePaceMph = 2.0;
        public double StandardizePaceMph
        {
            get => _standardizePaceMph;
            set
            {
                if (value > 0.05 && SetProperty(ref _standardizePaceMph, Math.Round(value, 1)))
                {
                    OnPropertyChanged(nameof(StandardizePaceMphText));
                }
            }
        }

        public string StandardizePaceMphText
        {
            get => _standardizePaceMph.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            set
            {
                if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed) ||
                    double.TryParse(value, out parsed))
                {
                    if (parsed > 0.05)
                    {
                        StandardizePaceMph = parsed;
                    }
                }
            }
        }

        public ICommand ToggleLogWindowCommand { get; }
        public ICommand ClearLogsCommand { get; }
        public ICommand ToggleAboutWindowCommand { get; }
        public ICommand CloseAboutWindowCommand { get; }
        public ICommand DismissOverlayCommand { get; }
        public ICommand AdjustElevationsFromDemCommand { get; }

        public ICommand NextPageCommand { get; }
        public ICommand PrevPageCommand { get; }
        public ICommand JumpToSelectedCommand { get; }
        public ICommand SetPageSizeCommand { get; }

        // Track Simplification via NetTopologySuite Douglas-Peucker (tolerance in feet)
        private double _simplifyTolerance = 5.0;
        public double SimplifyTolerance
        {
            get => _simplifyTolerance;
            set
            {
                if (value > 0.1 && SetProperty(ref _simplifyTolerance, Math.Round(value, 1)))
                {
                    OnPropertyChanged(nameof(SimplifyToleranceText));
                }
            }
        }

        public string SimplifyToleranceText
        {
            get => _simplifyTolerance.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            set
            {
                if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed) ||
                    double.TryParse(value, out parsed))
                {
                    if (parsed > 0.1)
                    {
                        SimplifyTolerance = parsed;
                    }
                }
            }
        }

        public ICommand SimplifyTrackCommand { get; }
        public ICommand IncreaseToleranceCommand { get; }
        public ICommand DecreaseToleranceCommand { get; }

        // Host Dialog Mode (Process + File contract with external parent desktop app)
        public bool IsDialogMode => HostDialogService.IsDialogMode;
        public System.Windows.Visibility DialogModeVisibility => IsDialogMode ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        public System.Windows.Visibility StandaloneVisibility => !IsDialogMode ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public ICommand ConfirmDialogCommand { get; }
        public ICommand CancelDialogCommand { get; }

        public TrackTrimmerViewModel()
        {
            UndoRedo.StateChanged += (s, e) =>
            {
                ((RelayCommand)UndoCommand).RaiseCanExecuteChanged();
                ((RelayCommand)RedoCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ReverseTrackCommand)?.RaiseCanExecuteChanged();
                ((RelayCommand)AddEndToStartCommand)?.RaiseCanExecuteChanged();
                ((RelayCommand)StandardizePaceCommand)?.RaiseCanExecuteChanged();
                UpdateTrimCommands();
            };

            LoadFileCommand = new RelayCommand(OpenFile);
            ExportGpxCommand = new RelayCommand(() => ExportTrack(".gpx"));
            ExportTcxCommand = new RelayCommand(() => ExportTrack(".tcx"));
            ExportFitCommand = new RelayCommand(() => ExportTrack(".fit"));
            DeleteSelectedCommand = new RelayCommand(DeleteSelectedPoint, () => SelectedPoint != null || SelectionStartIndex >= 0);
            TrimTrackCommand = new RelayCommand(ApplyTrim, CanApplyTrim);
            ResetTrimCommand = new RelayCommand(ResetTrim, CanResetTrim);
            UndoCommand = new RelayCommand(PerformUndo, () => UndoRedo.CanUndo);
            RedoCommand = new RelayCommand(PerformRedo, () => UndoRedo.CanRedo);
            LoadSampleCommand = new RelayCommand(LoadSample);
            ReverseTrackCommand = new RelayCommand(ReverseTrack, () => Document?.Points != null && Document.Points.Count >= 2);
            AddEndToStartCommand = new RelayCommand(AddEndToStart, () => Document?.Points != null && Document.Points.Count >= 2);
            StandardizePaceCommand = new RelayCommand(StandardizePace, () => Document?.Points != null && Document.Points.Count >= 1);

            ToggleLogWindowCommand = new RelayCommand(() => IsLogWindowOpen = !IsLogWindowOpen);
            ClearLogsCommand = new RelayCommand(() => Logs.Clear());
            ToggleAboutWindowCommand = new RelayCommand(() => IsAboutWindowOpen = !IsAboutWindowOpen);
            CloseAboutWindowCommand = new RelayCommand(() => IsAboutWindowOpen = false);
            ToggleToolsMenuCommand = new RelayCommand(() => IsToolsMenuOpen = !IsToolsMenuOpen);
            CloseToolsMenuCommand = new RelayCommand(() => IsToolsMenuOpen = false);
            DismissOverlayCommand = new RelayCommand(() => IsBusyOverlayVisible = false);

            NextPageCommand = new RelayCommand(() => { if (CurrentPage < TotalPages) CurrentPage++; }, () => CurrentPage < TotalPages);
            PrevPageCommand = new RelayCommand(() => { if (CurrentPage > 1) CurrentPage--; }, () => CurrentPage > 1);
            JumpToSelectedCommand = new RelayCommand(JumpToSelectedPoint, () => SelectedPoint != null || SelectionStartIndex >= 0);
            SetPageSizeCommand = new RelayCommand(param =>
            {
                if (param != null && int.TryParse(param.ToString(), out int size) && size >= 50)
                {
                    PageSize = size;
                    Logs.Info($"Adjusted display to {size} points per page.", "UI");
                }
            });

            SimplifyTrackCommand = new RelayCommand(SimplifyTrack, () => Document?.Points != null && Document.Points.Count >= 3);
            IncreaseToleranceCommand = new RelayCommand(() => SimplifyTolerance = Math.Round(SimplifyTolerance + 1.0, 1));
            DecreaseToleranceCommand = new RelayCommand(() =>
            {
                if (SimplifyTolerance > 1.0)
                    SimplifyTolerance = Math.Round(SimplifyTolerance - 1.0, 1);
            });

            ConfirmDialogCommand = new RelayCommand(ConfirmDialog, () => Document?.Points != null && Document.Points.Count > 0);
            CancelDialogCommand = new RelayCommand(CancelDialog);
            AdjustElevationsFromDemCommand = new RelayCommand(AdjustElevationsFromDem, () => Document?.Points != null && Document.Points.Count > 0);

            if (!string.IsNullOrEmpty(HostDialogService.InputFilePath) && File.Exists(HostDialogService.InputFilePath))
            {
                if (HostDialogService.IsDialogMode)
                    Logs.Info($"Launched in Host Dialog Mode. Auto-loading: {HostDialogService.InputFilePath}", "Host");
                else
                    Logs.Info($"Launched with file argument. Auto-loading: {HostDialogService.InputFilePath}", "App");

                _ = LoadInitialHostFileAsync(HostDialogService.InputFilePath);
            }
            else
            {
                Logs.Info("Track Trimmer ViewModel initialized with windowed paging enabled.", "App");
                _ = LoadInitialTrackAsync();
            }
        }

        public void UpdatePagedPoints()
        {
            _isUpdatingPaging = true;
            try
            {
                PagedPoints.Clear();
                if (Document?.Points == null || Document.Points.Count == 0) return;

                int skip = Math.Max(0, (CurrentPage - 1) * PageSize);
                var slice = Document.Points.Skip(skip).Take(PageSize).ToList();

                foreach (var pt in slice)
                {
                    PagedPoints.Add(pt);
                }
            }
            finally
            {
                _isUpdatingPaging = false;
            }

            OnPropertyChanged(nameof(SelectedPoint));
        }

        private void JumpToSelectedPoint()
        {
            int targetIdx = SelectionStartIndex >= 0 ? SelectionStartIndex : (SelectedPoint?.Index ?? -1);
            if (targetIdx < 0) return;
            int page = (targetIdx / PageSize) + 1;
            if (page <= TotalPages)
            {
                CurrentPage = page;
            }
        }

        public void ShowBusy(string title, string status)
        {
            BusyTitle = title;
            BusyStatus = status;
            IsBusyOverlayVisible = true;
            Logs.Info($"[Task Started] {title} - {status}", "Task");
        }

        public async void HideBusy(int delayMs = 600)
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs);
            }
            IsBusyOverlayVisible = false;
        }

        public void LoadSample()
        {
            _ = LoadInitialTrackAsync();
        }

        public async Task LoadInitialTrackAsync()
        {
            ShowBusy("Loading Initial Track", "Loading 2015 October Plateau Point...");
            try
            {
                Document = await SampleTrackService.LoadInitialTrackAsync();
                UndoRedo.Clear();
                ResetTrim();
                CurrentPage = 1;
                UpdatePagedPoints();
                StatusMessage = $"Loaded initial track '{Document.Name}' ({Document.Points.Count:N0} points).";
                Logs.Success($"Loaded initial track '{Document.Name}' ({Document.Points.Count:N0} points, {Document.Statistics.DistanceFormatted}).", "App");
                NotifyAllPanels();
            }
            catch (Exception ex)
            {
                Logs.Error($"Failed to load initial track: {ex.Message}", "App");
                StatusMessage = $"Failed to load initial track: {ex.Message}";
            }
            finally
            {
                HideBusy(500);
            }
        }

        private async void OpenFile()
        {
            var dialog = new OpenSilver.Controls.OpenFileDialog
            {
                Filter = "Track Files (*.fit, *.tcx, *.gpx)|*.fit;*.tcx;*.gpx|GPX files (*.gpx)|*.gpx|TCX files (*.tcx)|*.tcx|FIT files (*.fit)|*.fit|All files (*.*)|*.*",
                Multiselect = false
            };

            bool? result = await dialog.ShowDialogAsync();
            if (result == true && dialog.File != null)
            {
                var file = dialog.File;
                await LoadFromStreamAsync(file.OpenRead(), file.Name);
            }
        }

        public async Task LoadFromStreamAsync(Stream stream, string fileName)
        {
            IsLoading = true;
            ShowBusy($"Loading {Path.GetFileName(fileName)}", $"Reading file stream and parsing track data...");
            StatusMessage = $"Loading {fileName}...";

            try
            {
                await Task.Delay(50); // Yield to allow overlay to render
                var service = TrackFileServiceFactory.GetService(fileName);
                var doc = await service.LoadAsync(stream, fileName);
                Document = doc;
                UndoRedo.Clear();
                ResetTrim();
                CurrentPage = 1;
                UpdatePagedPoints();
                StatusMessage = $"Successfully loaded {Path.GetFileName(fileName)} ({doc.Points.Count} points)";
                Logs.Success($"File loaded successfully: '{doc.Name}' with {doc.Points.Count} points.", "I/O");
                NotifyAllPanels();
                BusyStatus = "Finalizing map rendering and metrics...";
                HideBusy(800);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading {fileName}: {ex.Message}";
                Logs.Error($"Error loading {fileName}: {ex.Message}", "I/O");
                BusyStatus = $"Error: {ex.Message}";
                HideBusy(1500);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadInitialHostFileAsync(string filePath)
        {
            try
            {
                using var fs = File.OpenRead(filePath);
                await LoadFromStreamAsync(fs, filePath);
                Logs.Success($"Dialog track loaded from host: '{Document.Name}' ({Document.Points.Count} points).", "Host");
            }
            catch (Exception ex)
            {
                Logs.Error($"Failed to load host dialog track: {ex.Message}", "Host");
                StatusMessage = $"Error loading track from host: {ex.Message}";
            }
        }

        public async void ConfirmDialog()
        {
            if (Document == null || Document.Points == null || Document.Points.Count == 0) return;

            string outPath = HostDialogService.OutputFilePath;
            if (string.IsNullOrEmpty(outPath))
            {
                outPath = HostDialogService.InputFilePath;
            }

            if (string.IsNullOrEmpty(outPath))
            {
                Logs.Error("Cannot confirm: No output file path specified.", "Host");
                return;
            }

            ShowBusy("Confirming & Saving", $"Writing edited track to '{Path.GetFileName(outPath)}'...");
            await Task.Delay(50);

            try
            {
                string dir = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var service = TrackFileServiceFactory.GetService(outPath);
                using (var fs = new FileStream(outPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    await service.SaveAsync(Document, fs);
                }

                Logs.Success($"Successfully saved final edited track to '{outPath}'. Returning to calling program with confirmation.", "Host");
                HostDialogService.IsConfirmed = true;
                HostDialogService.ExitCode = 0;

                await Task.Delay(150);
                HostDialogService.OnConfirm?.Invoke();
            }
            catch (Exception ex)
            {
                Logs.Error($"Failed to save dialog output: {ex.Message}", "Host");
                StatusMessage = $"Error saving output: {ex.Message}";
                HideBusy(1200);
            }
        }

        public void CancelDialog()
        {
            Logs.Info("User cancelled dialog edit. Returning to calling program without saving.", "Host");
            HostDialogService.IsConfirmed = false;
            HostDialogService.ExitCode = 1;
            HostDialogService.OnCancel?.Invoke();
        }

        public async void ExportTrack(string extension)
        {
            if (Document == null || Document.Points.Count == 0)
            {
                StatusMessage = "No track data to export.";
                Logs.Warn("Export attempted with empty track.", "Export");
                return;
            }

            ShowBusy($"Exporting as {extension.ToUpperInvariant()}", $"Serializing track points into {extension} format...");

            try
            {
                await Task.Delay(50);
                string baseName = Path.GetFileNameWithoutExtension(Document.Name);
                if (string.IsNullOrWhiteSpace(baseName)) baseName = "exported_track";
                string outFileName = $"{baseName}{extension}";

                var service = TrackFileServiceFactory.GetService(outFileName);
                using var ms = new MemoryStream();
                await service.SaveAsync(Document, ms);
                byte[] data = ms.ToArray();

                string mimeType = extension switch
                {
                    ".fit" => "application/octet-stream",
                    ".tcx" => "application/vnd.garmin.tcx+xml",
                    ".gpx" => "application/gpx+xml",
                    _ => "application/octet-stream"
                };

                Logs.Info($"Triggering browser download for '{outFileName}' ({data.Length:N0} bytes)...", "Export");
                FileDownloadHelper.TriggerBrowserDownload(outFileName, data, mimeType);

                StatusMessage = $"Exported track as {outFileName} ({data.Length:N0} bytes)";
                Logs.Success($"Track successfully exported to {outFileName}.", "Export");
                BusyStatus = $"Export complete! ({data.Length:N0} bytes)";
                HideBusy(700);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
                Logs.Error($"Export failed: {ex.Message}", "Export");
                BusyStatus = $"Export error: {ex.Message}";
                HideBusy(1500);
            }
        }

        public void MovePoint(int index, double newLat, double newLon)
        {
            if (Document == null || index < 0 || index >= Document.Points.Count) return;
            var pt = Document.Points[index];
            double oldLat = pt.Latitude;
            double oldLon = pt.Longitude;

            var action = new MovePointAction(pt, oldLat, oldLon, newLat, newLon, () =>
            {
                Recalculate();
                TrackDataChanged?.Invoke();
            });

            UndoRedo.ExecuteAction(action);
            StatusMessage = $"Moved Point #{index + 1} to ({newLat:F5}, {newLon:F5})";
            Logs.Info($"Point #{index + 1} moved from ({oldLat:F5}, {oldLon:F5}) to ({newLat:F5}, {newLon:F5}).", "Edit");
        }

        public void DeletePointAt(int index)
        {
            if (Document == null || index < 0 || index >= Document.Points.Count) return;
            var pt = Document.Points[index];
            DeletePoints(new[] { pt });
        }

        public void DeleteSelectedPoint()
        {
            if (SelectionStartIndex >= 0 && SelectionEndIndex >= SelectionStartIndex && Document?.Points != null)
            {
                int count = SelectionEndIndex - SelectionStartIndex + 1;
                var pointsToDelete = Document.Points.Skip(SelectionStartIndex).Take(count).ToList();
                ClearSelection();
                DeletePoints(pointsToDelete);
            }
            else if (SelectedPoint != null)
            {
                var pt = SelectedPoint;
                ClearSelection();
                DeletePoints(new[] { pt });
            }
        }

        public void DeletePoints(IEnumerable<TrackPoint> pointsToDelete)
        {
            if (Document == null) return;
            var list = pointsToDelete.ToList();
            if (list.Count == 0) return;

            var action = new DeletePointsAction(Document.Points, list, () =>
            {
                Recalculate();
                UpdatePagedPoints();
                TrackDataChanged?.Invoke();
            });

            UndoRedo.ExecuteAction(action);
            ClearSelection();
            UpdatePagedPoints();
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(PageInfoText));
            StatusMessage = $"Deleted {list.Count} point(s).";
            Logs.Warn($"Deleted {list.Count} point(s) from track. Points remaining: {Document.Points.Count}.", "Edit");
        }

        public async void ApplyTrim()
        {
            if (!CanApplyTrim()) return;

            int trimStart = TrimStartIndex;
            int trimEnd = TrimEndIndex;

            ShowBusy("Trimming Track", $"Trimming points outside [{trimStart}..{trimEnd}]...");
            await Task.Delay(40);

            var removeStart = Document.Points.Take(trimStart).ToList();
            var removeEnd = Document.Points.Skip(trimEnd + 1).ToList();

            var action = new TrimTrackAction(Document.Points, removeStart, removeEnd, () =>
            {
                ResetTrim();
                Recalculate();
                CurrentPage = 1;
                UpdatePagedPoints();
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(PageInfoText));
                TrackDataChanged?.Invoke();
            });

            UndoRedo.ExecuteAction(action);
            CurrentPage = 1;
            UpdatePagedPoints();
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(PageInfoText));
            StatusMessage = $"Trimmed track to range [{trimStart}..{trimEnd}]. Removed {removeStart.Count + removeEnd.Count} points.";
            Logs.Success($"Trim applied: discarded {removeStart.Count} start points and {removeEnd.Count} end points. Active points: {Document.Points.Count}.", "Edit");
            HideBusy(500);
        }

        public bool CanApplyTrim()
        {
            if (Document == null || Document.Points.Count <= 1) return false;
            return (TrimStartIndex > 0 || TrimEndIndex < Document.Points.Count - 1) && TrimStartIndex < TrimEndIndex;
        }

        public bool CanResetTrim()
        {
            if (Document == null || Document.Points.Count == 0) return false;
            return TrimStartIndex > 0 || TrimEndIndex < Document.Points.Count - 1;
        }

        public void UpdateTrimCommands()
        {
            ((RelayCommand)TrimTrackCommand)?.RaiseCanExecuteChanged();
            ((RelayCommand)ResetTrimCommand)?.RaiseCanExecuteChanged();
        }

        public void ResetTrim()
        {
            _trimStartIndex = 0;
            _trimEndIndex = Document != null && Document.Points.Count > 0 ? Document.Points.Count - 1 : 0;
            OnPropertyChanged(nameof(TrimStartIndex));
            OnPropertyChanged(nameof(TrimEndIndex));
            UpdateTrimVisuals();
            TrimChanged?.Invoke();
            UpdateTrimCommands();
            Logs.Info($"Trim markers reset to full span [0 .. {_trimEndIndex}].", "Edit");
        }

        public void SetTrimStart(int index)
        {
            if (Document?.Points == null || Document.Points.Count <= 1) return;
            int clamped = Math.Clamp(index, 0, Document.Points.Count - 2);
            if (clamped >= TrimEndIndex)
            {
                TrimEndIndex = Document.Points.Count - 1;
                if (clamped >= TrimEndIndex)
                {
                    clamped = Math.Max(0, TrimEndIndex - 1);
                }
            }
            TrimStartIndex = clamped;
            StatusMessage = $"Trim start set to Point #{TrimStartIndex + 1}.";
            Logs.Info($"Trim start set to Point #{TrimStartIndex + 1}.", "Trim");
        }

        public void SetTrimEnd(int index)
        {
            if (Document?.Points == null || Document.Points.Count <= 1) return;
            int clamped = Math.Clamp(index, 1, Document.Points.Count - 1);
            if (clamped <= TrimStartIndex)
            {
                TrimStartIndex = 0;
                if (clamped <= TrimStartIndex)
                {
                    clamped = Math.Min(Document.Points.Count - 1, TrimStartIndex + 1);
                }
            }
            TrimEndIndex = clamped;
            StatusMessage = $"Trim end set to Point #{TrimEndIndex + 1}.";
            Logs.Info($"Trim end set to Point #{TrimEndIndex + 1}.", "Trim");
        }

        private void UpdateTrimVisuals()
        {
            if (Document == null) return;
            for (int i = 0; i < Document.Points.Count; i++)
            {
                Document.Points[i].IsTrimmed = (i < TrimStartIndex || i > TrimEndIndex);
            }
        }

        public void ReverseTrack()
        {
            if (Document?.Points == null || Document.Points.Count < 2) return;

            ShowBusy("Reversing Track", "Inverting track point order and sequencing timestamps...");
            var points = Document.Points;
            int n = points.Count;

            var originalPoints = points.Select(p => p.Clone()).ToList();
            var reversedPoints = points.Select(p => p.Clone()).Reverse().ToList();

            // Starting timestamp from earliest available point or default 8am
            DateTime baseTime = originalPoints.FirstOrDefault(p => p.Time.HasValue)?.Time ?? DateTime.Today.AddHours(8);
            reversedPoints[0].Time = baseTime;

            for (int k = 1; k < n; k++)
            {
                var origPrev = originalPoints[n - k];
                var origCurr = originalPoints[n - 1 - k];

                TimeSpan delta = TimeSpan.FromSeconds(1);
                if (origPrev.Time.HasValue && origCurr.Time.HasValue && origPrev.Time.Value > origCurr.Time.Value)
                {
                    delta = origPrev.Time.Value - origCurr.Time.Value;
                    if (delta <= TimeSpan.Zero)
                    {
                        delta = TimeSpan.FromSeconds(1);
                    }
                }
                else
                {
                    double distMeters = TrackStatisticsCalculator.CalculateDistanceMeters(
                        origPrev.Latitude, origPrev.Longitude, origCurr.Latitude, origCurr.Longitude);
                    double seconds = distMeters / 0.89408; // 2.0 mph
                    delta = TimeSpan.FromSeconds(Math.Max(1.0, seconds));
                }

                reversedPoints[k].Time = reversedPoints[k - 1].Time.Value.Add(delta);
            }

            var action = new ReplaceTrackPointsAction(Document.Points, originalPoints, reversedPoints, "Reverse Track", () =>
            {
                ResetTrim();
                Recalculate();
                CurrentPage = 1;
                SelectedPoint = Document.Points.Count > 0 ? Document.Points[0] : null;
                UpdatePagedPoints();
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(PageInfoText));
                TrackDataChanged?.Invoke();
            });

            UndoRedo.ExecuteAction(action);
            ResetTrim();
            Recalculate();
            CurrentPage = 1;
            SelectedPoint = Document.Points.Count > 0 ? Document.Points[0] : null;
            UpdatePagedPoints();
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(PageInfoText));
            TrackDataChanged?.Invoke();

            StatusMessage = $"Reversed track ({reversedPoints.Count} points) with earlier-to-later timestamps.";
            Logs.Success($"Reversed track sequence ({reversedPoints.Count} points). Timestamps correctly sequenced from {reversedPoints[0].Time:HH:mm:ss} to {reversedPoints[reversedPoints.Count - 1].Time:HH:mm:ss}.", "Edit");
            HideBusy(400);
        }

        public void AddEndToStart()
        {
            if (Document?.Points == null || Document.Points.Count < 2) return;

            ShowBusy("Adding End to Start", "Creating out-and-back return route from end back to start...");
            var points = Document.Points;
            int n = points.Count;

            var originalPoints = points.Select(p => p.Clone()).ToList();
            var reversedPoints = points.Select(p => p.Clone()).Reverse().ToList();

            DateTime lastTime = originalPoints[n - 1].Time ?? (originalPoints.FirstOrDefault(p => p.Time.HasValue)?.Time ?? DateTime.Today.AddHours(8));

            // Assign earlier-to-later timestamps to reversed points
            reversedPoints[0].Time = lastTime.AddSeconds(1);

            for (int k = 1; k < n; k++)
            {
                var origPrev = originalPoints[n - k];
                var origCurr = originalPoints[n - 1 - k];

                TimeSpan delta = TimeSpan.FromSeconds(1);
                if (origPrev.Time.HasValue && origCurr.Time.HasValue && origPrev.Time.Value > origCurr.Time.Value)
                {
                    delta = origPrev.Time.Value - origCurr.Time.Value;
                    if (delta <= TimeSpan.Zero)
                    {
                        delta = TimeSpan.FromSeconds(1);
                    }
                }
                else
                {
                    double distMeters = TrackStatisticsCalculator.CalculateDistanceMeters(
                        origPrev.Latitude, origPrev.Longitude, origCurr.Latitude, origCurr.Longitude);
                    double seconds = distMeters / 0.89408; // 2.0 mph
                    delta = TimeSpan.FromSeconds(Math.Max(1.0, seconds));
                }

                reversedPoints[k].Time = reversedPoints[k - 1].Time.Value.Add(delta);
            }

            // Newly created/synthesized points must always have null/empty extended raw data
            foreach (var rp in reversedPoints)
            {
                rp.RawData = null;
            }

            var combinedPoints = originalPoints.Select(p => p.Clone()).Concat(reversedPoints).ToList();

            var action = new ReplaceTrackPointsAction(Document.Points, originalPoints, combinedPoints, "Add End to Start", () =>
            {
                ResetTrim();
                Recalculate();
                CurrentPage = 1;
                SelectedPoint = Document.Points.Count > 0 ? Document.Points[0] : null;
                UpdatePagedPoints();
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(PageInfoText));
                TrackDataChanged?.Invoke();
            });

            UndoRedo.ExecuteAction(action);
            ResetTrim();
            Recalculate();
            CurrentPage = 1;
            SelectedPoint = Document.Points.Count > 0 ? Document.Points[0] : null;
            UpdatePagedPoints();
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(PageInfoText));
            TrackDataChanged?.Invoke();

            StatusMessage = $"Added End to Start: expanded from {originalPoints.Count} to {combinedPoints.Count} points.";
            Logs.Success($"Added End to Start: appended {reversedPoints.Count} reversed points (Total: {combinedPoints.Count} points, {Document.Statistics.DistanceFormatted}, {Document.Statistics.DurationFormatted}).", "Edit");
            HideBusy(400);
        }

        public void StandardizePace()
        {
            if (Document?.Points == null || Document.Points.Count == 0) return;

            double mph = StandardizePaceMph;
            if (mph <= 0.05)
            {
                mph = 2.0;
                StandardizePaceMph = 2.0;
            }

            ShowBusy("Standardizing Pace", $"Re-timing track timestamps to approximately {mph:F1} mph...");
            var points = Document.Points;
            int n = points.Count;

            var originalPoints = points.Select(p => p.Clone()).ToList();
            var newPoints = points.Select(p => p.Clone()).ToList();

            double speedMps = mph * 0.44704; // 1 mph = 0.44704 m/s
            DateTime current = originalPoints[0].Time ?? DateTime.Today.AddHours(8);
            newPoints[0].Time = current;

            for (int i = 1; i < n; i++)
            {
                double distMeters = TrackStatisticsCalculator.CalculateDistanceMeters(
                    newPoints[i - 1].Latitude, newPoints[i - 1].Longitude,
                    newPoints[i].Latitude, newPoints[i].Longitude);

                double seconds = distMeters / speedMps;
                // Minimum amount of time if there is no geographic movement
                if (seconds < 1.0)
                {
                    seconds = 1.0;
                }

                current = current.Add(TimeSpan.FromSeconds(seconds));
                newPoints[i].Time = current;
            }

            var action = new ReplaceTrackPointsAction(Document.Points, originalPoints, newPoints, $"Standardize Pace ({mph:F1} mph)", () =>
            {
                Recalculate();
                UpdatePagedPoints();
                TrackDataChanged?.Invoke();
            });

            UndoRedo.ExecuteAction(action);
            Recalculate();
            UpdatePagedPoints();
            TrackDataChanged?.Invoke();

            StatusMessage = $"Standardized pace across {newPoints.Count} points to {mph:F1} mph.";
            Logs.Success($"Standardized pace to {mph:F1} mph: total duration updated to {Document.Statistics.DurationFormatted} (Average speed: {Document.Statistics.AverageSpeedFormatted}).", "Pace");
            HideBusy(400);
        }

        public async void SimplifyTrack()
        {
            if (Document == null || Document.Points == null || Document.Points.Count < 3) return;
            if (SimplifyTolerance <= 0) return;

            int originalCount = Document.Points.Count;
            ShowBusy("Simplifying Track", $"Running NetTopologySuite Douglas-Peucker simplification (tolerance: {SimplifyTolerance:F1} ft)...");
            await Task.Delay(40);

            try
            {
                var points = Document.Points;
                double lat0 = points[0].Latitude;
                double lon0 = points[0].Longitude;
                double degToRad = Math.PI / 180.0;
                double R = 6378137.0; // Earth radius in meters
                double cosLat = Math.Cos(lat0 * degToRad);

                var coords = new Coordinate[originalCount];
                for (int i = 0; i < originalCount; i++)
                {
                    double x = R * (points[i].Longitude - lon0) * degToRad * cosLat;
                    double y = R * (points[i].Latitude - lat0) * degToRad;
                    coords[i] = new Coordinate(x, y);
                }

                var lineString = GeometryFactory.Default.CreateLineString(coords);
                double toleranceMeters = SimplifyTolerance * 0.3048;
                var simplifiedGeom = DouglasPeuckerSimplifier.Simplify(lineString, toleranceMeters);
                var resultCoords = simplifiedGeom.Coordinates;

                int originalIdx = 0;
                var keptPoints = new List<TrackPoint>();
                for (int i = 0; i < resultCoords.Length; i++)
                {
                    var sc = resultCoords[i];
                    while (originalIdx < originalCount)
                    {
                        double px = R * (points[originalIdx].Longitude - lon0) * degToRad * cosLat;
                        double py = R * (points[originalIdx].Latitude - lat0) * degToRad;
                        if (Math.Abs(px - sc.X) < 1e-4 && Math.Abs(py - sc.Y) < 1e-4)
                        {
                            keptPoints.Add(points[originalIdx]);
                            originalIdx++;
                            break;
                        }
                        originalIdx++;
                    }
                }

                if (keptPoints.Count == originalCount)
                {
                    StatusMessage = $"Simplification at {SimplifyTolerance:F1} ft did not eliminate any points.";
                    Logs.Info($"Douglas-Peucker simplification (tolerance: {SimplifyTolerance:F1} ft): all {originalCount} points within tolerance, no points removed.", "Simplify");
                    HideBusy(400);
                    return;
                }

                var originalList = points.ToList();
                var action = new SimplifyTrackAction(Document.Points, originalList, keptPoints, () =>
                {
                    ResetTrim();
                    Recalculate();
                    CurrentPage = 1;
                    SelectedPoint = Document.Points.Count > 0 ? Document.Points[0] : null;
                    UpdatePagedPoints();
                    OnPropertyChanged(nameof(TotalPages));
                    OnPropertyChanged(nameof(PageInfoText));
                    ((RelayCommand)SimplifyTrackCommand)?.RaiseCanExecuteChanged();
                    TrackDataChanged?.Invoke();
                });

                UndoRedo.ExecuteAction(action);
                ResetTrim();
                Recalculate();
                CurrentPage = 1;
                SelectedPoint = Document.Points.Count > 0 ? Document.Points[0] : null;
                UpdatePagedPoints();
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(PageInfoText));
                ((RelayCommand)SimplifyTrackCommand)?.RaiseCanExecuteChanged();
                TrackDataChanged?.Invoke();

                int removed = originalCount - keptPoints.Count;
                double pct = (double)removed / originalCount * 100.0;
                StatusMessage = $"Simplified track: {originalCount} ➔ {keptPoints.Count} points ({removed} removed, {pct:F1}% reduction).";
                Logs.Success($"Simplified track using NetTopologySuite Douglas-Peucker (tolerance: {SimplifyTolerance:F1} ft): reduced from {originalCount} to {keptPoints.Count} points ({removed} removed, {pct:F1}% reduction).", "Simplify");
                HideBusy(500);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Simplification failed: {ex.Message}";
                Logs.Error($"Track simplification error: {ex.Message}", "Simplify");
                HideBusy(1000);
            }
        }

        public async void AdjustElevationsFromDem()
        {
            if (Document == null || Document.Points == null || Document.Points.Count == 0)
            {
                StatusMessage = "No track data available to adjust elevations.";
                return;
            }

            ShowBusy("Adjusting Elevations from DEM", "Scanning track coordinates and identifying required DEM tiles...");
            await Task.Delay(50);

            try
            {
                var result = await ElevationService.AdjustElevationsAsync(Document, (title, status) =>
                {
                    BusyTitle = title;
                    BusyStatus = status;
                });

                if (!result.Success)
                {
                    Logs.Error(result.Message, "DEM");
                    StatusMessage = result.Message;
                    HideBusy(200);

                    try
                    {
                        System.Windows.MessageBox.Show(
                            result.Message,
                            "DEM Elevation Adjustment Failed",
                            System.Windows.MessageBoxButton.OK);
                    }
                    catch
                    {
                        // Fallback if dialog display is suppressed
                    }
                    return;
                }

                // Push to Undo/Redo stack
                var action = new AdjustElevationsAction(result.Changes, () =>
                {
                    Recalculate();
                    UpdatePagedPoints();
                    TrackDataChanged?.Invoke();
                });

                UndoRedo.ExecuteAction(action);
                Recalculate();
                UpdatePagedPoints();
                TrackDataChanged?.Invoke();

                StatusMessage = result.Message;
                Logs.Success(result.Message, "DEM");
                HideBusy(500);
            }
            catch (Exception ex)
            {
                Logs.Error($"Error adjusting elevations from DEM: {ex.Message}", "DEM");
                StatusMessage = $"DEM Error: {ex.Message}";
                HideBusy(200);
            }
        }

        private void PerformUndo()
        {
            UndoRedo.Undo();
            UpdatePagedPoints();
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(PageInfoText));
            Logs.Info("Undo performed.", "History");
        }

        private void PerformRedo()
        {
            UndoRedo.Redo();
            UpdatePagedPoints();
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(PageInfoText));
            Logs.Info("Redo performed.", "History");
        }

        public void Recalculate()
        {
            if (Document != null)
            {
                TrackStatisticsCalculator.RecalculateTrack(Document.Points, Document.Statistics);
            }
        }

        private void NotifyAllPanels()
        {
            Recalculate();
            UpdatePagedPoints();
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(PageInfoText));
            TrackLoaded?.Invoke();
            TrimChanged?.Invoke();
            SelectionChanged?.Invoke();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
