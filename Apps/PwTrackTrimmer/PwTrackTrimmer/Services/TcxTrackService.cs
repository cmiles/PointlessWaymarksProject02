using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using PwTrackTrimmer.Models;

namespace PwTrackTrimmer.Services
{
    public class TcxTrackService : ITrackFileService
    {
        private static readonly XNamespace TcxNs = "http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2";
        private static readonly XNamespace ActivityExtNs = "http://www.garmin.com/xmlschemas/ActivityExtension/v2";

        public Task<TrackDocument> LoadAsync(Stream stream, string fileName)
        {
            var log = ActivityLogService.Instance;
            log.Info($"Opening TCX XML stream for '{Path.GetFileName(fileName)}'...", "TCX I/O");

            var document = new TrackDocument
            {
                Name = Path.GetFileNameWithoutExtension(fileName),
                SourceFilePath = fileName,
                FileFormat = "TCX"
            };

            log.Info("Loading and parsing TCX XDocument...", "TCX I/O");
            var xdoc = XDocument.Load(stream);
            var trackpoints = xdoc.Descendants().Where(e => e.Name.LocalName == "Trackpoint").ToList();
            log.Info($"Located {trackpoints.Count} <Trackpoint> nodes in XML tree...", "TCX I/O");

            var rawPoints = new List<TrackPoint>();
            int index = 0;

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

                double? elevation = null;
                var altEl = tp.Elements().FirstOrDefault(e => e.Name.LocalName == "AltitudeMeters");
                if (altEl != null && double.TryParse(altEl.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double alt))
                {
                    elevation = alt;
                }

                DateTime? time = null;
                var timeEl = tp.Elements().FirstOrDefault(e => e.Name.LocalName == "Time");
                if (timeEl != null && DateTime.TryParse(timeEl.Value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime parsedTime))
                {
                    time = parsedTime;
                }

                double distFromStart = 0;
                var distEl = tp.Elements().FirstOrDefault(e => e.Name.LocalName == "DistanceMeters");
                if (distEl != null && double.TryParse(distEl.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double dist))
                {
                    distFromStart = dist;
                }

                short? hr = null;
                var hrEl = tp.Elements().FirstOrDefault(e => e.Name.LocalName == "HeartRateBpm");
                var hrValEl = hrEl?.Elements().FirstOrDefault(e => e.Name.LocalName == "Value");
                if (hrValEl != null && short.TryParse(hrValEl.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out short parsedHr))
                {
                    hr = parsedHr;
                }

                short? cadence = null;
                var cadEl = tp.Elements().FirstOrDefault(e => e.Name.LocalName == "Cadence");
                if (cadEl != null && short.TryParse(cadEl.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out short parsedCad))
                {
                    cadence = parsedCad;
                }

                double? speed = null;
                var extEl = tp.Elements().FirstOrDefault(e => e.Name.LocalName == "Extensions");
                var tpxEl = extEl?.Elements().FirstOrDefault(e => e.Name.LocalName == "TPX");
                var speedEl = tpxEl?.Elements().FirstOrDefault(e => e.Name.LocalName == "Speed");
                if (speedEl != null && double.TryParse(speedEl.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedSpeed))
                {
                    speed = parsedSpeed;
                }

                var pt = new TrackPoint
                {
                    Index = index++,
                    Latitude = lat,
                    Longitude = lon,
                    Elevation = elevation,
                    Time = time,
                    DistanceFromStart = distFromStart,
                    HeartRate = hr,
                    Cadence = cadence,
                    Speed = speed,
                    RawData = new XElement(tp)
                };

                rawPoints.Add(pt);

                if (index % 500 == 0)
                {
                    log.Info($"Parsed {index} TCX points (HR: {hr?.ToString() ?? "--"}, Cad: {cadence?.ToString() ?? "--"})...", "TCX I/O");
                }
            }

            document.SetPoints(rawPoints);
            log.Success($"TCX import complete: {document.Points.Count} valid trackpoints loaded.", "TCX I/O");

            TrackStatisticsCalculator.RecalculateTrack(document.Points, document.Statistics);
            return Task.FromResult(document);
        }

