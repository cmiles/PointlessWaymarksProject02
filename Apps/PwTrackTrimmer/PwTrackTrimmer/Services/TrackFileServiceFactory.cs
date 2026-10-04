using System;
using System.IO;

namespace PwTrackTrimmer.Services
{
    public static class TrackFileServiceFactory
    {
        public static ITrackFileService GetService(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("Filename cannot be empty", nameof(fileName));
            }

            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".fit" => new FitTrackService(),
                ".tcx" => new TcxTrackService(),
                ".gpx" => new GpxTrackService(),
                _ => throw new NotSupportedException($"Format '{ext}' is not supported. Supported formats: .fit, .tcx, .gpx")
            };
        }
    }
}
