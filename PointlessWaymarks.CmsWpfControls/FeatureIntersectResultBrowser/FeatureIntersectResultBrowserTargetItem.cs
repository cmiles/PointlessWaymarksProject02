using Flurl.Util;
using NetTopologySuite.Features;
using PointlessWaymarks.SpatialTools;

namespace PointlessWaymarks.CmsWpfControls.FeatureIntersectResultBrowser;

public class FeatureIntersectResultBrowserTargetItem
{
    public required List<string> Attributes { get; set; }
    public required IFeature Feature { get; set; }
    public required string JsonString { get; set; }

    public required string Name { get; set; }

    public static async Task<FeatureIntersectResultBrowserTargetItem> CreateInstance(IFeature feature, string name)
    {
        var jsonString = await GeoJsonTools.SerializeFeatureToGeoJson(feature);

        var attributesList = new List<string>();

        foreach (var loopAttributes in feature.Attributes.ToKeyValuePairs())
            attributesList.Add(loopAttributes.Key + ":" + loopAttributes.Value);

        return new FeatureIntersectResultBrowserTargetItem
            { Attributes = attributesList, Feature = feature, JsonString = jsonString, Name = name };
    }
}