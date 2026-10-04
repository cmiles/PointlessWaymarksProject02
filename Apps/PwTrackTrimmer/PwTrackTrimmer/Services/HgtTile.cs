using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace PwTrackTrimmer.Services
{
    /// <summary>
    /// Represents a 1° x 1° SRTM / Viewfinderpanoramas HGT elevation tile.
    /// Supports both 3-arcsecond (1201x1201) and 1-arcsecond (3601x3601) grid resolutions
    /// with bilinear interpolation for sub-grid coordinate queries.
    /// </summary>
    public class HgtTile
    {
        private readonly short[] _elevations;
        private readonly int _gridSize;
        private readonly int _originLat;
        private readonly int _originLon;

        public string TileKey { get; }
        public int GridSize => _gridSize;
        public int OriginLat => _originLat;
        public int OriginLon => _originLon;

        public HgtTile(byte[] rawHgtBytes, string tileKey)
        {
            TileKey = tileKey;
            if (!TryParseTileKey(tileKey, out _originLat, out _originLon))
            {
                throw new ArgumentException($"Invalid HGT tile key format: '{tileKey}'. Expected format e.g. 'N37W123'.", nameof(tileKey));
            }

            // Raw HGT files are 2 bytes per point.
            // 1201x1201x2 = 2,884,802 bytes (3-arcsec)
            // 3601x3601x2 = 25,934,402 bytes (1-arcsec)
            int numShorts = rawHgtBytes.Length / 2;
            _gridSize = (int)Math.Round(Math.Sqrt(numShorts));

            if (_gridSize * _gridSize * 2 != rawHgtBytes.Length)
            {
                throw new InvalidDataException($"HGT byte array length ({rawHgtBytes.Length} bytes) does not form a square elevation grid.");
            }

            _elevations = new short[_gridSize * _gridSize];
            for (int i = 0; i < _elevations.Length; i++)
            {
                int byteOffset = i * 2;
                // SRTM HGT is 16-bit signed big-endian
                unchecked
                {
                    _elevations[i] = (short)((rawHgtBytes[byteOffset] << 8) | (rawHgtBytes[byteOffset + 1] & 0xFF));
                }
            }
        }

        public HgtTile(byte[] rawHgtBytes, int originLat, int originLon)
            : this(rawHgtBytes, GetTileKey(originLat, originLon))
        {
        }

        /// <summary>
        /// Calculates the standard SRTM 1°x1° tile key from geographic coordinates.
        /// E.g., (37.77, -122.41) -> "N37W123".
        /// </summary>
        public static string GetTileKey(double lat, double lon)
        {
            int floorLat = (int)Math.Floor(lat);
            int floorLon = (int)Math.Floor(lon);
            char ns = floorLat >= 0 ? 'N' : 'S';
            char ew = floorLon >= 0 ? 'E' : 'W';
            return $"{ns}{Math.Abs(floorLat):D2}{ew}{Math.Abs(floorLon):D3}";
        }

        /// <summary>
        /// Parses the South-West origin latitude and longitude from a tile key (e.g. "N37W123").
        /// </summary>
        public static bool TryParseTileKey(string keyOrFileName, out int originLat, out int originLon)
        {
            originLat = 0;
            originLon = 0;
            if (string.IsNullOrWhiteSpace(keyOrFileName)) return false;

            string clean = Path.GetFileName(keyOrFileName);
            int dotIdx = clean.IndexOf('.');
            if (dotIdx > 0) clean = clean.Substring(0, dotIdx);

            if (clean.Length != 7) return false;

            char ns = char.ToUpperInvariant(clean[0]);
            char ew = char.ToUpperInvariant(clean[3]);

            if ((ns != 'N' && ns != 'S') || (ew != 'E' && ew != 'W')) return false;

            if (!int.TryParse(clean.Substring(1, 2), out int lat) ||
                !int.TryParse(clean.Substring(4, 3), out int lon))
            {
                return false;
            }

            originLat = (ns == 'S') ? -lat : lat;
            originLon = (ew == 'W') ? -lon : lon;
            return true;
        }

        /// <summary>
        /// Inspects arbitrary file bytes and decompresses if zipped (PK header), returning raw HGT bytes.
        /// </summary>
        public static byte[] ExtractHgtBytes(byte[] fileBytes, string expectedTileKey)
        {
            if (fileBytes == null || fileBytes.Length < 4) return fileBytes;

            // Check for ZIP magic bytes: 'P', 'K', 0x03, 0x04
            if (fileBytes[0] == 0x50 && fileBytes[1] == 0x4B && fileBytes[2] == 0x03 && fileBytes[3] == 0x04)
            {
                using var ms = new MemoryStream(fileBytes);
                using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

                var entry = zip.Entries.FirstOrDefault(e => e.Name.Equals($"{expectedTileKey}.hgt", StringComparison.OrdinalIgnoreCase))
                            ?? zip.Entries.FirstOrDefault(e => e.Name.EndsWith(".hgt", StringComparison.OrdinalIgnoreCase))
                            ?? zip.Entries.FirstOrDefault();

                if (entry != null)
                {
                    using var entryStream = entry.Open();
                    using var outMs = new MemoryStream();
                    entryStream.CopyTo(outMs);
                    return outMs.ToArray();
                }
            }

            return fileBytes;
        }

        /// <summary>
        /// Retrieves interpolated elevation in meters for a coordinate within this tile.
        /// Returns null if outside the tile or if all neighboring grid points are void.
        /// </summary>
        public double? GetElevation(double lat, double lon)
        {
            double relLat = lat - _originLat;
            double relLon = lon - _originLon;

            // Allow small epsilon tolerance for edge points
            const double eps = 1e-7;
            if (relLat < -eps || relLat > 1.0 + eps || relLon < -eps || relLon > 1.0 + eps)
            {
                return null;
            }

            relLat = Math.Clamp(relLat, 0.0, 1.0);
            relLon = Math.Clamp(relLon, 0.0, 1.0);

            // Row 0 is North (relLat = 1.0), Row gridSize - 1 is South (relLat = 0.0)
            double y = (1.0 - relLat) * (_gridSize - 1);
            // Col 0 is West (relLon = 0.0), Col gridSize - 1 is East (relLon = 1.0)
            double x = relLon * (_gridSize - 1);

            int x0 = (int)Math.Floor(x);
            int x1 = Math.Min(x0 + 1, _gridSize - 1);
            int y0 = (int)Math.Floor(y);
            int y1 = Math.Min(y0 + 1, _gridSize - 1);

            short e00 = _elevations[y0 * _gridSize + x0]; // NW
            short e10 = _elevations[y0 * _gridSize + x1]; // NE
            short e01 = _elevations[y1 * _gridSize + x0]; // SW
            short e11 = _elevations[y1 * _gridSize + x1]; // SE

            double dx = x - x0;
            double dy = y - y0;

            // SRTM void value is -32768
            const short voidThreshold = -32000;
            if (e00 > voidThreshold && e10 > voidThreshold && e01 > voidThreshold && e11 > voidThreshold)
            {
                double top = e00 * (1.0 - dx) + e10 * dx;
                double bottom = e01 * (1.0 - dx) + e11 * dx;
                return top * (1.0 - dy) + bottom * dy;
            }

            // If some points are void, interpolate among the non-void neighbors
            double sumWeights = 0;
            double sumValues = 0;

            void AddValidPoint(short val, double weight)
            {
                if (val > voidThreshold && weight > 0)
                {
                    sumWeights += weight;
                    sumValues += val * weight;
                }
            }

            AddValidPoint(e00, (1.0 - dx) * (1.0 - dy));
            AddValidPoint(e10, dx * (1.0 - dy));
            AddValidPoint(e01, (1.0 - dx) * dy);
            AddValidPoint(e11, dx * dy);

            if (sumWeights > 1e-6)
            {
                return sumValues / sumWeights;
            }

            return null;
        }
    }
}
