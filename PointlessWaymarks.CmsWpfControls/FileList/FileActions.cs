using System.Text.Json;
using System.Windows;
using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;

namespace PointlessWaymarks.CmsWpfControls.FileList;

public static class FileActions
{
    public static async Task BracketCodesToClipboard(List<FileContent> contents,
        StatusControlContext statusContext)
    {
        var finalString = contents.Aggregate(string.Empty,
            (current, loopSelected) =>
                current + $"{BracketCodeFiles.Create(loopSelected)}{Environment.NewLine}");

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
        var finalString = contents.Aggregate(string.Empty,
            (current, loopSelected) =>
                current + $"{BracketCodeFiles.Create(loopSelected)}{Environment.NewLine}");

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }


    public static async Task DownloadBracketCodesToClipboard(List<FileContent> contents,
        StatusControlContext statusContext)
    {
        var finalString = contents.Aggregate(string.Empty,
            (current, loopSelected) =>
                current + $"{BracketCodeFileDownloads.Create(loopSelected)}{Environment.NewLine}");

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task EmbedBracketCodesToClipboard(List<FileContent> contents,
        StatusControlContext statusContext)
    {
        var finalString = contents.Aggregate(string.Empty,
            (current, loopSelected) =>
                current + $"{BracketCodeFileEmbed.Create(loopSelected)}{Environment.NewLine}");

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task FileUrlBracketCodesToClipboard(List<FileContent> contents,
        StatusControlContext statusContext)
    {
        var finalString = contents.Aggregate(string.Empty,
            (current, loopSelected) =>
                current + $"{BracketCodeFileUrl.Create(loopSelected)}{Environment.NewLine}");

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task ImageBracketCodesToClipboard(List<FileContent> contents,
        StatusControlContext statusContext)
    {
        var finalString = contents.Aggregate(string.Empty,
            (current, loopSelected) =>
                current + $"{BracketCodeFileImageLink.Create(loopSelected)}{Environment.NewLine}");

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task TextAndContentRepresentationToClipboard(List<FileContent> contents,
        string clipboardString, StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (contents.Count < 1)
        {
            await statusContext.ToastError("Nothing Selected?");
            return;
        }

        try
        {
            // Get the ContentClipboardRepresentation from ClipboardObject
            var clipboardRepresentation =
                ContentClipboardRepresentation.ClipboardObject(contents.Cast<IContentCommon>().ToList());

            // Create a DataObject for multiple clipboard formats
            var dataObject = new DataObject();

            // Add the plain text format for compatibility
            dataObject.SetText(clipboardString);

            // Add the ContentClipboardRepresentation as an alternate format
            // Using the ContentClipboardFormat constant as the format name
            var clipboardJson = JsonSerializer.Serialize(clipboardRepresentation);
            dataObject.SetData(ContentClipboardRepresentation.ContentClipboardFormat, clipboardJson);

            await ThreadSwitcher.ResumeForegroundAsync();

            // Set the clipboard with multiple formats
            Clipboard.SetDataObject(dataObject, true);

            await statusContext.ToastSuccess($"To Clipboard {clipboardString.TruncateWithEllipses(100)}");
        }
        catch (Exception ex)
        {
            // Fallback to simple text if the rich format fails
            await ThreadSwitcher.ResumeForegroundAsync();
            Clipboard.SetText(clipboardString);
            await statusContext.ToastWarning($"Simple text copied - rich format failed: {ex.Message}");
        }
    }
}