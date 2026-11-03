using Android.App;
using Android.Content.PM;
using Android.OS;
using Microsoft.Maui.ApplicationModel;

namespace PointedHorizons.MauiHybrid;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override async void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        InitializeOfflineStorage();
        await RequestLocationPermissionsAsync();
    }

    private static void InitializeOfflineStorage()
    {
        try
        {
            string? target = null;
            // 1. Try public Documents: /storage/emulated/0/Documents/PointedHorizons
            try
            {
                var docs = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDocuments)?.AbsolutePath;
                if (!string.IsNullOrEmpty(docs))
                {
                    string candidate = System.IO.Path.Combine(docs, "PointedHorizons");
                    if (!System.IO.Directory.Exists(candidate)) System.IO.Directory.CreateDirectory(candidate);
                    string test = System.IO.Path.Combine(candidate, ".probe");
                    System.IO.File.WriteAllText(test, "ok");
                    System.IO.File.Delete(test);
                    target = candidate;
                }
            }
            catch { }

            // 2. Try public Download: /storage/emulated/0/Download/PointedHorizons
            if (string.IsNullOrEmpty(target))
            {
                try
                {
                    var dl = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)?.AbsolutePath;
                    if (!string.IsNullOrEmpty(dl))
                    {
                        string candidate = System.IO.Path.Combine(dl, "PointedHorizons");
                        if (!System.IO.Directory.Exists(candidate)) System.IO.Directory.CreateDirectory(candidate);
                        string test = System.IO.Path.Combine(candidate, ".probe");
                        System.IO.File.WriteAllText(test, "ok");
                        System.IO.File.Delete(test);
                        target = candidate;
                    }
                }
                catch { }
            }

            // 3. Fallback to app external files dir: /storage/emulated/0/Android/data/.../files/PointedHorizons
            if (string.IsNullOrEmpty(target))
            {
                try
                {
                    var ext = Android.App.Application.Context.GetExternalFilesDir(null)?.AbsolutePath;
                    if (!string.IsNullOrEmpty(ext))
                    {
                        string candidate = System.IO.Path.Combine(ext, "PointedHorizons");
                        if (!System.IO.Directory.Exists(candidate)) System.IO.Directory.CreateDirectory(candidate);
                        target = candidate;
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(target))
            {
                // Ensure pmtiles, dem, and gpx folders are created beside each other on startup
                try
                {
                    string pmtiles = System.IO.Path.Combine(target, "pmtiles");
                    string dem = System.IO.Path.Combine(target, "dem");
                    string gpx = System.IO.Path.Combine(target, "gpx");
                    if (!System.IO.Directory.Exists(pmtiles)) System.IO.Directory.CreateDirectory(pmtiles);
                    if (!System.IO.Directory.Exists(dem)) System.IO.Directory.CreateDirectory(dem);
                    if (!System.IO.Directory.Exists(gpx)) System.IO.Directory.CreateDirectory(gpx);
                }
                catch { }

                PointedHorizons.Services.OfflineDataService.SetAndroidBaseDirectory(target);
            }
            PointedHorizons.Services.OfflineDataService.Initialize();
            PointedHorizons.Services.LocalTileServer.StartServer();
        }
        catch (System.Exception ex)
        {
            Android.Util.Log.Warn("MainActivity", $"InitializeOfflineStorage error: {ex.Message}");
        }
    }

    private static async Task RequestLocationPermissionsAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.Version.Major >= 13)
            {
                var notifStatus = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
                if (notifStatus != PermissionStatus.Granted)
                {
                    await Permissions.RequestAsync<Permissions.PostNotifications>();
                }
            }
        }
        catch
        {
        }
    }
}
