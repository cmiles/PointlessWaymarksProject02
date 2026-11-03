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
}

