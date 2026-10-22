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

public partial class RecentDaysChart : UserControl
{
    private static readonly SolidColorBrush HighBrush = new(Color.FromArgb(255, 244, 63, 94));   // Rose
    private static readonly SolidColorBrush LowBrush = new(Color.FromArgb(255, 59, 130, 246));    // Blue
    private static readonly SolidColorBrush TempRangeBrush = new(Color.FromArgb(50, 244, 63, 94));
    private static readonly SolidColorBrush NormalBrush = new(Color.FromArgb(255, 148, 163, 184)); // Slate
    private static readonly SolidColorBrush RainBrush = new(Color.FromArgb(200, 2, 132, 199));     // Ocean Blue
    private static SolidColorBrush GridBrush => Services.ThemeManager.Current.GridLineBrush;
    private static SolidColorBrush AxisBrush => Services.ThemeManager.Current.AxisTextBrush;

    public static readonly DependencyProperty RecentDaysProperty =
        DependencyProperty.Register(nameof(RecentDays), typeof(IEnumerable<RecentDayItem>),
            typeof(RecentDaysChart), new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty IsMetricProperty =
        DependencyProperty.Register(nameof(IsMetric), typeof(bool),
            typeof(RecentDaysChart), new PropertyMetadata(false, OnDataChanged));

    public static readonly DependencyProperty SelectedDayProperty =
        DependencyProperty.Register(nameof(SelectedDay), typeof(RecentDayItem),
            typeof(RecentDaysChart), new PropertyMetadata(null, OnDataChanged));

    public event EventHandler<RecentDayItem>? DaySelected;

    private readonly System.Windows.Threading.DispatcherTimer _resizeTimer;

    public RecentDaysChart()
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
        if (UnitSubtitle != null)
        {
            UnitSubtitle.Foreground = Services.ThemeManager.Current.TextSecondary;
            UnitSubtitle.FontSize = Services.ThemeManager.Current.FontSizeRegular;
        }
    }

    public IEnumerable<RecentDayItem>? RecentDays
    {
        get => (IEnumerable<RecentDayItem>?)GetValue(RecentDaysProperty);
        set => SetValue(RecentDaysProperty, value);
    }

    public bool IsMetric
    {
        get => (bool)GetValue(IsMetricProperty);
        set => SetValue(IsMetricProperty, value);
    }

