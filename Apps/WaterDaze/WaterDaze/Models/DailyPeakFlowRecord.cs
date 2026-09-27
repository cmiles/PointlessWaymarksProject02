using System;

namespace WaterDaze.Models;

public class DailyPeakFlowRecord
{
    public DateTime Date { get; set; }
    public string DisplayText => $"{Date:yyyy-MM-dd}: {FormattedFlow} cfs";
    public string FormattedFlow { get; set; } = string.Empty;
    public double MeanFlow { get; set; }
    public int Rank { get; set; }
}