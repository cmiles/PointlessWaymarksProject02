using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using OpenSilver;

namespace PointedHorizons.Services;

public class RecordedTrackPoint
{
    public double Lat { get; set; }
    public double Lon { get; set; }
    public double? Ele { get; set; }
    public double? Acc { get; set; }
    public DateTime Time { get; set; }
}

public class RecordedTrack
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool IsActive { get; set; }
    public double TotalDistanceKm { get; set; }
    public List<RecordedTrackPoint> Points { get; set; } = new();

    public TimeSpan Duration => (EndTime ?? DateTime.UtcNow) - StartTime;
}

public class TrackMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool IsActive { get; set; }
    public double TotalDistanceKm { get; set; }
    public int PointCount { get; set; }

    public TimeSpan Duration => (EndTime ?? DateTime.UtcNow) - StartTime;
}

/// <summary>
/// Provides persistent storage, recovery, and retrieval for recorded GPS tracks.
/// Automatically handles crash recovery by detecting unfinalized active tracks on startup.
/// </summary>
public static class TrackStorageService
{
    private const string IndexKey = "pointedhorizons_tracks_index";
    private const string ActiveIdKey = "pointedhorizons_active_track_id";
    private const string TrackKeyPrefix = "pointedhorizons_track_";

    public static event EventHandler? TracksUpdated;

    /// <summary>
    /// Checks for and recovers any active track from a previous crashed/unexpectedly closed session.
    /// </summary>
    public static async Task<RecordedTrack?> CheckAndRecoverCrashedTrackAsync()
    {
        try
        {
            string? activeId = GetLocalStorageItem(ActiveIdKey);
            if (!string.IsNullOrEmpty(activeId))
            {
                var track = await GetTrackByIdAsync(activeId);
                if (track != null && track.IsActive)
                {
                    Debug.WriteLine($"[TrackStorageService] Recovering crashed track: {track.Id} ({track.Points.Count} pts)");
                    track.IsActive = false;
                    track.EndTime = track.Points.LastOrDefault()?.Time ?? track.StartTime;
                    if (!track.Name.Contains("(Recovered)", StringComparison.OrdinalIgnoreCase))
                    {
                        track.Name += " (Recovered)";
                    }

                    await SaveTrackAsync(track);
                    RemoveLocalStorageItem(ActiveIdKey);
                    TracksUpdated?.Invoke(null, EventArgs.Empty);
                    return track;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TrackStorageService] Crash recovery error: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Gets the list of all recorded tracks metadata, sorted newest first.
    /// </summary>
    public static async Task<List<TrackMetadata>> GetTracksIndexAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                string? json = GetLocalStorageItem(IndexKey);
                if (!string.IsNullOrEmpty(json))
                {
                    var list = JsonSerializer.Deserialize<List<TrackMetadata>>(json);
                    if (list != null)
                    {
                        return list.OrderByDescending(t => t.StartTime).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrackStorageService] GetTracksIndex error: {ex.Message}");
            }
            return new List<TrackMetadata>();
        });
    }

