using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PointedHorizons.Services;
using PointedHorizons.ViewModels;

namespace PointedHorizons;

public partial class MainPage : Page
{
    private string _currentTab = "FullScreenMap";
    private bool _isSmallScreen;
    private double _lastWindowWidth;

    private DispatcherTimer? _longPressRecordTimer;
    private DispatcherTimer? _longPressGpsTimer;
    private DispatcherTimer? _toastTimer;
    private bool _isLongPressTriggered;
    private bool _isRecordButtonPressed;
    private bool _isGpsLongPressTriggered;
    private bool _isGpsButtonPressed;
    private bool? _lastRecordingState;

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

        if (BtnTitleRecordTrack != null)
        {
            BtnTitleRecordTrack.PreviewMouseLeftButtonDown += BtnTitleRecordTrack_PointerDown;
            BtnTitleRecordTrack.AddHandler(UIElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler(BtnTitleRecordTrack_PointerDown), true);

            BtnTitleRecordTrack.PreviewMouseLeftButtonUp += BtnTitleRecordTrack_PointerUp;
            BtnTitleRecordTrack.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler(BtnTitleRecordTrack_PointerUp), true);

            BtnTitleRecordTrack.MouseLeave += BtnTitleRecordTrack_MouseLeave;
            BtnTitleRecordTrack.LostMouseCapture += BtnTitleRecordTrack_LostCapture;
            BtnTitleRecordTrack.ContextMenuOpening += (s, e) => e.Handled = true;
        }

        if (BtnTitleToggleGps != null)
        {
            BtnTitleToggleGps.PreviewMouseLeftButtonDown += BtnTitleToggleGps_PointerDown;
            BtnTitleToggleGps.AddHandler(UIElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler(BtnTitleToggleGps_PointerDown), true);

            BtnTitleToggleGps.PreviewMouseLeftButtonUp += BtnTitleToggleGps_PointerUp;
            BtnTitleToggleGps.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler(BtnTitleToggleGps_PointerUp), true);

            BtnTitleToggleGps.MouseLeave += BtnTitleToggleGps_MouseLeave;
            BtnTitleToggleGps.LostMouseCapture += BtnTitleToggleGps_LostCapture;
            BtnTitleToggleGps.ContextMenuOpening += (s, e) => e.Handled = true;
        }

        if (FullScreenMapControlView != null)
        {
            FullScreenMapControlView.ConfigurePmTilesRequested += (s, e) => ShowPmTilesModal();
            FullScreenMapControlView.OpenTracksPageRequested += (s, e) => SwitchTab("Tracks");
            FullScreenMapControlView.TrackRecovered += (s, track) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    ShowTrackActionToast($"Previous track '{track.Name}' stopped and saved", "⏹️");
                });
            };
            FullScreenMapControlView.RecordingStateChanged += (s, isRecording) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    UpdateTitleBarButtonsState();
                    // Only show notification when the state actually transitions from a known prior state
                    if (_lastRecordingState.HasValue && _lastRecordingState.Value != isRecording)
                    {
                        ShowTrackActionToast(isRecording ? "Track recording started" : "Track recording stopped and saved", isRecording ? "⏺️" : "⏹️");
                    }
                    _lastRecordingState = isRecording;
                });
            };
            FullScreenMapControlView.GpsStateChanged += (s, e) => Dispatcher.BeginInvoke(UpdateTitleBarButtonsState);
            FullScreenMapControlView.GpsStatusChanged += (s, e) => Dispatcher.BeginInvoke(UpdateTitleBarButtonsState);
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

        if (ViewCelestialTab != null)
        {
            ViewCelestialTab.MouseWheel += (s, e) =>
            {
                CelestialPageView?.HandleExternalMouseWheel(e.Delta);
                e.Handled = true;
            };
        }

        if (ViewOptionsTab != null)
        {
            ViewOptionsTab.MouseWheel += (s, e) =>
            {
                if (OptionsScrollViewer != null)
                {
                    OptionsScrollViewer.ScrollToVerticalOffset(OptionsScrollViewer.VerticalOffset - (e.Delta / 2.0));
                    e.Handled = true;
                }
            };
        }
        if (OptionsScrollViewer != null)
        {
            OptionsScrollViewer.MouseWheel += (s, e) =>
            {
                OptionsScrollViewer.ScrollToVerticalOffset(OptionsScrollViewer.VerticalOffset - (e.Delta / 2.0));
                e.Handled = true;
            };
        }
        if (OptionsPageContainer != null)
        {
            OptionsPageContainer.MouseWheel += (s, e) =>
            {
                if (OptionsScrollViewer != null)
                {
                    OptionsScrollViewer.ScrollToVerticalOffset(OptionsScrollViewer.VerticalOffset - (e.Delta / 2.0));
                    e.Handled = true;
                }
            };
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
            if (CardOptionsOffline != null) CardOptionsOffline.Visibility = Visibility.Collapsed;
            if (PmTilesConfigModal != null) PmTilesConfigModal.Visibility = Visibility.Collapsed;
            if (PmTilesModalBackdrop != null) PmTilesModalBackdrop.Visibility = Visibility.Collapsed;
        }

        UpdateTheme(ViewModel.IsDarkTheme);
        UpdateResponsiveLayout(ActualWidth, ActualHeight);
        UpdateUnitsButtonsTheme(ViewModel.IsDarkTheme);
        UpdateTitleBarButtonsState();
        _lastRecordingState = FullScreenMapControlView?.IsRecordingActive ?? false;

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

        MapTilerService.ApiKeyChanged += (s, ev) =>
        {
            Dispatcher.BeginInvoke(UpdateMapTilerConfigDisplay);
        };
        UpdateMapTilerConfigDisplay();

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
        _lastWindowWidth = width;

        bool isSmall = width < 768 || (height > 0 && height < 600);
        bool modeChanged = isSmall != _isSmallScreen;
        _isSmallScreen = isSmall;

        if (isSmall)
        {
            // Top App Bar: Touch-friendly on small screen mobile
            if (TopHeaderBorder != null)
            {
                TopHeaderBorder.Padding = new Thickness(10, 8, 10, 8);
            }
            if (AppBrandingPanel != null)
            {
                if (width < 540)
                {
                    AppBrandingPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    AppBrandingPanel.Visibility = Visibility.Visible;
                    if (TxtAppTitle != null)
                    {
                        TxtAppTitle.Visibility = Visibility.Visible;
                        TxtAppTitle.Text = width < 700 ? "Horizons" : "Pointed Horizons";
                        TxtAppTitle.FontSize = 16;
                    }
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

            // Title bar action buttons: wider, finger-friendly touch targets on small screen mobile
            if (BtnTitleRecordTrack != null)
            {
                BtnTitleRecordTrack.MinHeight = 44;
                BtnTitleRecordTrack.MinWidth = 56;
                BtnTitleRecordTrack.Padding = new Thickness(14, 8, 14, 8);
                BtnTitleRecordTrack.FontSize = 18;
                if (IconTitleRecordTrack != null) IconTitleRecordTrack.IconSize = 22;
            }
            if (BtnTitleToggleGps != null)
            {
                BtnTitleToggleGps.MinHeight = 44;
                BtnTitleToggleGps.MinWidth = 56;
                BtnTitleToggleGps.Padding = new Thickness(6, 3, 6, 3);
                if (IconTitleGps != null) IconTitleGps.IconSize = 34;
            }

            // Page choice button: Finger friendly on small screen mobile, sized to never collide with action buttons
            double actionButtonsWidth = 124.0;
            double brandingWidth = (AppBrandingPanel != null && AppBrandingPanel.Visibility == Visibility.Visible) ? 95.0 : 0.0;
            double availableForNav = width - 20.0 - actionButtonsWidth - brandingWidth - 16.0;

            if (BtnCompactNav != null)
            {
                BtnCompactNav.MinHeight = 44;
                BtnCompactNav.Padding = new Thickness(14, 8, 14, 8);
                BtnCompactNav.FontSize = 13.5;
                BtnCompactNav.FontWeight = FontWeights.Bold;
                if (availableForNav > 60)
                {
                    BtnCompactNav.MaxWidth = availableForNav;
                }
                else
                {
                    BtnCompactNav.ClearValue(FrameworkElement.MaxWidthProperty);
                }
            }

            UpdateCompactNavContent(width);

            // Page Navigation dropdown panel & buttons: All uniform height (MinHeight=48)
            if (BtnNavPageMap != null) { BtnNavPageMap.MinHeight = 48; BtnNavPageMap.Padding = new Thickness(14, 12, 14, 12); BtnNavPageMap.FontSize = 14.5; }
            if (BtnNavPageCelestial != null) { BtnNavPageCelestial.MinHeight = 48; BtnNavPageCelestial.Padding = new Thickness(14, 12, 14, 12); BtnNavPageCelestial.FontSize = 14.5; }
            if (BtnNavPageAimMap != null) { BtnNavPageAimMap.MinHeight = 48; BtnNavPageAimMap.Padding = new Thickness(14, 12, 14, 12); BtnNavPageAimMap.FontSize = 14.5; }
            if (BtnNavPageHorizon != null) { BtnNavPageHorizon.MinHeight = 48; BtnNavPageHorizon.Padding = new Thickness(14, 12, 14, 12); BtnNavPageHorizon.FontSize = 14.5; }
            if (BtnNavPageTracks != null) { BtnNavPageTracks.MinHeight = 48; BtnNavPageTracks.Padding = new Thickness(14, 12, 14, 12); BtnNavPageTracks.FontSize = 14.5; }
            if (BtnNavPageOptions != null) { BtnNavPageOptions.MinHeight = 48; BtnNavPageOptions.Padding = new Thickness(14, 12, 14, 12); BtnNavPageOptions.FontSize = 14.5; }
            if (BtnNavPageAbout != null) { BtnNavPageAbout.MinHeight = 48; BtnNavPageAbout.Padding = new Thickness(14, 12, 14, 12); BtnNavPageAbout.FontSize = 14.5; }
            if (BtnCloseCompactNav != null) { BtnCloseCompactNav.MinHeight = 36; BtnCloseCompactNav.MinWidth = 36; BtnCloseCompactNav.FontSize = 14; }

            // Display toast notifications center screen
            if (TrackActionToast != null)
            {
                TrackActionToast.HorizontalAlignment = HorizontalAlignment.Center;
                TrackActionToast.VerticalAlignment = VerticalAlignment.Center;
                TrackActionToast.Margin = new Thickness(0);
            }

            // Content button in Horizon view: Twice as big
            if (BtnRefresh != null)
            {
                BtnRefresh.MinHeight = 44;
                BtnRefresh.Padding = new Thickness(14, 8, 14, 8);
                BtnRefresh.FontSize = 13;
            }
            if (SplitBarBorder != null)
            {
                SplitBarBorder.Height = double.NaN;
            }

            // Application options buttons: Twice as big on mobile
            if (BtnUnitFeet != null) { BtnUnitFeet.MinHeight = 44; BtnUnitFeet.Padding = new Thickness(12, 10, 12, 10); BtnUnitFeet.FontSize = 12.5; }
            if (BtnUnitMeters != null) { BtnUnitMeters.MinHeight = 44; BtnUnitMeters.Padding = new Thickness(12, 10, 12, 10); BtnUnitMeters.FontSize = 12.5; }
            if (BtnOpenPmTilesConfig != null) { BtnOpenPmTilesConfig.MinHeight = 48; BtnOpenPmTilesConfig.Padding = new Thickness(10, 12, 10, 12); BtnOpenPmTilesConfig.FontSize = 12.5; BtnOpenPmTilesConfig.HorizontalAlignment = HorizontalAlignment.Stretch; }
            if (BtnToggleAppTheme != null) { BtnToggleAppTheme.MinHeight = 48; BtnToggleAppTheme.Padding = new Thickness(10, 12, 10, 12); BtnToggleAppTheme.FontSize = 12.5; BtnToggleAppTheme.HorizontalAlignment = HorizontalAlignment.Stretch; }
            if (BtnSaveGeoNamesUsername != null) { BtnSaveGeoNamesUsername.MinHeight = 44; BtnSaveGeoNamesUsername.Padding = new Thickness(12, 8, 12, 8); BtnSaveGeoNamesUsername.FontSize = 12.5; }
            if (BtnSaveMapTilerApiKey != null) { BtnSaveMapTilerApiKey.MinHeight = 44; BtnSaveMapTilerApiKey.Padding = new Thickness(12, 8, 12, 8); BtnSaveMapTilerApiKey.FontSize = 12.5; }
            if (BtnCheckUpdates != null) { BtnCheckUpdates.MinHeight = 48; BtnCheckUpdates.Padding = new Thickness(10, 12, 10, 12); BtnCheckUpdates.FontSize = 12.5; BtnCheckUpdates.HorizontalAlignment = HorizontalAlignment.Stretch; }

            if (GridGeoNamesInput != null && TxtGeoNamesUsernameInput != null && BtnSaveGeoNamesUsername != null)
            {
                Grid.SetRow(TxtGeoNamesUsernameInput, 0);
                Grid.SetColumn(TxtGeoNamesUsernameInput, 0);
                Grid.SetColumnSpan(TxtGeoNamesUsernameInput, 2);
                TxtGeoNamesUsernameInput.Margin = new Thickness(0, 0, 0, 8);

                Grid.SetRow(BtnSaveGeoNamesUsername, 1);
                Grid.SetColumn(BtnSaveGeoNamesUsername, 0);
                Grid.SetColumnSpan(BtnSaveGeoNamesUsername, 2);
                BtnSaveGeoNamesUsername.Margin = new Thickness(0, 0, 0, 0);
                BtnSaveGeoNamesUsername.HorizontalAlignment = HorizontalAlignment.Stretch;
            }

            if (GridMapTilerInput != null && TxtMapTilerApiKeyInput != null && BtnSaveMapTilerApiKey != null)
            {
                Grid.SetRow(TxtMapTilerApiKeyInput, 0);
                Grid.SetColumn(TxtMapTilerApiKeyInput, 0);
                Grid.SetColumnSpan(TxtMapTilerApiKeyInput, 2);
                TxtMapTilerApiKeyInput.Margin = new Thickness(0, 0, 0, 8);

                Grid.SetRow(BtnSaveMapTilerApiKey, 1);
                Grid.SetColumn(BtnSaveMapTilerApiKey, 0);
                Grid.SetColumnSpan(BtnSaveMapTilerApiKey, 2);
                BtnSaveMapTilerApiKey.Margin = new Thickness(0, 0, 0, 0);
                BtnSaveMapTilerApiKey.HorizontalAlignment = HorizontalAlignment.Stretch;
            }

            if (OptionsPageContainer != null)
            {
                OptionsPageContainer.Margin = new Thickness(10, 12, 10, 12);
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
            if (AppBrandingPanel != null)
            {
                AppBrandingPanel.Visibility = Visibility.Visible;
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
                BtnCompactNav.ClearValue(FrameworkElement.MaxWidthProperty);
            }

            // Title bar action buttons: wider, finger-friendly on desktop
            if (BtnTitleRecordTrack != null)
            {
                BtnTitleRecordTrack.MinHeight = 32;
                BtnTitleRecordTrack.MinWidth = 44;
                BtnTitleRecordTrack.Padding = new Thickness(12, 4, 12, 4);
                BtnTitleRecordTrack.FontSize = 13.5;
                if (IconTitleRecordTrack != null) IconTitleRecordTrack.IconSize = 18;
            }
            if (BtnTitleToggleGps != null)
            {
                BtnTitleToggleGps.MinHeight = 32;
                BtnTitleToggleGps.MinWidth = 44;
                BtnTitleToggleGps.Padding = new Thickness(5, 2, 5, 2);
                if (IconTitleGps != null) IconTitleGps.IconSize = 26;
            }

            CloseCompactNav();

            // Tab button labels and availability (when header tabs are visible on large screens)
            if (BtnTabMap != null)
            {
                BtnTabMap.Content = "🗺️ Map";
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
            if (BtnTabOptions != null)
            {
                BtnTabOptions.Content = "⚙️ Options";
            }

            if (BtnRefresh != null)
            {
                BtnRefresh.MinHeight = 0;
                BtnRefresh.Padding = new Thickness(10, 4, 10, 4);
                BtnRefresh.FontSize = 11;
            }

            if (BtnNavPageMap != null) { BtnNavPageMap.MinHeight = 0; BtnNavPageMap.Padding = new Thickness(10, 7, 10, 7); BtnNavPageMap.FontSize = 11; }
            if (BtnNavPageCelestial != null) { BtnNavPageCelestial.MinHeight = 0; BtnNavPageCelestial.Padding = new Thickness(10, 7, 10, 7); BtnNavPageCelestial.FontSize = 11; }
            if (BtnNavPageAimMap != null) { BtnNavPageAimMap.MinHeight = 0; BtnNavPageAimMap.Padding = new Thickness(10, 7, 10, 7); BtnNavPageAimMap.FontSize = 11; }
            if (BtnNavPageHorizon != null) { BtnNavPageHorizon.MinHeight = 0; BtnNavPageHorizon.Padding = new Thickness(10, 7, 10, 7); BtnNavPageHorizon.FontSize = 11; }
            if (BtnNavPageTracks != null) { BtnNavPageTracks.MinHeight = 0; BtnNavPageTracks.Padding = new Thickness(10, 7, 10, 7); BtnNavPageTracks.FontSize = 11; }
            if (BtnNavPageOptions != null) { BtnNavPageOptions.MinHeight = 0; BtnNavPageOptions.Padding = new Thickness(10, 7, 10, 7); BtnNavPageOptions.FontSize = 11; }
            if (BtnNavPageAbout != null) { BtnNavPageAbout.MinHeight = 0; BtnNavPageAbout.Padding = new Thickness(10, 7, 10, 7); BtnNavPageAbout.FontSize = 11; }
            if (BtnCloseCompactNav != null) { BtnCloseCompactNav.MinHeight = 0; BtnCloseCompactNav.MinWidth = 0; BtnCloseCompactNav.FontSize = 10; }

            if (TrackActionToast != null)
            {
                TrackActionToast.HorizontalAlignment = HorizontalAlignment.Center;
                TrackActionToast.VerticalAlignment = VerticalAlignment.Center;
                TrackActionToast.Margin = new Thickness(0);
            }

            if (SplitBarBorder != null)
            {
                SplitBarBorder.Height = 36;
            }

            if (BtnUnitFeet != null) { BtnUnitFeet.MinHeight = 0; BtnUnitFeet.Padding = new Thickness(12, 5, 12, 5); BtnUnitFeet.FontSize = 11; }
            if (BtnUnitMeters != null) { BtnUnitMeters.MinHeight = 0; BtnUnitMeters.Padding = new Thickness(12, 5, 12, 5); BtnUnitMeters.FontSize = 11; }
            if (BtnOpenPmTilesConfig != null) { BtnOpenPmTilesConfig.MinHeight = 0; BtnOpenPmTilesConfig.Padding = new Thickness(8, 5, 8, 5); BtnOpenPmTilesConfig.FontSize = 11; BtnOpenPmTilesConfig.HorizontalAlignment = HorizontalAlignment.Left; }
            if (BtnToggleAppTheme != null) { BtnToggleAppTheme.MinHeight = 0; BtnToggleAppTheme.Padding = new Thickness(8, 5, 8, 5); BtnToggleAppTheme.FontSize = 11; BtnToggleAppTheme.HorizontalAlignment = HorizontalAlignment.Left; }
            if (BtnSaveGeoNamesUsername != null) { BtnSaveGeoNamesUsername.MinHeight = 0; BtnSaveGeoNamesUsername.Padding = new Thickness(10, 4, 10, 4); BtnSaveGeoNamesUsername.FontSize = 10; }
            if (BtnSaveMapTilerApiKey != null) { BtnSaveMapTilerApiKey.MinHeight = 0; BtnSaveMapTilerApiKey.Padding = new Thickness(10, 4, 10, 4); BtnSaveMapTilerApiKey.FontSize = 10; }
            if (BtnCheckUpdates != null) { BtnCheckUpdates.MinHeight = 0; BtnCheckUpdates.Padding = new Thickness(8, 5, 8, 5); BtnCheckUpdates.FontSize = 11; BtnCheckUpdates.HorizontalAlignment = HorizontalAlignment.Left; }

            if (GridGeoNamesInput != null && TxtGeoNamesUsernameInput != null && BtnSaveGeoNamesUsername != null)
            {
                Grid.SetRow(TxtGeoNamesUsernameInput, 0);
                Grid.SetColumn(TxtGeoNamesUsernameInput, 0);
                Grid.SetColumnSpan(TxtGeoNamesUsernameInput, 1);
                TxtGeoNamesUsernameInput.Margin = new Thickness(0, 0, 0, 0);

                Grid.SetRow(BtnSaveGeoNamesUsername, 0);
                Grid.SetColumn(BtnSaveGeoNamesUsername, 1);
                Grid.SetColumnSpan(BtnSaveGeoNamesUsername, 1);
                BtnSaveGeoNamesUsername.Margin = new Thickness(8, 0, 0, 0);
                BtnSaveGeoNamesUsername.HorizontalAlignment = HorizontalAlignment.Left;
            }

            if (GridMapTilerInput != null && TxtMapTilerApiKeyInput != null && BtnSaveMapTilerApiKey != null)
            {
                Grid.SetRow(TxtMapTilerApiKeyInput, 0);
                Grid.SetColumn(TxtMapTilerApiKeyInput, 0);
                Grid.SetColumnSpan(TxtMapTilerApiKeyInput, 1);
                TxtMapTilerApiKeyInput.Margin = new Thickness(0, 0, 0, 0);

                Grid.SetRow(BtnSaveMapTilerApiKey, 0);
                Grid.SetColumn(BtnSaveMapTilerApiKey, 1);
                Grid.SetColumnSpan(BtnSaveMapTilerApiKey, 1);
                BtnSaveMapTilerApiKey.Margin = new Thickness(8, 0, 0, 0);
                BtnSaveMapTilerApiKey.HorizontalAlignment = HorizontalAlignment.Left;
            }

            if (OptionsPageContainer != null)
            {
                OptionsPageContainer.Margin = new Thickness(16, 20, 16, 20);
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
        PmTilesConfigModal?.SetCompactMode(isSmall);

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
            if (ViewOptionsTab != null) ViewOptionsTab.Visibility = Visibility.Collapsed;
            if (ViewAboutTab != null) ViewAboutTab.Visibility = Visibility.Collapsed;

            FullScreenMapControlView?.ResizeMap();
        }
        else if (_currentTab == "Celestial")
        {
            if (ViewFullScreenMapTab != null) ViewFullScreenMapTab.Visibility = Visibility.Collapsed;
            if (ViewCelestialTab != null) ViewCelestialTab.Visibility = Visibility.Visible;
            if (ViewHorizonTab != null) ViewHorizonTab.Visibility = Visibility.Collapsed;
            if (ViewTracksTab != null) ViewTracksTab.Visibility = Visibility.Collapsed;
            if (ViewOptionsTab != null) ViewOptionsTab.Visibility = Visibility.Collapsed;
            if (ViewAboutTab != null) ViewAboutTab.Visibility = Visibility.Collapsed;
        }
        else if (_currentTab == "Tracks")
        {
            if (ViewFullScreenMapTab != null) ViewFullScreenMapTab.Visibility = Visibility.Collapsed;
            if (ViewCelestialTab != null) ViewCelestialTab.Visibility = Visibility.Collapsed;
            if (ViewHorizonTab != null) ViewHorizonTab.Visibility = Visibility.Collapsed;
            if (ViewTracksTab != null) ViewTracksTab.Visibility = Visibility.Visible;
            if (ViewOptionsTab != null) ViewOptionsTab.Visibility = Visibility.Collapsed;
            if (ViewAboutTab != null) ViewAboutTab.Visibility = Visibility.Collapsed;

            TracksControlView?.RefreshView();
        }
        else if (_currentTab == "Options")
        {
            if (ViewFullScreenMapTab != null) ViewFullScreenMapTab.Visibility = Visibility.Collapsed;
            if (ViewCelestialTab != null) ViewCelestialTab.Visibility = Visibility.Collapsed;
            if (ViewHorizonTab != null) ViewHorizonTab.Visibility = Visibility.Collapsed;
            if (ViewTracksTab != null) ViewTracksTab.Visibility = Visibility.Collapsed;
            if (ViewOptionsTab != null) ViewOptionsTab.Visibility = Visibility.Visible;
            if (ViewAboutTab != null) ViewAboutTab.Visibility = Visibility.Collapsed;

            UpdateUnitsButtonsTheme(ThemeManager.Current.IsDark);
            UpdateGeoNamesConfigDisplay();
            UpdateMapTilerConfigDisplay();
        }
        else if (_currentTab == "About")
        {
            if (ViewFullScreenMapTab != null) ViewFullScreenMapTab.Visibility = Visibility.Collapsed;
            if (ViewCelestialTab != null) ViewCelestialTab.Visibility = Visibility.Collapsed;
            if (ViewHorizonTab != null) ViewHorizonTab.Visibility = Visibility.Collapsed;
            if (ViewTracksTab != null) ViewTracksTab.Visibility = Visibility.Collapsed;
            if (ViewOptionsTab != null) ViewOptionsTab.Visibility = Visibility.Collapsed;
            if (ViewAboutTab != null) ViewAboutTab.Visibility = Visibility.Visible;

            AboutPageView?.UpdateTheme(ThemeManager.Current.IsDark);
        }
        else
        {
            if (ViewFullScreenMapTab != null) ViewFullScreenMapTab.Visibility = Visibility.Collapsed;
            if (ViewCelestialTab != null) ViewCelestialTab.Visibility = Visibility.Collapsed;
            if (ViewHorizonTab != null) ViewHorizonTab.Visibility = Visibility.Visible;
            if (ViewTracksTab != null) ViewTracksTab.Visibility = Visibility.Collapsed;
            if (ViewOptionsTab != null) ViewOptionsTab.Visibility = Visibility.Collapsed;
            if (ViewAboutTab != null) ViewAboutTab.Visibility = Visibility.Collapsed;

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

        UpdateCompactNavContent(_lastWindowWidth);

        UpdateTabButtonsTheme(tm.IsDark);
        UpdateCompactNavButtonsTheme(tm.IsDark);
    }

    private void UpdateCompactNavContent(double width)
    {
        if (BtnCompactNav == null) return;

        // If screen width is very compact (< 480px), use concise labels to ensure zero collision with action buttons
        bool isVeryCompact = width > 0 && width < 480;

        BtnCompactNav.Content = _currentTab switch
        {
            "FullScreenMap" => "🗺️ Map ▾",
            "Celestial" => isVeryCompact ? "☀️ Sun/Moon ▾" : "☀️ Sun & Moon ▾",
            "HorizonMap" => isVeryCompact ? "🧭 View ▾" : "🧭 View Map ▾",
            "Horizon" => isVeryCompact ? "🌄 Horizon ▾" : "🌄 Horizon View ▾",
            "Tracks" => isVeryCompact ? "📁 GPX ▾" : "📁 Gpx Files ▾",
            "Options" => "⚙️ Options ▾",
            "About" => "ℹ️ About ▾",
            _ => "🗺️ Map ▾"
        };
    }

    private void BtnTabMap_Click(object sender, RoutedEventArgs e) => SwitchTab("FullScreenMap");
    private void BtnTabCelestial_Click(object sender, RoutedEventArgs e) => SwitchTab("Celestial");
    private void BtnTabAimMap_Click(object sender, RoutedEventArgs e) => SwitchTab("HorizonMap");
    private void BtnTabHorizon_Click(object sender, RoutedEventArgs e) => SwitchTab("Horizon");
    private void BtnTabTracks_Click(object sender, RoutedEventArgs e) => SwitchTab("Tracks");
    private void BtnTabOptions_Click(object sender, RoutedEventArgs e) => SwitchTab("Options");
    private void BtnTabAbout_Click(object sender, RoutedEventArgs e) => SwitchTab("About");

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

    private void BtnNavPageOptions_Click(object sender, RoutedEventArgs e)
    {
        SwitchTab("Options");
        CloseCompactNav();
    }

    private void BtnNavPageAbout_Click(object sender, RoutedEventArgs e)
    {
        SwitchTab("About");
        CloseCompactNav();
    }

    #region Title Bar Action Buttons (Record Track & GPS Toggle)

    private void BtnTitleRecordTrack_PointerDown(object sender, MouseButtonEventArgs e)
    {
        if (_isRecordButtonPressed) return;
        _isRecordButtonPressed = true;
        _isLongPressTriggered = false;

        if (BtnTitleRecordTrack != null)
        {
            BtnTitleRecordTrack.Opacity = 0.75;
        }

        if (_longPressRecordTimer == null)
        {
            _longPressRecordTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
            _longPressRecordTimer.Tick += LongPressRecordTimer_Tick;
        }
        _longPressRecordTimer.Stop();
        _longPressRecordTimer.Start();
    }

    private void LongPressRecordTimer_Tick(object? sender, EventArgs e)
    {
        _longPressRecordTimer?.Stop();
        if (!_isRecordButtonPressed) return;

        _isLongPressTriggered = true;
        _isRecordButtonPressed = false;

        if (BtnTitleRecordTrack != null)
        {
            BtnTitleRecordTrack.Opacity = 1.0;
        }

        if (FullScreenMapControlView == null) return;
        FullScreenMapControlView.ToggleRecording();
    }

    private void BtnTitleRecordTrack_PointerUp(object sender, MouseButtonEventArgs e)
    {
        if (_longPressRecordTimer != null && _longPressRecordTimer.IsEnabled)
        {
            _longPressRecordTimer.Stop();
        }
    }

    private void BtnTitleRecordTrack_MouseLeave(object sender, MouseEventArgs e)
    {
        CancelRecordLongPress();
    }

    private void BtnTitleRecordTrack_LostCapture(object sender, MouseEventArgs e)
    {
        CancelRecordLongPress();
    }

    private void CancelRecordLongPress()
    {
        if (_longPressRecordTimer != null && _longPressRecordTimer.IsEnabled)
        {
            _longPressRecordTimer.Stop();
        }
        _isRecordButtonPressed = false;
        if (BtnTitleRecordTrack != null)
        {
            BtnTitleRecordTrack.Opacity = 1.0;
        }
    }

    private void BtnTitleRecordTrack_Click(object sender, RoutedEventArgs e)
    {
        if (_longPressRecordTimer != null && _longPressRecordTimer.IsEnabled)
        {
            _longPressRecordTimer.Stop();
        }

        if (BtnTitleRecordTrack != null)
        {
            BtnTitleRecordTrack.Opacity = 1.0;
        }

        if (_isLongPressTriggered)
        {
            // Already handled by long press timer tick
            _isLongPressTriggered = false;
            _isRecordButtonPressed = false;
            return;
        }

        _isRecordButtonPressed = false;

        // Normal tap/click without holding: do NOT start/stop recording, prompt user to long press
        bool isRecording = FullScreenMapControlView?.IsRecordingActive ?? false;
        ShowTrackActionToast(isRecording ? "Long press to stop recording" : "Long press to start recording", "ℹ️");
    }

    private void BtnTitleToggleGps_PointerDown(object sender, MouseButtonEventArgs e)
    {
        if (_isGpsButtonPressed) return;
        _isGpsButtonPressed = true;
        _isGpsLongPressTriggered = false;

        if (BtnTitleToggleGps != null)
        {
            BtnTitleToggleGps.Opacity = 0.65;
        }

        if (_longPressGpsTimer == null)
        {
            _longPressGpsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
            _longPressGpsTimer.Tick += LongPressGpsTimer_Tick;
        }
        _longPressGpsTimer.Stop();
        _longPressGpsTimer.Start();
    }

    private void LongPressGpsTimer_Tick(object? sender, EventArgs e)
    {
        _longPressGpsTimer?.Stop();
        if (!_isGpsButtonPressed) return;

        _isGpsLongPressTriggered = true;
        _isGpsButtonPressed = false;

        if (BtnTitleToggleGps != null)
        {
            BtnTitleToggleGps.Opacity = 1.0;
        }

        if (FullScreenMapControlView == null) return;
        if (FullScreenMapControlView.IsRecordingActive)
        {
            ShowTrackActionToast("GPS cannot be turned off while recording track", "⚠️");
            return;
        }

        FullScreenMapControlView.ToggleGps();
        bool isGpsNow = FullScreenMapControlView.IsGpsActive;
        ShowTrackActionToast(isGpsNow ? "GPS turned on" : "GPS turned off", "🛰️");
    }

    private void BtnTitleToggleGps_PointerUp(object sender, MouseButtonEventArgs e)
    {
        if (_longPressGpsTimer != null && _longPressGpsTimer.IsEnabled)
        {
            _longPressGpsTimer.Stop();
        }
    }

    private void BtnTitleToggleGps_MouseLeave(object sender, MouseEventArgs e)
    {
        CancelGpsLongPress();
    }

    private void BtnTitleToggleGps_LostCapture(object sender, MouseEventArgs e)
    {
        CancelGpsLongPress();
    }

    private void CancelGpsLongPress()
    {
        if (_longPressGpsTimer != null && _longPressGpsTimer.IsEnabled)
        {
            _longPressGpsTimer.Stop();
        }
        _isGpsButtonPressed = false;
        if (BtnTitleToggleGps != null)
        {
            BtnTitleToggleGps.Opacity = 1.0;
        }
    }

    private void BtnTitleToggleGps_Click(object sender, RoutedEventArgs e)
    {
        if (_longPressGpsTimer != null && _longPressGpsTimer.IsEnabled)
        {
            _longPressGpsTimer.Stop();
        }

        if (BtnTitleToggleGps != null)
        {
            BtnTitleToggleGps.Opacity = 1.0;
        }

        if (_isGpsLongPressTriggered)
        {
            _isGpsLongPressTriggered = false;
            _isGpsButtonPressed = false;
            return;
        }

        _isGpsButtonPressed = false;

        if (FullScreenMapControlView != null && FullScreenMapControlView.IsRecordingActive)
        {
            ShowTrackActionToast("GPS cannot be turned off while recording track", "⚠️");
            return;
        }

        bool isGps = FullScreenMapControlView?.IsGpsActive ?? false;
        ShowTrackActionToast(isGps ? "Long press to turn off GPS" : "Long press to turn on GPS", "🛰️");
    }

    public void ShowTrackActionToast(string message, string icon = "ℹ️")
    {
        if (TrackActionToast == null || TxtTrackActionToast == null) return;

        TxtTrackActionToast.Text = message;
        if (TxtTrackActionToastIcon != null)
        {
            TxtTrackActionToastIcon.Text = icon;
        }
        TrackActionToast.Visibility = Visibility.Visible;

        if (_toastTimer == null)
        {
            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2200) };
            _toastTimer.Tick += (s, e) =>
            {
                _toastTimer.Stop();
                if (TrackActionToast != null)
                {
                    TrackActionToast.Visibility = Visibility.Collapsed;
                }
            };
        }
        else
        {
            _toastTimer.Stop();
        }
        _toastTimer.Start();
    }

    private void UpdateTitleBarButtonsState()
    {
        var tm = ThemeManager.Current;
        bool isRecording = FullScreenMapControlView?.IsRecordingActive ?? false;
        bool isGps = FullScreenMapControlView?.IsGpsActive ?? false;
        string gpsStatus = FullScreenMapControlView?.CurrentGpsStatusText ?? (isGps ? "GPS On" : "GPS Off");

        // 1. Record Track Button
        if (BtnTitleRecordTrack != null)
        {
            if (isRecording)
            {
                BtnTitleRecordTrack.Content = "⏹️";
                BtnTitleRecordTrack.Background = new SolidColorBrush(Color.FromArgb(255, 220, 38, 38)); // Red-600
                BtnTitleRecordTrack.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 239, 68, 68));
                BtnTitleRecordTrack.Foreground = new SolidColorBrush(Colors.White);
                ToolTipService.SetToolTip(BtnTitleRecordTrack, "Recording Track (Active) - Long press to stop recording");
            }
            else
            {
                if (IconTitleRecordTrack != null)
                {
                    IconTitleRecordTrack.IsRecording = false;
                    BtnTitleRecordTrack.Content = IconTitleRecordTrack;
                }
                BtnTitleRecordTrack.Background = tm.ButtonInactiveBackground;
                BtnTitleRecordTrack.BorderBrush = tm.ButtonInactiveBorder;
                BtnTitleRecordTrack.Foreground = tm.TextPrimary;
                ToolTipService.SetToolTip(BtnTitleRecordTrack, "Record Track - Long press to start recording");
            }
        }

        // 2. GPS On/Off Button
        if (BtnTitleToggleGps != null)
        {
            if (IconTitleGps != null)
            {
                BtnTitleToggleGps.Content = IconTitleGps;
            }

            if (isRecording)
            {
                // GPS must stay ON while track recording is active
                BtnTitleToggleGps.Background = tm.ButtonActiveBackground;
                BtnTitleToggleGps.BorderBrush = tm.ButtonActiveBorder;
                BtnTitleToggleGps.Foreground = tm.ButtonActiveForeground;
                BtnTitleToggleGps.Opacity = 0.95;
                ToolTipService.SetToolTip(BtnTitleToggleGps, $"🛰️ {gpsStatus} (Track recording active - GPS cannot be turned off)");
            }
            else if (isGps)
            {
                BtnTitleToggleGps.Background = tm.ButtonActiveBackground;
                BtnTitleToggleGps.BorderBrush = tm.ButtonActiveBorder;
                BtnTitleToggleGps.Foreground = tm.ButtonActiveForeground;
                BtnTitleToggleGps.Opacity = 1.0;
                ToolTipService.SetToolTip(BtnTitleToggleGps, $"🛰️ {gpsStatus} - Long press to turn off GPS");
            }
            else
            {
                BtnTitleToggleGps.Background = tm.ButtonInactiveBackground;
                BtnTitleToggleGps.BorderBrush = tm.ButtonInactiveBorder;
                BtnTitleToggleGps.Foreground = tm.ButtonInactiveForeground;
                BtnTitleToggleGps.Opacity = 0.65;
                ToolTipService.SetToolTip(BtnTitleToggleGps, "🛰️ GPS: OFF - Long press to turn on GPS");
            }
        }
    }

    #endregion

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

        if (PmTilesModalBackdrop != null) PmTilesModalBackdrop.Visibility = Visibility.Visible;
        if (PmTilesConfigModal != null)
        {
            PmTilesConfigModal.SetCompactMode(_isSmallScreen);
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

    private void UpdateMapTilerConfigDisplay()
    {
        bool isConfigured = MapTilerService.IsConfigured;
        if (TxtMapTilerApiKeyInput != null)
        {
            TxtMapTilerApiKeyInput.Text = MapTilerService.ApiKey ?? "";
        }
        if (TxtMapTilerConfigStatus != null)
        {
            if (isConfigured)
            {
                string key = MapTilerService.ApiKey ?? "";
                string masked = key.Length > 8 ? $"{key.Substring(0, 4)}...{key.Substring(key.Length - 4)}" : key;
                TxtMapTilerConfigStatus.Text = $"Active API key configured: {masked}";
                TxtMapTilerConfigStatus.Foreground = ThemeManager.Current.Accent;
            }
            else
            {
                TxtMapTilerConfigStatus.Text = "No API key configured. Sign up free at maptiler.com to enable weather overlays.";
                TxtMapTilerConfigStatus.Foreground = ThemeManager.Current.TextMuted;
            }
        }
        FullScreenMapControlView?.UpdateMapTilerState();
    }

    private void BtnSaveMapTilerApiKey_Click(object sender, RoutedEventArgs e)
    {
        string apiKey = TxtMapTilerApiKeyInput?.Text?.Trim() ?? "";
        MapTilerService.ApiKey = apiKey;
        UpdateMapTilerConfigDisplay();
        if (MapTilerService.IsConfigured && TxtMapTilerConfigStatus != null)
        {
            TxtMapTilerConfigStatus.Text = "Saved! MapTiler weather overlays enabled in map tools menu.";
            TxtMapTilerConfigStatus.Foreground = ThemeManager.Current.Accent;
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

        // Header Action Buttons
        UpdateTitleBarButtonsState();

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

        // 4. Options Full-Sized Page Card Styling
        SolidColorBrush cardBg = isDark ? new SolidColorBrush(Color.FromArgb(255, 30, 41, 59)) : new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        SolidColorBrush cardBorder = isDark ? new SolidColorBrush(Color.FromArgb(255, 51, 65, 85)) : new SolidColorBrush(Color.FromArgb(255, 226, 232, 240));

        void StyleOptionsCard(Border? card)
        {
            if (card == null) return;
            card.Background = cardBg;
            card.BorderBrush = cardBorder;
        }

        StyleOptionsCard(OptionsHeaderCard);
        StyleOptionsCard(CardOptionsUnits);
        StyleOptionsCard(CardOptionsOffline);
        StyleOptionsCard(CardOptionsTheme);
        StyleOptionsCard(CardOptionsGeoNames);
        StyleOptionsCard(CardOptionsMapTiler);
        StyleOptionsCard(CardOptionsUpdates);

        if (TxtOptionsPageTitle != null) TxtOptionsPageTitle.Foreground = tm.TextPrimary;
        if (TxtOptionsPageSubtitle != null) TxtOptionsPageSubtitle.Foreground = tm.TextSecondary;
        if (TxtSectionUnits != null) TxtSectionUnits.Foreground = tm.TextPrimary;
        if (TxtUnitsHelp != null) TxtUnitsHelp.Foreground = tm.TextSecondary;
        if (TxtSectionOffline != null) TxtSectionOffline.Foreground = tm.TextPrimary;
        if (TxtOfflineHelp != null) TxtOfflineHelp.Foreground = tm.TextSecondary;
        if (TxtSectionTheme != null) TxtSectionTheme.Foreground = tm.TextPrimary;
        if (TxtThemeHelp != null) TxtThemeHelp.Foreground = tm.TextSecondary;
        if (TxtSectionGeoNames != null) TxtSectionGeoNames.Foreground = tm.TextPrimary;
        if (TxtGeoNamesHelp != null) TxtGeoNamesHelp.Foreground = tm.TextSecondary;
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
        if (TxtSectionMapTiler != null) TxtSectionMapTiler.Foreground = tm.TextPrimary;
        if (TxtMapTilerHelp != null) TxtMapTilerHelp.Foreground = tm.TextSecondary;
        if (TxtMapTilerApiKeyInput != null)
        {
            TxtMapTilerApiKeyInput.Background = tm.ButtonInactiveBackground;
            TxtMapTilerApiKeyInput.Foreground = tm.TextPrimary;
            TxtMapTilerApiKeyInput.BorderBrush = tm.ButtonInactiveBorder;
        }
        if (BtnSaveMapTilerApiKey != null)
        {
            BtnSaveMapTilerApiKey.Background = tm.ButtonInactiveBackground;
            BtnSaveMapTilerApiKey.BorderBrush = tm.ButtonInactiveBorder;
            BtnSaveMapTilerApiKey.Foreground = tm.TextPrimary;
        }
        if (TxtSectionUpdates != null) TxtSectionUpdates.Foreground = tm.TextPrimary;
        if (TxtUpdatesHelp != null) TxtUpdatesHelp.Foreground = tm.TextSecondary;
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
        AboutPageView?.UpdateTheme(isDark);

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
        StyleTabBtn(BtnTabOptions, _currentTab == "Options");
        StyleTabBtn(BtnTabAbout, _currentTab == "About");
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
        StyleNavBtn(BtnNavPageOptions, _currentTab == "Options");
        StyleNavBtn(BtnNavPageAbout, _currentTab == "About");
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ScheduleHorizonGeneration(immediate: true);
    }

    private void BtnUpdates_Click(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.CheckForUpdatesAsync(silent: false);
    }
}
