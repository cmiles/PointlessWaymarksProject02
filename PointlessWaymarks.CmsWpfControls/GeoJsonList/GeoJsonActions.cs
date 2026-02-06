using System.IO;
using System.Text;
using System.Text.Json;
using Omu.ValueInjecter;
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
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;
using Serilog;

namespace PointlessWaymarks.CmsWpfControls.GeoJsonList;

public static class GeoJsonActions
{
    public static async Task AddIntersectionTags(List<GeoJsonContent> contents,
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

        List<GeoJsonContent> dbEntriesToProcess = new();
        List<IntersectResult> intersectResults = new();

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
            var features = loopSelected.FeaturesFromGeoJson();

            if (!features.Any()) continue;

            dbEntriesToProcess.Add((GeoJsonContent)GeoJsonContent.CreateInstance().InjectFrom(loopSelected));

            var intersectResult = new IntersectResult(features)
            {
                ContentId = loopSelected.ContentId,
                Description = $"GeoJson Content - {loopSelected.Title}"
            };

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
                        $"Processed - {loopSelected.Title} - no tags found - GeoJson {processedCount} of {contents.Count}");
                    continue;
                }

                var tagListForIntersection = Db.TagListParse(loopSelected.Tags);
                tagListForIntersection.AddRange(taggerResult.Tags);
                loopSelected.Tags = Db.TagListJoin(tagListForIntersection);
                loopSelected.LastUpdatedBy = "Feature Intersection Tagger";
                loopSelected.LastUpdatedOn = updateTime;

                var (saveGenerationReturn, _) =
                    await GeoJsonGenerator.SaveAndGenerateHtml(loopSelected, DateTime.Now,
                        statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError)
                    //TODO: Need alerting on this that would actually be seen...
                {
                    Log.ForContext("generationError", saveGenerationReturn.GenerationNote)
                        .ForContext("generationException", saveGenerationReturn.Exception?.ToString() ?? string.Empty)
                        .Error(
                            "GeoJson Save Error during Selected GeoJson Feature Intersection Tagging");
                    errorList.Add(
                        $"Save Failed! GeoJson: {loopSelected.Title}, {saveGenerationReturn.GenerationNote}");
                    continue;
                }

                successList.Add(
                    $"{loopSelected.Title} - found Tags {string.Join(", ", taggerResult.Tags)}");
                statusContext.Progress(
                    $"Processed - {loopSelected.Title} - found Tags {string.Join(", ", taggerResult.Tags)} - GeoJson {processedCount} of {contents.Count}");
            }
            catch (Exception e)
            {
                Log.Error(e,
                    $"GeoJson Save Error during Selected GeoJson Feature Intersection Tagging {loopSelected.Title}, {loopSelected.ContentId}");
                errorList.Add(
                    $"Save Failed! GeoJson: {loopSelected.Title}, {e.Message}");
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

    public static string DefaultBracketCode(GeoJsonContent? content)
    {
        return content is null ? string.Empty : $"{BracketCodeGeoJson.Create(content)}";
    }

    public static async Task DefaultBracketCodesToClipboard(List<GeoJsonContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(BracketCodeGeoJson.Create).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task ExportFiles(List<GeoJsonContent> contents, StatusControlContext statusContext,
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

            var destinationFileName =
                UniqueFileTools.UniqueFile(exportDirectory, $"{loopSelected.Title ?? "PW-GeoJson"}.json");

            await File.WriteAllTextAsync(destinationFileName!.FullName, loopSelected.GeoJson ?? string.Empty,
                cancellationToken);

            exportedCount++;

            lastFile = destinationFileName.FullName;
        }

        if (exportedCount > 0)
        {
            await statusContext.ToastSuccess($"Exported {exportedCount} files to {exportDirectory.FullName}");
            await ProcessHelpers.OpenExplorerWindowForFile(lastFile);
        }
        else
        {
            await statusContext.ToastWarning("No files to export?");
        }
    }

    public static async Task GeoJsonTextToClipboard(List<GeoJsonContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(loopSelected => loopSelected.GeoJson ?? string.Empty)
            .ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task ImageBracketCodesToClipboard(List<GeoJsonContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(BracketCodeGeoJsonImageLink.Create).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task ShowIntersectionTagsForSelected(List<GeoJsonContent> contents,
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
            var features = loopSelected.FeaturesFromGeoJson();

            if (!features.Any()) continue;

            var intersectResult = new IntersectResult(features)
            {
                ContentId = loopSelected.ContentId,
                Description = $"GeoJson Content - {loopSelected.Title}"
            };

            var tagResult = await intersectResult.IntersectionTags(settingsFileInfo.FullName,
                cancellationToken, statusContext.ProgressTracker());

            await FeatureIntersectResultBrowserWindow.CreateInstanceAndShow(tagResult,
                loopSelected.Title ?? "Content has No Title...");
        }
    }

    private static async Task TextAndContentRepresentationToClipboard(List<GeoJsonContent> contents,
        string clipboardString, StatusControlContext statusContext)
    {
        await ContentClipboardRepresentation.TextAndContentRepresentationToClipboard(
            contents.Cast<IContentCommon>().ToList(), clipboardString, statusContext);
    }

    public static async Task TextBracketCodesToClipboard(List<GeoJsonContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(BracketCodeGeoJsonLinks.Create).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }
}