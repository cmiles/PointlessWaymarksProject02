using System;
using System.Collections.Generic;

namespace PointedHorizons.Models;

public record CelestialEvent(
    DateTime LocalTime,
    double Azimuth,
    double Altitude,
    string TimeText,
    string AzimuthText,
    bool IsRise
);

public class CelestialArcPoint
{
    public double Azimuth { get; set; }
    public double Altitude { get; set; }
    public double HourLocal { get; set; }
}

public class CelestialDayData
{
    public DateTime LocalDate { get; set; }
    public string LocalDateText { get; set; } = string.Empty;

    public CelestialEvent? TopographicSunrise { get; set; }
    public CelestialEvent? TopographicSunset { get; set; }
    public CelestialEvent? TopographicMoonrise { get; set; }
    public CelestialEvent? TopographicMoonset { get; set; }

    public CelestialEvent? AstronomicalSunrise { get; set; }
    public CelestialEvent? AstronomicalSunset { get; set; }

    public string MoonPhaseName { get; set; } = string.Empty;
    public double MoonIllumination { get; set; }

    public (double Azimuth, double Altitude)? CurrentSun { get; set; }
    public (double Azimuth, double Altitude)? CurrentMoon { get; set; }

    public List<CelestialArcPoint> SunArc { get; set; } = [];
    public List<CelestialArcPoint> MoonArc { get; set; } = [];
}

