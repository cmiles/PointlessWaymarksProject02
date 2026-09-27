using System;
using System.Collections.Generic;

namespace WaterDaze.Models;

public class GaugeDashboardData
{
    public List<MonthSummaryRecord> ClimatologySummaries { get; set; } = [];
    public List<DailyFlowRecord> DailyRecords { get; set; } = [];
    public List<MonthlyFlowRecord> MonthlyRecords { get; set; } = [];
    public DateTime? RecordEndDate { get; set; }

    public DateTime? RecordStartDate { get; set; }
    public GaugeSite Site { get; set; } = new();
    public List<YearlyFlowRecord> TopDriestYears { get; set; } = [];
    public List<StreakRecord> TopDryStreaks { get; set; } = [];

    public List<DailyPeakFlowRecord> TopPeakFlowDays { get; set; } = [];

    public List<StreakRecord> TopWetStreaks { get; set; } = [];

    public List<YearlyFlowRecord> TopWettestYears { get; set; } = [];
    public int TotalDaysWithData { get; set; }
    public int TotalDaysWithFlow { get; set; }
    public List<int> Years { get; set; } = [];
}