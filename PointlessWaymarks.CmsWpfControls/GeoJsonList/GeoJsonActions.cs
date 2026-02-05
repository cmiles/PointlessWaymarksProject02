using System.IO;
using Ookii.Dialogs.Wpf;
using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.CmsWpfControls.GeoJsonList;

public static class GeoJsonActions
{
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