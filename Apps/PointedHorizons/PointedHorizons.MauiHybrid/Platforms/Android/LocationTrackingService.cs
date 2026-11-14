#if ANDROID
using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using AndroidX.Core.App;
using PointedHorizons.Services;

namespace PointedHorizons.MauiHybrid;

[Service(ForegroundServiceType = ForegroundService.TypeLocation, Exported = false)]
public class LocationTrackingService : Service, ILocationListener
{
    private const int NotificationId = 1001;
    private const string ChannelId = "ph_track_recording_channel";
    private LocationManager? _locationManager;
    private PowerManager.WakeLock? _wakeLock;

    public override void OnCreate()
    {
        base.OnCreate();
        CreateNotificationChannel();
        _locationManager = GetSystemService(LocationService) as LocationManager;
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var notification = BuildNotification("Pointed Horizons", "Using GPS in background...");
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
        {
            StartForeground(NotificationId, notification, ForegroundService.TypeLocation);
        }
        else
        {
            StartForeground(NotificationId, notification);
        }

        AcquireWakeLock();
        StartLocationUpdates();

        return StartCommandResult.Sticky;
    }

    private void StartLocationUpdates()
    {
        if (_locationManager == null) return;

        try
        {
            // Strictly request satellite GPS fixes to avoid cell tower and Wi-Fi fixes.
            // Power saver interval governs receiver cycle; 0m distance filter ensures continuous
            // fixes are captured even when stationary for photo geotagging.
            long intervalMs = GeolocationService.PowerSaverIntervalSeconds * 1000L;
            float minDistanceM = 0f;

            if (_locationManager.IsProviderEnabled(LocationManager.GpsProvider))
            {
                _locationManager.RequestLocationUpdates(LocationManager.GpsProvider, intervalMs, minDistanceM, this);
            }
            else
            {
                Android.Util.Log.Warn("LocationTrackingService", "GPS hardware provider is not enabled on this device.");
            }
        }
        catch (Exception ex)
        {
            Android.Util.Log.Warn("LocationTrackingService", $"Error requesting location updates: {ex.Message}");
        }
    }

    public void OnLocationChanged(Android.Locations.Location location)
    {
        if (location == null) return;

        // Discard any non-GPS fixes (e.g. coarse cell/network fixes)
        if (location.Provider != null && !location.Provider.Equals(LocationManager.GpsProvider, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Discard inaccurate or degraded fixes (> 65m) to avoid cell/wifi jumps
        if (location.HasAccuracy && location.Accuracy > 65f)
        {
            return;
        }

        var pos = new GeoPosition(
            location.Latitude,
            location.Longitude,
            location.HasAltitude ? location.Altitude : null,
            location.HasAccuracy ? location.Accuracy : null,
            DateTime.UtcNow,
            location.HasBearing ? (double)location.Bearing : null
        );

        BackgroundLocationBridge.NativePositionReceived?.Invoke(pos);
    }

    public void OnProviderDisabled(string provider) { }
    public void OnProviderEnabled(string provider) { }
    public void OnStatusChanged(string? provider, Availability status, Bundle? extras) { }

    private void AcquireWakeLock()
    {
        try
        {
            var pm = GetSystemService(PowerService) as PowerManager;
            if (pm != null && _wakeLock == null)
            {
                _wakeLock = pm.NewWakeLock(WakeLockFlags.Partial, "PointedHorizons:LocationWakeLock");
                _wakeLock.Acquire();
            }
        }
        catch { }
    }

    private void ReleaseWakeLock()
    {
        try
        {
            if (_wakeLock != null && _wakeLock.IsHeld)
            {
                _wakeLock.Release();
                _wakeLock = null;
            }
        }
        catch { }
    }

    private Notification BuildNotification(string title, string text)
    {
        var builder = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle(title)
            .SetContentText(text)
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetOngoing(true)
            .SetPriority(NotificationCompat.PriorityLow)
            .SetCategory(NotificationCompat.CategoryService);

        return builder.Build();
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(ChannelId, "GPS Location", NotificationImportance.Low)
            {
                Description = "Ongoing notification while using GPS in the background"
            };
            var nm = GetSystemService(NotificationService) as NotificationManager;
            nm?.CreateNotificationChannel(channel);
        }
    }

    public override void OnDestroy()
    {
        try
        {
            _locationManager?.RemoveUpdates(this);
            ReleaseWakeLock();
            StopForeground(StopForegroundFlags.Remove);
        }
        catch { }

        base.OnDestroy();
    }

    public override IBinder? OnBind(Intent? intent) => null;
}
#endif

