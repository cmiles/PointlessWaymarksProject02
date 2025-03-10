using System.Text;
using System.Xml;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsData.Json;
using PointlessWaymarks.CmsData.Spatial;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.SpatialTools;
using Serilog;
using SimMetricsCore;

namespace PointlessWaymarks.CmsData.ContentHtml.LineHtml;

public static class LineData
{
    private static string ContentReferenceDescriptions(Guid? imageContentId,
        string? title,
        string? summary)
    {
        if (imageContentId == null) return string.Empty;

        var description = string.Empty;

        title = title.TrimNullToEmpty();
        summary = summary.TrimNullToEmpty();

        var smallImageUrl = UserSettingsSingleton.CurrentSettings().PictureSmallUrl(imageContentId.Value);

        if (!string.IsNullOrWhiteSpace(smallImageUrl))
            description += $"""
                            <img src="{smallImageUrl}"/>
                            """;

        if (!string.IsNullOrWhiteSpace(summary)
            && title.GetSimilarity(summary, SimMetricType.JaroWinkler) < .9 &&
            !(title.ContainsFuzzy(summary, 0.8, SimMetricType.JaroWinkler)
              || summary.ContainsFuzzy(title, 0.8, SimMetricType.JaroWinkler)))
            description += $" <p>{summary}</p>";

        return description;
    }

    public static async Task<(SpatialBounds bounds, List<FeatureCollection> featureList)>
        GenerateGeoJsonDataFromContent(
            List<object> dbEntries)
    {
        var geoJsonList = new List<FeatureCollection>();

        var boundsKeeper = new List<Point>();

        foreach (var loopElements in dbEntries)
            switch (loopElements)
            {
                case GeoJsonContent { GeoJson: not null } mapGeoJson:
                    var featureCollection =
                        GeoJsonTools.DeserializeStringToFeatureCollection(mapGeoJson.GeoJson);
                    foreach (var feature in featureCollection)
                        feature.Attributes.Add("displayId", mapGeoJson.ContentId);
                    geoJsonList.Add(featureCollection);
                    boundsKeeper.Add(new Point(mapGeoJson.InitialViewBoundsMaxLongitude,
                        mapGeoJson.InitialViewBoundsMaxLatitude));
                    boundsKeeper.Add(new Point(mapGeoJson.InitialViewBoundsMinLongitude,
                        mapGeoJson.InitialViewBoundsMinLatitude));
                    break;
                case LineContent mapLine:
                    var lineFeatureCollection = GeoJsonTools.DeserializeStringToFeatureCollection(mapLine.Line);
                    var line = lineFeatureCollection[0];
                    line.Attributes.Add("displayId", mapLine.ContentId);
                    if (!line.Attributes.Exists("description")) line.Attributes.Add("description", string.Empty);

                    var descriptionAndImage =
                        ContentReferenceDescriptions(mapLine.MainPicture, mapLine.Title, mapLine.Summary);

                    line.Attributes["description"] = descriptionAndImage;

                    if (!line.Attributes.Exists("title")) line.Attributes.Add("title", string.Empty);

                    line.Attributes["title"] =
                        $"""<a href="{UserSettingsSingleton.CurrentSettings().LinePageUrl(mapLine)}">{(string.IsNullOrWhiteSpace(mapLine.Title) ? "View" : mapLine.Title)}</a>""";

                    geoJsonList.Add(lineFeatureCollection);
                    boundsKeeper.Add(new Point(mapLine.InitialViewBoundsMaxLongitude,
                        mapLine.InitialViewBoundsMaxLatitude));
                    boundsKeeper.Add(new Point(mapLine.InitialViewBoundsMinLongitude,
                        mapLine.InitialViewBoundsMinLatitude));
                    break;
            }

        var db = await Db.Context();

        var points = await dbEntries.Where(x => x is PointContent).Cast<PointContent>()
            .SelectInSequenceAsync(async x => await Db.PointContentDtoFromPoint(x, db));

        var pointDtos = dbEntries.Where(x => x is PointContentDto).Cast<PointContentDto>().Union(points)
            .OrderBy(x => x.Title).ToList();

        if (pointDtos.Any())
        {
            var featureCollection = new FeatureCollection();

            foreach (var loopElements in pointDtos)
            {
                var descriptionAndImage =
                    ContentReferenceDescriptions(loopElements.MainPicture, loopElements.Title, loopElements.Summary);

                featureCollection.Add(new Feature(
                    PointTools.Wgs84Point(loopElements.Longitude, loopElements.Latitude,
                        loopElements.Elevation.FeetToMeters()),
                    new AttributesTable(new Dictionary<string, object>
                    {
                        {
                            "title",
                            $"""<a href="{UserSettingsSingleton.CurrentSettings().PointPageUrl(loopElements)}">{(string.IsNullOrWhiteSpace(loopElements.Title) ? "View" : loopElements.Title)}</a>"""
                        },
                        { "description", descriptionAndImage },
                        { "displayId", loopElements.ContentId }
                    })));
                boundsKeeper.Add(new Point(loopElements.Longitude, loopElements.Latitude));
            }

            geoJsonList.Add(featureCollection);
        }


        var photos = dbEntries.Where(x => x is PhotoContent).Cast<PhotoContent>()
            .Where(x => x.Latitude is not null && x.Longitude is not null).OrderBy(x => x.Title).ToList();

        if (photos.Count != 0)
        {
            var featureCollection = new FeatureCollection();

            foreach (var loopElements in photos)
            {
                var descriptionAndImage =
                    ContentReferenceDescriptions(loopElements.MainPicture, loopElements.Title, loopElements.Summary);

                featureCollection.Add(new Feature(
                    PointTools.Wgs84Point(loopElements.Longitude!.Value, loopElements.Latitude!.Value,
                        loopElements.Elevation?.FeetToMeters() ?? 0),
                    new AttributesTable(new Dictionary<string, object>
                    {
                        {
                            "title",
                            $"""<a href="{UserSettingsSingleton.CurrentSettings().PhotoPageUrl(loopElements)}">{(string.IsNullOrWhiteSpace(loopElements.Title) ? "View" : loopElements.Title)}</a>"""
                        },
                        { "description", descriptionAndImage },
                        { "displayId", loopElements.ContentId }
                    })));
                boundsKeeper.Add(new Point(loopElements.Longitude.Value, loopElements.Latitude.Value));
            }

            geoJsonList.Add(featureCollection);
        }

        var contentBounds = SpatialConverters.PointBoundingBox(boundsKeeper);

        return (SpatialBounds.FromEnvelope(contentBounds), geoJsonList);
    }

