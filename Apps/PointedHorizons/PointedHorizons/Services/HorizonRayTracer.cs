using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PointedHorizons.Models;

namespace PointedHorizons.Services;

/// <summary>
/// Ray tracing engine that computes the visible mountain horizon skyline,
/// layered depth ridges, and prominent high points from an observer's perspective
/// using SRTM DEM elevation data with Earth curvature and atmospheric refraction.
/// </summary>
public static class HorizonRayTracer
{
    private const double EarthRadiusM = 6_371_000.0;
    private const double RefractionK = 0.14; // Standard atmospheric refraction
    private static readonly double EffectiveEarthRadiusM = EarthRadiusM / (1.0 - RefractionK);

    // Well-known summits dictionary for auto-labeling when in vicinity
    private static readonly List<(string Name, double Lat, double Lon, double ElevationM)> KnownPeaks =
    [
        // Pacific Northwest / Rainier
        ("Mount Rainier", 46.8529, -121.7604, 4392),
        ("Little Tahoma Peak", 46.8517, -121.7139, 3395),
        ("Mount Adams", 46.2024, -121.4909, 3743),
        ("Mount St. Helens", 46.1912, -122.1944, 2549),
        ("Mount Hood", 45.3735, -121.6959, 3429),

        // Southwest / Arizona / Santa Catalina Mountains & Rincons
        ("Mount Lemmon", 32.4431, -110.7884, 2791),
        ("Mount Kimball", 32.3683, -110.8753, 2212),
        ("Cathedral Rock", 32.3667, -110.8917, 2420),
        ("Pusch Peak", 32.3556, -110.9500, 1634),
        ("Baboquivari Peak", 31.7844, -111.5978, 2356),
        ("Mica Mountain", 32.2178, -110.5433, 2641),
        ("Rincon Peak", 32.1217, -110.5244, 2588),
        ("Tanque Verde Ridge", 32.1850, -110.6500, 2100),
        ("Spud Rock", 32.2217, -110.5639, 2619),
        ("Wasson Peak", 32.2744, -111.1444, 1429),
        ("Humphreys Peak", 35.3464, -111.6780, 3852),
        ("Agassiz Peak", 35.3267, -111.6778, 3766),

        // Colorado Rockies
        ("Pikes Peak", 38.8405, -105.0442, 4302),
        ("Mount Elbert", 39.1178, -106.4454, 4401),
        ("Mount Massive", 39.1875, -106.4757, 4398),
        ("Longs Peak", 40.2550, -105.6151, 4346),
        ("Mount Evans", 39.5883, -105.6438, 4348),

        // California / Sierra Nevada
        ("Mount Whitney", 36.5786, -118.2920, 4421),
        ("Half Dome", 37.7459, -119.5332, 2694),
        ("El Capitan", 37.7340, -119.6377, 2307),
        ("Mount Dana", 37.8997, -119.2211, 3981),
        ("Mount Shasta", 41.4092, -122.1949, 4322),

        // Alps / Europe
        ("Matterhorn", 45.9765, 7.6586, 4478),
        ("Mont Blanc", 45.8326, 6.8652, 4809),
        ("Jungfrau", 46.5368, 7.9626, 4158),
        ("Eiger", 46.5775, 8.0053, 3967),
        ("Dufourspitze", 45.9369, 7.8668, 4634)
    ];

