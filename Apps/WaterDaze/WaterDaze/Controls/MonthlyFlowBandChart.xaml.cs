using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WaterDaze.Models;
using WaterDaze.Services;

namespace WaterDaze.Controls;

public partial class MonthlyFlowBandChart
{
    private static readonly SolidColorBrush BandFillBrush = new(Color.FromArgb(50, 31, 119, 180));
    private static readonly SolidColorBrush BandBorderBrush = new(Color.FromArgb(100, 31, 119, 180));
    private static readonly SolidColorBrush MedianLineBrush = new(Color.FromArgb(255, 31, 119, 180));
    private static readonly SolidColorBrush MarkerFillBrush = new(Color.FromArgb(255, 31, 119, 180));
    private static readonly SolidColorBrush GridLineBrush = new(Color.FromArgb(255, 240, 240, 240));
    private static readonly SolidColorBrush TextBrush = new(Color.FromArgb(255, 107, 114, 128));

    public static readonly DependencyProperty SummaryRecordsProperty =
        DependencyProperty.Register(nameof(SummaryRecords), typeof(IEnumerable<MonthSummaryRecord>),
            typeof(MonthlyFlowBandChart),
            new PropertyMetadata(null, OnDataChanged));

    public MonthlyFlowBandChart()
    {
        InitializeComponent();
        Loaded += (_, _) => RenderChart();
        SizeChanged += (_, _) => RenderChart();
    }

    public IEnumerable<MonthSummaryRecord>? SummaryRecords
    {
        get => (IEnumerable<MonthSummaryRecord>?)GetValue(SummaryRecordsProperty);
        set => SetValue(SummaryRecordsProperty, value);
    }

    private static string FormatTickValue(double value)
    {
        if (value >= 1000) return (value / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + "k";
        if (value >= 10) return value.ToString("0", CultureInfo.InvariantCulture);
        if (value >= 1) return value.ToString("0.#", CultureInfo.InvariantCulture);
        if (value == 0) return "0";
        return value.ToString("0.0#", CultureInfo.InvariantCulture);
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RenderChart();
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MonthlyFlowBandChart control)
        {
            if (e.OldValue is INotifyCollectionChanged oldColl)
                oldColl.CollectionChanged -= control.OnCollectionChanged;
            if (e.NewValue is INotifyCollectionChanged newColl)
                newColl.CollectionChanged += control.OnCollectionChanged;
            control.RenderChart();
        }
    }

