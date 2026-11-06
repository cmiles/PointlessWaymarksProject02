using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Dynastream.Fit;
using NetTopologySuite.IO;
using PointedHorizons.Models;
using DateTime = System.DateTime;
using File = System.IO.File;

namespace PointedHorizons.Services;

/// <summary>
/// High-performance parsing and export for GPX, TCX, and FIT files.
/// Extracts bounding boxes, summary statistics, and simplified coordinate lines for map rendering.
/// </summary>
public static class TrackParserService
{
    private const double SemicirclesToDegrees = 180.0 / 2147483648.0;

    private static readonly string[] TrackColors = new[]
    {
        "#F97316", // Vibrant Orange
        "#0284C7", // Sky Blue
        "#10B981", // Emerald Green
        "#8B5CF6", // Purple
        "#EF4444", // Red
        "#F59E0B", // Amber
        "#EC4899", // Pink
        "#06B6D4"  // Cyan
    };

    public static async Task<TrackFileInfo?> ParseTrackFileAsync(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        return await Task.Run(() =>
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                string ext = fileInfo.Extension.ToLowerInvariant();
                string id = ComputeFileId(filePath);
                string color = TrackColors[Math.Abs(id.GetHashCode()) % TrackColors.Length];

                var rawPoints = new List<(double Lat, double Lon, double? Ele, DateTime? Time)>();
                string trackName = Path.GetFileNameWithoutExtension(filePath);

                using (var stream = File.OpenRead(filePath))
                {
                    switch (ext)
                    {
                        case ".gpx":
                            ParseGpx(stream, ref trackName, rawPoints);
                            break;
                        case ".tcx":
                            ParseTcx(stream, ref trackName, rawPoints);
                            break;
                        case ".fit":
                            ParseFit(stream, ref trackName, rawPoints);
                            break;
                        default:
                            return null;
                    }
                }

                if (rawPoints.Count == 0) return null;

                double minLat = double.MaxValue, maxLat = double.MinValue;
                double minLon = double.MaxValue, maxLon = double.MinValue;
                double totalDistKm = 0.0;
                double totalClimbM = 0.0;
                double totalDescentM = 0.0;
                double minEleM = double.MaxValue;
                double maxEleM = double.MinValue;
                bool hasEle = false;
                DateTime? startTime = null;
                DateTime? endTime = null;

                var cumulativeDistances = new double[rawPoints.Count];
                double runDist = 0.0;

                for (int i = 0; i < rawPoints.Count; i++)
                {
                    var pt = rawPoints[i];
                    if (pt.Lat < minLat) minLat = pt.Lat;
                    if (pt.Lat > maxLat) maxLat = pt.Lat;
                    if (pt.Lon < minLon) minLon = pt.Lon;
                    if (pt.Lon > maxLon) maxLon = pt.Lon;

                    if (pt.Ele.HasValue)
                    {
                        hasEle = true;
                        double e = pt.Ele.Value;
                        if (e < minEleM) minEleM = e;
                        if (e > maxEleM) maxEleM = e;
                    }

                    if (pt.Time.HasValue)
                    {
                        if (!startTime.HasValue || pt.Time.Value < startTime.Value)
                            startTime = pt.Time.Value;
                        if (!endTime.HasValue || pt.Time.Value > endTime.Value)
                            endTime = pt.Time.Value;
                    }

                    if (i > 0)
                    {
                        var prev = rawPoints[i - 1];
                        double d = TrackStorageService.DistanceKm(prev.Lat, prev.Lon, pt.Lat, pt.Lon);
                        runDist += d;

                        if (pt.Ele.HasValue && prev.Ele.HasValue)
                        {
                            double dEle = pt.Ele.Value - prev.Ele.Value;
                            if (dEle > 0.6) totalClimbM += dEle;
                            else if (dEle < -0.6) totalDescentM += Math.Abs(dEle);
                        }
                    }
                    cumulativeDistances[i] = runDist;
                }
                totalDistKm = runDist;

                // Build downsampled elevation profile points [distKm, eleM, lat, lon]
                var elevationProfile = new List<double[]>();
                int profileSampleCount = Math.Min(rawPoints.Count, 250);
                if (profileSampleCount > 1)
                {
                    double pStep = (double)(rawPoints.Count - 1) / (profileSampleCount - 1);
                    for (int k = 0; k < profileSampleCount; k++)
                    {
                        int pIdx = (int)Math.Round(k * pStep);
                        if (pIdx >= rawPoints.Count) pIdx = rawPoints.Count - 1;
                        var p = rawPoints[pIdx];
                        double eleVal = p.Ele ?? 0.0;
                        if (!p.Ele.HasValue && !hasEle)
                        {
                            double? demE = DemService.GetElevation(p.Lat, p.Lon);
                            if (demE.HasValue) eleVal = demE.Value;
                        }
                        elevationProfile.Add(new[] {
                            Math.Round(cumulativeDistances[pIdx], 2),
                            Math.Round(eleVal, 1),
                            Math.Round(p.Lat, 6),
                            Math.Round(p.Lon, 6)
                        });
                    }
                }

                // If no file elevation existed but DEM sampled profiles, derive min/max/gain/loss
                if (!hasEle && elevationProfile.Count > 1)
                {
                    double dMin = double.MaxValue, dMax = double.MinValue;
                    for (int k = 0; k < elevationProfile.Count; k++)
                    {
                        double e = elevationProfile[k][1];
                        if (e > 0)
                        {
                            if (e < dMin) dMin = e;
                            if (e > dMax) dMax = e;
                            if (k > 0)
                            {
                                double diff = e - elevationProfile[k - 1][1];
                                if (diff > 0.6) totalClimbM += diff;
                                else if (diff < -0.6) totalDescentM += Math.Abs(diff);
                            }
                        }
                    }
                    if (dMin < double.MaxValue) minEleM = dMin;
                    if (dMax > double.MinValue) maxEleM = dMax;
                }

                if (minEleM == double.MaxValue) { minEleM = 0; maxEleM = 0; }

                // Decimate coordinates for fast MapLibre streaming (max 500 points per track)
                var simplified = DecimatePoints(rawPoints, maxPoints: 500);

                return new TrackFileInfo
                {
                    Id = id,
                    FilePath = filePath,
                    FileName = fileInfo.Name,
                    FileFormat = ext.TrimStart('.').ToUpperInvariant(),
                    FileSizeBytes = fileInfo.Length,
                    LastModifiedUtc = fileInfo.LastWriteTimeUtc,
                    FileHash = ComputeQuickFileHash(filePath, fileInfo.Length, fileInfo.LastWriteTimeUtc),
                    TrackName = trackName,
                    PointCount = rawPoints.Count,
                    TotalDistanceKm = totalDistKm,
                    TotalClimbM = totalClimbM,
                    TotalDescentM = totalDescentM,
                    MinElevationM = minEleM,
                    MaxElevationM = maxEleM,
                    MinLat = minLat,
                    MaxLat = maxLat,
                    MinLon = minLon,
                    MaxLon = maxLon,
                    StartTimeUtc = startTime,
                    EndTimeUtc = endTime,
                    DisplayColor = color,
                    IsVisibleOnMap = true,
                    SimplifiedCoordinates = simplified,
                    ElevationProfile = elevationProfile
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TrackParserService] Parse error on '{filePath}': {ex.Message}");
                return null;
            }
        });
    }

    private static void ParseGpx(Stream stream, ref string trackName, List<(double Lat, double Lon, double? Ele, DateTime? Time)> points)
    {
        using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings { IgnoreWhitespace = true });
        var gpxFile = GpxFile.ReadFrom(xmlReader, new GpxReaderSettings());

        if (!string.IsNullOrWhiteSpace(gpxFile.Metadata?.Name))
        {
            trackName = gpxFile.Metadata.Name.Trim();
        }

        if (gpxFile.Tracks != null)
        {
            foreach (var trk in gpxFile.Tracks)
            {
                if (!string.IsNullOrWhiteSpace(trk.Name) && string.Equals(trackName, Path.GetFileNameWithoutExtension(trackName)))
                {
                    trackName = trk.Name.Trim();
                }

                if (trk.Segments == null) continue;
                foreach (var seg in trk.Segments)
                {
                    if (seg.Waypoints == null) continue;
                    foreach (var wpt in seg.Waypoints)
                    {
                        points.Add((
                            wpt.Latitude.Value,
                            wpt.Longitude.Value,
                            wpt.ElevationInMeters,
                            wpt.TimestampUtc
                        ));
                    }
                }
            }
        }

        // Also check routes if no tracks
        if (points.Count == 0 && gpxFile.Routes != null)
        {
            foreach (var rte in gpxFile.Routes)
            {
                if (rte.Waypoints == null) continue;
                foreach (var wpt in rte.Waypoints)
                {
                    points.Add((
                        wpt.Latitude.Value,
                        wpt.Longitude.Value,
                        wpt.ElevationInMeters,
                        wpt.TimestampUtc
                    ));
                }
            }
        }
    }

    private static void ParseTcx(Stream stream, ref string trackName, List<(double Lat, double Lon, double? Ele, DateTime? Time)> points)
    {
        var xdoc = XDocument.Load(stream);
        var trackpoints = xdoc.Descendants().Where(e => e.Name.LocalName == "Trackpoint");

        foreach (var tp in trackpoints)
        {
            var pos = tp.Elements().FirstOrDefault(e => e.Name.LocalName == "Position");
            if (pos == null) continue;

            var latEl = pos.Elements().FirstOrDefault(e => e.Name.LocalName == "LatitudeDegrees");
            var lonEl = pos.Elements().FirstOrDefault(e => e.Name.LocalName == "LongitudeDegrees");
            if (latEl == null || lonEl == null) continue;

            if (!double.TryParse(latEl.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) ||
                !double.TryParse(lonEl.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
            {
                continue;
            }

            double? ele = null;
            var altEl = tp.Elements().FirstOrDefault(e => e.Name.LocalName == "AltitudeMeters");
            if (altEl != null && double.TryParse(altEl.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double a))
            {
                ele = a;
            }

            DateTime? time = null;
            var timeEl = tp.Elements().FirstOrDefault(e => e.Name.LocalName == "Time");
            if (timeEl != null && DateTime.TryParse(timeEl.Value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime t))
            {
                time = t;
            }

            points.Add((lat, lon, ele, time));
        }

        var activityIdEl = xdoc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Id");
        if (activityIdEl != null && !string.IsNullOrWhiteSpace(activityIdEl.Value))
        {
            trackName = $"Activity {activityIdEl.Value.Trim()}";
        }
    }

    private static void ParseFit(Stream stream, ref string trackName, List<(double Lat, double Lon, double? Ele, DateTime? Time)> points)
    {
        var decode = new Decode();
        var broadcaster = new MesgBroadcaster();
        decode.MesgEvent += broadcaster.OnMesg;

        string? discoveredTitle = null;

        broadcaster.FileIdMesgEvent += (sender, e) =>
        {
            var fileId = new FileIdMesg(e.mesg);
            var tc = fileId.GetTimeCreated();
            if (tc != null)
            {
                discoveredTitle = $"Activity {tc.GetDateTime():yyyy-MM-dd HH:mm}";
            }
        };

        broadcaster.RecordMesgEvent += (sender, e) =>
        {
            var rec = new RecordMesg(new Mesg(e.mesg));
            int? latSemi = rec.GetPositionLat();
            int? lonSemi = rec.GetPositionLong();

            if (!latSemi.HasValue || !lonSemi.HasValue ||
                latSemi.Value == 0x7FFFFFFF || lonSemi.Value == 0x7FFFFFFF)
            {
                return;
            }

            double lat = latSemi.Value * SemicirclesToDegrees;
            double lon = lonSemi.Value * SemicirclesToDegrees;

            if (lat < -90 || lat > 90 || lon < -180 || lon > 180) return;

            DateTime? time = rec.GetTimestamp()?.GetDateTime();
            float? alt = rec.GetAltitude();

            points.Add((lat, lon, alt.HasValue ? (double)alt.Value : null, time));
        };

        decode.Read(stream);

        if (!string.IsNullOrEmpty(discoveredTitle))
        {
            trackName = discoveredTitle;
        }
    }

    private static List<double[]> DecimatePoints(List<(double Lat, double Lon, double? Ele, DateTime? Time)> raw, int maxPoints)
    {
        var result = new List<double[]>();
        if (raw.Count <= maxPoints)
        {
            foreach (var p in raw)
            {
                result.Add(new[] {
                    Math.Round(p.Lon, 6),
                    Math.Round(p.Lat, 6),
                    p.Ele.HasValue ? Math.Round(p.Ele.Value, 1) : 0.0
                });
            }
            return result;
        }

        // Decimate keeping first and last points
        double step = (double)(raw.Count - 1) / (maxPoints - 1);
        for (int i = 0; i < maxPoints; i++)
        {
            int idx = (int)Math.Round(i * step);
            if (idx >= raw.Count) idx = raw.Count - 1;
            var p = raw[idx];
            result.Add(new[] {
                Math.Round(p.Lon, 6),
                Math.Round(p.Lat, 6),
                p.Ele.HasValue ? Math.Round(p.Ele.Value, 1) : 0.0
            });
        }
        return result;
    }

    public static string ComputeFileId(string filePath)
    {
        using var md5 = MD5.Create();
        byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(filePath.ToLowerInvariant()));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant().Substring(0, 16);
    }

    public static string ComputeQuickFileHash(string filePath, long length, DateTime lastModified)
    {
        return $"{length}_{lastModified.Ticks}";
    }

    /// <summary>
    /// Exports a recorded track into standard GPX 1.1 format XML in the designated folder.
    /// </summary>
    /// <summary>
    /// Sanitizes a string for use as a cross-platform filename across Windows, Android FAT/exFAT, and Linux.
    /// Replaces colons, slashes, wildcards, and invalid filesystem characters.
    /// </summary>
    public static string SanitizeFilename(string? name, string fallback = "Track")
    {
        if (string.IsNullOrWhiteSpace(name)) return fallback;
        char[] invalidChars = new[] { ':', '*', '?', '"', '<', '>', '|', '/', '\\' };
        var sb = new StringBuilder();
        foreach (char c in name.Trim())
        {
            if (invalidChars.Contains(c) || char.IsControl(c) || Path.GetInvalidFileNameChars().Contains(c))
            {
                sb.Append('_');
            }
            else
            {
                sb.Append(c);
            }
        }
        string result = sb.ToString().Trim(' ', '.', '_');
        return string.IsNullOrWhiteSpace(result) ? fallback : result;
    }

    public static async Task<string> ExportRecordedTrackToGpxAsync(RecordedTrack track, string destinationFolder)
    {
        return await Task.Run(() =>
        {
            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            string cleanName = SanitizeFilename(track.Name, "Track");
            string fileName = $"{cleanName}_{track.StartTime:yyyyMMdd_HHmmss}.gpx";
            string fullPath = Path.Combine(destinationFolder, fileName);

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<gpx version=\"1.1\" creator=\"PointedHorizons\" xmlns=\"http://www.topografix.com/GPX/1/1\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"http://www.topografix.com/GPX/1/1 http://www.topografix.com/GPX/1/1/gpx.xsd\">");
            sb.AppendLine("  <metadata>");
            sb.AppendLine($"    <name>{System.Security.SecurityElement.Escape(track.Name)}</name>");
            sb.AppendLine($"    <time>{track.StartTime:yyyy-MM-ddTHH:mm:ssZ}</time>");
            sb.AppendLine("  </metadata>");
            sb.AppendLine("  <trk>");
            sb.AppendLine($"    <name>{System.Security.SecurityElement.Escape(track.Name)}</name>");
            sb.AppendLine("    <trkseg>");

            foreach (var pt in track.Points)
            {
                sb.Append($"      <trkpt lat=\"{pt.Lat.ToString("F6", CultureInfo.InvariantCulture)}\" lon=\"{pt.Lon.ToString("F6", CultureInfo.InvariantCulture)}\">");
                if (pt.Ele.HasValue)
                {
                    sb.Append($"<ele>{pt.Ele.Value.ToString("F1", CultureInfo.InvariantCulture)}</ele>");
                }
                sb.Append($"<time>{pt.Time:yyyy-MM-ddTHH:mm:ssZ}</time>");
                sb.AppendLine("</trkpt>");
            }

            sb.AppendLine("    </trkseg>");
            sb.AppendLine("  </trk>");
            sb.AppendLine("</gpx>");

            File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);
            return fullPath;
        });
    }

    /// <summary>
    /// Exports a drawn route of [lon, lat] coordinates into standard GPX 1.1 format XML in the designated folder.
    /// Embeds local DEM elevation for each point if available in memory.
    /// </summary>
    public static async Task<string> ExportDrawnRouteToGpxAsync(string routeName, List<double[]> coordinates, string destinationFolder)
    {
        return await Task.Run(() =>
        {
            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            string cleanName = SanitizeFilename(routeName, "Route");
            DateTime now = DateTime.UtcNow;
            string fileName = $"{cleanName}_{now:yyyyMMdd_HHmmss}.gpx";
            string fullPath = Path.Combine(destinationFolder, fileName);

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<gpx version=\"1.1\" creator=\"PointedHorizons\" xmlns=\"http://www.topografix.com/GPX/1/1\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"http://www.topografix.com/GPX/1/1 http://www.topografix.com/GPX/1/1/gpx.xsd\">");
            sb.AppendLine("  <metadata>");
            sb.AppendLine($"    <name>{System.Security.SecurityElement.Escape(routeName ?? cleanName)}</name>");
            sb.AppendLine($"    <time>{now:yyyy-MM-ddTHH:mm:ssZ}</time>");
            sb.AppendLine("  </metadata>");
            sb.AppendLine("  <trk>");
            sb.AppendLine($"    <name>{System.Security.SecurityElement.Escape(routeName ?? cleanName)}</name>");
            sb.AppendLine("    <trkseg>");

            foreach (var pt in coordinates)
            {
                if (pt == null || pt.Length < 2) continue;
                double lon = pt[0];
                double lat = pt[1];
                sb.Append($"      <trkpt lat=\"{lat.ToString("F6", CultureInfo.InvariantCulture)}\" lon=\"{lon.ToString("F6", CultureInfo.InvariantCulture)}\">");

                // Embed route elevation: prefer point coordinate elevation (from BRouter), fallback to DEM
                double? elev = (pt.Length > 2 && pt[2] > 0) ? pt[2] : DemService.GetElevation(lat, lon);
                if (elev.HasValue)
                {
                    sb.Append($"<ele>{elev.Value.ToString("F1", CultureInfo.InvariantCulture)}</ele>");
                }
                sb.AppendLine("</trkpt>");
            }

            sb.AppendLine("    </trkseg>");
            sb.AppendLine("  </trk>");
            sb.AppendLine("</gpx>");

            File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);
            return fullPath;
        });
    }
}