    /// <summary>
    /// Renders a horizon scene from observer coordinates and viewing direction.
    /// </summary>
    public static HorizonSceneData TraceScene(
        double observerLat,
        double observerLon,
        double observerElevationM,
        double azimuthCenter,
        double fieldOfView,
        double maxDistanceKm = 70.0,
        int rayCount = 1800,
        IReadOnlyList<DemPeakRecord>? candidatePeaks = null)
    {
        var sw = Stopwatch.StartNew();

        double eyeElevationM = observerElevationM + 2.0; // 2m above ground

        var profilePoints = new List<HorizonProfilePoint>(rayCount);

        // 8 depth layers for multi-ridge perspective shading (PeakFinder signature aesthetic)
        const int numLayers = 8;
        var layers = new List<HorizonProfilePoint>[numLayers];
        for (int l = 0; l < numLayers; l++)
        {
            layers[l] = new List<HorizonProfilePoint>(rayCount);
        }

        double minAlt = double.MaxValue;
        double maxAlt = double.MinValue;
        var usedTiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        double cosLat = Math.Max(0.01, Math.Cos(observerLat * Math.PI / 180.0));
        const double metersPerDegreeLat = 111_320.0;
        double metersPerDegreeLon = metersPerDegreeLat * cosLat;

        // Trace the complete 360° cylindrical panorama around the observer location
        for (int i = 0; i < rayCount; i++)
        {
            double rayAzimuth = (i / (double)rayCount) * 360.0;

            double azRad = rayAzimuth * Math.PI / 180.0;
            double sinAz = Math.Sin(azRad);
            double cosAz = Math.Cos(azRad);

            double maxAngleAll = -90.0;
            double bestDistAll = 0;
            double bestElevAll = 0;
            double bestLatAll = observerLat;
            double bestLonAll = observerLon;

            double[] maxAngleBands = new double[numLayers];
            double[] distBands = new double[numLayers];
            double[] elevBands = new double[numLayers];
            for (int l = 0; l < numLayers; l++)
            {
                maxAngleBands[l] = -90.0;
            }

            // Step through distances: close (high detail), medium, and far
            double d = 40.0; // Start at 40 meters
            double maxDistM = maxDistanceKm * 1000.0;

            while (d <= maxDistM)
            {
                double step;
                if (d < 3000) step = 40.0;
                else if (d < 10000) step = 80.0;
                else if (d < 25000) step = 140.0;
                else if (d < 50000) step = 220.0;
                else step = 320.0;

                double dy = d * cosAz;
                double dx = d * sinAz;

                double lat = observerLat + (dy / metersPerDegreeLat);
                double lon = observerLon + (dx / metersPerDegreeLon);

                string tileKey = HgtTile.GetTileKey(lat, lon);
                usedTiles.Add(tileKey);

                double? elev = DemService.GetElevation(lat, lon);
                if (elev.HasValue)
                {
                    double curDrop = (d * d) / (2.0 * EffectiveEarthRadiusM);
                    double altAngle = Math.Atan2(elev.Value - eyeElevationM - curDrop, d) * (180.0 / Math.PI);

                    if (altAngle > maxAngleAll)
                    {
                        maxAngleAll = altAngle;
                        bestDistAll = d;
                        bestElevAll = elev.Value;
                        bestLatAll = lat;
                        bestLonAll = lon;
                    }

                    int band = d switch
                    {
                        <= 2500 => 0,
                        <= 6000 => 1,
                        <= 11000 => 2,
                        <= 18000 => 3,
                        <= 28000 => 4,
                        <= 42000 => 5,
                        <= 58000 => 6,
                        _ => 7
                    };

                    if (altAngle > maxAngleBands[band])
                    {
                        maxAngleBands[band] = altAngle;
                        distBands[band] = d;
                        elevBands[band] = elev.Value;
                    }
                }

                d += step;
            }

            if (maxAngleAll <= -89.0)
            {
                // Fallback to geometric horizon if no terrain found
                maxAngleAll = -Math.Sqrt(2.0 * eyeElevationM / EffectiveEarthRadiusM) * (180.0 / Math.PI);
                bestDistAll = Math.Sqrt(2.0 * eyeElevationM * EffectiveEarthRadiusM);
                bestElevAll = 0;
            }

            minAlt = Math.Min(minAlt, maxAngleAll);
            maxAlt = Math.Max(maxAlt, maxAngleAll);

            int distBand = bestDistAll <= 6000 ? 0 : (bestDistAll <= 18000 ? 1 : (bestDistAll <= 42000 ? 2 : 3));

            var point = new HorizonProfilePoint
            {
                Azimuth = Math.Round(rayAzimuth, 2),
                AltitudeAngle = Math.Round(maxAngleAll, 2),
                DistanceKm = Math.Round(bestDistAll / 1000.0, 1),
                ElevationM = Math.Round(bestElevAll, 0),
                Lat = Math.Round(bestLatAll, 5),
                Lon = Math.Round(bestLonAll, 5),
                DistanceBand = distBand
            };

            profilePoints.Add(point);

            for (int l = 0; l < numLayers; l++)
            {
                layers[l].Add(new HorizonProfilePoint
                {
                    Azimuth = Math.Round(rayAzimuth, 2),
                    AltitudeAngle = Math.Round(maxAngleBands[l] > -89.0 ? maxAngleBands[l] : -15.0, 2),
                    DistanceKm = Math.Round(distBands[l] / 1000.0, 1),
                    ElevationM = Math.Round(elevBands[l], 0)
                });
            }
        }

        // Peak and High Point Detection using DEM companion .peaks.json files
        var activeCandidatePeaks = candidatePeaks ?? DemService.GetLoadedPeaksForTiles(usedTiles);
        var peaks = DetectPeaks(profilePoints, observerLat, observerLon, eyeElevationM, maxDistanceKm, activeCandidatePeaks);

        sw.Stop();

        string[] bandLabels =
        [
            "Foreground (<2.5 km)",
            "Near Ridges (2.5-6 km)",
            "Inner Midground (6-11 km)",
            "Midground (11-18 km)",
            "Outer Midground (18-28 km)",
            "Far Range (28-42 km)",
            "Distant Mountains (42-58 km)",
            "Distant Skyline (>58 km)"
        ];

        var ridgeLayersList = new List<HorizonRidgeLayer>(numLayers);
        for (int l = 0; l < numLayers; l++)
        {
            ridgeLayersList.Add(new HorizonRidgeLayer
            {
                LayerIndex = l,
                Label = bandLabels[l],
                Points = layers[l]
            });
        }

        return new HorizonSceneData
        {
            ObserverLat = observerLat,
            ObserverLon = observerLon,
            ObserverElevationM = observerElevationM,
            AzimuthCenter = azimuthCenter,
            FieldOfView = fieldOfView,
            MinAltitude = Math.Floor(minAlt - 1.5),
            MaxAltitude = Math.Ceiling(maxAlt + 3.0),
            ProfilePoints = profilePoints,
            Peaks = peaks,
            RidgeLayers = ridgeLayersList,
            TileKeysUsed = usedTiles.OrderBy(t => t).ToList(),
            CalculationTimeMs = sw.Elapsed.TotalMilliseconds
        };
    }

