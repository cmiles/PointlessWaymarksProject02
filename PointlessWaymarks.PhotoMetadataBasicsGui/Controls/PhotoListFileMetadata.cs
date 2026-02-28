using Metalama.Patterns.Observability;

namespace PointlessWaymarks.PhotoMetadataBasicsGui.Controls;

[Observable]
public partial class PhotoListFileMetadata
{
    public double? Elevation { get; set; }
    public double? Latitude { get; set; }
    public string? License { get; set; }
    public double? Longitude { get; set; }
    public string? PhotoCreatedBy { get; set; }
    public DateTime? PhotoCreatedOn { get; set; }
    public DateTime? PhotoCreatedOnUtc { get; set; }
    public double? PhotoDirection { get; set; }
    public string? Summary { get; set; }
    public string? Tags { get; set; }
    public string? Title { get; set; }
}