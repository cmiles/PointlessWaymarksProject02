using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Xml;
using NetTopologySuite.Features;
using NetTopologySuite.IO;
using Ookii.Dialogs.Wpf;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.ContentGeneration;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.CmsWpfControls.FeatureIntersectResultBrowser;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.FeatureIntersectionTags;
using PointlessWaymarks.FeatureIntersectionTags.Models;
using PointlessWaymarks.SpatialTools;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;
using Serilog;

namespace PointlessWaymarks.CmsWpfControls.PointList;

public static class PointActions
{
    public static async Task AddIntersectionTags(List<PointContentDto> contents,
        StatusControlContext statusContext, bool includeOsm, CancellationToken cancellationToken)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (string.IsNullOrWhiteSpace(UserSettingsSingleton.CurrentSettings().FeatureIntersectionTagSettingsFile))
        {
            await statusContext.ToastError("The Settings File for the Feature Intersection is blank?");
            return;
        }

        var settingsFileInfo = new FileInfo(UserSettingsSingleton.CurrentSettings().FeatureIntersectionTagSettingsFile);
        if (!settingsFileInfo.Exists)
        {
            await statusContext.ToastError(
                $"The Settings File for the Feature Intersection {settingsFileInfo.FullName} doesn't exist?");
            return;
        }

        var errorList = new List<string>();
        var successList = new List<string>();
        var noTagsList = new List<string>();

        var processedCount = 0;

        cancellationToken.ThrowIfCancellationRequested();

        List<PointContentDto> dbEntriesToProcess = [];
        List<IntersectResult> intersectResults = [];

        var settings =
            JsonSerializer.Deserialize<IntersectSettings>(await File.ReadAllTextAsync(settingsFileInfo.FullName,
                cancellationToken));
        if (settings == null)
        {
            statusContext.Progress(
                $"The settings file {settingsFileInfo.FullName} did not deserialized to valid settings...");
            return;
        }

        settings.UseOsmOverpass = includeOsm;
        settings.OsmInTagging = includeOsm;

        foreach (var loopSelected in contents)
        {
            var feature = settings.BufferPointsAndLinesByFeet > 0
                ? loopSelected.FeatureFromPointAsCircle(settings.BufferPointsAndLinesByFeet.Value)
                : loopSelected.FeatureFromPoint();

            dbEntriesToProcess.Add(loopSelected);

            var intersectResult = new IntersectResult(feature)
            {
                ContentId = loopSelected.ContentId, Description = $"Point Content - {loopSelected.Title ?? "No Title"}"
            };

            intersectResult.OsmIsInPoints.AddRange(loopSelected.PointFromLatitudeLongitude().Coordinate);

            intersectResults.Add(intersectResult);
        }

        await intersectResults.IntersectionTags(settings,
            cancellationToken, statusContext.ProgressTracker());

        var updateTime = DateTime.Now;

        foreach (var loopSelected in dbEntriesToProcess)
        {
            processedCount++;

            try
            {
                var taggerResult = intersectResults.Single(x => x.ContentId == loopSelected.ContentId);

                if (!taggerResult.Tags.Any())
                {
                    noTagsList.Add($"{loopSelected.Title} - no tags found");
                    statusContext.Progress(
                        $"Processed - {loopSelected.Title} - no tags found - Point {processedCount} of {contents.Count}");
                    continue;
                }

                var tagListForIntersection = SlugTagTools.TagListParseToSpacedString(loopSelected.Tags);
                tagListForIntersection.AddRange(taggerResult.Tags);
                loopSelected.Tags = SlugTagTools.TagListJoinToSpacedString(tagListForIntersection);
                loopSelected.LastUpdatedBy = "Feature Intersection Tagger";
                loopSelected.LastUpdatedOn = updateTime;

                var (saveGenerationReturn, _) =
                    await PointGenerator.SaveAndGenerateHtml(loopSelected, DateTime.Now,
                        statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError)
                    //TODO: Need alerting on this that would actually be seen...
                {
                    Log.ForContext("generationError", saveGenerationReturn.GenerationNote)
                        .ForContext("generationException", saveGenerationReturn.Exception?.ToString() ?? string.Empty)
                        .Error(
                            "Point Save Error during Selected Point Feature Intersection Tagging");
                    errorList.Add(
                        $"Save Failed! Point: {loopSelected.Title}, {saveGenerationReturn.GenerationNote}");
                    continue;
                }

                successList.Add(
                    $"{loopSelected.Title} - found Tags {string.Join(", ", taggerResult.Tags)}");
                statusContext.Progress(
                    $"Processed - {loopSelected.Title} - found Tags {string.Join(", ", taggerResult.Tags)} - Point {processedCount} of {contents.Count}");
            }
            catch (Exception e)
            {
                Log.Error(e,
                    $"Point Save Error during Selected Point Feature Intersection Tagging {loopSelected.Title}, {loopSelected.ContentId}");
                errorList.Add(
                    $"Save Failed! Point: {loopSelected.Title}, {e.Message}");
            }

            if (cancellationToken.IsCancellationRequested) break;
        }

