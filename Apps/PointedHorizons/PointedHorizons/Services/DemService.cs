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
    /// Resolves the local DEM cache directory path (e.g. "DEM" folder beside the running application).
    /// </summary>
    public static string GetLocalCacheDirectory()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir))
            {
                return Path.Combine(baseDir, "DEM");
            }
        }
        catch
        {
        }
        return "DEM";
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

        // 2. Check local DEM/ directory beside application (Photino desktop cache)
        string localDir = GetLocalCacheDirectory();
        string[] localExtensions = [".hgt", ".zip", ".hgt.zip"];

        foreach (var ext in localExtensions)
        {
            try
            {
                string localFilePath = Path.Combine(localDir, $"{tileKey}{ext}");
                if (File.Exists(localFilePath))
                {
                    progress?.Report($"Loading local tile {tileKey}...");
                    byte[] fileBytes = await File.ReadAllBytesAsync(localFilePath);
                    byte[] hgtBytes = HgtTile.ExtractHgtBytes(fileBytes, tileKey);
                    var tile = new HgtTile(hgtBytes, tileKey);
                    _tileCache[tileKey] = tile;
                    return tile;
                }
            }
            catch
            {
                // Continue to remote download
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

                    // Save to local cache folder if supported (desktop/Photino)
                    try
                    {
                        if (!Directory.Exists(localDir))
                        {
                            Directory.CreateDirectory(localDir);
                        }
                        string savePath = Path.Combine(localDir, $"{tileKey}.hgt");
                        if (!File.Exists(savePath))
                        {
                            await File.WriteAllBytesAsync(savePath, hgtBytes);
                        }
                    }
                    catch
                    {
                        // Ignore local cache write failures in sandbox/browser
                    }

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
