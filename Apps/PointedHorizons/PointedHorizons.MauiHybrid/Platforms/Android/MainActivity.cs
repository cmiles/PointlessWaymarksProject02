using Android.App;
using Android.Content.PM;
using Android.OS;
using Microsoft.Maui.ApplicationModel;

namespace PointedHorizons.MauiHybrid;

[Activity(Label = "Pointed Horizons", Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private static bool _storagePermissionRequested;

    protected override async void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Ensure the app does not render underneath the phone's status bar (time, battery, notifications)
        if (Window != null)
        {
            Window.SetStatusBarColor(Android.Graphics.Color.Rgb(15, 23, 42)); // Match dark theme top header (#0F172A)
            AndroidX.Core.View.WindowCompat.SetDecorFitsSystemWindows(Window, true);

            var contentView = Window.DecorView.FindViewById(Android.Resource.Id.Content);
            if (contentView != null)
            {
                contentView.SetFitsSystemWindows(true);
                AndroidX.Core.View.ViewCompat.SetOnApplyWindowInsetsListener(contentView, new StatusBarInsetsListener());
            }
        }

        // Bridge storage permission checks to shared UI
        PointedHorizons.Services.StoragePermissionBridge.HasAllFilesAccess = () =>
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                return Android.OS.Environment.IsExternalStorageManager;
            }
            return true;
        };

        PointedHorizons.Services.StoragePermissionBridge.RequestAllFilesAccess = () =>
        {
            RequestManageAllFilesAccess(this);
        };

        await RequestStoragePermissionsAsync();
        InitializeOfflineStorage();
        AndroidAppUpdateHandler.Initialize();
        await RequestLocationPermissionsAsync();
    }

    protected override void OnResume()
    {
        base.OnResume();

        // If the user was redirected to Settings to grant "All files access", refresh offline data when they return
        if (_storagePermissionRequested)
        {
            _storagePermissionRequested = false;
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000); // Allow WebView and Activity window to resume focus
                try
                {
                    PointedHorizons.Services.OfflineDataService.EnsureDirectoriesExist();
                    PointedHorizons.Services.OfflineDataService.Refresh();
                }
                catch (System.Exception ex)
                {
                    Android.Util.Log.Warn("MainActivity", $"Post-permission refresh note: {ex.Message}");
                }
            });
        }
    }

    private static void InitializeOfflineStorage()
    {
        try
        {
            string? primaryTarget = null;

            // 1. Ensure public Documents: /storage/emulated/0/Documents/PointedHorizons
            try
            {
                var docs = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDocuments)?.AbsolutePath;
                if (!string.IsNullOrEmpty(docs))
                {
                    string candidate = System.IO.Path.Combine(docs, "PointedHorizons");
                    if (!System.IO.Directory.Exists(candidate)) System.IO.Directory.CreateDirectory(candidate);
                    string pmtiles = System.IO.Path.Combine(candidate, "pmtiles");
                    string dem = System.IO.Path.Combine(candidate, "dem");
                    string gpx = System.IO.Path.Combine(candidate, "gpx");
                    if (!System.IO.Directory.Exists(pmtiles)) System.IO.Directory.CreateDirectory(pmtiles);
                    if (!System.IO.Directory.Exists(dem)) System.IO.Directory.CreateDirectory(dem);
                    if (!System.IO.Directory.Exists(gpx)) System.IO.Directory.CreateDirectory(gpx);
                    primaryTarget = candidate;
                }
            }
            catch (System.Exception exDocs)
            {
                Android.Util.Log.Warn("MainActivity", $"Documents folder init note: {exDocs.Message}");
            }

            // 2. Also ensure public Download: /storage/emulated/0/Download/PointedHorizons
            try
            {
                var dl = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)?.AbsolutePath;
                if (!string.IsNullOrEmpty(dl))
                {
                    string candidate = System.IO.Path.Combine(dl, "PointedHorizons");
                    if (!System.IO.Directory.Exists(candidate)) System.IO.Directory.CreateDirectory(candidate);
                    string pmtiles = System.IO.Path.Combine(candidate, "pmtiles");
                    string dem = System.IO.Path.Combine(candidate, "dem");
                    string gpx = System.IO.Path.Combine(candidate, "gpx");
                    if (!System.IO.Directory.Exists(pmtiles)) System.IO.Directory.CreateDirectory(pmtiles);
                    if (!System.IO.Directory.Exists(dem)) System.IO.Directory.CreateDirectory(dem);
                    if (!System.IO.Directory.Exists(gpx)) System.IO.Directory.CreateDirectory(gpx);
                    if (string.IsNullOrEmpty(primaryTarget)) primaryTarget = candidate;
                }
            }
            catch (System.Exception exDl)
            {
                Android.Util.Log.Warn("MainActivity", $"Download folder init note: {exDl.Message}");
            }

            // 3. Fallback to app external files dir: /storage/emulated/0/Android/data/.../files/PointedHorizons
            if (string.IsNullOrEmpty(primaryTarget))
            {
                try
                {
                    var ext = Android.App.Application.Context.GetExternalFilesDir(null)?.AbsolutePath;
                    if (!string.IsNullOrEmpty(ext))
                    {
                        string candidate = System.IO.Path.Combine(ext, "PointedHorizons");
                        if (!System.IO.Directory.Exists(candidate)) System.IO.Directory.CreateDirectory(candidate);
                        string pmtiles = System.IO.Path.Combine(candidate, "pmtiles");
                        string dem = System.IO.Path.Combine(candidate, "dem");
                        string gpx = System.IO.Path.Combine(candidate, "gpx");
                        if (!System.IO.Directory.Exists(pmtiles)) System.IO.Directory.CreateDirectory(pmtiles);
                        if (!System.IO.Directory.Exists(dem)) System.IO.Directory.CreateDirectory(dem);
                        if (!System.IO.Directory.Exists(gpx)) System.IO.Directory.CreateDirectory(gpx);
                        primaryTarget = candidate;
                    }
                }
                catch (System.Exception exExt)
                {
                    Android.Util.Log.Warn("MainActivity", $"ExternalFilesDir init note: {exExt.Message}");
                }
            }

            if (!string.IsNullOrEmpty(primaryTarget))
            {
                PointedHorizons.Services.OfflineDataService.SetAndroidBaseDirectory(primaryTarget);
            }
            PointedHorizons.Services.OfflineDataService.Initialize();
            PointedHorizons.Services.LocalTileServer.StartServer();
        }
        catch (System.Exception ex)
        {
            Android.Util.Log.Warn("MainActivity", $"InitializeOfflineStorage error: {ex.Message}");
        }
    }

    private async Task RequestStoragePermissionsAsync()
    {
        try
        {
            if (DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.Version.Major < 13)
            {
                var readStatus = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
                if (readStatus != PermissionStatus.Granted)
                {
                    await Permissions.RequestAsync<Permissions.StorageRead>();
                }

                var writeStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                if (writeStatus != PermissionStatus.Granted)
                {
                    await Permissions.RequestAsync<Permissions.StorageWrite>();
                }
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                bool isManager = Android.OS.Environment.IsExternalStorageManager;
                Android.Util.Log.Info("MainActivity", $"ExternalStorageManager granted: {isManager}");
            }
        }
        catch (System.Exception ex)
        {
            Android.Util.Log.Warn("MainActivity", $"RequestStoragePermissionsAsync note: {ex.Message}");
        }
    }

    public static void RequestManageAllFilesAccess(Activity activity)
    {
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                if (!Android.OS.Environment.IsExternalStorageManager)
                {
                    _storagePermissionRequested = true;
                    try
                    {
                        var uri = Android.Net.Uri.Parse($"package:{activity.PackageName}");
                        var intent = new Android.Content.Intent(Android.Provider.Settings.ActionManageAppAllFilesAccessPermission, uri);
                        activity.StartActivity(intent);
                    }
                    catch (System.Exception exInner)
                    {
                        Android.Util.Log.Warn("MainActivity", $"ActionManageAppAllFilesAccessPermission direct intent failed: {exInner.Message}");
                        var fallbackIntent = new Android.Content.Intent(Android.Provider.Settings.ActionManageAllFilesAccessPermission);
                        activity.StartActivity(fallbackIntent);
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Android.Util.Log.Warn("MainActivity", $"RequestManageAllFilesAccess failed: {ex.Message}");
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

internal class StatusBarInsetsListener : Java.Lang.Object, AndroidX.Core.View.IOnApplyWindowInsetsListener
{
    public AndroidX.Core.View.WindowInsetsCompat OnApplyWindowInsets(Android.Views.View v, AndroidX.Core.View.WindowInsetsCompat insets)
    {
        var statusBars = insets.GetInsets(AndroidX.Core.View.WindowInsetsCompat.Type.StatusBars() | AndroidX.Core.View.WindowInsetsCompat.Type.DisplayCutout());
        v.SetPadding(0, statusBars.Top, 0, 0);
        return insets;
    }
}