    public static List<LineElevationChartDataPoint> GenerateLineElevationDataList(LineContent lineContent)
    {
        if (string.IsNullOrWhiteSpace(lineContent.Line)) return [];

        return LineTools.ElevationChartDataFromGeoJsonFeatureCollectionWithLinestring(lineContent.Line);
    }

    public static async Task<string> GenerateSerializedGeoJsonDataFromBodyContentReferences(LineContent lineContent)
    {
        var toSerialize = await GeoJsonDataFromBodyContentReferences(lineContent);

        return await GeoJsonTools.SerializeWithGeoJsonSerializer(toSerialize);
    }

    public static async Task<(SpatialBounds bounds, FeatureCollection featureList)>
        GeoJsonDataFromBodyContentReferences(LineContent lineContent)
    {
        if (!lineContent.ShowContentReferencesOnMap)
            return (new SpatialBounds(0, 0, 0, 0), []);

        var photos = (await BracketCodePhotos.DbContentFromBracketCodes(lineContent.BodyContent))
            .Where(x => x.ShowLocation).Cast<object>();
        var photosWithDetails = (await BracketCodePhotosWithDetails.DbContentFromBracketCodes(lineContent.BodyContent))
            .Where(x => x.ShowLocation).Cast<object>();
        var photoLinks = (await BracketCodePhotoLinks.DbContentFromBracketCodes(lineContent.BodyContent))
            .Where(x => x.ShowLocation)
            .Cast<object>();
        var points = (await BracketCodePoints.DbContentFromBracketCodes(lineContent.BodyContent)).Cast<object>();
        var pointLinks = (await BracketCodePointLinks.DbContentFromBracketCodes(lineContent.BodyContent))
            .Cast<object>();
        var pointExternalDirectionsLinks =
            (await BracketCodePointExternalDirectionLinks.DbContentFromBracketCodes(lineContent.BodyContent))
            .Cast<object>();
        var pointDetailsLinks = (await BracketCodePointDetails.DbContentFromBracketCodes(lineContent.BodyContent))
            .Cast<object>();
        var geoJson = (await BracketCodeGeoJson.DbContentFromBracketCodes(lineContent.BodyContent)).Cast<object>();
        var geoJsonLinks =
            (await BracketCodeGeoJsonLinks.DbContentFromBracketCodes(lineContent.BodyContent)).Cast<object>();

        var mapInformation = await GenerateGeoJsonDataFromContent(photos.Concat(photosWithDetails).Concat(photoLinks)
            .Concat(points)
            .Concat(pointExternalDirectionsLinks)
            .Concat(pointDetailsLinks).Concat(pointLinks).Concat(geoJson).Concat(geoJsonLinks).ToList());

        var toSerialize = new FeatureCollection
        {
            BoundingBox = mapInformation.bounds.ToEnvelope()
        };

        foreach (var loopFeatureCollection in mapInformation.featureList)
        foreach (var loopFeature in loopFeatureCollection)
            toSerialize.Add(loopFeature);

        return (mapInformation.bounds, toSerialize);
    }

