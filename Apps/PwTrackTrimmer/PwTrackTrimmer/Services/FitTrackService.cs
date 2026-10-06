using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dynastream.Fit;
using PwTrackTrimmer.Models;

namespace PwTrackTrimmer.Services
{
    public class FitTrackService : ITrackFileService
    {
        private const double SemicirclesToDegrees = 180.0 / 2147483648.0;
        private const double DegreesToSemicircles = 2147483648.0 / 180.0;

        public Task<TrackDocument> LoadAsync(Stream stream, string fileName)
        {
            var log = ActivityLogService.Instance;
            log.Info($"Opening Garmin FIT stream for '{Path.GetFileName(fileName)}'...", "FIT I/O");

            var document = new TrackDocument
            {
                Name = Path.GetFileNameWithoutExtension(fileName),
                SourceFilePath = fileName,
                FileFormat = "FIT"
            };

            var rawPoints = new List<TrackPoint>();
            int index = 0;
            int totalRecordsEncountered = 0;
            int recordsWithoutPosition = 0;
            var decode = new Decode();
            var broadcaster = new MesgBroadcaster();
            decode.MesgEvent += broadcaster.OnMesg;

            log.Info("Initializing FIT SDK message broadcaster and event hooks...", "FIT I/O");

            broadcaster.FileIdMesgEvent += (sender, e) =>
            {
                var fileId = new FileIdMesg(e.mesg);
                var timeCreated = fileId.GetTimeCreated();
                if (timeCreated != null && string.IsNullOrEmpty(document.Description))
                {
                    document.Description = $"Recorded on {timeCreated.GetDateTime():yyyy-MM-dd HH:mm:ss}";
                    log.Info($"Parsed FIT FileID: Activity timestamp = {document.Description}", "FIT I/O");
                }
            };

            broadcaster.RecordMesgEvent += (sender, e) =>
            {
                totalRecordsEncountered++;
                var rawMesg = new Mesg(e.mesg);
                var record = new RecordMesg(rawMesg);
                int? latSemicircles = record.GetPositionLat();
                int? lonSemicircles = record.GetPositionLong();

                if (!TryExtractValidPosition(latSemicircles, lonSemicircles, out double lat, out double lon))
                {
                    recordsWithoutPosition++;
                    return;
                }

                var timestamp = record.GetTimestamp();
                System.DateTime? time = timestamp?.GetDateTime();

                var pt = new TrackPoint
                {
                    Index = index++,
                    Latitude = lat,
                    Longitude = lon,
                    Elevation = record.GetAltitude(),
                    Time = time,
                    Speed = record.GetSpeed(),
                    HeartRate = record.GetHeartRate(),
                    Cadence = record.GetCadence(),
                    RawData = rawMesg
                };

                float? dist = record.GetDistance();
                if (dist.HasValue)
                {
                    pt.DistanceFromStart = dist.Value;
                }

                rawPoints.Add(pt);

                if (index % 500 == 0)
                {
                    log.Info($"Decoded {index} FIT track points (Point #{index} at {lat:F4}, {lon:F4})...", "FIT I/O");
                }
            };

            log.Info("Executing binary stream decode via Garmin.FIT.Sdk...", "FIT I/O");
            decode.Read(stream);

            if (recordsWithoutPosition > 0)
            {
                log.Info($"Filtered out {recordsWithoutPosition} FIT record(s) without valid GPS positions.", "FIT I/O");
            }

            document.SetPoints(rawPoints);
            log.Success($"FIT decode completed. Processed {totalRecordsEncountered} records, extracted {document.Points.Count} valid GPS trackpoints ({recordsWithoutPosition} points without positions filtered out).", "FIT I/O");

            TrackStatisticsCalculator.RecalculateTrack(document.Points, document.Statistics);
            return Task.FromResult(document);
        }

        private static bool IsValidSemicircles(int? semicircles)
        {
            if (!semicircles.HasValue) return false;
            int val = semicircles.Value;
            // 0x7FFFFFFF (2147483647) is Garmin FIT Sint32 Invalid sentinel
            // Values >= 0x7FFFFFF0 or int.MinValue (-2147483648) represent invalid/unset in FIT protocol
            if (val >= 0x7FFFFFF0 || val == int.MaxValue || val == int.MinValue)
            {
                return false;
            }
            return true;
        }

