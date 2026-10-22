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

public partial class MultiYearTimelineBarChart : UserControl
{
    private static readonly SolidColorBrush BoldRainBrush = new(Color.FromArgb(255, 2, 132, 199));     // Azure Blue
    private static readonly SolidColorBrush BoldWindBrush = new(Color.FromArgb(255, 15, 118, 110));    // Teal
    private static readonly SolidColorBrush BoldLightningBrush = new(Color.FromArgb(255, 124, 58, 237)); // Purple

    // Pale, toned down colors matching the temperature chart
    private static readonly SolidColorBrush Pale2025Brush = new(Color.FromArgb(200, 110, 231, 183)); // Pale Emerald
    private static readonly SolidColorBrush Pale2024Brush = new(Color.FromArgb(200, 252, 211, 77));  // Pale Amber
    private static readonly SolidColorBrush Pale2023Brush = new(Color.FromArgb(200, 148, 163, 184)); // Pale Slate

    private static SolidColorBrush GridBrush => Services.ThemeManager.Current.GridLineBrush;
    private static SolidColorBrush AxisTextBrush => Services.ThemeManager.Current.AxisTextBrush;
    private static SolidColorBrush ForwardBandBrush => Services.ThemeManager.Current.FutureBandBrush;

    public static readonly DependencyProperty DayDataProperty =
        DependencyProperty.Register(nameof(DayData), typeof(IEnumerable<DayTimelinePoint>),
            typeof(MultiYearTimelineBarChart), new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty MonthDataProperty =
        DependencyProperty.Register(nameof(MonthData), typeof(IEnumerable<MonthTimelinePoint>),
            typeof(MultiYearTimelineBarChart), new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty MetricProperty =
        DependencyProperty.Register(nameof(Metric), typeof(string),
            typeof(MultiYearTimelineBarChart), new PropertyMetadata("Rain", OnDataChanged));

    public static readonly DependencyProperty IsMetricProperty =
        DependencyProperty.Register(nameof(IsMetric), typeof(bool),
            typeof(MultiYearTimelineBarChart), new PropertyMetadata(false, OnDataChanged));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string),
            typeof(MultiYearTimelineBarChart), new PropertyMetadata("Precipitation Timeline", OnDataChanged));

    public static readonly DependencyProperty ShowFutureShadingProperty =
        DependencyProperty.Register(nameof(ShowFutureShading), typeof(bool),
            typeof(MultiYearTimelineBarChart), new PropertyMetadata(false, OnDataChanged));

    private readonly System.Windows.Threading.DispatcherTimer _resizeTimer;

