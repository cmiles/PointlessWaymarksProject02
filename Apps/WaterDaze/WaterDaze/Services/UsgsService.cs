using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WaterDaze.Models;

namespace WaterDaze.Services;

public class UsgsService(HttpClient? httpClient = null) : IUsgsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient = httpClient ?? new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public async Task<List<DailyFlowRecord>> FetchDailyValuesAsync(string siteCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(siteCode)) return [];

        // Target USGS NWIS Daily Values endpoint for ~75 years (3900 weeks)
        var url = $"https://waterservices.usgs.gov/nwis/dv/?format=json&sites={siteCode}&period=P3900W&siteStatus=all";

        using var response = await _httpClient
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var usgsResponse = await JsonSerializer
            .DeserializeAsync<UsgsDailyValueResponse>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);

        if (usgsResponse?.Value?.TimeSeries == null || usgsResponse.Value.TimeSeries.Count == 0) return [];

        var rawPoints = new List<DailyFlowRecord>();

        foreach (var ts in usgsResponse.Value.TimeSeries)
        {
            var site = ts.SourceInfo?.SiteCode?.FirstOrDefault()?.Value ?? siteCode;
            var siteName = ts.SourceInfo?.SiteName ?? string.Empty;

            // Streamflow is variable 00060 (Discharge, cfs)
            // If variable code is present and there are multiple series, we prefer discharge
            if (ts.Values == null) continue;

            foreach (var container in ts.Values)
            {
                if (container.Value == null) continue;

                foreach (var pt in container.Value)
                {
                    if (string.IsNullOrWhiteSpace(pt.DateTime)) continue;

                    if (!DateTime.TryParse(pt.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.None,
                            out var parsedDate)) continue;

                    double? flowVal = null;
                    var hasValidVal = false;

                    if (!string.IsNullOrWhiteSpace(pt.Value) &&
                        double.TryParse(pt.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedVal))
                        // USGS uses -999999 for missing values or invalid states
                        if (parsedVal > -900000)
                        {
                            flowVal = parsedVal;
                            hasValidVal = true;
                        }

                    var quals = pt.Qualifiers != null ? string.Join(",", pt.Qualifiers) : string.Empty;

                    rawPoints.Add(new DailyFlowRecord
                    {
                        Date = parsedDate.Date,
                        MeanFlow = flowVal,
                        HasData = hasValidVal,
                        HasFlow = hasValidVal && flowVal > 0,
                        SiteCode = site,
                        SiteName = siteName,
                        Qualifiers = quals
                    });
                }
            }
        }

        if (rawPoints.Count == 0) return [];

        // Group by Date in case multiple time series returned points for the same date
        var grouped = rawPoints
            .GroupBy(p => p.Date)
            .Select(g =>
            {
                var dataPoints = g.Where(p => p is { HasData: true, MeanFlow: not null }).ToList();
                var hasData = dataPoints.Count > 0;
                var avgFlow = hasData ? dataPoints.Average(p => p.MeanFlow!.Value) : (double?)null;
                var hasFlow = hasData && dataPoints.Any(p => p.MeanFlow!.Value > 0);
                var first = g.First();

                return new DailyFlowRecord
                {
                    Date = g.Key,
                    MeanFlow = avgFlow,
                    HasData = hasData,
                    HasFlow = hasFlow,
                    SiteCode = first.SiteCode,
                    SiteName = first.SiteName,
                    Qualifiers = string.Join(";",
                        g.Select(p => p.Qualifiers).Where(q => !string.IsNullOrEmpty(q)).Distinct())
                };
            })
            .OrderBy(p => p.Date)
            .ToList();

        return grouped;
    }
}