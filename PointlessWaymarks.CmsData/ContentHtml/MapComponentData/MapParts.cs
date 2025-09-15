using System.Collections.Immutable;
using System.Text;
using System.Xml;
using NetTopologySuite.IO;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.SpatialTools;
using Serilog;

namespace PointlessWaymarks.CmsData.ContentHtml.MapComponentData;

public static class MapParts
{
    public static string MapDivAndScript(MapComponent map)
    {
        return MapDivAndScript(map.ContentId);
    }

    public static string MapDivAndScript(Guid mapContentId)
    {
        var divScriptGuidConnector = Guid.NewGuid();

        var tag =
            $"""
             <div id="MapComponent-{divScriptGuidConnector}" class="leaflet-container leaflet-retina leaflet-fade-anim leaflet-grab leaflet-touch-drag point-content-map"  data-contentid="{mapContentId}">
             </div>
             """;

        var script =
            $"""
             <script>
                lazyInit(document.querySelector("#MapComponent-{divScriptGuidConnector}"), () => mapComponentInit(document.querySelector("#MapComponent-{divScriptGuidConnector}"), "{mapContentId}"));
             </script>
             """;

        return tag + script;
    }

    public static async Task WriteGpxData(MapComponentDto mapContent)
    {
        if (!mapContent.Elements.Any())
            return;

        var dataFileInfo = UserSettingsSingleton.CurrentSettings().LocalSiteMapGpxFile(mapContent.ToDbObject());

        List<GpxTrack> trackList = [];
        List<GpxWaypoint> waypointList = [];

        var db = await Db.Context();
        var mapElementContent =
            await db.ContentFromContentIds(mapContent.Elements.Select(x => x.ElementContentId).ToList(), false);


        foreach (var element in mapElementContent)
            try
            {
                // Process line content (tracks)
                if (element is LineContent lineContent && !string.IsNullOrWhiteSpace(lineContent.Line))
                {
                    trackList.AddRange(GpxTools.GpxTrackFromLineFeature(lineContent.FeatureFromGeoJsonLine()!,
                        lineContent.RecordingStartedOnUtc,
                        lineContent.Title ?? lineContent.RecordingEndedOnUtc?.ToString("yyyy MM dd") ??
                        lineContent.CreatedOn.ToString("yyyy MM dd"), string.Empty,
                        lineContent.Summary ?? string.Empty).AsList());
                }
                // Process point content (waypoints)
                else if (element is PointContent pointContent)
                {
                    var name = string.IsNullOrWhiteSpace(pointContent.MapLabel)
                        ? pointContent.Title ?? "Unnamed Point"
                        : pointContent.MapLabel;

                    var gpxPoint = new GpxWaypoint(new GpxLongitude(pointContent.Longitude),
                        new GpxLatitude(pointContent.Latitude), pointContent.Elevation?.FeetToMeters() ?? 0, null,
                        null, null, name, string.Empty, string.Empty, string.Empty, ImmutableArray<GpxWebLink>.Empty,
                        string.Empty, string.Empty, null, null, null, null, null, null, null, new object());

                    waypointList.Add(gpxPoint);
                }
                // Process anything with optional location (waypoints)
                else if (element is IOptionalLocation { Latitude: not null, Longitude: not null } optionalLocation)
                {
                    var contentCommon = element as IContentCommon;

                    var gpxPoint = new GpxWaypoint(new GpxLongitude(optionalLocation.Longitude.Value),
                        new GpxLatitude(optionalLocation.Latitude.Value),
                        optionalLocation.Elevation?.FeetToMeters() ?? 0, null, null, null,
                        contentCommon?.Title ?? "Unnamed Point", string.Empty, string.Empty, string.Empty,
                        ImmutableArray<GpxWebLink>.Empty, string.Empty, string.Empty, null, null, null, null, null,
                        null, null, new object());

                    waypointList.Add(gpxPoint);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing map element {ElementId} for GPX", element.ElementContentId);
            }

        var textStream = new Utf8StringWriter();

        var writerSettings = new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = true, CloseOutput = true };
        await using var xmlWriter = XmlWriter.Create(textStream, writerSettings);
        GpxWriter.Write(xmlWriter, new GpxWriterSettings(), new GpxMetadata("Pointless Waymarks CMS"), waypointList,
            null,
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