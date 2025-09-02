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
}