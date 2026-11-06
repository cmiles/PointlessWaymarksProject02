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

public class WindowCelestialEvent
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;

    public CelestialEvent? TopoEvent { get; set; }
    public string TopoTimeDisplay { get; set; } = "--:--";
    public string TopoAzimuthDisplay { get; set; } = "--";
    public string TopoElevationDisplay { get; set; } = "--";

    public CelestialEvent? AstroEvent { get; set; }
    public string AstroTimeDisplay { get; set; } = "--:--";
    public string AstroAzimuthDisplay { get; set; } = "--";

    public string TopoShiftDisplay { get; set; } = string.Empty;

    public bool HasEvent { get; set; }
    public bool IsInWindow { get; set; } = true;
    public bool IsFuture { get; set; }
    public TimeSpan TimeDifference { get; set; }
    public string TimeUntilDisplay { get; set; } = string.Empty;

    public DateTime? EffectiveTime { get; set; }
}

public class CelestialWindowReport
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double ElevationM { get; set; }
    public string CoordinatesDisplay { get; set; } = string.Empty;
    public string ElevationDisplay { get; set; } = string.Empty;
    public string WindowDisplay { get; set; } = string.Empty;
    public DateTime ReferenceTime { get; set; }
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
    public bool HasTopography { get; set; }

    public WindowCelestialEvent? Sunrise { get; set; }
    public WindowCelestialEvent? Sunset { get; set; }
    public WindowCelestialEvent? Moonrise { get; set; }
    public WindowCelestialEvent? Moonset { get; set; }

    public string MoonPhaseName { get; set; } = string.Empty;
    public double MoonIllumination { get; set; }
}

public class CelestialLocationEventArgs : EventArgs
{
    public double Latitude { get; }
    public double Longitude { get; }

    public CelestialLocationEventArgs(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }
}

