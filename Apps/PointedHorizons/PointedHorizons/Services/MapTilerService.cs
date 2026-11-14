using System;
using System.Diagnostics;
using OpenSilver;

namespace PointedHorizons.Services;

/// <summary>
/// Service for managing MapTiler Cloud API key configuration and weather overlay settings.
/// </summary>
public static class MapTilerService
{
    private const string ApiKeyStorageKey = "pointedhorizons_maptiler_apikey";
    private static string? _apiKey;
    private static bool _loaded;

    public static event EventHandler? ApiKeyChanged;

    /// <summary>
    /// Gets or sets the MapTiler Cloud API key.
    /// </summary>
    public static string? ApiKey
    {
        get
        {
            EnsureLoaded();
            return _apiKey;
        }
        set
        {
            string? trimmed = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (_apiKey != trimmed)
            {
                _apiKey = trimmed;
                SaveApiKey(trimmed);
                TurnOffAllOverlays();
                ApiKeyChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Returns true if a MapTiler API key has been configured.
    /// </summary>
    public static bool IsConfigured
    {
        get
        {
            EnsureLoaded();
            return !string.IsNullOrWhiteSpace(_apiKey);
        }
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        try
        {
            object? val = Interop.ExecuteJavaScript("localStorage.getItem($0)", ApiKeyStorageKey);
            if (val is string s && !string.IsNullOrWhiteSpace(s))
            {
                _apiKey = s.Trim();
            }
            else
            {
                _apiKey = null;
                // If API key is blank when the program starts, turn off all weather layers
                TurnOffAllOverlays();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MapTilerService] Error loading API key from localStorage: {ex.Message}");
        }
    }

    /// <summary>
    /// Turns off all MapTiler weather overlays and updates persistent storage.
    /// </summary>
    public static void TurnOffAllOverlays()
    {
        string[] overlays = ["temperature", "wind", "radar", "precipitation", "clouds", "pressure"];
        foreach (var o in overlays)
        {
            SetOverlayVisible(o, false);
        }
    }

    private static void SaveApiKey(string? apiKey)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Interop.ExecuteJavaScriptVoid("localStorage.removeItem($0)", ApiKeyStorageKey);
            }
            else
            {
                Interop.ExecuteJavaScriptVoid("localStorage.setItem($0, $1)", ApiKeyStorageKey, apiKey);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MapTilerService] Error saving API key to localStorage: {ex.Message}");
        }
    }

    public static bool GetOverlayVisible(string overlayType)
    {
        try
        {
            object? val = Interop.ExecuteJavaScript("localStorage.getItem($0)", $"pointedhorizons_weather_{overlayType.ToLowerInvariant()}");
            return val is string s && string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static void SetOverlayVisible(string overlayType, bool visible)
    {
        try
        {
            Interop.ExecuteJavaScriptVoid("localStorage.setItem($0, $1)", $"pointedhorizons_weather_{overlayType.ToLowerInvariant()}", visible ? "true" : "false");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MapTilerService] Error saving overlay visibility: {ex.Message}");
        }
    }
}
