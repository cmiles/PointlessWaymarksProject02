using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OpenSilver;
using PointedHorizons.Models;
using PointedHorizons.Services;
using PointedHorizons.ViewModels;

namespace PointedHorizons.Controls;

public partial class LeafletMapControl : UserControl
{
    private bool _isMapInitialized;
    private bool _isMapInitializing;
    private bool _isUpdatingFromInternal;

    public event EventHandler? ConfigurePmTilesRequested;
    public event EventHandler<CelestialLocationEventArgs>? CelestialInfoRequested;

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public LeafletMapControl()
    {
        InitializeComponent();
        Loaded += LeafletMapControl_Loaded;
        SizeChanged += LeafletMapControl_SizeChanged;
        DataContextChanged += LeafletMapControl_DataContextChanged;

        ThemeManager.Current.ThemeChanged += (s, e) =>
        {
            Dispatcher.BeginInvoke(() => UpdateTheme(ThemeManager.Current.IsDark));
        };

        OfflineDataService.ConfigChanged += (s, e) =>
        {
            Dispatcher.BeginInvoke(UpdatePmTilesLayers);
        };

        LeafletScrollIsolationHelper.ProtectControl(this, MapContainer);
    }

    private void LeafletMapControl_Loaded(object sender, RoutedEventArgs e)
    {
        OfflineDataService.EnsureSettingsLoaded();
        UpdateTheme(ThemeManager.Current.IsDark);
        UpdateLayerButtons();
        InitializeMap();
    }

    private void LeafletMapControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Hide direction bar (with directions and slider) on small screens
        bool isSmallScreen = e.NewSize.Width < 768 || (e.NewSize.Height > 0 && e.NewSize.Height < 600);
        if (DirectionToolbarBorder != null)
        {
            DirectionToolbarBorder.Visibility = isSmallScreen ? Visibility.Collapsed : Visibility.Visible;
        }

        if (!isSmallScreen)
        {
            bool isCompact = e.NewSize.Width < 900;
            if (BtnCardNE != null) BtnCardNE.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
            if (BtnCardSE != null) BtnCardSE.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
            if (BtnCardSW != null) BtnCardSW.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
            if (BtnCardNW != null) BtnCardNW.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
            if (TxtDirectionLabel != null) TxtDirectionLabel.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        }

        if (HamburgerPanel != null)
        {
            if (isSmallScreen)
            {
                HamburgerPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                HamburgerPanel.Width = double.NaN;
                HamburgerPanel.Margin = new Thickness(10, 50, 10, 10);
                HamburgerPanel.MaxHeight = Math.Max(260, e.NewSize.Height - 65);
            }
            else
            {
                HamburgerPanel.HorizontalAlignment = HorizontalAlignment.Left;
                HamburgerPanel.Width = 285;
                HamburgerPanel.Margin = new Thickness(12, 50, 0, 0);
                HamburgerPanel.MaxHeight = 520;
            }
        }

