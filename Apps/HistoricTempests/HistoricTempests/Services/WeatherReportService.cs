using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HistoricTempests.Models;

namespace HistoricTempests.Services;

public class WeatherReportService : IWeatherReportService
{
    private const string DefaultBaseUrl = "https://software.pointlesswaymarks.com/HistoricTempestsData/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public WeatherReportService(HttpClient? httpClient = null, string? baseUrl = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _baseUrl = (baseUrl ?? DefaultBaseUrl).TrimEnd('/') + "/";
    }

    public async Task<OverviewReport?> GetOverviewAsync(CancellationToken ct = default)
    {
        return await FetchJsonAsync<OverviewReport>("weather_summary_overview.json", ct).ConfigureAwait(false);
    }

    public async Task<MonthlyYearlyComparisonsReport?> GetMonthlyYearlyComparisonsAsync(CancellationToken ct = default)
    {
        // Try monthly_yearly_comparisons.json first, fall back to long_term_comparisons.json
        var report = await FetchJsonAsync<MonthlyYearlyComparisonsReport>("monthly_yearly_comparisons.json", ct).ConfigureAwait(false);
        if (report == null || report.MonthByMonthMultiYearComparison.Count == 0)
        {
            report = await FetchJsonAsync<MonthlyYearlyComparisonsReport>("long_term_comparisons.json", ct).ConfigureAwait(false);
        }
        return report;
    }

    public async Task<RecentDaysReport?> GetRecentDaysAsync(CancellationToken ct = default)
    {
        return await FetchJsonAsync<RecentDaysReport>("recent_day_comparisons.json", ct).ConfigureAwait(false);
    }

    public async Task<UpcomingWeekReport?> GetUpcomingWeekAsync(CancellationToken ct = default)
    {
        return await FetchJsonAsync<UpcomingWeekReport>("next_week_historical_comparisons.json", ct).ConfigureAwait(false);
    }

    public async Task<AllDailySummariesReport?> GetAllDailySummariesAsync(CancellationToken ct = default)
    {
        return await FetchJsonAsync<AllDailySummariesReport>("all_daily_summaries.json", ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Enriches monthly matrix and annual summaries with lightning and solar radiation metrics
    /// computed from the full daily summaries dataset.
    /// </summary>
    public static void EnrichWithDailySummaries(
        MonthlyYearlyComparisonsReport? comparisons,
        List<DailyWeatherRecord>? dailySummaries)
    {
        if (comparisons == null || dailySummaries == null || dailySummaries.Count == 0)
            return;

        // Group daily records by (Year, Month)
        var parsedDaily = new List<(DateTime Date, DailyWeatherRecord Record)>();
        foreach (var r in dailySummaries)
        {
            if (DateTime.TryParse(r.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                parsedDaily.Add((dt, r));
            }
        }

        var yearMonthMap = parsedDaily
            .GroupBy(x => (x.Date.Year, x.Date.Month))
            .ToDictionary(g => g.Key, g => g.Select(x => x.Record).ToList());

        var yearMap = parsedDaily
            .GroupBy(x => x.Date.Year)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Record).ToList());

        // 1. Enrich monthByMonthMultiYearComparison
        foreach (var (monthKey, monthItem) in comparisons.MonthByMonthMultiYearComparison)
        {
            int mNum = monthItem.MonthNumber;
            var lightningYearAverages = new List<int>();
            var solarYearAverages = new List<double>();

            foreach (var yData in monthItem.Years)
            {
                if (yearMonthMap.TryGetValue((yData.Year, mNum), out var daysInMonth))
                {
                    yData.LightningStrikes = daysInMonth.Sum(d => d.LightningStrikes);
                    yData.LightningDays = daysInMonth.Count(d => d.LightningStrikes > 0);
                    
                    var distList = daysInMonth.Where(d => d.LightningMinDistanceKm.HasValue)
                        .Select(d => d.LightningMinDistanceKm!.Value).ToList();
                    yData.LightningMinDistanceKm = distList.Count > 0 ? Math.Round(distList.Min(), 1) : null;

                    var solarList = daysInMonth.Where(d => d.SolarRadiationAvg.HasValue)
                        .Select(d => d.SolarRadiationAvg!.Value).ToList();
                    if (solarList.Count > 0)
                    {
                        yData.AvgSolarRadiation = Math.Round(solarList.Average(), 1);
                        yData.MaxSolarRadiation = Math.Round(solarList.Max(), 1);
                    }

                    var uvList = daysInMonth.Where(d => d.UvAvg.HasValue)
                        .Select(d => d.UvAvg!.Value).ToList();
                    if (uvList.Count > 0)
                    {
                        yData.AvgUvIndex = Math.Round(uvList.Average(), 1);
                    }

                    lightningYearAverages.Add(yData.LightningStrikes);
                    if (yData.AvgSolarRadiation.HasValue)
                        solarYearAverages.Add(yData.AvgSolarRadiation.Value);
                }
            }

            if (monthItem.HistoricalAverage != null)
            {
                if (lightningYearAverages.Count > 0)
                    monthItem.HistoricalAverage.AvgLightningStrikes = Math.Round(lightningYearAverages.Average(), 1);
                if (solarYearAverages.Count > 0)
                    monthItem.HistoricalAverage.AvgSolarRadiation = Math.Round(solarYearAverages.Average(), 1);
            }
        }

        // 2. Enrich monthlySummaries list
        foreach (var mSum in comparisons.MonthlySummaries)
        {
            if (yearMonthMap.TryGetValue((mSum.Year, mSum.Month), out var daysInMonth))
            {
                mSum.LightningStrikes = daysInMonth.Sum(d => d.LightningStrikes);
                mSum.LightningDays = daysInMonth.Count(d => d.LightningStrikes > 0);

                var distList = daysInMonth.Where(d => d.LightningMinDistanceKm.HasValue)
                    .Select(d => d.LightningMinDistanceKm!.Value).ToList();
                mSum.LightningMinDistanceKm = distList.Count > 0 ? Math.Round(distList.Min(), 1) : null;

                var solarList = daysInMonth.Where(d => d.SolarRadiationAvg.HasValue)
                    .Select(d => d.SolarRadiationAvg!.Value).ToList();
                if (solarList.Count > 0)
                {
                    mSum.AvgSolarRadiation = Math.Round(solarList.Average(), 1);
                    mSum.MaxSolarRadiation = Math.Round(solarList.Max(), 1);
                }

                var uvList = daysInMonth.Where(d => d.UvAvg.HasValue)
                    .Select(d => d.UvAvg!.Value).ToList();
                if (uvList.Count > 0)
                {
                    mSum.AvgUvIndex = Math.Round(uvList.Average(), 1);
                }
            }
        }

        // 3. Enrich annualSummaries list
        foreach (var aSum in comparisons.AnnualSummaries)
        {
            if (yearMap.TryGetValue(aSum.Year, out var daysInYear))
            {
                aSum.LightningStrikesCount = daysInYear.Sum(d => d.LightningStrikes);
                aSum.LightningDaysCount = daysInYear.Count(d => d.LightningStrikes > 0);

                var distList = daysInYear.Where(d => d.LightningMinDistanceKm.HasValue)
                    .Select(d => d.LightningMinDistanceKm!.Value).ToList();
                aSum.LightningMinDistanceKm = distList.Count > 0 ? Math.Round(distList.Min(), 1) : null;
            }
        }
    }

    private async Task<T?> FetchJsonAsync<T>(string relativeUri, CancellationToken ct) where T : class
    {
        try
        {
            var url = _baseUrl + relativeUri;
            var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            var jsonStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<T>(jsonStream, JsonOptions, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to fetch {relativeUri}: {ex.Message}");
            return null;
        }
    }
}
