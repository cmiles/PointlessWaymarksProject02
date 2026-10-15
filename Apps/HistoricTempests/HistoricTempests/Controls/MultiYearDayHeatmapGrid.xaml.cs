using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using HistoricTempests.Models;

namespace HistoricTempests.Controls;

public partial class MultiYearDayHeatmapGrid : UserControl
{
    private static readonly SolidColorBrush NeutralEmptyBg = new(Color.FromArgb(255, 248, 250, 252));
    private static readonly SolidColorBrush NeutralBorder = new(Color.FromArgb(255, 226, 232, 240));
    private static readonly SolidColorBrush FutureBg = new(Color.FromArgb(255, 241, 245, 249));
    private static readonly SolidColorBrush FutureBorder = new(Color.FromArgb(255, 203, 213, 225));
    private static readonly SolidColorBrush ForwardBandBrush = new(Color.FromArgb(12, 100, 116, 139));
    private static readonly SolidColorBrush AxisTextBrush = new(Color.FromArgb(255, 100, 116, 139));
    private static readonly SolidColorBrush TodayAccentBrush = new(Color.FromArgb(255, 2, 132, 199));

    public static readonly DependencyProperty DayDataProperty =
        DependencyProperty.Register(nameof(DayData), typeof(IEnumerable<DayTimelinePoint>),
            typeof(MultiYearDayHeatmapGrid), new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty MonthDataProperty =
        DependencyProperty.Register(nameof(MonthData), typeof(IEnumerable<MonthTimelinePoint>),
            typeof(MultiYearDayHeatmapGrid), new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty MetricProperty =
        DependencyProperty.Register(nameof(Metric), typeof(string),
            typeof(MultiYearDayHeatmapGrid), new PropertyMetadata("Rain", OnDataChanged));

    public static readonly DependencyProperty IsMetricProperty =
        DependencyProperty.Register(nameof(IsMetric), typeof(bool),
            typeof(MultiYearDayHeatmapGrid), new PropertyMetadata(false, OnDataChanged));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string),
            typeof(MultiYearDayHeatmapGrid), new PropertyMetadata("Daily Precipitation Grid", OnDataChanged));

    public static readonly DependencyProperty ShowFutureShadingProperty =
        DependencyProperty.Register(nameof(ShowFutureShading), typeof(bool),
            typeof(MultiYearDayHeatmapGrid), new PropertyMetadata(false, OnDataChanged));

    public static readonly DependencyProperty IsInvertedProperty =
        DependencyProperty.Register(nameof(IsInverted), typeof(bool),
            typeof(MultiYearDayHeatmapGrid), new PropertyMetadata(false, OnDataChanged));

    private readonly System.Windows.Threading.DispatcherTimer _resizeTimer;

    public MultiYearDayHeatmapGrid()
    {
        InitializeComponent();
        _resizeTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(75) };
        _resizeTimer.Tick += (s, e) =>
        {
            _resizeTimer.Stop();
            if (IsInverted && LegendContainer != null)
            {
                bool stack = ActualWidth > 0 && ActualWidth < 720;
                LegendContainer.Orientation = stack ? Orientation.Vertical : Orientation.Horizontal;
            }
            RenderChart();
        };

        Loaded += (_, _) => RenderChart();
        SizeChanged += (s, e) =>
        {
            _resizeTimer.Stop();
            _resizeTimer.Start();
        };
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

