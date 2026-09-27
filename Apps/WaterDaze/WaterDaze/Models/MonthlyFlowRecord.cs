using System;

namespace WaterDaze.Models;

public class MonthlyFlowRecord
{
    public int DaysWithData { get; set; }
    public int DaysWithFlow { get; set; }
    public double MaxMeanFlow { get; set; }
    public double? MeanFlow { get; set; }
    public int Month { get; set; }
    public DateTime MonthDate { get; set; }
    public double? Q25Flow { get; set; }
    public double? Q75Flow { get; set; }
    public int Year { get; set; }
}