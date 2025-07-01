using PointlessWaymarks.AvaloniaLlamaAspects;
using PointlessWaymarks.FeedReaderData.Models;

namespace PointlessWaymarks.FeedReaderAvalonia.Controls;

[NotifyPropertyChanged]
public partial class FeedItemListListItem
{
    public required ReaderFeed DbReaderFeed { get; set; }
    public required ReaderFeedItem DbItem { get; set; }
}