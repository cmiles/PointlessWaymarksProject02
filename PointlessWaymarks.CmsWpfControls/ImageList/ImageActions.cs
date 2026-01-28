using System.IO;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.FileMetadataDisplay;
using PointlessWaymarks.WpfCommon.Status;

namespace PointlessWaymarks.CmsWpfControls.ImageList;

public static class ImageActions
{
    public static async Task ReportVideoMetadata(List<ImageContent> contents, StatusControlContext statusContext)
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

    private static async Task TextAndContentRepresentationToClipboard(List<ImageContent> contents,
        string clipboardString, StatusControlContext statusContext)
    {
        await ContentClipboardRepresentation.TextAndContentRepresentationToClipboard(
            contents.Cast<IContentCommon>().ToList(), clipboardString, statusContext);
    }
}