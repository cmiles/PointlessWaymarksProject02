using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsData.Server;

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
}