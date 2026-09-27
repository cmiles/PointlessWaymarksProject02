namespace WaterDaze.Models;

public class YearlyFlowRecord
{
    public string DisplayText => $"{Year} — {FormattedFlow} cfs";
    public string FormattedFlow { get; set; } = string.Empty;
    public double MeanFlow { get; set; }
    public int Rank { get; set; }
    public int Year { get; set; }
}