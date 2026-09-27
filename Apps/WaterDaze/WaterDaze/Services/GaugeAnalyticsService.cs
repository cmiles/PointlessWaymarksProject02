using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WaterDaze.Models;

namespace WaterDaze.Services;

public class GaugeAnalyticsService
{
    private static readonly string[] MonthNames =
    [
        "Jan", "Feb", "Mar", "Apr", "May", "Jun",
        "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
    ];

    public static double CalculateQuantile(List<double> sortedValues, double quantile)
    {
        if (sortedValues.Count == 0) return 0.0;
        if (sortedValues.Count == 1) return sortedValues[0];
        if (quantile <= 0.0) return sortedValues[0];
        if (quantile >= 1.0) return sortedValues[^1];

        var idx = quantile * (sortedValues.Count - 1);
        var lowerIndex = (int)Math.Floor(idx);
        var upperIndex = (int)Math.Ceiling(idx);
        var weight = idx - lowerIndex;

        return sortedValues[lowerIndex] + weight * (sortedValues[upperIndex] - sortedValues[lowerIndex]);
    }

    public static string FormatCfs(double? flow)
    {
        if (!flow.HasValue || double.IsNaN(flow.Value)) return "—";
        var val = flow.Value;
        if (val >= 1000) return val.ToString("#,##0", CultureInfo.InvariantCulture);
        if (val >= 100) return val.ToString("0", CultureInfo.InvariantCulture);
        if (val >= 10) return val.ToString("0.0", CultureInfo.InvariantCulture);
        return val.ToString("0.00", CultureInfo.InvariantCulture);
    }

