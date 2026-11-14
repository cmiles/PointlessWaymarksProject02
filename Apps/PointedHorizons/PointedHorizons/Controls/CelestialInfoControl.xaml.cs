using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PointedHorizons.Models;
using PointedHorizons.Services;
using PointedHorizons.ViewModels;

namespace PointedHorizons.Controls;

public partial class CelestialInfoControl : UserControl
{
    public event EventHandler? CloseRequested;

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private double? _mapLat;
    private double? _mapLon;
    private double? _activeLat;
    private double? _activeLon;
    private bool _isGpsSelected;
    private bool _isCalculating;
    private bool _isCompact;
    private CelestialWindowReport? _lastReport;

    public CelestialInfoControl()
    {
        InitializeComponent();
        Loaded += CelestialInfoControl_Loaded;
        SizeChanged += CelestialInfoControl_SizeChanged;

        void AttachWheelTo(FrameworkElement? element)
        {
            if (element == null) return;
            element.MouseWheel += (s, e) =>
            {
                HandleExternalMouseWheel(e.Delta);
                e.Handled = true;
            };
        }

        AttachWheelTo(this);
        AttachWheelTo(DialogRoot);
        AttachWheelTo(CardBorder);
        AttachWheelTo(CardInnerGrid);
        AttachWheelTo(ContentScrollViewer);
        AttachWheelTo(ContentMainStack);
        AttachWheelTo(InitialChoicePanel);
        AttachWheelTo(ResultsPanel);
        AttachWheelTo(ColumnsContainerGrid);
        AttachWheelTo(ColLeftStack);
        AttachWheelTo(ColRightStack);
        AttachWheelTo(EventsCardsContainer);
        AttachWheelTo(SummaryCardBorder);
        AttachWheelTo(BtnBigGpsCard);
        AttachWheelTo(BtnBigMapCard);

        Border?[] allCards = [
            CardSunrise, CardSunset, CardMoonrise, CardMoonset,
            CardDaylightNoon, CardMorningBlue, CardMorningGolden,
            CardEveningGolden, CardEveningBlue, CardMilkyWay,
            CardTwilight, CardSeason
        ];
        foreach (var c in allCards)
        {
            AttachWheelTo(c);
        }
    }

    public void HandleExternalMouseWheel(double delta)
    {
        if (ContentScrollViewer != null)
        {
            ContentScrollViewer.ScrollToVerticalOffset(ContentScrollViewer.VerticalOffset - (delta / 2.0));
        }
    }

