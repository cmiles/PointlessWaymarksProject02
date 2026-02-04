using Ookii.Dialogs.Wpf;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.FileMetadataDisplay;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;
using System.IO;

namespace PointlessWaymarks.CmsWpfControls.VideoList;

public static class VideoActions
{
    public static string DefaultBracketCode(VideoContent? content)
    {
        return content is null ? string.Empty : $"{BracketCodeVideoEmbed.Create(content)}";
    }

    public static async Task DefaultBracketCodesToClipboard(List<VideoContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(BracketCodeVideoEmbed.Create).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task ExportFiles(List<VideoContent> contents, StatusControlContext statusContext,
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

            var fileToExport = UserSettingsSingleton.CurrentSettings().LocalMediaArchiveVideoContentFile(loopSelected);

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

    public static async Task ImageBracketCodesToClipboard(List<VideoContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = new List<string>();

        var showNoImageWarning = false;

        foreach (var loopSelected in contents)
            if (loopSelected.MainPicture == null)
            {
                showNoImageWarning = true;
                codeList.Add(BracketCodeVideoLinks.Create(loopSelected));
            }
            else
            {
                codeList.Add(BracketCodeVideoImageLink.Create(loopSelected));
            }

        var finalString = string.Join(Environment.NewLine, codeList);

        await ThreadSwitcher.ResumeForegroundAsync();

        if (showNoImageWarning)
            await statusContext.ToastWarning("Not all content had a main image - some bracket codes are text links...");
        else
            await statusContext.ToastSuccess($"To Clipboard {finalString}");

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }


    public static async Task ReportVideoMetadata(List<VideoContent> contents, StatusControlContext statusContext)
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
            fileList.Add(settings.LocalMediaArchiveVideoContentFile(loopContents));

        await FileMetadataDisplayWindow.ImageFileMetadataReports(fileList, settings.FfprobeExe(), statusContext);
    }

    private static async Task TextAndContentRepresentationToClipboard(List<VideoContent> contents,
        string clipboardString, StatusControlContext statusContext)
    {
        await ContentClipboardRepresentation.TextAndContentRepresentationToClipboard(
            contents.Cast<IContentCommon>().ToList(), clipboardString, statusContext);
    }

    public static async Task TextBracketCodesToClipboard(List<VideoContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(BracketCodeVideoLinks.Create).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }
}