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

    public TwilightReport? Twilight { get; set; }
    public PhotographyHoursReport? PhotographyHours { get; set; }
    public SolarNoonReport? SolarNoon { get; set; }
    public MilkyWayReport? MilkyWay { get; set; }
    public SeasonMarkerReport? SeasonMarker { get; set; }
}

public class TwilightPhaseItem
{
    public string Name { get; set; } = string.Empty;
    public string TimeDisplay { get; set; } = string.Empty;
    public string AngleDisplay { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public bool IsPast { get; set; }
    public bool IsTopographic { get; set; }
}

public class TwilightReport
{
    public string TrailLightWindowDisplay { get; set; } = string.Empty;
    public string TrailLightDurationDisplay { get; set; } = string.Empty;
    public string TrailLightNote { get; set; } = string.Empty;

    public TwilightPhaseItem? AstroDawn { get; set; }
    public TwilightPhaseItem? NauticalDawn { get; set; }
    public TwilightPhaseItem? CivilDawn { get; set; }
    public TwilightPhaseItem? MountainSunrise { get; set; }

    public TwilightPhaseItem? MountainSunset { get; set; }
    public TwilightPhaseItem? CivilDusk { get; set; }
    public TwilightPhaseItem? NauticalDusk { get; set; }
    public TwilightPhaseItem? AstroDusk { get; set; }
}

public class PhotographyWindowItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string TimeWindowDisplay { get; set; } = string.Empty;
    public string SubTitle { get; set; } = string.Empty;
    public string DirectSunDisplay { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CountdownDisplay { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsFuture { get; set; }
    public bool IsPast { get; set; }
    public bool HasWindow { get; set; }
}

public class PhotographyHoursReport
{
    public string MorningBlueHourDisplay { get; set; } = string.Empty;
    public string MorningGoldenHourDisplay { get; set; } = string.Empty;
    public string MorningDirectSunDisplay { get; set; } = string.Empty;

    public string EveningDirectSunDisplay { get; set; } = string.Empty;
    public string EveningGoldenHourDisplay { get; set; } = string.Empty;
    public string EveningBlueHourDisplay { get; set; } = string.Empty;

    public string TopoPhotoNote { get; set; } = string.Empty;

    public PhotographyWindowItem? MorningBlue { get; set; }
    public PhotographyWindowItem? MorningGolden { get; set; }
    public PhotographyWindowItem? EveningGolden { get; set; }
    public PhotographyWindowItem? EveningBlue { get; set; }
}

public class SolarNoonReport
{
    public DateTime? SolarNoonTime { get; set; }
    public string NoonTimeDisplay { get; set; } = string.Empty;
    public string MaxElevationDisplay { get; set; } = string.Empty;
    public string NoonCountdownDisplay { get; set; } = string.Empty;
    public bool IsNoonPast { get; set; }
    public bool IsNoonActive { get; set; }

    public string TopoDaylightDisplay { get; set; } = string.Empty;
    public string FlatDaylightDisplay { get; set; } = string.Empty;
    public string TopoLostDisplay { get; set; } = string.Empty;

    public string DayChangeDisplay { get; set; } = string.Empty;
    public bool IsDayLengthening { get; set; }
}

public class MilkyWayReport
{
    public bool IsVisibleTonight { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string WindowDisplay { get; set; } = string.Empty;
    public string PeakDisplay { get; set; } = string.Empty;
    public string MountainClearanceDisplay { get; set; } = string.Empty;
    public string MoonInterferenceDisplay { get; set; } = string.Empty;
    public string QualityBadge { get; set; } = string.Empty;
    public bool IsQualityPositive { get; set; }
    public DateTime? WindowStartTime { get; set; }
    public DateTime? WindowEndTime { get; set; }
    public bool IsActive { get; set; }
    public bool IsFuture { get; set; }
    public string CountdownDisplay { get; set; } = string.Empty;
}

public class SeasonMarkerReport
{
    public string CurrentSeason { get; set; } = string.Empty;
    public string NextMarkerName { get; set; } = string.Empty;
    public string NextMarkerDateDisplay { get; set; } = string.Empty;
    public string CountdownDisplay { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TrendDescription { get; set; } = string.Empty;
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

