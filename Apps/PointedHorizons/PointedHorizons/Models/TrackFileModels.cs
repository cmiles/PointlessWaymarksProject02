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
    /// Decimated [lon, lat] coordinates for fast GeoJSON map streaming.
    /// </summary>
    public List<double[]> SimplifiedCoordinates { get; set; } = new();

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

    public bool HasValidBounds => MaxLat > MinLat && MaxLon > MinLon && Math.Abs(MaxLat - MinLat) > 0.00001;
}

/// <summary>
/// Persistent index cache of all scanned track files and bounding boxes.
/// </summary>
public class TrackIndexCache
{
    public int CacheVersion { get; set; } = 1;
    public string GpxDirectory { get; set; } = string.Empty;
    public DateTime LastScanTimeUtc { get; set; }
    public List<TrackFileInfo> Entries { get; set; } = new();
}

