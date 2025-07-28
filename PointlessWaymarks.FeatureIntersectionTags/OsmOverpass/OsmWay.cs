using System.Text.Json.Serialization;

namespace PointlessWaymarks.FeatureIntersectionTags.OsmOverpass;

/// <summary>
///     Way element (line or polygon)
/// </summary>
public class OsmWay : OsmElement
{
    /// <summary>
    ///     Available when querying with "out geom"
    /// </summary>
    [JsonPropertyName("geometry")]
    public List<OsmGeometry>? Geometry { get; set; }

    [JsonPropertyName("nodes")] public List<long> Nodes { get; set; } = [];
}