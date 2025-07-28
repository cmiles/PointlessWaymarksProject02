using System.ComponentModel;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.FeatureIntersectionTags.Models;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.SpatialTools;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.WebViewVirtualDomain;
using PointlessWaymarks.WpfCommon.WpfHtml;
using Feature = NetTopologySuite.Features.Feature;

namespace PointlessWaymarks.GeoToolsGui.Controls;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class FeatureIntersectResultBrowserContext
{
    public FeatureIntersectResultBrowserContext(StatusControlContext statusContext, IntersectResult intersectResult)
    {
        StatusContext = statusContext;
        Intersect = intersectResult;

        PropertyChanged += OnPropertyChanged;
    }

    public List<FeatureIntersectResultBrowserTargetItem> FeatureItems { get; set; } = [];
    public IntersectResult Intersect { get; set; }
    public List<FeatureIntersectResultListItem> IntersectItems { get; set; } = [];
    public WebViewMessenger ResultMap { get; set; } = new();
    public FeatureIntersectResultListItem? SelectedIntersectItem { get; set; }
    public StatusControlContext StatusContext { get; }
    public WebViewMessenger TargetMap { get; set; } = new();


    public static async Task<FeatureIntersectResultBrowserContext> CreateInstance(
        StatusControlContext? statusContext,
        IntersectResult intersectResult)
    {
        await ThreadSwitcher.ResumeForegroundAsync();
        var control =
            new FeatureIntersectResultBrowserContext(statusContext ?? new StatusControlContext(), intersectResult);
        await control.Load(intersectResult);
        return control;
    }

    public async Task Load(IntersectResult intersectResult)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();
        var counter = 0;
        FeatureItems = await intersectResult.Features
            .SelectInSequenceAsync(async x =>
                await FeatureIntersectResultBrowserTargetItem.CreateInstance(x, counter++.ToString()));
        IntersectItems = await intersectResult.IntersectsWith
            .SelectInSequenceAsync(async x => await FeatureIntersectResultListItem.CreateInstance(x));
        ResultMap.SetupCmsLeafletMapHtmlAndJs("Result", 32.12063, -110.52313, true);
        TargetMap.SetupCmsLeafletMapHtmlAndJs("Target", 32.12063, -110.52313, true);

        var targetFeatures = FeatureItems.Select(x => x.Feature as Feature).Where(x => x is not null).Cast<Feature>()
            .ToList();

        if (targetFeatures.Any())
        {
            var targetFeatureCollection = new FeatureCollection(targetFeatures);
            var targetBounds = GeoJsonTools.GeometryBoundingBox(targetFeatureCollection);
            var targetJsonDto = await MapJson.NewMapFeatureCollectionDtoSerialized(targetFeatureCollection.AsList(),
                SpatialBounds.FromEnvelope(targetBounds));

            TargetMap.ToWebView.Enqueue(JsonData.CreateRequest(targetJsonDto));
        }

        if (IntersectItems.Any())
            SelectedIntersectItem = IntersectItems.First();
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)) return;

        if (e.PropertyName.Equals(nameof(SelectedIntersectItem)))
            StatusContext.RunFireAndForgetNonBlockingTask(async () =>
            {
                await ThreadSwitcher.ResumeBackgroundAsync();

                if (SelectedIntersectItem?.Result.Feature is not Feature feature)
                {
                    ResultMap.ToWebView.Enqueue(JsonData.CreateRequest(
                        await ResetMapGeoJsonDto()));
                    return;
                }

                var bounds = GeoJsonTools.GeometryBoundingBox(feature.Geometry.AsList());

                var collection = new FeatureCollection(feature.AsList());

                var jsonDto = await MapJson.NewMapFeatureCollectionDtoSerialized(collection.AsList(),
                    SpatialBounds.FromEnvelope(bounds));

                ResultMap.ToWebView.Enqueue(JsonData.CreateRequest(jsonDto));
            });
    }

    private async Task<string> ResetMapGeoJsonDto()
    {
        var features = new FeatureCollection();

        var basePoint = PointTools.Wgs84Point(-110.52313, 32.12063);
        var bounds = new Envelope();
        bounds.ExpandToInclude(basePoint.Coordinate);
        bounds.ExpandBy(1000);

        var jsonDto = await MapJson.NewMapFeatureCollectionDtoSerialized(features.AsList(),
            SpatialBounds.FromEnvelope(bounds));

        return await GeoJsonTools.SerializeWithGeoJsonSerializer(jsonDto);
    }
}