    /// <summary>
    /// Loads a full track with all points by ID.
    /// </summary>
    public static async Task<RecordedTrack?> GetTrackByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        return await Task.Run(() =>
        {
            try
            {
                string? json = GetLocalStorageItem(TrackKeyPrefix + id);
                if (!string.IsNullOrEmpty(json))
                {
                    return JsonSerializer.Deserialize<RecordedTrack>(json);
                }

                // Fallback to disk if localStorage was cleared
                string localPath = GetDiskFilePath(id);
                if (File.Exists(localPath))
                {
                    string diskJson = File.ReadAllText(localPath);
                    return JsonSerializer.Deserialize<RecordedTrack>(diskJson);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrackStorageService] GetTrackById error: {ex.Message}");
            }
            return null;
        });
    }

    /// <summary>
    /// Starts a new active track session and records it to storage.
    /// </summary>
    public static async Task<RecordedTrack> StartNewTrackAsync(string? customName = null)
    {
        string id = $"track_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        string name = string.IsNullOrWhiteSpace(customName)
            ? $"Track {DateTime.Now:yyyy-MM-dd HH-mm}"
            : customName;

        var track = new RecordedTrack
        {
            Id = id,
            Name = name,
            StartTime = DateTime.UtcNow,
            IsActive = true,
            TotalDistanceKm = 0,
            Points = new List<RecordedTrackPoint>()
        };

        SetLocalStorageItem(ActiveIdKey, id);
        await SaveTrackAsync(track);
        return track;
    }

    /// <summary>
    /// Appends a new point to an active track, saves to storage, and updates the index.
    /// </summary>
    public static async Task AppendPointAsync(RecordedTrack track, double lat, double lon, double? ele, double? acc)
    {
        if (track == null) return;

        var point = new RecordedTrackPoint
        {
            Lat = lat,
            Lon = lon,
            Ele = ele,
            Acc = acc,
            Time = DateTime.UtcNow
        };

        if (track.Points.Count > 0)
        {
            var prev = track.Points.Last();
            double dist = DistanceKm(prev.Lat, prev.Lon, lat, lon);
            track.TotalDistanceKm += dist;
        }

        track.Points.Add(point);

        // Save immediately for crash resilience
        await SaveTrackAsync(track);
    }

    /// <summary>
    /// Finishes an active track session and exports the final GPX file to the default GPX folder.
    /// </summary>
    public static async Task FinishTrackAsync(RecordedTrack track)
    {
        if (track == null) return;

        track.IsActive = false;
        track.EndTime = DateTime.UtcNow;
        await SaveTrackAsync(track);
        RemoveLocalStorageItem(ActiveIdKey);

        // Save into the designated GPX directory as requested
        try
        {
            if (track.Points.Count > 0 && OfflineDataService.IsPmTilesSupported)
            {
                string gpxDir = OfflineDataService.GpxDirectory;
                await TrackParserService.ExportRecordedTrackToGpxAsync(track, gpxDir);
                await TrackIndexService.RefreshAsync(forceFullRescan: true);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TrackStorageService] Export GPX on finish error: {ex.Message}");
        }

        TracksUpdated?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Checks all locally stored recorded tracks and exports any missing ones to the GPX directory on disk.
    /// This recovers any tracks previously recorded before GPX export or when file export was interrupted.
    /// </summary>
    public static async Task EnsureAllSavedTracksExportedToGpxAsync()
    {
        try
        {
            if (!OfflineDataService.IsPmTilesSupported) return;

            string gpxDir = OfflineDataService.GpxDirectory;
            if (!Directory.Exists(gpxDir))
            {
                Directory.CreateDirectory(gpxDir);
            }

            var index = await GetTracksIndexAsync();
            if (index == null || index.Count == 0) return;

            // Collect existing filenames across candidate directories to avoid duplicates
            var existingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dirs = OfflineDataService.GetGpxCandidateDirectories();
            foreach (var d in dirs)
            {
                try
                {
                    if (Directory.Exists(d))
                    {
                        foreach (var f in Directory.GetFiles(d, "*.gpx"))
                        {
                            existingFiles.Add(Path.GetFileNameWithoutExtension(f));
                        }
                    }
                }
                catch { }
            }

            foreach (var meta in index)
            {
                if (meta == null || meta.PointCount == 0) continue;

                // Check if already exported by matching date stamp or track ID
                string dateKey = meta.StartTime.ToString("yyyyMMdd");
                bool alreadyExists = existingFiles.Any(f => f.Contains(dateKey, StringComparison.OrdinalIgnoreCase) || f.Contains(meta.Id, StringComparison.OrdinalIgnoreCase));
                if (alreadyExists) continue;

                var fullTrack = await GetTrackByIdAsync(meta.Id);
                if (fullTrack != null && fullTrack.Points.Count > 0)
                {
                    Debug.WriteLine($"[TrackStorageService] Backfilling GPX export for saved track: {fullTrack.Name}");
                    await TrackParserService.ExportRecordedTrackToGpxAsync(fullTrack, gpxDir);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TrackStorageService] EnsureAllSavedTracksExportedToGpxAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// Deletes a track from localStorage and disk.
    /// </summary>
    public static async Task DeleteTrackAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        await Task.Run(() =>
        {
            try
            {
                RemoveLocalStorageItem(TrackKeyPrefix + id);

                string localPath = GetDiskFilePath(id);
                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
                }

                // Update index
                string? indexJson = GetLocalStorageItem(IndexKey);
                if (!string.IsNullOrEmpty(indexJson))
                {
                    var list = JsonSerializer.Deserialize<List<TrackMetadata>>(indexJson) ?? new();
                    list.RemoveAll(t => t.Id == id);
                    SetLocalStorageItem(IndexKey, JsonSerializer.Serialize(list));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrackStorageService] DeleteTrack error: {ex.Message}");
            }
        });

        TracksUpdated?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Persists the track to localStorage and local disk backup.
    /// </summary>
    public static async Task SaveTrackAsync(RecordedTrack track)
    {
        if (track == null || string.IsNullOrWhiteSpace(track.Id)) return;

        await Task.Run(() =>
        {
            try
            {
                string json = JsonSerializer.Serialize(track);
                SetLocalStorageItem(TrackKeyPrefix + track.Id, json);

                // Local disk backup if available
                try
                {
                    string diskPath = GetDiskFilePath(track.Id);
                    string? dir = Path.GetDirectoryName(diskPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.WriteAllText(diskPath, json);
                }
                catch { }

                // Update index
                string? indexJson = GetLocalStorageItem(IndexKey);
                var list = (!string.IsNullOrEmpty(indexJson))
                    ? JsonSerializer.Deserialize<List<TrackMetadata>>(indexJson) ?? new()
                    : new();

                var meta = list.FirstOrDefault(t => t.Id == track.Id);
                if (meta == null)
                {
                    meta = new TrackMetadata { Id = track.Id };
                    list.Add(meta);
                }

                meta.Name = track.Name;
                meta.StartTime = track.StartTime;
                meta.EndTime = track.EndTime;
                meta.IsActive = track.IsActive;
                meta.TotalDistanceKm = track.TotalDistanceKm;
                meta.PointCount = track.Points.Count;

                SetLocalStorageItem(IndexKey, JsonSerializer.Serialize(list));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrackStorageService] SaveTrack error: {ex.Message}");
            }
        });
    }

    private static string GetDiskFilePath(string trackId)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(appData))
            {
                return Path.Combine(appData, "PointedHorizons", "Tracks", $"{trackId}.json");
            }
        }
        catch { }
        return $"Tracks_{trackId}.json";
    }

    private static string? GetLocalStorageItem(string key)
    {
        try
        {
            object? val = Interop.ExecuteJavaScript("localStorage.getItem($0)", key);
            return val?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static void SetLocalStorageItem(string key, string value)
    {
        try
        {
            Interop.ExecuteJavaScriptVoid("localStorage.setItem($0, $1)", key, value);
        }
        catch { }
    }

    private static void RemoveLocalStorageItem(string key)
    {
        try
        {
            Interop.ExecuteJavaScriptVoid("localStorage.removeItem($0)", key);
        }
        catch { }
    }

    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
}

