#nullable enable
using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Android.OS;
using Android.Provider;
using PointedHorizons.Services;

namespace PointedHorizons.MauiHybrid;

public static class AndroidAppUpdateHandler
{
    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

    public static void Initialize()
    {
        AppUpdateService.AndroidDownloader = DownloadAndSaveApkAsync;
    }

    public static async Task<string> DownloadAndSaveApkAsync(
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
            try
            {
                fileName = Path.GetFileName(new Uri(release.InstallerUrl).LocalPath);
            }
            catch { }
        }
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "PointedHorizons-Signed.apk";
        }

        var appContext = Android.App.Application.Context;

        // 1. Download to temporary cache file first to verify SHA-1 and ensure progress tracking
        string cacheDir = appContext.CacheDir?.AbsolutePath ?? Path.GetTempPath();
        string tempFilePath = Path.Combine(cacheDir, $"update_{Guid.NewGuid():N}_{fileName}");

        statusCallback?.Invoke("Connecting to update server...");

        using (var response = await _httpClient.GetAsync(release.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1L;
            statusCallback?.Invoke(totalBytes > 0
                ? $"Downloading APK ({totalBytes / (1024.0 * 1024.0):F1} MB)..."
                : "Downloading APK...");

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
                    statusCallback?.Invoke("Verifying APK integrity (SHA-1)...");
                    fileStream.Position = 0;
                    using var sha1 = SHA1.Create();
                    byte[] hash = sha1.ComputeHash(fileStream);
                    string calculatedHex = BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
                    string expectedHex = release.InstallerFileSha1.Replace("-", "").Trim().ToUpperInvariant();

                    if (!string.Equals(calculatedHex, expectedHex, StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(tempFilePath); } catch { }
                        throw new InvalidOperationException(
                            $"Package checksum verification failed.\nExpected: {expectedHex}\nComputed: {calculatedHex}");
                    }
                }
            }
        }

        string? publicDownloadResult = null;
        Android.Net.Uri? mediaStoreUri = null;

        // 2. Target 1: Public Downloads folder
        // On Android 10+ (API 29+), use MediaStore.Downloads to insert into the public Downloads collection
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            try
            {
                statusCallback?.Invoke("Saving to public Downloads storage...");
                var resolver = appContext.ContentResolver;
                if (resolver != null)
                {
                    // Clean up existing entry with same name if present
                    try
                    {
                        var projection = new[] { IBaseColumns.Id };
                        var selection = $"{MediaStore.IMediaColumns.DisplayName} = ?";
                        var selectionArgs = new[] { fileName };
                        using var cursor = resolver.Query(MediaStore.Downloads.ExternalContentUri!, projection, selection, selectionArgs, null);
                        if (cursor != null && cursor.MoveToFirst())
                        {
                            long id = cursor.GetLong(cursor.GetColumnIndexOrThrow(IBaseColumns.Id));
                            var existingUri = ContentUris.WithAppendedId(MediaStore.Downloads.ExternalContentUri!, id);
                            resolver.Delete(existingUri, null, null);
                        }
                    }
                    catch { }

                    var values = new ContentValues();
                    values.Put(MediaStore.IMediaColumns.DisplayName, fileName);
                    values.Put(MediaStore.IMediaColumns.MimeType, "application/vnd.android.package-archive");
                    values.Put(MediaStore.IMediaColumns.RelativePath, Android.OS.Environment.DirectoryDownloads);
                    values.Put(MediaStore.IMediaColumns.IsPending, 1);

                    mediaStoreUri = resolver.Insert(MediaStore.Downloads.ExternalContentUri!, values);
                    if (mediaStoreUri != null)
                    {
                        using (var src = File.OpenRead(tempFilePath))
                        using (var dest = resolver.OpenOutputStream(mediaStoreUri))
                        {
                            if (dest != null)
                            {
                                await src.CopyToAsync(dest, 81920, cancellationToken);
                                await dest.FlushAsync(cancellationToken);
                            }
                        }

                        values.Clear();
                        values.Put(MediaStore.IMediaColumns.IsPending, 0);
                        resolver.Update(mediaStoreUri, values, null, null);

                        var publicDl = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)?.AbsolutePath;
                        publicDownloadResult = !string.IsNullOrEmpty(publicDl)
                            ? Path.Combine(publicDl, fileName)
                            : $"/storage/emulated/0/Download/{fileName}";
                    }
                }
            }
            catch (Exception ex)
            {
                Android.Util.Log.Warn("AndroidAppUpdate", $"MediaStore.Downloads save error: {ex.Message}");
            }
        }

        // Direct write to public Downloads directory (for Android <= 9 or when storage manager permission is available)
        if (string.IsNullOrEmpty(publicDownloadResult))
        {
            try
            {
                var dlDir = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)?.AbsolutePath
                    ?? "/storage/emulated/0/Download";
                if (!Directory.Exists(dlDir))
                {
                    Directory.CreateDirectory(dlDir);
                }
                string directTarget = Path.Combine(dlDir, fileName);
                File.Copy(tempFilePath, directTarget, overwrite: true);
                publicDownloadResult = directTarget;
            }
            catch (Exception ex)
            {
                Android.Util.Log.Warn("AndroidAppUpdate", $"Direct copy to Downloads error: {ex.Message}");
            }
        }

        // 3. Beside the DEM and PMTiles folder (as secondary copy or primary fallback if public Downloads failed)
        string? besideDemResult = null;
        try
        {
            string? baseDir = OfflineDataService.AndroidBaseDirectory;
            if (string.IsNullOrWhiteSpace(baseDir) && !string.IsNullOrWhiteSpace(OfflineDataService.DemDirectory))
            {
                baseDir = Path.GetDirectoryName(OfflineDataService.DemDirectory);
            }
            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                if (!Directory.Exists(baseDir))
                {
                    Directory.CreateDirectory(baseDir);
                }
                string besideTarget = Path.Combine(baseDir, fileName);
                File.Copy(tempFilePath, besideTarget, overwrite: true);
                besideDemResult = besideTarget;
                Android.Util.Log.Info("AndroidAppUpdate", $"Copied APK beside DEM and PMTiles: {besideTarget}");
            }
        }
        catch (Exception ex)
        {
            Android.Util.Log.Warn("AndroidAppUpdate", $"Copy beside DEM/PMTiles error: {ex.Message}");
        }

        // 4. Resolve the result path
        string finalPath;
        if (!string.IsNullOrEmpty(publicDownloadResult))
        {
            finalPath = publicDownloadResult;
            if (!string.IsNullOrEmpty(besideDemResult))
            {
                statusCallback?.Invoke($"APK saved to Downloads ({finalPath}) and beside DEM/PMTiles ({besideDemResult}).");
            }
            else
            {
                statusCallback?.Invoke($"APK saved to public Downloads: {finalPath}");
            }
        }
        else if (!string.IsNullOrEmpty(besideDemResult))
        {
            finalPath = besideDemResult;
            statusCallback?.Invoke($"Public Downloads unavailable. APK saved beside DEM/PMTiles: {finalPath}");
        }
        else
        {
            // Fallback to app external files or internal cache directory
            string fallbackPath = Path.Combine(appContext.GetExternalFilesDir(null)?.AbsolutePath ?? cacheDir, fileName);
            try
            {
                File.Copy(tempFilePath, fallbackPath, overwrite: true);
                finalPath = fallbackPath;
            }
            catch
            {
                finalPath = tempFilePath;
            }
            statusCallback?.Invoke($"APK saved to: {finalPath}");
        }

        // Clean up temporary download file if different from finalPath
        if (!string.Equals(tempFilePath, finalPath, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(tempFilePath); } catch { }
        }

        // 5. Attempt to launch package installer intent
        try
        {
            var installIntent = new Intent(Intent.ActionView);
            installIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission);

            Android.Net.Uri? launchUri = mediaStoreUri;
            if (launchUri == null && File.Exists(finalPath))
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
                {
                    string authority = $"{appContext.PackageName}.fileProvider";
                    launchUri = AndroidX.Core.Content.FileProvider.GetUriForFile(appContext, authority, new Java.IO.File(finalPath));
                }
                else
                {
                    launchUri = Android.Net.Uri.FromFile(new Java.IO.File(finalPath));
                }
            }

            if (launchUri != null)
            {
                installIntent.SetDataAndType(launchUri, "application/vnd.android.package-archive");
                appContext.StartActivity(installIntent);
            }
        }
        catch (Exception ex)
        {
            Android.Util.Log.Warn("AndroidAppUpdate", $"Could not launch installer intent: {ex.Message}");
        }

        return finalPath;
    }
}
