using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using PointlessWaymarks.CommonTools;

namespace PointlessWaymarks.FeatureIntersectionTags.Models;

public class IntersectResult
{
    public IntersectResult(IFeature feature)
    {
        Features = feature.AsList();
    }

    public IntersectResult(List<IFeature> features)
    {
        Features = features;
    }

    public Guid ContentId { get; init; } = Guid.Empty;
    public List<IFeature> Features { get; }
    public List<IntersectWithFeature> IntersectsWith { get; } = [];
    public List<Coordinate> OsmIsInPoints { get; } = [];
    public List<string> Sources { get; } = [];
    public List<string> Tags { get; } = [];
}