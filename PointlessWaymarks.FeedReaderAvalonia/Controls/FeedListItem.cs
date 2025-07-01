namespace PointlessWaymarks.FeedReaderAvalonia.Controls;

public class FeedListItem
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    public int ItemCount { get; set; }
    public bool IsArchived { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Language { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime LastChecked { get; set; }
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }
} 