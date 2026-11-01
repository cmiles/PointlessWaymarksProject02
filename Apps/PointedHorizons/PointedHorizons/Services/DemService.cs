using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace PointedHorizons.Services;

/// <summary>
/// Service that coordinates loading, caching, and querying Viewfinderpanoramas HGT DEM tiles
/// both from a local directory (for Photino desktop) and from the remote DEM server.
/// </summary>
public static class DemService
{
    /// <summary>
    /// Base URL for downloading DEM tiles. Default: https://software.pointlesswaymarks.com/dem
    /// </summary>
    public static string DemBaseUrl { get; set; } = "https://software.pointlesswaymarks.com/dem";

    private static readonly ConcurrentDictionary<string, HgtTile> _tileCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>
    /// Resolves the local DEM directory path (configured or default).
    /// </summary>
    public static string GetLocalCacheDirectory()
    {
        try
        {
            return OfflineDataService.DemDirectory;
        }
        catch
        {
            return "DEM";
        }
    }

    /// <summary>
    /// Registers a tile directly into the in-memory cache.
    /// </summary>
    public static void RegisterTile(string tileKey, HgtTile tile)
    {
        if (!string.IsNullOrEmpty(tileKey) && tile != null)
        {
            _tileCache[tileKey] = tile;
        }
    }

    /// <summary>
    /// Checks if a tile is already loaded in memory.
    /// </summary>
    public static bool IsTileLoaded(string tileKey) => _tileCache.ContainsKey(tileKey);

    /// <summary>
    /// Clears the in-memory tile cache.
    /// </summary>
    public static void ClearMemoryCache()
    {
        _tileCache.Clear();
    }

    /// <summary>
    /// Retrieves an HgtTile by key (e.g. "N32W111"), first checking memory cache,
    /// then the local DEM/ directory beside the app, and finally attempting to download from DemBaseUrl.
    /// Returns null if the tile cannot be found or downloaded.
    /// </summary>
    public static async Task<HgtTile?> GetOrLoadTileAsync(string tileKey, IProgress<string>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(tileKey)) return null;

        tileKey = tileKey.Trim().ToUpperInvariant();

        // 1. In-memory cache
        if (_tileCache.TryGetValue(tileKey, out var cached))
        {
            return cached;
        }