    private void CelestialInfoControl_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateTheme(ThemeManager.Current.IsDark);
    }

    private void CelestialInfoControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width > 0)
        {
            SetCompactMode(e.NewSize.Width < 780);
        }
    }

    /// <summary>
    /// Configures this control to behave as a dedicated full-page tab.
    /// </summary>
    public void PrepareAsPage()
    {
        if (BtnCloseDialog != null) BtnCloseDialog.Visibility = Visibility.Collapsed;
        if (BtnCancelBottom != null) BtnCancelBottom.Visibility = Visibility.Collapsed;
        if (CardBorder != null)
        {
            CardBorder.MaxWidth = double.PositiveInfinity;
            CardBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
            CardBorder.VerticalAlignment = VerticalAlignment.Stretch;
            CardBorder.Margin = new Thickness(10, 8, 10, 8);
        }
    }

    /// <summary>
    /// Adapts the layout between full-featured multi-column desktop and streamlined single-column mobile.
    /// </summary>
    public void SetCompactMode(bool isCompact)
    {
        _isCompact = isCompact;
        if (ColumnsContainerGrid == null || ColLeftDef == null || ColRightDef == null || ColLeftStack == null || ColRightStack == null) return;

        // Switch Twilight Trail Light title
        if (TxtTrailLightTitle != null)
        {
            TxtTrailLightTitle.Text = isCompact ? "Trail Light: " : "Trail Light (No Headlamp): ";
        }

        // Switch Twilight Progression Blocks
        if (GridMorningProgressionDesktop != null) GridMorningProgressionDesktop.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (PanelMorningProgressionMobile != null) PanelMorningProgressionMobile.Visibility = isCompact ? Visibility.Visible : Visibility.Collapsed;
        if (GridEveningProgressionDesktop != null) GridEveningProgressionDesktop.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (PanelEveningProgressionMobile != null) PanelEveningProgressionMobile.Visibility = isCompact ? Visibility.Visible : Visibility.Collapsed;

        // Switch Season Badge (desktop pill badge vs mobile inline countdown on 2nd line)
        if (BadgeSeason != null) BadgeSeason.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (TxtSeasonBadgeInline != null) TxtSeasonBadgeInline.Visibility = isCompact ? Visibility.Visible : Visibility.Collapsed;

        // Switch Desktop vs Mobile panels in timeline cards
        if (GridSunriseDesktop != null) GridSunriseDesktop.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (PanelSunriseStandardDesktop != null) PanelSunriseStandardDesktop.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (PanelSunriseMobile != null) PanelSunriseMobile.Visibility = isCompact ? Visibility.Visible : Visibility.Collapsed;

        if (GridSunsetDesktop != null) GridSunsetDesktop.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (PanelSunsetStandardDesktop != null) PanelSunsetStandardDesktop.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (PanelSunsetMobile != null) PanelSunsetMobile.Visibility = isCompact ? Visibility.Visible : Visibility.Collapsed;

        if (GridMoonriseDesktop != null) GridMoonriseDesktop.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (PanelMoonriseStandardDesktop != null) PanelMoonriseStandardDesktop.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (PanelMoonriseMobile != null) PanelMoonriseMobile.Visibility = isCompact ? Visibility.Visible : Visibility.Collapsed;

        if (GridMoonsetDesktop != null) GridMoonsetDesktop.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (PanelMoonsetStandardDesktop != null) PanelMoonsetStandardDesktop.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (PanelMoonsetMobile != null) PanelMoonsetMobile.Visibility = isCompact ? Visibility.Visible : Visibility.Collapsed;

        if (GridDaylightDesktop != null) GridDaylightDesktop.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (DaylightCompareBox != null) DaylightCompareBox.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (PanelDaylightMobile != null) PanelDaylightMobile.Visibility = isCompact ? Visibility.Visible : Visibility.Collapsed;

        if (GridMorningBlueDesktop != null) GridMorningBlueDesktop.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (TxtMorningBlueDesc != null) TxtMorningBlueDesc.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (PanelMorningBlueMobile != null) PanelMorningBlueMobile.Visibility = isCompact ? Visibility.Visible : Visibility.Collapsed;

        if (GridMorningGoldenDesktop != null) GridMorningGoldenDesktop.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (TxtMorningGoldenDirect != null) TxtMorningGoldenDirect.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (PanelMorningGoldenMobile != null) PanelMorningGoldenMobile.Visibility = isCompact ? Visibility.Visible : Visibility.Collapsed;

        if (GridEveningGoldenDesktop != null) GridEveningGoldenDesktop.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (TxtEveningGoldenDirect != null) TxtEveningGoldenDirect.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (PanelEveningGoldenMobile != null) PanelEveningGoldenMobile.Visibility = isCompact ? Visibility.Visible : Visibility.Collapsed;

        if (GridEveningBlueDesktop != null) GridEveningBlueDesktop.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (TxtEveningBlueDesc != null) TxtEveningBlueDesc.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (PanelEveningBlueMobile != null) PanelEveningBlueMobile.Visibility = isCompact ? Visibility.Visible : Visibility.Collapsed;

        if (TxtMilkyWayWindow != null) TxtMilkyWayWindow.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (TxtMilkyWayStatus != null) TxtMilkyWayStatus.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (MilkyWayDetailsBox != null) MilkyWayDetailsBox.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        if (PanelMilkyWayMobile != null) PanelMilkyWayMobile.Visibility = isCompact ? Visibility.Visible : Visibility.Collapsed;

        // Card Line 1 Titles & Icons: largest text on mobile (FontSize 15.5 bold)
        double titleSize = isCompact ? 15.5 : 11.0;
        double iconSize = isCompact ? 15.5 : 13.0;
        double badgeSize = isCompact ? 15.5 : 12.5;

        TextBlock?[] titles = [
            TxtSunriseTitle, TxtSunsetTitle, TxtMoonriseTitle, TxtMoonsetTitle,
            TxtDaylightTitle, TxtMorningBlueTitle, TxtMorningGoldenTitle,
            TxtEveningGoldenTitle, TxtEveningBlueTitle, TxtMilkyWayTitle
        ];
        foreach (var t in titles)
        {
            if (t != null)
            {
                t.FontSize = titleSize;
                t.FontWeight = FontWeights.Bold;
            }
        }

        TextBlock?[] icons = [
            IcoSunrise, IcoSunset, IcoMoonrise, IcoMoonset,
            IcoDaylight, IcoMorningBlue, IcoMorningGolden,
            IcoEveningGolden, IcoEveningBlue, IcoMilkyWay
        ];
        foreach (var ico in icons)
        {
            if (ico != null) ico.FontSize = iconSize;
        }

        TextBlock?[] badges = [
            TxtSunriseBadge, TxtSunsetBadge, TxtMoonriseBadge, TxtMoonsetBadge,
            TxtSolarNoonBadge, TxtMorningBlueBadge, TxtMorningGoldenBadge,
            TxtEveningGoldenBadge, TxtEveningBlueBadge, TxtMilkyWayBadge
        ];
        foreach (var b in badges)
        {
            if (b != null)
            {
                b.FontSize = badgeSize;
                b.FontWeight = FontWeights.Bold;
            }
        }

        if (isCompact)
        {
            // Position all 10 card badges on Row 1 (Line 2) on mobile
            Border?[] allBadges = [
                BadgeSunrise, BadgeSunset, BadgeMoonrise, BadgeMoonset,
                BadgeSolarNoon, BadgeMorningBlue, BadgeMorningGolden,
                BadgeEveningGolden, BadgeEveningBlue, BadgeMilkyWay
            ];
            foreach (var b in allBadges)
            {
                if (b != null)
                {
                    Grid.SetRow(b, 1);
                    Grid.SetColumn(b, 0);
                    Grid.SetColumnSpan(b, 2);
                    b.HorizontalAlignment = HorizontalAlignment.Left;
                    b.Margin = new Thickness(9, 2, 0, 4);
                    b.Background = Brushes.Transparent;
                    b.BorderBrush = Brushes.Transparent;
                    b.BorderThickness = new Thickness(0);
                    b.Padding = new Thickness(0);
                }
            }

            // Top buttons: Segmented GPS / Map buttons take 50% width each with wrap, refresh button on 2nd row full width
            if (ColModeRefresh != null) ColModeRefresh.Width = new GridLength(0);
            if (BtnOptionGps != null)
            {
                Grid.SetRow(BtnOptionGps, 0);
                Grid.SetColumn(BtnOptionGps, 0);
                Grid.SetColumnSpan(BtnOptionGps, 1);
                BtnOptionGps.Margin = new Thickness(0, 0, 3, 0);
                BtnOptionGps.MinHeight = 44;
            }
            if (BtnOptionMap != null)
            {
                Grid.SetRow(BtnOptionMap, 0);
                Grid.SetColumn(BtnOptionMap, 1);
                Grid.SetColumnSpan(BtnOptionMap, 2);
                BtnOptionMap.Margin = new Thickness(3, 0, 0, 0);
                BtnOptionMap.MinHeight = 44;
            }
            if (BtnTopRefresh != null)
            {
                Grid.SetRow(BtnTopRefresh, 1);
                Grid.SetColumn(BtnTopRefresh, 0);
                Grid.SetColumnSpan(BtnTopRefresh, 3);
                BtnTopRefresh.Margin = new Thickness(0, 6, 0, 0);
                BtnTopRefresh.HorizontalAlignment = HorizontalAlignment.Stretch;
                BtnTopRefresh.MinHeight = 42;
            }
            if (TxtTopRefresh != null) TxtTopRefresh.Text = "↺ Recalculate / Refresh";

            // Daylight comparison & Trail light
            if (PanelDaylightCompareRow != null) PanelDaylightCompareRow.Orientation = Orientation.Vertical;
            if (PanelTrailLightHeader != null) PanelTrailLightHeader.Orientation = Orientation.Vertical;

            // Single stacked column on small screen / mobile
            ColLeftDef.Width = new GridLength(1, GridUnitType.Star);
            ColRightDef.Width = new GridLength(0);

            Grid.SetColumn(ColRightStack, 0);
            Grid.SetRow(ColRightStack, 1);
            Grid.SetColumnSpan(ColLeftStack, 2);
            Grid.SetColumnSpan(ColRightStack, 2);

            ColLeftStack.Margin = new Thickness(0, 0, 0, 8);
            ColRightStack.Margin = new Thickness(0, 0, 0, 0);

            if (PanelDesktopTwilightNotes != null) PanelDesktopTwilightNotes.Visibility = Visibility.Collapsed;
            if (TxtDesktopSeasonDesc != null) TxtDesktopSeasonDesc.Visibility = Visibility.Collapsed;
            if (TxtDesktopTrailLightNote != null) TxtDesktopTrailLightNote.Visibility = Visibility.Collapsed;
        }
        else
        {
            // Position all 10 card badges on Row 0, Column 1 on desktop
            Border?[] allBadges = [
                BadgeSunrise, BadgeSunset, BadgeMoonrise, BadgeMoonset,
                BadgeSolarNoon, BadgeMorningBlue, BadgeMorningGolden,
                BadgeEveningGolden, BadgeEveningBlue, BadgeMilkyWay
            ];
            foreach (var b in allBadges)
            {
                if (b != null)
                {
                    Grid.SetRow(b, 0);
                    Grid.SetColumn(b, 1);
                    Grid.SetColumnSpan(b, 1);
                    b.HorizontalAlignment = HorizontalAlignment.Right;
                    b.Margin = new Thickness(0, 0, 0, 0);
                    b.BorderThickness = new Thickness(1.5);
                    b.Padding = new Thickness(10, 4, 10, 4);
                    b.CornerRadius = new CornerRadius(8);
                }
            }

            // Top buttons: Segmented GPS / Map / Refresh on single desktop row
            if (ColModeRefresh != null) ColModeRefresh.Width = GridLength.Auto;
            if (BtnOptionGps != null)
            {
                Grid.SetRow(BtnOptionGps, 0);
                Grid.SetColumn(BtnOptionGps, 0);
                Grid.SetColumnSpan(BtnOptionGps, 1);
                BtnOptionGps.Margin = new Thickness(0, 0, 3, 0);
                BtnOptionGps.MinHeight = 0;
            }
            if (BtnOptionMap != null)
            {
                Grid.SetRow(BtnOptionMap, 0);
                Grid.SetColumn(BtnOptionMap, 1);
                Grid.SetColumnSpan(BtnOptionMap, 1);
                BtnOptionMap.Margin = new Thickness(3, 0, 3, 0);
                BtnOptionMap.MinHeight = 0;
            }
            if (BtnTopRefresh != null)
            {
                Grid.SetRow(BtnTopRefresh, 0);
                Grid.SetColumn(BtnTopRefresh, 2);
                Grid.SetColumnSpan(BtnTopRefresh, 1);
                BtnTopRefresh.Margin = new Thickness(3, 0, 0, 0);
                BtnTopRefresh.HorizontalAlignment = HorizontalAlignment.Stretch;
                BtnTopRefresh.MinHeight = 0;
            }
            if (TxtTopRefresh != null) TxtTopRefresh.Text = "↺ Refresh";

            // Daylight comparison & Trail light
            if (PanelDaylightCompareRow != null) PanelDaylightCompareRow.Orientation = Orientation.Horizontal;
            if (PanelTrailLightHeader != null) PanelTrailLightHeader.Orientation = Orientation.Horizontal;

            // Two side-by-side columns on desktop
            ColLeftDef.Width = new GridLength(1, GridUnitType.Star);
            ColRightDef.Width = new GridLength(1, GridUnitType.Star);

            Grid.SetColumn(ColRightStack, 1);
            Grid.SetRow(ColRightStack, 0);
            Grid.SetColumnSpan(ColLeftStack, 1);
            Grid.SetColumnSpan(ColRightStack, 1);

            ColLeftStack.Margin = new Thickness(0, 0, 6, 0);
            ColRightStack.Margin = new Thickness(6, 0, 0, 0);

            if (PanelDesktopTwilightNotes != null) PanelDesktopTwilightNotes.Visibility = Visibility.Visible;
            if (TxtDesktopSeasonDesc != null) TxtDesktopSeasonDesc.Visibility = Visibility.Visible;
            if (TxtDesktopTrailLightNote != null) TxtDesktopTrailLightNote.Visibility = Visibility.Visible;
        }

        if (_lastReport != null)
        {
            DisplayResults(_lastReport);
        }
        else
        {
            UpdateTheme(ThemeManager.Current.IsDark);
        }
    }

    /// <summary>
    /// Opens the dialog with the provided map location coordinates and presents the options.
    /// </summary>
    public void Open(double mapLat, double mapLon)
    {
        _mapLat = mapLat;
        _mapLon = mapLon;
        _activeLat = null;
        _activeLon = null;
        _lastReport = null;

        // Reset to initial choice view - eliminate top mode buttons in first pop up
        InitialChoicePanel.Visibility = Visibility.Visible;
        PanelModeSelector.Visibility = Visibility.Collapsed;
        LoadingPanel.Visibility = Visibility.Collapsed;
        ResultsPanel.Visibility = Visibility.Collapsed;
        BtnRefresh.Visibility = Visibility.Collapsed;

        if (TxtBigMapSub != null)
        {
            TxtBigMapSub.Text = $"Calculates for current map view ({mapLat:F4}°, {mapLon:F4}°).";
        }

        ResetSegmentedButtons();
        UpdateTheme(ThemeManager.Current.IsDark);
    }

    private void ResetSegmentedButtons()
    {
        var tm = ThemeManager.Current;
        if (BtnOptionGps != null)
        {
            BtnOptionGps.Background = tm.ButtonInactiveBackground;
            BtnOptionGps.BorderBrush = tm.ButtonInactiveBorder;
            BtnOptionGps.Foreground = tm.TextPrimary;
        }
        if (TxtOptionGps != null) TxtOptionGps.Foreground = tm.TextPrimary;

        if (BtnOptionMap != null)
        {
            BtnOptionMap.Background = tm.ButtonInactiveBackground;
            BtnOptionMap.BorderBrush = tm.ButtonInactiveBorder;
            BtnOptionMap.Foreground = tm.TextPrimary;
        }
        if (TxtOptionMap != null) TxtOptionMap.Foreground = tm.TextPrimary;
    }

    private void SetActiveButton(bool isGps)
    {
        var tm = ThemeManager.Current;
        if (BtnOptionGps != null)
        {
            BtnOptionGps.Background = isGps ? tm.ButtonActiveBackground : tm.ButtonInactiveBackground;
            BtnOptionGps.BorderBrush = isGps ? tm.ButtonActiveBorder : tm.ButtonInactiveBorder;
            BtnOptionGps.Foreground = isGps ? tm.ButtonActiveForeground : tm.TextPrimary;
        }
        if (TxtOptionGps != null) TxtOptionGps.Foreground = isGps ? tm.ButtonActiveForeground : tm.TextPrimary;

        if (BtnOptionMap != null)
        {
            BtnOptionMap.Background = !isGps ? tm.ButtonActiveBackground : tm.ButtonInactiveBackground;
            BtnOptionMap.BorderBrush = !isGps ? tm.ButtonActiveBorder : tm.ButtonInactiveBorder;
            BtnOptionMap.Foreground = !isGps ? tm.ButtonActiveForeground : tm.TextPrimary;
        }
        if (TxtOptionMap != null) TxtOptionMap.Foreground = !isGps ? tm.ButtonActiveForeground : tm.TextPrimary;
    }

    private async void BtnOptionGps_Click(object sender, RoutedEventArgs e)
    {
        if (_isCalculating) return;
        _isGpsSelected = true;
        SetActiveButton(true);

        InitialChoicePanel.Visibility = Visibility.Collapsed;
        ResultsPanel.Visibility = Visibility.Collapsed;
        LoadingPanel.Visibility = Visibility.Visible;
        BtnRefresh.Visibility = Visibility.Collapsed;

        TxtLoadingStatus.Text = "Acquiring satellite GPS fix...";
        TxtLoadingSub.Text = "Querying device hardware location sensors...";

        try
        {
            var pos = await GeolocationService.GetCurrentPositionAsync(timeoutSeconds: 15);
            if (pos != null)
            {
                _activeLat = pos.Latitude;
                _activeLon = pos.Longitude;
                await RunCalculationAsync(_activeLat.Value, _activeLon.Value, isGps: true);
            }
            else
            {
                TxtLoadingStatus.Text = "GPS fix timed out or unavailable";
                TxtLoadingSub.Text = "Please ensure location permissions are granted, or select Map Location.";
            }
        }
        catch (Exception ex)
        {
            TxtLoadingStatus.Text = "GPS Error";
            TxtLoadingSub.Text = ex.Message;
        }
    }

    private async void BtnOptionMap_Click(object sender, RoutedEventArgs e)
    {
        if (_isCalculating) return;
        _isGpsSelected = false;
        SetActiveButton(false);

        InitialChoicePanel.Visibility = Visibility.Collapsed;
        ResultsPanel.Visibility = Visibility.Collapsed;
        LoadingPanel.Visibility = Visibility.Visible;
        BtnRefresh.Visibility = Visibility.Collapsed;

        double lat;
        double lon;

        if (FullScreenMapControl.Current != null)
        {
            var target = FullScreenMapControl.Current.GetCurrentTargetLocation();
            lat = target.Latitude;
            lon = target.Longitude;
        }
        else
        {
            lat = _mapLat ?? ViewModel?.Latitude ?? 32.2226;
            lon = _mapLon ?? ViewModel?.Longitude ?? -110.9747;
        }

        _activeLat = lat;
        _activeLon = lon;

        await RunCalculationAsync(lat, lon, isGps: false);
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (_isCalculating) return;
        if (_isGpsSelected)
        {
            BtnOptionGps_Click(sender, e);
        }
        else
        {
            BtnOptionMap_Click(sender, e);
        }
    }

    private async Task RunCalculationAsync(double lat, double lon, bool isGps)
    {
        _isCalculating = true;
        InitialChoicePanel.Visibility = Visibility.Collapsed;
        PanelModeSelector.Visibility = Visibility.Collapsed;
        LoadingPanel.Visibility = Visibility.Visible;
        ResultsPanel.Visibility = Visibility.Collapsed;
        BtnRefresh.Visibility = Visibility.Collapsed;

        TxtLoadingStatus.Text = isGps ? "GPS fix achieved!" : "Processing map location...";
        TxtLoadingSub.Text = "Querying DEM elevation & calculating 360° terrain silhouette...";

        try
        {
            // 1. Asynchronously retrieve elevation in meters
            double? elev = await DemService.GetElevationAsync(lat, lon);
            double elevM = elev ?? 0.0;

            // 2. Obtain or trace 360° horizon profile
            List<HorizonProfilePoint>? profile = null;
            var vm = ViewModel;
            if (vm?.CurrentSceneData?.ProfilePoints != null && vm.CurrentSceneData.ProfilePoints.Count > 0 &&
                Math.Abs(lat - vm.Latitude) < 0.002 && Math.Abs(lon - vm.Longitude) < 0.002)
            {
                profile = vm.CurrentSceneData.ProfilePoints;
            }
            else
            {
                var scene = await Task.Run(() => HorizonRayTracer.TraceScene(lat, lon, elevM, 0, 360, maxDistanceKm: 120, rayCount: 720));
                profile = scene?.ProfilePoints;
            }

            // 3. Compute continuous window celestial data (±12 hours)
            var report = await Task.Run(() => CelestialService.CalculateWindowCelestialData(lat, lon, elevM, DateTime.Now, profile));
            _lastReport = report;

            // 4. Render results
            DisplayResults(report);
        }
        catch (Exception ex)
        {
            TxtLoadingStatus.Text = "Calculation error";
            TxtLoadingSub.Text = ex.Message;
        }
        finally
        {
            _isCalculating = false;
        }
    }

    private void DisplayResults(CelestialWindowReport report)
    {
        LoadingPanel.Visibility = Visibility.Collapsed;
        ResultsPanel.Visibility = Visibility.Visible;
        PanelModeSelector.Visibility = Visibility.Visible;
        BtnRefresh.Visibility = Visibility.Visible;

        // Summary details
        TxtSummaryCoords.Text = report.CoordinatesDisplay;
        TxtSummaryElevation.Text = report.ElevationDisplay;
        TxtSummaryWindow.Text = report.WindowDisplay;
        TxtSummaryMoonPhase.Text = $"Moon: {report.MoonPhaseName} ({Math.Round(report.MoonIllumination * 100)}% illuminated)";

        // 1. Sunrise
        BindEventCard(
            report.Sunrise,
            TxtSunriseTitle,
            TxtSunriseTopoTime,
            TxtSunriseTopoTimeMobile,
            TxtSunriseTopoAz,
            TxtSunriseTopoAzMobile,
            TxtSunriseTopoTag,
            TxtSunriseStandard,
            TxtSunriseStandardMobile,
            TxtSunriseShift,
            TxtSunriseShiftMobile,
            BadgeSunrise,
            TxtSunriseBadge);

        // 2. Sunset
        BindEventCard(
            report.Sunset,
            TxtSunsetTitle,
            TxtSunsetTopoTime,
            TxtSunsetTopoTimeMobile,
            TxtSunsetTopoAz,
            TxtSunsetTopoAzMobile,
            TxtSunsetTopoTag,
            TxtSunsetStandard,
            TxtSunsetStandardMobile,
            TxtSunsetShift,
            TxtSunsetShiftMobile,
            BadgeSunset,
            TxtSunsetBadge);

        // 3. Moonrise
        BindEventCard(
            report.Moonrise,
            TxtMoonriseTitle,
            TxtMoonriseTopoTime,
            TxtMoonriseTopoTimeMobile,
            TxtMoonriseTopoAz,
            TxtMoonriseTopoAzMobile,
            TxtMoonriseTopoTag,
            TxtMoonriseStandard,
            TxtMoonriseStandardMobile,
            TxtMoonriseShift,
            TxtMoonriseShiftMobile,
            BadgeMoonrise,
            TxtMoonriseBadge);

        // 4. Moonset
        BindEventCard(
            report.Moonset,
            TxtMoonsetTitle,
            TxtMoonsetTopoTime,
            TxtMoonsetTopoTimeMobile,
            TxtMoonsetTopoAz,
            TxtMoonsetTopoAzMobile,
            TxtMoonsetTopoTag,
            TxtMoonsetStandard,
            TxtMoonsetStandardMobile,
            TxtMoonsetShift,
            TxtMoonsetShiftMobile,
            BadgeMoonset,
            TxtMoonsetBadge);

        // 5. Daylight & Solar Noon Culmination Card
        if (report.SolarNoon != null)
        {
            if (TxtSolarNoonTime != null) TxtSolarNoonTime.Text = report.SolarNoon.NoonTimeDisplay;
            if (TxtSolarNoonTimeMobile != null) TxtSolarNoonTimeMobile.Text = report.SolarNoon.NoonTimeDisplay;
            if (TxtSolarNoonElevation != null) TxtSolarNoonElevation.Text = $"Max Elevation: {report.SolarNoon.MaxElevationDisplay}";
            if (TxtSolarNoonElevationMobile != null) TxtSolarNoonElevationMobile.Text = $"Max Elevation: {report.SolarNoon.MaxElevationDisplay}";
            string noonCountdown = report.SolarNoon.NoonCountdownDisplay ?? string.Empty;
            if (noonCountdown.StartsWith("⏳ ")) noonCountdown = noonCountdown.Substring(2).TrimStart();
            if (noonCountdown.StartsWith("in ", StringComparison.OrdinalIgnoreCase)) noonCountdown = "+ " + noonCountdown.Substring(3);
            if (TxtSolarNoonBadge != null) TxtSolarNoonBadge.Text = noonCountdown;

            if (BadgeSolarNoon != null && TxtSolarNoonBadge != null)
            {
                var tm = ThemeManager.Current;
                if (_isCompact)
                {
                    BadgeSolarNoon.Background = Brushes.Transparent;
                    BadgeSolarNoon.BorderBrush = Brushes.Transparent;
                    BadgeSolarNoon.BorderThickness = new Thickness(0);
                    BadgeSolarNoon.Padding = new Thickness(0);
                    BadgeSolarNoon.HorizontalAlignment = HorizontalAlignment.Left;
                    BadgeSolarNoon.Margin = new Thickness(9, 2, 0, 4);
                    Grid.SetRow(BadgeSolarNoon, 1);
                    Grid.SetColumn(BadgeSolarNoon, 0);
                    Grid.SetColumnSpan(BadgeSolarNoon, 2);
                    TxtSolarNoonBadge.Foreground = tm.TextPrimary;
                }
                else
                {
                    Grid.SetRow(BadgeSolarNoon, 0);
                    Grid.SetColumn(BadgeSolarNoon, 1);
                    Grid.SetColumnSpan(BadgeSolarNoon, 1);
                    BadgeSolarNoon.HorizontalAlignment = HorizontalAlignment.Right;
                    BadgeSolarNoon.Margin = new Thickness(0);
                    BadgeSolarNoon.BorderThickness = new Thickness(1.5);
                    BadgeSolarNoon.Padding = new Thickness(10, 4, 10, 4);
                    BadgeSolarNoon.CornerRadius = new CornerRadius(8);
                    if (report.SolarNoon.IsNoonActive)
                    {
                        BadgeSolarNoon.Background = tm.Accent;
                        TxtSolarNoonBadge.Foreground = Brushes.White;
                    }
                    else if (!report.SolarNoon.IsNoonPast)
                    {
                        BadgeSolarNoon.Background = tm.ButtonActiveBackground;
                        TxtSolarNoonBadge.Foreground = tm.ButtonActiveForeground;
                    }
                    else
                    {
                        BadgeSolarNoon.Background = tm.ButtonInactiveBackground;
                        TxtSolarNoonBadge.Foreground = tm.TextMuted;
                    }
                }
            }
        }

        // 6. Individual Photography Window Cards
        if (report.PhotographyHours != null)
        {
            BindPhotoCard(report.PhotographyHours.MorningBlue, CardMorningBlue, TxtMorningBlueTime, TxtMorningBlueTimeMobile, TxtMorningBlueSub, TxtMorningBlueSubMobile, null, null, TxtMorningBlueDesc, BadgeMorningBlue, TxtMorningBlueBadge);
            BindPhotoCard(report.PhotographyHours.MorningGolden, CardMorningGolden, TxtMorningGoldenTime, TxtMorningGoldenTimeMobile, TxtMorningGoldenSub, TxtMorningGoldenSubMobile, TxtMorningGoldenDirect, TxtMorningGoldenDirectMobile, null, BadgeMorningGolden, TxtMorningGoldenBadge);
            BindPhotoCard(report.PhotographyHours.EveningGolden, CardEveningGolden, TxtEveningGoldenTime, TxtEveningGoldenTimeMobile, TxtEveningGoldenSub, TxtEveningGoldenSubMobile, TxtEveningGoldenDirect, TxtEveningGoldenDirectMobile, null, BadgeEveningGolden, TxtEveningGoldenBadge);
            BindPhotoCard(report.PhotographyHours.EveningBlue, CardEveningBlue, TxtEveningBlueTime, TxtEveningBlueTimeMobile, TxtEveningBlueSub, TxtEveningBlueSubMobile, null, null, TxtEveningBlueDesc, BadgeEveningBlue, TxtEveningBlueBadge);
        }

        // 7. Milky Way & Galactic Center Card
        if (report.MilkyWay != null)
        {
            if (TxtMilkyWayStatus != null) TxtMilkyWayStatus.Text = report.MilkyWay.StatusText;
            if (TxtMilkyWayStatusMobile != null) TxtMilkyWayStatusMobile.Text = report.MilkyWay.StatusText;

            string mwCountdown = report.MilkyWay.CountdownDisplay ?? string.Empty;
            if (mwCountdown.StartsWith("⏳ ")) mwCountdown = mwCountdown.Substring(2).TrimStart();
            if (mwCountdown.StartsWith("in ", StringComparison.OrdinalIgnoreCase)) mwCountdown = "+ " + mwCountdown.Substring(3);

            string mwBadge = _isCompact
                ? (string.IsNullOrEmpty(mwCountdown) ? report.MilkyWay.QualityBadge : mwCountdown)
                : report.MilkyWay.QualityBadge;
            if (mwBadge.StartsWith("⏳ ")) mwBadge = mwBadge.Substring(2).TrimStart();
            if (mwBadge.StartsWith("in ", StringComparison.OrdinalIgnoreCase)) mwBadge = "+ " + mwBadge.Substring(3);
            if (TxtMilkyWayBadge != null) TxtMilkyWayBadge.Text = mwBadge;

            if (TxtMilkyWayWindow != null) TxtMilkyWayWindow.Text = report.MilkyWay.WindowDisplay;
            if (TxtMilkyWayWindowMobile != null) TxtMilkyWayWindowMobile.Text = report.MilkyWay.WindowDisplay;

            string peak = report.MilkyWay.PeakDisplay ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(peak) && !peak.StartsWith("🧭"))
                peak = $"🧭 {peak}";
            if (TxtMilkyWayPeak != null)
            {
                TxtMilkyWayPeak.Text = peak;
                TxtMilkyWayPeak.Visibility = string.IsNullOrWhiteSpace(peak) ? Visibility.Collapsed : Visibility.Visible;
            }
            if (TxtMilkyWayPeakMobile != null)
            {
                TxtMilkyWayPeakMobile.Text = peak;
                TxtMilkyWayPeakMobile.Visibility = string.IsNullOrWhiteSpace(peak) ? Visibility.Collapsed : Visibility.Visible;
            }

            string clr = report.MilkyWay.MountainClearanceDisplay ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(clr) && !clr.StartsWith("⛰️"))
                clr = $"⛰️ {clr}";
            if (TxtMilkyWayClearance != null)
            {
                TxtMilkyWayClearance.Text = clr;
                TxtMilkyWayClearance.Visibility = string.IsNullOrWhiteSpace(clr) ? Visibility.Collapsed : Visibility.Visible;
            }
            if (TxtMilkyWayClearanceMobile != null)
            {
                TxtMilkyWayClearanceMobile.Text = clr;
                TxtMilkyWayClearanceMobile.Visibility = string.IsNullOrWhiteSpace(clr) ? Visibility.Collapsed : Visibility.Visible;
            }

            string moon = report.MilkyWay.MoonInterferenceDisplay ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(moon) && !moon.StartsWith("🌙"))
                moon = $"🌙 {moon}";
            if (TxtMilkyWayMoon != null)
            {
                TxtMilkyWayMoon.Text = moon;
                TxtMilkyWayMoon.Visibility = string.IsNullOrWhiteSpace(moon) ? Visibility.Collapsed : Visibility.Visible;
            }
            if (TxtMilkyWayMoonMobile != null)
            {
                TxtMilkyWayMoonMobile.Text = moon;
                TxtMilkyWayMoonMobile.Visibility = string.IsNullOrWhiteSpace(moon) ? Visibility.Collapsed : Visibility.Visible;
            }

            if (MilkyWayDetailsBox != null)
            {
                bool hasDetails = (TxtMilkyWayPeak != null && TxtMilkyWayPeak.Visibility == Visibility.Visible) ||
                                  (TxtMilkyWayClearance != null && TxtMilkyWayClearance.Visibility == Visibility.Visible) ||
                                  (TxtMilkyWayMoon != null && TxtMilkyWayMoon.Visibility == Visibility.Visible);
                MilkyWayDetailsBox.Visibility = (_isCompact || !hasDetails) ? Visibility.Collapsed : Visibility.Visible;
            }

            if (BadgeMilkyWay != null && TxtMilkyWayBadge != null)
            {
                var tm = ThemeManager.Current;
                if (_isCompact)
                {
                    BadgeMilkyWay.Background = Brushes.Transparent;
                    BadgeMilkyWay.BorderBrush = Brushes.Transparent;
                    BadgeMilkyWay.BorderThickness = new Thickness(0);
                    BadgeMilkyWay.Padding = new Thickness(0);
                    BadgeMilkyWay.HorizontalAlignment = HorizontalAlignment.Left;
                    BadgeMilkyWay.Margin = new Thickness(9, 2, 0, 4);
                    Grid.SetRow(BadgeMilkyWay, 1);
                    Grid.SetColumn(BadgeMilkyWay, 0);
                    Grid.SetColumnSpan(BadgeMilkyWay, 2);
                    TxtMilkyWayBadge.Foreground = tm.TextPrimary;
                }
                else
                {
                    Grid.SetRow(BadgeMilkyWay, 0);
                    Grid.SetColumn(BadgeMilkyWay, 1);
                    Grid.SetColumnSpan(BadgeMilkyWay, 1);
                    BadgeMilkyWay.HorizontalAlignment = HorizontalAlignment.Right;
                    BadgeMilkyWay.Margin = new Thickness(0);
                    BadgeMilkyWay.BorderThickness = new Thickness(1.5);
                    BadgeMilkyWay.Padding = new Thickness(10, 4, 10, 4);
                    BadgeMilkyWay.CornerRadius = new CornerRadius(8);
                    if (report.MilkyWay.IsActive)
                    {
                        BadgeMilkyWay.Background = tm.Accent;
                        TxtMilkyWayBadge.Foreground = Brushes.White;
                    }
                    else if (report.MilkyWay.IsFuture && report.MilkyWay.IsQualityPositive)
                    {
                        BadgeMilkyWay.Background = tm.ButtonActiveBackground;
                        TxtMilkyWayBadge.Foreground = tm.ButtonActiveForeground;
                    }
                    else
                    {
                        BadgeMilkyWay.Background = tm.ButtonInactiveBackground;
                        TxtMilkyWayBadge.Foreground = tm.TextMuted;
                    }
                }
            }
        }

        // 8. Order all timeline cards by start/event time (chronological ascending)
        if (EventsCardsContainer != null)
        {
            var cardsToOrder = new List<(DateTime? EventTime, Border? Card)>
            {
                (report.Sunrise?.EffectiveTime, CardSunrise),
                (report.Sunset?.EffectiveTime, CardSunset),
                (report.Moonrise?.EffectiveTime, CardMoonrise),
                (report.Moonset?.EffectiveTime, CardMoonset),
                (report.SolarNoon?.SolarNoonTime, CardDaylightNoon),
                (report.PhotographyHours?.MorningBlue?.StartTime, CardMorningBlue),
                (report.PhotographyHours?.MorningGolden?.StartTime, CardMorningGolden),
                (report.PhotographyHours?.EveningGolden?.StartTime, CardEveningGolden),
                (report.PhotographyHours?.EveningBlue?.StartTime, CardEveningBlue),
                (report.MilkyWay?.WindowStartTime, CardMilkyWay)
            }
            .Where(item => item.Card != null && item.Card.Visibility == Visibility.Visible)
            .OrderBy(item => item.EventTime ?? DateTime.MaxValue)
            .ToList();

            EventsCardsContainer.Children.Clear();
            for (int i = 0; i < cardsToOrder.Count; i++)
            {
                var card = cardsToOrder[i].Card;
                if (card == null) continue;
                card.VerticalAlignment = VerticalAlignment.Top;
                card.Margin = (i == cardsToOrder.Count - 1)
                    ? new Thickness(0, 0, 0, 4)
                    : new Thickness(0, 0, 0, 8);
                EventsCardsContainer.Children.Add(card);
            }
        }

        // 9. Season & Solstice / Equinox Card
        if (report.SeasonMarker != null)
        {
            if (TxtSeasonTitle != null) TxtSeasonTitle.Text = $"{report.SeasonMarker.CurrentSeason}: {report.SeasonMarker.NextMarkerName}";
            if (TxtSeasonBadge != null) TxtSeasonBadge.Text = report.SeasonMarker.CountdownDisplay;
            if (TxtSeasonBadgeInline != null) TxtSeasonBadgeInline.Text = $"  •  {report.SeasonMarker.CountdownDisplay}";
            if (TxtSeasonDate != null) TxtSeasonDate.Text = report.SeasonMarker.NextMarkerDateDisplay;
            if (TxtSeasonTrend != null) TxtSeasonTrend.Text = report.SeasonMarker.TrendDescription;
            if (TxtDesktopSeasonDesc != null) TxtDesktopSeasonDesc.Text = report.SeasonMarker.Description;
        }

        // 10. Twilight Phases & Trail Light
        if (report.Twilight != null)
        {
            if (TxtTrailLightWindow != null) TxtTrailLightWindow.Text = $"{report.Twilight.TrailLightWindowDisplay} ({report.Twilight.TrailLightDurationDisplay})";
            if (TxtDesktopTrailLightNote != null) TxtDesktopTrailLightNote.Text = report.Twilight.TrailLightNote;

            string astroDawn = report.Twilight.AstroDawn?.TimeDisplay ?? "--";
            string nauticalDawn = report.Twilight.NauticalDawn?.TimeDisplay ?? "--";
            string civilDawn = report.Twilight.CivilDawn?.TimeDisplay ?? "--";
            string mountainSunrise = report.Twilight.MountainSunrise?.TimeDisplay ?? "--";

            if (TxtAstroDawnTime != null) TxtAstroDawnTime.Text = astroDawn;
            if (TxtAstroDawnTimeMobile != null) TxtAstroDawnTimeMobile.Text = astroDawn;
            if (TxtNauticalDawnTime != null) TxtNauticalDawnTime.Text = nauticalDawn;
            if (TxtNauticalDawnTimeMobile != null) TxtNauticalDawnTimeMobile.Text = nauticalDawn;
            if (TxtCivilDawnTime != null) TxtCivilDawnTime.Text = civilDawn;
            if (TxtCivilDawnTimeMobile != null) TxtCivilDawnTimeMobile.Text = civilDawn;
            if (TxtTopoSunriseMiniTime != null) TxtTopoSunriseMiniTime.Text = mountainSunrise;
            if (TxtTopoSunriseMiniTimeMobile != null) TxtTopoSunriseMiniTimeMobile.Text = mountainSunrise;

            string mountainSunset = report.Twilight.MountainSunset?.TimeDisplay ?? "--";
            string civilDusk = report.Twilight.CivilDusk?.TimeDisplay ?? "--";
            string nauticalDusk = report.Twilight.NauticalDusk?.TimeDisplay ?? "--";
            string astroDusk = report.Twilight.AstroDusk?.TimeDisplay ?? "--";

            if (TxtTopoSunsetMiniTime != null) TxtTopoSunsetMiniTime.Text = mountainSunset;
            if (TxtTopoSunsetMiniTimeMobile != null) TxtTopoSunsetMiniTimeMobile.Text = mountainSunset;
            if (TxtCivilDuskTime != null) TxtCivilDuskTime.Text = civilDusk;
            if (TxtCivilDuskTimeMobile != null) TxtCivilDuskTimeMobile.Text = civilDusk;
            if (TxtNauticalDuskTime != null) TxtNauticalDuskTime.Text = nauticalDusk;
            if (TxtNauticalDuskTimeMobile != null) TxtNauticalDuskTimeMobile.Text = nauticalDusk;
            if (TxtAstroDuskTime != null) TxtAstroDuskTime.Text = astroDusk;
            if (TxtAstroDuskTimeMobile != null) TxtAstroDuskTimeMobile.Text = astroDusk;

            if (TxtTwilightNoteSunrise != null) TxtTwilightNoteSunrise.Text = $"• Mountain Sunrise: {report.Twilight.MountainSunrise?.Note ?? "--"}";
            if (TxtTwilightNoteSunset != null) TxtTwilightNoteSunset.Text = $"• Mountain Sunset: {report.Twilight.MountainSunset?.Note ?? "--"}";
        }

        UpdateTheme(ThemeManager.Current.IsDark);
    }

    private void BindEventCard(
        WindowCelestialEvent? ev,
        TextBlock titleText,
        TextBlock topoTimeText,
        TextBlock? topoTimeTextMobile,
        TextBlock topoAzText,
        TextBlock? topoAzTextMobile,
        TextBlock topoTagText,
        TextBlock standardText,
        TextBlock? standardTextMobile,
        TextBlock shiftText,
        TextBlock? shiftTextMobile,
        Border badgeBorder,
        TextBlock badgeText)
    {
        var tm = ThemeManager.Current;

        if (ev == null || !ev.HasEvent)
        {
            topoTimeText.Text = "None found";
            if (topoTimeTextMobile != null) topoTimeTextMobile.Text = "None found";
            topoAzText.Text = "--";
            if (topoAzTextMobile != null) topoAzTextMobile.Text = "--";
            topoTagText.Visibility = Visibility.Collapsed;
            standardText.Text = "Standard: None";
            if (standardTextMobile != null) standardTextMobile.Text = "Standard: None";
            shiftText.Text = "No crossing";
            if (shiftTextMobile != null) shiftTextMobile.Text = "No crossing";
            badgeBorder.Visibility = Visibility.Collapsed;
            return;
        }

        topoTagText.Visibility = Visibility.Visible;

        // Topo displays
        topoTimeText.Text = ev.TopoTimeDisplay;
        if (topoTimeTextMobile != null) topoTimeTextMobile.Text = ev.TopoTimeDisplay;

        string azDisplay = $"{ev.TopoAzimuthDisplay}  •  {ev.TopoElevationDisplay}";
        topoAzText.Text = azDisplay;
        if (topoAzTextMobile != null) topoAzTextMobile.Text = azDisplay;

        // Standard comparison
        string stdDisplay = $"Standard: {ev.AstroTimeDisplay}";
        standardText.Text = stdDisplay;
        if (standardTextMobile != null) standardTextMobile.Text = stdDisplay;

        shiftText.Text = ev.TopoShiftDisplay;
        if (shiftTextMobile != null) shiftTextMobile.Text = ev.TopoShiftDisplay;

        // Countdown / Past Badge
        badgeBorder.Visibility = Visibility.Visible;
        string countdown = ev.TimeUntilDisplay ?? string.Empty;
        if (countdown.StartsWith("⏳ ")) countdown = countdown.Substring(2).TrimStart();
        if (countdown.StartsWith("in ", StringComparison.OrdinalIgnoreCase)) countdown = "+ " + countdown.Substring(3);

        if (_isCompact)
        {
            badgeText.Text = countdown;
            badgeBorder.Background = Brushes.Transparent;
            badgeBorder.BorderBrush = Brushes.Transparent;
            badgeBorder.BorderThickness = new Thickness(0);
            badgeBorder.Padding = new Thickness(0);
            badgeBorder.HorizontalAlignment = HorizontalAlignment.Left;
            badgeBorder.Margin = new Thickness(9, 2, 0, 4);
            Grid.SetRow(badgeBorder, 1);
            Grid.SetColumn(badgeBorder, 0);
            Grid.SetColumnSpan(badgeBorder, 2);
            badgeText.Foreground = tm.TextPrimary;
        }
        else
        {
            Grid.SetRow(badgeBorder, 0);
            Grid.SetColumn(badgeBorder, 1);
            Grid.SetColumnSpan(badgeBorder, 1);
            badgeBorder.HorizontalAlignment = HorizontalAlignment.Right;
            badgeBorder.Margin = new Thickness(0);
            badgeBorder.BorderThickness = new Thickness(1.5);
            badgeBorder.Padding = new Thickness(10, 4, 10, 4);
            badgeBorder.CornerRadius = new CornerRadius(8);
            if (ev.IsFuture)
            {
                badgeText.Text = countdown;
                badgeBorder.Background = tm.ButtonActiveBackground;
                badgeBorder.BorderBrush = tm.Accent;
                badgeText.Foreground = tm.ButtonActiveForeground;
            }
            else
            {
                badgeText.Text = $"✓ {countdown}";
                badgeBorder.Background = tm.ButtonInactiveBackground;
                badgeBorder.BorderBrush = tm.ButtonInactiveBorder;
                badgeText.Foreground = tm.TextMuted;
            }
        }
    }

    private void BindPhotoCard(
        PhotographyWindowItem? item,
        Border card,
        TextBlock timeText,
        TextBlock? timeTextMobile,
        TextBlock subText,
        TextBlock? subTextMobile,
        TextBlock? directSunText,
        TextBlock? directSunTextMobile,
        TextBlock? descText,
        Border badgeBorder,
        TextBlock badgeText)
    {
        var tm = ThemeManager.Current;

        if (item == null || !item.HasWindow)
        {
            card.Visibility = Visibility.Collapsed;
            return;
        }

        card.Visibility = Visibility.Visible;
        timeText.Text = item.TimeWindowDisplay;
        if (timeTextMobile != null) timeTextMobile.Text = item.TimeWindowDisplay;

        subText.Text = item.SubTitle;
        if (subTextMobile != null) subTextMobile.Text = item.SubTitle;

        if (directSunText != null) directSunText.Text = item.DirectSunDisplay;
        if (directSunTextMobile != null) directSunTextMobile.Text = item.DirectSunDisplay;

        if (descText != null) descText.Text = item.Description;

        string countdown = item.CountdownDisplay ?? string.Empty;
        if (countdown.StartsWith("⏳ "))
        {
            countdown = countdown.Substring(2).TrimStart();
        }
        if (countdown.StartsWith("in ", StringComparison.OrdinalIgnoreCase))
        {
            countdown = "+ " + countdown.Substring(3);
        }
        badgeText.Text = countdown;

        if (_isCompact)
        {
            badgeBorder.Background = Brushes.Transparent;
            badgeBorder.BorderBrush = Brushes.Transparent;
            badgeBorder.BorderThickness = new Thickness(0);
            badgeBorder.Padding = new Thickness(0);
            badgeBorder.HorizontalAlignment = HorizontalAlignment.Left;
            badgeBorder.Margin = new Thickness(9, 2, 0, 4);
            Grid.SetRow(badgeBorder, 1);
            Grid.SetColumn(badgeBorder, 0);
            Grid.SetColumnSpan(badgeBorder, 2);
            badgeText.Foreground = tm.TextPrimary;
        }
        else
        {
            Grid.SetRow(badgeBorder, 0);
            Grid.SetColumn(badgeBorder, 1);
            Grid.SetColumnSpan(badgeBorder, 1);
            badgeBorder.HorizontalAlignment = HorizontalAlignment.Right;
            badgeBorder.Margin = new Thickness(0);
            badgeBorder.BorderThickness = new Thickness(1.5);
            badgeBorder.Padding = new Thickness(10, 4, 10, 4);
            badgeBorder.CornerRadius = new CornerRadius(8);
            if (item.IsActive)
            {
                badgeBorder.Background = tm.Accent;
                badgeBorder.BorderBrush = tm.Accent;
                badgeText.Foreground = Brushes.White;
            }
            else if (item.IsFuture)
            {
                badgeBorder.Background = tm.ButtonActiveBackground;
                badgeBorder.BorderBrush = tm.Accent;
                badgeText.Foreground = tm.ButtonActiveForeground;
            }
            else
            {
                badgeBorder.Background = tm.ButtonInactiveBackground;
                badgeBorder.BorderBrush = tm.ButtonInactiveBorder;
                badgeText.Foreground = tm.TextMuted;
            }
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateTheme(bool isDark)
    {
        var tm = ThemeManager.Current;

        if (CardBorder != null)
        {
            CardBorder.Background = tm.HeaderBackground;
            CardBorder.BorderBrush = tm.HeaderBorder;
        }

        if (Divider1 != null) Divider1.Background = tm.HeaderBorder;
        if (TxtDialogTitle != null) TxtDialogTitle.Foreground = tm.TextPrimary;
        if (TxtChoicePrompt != null) TxtChoicePrompt.Foreground = tm.TextSecondary;
        if (TxtSectionEvents != null) TxtSectionEvents.Foreground = tm.TextMuted;
        if (TxtLoadingStatus != null) TxtLoadingStatus.Foreground = tm.TextPrimary;
        if (TxtLoadingSub != null) TxtLoadingSub.Foreground = tm.TextMuted;

        // Big choice buttons
        if (BtnBigGpsCard != null)
        {
            BtnBigGpsCard.Background = tm.ButtonInactiveBackground;
            BtnBigGpsCard.BorderBrush = tm.ButtonInactiveBorder;
        }
        if (BtnBigMapCard != null)
        {
            BtnBigMapCard.Background = tm.ButtonInactiveBackground;
            BtnBigMapCard.BorderBrush = tm.ButtonInactiveBorder;
        }
        if (TxtBigGpsTitle != null) TxtBigGpsTitle.Foreground = tm.TextPrimary;
        if (TxtBigGpsSub != null) TxtBigGpsSub.Foreground = tm.TextMuted;
        if (TxtBigMapTitle != null) TxtBigMapTitle.Foreground = tm.TextPrimary;
        if (TxtBigMapSub != null) TxtBigMapSub.Foreground = tm.TextMuted;

        // Segmented selector buttons
        if (_activeLat.HasValue)
        {
            SetActiveButton(_isGpsSelected);
        }
        else
        {
            ResetSegmentedButtons();
        }

        // Summary Card
        if (SummaryCardBorder != null)
        {
            SummaryCardBorder.Background = tm.ButtonInactiveBackground;
            SummaryCardBorder.BorderBrush = tm.ButtonInactiveBorder;
        }
        if (TxtSummaryCoords != null) TxtSummaryCoords.Foreground = tm.TextPrimary;
        if (TxtSummaryElevation != null) TxtSummaryElevation.Foreground = tm.Accent;
        if (TxtSummaryWindow != null) TxtSummaryWindow.Foreground = tm.TextSecondary;
        if (TxtSummaryMoonPhase != null) TxtSummaryMoonPhase.Foreground = tm.TextMuted;

        // All Timeline and Section Cards
        Border?[] allCards = [
            CardSunrise, CardSunset, CardMoonrise, CardMoonset,
            CardDaylightNoon, CardMorningBlue, CardMorningGolden,
            CardEveningGolden, CardEveningBlue, CardMilkyWay,
            CardTwilight, CardSeason
        ];
        foreach (var c in allCards)
        {
            if (c != null)
            {
                c.Background = tm.ButtonInactiveBackground;
                c.BorderBrush = tm.ButtonInactiveBorder;
            }
        }

        // Section Headers
        TextBlock?[] sectionHeaders = [TxtSectionEvents, TxtSectionSeason, TxtSectionTwilight];
        foreach (var sh in sectionHeaders)
        {
            if (sh != null) sh.Foreground = tm.TextMuted;
        }

        // Card Titles
        TextBlock?[] titles = [
            TxtSunriseTitle, TxtSunsetTitle, TxtMoonriseTitle, TxtMoonsetTitle,
            TxtDaylightTitle, TxtMorningBlueTitle, TxtMorningGoldenTitle,
            TxtEveningGoldenTitle, TxtEveningBlueTitle, TxtMilkyWayTitle,
            TxtSeasonTitle, TxtTrailLightTitle, TxtTwilightMorningHeader, TxtTwilightEveningHeader
        ];
        foreach (var t in titles)
        {
            if (t != null) t.Foreground = tm.TextPrimary;
        }

        // Prominent Times / Windows
        TextBlock?[] prominentTimes = [
            TxtSunriseTopoTime, TxtSunsetTopoTime, TxtMoonriseTopoTime, TxtMoonsetTopoTime,
            TxtSolarNoonTime, TxtMorningBlueTime, TxtMorningGoldenTime,
            TxtEveningGoldenTime, TxtEveningBlueTime, TxtMilkyWayWindow, TxtSeasonDate,
            TxtSunriseTopoTimeMobile, TxtSunsetTopoTimeMobile, TxtMoonriseTopoTimeMobile, TxtMoonsetTopoTimeMobile,
            TxtSolarNoonTimeMobile, TxtMorningBlueTimeMobile, TxtMorningGoldenTimeMobile,
            TxtEveningGoldenTimeMobile, TxtEveningBlueTimeMobile, TxtMilkyWayWindowMobile, TxtSeasonBadgeInline
        ];
        foreach (var pt in prominentTimes)
        {
            if (pt != null) pt.Foreground = tm.TextPrimary;
        }

        // Topo Azimuth & Secondary Text
        TextBlock?[] secondaryTexts = [
            TxtSunriseTopoAz, TxtSunsetTopoAz, TxtMoonriseTopoAz, TxtMoonsetTopoAz,
            TxtSolarNoonElevation, TxtSeasonTrend, TxtTrailLightWindow,
            TxtMilkyWayPeak, TxtMilkyWayClearance, TxtMilkyWayMoon,
            TxtMorningBlueSub, TxtMorningGoldenSub,
            TxtEveningGoldenSub, TxtEveningBlueSub,
            TxtSunriseTopoAzMobile, TxtSunsetTopoAzMobile, TxtMoonriseTopoAzMobile, TxtMoonsetTopoAzMobile,
            TxtSolarNoonElevationMobile, TxtMorningBlueSubMobile, TxtMorningGoldenSubMobile,
            TxtEveningGoldenSubMobile, TxtEveningBlueSubMobile,
            TxtMilkyWayPeakMobile, TxtMilkyWayClearanceMobile, TxtMilkyWayMoonMobile,
            TxtAstroDawnLabelMobile, TxtNauticalDawnLabelMobile, TxtCivilDawnLabelMobile,
            TxtCivilDuskLabelMobile, TxtNauticalDuskLabelMobile, TxtAstroDuskLabelMobile
        ];
        foreach (var st in secondaryTexts)
        {
            if (st != null) st.Foreground = tm.TextSecondary;
        }

        // Standards & Shifts
        TextBlock?[] standards = [
            TxtSunriseStandard, TxtSunsetStandard, TxtMoonriseStandard, TxtMoonsetStandard,
            TxtSunriseStandardMobile, TxtSunsetStandardMobile, TxtMoonriseStandardMobile, TxtMoonsetStandardMobile
        ];
        foreach (var s in standards)
        {
            if (s != null) s.Foreground = tm.TextMuted;
        }

        TextBlock?[] shifts = [
            TxtSunriseShift, TxtSunsetShift, TxtMoonriseShift, TxtMoonsetShift,
            TxtMorningGoldenDirect, TxtEveningGoldenDirect,
            TxtSunriseShiftMobile, TxtSunsetShiftMobile, TxtMoonriseShiftMobile, TxtMoonsetShiftMobile,
            TxtMorningGoldenDirectMobile, TxtEveningGoldenDirectMobile
        ];
        foreach (var sh in shifts)
        {
            if (sh != null) sh.Foreground = tm.TextSecondary;
        }

        // Additional Card Sub-Borders
        Border?[] subBoxes = [DaylightCompareBox, TrailLightBanner, MilkyWayDetailsBox];
        foreach (var sb in subBoxes)
        {
            if (sb != null)
            {
                sb.Background = tm.MapOverlayBackground;
                sb.BorderBrush = tm.HeaderBorder;
            }
        }

        // Accent Highlights
        if (TxtTopoDaylight != null) TxtTopoDaylight.Foreground = tm.Accent;
        if (TxtTopoDaylightMobile != null) TxtTopoDaylightMobile.Foreground = tm.Accent;
        if (TxtTopoSunriseMiniTime != null) TxtTopoSunriseMiniTime.Foreground = tm.Accent;
        if (TxtTopoSunriseMiniTimeMobile != null) TxtTopoSunriseMiniTimeMobile.Foreground = tm.Accent;
        if (TxtTopoSunsetMiniTime != null) TxtTopoSunsetMiniTime.Foreground = tm.Accent;
        if (TxtTopoSunsetMiniTimeMobile != null) TxtTopoSunsetMiniTimeMobile.Foreground = tm.Accent;
        if (TxtTopoSunriseHeader != null) TxtTopoSunriseHeader.Foreground = tm.Accent;
        if (TxtTopoSunriseHeaderMobile != null) TxtTopoSunriseHeaderMobile.Foreground = tm.Accent;
        if (TxtTopoSunsetHeader != null) TxtTopoSunsetHeader.Foreground = tm.Accent;
        if (TxtTopoSunsetHeaderMobile != null) TxtTopoSunsetHeaderMobile.Foreground = tm.Accent;

        // Muted Descriptions
        TextBlock?[] mutedTexts = [
            TxtFlatDaylight, TxtTopoLost, TxtDaylightTrend,
            TxtFlatDaylightMobile, TxtTopoLostMobile, TxtDaylightTrendMobile,
            TxtDesktopSeasonDesc, TxtDesktopTrailLightNote,
            TxtTwilightNoteSunrise, TxtTwilightNoteSunset,
            TxtMorningBlueDesc, TxtEveningBlueDesc
        ];
        foreach (var mt in mutedTexts)
        {
            if (mt != null) mt.Foreground = tm.TextMuted;
        }

        // Progression Times
        TextBlock?[] progTimes = [
            TxtAstroDawnTime, TxtNauticalDawnTime, TxtCivilDawnTime,
            TxtCivilDuskTime, TxtNauticalDuskTime, TxtAstroDuskTime,
            TxtAstroDawnTimeMobile, TxtNauticalDawnTimeMobile, TxtCivilDawnTimeMobile,
            TxtCivilDuskTimeMobile, TxtNauticalDuskTimeMobile, TxtAstroDuskTimeMobile
        ];
        foreach (var pt in progTimes)
        {
            if (pt != null) pt.Foreground = tm.TextPrimary;
        }

        // All Timeline Badges styling
        Border?[] allBadges = [
            BadgeSunrise, BadgeSunset, BadgeMoonrise, BadgeMoonset,
            BadgeSolarNoon, BadgeMorningBlue, BadgeMorningGolden,
            BadgeEveningGolden, BadgeEveningBlue, BadgeMilkyWay
        ];
        TextBlock?[] allBadgeTexts = [
            TxtSunriseBadge, TxtSunsetBadge, TxtMoonriseBadge, TxtMoonsetBadge,
            TxtSolarNoonBadge, TxtMorningBlueBadge, TxtMorningGoldenBadge,
            TxtEveningGoldenBadge, TxtEveningBlueBadge, TxtMilkyWayBadge
        ];

        if (_isCompact)
        {
            foreach (var b in allBadges)
            {
                if (b != null)
                {
                    b.Background = Brushes.Transparent;
                    b.BorderBrush = Brushes.Transparent;
                    b.BorderThickness = new Thickness(0);
                    b.Padding = new Thickness(0);
                }
            }
            foreach (var bt in allBadgeTexts)
            {
                if (bt != null)
                {
                    bt.Foreground = tm.TextPrimary;
                }
            }
        }

        // Season Badge
        if (BadgeSeason != null)
        {
            BadgeSeason.Background = tm.ButtonInactiveBackground;
            if (TxtSeasonBadge != null) TxtSeasonBadge.Foreground = tm.TextSecondary;
        }

        // Milky Way Badge (when not compact)
        if (!_isCompact && BadgeMilkyWay != null && TxtMilkyWayBadge != null)
        {
            if (_lastReport?.MilkyWay != null)
            {
                if (_lastReport.MilkyWay.IsActive)
                {
                    BadgeMilkyWay.Background = tm.Accent;
                    TxtMilkyWayBadge.Foreground = Brushes.White;
                }
                else if (_lastReport.MilkyWay.IsFuture && _lastReport.MilkyWay.IsQualityPositive)
                {
                    BadgeMilkyWay.Background = tm.ButtonActiveBackground;
                    TxtMilkyWayBadge.Foreground = tm.ButtonActiveForeground;
                }
                else
                {
                    BadgeMilkyWay.Background = tm.ButtonInactiveBackground;
                    TxtMilkyWayBadge.Foreground = tm.TextMuted;
                }
            }
            else
            {
                BadgeMilkyWay.Background = tm.ButtonInactiveBackground;
                TxtMilkyWayBadge.Foreground = tm.TextMuted;
            }
        }

        // Milky Way Status
        if (TxtMilkyWayStatus != null)
        {
            bool isQualityPositive = _lastReport?.MilkyWay?.IsQualityPositive ?? true;
            var mwColor = !isQualityPositive
                ? tm.TextMuted
                : (isDark
                    ? new SolidColorBrush(Color.FromArgb(255, 165, 180, 252))
                    : new SolidColorBrush(Color.FromArgb(255, 79, 70, 229)));
            TxtMilkyWayStatus.Foreground = mwColor;
            if (TxtMilkyWayStatusMobile != null) TxtMilkyWayStatusMobile.Foreground = mwColor;
        }

        // Close and Refresh buttons
        if (BtnCloseDialog != null)
        {
            BtnCloseDialog.Background = tm.ButtonInactiveBackground;
            BtnCloseDialog.BorderBrush = tm.ButtonInactiveBorder;
            BtnCloseDialog.Foreground = tm.TextPrimary;
        }

        if (BtnRefresh != null)
        {
            BtnRefresh.Background = tm.ButtonInactiveBackground;
            BtnRefresh.BorderBrush = tm.ButtonInactiveBorder;
            BtnRefresh.Foreground = tm.TextPrimary;
        }

        if (BtnTopRefresh != null)
        {
            BtnTopRefresh.Background = tm.ButtonInactiveBackground;
            BtnTopRefresh.BorderBrush = tm.ButtonInactiveBorder;
            BtnTopRefresh.Foreground = tm.TextPrimary;
        }

        if (BtnCancelBottom != null)
        {
            BtnCancelBottom.Background = tm.ButtonInactiveBackground;
            BtnCancelBottom.BorderBrush = tm.ButtonInactiveBorder;
            BtnCancelBottom.Foreground = tm.TextPrimary;
        }
    }
}
