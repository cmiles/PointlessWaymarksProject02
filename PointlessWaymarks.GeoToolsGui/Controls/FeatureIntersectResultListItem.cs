using PointlessWaymarks.FeatureIntersectionTags.Models;
using PointlessWaymarks.SpatialTools;

namespace PointlessWaymarks.GeoToolsGui.Controls;

public class FeatureIntersectResultListItem
{
    public required string JsonString { get; set; }
    public required IntersectWithFeature Result { get; set; }
    public required string TagString { get; set; }

    public static async Task<FeatureIntersectResultListItem> CreateInstance(IntersectWithFeature result)
    {
        var jsonString = await GeoJsonTools.SerializeFeatureToGeoJson(result.Feature);

        var tagString = string.Join(", ", result.Tags.Distinct().OrderBy(x => x));

        return new FeatureIntersectResultListItem
            { JsonString = jsonString, TagString = tagString, Result = result };
    }
}