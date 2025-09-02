namespace PointlessWaymarks.CmsData.Server;

public class ContentMediaFileResponse
{
    public string ContentType { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FullPath { get; set; } = string.Empty;
}