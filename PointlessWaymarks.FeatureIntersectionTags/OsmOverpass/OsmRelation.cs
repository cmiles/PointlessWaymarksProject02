using System.Text.Json.Serialization;

namespace PointlessWaymarks.FeatureIntersectionTags.OsmOverpass;

/// <summary>
///     Relation element (multipolygon, route, etc.)
/// </summary>
public class OsmRelation : OsmElement
{
    [JsonPropertyName("members")] public List<OsmMember> Members { get; set; } = [];
}