    /// <summary>
    /// Identifies prominent summits and high points along the skyline profile using DEM companion .peaks.json files.
    /// </summary>
    private static List<HorizonPeak> DetectPeaks(
        List<HorizonProfilePoint> points,
        double observerLat,
        double observerLon,
        double eyeElevationM,
        double maxDistanceKm,
        IReadOnlyList<DemPeakRecord>? candidatePeaks)
    {
        var rawPeaks = new List<HorizonPeak>();
        if (points.Count < 5) return rawPeaks;

        // 1. Detect local maxima along the ray-traced skyline profile
        int window = Math.Max(4, points.Count / 70);

        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            if (p.DistanceKm < 0.2) continue; // Ignore immediate observer bump

            bool isLocalMax = true;
            double leftMin = p.AltitudeAngle;
            double rightMin = p.AltitudeAngle;

            for (int w = 1; w <= window; w++)
            {
                int leftIdx = (i - w + points.Count) % points.Count;
                int rightIdx = (i + w) % points.Count;

                if (points[leftIdx].AltitudeAngle >= p.AltitudeAngle ||
                    points[rightIdx].AltitudeAngle >= p.AltitudeAngle)
                {
                    isLocalMax = false;
                    break;
                }
                leftMin = Math.Min(leftMin, points[leftIdx].AltitudeAngle);
                rightMin = Math.Min(rightMin, points[rightIdx].AltitudeAngle);
            }

            if (!isLocalMax) continue;

            double prominenceAngle = p.AltitudeAngle - Math.Max(leftMin, rightMin);
            // Must have noticeable visual prominence above neighboring saddles
            if (prominenceAngle < 0.08) continue;

            // Attempt to match with companion .peaks.json summits or fallback known summits
            var match = FindMatchingPeak(p.Lat, p.Lon, p.ElevationM, candidatePeaks);
            string peakName = match?.Name ?? $"Summit {Math.Round(p.ElevationM):N0}m";
            double peakElev = match?.ElevationM ?? p.ElevationM;

            rawPeaks.Add(new HorizonPeak
            {
                Name = peakName,
                ElevationM = Math.Round(peakElev, 0),
                DistanceKm = Math.Round(p.DistanceKm, 1),
                Azimuth = Math.Round(p.Azimuth, 1),
                AltitudeAngle = Math.Round(p.AltitudeAngle, 2),
                ProminenceM = Math.Round(prominenceAngle * (p.DistanceKm * 1000.0 * Math.PI / 180.0), 0),
                Lat = p.Lat,
                Lon = p.Lon
            });
        }