    public static async Task<SpatialContentReferences>
        SpatialContentIdReferencesFromBodyContentReferences(LineContent lineContent)
    {
        if (!lineContent.ShowContentReferencesOnMap)
            return new SpatialContentReferences([], [], [],
                [], [], [], [], []);

        var files = new List<List<FileContent>>
        {
            (await BracketCodeFiles.DbContentFromBracketCodes(lineContent.BodyContent))
            .Where(x => x.HasLocation()).ToList(),
            (await BracketCodeFileDownloads.DbContentFromBracketCodes(lineContent.BodyContent))
            .Where(x => x.HasLocation())
            .OrderBy(x => x.Title).ToList(),
            (await BracketCodeFileEmbed.DbContentFromBracketCodes(lineContent.BodyContent))
            .Where(x => x.HasLocation())
            .OrderBy(x => x.Title).ToList(),
            (await BracketCodeFileImageLink.DbContentFromBracketCodes(lineContent.BodyContent))
            .Where(x => x.HasLocation())
            .OrderBy(x => x.Title).ToList()
        };
        var filesAllReferences = files.SelectMany(x => x).OrderBy(x => x.Title).Select(x => x.ContentId).Distinct()
            .ToList();

        var geojson = (await BracketCodeGeoJson.DbContentFromBracketCodes(lineContent.BodyContent)).ToList();
        var geojsonImageLinks = (await BracketCodeGeoJsonImageLink.DbContentFromBracketCodes(lineContent.BodyContent))
            .OrderBy(x => x.Title).ToList();
        var geojsonLinks = (await BracketCodeGeoJsonLinks.DbContentFromBracketCodes(lineContent.BodyContent))
            .OrderBy(x => x.Title).ToList();
        var geojsonAllReferences = geojson.Concat(geojsonImageLinks).Concat(geojsonLinks).OrderBy(x => x.Title)
            .Select(x => x.ContentId).Distinct()
            .ToList();

        var images = (await BracketCodeImages.DbContentFromBracketCodes(lineContent.BodyContent))
            .Where(x => x.HasLocation()).ToList();
        var imageLinks = (await BracketCodeImageLinks.DbContentFromBracketCodes(lineContent.BodyContent))
            .Where(x => x.HasLocation())
            .OrderBy(x => x.Title).ToList();
        var imageAllReferences = images.Concat(imageLinks).OrderBy(x => x.Title).Select(x => x.ContentId).Distinct()
            .ToList();

        var lineAllReferences = (await BracketCodeLineLinks.DbContentFromBracketCodes(lineContent.BodyContent))
            .OrderBy(x => x.Title).Select(x => x.ContentId).Distinct().ToList();

        var photos = (await BracketCodePhotos.DbContentFromBracketCodes(lineContent.BodyContent))
            .Where(x => x.HasLocation()).ToList();
        var photosWithDetails = (await BracketCodePhotosWithDetails.DbContentFromBracketCodes(lineContent.BodyContent))
            .Where(x => x.HasLocation()).ToList();
        var photoLinks = (await BracketCodePhotoLinks.DbContentFromBracketCodes(lineContent.BodyContent))
            .Where(x => x.HasLocation())
            .OrderBy(x => x.Title).ToList();
        var photoAllReferences = photos.Concat(photosWithDetails).Concat(photoLinks).OrderBy(x => x.Title)
            .Select(x => x.ContentId).Distinct()
            .ToList();

        var posts = (await BracketCodePosts.DbContentFromBracketCodes(lineContent.BodyContent))
            .Where(x => x.HasLocation()).ToList();
        var postLinks = (await BracketCodePostImageLink.DbContentFromBracketCodes(lineContent.BodyContent))
            .Where(x => x.HasLocation())
            .OrderBy(x => x.Title).ToList();
        var postAllReferences = posts.Concat(postLinks).OrderBy(x => x.Title).Select(x => x.ContentId).Distinct()
            .ToList();

        var points = (await BracketCodePoints.DbContentFromBracketCodes(lineContent.BodyContent)).ToList();
        var pointExternalDirections =
            (await BracketCodePointExternalDirectionLinks.DbContentFromBracketCodes(lineContent.BodyContent)).ToList();
        var pointDetails = (await BracketCodePointDetails.DbContentFromBracketCodes(lineContent.BodyContent)).ToList();
        var pointImageLinks = (await BracketCodePointImageLink.DbContentFromBracketCodes(lineContent.BodyContent))
            .OrderBy(x => x.Title).ToList();
        var pointLinks = (await BracketCodePointLinks.DbContentFromBracketCodes(lineContent.BodyContent))
            .OrderBy(x => x.Title).ToList();
        var pointAllReferences = points.Concat(pointImageLinks).Concat(pointLinks).Concat(pointExternalDirections)
            .Concat(pointDetails).OrderBy(x => x.Title).Select(x => x.ContentId).Distinct()
            .ToList();

        var videos = (await BracketCodeVideoEmbed.DbContentFromBracketCodes(lineContent.BodyContent))
            .Where(x => x.HasLocation()).ToList();
        var videoLinks = (await BracketCodeVideoLinks.DbContentFromBracketCodes(lineContent.BodyContent))
            .Where(x => x.HasLocation())
            .OrderBy(x => x.Title).ToList();
        var videoImageLinks = (await BracketCodeVideoImageLink.DbContentFromBracketCodes(lineContent.BodyContent))
            .Where(x => x.HasLocation())
            .OrderBy(x => x.Title).ToList();
        var videoAllReferences = videos.Concat(videoLinks).Concat(videoImageLinks).OrderBy(x => x.Title)
            .Select(x => x.ContentId).Distinct()
            .ToList();

        return new SpatialContentReferences(SpatialContentReference.FromListOfContentIds(filesAllReferences),
            SpatialContentReference.FromListOfContentIds(geojsonAllReferences),
            SpatialContentReference.FromListOfContentIds(imageAllReferences),
            SpatialContentReference.FromListOfContentIds(lineAllReferences),
            SpatialContentReference.FromListOfContentIds(photoAllReferences),
            SpatialContentReference.FromListOfContentIds(pointAllReferences),
            SpatialContentReference.FromListOfContentIds(postAllReferences),
            SpatialContentReference.FromListOfContentIds(videoAllReferences));
    }

