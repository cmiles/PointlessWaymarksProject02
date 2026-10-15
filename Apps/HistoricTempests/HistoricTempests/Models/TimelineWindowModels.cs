using System.Collections.Generic;

namespace HistoricTempests.Models;

public class YearPointData
{
    public int Year { get; set; }
    public double? MaxTempF { get; set; }
    public double? MinTempF { get; set; }
    public double? AvgTempF { get; set; }
    public double? MaxTempC { get; set; }
    public double? MinTempC { get; set; }
    public double? AvgTempC { get; set; }
    public double RainIn { get; set; }
    public double RainMm { get; set; }
    public double? GustMph { get; set; }
    public double? GustKmh { get; set; }
    public int LightningStrikes { get; set; }
    public double? LightningMinDistanceKm { get; set; }
    public double? SolarRadiationAvg { get; set; }
    public double? UvAvg { get; set; }
}

public class RainDaysYearSummary
{
    public int Year { get; set; }
    public int DaysRecorded { get; set; }
    public string DaysRecordedDisplay => $"{DaysRecorded} days";
    public double TotalRainInches { get; set; }
    public int RainDays { get; set; }
    public int TotalLightningStrikes { get; set; }
    public int LightningDays { get; set; }
    public double MaxDailyRainInches { get; set; }
    public string MaxDailyRainDate { get; set; } = string.Empty;
    public int MaxDailyStrikes { get; set; }
    public string MaxDailyStrikesDate { get; set; } = string.Empty;
}

public class DayTimelinePoint
{
    public int DayOffset { get; set; } // -14 to +14
    public string CalendarDay { get; set; } = string.Empty; // "10-11"
    public string DisplayDate { get; set; } = string.Empty; // "Oct 11"
    public string DayOfWeek { get; set; } = string.Empty;   // "Sun"
    public bool IsUpcoming { get; set; }
    public bool IsToday { get; set; }

    public double? ThisYearMaxTempF { get; set; }
    public double? ThisYearMinTempF { get; set; }
    public double? ThisYearAvgTempF { get; set; }
    public double? ThisYearMaxTempC { get; set; }
    public double? ThisYearMinTempC { get; set; }
    public double? ThisYearAvgTempC { get; set; }
    public double? ThisYearRainIn { get; set; }
    public double? ThisYearRainMm { get; set; }
    public double? ThisYearGustMph { get; set; }
    public double? ThisYearGustKmh { get; set; }
    public int? ThisYearLightningStrikes { get; set; }
    public double? ThisYearLightningMinDistanceKm { get; set; }
    public double? ThisYearSolarRadiationAvg { get; set; }
    public double? ThisYearUvAvg { get; set; }

    public List<YearPointData> PrevYears { get; set; } = [];
}

public class MonthTimelinePoint
{
    public int MonthOffset { get; set; } // -6 to +6
    public int MonthNumber { get; set; } // 1..12
    public string MonthName { get; set; } = string.Empty;
    public string DisplayLabel { get; set; } = string.Empty;
    public bool IsUpcoming { get; set; }
    public bool IsCurrent { get; set; }

    public double? ThisYearMaxTempF { get; set; }
    public double? ThisYearMinTempF { get; set; }
    public double? ThisYearAvgTempF { get; set; }
    public double? ThisYearMaxTempC { get; set; }
    public double? ThisYearMinTempC { get; set; }
    public double? ThisYearAvgTempC { get; set; }
    public double? ThisYearRainIn { get; set; }
    public double? ThisYearRainMm { get; set; }
    public double? ThisYearGustMph { get; set; }
    public double? ThisYearGustKmh { get; set; }
    public int? ThisYearLightningStrikes { get; set; }
    public double? ThisYearLightningMinDistanceKm { get; set; }
    public double? ThisYearSolarRadiationAvg { get; set; }
    public double? ThisYearUvAvg { get; set; }

    public List<YearPointData> PrevYears { get; set; } = [];
}
