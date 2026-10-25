using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using HistoricTempests.Models;

namespace HistoricTempests.Controls;

public partial class MultiYearTimelineLineChart : UserControl
{
    private static readonly SolidColorBrush BrightRedBrush = new(Color.FromArgb(255, 239, 68, 68));   // #EF4444
    private static readonly SolidColorBrush BrightBlueBrush = new(Color.FromArgb(255, 59, 130, 246));  // #3B82F6
    private static readonly SolidColorBrush BoldIndigoBrush = new(Color.FromArgb(255, 99, 102, 241));  // #6366F1
    private static readonly SolidColorBrush BoldWindBrush = new(Color.FromArgb(255, 15, 118, 110));    // #0F766E
    private static readonly SolidColorBrush BoldSolarBrush = new(Color.FromArgb(255, 217, 119, 6));    // #D97706 Warm Solar Amber

    // Pale, low-contrast, toned down colors for previous years
    private static readonly SolidColorBrush Pale2025Brush = new(Color.FromArgb(200, 110, 231, 183)); // Pale Emerald
    private static readonly SolidColorBrush Pale2024Brush = new(Color.FromArgb(200, 252, 211, 77));  // Pale Amber
    private static readonly SolidColorBrush Pale2023Brush = new(Color.FromArgb(200, 148, 163, 184)); // Pale Slate

    private static SolidColorBrush GridBrush => Services.ThemeManager.Current.GridLineBrush;
    private static SolidColorBrush AxisTextBrush => Services.ThemeManager.Current.AxisTextBrush;
    private static SolidColorBrush ForwardBandBrush => Services.ThemeManager.Current.FutureBandBrush;

    public static readonly DependencyProperty DayDataProperty =
        DependencyProperty.Register(nameof(DayData), typeof(IEnumerable<DayTimelinePoint>),
            typeof(MultiYearTimelineLineChart), new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty MonthDataProperty =
        DependencyProperty.Register(nameof(MonthData), typeof(IEnumerable<MonthTimelinePoint>),
            typeof(MultiYearTimelineLineChart), new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.Register(nameof(Mode), typeof(string),
            typeof(MultiYearTimelineLineChart), new PropertyMetadata("TemperatureDual", OnDataChanged));

    public static readonly DependencyProperty IsMetricProperty =
        DependencyProperty.Register(nameof(IsMetric), typeof(bool),
            typeof(MultiYearTimelineLineChart), new PropertyMetadata(false, OnDataChanged));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string),
            typeof(MultiYearTimelineLineChart), new PropertyMetadata("Daily Temperature", OnDataChanged));

    public static readonly DependencyProperty ShowFutureShadingProperty =
        DependencyProperty.Register(nameof(ShowFutureShading), typeof(bool),
            typeof(MultiYearTimelineLineChart), new PropertyMetadata(false, OnDataChanged));

    private readonly System.Windows.Threading.DispatcherTimer _resizeTimer;

    public MultiYearTimelineLineChart()
    {
        InitializeComponent();
        _resizeTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(75) };
        _resizeTimer.Tick += (s, e) =>
        {
            _resizeTimer.Stop();
            RenderChart();
        };

        Services.ThemeManager.Current.ThemeChanged += (_, _) =>
        {
            ApplyTheme();
            RenderChart();
        };

        Loaded += (_, _) =>
        {
            ApplyTheme();
            RenderChart();
        };
        SizeChanged += (_, _) =>
        {
            _resizeTimer.Stop();
            _resizeTimer.Start();
        };

