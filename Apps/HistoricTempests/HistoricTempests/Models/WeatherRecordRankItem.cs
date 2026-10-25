using System;
using System.Text.Json.Serialization;

namespace HistoricTempests.Models;

public class WeatherRecordRankItem
{
    public int Rank { get; set; }
    public string RankDisplay => $"#{Rank}";
    public string Date { get; set; } = string.Empty;
    public string FormattedDate { get; set; } = string.Empty;
    public string ValueDisplay { get; set; } = string.Empty;
    public string SecondaryInfo { get; set; } = string.Empty;

    [JsonIgnore]
    public Services.ThemeManager Theme => Services.ThemeManager.Current;
}

public class WeatherStreakRecord
{
    public int Rank { get; set; }
    public string RankDisplay => $"#{Rank}";
    public string StreakType { get; set; } = string.Empty; // "Dry" or "Wet"
    public int DaysCount { get; set; }
    public string DaysCountDisplay => $"{DaysCount} {(DaysCount == 1 ? "day" : "days")}";
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string DateRangeDisplay { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;

    [JsonIgnore]
    public Services.ThemeManager Theme => Services.ThemeManager.Current;
}
