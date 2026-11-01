using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using OpenSilver;

namespace PointedHorizons.Services;

/// <summary>
/// Configuration for a layered PMTiles file.
/// </summary>
public class PmTileLayerConfig
{
    public string FileName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public double Opacity { get; set; } = 1.0;
    public int Order { get; set; } = 0;
}

/// <summary>
/// Information about a discovered PMTiles file on disk.
/// </summary>
public class PmTileFileInfo
{
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime LastModified { get; set; }
    public bool IsValidArchive { get; set; } = true;
    public string? ArchiveFormatError { get; set; }

    public string FormattedSize
    {
        get
        {
            if (SizeBytes >= 1024 * 1024 * 1024)
                return $"{(SizeBytes / (1024.0 * 1024.0 * 1024.0)):F2} GB";
            if (SizeBytes >= 1024 * 1024)
                return $"{(SizeBytes / (1024.0 * 1024.0)):F1} MB";
            if (SizeBytes >= 1024)
                return $"{(SizeBytes / 1024.0):F0} KB";
            return $"{SizeBytes} B";
        }
    }
}

/// <summary>
/// Coordinates offline PMTiles directories and local DEM (.hgt) caching and discovery
/// across Photino desktop and Android.
/// </summary>
public static class OfflineDataService
{
    private const string SettingsStorageKey = "pointedhorizons_offline_settings_v1";

    public const string DefaultPhotinoPmTilesDir = @"M:\PointlessWaymarksPublications\pmtiles";
    public const string DefaultPhotinoDemDir = @"M:\PointlessWaymarksPublications\dem";

    private static string? _customPmTilesDir;
    private static string? _customDemDir;
    private static string? _androidBaseDir;
    private static string? _selectedStyleFile;
    private static List<PmTileLayerConfig>? _cachedLayers;

    public static event EventHandler? ConfigChanged;

    /// <summary>
    /// Gets whether running on Android platform.
    /// </summary>
    public static bool IsAndroid => OperatingSystem.IsAndroid();

    /// <summary>
    /// Gets whether path configuration is allowed (enabled on desktop, disabled on Android).
    /// </summary>
    public static bool CanConfigurePaths => !IsAndroid;

    /// <summary>
    /// Gets or sets the selected style JSON file name (located in the PMTiles directory).
    /// Null indicates default styling.
    /// </summary>
    public static string? SelectedStyleFile
    {
        get => _selectedStyleFile;
        set => SetSelectedStyleFile(value);
    }

