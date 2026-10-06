using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using NetTopologySuite.IO;
using PwTrackTrimmer.Models;

namespace PwTrackTrimmer.Services
{
    public class GpxTrackService : ITrackFileService
    {
        public Task<TrackDocument> LoadAsync(Stream stream, string fileName)
        {
            var log = ActivityLogService.Instance;
            log.Info($"Opening GPX file stream: '{Path.GetFileName(fileName)}'...", "GPX I/O");

            var document = new TrackDocument
            {
                Name = Path.GetFileNameWithoutExtension(fileName),
                SourceFilePath = fileName,
                FileFormat = "GPX"
            };

            log.Info("Initializing NetTopologySuite GpxReader and XmlReader...", "GPX I/O");
            using var xmlReader = XmlReader.Create(stream);
            var gpxFile = GpxFile.ReadFrom(xmlReader, new GpxReaderSettings());

            if (!string.IsNullOrEmpty(gpxFile.Metadata?.Name))
            {
                document.Name = gpxFile.Metadata.Name;
                log.Info($"Found GPX metadata track name: '{document.Name}'", "GPX I/O");
            }

            // Batch into local list
            var rawPoints = new List<TrackPoint>();
            int index = 0;

            if (gpxFile.Tracks != null && gpxFile.Tracks.Count > 0)
            {
                log.Info($"Discovered {gpxFile.Tracks.Count} GPX track(s) in file. Parsing segments...", "GPX I/O");

                foreach (var track in gpxFile.Tracks)
                {
                    if (!string.IsNullOrEmpty(track.Name) && string.IsNullOrEmpty(document.Name))
                    {
                        document.Name = track.Name;
                    }

                    if (track.Segments == null) continue;

                    foreach (var segment in track.Segments)
                    {
                        if (segment.Waypoints == null) continue;
                        log.Info($"Parsing track segment with {segment.Waypoints.Count} waypoints...", "GPX I/O");

                        foreach (var wpt in segment.Waypoints)
                        {
                            var pt = new TrackPoint
                            {
                                Index = index++,
                                Latitude = wpt.Latitude.Value,
                                Longitude = wpt.Longitude.Value,
                                Elevation = wpt.ElevationInMeters,
                                Time = wpt.TimestampUtc,
                                RawData = wpt
                            };
                            rawPoints.Add(pt);

                            if (index % 500 == 0)
                            {
                                log.Info($"Parsed {index} GPX trackpoints...", "GPX I/O");
                            }
                        }
                    }
                }
            }

            // Batch set points
            document.SetPoints(rawPoints);
            log.Success($"GPX import complete. Extracted {document.Points.Count} trackpoints with elevation and timestamps.", "GPX I/O");

            TrackStatisticsCalculator.RecalculateTrack(document.Points, document.Statistics);
            return Task.FromResult(document);
        }

        public Task SaveAsync(TrackDocument document, Stream outputStream)
        {
            var log = ActivityLogService.Instance;
            log.Info($"Initiating NetTopologySuite GPX export for '{document.Name}'...", "GPX I/O");

            bool isGpxSource = string.Equals(document.FileFormat, "GPX", StringComparison.OrdinalIgnoreCase);
            var waypoints = new List<GpxWaypoint>();
            int count = 0;
            int preservedRaw = 0;
            foreach (var pt in document.Points)
            {
                if (pt.IsTrimmed) continue;

                GpxWaypoint wpt;
                if (isGpxSource && pt.RawData is GpxWaypoint rawWpt)
                {
                    wpt = rawWpt
                        .WithLongitude(new GpxLongitude(pt.Longitude))
                        .WithLatitude(new GpxLatitude(pt.Latitude));

                    if (pt.Elevation.HasValue)
                    {
                        wpt = wpt.WithElevationInMeters(pt.Elevation.Value);
                    }
                    else
                    {
                        wpt = wpt.WithElevationInMeters(null);
                    }

                    if (pt.Time.HasValue)
                    {
                        wpt = wpt.WithTimestampUtc(pt.Time.Value);
                    }
                    else
                    {
                        wpt = wpt.WithTimestampUtc(null);
                    }
                    preservedRaw++;
                }
                else
                {
                    wpt = new GpxWaypoint(new GpxLongitude(pt.Longitude), new GpxLatitude(pt.Latitude));
                    if (pt.Elevation.HasValue)
                    {
                        wpt = wpt.WithElevationInMeters(pt.Elevation.Value);
                    }
                    if (pt.Time.HasValue)
                    {
                        wpt = wpt.WithTimestampUtc(pt.Time.Value);
                    }
                }
                waypoints.Add(wpt);
                count++;

                if (count % 500 == 0)
                {
                    log.Info($"Prepared {count} GPX waypoints for serialization ({preservedRaw} preserved raw)...", "GPX I/O");
                }
            }

            log.Info($"Constructing GPX track segment with {waypoints.Count} active waypoints...", "GPX I/O");
            var segment = new GpxTrackSegment().WithWaypoints(waypoints);
            var track = new GpxTrack()
                .WithName(document.Name ?? "Track")
                .WithDescription(document.Description ?? string.Empty)
                .WithSegments(System.Collections.Immutable.ImmutableArray.Create(segment));

            var gpxFile = new GpxFile();
            gpxFile.Metadata = new GpxMetadata("PwTrackTrimmer")
                .WithName(document.Name ?? "Track")
                .WithDescription(document.Description ?? string.Empty);

            gpxFile.Tracks.Add(track);

            log.Info("Writing XML with UTF-8 encoding...", "GPX I/O");
            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = System.Text.Encoding.UTF8
            };

            using var xmlWriter = XmlWriter.Create(outputStream, settings);
            gpxFile.WriteTo(xmlWriter, new GpxWriterSettings());
            xmlWriter.Flush();

            log.Success($"GPX export complete. Wrote {waypoints.Count} trackpoints to output stream.", "GPX I/O");
            return Task.CompletedTask;
        }
    }
}
