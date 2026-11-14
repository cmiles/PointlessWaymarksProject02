using System;
using System.Collections.Generic;
using PointedHorizons.Models;

namespace PointedHorizons.Services;

public static class CelestialService
{
    private const double Rad2Deg = 180.0 / Math.PI;
    private const double Deg2Rad = Math.PI / 180.0;
    private const double SunAngularRadiusDeg = 0.267;
    private const double MoonAngularRadiusDeg = 0.260;

    /// <summary>
    /// Calculates celestial data (sun and moon arcs, topographic rise/set times, astronomical comparison, moon phase)
    /// for the specified observer location and date.
    /// </summary>
    public static CelestialDayData CalculateDayCelestialData(
        double lat,
        double lon,
        double elevM,
        DateTime localDate,
        List<HorizonProfilePoint>? terrainProfile)
    {
        var result = new CelestialDayData
        {
            LocalDate = localDate.Date,
            LocalDateText = localDate.ToString("dddd, MMM d, yyyy")
        };

        TimeSpan tzOffset = TimeZoneInfo.Local.GetUtcOffset(localDate);
        DateTime dayStart = localDate.Date;

        // Build continuous topography elevation angle lookup
        Func<double, double> getHorizonAltAtAz = BuildHorizonAltLookup(terrainProfile);

        // 1. Current instantaneous celestial positions
        DateTime nowLocal = DateTime.Now;
        DateTime nowUtc = DateTime.UtcNow;
        var (currSunAz, currSunAlt) = GetSunPosition(nowUtc, lat, lon);
        var (currMoonAz, currMoonAlt, _, _, _) = GetMoonPosition(nowUtc, lat, lon);
        result.CurrentSun = (currSunAz, currSunAlt);
        result.CurrentMoon = (currMoonAz, currMoonAlt);

        // 2. Sample 24 hours of today in 3-minute steps (480 steps)
        const int stepMinutes = 3;
        const int numSteps = (24 * 60) / stepMinutes;

        CelestialEvent? topoSunrise = null;
        CelestialEvent? topoSunset = null;
        CelestialEvent? topoMoonrise = null;
        CelestialEvent? topoMoonset = null;

        CelestialEvent? astroSunrise = null;
        CelestialEvent? astroSunset = null;

        double prevSunTopoDiff = double.NaN;
        double prevSunAstroDiff = double.NaN;
        DateTime prevSunTime = DateTime.MinValue;

        double prevMoonTopoDiff = double.NaN;
        DateTime prevMoonTime = DateTime.MinValue;

        for (int i = 0; i <= numSteps; i++)
        {
            DateTime stepLocal = dayStart.AddMinutes(i * stepMinutes);
            DateTime stepUtc = stepLocal - tzOffset;
            double hourLocal = stepLocal.Hour + stepLocal.Minute / 60.0;

            // --- Sun Calculation ---
            var (sunAz, sunAlt) = GetSunPosition(stepUtc, lat, lon);
            result.SunArc.Add(new CelestialArcPoint
            {
                Azimuth = Math.Round(sunAz, 2),
                Altitude = Math.Round(sunAlt, 2),
                HourLocal = Math.Round(hourLocal, 3)
            });

            double topoAltAtSun = getHorizonAltAtAz(sunAz);
            double sunTopoDiff = (sunAlt + SunAngularRadiusDeg) - topoAltAtSun;
            double sunAstroDiff = (sunAlt + SunAngularRadiusDeg) - 0.0; // Flat sea-level horizon

            if (!double.IsNaN(prevSunTopoDiff))
            {
                // Topographic Sunrise (crossing upwards through terrain silhouette)
                if (prevSunTopoDiff <= 0 && sunTopoDiff > 0 && topoSunrise == null)
                {
                    topoSunrise = RefineCrossing(lat, lon, prevSunTime, stepLocal, tzOffset, SunAngularRadiusDeg, getHorizonAltAtAz, isSun: true, isRise: true);
                }
                // Topographic Sunset (crossing downwards through terrain silhouette)
                else if (prevSunTopoDiff >= 0 && sunTopoDiff < 0 && topoSunset == null)
                {
                    topoSunset = RefineCrossing(lat, lon, prevSunTime, stepLocal, tzOffset, SunAngularRadiusDeg, getHorizonAltAtAz, isSun: true, isRise: false);
                }

                // Astronomical Sunrise / Sunset (sea level horizon = 0.0)
                if (prevSunAstroDiff <= 0 && sunAstroDiff > 0 && astroSunrise == null)
                {
                    astroSunrise = RefineCrossing(lat, lon, prevSunTime, stepLocal, tzOffset, SunAngularRadiusDeg, _ => 0.0, isSun: true, isRise: true);
                }
                else if (prevSunAstroDiff >= 0 && sunAstroDiff < 0 && astroSunset == null)
                {
                    astroSunset = RefineCrossing(lat, lon, prevSunTime, stepLocal, tzOffset, SunAngularRadiusDeg, _ => 0.0, isSun: true, isRise: false);
                }
            }
            prevSunTopoDiff = sunTopoDiff;
            prevSunAstroDiff = sunAstroDiff;
            prevSunTime = stepLocal;

            // --- Moon Calculation ---
            var (moonAz, moonAlt, moonPhase, moonPhaseName, _) = GetMoonPosition(stepUtc, lat, lon);
            result.MoonArc.Add(new CelestialArcPoint
            {
                Azimuth = Math.Round(moonAz, 2),
                Altitude = Math.Round(moonAlt, 2),
                HourLocal = Math.Round(hourLocal, 3)
            });

            // Midday snapshot of phase
            if (i == numSteps / 2)
            {
                result.MoonPhaseName = moonPhaseName;
                result.MoonIllumination = Math.Round(moonPhase, 3);
            }

            double topoAltAtMoon = getHorizonAltAtAz(moonAz);
            double moonTopoDiff = (moonAlt + MoonAngularRadiusDeg) - topoAltAtMoon;

            if (!double.IsNaN(prevMoonTopoDiff))
            {
                if (prevMoonTopoDiff <= 0 && moonTopoDiff > 0 && topoMoonrise == null)
                {
                    topoMoonrise = RefineCrossing(lat, lon, prevMoonTime, stepLocal, tzOffset, MoonAngularRadiusDeg, getHorizonAltAtAz, isSun: false, isRise: true);
                }
                else if (prevMoonTopoDiff >= 0 && moonTopoDiff < 0 && topoMoonset == null)
                {
                    topoMoonset = RefineCrossing(lat, lon, prevMoonTime, stepLocal, tzOffset, MoonAngularRadiusDeg, getHorizonAltAtAz, isSun: false, isRise: false);
                }
            }
            prevMoonTopoDiff = moonTopoDiff;
            prevMoonTime = stepLocal;
        }

        result.TopographicSunrise = topoSunrise;
        result.TopographicSunset = topoSunset;
        result.TopographicMoonrise = topoMoonrise;
        result.TopographicMoonset = topoMoonset;

        result.AstronomicalSunrise = astroSunrise;
        result.AstronomicalSunset = astroSunset;

        return result;
    }

    /// <summary>
    /// Builds an altitude angle lookup from 360° ray-traced horizon points.
    /// </summary>
    private static Func<double, double> BuildHorizonAltLookup(List<HorizonProfilePoint>? terrainProfile)
    {
        if (terrainProfile == null || terrainProfile.Count == 0)
        {
            return _ => 0.0;
        }

        int n = terrainProfile.Count;
        double[] azs = new double[n];
        double[] alts = new double[n];
        for (int i = 0; i < n; i++)
        {
            azs[i] = terrainProfile[i].Azimuth;
            alts[i] = terrainProfile[i].AltitudeAngle;
        }

        return (az) =>
        {
            double normAz = NormalizeAngle(az);
            double fIdx = (normAz / 360.0) * n;
            int i0 = ((int)Math.Floor(fIdx)) % n;
            int i1 = (i0 + 1) % n;
            double frac = fIdx - Math.Floor(fIdx);
            return alts[i0] * (1.0 - frac) + alts[i1] * frac;
        };
    }

