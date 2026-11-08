using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
                var waypoints = new List<WaypointInfo>();
                var routes = new List<RouteInfo>();
                string trackName = Path.GetFileNameWithoutExtension(filePath);

                using (var stream = File.OpenRead(filePath))
                {
                    switch (ext)
                    {
                        case ".gpx":
                            ParseGpx(stream, ref trackName, rawPoints, waypoints, routes);
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

                if (rawPoints.Count == 0 && waypoints.Count == 0 && routes.Count == 0) return null;

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

                // Expand bounds and stats with routes
                foreach (var rte in routes)
                {
                    if (rawPoints.Count == 0)
                    {
                        totalDistKm += rte.TotalDistanceKm;
                    }
                    foreach (var c in rte.Coordinates)
                    {
                        double lon = c[0];
                        double lat = c[1];
                        if (lat < minLat) minLat = lat;
                        if (lat > maxLat) maxLat = lat;
                        if (lon < minLon) minLon = lon;
                        if (lon > maxLon) maxLon = lon;
                        if (c.Length > 2 && c[2] > 0)
                        {
                            if (c[2] < minEleM) minEleM = c[2];
                            if (c[2] > maxEleM) maxEleM = c[2];
                        }
                    }
                }

                // Expand bounds with waypoints
                foreach (var wpt in waypoints)
                {
                    if (wpt.Latitude < minLat) minLat = wpt.Latitude;
                    if (wpt.Latitude > maxLat) maxLat = wpt.Latitude;
                    if (wpt.Longitude < minLon) minLon = wpt.Longitude;
                    if (wpt.Longitude > maxLon) maxLon = wpt.Longitude;
                    if (wpt.ElevationM.HasValue)
                    {
                        if (wpt.ElevationM.Value < minEleM) minEleM = wpt.ElevationM.Value;
                        if (wpt.ElevationM.Value > maxEleM) maxEleM = wpt.ElevationM.Value;
                    }
                    if (wpt.TimeUtc.HasValue)
                    {
                        if (!startTime.HasValue || wpt.TimeUtc.Value < startTime.Value)
                            startTime = wpt.TimeUtc.Value;
                        if (!endTime.HasValue || wpt.TimeUtc.Value > endTime.Value)
                            endTime = wpt.TimeUtc.Value;
                    }
                }

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

                if (minLat == double.MaxValue)
                {
                    minLat = 0; maxLat = 0; minLon = 0; maxLon = 0;
                }
                if (minEleM == double.MaxValue) { minEleM = 0; maxEleM = 0; }

                // Decimate coordinates for fast MapLibre streaming (max 500 points per track)
                var simplified = rawPoints.Count > 0 ? DecimatePoints(rawPoints, maxPoints: 500) : new List<double[]>();

                int pointCount = rawPoints.Count;
                if (pointCount == 0)
                {
                    pointCount = routes.Sum(r => r.Coordinates.Count) + waypoints.Count;
                }

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
                    PointCount = pointCount,
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
                    ElevationProfile = elevationProfile,
                    Waypoints = waypoints,
                    Routes = routes
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TrackParserService] Parse error on '{filePath}': {ex.Message}");
                return null;
            }
        });
    }

    private static void ParseGpx(
        Stream stream,
        ref string trackName,
        List<(double Lat, double Lon, double? Ele, DateTime? Time)> points,
        List<WaypointInfo> waypoints,
        List<RouteInfo> routes)
    {
        using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings { IgnoreWhitespace = true });
        var gpxFile = GpxFile.ReadFrom(xmlReader, new GpxReaderSettings());

        if (!string.IsNullOrWhiteSpace(gpxFile.Metadata?.Name))
        {
            trackName = gpxFile.Metadata.Name.Trim();
        }

        // 1. Tracks (<trk>)
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

        // 2. Routes (<rte>)
        if (gpxFile.Routes != null)
        {
            int routeIdx = 1;
            foreach (var rte in gpxFile.Routes)
            {
                if (rte.Waypoints == null || rte.Waypoints.Count == 0) continue;

                string rName = !string.IsNullOrWhiteSpace(rte.Name) ? rte.Name.Trim() : $"Route {routeIdx}";
                string rDesc = !string.IsNullOrWhiteSpace(rte.Description) ? rte.Description.Trim() : (rte.Comment?.Trim() ?? string.Empty);
                string rId = $"rte_{routeIdx}_{Guid.NewGuid():N}";

                var routeCoords = new List<double[]>();
                double rDistKm = 0.0;
                double? prevLat = null;
                double? prevLon = null;

                foreach (var wpt in rte.Waypoints)
                {
                    double lat = wpt.Latitude.Value;
                    double lon = wpt.Longitude.Value;
                    double ele = wpt.ElevationInMeters ?? 0.0;

                    if (prevLat.HasValue && prevLon.HasValue)
                    {
                        rDistKm += TrackStorageService.DistanceKm(prevLat.Value, prevLon.Value, lat, lon);
                    }
                    prevLat = lat;
                    prevLon = lon;

                    routeCoords.Add(new[] {
                        Math.Round(lon, 6),
                        Math.Round(lat, 6),
                        Math.Round(ele, 1)
                    });
                }

                routes.Add(new RouteInfo
                {
                    Id = rId,
                    Name = rName,
                    Description = rDesc,
                    TotalDistanceKm = rDistKm,
                    Coordinates = routeCoords
                });

                routeIdx++;
            }
        }

        // 3. Top-level Waypoints (<wpt>)
        if (gpxFile.Waypoints != null)
        {
            int wptIdx = 1;
            foreach (var wpt in gpxFile.Waypoints)
            {
                string wName = !string.IsNullOrWhiteSpace(wpt.Name) ? wpt.Name.Trim() : $"Waypoint {wptIdx}";
                string wDesc = !string.IsNullOrWhiteSpace(wpt.Description) ? wpt.Description.Trim() : string.Empty;
                string wCmt = !string.IsNullOrWhiteSpace(wpt.Comment) ? wpt.Comment.Trim() : string.Empty;
                string wSym = !string.IsNullOrWhiteSpace(wpt.SymbolText) ? wpt.SymbolText.Trim() : string.Empty;

                waypoints.Add(new WaypointInfo
                {
                    Id = $"wpt_{wptIdx}_{Guid.NewGuid():N}",
                    Name = wName,
                    Description = wDesc,
                    Comment = wCmt,
                    Symbol = wSym,
                    Latitude = wpt.Latitude.Value,
                    Longitude = wpt.Longitude.Value,
                    ElevationM = wpt.ElevationInMeters,
                    TimeUtc = wpt.TimestampUtc
                });

                wptIdx++;
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

    /// <summary>
    /// Appends a new waypoint to the default application waypoints GPX file (waypoints.gpx) in the specified folder.
    /// If the file does not exist, a new standard GPX 1.1 file is created.
    /// If it already exists, the waypoint is added and all existing waypoints are preserved.
    /// </summary>
    public static async Task<string> AppendWaypointToGpxAsync(string waypointName, double lat, double lon, double? ele, string destinationFolder, string? description = null)
    {
        return await Task.Run(() =>
        {
            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            string fullPath = Path.Combine(destinationFolder, "waypoints.gpx");
            DateTime now = DateTime.UtcNow;

            XNamespace ns = "http://www.topografix.com/GPX/1/1";

            XElement newWpt = new XElement(ns + "wpt",
                new XAttribute("lat", lat.ToString("F6", CultureInfo.InvariantCulture)),
                new XAttribute("lon", lon.ToString("F6", CultureInfo.InvariantCulture))
            );

            if (ele.HasValue)
            {
                newWpt.Add(new XElement(ns + "ele", ele.Value.ToString("F1", CultureInfo.InvariantCulture)));
            }

            newWpt.Add(new XElement(ns + "time", now.ToString("yyyy-MM-ddTHH:mm:ssZ")));
            newWpt.Add(new XElement(ns + "name", waypointName));
            if (!string.IsNullOrWhiteSpace(description))
            {
                newWpt.Add(new XElement(ns + "desc", description.Trim()));
            }
            newWpt.Add(new XElement(ns + "sym", "Waypoint"));

            if (File.Exists(fullPath))
            {
                try
                {
                    var doc = XDocument.Load(fullPath);
                    var root = doc.Root;
                    if (root != null)
                    {
                        XNamespace rootNs = root.Name.Namespace;
                        if (rootNs != ns && !string.IsNullOrEmpty(rootNs.NamespaceName))
                        {
                            ns = rootNs;
                            newWpt.Name = ns + "wpt";
                            foreach (var el in newWpt.Elements())
                            {
                                el.Name = ns + el.Name.LocalName;
                            }
                        }

                        // Insert before first <trk> or <rte>, or append to root
                        var firstTrk = root.Element(ns + "trk") ?? root.Element(ns + "rte");
                        if (firstTrk != null)
                        {
                            firstTrk.AddBeforeSelf(newWpt);
                        }
                        else
                        {
                            root.Add(newWpt);
                        }

                        doc.Save(fullPath);
                        return fullPath;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TrackParserService] Append to existing waypoints.gpx failed: {ex.Message}");
                }
            }

            // Create new GPX file
            var newDoc = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement(ns + "gpx",
                    new XAttribute("version", "1.1"),
                    new XAttribute("creator", "PointedHorizons"),
                    new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                    new XAttribute(XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance") + "schemaLocation", "http://www.topografix.com/GPX/1/1 http://www.topografix.com/GPX/1/1/gpx.xsd"),
                    new XElement(ns + "metadata",
                        new XElement(ns + "name", "PointedHorizons Waypoints"),
                        new XElement(ns + "time", now.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                    ),
                    newWpt
                )
            );

            newDoc.Save(fullPath);
            return fullPath;
        });
    }

    /// <summary>
    /// Gets the count of existing waypoints in the default waypoints.gpx file.
    /// </summary>
    public static async Task<int> GetExistingWaypointCountAsync(string destinationFolder)
    {
        return await Task.Run(() =>
        {
            try
            {
                string fullPath = Path.Combine(destinationFolder, "waypoints.gpx");
                if (!File.Exists(fullPath)) return 0;

                var doc = XDocument.Load(fullPath);
                return doc.Descendants().Count(e => e.Name.LocalName == "wpt");
            }
            catch
            {
                return 0;
            }
        });
    }

    /// <summary>
    /// Updates an existing waypoint in the specified GPX file on disk.
    /// Finds the waypoint by matching original coordinates (or index) and updates its
    /// coordinates, name, and description while preserving existing metadata.
    /// </summary>
    public static async Task<bool> UpdateWaypointInGpxAsync(
        string filePath,
        string waypointId,
        double origLat,
        double origLon,
        string newName,
        string newDesc,
        double newLat,
        double newLon,
        double? newEle = null)
    {
        return await Task.Run(() =>
        {
            if (!File.Exists(filePath)) return false;

            try
            {
                var doc = XDocument.Load(filePath);
                var root = doc.Root;
                if (root == null) return false;

                XNamespace ns = root.Name.Namespace;
                var wptList = doc.Descendants().Where(e => e.Name.LocalName == "wpt").ToList();
                if (wptList.Count == 0) return false;

                // 1. Match by original coordinates if available
                XElement? target = wptList.FirstOrDefault(w =>
                {
                    var latAttr = w.Attribute("lat");
                    var lonAttr = w.Attribute("lon");
                    if (latAttr == null || lonAttr == null) return false;
                    if (double.TryParse(latAttr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double wLat) &&
                        double.TryParse(lonAttr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double wLon))
                    {
                        return Math.Abs(wLat - origLat) < 0.0001 && Math.Abs(wLon - origLon) < 0.0001;
                    }
                    return false;
                });

                // 2. If not matched by coords, try matching by index from waypointId
                if (target == null && waypointId.StartsWith("wpt_"))
                {
                    var parts = waypointId.Split('_');
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int idx1Based))
                    {
                        int idx0 = idx1Based - 1;
                        if (idx0 >= 0 && idx0 < wptList.Count)
                        {
                            target = wptList[idx0];
                        }
                    }
                }

                // 3. Fallback: match by name
                if (target == null)
                {
                    target = wptList.FirstOrDefault(w =>
                    {
                        var n = w.Elements().FirstOrDefault(e => e.Name.LocalName == "name");
                        return n != null && n.Value.Trim().Equals(newName.Trim(), StringComparison.OrdinalIgnoreCase);
                    }) ?? wptList[0];
                }

                // Update lat and lon attributes
                target.SetAttributeValue("lat", newLat.ToString("F6", CultureInfo.InvariantCulture));
                target.SetAttributeValue("lon", newLon.ToString("F6", CultureInfo.InvariantCulture));

                // Update elevation if provided
                if (newEle.HasValue)
                {
                    var eleElem = target.Elements().FirstOrDefault(e => e.Name.LocalName == "ele");
                    if (eleElem != null)
                    {
                        eleElem.Value = newEle.Value.ToString("F1", CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        target.Add(new XElement(ns + "ele", newEle.Value.ToString("F1", CultureInfo.InvariantCulture)));
                    }
                }

                // Update name element
                var nameElem = target.Elements().FirstOrDefault(e => e.Name.LocalName == "name");
                if (nameElem != null)
                {
                    nameElem.Value = newName;
                }
                else
                {
                    target.Add(new XElement(ns + "name", newName));
                }

                // Update description element
                var descElem = target.Elements().FirstOrDefault(e => e.Name.LocalName == "desc");
                if (!string.IsNullOrWhiteSpace(newDesc))
                {
                    if (descElem != null)
                    {
                        descElem.Value = newDesc;
                    }
                    else
                    {
                        target.Add(new XElement(ns + "desc", newDesc));
                    }
                }
                else if (descElem != null)
                {
                    descElem.Remove();
                }

                doc.Save(filePath);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TrackParserService] UpdateWaypointInGpxAsync error: {ex.Message}");
                return false;
            }
        });
    }

    /// <summary>
    /// Attempts to parse common latitude/longitude coordinate string formats.
    /// Accepts comma, space, dash, slash, or semicolon separators.
    /// Supports standard decimal (32.2226, -110.9747), signed or unsigned with cardinal directions
    /// (32.2226 N, 110.9747 W), dash-separated (32.2226 - 110.9747 or 32.2226 - -110.9747),
    /// and DMS / DDM representations.
    /// </summary>
    public static bool TryParseCoordinates(string? input, out double lat, out double lon)
    {
        lat = 0;
        lon = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;

        string s = input.Trim();

        // 1. Check for cardinal directions N/S/E/W
        bool hasCardinal = Regex.IsMatch(s, @"[NSEWnsew]");
        if (hasCardinal)
        {
            var match = Regex.Match(s, @"(?i)^\s*([NS])?\s*(\d+(?:\.\d+)?)\s*(?:[°\s]|deg)?\s*(?:(\d+(?:\.\d+)?)\s*['′\s]?)?\s*(?:(\d+(?:\.\d+)?)\s*[""″\s]?)?\s*([NS])?[\s,;/–—\-]+([EW])?\s*(\d+(?:\.\d+)?)\s*(?:[°\s]|deg)?\s*(?:(\d+(?:\.\d+)?)\s*['′\s]?)?\s*(?:(\d+(?:\.\d+)?)\s*[""″\s]?)?\s*([EW])?\s*$");
            if (match.Success)
            {
                string ns1 = match.Groups[1].Value.ToUpperInvariant();
                double latDeg = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                double latMin = match.Groups[3].Success && !string.IsNullOrWhiteSpace(match.Groups[3].Value) ? double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) : 0;
                double latSec = match.Groups[4].Success && !string.IsNullOrWhiteSpace(match.Groups[4].Value) ? double.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture) : 0;
                string ns2 = match.Groups[5].Value.ToUpperInvariant();

                string ew1 = match.Groups[6].Value.ToUpperInvariant();
                double lonDeg = double.Parse(match.Groups[7].Value, CultureInfo.InvariantCulture);
                double lonMin = match.Groups[8].Success && !string.IsNullOrWhiteSpace(match.Groups[8].Value) ? double.Parse(match.Groups[8].Value, CultureInfo.InvariantCulture) : 0;
                double lonSec = match.Groups[9].Success && !string.IsNullOrWhiteSpace(match.Groups[9].Value) ? double.Parse(match.Groups[9].Value, CultureInfo.InvariantCulture) : 0;
                string ew2 = match.Groups[10].Value.ToUpperInvariant();

                lat = latDeg + (latMin / 60.0) + (latSec / 3600.0);
                string ns = !string.IsNullOrEmpty(ns1) ? ns1 : ns2;
                if (ns == "S") lat = -lat;

                lon = lonDeg + (lonMin / 60.0) + (lonSec / 3600.0);
                string ew = !string.IsNullOrEmpty(ew1) ? ew1 : ew2;
                if (ew == "W") lon = -lon;

                if (lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180)
                {
                    return true;
                }
            }
        }

        // 2. Decimal degrees parsing
        // Replace degree symbols and brackets
        string cleaned = Regex.Replace(s, @"[°\(\)\[\]]", " ").Trim();

        // Extract numbers with optional sign
        var matches = Regex.Matches(cleaned, @"[+-]?\d+(?:\.\d+)?");
        if (matches.Count == 2)
        {
            if (double.TryParse(matches[0].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedLat) &&
                double.TryParse(matches[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedLon))
            {
                if (parsedLat >= -90 && parsedLat <= 90 && parsedLon >= -180 && parsedLon <= 180)
                {
                    lat = parsedLat;
                    lon = parsedLon;
                    return true;
                }
            }
        }
        else if (matches.Count == 4)
        {
            // Degrees & decimal minutes: D1 M1 D2 M2
            if (double.TryParse(matches[0].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d1) &&
                double.TryParse(matches[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double m1) &&
                double.TryParse(matches[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d2) &&
                double.TryParse(matches[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double m2))
            {
                lat = d1 >= 0 ? d1 + (m1 / 60.0) : d1 - (m1 / 60.0);
                lon = d2 >= 0 ? d2 + (m2 / 60.0) : d2 - (m2 / 60.0);
                if (lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180)
                {
                    return true;
                }
            }
        }
        else if (matches.Count == 6)
        {
            // DMS: D1 M1 S1 D2 M2 S2
            if (double.TryParse(matches[0].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d1) &&
                double.TryParse(matches[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double m1) &&
                double.TryParse(matches[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double s1) &&
                double.TryParse(matches[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d2) &&
                double.TryParse(matches[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double m2) &&
                double.TryParse(matches[5].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double s2))
            {
                lat = d1 >= 0 ? d1 + (m1 / 60.0) + (s1 / 3600.0) : d1 - (m1 / 60.0) - (s1 / 3600.0);
                lon = d2 >= 0 ? d2 + (m2 / 60.0) + (s2 / 3600.0) : d2 - (m2 / 60.0) - (s2 / 3600.0);
                if (lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
