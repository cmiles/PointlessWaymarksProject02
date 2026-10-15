using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HistoricTempests.Models;

public class MonthMatrixItem
{
    [JsonPropertyName("monthNumber")]
    public int MonthNumber { get; set; }

    [JsonPropertyName("monthName")]
    public string MonthName { get; set; } = string.Empty;

    [JsonPropertyName("historicalAverage")]
    public MonthHistoricalAverage? HistoricalAverage { get; set; }

    [JsonPropertyName("years")]
    public List<MonthYearData> Years { get; set; } = [];
}

public class MonthHistoricalAverage
{
    [JsonPropertyName("avgTempF")]
    public double? AvgTempF { get; set; }

    [JsonPropertyName("avgTempC")]
    public double? AvgTempC { get; set; }

    [JsonPropertyName("avgPrecipInches")]
    public double? AvgPrecipInches { get; set; }

    [JsonPropertyName("avgPrecipMm")]
    public double? AvgPrecipMm { get; set; }

    // Supplemented
    public double? AvgLightningStrikes { get; set; }
    public double? AvgSolarRadiation { get; set; }
}

public class MonthYearData
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("daysRecorded")]
    public int DaysRecorded { get; set; }

    [JsonPropertyName("avgTempF")]
    public double? AvgTempF { get; set; }

    [JsonPropertyName("avgTempC")]
    public double? AvgTempC { get; set; }

    [JsonPropertyName("maxTempF")]
    public double? MaxTempF { get; set; }

    [JsonPropertyName("minTempF")]
    public double? MinTempF { get; set; }

    [JsonPropertyName("totalPrecipInches")]
    public double TotalPrecipInches { get; set; }

    [JsonPropertyName("totalPrecipMm")]
    public double TotalPrecipMm { get; set; }

    [JsonPropertyName("rainyDays")]
    public int RainyDays { get; set; }

    [JsonPropertyName("maxGustMph")]
    public double? MaxGustMph { get; set; }

    // Supplemented from daily summaries
    public int LightningStrikes { get; set; }
    public int LightningDays { get; set; }
    public double? LightningMinDistanceKm { get; set; }
    public double? AvgSolarRadiation { get; set; }
    public double? MaxSolarRadiation { get; set; }
    public double? AvgUvIndex { get; set; }
}