    /// <summary>
    /// Binary search bisection to find the exact crossing time down to ~0.5 second precision.
    /// </summary>
    private static CelestialEvent RefineCrossing(
        double lat,
        double lon,
        DateTime t0,
        DateTime t1,
        TimeSpan tzOffset,
        double bodyRadiusDeg,
        Func<double, double> getHorizonAltAtAz,
        bool isSun,
        bool isRise)
    {
        for (int iter = 0; iter < 9; iter++)
        {
            DateTime mid = t0.AddTicks((t1 - t0).Ticks / 2);
            DateTime midUtc = mid - tzOffset;
            double az, alt;
            if (isSun)
            {
                (az, alt) = GetSunPosition(midUtc, lat, lon);
            }
            else
            {
                (az, alt, _, _, _) = GetMoonPosition(midUtc, lat, lon);
            }
            double diff = (alt + bodyRadiusDeg) - getHorizonAltAtAz(az);

            if (isRise)
            {
                if (diff <= 0) t0 = mid;
                else t1 = mid;
            }
            else
            {
                if (diff >= 0) t0 = mid;
                else t1 = mid;
            }
        }

        DateTime finalLocal = t0.AddTicks((t1 - t0).Ticks / 2);
        DateTime finalUtc = finalLocal - tzOffset;
        double finalAz, finalAlt;
        if (isSun) (finalAz, finalAlt) = GetSunPosition(finalUtc, lat, lon);
        else (finalAz, finalAlt, _, _, _) = GetMoonPosition(finalUtc, lat, lon);

        double horizonAlt = getHorizonAltAtAz(finalAz);
        string timeStr = finalLocal.ToString("h:mm tt");
        string azStr = $"{Math.Round(finalAz)}° {GetCardinal(finalAz)}";

        return new CelestialEvent(finalLocal, Math.Round(finalAz, 1), Math.Round(horizonAlt, 2), timeStr, azStr, isRise);
    }

    /// <summary>
    /// High-precision solar position algorithm (NOAA / Astronomical Almanac).
    /// </summary>
    public static (double Azimuth, double Altitude) GetSunPosition(DateTime utc, double lat, double lon)
    {
        double d = (utc - new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)).TotalDays;
        double w = 282.9404 + 4.70935e-5 * d;
        double e = 0.016709 - 1.151e-9 * d;
        double M = NormalizeAngle(356.0470 + 0.9856002585 * d);

        double L = NormalizeAngle(w + M);

        double E = M + Rad2Deg * e * Math.Sin(M * Deg2Rad) * (1.0 + e * Math.Cos(M * Deg2Rad));
        double x = Math.Cos(E * Deg2Rad) - e;
        double y = Math.Sin(E * Deg2Rad) * Math.Sqrt(1.0 - e * e);

        double r = Math.Sqrt(x * x + y * y);
        double v = Math.Atan2(y, x) * Rad2Deg;

        double lonSun = NormalizeAngle(v + w);
        double xSun = r * Math.Cos(lonSun * Deg2Rad);
        double ySun = r * Math.Sin(lonSun * Deg2Rad);

        double oblEcl = 23.4393 - 3.563e-7 * d;
        double xEquat = xSun;
        double yEquat = ySun * Math.Cos(oblEcl * Deg2Rad);
        double zEquat = ySun * Math.Sin(oblEcl * Deg2Rad);

        double ra = NormalizeAngle(Math.Atan2(yEquat, xEquat) * Rad2Deg);
        double dec = Math.Asin(zEquat / r) * Rad2Deg;

        double gmst0 = (L + 180.0);
        double utHours = utc.Hour + utc.Minute / 60.0 + utc.Second / 3600.0;
        double siderealTime = NormalizeAngle(gmst0 + utHours * 15.0 + lon);

        double ha = NormalizeAngle(siderealTime - ra);
        if (ha > 180.0) ha -= 360.0;

        double latRad = lat * Deg2Rad;
        double decRad = dec * Deg2Rad;
        double haRad = ha * Deg2Rad;

        double sinAlt = Math.Sin(latRad) * Math.Sin(decRad) + Math.Cos(latRad) * Math.Cos(decRad) * Math.Cos(haRad);
        double alt = Math.Asin(Math.Max(-1.0, Math.Min(1.0, sinAlt))) * Rad2Deg;

        double yAz = -Math.Cos(decRad) * Math.Sin(haRad);
        double xAz = Math.Sin(decRad) * Math.Cos(latRad) - Math.Cos(decRad) * Math.Sin(latRad) * Math.Cos(haRad);
        double az = NormalizeAngle(Math.Atan2(yAz, xAz) * Rad2Deg);

        if (alt > -2.0)
        {
            double rRefr = 1.02 / Math.Tan((alt + 10.3 / (alt + 5.11)) * Deg2Rad) / 60.0;
            alt += rRefr;
        }

