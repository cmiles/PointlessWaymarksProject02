using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PointedHorizons.Models;

namespace PointedHorizons.Services;

/// <summary>
/// Fast spatial index and disk cache manager for GPX, TCX, and FIT files.
/// Scans on startup and when requested, updating an incremental cache so large directories
/// load in milliseconds without re-parsing files.
/// </summary>
public static class TrackIndexService
{
    private static readonly List<TrackFileInfo> _tracks = new();
    private static readonly object _syncLock = new();
    private static bool _isScanning;
    private static bool _initialized;

    public static event EventHandler? TracksChanged;
    public static event EventHandler? ScanCompleted;

    public static IReadOnlyList<TrackFileInfo> Tracks
    {
        get
        {
            lock (_syncLock)
            {
                return _tracks.ToList();
            }
        }
    }

    public static bool IsScanning => _isScanning;

    public static string CacheFilePath
    {
        get
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(appData))
                {
                    return Path.Combine(appData, "PointedHorizons", "gpx_track_index.json");
                }
            }
            catch { }
            return "gpx_track_index.json";
        }
    }

    /// <summary>
    /// Initializes the track index on startup in the background.
    /// </summary>
    public static void InitializeBackgroundScan()
    {
        if (_initialized) return;
        _initialized = true;

        Task.Run(async () =>
        {
            try
            {
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrackIndexService] Startup scan error: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Scans the GPX directory, using the cache for unchanged files and parsing new/modified files.
    /// </summary>
    public static async Task RefreshAsync(bool forceFullRescan = false)
    {
        if (_isScanning) return;
        _isScanning = true;

        await Task.Run(async () =>
        {
            try
            {
                OfflineDataService.EnsureDirectoriesExist();
                string gpxDir = OfflineDataService.GpxDirectory;

                // 1. Ensure any tracks previously saved to localStorage are exported as GPX files on disk
                try
                {
                    await TrackStorageService.EnsureAllSavedTracksExportedToGpxAsync();
                }
                catch { }

                // 2. Discover all .gpx, .tcx, .fit files across all candidate directories
                var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".gpx", ".tcx", ".fit"
                };

                var candidateDirs = OfflineDataService.GetGpxCandidateDirectories();
                var files = new List<string>();
                var seenFnames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var dir in candidateDirs)
                {
                    try
                    {
                        if (Directory.Exists(dir))
                        {
                            foreach (var f in Directory.GetFiles(dir))
                            {
                                if (supportedExtensions.Contains(Path.GetExtension(f)))
                                {
                                    string fname = Path.GetFileName(f);
                                    if (seenFnames.Add(fname))
                                    {
                                        files.Add(f);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[TrackIndexService] Error scanning dir '{dir}': {ex.Message}");
                    }
                }

                if (files.Count == 0 && !Directory.Exists(gpxDir))
                {
                    lock (_syncLock) { _tracks.Clear(); }
                    return;
                }

                // 3. Load cached index
                var cachedMap = new Dictionary<string, TrackFileInfo>(StringComparer.OrdinalIgnoreCase);
                if (!forceFullRescan)
                {
                    var cache = LoadCacheFromDisk();
                    if (cache != null && cache.CacheVersion >= 2 && cache.Entries != null)
                    {
                        foreach (var entry in cache.Entries)
                        {
                            if (!string.IsNullOrEmpty(entry.FilePath))
                            {
                                cachedMap[entry.FilePath] = entry;
                            }
                        }
                    }
                }

                var updatedTracks = new List<TrackFileInfo>();
                bool cacheChanged = false;

                foreach (var file in files)
                {
                    try
                    {
                        var fi = new FileInfo(file);
                        string quickHash = TrackParserService.ComputeQuickFileHash(file, fi.Length, fi.LastWriteTimeUtc);

                        // Check cache hit
                        if (cachedMap.TryGetValue(file, out var cachedItem) &&
                            cachedItem.FileSizeBytes == fi.Length &&
                            cachedItem.LastModifiedUtc == fi.LastWriteTimeUtc &&
                            cachedItem.FileHash == quickHash &&
                            (cachedItem.SimplifiedCoordinates.Count > 0 || cachedItem.Waypoints.Count > 0 || cachedItem.Routes.Count > 0))
                        {
                            updatedTracks.Add(cachedItem);
                        }
                        else
                        {
                            // Cache miss or modified file: parse in background
                            var parsed = await TrackParserService.ParseTrackFileAsync(file);
                            if (parsed != null)
                            {
                                updatedTracks.Add(parsed);
                                cacheChanged = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[TrackIndexService] Error processing file '{file}': {ex.Message}");
                    }
                }

                // Sort by date newest first
                updatedTracks.Sort((a, b) =>
                {
                    DateTime ta = a.StartTimeUtc ?? a.LastModifiedUtc;
                    DateTime tb = b.StartTimeUtc ?? b.LastModifiedUtc;
                    return tb.CompareTo(ta);
                });

                lock (_syncLock)
                {
                    _tracks.Clear();
                    _tracks.AddRange(updatedTracks);
                }

                if (cacheChanged || updatedTracks.Count != cachedMap.Count)
                {
                    SaveCacheToDisk(updatedTracks, gpxDir);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrackIndexService] Refresh error: {ex.Message}");
            }
            finally
            {
                _isScanning = false;
            }
        });

        TracksChanged?.Invoke(null, EventArgs.Empty);
        ScanCompleted?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Gets all active tracks whose bounding box overlaps the given coordinate viewport.
    /// </summary>
    public static List<TrackFileInfo> GetTracksInBounds(double minLon, double minLat, double maxLon, double maxLat, bool onlyVisible = true)
    {
        lock (_syncLock)
        {
            return _tracks.Where(t =>
                (!onlyVisible || t.IsVisibleOnMap) &&
                t.HasValidBounds &&
                t.MaxLon >= minLon && t.MinLon <= maxLon &&
                t.MaxLat >= minLat && t.MinLat <= maxLat
            ).ToList();
        }
    }

    /// <summary>
    /// Toggles map visibility for an individual track.
    /// </summary>
    public static void SetTrackVisibility(string id, bool isVisible)
    {
        lock (_syncLock)
        {
            var t = _tracks.FirstOrDefault(x => x.Id == id);
            if (t != null)
            {
                t.IsVisibleOnMap = isVisible;
            }
        }
        TracksChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Sets visibility for all tracks.
    /// </summary>
    public static void SetAllVisibility(bool isVisible)
    {
        lock (_syncLock)
        {
            foreach (var t in _tracks)
            {
                t.IsVisibleOnMap = isVisible;
            }
        }
        TracksChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Updates an in-memory waypoint and saves cache to disk, firing TracksChanged.
    /// </summary>
    public static void UpdateWaypoint(string trackId, string waypointId, string newName, string newDesc, double newLat, double newLon, double? newEle)
    {
        lock (_syncLock)
        {
            var track = _tracks.FirstOrDefault(t => t.Id == trackId) ?? _tracks.FirstOrDefault(t => t.Waypoints.Any(w => w.Id == waypointId));
            if (track != null)
            {
                var wpt = track.Waypoints.FirstOrDefault(w => w.Id == waypointId);
                if (wpt != null)
                {
                    wpt.Name = newName;
                    wpt.Description = newDesc;
                    wpt.Latitude = newLat;
                    wpt.Longitude = newLon;
                    if (newEle.HasValue) wpt.ElevationM = newEle.Value;
                }
            }
        }
        SaveCacheToDisk(_tracks, OfflineDataService.GpxDirectory);
        TracksChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Generates lightweight payload of visible tracks for MapLibre viewport rendering.
    /// </summary>
    public static object GetMapTracksPayload()
    {
        lock (_syncLock)
        {
            return _tracks.Select(t =>
            {
                t.EnsureElevationProfile();
                return new
                {
                    id = t.Id,
                    name = t.TrackName,
                    fileName = t.FileName,
                    format = t.FileFormat,
                    color = t.DisplayColor,
                    isVisible = t.IsVisibleOnMap,
                    minLon = t.MinLon,
                    maxLon = t.MaxLon,
                    minLat = t.MinLat,
                    maxLat = t.MaxLat,
                    distance = t.TotalDistanceKm,
                    duration = t.FormattedDuration,
                    climb = t.TotalClimbM,
                    descent = t.TotalDescentM,
                    minElevation = t.MinElevationM,
                    maxElevation = t.MaxElevationM,
                    coordinates = t.SimplifiedCoordinates,
                    profile = t.ElevationProfile,
                    waypoints = t.Waypoints.Select(w => new
                    {
                        id = w.Id,
                        name = w.Name,
                        desc = !string.IsNullOrWhiteSpace(w.Description) ? w.Description : w.Comment,
                        sym = w.Symbol,
                        lat = w.Latitude,
                        lon = w.Longitude,
                        ele = w.ElevationM,
                        time = w.TimeUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                    }).ToList(),
                    routes = t.Routes.Select(r => new
                    {
                        id = r.Id,
                        name = r.Name,
                        desc = r.Description,
                        distance = r.TotalDistanceKm,
                        coordinates = r.Coordinates
                    }).ToList()
                };
            }).ToList();
        }
    }

    /// <summary>
    /// Generates JSON string of all tracks for MapLibre viewport rendering.
    /// </summary>
    public static string GetMapTracksJson()
    {
        var payload = GetMapTracksPayload();
        return JsonSerializer.Serialize(payload);
    }

    private static TrackIndexCache? LoadCacheFromDisk()
    {
        try
        {
            string path = CacheFilePath;
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<TrackIndexCache>(json);
            }
        }
        catch { }
        return null;
    }

    private static void SaveCacheToDisk(List<TrackFileInfo> tracks, string gpxDir)
    {
        try
        {
            string path = CacheFilePath;
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var cache = new TrackIndexCache
            {
                CacheVersion = 2,
                GpxDirectory = gpxDir,
                LastScanTimeUtc = DateTime.UtcNow,
                Entries = tracks
            };

            string json = JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = false });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TrackIndexService] Save cache error: {ex.Message}");
        }
    }
}
