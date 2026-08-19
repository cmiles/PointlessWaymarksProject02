using System.Text.Json;
using System.Text.Json.Serialization;

namespace PointlessWaymarks.SiteViewerMaui.Models;

/// <summary>
///     A tolerant, self-contained representation of the values that the "Load From File" feature on the
///     Connection editor can import. Every value is nullable so that a file may contain only a partial set
///     of settings - a null value means "the file did not specify this setting" and it is simply left
///     unchanged on the editor.
/// </summary>
public class ConnectionSettingsImport
{
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
    ///     Attempts to deserialize the supplied JSON text into a <see cref="ConnectionSettingsImport" />.
    ///     Returns false (and a null result) when the text is empty or is not valid connection-settings JSON;
    ///     it never throws so callers can safely warn the user instead of crashing.
    /// </summary>
    public static bool TryDeserialize(string? json, out ConnectionSettingsImport? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            result = JsonSerializer.Deserialize<ConnectionSettingsImport>(json, SerializerOptions);
        }
        catch
        {
            result = null;
            return false;
        }

        return result is not null;
    }

    /// <summary>
    ///     Serializes the supplied settings to indented JSON (used for producing example/export files).
    /// </summary>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions(SerializerOptions) { WriteIndented = true });
    }
}
