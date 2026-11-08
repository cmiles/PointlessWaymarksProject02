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
        }

        CelestialPageView?.PrepareAsPage();

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

        if (isSmall)
        {
            // Top App Bar: Double size on small screen mobile
            if (TopHeaderBorder != null)
            {
                TopHeaderBorder.Padding = new Thickness(10, 10, 10, 10);
            }
            if (ImgAppIcon != null)
            {
                ImgAppIcon.Width = 32;
                ImgAppIcon.Height = 32;
                ImgAppIcon.Margin = new Thickness(0, 0, 8, 0);
            }
            if (TxtAppTitle != null)
            {
                if (width < 450)
                {
                    TxtAppTitle.Visibility = Visibility.Collapsed;
                }
                else if (width < 700)
                {
                    TxtAppTitle.Visibility = Visibility.Visible;
                    TxtAppTitle.Text = "Horizons";
                    TxtAppTitle.FontSize = 18;
                }
                else
                {
                    TxtAppTitle.Visibility = Visibility.Visible;
                    TxtAppTitle.Text = "Pointed Horizons";
                    TxtAppTitle.FontSize = 18;
                }
            }

            if (TabsBorder != null)
            {
                TabsBorder.Visibility = Visibility.Collapsed;
            }
            if (CompactNavBorder != null)
            {
                CompactNavBorder.Visibility = Visibility.Visible;
            }

            // Page choice button: Twice as big on small screen mobile
            if (BtnCompactNav != null)
            {
                BtnCompactNav.MinHeight = 48;
                BtnCompactNav.Padding = new Thickness(16, 10, 16, 10);
                BtnCompactNav.FontSize = 14.5;
                BtnCompactNav.FontWeight = FontWeights.Bold;
            }

            // Options button: Twice as big touch target on small screen mobile
            if (BtnOptions != null)
            {
                BtnOptions.Content = "⚙️";
                BtnOptions.MinHeight = 48;
                BtnOptions.MinWidth = 48;
                BtnOptions.Padding = new Thickness(12, 8, 12, 8);
                BtnOptions.FontSize = 20;
            }

            // Page Navigation dropdown panel & buttons: Twice as big
            if (BtnNavPageMap != null) { BtnNavPageMap.MinHeight = 48; BtnNavPageMap.Padding = new Thickness(14, 12, 14, 12); BtnNavPageMap.FontSize = 14.5; }
            if (BtnNavPageAimMap != null) { BtnNavPageAimMap.MinHeight = 48; BtnNavPageAimMap.Padding = new Thickness(14, 12, 14, 12); BtnNavPageAimMap.FontSize = 14.5; }
            if (BtnNavPageHorizon != null) { BtnNavPageHorizon.MinHeight = 48; BtnNavPageHorizon.Padding = new Thickness(14, 12, 14, 12); BtnNavPageHorizon.FontSize = 14.5; }
            if (BtnNavPageTracks != null) { BtnNavPageTracks.MinHeight = 48; BtnNavPageTracks.Padding = new Thickness(14, 12, 14, 12); BtnNavPageTracks.FontSize = 14.5; }
            if (BtnCloseCompactNav != null) { BtnCloseCompactNav.MinHeight = 36; BtnCloseCompactNav.MinWidth = 36; BtnCloseCompactNav.FontSize = 14; }

            // Content button in Horizon view: Twice as big
            if (BtnRefresh != null)
            {
                BtnRefresh.MinHeight = 44;
                BtnRefresh.Padding = new Thickness(14, 8, 14, 8);
                BtnRefresh.FontSize = 13;
            }

            // Application options buttons: Twice as big
            if (BtnUnitFeet != null) { BtnUnitFeet.MinHeight = 44; BtnUnitFeet.Padding = new Thickness(12, 10, 12, 10); BtnUnitFeet.FontSize = 12.5; }
            if (BtnUnitMeters != null) { BtnUnitMeters.MinHeight = 44; BtnUnitMeters.Padding = new Thickness(12, 10, 12, 10); BtnUnitMeters.FontSize = 12.5; }
            if (BtnOpenPmTilesConfig != null) { BtnOpenPmTilesConfig.MinHeight = 48; BtnOpenPmTilesConfig.Padding = new Thickness(10, 12, 10, 12); BtnOpenPmTilesConfig.FontSize = 12.5; }
            if (BtnToggleAppTheme != null) { BtnToggleAppTheme.MinHeight = 48; BtnToggleAppTheme.Padding = new Thickness(10, 12, 10, 12); BtnToggleAppTheme.FontSize = 12.5; }
            if (BtnSaveGeoNamesUsername != null) { BtnSaveGeoNamesUsername.MinHeight = 44; BtnSaveGeoNamesUsername.Padding = new Thickness(12, 8, 12, 8); BtnSaveGeoNamesUsername.FontSize = 12.5; }
            if (BtnCheckUpdates != null) { BtnCheckUpdates.MinHeight = 48; BtnCheckUpdates.Padding = new Thickness(10, 12, 10, 12); BtnCheckUpdates.FontSize = 12.5; }
            if (BtnCloseOptions != null) { BtnCloseOptions.MinHeight = 36; BtnCloseOptions.MinWidth = 36; BtnCloseOptions.FontSize = 14; }

            if (OptionsPanel != null)
            {
                OptionsPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                OptionsPanel.Width = double.NaN;
                OptionsPanel.Margin = new Thickness(10, 68, 10, 10);
                if (height > 0)
                {
                    OptionsPanel.MaxHeight = Math.Max(260, height - 80);
                }
            }

            if (CompactNavPanel != null)
            {
                CompactNavPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                CompactNavPanel.Width = double.NaN;
                CompactNavPanel.Margin = new Thickness(10, 68, 10, 10);
            }
        }
        else
        {
            if (TopHeaderBorder != null)
            {
                TopHeaderBorder.Padding = new Thickness(12, 8, 12, 8);
            }
            if (ImgAppIcon != null)
            {
                ImgAppIcon.Width = 22;
                ImgAppIcon.Height = 22;
                ImgAppIcon.Margin = new Thickness(0, 0, 8, 0);
            }
            if (TxtAppTitle != null)
            {
                TxtAppTitle.Visibility = Visibility.Visible;
                TxtAppTitle.Text = "Pointed Horizons";
                TxtAppTitle.FontSize = 15;
            }

            if (TabsBorder != null)
            {
                TabsBorder.Visibility = Visibility.Visible;
            }
            if (CompactNavBorder != null)
            {
                CompactNavBorder.Visibility = Visibility.Collapsed;
            }
            if (BtnCompactNav != null)
            {
                BtnCompactNav.MinHeight = 0;
                BtnCompactNav.Padding = new Thickness(10, 4, 10, 4);
                BtnCompactNav.FontSize = 11;
            }
            if (BtnOptions != null)
            {
                BtnOptions.Content = "⚙️ Options";
                BtnOptions.MinHeight = 0;
                BtnOptions.MinWidth = 0;
                BtnOptions.Padding = new Thickness(10, 5, 10, 5);
                BtnOptions.FontSize = 11;
            }
            CloseCompactNav();

            // Tab button labels and availability (when header tabs are visible on large screens)
            if (BtnTabMap != null)
            {
                BtnTabMap.Content = "🗺️ Full Screen Map";
            }
            if (BtnTabCelestial != null)
            {
                BtnTabCelestial.Content = "☀️ Sun & Moon";
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
                BtnTabTracks.Content = "📁 Gpx Files";
            }

            if (BtnRefresh != null)
            {
                BtnRefresh.MinHeight = 0;
                BtnRefresh.Padding = new Thickness(10, 4, 10, 4);
                BtnRefresh.FontSize = 11;
            }

            if (BtnUnitFeet != null) { BtnUnitFeet.MinHeight = 0; BtnUnitFeet.Padding = new Thickness(12, 5, 12, 5); BtnUnitFeet.FontSize = 11; }
            if (BtnUnitMeters != null) { BtnUnitMeters.MinHeight = 0; BtnUnitMeters.Padding = new Thickness(12, 5, 12, 5); BtnUnitMeters.FontSize = 11; }
            if (BtnOpenPmTilesConfig != null) { BtnOpenPmTilesConfig.MinHeight = 0; BtnOpenPmTilesConfig.Padding = new Thickness(8, 5, 8, 5); BtnOpenPmTilesConfig.FontSize = 11; }
            if (BtnToggleAppTheme != null) { BtnToggleAppTheme.MinHeight = 0; BtnToggleAppTheme.Padding = new Thickness(8, 5, 8, 5); BtnToggleAppTheme.FontSize = 11; }
            if (BtnSaveGeoNamesUsername != null) { BtnSaveGeoNamesUsername.MinHeight = 0; BtnSaveGeoNamesUsername.Padding = new Thickness(10, 4, 10, 4); BtnSaveGeoNamesUsername.FontSize = 10; }
            if (BtnCheckUpdates != null) { BtnCheckUpdates.MinHeight = 0; BtnCheckUpdates.Padding = new Thickness(8, 5, 8, 5); BtnCheckUpdates.FontSize = 11; }
            if (BtnCloseOptions != null) { BtnCloseOptions.MinHeight = 0; BtnCloseOptions.MinWidth = 0; BtnCloseOptions.FontSize = 11; }

            if (OptionsPanel != null)
            {
                OptionsPanel.HorizontalAlignment = HorizontalAlignment.Right;
                OptionsPanel.Width = 310;
                OptionsPanel.Margin = new Thickness(0, 52, 14, 0);
                OptionsPanel.MaxHeight = 600;
            }

            if (CompactNavPanel != null)
            {
                CompactNavPanel.HorizontalAlignment = HorizontalAlignment.Center;
                CompactNavPanel.Width = 230;
                CompactNavPanel.Margin = new Thickness(0, 52, 0, 0);
            }
        }

        HorizonViewer?.SetCompactMode(isSmall);
        CelestialPageView?.SetCompactMode(isSmall);
        FullScreenMapControlView?.UpdateResponsiveLayout(width, height);

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
            if (ViewCelestialTab != null) ViewCelestialTab.Visibility = Visibility.Collapsed;
            if (ViewHorizonTab != null) ViewHorizonTab.Visibility = Visibility.Collapsed;
            if (ViewTracksTab != null) ViewTracksTab.Visibility = Visibility.Collapsed;

            FullScreenMapControlView?.ResizeMap();
        }
        else if (_currentTab == "Celestial")
        {
            if (ViewFullScreenMapTab != null) ViewFullScreenMapTab.Visibility = Visibility.Collapsed;
            if (ViewCelestialTab != null) ViewCelestialTab.Visibility = Visibility.Visible;
            if (ViewHorizonTab != null) ViewHorizonTab.Visibility = Visibility.Collapsed;
            if (ViewTracksTab != null) ViewTracksTab.Visibility = Visibility.Collapsed;
        }
        else if (_currentTab == "Tracks")
        {
            if (ViewFullScreenMapTab != null) ViewFullScreenMapTab.Visibility = Visibility.Collapsed;
            if (ViewCelestialTab != null) ViewCelestialTab.Visibility = Visibility.Collapsed;
            if (ViewHorizonTab != null) ViewHorizonTab.Visibility = Visibility.Collapsed;
            if (ViewTracksTab != null) ViewTracksTab.Visibility = Visibility.Visible;

            TracksControlView?.RefreshView();
        }
        else
        {
            if (ViewFullScreenMapTab != null) ViewFullScreenMapTab.Visibility = Visibility.Collapsed;
            if (ViewCelestialTab != null) ViewCelestialTab.Visibility = Visibility.Collapsed;
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
                "Celestial" => "☀️ Sun & Moon ▾",
                "HorizonMap" => "🧭 Aim Map ▾",
                "Horizon" => "🌄 Horizon View ▾",
                "Tracks" => "📁 Gpx Files ▾",
                _ => "🗺️ Map ▾"
            };
        }

        UpdateTabButtonsTheme(tm.IsDark);
        UpdateCompactNavButtonsTheme(tm.IsDark);
    }

    private void BtnTabMap_Click(object sender, RoutedEventArgs e) => SwitchTab("FullScreenMap");
    private void BtnTabCelestial_Click(object sender, RoutedEventArgs e) => SwitchTab("Celestial");
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

    private void BtnNavPageCelestial_Click(object sender, RoutedEventArgs e)
    {
        SwitchTab("Celestial");
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

    public void ShowCelestialInfo()
    {
        SwitchTab("Celestial");
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
        CelestialPageView?.UpdateTheme(isDark);

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
        StyleTabBtn(BtnTabCelestial, _currentTab == "Celestial");
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
        StyleNavBtn(BtnNavPageCelestial, _currentTab == "Celestial");
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
