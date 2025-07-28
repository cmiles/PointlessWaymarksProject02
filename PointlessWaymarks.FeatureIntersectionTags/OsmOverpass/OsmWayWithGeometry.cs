using System.Text.Json.Serialization;

namespace PointlessWaymarks.FeatureIntersectionTags.OsmOverpass;

/// <summary>
///     Way element (line or polygon)
/// </summary>
public class OsmWayWithGeometry : OsmElement
{
    /// <summary>
    ///     Available when querying with "out geom"
    /// </summary>
    [JsonPropertyName("geometry")]
    public List<OsmGeometry>? Geometry { get; set; }

    public List<OsmNode> GeometryNodes { get; set; } = [];

    [JsonPropertyName("nodes")] public List<long> Nodes { get; set; } = [];

    public static OsmWayWithGeometry FromOsmWay(OsmWay way, OsmResponse osmResponse, bool strictMode = false)
    {
        return FromOsmWay(way, osmResponse.Elements.OfType<OsmNode>().ToList(), strictMode);
    }

    public static OsmWayWithGeometry FromOsmWay(OsmWay way, List<OsmNode> nodeDetails, bool strictMode = false)
    {
        var nodeDetailList = new List<OsmNode>();

        if (way.Geometry is null || !way.Geometry.Any())
            return new OsmWayWithGeometry
            {
                Id = way.Id,
                Tags = way.Tags,
                Nodes = way.Nodes,
                Geometry = way.Geometry,
                GeometryNodes = nodeDetailList
            };

        var counter = 1;
        foreach (var loopGeometry in way.Geometry)
        {
            var existing = nodeDetailList.FirstOrDefault(n => n.Lat == loopGeometry.Lat && n.Lon == loopGeometry.Lon);

            if (existing is not null) nodeDetailList.Add(existing);

            nodeDetailList.Add(new OsmNode { Id = counter++, Lat = loopGeometry.Lat, Lon = loopGeometry.Lon });
        }

        return new OsmWayWithGeometry
        {
            Id = way.Id,
            Tags = way.Tags,
            Nodes = way.Nodes,
            Geometry = way.Geometry,
            GeometryNodes = nodeDetailList
        };
    }
}