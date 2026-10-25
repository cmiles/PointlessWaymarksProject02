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

public partial class ComparisonBarBandChart : UserControl
{
    private static readonly SolidColorBrush Brush2023 = new(Color.FromArgb(255, 99, 102, 241));  // Indigo
    private static readonly SolidColorBrush Brush2024 = new(Color.FromArgb(255, 245, 158, 11));  // Amber
    private static readonly SolidColorBrush Brush2025 = new(Color.FromArgb(255, 16, 185, 129));  // Emerald
    private static readonly SolidColorBrush Brush2026 = new(Color.FromArgb(255, 244, 63, 94));   // Rose
    private static readonly SolidColorBrush BrushNormal = new(Color.FromArgb(255, 148, 163, 184)); // Slate
    private static SolidColorBrush GridBrush => Services.ThemeManager.Current.GridLineBrush;
    private static SolidColorBrush AxisTextBrush => Services.ThemeManager.Current.AxisTextBrush;

    public static readonly DependencyProperty MonthDataProperty =
        DependencyProperty.Register(nameof(MonthData), typeof(IEnumerable<MonthMatrixItem>),
            typeof(ComparisonBarBandChart), new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty MetricProperty =
        DependencyProperty.Register(nameof(Metric), typeof(string),
            typeof(ComparisonBarBandChart), new PropertyMetadata("Rainfall", OnDataChanged));

    public static readonly DependencyProperty IsMetricProperty =
        DependencyProperty.Register(nameof(IsMetric), typeof(bool),
            typeof(ComparisonBarBandChart), new PropertyMetadata(false, OnDataChanged));

    private readonly System.Windows.Threading.DispatcherTimer _resizeTimer;

    public ComparisonBarBandChart()
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
            ChartTitleBlock.FontSize = Services.ThemeManager.Current.FontSizeMedium;
        }
        if (ChartSubtitleBlock != null)
        {
            ChartSubtitleBlock.Foreground = Services.ThemeManager.Current.TextSecondary;
            ChartSubtitleBlock.FontSize = Services.ThemeManager.Current.FontSizeRegular;
        }
        double legendSize = Services.ThemeManager.Current.FontSizeLegend;
        if (Legend2023 != null) { Legend2023.FontSize = legendSize; Legend2023.Foreground = Services.ThemeManager.Current.TextSecondary; }
        if (Legend2024 != null) { Legend2024.FontSize = legendSize; Legend2024.Foreground = Services.ThemeManager.Current.TextSecondary; }
        if (Legend2025 != null) { Legend2025.FontSize = legendSize; Legend2025.Foreground = Services.ThemeManager.Current.TextSecondary; }
        if (Legend2026 != null) { Legend2026.FontSize = legendSize; Legend2026.Foreground = Services.ThemeManager.Current.TextSecondary; }
        if (LegendNormal != null) { LegendNormal.FontSize = legendSize; LegendNormal.Foreground = Services.ThemeManager.Current.TextSecondary; }
    }

    public IEnumerable<MonthMatrixItem>? MonthData
    {
        get => (IEnumerable<MonthMatrixItem>?)GetValue(MonthDataProperty);
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

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ComparisonBarBandChart chart)
        {
            chart.RenderChart();
        }
    }

    public void RenderChart()
    {
        if (ChartCanvas == null || GridLinesCanvas == null || YAxisCanvas == null || OverlayCanvas == null)
            return;

        ChartCanvas.Children.Clear();
        GridLinesCanvas.Children.Clear();
        YAxisCanvas.Children.Clear();
        OverlayCanvas.Children.Clear();

        var data = MonthData?.OrderBy(m => m.MonthNumber).ToList();
        if (data == null || data.Count == 0)
            return;

        double width = ChartCanvas.ActualWidth;
        double height = ChartCanvas.ActualHeight;
        if (width <= 10 || height <= 10)
            return;

        string metric = Metric ?? "Rainfall";
        bool isMetricUnit = IsMetric;

        // Update title and unit label
        string unitLabel = GetUnitLabel(metric, isMetricUnit);
        if (YAxisUnitLabel != null) YAxisUnitLabel.Text = unitLabel;
        if (ChartTitleBlock != null) ChartTitleBlock.Text = GetMetricTitle(metric);
        if (ChartSubtitleBlock != null) ChartSubtitleBlock.Text = $"({unitLabel})";

        // Determine min and max value
        double minVal = double.MaxValue;
        double maxVal = double.MinValue;

        foreach (var m in data)
        {
            foreach (var y in m.Years)
            {
                double? val = ExtractMetricValue(y, metric, isMetricUnit);
                if (val.HasValue)
                {
                    minVal = Math.Min(minVal, val.Value);
                    maxVal = Math.Max(maxVal, val.Value);
                }
            }
            double? normVal = ExtractNormalValue(m.HistoricalAverage, metric, isMetricUnit);
            if (normVal.HasValue)
            {
                minVal = Math.Min(minVal, normVal.Value);
                maxVal = Math.Max(maxVal, normVal.Value);
            }
        }

        if (maxVal <= double.MinValue)
        {
            minVal = 0;
            maxVal = 10;
        }

        // Adjust scale
        if (minVal > 0) minVal = 0; // Most weather bar charts anchor at 0
        if (maxVal == minVal) maxVal = minVal + 10;
        maxVal = Math.Ceiling(maxVal * 1.15); // Add headroom

        // Draw Y Axis gridlines and ticks
        int tickCount = 4;
        for (int i = 0; i <= tickCount; i++)
        {
            double ratio = (double)i / tickCount;
            double tickVal = minVal + (maxVal - minVal) * ratio;
            double yPos = height - (ratio * height);

            // Gridline
            var line = new Line
            {
                X1 = 0,
                Y1 = yPos,
                X2 = width,
                Y2 = yPos,
                Stroke = GridBrush,
                StrokeThickness = 1
            };
            GridLinesCanvas.Children.Add(line);

            // Y Axis Label
            var tb = new TextBlock
            {
                Text = FormatTick(tickVal, metric),
                FontSize = 10,
                Foreground = AxisTextBrush,
                TextAlignment = TextAlignment.Right,
                Width = 48
            };
            Canvas.SetLeft(tb, 0);
            Canvas.SetTop(tb, Math.Max(0, yPos - 7));
            YAxisCanvas.Children.Add(tb);
        }

        // Draw grouped bars for 12 months
        int totalMonths = 12;
        double monthSlotWidth = width / totalMonths;

        int[] years = [2023, 2024, 2025, 2026];
        double barSpacing = 2.0;
        double availableBarWidth = Math.Max(2.0, (monthSlotWidth - 12) / years.Length - barSpacing);

        for (int mIdx = 0; mIdx < totalMonths; mIdx++)
        {
            int monthNum = mIdx + 1;
            var monthItem = data.FirstOrDefault(d => d.MonthNumber == monthNum);
            double monthStartX = mIdx * monthSlotWidth;

            // Transparent hit target for hover
            var hitArea = new Rectangle
            {
                Width = monthSlotWidth,
                Height = height,
                Fill = new SolidColorBrush(Colors.Transparent),
                Cursor = Cursors.Hand
            };
            Canvas.SetLeft(hitArea, monthStartX);
            Canvas.SetTop(hitArea, 0);

            if (monthItem != null)
            {
                int currentMonthNum = monthNum;
                var currentMonthItem = monthItem;
                hitArea.MouseEnter += (_, _) => ShowMonthTooltip(monthStartX + monthSlotWidth / 2, currentMonthItem, metric, unitLabel);
                hitArea.MouseLeave += (_, _) => OverlayCanvas.Children.Clear();

                // Draw Historical Normal dashed line marker across this month
                double? normVal = ExtractNormalValue(monthItem.HistoricalAverage, metric, isMetricUnit);
                if (normVal.HasValue)
                {
                    double normRatio = Math.Max(0, Math.Min(1, (normVal.Value - minVal) / (maxVal - minVal)));
                    double normY = height - (normRatio * height);

                    var normMarker = new Line
                    {
                        X1 = monthStartX + 4,
                        Y1 = normY,
                        X2 = monthStartX + monthSlotWidth - 4,
                        Y2 = normY,
                        Stroke = BrushNormal,
                        StrokeThickness = 2,
                        StrokeDashArray = new DoubleCollection { 3, 2 }
                    };
                    ChartCanvas.Children.Add(normMarker);
                }

                // Draw bars for each year
                for (int yIdx = 0; yIdx < years.Length; yIdx++)
                {
                    int yr = years[yIdx];
                    var yData = monthItem.Years.FirstOrDefault(y => y.Year == yr);
                    if (yData == null) continue;

                    double? val = ExtractMetricValue(yData, metric, isMetricUnit);
                    if (!val.HasValue || val.Value <= 0.001) continue;

                    double valRatio = Math.Max(0, Math.Min(1, (val.Value - minVal) / (maxVal - minVal)));
                    double barHeight = Math.Max(3, valRatio * height);
                    double barX = monthStartX + 6 + yIdx * (availableBarWidth + barSpacing);
                    double barY = height - barHeight;

                    var barBrush = GetYearBrush(yr);
                    var bar = new Rectangle
                    {
                        Width = availableBarWidth,
                        Height = barHeight,
                        Fill = barBrush,
                        RadiusX = 2,
                        RadiusY = 2
                    };
                    Canvas.SetLeft(bar, barX);
                    Canvas.SetTop(bar, barY);
                    ChartCanvas.Children.Add(bar);
                }
            }

            ChartCanvas.Children.Add(hitArea);
        }
    }

    private void ShowMonthTooltip(double anchorX, MonthMatrixItem item, string metric, string unitLabel)
    {
        OverlayCanvas.Children.Clear();

        var panel = new StackPanel
        {
            Margin = new Thickness(0)
        };
        var border = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromArgb(240, 15, 23, 42)), // Slate 900
            Padding = new Thickness(10, 8, 10, 8),
            Child = panel,
            BorderBrush = new SolidColorBrush(Color.FromArgb(200, 51, 65, 85)),
            BorderThickness = new Thickness(1)
        };

        panel.Children.Add(new TextBlock
        {
            Text = $"{item.MonthName} ({GetMetricTitle(metric)})",
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Colors.White),
            Margin = new Thickness(0, 0, 0, 6)
        });

        foreach (var yr in item.Years.OrderBy(y => y.Year))
        {
            double? val = ExtractMetricValue(yr, metric, IsMetric);
            string valStr = val.HasValue ? $"{val.Value:F1} {unitLabel}" : "--";

            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
            sp.Children.Add(new Border
            {
                Width = 8,
                Height = 8,
                Background = GetYearBrush(yr.Year),
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            sp.Children.Add(new TextBlock
            {
                Text = $"{yr.Year}: ",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 203, 213, 225))
            });
            sp.Children.Add(new TextBlock
            {
                Text = valStr,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White)
            });
            panel.Children.Add(sp);
        }

        double? norm = ExtractNormalValue(item.HistoricalAverage, metric, IsMetric);
        if (norm.HasValue)
        {
            var spNorm = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 1) };
            spNorm.Children.Add(new TextBlock
            {
                Text = $"Normal: {norm.Value:F1} {unitLabel}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 148, 163, 184))
            });
            panel.Children.Add(spNorm);
        }

        double left = Math.Max(10, Math.Min(ChartCanvas.ActualWidth - 170, anchorX - 85));
        Canvas.SetLeft(border, left);
        Canvas.SetTop(border, 10);
        OverlayCanvas.Children.Add(border);
    }

    private static SolidColorBrush GetYearBrush(int year) => year switch
    {
        2023 => Brush2023,
        2024 => Brush2024,
        2025 => Brush2025,
        2026 => Brush2026,
        _ => BrushNormal
    };

    private static double? ExtractMetricValue(MonthYearData data, string metric, bool isMetric) => metric switch
    {
        "Rainfall" => isMetric ? data.TotalPrecipMm : data.TotalPrecipInches,
        "TemperatureHigh" => isMetric ? (data.MaxTempF.HasValue ? (data.MaxTempF.Value - 32) * 5 / 9 : null) : data.MaxTempF,
        "TemperatureAvg" => isMetric ? data.AvgTempC : data.AvgTempF,
        "TemperatureLow" => isMetric ? (data.MinTempF.HasValue ? (data.MinTempF.Value - 32) * 5 / 9 : null) : data.MinTempF,
        "Lightning" => data.LightningStrikes,
        "Solar" => data.AvgSolarRadiation,
        "WindGust" => isMetric && data.MaxGustMph.HasValue ? data.MaxGustMph.Value * 1.60934 : data.MaxGustMph,
        _ => data.TotalPrecipInches
    };

    private static double? ExtractNormalValue(MonthHistoricalAverage? norm, string metric, bool isMetric)
    {
        if (norm == null) return null;
        return metric switch
        {
            "Rainfall" => isMetric ? norm.AvgPrecipMm : norm.AvgPrecipInches,
            "TemperatureAvg" => isMetric ? norm.AvgTempC : norm.AvgTempF,
            "Lightning" => norm.AvgLightningStrikes,
            "Solar" => norm.AvgSolarRadiation,
            _ => null
        };
    }

    private static string GetMetricTitle(string metric) => metric switch
    {
        "Rainfall" => "Monthly Rainfall",
        "TemperatureHigh" => "Monthly Max High Temperature",
        "TemperatureAvg" => "Monthly Average Temperature",
        "TemperatureLow" => "Monthly Minimum Low Temperature",
        "Lightning" => "Monthly Lightning Strikes",
        "Solar" => "Monthly Solar Radiation (W/m²)",
        "WindGust" => "Monthly Peak Wind Gust",
        _ => metric
    };

    private static string GetUnitLabel(string metric, bool isMetric) => metric switch
    {
        "Rainfall" => isMetric ? "mm" : "in",
        "TemperatureHigh" or "TemperatureAvg" or "TemperatureLow" => isMetric ? "°C" : "°F",
        "Lightning" => "strikes",
        "Solar" => "W/m²",
        "WindGust" => isMetric ? "km/h" : "mph",
        _ => ""
    };

    private static string FormatTick(double val, string metric)
    {
        if (metric == "Rainfall") return val.ToString("0.#", CultureInfo.InvariantCulture);
        if (metric == "Lightning" || metric == "Solar") return Math.Round(val).ToString("F0", CultureInfo.InvariantCulture);
        return val.ToString("0", CultureInfo.InvariantCulture);
    }
}
