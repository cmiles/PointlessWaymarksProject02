using System;
using System.Collections.Generic;
using System.IO;

namespace PointedHorizons.Models;

/// <summary>
/// Summary and spatial metadata for a track file (.gpx, .tcx, .fit) on disk.
/// Includes pre-computed bounds and decimated coordinates for instant map visualization.
/// </summary>
public class TrackFileInfo
{
    public string Id { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileFormat { get; set; } = "GPX"; // GPX, TCX, FIT
    public long FileSizeBytes { get; set; }
    public DateTime LastModifiedUtc { get; set; }
    public string FileHash { get; set; } = string.Empty;

    public string TrackName { get; set; } = string.Empty;
    public int PointCount { get; set; }
    public double TotalDistanceKm { get; set; }

    public double MinLat { get; set; }
    public double MaxLat { get; set; }
    public double MinLon { get; set; }
    public double MaxLon { get; set; }

    public DateTime? StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }

    public bool IsVisibleOnMap { get; set; } = true;
    public string DisplayColor { get; set; } = "#F97316";

    /// <summary>
    /// Cumulative elevation gain in meters.
    /// </summary>
    public double TotalClimbM { get; set; }

    /// <summary>
    /// Cumulative elevation loss in meters.
    /// </summary>
    public double TotalDescentM { get; set; }

    /// <summary>
    /// Minimum elevation in meters.
    /// </summary>
    public double MinElevationM { get; set; }

    /// <summary>
    /// Maximum elevation in meters.
    /// </summary>
    public double MaxElevationM { get; set; }

    /// <summary>
    /// Decimated [lon, lat] or [lon, lat, ele] coordinates for fast GeoJSON map streaming.
    /// </summary>
    public List<double[]> SimplifiedCoordinates { get; set; } = new();

    /// <summary>
    /// Downsampled elevation profile points [distanceKm, elevationM, lat, lon] for interactive elevation graph.
    /// </summary>
    public List<double[]> ElevationProfile { get; set; } = new();

    /// <summary>
    /// Waypoints extracted from this GPX file.
    /// </summary>
    public List<WaypointInfo> Waypoints { get; set; } = new();

    /// <summary>
    /// Routes extracted from this GPX file.
    /// </summary>
    public List<RouteInfo> Routes { get; set; } = new();

    public TimeSpan Duration => (EndTimeUtc.HasValue && StartTimeUtc.HasValue)
        ? (EndTimeUtc.Value - StartTimeUtc.Value)
        : TimeSpan.Zero;

    public string FormattedSize
    {
        get
        {
            if (FileSizeBytes >= 1024 * 1024)
                return $"{(FileSizeBytes / (1024.0 * 1024.0)):F1} MB";
            if (FileSizeBytes >= 1024)
                return $"{(FileSizeBytes / 1024.0):F0} KB";
            return $"{FileSizeBytes} B";
        }
    }

    public string FormattedDistance(bool isMetric = false)
    {
        if (isMetric)
            return $"{TotalDistanceKm:F1} km";
        double miles = TotalDistanceKm * 0.621371;
        return $"{miles:F1} mi";
    }

    public string FormattedClimb(bool isMetric = false)
    {
        if (isMetric)
            return $"↗ +{Math.Round(TotalClimbM):N0} m";
        double feet = TotalClimbM * 3.28084;
        return $"↗ +{Math.Round(feet):N0} ft";
    }

    public string FormattedDescent(bool isMetric = false)
    {
        if (isMetric)
            return $"↘ -{Math.Round(TotalDescentM):N0} m";
        double feet = TotalDescentM * 3.28084;
        return $"↘ -{Math.Round(feet):N0} ft";
    }

    public string FormattedElevationRange(bool isMetric = false)
    {
        if (MinElevationM <= 0 && MaxElevationM <= 0) return "--";
        if (isMetric)
            return $"{Math.Round(MinElevationM):N0} m – {Math.Round(MaxElevationM):N0} m";
        double minFt = MinElevationM * 3.28084;
        double maxFt = MaxElevationM * 3.28084;
        return $"{Math.Round(minFt):N0} ft – {Math.Round(maxFt):N0} ft";
    }

    public void EnsureElevationProfile()
    {
        if (ElevationProfile.Count > 0 || SimplifiedCoordinates.Count < 2) return;

        double cumDist = 0.0;
        double minE = double.MaxValue;
        double maxE = double.MinValue;
        double climb = 0.0;
        double descent = 0.0;

        for (int i = 0; i < SimplifiedCoordinates.Count; i++)
        {
            var pt = SimplifiedCoordinates[i];
            if (pt == null || pt.Length < 2) continue;
            double lon = pt[0];
            double lat = pt[1];
            double ele = pt.Length > 2 ? pt[2] : 0.0;

            if (i > 0)
            {
                var prev = SimplifiedCoordinates[i - 1];
                cumDist += PointedHorizons.Services.TrackStorageService.DistanceKm(prev[1], prev[0], lat, lon);
                if (ele > 0 && prev.Length > 2 && prev[2] > 0)
                {
                    double diff = ele - prev[2];
                    if (diff > 0.6) climb += diff;
                    else if (diff < -0.6) descent += Math.Abs(diff);
                }
            }

            if (ele > 0)
            {
                if (ele < minE) minE = ele;
                if (ele > maxE) maxE = ele;
            }

            ElevationProfile.Add(new[] { Math.Round(cumDist, 2), Math.Round(ele, 1), lat, lon });
        }

        if (TotalClimbM <= 0 && climb > 0) TotalClimbM = climb;
        if (TotalDescentM <= 0 && descent > 0) TotalDescentM = descent;
        if (MinElevationM <= 0 && minE < double.MaxValue) MinElevationM = minE;
        if (MaxElevationM <= 0 && maxE > double.MinValue) MaxElevationM = maxE;
    }

    public string FormattedDuration
    {
        get
        {
            var d = Duration;
            if (d == TimeSpan.Zero) return "--";
            if (d.TotalHours >= 1)
                return $"{(int)d.TotalHours}h {d.Minutes:D2}m";
            return $"{d.Minutes}m {d.Seconds:D2}s";
        }
    }

    public string FormattedDate => StartTimeUtc?.ToLocalTime().ToString("MMM d, yyyy") 
        ?? LastModifiedUtc.ToLocalTime().ToString("MMM d, yyyy");

    public bool HasValidBounds =>
        MaxLat >= MinLat && MaxLon >= MinLon &&
        MinLat >= -90 && MaxLat <= 90 &&
        MinLon >= -180 && MaxLon <= 180 &&
        (Math.Abs(MinLat) + Math.Abs(MaxLat) + Math.Abs(MinLon) + Math.Abs(MaxLon) > 0.00001);
}

/// <summary>
/// Metadata for an individual waypoint extracted from a GPX file.
/// </summary>
public class WaypointInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? ElevationM { get; set; }
    public DateTime? TimeUtc { get; set; }
}

/// <summary>
/// Metadata and coordinates for a route extracted from a GPX file.
/// </summary>
public class RouteInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double TotalDistanceKm { get; set; }
    public List<double[]> Coordinates { get; set; } = new();
}

/// <summary>
/// Persistent index cache of all scanned track files and bounding boxes.
/// </summary>
public class TrackIndexCache
{
    public int CacheVersion { get; set; } = 2;
    public string GpxDirectory { get; set; } = string.Empty;
    public DateTime LastScanTimeUtc { get; set; }
    public List<TrackFileInfo> Entries { get; set; } = new();
}