    public static async Task WriteGpxData(LineContent lineContent)
    {
        if (string.IsNullOrWhiteSpace(lineContent.Line))
            throw new ArgumentException(
                "WriteGpxData in LineData was given a LineContent with a null/blank/empty Line");

        var dataFileInfo = UserSettingsSingleton.CurrentSettings().LocalSiteLineGpxFile(lineContent);

        var trackList = GpxTools.GpxTrackFromLineFeature(lineContent.FeatureFromGeoJsonLine()!,
            lineContent.RecordingStartedOnUtc,
            lineContent.Title ?? lineContent.RecordingEndedOnUtc?.ToString("yyyy MM dd") ??
            lineContent.CreatedOn.ToString("yyyy MM dd"), string.Empty,
            lineContent.Summary ?? string.Empty).AsList();

        var textStream = new Utf8StringWriter();

        var writerSettings = new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = true, CloseOutput = true };
        await using var xmlWriter = XmlWriter.Create(textStream, writerSettings);
        GpxWriter.Write(xmlWriter, new GpxWriterSettings(), new GpxMetadata("Pointless Waymarks CMS"), null, null,
            trackList, null);
        xmlWriter.Close();

        var temporaryGpxFile = UniqueFileTools.UniqueFile(FileLocationTools.TempStorageDirectory(),
            $"GpxDataTemp-{Guid.NewGuid()}.gpx");

        await File.WriteAllTextAsync(temporaryGpxFile!.FullName, textStream.ToString());
        temporaryGpxFile.Refresh();

        if (dataFileInfo.Exists)
        {
            var temporaryMd5 = temporaryGpxFile.CalculateMD5();
            var onDiskMd5 = dataFileInfo.CalculateMD5();

            if (temporaryMd5 == onDiskMd5)
            {
                try
                {
                    temporaryGpxFile.Delete();
                }
                catch (Exception e)
                {
                    Log.ForContext("exception", e.ToString())
                        .Debug("Ignored Temporary File Delete Exception in {methodName}", nameof(WriteGpxData));
                }

                return;
            }
        }

        if (dataFileInfo.Exists)
        {
            dataFileInfo.Delete();
            dataFileInfo.Refresh();
        }

        await FileManagement.MoveFileAndLogAsync(temporaryGpxFile.FullName, dataFileInfo.FullName);
    }
}