    public MultiYearTimelineBarChart()
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
        }
        if (ChartUnitBlock != null)
        {
            ChartUnitBlock.Foreground = Services.ThemeManager.Current.TextSecondary;
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

    public string Metric
    {
        get => (string)GetValue(MetricProperty);
        set => SetValue(MetricProperty, value);
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
        if (d is MultiYearTimelineBarChart chart)
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
        string metric = Metric ?? "Rain";

        if (ChartTitleBlock != null) ChartTitleBlock.Text = Title;
        string unitLabel = GetUnitLabel(metric, isMetric);
        if (ChartUnitBlock != null) ChartUnitBlock.Text = $"({unitLabel})";

        UpdateLegends(metric);

        bool isDayMode = DayData != null && DayData.Any();
        int totalSlots = isDayMode ? DayData!.Count() : (MonthData != null ? MonthData!.Count() : 0);
        if (totalSlots == 0) return;

        // Calculate max value across all points and years
        double maxVal = 0.0;
        if (isDayMode)
        {
            foreach (var p in DayData!)
            {
                double? val = ExtractDayVal(p, metric, isMetric, false, 0);
                if (val.HasValue) maxVal = Math.Max(maxVal, val.Value);

                foreach (var py in p.PrevYears)
                {
                    double? pval = ExtractYearVal(py, metric, isMetric);
                    if (pval.HasValue) maxVal = Math.Max(maxVal, pval.Value);
                }
            }
        }
        else if (MonthData != null)
        {
            foreach (var m in MonthData)
            {
                double? val = ExtractMonthVal(m, metric, isMetric);
                if (val.HasValue) maxVal = Math.Max(maxVal, val.Value);

                foreach (var py in m.PrevYears)
                {
                    double? pval = ExtractYearVal(py, metric, isMetric);
                    if (pval.HasValue) maxVal = Math.Max(maxVal, pval.Value);
                }
            }
        }

        if (maxVal <= 0.001)
        {
            maxVal = metric == "Rain" ? (isMetric ? 10.0 : 0.5) : (metric == "Lightning" ? 10 : 20);
        }
        maxVal = Math.Ceiling(maxVal * 1.15 * 10) / 10.0; // 15% headroom

        // Draw Y Axis and grid lines
        int ticks = 4;
        for (int i = 0; i <= ticks; i++)
        {
            double ratio = (double)i / ticks;
            double val = maxVal * ratio;
            double y = height - (ratio * height);

            var line = new Line { X1 = 0, Y1 = y, X2 = width, Y2 = y, Stroke = GridBrush, StrokeThickness = 1 };
            GridLinesCanvas.Children.Add(line);

            string tickText = metric == "Rain"
                ? val.ToString("0.0#", CultureInfo.InvariantCulture)
                : Math.Round(val).ToString("F0", CultureInfo.InvariantCulture);

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

        // 4 Series: This Year (2026), 2025, 2024, 2023
        int totalSeries = 4;
        double barPad = 1.0;
        double barWidth = Math.Max(2.0, (slotWidth - 6) / totalSeries - barPad);

        for (int i = 0; i < totalSlots; i++)
        {
            double slotStartX = i * slotWidth;

            // 1. This year bar (if not upcoming)
            double? thisVal = null;
            bool isUpcoming = false;
            if (isDayMode)
            {
                var d = DayData!.ElementAt(i);
                isUpcoming = d.IsUpcoming;
                if (!isUpcoming) thisVal = ExtractDayVal(d, metric, isMetric, false, 0);
            }
            else if (MonthData != null)
            {
                var m = MonthData.ElementAt(i);
                isUpcoming = m.IsUpcoming;
                if (!isUpcoming) thisVal = ExtractMonthVal(m, metric, isMetric);
            }

            if (thisVal.HasValue && thisVal.Value > 0.001)
            {
                double bh = Math.Max(2, (thisVal.Value / maxVal) * height);
                double bx = slotStartX + 3;
                double by = height - bh;

                var bar = new Rectangle
                {
                    Width = barWidth,
                    Height = bh,
                    Fill = GetThisYearBrush(metric),
                    RadiusX = 1.5,
                    RadiusY = 1.5
                };
                Canvas.SetLeft(bar, bx);
                Canvas.SetTop(bar, by);
                ChartCanvas.Children.Add(bar);
            }

            // 2. Previous years bars (2025, 2024, 2023)
            int[] prevYears = [2025, 2024, 2023];
            for (int yIdx = 0; yIdx < prevYears.Length; yIdx++)
            {
                int yr = prevYears[yIdx];
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
                    double? pval = ExtractYearVal(py, metric, isMetric);
                    if (pval.HasValue && pval.Value > 0.001)
                    {
                        double bh = Math.Max(2, (pval.Value / maxVal) * height);
                        double bx = slotStartX + 3 + (yIdx + 1) * (barWidth + barPad);
                        double by = height - bh;

                        var bar = new Rectangle
                        {
                            Width = barWidth,
                            Height = bh,
                            Fill = GetPaleBrush(yr),
                            RadiusX = 1.5,
                            RadiusY = 1.5
                        };
                        Canvas.SetLeft(bar, bx);
                        Canvas.SetTop(bar, by);
                        ChartCanvas.Children.Add(bar);
                    }
                }
            }

            // X-Axis label
            int todayIdx = isDayMode && DayData != null ? DayData.ToList().FindIndex(x => x.IsToday) : -1;
            if (todayIdx < 0) todayIdx = 0;

            int currentMonthIdx = !isDayMode && MonthData != null ? MonthData.ToList().FindIndex(x => x.IsCurrent) : -1;
            if (currentMonthIdx < 0) currentMonthIdx = 0;

            // Adaptive step to prevent label overlap at small widths
            int dayStep = (int)Math.Max(1, Math.Ceiling(46.0 / slotWidth));
            if (totalSlots > 20 && dayStep < 2) dayStep = 2;

            int monthStep = (int)Math.Max(1, Math.Ceiling(34.0 / slotWidth));

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

            // Hover hit target
            var hitArea = new Rectangle
            {
                Width = slotWidth,
                Height = height,
                Fill = new SolidColorBrush(Colors.Transparent),
                Cursor = Cursors.Hand
            };
            Canvas.SetLeft(hitArea, i * slotWidth);
            Canvas.SetTop(hitArea, 0);

            int slotIdx = i;
            hitArea.MouseEnter += (_, _) => ShowTooltip(slotIdx, isDayMode, metric, isMetric, unitLabel, slotWidth * slotIdx + slotWidth / 2);
            hitArea.MouseLeave += (_, _) => OverlayCanvas.Children.Clear();

            ChartCanvas.Children.Add(hitArea);
        }
    }

    private void UpdateLegends(string metric)
    {
        if (LegendContainer == null) return;
        LegendContainer.Children.Clear();

        AddLegendItem(GetThisYearBrush(metric), "This Year", true);
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
            FontSize = 10,
            FontWeight = isBold ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = Services.ThemeManager.Current.TextSecondary,
            VerticalAlignment = VerticalAlignment.Center
        };
        sp.Children.Add(box);
        sp.Children.Add(tb);
        LegendContainer.Children.Add(sp);
    }

    private void ShowTooltip(int index, bool isDayMode, string metric, bool isMetric, string unitLabel, double xPos)
    {
        OverlayCanvas.Children.Clear();

        var panel = new StackPanel
        {
            Margin = new Thickness(0)
        };
        var border = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromArgb(245, 15, 23, 42)),
            Padding = new Thickness(10, 8, 10, 8),
            Child = panel,
            BorderBrush = new SolidColorBrush(Color.FromArgb(200, 51, 65, 85)),
            BorderThickness = new Thickness(1)
        };

        string headerText = "";
        if (isDayMode)
        {
            var d = DayData!.ElementAt(index);
            headerText = $"{d.DisplayDate} ({d.DayOfWeek}) {(d.IsUpcoming ? "• Upcoming" : (d.IsToday ? "• Today" : "• Past"))}";
            panel.Children.Add(new TextBlock
            {
                Text = headerText,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 0, 0, 4)
            });

            if (!d.IsUpcoming)
            {
                double? val = ExtractDayVal(d, metric, isMetric, false, 0);
                string vStr = val.HasValue ? $"{val.Value:F2} {unitLabel}" : "--";
                if (metric == "Lightning" && d.ThisYearLightningMinDistanceKm.HasValue)
                    vStr += $" (min dist {d.ThisYearLightningMinDistanceKm.Value:F1} km)";

                panel.Children.Add(new TextBlock
                {
                    Text = $"This Year (2026): {vStr}",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = GetThisYearBrush(metric),
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }

            foreach (var py in d.PrevYears.OrderByDescending(y => y.Year))
            {
                double? pval = ExtractYearVal(py, metric, isMetric);
                string vStr = pval.HasValue ? $"{pval.Value:F2} {unitLabel}" : "--";
                if (metric == "Lightning" && py.LightningMinDistanceKm.HasValue)
                    vStr += $" (min dist {py.LightningMinDistanceKm.Value:F1} km)";

                panel.Children.Add(new TextBlock
                {
                    Text = $"{py.Year}: {vStr}",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 203, 213, 225))
                });
            }
        }
        else if (MonthData != null)
        {
            var m = MonthData.ElementAt(index);
            headerText = $"{m.MonthName} {(m.IsUpcoming ? "• Upcoming" : (m.IsCurrent ? "• Current" : "• Past"))}";
            panel.Children.Add(new TextBlock
            {
                Text = headerText,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 0, 0, 4)
            });

            if (!m.IsUpcoming)
            {
                double? val = ExtractMonthVal(m, metric, isMetric);
                string vStr = val.HasValue ? $"{val.Value:F2} {unitLabel}" : "--";
                if (metric == "Lightning" && m.ThisYearLightningMinDistanceKm.HasValue)
                    vStr += $" (min dist {m.ThisYearLightningMinDistanceKm.Value:F1} km)";

                panel.Children.Add(new TextBlock
                {
                    Text = $"This Year (2026): {vStr}",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = GetThisYearBrush(metric),
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }

            foreach (var py in m.PrevYears.OrderByDescending(y => y.Year))
            {
                double? pval = ExtractYearVal(py, metric, isMetric);
                string vStr = pval.HasValue ? $"{pval.Value:F2} {unitLabel}" : "--";
                if (metric == "Lightning" && py.LightningMinDistanceKm.HasValue)
                    vStr += $" (min dist {py.LightningMinDistanceKm.Value:F1} km)";

                panel.Children.Add(new TextBlock
                {
                    Text = $"{py.Year}: {vStr}",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 203, 213, 225))
                });
            }
        }

        double left = Math.Max(10, Math.Min(ChartCanvas.ActualWidth - 210, xPos - 100));
        Canvas.SetLeft(border, left);
        Canvas.SetTop(border, 10);
        OverlayCanvas.Children.Add(border);
    }

    private static SolidColorBrush GetThisYearBrush(string metric) => metric switch
    {
        "Rain" => BoldRainBrush,
        "WindGust" => BoldWindBrush,
        "Lightning" => BoldLightningBrush,
        _ => BoldRainBrush
    };

    private static SolidColorBrush GetPaleBrush(int year) => year switch
    {
        2025 => Pale2025Brush,
        2024 => Pale2024Brush,
        2023 => Pale2023Brush,
        _ => Pale2023Brush
    };

    private static string GetUnitLabel(string metric, bool isMetric) => metric switch
    {
        "Rain" => isMetric ? "mm" : "in",
        "WindGust" => isMetric ? "km/h" : "mph",
        "Lightning" => "strikes",
        _ => ""
    };

    private static double? ExtractDayVal(DayTimelinePoint d, string metric, bool isMetric, bool isPrev, int yr)
    {
        return metric switch
        {
            "Rain" => isMetric ? d.ThisYearRainMm : d.ThisYearRainIn,
            "WindGust" => isMetric ? d.ThisYearGustKmh : d.ThisYearGustMph,
            "Lightning" => d.ThisYearLightningStrikes,
            _ => null
        };
    }

    private static double? ExtractMonthVal(MonthTimelinePoint m, string metric, bool isMetric)
    {
        return metric switch
        {
            "Rain" => isMetric ? m.ThisYearRainMm : m.ThisYearRainIn,
            "WindGust" => isMetric ? m.ThisYearGustKmh : m.ThisYearGustMph,
            "Lightning" => m.ThisYearLightningStrikes,
            _ => null
        };
    }

    private static double? ExtractYearVal(YearPointData py, string metric, bool isMetric)
    {
        return metric switch
        {
            "Rain" => isMetric ? py.RainMm : py.RainIn,
            "WindGust" => isMetric ? py.GustKmh : py.GustMph,
            "Lightning" => py.LightningStrikes,
            _ => null
        };
    }
}
