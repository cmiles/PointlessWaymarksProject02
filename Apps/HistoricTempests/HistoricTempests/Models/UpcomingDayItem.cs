using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HistoricTempests.Models;

public class UpcomingWeekReport
{
    [JsonPropertyName("station")]
    public StationMetadata? Station { get; set; }

    [JsonPropertyName("generatedFromReferenceDate")]
    public string GeneratedFromReferenceDate { get; set; } = string.Empty;

    [JsonPropertyName("upcomingWeek")]
    public List<UpcomingDayItem> UpcomingWeek { get; set; } = [];
}

public class UpcomingDayItem
{
    [JsonPropertyName("dayOffset")]
    public int DayOffset { get; set; }

    [JsonPropertyName("upcomingDate")]
    public string UpcomingDate { get; set; } = string.Empty;

    [JsonPropertyName("calendarDay")]
    public string CalendarDay { get; set; } = string.Empty;

    [JsonPropertyName("dayOfWeek")]
    public string DayOfWeek { get; set; } = string.Empty;

    [JsonPropertyName("displayDate")]
    public string DisplayDate { get; set; } = string.Empty;

    [JsonPropertyName("previousYearWeather")]
    public UpcomingPreviousYearWeather? PreviousYearWeather { get; set; }

    [JsonPropertyName("allHistoricalYears")]
    public List<UpcomingHistoricalYearItem> AllHistoricalYears { get; set; } = [];

    [JsonPropertyName("historicalNormal")]
    public UpcomingHistoricalNormal? HistoricalNormal { get; set; }
}

public class UpcomingPreviousYearWeather
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("temperatureHighF")]
    public double? TemperatureHighF { get; set; }

    [JsonPropertyName("temperatureHighC")]
    public double? TemperatureHighC { get; set; }

    [JsonPropertyName("temperatureLowF")]
    public double? TemperatureLowF { get; set; }

    [JsonPropertyName("temperatureLowC")]
    public double? TemperatureLowC { get; set; }

    [JsonPropertyName("temperatureAvgF")]
    public double? TemperatureAvgF { get; set; }

    [JsonPropertyName("temperatureAvgC")]
    public double? TemperatureAvgC { get; set; }

    [JsonPropertyName("precipInches")]
    public double PrecipInches { get; set; }

    [JsonPropertyName("precipMm")]
    public double PrecipMm { get; set; }

    [JsonPropertyName("windGustMph")]
    public double? WindGustMph { get; set; }

    [JsonPropertyName("windAvgMph")]
    public double? WindAvgMph { get; set; }

    [JsonPropertyName("humidityAvg")]
    public double? HumidityAvg { get; set; }

    [JsonPropertyName("weatherDescription")]
    public string WeatherDescription { get; set; } = string.Empty;
}

public class UpcomingHistoricalYearItem
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("temperatureHighF")]
    public double? TemperatureHighF { get; set; }

    [JsonPropertyName("temperatureHighC")]
    public double? TemperatureHighC { get; set; }

    [JsonPropertyName("temperatureLowF")]
    public double? TemperatureLowF { get; set; }

    [JsonPropertyName("temperatureLowC")]
    public double? TemperatureLowC { get; set; }

    [JsonPropertyName("temperatureAvgF")]
    public double? TemperatureAvgF { get; set; }

    [JsonPropertyName("precipInches")]
    public double PrecipInches { get; set; }

    [JsonPropertyName("precipMm")]
    public double PrecipMm { get; set; }

    [JsonPropertyName("windGustMph")]
    public double? WindGustMph { get; set; }

    [JsonPropertyName("weatherDescription")]
    public string WeatherDescription { get; set; } = string.Empty;
}

public class UpcomingHistoricalNormal
{
    [JsonPropertyName("normalHighF")]
    public double? NormalHighF { get; set; }

    [JsonPropertyName("normalLowF")]
    public double? NormalLowF { get; set; }

    [JsonPropertyName("recordHighF")]
    public double? RecordHighF { get; set; }

    [JsonPropertyName("recordHighYear")]
    public int? RecordHighYear { get; set; }

    [JsonPropertyName("recordLowF")]
    public double? RecordLowF { get; set; }

    [JsonPropertyName("recordLowYear")]
    public int? RecordLowYear { get; set; }

    [JsonPropertyName("rainChancePercent")]
    public double RainChancePercent { get; set; }

    [JsonPropertyName("yearsObserved")]
    public int YearsObserved { get; set; }
}
