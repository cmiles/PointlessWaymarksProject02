using System.Text.Json.Serialization;

namespace PointlessWaymarks.FeatureIntersectionTags.OsmOverpass;

/// <summary>
///     Member of a relation
/// </summary>
public class OsmMember
{
    /// <summary>
    ///     Available when querying with "out geom"
    /// </summary>
    [JsonPropertyName("geometry")]
    public List<OsmGeometry>? Geometry { get; set; }

    [JsonPropertyName("ref")] public long Ref { get; set; }

    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;

    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
}