        // 2. Add prominent named summits from candidatePeaks that lie on or form the visible skyline
        if (candidatePeaks != null && candidatePeaks.Count > 0)
        {
            double cosObsLat = Math.Cos(observerLat * Math.PI / 180.0);

            for (int c = 0; c < candidatePeaks.Count; c++)
            {
                var cand = candidatePeaks[c];
                if (string.IsNullOrWhiteSpace(cand.Name)) continue;

                double dLat = (cand.Lat - observerLat) * 111.32;
                double dLon = (cand.Lon - observerLon) * 111.32 * cosObsLat;
                double distKm = Math.Sqrt(dLat * dLat + dLon * dLon);

                if (distKm < 0.6 || distKm > maxDistanceKm) continue;

                double az = CalculateBearing(observerLat, observerLon, cand.Lat, cand.Lon);
                double skylineAlt = InterpolateSkylineAltitude(points, az);

                // Calculate visual altitude angle with Earth curvature and atmospheric refraction
                double dM = distKm * 1000.0;
                double curDrop = (dM * dM) / (2.0 * EffectiveEarthRadiusM);
                double candAltAngle = Math.Atan2(cand.Ele - eyeElevationM - curDrop, dM) * (180.0 / Math.PI);

                // If summit forms or sits right at the visible skyline (within line-of-sight tolerance)
                if (candAltAngle >= skylineAlt - 0.25)
                {
                    rawPeaks.Add(new HorizonPeak
                    {
                        Name = cand.Name,
                        ElevationM = Math.Round(cand.Ele, 0),
                        DistanceKm = Math.Round(distKm, 1),
                        Azimuth = Math.Round(az, 1),
                        AltitudeAngle = Math.Round(Math.Max(candAltAngle, skylineAlt), 2),
                        ProminenceM = 0,
                        Lat = cand.Lat,
                        Lon = cand.Lon
                    });
                }
            }
        }

        // 3. Deduplicate peaks that are close in azimuth (< 1.8 degrees).
        // Prioritize named summits over generic "Summit ..." placeholders, then by visual altitude.
        var filteredPeaks = new List<HorizonPeak>();
        foreach (var peak in rawPeaks
            .OrderByDescending(p => IsNamedSummit(p.Name))
            .ThenByDescending(p => p.AltitudeAngle)
            .ThenByDescending(p => p.ElevationM))
        {
            bool collides = filteredPeaks.Any(existing =>
                AngularDistance(existing.Azimuth, peak.Azimuth) < 1.8);

            if (!collides)
            {
                filteredPeaks.Add(peak);
            }

            if (filteredPeaks.Count >= 48) break; // Capture top prominent peaks around the full 360° circle
        }