        ChartGrid.MouseMove += OnChartGridMouseMove;
        ChartGrid.MouseLeave += OnChartGridMouseLeave;
    }

    public void ApplyTheme()
    {
        if (ChartCardBorder != null)
        {
            ChartCardBorder.Background = Services.ThemeManager.Current.CardBackground;
            ChartCardBorder.BorderBrush = Services.ThemeManager.Current.CardBorder;
        }
        if (ChartTitleBlock != null)
        {
            ChartTitleBlock.Foreground = Services.ThemeManager.Current.TextPrimary;
            ChartTitleBlock.FontSize = Services.ThemeManager.Current.FontSizeMedium;
        }
        if (ChartUnitBlock != null)
        {
            ChartUnitBlock.Foreground = Services.ThemeManager.Current.TextSecondary;
            ChartUnitBlock.FontSize = Services.ThemeManager.Current.FontSizeRegular;
        }
        if (LegendContainer != null)
        {
            double legendSize = Services.ThemeManager.Current.FontSizeLegend;
            foreach (var child in LegendContainer.Children)
            {
                if (child is Panel p)
                {
                    foreach (var sub in p.Children)
                    {
                        if (sub is TextBlock tb)
                        {
                            tb.FontSize = legendSize;
                            tb.Foreground = Services.ThemeManager.Current.TextSecondary;
                        }
                    }
                }
            }
        }
    }

    public IEnumerable<DayTimelinePoint>? DayData
    {
        get => (IEnumerable<DayTimelinePoint>?)GetValue(DayDataProperty);
        set => SetValue(DayDataProperty, value);
    }

    public IEnumerable<MonthTimelinePoint>? MonthData
    {
        get => (IEnumerable<MonthTimelinePoint>?)GetValue(MonthDataProperty);
        set => SetValue(MonthDataProperty, value);
    }

    public string Mode
    {
        get => (string)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public bool IsMetric
    {
        get => (bool)GetValue(IsMetricProperty);
        set => SetValue(IsMetricProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool ShowFutureShading
    {
        get => (bool)GetValue(ShowFutureShadingProperty);
        set => SetValue(ShowFutureShadingProperty, value);
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MultiYearTimelineLineChart chart)
        {
            chart.RenderChart();
        }
    }

    public void RenderChart()
    {
        if (ChartCanvas == null || GridLinesCanvas == null || YAxisCanvas == null || 
            XAxisCanvas == null || OverlayCanvas == null || DividerCanvas == null || BackgroundBandsCanvas == null)
            return;

        if (ChartCardBorder != null)
        {
            ChartCardBorder.Padding = ActualWidth < 500 ? new Thickness(2, 10, 6, 10) : new Thickness(2, 12, 10, 12);
        }

        if (ChartTitleBlock != null && ActualWidth > 60)
        {
            ChartTitleBlock.MaxWidth = Math.Max(120.0, ActualWidth - 45.0);
        }

        _lastHoverIndex = -1;
        HideTooltip();
        _tooltipBorder = null;
        _tooltipPanel = null;
        _indicatorLine = null;

        ChartCanvas.Children.Clear();
        GridLinesCanvas.Children.Clear();
        YAxisCanvas.Children.Clear();
        XAxisCanvas.Children.Clear();
        OverlayCanvas.Children.Clear();
        DividerCanvas.Children.Clear();
        BackgroundBandsCanvas.Children.Clear();

        double width = ChartCanvas.ActualWidth;
        double height = ChartCanvas.ActualHeight;
        if (width <= 10 || height <= 10) return;

        bool isMetric = IsMetric;
        string mode = Mode ?? "TemperatureDual";

        // Temperatures must strictly be in Fahrenheit (°F)
        if (mode == "TemperatureDual" || mode == "AverageTemperature")
        {
            isMetric = false;
        }

        if (ChartTitleBlock != null) ChartTitleBlock.Text = Title;
        if (ChartUnitBlock != null)
        {
            if (mode == "WindGust")
                ChartUnitBlock.Text = isMetric ? "(km/h)" : "(mph)";
            else if (mode == "SolarRadiation")
                ChartUnitBlock.Text = "(W/m²)";
            else
                ChartUnitBlock.Text = "(°F)";
        }

        UpdateLegends(mode);

        bool isDayMode = DayData != null && DayData.Any();
        int totalSlots = isDayMode ? DayData!.Count() : (MonthData != null ? MonthData!.Count() : 0);
        if (totalSlots == 0) return;

        // Calculate min and max values across all points and years
        double minVal = double.MaxValue;
        double maxVal = double.MinValue;

        if (mode == "WindGust")
        {
            minVal = 0.0;
            maxVal = 0.0;
            if (isDayMode)
            {
                foreach (var p in DayData!)
                {
                    double? gust = isMetric ? p.ThisYearGustKmh : p.ThisYearGustMph;
                    if (gust.HasValue) maxVal = Math.Max(maxVal, gust.Value);
                    foreach (var py in p.PrevYears)
                    {
                        double? pg = isMetric ? py.GustKmh : py.GustMph;
                        if (pg.HasValue) maxVal = Math.Max(maxVal, pg.Value);
                    }
                }
            }
            else if (MonthData != null)
            {
                foreach (var m in MonthData)
                {
                    double? gust = isMetric ? m.ThisYearGustKmh : m.ThisYearGustMph;
                    if (gust.HasValue) maxVal = Math.Max(maxVal, gust.Value);
                    foreach (var py in m.PrevYears)
                    {
                        double? pg = isMetric ? py.GustKmh : py.GustMph;
                        if (pg.HasValue) maxVal = Math.Max(maxVal, pg.Value);
                    }
                }
            }

            if (maxVal <= 0.1) maxVal = isMetric ? 60.0 : 40.0;
            maxVal = Math.Ceiling((maxVal * 1.15) / 5) * 5;
        }
        else if (mode == "SolarRadiation")
        {
            minVal = 0.0;
            maxVal = 0.0;
            if (isDayMode)
            {
                foreach (var p in DayData!)
                {
                    if (p.ThisYearSolarRadiationAvg.HasValue) maxVal = Math.Max(maxVal, p.ThisYearSolarRadiationAvg.Value);
                    foreach (var py in p.PrevYears)
                    {
                        if (py.SolarRadiationAvg.HasValue) maxVal = Math.Max(maxVal, py.SolarRadiationAvg.Value);
                    }
                }
            }
            else if (MonthData != null)
            {
                foreach (var m in MonthData)
                {
                    if (m.ThisYearSolarRadiationAvg.HasValue) maxVal = Math.Max(maxVal, m.ThisYearSolarRadiationAvg.Value);
                    foreach (var py in m.PrevYears)
                    {
                        if (py.SolarRadiationAvg.HasValue) maxVal = Math.Max(maxVal, py.SolarRadiationAvg.Value);
                    }
                }
            }

            if (maxVal <= 0.1) maxVal = 300.0;
            maxVal = Math.Ceiling((maxVal * 1.15) / 50) * 50;
        }
        else if (isDayMode)
        {
            foreach (var p in DayData!)
            {
                if (mode == "TemperatureDual")
                {
                    double? hi = p.ThisYearMaxTempF;
                    double? lo = p.ThisYearMinTempF;
                    if (hi.HasValue) { maxVal = Math.Max(maxVal, hi.Value); minVal = Math.Min(minVal, hi.Value); }
                    if (lo.HasValue) { maxVal = Math.Max(maxVal, lo.Value); minVal = Math.Min(minVal, lo.Value); }

                    foreach (var py in p.PrevYears)
                    {
                        double? phi = py.MaxTempF;
                        double? plo = py.MinTempF;
                        if (phi.HasValue) { maxVal = Math.Max(maxVal, phi.Value); minVal = Math.Min(minVal, phi.Value); }
                        if (plo.HasValue) { maxVal = Math.Max(maxVal, plo.Value); minVal = Math.Min(minVal, plo.Value); }
                    }
                }
                else
                {
                    double? avg = p.ThisYearAvgTempF;
                    if (avg.HasValue) { maxVal = Math.Max(maxVal, avg.Value); minVal = Math.Min(minVal, avg.Value); }

                    foreach (var py in p.PrevYears)
                    {
                        double? pavg = py.AvgTempF;
                        if (pavg.HasValue) { maxVal = Math.Max(maxVal, pavg.Value); minVal = Math.Min(minVal, pavg.Value); }
                    }
                }
            }
        }
        else if (MonthData != null)
        {
            foreach (var m in MonthData)
            {
                if (mode == "TemperatureDual")
                {
                    double? hi = m.ThisYearMaxTempF;
                    double? lo = m.ThisYearMinTempF;
                    if (hi.HasValue) { maxVal = Math.Max(maxVal, hi.Value); minVal = Math.Min(minVal, hi.Value); }
                    if (lo.HasValue) { maxVal = Math.Max(maxVal, lo.Value); minVal = Math.Min(minVal, lo.Value); }

                    foreach (var py in m.PrevYears)
                    {
                        double? phi = py.MaxTempF;
                        double? plo = py.MinTempF;
                        if (phi.HasValue) { maxVal = Math.Max(maxVal, phi.Value); minVal = Math.Min(minVal, phi.Value); }
                        if (plo.HasValue) { maxVal = Math.Max(maxVal, plo.Value); minVal = Math.Min(minVal, plo.Value); }
                    }
                }
                else
                {
                    double? avg = m.ThisYearAvgTempF;
                    if (avg.HasValue) { maxVal = Math.Max(maxVal, avg.Value); minVal = Math.Min(minVal, avg.Value); }

                    foreach (var py in m.PrevYears)
                    {
                        double? pavg = py.AvgTempF;
                        if (pavg.HasValue) { maxVal = Math.Max(maxVal, pavg.Value); minVal = Math.Min(minVal, pavg.Value); }
                    }
                }
            }
        }

        if (mode != "WindGust" && mode != "SolarRadiation")
        {
            if (maxVal <= double.MinValue) { minVal = 50; maxVal = 95; }
            minVal = Math.Floor((minVal - 3) / 5) * 5;
            maxVal = Math.Ceiling((maxVal + 3) / 5) * 5;
        }

        // Draw Y Axis and grid lines
        int ticks = 4;
        for (int i = 0; i <= ticks; i++)
        {
            double ratio = (double)i / ticks;
            double val = minVal + (maxVal - minVal) * ratio;
            double y = height - (ratio * height);

            var line = new Line { X1 = 0, Y1 = y, X2 = width, Y2 = y, Stroke = GridBrush, StrokeThickness = 1 };
            GridLinesCanvas.Children.Add(line);

            string tickText = (mode == "WindGust" || mode == "SolarRadiation") ? $"{Math.Round(val):F0}" : $"{Math.Round(val):F0}°";

            var tb = new TextBlock
            {
                Text = tickText,
                FontSize = 10,
                Foreground = AxisTextBrush,
                TextAlignment = TextAlignment.Right,
                Width = 32
            };
            Canvas.SetLeft(tb, 0);
            Canvas.SetTop(tb, Math.Max(0, y - 7));
            YAxisCanvas.Children.Add(tb);
        }

        double slotWidth = width / totalSlots;

        // Find reference index (Day before today for Day mode, Current month for Month mode)
        int refIdx = -1;
        if (isDayMode)
        {
            var dayList = DayData!.ToList();
            refIdx = dayList.FindIndex(d => d.DayOffset == -1);
            if (refIdx == -1) refIdx = dayList.FindIndex(d => d.IsToday || d.DayOffset == 0);
            if (refIdx == -1) refIdx = dayList.Count / 2;
        }
        else if (MonthData != null)
        {
            var monthList = MonthData.ToList();
            refIdx = monthList.FindIndex(m => m.IsCurrent || m.MonthOffset == 0);
            if (refIdx == -1) refIdx = monthList.Count / 2;
        }

        // Draw Past vs Future background shading if enabled (14 Days and 6 Months)
        if (ShowFutureShading && refIdx >= 0 && refIdx < totalSlots)
        {
            double splitX = (refIdx + 0.5) * slotWidth;
            if (splitX < width)
            {
                var forwardRect = new Rectangle
                {
                    Width = width - splitX,
                    Height = height,
                    Fill = ForwardBandBrush
                };
                Canvas.SetLeft(forwardRect, splitX);
                Canvas.SetTop(forwardRect, 0);
                BackgroundBandsCanvas.Children.Add(forwardRect);

                var splitLine = new Rectangle
                {
                    Width = 1.5,
                    Height = height,
                    Fill = Services.ThemeManager.Current.DividerBrush
                };
                Canvas.SetLeft(splitLine, splitX);
                Canvas.SetTop(splitLine, 0);
                BackgroundBandsCanvas.Children.Add(splitLine);
            }
        }

        // Draw Previous Years lines across the FULL window first (so they are in the background)
        int[] years = [2023, 2024, 2025];
        foreach (var yr in years)
        {
            var paleBrush = GetPaleBrush(yr);

            if (mode == "TemperatureDual")
            {
                var maxPts = new PointCollection();
                var minPts = new PointCollection();

                for (int i = 0; i < totalSlots; i++)
                {
                    double x = i * slotWidth + slotWidth / 2;
                    YearPointData? py = null;

                    if (isDayMode)
                    {
                        var d = DayData!.ElementAt(i);
                        py = d.PrevYears.FirstOrDefault(y => y.Year == yr);
                    }
                    else if (MonthData != null)
                    {
                        var m = MonthData.ElementAt(i);
                        py = m.PrevYears.FirstOrDefault(y => y.Year == yr);
                    }

                    if (py != null)
                    {
                        double? hi = isMetric ? py.MaxTempC : py.MaxTempF;
                        double? lo = isMetric ? py.MinTempC : py.MinTempF;
                        if (hi.HasValue) maxPts.Add(new Point(x, height - (hi.Value - minVal) / (maxVal - minVal) * height));
                        if (lo.HasValue) minPts.Add(new Point(x, height - (lo.Value - minVal) / (maxVal - minVal) * height));
                    }
                }

                if (maxPts.Count > 1)
                {
                    ChartCanvas.Children.Add(new Polyline { Points = maxPts, Stroke = paleBrush, StrokeThickness = 1.2 });
                }
                if (minPts.Count > 1)
                {
                    ChartCanvas.Children.Add(new Polyline
                    {
                        Points = minPts,
                        Stroke = paleBrush,
                        StrokeThickness = 1.2,
                        StrokeDashArray = new DoubleCollection { 3, 2 }
                    });
                }
            }
            else if (mode == "WindGust")
            {
                var gustPts = new PointCollection();
                for (int i = 0; i < totalSlots; i++)
                {
                    double x = i * slotWidth + slotWidth / 2;
                    YearPointData? py = null;

                    if (isDayMode)
                    {
                        var d = DayData!.ElementAt(i);
                        py = d.PrevYears.FirstOrDefault(y => y.Year == yr);
                    }
                    else if (MonthData != null)
                    {
                        var m = MonthData.ElementAt(i);
                        py = m.PrevYears.FirstOrDefault(y => y.Year == yr);
                    }

                    if (py != null)
                    {
                        double? pg = isMetric ? py.GustKmh : py.GustMph;
                        if (pg.HasValue)
                        {
                            double y = height - (pg.Value - minVal) / (maxVal - minVal) * height;
                            gustPts.Add(new Point(x, y));
                        }
                    }
                }

                if (gustPts.Count > 1)
                {
                    ChartCanvas.Children.Add(new Polyline { Points = gustPts, Stroke = paleBrush, StrokeThickness = 1.4 });
                }
            }
            else if (mode == "SolarRadiation")
            {
                var solarPts = new PointCollection();
                for (int i = 0; i < totalSlots; i++)
                {
                    double x = i * slotWidth + slotWidth / 2;
                    YearPointData? py = null;

                    if (isDayMode)
                    {
                        var d = DayData!.ElementAt(i);
                        py = d.PrevYears.FirstOrDefault(y => y.Year == yr);
                    }
                    else if (MonthData != null)
                    {
                        var m = MonthData.ElementAt(i);
                        py = m.PrevYears.FirstOrDefault(y => y.Year == yr);
                    }

                    if (py != null && py.SolarRadiationAvg.HasValue)
                    {
                        double y = height - (py.SolarRadiationAvg.Value - minVal) / (maxVal - minVal) * height;
                        solarPts.Add(new Point(x, y));
                    }
                }

                if (solarPts.Count > 1)
                {
                    ChartCanvas.Children.Add(new Polyline { Points = solarPts, Stroke = paleBrush, StrokeThickness = 1.4 });
                }
            }
            else // Average temperature
            {
                var avgPts = new PointCollection();
                for (int i = 0; i < totalSlots; i++)
                {
                    double x = i * slotWidth + slotWidth / 2;
                    YearPointData? py = null;

                    if (isDayMode)
                    {
                        var d = DayData!.ElementAt(i);
                        py = d.PrevYears.FirstOrDefault(y => y.Year == yr);
                    }
                    else if (MonthData != null)
                    {
                        var m = MonthData.ElementAt(i);
                        py = m.PrevYears.FirstOrDefault(y => y.Year == yr);
                    }

                    if (py != null)
                    {
                        double? avg = py.AvgTempF;
                        if (avg.HasValue) avgPts.Add(new Point(x, height - (avg.Value - minVal) / (maxVal - minVal) * height));
                    }
                }

                if (avgPts.Count > 1)
                {
                    ChartCanvas.Children.Add(new Polyline { Points = avgPts, Stroke = paleBrush, StrokeThickness = 1.4 });
                }
            }
        }

        // Draw THIS YEAR lines on top (for the past portion)
        if (mode == "TemperatureDual")
        {
            var thisMaxPts = new PointCollection();
            var thisMinPts = new PointCollection();

            for (int i = 0; i < totalSlots; i++)
            {
                double x = i * slotWidth + slotWidth / 2;
                double? hi = null;
                double? lo = null;

                if (isDayMode)
                {
                    var d = DayData!.ElementAt(i);
                    if (!d.IsUpcoming)
                    {
                        hi = d.ThisYearMaxTempF;
                        lo = d.ThisYearMinTempF;
                    }
                }
                else if (MonthData != null)
                {
                    var m = MonthData.ElementAt(i);
                    if (!m.IsUpcoming)
                    {
                        hi = m.ThisYearMaxTempF;
                        lo = m.ThisYearMinTempF;
                    }
                }

                if (hi.HasValue)
                {
                    double y = height - (hi.Value - minVal) / (maxVal - minVal) * height;
                    thisMaxPts.Add(new Point(x, y));
                    var dot = new Ellipse { Width = 9, Height = 9, Fill = BrightRedBrush };
                    Canvas.SetLeft(dot, x - 4.5); Canvas.SetTop(dot, y - 4.5);
                    ChartCanvas.Children.Add(dot);
                }

                if (lo.HasValue)
                {
                    double y = height - (lo.Value - minVal) / (maxVal - minVal) * height;
                    thisMinPts.Add(new Point(x, y));
                    var dot = new Ellipse { Width = 9, Height = 9, Fill = BrightBlueBrush };
                    Canvas.SetLeft(dot, x - 4.5); Canvas.SetTop(dot, y - 4.5);
                    ChartCanvas.Children.Add(dot);
                }
            }

            if (thisMaxPts.Count > 1)
            {
                ChartCanvas.Children.Add(new Polyline { Points = thisMaxPts, Stroke = BrightRedBrush, StrokeThickness = 3.9 });
            }
            if (thisMinPts.Count > 1)
            {
                ChartCanvas.Children.Add(new Polyline { Points = thisMinPts, Stroke = BrightBlueBrush, StrokeThickness = 3.9 });
            }
        }
        else if (mode == "WindGust")
        {
            var thisGustPts = new PointCollection();
            for (int i = 0; i < totalSlots; i++)
            {
                double x = i * slotWidth + slotWidth / 2;
                double? gust = null;

                if (isDayMode)
                {
                    var d = DayData!.ElementAt(i);
                    if (!d.IsUpcoming) gust = isMetric ? d.ThisYearGustKmh : d.ThisYearGustMph;
                }
                else if (MonthData != null)
                {
                    var m = MonthData.ElementAt(i);
                    if (!m.IsUpcoming) gust = isMetric ? m.ThisYearGustKmh : m.ThisYearGustMph;
                }

                if (gust.HasValue)
                {
                    double y = height - (gust.Value - minVal) / (maxVal - minVal) * height;
                    thisGustPts.Add(new Point(x, y));
                    var dot = new Ellipse { Width = 9, Height = 9, Fill = BoldWindBrush };
                    Canvas.SetLeft(dot, x - 4.5); Canvas.SetTop(dot, y - 4.5);
                    ChartCanvas.Children.Add(dot);
                }
            }

            if (thisGustPts.Count > 1)
            {
                ChartCanvas.Children.Add(new Polyline { Points = thisGustPts, Stroke = BoldWindBrush, StrokeThickness = 3.9 });
            }
        }
        else if (mode == "SolarRadiation")
        {
            var thisSolarPts = new PointCollection();
            for (int i = 0; i < totalSlots; i++)
            {
                double x = i * slotWidth + slotWidth / 2;
                double? sol = null;

                if (isDayMode)
                {
                    var d = DayData!.ElementAt(i);
                    if (!d.IsUpcoming) sol = d.ThisYearSolarRadiationAvg;
                }
                else if (MonthData != null)
                {
                    var m = MonthData.ElementAt(i);
                    if (!m.IsUpcoming) sol = m.ThisYearSolarRadiationAvg;
                }

                if (sol.HasValue)
                {
                    double y = height - (sol.Value - minVal) / (maxVal - minVal) * height;
                    thisSolarPts.Add(new Point(x, y));
                    var dot = new Ellipse { Width = 9, Height = 9, Fill = BoldSolarBrush };
                    Canvas.SetLeft(dot, x - 4.5); Canvas.SetTop(dot, y - 4.5);
                    ChartCanvas.Children.Add(dot);
                }
            }

            if (thisSolarPts.Count > 1)
            {
                ChartCanvas.Children.Add(new Polyline { Points = thisSolarPts, Stroke = BoldSolarBrush, StrokeThickness = 3.9 });
            }
        }
        else // Average temperature
        {
            var thisAvgPts = new PointCollection();
            for (int i = 0; i < totalSlots; i++)
            {
                double x = i * slotWidth + slotWidth / 2;
                double? avg = null;

                if (isDayMode)
                {
                    var d = DayData!.ElementAt(i);
                    if (!d.IsUpcoming) avg = d.ThisYearAvgTempF;
                }
                else if (MonthData != null)
                {
                    var m = MonthData.ElementAt(i);
                    if (!m.IsUpcoming) avg = m.ThisYearAvgTempF;
                }

                if (avg.HasValue)
                {
                    double y = height - (avg.Value - minVal) / (maxVal - minVal) * height;
                    thisAvgPts.Add(new Point(x, y));
                    var dot = new Ellipse { Width = 9, Height = 9, Fill = BoldIndigoBrush };
                    Canvas.SetLeft(dot, x - 4.5); Canvas.SetTop(dot, y - 4.5);
                    ChartCanvas.Children.Add(dot);
                }
            }

            if (thisAvgPts.Count > 1)
            {
                ChartCanvas.Children.Add(new Polyline { Points = thisAvgPts, Stroke = BoldIndigoBrush, StrokeThickness = 3.9 });
            }
        }

        // Add X-Axis labels and hover hit targets
        int todayIdx = isDayMode && DayData != null ? DayData.ToList().FindIndex(x => x.IsToday) : -1;
        if (todayIdx < 0) todayIdx = 0;

        int currentMonthIdx = !isDayMode && MonthData != null ? MonthData.ToList().FindIndex(x => x.IsCurrent) : -1;
        if (currentMonthIdx < 0) currentMonthIdx = 0;

        // Adaptive step to prevent label overlap at small widths
        int dayStep = (int)Math.Max(1, Math.Ceiling(46.0 / slotWidth));
        if (totalSlots > 20 && dayStep < 2) dayStep = 2;

        int monthStep = (int)Math.Max(1, Math.Ceiling(34.0 / slotWidth));

        for (int i = 0; i < totalSlots; i++)
        {
            string labelText = "";
            if (isDayMode)
            {
                var d = DayData!.ElementAt(i);
                if (Math.Abs(i - todayIdx) % dayStep != 0 && !d.IsToday)
                    labelText = "";
                else
                    labelText = d.DisplayDate;
            }
            else if (MonthData != null)
            {
                var m = MonthData.ElementAt(i);
                if (Math.Abs(i - currentMonthIdx) % monthStep != 0 && !m.IsCurrent)
                    labelText = "";
                else
                    labelText = m.DisplayLabel;
            }

            if (!string.IsNullOrEmpty(labelText))
            {
                var tb = new TextBlock
                {
                    Text = labelText,
                    FontSize = 9,
                    Foreground = AxisTextBrush,
                    TextAlignment = TextAlignment.Center,
                    Width = slotWidth * 2
                };
                Canvas.SetLeft(tb, i * slotWidth + slotWidth / 2 - slotWidth);
                Canvas.SetTop(tb, 4);
                XAxisCanvas.Children.Add(tb);
            }

        }
    }

    private void UpdateLegends(string mode)
    {
        if (LegendContainer == null) return;
        LegendContainer.Children.Clear();

        string currentYear = "2026";
        if (DayData != null)
        {
            var firstWithPy = DayData.FirstOrDefault(d => d.PrevYears != null && d.PrevYears.Any());
            if (firstWithPy != null)
            {
                int maxPrevYear = firstWithPy.PrevYears.Max(y => y.Year);
                if (maxPrevYear > 2000)
                {
                    currentYear = (maxPrevYear + 1).ToString();
                }
            }
        }
        else if (MonthData != null)
        {
            var firstWithPy = MonthData.FirstOrDefault(m => m.PrevYears != null && m.PrevYears.Any());
            if (firstWithPy != null)
            {
                int maxPrevYear = firstWithPy.PrevYears.Max(y => y.Year);
                if (maxPrevYear > 2000)
                {
                    currentYear = (maxPrevYear + 1).ToString();
                }
            }
        }

        if (mode == "TemperatureDual")
        {
            AddLegendItem(BrightRedBrush, $"{currentYear} Max", true);
            AddLegendItem(BrightBlueBrush, $"{currentYear} Min", true);
        }
        else if (mode == "WindGust")
        {
            AddLegendItem(BoldWindBrush, currentYear, true);
        }
        else if (mode == "SolarRadiation")
        {
            AddLegendItem(BoldSolarBrush, currentYear, true);
        }
        else
        {
            AddLegendItem(BoldIndigoBrush, currentYear, true);
        }

        AddLegendItem(Pale2025Brush, "2025", false);
        AddLegendItem(Pale2024Brush, "2024", false);
        AddLegendItem(Pale2023Brush, "2023", false);
    }

    private void AddLegendItem(SolidColorBrush brush, string text, bool isBold)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        var box = new Border
        {
            Width = 10,
            Height = 10,
            Background = brush,
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var tb = new TextBlock
        {
            Text = text,
            FontSize = Services.ThemeManager.Current.FontSizeLegend,
            FontWeight = isBold ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = Services.ThemeManager.Current.TextSecondary,
            VerticalAlignment = VerticalAlignment.Center
        };
        sp.Children.Add(box);
        sp.Children.Add(tb);
        LegendContainer.Children.Add(sp);
    }

    private int _lastHoverIndex = -1;
    private Border? _tooltipBorder;
    private StackPanel? _tooltipPanel;
    private Line? _indicatorLine;

    private void EnsureTooltipElements()
    {
        if (_tooltipBorder != null) return;

        _indicatorLine = new Line
        {
            Stroke = new SolidColorBrush(Color.FromArgb(160, 148, 163, 184)),
            StrokeThickness = 1.2,
            StrokeDashArray = new DoubleCollection { 3, 3 },
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };
        OverlayCanvas.Children.Add(_indicatorLine);

        _tooltipPanel = new StackPanel
        {
            Margin = new Thickness(0)
        };

        _tooltipBorder = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromArgb(245, 15, 23, 42)),
            Padding = new Thickness(10, 8, 10, 8),
            Child = _tooltipPanel,
            BorderBrush = new SolidColorBrush(Color.FromArgb(200, 51, 65, 85)),
            BorderThickness = new Thickness(1),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };
        OverlayCanvas.Children.Add(_tooltipBorder);
    }

    private void OnChartGridMouseMove(object sender, MouseEventArgs e)
    {
        bool isDayMode = DayData != null && DayData.Any();
        int totalSlots = isDayMode ? DayData!.Count() : (MonthData != null ? MonthData!.Count() : 0);
        if (totalSlots == 0 || ChartCanvas == null || ChartCanvas.ActualWidth <= 10)
        {
            HideTooltip();
            return;
        }

        Point pos = e.GetPosition(ChartGrid);
        double slotWidth = ChartCanvas.ActualWidth / totalSlots;
        int slotIdx = (int)(pos.X / slotWidth);

        if (slotIdx < 0 || slotIdx >= totalSlots)
        {
            HideTooltip();
            return;
        }

        // Only show tooltips for data with data points and bold lines showing (i.e. usually this year's data)
        string mode = Mode ?? "TemperatureDual";
        bool hasDataPoint = false;

        if (isDayMode)
        {
            var d = DayData!.ElementAt(slotIdx);
            if (!d.IsUpcoming)
            {
                if (mode == "TemperatureDual")
                    hasDataPoint = d.ThisYearMaxTempF.HasValue || d.ThisYearMinTempF.HasValue;
                else if (mode == "WindGust")
                    hasDataPoint = (IsMetric ? d.ThisYearGustKmh : d.ThisYearGustMph).HasValue;
                else if (mode == "SolarRadiation")
                    hasDataPoint = d.ThisYearSolarRadiationAvg.HasValue;
                else
                    hasDataPoint = d.ThisYearAvgTempF.HasValue;
            }
        }
        else if (MonthData != null)
        {
            var m = MonthData.ElementAt(slotIdx);
            if (!m.IsUpcoming)
            {
                if (mode == "TemperatureDual")
                    hasDataPoint = m.ThisYearMaxTempF.HasValue || m.ThisYearMinTempF.HasValue;
                else if (mode == "WindGust")
                    hasDataPoint = (IsMetric ? m.ThisYearGustKmh : m.ThisYearGustMph).HasValue;
                else if (mode == "SolarRadiation")
                    hasDataPoint = m.ThisYearSolarRadiationAvg.HasValue;
                else
                    hasDataPoint = m.ThisYearAvgTempF.HasValue;
            }
        }

        if (!hasDataPoint)
        {
            HideTooltip();
            return;
        }

        if (slotIdx == _lastHoverIndex && _tooltipBorder?.Visibility == Visibility.Visible)
        {
            return;
        }

        _lastHoverIndex = slotIdx;
        UpdateTooltip(slotIdx, isDayMode, mode, IsMetric, slotWidth * slotIdx + slotWidth / 2);
    }

    private void OnChartGridMouseLeave(object sender, MouseEventArgs e)
    {
        HideTooltip();
    }

    private void HideTooltip()
    {
        _lastHoverIndex = -1;
        if (_tooltipBorder != null) _tooltipBorder.Visibility = Visibility.Collapsed;
        if (_indicatorLine != null) _indicatorLine.Visibility = Visibility.Collapsed;
    }

    private void UpdateTooltip(int index, bool isDayMode, string mode, bool isMetric, double xCenter)
    {
        EnsureTooltipElements();
        if (_tooltipPanel == null || _tooltipBorder == null || _indicatorLine == null) return;

        _tooltipPanel.Children.Clear();

        string u = mode == "WindGust" ? (isMetric ? "km/h" : "mph") : "°F";

        if (isDayMode)
        {
            var d = DayData!.ElementAt(index);
            string headerText = $"{d.DisplayDate} ({d.DayOfWeek}) {(d.IsToday ? "• Today" : "• Past")}";
            _tooltipPanel.Children.Add(new TextBlock
            {
                Text = headerText,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 0, 0, 4)
            });

            if (mode == "WindGust" && d.ThisYearGustMph.HasValue)
            {
                double? gust = isMetric ? d.ThisYearGustKmh : d.ThisYearGustMph;
                _tooltipPanel.Children.Add(new TextBlock
                {
                    Text = $"This Year (2026): Peak Gust: {gust:F1} {u}",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = BoldWindBrush,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }
            else if (mode == "TemperatureDual" && (d.ThisYearMaxTempF.HasValue || d.ThisYearMinTempF.HasValue))
            {
                _tooltipPanel.Children.Add(new TextBlock
                {
                    Text = $"This Year (2026): High: {d.ThisYearMaxTempF:F1}{u} | Low: {d.ThisYearMinTempF:F1}{u}",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = BrightRedBrush,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }
            else if (mode == "AverageTemperature" && d.ThisYearAvgTempF.HasValue)
            {
                _tooltipPanel.Children.Add(new TextBlock
                {
                    Text = $"This Year (2026): Avg: {d.ThisYearAvgTempF:F1}{u}",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = BoldIndigoBrush,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }
            else if (mode == "SolarRadiation" && d.ThisYearSolarRadiationAvg.HasValue)
            {
                _tooltipPanel.Children.Add(new TextBlock
                {
                    Text = $"This Year (2026): Avg Solar: {d.ThisYearSolarRadiationAvg.Value:F0} W/m²",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = BoldSolarBrush,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }

            foreach (var py in d.PrevYears.OrderByDescending(y => y.Year))
            {
                if (mode == "WindGust")
                {
                    double? pg = isMetric ? py.GustKmh : py.GustMph;
                    if (pg.HasValue)
                    {
                        _tooltipPanel.Children.Add(new TextBlock
                        {
                            Text = $"{py.Year}: Peak Gust: {pg:F1} {u}",
                            FontSize = 10,
                            Foreground = new SolidColorBrush(Color.FromArgb(255, 203, 213, 225))
                        });
                    }
                }
                else if (mode == "TemperatureDual")
                {
                    if (py.MaxTempF.HasValue || py.MinTempF.HasValue)
                    {
                        _tooltipPanel.Children.Add(new TextBlock
                        {
                            Text = $"{py.Year}: High: {py.MaxTempF:F1}{u} | Low: {py.MinTempF:F1}{u}",
                            FontSize = 10,
                            Foreground = new SolidColorBrush(Color.FromArgb(255, 203, 213, 225))
                        });
                    }
                }
                else if (mode == "SolarRadiation")
                {
                    if (py.SolarRadiationAvg.HasValue)
                    {
                        _tooltipPanel.Children.Add(new TextBlock
                        {
                            Text = $"{py.Year}: Avg Solar: {py.SolarRadiationAvg.Value:F0} W/m²",
                            FontSize = 10,
                            Foreground = new SolidColorBrush(Color.FromArgb(255, 203, 213, 225))
                        });
                    }
                }
                else
                {
                    if (py.AvgTempF.HasValue)
                    {
                        _tooltipPanel.Children.Add(new TextBlock
                        {
                            Text = $"{py.Year}: Avg: {py.AvgTempF:F1}{u}",
                            FontSize = 10,
                            Foreground = new SolidColorBrush(Color.FromArgb(255, 203, 213, 225))
                        });
                    }
                }
            }
        }
        else if (MonthData != null)
        {
            var m = MonthData.ElementAt(index);
            string headerText = $"{m.MonthName} {(m.IsCurrent ? "• Current" : "• Past")}";
            _tooltipPanel.Children.Add(new TextBlock
            {
                Text = headerText,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 0, 0, 4)
            });

            if (mode == "WindGust" && m.ThisYearGustMph.HasValue)
            {
                double? gust = isMetric ? m.ThisYearGustKmh : m.ThisYearGustMph;
                _tooltipPanel.Children.Add(new TextBlock
                {
                    Text = $"This Year (2026): Peak Gust: {gust:F1} {u}",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = BoldWindBrush,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }
            else if (mode == "TemperatureDual" && (m.ThisYearMaxTempF.HasValue || m.ThisYearMinTempF.HasValue))
            {
                _tooltipPanel.Children.Add(new TextBlock
                {
                    Text = $"This Year (2026): High: {m.ThisYearMaxTempF:F1}{u} | Low: {m.ThisYearMinTempF:F1}{u}",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = BrightRedBrush,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }
            else if (mode == "AverageTemperature" && m.ThisYearAvgTempF.HasValue)
            {
                _tooltipPanel.Children.Add(new TextBlock
                {
                    Text = $"This Year (2026): Avg: {m.ThisYearAvgTempF:F1}{u}",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = BoldIndigoBrush,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }
            else if (mode == "SolarRadiation" && m.ThisYearSolarRadiationAvg.HasValue)
            {
                _tooltipPanel.Children.Add(new TextBlock
                {
                    Text = $"This Year (2026): Avg Solar: {m.ThisYearSolarRadiationAvg.Value:F0} W/m²",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = BoldSolarBrush,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }

            foreach (var py in m.PrevYears.OrderByDescending(y => y.Year))
            {
                if (mode == "WindGust")
                {
                    double? pg = isMetric ? py.GustKmh : py.GustMph;
                    if (pg.HasValue)
                    {
                        _tooltipPanel.Children.Add(new TextBlock
                        {
                            Text = $"{py.Year}: Peak Gust: {pg:F1} {u}",
                            FontSize = 10,
                            Foreground = new SolidColorBrush(Color.FromArgb(255, 203, 213, 225))
                        });
                    }
                }
                else if (mode == "TemperatureDual")
                {
                    if (py.MaxTempF.HasValue || py.MinTempF.HasValue)
                    {
                        _tooltipPanel.Children.Add(new TextBlock
                        {
                            Text = $"{py.Year}: High: {py.MaxTempF:F1}{u} | Low: {py.MinTempF:F1}{u}",
                            FontSize = 10,
                            Foreground = new SolidColorBrush(Color.FromArgb(255, 203, 213, 225))
                        });
                    }
                }
                else if (mode == "SolarRadiation")
                {
                    if (py.SolarRadiationAvg.HasValue)
                    {
                        _tooltipPanel.Children.Add(new TextBlock
                        {
                            Text = $"{py.Year}: Avg Solar: {py.SolarRadiationAvg.Value:F0} W/m²",
                            FontSize = 10,
                            Foreground = new SolidColorBrush(Color.FromArgb(255, 203, 213, 225))
                        });
                    }
                }
                else
                {
                    if (py.AvgTempF.HasValue)
                    {
                        _tooltipPanel.Children.Add(new TextBlock
                        {
                            Text = $"{py.Year}: Avg: {py.AvgTempF:F1}{u}",
                            FontSize = 10,
                            Foreground = new SolidColorBrush(Color.FromArgb(255, 203, 213, 225))
                        });
                    }
                }
            }
        }

        double chartH = ChartCanvas.ActualHeight;
        _indicatorLine.X1 = xCenter;
        _indicatorLine.X2 = xCenter;
        _indicatorLine.Y1 = 0;
        _indicatorLine.Y2 = chartH;
        _indicatorLine.Visibility = Visibility.Visible;

        double chartW = ChartCanvas.ActualWidth;
        double left = Math.Max(10, Math.Min(chartW - 210, xCenter - 100));
        Canvas.SetLeft(_tooltipBorder, left);
        Canvas.SetTop(_tooltipBorder, 8);
        _tooltipBorder.Visibility = Visibility.Visible;
    }

    private static SolidColorBrush GetPaleBrush(int year) => year switch
    {
        2025 => Pale2025Brush,
        2024 => Pale2024Brush,
        2023 => Pale2023Brush,
        _ => Pale2023Brush
    };
}
