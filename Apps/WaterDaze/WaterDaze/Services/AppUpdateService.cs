#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WaterDaze.Services
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
    }

    public static class AppUpdateService
    {
        public const string SoftwareListUrl = "https://software.pointlesswaymarks.com/Software/PointlessWaymarksSoftwareList.json";
        public const string TargetApplicationName = "Pointless Waymarks-Water Daze Win-Setup";
        public const string TargetInstallerBaseName = "PointlessWaymarks-WaterDazeWin-Setup";
        public const string VersionSearchPattern = "PublishVersion--*.txt";

        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        /// <summary>
        /// Collects all candidate directories where the application binary and installer marker files may reside.
        /// Prioritizes Environment.ProcessPath to correctly locate the install folder in single-file self-extracting executables.
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

            // 1. Process Path (essential for .NET single-file self-extracting executables where BaseDirectory points to %TEMP%\.net)
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

            // 3. AppContext.BaseDirectory
            try
            {
                TryAddDir(AppContext.BaseDirectory);
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

            return dirs;
        }

        /// <summary>
        /// Reads the installed PublishVersion--*.txt file by probing candidate application directories.
        /// Returns an InstalledVersionInfo detailing probed directories, match results, and parsed date.
        /// </summary>
        public static InstalledVersionInfo GetInstalledVersion()
        {
            var info = new InstalledVersionInfo
            {
                SearchPattern = VersionSearchPattern,
                ProbedDirectories = GetProbeDirectories()
            };

            string[] dateFormats = new[]
            {
                "yyyy-MM-dd-HH-mm",
                "yyyy-MM-dd-HH-mm-ss",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-ddTHH-mm-ss",
                "yyyy-MM-dd"
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

                        if (DateTime.TryParseExact(stamp, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                        {
                            info.VersionDate = dt;
                        }
                        else if (DateTime.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                        {
                            info.VersionDate = dt;
                        }
                        else
                        {
                            // If timestamp in filename failed to parse, check if file content contains the version string
                            try
                            {
                                var content = File.ReadAllText(newest).Trim();
                                if (!string.IsNullOrEmpty(content))
                                {
                                    if (DateTime.TryParseExact(content, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                                    {
                                        info.VersionDate = dt;
                                        info.VersionString = content;
                                    }
                                    else if (DateTime.TryParse(content, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                                    {
                                        info.VersionDate = dt;
                                        info.VersionString = content;
                                    }
                                }
                            }
                            catch { }
                        }

                        break;
                    }
                }
                catch
                {
                    // Continue to next probe directory
                }
            }

            return info;
        }

        public static DateTime? ParseRemoteVersion(string? rawVersion)
        {
            if (string.IsNullOrWhiteSpace(rawVersion)) return null;

            if (DateTime.TryParse(rawVersion, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return dt;
            }

            if (DateTime.TryParseExact(rawVersion, "yyyy-MM-dd-HH-mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            {
                return dt;
            }

            return null;
        }

        /// <summary>
        /// Checks the remote software list for newer releases of WaterDaze.
        /// </summary>
        public static async Task<AppUpdateCheckResult> CheckForUpdatesAsync()
        {
            var installed = GetInstalledVersion();
            string probeSummary = installed.ProbedDirectories.Count > 0
                ? string.Join("; ", installed.ProbedDirectories)
                : "(none)";

            bool isDevMode = string.IsNullOrEmpty(installed.VersionString) || !installed.VersionDate.HasValue;

            var result = new AppUpdateCheckResult
            {
                InstalledVersionString = installed.VersionString,
                InstalledVersionDate = installed.VersionDate,
                FoundVersionFilePath = installed.FoundFilePath,
                FoundDirectory = installed.FoundDirectory,
                ProbedDirectories = installed.ProbedDirectories,
                SearchPattern = installed.SearchPattern,
                IsDevelopmentOrPortableMode = isDevMode
            };

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

                var matchingReleases = releases
                    .Where(r => string.Equals(r.ApplicationName, TargetApplicationName, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(r.InstallerBaseName, TargetInstallerBaseName, StringComparison.OrdinalIgnoreCase))
                    .Select(r => new { Release = r, ParsedDate = ParseRemoteVersion(r.ApplicationVersion) })
                    .Where(x => x.ParsedDate.HasValue)
                    .OrderByDescending(x => x.ParsedDate!.Value)
                    .ToList();

                var latest = matchingReleases.FirstOrDefault();
                if (latest == null)
                {
                    result.HasUpdate = false;
                    result.Message = isDevMode
                        ? $"No version marker matching '{installed.SearchPattern}' found in probed directories: [{probeSummary}]. No compatible releases found in the online catalog."
                        : "No compatible releases found in the online catalog.";
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
        /// Downloads the installer to a temporary file with progress reporting, verifies SHA1, launches it, and exits the current app.
        /// </summary>
        public static async Task DownloadAndExecuteInstallerAsync(
            SoftwareReleaseEntry release,
            IProgress<(long bytesDownloaded, long totalBytes, double percent)>? progress = null,
            Action<string>? statusCallback = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(release.InstallerUrl))
            {
                throw new InvalidOperationException("Installer URL is missing from the release entry.");
            }

            string? fileName = release.InstallerFileName;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = Path.GetFileName(new Uri(release.InstallerUrl).LocalPath);
            }
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "PointlessWaymarks-WaterDazeWin-Setup.exe";
            }

            string tempFilePath = Path.Combine(Path.GetTempPath(), fileName);

            statusCallback?.Invoke("Connecting to update server...");

            using (var response = await _httpClient.GetAsync(release.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? -1L;
                statusCallback?.Invoke(totalBytes > 0 
                    ? $"Downloading installer ({totalBytes / (1024.0 * 1024.0):F1} MB)..." 
                    : "Downloading installer...");

                using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
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
                        statusCallback?.Invoke("Verifying installer integrity (SHA-1)...");
                        fileStream.Position = 0;
                        using var sha1 = SHA1.Create();
                        byte[] hash = sha1.ComputeHash(fileStream);
                        string calculatedHex = BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
                        string expectedHex = release.InstallerFileSha1.Replace("-", "").Trim().ToUpperInvariant();

                        if (!string.Equals(calculatedHex, expectedHex, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException(
                                $"Installer checksum verification failed.\nExpected: {expectedHex}\nComputed: {calculatedHex}");
                        }
                    }
                }
            }

            statusCallback?.Invoke("Launching installer...");

            // Launch the installer process
            var psi = new ProcessStartInfo
            {
                FileName = tempFilePath,
                UseShellExecute = true
            };

            Process.Start(psi);

            // Give a tiny moment for process start, then exit so installer can update files without locks
            await Task.Delay(500);
            Environment.Exit(0);
        }
    }
}