        public Task SaveAsync(TrackDocument document, Stream outputStream)
        {
            var log = ActivityLogService.Instance;
            log.Info($"Initiating TCX XML generation for '{document.Name}'...", "TCX I/O");

            var firstTime = document.Points.FirstOrDefault()?.Time ?? DateTime.UtcNow;
            string isoStartTime = firstTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");

            bool isTcxSource = string.Equals(document.FileFormat, "TCX", StringComparison.OrdinalIgnoreCase);
            var trackElement = new XElement(TcxNs + "Track");
            int count = 0;
            int preservedRaw = 0;

            foreach (var pt in document.Points)
            {
                if (pt.IsTrimmed) continue;

                XElement tpEl;
                if (isTcxSource && pt.RawData is XElement rawTp)
                {
                    tpEl = new XElement(rawTp);
                    preservedRaw++;

                    // Update Position
                    var pos = tpEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Position");
                    if (pos == null)
                    {
                        pos = new XElement(TcxNs + "Position");
                        tpEl.Add(pos);
                    }
                    var latEl = pos.Elements().FirstOrDefault(e => e.Name.LocalName == "LatitudeDegrees");
                    if (latEl == null)
                    {
                        latEl = new XElement(TcxNs + "LatitudeDegrees");
                        pos.Add(latEl);
                    }
                    latEl.Value = pt.Latitude.ToString("F7", CultureInfo.InvariantCulture);

                    var lonEl = pos.Elements().FirstOrDefault(e => e.Name.LocalName == "LongitudeDegrees");
                    if (lonEl == null)
                    {
                        lonEl = new XElement(TcxNs + "LongitudeDegrees");
                        pos.Add(lonEl);
                    }
                    lonEl.Value = pt.Longitude.ToString("F7", CultureInfo.InvariantCulture);

                    // Update Altitude
                    var altEl = tpEl.Elements().FirstOrDefault(e => e.Name.LocalName == "AltitudeMeters");
                    if (pt.Elevation.HasValue)
                    {
                        if (altEl == null)
                        {
                            altEl = new XElement(TcxNs + "AltitudeMeters");
                            tpEl.Add(altEl);
                        }
                        altEl.Value = pt.Elevation.Value.ToString("F1", CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        altEl?.Remove();
                    }

                    // Update Time
                    var timeEl = tpEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Time");
                    if (pt.Time.HasValue)
                    {
                        if (timeEl == null)
                        {
                            timeEl = new XElement(TcxNs + "Time");
                            tpEl.Add(timeEl);
                        }
                        timeEl.Value = pt.Time.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
                    }
                    else
                    {
                        timeEl?.Remove();
                    }

                    // Update Distance
                    var distEl = tpEl.Elements().FirstOrDefault(e => e.Name.LocalName == "DistanceMeters");
                    if (distEl == null)
                    {
                        distEl = new XElement(TcxNs + "DistanceMeters");
                        tpEl.Add(distEl);
                    }
                    distEl.Value = pt.DistanceFromStart.ToString("F1", CultureInfo.InvariantCulture);

                    // Update HeartRate
                    if (pt.HeartRate.HasValue)
                    {
                        var hrEl = tpEl.Elements().FirstOrDefault(e => e.Name.LocalName == "HeartRateBpm");
                        if (hrEl == null)
                        {
                            hrEl = new XElement(TcxNs + "HeartRateBpm");
                            tpEl.Add(hrEl);
                        }
                        var valEl = hrEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Value");
                        if (valEl == null)
                        {
                            valEl = new XElement(TcxNs + "Value");
                            hrEl.Add(valEl);
                        }
                        valEl.Value = pt.HeartRate.Value.ToString();
                    }

                    // Update Cadence
                    if (pt.Cadence.HasValue)
                    {
                        var cadEl = tpEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Cadence");
                        if (cadEl == null)
                        {
                            cadEl = new XElement(TcxNs + "Cadence");
                            tpEl.Add(cadEl);
                        }
                        cadEl.Value = pt.Cadence.Value.ToString();
                    }

                    // Update Speed
                    if (pt.Speed.HasValue)
                    {
                        var extEl = tpEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Extensions");
                        if (extEl == null)
                        {
                            extEl = new XElement(TcxNs + "Extensions");
                            tpEl.Add(extEl);
                        }
                        var tpxEl = extEl.Elements().FirstOrDefault(e => e.Name.LocalName == "TPX");
                        if (tpxEl == null)
                        {
                            tpxEl = new XElement(ActivityExtNs + "TPX");
                            extEl.Add(tpxEl);
                        }
                        var speedEl = tpxEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Speed");
                        if (speedEl == null)
                        {
                            speedEl = new XElement(ActivityExtNs + "Speed");
                            tpxEl.Add(speedEl);
                        }
                        speedEl.Value = pt.Speed.Value.ToString("F2", CultureInfo.InvariantCulture);
                    }
                }
                else
                {
                    tpEl = new XElement(TcxNs + "Trackpoint");

                    if (pt.Time.HasValue)
                    {
                        tpEl.Add(new XElement(TcxNs + "Time", pt.Time.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")));
                    }

                    tpEl.Add(new XElement(TcxNs + "Position",
                        new XElement(TcxNs + "LatitudeDegrees", pt.Latitude.ToString("F7", CultureInfo.InvariantCulture)),
                        new XElement(TcxNs + "LongitudeDegrees", pt.Longitude.ToString("F7", CultureInfo.InvariantCulture))
                    ));

                    if (pt.Elevation.HasValue)
                    {
                        tpEl.Add(new XElement(TcxNs + "AltitudeMeters", pt.Elevation.Value.ToString("F1", CultureInfo.InvariantCulture)));
                    }

                    tpEl.Add(new XElement(TcxNs + "DistanceMeters", pt.DistanceFromStart.ToString("F1", CultureInfo.InvariantCulture)));

                    if (pt.HeartRate.HasValue)
                    {
                        tpEl.Add(new XElement(TcxNs + "HeartRateBpm",
                            new XElement(TcxNs + "Value", pt.HeartRate.Value.ToString())
                        ));
                    }

                    if (pt.Cadence.HasValue)
                    {
                        tpEl.Add(new XElement(TcxNs + "Cadence", pt.Cadence.Value.ToString()));
                    }

                    if (pt.Speed.HasValue)
                    {
                        tpEl.Add(new XElement(TcxNs + "Extensions",
                            new XElement(ActivityExtNs + "TPX",
                                new XElement(ActivityExtNs + "Speed", pt.Speed.Value.ToString("F2", CultureInfo.InvariantCulture))
                            )
                        ));
                    }
                }

                trackElement.Add(tpEl);
                count++;
            }

            log.Info($"Built TCX Track with {count} points. Adding Lap and Activity headers...", "TCX I/O");

            var lapElement = new XElement(TcxNs + "Lap",
                new XAttribute("StartTime", isoStartTime),
                new XElement(TcxNs + "TotalTimeSeconds", document.Statistics.TotalDuration.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture)),
                new XElement(TcxNs + "DistanceMeters", document.Statistics.TotalDistanceMeters.ToString("F1", CultureInfo.InvariantCulture)),
                new XElement(TcxNs + "MaximumSpeed", (document.Statistics.MaxSpeedKmh / 3.6).ToString("F2", CultureInfo.InvariantCulture)),
                new XElement(TcxNs + "Calories", 0),
                new XElement(TcxNs + "Intensity", "Active"),
                new XElement(TcxNs + "TriggerMethod", "Manual"),
                trackElement
            );

            var activityElement = new XElement(TcxNs + "Activity",
                new XAttribute("Sport", "Other"),
                new XElement(TcxNs + "Id", isoStartTime),
                lapElement
            );

            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement(TcxNs + "TrainingCenterDatabase",
                    new XAttribute(XNamespace.Xmlns + "ns2", ActivityExtNs),
                    new XElement(TcxNs + "Activities", activityElement)
                )
            );

            doc.Save(outputStream);
            log.Success($"TCX export complete. Successfully saved XML document to stream.", "TCX I/O");
            return Task.CompletedTask;
        }
    }
}
