using System;
using System.Diagnostics;
using System.Threading.Tasks;
using OpenSilver;

namespace PointedHorizons.Services;

public record GeoPosition(double Latitude, double Longitude, double? AltitudeM, double? AccuracyM, DateTime Timestamp);

/// <summary>
/// Bridge hook for platform-specific background location services (e.g. Android Foreground Service).
/// </summary>
public static class BackgroundLocationBridge
{
    public static Action<bool>? ToggleForegroundService;
    public static Action<GeoPosition>? NativePositionReceived;
}

public static class GeolocationService
{
    private static bool _isTracking;
    public static bool BatterySaverMode { get; set; } = true;

    /// <summary>
    /// Gets the current GPS position using the HTML5 / WebView Geolocation API.
    /// Uses battery-efficient settings when BatterySaverMode is active.
    /// </summary>
    public static async Task<GeoPosition?> GetCurrentPositionAsync(int timeoutSeconds = 12)
    {
        var tcs = new TaskCompletionSource<GeoPosition?>();

        Action<double, double, object?, object?> onCoords = (lat, lon, altObj, accObj) =>
        {
            double? alt = altObj is double dAlt ? dAlt : (double.TryParse(altObj?.ToString(), out var pa) ? pa : null);
            double? acc = accObj is double dAcc ? dAcc : (double.TryParse(accObj?.ToString(), out var pac) ? pac : null);
            tcs.TrySetResult(new GeoPosition(lat, lon, alt, acc, DateTime.UtcNow));
        };

        Action<string> onError = (err) =>
        {
            Debug.WriteLine($"[GeolocationService] Error: {err}");
            tcs.TrySetResult(null);
        };

        // Always force high accuracy to ensure genuine GPS satellite hardware fixes and avoid cell/wifi positioning
        const bool highAcc = true;
        int maxAgeMs = BatterySaverMode ? 10000 : 3000;

        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                (function(successCb, errorCb, timeoutSec, highAcc, maxAge) {
                    if (!navigator || !navigator.geolocation) {
                        if (errorCb) errorCb('Geolocation API not supported in this environment.');
                        return;
                    }
                    var options = {
                        enableHighAccuracy: highAcc,
                        timeout: (timeoutSec || 12) * 1000,
                        maximumAge: maxAge
                    };
                    navigator.geolocation.getCurrentPosition(
                        function(pos) {
                            if (successCb) {
                                successCb(
                                    pos.coords.latitude,
                                    pos.coords.longitude,
                                    pos.coords.altitude != null ? pos.coords.altitude : null,
                                    pos.coords.accuracy != null ? pos.coords.accuracy : null
                                );
                            }
                        },
                        function(err) {
                            var msg = err.message || ('Code ' + err.code);
                            if (errorCb) errorCb(msg);
                        },
                        options
                    );
                })($0, $1, $2, $3, $4);
            ", onCoords, onError, timeoutSeconds, highAcc, maxAgeMs);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GeolocationService] Exception calling JS: {ex.Message}");
            tcs.TrySetResult(null);
        }

        var delayTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds + 2));
        var completed = await Task.WhenAny(tcs.Task, delayTask);
        if (completed == tcs.Task)
        {
            return await tcs.Task;
        }

        return null;
    }

    /// <summary>
    /// Starts live GPS tracking via navigator.geolocation.watchPosition and platform background service.
    /// Configured for high battery efficiency by default.
    /// </summary>
    public static void StartTracking(Action<GeoPosition> onUpdate, Action<string>? onError = null, bool startBackgroundService = true)
    {
        _isTracking = true;

        Action<double, double, object?, object?> onCoords = (lat, lon, altObj, accObj) =>
        {
            double? alt = altObj is double dAlt ? dAlt : (double.TryParse(altObj?.ToString(), out var pa) ? pa : null);
            double? acc = accObj is double dAcc ? dAcc : (double.TryParse(accObj?.ToString(), out var pac) ? pac : null);
            onUpdate(new GeoPosition(lat, lon, alt, acc, DateTime.UtcNow));
        };

        Action<string> onErr = (msg) =>
        {
            Debug.WriteLine($"[GeolocationService] Watch error: {msg}");
            onError?.Invoke(msg);
        };

        // Wire native background service callback
        BackgroundLocationBridge.NativePositionReceived = (pos) =>
        {
            onUpdate(pos);
        };

        if (startBackgroundService)
        {
            try
            {
                BackgroundLocationBridge.ToggleForegroundService?.Invoke(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GeolocationService] ToggleForegroundService error: {ex.Message}");
            }
        }

        // Always force high accuracy to ensure genuine GPS satellite hardware fixes and avoid cell/wifi positioning
        const bool highAcc = true;
        // In battery saver mode, accept cached positions up to 10s and avoid aggressive GPS wakeups
        int maxAgeMs = BatterySaverMode ? 10000 : 3000;
        int timeoutMs = BatterySaverMode ? 25000 : 15000;

        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                (function(updateCb, errCb, highAcc, maxAge, timeout) {
                    if (!navigator || !navigator.geolocation) {
                        if (errCb) errCb('Geolocation API not supported');
                        return;
                    }
                    if (window._phWatchId != null) {
                        navigator.geolocation.clearWatch(window._phWatchId);
                        window._phWatchId = null;
                    }
                    window._phWatchId = navigator.geolocation.watchPosition(
                        function(pos) {
                            if (updateCb) {
                                updateCb(
                                    pos.coords.latitude,
                                    pos.coords.longitude,
                                    pos.coords.altitude != null ? pos.coords.altitude : null,
                                    pos.coords.accuracy != null ? pos.coords.accuracy : null
                                );
                            }
                        },
                        function(err) {
                            var msg = err.message || ('Code ' + err.code);
                            if (errCb) errCb(msg);
                        },
                        {
                            enableHighAccuracy: highAcc,
                            maximumAge: maxAge,
                            timeout: timeout
                        }
                    );
                })($0, $1, $2, $3, $4);
            ", onCoords, onErr, highAcc, maxAgeMs, timeoutMs);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GeolocationService] Watch exception: {ex.Message}");
            onError?.Invoke(ex.Message);
        }
    }

    /// <summary>
    /// Stops live GPS tracking and stops any active background service.
    /// </summary>
    public static void StopTracking()
    {
        _isTracking = false;

        try
        {
            BackgroundLocationBridge.ToggleForegroundService?.Invoke(false);
            BackgroundLocationBridge.NativePositionReceived = null;
        }
        catch { }

        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window._phWatchId != null) {
                    navigator.geolocation.clearWatch(window._phWatchId);
                    window._phWatchId = null;
                }
            ");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GeolocationService] StopTracking exception: {ex.Message}");
        }
    }

    public static bool IsTracking => _isTracking;
}
