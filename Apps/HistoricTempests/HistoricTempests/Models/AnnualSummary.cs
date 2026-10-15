using System.Text.Json.Serialization;

namespace HistoricTempests.Models;

public class AnnualSummaryRecord
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("daysRecorded")]
    public int DaysRecorded { get; set; }

    [JsonPropertyName("temperature")]
    public AnnualTempInfo? Temperature { get; set; }

    [JsonPropertyName("precipitation")]
    public AnnualPrecipInfo? Precipitation { get; set; }

    [JsonPropertyName("wind")]
    public AnnualWindInfo? Wind { get; set; }

    [JsonPropertyName("solar")]
    public AnnualSolarInfo? Solar { get; set; }

    // Calculated / supplemented from daily summaries
    public int LightningStrikesCount { get; set; }
    public int LightningDaysCount { get; set; }
    public double? LightningMinDistanceKm { get; set; }
}

public class AnnualTempInfo
{
    [JsonPropertyName("avgC")]
    public double? AvgC { get; set; }

    [JsonPropertyName("avgF")]
    public double? AvgF { get; set; }

    [JsonPropertyName("maxC")]
    public double? MaxC { get; set; }

    [JsonPropertyName("maxF")]
    public double? MaxF { get; set; }

    [JsonPropertyName("maxDate")]
    public string? MaxDate { get; set; }

    [JsonPropertyName("minC")]
    public double? MinC { get; set; }

    [JsonPropertyName("minF")]
    public double? MinF { get; set; }

    [JsonPropertyName("minDate")]
    public string? MinDate { get; set; }
}

public class AnnualPrecipInfo
{
    [JsonPropertyName("totalMm")]
    public double TotalMm { get; set; }

    [JsonPropertyName("totalInches")]
    public double TotalInches { get; set; }

    [JsonPropertyName("rainyDaysCount")]
    public int RainyDaysCount { get; set; }

    [JsonPropertyName("maxDailyMm")]
    public double MaxDailyMm { get; set; }

    [JsonPropertyName("maxDailyInches")]
    public double MaxDailyInches { get; set; }

    [JsonPropertyName("maxDailyDate")]
    public string? MaxDailyDate { get; set; }
}

public class AnnualWindInfo
{
    [JsonPropertyName("avgMps")]
    public double? AvgMps { get; set; }

    [JsonPropertyName("avgMph")]
    public double? AvgMph { get; set; }

    [JsonPropertyName("maxGustMps")]
    public double? MaxGustMps { get; set; }

    [JsonPropertyName("maxGustMph")]
    public double? MaxGustMph { get; set; }

    [JsonPropertyName("maxGustDate")]
    public string? MaxGustDate { get; set; }
}

public class AnnualSolarInfo
{
    [JsonPropertyName("avgRadiationWM2")]
    public double? AvgRadiationWM2 { get; set; }
}
