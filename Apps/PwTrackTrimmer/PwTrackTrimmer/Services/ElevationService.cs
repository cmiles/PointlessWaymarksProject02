using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using PwTrackTrimmer.Models;

namespace PwTrackTrimmer.Services
{
    public class ElevationAdjustResult
    {
        public bool Success { get; set; }
        public int UpdatedPointsCount { get; set; }
        public int TotalPointsCount { get; set; }
        public List<string> MissingTileKeys { get; set; } = new List<string>();
        public List<string> UsedTileKeys { get; set; } = new List<string>();
        public List<(TrackPoint Point, double? OldElevation, double? NewElevation)> Changes { get; set; } = new List<(TrackPoint, double?, double?)>();
        public string Message { get; set; }
    }

    /// <summary>
    /// Service that coordinates loading, caching, and querying Viewfinderpanoramas HGT DEM tiles
    /// both from a local directory (for Photino desktop) and from the remote DEM server.
    /// </summary>
    public static class ElevationService
    {
        /// <summary>
        /// Base URL for downloading DEM tiles. Default: https://software.pointlesswaymarks.com/dem
        /// </summary>
        public static string DemBaseUrl { get; set; } = "https://software.pointlesswaymarks.com/dem";

        private static readonly ConcurrentDictionary<string, HgtTile> _tileCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

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
        /// Registers a tile directly into the in-memory cache (useful for testing or preloaded tiles).
        /// </summary>
        public static void RegisterTile(string tileKey, HgtTile tile)
        {
            if (!string.IsNullOrEmpty(tileKey) && tile != null)
            {
                _tileCache[tileKey] = tile;
            }
        }

        /// <summary>
        /// Clears the in-memory tile cache.
        /// </summary>
        public static void ClearMemoryCache()
        {
            _tileCache.Clear();
        }

        /// <summary>
        /// Retrieves an HgtTile by key (e.g. "N37W123"), first checking memory cache,
        /// then the local DEM/ directory beside the app, and finally attempting to download from DemBaseUrl.
        /// Returns null if the tile cannot be found or downloaded.
        /// </summary>
        public static async Task<HgtTile> GetOrLoadTileAsync(string tileKey)
        {
            if (string.IsNullOrWhiteSpace(tileKey)) return null;

            // 1. In-memory cache
            if (_tileCache.TryGetValue(tileKey, out var cached))
            {
                return cached;
            }

            // 2. Check local DEM/ directory beside application (Photino desktop cache)
            string localDir = GetLocalCacheDirectory();
            string[] localExtensions = { ".hgt", ".zip", ".hgt.zip" };

            foreach (var ext in localExtensions)
            {
                try
                {
                    string localFilePath = Path.Combine(localDir, $"{tileKey}{ext}");
                    if (File.Exists(localFilePath))
                    {
                        byte[] fileBytes = await File.ReadAllBytesAsync(localFilePath);
                        byte[] hgtBytes = HgtTile.ExtractHgtBytes(fileBytes, tileKey);
                        var tile = new HgtTile(hgtBytes, tileKey);
                        _tileCache[tileKey] = tile;
                        return tile;
                    }
                }
                catch
                {
                    // If reading local file fails, continue to other extensions / remote source
                }
            }

            // 3. Retrieve from remote DEM server (DemBaseUrl)
            string baseUrl = DemBaseUrl.TrimEnd('/');
            string[] remoteUrls =
            {
                $"{baseUrl}/{tileKey}.hgt",
                $"{baseUrl}/{tileKey}.zip",
                $"{baseUrl}/{tileKey}.hgt.zip"
            };

            foreach (var url in remoteUrls)
            {
                try
                {
                    var response = await _httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        byte[] downloadedBytes = await response.Content.ReadAsByteArrayAsync();
                        byte[] hgtBytes = HgtTile.ExtractHgtBytes(downloadedBytes, tileKey);
                        var tile = new HgtTile(hgtBytes, tileKey);

                        // Cache in memory
                        _tileCache[tileKey] = tile;

                        // Cache to local DEM/ directory if disk is accessible
                        try
                        {
                            if (!Directory.Exists(localDir))
                            {
                                Directory.CreateDirectory(localDir);
                            }
                            string savePath = Path.Combine(localDir, $"{tileKey}.hgt");
                            await File.WriteAllBytesAsync(savePath, hgtBytes);
                        }
                        catch
                        {
                            // Ignored if disk cache is read-only (e.g. web sandbox)
                        }

                        return tile;
                    }
                }
                catch
                {
                    // Network or DNS error; will try next URL or fail
                }
            }

