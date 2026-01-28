using System.Text.Json;
using System.Windows;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsData.Server;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;

namespace PointlessWaymarks.CmsWpfControls.ContentList;

public class ContentClipboardRepresentation
{
    public const string ContentClipboardCollectionListFormat = "PointlessWaymarksContentReferenceList";
    public const string ContentClipboardFormat = "PointlessWaymarksContentReference";
    public Guid ContentId { get; set; } = Guid.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FormatIdentifier { get; set; } = ContentClipboardFormat;
    public Guid SiteId { get; set; } = Guid.Empty;
    public string SiteLocalApiUrl { get; set; } = string.Empty;

    public static List<ContentClipboardRepresentation> ClipboardObject(List<IContentCommon> content)
    {
        if (content.Count < 1)
            return [];

        var settings = UserSettingsSingleton.CurrentSettings();

        var returnList = new List<ContentClipboardRepresentation>();

        foreach (var loopContent in content)
            returnList.Add(new ContentClipboardRepresentation
            {
                FormatIdentifier = ContentClipboardFormat,
                SiteId = settings.SettingsId,
                ContentId = loopContent.ContentId,
                ContentType = Db.ContentTypeDisplayString(content),
                SiteLocalApiUrl = PartialContentPreviewServer.PreviewServerLocalApiUrl
            });

        return returnList;
    }

    public static ContentClipboardRepresentation ClipboardObject(IContentCommon? content)
    {
        if (content == null)
            return new ContentClipboardRepresentation();

        var settings = UserSettingsSingleton.CurrentSettings();

        return new ContentClipboardRepresentation
        {
            FormatIdentifier = ContentClipboardFormat,
            SiteId = settings.SettingsId,
            ContentId = content.ContentId,
            ContentType = Db.ContentTypeDisplayString(content),
            SiteLocalApiUrl = PartialContentPreviewServer.PreviewServerLocalApiUrl
        };
    }

    public static async Task TextAndContentRepresentationToClipboard(List<IContentCommon> contents,
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
                ClipboardObject(contents.ToList());

            // Create a DataObject for multiple clipboard formats
            var dataObject = new DataObject();

            // Add the plain text format for compatibility
            dataObject.SetText(clipboardString);

            // Add the ContentClipboardRepresentation as an alternate format
            // Using the ContentClipboardFormat constant as the format name
            var clipboardJson = JsonSerializer.Serialize(clipboardRepresentation);
            dataObject.SetData(ContentClipboardFormat, clipboardJson);

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