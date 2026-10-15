using System.Net.Http.Json;
using System.Text.Json;

namespace HistoricTempestsDataDownloader;

public class TempestApiClient
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://swd.weatherflow.com/swd/rest";

    public TempestApiClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "HistoricTempestsDataDownloader/1.0");
        }
    }

    /// <summary>
    /// Looks up station information containing the specified deviceId.
    /// </summary>
    public async Task<StationMetadata?> GetStationForDeviceAsync(long deviceId, string token, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/stations?token={Uri.EscapeDataString(token)}";
        using var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var root = doc.RootElement;
        if (!root.TryGetProperty("stations", out var stationsArray) || stationsArray.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var stationEl in stationsArray.EnumerateArray())
        {
            if (stationEl.TryGetProperty("devices", out var devicesArray) && devicesArray.ValueKind == JsonValueKind.Array)
            {
                bool matches = false;
                foreach (var devEl in devicesArray.EnumerateArray())
                {
                    if (devEl.TryGetProperty("device_id", out var idProp) && idProp.GetInt64() == deviceId)
                    {
                        matches = true;
                        break;
                    }
                }

                if (matches)
                {
                    var meta = new StationMetadata
                    {
                        StationId = stationEl.GetProperty("station_id").GetInt64(),
                        DeviceId = deviceId,
                        StationName = stationEl.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "",
                        Timezone = stationEl.TryGetProperty("timezone", out var tzProp) ? tzProp.GetString() ?? "UTC" : "UTC",
                        TimezoneOffsetMinutes = stationEl.TryGetProperty("timezone_offset_minutes", out var tzOff) ? tzOff.GetInt32() : 0,
                        Latitude = stationEl.TryGetProperty("latitude", out var lat) ? lat.GetDouble() : 0.0,
                        Longitude = stationEl.TryGetProperty("longitude", out var lon) ? lon.GetDouble() : 0.0,
                        CreatedEpoch = stationEl.TryGetProperty("created_epoch", out var cr) ? cr.GetInt64() : 0
                    };

                    if (stationEl.TryGetProperty("station_meta", out var sm) && sm.TryGetProperty("elevation", out var el))
                    {
                        meta.ElevationMeters = el.GetDouble();
                    }

                    return meta;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Retrieves historical daily summaries from the /stats/device endpoint.
    /// </summary>
    public async Task<(List<DailyWeatherSummary> Summaries, string? FirstLocalDay, string? LastLocalDay)> GetDeviceDailyStatsAsync(
        long deviceId, string token, long stationId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/stats/device/{deviceId}?token={Uri.EscapeDataString(token)}";
        using var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;

        string? firstDay = root.TryGetProperty("first_ob_day_local", out var fd) ? fd.GetString() : null;
        string? lastDay = root.TryGetProperty("last_ob_day_local", out var ld) ? ld.GetString() : null;

        var list = new List<DailyWeatherSummary>();

        if (root.TryGetProperty("stats_day", out var statsArray) && statsArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in statsArray.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() == 0) continue;

                var summary = ParseStatsDayRow(row, stationId, deviceId);
                if (summary != null)
                {
                    list.Add(summary);
                }
            }
        }

        return (list, firstDay, lastDay);
    }

    /// <summary>
    /// Fallback: Retrieves 1-minute observations for a local day and computes the daily summary.
    /// </summary>
    public async Task<DailyWeatherSummary?> GetObservationsDailySummaryAsync(
        long deviceId, DateOnly localDay, TimeZoneInfo tz, string token, long stationId, CancellationToken ct = default)
    {
        // Calculate UTC epoch start and end for station local day
        var localStart = localDay.ToDateTime(new TimeOnly(0, 0, 0));
        var localEnd = localDay.ToDateTime(new TimeOnly(23, 59, 59));

        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
        var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, tz);

        long startEpoch = new DateTimeOffset(utcStart, TimeSpan.Zero).ToUnixTimeSeconds();
        long endEpoch = new DateTimeOffset(utcEnd, TimeSpan.Zero).ToUnixTimeSeconds();

        var url = $"{BaseUrl}/observations/device/{deviceId}?time_start={startEpoch}&time_end={endEpoch}&token={Uri.EscapeDataString(token)}";
        using var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return null;

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;

        if (!root.TryGetProperty("obs", out var obsArray) || obsArray.ValueKind != JsonValueKind.Array || obsArray.GetArrayLength() == 0)
        {
            return null;
        }

        return AggregateObservationsToSummary(obsArray, localDay.ToString("yyyy-MM-dd"), stationId, deviceId);
    }

    private static DailyWeatherSummary? ParseStatsDayRow(JsonElement row, long stationId, long deviceId)
    {
        // Row layout for stats_day:
        // 0: date string (YYYY-MM-DD)
        // 1: pressure avg, 2: pressure high, 3: pressure low
        // 4: temp avg, 5: temp high, 6: temp low
        // 7: humidity avg, 8: humidity high, 9: humidity low
        // 10: lux avg, 11: lux high, 12: lux low
        // 13: uv avg, 14: uv high, 15: uv low
        // 16: solar avg, 17: solar high, 18: solar low
        // 19: wind avg, 20: wind gust, 21: wind lull, 22: wind dir, 23: wind interval
        // 24: strike count, 25: strike avg dist
        // 26: record count, 27: battery
        // 28: precip accum today, 29: precip accum final (NC)
        // 30: precip minutes today, 31: precip minutes final
        // 32: precip type, 33: precip analysis type

        int len = row.GetArrayLength();
        if (len < 7) return null;

        var dateStr = row[0].GetString();
        if (string.IsNullOrWhiteSpace(dateStr)) return null;

        var summary = new DailyWeatherSummary
        {
            Date = dateStr,
            StationId = stationId,
            DeviceId = deviceId,
            PressureAvg = GetDouble(row, 1),
            PressureHigh = GetDouble(row, 2),
            PressureLow = GetDouble(row, 3),
            TemperatureAvg = GetDouble(row, 4),
            TemperatureHigh = GetDouble(row, 5),
            TemperatureLow = GetDouble(row, 6),
            HumidityAvg = GetDouble(row, 7),
            HumidityHigh = GetDouble(row, 8),
            HumidityLow = GetDouble(row, 9),
            LuxAvg = GetDouble(row, 10),
            LuxHigh = GetDouble(row, 11),
            LuxLow = GetDouble(row, 12),
            UvAvg = GetDouble(row, 13),
            UvHigh = GetDouble(row, 14),
            UvLow = GetDouble(row, 15),
            SolarRadiationAvg = GetDouble(row, 16),
            SolarRadiationHigh = GetDouble(row, 17),
            SolarRadiationLow = GetDouble(row, 18),
            WindAvg = GetDouble(row, 19),
            WindGust = GetDouble(row, 20),
            WindLull = GetDouble(row, 21),
            WindDirectionAvg = GetDouble(row, 22),
            LightningStrikeCount = GetInt(row, 24),
            LightningStrikeAvgDistanceKm = GetDouble(row, 25),
            RecordCount = GetInt(row, 26),
            BatteryAvg = GetDouble(row, 27),
            PrecipitationAccumulationMm = GetDouble(row, 28),
            PrecipitationFinalMm = GetDouble(row, 29),
            PrecipitationMinutes = GetDouble(row, 30),
            PrecipitationMinutesFinal = GetDouble(row, 31),
            PrecipitationType = GetInt(row, 32),
            CreatedUtc = DateTime.UtcNow,
            LastUpdatedUtc = DateTime.UtcNow
        };

        return summary;
    }

    private static DailyWeatherSummary AggregateObservationsToSummary(JsonElement obsArray, string dateStr, long stationId, long deviceId)
    {
        var summary = new DailyWeatherSummary
        {
            Date = dateStr,
            StationId = stationId,
            DeviceId = deviceId
        };

        var temps = new List<double>();
        var pressures = new List<double>();
        var hums = new List<double>();
        var luxes = new List<double>();
        var uvs = new List<double>();
        var solars = new List<double>();
        var windAvgs = new List<double>();
        var windGusts = new List<double>();
        var windLulls = new List<double>();
        var windDirs = new List<double>();
        double rainSum = 0;
        double? lastDayRain = null;
        double? lastDayNcRain = null;
        int strikeCount = 0;
        int count = 0;

        foreach (var obs in obsArray.EnumerateArray())
        {
            if (obs.ValueKind != JsonValueKind.Array) continue;
            count++;

            // Index 1: lull, 2: avg, 3: gust, 4: dir, 6: pressure, 7: temp, 8: hum, 9: lux, 10: uv, 11: solar, 12: rain, 15: strike, 18: local day rain, 20: local day nc rain
            var t = GetDouble(obs, 7);
            if (t.HasValue) temps.Add(t.Value);

            var p = GetDouble(obs, 6);
            if (p.HasValue) pressures.Add(p.Value);

            var h = GetDouble(obs, 8);
            if (h.HasValue) hums.Add(h.Value);

            var l = GetDouble(obs, 9);
            if (l.HasValue) luxes.Add(l.Value);

            var u = GetDouble(obs, 10);
            if (u.HasValue) uvs.Add(u.Value);

            var s = GetDouble(obs, 11);
            if (s.HasValue) solars.Add(s.Value);

            var wAvg = GetDouble(obs, 2);
            if (wAvg.HasValue) windAvgs.Add(wAvg.Value);

            var wGust = GetDouble(obs, 3);
            if (wGust.HasValue) windGusts.Add(wGust.Value);

            var wLull = GetDouble(obs, 1);
            if (wLull.HasValue) windLulls.Add(wLull.Value);

            var wDir = GetDouble(obs, 4);
            if (wDir.HasValue) windDirs.Add(wDir.Value);

            var r = GetDouble(obs, 12);
            if (r.HasValue) rainSum += r.Value;

            var ldr = GetDouble(obs, 18);
            if (ldr.HasValue) lastDayRain = ldr.Value;

            var ldnc = GetDouble(obs, 20);
            if (ldnc.HasValue) lastDayNcRain = ldnc.Value;

            var strikes = GetInt(obs, 15);
            if (strikes.HasValue) strikeCount += strikes.Value;
        }

        summary.RecordCount = count;
        if (temps.Count > 0)
        {
            summary.TemperatureAvg = UnitConversions.Round1(temps.Average());
            summary.TemperatureHigh = UnitConversions.Round1(temps.Max());
            summary.TemperatureLow = UnitConversions.Round1(temps.Min());
        }

        if (pressures.Count > 0)
        {
            summary.PressureAvg = UnitConversions.Round1(pressures.Average());
            summary.PressureHigh = UnitConversions.Round1(pressures.Max());
            summary.PressureLow = UnitConversions.Round1(pressures.Min());
        }

        if (hums.Count > 0)
        {
            summary.HumidityAvg = UnitConversions.Round1(hums.Average());
            summary.HumidityHigh = hums.Max();
            summary.HumidityLow = hums.Min();
        }

        if (luxes.Count > 0)
        {
            summary.LuxAvg = UnitConversions.Round1(luxes.Average());
            summary.LuxHigh = luxes.Max();
            summary.LuxLow = luxes.Min();
        }

        if (uvs.Count > 0)
        {
            summary.UvAvg = UnitConversions.Round1(uvs.Average());
            summary.UvHigh = UnitConversions.Round1(uvs.Max());
            summary.UvLow = UnitConversions.Round1(uvs.Min());
        }

        if (solars.Count > 0)
        {
            summary.SolarRadiationAvg = UnitConversions.Round1(solars.Average());
            summary.SolarRadiationHigh = solars.Max();
            summary.SolarRadiationLow = solars.Min();
        }

        if (windAvgs.Count > 0)
        {
            summary.WindAvg = UnitConversions.Round1(windAvgs.Average());
        }

        if (windGusts.Count > 0)
        {
            summary.WindGust = UnitConversions.Round1(windGusts.Max());
        }

        if (windLulls.Count > 0)
        {
            summary.WindLull = UnitConversions.Round1(windLulls.Min());
        }

        if (windDirs.Count > 0)
        {
            summary.WindDirectionAvg = UnitConversions.Round1(windDirs.Average());
        }

        summary.PrecipitationAccumulationMm = lastDayRain ?? UnitConversions.Round3(rainSum);
        summary.PrecipitationFinalMm = lastDayNcRain ?? summary.PrecipitationAccumulationMm;
        summary.LightningStrikeCount = strikeCount;

        return summary;
    }

    private static double? GetDouble(JsonElement array, int index)
    {
        if (index < 0 || index >= array.GetArrayLength()) return null;
        var el = array[index];
        if (el.ValueKind == JsonValueKind.Null || el.ValueKind == JsonValueKind.Undefined) return null;
        if (el.ValueKind == JsonValueKind.Number) return el.GetDouble();
        if (el.ValueKind == JsonValueKind.String && double.TryParse(el.GetString(), out var d)) return d;
        return null;
    }

    private static int? GetInt(JsonElement array, int index)
    {
        if (index < 0 || index >= array.GetArrayLength()) return null;
        var el = array[index];
        if (el.ValueKind == JsonValueKind.Null || el.ValueKind == JsonValueKind.Undefined) return null;
        if (el.ValueKind == JsonValueKind.Number) return el.GetInt32();
        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var i)) return i;
        return null;
    }
}
