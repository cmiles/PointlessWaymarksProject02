using System;

namespace WaterDaze.Models;

public class DailyFlowRecord
{
    public DateTime Date { get; set; }
    public bool HasData { get; set; }
    public bool HasFlow { get; set; }
    public double? MeanFlow { get; set; }
    public string Qualifiers { get; set; } = string.Empty;
    public string SiteCode { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
}