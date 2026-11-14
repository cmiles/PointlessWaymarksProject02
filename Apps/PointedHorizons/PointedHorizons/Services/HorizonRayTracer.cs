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

        // 14 depth layers for multi-ridge perspective shading (rich topography aesthetic)
        const int numLayers = 14;
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
                        <= 1500 => 0,   // Immediate Foreground (<1.5 km)
                        <= 3000 => 1,   // Near Foreground (1.5-3 km)
                        <= 5000 => 2,   // Close Ridges (3-5 km)
                        <= 8000 => 3,   // Near Midground (5-8 km)
                        <= 12000 => 4,  // Inner Midground (8-12 km)
                        <= 17000 => 5,  // Midground (12-17 km)
                        <= 23000 => 6,  // Outer Midground (17-23 km)
                        <= 30000 => 7,  // Near Far Range (23-30 km)
                        <= 39000 => 8,  // Far Range (30-39 km)
                        <= 50000 => 9,  // Distant Range (39-50 km)
                        <= 64000 => 10, // Distant Mountains (50-64 km)
                        <= 80000 => 11, // Far Skyline (64-80 km)
                        <= 100000 => 12,// Outer Skyline (80-100 km)
                        _ => 13         // Deep Skyline (>100 km)
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

        string[] bandLabels =
        [
            "Immediate Foreground (<1.5 km)",
            "Near Foreground (1.5-3 km)",
            "Close Ridges (3-5 km)",
            "Near Midground (5-8 km)",
            "Inner Midground (8-12 km)",
            "Midground (12-17 km)",
            "Outer Midground (17-23 km)",
            "Near Far Range (23-30 km)",
            "Far Range (30-39 km)",
            "Distant Range (39-50 km)",
            "Distant Mountains (50-64 km)",
            "Far Skyline (64-80 km)",
            "Outer Skyline (80-100 km)",
            "Deep Skyline (>100 km)"
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

        // Peak and High Point Detection using DEM companion .peaks.json files & intermediate ridge maxima
        var activeCandidatePeaks = candidatePeaks ?? DemService.GetLoadedPeaksForTiles(usedTiles);
        var peaks = DetectPeaks(profilePoints, observerLat, observerLon, eyeElevationM, maxDistanceKm, activeCandidatePeaks, ridgeLayersList);

        sw.Stop();

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
    /// Identifies all visible points from the companion peak/point files (.peaks.json).
    /// Draws labels for all visible points regardless of whether they are a local maxima or intermediate point or not.
    /// </summary>
    private static List<HorizonPeak> DetectPeaks(
        List<HorizonProfilePoint> points,
        double observerLat,
        double observerLon,
        double eyeElevationM,
        double maxDistanceKm,
        IReadOnlyList<DemPeakRecord>? candidatePeaks,
        IReadOnlyList<HorizonRidgeLayer>? ridgeLayers = null)
    {
        var visiblePeaks = new List<HorizonPeak>();
        if (points.Count < 5) return visiblePeaks;

        // Collect all distinct named candidate points from the companion peak/point files
        var allCandidates = new List<DemPeakRecord>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (candidatePeaks != null && candidatePeaks.Count > 0)
        {
            for (int i = 0; i < candidatePeaks.Count; i++)
            {
                var p = candidatePeaks[i];
                if (string.IsNullOrWhiteSpace(p.Name)) continue;
                if (p.Name.StartsWith("Ridge ", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.StartsWith("Summit ", StringComparison.OrdinalIgnoreCase)) continue;

                string key = $"{p.Name.Trim()}_{Math.Round(p.Lat, 4)}_{Math.Round(p.Lon, 4)}";
                if (seenKeys.Add(key))
                {
                    allCandidates.Add(p);
                }
            }
        }

        if (allCandidates.Count == 0) return visiblePeaks;

        double cosObsLat = Math.Cos(observerLat * Math.PI / 180.0);

        // Evaluate visibility for every point in the peak/point file
        for (int c = 0; c < allCandidates.Count; c++)
        {
            var cand = allCandidates[c];

            double dLat = (cand.Lat - observerLat) * 111.32;
            double dLon = (cand.Lon - observerLon) * 111.32 * cosObsLat;
            double distKm = Math.Sqrt(dLat * dLat + dLon * dLon);

            // Skip if observer is right on the feature (< 100m) or beyond horizon max distance
            if (distKm < 0.1 || distKm > maxDistanceKm) continue;

            double az = CalculateBearing(observerLat, observerLon, cand.Lat, cand.Lon);
            double skylineAlt = InterpolateSkylineAltitude(points, az);

            // Determine elevation of the feature
            double targetElev = cand.Ele > 0 ? cand.Ele : (DemService.GetElevation(cand.Lat, cand.Lon) ?? 0);
            if (targetElev <= 0) continue;

            // Calculate visual altitude angle accounting for Earth curvature and refraction
            double dM = distKm * 1000.0;
            double curDrop = (dM * dM) / (2.0 * EffectiveEarthRadiusM);
            double candAltAngle = Math.Atan2(targetElev - eyeElevationM - curDrop, dM) * (180.0 / Math.PI);

            // 1. Skyline visibility: point sits at or above the skyline silhouette
            bool isSkyline = candAltAngle >= skylineAlt - 0.25;

            // 2. Line of sight visibility: if below the skyline, verify no closer terrain blocks the view
            bool isVisible = isSkyline || IsPeakLineOfSightClear(observerLat, observerLon, eyeElevationM, cand.Lat, cand.Lon, targetElev, candAltAngle, dM);

            if (isVisible)
            {
                // Prevent duplicate markers if a summit is cataloged multiple times within close proximity
                var existing = visiblePeaks.FirstOrDefault(v =>
                    v.Name.Equals(cand.Name.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    (Math.Abs(v.DistanceKm - distKm) < 1.5 || AngularDistance(v.Azimuth, az) < 0.5));

                if (existing != null)
                {
                    if (targetElev > existing.ElevationM)
                    {
                        visiblePeaks.Remove(existing);
                        visiblePeaks.Add(new HorizonPeak
                        {
                            Name = cand.Name.Trim(),
                            ElevationM = Math.Round(targetElev, 0),
                            DistanceKm = Math.Round(distKm, 1),
                            Azimuth = Math.Round(az, 1),
                            AltitudeAngle = Math.Round(isSkyline ? Math.Max(candAltAngle, skylineAlt) : candAltAngle, 2),
                            ProminenceM = 0,
                            Lat = cand.Lat,
                            Lon = cand.Lon,
                            IsSkyline = isSkyline
                        });
                    }
                }
                else
                {
                    visiblePeaks.Add(new HorizonPeak
                    {
                        Name = cand.Name.Trim(),
                        ElevationM = Math.Round(targetElev, 0),
                        DistanceKm = Math.Round(distKm, 1),
                        Azimuth = Math.Round(az, 1),
                        AltitudeAngle = Math.Round(isSkyline ? Math.Max(candAltAngle, skylineAlt) : candAltAngle, 2),
                        ProminenceM = 0,
                        Lat = cand.Lat,
                        Lon = cand.Lon,
                        IsSkyline = isSkyline
                    });
                }
            }
        }

        // Return all visible points from the peak/point files sorted around the 360° horizon
        return visiblePeaks.OrderBy(p => p.Azimuth).ToList();
    }

    /// <summary>
    /// Performs a fast targeted line-of-sight test from observer to candidate summit,
    /// verifying that no intermediate terrain obstructs the view.
    /// </summary>
    private static bool IsPeakLineOfSightClear(
        double observerLat,
        double observerLon,
        double eyeElevationM,
        double peakLat,
        double peakLon,
        double peakElevM,
        double targetAltAngle,
        double targetDistM)
    {
        // Step along line from observer to peak at ~140m intervals
        int steps = (int)Math.Max(5, targetDistM / 140.0);
        double dLat = (peakLat - observerLat);
        double dLon = (peakLon - observerLon);

        for (int s = 1; s < steps - 1; s++) // Stop before reaching the peak summit itself
        {
            double frac = (double)s / steps;
            double curDistM = frac * targetDistM;
            double curLat = observerLat + frac * dLat;
            double curLon = observerLon + frac * dLon;

            double? elev = DemService.GetElevation(curLat, curLon);
            if (elev.HasValue)
            {
                double drop = (curDistM * curDistM) / (2.0 * EffectiveEarthRadiusM);
                double terrainAlt = Math.Atan2(elev.Value - eyeElevationM - drop, curDistM) * (180.0 / Math.PI);

                if (terrainAlt > targetAltAngle + 0.05)
                {
                    return false; // Obstructed by closer terrain
                }
            }
        }

        return true; // Direct line of sight is clear
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
