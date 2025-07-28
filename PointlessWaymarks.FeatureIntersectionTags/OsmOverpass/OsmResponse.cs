using System.Text.Json.Serialization;

namespace PointlessWaymarks.FeatureIntersectionTags.OsmOverpass;

/// <summary>
///     Root class for Overpass API JSON response
/// </summary>
public class OsmResponse
{
    [JsonPropertyName("elements")] public List<OsmElement> Elements { get; set; } = [];

    [JsonPropertyName("generator")] public string Generator { get; set; } = string.Empty;

    [JsonPropertyName("osm3s")] public Osm3s? Osm3s { get; set; }

    [JsonPropertyName("version")] public double Version { get; set; }
}