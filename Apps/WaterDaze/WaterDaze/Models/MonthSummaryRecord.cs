namespace WaterDaze.Models;

public class MonthSummaryRecord
{
    public double? MaxFlow { get; set; }
    public double? MedianFlow { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public double? Q25Flow { get; set; }
    public double? Q75Flow { get; set; }
}