        private static bool TryExtractValidPosition(int? latSemicircles, int? lonSemicircles, out double lat, out double lon)
        {
            lat = 0;
            lon = 0;

            if (!IsValidSemicircles(latSemicircles) || !IsValidSemicircles(lonSemicircles))
            {
                return false;
            }

            // Both semicircles equal to 0 indicates missing GPS fix (indoor activity / unacquired lock)
            if (latSemicircles.Value == 0 && lonSemicircles.Value == 0)
            {
                return false;
            }

            lat = latSemicircles.Value * SemicirclesToDegrees;
            lon = lonSemicircles.Value * SemicirclesToDegrees;

            if (double.IsNaN(lat) || double.IsInfinity(lat) || double.IsNaN(lon) || double.IsInfinity(lon))
            {
                return false;
            }

            // Latitude must be strictly within [-90.0, 90.0] and Longitude within [-180.0, 180.0]
            if (lat < -90.0 || lat > 90.0 || lon < -180.0 || lon > 180.0)
            {
                return false;
            }

            // Filter points within ~11 meters of (0.0, 0.0) which represent missing position coordinates
            if (Math.Abs(lat) < 1e-4 && Math.Abs(lon) < 1e-4)
            {
                return false;
            }

            return true;
        }

        public async Task SaveAsync(TrackDocument document, Stream outputStream)
        {
            var log = ActivityLogService.Instance;
            log.Info($"Initiating Garmin FIT binary encoding for '{document.Name}' ({document.Points.Count} points)...", "FIT I/O");

            // Garmin FIT SDK's Encode.Close() requires a stream that supports both reading and seeking
            // to compute the file CRC and update the FIT header data size. If the target stream is write-only
            // or non-seekable, encode to an in-memory buffer first and copy to the destination stream.
            if (!outputStream.CanRead || !outputStream.CanSeek)
            {
                using var ms = new MemoryStream();
                WriteFitRecords(document, ms, log);
                ms.Position = 0;
                await ms.CopyToAsync(outputStream);
            }
            else
            {
                WriteFitRecords(document, outputStream, log);
            }
        }

        private void WriteFitRecords(TrackDocument document, Stream stream, ActivityLogService log)
        {
            var encode = new Encode(ProtocolVersion.V20);
            encode.Open(stream);

            log.Info("Writing FIT FileId header message (Activity)...", "FIT I/O");
            var fileIdMesg = new FileIdMesg();
            fileIdMesg.SetType(Dynastream.Fit.File.Activity);
            fileIdMesg.SetManufacturer(Manufacturer.Development);
            fileIdMesg.SetProduct(1);
            fileIdMesg.SetSerialNumber(12345);
            var nowTime = new Dynastream.Fit.DateTime(System.DateTime.UtcNow);
            fileIdMesg.SetTimeCreated(nowTime);
            encode.Write(fileIdMesg);

            bool isFitSource = string.Equals(document.FileFormat, "FIT", StringComparison.OrdinalIgnoreCase);
            int written = 0;
            int preservedRaw = 0;

            foreach (var pt in document.Points)
            {
                if (pt.IsTrimmed) continue;
                if (double.IsNaN(pt.Latitude) || double.IsNaN(pt.Longitude) ||
                    pt.Latitude < -90.0 || pt.Latitude > 90.0 || pt.Longitude < -180.0 || pt.Longitude > 180.0 ||
                    (Math.Abs(pt.Latitude) < 1e-4 && Math.Abs(pt.Longitude) < 1e-4)) continue;

                RecordMesg record;
                if (isFitSource && pt.RawData is Mesg rawMesg)
                {
                    record = new RecordMesg(new Mesg(rawMesg));
                    preservedRaw++;
                }
                else
                {
                    record = new RecordMesg();
                }

                record.SetPositionLat((int)(pt.Latitude * DegreesToSemicircles));
                record.SetPositionLong((int)(pt.Longitude * DegreesToSemicircles));

                if (pt.Elevation.HasValue)
                {
                    record.SetAltitude((float)pt.Elevation.Value);
                }
                else if (isFitSource && pt.RawData is Mesg)
                {
                    record.SetAltitude(null);
                }

                if (pt.Time.HasValue)
                {
                    record.SetTimestamp(new Dynastream.Fit.DateTime(pt.Time.Value));
                }
                else if (isFitSource && pt.RawData is Mesg)
                {
                    record.SetTimestamp(null);
                }

                if (pt.Speed.HasValue)
                {
                    record.SetSpeed((float)pt.Speed.Value);
                }

                if (pt.HeartRate.HasValue)
                {
                    record.SetHeartRate((byte)pt.HeartRate.Value);
                }

                if (pt.Cadence.HasValue)
                {
                    record.SetCadence((byte)pt.Cadence.Value);
                }

                record.SetDistance((float)pt.DistanceFromStart);
                encode.Write(record);
                written++;

                if (written % 500 == 0)
                {
                    log.Info($"Encoded {written} FIT records ({preservedRaw} with preserved raw sensor metadata)...", "FIT I/O");
                }
            }

            encode.Close();
            log.Success($"FIT encoding complete. Successfully wrote {written} Record messages to output stream ({preservedRaw} preserved extended records).", "FIT I/O");
        }
    }
}