    /// <summary>
    /// Sets the selected style file and persists to local storage.
    /// </summary>
    public static void SetSelectedStyleFile(string? fileName)
    {
        string? normalized = string.IsNullOrWhiteSpace(fileName) ? null : fileName.Trim();
        if (normalized != null && normalized.StartsWith("(", StringComparison.Ordinal))
        {
            normalized = null;
        }

        if (!string.Equals(_selectedStyleFile, normalized, StringComparison.OrdinalIgnoreCase))
        {
            _selectedStyleFile = normalized;
            try
            {
                SetLocalStorageItem(SettingsStorageKey + "_style", _selectedStyleFile ?? "");
            }
            catch { }

            ConfigChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Sets the Android base directory (e.g. from MauiProgram or MainActivity).
    /// </summary>
    public static void SetAndroidBaseDirectory(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            _androidBaseDir = path;
            EnsureDirectoriesExist();
        }
    }

    /// <summary>
    /// Gets the active PMTiles directory.
    /// </summary>
    public static string PmTilesDirectory
    {
        get
        {
            if (IsAndroid)
            {
                string baseDir = GetAndroidBaseDirectory();
                return Path.Combine(baseDir, "pmtiles");
            }

            if (!string.IsNullOrWhiteSpace(_customPmTilesDir))
            {
                return _customPmTilesDir;
            }

            return DefaultPhotinoPmTilesDir;
        }
    }

    /// <summary>
    /// Gets the active DEM (.hgt) directory.
    /// </summary>
    public static string DemDirectory
    {
        get
        {
            if (IsAndroid)
            {
                string baseDir = GetAndroidBaseDirectory();
                return Path.Combine(baseDir, "dem");
            }

            if (!string.IsNullOrWhiteSpace(_customDemDir))
            {
                return _customDemDir;
            }

            return DefaultPhotinoDemDir;
        }
    }

    /// <summary>
    /// Initializes directories and loads persistent offline settings.
    /// </summary>
    public static void Initialize()
    {
        LoadSettings();
        EnsureDirectoriesExist();
    }

    /// <summary>
    /// Ensures both pmtiles and dem directories exist if possible.
    /// </summary>
    public static void EnsureDirectoriesExist()
    {
        try
        {
            string pmtilesDir = PmTilesDirectory;
            if (!Directory.Exists(pmtilesDir))
            {
                Directory.CreateDirectory(pmtilesDir);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OfflineDataService] Ensure PMTiles dir error: {ex.Message}");
        }

        try
        {
            string demDir = DemDirectory;
            if (!Directory.Exists(demDir))
            {
                Directory.CreateDirectory(demDir);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OfflineDataService] Ensure DEM dir error: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates custom paths (desktop only).
    /// </summary>
    public static void SetCustomPaths(string? pmtilesDir, string? demDir)
    {
        if (!CanConfigurePaths) return;

        _customPmTilesDir = string.IsNullOrWhiteSpace(pmtilesDir) ? null : pmtilesDir.Trim();
        _customDemDir = string.IsNullOrWhiteSpace(demDir) ? null : demDir.Trim();

        SaveSettings();
        EnsureDirectoriesExist();
        ConfigChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Resets paths to platform defaults.
    /// </summary>
    public static void ResetPathsToDefault()
    {
        if (!CanConfigurePaths) return;

        _customPmTilesDir = null;
        _customDemDir = null;

        SaveSettings();
        EnsureDirectoriesExist();
        ConfigChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Scans the PMTiles directory for all available *.pmtiles files.
    /// </summary>
    public static List<PmTileFileInfo> GetAvailablePmTileFiles()
    {
        var result = new List<PmTileFileInfo>();
        try
        {
            string dir = PmTilesDirectory;
            if (Directory.Exists(dir))
            {
                var dirInfo = new DirectoryInfo(dir);
                foreach (var file in dirInfo.GetFiles("*.pmtiles"))
                {
                    bool isValidArchive = false;
                    string? formatError = null;
                    try
                    {
                        using var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        byte[] header = new byte[8];
                        int bytesRead = fs.Read(header, 0, 8);
                        if (bytesRead >= 7 &&
                            header[0] == 0x50 && header[1] == 0x4D && header[2] == 0x54 &&
                            header[3] == 0x69 && header[4] == 0x6C && header[5] == 0x65 && header[6] == 0x73)
                        {
                            isValidArchive = true;
                        }
                        else if (bytesRead >= 6 && header[0] == 0x53 && header[1] == 0x51 && header[2] == 0x4C && header[3] == 0x69)
                        {
                            formatError = "MBTiles (SQLite) format - must be converted to PMTiles";
                        }
                        else
                        {
                            formatError = "Invalid PMTiles magic bytes";
                        }
                    }
                    catch (Exception ex)
                    {
                        formatError = ex.Message;
                    }

                    result.Add(new PmTileFileInfo
                    {
                        FileName = file.Name,
                        FullPath = file.FullName,
                        SizeBytes = file.Length,
                        LastModified = file.LastWriteTime,
                        IsValidArchive = isValidArchive,
                        ArchiveFormatError = formatError
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OfflineDataService] GetAvailablePmTileFiles error: {ex.Message}");
        }

        return result.OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Gets the current user configuration for layered PMTiles.
    /// Synchronizes with currently available files on disk.
    /// </summary>
    public static List<PmTileLayerConfig> GetConfiguredLayers()
    {
        if (_cachedLayers != null) return _cachedLayers;

        var available = GetAvailablePmTileFiles();
        var layers = new List<PmTileLayerConfig>();

        // Load saved configuration from storage
        try
        {
            string? json = GetLocalStorageItem(SettingsStorageKey + "_layers");
            if (!string.IsNullOrEmpty(json))
            {
                var saved = JsonSerializer.Deserialize<List<PmTileLayerConfig>>(json);
                if (saved != null)
                {
                    layers = saved;
                }
            }
        }
        catch { }

        // Remove layers for files that no longer exist
        layers.RemoveAll(l => !available.Any(a => string.Equals(a.FileName, l.FileName, StringComparison.OrdinalIgnoreCase)));

        // Add any newly discovered PMTiles files that weren't previously saved (disabled by default)
        int orderIndex = layers.Count > 0 ? layers.Max(l => l.Order) + 1 : 0;
        foreach (var file in available)
        {
            if (!layers.Any(l => string.Equals(l.FileName, file.FileName, StringComparison.OrdinalIgnoreCase)))
            {
                layers.Add(new PmTileLayerConfig
                {
                    FileName = file.FileName,
                    IsEnabled = layers.Count == 0, // Auto-enable first file if list was empty
                    Opacity = 1.0,
                    Order = orderIndex++
                });
            }
        }

        _cachedLayers = layers.OrderBy(l => l.Order).ToList();
        return _cachedLayers;
    }

    /// <summary>
    /// Saves the PMTiles layer configurations.
    /// </summary>
    public static void SaveConfiguredLayers(List<PmTileLayerConfig> layers)
    {
        _cachedLayers = layers.OrderBy(l => l.Order).ToList();
        try
        {
            string json = JsonSerializer.Serialize(_cachedLayers);
            SetLocalStorageItem(SettingsStorageKey + "_layers", json);
        }
        catch { }

        ConfigChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Scans the PMTiles directory for all available *.json style files.
    /// </summary>
    public static List<string> GetAvailableStyleFiles()
    {
        var result = new List<string>();
        try
        {
            string dir = PmTilesDirectory;
            if (Directory.Exists(dir))
            {
                var dirInfo = new DirectoryInfo(dir);
                foreach (var file in dirInfo.GetFiles("*.json"))
                {
                    result.Add(file.Name);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OfflineDataService] GetAvailableStyleFiles error: {ex.Message}");
        }

        return result.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Reads and returns the content of the currently selected style JSON file, if any.
    /// </summary>
    public static string? GetSelectedStyleJson()
    {
        if (string.IsNullOrWhiteSpace(_selectedStyleFile)) return null;

        try
        {
            string dir = PmTilesDirectory;
            string path = Path.Combine(dir, _selectedStyleFile);
            if (File.Exists(path))
            {
                string text = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.Trim();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OfflineDataService] GetSelectedStyleJson error: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Builds a full configuration payload for the JavaScript map runtime,
    /// including enabled PMTiles layers, selected style filename, style JSON content,
    /// and local tile server base URL.
    /// </summary>
    public static object GetPmTilesConfigPayload()
    {
        var layers = GetConfiguredLayers().Where(l => l.IsEnabled).OrderBy(l => l.Order).Select(l => new
        {
            fileName = l.FileName,
            url = LocalTileServer.GetPmTileUrl(l.FileName),
            opacity = l.Opacity,
            isEnabled = l.IsEnabled,
            order = l.Order
        }).ToList();

        string? styleJson = GetSelectedStyleJson();

        return new
        {
            layers = layers,
            styleFileName = _selectedStyleFile,
            styleUrl = !string.IsNullOrEmpty(_selectedStyleFile) ? LocalTileServer.GetPmTileUrl(_selectedStyleFile) : null,
            styleJson = styleJson,
            tileServerBase = LocalTileServer.GetBaseUrl()
        };
    }

    /// <summary>
    /// Checks for a local DEM file matching tileKey in the DEM directory (.hgt, .zip, .hgt.zip).
    /// </summary>
    public static string? FindLocalDemFile(string tileKey)
    {
        if (string.IsNullOrWhiteSpace(tileKey)) return null;
        string dir = DemDirectory;
        if (!Directory.Exists(dir)) return null;

        try
        {
            string[] candidateExtensions = [".hgt", ".zip", ".hgt.zip"];
            foreach (var ext in candidateExtensions)
            {
                string path = Path.Combine(dir, $"{tileKey}{ext}");
                if (File.Exists(path)) return path;

                // Also check lowercase
                string lowerPath = Path.Combine(dir, $"{tileKey.ToLowerInvariant()}{ext}");
                if (File.Exists(lowerPath)) return lowerPath;
            }

            // Search directory for prefix match (e.g. N32W111.SRTMGL1.hgt.zip)
            var dirInfo = new DirectoryInfo(dir);
            var match = dirInfo.GetFiles($"{tileKey}*")
                .FirstOrDefault(f => f.Name.EndsWith(".hgt", StringComparison.OrdinalIgnoreCase) ||
                                     f.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return match.FullName;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OfflineDataService] FindLocalDemFile error: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Caches downloaded DEM bytes to the local DEM folder.
    /// </summary>
    public static async Task SaveDownloadedDemAsync(string tileKey, byte[] hgtBytes)
    {
        if (string.IsNullOrWhiteSpace(tileKey) || hgtBytes == null || hgtBytes.Length == 0) return;

        try
        {
            string dir = DemDirectory;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string savePath = Path.Combine(dir, $"{tileKey.ToUpperInvariant()}.hgt");
            if (!File.Exists(savePath))
            {
                await File.WriteAllBytesAsync(savePath, hgtBytes);
                Debug.WriteLine($"[OfflineDataService] Cached downloaded DEM to: {savePath}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OfflineDataService] Failed to cache downloaded DEM: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the count of DEM files present in the local DEM directory.
    /// </summary>
    public static int GetLocalDemFileCount()
    {
        try
        {
            string dir = DemDirectory;
            if (Directory.Exists(dir))
            {
                var dirInfo = new DirectoryInfo(dir);
                return dirInfo.GetFiles("*.hgt").Length + dirInfo.GetFiles("*.zip").Length;
            }
        }
        catch { }
        return 0;
    }

    private static string GetAndroidBaseDirectory()
    {
        if (!string.IsNullOrEmpty(_androidBaseDir)) return _androidBaseDir;

        // Try standard user documents folder
        try
        {
            string myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrEmpty(myDocs))
            {
                string target = Path.Combine(myDocs, "PointedHorizons");
                if (!Directory.Exists(target)) Directory.CreateDirectory(target);
                _androidBaseDir = target;
                return target;
            }
        }
        catch { }

        // Fallback to local app data
        string fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PointedHorizons");
        _androidBaseDir = fallback;
        return fallback;
    }

    private static void LoadSettings()
    {
        // Load style selection on all platforms (Photino, Browser, Android)
        try
        {
            string? savedStyle = GetLocalStorageItem(SettingsStorageKey + "_style");
            if (!string.IsNullOrWhiteSpace(savedStyle))
            {
                _selectedStyleFile = savedStyle.Trim();
            }
        }
        catch { }

        if (IsAndroid) return;

        try
        {
            string? json = GetLocalStorageItem(SettingsStorageKey);
            if (!string.IsNullOrEmpty(json))
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("pmtilesDir", out var pProp) && pProp.ValueKind == JsonValueKind.String)
                {
                    _customPmTilesDir = pProp.GetString();
                }
                if (root.TryGetProperty("demDir", out var dProp) && dProp.ValueKind == JsonValueKind.String)
                {
                    _customDemDir = dProp.GetString();
                }
            }
        }
        catch { }
    }

    private static void SaveSettings()
    {
        if (IsAndroid) return;

        try
        {
            var data = new Dictionary<string, string?>
            {
                ["pmtilesDir"] = _customPmTilesDir,
                ["demDir"] = _customDemDir
            };
            string json = JsonSerializer.Serialize(data);
            SetLocalStorageItem(SettingsStorageKey, json);
        }
        catch { }
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
}
