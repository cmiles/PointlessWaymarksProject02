using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HistoricTempests.Models;

public class OverviewReport
{
    [JsonPropertyName("station")]
    public StationMetadata? Station { get; set; }

    [JsonPropertyName("timeRange")]
    public TimeRangeInfo? TimeRange { get; set; }

    [JsonPropertyName("allTimeRecords")]
    public AllTimeRecordsInfo? AllTimeRecords { get; set; }

    [JsonPropertyName("availableReports")]
    public List<string> AvailableReports { get; set; } = [];

    [JsonPropertyName("generatedUtc")]
    public string GeneratedUtc { get; set; } = string.Empty;
}

public class TimeRangeInfo
{
    [JsonPropertyName("startDate")]
    public string StartDate { get; set; } = string.Empty;

    [JsonPropertyName("endDate")]
    public string EndDate { get; set; } = string.Empty;

    [JsonPropertyName("totalDaysRecorded")]
    public int TotalDaysRecorded { get; set; }

    [JsonPropertyName("asOfLocal")]
    public string AsOfLocal { get; set; } = string.Empty;
}

public class AllTimeRecordsInfo
{
    [JsonPropertyName("hottestDay")]
    public TempRecord? HottestDay { get; set; }

    [JsonPropertyName("coldestDay")]
    public TempRecord? ColdestDay { get; set; }

    [JsonPropertyName("rainiestDay")]
    public PrecipRecord? RainiestDay { get; set; }

    [JsonPropertyName("windiestDay")]
    public WindRecord? WindiestDay { get; set; }

    [JsonPropertyName("totalPrecipitation")]
    public TotalPrecipRecord? TotalPrecipitation { get; set; }
}

public class TempRecord
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("tempF")]
    public double TempF { get; set; }

    [JsonPropertyName("tempC")]
    public double TempC { get; set; }
}

public class PrecipRecord
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("precipInches")]
    public double PrecipInches { get; set; }

    [JsonPropertyName("precipMm")]
    public double PrecipMm { get; set; }
}

public class WindRecord
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("gustMph")]
    public double GustMph { get; set; }

    [JsonPropertyName("gustMps")]
    public double GustMps { get; set; }
}

public class TotalPrecipRecord
{
    [JsonPropertyName("totalInches")]
    public double TotalInches { get; set; }

    [JsonPropertyName("totalMm")]
    public double TotalMm { get; set; }

    [JsonPropertyName("rainyDaysCount")]
    public int RainyDaysCount { get; set; }
}
