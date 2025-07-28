using System.Text.Json.Serialization;

namespace PointlessWaymarks.FeatureIntersectionTags.OsmOverpass;

/// <summary>
///     Metadata about the Overpass API response
/// </summary>
public class Osm3s
{
    [JsonPropertyName("copyright")] public string Copyright { get; set; } = string.Empty;

    [JsonPropertyName("timestamp_osm_base")]
    public string TimestampOsmBase { get; set; } = string.Empty;
}