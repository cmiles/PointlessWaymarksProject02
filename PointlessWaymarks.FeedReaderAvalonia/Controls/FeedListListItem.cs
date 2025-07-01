using PointlessWaymarks.AvaloniaLlamaAspects;
using PointlessWaymarks.FeedReaderData.Models;

namespace PointlessWaymarks.FeedReaderAvalonia.Controls;

[NotifyPropertyChanged]
public partial class FeedListListItem
{
    public int UnreadItemsCount { get; set; }
    public int ItemsCount { get; set; }
    public required ReaderFeed DbReaderFeed { get; set; }
}