        if (errorList.Any())
        {
            var bodyBuilder = new StringBuilder();
            bodyBuilder.AppendLine(
                $"There were errors getting Feature Intersection Tags and saving items - Errors: {errorList.Count}, Success: {successList.Count}, No Tags: {noTagsList.Count}.");
            bodyBuilder.AppendLine();
            bodyBuilder.AppendFormat("Errors:");
            bodyBuilder.AppendLine(string.Join(Environment.NewLine, errorList));
            bodyBuilder.AppendLine();
            bodyBuilder.AppendFormat("Successes:");
            bodyBuilder.AppendLine(string.Join(Environment.NewLine, successList));
            bodyBuilder.AppendLine();
            bodyBuilder.AppendFormat("No Tags Found:");
            bodyBuilder.AppendLine(string.Join(Environment.NewLine, noTagsList));

            await statusContext.ShowMessageWithOkButton("Feature Intersection Errors", bodyBuilder.ToString());
        }
    }

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

    public static async Task ShowIntersectionTagsForSelected(List<PointContentDto> contents,
        StatusControlContext statusContext, CancellationToken cancellationToken)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (string.IsNullOrWhiteSpace(UserSettingsSingleton.CurrentSettings().FeatureIntersectionTagSettingsFile))
        {
            await statusContext.ToastError("The Settings File for the Feature Intersection is blank?");
            return;
        }

        var settingsFileInfo = new FileInfo(UserSettingsSingleton.CurrentSettings().FeatureIntersectionTagSettingsFile);
        if (!settingsFileInfo.Exists)
        {
            await statusContext.ToastError(
                $"The Settings File for the Feature Intersection {settingsFileInfo.FullName} doesn't exist?");
            return;
        }

        var settings =
            JsonSerializer.Deserialize<IntersectSettings>(await File.ReadAllTextAsync(settingsFileInfo.FullName,
                cancellationToken));
        if (settings == null)
        {
            statusContext.Progress(
                $"The settings file {settingsFileInfo.FullName} did not deserialized to valid settings...");
            return;
        }

        foreach (var loopSelected in contents)
        {
            var feature = settings.BufferPointsAndLinesByFeet > 0
                ? loopSelected.FeatureFromPointAsCircle(settings.BufferPointsAndLinesByFeet.Value)
                : loopSelected.FeatureFromPoint();

            var intersectResult = new IntersectResult(feature)
            {
                ContentId = loopSelected.ContentId,
                Description = $"Point Content - {loopSelected.Title ?? "No Title"}"
            };

            intersectResult.OsmIsInPoints.AddRange(loopSelected.PointFromLatitudeLongitude().Coordinate);

            var tagResult = await intersectResult.IntersectionTags(settingsFileInfo.FullName,
                cancellationToken, statusContext.ProgressTracker());

            await FeatureIntersectResultBrowserWindow.CreateInstanceAndShow(tagResult,
                loopSelected.Title ?? "Content has No Title...");
        }
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