using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using OpenSilver;

namespace PointedHorizons.Services;

public class GeoNamesResult
{
    public long GeoNameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ToponymName { get; set; } = string.Empty;
    public string AdminName1 { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string FeatureCodeName { get; set; } = string.Empty;
    public string FeatureClass { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? ElevationM { get; set; }

    public string TitleDisplay
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Name)) parts.Add(Name);
            if (!string.IsNullOrWhiteSpace(AdminName1)) parts.Add(AdminName1);
            if (!string.IsNullOrWhiteSpace(CountryCode)) parts.Add(CountryCode);
            else if (!string.IsNullOrWhiteSpace(CountryName)) parts.Add(CountryName);
            return parts.Count > 0 ? string.Join(", ", parts) : "Unknown Location";
        }
    }

    public string SubtitleDisplay
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(FeatureCodeName)) parts.Add(FeatureCodeName);
            parts.Add($"{Latitude:F4}°, {Longitude:F4}°");
            if (ElevationM.HasValue && ElevationM.Value > -500)
            {
                parts.Add($"{ElevationM.Value:F0}m / {ElevationM.Value * 3.28084:F0}ft");
            }
            return string.Join(" • ", parts);
        }
    }
}

public static class GeoNamesService
{
    private const string StorageKey = "pointedhorizons_geonames_username";
    private static string? _username;
    private static bool _loaded;

    public static event EventHandler? ConfigurationChanged;

    public static string? Username
    {
        get
        {
            EnsureLoaded();
            return _username;
        }
        set
        {
            string? trimmed = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (_username != trimmed)
            {
                _username = trimmed;
                SaveUsername(trimmed);
                ConfigurationChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }

    public static bool IsConfigured
    {
        get
        {
            EnsureLoaded();
            return !string.IsNullOrWhiteSpace(_username);
        }
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        try
        {
            object? val = Interop.ExecuteJavaScript("localStorage.getItem($0)", StorageKey);
            if (val is string s && !string.IsNullOrWhiteSpace(s))
            {
                _username = s.Trim();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GeoNamesService] Error loading username from localStorage: {ex.Message}");
        }
    }

    private static void SaveUsername(string? username)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                Interop.ExecuteJavaScriptVoid("localStorage.removeItem($0)", StorageKey);
            }
            else
            {
                Interop.ExecuteJavaScriptVoid("localStorage.setItem($0, $1)", StorageKey, username);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GeoNamesService] Error saving username to localStorage: {ex.Message}");
        }
    }

    /// <summary>
    /// Searches for geographic places, peaks, cities, and landmarks using the GeoNames Search API.
    /// </summary>
    public static async Task<(List<GeoNamesResult> Results, string? ErrorMessage)> SearchAsync(string query, int maxRows = 25)
    {
        EnsureLoaded();

        if (string.IsNullOrWhiteSpace(_username))
        {
            return (new List<GeoNamesResult>(), "GeoNames username is not configured. Please enter your GeoNames username in Settings / Options.");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return (new List<GeoNamesResult>(), "Please enter a location name to search.");
        }

        try
        {
            string url = $"https://secure.geonames.org/searchJSON?q={Uri.EscapeDataString(query.Trim())}&maxRows={maxRows}&username={Uri.EscapeDataString(_username)}&style=FULL";

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(12);

            string responseJson = await client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            // Check for GeoNames error status response: {"status": {"message": "...", "value": 10}}
            if (root.TryGetProperty("status", out var statusEl))
            {
                string msg = "GeoNames API returned an error.";
                if (statusEl.TryGetProperty("message", out var msgEl))
                {
                    msg = msgEl.GetString() ?? msg;
                }
                return (new List<GeoNamesResult>(), msg);
            }

            if (!root.TryGetProperty("geonames", out var geonamesEl) || geonamesEl.ValueKind != JsonValueKind.Array)
            {
                return (new List<GeoNamesResult>(), null);
            }

            var results = new List<GeoNamesResult>();

            foreach (var item in geonamesEl.EnumerateArray())
            {
                var r = new GeoNamesResult();

                if (item.TryGetProperty("geonameId", out var idEl) && idEl.TryGetInt64(out long id))
                    r.GeoNameId = id;

                if (item.TryGetProperty("name", out var nameEl))
                    r.Name = nameEl.GetString() ?? "";

                if (item.TryGetProperty("toponymName", out var topEl))
                    r.ToponymName = topEl.GetString() ?? "";

                if (item.TryGetProperty("adminName1", out var admEl))
                    r.AdminName1 = admEl.GetString() ?? "";

                if (item.TryGetProperty("countryName", out var ctryEl))
                    r.CountryName = ctryEl.GetString() ?? "";

                if (item.TryGetProperty("countryCode", out var ccEl))
                    r.CountryCode = ccEl.GetString() ?? "";

                if (item.TryGetProperty("fcodeName", out var fcNameEl))
                    r.FeatureCodeName = fcNameEl.GetString() ?? "";

                if (item.TryGetProperty("fcl", out var fclEl))
                    r.FeatureClass = fclEl.GetString() ?? "";

                // Coordinates
                if (item.TryGetProperty("lat", out var latEl))
                {
                    if (latEl.ValueKind == JsonValueKind.String && double.TryParse(latEl.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lat))
                        r.Latitude = lat;
                    else if (latEl.ValueKind == JsonValueKind.Number && latEl.TryGetDouble(out double latN))
                        r.Latitude = latN;
                }

                if (item.TryGetProperty("lng", out var lngEl))
                {
                    if (lngEl.ValueKind == JsonValueKind.String && double.TryParse(lngEl.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lng))
                        r.Longitude = lng;
                    else if (lngEl.ValueKind == JsonValueKind.Number && lngEl.TryGetDouble(out double lngN))
                        r.Longitude = lngN;
                }

                // Elevation: prefer 'elevation' property, fallback to 'dem'
                if (item.TryGetProperty("elevation", out var elevEl))
                {
                    if (elevEl.ValueKind == JsonValueKind.Number && elevEl.TryGetDouble(out double eVal))
                        r.ElevationM = eVal;
                    else if (elevEl.ValueKind == JsonValueKind.String && double.TryParse(elevEl.GetString(), out double eParsed))
                        r.ElevationM = eParsed;
                }

                if (!r.ElevationM.HasValue && item.TryGetProperty("dem", out var demEl))
                {
                    if (demEl.ValueKind == JsonValueKind.Number && demEl.TryGetDouble(out double dVal) && dVal > -500)
                        r.ElevationM = dVal;
                    else if (demEl.ValueKind == JsonValueKind.String && double.TryParse(demEl.GetString(), out double dParsed) && dParsed > -500)
                        r.ElevationM = dParsed;
                }

                results.Add(r);
            }

            return (results, null);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GeoNamesService] Search exception: {ex.Message}");
            return (new List<GeoNamesResult>(), $"Search failed: {ex.Message}");
        }
    }
}