        ResizeMap();
    }

    public void ResizeMap()
    {
        if (_isMapInitialized)
        {
            try
            {
                Interop.ExecuteJavaScriptVoid("if (window.pointedMap && window.pointedMap.invalidateSize) { window.pointedMap.invalidateSize(); }");
            }
            catch { }
        }
        else
        {
            InitializeMap();
        }
    }

    private void LeafletMapControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldVm)
        {
            oldVm.MapLocationRequested -= Vm_MapLocationRequested;
            oldVm.MapDirectionRequested -= Vm_MapDirectionRequested;
            oldVm.MapFovRequested -= Vm_MapFovRequested;
            oldVm.FarthestVisibleLineCalculated -= Vm_FarthestVisibleLineCalculated;
            oldVm.FarthestVisibleLineCleared -= Vm_FarthestVisibleLineCleared;
        }

        if (e.NewValue is MainViewModel newVm)
        {
            newVm.MapLocationRequested += Vm_MapLocationRequested;
            newVm.MapDirectionRequested += Vm_MapDirectionRequested;
            newVm.MapFovRequested += Vm_MapFovRequested;
            newVm.FarthestVisibleLineCalculated += Vm_FarthestVisibleLineCalculated;
            newVm.FarthestVisibleLineCleared += Vm_FarthestVisibleLineCleared;

            if (_isMapInitialized)
            {
                SyncMapFromViewModel(panTo: true);
            }
        }
    }

    public void UpdateTheme(bool isDark)
    {
        var tm = ThemeManager.Current;

        if (MapRootGrid != null)
        {
            MapRootGrid.Background = isDark
                ? new SolidColorBrush(Color.FromArgb(255, 11, 15, 25))
                : new SolidColorBrush(Color.FromArgb(255, 241, 245, 249));
        }

        if (MapContainer != null)
        {
            MapContainer.Background = isDark
                ? new SolidColorBrush(Color.FromArgb(255, 15, 23, 42))
                : new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        }

        var overlayBg = tm.MapOverlayBackground;
        var overlayBorder = tm.MapOverlayBorder;

        if (DirectionToolbarBorder != null)
        {
            DirectionToolbarBorder.Background = overlayBg;
            DirectionToolbarBorder.BorderBrush = overlayBorder;
        }

        if (LocationBadgeBorder != null)
        {
            LocationBadgeBorder.Background = overlayBg;
            LocationBadgeBorder.BorderBrush = overlayBorder;
        }

        if (BtnMyLocation != null)
        {
            BtnMyLocation.Background = tm.ButtonInactiveBackground;
            BtnMyLocation.BorderBrush = tm.ButtonInactiveBorder;
            BtnMyLocation.Foreground = tm.Accent;
        }

        if (BtnAim3d != null)
        {
            BtnAim3d.Background = _isAim3dActive ? tm.ButtonActiveBackground : tm.ButtonInactiveBackground;
            BtnAim3d.BorderBrush = _isAim3dActive ? tm.ButtonActiveBorder : tm.ButtonInactiveBorder;
            BtnAim3d.Foreground = _isAim3dActive ? tm.ButtonActiveForeground : tm.TextPrimary;
        }

        if (BtnAimReset != null)
        {
            BtnAimReset.Background = tm.ButtonInactiveBackground;
            BtnAimReset.BorderBrush = tm.ButtonInactiveBorder;
            BtnAimReset.Foreground = tm.TextPrimary;
        }

        if (BtnMenuAimReset != null)
        {
            BtnMenuAimReset.Background = tm.ButtonInactiveBackground;
            BtnMenuAimReset.BorderBrush = tm.ButtonInactiveBorder;
            BtnMenuAimReset.Foreground = tm.Accent;
        }

        if (BtnMenuCelestialInfo != null)
        {
            BtnMenuCelestialInfo.Background = tm.ButtonInactiveBackground;
            BtnMenuCelestialInfo.BorderBrush = tm.ButtonInactiveBorder;
            BtnMenuCelestialInfo.Foreground = tm.TextPrimary;
        }

        if (BtnHamburger != null)
        {
            BtnHamburger.Background = tm.ButtonInactiveBackground;
            BtnHamburger.BorderBrush = tm.ButtonInactiveBorder;
            BtnHamburger.Foreground = tm.TextPrimary;
        }

        if (HamburgerPanel != null)
        {
            HamburgerPanel.Background = overlayBg;
            HamburgerPanel.BorderBrush = overlayBorder;
        }

        if (TxtMenuTitle != null) TxtMenuTitle.Foreground = tm.TextPrimary;
        if (TxtSectionLayer != null) TxtSectionLayer.Foreground = tm.TextSecondary;
        if (TxtSectionPresets != null) TxtSectionPresets.Foreground = tm.TextSecondary;
        if (TxtSectionViews != null) TxtSectionViews.Foreground = tm.TextSecondary;
        if (MenuDivider != null) MenuDivider.Background = overlayBorder;
        if (BtnCloseMenu != null)
        {
            BtnCloseMenu.Background = tm.ButtonInactiveBackground;
            BtnCloseMenu.BorderBrush = tm.ButtonInactiveBorder;
            BtnCloseMenu.Foreground = tm.TextPrimary;
        }

        UpdateLayerButtons();
        UpdatePresetButtonsTheme(isDark);

        if (ToolbarDivider != null)
        {
            ToolbarDivider.Background = overlayBorder;
        }

        if (MenuDivider2 != null) MenuDivider2.Background = overlayBorder;
        if (MenuDivider3 != null) MenuDivider3.Background = overlayBorder;

        if (TxtDirectionLabel != null) TxtDirectionLabel.Foreground = tm.TextSecondary;
        if (TxtAngleLabel != null) TxtAngleLabel.Foreground = tm.TextSecondary;
        if (TxtAzimuthValue != null) TxtAzimuthValue.Foreground = tm.TextPrimary;

        if (TxtCoordinatesValue != null) TxtCoordinatesValue.Foreground = tm.TextPrimary;
        if (TxtElevationLabel != null) TxtElevationLabel.Foreground = tm.TextSecondary;
        if (TxtElevationValue != null) TxtElevationValue.Foreground = tm.Accent;

        void StyleMajorCard(Button btn)
        {
            if (btn == null) return;
            btn.Background = tm.CardinalMajorBackground;
            btn.BorderBrush = tm.CardinalMajorBorder;
            btn.Foreground = tm.CardinalMajorForeground;
        }

        void StyleMinorCard(Button btn)
        {
            if (btn == null) return;
            btn.Background = tm.CardinalMinorBackground;
            btn.BorderBrush = tm.CardinalMinorBorder;
            btn.Foreground = tm.CardinalMinorForeground;
        }

        StyleMajorCard(BtnCardN);
        StyleMajorCard(BtnCardE);
        StyleMajorCard(BtnCardS);
        StyleMajorCard(BtnCardW);

        StyleMinorCard(BtnCardNE);
        StyleMinorCard(BtnCardSE);
        StyleMinorCard(BtnCardSW);
        StyleMinorCard(BtnCardNW);
    }

    private void Vm_MapLocationRequested(double lat, double lon, bool panTo)
    {
        if (!_isMapInitialized) return;
        try
        {
            Interop.ExecuteJavaScriptVoid("if (window.pointedMap && window.pointedMap.setLocation) { window.pointedMap.setLocation($0, $1, $2); }", lat, lon, panTo);
        }
        catch
        {
        }
    }

    private void Vm_MapDirectionRequested(double bearing)
    {
        if (!_isMapInitialized) return;
        try
        {
            Interop.ExecuteJavaScriptVoid("if (window.pointedMap && window.pointedMap.setDirection) { window.pointedMap.setDirection($0); }", bearing);
        }
        catch
        {
        }
    }

    private void Vm_MapFovRequested(double fov)
    {
        if (!_isMapInitialized) return;
        try
        {
            Interop.ExecuteJavaScriptVoid("if (window.pointedMap && window.pointedMap.setFieldOfView) { window.pointedMap.setFieldOfView($0); }", fov);
        }
        catch
        {
        }
    }

    private void Vm_FarthestVisibleLineCalculated(double lat, double lon, double distKm, double elevM, double bearing)
    {
        if (!_isMapInitialized) return;
        try
        {
            Interop.ExecuteJavaScriptVoid("if (window.pointedMap && window.pointedMap.setFarthestVisibleLine) { window.pointedMap.setFarthestVisibleLine($0, $1, $2, $3, $4); }", lat, lon, distKm, elevM, bearing);
        }
        catch
        {
        }
    }

    private void Vm_FarthestVisibleLineCleared()
    {
        if (!_isMapInitialized) return;
        try
        {
            Interop.ExecuteJavaScriptVoid("if (window.pointedMap && window.pointedMap.clearFarthestVisibleLine) { window.pointedMap.clearFarthestVisibleLine(); }");
        }
        catch
        {
        }
    }

    private async void BtnMyLocation_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;
        ViewModel.StatusMessage = "Acquiring current GPS location...";
        var pos = await GeolocationService.GetCurrentPositionAsync(timeoutSeconds: 8);
        if (pos != null)
        {
            ViewModel.SetLocation(pos.Latitude, pos.Longitude, "Current Location", panMap: true);
            ViewModel.StatusMessage = $"Moved to GPS location: {pos.Latitude:F4}°, {pos.Longitude:F4}° (±{pos.AccuracyM:F0}m)";
        }
        else
        {
            ViewModel.StatusMessage = "GPS location unavailable or permission denied.";
        }
    }

    private bool _isAim3dActive;

    private void BtnMenuCelestialInfo_Click(object sender, RoutedEventArgs e)
    {
        if (HamburgerPanel != null)
        {
            HamburgerPanel.Visibility = Visibility.Collapsed;
        }

        double lat = ViewModel?.Latitude ?? 32.2226;
        double lon = ViewModel?.Longitude ?? -110.9747;
        CelestialInfoRequested?.Invoke(this, new CelestialLocationEventArgs(lat, lon));
    }

    private void BtnAim3d_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _isAim3dActive = !_isAim3dActive;
            var tm = ThemeManager.Current;
            BtnAim3d.Content = _isAim3dActive ? "🗺️ 2D" : "🏔️ 3D";
            BtnAim3d.Background = _isAim3dActive ? tm.ButtonActiveBackground : tm.ButtonInactiveBackground;
            BtnAim3d.BorderBrush = _isAim3dActive ? tm.ButtonActiveBorder : tm.ButtonInactiveBorder;
            BtnAim3d.Foreground = _isAim3dActive ? tm.ButtonActiveForeground : tm.TextPrimary;
            ToolTipService.SetToolTip(BtnAim3d, _isAim3dActive ? "Switch to flat 2D top-down view" : "Switch to 3D terrain perspective view along azimuth");

            Interop.ExecuteJavaScriptVoid(@"
                if (window.pointedMap && window.pointedMap.toggle3d) {
                    window.pointedMap.toggle3d($0);
                }
            ", !_isAim3dActive);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LeafletMap] Error toggling 3D: {ex.Message}");
        }
    }

    private void BtnAimReset_Click(object sender, RoutedEventArgs e)
    {
        _isAim3dActive = false;
        if (BtnAim3d != null)
        {
            var tm = ThemeManager.Current;
            BtnAim3d.Content = "🏔️ 3D";
            BtnAim3d.Background = tm.ButtonInactiveBackground;
            BtnAim3d.BorderBrush = tm.ButtonInactiveBorder;
            BtnAim3d.Foreground = tm.TextPrimary;
            ToolTipService.SetToolTip(BtnAim3d, "Toggle 3D terrain perspective view along azimuth");
        }
        if (HamburgerPanel != null)
        {
            HamburgerPanel.Visibility = Visibility.Collapsed;
        }

        try
        {
            Interop.ExecuteJavaScriptVoid("if (window.pointedMap && window.pointedMap.resetView) { window.pointedMap.resetView(); }");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LeafletMap] Error in resetView: {ex.Message}");
        }
    }

    private void InitializeMap()
    {
        if (_isMapInitialized || _isMapInitializing) return;

        OfflineDataService.EnsureSettingsLoaded();

        object div = Interop.GetDiv(MapContainer);
        if (div == null)
        {
            Dispatcher.BeginInvoke(InitializeMap);
            return;
        }

        _isMapInitializing = true;

        Action<double, double> onLocationPicked = (lat, lon) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (ViewModel != null)
                {
                    _isUpdatingFromInternal = true;
                    try
                    {
                        ViewModel.UpdateLocationFromMap(lat, lon);
                    }
                    finally
                    {
                        _isUpdatingFromInternal = false;
                    }
                }
            });
        };

        Action<double> onDirectionChanged = (bearing) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (ViewModel != null)
                {
                    _isUpdatingFromInternal = true;
                    try
                    {
                        ViewModel.UpdateDirectionFromMap(bearing);
                    }
                    finally
                    {
                        _isUpdatingFromInternal = false;
                    }
                }
            });
        };

        Action onConfigurePmTiles = () =>
        {
            Dispatcher.BeginInvoke(() => ConfigurePmTilesRequested?.Invoke(this, EventArgs.Empty));
        };

        double initLat = ViewModel?.Latitude ?? 32.4431;
        double initLon = ViewModel?.Longitude ?? -110.7884;
        double initBearing = ViewModel?.Azimuth ?? 270.0;
        double initFov = ViewModel?.FieldOfView ?? 90.0;

        bool pmtilesSupported = OfflineDataService.IsPmTilesSupported;
        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                (function(container, lat, lon, bearing, fov, locCb, dirCb, cfgCb, pmtilesSupported) {
                    function tryInit() {
                        if (typeof maplibregl === 'undefined' || !window.pointedMap || !window.pointedMap.init) {
                            setTimeout(tryInit, 50);
                            return;
                        }
                        window.pointedMap.isPmTilesSupported = pmtilesSupported;
                        window.pointedMap.onConfigurePmTilesRequested = cfgCb;
                        window.pointedMap.init(container, {
                            lat: lat,
                            lon: lon,
                            bearing: bearing,
                            fov: fov
                        }, function(newLat, newLon) {
                            if (locCb) locCb(newLat, newLon);
                        }, function(newBearing) {
                            if (dirCb) dirCb(newBearing);
                        });
                    }
                    tryInit();
                })($0, $1, $2, $3, $4, $5, $6, $7, $8);
            ", div, initLat, initLon, initBearing, initFov, onLocationPicked, onDirectionChanged, onConfigurePmTiles, pmtilesSupported);

            _isMapInitialized = true;
            if (pmtilesSupported)
            {
                UpdatePmTilesLayers();
            }
        }
        catch
        {
        }
        finally
        {
            _isMapInitializing = false;
        }
    }

    private void SyncMapFromViewModel(bool panTo = false)
    {
        if (ViewModel == null || !_isMapInitialized) return;

        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.pointedMap) {
                    if (window.pointedMap.setLocation) window.pointedMap.setLocation($0, $1, $2);
                    if (window.pointedMap.setDirection) window.pointedMap.setDirection($3);
                    if (window.pointedMap.setFieldOfView) window.pointedMap.setFieldOfView($4);
                }
            ", ViewModel.Latitude, ViewModel.Longitude, panTo, ViewModel.Azimuth, ViewModel.FieldOfView);
        }
        catch
        {
        }
    }

    public void UpdatePmTilesLayers()
    {
        if (!_isMapInitialized || !OfflineDataService.IsPmTilesSupported) return;
        try
        {
            var payload = OfflineDataService.GetPmTilesConfigPayload();
            string json = System.Text.Json.JsonSerializer.Serialize(payload);
            Interop.ExecuteJavaScriptVoid("if (window.pointedMap && window.pointedMap.setPmTilesLayers) { window.pointedMap.setPmTilesLayers(JSON.parse($0)); }", json);
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LeafletMapControl] UpdatePmTilesLayers error: {ex.Message}");
        }
    }

    private string _currentBaseLayer = "osm";
    private bool _isPadUsVisible;

    public void SetBaseLayer(string layerKey)
    {
        if (!OfflineDataService.IsPmTilesSupported && layerKey == "pmtiles")
        {
            layerKey = "osm";
        }

        _currentBaseLayer = layerKey;
        if (!_isMapInitialized) return;
        try
        {
            Interop.ExecuteJavaScriptVoid("if (window.pointedMap && window.pointedMap.setBaseLayer) { window.pointedMap.setBaseLayer($0); }", layerKey);
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LeafletMapControl] SetBaseLayer error: {ex.Message}");
        }

        UpdateLayerButtons();
    }

    private void BtnHamburger_Click(object sender, RoutedEventArgs e)
    {
        if (HamburgerPanel == null) return;
        HamburgerPanel.Visibility = (HamburgerPanel.Visibility == Visibility.Visible)
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (HamburgerPanel.Visibility == Visibility.Visible)
        {
            UpdatePresetButtonsTheme(ThemeManager.Current.IsDark);
        }
    }

    private void BtnCloseMenu_Click(object sender, RoutedEventArgs e)
    {
        if (HamburgerPanel != null) HamburgerPanel.Visibility = Visibility.Collapsed;
    }

    private void ApplyPresetAndClose(string name)
    {
        var preset = ViewModel?.Presets.FirstOrDefault(p => p.Name == name);
        if (preset != null && ViewModel != null)
        {
            ViewModel.ApplyPreset(preset);
            UpdatePresetButtonsTheme(ThemeManager.Current.IsDark);
        }
        if (HamburgerPanel != null) HamburgerPanel.Visibility = Visibility.Collapsed;
    }

    private void BtnPresetRincon_Click(object sender, RoutedEventArgs e) =>
        ApplyPresetAndClose("Tucson East (Rincon Ridges), AZ");

    private void BtnPresetLemmon_Click(object sender, RoutedEventArgs e) =>
        ApplyPresetAndClose("Mount Lemmon, AZ");

    private void BtnPresetRainier_Click(object sender, RoutedEventArgs e) =>
        ApplyPresetAndClose("Mount Rainier (Paradise), WA");

    private void BtnPresetGrandCanyon_Click(object sender, RoutedEventArgs e) =>
        ApplyPresetAndClose("Grand Canyon (Mather Point), AZ");

    private void BtnPresetPikes_Click(object sender, RoutedEventArgs e) =>
        ApplyPresetAndClose("Pikes Peak, CO");

    private void BtnPresetYosemite_Click(object sender, RoutedEventArgs e) =>
        ApplyPresetAndClose("Yosemite (Glacier Point), CA");

    private void BtnPresetWashington_Click(object sender, RoutedEventArgs e) =>
        ApplyPresetAndClose("Mount Washington, NH");

    private void UpdatePresetButtonsTheme(bool isDark)
    {
        string? selectedName = ViewModel?.LocationPresetName;
        var tm = ThemeManager.Current;

        var selectedBg = tm.ButtonActiveBackground;
        var selectedBorder = tm.ButtonActiveBorder;
        var selectedFg = tm.ButtonActiveForeground;

        var unselectedBg = tm.ButtonInactiveBackground;
        var unselectedBorder = tm.ButtonInactiveBorder;
        var unselectedFg = tm.ButtonInactiveForeground;

        void StylePreset(Button? btn, string presetName)
        {
            if (btn == null) return;
            bool isSelected = string.Equals(selectedName, presetName, StringComparison.OrdinalIgnoreCase);

            btn.Background = isSelected ? selectedBg : unselectedBg;
            btn.BorderBrush = isSelected ? selectedBorder : unselectedBorder;
            btn.Foreground = isSelected ? selectedFg : unselectedFg;
            btn.FontWeight = isSelected ? FontWeights.Bold : FontWeights.SemiBold;
        }

        StylePreset(BtnPresetRincon, "Tucson East (Rincon Ridges), AZ");
        StylePreset(BtnPresetLemmon, "Mount Lemmon, AZ");
        StylePreset(BtnPresetRainier, "Mount Rainier (Paradise), WA");
        StylePreset(BtnPresetGrandCanyon, "Grand Canyon (Mather Point), AZ");
        StylePreset(BtnPresetPikes, "Pikes Peak, CO");
        StylePreset(BtnPresetYosemite, "Yosemite (Glacier Point), CA");
        StylePreset(BtnPresetWashington, "Mount Washington, NH");
    }

    private void BtnLayerTopo_Click(object sender, RoutedEventArgs e) => SetBaseLayer("topo");
    private void BtnLayerStreets_Click(object sender, RoutedEventArgs e) => SetBaseLayer("streets");
    private void BtnLayerSatellite_Click(object sender, RoutedEventArgs e) => SetBaseLayer("satellite");
    private void BtnLayerQuads_Click(object sender, RoutedEventArgs e) => SetBaseLayer("quads");
    private void BtnLayerOpenTopo_Click(object sender, RoutedEventArgs e) => SetBaseLayer("opentopo");
    private void BtnLayerHybrid_Click(object sender, RoutedEventArgs e) => SetBaseLayer("hybrid");

    private void BtnTogglePadUs_Click(object sender, RoutedEventArgs e)
    {
        _isPadUsVisible = !_isPadUsVisible;
        if (!_isMapInitialized) return;
        try
        {
            Interop.ExecuteJavaScriptVoid("if (window.pointedMap && window.pointedMap.setPadUsVisible) { window.pointedMap.setPadUsVisible($0); }", _isPadUsVisible);
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LeafletMapControl] SetPadUsVisible error: {ex.Message}");
        }

        UpdateLayerButtons();
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
        StyleLayerBtn(BtnLayerStreets, _currentBaseLayer == "streets" || _currentBaseLayer == "osm");
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
            BtnConfigurePmTiles.Foreground = tm.Accent;
        }
    }

    private void SliderAzimuth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingFromInternal) return;
        if (_isMapInitialized)
        {
            Vm_MapDirectionRequested(e.NewValue);
        }
    }

    private void BtnCardN_Click(object sender, RoutedEventArgs e) => ViewModel?.SetQuickAzimuth(0);
    private void BtnCardNE_Click(object sender, RoutedEventArgs e) => ViewModel?.SetQuickAzimuth(45);
    private void BtnCardE_Click(object sender, RoutedEventArgs e) => ViewModel?.SetQuickAzimuth(90);
    private void BtnCardSE_Click(object sender, RoutedEventArgs e) => ViewModel?.SetQuickAzimuth(135);
    private void BtnCardS_Click(object sender, RoutedEventArgs e) => ViewModel?.SetQuickAzimuth(180);
    private void BtnCardSW_Click(object sender, RoutedEventArgs e) => ViewModel?.SetQuickAzimuth(225);
    private void BtnCardW_Click(object sender, RoutedEventArgs e) => ViewModel?.SetQuickAzimuth(270);
    private void BtnCardNW_Click(object sender, RoutedEventArgs e) => ViewModel?.SetQuickAzimuth(315);
}
