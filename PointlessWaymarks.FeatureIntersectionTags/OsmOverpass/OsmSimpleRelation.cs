using NetTopologySuite.Features;

namespace PointlessWaymarks.FeatureIntersectionTags.OsmOverpass;

public class OsmSimpleRelation
{
    public long Id { get; set; }
    private List<OsmNode> Nodes { get; set; } = [];
    public required OsmRelation Relation { get; set; }
    public Dictionary<string, string> Tags { get; set; } = [];
    public List<OsmWayWithGeometry> Ways { get; set; } = [];

    public List<IFeature> Features()
    {
        var features = new List<IFeature>();

        foreach (var node in Nodes)
        {
            var feature = OsmGeometryHelpers.NodeToFeatureAndReplaceTags(node, Tags);
            features.Add(feature);
        }

        foreach (var way in Ways)
        {
            var feature = OsmGeometryHelpers.WayToFeatureAndReplaceTags(way, Tags);
            if (feature is not null) features.Add(feature);
        }

        return features;
    }


    /// <summary>
    ///     Determines if this relation represents a complex multipolygon
    /// </summary>
    public static bool RelationIsComplexMultipolygon(OsmRelation toSearch)
    {
        // Check if it's a multipolygon or boundary relation type
        if (!toSearch.Tags.TryGetValue("type", out var type) ||
            (type != "multipolygon" && type != "boundary"))
            return false;

        // Count members with outer and inner roles
        var outerCount = toSearch.Members.Count(w => w.Role == "outer");
        var innerCount = toSearch.Members.Count(w => w.Role == "inner");

        // It's complex if it has multiple outer rings or any inner rings
        return outerCount > 1 || innerCount > 0;
    }

    /// <summary>
    ///     Extracts route relations from an OSM response
    /// </summary>
    public static List<OsmSimpleRelation> SimpleRelationsFromResponse(OsmResponse response, List<string> tagFilters)
    {
        var result = new List<OsmSimpleRelation>();

        // Find all relations of type "route"
        var relations = response.Elements
            .OfType<OsmRelation>()
            .Where(r => !RelationIsComplexMultipolygon(r) && !OsmIntersection.IsOsmElementFiltered(r, tagFilters));

        foreach (var relation in relations)
        {
            var simpleRelation = new OsmSimpleRelation
            {
                Relation = relation,
                Id = relation.Id,
                Tags = relation.Tags
            };

            var wayIds = relation.Members
                .Where(m => m.Type == "way")
                .Select(m => m.Ref)
                .ToList();

            var nodeIds = relation.Members
                .Where(m => m.Type == "node")
                .Select(m => m.Ref)
                .ToList();

            // Get the actual ways from the response
            var ways = response.Elements
                .OfType<OsmWay>()
                .Where(w => wayIds.Contains(w.Id) && !OsmIntersection.IsOsmElementFiltered(w, tagFilters))
                .Select(w => OsmWayWithGeometry.FromOsmWay(
                    w,
                    response.Elements.OfType<OsmNode>().ToList()))
                .ToList();

            var nodes = response.Elements
                .OfType<OsmNode>()
                .Where(n => nodeIds.Contains(n.Id) && !OsmIntersection.IsOsmElementFiltered(n, tagFilters))
                .ToList();

            if (ways.Any() || nodes.Any())
            {
                simpleRelation.Ways = ways;
                simpleRelation.Nodes = nodes;

                result.Add(simpleRelation);
            }
        }

        return result;
    }
}