        // 2. Check local DEM directory (.hgt, .zip, etc.)
        string? localFilePath = OfflineDataService.FindLocalDemFile(tileKey);
        if (!string.IsNullOrEmpty(localFilePath) && File.Exists(localFilePath))
        {
            try
            {
                progress?.Report($"Loading local DEM tile {tileKey}...");
                byte[] fileBytes = await File.ReadAllBytesAsync(localFilePath);
                byte[] hgtBytes = HgtTile.ExtractHgtBytes(fileBytes, tileKey);
                var tile = new HgtTile(hgtBytes, tileKey);
                _tileCache[tileKey] = tile;
                return tile;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DemService] Error loading local tile {localFilePath}: {ex.Message}");
            }
        }

        // 3. Retrieve from remote DEM server (DemBaseUrl)
        string baseUrl = DemBaseUrl.TrimEnd('/');
        string[] remoteUrls =
        [
            $"{baseUrl}/{tileKey}.hgt",
            $"{baseUrl}/{tileKey}.zip",
            $"{baseUrl}/{tileKey}.hgt.zip"
        ];

        foreach (var url in remoteUrls)
        {
            try
            {
                progress?.Report($"Downloading DEM tile {tileKey}...");
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (response.IsSuccessStatusCode)
                {
                    byte[] downloadedBytes = await response.Content.ReadAsByteArrayAsync();
                    byte[] hgtBytes = HgtTile.ExtractHgtBytes(downloadedBytes, tileKey);
                    var tile = new HgtTile(hgtBytes, tileKey);
                    _tileCache[tileKey] = tile;

                    // Automatically cache downloaded DEMs to local folder
                    _ = OfflineDataService.SaveDownloadedDemAsync(tileKey, hgtBytes);

                    return tile;
                }
            }
            catch
            {
                // Try next URL extension
            }
        }

        return null;
    }

    /// <summary>
    /// Calculates all 1x1 degree tile keys required for a given bounding box.
    /// </summary>
    public static List<string> GetTileKeysForBox(double minLat, double minLon, double maxLat, double maxLon)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int startLat = (int)Math.Floor(minLat);
        int endLat = (int)Math.Floor(maxLat);
        int startLon = (int)Math.Floor(minLon);
        int endLon = (int)Math.Floor(maxLon);

        for (int lat = startLat; lat <= endLat; lat++)
        {
            for (int lon = startLon; lon <= endLon; lon++)
            {
                keys.Add(HgtTile.GetTileKey(lat, lon));
            }
        }

        return keys.OrderBy(k => k).ToList();
    }

    /// <summary>
    /// Calculates tile keys required for an observer location within a radius in kilometers.
    /// </summary>
    public static List<string> GetTileKeysForRadius(double centerLat, double centerLon, double radiusKm)
    {
        double latSpan = radiusKm / 111.0;
        double lonSpan = radiusKm / (111.0 * Math.Max(0.1, Math.Cos(centerLat * Math.PI / 180.0)));

        return GetTileKeysForBox(
            centerLat - latSpan,
            centerLon - lonSpan,
            centerLat + latSpan,
            centerLon + lonSpan
        );
    }

    /// <summary>
    /// Calculates all 1x1 degree tile keys along a ray defined by an origin, bearing, and distance in km.
    /// </summary>
    public static List<string> GetTileKeysAlongRay(double originLat, double originLon, double bearingDeg, double distanceKm)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        const double R = 6371.0;
        double bRad = bearingDeg * Math.PI / 180.0;
        double latRad = originLat * Math.PI / 180.0;
        double lonRad = originLon * Math.PI / 180.0;
        double sinLat = Math.Sin(latRad);
        double cosLat = Math.Cos(latRad);
        double sinB = Math.Sin(bRad);
        double cosB = Math.Cos(bRad);

        for (double d = 0; d <= distanceKm + 6.0; d += 12.0)
        {
            double dClamped = Math.Min(d, distanceKm);
            double dByR = dClamped / R;
            double sinD = Math.Sin(dByR);
            double cosD = Math.Cos(dByR);

            double lat2 = Math.Asin(sinLat * cosD + cosLat * sinD * cosB);
            double lon2 = lonRad + Math.Atan2(sinB * sinD * cosLat, cosD - sinLat * Math.Sin(lat2));

            double degLat = lat2 * 180.0 / Math.PI;
            double degLon = lon2 * 180.0 / Math.PI;

            keys.Add(HgtTile.GetTileKey(degLat, degLon));
        }

        return keys.OrderBy(k => k).ToList();
    }

    /// <summary>
    /// Preloads all tiles needed for a radius around observer coordinates.
    /// </summary>
    public static async Task<List<string>> PreloadTilesForRadiusAsync(
        double centerLat, double centerLon, double radiusKm, IProgress<string>? progress = null)
    {
        var keys = GetTileKeysForRadius(centerLat, centerLon, radiusKm);
        var loaded = new List<string>();

        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            progress?.Report($"Fetching DEM tile {i + 1}/{keys.Count} ({key})...");
            var tile = await GetOrLoadTileAsync(key, progress);
            if (tile != null)
            {
                loaded.Add(key);
            }
        }

        return loaded;
    }

    /// <summary>
    /// Synchronously retrieves interpolated elevation in meters if the corresponding tile is in memory.
    /// Returns null if outside cached tiles or if void.
    /// </summary>
    public static double? GetElevation(double lat, double lon)
    {
        string key = HgtTile.GetTileKey(lat, lon);
        if (_tileCache.TryGetValue(key, out var tile))
        {
            return tile.GetElevation(lat, lon);
        }
        return null;
    }

    /// <summary>
    /// Asynchronously retrieves elevation in meters, downloading the tile if not already cached.
    /// </summary>
    public static async Task<double?> GetElevationAsync(double lat, double lon)
    {
        string key = HgtTile.GetTileKey(lat, lon);
        var tile = await GetOrLoadTileAsync(key);
        return tile?.GetElevation(lat, lon);
    }
}
