using System.IO;
using System.Text;
using System.Text.Json;
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
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.FileMetadataDisplay;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;
using Serilog;

namespace PointlessWaymarks.CmsWpfControls.ImageList;

public static class ImageActions
{
    public static async Task AddIntersectionTags(List<ImageContent> contents,
        StatusControlContext statusContext, bool includeOsm, CancellationToken cancellationToken)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        List<ImageContent> contentsWithLocation = [];

        foreach (var loopImages in contents)
            if (await loopImages.HasValidLocation())
                contentsWithLocation.Add(loopImages);

        if (!contentsWithLocation.Any())
        {
            await statusContext.ToastError("No Selected Images have valid location data?");
            return;
        }

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

        List<ImageContent> dbEntriesToProcess = [];
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

        foreach (var loopSelected in contentsWithLocation)
        {
            var feature = settings.BufferPointsAndLinesByFeet > 0
                ? loopSelected.FeatureFromPointAsCircle(settings.BufferPointsAndLinesByFeet.Value)
                : loopSelected.FeatureFromPoint();

            if (feature == null) continue;

            dbEntriesToProcess.Add(loopSelected);

            var intersectResult = new IntersectResult(feature)
            {
                ContentId = loopSelected.ContentId, Description = $"Image Content - {loopSelected.Title ?? "No Title"}"
            };

            intersectResult.OsmIsInPoints.AddRange(loopSelected.PointFromLatitudeLongitude()!.Coordinate);

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
                        $"Processed - {loopSelected.Title} - no tags found - Image {processedCount} of {contentsWithLocation.Count}");
                    continue;
                }

                var tagListForIntersection = SlugTagTools.TagListParseToSpacedString(loopSelected.Tags);
                tagListForIntersection.AddRange(taggerResult.Tags);
                loopSelected.Tags = SlugTagTools.TagListJoinToSpacedString(tagListForIntersection);
                loopSelected.LastUpdatedBy = "Feature Intersection Tagger";
                loopSelected.LastUpdatedOn = updateTime;

                var mediaFile = UserSettingsSingleton.CurrentSettings().LocalSiteImageContentFile(loopSelected);

                if (mediaFile == null)
                {
                    errorList.Add(
                        $"No Media File Found for Image: {loopSelected.Title}");
                    continue;
                }

                var (saveGenerationReturn, _) =
                    await ImageGenerator.SaveAndGenerateHtml(loopSelected, mediaFile, false,
                        DateTime.Now, statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError)
                    //TODO: Need alerting on this that would actually be seen...
                {
                    Log.ForContext("generationError", saveGenerationReturn.GenerationNote)
                        .ForContext("generationException", saveGenerationReturn.Exception?.ToString() ?? string.Empty)
                        .Error(
                            "Image Save Error during Selected Image Feature Intersection Tagging");
                    errorList.Add(
                        $"Save Failed! Image: {loopSelected.Title}, {saveGenerationReturn.GenerationNote}");
                    continue;
                }

                successList.Add(
                    $"{loopSelected.Title} - found Tags {string.Join(", ", taggerResult.Tags)}");
                statusContext.Progress(
                    $"Processed - {loopSelected.Title} - found Tags {string.Join(", ", taggerResult.Tags)} - Image {processedCount} of {contentsWithLocation.Count}");
            }
            catch (Exception e)
            {
                Log.Error(e,
                    $"Image Save Error during Selected Image Feature Intersection Tagging {loopSelected.Title}, {loopSelected.ContentId}");
                errorList.Add(
                    $"Save Failed! Image: {loopSelected.Title}, {e.Message}");
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

    public static string DefaultBracketCode(ImageContent? content)
    {
        return content is null ? string.Empty : $"{BracketCodeImages.Create(content)}";
    }

    public static async Task DefaultBracketCodesToClipboard(List<ImageContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(BracketCodeImages.Create).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task ExportFiles(List<ImageContent> contents, StatusControlContext statusContext,
        CancellationToken cancellationToken)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (!contents.Any())
        {
            await statusContext.ToastError("Nothing Selected?");
            return;
        }

        await ThreadSwitcher.ResumeForegroundAsync();

        var dialog = new VistaFolderBrowserDialog
        {
            Description = "Select folder to export files to",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() != true) return;

        var exportDirectory = new DirectoryInfo(dialog.SelectedPath);

        if (!exportDirectory.Exists)
        {
            await statusContext.ToastError("Selected directory does not exist?");
            return;
        }

        await ThreadSwitcher.ResumeBackgroundAsync();

        var exportedCount = 0;
        var lastFile = "";

        foreach (var loopSelected in contents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileToExport = UserSettingsSingleton.CurrentSettings().LocalMediaArchiveImageContentFile(loopSelected);

            if (fileToExport is not { Exists: true }) continue;

            var destinationFileName = UniqueFileTools.UniqueFile(exportDirectory, fileToExport.Name);

            File.Copy(fileToExport.FullName, destinationFileName!.FullName);
            exportedCount++;
            
            lastFile = destinationFileName.FullName;
        }

        if (exportedCount > 0)
        {
            await statusContext.ToastSuccess($"Exported {exportedCount} files to {exportDirectory.FullName}");
            await ProcessHelpers.OpenExplorerWindowForFile(lastFile);
        }
        else
            await statusContext.ToastWarning("No files to export?");
    }

    public static async Task ReportImageMetadata(List<ImageContent> contents, StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (contents.Count < 1)
        {
            await statusContext.ToastError("Nothing Selected?");
            return;
        }

        var settings = UserSettingsSingleton.CurrentSettings();

        var fileList = new List<FileInfo?>();

        foreach (var loopContents in contents)
            fileList.Add(settings.LocalMediaArchiveImageContentFile(loopContents));

        await FileMetadataDisplayWindow.ImageFileMetadataReports(fileList, settings.FfprobeExe(), statusContext);
    }

    public static async Task ShowInGoogleMapsWeb(ImageContent contents,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();
        var validLocation = await contents.HasValidLocation();
        if (!validLocation)
        {
            await statusContext.ToastError("No Valid Location Data?");
            return;
        }

        var mapUrl =
            $"https://www.google.com/maps/search/?api=1&query={contents.Latitude:F5},{contents.Longitude:F5}";

        await ThreadSwitcher.ResumeForegroundAsync();
        ProcessHelpers.OpenUrlInExternalBrowser(mapUrl);
    }

    public static async Task ShowInOsmCycleMap(ImageContent contents,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();
        var validLocation = await contents.HasValidLocation();
        if (!validLocation)
        {
            await statusContext.ToastError("No Valid Location Data?");
            return;
        }

        var mapUrl =
            $"http://www.openstreetmap.org/?mlat={contents.Latitude:F5}&mlon={contents.Longitude:F5}&zoom=13&layers=C";

        await ThreadSwitcher.ResumeForegroundAsync();
        ProcessHelpers.OpenUrlInExternalBrowser(mapUrl);
    }

    public static async Task ShowInPeakFinderWeb(ImageContent contents,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var validLocation = await contents.HasValidLocation();

        if (!validLocation)
        {
            await statusContext.ToastError("No Valid Location Data?");
            return;
        }

        await ThreadSwitcher.ResumeForegroundAsync();

        var peakFinderUrl =
            $"https://www.peakfinder.com/?lat={contents.Latitude:F5}&lng={contents.Longitude:F5}";

        ProcessHelpers.OpenUrlInExternalBrowser(peakFinderUrl);
    }

    public static async Task ShowIntersectionTagsForSelected(List<ImageContent> contents,
        StatusControlContext statusContext, CancellationToken cancellationToken)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        List<ImageContent> contentsWithLocation = [];

        foreach (var loopImages in contents)
            if (await loopImages.HasValidLocation())
                contentsWithLocation.Add(loopImages);

        if (!contentsWithLocation.Any())
        {
            await statusContext.ToastError("No Selected Images have valid location data?");
            return;
        }

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

        foreach (var loopSelected in contentsWithLocation)
        {
            var feature = settings.BufferPointsAndLinesByFeet > 0
                ? loopSelected.FeatureFromPointAsCircle(settings.BufferPointsAndLinesByFeet.Value)
                : loopSelected.FeatureFromPoint();

            if (feature is null) continue;

            var intersectResult = new IntersectResult(feature)
            {
                ContentId = loopSelected.ContentId,
                Description = $"Image Content - {loopSelected.Title ?? "No Title"}"
            };

            intersectResult.OsmIsInPoints.AddRange(loopSelected.PointFromLatitudeLongitude()!.Coordinate);

            var tagResult = await intersectResult.IntersectionTags(settingsFileInfo.FullName,
                cancellationToken, statusContext.ProgressTracker());

            await FeatureIntersectResultBrowserWindow.CreateInstanceAndShow(tagResult,
                loopSelected.Title ?? "Content has No Title...");
        }
    }

    private static async Task TextAndContentRepresentationToClipboard(List<ImageContent> contents,
        string clipboardString, StatusControlContext statusContext)
    {
        await ContentClipboardRepresentation.TextAndContentRepresentationToClipboard(
            contents.Cast<IContentCommon>().ToList(), clipboardString, statusContext);
    }

    public static async Task TextBracketCodesToClipboard(List<ImageContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(BracketCodeImageLinks.Create).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }
}