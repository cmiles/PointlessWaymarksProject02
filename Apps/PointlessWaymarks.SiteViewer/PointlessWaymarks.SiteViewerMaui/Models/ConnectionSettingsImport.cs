using System.Text.Json;
using System.Text.Json.Serialization;
using PointlessWaymarks.SiteViewerMaui.Tools;

namespace PointlessWaymarks.SiteViewerMaui.Models;

/// <summary>
///     A tolerant, self-contained representation of the values that the "Load From File" feature on the
///     Connection editor can import. Every value is nullable so that a file may contain only a partial set
///     of settings - a null value means "the file did not specify this setting" and it is simply left
///     unchanged on the editor.
/// </summary>
public class ConnectionSettingsImport
{
    public string? SettingsType { get; set; }

    public string? Name { get; set; }
    public string? CloudViewerSettingsName { get => Name; set => Name ??= value; }

    public string? SiteDomain { get; set; }
    public string? CloudViewerSiteDomain { get => SiteDomain; set => SiteDomain ??= value; }

    public string? Provider { get; set; }
    public string? CloudViewerProvider { get => Provider; set => Provider ??= value; }

    public string? Region { get; set; }
    public string? CloudViewerRegion { get => Region; set => Region ??= value; }

    public string? ServiceUrl { get; set; }
    public string? CloudServiceUrl { get => ServiceUrl; set => ServiceUrl ??= value; }
    public string? CloudViewerServiceUrl { get => ServiceUrl; set => ServiceUrl ??= value; }

    public string? Bucket { get; set; }
    public string? CloudViewerBucket { get => Bucket; set => Bucket ??= value; }

    public string? AccessKey { get; set; }
    public string? CloudViewerAccessKey { get => AccessKey; set => AccessKey ??= value; }

    public string? Secret { get; set; }
    public string? CloudViewerSecret { get => Secret; set => Secret ??= value; }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    ///     Attempts to inspect the <see cref="SettingsType" /> value from the supplied JSON text
    ///     without attempting decryption. Returns null if the text is empty, is not valid JSON,
    ///     or does not specify a SettingsType.
    /// </summary>
    public static string? ReadSettingsType(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            var deserialized = JsonSerializer.Deserialize<ConnectionSettingsImport>(json, SerializerOptions);
            return deserialized?.SettingsType;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Attempts to deserialize the supplied JSON text into a <see cref="ConnectionSettingsImport" />.
    ///     Returns false (and a null result) when the text is empty, is not valid connection-settings JSON,
    ///     or fails decryption; it never throws so callers can safely warn the user instead of crashing.
    ///     If a <paramref name="password" /> is provided, non-null property values (except <see cref="SettingsType" />)
    ///     are decrypted.
    /// </summary>
    public static bool TryDeserialize(string? json, out ConnectionSettingsImport? result, string? password = null)
    {
        return TryDeserialize(json, password, out result);
    }

    /// <summary>
    ///     Attempts to deserialize the supplied JSON text into a <see cref="ConnectionSettingsImport" />.
    ///     Returns false (and a null result) when the text is empty, is not valid connection-settings JSON,
    ///     or fails decryption; it never throws so callers can safely warn the user instead of crashing.
    ///     If a <paramref name="password" /> is provided, non-null property values (except <see cref="SettingsType" />)
    ///     are decrypted.
    /// </summary>
    public static bool TryDeserialize(string? json, string? password, out ConnectionSettingsImport? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            var deserialized = JsonSerializer.Deserialize<ConnectionSettingsImport>(json, SerializerOptions);
            if (deserialized is null) return false;

            if (!string.IsNullOrWhiteSpace(password))
            {
                deserialized.Name = DecryptValue(deserialized.Name, password);
                deserialized.SiteDomain = DecryptValue(deserialized.SiteDomain, password);
                deserialized.Provider = DecryptValue(deserialized.Provider, password);
                deserialized.Region = DecryptValue(deserialized.Region, password);
                deserialized.ServiceUrl = DecryptValue(deserialized.ServiceUrl, password);
                deserialized.Bucket = DecryptValue(deserialized.Bucket, password);
                deserialized.AccessKey = DecryptValue(deserialized.AccessKey, password);
                deserialized.Secret = DecryptValue(deserialized.Secret, password);
            }
            else if (string.Equals(deserialized.SettingsType, "SecureCloudViewer", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            result = deserialized;
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    private static string? DecryptValue(string? value, string password)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Decrypt(password);
    }

    /// <summary>
    ///     Serializes the supplied settings to indented JSON (used for producing example/export files).
    /// </summary>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions(SerializerOptions) { WriteIndented = true });
    }
}