    public bool IsInverted
    {
        get => (bool)GetValue(IsInvertedProperty);
        set => SetValue(IsInvertedProperty, value);
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MultiYearDayHeatmapGrid grid)
        {
            grid.RenderChart();
        }
    }

    public void RenderChart()
    {
        if (IsInverted)
        {
            if (HorizontalLayoutGrid != null) HorizontalLayoutGrid.Visibility = Visibility.Collapsed;
            if (InvertedLayoutGrid != null) InvertedLayoutGrid.Visibility = Visibility.Visible;
            RenderInvertedGrid();
            return;
        }

        if (HorizontalLayoutGrid != null) HorizontalLayoutGrid.Visibility = Visibility.Visible;
        if (InvertedLayoutGrid != null) InvertedLayoutGrid.Visibility = Visibility.Collapsed;

        if (GridCanvas == null || YAxisCanvas == null || XAxisCanvas == null) return;

        GridCanvas.Children.Clear();
        YAxisCanvas.Children.Clear();
        XAxisCanvas.Children.Clear();
        if (LegendContainer != null) LegendContainer.Children.Clear();

        double width = GridCanvas.ActualWidth;
        double height = GridCanvas.ActualHeight;
        if (width <= 10 || height <= 10) return;

        bool isMetric = IsMetric;
        string metric = Metric ?? "Rain";

        if (ChartTitleBlock != null) ChartTitleBlock.Text = Title;
        if (ChartUnitBlock != null)
        {
            ChartUnitBlock.Text = metric == "Combined"
                ? ""
                : (metric == "Rain" ? (isMetric ? "(mm)" : "(in)") : "(strikes)");
        }

        var slotList = new List<HeatmapSlot>();
        if (DayData != null && DayData.Any())
        {
            foreach (var d in DayData)
            {
                slotList.Add(new HeatmapSlot
                {
                    DisplayLabel = d.DisplayDate,
                    SubLabel = d.DayOfWeek,
                    IsReference = d.DayOffset == -1,
                    IsToday = d.IsToday || d.DayOffset == 0,
                    IsUpcoming = d.IsUpcoming,
                    ThisYearRainIn = d.ThisYearRainIn,
                    ThisYearRainMm = d.ThisYearRainMm,
                    ThisYearLightningStrikes = d.ThisYearLightningStrikes,
                    PrevYears = d.PrevYears
                });
            }
        }
        else if (MonthData != null && MonthData.Any())
        {
            foreach (var m in MonthData)
            {
                slotList.Add(new HeatmapSlot
                {
                    DisplayLabel = !string.IsNullOrEmpty(m.DisplayLabel) ? m.DisplayLabel : m.MonthName,
                    SubLabel = m.MonthName,
                    IsReference = m.IsCurrent || m.MonthOffset == 0,
                    IsToday = m.IsCurrent || m.MonthOffset == 0,
                    IsUpcoming = m.IsUpcoming,
                    ThisYearRainIn = m.ThisYearRainIn,
                    ThisYearRainMm = m.ThisYearRainMm,
                    ThisYearLightningStrikes = m.ThisYearLightningStrikes,
                    PrevYears = m.PrevYears
                });
            }
        }

        if (!slotList.Any()) return;
        int totalSlots = slotList.Count;

        // Years to display on Y-axis
        int[] years = [2026, 2025, 2024, 2023];

        // Determine reference (Day before today or Current Month) index
        int refIdx = slotList.FindIndex(s => s.IsReference);
        if (refIdx == -1) refIdx = slotList.FindIndex(s => s.IsToday);
        if (refIdx == -1) refIdx = totalSlots / 2;

        // Calculate max value across all points and years to calibrate color scale
        double maxRainVal = 0.0;
        double maxLightningVal = 0.0;
        foreach (var s in slotList)
        {
            if (!s.IsUpcoming)
            {
                double? rv = isMetric ? s.ThisYearRainMm : s.ThisYearRainIn;
                if (rv.HasValue && rv.Value > maxRainVal) maxRainVal = rv.Value;
                if (s.ThisYearLightningStrikes.HasValue && s.ThisYearLightningStrikes.Value > maxLightningVal)
                    maxLightningVal = s.ThisYearLightningStrikes.Value;
            }

            foreach (var py in s.PrevYears)
            {
                double? prv = isMetric ? py.RainMm : py.RainIn;
                if (prv.HasValue && prv.Value > maxRainVal) maxRainVal = prv.Value;
                if (py.LightningStrikes > maxLightningVal)
                    maxLightningVal = py.LightningStrikes;
            }
        }

        if (maxRainVal <= 0.001) maxRainVal = isMetric ? 10.0 : 0.5;
        if (maxLightningVal <= 0.001) maxLightningVal = 50.0;

        // Render Legend
        RenderLegend(metric, isMetric, maxRainVal, maxLightningVal);

        // Dimensions & Gaps
        double colGap = 2.0;
        double subRowGap = 2.0;
        double yearGap = 24.0;
        int numYears = years.Length;
        int rowsPerYear = metric == "Combined" ? 2 : 1;
        int totalRows = numYears * rowsPerYear;

        double totalGapSpace = numYears * (rowsPerYear - 1) * subRowGap + (numYears - 1) * yearGap;
        double availableHeight = height - totalGapSpace;
        double rowHeight = Math.Max(16.0, availableHeight / totalRows);
        double colWidth = (width - (totalSlots - 1) * colGap) / totalSlots;

        // Full chart background (Pure White)
        var whiteBand = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = Brushes.White
        };
        Canvas.SetLeft(whiteBand, 0);
        Canvas.SetTop(whiteBand, 0);
        GridCanvas.Children.Add(whiteBand);

        // Draw Past vs Future background shading if enabled (14 Days and 6 Months)
        if (ShowFutureShading && refIdx >= 0 && refIdx < totalSlots)
        {
            double splitX = (refIdx + 1) * (colWidth + colGap) - (colGap / 2.0);
            if (splitX < width)
            {
                var forwardRect = new Rectangle
                {
                    Width = width - splitX,
                    Height = height,
                    Fill = new SolidColorBrush(Color.FromArgb(255, 241, 245, 249)) // #F1F5F9 Slate 100
                };
                Canvas.SetLeft(forwardRect, splitX);
                Canvas.SetTop(forwardRect, 0);
                GridCanvas.Children.Add(forwardRect);

                var splitLine = new Rectangle
                {
                    Width = 1.5,
                    Height = height,
                    Fill = new SolidColorBrush(Color.FromArgb(255, 203, 213, 225)) // Slate 300
                };
                Canvas.SetLeft(splitLine, splitX);
                Canvas.SetTop(splitLine, 0);
                GridCanvas.Children.Add(splitLine);
            }
        }

        // Layout rows tracking: (Year, SubMetric, Y)
        var layoutRows = new List<(int Year, string SubMetric, double Y)>();
        double currentY = 0.0;

        for (int yi = 0; yi < numYears; yi++)
        {
            int yr = years[yi];
            double yearStartY = currentY;
            var subRowList = new List<(string SubMetric, double Y)>();

            if (metric == "Combined")
            {
                subRowList.Add(("Rain", currentY));
                layoutRows.Add((yr, "Rain", currentY));
                currentY += rowHeight + subRowGap;

                subRowList.Add(("Lightning", currentY));
                layoutRows.Add((yr, "Lightning", currentY));
                currentY += rowHeight;
            }
            else
            {
                subRowList.Add((metric, currentY));
                layoutRows.Add((yr, metric, currentY));
                currentY += rowHeight;
            }

            double yearEndY = currentY;
            double yearTotalHeight = yearEndY - yearStartY;

            // Render Year on Y-Axis: list the year ONCE without a color (neutral dark slate)
            var yrText = new TextBlock
            {
                Text = yr.ToString(),
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 30, 41, 59)), // Slate 800 (#1E293B) - neutral
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Left
            };
            double yrTextTop = yearStartY + (yearTotalHeight - 16) / 2.0;
            Canvas.SetLeft(yrText, 4);
            Canvas.SetTop(yrText, Math.Max(0, yrTextTop));
            YAxisCanvas.Children.Add(yrText);

            // Render the rain and lightning symbols in their respective colors
            foreach (var (subMetric, subY) in subRowList)
            {
                bool isRain = subMetric == "Rain";
                var symbolBorder = new Border
                {
                    Width = 24,
                    Height = Math.Max(18, Math.Min(rowHeight - 2, 22)),
                    CornerRadius = new CornerRadius(4),
                    Background = isRain
                        ? new SolidColorBrush(Color.FromArgb(255, 239, 246, 255)) // Blue 50
                        : new SolidColorBrush(Color.FromArgb(255, 254, 243, 199)), // Amber 50
                    BorderBrush = isRain
                        ? new SolidColorBrush(Color.FromArgb(255, 191, 219, 254)) // Blue 200
                        : new SolidColorBrush(Color.FromArgb(255, 253, 230, 138)), // Amber 200
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(0)
                };

                var symbolIcon = new TextBlock
                {
                    Text = isRain ? "🌧" : "⚡",
                    FontSize = 11,
                    Foreground = isRain
                        ? new SolidColorBrush(Color.FromArgb(255, 29, 78, 216)) // Blue 700
                        : new SolidColorBrush(Color.FromArgb(255, 180, 83, 9)),  // Amber 700
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                symbolBorder.Child = symbolIcon;
                ToolTipService.SetToolTip(symbolBorder, isRain ? $"{yr} Precipitation (Rain)" : $"{yr} Lightning Strikes");

                double symbolTop = subY + (rowHeight - symbolBorder.Height) / 2.0;
                Canvas.SetLeft(symbolBorder, 44);
                Canvas.SetTop(symbolBorder, symbolTop);
                YAxisCanvas.Children.Add(symbolBorder);
            }

            // If not the last year, add space and draw horizontal divider line between years
            if (yi < numYears - 1)
            {
                double dividerY = yearEndY + yearGap / 2.0;

                // Horizontal line on GridCanvas across all cells
                var gridDivider = new Rectangle
                {
                    Width = width,
                    Height = 1.0,
                    Fill = new SolidColorBrush(Color.FromArgb(255, 203, 213, 225)) // Slate 300
                };
                Canvas.SetLeft(gridDivider, 0);
                Canvas.SetTop(gridDivider, Math.Round(dividerY));
                GridCanvas.Children.Add(gridDivider);

                // Horizontal line on YAxisCanvas
                var yAxisDivider = new Rectangle
                {
                    Width = 68,
                    Height = 1.0,
                    Fill = new SolidColorBrush(Color.FromArgb(255, 203, 213, 225)) // Slate 300
                };
                Canvas.SetLeft(yAxisDivider, 4);
                Canvas.SetTop(yAxisDivider, Math.Round(dividerY));
                YAxisCanvas.Children.Add(yAxisDivider);

                currentY += yearGap;
            }
        }

        // Render Grid Cells
        foreach (var (yr, subMetric, y) in layoutRows)
        {
            for (int c = 0; c < totalSlots; c++)
            {
                var s = slotList[c];
                double x = c * (colWidth + colGap);
                double? val = null;
                if (yr == 2026)
                {
                    val = subMetric == "Rain"
                        ? (isMetric ? s.ThisYearRainMm : s.ThisYearRainIn)
                        : s.ThisYearLightningStrikes;
                }
                else
                {
                    var py = s.PrevYears.FirstOrDefault(p => p.Year == yr);
                    if (py != null)
                    {
                        val = subMetric == "Rain"
                            ? (isMetric ? py.RainMm : py.RainIn)
                            : py.LightningStrikes;
                    }
                    else
                    {
                        val = null;
                    }
                }

                // If data is missing (e.g. today or future or unrecorded), do not show any square
                if (!val.HasValue)
                {
                    continue;
                }

                var cellBorder = new Border
                {
                    Width = colWidth,
                    Height = rowHeight,
                    CornerRadius = new CornerRadius(3)
                };

                bool isForward = (c > refIdx);

                if (!val.HasValue || val.Value <= 0.0001)
                {
                    cellBorder.Background = (ShowFutureShading && isForward)
                        ? new SolidColorBrush(Color.FromArgb(255, 241, 245, 249))
                        : Brushes.White;
                    cellBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 226, 232, 240));
                    cellBorder.BorderThickness = new Thickness(1);
                    string zeroLabel = subMetric == "Rain" ? $"0 {(isMetric ? "mm" : "in")}" : "0 strikes";
                    ToolTipService.SetToolTip(cellBorder, $"{s.SubLabel}, {s.DisplayLabel} ({yr}) {subMetric}: {zeroLabel}");
                }
                else
                {
                    double rawVal = val.Value;
                    double activeMax = subMetric == "Rain" ? maxRainVal : maxLightningVal;
                    var (fill, border, isDark) = GetCellBrush(subMetric, rawVal, activeMax);

                    cellBorder.Background = fill;
                    cellBorder.BorderBrush = border;
                    cellBorder.BorderThickness = new Thickness(1.2);

                    string valText = subMetric == "Rain"
                        ? (isMetric ? rawVal.ToString("0.#", CultureInfo.InvariantCulture) : (rawVal < 0.1 ? rawVal.ToString(".00", CultureInfo.InvariantCulture) : rawVal.ToString("0.0#", CultureInfo.InvariantCulture)))
                        : (rawVal >= 1000 ? (rawVal / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + "k" : Math.Round(rawVal).ToString("F0", CultureInfo.InvariantCulture));

                    string unit = subMetric == "Rain" ? (isMetric ? "mm" : "in") : "strikes";
                    string formattedTooltip = subMetric == "Rain"
                        ? $"{s.SubLabel}, {s.DisplayLabel} ({yr}) Rain: {rawVal:F2} {unit}"
                        : $"{s.SubLabel}, {s.DisplayLabel} ({yr}) Lightning: {Math.Round(rawVal):N0} {unit}";
                    ToolTipService.SetToolTip(cellBorder, formattedTooltip);

                    if (colWidth >= 20)
                    {
                        Brush textFg;
                        if (subMetric == "Lightning")
                        {
                            textFg = isDark ? Brushes.White : new SolidColorBrush(Color.FromArgb(255, 69, 26, 3));
                        }
                        else
                        {
                            textFg = isDark ? Brushes.White : new SolidColorBrush(Color.FromArgb(255, 15, 23, 42));
                        }

                        var textBlock = new TextBlock
                        {
                            Text = valText,
                            FontSize = colWidth >= 28 ? 9 : 8,
                            FontWeight = FontWeights.Bold,
                            Foreground = textFg,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        cellBorder.Child = textBlock;
                    }
                }

                Canvas.SetLeft(cellBorder, x);
                Canvas.SetTop(cellBorder, y);
                GridCanvas.Children.Add(cellBorder);
            }
        }

        // Render X-Axis Date/Month Labels
        if (totalSlots > 60)
        {
            // Full Year / Multi-Month: show month headers (Jan, Feb, Mar...)
            double lastMonthX = -999.0;
            for (int c = 0; c < totalSlots; c++)
            {
                var s = slotList[c];
                bool isMonthStart = s.DisplayLabel.EndsWith(" 1") || s.DisplayLabel == "Jan 1" || c == 0;
                if (!isMonthStart) continue;

                double x = c * (colWidth + colGap);
                // Ensure month labels don't collide on small screens: require at least 32px between headers
                if (lastMonthX >= 0 && (x - lastMonthX < 32.0)) continue;

                string monthLabel = s.DisplayLabel.Split(' ')[0]; // "Jan", "Feb", etc.
                double slotPitch = colWidth + colGap;
                if (colWidth < 2.0 && slotPitch * 30 < 45)
                {
                    // Space is tight on small screens: use single letter abbreviation
                    monthLabel = monthLabel.Length > 0 ? monthLabel.Substring(0, 1) : monthLabel;
                }

                var tb = new TextBlock
                {
                    Text = monthLabel,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = AxisTextBrush,
                    TextAlignment = TextAlignment.Left,
                    Width = 45
                };
                Canvas.SetLeft(tb, x);
                Canvas.SetTop(tb, 3);
                XAxisCanvas.Children.Add(tb);

                var tick = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = 5,
                    Stroke = new SolidColorBrush(Color.FromArgb(255, 148, 163, 184)),
                    StrokeThickness = 1.2
                };
                XAxisCanvas.Children.Add(tick);

                lastMonthX = x;
            }
        }
        else
        {
            int anchorIdx = slotList.FindIndex(s => s.IsToday);
            if (anchorIdx < 0) anchorIdx = slotList.FindIndex(s => s.IsReference);
            if (anchorIdx < 0) anchorIdx = 0;

            double slotPitch = colWidth + colGap;
            // Adaptive step: on small screens ensure step >= 2 so adjacent labels are never shown
            int labelStep = (int)Math.Max(1, Math.Ceiling(46.0 / Math.Max(1.0, slotPitch)));
            if (totalSlots > 12 && labelStep < 2)
            {
                labelStep = 2;
            }

            int lastPlacedCol = -999;
            double lastPlacedX = -999.0;

            for (int c = 0; c < totalSlots; c++)
            {
                var s = slotList[c];
                bool isAnchor = (c == anchorIdx);
                bool isStepSlot = (Math.Abs(c - anchorIdx) % labelStep == 0);
                bool isLastSlot = (c == totalSlots - 1);

                if (!isAnchor && !isStepSlot && !isLastSlot) continue;

                double x = c * slotPitch;

                // CRITICAL: On small screens, never use adjacent labels (they collide).
                if (lastPlacedCol >= 0 && (c - lastPlacedCol <= 1))
                {
                    continue;
                }
                // Also ensure minimum horizontal distance in pixels to prevent collisions
                if (lastPlacedX >= 0 && (x - lastPlacedX < 36.0))
                {
                    continue;
                }

                double labelW = Math.Max(colWidth * labelStep, 38);
                bool isHighlighted = s.IsToday;
                var tb = new TextBlock
                {
                    Text = s.DisplayLabel,
                    FontSize = 9.5,
                    FontWeight = isHighlighted ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = isHighlighted ? TodayAccentBrush : AxisTextBrush,
                    TextAlignment = TextAlignment.Center,
                    Width = labelW
                };

                Canvas.SetLeft(tb, x + (colWidth - labelW) / 2);
                Canvas.SetTop(tb, 2);
                XAxisCanvas.Children.Add(tb);

                lastPlacedCol = c;
                lastPlacedX = x;
            }
        }
    }

    private static (SolidColorBrush fill, SolidColorBrush border, bool isDark) GetCellBrush(string metric, double val, double maxVal)
    {
        if (metric == "Rain")
        {
            double ratio = Math.Sqrt(Math.Clamp(val / maxVal, 0.0, 1.0));

            byte r = (byte)(186 + (30 - 186) * ratio);
            byte g = (byte)(230 + (58 - 230) * ratio);
            byte b = (byte)(253 + (138 - 253) * ratio);

            byte br = (byte)Math.Max(0, r - 35);
            byte bg = (byte)Math.Max(0, g - 45);
            byte bb = (byte)Math.Max(0, b - 20);

            var fillBrush = new SolidColorBrush(Color.FromArgb(255, r, g, b));
            var borderBrush = new SolidColorBrush(Color.FromArgb(255, br, bg, bb));
            bool isDark = ratio >= 0.55;

            return (fillBrush, borderBrush, isDark);
        }
        else
        {
            double ratio = Math.Pow(Math.Clamp(val / maxVal, 0.0, 1.0), 0.42);

            byte r = (byte)(254 + (180 - 254) * ratio);
            byte g = (byte)(240 + (83 - 240) * ratio);
            byte b = (byte)(138 + (9 - 138) * ratio);

            byte br = (byte)Math.Max(0, r - 45);
            byte bg = (byte)Math.Max(0, g - 65);
            byte bb = (byte)Math.Max(0, b - 20);

            var fillBrush = new SolidColorBrush(Color.FromArgb(255, r, g, b));
            var borderBrush = new SolidColorBrush(Color.FromArgb(255, br, bg, bb));
            bool isDark = ratio >= 0.65;

            return (fillBrush, borderBrush, isDark);
        }
    }

    private void RenderLegend(string metric, bool isMetric, double maxRainVal, double maxLightningVal)
    {
        if (LegendContainer == null) return;
        LegendContainer.Children.Clear();

        double availableWidth = ActualWidth > 0 ? ActualWidth : (Parent is FrameworkElement p && p.ActualWidth > 0 ? p.ActualWidth : 1000);
        bool stackVertically = IsInverted && availableWidth < 720;
        LegendContainer.Orientation = stackVertically ? Orientation.Vertical : Orientation.Horizontal;

        if (metric == "Combined")
        {
            // Rain scale group
            var rainGroup = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = stackVertically ? new Thickness(0, 1, 0, 4) : new Thickness(0, 2, 16, 2)
            };

            var rainTitle = new TextBlock
            {
                Text = isMetric ? "🌧 Rain (mm):" : "🌧 Rain (in):",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = AxisTextBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            rainGroup.Children.Add(rainTitle);
            AddLegendSwatch(Brushes.White, NeutralBorder, "0", false, false, rainGroup);
            double[] rainSteps = isMetric ? [0.5, 2.5, 6.0, 15.0] : [0.02, 0.15, 0.35, 0.75];
            string[] rainLabels = isMetric ? [">0", "2.5", "6", "15+"] : [">0", ".15", ".35", ".75+"];
            for (int i = 0; i < rainSteps.Length; i++)
            {
                var (f, b, isDark) = GetCellBrush("Rain", rainSteps[i], maxRainVal);
                AddLegendSwatch(f, b, rainLabels[i], isDark, false, rainGroup);
            }
            LegendContainer.Children.Add(rainGroup);

            // Lightning scale group
            var lightGroup = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = stackVertically ? new Thickness(0, 1, 0, 2) : new Thickness(0, 2, 0, 2)
            };

            var lightTitle = new TextBlock
            {
                Text = "⚡ Strikes:",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = AxisTextBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            lightGroup.Children.Add(lightTitle);
            AddLegendSwatch(Brushes.White, NeutralBorder, "0", false, true, lightGroup);
            double[] lightSteps = [2.0, 25.0, 80.0, 250.0];
            string[] lightLabels = ["1+", "25", "80", "250+"];
            for (int i = 0; i < lightSteps.Length; i++)
            {
                var (f, b, isDark) = GetCellBrush("Lightning", lightSteps[i], maxLightningVal);
                AddLegendSwatch(f, b, lightLabels[i], isDark, true, lightGroup);
            }
            LegendContainer.Children.Add(lightGroup);
        }
        else
        {
            var singleGroup = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2)
            };

            var titleBlock = new TextBlock
            {
                Text = "Scale: ",
                FontSize = 10,
                Foreground = AxisTextBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            singleGroup.Children.Add(titleBlock);

            AddLegendSwatch(Brushes.White, NeutralBorder, "0", false, metric == "Lightning", singleGroup);

            if (metric == "Rain")
            {
                double[] stepVals = isMetric ? [0.5, 2.5, 6.0, 15.0] : [0.02, 0.15, 0.35, 0.75];
                string[] stepLabels = isMetric ? [">0", "2.5", "6", "15+"] : [">0", ".15", ".35", ".75+"];
                for (int i = 0; i < stepVals.Length; i++)
                {
                    var (f, b, isDark) = GetCellBrush("Rain", stepVals[i], maxRainVal);
                    AddLegendSwatch(f, b, stepLabels[i], isDark, false, singleGroup);
                }
            }
            else
            {
                double[] stepVals = [2.0, 25.0, 80.0, 250.0];
                string[] stepLabels = ["1+", "25", "80", "250+"];
                for (int i = 0; i < stepVals.Length; i++)
                {
                    var (f, b, isDark) = GetCellBrush("Lightning", stepVals[i], maxLightningVal);
                    AddLegendSwatch(f, b, stepLabels[i], isDark, true, singleGroup);
                }
            }
            LegendContainer.Children.Add(singleGroup);
        }
    }

    private void AddLegendSwatch(Brush fill, Brush border, string label, bool isDark, bool isLightning, Panel? target = null)
    {
        var panel = target ?? LegendContainer;
        if (panel == null) return;

        var swatchBorder = new Border
        {
            Width = 26,
            Height = 15,
            CornerRadius = new CornerRadius(2),
            Background = fill,
            BorderBrush = border,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(2, 0, 2, 0)
        };

        Brush textFg;
        if (isLightning)
        {
            textFg = isDark ? Brushes.White : new SolidColorBrush(Color.FromArgb(255, 69, 26, 3));
        }
        else
        {
            textFg = isDark ? Brushes.White : new SolidColorBrush(Color.FromArgb(255, 15, 23, 42));
        }

        var tb = new TextBlock
        {
            Text = label,
            FontSize = 8,
            FontWeight = FontWeights.Bold,
            Foreground = textFg,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        swatchBorder.Child = tb;

        panel.Children.Add(swatchBorder);
    }

    private void RenderInvertedGrid()
    {
        if (InvertedLayoutGrid == null || InvertedCanvasContainer == null) return;

        if (LegendContainer != null) LegendContainer.Children.Clear();

        bool isMetric = IsMetric;
        string metric = Metric ?? "Combined";

        if (ChartTitleBlock != null) ChartTitleBlock.Text = Title;
        if (ChartUnitBlock != null) ChartUnitBlock.Text = "";

        var slotList = new List<HeatmapSlot>();
        if (DayData != null && DayData.Any())
        {
            foreach (var d in DayData)
            {
                slotList.Add(new HeatmapSlot
                {
                    DisplayLabel = d.DisplayDate,
                    SubLabel = d.DayOfWeek,
                    IsReference = d.DayOffset == -1,
                    IsToday = d.IsToday || d.DayOffset == 0,
                    IsUpcoming = d.IsUpcoming,
                    ThisYearRainIn = d.ThisYearRainIn,
                    ThisYearRainMm = d.ThisYearRainMm,
                    ThisYearLightningStrikes = d.ThisYearLightningStrikes,
                    PrevYears = d.PrevYears
                });
            }
        }
        else if (MonthData != null && MonthData.Any())
        {
            foreach (var m in MonthData)
            {
                slotList.Add(new HeatmapSlot
                {
                    DisplayLabel = !string.IsNullOrEmpty(m.DisplayLabel) ? m.DisplayLabel : m.MonthName,
                    SubLabel = m.MonthName,
                    IsReference = m.IsCurrent || m.MonthOffset == 0,
                    IsToday = m.IsCurrent || m.MonthOffset == 0,
                    IsUpcoming = m.IsUpcoming,
                    ThisYearRainIn = m.ThisYearRainIn,
                    ThisYearRainMm = m.ThisYearRainMm,
                    ThisYearLightningStrikes = m.ThisYearLightningStrikes,
                    PrevYears = m.PrevYears
                });
            }
        }

        if (!slotList.Any()) return;

        int[] years = [2026, 2025, 2024, 2023];

        // Max values for legend and coloring
        double maxRainVal = 0.0;
        double maxLightningVal = 0.0;
        foreach (var s in slotList)
        {
            double? rv = isMetric ? s.ThisYearRainMm : s.ThisYearRainIn;
            if (rv.HasValue && rv.Value > maxRainVal) maxRainVal = rv.Value;
            if (s.ThisYearLightningStrikes.HasValue && s.ThisYearLightningStrikes.Value > maxLightningVal)
                maxLightningVal = s.ThisYearLightningStrikes.Value;

            foreach (var py in s.PrevYears)
            {
                double? prv = isMetric ? py.RainMm : py.RainIn;
                if (prv.HasValue && prv.Value > maxRainVal) maxRainVal = prv.Value;
                if (py.LightningStrikes > maxLightningVal)
                    maxLightningVal = py.LightningStrikes;
            }
        }

        if (maxRainVal <= 0.001) maxRainVal = isMetric ? 10.0 : 0.5;
        if (maxLightningVal <= 0.001) maxLightningVal = 50.0;

        RenderLegend("Combined", isMetric, maxRainVal, maxLightningVal);

        // Build compact JSON payload for hardware-accelerated canvas
        var sb = new System.Text.StringBuilder(16384);
        sb.Append("{\"years\":[2026,2025,2024,2023],");
        sb.AppendFormat(CultureInfo.InvariantCulture, "\"isMetric\":{0},\"maxRain\":{1:F2},\"maxLight\":{2:F0},\"slots\":[",
            isMetric ? "true" : "false", maxRainVal, maxLightningVal);

        for (int i = 0; i < slotList.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var s = slotList[i];
            string monthPart = s.DisplayLabel.Contains(' ') ? s.DisplayLabel.Split(' ')[0] : (s.SubLabel ?? "");

            sb.Append("{\"d\":\"").Append(s.DisplayLabel).Append("\",");
            sb.Append("\"m\":\"").Append(monthPart).Append("\",");
            sb.Append("\"ref\":").Append(s.IsReference ? "true" : "false").Append(",\"y\":{");

            // 2026
            sb.Append("\"2026\":{");
            double? r26 = isMetric ? s.ThisYearRainMm : s.ThisYearRainIn;
            double? l26 = s.ThisYearLightningStrikes;
            if (r26.HasValue) sb.AppendFormat(CultureInfo.InvariantCulture, "\"r\":{0:F2},", r26.Value);
            else sb.Append("\"r\":null,");
            if (l26.HasValue) sb.AppendFormat(CultureInfo.InvariantCulture, "\"l\":{0:F0}", l26.Value);
            else sb.Append("\"l\":null");
            sb.Append('}');

            // Previous years
            for (int yi = 1; yi < years.Length; yi++)
            {
                int yr = years[yi];
                sb.Append(",\"").Append(yr).Append("\":{");
                var py = s.PrevYears.FirstOrDefault(p => p.Year == yr);
                if (py != null)
                {
                    double pr = isMetric ? py.RainMm : py.RainIn;
                    sb.AppendFormat(CultureInfo.InvariantCulture, "\"r\":{0:F2},\"l\":{1:F0}", pr, py.LightningStrikes);
                }
                else
                {
                    sb.Append("\"r\":null,\"l\":null");
                }
                sb.Append('}');
            }

            sb.Append("}}");
        }
        sb.Append("]}");

        string jsonPayload = sb.ToString();

        // Calculate height so OpenSilver layout expands correctly
        double approxHeight = 46.0 + 4.0 + (12 * 26.0) + (slotList.Count * 18.0) + 40.0;
        InvertedCanvasContainer.Height = approxHeight;
        InvertedCanvasContainer.MinHeight = approxHeight;
        InvertedLayoutGrid.Height = approxHeight;
        Height = double.NaN;

        // Render via JS Interop to HTML5 Canvas
        try
        {
            var hostDiv = OpenSilver.Interop.GetDiv(InvertedCanvasContainer);
            OpenSilver.Interop.ExecuteJavaScriptVoid(@"
                if (window.initRainDaysCanvas) {
                    window.initRainDaysCanvas($0, $1);
                } else {
                    var s = document.createElement('script');
                    s.src = 'rain-days-canvas.js';
                    s.onload = function() { window.initRainDaysCanvas($0, $1); };
                    document.head.appendChild(s);
                }
            ", hostDiv, jsonPayload);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Canvas interop error: " + ex.Message);
        }
    }

    private sealed class HeatmapSlot
    {
        public string DisplayLabel { get; set; } = string.Empty;
        public string SubLabel { get; set; } = string.Empty;
        public bool IsReference { get; set; }
        public bool IsToday { get; set; }
        public bool IsUpcoming { get; set; }
        public double? ThisYearRainIn { get; set; }
        public double? ThisYearRainMm { get; set; }
        public double? ThisYearLightningStrikes { get; set; }
        public List<YearPointData> PrevYears { get; set; } = [];
    }
}
