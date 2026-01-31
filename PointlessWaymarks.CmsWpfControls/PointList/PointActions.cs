using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Xml;
using NetTopologySuite.Features;
using NetTopologySuite.IO;
using Ookii.Dialogs.Wpf;
using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.SpatialTools;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.CmsWpfControls.PointList;

public static class PointActions
{
    public static async Task CoordinateTextToClipboard(List<PointContentDto> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(loopSelected => $"{loopSelected.Latitude:F5},{loopSelected.Longitude:F5}")
            .ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static string DefaultBracketCode(PointContentDto? content)
    {
        return content is null ? string.Empty : $"{BracketCodePoints.Create(content.ToDbObject())}";
    }

    public static async Task DefaultBracketCodesToClipboard(List<PointContentDto> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(loopSelected => BracketCodePoints.Create(loopSelected.ToDbObject())).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task ExternalDirectionsBracketCodesToClipboard(List<PointContentDto> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents
            .Select(loopSelected => BracketCodePointExternalDirectionLinks.Create(loopSelected.ToDbObject())).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task GeoJsonToClipboard(List<PointContentDto> pointContents, StatusControlContext statusContext)
    {
        var featureList = new List<IFeature>();

        foreach (var loopSelected in pointContents)
        {
            var pointFeature = loopSelected.FeatureFromPoint();
            featureList.Add(pointFeature);
        }

        var finalString = await GeoJsonTools.SerializeListOfFeaturesCollectionToGeoJson(featureList);

        await ThreadSwitcher.ResumeForegroundAsync();

        Clipboard.SetText(finalString);

        await statusContext.ToastSuccess($"GeoJson Points To Clipboard for {pointContents.Count} Points");
    }

    public static async Task GoogleMapsBracketCodesToClipboard(List<PointContentDto> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents
            .Select(loopSelected => BracketCodePointGoogleMapsLinks.Create(loopSelected.ToDbObject())).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task ImageBracketCodesToClipboard(List<PointContentDto> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(loopSelected => BracketCodePointImageLink.Create(loopSelected.ToDbObject()))
            .ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task PointDetailsBracketCodesToClipboard(List<PointContentDto> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(loopSelected => BracketCodePointDetails.Create(loopSelected.ToDbObject()))
            .ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task ShowInGoogleMapsWeb(PointContentDto contents,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var mapUrl =
            $"https://www.google.com/maps/search/?api=1&query={contents.Latitude:F5},{contents.Longitude:F5}";

        await ThreadSwitcher.ResumeForegroundAsync();
        ProcessHelpers.OpenUrlInExternalBrowser(mapUrl);
    }

    public static async Task ShowInOsmCycleMap(PointContentDto contents,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var mapUrl =
            $"http://www.openstreetmap.org/?mlat={contents.Latitude:F5}&mlon={contents.Longitude:F5}&zoom=13&layers=C";

        await ThreadSwitcher.ResumeForegroundAsync();
        ProcessHelpers.OpenUrlInExternalBrowser(mapUrl);
    }

    private static async Task TextAndContentRepresentationToClipboard(List<PointContentDto> contents,
        string finalString, StatusControlContext statusContext)
    {
        await ContentClipboardRepresentation.TextAndContentRepresentationToClipboard(
            contents.Cast<IContentCommon>().ToList(), finalString, statusContext);
    }

    public static async Task TextBracketCodesToClipboard(List<PointContentDto> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(loopSelected => BracketCodePointLinks.Create(loopSelected.ToDbObject()))
            .ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task ToGpxFile(List<PointContentDto> pointContents, StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var fileDialog = new VistaSaveFileDialog
        {
            Filter = "gpx file (*.gpx)|*.gpx;",
            AddExtension = true,
            OverwritePrompt = true,
            DefaultExt = ".gpx"
        };
        var fileDialogResult = fileDialog.ShowDialog();

        if (!(fileDialogResult ?? false)) return;

        var fileName = fileDialog.FileName;

        await ThreadSwitcher.ResumeBackgroundAsync();

        var waypointList = new List<GpxWaypoint>();

        foreach (var loopItems in pointContents)
        {
            var toAdd = new GpxWaypoint(new GpxLongitude(loopItems.Longitude),
                new GpxLatitude(loopItems.Latitude),
                loopItems.Elevation?.FeetToMeters(),
                loopItems.LastUpdatedOn?.ToUniversalTime() ?? loopItems.CreatedOn.ToUniversalTime(),
                null, null,
                loopItems.Title, null, loopItems.Summary, null, [], null,
                null, null, null, null, null, null, null, null, null);
            waypointList.Add(toAdd);
        }

        var fileStream = new FileStream(fileName, FileMode.OpenOrCreate);

        var writerSettings = new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = true, CloseOutput = true };
        await using var xmlWriter = XmlWriter.Create(fileStream, writerSettings);
        GpxWriter.Write(xmlWriter, null, new GpxMetadata("Pointless Waymarks CMS"), waypointList, null, null, null);
        xmlWriter.Close();

        await ProcessHelpers.OpenExplorerWindowForFile(fileName);

        await statusContext.ToastSuccess($"File written to {fileName}");
    }
}