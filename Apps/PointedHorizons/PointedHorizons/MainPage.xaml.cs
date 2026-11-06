using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PointedHorizons.Services;
using PointedHorizons.ViewModels;

namespace PointedHorizons;

public partial class MainPage : Page
{
    private string _currentTab = "FullScreenMap";
    private bool _isSmallScreen;

    public MainViewModel ViewModel { get; }

    public MainPage()
    {
        InitializeComponent();

        ViewModel = new MainViewModel();
        DataContext = ViewModel;

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ThemeManager.Current.ThemeChanged += ThemeManager_ThemeChanged;
        Loaded += MainPage_Loaded;
        SizeChanged += MainPage_SizeChanged;

        if (FullScreenMapControlView != null)
        {
            FullScreenMapControlView.ConfigurePmTilesRequested += (s, e) => ShowPmTilesModal();
            FullScreenMapControlView.OpenTracksPageRequested += (s, e) => SwitchTab("Tracks");
        }
        if (TracksControlView != null)
        {
            TracksControlView.TrackZoomRequested += (s, track) =>
            {
                SwitchTab("FullScreenMap");
                FullScreenMapControlView?.ZoomToTrack(track);
            };
        }
        if (LeafletMap != null)
        {
            LeafletMap.ConfigurePmTilesRequested += (s, e) => ShowPmTilesModal();
        }
        if (PmTilesConfigModal != null)
        {
            PmTilesConfigModal.CloseRequested += (s, e) => HidePmTilesModal();
            PmTilesConfigModal.ApplyAndUseRequested += (s, e) => ApplyPmTilesOnMaps();
        }
    }

