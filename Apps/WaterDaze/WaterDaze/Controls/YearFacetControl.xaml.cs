using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WaterDaze.Models;

namespace WaterDaze.Controls;

public partial class YearFacetControl
{
    private static readonly SolidColorBrush BarBrush = new(Color.FromArgb(180, 200, 200, 200));
    private static readonly SolidColorBrush LineBrush = new(Color.FromArgb(255, 0x33, 0x66, 0xCC));

    public static readonly DependencyProperty YearProperty =
        DependencyProperty.Register(nameof(Year), typeof(int), typeof(YearFacetControl),
            new PropertyMetadata(0, OnDataChanged));

    public static readonly DependencyProperty MonthlyRecordsProperty =
        DependencyProperty.Register(nameof(MonthlyRecords), typeof(IEnumerable<MonthlyFlowRecord>),
            typeof(YearFacetControl),
            new PropertyMetadata(null, OnDataChanged));

    public YearFacetControl()
    {
        InitializeComponent();
        Loaded += (_, _) => RenderChart();
    }

    public IEnumerable<MonthlyFlowRecord>? MonthlyRecords
    {
        get => (IEnumerable<MonthlyFlowRecord>?)GetValue(MonthlyRecordsProperty);
        set => SetValue(MonthlyRecordsProperty, value);
    }

    public int Year
    {
        get => (int)GetValue(YearProperty);
        set => SetValue(YearProperty, value);
    }

    private void DrawPolyline(List<Point> points)
    {
        if (points.Count == 1)
        {
            // Single point dot
            var dot = new Ellipse
            {
                Width = 3,
                Height = 3,
                Fill = LineBrush
            };
            Canvas.SetLeft(dot, points[0].X - 1.5);
            Canvas.SetTop(dot, points[0].Y - 1.5);
            ChartCanvas.Children.Add(dot);
            return;
        }

        var polyline = new Polyline
        {
            Stroke = LineBrush,
            StrokeThickness = 1.5,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        var collection = new PointCollection();
        foreach (var p in points) collection.Add(p);
        polyline.Points = collection;

        ChartCanvas.Children.Add(polyline);
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is YearFacetControl control) control.RenderChart();
    }

    public void RenderChart()
    {
        if (TxtYear != null) TxtYear.Text = Year > 0 ? Year.ToString() : string.Empty;

        if (ChartCanvas == null) return;

        ChartCanvas.Children.Clear();

        var records = MonthlyRecords?.Where(r => r.Year == Year).ToList() ?? [];
        var monthMap = records.ToDictionary(r => r.Month, r => r);

        var canvasWidth = ChartCanvas.Width > 0 ? ChartCanvas.Width : 84.0;
        var canvasHeight = ChartCanvas.Height > 0 ? ChartCanvas.Height : 50.0;
        var maxRange = 31.6;
        var slotWidth = canvasWidth / 12.0;
        var barWidth = 4.8;
        var barOffset = (slotWidth - barWidth) / 2.0;

        // 1. Draw Month Data-Day Bars
        for (var m = 1; m <= 12; m++)
        {
            monthMap.TryGetValue(m, out var rec);
            var daysWithData = rec?.DaysWithData ?? 0;

            if (daysWithData > 0)
            {
                var barHeight = Math.Min(canvasHeight, daysWithData / maxRange * canvasHeight);
                var x = (m - 1) * slotWidth + barOffset;
                var y = canvasHeight - barHeight;

                var rect = new Rectangle
                {
                    Width = barWidth,
                    Height = Math.Max(1.0, barHeight),
                    Fill = BarBrush,
                    RadiusX = 1,
                    RadiusY = 1
                };

                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                ChartCanvas.Children.Add(rect);
            }
        }

        // 2. Draw Flow-Day Polyline Segments (discontinuous when missing data)
        List<Point>? currentSegment = null;

        for (var m = 1; m <= 12; m++)
        {
            monthMap.TryGetValue(m, out var rec);
            var daysWithData = rec?.DaysWithData ?? 0;
            var daysWithFlow = rec?.DaysWithFlow ?? 0;

            if (daysWithData > 0)
            {
                currentSegment ??= [];

                var x = (m - 1) * slotWidth + slotWidth / 2.0;
                var flowRatio = Math.Min(1.0, daysWithFlow / maxRange);
                var y = canvasHeight - flowRatio * canvasHeight;

                currentSegment.Add(new Point(x, y));
            }
            else
            {
                // Break segment
                if (currentSegment is { Count: > 0 })
                {
                    DrawPolyline(currentSegment);
                    currentSegment = null;
                }
            }
        }

        if (currentSegment is { Count: > 0 }) DrawPolyline(currentSegment);
    }
}