using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.WpfCommon.Status;
using System.Linq;

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