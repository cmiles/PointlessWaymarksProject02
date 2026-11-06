#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PointedHorizons.Services
{
    public class SoftwareReleaseEntry
    {
        public string? ApplicationName { get; set; }
        public string? InstallerFileName { get; set; }
        public string? InstallerFileSha1 { get; set; }
        public string? InstallerBaseName { get; set; }
        public string? ApplicationVersion { get; set; }
        public string? InstallerUrl { get; set; }
    }

    public class InstalledVersionInfo
    {
        public string? VersionString { get; set; }
        public DateTime? VersionDate { get; set; }
        public string? FoundFilePath { get; set; }
        public string? FoundDirectory { get; set; }
        public List<string> ProbedDirectories { get; set; } = new();
        public string SearchPattern { get; set; } = "PublishVersion--*.txt";
    }

    public class AppUpdateCheckResult
    {
        public bool HasUpdate { get; set; }
        public bool IsAndroid { get; set; }
        public bool IsBrowser { get; set; }
        public string? InstalledVersionString { get; set; }
        public DateTime? InstalledVersionDate { get; set; }
        public string? FoundVersionFilePath { get; set; }
        public string? FoundDirectory { get; set; }
        public List<string> ProbedDirectories { get; set; } = new();
        public string SearchPattern { get; set; } = "PublishVersion--*.txt";
        public SoftwareReleaseEntry? AvailableRelease { get; set; }
        public DateTime? RemoteVersionDate { get; set; }
        public string? Message { get; set; }
        public bool IsDevelopmentOrPortableMode { get; set; }
        public string? DownloadedFilePath { get; set; }
    }

    public static class AppUpdateService
    {
        public const string SoftwareListUrl = "https://software.pointlesswaymarks.com/Software/PointlessWaymarksSoftwareList.json";

        public const string WindowsApplicationName = "Pointless Waymarks-Pointed Horizons Win-Setup";
        public const string WindowsInstallerBaseName = "PointlessWaymarks-PointedHorizonsWin-Setup";

        public const string AndroidApplicationName = "com.pointlesswaymarks.pointedhorizons-Signed";
        public const string AndroidInstallerBaseName = "com.pointlesswaymarks.pointedhorizons-Signed";

        public const string VersionSearchPattern = "PublishVersion--*.txt";

        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        public static bool IsAndroidPlatform => OfflineDataService.IsAndroid;

        public static string TargetApplicationName => IsAndroidPlatform ? AndroidApplicationName : WindowsApplicationName;
        public static string TargetInstallerBaseName => IsAndroidPlatform ? AndroidInstallerBaseName : WindowsInstallerBaseName;

        /// <summary>
        /// Platform-specific downloader handler for Android (e.g. to save to MediaStore public Downloads).
        /// Returns the resolved local or content path to the saved APK.
        /// </summary>
        public static Func<SoftwareReleaseEntry, IProgress<(long bytesDownloaded, long totalBytes, double percent)>?, Action<string>?, CancellationToken, Task<string>>? AndroidDownloader { get; set; }

        /// <summary>
        /// Collects candidate directories where the application binary and installer marker files may reside.
        /// </summary>
        public static List<string> GetProbeDirectories()
        {
            var dirs = new List<string>();

            void TryAddDir(string? rawPath)
            {
                if (string.IsNullOrWhiteSpace(rawPath)) return;
                try
                {
                    string path = rawPath.Trim('"', ' ');
                    string? dir = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
                    if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir) && !dirs.Contains(dir, StringComparer.OrdinalIgnoreCase))
                    {
                        dirs.Add(dir);
                    }
                }
                catch { }
            }

            // 1. Process Path
            try
            {
                TryAddDir(Environment.ProcessPath);
            }
            catch { }

            // 2. MainModule FileName fallback
            try
            {
                using var proc = Process.GetCurrentProcess();
                TryAddDir(proc.MainModule?.FileName);
            }
            catch { }

            // 3. AppContext.BaseDirectory and subfolders
            try
            {
                TryAddDir(AppContext.BaseDirectory);
                TryAddDir(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
            }
            catch { }

            // 4. AppDomain.CurrentDomain.BaseDirectory
            try
            {
                TryAddDir(AppDomain.CurrentDomain.BaseDirectory);
            }
            catch { }

            // 5. Current Working Directory
            try
            {
                TryAddDir(Directory.GetCurrentDirectory());
            }
            catch { }

            // 6. Platform-specific user documents folders
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrEmpty(docs))
                {
                    TryAddDir(Path.Combine(docs, "PointedHorizons"));
                }
            }
            catch { }

            try
            {
                string personal = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                if (!string.IsNullOrEmpty(personal))
                {
                    TryAddDir(Path.Combine(personal, "PointedHorizons"));
                }
            }
            catch { }

            return dirs;
        }

        private static readonly string[] DateFormats = new[]
        {
            "yyyy-MM-dd-HH-mm",
            "yyyy-MM-dd-HH-mm-ss",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH-mm-ss",
            "yyyy-MM-dd"
        };

        /// <summary>
        /// Reads the installed PublishVersion--*.txt file by probing candidate application directories,
        /// falling back to assembly informational version on mobile/embedded environments.
        /// </summary>
        public static InstalledVersionInfo GetInstalledVersion()
        {
            var info = new InstalledVersionInfo
            {
                SearchPattern = VersionSearchPattern,
                ProbedDirectories = GetProbeDirectories()
            };

            foreach (var dir in info.ProbedDirectories)
            {
                try
                {
                    var files = Directory.GetFiles(dir, VersionSearchPattern);
                    if (files != null && files.Length > 0)
                    {
                        var newest = files.OrderByDescending(f => f).First();
                        info.FoundFilePath = newest;
                        info.FoundDirectory = dir;

                        var rawName = Path.GetFileNameWithoutExtension(newest);
                        var stamp = rawName.StartsWith("PublishVersion--", StringComparison.OrdinalIgnoreCase)
                            ? rawName.Substring("PublishVersion--".Length).Trim()
                            : rawName.Trim();
                        info.VersionString = stamp;

                        if (DateTime.TryParseExact(stamp, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                        {
                            info.VersionDate = dt;
                        }
                        else if (DateTime.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                        {
                            info.VersionDate = dt;
                        }
                        else
                        {
                            try
                            {
                                var content = File.ReadAllText(newest).Trim();
                                if (!string.IsNullOrEmpty(content))
                                {
                                    if (DateTime.TryParseExact(content, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt) ||
                                        DateTime.TryParse(content, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                                    {
                                        info.VersionDate = dt;
                                        info.VersionString = content;
                                    }
                                }
                            }
                            catch { }
                        }

                        if (info.VersionDate.HasValue)
                        {
                            return info;
                        }
                    }
                }
                catch { }
            }

            // Fallback: Check assembly informational / version attributes (useful for Android & Blazor)
            try
            {
                var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (!string.IsNullOrWhiteSpace(infoVer))
                {
                    // Clean up commit hash if present: "2026-11-04-12-00+abc" -> "2026-11-04-12-00"
                    int plusIdx = infoVer.IndexOf('+');
                    string rawVer = plusIdx > 0 ? infoVer.Substring(0, plusIdx).Trim() : infoVer.Trim();

                    if (DateTime.TryParseExact(rawVer, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDt) ||
                        DateTime.TryParse(rawVer, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDt))
                    {
                        info.VersionString = rawVer;
                        info.VersionDate = parsedDt;
                        return info;
                    }
                }
            }
            catch { }

            return info;
        }

        public static DateTime? ParseRemoteVersion(string? rawVersion)
        {
            if (string.IsNullOrWhiteSpace(rawVersion)) return null;

            if (DateTime.TryParse(rawVersion, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return dt;
            }

            if (DateTime.TryParseExact(rawVersion, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            {
                return dt;
            }

            return null;
        }

        /// <summary>
        /// Checks the remote software list for newer releases of PointedHorizons.
        /// </summary>
        public static async Task<AppUpdateCheckResult> CheckForUpdatesAsync()
        {
            bool isBrowser = OperatingSystem.IsBrowser();
            bool isAndroid = IsAndroidPlatform;

            var installed = GetInstalledVersion();
            string probeSummary = installed.ProbedDirectories.Count > 0
                ? string.Join("; ", installed.ProbedDirectories)
                : "(none)";

            bool isDevMode = string.IsNullOrEmpty(installed.VersionString) || !installed.VersionDate.HasValue;

            var result = new AppUpdateCheckResult
            {
                IsAndroid = isAndroid,
                IsBrowser = isBrowser,
                InstalledVersionString = installed.VersionString,
                InstalledVersionDate = installed.VersionDate,
                FoundVersionFilePath = installed.FoundFilePath,
                FoundDirectory = installed.FoundDirectory,
                ProbedDirectories = installed.ProbedDirectories,
                SearchPattern = installed.SearchPattern,
                IsDevelopmentOrPortableMode = isDevMode
            };

            if (isBrowser)
            {
                result.HasUpdate = false;
                result.Message = "Pointed Horizons Web runs live in the browser and updates automatically on page reload.";
                return result;
            }

            try
            {
                string json = await _httpClient.GetStringAsync(SoftwareListUrl);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var releases = JsonSerializer.Deserialize<List<SoftwareReleaseEntry>>(json, options);

                if (releases == null || releases.Count == 0)
                {
                    result.HasUpdate = false;
                    result.Message = isDevMode
                        ? $"No version marker matching '{installed.SearchPattern}' found in probed directories: [{probeSummary}]. Could not retrieve releases from update catalog."
                        : "Could not retrieve software releases list from server.";
                    return result;
                }

                string targetApp = TargetApplicationName;
                string targetBase = TargetInstallerBaseName;

                var matchingReleases = releases
                    .Where(r => string.Equals(r.ApplicationName, targetApp, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(r.InstallerBaseName, targetBase, StringComparison.OrdinalIgnoreCase))
                    .Select(r => new { Release = r, ParsedDate = ParseRemoteVersion(r.ApplicationVersion) })
                    .Where(x => x.ParsedDate.HasValue)
                    .OrderByDescending(x => x.ParsedDate!.Value)
                    .ToList();

                var latest = matchingReleases.FirstOrDefault();
                if (latest == null)
                {
                    result.HasUpdate = false;
                    result.Message = isDevMode
                        ? $"No version marker matching '{installed.SearchPattern}' found in probed directories: [{probeSummary}]. No compatible releases found in the online catalog for {targetApp}."
                        : $"No compatible releases found in the online catalog for {targetApp}.";
                    return result;
                }

                result.AvailableRelease = latest.Release;
                result.RemoteVersionDate = latest.ParsedDate!.Value;

                if (isDevMode)
                {
                    result.HasUpdate = false;
                    result.Message = $"No version marker matching '{installed.SearchPattern}' found in probed directories: [{probeSummary}]. Latest online version is {latest.Release.ApplicationVersion}.";
                }
                else if (latest.ParsedDate!.Value > installed.VersionDate!.Value)
                {
                    result.HasUpdate = true;
                    result.Message = $"Update available: {latest.Release.InstallerFileName}";
                }
                else
                {
                    result.HasUpdate = false;
                    result.Message = $"You are using the latest version ({installed.VersionString}).";
                }

                return result;
            }
            catch (Exception ex)
            {
                result.HasUpdate = false;
                result.Message = isDevMode
                    ? $"No version marker matching '{installed.SearchPattern}' found in probed directories: [{probeSummary}]. Error contacting update catalog: {ex.Message}"
                    : $"Error checking for updates: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Finds or creates a writable download directory on Android.
        /// Prioritizes the public Downloads directory, followed by the folder beside DEM and PMTiles.
        /// </summary>
        public static string GetAndroidDownloadDirectory()
        {
            var candidates = new List<string>();

            // 1. Standard Android public Downloads directory
            candidates.Add("/storage/emulated/0/Download");
            candidates.Add("/sdcard/Download");

            // 2. Beside the DEM and PMTiles folder (OfflineDataService base folder)
            try
            {
                string baseDir = OfflineDataService.AndroidBaseDirectory;
                if (!string.IsNullOrWhiteSpace(baseDir))
                {
                    candidates.Add(baseDir);
                }
            }
            catch { }

            // 3. Parent directory of DemDirectory or PmTilesDirectory
            try
            {
                string demDir = OfflineDataService.DemDirectory;
                if (!string.IsNullOrWhiteSpace(demDir))
                {
                    string? parent = Path.GetDirectoryName(demDir);
                    if (!string.IsNullOrWhiteSpace(parent))
                    {
                        candidates.Add(parent);
                    }
                }
            }
            catch { }

            // 4. Standard public Documents/PointedHorizons
            candidates.Add("/storage/emulated/0/Documents/PointedHorizons");
            candidates.Add("/sdcard/Documents/PointedHorizons");

            // 5. User profile download folder
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(userProfile))
                {
                    candidates.Add(Path.Combine(userProfile, "Download"));
                }
            }
            catch { }

            // 6. PointedHorizons App Documents directory
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrEmpty(docs))
                {
                    candidates.Add(Path.Combine(docs, "PointedHorizons", "Updates"));
                    candidates.Add(Path.Combine(docs, "PointedHorizons"));
                }
            }
            catch { }

            // 7. Temp fallback
            candidates.Add(Path.Combine(Path.GetTempPath(), "PointedHorizonsUpdates"));

            foreach (var path in candidates)
            {
                try
                {
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }
                    // Test writability
                    string testFile = Path.Combine(path, $".test_write_{Guid.NewGuid():N}.tmp");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                    return path;
                }
                catch { }
            }

            return Path.GetTempPath();
        }

        /// <summary>
        /// Downloads the update package with progress reporting, verifies SHA-1,
        /// and either launches the installer (Windows) or saves the APK and attempts launch (Android).
        /// Returns the full local path to the downloaded installer/APK.
        /// </summary>
        public static async Task<string> DownloadAndExecuteInstallerAsync(
            SoftwareReleaseEntry release,
            IProgress<(long bytesDownloaded, long totalBytes, double percent)>? progress = null,
            Action<string>? statusCallback = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(release.InstallerUrl))
            {
                throw new InvalidOperationException("Installer URL is missing from the release entry.");
            }

            bool isAndroid = IsAndroidPlatform;

            // Delegate to platform downloader on Android when available (handles MediaStore public Downloads)
            if (isAndroid && AndroidDownloader != null)
            {
                return await AndroidDownloader(release, progress, statusCallback, cancellationToken);
            }

            string? fileName = release.InstallerFileName;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                try
                {
                    fileName = Path.GetFileName(new Uri(release.InstallerUrl).LocalPath);
                }
                catch { }
            }
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = isAndroid
                    ? "PointedHorizons-Signed.apk"
                    : "PointlessWaymarks-PointedHorizonsWin-Setup.exe";
            }

            string targetFilePath;
            if (isAndroid)
            {
                string downloadDir = GetAndroidDownloadDirectory();
                targetFilePath = Path.Combine(downloadDir, fileName);
            }
            else
            {
                targetFilePath = Path.Combine(Path.GetTempPath(), fileName);
            }

            statusCallback?.Invoke("Connecting to update server...");

            using (var response = await _httpClient.GetAsync(release.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? -1L;
                statusCallback?.Invoke(totalBytes > 0 
                    ? $"Downloading package ({totalBytes / (1024.0 * 1024.0):F1} MB)..." 
                    : "Downloading package...");

                using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                using (var fileStream = new FileStream(targetFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    byte[] buffer = new byte[81920];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        totalRead += bytesRead;

                        double percent = totalBytes > 0 ? ((double)totalRead / totalBytes * 100.0) : 0.0;
                        progress?.Report((totalRead, totalBytes, percent));
                    }

                    // Checksum validation
                    if (!string.IsNullOrWhiteSpace(release.InstallerFileSha1))
                    {
                        statusCallback?.Invoke("Verifying package integrity (SHA-1)...");
                        fileStream.Position = 0;
                        using var sha1 = SHA1.Create();
                        byte[] hash = sha1.ComputeHash(fileStream);
                        string calculatedHex = BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
                        string expectedHex = release.InstallerFileSha1.Replace("-", "").Trim().ToUpperInvariant();

                        if (!string.Equals(calculatedHex, expectedHex, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException(
                                $"Package checksum verification failed.\nExpected: {expectedHex}\nComputed: {calculatedHex}");
                        }
                    }
                }
            }

            if (isAndroid)
            {
                statusCallback?.Invoke($"Downloaded APK to: {targetFilePath}");

                // Attempt to launch installer via standard file invocation
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = targetFilePath,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Update] Android direct APK start note: {ex.Message}");
                }

                return targetFilePath;
            }
            else
            {
                statusCallback?.Invoke("Launching installer...");

                var psi = new ProcessStartInfo
                {
                    FileName = targetFilePath,
                    UseShellExecute = true
                };

                Process.Start(psi);

                // Give a short delay for installer process start, then exit so files update without lock
                await Task.Delay(500);
                Environment.Exit(0);
                return targetFilePath;
            }
        }
    }
}