    private void ThemeManager_ThemeChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            UpdateTheme(ThemeManager.Current.IsDark);
        });
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsDarkTheme))
        {
            UpdateTheme(ViewModel.IsDarkTheme);
        }
        else if (e.PropertyName == nameof(MainViewModel.IsMetric))
        {
            UpdateUnitsButtonsTheme(ViewModel.IsDarkTheme);
        }
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (!OfflineDataService.IsPmTilesSupported)
        {
            if (BtnOpenPmTilesConfig != null) BtnOpenPmTilesConfig.Visibility = Visibility.Collapsed;
            if (TxtSectionOffline != null) TxtSectionOffline.Visibility = Visibility.Collapsed;
            if (OptionsDivider2 != null) OptionsDivider2.Visibility = Visibility.Collapsed;
            if (PmTilesConfigModal != null) PmTilesConfigModal.Visibility = Visibility.Collapsed;
            if (PmTilesModalBackdrop != null) PmTilesModalBackdrop.Visibility = Visibility.Collapsed;
        }

        UpdateTheme(ViewModel.IsDarkTheme);
        UpdateResponsiveLayout(ActualWidth, ActualHeight);
        UpdateUnitsButtonsTheme(ViewModel.IsDarkTheme);

        // 1. Make the full screen map the first page on launch
        SwitchTab("FullScreenMap");

        // Start background scan of GPX track folder
        TrackIndexService.InitializeBackgroundScan();

        // Schedule background horizon calculations so data is ready when navigating to Horizon
        ViewModel.ScheduleHorizonGeneration(immediate: true);
        ViewModel.ScheduleFarthestVisibleFeatureCalculation();

        // Wire up GeoNames search modal
        if (GeoNamesSearchModal != null)
        {
            GeoNamesSearchModal.CloseRequested += (s, ev) => HideGeoNamesSearchModal();
            GeoNamesSearchModal.LocationSelected += (s, loc) =>
            {
                ViewModel.SetLocation(loc.Latitude, loc.Longitude, loc.TitleDisplay, panMap: true);
                if (loc.ElevationM.HasValue && loc.ElevationM.Value > -500)
                {
                    ViewModel.ObserverElevationM = loc.ElevationM.Value;
                }
                FullScreenMapControlView?.CenterOn(loc.Latitude, loc.Longitude);
                if (_currentTab == "Tracks")
                {
                    SwitchTab("FullScreenMap");
                }
                HideGeoNamesSearchModal();
            };
        }

        // Wire up location search requested from full screen map menu
        if (FullScreenMapControlView != null)
        {
            FullScreenMapControlView.SearchLocationRequested += (s, ev) => ShowGeoNamesSearchModal();
            FullScreenMapControlView.CelestialInfoRequested += (s, ev) => ShowCelestialInfoModal(ev.Latitude, ev.Longitude);
        }

        if (LeafletMap != null)
        {
            LeafletMap.CelestialInfoRequested += (s, ev) => ShowCelestialInfoModal(ev.Latitude, ev.Longitude);
        }

        if (CelestialInfoModal != null)
        {
            CelestialInfoModal.CloseRequested += (s, ev) => HideCelestialInfoModal();
        }

        GeoNamesService.ConfigurationChanged += (s, ev) =>
        {
            Dispatcher.BeginInvoke(UpdateGeoNamesConfigDisplay);
        };
        UpdateGeoNamesConfigDisplay();

        // Check for application updates silently on startup (Photino and Android)
        if (!OperatingSystem.IsBrowser())
        {
            _ = ViewModel.CheckForUpdatesAsync(silent: true);
        }
    }

    private void MainPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveLayout(e.NewSize.Width, e.NewSize.Height);
    }

    private void UpdateResponsiveLayout(double width, double height)
    {
        if (width <= 0) return;

        bool isSmall = width < 768 || (height > 0 && height < 600);
        bool modeChanged = isSmall != _isSmallScreen;
        _isSmallScreen = isSmall;

        // Responsive branding title
        if (TxtAppTitle != null)
        {
            if (width < 500)
            {
                TxtAppTitle.Visibility = Visibility.Collapsed;
            }
            else if (width < 700)
            {
                TxtAppTitle.Visibility = Visibility.Visible;
                TxtAppTitle.Text = "Horizons";
            }
            else
            {
                TxtAppTitle.Visibility = Visibility.Visible;
                TxtAppTitle.Text = "Pointed Horizons";
            }
        }

        // Responsive header elements: On small screens, collapse top bar tabs to compact nav selector
        if (TabsBorder != null)
        {
            TabsBorder.Visibility = isSmall ? Visibility.Collapsed : Visibility.Visible;
        }
        if (CompactNavBorder != null)
        {
            CompactNavBorder.Visibility = isSmall ? Visibility.Visible : Visibility.Collapsed;
        }
        if (BtnOptions != null)
        {
            BtnOptions.Content = isSmall ? "⚙️" : "⚙️ Options";
        }
        if (!isSmall)
        {
            CloseCompactNav();
        }

        // Tab button labels and availability (when header tabs are visible on large screens)
        if (BtnTabMap != null)
        {
            BtnTabMap.Content = "🗺️ Full Screen Map";
        }
        if (BtnTabAimMap != null)
        {
            BtnTabAimMap.Visibility = Visibility.Collapsed;
        }
        if (BtnTabHorizon != null)
        {
            BtnTabHorizon.Content = "🌄 Horizon View";
        }
        if (BtnTabTracks != null)
        {
            BtnTabTracks.Content = "📁 Tracks Library";
        }

        HorizonViewer?.SetCompactMode(isSmall);

        if (modeChanged)
        {
            if (!isSmall && _currentTab == "HorizonMap")
            {
                _currentTab = "Horizon";
            }
            ApplyTabLayout();
        }
    }

    public void SwitchTab(string tabName)
    {
        // On large screens, "HorizonMap" collapses into the split "Horizon" view
        if (!_isSmallScreen && tabName == "HorizonMap")
        {
            tabName = "Horizon";
        }

        _currentTab = tabName;
        ApplyTabLayout();
    }

    private void ApplyTabLayout()
    {
        var tm = ThemeManager.Current;

        if (_currentTab == "FullScreenMap")
        {
            if (ViewFullScreenMapTab != null) ViewFullScreenMapTab.Visibility = Visibility.Visible;
            if (ViewHorizonTab != null) ViewHorizonTab.Visibility = Visibility.Collapsed;
            if (ViewTracksTab != null) ViewTracksTab.Visibility = Visibility.Collapsed;

            FullScreenMapControlView?.ResizeMap();
        }
        else if (_currentTab == "Tracks")
        {
            if (ViewFullScreenMapTab != null) ViewFullScreenMapTab.Visibility = Visibility.Collapsed;
            if (ViewHorizonTab != null) ViewHorizonTab.Visibility = Visibility.Collapsed;
            if (ViewTracksTab != null) ViewTracksTab.Visibility = Visibility.Visible;

            TracksControlView?.RefreshView();
        }
        else
        {
            if (ViewFullScreenMapTab != null) ViewFullScreenMapTab.Visibility = Visibility.Collapsed;
            if (ViewHorizonTab != null) ViewHorizonTab.Visibility = Visibility.Visible;
            if (ViewTracksTab != null) ViewTracksTab.Visibility = Visibility.Collapsed;

            if (_isSmallScreen)
            {
                // On small screens, make each view in Horizon its own full page
                if (_currentTab == "HorizonMap")
                {
                    // Dedicated Horizon Aiming Map Page (100% height)
                    if (RowHorizonTop != null)
                    {
                        RowHorizonTop.Height = new GridLength(0);
                        RowHorizonTop.MinHeight = 0;
                    }
                    if (RowDirectionsBar != null)
                    {
                        RowDirectionsBar.Height = new GridLength(0);
                    }
                    if (RowHorizonBottom != null)
                    {
                        RowHorizonBottom.Height = new GridLength(1, GridUnitType.Star);
                        RowHorizonBottom.MinHeight = 0;
                    }

                    if (HorizonViewer != null) HorizonViewer.Visibility = Visibility.Collapsed;
                    if (SplitBarBorder != null) SplitBarBorder.Visibility = Visibility.Collapsed;
                    if (LeafletMap != null)
                    {
                        LeafletMap.Visibility = Visibility.Visible;
                        LeafletMap.ResizeMap();
                    }
                }
                else
                {
                    // Dedicated Horizon Ray-Traced Panorama Page (100% height)
                    if (RowHorizonTop != null)
                    {
                        RowHorizonTop.Height = new GridLength(1, GridUnitType.Star);
                        RowHorizonTop.MinHeight = 0;
                    }
                    if (RowDirectionsBar != null)
                    {
                        RowDirectionsBar.Height = GridLength.Auto;
                    }
                    if (RowHorizonBottom != null)
                    {
                        RowHorizonBottom.Height = new GridLength(0);
                        RowHorizonBottom.MinHeight = 0;
                    }

                    if (HorizonViewer != null)
                    {
                        HorizonViewer.Visibility = Visibility.Visible;
                        HorizonViewer.Redraw();
                    }
                    if (SplitBarBorder != null) SplitBarBorder.Visibility = Visibility.Visible;
                    if (DirectionsBar != null) DirectionsBar.Redraw();
                    if (LeafletMap != null) LeafletMap.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                // On large screens, Split View with both Horizon on top and Map on bottom
                if (RowHorizonTop != null)
                {
                    RowHorizonTop.Height = new GridLength(1.1, GridUnitType.Star);
                    RowHorizonTop.MinHeight = 220;
                }
                if (RowDirectionsBar != null)
                {
                    RowDirectionsBar.Height = GridLength.Auto;
                }
                if (RowHorizonBottom != null)
                {
                    RowHorizonBottom.Height = new GridLength(1.0, GridUnitType.Star);
                    RowHorizonBottom.MinHeight = 200;
                }

                if (HorizonViewer != null)
                {
                    HorizonViewer.Visibility = Visibility.Visible;
                    HorizonViewer.Redraw();
                }
                if (SplitBarBorder != null) SplitBarBorder.Visibility = Visibility.Visible;
                if (DirectionsBar != null) DirectionsBar.Redraw();
                if (LeafletMap != null)
                {
                    LeafletMap.Visibility = Visibility.Visible;
                    LeafletMap.ResizeMap();
                }
            }
        }

        if (BtnCompactNav != null)
        {
            BtnCompactNav.Content = _currentTab switch
            {
                "FullScreenMap" => "🗺️ Full Screen Map ▾",
                "HorizonMap" => "🧭 Aim Map ▾",
                "Horizon" => "🌄 Horizon View ▾",
                "Tracks" => "📁 Tracks ▾",
                _ => "🗺️ Map ▾"
            };
        }

        UpdateTabButtonsTheme(tm.IsDark);
        UpdateCompactNavButtonsTheme(tm.IsDark);
    }

    private void BtnTabMap_Click(object sender, RoutedEventArgs e) => SwitchTab("FullScreenMap");
    private void BtnTabAimMap_Click(object sender, RoutedEventArgs e) => SwitchTab("HorizonMap");
    private void BtnTabHorizon_Click(object sender, RoutedEventArgs e) => SwitchTab("Horizon");
    private void BtnTabTracks_Click(object sender, RoutedEventArgs e) => SwitchTab("Tracks");

    private void BtnCompactNav_Click(object sender, RoutedEventArgs e)
    {
        if (CompactNavPanel == null) return;
        bool willShow = CompactNavPanel.Visibility != Visibility.Visible;
        CompactNavPanel.Visibility = willShow ? Visibility.Visible : Visibility.Collapsed;
        if (CompactNavBackdrop != null)
        {
            CompactNavBackdrop.Visibility = willShow ? Visibility.Visible : Visibility.Collapsed;
        }
        if (willShow)
        {
            UpdateCompactNavButtonsTheme(ThemeManager.Current.IsDark);
        }
    }

    private void BtnCloseCompactNav_Click(object sender, RoutedEventArgs e)
    {
        CloseCompactNav();
    }

    private void CloseCompactNav()
    {
        if (CompactNavPanel != null) CompactNavPanel.Visibility = Visibility.Collapsed;
        if (CompactNavBackdrop != null) CompactNavBackdrop.Visibility = Visibility.Collapsed;
    }

    private void BtnNavPageMap_Click(object sender, RoutedEventArgs e)
    {
        SwitchTab("FullScreenMap");
        CloseCompactNav();
    }

    private void BtnNavPageAimMap_Click(object sender, RoutedEventArgs e)
    {
        SwitchTab("HorizonMap");
        CloseCompactNav();
    }

    private void BtnNavPageHorizon_Click(object sender, RoutedEventArgs e)
    {
        SwitchTab("Horizon");
        CloseCompactNav();
    }

    private void BtnNavPageTracks_Click(object sender, RoutedEventArgs e)
    {
        SwitchTab("Tracks");
        CloseCompactNav();
    }

    #region Options Hamburger (Application-Wide Settings)

    private void BtnOptions_Click(object sender, RoutedEventArgs e)
    {
        if (OptionsPanel != null)
        {
            bool willShow = OptionsPanel.Visibility != Visibility.Visible;
            OptionsPanel.Visibility = willShow ? Visibility.Visible : Visibility.Collapsed;
            if (OptionsBackdrop != null) OptionsBackdrop.Visibility = willShow ? Visibility.Visible : Visibility.Collapsed;

            if (willShow)
            {
                UpdateUnitsButtonsTheme(ThemeManager.Current.IsDark);
            }
        }
    }

    private void BtnCloseOptions_Click(object sender, RoutedEventArgs e)
    {
        if (OptionsPanel != null) OptionsPanel.Visibility = Visibility.Collapsed;
        if (OptionsBackdrop != null) OptionsBackdrop.Visibility = Visibility.Collapsed;
    }

    private void BtnUnitFeet_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IsMetric = false;
        UpdateUnitsButtonsTheme(ThemeManager.Current.IsDark);
    }

    private void BtnUnitMeters_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IsMetric = true;
        UpdateUnitsButtonsTheme(ThemeManager.Current.IsDark);
    }

    private void BtnToggleAppTheme_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IsDarkTheme = !ViewModel.IsDarkTheme;
    }

    private void UpdateUnitsButtonsTheme(bool isDark)
    {
        var tm = ThemeManager.Current;
        bool isMetric = ViewModel.IsMetric;

        void StyleUnitBtn(Button btn, bool active)
        {
            if (btn == null) return;
            btn.Background = active ? tm.ButtonActiveBackground : tm.ButtonInactiveBackground;
            btn.BorderBrush = active ? tm.ButtonActiveBorder : tm.ButtonInactiveBorder;
            btn.Foreground = active ? tm.ButtonActiveForeground : tm.ButtonInactiveForeground;
            btn.FontWeight = active ? FontWeights.Bold : FontWeights.SemiBold;
        }

        StyleUnitBtn(BtnUnitFeet, !isMetric);
        StyleUnitBtn(BtnUnitMeters, isMetric);
    }

    public void ShowPmTilesModal()
    {
        if (!OfflineDataService.IsPmTilesSupported) return;

        if (OptionsPanel != null) OptionsPanel.Visibility = Visibility.Collapsed;
        if (OptionsBackdrop != null) OptionsBackdrop.Visibility = Visibility.Collapsed;

        if (PmTilesModalBackdrop != null) PmTilesModalBackdrop.Visibility = Visibility.Visible;
        if (PmTilesConfigModal != null)
        {
            PmTilesConfigModal.Visibility = Visibility.Visible;
            PmTilesConfigModal.RefreshView();
        }
    }

    public void HidePmTilesModal()
    {
        if (PmTilesModalBackdrop != null) PmTilesModalBackdrop.Visibility = Visibility.Collapsed;
        if (PmTilesConfigModal != null) PmTilesConfigModal.Visibility = Visibility.Collapsed;
    }

    public void ShowGeoNamesSearchModal()
    {
        if (OptionsPanel != null) OptionsPanel.Visibility = Visibility.Collapsed;
        if (OptionsBackdrop != null) OptionsBackdrop.Visibility = Visibility.Collapsed;

        if (GeoNamesSearchBackdrop != null) GeoNamesSearchBackdrop.Visibility = Visibility.Visible;
        if (GeoNamesSearchModal != null)
        {
            GeoNamesSearchModal.Visibility = Visibility.Visible;
            GeoNamesSearchModal.FocusSearchInput();
        }
    }

    public void HideGeoNamesSearchModal()
    {
        if (GeoNamesSearchBackdrop != null) GeoNamesSearchBackdrop.Visibility = Visibility.Collapsed;
        if (GeoNamesSearchModal != null) GeoNamesSearchModal.Visibility = Visibility.Collapsed;
    }

    private void BtnCloseGeoNamesSearch_Click(object sender, RoutedEventArgs e)
    {
        HideGeoNamesSearchModal();
    }

    public void ShowCelestialInfoModal(double lat, double lon)
    {
        if (OptionsPanel != null) OptionsPanel.Visibility = Visibility.Collapsed;
        if (OptionsBackdrop != null) OptionsBackdrop.Visibility = Visibility.Collapsed;

        if (CelestialInfoBackdrop != null) CelestialInfoBackdrop.Visibility = Visibility.Visible;
        if (CelestialInfoModal != null)
        {
            CelestialInfoModal.Visibility = Visibility.Visible;
            CelestialInfoModal.Open(lat, lon);
        }
    }

    public void HideCelestialInfoModal()
    {
        if (CelestialInfoBackdrop != null) CelestialInfoBackdrop.Visibility = Visibility.Collapsed;
        if (CelestialInfoModal != null) CelestialInfoModal.Visibility = Visibility.Collapsed;
    }

    private void BtnCloseCelestialInfo_Click(object sender, RoutedEventArgs e)
    {
        HideCelestialInfoModal();
    }

    private void UpdateGeoNamesConfigDisplay()
    {
        bool isConfigured = GeoNamesService.IsConfigured;
        if (TxtGeoNamesUsernameInput != null)
        {
            TxtGeoNamesUsernameInput.Text = GeoNamesService.Username ?? "";
        }
        if (TxtGeoNamesConfigStatus != null)
        {
            if (isConfigured)
            {
                TxtGeoNamesConfigStatus.Text = $"Active account: {GeoNamesService.Username}";
                TxtGeoNamesConfigStatus.Foreground = ThemeManager.Current.Accent;
            }
            else
            {
                TxtGeoNamesConfigStatus.Text = "No username configured. Register free at geonames.org to enable location search.";
                TxtGeoNamesConfigStatus.Foreground = ThemeManager.Current.TextMuted;
            }
        }
        GeoNamesSearchModal?.UpdateAccountInfo();
    }

    private void BtnSaveGeoNamesUsername_Click(object sender, RoutedEventArgs e)
    {
        string username = TxtGeoNamesUsernameInput?.Text?.Trim() ?? "";
        GeoNamesService.Username = username;
        UpdateGeoNamesConfigDisplay();
        if (GeoNamesService.IsConfigured && TxtGeoNamesConfigStatus != null)
        {
            TxtGeoNamesConfigStatus.Text = $"Saved! Active account: {GeoNamesService.Username}";
            TxtGeoNamesConfigStatus.Foreground = ThemeManager.Current.Accent;
        }
    }

    private void BtnOpenPmTilesConfig_Click(object sender, RoutedEventArgs e)
    {
        ShowPmTilesModal();
    }

    private void BtnClosePmTilesModal_Click(object sender, RoutedEventArgs e)
    {
        HidePmTilesModal();
    }

    private void ApplyPmTilesOnMaps()
    {
        if (!OfflineDataService.IsPmTilesSupported) return;

        FullScreenMapControlView?.UpdatePmTilesLayers();
        FullScreenMapControlView?.SetBaseLayer("pmtiles");
        LeafletMap?.UpdatePmTilesLayers();
        LeafletMap?.SetBaseLayer("pmtiles");
        ViewModel.StatusMessage = "Applied offline PMTiles map layer(s).";
    }

    #endregion

    private void UpdateTheme(bool isDark)
    {
        var tm = ThemeManager.Current;

        // 1. Top Bar & App Header
        if (TopHeaderBorder != null)
        {
            TopHeaderBorder.Background = tm.HeaderBackground;
            TopHeaderBorder.BorderBrush = tm.HeaderBorder;
        }
        if (TxtAppTitle != null)
        {
            TxtAppTitle.Foreground = tm.TextPrimary;
        }

        // Tabs container
        if (TabsBorder != null)
        {
            TabsBorder.Background = tm.ButtonInactiveBackground;
            TabsBorder.BorderBrush = tm.ButtonInactiveBorder;
        }

        // Header Buttons
        if (BtnOptions != null)
        {
            BtnOptions.Background = tm.ButtonInactiveBackground;
            BtnOptions.BorderBrush = tm.ButtonInactiveBorder;
            BtnOptions.Foreground = tm.TextPrimary;
        }

        // 2. Main Root Grid Background
        if (MainRootGrid != null)
        {
            MainRootGrid.Background = tm.PageBackground;
        }

        // 3. Middle Split Directions Bar & Refresh Button
        if (SplitBarBorder != null)
        {
            SplitBarBorder.Background = tm.SplitBarBackground;
            SplitBarBorder.BorderBrush = tm.SplitBarBorder;
        }
        if (BtnRefresh != null)
        {
            BtnRefresh.Background = tm.ButtonInactiveBackground;
            BtnRefresh.BorderBrush = tm.ButtonInactiveBorder;
            BtnRefresh.Foreground = tm.Accent;
        }

        // 4. Options Dropdown Panel
        if (OptionsPanel != null)
        {
            OptionsPanel.Background = tm.HeaderBackground;
            OptionsPanel.BorderBrush = tm.HeaderBorder;
        }
        if (OptionsDivider0 != null) OptionsDivider0.Background = tm.HeaderBorder;
        if (OptionsDivider1 != null) OptionsDivider1.Background = tm.HeaderBorder;
        if (OptionsDivider2 != null) OptionsDivider2.Background = tm.HeaderBorder;
        if (OptionsDivider3 != null) OptionsDivider3.Background = tm.HeaderBorder;
        if (OptionsDivider4 != null) OptionsDivider4.Background = tm.HeaderBorder;

        if (TxtOptionsTitle != null) TxtOptionsTitle.Foreground = tm.TextPrimary;
        if (TxtSectionUnits != null) TxtSectionUnits.Foreground = tm.TextSecondary;
        if (TxtSectionOffline != null) TxtSectionOffline.Foreground = tm.TextSecondary;
        if (TxtSectionTheme != null) TxtSectionTheme.Foreground = tm.TextSecondary;
        if (TxtSectionGeoNames != null) TxtSectionGeoNames.Foreground = tm.TextSecondary;
        if (TxtGeoNamesHelp != null) TxtGeoNamesHelp.Foreground = tm.TextMuted;
        if (TxtGeoNamesUsernameInput != null)
        {
            TxtGeoNamesUsernameInput.Background = tm.ButtonInactiveBackground;
            TxtGeoNamesUsernameInput.Foreground = tm.TextPrimary;
            TxtGeoNamesUsernameInput.BorderBrush = tm.ButtonInactiveBorder;
        }
        if (BtnSaveGeoNamesUsername != null)
        {
            BtnSaveGeoNamesUsername.Background = tm.ButtonInactiveBackground;
            BtnSaveGeoNamesUsername.BorderBrush = tm.ButtonInactiveBorder;
            BtnSaveGeoNamesUsername.Foreground = tm.TextPrimary;
        }
        if (TxtSectionUpdates != null) TxtSectionUpdates.Foreground = tm.TextSecondary;
        if (BtnCheckUpdates != null)
        {
            BtnCheckUpdates.Background = tm.ButtonInactiveBackground;
            BtnCheckUpdates.BorderBrush = tm.ButtonInactiveBorder;
            BtnCheckUpdates.Foreground = tm.Accent;
        }

        if (BtnOpenPmTilesConfig != null)
        {
            BtnOpenPmTilesConfig.Background = tm.ButtonInactiveBackground;
            BtnOpenPmTilesConfig.BorderBrush = tm.ButtonInactiveBorder;
            BtnOpenPmTilesConfig.Foreground = tm.TextPrimary;
        }

        if (BtnToggleAppTheme != null)
        {
            BtnToggleAppTheme.Background = tm.ButtonInactiveBackground;
            BtnToggleAppTheme.BorderBrush = tm.ButtonInactiveBorder;
            BtnToggleAppTheme.Foreground = tm.ButtonInactiveForeground;
        }

        // Compact Nav selector & popup styling
        if (BtnCompactNav != null)
        {
            BtnCompactNav.Background = tm.ButtonInactiveBackground;
            BtnCompactNav.BorderBrush = tm.ButtonInactiveBorder;
            BtnCompactNav.Foreground = tm.TextPrimary;
        }
        if (CompactNavPanel != null)
        {
            CompactNavPanel.Background = tm.HeaderBackground;
            CompactNavPanel.BorderBrush = tm.HeaderBorder;
        }
        if (CompactNavDivider != null) CompactNavDivider.Background = tm.HeaderBorder;
        if (TxtCompactNavTitle != null) TxtCompactNavTitle.Foreground = tm.TextSecondary;
        if (BtnCloseCompactNav != null)
        {
            BtnCloseCompactNav.Background = tm.ButtonInactiveBackground;
            BtnCloseCompactNav.BorderBrush = tm.ButtonInactiveBorder;
            BtnCloseCompactNav.Foreground = tm.TextPrimary;
        }

        GeoNamesSearchModal?.UpdateTheme(isDark);
        CelestialInfoModal?.UpdateTheme(isDark);

        // 5. Update Tab Buttons & Compact Nav Buttons
        UpdateTabButtonsTheme(isDark);
        UpdateCompactNavButtonsTheme(isDark);
        UpdateUnitsButtonsTheme(isDark);
    }

    private void UpdateTabButtonsTheme(bool isDark)
    {
        var tm = ThemeManager.Current;

        void StyleTabBtn(Button? btn, bool isSelected)
        {
            if (btn == null) return;
            btn.Background = isSelected ? tm.ButtonActiveBackground : Brushes.Transparent;
            btn.BorderBrush = isSelected ? tm.ButtonActiveBorder : Brushes.Transparent;
            btn.Foreground = isSelected ? tm.ButtonActiveForeground : tm.TextPrimary;
            btn.FontWeight = isSelected ? FontWeights.Bold : FontWeights.SemiBold;
        }

        StyleTabBtn(BtnTabMap, _currentTab == "FullScreenMap");
        StyleTabBtn(BtnTabAimMap, _currentTab == "HorizonMap");
        StyleTabBtn(BtnTabHorizon, _currentTab == "Horizon");
        StyleTabBtn(BtnTabTracks, _currentTab == "Tracks");
    }

    private void UpdateCompactNavButtonsTheme(bool isDark)
    {
        var tm = ThemeManager.Current;

        void StyleNavBtn(Button? btn, bool isSelected)
        {
            if (btn == null) return;
            btn.Background = isSelected ? tm.ButtonActiveBackground : tm.ButtonInactiveBackground;
            btn.BorderBrush = isSelected ? tm.ButtonActiveBorder : tm.ButtonInactiveBorder;
            btn.Foreground = isSelected ? tm.ButtonActiveForeground : tm.ButtonInactiveForeground;
            btn.FontWeight = isSelected ? FontWeights.Bold : FontWeights.SemiBold;
        }

        StyleNavBtn(BtnNavPageMap, _currentTab == "FullScreenMap");
        StyleNavBtn(BtnNavPageAimMap, _currentTab == "HorizonMap");
        StyleNavBtn(BtnNavPageHorizon, _currentTab == "Horizon");
        StyleNavBtn(BtnNavPageTracks, _currentTab == "Tracks");
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ScheduleHorizonGeneration(immediate: true);
    }

    private void BtnUpdates_Click(object sender, RoutedEventArgs e)
    {
        BtnCloseOptions_Click(this, new RoutedEventArgs());
        _ = ViewModel.CheckForUpdatesAsync(silent: false);
    }
}