    public RecentDayItem? SelectedDay
    {
        get => (RecentDayItem?)GetValue(SelectedDayProperty);
        set => SetValue(SelectedDayProperty, value);
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RecentDaysChart chart)
        {
            chart.RenderChart();
        }
    }

    public void RenderChart()
    {
        if (ChartCanvas == null || GridLinesCanvas == null || YAxisCanvas == null || XAxisCanvas == null || OverlayCanvas == null)
            return;

        ChartCanvas.Children.Clear();
        GridLinesCanvas.Children.Clear();
        YAxisCanvas.Children.Clear();
        XAxisCanvas.Children.Clear();
        OverlayCanvas.Children.Clear();

        var days = RecentDays?.OrderBy(d => d.Date).ToList();
        if (days == null || days.Count == 0)
            return;

        double width = ChartCanvas.ActualWidth;
        double height = ChartCanvas.ActualHeight;
        if (width <= 10 || height <= 10)
            return;

        bool isMetric = IsMetric;
        if (UnitSubtitle != null) UnitSubtitle.Text = isMetric ? "(°C)" : "(°F)";

        // Calculate Temperature bounds
        double minTemp = double.MaxValue;
        double maxTemp = double.MinValue;
        double maxRain = 0;

        foreach (var d in days)
        {
            if (d.CurrentYear != null)
            {
                double? hi = isMetric ? d.CurrentYear.TemperatureHighC : d.CurrentYear.TemperatureHighF;
                double? lo = isMetric ? d.CurrentYear.TemperatureLowC : d.CurrentYear.TemperatureLowF;
                if (hi.HasValue) maxTemp = Math.Max(maxTemp, hi.Value);
                if (lo.HasValue) minTemp = Math.Min(minTemp, lo.Value);

                double rain = isMetric ? d.CurrentYear.PrecipMm : d.CurrentYear.PrecipInches;
                maxRain = Math.Max(maxRain, rain);
            }
            if (d.HistoricalNormal != null)
            {
                double? normHi = isMetric ? (d.HistoricalNormal.NormalHighF.HasValue ? (d.HistoricalNormal.NormalHighF.Value - 32) * 5 / 9 : null) : d.HistoricalNormal.NormalHighF;
                if (normHi.HasValue) maxTemp = Math.Max(maxTemp, normHi.Value);
            }
        }

        if (maxTemp <= double.MinValue) { minTemp = isMetric ? 10 : 50; maxTemp = isMetric ? 35 : 95; }
        minTemp = Math.Floor((minTemp - 5) / 5) * 5;
        maxTemp = Math.Ceiling((maxTemp + 5) / 5) * 5;

        // Draw Y Axis and Grid Lines (4 steps)
        int steps = 4;
        for (int i = 0; i <= steps; i++)
        {
            double ratio = (double)i / steps;
            double tempVal = minTemp + (maxTemp - minTemp) * ratio;
            double y = height - (ratio * height);

            var line = new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = GridBrush,
                StrokeThickness = 1
            };
            GridLinesCanvas.Children.Add(line);

            var tb = new TextBlock
            {
                Text = $"{Math.Round(tempVal):F0}°",
                FontSize = 10,
                Foreground = AxisBrush,
                TextAlignment = TextAlignment.Right,
                Width = 44
            };
            Canvas.SetLeft(tb, 0);
            Canvas.SetTop(tb, Math.Max(0, y - 7));
            YAxisCanvas.Children.Add(tb);
        }

        // Draw data points across days
        int count = days.Count;
        double slotWidth = width / count;

        PointCollection? normPoints = new PointCollection();
        PointCollection? highPoints = new PointCollection();
        PointCollection? lowPoints = new PointCollection();

        for (int i = 0; i < count; i++)
        {
            var day = days[i];
            double xCenter = i * slotWidth + slotWidth / 2;

            // X-Axis date label
            string shortDate = DateTime.TryParse(day.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
                ? dt.ToString("M/d", CultureInfo.InvariantCulture)
                : day.CalendarDay;

            var xLabel = new TextBlock
            {
                Text = shortDate,
                FontSize = 10,
                Foreground = AxisBrush,
                TextAlignment = TextAlignment.Center,
                Width = slotWidth
            };
            Canvas.SetLeft(xLabel, i * slotWidth);
            Canvas.SetTop(xLabel, 4);
            XAxisCanvas.Children.Add(xLabel);

            // Rainfall bar at bottom if rain > 0
            if (day.CurrentYear != null)
            {
                double rain = isMetric ? day.CurrentYear.PrecipMm : day.CurrentYear.PrecipInches;
                if (rain > 0.005)
                {
                    double rainRatio = maxRain > 0 ? Math.Min(1, rain / maxRain) : 0.5;
                    double barH = Math.Max(4, rainRatio * (height * 0.35));
                    var rainBar = new Rectangle
                    {
                        Width = Math.Max(6, slotWidth * 0.4),
                        Height = barH,
                        Fill = RainBrush,
                        RadiusX = 2,
                        RadiusY = 2
                    };
                    Canvas.SetLeft(rainBar, xCenter - rainBar.Width / 2);
                    Canvas.SetTop(rainBar, height - barH);
                    ChartCanvas.Children.Add(rainBar);
                }

                // High / Low temps
                double? hi = isMetric ? day.CurrentYear.TemperatureHighC : day.CurrentYear.TemperatureHighF;
                double? lo = isMetric ? day.CurrentYear.TemperatureLowC : day.CurrentYear.TemperatureLowF;

                if (hi.HasValue && lo.HasValue)
                {
                    double hiY = height - ((hi.Value - minTemp) / (maxTemp - minTemp) * height);
                    double loY = height - ((lo.Value - minTemp) / (maxTemp - minTemp) * height);

                    highPoints.Add(new Point(xCenter, hiY));
                    lowPoints.Add(new Point(xCenter, loY));

                    // Range vertical bar connecting High and Low
                    var rangeBar = new Rectangle
                    {
                        Width = 4,
                        Height = Math.Max(2, loY - hiY),
                        Fill = TempRangeBrush,
                        RadiusX = 2,
                        RadiusY = 2
                    };
                    Canvas.SetLeft(rangeBar, xCenter - 2);
                    Canvas.SetTop(rangeBar, hiY);
                    ChartCanvas.Children.Add(rangeBar);

                    // High dot
                    var hiDot = new Ellipse
                    {
                        Width = 7,
                        Height = 7,
                        Fill = HighBrush
                    };
                    Canvas.SetLeft(hiDot, xCenter - 3.5);
                    Canvas.SetTop(hiDot, hiY - 3.5);
                    ChartCanvas.Children.Add(hiDot);

                    // Low dot
                    var loDot = new Ellipse
                    {
                        Width = 7,
                        Height = 7,
                        Fill = LowBrush
                    };
                    Canvas.SetLeft(loDot, xCenter - 3.5);
                    Canvas.SetTop(loDot, loY - 3.5);
                    ChartCanvas.Children.Add(loDot);
                }
            }

            // Normal line point
            if (day.HistoricalNormal != null)
            {
                double? normHi = isMetric ? (day.HistoricalNormal.NormalHighF.HasValue ? (day.HistoricalNormal.NormalHighF.Value - 32) * 5 / 9 : null) : day.HistoricalNormal.NormalHighF;
                if (normHi.HasValue)
                {
                    double nY = height - ((normHi.Value - minTemp) / (maxTemp - minTemp) * height);
                    normPoints.Add(new Point(xCenter, nY));
                }
            }

            // Hit target for selection and tooltip
            var hitArea = new Rectangle
            {
                Width = slotWidth,
                Height = height,
                Fill = new SolidColorBrush(Colors.Transparent),
                Cursor = Cursors.Hand
            };
            Canvas.SetLeft(hitArea, i * slotWidth);
            Canvas.SetTop(hitArea, 0);

            var currentDay = day;
            hitArea.MouseLeftButtonDown += (_, _) =>
            {
                SelectedDay = currentDay;
                DaySelected?.Invoke(this, currentDay);
            };
            hitArea.MouseEnter += (_, _) => ShowDayTooltip(xCenter, currentDay, isMetric);
            hitArea.MouseLeave += (_, _) => OverlayCanvas.Children.Clear();

            ChartCanvas.Children.Add(hitArea);
        }

        // Draw Normal dashed polyline
        if (normPoints.Count > 1)
        {
            var normLine = new Polyline
            {
                Points = normPoints,
                Stroke = NormalBrush,
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 3, 2 }
            };
            ChartCanvas.Children.Add(normLine);
        }

        // Draw High temperature trend polyline
        if (highPoints.Count > 1)
        {
            var hiLine = new Polyline
            {
                Points = highPoints,
                Stroke = HighBrush,
                StrokeThickness = 2
            };
            ChartCanvas.Children.Add(hiLine);
        }

        // Draw Low temperature trend polyline
        if (lowPoints.Count > 1)
        {
            var loLine = new Polyline
            {
                Points = lowPoints,
                Stroke = LowBrush,
                StrokeThickness = 2
            };
            ChartCanvas.Children.Add(loLine);
        }
    }

    private void ShowDayTooltip(double xPos, RecentDayItem day, bool isMetric)
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

        panel.Children.Add(new TextBlock
        {
            Text = $"{day.DisplayDate} ({day.DayOfWeek})",
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Colors.White),
            Margin = new Thickness(0, 0, 0, 4)
        });

        if (day.CurrentYear != null)
        {
            double? hi = isMetric ? day.CurrentYear.TemperatureHighC : day.CurrentYear.TemperatureHighF;
            double? lo = isMetric ? day.CurrentYear.TemperatureLowC : day.CurrentYear.TemperatureLowF;
            string u = isMetric ? "°C" : "°F";
            double rain = isMetric ? day.CurrentYear.PrecipMm : day.CurrentYear.PrecipInches;
            string rUnit = isMetric ? "mm" : "in";

            panel.Children.Add(new TextBlock
            {
                Text = $"High: {hi:F1}{u} | Low: {lo:F1}{u}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 241, 245, 249))
            });

            if (rain > 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = $"Rain: {rain:F2} {rUnit}",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 56, 189, 248))
                });
            }
        }

        if (day.HistoricalNormal != null && day.HistoricalNormal.NormalHighF.HasValue)
        {
            double norm = isMetric ? (day.HistoricalNormal.NormalHighF.Value - 32) * 5 / 9 : day.HistoricalNormal.NormalHighF.Value;
            string u = isMetric ? "°C" : "°F";
            panel.Children.Add(new TextBlock
            {
                Text = $"Normal High: {norm:F1}{u}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 148, 163, 184))
            });
        }

        double left = Math.Max(10, Math.Min(ChartCanvas.ActualWidth - 160, xPos - 80));
        Canvas.SetLeft(border, left);
        Canvas.SetTop(border, 10);
        OverlayCanvas.Children.Add(border);
    }
}