    public void RenderChart()
    {
        if (ChartCanvas == null || GridLinesCanvas == null || YAxisCanvas == null || OverlayCanvas == null) return;

        ChartCanvas.Children.Clear();
        GridLinesCanvas.Children.Clear();
        YAxisCanvas.Children.Clear();
        OverlayCanvas.Children.Clear();

        var width = ChartGrid is { ActualWidth: > 0 } ? ChartGrid.ActualWidth :
            ActualWidth > 90 ? ActualWidth - 90 : 600.0;
        var height = ChartGrid is { ActualHeight: > 0 } ? ChartGrid.ActualHeight :
            ActualHeight > 80 ? ActualHeight - 80 : 180.0;

        if (width <= 10 || height <= 10) return;

        ChartCanvas.Width = width;
        ChartCanvas.Height = height;
        GridLinesCanvas.Width = width;
        GridLinesCanvas.Height = height;
        OverlayCanvas.Width = width;
        OverlayCanvas.Height = height;

        var records = SummaryRecords?.OrderBy(r => r.Month).ToList() ?? [];

        // Ensure 1..12 present
        var monthMap = new Dictionary<int, MonthSummaryRecord>();
        for (var m = 1; m <= 12; m++)
        {
            var existing = records.FirstOrDefault(r => r.Month == m);
            if (existing != null)
                monthMap[m] = existing;
            else
                monthMap[m] = new MonthSummaryRecord
                {
                    Month = m,
                    MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(m),
                    MedianFlow = null,
                    Q25Flow = null,
                    Q75Flow = null,
                    MaxFlow = null
                };
        }

        // Determine Y-axis maximum dynamically with appropriate headroom
        var rawMax = 0.0;
        foreach (var r in monthMap.Values)
        {
            if (r.Q75Flow.HasValue && r.Q75Flow.Value > rawMax) rawMax = r.Q75Flow.Value;
            if (r.MedianFlow.HasValue && r.MedianFlow.Value > rawMax) rawMax = r.MedianFlow.Value;
        }

        double maxY;
        if (rawMax <= 0.0)
        {
            maxY = 1.0;
        }
        else if (rawMax >= 10.0)
        {
            maxY = Math.Ceiling(rawMax * 1.15);
        }
        else if (rawMax >= 1.0)
        {
            maxY = Math.Ceiling(rawMax * 1.15 * 10.0) / 10.0;
        }
        else
        {
            maxY = Math.Ceiling(rawMax * 1.25 * 100.0) / 100.0;
            if (maxY <= 0.0) maxY = 0.1;
        }

        // Draw Y-Axis Ticks and Gridlines
        var tickCount = 4;
        for (var i = 0; i <= tickCount; i++)
        {
            var tickVal = maxY / tickCount * i;
            var yPos = height - tickVal / maxY * height;

            // Horizontal Gridline
            var gridLine = new Line
            {
                X1 = 0,
                Y1 = yPos,
                X2 = width,
                Y2 = yPos,
                Stroke = GridLineBrush,
                StrokeThickness = 1
            };
            GridLinesCanvas.Children.Add(gridLine);

            // Y-Axis Tick Label
            var tickText = FormatTickValue(tickVal);
            var textBlock = new TextBlock
            {
                Text = tickText,
                FontSize = 10,
                Foreground = TextBrush,
                TextAlignment = TextAlignment.Right,
                Width = 55
            };
            Canvas.SetLeft(textBlock, 0);
            Canvas.SetTop(textBlock, yPos - 7);
            YAxisCanvas.Children.Add(textBlock);
        }

        var colWidth = width / 12.0;

        // Prepare points for band polygon and median line
        var q75Points = new List<Point>();
        var q25Points = new List<Point>();
        var medianPoints = new List<Point>();

        for (var m = 1; m <= 12; m++)
        {
            var rec = monthMap[m];
            var x = (m - 1) * colWidth + colWidth / 2.0;

            var q25 = rec.Q25Flow ?? 0.0;
            var q75 = rec.Q75Flow ?? 0.0;
            var med = rec.MedianFlow ?? 0.0;

            // Handle swapped quantiles safety
            if (q25 > q75) (q25, q75) = (q75, q25);

            var yQ75 = height - Math.Min(height, q75 / maxY * height);
            var yQ25 = height - Math.Min(height, q25 / maxY * height);
            var yMed = height - Math.Min(height, med / maxY * height);

            q75Points.Add(new Point(x, yQ75));
            q25Points.Add(new Point(x, yQ25));
            medianPoints.Add(new Point(x, yMed));

            // Hover Column for interactive tooltip
            var hitRect = new Rectangle
            {
                Width = colWidth,
                Height = height,
                Fill = Brushes.Transparent,
                Cursor = Cursors.Hand
            };

            var tooltipText = $"Month: {rec.MonthName}\n" +
                              $"Median: {GaugeAnalyticsService.FormatCfs(rec.MedianFlow)} cfs\n" +
                              $"25–75%: {GaugeAnalyticsService.FormatCfs(rec.Q25Flow)} – {GaugeAnalyticsService.FormatCfs(rec.Q75Flow)} cfs";

            ToolTipService.SetToolTip(hitRect, tooltipText);
            Canvas.SetLeft(hitRect, (m - 1) * colWidth);
            Canvas.SetTop(hitRect, 0);
            OverlayCanvas.Children.Add(hitRect);
        }

        // 1. Render 25th-75th Percentile Band Polygon
        var bandPolygon = new Polygon
        {
            Fill = BandFillBrush,
            Stroke = BandBorderBrush,
            StrokeThickness = 0.5
        };

        var bandCollection = new PointCollection();
        foreach (var p in q75Points) bandCollection.Add(p);
        for (var i = q25Points.Count - 1; i >= 0; i--) bandCollection.Add(q25Points[i]);
        bandPolygon.Points = bandCollection;
        ChartCanvas.Children.Add(bandPolygon);

        // 2. Render Median Polyline
        var medianPolyline = new Polyline
        {
            Stroke = MedianLineBrush,
            StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round
        };

        var medianCollection = new PointCollection();
        foreach (var p in medianPoints) medianCollection.Add(p);
        medianPolyline.Points = medianCollection;
        ChartCanvas.Children.Add(medianPolyline);

        // 3. Render Marker Dots
        for (var m = 1; m <= 12; m++)
        {
            var pt = medianPoints[m - 1];
            var rec = monthMap[m];

            var dot = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = MarkerFillBrush,
                Stroke = Brushes.White,
                StrokeThickness = 1.5
            };

            var tooltipText = $"Month: {rec.MonthName}\n" +
                              $"Median: {GaugeAnalyticsService.FormatCfs(rec.MedianFlow)} cfs\n" +
                              $"25–75%: {GaugeAnalyticsService.FormatCfs(rec.Q25Flow)} – {GaugeAnalyticsService.FormatCfs(rec.Q75Flow)} cfs";

            ToolTipService.SetToolTip(dot, tooltipText);
            Canvas.SetLeft(dot, pt.X - 3.5);
            Canvas.SetTop(dot, pt.Y - 3.5);
            ChartCanvas.Children.Add(dot);
        }
    }
}