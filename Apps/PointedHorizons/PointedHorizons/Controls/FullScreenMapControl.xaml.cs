using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using OpenSilver;
using PointedHorizons.Models;
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
    public event EventHandler? OpenTracksPageRequested;
    public event EventHandler? SearchLocationRequested;
    public event EventHandler<CelestialLocationEventArgs>? CelestialInfoRequested;

    private double _lastPointerLat = 32.2226;
    private double _lastPointerLon = -110.9747;
    private bool _hasPointerPlaced;
    private GeoPosition? _lastGpsPos;

    // Route Builder state
    private bool _isRouteDrawingActive;
    private bool _isRouteSnapToTrails = true;
    private double _currentRouteDistanceKm;
    private double _currentRouteClimbM;
    private double _currentRouteDescentM;
    private int _currentRoutePointCount;
    private int _currentRouteWaypointCount;

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public FullScreenMapControl()
    {
        InitializeComponent();
        Loaded += FullScreenMapControl_Loaded;
        SizeChanged += FullScreenMapControl_SizeChanged;
        DataContextChanged += FullScreenMapControl_DataContextChanged;

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

        TrackIndexService.TracksChanged += (s, e) =>
        {
            Dispatcher.BeginInvoke(UpdateImportedTracksOnMap);
        };

        GeoNamesService.ConfigurationChanged += (s, e) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (MenuSearchLocationSection != null)
                {
                    MenuSearchLocationSection.Visibility = GeoNamesService.IsConfigured ? Visibility.Visible : Visibility.Collapsed;
                }
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
        OfflineDataService.EnsureSettingsLoaded();

        if (!OfflineDataService.IsPmTilesSupported)
        {
            if (BtnLayerPmTiles != null) BtnLayerPmTiles.Visibility = Visibility.Collapsed;
            if (BtnConfigurePmTiles != null) BtnConfigurePmTiles.Visibility = Visibility.Collapsed;
            if (_currentBaseLayer == "pmtiles") _currentBaseLayer = "topo";
        }

        UpdateTheme(ThemeManager.Current.IsDark);
        InitializeMap();
        _ = CheckCrashRecoveryAndRefreshTracksAsync();
        UpdateResponsiveLayout(ActualWidth, ActualHeight);
    }

    private bool _isSmallScreen;

    private void FullScreenMapControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ResizeMap();
        UpdateResponsiveLayout(e.NewSize.Width, e.NewSize.Height);
    }

    public void UpdateResponsiveLayout(double width, double height)
    {
        if (width <= 0) width = ActualWidth;
        if (height <= 0) height = ActualHeight;
        if (width <= 0) return;

        bool isSmall = width < 768 || (height > 0 && height < 600);
        _isSmallScreen = isSmall;

        if (isSmall)
        {
            // On a small screen, move all buttons and info along top of map to hamburger,
            // leaving only Current Location and Reset
            if (CoordsBadgeBorder != null) CoordsBadgeBorder.Visibility = Visibility.Collapsed;
            if (BtnDrawRoute != null) BtnDrawRoute.Visibility = Visibility.Collapsed;
            if (BtnToggle3d != null) BtnToggle3d.Visibility = Visibility.Collapsed;

            if (BtnCurrentLocation != null)
            {
                BtnCurrentLocation.Visibility = Visibility.Visible;
                BtnCurrentLocation.Content = "📍 Location";
                BtnCurrentLocation.Padding = new Thickness(10, 6, 10, 6);
                BtnCurrentLocation.Margin = new Thickness(0, 0, 6, 0);
            }
            if (BtnResetView != null)
            {
                BtnResetView.Visibility = Visibility.Visible;
                BtnResetView.Content = "↺ Reset";
                BtnResetView.Padding = new Thickness(10, 6, 10, 6);
            }

            // Location & Elevation info is presented in hamburger menu
            if (MenuLocationInfoBorder != null)
            {
                MenuLocationInfoBorder.Visibility = Visibility.Visible;
            }
        }
        else
        {
            // Normal / Large screen: show top coords badge, draw route, current location, 3D toggle and reset
            if (CoordsBadgeBorder != null) CoordsBadgeBorder.Visibility = Visibility.Visible;
            if (BtnDrawRoute != null) BtnDrawRoute.Visibility = Visibility.Visible;
            if (BtnToggle3d != null) BtnToggle3d.Visibility = Visibility.Visible;

            if (BtnCurrentLocation != null)
            {
                BtnCurrentLocation.Visibility = Visibility.Visible;
                BtnCurrentLocation.Content = "📍 Current Location";
                BtnCurrentLocation.Padding = new Thickness(12, 6, 12, 6);
                BtnCurrentLocation.Margin = new Thickness(0, 0, 8, 0);
            }
            if (BtnResetView != null)
            {
                BtnResetView.Visibility = Visibility.Visible;
                BtnResetView.Content = "↺ Reset";
                BtnResetView.Padding = new Thickness(10, 6, 10, 6);
            }

            if (MenuLocationInfoBorder != null)
            {
                MenuLocationInfoBorder.Visibility = Visibility.Collapsed;
            }
        }
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

        OfflineDataService.EnsureSettingsLoaded();

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
                _hasPointerPlaced = true;
                _lastPointerLat = lat;
                _lastPointerLon = lon;
                string coordsText = $"📍 {lat:F4}°, {lon:F4}°";
                if (TxtPointerCoords != null)
                {
                    TxtPointerCoords.Text = coordsText;
                }
                if (TxtMenuPointerCoords != null)
                {
                    TxtMenuPointerCoords.Text = coordsText;
                }
                UpdateElevationDisplay(lat, lon);
            });
        };

        bool pmtilesSupported = OfflineDataService.IsPmTilesSupported;
        bool isMetric = ViewModel?.IsMetric ?? false;
        bool isDark = ThemeManager.Current.IsDark;

        Action<string> onTrackClicked = (trackId) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                System.Diagnostics.Debug.WriteLine($"[FullScreenMapControl] Map track clicked: {trackId}");
            });
        };

        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                (function(container, initLat, initLon, movedCb, trackCb, pmtilesSupported, isMetric, isDark) {
                    function tryInit() {
                        if (typeof window.fullScreenMap === 'undefined' || typeof window.maplibregl === 'undefined') {
                            setTimeout(tryInit, 50);
                            return;
                        }
                        window.fullScreenMap.isPmTilesSupported = pmtilesSupported;
                        window.fullScreenMap.init(container, {
                            lat: initLat,
                            lon: initLon,
                            zoom: 12,
                            isMetric: isMetric,
                            isDark: isDark
                        }, function(lat, lon) {
                            if (movedCb) movedCb(lat, lon);
                        }, function(trackId) {
                            if (trackCb) trackCb(trackId);
                        });
                    }
                    tryInit();
                })($0, $1, $2, $3, $4, $5, $6, $7);
            ", div, _lastPointerLat, _lastPointerLon, onPointerMoved, onTrackClicked, pmtilesSupported, isMetric, isDark);

            _isInitialized = true;
            UpdateElevationDisplay(_lastPointerLat, _lastPointerLon);
            if (pmtilesSupported)
            {
                UpdatePmTilesLayers();
            }
            UpdateImportedTracksOnMap();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FullScreenMapControl] Init error: {ex.Message}");
        }
    }

    public void UpdateImportedTracksOnMap()
    {
        if (!_isInitialized) return;
        try
        {
            string tracksJson = TrackIndexService.GetMapTracksJson();
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.setAvailableTracks) {
                    window.fullScreenMap.setAvailableTracks($0);
                }
            ", tracksJson);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FullScreenMapControl] Error updating tracks on map: {ex.Message}");
        }
    }

    public void ZoomToTrack(TrackFileInfo track)
    {
        if (track == null || !track.HasValidBounds) return;
        try
        {
            TrackIndexService.SetTrackVisibility(track.Id, true);

            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.zoomToTrack) {
                    window.fullScreenMap.zoomToTrack($0, $1, $2, $3, $4);
                }
                if (window.fullScreenMap && window.fullScreenMap.showElevationProfile) {
                    window.fullScreenMap.showElevationProfile($4);
                }
            ", track.MinLon, track.MinLat, track.MaxLon, track.MaxLat, track.Id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FullScreenMapControl] Error zooming to track: {ex.Message}");
        }
    }

    private void BtnOpenTracksLibrary_Click(object sender, RoutedEventArgs e)
    {
        if (HamburgerPanel != null) HamburgerPanel.Visibility = Visibility.Collapsed;
        OpenTracksPageRequested?.Invoke(this, EventArgs.Empty);
    }

    #region Route Builder & Trail Snapping

    private void BtnDrawRoute_Click(object sender, RoutedEventArgs e)
    {
        if (HamburgerPanel != null) HamburgerPanel.Visibility = Visibility.Collapsed;

        if (_isRouteDrawingActive)
        {
            ExitRouteDrawingMode();
        }
        else
        {
            StartRouteDrawingMode();
        }
    }

    public void StartRouteDrawingMode()
    {
        _isRouteDrawingActive = true;
        if (RouteBuilderPanel != null) RouteBuilderPanel.Visibility = Visibility.Visible;
        if (BtnDrawRoute != null)
        {
            BtnDrawRoute.Content = "✏️ Drawing Route...";
            BtnDrawRoute.Background = ThemeManager.Current.ButtonActiveBackground;
            BtnDrawRoute.Foreground = ThemeManager.Current.ButtonActiveForeground;
        }

        Action<double, int, int, bool, double, double, bool> onRouteUpdated = (distKm, ptCount, wpCount, isRouting, climbM, descentM, hasEle) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                _currentRouteDistanceKm = distKm;
                _currentRoutePointCount = ptCount;
                _currentRouteWaypointCount = wpCount;
                _currentRouteClimbM = climbM;
                _currentRouteDescentM = descentM;

                bool isMetric = ViewModel?.IsMetric ?? false;
                string distStr = isMetric
                    ? $"{distKm:F2} km"
                    : $"{distKm * 0.621371:F2} mi";

                string eleStats = "";
                if (hasEle || climbM > 0 || descentM > 0)
                {
                    string climbStr = isMetric ? $"+{climbM:F0} m" : $"+{(climbM * 3.28084):F0} ft";
                    string descentStr = isMetric ? $"-{descentM:F0} m" : $"-{(descentM * 3.28084):F0} ft";
                    eleStats = $" | ↗ {climbStr}  ↘ {descentStr}";
                }

                if (TxtRouteStats != null)
                {
                    if (wpCount == 0)
                    {
                        TxtRouteStats.Text = "Click anywhere on the map to start your route";
                    }
                    else if (wpCount == 1)
                    {
                        TxtRouteStats.Text = "Click next point along trail or destination";
                    }
                    else
                    {
                        TxtRouteStats.Text = $"Route: {distStr}{eleStats} | {wpCount} waypoints ({ptCount} pts)";
                    }
                }

                if (TxtRouteStatus != null)
                {
                    TxtRouteStatus.Text = isRouting ? "⏳ Snapping to trail..." : "";
                }
            });
        };

        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.startRouteDrawing) {
                    window.fullScreenMap.startRouteDrawing(function(dist, pts, wps, isRouting, climb, descent, hasEle) {
                        $0(dist, pts, wps, isRouting, climb || 0, descent || 0, hasEle || false);
                    });
                }
            ", onRouteUpdated);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FullScreenMapControl] StartRouteDrawing error: {ex.Message}");
        }
    }

    public void ExitRouteDrawingMode()
    {
        _isRouteDrawingActive = false;
        if (RouteBuilderPanel != null) RouteBuilderPanel.Visibility = Visibility.Collapsed;
        if (SaveRouteDialog != null) SaveRouteDialog.Visibility = Visibility.Collapsed;
        if (SaveRouteBackdrop != null) SaveRouteBackdrop.Visibility = Visibility.Collapsed;

        if (BtnDrawRoute != null)
        {
            BtnDrawRoute.Content = "✏️ Draw Route";
            BtnDrawRoute.Background = ThemeManager.Current.ButtonInactiveBackground;
            BtnDrawRoute.Foreground = ThemeManager.Current.ButtonInactiveForeground;
        }

        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.exitRouteDrawing) {
                    window.fullScreenMap.exitRouteDrawing();
                }
            ");
        }
        catch { }
    }

    private void BtnToggleRouteSnap_Click(object sender, RoutedEventArgs e)
    {
        _isRouteSnapToTrails = !_isRouteSnapToTrails;
        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.setRouteSnapToTrails) {
                    window.fullScreenMap.setRouteSnapToTrails($0);
                }
            ", _isRouteSnapToTrails);
        }
        catch { }

        if (BtnToggleRouteSnap != null)
        {
            BtnToggleRouteSnap.Content = _isRouteSnapToTrails
                ? "🥾 Snap to Trails: ON"
                : "📐 Straight Line: ON";
            BtnToggleRouteSnap.Foreground = _isRouteSnapToTrails
                ? new SolidColorBrush(Color.FromArgb(255, 16, 185, 129))
                : new SolidColorBrush(Color.FromArgb(255, 245, 158, 11));
        }
    }

    private void BtnRouteUndo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.undoLastRoutePoint) {
                    window.fullScreenMap.undoLastRoutePoint();
                }
            ");
        }
        catch { }
    }

    private void BtnRouteReverse_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.reverseRoute) {
                    window.fullScreenMap.reverseRoute();
                }
            ");
        }
        catch { }
    }

    private void BtnRouteClear_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.clearRoute) {
                    window.fullScreenMap.clearRoute();
                }
            ");
        }
        catch { }
    }

    private void BtnRouteDone_Click(object sender, RoutedEventArgs e)
    {
        ExitRouteDrawingMode();
    }

    private void BtnRouteSave_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRouteWaypointCount < 2)
        {
            if (TxtRouteStats != null)
            {
                TxtRouteStats.Text = "⚠️ Please click at least 2 points on the map to form a route.";
            }
            return;
        }

        if (TxtRouteSaveName != null)
        {
            TxtRouteSaveName.Text = $"Route_{DateTime.Now:yyyyMMdd_HHmm}";
        }

        bool isMetric = ViewModel?.IsMetric ?? false;
        string distStr = isMetric
            ? $"{_currentRouteDistanceKm:F2} km"
            : $"{_currentRouteDistanceKm * 0.621371:F2} mi";

        string eleStr = "";
        if (_currentRouteClimbM > 0 || _currentRouteDescentM > 0)
        {
            string climbStr = isMetric ? $"+{_currentRouteClimbM:F0} m" : $"+{(_currentRouteClimbM * 3.28084):F0} ft";
            string descentStr = isMetric ? $"-{_currentRouteDescentM:F0} m" : $"-{(_currentRouteDescentM * 3.28084):F0} ft";
            eleStr = $" | ↗ {climbStr}  ↘ {descentStr}";
        }

        if (TxtRouteSaveDetails != null)
        {
            TxtRouteSaveDetails.Text = $"Distance: {distStr}{eleStr} | {_currentRoutePointCount} points ({_currentRouteWaypointCount} waypoints)";
        }

        if (SaveRouteDialog != null) SaveRouteDialog.Visibility = Visibility.Visible;
        if (SaveRouteBackdrop != null) SaveRouteBackdrop.Visibility = Visibility.Visible;
    }

    private void BtnCancelSaveRoute_Click(object sender, RoutedEventArgs e)
    {
        if (SaveRouteDialog != null) SaveRouteDialog.Visibility = Visibility.Collapsed;
        if (SaveRouteBackdrop != null) SaveRouteBackdrop.Visibility = Visibility.Collapsed;
    }

    private async void BtnConfirmSaveRoute_Click(object sender, RoutedEventArgs e)
    {
        string routeName = TxtRouteSaveName?.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(routeName))
        {
            routeName = $"Route_{DateTime.Now:yyyyMMdd_HHmm}";
        }

        try
        {
            object coordsJsonObj = Interop.ExecuteJavaScript(@"
                (function() {
                    if (window.fullScreenMap && window.fullScreenMap.getRouteCoordinatesJson) {
                        return window.fullScreenMap.getRouteCoordinatesJson();
                    }
                    return '[]';
                })()
            ");

            string coordsJson = coordsJsonObj?.ToString() ?? "[]";
            var coords = System.Text.Json.JsonSerializer.Deserialize<List<double[]>>(coordsJson);

            if (coords != null && coords.Count >= 2)
            {
                string gpxDir = OfflineDataService.GpxDirectory;
                string savedPath = await TrackParserService.ExportDrawnRouteToGpxAsync(routeName, coords, gpxDir);

                if (SaveRouteDialog != null) SaveRouteDialog.Visibility = Visibility.Collapsed;
                if (SaveRouteBackdrop != null) SaveRouteBackdrop.Visibility = Visibility.Collapsed;

                // Clear route and exit drawing mode
                Interop.ExecuteJavaScriptVoid(@"
                    if (window.fullScreenMap && window.fullScreenMap.clearRoute) {
                        window.fullScreenMap.clearRoute();
                    }
                ");
                ExitRouteDrawingMode();

                // Refresh tracks index so the new route immediately appears in library and map
                await TrackIndexService.RefreshAsync(forceFullRescan: true);

                if (TxtRecoveryMsg != null && CrashRecoveryNotice != null)
                {
                    CrashRecoveryNotice.Visibility = Visibility.Visible;
                    TxtRecoveryMsg.Text = $"✓ Saved route '{routeName}' to GPX library";
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FullScreenMapControl] Error saving route: {ex.Message}");
        }
    }

    #endregion

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
        if (BadgeDividerElev != null) BadgeDividerElev.Background = tm.HeaderBorder;

        if (TxtPointerCoords != null) TxtPointerCoords.Foreground = tm.TextPrimary;
        if (TxtPointerElevation != null) TxtPointerElevation.Foreground = tm.Accent;
        if (TxtGpsIndicator != null) TxtGpsIndicator.Foreground = _isTrackingActive ? tm.Accent : tm.TextSecondary;
        if (TxtMenuTitle != null) TxtMenuTitle.Foreground = tm.TextPrimary;
        if (TxtTracksTitle != null) TxtTracksTitle.Foreground = tm.TextPrimary;
        if (TxtTracksCount != null) TxtTracksCount.Foreground = tm.TextSecondary;
        if (TxtNoTracks != null) TxtNoTracks.Foreground = tm.TextMuted;
        if (MenuSearchLocationSection != null)
        {
            MenuSearchLocationSection.Visibility = GeoNamesService.IsConfigured ? Visibility.Visible : Visibility.Collapsed;
        }
        if (TxtSectionSearchLocation != null) TxtSectionSearchLocation.Foreground = tm.TextSecondary;
        if (MenuDividerSearch != null) MenuDividerSearch.Background = tm.HeaderBorder;
        if (BtnMapSearchLocation != null)
        {
            BtnMapSearchLocation.Background = tm.ButtonInactiveBackground;
            BtnMapSearchLocation.BorderBrush = tm.ButtonInactiveBorder;
            BtnMapSearchLocation.Foreground = tm.Accent;
        }

        if (TxtSectionTrack != null) TxtSectionTrack.Foreground = tm.TextSecondary;
        if (TxtSectionPower != null) TxtSectionPower.Foreground = tm.TextSecondary;
        if (TxtSectionRecord != null) TxtSectionRecord.Foreground = tm.TextSecondary;
        if (TxtSectionHistory != null) TxtSectionHistory.Foreground = tm.TextSecondary;
        if (TxtSectionLayer != null) TxtSectionLayer.Foreground = tm.TextSecondary;
        if (TxtTrackingDetails != null) TxtTrackingDetails.Foreground = tm.TextMuted;
        if (TxtBatterySaverDetails != null) TxtBatterySaverDetails.Foreground = tm.TextMuted;
        if (TxtTracksSummary != null) TxtTracksSummary.Foreground = tm.TextMuted;
        if (TxtRecordingStats != null) TxtRecordingStats.Foreground = tm.TextPrimary;
        if (TxtSectionViews != null) TxtSectionViews.Foreground = tm.TextSecondary;

        if (MenuLocationInfoBorder != null)
        {
            MenuLocationInfoBorder.Background = tm.MapOverlayBackground;
            MenuLocationInfoBorder.BorderBrush = tm.HeaderBorder;
        }
        if (TxtMenuPointerCoords != null) TxtMenuPointerCoords.Foreground = tm.TextPrimary;
        if (TxtMenuPointerElevation != null) TxtMenuPointerElevation.Foreground = tm.Accent;
        if (TxtMenuGpsIndicator != null) TxtMenuGpsIndicator.Foreground = _isTrackingActive ? tm.Accent : tm.TextSecondary;

        if (BtnHamburger != null)
        {
            BtnHamburger.Background = tm.ButtonInactiveBackground;
            BtnHamburger.BorderBrush = tm.ButtonInactiveBorder;
            BtnHamburger.Foreground = tm.TextPrimary;
        }

        if (BtnCurrentLocation != null)
        {
            BtnCurrentLocation.Background = tm.ButtonInactiveBackground;
            BtnCurrentLocation.BorderBrush = tm.ButtonInactiveBorder;
            BtnCurrentLocation.Foreground = tm.Accent;
        }

        if (BtnToggle3d != null)
        {
            BtnToggle3d.Background = _is3dActive ? tm.ButtonActiveBackground : tm.ButtonInactiveBackground;
            BtnToggle3d.BorderBrush = _is3dActive ? tm.ButtonActiveBorder : tm.ButtonInactiveBorder;
            BtnToggle3d.Foreground = _is3dActive ? tm.ButtonActiveForeground : tm.TextPrimary;
        }

        if (BtnMenuToggle3d != null)
        {
            BtnMenuToggle3d.Background = _is3dActive ? tm.ButtonActiveBackground : tm.ButtonInactiveBackground;
            BtnMenuToggle3d.BorderBrush = _is3dActive ? tm.ButtonActiveBorder : tm.ButtonInactiveBorder;
            BtnMenuToggle3d.Foreground = _is3dActive ? tm.ButtonActiveForeground : tm.TextPrimary;
        }

        if (BtnMenuCelestialInfo != null)
        {
            BtnMenuCelestialInfo.Background = tm.ButtonInactiveBackground;
            BtnMenuCelestialInfo.BorderBrush = tm.ButtonInactiveBorder;
            BtnMenuCelestialInfo.Foreground = tm.TextPrimary;
        }

        if (BtnMenuSetAimLocation != null)
        {
            BtnMenuSetAimLocation.Background = tm.ButtonInactiveBackground;
            BtnMenuSetAimLocation.BorderBrush = tm.ButtonInactiveBorder;
            BtnMenuSetAimLocation.Foreground = tm.TextPrimary;
        }

        if (BtnResetView != null)
        {
            BtnResetView.Background = tm.ButtonInactiveBackground;
            BtnResetView.BorderBrush = tm.ButtonInactiveBorder;
            BtnResetView.Foreground = tm.TextPrimary;
        }

        if (BtnMenuResetView != null)
        {
            BtnMenuResetView.Background = tm.ButtonInactiveBackground;
            BtnMenuResetView.BorderBrush = tm.ButtonInactiveBorder;
            BtnMenuResetView.Foreground = tm.Accent;
        }

        if (BtnDrawRoute != null)
        {
            BtnDrawRoute.Background = _isRouteDrawingActive ? tm.ButtonActiveBackground : tm.ButtonInactiveBackground;
            BtnDrawRoute.BorderBrush = _isRouteDrawingActive ? tm.ButtonActiveBorder : tm.ButtonInactiveBorder;
            BtnDrawRoute.Foreground = _isRouteDrawingActive ? tm.ButtonActiveForeground : tm.TextPrimary;
        }

        if (RouteBuilderPanel != null)
        {
            RouteBuilderPanel.Background = tm.HeaderBackground;
            RouteBuilderPanel.BorderBrush = tm.HeaderBorder;
        }
        if (TxtRouteBuilderTitle != null) TxtRouteBuilderTitle.Foreground = tm.TextPrimary;
        if (TxtRouteStats != null) TxtRouteStats.Foreground = tm.TextPrimary;

        if (SaveRouteDialog != null)
        {
            SaveRouteDialog.Background = tm.HeaderBackground;
            SaveRouteDialog.BorderBrush = tm.HeaderBorder;
        }
        if (TxtSaveRouteTitle != null) TxtSaveRouteTitle.Foreground = tm.TextPrimary;
        if (TxtSaveRouteNameLabel != null) TxtSaveRouteNameLabel.Foreground = tm.TextSecondary;
        if (TxtRouteSaveDetails != null) TxtRouteSaveDetails.Foreground = tm.TextSecondary;
        if (TxtRouteSaveName != null)
        {
            TxtRouteSaveName.Background = isDark ? new SolidColorBrush(Color.FromArgb(255, 15, 23, 42)) : new SolidColorBrush(Color.FromArgb(255, 248, 250, 252));
            TxtRouteSaveName.Foreground = tm.TextPrimary;
            TxtRouteSaveName.BorderBrush = tm.HeaderBorder;
        }

        // Update button states
        UpdateTrackingButtonState();
        UpdatePowerIntervalButtonsState();
        UpdateRecordingButtonState();
        UpdateTracksSummaryButtonState();
        UpdateLayerButtons();

        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.setTheme) {
                    window.fullScreenMap.setTheme($0);
                }
            ", isDark);
        }
        catch { }
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
        if (TxtMenuGpsIndicator != null) TxtMenuGpsIndicator.Text = "Acquiring GPS...";

        var pos = await GeolocationService.GetCurrentPositionAsync(timeoutSeconds: 8);
        if (pos != null)
        {
            _lastGpsPos = pos;
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
                string coordsText = $"📍 {pos.Latitude:F4}°, {pos.Longitude:F4}°";
                if (TxtPointerCoords != null)
                {
                    TxtPointerCoords.Text = coordsText;
                }
                if (TxtMenuPointerCoords != null)
                {
                    TxtMenuPointerCoords.Text = coordsText;
                }
                UpdateElevationDisplay(pos.Latitude, pos.Longitude);
            }

            string gpsText = $"GPS ±{pos.AccuracyM:F0}m";
            if (TxtGpsIndicator != null)
            {
                TxtGpsIndicator.Text = gpsText;
                TxtGpsIndicator.Foreground = ThemeManager.Current.Accent;
            }
            if (TxtMenuGpsIndicator != null)
            {
                TxtMenuGpsIndicator.Text = gpsText;
                TxtMenuGpsIndicator.Foreground = ThemeManager.Current.Accent;
            }
        }
        else
        {
            if (TxtGpsIndicator != null)
            {
                TxtGpsIndicator.Text = "GPS Unavailable";
                TxtGpsIndicator.Foreground = new SolidColorBrush(Colors.Red);
            }
            if (TxtMenuGpsIndicator != null)
            {
                TxtMenuGpsIndicator.Text = "GPS Unavailable";
                TxtMenuGpsIndicator.Foreground = new SolidColorBrush(Colors.Red);
            }
        }
    }

    private bool _is3dActive;

    private void BtnMenuCelestialInfo_Click(object sender, RoutedEventArgs e)
    {
        HamburgerPanel.Visibility = Visibility.Collapsed;

        double targetLat;
        double targetLon;

        if (_isTrackingActive && _lastGpsPos != null)
        {
            targetLat = _lastGpsPos.Latitude;
            targetLon = _lastGpsPos.Longitude;
        }
        else if (_hasPointerPlaced)
        {
            targetLat = _lastPointerLat;
            targetLon = _lastPointerLon;
        }
        else
        {
            double? centerLat = null;
            double? centerLon = null;
            try
            {
                object? jsonObj = OpenSilver.Interop.ExecuteJavaScript("window.fullScreenMap ? window.fullScreenMap.getMapCenterJson() : null");
                if (jsonObj != null && !string.IsNullOrWhiteSpace(jsonObj.ToString()))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(jsonObj.ToString()!);
                    centerLat = doc.RootElement.GetProperty("lat").GetDouble();
                    centerLon = doc.RootElement.GetProperty("lng").GetDouble();
                }
            }
            catch { }

            if (centerLat.HasValue && centerLon.HasValue)
            {
                targetLat = centerLat.Value;
                targetLon = centerLon.Value;
            }
            else
            {
                targetLat = _lastPointerLat;
                targetLon = _lastPointerLon;
            }
        }

        CelestialInfoRequested?.Invoke(this, new CelestialLocationEventArgs(targetLat, targetLon));
    }

    private void BtnMenuSetAimLocation_Click(object sender, RoutedEventArgs e)
    {
        double targetLat;
        double targetLon;
        string source;

        // 1. Last GPS fix if tracking
        if (_isTrackingActive && _lastGpsPos != null)
        {
            targetLat = _lastGpsPos.Latitude;
            targetLon = _lastGpsPos.Longitude;
            source = "current GPS fix";
        }
        else if (_hasPointerPlaced)
        {
            // 2. Map marker if explicitly placed / moved
            targetLat = _lastPointerLat;
            targetLon = _lastPointerLon;
            source = "map marker";
        }
        else
        {
            // 3. Current map center
            double? centerLat = null;
            double? centerLon = null;
            try
            {
                object? jsonObj = OpenSilver.Interop.ExecuteJavaScript("window.fullScreenMap ? window.fullScreenMap.getMapCenterJson() : null");
                if (jsonObj != null && !string.IsNullOrWhiteSpace(jsonObj.ToString()))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(jsonObj.ToString()!);
                    centerLat = doc.RootElement.GetProperty("lat").GetDouble();
                    centerLon = doc.RootElement.GetProperty("lng").GetDouble();
                }
            }
            catch { }

            if (centerLat.HasValue && centerLon.HasValue)
            {
                targetLat = centerLat.Value;
                targetLon = centerLon.Value;
                source = "map center";
            }
            else
            {
                targetLat = _lastPointerLat;
                targetLon = _lastPointerLon;
                source = "map location";
            }
        }

        // Update Aim Map viewpoint & Horizon view observer location
        if (ViewModel != null)
        {
            ViewModel.SetLocation(targetLat, targetLon, panMap: true);
        }

        if (TxtPointerCoords != null)
        {
            TxtPointerCoords.Text = $"🎯 AIM set ({source}): {targetLat:F4}°, {targetLon:F4}°";
        }

        BtnCloseMenu_Click(sender, e);
    }

    private void BtnMenuToggle3d_Click(object sender, RoutedEventArgs e)
    {
        BtnToggle3d_Click(sender, e);
    }

    private void BtnMenuResetView_Click(object sender, RoutedEventArgs e)
    {
        BtnCloseMenu_Click(sender, e);
        BtnResetView_Click(sender, e);
    }

    private void BtnMenuDrawRoute_Click(object sender, RoutedEventArgs e)
    {
        BtnCloseMenu_Click(sender, e);
        BtnDrawRoute_Click(sender, e);
    }

    private void Update3dButtonState()
    {
        var tm = ThemeManager.Current;
        if (BtnToggle3d != null)
        {
            BtnToggle3d.Content = _is3dActive ? "🗺️ 2D" : "🏔️ 3D";
            BtnToggle3d.Background = _is3dActive ? tm.ButtonActiveBackground : tm.ButtonInactiveBackground;
            BtnToggle3d.BorderBrush = _is3dActive ? tm.ButtonActiveBorder : tm.ButtonInactiveBorder;
            BtnToggle3d.Foreground = _is3dActive ? tm.ButtonActiveForeground : tm.TextPrimary;
            ToolTipService.SetToolTip(BtnToggle3d, _is3dActive ? "Switch to flat 2D top-down view" : "Switch to 3D perspective terrain view (right-click / Ctrl-drag to orbit)");
        }
        if (BtnMenuToggle3d != null)
        {
            BtnMenuToggle3d.Content = _is3dActive ? "🗺️ 3D Perspective: ON (Switch to 2D)" : "🏔️ 3D Perspective: OFF (Switch to 3D)";
            BtnMenuToggle3d.Background = _is3dActive ? tm.ButtonActiveBackground : tm.ButtonInactiveBackground;
            BtnMenuToggle3d.BorderBrush = _is3dActive ? tm.ButtonActiveBorder : tm.ButtonInactiveBorder;
            BtnMenuToggle3d.Foreground = _is3dActive ? tm.ButtonActiveForeground : tm.TextPrimary;
        }
    }

    private void BtnToggle3d_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _is3dActive = !_is3dActive;
            Update3dButtonState();

            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.toggle3d) {
                    window.fullScreenMap.toggle3d($0);
                }
            ", !_is3dActive);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FullScreenMap] Error toggling 3D: {ex.Message}");
        }
    }

    private void BtnResetView_Click(object sender, RoutedEventArgs e)
    {
        _is3dActive = false;
        Update3dButtonState();
        if (HamburgerPanel != null)
        {
            HamburgerPanel.Visibility = Visibility.Collapsed;
        }

        try
        {
            Interop.ExecuteJavaScriptVoid("if (window.fullScreenMap && window.fullScreenMap.resetView) { window.fullScreenMap.resetView(); }");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FullScreenMap] Error in resetView: {ex.Message}");
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
        if (TxtMenuGpsIndicator != null)
        {
            TxtMenuGpsIndicator.Text = "🟢 Tracking GPS";
            TxtMenuGpsIndicator.Foreground = ThemeManager.Current.Accent;
        }

        GeolocationService.StartTracking(
            onUpdate: (pos) =>
            {
                _lastGpsPos = pos;
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

                    if (TxtMenuGpsIndicator != null)
                    {
                        if (pos.AccuracyM.HasValue && pos.AccuracyM.Value > 65.0)
                        {
                            TxtMenuGpsIndicator.Text = $"🟡 Coarse Fix ±{pos.AccuracyM:F0}m";
                            TxtMenuGpsIndicator.Foreground = new SolidColorBrush(Color.FromArgb(255, 234, 179, 8));
                        }
                        else
                        {
                            TxtMenuGpsIndicator.Text = $"🟢 GPS ±{pos.AccuracyM:F0}m";
                            TxtMenuGpsIndicator.Foreground = ThemeManager.Current.Accent;
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
                    if (TxtMenuGpsIndicator != null)
                    {
                        TxtMenuGpsIndicator.Text = "GPS Lost";
                        TxtMenuGpsIndicator.Foreground = new SolidColorBrush(Colors.Red);
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
        if (TxtMenuGpsIndicator != null)
        {
            TxtMenuGpsIndicator.Text = "Tracking Stopped";
            TxtMenuGpsIndicator.Foreground = ThemeManager.Current.TextSecondary;
        }
        if (TxtTrackingDetails != null)
        {
            TxtTrackingDetails.Text = "Follows GPS location live as you move";
        }
    }

    private void BtnMapSearchLocation_Click(object sender, RoutedEventArgs e)
    {
        BtnCloseMenu_Click(sender, e);
        SearchLocationRequested?.Invoke(this, EventArgs.Empty);
    }

    public void CenterOn(double lat, double lon)
    {
        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.centerOn) {
                    window.fullScreenMap.centerOn($0, $1, 13);
                }
                if (window.fullScreenMap && window.fullScreenMap.setPointerLocation) {
                    window.fullScreenMap.setPointerLocation($0, $1);
                }
            ", lat, lon);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FullScreenMap] Error centering on location: {ex.Message}");
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

    private void UpdatePowerIntervalButtonsState()
    {
        var tm = ThemeManager.Current;
        int currentSec = GeolocationService.PowerSaverIntervalSeconds;

        void StyleIntervalBtn(Button? btn, int targetSec)
        {
            if (btn == null) return;
            bool isSelected = currentSec == targetSec;
            btn.Background = isSelected ? tm.ButtonActiveBackground : tm.ButtonInactiveBackground;
            btn.BorderBrush = isSelected ? tm.ButtonActiveBorder : tm.ButtonInactiveBorder;
            btn.Foreground = isSelected ? tm.ButtonActiveForeground : tm.ButtonInactiveForeground;
            btn.FontWeight = isSelected ? FontWeights.Bold : FontWeights.SemiBold;
        }

        StyleIntervalBtn(BtnInterval15s, 15);
        StyleIntervalBtn(BtnInterval1m, 60);
        StyleIntervalBtn(BtnInterval5m, 300);
        StyleIntervalBtn(BtnInterval15m, 900);

        if (TxtBatterySaverDetails != null)
        {
            string desc = currentSec switch
            {
                15 => "High Detail (15s / 15m delta): Fast cadence for switchbacks, running, and cycling. Satellite GPS receiver stays active.",
                60 => "Standard (1m / 15m delta): Recommended for hiking. Hardware sleeps ~95% between fixes, preserving battery while capturing trail turns.",
                300 => "Extended (5m / 15m delta): High battery savings for all-day backpacking and long routes. Hardware sleeps >98% between fixes.",
                900 => "Expedition (15m / 15m delta): Ultra battery conservation for multi-day trips with minimal charging. Logs waypoint milestones.",
                _ => $"GPS duty-cycling interval: {currentSec}s with 15m motion filter."
            };
            TxtBatterySaverDetails.Text = desc;
        }
    }

    private void SetPowerInterval(int seconds)
    {
        GeolocationService.PowerSaverIntervalSeconds = seconds;
        UpdatePowerIntervalButtonsState();

        if (_isTrackingActive)
        {
            StopLiveTracking();
            StartLiveTracking();
        }
    }

    private void BtnInterval15s_Click(object sender, RoutedEventArgs e) => SetPowerInterval(15);
    private void BtnInterval1m_Click(object sender, RoutedEventArgs e) => SetPowerInterval(60);
    private void BtnInterval5m_Click(object sender, RoutedEventArgs e) => SetPowerInterval(300);
    private void BtnInterval15m_Click(object sender, RoutedEventArgs e) => SetPowerInterval(900);

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

            // Enforce configured power saver interval and 15m drift filter
            if (timeSec < GeolocationService.PowerSaverIntervalSeconds) return;
            if (d < 0.015) return;

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

    private async void BtnExportGpx_Click(object sender, RoutedEventArgs e)
    {
        if (_recordedPoints.Count == 0) return;
        string gpx = GenerateGpx("Pointed Horizons GPS Track", _recordedPoints);
        string filename = $"track_{DateTime.Now:yyyyMMdd_HHmmss}.gpx";
        DownloadFile(filename, gpx, "application/gpx+xml");

        // Also write to offline GPX directory on disk if supported so it appears in library
        if (OfflineDataService.IsPmTilesSupported)
        {
            try
            {
                string gpxDir = OfflineDataService.GpxDirectory;
                if (!Directory.Exists(gpxDir)) Directory.CreateDirectory(gpxDir);
                string fullPath = Path.Combine(gpxDir, filename);
                File.WriteAllText(fullPath, gpx, Encoding.UTF8);
                await TrackIndexService.RefreshAsync(forceFullRescan: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FullScreenMapControl] Export GPX to disk error: {ex.Message}");
            }
        }
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

        Style? pillStyle = TryFindResource("PillButtonStyle") as Style;

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
        if (!OfflineDataService.IsPmTilesSupported) return;

        OfflineDataService.EnsureSettingsLoaded();
        UpdatePmTilesLayers();

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
        if (!OfflineDataService.IsPmTilesSupported) return;

        if (HamburgerPanel != null) HamburgerPanel.Visibility = Visibility.Collapsed;
        ConfigurePmTilesRequested?.Invoke(this, EventArgs.Empty);
    }

    public void UpdatePmTilesLayers()
    {
        if (!_isInitialized || !OfflineDataService.IsPmTilesSupported) return;
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
        if (!OfflineDataService.IsPmTilesSupported && layerName == "pmtiles")
        {
            layerName = "topo";
        }

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

        if (!OfflineDataService.IsPmTilesSupported)
        {
            if (BtnLayerPmTiles != null) BtnLayerPmTiles.Visibility = Visibility.Collapsed;
            if (BtnConfigurePmTiles != null) BtnConfigurePmTiles.Visibility = Visibility.Collapsed;
        }

        void StyleLayerBtn(Button? btn, bool active)
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

    private void FullScreenMapControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldVm)
        {
            oldVm.PropertyChanged -= ViewModel_PropertyChanged;
        }
        if (e.NewValue is MainViewModel newVm)
        {
            newVm.PropertyChanged += ViewModel_PropertyChanged;
            UpdateElevationDisplay(_lastPointerLat, _lastPointerLon);
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ObserverElevationDisplay) ||
            e.PropertyName == nameof(MainViewModel.IsMetric))
        {
            bool isMetric = ViewModel?.IsMetric ?? false;
            try
            {
                Interop.ExecuteJavaScriptVoid(@"
                    if (window.fullScreenMap && window.fullScreenMap.setIsMetric) {
                        window.fullScreenMap.setIsMetric($0);
                    }
                ", isMetric);
            }
            catch { }

            Dispatcher.BeginInvoke(() => UpdateElevationDisplay(_lastPointerLat, _lastPointerLon));
        }
    }

    private async void UpdateElevationDisplay(double lat, double lon)
    {
        bool isMetric = ViewModel?.IsMetric ?? false;

        // Try getting elevation from DEM tiles
        double? elev = DemService.GetElevation(lat, lon);
        if (!elev.HasValue)
        {
            elev = await DemService.GetElevationAsync(lat, lon);
        }

        string elevText;
        if (elev.HasValue && elev.Value > -500)
        {
            double m = elev.Value;
            double ft = m * 3.28084;
            elevText = isMetric
                ? $"⛰️ {Math.Round(m):N0} m"
                : $"⛰️ {Math.Round(ft):N0} ft";
        }
        else if (ViewModel != null && Math.Abs(ViewModel.Latitude - lat) < 0.001 && Math.Abs(ViewModel.Longitude - lon) < 0.001)
        {
            elevText = $"⛰️ {ViewModel.ObserverElevationDisplay}";
        }
        else
        {
            elevText = "⛰️ --";
        }

        if (TxtPointerElevation != null)
        {
            TxtPointerElevation.Text = elevText;
        }
        if (TxtMenuPointerElevation != null)
        {
            TxtMenuPointerElevation.Text = elevText;
        }
    }
}
