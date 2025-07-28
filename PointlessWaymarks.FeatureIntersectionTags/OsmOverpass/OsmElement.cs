using System.Text.Json.Serialization;

namespace PointlessWaymarks.FeatureIntersectionTags.OsmOverpass;

/// <summary>
///     Base class for all OSM elements
/// </summary>
public abstract class OsmElement
{
    [JsonPropertyName("id")] public long Id { get; set; }

    [JsonPropertyName("tags")] public Dictionary<string, string> Tags { get; set; } = [];
}