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
        else if (e.PropertyName == nameof(MainViewModel.LocationPresetName))
        {
            UpdatePresetButtonsTheme(ViewModel.IsDarkTheme);
        }
        else if (e.PropertyName == nameof(MainViewModel.IsMetric))
        {
            UpdateUnitsButtonsTheme(ViewModel.IsDarkTheme);
        }
        else if (e.PropertyName == nameof(MainViewModel.FieldOfView))
        {
            UpdateOptFovButtonsTheme(ViewModel.IsDarkTheme);
        }
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (!OfflineDataService.IsPmTilesSupported)
        {
            if (BtnOpenPmTilesConfig != null) BtnOpenPmTilesConfig.Visibility = Visibility.Collapsed;
            if (TxtSectionOffline != null) TxtSectionOffline.Visibility = Visibility.Collapsed;
            if (OptionsDivider3 != null) OptionsDivider3.Visibility = Visibility.Collapsed;
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

        // Tab button labels and availability (Aim Map sits BEFORE Horizon on small screens)
        if (BtnTabMap != null)
        {
            BtnTabMap.Content = isSmall ? "🗺️ Map" : "🗺️ Full Screen Map";
        }
        if (BtnTabAimMap != null)
        {
            BtnTabAimMap.Visibility = isSmall ? Visibility.Visible : Visibility.Collapsed;
        }
        if (BtnTabHorizon != null)
        {
            BtnTabHorizon.Content = isSmall ? "🌄 Horizon" : "🌄 Horizon View";
        }
        if (BtnTabTracks != null)
        {
            BtnTabTracks.Content = isSmall ? "📁 Tracks" : "📁 Tracks Library";
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

        UpdateTabButtonsTheme(tm.IsDark);
    }

    private void BtnTabMap_Click(object sender, RoutedEventArgs e) => SwitchTab("FullScreenMap");
    private void BtnTabAimMap_Click(object sender, RoutedEventArgs e) => SwitchTab("HorizonMap");
    private void BtnTabHorizon_Click(object sender, RoutedEventArgs e) => SwitchTab("Horizon");
    private void BtnTabTracks_Click(object sender, RoutedEventArgs e) => SwitchTab("Tracks");

    #region Options Hamburger & Presets

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
                UpdatePresetButtonsTheme(ThemeManager.Current.IsDark);
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

    private void BtnOptFov15_Click(object sender, RoutedEventArgs e) => SetOptFocalLength(15.0);
    private void BtnOptFov35_Click(object sender, RoutedEventArgs e) => SetOptFocalLength(35.0);
    private void BtnOptFov50_Click(object sender, RoutedEventArgs e) => SetOptFocalLength(50.0);
    private void BtnOptFov90_Click(object sender, RoutedEventArgs e) => SetOptFocalLength(90.0);

    private void SetOptFocalLength(double mm)
    {
        double fov = MainViewModel.FocalLengthToFieldOfView(mm);
        ViewModel.FieldOfView = fov;
        UpdateOptFovButtonsTheme(ThemeManager.Current.IsDark);
    }

    private void UpdateOptFovButtonsTheme(bool isDark)
    {
        var tm = ThemeManager.Current;
        double fl = MainViewModel.FieldOfViewToFocalLength(ViewModel.FieldOfView);

        void StyleFovBtn(Button btn, bool active)
        {
            if (btn == null) return;
            btn.Background = active ? tm.ButtonActiveBackground : tm.ButtonInactiveBackground;
            btn.BorderBrush = active ? tm.ButtonActiveBorder : tm.ButtonInactiveBorder;
            btn.Foreground = active ? tm.ButtonActiveForeground : tm.ButtonInactiveForeground;
            btn.FontWeight = active ? FontWeights.Bold : FontWeights.SemiBold;
        }

        StyleFovBtn(BtnOptFov15, Math.Abs(fl - 15.0) < 2.0);
        StyleFovBtn(BtnOptFov35, Math.Abs(fl - 35.0) < 3.0);
        StyleFovBtn(BtnOptFov50, Math.Abs(fl - 50.0) < 4.0);
        StyleFovBtn(BtnOptFov90, Math.Abs(fl - 90.0) < 6.0);
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

        // Options button
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
        if (OptionsDividerSunMoon != null) OptionsDividerSunMoon.Background = tm.HeaderBorder;
        if (OptionsDivider2 != null) OptionsDivider2.Background = tm.HeaderBorder;
        if (OptionsDivider3 != null) OptionsDivider3.Background = tm.HeaderBorder;
        if (OptionsDivider4 != null) OptionsDivider4.Background = tm.HeaderBorder;
        if (TxtOptionsTitle != null) TxtOptionsTitle.Foreground = tm.TextPrimary;
        if (TxtSectionFov != null) TxtSectionFov.Foreground = tm.TextSecondary;
        if (TxtSectionSunMoon != null) TxtSectionSunMoon.Foreground = tm.TextSecondary;
        if (TxtSectionUnits != null) TxtSectionUnits.Foreground = tm.TextSecondary;
        if (TxtSectionPresets != null) TxtSectionPresets.Foreground = tm.TextSecondary;
        if (TxtSectionOffline != null) TxtSectionOffline.Foreground = tm.TextSecondary;
        if (TxtSectionTheme != null) TxtSectionTheme.Foreground = tm.TextSecondary;

        if (SunMoonCardBorder != null)
        {
            SunMoonCardBorder.Background = tm.ButtonInactiveBackground;
            SunMoonCardBorder.BorderBrush = tm.ButtonInactiveBorder;
        }
        if (SunMoonCardDivider != null) SunMoonCardDivider.Background = tm.ButtonInactiveBorder;
        if (TxtCelestialDate != null) TxtCelestialDate.Foreground = tm.TextSecondary;
        if (TxtSunriseTopo != null) TxtSunriseTopo.Foreground = tm.TextPrimary;
        if (TxtSunsetTopo != null) TxtSunsetTopo.Foreground = tm.TextPrimary;
        if (TxtMoonriseTopo != null) TxtMoonriseTopo.Foreground = tm.TextPrimary;
        if (TxtMoonsetTopo != null) TxtMoonsetTopo.Foreground = tm.TextPrimary;
        if (TxtDaylightSummary != null) TxtDaylightSummary.Foreground = tm.TextSecondary;
        if (TxtMoonPhase != null) TxtMoonPhase.Foreground = tm.TextSecondary;
        if (TxtSunAstroComparison != null) TxtSunAstroComparison.Foreground = tm.TextSecondary;

        UpdateOptFovButtonsTheme(isDark);

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

        // 5. Update Tab Buttons & Preset Buttons
        UpdateTabButtonsTheme(isDark);
        UpdateUnitsButtonsTheme(isDark);
        UpdatePresetButtonsTheme(isDark);
    }

    private void UpdateTabButtonsTheme(bool isDark)
    {
        var tm = ThemeManager.Current;

        void StyleTabBtn(Button btn, bool isSelected)
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

    private void UpdatePresetButtonsTheme(bool isDark)
    {
        string selectedName = ViewModel.LocationPresetName;
        var tm = ThemeManager.Current;

        var selectedBg = tm.ButtonActiveBackground;
        var selectedBorder = tm.ButtonActiveBorder;
        var selectedFg = tm.ButtonActiveForeground;

        var unselectedBg = tm.ButtonInactiveBackground;
        var unselectedBorder = tm.ButtonInactiveBorder;
        var unselectedFg = tm.ButtonInactiveForeground;

        void StylePreset(Button btn, string presetName)
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
        StylePreset(BtnPresetCanyon, "Grand Canyon (Mather Point), AZ");
        StylePreset(BtnPresetPikes, "Pikes Peak, CO");
        StylePreset(BtnPresetYosemite, "Yosemite (Glacier Point), CA");
        StylePreset(BtnPresetWashington, "Mount Washington, NH");
    }

    private void BtnPresetRincon_Click(object sender, RoutedEventArgs e) =>
        ApplyPresetAndClose("Tucson East (Rincon Ridges), AZ");

    private void BtnPresetLemmon_Click(object sender, RoutedEventArgs e) =>
        ApplyPresetAndClose("Mount Lemmon, AZ");

    private void BtnPresetRainier_Click(object sender, RoutedEventArgs e) =>
        ApplyPresetAndClose("Mount Rainier (Paradise), WA");

    private void BtnPresetCanyon_Click(object sender, RoutedEventArgs e) =>
        ApplyPresetAndClose("Grand Canyon (Mather Point), AZ");

    private void BtnPresetPikes_Click(object sender, RoutedEventArgs e) =>
        ApplyPresetAndClose("Pikes Peak, CO");

    private void BtnPresetYosemite_Click(object sender, RoutedEventArgs e) =>
        ApplyPresetAndClose("Yosemite (Glacier Point), CA");

    private void BtnPresetWashington_Click(object sender, RoutedEventArgs e) =>
        ApplyPresetAndClose("Mount Washington, NH");

    private void ApplyPresetAndClose(string name)
    {
        var preset = ViewModel.Presets.FirstOrDefault(p => p.Name == name);
        if (preset != null)
        {
            ViewModel.ApplyPreset(preset);
            UpdatePresetButtonsTheme(ThemeManager.Current.IsDark);
        }
        BtnCloseOptions_Click(this, new RoutedEventArgs());
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ScheduleHorizonGeneration(immediate: true);
    }
}
