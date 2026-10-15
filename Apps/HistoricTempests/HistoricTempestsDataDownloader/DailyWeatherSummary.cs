namespace HistoricTempestsDataDownloader;

public class DailyWeatherSummary
{
    // Date string formatted as YYYY-MM-DD in station local timezone
    public string Date { get; set; } = string.Empty;
    public long StationId { get; set; }
    public long DeviceId { get; set; }

    // Pressure (millibars)
    public double? PressureAvg { get; set; }
    public double? PressureHigh { get; set; }
    public double? PressureLow { get; set; }

    // Temperature (Celsius)
    public double? TemperatureAvg { get; set; }
    public double? TemperatureHigh { get; set; }
    public double? TemperatureLow { get; set; }

    // Temperature (Fahrenheit)
    public double? TemperatureAvgF => UnitConversions.CelsiusToFahrenheit(TemperatureAvg);
    public double? TemperatureHighF => UnitConversions.CelsiusToFahrenheit(TemperatureHigh);
    public double? TemperatureLowF => UnitConversions.CelsiusToFahrenheit(TemperatureLow);

    // Relative Humidity (%)
    public double? HumidityAvg { get; set; }
    public double? HumidityHigh { get; set; }
    public double? HumidityLow { get; set; }

    // Illuminance (lux)
    public double? LuxAvg { get; set; }
    public double? LuxHigh { get; set; }
    public double? LuxLow { get; set; }

    // UV Index
    public double? UvAvg { get; set; }
    public double? UvHigh { get; set; }
    public double? UvLow { get; set; }

    // Solar Radiation (W/m^2)
    public double? SolarRadiationAvg { get; set; }
    public double? SolarRadiationHigh { get; set; }
    public double? SolarRadiationLow { get; set; }

    // Wind (m/s)
    public double? WindAvg { get; set; }
    public double? WindGust { get; set; }
    public double? WindLull { get; set; }
    public double? WindDirectionAvg { get; set; }

    // Wind (mph)
    public double? WindAvgMph => UnitConversions.MetersPerSecondToMph(WindAvg);
    public double? WindGustMph => UnitConversions.MetersPerSecondToMph(WindGust);
    public double? WindLullMph => UnitConversions.MetersPerSecondToMph(WindLull);

    // Lightning
    public int? LightningStrikeCount { get; set; }
    public double? LightningStrikeAvgDistanceKm { get; set; }

    // Quality / Observation counts
    public int? RecordCount { get; set; }
    public double? BatteryAvg { get; set; }

    // Precipitation (mm)
    public double? PrecipitationAccumulationMm { get; set; }
    public double? PrecipitationFinalMm { get; set; } // Rain Check calibrated NC rain
    public double? PrecipitationMinutes { get; set; }
    public double? PrecipitationMinutesFinal { get; set; }
    public int? PrecipitationType { get; set; }

    // Precipitation (inches)
    public double? PrecipitationAccumulationInches => UnitConversions.MillimetersToInches(PrecipitationAccumulationMm);
    public double? PrecipitationFinalInches => UnitConversions.MillimetersToInches(PrecipitationFinalMm);

    // Best precipitation value (prefers final RainCheck NC if available, otherwise raw accumulation)
    public double BestPrecipitationMm => PrecipitationFinalMm ?? PrecipitationAccumulationMm ?? 0.0;
    public double BestPrecipitationInches => UnitConversions.MillimetersToInches(BestPrecipitationMm);

    // Timestamps
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    // Helper to get DateOnly
    public DateOnly ToDateOnly() => DateOnly.Parse(Date);
}
