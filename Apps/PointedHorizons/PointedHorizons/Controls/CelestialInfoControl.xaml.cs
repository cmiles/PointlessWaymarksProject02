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
    }

    private void CelestialInfoControl_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateTheme(ThemeManager.Current.IsDark);
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

        double lat = _mapLat ?? ViewModel?.Latitude ?? 32.2226;
        double lon = _mapLon ?? ViewModel?.Longitude ?? -110.9747;
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
        else if (_activeLat.HasValue && _activeLon.HasValue)
        {
            await RunCalculationAsync(_activeLat.Value, _activeLon.Value, isGps: false);
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

        // Order cards from farthest past to most in the future (chronological ascending)
        if (EventsCardsContainer != null)
        {
            var orderedCards = new List<(WindowCelestialEvent? Ev, Border Card)>
            {
                (report.Sunrise, CardSunrise),
                (report.Sunset, CardSunset),
                (report.Moonrise, CardMoonrise),
                (report.Moonset, CardMoonset)
            }
            .OrderBy(c => c.Ev?.EffectiveTime ?? DateTime.MaxValue)
            .ToList();

            EventsCardsContainer.Children.Clear();
            for (int i = 0; i < orderedCards.Count; i++)
            {
                var card = orderedCards[i].Card;
                card.Margin = (i == orderedCards.Count - 1)
                    ? new Thickness(0, 0, 0, 4)
                    : new Thickness(0, 0, 0, 8);
                EventsCardsContainer.Children.Add(card);
            }
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

        // Event Cards
        Border[] cards = [CardSunrise, CardSunset, CardMoonrise, CardMoonset];
        foreach (var c in cards)
        {
            if (c != null)
            {
                c.Background = tm.ButtonInactiveBackground;
                c.BorderBrush = tm.ButtonInactiveBorder;
            }
        }

        // Titles
        TextBlock[] titles = [TxtSunriseTitle, TxtSunsetTitle, TxtMoonriseTitle, TxtMoonsetTitle];
        foreach (var t in titles)
        {
            if (t != null) t.Foreground = tm.TextPrimary;
        }

        // Topo Times (Large)
        TextBlock[] topoTimes = [TxtSunriseTopoTime, TxtSunsetTopoTime, TxtMoonriseTopoTime, TxtMoonsetTopoTime];
        foreach (var tt in topoTimes)
        {
            if (tt != null) tt.Foreground = tm.TextPrimary;
        }

        // Topo Azimuth
        TextBlock[] topoAzs = [TxtSunriseTopoAz, TxtSunsetTopoAz, TxtMoonriseTopoAz, TxtMoonsetTopoAz];
        foreach (var az in topoAzs)
        {
            if (az != null) az.Foreground = tm.TextSecondary;
        }

        // Standards & Shifts
        TextBlock[] standards = [TxtSunriseStandard, TxtSunsetStandard, TxtMoonriseStandard, TxtMoonsetStandard];
        foreach (var s in standards)
        {
            if (s != null) s.Foreground = tm.TextMuted;
        }

        TextBlock[] shifts = [TxtSunriseShift, TxtSunsetShift, TxtMoonriseShift, TxtMoonsetShift];
        foreach (var sh in shifts)
        {
            if (sh != null) sh.Foreground = tm.TextSecondary;
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

        if (BtnCancelBottom != null)
        {
            BtnCancelBottom.Background = tm.ButtonInactiveBackground;
            BtnCancelBottom.BorderBrush = tm.ButtonInactiveBorder;
            BtnCancelBottom.Foreground = tm.TextPrimary;
        }
    }
}
