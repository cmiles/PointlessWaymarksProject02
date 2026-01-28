using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Xml;
using NetTopologySuite.Features;
using NetTopologySuite.IO;
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
using PointlessWaymarks.SpatialTools;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using Serilog;

namespace PointlessWaymarks.CmsWpfControls.LineList;

public static class LineActions
{
    public static async Task AddIntersectionTags(List<LineContent> contents,
        StatusControlContext statusContext, bool includeOsm, CancellationToken cancellationToken)
    {
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

        List<LineContent> dbEntriesToProcess = new();
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
            var feature = loopSelected.FeatureFromGeoJsonLineAsPolygon(settings.BufferPointsAndLinesByFeet);

            if (feature == null) continue;

            dbEntriesToProcess.Add((LineContent)LineContent.CreateInstance().InjectFrom(loopSelected));
            var intersectResult = new IntersectResult(feature)
            {
                ContentId = loopSelected.ContentId,
                Description = $"Line Content - {loopSelected.Title ?? "No Title"}"
            };

            var lineFeature = loopSelected.FeatureFromGeoJsonLine();
            if (lineFeature is not null)
                intersectResult.OsmIsInPoints.AddRange(LineTools.GetRepresentativePointsFromLine(lineFeature.Geometry));
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
                        $"Processed - {loopSelected.Title} - no tags found - Line {processedCount} of {contents.Count}");
                    continue;
                }

                var tagListForIntersection = Db.TagListParse(loopSelected.Tags);
                tagListForIntersection.AddRange(taggerResult.Tags);
                loopSelected.Tags = Db.TagListJoin(tagListForIntersection);
                loopSelected.LastUpdatedBy = "Feature Intersection Tagger";
                loopSelected.LastUpdatedOn = updateTime;

                var (saveGenerationReturn, _) =
                    await LineGenerator.SaveAndGenerateHtml(loopSelected, DateTime.Now,
                        statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError)
                    //TODO: Need alerting on this that would actually be seen...
                {
                    Log.ForContext("generationError", saveGenerationReturn.GenerationNote)
                        .ForContext("generationException", saveGenerationReturn.Exception?.ToString() ?? string.Empty)
                        .Error(
                            "Line Save Error during Selected Line Feature Intersection Tagging");
                    errorList.Add(
                        $"Save Failed! Line: {loopSelected.Title}, {saveGenerationReturn.GenerationNote}");
                    continue;
                }

                successList.Add(
                    $"{loopSelected.Title} - found Tags {string.Join(", ", taggerResult.Tags)}");
                statusContext.Progress(
                    $"Processed - {loopSelected.Title} - found Tags {string.Join(", ", taggerResult.Tags)} - Line {processedCount} of {contents.Count}");
            }
            catch (Exception e)
            {
                Log.Error(e,
                    $"Line Save Error during Selected Line Feature Intersection Tagging {loopSelected.Title}, {loopSelected.ContentId}");
                errorList.Add(
                    $"Save Failed! Line: {loopSelected.Title}, {e.Message}");
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

    public static string DefaultBracketCode(LineContent? content)
    {
        return content is null ? string.Empty : $"{BracketCodeLines.Create(content)}";
    }

    public static async Task DefaultBracketCodesToClipboard(List<LineContent> contents,
        StatusControlContext statusContext)
    {
        var finalString = contents.Aggregate(string.Empty,
            (current, loopSelected) =>
                current + $"{BracketCodeLines.Create(loopSelected)}{Environment.NewLine}");

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task ElevationChartBracketCodesToClipboard(List<LineContent> contents,
        StatusControlContext statusContext)
    {
        var finalString = contents.Aggregate(string.Empty,
            (current, loopSelected) =>
                current + $"{BracketCodeLineElevationCharts.Create(loopSelected)}{Environment.NewLine}");

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task GeoJsonToClipboard(List<LineContent> contents,
        StatusControlContext statusContext)
    {
        var featureList = new List<IFeature>();
        var warningList = new List<string>();
        var successCounter = 0;

        foreach (var loopSelected in contents)
        {
            var lineFeature = loopSelected.FeatureFromGeoJsonLine();

            if (lineFeature is null)
            {
                warningList.Add(loopSelected.Title ?? "Unknown");
                continue;
            }

            featureList.Add(lineFeature);
            successCounter++;
        }

        var finalString = await GeoJsonTools.SerializeListOfFeaturesCollectionToGeoJson(featureList);

        await ThreadSwitcher.ResumeForegroundAsync();

        Clipboard.SetText(finalString);

        if (successCounter > 0)
            await statusContext.ToastSuccess($"GeoJson To Clipboard for {successCounter} Lines");

        if (warningList.Any())
            await statusContext.ShowMessageWithOkButton("GeoJson Conversion Failures?",
                $"GeoJson Conversion failed for {warningList.Count} items.{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, warningList)}");
    }

    public static async Task LinkBracketCodesToClipboard(List<LineContent> contents,
        StatusControlContext statusContext)
    {
        var finalString = contents.Aggregate(string.Empty,
            (current, loopSelected) =>
                current + $"{BracketCodeLineLinks.Create(loopSelected)}{Environment.NewLine}");

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task ShowIntersectionTagsForSelected(List<LineContent> contents,
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
            var feature = loopSelected.FeatureFromGeoJsonLineAsPolygon(settings.BufferPointsAndLinesByFeet);

            if (feature == null) continue;

            var intersectResult = new IntersectResult(feature)
            {
                ContentId = loopSelected.ContentId,
                Description = $"Line Content - {loopSelected.Title ?? "No Title"}"
            };

            var lineFeature = loopSelected.FeatureFromGeoJsonLine();
            if (lineFeature is not null)
            {
                intersectResult.OsmIsInPoints.AddRange(LineTools.GetRepresentativePointsFromLine(lineFeature.Geometry));

                var tagResult = await intersectResult.IntersectionTags(settingsFileInfo.FullName,
                    cancellationToken, statusContext.ProgressTracker());

                await FeatureIntersectResultBrowserWindow.CreateInstanceAndShow(tagResult,
                    loopSelected.Title ?? "Content has No Title...");
            }
        }
    }

    public static async Task StatsBracketCodesToClipboard(List<LineContent> contents,
        StatusControlContext statusContext)
    {
        var finalString = contents.Aggregate(string.Empty,
            (current, loopSelected) =>
                current + $"{BracketCodeLineStats.Create(loopSelected)}{Environment.NewLine}");

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task TextAndContentRepresentationToClipboard(List<LineContent> contents, string clipboardString,
        StatusControlContext statusContext)
    {
        await ContentClipboardRepresentation.TextAndContentRepresentationToClipboard(
            contents.Cast<IContentCommon>().ToList(), clipboardString, statusContext);
    }

    public static async Task TextStatsBracketCodesToClipboard(List<LineContent> contents,
        StatusControlContext statusContext)
    {
        var finalString = contents.Aggregate(string.Empty,
            (current, loopSelected) =>
                current + $"{BracketCodeLineTextStats.Create(loopSelected)}");

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task ToGpxFile(List<LineContent> contents, StatusControlContext statusContext)
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

        if (string.IsNullOrWhiteSpace(fileName))
        {
            await statusContext.ToastError("No file name?");
            return;
        }

        await ThreadSwitcher.ResumeBackgroundAsync();

        var trackList = contents.Select(x => GpxTools.GpxTrackFromLineFeature(x.FeatureFromGeoJsonLine()!,
            x.RecordingStartedOnUtc, x.Title ?? "New Track", string.Empty,
            x.Title!.Replace(".", string.Empty)
                .Contains(x.Summary.TrimNullToEmpty().Replace(".", string.Empty),
                    StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : x.Summary ?? string.Empty)).ToList();

        var fileStream = new FileStream(fileName, FileMode.OpenOrCreate);

        var writerSettings = new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = true, CloseOutput = true };
        await using var xmlWriter = XmlWriter.Create(fileStream, writerSettings);
        GpxWriter.Write(xmlWriter, null, new GpxMetadata("Pointless Waymarks CMS"), null, null, trackList, null);
        xmlWriter.Close();
    }

    public static async Task ToGpxFiles(List<LineContent> contents,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var fileDialog = new VistaFolderBrowserDialog { Multiselect = false };
        var fileDialogResult = fileDialog.ShowDialog();

        if (!(fileDialogResult ?? false)) return;

        var directory = new DirectoryInfo(fileDialog.SelectedPath);

        if (!directory.Exists)
        {
            await statusContext.ToastError("Directory doesn't exist?");
            return;
        }

        await ThreadSwitcher.ResumeBackgroundAsync();

        foreach (var loopSelected in contents)
        {
            var trackList = GpxTools.GpxTrackFromLineFeature(loopSelected.FeatureFromGeoJsonLine()!,
                loopSelected.RecordingStartedOnUtc, loopSelected.Title ?? "New Track", string.Empty,
                loopSelected.Summary ?? string.Empty).AsList();

            var fileName = UniqueFileTools.UniqueFile(directory, $"{loopSelected.Title!}.gpx");

            if (fileName is null)
            {
                await statusContext.ToastError(
                    $"Couldn't create a unique file name for {loopSelected.Title}?: {fileName}");
                continue;
            }

            var fileStream = new FileStream(fileName.FullName, FileMode.OpenOrCreate);

            var writerSettings = new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = true, CloseOutput = true };
            await using var xmlWriter = XmlWriter.Create(fileStream, writerSettings);
            GpxWriter.Write(xmlWriter, null, new GpxMetadata("Pointless Waymarks CMS"), null, null, trackList, null);
            xmlWriter.Close();
        }
    }
}