            // Tile could not be loaded or retrieved
            return null;
        }

        /// <summary>
        /// Adjusts elevations for all points in the track document using Viewfinderpanoramas HGT tiles.
        /// If ANY required DEM tile is missing or fails to load, the operation is failed immediately
        /// and no points are altered, returning the list of missing tiles.
        /// </summary>
        public static async Task<ElevationAdjustResult> AdjustElevationsAsync(
            TrackDocument document,
            Action<string, string> progressCallback = null)
        {
            if (document == null || document.Points == null || document.Points.Count == 0)
            {
                return new ElevationAdjustResult
                {
                    Success = false,
                    Message = "Track document contains no points to adjust."
                };
            }

            // Identify all required tile keys
            var requiredTileKeys = document.Points
                .Select(p => HgtTile.GetTileKey(p.Latitude, p.Longitude))
                .Distinct()
                .OrderBy(k => k)
                .ToList();

            var loadedTiles = new Dictionary<string, HgtTile>(StringComparer.OrdinalIgnoreCase);
            var missingTiles = new List<string>();

            // Retrieve all required tiles
            for (int i = 0; i < requiredTileKeys.Count; i++)
            {
                string key = requiredTileKeys[i];
                progressCallback?.Invoke("Loading DEM Tiles", $"Checking tile {key} ({i + 1}/{requiredTileKeys.Count})...");

                var tile = await GetOrLoadTileAsync(key);
                if (tile == null)
                {
                    missingTiles.Add(key);
                }
                else
                {
                    loadedTiles[key] = tile;
                }
            }

            // If ANY required tile is missing, fail the operation and alert caller
            if (missingTiles.Count > 0)
            {
                string missingFormatted = string.Join(", ", missingTiles.Select(k => $"{k}.hgt"));
                string errorMsg = $"Operation failed: Missing DEM tile(s): {missingFormatted}. " +
                                  $"The required DEM files were not found in the local DEM/ directory and could not be retrieved from {DemBaseUrl}.";

                return new ElevationAdjustResult
                {
                    Success = false,
                    TotalPointsCount = document.Points.Count,
                    MissingTileKeys = missingTiles,
                    Message = errorMsg
                };
            }

            // All tiles are available: apply elevations with bilinear interpolation
            progressCallback?.Invoke("Interpolating Elevations", "Applying sub-grid elevations to track points...");
            await Task.Delay(20);

            var changes = new List<(TrackPoint Point, double? OldElevation, double? NewElevation)>();
            int updatedCount = 0;

            foreach (var pt in document.Points)
            {
                string key = HgtTile.GetTileKey(pt.Latitude, pt.Longitude);
                if (loadedTiles.TryGetValue(key, out var tile))
                {
                    var newElev = tile.GetElevation(pt.Latitude, pt.Longitude);
                    if (newElev.HasValue)
                    {
                        double rounded = Math.Round(newElev.Value, 1);
                        changes.Add((pt, pt.Elevation, rounded));
                        pt.Elevation = rounded;
                        updatedCount++;
                    }
                }
            }

            TrackStatisticsCalculator.RecalculateTrack(document.Points, document.Statistics);

            return new ElevationAdjustResult
            {
                Success = true,
                UpdatedPointsCount = updatedCount,
                TotalPointsCount = document.Points.Count,
                UsedTileKeys = requiredTileKeys,
                Changes = changes,
                Message = $"Adjusted elevations for {updatedCount} points using {requiredTileKeys.Count} DEM tile(s) ({string.Join(", ", requiredTileKeys)})."
            };
        }
    }
}