        return filteredPeaks.OrderBy(p => p.Azimuth).ToList();
    }

    private static bool IsNamedSummit(string? name) =>
        !string.IsNullOrWhiteSpace(name) && !name.StartsWith("Summit ", StringComparison.OrdinalIgnoreCase);

    private static (string Name, double ElevationM)? FindMatchingPeak(
        double lat,
        double lon,
        double elevM,
        IReadOnlyList<DemPeakRecord>? candidatePeaks)
    {
        (string Name, double ElevationM)? bestMatch = null;
        double bestScore = double.MaxValue;

        if (candidatePeaks != null && candidatePeaks.Count > 0)
        {
            for (int i = 0; i < candidatePeaks.Count; i++)
            {
                var p = candidatePeaks[i];
                if (string.IsNullOrWhiteSpace(p.Name)) continue;

                double dLat = (lat - p.Lat) * 111.32;
                double dLon = (lon - p.Lon) * 111.32 * Math.Cos(lat * Math.PI / 180.0);
                double distKm = Math.Sqrt(dLat * dLat + dLon * dLon);

                if (distKm > 3.0) continue;

                double elevDiff = (p.Ele > 0 && elevM > 0) ? Math.Abs(elevM - p.Ele) : 0.0;
                if (elevDiff > 250) continue;

                double score = distKm + (elevDiff / 100.0);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestMatch = (p.Name, p.Ele > 0 ? p.Ele : elevM);
                }
            }

            if (bestMatch.HasValue) return bestMatch;
        }

        // Fallback to built-in KnownPeaks dictionary if not matched in companion peaks
        foreach (var (name, kLat, kLon, kElev) in KnownPeaks)
        {
            double dLat = (lat - kLat) * 111.32;
            double dLon = (lon - kLon) * 111.32 * Math.Cos(lat * Math.PI / 180.0);
            double distKm = Math.Sqrt(dLat * dLat + dLon * dLon);

            if (distKm < 3.0 && Math.Abs(elevM - kElev) < 250)
            {
                double score = distKm + Math.Abs(elevM - kElev) / 100.0;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestMatch = (name, kElev);
                }
            }
        }

        return bestMatch;
    }

    private static double CalculateBearing(double lat1, double lon1, double lat2, double lon2)
    {
        double lat1Rad = lat1 * Math.PI / 180.0;
        double lat2Rad = lat2 * Math.PI / 180.0;
        double dLonRad = (lon2 - lon1) * Math.PI / 180.0;

        double y = Math.Sin(dLonRad) * Math.Cos(lat2Rad);
        double x = Math.Cos(lat1Rad) * Math.Sin(lat2Rad) -
                   Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(dLonRad);

        double brg = Math.Atan2(y, x) * (180.0 / Math.PI);
        return NormalizeAngle(brg);
    }

    private static double InterpolateSkylineAltitude(List<HorizonProfilePoint> points, double az)
    {
        if (points.Count == 0) return -90.0;

        double step = 360.0 / points.Count;
        int idx = (int)Math.Floor(NormalizeAngle(az) / step);
        if (idx < 0) idx = 0;
        if (idx >= points.Count) idx = points.Count - 1;

        int nextIdx = (idx + 1) % points.Count;
        double fraction = (NormalizeAngle(az) - (idx * step)) / step;

        return points[idx].AltitudeAngle + fraction * (points[nextIdx].AltitudeAngle - points[idx].AltitudeAngle);
    }

    private static double AngularDistance(double a, double b)
    {
        double diff = Math.Abs(a - b) % 360.0;
        return diff > 180.0 ? 360.0 - diff : diff;
    }

    private static double NormalizeAngle(double angle)
    {
        angle %= 360.0;
        if (angle < 0) angle += 360.0;
        return angle;
    }

    public record struct FarthestVisiblePoint(double Lat, double Lon, double DistanceKm, double ElevationM);

    /// <summary>
    /// Traces a single ray along a given bearing out to maxDistanceKm and identifies
    /// the farthest visible land feature (unobstructed terrain point with line-of-sight from the observer).
    /// Returns null if no terrain point beyond the minimum threshold (1.5 km) is visible.
    /// </summary>
    public static FarthestVisiblePoint? FindFarthestVisibleLandFeature(
        double observerLat,
        double observerLon,
        double eyeElevationM,
        double bearingDeg,
        double maxDistanceKm = 300.0)
    {
        const double R = 6371.0;
        double bRad = bearingDeg * Math.PI / 180.0;
        double latRad = observerLat * Math.PI / 180.0;
        double lonRad = observerLon * Math.PI / 180.0;
        double sinLat = Math.Sin(latRad);
        double cosLat = Math.Cos(latRad);
        double sinB = Math.Sin(bRad);
        double cosB = Math.Cos(bRad);

        double maxDistM = maxDistanceKm * 1000.0;
        double maxAngle = -90.0;

        double bestDistM = 0;
        double bestLat = 0;
        double bestLon = 0;
        double bestElev = 0;

        // Minimum distance to count as a distinct land feature (1.5 km)
        const double minFeatureDistM = 1500.0;

        double d = 60.0; // Start at 60 meters
        while (d <= maxDistM)
        {
            double step;
            if (d < 3000) step = 40.0;
            else if (d < 10000) step = 80.0;
            else if (d < 30000) step = 140.0;
            else if (d < 70000) step = 200.0;
            else if (d < 150000) step = 300.0;
            else step = 450.0;

            double dByR = (d / 1000.0) / R;
            double sinD = Math.Sin(dByR);
            double cosD = Math.Cos(dByR);

            double ptLatRad = Math.Asin(sinLat * cosD + cosLat * sinD * cosB);
            double ptLonRad = lonRad + Math.Atan2(sinB * sinD * cosLat, cosD - sinLat * Math.Sin(ptLatRad));

            double ptLat = ptLatRad * 180.0 / Math.PI;
            double ptLon = ptLonRad * 180.0 / Math.PI;

            double? elev = DemService.GetElevation(ptLat, ptLon);
            if (elev.HasValue)
            {
                double curDrop = (d * d) / (2.0 * EffectiveEarthRadiusM);
                double altAngle = Math.Atan2(elev.Value - eyeElevationM - curDrop, d) * (180.0 / Math.PI);

                if (altAngle > maxAngle)
                {
                    maxAngle = altAngle;

                    if (d >= minFeatureDistM)
                    {
                        bestDistM = d;
                        bestLat = ptLat;
                        bestLon = ptLon;
                        bestElev = elev.Value;
                    }
                }
            }

            d += step;
        }

        if (bestDistM >= minFeatureDistM)
        {
            return new FarthestVisiblePoint(bestLat, bestLon, Math.Round(bestDistM / 1000.0, 2), Math.Round(bestElev, 1));
        }

        return null;
    }
}