        return (az, alt);
    }

    /// <summary>
    /// Topocentric lunar position with perturbation terms and phase calculation (Schlyter / Meeus).
    /// </summary>
    public static (double Azimuth, double Altitude, double Phase, string PhaseName, double DistanceEarthRadii) GetMoonPosition(DateTime utc, double lat, double lon)
    {
        double d = (utc - new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)).TotalDays;
        double N = NormalizeAngle(125.1228 - 0.0529538083 * d);
        double w = NormalizeAngle(318.0634 + 0.1643573223 * d);
        double a = 60.2666;
        double e = 0.054900;
        double M = NormalizeAngle(115.3654 + 13.0649929509 * d);

        double wSun = NormalizeAngle(282.9404 + 4.70935e-5 * d);
        double MSun = NormalizeAngle(356.0470 + 0.9856002585 * d);
        double LSun = NormalizeAngle(wSun + MSun);
        double LMoon = NormalizeAngle(N + w + M);
        double D = NormalizeAngle(LMoon - LSun);
        double F = NormalizeAngle(LMoon - N);

        double E = M + Rad2Deg * e * Math.Sin(M * Deg2Rad) * (1.0 + e * Math.Cos(M * Deg2Rad));
        double x = a * (Math.Cos(E * Deg2Rad) - e);
        double y = a * Math.Sqrt(1.0 - e * e) * Math.Sin(E * Deg2Rad);

        double r = Math.Sqrt(x * x + y * y);
        double v = Math.Atan2(y, x) * Rad2Deg;

        // Perturbations
        double dLon = -1.274 * Math.Sin((M - 2 * D) * Deg2Rad)
                    + 0.658 * Math.Sin(2 * D * Deg2Rad)
                    - 0.186 * Math.Sin(MSun * Deg2Rad)
                    - 0.059 * Math.Sin((2 * M - 2 * D) * Deg2Rad)
                    - 0.057 * Math.Sin((M - 2 * D + MSun) * Deg2Rad)
                    + 0.053 * Math.Sin((M + 2 * D) * Deg2Rad)
                    + 0.046 * Math.Sin((2 * D - MSun) * Deg2Rad)
                    + 0.041 * Math.Sin((M - MSun) * Deg2Rad)
                    - 0.035 * Math.Sin(D * Deg2Rad)
                    - 0.031 * Math.Sin((M + MSun) * Deg2Rad);

        double dLat = -0.173 * Math.Sin((F - 2 * D) * Deg2Rad)
                    - 0.055 * Math.Sin((M - F - 2 * D) * Deg2Rad)
                    - 0.046 * Math.Sin((M + F - 2 * D) * Deg2Rad)
                    + 0.033 * Math.Sin((F + 2 * D) * Deg2Rad)
                    + 0.017 * Math.Sin((2 * M + F) * Deg2Rad);

        double dR = -0.58 * Math.Cos((M - 2 * D) * Deg2Rad) - 0.46 * Math.Cos(2 * D * Deg2Rad);

        double moonLon = NormalizeAngle(v + w + dLon);
        double moonLat = dLat;
        double moonR = r + dR;

        // Ecliptic to Equatorial
        double oblEcl = (23.4393 - 3.563e-7 * d) * Deg2Rad;
        double cosLat = Math.Cos(moonLat * Deg2Rad);
        double sinLat = Math.Sin(moonLat * Deg2Rad);
        double cosLon = Math.Cos(moonLon * Deg2Rad);
        double sinLon = Math.Sin(moonLon * Deg2Rad);

        double xEq = moonR * cosLat * cosLon;
        double yEq = moonR * (Math.Cos(oblEcl) * cosLat * sinLon - Math.Sin(oblEcl) * sinLat);
        double zEq = moonR * (Math.Sin(oblEcl) * cosLat * sinLon + Math.Cos(oblEcl) * sinLat);

        // Topocentric Parallax Correction
        double LST = NormalizeAngle(LSun + 180.0 + (utc.Hour + utc.Minute / 60.0 + utc.Second / 3600.0) * 15.0 + lon);
        double lstRad = LST * Deg2Rad;
        double latRad = lat * Deg2Rad;
        double rhoSinPhi = Math.Sin(latRad);
        double rhoCosPhi = Math.Cos(latRad);

        double xTopo = xEq - rhoCosPhi * Math.Cos(lstRad);
        double yTopo = yEq - rhoCosPhi * Math.Sin(lstRad);
        double zTopo = zEq - rhoSinPhi;
        double rTopo = Math.Sqrt(xTopo * xTopo + yTopo * yTopo + zTopo * zTopo);

        double raTopo = NormalizeAngle(Math.Atan2(yTopo, xTopo) * Rad2Deg);
        double decTopo = Math.Asin(zTopo / rTopo) * Rad2Deg;

        double ha = NormalizeAngle(LST - raTopo);
        if (ha > 180.0) ha -= 360.0;

        double haRad = ha * Deg2Rad;
        double decRad = decTopo * Deg2Rad;

        double sinAlt = Math.Sin(latRad) * Math.Sin(decRad) + Math.Cos(latRad) * Math.Cos(decRad) * Math.Cos(haRad);
        double alt = Math.Asin(Math.Max(-1.0, Math.Min(1.0, sinAlt))) * Rad2Deg;

        double yAz = -Math.Cos(decRad) * Math.Sin(haRad);
        double xAz = Math.Sin(decRad) * Math.Cos(latRad) - Math.Cos(decRad) * Math.Sin(latRad) * Math.Cos(haRad);
        double az = NormalizeAngle(Math.Atan2(yAz, xAz) * Rad2Deg);

        if (alt > -2.0)
        {
            double rRefr = 1.02 / Math.Tan((alt + 10.3 / (alt + 5.11)) * Deg2Rad) / 60.0;
            alt += rRefr;
        }

        double phase = 0.5 * (1.0 - Math.Cos(D * Deg2Rad));
        string phaseName = GetPhaseName(D, phase);

        return (az, alt, phase, phaseName, rTopo);
    }

    private static string GetPhaseName(double elongation, double phase)
    {
        if (phase < 0.03) return "New Moon";
        if (phase > 0.97) return "Full Moon";
        if (elongation < 180.0)
        {
            if (phase < 0.45) return "Waxing Crescent";
            if (phase <= 0.55) return "First Quarter";
            return "Waxing Gibbous";
        }
        else
        {
            if (phase > 0.55) return "Waning Gibbous";
            if (phase >= 0.45) return "Third Quarter";
            return "Waning Crescent";
        }
    }

    public static string GetCardinal(double deg)
    {
        string[] cardinals = ["N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"];
        int idx = (int)Math.Round(deg / 22.5) % 16;
        return cardinals[idx];
    }

    private static double NormalizeAngle(double a)
    {
        a = a % 360.0;
        if (a < 0) a += 360.0;
        return a;
    }

    /// <summary>
    /// Calculates the next upcoming occurrence of each celestial event (sunrise, sunset, moonrise, moonset)
    /// after referenceTime, comparing topography-compensated horizon times with standard flat sea-level horizon times,
    /// and calculating countdowns for each upcoming event.
    /// </summary>
    public static CelestialWindowReport CalculateWindowCelestialData(
        double lat,
        double lon,
        double elevM,
        DateTime referenceTime,
        List<HorizonProfilePoint>? terrainProfile)
    {
        // Search window: from referenceTime forward 36 hours in 2-minute steps
        DateTime searchStart = referenceTime.AddMinutes(-10);
        DateTime searchEnd = referenceTime.AddHours(36);

        var report = new CelestialWindowReport
        {
            Latitude = lat,
            Longitude = lon,
            ElevationM = elevM,
            ReferenceTime = referenceTime,
            WindowStart = referenceTime,
            WindowEnd = searchEnd,
            CoordinatesDisplay = $"{Math.Abs(lat):F4}° {(lat >= 0 ? "N" : "S")}, {Math.Abs(lon):F4}° {(lon >= 0 ? "E" : "W")}",
            ElevationDisplay = $"{Math.Round(elevM * 3.28084):N0} ft ({Math.Round(elevM):N0} m)",
            WindowDisplay = $"Upcoming from {referenceTime:ddd, MMM d, h:mm tt}",
            HasTopography = terrainProfile != null && terrainProfile.Count > 0
        };

        TimeSpan tzOffset = TimeZoneInfo.Local.GetUtcOffset(referenceTime);
        Func<double, double> getHorizonAltAtAz = BuildHorizonAltLookup(terrainProfile);

        // Current Moon phase at reference time
        var (_, _, currMoonPhase, currMoonPhaseName, _) = GetMoonPosition(referenceTime - tzOffset, lat, lon);
        report.MoonPhaseName = currMoonPhaseName;
        report.MoonIllumination = Math.Round(currMoonPhase, 3);

        const int stepMinutes = 2;
        int numSteps = (int)((searchEnd - searchStart).TotalMinutes / stepMinutes);

        var sunTopRises = new List<CelestialEvent>();
        var sunTopSets = new List<CelestialEvent>();
        var sunAstRises = new List<CelestialEvent>();
        var sunAstSets = new List<CelestialEvent>();

        var moonTopRises = new List<CelestialEvent>();
        var moonTopSets = new List<CelestialEvent>();
        var moonAstRises = new List<CelestialEvent>();
        var moonAstSets = new List<CelestialEvent>();

        double prevSunTopoDiff = double.NaN;
        double prevSunAstroDiff = double.NaN;
        double prevMoonTopoDiff = double.NaN;
        double prevMoonAstroDiff = double.NaN;
        DateTime prevStepLocal = DateTime.MinValue;

        for (int i = 0; i <= numSteps; i++)
        {
            DateTime stepLocal = searchStart.AddMinutes(i * stepMinutes);
            DateTime stepUtc = stepLocal - tzOffset;

            // Sun
            var (sunAz, sunAlt) = GetSunPosition(stepUtc, lat, lon);
            double topoAltAtSun = getHorizonAltAtAz(sunAz);
            double sunTopoDiff = (sunAlt + SunAngularRadiusDeg) - topoAltAtSun;
            double sunAstroDiff = (sunAlt + SunAngularRadiusDeg) - 0.0;

            if (!double.IsNaN(prevSunTopoDiff))
            {
                if (prevSunTopoDiff <= 0 && sunTopoDiff > 0)
                {
                    sunTopRises.Add(RefineCrossing(lat, lon, prevStepLocal, stepLocal, tzOffset, SunAngularRadiusDeg, getHorizonAltAtAz, isSun: true, isRise: true));
                }
                else if (prevSunTopoDiff >= 0 && sunTopoDiff < 0)
                {
                    sunTopSets.Add(RefineCrossing(lat, lon, prevStepLocal, stepLocal, tzOffset, SunAngularRadiusDeg, getHorizonAltAtAz, isSun: true, isRise: false));
                }

                if (prevSunAstroDiff <= 0 && sunAstroDiff > 0)
                {
                    sunAstRises.Add(RefineCrossing(lat, lon, prevStepLocal, stepLocal, tzOffset, SunAngularRadiusDeg, _ => 0.0, isSun: true, isRise: true));
                }
                else if (prevSunAstroDiff >= 0 && sunAstroDiff < 0)
                {
                    sunAstSets.Add(RefineCrossing(lat, lon, prevStepLocal, stepLocal, tzOffset, SunAngularRadiusDeg, _ => 0.0, isSun: true, isRise: false));
                }
            }
            prevSunTopoDiff = sunTopoDiff;
            prevSunAstroDiff = sunAstroDiff;

            // Moon
            var (moonAz, moonAlt, _, _, _) = GetMoonPosition(stepUtc, lat, lon);
            double topoAltAtMoon = getHorizonAltAtAz(moonAz);
            double moonTopoDiff = (moonAlt + MoonAngularRadiusDeg) - topoAltAtMoon;
            double moonAstroDiff = (moonAlt + MoonAngularRadiusDeg) - 0.0;

            if (!double.IsNaN(prevMoonTopoDiff))
            {
                if (prevMoonTopoDiff <= 0 && moonTopoDiff > 0)
                {
                    moonTopRises.Add(RefineCrossing(lat, lon, prevStepLocal, stepLocal, tzOffset, MoonAngularRadiusDeg, getHorizonAltAtAz, isSun: false, isRise: true));
                }
                else if (prevMoonTopoDiff >= 0 && moonTopoDiff < 0)
                {
                    moonTopSets.Add(RefineCrossing(lat, lon, prevStepLocal, stepLocal, tzOffset, MoonAngularRadiusDeg, getHorizonAltAtAz, isSun: false, isRise: false));
                }

                if (prevMoonAstroDiff <= 0 && moonAstroDiff > 0)
                {
                    moonAstRises.Add(RefineCrossing(lat, lon, prevStepLocal, stepLocal, tzOffset, MoonAngularRadiusDeg, _ => 0.0, isSun: false, isRise: true));
                }
                else if (prevMoonAstroDiff >= 0 && moonAstroDiff < 0)
                {
                    moonAstSets.Add(RefineCrossing(lat, lon, prevStepLocal, stepLocal, tzOffset, MoonAngularRadiusDeg, _ => 0.0, isSun: false, isRise: false));
                }
            }
            prevMoonTopoDiff = moonTopoDiff;
            prevMoonAstroDiff = moonAstroDiff;
            prevStepLocal = stepLocal;
        }

        // Build the next 4 events
        report.Sunrise = BuildWindowEvent("Sunrise", "🌅", sunTopRises, sunAstRises, referenceTime, isRise: true);
        report.Sunset = BuildWindowEvent("Sunset", "🌇", sunTopSets, sunAstSets, referenceTime, isRise: false);
        report.Moonrise = BuildWindowEvent("Moonrise", "🌙", moonTopRises, moonAstRises, referenceTime, isRise: true);
        report.Moonset = BuildWindowEvent("Moonset", "🌘", moonTopSets, moonAstSets, referenceTime, isRise: false);

        // Additional Topography-Compensated Celestial Reports
        report.Twilight = CalculateTwilight(lat, lon, referenceTime, getHorizonAltAtAz, report.Sunrise, report.Sunset);
        report.PhotographyHours = CalculatePhotographyHours(lat, lon, referenceTime, getHorizonAltAtAz, report.Sunrise, report.Sunset, report.Twilight);
        report.SolarNoon = CalculateSolarNoonAndDaylight(lat, lon, referenceTime, getHorizonAltAtAz, report.Sunrise, report.Sunset);
        report.MilkyWay = CalculateMilkyWayWindow(lat, lon, referenceTime, getHorizonAltAtAz);
        report.SeasonMarker = CalculateSeasonMarker(lat, referenceTime);

        return report;
    }

    private static WindowCelestialEvent BuildWindowEvent(
        string name,
        string icon,
        List<CelestialEvent> topoEvents,
        List<CelestialEvent> astroEvents,
        DateTime referenceTime,
        bool isRise)
    {
        var ev = new WindowCelestialEvent
        {
            Name = name,
            Icon = icon
        };

        // Select the next upcoming crossing strictly after referenceTime
        CelestialEvent? bestTopo = null;
        CelestialEvent? bestAstro = null;

        CelestialEvent? nextTopo = SelectNextEvent(topoEvents, referenceTime);
        if (nextTopo != null)
        {
            bestTopo = nextTopo;
            bestAstro = FindClosest(astroEvents, nextTopo.LocalTime);
        }
        else
        {
            CelestialEvent? nextAstro = SelectNextEvent(astroEvents, referenceTime);
            if (nextAstro != null)
            {
                bestAstro = nextAstro;
                bestTopo = FindClosest(topoEvents, nextAstro.LocalTime);
            }
        }

        ev.TopoEvent = bestTopo;
        ev.AstroEvent = bestAstro;

        DateTime? effectiveTime = bestTopo?.LocalTime ?? bestAstro?.LocalTime;
        ev.EffectiveTime = effectiveTime;

        if (effectiveTime == null)
        {
            ev.HasEvent = false;
            ev.TopoTimeDisplay = "None found";
            ev.AstroTimeDisplay = "--:--";
            ev.TimeUntilDisplay = "No upcoming event";
            return ev;
        }

        ev.HasEvent = true;
        ev.IsInWindow = true;
        ev.IsFuture = effectiveTime.Value >= referenceTime;
        ev.TimeDifference = ev.IsFuture ? (effectiveTime.Value - referenceTime) : (referenceTime - effectiveTime.Value);

        string durationStr = FormatDuration(ev.TimeDifference);
        ev.TimeUntilDisplay = ev.IsFuture ? $"+ {durationStr}" : $"{durationStr} ago";

        if (bestTopo != null)
        {
            string dayPrefix = GetDayPrefix(bestTopo.LocalTime, referenceTime);
            ev.TopoTimeDisplay = $"{dayPrefix}{bestTopo.TimeText}";
            ev.TopoAzimuthDisplay = bestTopo.AzimuthText;
            ev.TopoElevationDisplay = $"Ridge: {bestTopo.Altitude:F1}°";
        }
        else
        {
            string dayPrefix = bestAstro != null ? GetDayPrefix(bestAstro.LocalTime, referenceTime) : "";
            ev.TopoTimeDisplay = bestAstro != null ? $"{dayPrefix}{bestAstro.TimeText}" : "--:--";
            ev.TopoAzimuthDisplay = bestAstro?.AzimuthText ?? "--";
            ev.TopoElevationDisplay = "Flat: 0.0°";
        }

        if (bestAstro != null)
        {
            string astroDayPrefix = GetDayPrefix(bestAstro.LocalTime, referenceTime);
            ev.AstroTimeDisplay = $"{astroDayPrefix}{bestAstro.TimeText} ({bestAstro.AzimuthText})";
            ev.AstroAzimuthDisplay = bestAstro.AzimuthText;
        }
        else
        {
            ev.AstroTimeDisplay = "N/A";
        }

        if (bestTopo != null && bestAstro != null)
        {
            double diffMinutes = (bestTopo.LocalTime - bestAstro.LocalTime).TotalMinutes;
            if (Math.Abs(diffMinutes) < 0.5)
            {
                ev.TopoShiftDisplay = "Same as flat sea-level horizon (0.0°)";
            }
            else if (diffMinutes > 0)
            {
                ev.TopoShiftDisplay = $"+{Math.Round(diffMinutes)} min delay (ridge: {bestTopo.Altitude:F1}°)";
            }
            else
            {
                ev.TopoShiftDisplay = $"-{Math.Round(Math.Abs(diffMinutes))} min earlier (ridge: {bestTopo.Altitude:F1}°)";
            }
        }
        else
        {
            ev.TopoShiftDisplay = "Flat sea-level horizon";
        }

        return ev;
    }

    private static string GetDayPrefix(DateTime eventLocalTime, DateTime referenceTime)
    {
        if (eventLocalTime.Date == referenceTime.Date)
        {
            return "Today ";
        }
        if (eventLocalTime.Date == referenceTime.Date.AddDays(1))
        {
            return "Tomorrow ";
        }
        if (eventLocalTime.Date == referenceTime.Date.AddDays(-1))
        {
            return "Yesterday ";
        }
        return $"{eventLocalTime:ddd} ";
    }

    private static CelestialEvent? SelectNextEvent(List<CelestialEvent> events, DateTime refTime)
    {
        if (events == null || events.Count == 0) return null;

        CelestialEvent? best = null;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.LocalTime > refTime)
            {
                if (best == null || e.LocalTime < best.LocalTime)
                {
                    best = e;
                }
            }
        }
        return best;
    }

    private static CelestialEvent? FindClosest(List<CelestialEvent> events, DateTime targetTime)
    {
        if (events == null || events.Count == 0) return null;
        CelestialEvent best = events[0];
        double bestDiff = Math.Abs((best.LocalTime - targetTime).TotalMinutes);
        for (int i = 1; i < events.Count; i++)
        {
            double diff = Math.Abs((events[i].LocalTime - targetTime).TotalMinutes);
            if (diff < bestDiff)
            {
                best = events[i];
                bestDiff = diff;
            }
        }
        return best;
    }

    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalHours >= 1.0)
        {
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        }
        if (ts.TotalMinutes >= 1.0)
        {
            return $"{Math.Max(1, (int)ts.TotalMinutes)}m";
        }
        return "< 1m";
    }

    #region Additional Topographic Celestial Calculations

    public static (double Azimuth, double Altitude) GetGalacticCenterPosition(DateTime utc, double lat, double lon)
    {
        double d = (utc - new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)).TotalDays;
        double lst = NormalizeAngle(280.46061837 + 360.98564736629 * d + lon);
        const double ra = 266.4168; // RA of Sagittarius A* (J2000)
        const double dec = -29.0078; // Dec of Sagittarius A* (J2000)
        double ha = NormalizeAngle(lst - ra);
        if (ha > 180.0) ha -= 360.0;

        double latRad = lat * Deg2Rad;
        double decRad = dec * Deg2Rad;
        double haRad = ha * Deg2Rad;

        double sinAlt = Math.Sin(latRad) * Math.Sin(decRad) + Math.Cos(latRad) * Math.Cos(decRad) * Math.Cos(haRad);
        double alt = Math.Asin(Math.Max(-1.0, Math.Min(1.0, sinAlt))) * Rad2Deg;

        double yAz = -Math.Cos(decRad) * Math.Sin(haRad);
        double xAz = Math.Sin(decRad) * Math.Cos(latRad) - Math.Cos(decRad) * Math.Sin(latRad) * Math.Cos(haRad);
        double az = NormalizeAngle(Math.Atan2(yAz, xAz) * Rad2Deg);

        if (alt > -2.0)
        {
            double rRefr = 1.02 / Math.Tan((alt + 10.3 / (alt + 5.11)) * Deg2Rad) / 60.0;
            alt += rRefr;
        }

        return (az, alt);
    }

    private static DateTime? RefineSunAltitudeCrossing(
        double lat,
        double lon,
        DateTime t0Local,
        DateTime t1Local,
        TimeSpan tzOffset,
        double targetAlt,
        bool isRise)
    {
        for (int iter = 0; iter < 10; iter++)
        {
            DateTime mid = t0Local.AddTicks((t1Local - t0Local).Ticks / 2);
            DateTime midUtc = mid - tzOffset;
            var (_, alt) = GetSunPosition(midUtc, lat, lon);
            if (isRise)
            {
                if (alt <= targetAlt) t0Local = mid;
                else t1Local = mid;
            }
            else
            {
                if (alt >= targetAlt) t0Local = mid;
                else t1Local = mid;
            }
        }
        return t0Local.AddTicks((t1Local - t0Local).Ticks / 2);
    }

    private static (DateTime? Rise, DateTime? Set) FindSunCrossingsForTarget(
        double lat,
        double lon,
        DateTime date,
        TimeSpan tzOffset,
        double targetAlt)
    {
        DateTime start = date.Date;
        const int stepMin = 3;
        const int steps = (24 * 60) / stepMin;

        DateTime? rise = null;
        DateTime? set = null;

        double prevAlt = double.NaN;
        DateTime prevTime = DateTime.MinValue;

        for (int i = 0; i <= steps; i++)
        {
            DateTime curTime = start.AddMinutes(i * stepMin);
            DateTime curUtc = curTime - tzOffset;
            var (_, curAlt) = GetSunPosition(curUtc, lat, lon);

            if (!double.IsNaN(prevAlt))
            {
                if (prevAlt <= targetAlt && curAlt > targetAlt && rise == null)
                {
                    rise = RefineSunAltitudeCrossing(lat, lon, prevTime, curTime, tzOffset, targetAlt, isRise: true);
                }
                else if (prevAlt >= targetAlt && curAlt < targetAlt && set == null)
                {
                    set = RefineSunAltitudeCrossing(lat, lon, prevTime, curTime, tzOffset, targetAlt, isRise: false);
                }
            }
            prevAlt = curAlt;
            prevTime = curTime;
        }

        return (rise, set);
    }

    private static TwilightReport CalculateTwilight(
        double lat,
        double lon,
        DateTime refTime,
        Func<double, double> getHorizonAltAtAz,
        WindowCelestialEvent? topoSunriseEvent,
        WindowCelestialEvent? topoSunsetEvent)
    {
        TimeSpan tzOffset = TimeZoneInfo.Local.GetUtcOffset(refTime);
        DateTime targetDate = refTime.Date;

        var (astroDawn, astroDusk) = FindSunCrossingsForTarget(lat, lon, targetDate, tzOffset, -18.0);
        var (nautDawn, nautDusk) = FindSunCrossingsForTarget(lat, lon, targetDate, tzOffset, -12.0);
        var (civDawn, civDusk) = FindSunCrossingsForTarget(lat, lon, targetDate, tzOffset, -6.0);

        var report = new TwilightReport();

        if (civDawn.HasValue && civDusk.HasValue)
        {
            TimeSpan trailDuration = civDusk.Value - civDawn.Value;
            report.TrailLightWindowDisplay = $"{civDawn.Value:h:mm tt} – {civDusk.Value:h:mm tt}";
            report.TrailLightDurationDisplay = $"{trailDuration.Hours}h {trailDuration.Minutes}m";
            report.TrailLightNote = "Natural daylight sufficient for trail hiking without headlamps.";
        }
        else
        {
            report.TrailLightWindowDisplay = "24h Ambient Daylight or Polar Night";
            report.TrailLightDurationDisplay = "--";
            report.TrailLightNote = "Sun does not transition through civil twilight today.";
        }

        report.AstroDawn = new TwilightPhaseItem
        {
            Name = "Astronomical Dawn",
            TimeDisplay = astroDawn?.ToString("h:mm tt") ?? "--",
            AngleDisplay = "-18°",
            Note = "First subtle ambient light appears in complete darkness",
            IsPast = astroDawn.HasValue && astroDawn.Value < refTime
        };

        report.NauticalDawn = new TwilightPhaseItem
        {
            Name = "Nautical Dawn",
            TimeDisplay = nautDawn?.ToString("h:mm tt") ?? "--",
            AngleDisplay = "-12°",
            Note = "Horizon distinguishable; brightest navigation stars fade",
            IsPast = nautDawn.HasValue && nautDawn.Value < refTime
        };

        report.CivilDawn = new TwilightPhaseItem
        {
            Name = "Civil Dawn",
            TimeDisplay = civDawn?.ToString("h:mm tt") ?? "--",
            AngleDisplay = "-6°",
            Note = "Usable trail daylight begins; headlamps no longer needed",
            IsPast = civDawn.HasValue && civDawn.Value < refTime
        };

        if (topoSunriseEvent?.TopoEvent != null)
        {
            report.MountainSunrise = new TwilightPhaseItem
            {
                Name = "Mountain Sunrise",
                TimeDisplay = topoSunriseEvent.TopoEvent.TimeText,
                AngleDisplay = $"+{topoSunriseEvent.TopoEvent.Altitude:F1}°",
                Note = $"First direct sun rays clear {topoSunriseEvent.TopoEvent.Altitude:F1}° ridge at {topoSunriseEvent.TopoEvent.AzimuthText}",
                IsPast = topoSunriseEvent.TopoEvent.LocalTime < refTime,
                IsTopographic = true
            };
        }

        if (topoSunsetEvent?.TopoEvent != null)
        {
            report.MountainSunset = new TwilightPhaseItem
            {
                Name = "Mountain Sunset",
                TimeDisplay = topoSunsetEvent.TopoEvent.TimeText,
                AngleDisplay = $"+{topoSunsetEvent.TopoEvent.Altitude:F1}°",
                Note = $"Direct sunlight drops behind {topoSunsetEvent.TopoEvent.Altitude:F1}° ridge at {topoSunsetEvent.TopoEvent.AzimuthText}",
                IsPast = topoSunsetEvent.TopoEvent.LocalTime < refTime,
                IsTopographic = true
            };
        }

        report.CivilDusk = new TwilightPhaseItem
        {
            Name = "Civil Dusk",
            TimeDisplay = civDusk?.ToString("h:mm tt") ?? "--",
            AngleDisplay = "-6°",
            Note = "Ambient light fades; headlamps recommended on mountain trails",
            IsPast = civDusk.HasValue && civDusk.Value < refTime
        };

        report.NauticalDusk = new TwilightPhaseItem
        {
            Name = "Nautical Dusk",
            TimeDisplay = nautDusk?.ToString("h:mm tt") ?? "--",
            AngleDisplay = "-12°",
            Note = "Deep twilight; major constellations and horizon silhouette visible",
            IsPast = nautDusk.HasValue && nautDusk.Value < refTime
        };

        report.AstroDusk = new TwilightPhaseItem
        {
            Name = "Astronomical Dusk",
            TimeDisplay = astroDusk?.ToString("h:mm tt") ?? "--",
            AngleDisplay = "-18°",
            Note = "True night begins; total darkness ideal for deep-sky stargazing",
            IsPast = astroDusk.HasValue && astroDusk.Value < refTime
        };

        return report;
    }

    private static PhotographyHoursReport CalculatePhotographyHours(
        double lat,
        double lon,
        DateTime refTime,
        Func<double, double> getHorizonAltAtAz,
        WindowCelestialEvent? topoSunriseEvent,
        WindowCelestialEvent? topoSunsetEvent,
        TwilightReport? twilight)
    {
        TimeSpan tzOffset = TimeZoneInfo.Local.GetUtcOffset(refTime);
        var report = new PhotographyHoursReport();

        // 1. Morning Photography Windows (Civil Dawn to Sun +6°)
        DateTime morningTargetDate = refTime.Date;
        var (mRiseMinus4, _) = FindSunCrossingsForTarget(lat, lon, morningTargetDate, tzOffset, -4.0);
        var (mRisePlus6, _) = FindSunCrossingsForTarget(lat, lon, morningTargetDate, tzOffset, 6.0);
        DateTime? mCivDawn = FindSunCrossingsForTarget(lat, lon, morningTargetDate, tzOffset, -6.0).Rise;

        // If morning golden hour has completely passed today, look forward to tomorrow morning
        if (mRisePlus6.HasValue && refTime > mRisePlus6.Value)
        {
            morningTargetDate = morningTargetDate.AddDays(1);
            mRiseMinus4 = FindSunCrossingsForTarget(lat, lon, morningTargetDate, tzOffset, -4.0).Rise;
            mRisePlus6 = FindSunCrossingsForTarget(lat, lon, morningTargetDate, tzOffset, 6.0).Rise;
            mCivDawn = FindSunCrossingsForTarget(lat, lon, morningTargetDate, tzOffset, -6.0).Rise;
        }

        // Morning Blue Hour Item
        if (mCivDawn.HasValue && mRiseMinus4.HasValue)
        {
            string dayPfx = GetDayPrefix(mCivDawn.Value, refTime);
            bool isActive = refTime >= mCivDawn.Value && refTime <= mRiseMinus4.Value;
            bool isFuture = refTime < mCivDawn.Value;
            string countdown = isActive ? "● Active Now" : (isFuture ? $"+ {FormatDuration(mCivDawn.Value - refTime)}" : "✓ Passed");

            report.MorningBlue = new PhotographyWindowItem
            {
                Id = "MorningBlue",
                Title = "Morning Blue Hour",
                Icon = "🌌",
                StartTime = mCivDawn.Value,
                EndTime = mRiseMinus4.Value,
                TimeWindowDisplay = $"{dayPfx}{mCivDawn.Value:h:mm} – {mRiseMinus4.Value:h:mm tt}",
                SubTitle = "Civil Dawn to Sun at -4°",
                Description = "Deep blue ambient sky before dawn; serene pre-sunrise silhouettes.",
                CountdownDisplay = countdown,
                IsActive = isActive,
                IsFuture = isFuture,
                IsPast = !isActive && !isFuture,
                HasWindow = true
            };
            report.MorningBlueHourDisplay = $"{mCivDawn.Value:h:mm} – {mRiseMinus4.Value:h:mm tt}";
        }
        else
        {
            report.MorningBlueHourDisplay = "--";
        }

        // Morning Golden Hour Item
        if (mRiseMinus4.HasValue && mRisePlus6.HasValue)
        {
            string dayPfx = GetDayPrefix(mRiseMinus4.Value, refTime);
            bool isActive = refTime >= mRiseMinus4.Value && refTime <= mRisePlus6.Value;
            bool isFuture = refTime < mRiseMinus4.Value;
            string countdown = isActive ? "● Active Now" : (isFuture ? $"+ {FormatDuration(mRiseMinus4.Value - refTime)}" : "✓ Passed");

            string directSun = "Direct sun unobstructed by terrain";
            if (topoSunriseEvent?.TopoEvent != null)
            {
                DateTime topoRise = topoSunriseEvent.TopoEvent.LocalTime;
                if (topoRise < mRisePlus6.Value)
                {
                    directSun = $"Direct sun: {topoRise:h:mm} – {mRisePlus6.Value:h:mm tt} (clears {topoSunriseEvent.TopoEvent.Altitude:F1}° ridge)";
                }
                else
                {
                    directSun = $"Sun clears ridge above +6° ({topoRise:h:mm tt})";
                }
            }

            report.MorningGolden = new PhotographyWindowItem
            {
                Id = "MorningGolden",
                Title = "Morning Golden Hour",
                Icon = "🌅",
                StartTime = mRiseMinus4.Value,
                EndTime = mRisePlus6.Value,
                TimeWindowDisplay = $"{dayPfx}{mRiseMinus4.Value:h:mm} – {mRisePlus6.Value:h:mm tt}",
                SubTitle = "Sun -4° to +6° elevation",
                DirectSunDisplay = directSun,
                Description = "Warm morning glow, soft shadows, and vibrant alpine ridgelines.",
                CountdownDisplay = countdown,
                IsActive = isActive,
                IsFuture = isFuture,
                IsPast = !isActive && !isFuture,
                HasWindow = true
            };
            report.MorningGoldenHourDisplay = $"{mRiseMinus4.Value:h:mm} – {mRisePlus6.Value:h:mm tt}";
            report.MorningDirectSunDisplay = directSun;
        }
        else
        {
            report.MorningGoldenHourDisplay = "--";
            report.MorningDirectSunDisplay = "--";
        }

        // 2. Evening Photography Windows (Sun +6° to Civil Dusk)
        DateTime eveningTargetDate = refTime.Date;
        var (_, eSetMinus4) = FindSunCrossingsForTarget(lat, lon, eveningTargetDate, tzOffset, -4.0);
        var (_, eSetPlus6) = FindSunCrossingsForTarget(lat, lon, eveningTargetDate, tzOffset, 6.0);
        DateTime? eCivDusk = FindSunCrossingsForTarget(lat, lon, eveningTargetDate, tzOffset, -6.0).Set;

        // If evening blue hour has completely passed today, look forward to tomorrow evening
        if (eCivDusk.HasValue && refTime > eCivDusk.Value)
        {
            eveningTargetDate = eveningTargetDate.AddDays(1);
            eSetMinus4 = FindSunCrossingsForTarget(lat, lon, eveningTargetDate, tzOffset, -4.0).Set;
            eSetPlus6 = FindSunCrossingsForTarget(lat, lon, eveningTargetDate, tzOffset, 6.0).Set;
            eCivDusk = FindSunCrossingsForTarget(lat, lon, eveningTargetDate, tzOffset, -6.0).Set;
        }

        // Evening Golden Hour Item
        if (eSetPlus6.HasValue && eSetMinus4.HasValue)
        {
            string dayPfx = GetDayPrefix(eSetPlus6.Value, refTime);
            bool isActive = refTime >= eSetPlus6.Value && refTime <= eSetMinus4.Value;
            bool isFuture = refTime < eSetPlus6.Value;
            string countdown = isActive ? "● Active Now" : (isFuture ? $"+ {FormatDuration(eSetPlus6.Value - refTime)}" : "✓ Passed");

            string directSun = "Direct sun unobstructed by terrain";
            if (topoSunsetEvent?.TopoEvent != null)
            {
                DateTime topoSet = topoSunsetEvent.TopoEvent.LocalTime;
                if (eSetPlus6.Value < topoSet)
                {
                    directSun = $"Direct sun: {eSetPlus6.Value:h:mm} – {topoSet:h:mm tt} (drops behind {topoSunsetEvent.TopoEvent.Altitude:F1}° ridge)";
                }
                else
                {
                    directSun = $"Sun drops below ridge before +6° ({topoSet:h:mm tt})";
                }
            }

            report.EveningGolden = new PhotographyWindowItem
            {
                Id = "EveningGolden",
                Title = "Evening Golden Hour",
                Icon = "🌇",
                StartTime = eSetPlus6.Value,
                EndTime = eSetMinus4.Value,
                TimeWindowDisplay = $"{dayPfx}{eSetPlus6.Value:h:mm} – {eSetMinus4.Value:h:mm tt}",
                SubTitle = "Sun +6° to -4° elevation",
                DirectSunDisplay = directSun,
                Description = "Rich golden side-lighting, long mountain shadows, and warm peaks.",
                CountdownDisplay = countdown,
                IsActive = isActive,
                IsFuture = isFuture,
                IsPast = !isActive && !isFuture,
                HasWindow = true
            };
            report.EveningGoldenHourDisplay = $"{eSetPlus6.Value:h:mm} – {eSetMinus4.Value:h:mm tt}";
            report.EveningDirectSunDisplay = directSun;
        }
        else
        {
            report.EveningGoldenHourDisplay = "--";
            report.EveningDirectSunDisplay = "--";
        }

        // Evening Blue Hour Item
        if (eSetMinus4.HasValue && eCivDusk.HasValue)
        {
            string dayPfx = GetDayPrefix(eSetMinus4.Value, refTime);
            bool isActive = refTime >= eSetMinus4.Value && refTime <= eCivDusk.Value;
            bool isFuture = refTime < eSetMinus4.Value;
            string countdown = isActive ? "● Active Now" : (isFuture ? $"+ {FormatDuration(eSetMinus4.Value - refTime)}" : "✓ Passed");

            report.EveningBlue = new PhotographyWindowItem
            {
                Id = "EveningBlue",
                Title = "Evening Blue Hour",
                Icon = "🌌",
                StartTime = eSetMinus4.Value,
                EndTime = eCivDusk.Value,
                TimeWindowDisplay = $"{dayPfx}{eSetMinus4.Value:h:mm} – {eCivDusk.Value:h:mm tt}",
                SubTitle = "Sun at -4° to Civil Dusk",
                Description = "Cobalt blue dusk skies, warm afterglow, and crisp topographic silhouettes.",
                CountdownDisplay = countdown,
                IsActive = isActive,
                IsFuture = isFuture,
                IsPast = !isActive && !isFuture,
                HasWindow = true
            };
            report.EveningBlueHourDisplay = $"{eSetMinus4.Value:h:mm} – {eCivDusk.Value:h:mm tt}";
        }
        else
        {
            report.EveningBlueHourDisplay = "--";
        }

        if (topoSunriseEvent?.TopoEvent != null && topoSunsetEvent?.TopoEvent != null)
        {
            report.TopoPhotoNote = $"Terrain ridges frame direct alpine light between {topoSunriseEvent.TopoEvent.TimeText} and {topoSunsetEvent.TopoEvent.TimeText}.";
        }
        else
        {
            report.TopoPhotoNote = "Flat sea-level horizon lighting without terrain obstruction.";
        }

        return report;
    }

    private static SolarNoonReport CalculateSolarNoonAndDaylight(
        double lat,
        double lon,
        DateTime refTime,
        Func<double, double> getHorizonAltAtAz,
        WindowCelestialEvent? topoSunriseEvent,
        WindowCelestialEvent? topoSunsetEvent)
    {
        TimeSpan tzOffset = TimeZoneInfo.Local.GetUtcOffset(refTime);
        DateTime targetDate = refTime.Date;

        // Sample between 10:00 AM and 3:00 PM for maximum solar elevation
        DateTime scanStart = targetDate.AddHours(10);
        DateTime scanEnd = targetDate.AddHours(15);
        double maxAlt = -90.0;
        DateTime bestNoonTime = targetDate.AddHours(12);
        double bestAz = 180.0;

        for (DateTime t = scanStart; t <= scanEnd; t = t.AddMinutes(1))
        {
            var (az, alt) = GetSunPosition(t - tzOffset, lat, lon);
            if (alt > maxAlt)
            {
                maxAlt = alt;
                bestNoonTime = t;
                bestAz = az;
            }
        }

        // If today's solar noon culmination has passed by more than 30 minutes, compute tomorrow's upcoming solar noon
        if (refTime > bestNoonTime.AddMinutes(30))
        {
            targetDate = targetDate.AddDays(1);
            scanStart = targetDate.AddHours(10);
            scanEnd = targetDate.AddHours(15);
            maxAlt = -90.0;
            bestNoonTime = targetDate.AddHours(12);
            bestAz = 180.0;

            for (DateTime t = scanStart; t <= scanEnd; t = t.AddMinutes(1))
            {
                var (az, alt) = GetSunPosition(t - tzOffset, lat, lon);
                if (alt > maxAlt)
                {
                    maxAlt = alt;
                    bestNoonTime = t;
                    bestAz = az;
                }
            }
        }

        var report = new SolarNoonReport();
        report.SolarNoonTime = bestNoonTime;
        string dayPfx = GetDayPrefix(bestNoonTime, refTime);
        report.NoonTimeDisplay = $"{dayPfx}{bestNoonTime:h:mm tt}";
        report.MaxElevationDisplay = $"{maxAlt:F1}° {GetCardinal(bestAz)}";

        report.IsNoonActive = Math.Abs((refTime - bestNoonTime).TotalMinutes) <= 15;
        report.IsNoonPast = refTime > bestNoonTime.AddMinutes(15);

        if (report.IsNoonActive)
        {
            report.NoonCountdownDisplay = "● Culmination Now";
        }
        else if (refTime < bestNoonTime)
        {
            report.NoonCountdownDisplay = $"+ {FormatDuration(bestNoonTime - refTime)}";
        }
        else
        {
            report.NoonCountdownDisplay = "✓ Culmination passed";
        }

        // Daylight Duration Comparison
        var (flatRiseToday, flatSetToday) = FindSunCrossingsForTarget(lat, lon, targetDate, tzOffset, -0.833);
        if (flatRiseToday.HasValue && flatSetToday.HasValue)
        {
            TimeSpan flatDuration = flatSetToday.Value - flatRiseToday.Value;
            report.FlatDaylightDisplay = $"{flatDuration.Hours}h {flatDuration.Minutes}m standard";

            if (topoSunriseEvent?.TopoEvent != null && topoSunsetEvent?.TopoEvent != null)
            {
                DateTime topoRise = topoSunriseEvent.TopoEvent.LocalTime;
                DateTime topoSet = topoSunsetEvent.TopoEvent.LocalTime;
                if (topoSet > topoRise)
                {
                    TimeSpan topoDuration = topoSet - topoRise;
                    report.TopoDaylightDisplay = $"{topoDuration.Hours}h {topoDuration.Minutes}m direct sun";

                    int lostMinutes = (int)(flatDuration - topoDuration).TotalMinutes;
                    if (lostMinutes > 0)
                    {
                        report.TopoLostDisplay = $"-{lostMinutes}m direct sunlight lost to mountain ridges";
                    }
                    else
                    {
                        report.TopoLostDisplay = "Full unobstructed direct sunlight";
                    }
                }
                else
                {
                    report.TopoDaylightDisplay = report.FlatDaylightDisplay;
                    report.TopoLostDisplay = "Full unobstructed daylight";
                }
            }
            else
            {
                report.TopoDaylightDisplay = report.FlatDaylightDisplay;
                report.TopoLostDisplay = "Standard flat horizon daylight";
            }

            // Yesterday's daylight comparison for seasonal day length trend
            DateTime yesterday = targetDate.AddDays(-1);
            var (flatRiseYest, flatSetYest) = FindSunCrossingsForTarget(lat, lon, yesterday, tzOffset, -0.833);
            if (flatRiseYest.HasValue && flatSetYest.HasValue)
            {
                TimeSpan yesterdayDuration = flatSetYest.Value - flatRiseYest.Value;
                int diffSec = (int)(flatDuration - yesterdayDuration).TotalSeconds;
                report.IsDayLengthening = diffSec >= 0;
                int absSec = Math.Abs(diffSec);
                int m = absSec / 60;
                int s = absSec % 60;
                report.DayChangeDisplay = report.IsDayLengthening
                    ? $"+{m}m {s}s longer than yesterday"
                    : $"-{m}m {s}s shorter than yesterday";
            }
            else
            {
                report.DayChangeDisplay = "Day length stable";
            }
        }
        else
        {
            report.FlatDaylightDisplay = "--";
            report.TopoDaylightDisplay = "--";
            report.TopoLostDisplay = "Continuous daylight or polar night";
            report.DayChangeDisplay = "--";
        }

        return report;
    }

    private static MilkyWayReport CalculateMilkyWayWindow(
        double lat,
        double lon,
        DateTime refTime,
        Func<double, double> getHorizonAltAtAz)
    {
        TimeSpan tzOffset = TimeZoneInfo.Local.GetUtcOffset(refTime);

        // Search upcoming night: if early morning (< 8 AM) and search from yesterday passed, scan tonight
        DateTime searchStart = (refTime.Hour < 8) ? refTime.Date.AddDays(-1).AddHours(18) : refTime.Date.AddHours(18);
        DateTime searchEnd = searchStart.AddHours(14); // Next morning 8:00 AM

        DateTime? winStart = null;
        DateTime? winEnd = null;

        double maxAlt = -90.0;
        DateTime peakTime = DateTime.MinValue;
        double peakAz = 180.0;
        double ridgeAtPeak = 0.0;
        double peakMoonPhase = 0.0;
        bool peakMoonBelowRidge = true;

        const int stepMin = 5;
        int steps = (int)((searchEnd - searchStart).TotalMinutes / stepMin);

        for (int i = 0; i <= steps; i++)
        {
            DateTime curTime = searchStart.AddMinutes(i * stepMin);
            DateTime curUtc = curTime - tzOffset;

            var (_, sunAlt) = GetSunPosition(curUtc, lat, lon);
            var (moonAz, moonAlt, moonPhase, _, _) = GetMoonPosition(curUtc, lat, lon);
            var (gcAz, gcAlt) = GetGalacticCenterPosition(curUtc, lat, lon);

            double topoAltGc = getHorizonAltAtAz(gcAz);
            double topoAltMoon = getHorizonAltAtAz(moonAz);

            // Criteria:
            // 1. Dark sky: Sun below -12° (nautical/astronomical darkness)
            // 2. Galactic Center above the mountain ridge and above horizontal
            // 3. Moon below mountain ridge OR moon phase < 0.25 (thin crescent)
            bool isDark = sunAlt <= -12.0;
            bool isGcAboveRidge = gcAlt > topoAltGc && gcAlt > 0.0;
            bool isMoonAcceptable = moonAlt < topoAltMoon || moonPhase < 0.25;

            bool isOptimal = isDark && isGcAboveRidge && isMoonAcceptable;

            if (isOptimal)
            {
                if (!winStart.HasValue) winStart = curTime;
                winEnd = curTime;

                if (gcAlt > maxAlt)
                {
                    maxAlt = gcAlt;
                    peakTime = curTime;
                    peakAz = gcAz;
                    ridgeAtPeak = topoAltGc;
                    peakMoonPhase = moonPhase;
                    peakMoonBelowRidge = moonAlt < topoAltMoon;
                }
            }
        }

        // If the window found has already ended in the past, scan the upcoming night
        if (winEnd.HasValue && winEnd.Value < refTime)
        {
            searchStart = searchStart.AddDays(1);
            searchEnd = searchStart.AddHours(14);
            winStart = null;
            winEnd = null;
            maxAlt = -90.0;
            peakTime = DateTime.MinValue;
            peakAz = 180.0;
            ridgeAtPeak = 0.0;
            peakMoonPhase = 0.0;
            peakMoonBelowRidge = true;

            steps = (int)((searchEnd - searchStart).TotalMinutes / stepMin);
            for (int i = 0; i <= steps; i++)
            {
                DateTime curTime = searchStart.AddMinutes(i * stepMin);
                DateTime curUtc = curTime - tzOffset;

                var (_, sunAlt) = GetSunPosition(curUtc, lat, lon);
                var (moonAz, moonAlt, moonPhase, _, _) = GetMoonPosition(curUtc, lat, lon);
                var (gcAz, gcAlt) = GetGalacticCenterPosition(curUtc, lat, lon);

                double topoAltGc = getHorizonAltAtAz(gcAz);
                double topoAltMoon = getHorizonAltAtAz(moonAz);

                bool isDark = sunAlt <= -12.0;
                bool isGcAboveRidge = gcAlt > topoAltGc && gcAlt > 0.0;
                bool isMoonAcceptable = moonAlt < topoAltMoon || moonPhase < 0.25;

                bool isOptimal = isDark && isGcAboveRidge && isMoonAcceptable;

                if (isOptimal)
                {
                    if (!winStart.HasValue) winStart = curTime;
                    winEnd = curTime;

                    if (gcAlt > maxAlt)
                    {
                        maxAlt = gcAlt;
                        peakTime = curTime;
                        peakAz = gcAz;
                        ridgeAtPeak = topoAltGc;
                        peakMoonPhase = moonPhase;
                        peakMoonBelowRidge = moonAlt < topoAltMoon;
                    }
                }
            }
        }

        var report = new MilkyWayReport();

        if (winStart.HasValue && winEnd.HasValue && (winEnd.Value - winStart.Value).TotalMinutes >= 15)
        {
            TimeSpan duration = winEnd.Value - winStart.Value;
            report.IsVisibleTonight = true;
            report.WindowStartTime = winStart.Value;
            report.WindowEndTime = winEnd.Value;

            string dayPfx = GetDayPrefix(winStart.Value, refTime);
            report.WindowDisplay = $"{dayPfx}{winStart.Value:h:mm tt} – {winEnd.Value:h:mm tt} ({duration.Hours}h {duration.Minutes}m)";
            report.PeakDisplay = $"Peak: {maxAlt:F1}° Alt at {peakTime:h:mm tt} ({Math.Round(peakAz)}° {GetCardinal(peakAz)})";
            double clearance = maxAlt - ridgeAtPeak;
            report.MountainClearanceDisplay = $"Clears southern ridge (+{ridgeAtPeak:F1}°) by +{clearance:F1}°";

            if (peakMoonBelowRidge)
            {
                report.MoonInterferenceDisplay = "Moon below mountain horizon (Zero lunar washout)";
            }
            else
            {
                report.MoonInterferenceDisplay = $"Moon: {Math.Round(peakMoonPhase * 100)}% thin crescent (Minimal glare)";
            }

            report.StatusText = "Milky Way Core visible in dark skies";
            report.IsQualityPositive = true;

            bool isActive = refTime >= winStart.Value && refTime <= winEnd.Value;
            bool isFuture = refTime < winStart.Value;
            report.IsActive = isActive;
            report.IsFuture = isFuture;

            if (isActive)
            {
                report.QualityBadge = "● Active Now";
                report.CountdownDisplay = "● Active Now";
            }
            else if (isFuture)
            {
                report.QualityBadge = $"+ {FormatDuration(winStart.Value - refTime)}";
                report.CountdownDisplay = $"+ {FormatDuration(winStart.Value - refTime)}";
            }
            else
            {
                report.QualityBadge = "✓ Passed";
                report.CountdownDisplay = "✓ Passed";
            }
        }
        else
        {
            report.IsVisibleTonight = false;
            report.WindowDisplay = "No dark-sky window tonight";

            DateTime fallbackNight = (refTime.Hour >= 12 ? refTime.Date.AddHours(22) : refTime.Date.AddDays(-1).AddHours(22));
            if (fallbackNight < refTime) fallbackNight = refTime.Date.AddHours(22);
            report.WindowStartTime = fallbackNight;

            // Determine why it's not visible
            var (_, currentMoonPhase, _, _, _) = GetMoonPosition(refTime - tzOffset, lat, lon);
            if (currentMoonPhase > 0.50)
            {
                report.StatusText = $"Washout by bright moon ({Math.Round(currentMoonPhase * 100)}% illuminated)";
                report.QualityBadge = "🌙 Lunar Glare";
                report.CountdownDisplay = "🌙 Lunar Glare";
            }
            else
            {
                report.StatusText = "Galactic Center below mountain ridge/horizon during dark hours (Winter season)";
                report.QualityBadge = "🍂 Off-Season";
                report.CountdownDisplay = "🍂 Off-Season";
            }

            report.PeakDisplay = $"Culmination: ~{Math.Round(90.0 - Math.Abs(lat) - 29.0):F0}° Alt (Daytime transit)";
            report.MountainClearanceDisplay = "Core traverses sky during sunlit hours";
            report.MoonInterferenceDisplay = "Milky Way season peaks in Spring & Summer";
            report.IsQualityPositive = false;
        }

        return report;
    }

    private static SeasonMarkerReport CalculateSeasonMarker(double lat, DateTime refTime)
    {
        int year = refTime.Year;
        // 0: March Equinox, 1: June Solstice, 2: Sept Equinox, 3: Dec Solstice
        var milestones = new List<(int Type, DateTime DateUtc)>
        {
            (0, GetEquinoxSolsticeUtc(year, 0)),
            (1, GetEquinoxSolsticeUtc(year, 1)),
            (2, GetEquinoxSolsticeUtc(year, 2)),
            (3, GetEquinoxSolsticeUtc(year, 3)),
            (0, GetEquinoxSolsticeUtc(year + 1, 0))
        };

        TimeSpan tzOffset = TimeZoneInfo.Local.GetUtcOffset(refTime);

        (int Type, DateTime DateUtc) next = milestones[0];
        foreach (var m in milestones)
        {
            DateTime localM = m.DateUtc + tzOffset;
            if (localM.Date >= refTime.Date)
            {
                next = m;
                break;
            }
        }

        DateTime nextLocal = next.DateUtc + tzOffset;
        int daysUntil = (int)Math.Ceiling((nextLocal.Date - refTime.Date).TotalDays);

        bool isNorth = lat >= 0;
        string markerName = next.Type switch
        {
            0 => isNorth ? "Spring (Vernal) Equinox" : "Autumn Equinox",
            1 => isNorth ? "Summer Solstice" : "Winter Solstice",
            2 => isNorth ? "Autumn Equinox" : "Spring (Vernal) Equinox",
            _ => isNorth ? "Winter Solstice" : "Summer Solstice"
        };

        string currentSeason = (refTime.Month, isNorth) switch
        {
            (3 or 4 or 5, true) => "Spring",
            (6 or 7 or 8, true) => "Summer",
            (9 or 10 or 11, true) => "Autumn (Fall)",
            (_, true) => "Winter",
            (3 or 4 or 5, false) => "Autumn (Fall)",
            (6 or 7 or 8, false) => "Winter",
            (9 or 10 or 11, false) => "Spring",
            (_, false) => "Summer"
        };

        string desc = next.Type switch
        {
            0 => "Day and night are approximately equal in length; sun crosses celestial equator northwards.",
            1 => isNorth
                ? "Longest day and shortest night of the year; maximum solar altitude."
                : "Shortest day and longest night of the year; minimum solar altitude.",
            2 => "Day and night are approximately equal in length; sun crosses celestial equator southwards.",
            _ => isNorth
                ? "Shortest day and longest night of the year; solar arc begins rising northwards afterwards."
                : "Longest day and shortest night of the year; maximum solar altitude."
        };

        string trend = (next.Type, isNorth) switch
        {
            (1 or 3, true) => next.Type == 3 ? "Days currently decreasing; daylight begins lengthening after this milestone." : "Days currently increasing; daylight begins shortening after this milestone.",
            (1 or 3, false) => next.Type == 1 ? "Days currently decreasing; daylight begins lengthening after this milestone." : "Days currently increasing; daylight begins shortening after this milestone.",
            _ => "Solar elevation changing by ~1–2 minutes of daylight daily."
        };

        return new SeasonMarkerReport
        {
            CurrentSeason = currentSeason,
            NextMarkerName = markerName,
            NextMarkerDateDisplay = nextLocal.ToString("dddd, MMM d, yyyy"),
            CountdownDisplay = daysUntil == 0 ? "Today!" : daysUntil == 1 ? "Tomorrow" : $"+ {daysUntil} days",
            Description = desc,
            TrendDescription = trend
        };
    }

    private static DateTime GetEquinoxSolsticeUtc(int year, int eventType)
    {
        double m = (year - 2000) / 1000.0;
        double jde0 = eventType switch
        {
            0 => 2451623.80984 + 365242.37404 * m + 0.05169 * m * m - 0.00411 * m * m * m - 0.00057 * m * m * m * m,
            1 => 2451716.56767 + 365241.62603 * m + 0.00325 * m * m + 0.00888 * m * m * m - 0.00030 * m * m * m * m,
            2 => 2451810.21715 + 365242.01767 * m - 0.11575 * m * m + 0.00337 * m * m * m + 0.00078 * m * m * m * m,
            _ => 2451900.05952 + 365242.74049 * m - 0.06223 * m * m - 0.00823 * m * m * m + 0.00032 * m * m * m * m
        };
        DateTime epoch = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        return epoch.AddDays(jde0 - 2451545.0);
    }

    #endregion
}

