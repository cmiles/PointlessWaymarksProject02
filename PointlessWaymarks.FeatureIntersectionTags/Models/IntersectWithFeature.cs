using NetTopologySuite.Features;

namespace PointlessWaymarks.FeatureIntersectionTags.Models;

public class IntersectWithFeature
{
    public required IFeature Feature { get; set; }
    public required string Source { get; set; }
    public required List<string> Tags { get; set; }
}