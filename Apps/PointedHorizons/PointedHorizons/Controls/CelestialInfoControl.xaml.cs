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
    private CelestialWindowReport? _lastReport;

    public CelestialInfoControl()
    {
        InitializeComponent();
        Loaded += CelestialInfoControl_Loaded;
        SizeChanged += CelestialInfoControl_SizeChanged;
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
        if (ColumnsContainerGrid == null || ColLeftDef == null || ColRightDef == null || ColLeftStack == null || ColRightStack == null) return;

        if (isCompact)
        {
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
        if (BtnOptionMap != null)
        {
            BtnOptionMap.Background = tm.ButtonInactiveBackground;
            BtnOptionMap.BorderBrush = tm.ButtonInactiveBorder;
            BtnOptionMap.Foreground = tm.TextPrimary;
        }
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
        if (BtnOptionMap != null)
        {
            BtnOptionMap.Background = !isGps ? tm.ButtonActiveBackground : tm.ButtonInactiveBackground;
            BtnOptionMap.BorderBrush = !isGps ? tm.ButtonActiveBorder : tm.ButtonInactiveBorder;
            BtnOptionMap.Foreground = !isGps ? tm.ButtonActiveForeground : tm.TextPrimary;
        }
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
            TxtSunriseTopoAz,
            TxtSunriseTopoTag,
            TxtSunriseStandard,
            TxtSunriseShift,
            BadgeSunrise,
            TxtSunriseBadge);

        // 2. Sunset
        BindEventCard(
            report.Sunset,
            TxtSunsetTitle,
            TxtSunsetTopoTime,
            TxtSunsetTopoAz,
            TxtSunsetTopoTag,
            TxtSunsetStandard,
            TxtSunsetShift,
            BadgeSunset,
            TxtSunsetBadge);

        // 3. Moonrise
        BindEventCard(
            report.Moonrise,
            TxtMoonriseTitle,
            TxtMoonriseTopoTime,
            TxtMoonriseTopoAz,
            TxtMoonriseTopoTag,
            TxtMoonriseStandard,
            TxtMoonriseShift,
            BadgeMoonrise,
            TxtMoonriseBadge);

        // 4. Moonset
        BindEventCard(
            report.Moonset,
            TxtMoonsetTitle,
            TxtMoonsetTopoTime,
            TxtMoonsetTopoAz,
            TxtMoonsetTopoTag,
            TxtMoonsetStandard,
            TxtMoonsetShift,
            BadgeMoonset,
            TxtMoonsetBadge);

        // 5. Daylight & Solar Noon Culmination Card
        if (report.SolarNoon != null)
        {
            if (TxtSolarNoonTime != null) TxtSolarNoonTime.Text = report.SolarNoon.NoonTimeDisplay;
            if (TxtSolarNoonElevation != null) TxtSolarNoonElevation.Text = $"Max Elevation: {report.SolarNoon.MaxElevationDisplay}";
            if (TxtSolarNoonBadge != null) TxtSolarNoonBadge.Text = report.SolarNoon.NoonCountdownDisplay;
            if (TxtTopoDaylight != null) TxtTopoDaylight.Text = $"⛰️ {report.SolarNoon.TopoDaylightDisplay}";
            if (TxtFlatDaylight != null) TxtFlatDaylight.Text = report.SolarNoon.FlatDaylightDisplay;
            if (TxtTopoLost != null) TxtTopoLost.Text = report.SolarNoon.TopoLostDisplay;
            if (TxtDaylightTrend != null) TxtDaylightTrend.Text = report.SolarNoon.DayChangeDisplay;

            if (BadgeSolarNoon != null && TxtSolarNoonBadge != null)
            {
                var tm = ThemeManager.Current;
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

        // 6. Individual Photography Window Cards
        if (report.PhotographyHours != null)
        {
            BindPhotoCard(report.PhotographyHours.MorningBlue, CardMorningBlue, TxtMorningBlueTime, TxtMorningBlueSub, null, TxtMorningBlueDesc, BadgeMorningBlue, TxtMorningBlueBadge);
            BindPhotoCard(report.PhotographyHours.MorningGolden, CardMorningGolden, TxtMorningGoldenTime, TxtMorningGoldenSub, TxtMorningGoldenDirect, null, BadgeMorningGolden, TxtMorningGoldenBadge);
            BindPhotoCard(report.PhotographyHours.EveningGolden, CardEveningGolden, TxtEveningGoldenTime, TxtEveningGoldenSub, TxtEveningGoldenDirect, null, BadgeEveningGolden, TxtEveningGoldenBadge);
            BindPhotoCard(report.PhotographyHours.EveningBlue, CardEveningBlue, TxtEveningBlueTime, TxtEveningBlueSub, null, TxtEveningBlueDesc, BadgeEveningBlue, TxtEveningBlueBadge);
        }

        // 7. Milky Way & Galactic Center Card
        if (report.MilkyWay != null)
        {
            if (TxtMilkyWayStatus != null) TxtMilkyWayStatus.Text = report.MilkyWay.StatusText;
            if (TxtMilkyWayBadge != null) TxtMilkyWayBadge.Text = report.MilkyWay.QualityBadge;
            if (TxtMilkyWayWindow != null) TxtMilkyWayWindow.Text = report.MilkyWay.WindowDisplay;
            if (TxtMilkyWayPeak != null) TxtMilkyWayPeak.Text = report.MilkyWay.PeakDisplay;
            if (TxtMilkyWayClearance != null) TxtMilkyWayClearance.Text = report.MilkyWay.MountainClearanceDisplay;
            if (TxtMilkyWayMoon != null) TxtMilkyWayMoon.Text = report.MilkyWay.MoonInterferenceDisplay;

            if (BadgeMilkyWay != null && TxtMilkyWayBadge != null)
            {
                var tm = ThemeManager.Current;
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
            if (TxtSeasonDate != null) TxtSeasonDate.Text = report.SeasonMarker.NextMarkerDateDisplay;
            if (TxtSeasonTrend != null) TxtSeasonTrend.Text = report.SeasonMarker.TrendDescription;
            if (TxtDesktopSeasonDesc != null) TxtDesktopSeasonDesc.Text = report.SeasonMarker.Description;
        }

        // 10. Twilight Phases & Trail Light
        if (report.Twilight != null)
        {
            if (TxtTrailLightWindow != null) TxtTrailLightWindow.Text = $"{report.Twilight.TrailLightWindowDisplay} ({report.Twilight.TrailLightDurationDisplay})";
            if (TxtDesktopTrailLightNote != null) TxtDesktopTrailLightNote.Text = report.Twilight.TrailLightNote;
            if (TxtAstroDawnTime != null) TxtAstroDawnTime.Text = report.Twilight.AstroDawn?.TimeDisplay ?? "--";
            if (TxtNauticalDawnTime != null) TxtNauticalDawnTime.Text = report.Twilight.NauticalDawn?.TimeDisplay ?? "--";
            if (TxtCivilDawnTime != null) TxtCivilDawnTime.Text = report.Twilight.CivilDawn?.TimeDisplay ?? "--";
            if (TxtTopoSunriseMiniTime != null) TxtTopoSunriseMiniTime.Text = report.Twilight.MountainSunrise?.TimeDisplay ?? "--";

            if (TxtTopoSunsetMiniTime != null) TxtTopoSunsetMiniTime.Text = report.Twilight.MountainSunset?.TimeDisplay ?? "--";
            if (TxtCivilDuskTime != null) TxtCivilDuskTime.Text = report.Twilight.CivilDusk?.TimeDisplay ?? "--";
            if (TxtNauticalDuskTime != null) TxtNauticalDuskTime.Text = report.Twilight.NauticalDusk?.TimeDisplay ?? "--";
            if (TxtAstroDuskTime != null) TxtAstroDuskTime.Text = report.Twilight.AstroDusk?.TimeDisplay ?? "--";

            if (TxtTwilightNoteSunrise != null) TxtTwilightNoteSunrise.Text = $"• Mountain Sunrise: {report.Twilight.MountainSunrise?.Note ?? "--"}";
            if (TxtTwilightNoteSunset != null) TxtTwilightNoteSunset.Text = $"• Mountain Sunset: {report.Twilight.MountainSunset?.Note ?? "--"}";
        }

        UpdateTheme(ThemeManager.Current.IsDark);
    }

    private void BindEventCard(
        WindowCelestialEvent? ev,
        TextBlock titleText,
        TextBlock topoTimeText,
        TextBlock topoAzText,
        TextBlock topoTagText,
        TextBlock standardText,
        TextBlock shiftText,
        Border badgeBorder,
        TextBlock badgeText)
    {
        var tm = ThemeManager.Current;

        if (ev == null || !ev.HasEvent)
        {
            topoTimeText.Text = "None found";
            topoAzText.Text = "--";
            topoTagText.Visibility = Visibility.Collapsed;
            standardText.Text = "Standard: None";
            shiftText.Text = "No crossing";
            badgeBorder.Visibility = Visibility.Collapsed;
            return;
        }

        topoTagText.Visibility = Visibility.Visible;

        // Topo displays
        topoTimeText.Text = ev.TopoTimeDisplay;
        topoAzText.Text = $"{ev.TopoAzimuthDisplay}  •  {ev.TopoElevationDisplay}";

        // Standard comparison
        standardText.Text = $"Standard: {ev.AstroTimeDisplay}";
        shiftText.Text = ev.TopoShiftDisplay;

        // Countdown / Past Badge
        badgeBorder.Visibility = Visibility.Visible;
        if (ev.IsFuture)
        {
            badgeText.Text = $"⏳ {ev.TimeUntilDisplay}";
            badgeBorder.Background = tm.ButtonActiveBackground;
            badgeText.Foreground = tm.ButtonActiveForeground;
        }
        else
        {
            badgeText.Text = $"✓ {ev.TimeUntilDisplay}";
            badgeBorder.Background = tm.ButtonInactiveBackground;
            badgeText.Foreground = tm.TextMuted;
        }
    }

    private void BindPhotoCard(
        PhotographyWindowItem? item,
        Border card,
        TextBlock timeText,
        TextBlock subText,
        TextBlock? directSunText,
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
        subText.Text = item.SubTitle;
        if (directSunText != null) directSunText.Text = item.DirectSunDisplay;
        if (descText != null) descText.Text = item.Description;

        badgeText.Text = item.CountdownDisplay;
        if (item.IsActive)
        {
            badgeBorder.Background = tm.Accent;
            badgeText.Foreground = Brushes.White;
        }
        else if (item.IsFuture)
        {
            badgeBorder.Background = tm.ButtonActiveBackground;
            badgeText.Foreground = tm.ButtonActiveForeground;
        }
        else
        {
            badgeBorder.Background = tm.ButtonInactiveBackground;
            badgeText.Foreground = tm.TextMuted;
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
            TxtEveningGoldenTime, TxtEveningBlueTime, TxtMilkyWayWindow, TxtSeasonDate
        ];
        foreach (var pt in prominentTimes)
        {
            if (pt != null) pt.Foreground = tm.TextPrimary;
        }

        // Topo Azimuth & Secondary Text
        TextBlock?[] secondaryTexts = [
            TxtSunriseTopoAz, TxtSunsetTopoAz, TxtMoonriseTopoAz, TxtMoonsetTopoAz,
            TxtSolarNoonElevation, TxtSeasonTrend, TxtTrailLightWindow,
            TxtMilkyWayPeak, TxtMorningBlueSub, TxtMorningGoldenSub,
            TxtEveningGoldenSub, TxtEveningBlueSub
        ];
        foreach (var st in secondaryTexts)
        {
            if (st != null) st.Foreground = tm.TextSecondary;
        }

        // Standards & Shifts
        TextBlock?[] standards = [TxtSunriseStandard, TxtSunsetStandard, TxtMoonriseStandard, TxtMoonsetStandard];
        foreach (var s in standards)
        {
            if (s != null) s.Foreground = tm.TextMuted;
        }

        TextBlock?[] shifts = [
            TxtSunriseShift, TxtSunsetShift, TxtMoonriseShift, TxtMoonsetShift,
            TxtMorningGoldenDirect, TxtEveningGoldenDirect
        ];
        foreach (var sh in shifts)
        {
            if (sh != null) sh.Foreground = tm.TextSecondary;
        }

        // Additional Card Sub-Borders
        Border?[] subBoxes = [DaylightCompareBox, TrailLightBanner];
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
        if (TxtTopoSunriseMiniTime != null) TxtTopoSunriseMiniTime.Foreground = tm.Accent;
        if (TxtTopoSunsetMiniTime != null) TxtTopoSunsetMiniTime.Foreground = tm.Accent;
        if (TxtTopoSunriseHeader != null) TxtTopoSunriseHeader.Foreground = tm.Accent;
        if (TxtTopoSunsetHeader != null) TxtTopoSunsetHeader.Foreground = tm.Accent;

        // Muted Descriptions
        TextBlock?[] mutedTexts = [
            TxtFlatDaylight, TxtTopoLost, TxtDaylightTrend,
            TxtDesktopSeasonDesc, TxtDesktopTrailLightNote,
            TxtTwilightNoteSunrise, TxtTwilightNoteSunset,
            TxtMilkyWayClearance, TxtMilkyWayMoon,
            TxtMorningBlueDesc, TxtEveningBlueDesc
        ];
        foreach (var mt in mutedTexts)
        {
            if (mt != null) mt.Foreground = tm.TextMuted;
        }

        // Progression Times
        TextBlock?[] progTimes = [
            TxtAstroDawnTime, TxtNauticalDawnTime, TxtCivilDawnTime,
            TxtCivilDuskTime, TxtNauticalDuskTime, TxtAstroDuskTime
        ];
        foreach (var pt in progTimes)
        {
            if (pt != null) pt.Foreground = tm.TextPrimary;
        }

        // Season Badge
        if (BadgeSeason != null)
        {
            BadgeSeason.Background = tm.ButtonInactiveBackground;
            if (TxtSeasonBadge != null) TxtSeasonBadge.Foreground = tm.TextSecondary;
        }

        // Milky Way Badge
        if (BadgeMilkyWay != null)
        {
            bool isQualityPositive = _lastReport?.MilkyWay?.IsQualityPositive ?? false;
            BadgeMilkyWay.Background = isQualityPositive ? tm.ButtonActiveBackground : tm.ButtonInactiveBackground;
            if (TxtMilkyWayBadge != null)
            {
                TxtMilkyWayBadge.Foreground = isQualityPositive ? tm.ButtonActiveForeground : tm.TextMuted;
            }
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
