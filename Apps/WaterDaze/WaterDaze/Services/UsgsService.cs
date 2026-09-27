using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient = httpClient ?? new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    
    public Task<List<DailyFlowRecord>> FetchDailyValuesAsync(string siteCode,
        CancellationToken cancellationToken = default)
    {
        return FetchDailyValuesAsync(siteCode, null, null, cancellationToken);
    }

    public Task<List<DailyFlowRecord>> FetchDailyValuesAsync(string siteCode,
        DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        return FetchDailyValuesAsync(siteCode, null, endDate, cancellationToken);
    }

    public async Task<List<DailyFlowRecord>> FetchDailyValuesAsync(string siteCode,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(siteCode)) return [];

        return await FetchFromUsgsAsync(siteCode, startDate, endDate, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads daily flow records from a specific local JSON file path.
    /// </summary>
    public async Task<List<DailyFlowRecord>> LoadFromLocalFileAsync(string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return [];

        await using var stream = File.OpenRead(filePath);
        return await DeserializeDailyFlowRecordsAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Fetches daily values directly from the USGS NWIS REST endpoint.
    /// </summary>
    public async Task<List<DailyFlowRecord>> FetchFromUsgsAsync(string siteCode,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(siteCode)) return [];

        var url = BuildUsgsUrl(siteCode, startDate, endDate);

        using var response = await _httpClient
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var usgsResponse = await JsonSerializer
            .DeserializeAsync<UsgsDailyValueResponse>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);

        if (usgsResponse == null) return [];

        var parsed = ParseUsgsResponse(usgsResponse, siteCode);
        if (startDate.HasValue)
        {
            parsed = parsed.Where(r => r.Date >= startDate.Value.Date).ToList();
        }
        if (endDate.HasValue)
        {
            parsed = parsed.Where(r => r.Date <= endDate.Value.Date).ToList();
        }

        return parsed;
    }

    public static string BuildUsgsUrl(string siteCode, DateTime? startDate = null, DateTime? endDate = null)
    {
        if (startDate.HasValue)
        {
            var startParam = $"&startDT={startDate.Value:yyyy-MM-dd}";
            var endParam = endDate.HasValue ? $"&endDT={endDate.Value:yyyy-MM-dd}" : string.Empty;
            return $"https://waterservices.usgs.gov/nwis/dv/?format=json&sites={siteCode}{startParam}{endParam}&siteStatus=all";
        }
        else if (endDate.HasValue)
        {
            return $"https://waterservices.usgs.gov/nwis/dv/?format=json&sites={siteCode}&startDT=1900-01-01&endDT={endDate.Value:yyyy-MM-dd}&siteStatus=all";
        }
        else
        {
            return $"https://waterservices.usgs.gov/nwis/dv/?format=json&sites={siteCode}&period=P3900W&siteStatus=all";
        }
    }

    /// <summary>
    /// Parses a raw USGS NWIS Daily Values response into an aggregated List of DailyFlowRecord.
    /// </summary>
    public static List<DailyFlowRecord> ParseUsgsResponse(UsgsDailyValueResponse usgsResponse, string fallbackSiteCode = "")
    {
        if (usgsResponse?.Value?.TimeSeries == null || usgsResponse.Value.TimeSeries.Count == 0) return [];

        var rawPoints = new List<DailyFlowRecord>();

        foreach (var ts in usgsResponse.Value.TimeSeries)
        {
            var site = ts.SourceInfo?.SiteCode?.FirstOrDefault()?.Value ?? fallbackSiteCode;
            var siteName = ts.SourceInfo?.SiteName ?? string.Empty;

            // Streamflow is variable 00060 (Discharge, cfs)
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
                    {
                        // USGS uses -999999 for missing values or invalid states
                        if (parsedVal > -900000)
                        {
                            flowVal = parsedVal;
                            hasValidVal = true;
                        }
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

    /// <summary>
    /// Serializes a collection of DailyFlowRecord to JSON string.
    /// </summary>
    public static string SerializeDailyFlowRecords(IEnumerable<DailyFlowRecord> records, bool writeIndented = true)
    {
        var options = writeIndented ? SerializerOptions : new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return JsonSerializer.Serialize(records, options);
    }

    /// <summary>
    /// Deserializes a JSON string into a list of DailyFlowRecord, supporting both raw arrays and wrapped JSON objects.
    /// </summary>
    public static List<DailyFlowRecord> DeserializeDailyFlowRecords(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<DailyFlowRecord>>(json, JsonOptions) ?? [];
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("value", out var valProp) && valProp.TryGetProperty("timeSeries", out _))
            {
                var usgsResponse = JsonSerializer.Deserialize<UsgsDailyValueResponse>(json, JsonOptions);
                return usgsResponse != null ? ParseUsgsResponse(usgsResponse) : [];
            }

            foreach (var propName in new[] { "records", "dailyRecords", "data", "dailyFlowRecords", "values" })
            {
                if (root.TryGetProperty(propName, out var arrProp) && arrProp.ValueKind == JsonValueKind.Array)
                {
                    return JsonSerializer.Deserialize<List<DailyFlowRecord>>(arrProp.GetRawText(), JsonOptions) ?? [];
                }
            }
        }

        return [];
    }

    /// <summary>
    /// Deserializes a JSON stream into a list of DailyFlowRecord.
    /// </summary>
    public static async Task<List<DailyFlowRecord>> DeserializeDailyFlowRecordsAsync(Stream stream,
        CancellationToken cancellationToken = default)
    {
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<DailyFlowRecord>>(root.GetRawText(), JsonOptions) ?? [];
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("value", out var valProp) && valProp.TryGetProperty("timeSeries", out _))
            {
                var usgsResponse = JsonSerializer.Deserialize<UsgsDailyValueResponse>(root.GetRawText(), JsonOptions);
                return usgsResponse != null ? ParseUsgsResponse(usgsResponse) : [];
            }

            foreach (var propName in new[] { "records", "dailyRecords", "data", "dailyFlowRecords", "values" })
            {
                if (root.TryGetProperty(propName, out var arrProp) && arrProp.ValueKind == JsonValueKind.Array)
                {
                    return JsonSerializer.Deserialize<List<DailyFlowRecord>>(arrProp.GetRawText(), JsonOptions) ?? [];
                }
            }
        }

        return [];
    }
}