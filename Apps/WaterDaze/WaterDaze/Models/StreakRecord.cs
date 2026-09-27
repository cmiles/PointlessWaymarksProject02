using System;

namespace WaterDaze.Models;

public class StreakRecord
{
    public string DisplayText => $"{LengthDays} days ({StartDate:yyyy-MM-dd} → {EndDate:yyyy-MM-dd})";
    public DateTime EndDate { get; set; }
    public int LengthDays { get; set; }
    public int Rank { get; set; }
    public DateTime StartDate { get; set; }
}