    public GaugeDashboardData ProcessGaugeData(GaugeSite site, List<DailyFlowRecord> rawRecords)
    {
        var dashboard = new GaugeDashboardData
        {
            Site = site
        };

        if (rawRecords.Count == 0) return dashboard;

        var validRaw = rawRecords.Where(r => r.Date != default).OrderBy(r => r.Date).ToList();
        if (validRaw.Count == 0) return dashboard;

        var minYear = validRaw.Min(r => r.Date.Year);
        var startYear = minYear + 1;
        var currentYear = DateTime.Today.Year;

        // In case startYear > currentYear (e.g. Gauge installed in current year)
        if (startYear > currentYear) startYear = minYear;

        var startDate = new DateTime(startYear, 1, 1);
        var endDate = new DateTime(currentYear, 12, 31);

        dashboard.RecordStartDate = validRaw.Min(r => r.Date);
        dashboard.RecordEndDate = validRaw.Max(r => r.Date);

        // Index raw records by date for O(1) lookup
        var rawMap = new Dictionary<DateTime, DailyFlowRecord>();
        foreach (var r in validRaw) rawMap[r.Date] = r;

        // 1. Expand contiguous daily frame
        var allDays = new List<DailyFlowRecord>();
        for (var d = startDate; d <= endDate; d = d.AddDays(1))
            if (rawMap.TryGetValue(d, out var raw) && raw.HasData)
                allDays.Add(new DailyFlowRecord
                {
                    Date = d,
                    MeanFlow = raw.MeanFlow,
                    HasData = true,
                    HasFlow = raw.HasFlow,
                    SiteCode = site.SiteCode,
                    SiteName = site.SiteName,
                    Qualifiers = raw.Qualifiers
                });
            else
                allDays.Add(new DailyFlowRecord
                {
                    Date = d,
                    MeanFlow = null,
                    HasData = false,
                    HasFlow = false,
                    SiteCode = site.SiteCode,
                    SiteName = site.SiteName,
                    Qualifiers = string.Empty
                });

        dashboard.DailyRecords = allDays;
        dashboard.TotalDaysWithData = allDays.Count(d => d.HasData);
        dashboard.TotalDaysWithFlow = allDays.Count(d => d.HasFlow);

        // 2. Compute Monthly Aggregations
        var monthlyRecords = new List<MonthlyFlowRecord>();

        var yearSet = new SortedSet<int>();
        for (var y = startYear; y <= currentYear; y++) yearSet.Add(y);
        dashboard.Years = [.. yearSet];

        foreach (var y in dashboard.Years)
            for (var m = 1; m <= 12; m++)
            {
                var daysInMonth = allDays.Where(d => d.Date.Year == y && d.Date.Month == m).ToList();
                var dataDays = daysInMonth.Where(d => d is { HasData: true, MeanFlow: not null }).ToList();
                var flowDays = dataDays.Where(d => d.HasFlow).ToList();

                var daysWithData = dataDays.Count;
                var daysWithFlow = flowDays.Count;
                var maxMeanFlow = dataDays.Count > 0 ? dataDays.Max(d => d.MeanFlow!.Value) : 0.0;
                var meanFlow = dataDays.Count > 0 ? dataDays.Average(d => d.MeanFlow!.Value) : (double?)null;

                double? q25 = null;
                double? q75 = null;
                if (dataDays.Count > 0)
                {
                    var sortedFlows = dataDays.Select(d => d.MeanFlow!.Value).OrderBy(v => v).ToList();
                    q25 = CalculateQuantile(sortedFlows, 0.25);
                    q75 = CalculateQuantile(sortedFlows, 0.75);
                }

                monthlyRecords.Add(new MonthlyFlowRecord
                {
                    Year = y,
                    Month = m,
                    MonthDate = new DateTime(y, m, 1),
                    DaysWithData = daysWithData,
                    DaysWithFlow = daysWithFlow,
                    MaxMeanFlow = maxMeanFlow,
                    MeanFlow = meanFlow,
                    Q25Flow = q25,
                    Q75Flow = q75
                });
            }

        dashboard.MonthlyRecords = monthlyRecords;

        // 3. Compute 12-Month Climatology Quantiles
        var climatology = new List<MonthSummaryRecord>();
        for (var m = 1; m <= 12; m++)
        {
            var allMonthDataDays = allDays
                .Where(d => d.Date.Month == m && d is { HasData: true, MeanFlow: not null })
                .Select(d => d.MeanFlow!.Value)
                .OrderBy(v => v)
                .ToList();

            var maxFlow = allMonthDataDays.Count > 0 ? allMonthDataDays.Max() : (double?)null;
            var medianFlow = allMonthDataDays.Count > 0 ? CalculateQuantile(allMonthDataDays, 0.50) : (double?)null;
            var q25 = allMonthDataDays.Count > 0 ? CalculateQuantile(allMonthDataDays, 0.25) : (double?)null;
            var q75 = allMonthDataDays.Count > 0 ? CalculateQuantile(allMonthDataDays, 0.75) : (double?)null;

            climatology.Add(new MonthSummaryRecord
            {
                Month = m,
                MonthName = MonthNames[m - 1],
                MedianFlow = medianFlow,
                Q25Flow = q25,
                Q75Flow = q75,
                MaxFlow = maxFlow
            });
        }

        dashboard.ClimatologySummaries = climatology;

        // 4. Run-length Streak Detection
        // state: -1 = missing data, 1 = dry (flow == 0), 0 = wet (flow > 0)
        var dryStreaks = new List<StreakRecord>();
        var wetStreaks = new List<StreakRecord>();

        var currentDryLength = 0;
        DateTime? currentDryStart = null;

        var currentWetLength = 0;
        DateTime? currentWetStart = null;

        for (var i = 0; i < allDays.Count; i++)
        {
            var day = allDays[i];

            if (!day.HasData)
            {
                // Missing data breaks both streaks
                if (currentDryLength > 0 && currentDryStart.HasValue)
                {
                    dryStreaks.Add(new StreakRecord
                    {
                        LengthDays = currentDryLength,
                        StartDate = currentDryStart.Value,
                        EndDate = allDays[i - 1].Date
                    });
                    currentDryLength = 0;
                    currentDryStart = null;
                }

                if (currentWetLength > 0 && currentWetStart.HasValue)
                {
                    wetStreaks.Add(new StreakRecord
                    {
                        LengthDays = currentWetLength,
                        StartDate = currentWetStart.Value,
                        EndDate = allDays[i - 1].Date
                    });
                    currentWetLength = 0;
                    currentWetStart = null;
                }
            }
            else if (day.HasFlow) // wet (flow > 0)
            {
                // Ends dry streak
                if (currentDryLength > 0 && currentDryStart.HasValue)
                {
                    dryStreaks.Add(new StreakRecord
                    {
                        LengthDays = currentDryLength,
                        StartDate = currentDryStart.Value,
                        EndDate = allDays[i - 1].Date
                    });
                    currentDryLength = 0;
                    currentDryStart = null;
                }

                // Continues or starts wet streak
                if (currentWetLength == 0) currentWetStart = day.Date;
                currentWetLength++;
            }
            else // dry (flow == 0)
            {
                // Ends wet streak
                if (currentWetLength > 0 && currentWetStart.HasValue)
                {
                    wetStreaks.Add(new StreakRecord
                    {
                        LengthDays = currentWetLength,
                        StartDate = currentWetStart.Value,
                        EndDate = allDays[i - 1].Date
                    });
                    currentWetLength = 0;
                    currentWetStart = null;
                }

                // Continues or starts dry streak
                if (currentDryLength == 0) currentDryStart = day.Date;
                currentDryLength++;
            }
        }

        // Check if trailing streaks ended on the last day
        if (currentDryLength > 0 && currentDryStart.HasValue)
            dryStreaks.Add(new StreakRecord
            {
                LengthDays = currentDryLength,
                StartDate = currentDryStart.Value,
                EndDate = allDays[^1].Date
            });

        if (currentWetLength > 0 && currentWetStart.HasValue)
            wetStreaks.Add(new StreakRecord
            {
                LengthDays = currentWetLength,
                StartDate = currentWetStart.Value,
                EndDate = allDays[^1].Date
            });

        dashboard.TopDryStreaks =
        [
            .. dryStreaks
                .OrderByDescending(s => s.LengthDays)
                .ThenBy(s => s.StartDate)
                .Take(10)
                .Select((s, index) =>
                {
                    s.Rank = index + 1;
                    return s;
                })
        ];

        dashboard.TopWetStreaks =
        [
            .. wetStreaks
                .OrderByDescending(s => s.LengthDays)
                .ThenBy(s => s.StartDate)
                .Take(10)
                .Select((s, index) =>
                {
                    s.Rank = index + 1;
                    return s;
                })
        ];

        // 5. Annual Full-Coverage Filtering (>= 1 data day per month)
        var eligibleYears = new HashSet<int>();
        foreach (var y in dashboard.Years)
        {
            var monthsWithData = 0;
            for (var m = 1; m <= 12; m++)
                if (allDays.Any(d => d.Date.Year == y && d.Date.Month == m && d.HasData))
                    monthsWithData++;

            if (monthsWithData >= 12) eligibleYears.Add(y);
        }

        var yearlyMeans = allDays
            .Where(d => eligibleYears.Contains(d.Date.Year) && d is { HasData: true, MeanFlow: not null })
            .GroupBy(d => d.Date.Year)
            .Select(g => new
            {
                Year = g.Key,
                MeanFlow = g.Average(d => d.MeanFlow!.Value)
            })
            .ToList();

        dashboard.TopWettestYears =
        [
            .. yearlyMeans
                .OrderByDescending(y => y.MeanFlow)
                .Take(10)
                .Select((y, idx) => new YearlyFlowRecord
                {
                    Rank = idx + 1,
                    Year = y.Year,
                    MeanFlow = y.MeanFlow,
                    FormattedFlow = FormatCfs(y.MeanFlow)
                })
        ];

        dashboard.TopDriestYears =
        [
            .. yearlyMeans
                .OrderBy(y => y.MeanFlow)
                .Take(10)
                .Select((y, idx) => new YearlyFlowRecord
                {
                    Rank = idx + 1,
                    Year = y.Year,
                    MeanFlow = y.MeanFlow,
                    FormattedFlow = FormatCfs(y.MeanFlow)
                })
        ];

        // 6. Top 10 Peak Daily Mean Flow Days
        dashboard.TopPeakFlowDays =
        [
            .. allDays
                .Where(d => d is { HasData: true, MeanFlow: not null })
                .OrderByDescending(d => d.MeanFlow!.Value)
                .ThenBy(d => d.Date)
                .Take(10)
                .Select((d, idx) => new DailyPeakFlowRecord
                {
                    Rank = idx + 1,
                    Date = d.Date,
                    MeanFlow = d.MeanFlow!.Value,
                    FormattedFlow = FormatCfs(d.MeanFlow!.Value)
                })
        ];

        return dashboard;
    }
}