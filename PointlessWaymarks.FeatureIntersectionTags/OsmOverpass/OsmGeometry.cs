using System.Text.Json.Serialization;

namespace PointlessWaymarks.FeatureIntersectionTags.OsmOverpass;

/// <summary>
///     Simplified node format used in way geometries
/// </summary>
public class OsmGeometry
{
    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lon")]
    public double Lon { get; set; }
}