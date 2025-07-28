using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using PointlessWaymarks.SpatialTools;

namespace PointlessWaymarks.FeatureIntersectionTags.OsmOverpass;

public static class OsmGeometryHelpers
{
    public static IAttributesTable ConvertOsmTagsToAttributesTable(OsmElement osmObject)
    {
        return new AttributesTable(osmObject.Tags.ToDictionary(t => t.Key, t => t.Value as object));
    }

    public static IAttributesTable ConvertOsmTagsToAttributesTable(Dictionary<string, string> osmTags)
    {
        return new AttributesTable(osmTags.ToDictionary(t => t.Key, t => t.Value as object));
    }

    public static Geometry GetGeometryFromOsmNodes(OsmNode[] nodes, bool closePolygons)
    {
        var coordinates = nodes.Select(OsmNodeToCoordinate).ToArray();
        return nodes.First().Id == nodes.Last().Id && nodes.Length >= 4 && closePolygons
            ? GeoJsonTools.Wgs84GeometryFactory()
                .CreatePolygon(GeoJsonTools.Wgs84GeometryFactory().CreateLinearRing(coordinates))
            : GeoJsonTools.Wgs84GeometryFactory().CreateLineString(coordinates);
    }

    public static IFeature NodeToFeature(OsmNode node)
    {
        return new Feature(GeoJsonTools.Wgs84GeometryFactory().CreatePoint(OsmNodeToCoordinate(node)),
            ConvertOsmTagsToAttributesTable(node));
    }

    public static IFeature NodeToFeatureAndReplaceTags(OsmNode node, Dictionary<string, string> tags)
    {
        return new Feature(GeoJsonTools.Wgs84GeometryFactory().CreatePoint(OsmNodeToCoordinate(node)),
            ConvertOsmTagsToAttributesTable(tags));
    }

    private static Coordinate OsmNodeToCoordinate(OsmNode node)
    {
        return new Coordinate(GeoJsonTools.Wgs84GeometryFactory().PrecisionModel.MakePrecise(node.Lon),
            GeoJsonTools.Wgs84GeometryFactory().PrecisionModel.MakePrecise(node.Lat));
    }

    public static IFeature? WayToFeature(OsmWayWithGeometry way)
    {
        if (way.GeometryNodes.Count <= 1) return null;
        return new Feature(GetGeometryFromOsmNodes(way.GeometryNodes.ToArray(), true),
            ConvertOsmTagsToAttributesTable(way));
    }

    public static IFeature? WayToFeatureAndReplaceTags(OsmWayWithGeometry way, Dictionary<string, string> tags)
    {
        if (way.GeometryNodes.Count <= 1) return null;
        return new Feature(GetGeometryFromOsmNodes(way.GeometryNodes.ToArray(), true),
            ConvertOsmTagsToAttributesTable(tags));
    }
}