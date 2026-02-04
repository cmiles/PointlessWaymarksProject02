using System.IO;
using Ookii.Dialogs.Wpf;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.CmsWpfControls.FileList;

public static class FileActions
{
    public static async Task BracketCodesToClipboard(List<FileContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(BracketCodeFiles.Create).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static string DefaultBracketCode(FileContent? content)
    {
        if (content is null) return string.Empty;

        return content.MainPicture != null
            ? $"{BracketCodeFileImageLink.Create(content)}"
            : $"{BracketCodeFiles.Create(content)}";
    }

    public static async Task DefaultBracketCodesToClipboard(List<FileContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(BracketCodeFiles.Create).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }


    public static async Task DownloadBracketCodesToClipboard(List<FileContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(BracketCodeFileDownloads.Create).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task EmbedBracketCodesToClipboard(List<FileContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(BracketCodeFileEmbed.Create).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task ExportFiles(List<FileContent> contents, StatusControlContext statusContext,
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

            var fileToExport = UserSettingsSingleton.CurrentSettings().LocalMediaArchiveFileContentFile(loopSelected);

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

    public static async Task FileUrlBracketCodesToClipboard(List<FileContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(BracketCodeFileUrl.Create).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task ImageBracketCodesToClipboard(List<FileContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(BracketCodeFileImageLink.Create).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task TextAndContentRepresentationToClipboard(List<FileContent> contents,
        string clipboardString, StatusControlContext statusContext)
    {
        await ContentClipboardRepresentation.TextAndContentRepresentationToClipboard(
            contents.Cast<IContentCommon>().ToList(), clipboardString, statusContext);
    }
}