using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HistoricTempests.Models;

public class AllDailySummariesReport
{
    [JsonPropertyName("station")]
    public StationMetadata? Station { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("dailySummaries")]
    public List<DailyWeatherRecord> DailySummaries { get; set; } = [];
}

public class DailyWeatherRecord
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("tempAvgF")]
    public double? TempAvgF { get; set; }

    [JsonPropertyName("tempHighF")]
    public double? TempHighF { get; set; }

    [JsonPropertyName("tempLowF")]
    public double? TempLowF { get; set; }

    [JsonPropertyName("tempAvgC")]
    public double? TempAvgC { get; set; }

    [JsonPropertyName("tempHighC")]
    public double? TempHighC { get; set; }

    [JsonPropertyName("tempLowC")]
    public double? TempLowC { get; set; }

    [JsonPropertyName("precipInches")]
    public double PrecipInches { get; set; }

    [JsonPropertyName("precipMm")]
    public double PrecipMm { get; set; }

    [JsonPropertyName("windAvgMph")]
    public double? WindAvgMph { get; set; }

    [JsonPropertyName("windGustMph")]
    public double? WindGustMph { get; set; }

    [JsonPropertyName("windDirDeg")]
    public double? WindDirDeg { get; set; }

    [JsonPropertyName("pressureMb")]
    public double? PressureMb { get; set; }

    [JsonPropertyName("humidityAvg")]
    public double? HumidityAvg { get; set; }

    [JsonPropertyName("solarRadiationAvg")]
    public double? SolarRadiationAvg { get; set; }

    [JsonPropertyName("uvAvg")]
    public double? UvAvg { get; set; }

    [JsonPropertyName("lightningStrikes")]
    public int LightningStrikes { get; set; }

    // Support optional min distance if present or added
    [JsonPropertyName("lightningMinDistanceKm")]
    public double? LightningMinDistanceKm { get; set; }

    [JsonPropertyName("lightningAvgDistanceKm")]
    public double? LightningAvgDistanceKm { get; set; }
}
