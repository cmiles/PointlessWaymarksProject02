using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using OpenSilver;
using PointedHorizons.Services;
using PointedHorizons.ViewModels;

namespace PointedHorizons.Controls;

public partial class FullScreenMapControl : UserControl
{
    private bool _isInitialized;
    private bool _isTrackingActive;
    private bool _isRecordingActive;
    private DateTime? _recordingStartTime;
    private DateTime _lastPointRecordedTime = DateTime.MinValue;
    private readonly List<GeoPosition> _recordedPoints = new();
    private double _totalRecordedDistanceKm;
    private DispatcherTimer? _recordingTimer;
    private string _currentBaseLayer = "topo";
    private RecordedTrack? _currentTrack;

    public event EventHandler? ConfigurePmTilesRequested;

    private double _lastPointerLat = 32.2226;
    private double _lastPointerLon = -110.9747;

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public FullScreenMapControl()
    {
        InitializeComponent();
        Loaded += FullScreenMapControl_Loaded;
        SizeChanged += FullScreenMapControl_SizeChanged;

        ThemeManager.Current.ThemeChanged += (s, e) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                UpdateTheme(ThemeManager.Current.IsDark);
            });
        };

        TrackStorageService.TracksUpdated += (s, e) =>
        {
            Dispatcher.BeginInvoke(async () =>
            {
                await RefreshSavedTracksSummaryAsync();
            });
        };

        OfflineDataService.ConfigChanged += (s, e) =>
        {
            Dispatcher.BeginInvoke(UpdatePmTilesLayers);
        };

        LeafletScrollIsolationHelper.ProtectControl(this, MapContainer);
    }

    private void FullScreenMapControl_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateTheme(ThemeManager.Current.IsDark);
        InitializeMap();
        _ = CheckCrashRecoveryAndRefreshTracksAsync();
    }

    private void FullScreenMapControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ResizeMap();
    }

    public void ResizeMap()
    {
        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.resize) {
                    window.fullScreenMap.resize();
                }
            ");
        }
        catch { }
    }

    private void InitializeMap()
    {
        if (_isInitialized) return;

        object div = Interop.GetDiv(MapContainer);
        if (div == null)
        {
            Dispatcher.BeginInvoke(InitializeMap);
            return;
        }

        Action<double, double> onPointerMoved = (lat, lon) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                _lastPointerLat = lat;
                _lastPointerLon = lon;
                if (TxtPointerCoords != null)
                {
                    TxtPointerCoords.Text = $"📍 {lat:F4}°, {lon:F4}°";
                }
            });
        };

        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                (function(container, initLat, initLon, movedCb) {
                    function tryInit() {
                        if (typeof window.fullScreenMap === 'undefined' || typeof window.maplibregl === 'undefined') {
                            setTimeout(tryInit, 50);
                            return;
                        }
                        window.fullScreenMap.init(container, {
                            lat: initLat,
                            lon: initLon,
                            zoom: 12
                        }, function(lat, lon) {
                            if (movedCb) movedCb(lat, lon);
                        });
                    }
                    tryInit();
                })($0, $1, $2, $3);
            ", div, _lastPointerLat, _lastPointerLon, onPointerMoved);

            _isInitialized = true;
            UpdatePmTilesLayers();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FullScreenMapControl] Init error: {ex.Message}");
        }
    }

    private async Task CheckCrashRecoveryAndRefreshTracksAsync()
    {
        try
        {
            var recovered = await TrackStorageService.CheckAndRecoverCrashedTrackAsync();
            if (recovered != null)
            {
                if (CrashRecoveryNotice != null && TxtRecoveryMsg != null)
                {
                    CrashRecoveryNotice.Visibility = Visibility.Visible;
                    TxtRecoveryMsg.Text = $"⚠️ Restored '{recovered.Name}' ({recovered.Points.Count} pts) from previous session";
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FullScreenMapControl] CheckCrashRecovery error: {ex.Message}");
        }

        await RefreshSavedTracksSummaryAsync();
    }

    private async Task RefreshSavedTracksSummaryAsync()
    {
        try
        {
            var tracks = await TrackStorageService.GetTracksIndexAsync();
            int count = tracks.Count;

            if (BtnOpenTracksList != null)
            {
                BtnOpenTracksList.Content = $"📂 View Saved Tracks ({count})";
            }
            if (TxtTracksCount != null)
            {
                TxtTracksCount.Text = $"({count} saved)";
            }
            if (TxtTracksSummary != null)
            {
                TxtTracksSummary.Text = count == 0
                    ? "Auto-saves progress; crash resilient"
                    : $"{count} track{(count == 1 ? "" : "s")} saved in local storage";
            }
        }
        catch { }
    }

    public void UpdateTheme(bool isDark)
    {
        var tm = ThemeManager.Current;

        if (CoordsBadgeBorder != null)
        {
            CoordsBadgeBorder.Background = tm.MapOverlayBackground;
            CoordsBadgeBorder.BorderBrush = tm.MapOverlayBorder;
        }

        if (CurrentLocationBorder != null)
        {
            CurrentLocationBorder.Background = tm.MapOverlayBackground;
            CurrentLocationBorder.BorderBrush = tm.MapOverlayBorder;
        }

        if (CrashRecoveryNotice != null)
        {
            CrashRecoveryNotice.Background = isDark
                ? new SolidColorBrush(Color.FromArgb(230, 40, 25, 5))
                : new SolidColorBrush(Color.FromArgb(240, 254, 243, 199));
            CrashRecoveryNotice.BorderBrush = isDark
                ? new SolidColorBrush(Color.FromArgb(255, 180, 83, 9))
                : new SolidColorBrush(Color.FromArgb(255, 245, 158, 11));
        }

        if (TxtRecoveryMsg != null)
        {
            TxtRecoveryMsg.Foreground = isDark
                ? new SolidColorBrush(Color.FromArgb(255, 251, 191, 36))
                : new SolidColorBrush(Color.FromArgb(255, 180, 83, 9));
        }

        if (HamburgerPanel != null)
        {
            HamburgerPanel.Background = tm.HeaderBackground;
            HamburgerPanel.BorderBrush = tm.HeaderBorder;
        }

        if (TracksOverlayPanel != null)
        {
            TracksOverlayPanel.Background = tm.HeaderBackground;
            TracksOverlayPanel.BorderBrush = tm.HeaderBorder;
        }

        if (MenuDivider1 != null) MenuDivider1.Background = tm.HeaderBorder;
        if (MenuDividerPower != null) MenuDividerPower.Background = tm.HeaderBorder;
        if (MenuDivider2 != null) MenuDivider2.Background = tm.HeaderBorder;
        if (MenuDividerTracks != null) MenuDividerTracks.Background = tm.HeaderBorder;
        if (MenuDivider3 != null) MenuDivider3.Background = tm.HeaderBorder;
        if (TracksDivider != null) TracksDivider.Background = tm.HeaderBorder;
        if (BadgeDivider != null) BadgeDivider.Background = tm.HeaderBorder;

        if (TxtPointerCoords != null) TxtPointerCoords.Foreground = tm.TextPrimary;
        if (TxtGpsIndicator != null) TxtGpsIndicator.Foreground = _isTrackingActive ? tm.Accent : tm.TextSecondary;
        if (TxtMenuTitle != null) TxtMenuTitle.Foreground = tm.TextPrimary;
        if (TxtTracksTitle != null) TxtTracksTitle.Foreground = tm.TextPrimary;
        if (TxtTracksCount != null) TxtTracksCount.Foreground = tm.TextSecondary;
        if (TxtNoTracks != null) TxtNoTracks.Foreground = tm.TextMuted;
        if (TxtSectionTrack != null) TxtSectionTrack.Foreground = tm.TextSecondary;
        if (TxtSectionPower != null) TxtSectionPower.Foreground = tm.TextSecondary;
        if (TxtSectionRecord != null) TxtSectionRecord.Foreground = tm.TextSecondary;
        if (TxtSectionHistory != null) TxtSectionHistory.Foreground = tm.TextSecondary;
        if (TxtSectionLayer != null) TxtSectionLayer.Foreground = tm.TextSecondary;
        if (TxtTrackingDetails != null) TxtTrackingDetails.Foreground = tm.TextMuted;
        if (TxtBatterySaverDetails != null) TxtBatterySaverDetails.Foreground = tm.TextMuted;
        if (TxtTracksSummary != null) TxtTracksSummary.Foreground = tm.TextMuted;
        if (TxtRecordingStats != null) TxtRecordingStats.Foreground = tm.TextPrimary;

        // Update button states
        UpdateTrackingButtonState();
        UpdateBatterySaverButtonState();
        UpdateRecordingButtonState();
        UpdateTracksSummaryButtonState();
        UpdateLayerButtons();
    }

    private void BtnHamburger_Click(object sender, RoutedEventArgs e)
    {
        if (HamburgerPanel != null)
        {
            HamburgerPanel.Visibility = HamburgerPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }

    private void BtnCloseMenu_Click(object sender, RoutedEventArgs e)
    {
        if (HamburgerPanel != null)
        {
            HamburgerPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnDismissRecovery_Click(object sender, RoutedEventArgs e)
    {
        if (CrashRecoveryNotice != null)
        {
            CrashRecoveryNotice.Visibility = Visibility.Collapsed;
        }
    }

    private async void BtnCurrentLocation_Click(object sender, RoutedEventArgs e)
    {
        if (TxtGpsIndicator != null) TxtGpsIndicator.Text = "Acquiring GPS...";

        var pos = await GeolocationService.GetCurrentPositionAsync(timeoutSeconds: 8);
        if (pos != null)
        {
            bool movePointer = !_isTrackingActive;

            try
            {
                Interop.ExecuteJavaScriptVoid(@"
                    if (window.fullScreenMap && window.fullScreenMap.centerOnCurrentLocation) {
                        window.fullScreenMap.centerOnCurrentLocation($0, $1, $2);
                    }
                ", pos.Latitude, pos.Longitude, movePointer);
            }
            catch { }

            if (movePointer)
            {
                _lastPointerLat = pos.Latitude;
                _lastPointerLon = pos.Longitude;
                if (TxtPointerCoords != null)
                {
                    TxtPointerCoords.Text = $"📍 {pos.Latitude:F4}°, {pos.Longitude:F4}°";
                }
            }

            if (TxtGpsIndicator != null)
            {
                TxtGpsIndicator.Text = $"GPS ±{pos.AccuracyM:F0}m";
                TxtGpsIndicator.Foreground = ThemeManager.Current.Accent;
            }
        }
        else
        {
            if (TxtGpsIndicator != null)
            {
                TxtGpsIndicator.Text = "GPS Unavailable";
                TxtGpsIndicator.Foreground = new SolidColorBrush(Colors.Red);
            }
        }
    }

    private void BtnToggleTracking_Click(object sender, RoutedEventArgs e)
    {
        if (!_isTrackingActive)
        {
            StartLiveTracking();
        }
        else
        {
            StopLiveTracking();
        }
    }

    private void StartLiveTracking()
    {
        _isTrackingActive = true;
        UpdateTrackingButtonState();

        if (TxtGpsIndicator != null)
        {
            TxtGpsIndicator.Text = "🟢 Tracking GPS";
            TxtGpsIndicator.Foreground = ThemeManager.Current.Accent;
        }

        GeolocationService.StartTracking(
            onUpdate: (pos) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        Interop.ExecuteJavaScriptVoid(@"
                            if (window.fullScreenMap && window.fullScreenMap.updateLivePosition) {
                                window.fullScreenMap.updateLivePosition($0, $1, $2, true);
                            }
                        ", pos.Latitude, pos.Longitude, pos.AccuracyM);
                    }
                    catch { }

                    if (TxtGpsIndicator != null)
                    {
                        if (pos.AccuracyM.HasValue && pos.AccuracyM.Value > 65.0)
                        {
                            TxtGpsIndicator.Text = $"🟡 Coarse Fix ±{pos.AccuracyM:F0}m";
                            TxtGpsIndicator.Foreground = new SolidColorBrush(Color.FromArgb(255, 234, 179, 8)); // Amber
                        }
                        else
                        {
                            TxtGpsIndicator.Text = $"🟢 GPS ±{pos.AccuracyM:F0}m";
                            TxtGpsIndicator.Foreground = ThemeManager.Current.Accent;
                        }
                    }

                    if (TxtTrackingDetails != null)
                    {
                        TxtTrackingDetails.Text = $"Lat: {pos.Latitude:F5}°, Lon: {pos.Longitude:F5}° (±{pos.AccuracyM:F0}m)";
                    }

                    // If recording is active, append track point
                    if (_isRecordingActive)
                    {
                        AppendRecordedPoint(pos);
                    }
                });
            },
            onError: (err) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (TxtGpsIndicator != null)
                    {
                        TxtGpsIndicator.Text = "GPS Lost";
                        TxtGpsIndicator.Foreground = new SolidColorBrush(Colors.Red);
                    }
                });
            }
        );
    }

    private void StopLiveTracking()
    {
        _isTrackingActive = false;
        GeolocationService.StopTracking();

        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.stopLiveTracking) {
                    window.fullScreenMap.stopLiveTracking();
                }
            ");
        }
        catch { }

        UpdateTrackingButtonState();

        if (TxtGpsIndicator != null)
        {
            TxtGpsIndicator.Text = "Tracking Stopped";
            TxtGpsIndicator.Foreground = ThemeManager.Current.TextSecondary;
        }
        if (TxtTrackingDetails != null)
        {
            TxtTrackingDetails.Text = "Follows GPS location live as you move";
        }
    }

    private void UpdateTrackingButtonState()
    {
        var tm = ThemeManager.Current;
        if (BtnToggleTracking == null) return;

        if (_isTrackingActive)
        {
            BtnToggleTracking.Content = "🛰️ Track Location: ON";
            BtnToggleTracking.Background = tm.ButtonActiveBackground;
            BtnToggleTracking.BorderBrush = tm.ButtonActiveBorder;
            BtnToggleTracking.Foreground = tm.ButtonActiveForeground;
        }
        else
        {
            BtnToggleTracking.Content = "🛰️ Track Location: OFF";
            BtnToggleTracking.Background = tm.ButtonInactiveBackground;
            BtnToggleTracking.BorderBrush = tm.ButtonInactiveBorder;
            BtnToggleTracking.Foreground = tm.ButtonInactiveForeground;
        }
    }

    private void BtnToggleBatterySaver_Click(object sender, RoutedEventArgs e)
    {
        GeolocationService.BatterySaverMode = !GeolocationService.BatterySaverMode;
        UpdateBatterySaverButtonState();

        // If tracking is active, restart with updated power/polling options
        if (_isTrackingActive)
        {
            StopLiveTracking();
            StartLiveTracking();
        }
    }

    private void UpdateBatterySaverButtonState()
    {
        var tm = ThemeManager.Current;
        if (BtnToggleBatterySaver == null) return;

        if (GeolocationService.BatterySaverMode)
        {
            BtnToggleBatterySaver.Content = "🔋 GPS Power Saver: ON";
            BtnToggleBatterySaver.Background = tm.ButtonActiveBackground;
            BtnToggleBatterySaver.BorderBrush = tm.ButtonActiveBorder;
            BtnToggleBatterySaver.Foreground = tm.ButtonActiveForeground;
            if (TxtBatterySaverDetails != null)
            {
                TxtBatterySaverDetails.Text = "Enforces satellite GPS fixes with power-efficient throttling (10s interval / 15m delta) and avoids cell/Wi-Fi.";
            }
        }
        else
        {
            BtnToggleBatterySaver.Content = "⚡ High-Frequency GPS: ON";
            BtnToggleBatterySaver.Background = tm.ButtonInactiveBackground;
            BtnToggleBatterySaver.BorderBrush = tm.ButtonInactiveBorder;
            BtnToggleBatterySaver.Foreground = tm.ButtonInactiveForeground;
            if (TxtBatterySaverDetails != null)
            {
                TxtBatterySaverDetails.Text = "Frequent satellite GPS fixes (5m delta) for maximum track detail (uses more battery). Discards cell/Wi-Fi fixes.";
            }
        }
    }

    private void UpdateTracksSummaryButtonState()
    {
        var tm = ThemeManager.Current;
        if (BtnOpenTracksList != null)
        {
            BtnOpenTracksList.Background = tm.ButtonInactiveBackground;
            BtnOpenTracksList.BorderBrush = tm.ButtonInactiveBorder;
            BtnOpenTracksList.Foreground = tm.ButtonInactiveForeground;
        }
    }

    private void BtnToggleRecording_Click(object sender, RoutedEventArgs e)
    {
        if (!_isRecordingActive)
        {
            StartRecording();
        }
        else
        {
            StopRecording();
        }
    }

    private async void StartRecording()
    {
        _isRecordingActive = true;
        _recordingStartTime = DateTime.UtcNow;
        _totalRecordedDistanceKm = 0;
        _recordedPoints.Clear();
        _lastPointRecordedTime = DateTime.MinValue;

        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.clearTrack) {
                    window.fullScreenMap.clearTrack();
                }
            ");
        }
        catch { }

        // Start new persistent session in TrackStorageService for crash resilience
        try
        {
            _currentTrack = await TrackStorageService.StartNewTrackAsync();
            await RefreshSavedTracksSummaryAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FullScreenMapControl] StartNewTrack error: {ex.Message}");
        }

        // Ensure live tracking is on so positions stream in
        if (!_isTrackingActive)
        {
            StartLiveTracking();
        }

        // Start elapsed timer
        _recordingTimer?.Stop();
        _recordingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _recordingTimer.Tick += (s, e) => UpdateRecordingDisplay();
        _recordingTimer.Start();

        UpdateRecordingButtonState();
        UpdateRecordingDisplay();
    }

    private async void StopRecording()
    {
        _isRecordingActive = false;
        _recordingTimer?.Stop();

        if (_currentTrack != null)
        {
            try
            {
                await TrackStorageService.FinishTrackAsync(_currentTrack);
                _currentTrack = null;
                await RefreshSavedTracksSummaryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FullScreenMapControl] FinishTrack error: {ex.Message}");
            }
        }

        UpdateRecordingButtonState();
        UpdateRecordingDisplay();
    }

    private async void AppendRecordedPoint(GeoPosition pos)
    {
        // Require genuine GPS fixes: reject coarse cell/Wi-Fi positioning (accuracy > 65m)
        if (pos.AccuracyM.HasValue && pos.AccuracyM.Value > 65.0)
        {
            return;
        }

        if (_recordedPoints.Count > 0)
        {
            var prev = _recordedPoints.Last();
            double d = DistanceKm(prev.Latitude, prev.Longitude, pos.Latitude, pos.Longitude);
            double timeSec = _lastPointRecordedTime != DateTime.MinValue
                ? (pos.Timestamp - _lastPointRecordedTime).TotalSeconds
                : 999;

            if (GeolocationService.BatterySaverMode)
            {
                // Battery Saver: 15m minimum distance threshold to filter out standing still & drift
                if (d < 0.015 && timeSec < 120) return;
                // Throttle to minimum 5s interval unless moved significantly (>30m)
                if (timeSec < 5.0 && d < 0.030) return;
            }
            else
            {
                // High Frequency mode: 5m threshold
                if (d < 0.005) return;
            }

            _totalRecordedDistanceKm += d;
        }

        _recordedPoints.Add(pos);
        _lastPointRecordedTime = pos.Timestamp;

        // Immediately save point to storage for crash resilience
        if (_currentTrack != null)
        {
            try
            {
                await TrackStorageService.AppendPointAsync(_currentTrack, pos.Latitude, pos.Longitude, pos.AltitudeM, pos.AccuracyM);
            }
            catch { }
        }

        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.addTrackPoint) {
                    window.fullScreenMap.addTrackPoint($0, $1);
                }
            ", pos.Latitude, pos.Longitude);
        }
        catch { }

        UpdateRecordingDisplay();
    }

    private void UpdateRecordingDisplay()
    {
        if (TxtRecordingStats == null) return;

        TimeSpan elapsed = _recordingStartTime.HasValue
            ? DateTime.UtcNow - _recordingStartTime.Value
            : TimeSpan.Zero;

        bool isMetric = ViewModel?.IsMetric ?? true;
        string distStr = isMetric
            ? $"{_totalRecordedDistanceKm:F2} km"
            : $"{_totalRecordedDistanceKm * 0.621371:F2} mi";

        string prefix = _isRecordingActive ? "🔴 " : "";
        TxtRecordingStats.Text = $"{prefix}Points: {_recordedPoints.Count} | {distStr} | {elapsed:mm\\:ss}";
    }

    private void UpdateRecordingButtonState()
    {
        var tm = ThemeManager.Current;
        if (BtnToggleRecording == null) return;

        if (_isRecordingActive)
        {
            BtnToggleRecording.Content = "⏹️ Stop Recording";
            BtnToggleRecording.Background = new SolidColorBrush(Color.FromArgb(255, 239, 68, 68)); // Red-500
            BtnToggleRecording.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 220, 38, 38));
            BtnToggleRecording.Foreground = new SolidColorBrush(Colors.White);
        }
        else
        {
            BtnToggleRecording.Content = "⏺️ Record Location";
            BtnToggleRecording.Background = tm.ButtonInactiveBackground;
            BtnToggleRecording.BorderBrush = tm.ButtonInactiveBorder;
            BtnToggleRecording.Foreground = tm.ButtonInactiveForeground;
        }
    }

    private void BtnClearTrack_Click(object sender, RoutedEventArgs e)
    {
        _recordedPoints.Clear();
        _totalRecordedDistanceKm = 0;
        _recordingStartTime = null;

        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.clearTrack) {
                    window.fullScreenMap.clearTrack();
                }
            ");
        }
        catch { }

        UpdateRecordingDisplay();
    }

    private void BtnExportGpx_Click(object sender, RoutedEventArgs e)
    {
        if (_recordedPoints.Count == 0) return;
        string gpx = GenerateGpx("Pointed Horizons GPS Track", _recordedPoints);
        DownloadFile($"track_{DateTime.Now:yyyyMMdd_HHmmss}.gpx", gpx, "application/gpx+xml");
    }

    private void BtnExportGeoJson_Click(object sender, RoutedEventArgs e)
    {
        if (_recordedPoints.Count == 0) return;
        string json = GenerateGeoJson("Pointed Horizons GPS Track", _recordedPoints);
        DownloadFile($"track_{DateTime.Now:yyyyMMdd_HHmmss}.geojson", json, "application/geo+json");
    }

    private static string GenerateGpx(string trackName, IEnumerable<GeoPosition> points)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<gpx version=\"1.1\" creator=\"PointedHorizons\" xmlns=\"http://www.topografix.com/GPX/1/1\">");
        sb.AppendLine("  <metadata>");
        sb.AppendLine($"    <time>{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</time>");
        sb.AppendLine("  </metadata>");
        sb.AppendLine("  <trk>");
        sb.AppendLine($"    <name>{System.Security.SecurityElement.Escape(trackName)}</name>");
        sb.AppendLine("    <trkseg>");
        foreach (var pt in points)
        {
            sb.Append($"      <trkpt lat=\"{pt.Latitude.ToString(CultureInfo.InvariantCulture)}\" lon=\"{pt.Longitude.ToString(CultureInfo.InvariantCulture)}\">");
            if (pt.AltitudeM.HasValue)
            {
                sb.Append($"<ele>{pt.AltitudeM.Value.ToString("F1", CultureInfo.InvariantCulture)}</ele>");
            }
            sb.Append($"<time>{pt.Timestamp:yyyy-MM-ddTHH:mm:ssZ}</time>");
            sb.AppendLine("</trkpt>");
        }
        sb.AppendLine("    </trkseg>");
        sb.AppendLine("  </trk>");
        sb.AppendLine("</gpx>");
        return sb.ToString();
    }

    private static string GenerateGeoJson(string trackName, IEnumerable<GeoPosition> points)
    {
        var ptList = points.ToList();
        var coords = string.Join(",", ptList.Select(p =>
            $"[{p.Longitude.ToString(CultureInfo.InvariantCulture)},{p.Latitude.ToString(CultureInfo.InvariantCulture)}]"));

        return $"{{\"type\":\"FeatureCollection\",\"features\":[{{\"type\":\"Feature\",\"geometry\":{{\"type\":\"LineString\",\"coordinates\":[{coords}]}},\"properties\":{{\"name\":{System.Text.Json.JsonSerializer.Serialize(trackName)},\"points\":{ptList.Count}}}}} ]}}";
    }

    private void DownloadFile(string filename, string content, string mimeType)
    {
        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                (function(name, content, mime) {
                    var blob = new Blob([content], { type: mime });
                    var url = URL.createObjectURL(blob);
                    var a = document.createElement('a');
                    a.href = url;
                    a.download = name;
                    document.body.appendChild(a);
                    a.click();
                    setTimeout(function() {
                        document.body.removeChild(a);
                        URL.revokeObjectURL(url);
                    }, 200);
                })($0, $1, $2);
            ", filename, content, mimeType);
        }
        catch { }
    }

    #region Saved Tracks Overlay & History

    private async void BtnOpenTracksList_Click(object sender, RoutedEventArgs e)
    {
        if (HamburgerPanel != null) HamburgerPanel.Visibility = Visibility.Collapsed;
        if (CrashRecoveryNotice != null) CrashRecoveryNotice.Visibility = Visibility.Collapsed;

        if (TracksOverlayBackdrop != null) TracksOverlayBackdrop.Visibility = Visibility.Visible;
        if (TracksOverlayPanel != null) TracksOverlayPanel.Visibility = Visibility.Visible;

        await LoadAndDisplayTracksListAsync();
    }

    private void BtnCloseTracksList_Click(object sender, RoutedEventArgs e)
    {
        if (TracksOverlayBackdrop != null) TracksOverlayBackdrop.Visibility = Visibility.Collapsed;
        if (TracksOverlayPanel != null) TracksOverlayPanel.Visibility = Visibility.Collapsed;
    }

    private async Task LoadAndDisplayTracksListAsync()
    {
        if (TracksListContainer == null) return;

        TracksListContainer.Children.Clear();

        var tracks = await TrackStorageService.GetTracksIndexAsync();

        if (TxtTracksCount != null)
        {
            TxtTracksCount.Text = $"({tracks.Count} saved)";
        }

        if (tracks.Count == 0)
        {
            if (TxtNoTracks == null)
            {
                TxtNoTracks = new TextBlock
                {
                    Text = "No recorded tracks yet. Start recording a track using the menu.",
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(8, 20, 8, 20),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
            }
            TxtNoTracks.Foreground = ThemeManager.Current.TextMuted;
            TracksListContainer.Children.Add(TxtNoTracks);
            return;
        }

        Style? pillStyle = Application.Current.Resources["PillButtonStyle"] as Style;

        foreach (var meta in tracks)
        {
            var card = CreateTrackItemCard(meta, pillStyle);
            TracksListContainer.Children.Add(card);
        }
    }

    private Border CreateTrackItemCard(TrackMetadata meta, Style? pillStyle)
    {
        var tm = ThemeManager.Current;
        bool isDark = tm.IsDark;

        var card = new Border
        {
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(10, 8, 10, 8),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Background = isDark ? ThemeManager.BrushDarkButtonBg : ThemeManager.BrushLightButtonBg,
            BorderBrush = tm.HeaderBorder
        };

        var stack = new StackPanel { Orientation = Orientation.Vertical };

        // Line 1: Header (Title + Status Badge)
        var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleBlock = new TextBlock
        {
            Text = meta.Name,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = tm.TextPrimary,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(titleBlock, 0);
        headerGrid.Children.Add(titleBlock);

        string badgeText = meta.IsActive ? "ACTIVE" : (meta.Name.Contains("(Recovered)") ? "RECOVERED" : "SAVED");
        var badgeBrush = meta.IsActive
            ? new SolidColorBrush(Color.FromArgb(255, 239, 68, 68))
            : (meta.Name.Contains("(Recovered)")
                ? new SolidColorBrush(Color.FromArgb(255, 245, 158, 11))
                : tm.Accent);

        var badgeBorder = new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = badgeBrush,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(5, 1, 5, 1),
            VerticalAlignment = VerticalAlignment.Center
        };
        var badgeTextblock = new TextBlock
        {
            Text = badgeText,
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = badgeBrush
        };
        badgeBorder.Child = badgeTextblock;
        Grid.SetColumn(badgeBorder, 1);
        headerGrid.Children.Add(badgeBorder);

        stack.Children.Add(headerGrid);

        // Line 2: Details (Date, duration, distance, points)
        bool isMetric = ViewModel?.IsMetric ?? true;
        string distStr = isMetric
            ? $"{meta.TotalDistanceKm:F2} km"
            : $"{meta.TotalDistanceKm * 0.621371:F2} mi";
        var dur = meta.Duration;
        string durStr = $"{(int)dur.TotalMinutes}m {dur.Seconds:D2}s";
        string timeStr = meta.StartTime.ToLocalTime().ToString("MMM d, yyyy h:mm tt");

        var detailsBlock = new TextBlock
        {
            Text = $"{timeStr} • {durStr} • {distStr} • {meta.PointCount} pts",
            FontSize = 10,
            Foreground = tm.TextSecondary,
            Margin = new Thickness(0, 0, 0, 6)
        };
        stack.Children.Add(detailsBlock);

        // Line 3: Actions row
        var actionsPanel = new StackPanel { Orientation = Orientation.Horizontal };

        Button CreateActionButton(string text, RoutedEventHandler clickHandler)
        {
            var btn = new Button
            {
                Content = text,
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 6, 0),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Background = tm.ButtonInactiveBackground,
                BorderBrush = tm.ButtonInactiveBorder,
                Foreground = tm.ButtonInactiveForeground
            };
            if (pillStyle != null) btn.Style = pillStyle;
            btn.Click += clickHandler;
            return btn;
        }

        var btnShow = CreateActionButton("👁️ Show", async (s, e) =>
        {
            await ShowTrackOnMapAsync(meta.Id);
        });

        var btnGpx = CreateActionButton("💾 GPX", async (s, e) =>
        {
            await ExportTrackGpxAsync(meta.Id);
        });

        var btnGeoJson = CreateActionButton("💾 GeoJSON", async (s, e) =>
        {
            await ExportTrackGeoJsonAsync(meta.Id);
        });

        var btnDelete = CreateActionButton("🗑️ Delete", async (s, e) =>
        {
            await TrackStorageService.DeleteTrackAsync(meta.Id);
            await LoadAndDisplayTracksListAsync();
            await RefreshSavedTracksSummaryAsync();
        });

        actionsPanel.Children.Add(btnShow);
        actionsPanel.Children.Add(btnGpx);
        actionsPanel.Children.Add(btnGeoJson);
        actionsPanel.Children.Add(btnDelete);

        stack.Children.Add(actionsPanel);
        card.Child = stack;

        return card;
    }

    private async Task ShowTrackOnMapAsync(string trackId)
    {
        var track = await TrackStorageService.GetTrackByIdAsync(trackId);
        if (track == null || track.Points.Count == 0) return;

        // Close modal
        BtnCloseTracksList_Click(this, new RoutedEventArgs());

        var coords = track.Points.Select(p => new double[] { p.Lon, p.Lat }).ToList();
        string json = System.Text.Json.JsonSerializer.Serialize(coords);

        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.displayLoadedTrack) {
                    window.fullScreenMap.displayLoadedTrack(JSON.parse($0));
                }
            ", json);
        }
        catch { }

        bool isMetric = ViewModel?.IsMetric ?? true;
        string distStr = isMetric
            ? $"{track.TotalDistanceKm:F2} km"
            : $"{track.TotalDistanceKm * 0.621371:F2} mi";

        if (TxtRecordingStats != null)
        {
            TxtRecordingStats.Text = $"Showing: {track.Name} | {track.Points.Count} pts | {distStr}";
        }
    }

    private async Task ExportTrackGpxAsync(string trackId)
    {
        var track = await TrackStorageService.GetTrackByIdAsync(trackId);
        if (track == null || track.Points.Count == 0) return;

        string gpx = GenerateGpx(track.Name, track.Points.Select(p => new GeoPosition(p.Lat, p.Lon, p.Ele, p.Acc, p.Time)));
        DownloadFile($"{SanitizeFilename(track.Name)}.gpx", gpx, "application/gpx+xml");
    }

    private async Task ExportTrackGeoJsonAsync(string trackId)
    {
        var track = await TrackStorageService.GetTrackByIdAsync(trackId);
        if (track == null || track.Points.Count == 0) return;

        string geojson = GenerateGeoJson(track.Name, track.Points.Select(p => new GeoPosition(p.Lat, p.Lon, p.Ele, p.Acc, p.Time)));
        DownloadFile($"{SanitizeFilename(track.Name)}.geojson", geojson, "application/geo+json");
    }

    private static string SanitizeFilename(string name)
    {
        var invalids = System.IO.Path.GetInvalidFileNameChars();
        var clean = string.Concat(name.Select(c => invalids.Contains(c) ? '_' : c)).Trim();
        return string.IsNullOrWhiteSpace(clean) ? "track" : clean;
    }

    #endregion

    private void BtnLayerTopo_Click(object sender, RoutedEventArgs e) => SetBaseLayer("topo");
    private void BtnLayerStreets_Click(object sender, RoutedEventArgs e) => SetBaseLayer("streets");
    private void BtnLayerSatellite_Click(object sender, RoutedEventArgs e) => SetBaseLayer("satellite");

    private void BtnLayerPmTiles_Click(object sender, RoutedEventArgs e)
    {
        var layers = OfflineDataService.GetConfiguredLayers();
        if (layers.Any(l => l.IsEnabled))
        {
            SetBaseLayer("pmtiles");
        }
        else
        {
            BtnConfigurePmTiles_Click(sender, e);
        }
    }

    private void BtnConfigurePmTiles_Click(object sender, RoutedEventArgs e)
    {
        if (HamburgerPanel != null) HamburgerPanel.Visibility = Visibility.Collapsed;
        ConfigurePmTilesRequested?.Invoke(this, EventArgs.Empty);
    }

    public void UpdatePmTilesLayers()
    {
        if (!_isInitialized) return;
        try
        {
            var payload = OfflineDataService.GetPmTilesConfigPayload();
            string json = System.Text.Json.JsonSerializer.Serialize(payload);
            Interop.ExecuteJavaScriptVoid("if (window.fullScreenMap && window.fullScreenMap.setPmTilesLayers) { window.fullScreenMap.setPmTilesLayers(JSON.parse($0)); }", json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FullScreenMapControl] UpdatePmTilesLayers error: {ex.Message}");
        }
    }

    public void SetBaseLayer(string layerName)
    {
        _currentBaseLayer = layerName;
        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.setBaseLayer) {
                    window.fullScreenMap.setBaseLayer($0);
                }
            ", layerName);
        }
        catch { }

        UpdateLayerButtons();
    }

    private void UpdateLayerButtons()
    {
        var tm = ThemeManager.Current;

        void StyleLayerBtn(Button btn, bool active)
        {
            if (btn == null) return;
            btn.Background = active ? tm.ButtonActiveBackground : tm.ButtonInactiveBackground;
            btn.BorderBrush = active ? tm.ButtonActiveBorder : tm.ButtonInactiveBorder;
            btn.Foreground = active ? tm.ButtonActiveForeground : tm.ButtonInactiveForeground;
            btn.FontWeight = active ? FontWeights.Bold : FontWeights.SemiBold;
        }

        StyleLayerBtn(BtnLayerTopo, _currentBaseLayer == "topo");
        StyleLayerBtn(BtnLayerStreets, _currentBaseLayer == "streets");
        StyleLayerBtn(BtnLayerSatellite, _currentBaseLayer == "satellite");
        StyleLayerBtn(BtnLayerPmTiles, _currentBaseLayer == "pmtiles");

        if (BtnConfigurePmTiles != null)
        {
            BtnConfigurePmTiles.Background = tm.ButtonInactiveBackground;
            BtnConfigurePmTiles.BorderBrush = tm.ButtonInactiveBorder;
            BtnConfigurePmTiles.Foreground = tm.Accent;
        }
    }

    private static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
}
