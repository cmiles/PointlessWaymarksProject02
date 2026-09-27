using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WaterDaze.Models;
using WaterDaze.Services;

namespace WaterDaze.Controls;

public partial class MonthlyHeatmapControl
{
    private static readonly string[] MonthNames =
    [
        "Jan", "Feb", "Mar", "Apr", "May", "Jun",
        "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
    ];

    private static readonly SolidColorBrush NoDataBrush = new(Color.FromArgb(255, 245, 247, 250));
    private static readonly SolidColorBrush HeaderTextBrush = new(Color.FromArgb(255, 75, 85, 99));
    private static readonly SolidColorBrush CellBorderBrush = new(Color.FromArgb(255, 235, 238, 242));

    public static readonly DependencyProperty MonthlyRecordsProperty =
        DependencyProperty.Register(nameof(MonthlyRecords), typeof(IEnumerable<MonthlyFlowRecord>),
            typeof(MonthlyHeatmapControl),
            new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty YearsProperty =
        DependencyProperty.Register(nameof(Years), typeof(IEnumerable<int>), typeof(MonthlyHeatmapControl),
            new PropertyMetadata(null, OnDataChanged));

    public MonthlyHeatmapControl()
    {
        InitializeComponent();
        Loaded += (_, _) => RenderHeatmap();
    }

    public IEnumerable<MonthlyFlowRecord>? MonthlyRecords
    {
        get => (IEnumerable<MonthlyFlowRecord>?)GetValue(MonthlyRecordsProperty);
        set => SetValue(MonthlyRecordsProperty, value);
    }

    public IEnumerable<int>? Years
    {
        get => (IEnumerable<int>?)GetValue(YearsProperty);
        set => SetValue(YearsProperty, value);
    }

    private static Grid CreateRowGrid()
    {
        var grid = new Grid
        {
            Margin = new Thickness(0, 1, 0, 1)
        };

        // Column 0: Year Label (width 48)
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });

        // Columns 1..12: Months (proportional width)
        for (var i = 0; i < 12; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        return grid;
    }

    private static SolidColorBrush GetPuBuColorBrush(double t)
    {
        // PuBu Sequential Color Scale Interpolation
        // 0.0: #fff7fb (255, 247, 251)
        // 0.2: #ece7f2 (236, 231, 242)
        // 0.4: #d0d1e6 (208, 209, 230)
        // 0.6: #a6bddb (166, 189, 219)
        // 0.8: #67a9cf (103, 169, 207)
        // 1.0: #014636 (1, 70, 54) or deep teal/navy #02818a
        var stops = new (double pos, byte r, byte g, byte b)[]
        {
            (0.00, 255, 247, 251),
            (0.15, 236, 231, 242),
            (0.30, 208, 209, 230),
            (0.45, 166, 189, 219),
            (0.60, 103, 169, 207),
            (0.75, 54, 144, 192),
            (0.90, 2, 129, 138),
            (1.00, 1, 70, 54)
        };

        for (var i = 0; i < stops.Length - 1; i++)
            if (t <= stops[i + 1].pos)
            {
                var range = stops[i + 1].pos - stops[i].pos;
                var factor = range > 0 ? (t - stops[i].pos) / range : 0;

                var r = (byte)(stops[i].r + factor * (stops[i + 1].r - stops[i].r));
                var g = (byte)(stops[i].g + factor * (stops[i + 1].g - stops[i].g));
                var b = (byte)(stops[i].b + factor * (stops[i + 1].b - stops[i].b));

                return new SolidColorBrush(Color.FromArgb(255, r, g, b));
            }

        return new SolidColorBrush(Color.FromArgb(255, stops[^1].r, stops[^1].g, stops[^1].b));
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RenderHeatmap();
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MonthlyHeatmapControl control)
        {
            if (e.OldValue is INotifyCollectionChanged oldColl)
                oldColl.CollectionChanged -= control.OnCollectionChanged;
            if (e.NewValue is INotifyCollectionChanged newColl)
                newColl.CollectionChanged += control.OnCollectionChanged;
            control.RenderHeatmap();
        }
    }

    public void RenderHeatmap()
    {
        if (MatrixContainer == null) return;

        MatrixContainer.Children.Clear();

        var records = MonthlyRecords?.ToList() ?? [];
        var years = Years?.OrderBy(y => y).ToList();

        if (years == null || years.Count == 0)
        {
            if (records.Count > 0)
                years = [.. records.Select(r => r.Year).Distinct().OrderBy(y => y)];
            else
                return;
        }

        // Determine max log10 value across all data
        var maxVal = 0.0;
        foreach (var r in records)
            if (r.DaysWithData > 0 && r.MaxMeanFlow > maxVal)
                maxVal = r.MaxMeanFlow;

        var maxLog = Math.Log10(maxVal + 1.0);
        if (maxLog <= 0.0) maxLog = 1.0;

        if (TxtMaxLog != null) TxtMaxLog.Text = $"log₁₀({maxVal:F0})";

        // Fast lookup by (year, month)
        var recordMap = records.ToDictionary(r => (r.Year, r.Month), r => r);

        // 1. Month Header Row
        var headerGrid = CreateRowGrid();
        var yearHeader = new TextBlock
        {
            Text = "Year",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = HeaderTextBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        };
        Grid.SetColumn(yearHeader, 0);
        headerGrid.Children.Add(yearHeader);

        for (var m = 1; m <= 12; m++)
        {
            var monthHeader = new TextBlock
            {
                Text = MonthNames[m - 1],
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = HeaderTextBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(monthHeader, m);
            headerGrid.Children.Add(monthHeader);
        }

        MatrixContainer.Children.Add(headerGrid);

        // 2. Year Data Rows
        foreach (var year in years)
        {
            var rowGrid = CreateRowGrid();

            // Year label
            var yearLabel = new TextBlock
            {
                Text = year.ToString(),
                FontSize = 10,
                Foreground = HeaderTextBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };
            Grid.SetColumn(yearLabel, 0);
            rowGrid.Children.Add(yearLabel);

            // 12 month cells
            for (var m = 1; m <= 12; m++)
            {
                recordMap.TryGetValue((year, m), out var rec);
                var hasData = rec is { DaysWithData: > 0 };
                var flow = rec?.MaxMeanFlow ?? 0.0;

                var cellBorder = new Border
                {
                    Margin = new Thickness(1),
                    CornerRadius = new CornerRadius(1.5),
                    BorderThickness = new Thickness(0.5),
                    BorderBrush = CellBorderBrush,
                    Height = 18
                };

                if (hasData)
                {
                    var logVal = Math.Log10(flow + 1.0);
                    var normalized = Math.Max(0.0, Math.Min(1.0, logVal / maxLog));
                    cellBorder.Background = GetPuBuColorBrush(normalized);

                    var tooltip =
                        $"Year: {year}\nMonth: {MonthNames[m - 1]}\nMax Daily Mean Flow: {GaugeAnalyticsService.FormatCfs(flow)} cfs";
                    ToolTipService.SetToolTip(cellBorder, tooltip);
                }
                else
                {
                    cellBorder.Background = NoDataBrush;
                    var tooltip = $"Year: {year}\nMonth: {MonthNames[m - 1]}\nNo Data";
                    ToolTipService.SetToolTip(cellBorder, tooltip);
                }

                Grid.SetColumn(cellBorder, m);
                rowGrid.Children.Add(cellBorder);
            }

            MatrixContainer.Children.Add(rowGrid);
        }
    }
}