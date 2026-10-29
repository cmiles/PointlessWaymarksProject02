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
        int rayCount = 1800)
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

        // Peak and High Point Detection
        var peaks = DetectPeaks(profilePoints, observerLat, observerLon);

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
    /// Identifies prominent summits and high points along the skyline profile.
    /// </summary>
    private static List<HorizonPeak> DetectPeaks(List<HorizonProfilePoint> points, double observerLat, double observerLon)
    {
        var rawPeaks = new List<HorizonPeak>();
        if (points.Count < 5) return rawPeaks;

        // Window size for local maximum detection across 360° circle (approx 2.5 degrees)
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

            // Attempt to match with known summits
            string peakName = FindMatchingKnownPeak(p.Lat, p.Lon, p.ElevationM)
                              ?? $"Summit {Math.Round(p.ElevationM):N0}m";

            rawPeaks.Add(new HorizonPeak
            {
                Name = peakName,
                ElevationM = Math.Round(p.ElevationM, 0),
                DistanceKm = Math.Round(p.DistanceKm, 1),
                Azimuth = Math.Round(p.Azimuth, 1),
                AltitudeAngle = Math.Round(p.AltitudeAngle, 2),
                ProminenceM = Math.Round(prominenceAngle * (p.DistanceKm * 1000.0 * Math.PI / 180.0), 0),
                Lat = p.Lat,
                Lon = p.Lon
            });
        }

        // Deduplicate peaks that are too close together in azimuth (< 2.0 degrees)
        var filteredPeaks = new List<HorizonPeak>();
        foreach (var peak in rawPeaks.OrderByDescending(p => p.AltitudeAngle).ThenByDescending(p => p.ElevationM))
        {
            bool collides = filteredPeaks.Any(existing =>
                AngularDistance(existing.Azimuth, peak.Azimuth) < 2.0);

            if (!collides)
            {
                filteredPeaks.Add(peak);
            }

            if (filteredPeaks.Count >= 24) break; // Capture top prominent peaks around the full 360° circle
        }

        return filteredPeaks.OrderBy(p => p.Azimuth).ToList();
    }

    private static double AngularDistance(double a, double b)
    {
        double diff = Math.Abs(a - b) % 360.0;
        return diff > 180.0 ? 360.0 - diff : diff;
    }

    private static string? FindMatchingKnownPeak(double lat, double lon, double elevM)
    {
        foreach (var (name, kLat, kLon, kElev) in KnownPeaks)
        {
            double dLat = (lat - kLat) * 111.32;
            double dLon = (lon - kLon) * 111.32 * Math.Cos(lat * Math.PI / 180.0);
            double distKm = Math.Sqrt(dLat * dLat + dLon * dLon);

            // Within 2.5 km and within 150m elevation
            if (distKm < 2.5 && Math.Abs(elevM - kElev) < 180)
            {
                return name;
            }
        }
        return null;
    }

    private static double NormalizeAngle(double angle)
    {
        angle %= 360.0;
        if (angle < 0) angle += 360.0;
        return angle;
    }
}
