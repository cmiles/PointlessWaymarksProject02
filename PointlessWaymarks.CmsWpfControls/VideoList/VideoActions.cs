using System.IO;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.FileMetadataDisplay;
using PointlessWaymarks.WpfCommon.Status;

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
        var finalString = contents.Aggregate(string.Empty,
            (current, loopSelected) =>
                current + $"{BracketCodeVideoEmbed.Create(loopSelected)}{Environment.NewLine}");

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task ImageBracketCodesToClipboard(List<VideoContent> contents,
        StatusControlContext statusContext)
    {
        var finalString = string.Empty;

        var showNoImageWarning = false;

        foreach (var loopSelected in contents)
            if (loopSelected.MainPicture == null)
            {
                showNoImageWarning = true;
                finalString += $"{BracketCodeVideoLinks.Create(loopSelected)}{Environment.NewLine}";
            }
            else
            {
                finalString += $"{BracketCodeVideoImageLink.Create(loopSelected)}{Environment.NewLine}";
            }

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
        var finalString = contents.Aggregate(string.Empty,
            (current, loopSelected) =>
                current + $"{BracketCodeVideoLinks.Create(loopSelected)}{Environment.NewLine}");

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }
}