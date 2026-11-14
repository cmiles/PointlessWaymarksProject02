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
    private bool _isGpsActive;
    private bool _isTrackLocationActive;
    private bool _isRecordingActive;
    private DateTime? _recordingStartTime;
    private DateTime _lastPointRecordedTime = DateTime.MinValue;
    private readonly List<GeoPosition> _recordedPoints = new();
    private double _totalRecordedDistanceKm;
    private DispatcherTimer? _recordingTimer;
    private string _currentBaseLayer = "topo";
    private bool _isPadUsVisible;
    private bool _isWeatherTempVisible;
    private bool _isWeatherWindVisible;
    private bool _isWeatherRadarVisible;
    private bool _isWeatherPrecipVisible;
    private bool _isWeatherCloudVisible;
    private bool _isWeatherPressureVisible;
    private RecordedTrack? _currentTrack;

    public event EventHandler? ConfigurePmTilesRequested;
    public event EventHandler? OpenTracksPageRequested;
    public event EventHandler? SearchLocationRequested;

    public bool IsRecordingActive => _isRecordingActive;
    public bool IsGpsActive => _isGpsActive;
    public string CurrentGpsStatusText => _currentGpsStatusText;
    private string _currentGpsStatusText = "GPS Ready";

    public event EventHandler<bool>? RecordingStateChanged;
    public event EventHandler<RecordedTrack>? TrackRecovered;
    public event EventHandler<bool>? GpsStateChanged;
    public event EventHandler<string>? GpsStatusChanged;

    public void ToggleRecording()
    {
        BtnToggleRecording_Click(this, new RoutedEventArgs());
    }

    public void ToggleGps()
    {
        BtnToggleGps_Click(this, new RoutedEventArgs());
    }

    private double _lastPointerLat = 32.2226;
    private double _lastPointerLon = -110.9747;
    private bool _hasPointerPlaced;
    private GeoPosition? _lastGpsPos;
    private double? _lastHeading;

    // Route Builder state
    private bool _isRouteDrawingActive;
    private bool _isRouteSnapToTrails = true;
    private double _currentRouteDistanceKm;
    private double _currentRouteClimbM;
    private double _currentRouteDescentM;
    private int _currentRoutePointCount;
    private int _currentRouteWaypointCount;

    // Waypoint state
    private double _pendingWaypointLat;
    private double _pendingWaypointLon;
    private double? _pendingWaypointEle;
    private bool _isEditingWaypoint;
    private string _editingTrackId = string.Empty;
    private string _editingWaypointId = string.Empty;
    private string _editingFilePath = string.Empty;
    private double _editingOrigLat;
    private double _editingOrigLon;
    private string _lastElevationFormatted = "--";

    private void UpdateLocationElevationBox(double lat, double lon)
    {
        if (TxtMenuLocationElevation != null)
        {
            TxtMenuLocationElevation.Text = $"{lat:F4}, {lon:F4}, {_lastElevationFormatted}";
        }
    }

    public static FullScreenMapControl? Current { get; private set; }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public FullScreenMapControl()
    {
        Current = this;
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

        if (!MapTilerService.IsConfigured)
        {
            MapTilerService.TurnOffAllOverlays();
            _isWeatherTempVisible = false;
            _isWeatherWindVisible = false;
            _isWeatherRadarVisible = false;
            _isWeatherPrecipVisible = false;
            _isWeatherCloudVisible = false;
            _isWeatherPressureVisible = false;
        }
        else
        {
            _isWeatherTempVisible = MapTilerService.GetOverlayVisible("temperature");
            _isWeatherWindVisible = MapTilerService.GetOverlayVisible("wind");
            _isWeatherRadarVisible = MapTilerService.GetOverlayVisible("radar");
            _isWeatherPrecipVisible = MapTilerService.GetOverlayVisible("precipitation");
            _isWeatherCloudVisible = MapTilerService.GetOverlayVisible("clouds");
            _isWeatherPressureVisible = MapTilerService.GetOverlayVisible("pressure");
        }

        MapTilerService.ApiKeyChanged += (s, e) =>
        {
            Dispatcher.BeginInvoke(OnMapTilerApiKeyChanged);
        };
        UpdateMapTilerState();

        LeafletScrollIsolationHelper.ProtectControl(this, MapContainer);

        if (HamburgerPanel != null)
        {
            HamburgerPanel.MouseWheel += (s, e) =>
            {
                if (HamburgerScrollViewer != null)
                {
                    HamburgerScrollViewer.ScrollToVerticalOffset(HamburgerScrollViewer.VerticalOffset - (e.Delta / 2.0));
                    e.Handled = true;
                }
            };
        }
        if (HamburgerScrollViewer != null)
        {
            HamburgerScrollViewer.MouseWheel += (s, e) =>
            {
                HamburgerScrollViewer.ScrollToVerticalOffset(HamburgerScrollViewer.VerticalOffset - (e.Delta / 2.0));
                e.Handled = true;
            };
        }
        if (TracksOverlayPanel != null)
        {
            TracksOverlayPanel.MouseWheel += (s, e) =>
            {
                if (TracksOverlayScrollViewer != null)
                {
                    TracksOverlayScrollViewer.ScrollToVerticalOffset(TracksOverlayScrollViewer.VerticalOffset - (e.Delta / 2.0));
                    e.Handled = true;
                }
            };
        }
        if (TracksOverlayScrollViewer != null)
        {
            TracksOverlayScrollViewer.MouseWheel += (s, e) =>
            {
                TracksOverlayScrollViewer.ScrollToVerticalOffset(TracksOverlayScrollViewer.VerticalOffset - (e.Delta / 2.0));
                e.Handled = true;
            };
        }
    }

    private void AttachWheelIsolation(FrameworkElement? element)
    {
        if (element == null) return;
        try
        {
            object div = Interop.GetDiv(element);
            if (div != null)
            {
                Interop.ExecuteJavaScriptVoid(@"
                    (function(d) {
                        if (!d || d._wheelIsolated) return;
                        d._wheelIsolated = true;
                        d.addEventListener('wheel', function(e) {
                            e.stopPropagation();
                        }, { passive: false });
                    })($0);
                ", div);
            }
        }
        catch { }
    }

    private void FullScreenMapControl_Loaded(object sender, RoutedEventArgs e)
    {
        OfflineDataService.EnsureSettingsLoaded();

        _currentBaseLayer = LoadLastUsedBaseLayer();

        if (!OfflineDataService.IsPmTilesSupported)
        {
            if (BtnLayerPmTiles != null) BtnLayerPmTiles.Visibility = Visibility.Collapsed;
            if (BtnConfigurePmTiles != null) BtnConfigurePmTiles.Visibility = Visibility.Collapsed;
            if (_currentBaseLayer == "pmtiles") _currentBaseLayer = "topo";
        }

        UpdateTheme(ThemeManager.Current.IsDark);
        InitializeMap();
        SetBaseLayer(_currentBaseLayer);
        _ = CheckCrashRecoveryAndRefreshTracksAsync();
        UpdateResponsiveLayout(ActualWidth, ActualHeight);
        AttachWheelIsolation(HamburgerPanel);
        AttachWheelIsolation(TracksOverlayPanel);
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
            // On a small screen, move secondary buttons and info along top of map to hamburger,
            // leaving only the Hamburger on top-left, and on top-right two icon buttons stacked vertically:
            // 1. Center on current location (top)
            // 2. Track location (bottom)
            if (CoordsBadgeBorder != null) CoordsBadgeBorder.Visibility = Visibility.Collapsed;
            if (BtnDrawRoute != null) BtnDrawRoute.Visibility = Visibility.Collapsed;
            if (BtnToggle3d != null) BtnToggle3d.Visibility = Visibility.Collapsed;
            if (BtnResetView != null) BtnResetView.Visibility = Visibility.Collapsed;

            // Make Hamburger button double size (x2 touch target) and centered on mobile
            if (BtnHamburger != null)
            {
                BtnHamburger.HorizontalAlignment = HorizontalAlignment.Center;
                BtnHamburger.Margin = new Thickness(0, 14, 0, 0);
                BtnHamburger.MinHeight = 48;
                BtnHamburger.MinWidth = 48;
                BtnHamburger.Padding = new Thickness(12, 8, 12, 8);
                BtnHamburger.FontSize = 22;
            }

            if (TopRightControlsPanel != null)
            {
                TopRightControlsPanel.Orientation = Orientation.Vertical;
                TopRightControlsPanel.Margin = new Thickness(0, 14, 10, 0);
            }

            // Top-right button 1: Center on Current Location (icon only, stacked on top)
            if (BtnCurrentLocation != null)
            {
                BtnCurrentLocation.Visibility = Visibility.Visible;
                BtnCurrentLocation.MinHeight = 48;
                BtnCurrentLocation.MinWidth = 48;
                BtnCurrentLocation.Padding = new Thickness(12, 8, 12, 8);
                BtnCurrentLocation.Margin = new Thickness(0, 0, 0, 8);
                ToolTipService.SetToolTip(BtnCurrentLocation, "Center map on current GPS location");
            }
            if (TxtCurrentLocationLabel != null) TxtCurrentLocationLabel.Visibility = Visibility.Collapsed;
            if (IconCurrentLocation != null)
            {
                IconCurrentLocation.Margin = new Thickness(0);
                IconCurrentLocation.ApplySize(20);
            }

            // Top-right button 2: Track Location (icon only, stacked below)
            if (BtnMapTrackLocation != null)
            {
                BtnMapTrackLocation.Visibility = Visibility.Visible;
                BtnMapTrackLocation.Margin = new Thickness(0);
                BtnMapTrackLocation.MinHeight = 48;
                BtnMapTrackLocation.MinWidth = 48;
                BtnMapTrackLocation.Padding = new Thickness(12, 8, 12, 8);
                ToolTipService.SetToolTip(BtnMapTrackLocation, "Toggle Track Location: keep map centered on your moving GPS location");
            }
            if (IconMapTrackLocation != null)
            {
                IconMapTrackLocation.ApplySize(20);
            }

            // Location & Elevation info is presented in hamburger menu
            if (MenuLocationInfoBorder != null)
            {
                MenuLocationInfoBorder.Visibility = Visibility.Visible;
            }

            if (BtnMenuCurrentLocation != null)
            {
                BtnMenuCurrentLocation.Visibility = Visibility.Visible;
            }

            if (LiveRecordingBar != null)
            {
                LiveRecordingBar.Margin = new Thickness(0, 0, 0, 12);
                LiveRecordingBar.Padding = new Thickness(12, 7, 12, 7);
            }

            if (HamburgerPanel != null)
            {
                HamburgerPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                HamburgerPanel.VerticalAlignment = VerticalAlignment.Stretch;
                HamburgerPanel.Width = double.NaN;
                HamburgerPanel.Height = double.NaN;
                HamburgerPanel.Margin = new Thickness(10, 10, 10, 10);
            }
            if (HamburgerScrollViewer != null)
            {
                HamburgerScrollViewer.MaxHeight = Math.Max(200, height - 70);
            }
            if (SaveWaypointDialog != null)
            {
                SaveWaypointDialog.Width = Math.Min(380, width - 16);
                SaveWaypointDialog.VerticalAlignment = VerticalAlignment.Top;
                SaveWaypointDialog.Margin = new Thickness(0, Math.Max(16, (height > 600 ? 52 : 28)), 0, 0);
            }
        }
        else
        {
            // Normal / Large screen: show top coords badge, draw route, current location, track location, 3D toggle and reset
            if (CoordsBadgeBorder != null)
            {
                CoordsBadgeBorder.Visibility = Visibility.Visible;
                CoordsBadgeBorder.HorizontalAlignment = HorizontalAlignment.Left;
                CoordsBadgeBorder.Margin = new Thickness(64, 14, 0, 0);
            }
            if (BtnDrawRoute != null) BtnDrawRoute.Visibility = Visibility.Visible;
            if (BtnToggle3d != null) BtnToggle3d.Visibility = Visibility.Visible;

            if (BtnHamburger != null)
            {
                BtnHamburger.HorizontalAlignment = HorizontalAlignment.Left;
                BtnHamburger.Margin = new Thickness(14, 14, 0, 0);
                BtnHamburger.MinHeight = 32;
                BtnHamburger.MinWidth = 36;
                BtnHamburger.Padding = new Thickness(10, 6, 10, 6);
                BtnHamburger.FontSize = 16;
            }

            if (TopRightControlsPanel != null)
            {
                TopRightControlsPanel.Orientation = Orientation.Horizontal;
                TopRightControlsPanel.Margin = new Thickness(0, 14, 14, 0);
            }

            if (BtnCurrentLocation != null)
            {
                BtnCurrentLocation.Visibility = Visibility.Visible;
                BtnCurrentLocation.MinHeight = 32;
                BtnCurrentLocation.MinWidth = 0;
                BtnCurrentLocation.Padding = new Thickness(10, 6, 12, 6);
                BtnCurrentLocation.FontSize = 11;
                BtnCurrentLocation.Margin = new Thickness(0, 0, 8, 0);
                ToolTipService.SetToolTip(BtnCurrentLocation, "Center on current GPS position and move pointer (if not tracking)");
            }
            if (TxtCurrentLocationLabel != null) TxtCurrentLocationLabel.Visibility = Visibility.Visible;
            if (IconCurrentLocation != null)
            {
                IconCurrentLocation.Margin = new Thickness(0, 0, 6, 0);
                IconCurrentLocation.ApplySize(16);
            }

            if (BtnMapTrackLocation != null)
            {
                BtnMapTrackLocation.Visibility = Visibility.Visible;
                BtnMapTrackLocation.Margin = new Thickness(0, 0, 8, 0);
                BtnMapTrackLocation.MinHeight = 32;
                BtnMapTrackLocation.MinWidth = 36;
                BtnMapTrackLocation.Padding = new Thickness(8, 6, 8, 6);
                ToolTipService.SetToolTip(BtnMapTrackLocation, "Toggle Track Location: keep map centered on your moving GPS location");
            }
            if (IconMapTrackLocation != null)
            {
                IconMapTrackLocation.ApplySize(18);
            }

            if (BtnResetView != null)
            {
                BtnResetView.Visibility = Visibility.Visible;
                BtnResetView.Content = "↺ Reset";
                BtnResetView.MinHeight = 32;
                BtnResetView.Padding = new Thickness(10, 6, 10, 6);
            }

            if (MenuLocationInfoBorder != null)
            {
                MenuLocationInfoBorder.Visibility = Visibility.Collapsed;
            }

            if (BtnMenuCurrentLocation != null)
            {
                BtnMenuCurrentLocation.Visibility = Visibility.Visible;
            }

            if (LiveRecordingBar != null)
            {
                LiveRecordingBar.Margin = new Thickness(0, 0, 0, 16);
                LiveRecordingBar.Padding = new Thickness(14, 8, 14, 8);
            }

            if (HamburgerPanel != null)
            {
                HamburgerPanel.HorizontalAlignment = HorizontalAlignment.Left;
                HamburgerPanel.VerticalAlignment = VerticalAlignment.Top;
                HamburgerPanel.Width = 472;
                HamburgerPanel.Height = double.NaN;
                HamburgerPanel.Margin = new Thickness(14, 56, 0, 14);
                HamburgerPanel.MaxHeight = Math.Max(400, height - 70);
            }
            if (HamburgerScrollViewer != null)
            {
                // Allow the menu to be full height if needed to show all items, scrolling only if screen height is exceeded
                HamburgerScrollViewer.MaxHeight = Math.Max(350, height - 120);
            }
            if (SaveWaypointDialog != null)
            {
                SaveWaypointDialog.Width = 380;
                SaveWaypointDialog.VerticalAlignment = VerticalAlignment.Center;
                SaveWaypointDialog.Margin = new Thickness(0);
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
                UpdateLocationElevationBox(lat, lon);
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

        Action<string, string> onEditWaypoint = (trackId, waypointId) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                OpenEditWaypointDialog(trackId, waypointId);
            });
        };

        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                (function(container, initLat, initLon, movedCb, trackCb, editWptCb, pmtilesSupported, isMetric, isDark, baseLayer) {
                    function tryInit() {
                        if (typeof window.fullScreenMap === 'undefined' || typeof window.maplibregl === 'undefined') {
                            setTimeout(tryInit, 50);
                            return;
                        }
                        window.fullScreenMap.isPmTilesSupported = pmtilesSupported;
                        window.fullScreenMap.init(container, {
                            lat: initLat,
                            lon: initLon,
                            zoom: 13,
                            isMetric: isMetric,
                            isDark: isDark,
                            baseLayer: baseLayer
                        }, function(lat, lon) {
                            if (movedCb) movedCb(lat, lon);
                        }, function(trackId) {
                            if (trackCb) trackCb(trackId);
                        }, function(trackId, waypointId) {
                            if (editWptCb) editWptCb(trackId, waypointId);
                        });
                        if (window.fullScreenMap.setEditWaypointCallback) {
                            window.fullScreenMap.setEditWaypointCallback(function(trackId, waypointId) {
                                if (editWptCb) editWptCb(trackId, waypointId);
                            });
                        }
                    }
                    tryInit();
                })($0, $1, $2, $3, $4, $5, $6, $7, $8, $9);
            ", div, _lastPointerLat, _lastPointerLon, onPointerMoved, onTrackClicked, onEditWaypoint, pmtilesSupported, isMetric, isDark, _currentBaseLayer);

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
        if (BtnHamburger != null) BtnHamburger.Visibility = Visibility.Collapsed;
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
        if (BtnHamburger != null) BtnHamburger.Visibility = Visibility.Visible;
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
                TrackRecovered?.Invoke(this, recovered);
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
            CoordsBadgeBorder.Opacity = 0.88;
        }

        if (LiveRecordingBar != null)
        {
            LiveRecordingBar.Background = tm.MapOverlayBackground;
            LiveRecordingBar.BorderBrush = tm.MapOverlayBorder;
            LiveRecordingBar.Opacity = 0.90;
        }
        if (LiveRecDivider != null) LiveRecDivider.Background = tm.MapOverlayBorder;
        if (TxtLiveRecordingTime != null) TxtLiveRecordingTime.Foreground = tm.TextPrimary;
        if (TxtLiveRecordingDist != null) TxtLiveRecordingDist.Foreground = tm.TextPrimary;
        if (TxtLiveRecInfoIcon != null) TxtLiveRecInfoIcon.Foreground = tm.TextSecondary;

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

        if (MenuDividerLayers != null) MenuDividerLayers.Background = tm.HeaderBorder;
        if (MenuDivider1 != null) MenuDivider1.Background = tm.HeaderBorder;
        if (MenuDividerPower != null) MenuDividerPower.Background = tm.HeaderBorder;
        if (MenuDividerTracks != null) MenuDividerTracks.Background = tm.HeaderBorder;
        if (MenuDivider3 != null) MenuDivider3.Background = tm.HeaderBorder;
        if (TracksDivider != null) TracksDivider.Background = tm.HeaderBorder;
        if (BadgeDivider != null) BadgeDivider.Background = tm.HeaderBorder;
        if (BadgeDividerElev != null) BadgeDividerElev.Background = tm.HeaderBorder;

        if (TxtPointerCoords != null) TxtPointerCoords.Foreground = tm.TextPrimary;
        if (TxtPointerElevation != null) TxtPointerElevation.Foreground = tm.Accent;
        if (TxtGpsIndicator != null) TxtGpsIndicator.Foreground = _isGpsActive ? tm.Accent : tm.TextSecondary;
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

        if (TxtSectionGps != null) TxtSectionGps.Foreground = tm.TextSecondary;
        if (TxtSectionPower != null) TxtSectionPower.Foreground = tm.TextSecondary;
        if (TxtSectionHistory != null) TxtSectionHistory.Foreground = tm.TextSecondary;
        if (TxtSectionLayer != null) TxtSectionLayer.Foreground = tm.TextSecondary;
        if (TxtBatterySaverDetails != null) TxtBatterySaverDetails.Foreground = tm.TextMuted;
        if (TxtTracksSummary != null) TxtTracksSummary.Foreground = tm.TextMuted;
        if (TxtRecordingStats != null) TxtRecordingStats.Foreground = tm.TextPrimary;
        if (TxtSectionViews != null) TxtSectionViews.Foreground = tm.TextSecondary;

        if (MenuLocationInfoBorder != null)
        {
            MenuLocationInfoBorder.Background = tm.MapOverlayBackground;
            MenuLocationInfoBorder.BorderBrush = tm.HeaderBorder;
        }
        if (TxtMenuLocationElevation != null)
        {
            TxtMenuLocationElevation.Foreground = tm.TextPrimary;
        }

        if (BtnHamburger != null)
        {
            BtnHamburger.Background = tm.ButtonInactiveBackground;
            BtnHamburger.BorderBrush = tm.ButtonInactiveBorder;
            BtnHamburger.Foreground = tm.TextPrimary;
            BtnHamburger.Opacity = 0.88;
        }

        if (BtnCurrentLocation != null)
        {
            BtnCurrentLocation.Background = tm.ButtonInactiveBackground;
            BtnCurrentLocation.BorderBrush = tm.ButtonInactiveBorder;
            BtnCurrentLocation.Foreground = tm.Accent;
            BtnCurrentLocation.Opacity = 0.88;
        }

        if (BtnMapTrackLocation != null)
        {
            BtnMapTrackLocation.Opacity = 0.88;
        }

        if (BtnMenuCurrentLocation != null)
        {
            BtnMenuCurrentLocation.Background = tm.ButtonInactiveBackground;
            BtnMenuCurrentLocation.BorderBrush = tm.ButtonInactiveBorder;
            BtnMenuCurrentLocation.Foreground = tm.TextPrimary;
        }

        if (BtnToggle3d != null)
        {
            BtnToggle3d.Background = _is3dActive ? tm.ButtonActiveBackground : tm.ButtonInactiveBackground;
            BtnToggle3d.BorderBrush = _is3dActive ? tm.ButtonActiveBorder : tm.ButtonInactiveBorder;
            BtnToggle3d.Foreground = _is3dActive ? tm.ButtonActiveForeground : tm.TextPrimary;
            BtnToggle3d.Opacity = 0.88;
        }

        if (BtnMenuToggle3d != null)
        {
            BtnMenuToggle3d.Background = _is3dActive ? tm.ButtonActiveBackground : tm.ButtonInactiveBackground;
            BtnMenuToggle3d.BorderBrush = _is3dActive ? tm.ButtonActiveBorder : tm.ButtonInactiveBorder;
            BtnMenuToggle3d.Foreground = _is3dActive ? tm.ButtonActiveForeground : tm.TextPrimary;
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
            BtnResetView.Opacity = 0.88;
        }

        if (BtnMenuResetView != null)
        {
            BtnMenuResetView.Background = tm.ButtonInactiveBackground;
            BtnMenuResetView.BorderBrush = tm.ButtonInactiveBorder;
            BtnMenuResetView.Foreground = tm.TextPrimary;
        }

        if (BtnDrawRoute != null)
        {
            BtnDrawRoute.Background = _isRouteDrawingActive ? tm.ButtonActiveBackground : tm.ButtonInactiveBackground;
            BtnDrawRoute.BorderBrush = _isRouteDrawingActive ? tm.ButtonActiveBorder : tm.ButtonInactiveBorder;
            BtnDrawRoute.Foreground = _isRouteDrawingActive ? tm.ButtonActiveForeground : tm.TextPrimary;
            BtnDrawRoute.Opacity = 0.88;
        }

        if (BtnOpenTracksLibrary != null)
        {
            BtnOpenTracksLibrary.Background = tm.ButtonInactiveBackground;
            BtnOpenTracksLibrary.BorderBrush = tm.ButtonInactiveBorder;
            BtnOpenTracksLibrary.Foreground = tm.ButtonInactiveForeground;
        }

        if (BtnMenuDrawRoute != null)
        {
            BtnMenuDrawRoute.Background = tm.ButtonInactiveBackground;
            BtnMenuDrawRoute.BorderBrush = tm.ButtonInactiveBorder;
            BtnMenuDrawRoute.Foreground = tm.ButtonInactiveForeground;
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

        if (BtnMarkWaypoint != null)
        {
            BtnMarkWaypoint.Background = tm.ButtonInactiveBackground;
            BtnMarkWaypoint.BorderBrush = tm.ButtonInactiveBorder;
            BtnMarkWaypoint.Foreground = tm.ButtonInactiveForeground;
        }

        if (SaveWaypointDialog != null)
        {
            SaveWaypointDialog.Background = tm.HeaderBackground;
            SaveWaypointDialog.BorderBrush = tm.HeaderBorder;
        }
        if (TxtSaveWaypointTitle != null) TxtSaveWaypointTitle.Foreground = tm.TextPrimary;
        if (TxtWaypointNameLabel != null) TxtWaypointNameLabel.Foreground = tm.TextSecondary;
        if (TxtWaypointDescLabel != null) TxtWaypointDescLabel.Foreground = tm.TextSecondary;
        if (TxtWaypointLocationLabel != null) TxtWaypointLocationLabel.Foreground = tm.TextSecondary;
        if (TxtWaypointDetails != null) TxtWaypointDetails.Foreground = tm.TextSecondary;
        if (TxtWaypointName != null)
        {
            TxtWaypointName.Background = isDark ? new SolidColorBrush(Color.FromArgb(255, 15, 23, 42)) : new SolidColorBrush(Color.FromArgb(255, 248, 250, 252));
            TxtWaypointName.Foreground = tm.TextPrimary;
            TxtWaypointName.BorderBrush = tm.HeaderBorder;
        }
        if (TxtWaypointDesc != null)
        {
            TxtWaypointDesc.Background = isDark ? new SolidColorBrush(Color.FromArgb(255, 15, 23, 42)) : new SolidColorBrush(Color.FromArgb(255, 248, 250, 252));
            TxtWaypointDesc.Foreground = tm.TextPrimary;
            TxtWaypointDesc.BorderBrush = tm.HeaderBorder;
        }
        if (TxtWaypointCoordinates != null)
        {
            TxtWaypointCoordinates.Background = isDark ? new SolidColorBrush(Color.FromArgb(255, 15, 23, 42)) : new SolidColorBrush(Color.FromArgb(255, 248, 250, 252));
            TxtWaypointCoordinates.Foreground = tm.TextPrimary;
            TxtWaypointCoordinates.BorderBrush = tm.HeaderBorder;
        }
        if (BtnCancelSaveWaypoint != null)
        {
            BtnCancelSaveWaypoint.Background = tm.ButtonInactiveBackground;
            BtnCancelSaveWaypoint.BorderBrush = tm.ButtonInactiveBorder;
            BtnCancelSaveWaypoint.Foreground = tm.ButtonInactiveForeground;
        }
        if (BtnConfirmSaveWaypoint != null)
        {
            BtnConfirmSaveWaypoint.Background = tm.ButtonActiveBackground;
            BtnConfirmSaveWaypoint.BorderBrush = tm.ButtonActiveBorder;
            BtnConfirmSaveWaypoint.Foreground = tm.ButtonActiveForeground;
        }

        // Update button states
        UpdateGpsButtonState();
        UpdateTrackLocationButtonsState();
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

        var pos = await GeolocationService.GetCurrentPositionAsync(timeoutSeconds: 8);
        if (pos != null)
        {
            _lastGpsPos = pos;
            bool movePointer = !_isGpsActive;

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
                UpdateLocationElevationBox(pos.Latitude, pos.Longitude);
                UpdateElevationDisplay(pos.Latitude, pos.Longitude);
            }

            string gpsText = $"GPS ±{pos.AccuracyM:F0}m";
            if (TxtGpsIndicator != null)
            {
                TxtGpsIndicator.Text = gpsText;
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

    private void BtnMenuCurrentLocation_Click(object sender, RoutedEventArgs e)
    {
        if (HamburgerPanel != null) HamburgerPanel.Visibility = Visibility.Collapsed;
        BtnCurrentLocation_Click(sender, e);
    }

    private async void BtnMarkWaypoint_Click(object sender, RoutedEventArgs e)
    {
        if (HamburgerPanel != null) HamburgerPanel.Visibility = Visibility.Collapsed;

        if (TxtGpsIndicator != null) TxtGpsIndicator.Text = "Acquiring GPS...";

        GeoPosition? pos = null;
        if (_isGpsActive && _lastGpsPos != null)
        {
            pos = _lastGpsPos;
        }
        else
        {
            pos = await GeolocationService.GetCurrentPositionAsync(timeoutSeconds: 8);
        }

        if (pos == null)
        {
            if (_lastGpsPos != null)
            {
                pos = _lastGpsPos;
            }
            else
            {
                if (TxtGpsIndicator != null)
                {
                    TxtGpsIndicator.Text = "GPS Fix Failed";
                    TxtGpsIndicator.Foreground = new SolidColorBrush(Colors.Red);
                }
                return;
            }
        }

        _lastGpsPos = pos;
        _lastPointerLat = pos.Latitude;
        _lastPointerLon = pos.Longitude;
        _hasPointerPlaced = true;

        // Mark on map: center on location and place pointer marker
        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.centerOnCurrentLocation) {
                    window.fullScreenMap.centerOnCurrentLocation($0, $1, true);
                }
            ", pos.Latitude, pos.Longitude);
        }
        catch { }

        string coordsText = $"📍 {pos.Latitude:F4}°, {pos.Longitude:F4}°";
        if (TxtPointerCoords != null) TxtPointerCoords.Text = coordsText;
        UpdateLocationElevationBox(pos.Latitude, pos.Longitude);
        UpdateElevationDisplay(pos.Latitude, pos.Longitude);

        // Calculate elevation: prefer GPS altitude, fallback to DEM
        double? eleM = pos.AltitudeM ?? DemService.GetElevation(pos.Latitude, pos.Longitude);

        _isEditingWaypoint = false;
        _pendingWaypointLat = pos.Latitude;
        _pendingWaypointLon = pos.Longitude;
        _pendingWaypointEle = eleM;

        // Query existing waypoint count from waypoints.gpx in default GPX directory
        string gpxDir = OfflineDataService.GpxDirectory;
        int count = await TrackParserService.GetExistingWaypointCountAsync(gpxDir);
        string defaultName = $"Waypoint {count + 1}";

        if (TxtSaveWaypointTitle != null) TxtSaveWaypointTitle.Text = "Save Waypoint";
        if (TxtWaypointName != null) TxtWaypointName.Text = defaultName;
        if (TxtWaypointDesc != null) TxtWaypointDesc.Text = string.Empty;
        if (TxtWaypointCoordinates != null)
        {
            TxtWaypointCoordinates.Text = $"{pos.Latitude.ToString("F6", CultureInfo.InvariantCulture)}, {pos.Longitude.ToString("F6", CultureInfo.InvariantCulture)}";
        }

        string eleStr = eleM.HasValue
            ? (ViewModel?.IsMetric ?? false ? $"{eleM.Value:F0} m" : $"{eleM.Value * 3.28084:F0} ft")
            : "--";
        string accStr = pos.AccuracyM.HasValue ? $"±{pos.AccuracyM.Value:F0}m" : "GPS";
        if (TxtWaypointDetails != null)
        {
            TxtWaypointDetails.Text = $"Elev: {eleStr} | Fix: {accStr} | {pos.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
        }

        if (BtnConfirmSaveWaypoint != null) BtnConfirmSaveWaypoint.Content = "Save Waypoint";

        if (SaveWaypointDialog != null)
        {
            if (_isSmallScreen)
            {
                SaveWaypointDialog.Width = Math.Min(380, ActualWidth > 0 ? ActualWidth - 16 : 380);
                SaveWaypointDialog.VerticalAlignment = VerticalAlignment.Top;
                SaveWaypointDialog.Margin = new Thickness(0, Math.Max(16, (ActualHeight > 600 ? 52 : 28)), 0, 0);
            }
            else
            {
                SaveWaypointDialog.Width = 380;
                SaveWaypointDialog.VerticalAlignment = VerticalAlignment.Center;
                SaveWaypointDialog.Margin = new Thickness(0);
            }
            SaveWaypointDialog.Visibility = Visibility.Visible;
        }
        if (SaveWaypointBackdrop != null) SaveWaypointBackdrop.Visibility = Visibility.Visible;
    }

    private void OpenEditWaypointDialog(string trackId, string waypointId)
    {
        var tracks = TrackIndexService.Tracks;
        TrackFileInfo? matchedTrack = null;
        WaypointInfo? matchedWaypoint = null;

        foreach (var t in tracks)
        {
            if (t.Id == trackId)
            {
                matchedWaypoint = t.Waypoints.FirstOrDefault(w => w.Id == waypointId);
                if (matchedWaypoint != null)
                {
                    matchedTrack = t;
                    break;
                }
            }
        }

        if (matchedWaypoint == null)
        {
            foreach (var t in tracks)
            {
                matchedWaypoint = t.Waypoints.FirstOrDefault(w => w.Id == waypointId);
                if (matchedWaypoint != null)
                {
                    matchedTrack = t;
                    break;
                }
            }
        }

        if (matchedTrack == null || matchedWaypoint == null) return;

        _isEditingWaypoint = true;
        _editingTrackId = matchedTrack.Id;
        _editingWaypointId = matchedWaypoint.Id;
        _editingFilePath = matchedTrack.FilePath;
        _editingOrigLat = matchedWaypoint.Latitude;
        _editingOrigLon = matchedWaypoint.Longitude;
        _pendingWaypointLat = matchedWaypoint.Latitude;
        _pendingWaypointLon = matchedWaypoint.Longitude;
        _pendingWaypointEle = matchedWaypoint.ElevationM;

        if (TxtSaveWaypointTitle != null) TxtSaveWaypointTitle.Text = "✏️ Edit Waypoint";
        if (TxtWaypointName != null) TxtWaypointName.Text = matchedWaypoint.Name;
        if (TxtWaypointDesc != null) TxtWaypointDesc.Text = matchedWaypoint.Description;
        if (TxtWaypointCoordinates != null)
        {
            TxtWaypointCoordinates.Text = $"{matchedWaypoint.Latitude.ToString("F6", CultureInfo.InvariantCulture)}, {matchedWaypoint.Longitude.ToString("F6", CultureInfo.InvariantCulture)}";
        }

        string eleStr = matchedWaypoint.ElevationM.HasValue
            ? (ViewModel?.IsMetric ?? false ? $"{matchedWaypoint.ElevationM.Value:F0} m" : $"{matchedWaypoint.ElevationM.Value * 3.28084:F0} ft")
            : "--";
        if (TxtWaypointDetails != null)
        {
            TxtWaypointDetails.Text = $"Source: {matchedTrack.FileName} | Elev: {eleStr}";
        }

        if (BtnConfirmSaveWaypoint != null) BtnConfirmSaveWaypoint.Content = "Save Changes";

        if (SaveWaypointDialog != null)
        {
            if (_isSmallScreen)
            {
                SaveWaypointDialog.Width = Math.Min(380, ActualWidth > 0 ? ActualWidth - 16 : 380);
                SaveWaypointDialog.VerticalAlignment = VerticalAlignment.Top;
                SaveWaypointDialog.Margin = new Thickness(0, Math.Max(16, (ActualHeight > 600 ? 52 : 28)), 0, 0);
            }
            else
            {
                SaveWaypointDialog.Width = 380;
                SaveWaypointDialog.VerticalAlignment = VerticalAlignment.Center;
                SaveWaypointDialog.Margin = new Thickness(0);
            }
            SaveWaypointDialog.Visibility = Visibility.Visible;
        }
        if (SaveWaypointBackdrop != null) SaveWaypointBackdrop.Visibility = Visibility.Visible;
    }

    private void BtnCancelSaveWaypoint_Click(object sender, RoutedEventArgs e)
    {
        if (TxtWaypointDesc != null) TxtWaypointDesc.Text = string.Empty;
        if (SaveWaypointDialog != null) SaveWaypointDialog.Visibility = Visibility.Collapsed;
        if (SaveWaypointBackdrop != null) SaveWaypointBackdrop.Visibility = Visibility.Collapsed;
    }

    private async void BtnConfirmSaveWaypoint_Click(object sender, RoutedEventArgs e)
    {
        string name = TxtWaypointName?.Text?.Trim() ?? "";
        string desc = TxtWaypointDesc?.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name))
        {
            name = $"Waypoint_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        }

        double lat = _pendingWaypointLat;
        double lon = _pendingWaypointLon;

        if (TxtWaypointCoordinates != null &&
            TrackParserService.TryParseCoordinates(TxtWaypointCoordinates.Text, out double parsedLat, out double parsedLon))
        {
            lat = parsedLat;
            lon = parsedLon;
        }

        double? ele = _pendingWaypointEle ?? DemService.GetElevation(lat, lon);

        // Hide dialog immediately
        if (TxtWaypointDesc != null) TxtWaypointDesc.Text = string.Empty;
        if (SaveWaypointDialog != null) SaveWaypointDialog.Visibility = Visibility.Collapsed;
        if (SaveWaypointBackdrop != null) SaveWaypointBackdrop.Visibility = Visibility.Collapsed;

        try
        {
            if (_isEditingWaypoint)
            {
                bool success = await TrackParserService.UpdateWaypointInGpxAsync(
                    _editingFilePath, _editingWaypointId, _editingOrigLat, _editingOrigLon, name, desc, lat, lon, ele);

                if (success)
                {
                    TrackIndexService.UpdateWaypoint(_editingTrackId, _editingWaypointId, name, desc, lat, lon, ele);
                    UpdateImportedTracksOnMap();
                    if (TxtGpsIndicator != null)
                    {
                        TxtGpsIndicator.Text = $"✏️ Updated '{name}'";
                        TxtGpsIndicator.Foreground = ThemeManager.Current.Accent;
                    }
                }
            }
            else
            {
                string gpxDir = OfflineDataService.GpxDirectory;
                string savedPath = await TrackParserService.AppendWaypointToGpxAsync(name, lat, lon, ele, gpxDir, desc);

                // Rescan track library so waypoints.gpx is indexed and visible
                await TrackIndexService.RefreshAsync(forceFullRescan: true);
                await RefreshSavedTracksSummaryAsync();

                if (TxtGpsIndicator != null)
                {
                    TxtGpsIndicator.Text = $"📌 Saved '{name}'";
                    TxtGpsIndicator.Foreground = ThemeManager.Current.Accent;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FullScreenMapControl] Error saving waypoint: {ex.Message}");
        }
    }

    private bool _is3dActive;

    /// <summary>
    /// Gets the current active location on the Main Map (GPS fix if tracking, placed marker, or map center).
    /// </summary>
    public (double Latitude, double Longitude, string Source) GetCurrentTargetLocation()
    {
        double targetLat;
        double targetLon;
        string source;

        // 1. Last GPS fix if tracking
        if (_isGpsActive && _lastGpsPos != null)
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

        return (targetLat, targetLon, source);
    }

    private void BtnMenuSetAimLocation_Click(object sender, RoutedEventArgs e)
    {
        var (targetLat, targetLon, source) = GetCurrentTargetLocation();

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

    private void BtnToggleGps_Click(object sender, RoutedEventArgs e)
    {
        if (_isRecordingActive)
        {
            return;
        }

        if (!_isGpsActive)
        {
            StartGps();
        }
        else
        {
            StopGps();
        }
    }

    private void StartGps()
    {
        _isGpsActive = true;
        UpdateGpsButtonState();

        if (TxtGpsIndicator != null)
        {
            TxtGpsIndicator.Text = "🟢 GPS Active";
            TxtGpsIndicator.Foreground = ThemeManager.Current.Accent;
        }
        _currentGpsStatusText = "GPS Active";
        GpsStatusChanged?.Invoke(this, _currentGpsStatusText);

        // Immediately notify map that GPS is active so icon switches immediately
        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.setGpsActive) {
                    window.fullScreenMap.setGpsActive(true);
                }
            ");

            if (_lastGpsPos != null)
            {
                Interop.ExecuteJavaScriptVoid(@"
                    if (window.fullScreenMap && window.fullScreenMap.updateLivePosition) {
                        window.fullScreenMap.updateLivePosition($0, $1, $2, $3, $4);
                    }
                ", _lastGpsPos.Latitude, _lastGpsPos.Longitude, _lastGpsPos.AccuracyM, _isTrackLocationActive, _lastHeading);
            }
        }
        catch { }

        GeolocationService.StartTracking(
            onUpdate: (pos) =>
            {
                if (!_isGpsActive) return;

                double? currentHeading = pos.Heading;
                if (!currentHeading.HasValue && _lastGpsPos != null)
                {
                    double distKm = DistanceKm(_lastGpsPos.Latitude, _lastGpsPos.Longitude, pos.Latitude, pos.Longitude);
                    if (distKm >= 0.003) // moved at least 3 meters
                    {
                        currentHeading = BearingDegrees(_lastGpsPos.Latitude, _lastGpsPos.Longitude, pos.Latitude, pos.Longitude);
                    }
                }

                if (currentHeading.HasValue)
                {
                    _lastHeading = currentHeading.Value;
                }

                _lastGpsPos = pos;
                Dispatcher.BeginInvoke(() =>
                {
                    if (!_isGpsActive) return;

                    try
                    {
                        Interop.ExecuteJavaScriptVoid(@"
                            if (window.fullScreenMap && window.fullScreenMap.updateLivePosition) {
                                window.fullScreenMap.updateLivePosition($0, $1, $2, $3, $4);
                            }
                        ", pos.Latitude, pos.Longitude, pos.AccuracyM, _isTrackLocationActive, _lastHeading);
                    }
                    catch { }

                    if (TxtGpsIndicator != null)
                    {
                        if (pos.AccuracyM.HasValue && pos.AccuracyM.Value > 65.0)
                        {
                            TxtGpsIndicator.Text = $"🟡 Coarse Fix ±{pos.AccuracyM:F0}m";
                            TxtGpsIndicator.Foreground = new SolidColorBrush(Color.FromArgb(255, 234, 179, 8)); // Amber
                            _currentGpsStatusText = $"Coarse Fix ±{pos.AccuracyM:F0}m";
                        }
                        else
                        {
                            TxtGpsIndicator.Text = $"🟢 GPS ±{pos.AccuracyM:F0}m";
                            TxtGpsIndicator.Foreground = ThemeManager.Current.Accent;
                            _currentGpsStatusText = $"GPS ±{pos.AccuracyM:F0}m";
                        }
                        GpsStatusChanged?.Invoke(this, _currentGpsStatusText);
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
                if (!_isGpsActive) return;
                Dispatcher.BeginInvoke(() =>
                {
                    if (!_isGpsActive) return;
                    if (TxtGpsIndicator != null)
                    {
                        TxtGpsIndicator.Text = "GPS Lost";
                        TxtGpsIndicator.Foreground = new SolidColorBrush(Colors.Red);
                        _currentGpsStatusText = "GPS Lost";
                        GpsStatusChanged?.Invoke(this, _currentGpsStatusText);
                    }
                });
            }
        );
    }

    private void StopGps(bool force = false)
    {
        if (_isRecordingActive && !force)
        {
            return;
        }

        _isGpsActive = false;
        _isTrackLocationActive = false;
        _lastHeading = null;
        GeolocationService.StopTracking();

        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.setGpsActive) {
                    window.fullScreenMap.setGpsActive(false);
                }
                if (window.fullScreenMap && window.fullScreenMap.stopLiveTracking) {
                    window.fullScreenMap.stopLiveTracking();
                }
            ");
        }
        catch { }

        UpdateGpsButtonState();
        UpdateTrackLocationButtonsState();

        if (TxtGpsIndicator != null)
        {
            TxtGpsIndicator.Text = "GPS Off";
            TxtGpsIndicator.Foreground = ThemeManager.Current.TextSecondary;
        }
        _currentGpsStatusText = "GPS Off";
        GpsStatusChanged?.Invoke(this, _currentGpsStatusText);
    }

    private void BtnToggleTrackLocation_Click(object sender, RoutedEventArgs e)
    {
        ToggleTrackLocation();
    }

    private void BtnMapTrackLocation_Click(object sender, RoutedEventArgs e)
    {
        ToggleTrackLocation();
    }

    private void ToggleTrackLocation()
    {
        _isTrackLocationActive = !_isTrackLocationActive;

        if (_isTrackLocationActive)
        {
            // If GPS is not active, turn it on
            if (!_isGpsActive)
            {
                StartGps();
            }

            // Immediately center map on last known GPS position if available
            if (_lastGpsPos != null)
            {
                try
                {
                    Interop.ExecuteJavaScriptVoid(@"
                        if (window.fullScreenMap && window.fullScreenMap.centerOnCurrentLocation) {
                            window.fullScreenMap.centerOnCurrentLocation($0, $1, false);
                        }
                    ", _lastGpsPos.Latitude, _lastGpsPos.Longitude);
                }
                catch { }
            }
        }

        UpdateTrackLocationButtonsState();
    }

    private void UpdateGpsButtonState()
    {
        var tm = ThemeManager.Current;
        if (BtnToggleGps == null) return;

        if (_isGpsActive)
        {
            if (_isRecordingActive)
            {
                if (TxtToggleGps != null) TxtToggleGps.Text = "GPS: ON (Recording Active)";
                ToolTipService.SetToolTip(BtnToggleGps, "GPS is required while recording a track. Stop track recording to turn off GPS.");
            }
            else
            {
                if (TxtToggleGps != null) TxtToggleGps.Text = "GPS: ON";
                ToolTipService.SetToolTip(BtnToggleGps, "Toggle GPS hardware receiver and position tracking");
            }
            BtnToggleGps.Background = tm.ButtonActiveBackground;
            BtnToggleGps.BorderBrush = tm.ButtonActiveBorder;
            BtnToggleGps.Foreground = tm.ButtonActiveForeground;
        }
        else
        {
            if (TxtToggleGps != null) TxtToggleGps.Text = "GPS: OFF";
            BtnToggleGps.Background = tm.ButtonInactiveBackground;
            BtnToggleGps.BorderBrush = tm.ButtonInactiveBorder;
            BtnToggleGps.Foreground = tm.ButtonInactiveForeground;
            ToolTipService.SetToolTip(BtnToggleGps, "Toggle GPS hardware receiver and position tracking");
        }

        GpsStateChanged?.Invoke(this, _isGpsActive);
    }

    private void UpdateTrackLocationButtonsState()
    {
        var tm = ThemeManager.Current;

        if (TxtToggleTrackLocation != null)
        {
            TxtToggleTrackLocation.Text = _isTrackLocationActive ? "Track Location: ON" : "Track Location: OFF";
        }

        if (IconMenuTrackLocation != null)
        {
            IconMenuTrackLocation.IsTracking = _isTrackLocationActive;
        }

        if (IconMapTrackLocation != null)
        {
            IconMapTrackLocation.IsTracking = _isTrackLocationActive;
        }

        if (BtnToggleTrackLocation != null)
        {
            if (_isTrackLocationActive)
            {
                BtnToggleTrackLocation.Background = tm.ButtonActiveBackground;
                BtnToggleTrackLocation.BorderBrush = tm.ButtonActiveBorder;
                BtnToggleTrackLocation.Foreground = tm.ButtonActiveForeground;
            }
            else
            {
                BtnToggleTrackLocation.Background = tm.ButtonInactiveBackground;
                BtnToggleTrackLocation.BorderBrush = tm.ButtonInactiveBorder;
                BtnToggleTrackLocation.Foreground = tm.ButtonInactiveForeground;
            }
        }

        if (BtnMapTrackLocation != null)
        {
            BtnMapTrackLocation.Opacity = 0.88;
            if (_isTrackLocationActive)
            {
                BtnMapTrackLocation.Background = tm.ButtonActiveBackground;
                BtnMapTrackLocation.BorderBrush = tm.ButtonActiveBorder;
                BtnMapTrackLocation.Foreground = tm.ButtonActiveForeground;
                ToolTipService.SetToolTip(BtnMapTrackLocation, "Track Location: ON (following position on map)");
            }
            else
            {
                BtnMapTrackLocation.Background = tm.ButtonInactiveBackground;
                BtnMapTrackLocation.BorderBrush = tm.ButtonInactiveBorder;
                BtnMapTrackLocation.Foreground = tm.ButtonInactiveForeground;
                ToolTipService.SetToolTip(BtnMapTrackLocation, "Track Location: OFF (click to follow position on map)");
            }
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

        if (_isGpsActive)
        {
            StopGps(force: true);
            StartGps();
        }
    }

    private void BtnInterval15s_Click(object sender, RoutedEventArgs e) => SetPowerInterval(15);
    private void BtnInterval1m_Click(object sender, RoutedEventArgs e) => SetPowerInterval(60);
    private void BtnInterval5m_Click(object sender, RoutedEventArgs e) => SetPowerInterval(300);
    private void BtnInterval15m_Click(object sender, RoutedEventArgs e) => SetPowerInterval(900);

    private void UpdateTracksSummaryButtonState()
    {
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
                if (window.fullScreenMap && window.fullScreenMap.setRecordingState) {
                    window.fullScreenMap.setRecordingState(true);
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

        // Ensure live GPS is on so positions stream in
        if (!_isGpsActive)
        {
            StartGps();
        }
        else
        {
            UpdateGpsButtonState();
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

        // Stopping Track recording turns off GPS automatically
        StopGps(force: true);

        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.hideElevationProfile) {
                    window.fullScreenMap.hideElevationProfile();
                }
                if (window.fullScreenMap && window.fullScreenMap.setRecordingState) {
                    window.fullScreenMap.setRecordingState(false);
                }
            ");
        }
        catch { }

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

            // Enforce configured power saver interval.
            // Note: Do not drop points when stationary (d < 15m) so continuous track points are
            // preserved for photo geotagging even during stops.
            if (timeSec < GeolocationService.PowerSaverIntervalSeconds) return;

            // Only accumulate distance if movement exceeds minimal GPS jitter (~2m)
            if (d >= 0.002)
            {
                _totalRecordedDistanceKm += d;
            }
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
                    window.fullScreenMap.addTrackPoint($0, $1, $2);
                }
            ", pos.Latitude, pos.Longitude, pos.AltitudeM ?? 0);
        }
        catch { }

        UpdateRecordingDisplay();
    }

    private void UpdateRecordingDisplay()
    {
        TimeSpan elapsed = _recordingStartTime.HasValue
            ? DateTime.UtcNow - _recordingStartTime.Value
            : TimeSpan.Zero;

        bool isMetric = ViewModel?.IsMetric ?? true;
        string distStr = isMetric
            ? $"{_totalRecordedDistanceKm:F2} km"
            : $"{_totalRecordedDistanceKm * 0.621371:F2} mi";

        string timeStr = elapsed.TotalHours >= 1
            ? $"{(int)elapsed.TotalHours}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}"
            : $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";

        if (TxtRecordingStats != null)
        {
            if (_isRecordingActive)
            {
                TxtRecordingStats.Text = $"🔴 Points: {_recordedPoints.Count} | {distStr} | {timeStr}";
                TxtRecordingStats.Visibility = Visibility.Visible;
            }
            else
            {
                TxtRecordingStats.Text = string.Empty;
                TxtRecordingStats.Visibility = Visibility.Collapsed;
            }
        }

        if (TxtLiveRecordingTime != null)
        {
            TxtLiveRecordingTime.Text = timeStr;
        }

        if (TxtLiveRecordingDist != null)
        {
            TxtLiveRecordingDist.Text = distStr;
        }

        if (LiveRecordingBar != null)
        {
            LiveRecordingBar.Visibility = _isRecordingActive ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void LiveRecordingBar_Click(object sender, RoutedEventArgs e)
    {
        ShowActiveRecordingTrackInfo();
    }

    public void ShowActiveRecordingTrackInfo()
    {
        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.toggleElevationProfile) {
                    window.fullScreenMap.toggleElevationProfile('active_gps_track');
                } else if (window.fullScreenMap && window.fullScreenMap.showElevationProfile) {
                    window.fullScreenMap.showElevationProfile('active_gps_track');
                }
            ");
        }
        catch { }
    }

    private void UpdateRecordingButtonState()
    {
        var tm = ThemeManager.Current;
        if (BtnToggleRecording == null) return;

        if (_isRecordingActive)
        {
            if (TxtToggleRecording != null) TxtToggleRecording.Text = "Stop Recording";
            if (IconMenuRecordTrack != null) IconMenuRecordTrack.IsRecording = true;
            BtnToggleRecording.Background = new SolidColorBrush(Color.FromArgb(255, 239, 68, 68)); // Red-500
            BtnToggleRecording.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 220, 38, 38));
            BtnToggleRecording.Foreground = new SolidColorBrush(Colors.White);
        }
        else
        {
            if (TxtToggleRecording != null) TxtToggleRecording.Text = "Record Track";
            if (IconMenuRecordTrack != null) IconMenuRecordTrack.IsRecording = false;
            BtnToggleRecording.Background = tm.ButtonInactiveBackground;
            BtnToggleRecording.BorderBrush = tm.ButtonInactiveBorder;
            BtnToggleRecording.Foreground = tm.ButtonInactiveForeground;
        }

        RecordingStateChanged?.Invoke(this, _isRecordingActive);
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
                if (window.fullScreenMap && window.fullScreenMap.setRecordingState) {
                    window.fullScreenMap.setRecordingState(true);
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
    private void BtnLayerQuads_Click(object sender, RoutedEventArgs e) => SetBaseLayer("quads");
    private void BtnLayerOpenTopo_Click(object sender, RoutedEventArgs e) => SetBaseLayer("opentopo");
    private void BtnLayerHybrid_Click(object sender, RoutedEventArgs e) => SetBaseLayer("hybrid");

    private void BtnTogglePadUs_Click(object sender, RoutedEventArgs e)
    {
        _isPadUsVisible = !_isPadUsVisible;
        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.setPadUsVisible) {
                    window.fullScreenMap.setPadUsVisible($0);
                }
            ", _isPadUsVisible);
        }
        catch { }

        UpdateLayerButtons();
    }

    private void BtnToggleWeatherTemp_Click(object sender, RoutedEventArgs e)
    {
        _isWeatherTempVisible = !_isWeatherTempVisible;
        MapTilerService.SetOverlayVisible("temperature", _isWeatherTempVisible);
        SendWeatherOverlayVisibility("temperature", _isWeatherTempVisible);
        UpdateWeatherButtons();
    }

    private void BtnToggleWeatherWind_Click(object sender, RoutedEventArgs e)
    {
        _isWeatherWindVisible = !_isWeatherWindVisible;
        MapTilerService.SetOverlayVisible("wind", _isWeatherWindVisible);
        SendWeatherOverlayVisibility("wind", _isWeatherWindVisible);
        UpdateWeatherButtons();
    }

    private void BtnToggleWeatherRadar_Click(object sender, RoutedEventArgs e)
    {
        _isWeatherRadarVisible = !_isWeatherRadarVisible;
        MapTilerService.SetOverlayVisible("radar", _isWeatherRadarVisible);
        SendWeatherOverlayVisibility("radar", _isWeatherRadarVisible);
        UpdateWeatherButtons();
    }

    private void BtnToggleWeatherPrecip_Click(object sender, RoutedEventArgs e)
    {
        _isWeatherPrecipVisible = !_isWeatherPrecipVisible;
        MapTilerService.SetOverlayVisible("precipitation", _isWeatherPrecipVisible);
        SendWeatherOverlayVisibility("precipitation", _isWeatherPrecipVisible);
        UpdateWeatherButtons();
    }

    private void BtnToggleWeatherCloud_Click(object sender, RoutedEventArgs e)
    {
        _isWeatherCloudVisible = !_isWeatherCloudVisible;
        MapTilerService.SetOverlayVisible("clouds", _isWeatherCloudVisible);
        SendWeatherOverlayVisibility("clouds", _isWeatherCloudVisible);
        UpdateWeatherButtons();
    }

    private void BtnToggleWeatherPressure_Click(object sender, RoutedEventArgs e)
    {
        _isWeatherPressureVisible = !_isWeatherPressureVisible;
        MapTilerService.SetOverlayVisible("pressure", _isWeatherPressureVisible);
        SendWeatherOverlayVisibility("pressure", _isWeatherPressureVisible);
        UpdateWeatherButtons();
    }

    private void SendWeatherOverlayVisibility(string overlayType, bool visible)
    {
        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.setWeatherOverlayVisible) {
                    window.fullScreenMap.setWeatherOverlayVisible($0, $1);
                }
            ", overlayType, visible);
        }
        catch { }
    }

    private void OnMapTilerApiKeyChanged()
    {
        // When the API key changes in the options during runtime, turn off all weather layers
        _isWeatherTempVisible = false;
        _isWeatherWindVisible = false;
        _isWeatherRadarVisible = false;
        _isWeatherPrecipVisible = false;
        _isWeatherCloudVisible = false;
        _isWeatherPressureVisible = false;

        UpdateMapTilerState();
    }

    public void UpdateMapTilerState()
    {
        bool isConfigured = MapTilerService.IsConfigured;
        if (MenuWeatherSection != null)
        {
            MenuWeatherSection.Visibility = isConfigured ? Visibility.Visible : Visibility.Collapsed;
        }

        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.fullScreenMap && window.fullScreenMap.setMapTilerApiKey) {
                    window.fullScreenMap.setMapTilerApiKey($0);
                }
            ", MapTilerService.ApiKey ?? "");
        }
        catch { }

        UpdateWeatherButtons();
    }

    private void UpdateWeatherButtons()
    {
        var tm = ThemeManager.Current;

        void StyleWeatherBtn(Button? btn, string title, string emoji, bool active)
        {
            if (btn == null) return;
            btn.Content = $"{emoji} {title}: {(active ? "ON" : "OFF")}";
            btn.Background = active ? tm.ButtonActiveBackground : tm.ButtonInactiveBackground;
            btn.BorderBrush = active ? tm.ButtonActiveBorder : tm.ButtonInactiveBorder;
            btn.Foreground = active ? tm.ButtonActiveForeground : tm.ButtonInactiveForeground;
            btn.FontWeight = active ? FontWeights.Bold : FontWeights.SemiBold;
        }

        StyleWeatherBtn(BtnToggleWeatherTemp, "Temperature", "🌡️", _isWeatherTempVisible);
        StyleWeatherBtn(BtnToggleWeatherWind, "Wind", "💨", _isWeatherWindVisible);
        StyleWeatherBtn(BtnToggleWeatherRadar, "Radar", "📡", _isWeatherRadarVisible);
        StyleWeatherBtn(BtnToggleWeatherPrecip, "Rain & Snow", "🌧️", _isWeatherPrecipVisible);
        StyleWeatherBtn(BtnToggleWeatherCloud, "Cloud Cover", "☁️", _isWeatherCloudVisible);
        StyleWeatherBtn(BtnToggleWeatherPressure, "Pressure", "⏱️", _isWeatherPressureVisible);

        if (TxtSectionWeather != null) TxtSectionWeather.Foreground = tm.TextSecondary;
        if (MenuDividerWeather != null) MenuDividerWeather.Background = tm.HeaderBorder;
    }

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

    public const string LastLayerStorageKey = "pointedhorizons_last_map_layer";

    private static string GetLastLayerFilePath()
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appData, "PointedHorizons", "last_map_layer.txt");
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string LoadLastUsedBaseLayer()
    {
        try
        {
            string localFilePath = GetLastLayerFilePath();
            if (!string.IsNullOrEmpty(localFilePath) && File.Exists(localFilePath))
            {
                string saved = File.ReadAllText(localFilePath).Trim().ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(saved))
                {
                    if (saved == "pmtiles" && !OfflineDataService.IsPmTilesSupported) return "topo";
                    return saved;
                }
            }
        }
        catch { }

        try
        {
            object? val = Interop.ExecuteJavaScript("localStorage.getItem($0)", LastLayerStorageKey);
            if (val is string str && !string.IsNullOrWhiteSpace(str))
            {
                str = str.Trim().ToLowerInvariant();
                if (str == "pmtiles" && !OfflineDataService.IsPmTilesSupported) return "topo";
                return str;
            }
        }
        catch { }
        return "topo";
    }

    public static void SaveLastUsedBaseLayer(string layerName)
    {
        try
        {
            Interop.ExecuteJavaScriptVoid("localStorage.setItem($0, $1)", LastLayerStorageKey, layerName);
        }
        catch { }

        try
        {
            string localFilePath = GetLastLayerFilePath();
            if (!string.IsNullOrEmpty(localFilePath))
            {
                string? dir = Path.GetDirectoryName(localFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(localFilePath, layerName);
            }
        }
        catch { }
    }

    public void SetBaseLayer(string layerName)
    {
        if (!OfflineDataService.IsPmTilesSupported && layerName == "pmtiles")
        {
            layerName = "topo";
        }

        _currentBaseLayer = layerName;
        SaveLastUsedBaseLayer(layerName);
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
        StyleLayerBtn(BtnLayerQuads, _currentBaseLayer == "quads");
        StyleLayerBtn(BtnLayerOpenTopo, _currentBaseLayer == "opentopo");
        StyleLayerBtn(BtnLayerHybrid, _currentBaseLayer == "hybrid");

        if (BtnTogglePadUs != null)
        {
            BtnTogglePadUs.Content = _isPadUsVisible
                ? "🌲 Public Lands (PAD-US): ON"
                : "🌲 Public Lands (PAD-US): OFF";
            BtnTogglePadUs.Background = _isPadUsVisible ? tm.ButtonActiveBackground : tm.ButtonInactiveBackground;
            BtnTogglePadUs.BorderBrush = _isPadUsVisible ? tm.ButtonActiveBorder : tm.ButtonInactiveBorder;
            BtnTogglePadUs.Foreground = _isPadUsVisible ? tm.ButtonActiveForeground : tm.ButtonInactiveForeground;
            BtnTogglePadUs.FontWeight = _isPadUsVisible ? FontWeights.Bold : FontWeights.SemiBold;
        }

        if (BtnConfigurePmTiles != null)
        {
            BtnConfigurePmTiles.Background = tm.ButtonInactiveBackground;
            BtnConfigurePmTiles.BorderBrush = tm.ButtonInactiveBorder;
            BtnConfigurePmTiles.Foreground = tm.TextPrimary;
        }

        UpdateWeatherButtons();
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

    private static double BearingDegrees(double lat1, double lon1, double lat2, double lon2)
    {
        double phi1 = lat1 * Math.PI / 180.0;
        double phi2 = lat2 * Math.PI / 180.0;
        double deltaLambda = (lon2 - lon1) * Math.PI / 180.0;

        double y = Math.Sin(deltaLambda) * Math.Cos(phi2);
        double x = Math.Cos(phi1) * Math.Sin(phi2) - Math.Sin(phi1) * Math.Cos(phi2) * Math.Cos(deltaLambda);
        double theta = Math.Atan2(y, x);
        return (theta * 180.0 / Math.PI + 360.0) % 360.0;
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

        string rawElevText;
        if (elev.HasValue && elev.Value > -500)
        {
            double m = elev.Value;
            double ft = m * 3.28084;
            rawElevText = isMetric
                ? $"{Math.Round(m):N0} m"
                : $"{Math.Round(ft):N0} ft";
        }
        else if (ViewModel != null && Math.Abs(ViewModel.Latitude - lat) < 0.001 && Math.Abs(ViewModel.Longitude - lon) < 0.001)
        {
            rawElevText = ViewModel.ObserverElevationDisplay;
        }
        else
        {
            rawElevText = "--";
        }

        _lastElevationFormatted = rawElevText;
        string elevText = $"⛰️ {rawElevText}";

        if (TxtPointerElevation != null)
        {
            TxtPointerElevation.Text = elevText;
        }
        UpdateLocationElevationBox(lat, lon);
    }
}
