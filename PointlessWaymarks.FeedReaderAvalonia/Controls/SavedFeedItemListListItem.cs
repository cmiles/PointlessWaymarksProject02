using PointlessWaymarks.AvaloniaLlamaAspects;
using PointlessWaymarks.FeedReaderData.Models;

namespace PointlessWaymarks.FeedReaderAvalonia.Controls;

[NotifyPropertyChanged]
public partial class SavedFeedItemListListItem
{
    public required SavedFeedItem DbItem { get; set; }
    public required ReaderFeed? DbReaderFeed { get; set; }
}