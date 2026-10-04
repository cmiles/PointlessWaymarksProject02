using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using PwTrackTrimmer.Models;

namespace PwTrackTrimmer.Services
{
    public static class SampleTrackService
    {
        public const string DefaultFilePath = @"C:\Users\charl\Downloads\2015 October Plateau Point - 10242015.gpx";

        public static async Task<TrackDocument> LoadInitialTrackAsync()
        {
            string fileName = "2015 October Plateau Point - 10242015.gpx";

            // 1. Try reading directly from requested local path (for desktop / Photino)
            try
            {
                if (File.Exists(DefaultFilePath))
                {
                    using var fileStream = File.OpenRead(DefaultFilePath);
                    var gpxService = new GpxTrackService();
                    return await gpxService.LoadAsync(fileStream, fileName);
                }
            }
            catch
            {
                // In sandboxed environments (like WebAssembly), direct local file access may not be permitted
            }

            // 2. Read from embedded resource (for WebAssembly browser target & offline fallback)
            try
            {
                var asm = typeof(SampleTrackService).Assembly;
                using var resourceStream = asm.GetManifestResourceStream("PwTrackTrimmer.Resources.SampleTrack.gpx");
                if (resourceStream != null)
                {
                    var gpxService = new GpxTrackService();
                    return await gpxService.LoadAsync(resourceStream, fileName);
                }
            }
            catch
            {
                // Fall through to synthetic track if resource stream fails
            }

            // 3. Fallback to synthetic track
            return CreateSampleRide();
        }

        public static TrackDocument CreateSampleRide()
        {
            var doc = new TrackDocument
            {
                Name = "Mount Tamalpais Scenic Loop",
                Description = "Sample road ride with climbs and descents",
                FileFormat = "GPX"
            };

            // Base coords around Marin County, CA (Mount Tamalpais)
            double startLat = 37.905;
            double startLon = -122.600;
            DateTime startTime = DateTime.UtcNow.Date.AddHours(9); // 9:00 AM

            int totalPoints = 180;
            double baseEle = 120.0;

            for (int i = 0; i < totalPoints; i++)
            {
                double fraction = (double)i / totalPoints;
                double angle = fraction * 2 * Math.PI;

                // Loop path
                double lat = startLat + 0.04 * Math.Sin(angle) + 0.01 * Math.Sin(2 * angle);
                double lon = startLon + 0.06 * Math.Cos(angle);

                // Mountain elevation profile (climb up to 750m then descend)
                double elevation = baseEle + 630.0 * Math.Pow(Math.Sin(fraction * Math.PI), 1.8);
                // slight jitter
                elevation += 3.0 * Math.Sin(i * 0.4);

                DateTime time = startTime.AddSeconds(i * 30);
                double speed = 5.5 + 4.0 * Math.Cos(fraction * Math.PI); // slower on climbs, faster on descents
                if (speed < 2.5) speed = 2.5;

                var pt = new TrackPoint
                {
                    Index = i,
                    Latitude = lat,
                    Longitude = lon,
                    Elevation = Math.Round(elevation, 1),
                    Time = time,
                    Speed = Math.Round(speed, 1),
                    HeartRate = (short)(135 + (int)(30 * Math.Sin(fraction * Math.PI))),
                    Cadence = (short)(82 + (int)(10 * Math.Cos(fraction * Math.PI)))
                };

                doc.Points.Add(pt);
            }

            TrackStatisticsCalculator.RecalculateTrack(doc.Points, doc.Statistics);
            return doc;
        }
    }
}
