using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HistoricTempests.Models;

public class MonthlySummaryRecord
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("month")]
    public int Month { get; set; }

    [JsonPropertyName("monthName")]
    public string MonthName { get; set; } = string.Empty;

    [JsonPropertyName("daysCount")]
    public int DaysCount { get; set; }

    [JsonPropertyName("temperature")]
    public MonthlyTempInfo? Temperature { get; set; }

    [JsonPropertyName("precipitation")]
    public MonthlyPrecipInfo? Precipitation { get; set; }

    [JsonPropertyName("wind")]
    public MonthlyWindInfo? Wind { get; set; }

    // Supplemented from daily summaries
    public int LightningStrikes { get; set; }
    public int LightningDays { get; set; }
    public double? LightningMinDistanceKm { get; set; }
    public double? AvgSolarRadiation { get; set; }
    public double? MaxSolarRadiation { get; set; }
    public double? AvgUvIndex { get; set; }
}

public class MonthlyTempInfo
{
    [JsonPropertyName("avgC")]
    public double? AvgC { get; set; }

    [JsonPropertyName("avgF")]
    public double? AvgF { get; set; }

    [JsonPropertyName("meanHighC")]
    public double? MeanHighC { get; set; }

    [JsonPropertyName("meanHighF")]
    public double? MeanHighF { get; set; }

    [JsonPropertyName("meanLowC")]
    public double? MeanLowC { get; set; }

    [JsonPropertyName("meanLowF")]
    public double? MeanLowF { get; set; }

    [JsonPropertyName("maxC")]
    public double? MaxC { get; set; }

    [JsonPropertyName("maxF")]
    public double? MaxF { get; set; }

    [JsonPropertyName("minC")]
    public double? MinC { get; set; }

    [JsonPropertyName("minF")]
    public double? MinF { get; set; }
}

public class MonthlyPrecipInfo
{
    [JsonPropertyName("totalMm")]
    public double TotalMm { get; set; }

    [JsonPropertyName("totalInches")]
    public double TotalInches { get; set; }

    [JsonPropertyName("rainyDays")]
    public int RainyDays { get; set; }

    [JsonPropertyName("maxDailyMm")]
    public double MaxDailyMm { get; set; }

    [JsonPropertyName("maxDailyInches")]
    public double MaxDailyInches { get; set; }

    [JsonPropertyName("maxDailyDate")]
    public string? MaxDailyDate { get; set; }
}

public class MonthlyWindInfo
{
    [JsonPropertyName("avgMph")]
    public double? AvgMph { get; set; }

    [JsonPropertyName("maxGustMph")]
    public double? MaxGustMph { get; set; }
}
